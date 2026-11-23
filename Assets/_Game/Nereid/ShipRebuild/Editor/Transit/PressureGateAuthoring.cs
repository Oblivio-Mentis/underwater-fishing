using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    internal static class PressureGateAuthoring
    {
        const string Plumbing="Assets/_Game/Nereid/ShipRebuild/Prefabs/Plumbing/";
        internal static void Install(NereidPipeRoute pipe)
        {
            if(pipe.valveFrame)return;
            string path=Plumbing+"OutletIsolationGate.prefab";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset)
            {
                var go=new GameObject("Pressure isolation gate");
                try
                {
                    var mesh=LeafMesh();
                    for(int side=-1;side<=1;side+=2)
                    {
                        var leaf=new GameObject(side<0?"Port vane":"Starboard vane");
                        leaf.transform.SetParent(go.transform,false);
                        if(side<0)leaf.transform.localRotation=Quaternion.Euler(0,0,180);
                        leaf.AddComponent<MeshFilter>().sharedMesh=mesh;leaf.AddComponent<MeshRenderer>().sharedMaterial=pipe.metal;
                        var brace=GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.DestroyImmediate(brace.GetComponent<Collider>());
                        brace.name="Linear gate guide"; brace.transform.SetParent(go.transform,false);
                        brace.transform.localPosition=new Vector3(0,side*1.03f,0);
                        brace.transform.localScale=new Vector3(3.1f,.14f,.26f);
                        brace.GetComponent<Renderer>().sharedMaterial=pipe.enamel;
                    }
                    asset=PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally {Object.DestroyImmediate(go);}
            }
            var frame=((GameObject)PrefabUtility.InstantiatePrefab(asset,pipe.transform)).transform;
            pipe.valveFrame=frame;
            pipe.portLeaf=frame.Find("Port vane");pipe.starboardLeaf=frame.Find("Starboard vane");
            pipe.Rebuild();
            EditorUtility.SetDirty(pipe); PrefabUtility.RecordPrefabInstancePropertyModifications(pipe);
            PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
        }

        static Mesh LeafMesh()
        {
            string path=Plumbing+"Pressure shutter half disk.asset";
            var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(asset)return asset;
            var v=new List<Vector3>();var f=new List<int>();
            const int segments=16;
            for(int face=0;face<2;face++)
            {
                float z=face==0?-.07f:.07f;
                v.Add(Vector3.forward*z);
                for(int i=0;i<=segments;i++)
                {
                    float a=Mathf.PI*i/segments-Mathf.PI*.5f;
                    v.Add(new Vector3(Mathf.Cos(a)*.985f,Mathf.Sin(a)*.985f,z));
                }
            }
            int back=segments+2;
            for(int i=1;i<=segments;i++)
            {
                f.AddRange(new[]{0,i+1,i,back,back+i,back+i+1,i,i+1,back+i,back+i,i+1,back+i+1});
            }
            f.AddRange(new[]{1,back+1,back-1,back+1,2*back-1,back-1});
            var mesh=new Mesh{name="Machined valve half disc"};mesh.SetVertices(v);mesh.SetTriangles(f,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}

