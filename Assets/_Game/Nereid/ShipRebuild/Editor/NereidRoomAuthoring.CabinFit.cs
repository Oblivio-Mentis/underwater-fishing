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
                (dark,"353B3E",.37f,.27f,0),(hull,"465252",.35f,.32f,0),(enamel,"4D5A56",.28f,.34f,0),
                (ceramic,"6A7068",.25f,.29f,0),(steel,"767C79",.68f,.46f,0),(ochre,"806548",.33f,.34f,0),
                (rubber,"35373A",.04f,.22f,0),(deck,"4C524F",.49f,.34f,0),(warm,"E4C69C",.04f,.38f,.72f),
                (cyan,"A5C4BF",.05f,.42f,.48f),(alarm,"AE6144",.06f,.43f,.46f),
                (glass,"435758",.41f,.63f,0),(water,"53574C",.06f,.28f,0) };
            const int tile=128,size=tile*4;
            var buffers=new[]{new Color32[size*size],new Color32[size*size],new Color32[size*size],new Color32[size*size]};
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
                    // Short scuffs and displaced paint collect on pressed lips. On large panels variation stays broad.
                    float scratch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.74f,.90f,
                        Mathf.PerlinNoise(u*3.1f+i*13.5f,v*61.0f+i*3.4f)));
                    float crust=Mathf.PerlinNoise(i*3.71f+u*21f,v*16f);
                    float bare=(i==4 || i==5)?scratch*.42f:0;
                    float pitting=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,.84f,crust))*(1-c.light);
                    float pigment=1+(powder*.40f+brushed*.28f)*(1-c.light)-pitting*.13f;
                    Color paint=Color.Lerp(rgb,new Color(.53f,.54f,.49f),bare);
                    float finish=Mathf.Clamp01(c.smooth+brushed*.28f+powder*.22f-pitting*.20f+bare*.22f);
                    int index=(row*tile+y)*size+column*tile+x;
                    buffers[0][index]=new Color(paint.r*pigment,paint.g*pigment,paint.b*pigment,1);
                    buffers[1][index]=new Color(c.metal,0,0,finish);
                    buffers[2][index]=new Color(rgb.r*c.light,rgb.g*c.light,rgb.b*c.light);
                    if(i>=13)InstrumentFinish(i,u,v,index,buffers);
                    const float step=1f/(tile-1);
                    float relief=FinishRelief(i,u,v);
                    Vector3 normal=new Vector3((relief-FinishRelief(i,u+step,v))*1.85f,
                        (relief-FinishRelief(i,u,v+step))*1.85f,1f).normalized;
                    buffers[3][index]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f);
                }
            }
            var maps=new Texture2D[4];
            string[] names={"Anodized cabin finishes","Metal and smoothness","Luminous ceramics","Brushed and pitted relief"};
            for(int i=0;i<maps.Length;i++)
            {
                string path=FinishFolder+"/"+names[i]+".png";
                if(!File.Exists(path))
                {
                    var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
                    texture.SetPixels32(buffers[i]);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    if(i==3)importer.textureType=TextureImporterType.NormalMap;
                    importer.sRGBTexture=i==0 || i==2;importer.textureCompression=TextureImporterCompression.Uncompressed;
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
            m.SetTexture("_BumpMap",maps[3]);m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.32f);
            m.SetTexture("_EmissionMap",maps[2]);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.white*.86f);
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
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

