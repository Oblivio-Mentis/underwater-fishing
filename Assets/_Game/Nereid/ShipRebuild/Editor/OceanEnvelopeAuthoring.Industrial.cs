using System;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        const string RefitMarker="Port nacelle split clamps";

        static bool HasFacetedPressureHull(GameObject root)
        {
            var clamp=root.transform.Find(RefitMarker);
            var mesh=clamp?clamp.GetComponent<MeshFilter>()?.sharedMesh:null;
            return mesh&&mesh.name=="Faceted "+RefitMarker;
        }

        // Update the original authored meshes in place; their GUIDs, child transforms, unrelated
        // scene overrides and hand-placed decorations survive. Additional hardware is installed once.
        [MenuItem("RAID/020/Install faceted pressure plating")]
        public static void InstallIndustrialPlating()
        {
            var template=PrefabUtility.LoadPrefabContents(Envelope);
            var generated=new GameObject("Refit source");
            try
            {
                if(HasFacetedPressureHull(template))return;
                Palette(true);body=generated.transform;assemblies.Clear();
                PressureForm();CommandSaddle();Wheelhouse();Propulsion();NavigationDetails();ServiceHardware();
                foreach(var part in assemblies)
                {
                    string name=part.mesh.node.name;
                    part.mesh.Save(Folder+"/Meshes/"+name+".asset",part.collision);
                    var existing=template.transform.Find(name);
                    if(!existing){part.mesh.node.SetParent(template.transform,false);continue;}
                    var before=existing.GetComponent<MeshRenderer>();var after=part.mesh.node.GetComponent<MeshRenderer>();
                    // Keep custom finishes assigned to the original assembly by the artist.
                    var previous=before.sharedMaterials;var next=after.sharedMaterials;
                    for(int i=0;i<Math.Min(previous.Length,next.Length);i++)
                        if(previous[i]&&!AssetDatabase.GetAssetPath(previous[i]).StartsWith(Folder+"/Materials/",StringComparison.Ordinal))next[i]=previous[i];
                    before.sharedMaterials=next;
                    for(int i=part.mesh.node.childCount-1;i>=0;i--)
                    {
                        var light=part.mesh.node.GetChild(i);
                        if(!existing.Find(light.name))light.SetParent(existing,false);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(template,Envelope);AssetDatabase.SaveAssets();
            }
            finally
            {
                assemblies.Clear();UnityEngine.Object.DestroyImmediate(generated);PrefabUtility.UnloadPrefabContents(template);
            }
        }

        static void FlankPlates(OceanEnvelopeMesh p,int side)
        {
            var spans=side<0?new[]{(-34.4f,-29.70f),(-29.55f,-25.74f),(-25.59f,-21.99f),(-20.39f,-16.48f)}
                :new[]{(-34.4f,-29.70f),(-29.55f,-25.74f),(-25.59f,-21.12f),(-20.97f,-16.48f)};
            int count=0;
            foreach(var span in spans)
            {
                float za=span.Item1,zb=span.Item2;
                Vector3 At(float u,float t,float outboard)
                {
                    float tip=Mathf.Max(0,.21f-t)+Mathf.Max(0,t-.79f);
                    float z=Mix(za+tip,zb-tip,u)+.43f*(t-.5f);
                    float h=Mix(.07f+.85f*(1-Mathf.InverseLerp(-34.4f,-16.48f,z)),2.39f+.28f*Mathf.Sin(z*.19f),t);
                    var v=Side(side,z,h);v.x+=side*(.16f+.045f*Mathf.Sin(u*Mathf.PI)+outboard);return v;
                }
                p.Surface(count%3==1?slate:pearl,2,2,(u,v)=>At(u,v,0),(u,v)=>Vector3.right*side);
                foreach(float t in new[]{0f,1f})p.Surface(t==0?carbon:titanium,2,1,(u,v)=>At(u,t,-v*.19f),(u,v)=>Vector3.up*(t==0?-1:1));
                foreach(float u in new[]{0f,1f})p.Surface(black,1,2,(v,t)=>At(u,t,-.19f*v),(v,t)=>Vector3.forward*(u==0?-1:1));
                count++;
            }
        }

        static void FacedPlate(OceanEnvelopeMesh p,Vector3 at,Vector3 normal,Vector3 lengthAxis,
            float length,float width,float rise,Material face)
        {
            Vector3 n=normal.normalized,along=Vector3.ProjectOnPlane(lengthAxis,n).normalized,across=Vector3.Cross(n,along).normalized;
            float h=length*.5f,w=width*.5f,cut=Mathf.Min(h,w)*.26f;
            Vector2[] loop={new(-h+cut,-w),new(h-cut,-w),new(h,-w+cut),new(h,w-cut),new(h-cut,w),new(-h+cut,w),new(-h,w-cut),new(-h,-w+cut)};
            Vector3 L(Vector2 c)=>c.x*along+c.y*across;
            for(int i=0;i<loop.Length;i++)
            {
                Vector3 a=L(loop[i]),b=L(loop[(i+1)%loop.Length]);
                p.Quad(carbon,at+a,at+b,at+b+n*rise,at+a+n*rise,(a+b).normalized);
                p.Quad(face,at+a+n*rise,at+b+n*rise,at+n*rise,at+n*rise,n);
            }

        }

        static void ServiceHardware()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var clamps=Part(hand+" nacelle split clamps",new(side*8,.1f,-31),true);
                foreach(float z in new[]{-35.34f,-30.63f,-25.49f})
                {
                    foreach(var span in new[]{(-.20f,-.15f,black),(-.15f,.11f,titanium),(.11f,.18f,slate)})
                        clamps.Surface(span.Item3,1,12,(u,v)=>Pod(side,z+Mix(span.Item1,span.Item2,u),v*Mathf.PI*2,1.048f),
                            (u,v)=>new Vector3(side*Mathf.Sin(v*Mathf.PI*2),Mathf.Cos(v*Mathf.PI*2),0));
                    foreach(float edge in new[]{-.20f,.18f})clamps.Surface(black,1,12,
                        (u,v)=>Pod(side,z+edge,v*Mathf.PI*2,Mix(.994f,1.048f,u)),(u,v)=>Vector3.forward*Mathf.Sign(edge));
                    foreach(float angle in new[]{.82f,4.54f})
                    {
                        Vector3 n=new Vector3(side*Mathf.Sin(angle),Mathf.Cos(angle),0).normalized;
                        FacedPlate(clamps,Pod(side,z,angle,1.03f),n,Vector3.forward,.78f,.53f,.16f,slate);
                    }
                }
                var casing=Part(hand+" armored pump service covers",new(side*7.7f,.8f,-28),true);
                foreach(float z in new[]{-33.35f,-28.13f})
                {
                    const float theta=1.00f;
                    var n=new Vector3(side*Mathf.Sin(theta),Mathf.Cos(theta),0).normalized;
                    var at=Pod(side,z,theta,1.01f);
                    FacedPlate(casing,at,n,Vector3.forward,2.67f,.87f,.083f,black);
                    FacedPlate(casing,at+n*.075f,n,Vector3.forward,2.56f,.77f,.075f,pearl);
                    foreach(float offset in new[]{-.41f,.41f})FacedPlate(casing,at+n*.15f+Vector3.forward*offset,n,Vector3.forward,.23f,.46f,.032f,carbon);
                }
                var brows=Part(hand+" command shutter reinforcement",new(side*4.35f,7.68f,-27.46f),true);
                foreach(float z in new[]{-29.13f,-26.68f,-24.35f})
                {
                    brows.Surface(titanium,1,4,(u,v)=>Crown(side,z+Mix(-.11f,.11f,u),Mix(.18f,.97f,v))+
                        new Vector3(side*.02f,.034f,0),(u,v)=>new Vector3(side*.6f,.8f,0));
                }
                var lamps=Part(hand+" recessed hull work lamps",new(side*6.73f,2.5f,-3));
                foreach(float z in new[]{-6.15f,1.72f})
                {
                    var at=Side(side,z,3.70f);
                    var n=new Vector3(side*.56f,-.83f,0).normalized;
                    FacedPlate(lamps,at+Vector3.right*side*.10f,n,Vector3.forward,1.01f,.34f,.16f,black);
                    FacedPlate(lamps,at+Vector3.right*side*.10f+n*.15f,n,Vector3.forward,.71f,.17f,.041f,amber);
                    foreach(int i in new[]{-1,1}) FacedPlate(lamps,at+Vector3.right*side*.10f+n*.204f+Vector3.forward*i*.22f,n,Vector3.forward,.034f,.22f,.016f,carbon);
                    var light=new GameObject("Recessed amber wash").AddComponent<Light>();light.transform.SetParent(lamps.node,false);
                    light.transform.localPosition=at+Vector3.right*side*.32f+n*.22f-lamps.node.localPosition;
                    light.transform.localRotation=Quaternion.LookRotation(new Vector3(side*.16f,-1,.26f));
                    light.type=LightType.Spot;light.range=11;light.innerSpotAngle=43;light.spotAngle=75;light.color=new(1,.69f,.41f);light.intensity=88;
                    light.shadows=LightShadows.None;
                }
            }
        }
    }
}
