using UnityEngine;

namespace Raid.Nereid
{
    // Client-only presentation of the working helper: bubbles while swimming, a dense trail on a lunge,
    // a flank sweep on turns, a silt puff near the floor, a burst on a grab. The bag appears at the third
    // claws rank and the trawl net at the fifth. Everything reads the replicated snapshot; nothing is sent.
    internal sealed partial class GoshaView
    {
        ParticleSystem trail, lungeTrail, sweep, silt, grab;
        Material bubbleMaterial, siltMaterial;
        Transform bag, net;
        Mesh bagMesh, netMesh;
        float fxEffort, siltCooldown, lungeGlow;
        uint lastLungeSequence;
        byte lastLoad;
        Vector3 lastTrailPosition;
        static readonly RaycastHit[] floorHits = new RaycastHit[4];

        void PresentWork(GoshaSnapshot s, bool swimming, float dt)
        {
            if (trail == null && !CreateWorkFx()) return;
            var world = RaidWorld.Instance;
            var team = world != null ? world.View : null;
            int claws = team?.TeamRank(SkillLine.GoshaClaws) ?? 0;
            PresentGear(claws, s);

            dt = Mathf.Max(dt, .0001f);
            Vector3 at = creature.position;
            float speed = Vector3.Distance(at, lastTrailPosition) / dt;
            lastTrailPosition = at;
            bool visible = creature.gameObject.activeSelf && swimming;
            float effort = visible ? Mathf.InverseLerp(.4f, 5.5f, speed) : 0;
            fxEffort = Mathf.Lerp(fxEffort, effort, 1 - Mathf.Exp(-dt * 6));

            var em = trail.emission;
            em.rateOverTime = visible ? 12f + fxEffort * 110f : 0;
            var shape = trail.shape; shape.scale = new Vector3(.5f, .3f, 1.4f) * RaidWorld.Instance.tuning.petScale;

            // Lunge: the snapshot sequence steps once per committed burst.
            if (s.Lunge != lastLungeSequence)
            {
                lastLungeSequence = s.Lunge;
                if (visible) { lungeGlow = 1; lungeTrail.Emit(150); sweep.Emit(26); }
            }
            lungeGlow = Mathf.MoveTowards(lungeGlow, 0, dt * 1.4f);
            var le = lungeTrail.emission;
            le.rateOverTime = lungeGlow * 420f;

            // Flank sweep on a turn: the bank angle already measures how hard the body swings.
            if (visible && fxEffort > .3f && Mathf.Abs(bank) > 9f && Time.frameCount % 6 == 0) sweep.Emit(Mathf.RoundToInt(2 + Mathf.Abs(bank) * .25f));

            // Grab: the load count rose while at sea.
            if (s.Load != lastLoad)
            {
                if (s.Load > lastLoad && visible) { grab.transform.position = body.TransformPoint(JawSocket); grab.Emit(70); sweep.Emit(18); }
                lastLoad = s.Load;
            }

            // Silt: a puff when working low over the sand.
            siltCooldown -= dt;
            if (visible && siltCooldown <= 0 && fxEffort > .25f)
            {
                siltCooldown = .55f;
                int count = Physics.RaycastNonAlloc(at, Vector3.down, floorHits, .9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = floorHits[i];
                    if (hit.normal.y < .5f || hit.collider.GetComponentInParent<DiverMotor>() != null || hit.collider.GetComponentInParent<FishActor>() != null) continue;
                    silt.transform.SetPositionAndRotation(hit.point + hit.normal * .06f, Quaternion.FromToRotation(Vector3.up, hit.normal));
                    silt.Emit(Mathf.RoundToInt(10 + fxEffort * 22));
                    break;
                }
            }
        }

