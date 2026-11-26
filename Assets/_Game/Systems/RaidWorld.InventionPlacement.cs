using System;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        internal void PlaceSelectedInvention(int slot,string kind)
        {
            if(_camera)PlaceInventionServer(slot,kind,_camera.transform.forward);
        }

        [ServerRpc(RequireOwnership=true)]
        void PlaceInventionServer(int slot,string kind,Vector3 direction) =>
            RaidWorld.Instance?.InstallPocketInvention(this,slot,kind,direction);
    }

    public sealed partial class RaidWorld
    {
        GameObject placementProjection;
        MeshFilter projectionShape;
        MeshRenderer projectionRenderer;
        Material projectionMaterial;
        MaterialPropertyBlock projectionProperties;

        bool FindInstallationSite(DiverMotor diver,string kind,int movingSerial,Vector3 aim,TeamProgress state,
            out Vector3 point,out float yaw,out string reason)
        {
            var eye=diver.transform.position+Vector3.up*.6f;
            var bounds=TechRigShell.EquipmentMesh(kind).bounds;
            var heading=Vector3.ProjectOnPlane(aim,Vector3.up);
            if(heading.sqrMagnitude<.01f)heading=diver.transform.forward;
            yaw=Quaternion.LookRotation(heading).eulerAngles.y;
            var facing=Quaternion.Euler(0,yaw,0);
            float reach=Mathf.Max(3.4f,bounds.extents.magnitude*3f);
            point=eye+aim*reach;
            reason="Наведитесь на свободное дно";
            if(diver.IsInside){reason="Установка доступна за бортом";return false;}
            if(!Physics.Raycast(eye,aim,out var floor,reach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(aim.y>.2f ||
                   !Physics.Raycast(point+Vector3.up*.15f,Vector3.down,out floor,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    return false;
            }
            if(floor.normal.y<.72f || floor.collider.GetComponentInParent<DiverMotor>())
            {
                point=floor.point-aim*(bounds.extents.magnitude+.12f);
                return false;
            }
            float slope=(Mathf.Abs(Vector3.Dot(floor.normal,facing*Vector3.right))*bounds.extents.x+
                Mathf.Abs(Vector3.Dot(floor.normal,facing*Vector3.forward))*bounds.extents.z)/floor.normal.y;
            point=floor.point+Vector3.up*(-bounds.min.y+slope+.035f);
            var center=point+facing*bounds.center;
            if((eye-center).sqrMagnitude>(reach+1f)*(reach+1f) ||
               Physics.Linecast(eye,center,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
            if(Physics.CheckBox(center,bounds.extents,facing,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {reason="Для установки мало места";return false;}
            foreach(var other in state.rigs)
            {
                if(other.serial==movingSerial || other.depth!=CurrentDepth || other.mode is "carried" or "spent")continue;
                var spacing=new Vector2(bounds.extents.x,bounds.extents.z).magnitude+Footprint(other.kind);
                var offset=other.position-point;
                if(Mathf.Abs(offset.y)<bounds.size.y+Footprint(other.kind) &&
                    new Vector2(offset.x,offset.z).sqrMagnitude<spacing*spacing)
                {reason="Здесь уже стоит оборудование";return false;}
            }
            if(kind=="vacuum" && !state.CanPlaceVacuum(movingSerial,out reason))return false;
            if(movingSerial==0 && kind!="vacuum" &&
               state.rigs.FindAll(r=>r.kind==kind).Count>=TechRig.TotalLimit(state,TechCatalog.Find(kind)))
            {reason="Достигнут лимит установок экипажа";return false;}
            reason=null;
            return true;
        }

        internal void InstallPocketInvention(DiverMotor diver,int slot,string kind,Vector3 direction)
        {
            if(!InventoryRequest(diver,out var profile))return;
            var tool=diver.GetComponent<HarpoonTool>();
            if(!tool || slot!=tool.ActiveSlot || !tool.CanDrawPocketInvention || kind=="charge" ||
               TechCatalog.Find(kind)==null || tool.InventionInSlot(slot).kind!=kind ||
               !(direction.sqrMagnitude>.99f && direction.sqrMagnitude<1.01f) ||
               Vector3.Dot(direction,diver.AimDirection)<.55f)return;
            if(!FindInstallationSite(diver,kind,0,direction,team,out var position,out float yaw,out var reason))
            {InventoryReceipt(diver,profile,false,reason);return;}
            bool placed=team.DeployTechFromSlot(profile,slot-FirstCargoSlot,position,diver.OwnerId,
                rig=>{rig.Detach();rig.yaw=yaw;},SaveTransaction,out reason);
            if(placed)Publish();
            InventoryReceipt(diver,profile,placed,placed?"Установка закреплена":reason);
        }

        internal string PlacementHint { get; private set; }

        void PresentInstallation(DiverMotor diver,Camera camera,HarpoonTool tool,TechRig held)
        {
            PlacementHint=null;
            if(!diver || !camera || !tool || diver.IsHelpless || diver.IsTrapped ||
               UI.UiInputGate.Modal || UI.CheatPanel.PointerActive || UI.UiInputGate.PointerReserved)
            {if(placementProjection)placementProjection.SetActive(false);return;}
            var stack=tool.InventionInSlot(tool.ActiveSlot);
            string kind=held?.kind ?? (tool.CanDrawPocketInvention && stack.count>0?stack.kind:null);
            if(string.IsNullOrEmpty(kind) || kind=="charge" || held!=null && held.operatorId!=diver.OwnerId)
            {if(placementProjection)placementProjection.SetActive(false);return;}
            bool valid=FindInstallationSite(diver,kind,held?.serial??0,camera.transform.forward,view,
                out var at,out float yaw,out var reason);
            PlacementHint=reason;
            if(diver.IsInside){if(placementProjection)placementProjection.SetActive(false);return;}
            if(!placementProjection)
            {
                var shader=Resources.Load<Shader>("RaidPlacement");
                if(!shader)return;
                placementProjection=new GameObject("Equipment placement projection");
                placementProjection.transform.SetParent(transform,false);
                projectionMaterial=new Material(shader);
                projectionProperties=new MaterialPropertyBlock();
                projectionShape=placementProjection.AddComponent<MeshFilter>();
                projectionRenderer=placementProjection.AddComponent<MeshRenderer>();
                projectionRenderer.shadowCastingMode=ShadowCastingMode.Off;
                projectionRenderer.receiveShadows=false;
            }
            var shape=TechRigShell.EquipmentMesh(kind);
            if(projectionShape.sharedMesh!=shape)
            {
                projectionShape.sharedMesh=shape;
                var materials=new Material[shape.subMeshCount];
                Array.Fill(materials,projectionMaterial);
                projectionRenderer.sharedMaterials=materials;
            }
            projectionProperties.SetColor("_Color",valid?new Color(.22f,1,.72f):new Color(1,.3f,.19f));
            projectionRenderer.SetPropertyBlock(projectionProperties);
            placementProjection.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
            placementProjection.SetActive(true);
        }
    }
}
