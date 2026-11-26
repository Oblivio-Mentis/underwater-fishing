using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed class WaterPressureFx : MonoBehaviour
    {
        static Material pressure;
        static Mesh sphere,ring,funnel;
        static readonly int Tint=Shader.PropertyToID("_Tint"),Phase=Shader.PropertyToID("_Phase"),Mode=Shader.PropertyToID("_Mode");
        MaterialPropertyBlock properties;
        MeshRenderer surface,ground,core;
        float age;
        float range;

        static Mesh Shell(string name,bool vortex)
        {
            const int sides=44, levels=18;
            var points=new Vector3[(sides+1)*(levels+1)];
            var uv=new Vector2[points.Length];
            var faces=new int[sides*levels*6];
            int index=0;
            for(int i=0;i<=levels;i++)
            for(int j=0;j<=sides;j++)
            {
                float u=j/(float)sides,v=i/(float)levels, a=u*2*Mathf.PI;
                float radius=vortex?.17f+Mathf.Pow(v,1.6f)*1.34f:Mathf.Sin(v*Mathf.PI);
                float y=vortex?v*2.05f:Mathf.Cos(v*Mathf.PI);
                int row=i*(sides+1)+j;
                points[row]=new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);
                uv[row]=new Vector2(u,v);
                if(i==levels || j==sides)continue;
                int next=row+sides+1;
                faces[index++]=row;faces[index++]=row+1;faces[index++]=next;
                faces[index++]=row+1;faces[index++]=next+1;faces[index++]=next;
            }
            var mesh=new Mesh{name=name,vertices=points,uv=uv,triangles=faces};
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh RingMesh()
        {
            var mesh=new Mesh{name="Pressure front on the seabed",
                vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)},
                uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},
                triangles=new[]{0,1,2,0,2,3}};
            mesh.RecalculateNormals();
            return mesh;
        }

        static MeshRenderer Surface(Transform parent,string name,Mesh mesh)
        {
            if(!pressure)
            {
                var shader=Resources.Load<Shader>("RaidWaterPressure");
                if(!shader)return null;
                pressure=new Material(shader){name="NEREID · water pressure"};
            }
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var draw=go.AddComponent<MeshRenderer>();
            draw.sharedMaterial=pressure;
            draw.shadowCastingMode=ShadowCastingMode.Off;
            draw.receiveShadows=false;
            return draw;
        }

        static void Set(MeshRenderer draw,MaterialPropertyBlock block,int mode,Color tint,float phase)
        {
            if(!draw)return;
            block.SetFloat(Mode,mode);
            block.SetColor(Tint,tint);
            block.SetFloat(Phase,phase);
            draw.SetPropertyBlock(block);
        }

        internal static MeshRenderer BuildIntake(Transform parent)
        {
            if(!funnel)funnel=Shell("Twisted intake surface",true);
            return Surface(parent,"Воронка водозабора",funnel);
        }

        internal static void SetIntake(MeshRenderer surface,MaterialPropertyBlock block,float strength) =>
            Set(surface,block,2,new Color(.17f,.84f,1.25f,1),strength);

        internal static void Blast(Vector3 at,float distance)
        {
            var go=new GameObject("Динамит · фронт давления");
            go.transform.position=at;
            var fx=go.AddComponent<WaterPressureFx>();
            fx.range=distance;
            fx.properties=new MaterialPropertyBlock();
            if(!sphere)sphere=Shell("Spherical pressure front",false);
            if(!ring)ring=RingMesh();
            fx.surface=Surface(go.transform,"Объёмная ударная волна",sphere);
            fx.ground=Surface(go.transform,"Кольцо давления",ring);
            fx.core=Surface(go.transform,"Вспышка",sphere);
            if(fx.ground && Physics.Raycast(at+Vector3.up*.18f,Vector3.down,out var bottom,3f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                fx.ground.transform.position=bottom.point+Vector3.up*.14f;
            fx.Update();
            Destroy(go,1.2f);
        }

        void Update()
        {
            age+=Time.deltaTime;
            float phase=Mathf.Clamp01(age/.64f);
            float radius=Mathf.Lerp(.35f,range,1-Mathf.Pow(1-phase,1.55f));
            if(surface)surface.transform.localScale=Vector3.one*radius;
            if(ground)ground.transform.localScale=Vector3.one*(radius*1.13f);
            float coreAge=Mathf.Clamp01(age/.31f);
            if(core)
            {
                core.transform.localScale=Vector3.one*Mathf.Lerp(.28f,2.15f,coreAge);
                core.enabled=coreAge<1;
            }
            Set(surface,properties,0,new Color(.70f,.95f,1.35f,.79f),phase);
            Set(ground,properties,1,new Color(.31f,.78f,1.1f,.64f),phase);
            Set(core,properties,3,new Color(2.25f,1.14f,.52f,.94f),coreAge);
        }
    }
}
