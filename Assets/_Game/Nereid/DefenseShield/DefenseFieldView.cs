using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class DefenseFieldView : MonoBehaviour
    {
        const int RecentImpactCount = 3;
        const float ImpactLifetime = 1.42f;
        const float SparkLifetime = .68f;
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
        readonly float[] impactTimes = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        MaterialPropertyBlock state;
        int lastSequence, impactSlot;
        float lastFormation = 1;
        float recovering;
        Vector3 breakAxis = Vector3.down;
        Vector3 presentedDimensions;

        // The mesh and server queries use the same cross-section: a sixth-power rounded
        // rectangle in plan, curved in Y like a half ellipse and closed at the hull sill.
        static float HorizontalRadius(Vector3 unit)
        {
            float x2 = unit.x * unit.x, z2 = unit.z * unit.z;
            return Mathf.Pow(x2*x2*x2 + z2*z2*z2, 1f/6);
        }

        internal static Vector3 RimDirection(float angle)
        {
            float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
            return new Vector3(Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x),1f/3), 0,
                Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z),1f/3));
        }

        Vector3 ToUnit(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint) - Vector3.up * RimHeight;
            var scale = EnvelopeScale;
            return new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z);
        }

        static Vector3 Boundary(Vector3 unit)
        {
            unit.y = Mathf.Min(0, unit.y);
            float horizontal = HorizontalRadius(unit);
            float distance = Mathf.Sqrt(horizontal*horizontal + unit.y*unit.y);
            return distance > .0001f ? unit / distance : Vector3.down;
        }

        /// <summary>Tests the closed field volume without making a wall for the crew or their gear.</summary>
        public bool ContainsWorld(Vector3 point)
        {
            var unit = ToUnit(point);
            float horizontal = HorizontalRadius(unit);
            return unit.y <= 0 && horizontal*horizontal + unit.y*unit.y < 1;
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
            float x2 = point.x * point.x, z2 = point.z * point.z;
            float horizontal = HorizontalRadius(point);
            float h2 = horizontal * horizontal;
            float h4 = Mathf.Max(.0001f,h2*h2);
            return new Vector3(x2*x2*point.x/h4, point.y, z2*z2*point.z/h4).normalized;
        }

        void OnEnable()
        {
            lastSequence = impactSlot = 0;
            lastFormation = 1;
            recovering = 0;
            breakAxis = Vector3.down;
            presentedDimensions = Vector3.zero;
            for (int i = 0; i < RecentImpactCount; i++) impactTimes[i] = float.NegativeInfinity;
            Apply(1, 1, 0, Vector3.zero, float.PositiveInfinity);
        }

        /// <summary>Present the server's field state and most recent impact, with age in seconds.</summary>
        public void Apply(float integrity01, float formation01, int impactSequence, Vector3 impactWorldPoint, float impactAge)
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
            }

            if (impactSequence != 0 && impactSequence != lastSequence)
            {
                lastSequence = impactSequence;
                if (impactAge >= 0 && impactAge < ImpactLifetime)
                {
                    impactDirections[impactSlot] = Boundary(ToUnit(impactWorldPoint));
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
                Gizmos.DrawLine(new Vector3(0,-Mathf.Sin(a),Mathf.Cos(a)),new Vector3(0,-Mathf.Sin(b),Mathf.Cos(b)));
            }
            Gizmos.matrix = saved;
            Gizmos.color = color;
        }
    }
}
