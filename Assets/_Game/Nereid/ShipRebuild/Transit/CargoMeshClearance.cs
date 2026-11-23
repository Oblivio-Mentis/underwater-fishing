#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Object=UnityEngine.Object;

namespace Raid.Nereid
{
    // Checks the triangles of visible cargo parts against the triangles of installed ship equipment.
    // A closed cage, an open unloading leaf and a rotating loading grille each have their own mesh pose.
    public sealed class CargoMeshClearance
    {
        readonly struct Triangle
        {
            public readonly Vector3 a,b,c;
            public readonly Bounds box;
            public Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                this.a=a;this.b=b;this.c=c;
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);box=bounds;
            }
            public Triangle In(Matrix4x4 frame)=>new(frame.MultiplyPoint3x4(a),frame.MultiplyPoint3x4(b),frame.MultiplyPoint3x4(c));
        }

        sealed class Node
        {
            public Bounds bounds;
            public int start,count;
            public Node first,second;
        }

        sealed class TriangleIndex
        {
            public readonly Triangle[] faces;
            public readonly Node root;
            public TriangleIndex(Vector3[] vertices,int[] indices)
            {
                faces=new Triangle[indices.Length/3];
                for(int i=0;i<faces.Length;i++)
                    faces[i]=new(vertices[indices[3*i]],vertices[indices[3*i+1]],vertices[indices[3*i+2]]);
                root=faces.Length==0?null:Build(0,faces.Length);
            }
            Node Build(int start,int count)
            {
                var n=new Node{bounds=faces[start].box,start=start,count=count};
                for(int i=start+1;i<start+count;i++)n.bounds.Encapsulate(faces[i].box);
                if(count<=12)return n;
                var size=n.bounds.size;int axis=size.x>size.y && size.x>size.z?0:size.y>size.z?1:2;
                Array.Sort(faces,start,count,Comparer<Triangle>.Create((a,b)=>a.box.center[axis].CompareTo(b.box.center[axis])));
                n.first=Build(start,count/2);n.second=Build(start+count/2,count-count/2);return n;
            }
            public bool Intersects(Node n,Triangle other)
            {
                if(n==null || !n.bounds.Intersects(other.box))return false;
                if(n.first!=null)return Intersects(n.first,other) || Intersects(n.second,other);
                for(int i=n.start;i<n.start+n.count;i++)
                    if(faces[i].box.Intersects(other.box) && Touches(faces[i],other))return true;
                return false;
            }
        }

        // Separating axes of both triangles and their edges. The lateral axes also handle coplanar faces.
        static bool Touches(Triangle p,Triangle q)
        {
            var u=p.b-p.a;var v=p.c-p.b;var w=p.a-p.c;
            var x=q.b-q.a;var y=q.c-q.b;var z=q.a-q.c;
            var n=Vector3.Cross(u,v);var m=Vector3.Cross(x,y);
            if(n.sqrMagnitude<1e-12f || m.sqrMagnitude<1e-12f)return false;
            bool Apart(Vector3 axis)
            {
                if(axis.sqrMagnitude<1e-14f)return false;
                float p0=Vector3.Dot(p.a,axis),p1=Vector3.Dot(p.b,axis),p2=Vector3.Dot(p.c,axis);
                float q0=Vector3.Dot(q.a,axis),q1=Vector3.Dot(q.b,axis),q2=Vector3.Dot(q.c,axis);
                return Mathf.Max(p0,Mathf.Max(p1,p2))<Mathf.Min(q0,Mathf.Min(q1,q2)) ||
                       Mathf.Max(q0,Mathf.Max(q1,q2))<Mathf.Min(p0,Mathf.Min(p1,p2));
            }
            bool Crossing(Vector3 edge)=>Apart(Vector3.Cross(edge,x)) || Apart(Vector3.Cross(edge,y)) || Apart(Vector3.Cross(edge,z));
            return !(Apart(n)||Apart(m)||Crossing(u)||Crossing(v)||Crossing(w)||
                     Apart(Vector3.Cross(n,u))||Apart(Vector3.Cross(n,v))||Apart(Vector3.Cross(n,w))||
                     Apart(Vector3.Cross(m,x))||Apart(Vector3.Cross(m,y))||Apart(Vector3.Cross(m,z)));
        }

        readonly struct Surface
        {
            public readonly MeshFilter filter;
            public readonly MeshRenderer renderer;
            public Surface(MeshFilter f,MeshRenderer r){filter=f;renderer=r;}
            public bool Drawn=>renderer && renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff;
        }

        readonly List<Surface> obstacles=new(),cargo=new();
        readonly Dictionary<Mesh,TriangleIndex> indices=new();
        readonly HashSet<MeshFilter> reported=new();
        readonly Transform cage;
        public readonly List<string> contacts=new();
        public readonly List<string> unreadable=new();
        public int StructuralMeshes=>obstacles.Count;
        public int CargoMeshes=>cargo.Count;

        public CargoMeshClearance(NereidCargoRail rail)
        {
            cage=rail.cageAnchor;
            var path=new Bounds(rail.Point(0),rail.cageDimensionsMetres);
            foreach(var stop in rail.stops)path.Encapsulate(stop.marker.position);
            path.Expand(rail.cageDimensionsMetres+Vector3.one*4);

            foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                if(!f.sharedMesh || !f.TryGetComponent<MeshRenderer>(out var r) || !r.enabled ||
                   r.gameObject.scene!=rail.gameObject.scene || r.forceRenderingOff)continue;
                if(f.transform.IsChildOf(cage)){
                    if(!f.GetComponentInParent<CatchFishModel>() && !f.GetComponent<TMPro.TMP_Text>())
                        cargo.Add(new Surface(f,r));
                    continue;
                }
                if(!path.Intersects(r.bounds) || (!f.GetComponentInParent<NereidVessel>() && !f.GetComponentInParent<Raid.Factory.FactoryVisuals>()) ||
                   f.GetComponentInParent<NereidReef>() || f.GetComponentInParent<DiverMotor>() || f.GetComponentInParent<FishActor>() ||
                   f.GetComponentInParent<WetCargoShoal>() || f.GetComponent<TMPro.TMP_Text>())continue;
                obstacles.Add(new Surface(f,r));
            }
        }

        TriangleIndex Index(MeshFilter f)
        {
            Mesh mesh=f.sharedMesh;
            if(indices.TryGetValue(mesh,out var index))return index;
            try
            {
                if(mesh.isReadable)index=new TriangleIndex(mesh.vertices,mesh.triangles);
                else index=ReadGeometry(mesh);
            }
            catch(Exception e){unreadable.Add(mesh.name+" / "+Name(f.transform)+": "+e.Message);}
            indices.Add(mesh,index);return index;
        }

        // Keep the normal player's import settings and memory footprint intact. GPU-only art
        // is inspected on demand in the development player and through MeshUtility in Edit Mode.
        static TriangleIndex ReadGeometry(Mesh mesh)
        {
            var faces=new List<int>();
#if UNITY_EDITOR
            using var dataSet=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh);
            var data=dataSet[0];
            using var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
            data.GetVertices(positions);
            for(int i=0;i<data.subMeshCount;i++)
            {
                var part=data.GetSubMesh(i);
                if(part.topology!=MeshTopology.Triangles)continue;
                using var slice=new NativeArray<int>(part.indexCount,Allocator.Temp);data.GetIndices(slice,i,true);
                faces.AddRange(slice.ToArray());
            }
            return new TriangleIndex(positions.ToArray(),faces.ToArray());
