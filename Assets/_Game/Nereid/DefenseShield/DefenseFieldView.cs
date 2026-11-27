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

        [Tooltip("Semi-axes of the field in local metres. Keep the root at the centre of the swimming volume.")]
        public Vector3 radii = new(2.52f, 1.28f, 4.88f);
        [SerializeField] internal MeshRenderer surface;
        [SerializeField] internal MeshRenderer sparks;
        [SerializeField] internal MeshRenderer[] generatorLenses;

        readonly Vector3[] impactDirections = new Vector3[RecentImpactCount];
        readonly float[] impactTimes = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        MaterialPropertyBlock state;
        int lastSequence, impactSlot;
        float lastFormation = 1;
        float recovering;
        Vector3 breakAxis = Vector3.down;

        void OnEnable()
        {
            lastSequence = impactSlot = 0;
            lastFormation = 1;
            recovering = 0;
            breakAxis = Vector3.down;
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
            var dimensions = Vector3.Max(radii, Vector3.one * .05f);
            if (surface.transform.localScale != dimensions)
            {
                surface.transform.localScale = dimensions;
                if (sparks) sparks.localBounds = new Bounds(Vector3.zero, (dimensions + Vector3.one * 2.6f) * 2);
            }

            if (impactSequence != 0 && impactSequence != lastSequence)
            {
                lastSequence = impactSequence;
                if (impactAge >= 0 && impactAge < ImpactLifetime)
                {
                    var local = transform.InverseTransformPoint(impactWorldPoint);
                    var unit = new Vector3(local.x / dimensions.x, local.y / dimensions.y, local.z / dimensions.z);
                    impactDirections[impactSlot] = unit.sqrMagnitude > .0001f ? unit.normalized : Vector3.down;
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
            state.SetVector(FieldRadii, dimensions);
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

            if (generatorLenses == null) return;
            state.Clear();
            state.SetVector(GeneratorState, new Vector4(integrity01, formation01, recovering, lastHitAge));
            foreach (var lens in generatorLenses)
                if (lens) lens.SetPropertyBlock(state);
        }

        void OnDrawGizmosSelected()
        {
            var saved = Gizmos.matrix;
            var color = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.Max(radii, Vector3.one * .05f));
            Gizmos.color = new Color(.26f, .85f, .84f, .75f);
            Gizmos.DrawWireSphere(Vector3.zero, 1);
            Gizmos.matrix = saved;
            Gizmos.color = color;
        }
    }
}
