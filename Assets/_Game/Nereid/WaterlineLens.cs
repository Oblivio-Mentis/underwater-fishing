using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    // Owned-camera waterline: a near-plane quad that bends the image along the flood surface,
    // rushes bubbles when the eye goes under and beads drops on the glass when it comes out.
    // Visual only: reads the airlock level, never changes it.
    [DefaultExecutionOrder(965)]
    public sealed class WaterlineLens : MonoBehaviour
    {
        static readonly int LineId = Shader.PropertyToID("_Line"), StateId = Shader.PropertyToID("_State");
        Camera view;
        MeshRenderer quad;
        Material material;
        bool wasWet, hasHistory;
        float rush, drying, submerged;

        public static WaterlineLens Attach(Camera camera)
        {
            var lens = camera.GetComponent<WaterlineLens>();
            if (lens == null) lens = camera.gameObject.AddComponent<WaterlineLens>();
            lens.view = camera;
            return lens;
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("WaterlineLens");
            if (shader == null) return;
            material = new Material(shader) { name = "Owned waterline lens", hideFlags = HideFlags.DontSave };
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Waterline lens";
            go.hideFlags = HideFlags.DontSave;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(view.transform, false);
            quad = go.GetComponent<MeshRenderer>();
            quad.sharedMaterial = material;
            quad.shadowCastingMode = ShadowCastingMode.Off;
            quad.receiveShadows = false;
            quad.lightProbeUsage = LightProbeUsage.Off;
            quad.enabled = false;
        }

        void LateUpdate()
        {
            if (view == null) { enabled = false; return; }
            if (quad == null) { Build(); if (quad == null) { enabled = false; return; } }
            float dt = Time.unscaledDeltaTime;
            var chamber = Nereid.NereidAirlock.Active;
            Vector3 eye = view.transform.position;
            bool inChamber = chamber != null && chamber.Contains(eye);
            bool wet = inChamber ? chamber.IsSubmerged(eye) : UnderwaterLook.Instance != null && UnderwaterLook.Instance.EyeSubmerged;
            if (hasHistory && wet != wasWet)
            {
                if (wet) rush = 1f; else drying = 1f;
            }
            hasHistory = true; wasWet = wet;
            rush = Mathf.MoveTowards(rush, 0, dt / 1.6f);
            drying = Mathf.MoveTowards(drying, 0, dt / 6f);
            submerged = Mathf.MoveTowards(submerged, wet ? 1 : 0, dt * 1.5f);

            // Place the quad just past the near plane, sized to the frustum.
            float distance = view.nearClipPlane + .02f;
            float height = 2f * distance * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            quad.transform.localPosition = Vector3.forward * distance;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(height * view.aspect * 1.02f, height * 1.02f, 1);

            Vector4 line = new Vector4(0, 1, wet ? 2 : -2, .02f); // off-screen: wholly under or wholly above
            bool lineVisible = false;
            if (inChamber)
            {
                float h = chamber.WaterHeight;
                Vector3 fwd = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < .01f) fwd = Vector3.ProjectOnPlane(view.transform.up, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 a = new Vector3(eye.x, h, eye.z) + fwd * .35f - right * .6f;
                Vector3 b = new Vector3(eye.x, h, eye.z) + fwd * .35f + right * .6f;
                Vector3 c = new Vector3(eye.x, h, eye.z) + fwd * .35f - Vector3.up * .3f;
                Vector3 va = view.WorldToViewportPoint(a), vb = view.WorldToViewportPoint(b), vc = view.WorldToViewportPoint(c);
                if (va.z > 0 && vb.z > 0 && Mathf.Abs(eye.y - h) < 1.6f)
                {
                    Vector2 d = new Vector2(vb.x - va.x, vb.y - va.y);
                    if (d.sqrMagnitude > 1e-6f)
                    {
                        Vector2 n = new Vector2(-d.y, d.x).normalized;
                        float cc = -Vector2.Dot(n, new Vector2(va.x, va.y));
                        float sideC = Vector2.Dot(n, new Vector2(vc.x, vc.y)) + cc;
                        if (sideC < 0) { n = -n; cc = -cc; }
                        line = new Vector4(n.x, n.y, cc, .018f + .012f * Mathf.Abs(n.x));
                        lineVisible = true;
                    }
                }
            }
            bool active = lineVisible || rush > 0 || drying > 0;
            quad.enabled = active;
            if (!active) return;
            material.SetVector(LineId, line);
            material.SetVector(StateId, new Vector4(submerged, rush, drying, view.aspect));
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
