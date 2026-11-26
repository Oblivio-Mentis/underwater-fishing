using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        IndustryFeed industrialDisplay;
        float nextPilotInput, nextCableInput;
        public void StopIndustryPresentation()
        {
            if(industrialDisplay)Destroy(industrialDisplay.gameObject);
            industrialDisplay=null;
        }

        void PresentIndustry()
        {
            if(!IsClientInitialized || view?.rigs==null || !localDiver)return;
            if(!industrialDisplay)
            {
                var go=new GameObject("NEREID • industrial telemetry");
                go.transform.SetParent(transform,false);
                industrialDisplay=go.AddComponent<IndustryFeed>();
            }
            TechRig robot=null,nearCable=null;
            float nearest=float.MaxValue;
            foreach(var r in view.rigs)
            {
                if(r.depth!=CurrentDepth || !IndustryPresent(r))continue;
                if(r.kind=="robot" && r.operatorId==localDiver.OwnerId && r.flight=="piloted")robot=r;
                if(r.kind!="winch" || localDiver.IsInside)continue;
                float d=Vector3.Distance(r.position,localDiver.transform.position);
                if(d<nearest && d<TechCatalog.Find("winch").range*view.TechValue("winch","range",1))
                {nearCable=r;nearest=d;}
            }
            industrialDisplay.Draw(view,CurrentDepth,robot,nearCable);
            var kb=Keyboard.current;
            if(kb==null)return;
            if(nearCable!=null && !UI.UiInputGate.Blocked && kb.hKey.wasPressedThisFrame && Time.unscaledTime>=nextCableInput)
            {nextCableInput=Time.unscaledTime+.4f;HookIndustryCable(nearCable.serial);}
            if(robot==null)return;
            int command=kb.escapeKey.wasPressedThisFrame?1:kb.homeKey.wasPressedThisFrame?2:
                kb.enterKey.wasPressedThisFrame?3:kb.endKey.wasPressedThisFrame?4:0;
            if(command==0 && Time.unscaledTime<nextPilotInput)return;
            Vector3 move=Vector3.right*((kb.rightArrowKey.isPressed?1:0)-(kb.leftArrowKey.isPressed?1:0))+
                Vector3.forward*((kb.upArrowKey.isPressed?1:0)-(kb.downArrowKey.isPressed?1:0))+
                Vector3.up*((kb.pageUpKey.isPressed?1:0)-(kb.pageDownKey.isPressed?1:0));
            nextPilotInput=Time.unscaledTime+.12f;RobotControls(robot.serial,move,command);
        }
    }
}
