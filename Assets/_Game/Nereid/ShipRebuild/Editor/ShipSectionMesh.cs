using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid.Editor
{
    // One editable mesh per assembly (pressure rib, keel, light bank, etc).
    // Points are laid out in vessel coordinates; the saved asset has an assembly-local pivot.
    internal sealed class ShipSectionMesh
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uv = new();
        readonly List<(int start,int count)> faces = new();
        readonly List<Material> finishes = new();
        readonly List<List<int>> indices = new();
        readonly List<(int vertex,Vector2 atlasUV)> printed = new();
        readonly Vector3 pivot;
        internal readonly Transform node;

        internal ShipSectionMesh(Transform room, Vector3 origin, string name, Vector3 at)
        {
            pivot = at;
            node = new GameObject(name).transform;
            node.SetParent(room, false);
            node.localPosition = at - origin;
        }

        List<int> Surface(Material m)
        {
            if (!m) throw new ArgumentNullException(nameof(m));
            var i=finishes.IndexOf(m);
            if (i>=0) return indices[i];
            finishes.Add(m);
            indices.Add(new List<int>());
            return indices[indices.Count-1];
        }

        internal void Face(Material mat, params Vector3[] points)
        {
            var tris=Surface(mat);
            var start=vertices.Count;
            faces.Add((start,points.Length));
            var along=(points[1]-points[0]).normalized;
            var across=Vector3.Cross(Vector3.Cross(along,points[2]-points[0]).normalized,along);
            foreach (var p in points)
            {
                vertices.Add(p-pivot);
                uv.Add(new Vector2(Vector3.Dot(p-points[0],along),Vector3.Dot(p-points[0],across)));
            }
            for (int i=1;i<points.Length-1;i++) { tris.Add(start); tris.Add(start+i); tris.Add(start+i+1); }
        }

        internal void PrintedFace(Material baseMaterial, int tile, Vector3 center, Vector3 u, Vector3 v,
            float halfWidth, float halfHeight, params Vector3[] polygon)
        {
            int start=vertices.Count;
            Face(baseMaterial,polygon);
            var chip=new Vector2(((tile%4)+.5f)*.25f,((tile/4)+.5f)*.25f);
            for(int i=0;i<polygon.Length;i++)
            {
                Vector3 delta=polygon[i]-center;
                var offset=new Vector2(Vector3.Dot(delta,u.normalized)/halfWidth,Vector3.Dot(delta,v.normalized)/halfHeight);
                printed.Add((start+i,chip+offset*.118f));
            }
        }

        internal void Box(Material m, Vector3 c, Vector3 size, Quaternion rotation=default, float bevel=.055f)
        {
            if(rotation==default) rotation=Quaternion.identity;
            var h=size*.5f;
            float k=Mathf.Min(bevel, Mathf.Min(h.x,h.z)*.40f);
            // Octagonal silhouette with a smaller top and bottom bevel. The plate retains a flat bearing face.
            var outline=new Vector2[]{new(-h.x+k,-h.z),new(h.x-k,-h.z),new(h.x,-h.z+k),new(h.x,h.z-k),
                new(h.x-k,h.z),new(-h.x+k,h.z),new(-h.x,h.z-k),new(-h.x,-h.z+k)};
            float edge=Mathf.Min(k*.36f,h.y*.26f);
            var rings=new Vector3[4][];
            for (int j=0;j<4;j++)
            {
                rings[j]=new Vector3[8];
                float inset=j==0||j==3?edge:0;
                float y=j==0?-h.y:j==1?-h.y+edge:j==2?h.y-edge:h.y;
                for(int i=0;i<8;i++)
                {
                    Vector2 q=outline[i];
                    rings[j][i]=c+rotation*new Vector3(q.x*(1-inset/h.x),y,q.y*(1-inset/h.z));
                }
            }
            Face(m,rings[0]);
            for(int j=0;j<3;j++) for(int i=0;i<8;i++)
            {
                int n=(i+1)%8;
                Face(m,rings[j][i],rings[j+1][i],rings[j+1][n],rings[j][n]);
            }
            Array.Reverse(rings[3]);
            Face(m,rings[3]);
        }

        internal void Beam(Material m, Vector3 a, Vector3 b, float width, float depth)
        {
            Vector3 run=b-a;
            Box(m,(a+b)*.5f,new Vector3(width,depth,run.magnitude),Quaternion.LookRotation(run,
                Mathf.Abs(run.normalized.y)>.98f ? Vector3.forward : Vector3.up));
        }

        internal void Tube(Material m, Vector3 a, Vector3 b, float r, float endRadius=-1, int sides=12)
        {
            Vector3 direction=(b-a).normalized;
            var u=Vector3.Cross(Mathf.Abs(direction.y)>.9f?Vector3.forward:Vector3.up,direction).normalized;
            var v=Vector3.Cross(direction,u);
            var ring=new Vector3[sides];var end=new Vector3[sides];
            for(int i=0;i<sides;i++)
            {
                float t=i*Mathf.PI*2/sides;
                var radial=Mathf.Cos(t)*u+Mathf.Sin(t)*v;
                ring[i]=a+r*radial; end[i]=b+(endRadius<0?r:endRadius)*radial;
            }
            Face(m,end);
            for(int i=0;i<sides;i++)
            {
                int n=(i+1)%sides;
                Face(m,ring[i],ring[n],end[n],end[i]);
            }
            Array.Reverse(ring);
            Face(m,ring);
        }

        internal void Guard(Material posts, Material rail, Vector3 a, Vector3 b, float height=.6f)
        {
            var up=Vector3.up;
            int n=Mathf.Max(1,Mathf.CeilToInt((b-a).magnitude/1.15f));
            for(int i=0;i<=n;i++)
            {
                Vector3 foot=Vector3.Lerp(a,b,(float)i/n);
                Tube(posts,foot,foot+up*height,.031f,-1,8);
            }
            Tube(rail,a+up*height,b+up*height,.040f);
            Tube(posts,a+up*(height*.49f),b+up*(height*.49f),.019f,-1,8);
        }

        internal void Collision(Vector3 centre,Vector3 size,Quaternion rotation=default)
        {
            if(rotation==default) rotation=Quaternion.identity;
            var host=node;
            if(rotation!=Quaternion.identity)
            {
                host=new GameObject("Inclined collision").transform;
                host.SetParent(node,false);host.localPosition=centre-pivot;host.localRotation=rotation;
            }
            var collider=host.gameObject.AddComponent<BoxCollider>();
            collider.center=host==node?centre-pivot:Vector3.zero; collider.size=size;
        }

        internal void Slab(Material mat,Vector3 centre,Vector3 size)
        {
            Box(mat,centre,size);Collision(centre,size);
        }

        internal void Lamp(string name, Color color, Vector3 p, float intensity, float worldRange)
        {
            var lamp=new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(node,false);lamp.transform.localPosition=p-pivot;
            lamp.type=LightType.Point;lamp.color=color;lamp.intensity=intensity;lamp.range=worldRange;
            lamp.shadows=LightShadows.None;lamp.renderMode=LightRenderMode.Auto;
        }

        // All fittings in a room kit share one standard URP/Lit atlas, including metal and emission.
        internal void StorePalette(string path, Material atlas, IReadOnlyDictionary<Material,Vector2> swatches)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                var swatchUV=new Vector2[vertices.Count];
                // Keep grain subtle on small fittings and limit stretching on large castings.
                // All vertices in a polygon stay within its padded atlas tile, including at lower mips.
                var detailUV=new Vector2[vertices.Count];
                foreach(var face in faces)
                {
                    Vector2 lo=uv[face.start],hi=lo;
                    for(int n=1;n<face.count;n++){lo=Vector2.Min(lo,uv[face.start+n]);hi=Vector2.Max(hi,uv[face.start+n]);}
                    var center=(lo+hi)*.5f;
                    float span=Mathf.Max(1.50f,Mathf.Max(hi.x-lo.x,hi.y-lo.y));
                    for(int n=0;n<face.count;n++) detailUV[face.start+n]=(uv[face.start+n]-center)*(.213f/span);
                }
                var triangles=new List<int>();
                for(int i=0;i<indices.Count;i++)
                {
                    Vector2 sample=swatches[finishes[i]];
                    foreach(int v in indices[i])swatchUV[v]=sample+detailUV[v];
                    triangles.AddRange(indices[i]);
                }
                foreach(var mark in printed)swatchUV[mark.vertex]=mark.atlasUV;
                mesh=new Mesh{name=node.name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.uv=swatchUV;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,path);
            }
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=atlas;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        internal void Store(string path)
        {
            if(vertices.Count==0)return;
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=new Mesh {name=node.name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=indices.Count;
                for(int i=0;i<indices.Count;i++)mesh.SetTriangles(indices[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                if(mesh.subMeshCount!=finishes.Count)throw new InvalidOperationException("Incorrect ship material slots");
                AssetDatabase.CreateAsset(mesh,path);
            }
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials=finishes.ToArray();
        }
    }
}