#else
            if(mesh.GetVertexAttributeFormat(VertexAttribute.Position)!=VertexAttributeFormat.Float32 ||
               mesh.GetVertexAttributeDimension(VertexAttribute.Position)!=3)
                throw new InvalidOperationException("Mesh position format requires a new probe decoder");
            int stream=mesh.GetVertexAttributeStream(VertexAttribute.Position),stride=mesh.GetVertexBufferStride(stream);
            int first=mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            using var vertices=mesh.GetVertexBuffer(stream);
            using var triangles=mesh.GetIndexBuffer();
            var vp=AsyncGPUReadback.Request(vertices);vp.WaitForCompletion();
            var ip=AsyncGPUReadback.Request(triangles);ip.WaitForCompletion();
            if(vp.hasError || ip.hasError)throw new InvalidOperationException("Graphics readback failed");
            var buffer=vp.GetData<byte>().ToArray();var positions=new Vector3[mesh.vertexCount];
            for(int i=0;i<positions.Length;i++)
            {
                int offset=i*stride+first;
                positions[i]=new Vector3(BitConverter.ToSingle(buffer,offset),BitConverter.ToSingle(buffer,offset+4),BitConverter.ToSingle(buffer,offset+8));
            }
            byte[] raw=ip.GetData<byte>().ToArray();bool wide=mesh.indexFormat==IndexFormat.UInt32;
            for(int i=0;i<mesh.subMeshCount;i++)
            {
                var part=mesh.GetSubMesh(i);
                if(part.topology!=MeshTopology.Triangles)continue;
                for(int j=0;j<part.indexCount;j++)
                {
                    int offset=(part.indexStart+j)*(wide?4:2);
                    faces.Add((wide?BitConverter.ToInt32(raw,offset):BitConverter.ToUInt16(raw,offset))+part.baseVertex);
                }
            }
            return new TriangleIndex(positions,faces.ToArray());
