using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed class SquidTongueView : MonoBehaviour
    {
        const int Rings=56, StemEnd=19, Sides=10;
        static Material tissue;
        readonly Vector3[] centers=new Vector3[Rings];
        readonly Vector3[] points=new Vector3[Rings*Sides+2];
        MaterialPropertyBlock properties;
        Mesh mesh;
        MeshRenderer draw;
        float wrapLength;
        Vector3 gripCenter;

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
            var indices=new int[Rings*Sides*6];
            int cursor=0;
            for(int ring=0;ring<Rings;ring++)
            for(int side=0;side<Sides;side++)
            {
                int a=ring*Sides+side,b=ring*Sides+(side+1)%Sides;
                uv[a]=new Vector2(ring/(Rings-1f),Mathf.Sin(side*Mathf.PI*2/Sides)*.5f+.5f);
                if(ring==Rings-1)continue;
                indices[cursor++]=a;indices[cursor++]=b;indices[cursor++]=a+Sides;
                indices[cursor++]=b;indices[cursor++]=b+Sides;indices[cursor++]=a+Sides;
            }
            int tip=(Rings-1)*Sides;
            for(int side=0;side<Sides;side++)
            {
                int next=(side+1)%Sides;
                indices[cursor++]=Rings*Sides;indices[cursor++]=next;indices[cursor++]=side;
                indices[cursor++]=Rings*Sides+1;indices[cursor++]=tip+side;indices[cursor++]=tip+next;
            }
            uv[Rings*Sides]=new Vector2(0,.5f);
            uv[Rings*Sides+1]=new Vector2(1,.5f);
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

        internal void Present(Vector3 origin,Vector3 target,DiverMotor captive,float wrap,float phase,bool firstPerson)
        {
            var delta=target-origin;
            bool winding=captive && wrap>.008f;
            draw.enabled=winding || delta.sqrMagnitude>.003f;
            wrapLength=0;
            if(!draw.enabled)return;
            Vector3 torsoUp=Vector3.up;
            if(winding)torsoUp=TraceGrip(origin,target,captive,wrap,phase);
            else TraceStrike(origin,target,phase);

            var space=transform.worldToLocalMatrix;
            Vector3 across=transform.right;
            for(int i=0;i<Rings;i++)
            {
                float t=i/(Rings-1f);
                Vector3 tangent=(centers[Mathf.Min(Rings-1,i+1)]-centers[Mathf.Max(0,i-1)]).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=transform.forward;
                Vector3 flat=Vector3.Cross(Vector3.up,tangent);
                if(flat.sqrMagnitude<.002f)flat=Vector3.ProjectOnPlane(across,tangent);
                Vector3 upright=Vector3.ProjectOnPlane(torsoUp,tangent);
                float turn=winding?Mathf.SmoothStep(0,1,Mathf.InverseLerp(StemEnd-6,StemEnd+1,i)):0;
                Vector3 desired=Vector3.Slerp(flat.normalized,upright.normalized,turn);
                desired=Quaternion.AngleAxis((1-turn)*17*Mathf.Sin(t*7-phase*.65f),tangent)*desired;
                if(desired.sqrMagnitude>.01f)across=desired;
                Vector3 edge=Vector3.Cross(tangent,across).normalized;
                float taper=1-.85f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1,t));
                float width=(winding?Mathf.Lerp(.101f,.068f,t)+.007f*turn:
                    Mathf.Lerp(.145f,.077f,t)+.041f*Mathf.Exp(-72*(t-.78f)*(t-.78f)))*taper;
                float thickness=width*Mathf.Lerp(.62f,.38f,turn);
                for(int side=0;side<Sides;side++)
                {
                    float angle=side*Mathf.PI*2/Sides;
                    float face=Mathf.Sin(angle);
                    float bevel=Mathf.Cos(angle);
                    float ridge=1+.21f*Mathf.Max(0,face)*Mathf.Pow(1-Mathf.Abs(bevel),4);
                    var rim=across*(bevel*width)+edge*(face*thickness*ridge);
                    points[i*Sides+side]=space.MultiplyPoint3x4(centers[i]+rim);
                }
            }
            points[Rings*Sides]=space.MultiplyPoint3x4(centers[0]-transform.forward*.008f);
            points[Rings*Sides+1]=space.MultiplyPoint3x4(centers[Rings-1]+
                (centers[Rings-1]-centers[Rings-2]).normalized*.018f);
            mesh.vertices=points;
            mesh.RecalculateBounds();
            properties.SetFloat("_Grip",winding?wrap:0);
            properties.SetFloat("_CameraClearance",firstPerson?.16f:.05f);
            draw.SetPropertyBlock(properties);
        }

        void TraceStrike(Vector3 origin,Vector3 target,float phase)
        {
            Vector3 leg=target-origin;
            Vector3 direction=leg.normalized;
            float length=leg.magnitude;
            Vector3 lateral=Vector3.Cross(Vector3.up,direction).normalized;
            float slack=Mathf.Min(.48f,length*.17f);
            Vector3 first=origin+Vector3.Slerp(direction,transform.forward,.35f)*(length*.31f)
                -Vector3.up*slack*.47f;
            Vector3 last=target-leg*.29f+(lateral*Mathf.Sin(phase*.58f)-Vector3.up)*slack;
            for(int i=0;i<Rings;i++)centers[i]=Bezier(origin,first,last,target,i/(Rings-1f));
        }

        Vector3 TraceGrip(Vector3 origin,Vector3 target,DiverMotor captive,float wrap,float phase)
        {
            captive.RaidStrugglePose(out float brace,out float kick);
            DiverMotor.RaidStruggleTorso(brace,kick,out var offset,out var tilt);
            Quaternion rotation=captive.transform.rotation*tilt;
            gripCenter=captive.transform.TransformPoint(offset);
            Vector3 near=Quaternion.Inverse(rotation)*(origin-gripCenter);
            if(near.x*near.x+near.z*near.z<.003f)near.z=1;
            float angle=Mathf.Atan2(near.x,near.z);
            const float turns=1.37f;
            float radiusX=.342f+.059f*(1-wrap);
            float radiusZ=.302f+.059f*(1-wrap);

            Vector3 Coil(float part)
            {
                float theta=angle+part*wrap*turns*Mathf.PI*2;
                float rise=.042f*Mathf.Sin(theta-.65f);
                float pulse=.013f*Mathf.Sin(part*Mathf.PI*2-phase*.73f);
                Vector3 around=new(Mathf.Sin(theta)*(radiusX+pulse),
                    .42f-part*wrap*.37f+rise,Mathf.Cos(theta)*(radiusZ+pulse));
                return gripCenter+rotation*around;
            }

            float close=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.16f,wrap));
            Vector3 entry=Vector3.Lerp(target,Coil(0),close);
            Vector3 intoCoil=(Coil(.022f)-Coil(0)).normalized;
            float lead=Mathf.Clamp(Vector3.Distance(origin,entry)*.25f,.12f,.78f);
            Vector3 line=(entry-origin).normalized;
            Vector3 start=origin+Vector3.Slerp(line,transform.forward,.22f)*lead-Vector3.up*lead*.31f;
            for(int i=0;i<=StemEnd;i++)
                centers[i]=Bezier(origin,start,entry-intoCoil*lead*.67f,entry,i/(float)StemEnd);
            for(int i=StemEnd+1;i<Rings;i++)
            {
                float part=(i-StemEnd)/(float)(Rings-1-StemEnd);
                centers[i]=Vector3.Lerp(target,Coil(part),close);
                wrapLength+=Vector3.Distance(centers[i-1],centers[i]);
            }
            return rotation*Vector3.up;
        }

        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {
            float r=1-t;
            return r*r*r*a+3*r*r*t*b+3*r*t*t*c+t*t*t*d;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool TrialWrap(out float span,out Vector3 center)
        {
            span=wrapLength;
            center=gripCenter;
            return draw && draw.enabled && wrapLength>1.25f;
        }
#endif

        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
