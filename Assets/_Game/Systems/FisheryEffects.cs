using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal static class FisheryEffects
    {
        static readonly AudioClip[] cues=new AudioClip[5];
        static Material bubbles;
        static Mesh mesh;

        public static void Play(Vector3 at, byte cue)
        {
            if(cue>=cues.Length || Application.isBatchMode)return;
            if(!cues[cue])cues[cue]=MakeSound(cue);
            AudioSource.PlayClipAtPoint(cues[cue],at,cue==1?.85f:.6f);
            if(cue!=1 && cue!=3)return;
            var go=new GameObject(cue==1?"Fishery · cavitation bloom":"Cage · displaced water");
            go.transform.position=at;
            var ps=go.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.duration=.3f;main.maxParticles=96;
            main.startSize=new ParticleSystem.MinMaxCurve(.04f,cue==1?.52f:.17f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,cue==1?3.8f:1.4f);
            main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,cue==1?2.6f:1.1f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.46f,.66f,.61f,.7f),new Color(.85f,.97f,.95f,.85f));
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=cue==1?.63f:.35f;
            var speed=ps.velocityOverLifetime;speed.enabled=true;speed.y=.63f;
            var emit=ps.emission;emit.rateOverTime=0;
            var draw=ps.GetComponent<ParticleSystemRenderer>();
            if(!bubbles)bubbles=Resources.Load<Material>("NereidBubbles");
            if(!mesh)
            {
                var factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
                if(factory)mesh=factory.bubbleMesh;
            }
            if(bubbles)draw.sharedMaterial=bubbles;
            if(mesh){draw.renderMode=ParticleSystemRenderMode.Mesh;draw.mesh=mesh;}
            draw.shadowCastingMode=ShadowCastingMode.Off;
            ps.Play();ps.Emit(cue==1?92:28);
            Object.Destroy(go,3f);
        }

        static AudioClip MakeSound(int cue)
        {
            const int rate=22050;
            float seconds=cue==1?.9f:cue==0?.64f:.37f;
            float[] data=new float[Mathf.CeilToInt(rate*seconds)];
            var random=new System.Random(6083+cue);
            float low=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate;
                float phase=t/seconds, envelope=Mathf.Exp(-phase*(cue==1?5.5f:6f));
                low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.12f);
                float sample;
                if(cue==0) sample=Mathf.Sin(2*Mathf.PI*680*t)*Mathf.Exp(-12*(t%.21f));
                else if(cue==1) sample=Mathf.Sin(2*Mathf.PI*(81*t-26*t*t))*.64f+low*.8f;
                else sample=Mathf.Sin(2*Mathf.PI*(cue==2?430:127)*t)*.5f+low*(cue==2?.2f:.9f);
                data[i]=sample*envelope*Mathf.Clamp01(t*500);
            }
            var clip=AudioClip.Create("Fishery underwater cue "+cue,data.Length,1,rate,false);
            clip.SetData(data,0);return clip;
        }
    }
}
