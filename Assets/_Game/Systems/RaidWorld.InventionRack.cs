using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        [ServerRpc] public void CmdRackTake(string kind,int slot) => RaidWorld.Instance?.TakeRackItem(this,kind,slot);
        [ServerRpc] public void CmdCollectRackParcel(int serial) => RaidWorld.Instance?.CollectRackParcel(this,serial);
        [ServerRpc] public void CmdTechEquipSlot(int slot) => RaidWorld.Instance?.EquipRackItem(this,slot);
        [ServerRpc] public void CmdTechStore(int serial,int slot) => RaidWorld.Instance?.StowRackItem(this,serial,slot);
    }

    public sealed partial class RaidWorld
    {
        Transform inventionRack;
        BoxCollider rackContact;
        public Transform InventionRackTransform => inventionRack;

        void EnsureInventionRack()
        {
            if(inventionRack)return;
            foreach(var station in ShipConsole.Stations)
            {
                if(!station || station.kind!=ShipConsoleKind.SkillTree || !station.screenAnchor || station.gameObject.scene!=gameObject.scene)continue;
                var screen=station.screenAnchor;
                var forward=Vector3.ProjectOnPlane(-screen.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.9f)continue;
                var center=screen.position-screen.right*2+forward*.30f;
                if(Physics.Raycast(center,Vector3.down,out var floor,3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    center.y=floor.point.y+1.2f;
                var go=new GameObject("NEREID · Раздатчик снаряжения");
                // The workshop is already mounted on the moving vessel. Preserve the cabinet's metre scale.
                go.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward,Vector3.up));
                go.transform.SetParent(station.transform.parent,true);
                inventionRack=go.transform;
                rackContact=go.AddComponent<BoxCollider>();
                rackContact.center=new Vector3(0,-.13f,-.24f);rackContact.size=new Vector3(1.32f,2.10f,.49f);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)TechRigShell.BuildDispenser(go);
                break;
            }
        }

        public bool NearInventionRack(DiverMotor diver) => inventionRack && diver && diver.IsInside && !diver.IsHelpless && !diver.IsTrapped &&
            Vector3.Distance(diver.transform.position+Vector3.up*.6f,inventionRack.position)<=tuning.interactionRange;
        public bool RackFocused(DiverMotor diver,bool localCamera=false)
        {
            if(!NearInventionRack(diver))return false;
            var eye=diver.transform.position+Vector3.up*.6f;
            var camera=localCamera?diver.GetComponentInChildren<Camera>():null;
            var ray=camera?camera.ViewportPointToRay(new Vector3(.5f,.5f,0)):new Ray(eye,diver.AimDirection);
            return Physics.Raycast(ray,out var hit,tuning.interactionRange,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                hit.collider==rackContact;
        }

        bool InventoryRequest(DiverMotor diver,out PlayerProgress profile)
        {
            profile=null;
            if(!RigRequest(diver) || diver.IsTrapped)return false;
            profile=team.Player(ProgressKey(diver));
            return profile!=null;
        }
        void InventoryReceipt(DiverMotor diver,PlayerProgress profile,bool accepted,string reason)
        {
            SendProfile(diver.Owner,profile);
            if(accepted)diver.ConfirmInteraction();
            SkillReply(diver.Owner,accepted,reason);
        }
        internal void TakeRackItem(DiverMotor diver,string kind,int slot)
        {
            if(!InventoryRequest(diver,out var profile))return;
            if(!RackFocused(diver)){InventoryReceipt(diver,profile,false,"Подойдите к раздатчику и посмотрите на его дверцу");return;}
            if(!team.CanTakeTechFromRack(profile,kind,slot,out var reason)){InventoryReceipt(diver,profile,false,reason);return;}
            LaunchRackParcel(diver,kind,slot);
        }
        internal void EquipRackItem(DiverMotor diver,int slot)
        {
            if(!InventoryRequest(diver,out var profile))return;
            if(Engaged(diver.OwnerId) || diver.GetComponent<HarpoonTool>()?.HandsFull==true)
            {InventoryReceipt(diver,profile,false,"Для снаряжения нужны свободные руки");return;}
            bool ok=team.DeployTechFromSlot(profile,slot,diver.transform.position,diver.OwnerId,SaveTransaction,out var reason);
            if(ok)Publish();
            InventoryReceipt(diver,profile,ok,reason);
        }
        internal void StowRackItem(DiverMotor diver,int serial,int slot)
        {
            if(!InventoryRequest(diver,out var profile))return;
            var rig=team.rigs.Find(r=>r.serial==serial);
            var eye=diver.transform.position+Vector3.up*.6f;
            if(rig==null || rig.depth!=CurrentDepth || (!rig.Carrying(diver.OwnerId) &&
                (Vector3.Distance(eye,rig.position)>tuning.interactionRange || Physics.Linecast(eye,rig.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))))
            {InventoryReceipt(diver,profile,false,"Подойдите к установке");return;}
            bool ok=team.StoreTechInSlot(profile,serial,slot,diver.OwnerId,SaveTransaction,out var reason);
            if(ok)Publish();
            InventoryReceipt(diver,profile,ok,reason);
        }
    }
}
