using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Raid.UI;

namespace Raid.Nereid
{
    // Coordinates are metres in the original 2x vessel rig. Network peers build
    // identical bulkheads and stairs without changing the baked hull's origin.
    internal sealed class NereidDeckExtension : MonoBehaviour
    {
        const float StairX=-2.58f, StairWidth=1.88f, StairStart=-16.95f, StairEnd=-24.85f, UpperDeck=4.85f;
        const float BridgeFront=-24.78f, BridgeBack=-30.42f, BridgeHalfWidth=4.32f;
        static readonly Bounds Machinery=new(new Vector3(0,1.63f,-20.27f),new Vector3(8.44f,3.50f,8.87f));
        internal static readonly Vector3 SkillStation=new(3.30f,0,-22.40f);
        internal static readonly Vector2 ScreenSize=new(2.64f,1.94f);
        const string PropFolder="ExpeditionAssets/NereidExpeditionProps/";

        readonly List<Object> owned=new();
        readonly List<Light> cabinLights=new();
        Material enamel,steel,gunmetal,ochre,warm,cold,glass,screen,coating;
        Transform archive;
        PanelBatch batch;
        ThreatDirector threat;
        float nextLighting;


        internal static NereidDeckExtension Install(NereidVessel vessel)
        {
            var expansion=Node(vessel.transform,"NEREID / machinery deck and bridge",Vector3.zero).gameObject.AddComponent<NereidDeckExtension>();
            expansion.Build(vessel.transform);
            return expansion;
        }

        internal static Vector3 StairMark(float z,float clearance)
            => new(StairX,UpperDeck*Mathf.InverseLerp(StairStart,StairEnd,z)+clearance,z);

        internal static bool TryGround(Vector3 local,out float floor)
        {
            floor=0;
            bool overStairs=Mathf.Abs(local.x-StairX)<=StairWidth*.5f+.08f &&
                local.z<=StairStart+.12f && local.z>=StairEnd-.06f;
            if(overStairs)
            {
                floor=UpperDeck*Mathf.InverseLerp(StairStart,StairEnd,local.z);
                if(local.y>=floor-.14f && local.y<=floor+2.01f)return true;
            }
            if(local.y>=UpperDeck-.12f && local.y<=UpperDeck+2.20f && local.z<=BridgeFront+.10f &&
                local.z>=BridgeBack-.08f && Mathf.Abs(local.x)<=BridgeHalfWidth+.08f)
            {
                floor=UpperDeck;
                return true;
            }
            floor=0;
            return Machinery.Contains(local);
        }

        void Build(Transform hull)
        {
            var oldStern=hull.Find("Stern sealed bulkhead");
            if(oldStern!=null && oldStern.TryGetComponent<BoxCollider>(out var obsolete))obsolete.enabled=false;
            CreateMaterials(hull);
            BuildMachinery();
            BuildStairway();
            BuildBridge();
            Wayfinding();
            FitExistingMonitors(hull);
        }

        void CreateMaterials(Transform hull)
        {
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            enamel=Finish(lit,"Pressure panels / petrol",new Color(.16f,.245f,.245f),.40f,.38f);
            steel=Finish(lit,"Deck zinc and welds",new Color(.35f,.42f,.415f),.63f,.48f);
            gunmetal=Finish(lit,"Structural graphite",new Color(.088f,.115f,.120f),.39f,.33f);
            ochre=Finish(lit,"Traffic and machinery ochre",new Color(.67f,.37f,.090f),.24f,.42f);
            warm=Finish(lit,"Opal amber fixtures",new Color(.95f,.63f,.30f),.02f,.42f,1.75f);
            cold=Finish(lit,"Ice-blue guides",new Color(.28f,.80f,.84f),.06f,.42f,1.42f);
            screen=Finish(lit,"Terminal pressure glass",new Color(.012f,.039f,.048f),.02f,.26f,.35f);
            coating=Finish(Resources.Load<Shader>("NereidHullCoating")??lit,"NEREID / saddle plate",new Color(.23f,.36f,.38f),.21f,.39f);
            var originalGlass=FindMaterial(hull,"Nereid_Window");
            glass=originalGlass!=null?originalGlass:Finish(lit,"Window fallback",new Color(.18f,.37f,.41f),.08f,.49f);
        }

