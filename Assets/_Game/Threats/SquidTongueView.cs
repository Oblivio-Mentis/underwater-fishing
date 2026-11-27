using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed class SquidTongueView : MonoBehaviour
    {
        const int Rings=17, Sides=7;
        static Material tissue;
        readonly Vector3[] points=new Vector3[Rings*Sides];
        MaterialPropertyBlock properties;
        Mesh mesh;
        MeshRenderer draw;

        internal static SquidTongueView Attach(Transform host)
        {
            if(!tissue)
            {
                var shader=Resources.Load<Shader>("ExpeditionSquidTongue");
                if(!shader)return null;
                tissue=new Material(shader){name="Squid tongue tissue",hideFlags=HideFlags.DontSave};
            }
            var part=new GameObject("Squid tongue",typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(host,false);
            var view=part.AddComponent<SquidTongueView>();
            view.Build();
            return view;
        }

        void Build()
        {
            properties=new MaterialPropertyBlock();
            var uv=new Vector2[points.Length];
            var indices=new int[(Rings-1)*Sides*6];
            int cursor=0;
            for(int ring=0;ring<Rings;ring++)
            for(int side=0;side<Sides;side++)
            {
                int a=ring*Sides+side,b=ring*Sides+(side+1)%Sides;
                uv[a]=new Vector2(ring/(Rings-1f),side/(float)Sides);
                if(ring==Rings-1)continue;
                indices[cursor++]=a;indices[cursor++]=b;indices[cursor++]=a+Sides;
                indices[cursor++]=b;indices[cursor++]=b+Sides;indices[cursor++]=a+Sides;
            }
            mesh=new Mesh{name="Living tongue"};
            mesh.MarkDynamic();
            mesh.vertices=points;
            mesh.uv=uv;
            mesh.triangles=indices;
            GetComponent<MeshFilter>().sharedMesh=mesh;
            draw=GetComponent<MeshRenderer>();
            draw.sharedMaterial=tissue;
            draw.shadowCastingMode=ShadowCastingMode.Off;
            draw.receiveShadows=false;
            draw.enabled=false;
        }

        internal void Present(Vector3 origin,Vector3 target,float reach,bool gripping,float charge,float phase,bool firstPerson)
        {
            var delta=target-origin;
            draw.enabled=reach>.015f && delta.sqrMagnitude>.009f;
            if(!draw.enabled)return;
            float length=delta.magnitude;
            Vector3 direction=delta/length;
            Vector3 right=Vector3.Cross(Mathf.Abs(direction.y)>.93f?Vector3.forward:Vector3.up,direction).normalized;
            Vector3 up=Vector3.Cross(direction,right);
            var space=transform.worldToLocalMatrix;
            for(int ring=0;ring<Rings;ring++)
            {
                float t=ring/(Rings-1f);
                float arc=Mathf.Sin(t*Mathf.PI)*Mathf.Min(.62f,length*.12f);
                Vector3 p=Vector3.Lerp(origin,target,t)+
                    (right*(Mathf.Sin(t*6.0f-phase)*.55f)-up*(gripping?.31f:.76f))*arc;
                float radius=Mathf.Lerp(.151f,.055f,t)+.079f*Mathf.Exp(-Mathf.Pow((t-.87f)*16,2));
                if(t>.90f)radius*=Mathf.Lerp(1,.14f,Mathf.InverseLerp(.90f,1,t));
                for(int side=0;side<Sides;side++)
                {
                    float angle=side*Mathf.PI*2/Sides;
                    var rim=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle)*.69f)*radius;
                    points[ring*Sides+side]=space.MultiplyPoint3x4(p+rim);
                }
            }
            mesh.vertices=points;
            mesh.RecalculateBounds();
            properties.SetFloat("_Grip",gripping?1:charge*.4f);
            properties.SetFloat("_CameraClearance",firstPerson?.63f:.075f);
            draw.SetPropertyBlock(properties);
        }

        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
