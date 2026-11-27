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
        static DefenseAtmosphereSettings stock;

        [SerializeField] DefenseAtmosphereSettings settings;
        [SerializeField] Transform warningOrigin;

        readonly Matrix4x4[][] matrices = new Matrix4x4[SpeciesCount][];
        readonly Mesh[] meshes = new Mesh[SpeciesCount];
        readonly MaterialPropertyBlock[,] blocks = new MaterialPropertyBlock[SpeciesCount,2];
        readonly int[] counts = new int[SpeciesCount];
        Material material;
        int raidSeed, phase;
        float phaseAge, appliedAt, swarmAge, sampledAt, intensity, shade, impactAge = float.PositiveInfinity;
        Vector3 anchor, impactPoint;
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

        public void Apply(int seed,int phase,float phaseAge,float tension,Vector3 vesselCenter,float visibility,
            float shieldAge,Vector3 newestImpact)
        {
            if (seed != raidSeed && (phase == 0 || phase == 1) && Time.frameCount != clockFrame)
                hasSwarmClock = false;
            raidSeed = seed;
            this.phase = Mathf.Clamp(phase,0,5);
            this.phaseAge = Mathf.Max(0,phaseAge);
            appliedAt = Time.time;
            intensity = Mathf.Clamp01(tension);
            shade = Mathf.Clamp01(visibility);
            anchor = vesselCenter;
            impactAge = shieldAge;
            impactPoint = newestImpact;
        }

        int clockFrame;
        public void SetWarningOrigin(Vector3 worldPoint)
        {
            sceneRoarPoint = worldPoint;
            hasSceneRoarPoint = true;
        }

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
                matrices[i] = new Matrix4x4[MaxSlots];
                blocks[i,0] = new MaterialPropertyBlock();
                blocks[i,1] = new MaterialPropertyBlock();
            }
        }

        void LateUpdate()
        {
            VisibleCount = 0;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var look = UnderwaterLook.Instance;
            Camera eye = look ? look.DiverCamera : Camera.main;
            if (!eye || !eye.isActiveAndEnabled) return;
            var profile = Settings;
            Vector3 observer = eye.transform.position;
            float now = Time.time;
            float elapsed = phaseAge + Mathf.Max(0, now-appliedAt);
            float orbitAge = hasSwarmClock ? swarmAge + Mathf.Max(0,now-sampledAt) : elapsed;
            float volumeInWater = look && !look.EyeSubmerged ? .64f : 1f;
            UpdateAudio(profile,elapsed,orbitAge,observer,volumeInWater);

            if (phase == 0) return;
            BindAssets();
            float reveal = phase == 1 ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,5.5f,elapsed))
                : phase == 5 ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,4.4f,elapsed)) : 1;
            if (!material || reveal < .02f) return;
            RenderSwarm(profile,eye,observer,orbitAge,reveal);
        }

        void RenderSwarm(DefenseAtmosphereSettings profile,Camera eye,Vector3 observer,float orbitAge,float reveal)
        {
            System.Array.Clear(counts,0,counts.Length);
            float range = Mathf.Lerp(profile.clearDrawingDistance,profile.darkDrawingDistance,shade);
            int slots = Mathf.Clamp(profile.silhouetteCount,35,MaxSlots);
            float fleeRange = Mathf.Max(.01f,profile.giveWayDistance);
            for (int i = 0; i < slots; i++)
            {
                if ((promoted & (1UL << i)) != 0) continue;
                OrbitPose pose = SampleOrbit(raidSeed,i,orbitAge,anchor,profile);
                Vector3 point = pose.Position;
                Vector3 heading = pose.Forward;
                Vector3 away = point-observer;
                float distanceSquared = away.sqrMagnitude;
                if (distanceSquared > range*range) continue;
                if (distanceSquared < fleeRange*fleeRange)
                {
                    Vector3 swimOff = (away.normalized + pose.Forward*.44f).normalized;
                    float avoid = Mathf.Pow(1-Mathf.Sqrt(distanceSquared)/fleeRange,1.7f);
                    point += swimOff * (5.6f*avoid);
                    heading = Vector3.Slerp(heading,swimOff,avoid);
                }
                Vector3 view = eye.WorldToViewportPoint(point);
                if (view.z <= .01f || (view.z > 5f && (view.x < -.14f || view.x > 1.14f || view.y < -.18f || view.y > 1.18f))) continue;
                if (phase == 5) point += (pose.Position-anchor).normalized * (4.9f*(1-reveal));
                float roll = Mathf.Sin(orbitAge*(1.5f+.17f*pose.Kind)+i*.73f) * (8+pose.Kind*2.2f);
                Quaternion bank = Quaternion.LookRotation(heading) * Quaternion.AngleAxis(roll,Vector3.forward);
                matrices[pose.Kind][counts[pose.Kind]++] = Matrix4x4.TRS(point,bank,Vector3.one*pose.Scale);
            }
            for (int kind = 0; kind < SpeciesCount; kind++)
            {
                int count = counts[kind];
                Mesh mesh = meshes[kind];
                if (count == 0 || !mesh) continue;
                bool instanced = SystemInfo.supportsInstancing && material.enableInstancing;
                int rendered = instanced ? count : Mathf.Min(count,4);
                for (int surface = 0; surface < Mathf.Min(mesh.subMeshCount,2); surface++)
                {
                    var block = blocks[kind,surface];
                    block.SetFloat(EyePass,surface);
                    block.SetFloat(Fade,reveal);
                    block.SetFloat(Glow,profile.eyeGlow);
                    block.SetFloat(Range,range);
                    block.SetVector(Billow,new Vector4(SwimBeat[kind],FinFlex[kind],0,0));
                    var render = new RenderParams(material)
                    {
                        camera = eye,
                        matProps = block,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false,
                        lightProbeUsage = LightProbeUsage.Off,
                        layer = gameObject.layer
                    };
                    if (instanced) Graphics.RenderMeshInstanced(render,mesh,surface,matrices[kind],rendered);
                    else for (int i = 0; i < rendered; i++) Graphics.RenderMesh(render,mesh,surface,matrices[kind][i]);
                }
                VisibleCount += rendered;
            }
        }

        void OnDisable()
        {
            if (roarWorld) roarWorld.Stop();
            if (roarWide) roarWide.Stop();
            if (fieldLoop) fieldLoop.Stop();
            if (scoreLoop) scoreLoop.Stop();
            foreach (var voice in effects) if (voice) voice.Stop();
            VisibleCount = 0;
            audioObserved = false;
        }
    }
}
