using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Raid.VisualSlice.Editor
{
    public static class VisualKit
    {
        public const string AssetsPath="Assets/_Game/VisualSlice/Generated";
        public static Material Mat(string key)
        {
            string folder=AssetsPath+"/Materials"; System.IO.Directory.CreateDirectory(folder);
            string path=folder+"/"+key+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat!=null)return mat;
            Color color;
            switch(key)
            {
                case "Hull":color=new Color(.19f,.31f,.33f);break;
                case "Navy":color=new Color(.035f,.09f,.115f);break;
                case "Trim":color=new Color(.43f,.54f,.52f);break;
                case "Amber":color=new Color(.9f,.39f,.075f);break;
                case "Light":color=new Color(1,.7f,.3f);break;
                case "Glass":color=new Color(.035f,.38f,.43f);break;
                case "Rust":color=new Color(.27f,.14f,.095f);break;
                case "Rock":color=new Color(.08f,.17f,.20f);break;
                case "Kelp":color=new Color(.12f,.32f,.24f);break;
                case "Creature":color=new Color(.29f,.12f,.19f);break;
                case "CreatureDark":color=new Color(.09f,.045f,.09f);break;
                case "CreatureLight":color=new Color(.61f,.32f,.30f);break;
                case "Glow":color=new Color(.22f,.94f,.85f);break;
                default:color=Color.white;break;
            }
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key};mat.color=color;
            mat.SetFloat("_Smoothness",key=="Glass"?.75f:.27f); mat.SetFloat("_Metallic",key=="Hull"||key=="Trim"?.35f:0);
            if(key=="Light"||key=="Glow") {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*(key=="Light"?2f:1.4f));}
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        public static Transform Group(string name,Transform parent,Vector3 position=default)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        public static GameObject Part(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion rotation=default)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.transform.localRotation=rotation==default?Quaternion.identity:rotation;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        public static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            System.IO.Directory.CreateDirectory(AssetsPath+"/Meshes");
            string path=AssetsPath+"/Meshes/"+name.Replace(" ","_").Replace("/","_")+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null) {AssetDatabase.CreateAsset(mesh,path);old=mesh;} else Object.DestroyImmediate(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=old;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        public static GameObject Tube(string name,Transform parent,Vector3[] points,float[] radii,Material material,int sides=8)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            for(int ring=0;ring<points.Length;ring++)
            {
                Vector3 tangent=(points[Mathf.Min(ring+1,points.Length-1)]-points[Mathf.Max(0,ring-1)]).normalized;
                Vector3 a=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;Vector3 b=Vector3.Cross(tangent,a);
                for(int i=0;i<sides;i++) {float angle=i*2*Mathf.PI/sides;vertices.Add(points[ring]+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radii[ring]);}
            }
            for(int r=0;r<points.Length-1;r++)for(int s=0;s<sides;s++)
            {int a=r*sides+s,b=r*sides+(s+1)%sides,c=a+sides,d=b+sides;indices.AddRange(new[]{a,c,b,b,c,d});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return MeshObject(name,parent,mesh,material);
        }
        public static GameObject Rock(string name,Transform parent,Vector3 position,Vector3 scale,int seed)
        {
            var rng=new System.Random(seed);int sides=7,rings=5;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {float theta=(y+.15f)/(rings-.7f)*Mathf.PI;float phi=x*2*Mathf.PI/sides;float r=.85f+(float)rng.NextDouble()*.3f;vertices.Add(new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi))*r);}
            for(int y=0;y<rings-1;y++)for(int x=0;x<sides;x++){int a=y*sides+x,b=y*sides+(x+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            var flat=new List<Vector3>();for(int i=0;i<triangles.Count;i++)flat.Add(vertices[triangles[i]]);
            var mesh=new Mesh{name="Rock"+seed};mesh.SetVertices(flat);var ids=new int[flat.Count];for(int i=0;i<ids.Length;i++)ids[i]=i;mesh.SetTriangles(ids,0);mesh.RecalculateNormals();
            var go=MeshObject("RockMesh"+seed,parent,mesh,Mat("Rock"));go.name=name;go.transform.localPosition=position;go.transform.localScale=scale;return go;
        }
        public static void Label(string words,Transform parent,Vector3 position,float size,Color color,Quaternion rotation=default)
        {
            var text=Group(words,parent,position).gameObject.AddComponent<TextMesh>();text.text=words;text.fontSize=64;text.characterSize=size;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;text.transform.localRotation=rotation==default?Quaternion.identity:rotation;
        }
        public static Light Lamp(string name,Transform parent,Vector3 position,Color color,float intensity,float range)
        {
            var light=Group(name,parent,position).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;return light;
        }
    }
}
