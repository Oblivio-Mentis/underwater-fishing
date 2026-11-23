using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    // The original deck has a continuous plate collider. Each pressure riser needs a real
    // hole through the bearing surface, with the cable trough on the outboard edge intact.
    internal static class DeckPenetrations
    {
        const float Sleeve=.60f;
        const string Folder="Assets/_Game/Nereid/ShipRebuild/Prefabs/Plumbing/";

        internal static void Install(Transform observation)
        {
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(observation);
            if(string.IsNullOrEmpty(path)) return;
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed=Cut(prefab.transform.Find("Port inspection aisle"),new[]{new Vector2(.76f,-3.57f)});
                changed|=Cut(prefab.transform.Find("Starboard inspection aisle"),new[]{new Vector2(-1.01f,-1),new Vector2(-.76f,3.63f)});
                if(changed) PrefabUtility.SaveAsPrefabAsset(prefab,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }

        static bool Cut(Transform plate,Vector2[] sockets)
        {
            if(!plate || plate.Find("Pressure sleeve lands"))return false;
            var filter=plate.GetComponent<MeshFilter>();
            var original=filter ? filter.sharedMesh:null;
            var box=plate.GetComponentInChildren<BoxCollider>();
            if(!original || !box || !AssetDatabase.GetAssetPath(original).EndsWith("_"+plate.name+".asset",System.StringComparison.Ordinal))
                return false;
            var lands=new GameObject("Pressure sleeve lands").transform;lands.SetParent(plate,false);
            var half=box.size*.5f;
            var centre=plate.InverseTransformPoint(box.transform.TransformPoint(box.center));
            box.enabled=false;
            var vertices=new List<Vector3>(original.vertices);var uv=new List<Vector2>(original.uv);
            var faces=new List<int>[original.subMeshCount];
            for(int i=0;i<faces.Length;i++)faces[i]=i==0?new List<int>():new List<int>(original.GetTriangles(i));
            void Quad(int slot,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                foreach(var p in new[]{a,b,c,d})uv.Add(Mathf.Abs(normal.y)>.5f?new Vector2(p.x,p.z):new Vector2(p.x+p.z,p.y));
                faces[slot].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }

            var xs=new SortedSet<float>{centre.x-half.x,centre.x+half.x};
            var zs=new SortedSet<float>{centre.z-half.z,centre.z+half.z};
            foreach(var hole in sockets)
            {
                xs.Add(hole.x-Sleeve);xs.Add(hole.x+Sleeve);
                zs.Add(hole.y-Sleeve);zs.Add(hole.y+Sleeve);
            }
            var x=new List<float>(xs);var z=new List<float>(zs);
            for(int i=0;i<x.Count-1;i++)
                for(int j=0;j<z.Count-1;j++)
                {
                    float x0=x[i],x1=x[i+1],z0=z[j],z1=z[j+1];
                    var mid=new Vector3((x0+x1)*.5f,centre.y,(z0+z1)*.5f);
                    bool opening=false;
                    foreach(var s in sockets) if(Mathf.Abs(mid.x-s.x)<Sleeve-.001f && Mathf.Abs(mid.z-s.y)<Sleeve-.001f)opening=true;
                    if(opening)continue;
                    var a=new Vector3(x0,centre.y+half.y,z0);var b=new Vector3(x0,a.y,z1);
                    var c=new Vector3(x1,a.y,z1);var d=new Vector3(x1,a.y,z0);
                    var down=Vector3.down*box.size.y;
                    Quad(0,a,b,c,d);Quad(0,a+down,d+down,c+down,b+down);
                    Quad(0,a,a+down,b+down,b);Quad(0,b,b+down,c+down,c);
                    Quad(0,c,c+down,d+down,d);Quad(0,d,d+down,a+down,a);
                    var part=lands.gameObject.AddComponent<BoxCollider>();part.center=mid;part.size=new Vector3(x1-x0,box.size.y,z1-z0);
                }
            foreach(var socket in sockets)
            {
                var at=new Vector3(socket.x,centre.y+half.y+.015f,socket.y);
                const int n=24;
                Vector3 Radial(int i)=>new Vector3(Mathf.Cos(i*Mathf.PI*2/n),0,Mathf.Sin(i*Mathf.PI*2/n));
                for(int i=0;i<n;i++)
                {
                    var u=Radial(i);var v=Radial(i+1);
                    var a=at+u*Sleeve/Mathf.Max(Mathf.Abs(u.x),Mathf.Abs(u.z));var b=at+v*Sleeve/Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.z));
                    var c=at+v*.53f;var d=at+u*.53f;var down=Vector3.down*(box.size.y+.05f);
                    int steel=Mathf.Min(2,faces.Length-1);
                    Quad(steel,b,a,d,c);Quad(steel,c,d,d+down,c+down);Quad(steel,d+down,a+down,b+down,c+down);
                }
            }
            var mesh=new Mesh{name=plate.name+" with sleeved penetrations"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=faces.Length;
            for(int i=0;i<faces.Length;i++)mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string file=Folder+mesh.name+".asset";
            var stored=AssetDatabase.LoadAssetAtPath<Mesh>(file);
            if(!stored){AssetDatabase.CreateAsset(mesh,file);stored=mesh;}else Object.DestroyImmediate(mesh);
            filter.sharedMesh=stored;
            return true;
        }
    }
}

