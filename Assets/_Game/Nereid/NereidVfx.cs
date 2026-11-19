using UnityEngine;

namespace Raid.Nereid
{
    // Machinery cues follow the replicated route, including remote-client starts and interruption.
    public sealed class NereidVfx : MonoBehaviour
    {
        ParticleSystem outlet,mouth;
        Material particles;
        Factory.FactoryVisuals factory;

        void Awake()
        {
            factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            var source=Resources.Load<Material>("NereidBubbles");if(source==null||factory==null){enabled=false;return;}
            particles=new Material(source);
            Vector3 discharge=factory.processTube!=null&&factory.processTube.Length>0?factory.processTube[factory.processTube.Length-1]:new Vector3(-3.45f,2.95f,-4.95f);
            outlet=MakeParticles("Processor water vapour",discharge+new Vector3(0,-1.15f,-.15f),new Color(.5f,.62f,.63f,.11f),.14f,.8f);
            mouth=MakeParticles("Feeding pressure bubbles",new Vector3(1.35f,2.5f,7.55f),new Color(.35f,.67f,.72f,.2f),.055f,.7f);
        }

        void Update()
        {
            var w=RaidWorld.Instance;
            bool connected=w!=null&&w.NetworkObject!=null&&w.IsClientInitialized;
            bool active=connected&&w.FishRoute.active;
            bool feed=active&&w.FishRoute.feeding;
            var emission=outlet.emission;emission.rateOverTime=active&&!feed?9:0;
            emission=mouth.emission;emission.rateOverTime=feed?7:0;
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

        void OnDestroy()
        {if(particles!=null)Destroy(particles);}
    }
}