#endif
        }

        static string Name(Transform item)
        {
            string path=item.name;
            while(item.parent && !item.parent.GetComponent<NereidVessel>()){item=item.parent;path=item.name+"/"+path;}
            return path;
        }

        public Bounds VisibleCargoBounds()
        {
            var volume=new Bounds(cage.position,Vector3.zero);
            foreach(var p in cargo)if(p.Drawn)volume.Encapsulate(p.renderer.bounds);
            return volume;
        }

        public static bool OccupiesAperture(NereidCargoRail rail,Bounds volume,float reserve,out float bottomClearance)
        {
            var bounds=rail.leftDoor.GetComponentInChildren<MeshRenderer>().bounds;
            foreach(var door in new[]{rail.leftDoor,rail.rightDoor})
                foreach(var panel in door.GetComponentsInChildren<MeshRenderer>())
                    if(panel.enabled && !panel.forceRenderingOff)bounds.Encapsulate(panel.bounds);
            var origin=rail.hatch.position;
            var size=rail.aperture;
            float openingRight=.5f*rail.hatch.TransformVector(Vector3.right*size.x).magnitude;
            float openingForward=.5f*rail.hatch.TransformVector(Vector3.forward*size.y).magnitude;
            float Extent(Vector3 axis,Vector3 ext)=>Mathf.Abs(axis.x)*ext.x+Mathf.Abs(axis.y)*ext.y+Mathf.Abs(axis.z)*ext.z;
            float Gap(Vector3 axis,float half)=>Mathf.Abs(Vector3.Dot(volume.center-origin,axis))-Extent(axis,volume.extents)-half;
            Vector3 up=rail.hatch.up;
            float doorTop=Vector3.Dot(bounds.center-origin,up)+Extent(up,bounds.extents);
            float doorBottom=Vector3.Dot(bounds.center-origin,up)-Extent(up,bounds.extents);
            float cargoHeight=Vector3.Dot(volume.center-origin,up),cargoRadius=Extent(up,volume.extents);
            bottomClearance=cargoHeight-cargoRadius-doorTop;
            return Gap(rail.hatch.right,openingRight)<reserve && Gap(rail.hatch.forward,openingForward)<reserve
                   && bottomClearance<=reserve && cargoHeight+cargoRadius>=doorBottom-reserve;
        }

        public void Sample(string stage)
        {
            var volume=VisibleCargoBounds();
            foreach(var wall in obstacles)
            {
                if(!wall.Drawn || reported.Contains(wall.filter) || !volume.Intersects(wall.renderer.bounds))continue;
                var target=Index(wall.filter);if(target==null)continue;
                foreach(var load in cargo)
                {
                    if(!load.Drawn || !load.renderer.bounds.Intersects(wall.renderer.bounds))continue;
                    var data=Index(load.filter);if(data==null)continue;
                    var matrix=wall.filter.transform.worldToLocalMatrix*load.filter.transform.localToWorldMatrix;
                    bool collision=false;
                    foreach(var tri in data.faces)if(target.Intersects(target.root,tri.In(matrix))){collision=true;break;}
                    if(!collision)continue;
                    contacts.Add(stage+" | "+Name(wall.filter.transform)+" meets "+load.filter.name);
                    reported.Add(wall.filter);break;
                }
            }
        }
    }
}
#endif
