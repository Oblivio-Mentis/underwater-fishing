using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidAirlock : MonoBehaviour
    {
        public static NereidAirlock Active {get;private set;}
        const float OuterX=8.65f, HalfDoor=1.48f, Height=4.15f;
        readonly Transform[,] leaves=new Transform[2,2];
        readonly AirlockPad[] pads=new AirlockPad[4];
        readonly List<Material> owned=new();
        Light chamberKey,chamberFill;
        static readonly Color DryKey=new(1,.78f,.52f), WetKey=new(.21f,.65f,.76f);
        static readonly Color DryFill=new(.42f,.67f,.81f), WetFill=new(.17f,.58f,.67f);
        Material steel, charcoal, trim, paint, amber, cold, signal, floor;
        Transform surface, gauge, impeller;
        NereidPressureDoors authored;
        float Level => authored ? Mathf.Lerp(authored.emptyLevel,authored.fullLevel,water) : Mathf.Lerp(-.12f,Height+.05f,water);
        Mesh pressurePlate;
        PressureTransferFlow transfer;
        AirlockWaterVfx waterVfx;
        TMP_Text display;
        float water,shownInner=1,shownOuter;
        bool receivedDoorPose;
        public float WaterHeight => transform.TransformPoint(Vector3.up*Level).y;
        public AirlockPad GetPad(int index) => index>=0&&index<pads.Length?pads[index]:null;

        public static NereidAirlock Install(Transform hull)
        {
            var doors=hull.GetComponentInChildren<NereidPressureDoors>();
            if(doors) return doors.gameObject.AddComponent<NereidAirlock>();
            var go=new GameObject("Stern pressure lock");
            go.transform.SetPositionAndRotation(hull.TransformPoint(new Vector3(5.95f,0,-14)),hull.rotation);
            go.transform.SetParent(hull,true);
            return go.AddComponent<NereidAirlock>();
        }

        void Awake()
        {
            Active=this;
            steel=Metal("Petrol enamel",new Color(.16f,.23f,.24f),.51f);
            charcoal=Metal("Gasket",new Color(.028f,.055f,.062f),.13f);
            trim=Metal("Brushed sill",new Color(.45f,.54f,.53f),.67f);
            paint=Metal("Safety ochre",new Color(.69f,.39f,.10f),.4f);
            amber=Metal("Pump telltale",new Color(.96f,.53f,.11f),.18f,1.8f);
            cold=Metal("Deck lighting",new Color(.27f,.82f,.84f),.22f,1.5f);
            signal=Metal("Door status",new Color(.34f,.82f,.57f),.22f,1.1f);
            floor=Metal("Flood chamber deck",new Color(.19f,.245f,.249f),.42f);
            NereidSurfaceFinish.Apply(steel,NereidSurfaceFinish.Surface.Enamel);
            NereidSurfaceFinish.Apply(trim,NereidSurfaceFinish.Surface.Ceramic);
            NereidSurfaceFinish.Apply(floor,NereidSurfaceFinish.Surface.Deck);
            authored=GetComponent<NereidPressureDoors>();
            if(authored) authored.Bind();
            BuildChamber();
            BuildGate(false);BuildGate(true);
            if(authored)
            {
                foreach(var pad in authored.controls) pads[pad.Id]=pad;
            }
            else
            {
                BuildPad(0,false,-.39f,-1.92f,-1);
                BuildPad(1,false,.39f,1.92f,1);
                BuildPad(2,true,OuterX-.39f,-1.92f,-1);
                BuildPad(3,true,OuterX+.39f,1.92f,1);
            }
            BuildMarineCollar();
            if(authored) surface=authored.waterSurface;
            else BuildWater();
            if(surface!=null)transfer=PressureTransferFlow.Install(transform,surface.GetComponent<MeshRenderer>(),steel,charcoal,trim);
            waterVfx=AirlockWaterVfx.Install(transform);
            ApplyDoors(AirlockSnapshot.Dry);
        }

        public bool Contains(Vector3 point)
        {
            var p=transform.InverseTransformPoint(point);
            return p.x>=-.05f && p.x<=OuterX+.12f && Mathf.Abs(p.z)<=3.17f && p.y>-.18f && p.y<Height+.08f;
        }
        public bool IsSubmerged(Vector3 point) => point.y<WaterHeight;
        public bool DoorOccupied(bool sea)
        {
            float x=sea?OuterX:0;
            var world=RaidWorld.Instance;
            if(world!=null && world.PetInDoor(transform.InverseTransformPoint(world.Gosha.Position),x))return true;
            var threats = ThreatDirector.Instance;
            if (threats != null && threats.WaveActive)
            {
                var raider = transform.InverseTransformPoint(threats.WavePosition);
                if (Mathf.Abs(raider.x-x) < threats.tuning.raiderBodyRadius + .18f && Mathf.Abs(raider.z) < HalfDoor)
                    return true;
            }
            foreach(var d in DiverMotor.ServerDivers)
            {
                if(d==null||d.IsConsumed)continue;
                var p=transform.InverseTransformPoint(d.transform.position);
                if(Mathf.Abs(p.x-x)<.88f && Mathf.Abs(p.z)<HalfDoor+.5f && p.y>-.8f && p.y<3.9f)return true;
            }
            return false;
        }

        public bool ValidatePress(DiverMotor diver,byte id,Vector3 direction,out AirlockPad pad)
        {
            pad=GetPad(id);
            if(pad==null || float.IsNaN(direction.sqrMagnitude) || Vector3.Dot(diver.AimDirection,direction)<.78f)return false;
            var eye=diver.transform.position+Vector3.up*.6f;
            return Physics.Raycast(eye,direction,out var hit,AirlockPad.Reach,~0,QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponent<AirlockPad>()==pad;
        }

        void BuildChamber()
        {
            var pressedSteel=Metal("Pressed steel inserts",new Color(.32f,.37f,.39f),.27f);
            Lining("Pressure ceiling",new(OuterX*.5f,Height+.12f,0),new(OuterX+.32f,.26f,6.52f),steel,
                Vector3.down,Vector3.right,Vector3.forward);
            Lining("Slotted deck",new(OuterX*.5f,-.055f,0),new(OuterX+.16f,.14f,6.4f),floor,
                Vector3.up,Vector3.right,Vector3.back);
            // Flush threshold inlays pick out the two doors across the matte deck.
            foreach(float x in new[]{.42f,OuterX-.42f})
                Box("Pressure threshold inlay",new Vector3(x,.025f,0),new Vector3(.072f,.014f,2.62f),paint);
            for(int side=-1;side<=1;side+=2)
            {
                Lining("Pressure chamber flank",new(OuterX*.5f,Height*.5f,side*3.2f),new(OuterX+.32f,Height,.22f),
                    steel,Vector3.back*side,Vector3.left*side,Vector3.up);
                for(int bay=0;bay<4;bay++)
                {
                    float center=.92f+bay*2.12f;
                    Box("Bearing panel above waterline",new Vector3(center,2.45f,side*3.019f),
                        new Vector3(1.90f,1.36f,.092f),steel);
                    Box("Machined windowless pressure insert",new Vector3(center,2.47f,side*2.961f),
                        new Vector3(1.73f,1.19f,.052f),pressedSteel);
                    Box("High water witness line",new Vector3(center,3.05f,side*2.929f),
                        new Vector3(1.57f,.031f,.025f),paint);
                }
                Box("Waist rail",new Vector3(OuterX*.5f,1.06f,side*2.97f),new Vector3(OuterX-.48f,.08f,.10f),paint);
                Box("Flood chamber egress line",new Vector3(OuterX*.5f,.038f,side*1.69f),new Vector3(OuterX-.24f,.013f,.052f),cold);
                for(int i=0;i<4;i++)
                {
                    float x=.64f+i*2.43f;
                    Box("Structural frame",new Vector3(x,2.02f,side*3.03f),new Vector3(.18f,4.04f,.19f),trim);
                    Box("Low level lamp",new Vector3(x,.34f,side*2.90f),new Vector3(.35f,.07f,.10f),cold);
                }
            }
            for(int i=0;i<3;i++)
            {
                float x=1.4f+i*2.82f;
                Box("Overhead rib",new Vector3(x,Height-.08f,0),new Vector3(.18f,.22f,6.24f),charcoal);
                Box("Inspection lens housing",new Vector3(x,Height-.20f,0),new Vector3(.32f,.19f,1.78f),charcoal);
                for(int s=-1;s<=1;s+=2)
                    Box("Recessed inspection lens",new Vector3(x,Height-.31f,s*.47f),new Vector3(.147f,.023f,.61f),amber);
            }
            var lum=new GameObject("Amber chamber fill");lum.transform.SetParent(transform,false);lum.transform.localPosition=new Vector3(4.3f,3.25f,0);
            chamberKey=lum.AddComponent<Light>();chamberKey.type=LightType.Point;chamberKey.color=DryKey;chamberKey.intensity=8;chamberKey.range=8;chamberKey.shadows=LightShadows.None;
            var wallLight=new GameObject("Cool reflected lock light").transform;
            wallLight.SetParent(transform,false);
            wallLight.localPosition=new(6.68f,2.81f,-1.43f);
            chamberFill=wallLight.gameObject.AddComponent<Light>();chamberFill.type=LightType.Point;
            chamberFill.color=DryFill;chamberFill.intensity=3.2f;chamberFill.range=5.6f;chamberFill.shadows=LightShadows.None;
            // A measured column and pump on the same wall give the surface a visible reference.
            Box("Level staff",new Vector3(4.35f,2,2.99f),new Vector3(.26f,3.85f,.1f),charcoal);
            for(int i=0;i<9;i++)Box("Staff graduation",new Vector3(4.36f,.21f+i*.44f,2.932f),new Vector3(i%2==0?.18f:.10f,.024f,.018f),trim);
            gauge=Box("Water float",new Vector3(4.34f,.2f,2.9f),new Vector3(.32f,.08f,.04f),cold);
            Box("Pump indicator plate",new Vector3(5.3f,2,3.04f),new Vector3(1.55f,.63f,.09f),charcoal);
            display=Label("Pump readout",new Vector3(5.3f,2,2.974f),Vector3.back,"ОСУШЕН",1.44f,.42f,.17f);
            Box("Pump strainer",new Vector3(2.4f,.38f,2.96f),new Vector3(1.45f,.57f,.16f),charcoal);
            for(int i=0;i<11;i++)Box("Strainer fin",new Vector3(1.8f+i*.12f,.38f,2.85f),new Vector3(.041f,.48f,.042f),trim);
            impeller=Box("Pump rotor",new Vector3(2.4f,.38f,2.84f),new Vector3(.5f,.07f,.035f),paint);
        }

        void BuildGate(bool outer)
        {
            float x=outer?OuterX:0;int gate=outer?1:0;
            for(int s=-1;s<=1;s+=2)
            {
                var point=new Vector3(x,Height*.5f,s*2.37f);
                var size=new Vector3(.32f,Height,1.8f);
                if(outer)Lining("Pressure bulkhead",point,size,steel,Vector3.left,Vector3.forward,Vector3.up);
                else Box("Pressure bulkhead",point,size,steel,true);
                Box("Door track",new Vector3(x,1.65f,s*1.55f),new Vector3(.4f,3.3f,.14f),charcoal);
            }
            var header=new Vector3(x,(Height+3.22f)*.5f,0);
            var span=new Vector3(.34f,Height-3.22f,3.12f);
            if(outer)Lining("Sealed header",header,span,steel,Vector3.left,Vector3.forward,Vector3.up);
            else Box("Sealed header",header,span,steel,true);
            if(!outer)Box("Hatch crown seal",new Vector3(x,5.18f,0),new Vector3(.36f,2.1f,6.56f),steel,true);
            if(authored)return;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                var leaf=new GameObject(outer?"Ocean door leaf":"Cabin door leaf").transform;leaf.SetParent(transform,false);
                leaves[gate,side]=leaf;
                var col=leaf.gameObject.AddComponent<BoxCollider>();col.size=new Vector3(.245f,3.22f,HalfDoor);
                Box("Stamped pressure leaf",Vector3.zero,new Vector3(.22f,3.21f,HalfDoor),steel,false,leaf);
                Box("Edge compression seal",new Vector3(0,0,-sign*(HalfDoor*.5f-.036f)),new Vector3(.25f,3.18f,.07f),charcoal,false,leaf);
                for(int face=-1;face<=1;face+=2)
                {
                    float skin=face*.128f;
                    DressLeaf(leaf,face,sign);
                    Box("Door instrumentation bezel",new Vector3(face*.174f,.66f,0),new Vector3(.035f,.22f,.77f),charcoal,false,leaf);
                    Box("Door status slit",new Vector3(face*.195f,.685f,0),new Vector3(.018f,.056f,.54f),signal,false,leaf);
                    Box("Door caution band",new Vector3(skin,-1.13f,0),new Vector3(.045f,.24f,1.35f),paint,false,leaf);
                    for(int k=-1;k<=1;k++)
                    {
                        var stripe=Box("Band slash",new Vector3(face*.156f,-1.13f,k*.37f),new Vector3(.018f,.235f,.10f),charcoal,false,leaf);
                        stripe.localRotation=Quaternion.Euler(33,0,0);
                    }
                }
            }
        }

        void DressLeaf(Transform leaf,int face,float openingSide)
        {
            if(pressurePlate==null)
            {
                var shape=new ReefGeometry();
                Vector2[] outline={
                    new(-.62f,-.61f),new(-.47f,-.90f),new(.47f,-.90f),new(.62f,-.61f),
                    new(.62f,.74f),new(.40f,1.03f),new(-.40f,1.03f),new(-.62f,.74f)
                };
                int center=shape.Add(new Vector3(.182f,.04f,0),Color.white);
                for(int i=0;i<outline.Length;i++)
                {
                    Vector2 a=outline[i], b=outline[(i+1)%outline.Length];
                    var outerA=shape.Add(new Vector3(.114f,a.y,a.x),Color.white);
                    var outerB=shape.Add(new Vector3(.114f,b.y,b.x),Color.white);
                    var innerA=shape.Add(new Vector3(.182f,a.y*.91f,a.x*.89f),Color.white);
                    var innerB=shape.Add(new Vector3(.182f,b.y*.91f,b.x*.89f),Color.white);
                    shape.Quad(outerA,innerA,innerB,outerB);
                    shape.Face(center,innerB,innerA);
                }
                pressurePlate=shape.Mesh("Chamfered pressure leaf plate");
            }
            var assembly=new GameObject("Bevelled pressure armour");
            assembly.transform.SetParent(leaf,false);
            assembly.transform.localRotation=face>0?Quaternion.identity:Quaternion.Euler(0,180,0);
            assembly.AddComponent<MeshFilter>().sharedMesh=pressurePlate;
            assembly.AddComponent<MeshRenderer>().sharedMaterial=trim;
            Box("Edge clamp",new Vector3(face*.14f,.24f,openingSide*.61f),
                new Vector3(.080f,1.39f,.11f),charcoal,false,leaf);
            for(int i=0;i<3;i++)
                Box("Locking lug",new Vector3(face*.19f,-.22f+i*.46f,openingSide*.62f),
                    new Vector3(.046f,.13f,.15f),paint,false,leaf);
        }

        void BuildPad(byte id,bool sea,float x,float z,int side)
        {
            var at=new Vector3(x,1.53f,z);
            Box("Control housing",at,new Vector3(.16f,.85f,.76f),charcoal);
            var face=Box("Pressure lock control "+id,at+Vector3.right*side*.11f,new Vector3(.075f,.73f,.66f),steel,true);
            var pad=face.gameObject.AddComponent<AirlockPad>();pad.Id=id;pad.Sea=sea;pads[id]=pad;
            Box("Illuminated push plate",at+new Vector3(side*.153f,-.10f,0),new Vector3(.012f,.27f,.44f),sea?cold:amber);
            Vector3 normal=Vector3.right*side;
            Label("Cycle direction",at+new Vector3(side*.16f,.19f,0),normal,sea?"МОРЕ":"БОРТ",.58f,.23f,.13f);
            Label("Gate identification",new Vector3(x,3.74f,0)+normal*.05f,normal,sea?"02   /   МОРЕ":"01   /   БОРТ",2.5f,.45f,.22f);
        }

        void BuildWater()
        {
            var waterShader=Resources.Load<Shader>("PressureLockWater");
            if(waterShader==null)return;
            var mat=new Material(waterShader){name="Flood chamber water"};owned.Add(mat);
            surface=Box("Filling water surface",new Vector3(OuterX*.5f,0,0),new Vector3(OuterX-.12f,.012f,6.17f),mat);
            surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        void Update()
        {
            var w=RaidWorld.Instance;
            if(w==null || w.NetworkObject==null || (!w.IsClientInitialized&&!w.IsServerInitialized))
            {receivedDoorPose=false;return;}
            var s=w.Airlock;
            if(!w.IsServerInitialized && (!receivedDoorPose || (s.Outer>0 && shownInner>.001f) || (s.Inner>0 && shownOuter>.001f)))
            {
                // Late boarding and reconnect restore one coherent seal/water state before interpolating.
                ApplyDoors(s);
                receivedDoorPose=true;
            }
            else if(!w.IsServerInitialized)
            {
                float step=Time.deltaTime*1.2f;
                shownInner=Mathf.MoveTowards(shownInner,s.Inner,step);shownOuter=Mathf.MoveTowards(shownOuter,s.Outer,step);
                PositionLeaves(0,shownInner);PositionLeaves(1,shownOuter);
            }
            water=w.IsServerInitialized?s.Water:Mathf.MoveTowards(water,s.Water,Time.deltaTime*.35f);
            if(surface!=null)
            {
                surface.gameObject.SetActive(water>.006f && water<.997f);
                var position=surface.localPosition;
                position.y=Level;
                surface.localPosition=position;
            }
            chamberKey.color=Color.Lerp(DryKey,WetKey,water);
            chamberFill.color=Color.Lerp(DryFill,WetFill,water);
            chamberFill.intensity=Mathf.Lerp(3.2f,7.8f,water);
            gauge.localPosition=new Vector3(4.34f,Mathf.Lerp(.19f,3.8f,water),2.9f);
            bool running=s.Phase==AirlockPhase.Flooding||s.Phase==AirlockPhase.Draining;
            if(transfer!=null)transfer.Present(s.Phase,surface.localPosition.y);
            if(waterVfx!=null)waterVfx.Present(s.Phase,Level);
            if(running)impeller.Rotate(0,0,Time.deltaTime*240);
            if(display!=null)
            {
                int percent=Mathf.RoundToInt(water*100);
                display.SetText("{0:0} %",percent);
                display.color=running?new Color(1,.72f,.26f):new Color(.48f,.94f,.84f);
            }
        }
        public void ApplyDoors(AirlockSnapshot state)
        {
            shownInner=state.Inner;shownOuter=state.Outer;water=state.Water;
            PositionLeaves(0,shownInner);PositionLeaves(1,shownOuter);
        }
        void PositionLeaves(int gate,float open)
        {
            if(authored){authored.MoveGate(gate,open);return;}
            for(int i=0;i<2;i++)leaves[gate,i].localPosition=new Vector3(gate==0?0:OuterX,1.61f,(i==0?-1:1)*(HalfDoor*.5f+open*(HalfDoor+.11f)));
        }
        Transform Box(string label,Vector3 at,Vector3 scale,Material mat,bool solid=false,Transform parent=null)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=label;var t=obj.transform;t.SetParent(parent==null?transform:parent,false);
            t.localPosition=at;t.localScale=scale;obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(!solid){var col=obj.GetComponent<Collider>();col.enabled=false;Destroy(col);}
            return t;
        }
        TMP_Text Label(string name,Vector3 at,Vector3 normal,string caption,float width,float height,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=at;go.transform.localRotation=Quaternion.LookRotation(-normal,Vector3.up);
            var text=go.AddComponent<TextMeshPro>();text.rectTransform.sizeDelta=new Vector2(width,height);
            text.fontSize=size*10;text.alignment=TextAlignmentOptions.Center;text.text=caption;text.color=new Color(.85f,.9f,.86f);text.textWrappingMode=TextWrappingModes.NoWrap;
            text.raycastTarget=false;return text;
        }
        Material Metal(string name,Color color,float smooth,float glow=0)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);
            if(glow>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*glow);}
            owned.Add(mat);return mat;
        }
        void OnDestroy()
        {
            if(Active==this)Active=null;
            if(pressurePlate!=null)Destroy(pressurePlate);
            foreach(var mat in owned)if(mat!=null)Destroy(mat);
            foreach(var mesh in fairingMeshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
