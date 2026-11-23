using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class FishingPullEdges : MaskableGraphic
    {
        Vector2 pull;

        internal void Show(Vector2 direction, float strength)
        {
            var next = direction * Mathf.Clamp01(strength);
            next.y = Mathf.Min(0, next.y);
            if ((next - pull).sqrMagnitude < .00001f && (next == Vector2.zero) == (pull == Vector2.zero)) return;
            pull = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            var start = new Vector2(pull.x > 0 ? r.xMax : r.xMin, r.yMin);
            Strip(mesh, start, Vector2.up * r.height * .85f, Vector2.left * Mathf.Sign(pull.x) * r.width * .18f, Mathf.Abs(pull.x));
            Strip(mesh, r.min, Vector2.right * r.width, Vector2.up * r.height * .18f, -pull.y);
        }

        static void Strip(VertexHelper mesh, Vector2 start, Vector2 along, Vector2 inward, float strength)
        {
            if (strength < .002f) return;
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float taper = Mathf.Sin(t * Mathf.PI);
                Vector2 edge = start + along * t;
                int index = mesh.currentVertCount;
                mesh.AddVert(edge, new Color(.72f, .96f, 1, strength * taper * .62f), Vector2.zero);
                mesh.AddVert(edge + inward * taper, Color.clear, Vector2.zero);
                if (i == 0) continue;
                mesh.AddTriangle(index - 2, index - 1, index);
                mesh.AddTriangle(index - 1, index + 1, index);
            }
        }
    }
}
