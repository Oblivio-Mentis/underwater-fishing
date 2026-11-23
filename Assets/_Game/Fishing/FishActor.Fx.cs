using UnityEngine;

namespace Raid
{
    // Client-only presentation driven by the replicated snapshot. Sends nothing, decides nothing.
    // A fighting fish throws a cloud: a dense body-hugging bubble sheath while it works,
    // a bright tail spray on every stroke, a flank sweep of fines when it turns hard,
    // and a bottom silt bloom when it thrashes near the floor. Quiet phases stay clean.
    public sealed partial class FishActor
    {
        ParticleSystem sheath, tailSpray, flankSweep, siltCloud, churn;
        Material bubbleMaterial, siltMaterial, sprayMaterial, foamMaterial;
        int fxTugSequence, fxHookSequence;
        float siltCooldown, strokeClock, lastStroke, fxEffort;
        float hookFlash, tugFlash, beatPulse;
        FishMode fxMode;
        static readonly RaycastHit[] fxHits = new RaycastHit[4];
        Vector3 fxLastPosition, fxVelocity;

        float BodyLength => RequiresPartner ? 1.5f : 1f;

        void PresentFx(FishSnapshot state, bool visible)
        {
            if (!visible)
            {
                if (sheath != null)
                {
                    // The catch reaches a hand: one burst where the body was, then silence.
                    if (state.Mode == FishMode.Captured && fxMode != FishMode.Captured)
                    { sheath.Emit(RequiresPartner ? 260 : 170); tailSpray.Emit(RequiresPartner ? 160 : 110); churn.Emit(RequiresPartner ? 40 : 28); }
                    Quiet();
                }
                fxMode = state.Mode;
                ApplyFlash(0);
                return;
            }
            fxMode = state.Mode;
            if (sheath == null && !CreateFx()) return;
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            Vector3 position = transform.position;
            Vector3 step = position - fxLastPosition;
            fxLastPosition = position;
            float speed = step.magnitude / dt;
            fxVelocity = Vector3.Lerp(fxVelocity, step / dt, 1 - Mathf.Exp(-dt * 8));
            bool fighting = state.Mode >= FishMode.Hooked && state.Mode <= FishMode.Tired;
            float effort = state.Behaviour switch
            {
                FishBehaviour.Surge => 1f,
                FishBehaviour.LastEscape => 1f,
                FishBehaviour.Flee => .75f,
                FishBehaviour.Lateral => .55f,
                FishBehaviour.HookShock => .7f,
                FishBehaviour.SurgeWindup => .35f,
                FishBehaviour.Weakened => .18f,
                _ => fighting ? .22f : Mathf.InverseLerp(2.2f, 4.5f, speed) * .5f
            };
            fxEffort = Mathf.Lerp(fxEffort, effort, 1 - Mathf.Exp(-dt * 6));
            float body = BodyLength;

            // Sheath: continuous while working. Rate scales with effort and body size.
            var em = sheath.emission;
            em.rateOverTime = fxEffort * 430f * body;
            // Churn: a soft white cloud boiling off the body while it fights hard.
            var ce = churn.emission;
            ce.rateOverTime = Mathf.Max(0, fxEffort - .3f) * 30f * body;
            var cs = churn.shape; cs.scale = new Vector3(.4f * body, .3f * body, 1.1f * body);
            var shape = sheath.shape;
            shape.scale = new Vector3(.22f * body, .16f * body, .9f * body);

            // Tail spray: one burst per tail beat, timed to the shared stroke clock.
            float beat = fighting ? Mathf.Lerp(7.1f, 14f, fxEffort) : 7.1f;
            strokeClock += dt * beat;
            if (fxEffort > .2f && strokeClock - lastStroke >= Mathf.PI)
            {
                lastStroke = strokeClock;
                beatPulse = 1;
                int count = Mathf.RoundToInt(Mathf.Lerp(18, 80, fxEffort) * body);
                tailSpray.transform.localPosition = new Vector3(0, .02f, -.55f * body);
                tailSpray.Emit(count);
                churn.Emit(Mathf.RoundToInt(Mathf.Lerp(2, 7, fxEffort) * body));
            }

            // Flank sweep: when the body swings sideways relative to its heading.
            Vector3 lateral = Vector3.ProjectOnPlane(fxVelocity, transform.forward);
            float sideways = lateral.magnitude;
            if (sideways > .6f && fxEffort > .25f)
            {
                var side = flankSweep.transform;
                side.localPosition = transform.InverseTransformDirection(lateral.normalized) * (.2f * body);
                flankSweep.Emit(Mathf.RoundToInt(Mathf.Clamp(sideways * 6f, 2, 14)));
            }

            if (state.HookSequence != fxHookSequence)
            {
                fxHookSequence = state.HookSequence;
                if (fxHookSequence > 0) { hookFlash = 1; sheath.Emit(RequiresPartner ? 120 : 70); tailSpray.Emit(RequiresPartner ? 80 : 50); churn.Emit(RequiresPartner ? 22 : 14); }
            }
            if (state.TugSequence != fxTugSequence)
            {
                fxTugSequence = state.TugSequence;
                if (fxTugSequence > 0) { tugFlash = 1; sheath.Emit(RequiresPartner ? 180 : 110); tailSpray.Emit(RequiresPartner ? 120 : 70); churn.Emit(RequiresPartner ? 30 : 18); }
            }

            // Flash: a hard white hit on the hook, a shorter one on each run, a glint on every beat of a fight.
            hookFlash = Mathf.MoveTowards(hookFlash, 0, dt * 3.2f);
            tugFlash = Mathf.MoveTowards(tugFlash, 0, dt * 2.4f);
            beatPulse = Mathf.MoveTowards(beatPulse, 0, dt * 7f);
            float flash = hookFlash * hookFlash * .95f + tugFlash * tugFlash * .5f
                + (fighting ? .08f + beatPulse * .22f * fxEffort : 0);
            ApplyFlash(flash);

            siltCooldown -= dt;
            if (siltCooldown <= 0 && fxEffort > .3f)
            {
                siltCooldown = .45f;
                int count = Physics.RaycastNonAlloc(position, Vector3.down, fxHits, (RequiresPartner ? 1.3f : .8f) * body,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = fxHits[i];
                    if (hit.collider.GetComponentInParent<FishActor>() != null || hit.collider.GetComponentInParent<DiverMotor>() != null
                        || hit.normal.y < .5f) continue;
                    siltCloud.transform.SetPositionAndRotation(hit.point + hit.normal * .08f, Quaternion.FromToRotation(Vector3.up, hit.normal));
                    siltCloud.Emit(Mathf.RoundToInt(14 + fxEffort * 30));
                    break;
                }
            }
        }

