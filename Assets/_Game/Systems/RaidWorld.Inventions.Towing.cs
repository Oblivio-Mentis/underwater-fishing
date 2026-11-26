using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        static bool CloseToPayload(Vector3 a,Vector3 b) => (a-b).sqrMagnitude<=9f;
        bool RigAlreadyTowed(TechRig rig) => team.rigs.Exists(other=> (other.kind=="winch" || other.kind=="robot") && other.targetSerial==rig.serial);
        static float RigWeight(TechRig rig)
        {
            float weight=rig.kind is "winch" or "net" or "vacuum" or "cage" ? IndustrialSettings.heavyRigKg : IndustrialSettings.lightRigKg;
            foreach(var item in rig.contents)weight+=CargoKilograms(item);
            foreach(var item in rig.brood)weight+=CargoKilograms(item);
            return weight;
        }

        [ServerRpc(RequireOwnership=false)]
        void HookIndustryCable(int serial, NetworkConnection sender=null)
        {
            if(team==null || sender==null || !sender.FirstObject || transitionRemaining>0)return;
            var owner=sender.FirstObject.GetComponent<DiverMotor>();
            var cable=IndustryRig(serial);
            if(!owner || owner.IsDead || owner.IsInside || cable==null || cable.kind!="winch" ||
                !IndustryPresent(cable) || cable.depth!=CurrentDepth || (cable.position-owner.transform.position).magnitude>IndustryRange(cable))return;
            if(cable.towDiver==owner.OwnerId) { ClearCable(cable);return; }
            if(cable.targetSerial!=0 || cable.towDiver>=0 || cable.towCargo!=0)return;
            if(!IndustryUnlocked(cable,"remote") && cable.operatorId==owner.OwnerId)return;
            var target=ClosestIndustryRig(cable,owner.transform.position,3f,r=>r.kind!="winch" && r.kind!="robot" && r.baitId<0 && !RigAlreadyTowed(r));
            if(target!=null)cable.targetSerial=target.serial;
            else if(TryAimDrop(owner,out var droppedItem) && droppedItem.kind==DroppedKind.Catch && CloseToPayload(owner.transform.position,droppedItem.position))
                {if(team.rigs.Exists(other=>other.towCargo==droppedItem.id))return;cable.towCargo=droppedItem.id;}
            else {if(team.rigs.Exists(other=>other.towDiver==owner.OwnerId))return;cable.towDiver=owner.OwnerId;} // A diver hooks their own harness. Others cannot attach it remotely.
            if(IndustryUnlocked(cable,"remote"))cable.operatorId=owner.OwnerId;
            cable.progress=0;
            Publish();
        }

        void TickCable(TechRig cable,float dt)
        {
            bool automatic=IndustryUnlocked(cable,"auto");
            var driver=IndustryDiver(cable.operatorId);
            if(!driver || driver.IsHelpless || driver.IsInside || (!IndustryUnlocked(cable,"remote") && !CloseToPayload(driver.transform.position,cable.position)))
            {cable.operatorId=-1;driver=null;}
            if(!driver && !automatic)return;
            var target=IndustryRig(cable.targetSerial);
            if(target==null && automatic && IndustryUnlocked(cable,"delivery") && cable.towCargo==0 && cable.towDiver<0)
            {
                target=ClosestIndustryRig(cable,cable.position,IndustryRange(cable),x=>x.kind=="cage"&& x.contents.Count>0 && x.baitId<0 && x.mode=="captured" && IndustryUnlocked(x,"delivery") && !RigAlreadyTowed(x) &&
                    (x.position-cable.position).sqrMagnitude>4f);
                cable.targetSerial=target?.serial??0;
            }
            var passenger=IndustryDiver(cable.towDiver);
            int index=cable.towCargo>0?DropIndex(cable.towCargo):-1;
            if(target==null && passenger==null && index<0)
            {
                cable.targetSerial=0;cable.towDiver=-1;cable.towCargo=0;return;
            }
            if(target!=null && (!IndustryPresent(target) || target.depth!=cable.depth || target.baitId>=0) || passenger && passenger.IsInside)
            {ClearCable(cable);return;}
            Vector3 position=target!=null?target.position:passenger?passenger.transform.position:dropped[index].position;
            Vector3 towards=cable.position+Vector3.up*.15f;
            float distance=Vector3.Distance(position,towards);
            if(distance<1.4f)
            {
                if(target!=null && target.kind=="cage" && target.contents.Count>0 && (target.position-CargoLoadingPosition).magnitude<=tuning.interactionRange)
                    DeliverTowedCage(target);
                ClearCable(cable);return;
            }
            if(distance>IndustryRange(cable)*1.1f){ClearCable(cable);return;}
            float mass=target!=null?RigWeight(target):passenger?IndustrialSettings.diverKg:IndustrialSettings.fishSlotKg*Mathf.Max(1,dropped[index].Fish?dropped[index].Fish.cargoSize:1);
            float speed=IndustrialSettings.towSpeed*IndustryValue(cable,"speed")*Mathf.Min(1,IndustrialSettings.heavyRigKg*IndustryValue(cable,"reinforced")/mass);
            Vector3 next=Vector3.MoveTowards(position,towards,speed*dt);
            if(mass>TechCatalog.Find("winch").capacity*IndustryValue(cable,"reinforced") ||
                !IndustryCanPull(position,next,target!=null?.45f:.16f) || !IndustryCanPull(position+Vector3.up*.2f,towards,.03f))
            {
                cable.progress+=dt;
                if(cable.progress>=IndustrialSettings.overloadSeconds) {cable.health=Mathf.Max(.05f,cable.health-.2f);ClearCable(cable);}
                return;
            }
            cable.progress=0;
            if(target!=null)target.position=next;
            else if(passenger)passenger.ApplyTetherMotion(next-position);
            else {var item=dropped[index];item.position=next;dropped[index]=item;}
        }

        bool DeliverTowedCage(TechRig cage)
        {
            if(team.SettleRigCargo(cage,progression,false,SaveTransaction)==0)return false;
            if(cage.contents.Count==0)
            {
                cage.mode="placed";cage.operatorId=cage.helperId=cage.baitId=-1;cage.progress=cage.timer=0;
            }
            return true;
        }
    }
}
