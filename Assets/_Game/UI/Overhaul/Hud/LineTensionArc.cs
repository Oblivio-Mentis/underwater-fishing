using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // Local coordinates are centred on the same canvas point as the aiming ray.
    [DisallowMultipleComponent]
    public sealed class LineTensionArc : MaskableGraphic
    {
        const float Radius = 104f;
        internal const float Caution = .60f;
        internal const float Danger = .82f;
        static readonly Color32 Underlay = new(8, 23, 28, 223);
        static readonly Color32 Easy = new(102, 224, 159, 255);
        static readonly Color32 Loaded = new(255, 202, 83, 255);
        static readonly Color32 Critical = new(255, 100, 92, 255);
        static readonly Color32 Ivory = new(247, 251, 245, 255);
        float load;

        public void ShowLoad(float value)
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Abs(load - next) < .0005f) return;
            load = next;
            SetVerticesDirty();
        }

        static Vector2 Radial(float fraction)
        {
            float angle = Mathf.Lerp(173f, 86f, fraction) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Ring(mesh, 0f, 1f, 7f, Underlay);
            Ring(mesh, 0f, Caution - .006f, 3.4f, Easy);
            Ring(mesh, Caution + .006f, Danger - .006f, 3.4f, Loaded);
            Ring(mesh, Danger + .006f, 1f, 4.2f, Critical);

            // Fixed index marks distinguish high load even without colour.
            for (int i = 0; i <= 2; i++)
                Tick(mesh, Mathf.Lerp(Danger, 1f, i * .5f), 3f, 8f, 2.3f, Critical);

            Tick(mesh, load, -8.5f, 11f, 6f, Underlay);
            Tick(mesh, load, -7f, 9.5f, 2.8f, Ivory);
        }

        static void Ring(VertexHelper mesh, float from, float to, float thickness, Color32 ink)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) * 36f));
            int start = mesh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 ray = Radial(Mathf.Lerp(from, to, (float)i / steps));
                mesh.AddVert(ray * (Radius - thickness * .5f), ink, Vector2.zero);
                mesh.AddVert(ray * (Radius + thickness * .5f), ink, Vector2.zero);
                if (i == 0) continue;
                int previous = start + (i - 1) * 2;
                mesh.AddTriangle(previous, previous + 1, previous + 3);
                mesh.AddTriangle(previous, previous + 3, previous + 2);
            }
        }

        static void Tick(VertexHelper mesh, float fraction, float inner, float outer, float width, Color32 ink)
        {
            Vector2 ray = Radial(fraction);
            Vector2 side = new Vector2(-ray.y, ray.x) * (width * .5f);
            Vector2 a = ray * (Radius + inner), b = ray * (Radius + outer);
            int start = mesh.currentVertCount;
            mesh.AddVert(a - side, ink, Vector2.zero);
            mesh.AddVert(a + side, ink, Vector2.zero);
            mesh.AddVert(b + side, ink, Vector2.zero);
            mesh.AddVert(b - side, ink, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
