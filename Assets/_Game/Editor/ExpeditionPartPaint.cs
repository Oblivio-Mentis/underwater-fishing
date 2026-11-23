using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Raid.Editor
{
    // Read the source atlas once on import. A connected piece of equipment gets one paint,
    // including vertices Unity split along a UV seam. Rig weights and source UVs stay intact.
    sealed class ExpeditionPartPaint : AssetPostprocessor
    {
        const string Art = "Assets/_Game/Factory/";
        const string Diver = Art+"Diver/ExpeditionDiver.fbx";
        const string Sleeves = Art+"Diver/ExpeditionSleeves.fbx";
        const string Harpoon = Art+"HarpoonArt/ExpeditionHarpoon.fbx";
        const int Paints = 7;

        void OnPostprocessModel(GameObject root)
        {
            if (assetPath != Diver && assetPath != Sleeves && assetPath != Harpoon) return;
            bool weapon = assetPath == Harpoon;
            string path = Art+(weapon ? "HarpoonArt/ExpeditionHarpoon_Albedo.png" : "Diver/ExpeditionDiver_BaseColor.jpg");
            context.DependsOnSourceAsset(path);
            var source = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!ImageConversion.LoadImage(source,File.ReadAllBytes(path))) throw new InvalidDataException(path);
                var pixels=source.GetPixels32();
                var meshes=root.GetComponentsInChildren<MeshFilter>(true).Select(r=>r.sharedMesh)
                    .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh));
                foreach(var mesh in meshes.Where(m=>m).Distinct()) Paint(mesh,pixels,source.width,source.height,weapon);
                if(assetPath==Diver)
                    foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.forceMeshLod=3;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        static int Swatch(Color32 p)
        {
            int value=(p.r*3+p.g*6+p.b)/10;
            if(p.r>p.b*1.38f+12 && p.r>p.g*1.12f) return value>85 ? 4 : 5;
            if(p.b>p.r*1.24f+12 && value>62) return 6;
            return value<40 ? 0 : value<80 ? 1 : value<135 ? 2 : 3;
        }

        static void Paint(Mesh mesh,Color32[] atlas,int width,int height,bool weapon)
        {
            var points=mesh.vertices;
            var uv=mesh.uv;
            var triangles=mesh.triangles;
            if(uv.Length!=points.Length || points.Length==0) throw new InvalidDataException("Missing UVs on "+mesh.name);
            var parents=Enumerable.Range(0,points.Length).ToArray();
            int Group(int i)
            {
                while(parents[i]!=i){ parents[i]=parents[parents[i]]; i=parents[i]; }
                return i;
            }
            // The source harpoon is already split into individual manufactured parts.
            if(!weapon)
            {
                var welded=new Dictionary<Vector3Int,int>();
                for(int i=0;i<points.Length;i++)
                {
                    var key=Vector3Int.RoundToInt(points[i]*100000f);
                    if(welded.TryGetValue(key,out int other)) parents[i]=other;
                    else welded.Add(key,i);
                }
                for(int k=0;k<triangles.Length;k+=3)
                {
                    int a=Group(triangles[k]);
                    parents[Group(triangles[k+1])]=a;
                    parents[Group(triangles[k+2])]=a;
                }
            }
            var weights=new Dictionary<int,float[]>();
            for(int k=0;k<triangles.Length;k+=3)
            {
                int a=triangles[k],b=triangles[k+1],c=triangles[k+2],key=weapon?0:Group(a);
                if(!weights.TryGetValue(key,out var votes)) weights.Add(key,votes=new float[Paints]);
                Vector2 sample=(uv[a]+uv[b]+uv[c])/3f;
                int x=Mathf.FloorToInt(Mathf.Repeat(sample.x,1)*width);
                int y=Mathf.FloorToInt(Mathf.Repeat(sample.y,1)*height);
                float area=Vector3.Cross(points[b]-points[a],points[c]-points[a]).magnitude;
                votes[Swatch(atlas[y*width+x])]+=area;
            }
            var paints=new Dictionary<int,Color32>();
            foreach(var pair in weights)
            {
                int index=Array.IndexOf(pair.Value,pair.Value.Max());
                if(weapon && mesh.name.Contains("LineStatusLens"))index=4;
                paints.Add(pair.Key,new Color32((byte)(index*32),0,0,255));
            }
            var colors=new Color32[points.Length];
            for(int i=0;i<colors.Length;i++) paints.TryGetValue(weapon?0:Group(i),out colors[i]);
            mesh.colors32=colors;
        }

        [MenuItem("RAID/Art/Refresh expedition part paint")]
        public static void Refresh()
        {
            foreach(var path in new[]{Diver,Sleeves,Harpoon}) AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var former=Selection.activeObject;
            try
            {
                Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Harpoon);
                ExpeditionHarpoonMeshBaker.Bake();
            }
            finally { Selection.activeObject=former; }
        }
    }
}

