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
                if(r.kind=="vacuum" && r.depth==CurrentDepth && rigShells.TryGetValue(r.serial,out var shell))
                    PresentVacuumIntake(r,shell);
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

        void PresentVacuumIntake(TechRig rig,TechRigShell shell)
        {
            if(!shell)return;
            int used=rig.contents.Count;
            bool active=rig.mode=="placed" && rig.health>0 && used<view.TechUnitCapacity("vacuum");
            var flow=shell.GetComponentInChildren<VacuumIntakeFlow>(true);
            if(!flow)
            {
                if(!active)return;
                var child=new GameObject("Vacuum intake current");
                child.transform.SetParent(shell.transform,false);
                child.transform.localPosition=VacuumInlet(rig)-rig.position;
                flow=child.AddComponent<VacuumIntakeFlow>();
            }
            flow.SetFlow(active,rig.progress);
        }
    }

    internal sealed class VacuumIntakeFlow : MonoBehaviour
    {
        ParticleSystem current;
        MeshRenderer surface;
        MaterialPropertyBlock properties;
        float strength,target;
        static Material bubbles;

        void Awake()
        {
            surface=WaterPressureFx.BuildIntake(transform);
            properties=new MaterialPropertyBlock();
            var froth=new GameObject("Пузырьки в потоке");
            froth.transform.SetParent(transform,false);
            froth.transform.localRotation=Quaternion.Euler(-90,0,0);
            current=froth.AddComponent<ParticleSystem>();
            current.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=current.main;
            main.playOnAwake=false;
            main.loop=true;
            main.startLifetime=1.25f;
            main.startSpeed=0;
            main.startSize=new ParticleSystem.MinMaxCurve(.022f,.055f);
            main.startColor=new Color(.58f,.91f,1f,.76f);
            main.maxParticles=115;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var shape=current.shape;
            shape.shapeType=ParticleSystemShapeType.Hemisphere;
            shape.radius=2.55f;
            shape.radiusThickness=0;
            var velocity=current.velocityOverLifetime;
            velocity.enabled=true;
            velocity.space=ParticleSystemSimulationSpace.Local;
            var acceleration=AnimationCurve.Linear(0,.23f,1,1.73f);
            velocity.radial=new ParticleSystem.MinMaxCurve(-shape.radius/main.startLifetime.constant,acceleration);
            velocity.orbitalZ=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.29f,1,1.4f));
            var size=current.sizeOverLifetime;
            size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.52f),
                new Keyframe(.28f,1),new Keyframe(.82f,.63f),new Keyframe(1,0)));
            var emission=current.emission;
            emission.rateOverTime=65;
            if(!bubbles)bubbles=Resources.Load<Material>("NereidBubbles");
            if(bubbles)current.GetComponent<ParticleSystemRenderer>().sharedMaterial=bubbles;
        }

        internal void SetFlow(bool active,float intake)
        {
            target=active?Mathf.Lerp(.57f,1.2f,intake):0;
            if(active && !current.isEmitting)current.Play();
            else if(!active && current.isEmitting)current.Stop(true,ParticleSystemStopBehavior.StopEmitting);
        }

        void LateUpdate()
        {
            strength=Mathf.MoveTowards(strength,target,Time.deltaTime*3);
            if(!surface)return;
            surface.enabled=strength>.01f;
            WaterPressureFx.SetIntake(surface,properties,strength);
        }
    }
}
