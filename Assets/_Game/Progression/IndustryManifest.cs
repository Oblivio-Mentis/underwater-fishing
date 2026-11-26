using System;
using UnityEngine;

namespace Raid
{
    public static class IndustryManifest
    {
        public static bool AdvanceFarm(TeamProgress state,TechRig rig,float seconds,float interval,int capacity,bool unattended)
        {
            if(rig.kind!="farm" || rig.brood.Count!=2 || seconds<=0 || interval<=0 || rig.brood[0].species!=rig.brood[1].species) return false;
            if(rig.brood[0].depthTier!=rig.depth || rig.brood[1].depthTier!=rig.depth || rig.brood[0].cargoSize!=1 || rig.brood[1].cargoSize!=1)return false;
            if(rig.brood.Count+rig.contents.Count>=capacity)return false;
            if(!unattended)
            {
                seconds=Mathf.Min(seconds,Mathf.Max(0,rig.care));
                rig.care-=seconds;
            }
            rig.progress+=seconds;
            bool produced=false;
            while(rig.progress>=interval && rig.brood.Count+rig.contents.Count<capacity)
            {
                rig.progress-=interval;
                var fry=rig.brood[0].Copy();
                fry.id=state.NewTechCatchId();fry.caughtBy=null;
                fry.dispatchCommitted=false;fry.destination=CatchDestination.Hold;
                rig.contents.Add(fry);produced=true;
            }
            if(rig.brood.Count+rig.contents.Count>=capacity)rig.progress=0;
            return produced;
        }

        public static int Arrive(TeamProgress team,TechRig courier,ProgressionTuning tuning)
        {
            int moved=0;
            while(courier.contents.Count>0 && team.Deposit(courier.contents[0],tuning))
            {courier.contents.RemoveAt(0);moved++;}
            return moved;
        }
    }
}
