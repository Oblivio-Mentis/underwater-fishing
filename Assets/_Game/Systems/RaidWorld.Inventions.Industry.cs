using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        // All velocities below are applied by the host. Geometry checks ignore triggers, swimmers, and the object being pulled.
        internal static bool IndustryCanPull(Vector3 origin, Vector3 destination, float radius)
        {
            var delta = destination - origin;
            if (delta.sqrMagnitude < .0001f) return true;
            var hits = Physics.SphereCastNonAlloc(origin, radius, delta.normalized, pullHits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == pullHits.Length) return false;
            for (int i=0;i<hits;i++)
            {
                var col = pullHits[i].collider;
                if (col && !col.GetComponentInParent<DiverMotor>() && !col.GetComponentInParent<LootActor>() && !col.GetComponentInParent<FishActor>())
                    return false;
            }
            return true;
        }

        static IndustryCalibration IndustrialSettings => IndustryCalibration.Settings;
        static readonly RaycastHit[] pullHits = new RaycastHit[16];
        float IndustryValue(TechRig rig, string id, float fallback = 1) => team.TechValue(rig.kind,id,fallback);
        bool IndustryUnlocked(TechRig rig, string id) => team.TechRank(rig.kind,id)>0;
        float IndustryRange(TechRig rig) => Mathf.Max(1,TechCatalog.Find(rig.kind).range * IndustryValue(rig,"range"));
        float IndustryRadius(TechRig rig) => Mathf.Max(.5f,TechCatalog.Find(rig.kind).radius * IndustryValue(rig,"range"));
        float IndustryPeriod(TechRig rig) => Mathf.Max(.5f,TechCatalog.Find(rig.kind).interval / Mathf.Max(.1f,IndustryValue(rig,"speed")));
        int IndustryCapacity(TechRig rig) => team.TechUnitCapacity(rig.kind);
        int IndustryFree(TechRig rig)
        {
            int space=IndustryCapacity(rig) - rig.brood.Count;
            foreach (var item in rig.contents) space-=item.cargoSize;
            return space;
        }

        static DiverMotor IndustryDiver(int owner)
        {
            foreach (var diver in DiverMotor.ServerDivers)
                if (diver && diver.OwnerId==owner && !diver.IsDead) return diver;
            return null;
        }

        bool IndustryPresent(TechRig rig) => rig.mode!="carried" && rig.mode!="stored" && rig.mode!="damaged" && rig.health>0;
        TechRig IndustryRig(int serial)
        {
            if (serial<=0) return null;
            foreach (var rig in team.rigs) if (rig.serial==serial) return rig;
            return null;
        }

        bool IndustryStock(TechRig rig, DiverMotor diver, bool breeder)
        {
            if (!diver.TryGetComponent<HarpoonTool>(out var tool)) return false;
            if (breeder)
            {
                if (rig.brood.Count>=2 || rig.contents.Count+rig.brood.Count>=IndustryCapacity(rig)) return false;
                bool added=tool.StowSelectedInRig(rig.brood,1,rig.depth,broodSpecies:rig.brood.Count==0?null:rig.brood[0].species,breed:true);
                if (added) rig.care=IndustryPeriod(rig)*IndustrialSettings.careCycles;
                return added;
            }
            var salvage=tool.CarriedSalvage;
            int space=IndustryFree(rig);
            if(rig.kind=="robot")
            {
                float kg=RobotFreeKilograms(rig);
                if(salvage && SalvageKilograms(salvage.electronics)>kg)return false;
                if(!salvage)space=Mathf.Min(space,Mathf.FloorToInt(kg/IndustrialSettings.fishSlotKg));
            }
            return salvage ? salvage.IntoEquipment(rig.contents,space,diver) : tool.StowSelectedInRig(rig.contents,space);
        }

        bool UseIndustrialRig(TechRig rig, DiverMotor diver, string action, Vector3 aim)
        {
            if (rig.kind=="magnet" && rig.Carrying(diver.OwnerId) && action=="use") {rig.flight=rig.flight=="pull"?"":"pull";return true;}
            if (!IndustryPresent(rig) || (action!="use" && action!="alternate")) return false;
            switch(rig.kind)
            {
                case "lure": case "bubbles": case "magnet":
                    if (action=="alternate")
                    {
                        if (rig.kind=="bubbles")
                        {
                            float range=IndustryRange(rig);
                            TechRig first=null,after=null;
                            foreach(var candidate in team.rigs)
                            {
                                if(candidate==rig || candidate.kind!="bubbles" || candidate.depth!=rig.depth || !IndustryPresent(candidate) ||
                                   (candidate.position-rig.position).sqrMagnitude>range*range)continue;
                                if(first==null || candidate.serial<first.serial)first=candidate;
                                if(candidate.serial>rig.targetSerial && (after==null || candidate.serial<after.serial))after=candidate;
                            }
                            var other=after??first;
                            if (other==null) return false;
                            rig.targetSerial=other.serial;
                            return true;
                        }
                        if (!IndustryUnlocked(rig,rig.kind=="lure"?"bait":"select")) return false;
                        rig.selection=(rig.selection+1)%3;
                        return true;
                    }
                    rig.mode=rig.mode=="running"?"placed":"running";
                    rig.care=IndustrialSettings.manualDuration;
                    rig.progress=0;
                    return true;
                case "farm":
                    if (action=="use" && rig.brood.Count<2) return IndustryStock(rig,diver,true);
                    if (rig.brood.Count==0) return false;
                    if(action=="use")IndustryManifest.HarvestSurplus(rig);
                    rig.care=IndustryPeriod(rig)*IndustrialSettings.careCycles;
                    return true;
                case "oxygen":
                    if (action=="alternate" && (rig.position-CargoLoadingPosition).sqrMagnitude<=IndustrialSettings.airFillDistance*IndustrialSettings.airFillDistance)
                    {
                        rig.air=IndustryCapacity(rig);return true;
                    }
                    if (!IndustryUnlocked(rig,"auto") && rig.operatorId>=0 && rig.operatorId!=diver.OwnerId) return false;
                    rig.operatorId=rig.operatorId==diver.OwnerId?-1:diver.OwnerId; return true;
                case "winch":
                    if (action=="alternate") {ClearCable(rig);return true;}
                    if (rig.operatorId>=0 && rig.operatorId!=diver.OwnerId) return false;
                    rig.operatorId=rig.operatorId==diver.OwnerId?-1:diver.OwnerId;
                    // Remote spool lets one diver hook a payload and return to the winding handle.
                    return true;
                case "mail":
                    if (rig.flight=="transit") return false;
                    if (action=="use" && IndustryStock(rig,diver,false)) return true;
                    return LaunchCapsule(rig);
                case "robot":
                    if(action=="alternate" && IndustryUnlocked(rig,"auto"))
                    {rig.flight=rig.flight=="collect"?"":"collect"; rig.operatorId=-1;return true;}
                    if(IndustryStock(rig,diver,false)) return true;
                    if(rig.contents.Count==0 || !diver.TryGetComponent<HarpoonTool>(out var hands)) return false;
                    var item=rig.contents[0];
                    bool received=hands.TakeEquipmentCatch(item);
                    if(!received && item.IsSalvage)
                        foreach(var stock in LootActor.VisibleStock)
                            if(stock && stock.FromEquipmentToHands(item,diver)){received=true;break;}
                    if(received)rig.contents.RemoveAt(0);
                    return received;
            }
            return false;
        }

        static void ClearCable(TechRig rig)
        {
            rig.targetSerial=0;rig.towCargo=0;rig.towDiver=-1;rig.operatorId=-1;rig.progress=0;
        }

        TechRig ClosestIndustryRig(TechRig origin, Vector3 at, float range, System.Predicate<TechRig> filter)
        {
            TechRig found=null;
            float best=range*range;
            foreach(var rig in team.rigs)
            {
                if(rig==origin || rig.depth!=origin.depth || !IndustryPresent(rig) || !filter(rig)) continue;
                float d=(rig.position-at).sqrMagnitude;
                if(d>=best) continue;
                best=d;found=rig;
            }
            return found;
        }

        void TickIndustrialRig(TechRig rig,float dt)
        {
            if (dt<=0)return;
            if(rig.kind=="magnet" && rig.mode=="carried" && rig.flight=="pull" && rig.depth==CurrentDepth)
            {
                rig.progress-=dt;
                if(rig.progress<=0){rig.progress=.45f;MagnetPulse(rig,.45f);}
                var handler=IndustryDiver(rig.operatorId);
                if(handler)handler.equipmentPace *= Mathf.Lerp(1,.45f,Mathf.Clamp01(CargoWeight(rig)/Mathf.Max(1,IndustryCapacity(rig))));
                return;
            }
            if(!IndustryPresent(rig))return;
            bool here=rig.depth==CurrentDepth;
            switch(rig.kind)
            {
                case "farm": TickNursery(rig,dt); break;
                case "mail": TickCapsule(rig,dt); break;
                case "robot": if(here) TickWorkRobot(rig,dt); break;
                case "winch": if(here) TickCable(rig,dt); break;
                case "oxygen": if(here) TickCompressor(rig,dt); break;
                case "lure": case "bubbles": case "magnet":
                    if(!here || rig.mode!="running") break;
                    if(!IndustryUnlocked(rig,"auto") && (rig.care-=dt)<=0) {rig.mode="placed";break;}
                    rig.progress-=dt;
                    if(rig.progress>0)break;
                    float cycle=Mathf.Clamp(IndustryPeriod(rig)/6,.3f,1.2f);
                    rig.progress=cycle;
                    if(rig.kind=="magnet") MagnetPulse(rig,cycle);
                    else SoundAndBubbles(rig,cycle);
                    break;
            }
        }

        void SoundAndBubbles(TechRig rig, float dt)
        {
            float radius=rig.kind=="bubbles"?IndustrialSettings.bubbleThickness:IndustryRadius(rig);
            var center=rig.position+Vector3.up*.8f;
            TechRig pair=null;
            if(rig.kind=="bubbles")
            {
                pair=IndustryRig(rig.targetSerial);
                if (pair==null || pair.kind!="bubbles" || pair.depth!=rig.depth || !IndustryPresent(pair) || (pair.position-center).sqrMagnitude>IndustryRange(rig)*IndustryRange(rig))
                {
                    pair=ClosestIndustryRig(rig,center,IndustryRange(rig),r=>r.kind=="bubbles");
                    rig.targetSerial=pair?.serial??0;
                }
                if(pair==null) return;
            }
            var intake=pair!=null && IndustryUnlocked(rig,"group") ? ClosestIndustryRig(rig,(rig.position+pair.position)*.5f,IndustryRange(rig),r=>r.kind is "net" or "trap" or "vacuum") : null;
            foreach(var fish in FishActor.ServerSchool)
            {
                if(!fish || !fish.IsFreeSwimming || fish.depthTier!=rig.depth) continue;
                Vector3 p=fish.transform.position;
                if(pair==null)
                {
                    if((p-center).sqrMagnitude>radius*radius || (rig.selection==1 && fish.RequiresPartner) || (rig.selection==2 && !fish.RequiresPartner))continue;
                    fish.DrawTowardRig(center,dt*IndustrialSettings.signalPull*IndustryValue(rig,"quality"));
                }
                else
                {
                    if(fish.RequiresPartner)continue;
                    var segment=pair.position-rig.position;
                    float t=Mathf.Clamp01(Vector3.Dot(p-center,segment)/Mathf.Max(.001f,segment.sqrMagnitude));
                    var wall=center+t*segment;
                    wall.y=Mathf.Clamp(p.y,wall.y-.8f,wall.y+IndustrialSettings.bubbleHeight);
                    var away=p-wall;
                    if(away.sqrMagnitude>radius*radius) continue;
                    if(away.sqrMagnitude<.01f)away=Vector3.Cross(segment,Vector3.up);
                    Vector3 steer=away.normalized;
                    if(intake!=null)steer+=Vector3.ProjectOnPlane((intake.position-wall).normalized,steer);
                    fish.DrawTowardRig(p+steer*radius,dt*IndustrialSettings.bubblePush*IndustryValue(rig,"quality"));
                }
            }
            if (pair==null)
            {
                rig.timer-=dt;
                if (rig.timer<=0)
                {
                    ThreatDirector.ReportStruggle(center);
                    AnnounceRigPulse(center,2);
                    rig.timer=IndustryPeriod(rig)/Mathf.Max(.1f,IndustryValue(rig,"select"));
                }
            }
        }

        void TickNursery(TechRig rig, float dt)
        {
            IndustryManifest.AdvanceFarm(team,rig,dt,IndustryPeriod(rig),IndustryCapacity(rig),IndustryUnlocked(rig,"quality"));
            if(IndustryUnlocked(rig,"auto"))IndustryManifest.HarvestSurplus(rig);
        }

        void TickCompressor(TechRig rig, float dt)
        {
            float capacity=IndustryCapacity(rig);
            if(rig.air<0)rig.air=capacity;
            float r=IndustryRadius(rig);
            if(IndustryUnlocked(rig,"quality") && rig.depth==1 && rig.position.y> -IndustrialSettings.regenerationDepth)
                rig.air=Mathf.Min(capacity, rig.air + dt*IndustrialSettings.airRegeneration);
            bool automatic=IndustryUnlocked(rig,"auto");
            var pump=IndustryDiver(rig.operatorId);
            if(!pump || (pump.transform.position-rig.position).sqrMagnitude>r*r || pump.IsHelpless) {pump=null;rig.operatorId=-1;}
            if(!automatic && !pump)return;
            int sockets=Mathf.RoundToInt(IndustryValue(rig,"limit"));
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!diver || (!automatic && diver==pump) || (diver.transform.position-rig.position).sqrMagnitude>r*r)continue;
                float used=diver.TakeStationAir(Mathf.Min(rig.air,IndustrialSettings.airFlow*dt*IndustryValue(rig,"speed")));
                rig.air-=used;
                if(used>0 && --sockets<=0)break;
            }
        }

        static float SalvageKilograms(int electronics) => electronics>0?IndustrialSettings.crateWeight:IndustrialSettings.metalWeight;
        static float CargoKilograms(CatchItem item) => item.IsSalvage?SalvageKilograms(item.electronics):item.cargoSize*IndustrialSettings.fishSlotKg;
        static float CargoWeight(TechRig rig)
        {
            float weight=0;
            foreach(var item in rig.contents)weight+=CargoKilograms(item);
            return weight;
        }

        void MagnetPulse(TechRig rig, float dt)
        {
            int space=IndustryFree(rig);if(space<=0)return;
            float radius=IndustryRadius(rig);
            foreach(var stock in LootActor.VisibleStock)
            {
                if(!stock || !stock.IsFreeOnSeabed || stock.tier!=rig.depth || stock.CarryWeight>space || SalvageKilograms(stock.electronics)>Mathf.Min(IndustrialSettings.maxMagnetLoad*IndustryValue(rig,"reinforced"),IndustryCapacity(rig)-CargoWeight(rig)))continue;
                if((rig.selection==1 && stock.electronics>0) || (rig.selection==2 && stock.electronics==0)) continue;
                var delta=stock.transform.position-rig.position;
                if(delta.sqrMagnitude>radius*radius)continue;
                var dock=rig.position+Vector3.up*.2f;
                if(delta.magnitude<=1 && IndustryCanPull(stock.transform.position,dock,.06f))
                {stock.IntoEquipment(rig.contents,space);return;}
                if(stock.PullWithMagnet(dock,dt*IndustrialSettings.magnetSpeed*IndustryValue(rig,"reinforced")))return;
            }
        }

        bool LaunchCapsule(TechRig rig)
        {
            if(rig.contents.Count==0 || rig.flight=="transit" || (rig.position-CargoLoadingPosition).magnitude>IndustryRange(rig))return false;
            rig.flight="transit";rig.timer=IndustryPeriod(rig);return true;
        }

        bool CanExport(TechRig source,bool industrial)
        {
            if(source.contents.Count==0 || source.kind=="robot" || source.kind=="mail" || source.kind=="cage")return false;
            return (source.kind!="farm" || IndustryUnlocked(source,"auto")) && (source.kind=="trap" || industrial) && IndustryUnlocked(source,"delivery");
        }

        void TickCapsule(TechRig rig,float dt)
        {
            if(rig.flight=="transit")
            {
                rig.timer=Mathf.Max(0,rig.timer-dt);
                if(rig.timer>0)return;
                IndustryManifest.Arrive(team,rig,progression,SaveTransaction);
                if(rig.contents.Count==0)rig.flight="";
                return;
            }
            if(!IndustryUnlocked(rig,"auto") && !IndustryUnlocked(rig,"group"))return;
            rig.timer-=dt;
            if(rig.timer>0)return;
            rig.timer=IndustryPeriod(rig)/4;
            if((rig.position-CargoLoadingPosition).sqrMagnitude>IndustryRange(rig)*IndustryRange(rig))return;
            var source=ClosestIndustryRig(rig,rig.position,IndustrialSettings.transferReach,other=>CanExport(other,IndustryUnlocked(rig,"group")));
            if(source!=null)TransferManifest(source,rig);
            LaunchCapsule(rig);
        }

        bool TransferManifest(TechRig source,TechRig target)
        {
            int space=IndustryFree(target);
            float kg=target.kind=="robot"?RobotFreeKilograms(target):float.PositiveInfinity;
            bool moved=false;
            for(int i=0;i<source.contents.Count && space>0;)
            {
                var item=source.contents[i];
                float mass=CargoKilograms(item);
                if(item.cargoSize>space || mass>kg) {i++;continue;}
                source.contents.RemoveAt(i);target.contents.Add(item);space-=item.cargoSize;kg-=mass;moved=true;
            }
            return moved;
        }
    }
}
