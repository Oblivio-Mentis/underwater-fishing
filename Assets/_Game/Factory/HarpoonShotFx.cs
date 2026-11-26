using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Raid
{
    /// <summary>Presents accepted shots and confirmed hooks on the local camera or an observed diver. Owns no hit, rope physics or gameplay commands.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(300)]
    public sealed class HarpoonShotFx : MonoBehaviour
    {
        public enum CablePhase { Idle, Flying, AwaitingAttachment, Attached, Retracting }
        [Min(.02f)] public float minimumFlightSeconds=.08f;
        [Min(.03f)] public float maximumFlightSeconds=.16f;
        [Min(.02f)] public float attachmentWaitSeconds=.18f;
        [Min(.02f)] public float retrievalSeconds=.24f;
        [Min(.001f)] public float cableWidth=.014f;
        public CablePhase Phase {get;private set;}
        public int AcceptedShotCount {get;private set;}
        public float SpoolDegrees {get;private set;}
        public bool HasConfirmedAttachment => attached!=null;
        public Vector3 VisualTip => tip;
        public Vector3 BoltPoint => flightBolt.TransformPoint(BoltTipLocal);
        public Vector3 LineKnot => flightBolt.TransformPoint(BoltNockLocal);
        public Vector3 CableEnd => cable.GetPosition(Segments-1);
        public Vector3 ServerShotEndpoint {get;private set;}
        public float ShotAge => Time.unscaledTime-shotAt;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int VerifiedFrames { get; private set; }
        public float AnchorTravel { get; private set; }
        public float MaxTipError { get; private set; }
        public float MaxKnotError { get; private set; }
        public int VerifiedShot { get; private set; }
        public int VerifiedOwner { get; private set; }
        Vector3 firstAnchor;
        void VerifyAnchor(Vector3 contact,Vector3 normal)
        {
            if(VerifiedFrames==0)firstAnchor=contact;
            VerifiedOwner=tool.OwnerId;VerifiedShot=tool.ShotSequence;VerifiedFrames++;
            AnchorTravel=Mathf.Max(AnchorTravel,Vector3.Distance(contact,firstAnchor));
            MaxTipError=Mathf.Max(MaxTipError,Vector3.Distance(BoltPoint,contact-normal*.008f));
            MaxKnotError=Mathf.Max(MaxKnotError,Vector3.Distance(CableEnd,LineKnot));
        }
#endif

        const int Segments=25;
        Vector3 BoltTipLocal=new Vector3(0,.025f,.48f);
        Vector3 BoltNockLocal=new Vector3(0,.025f,.19f);
        readonly Vector3[] points=new Vector3[Segments];
        HarpoonTool tool;
        bool worldView;
        Transform muzzle,loadedBolt,reel,flightBolt,attached;
        Vector3 flightStart,tip,retractStart;
        HarpoonContact inFlightContact;
        Quaternion flightRotation;
        LineRenderer cable,legacy;
        Material material;
        Texture2D braid;
        float shotAt=float.NegativeInfinity,flightSeconds,phaseAt,lastLength;

        public void Initialize(HarpoonTool source,Transform muzzleAnchor,Transform template,Transform spool,Material fallback,bool worldView=false)
        {
            this.worldView=worldView;muzzle=muzzleAnchor;loadedBolt=template;reel=spool;
            var rig=template.GetComponentInParent<HarpoonVisualRig>(true);
            if (rig) { BoltTipLocal=rig.boltTipLocal; BoltNockLocal=rig.boltNockLocal; }
            flightRotation=muzzle.rotation;
            flightBolt=Instantiate(template,transform,false);flightBolt.name="Accepted-shot flying bolt";flightBolt.gameObject.SetActive(false);
            var go=new GameObject("Braided harpoon cable");go.transform.SetParent(transform,false);cable=go.AddComponent<LineRenderer>();
            cable.useWorldSpace=true;cable.positionCount=Segments;cable.startWidth=cable.endWidth=cableWidth;cable.numCornerVertices=2;cable.numCapVertices=2;
            cable.textureMode=LineTextureMode.Tile;cable.shadowCastingMode=ShadowCastingMode.Off;cable.receiveShadows=false;
            var sourceMaterial=source.ropeMaterial!=null?source.ropeMaterial:fallback;
            material=new Material(sourceMaterial);material.name="Owned harpoon braided cable";
            braid=new Texture2D(32,8,TextureFormat.RGBA32,true);braid.name="NEREID woven cable";braid.wrapMode=TextureWrapMode.Repeat;
            for(int x=0;x<32;x++)for(int y=0;y<8;y++)
            {float light=((x+y*2)%8)<4?.88f:.53f;braid.SetPixel(x,y,new Color(light,light,light,1));}
            braid.Apply();
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",braid);
            else if(material.HasProperty("_MainTex"))material.mainTexture=braid;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(.72f,.66f,.45f));
            cable.sharedMaterial=material;cable.enabled=false;
            BindSource(source);
        }

        // A spectator uses their local camera rig with the observed diver's networked tool.
        public void BindSource(HarpoonTool source)
        {
            if(tool==source)return;
            if(tool!=null)tool.ShotVisual-=OnShot;
            Clear();tool=source;legacy=null;
            shotAt=float.NegativeInfinity;SpoolDegrees=0;AcceptedShotCount=0;
            if(reel!=null)reel.localRotation=Quaternion.identity;
            if(tool==null)return;
            foreach(var line in tool.GetComponentsInChildren<LineRenderer>(true))
                if(line.name=="Harpoon line"){legacy=line;break;}
            tool.ShotVisual+=OnShot;
        }

        bool CanDraw()
        {
            if(tool==null||tool.NetworkObject==null||!tool.IsClientInitialized||tool.Motor==null||tool.Motor.IsDead)return false;
            var local=UI.UiRoot.Instance==null?null:UI.UiRoot.Instance.Context.Player;
            if(worldView)return !tool.IsOwner && local!=null && local.Spectating!=tool.Motor;
            return tool.IsOwner||local!=null&&local.Spectating==tool.Motor;
        }

        void OnShot(Vector3 origin,Vector3 endpoint)
        {
            if(!CanDraw())return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VerifiedFrames=0;AnchorTravel=MaxTipError=MaxKnotError=0;
#endif
            inFlightContact=tool.LastShotContact;
            AcceptedShotCount++;ServerShotEndpoint=endpoint;shotAt=phaseAt=Time.unscaledTime;
            flightStart=loadedBolt.gameObject.activeInHierarchy?loadedBolt.TransformPoint(BoltTipLocal):MuzzlePoint();
            if(Vector3.Distance(flightStart,origin)>2)flightStart=origin;
            var serverDirection=endpoint-origin;
            if(serverDirection.sqrMagnitude>.0001f&&Vector3.Dot(flightStart-origin,serverDirection.normalized)>=serverDirection.magnitude-.05f)
                flightStart=Vector3.Lerp(origin,endpoint,.2f); // Close contact lies before the long camera viewmodel's muzzle.
            tip=flightStart;
            var direction=endpoint-flightStart;flightRotation=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):muzzle.rotation;
            flightSeconds=Mathf.Lerp(minimumFlightSeconds,maximumFlightSeconds,Mathf.Clamp01(direction.magnitude/Mathf.Max(1,tool.tuning.maxRange)));
            attached=null;Phase=CablePhase.Flying;loadedBolt.gameObject.SetActive(false);flightBolt.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if(!CanDraw()){if(Phase!=CablePhase.Idle)Clear();return;}
            if(legacy!=null)legacy.enabled=false;
            float now=Time.unscaledTime,dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            var hit=tool.AttachedContact;
            bool confirmed=hit.Resolve(out var target,out var targetPoint,out var targetNormal,out var targetDirection)
                && (tool.OwnsFriendTether || tool.HasBallLine || tool.HookedFish!=null && tool.HookedFish.Mode>=FishMode.Hooked && tool.HookedFish.Mode<=FishMode.Tired);
            if(confirmed)
            {
                attached=target;
                if(Phase!=CablePhase.Flying)
                {
                    Phase=CablePhase.Attached;
                    loadedBolt.gameObject.SetActive(false);
                }
            }
            else if(attached!=null || Phase==CablePhase.Attached)
            {
                attached=null;Retract(now);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool settled=confirmed && Phase==CablePhase.Attached;
#endif
            if(Phase==CablePhase.Flying)
            {
                float t=Mathf.Clamp01((now-shotAt)/Mathf.Max(.01f,flightSeconds));
                var end=ServerShotEndpoint;
                if(confirmed)end=targetPoint;
                else if(inFlightContact.Resolve(out _,out var movingPoint,out _,out _))end=movingPoint;
                tip=Vector3.Lerp(flightStart,end,1-(1-t)*(1-t));
                var path=end-flightStart;
                if(path.sqrMagnitude>.0001f)flightRotation=Quaternion.LookRotation(path);
                SetBolt(tip,flightRotation);
                if(t>=1){Phase=attached!=null?CablePhase.Attached:CablePhase.AwaitingAttachment;phaseAt=now;}
            }
            else if(Phase==CablePhase.AwaitingAttachment)
            {
                SetBolt(tip,flightRotation);
                if(now-phaseAt>=attachmentWaitSeconds)Retract(now);
            }
            else if(Phase==CablePhase.Attached&&attached!=null)
            {
                tip=targetPoint;
                flightRotation=Quaternion.LookRotation(targetDirection);
                // Sink only the barb into the skin. The eye and cable stay on the exposed shaft.
                SetBolt(tip-targetNormal*.008f,flightRotation);
            }
            else if(Phase==CablePhase.Retracting)
            {
                float t=Mathf.Clamp01((now-phaseAt)/Mathf.Max(.01f,retrievalSeconds));
                tip=Vector3.Lerp(retractStart,MuzzlePoint(),t*t*(3-2*t));
                var direction=MuzzlePoint()-tip;
                SetBolt(tip,direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):flightRotation);
                if(t>=1)Clear();
            }

            bool visible=Phase!=CablePhase.Idle;
            cable.enabled=visible;
            if(visible)
            {
                float length=Vector3.Distance(MuzzlePoint(),tip);
                bool ownerInput=tool.IsOwner&&Application.isFocused&&!UI.UiInputGate.Blocked&&!UI.UiInputGate.PointerReserved&&Mouse.current!=null&&Mouse.current.leftButton.isPressed;
                bool reeling=confirmed && (tool.Reeling || ownerInput);
                float tension=tool.HookedFish==null?(reeling?1:0):tool.LineTension;
                DrawCable(MuzzlePoint(),LineKnot,now,tension,reeling);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(settled)
                    VerifyAnchor(targetPoint,targetNormal);
#endif
                float spin=0;
                if(Phase==CablePhase.Flying)spin=-2400;
                else if(Phase==CablePhase.Retracting)spin=1800;
                else if(Phase==CablePhase.Attached)
                {
                    float cableSpeed=lastLength>0?(length-lastLength)/Mathf.Max(.001f,dt):0;
                    if(cableSpeed>.25f)spin=-Mathf.Min(1800,cableSpeed*220);
                    else if(reeling)spin=620;
                    else if(cableSpeed<-.15f)spin=Mathf.Min(900,-cableSpeed*220);
                }
                SpoolDegrees+=spin*dt;if(reel!=null)reel.localRotation=Quaternion.AngleAxis(SpoolDegrees,Vector3.right);
                if(material!=null&&material.HasProperty("_BaseMap"))material.SetTextureOffset("_BaseMap",new Vector2(SpoolDegrees/1800,0));
                lastLength=length;
            }
        }

        Vector3 MuzzlePoint() => muzzle!=null&&muzzle.gameObject.activeInHierarchy?muzzle.position:tool.transform.position+Vector3.up*.6f+tool.transform.right*.2f;

        void SetBolt(Vector3 point,Quaternion rotation)
        {
            flightBolt.gameObject.SetActive(true);
            flightBolt.rotation=rotation;
            flightBolt.position=point-flightBolt.TransformVector(BoltTipLocal);
        }

        void Retract(float now)
        {retractStart=tip;phaseAt=now;Phase=CablePhase.Retracting;}

        float shownLoad, thrum, twangKick; int cableTug;
        // The cable tells the fight: slack line hangs in a lazy catenary and drifts; a loaded line
        // snaps straight, thins, and thrums with a high-frequency standing wave that grows with pull.
        void DrawCable(Vector3 from,Vector3 to,float now,float tension,bool reeling)
        {
            Vector3 delta=to-from;
            float length=delta.magnitude;
            Vector3 dir=length>.001f?delta/length:Vector3.forward;
            Vector3 normal=Vector3.Cross(dir,Vector3.up);if(normal.sqrMagnitude<.1f)normal=Vector3.right;normal.Normalize();
            Vector3 binormal=Vector3.Cross(dir,normal);
            bool flying=Phase==CablePhase.Flying;
            bool fighting=Phase==CablePhase.Attached && tool.HookedFish!=null;
            // Load = how taut the physical line is. The server spool pays out to the body anchor:
            // when paid-out length is within the slack allowance of the real anchor distance the
            // line is a straight wire; only genuine slack hangs.
            float anchorDistance=fighting && tool.AttachedContact.Resolve(out _,out var contactPoint,out _,out _)
                ? Vector3.Distance(tool.FishingAnchor,contactPoint) : length;
            float slack=fighting?Mathf.Max(0,tool.LineLength-anchorDistance):0;
            float allowance=tool.tuning!=null?tool.tuning.slackAllowance:.18f;
            bool taut=fighting?slack<=allowance+.04f:reeling;
            var fishNow=tool.HookedFish;
            bool pulling=fighting && (fishNow.Surging || fishNow.SurgeWarning || tool.Reeling);
            float load=fighting?(taut||pulling?1f:Mathf.Clamp01(1-slack/.9f)*.5f+tension*.3f):(reeling?1:0);
            shownLoad=Mathf.Lerp(shownLoad,load,1-Mathf.Exp(-Time.unscaledDeltaTime*(load>shownLoad?22:8)));
            float sag=Mathf.Lerp(Mathf.Min(.8f,.05f+length*.045f),0f,shownLoad)+Mathf.Min(.7f,slack*.4f)*(1-shownLoad);
            if(Phase==CablePhase.Retracting)sag=.2f;
            if(flying)sag=.02f;
            // Thrum: a fast standing wave while loaded, stronger during a tug, invisible when slack.
            var fish=tool.HookedFish;
            float pull=fighting?Mathf.Clamp01(fish.TugStrength):0;
            if(fighting && taut) sag=Mathf.Min(sag,.004f);
            // A taut wire is still. A tug plucks it once: a short twang that dies within a third of a second.
            if(fighting && fish.TugSequence!=cableTug){cableTug=fish.TugSequence;twangKick=.018f*Mathf.Clamp01(length/3f);}
            twangKick=Mathf.Lerp(twangKick,0,1-Mathf.Exp(-Time.unscaledDeltaTime*9));
            float wantThrum=twangKick+(taut?0:shownLoad*.003f*Mathf.Clamp01(length/3f));
            thrum=Mathf.Lerp(thrum,wantThrum,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
            float drift=flying?.06f:Phase==CablePhase.Retracting?.035f:.012f*(1-shownLoad);
            float twang=Mathf.Sin(now*62f)*thrum+Mathf.Sin(now*41f+1.3f)*thrum*.5f;
            // The muzzle and nock are physical attachment points; only the cable between them vibrates.
            points[0]=from;
            points[^1]=to;
            for(int i=1;i<points.Length-1;i++)
            {
                float t=i/(float)(points.Length-1),envelope=Mathf.Sin(t*Mathf.PI);
                float standing=Mathf.Sin(t*Mathf.PI*3f)*twang+Mathf.Sin(t*Mathf.PI*5f+now*9f)*thrum*.35f;
                points[i]=Vector3.Lerp(from,to,t)
                    +Vector3.down*(envelope*sag)
                    +normal*(Mathf.Sin(t*24-now*36)*envelope*drift+standing)
                    +binormal*(Mathf.Cos(t*19+now*31)*envelope*drift*.6f+standing*.4f);
            }
            cable.SetPositions(points);
            // A loaded line reads bright and hard; slack hangs dull and a little thicker.
            float width=cableWidth*Mathf.Lerp(1.3f,1f,shownLoad);
            cable.startWidth=cable.endWidth=width;
            if(material!=null && material.HasProperty("_BaseColor"))
            {
                float glow=Mathf.Clamp01(shownLoad*.75f+pull*.6f);
                material.SetColor("_BaseColor",Color.Lerp(new Color(.46f,.42f,.30f),new Color(.98f,.94f,.74f),glow));
                if(material.HasProperty("_EmissionColor"))
                {
                    if(!material.IsKeywordEnabled("_EMISSION"))material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor",new Color(.9f,.86f,.62f)*(glow*glow*.9f));
                }
            }
        }

        void Clear()
        {
            Phase=CablePhase.Idle;attached=null;inFlightContact=default;lastLength=0;
            if(cable!=null)cable.enabled=false;if(flightBolt!=null)flightBolt.gameObject.SetActive(false);if(loadedBolt!=null)loadedBolt.gameObject.SetActive(true);
        }

        public void Release()
        {
            if(tool!=null)tool.ShotVisual-=OnShot;
            Clear();if(legacy!=null)legacy.enabled=tool!=null&&tool.NetworkObject!=null&&tool.IsClientInitialized&&(tool.OwnsFriendTether||tool.HasBallLine||tool.HookedFish!=null&&tool.HookedFish.Mode>=FishMode.Hooked&&tool.HookedFish.Mode<=FishMode.Tired);
            if(material!=null){Destroy(material);material=null;}if(braid!=null){Destroy(braid);braid=null;}
            tool=null;legacy=null;
        }
        void OnDisable(){if(Phase!=CablePhase.Idle)Clear();}
        void OnDestroy(){Release();}
    }
}