        Material Finish(Shader shader,string label,Color tint,float metallic,float smooth,float emission=0)
        {
            var material=new Material(shader){name=label,hideFlags=HideFlags.DontSave};
            material.SetColor("_BaseColor",tint);
            material.SetFloat("_Metallic",metallic);
            material.SetFloat("_Smoothness",smooth);
            if(emission>0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",tint*emission);
            }
            owned.Add(material);
            return material;
        }

        static Material FindMaterial(Transform hull,string name)
        {
            foreach(var renderer in hull.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    if(material!=null && material.name==name)return material;
            return null;
        }

        void BuildMachinery()
        {
            archive=Node(transform,"03 / Service and machinery deck",Vector3.zero);
            batch=new PanelBatch(archive,owned);
            Panel("Machinery deck",new(0,-.13f,-20.28f),new(8.5f,.26f,8.92f),gunmetal,true);
            Panel("Stern watertight wall",new(0,1.60f,-24.7f),new(8.45f,3.2f,.23f),enamel,true);
            Panel("Aft transfer header",new(0,2.83f,-16.015f),new(2.95f,.76f,.25f),steel,true);
            Panel("Shipboard equipment ceiling",new(1.16f,3.20f,-20.32f),new(5.95f,.24f,8.86f),gunmetal,true);
            Panel("Port equipment ceiling",new(-3.98f,3.20f,-20.32f),new(.48f,.24f,8.86f),gunmetal,true);
            for(int side=-1;side<=1;side+=2)
            {
                Panel("Stern frame cheek",new(side*2.27f,1.56f,-16.015f),new(1.62f,3.12f,.24f),enamel,true);
                Panel("Stern frame cheek outboard",new(side*4.49f,1.56f,-16.015f),new(2.84f,3.12f,.24f),enamel,true);
                Panel("Pressure-room side",new(side*4.22f,1.57f,-20.28f),new(.18f,3.15f,8.87f),enamel,true);
                Panel("Painted utility chase",new(side*4.04f,2.34f,-20.35f),new(.23f,.37f,8.55f),gunmetal);
                Panel("Upper fluid pipe",new(side*3.99f,2.64f,-20.32f),new(.22f,.19f,8.63f),ochre);
                for(int i=0;i<5;i++)
                {
                    float z=-16.43f-i*1.76f;
                    Panel("Pressure rib",new(side*4.04f,1.59f,z),new(.20f,3.07f,.16f),steel);
                    Panel("Horizontal inspection panel",new(side*4.106f,1.31f,z-.76f),new(.09f,1.25f,1.30f),gunmetal);
                    Panel("Service footlight",new(side*3.998f,.25f,z-.46f),new(.07f,.075f,.45f),side<0?warm:cold);
                }
            }

            // Close the original aft pressure compartment at the new doorway.
            // Rounded trim is deliberately outside the capsule's entry path.
            for(int i=0;i<15;i++)
            {
                float x=(i-7)*.19f;
                Panel("Machinery threshold chevron",new(x,.018f,-16.03f),new(.115f,.018f,.31f),ochre,false,Quaternion.Euler(0,32,0));
            }
            for(int row=0;row<7;row++)
                for(int column=0;column<5;column++)
                {
                    float x=-3.26f+column*1.64f,z=-16.61f-row*1.18f;
                    Panel("Pressed deck panel",new(x,.014f,z),new(1.59f,.024f,1.13f),column==2?enamel:steel);
                    if(column==2)Panel("Path edge marking",new(x+.74f,.029f,z),new(.05f,.016f,.86f),ochre);
                }
            for(int i=0;i<3;i++)
            {
                float z=-17.7f-i*2.65f;
                Panel("Inspection gantry",new(1.16f,2.98f,z),new(5.87f,.22f,.18f),steel);
                Panel("Twin task lamp",new(.37f,2.83f,z),new(1.55f,.10f,.28f),gunmetal);
                Panel("Twin task lamp diffuser",new(.37f,2.765f,z),new(1.30f,.035f,.18f),i==0?cold:warm);
                Lamp(new(.3f,2.70f,z),i==0?new(.51f,.88f,.98f):new(1,.73f,.46f),7.7f,170);
            }
            PanelsOfExchange();
            batch.Build();
            SetProp("Prop_AccessPoint",new(3.72f,1.06f,-23.66f),new(.38f,1.62f,.83f),Quaternion.Euler(0,90,0),steel);
            SetProp("Prop_Crate4",new(-1.25f,.37f,-23.66f),new(.83f,.72f,.84f),Quaternion.Euler(0,12,0),ochre);
            SetProp("Door_Frame_SquareTall",new(0,1.19f,-16.10f),new(3.64f,2.44f,.31f),Quaternion.identity,enamel,false);
        }

        void PanelsOfExchange()
        {
            Panel("Pump rack footing",new(0,.19f,-23.80f),new(1.40f,.38f,1.13f),gunmetal,true);
            Panel("Accumulator enclosure",new(0,1.09f,-24.04f),new(1.38f,1.51f,.81f),enamel,true);
            Panel("Inset accumulator face",new(0,1.14f,-23.60f),new(1.23f,1.28f,.06f),gunmetal);
            for(int i=0;i<8;i++)
                Panel("Oblong cooling slots",new(0,.63f+i*.107f,-23.55f),new(.95f,.039f,.035f),steel);
            for(int j=0;j<3;j++)
                Panel("Pump current indicators",new(-.36f+j*.19f,1.61f,-23.546f),new(.085f,.055f,.035f),j==0?warm:cold);
            foreach(float x in new[]{-.68f,.68f})
            {
                Panel("Accumulator copper inlet",new(x,1.0f,-23.69f),new(.11f,1.66f,.11f),ochre);
                Panel("Accumulator shut-off lever",new(x,1.36f,-23.62f),new(.28f,.053f,.052f),steel);
            }
        }

        void BuildStairway()
        {
            archive=Node(transform,"04 / Ascent to command deck",Vector3.zero);
            batch=new PanelBatch(archive,owned);
            const int treads=41;
            float run=(StairStart-StairEnd)/treads,rise=UpperDeck/treads;
            for(int step=0;step<treads;step++)
            {
                float z=StairStart-run*(step+.5f),y=rise*(step+1);
                Panel("Raised traction tread",new(StairX,y-.085f,z),new(StairWidth,.17f,run+.024f),steel,true);
                Panel("Machined amber nosing",new(StairX,y-.033f,z+run*.50f),new(StairWidth-.11f,.049f,.024f),step%4==0?ochre:gunmetal);
            }
            foreach(int side in new[]{-1,1})
            {
                float x=StairX+side*StairWidth*.51f;
                Beam("Stair stringer",new(x,-.16f,StairStart),new(x,UpperDeck-.16f,StairEnd),.13f,.44f,gunmetal);
                Beam("Stair guard",new(x,.58f,StairStart),new(x,UpperDeck+.58f,StairEnd),.082f,.09f,ochre,true);
                Beam("Pressure step cheek",new(x,.02f,StairStart),new(x,UpperDeck+.02f,StairEnd),.09f,.19f,enamel,true);
                for(int i=1;i<10;i++)
                {
                    float t=i/10f;
                    Panel("Stair rail support",new(x,UpperDeck*t+.30f,Mathf.Lerp(StairStart,StairEnd,t)),new(.090f,.62f,.082f),steel);
                }
            }

            // The external part of the inclining companionway has its own
            // pressure cheeks. At the lower end it opens inside the machinery room.
            float exposedStart=-21.30f;
            float baseHeight=UpperDeck*Mathf.InverseLerp(StairStart,StairEnd,exposedStart);
            Beam("Companionway roof",new(StairX,baseHeight+1.95f,exposedStart),
                new(StairX,UpperDeck+1.95f,StairEnd),2.27f,.21f,coating,true);
            foreach(int side in new[]{-1,1})
            {
                float x=StairX+side*1.04f;
                Beam("Inclined pressure side",new(x,baseHeight+.98f,exposedStart),new(x,UpperDeck+.98f,StairEnd),.20f,1.96f,enamel,true);
                Beam("Ascent indicator strip",new(x-side*.13f,baseHeight+.92f,exposedStart),new(x-side*.13f,UpperDeck+.92f,StairEnd),
                    .048f,.052f,cold);
            }
            Lamp(new(StairX,3.14f,-19.68f),new(1,.60f,.30f),6.0f,91);
            Lamp(new(StairX,6.27f,-24.19f),new(.39f,.8f,1),5.0f,103);
            batch.Build();
        }

        void BuildBridge()
        {
            archive=Node(transform,"05 / Panoramic bridge",Vector3.zero);
            batch=new PanelBatch(archive,owned);
            float center=(BridgeFront+BridgeBack)*.5f,length=BridgeFront-BridgeBack;
            Panel("Bridge / deck collision",new(0,UpperDeck-.16f,center),new(8.67f,.32f,length+.04f),gunmetal,true);
            Panel("Bridge / pressure crown",new(0,UpperDeck+2.14f,center),new(8.93f,.29f,length+.23f),coating,true);
            Panel("Bridge / hull keel band",new(0,UpperDeck-.32f,center),new(8.87f,.22f,length+.18f),coating);
            Panel("Bridge / raised weatherproof cap",new(0,UpperDeck+2.38f,center-.19f),new(7.85f,.22f,length-.20f),enamel);
            Panel("Bridge / marine equipment trunk",new(.72f,UpperDeck+2.62f,center-.83f),new(4.36f,.36f,2.70f),coating);
            Panel("Bridge / radio mast",new(.66f,UpperDeck+3.11f,center-.84f),new(.13f,1.39f,.17f),steel);
            Panel("Bridge / mast sweep",new(.66f,UpperDeck+3.72f,center-.84f),new(1.77f,.10f,.14f),steel);
            Panel("Bridge / red running light",new(.66f,UpperDeck+3.90f,center-.84f),new(.20f,.29f,.22f),warm);
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<4;i++)
                {
                    float z=BridgeFront-(i+.5f)*length/4;
                    Window(new(side*BridgeHalfWidth,UpperDeck,z),length/4,Quaternion.Euler(0,90,0));
                }
                float x=side*2.46f;
                for(int i=0;i<3;i++)Panel("Reflective deck sheet",new(x,UpperDeck+.018f,BridgeFront-.9f-1.85f*i),new(3.38f,.033f,1.80f),steel);
                Panel("Foredeck scanner seat",new(side*2.97f,UpperDeck+.26f,BridgeBack+.69f),new(1.72f,.52f,.86f),gunmetal,true);
                Panel("Foredeck status horizon",new(side*2.97f,UpperDeck+.52f,BridgeBack+.69f),new(1.59f,.04f,.73f),cold);
                Panel("Exterior beam finder",new(side*3.68f,UpperDeck+1.99f,BridgeBack-.22f),new(.98f,.25f,.27f),gunmetal);
                Panel("Exterior lens in a dark bezel",new(side*3.68f,UpperDeck+1.99f,BridgeBack-.37f),new(.72f,.12f,.038f),warm);
            }
            float rearSpan=(BridgeHalfWidth*2)/5;
            for(int i=0;i<5;i++)Window(new(-BridgeHalfWidth+(i+.5f)*rearSpan,UpperDeck,BridgeBack),rearSpan,Quaternion.identity);
            Window(new(1.20f,UpperDeck,BridgeFront),6.2f,Quaternion.identity);
            Panel("Port portal wall",new(-3.95f,UpperDeck+1.06f,BridgeFront),new(.71f,2.12f,.24f),enamel,true);
            Panel("Incoming stair portal lintel",new(StairX,UpperDeck+2.01f,BridgeFront),new(2.15f,.23f,.28f),ochre,true);
            for(int i=0;i<3;i++)
            {
                float z=BridgeFront-.85f-i*1.73f;
                Panel("Amber command-ring lamp",new(0,UpperDeck+1.99f,z),new(3.48f,.092f,.22f),gunmetal);
                Panel("Amber command-ring lens",new(0,UpperDeck+1.931f,z),new(2.98f,.038f,.082f),warm);
                Lamp(new(0,UpperDeck+1.86f,z),new(1,.69f,.40f),6.4f,118);
            }
            batch.Build();
        }

