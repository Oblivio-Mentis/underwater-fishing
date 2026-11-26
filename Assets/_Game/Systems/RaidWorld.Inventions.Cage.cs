using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        internal bool IsCageBait(DiverMotor diver)
        {
            if (!diver || team?.rigs == null) return false;
            foreach (var r in team.rigs)
                if (r.kind == "cage" && r.depth == CurrentDepth && r.baitId == diver.OwnerId &&
                    (r.mode == "armed" || r.mode == "closing")) return true;
            return false;
        }

        bool UseMonsterCage(TechRig rig, DiverMotor diver,string action)
        {
            if (rig.mode == "carried" || rig.contents.Count > 0) return false;
            if (action == "alternate")
            {
                if (rig.mode == "armed" || rig.mode == "closing")
                {
                    ClearGearHands(rig); rig.mode = "placed"; rig.timer = rig.progress = 0;
                    AnnounceRigPulse(rig.position,2);
                    return true;
                }
                if (rig.mode != "placed" || !GearFeature(rig,"bait")) return false;
                rig.baitId = -1; rig.operatorId = diver.OwnerId;
                rig.mode = "armed"; rig.progress = rig.timer = 0;
                AnnounceRigPulse(rig.position,2);
                return true;
            }
            if (rig.mode == "armed")
            {
                // Anyone holding the local remote, including the volunteer, may close the jaws.
                if (rig.operatorId >= 0 && rig.operatorId != diver.OwnerId && rig.baitId != diver.OwnerId) return false;
                rig.operatorId = diver.OwnerId;
                return BeginCageClose(rig);
            }
            if (rig.mode != "placed" || diver.IsTrapped || IsCageBait(diver) ||
                (diver.transform.position-rig.position).magnitude > Fishery.cageSafeLeash) return false;
            rig.baitId = diver.OwnerId; rig.operatorId = -1; rig.helperId = -1;
            rig.progress = rig.timer = 0;
            rig.mode = "armed";
            AnnounceRigPulse(rig.position,2);
            return true;
        }

        bool BeginCageClose(TechRig rig)
        {
            if (rig.mode != "armed") return false;
            rig.mode = "closing";
            rig.timer = Mathf.Max(.05f, TechCatalog.Find("cage").interval) / GearScale(rig,"speed");
            AnnounceRigPulse(rig.position,3);
            return true;
        }

        CageSpecies CageTarget(TechRig rig)
        {
            if (!threats) return null;
            int size = team.TechRank("cage","capacity");
            foreach (var species in Fishery.monsters)
            {
                if (size < species.minSize || (rig.baitId < 0 && !species.allowAuto)) continue;
                if (threats.CageAnimalNear(species,rig.position+Vector3.up*Fishery.cageTetherHeight,
                    GearRange(rig))) return species;
            }
            return null;
        }

        void TickMonsterCage(TechRig rig,float dt)
        {
            if (rig.contents.Count > 0)
            {
                // Even a parked cage has a live occupant. Damage persists through dropping and regripping.
                float strength = GearScale(rig,"reinforced");
                rig.health = Mathf.Max(0,rig.health-dt/(Fishery.cageEscapeSeconds*strength*GearScale(rig,"quality")));
                rig.timer += dt;
                if (rig.timer >= Fishery.cageStrugglePeriod)
                {
                    rig.timer = 0;
                    int direction = (rig.cycle++ & 1) == 0 ? 1 : -1;
                    if (threats && (threats.AnglerPosition-rig.position).sqrMagnitude < Fishery.trapPredatorDistance*Fishery.trapPredatorDistance)
                        rig.health = Mathf.Max(0,rig.health-Fishery.cageNearbyDamage/strength);
                    var pull = Vector3.right*(direction*Fishery.cageNearbyDamage);
                    RigUser(rig.operatorId)?.ApplyTetherMotion(pull);
                    RigUser(rig.helperId)?.ApplyTetherMotion(pull);
                    AnnounceRigPulse(rig.position,4);
                }
                if (rig.health <= 0)
                {
                    if (threats) threats.ReleaseCageAnimal(rig.contents[0].species,rig.position+Vector3.up*Fishery.cageTetherHeight);
                    rig.contents.Clear(); ClearGearHands(rig); rig.mode = "damaged"; rig.timer = rig.progress = 0;
                    AnnounceRigPulse(rig.position,3);
                }
                return;
            }
            if (rig.mode == "closed" || rig.mode == "captured")
            {
                rig.timer -= dt;
                if (rig.timer <= 0) { rig.mode = "placed"; rig.timer = 0; }
                return;
            }
            if (rig.mode == "placed" && GearFeature(rig,"auto") && GearFeature(rig,"bait"))
            { rig.mode = "armed"; rig.baitId = -1; rig.progress = 0; }
            if (rig.mode != "armed" && rig.mode != "closing") return;
            var bait = RigUser(rig.baitId);
            if ((rig.baitId >= 0 && (!bait || bait.IsHelpless ||
                (bait.transform.position-rig.position).sqrMagnitude > Fishery.cageSafeLeash*Fishery.cageSafeLeash)) ||
                (rig.baitId < 0 && !GearFeature(rig,"bait")))
            {
                ClearGearHands(rig); rig.mode = "placed"; rig.progress = rig.timer = 0; return;
            }
            Vector3 point = rig.position + Vector3.up*Fishery.cageTetherHeight;
            if (bait) bait.ApplyTetherMotion(Vector3.ClampMagnitude(point-bait.transform.position,Fishery.cageTetherSpeed*dt));
            if (rig.mode == "closing")
            {
                rig.timer -= dt;
                if (rig.timer > 0) return;
                var species = CageTarget(rig);
                if (species != null && threats.TakeCageAnimal(species,point,GearRange(rig)))
                {
                    rig.contents.Add(species.Catch(team.NewTechCatchId(),rig.depth));
                    rig.mode = "captured"; rig.timer = 0;
                    message.Value = "Клетка полна. Буксируйте её к грузовому подъёмнику.";
                }
                else
                {
                    rig.mode = "closed"; rig.timer = Fishery.cageLatchCooldown;
                    message.Value = "Створки закрылись впустую.";
                }
                ClearGearHands(rig); rig.progress = 0; AnnounceRigPulse(rig.position,4);
                return;
            }
            rig.progress -= dt;
            if (rig.progress <= 0)
            {
                rig.progress = Fishery.cageLurePulse;
                if (threats) threats.AdvertiseCage(point,bait,Fishery.cageLureRadius,bait == null,GearFeature(rig,"capacity"));
            }
            if (GearFeature(rig,"select") && CageTarget(rig) != null) BeginCageClose(rig);
        }
    }
}
