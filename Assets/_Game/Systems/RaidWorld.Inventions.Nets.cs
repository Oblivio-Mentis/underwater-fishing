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

    }
}