        void PresentGear(int claws, GoshaSnapshot s)
        {
            bool showBag = claws >= 3 && s.Mode != GoshaMode.Cradled && s.Mode != GoshaMode.Riding;
            if (showBag && bag == null)
            {
                bag = Node(body, "Catch bag", new Vector3(0, .34f, -.35f));
                var g = new ReefGeometry();
                g.Tube(new[] { new Vector3(0, 0, -.42f), new Vector3(0, .07f, -.2f), new Vector3(0, .09f, .14f), new Vector3(0, .03f, .36f) },
                    new[] { .14f, .3f, .3f, .12f }, new Color(.21f, .19f, .14f), 10, .72f);
                g.Tube(new[] { new Vector3(-.26f, -.02f, .1f), new Vector3(.26f, -.02f, .1f) }, new[] { .04f, .04f }, new Color(.62f, .48f, .2f), 6);
                bagMesh = g.Mesh("Catch bag");
                bag.gameObject.AddComponent<MeshFilter>().sharedMesh = bagMesh;
                bag.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shell;
            }
            if (bag != null) bag.gameObject.SetActive(showBag);
            // The bag swells with its load.
            if (bag != null && showBag) bag.localScale = Vector3.one * (1f + .18f * Mathf.Clamp(s.Load - 1, 0, 3));

            bool showNet = claws >= 5 && s.Mode == GoshaMode.Fishing && s.Load > 0 && s.Load < 3;
            if (showNet && net == null)
            {
                net = Node(body, "Crab trawl", new Vector3(0, -.12f, -1.1f));
                var g = new ReefGeometry();
                const int rows = 5, cols = 9;
                for (int j = 0; j <= rows; j++)
                    for (int i = 0; i < cols; i++)
                    {
                        float u = i / (float)(cols - 1), v = j / (float)rows;
                        var p = new Vector3((u - .5f) * 1.6f, -.25f * Mathf.Sin(v * Mathf.PI) * Mathf.Sin(u * Mathf.PI), -v * 1.4f);
                        g.Add(p, new Color(.78f, .74f, .55f, .55f));
                        if (j > 0 && i > 0) { int a = (j - 1) * cols + i - 1, b = (j - 1) * cols + i; g.Quad(a, b, b + cols, a + cols); }
                    }
                netMesh = g.Mesh("Crab trawl");
                net.gameObject.AddComponent<MeshFilter>().sharedMesh = netMesh;
                net.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shell;
            }
            if (net != null) net.gameObject.SetActive(showNet);
        }

        bool CreateWorkFx()
        {
            var bubbleSource = Resources.Load<Material>("NereidBubbles");
            var siltShader = Resources.Load<Shader>("NereidSilt");
            if (bubbleSource == null || siltShader == null) return false;
            bubbleMaterial = new Material(bubbleSource) { name = "Gosha bubbles" };
            bubbleMaterial.SetFloat("_ParticleTint", 1);
            siltMaterial = new Material(siltShader) { name = "Gosha silt" };
            var factory = FindAnyObjectByType<Factory.FactoryVisuals>();
            var bubbleMesh = factory != null ? factory.bubbleMesh : null;

            trail = Make("Swim bubbles", bubbleMaterial, bubbleMesh, body);
            var main = trail.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .9f, .92f, .4f), new Color(.96f, 1f, 1f, .7f));
            main.startSize = new ParticleSystem.MinMaxCurve(.01f, .035f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .4f);
            main.maxParticles = 600;
            var shape = trail.shape; shape.shapeType = ParticleSystemShapeType.Box;
            Rise(trail, .3f, .65f); Fade(trail); trail.Play();

            lungeTrail = Make("Lunge trail", bubbleMaterial, bubbleMesh, body);
            main = lungeTrail.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.86f, .97f, 1f, .7f), Color.white);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .06f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.maxParticles = 700;
            shape = lungeTrail.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 24; shape.radius = .12f; shape.rotation = new Vector3(0, 180, 0);
            shape.position = new Vector3(0, 0, -1.3f);
            Rise(lungeTrail, .25f, .55f); Fade(lungeTrail); lungeTrail.Play();

            sweep = Make("Flank sweep", bubbleMaterial, bubbleMesh, body);
            main = sweep.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f, .8f, .82f, .35f), new Color(.85f, .95f, .96f, .6f));
            main.startSize = new ParticleSystem.MinMaxCurve(.01f, .03f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .9f);
            main.maxParticles = 160;
            shape = sweep.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .35f;
            Rise(sweep, .2f, .45f); Fade(sweep); sweep.Play();

            grab = Make("Grab burst", bubbleMaterial, bubbleMesh, null);
            main = grab.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.9f, 1f, 1f, .7f), Color.white);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .05f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.2f);
            main.maxParticles = 200;
            shape = grab.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            Rise(grab, .3f, .7f); Fade(grab); grab.Play();

            silt = Make("Floor silt", siltMaterial, null, null);
            main = silt.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.27f, .31f, .23f, .5f), new Color(.46f, .43f, .31f, .32f));
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .8f);
            main.maxParticles = 80;
            shape = silt.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .3f; shape.angle = 34; shape.rotation = new Vector3(-90, 0, 0);
            Fade(silt); silt.Play();
            lastTrailPosition = creature.position;
            return true;
        }

        ParticleSystem Make(string label, Material material, Mesh mesh, Transform parent)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (mesh != null) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void Rise(ParticleSystem ps, float min, float max)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(min, max);
            velocity.enabled = true;
        }

        static void Fade(ParticleSystem ps)
        {
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
        }

        void DisposeWork()
        {
            if (bubbleMaterial != null) Destroy(bubbleMaterial);
            if (siltMaterial != null) Destroy(siltMaterial);
            if (bagMesh != null) Destroy(bagMesh);
            if (netMesh != null) Destroy(netMesh);
        }
    }
}
