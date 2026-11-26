using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly Dictionary<int,TechRigShell> rigShells=new();
        readonly List<int> oldShells=new();
        LineRenderer rigGuide;
        Material guideInk;
        string rigHint;
        GUIStyle rigCaption;
        static string ActionLabel(TechRig r) => r.kind switch
        {
            "charge"=>"Y бросить / подорвать     U отложенный / групповой пуск",
            "trap"=>"Y работа ловушки     I забрать улов",
            "cage"=>"Y крепление / спуск     U сброс / аварийный выход",
            "vacuum"=>"Y сопло / второй оператор     U очистить вход",
            "net"=>"Y взяться за кромку     U стянуть невод",
            "oxygen"=>"Y подключить шланг     U обслуживание",
            "lure"=>"Y подать сигнал     U сменить частоту",
            "bubbles"=>"Y питание     U выбрать соседний излучатель",
            "farm"=>"Y заселить / собрать излишек     U уход",
            "winch"=>"Y привод     U отцепить трос",
            "robot"=>"Y передать груз     U автоматический сбор",
            "mail"=>"Y загрузить / отправить     U отправить капсулу",
            "magnet"=>"Y магнит     U фильтр",
            _=>"I сдать груз возле грузового подъёмника"
        };
        internal static string RigModeName(string mode) => mode switch
        {
            "placed"=>"Установлена","carried"=>"Переноска","captured"=>"Добыча в клетке","armed"=>"Взведена",
            "closed"=>"Закрыта","closing"=>"Закрывается","jammed"=>"Затор","damaged"=>"Повреждена",
            "working"=>"В работе","fuse"=>"Активация","remote"=>"Ожидает сигнала","pumping"=>"Всасывание",
            "netting"=>"Ловля","running"=>"Работает","stored"=>"На складе","spent"=>"Израсходовано",_=>mode
        };
        void StopTechPresentation()
        {
            foreach(var shell in rigShells.Values)if(shell)Destroy(shell.gameObject);
            rigShells.Clear();if(rigGuide)Destroy(rigGuide.gameObject);if(guideInk)Destroy(guideInk);
            rigHint=null;StopIndustryPresentation();
        }
        void PreviewGuide(Vector3 point,float radius,bool valid)
        {
            if(!rigGuide)
            {
                var go=new GameObject("Проекция • контур опоры");go.transform.SetParent(transform,false);
                rigGuide=go.AddComponent<LineRenderer>();
                guideInk=new Material(Shader.Find("Sprites/Default"));rigGuide.sharedMaterial=guideInk;
                rigGuide.useWorldSpace=true;rigGuide.widthMultiplier=.024f;rigGuide.loop=true;rigGuide.positionCount=32;
            }
            rigGuide.enabled=true;
            rigGuide.startColor=rigGuide.endColor=valid?new Color(.18f,1,.72f):new Color(1,.28f,.12f);
            for(int i=0;i<32;i++){float a=i*2*Mathf.PI/32;rigGuide.SetPosition(i,point+new Vector3(Mathf.Cos(a)*radius,-radius+.035f,Mathf.Sin(a)*radius));}
        }

        void PresentTech()
        {
            if(!IsClientInitialized || view?.rigs==null)return;
            PresentIndustry();
            oldShells.Clear();oldShells.AddRange(rigShells.Keys);rigHint=null;
            var camera=localDiver?localDiver.GetComponentInChildren<Camera>():null;
            var aim=camera?camera.transform.forward:Vector3.forward;
            TechRig held=null,target=null,bait=null;
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
                RigFishingLines.Present(shell.gameObject,r);
                if(!camera)continue;
                if(r.Carrying(localDiver.OwnerId))held=r;
                if(r.baitId==localDiver.OwnerId)bait=r;
                var v=r.position-camera.transform.position;
                float along=Vector3.Dot(v,aim),error=(v-aim*along).magnitude;
                float reach=tuning.interactionRange;
                if(r.kind=="charge" && r.mode=="remote")reach=Mathf.Max(reach,TechCatalog.Find(r.kind).range);
                if(r.kind=="cage" && r.baitId>=0)reach=Mathf.Max(reach,TechCatalog.Find(r.kind).radius*3);
                if(r.kind=="winch" && view.TechRank("winch","remote")>0)reach=Mathf.Max(reach,TechCatalog.Find("winch").range);
                if(along<0 || along>reach || error>Footprint(r.kind)+.24f || error+.035f*along>=best)continue;
                best=error+.035f*along;target=r;
            }
            foreach(var serial in oldShells){if(rigShells[serial])Destroy(rigShells[serial].gameObject);rigShells.Remove(serial);}
            if(rigGuide)rigGuide.enabled=false;
            if(!localDiver || localDiver.IsHelpless || UI.UiInputGate.Blocked || Keyboard.current==null)return;
            var selected=bait??held??target;
            if(selected==null)return;
            string hint=ActionLabel(selected);
            if(held!=null && held.operatorId==localDiver.OwnerId)
            {
                bool valid=Placement(held,localDiver,aim,view,out var point);
                if(!localDiver.IsInside)PreviewGuide(point,Footprint(held.kind),valid);
                hint+="     T "+(valid?"установить":"нужно свободное дно")+"     Shift+T отпустить";
            }
            else hint+="     T взять / помочь";
            string title=selected.kind=="basket"?"Корзина улова":TechCatalog.Find(selected.kind)?.title;
            rigHint=title+"  ·  "+RigModeName(selected.mode)+"  ·  "+selected.contents.Count+" ед.    "+Mathf.RoundToInt(100*selected.health)+"%\n"+hint+
                (selected.contents.Count>0?"     I разгрузить":"")+(selected.health<.99f?"     O ремонт":"");
            var k=Keyboard.current;
            string action=k.tKey.wasPressedThisFrame?(k.shiftKey.isPressed?"release":"carry"):k.yKey.wasPressedThisFrame?"use":
                k.uKey.wasPressedThisFrame?"alternate":k.iKey.wasPressedThisFrame?"unload":k.oKey.wasPressedThisFrame?"repair":null;
            if(action!=null)localDiver.CmdTechUse(selected.serial,action);
        }
        void TechHUD()
        {
            if(string.IsNullOrEmpty(rigHint) || !IsClientInitialized || !localDiver || UI.UiInputGate.Blocked)return;
            rigCaption??=new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleCenter,fontSize=15,wordWrap=true};
            float w=Mathf.Min(920,Screen.width-20);
            GUI.Box(new Rect((Screen.width-w)*.5f,Screen.height*.71f,w,58),rigHint,rigCaption);
        }
    }
}

