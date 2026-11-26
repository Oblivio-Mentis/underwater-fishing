using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    internal sealed class FacetedFlowMesh : System.IDisposable
    {
        readonly Vector3[] points;
        readonly Color[] colors;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Material material;
        readonly int strands, rings, sides;

        internal FacetedFlowMesh(Transform parent,string name,int strands,int rings,int sides)
        {
            this.strands=strands;this.rings=rings;this.sides=sides;
            var node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            node.transform.SetParent(parent,false);
            renderer=node.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            var shader=Resources.Load<Shader>("RaidFacetedFlow");
            material=new Material(shader?shader:Shader.Find("Universal Render Pipeline/Unlit"));
            renderer.sharedMaterial=material;
            var uv=new Vector2[strands*rings*sides*2];
            points=new Vector3[uv.Length];
            colors=new Color[uv.Length];
            var indices=new int[strands*(rings-1)*sides*6];
            int face=0;
            for(int strand=0;strand<strands;strand++)
            for(int ring=0;ring<rings;ring++)
            for(int edge=0;edge<sides;edge++)
            {
                int i=Vertex(strand,ring,edge);
                uv[i]=new Vector2(ring/(float)(rings-1),0);
                uv[i+1]=new Vector2(ring/(float)(rings-1),1);
                if(ring==rings-1)continue;
                int next=i+sides*2;
                indices[face++]=i;indices[face++]=i+1;indices[face++]=next;
                indices[face++]=i+1;indices[face++]=next+1;indices[face++]=next;
            }
            mesh=new Mesh{name=name};mesh.MarkDynamic();
            mesh.vertices=points;mesh.uv=uv;mesh.triangles=indices;
            node.GetComponent<MeshFilter>().sharedMesh=mesh;
            Hide();
        }

        int Vertex(int strand,int ring,int edge) => ((strand*rings+ring)*sides+edge)*2;
        internal void Place(Vector3 point,Quaternion rotation)
        {
            var node=renderer.transform;
            node.SetPositionAndRotation(point,rotation);
            var scale=node.parent.lossyScale;
            node.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
        }
        internal void Ring(int strand,int ring,Vector3 center,float width,float height,float twist,Color color,Quaternion? axis=null)
        {
            var orientation=axis??Quaternion.identity;
            for(int edge=0;edge<sides;edge++)
            {
                float theta=twist+edge*2*Mathf.PI/sides;
                float next=theta+2*Mathf.PI/sides;
                int i=Vertex(strand,ring,edge);
                points[i]=center+orientation*new Vector3(Mathf.Cos(theta)*width,Mathf.Sin(theta)*height,0);
                points[i+1]=center+orientation*new Vector3(Mathf.Cos(next)*width,Mathf.Sin(next)*height,0);
                float light=.81f+.19f*Mathf.Sin((theta+next)*.5f);
                colors[i]=colors[i+1]=new Color(color.r*light,color.g*light,color.b*light,color.a);
            }
        }

        internal void Draw()
        {
            mesh.vertices=points;mesh.colors=colors;mesh.RecalculateBounds();
            renderer.enabled=true;
        }
        internal void Hide(){if(renderer)renderer.enabled=false;}
        public void Dispose()
        {
            if(renderer)Object.Destroy(renderer.gameObject);
            if(mesh)Object.Destroy(mesh);
            if(material)Object.Destroy(material);
        }
    }
}