        void Window(Vector3 origin,float width,Quaternion rotation)
        {
            void Rail(string label,Vector3 offset,Vector3 size,Material finish,bool solid=false)
                => Panel(label,origin+rotation*offset,size,finish,solid,rotation);
            Rail("Panorama lower armour",new(0,.27f,0),new(width+.025f,.58f,.24f),enamel,true);
            Rail("Panorama sill / machined steel",new(0,.59f,0),new(width+.02f,.10f,.35f),steel);
            Rail("Panorama glazing",new(0,1.19f,0),new(width-.16f,1.14f,.035f),glass,true);
            Rail("Panorama upper band",new(0,1.95f,0),new(width+.03f,.36f,.24f),gunmetal);
            Rail("Panorama keeper",new(width*.5f-.04f,1.21f,0),new(.095f,1.37f,.20f),steel,true);
        }

        void Wayfinding()
        {
            Sign(transform,"03   МАШИННЫЙ ОТСЕК",new(0,2.80f,-15.82f),Quaternion.Euler(0,180,0),2.57f,.17f);
            Sign(transform,"01   РЕЗЕРВУАР   ↑",new(0,2.75f,-16.17f),Quaternion.identity,2.46f,.17f);
            Sign(transform,"05   МОСТИК   ↑",new(StairX,1.46f,-16.47f),Quaternion.Euler(0,180,0),2.02f,.16f);
            Sign(transform,"04   СНАРЯЖЕНИЕ",new(4.005f,2.08f,-20.61f),Quaternion.Euler(0,90,0),2.36f,.21f);
            Sign(transform,"02   ←   ШЛЮЗ",new(1.83f,2.36f,-14.35f),Quaternion.Euler(0,180,0),2.00f,.17f);
            Sign(transform,"NEREID   /   07",new(0,UpperDeck+2.42f,BridgeBack-.124f),Quaternion.identity,5.75f,.42f);
            Sign(transform,"КОМАНДНЫЙ МОСТИК",new(1.09f,UpperDeck+1.75f,BridgeFront-.165f),Quaternion.identity,3.20f,.23f);
        }

