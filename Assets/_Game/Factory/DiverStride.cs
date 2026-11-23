using UnityEngine;

namespace Raid
{
    // One distance-driven gait for the remote suit and the gear in the local camera.
    sealed class DiverStride
    {
        // One foot contact: settle under the load, recover, roll into the next step.
        // Matching end slopes keep the loop continuous as the supporting foot changes.
        static readonly AnimationCurve Footfall = new AnimationCurve(
            new Keyframe(0f, .15f, -3.2f, -3.2f), new Keyframe(.19f, -.9f),
            new Keyframe(.64f, .78f), new Keyframe(1f, .15f, -3.2f, -3.2f));

        Vector3 lastPosition, sampledVelocity, velocity;
        float phase, weight, run, water, side;
        bool tracking;

        public float Speed {get; private set;}
        public float Weight => weight;
        public float Running => run;
        public float Water => water;
        public float Phase => phase*2*Mathf.PI;
        public float Lateral => side;
        public bool Moving => weight > .04f;
        public Vector3 Offset { get; private set; }
        public Vector3 Tilt { get; private set; }

        public void Reset()
        {
            tracking = false;
            sampledVelocity = velocity = Offset = Tilt = Vector3.zero;
            phase = weight = run = side = Speed = 0;
        }

        public void Advance(DiverMotor actor, float dt)
        {
            Vector3 position = actor.transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            bool onDeck = actor.IsWalking;
            if (!tracking || dt <= 0 || dt > .25f || delta.sqrMagnitude > 1.5f * 1.5f)
            {
                Reset();
                tracking = true;
                water = onDeck ? 0f : 1f;
                return;
            }

            // A host moves in physics steps, a client receives snapshots. Preserve the
            // entire distance first, then filter before reading speed on a render frame.
            float follow = 1f - Mathf.Exp(-dt * 18f);
            sampledVelocity = Vector3.Lerp(sampledVelocity, delta / dt, follow);
            velocity = Vector3.Lerp(velocity, sampledVelocity, follow);
            float blend = 1f - Mathf.Exp(-dt * 8f);
            water = Mathf.Lerp(water, onDeck ? 0f : 1f, blend);
            float speed = Speed = new Vector3(velocity.x, velocity.y * water, velocity.z).magnitude;
            float cruise = onDeck ? Nereid.NereidVessel.Active.indoorWalkSpeed : actor.tuning.swimSpeed;
            float pace = speed / Mathf.Max(.1f, cruise);
            weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.025f, .85f, pace));
            run = Mathf.Lerp(run, actor.IsSprinting ? weight : 0f, blend);
            side = Mathf.Clamp(Vector3.Dot(velocity, actor.transform.right) / Mathf.Max(1f, cruise), -1f, 1f);

            // Phase is a full left/right cycle. Swimming uses a longer pull and glide.
            float cycles = Mathf.Lerp(.93f + run * .60f, .51f + run * .17f, water);
            float forward = Vector3.Dot(velocity, actor.transform.forward);
            float direction = onDeck && forward < -speed*.5f ? -1f : 1f;
            phase = Mathf.Repeat(phase + direction*dt*cycles*Mathf.Clamp01(pace), 1f);
            float shift = Mathf.Sin(phase * 2f * Mathf.PI);
            float swing = Mathf.Cos(phase * 2f * Mathf.PI);
            float foot = Footfall.Evaluate(Mathf.Repeat(phase * 2f, 1f));
            Vector3 step = new Vector3(shift * (.013f + .015f * run) - .008f * side,
                foot * (.008f + .020f * run), swing * (.004f+.010f*run) - (.004f+.009f*run)*foot);
            Vector3 glide = new Vector3(shift * .011f, swing * .007f, shift * .012f);
            Offset = Vector3.Lerp(step, glide, water) * weight;
            Tilt = Vector3.Lerp(new Vector3(-foot * (1.25f+1.3f*run), shift * (1.2f+1.6f*run), -shift * (1.4f+2.2f*run) - side * 1.2f),
                new Vector3(-swing * 1.2f, shift * .9f, -shift * 1.9f), water) * weight;
            // During a run tuck the stock back against the shoulder and carry across the chest.
            float tuck = run * (1f - water);
            Offset += new Vector3(-.067f, -.053f, -.050f) * tuck;
            Tilt += new Vector3(10.5f, -18f, 10f) * tuck;
        }

        public void FreeHand(int hand, out Vector3 offset, out Vector3 angles)
        {
            float radians = phase * 2f * Mathf.PI;
            float sweep = hand * Mathf.Sin(radians);
            float recover = hand * Mathf.Cos(radians);
            Vector3 walk = new Vector3(hand * (recover * .008f - run*.027f), sweep * (.024f + run * .042f) + run*.024f,
                sweep * (.070f + run * .085f) - run*.031f);
            Vector3 stroke = new Vector3(hand * recover * .046f, recover * .028f, sweep * .135f);
            offset = Vector3.Lerp(walk, stroke, water) * weight;
            angles = new Vector3(-sweep * Mathf.Lerp(13f+15f*run, 21f, water), hand * recover * water * 10f,
                -hand * recover * Mathf.Lerp(6f, 15f, water)) * weight;
        }
    }
}
