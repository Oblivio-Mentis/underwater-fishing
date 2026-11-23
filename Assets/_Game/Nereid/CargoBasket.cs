using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Dimensions are in world metres. The scene's basket root cancels its parent's nonuniform scale.
    [RequireComponent(typeof(CargoLandingSilt))]
    public sealed class CargoBasket : MonoBehaviour
    {
        internal const float Width=3.40f, Depth=3.38f;
        internal const float Floor=-.96f;
        const float Side=Width*.5f, End=Depth*.5f, Rim=1.14f;
        [field: SerializeField] public Transform CableSocket { get; private set; }
        [SerializeField] CargoDispatch dispatch;
        [SerializeField] Transform[] lids=new Transform[2], trapdoors=new Transform[2];
        [SerializeField] MeshRenderer[] meter=new MeshRenderer[24];
        [SerializeField] List<Collider> touching=new();
        readonly List<Mesh> combinedMeshes=new();
        [SerializeField] Transform loadingGate,shockRail,buttonStem;
        [SerializeField] MeshRenderer buttonLens;
        Material steel,frame,ochre,signal,fascia;
        MaterialPropertyBlock lights;
        [SerializeField] TMP_Text meterText;
        Mesh cube;
        int previousUsed=-1,previousLimit=-1;
        float buttonDepth;
        bool ownsMaterials;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"), Emission=Shader.PropertyToID("_EmissionColor");
        static readonly Color Green=new(.20f,.91f,.56f), Amber=new(1f,.73f,.13f), Red=new(1f,.19f,.10f);

        internal static CargoBasket Install(RaidWorld world)
        {
            var authored=world.container.GetComponentInChildren<CargoBasket>();
            if(authored)
            {
                if(authored.dispatch) authored.dispatch.Initialize(world,authored.transform);
                authored.lights??=new MaterialPropertyBlock();
                return authored;
            }
            var basket=world.container.Find("RAID010 cargo basket");
            if(basket==null)return null;
            var existing=basket.GetComponent<CargoBasket>();
            if(existing!=null)return existing;
            var floor=basket.Find("Basket base");
            if(floor==null)return null;
            var result=basket.gameObject.AddComponent<CargoBasket>();
            result.Build(world,floor);
            return result;
        }

#if UNITY_EDITOR
        public static CargoBasket Bake(Transform parent,Transform oldFloor,Material steel,Material enamel,Material signal,Material fascia)
        {
            var component=parent.gameObject.AddComponent<CargoBasket>();
            component.steel=steel;
            component.ochre=enamel;
            component.signal=signal;
            component.fascia=fascia;
            component.Build(null,oldFloor);
            return component;
        }
#endif

        void Build(RaidWorld world,Transform oldFloor)
        {
            cube=oldFloor.GetComponent<MeshFilter>().sharedMesh;
            frame=oldFloor.GetComponent<MeshRenderer>().sharedMaterial;
            if(world) steel=transform.Find("Basket rim").GetComponent<MeshRenderer>().sharedMaterial;
            foreach(Transform part in transform)part.gameObject.SetActive(false);
            if(world)
            {
                var legacy=world.container.GetComponent<Collider>();if(legacy!=null)legacy.enabled=false;
                ownsMaterials=true;
                ochre=Finish("Recovery cage · enamel",new Color(.78f,.36f,.09f),.38f,false);
                var instrumentShader=Resources.Load<Shader>("CargoInstrument");
                signal=new Material(instrumentShader){name="Recovery cage illuminated indicator"};
                fascia=new Material(instrumentShader){name="Recovery cage matte instrument face"};
                fascia.SetColor(BaseColor,new Color(.016f,.032f,.048f));
            }
            lights=new MaterialPropertyBlock();

            for(int s=-1;s<=1;s+=2)
            {
                Box(transform,"Base rail",new Vector3(s*Side,Floor,0),new Vector3(.16f,.23f,Depth+.21f),steel);
                Box(transform,"Base cross rail",new Vector3(0,Floor,s*End),new Vector3(Width,.23f,.16f),steel);
                Box(transform,"Roof brace",new Vector3(0,Rim,s*End),new Vector3(Width+.12f,.15f,.17f),steel);
                Box(transform,"Lifting shoe",new Vector3(0,Rim+.15f,s*(End-.18f)),new Vector3(.56f,.2f,.26f),ochre);
                Box(transform,"Gantry pickup",new Vector3(0,Rim+.33f,s*(End-.18f)),new Vector3(.28f,.19f,.16f),steel);
                Panel(transform,new Vector3(0,0,s*End),false);
                for(int x=-1;x<=1;x+=2)
                {
                    var corner=new Vector3(x*Side,.09f,s*End);
                    Box(transform,"Corner pressure rib",corner,new Vector3(.17f,2.36f,.17f),steel);
                    Box(transform,"Shock absorbing corner jacket",corner+Vector3.up*.83f,new Vector3(.265f,.38f,.27f),ochre);
                    Box(transform,"Shock absorbing landing foot",corner+Vector3.down*1.06f,new Vector3(.31f,.24f,.32f),frame);
                }
            }
            Box(transform,"Main lifting yoke",new Vector3(0,Rim+.41f,0),new Vector3(.20f,.17f,Depth-.15f),steel);
            CableSocket=Pivot(transform,"Cable termination",new Vector3(0,Rim+.49f,0));
            Box(transform,"Guide block",CableSocket.localPosition,new Vector3(.49f,.30f,.44f),ochre);
            Panel(transform,new Vector3(-Side,0,0),true);
            loadingGate=Pivot(transform,"Guided loading shutter",new Vector3(Side,0,0));
            Panel(loadingGate,Vector3.zero,true,false);
            shockRail=Box(transform,"Safety edge",new Vector3(Side,.99f,0),new Vector3(.15f,.13f,Depth-.30f),ochre).transform;

            for(int i=0;i<2;i++)
            {
                int sign=i==0?-1:1;
                lids[i]=Pivot(transform,"Vented lid spindle",new Vector3(sign*Side,Rim+.04f,0));
                trapdoors[i]=Pivot(transform,"Discharge leaf spindle",new Vector3(sign*Side,Floor+.10f,0));
                HorizontalGrid(lids[i],-sign,true);
                HorizontalGrid(trapdoors[i],-sign,false);
                var hatch=Box(trapdoors[i],"Bottom leaf solid support",new Vector3(-sign*Side*.5f,0,0),new Vector3(Side,.095f,Depth-.19f),frame);
                touching.Add(hatch.gameObject.AddComponent<BoxCollider>());
            }

            var panel=Pivot(transform,"Physical dispatch console",new Vector3(Side+.105f,.67f,.93f));
            Box(panel,"Recessed control housing",Vector3.zero,new Vector3(.25f,.54f,1.29f),frame);
            Box(panel,"Contrasting control fascia",new Vector3(.127f,.012f,0),new Vector3(.027f,.45f,1.19f),fascia,false);
            Box(panel,"Padded push collar",new Vector3(.155f,-.02f,.43f),new Vector3(.115f,.32f,.30f),ochre);
            buttonStem=Pivot(panel,"Dispatch push rod",new Vector3(.215f,-.02f,.43f));
            var button=Box(buttonStem,"Physical cargo dispatch button",Vector3.zero,new Vector3(.09f,.188f,.188f),signal,false);
            buttonLens=button;
            dispatch=button.gameObject.AddComponent<CargoDispatch>();
            dispatch.Initialize(world,transform);
            button.gameObject.AddComponent<BoxCollider>();
            Box(panel,"Capacity bar recess",new Vector3(.151f,-.107f,-.20f),new Vector3(.025f,.110f,.72f),fascia,false);
            for(int i=0;i<meter.Length;i++)
                meter[i]=Box(panel,"Capacity cell",new Vector3(.170f,-.107f,.142f-i*.0298f),new Vector3(.022f,.063f,.022f),signal,false);
            var font=Raid.UI.UiRoot.Instance?.font;
            if(font==null)font=TMP_Settings.defaultFontAsset;
            if(font!=null)
            {
                meterText=Label(panel,font,"Capacity readout",new Vector3(.169f,.097f,-.20f),.155f,new Vector2(.74f,.16f));
                var label=Label(panel,font,"Button engraving",new Vector3(.169f,.177f,.405f),.080f,new Vector2(.29f,.09f));
                label.text="ПОДЪЁМ";
            }

            if(world)
            {
                CombineShell(transform);
                CombineShell(loadingGate);
                foreach(var lid in lids)CombineShell(lid);
                foreach(var door in trapdoors)CombineShell(door);
            }
            var lamp=Pivot(transform,"Cage work lights",new Vector3(0,Rim-.26f,-End+.3f)).gameObject.AddComponent<Light>();
            lamp.transform.rotation=Quaternion.LookRotation(new Vector3(0,-.65f,.78f));lamp.type=LightType.Spot;
            lamp.color=new Color(.45f,.83f,1f);lamp.intensity=4.6f;lamp.range=5.5f;lamp.spotAngle=110;lamp.shadows=LightShadows.None;
        }

        void CombineShell(Transform root)
        {
            var finishes=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var part=renderer.transform;
                if(renderer.sharedMaterial==signal || part==shockRail || part==buttonStem || part.IsChildOf(buttonStem) ||
                    MovesIndependently(root,part))continue;
                if(!renderer.TryGetComponent<MeshFilter>(out var source))continue;
                if(!finishes.TryGetValue(renderer.sharedMaterial,out var group))
                    finishes.Add(renderer.sharedMaterial,group=new List<CombineInstance>());
                group.Add(new CombineInstance {mesh=source.sharedMesh,transform=root.worldToLocalMatrix*part.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var finish in finishes)
            {
                var joined=new Mesh {name="Combined recovery cage steelwork"};
                joined.CombineMeshes(finish.Value.ToArray());
                combinedMeshes.Add(joined);
                var part=Box(root,joined.name,Vector3.zero,Vector3.one,finish.Key);
                part.GetComponent<MeshFilter>().sharedMesh=joined;
            }
        }

        bool MovesIndependently(Transform root,Transform part)
        {
            if(root!=loadingGate && part.IsChildOf(loadingGate))return true;
            for(int i=0;i<2;i++)
                if(root!=lids[i] && part.IsChildOf(lids[i]) || root!=trapdoors[i] && part.IsChildOf(trapdoors[i]))return true;
            return false;
        }

        TMP_Text Label(Transform parent,TMP_FontAsset font,string name,Vector3 at,float size,Vector2 rect)
        {
            var label=new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent,false);label.transform.localPosition=at;
            label.transform.localRotation=Quaternion.LookRotation(Vector3.left);
            label.rectTransform.sizeDelta=rect;label.alignment=TextAlignmentOptions.Center;label.font=font;
            label.isOrthographic=false;label.fontSize=size*10;label.color=new Color(.84f,.91f,.94f);
            return label;
        }

        // Fixed walls use thin colliders while the loading apron remains open.
        void Panel(Transform parent,Vector3 center,bool alongZ,bool physical=true)
        {
            var length=alongZ?Depth:Width;
            var skin=Pivot(parent,"Framed retention grille",center);
            if(alongZ)skin.localRotation=Quaternion.Euler(0,90,0);
            for(int i=1;i<13;i++)
            {
                float x=(i/13f-.5f)*length;
                Box(skin,"Welded grille upright",new Vector3(x,.10f,0),new Vector3(.041f,2.0f,.05f),steel);
            }
            for(int i=0;i<5;i++)
                Box(skin,"Welded grille crosswire",new Vector3(0,Floor+.31f+i*.35f,0),new Vector3(length-.12f,.034f,.042f),frame);
            Box(parent,"Frame tie",center+Vector3.up*.33f,alongZ?new Vector3(.095f,.11f,length):new Vector3(length,.11f,.095f),ochre);
            if(physical)
            {
                var stop=skin.gameObject.AddComponent<BoxCollider>();
                stop.center=Vector3.up*.05f;stop.size=new Vector3(length-.06f,1.93f,.08f);touching.Add(stop);
            }
        }

        void HorizontalGrid(Transform parent,int lengthSign,bool top)
        {
            var mat=top?steel:frame;
            for(int i=1;i<=6;i++)
                Box(parent,"Vented panel stave",new Vector3(lengthSign*i*Side/6,0,0),new Vector3(.055f,.08f,Depth-.22f),mat);
            for(int i=-2;i<=2;i++)
                Box(parent,"Gate cross brace",new Vector3(lengthSign*Side*.51f,0,i*(End-.12f)/2),new Vector3(Side,.079f,.07f),i==0?ochre:mat);
        }

        Material Finish(string name,Color color,float smooth,bool emit)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,hideFlags=HideFlags.DontSave};
            material.SetColor(BaseColor,color);material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",.45f);
            if(emit){material.EnableKeyword("_EMISSION");material.SetColor(Emission,color*.5f);}
            return material;
        }

        static Transform Pivot(Transform parent,string name,Vector3 at)
        {
            var pivot=new GameObject(name).transform;
            pivot.SetParent(parent,false);pivot.localPosition=at;
            return pivot;
        }

        MeshRenderer Box(Transform parent,string name,Vector3 at,Vector3 size,Material material,bool cast=true)
        {
            var part=Pivot(parent,name,at);
            part.localScale=size;part.gameObject.AddComponent<MeshFilter>().sharedMesh=cube;
            var mesh=part.gameObject.AddComponent<MeshRenderer>();
            mesh.sharedMaterial=material;if(!cast)mesh.shadowCastingMode=ShadowCastingMode.Off;
            return mesh;
        }

        public void PoseMechanism(HoistPhase phase,float progress)
        {
            float closure=phase switch {
                HoistPhase.Loading=>0,
                HoistPhase.Raising=>Ease(Mathf.InverseLerp(0,.145f,progress)),
                HoistPhase.Returning=>1-Ease(Mathf.InverseLerp(.87f,1,progress)),
                _=>1
            };
            float dockOpen=phase==HoistPhase.Unloading
                ? Ease(Mathf.InverseLerp(.12f,.32f,progress))*(1-Ease(Mathf.InverseLerp(.83f,1,progress))) : 0;
            loadingGate.localPosition=new Vector3(Side,2.02f*(1-closure),0);
            shockRail.localPosition=new Vector3(Side,1.02f-.17f*closure,0);
            for(int i=0;i<2;i++)
            {
                float handed=i==0?1:-1;
                lids[i].localRotation=Quaternion.Euler(0,0,handed*62*(1-closure));
                trapdoors[i].localRotation=Quaternion.Euler(0,0,-handed*71*dockOpen);
            }
        }

        internal void Present(TeamProgress state,float progress)
        {
            bool atField=state.hoistPhase==HoistPhase.Loading;
            PoseMechanism(state.hoistPhase,progress);
            foreach(var part in touching)if(part.enabled!=atField)part.enabled=atField;
            float pressDepth=state.hoistPhase==HoistPhase.Raising?.092f*(1-Ease(progress/.075f)):0;
            buttonDepth=Mathf.Lerp(buttonDepth,pressDepth,1-Mathf.Exp(-Time.deltaTime*18));
            buttonStem.localPosition=new Vector3(.215f-buttonDepth,-.02f,.43f);
            Tint(buttonLens,atField?state.CargoUsed>0?Amber:Green:new Color(.14f,.28f,.33f),atField?.96f:.59f);
        }

        internal void Meter(int used,int limit)
        {
            if(used==previousUsed && limit==previousLimit)return;
            previousUsed=used;previousLimit=limit;
            float fraction=Mathf.Clamp01((float)used/Mathf.Max(1,limit));
            Color color=fraction<=.58f?Color.Lerp(Green,Amber,fraction/.58f):Color.Lerp(Amber,Red,(fraction-.58f)/.42f);
            int amount=Mathf.Clamp(Mathf.CeilToInt(fraction*meter.Length),1,meter.Length);
            for(int i=0;i<meter.Length;i++)Tint(meter[i],color,i<amount?.90f:.18f);
            if(meterText!=null)meterText.text=used+" / "+limit;
        }

        void Tint(Renderer renderer,Color color,float power)
        {
            lights.Clear();lights.SetColor(BaseColor,color*power);
            renderer.SetPropertyBlock(lights);
        }

        internal static float Ease(float t) { t=Mathf.Clamp01(t);return t*t*(3-2*t); }

        void OnDestroy()
        {
            foreach(var mesh in combinedMeshes)if(mesh!=null)Destroy(mesh);
            if(!ownsMaterials)return;
            if(ochre!=null)Destroy(ochre);if(signal!=null)Destroy(signal);if(fascia!=null)Destroy(fascia);
        }
    }
}
