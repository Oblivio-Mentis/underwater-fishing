using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // One GPU-animated mesh serves the filling jet and drain eddy. No particles are spawned on simulation ticks.
    public sealed class PressureTransferFlow : MonoBehaviour
    {
        static readonly Vector3 Outlet = new(2.4f,3.08f,2.66f);
        static readonly Vector3 Intake = new(2.4f,.38f,2.79f);
        static readonly int Flow = Shader.PropertyToID("_Transfer");
        static readonly int SurfaceMark = Shader.PropertyToID("_PumpDisturbance");
        const int Rings=16,Sides=8,Drops=34;
        const float JetSpeed=1.85f,Gravity=7.8f,DownSpeed=.35f;
        MeshRenderer ribbons,waterRenderer;
        Material flowMaterial;
        Mesh flowMesh;
        readonly MaterialPropertyBlock surfaceProps=new();
        int shownPhase;
        float shownHeight;

        public static PressureTransferFlow Install(Transform hull,MeshRenderer surface,Material steel,Material dark,Material lip)
        {
            // A bolted flange, short protruding barrel and recessed dark bore face into the chamber.
            Disc(hull,"Flood flange",Outlet+Vector3.forward*.34f,.53f,.17f,steel);
            Disc(hull,"Flood barrel",Outlet+Vector3.forward*.19f,.31f,.34f,dark);
            Disc(hull,"Flood nozzle lip",Outlet+Vector3.forward*.043f,.34f,.085f,lip);
            Disc(hull,"Flood nozzle bore",Outlet+Vector3.back*.002f,.245f,.008f,dark);
            var go=new GameObject("Pressure transfer | jet and intake wake");
            go.transform.SetParent(hull,false);
            var effect=go.AddComponent<PressureTransferFlow>();
            effect.waterRenderer=surface;
            effect.Build();
            return effect;
        }

        static void Disc(Transform parent,string label,Vector3 center,float diameter,float length,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name=label;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=center;
            go.transform.localRotation=Quaternion.Euler(90,0,0);
            go.transform.localScale=new Vector3(diameter,length*.5f,diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        }

        void Build()
        {
            var shader=Resources.Load<Shader>("PressurePumpFlow");
            if(shader==null)return;
            flowMaterial=new Material(shader){name="Aerated ballast discharge"};
            flowMaterial.SetVector("_Outlet",Outlet);
            flowMaterial.SetVector("_Intake",Intake);
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var triangles=new List<int>();
            for(int ring=0;ring<=Rings;ring++)
            {
                for(int side=0;side<Sides;side++)
                {
                    float a=side*(Mathf.PI*2/Sides);
                    vertices.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));
                    uv.Add(new Vector2(ring/(float)Rings,0));
                    if(ring==Rings)continue;
                    int i=ring*Sides+side,j=ring*Sides+(side+1)%Sides;
                    triangles.Add(i);triangles.Add(i+Sides);triangles.Add(j);
                    triangles.Add(j);triangles.Add(i+Sides);triangles.Add(j+Sides);
                }
            }
            // Small pointed drops have actual volume when viewed from above, below, or the side.
            Vector3[] corners={Vector3.up,Vector3.down,Vector3.right,Vector3.forward,Vector3.left,Vector3.back};
            for(int drop=0;drop<Drops;drop++)
            {
                int first=vertices.Count;
                for(int n=0;n<corners.Length;n++)
                {
                    vertices.Add(corners[n]);
                    uv.Add(new Vector2((drop+.5f)/Drops,1));
                }
                for(int n=0;n<4;n++)
                {
                    int i=first+2+n,j=first+2+(n+1)%4;
                    triangles.Add(first);triangles.Add(i);triangles.Add(j);
                    triangles.Add(first+1);triangles.Add(j);triangles.Add(i);
                }
            }
            flowMesh=new Mesh{name="Ballast flow template"};
            flowMesh.SetVertices(vertices);flowMesh.SetUVs(0,uv);flowMesh.SetTriangles(triangles,0);
            flowMesh.bounds=new Bounds(new Vector3(2.4f,2.12f,1.78f),new Vector3(2.05f,4.72f,2.68f));
            gameObject.AddComponent<MeshFilter>().sharedMesh=flowMesh;
            ribbons=gameObject.AddComponent<MeshRenderer>();ribbons.sharedMaterial=flowMaterial;
            ribbons.shadowCastingMode=ShadowCastingMode.Off;ribbons.receiveShadows=false;
            ribbons.lightProbeUsage=LightProbeUsage.Off;ribbons.reflectionProbeUsage=ReflectionProbeUsage.Off;
            ribbons.enabled=false;
            CheckBudgetAndReach();
            flowMesh.UploadMeshData(true);
        }

        static float FlightTime(float head)
        {
            return (Mathf.Sqrt(DownSpeed*DownSpeed+2*Gravity*Mathf.Max(0,head))-DownSpeed)/Gravity;
        }

        [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
        void CheckBudgetAndReach()
        {
            Debug.Assert(flowMesh.vertexCount<=350 && flowMesh.GetIndexCount(0)<=1600,"Pump mesh exceeded its single-pass geometry budget.");
            for(float h=.02f;h<4.2f;h+=.1f)
            {
                var impact=new Vector3(Outlet.x,h,Outlet.z-JetSpeed*FlightTime(Outlet.y-h));
                Debug.Assert(flowMesh.bounds.Contains(impact) && flowMesh.bounds.Contains(new Vector3(Intake.x,h,Intake.z-.61f)),
                    "Pump effect leaves its render bounds during a pressure cycle.");
                Debug.Assert(Vector2.Distance(new Vector2(impact.x,impact.z),new Vector2(Outlet.x,Outlet.z))<=1.65f,
                    "Jet reaches beyond the pump service bay.");
            }
        }

        public void Present(AirlockPhase phase,float surfaceY)
        {
            if(ribbons==null)return;
            int mode=phase==AirlockPhase.Flooding?1:phase==AirlockPhase.Draining?-1:0;
            if(mode==shownPhase && (mode==0 || Mathf.Abs(shownHeight-surfaceY)<.0002f))return;
            shownPhase=mode;shownHeight=surfaceY;ribbons.enabled=mode!=0;
            float h=Mathf.Max(.045f,surfaceY);
            float t=FlightTime(Outlet.y-h);
            flowMaterial.SetVector(Flow,new Vector4(h,mode,t,JetSpeed));
            var contact=mode<0?new Vector3(Intake.x,h,Intake.z-.61f):new Vector3(Outlet.x,h,Outlet.z-JetSpeed*t);
            Vector3 world=transform.TransformPoint(contact);
            // Fade the surface boil when the inlet is deeply submerged.
            float disturbance=mode>0?Mathf.Clamp01(1-(h-Outlet.y)*1.2f):mode;
            surfaceProps.SetVector(SurfaceMark,new Vector4(world.x,world.z,disturbance,0));
            waterRenderer.SetPropertyBlock(surfaceProps);
        }

        void OnDestroy()
        {
            if(flowMaterial!=null)Destroy(flowMaterial);
            if(flowMesh!=null)Destroy(flowMesh);
        }
    }
}
