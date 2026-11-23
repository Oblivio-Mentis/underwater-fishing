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
                FitFloorGroup(module.id,module.at,path,finish);
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
            // Each material occupies one flat texel. Flat normals on the actual mesh describe
            // the plane changes. There is no surface grain or view-dependent detail in the palette.
            var chips=new (Material key,string rgb,float metal,float smooth,float glow)[] {
                (dark,"384449",.19f,.25f,0),(hull,"495A5B",.22f,.29f,0),(enamel,"556962",.15f,.27f,0),
                (ceramic,"737E75",.09f,.25f,0),(steel,"818C88",.43f,.34f,0),(ochre,"94774F",.18f,.28f,0),
                (rubber,"353E40",.03f,.21f,0),(deck,"4F5B5C",.24f,.27f,0),(warm,"DFC5A0",.03f,.30f,.67f),
                (cyan,"AAC8C5",.03f,.32f,.50f),(alarm,"B97A61",.05f,.30f,.44f),
                (glass,"536C71",.28f,.47f,0),(water,"656B5A",.06f,.26f,0) };
            const int size=4;
            int cells=size*size;
            var values=new[]{new Color32[cells],new Color32[cells],new Color32[cells]};
            cabinSwatches=new Dictionary<Material,Vector2>();
            for(int i=0;i<cells;i++)
            {
                var chip=chips[Mathf.Min(i,chips.Length-1)];
                if(i<chips.Length)cabinSwatches[chip.key]=new Vector2((i%size+.5f)/size,(i/size+.5f)/size);
                ColorUtility.TryParseHtmlString("#"+chip.rgb,out Color paint);
                values[0][i]=paint;
                values[1][i]=new Color(chip.metal,0,0,chip.smooth);
                values[2][i]=new Color(paint.r*chip.glow,paint.g*chip.glow,paint.b*chip.glow,1);
            }
            var maps=new Texture2D[3];
            string[] names={"Anodized cabin finishes","Metal and smoothness","Luminous ceramics"};
            for(int i=0;i<maps.Length;i++)
            {
                string path=FinishFolder+"/"+names[i]+".png";
                if(!File.Exists(path))
                {
                    var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
                    texture.SetPixels32(values[i]);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.sRGBTexture=i!=1;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.anisoLevel=0;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                }
                maps[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            string materialPath=FinishFolder+"/Cabin inlays.mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Cabin inlays",enableInstancing=true};
            m.SetTexture("_BaseMap",maps[0]);m.SetColor("_BaseColor",Color.white);
            m.SetTexture("_MetallicGlossMap",maps[1]);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",.87f);
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
            Material front,Material rim,float first=0,float last=360,int spans=16)
        {
            spans=Mathf.Min(spans,Mathf.Max(2,Mathf.CeilToInt((last-first)/22.5f)));
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
            Band(s,c,u,v,r,r*.73f,.064f,deck,hull,0,360,10);
            var plate=new Vector3[10];
            for(int i=0;i<plate.Length;i++)
            {
                float a=i*2*Mathf.PI/plate.Length;
                plate[i]=c+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*r*.74f+n*.052f;
            }
            s.Face(fill,plate);
        }
    }
}

