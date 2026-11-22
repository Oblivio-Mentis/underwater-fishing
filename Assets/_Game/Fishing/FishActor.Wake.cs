using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        bool lastVisualActive, wakeSampled;
        Vector3 wakePosition;
        float visualSwimSpeed;
        TrailRenderer[] wakeTrails;
        Material wakeMaterial;

        void PresentWake(FishSnapshot state,bool visible,bool snap)
        {
            Vector3 position = transform.position;
            float distance = Vector3.Distance(position,wakePosition);
            float dt = Time.deltaTime;
            bool swimming = state.Mode == FishMode.Roam || state.Mode == FishMode.Escaped || state.Mode == FishMode.Struggling;
            bool reset = !visible || !swimming || snap || !wakeSampled || distance > 4 || dt <= 0;
            wakeSampled = visible && swimming;
            wakePosition = position;
            if (reset)
            {
                visualSwimSpeed = 0;
                if (wakeTrails != null)
                    foreach (var trail in wakeTrails) { trail.emitting = false; trail.Clear(); }
                return;
            }

            visualSwimSpeed = Mathf.Lerp(visualSwimSpeed,distance / dt,1 - Mathf.Exp(-dt * 14));
            float intensity = state.Surging ? Mathf.InverseLerp(.9f,4.2f,visualSwimSpeed) :
                Mathf.InverseLerp(2.35f,5.4f,visualSwimSpeed);
            if (wakeTrails == null && intensity >= .1f) CreateWake();
            if (wakeTrails == null) return;
            float body = RequiresPartner ? 1.4f : 1;
            for (int i = 0; i < wakeTrails.Length; i++)
            {
                var trail = wakeTrails[i];
                float side = i == 0 ? -1 : 1;
                float sweep = Mathf.Sin(Time.time * (state.Surging ? 17 : 12) + i * .7f) * .085f;
                trail.transform.localPosition = new Vector3(side * .14f + sweep,i == 0 ? .08f : -.04f,-.52f) * body;
                trail.emitting = intensity > .095f;
                trail.widthMultiplier = (.12f + intensity * .24f) * body * (i == 0 ? 1 : .64f);
            }
        }

        void CreateWake()
        {
            var shader = Resources.Load<Shader>("NereidFishWake");
            if (shader == null) return;
            wakeMaterial = new Material(shader);
            wakeTrails = new TrailRenderer[2];
            for (int i = 0; i < wakeTrails.Length; i++)
            {
                var emitter = new GameObject("Fish wake");
                emitter.transform.SetParent(transform,false);
                var trail = emitter.AddComponent<TrailRenderer>();
                trail.sharedMaterial = wakeMaterial;
                trail.time = i == 0 ? .66f : .43f;
                trail.minVertexDistance = .055f;
                trail.alignment = LineAlignment.View;
                trail.numCapVertices = 3;
                trail.numCornerVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.widthCurve = new AnimationCurve(new Keyframe(0,.38f),new Keyframe(.19f,.9f),new Keyframe(1,0));
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] {new GradientColorKey(new Color(.58f,.94f,1),0),new GradientColorKey(new Color(.29f,.67f,.75f),1)},
                    alphaKeys = new[] {new GradientAlphaKey(.63f,0),new GradientAlphaKey(.46f,.34f),new GradientAlphaKey(0,1)}
                };
                trail.emitting = false;
                wakeTrails[i] = trail;
            }
        }

        void DisposeWake()
        {
            if (wakeMaterial != null) Destroy(wakeMaterial);
        }
    }
}
