using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed class NereidFauna : MonoBehaviour
    {
        static readonly Color Armour=new(.19f,.25f,.229f),Soft=new(.152f,.079f,.119f),Pale=new(.66f,.66f,.46f),Dark=new(.061f,.104f,.137f);
        readonly List<Object> owned=new();
        Material skin,luminous,mud;
        Transform clamShell,eel,kraken,warning,lure,shadow;
        Transform anglerJaw,anglerTail,anglerFinLeft,anglerFinRight,krakenMantle,eelPhotophores;
        Material huntingLights;
        FlexibleBody eelBody,strikingArm;
        Transform[] lureFish;
        float lureDisplay;
        ParticleSystem silt;
        byte previousKraken;
        bool first;

        public void Build(Transform angler)
        {
            skin=Material("NereidReef","Reef fauna");luminous=Material("NereidReef","Photophores");
            skin.SetFloat("_Hide",1);luminous.SetFloat("_Glow",1.35f);
            mud=Material("NereidSilt","Silt bloom");
            MakeClam();MakeEel();MakeKraken();MakeAngler(angler);
            first=true;
        }

        Material Material(string shader,string name)
        {
            var material=new Material(Resources.Load<Shader>(shader)){name=name};owned.Add(material);return material;
        }

        Transform Node(Transform parent,string name,Vector3 offset)
        {
            var node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=offset;return node;
        }
        Transform Part(Transform parent,string name,ReefGeometry geometry,Material material)
        {
            var piece=Node(parent,name,Vector3.zero);
            var mesh=geometry.Mesh(name);owned.Add(mesh);
            piece.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            return piece;
        }

        void MakeClam()
        {
            var clam=Node(transform,"Донный капкан",ThreatDirector.ClamBed);
            clam.rotation=Quaternion.Euler(0,-28,0);
            for(int half=0;half<2;half++)
            {
                var shell=new ReefGeometry();
                float sign=half==0?-1:1;
                const int rays=19,rings=5;
                for(int ring=0;ring<rings;ring++)for(int i=0;i<rays;i++)
                {
                    float r=ring/(rings-1f);
                    float angle=Mathf.Lerp(-1.37f,1.37f,i/(rays-1f));
                    float fluting=i%2==0?.064f:-.026f;
                    var point=new Vector3(Mathf.Sin(angle)*r*1.38f,sign*r*(.2f+Mathf.Sin(r*Mathf.PI)*.41f+fluting),Mathf.Cos(angle)*r*1.8f);
                    var colour=Color.Lerp(Soft,(i%2==0?Armour:Pale)*.85f,r);
                    int v=shell.Add(point,colour);
                    if(i>0&&ring>0){shell.Quad(v-rays-1,v-1,v,v-rays);}
                }
                for(int i=-3;i<=3;i++)
                {
                    float angle=i*.32f;
                    var edge=new Vector3(Mathf.Sin(angle)*1.32f,.2f*sign,Mathf.Cos(angle)*1.76f);
                    shell.Tube(new[]{edge,edge+new Vector3(0,-sign*.17f,-.02f)},new[]{.085f,.009f},Pale,5);
                }
                var piece=Part(clam,half==0?"Lower shell":"Hinged shell",shell,skin);
                if(half==1)clamShell=piece;
            }
        }

        void MakeEel()
        {
            eel=Node(transform,"Сетевик",Vector3.zero);
            eelBody=new FlexibleBody(this,eel,"Ribbon body",22,.29f,Dark,skin);
            var face=new ReefGeometry();
            face.Tube(new[]{new Vector3(0,0,-.15f),new(0,.02f,.14f),new(0,-.035f,.59f)},new[]{.27f,.33f,.09f},Dark,11,.8f);
            for(int s=-1;s<=1;s+=2)
            {
                face.Tube(new[]{new Vector3(s*.19f,-.02f,.22f),new(s*.16f,-.15f,.53f),new(s*.09f,-.12f,.72f)},new[]{.104f,.068f,.018f},Pale,7);
                face.Tube(new[]{new Vector3(s*.2f,.06f,.3f),new(s*.24f,.06f,.41f)},new[]{.076f,.038f},new Color(.81f,.43f,.12f),7);
            }
            Part(eel,"Scraping jaws",face,skin);
            var eyes = new ReefGeometry();
            for (int i = -1; i <= 1; i += 2)
                for (int j = 0; j < 3; j++)
                {
                    var point = new Vector3(i * (.17f + j * .03f),.11f,.38f - j * .17f);
                    eyes.Tube(new[]{point,point + Vector3.up * .028f},new[]{.035f - j * .006f,.006f},new Color(.9f,.37f,.1f),6);
                }
            eelPhotophores = Part(eel,"Hunting photophores",eyes,luminous);
        }

        void MakeKraken()
        {
            kraken=Node(transform,"Кракен",ThreatDirector.Rift);
            var body=new ReefGeometry();
            body.Tube(new[]{new Vector3(0,-1.5f,1.1f),new(.1f,.4f,.6f),new(.1f,2.65f,-.6f),new(0,3.35f,-1.1f)},
                new[]{1.15f,2.28f,1.18f,.08f},new Color(.148f,.105f,.18f),14,.89f);
            for(int i=0;i<7;i++)
            {
                float a=i*2*Mathf.PI/7;
                Vector3 d=new(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(d,Vector3.up);
                body.Tube(new[]{d*1.3f,d*2.8f+Vector3.down*.76f,d*4.4f+side*.8f+Vector3.down*1.5f,d*5.7f+side*.93f+Vector3.down*.9f,d*6.3f+side*.48f+Vector3.down*.45f},
                    new[]{.64f,.49f,.31f,.16f,.018f},new Color(.16f,.116f,.21f),10,.85f);
            }
            krakenMantle = Node(kraken,"Kraken mantle",Vector3.zero);
            Part(krakenMantle,"Mantle over the rift",body,skin);
            var eyes=new ReefGeometry();
            for(int s=-1;s<=1;s+=2)
                eyes.Tube(new[]{new Vector3(s*1.1f,.3f,-1.14f),new(s*1.33f,.39f,-1.42f),new(s*1.52f,.46f,-1.5f)},new[]{.04f,.19f,.03f},new Color(.86f,.28f,.055f),9,.5f);
            Part(krakenMantle,"Rift eyes",eyes,luminous);
            strikingArm=new FlexibleBody(this,kraken,"Sweeping arm",25,.71f,new Color(.21f,.15f,.24f),skin);

            warning=Node(kraken,"Rising silt",Vector3.zero);
            silt=warning.gameObject.AddComponent<ParticleSystem>();
            silt.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=silt.main;main.playOnAwake=false;main.loop=true;main.startColor=new ParticleSystem.MinMaxGradient(new Color(.27f,.31f,.23f,.66f),new Color(.49f,.45f,.31f,.39f));
            main.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.8f);main.startSpeed=new ParticleSystem.MinMaxCurve(.45f,1.4f);main.startSize=new ParticleSystem.MinMaxCurve(.65f,1.5f);main.maxParticles=75;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=silt.emission;emission.rateOverTime=30;
            var shape=silt.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=2.25f;shape.angle=19;shape.rotation=new Vector3(-90,0,0);
            var col=silt.colorOverLifetime;col.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.2f),new GradientAlphaKey(0,1)});col.color=gradient;
            silt.GetComponent<ParticleSystemRenderer>().sharedMaterial=mud;
            shadow=Node(kraken,"Sweep shadow",Vector3.zero);
            var patch=new Mesh{name="Rift shadow"};
            patch.vertices=new[]{new Vector3(-3.7f,0,-3.7f),new(3.7f,0,-3.7f),new(3.7f,0,3.7f),new(-3.7f,0,3.7f)};
            patch.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            var ink=new Color(.018f,.035f,.042f,.85f);patch.colors=new[]{ink,ink,ink,ink};
            patch.triangles=new[]{0,2,1,0,3,2};patch.RecalculateBounds();owned.Add(patch);
            shadow.gameObject.AddComponent<MeshFilter>().sharedMesh=patch;
            var shade=shadow.gameObject.AddComponent<MeshRenderer>();shade.sharedMaterial=mud;shade.shadowCastingMode=ShadowCastingMode.Off;
            shadow.gameObject.SetActive(false);
        }

        void MakeAngler(Transform proxy)
        {
            proxy.GetComponent<MeshRenderer>().enabled = false;
            Transform body = Node(proxy,"Reef angler",Vector3.zero);
            Vector3 scale = proxy.localScale;
            body.localScale = new Vector3(1 / scale.x,1 / scale.y,1 / scale.z);
            var hide = new Color(.23f,.19f,.24f);
            var silhouette = new ReefGeometry();
            silhouette.Tube(new[]{new Vector3(0,0,-1.45f),new(0,.02f,-.73f),new(0,.08f,-.18f),new(0,.06f,.43f),new(0,-.10f,.83f)},
                new[]{.065f,.29f,.53f,.53f,.34f},hide,12,.85f);
            silhouette.Tube(new[]{new Vector3(0,.37f,-.57f),new(0,.72f,-.74f),new(0,.52f,-.96f)},
                new[]{.17f,.1f,.012f},new Color(.27f,.25f,.22f),7,.56f);
            Part(body,"Spined angler hide",silhouette,skin);

            var ridge = new ReefGeometry();
            for (int side = -1; side <= 1; side += 2)
            {
                ridge.Tube(new[]{new Vector3(side*.17f,-.14f,.88f),new(side*.39f,-.06f,.62f),new(side*.46f,.06f,.37f)},
                    new[]{.1f,.17f,.1f},Armour,8,.8f);
                for (int i = 0; i < 4; i++)
                {
                    float z = .81f - i * .11f;
                    Vector3 top = new(side * (.20f + i * .054f),-.16f + i * .022f,z);
                    ridge.Tube(new[]{top,top + new Vector3(0,-.12f,-.018f),top + new Vector3(0,-.17f,.042f)},
                        new[]{.034f,.02f,.003f},Pale,6);
                }
            }
            Part(body,"Upper jaw and teeth",ridge,skin);
            anglerJaw = Node(body,"Angler jaw hinge",new Vector3(0,-.19f,.4f));
            var mandible = new ReefGeometry();
            mandible.Tube(new[]{new Vector3(0,-.11f,-.05f),new(0,-.092f,.23f),new(0,-.025f,.47f)},
                new[]{.24f,.25f,.10f},hide,9,.46f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    var tooth = new Vector3(side * (.12f + i * .041f),.005f,.39f - i * .09f);
                    mandible.Tube(new[]{tooth,tooth + new Vector3(0,.105f,-.03f)},new[]{.029f,.003f},Pale,6);
                }
            Part(anglerJaw,"Lower jaw and teeth",mandible,skin);

            anglerTail = Node(body,"Muscular tail",new Vector3(0,0,-1.18f));
            var tail = new ReefGeometry();
            tail.Tube(new[]{Vector3.zero,new Vector3(0,.018f,-.40f),new(0,.13f,-.76f)},
                new[]{.16f,.10f,.020f},hide,8,.76f);
            tail.Tube(new[]{new Vector3(0,-.13f,-.54f),new(0,.05f,-.68f),new(0,.40f,-.91f)},
                new[]{.014f,.15f,.016f},Armour,8,.42f);
            Part(anglerTail,"Tail and split fin",tail,skin);
            anglerFinLeft = AnglerSideFin(body,-1);
            anglerFinRight = AnglerSideFin(body,1);

            var eyes = new ReefGeometry();
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = new Vector3(s * .405f,.19f,.52f);
                eyes.Tube(new[]{eye,eye + new Vector3(s*.055f,.037f,.054f)},new[]{.084f,.042f},new Color(.32f,.81f,.64f),10,.86f);
            }
            huntingLights = Material("NereidReef","Angler hunting light");
            huntingLights.SetFloat("_Glow",1.2f);
            Part(body,"Reflective angler eyes",eyes,huntingLights);
            MakeLure(body);
        }

        Transform AnglerSideFin(Transform body,int side)
        {
            var joint = Node(body,"Angler fin joint",new Vector3(side*.34f,-.03f,-.1f));
            var fin = new ReefGeometry();
            fin.Tube(new[]{Vector3.zero,new Vector3(side*.25f,-.07f,-.19f),new(side*.59f,-.17f,-.53f)},
                new[]{.13f,.14f,.014f},Armour,7,.39f);
            Part(joint,"Ribbed pectoral fin",fin,skin);
            return joint;
        }

        void MakeLure(Transform angler)
        {
            var whisker = new ReefGeometry();
            whisker.Tube(new[]{new Vector3(0,.36f,.3f),new Vector3(0,.64f,.45f),new Vector3(.035f,.69f,.68f),new Vector3(.06f,.6f,.9f)},
                new[]{.05f,.042f,.025f,.017f},Armour,7,.82f);
            Part(angler,"Lure filament",whisker,skin);
            lure=Node(angler,"Shoal-shaped angler lure",new(.06f,.6f,.9f));
            lureFish=new Transform[5];
            for(int i=0;i<lureFish.Length;i++)
            {
                var shape=new ReefGeometry();
                Color tint=new(.19f,.69f,.59f);
                shape.Tube(new[]{new Vector3(-.046f,0,0),Vector3.zero,new(.057f,.006f,0)},new[]{.003f,.018f,.002f},tint,6,.74f);
                shape.Tube(new[]{new Vector3(-.066f,.009f,0),new(-.044f,0,0)},new[]{.014f,.003f},tint,5);
                lureFish[i]=Part(lure,"Lure fin "+i,shape,luminous);
            }
        }

        public void Present(ThreatDirector.ReefSnapshot state)
        {
            float t=Time.time,blend=first?1:1-Mathf.Exp(-Time.deltaTime*9);
            var angler=ThreatDirector.Instance.AnglerStage;
            clamShell.localRotation=Quaternion.Slerp(clamShell.localRotation,Quaternion.Euler(state.ClamClosed?-1:-34+2*Mathf.Sin(t*.85f),0,0),1-Mathf.Exp(-Time.deltaTime*10));
            eel.gameObject.SetActive(state.Deep);kraken.gameObject.SetActive(state.Deep);
            if(state.Deep)
            {
                eel.position=Vector3.Lerp(eel.position,state.Eel,blend);
                if(state.EelHeading.sqrMagnitude>.1f)eel.rotation=Quaternion.Slerp(eel.rotation,Quaternion.LookRotation(state.EelHeading),blend);
                for(int i=0;i<eelBody.Count;i++)
                {
                    float u=i/(float)(eelBody.Count-1);
                    eelBody.Path[i]=new Vector3(Mathf.Sin(u*6.4f-t*(state.EelPhase==2?7.6f:3.7f))*.52f*u,Mathf.Sin(u*4-t*1.4f)*u*.24f,-3.7f*u);
                }
                eelBody.Deform();
                eelPhotophores.localScale = Vector3.one * (state.EelPhase == 1 ? 1 + .12f * Mathf.Sin(t*14) : 1);
                float breath = Mathf.Sin(t * .9f) * .035f;
                float emerge = state.KrakenPhase == 1 ? .72f : state.KrakenPhase == 2 ? 1f : 0;
                krakenMantle.localPosition = Vector3.Lerp(krakenMantle.localPosition,Vector3.up * (emerge * 1.2f + breath),blend);
                krakenMantle.localScale = new Vector3(1 + breath,1 - breath * .5f,1 + breath);

                Vector3 rest=new(-4.2f,.15f,3.45f);
                Vector3 destination=state.KrakenPhase is 1 or 2?kraken.InverseTransformPoint(state.Sweep):rest;
                if(state.KrakenPhase==1)destination=Vector3.Lerp(rest,destination,.38f)+Vector3.up*1.8f;
                if(state.KrakenPhase==2)destination+=Vector3.up*.33f;
                var oldTip=strikingArm.Path[strikingArm.Count-1];
                var tip=first?rest:Vector3.Lerp(oldTip,destination,1-Mathf.Exp(-Time.deltaTime*(state.KrakenPhase==2?13:4)));
                for(int i=0;i<strikingArm.Count;i++)
                {
                    float u=i/(float)(strikingArm.Count-1);
                    strikingArm.Path[i]=Vector3.Lerp(Vector3.forward,tip,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*(1.1f+tip.magnitude*.12f)+Vector3.right*Mathf.Sin(u*4+t*1.5f)*.48f*u;
                }
                strikingArm.Deform();
                if(previousKraken!=state.KrakenPhase)
                {
                    if(state.KrakenPhase==1)
                    {
                        warning.position=new Vector3(state.Sweep.x,-16.8f,state.Sweep.z);
                        var start=state.Sweep+Vector3.up*.1f;
                        float closest=float.PositiveInfinity;
                        foreach(var hit in Physics.RaycastAll(start,Vector3.down,40,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        {
                            if(hit.distance>=closest || hit.collider.GetComponentInParent<DiverMotor>()!=null || hit.normal.y<.45f)continue;
                            closest=hit.distance;warning.position=hit.point+hit.normal*.12f;
                            shadow.rotation=Quaternion.FromToRotation(Vector3.up,hit.normal);
                        }
                        shadow.position=warning.position;shadow.gameObject.SetActive(true);
                        silt.Play();
                    }
                    else if(state.KrakenPhase!=2){silt.Stop();shadow.gameObject.SetActive(false);}
                }
            }
            if(lure!=null)
            {
                // The moving points on the lure spread into a pale fan before the fish commits to a strike.
                lureDisplay=Mathf.MoveTowards(lureDisplay,angler==ThreatDirector.HuntStage.Display?1f:0f,Time.deltaTime*3.5f);
                bool striking = angler == ThreatDirector.HuntStage.Pursuit;
                bool fast = striking || angler == ThreatDirector.HuntStage.Overshoot;
                float jaw = striking ? 36 : 4 + lureDisplay * 22;
                anglerJaw.localRotation = Quaternion.Slerp(anglerJaw.localRotation,Quaternion.Euler(jaw,0,0),blend);
                float stroke = Mathf.Sin(t * (fast ? 11 : angler == ThreatDirector.HuntStage.Circle ? 6 : 4));
                anglerTail.localRotation = Quaternion.Euler(0,stroke * (fast ? 36 : 20),0);
                anglerFinLeft.localRotation = Quaternion.Euler(0,0,stroke * 17);
                anglerFinRight.localRotation = Quaternion.Euler(0,0,-stroke * 17);
                huntingLights.SetFloat("_Glow",1.05f + lureDisplay * (2.7f + .48f * Mathf.Sin(t * 16)));
                for(int i=0;i<lureFish.Length;i++)
                {
                    float phase=t*1.22f+i*2.4f;
                    var drift=new Vector3(Mathf.Cos(phase)*.13f,.04f*i+.018f*Mathf.Sin(phase),Mathf.Sin(phase)*.055f);
                    float side=i-(lureFish.Length-1)*.5f;
                    var fan=new Vector3(side*.11f,.12f-.018f*side*side,.03f);
                    lureFish[i].localPosition=Vector3.Lerp(drift,fan,lureDisplay);
                    lureFish[i].localRotation=Quaternion.Euler(0,-phase*Mathf.Rad2Deg,0);
                }
            }
            previousKraken=state.KrakenPhase;first=false;
        }

        sealed class FlexibleBody
        {
            const int Sides=10;
            readonly Vector3[] vertices,normals;
            readonly Mesh mesh;
            readonly float width;
            public readonly Vector3[] Path;
            public int Count=>Path.Length;

            public FlexibleBody(NereidFauna owner,Transform parent,string label,int rings,float radius,Color tint,Material material)
            {
                width=radius;Path=new Vector3[rings];vertices=new Vector3[rings*Sides];normals=new Vector3[vertices.Length];
                var colours=new Color[vertices.Length];var faces=new List<int>();
                for(int i=0;i<rings;i++)for(int j=0;j<Sides;j++)
                {
                    int a=i*Sides+j,b=i*Sides+(j+1)%Sides;
                    colours[a]=j<=2&&i%3==0?Color.Lerp(tint,Pale,.45f):tint;
                    if(i==0)continue;
                    faces.Add(a-Sides);faces.Add(b-Sides);faces.Add(b);faces.Add(a-Sides);faces.Add(b);faces.Add(a);
                }
                mesh=new Mesh{name=label};mesh.MarkDynamic();mesh.vertices=vertices;mesh.colors=colours;mesh.SetTriangles(faces,0);
                owner.owned.Add(mesh);
                var node=owner.Node(parent,label,Vector3.zero);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            }

            public void Deform()
            {
                Vector3 side=Vector3.right;
                for(int i=0;i<Path.Length;i++)
                {
                    float u=i/(float)(Path.Length-1);
                    var tangent=(Path[Mathf.Min(i+1,Path.Length-1)]-Path[Mathf.Max(0,i-1)]).normalized;
                    side=Vector3.ProjectOnPlane(side,tangent);
                    if(side.sqrMagnitude<.001f)side=Vector3.Cross(tangent,Vector3.up);
                    side.Normalize();var up=Vector3.Cross(tangent,side);
                    float rad=width*(1-Mathf.Pow(u,1.3f))+.012f;
                    for(int j=0;j<Sides;j++)
                    {
                        float angle=j*(2*Mathf.PI/Sides);
                        var n=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                        vertices[i*Sides+j]=Path[i]+n*rad;normals[i*Sides+j]=n;
                    }
                }
                mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
            }
        }

        void OnDestroy()
        {
            if(lure!=null)Destroy(lure.gameObject);
            foreach(var asset in owned)if(asset!=null)Destroy(asset);
        }
    }
}
