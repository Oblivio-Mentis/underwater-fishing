using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string CabinFolder=RoomPath+"/Cabin systems";
        const string FinishFolder=Root+"/Materials/Cabin enamel";
        static Dictionary<Material, Vector2> cabinSwatches;

        [MenuItem("NEREID/Fit cabin pressure systems")]
        public static void FitCabinSystems()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before fitting modules.");
            Folder(CabinFolder); Folder(CabinFolder+"/Meshes"); Folder(FinishFolder);
            Palette();var finish=CabinAtlas();
            foreach(var module in modules)
            {
                Action build=module.id switch {
                    "ObservationHall"=>FitObservation,"NavigationBridge"=>FitBridge,
                    "AftTransfer"=>FitTransfer,"BioContainment"=>FitContainment,
                    "CompanionBerth"=>FitBerth,"AirlockCollar"=>FitLock,"EquipmentRoom"=>FitEngineering,
                    _=>null };
                if(build==null)continue;
                string hostPath=RoomPath+"/"+module.id+".prefab";
                string path=CabinFolder+"/"+module.id+" fittings.prefab";
                var kit=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!kit)
                {
                    var assembly=new GameObject(module.id+" fittings");
                    try
                    {
                        parts.Clear();origin=module.at;room=assembly.transform;build();
                        foreach(var part in parts)part.StorePalette(CabinFolder+"/Meshes/"+part.node.name+".asset",finish,cabinSwatches);
                        kit=PrefabUtility.SaveAsPrefabAsset(assembly,path);
                    }
                    finally { parts.Clear();UnityEngine.Object.DestroyImmediate(assembly); }
                }
                var host=PrefabUtility.LoadPrefabContents(hostPath);
                try
                {
                    // The whole kit is a nested prefab. Artist overrides on an individual pressure
                    // rib, lamp or assembly, as well as all other room children, survive later runs.
                    bool installed=false;
                    foreach(Transform child in host.transform)
                        if(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)==kit)installed=true;
                    if(installed)continue;
                    PrefabUtility.InstantiatePrefab(kit,host.transform);
                    PrefabUtility.SaveAsPrefabAsset(host,hostPath);
                }
                finally {PrefabUtility.UnloadPrefabContents(host);}
            }
            AssetDatabase.SaveAssets();Debug.Log("NEREID: fitted seven cabin system modules.");
        }

        static Material CabinAtlas()
        {
            // Colour, metalness, polished finish and emissive inlays occupy corresponding texels.
            // The ordinary URP/Lit maps remain directly paintable and replaceable in Unity.
            var chips=new (Material key, string rgb,float metal,float smooth,float light)[]{
                (dark,"303D49",.35f,.35f,0),(hull,"314C58",.42f,.44f,0),(enamel,"4C7479",.28f,.40f,0),
                (ceramic,"9DAFA8",.22f,.50f,0),(steel,"A4B6B9",.72f,.55f,0),(ochre,"D88837",.37f,.43f,0),
                (rubber,"232A33",.09f,.27f,0),(deck,"505861",.41f,.39f,0),(warm,"FFBE72",.04f,.48f,1),
                (cyan,"80F0EB",.03f,.48f,.90f),(alarm,"FF7052",.06f,.42f,.88f),
                (glass,"375884",.40f,.69f,0),(water,"986B88",.15f,.45f,0) };
            const int tile=128,size=tile*4;
            var buffers=new[]{new Color32[size*size],new Color32[size*size],new Color32[size*size]};
            cabinSwatches=new Dictionary<Material,Vector2>();
            for(int i=0;i<16;i++)
            {
                var c=chips[Mathf.Min(i,chips.Length-1)];
                ColorUtility.TryParseHtmlString("#"+c.rgb,out Color rgb);
                int column=i%4,row=i/4;
                if(i<chips.Length)cabinSwatches.Add(c.key,new Vector2((column+.5f)/4,(row+.5f)/4));
                for(int y=0;y<tile;y++) for(int x=0;x<tile;x++)
                {
                    float u=x/(tile-1f),v=y/(tile-1f);
                    float powder=Mathf.PerlinNoise(i*13.7f+u*4.4f,v*4.4f+1.9f)-.5f;
                    float brushed=Mathf.PerlinNoise(u*(1.4f+c.metal),v*(18+24*c.metal)+i*3.6f)-.5f;
                    float pigment=1+(powder*.13f+brushed*.10f)*(1-c.light);
                    float finish=Mathf.Clamp01(c.smooth+brushed*.21f+powder*.12f);
                    int index=(row*tile+y)*size+column*tile+x;
                    buffers[0][index]=new Color(rgb.r*pigment,rgb.g*pigment,rgb.b*pigment,1);
                    buffers[1][index]=new Color(c.metal,0,0,finish);
                    buffers[2][index]=new Color(rgb.r*c.light,rgb.g*c.light,rgb.b*c.light);
                }
            }
            var maps=new Texture2D[3];
            string[] names={"Anodized cabin finishes","Metal and smoothness","Luminous ceramics"};
            for(int i=0;i<3;i++)
            {
                string path=FinishFolder+"/"+names[i]+".png";
                if(!File.Exists(path))
                {
                    var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
                    texture.SetPixels32(buffers[i]);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.sRGBTexture=i!=1;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                }
                maps[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            string materialPath=FinishFolder+"/Cabin inlays.mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Cabin inlays",enableInstancing=true};
            m.SetTexture("_BaseMap",maps[0]);m.SetColor("_BaseColor",Color.white);
            m.SetTexture("_MetallicGlossMap",maps[1]);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",.87f);
            m.SetTexture("_EmissionMap",maps[2]);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.white*1.50f);
            AssetDatabase.CreateAsset(m,materialPath);return m;
        }

        static Vector2[] Shield(float w,float h,float corner=.22f)
        {
            float x=w*.5f,y=h*.5f,k=Mathf.Min(x,y)*corner;
            return new[]{new Vector2(-x+k,-y),new Vector2(x-k,-y),new Vector2(x,-y+k),new Vector2(x,y-k),
                new Vector2(x-k,y),new Vector2(-x+k,y),new Vector2(-x,y-k),new Vector2(-x,-y+k)};
        }

        static void Casting(ShipSectionMesh s,Vector3 at,Vector3 u,Vector3 v,Vector2[] outline,float depth,Material face,Material bevel,Material side,float taper=.87f)
        {
            Vector3 n=Vector3.Cross(u,v).normalized;
            var bottom=new Vector3[outline.Length];var belt=new Vector3[outline.Length];var top=new Vector3[outline.Length];
            for(int i=0;i<outline.Length;i++)
            {
                Vector3 radial=u*outline[i].x+v*outline[i].y;
                bottom[i]=at+radial;belt[i]=at+radial+n*depth*.39f;top[i]=at+radial*taper+n*depth;
            }
            s.Face(face,top);
            for(int i=0;i<top.Length;i++)
            {
                int j=(i+1)%top.Length;
                s.Face(side,bottom[i],bottom[j],belt[j],belt[i]);
                s.Face(bevel,belt[i],belt[j],top[j],top[i]);
            }
        }

        static void Band(ShipSectionMesh s,Vector3 center,Vector3 u,Vector3 v,float outer,float inner,float height,
            Material front,Material rim,float first=0,float last=360,int spans=32)
        {
            var axis=Vector3.Cross(u,v).normalized;
            for(int i=0;i<spans;i++)
            {
                float a=Mathf.Lerp(first,last,(float)i/spans)*Mathf.Deg2Rad,b=Mathf.Lerp(first,last,(i+1f)/spans)*Mathf.Deg2Rad;
                var da=u*Mathf.Cos(a)+v*Mathf.Sin(a);var db=u*Mathf.Cos(b)+v*Mathf.Sin(b);
                var x=center+da*outer;var y=center+db*outer;var p=center+da*inner;var q=center+db*inner;var lift=axis*height;
                s.Face(front,x+lift,y+lift,q+lift,p+lift);
                s.Face(rim,x,y,y+lift,x+lift);s.Face(rim,q,p,p+lift,q+lift);
                if(i==0)s.Face(rim,p,x,x+lift,p+lift);
                if(i==spans-1)s.Face(rim,y,q,q+lift,y+lift);
            }
        }

        static void Lens(ShipSectionMesh s,Vector3 c,Vector3 u,Vector3 v,float r,Material fill)
        {
            var n=Vector3.Cross(u,v).normalized;
            Band(s,c,u,v,r,r*.64f,.105f,dark,steel,0,360,20);
            Band(s,c+n*.105f,u,v,r*.87f,r*.71f,.025f,steel,deck,0,360,20);
            var plate=new Vector3[20];
            for(int i=0;i<plate.Length;i++)
            {
                float a=i*2*Mathf.PI/plate.Length;
                plate[i]=c+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*r*.67f+n*.077f;
            }
            s.Face(fill,plate);
        }
    }
}