        void FitExistingMonitors(Transform hull)
        {
            foreach(var station in FindObjectsByType<ShipConsole>())
            {
                if(!station.IsMonitor || station.screenAnchor==null ||
                    station.gameObject.scene!=hull.gameObject.scene)continue;
                Vector3 local;
                float yaw;
                switch(station.kind)
                {
                    case ShipConsoleKind.Depths:local=new(-.38f,UpperDeck,-28.53f);yaw=180;break;
                    case ShipConsoleKind.Upgrades:local=new(3.30f,0,-19.19f);yaw=90;break;
                    case ShipConsoleKind.Reservoir:local=new(0,0,-.72f);yaw=180;break;
                    default:continue;
                }
                station.transform.SetPositionAndRotation(hull.TransformPoint(local),hull.rotation*Quaternion.Euler(0,yaw,0));
                FitMonitor(station);
            }
        }

        internal void FitMonitor(ShipConsole station)
        {
            Transform frame=station.transform, anchor=station.screenAnchor;
            foreach(Transform child in frame) if(child!=anchor)child.gameObject.SetActive(false);
            anchor.localPosition=new Vector3(0,1.42f,-.15f);
            anchor.localRotation=Quaternion.identity;
            anchor.localScale=Vector3.one;
            var face=anchor.GetComponent<BoxCollider>()??anchor.gameObject.AddComponent<BoxCollider>();
            face.enabled=true;
            face.center=Vector3.zero;
            face.size=new Vector3(ScreenSize.x/anchor.lossyScale.x,ScreenSize.y/anchor.lossyScale.y,.025f/anchor.lossyScale.z);
            station.range=3.45f;
            station.screenScale=.0018f;
            station.screenSize=ScreenSize;
            foreach(var text in anchor.GetComponentsInChildren<TMP_Text>(true))
            {
                text.rectTransform.sizeDelta=ScreenSize*.89f;
                text.rectTransform.localPosition=Vector3.back*.017f;
                text.fontSize=1.23f;
            }

            var console=Node(frame,"Armoured instrument and pressure frame",Vector3.zero);
            var art=new PanelBatch(console,owned);
            Vector3 centre=anchor.localPosition;
            art.Box(centre+new Vector3(0,0,.038f),new Vector3(ScreenSize.x,ScreenSize.y,.048f),screen);
            art.Box(centre+new Vector3(0,0,.208f),new Vector3(2.89f,2.14f,.30f),gunmetal);
            art.Box(new Vector3(0,.095f,.12f),new Vector3(1.60f,.16f,.76f),gunmetal);
            art.Box(new Vector3(0,.51f,.21f),new Vector3(.75f,.78f,.58f),enamel);
            art.Box(new Vector3(0,.453f,-.24f),new Vector3(1.53f,.085f,.46f),steel,Quaternion.Euler(14,0,0));
            art.Box(new Vector3(0,.408f,-.40f),new Vector3(1.34f,.047f,.055f),warm);
            foreach(int side in new[]{-1,1})
            {
                art.Box(centre+new Vector3(side*1.392f,0,.003f),new Vector3(.144f,2.22f,.102f),ochre);
                art.Box(centre+new Vector3(0,side*1.036f,.005f),new Vector3(2.66f,.131f,.10f),steel);
                art.Box(centre+new Vector3(side*1.444f,0,-.065f),new Vector3(.038f,.82f,.042f),warm);
                for(int i=0;i<2;i++)
                    art.Box(centre+new Vector3(side*1.393f,side*.94f,(i-.5f)*.052f),new Vector3(.055f,.06f,.06f),gunmetal);
            }
            art.Build();
            Collision(console,"Console lower support",new Vector3(0,.38f,.12f),new Vector3(1.18f,.72f,.62f),Quaternion.identity);
        }

