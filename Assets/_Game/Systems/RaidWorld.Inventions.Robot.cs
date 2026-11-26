using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        public void RequestRobotConsole(int serial) => RobotConsoleRequest(serial);
        bool RobotPilotAtStation(DiverMotor diver)
        {
            if(!diver || diver.IsHelpless || diver.IsTrapped || !diver.IsInside)return false;
            foreach(var station in ShipConsole.Stations)
                if(station && station.kind==ShipConsoleKind.SkillTree && station.InRange(diver))return true;
            return false;
        }

        [ServerRpc(RequireOwnership=false)]
        void RobotConsoleRequest(int serial, NetworkConnection sender=null)
        {
            if(team==null || sender==null || !sender.FirstObject || transitionRemaining>0)return;
            var rig=IndustryRig(serial);
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(rig==null || rig.kind!="robot" || !IndustryPresent(rig) || rig.depth!=CurrentDepth ||
                !ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree) || (rig.operatorId>=0 && rig.operatorId!=sender.ClientId))return;
            foreach(var other in team.rigs)
                if(other.kind=="robot" && other.operatorId==diver.OwnerId) {other.operatorId=-1;other.flight="";}
            rig.operatorId=diver.OwnerId;rig.flight="piloted";rig.destination=rig.position;rig.care=0;
            Publish();
        }

        [ServerRpc(RequireOwnership=false)]
        void RobotControls(int serial, Vector3 input, int command,NetworkConnection sender=null)
        {
            if(team==null || sender==null || !sender.FirstObject || transitionRemaining>0)return;
            if (!float.IsFinite(input.x)||!float.IsFinite(input.y)||!float.IsFinite(input.z))return;
            var rig=IndustryRig(serial);
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(rig==null || rig.kind!="robot" || rig.flight!="piloted" || rig.operatorId!=sender.ClientId ||
                !RobotPilotAtStation(diver) || !IndustryPresent(rig) || rig.depth!=CurrentDepth) return;
            if(command==1 || command==2 && IndustryUnlocked(rig,"remote") || command==4 && IndustryUnlocked(rig,"auto"))
            {rig.flight=command==2?"return":command==4?"collect":"";rig.operatorId=-1;rig.destination=rig.position;return;}
            if(command==3 && rig.timer<=0)
            {
                if(rig.targetSerial>0)rig.targetSerial=0;
                else if(!RobotManipulate(rig))return;
                rig.timer=.45f;
            }
            if(command==0)
            {
                // Commands convey direction only. Server tick owns speed, collision and tether displacement.
                rig.destination=rig.position+Vector3.ClampMagnitude(input,1)*2;
                rig.care=.3f;
            }
        }

        bool RobotManipulate(TechRig robot)
        {
            if(CloseToPayload(robot.position,CargoLoadingPosition) && RobotUnload(robot))return true;
            if(IndustryUnlocked(robot,"quality"))
            {
                foreach(var diver in DiverMotor.ServerDivers)
                {
                    if(!diver || !diver.IsTrapped || diver.IsDead || !CloseToPayload(diver.transform.position,robot.position))continue;
                    diver.Rescue();return true;
                }
            }
            if(RobotPickup(robot))return true;
            var other=ClosestIndustryRig(robot,robot.position,2.5f,r=>r.kind!="robot" && r.baitId<0 && r.operatorId<0 && !RigAlreadyTowed(r));
            if(other!=null)
            {
                if(other.contents.Count>0 && other.kind!="cage" && TransferManifest(other,robot))return true;
                if(RigWeight(other)>1+team.TechRank("robot","capacity")*3)return false;
                robot.targetSerial=other.serial;return true;
            }
            return false;
        }

        bool RobotPickup(TechRig rig)
        {
            int free=IndustryFree(rig);
            foreach(var loot in LootActor.VisibleStock)
                if(loot && loot.tier==rig.depth && loot.IsFreeOnSeabed && CloseToPayload(loot.transform.position,rig.position) &&
                    IndustryCanPull(loot.transform.position,rig.position,.08f) && loot.IntoEquipment(rig.contents,free))return true;
            for(int i=0;i<dropped.Count;i++)
            {
                var item=dropped[i];
                if(item.kind!=DroppedKind.Catch || !item.Fish || item.Fish.cargoSize>free || !CloseToPayload(item.position,rig.position) ||
                    !IndustryCanPull(item.position,rig.position,.08f)) continue;
                rig.contents.Add(AttributeCatch(item.Fish.ToCargo(item.id)));dropped.RemoveAt(i);return true;
            }
            return false;
        }

        bool RobotUnload(TechRig rig)
        {
            return IndustryManifest.Arrive(team,rig,progression)>0;
        }

        void TickWorkRobot(TechRig rig,float dt)
        {
            rig.timer=Mathf.Max(0,rig.timer-dt);
            if(rig.flight=="piloted")
            {
                if(!RobotPilotAtStation(IndustryDiver(rig.operatorId)))
                {rig.operatorId=-1;rig.flight="";rig.targetSerial=0;return;}
                if(rig.care<=0)return;
                rig.care-=dt;
            }
            else if(rig.flight=="return" || rig.flight=="collect")
            {
                if(rig.contents.Count>0 || rig.targetSerial>0 || rig.flight=="return")
                {
                    rig.destination=CargoLoadingPosition+Vector3.up;
                    if(CloseToPayload(rig.position,CargoLoadingPosition))
                    {
                        RobotUnload(rig);
                        var load=IndustryRig(rig.targetSerial);
                        if(load!=null && load.kind=="cage" && load.contents.Count>0 && !DeliverTowedCage(load))return;
                        rig.targetSerial=0;
                        if(rig.flight=="return")rig.flight="";
                        return;
                    }
                }
                else if(rig.timer<=0)
                {
                    rig.timer=1f;
                    var source=ClosestIndustryRig(rig,rig.position,IndustryRange(rig),r=>
                        (r.position-CargoLoadingPosition).magnitude<=IndustryRange(rig) &&
                        (CanExport(r,IndustryUnlocked(rig,"delivery")) || r.kind=="cage" && r.mode=="captured" && !RigAlreadyTowed(r) && RigWeight(r)<=1+team.TechRank("robot","capacity")*3 && IndustryUnlocked(r,"delivery")));
                    if(source!=null)
                    {
                        rig.destination=source.position+Vector3.up*.7f;
                        if(CloseToPayload(rig.position,source.position))
                        {
                            if(source.kind=="cage")rig.targetSerial=source.serial;
                            else TransferManifest(source,rig);
                        }
                    }
                    else
                    {
                        LootActor nearest=null;
                        float best=IndustryRange(rig)*IndustryRange(rig);
                        foreach(var stock in LootActor.VisibleStock)
                        {
                            if(!stock || !stock.IsFreeOnSeabed || stock.tier!=rig.depth || (stock.transform.position-CargoLoadingPosition).magnitude>IndustryRange(rig))continue;
                            float d=(stock.transform.position-rig.position).sqrMagnitude;
                            if(d>=best)continue;best=d;nearest=stock;
                        }
                        rig.destination=nearest?nearest.transform.position+Vector3.up*.55f:rig.position;
                        if(nearest && CloseToPayload(rig.position,nearest.transform.position))RobotPickup(rig);
                    }
                }
            }
            else return;
            float range=IndustryRange(rig);
            Vector3 dock=CargoLoadingPosition;
            Vector3 target=dock+Vector3.ClampMagnitude(rig.destination-dock,range);
            Vector3 next=Vector3.MoveTowards(rig.position,target,dt*IndustrialSettings.robotSpeed*IndustryValue(rig,"speed"));
            if(!IndustryCanPull(rig.position,next,.32f))return;
            var payload=IndustryRig(rig.targetSerial);
            if(payload!=null)
            {
                if(!IndustryPresent(payload) || payload.baitId>=0 || payload.operatorId>=0 || payload.depth!=rig.depth || (payload.position-rig.position).magnitude>4f)
                    rig.targetSerial=0;
                else
                {
                    var cargoPosition=payload.position+(next-rig.position);
                    if(!IndustryCanPull(payload.position,cargoPosition,.55f))return;
                    payload.position=cargoPosition;
                }
            }
            rig.position=next;
        }
    }
}
