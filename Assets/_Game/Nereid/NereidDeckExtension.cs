using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Coordinates are metres in the original 2x vessel rig. Network peers build
    // identical bulkheads and stairs without changing the baked hull's origin.
    internal sealed partial class NereidDeckExtension : MonoBehaviour
    {
        const float StairX=-2.58f, StairWidth=1.88f, StairStart=-17.65f, StairEnd=-24.85f, UpperDeck=4.85f;
        const float BridgeFront=-24.78f, BridgeBack=-30.42f, BridgeHalfWidth=4.32f;
        static readonly Bounds Machinery=new(new Vector3(0,1.63f,-20.27f),new Vector3(8.44f,3.50f,8.87f));
        internal static readonly Vector3 SkillStation=new(3.30f,0,-22.40f);
        internal static readonly Vector2 ScreenSize=new(2.64f,1.94f);
        const string PropFolder="ExpeditionAssets/NereidExpeditionProps/";

        readonly List<Object> owned=new();
        readonly List<Light> cabinLights=new();
        Material enamel,steel,gunmetal,ochre,warm,cold,glass,screen,coating,ceramic;
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

        internal static Vector3 CompanionwayFoot(float clearance) => StairMark(StairStart,clearance);

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
            BuildGalleryLandings();
            FitExistingMonitors(hull);
        }

        void BuildGalleryLandings()
        {
            archive=Node(transform,"Observation gallery turning landings",Vector3.zero);
            batch=new PanelBatch(archive,owned);
            foreach(float z in new[]{NereidVessel.GalleryAftFoot-.54f,NereidVessel.GalleryBowFoot+.54f})
            {
                Panel("Inset turning plate",new(-4.17f,.018f,z),new(2.22f,.025f,1.01f),centerDeckTexture);
                for(int i=0;i<3;i++)
                    foreach(int side in new[]{-1,1})
                        Beam("Painted turn chevron",new(-4.75f+i*.46f,.034f,z+side*.26f),
                            new(-4.53f+i*.46f,.034f,z),.052f,.009f,ochre);
            }
            batch.Build();
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
            ceramic=Finish(lit,"Ceramic instrument cladding",new Color(.57f,.63f,.59f),.23f,.48f);
            NereidSurfaceFinish.Apply(enamel,NereidSurfaceFinish.Surface.Enamel);
            NereidSurfaceFinish.Apply(gunmetal,NereidSurfaceFinish.Surface.Deck);
            NereidSurfaceFinish.Apply(steel,NereidSurfaceFinish.Surface.Ceramic);
            NereidSurfaceFinish.Apply(ceramic,NereidSurfaceFinish.Surface.Ceramic);
            NereidSurfaceFinish.Apply(coating,NereidSurfaceFinish.Surface.Hull,exterior:true);
            CreateFinishes(lit);
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
            Panel("Aft propulsion pressure wall",new(0,1.60f,-24.7f),new(8.45f,3.2f,.23f),enamel,true);
            Panel("Transfer header",new(0,2.83f,-16.015f),new(2.95f,.76f,.25f),gunmetal,true);
            Panel("Coolant plenum",new(1.16f,3.20f,-20.32f),new(5.95f,.24f,8.86f),gunmetal,true);
            Panel("Port equipment ceiling",new(-3.98f,3.20f,-20.32f),new(.48f,.24f,8.86f),gunmetal,true);
            foreach(int side in new[]{-1,1})
            {
                Panel("Machinery entrance bulkhead",new(side*2.27f,1.56f,-16.015f),new(1.62f,3.12f,.24f),enamel,true);
                Panel("Machinery entrance bulkhead return",new(side*4.49f,1.56f,-16.015f),new(2.84f,3.12f,.24f),enamel,true);
                Panel("Propulsion room side",new(side*4.22f,1.57f,-20.28f),new(.18f,3.15f,8.87f),gunmetal,true);
                Beam("Entrance pressure jamb",new(side*1.35f,.026f,-16.20f),new(side*1.35f,1.95f,-16.20f),.16f,.20f,ceramic);
                Beam("Rounded pressure jamb shoulder",new(side*1.35f,1.95f,-16.20f),new(side*.97f,2.41f,-16.20f),.17f,.20f,ceramic);
                Panel("Downward handle light",new(side*1.255f,1.53f,-16.234f),new(.028f,.61f,.10f),side<0?cold:warm);
            }
            Panel("Pressure jamb crown",new(0,2.41f,-16.20f),new(2.0f,.16f,.20f),ceramic);
            Panel("Propulsion access threshold",new(0,.022f,-16.12f),new(2.63f,.035f,.30f),steel);
            MachineryFinishes();
            batch.Build();
            SetProp("Prop_AccessPoint",new(3.72f,1.06f,-23.66f),new(.38f,1.62f,.83f),Quaternion.Euler(0,90,0),steel);
            SetProp("Prop_Crate4",new(-1.25f,.37f,-23.66f),new(.83f,.72f,.84f),Quaternion.Euler(0,12,0),ochre);
            SetProp("Prop_Vent_Wide",new(3.99f,2.24f,-17.24f),new(1.06f,.12f,.38f),Quaternion.Euler(90,0,90),
                brushedRib,false,Fittings+"Models/");
            SetProp("TopCables_Straight",new(2.50f,3.02f,-21.71f),new(.51f,.19f,3.87f),Quaternion.identity,
                gunmetal,false,Fittings+"Models/");
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
                Panel("Raised traction tread",new(StairX,y-.085f,z),new(StairWidth,.17f,run+.024f),gunmetal,true);
                Panel("Machined amber nosing",new(StairX,y-.033f,z+run*.50f),new(StairWidth-.11f,.049f,.024f),step%4==0?ochre:steel);
            }
            Panel("Companionway turning plate",new(-1.73f,.017f,StairStart+.71f),new(3.55f,.025f,1.33f),centerDeckTexture);
            foreach(int side in new[]{-1,1})
            {
                Panel("Companionway landing edge",new(-1.73f,.033f,StairStart+.71f+side*.63f),new(3.49f,.009f,.046f),ochre);
                Panel("Companionway sill",new(StairX+side*.94f,.036f,StairStart+.30f),new(.046f,.009f,.57f),ochre);
            }
            foreach(int side in new[]{-1,1})
            {
                float x=StairX+side*StairWidth*.51f;
                Beam("Stair stringer",new(x,-.16f,StairStart),new(x,UpperDeck-.16f,StairEnd),.13f,.44f,gunmetal);
                Beam("Stair guard",new(x,.58f,StairStart),new(x,UpperDeck+.58f,StairEnd),.082f,.09f,ceramic,true);
                Beam("Pressure step cheek",new(x,.02f,StairStart),new(x,UpperDeck+.02f,StairEnd),.09f,.19f,enamel,true);
                for(int i=1;i<10;i++)
                {
                    float t=i/10f;
                    Panel("Stair rail support",new(x,UpperDeck*t+.30f,Mathf.Lerp(StairStart,StairEnd,t)),new(.090f,.62f,.082f),steel);
                }
            }

            // The external part of the inclining companionway has its own
            // pressure cheeks. At the lower end it opens inside the machinery room.
            const float exposedStart=-19.54f, headroom=1.94f;
            float baseHeight=StairMark(exposedStart,0).y;
            Beam("Companionway roof",new(StairX,baseHeight+headroom,exposedStart),
                new(StairX,UpperDeck+headroom,StairEnd),2.27f,.21f,coating,true);
            foreach(int side in new[]{-1,1})
            {
                float x=StairX+side*1.04f;
                Beam("Inclined pressure side",new(x,baseHeight+.98f,exposedStart),new(x,UpperDeck+.98f,StairEnd),.20f,1.96f,enamel,true);
                Beam("Ascent indicator strip",new(x-side*.13f,baseHeight+.92f,exposedStart),new(x-side*.13f,UpperDeck+.92f,StairEnd),
                    .048f,.052f,cold);
            }
            foreach(float z in new[]{-19.84f,-21.78f,-23.72f})
            {
                var tread=StairMark(z,0);
                Beam("Recessed companionway ceiling light",StairMark(z+.29f,headroom-.13f),
                    StairMark(z-.29f,headroom-.13f),.54f,.028f,warm);
                Lamp(tread+Vector3.up*(headroom-.20f),new(.66f,.87f,1),5.6f,125);
            }
            batch.Build();
        }

        void BuildBridge()
        {
            archive=Node(transform,"05 / Panoramic bridge",Vector3.zero);
            batch=new PanelBatch(archive,owned);
            float center=(BridgeFront+BridgeBack)*.5f,length=BridgeFront-BridgeBack;
            Collision(archive,"Bridge / deck collision",new(0,UpperDeck-.16f,center),new(8.67f,.32f,length+.04f),Quaternion.identity);
            Collision(archive,"Bridge / pressure crown",new(0,UpperDeck+2.14f,center),new(8.93f,.29f,length+.23f),Quaternion.identity);
            Panel("Bridge deck plate",new(0,UpperDeck-.055f,center),new(8.57f,.105f,length+.04f),gunmetal);
            Panel("Interior crown lining",new(0,UpperDeck+2.02f,center),new(8.48f,.105f,length+.026f),gunmetal);
            const float crownPanelWidth=2.66f, crownHeight=1.82f;
            // The outer fairing overlaps the old lining. Mount the service
            // panels below its lowest point inside the glazed cabin.
            float crownPanelLength=length/4;
            for(int row=0;row<4;row++)
                for(int column=-1;column<=1;column++)
                {
                    var seat=new Vector3(column*(crownPanelWidth+.055f),UpperDeck+crownHeight,BridgeFront-(row+.5f)*crownPanelLength);
                    TiledPanel(seat,new(crownPanelWidth,.045f,crownPanelLength-.056f),Quaternion.identity,pressedEnamel,PressedPanelUv);
                    Panel("Crown service access welt",seat+Vector3.down*.033f,new(1.91f,.035f,.96f),enamel);
                    TiledPanel(seat+Vector3.down*.057f,new(1.78f,.026f,.83f),Quaternion.identity,pressedEnamel,PressedPanelUv);
                    Panel("Crown access pull",seat+new Vector3(0,-.084f,.303f),new(.35f,.036f,.031f),steel);
                }
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
                for(int i=0;i<3;i++)
                    TiledPanel(new(x,UpperDeck+.018f,BridgeFront-.9f-1.85f*i),new(3.38f,.033f,1.80f),
                        Quaternion.identity,deckTexture,DeckPlateUv);
                Panel("Foredeck scanner seat",new(side*2.97f,UpperDeck+.26f,BridgeBack+.69f),new(1.72f,.52f,.86f),gunmetal,true);
                Panel("Foredeck status horizon",new(side*2.97f,UpperDeck+.52f,BridgeBack+.69f),new(1.59f,.04f,.73f),cold);
                Panel("Exterior beam finder",new(side*3.68f,UpperDeck+1.99f,BridgeBack-.22f),new(.98f,.25f,.27f),gunmetal);
                Panel("Exterior lens in a dark bezel",new(side*3.68f,UpperDeck+1.99f,BridgeBack-.37f),new(.72f,.12f,.038f),warm);
            }
            float rearSpan=(BridgeHalfWidth*2)/5;
            for(int i=0;i<4;i++)
                Panel("Non-slip bridge walkway",new(0,UpperDeck+.023f,BridgeFront-(i+.5f)*crownPanelLength),
                    new(1.46f,.027f,crownPanelLength-.037f),centerDeckTexture);
            for(int i=0;i<5;i++)Window(new(-BridgeHalfWidth+(i+.5f)*rearSpan,UpperDeck,BridgeBack),rearSpan,Quaternion.identity);
            Window(new(1.20f,UpperDeck,BridgeFront),6.2f,Quaternion.identity);
            Panel("Port portal wall",new(-3.95f,UpperDeck+1.06f,BridgeFront),new(.71f,2.12f,.24f),enamel,true);
            Panel("Incoming stair portal lintel",new(StairX,UpperDeck+2.01f,BridgeFront),new(2.15f,.23f,.28f),ochre,true);
            for(int i=0;i<3;i++)
            {
                float z=BridgeFront-.85f-i*1.73f;
                Panel("Amber command-ring lamp",new(0,UpperDeck+crownHeight-.035f,z),new(3.48f,.082f,.22f),gunmetal);
                Panel("Amber command-ring lens",new(0,UpperDeck+crownHeight-.081f,z),new(2.98f,.028f,.082f),warm);
                Lamp(new(0,UpperDeck+crownHeight-.12f,z),new(1,.69f,.40f),6.4f,105);
            }
            BridgeFinishes();
            BuildBridgePressureFairing();
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
            if(!anchor.TryGetComponent<BoxCollider>(out var face))
                face=anchor.gameObject.AddComponent<BoxCollider>();
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

        void SetProp(string id,Vector3 position,Vector3 size,Quaternion rotation,Material material,bool solid=true,string path=PropFolder)
        {
            var asset=Resources.Load<GameObject>(path+id);
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
            sealed class Surface
            {
                internal readonly ReefGeometry Geometry=new();
                internal readonly List<Vector2> UVs=new();
            }
            readonly Dictionary<Material,Surface> groups=new();
            readonly Transform parent;
            readonly List<Object> owned;

            internal PanelBatch(Transform parent,List<Object> owned)
            {
                this.parent=parent;
                this.owned=owned;
            }

            Surface For(Material material)
            {
                if(!groups.TryGetValue(material,out var surface))groups.Add(material,surface=new Surface());
                return surface;
            }

            internal void Box(Vector3 point,Vector3 size,Material material,Quaternion rotation=default,Rect atlas=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                if(atlas.width<=0)atlas=new Rect(0,0,1,1);
                var surface=For(material);
                Face(surface,point,size,rotation,Vector3.forward,Vector3.right,Vector3.up,atlas);
                Face(surface,point,size,rotation,Vector3.back,Vector3.left,Vector3.up,atlas);
                Face(surface,point,size,rotation,Vector3.right,Vector3.back,Vector3.up,atlas);
                Face(surface,point,size,rotation,Vector3.left,Vector3.forward,Vector3.up,atlas);
                Face(surface,point,size,rotation,Vector3.up,Vector3.right,Vector3.back,atlas);
                Face(surface,point,size,rotation,Vector3.down,Vector3.right,Vector3.forward,atlas);
            }

            internal void Tube(Vector3[] path,float radius,Material material)
            {
                var surface=For(material);
                var widths=new float[path.Length];
                for(int i=0;i<widths.Length;i++)widths[i]=radius;
                surface.Geometry.Tube(path,widths,Color.white,10);
                while(surface.UVs.Count<surface.Geometry.VertexCount)surface.UVs.Add(Vector2.zero);
            }

            static void Face(Surface surface,Vector3 center,Vector3 size,Quaternion rotation,Vector3 normal,Vector3 right,Vector3 up,Rect atlas)
            {
                var c=center+rotation*Vector3.Scale(size*.5f,normal);
                var x=rotation*Vector3.Scale(size*.5f,right);
                var y=rotation*Vector3.Scale(size*.5f,up);
                var mesh=surface.Geometry;
                int a=mesh.Add(c-x-y,Color.white),b=mesh.Add(c+x-y,Color.white),d=mesh.Add(c+x+y,Color.white),e=mesh.Add(c-x+y,Color.white);
                mesh.Quad(a,b,d,e);
                surface.UVs.Add(atlas.min);
                surface.UVs.Add(new Vector2(atlas.xMax,atlas.yMin));
                surface.UVs.Add(atlas.max);
                surface.UVs.Add(new Vector2(atlas.xMin,atlas.yMax));
            }

            internal void Build()
            {
                foreach(var group in groups)
                {
                    var part=Node(parent,group.Key.name,Vector3.zero);
                    var mesh=group.Value.Geometry.Mesh(group.Key.name);
                    mesh.SetUVs(0,group.Value.UVs);
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
