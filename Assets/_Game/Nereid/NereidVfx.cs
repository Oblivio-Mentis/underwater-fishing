using UnityEngine;

namespace Raid.Nereid
{
    // Machinery cues follow the replicated route, including remote-client starts and interruption.
    public sealed class NereidVfx : MonoBehaviour
    {
        ParticleSystem outlet,mouth;
        AudioSource machine,feeding;
        AudioClip motorClip,flowClip;
        Material particles;
        Factory.FactoryVisuals factory;

        void Awake()
        {
            factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            var source=Resources.Load<Material>("NereidBubbles");if(source==null||factory==null){enabled=false;return;}
            particles=new Material(source);
            outlet=MakeParticles("Processor water vapour",new Vector3(-3.45f,1.8f,-5.1f),new Color(.5f,.62f,.63f,.11f),.14f,.8f);
            mouth=MakeParticles("Feeding pressure bubbles",new Vector3(1.35f,2.5f,7.55f),new Color(.35f,.67f,.72f,.2f),.055f,.7f);
            machine=MakeAudio("Conveyor drive",new Vector3(-3.5f,1,-6.5f),.12f);
            feeding=MakeAudio("Feed pipe circulation",new Vector3(1.9f,2,-2),.09f);
            motorClip=Sound("NEREID conveyor motor",false);flowClip=Sound("NEREID pressure feed",true);machine.clip=motorClip;feeding.clip=flowClip;
        }

        void Update()
        {
            var w=RaidWorld.Instance;
            bool connected=w!=null&&w.NetworkObject!=null&&w.IsClientInitialized;
            bool active=connected&&w.FishRoute.active;
            bool feed=active&&w.FishRoute.feeding;
            var emission=outlet.emission;emission.rateOverTime=active&&!feed?9:0;
            emission=mouth.emission;emission.rateOverTime=feed?7:0;
            SetAudio(machine,active&&!feed);SetAudio(feeding,feed);
        }

        static void SetAudio(AudioSource source,bool active)
        {
            source.mute=!Application.isFocused;
            if(active&&!source.isPlaying)source.Play();else if(!active&&source.isPlaying)source.Stop();
        }

        ParticleSystem MakeParticles(string name,Vector3 point,Color color,float size,float speed)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=point;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startColor=color;main.startSize=new ParticleSystem.MinMaxCurve(size*.35f,size);main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.9f);
            main.startSpeed=speed;main.maxParticles=20;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.16f;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.y=.18f;
            var emission=ps.emission;emission.rateOverTime=0;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particles;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=factory.bubbleMesh;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;ps.Play();return ps;
        }

        AudioSource MakeAudio(string name,Vector3 point,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=point;
            var source=go.AddComponent<AudioSource>();source.spatialBlend=1;source.volume=volume;source.loop=true;source.playOnAwake=false;
            source.dopplerLevel=0;source.minDistance=1;source.maxDistance=12;source.rolloffMode=AudioRolloffMode.Linear;return source;
        }

        static AudioClip Sound(string name,bool water)
        {
            const int rate=22050;var samples=new float[rate*2];var random=new System.Random(water?12102:12101);float noise=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;noise=noise*.94f+(float)(random.NextDouble()*2-1)*.06f;
                float hum=Mathf.Sin(2*Mathf.PI*(water?79:48)*t);
                float pulse=.8f+.2f*Mathf.Sin(2*Mathf.PI*(water?3:8)*t);
                samples[i]=(noise*(water?.7f:.2f)+hum*(water?.03f:.1f))*pulse;
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void OnDestroy()
        {if(particles!=null)Destroy(particles);if(motorClip!=null)Destroy(motorClip);if(flowClip!=null)Destroy(flowClip);}
    }
}
