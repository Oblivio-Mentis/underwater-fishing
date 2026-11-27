using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        Material raidWakeMaterial;

        void PresentRaiderWake(VisibleAttacker view,AttackerSnapshot actor,float pace)
        {
            bool rushing=actor.pose is AttackerPose.Strike or AttackerPose.Snatch or AttackerPose.Tongue ||
                actor.pose==AttackerPose.Carry && (actor.stolenId>0 || actor.victim>=0) ||
                actor.pose==AttackerPose.Retreat && Defense.phase==DefensePhase.Resolution;
            if(view.wake==null && rushing)
            {
                if(!raidWakeMaterial)
                {
                    var shader=Resources.Load<Shader>("NereidFishWake");
                    if(!shader)return;
                    raidWakeMaterial=new Material(shader){name="Raider cavitation"};
                }
                view.wake=new TrailRenderer[2];
                for(int i=0;i<view.wake.Length;i++)
                {
                    var node=new GameObject("Raider pressure trail");
                    node.transform.SetParent(view.root,false);
                    var trail=view.wake[i]=node.AddComponent<TrailRenderer>();
                    trail.sharedMaterial=raidWakeMaterial;
                    trail.time=i==0?.64f:.48f;
                    trail.minVertexDistance=.10f;
                    trail.widthCurve=new AnimationCurve(new Keyframe(0,.42f),new Keyframe(.19f,1),new Keyframe(1,0));
                    trail.colorGradient=new Gradient{
                        colorKeys=new[]{new GradientColorKey(new Color(.58f,.94f,1),0),new GradientColorKey(new Color(.15f,.43f,.70f),1)},
                        alphaKeys=new[]{new GradientAlphaKey(.76f,0),new GradientAlphaKey(.40f,.50f),new GradientAlphaKey(0,1)}};
                    trail.shadowCastingMode=ShadowCastingMode.Off;
                    trail.receiveShadows=false;
                }
            }
            if(view.wake==null)return;
            for(int i=0;i<view.wake.Length;i++)
            {
                var trail=view.wake[i];
                trail.transform.localPosition=new Vector3((i==0?-1:1)*(.23f+.065f*Mathf.Sin(view.phase)),.10f,-.78f);
                trail.widthMultiplier=Mathf.Lerp(.23f,.63f,pace)*actor.scale;
                trail.emitting=rushing && pace>.16f;
            }
        }

        void BurstRaider(VisibleAttacker view,AttackerSnapshot actor)
        {
            if(!view.thrust)
            {
                var material=Resources.Load<Material>("NereidBubbles");
                if(!material)return;
                var node=new GameObject("Raider burst bubbles");
                node.transform.SetParent(view.root,false);
                view.thrust=node.AddComponent<ParticleSystem>();
                view.thrust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=view.thrust.main;
                main.playOnAwake=false;main.loop=false;main.maxParticles=45;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.31f,.91f);
                main.startSize=new ParticleSystem.MinMaxCurve(.028f,.094f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(1.8f,3.9f);
                var emission=view.thrust.emission;
                emission.enabled=false;
                var shape=view.thrust.shape;
                shape.shapeType=ParticleSystemShapeType.Cone;
                shape.angle=38;
                shape.radius=.20f;
                var renderer=node.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=material;
                renderer.renderMode=ParticleSystemRenderMode.Billboard;
            }
            bool contact=actor.pose is AttackerPose.Latch or AttackerPose.Tongue;
            Vector3 origin=contact && view.mouth?view.mouth.localPosition:new Vector3(0,0,-.80f);
            view.thrust.transform.localPosition=origin;
            view.thrust.transform.localRotation=Quaternion.Euler(contact?-55:18,contact?0:180,0);
            view.thrust.Emit(actor.pose==AttackerPose.Tongue?13:contact?27:actor.kind==DefenseSpecies.Rammer?35:21);
        }
    }
}
