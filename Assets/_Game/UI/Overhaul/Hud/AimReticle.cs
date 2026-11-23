using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer)), DefaultExecutionOrder(300)]
    public sealed class AimReticle : MaskableGraphic
    {
        public Color ContextColor = UiTheme.Text;
        static readonly Color32 Outline = new(5, 14, 17, 223);
        static readonly Vector2[] Axes = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        IUiController app;
        DiverMotor observed;
        Camera eye;
        int sequence;
        bool dotOnly;
        float gap = 4f, kick;
        Color32 ink;
        Color confirmedColor;
        float confirmedAt = -1f, confirmedStrength;

        public void Hit(Color tint, float strength)
        {
            confirmedAt = Time.unscaledTime;
            confirmedColor = tint;
            confirmedStrength = Mathf.Clamp(strength, .2f, 1.6f);
        }

        public void Initialize(IUiController controller)
        {
            app = controller;
            raycastTarget = false;
            ink = ContextColor;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            observed = null;
            kick = 0f;
        }

        void LateUpdate()
        {
            var ctx = app?.Context;
            if (ctx == null || !ctx.Connected) return;
            var tool = ctx.Harpoon;
            var player = ctx.Player;
            if (observed != player)
            {
                observed = player;
                eye = player.GetComponentInChildren<Camera>();
                sequence = tool != null ? tool.ShotSequence : 0;
            }
            if (tool != null && sequence != tool.ShotSequence)
            {
                sequence = tool.ShotSequence;
                kick = 3.5f;
            }
            kick = Mathf.MoveTowards(kick, 0f, Time.unscaledDeltaTime * 19f);
            bool point = UiInputGate.PointerReserved || tool == null || !tool.HarpoonInHands;
            float nextGap = Mathf.Lerp(4.2f, 2.5f, Nereid.NereidAim.Blend) + kick;
            Color32 nextInk = ContextColor;
            if (!point && eye && tool.tuning != null &&
                Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, tool.tuning.maxRange,
                    tool.tuning.hitMask, QueryTriggerInteraction.Ignore))
            {
                var teammate = hit.collider.GetComponentInParent<DiverMotor>();
                if (teammate && teammate != player) nextInk = UiTheme.Green;
            }
            float age = Time.unscaledTime - confirmedAt;
            if (!point && age < .32f)
            {
                float pulse = confirmedStrength * Mathf.Exp(-10f * age);
                nextGap += 8f * pulse;
                nextInk = Color.Lerp(nextInk, confirmedColor, Mathf.Clamp01(pulse));
            }
            if (point == dotOnly && Mathf.Abs(nextGap - gap) < .04f && nextInk.Equals(ink)) return;
            dotOnly = point;
            gap = nextGap;
            ink = nextInk;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!dotOnly)
            {
                foreach (Vector2 axis in Axes)
                {
                    Stroke(mesh, axis, gap - 1f, gap + 8.5f, 3.6f, Outline);
                    Stroke(mesh, axis, gap, gap + 7.5f, 1.7f, ink);
                }
            }
            float radius = dotOnly ? 2.35f : 1.65f;
            Stroke(mesh, Vector2.up, -radius - .9f, radius + .9f, radius * 2f + 1.8f, Outline);
            Stroke(mesh, Vector2.up, -radius, radius, radius * 2f, ink);
        }

        static void Stroke(VertexHelper mesh, Vector2 direction, float from, float to, float width, Color32 tint)
        {
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * .5f);
            Vector2 tail = direction * from, tip = direction * to;
            int first = mesh.currentVertCount;
            mesh.AddVert(tail - side, tint, Vector2.zero);
            mesh.AddVert(tail + side, tint, Vector2.zero);
            mesh.AddVert(tip + side, tint, Vector2.zero);
            mesh.AddVert(tip - side, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
