using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        // Closed seine volume is bounded by the two paths traced by its ends, not by a radius from the operator.
        sealed class SeineSweep
        {
            public readonly List<Vector3> left = new(), right = new();
            public bool stretched;
            public void Sample(Vector3 a,Vector3 b)
            {
                if (left.Count > 0 && (left[^1]-a).sqrMagnitude < .64f && (right[^1]-b).sqrMagnitude < .64f) return;
                if (left.Count >= 80) { left.RemoveAt(1); right.RemoveAt(1); }
                left.Add(a); right.Add(b);
            }
            public bool Contains(Vector3 p, float verticalReach)
            {
                int sides = left.Count;
                if (sides < 3) return false;
                Vector3 At(int i) => i < sides ? left[i] : right[2*sides-i-1];
                bool inside = false;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (int i=0,j=2*sides-1;i<2*sides;j=i++)
                {
                    var a=At(i); var b=At(j);
                    minY=Mathf.Min(minY,a.y); maxY=Mathf.Max(maxY,a.y);
                    if ((a.z>p.z)!=(b.z>p.z) && p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x) inside=!inside;
                }
                return inside && p.y >= minY-verticalReach && p.y <= maxY+verticalReach;
            }
        }
        readonly Dictionary<int,SeineSweep> netSweeps = new();

        bool UseFishingNet(TechRig rig,DiverMotor diver,string action)
        {
            if (rig.mode == "placed" && action == "alternate" && GearFeature(rig,"compact"))
            {
                rig.mode="working"; rig.timer=0; return true;
            }
            if (rig.mode=="working" && action=="alternate") { rig.mode="placed"; return true; }
            if (rig.mode == "placed" && action == "use")
            {
                rig.operatorId=diver.OwnerId;rig.helperId=-1;rig.mode="netting";rig.anchor=rig.position;
                netSweeps[rig.serial]=new SeineSweep();
                return true;
            }
            if (rig.mode != "netting") return false;
            if (rig.operatorId == diver.OwnerId || rig.helperId == diver.OwnerId)
            {
                if (action=="alternate") return FinishNet(rig);
                DropNet(rig);return true;
            }
            if (rig.helperId>=0 || action!="use")return false;
            rig.helperId=diver.OwnerId;return true;
        }

        void DropNet(TechRig r)
        {
            netSweeps.Remove(r.serial); ClearGearHands(r); r.mode=r.health>0?"placed":"damaged"; r.progress=r.timer=0;
        }

        bool FinishNet(TechRig r)
        {
            var a=RigUser(r.operatorId);var b=RigUser(r.helperId);
            if (!a || !b || !netSweeps.TryGetValue(r.serial,out var sweep) || !sweep.stretched ||
                Vector3.Distance(a.transform.position,b.transform.position)>Fishery.netCloseDistance) return false;
            foreach (var fish in FishActor.ServerSchool)
            {
                if (r.contents.Count >= GearSlots(r)) break;
                if (!fish || !fish.CanHarvestRig(true) || !sweep.Contains(fish.transform.position,Fishery.netThickness) ||
                    !ClearGearPath(r.position,fish.transform.position)) continue;
                if (fish.RequiresPartner)
                {
                    r.health = Mathf.Max(0,r.health-Fishery.netDamage/GearScale(r,"reinforced"));
                    if (r.health<=0) break;
                    continue;
                }
                var item=fish.HarvestRig(false);
                if (item!=null) r.contents.Add(item);
            }
            DropNet(r);AnnounceRigPulse(r.position,2);return true;
        }

        void TickFishingNet(TechRig r,float dt)
        {
            if (r.mode=="working" && GearFeature(r,"compact"))
            {
                if (r.contents.Count>=GearSlots(r)) return;
                r.timer+=dt;
                if (r.timer<GearPeriod(r))return;
                r.timer=0;
                foreach (var f in FishActor.ServerSchool)
                {
                    if (!f || !f.CanHarvestRig(false) || (f.transform.position-r.position).sqrMagnitude>GearRange(r)*GearRange(r) ||
                        !ClearGearPath(r.position,f.transform.position))continue;
                    var item=f.HarvestRig(false);
                    if(item!=null)r.contents.Add(item);
                    break;
                }
                return;
            }
            if(r.mode!="netting") return;
            var first=RigUser(r.operatorId); var second=RigUser(r.helperId);
            if(!first || (r.helperId>=0 && !second) || !netSweeps.TryGetValue(r.serial,out var sweep))
            {DropNet(r);return;}
            var a=first.transform.position;var b=second?second.transform.position:r.anchor;
            float distance=Vector3.Distance(a,b),limit=Mathf.Max(1,TechCatalog.Find(r.kind).range*GearScale(r,"range"));
            if(distance>limit || !ClearGearPath(a,b))
            {DropNet(r);return;}
            r.position=(a+b)*.5f;
            r.progress=Mathf.Clamp01(distance/limit);
            if(!second)return;
            r.anchor=b;
            sweep.Sample(a,b);
            if (distance>=Fishery.netMinimumSpan) sweep.stretched=true;
            if (sweep.stretched && sweep.left.Count>=3 && distance<Fishery.netCloseDistance && GearFeature(r,"auto"))
                FinishNet(r);
        }

        bool UseVacuum(TechRig r,DiverMotor d,string action)
        {
            if(r.mode=="carried")return false;
            if(r.mode=="jammed")
            {
                if(action!="alternate" || (d.transform.position-r.position).sqrMagnitude>9)return false;
                r.mode="placed";r.timer=0;ClearGearHands(r);return true;
            }
            if(r.mode=="working")
            {
                if(action!="alternate")return false;
                r.mode="placed";return true;
            }
            if(r.mode=="placed")
            {
                if(action=="alternate" && GearFeature(r,"auto"))
                {r.mode="working";r.anchor=d.AimDirection;r.timer=0;return true;}
                if(action!="use")return false;
                r.mode="pumping";r.operatorId=d.OwnerId;r.helperId=-1;r.anchor=d.AimDirection;r.timer=0;
                return true;
            }
            if(r.mode!="pumping")return false;
            if(r.operatorId==d.OwnerId || r.helperId==d.OwnerId)
            {
                ClearGearHands(r);r.mode="placed";
                // Break the stream when the pump stops, so a teammate caught in it drifts free.
                foreach(var other in DiverMotor.ServerDivers)
                    if(other && other!=d && !other.IsInside && Vector3.Distance(other.transform.position,r.position)<Fishery.vacuumInlet*2)
                        other.ApplyExternalMotion(r.anchor.normalized*Fishery.vacuumPush);
                return true;
            }
            if(r.helperId>=0)return false;
            r.helperId=d.OwnerId;return true;
        }

        void TickVacuum(TechRig r,float dt)
        {
            if(r.mode=="jammed"){r.timer-=dt;if(r.timer<=0){r.mode="placed";ClearGearHands(r);}return;}
            if(r.mode!="pumping" && r.mode!="working")return;
            var pilot=RigUser(r.operatorId);var helper=RigUser(r.helperId);
            bool stationary=r.mode=="working" && GearFeature(r,"auto");
            if(!stationary)
            {
                if(!pilot || Vector3.Distance(r.position,pilot.transform.position)>Mathf.Max(3,TechCatalog.Find(r.kind).range))
                {r.mode="placed";ClearGearHands(r);return;}
                if(!helper && !GearFeature(r,"compact"))return;
                if(helper && (helper.transform.position-r.position).sqrMagnitude>16){r.helperId=-1;return;}
                if(helper || GearFeature(r,"compact"))
                {
                    var carrier=helper?helper:pilot;
                    var desired=carrier.transform.position+carrier.transform.forward+Vector3.down*.2f;
                    var next=Vector3.MoveTowards(r.position,desired,dt*Fishery.vacuumPull*.35f);
                    if(ClearGearPath(r.position,next))r.position=next;
                }
                r.anchor=pilot.AimDirection;
            }
            if(r.contents.Count>=GearSlots(r))return;
            Vector3 direction=r.anchor.sqrMagnitude>.01f?r.anchor.normalized:Vector3.forward;
            Vector3 tip=stationary?r.position+direction*.8f:pilot.transform.position+Vector3.up*.3f+direction;
            float reach=GearRange(r),strength=Fishery.vacuumPull*GearScale(r,"speed");
            r.timer=Mathf.Max(0,r.timer-dt);
            foreach(var fish in FishActor.ServerSchool)
            {
                if(!fish || !fish.CanHarvestRig(true))continue;
                var delta=fish.transform.position-tip;
                if(delta.sqrMagnitude>reach*reach || Vector3.Dot(delta.normalized,direction)<Fishery.vacuumReachCone ||
                    !ClearGearPath(tip,fish.transform.position))continue;
                if(delta.magnitude>Fishery.vacuumInlet){fish.DrawTowardRig(tip,strength*dt);continue;}
                if(fish.RequiresPartner && !GearFeature(r,"range"))
                {r.mode="jammed";r.timer=Fishery.vacuumJamTime;AnnounceRigPulse(r.position,4);return;}
                if(r.timer>0 || r.contents.Count>=GearSlots(r)) continue;
                var item=fish.HarvestRig(!stationary && GearFeature(r,"range"));
                if(item!=null){r.contents.Add(item);r.timer=GearPeriod(r);}
            }
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!diver || diver==pilot || diver==helper || diver.IsInside || diver.IsDead)continue;
                var delta=diver.transform.position-tip;
                if(delta.sqrMagnitude>reach*reach || Vector3.Dot(delta.normalized,direction)<Fishery.vacuumReachCone ||
                    !ClearGearPath(tip,diver.transform.position))continue;
                diver.ApplyExternalMotion(-delta.normalized*(.45f*strength*dt));
            }
        }
    }
}