        void SetProp(string id,Vector3 position,Vector3 size,Quaternion rotation,Material material,bool solid=true)
        {
            var asset=Resources.Load<GameObject>(PropFolder+id);
            if(asset==null)return;
            var seat=Node(archive,id,position);
            var copy=Instantiate(asset,seat);
            var renderers=copy.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0){Destroy(seat.gameObject);return;}
            Bounds bounds=new Bounds(seat.InverseTransformPoint(renderers[0].bounds.center),Vector3.zero);
            foreach(var renderer in renderers)
            {
                var rb=renderer.bounds;
                bounds.Encapsulate(seat.InverseTransformPoint(rb.min));
                bounds.Encapsulate(seat.InverseTransformPoint(rb.max));
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)materials[i]=material;
                renderer.sharedMaterials=materials;
            }
            if(Mathf.Min(bounds.size.x,bounds.size.y,bounds.size.z)<.001f){Destroy(seat.gameObject);return;}
            copy.transform.localScale=Vector3.Scale(copy.transform.localScale,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
            copy.transform.localPosition-=Vector3.Scale(bounds.center,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
            seat.localRotation=rotation;
            if(solid)Collision(seat,"Furniture collision",Vector3.zero,size,Quaternion.identity);
        }

        static void Sign(Transform parent,string value,Vector3 point,Quaternion rotation,float width,float size)
        {
            var font=UiRoot.Instance?.font;
            if(font==null)return;
            var sign=Node(parent,value,point);
            sign.localRotation=rotation;
            var glyph=sign.gameObject.AddComponent<TextMeshPro>();
            glyph.font=font;
            glyph.text=value;
            glyph.color=new Color(.77f,.87f,.84f);
            glyph.fontSize=size*10;
            glyph.rectTransform.sizeDelta=new Vector2(width,size*2.5f);
            glyph.alignment=TextAlignmentOptions.Center;
            glyph.textWrappingMode=TextWrappingModes.NoWrap;
            glyph.overflowMode=TextOverflowModes.Ellipsis;
        }

        void Lamp(Vector3 at,Color tint,float range,float power)
        {
            var lamp=Node(archive,"Baffled task lighting",at);
            lamp.localRotation=Quaternion.Euler(90,0,0);
            var light=lamp.gameObject.AddComponent<Light>();
            light.type=LightType.Spot;
            light.color=tint;
            light.range=range;
            light.intensity=power;
            light.spotAngle=111;
            light.innerSpotAngle=67;
            light.shadows=LightShadows.None;
            cabinLights.Add(light);
        }

        void Update()
        {
            if(Time.unscaledTime<nextLighting)return;
            nextLighting=Time.unscaledTime+.30f;
            var eye=Camera.main;
            if(eye==null)return;
            if(threat==null)threat=ThreatDirector.Instance;
            bool alarm=threat!=null && threat.NetworkObject!=null && threat.IsClientInitialized && threat.WaveActive;
            for(int i=0;i<cabinLights.Count;i++)
            {
                var light=cabinLights[i];
                if(i==0)light.color=alarm?new(.99f,.24f,.085f):new(.51f,.88f,.98f);
                light.enabled=(eye.transform.position-light.transform.position).sqrMagnitude<18*18;
            }
        }

        void OnDestroy()
        {
            foreach(var asset in owned) if(asset!=null)Destroy(asset);
        }

        void Panel(string label,Vector3 point,Vector3 size,Material material,bool solid=false,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            batch.Box(point,size,material,rotation);
            if(solid)Collision(archive,label,point,size,rotation);
        }

        void Beam(string label,Vector3 start,Vector3 end,float width,float height,Material material,bool solid=false)
        {
            var delta=end-start;
            Panel(label,(start+end)*.5f,new Vector3(width,height,delta.magnitude),material,solid,Quaternion.LookRotation(delta));
        }

        internal static Transform Node(Transform parent,string label,Vector3 point)
        {
            var item=new GameObject(label).transform;
            item.SetParent(parent,false);
            item.localPosition=point;
            return item;
        }

        static void Collision(Transform parent,string label,Vector3 point,Vector3 size,Quaternion rotation)
        {
            var node=Node(parent,label,point);
            node.localRotation=rotation;
            node.gameObject.AddComponent<BoxCollider>().size=size;
        }

        sealed class PanelBatch
        {
            readonly Dictionary<Material,ReefGeometry> groups=new();
            readonly Transform parent;
            readonly List<Object> owned;

            internal PanelBatch(Transform parent,List<Object> owned)
            {
                this.parent=parent;
                this.owned=owned;
            }

            internal void Box(Vector3 point,Vector3 size,Material material,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                if(!groups.TryGetValue(material,out var mesh))groups.Add(material,mesh=new ReefGeometry());
                Face(mesh,point,size,rotation,Vector3.forward,Vector3.right,Vector3.up);
                Face(mesh,point,size,rotation,Vector3.back,Vector3.left,Vector3.up);
                Face(mesh,point,size,rotation,Vector3.right,Vector3.back,Vector3.up);
                Face(mesh,point,size,rotation,Vector3.left,Vector3.forward,Vector3.up);
                Face(mesh,point,size,rotation,Vector3.up,Vector3.right,Vector3.back);
                Face(mesh,point,size,rotation,Vector3.down,Vector3.right,Vector3.forward);
            }

            static void Face(ReefGeometry mesh,Vector3 center,Vector3 size,Quaternion rotation,Vector3 normal,Vector3 right,Vector3 up)
            {
                var c=center+rotation*Vector3.Scale(size*.5f,normal);
                var x=rotation*Vector3.Scale(size*.5f,right);
                var y=rotation*Vector3.Scale(size*.5f,up);
                var tint=Color.white;
                int a=mesh.Add(c-x-y,tint),b=mesh.Add(c+x-y,tint),d=mesh.Add(c+x+y,tint),e=mesh.Add(c-x+y,tint);
                mesh.Quad(a,b,d,e);
            }

            internal void Build()
            {
                foreach(var group in groups)
                {
                    var part=Node(parent,group.Key.name,Vector3.zero);
                    var mesh=group.Value.Mesh(group.Key.name);
                    owned.Add(mesh);
                    part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=part.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=group.Key;
                    renderer.shadowCastingMode=group.Key.renderQueue>=3000?ShadowCastingMode.Off:ShadowCastingMode.On;
                }
            }
        }
    }
}
