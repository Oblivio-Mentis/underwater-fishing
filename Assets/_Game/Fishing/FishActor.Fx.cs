using UnityEngine;

namespace Raid
{
    // Client-only presentation driven by the replicated snapshot. Sends nothing, decides nothing.
    public sealed partial class FishActor
    {
        ParticleSystem surgeBubbles, siltCloud;
        Material bubbleMaterial, siltMaterial;
        int fxTugSequence, fxHookSequence;
        float siltCooldown;
        static readonly RaycastHit[] fxHits = new RaycastHit[4];

        void PresentFx(FishSnapshot state, bool visible)
        {
            if (!visible) { if (surgeBubbles != null) { var e = surgeBubbles.emission; e.rateOverTime = 0; } return; }
            if (surgeBubbles == null && !CreateFx()) return;
            float dt = Time.deltaTime;
            bool fighting = state.Mode >= FishMode.Hooked && state.Mode <= FishMode.Tired;
            // Bubbles and suspended matter only while the body works hard; quiet phases stay clean.
            float effort = state.Behaviour switch
            {
                FishBehaviour.Surge => 1f,
                FishBehaviour.Flee or FishBehaviour.LastEscape => .7f,
                FishBehaviour.Lateral => .3f,
                _ => fighting ? .12f : 0f
            } * (RequiresPartner ? 1.5f : 1f);
            var emission = surgeBubbles.emission;
            emission.rateOverTime = effort * 26f;
            surgeBubbles.transform.localPosition = new Vector3(0, .06f, .35f) * (RequiresPartner ? 1.4f : 1);
            if (state.HookSequence != fxHookSequence)
            {
                fxHookSequence = state.HookSequence;
                if (fxHookSequence > 0) surgeBubbles.Emit(RequiresPartner ? 14 : 8);
            }
            if (state.TugSequence != fxTugSequence)
            {
                fxTugSequence = state.TugSequence;
                if (fxTugSequence > 0) surgeBubbles.Emit(RequiresPartner ? 22 : 12);
            }
            siltCooldown -= dt;
            if (siltCooldown <= 0 && effort > .25f)
            {
                siltCooldown = .55f;
                int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, fxHits, RequiresPartner ? 1.1f : .7f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = fxHits[i];
                    if (hit.collider.GetComponentInParent<FishActor>() != null || hit.collider.GetComponentInParent<DiverMotor>() != null
                        || hit.normal.y < .5f) continue;
                    siltCloud.transform.SetPositionAndRotation(hit.point + hit.normal * .08f, Quaternion.FromToRotation(Vector3.up, hit.normal));
                    siltCloud.Emit(Mathf.RoundToInt(10 + effort * 18));
                    break;
                }
            }
        }

        bool CreateFx()
        {
            var bubbleSource = Resources.Load<Material>("NereidBubbles");
            var siltShader = Resources.Load<Shader>("NereidSilt");
            if (bubbleSource == null || siltShader == null) return false;
            bubbleMaterial = new Material(bubbleSource) { name = "Fish effort bubbles" };
            bubbleMaterial.SetFloat("_ParticleTint", 1);
            siltMaterial = new Material(siltShader) { name = "Fish bottom silt" };

            var bubbles = new GameObject("Effort bubbles");
            bubbles.transform.SetParent(transform, false);
            surgeBubbles = bubbles.AddComponent<ParticleSystem>();
            surgeBubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = surgeBubbles.main;
            main.startColor = new Color(.68f, .84f, .84f, .55f);
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .06f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .45f);
            main.maxParticles = 48; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var shape = surgeBubbles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .09f;
            var velocity = surgeBubbles.velocityOverLifetime; velocity.enabled = true; velocity.y = .5f;
            var fade = surgeBubbles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var emission = surgeBubbles.emission; emission.rateOverTime = 0;
            var renderer = bubbles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = bubbleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            surgeBubbles.Play();

            var silt = new GameObject("Bottom silt");
            silt.transform.SetParent(transform.parent, true);
            siltCloud = silt.AddComponent<ParticleSystem>();
            siltCloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = siltCloud.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.27f, .31f, .23f, .5f), new Color(.46f, .43f, .31f, .32f));
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .7f);
            main.maxParticles = 40; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            shape = siltCloud.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .3f; shape.angle = 32; shape.rotation = new Vector3(-90, 0, 0);
            fade = siltCloud.colorOverLifetime; fade.enabled = true;
            var mud = new Gradient();
            mud.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.8f, .2f), new GradientAlphaKey(0, 1) });
            fade.color = mud;
            emission = siltCloud.emission; emission.rateOverTime = 0;
            siltCloud.GetComponent<ParticleSystemRenderer>().sharedMaterial = siltMaterial;
            siltCloud.Play();
            return true;
        }

        void DisposeFx()
        {
            if (siltCloud != null) Destroy(siltCloud.gameObject);
            if (bubbleMaterial != null) Destroy(bubbleMaterial);
            if (siltMaterial != null) Destroy(siltMaterial);
        }
    }
}
