using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        [ServerRpc] public void CmdTechBuy(string id,string upgrade,int expectedRank,int revision) =>
            RaidWorld.Instance?.BuyTechnology(this,id,upgrade,expectedRank,revision);
        [ServerRpc] public void CmdTechUse(int serial,string action) => RaidWorld.Instance?.UseTechnology(this,serial,action);
        internal float equipmentPace=1;
    }

    public sealed partial class RaidWorld
    {
        readonly Dictionary<int,float> rigRequests=new();
        bool RigRequest(DiverMotor diver)
        {
            if(!IsServerInitialized || team==null || saveFailed || transitionRemaining>0 || !diver || diver.IsHelpless ||
               diver.Owner?.IsAuthenticated!=true || diver.Owner.FirstObject!=diver.NetworkObject) return false;
            if(rigRequests.TryGetValue(diver.OwnerId,out float previous) && Time.unscaledTime-previous<.18f)return false;
            rigRequests[diver.OwnerId]=Time.unscaledTime;
            return true;
        }

        internal void BuyTechnology(DiverMotor diver,string id,string upgrade,int rank,int revision)
        {
            if(!RigRequest(diver) || !ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree))return;
            bool ok=team.TryBuyTech(id,upgrade,rank,revision,SaveTransaction,out var reason);
            if(ok){Publish();diver.ConfirmInteraction();}
            SkillReply(diver.Owner,ok,reason);
        }

        bool Engaged(int owner) => team.rigs.Exists(r=>r.operatorId==owner || r.helperId==owner || r.baitId==owner);
        internal bool HaulingEquipment(DiverMotor diver)
        {
            if(!diver || !NetworkObject || diver.OwnerId<0)return false;
            var state=IsServerInitialized?team:view;
            if(state?.rigs==null)return false;
            foreach(var r in state.rigs)if(r.Carrying(diver.OwnerId))return true;
            return false;
        }

        void ReleaseTechOwner(int owner)
        {
            if(team?.rigs==null || owner<0)return;
            rigRequests.Remove(owner);
            CancelRackOrder(owner);
            foreach(var r in team.rigs)
            {
                if(r.mode=="netting" && (r.operatorId==owner || r.helperId==owner)){DropNet(r);continue;}
                if(r.baitId==owner){r.Detach();r.mode="placed";r.progress=r.timer=0;}
                if(r.helperId==owner)r.helperId=-1;
                if(r.towDiver==owner)r.towDiver=-1;
                if(r.operatorId!=owner)continue;
                if(r.mode=="carried" && r.helperId>=0){r.operatorId=r.helperId;r.helperId=-1;continue;}
                if(r.flight=="piloted")LoseRobotSignal(r);
                if(r.baitId>=0)r.operatorId=-1;
                else r.Detach();
            }
        }
        static DiverMotor TechDiver(int id)
        {
            if(id<0)return null;
            foreach(var diver in DiverMotor.ServerDivers)if(diver && diver.OwnerId==id && !diver.IsHelpless)return diver;
            return null;
        }
        static bool Heavy(TechRig rig) => rig.kind is "cage" or "net" or "winch" or "vacuum" || rig.contents.Count>2;
        static float Footprint(string kind) => kind=="cage"?.82f:kind is "farm" or "robot"?.55f:kind=="charge"?.14f:.38f;
        internal void UseTechnology(DiverMotor diver,int serial,string action)
        {
            if(!RigRequest(diver))return;
            var r=team.rigs.Find(x=>x.serial==serial);
            if(r==null || r.depth!=CurrentDepth || r.mode=="dropped")return;
            int who=diver.OwnerId;
            bool carried=r.Carrying(who), bound=carried || r.baitId==who || r.operatorId==who || r.helperId==who;
            bool remote=r.kind=="cage" && r.baitId>=0 ||
                r.kind=="winch" && team.TechRank("winch","remote")>0;
            if(r.kind=="mail" && r.flight=="transit")return;
            var eye=diver.transform.position+Vector3.up*.65f;
            float range=remote && (action is "use" or "alternate")?Mathf.Max(4,r.kind=="cage"?TechCatalog.Find("cage").radius*3:TechCatalog.Find(r.kind).range):tuning.interactionRange;
            if(!bound && (Vector3.Distance(eye,r.position)>range || Physics.Linecast(eye,r.position,~0,QueryTriggerInteraction.Ignore)))return;
            if(diver.IsTrapped && r.baitId!=who)return;
            if(action=="carry")
            {
                if(carried)
                {
                    if(r.helperId==who){r.helperId=-1;}
                    else
                    {
                        if(r.kind=="charge")return;
                        if(!FindInstallationSite(diver,r.kind,r.serial,diver.AimDirection,team,out var at,out var yaw,out var reason))
                        {SkillReply(diver.Owner,false,reason);return;}
                        r.Detach();r.position=r.anchor=at;r.yaw=yaw;r.localStock=null;r.timer=0;r.progress=0;
                    }
                }
                else if(!Engaged(who) && r.mode=="carried" && r.helperId<0 && Heavy(r) && diver.GetComponent<HarpoonTool>()?.HandsFull!=true){r.helperId=who;}
                else if(!Engaged(who) && r.baitId<0 && r.operatorId<0 && (r.mode is "placed" or "captured" or "damaged" or "closed" or "working") &&
                    diver.GetComponent<HarpoonTool>()?.HandsFull!=true)
                {r.mode="carried";r.operatorId=who;r.helperId=-1;}
                else return;
            }
            else if(action=="release" && carried)
            {
                if(r.operatorId==who && r.helperId>=0){r.operatorId=r.helperId;r.helperId=-1;}
                else if(r.helperId==who)r.helperId=-1;
                else r.Detach();
            }
            else if(action=="unload")
            {
                if(r.contents.Count==0)return;
                bool onboard=Vector3.Distance(r.position,CargoLoadingPosition)<=tuning.interactionRange;
                if(onboard)
                {
                    int moved=team.SettleRigCargo(r,progression,false,SaveTransaction);
                    if(moved==0){SkillReply(diver.Owner,false,"Разгрузка не завершена. Проверьте грузовую клеть и сохранение");return;}
                    if(r.contents.Count>0){SkillReply(diver.Owner,false,"Клеть занята или заполнена");}
                }
                else if(r.kind!="cage" && r.kind!="basket" && !Engaged(who) && diver.GetComponent<HarpoonTool>()?.HandsFull!=true &&
                    team.rigs.FindAll(x=>x.kind=="basket").Count<tuning.maxSampleBaskets)
                {
                    if(!team.PackRigBasket(r,tuning.sampleBasketCapacity,diver.transform.position,who,SaveTransaction))
                    {SkillReply(diver.Owner,false,"Не удалось записать передачу улова");return;}
                }else return;
                Publish();return;
            }
            else if(action=="repair")
            {
                if(!team.RepairRig(r,tuning.rigRepairScrap,SaveTransaction))
                {SkillReply(diver.Owner,false,"Ремонт не завершён. Проверьте состояние, материалы и сохранение");return;}
                Publish();return;
            }
            else if(action is "use" or "alternate")
            {
                if(diver.IsInside || r.health<=0 || (r.mode=="carried" && !carried) || (!bound && Engaged(who)))return;
                if(!UseActiveRig(r,diver,action,diver.AimDirection) && !UseIndustrialRig(r,diver,action,diver.AimDirection))return;
            }
            else return;
            Publish();Save();
        }

        void StartTech()
        {
            rigRequests.Clear();
            netSweeps.Clear();
            foreach(var r in team.rigs)
            {
                bool lostBait=r.baitId>=0 && r.kind=="cage";
                bool armedLure=r.baitId<0 && r.kind=="cage" && r.mode=="armed";
                r.Detach();
                if(lostBait){r.mode="placed";r.timer=r.progress=0;}
                else if(armedLure)r.mode="armed";
                if(r.flight=="piloted")LoseRobotSignal(r);
            }
        }
        void TickTech(float dt)
        {
            foreach(var diver in DiverMotor.ServerDivers)if(diver)diver.equipmentPace=1;
            for(int i=team.rigs.Count-1;i>=0;i--)
            {
                var r=team.rigs[i];
                if(r.mode=="dropped")continue;
                var carrier=TechDiver(r.operatorId);
                var helper=TechDiver(r.helperId);
                if(r.baitId>=0 && !TechDiver(r.baitId)){r.baitId=-1;if(r.mode=="armed")r.mode="placed";}
                if(r.helperId>=0 && !helper)r.helperId=-1;
                if(r.operatorId>=0 && !carrier && r.mode!="carried")r.operatorId=-1;
                if(r.mode=="carried")
                {
                    if(!carrier && helper){carrier=helper;r.operatorId=r.helperId;r.helperId=-1;helper=null;}
                    if(!carrier){r.Detach();}
                    else
                    {
                        if(helper && Vector3.Distance(carrier.transform.position,helper.transform.position)>tuning.interactionRange)
                        {helper=null;r.helperId=-1;}
                        Vector3 target=helper?(carrier.transform.position+helper.transform.position)*.5f:
                            carrier.transform.position+carrier.transform.forward*(Footprint(r.kind)+.35f);
                        target+=Vector3.up*.45f;
                        float factor=Heavy(r)?(helper?tuning.teamTowSpeed:tuning.soloTowSpeed):.9f;
                        if(r.kind=="cage" && r.contents.Count>0 && !helper && team.TechRank("cage","compact")==0)
                            factor*=.62f;
                        carrier.equipmentPace=factor;if(helper)helper.equipmentPace=factor;
                        var delta=target-r.position;
                        float len=delta.magnitude;
                        if(len>0.001f)
                        {
                            var dir=delta/len;
                            if(Physics.SphereCast(r.position,Footprint(r.kind)*.75f,dir,out var hit,len,~0,QueryTriggerInteraction.Ignore) &&
                               !hit.collider.GetComponentInParent<DiverMotor>())target=r.position+dir*Mathf.Max(0,hit.distance-.02f);
                        }
                        r.position=Vector3.MoveTowards(r.position,target,Mathf.Max(1,carrier.tuning.swimSpeed)*dt*1.5f);
                        if(Vector3.Distance(r.position,carrier.transform.position)>tuning.interactionRange)r.Detach();
                    }
                }
                TickActiveRig(r,dt);
                TickIndustrialRig(r,dt);
                if(r.mode=="spent")team.rigs.RemoveAt(i);
            }
        }
    }
}
