using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class PipeFabrication
    {
        const string MainScene="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Plumbing="Assets/_Game/Nereid/ShipRebuild/Prefabs/Plumbing/";

        [MenuItem("NEREID/Fit pressure pipe couplings and wall sleeves")]
        public static void FitScene()
        {
            var vessel=Object.FindAnyObjectByType<NereidVessel>();
            if(!vessel || !vessel.authoredRail || Application.isPlaying)
                throw new InvalidOperationException("Open the rebuilt NEREID in Edit Mode.");
            Fit(vessel);
        }

        public static void ApplyToMain()
        {
            if(!Application.isBatchMode)
                throw new InvalidOperationException("Use NEREID/Fit pressure pipe couplings and wall sleeves in the open scene.");
            var scene=EditorSceneManager.OpenScene(MainScene);
            FitScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            ReportScene();
        }

        // Batch entry point for the isolated authoring check. The review scene is deliberately unsaved.
        public static void ProbeMain()
        {
            EditorSceneManager.OpenScene(MainScene);
            FitScene();
            AssetDatabase.SaveAssets();
            ReportScene();
        }

        [MenuItem("NEREID/Report pressure pipe clearance")]
        public static void ReportScene()
        {
            var vessel=Object.FindAnyObjectByType<NereidVessel>();
            if(!vessel)throw new InvalidOperationException("Open the rebuilt NEREID to inspect its pressure pipes.");
            var log=new List<string>();
            foreach(var route in vessel.GetComponentsInChildren<NereidPipeRoute>(true))
            {
                route.Rebuild();Physics.SyncTransforms();
                var wall=route.GetComponents<MeshCollider>().FirstOrDefault(c=>c.sharedMesh);
                var collision=wall?.sharedMesh;
                bool open=wall && ValidateWall(route,wall);
                var start=vessel.transform.InverseTransformPoint(route.WorldPoint(0));
                var end=vessel.transform.InverseTransformPoint(route.WorldPoint(1));
                var issues=TransitClearanceChecks.Pipe(route);
                log.Add($"{route.Circuit} | connected={route.IsConnected && route.source.Fits && route.destination.Fits}" +
                    $" | endpoints {start:F3} to {end:F3} | radius={route.radius:F3} | " +
                    $"open bore and shell collision={open} | {(collision?collision.vertexCount:0)} vertices | " +
                    $"{issues.Count} clearance issues" + (issues.Count>0?": "+string.Join(" | ",issues):string.Empty));
            }
            File.WriteAllLines(Path.Combine(Path.GetTempPath(),"RAID-PipeFit-Final.txt"),log);
        }

        static bool ValidateWall(NereidPipeRoute route,MeshCollider wall)
        {
            var centre=route.WorldPoint(.5f);
            var ahead=(route.WorldPoint(.51f)-route.WorldPoint(.49f)).normalized;
            var side=Vector3.Cross(ahead,Vector3.up).normalized;
            if(side.sqrMagnitude<.5f)side=Vector3.right;
            var scale=route.transform.lossyScale;
            float radius=route.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            var exterior=new Ray(centre+side*radius*1.8f,-side);
            var inside=new Ray(centre,side);
            var axial=new Ray(centre,ahead);
            return wall.Raycast(exterior,out _,radius*1.1f)
                && wall.Raycast(inside,out _,radius*1.09f)
                && !wall.Raycast(axial,out _,radius*.9f);
        }

        internal static void Fit(NereidVessel vessel)
        {
            var issueRoutes=vessel.GetComponentsInChildren<NereidPipeRoute>(true);
            var process=issueRoutes.FirstOrDefault(p=>p.line==PipeLine.Process);
            var service=issueRoutes.FirstOrDefault(p=>p.Circuit==PipeCircuit.GoshaPassage);
            var arch=Find(vessel.transform,"Process hall pressure arch");
            var aisle=Find(vessel.transform,"Starboard inspection aisle");

            if(process && arch)
            {
                var trim=Find(vessel.transform,"Pump room passage")?.Find("Door jambs, sill and drive");
                FitPenetration(vessel.transform,arch,process,0,1,2,.61f,.58f,"Process arch",trim);
            }

            if(service && aisle)
            {
                RerouteService(vessel.transform,service);
                FitPenetration(vessel.transform,aisle,service,0,2,1,.565f,.546f,"Gosha deck");
                FitExteriorMount(vessel.transform,service);
            }

            foreach(var route in issueRoutes)route.Rebuild();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(vessel.gameObject.scene);
        }

        static Transform Find(Transform root,string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child=>child.name==name);

        static bool Crossing(Transform hull,NereidPipeRoute route,int axis,float plane,out Vector3 centre)
        {
            var last=hull.InverseTransformPoint(route.WorldPoint(0));
            const int samples=160;
            for(int i=1;i<=samples;i++)
            {
                var next=hull.InverseTransformPoint(route.WorldPoint((float)i/samples));
                if(Mathf.Abs(next[axis]-last[axis])>.0001f && (next[axis]-plane)*(last[axis]-plane)<=0)
                {
                    centre=Vector3.Lerp(last,next,Mathf.InverseLerp(last[axis],next[axis],plane));
                    centre[axis]=plane;
                    return true;
                }
                last=next;
            }
            centre=default;return false;
        }

        static void RerouteService(Transform hull,NereidPipeRoute route)
        {
            if(!route.source || !route.destination)return;
            // Convert only the original four-knot service route. Repeating the fitter preserves later edits.
            if(route.knots.Count!=4 || (hull.InverseTransformPoint(route.source.transform.position)
                -new Vector3(8.135f,-.755f,-.735f)).sqrMagnitude>.004f)return;

            var original=new[]{new Vector3(7.360f,-.755f,-.735f),new(4.797f,.037f,-1.557f)};
            for(int i=0;i<original.Length;i++)
            {
                var knot=route.knots[i+1];
                var point=hull.InverseTransformPoint(route.transform.TransformPoint(knot.position));
                if(knot.manualTangents || (point-original[i]).sqrMagnitude>.004f)return;
            }

            Vector3 last=hull.InverseTransformPoint(route.destination.transform.position);
            var points=new[]{
                new Vector3(7.68f,-1.66f,-2.77f),new(7.06f,-1.66f,-2.77f),new(6.0f,-1.72f,-2.63f),
                new(4.22f,-1.67f,-2.17f),new(3.22f,-1.31f,-2.00f),new(last.x,last.y-.80f,last.z),last};
            Undo.RecordObjects(new Object[]{route,route.source,route.source.transform},"Fit the Gosha wet passage");
            route.source.transform.position=hull.TransformPoint(points[0]);
            route.source.connectionVolume=new Bounds(
                route.source.fitting.InverseTransformPoint(route.source.transform.position),Vector3.one*.8f);
            route.radius=.54f;
            route.smoothing=.62f;
            route.knots.Clear();
            for(int i=0;i<points.Length;i++)
                route.knots.Add(new PipeKnot{
                    position=route.transform.InverseTransformPoint(hull.TransformPoint(points[i])),
                    section=i==0 || i>=4?PipeSection.Metal:PipeSection.Glass
                });
            PrefabUtility.RecordPrefabInstancePropertyModifications(route);
            PrefabUtility.RecordPrefabInstancePropertyModifications(route.source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(route.source.transform);
            route.Rebuild();
        }

        struct Vertex
        {
            public Vector3 local,hull,normal;
            public Vector2 uv;
            public static Vertex Between(Vertex a,Vertex b,float t)=>new(){
                local=Vector3.LerpUnclamped(a.local,b.local,t),hull=Vector3.LerpUnclamped(a.hull,b.hull,t),
                normal=Vector3.LerpUnclamped(a.normal,b.normal,t).normalized,uv=Vector2.LerpUnclamped(a.uv,b.uv,t)};
        }

        static List<Vertex> Clip(IReadOnlyList<Vertex> polygon,int axis,float limit,bool keepAbove)
        {
            var result=new List<Vertex>(polygon.Count+1);
            if(polygon.Count==0)return result;
            var a=polygon[^1];
            float da=(a.hull[axis]-limit)*(keepAbove?1:-1);
            foreach(var b in polygon)
            {
                float db=(b.hull[axis]-limit)*(keepAbove?1:-1);
                if((da>=0)!=(db>=0))result.Add(Vertex.Between(a,b,da/(da-db)));
                if(db>=0)result.Add(b);
                a=b;da=db;
            }
            return result;
        }

        static void FitPenetration(Transform hull,Transform wall,NereidPipeRoute route,
            int u,int v,int axis,float halfU,float halfV,string label,Transform trim=null)
        {
            var backing=wall.Find("Inclined collision");
            if(!backing || !backing.TryGetComponent<BoxCollider>(out var plate))return;
            float plane=hull.InverseTransformPoint(backing.TransformPoint(plate.center))[axis];
            var thickness=Vector3.zero;
            thickness[axis]=plate.size[axis];
            float depth=Mathf.Abs(hull.InverseTransformVector(backing.TransformVector(thickness))[axis]);
            if(depth>.01f && Crossing(hull,route,axis,plane,out var centre))
                FitSleeve(hull,wall,route,centre,u,v,axis,halfU,halfV,depth,label,trim);
        }

        static void FitSleeve(Transform hull,Transform wall,NereidPipeRoute route,Vector3 at,
            int u,int v,int axis,float halfU,float halfV,float depth,string label,Transform trim)
        {
            float sleeveDepth=depth;
            if(trim && trim.TryGetComponent<MeshFilter>(out var fascia) && fascia.sharedMesh)
            {
                string asset=label+" opened facing.asset";
                if(AssetDatabase.GetAssetPath(fascia.sharedMesh)!=Plumbing+asset)
                {
                    var border=DrillMesh(fascia.sharedMesh,trim,hull,at,u,v,axis,halfU,halfV,.53f);
                    if(border)
                    {
                        Undo.RecordObject(fascia,"Open the facing around the pressure sleeve");
                        fascia.sharedMesh=Store(border,asset);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(fascia);
                    }
                }
                sleeveDepth=Mathf.Max(depth,.43f);
            }

            string tag=label+" · pressure penetration";
            if(wall.Find(tag))return;
            var filter=wall.GetComponent<MeshFilter>();
            if(!filter || !filter.sharedMesh)return;

            var owner=new GameObject(tag).transform;
            Undo.RegisterCreatedObjectUndo(owner.gameObject,"Fit pressure passage");
            owner.SetParent(wall,false);
            var original=filter.sharedMesh;
            var mesh=DrillMesh(original,wall,hull,at,u,v,axis,halfU,halfV,depth);
            if(!mesh){Object.DestroyImmediate(owner.gameObject);return;}
            Undo.RecordObject(filter,"Open pressure penetration");
            filter.sharedMesh=Store(mesh,label+" opened wall.asset");
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);

            var affected=wall.GetComponentsInChildren<BoxCollider>(true)
                .Where(box=>box.enabled && !box.transform.IsChildOf(owner)).ToArray();
            foreach(var box in affected)SplitCollider(hull,owner,box,at,u,v,axis,halfU,halfV,depth);
            var slope=Vector3.zero;
            if(Crossing(hull,route,axis,at[axis]-.035f,out var before) &&
               Crossing(hull,route,axis,at[axis]+.035f,out var after))
                slope=(after-before)/.07f;
            var sleeve=SleeveMesh(hull,owner,at,slope,route.radius,u,v,axis,halfU,halfV,sleeveDepth);
            owner.gameObject.AddComponent<MeshFilter>().sharedMesh=Store(sleeve,label+" sleeve flange.asset");
            owner.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{route.metal,route.enamel};
            var shell=owner.gameObject.AddComponent<MeshCollider>();
            shell.sharedMesh=owner.GetComponent<MeshFilter>().sharedMesh;
        }

        // Clip only triangles within the thickness of this particular plate. Surrounding trim is preserved.
        static Mesh DrillMesh(Mesh original,Transform wall,Transform hull,Vector3 at,
            int u,int v,int axis,float halfU,float halfV,float depth)
        {
            var all=original.vertices;var normals=original.normals;var uvs=original.uv;
            var source=new Vertex[all.Length];
            for(int i=0;i<all.Length;i++)
                source[i]=new Vertex{local=all[i],hull=hull.InverseTransformPoint(wall.TransformPoint(all[i])),
                    normal=normals.Length==all.Length?normals[i]:Vector3.up,uv=uvs.Length==all.Length?uvs[i]:Vector2.zero};
            var verts=new List<Vector3>();var vertexNormals=new List<Vector3>();var tex=new List<Vector2>();
            var slots=new List<int>[original.subMeshCount];bool crossed=false;
            float minU=at[u]-halfU,maxU=at[u]+halfU,minV=at[v]-halfV,maxV=at[v]+halfV;
            for(int m=0;m<slots.Length;m++)
            {
                var indices=slots[m]=new List<int>();var faces=original.GetTriangles(m);
                for(int i=0;i<faces.Length;i+=3)
                {
                    var face=new[]{source[faces[i]],source[faces[i+1]],source[faces[i+2]]};
                    if(face.All(p=>p.hull[u]<=minU) || face.All(p=>p.hull[u]>=maxU) ||
                        face.All(p=>p.hull[v]<=minV) || face.All(p=>p.hull[v]>=maxV) ||
                        face.All(p=>p.hull[axis]<at[axis]-depth*.5f-.09f) ||
                        face.All(p=>p.hull[axis]>at[axis]+depth*.5f+.09f))
                    {Append(face,indices);continue;}
                    crossed=true;
                    Append(Clip(face,u,minU,false),indices);
                    var middle=Clip(face,u,minU,true);
                    Append(Clip(middle,u,maxU,true),indices);
                    middle=Clip(middle,u,maxU,false);
                    Append(Clip(middle,v,minV,false),indices);
                    Append(Clip(middle,v,maxV,true),indices);
                }
            }
            if(!crossed)return null;
            var result=new Mesh{name=wall.name+" · opened pressure bore",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            result.SetVertices(verts);result.SetNormals(vertexNormals);result.SetUVs(0,tex);result.subMeshCount=slots.Length;
            for(int m=0;m<slots.Length;m++)result.SetTriangles(slots[m],m);
            result.RecalculateBounds();result.RecalculateTangents();
            return result;

            void Append(IReadOnlyList<Vertex> polygon,List<int> indices)
            {
                if(polygon.Count<3)return;
                var first=polygon[0];
                for(int i=1;i+1<polygon.Count;i++)
                {
                    var b=polygon[i];var c=polygon[i+1];
                    if(Vector3.Cross(b.local-first.local,c.local-first.local).sqrMagnitude<1e-12f)continue;
                    foreach(var p in new[]{first,b,c})
                    {indices.Add(verts.Count);verts.Add(p.local);vertexNormals.Add(p.normal);tex.Add(p.uv);}
                }
            }
        }

        static void SplitCollider(Transform hull,Transform owner,BoxCollider old,Vector3 at,
            int u,int v,int axis,float halfU,float halfV,float depth)
        {
            var half=old.size*.5f;
            var lo=Vector3.one*float.PositiveInfinity;
            var hi=Vector3.one*float.NegativeInfinity;
            for(int corner=0;corner<8;corner++)
            {
                var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                var point=old.center+Vector3.Scale(half,sign);
                point=hull.InverseTransformPoint(old.transform.TransformPoint(point));
                lo=Vector3.Min(lo,point);
                hi=Vector3.Max(hi,point);
            }
            if(at[axis]<lo[axis]-.03f || at[axis]>hi[axis]+.03f ||
                hi[axis]-lo[axis]>depth+.16f)return;
            float u0=Mathf.Max(lo[u],at[u]-halfU),u1=Mathf.Min(hi[u],at[u]+halfU),
                v0=Mathf.Max(lo[v],at[v]-halfV),v1=Mathf.Min(hi[v],at[v]+halfV);
            if(u0>=u1 || v0>=v1)return;
            Undo.RecordObject(old,"Replace bearing plate around pressure bore");
            old.enabled=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(old);
            Add(lo[u],u0,lo[v],hi[v]);Add(u1,hi[u],lo[v],hi[v]);
            Add(u0,u1,lo[v],v0);Add(u0,u1,v1,hi[v]);

            void Add(float a,float b,float c,float d)
            {
                if(b-a<.003f || d-c<.003f)return;
                var mid=(lo+hi)*.5f;mid[u]=(a+b)*.5f;mid[v]=(c+d)*.5f;
                var size=hi-lo;size[u]=b-a;size[v]=d-c;
                var solid=owner.gameObject.AddComponent<BoxCollider>();
                solid.center=owner.InverseTransformPoint(hull.TransformPoint(mid));
                solid.size=new Vector3(
                    owner.InverseTransformVector(hull.TransformVector(Vector3.right*size.x)).magnitude,
                    owner.InverseTransformVector(hull.TransformVector(Vector3.up*size.y)).magnitude,
                    owner.InverseTransformVector(hull.TransformVector(Vector3.forward*size.z)).magnitude);
            }
        }

        static Mesh SleeveMesh(Transform hull,Transform owner,Vector3 at,Vector3 slope,float radius,
            int u,int v,int axis,float halfU,float halfV,float depth)
        {
            const int n=24;
            var verts=new List<Vector3>();
            var metal=new List<int>();var paint=new List<int>();
            // The hole has square corners. The face covers them and tapers into an open circular bore.
            foreach(var ring in new[]{(phase:-1f,face:true),(phase:-1f,face:false),(phase:1f,face:false),(phase:1f,face:true)})
                for(int i=0;i<n;i++)
                {
                    float angle=i*Mathf.PI*2/n;
                    float x=Mathf.Cos(angle),y=Mathf.Sin(angle);
                    var point=at;
                    float outer=1/Mathf.Max(Mathf.Abs(x),Mathf.Abs(y));
                    float offset=ring.phase*(depth*.5f+.033f);
                    var tangent=slope.normalized;
                    float projection=x*tangent[u]+y*tangent[v];
                    float bore=radius*(axis==1?1.023f:1.12f)/Mathf.Sqrt(Mathf.Max(.4f,1-projection*projection));
                    float rim=axis==1?.012f:.052f;
                    point[u]+=ring.face?x*(halfU+rim)*outer:x*bore+slope[u]*offset;
                    point[v]+=ring.face?y*(halfV+rim)*outer:y*bore+slope[v]*offset;
                    point[axis]+=offset;
                    verts.Add(owner.InverseTransformPoint(hull.TransformPoint(point)));
                }
            for(int row=0;row<4;row++)
                for(int i=0;i<n;i++)
                {
                    var j=(i+1)%n;var next=(row+1)%4;
                    int a=row*n+i,b=row*n+j,c=next*n+j,d=next*n+i;
                    var indices=row==1?paint:metal;
                    // Axis order differs between a hull wall and a horizontal deck plate.
                    if(axis!=1)indices.AddRange(new[]{a,c,b,a,d,c});
                    else indices.AddRange(new[]{a,b,c,a,c,d});
                }
            var mesh=new Mesh{name=owner.name};
            mesh.SetVertices(verts);mesh.subMeshCount=2;
            mesh.SetTriangles(metal,0);mesh.SetTriangles(paint,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        static void FitExteriorMount(Transform hull,NereidPipeRoute route)
        {
            const string housing="Hull-mounted Gosha service casing";
            const string yoke="Service mouth yoke";
            if(!route.source)return;
            var previous=route.transform.Find(housing);
            if(previous && previous.Find(yoke))return;
            if(previous)Undo.DestroyObjectImmediate(previous.gameObject);
            var shell=new GameObject(housing).transform;
            Undo.RegisterCreatedObjectUndo(shell.gameObject,"Fit exterior service housing");
            shell.SetParent(route.transform,false);
            var point=hull.InverseTransformPoint(route.source.transform.position);
            var throat=point-Vector3.right*.41f;
            float hullScale=hull.TransformVector(Vector3.right).magnitude;
            float bore=route.radius*route.transform.TransformVector(Vector3.one.normalized).magnitude/hullScale;
            var frame=new GameObject(yoke).transform;
            frame.SetParent(shell,false);
            const int faces=4;

            for(int segment=0;segment<faces;segment++)
                Box("Service collar saddle",Seat(segment),Seat(segment+1),.29f,.105f,route.metal,frame,.035f);
            for(int side=-1;side<=1;side+=2)
            {
                var at=Seat(side<0?0:faces)+Vector3.up*.054f;
                var sill=new Vector3(Mathf.Min(point.x-.53f,7.035f),-.27f,at.z);
                Box(side<0?"Aft sill bracket":"Forward sill bracket",at,sill,.19f,.16f,route.metal,shell,.07f);
                Box("Sill bearing plate",sill-Vector3.right*.19f,sill+Vector3.right*.19f,.37f,.105f,route.enamel,shell,.10f);
                foreach(float z in new[]{-.112f,.112f})
                    Part(PrimitiveType.Cylinder,"Bearing plate bolt",sill+new Vector3(.105f,-.067f,z),
                        hull.rotation,new Vector3(.082f,.035f,.082f),route.metal,shell);
            }

            Vector3 Seat(int side)
            {
                float angle=Mathf.Lerp(-47f,47f,(float)side/faces)*Mathf.Deg2Rad;
                return throat+(bore+.055f)*(Vector3.up*Mathf.Cos(angle)+Vector3.forward*Mathf.Sin(angle));
            }

            void Box(string name,Vector3 a,Vector3 b,float width,float height,Material material,Transform parent,float overlap) =>
                Part(PrimitiveType.Cube,name,(a+b)*.5f,Quaternion.LookRotation(hull.TransformVector(b-a),hull.up),
                    new Vector3(width,height,Vector3.Distance(a,b)+overlap),material,parent);

            void Part(PrimitiveType primitive,string name,Vector3 centre,Quaternion rotation,Vector3 size,Material material,Transform parent)
            {
                var part=GameObject.CreatePrimitive(primitive);
                if(primitive!=PrimitiveType.Cube)Object.DestroyImmediate(part.GetComponent<Collider>());
                part.name=name;
                var basis=part.transform;
                basis.SetParent(parent,false);
                basis.SetPositionAndRotation(hull.TransformPoint(centre),rotation);
                var local=basis.localRotation;
                basis.localScale=new Vector3(
                    size.x*hullScale/parent.TransformVector(local*Vector3.right).magnitude,
                    size.y*hullScale/parent.TransformVector(local*Vector3.up).magnitude,
                    size.z*hullScale/parent.TransformVector(local*Vector3.forward).magnitude);
                part.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }

        static Mesh Store(Mesh mesh,string name)
        {
            string path=Plumbing+name;
            var known=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!known){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,known);
            EditorUtility.SetDirty(known);
            Object.DestroyImmediate(mesh);
            return known;
        }
    }
}
