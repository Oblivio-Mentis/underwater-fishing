using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        [ServerRpc(RequireOwnership=true)]
        internal void CmdTakeEquipmentCatch(int serial,long catchId,Vector3 aim) =>
            RaidWorld.Instance?.TakeEquipmentCatch(this,serial,catchId,aim);
    }

    public sealed partial class RaidWorld
    {
        internal static bool HasCatchOutlet(TechRig rig) => rig.kind is "vacuum" or "trap" or "net" or "farm";

        TechRig SightCatchOutlet(DiverMotor diver,Vector3 aim,TeamProgress state)
        {
            Vector3 eye=diver.transform.position+Vector3.up*.6f;
            TechRig target=null;
            float best=tuning.interactionRange;
            foreach(var rig in state.rigs)
            {
                if(rig.depth!=CurrentDepth || !HasCatchOutlet(rig) ||
                   rig.mode is "carried" or "dropped" or "spent" or "stored" or "netting")continue;
                if(rig.kind!="vacuum" && rig.contents.Count==0)continue;
                var outlet=rig.kind=="vacuum"?TechRigShell.VacuumPoint(Vector3.up*.17f):Vector3.up*.17f;
                var point=rig.position+outlet;
                Vector3 offset=point-eye;
                float along=Vector3.Dot(offset,aim);
                if(along<=0 || along>=best || (offset-aim*along).sqrMagnitude>
                   Mathf.Pow(Footprint(rig.kind)+.30f,2) || !ClearGearPath(eye,point))continue;
                target=rig;
                best=along;
            }
            return target;
        }

        internal bool PointAtCatchOutlet(DiverMotor diver,Camera camera,bool click)
        {
            if(!diver || !camera || diver.IsHelpless || diver.IsTrapped || view?.rigs==null)return false;
            var aim=camera.transform.forward;
            var rig=SightCatchOutlet(diver,aim,view);
            if(rig==null)return false;
            if(click)
            {
                if(rig.contents.Count==0)UI.UiRoot.Instance?.Toast("Контейнер пуст");
                else diver.CmdTakeEquipmentCatch(rig.serial,rig.contents[0].id,aim);
            }
            return true;
        }

        internal void TakeEquipmentCatch(DiverMotor diver,int serial,long catchId,Vector3 aim)
        {
            if(!(aim.sqrMagnitude>.99f && aim.sqrMagnitude<1.01f) || !RigRequest(diver) || diver.IsTrapped)return;
            var rig=SightCatchOutlet(diver,aim,team);
            if(rig==null || rig.serial!=serial || !diver.TryGetComponent<HarpoonTool>(out var tool))return;
            int row=rig.contents.FindIndex(item=>item.id==catchId);
            if(row<0){SkillReply(diver.Owner,false,"Этот экземпляр уже забрали");return;}
            var catchItem=rig.contents[row];
            bool saveFailedForCatch=false;
            bool CommitCatch()
            {
                rig.contents.RemoveAt(row);
                bool saved=false;
                try{return saved=SaveTransaction();}
                finally
                {
                    if(!saved){rig.contents.Insert(row,catchItem);saveFailedForCatch=true;}
                }
            }
            if(!tool.TakeEquipmentCatch(catchItem,CommitCatch,true))
            {
                SkillReply(diver.Owner,false,saveFailedForCatch?"Не удалось сохранить выдачу":"Освободите руки и место для рыбы");
                return;
            }
            Publish();
            diver.ConfirmInteraction();
            SkillReply(diver.Owner,true,UI.ReelReadout.Species(catchItem.species));
        }
    }
}
