using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    [DisallowMultipleComponent, DefaultExecutionOrder(860)]
    public sealed partial class DefenseAtmosphereView : MonoBehaviour
    {
        internal const int SpeciesCount = 5;
        internal const int SlotCapacity = 60;
        internal const int LeadSlots = 10;
        internal const float HandoffSeconds = .18f;
        const int PodCount = 8;
        internal const float WarningDrift = 15f;
        static readonly float[] PodBearings = { 0, -36, 52, -77, 97, 143, -142, 177 };
        static readonly float[] PodTiers = { .10f, -.31f, .56f, .18f, .83f, .42f, -.12f, .69f };
        const float Tau = 2 * Mathf.PI;
        static readonly string[] ModelNames = { "Rammer", "Remora", "Thief", "Abductor", "Stalker" };
        static readonly float[] SwimBeat = { 5.4f, 6.1f, 8.8f, 4.1f, 6.3f };
        static readonly float[] FinFlex = { .18f, .33f, .19f, .30f, .24f };
        static readonly int EyePass = Shader.PropertyToID("_EyePass");
        static readonly int Fade = Shader.PropertyToID("_Fade");
        static readonly int Glow = Shader.PropertyToID("_EyeGlow");
        static readonly int Range = Shader.PropertyToID("_Range");
        static readonly int Billow = Shader.PropertyToID("_Billow");
        static readonly int Motion = Shader.PropertyToID("_Motion");
        static readonly int Handoff = Shader.PropertyToID("_Handoff");
        static DefenseAtmosphereSettings stock;

        [SerializeField] DefenseAtmosphereSettings settings;

        readonly Mesh[] meshes = new Mesh[SpeciesCount];
        readonly MeshRenderer[] swimmers = new MeshRenderer[SlotCapacity];
        readonly MaterialPropertyBlock[,] blocks = new MaterialPropertyBlock[SpeciesCount,2];
        Material material;
        int raidSeed, phase;
        float phaseAge, appliedAt, swarmAge, sampledAt, shade;
        Vector3 anchor;
        Vector3 approachStart, approachEnd;
        float approachAge, approachSeconds, warningReveal, retreatElapsed, recoveryEntry;
        ulong promoted;
        float[] handoffs;
        bool hasSwarmClock, bound;

        public static DefenseAtmosphereSettings Stock
        {
            get
            {
                if (!stock) stock = Resources.Load<DefenseAtmosphereSettings>("DefenseAtmosphere/Atmosphere settings");
                if (!stock) stock = ScriptableObject.CreateInstance<DefenseAtmosphereSettings>();
                return stock;
            }
        }

        public DefenseAtmosphereSettings Settings => settings ? settings : Stock;
        public static int SwarmSlotCount => Mathf.Clamp(Stock.silhouetteCount, 35, SlotCapacity);
        public int VisibleCount { get; private set; }

        public readonly struct OrbitPose
        {
            public readonly Vector3 Position, Forward, Velocity;
            public readonly float Scale, Bank;
            public readonly int Kind;
            public OrbitPose(Vector3 position, Vector3 velocity, float scale, float bank, int kind)
            {
                Position = position;
                Velocity = velocity;
                Forward = velocity.sqrMagnitude > .0001f ? velocity.normalized : Vector3.forward;
                Scale = scale;
                Bank = bank;
                Kind = kind;
            }
        }

        // The server promotes these exact poses. Keep a slot's identity and trajectory
        // independent of frame rate, view visibility, and the order in which peers connect.
        public static OrbitPose SampleSwimmer(int seed,int slot,float signalledAge,float closeSeconds,
            Vector3 center,Vector3 start,Vector3 end,float withdrawal=0,DefenseAtmosphereSettings profile=null)
            => SampleShoal(seed,slot,signalledAge,closeSeconds,center,start,end,withdrawal,false,profile);

        static OrbitPose SampleShoal(int seed,int slot,float age,float closing,Vector3 center,Vector3 start,
            Vector3 end,float withdrawal,bool distant,DefenseAtmosphereSettings profile)
        {
            if(!profile)profile=Stock;
            const float sampleStep=.10f;
            uint key=SlotKey(seed,slot);
            int pod=distant?(slot/SpeciesCount)%PodCount : slot<LeadSlots?0:1+(slot-LeadSlots)%(PodCount-1);
            uint podKey=SlotKey(seed,pod+607);
            Vector3 axis=Vector3.ProjectOnPlane(end-center,Vector3.up).normalized;
            if(axis.sqrMagnitude<.5f)axis=Vector3.forward;
            float initial=Mathf.Max(profile.orbitRadius+40,Vector3.ProjectOnPlane(start-center,Vector3.up).magnitude);
            float shelf=Mathf.Max(profile.orbitRadius,Vector3.ProjectOnPlane(end-center,Vector3.up).magnitude);
            float level=end.y-center.y;
            float offset=PhaseOffset(key,distant,pod==0);
            float clock=age+offset;
            int leadRun=LeadRun(slot);
            var position=Path(clock,withdrawal,pod,leadRun,podKey,key,closing,initial,shelf,level,axis,distant,profile);
            var next=Path(clock+sampleStep,withdrawal>0?withdrawal+sampleStep:0,pod,leadRun,podKey,key,
                closing,initial,shelf,level,axis,distant,profile);
            Vector3 velocity;
            float bank;
            if(distant)
            {
                velocity=(next-position)/sampleStep;
                var radius=Vector3.ProjectOnPlane(position,Vector3.up).normalized;
                bank=Mathf.Clamp(Vector3.Cross(radius,velocity).y*1.9f,-18,18);
            }
            else
            {
                var previous=Path(clock-sampleStep,Mathf.Max(0,withdrawal-sampleStep),pod,leadRun,podKey,key,
                    closing,initial,shelf,level,axis,distant,profile);
                velocity=(next-previous)/(2*sampleStep);
                float yaw=Vector3.SignedAngle(Vector3.ProjectOnPlane(position-previous,Vector3.up),
                    Vector3.ProjectOnPlane(next-position,Vector3.up),Vector3.up);
                bank=Mathf.Clamp(-yaw*11,-27,27);
            }
            float scale=Mathf.Lerp(1-profile.sizeVariation,1+profile.sizeVariation,Unit(key+17));
            return new OrbitPose(center+position,velocity,scale,bank,slot%SpeciesCount);
        }

        static uint SlotKey(int seed,int slot) => unchecked((uint)seed*0x9e3779b9u+(uint)slot*0x85ebca6bu);
        static float PhaseOffset(uint key,bool distant,bool lead) => (Unit(key+41)-.5f)*(distant?3.4f:lead?.42f:1.4f);

        static Vector3 Path(float age,float withdrawal,int pod,int leadRun,uint podKey,uint key,float closing,float initial,float shelf,
            float level,Vector3 axis,bool distant,DefenseAtmosphereSettings profile)
        {
            bool wing=pod==0 && !distant;
            float tier=PodTiers[pod];
            float angle=(PodBearings[pod]+(Unit(podKey+7)-.5f)*(wing?4:24))*Mathf.Deg2Rad;
            float individual=(Unit(key+19)-.5f)*(distant?12:5.6f);
            float scatter=(Unit(key+23)-.5f)*(distant?22:4.6f);
            float depth=(Unit(key+29)-.5f)*(distant?10:3.8f);
            float phase=Unit(podKey+31)*Tau;
            float turn=(pod==2 || pod==5 || pod==7?-1:1);
            float strength=Mathf.Lerp(.72f,1.22f,Unit(podKey+37));
            float speed=turn*profile.swimSpeed*strength*(distant?profile.cloudOrbitSpeed*.55f:.080f);
            float radial,height;
            float bankAge=Mathf.Max(0,age+closing+WarningDrift);
            float pulse=Mathf.Sin(bankAge*.19f+phase);
            if(distant)
            {
                // Several deep bands with open lanes. Drift round the sides at different
                // rates and depths, leaving water between the groups for the searchlights.
                float outer=profile.cloudRadius*profile.cloudDistanceScale;
                radial=outer*(1.02f+tier*.33f)+11+6*pulse+2.3f*Mathf.Sin(bankAge*.43f+phase)+
                    Mathf.SmoothStep(20,0,Mathf.InverseLerp(-closing-WarningDrift,3,age));
                angle+=speed*bankAge+.085f*Mathf.Sin(bankAge*.24f+phase);
                height=level-7+profile.cloudHeight*tier+3.9f*Mathf.Sin(bankAge*.13f+phase);
            }
            else
            {
                float berth=wing?profile.handoffRadius:Mathf.Max(shelf+3,shelf+profile.radiusVariation*(.27f+tier*.63f));
                float entranceSpeed=wing?-2.8f:-.7f;
                float tangentSpeed=wing?.014f:turn*.043f;
                float approachStart=-closing-WarningDrift;
                float approachRadius=initial+5*tier;
                float halfway=approachRadius-15;
                if(age<-closing)
                {
                    float t=Mathf.InverseLerp(approachStart,-closing,age);
                    radial=Hermite(approachRadius,halfway,0,-3.4f,t,WarningDrift);
                    angle+=Hermite(-tangentSpeed*2,0,0,tangentSpeed*.44f,t,WarningDrift);
                }
                else if(age<=0)
                {
                    float t=Mathf.InverseLerp(-closing,0,age);
                    radial=Hermite(halfway,berth,-3.4f,entranceSpeed,t,closing);
                    angle+=Hermite(0,tangentSpeed*closing*.70f,tangentSpeed*.44f,tangentSpeed,t,closing);
                }
                else
                {
                    float leave=wing?10:5;
                    float orbit=wing?shelf+4:berth+4.1f;
                    float t=Mathf.Clamp01(age/leave);
                    radial=Hermite(berth,orbit,entranceSpeed,0,t,leave);
                    radial+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,11,age))*
                        (4.4f*pulse+2.1f*Mathf.Sin(bankAge*.47f+phase));
                    angle+=tangentSpeed*closing*.70f+
                        EasedTravel(tangentSpeed,speed,age,leave);
                    angle+=Mathf.SmoothStep(0,1,t)*.11f*Mathf.Sin(bankAge*.17f+phase);
                }
                height=level+profile.heightVariation*tier+1.55f*Mathf.Sin(bankAge*.34f+phase);
                if(wing)
                {
                    // The fast pod peels into three offset runs before the server takes over.
                    int split=leadRun;
                    float release=split*.67f;
                    float fork=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-6.4f+release,-.25f+release,age));
                    float sweep=Mathf.Sin(fork*Mathf.PI);
                    scatter+=split*(8.4f*fork+2.2f*sweep);
                    height+=(2.0f-split*2.0f)*fork-2.4f*sweep*(split==0?1:.24f);
                    radial+=split*3.1f*fork;
                }
            }
            float spacing=wing?Mathf.SmoothStep(.25f,1,Mathf.InverseLerp(.65f,3.3f,Mathf.Abs(age))):1;
            radial+=(individual+Mathf.Sin(age*.77f+Unit(key+59)*Tau)*(distant?2.4f:.66f))*spacing;
            if(withdrawal>0)
            {
                float spread=withdrawal-.9f*(1-Mathf.Exp(-withdrawal/.9f));
                radial+=spread*(distant?5.4f:5.9f);
                height+=Mathf.Min(10,spread*.8f);
            }
            angle+=scatter/Mathf.Max(radial,12);
            var facing=axis*Mathf.Cos(angle)+Vector3.Cross(Vector3.up,axis)*Mathf.Sin(angle);
            return facing*radial+Vector3.up*(height+depth);
        }

        static float Hermite(float a,float b,float da,float db,float t,float duration)
        {
            float t2=t*t,t3=t2*t;
            return (2*t3-3*t2+1)*a+(t3-2*t2+t)*da*duration+(-2*t3+3*t2)*b+(t3-t2)*db*duration;
        }

        static int LeadRun(int slot) => slot%3-1;

        static float SwimmerEmergence(int seed,int slot,int pod,float warning)
        {
            // The glints emerge first. A slight lag across adjacent pods carries the
            // reveal around the boat, with variation inside each moving group.
            float lead=pod*.046f+Unit(SlotKey(seed,slot+153))*.19f;
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(lead,lead+.38f,warning));
        }

        // Integral of a cubic ease between angular velocities. The heading stays
        // tangent to the same continuous curve when the assault begins.
        static float EasedTravel(float from,float to,float age,float duration)
        {
            float t=Mathf.Clamp01(age/duration);
            return from*t*duration+(to-from)*duration*t*t*t*(1-.5f*t)+Mathf.Max(0,age-duration)*to;
        }

        static float Unit(uint number)
        {
            unchecked
            {
                number ^= number >> 16;
                number *= 0x7feb352du;
                number ^= number >> 15;
                number *= 0x846ca68bu;
                return ((number ^ (number >> 16)) & 0xffffff) / 16777215f;
            }
        }

        public void SetSwarmAge(float seconds)
        {
            hasSwarmClock = float.IsFinite(seconds) && seconds >= 0;
            swarmAge = hasSwarmClock ? seconds : 0;
            sampledAt = Time.time;
            clockFrame = Time.frameCount;
        }

        public void SetPromotedSlots(ulong mask,float[] startedAt)
        {
            promoted=mask;
            handoffs=startedAt;
        }

        public void SetApproach(Vector3 start,Vector3 end,float age,float closing,float anticipation)
        {
            approachStart=start;approachEnd=end;
            approachAge=age;
            approachSeconds=Mathf.Max(.5f,closing);
            warningReveal=Mathf.Clamp01(anticipation);
        }

        public void Apply(int seed,int phase,float phaseAge,Vector3 vesselCenter,float visibility)
        {
            if (seed != raidSeed && (phase == 0 || phase == 1) && Time.frameCount != clockFrame)
                hasSwarmClock = false;
            if(seed!=raidSeed || phase<4)retreatElapsed=recoveryEntry=0;
            if(phase==4)retreatElapsed=phaseAge;
            if(phase==5)
            {
                if(this.phase!=5 || seed!=raidSeed)recoveryEntry=this.phase==4?retreatElapsed:1.2f;
                retreatElapsed=recoveryEntry+phaseAge;
            }
            raidSeed = seed;
            this.phase = Mathf.Clamp(phase,0,5);
            this.phaseAge = Mathf.Max(0,phaseAge);
            appliedAt = Time.time;
            shade = Mathf.Clamp01(visibility);
            anchor = vesselCenter;
        }

        int clockFrame;

        void BindAssets()
        {
            if (bound) return;
            bound = true;
            var profile = Settings;
            material = profile.silhouetteMaterial;
            for (int i = 0; i < SpeciesCount; i++)
            {
                if (profile.silhouetteMeshes != null && i < profile.silhouetteMeshes.Length)
                    meshes[i] = profile.silhouetteMeshes[i];
                if (!meshes[i])
                {
                    var model = Resources.Load<GameObject>("DefenseCreatures/Impostors/" + ModelNames[i]);
                    if (model && model.GetComponentInChildren<MeshFilter>() is MeshFilter filter)
                        meshes[i] = filter.sharedMesh;
                }
                blocks[i,0] = new MaterialPropertyBlock();
                blocks[i,1] = new MaterialPropertyBlock();
            }
            if(!material)return;
            var pigmentAndEyes=new[]{material,material};
            var pigment=new[]{material};
            for(int i=0;i<swimmers.Length;i++)
            {
                var mesh=meshes[i%SpeciesCount];
                if(!mesh)continue;
                var node=new GameObject("Incoming silhouette "+(i+1),typeof(MeshFilter),typeof(MeshRenderer));
                node.layer=gameObject.layer;
                node.transform.SetParent(transform,false);
                node.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=swimmers[i]=node.GetComponent<MeshRenderer>();
                renderer.sharedMaterials=mesh.subMeshCount>1?pigmentAndEyes:pigment;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;
                renderer.enabled=false;
            }
            BindCloud(profile);
        }

        void LateUpdate()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var look = UnderwaterLook.Instance;
            Camera eye = look ? look.DiverCamera : Camera.main;
            if (!eye || !eye.isActiveAndEnabled){HideSwarm();HideCloud();HideCurrents();return;}
            var profile = Settings;
            float now = Time.time;
            float interpolation = Mathf.Clamp(now-appliedAt,0,.10f);
            float elapsed = phaseAge + interpolation;
            float orbitAge = hasSwarmClock ? swarmAge + Mathf.Clamp(now-sampledAt,0,.10f) : elapsed;
            float agedApproach = approachAge+interpolation;
            float retreat = phase>=4?retreatElapsed+interpolation:0;

            if (phase == 0){HideSwarm();HideCloud();HideCurrents();return;}
            BindAssets();
            PresentCloud(profile,orbitAge,agedApproach,retreat);
            PresentCurrents(profile,agedApproach,retreat);
            float reveal = phase == 1 ? Mathf.SmoothStep(0,1,warningReveal)
                : phase == 5 ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,4.4f,elapsed)) : 1;
            if (!material || reveal < .02f){HideSwarm();return;}
            RenderSwarm(profile,eye,orbitAge,agedApproach,retreat,reveal);
        }

        void RenderSwarm(DefenseAtmosphereSettings profile,Camera eye,float orbitAge,float agedApproach,float retreat,float reveal)
        {
            VisibleCount=0;
            float range = Mathf.Lerp(profile.clearDrawingDistance,profile.darkDrawingDistance,shade);
            int slots = Mathf.Clamp(profile.silhouetteCount,35,SlotCapacity);
            var vessel=NereidVessel.Active;
            Vector3 scale=transform.lossyScale;
            var unit=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            for(int species=0;species<SpeciesCount;species++)
            for(int surface=0;surface<2;surface++)
            {
                var block=blocks[species,surface];
                block.SetFloat(EyePass,surface);
                block.SetFloat(Glow,profile.eyeGlow);
                block.SetFloat(Range,range);
                block.SetVector(Billow,new Vector4(SwimBeat[species],FinFlex[species],0,0));
            }
            for (int i = 0; i < swimmers.Length; i++)
            {
                var swimmer=swimmers[i];
                if(!swimmer)continue;
                if(i>=slots || (promoted & (1UL << i)) != 0){swimmer.enabled=false;continue;}
                OrbitPose pose = SampleSwimmer(raidSeed,i,agedApproach,approachSeconds,anchor,approachStart,approachEnd,retreat,profile);
                Vector3 point = pose.Position;
                Vector3 heading = pose.Forward;
                if ((point-eye.transform.position).sqrMagnitude>(range+3)*(range+3)){swimmer.enabled=false;continue;}
                Vector3 view = eye.WorldToViewportPoint(point);
                if (view.z <= .01f || (view.z > 5f && (view.x < -.14f || view.x > 1.14f || view.y < -.18f || view.y > 1.18f)))
                {swimmer.enabled=false;continue;}
                if(vessel && !vessel.OutsideDefenseHull(point,heading,pose.Scale))
                {swimmer.enabled=false;continue;}
                Quaternion bank = Quaternion.LookRotation(heading) * Quaternion.AngleAxis(pose.Bank,Vector3.forward);
                swimmer.transform.SetPositionAndRotation(point,bank);
                swimmer.transform.localScale=unit*pose.Scale;
                float emerge=phase==1?SwimmerEmergence(raidSeed,i,i<LeadSlots?0:1+(i-LeadSlots)%(PodCount-1),warningReveal):1;
                for(int surface=0;surface<Mathf.Min(meshes[pose.Kind].subMeshCount,2);surface++)
                {
                    var block=blocks[pose.Kind,surface];
                    block.SetFloat(Fade,reveal*emerge);
                    float handoff=handoffs!=null && handoffs[i]>0?Mathf.Clamp01((Time.time-handoffs[i])/HandoffSeconds):0;
                    block.SetFloat(Handoff,handoff);
                    block.SetVector(Motion,SampleStroke(raidSeed,i,pose,orbitAge,agedApproach));
                    swimmer.SetPropertyBlock(block,surface);
                }
                swimmer.enabled=true;
                VisibleCount++;
            }
        }

        internal static Vector4 SampleStroke(int seed,int slot,OrbitPose pose,float age,float approachTime=float.NegativeInfinity)
        {
            uint key=SlotKey(seed,slot);
            float rhythm=SwimBeat[pose.Kind]*Mathf.Lerp(.84f,1.16f,Unit(key+71));
            float stroke=age*rhythm+Unit(key+73)*Tau+Mathf.Sin(age*.75f+Unit(key+79)*Tau)*.52f;
            float effort=Mathf.Lerp(.49f,1.03f,Mathf.InverseLerp(.9f,8f,pose.Velocity.magnitude));
            float alertCenter=slot<LeadSlots?-5.35f+LeadRun(slot)*.67f+(slot/3)*.28f:-4.40f+Unit(key+97)*1.7f;
            float flash=approachTime-alertCenter;
            float recruitment=Mathf.Exp(-(flash*flash)*(slot<LeadSlots?7.5f:18f));
            return new Vector4(stroke,effort,pose.Kind,Mathf.Lerp(.72f,1.08f,Unit(key+83))+
                recruitment*(slot<LeadSlots?.67f:.21f));
        }

        void HideSwarm()
        {
            if(VisibleCount>0)foreach(var swimmer in swimmers)if(swimmer)swimmer.enabled=false;
            VisibleCount=0;
        }

        void OnDisable()
        {
            HideCurrents();
            HideCloud();
            HideSwarm();
        }
    }
}
