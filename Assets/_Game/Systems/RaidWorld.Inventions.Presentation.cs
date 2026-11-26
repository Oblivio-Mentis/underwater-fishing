using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly Dictionary<int,TechRigShell> rigShells=new();
        readonly List<int> oldShells=new();
        internal TechRig AimedInvention {get;private set;}
        internal static string RigModeName(string mode) => mode switch
        {
            "placed"=>"Установлена","carried"=>"Переноска","captured"=>"Добыча в клетке","armed"=>"Взведена",
            "closed"=>"Закрыта","closing"=>"Закрывается","jammed"=>"Затор","damaged"=>"Повреждена",
            "working"=>"В работе","fuse"=>"Активация","remote"=>"Ожидает сигнала","pumping"=>"Всасывание",
            "netting"=>"Ловля","running"=>"Работает","paused"=>"Приостановлена","stored"=>"На складе","spent"=>"Израсходовано",_=>mode
        };
        void StopTechPresentation()
        {
            foreach(var shell in rigShells.Values)if(shell)Destroy(shell.gameObject);
            rigShells.Clear();
            if(placementProjection)Destroy(placementProjection);
            if(projectionMaterial)Destroy(projectionMaterial);
            AimedInvention=null;
            StopIndustryPresentation();
        }

        void PresentTech()
        {
            if(!IsClientInitialized || view?.rigs==null)return;
            PresentIndustry();
            oldShells.Clear();oldShells.AddRange(rigShells.Keys);AimedInvention=null;
            var camera=localDiver?localDiver.GetComponentInChildren<Camera>():null;
            var tool=localDiver?localDiver.GetComponent<HarpoonTool>():null;
            var aim=camera?camera.transform.forward:Vector3.forward;
            TechRig held=null,target=null,bait=null,handset=null;
            float best=9999;
            foreach(var r in view.rigs)
            {
                if(r.depth!=CurrentDepth)continue;
                oldShells.Remove(r.serial);
                bool fresh=!rigShells.TryGetValue(r.serial,out var shell);
                if(fresh)
                {
                    var go=new GameObject("Снаряжение "+r.kind+" №"+r.serial);go.transform.SetParent(transform,false);
                    rigShells[r.serial]=shell=go.AddComponent<TechRigShell>();shell.Build(r.kind);
                }
                shell.Display(r,fresh);
                if(r.kind=="charge")FisheryEffects.ShowChargeFuse(shell,r);
                RigFishingLines.Present(shell.gameObject,r);
                if(!camera)continue;
                if(r.Carrying(localDiver.OwnerId))held=r;
                if(r.baitId==localDiver.OwnerId)bait=r;
                if(r.kind=="cage" && r.mode=="armed" && r.operatorId==localDiver.OwnerId)handset=r;
                var v=r.position-camera.transform.position;
                float along=Vector3.Dot(v,aim),error=(v-aim*along).magnitude;
                float reach=tuning.interactionRange;
                if(r.kind=="cage" && r.baitId>=0)reach=Mathf.Max(reach,TechCatalog.Find(r.kind).radius*3);
                if(r.kind=="winch" && view.TechRank("winch","remote")>0)reach=Mathf.Max(reach,TechCatalog.Find("winch").range);
                if(r.mode is "fuse" or "spent" || along<0 || along>reach || error>Footprint(r.kind)+.24f || error+.035f*along>=best)continue;
                if(Physics.Linecast(camera.transform.position,r.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                best=error+.035f*along;target=r;
            }
            foreach(var serial in oldShells){if(rigShells[serial])Destroy(rigShells[serial].gameObject);rigShells.Remove(serial);}
            PresentInstallation(localDiver,camera,tool,held);
            if(!tool || localDiver.IsHelpless || UI.UiInputGate.Blocked || UI.UiInputGate.PointerReserved)return;
            var selected=bait??held??target??handset;
            if(selected==null)return;
            AimedInvention=selected;
            var k=Keyboard.current;
            if(k==null || !Application.isFocused)return;
            if(k.rKey.wasPressedThisFrame && held==null && tool.CanDrawPocketInvention &&
               view.CanPackInvention(selected,localDiver.OwnerId,out _))
            {localDiver.CmdTechStore(selected.serial);return;}
            if(held==null && selected==target && tool.InventionInSlot(tool.ActiveSlot).count>0 && tool.CanDrawPocketInvention)return;
            string action=k.tKey.wasPressedThisFrame?(k.shiftKey.isPressed?"release":"carry"):k.yKey.wasPressedThisFrame?"use":
                k.uKey.wasPressedThisFrame?"alternate":k.iKey.wasPressedThisFrame?"unload":k.oKey.wasPressedThisFrame?"repair":null;
            if(action!=null)localDiver.CmdTechUse(selected.serial,action);
        }
    }
}
