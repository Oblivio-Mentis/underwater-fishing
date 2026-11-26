using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        static ActiveFisheryTuning Fishery => ActiveFisheryTuning.Get;
        readonly RaycastHit[] gearHits = new RaycastHit[48];
        readonly Collider[] pulseObjects = new Collider[64];
        readonly HashSet<Rigidbody> pulseBodies = new();

        static DiverMotor RigUser(int id)
        {
            if (id < 0) return null;
            foreach (var diver in DiverMotor.ServerDivers)
                if (diver && diver.OwnerId == id && !diver.IsHelpless && !diver.IsInside) return diver;
            return null;
        }

        bool GearFeature(TechRig r, string upgrade) => team.TechRank(r.kind, upgrade) > 0;
        float GearScale(TechRig r, string upgrade) => Mathf.Max(.01f, team.TechValue(r.kind, upgrade, 1f));
        int GearSlots(TechRig r) => Mathf.Max(1, Mathf.RoundToInt(TechCatalog.Find(r.kind).capacity * GearScale(r,"capacity")));
        float GearRange(TechRig r) => Mathf.Max(.25f, TechCatalog.Find(r.kind).radius * GearScale(r,"range"));
        float GearPeriod(TechRig r) => Mathf.Max(.1f, TechCatalog.Find(r.kind).interval / GearScale(r,"speed"));

        bool UseActiveRig(TechRig rig, DiverMotor diver, string action, Vector3 aim)
        {
            if (!IsServerInitialized || !diver || diver.IsHelpless || diver.IsInside || rig.depth != CurrentDepth ||
                (action != "use" && action != "alternate")) return false;
            if (rig.health <= 0 || rig.mode == "damaged") return false;
            switch (rig.kind)
            {
                case "cage": return UseMonsterCage(rig, diver, action);
                case "net": return UseFishingNet(rig, diver, action);
                case "trap":
                    if (rig.mode != "placed" && rig.mode != "working" && rig.mode != "paused") return false;
                    rig.mode = rig.mode == "paused" ? "working" : "paused";
                    return true;
                default: return false;
            }
        }

        void TickActiveRig(TechRig rig, float dt)
        {
            if (!(dt > 0) || TechCatalog.Find(rig.kind) == null) return;
            if (rig.kind == "trap") { TickFishTrap(rig,dt); return; }
            if (rig.kind == "charge") { TickCharge(rig,dt); return; }
            if (rig.depth != CurrentDepth) return;
            switch (rig.kind)
            {
                case "cage": TickMonsterCage(rig, dt); break;
                case "net": TickFishingNet(rig, dt); break;
            }
        }

        bool ClearGearPath(Vector3 start, Vector3 end)
        {
            Vector3 offset = end - start;
            if (offset.sqrMagnitude < .0001f) return true;
            int hits = Physics.RaycastNonAlloc(start,offset.normalized,gearHits,offset.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                var c = gearHits[i].collider;
                if (c.GetComponentInParent<FishActor>() || c.GetComponentInParent<DiverMotor>()) continue;
                return false;
            }
            return true;
        }

        static void ClearGearHands(TechRig rig)
        {
            rig.operatorId = rig.helperId = rig.baitId = -1;
        }

        void TickCharge(TechRig rig,float dt)
        {
            if(rig.mode=="remote")
            {
                // Preserve already-deployed remote charges from older expeditions.
                rig.mode="fuse";
                rig.timer=TechCatalog.Find("charge").interval;
            }
            if(rig.mode!="fuse")return;
            if(rig.depth==CurrentDepth && rig.anchor.sqrMagnitude>0)
                FlyCharge(rig,Mathf.Min(dt,Mathf.Max(0,rig.timer)));
            rig.timer -= dt;
            if (rig.timer > 0) return;
            rig.mode = "spent";
            // Persist consumption before applying a non-repeatable pulse. A restart cannot fire it again.
            if(!SaveTransaction()){rig.mode="fuse";rig.timer=1;return;}
            rig.anchor = Vector3.zero;
            if(rig.depth!=CurrentDepth)return;
            float radius = GearRange(rig);
            float impactRadius = TechCatalog.Find("charge").radius;
            float keep = Mathf.Clamp01(Fishery.chargeYield * GearScale(rig,"quality"));
            int affected = 0;
            foreach (var fish in FishActor.ServerSchool)
            {
                if (!fish || fish.depthTier != rig.depth || !fish.CanHarvestRig(false) ||
                    (fish.transform.position-rig.position).sqrMagnitude > radius*radius || !ClearGearPath(rig.position,fish.transform.position)) continue;
                Vector3 point = fish.transform.position;
                float yaw = fish.transform.eulerAngles.y;
                var item = fish.HarvestRig(false);
                if (item == null) continue;
                // Damaged catches are lost. Preserved fish retain their individual original cargo ID.
                if (Random.value <= keep && CanRegisterDrop(item.id))
                    RegisterDrop(new DroppedItem {id=item.id,species=fish.NetworkObject,kind=DroppedKind.Catch,position=point,yaw=yaw});
                if (++affected >= Fishery.chargeCap) break;
            }
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (!diver || diver.IsInside || diver.IsDead) continue;
                var offset = diver.transform.position-rig.position;
                float intensity = 1-Mathf.Clamp01(offset.magnitude / impactRadius);
                if (intensity <= 0 || !ClearGearPath(rig.position,diver.transform.position)) continue;
                diver.ApplyExternalMotion((offset.normalized+Vector3.up*.25f)*Fishery.chargeForce*intensity);
                diver.TakeRigImpact(Fishery.chargeDamage*intensity);
            }
            pulseBodies.Clear();
            int count = Physics.OverlapSphereNonAlloc(rig.position,impactRadius,pulseObjects,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for (int i=0;i<count;i++)
            {
                var body = pulseObjects[i].attachedRigidbody;
                if (body && !body.isKinematic && pulseBodies.Add(body) && ClearGearPath(rig.position,body.worldCenterOfMass))
                    body.AddExplosionForce(Fishery.chargeForce,rig.position,impactRadius,.15f,ForceMode.VelocityChange);
            }
            foreach (var other in team.rigs)
                if (other != rig && other.depth == rig.depth && (other.position-rig.position).sqrMagnitude < impactRadius*impactRadius &&
                    ClearGearPath(rig.position,other.position))
                    other.health = Mathf.Clamp01(other.health - Fishery.cageNearbyDamage);
            ThreatDirector.ReportStruggle(rig.position);
            AnnounceRigPulse(rig.position,1);
        }

        void TickFishTrap(TechRig rig,float dt)
        {
            if (rig.health <= 0 || (rig.mode != "placed" && rig.mode != "working")) return;
            rig.timer += dt * (rig.depth > 1 ? Fishery.trapActiveDepthBonus : 1f) *
                (rig.depth == CurrentDepth ? 1f : Fishery.trapIdleYield);
            if (rig.timer < GearPeriod(rig)) return;
            rig.timer = 0;
            if (rig.depth != CurrentDepth)
            {
                // Only a previously productive site simulates the same common local species away from the crew.
                if (rig.localStock == null || rig.localStock.cargoSize != 1 || rig.contents.Count >= GearSlots(rig)) return;
                var sample = rig.localStock.Copy(); sample.id = team.NewTechCatchId();
                rig.contents.Add(sample);
                return;
            }
            if (threats && rig.contents.Count > 0 && (threats.AnglerPosition-rig.position).magnitude < Fishery.trapPredatorDistance &&
                Random.value < Fishery.trapPredatorLoss/GearScale(rig,"reinforced"))
            {
                rig.contents.RemoveAt(rig.contents.Count-1);
                rig.health = Mathf.Max(0,rig.health-Fishery.cageNearbyDamage/GearScale(rig,"reinforced"));
                if (rig.health <= 0) rig.mode = "damaged";
                return;
            }
            if (rig.contents.Count >= GearSlots(rig)) return;
            float radius = Mathf.Max(.3f,GearRange(rig));
            FishActor closest=null;
            foreach (var fish in FishActor.ServerSchool)
            {
                if (!fish || fish.depthTier != rig.depth || !fish.CanHarvestRig(false)) continue;
                float dist = Vector3.Distance(rig.position,fish.transform.position);
                if (dist > radius || !ClearGearPath(rig.position,fish.transform.position)) continue;
                radius = dist; closest = fish;
            }
            if (!closest) return;
            var caught = closest.HarvestRig(false);
            if (caught == null) return;
            rig.contents.Add(caught);
            rig.localStock = caught.Copy();
        }

        [ObserversRpc]
        void AnnounceRigPulse(Vector3 position, byte cue) => FisheryEffects.Play(position,cue);
    }

    public sealed partial class DiverMotor
    {
        internal void TakeRigImpact(float amount)
        {
            if (!IsServerInitialized || IsDead || !(amount > 0)) return;
            var state = _state.Value;
            state.Health = Mathf.Max(0f,state.Health-amount*(1-Mathf.Clamp(Personal(SkillLine.Armor),0,.7f)));
            _state.Value = state;
            if (state.Health <= 0) BeginDeath(false);
        }
    }
}
