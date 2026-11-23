using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class CargoLandingSilt : MonoBehaviour
    {
        [Tooltip("Height above the seabed at which the descending cage first stirs fines.")]
        [Min(.2f)] public float washHeight=1.6f;
        [Min(.02f)] public float contactHeight=.18f;
        [Range(8,64)] public int touchdownCount=34;
        [Min(.1f)] public float spreadSpeed=2.45f;

        ParticleSystem cloud,fines;
        Material material,fineMaterial;
        Mesh puff;
        Collider bed;
        Vector3 previous;
        bool returning,settled,sampled;
        int leadPuffs;
        const int LeadCount=21;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal int Impacts {get;private set;}
        internal int VisiblePuffs => cloud ? cloud.particleCount : 0;
        internal float ImpactGap {get;private set;}
#endif

        internal void Present(HoistPhase phase,Vector3 berth,Collider ground)
        {
            if(bed!=ground)
            {
                bed=ground;returning=false;sampled=false;
                if(cloud){cloud.Clear();if(fines)fines.Clear();}
            }
            var foot=transform.TransformPoint(new Vector3(0,CargoBasket.Floor-.12f,0));
            bool dropping=sampled && foot.y<previous.y-.00001f;
            previous=foot;sampled=true;
            // Replication can skip the last return frame, but a client joining a parked
            // cage has never seen a descent and must not replay the touchdown.
            bool finalStep=returning && phase==HoistPhase.Loading;
            bool active=phase==HoistPhase.Returning;
            if(active && !returning){settled=false;leadPuffs=0;}
            returning=active;
            if(!bed || (!active && !finalStep) || settled || !dropping)return;
            var lateral=foot-berth;lateral.y=0;
            if(lateral.sqrMagnitude>5f || !bed.Raycast(new Ray(foot+Vector3.up,Vector3.down),out var bottom,washHeight+1.12f))return;
            float gap=foot.y-bottom.point.y;
            if(gap>washHeight || gap<-.32f || !CreateCloud())return;
            float width=Mathf.Max(.01f,washHeight-contactHeight);
            int wanted=Mathf.FloorToInt(LeadCount*Mathf.Clamp01((washHeight-gap)/width));
            while(leadPuffs<wanted)
            {
                int index=leadPuffs++;
                Emit(bottom,index*2.39996f,.21f+.21f*Noise(index),spreadSpeed*.46f,1.2f);
            }
            if(gap>contactHeight)return;
            settled=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Impacts++;ImpactGap=gap;
#endif
            for(int i=0;i<touchdownCount;i++)
            {
                float angle=Mathf.PI*2*(i+Noise(i)*.55f)/touchdownCount;
                Emit(bottom,angle,.46f+.32f*Noise(i+33),spreadSpeed*(.70f+Noise(i+81)*.55f),2.15f);
                Emit(bottom,angle+.085f,.055f+.06f*Noise(i+17),spreadSpeed*1.18f,1.05f);
            }
        }

        static float Noise(int index)=>Mathf.Repeat(Mathf.Sin((index+9)*12.974f)*1321.67f,1);

        void Emit(RaycastHit centre,float angle,float radius,float speed,float lifetime)
        {
            var normal=centre.normal;
            var across=Vector3.ProjectOnPlane(transform.right,normal).normalized;
            var along=Vector3.Cross(across,normal);
            var direction=across*Mathf.Cos(angle)+along*Mathf.Sin(angle);
            Vector3 at=centre.point+direction*(1.64f+.28f*Mathf.Abs(Mathf.Sin(2*angle)));
            if(!bed.Raycast(new Ray(at+Vector3.up,Vector3.down),out var floor,2f))return;
            var tone=Mathf.Repeat(angle*3.37f,1);
            var particle=new ParticleSystem.EmitParams
            {
                position=floor.point+floor.normal*.075f,
                velocity=Vector3.ProjectOnPlane(direction,floor.normal).normalized*speed+floor.normal*(.28f+radius*.36f),
                startLifetime=lifetime*(.88f+.26f*tone),
                startSize3D=new Vector3(radius*2.4f,radius*1.25f,radius*1.7f),
                rotation3D=new Vector3(0,angle*Mathf.Rad2Deg,0),
                startColor=Color.Lerp(new Color(.39f,.37f,.28f,.29f),new Color(.63f,.59f,.46f,.38f),tone)
            };
            cloud.Emit(particle,1);
            if(!fines || radius<.14f)return;
            particle.startSize3D=new Vector3(radius*4.6f,radius*2.9f,radius*3.4f);
            particle.startLifetime*=1.46f;
            particle.velocity=particle.velocity*.87f+floor.normal*.25f;
            particle.startColor=Color.Lerp(new Color(.40f,.38f,.29f,.59f),new Color(.62f,.57f,.43f,.70f),tone);
            fines.Emit(particle,1);
        }

        bool CreateCloud()
        {
            if(cloud)return true;
            var shader=Resources.Load<Shader>("NereidLandingSand");
            if(!shader || !shader.isSupported)return false;
            material=new Material(shader){name="Suspended seabed sand"};
            puff=MakePuff();
            var child=new GameObject("Touchdown sediment");
            child.transform.SetParent(transform,false);
            cloud=child.AddComponent<ParticleSystem>();
            cloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=cloud.main;main.loop=true;main.playOnAwake=false;main.startSize3D=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            main.maxParticles=180;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=cloud.emission;emission.enabled=false;
            var shape=cloud.shape;shape.enabled=false;
            var fade=cloud.colorOverLifetime;fade.enabled=true;
            var grad=new Gradient();
            grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.68f,.82f,.79f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.76f,.12f),new GradientAlphaKey(.44f,.47f),new GradientAlphaKey(0,1)});
            fade.color=grad;
            var grow=cloud.sizeOverLifetime;grow.enabled=true;
            grow.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.42f),new Keyframe(.32f,1.1f),new Keyframe(1,1.8f)));
            var velocity=cloud.velocityOverLifetime;velocity.enabled=true;
            velocity.speedModifier=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.22f));
            var renderer=cloud.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=puff;renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var fineShader=Resources.Load<Shader>("NereidSilt");
            if(fineShader)
            {
                fineMaterial=new Material(fineShader){name="Fine suspended sand"};
                fines=Instantiate(cloud,transform,false);fines.name="Touchdown suspended fines";
                var fineRenderer=fines.GetComponent<ParticleSystemRenderer>();
                fineRenderer.renderMode=ParticleSystemRenderMode.Billboard;fineRenderer.sharedMaterial=fineMaterial;
                fines.Play();
            }
            cloud.Play();return true;
        }

        static Mesh MakePuff()
        {
            const int sides=7;
            var vertices=new Vector3[sides*6];var triangles=new int[vertices.Length];
            Vector3 Ring(int i)
            {
                float a=i*Mathf.PI*2/sides,r=.44f+.09f*Noise(i%sides);
                return new Vector3(r*Mathf.Cos(a),.05f*Mathf.Sin(a*2),r*Mathf.Sin(a));
            }
            for(int i=0;i<sides;i++)
            {
                var a=Ring(i);var b=Ring(i+1);int n=i*6;
                vertices[n]=b;vertices[n+1]=a;vertices[n+2]=new Vector3(-.05f,.43f,.04f);
                vertices[n+3]=a;vertices[n+4]=b;vertices[n+5]=new Vector3(.09f,-.32f,-.03f);
            }
            for(int i=0;i<vertices.Length;i++)triangles[i]=i;
            var mesh=new Mesh{name="Faceted sediment lobe",vertices=vertices,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        void OnDestroy(){if(material)Destroy(material);if(fineMaterial)Destroy(fineMaterial);if(puff)Destroy(puff);}
    }
}
