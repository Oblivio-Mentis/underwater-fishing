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
            float radius = GearRange(rig)*team.TechValue("charge","group",1);
            int fishLimit=Mathf.RoundToInt(Fishery.chargeCap*team.TechValue("charge","limit",1));
            float impactRadius = Fishery.chargeImpactRadius;
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
                if (++affected >= fishLimit) break;
            }
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (!diver || diver.IsDead) continue;
                var target=diver.transform.position+Vector3.up*.35f;
                var offset=target-rig.position;
                float intensity=1-Mathf.Clamp01(offset.magnitude/impactRadius);
                if(intensity<=0 || !ClearGearPath(rig.position,target))continue;
                var away=Vector3.ProjectOnPlane(offset,Vector3.up);
                if(away.sqrMagnitude<.01f)away=diver.transform.forward;
                float impulse=Fishery.chargeForce*Mathf.Sqrt(intensity)*Mathf.Lerp(1f,2.65f,intensity*intensity*intensity);
                diver.BeginBlastImpulse((away.normalized+Vector3.up*Mathf.Lerp(.17f,.26f,intensity))*impulse);
                diver.TakeRigImpact(Fishery.chargeDamage*Mathf.Pow(intensity,1.35f));
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
        Vector3 blastVelocity;

        public struct BlastPoseState
        {
            public uint Sequence;
            public Vector3 Velocity;
            public double StartedAt;
        }

        internal const float BlastPoseDuration=1.1f;
        readonly FishNet.Object.Synchronizing.SyncVar<BlastPoseState> blastPose=new();
        internal BlastPoseState ObservedBlast => blastPose.Value;
        internal double BlastClock => TimeManager==null?Time.timeAsDouble:
            TimeManager.TicksToTime(TimeManager.Tick)+TimeManager.GetTickElapsedAsDouble();

        internal void BeginBlastImpulse(Vector3 velocity)
        {
            blastVelocity=Vector3.ClampMagnitude(blastVelocity+velocity,ActiveFisheryTuning.Get.chargeForce*2.8f);
            if(velocity.sqrMagnitude<6.25f)return;
            blastPose.Value=new BlastPoseState
            {
                Sequence=blastPose.Value.Sequence+1,
                Velocity=blastVelocity,
                StartedAt=BlastClock
            };
        }

        void TickBlastImpulse(float dt)
        {
            if(blastVelocity.sqrMagnitude<.01f){blastVelocity=Vector3.zero;return;}
            var contact=MoveSwimmer(blastVelocity*dt);
            if((contact&CollisionFlags.Above)!=0 && blastVelocity.y>0)blastVelocity.y=0;
            if((contact&CollisionFlags.Below)!=0 && blastVelocity.y<0)blastVelocity.y=0;
            blastVelocity*=Mathf.Exp(-Mathf.Lerp(3.8f,3.45f,Mathf.InverseLerp(10,27,blastVelocity.magnitude))*dt);
        }

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
