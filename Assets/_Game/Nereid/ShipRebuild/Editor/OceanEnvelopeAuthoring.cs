using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        const string HullFolder="Assets/_Game/Nereid/ShipRebuild/Prefabs/Hull";
        const string Folder="Assets/_Game/Nereid/ShipRebuild/OceanEnvelope";
        const string Envelope=Folder+"/NereidOceanEnvelope.prefab";
        static Transform body;
        static Material pearl,slate,carbon,teal,titanium,orange,amber,black,visor;
        static readonly List<(OceanEnvelopeMesh mesh,bool collision)> assemblies=new();

        [MenuItem("RAID/020/Install sculpted NEREID exterior")]
        public static void Install()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Envelope);
            if(!asset)
            {
                Ensure(Folder);Ensure(Folder+"/Meshes");Ensure(Folder+"/Materials");
                Palette();
                var sculpt=new GameObject("NereidOceanEnvelope");
                body=sculpt.transform;assemblies.Clear();
                try
                {
                    PressureForm();CommandSaddle();Wheelhouse();Propulsion();NavigationDetails();ServiceHardware();
                    foreach(var part in assemblies)part.mesh.Save(Folder+"/Meshes/"+part.mesh.node.name+".asset",part.collision);
                    asset=PrefabUtility.SaveAsPrefabAsset(sculpt,Envelope);
                }
                finally { UnityEngine.Object.DestroyImmediate(sculpt);assemblies.Clear(); }
            }
            if(!HasFacetedPressureHull(asset))InstallIndustrialPlating();
            string hullPath=HullFolder+"/PressureHull.prefab";
            var template=PrefabUtility.LoadPrefabContents(hullPath);
            try
            {
                if(template.transform.Find("NereidOceanEnvelope"))return;
                // Existing mesh objects retain their IDs and any scene overrides or added children.
                // The replacements cover only the legacy welded exterior and its exterior fittings.
                var old=new HashSet<string>(StringComparer.Ordinal){"Navigation mast and locator","Bilge riser port","Bilge riser starboard",
                    "Twin shrouded azimuth drives","Port glazing returns","Starboard glazing returns","Port machinery saddle",
                    "Starboard machinery saddle","Aft machinery pressure cap"};
                foreach(var r in template.GetComponentsInChildren<MeshRenderer>(true))
                    if(old.Contains(r.name)||r.name.StartsWith("Welded shell bay ",StringComparison.Ordinal))r.enabled=false;
                var child=(GameObject)PrefabUtility.InstantiatePrefab(asset,template.transform);
                child.transform.localPosition=new Vector3(0,0,9);
                PrefabUtility.SaveAsPrefabAsset(template,hullPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(template); }
            AssetDatabase.SaveAssets();
        }

        static void Ensure(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int n=path.LastIndexOf('/');
            Ensure(path.Substring(0,n));AssetDatabase.CreateFolder(path.Substring(0,n),path.Substring(n+1));
        }
        static void Palette(bool update=false)
        {
            Material Finish(string label,Color color,float metal,float gloss,float glow=0)
            {
                string path=Folder+"/Materials/"+label+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m&&!update)return m;
                bool create=!m;
                if(create)m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name=label;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",gloss);
                foreach(string map in new[]{"_BaseMap","_MainTex","_BumpMap","_MetallicGlossMap"})m.SetTexture(map,null);
                m.DisableKeyword("_NORMALMAP");m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.SetColor("_EmissionColor",color.linear*glow);
                if(glow>0){m.EnableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
                else {m.DisableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;}
                if(create)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);
                return m;
            }
            pearl=Finish("Oxidised pressure plate",new(.40f,.46f,.46f),.18f,.25f);
            slate=Finish("Heat-treated hull steel",new(.255f,.325f,.35f),.16f,.23f);
            carbon=Finish("Keel bituminous coat",new(.16f,.205f,.225f),.075f,.20f);
            teal=Finish("Weathered petrol enamel",new(.26f,.33f,.31f),.14f,.25f);
            titanium=Finish("Brushed clamp steel",new(.51f,.53f,.495f),.26f,.30f);
            orange=Finish("Faded safety ochre",new(.55f,.345f,.16f),.12f,.21f);
            black=Finish("Recessed acoustic rubber",new(.11f,.135f,.14f),.02f,.19f);
            amber=Finish("Amber flood lens",new(.98f,.63f,.30f),.10f,.34f,1.50f);
            Finish("Smoked service lens",new(.19f,.28f,.275f),.12f,.33f);
            visor=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Command laminated visor.mat");
            if (visor && !update) return;
            visor=Finish("Command laminated visor",new(.22f,.29f,.28f,.16f),.08f,.50f);
            visor.SetFloat("_Surface",1);visor.SetFloat("_SrcBlend",5);visor.SetFloat("_DstBlend",10);
            visor.SetFloat("_SrcBlendAlpha",1);visor.SetFloat("_DstBlendAlpha",10);visor.SetFloat("_ZWrite",0);
            visor.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");visor.SetOverrideTag("RenderType","Transparent");
            visor.renderQueue=3000;visor.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(visor);
        }
        static OceanEnvelopeMesh Part(string label,Vector3 pivot,bool solid=false)
        {
            var p=new OceanEnvelopeMesh(body,label,pivot);assemblies.Add((p,solid));return p;
        }
        static float Mix(float a,float b,float t)=>Mathf.LerpUnclamped(a,b,t);
        // Shape between authored stations follows straight chines. Openings and bay ends are stations too.
        static float Section(float z,float[] stations,float[] values)
        {
            int i=0;while(i<stations.Length-2&&z>stations[i+1])i++;
            return Mathf.Lerp(values[i],values[i+1],Mathf.InverseLerp(stations[i],stations[i+1],z));
        }
        static IEnumerable<(float start,float end)> Bays(float start,float end,float[] stations)
        {
            foreach(float z in stations)if(z>start&&z<end){yield return (start,z);start=z;}
            yield return (start,end);
        }
        static readonly float[] Z={-39.2f,-37.2f,-34.3f,-30.5f,-26.4f,-22.0f,-17.1f,-13.0f,-7.4f,-1,4.4f,8.05f,10.45f,12.55f,13.10f};
        static readonly float[] Width={.09f,2.10f,3.91f,5.24f,6.13f,6.45f,6.78f,6.92f,7.10f,7.13f,7.02f,6.64f,4.88f,2.18f,.12f};
        static readonly float[] Low={1.72f,.21f,-1.78f,-3.03f,-3.84f,-4.25f,-4.54f,-4.70f,-4.88f,-5.02f,-4.71f,-4.14f,-2.86f,-.68f,1.09f};
        static readonly float[] High={1.91f,2.97f,4.06f,4.72f,4.73f,4.80f,5.12f,5.22f,5.15f,5.20f,5.31f,5.21f,4.69f,3.03f,1.42f};
        static float W(float z)=>Section(z,Z,Width);
        static float Bottom(float z)=>Section(z,Z,Low);
        static float Roof(float z)=>Section(z,Z,High);
        static float Sill(float z)
        {
            float baseY=Mathf.Max(-.31f,Bottom(z)+(Roof(z)-Bottom(z))*.29f);
            return z>=-16.30f&&z<=-11.83f?-.31f:
                Mathf.Lerp(-.31f,baseY,Mathf.Clamp01(Mathf.Max(-16.30f-z,z+11.83f)/1.2f));
        }
        static float Brow(float z)=>Mathf.Lerp(Sill(z),Roof(z),.76f);
        static float SideX(float z,float y)
        {
            float t=Mathf.InverseLerp(Sill(z),Brow(z),y);
            return W(z)*(1+.026f*Mathf.Sin(t*Mathf.PI));
        }
        static Vector3 Side(int side,float z,float y)=>new(side*SideX(z,y),y,z);
        static Vector3 Lower(int side,float z,float t)
        {
            float angle=t*Mathf.PI*.5f;
            return new(side*W(z)*Mathf.Sin(angle),Mathf.Lerp(Sill(z),Bottom(z),Mathf.Cos(angle)),z);
        }
        static Vector3 Upper(int side,float z,float x)
        {
            float norm=Mathf.Clamp01(x/W(z));
            return new(side*x,Mix(Brow(z),Roof(z),Mathf.Sqrt(1-norm*norm)),z);
        }

        static void PressureForm()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var belly=Part(hand+" bathysphere plating",new(side*3,-2,-10),true);
                foreach(var band in new[]{(0f,.48f,carbon),(.48f,.52f,titanium),(.52f,.78f,teal),(.78f,1f,slate)})
                    foreach(var span in Bays(Z[0],Z[Z.Length-1],Z))
                        belly.Surface(band.Item3,1,band.Item1==0?2:1,(u,v)=>Lower(side,Mix(span.start,span.end,u),Mix(band.Item1,band.Item2,v)),
                        (u,v)=>new Vector3(side*.75f,-.7f,0));
                var roof=Part(hand+" dorsal ceramic shield",new(side*3,4.6f,-8),true);
                foreach(var bay in new[]{(Z[0],-30.63f,0f),(-30.63f,-24.74f,4.39f),(-24.74f,-16.24f,3.70f),
                    (-16.24f,-5.55f,0f),(-5.55f,-.45f,2.58f),(-.45f,8.45f,0f),(8.45f,Z[Z.Length-1],0f)})
                {
                    float from=bay.Item1,to=bay.Item2,cut=bay.Item3;
                    foreach(var band in new[]{(0f,.69f,pearl),(.69f,.73f,titanium),(.73f,1f,slate)})
                        foreach(var span in Bays(from,to,Z))roof.Surface(band.Item3,1,band.Item1==.69f?1:2,(u,v)=>{
                            float z=Mix(span.start,span.end,u);return Upper(side,z,Mix(cut,W(z),Mix(band.Item1,band.Item2,v)));
                        },(u,v)=>new Vector3(side*.3f,1,0));
                }
                var walls=Part(hand+" side fairings and window reveals",new(side*6,2,-5),true);
                void Closed(float a,float b,Material m)
                {
                    foreach(var span in Bays(a,b,Z))walls.Surface(m,1,2,(u,v)=>{
                        float z=Mix(span.start,span.end,u);return Side(side,z,Mix(Sill(z),Brow(z),v));
                    },(u,v)=>Vector3.right*side);
                }
                void Window(float a,float b,float bottom,float top,float inset,bool gate=false)
                {
                    // Rounded ends narrow the long observation strip. The boarding collar is a full-width aperture.
                    (float lower,float upper) Edges(float z)
                    {
                        float edge=Mathf.Min(z-a,b-z);
                        float factor=gate?1:Mathf.Clamp01(edge/.59f);
                        float mid=(top+bottom)*.5f;
                        return(Mix(mid,bottom,factor),Mix(mid,top,factor));
                    }
                    int n=Mathf.Max(3,Mathf.CeilToInt((b-a)/.85f));
                    foreach(bool upper in new[]{false,true})
                    {
                        walls.Surface(upper?slate:pearl,n,2,(u,v)=>{
                            float z=Mix(a,b,u);var edge=Edges(z);
                            return Side(side,z,upper?Mix(edge.upper,Brow(z),v):Mix(Sill(z),edge.lower,v));
                        },(u,v)=>Vector3.right*side);
                        if(gate)continue;
                        walls.Surface(black,n,1,(u,v)=>{
                            float z=Mix(a,b,u);var edge=Edges(z);float h=upper?edge.upper:edge.lower;
                            var outside=Side(side,z,h);
                            return new Vector3(Mix(outside.x,side*inset,v),h+(upper?.09f:-.09f)*v,z);
                        },(u,v)=>Vector3.up*(upper?-1:1));
                        walls.Ring(upper?titanium:teal,u=>{float z=Mix(a,b,u);var edge=Edges(z);return Side(side,z,upper?edge.upper:edge.lower);},.078f,n);
                    }
                }
                Closed(Z[0],-21.85f,slate);
                Closed(-21.85f,-20.54f,slate); // The pump room sits behind a continuous pressure plate.
                Closed(-20.54f,-15.81f,slate);
                if(side>0)Window(-15.81f,-12.19f,-.31f,2.43f,5.94f,true);else Closed(-15.81f,-12.19f,slate);
                Closed(-12.19f,-7.95f,slate);
                Window(-7.95f,4.37f,.82f,3.60f,5.97f);
                Closed(4.37f,4.69f,pearl);Window(4.69f,8.04f,.93f,3.59f,5.97f);
                Closed(8.04f,Z[Z.Length-1],slate);
            }
        }
    }
}
