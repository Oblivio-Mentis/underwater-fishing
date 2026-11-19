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
        [Min(.001f)] public float cableWidth=.011f;
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
        static readonly Vector3 BoltTipLocal=new Vector3(0,.025f,.48f);
        static readonly Vector3 BoltNockLocal=new Vector3(0,.025f,.19f);
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
        AudioSource shotAudio,reelAudio;
        AudioClip shotClip,reelClip;
        float shotAt=float.NegativeInfinity,flightSeconds,phaseAt,lastLength;

        public void Initialize(HarpoonTool source,Transform muzzleAnchor,Transform template,Transform spool,Material fallback,bool worldView=false)
        {
            this.worldView=worldView;muzzle=muzzleAnchor;loadedBolt=template;reel=spool;
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
            shotAudio=gameObject.AddComponent<AudioSource>();shotAudio.playOnAwake=false;shotAudio.volume=.17f;shotAudio.mute=!Application.isFocused;
            reelAudio=gameObject.AddComponent<AudioSource>();reelAudio.playOnAwake=false;reelAudio.volume=.065f;reelAudio.loop=true;reelAudio.mute=!Application.isFocused;
            shotClip=Sound(false);reelClip=Sound(true);reelAudio.clip=reelClip;
            if(worldView)
            {
                shotAudio.spatialBlend=reelAudio.spatialBlend=1;
                shotAudio.minDistance=reelAudio.minDistance=1.5f;
                shotAudio.maxDistance=reelAudio.maxDistance=24;
            }
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
            shotAudio.mute=!Application.isFocused;if(!shotAudio.mute){shotAudio.pitch=tool.Motor!=null&&!tool.Motor.IsInside?.78f:1;shotAudio.PlayOneShot(shotClip);}
        }

        void LateUpdate()
        {
            if(!CanDraw()){if(Phase!=CablePhase.Idle)Clear();return;}
            if(legacy!=null)legacy.enabled=false;
            float now=Time.unscaledTime,dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            var hit=tool.AttachedContact;
            bool confirmed=hit.Resolve(out var target,out var targetPoint,out var targetNormal,out var targetDirection)
                && (tool.OwnsFriendTether || tool.HookedFish!=null && tool.HookedFish.Mode>=FishMode.Hooked && tool.HookedFish.Mode<=FishMode.Tired);
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
                bool reeling=confirmed&&(tool.Reeling||ownerInput||(!tool.IsOwner && lastLength>0 && length<lastLength-.008f));
                float tension=tool.HookedFish==null?(reeling?1:0):tool.HookedFish.Tension;
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
                    float change=length-lastLength;
                    if(change>.01f)spin=-Mathf.Min(1800,change/Mathf.Max(.001f,dt)*220);
                    else if(reeling)spin=620;
                    else if(change<-.005f)spin=280;
                }
                SpoolDegrees+=spin*dt;if(reel!=null)reel.localRotation=Quaternion.AngleAxis(SpoolDegrees,Vector3.right);
                reelAudio.mute=!Application.isFocused||UI.UiInputGate.Modal;reelAudio.pitch=.85f+Mathf.Clamp01(Mathf.Abs(spin)/1800)*.6f;
                if(Mathf.Abs(spin)>20&&!reelAudio.isPlaying)reelAudio.Play();else if(Mathf.Abs(spin)<=20&&reelAudio.isPlaying)reelAudio.Stop();
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

        void DrawCable(Vector3 from,Vector3 to,float now,float tension,bool reeling)
        {
            Vector3 delta=to-from;
            Vector3 normal=Vector3.Cross(delta.normalized,Vector3.up);if(normal.sqrMagnitude<.1f)normal=Vector3.right;normal.Normalize();
            bool flying=Phase==CablePhase.Flying;
            float sag=Mathf.Lerp(Mathf.Min(.65f,.06f+delta.magnitude*.035f),.015f,Mathf.Clamp01(tension));
            if(reeling)sag*=.35f;if(Phase==CablePhase.Retracting)sag=.2f;
            float wave=flying?.06f:Phase==CablePhase.Retracting?.035f:.008f*(1-Mathf.Clamp01(tension));
            for(int i=0;i<points.Length;i++)
            {
                float t=i/(float)(points.Length-1),envelope=Mathf.Sin(t*Mathf.PI);
                points[i]=Vector3.Lerp(from,to,t)+Vector3.down*(envelope*sag)+normal*(Mathf.Sin(t*24-now*36)*envelope*wave);
            }
            cable.SetPositions(points);
        }

        void Clear()
        {
            Phase=CablePhase.Idle;attached=null;inFlightContact=default;lastLength=0;
            if(cable!=null)cable.enabled=false;if(flightBolt!=null)flightBolt.gameObject.SetActive(false);if(loadedBolt!=null)loadedBolt.gameObject.SetActive(true);
            if(reelAudio!=null)reelAudio.Stop();if(shotAudio!=null)shotAudio.mute=!Application.isFocused;
        }

        static AudioClip Sound(bool reel)
        {
            const int rate=22050;var samples=new float[reel?rate:rate/5];var random=new System.Random(reel?12202:12201);float filtered=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;filtered=filtered*.7f+(float)(random.NextDouble()*2-1)*.3f;
                float envelope=reel?.7f+.3f*Mathf.Sin(t*Mathf.PI*2*12):Mathf.Exp(-t*32);
                samples[i]=(filtered*(reel?.14f:.45f)+Mathf.Sin(t*Mathf.PI*2*(reel?96:170))*(reel?.06f:.22f))*envelope;
            }
            var clip=AudioClip.Create(reel?"NEREID cable drive":"NEREID pneumatic harpoon",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        public void Release()
        {
            if(tool!=null)tool.ShotVisual-=OnShot;
            Clear();if(legacy!=null)legacy.enabled=tool!=null&&tool.NetworkObject!=null&&tool.IsClientInitialized&&(tool.OwnsFriendTether||tool.HookedFish!=null&&tool.HookedFish.Mode>=FishMode.Hooked&&tool.HookedFish.Mode<=FishMode.Tired);
            if(material!=null){Destroy(material);material=null;}if(braid!=null){Destroy(braid);braid=null;}
            if(shotAudio!=null)shotAudio.Stop();if(reelAudio!=null)reelAudio.Stop();
            if(shotClip!=null){Destroy(shotClip);shotClip=null;}if(reelClip!=null){Destroy(reelClip);reelClip=null;}
            tool=null;legacy=null;
        }
        void OnDisable(){if(Phase!=CablePhase.Idle)Clear();}
        void OnDestroy(){Release();}
    }
}
