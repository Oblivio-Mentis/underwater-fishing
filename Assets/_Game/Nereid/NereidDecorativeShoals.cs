using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Each visible shoal is a single instanced draw, without scene objects or network state.
    [DefaultExecutionOrder(290)]
    internal sealed class NereidDecorativeShoals : MonoBehaviour
    {
        internal static readonly Vector3 ShallowShoal = new(34,-5.6f,-31.5f);
        internal static readonly Vector3 DeepShoal = new(44,-7.9f,-44.7f);
        const int MaxSchoolSize=32;
        readonly Matrix4x4[] instances=new Matrix4x4[MaxSchoolSize];
        Material silver, slate;
        School[] schools;

        readonly struct Member
        {
            public readonly Vector3 offset;
            public readonly float length, phase;

            public Member(System.Random random, float size)
            {
                float angle=Next(random)*Mathf.PI*2;
                float ring=Mathf.Sqrt(Next(random));
                offset=new Vector3(Mathf.Cos(angle)*ring*1.55f,(Next(random)-.5f)*1.27f,Mathf.Sin(angle)*ring*2.45f);
                length=size*Mathf.Lerp(.70f,1.24f,Next(random));
                phase=Next(random)*Mathf.PI*2;
            }
        }

        sealed class School
        {
            public readonly int tier;
            public readonly Mesh mesh;
            public readonly Material material;
            public readonly Vector3 center, radii;
            public readonly float speed, phase, visibleRange;
            public readonly Member[] members;

            public School(int tier, Mesh mesh, Material material, Vector3 center, Vector3 radii,
                float speed, float phase, float visibleRange, int count, float size)
            {
                this.tier=tier;
                this.mesh=mesh;
                this.material=material;
                this.center=center;
                this.radii=radii;
                this.speed=speed;
                this.phase=phase;
                this.visibleRange=visibleRange;
                members=new Member[count];
                var random=new System.Random((int)(center.x*31-center.z*17));
                for(int i=0;i<count;i++)members[i]=new Member(random,size);
            }

            public Vector3 OnPath(float angle) => center+new Vector3(
                Mathf.Cos(angle)*radii.x,Mathf.Sin(angle*2+.7f)*radii.y,Mathf.Sin(angle)*radii.z);
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        void Awake()
        {
            var source=Resources.Load<Material>("NereidShoal");
            if(source==null || !SystemInfo.supportsInstancing){enabled=false;return;}
            var silverMesh=LoadMesh("Silver shoal fish");
            var slateMesh=LoadMesh("Rift shoal fish");
            silver=new Material(source){name="Silver shoal scales"};
            silver.SetColor("_Back",new Color(.125f,.255f,.269f));
            silver.SetColor("_Belly",new Color(.49f,.63f,.57f));
            silver.SetColor("_Fin",new Color(.19f,.36f,.36f));
            slate=new Material(source){name="Rift shoal scales"};
            slate.SetColor("_Back",new Color(.157f,.218f,.286f));
            slate.SetColor("_Belly",new Color(.343f,.46f,.5f));
            slate.SetColor("_Fin",new Color(.174f,.28f,.32f));
            // Keep clear of the cargo cage and arch footing, and above the rift ledges.
            schools=new[]
            {
                new School(1,silverMesh,silver,ShallowShoal,new Vector3(4f,.42f,2.8f),.39f,1.2f,38,30,.31f),
                new School(1,slateMesh,slate,new Vector3(47,-2.9f,-55),new Vector3(4.6f,.52f,2.7f),-.31f,3.8f,39,19,.28f),
                new School(2,slateMesh,slate,DeepShoal,new Vector3(2f,.65f,3.35f),.38f,2.9f,22,22,.35f),
                new School(2,silverMesh,silver,new Vector3(45.3f,-8.8f,-50.1f),new Vector3(1.5f,.52f,2.2f),-.35f,2.1f,21,13,.26f),
            };
        }

        static Mesh LoadMesh(string name)
        {
            var prefab=Resources.Load<GameObject>("DecorativeFish/"+name);
            return prefab!=null?prefab.GetComponentInChildren<MeshFilter>()?.sharedMesh:null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnEnable()=>ArtPassTrial.PrepareShoalStill+=ShoalStill;
        void OnDisable()=>ArtPassTrial.PrepareShoalStill-=ShoalStill;
        void ShoalStill(Camera camera)=>Draw(camera,Time.time,true);
#endif

        void LateUpdate()
        {
            var world=RaidWorld.Instance;
            var look=UnderwaterLook.Instance;
            if(world==null || world.NetworkObject==null || !world.IsClientInitialized || look==null ||
                !look.EyeSubmerged || look.DiverCamera==null)return;
            Draw(look.DiverCamera,Time.time,false);
        }

        void Draw(Camera camera,float time,bool still)
        {
            var world=RaidWorld.Instance;
            Vector3 eye=camera.transform.position;
            foreach(var school in schools)
            {
                if(school.tier!=world.CurrentDepth || school.mesh==null || school.mesh.subMeshCount!=1 ||
                    (eye-school.center).sqrMagnitude>school.visibleRange*school.visibleRange)continue;

                float angle=still?Mathf.PI*.5f:time*school.speed+school.phase;
                Vector3 center=school.OnPath(angle);
                Vector3 heading=(school.OnPath(angle+school.speed*.045f)-center).normalized;
                var formation=Quaternion.LookRotation(heading);
                int count=0;
                foreach(var fish in school.members)
                {
                    float beat=time*2.4f+fish.phase;
                    var offset=fish.offset+new Vector3(.21f*Mathf.Sin(beat),.13f*Mathf.Cos(beat*.71f),.19f*Mathf.Cos(beat));
                    Vector3 position=center+formation*offset;
                    // Keep the small background silhouettes out of the diver's workspace.
                    if((position-eye).sqrMagnitude<2.05f*2.05f)continue;
                    var bank=Quaternion.Euler(Mathf.Cos(beat)*4,Mathf.Sin(beat)*7,Mathf.Sign(school.speed)*12+Mathf.Cos(beat*.76f)*6);
                    instances[count++]=Matrix4x4.TRS(position,formation*bank,Vector3.one*fish.length);
                }
                if(count==0)continue;
                var parameters=new RenderParams(school.material)
                {
                    camera=camera,
                    worldBounds=new Bounds(center,new Vector3(6.7f,2.8f,6.7f)),
                    shadowCastingMode=ShadowCastingMode.Off,
                    receiveShadows=false,
                    lightProbeUsage=LightProbeUsage.Off
                };
                Graphics.RenderMeshInstanced(parameters,school.mesh,0,instances,count);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(still)Debug.Log($"[RAID016 school] {school.tier} {school.mesh.name} x{count} at {center}");
#endif
            }
        }

        void OnDestroy()
        {
            if(silver!=null)Destroy(silver);
            if(slate!=null)Destroy(slate);
        }
    }
}
