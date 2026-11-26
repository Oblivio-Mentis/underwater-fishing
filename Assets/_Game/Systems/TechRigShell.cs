using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    // Fabricated equipment is drawn as three reusable meshes per model (housing, drive and lamp).
    // Profile rings keep rolled edges, flanges and hose fittings visible at swimming distance.
    public sealed partial class TechRigShell : MonoBehaviour
    {
        sealed class Press
        {
            public readonly List<Vector3> verts=new();
            public readonly List<int>[] faces={new(),new(),new(),new(),new(),new()};
            public void Tube(Vector3 a,Vector3 b,float ra,float rb,int material,int sides=12,bool caps=true)
            {
                var axis=(b-a).normalized;
                if(axis.sqrMagnitude<.1f)return;
                var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.8f?Vector3.forward:Vector3.up).normalized;
                var v=Vector3.Cross(axis,u);
                int first=verts.Count;
                for(int j=0;j<2;j++)for(int i=0;i<sides;i++)
                {
                    float theta=i*Mathf.PI*2/sides;
                    verts.Add((j==0?a:b)+(j==0?ra:rb)*(u*Mathf.Cos(theta)+v*Mathf.Sin(theta)));
                }
                var t=faces[material];
                for(int i=0;i<sides;i++)
                {
                    int n=(i+1)%sides, k=first+i, l=first+n;
                    t.Add(k);t.Add(l);t.Add(k+sides);t.Add(l);t.Add(l+sides);t.Add(k+sides);
                    if(caps && i>=1 && i<sides-1){t.Add(first);t.Add(l);t.Add(k);t.Add(first+sides);t.Add(k+sides);t.Add(l+sides);}
                }
            }
            public void Rod(Vector3 a,Vector3 b,float r,int m)=>Tube(a,b,r,r,m,8);
            public void Axis(float y,float z,float start,float end,float r,int m)=>
                Tube(new(start,y,z),new(end,y,z),r,r,m,20);
            public void Ring(Vector3 center,float rx,float rz,float wire,int mat,int sides=18)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=2*Mathf.PI*i/sides,b=2*Mathf.PI*(i+1)/sides;
                    Rod(center+new Vector3(rx*Mathf.Cos(a),0,rz*Mathf.Sin(a)),center+new Vector3(rx*Mathf.Cos(b),0,rz*Mathf.Sin(b)),wire,mat);
                }
            }
            public void Box(Vector3 c,Vector3 size,int mat)
            {
                // A pressed eight-sided outline with rounded-off corners.
                float x=size.x*.5f,z=size.z*.5f,h=size.y*.5f,k=Mathf.Min(x,z)*.32f;
                Vector3[] rim={new(-x+k,0,-z),new(-x,0,-z+k),new(-x,0,z-k),new(-x+k,0,z),
                    new(x-k,0,z),new(x,0,z-k),new(x,0,-z+k),new(x-k,0,-z)};
                var f=faces[mat];
                for(int i=0;i<8;i++)
                {
                    int a=verts.Count;
                    Vector3 p=c+rim[i],q=c+rim[(i+1)%8],lift=Vector3.up*h;
                    verts.Add(p-lift);verts.Add(q-lift);verts.Add(q+lift);verts.Add(p+lift);
                    f.Add(a);f.Add(a+1);f.Add(a+2);f.Add(a);f.Add(a+2);f.Add(a+3);
                    a=verts.Count;
                    verts.Add(c+lift);verts.Add(p+lift);verts.Add(q+lift);
                    verts.Add(c-lift);verts.Add(q-lift);verts.Add(p-lift);
                    for(int n=0;n<6;n++)f.Add(a+n);
                }
            }
            public Mesh Bake(string label)
            {
                var m=new Mesh{name=label};m.SetVertices(verts);m.subMeshCount=faces.Length;
                for(int i=0;i<faces.Length;i++)m.SetTriangles(faces[i],i,false);
                m.RecalculateNormals();
                var normals=m.normals;
                var uv=new Vector2[verts.Count];
                for(int i=0;i<uv.Length;i++)
                {
                    var n=normals[i];var v=verts[i];
                    uv[i]=(Mathf.Abs(n.y)>.65f?new Vector2(v.x,v.z):
                        Mathf.Abs(n.x)>.65f?new Vector2(v.z,v.y):new Vector2(v.x,v.y))*2.5f;
                }
                m.uv=uv;m.RecalculateBounds();return m;
            }
        }

        static readonly Dictionary<string,Mesh[]> library=new();
        static Material[] palette;
        Transform drive,lamp,occupant;
        MeshFilter captiveMesh;
        string lastMode,model;
        AudioSource sound;
        static AudioClip click,chirp;
        MeshRenderer beacon;
        MaterialPropertyBlock beaconInk;
        int signal=-1;
        static Texture2D PaintGrain()
        {
            const int side=128;
            var texture=new Texture2D(side,side,TextureFormat.RGB24,true){name="NEREID · enamel and brush marks",wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color32[side*side];
            var grain=new System.Random(237);
            for(int y=0;y<side;y++)
            {
                int brushing=grain.Next(0,8);
                for(int x=0;x<side;x++)
                {
                    byte tone=(byte)(221+brushing+grain.Next(0,22));
                    pixels[y*side+x]=new Color32(tone,tone,tone,255);
                }
            }
            for(int i=0;i<55;i++)
            {
                int x=grain.Next(side),y=grain.Next(side),length=grain.Next(2,19);
                for(int j=0;j<length;j++)pixels[y*side+(x+j)%side]=new Color32(174,180,179,255);
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }
        static readonly Color[] colors={new(.075f,.14f,.16f),new(.29f,.42f,.44f),new(.75f,.39f,.085f),
            new(.16f,.46f,.43f),new(.09f,.88f,.83f),new(.82f,.14f,.075f)};
        static void Materials()
        {
            if(palette!=null && palette[0])return;
            palette=new Material[colors.Length];
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var grain=PaintGrain();
            for(int i=0;i<palette.Length;i++)
            {
                var m=new Material(shader){name="NEREID / "+i,color=colors[i]};m.SetFloat("_Metallic",i==4?.12f:.58f);
                m.SetFloat("_Smoothness",i==1?.40f:.26f);
                if(i!=4)m.mainTexture=grain;
                if(i==4){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",colors[i]*1.7f);}
                palette[i]=m;
            }
        }
        static void Skids(Press p,float x,float z)
        {
            for(int i=-1;i<=1;i+=2)
            {
                var a=new Vector3(i*x,-.28f,-z);var b=new Vector3(i*x,-.28f,z);
                p.Rod(a,b,.046f,1);p.Rod(a,a+new Vector3(0,.09f,-.12f),.046f,1);
                p.Rod(b,b+new Vector3(0,.09f,.12f),.046f,1);
                foreach(float f in new[]{-.6f,.6f})p.Rod(new(i*x,-.28f,f*z),new(i*x*.73f,.015f,f*z),.04f,2);
            }
        }
        static void Drum(Press p,float y,float z,float length,float r)
        {
            p.Axis(y,z,-length,length,r,3);
            foreach(int s in new[]{-1,1})
            {
                p.Axis(y,z,s*length-.025f,s*length+.025f,r*1.13f,1);
                p.Axis(y,z,s*length-.05f,s*length+.05f,r*.43f,2);
            }
            for(int i=-3;i<=3;i++)p.Axis(y,z,i*length*.24f-.013f,i*length*.24f+.013f,r*1.03f,0);
        }
        static void Enclosure(Press p,float x,float z,float h,int n)
        {
            foreach(int k in new[]{-1,1})
            {
                for(int i=0;i<=n;i++)
                {
                    float xx=Mathf.Lerp(-x,x,i/(float)n);
                    p.Rod(new(xx,-h,k*z),new(xx,h,k*z),i==0||i==n?.038f:.013f,1);
                    float zz=Mathf.Lerp(-z,z,i/(float)n);p.Rod(new(k*x,-h,zz),new(k*x,h,zz),.014f,1);
                }
                foreach(float y in new[]{-h,0f,h})
                {
                    p.Rod(new(-x,y,k*z),new(x,y,k*z),.028f,y==h?2:0);
                    p.Rod(new(k*x,y,-z),new(k*x,y,z),.028f,y==h?2:0);
                }
                p.Rod(new(-x,-h,k*z),new(x,h,k*z),.024f,3);
            }
            p.Box(new(0,-h,0),new(2*x,.065f,2*z),0);
        }
        static Mesh[] Design(string kind)
        {
            var p=new Press();var d=new Press();var l=new Press();
            switch(kind)
            {
                case "cage":
                    Enclosure(p,.72f,.65f,.48f,8);
                    for(int i=-4;i<=4;i++)d.Rod(new(i*.16f,0,0),new(i*.16f,0,1.3f),.022f,1);
                    d.Rod(new(-.72f,0,1.3f),new(.72f,0,1.3f),.045f,2);
                    for(int i=-1;i<=1;i+=2){p.Rod(new(i*.72f,.48f,-.65f),new(i*.5f,.93f,-.46f),.045f,2);}
                    p.Rod(new(-.5f,.93f,-.46f),new(.5f,.93f,-.46f),.043f,1);break;
                case "trap":case "basket":case "farm":
                    float x=kind=="farm"?.64f:.38f;
                    Enclosure(p,x,x*.85f,x*.64f,kind=="basket"?5:8);
                    p.Ring(new(0,x*.7f,0),x*.65f,x*.55f,.03f,2);
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;p.Rod(new(x*.65f*Mathf.Cos(a),x*.7f,x*.55f*Mathf.Sin(a)),new(x*.24f*Mathf.Cos(a),.01f,x*.21f*Mathf.Sin(a)),.013f,1);}
                    if(kind=="farm"){Drum(p,.12f,-.62f,.32f,.12f);p.Ring(Vector3.up*.55f,.6f,.52f,.045f,3);}
                    break;
                case "charge":
                    for(int i=0;i<3;i++)
                    {float a=i*Mathf.PI*2/3;var c=new Vector3(.055f*Mathf.Cos(a),.055f*Mathf.Sin(a),0);
                     p.Tube(c+Vector3.back*.18f,c+Vector3.forward*.15f,.064f,.064f,5);}
                    foreach(float z in new[]{-.1f,.095f})p.Rod(new(0,0,z-.018f),new(0,0,z+.018f),.116f,0);
                    p.Box(new(0,.10f,0),new(.095f,.052f,.20f),2);break;
                case "net":case "winch":
                    Skids(p,.35f,.38f);Drum(d,.04f,0,.32f,.2f);
                    foreach(int i in new[]{-1,1}){p.Box(new(i*.32f,-.06f,0),new(.06f,.4f,.48f),2);}
                    if(kind=="net")Enclosure(p,.34f,.22f,.14f,10);
                    else p.Rod(new(-.36f,.05f,0),new(-.42f,.28f,.1f),.036f,1);
                    break;
                case "oxygen":case "lure":case "bubbles":
                    Skids(p,.27f,.30f);
                    if(kind=="oxygen")foreach(float xOff in new[]{-.14f,.14f})
                    {
                        p.Tube(new(xOff,-.2f,0),new(xOff,.4f,0),.125f,.125f,3);
                        p.Tube(new(xOff,.4f,0),new(xOff,.48f,0),.125f,.055f,1);
                        p.Ring(new(xOff,.52f,0),.096f,.096f,.016f,2);
                    }
                    else if(kind=="lure")
                    {
                        p.Box(new(0,-.13f,0),new(.42f,.22f,.45f),0);
                        p.Rod(new(0,-.04f,0),new(0,.18f,0),.055f,1);
                        for(int s=-1;s<=1;s+=2)
                        {
                            d.Tube(new(s*.03f,.20f,0),new(s*.32f,.32f,.06f),.075f,.175f,2);
                            d.Tube(new(s*.32f,.32f,.06f),new(s*.34f,.325f,.064f),.146f,.146f,0);
                            d.Rod(new(s*.32f,.32f,.06f),new(s*.36f,.33f,.064f),.045f,3);
                        }
                    }
                    else
                    {
                        p.Ring(new(0,-.12f,0),.36f,.32f,.05f,3);
                        for(int i=0;i<4;i++)
                        {
                            float a=i*Mathf.PI*.5f;
                            var at=new Vector3(Mathf.Sin(a)*.28f,-.12f,Mathf.Cos(a)*.25f);
                            p.Tube(at,at+Vector3.up*.12f,.081f,.064f,1);
                            p.Ring(at+Vector3.up*.13f,.062f,.062f,.012f,4,10);
                        }
                        p.Tube(new(0,-.19f,0),new(0,.18f,0),.12f,.09f,2);
                        d.Ring(Vector3.up*.21f,.13f,.13f,.019f,1);
                    }
                    break;
                case "robot":
                    Skids(p,.41f,.49f);p.Box(Vector3.zero,new(.67f,.4f,.80f),2);
                    p.Box(new(0,.06f,.41f),new(.51f,.23f,.1f),0);
                    p.Tube(new(0,.06f,.44f),new(0,.06f,.51f),.091f,.078f,1);
                    p.Tube(new(0,.06f,.51f),new(0,.06f,.52f),.055f,.055f,4);
                    p.Ring(new(0,.23f,-.12f),.20f,.16f,.025f,0);
                    for(int s=-1;s<=1;s+=2){p.Tube(new(s*.43f,0,-.28f),new(s*.43f,0,.26f),.16f,.14f,3);
                        p.Rod(new(s*.20f,.10f,.42f),new(s*.20f,.10f,.48f),.038f,4);
                        p.Rod(new(s*.29f,.22f,-.31f),new(s*.29f,.22f,.26f),.018f,1);
                        p.Rod(new(s*.22f,-.14f,.32f),new(s*.36f,-.25f,.66f),.035f,1);
                        d.Rod(new(s*.36f,-.25f,.66f),new(s*.24f,-.18f,.80f),.035f,2);}
                    break;
                case "magnet":
                    Skids(p,.24f,.25f);Drum(p,.02f,-.08f,.22f,.19f);
                    foreach(int s in new[]{-1,1}){p.Box(new(s*.17f,0,.19f),new(.13f,.14f,.5f),5);p.Box(new(s*.17f,0,.44f),new(.13f,.14f,.09f),1);}
                    break;
                case "vacuum":
                    VacuumHousing(p);
                    break;
                case "mail":
                    Skids(p,.32f,.39f);Drum(p,.04f,-.12f,.24f,.27f);
                    p.Tube(new(0,.02f,.04f),new(0,.12f,.55f),.12f,.16f,3);
                    for(int i=0;i<6;i++){float t=i/6f;var c=new Vector3(0,.02f+.1f*t,.1f+.45f*t);p.Rod(c,c+Vector3.forward*.017f,.14f+.035f*t,1);}
                    p.Box(new(0,.32f,-.11f),new(.30f,.18f,.37f),2);
                    break;
                default:p.Box(Vector3.zero,new(.36f,.25f,.48f),3);break;
            }
            l.Box(Vector3.zero,new(.095f,.039f,.065f),4);
            var animal=new Press();
            if(kind=="cage")
            {
                Vector3[] spine={new(-.46f,-.08f,.16f),new(-.31f,-.10f,.05f),new(-.08f,-.01f,-.045f),
                    new(.14f,.025f,-.06f),new(.38f,.015f,-.12f),new(.47f,-.04f,-.12f)};
                float[] radii={.03f,.06f,.12f,.19f,.15f,.035f};
                for(int i=1;i<spine.Length;i++)animal.Tube(spine[i-1],spine[i],radii[i-1],radii[i],0);
                foreach(float side in new[]{-1f,1f})
                {
                    animal.Tube(spine[1],spine[0]+new Vector3(-.07f,side*.14f,.03f),.056f,.009f,3,5);
                    animal.Tube(spine[2],spine[2]+new Vector3(-.12f,-.10f,side*.22f),.065f,.008f,1,5);
                    animal.Rod(spine[4]+new Vector3(0,.045f,side*.125f),spine[4]+new Vector3(.01f,.048f,side*.14f),.023f,4);
                }
                animal.Rod(spine[4],spine[4]+Vector3.up*.20f,.012f,3);
                animal.Rod(spine[4]+Vector3.up*.20f,spine[4]+new Vector3(.10f,.18f,0),.012f,4);
            }
            var main=p.Bake(kind+" корпус");var motor=d.Bake(kind+" привод");var light=l.Bake("Маяк");
            if(kind!="cage")return new[]{main,motor,light};
            var eel=new Press();
            var tail=new Vector3(-.55f,0,.18f);
            for(int i=1;i<=14;i++)
            {
                float f=i/14f;
                var tip=new Vector3(-.55f+f*1.02f,.03f+.06f*Mathf.Sin(7*f),.20f*Mathf.Cos(6*f));
                eel.Tube(tail,tip,.025f+.058f*(i-1)/14f,.025f+.058f*f,3);
                eel.Rod(tail+Vector3.up*.04f,tip+Vector3.up*.065f,.014f,1);
                tail=tip;
            }
            eel.Tube(tail,tail+Vector3.right*.13f,.1f,.021f,0);
            foreach(int side in new[]{-1,1})eel.Rod(tail+new Vector3(.05f,.04f,side*.055f),tail+new Vector3(.058f,.04f,side*.072f),.014f,4);
            return new[]{main,motor,light,animal.Bake("Рифовый удильщик"),eel.Bake("Сетевой угорь")};
        }
        public void Build(string kind)
        {
            model=kind;Materials();
            if(!library.TryGetValue(kind,out var meshes) || !meshes[0])library[kind]=meshes=Design(kind);
            for(int i=0;i<(kind=="cage"?4:3);i++)
            {
                var child=new GameObject(new[]{"Корпус","Привод","Маяк","Хищник"}[i]);child.transform.SetParent(transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=meshes[i];child.AddComponent<MeshRenderer>().sharedMaterials=palette;
                if(i==1)drive=child.transform;
                if(i==2){lamp=child.transform;beacon=child.GetComponent<MeshRenderer>();beaconInk=new MaterialPropertyBlock();}
                if(i==3){occupant=child.transform;captiveMesh=child.GetComponent<MeshFilter>();occupant.gameObject.SetActive(false);}
            }
            if(kind=="cage")drive.localPosition=new Vector3(0,.48f,-.65f);
            lamp.localPosition=BeaconOrigin(kind);
            sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=1;sound.minDistance=2;sound.maxDistance=18;sound.playOnAwake=false;
            if(!click)click=Tone("Защёлка",160,.08f);if(!chirp)chirp=Tone("Сигнал",740,.15f);
        }
        static AudioClip Tone(string name,float hz,float seconds)
        {
            if(Application.isBatchMode || AudioSettings.outputSampleRate<=0)return null;
            const int rate=16000;var samples=new float[(int)(seconds*rate)];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=.42f*Mathf.Exp(-6*t/seconds)*Mathf.Sin(2*Mathf.PI*hz*t*(1-.3f*t/seconds));}
            var c=AudioClip.Create(name,samples.Length,1,rate,false);c.SetData(samples,0);return c;
        }
        public void Display(TechRig rig,bool snap)
        {
            if(model!="charge" || rig.mode!="fuse")
            {
                transform.position=snap?rig.position:Vector3.Lerp(transform.position,rig.position,1-Mathf.Exp(-Time.deltaTime*14));
                transform.rotation=Quaternion.Euler(0,rig.yaw,model=="cage" && rig.contents.Count>0?Mathf.Sin(Time.time*7)*2:0);
            }
            if(rig.mode!=lastMode && lastMode!=null)
            {
                var cue=rig.mode is "armed" or "captured" or "fuse"?chirp:click;
                if(cue)sound.PlayOneShot(cue);
            }
            lastMode=rig.mode;
            bool busy=rig.mode is not ("placed" or "carried" or "damaged");
            int light=rig.health<.36f?3:rig.mode is "armed" or "closing" or "fuse"?2:rig.contents.Count>0?1:0;
            if(light!=signal)
            {
                signal=light;
                Color c=light==3?new Color(1,.10f,.025f):light==2?new Color(1,.55f,.035f):light==1?new Color(.38f,1,.40f):colors[4];
                beaconInk.SetColor("_BaseColor",c);beaconInk.SetColor("_EmissionColor",c*1.8f);
                beacon.SetPropertyBlock(beaconInk);
            }
            if(model=="cage")
            {
                float angle=rig.contents.Count>0 || rig.mode is "closed" or "damaged"?0:-72;
                var pose=Quaternion.Euler(angle,0,0);
                if(rig.health<.36f)pose*=Quaternion.Euler(0,0,12);
                drive.localRotation=snap?pose:Quaternion.RotateTowards(drive.localRotation,pose,Time.deltaTime*220);
                occupant.gameObject.SetActive(rig.contents.Count>0);
                if(rig.contents.Count>0)captiveMesh.sharedMesh=library[model][rig.contents[0].species=="NetEel"?4:3];
                occupant.localRotation=Quaternion.Euler(0,Mathf.Sin(Time.time*4)*14,0);
            }
            else if(busy)drive.Rotate(Vector3.right,Time.deltaTime*100,Space.Self);
            if(model=="vacuum")DisplayVacuumSchool(rig);
            lamp.gameObject.SetActive(snap || (!busy && light<3) || Mathf.Sin(Time.time*(light==3?9:rig.mode=="fuse"?17:4)+rig.serial)>-.5f);
        }

        static Vector3 BeaconOrigin(string kind) => kind switch
        {
            "cage"=>new(.6f,.53f,-.62f),
            "charge"=>new(0,.135f,0),
            "vacuum"=>VacuumPoint(new(.48f,.92f,0)),
            _=>new(0,.29f,0)
        };

        public void FollowCharge(Vector3 point,Vector3 velocity,bool snap)
        {
            transform.position=snap?point:Vector3.Lerp(transform.position,point,1-Mathf.Exp(-Time.deltaTime*26));
            float speed=velocity.magnitude;
            if(speed>.15f)transform.Rotate(38f*speed*Time.deltaTime,13f*speed*Time.deltaTime,7f*speed*Time.deltaTime,Space.Self);
        }
    }
}
