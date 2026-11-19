using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    /// <summary>Editor-only, idempotent presentation upgrade. Gameplay objects, anchors and route timing are retained.</summary>
    public static class NereidEquipmentAuthoring
    {
        const string ScenePath="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Path="Assets/_Game/Nereid/Generated/Equipment";
        const string RootName="NEREID equipment refinement";
        static Material steel,dark,trim,ochre,cyan,glass,feedGlass,skin;

        public static void Upgrade()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("Equipment upgrade requires stopped InterfaceVerticalSlice");
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            if(factory==null)throw new InvalidOperationException("Install and bind the existing physical factory first");
            Folder(Path);Folder(Path+"/Meshes");Folder(Path+"/Materials");
            Materials();
            var old=factory.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=Group(RootName,factory.transform);
            Tank(root,factory);Pipes(root,factory);Processor(root,factory);Graft(root,factory);
            Combine(root,"Equipment");
            foreach(var station in Object.FindObjectsByType<ShipConsole>(FindObjectsInactive.Include))
                if(station.gameObject.scene.path==ScenePath)Console(station);
            EditorUtility.SetDirty(factory);
            Debug.Log("[NEREID equipment] Cylindrical pressure reservoir, industrial pipe saddles, machines and computer housings upgraded; gameplay anchors retained.");
        }

        static void Materials()
        {
            steel=Mat("Pressure enamel",new Color(.105f,.19f,.205f),.46f,.31f);
            dark=Mat("Gunmetal frames",new Color(.028f,.045f,.051f),.52f,.25f);
            trim=Mat("Worn machined steel",new Color(.31f,.36f,.355f),.7f,.41f);
            ochre=Mat("Muted equipment ochre",new Color(.58f,.35f,.105f),.24f,.28f);
            cyan=Mat("Reservoir light diffuser",new Color(.18f,.56f,.59f),.25f,.37f,1.05f);
            glass=Flow("Pressure glass",new Color(.38f,.59f,.62f,.16f),new Color(.1f,.5f,.57f));
            feedGlass=Flow("Feeding pressure glass",new Color(.48f,.52f,.46f,.16f),new Color(.6f,.41f,.16f));
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Factory/Generated/Materials/Living_mouth_skin.mat");
            string skinPath=Path+"/Materials/Rooted graft tissue.mat";
            skin=AssetDatabase.LoadAssetAtPath<Material>(skinPath);
            if(skin==null){skin=new Material(original!=null?original:steel);skin.name="Rooted graft tissue";AssetDatabase.CreateAsset(skin,skinPath);}
            skin.SetColor("_BaseColor",new Color(.11f,.07f,.115f));EditorUtility.SetDirty(skin);
        }

        static void Tank(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            var legacy=runtime.transform.Find("Large industrial catch reservoir");
            if(legacy!=null)foreach(var renderer in legacy.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            var tank=Group("Pressure reservoir",root,new Vector3(0,0,-3));
            Ring("Foundation foot",tank,1.49f,1.84f,.055f,.28f,dark);
            Ring("Lower pressure flange",tank,1.49f,1.77f,.28f,.46f,steel);
            Ring("Polished lower seat",tank,1.49f,1.64f,.44f,.53f,trim);
            Ring("Water inspection strip",tank,1.535f,1.61f,.52f,.557f,cyan);
            Ring("Upper pressure seat",tank,1.49f,1.66f,2.68f,2.79f,trim);
            Ring("Upper pressure flange",tank,1.49f,1.81f,2.79f,3.04f,steel);
            Ring("Lid insulation lip",tank,1.49f,1.75f,3.04f,3.12f,dark);
            Ring("Upper inspection strip",tank,1.54f,1.61f,2.65f,2.685f,cyan);
            var shell=Lathe(new[]{new Vector2(1.59f,.52f),new Vector2(1.59f,2.68f),new Vector2(1.555f,2.68f),new Vector2(1.555f,.52f),new Vector2(1.59f,.52f)},64);
            var surface=Part("Laminated pressure glass",tank,shell,glass);Persist(surface,"Tank laminated glass");
            for(int i=0;i<6;i++)
            {
                float a=(i+.5f)*Mathf.PI/3;Vector3 p=new Vector3(Mathf.Sin(a)*1.67f,1.59f,Mathf.Cos(a)*1.67f);
                var rib=Box("Tank pressure tie",tank,p,new Vector3(.11f,2.35f,.15f),dark,.025f);rib.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
                foreach(float y in new[]{.37f,2.9f})
                {
                    var foot=Box("Tie flange fastening",tank,new Vector3(p.x,y,p.z),new Vector3(.24f,.18f,.24f),trim,.035f);
                    foot.localRotation=rib.localRotation;
                }
            }
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;
                foreach(float y in new[]{.48f,3.07f})
                    Part("Tank lid hex fastener",tank,Lathe(new[]{new Vector2(0,0),new Vector2(.038f,0),new Vector2(.038f,.04f),new Vector2(0,.04f)},6),trim,new Vector3(Mathf.Sin(a)*1.7f,y,Mathf.Cos(a)*1.7f));
            }
            // Diffusers illuminate actual stock; there are no decorative fish or pretend tank gauges.
            foreach(int side in new[]{-1,1})
            {
                Box("Tank diffuser guard",tank,new Vector3(side*1.45f,1.55f,.15f),new Vector3(.065f,1.92f,.075f),dark,.015f);
                Box("Tank submerged diffuser",tank,new Vector3(side*1.43f,1.55f,.10f),new Vector3(.035f,1.76f,.035f),cyan,.008f);
            }
        }

        static void Pipes(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            foreach(var renderer in runtime.GetComponentsInChildren<Renderer>(true))
                if(renderer.name.EndsWith("bolted pipe sleeves",StringComparison.Ordinal))renderer.enabled=false;
            foreach(var r in runtime.processGlass)if(r!=null){r.sharedMaterial=glass;EditorUtility.SetDirty(r);}
            foreach(var r in runtime.feedGlass)if(r!=null){r.sharedMaterial=feedGlass;EditorUtility.SetDirty(r);}
            PipeHardware(root,runtime.processTube,"Process");PipeHardware(root,runtime.feedTube,"Feeding");
        }
        static void PipeHardware(Transform parent,Vector3[] path,string name)
        {
            if(path==null||path.Length<2)return;
            var root=Group(name+" structural pipe supports",parent);
            // An opaque lower cradle makes the full pressure tube legible without obscuring fish above it.
            Part("Lower pressure cradle",root,Trough(path,.35f),steel);
            float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);
            for(float distance=.15f;distance<length;distance+=1.9f)
            {
                Vector3 tangent;Vector3 p=Along(path,distance,out tangent);
                var cuff=Ring("Split pipe compression collar",root,.34f,.39f,-.035f,.035f,trim);cuff.localPosition=p;cuff.localRotation=Quaternion.FromToRotation(Vector3.up,tangent);
                if(p.y<2.45f)continue;
                Box("Suspension attachment",root,p+new Vector3(.43f,.2f,0),new Vector3(.07f,.38f,.07f),trim,.012f);
                Box("Ceiling standoff",root,new Vector3(p.x+.43f,(p.y+4.05f)*.5f,p.z),new Vector3(.055f,Mathf.Max(.08f,4.05f-p.y),.055f),dark,.008f);
                Box("Hanger shoulder",root,p+new Vector3(.2f,.37f,0),new Vector3(.53f,.07f,.16f),dark,.016f);
                Box("Ceiling anchor plate",root,new Vector3(p.x+.43f,4.03f,p.z),new Vector3(.25f,.045f,.26f),trim,.012f);
            }
        }

        static void Processor(Transform parent,Raid.Factory.FactoryVisuals runtime)
        {
            var root=Group("Deep biomaterial pressure assembly",parent,new Vector3(-3.45f,0,-6.9f));
            // The existing intakes, animation, colliders, material outputs and active lamp are untouched.
            Box("Upper sloping motor cover",root,new Vector3(0,2.27f,-.02f),new Vector3(4.08f,.45f,.88f),steel,.12f);
            Box("Processor chassis cap",root,new Vector3(0,2.51f,-.09f),new Vector3(3.38f,.11f,.68f),dark,.04f);
            foreach(int side in new[]{-1,1})
            {
                Box("Vertical machine corner brace",root,new Vector3(side*1.98f,1.22f,.35f),new Vector3(.13f,1.96f,.18f),trim,.025f);
                Box("Machine equipment skirt",root,new Vector3(side*1.86f,.18f,-.02f),new Vector3(.38f,.28f,.86f),dark,.05f);
                Box("Processor side access panel",root,new Vector3(side*2.075f,1.52f,-.10f),new Vector3(.075f,1.13f,.65f),steel,.025f);
                for(int n=0;n<8;n++)Box("Side cooling louvre",root,new Vector3(side*2.122f,1.27f+n*.08f,-.10f),new Vector3(.025f,.027f,.45f),dark,.005f);
            }
            for(int i=0;i<12;i++)
            {
                float x=-1.67f+i*.3f;
                Box("Pressure panel seam",root,new Vector3(x,1.72f,.54f),new Vector3(.018f,.58f,.018f),dark,.003f);
            }
            for(int lane=0;lane<4;lane++)
            {
                float x=runtime.beltX[lane]+3.45f;
                Box("Intake pressure lip",root,new Vector3(x,1.21f,.61f),new Vector3(.68f,.13f,.26f),steel,.035f);
                foreach(int side in new[]{-1,1})Box("Intake bolted side jamb",root,new Vector3(x+side*.35f,.92f,.58f),new Vector3(.065f,.54f,.22f),trim,.016f);
            }
            // Restrained ochre belongs only to the machine's actual moving entry hazard.
            var workshop=runtime.transform.Find("Aft processing workshop");
            if(workshop!=null)foreach(var renderer in workshop.GetComponentsInChildren<Renderer>(true))
                if(renderer.name=="Conveyor guard rail")renderer.sharedMaterial=ochre;
        }

        static void Graft(Transform parent,Raid.Factory.FactoryVisuals runtime)
        {
            var root=Group("Rooted bow pressure graft",parent,new Vector3(0,1.75f,7.8f));
            // A deep irregular annulus seats the breathing mouth into the bulkhead rather than placing it on it.
            Part("Asymmetric outer muscular mantle",root,Mantle(),skin);
            foreach(int side in new[]{-1,1})
            {
                var brace=Box("Bow restraint stanchion",root,new Vector3(side*2.52f,-.18f,.39f),new Vector3(.28f,2.85f,.54f),dark,.06f);
                brace.localRotation=Quaternion.Euler(0,0,side*7);
                for(int i=0;i<3;i++)Box("Graft restraint saddle",root,new Vector3(side*2.38f,-.8f+i*.78f,.12f),new Vector3(.42f,.18f,.42f),trim,.04f);
            }
        }

        static void Console(ShipConsole station)
        {
            var old=station.transform.Find(RootName);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=Group(RootName,station.transform);
            bool monitor=station.kind==ShipConsoleKind.Upgrades||station.kind==ShipConsoleKind.Depths||station.kind==ShipConsoleKind.Reservoir;
            if(!monitor)
            {
                Box("Sloping selector support",root,new Vector3(0,.68f,.03f),new Vector3(.69f,.8f,.53f),dark,.05f);
                foreach(int side in new[]{-1,1})Box("Selector casing side reinforcement",root,new Vector3(side*.55f,1.22f,.01f),new Vector3(.08f,.66f,.58f),trim,.02f);
                Combine(root,"Console_"+station.name);return;
            }
            if(station.screenAnchor==null)return;
            float ratio=station.screenScale/.0018f;
            float width=2.64f*ratio,height=1.94f*ratio;
            Vector3 centre=station.screenAnchor.localPosition;
            foreach(Transform child in station.transform)
            {
                if(child==root||child==station.screenAnchor)continue;
                if(child.name.Contains("casing")||child.name=="Reservoir inspection screen housing")
                    foreach(var renderer in child.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            }
            Box("Instrument rear pressure body",root,centre+new Vector3(0,0,.23f),new Vector3(width+.32f,height+.32f,.42f),dark,.08f);
            Box("Baked instrument face",root,centre+new Vector3(0,0,.045f),new Vector3(width+.10f,height+.10f,.05f),steel,.025f);
            foreach(int side in new[]{-1,1})
            {
                Box("Vertical instrument bezel",root,centre+new Vector3(side*(width*.5f+.07f),0,-.025f),new Vector3(.14f,height+.27f,.12f),trim,.025f);
                Box("Horizontal instrument bezel",root,centre+new Vector3(0,side*(height*.5f+.07f),-.025f),new Vector3(width+.13f,.14f,.12f),steel,.025f);
            }
            var tray=Group("Sloping input deck",root,centre+new Vector3(0,-height*.5f-.18f,-.15f));tray.localRotation=Quaternion.Euler(20,0,0);
            Box("Keyboard deck casting",tray,Vector3.zero,new Vector3(width*.91f,.12f,.54f),dark,.045f);
            for(int row=0;row<3;row++)for(int column=0;column<11;column++)
                Box("Physical keyboard key",tray,new Vector3((column-5)*width*.063f,.071f,(row-1)*.108f),new Vector3(width*.05f,.035f,.075f),steel,.008f);
            Box("Support spine",root,new Vector3(0,Mathf.Max(.3f,(centre.y-height*.5f)*.5f),centre.z+.21f),new Vector3(width*.5f,Mathf.Max(.3f,centre.y-height*.5f),.34f),dark,.065f);
            foreach(int side in new[]{-1,1})
            {
                var socket=Part("Console lower service fastener",root,Lathe(new[]{new Vector2(0,0),new Vector2(.05f,0),new Vector2(.05f,.045f),new Vector2(0,.045f)},8),trim,centre+new Vector3(side*(width*.5f+.06f),-height*.5f-.05f,-.09f));socket.localRotation=Quaternion.Euler(90,0,0);
            }
            Combine(root,"Console_"+station.name);
        }

        static Transform Group(string name,Transform parent,Vector3 position=default)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static Transform Part(string name,Transform parent,Mesh mesh,Material material,Vector3 position=default)
        {var t=Group(name,parent,position);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;}
        static Transform Box(string name,Transform parent,Vector3 position,Vector3 dimensions,Material material,float bevel)
        {return Part(name,parent,BevelBox(dimensions,bevel),material,position);}
        static Transform Ring(string name,Transform parent,float inner,float outer,float bottom,float top,Material material)
        {
            float b=Mathf.Min(.035f,(top-bottom)*.23f);
            return Part(name,parent,Lathe(new[]{new Vector2(outer-b,bottom),new Vector2(outer,bottom+b),new Vector2(outer,top-b),new Vector2(outer-b,top),new Vector2(inner+b,top),new Vector2(inner,top-b),new Vector2(inner,bottom+b),new Vector2(inner+b,bottom),new Vector2(outer-b,bottom)},48),material);
        }
        static Material Mat(string name,Color color,float metallic,float smoothness,float emission=0)
        {
            string path=Path+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);
            if(emission>0){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*emission);}
            EditorUtility.SetDirty(material);return material;
        }
        static Material Flow(string name,Color color,Color flow)
        {
            string path=Path+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("RAID011/Factory Flow Glass")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetColor("_FlowColor",flow);material.SetFloat("_FlowActive",0);EditorUtility.SetDirty(material);return material;
        }
        static void Folder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int cut=path.LastIndexOf('/');Folder(path.Substring(0,cut));AssetDatabase.CreateFolder(path.Substring(0,cut),path.Substring(cut+1));}
        static void Persist(Transform part,string name)
        {
            var filter=part.GetComponent<MeshFilter>();filter.sharedMesh=MeshAsset(name,filter.sharedMesh);
        }
        static Mesh MeshAsset(string name,Mesh mesh)
        {
            string path=Path+"/Meshes/"+name+".asset";var current=AssetDatabase.LoadAssetAtPath<Mesh>(path);mesh.name=name;
            if(current==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,current);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(current);return current;
        }
        static Mesh Mesh(List<Vector3> points,List<int> indices,List<Vector2> uv=null)
        {
            var mesh=new Mesh();if(points.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(points);mesh.SetTriangles(indices,0);if(uv!=null)mesh.SetUVs(0,uv);mesh.RecalculateNormals();if(uv!=null)mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Lathe(Vector2[] profile,int sides)
        {
            var p=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int band=0;band<profile.Length-1;band++)
            {
                int start=p.Count;
                for(int side=0;side<=sides;side++)
                {
                    float a=side*Mathf.PI*2/sides;
                    for(int edge=0;edge<2;edge++){var point=profile[band+edge];p.Add(new Vector3(Mathf.Cos(a)*point.x,point.y,Mathf.Sin(a)*point.x));uv.Add(new Vector2(side/(float)sides,point.y));}
                }
                for(int i=0;i<sides;i++){int a=start+i*2;t.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});}
            }
            return Mesh(p,t,uv);
        }
        static Mesh BevelBox(Vector3 size,float bevel)
        {
            var p=new List<Vector3>();var t=new List<int>();
            float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,Mathf.Min(y,z))*.65f);
            var rings=new[]{new Vector3(x-b,y-b,-z),new Vector3(x,y,-z+b),new Vector3(x,y,z-b),new Vector3(x-b,y-b,z)};
            for(int band=0;band<rings.Length-1;band++)
            {
                for(int side=0;side<8;side++)
                {
                    int a=p.Count;foreach(int edge in new[]{0,1})foreach(int corner in new[]{side,(side+1)%8})
                    {var r=rings[band+edge];p.Add(Octagon(corner,r.x,r.y,b*.6f,r.z));}
                    t.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});
                }
            }
            for(int face=0;face<2;face++)
            {
                var ring=rings[face==0?0:3];int a=p.Count;p.Add(new Vector3(0,0,ring.z));
                for(int side=0;side<=8;side++)p.Add(Octagon(side%8,ring.x,ring.y,b*.6f,ring.z));
                for(int side=0;side<8;side++)if(face==0)t.AddRange(new[]{a,a+side+2,a+side+1});else t.AddRange(new[]{a,a+side+1,a+side+2});
            }
            return Mesh(p,t);
        }
        static Vector3 Octagon(int i,float x,float y,float bevel,float z)
        {
            switch(i){case 0:return new Vector3(-x+bevel,-y,z);case 1:return new Vector3(x-bevel,-y,z);case 2:return new Vector3(x,-y+bevel,z);case 3:return new Vector3(x,y-bevel,z);case 4:return new Vector3(x-bevel,y,z);case 5:return new Vector3(-x+bevel,y,z);case 6:return new Vector3(-x,y-bevel,z);default:return new Vector3(-x,-y+bevel,z);}
        }
        static Mesh Trough(Vector3[] path,float radius)
        {
            var p=new List<Vector3>();var t=new List<int>();const int sides=8;
            for(int ring=0;ring<path.Length;ring++)
            {
                var tangent=(path[Mathf.Min(ring+1,path.Length-1)]-path[Mathf.Max(0,ring-1)]).normalized;
                var up=Vector3.ProjectOnPlane(Vector3.up,tangent).normalized;if(up.sqrMagnitude<.5f)up=Vector3.forward;
                var right=Vector3.Cross(tangent,up).normalized;
                for(int side=0;side<=sides;side++){float a=Mathf.Lerp(-.57f,.57f,side/(float)sides);p.Add(path[ring]+(-up*Mathf.Cos(a)+right*Mathf.Sin(a))*radius);}
            }
            for(int ring=0;ring<path.Length-1;ring++)for(int side=0;side<sides;side++)
            {int a=ring*(sides+1)+side,b=a+sides+1;t.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            // Separate back vertices preserve valid normals instead of cancelling opposed triangles.
            int count=p.Count;int triangles=t.Count;p.AddRange(p.ToArray());
            for(int i=0;i<triangles;i+=3)t.AddRange(new[]{t[i]+count,t[i+2]+count,t[i+1]+count});
            return Mesh(p,t);
        }
        static Vector3 Along(Vector3[] path,float distance,out Vector3 tangent)
        {
            tangent=Vector3.forward;for(int i=1;i<path.Length;i++){Vector3 d=path[i]-path[i-1];float length=d.magnitude;tangent=d.normalized;if(distance<=length)return Vector3.Lerp(path[i-1],path[i],distance/Mathf.Max(.0001f,length));distance-=length;}return path[path.Length-1];
        }
        static Mesh Mantle()
        {
            var p=new List<Vector3>();var t=new List<int>();const int sides=64,rings=8;
            for(int ring=0;ring<rings;ring++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides,u=ring/(float)(rings-1);float lobe=Mathf.Sin(a*3+.7f)*.1f+Mathf.Sin(a*5-1.1f)*.055f;
                float x=Mathf.Lerp(2.01f,2.82f,u),y=Mathf.Lerp(1.29f,1.85f,u);
                float z=Mathf.Lerp(-.06f,.9f,u)+Mathf.Sin(u*Mathf.PI)*(.28f+lobe)+Mathf.Sin(a*4+u*4)*.045f;
                p.Add(new Vector3(Mathf.Cos(a)*(x+lobe*u),Mathf.Sin(a)*(y+lobe*.65f*u),z));
            }
            for(int ring=0;ring<rings-1;ring++)for(int i=0;i<sides;i++){int a=ring*(sides+1)+i,b=a+sides+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=Mesh(p,t);return mesh;
        }
        static void Combine(Transform root,string name)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<MeshRenderer>()!=null&&!AssetDatabase.Contains(f.sharedMesh)).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var items=group.ToArray();var combines=new CombineInstance[items.Length];
                for(int i=0;i<items.Length;i++)combines[i]=new CombineInstance{mesh=items[i].sharedMesh,transform=root.worldToLocalMatrix*items[i].transform.localToWorldMatrix};
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);
                Part(name+" / "+group.Key.name,root,MeshAsset(name+"_"+group.Key.name,mesh),group.Key);
            }
            foreach(var filter in filters){var mesh=filter.sharedMesh;Object.DestroyImmediate(filter.gameObject);if(!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);}
        }
    }
}
