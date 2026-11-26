using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        public void CmdRackTake(string kind)
        {
            if(!NetworkObject || !IsClientInitialized || !IsOwner || !_camera)return;
            // The click can precede the next periodic movement/aim packet.
            RequestRackProduct(kind,_camera.ViewportPointToRay(new Vector3(.5f,.5f)).direction);
        }
        [ServerRpc(RequireOwnership=true)]
        void RequestRackProduct(string kind,Vector3 direction) => RaidWorld.Instance?.TakeRackItem(this,kind,direction);
        [ServerRpc] public void CmdCollectRackParcel(int serial) => RaidWorld.Instance?.CollectRackParcel(this,serial);
        [ServerRpc] public void CmdTechEquipSlot(int slot) => RaidWorld.Instance?.EquipRackItem(this,slot);
        [ServerRpc] public void CmdTechStore(int serial) => RaidWorld.Instance?.StowRackItem(this,serial);
    }

    public sealed partial class RaidWorld
    {
        Transform inventionRack;
        BoxCollider rackContact;
        InventionVendorDisplay rackDisplay;
        Matrix4x4 rackPhysicsPose;
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
                const float back=-.485f;
                rackContact.center=new Vector3(0,-.13f,(back+TechRigShell.VendorGlassZ)*.5f);
                rackContact.size=new Vector3(1.32f,2.10f,TechRigShell.VendorGlassZ-back);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    rackDisplay=TechRigShell.BuildDispenser(go);
                    rackDisplay.RefreshLabels(view);
                }
                break;
            }
        }

        public bool NearInventionRack(DiverMotor diver)
        {
            if(!inventionRack || !diver || !diver.IsInside || diver.IsHelpless || diver.IsTrapped)return false;
            var pose=inventionRack.localToWorldMatrix;
            if(rackPhysicsPose!=pose)
            {
                Physics.SyncTransforms();
                rackPhysicsPose=pose;
            }
            var eye=diver.transform.position+Vector3.up*.6f;
            return (rackContact.ClosestPoint(eye)-eye).sqrMagnitude<=tuning.interactionRange*tuning.interactionRange;
        }
        Ray RackRay(DiverMotor diver,bool localCamera)
        {
            var camera=localCamera?diver.GetComponentInChildren<Camera>():null;
            return camera?new Ray(camera.transform.position,camera.ViewportPointToRay(new Vector3(.5f,.5f)).direction):
                new Ray(diver.transform.position+Vector3.up*.6f,diver.AimDirection);
        }
        public bool RackFocused(DiverMotor diver,bool localCamera=false) =>
            AimAtCabinet(diver,localCamera,out _);

        internal bool TryRackProduct(DiverMotor diver,bool localCamera,out TechItem product) =>
            AimAtCabinet(diver,localCamera,out product) && product!=null;

        bool AimAtCabinet(DiverMotor diver,bool localCamera,out TechItem product)
        {
            product=null;
            return NearInventionRack(diver) && HitRack(RackRay(diver,localCamera),out product);
        }
        bool HitRack(Ray ray,out TechItem product)
        {
            product=null;
            if(!Physics.Raycast(ray,out var hit,tuning.interactionRange,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                hit.collider!=rackContact)return false;
            if(Vector3.Dot(hit.normal,inventionRack.forward)>=.95f)
                TechRigShell.TryVendorCell(inventionRack.InverseTransformPoint(hit.point),out product);
            return true;
        }
        internal Vector3 RackProductPosition(string kind)
        {
            if(inventionRack)
            {
                var items=TechCatalog.Items;
                for(int i=0;i<items.Length;i++)
                    if(items[i].id==kind)return inventionRack.TransformPoint(TechRigShell.VendorCell(i));
            }
            return Vector3.zero;
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
        const int FirstCargoSlot = 2;

        bool FindStoragePocket(HarpoonTool tool, PlayerProgress profile, string kind, out int slot, out string reason)
        {
            slot = -1;
            int limit = team.TechStackLimit(kind);
            if (limit == 0 || team.TechRank(kind,"unlock") == 0)
            {
                reason = "Сначала исследуйте чертёж";
                return false;
            }
            if (tool) slot = tool.FindInventionPocket(kind,limit);
            if (slot < FirstCargoSlot)
            {
                reason = "В инвентаре нет места";
                return false;
            }
            return team.CanTakeTechFromRack(profile,kind,slot-FirstCargoSlot,out reason);
        }

        internal void TakeRackItem(DiverMotor diver,string kind,Vector3 direction)
        {
            if(!InventoryRequest(diver,out var profile))return;
            // The server supplies origin, reach, occlusion and cell geometry. Ignore stale backwards clicks.
            float length=direction.sqrMagnitude;
            Physics.SyncTransforms();
            if(!(length>.99f && length<1.01f) || Vector3.Dot(direction,diver.AimDirection)<.342f ||
                !NearInventionRack(diver) || !HitRack(new Ray(diver.transform.position+Vector3.up*.6f,direction),out var product) ||
                product==null || product.id!=kind)
            {InventoryReceipt(diver,profile,false,"Наведитесь на товар за стеклом");return;}
            RefreshVendorStock();
            if(TechRig.HasTeamLimit(kind) && team.TechRank(kind,"unlock")>0 && VendorIssuedCount(kind)>=TechRig.TotalLimit(team,product))
            {InventoryReceipt(diver,profile,false,VendorTeamLimitReason);return;}
            if(!FindStoragePocket(diver.GetComponent<HarpoonTool>(),profile,kind,out _,out var reason)){InventoryReceipt(diver,profile,false,reason);return;}
            LaunchRackParcel(diver,kind);
        }
        internal void EquipRackItem(DiverMotor diver,int slot)
        {
            if(!InventoryRequest(diver,out var profile))return;
            var tool=diver.GetComponent<HarpoonTool>();
            if(!tool || slot<FirstCargoSlot || slot>=tool.SlotCount || slot!=tool.ActiveSlot ||
                tool.InventionInSlot(slot).count<=0)
            {InventoryReceipt(diver,profile,false,"Выберите карман со снаряжением");return;}
            if(Engaged(diver.OwnerId) || !tool.CanDrawPocketInvention)
            {InventoryReceipt(diver,profile,false,"Для снаряжения нужны свободные руки");return;}
            bool ok=team.DeployTechFromSlot(profile,slot-FirstCargoSlot,diver.transform.position,diver.OwnerId,SaveTransaction,out var reason);
            if(ok)Publish();
            InventoryReceipt(diver,profile,ok,reason);
        }
        internal void StowRackItem(DiverMotor diver,int serial)
        {
            if(!InventoryRequest(diver,out var profile))return;
            var rig=team.rigs.Find(r=>r.serial==serial);
            var eye=diver.transform.position+Vector3.up*.6f;
            if(rig==null || rig.depth!=CurrentDepth || (!rig.Carrying(diver.OwnerId) &&
                (Vector3.Distance(eye,rig.position)>tuning.interactionRange || Physics.Linecast(eye,rig.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))))
            {InventoryReceipt(diver,profile,false,"Подойдите к установке");return;}
            var tool=diver.GetComponent<HarpoonTool>();
            if(!tool)return;
            if(!rig.Carrying(diver.OwnerId) && (!tool.CanDrawPocketInvention || Engaged(diver.OwnerId)))return;
            int slot=tool.ActiveSlot;
            if(!tool.CanFitInvention(slot,rig.kind,team.TechStackLimit(rig.kind)))
            {
                if(!FindStoragePocket(tool,profile,rig.kind,out slot,out var issue)){InventoryReceipt(diver,profile,false,issue);return;}
            }
            bool ok=team.StoreTechInSlot(profile,serial,slot-FirstCargoSlot,diver.OwnerId,SaveTransaction,out var reason);
            if(ok)Publish();
            InventoryReceipt(diver,profile,ok,reason);
        }
    }
}
