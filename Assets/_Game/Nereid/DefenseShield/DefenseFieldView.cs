using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class DefenseFieldView : MonoBehaviour
    {
        const int RecentImpactCount = 3;
        const float ImpactLifetime = 1.75f;
        const float SparkLifetime = .83f;
        static readonly int ImpactPower = Shader.PropertyToID("_ImpactPower");
        static readonly int ImpactNormals = Shader.PropertyToID("_ImpactNormals");
        const int MaxPipeContacts = 12;
        static readonly int PipeContactCount = Shader.PropertyToID("_PipeContactCount");
        static readonly int PipePoints = Shader.PropertyToID("_PipePoints");
        static readonly int PipeAxes = Shader.PropertyToID("_PipeAxes");
        static readonly int FieldState = Shader.PropertyToID("_FieldState");
        static readonly int FieldRadii = Shader.PropertyToID("_FieldRadii");
        static readonly int BreakAxis = Shader.PropertyToID("_BreakAxis");
        static readonly int GeneratorState = Shader.PropertyToID("_GeneratorState");
        static readonly int[] ImpactProperties =
        {
            Shader.PropertyToID("_Impact0"), Shader.PropertyToID("_Impact1"), Shader.PropertyToID("_Impact2")
        };

        [Tooltip("Half width at the sill, depth below the stock centre, and half length in vessel metres.")]
        public Vector3 radii = new(7.13f, 4.05f, 4.70f);
        [Tooltip("Height of the flat opening above the stock centre, in vessel metres.")]
        [Min(0)] public float rimHeight = .885f;
        [SerializeField] internal MeshRenderer surface;
        [SerializeField] internal MeshRenderer sparks;
        [SerializeField] internal MeshRenderer[] generatorLenses;
        [SerializeField] internal MeshRenderer[] contactLenses;

        public float DepthBelowCenter => Mathf.Max(radii.y, .05f);

        internal Vector3 EnvelopeScale => new(Mathf.Max(radii.x, .05f), DepthBelowCenter + RimHeight,
            Mathf.Max(radii.z,.05f));
        internal float RimHeight => Mathf.Max(0, rimHeight);

        readonly Vector3[] impactDirections = new Vector3[RecentImpactCount];
        readonly float[] impactPowers = new float[RecentImpactCount];
        readonly Vector4[] impactNormals = new Vector4[RecentImpactCount];
        readonly Vector4[] pipePoints = new Vector4[MaxPipeContacts];
        readonly Vector4[] pipeAxes = new Vector4[MaxPipeContacts];
        int pipeContactCount;
        bool pipesReady;
        internal int PipeSealCount => pipeContactCount;
        internal Vector3 PipeSealWorld(int index) => transform.TransformPoint((Vector3)pipePoints[index]);
        readonly float[] impactTimes = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        MaterialPropertyBlock state;
        int lastSequence, impactSlot;
        float lastFormation = 1;
        float recovering;
        Vector3 breakAxis = Vector3.down;
        Vector3 presentedDimensions;

        const float KeelFore = .12f;
        internal const float AftEdge = -1.034f;

        internal static float SectionWidth(float depth) => Mathf.Sqrt(Mathf.Max(0,1-depth*depth));
        internal static float BowLimit(float depth) => KeelFore+(1-KeelFore)*SectionWidth(depth);
        internal static float DepthAt(float z) => Mathf.SmoothStep(.928f,1,Mathf.InverseLerp(-1,-.52f,z));
        internal static float BeamAt(float z) => Mathf.SmoothStep(.996f,1,Mathf.InverseLerp(-1,-.52f,z));
        internal static float BeamAtDepth(float depth,float z)
        {
            float hullCurl = SectionWidth(depth);
            float wideSide = .56f+.44f*Mathf.Pow(hullCurl,.38f);
            float blend = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1,-.92f,z));
            return BeamAt(z)*Mathf.Lerp(hullCurl,wideSide,blend);
        }

        internal static Vector3 RimDirection(float angle)
        {
            float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
            return new Vector3(x,0,z)/Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
        }

        Vector3 ToUnit(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint) - Vector3.up * RimHeight;
            var scale = EnvelopeScale;
            return new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z);
        }

        static Vector3 Boundary(Vector3 unit)
        {
            unit.y = Mathf.Min(-.004f, unit.y);
            float span = Mathf.Max(Mathf.Max(Mathf.Abs(unit.x),-unit.y),Mathf.Max(unit.z,unit.z/AftEdge));
            Vector3 point = unit / span;
            float low = 0, high = 1;
            for (int i = 0; i < 13; i++)
            {
                float middle = (low+high)*.5f;
                if (InsideSection(point*middle)) low = middle;
                else high = middle;
            }
            return point*((low+high)*.5f);
        }

        /// <summary>Tests the closed field volume without making a wall for the crew or their gear.</summary>
        public bool ContainsWorld(Vector3 point)
        {
            return InsideSection(ToUnit(point));
        }

        static bool InsideSection(Vector3 unit)
        {
            if (unit.y > 0 || unit.z <= AftEdge || unit.z >= BowLimit(-unit.y)) return false;
            float depth = -unit.y/DepthAt(unit.z);
            return depth < 1 && Mathf.Abs(unit.x) < BeamAtDepth(depth,unit.z);
        }

        /// <summary>Projects a source to the exposed membrane, optionally offset along its outward normal.</summary>
        public Vector3 ContactWorld(Vector3 source, float outwardOffset = 0)
        {
            Vector3 unit = Boundary(ToUnit(source));
            Vector3 scale = EnvelopeScale;
            var point = transform.TransformPoint(Vector3.Scale(unit, scale) + Vector3.up * RimHeight);
            if (outwardOffset == 0) return point;

            var normal = UnitNormal(unit);
            normal = new Vector3(normal.x/scale.x, normal.y/scale.y, normal.z/scale.z);
            return point + transform.worldToLocalMatrix.transpose.MultiplyVector(normal).normalized * outwardOffset;
        }

        internal static Vector3 UnitNormal(Vector3 point)
        {
            if (point.z <= AftEdge+.001f) return Vector3.back;
            if (point.z >= BowLimit(-point.y)-.001f) return BowNormal(-point.y);
            if (-point.y >= DepthAt(point.z)-.001f && Mathf.Abs(point.x) <= BeamAtDepth(1,point.z))
                return Vector3.down;
            return SideNormal(Mathf.Sign(point.x),-point.y/DepthAt(point.z),point.z);
        }

        internal static Vector3 SideNormal(float side,float depth,float z)
        {
            const float step = .003f;
            float vertical = (BeamAtDepth(Mathf.Min(1,depth+step),z)-BeamAtDepth(Mathf.Max(0,depth-step),z))/(2*step*DepthAt(z));
            float fore = -(BeamAtDepth(depth,z+step)-BeamAtDepth(depth,z-step))/(2*step);
            return new Vector3(side,vertical,fore).normalized;
        }

        internal static Vector3 BowNormal(float depth) => new Vector3(0,-(1-KeelFore)*depth,SectionWidth(depth)).normalized;

        void OnEnable()
        {
            lastSequence = impactSlot = 0;
            lastFormation = 1;
            recovering = 0;
            breakAxis = Vector3.down;
            presentedDimensions = Vector3.zero;
            pipesReady = false;
            for (int i = 0; i < RecentImpactCount; i++) impactTimes[i] = float.NegativeInfinity;
            Apply(1, 1, 0, Vector3.zero, float.PositiveInfinity);
        }

        /// <summary>Present the server's field state and most recent impact, with age in seconds.</summary>
        public void Apply(float integrity01, float formation01, int impactSequence, Vector3 impactWorldPoint, float impactAge, float impactStrength = 1)
        {
            if (!surface) return;

            float now = Time.time;
            integrity01 = Mathf.Clamp01(integrity01);
            formation01 = Mathf.Clamp01(formation01);
            var dimensions = EnvelopeScale;
            var rim = Vector3.up * RimHeight;
            if (presentedDimensions != dimensions || surface.transform.localScale != dimensions || surface.transform.localPosition != rim)
            {
                presentedDimensions = dimensions;
                surface.transform.localScale = dimensions;
                surface.transform.localPosition = rim;
                if (sparks) sparks.localBounds = new Bounds(Vector3.zero, (dimensions + Vector3.one * 2.6f) * 2);
                pipesReady = false;
            }
            if (!pipesReady) CachePipeSeals();

            if (impactSequence == 0 && lastSequence != 0)
            {
                lastSequence = impactSlot = 0;
                for (int i = 0; i < RecentImpactCount; i++) impactTimes[i] = float.NegativeInfinity;
            }
            else if (impactSequence != lastSequence)
            {
                lastSequence = impactSequence;
                if (impactAge >= 0 && impactAge < ImpactLifetime)
                {
                    impactDirections[impactSlot] = Boundary(ToUnit(impactWorldPoint));
                    impactPowers[impactSlot] = Mathf.Clamp(impactStrength,.12f,1.25f);
                    Vector3 n = UnitNormal(impactDirections[impactSlot]);
                    impactNormals[impactSlot] = new Vector3(n.x/dimensions.x,n.y/dimensions.y,n.z/dimensions.z).normalized;
                    breakAxis = impactDirections[impactSlot];
                    impactTimes[impactSlot] = now - impactAge;
                    impactSlot = (impactSlot + 1) % RecentImpactCount;
                }
            }

            if (Mathf.Abs(formation01 - lastFormation) > .0001f)
                recovering = formation01 > lastFormation ? 1 : 0;
            lastFormation = formation01;

            state ??= new MaterialPropertyBlock();
            state.Clear();
            state.SetVector(FieldState, new Vector4(integrity01, formation01, recovering, 0));
            state.SetVector(FieldRadii, new Vector4(dimensions.x, dimensions.y, dimensions.z, RimHeight));
            state.SetVector(ImpactPower, new Vector4(impactPowers[0],impactPowers[1],impactPowers[2],0));
            state.SetVectorArray(ImpactNormals,impactNormals);
            state.SetFloat(PipeContactCount,pipeContactCount);
            if (pipeContactCount > 0)
            {
                state.SetVectorArray(PipePoints,pipePoints);
                state.SetVectorArray(PipeAxes,pipeAxes);
            }
            state.SetVector(BreakAxis, breakAxis);
            bool activeSparks = false;
            float lastHitAge = ImpactLifetime;
            for (int i = 0; i < RecentImpactCount; i++)
            {
                float age = Mathf.Min(ImpactLifetime + 1, now - impactTimes[i]);
                Vector3 direction = impactDirections[i];
                state.SetVector(ImpactProperties[i], new Vector4(direction.x, direction.y, direction.z, age));
                activeSparks |= age < SparkLifetime;
                lastHitAge = Mathf.Min(lastHitAge, age);
            }
            bool activeSurface = formation01 > .001f;
            if (surface.enabled != activeSurface) surface.enabled = activeSurface;
            if (activeSurface) surface.SetPropertyBlock(state);
            if (sparks)
            {
                if (sparks.enabled != activeSparks) sparks.enabled = activeSparks;
                if (activeSparks) sparks.SetPropertyBlock(state);
            }

            state.Clear();
            state.SetVector(GeneratorState, new Vector4(integrity01, formation01, recovering, lastHitAge));
            ApplyLenses(generatorLenses);
            ApplyLenses(contactLenses);
        }

        void CachePipeSeals()
        {
            pipesReady = true;
            pipeContactCount = 0;
            var vessel = GetComponentInParent<NereidVessel>(true);
            if (!vessel) return;
            foreach (var route in vessel.GetComponentsInChildren<NereidPipeRoute>(true))
            {
                if (!route.IsConnected || !route.gameObject.activeInHierarchy) continue;
                int samples = Mathf.Clamp(Mathf.CeilToInt(route.Length/.12f),16,160);
                bool lastInside = ContainsWorld(route.WorldPoint(0));
                for (int i = 1; i <= samples && pipeContactCount < MaxPipeContacts; i++)
                {
                    float to = (float)i/samples;
                    bool inside = ContainsWorld(route.WorldPoint(to));
                    if (lastInside == inside) continue;
                    float from = (float)(i-1)/samples;
                    for (int step = 0; step < 7; step++)
                    {
                        float middle = (from+to)*.5f;
                        if (ContainsWorld(route.WorldPoint(middle)) == inside) to = middle;
                        else from = middle;
                    }
                    lastInside = inside;
                    float t = (from+to)*.5f;
                    Vector3 local = transform.InverseTransformPoint(route.WorldPoint(t));
                    Vector3 axis = transform.InverseTransformVector(route.WorldPoint(t+.003f)-route.WorldPoint(t-.003f)).normalized;
                    float radius = transform.InverseTransformVector(route.transform.TransformVector(Vector3.right*route.radius)).magnitude;
                    if (axis.sqrMagnitude < .5f || local.y >= RimHeight-.03f &&
                        Mathf.Abs(local.x) < radii.x-radius && local.z > -radii.z+radius && local.z < radii.z-radius) continue;
                    pipePoints[pipeContactCount] = new Vector4(local.x,local.y,local.z,radius);
                    pipeAxes[pipeContactCount++] = axis;
                }
            }
        }

        void ApplyLenses(MeshRenderer[] lenses)
        {
            if (lenses == null) return;
            foreach (var lens in lenses)
                if (lens) lens.SetPropertyBlock(state);
        }

        void OnDrawGizmosSelected()
        {
            var saved = Gizmos.matrix;
            var color = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(Vector3.up * RimHeight,
                Quaternion.identity, EnvelopeScale);
            Gizmos.color = new Color(.26f, .85f, .84f, .75f);
            const int steps = 40;
            for(int i=0;i<steps;i++)
            {
                float a = 2 * Mathf.PI * i / steps, b = 2 * Mathf.PI * (i+1) / steps;
                Gizmos.DrawLine(RimDirection(a),RimDirection(b));
                if (i >= steps / 2) continue;
                Gizmos.DrawLine(new Vector3(Mathf.Cos(a),-Mathf.Sin(a),0),new Vector3(Mathf.Cos(b),-Mathf.Sin(b),0));
                float depthA=Mathf.Sin(a), depthB=Mathf.Sin(b);
                Gizmos.DrawLine(new Vector3(0,-depthA,BowLimit(depthA)),new Vector3(0,-depthB,BowLimit(depthB)));
            }
            Gizmos.DrawLine(new Vector3(0,0,AftEdge),new Vector3(0,-DepthAt(AftEdge),AftEdge));
            Gizmos.DrawLine(new Vector3(0,-DepthAt(AftEdge),AftEdge),new Vector3(0,-1,-.52f));
            Gizmos.DrawLine(new Vector3(0,-1,-.52f),new Vector3(0,-1,KeelFore));
            Gizmos.matrix = saved;
            Gizmos.color = color;
        }
    }
}
