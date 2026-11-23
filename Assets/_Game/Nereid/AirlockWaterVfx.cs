using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Local VFX for the pressure lock cycle: a hard jet and spray while the chamber floods,
    // an intake vortex with streaks while it drains, and hanging mist after a cycle.
    // Purely visual, driven by the replicated airlock phase and level.
    public sealed class AirlockWaterVfx : MonoBehaviour
    {
        static readonly Vector3 Outlet = new(2.4f, 3.08f, 2.66f);
        static readonly Vector3 Intake = new(2.4f, .38f, 2.79f);
        Transform root;
        ParticleSystem jet, spray, vortex, mist;
        ParticleSystem.EmissionModule jetEmission, sprayEmission, vortexEmission, mistEmission;
        Material particleMaterial;
        Texture2D softDisc;
        float shownPhase = -1f;

        public static AirlockWaterVfx Install(Transform chamber)
        {
            var go = new GameObject("Pressure lock water VFX");
            go.transform.SetParent(chamber, false);
            var vfx = go.AddComponent<AirlockWaterVfx>();
            vfx.root = chamber;
            vfx.Build();
            return vfx;
        }

        Material ParticleMaterial(float brightness)
        {
            if (particleMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                particleMaterial = new Material(shader) { name = "Lock spray" };
                particleMaterial.SetFloat("_Surface", 1);
                particleMaterial.SetFloat("_Blend", 0);
                particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                particleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                particleMaterial.SetFloat("_ZWrite", 0);
                particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                particleMaterial.renderQueue = 3100;
                particleMaterial.SetTexture("_BaseMap", SoftDisc());
            }
            return particleMaterial;
        }

        Texture2D SoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int size = 32;
            softDisc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (2f * x + 1) / size - 1, dy = (2f * y + 1) / size - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1 - Mathf.SmoothStep(.25f, 1f, r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            softDisc.SetPixels32(px); softDisc.Apply(false, true);
            return softDisc;
        }

        ParticleSystem Make(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(1);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        void Build()
        {
            // The filling jet: dense droplets arcing from the nozzle into the rising surface.
            jet = Make("Flood jet", Outlet);
            var main = jet.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .034f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.66f, .86f, .88f, .75f), new Color(.86f, .97f, .98f, .9f));
            main.maxParticles = 160;
            var shape = jet.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 9; shape.radius = .05f; shape.rotation = new Vector3(78, 0, 0);
            var gravity = jet.limitVelocityOverLifetime; gravity.enabled = false;
            var velocity = jet.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(-4.2f, -2.4f); velocity.space = ParticleSystemSimulationSpace.Local;
            var drag = jet.forceOverLifetime; drag.enabled = true; drag.space = ParticleSystemSimulationSpace.Local; drag.y = new ParticleSystem.MinMaxCurve(0, 0);
            jetEmission = jet.emission; jetEmission.rateOverTime = 0;

            // Splash where the jet lands on the surface.
            spray = Make("Flood splash", Outlet);
            main = spray.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .9f, .91f, .55f), new Color(.94f, .99f, .99f, .8f));
            main.maxParticles = 120;
            shape = spray.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 55; shape.radius = .12f; shape.rotation = new Vector3(-90, 0, 0);
            velocity = spray.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(.2f, 1.2f);
            sprayEmission = spray.emission; sprayEmission.rateOverTime = 0;

            // Draining: a rotating vortex ring and upward streaks over the intake.
            vortex = Make("Drain vortex", Intake + Vector3.up * .12f);
            main = vortex.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .028f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f, .82f, .85f, .5f), new Color(.9f, .98f, .99f, .75f));
            main.maxParticles = 110;
            shape = vortex.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .17f; shape.radiusThickness = .4f;
            velocity = vortex.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.8f, 4.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-.5f, -.1f);
            vortexEmission = vortex.emission; vortexEmission.rateOverTime = 0;

            // Thin hanging mist in the chamber during and shortly after a cycle.
            mist = Make("Chamber mist", new Vector3(4.3f, 1.3f, 0));
            main = mist.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, .28f);
            main.startSize = new ParticleSystem.MinMaxCurve(.5f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .7f, .72f, .05f), new Color(.7f, .84f, .85f, .12f));
            main.maxParticles = 40;
            shape = mist.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(7.4f, 1.6f, 5.4f);
            velocity = mist.velocityOverLifetime; velocity.enabled = true; velocity.y = .04f;
            mistEmission = mist.emission; mistEmission.rateOverTime = 0;
        }

        public void Present(AirlockPhase phase, float surfaceY)
        {
            int mode = phase == AirlockPhase.Flooding ? 1 : phase == AirlockPhase.Draining ? -1 : 0;
            if (mode == shownPhase) { FollowSurface(surfaceY, mode); return; }
            shownPhase = mode;
            jetEmission.rateOverTime = mode > 0 ? 110 : 0;
            sprayEmission.rateOverTime = mode > 0 ? 60 : 0;
            vortexEmission.rateOverTime = mode < 0 ? 80 : 0;
            mistEmission.rateOverTime = mode != 0 ? 9 : 0;
            if (mode == 0) { mistEmission.rateOverTime = 3f; }
            FollowSurface(surfaceY, mode);
        }

        void FollowSurface(float surfaceY, int mode)
        {
            if (mode <= 0) return;
            float h = Mathf.Max(.05f, surfaceY);
            var local = new Vector3(Outlet.x, Mathf.Min(h, Outlet.y), Outlet.z - Mathf.Max(0, Outlet.y - h) * .9f);
            spray.transform.localPosition = local;
        }

        void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
            if (softDisc != null) Destroy(softDisc);
        }
    }
}