        void Quiet()
        {
            var e = sheath.emission; e.rateOverTime = 0;
            var c = churn.emission; c.rateOverTime = 0;
            fxEffort = 0; hookFlash = tugFlash = beatPulse = 0;
        }

        ParticleSystem MakeFx(string name, Material material, Mesh mesh, Transform parent, bool worldSpace)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (mesh != null) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        static void Fade(ParticleSystem ps, float peakAt, float hold)
        {
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, peakAt), new GradientAlphaKey(.85f, hold), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
        }

        static void SetBubbleRise(ParticleSystem particles,float min,float max)
        {
            var velocity=particles.velocityOverLifetime;
            velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);
            velocity.y=new ParticleSystem.MinMaxCurve(min,max);
            velocity.enabled=true;
        }

        bool CreateFx()
        {
            var bubbleSource = Resources.Load<Material>("NereidBubbles");
            var siltShader = Resources.Load<Shader>("NereidSilt");
            if (bubbleSource == null || siltShader == null) return false;
            bubbleMaterial = new Material(bubbleSource) { name = "Fish effort bubbles" };
            bubbleMaterial.SetFloat("_ParticleTint", 1);
            siltMaterial = new Material(siltShader) { name = "Fish bottom silt" };
            sprayMaterial = new Material(bubbleSource) { name = "Fish tail spray" };
            sprayMaterial.SetFloat("_ParticleTint", 1);
            var factory = FindAnyObjectByType<Factory.FactoryVisuals>();
            var bubbleMesh = factory != null ? factory.bubbleMesh : null;
            float body = BodyLength;

            // Body sheath: small bubbles born along the flanks, drifting up and back.
            sheath = MakeFx("Effort sheath", bubbleMaterial, bubbleMesh, transform, true);
            var main = sheath.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .9f, .92f, .45f), new Color(.96f, 1f, 1f, .75f));
            main.startSize = new ParticleSystem.MinMaxCurve(.008f, .03f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .45f);
            main.maxParticles = 900;
            var shape = sheath.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.22f * body, .16f * body, .9f * body);
            SetBubbleRise(sheath,.35f,.7f);
            var inherit = sheath.inheritVelocity; inherit.enabled = true; inherit.mode = ParticleSystemInheritVelocityMode.Initial; inherit.curve = .35f;
            Fade(sheath, .12f, .6f);
            var noise = sheath.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = 1.6f; noise.scrollSpeed = .8f;
            sheath.Play();

            // Tail spray: a fan of brighter, faster bubbles and fine foam thrown back on each beat.
            tailSpray = MakeFx("Tail spray", sprayMaterial, bubbleMesh, transform, true);
            main = tailSpray.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.86f, .97f, .98f, .6f), new Color(1f, 1f, 1f, .9f));
            main.startSize = new ParticleSystem.MinMaxCurve(.008f, .04f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.8f);
            main.maxParticles = 560;
            shape = tailSpray.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 38; shape.radius = .06f * body; shape.rotation = new Vector3(0, 180, 0);
            SetBubbleRise(tailSpray,.3f,.6f);
            var limit = tailSpray.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .35f; limit.limit = 1.2f;
            Fade(tailSpray, .08f, .5f);
            tailSpray.Play();

            // Flank sweep: fines and a few bubbles thrown off the side the body swings through.
            flankSweep = MakeFx("Flank sweep", bubbleMaterial, bubbleMesh, transform, true);
            main = flankSweep.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f, .78f, .8f, .35f), new Color(.8f, .9f, .92f, .6f));
            main.startSize = new ParticleSystem.MinMaxCurve(.01f, .03f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .7f);
            main.maxParticles = 80;
            shape = flankSweep.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f * body;
            SetBubbleRise(flankSweep,.25f,.5f);
            Fade(flankSweep, .1f, .6f);
            flankSweep.Play();

            // Churn: large soft white puffs that bloom off the body and fade, the "boiling water" of a fight.
            foamMaterial = new Material(siltShader) { name = "Fish churn foam" };
            churn = MakeFx("Churn", foamMaterial, null, transform, true);
            main = churn.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.8f, .92f, .94f, .28f), new Color(.97f, 1f, 1f, .5f));
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .22f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, 1.0f);
            main.maxParticles = 200;
            shape = churn.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.4f * body, .3f * body, 1.1f * body);
            var grow = churn.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.25f, 1f), new Keyframe(1, 1.35f)));
            SetBubbleRise(churn,.15f,.4f);
            Fade(churn, .08f, .35f);
            churn.Play();

            // Bottom silt bloom.
            var silt = new GameObject("Bottom silt");
            silt.transform.SetParent(transform.parent, true);
            siltCloud = silt.AddComponent<ParticleSystem>();
            siltCloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = siltCloud.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.27f, .31f, .23f, .5f), new Color(.46f, .43f, .31f, .32f));
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.3f, .9f);
            main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            shape = siltCloud.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .35f; shape.angle = 34; shape.rotation = new Vector3(-90, 0, 0);
            Fade(siltCloud, .2f, .5f);
            var emission = siltCloud.emission; emission.rateOverTime = 0;
            siltCloud.GetComponent<ParticleSystemRenderer>().sharedMaterial = siltMaterial;
            siltCloud.Play();
            fxLastPosition = transform.position;
            return true;
        }

        void DisposeFx()
        {
            if (siltCloud != null) Destroy(siltCloud.gameObject);
            if (bubbleMaterial != null) Destroy(bubbleMaterial);
            if (sprayMaterial != null) Destroy(sprayMaterial);
            if (foamMaterial != null) Destroy(foamMaterial);
            if (siltMaterial != null) Destroy(siltMaterial);
        }
    }
}
