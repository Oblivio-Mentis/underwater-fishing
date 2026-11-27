using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    [DisallowMultipleComponent, DefaultExecutionOrder(860)]
    public sealed partial class DefenseAtmosphereView : MonoBehaviour
    {
        const int SpeciesCount = 5;
        const int MaxSlots = 60;
        const float Tau = 2 * Mathf.PI;
        static readonly string[] ModelNames = { "Rammer", "Remora", "Thief", "Abductor", "Stalker" };
        static readonly float[] SwimBeat = { 5.4f, 6.1f, 8.8f, 4.1f, 6.3f };
        static readonly float[] FinFlex = { .18f, .33f, .19f, .30f, .24f };
        static readonly int EyePass = Shader.PropertyToID("_EyePass");
        static readonly int Fade = Shader.PropertyToID("_Fade");
        static readonly int Glow = Shader.PropertyToID("_EyeGlow");
        static readonly int Range = Shader.PropertyToID("_Range");
        static readonly int Billow = Shader.PropertyToID("_Billow");
        static readonly int CloudDistanceScale = Shader.PropertyToID("_CloudDistanceScale");
        static DefenseAtmosphereSettings stock;

        [SerializeField] DefenseAtmosphereSettings settings;

        readonly Mesh[] meshes = new Mesh[SpeciesCount];
        readonly MeshRenderer[] swimmers = new MeshRenderer[MaxSlots];
        readonly MaterialPropertyBlock[,] blocks = new MaterialPropertyBlock[SpeciesCount,2];
        Material material;
        int raidSeed, phase;
        float phaseAge, appliedAt, swarmAge, sampledAt, shade;
        Vector3 anchor;
        Vector3 approachStart, approachEnd;
        float approachFraction, warningReveal;
        ulong promoted;
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
        public static int SwarmSlotCount => Mathf.Clamp(Stock.silhouetteCount, 35, MaxSlots);
        public int VisibleCount { get; private set; }

        public readonly struct OrbitPose
        {
            public readonly Vector3 Position, Forward;
            public readonly float Scale;
            public readonly int Kind;
            public OrbitPose(Vector3 position, Vector3 forward, float scale, int kind)
            {
                Position = position;
                Forward = forward;
                Scale = scale;
                Kind = kind;
            }
        }

        public static OrbitPose SampleOrbit(int seed, int slot, float swarmSeconds, Vector3 vesselCenter)
            => SampleOrbit(seed, slot, swarmSeconds, vesselCenter, Stock);

        public static OrbitPose SampleOrbit(int seed, int slot, float swarmSeconds, Vector3 vesselCenter,
            DefenseAtmosphereSettings profile)
        {
            if (!profile) profile = Stock;
            uint key = unchecked((uint)seed * 0x9e3779b9u + (uint)slot * 0x85ebca6bu);
            int kind = (int)((uint)slot % SpeciesCount);
            float start = slot * 2.399963f + Unit(key) * .58f;
            float direction = Unit(key + 3) > .31f ? 1 : -1;
            float speed = direction * Mathf.Lerp(.049f,.12f,Unit(key + 5)) * profile.swimSpeed;
            float tilt = Unit(key + 7) * Tau;
            float radius = Mathf.Max(6,profile.orbitRadius) + Mathf.Max(0,profile.radiusVariation) * Unit(key + 11);
            float height = (Unit(key + 13) - .48f) * profile.heightVariation * 1.36f;
            float scale = Mathf.Lerp(1-profile.sizeVariation,1+profile.sizeVariation,Unit(key + 17));
            float age = Mathf.Max(0,swarmSeconds);
            Vector3 current = Point(age,start,speed,tilt,radius,height);
            Vector3 tangent = Point(age+.075f,start,speed,tilt,radius,height)-current;
            if (tangent.sqrMagnitude < .000001f) tangent = Vector3.forward;
            Vector3 faceShip = new Vector3(-current.x,-.18f*current.y,-current.z).normalized;
            float bank = Mathf.Pow(Mathf.Max(0, Mathf.Sin(age*.37f + tilt)), 5) * .81f;
            Vector3 heading = Vector3.Slerp(tangent.normalized, faceShip, bank);
            return new OrbitPose(vesselCenter + current, heading, scale, kind);
        }

        static Vector3 Point(float age,float start,float speed,float tilt,float radius,float height)
        {
            float angle = start + speed * age + Mathf.Sin(age*.19f + tilt)*.19f;
            float swimRadius = radius + Mathf.Sin(angle * 2.1f + tilt + age*.16f)*2.2f;
            float y = height + Mathf.Sin(angle*1.7f + tilt + age*.27f)*1.9f + Mathf.Cos(angle*3.1f-tilt)*.81f;
            return new Vector3(Mathf.Cos(angle)*swimRadius,Mathf.Clamp(y-1.1f,-6.8f,7.9f),Mathf.Sin(angle)*swimRadius*1.12f);
        }

        public static OrbitPose SampleApproach(int seed,int slot,float time,Vector3 start,Vector3 end,float fraction,
            DefenseAtmosphereSettings profile=null)
        {
            if(!profile)profile=Stock;
            uint key=unchecked((uint)seed+(uint)slot*15731u);
            uint sizing=unchecked((uint)seed*0x9e3779b9u+(uint)slot*0x85ebca6bu);
            Vector3 heading=(end-start).normalized;
            Vector3 lateral=Vector3.Cross(Vector3.up,heading).normalized;
            float push=Mathf.InverseLerp(Unit(key+29)*.22f,1,fraction);
            Vector3 school=Vector3.Lerp(start,end,push*(.23f+.77f*push));
            float row=(slot%5-2)*2.35f+Mathf.Sin(time*.82f+slot*.81f)*1.42f;
            Vector3 point=school+lateral*(row+(Unit(key+19)-.5f)*3.1f)+
                Vector3.up*((Unit(key+31)-.5f)*4.7f+Mathf.Sin(time*.9f+slot*.47f)*.28f)-heading*(Unit(key+43)*8.5f);
            float scale=Mathf.Lerp(1-profile.sizeVariation,1+profile.sizeVariation,Unit(sizing+17));
            return new OrbitPose(point,heading,scale,slot%SpeciesCount);
        }

        public static OrbitPose SampleSwimmer(int seed,int slot,float time,Vector3 center,Vector3 start,Vector3 end,
            float fraction,int phase,float phaseSeconds,DefenseAtmosphereSettings profile=null)
        {
            var orbit=SampleOrbit(seed,slot,time,center,profile);
            if(phase>=4 || (end-start).sqrMagnitude<=1 || phase==3 && phaseSeconds>=13)return orbit;
            var approach=SampleApproach(seed,slot,time,start,end,fraction,profile);
            float spread=phase==3?Mathf.SmoothStep(0,1,phaseSeconds/13f):0;
            return new OrbitPose(Vector3.Lerp(approach.Position,orbit.Position,spread),
                Vector3.Slerp(approach.Forward,orbit.Forward,spread),orbit.Scale,orbit.Kind);
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

        public void SetPromotedSlots(ulong mask) => promoted = mask;

        public void SetApproach(Vector3 start,Vector3 end,float fraction,float anticipation)
        {
            approachStart=start;approachEnd=end;
            approachFraction=Mathf.Clamp01(fraction);
            warningReveal=Mathf.Clamp01(anticipation);
        }

        public void Apply(int seed,int phase,float phaseAge,Vector3 vesselCenter,float visibility)
        {
            if (seed != raidSeed && (phase == 0 || phase == 1) && Time.frameCount != clockFrame)
                hasSwarmClock = false;
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
            if (!eye || !eye.isActiveAndEnabled){HideSwarm();HideCloud();return;}
            var profile = Settings;
            float now = Time.time;
            float elapsed = phaseAge + Mathf.Max(0, now-appliedAt);
            float orbitAge = hasSwarmClock ? swarmAge + Mathf.Max(0,now-sampledAt) : elapsed;

            if (phase == 0){HideSwarm();HideCloud();return;}
            BindAssets();
            PresentCloud(profile,orbitAge,elapsed);
            float reveal = phase == 1 ? Mathf.SmoothStep(0,1,warningReveal)
                : phase == 5 ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,4.4f,elapsed)) : 1;
            if (!material || reveal < .02f){HideSwarm();return;}
            RenderSwarm(profile,eye,orbitAge,reveal);
        }

        void RenderSwarm(DefenseAtmosphereSettings profile,Camera eye,float orbitAge,float reveal)
        {
            VisibleCount=0;
            float range = Mathf.Lerp(profile.clearDrawingDistance,profile.darkDrawingDistance,shade);
            int slots = Mathf.Clamp(profile.silhouetteCount,35,MaxSlots);
            var vessel=NereidVessel.Active;
            Vector3 scale=transform.lossyScale;
            var unit=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            for(int species=0;species<SpeciesCount;species++)
            for(int surface=0;surface<2;surface++)
            {
                var block=blocks[species,surface];
                block.SetFloat(EyePass,surface);
                block.SetFloat(Fade,reveal);
                block.SetFloat(Glow,profile.eyeGlow);
                block.SetFloat(Range,range);
                block.SetVector(Billow,new Vector4(SwimBeat[species],FinFlex[species],0,0));
            }
            for (int i = 0; i < swimmers.Length; i++)
            {
                var swimmer=swimmers[i];
                if(!swimmer)continue;
                if(i>=slots || (promoted & (1UL << i)) != 0){swimmer.enabled=false;continue;}
                OrbitPose pose = SampleSwimmer(raidSeed,i,orbitAge,anchor,approachStart,approachEnd,
                    approachFraction,phase,phaseAge,profile);
                Vector3 point = pose.Position;
                Vector3 heading = pose.Forward;
                if(phase>=4)
                {
                    Vector3 leave=Vector3.ProjectOnPlane(point-anchor,Vector3.up).normalized;
                    float distance=phase==4?phaseAge*6.4f:34+phaseAge*6.4f;
                    point+=leave*distance+Vector3.up*Mathf.Min(9,distance*.19f);
                    heading=Vector3.Slerp(heading,(leave+Vector3.up*.19f).normalized,
                        Mathf.Clamp01(distance/11f));
                }
                if ((point-eye.transform.position).sqrMagnitude>(range+3)*(range+3)){swimmer.enabled=false;continue;}
                Vector3 view = eye.WorldToViewportPoint(point);
                if (view.z <= .01f || (view.z > 5f && (view.x < -.14f || view.x > 1.14f || view.y < -.18f || view.y > 1.18f)))
                {swimmer.enabled=false;continue;}
                if(vessel && !vessel.OutsideDefenseHull(point,heading,pose.Scale))
                {swimmer.enabled=false;continue;}
                float roll = Mathf.Sin(orbitAge*(1.5f+.17f*pose.Kind)+i*.73f) * (8+pose.Kind*2.2f);
                Quaternion bank = Quaternion.LookRotation(heading) * Quaternion.AngleAxis(roll,Vector3.forward);
                swimmer.transform.SetPositionAndRotation(point,bank);
                swimmer.transform.localScale=unit*pose.Scale;
                for(int surface=0;surface<Mathf.Min(meshes[pose.Kind].subMeshCount,2);surface++)
                    swimmer.SetPropertyBlock(blocks[pose.Kind,surface],surface);
                swimmer.enabled=true;
                VisibleCount++;
            }
        }

        void HideSwarm()
        {
            if(VisibleCount>0)foreach(var swimmer in swimmers)if(swimmer)swimmer.enabled=false;
            VisibleCount=0;
        }

        void OnDisable()
        {
            HideCloud();
            HideSwarm();
        }
    }
}
