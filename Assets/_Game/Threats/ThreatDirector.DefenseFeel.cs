using Raid.Nereid;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        int pressureSeed, observedImpact, observedGrip;
        bool observedBreach;
        ParticleSystem pressureSilt;

        void PresentRaidPressure(DefenseSnapshot snapshot,DefenseFieldView field)
        {
            var player=UI.UiRoot.Instance?.Context?.Player;
            int grip=player?player.RaidGripVisual:0;
            if(grip!=observedGrip && grip>0 && player.CameraImpulse)
            {
                for(int i=0;i<defenseActors.Count;i++)
                {
                    var actor=defenseActors[i];
                    if(actor.id!=grip || DefenseClock-actor.tongueGripAt>.48)continue;
                    Vector3 toward=actor.position-player.transform.position;
                    float side=Vector3.Dot(player.CameraImpulse.transform.right,toward.normalized);
                    player.CameraImpulse.Hit(.033f,2.25f,side);
                    break;
                }
            }
            observedGrip=grip;

            if(pressureSeed!=snapshot.seed || snapshot.impact<observedImpact)
            {
                pressureSeed=snapshot.seed;
                observedImpact=snapshot.impact;
                observedBreach=snapshot.breached;
                return;
            }

            bool breach=snapshot.breached && !observedBreach;
            bool impact=snapshot.impact>observedImpact && snapshot.impactStrength>.36f;
            observedImpact=snapshot.impact;
            observedBreach=snapshot.breached;
            if(!breach && !impact)return;
            float age=Mathf.Max(0,(float)(DefenseClock-snapshot.impactAt));
            if(age>.66f)return;
            if(player && player.CameraImpulse)
                player.CameraImpulse.FieldImpact(snapshot.impactPoint,breach?1:snapshot.impactStrength,breach,age);
            if(field)
            {
                Vector3 point=field.ContactWorld(snapshot.impactPoint);
                Vector3 normal=(field.ContactWorld(snapshot.impactPoint,.2f)-point).normalized;
                EmitShieldSilt(point,normal,breach,snapshot.impact);
            }
        }

        void EmitShieldSilt(Vector3 point,Vector3 normal,bool breach,int sequence)
        {
            if(!pressureSilt)
            {
                var material=Resources.Load<Material>("ExpeditionPressureSilt");
                if(!material)return;
                var node=new GameObject("Field impact suspended sediment");
                node.transform.SetParent(transform,false);
                pressureSilt=node.AddComponent<ParticleSystem>();
                pressureSilt.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=pressureSilt.main;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.playOnAwake=false;
                main.loop=false;
                main.maxParticles=104;
                var emission=pressureSilt.emission;
                emission.enabled=false;
                var shape=pressureSilt.shape;
                shape.enabled=false;
                var color=pressureSilt.colorOverLifetime;
                color.enabled=true;
                color.color=new ParticleSystem.MinMaxGradient(new Gradient{
                    colorKeys=new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    alphaKeys=new[]{new GradientAlphaKey(.15f,0),new GradientAlphaKey(.85f,.09f),
                        new GradientAlphaKey(.55f,.38f),new GradientAlphaKey(0,1)}});
                var drag=pressureSilt.limitVelocityOverLifetime;
                drag.enabled=true;
                drag.limit=1.1f;
                drag.dampen=.095f;
                var rendering=pressureSilt.GetComponent<ParticleSystemRenderer>();
                rendering.sharedMaterial=material;
                rendering.renderMode=ParticleSystemRenderMode.Billboard;
                rendering.shadowCastingMode=ShadowCastingMode.Off;
                rendering.receiveShadows=false;
            }

            var side=Vector3.Cross(normal,Mathf.Abs(normal.y)>.95f?Vector3.forward:Vector3.up).normalized;
            var up=Vector3.Cross(normal,side);
            var random=new System.Random(unchecked(pressureSeed*397+sequence));
            int count=breach?60:24;
            for(int i=0;i<count;i++)
            {
                float phase=(float)random.NextDouble()*Mathf.PI*2;
                float scatter=(float)random.NextDouble();
                var tangent=side*Mathf.Cos(phase)+up*Mathf.Sin(phase);
                float span=Mathf.Lerp(.16f,breach?.74f:.34f,scatter);
                pressureSilt.Emit(new ParticleSystem.EmitParams{
                    position=point+normal*.035f+tangent*span,
                    velocity=tangent*Mathf.Lerp(.85f,breach?4.8f:2.5f,scatter)
                        +normal*Mathf.Lerp(.32f,1.4f,(float)random.NextDouble())+Vector3.up*.11f,
                    startLifetime=Mathf.Lerp(.28f,breach?1.45f:.84f,(float)random.NextDouble()),
                    startSize=Mathf.Lerp(.065f,breach?.35f:.22f,scatter),
                    rotation=(float)random.NextDouble()*360f,
                    startColor=Color.Lerp(new Color(.19f,.43f,.49f,.68f),new Color(.54f,.81f,.80f,.79f),scatter)
                },1);
            }
        }

        void ClearRaidPressure()
        {
            observedImpact=observedGrip=pressureSeed=0;
            observedBreach=false;
            if(pressureSilt)Destroy(pressureSilt.gameObject);
            pressureSilt=null;
        }
    }
}
