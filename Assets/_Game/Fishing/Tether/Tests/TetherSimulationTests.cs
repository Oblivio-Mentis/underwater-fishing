using System;
using NUnit.Framework;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Tether.Tests
{
    public sealed class TetherSimulationTests
    {
        private TetherSettings settings;

        [SetUp] public void SetUp() => settings = ScriptableObject.CreateInstance<TetherSettings>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);

        [Test]
        public void AttachmentUsesSlackWithinSpoolLimitsAndRejectsOutOfRange()
        {
            Assert.That(TetherSimulation.Attach(10f, settings).Length, Is.EqualTo(11f));
            Assert.That(TetherSimulation.Attach(25f, settings).Length, Is.EqualTo(25f));
            Assert.That(TetherSimulation.Attach(0f, settings).Length, Is.EqualTo(settings.MinLength));
            Assert.That(TetherSimulation.Attach(25.1f, settings).Connected, Is.False);
            Assert.That(TetherSimulation.Attach(float.NaN, settings).Connected, Is.False);
        }

        [Test]
        public void SlackTransmitsNeitherSpringNorDampingForce()
        {
            var state = TetherSimulation.Attach(10f, settings);
            var forces = Step(ref state, new Vector3(8f, 0f, 0f), new Vector3(100f, 0f, 0f));
            Assert.That(state.Tension, Is.Zero);
            Assert.That(forces.PlayerAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(forces.FishAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(state.Connected, Is.True);
        }

        [Test]
        public void TautRopePullsBothEndpointsTowardEachOtherIncludingDepthAxis()
        {
            var state = TetherSimulation.Attach(3f, settings);
            var forces = Step(ref state, new Vector3(3f, -4f, 0f));
            Assert.That(state.Tension, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(forces.PlayerAcceleration.x, Is.EqualTo(7.2f).Within(0.0001f));
            Assert.That(forces.PlayerAcceleration.y, Is.EqualTo(-9.6f).Within(0.0001f));
            Assert.That(forces.FishAcceleration, Is.EqualTo(-forces.PlayerAcceleration));
            Assert.That(forces.NormalizedTension, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void DampingAddsTensionOnlyForSeparatingEndpoints()
        {
            var state = TetherSimulation.Attach(5f, settings);
            var separating = Step(ref state, new Vector3(6f, 0f, 0f), new Vector3(3f, 0f, 0f));
            Assert.That(separating.PlayerAcceleration.x, Is.EqualTo(6f));
            var closing = Step(ref state, new Vector3(6f, 0f, 0f), new Vector3(-3f, 0f, 0f));
            Assert.That(closing.PlayerAcceleration, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ReelLengthAndHelperBonusAreBounded()
        {
            settings.Stiffness = settings.Damping = 0f;
            var capped = TetherSimulation.Attach(10f, settings);
            var excessiveHelpers = capped;
            Step(ref capped, new Vector3(10f, 0f, 0f), reel: true, helpers: 2, dt: 1f);
            Step(ref excessiveHelpers, new Vector3(10f, 0f, 0f), reel: true, helpers: 1000, dt: 1f);
            Assert.That(capped.Length, Is.EqualTo(8.4f).Within(0.0001f));
            Assert.That(excessiveHelpers.Length, Is.EqualTo(capped.Length));
            Step(ref capped, new Vector3(10f, 0f, 0f), reel: true, dt: 100f);
            Assert.That(capped.Length, Is.EqualTo(settings.MinLength));
            Assert.That(capped.Connected, Is.True);
        }

        [Test]
        public void AssistanceReducesCappedPlayerPullWithoutEliminatingItOrWeakeningFishPull()
        {
            var solo = TetherSimulation.Attach(0f, settings);
            var assisted = solo;
            var excessive = solo;
            var a = Step(ref solo, new Vector3(5f, 0f, 0f));
            var b = Step(ref assisted, new Vector3(5f, 0f, 0f), helpers: 2);
            var c = Step(ref excessive, new Vector3(5f, 0f, 0f), helpers: int.MaxValue);
            Assert.That(a.PlayerAcceleration.x, Is.EqualTo(settings.MaxAcceleration));
            Assert.That(b.PlayerAcceleration.x, Is.GreaterThan(0f).And.LessThan(a.PlayerAcceleration.x));
            Assert.That(c.PlayerAcceleration.x, Is.EqualTo(b.PlayerAcceleration.x));
            Assert.That(b.FishAcceleration, Is.EqualTo(a.FishAcceleration));
            Assert.That(b.NormalizedTension, Is.EqualTo(a.NormalizedTension));
            settings.MaxAssistants = int.MaxValue;
            var floor = Step(ref excessive, new Vector3(5f, 0f, 0f), helpers: int.MaxValue);
            Assert.That(excessive.Connected, Is.True);
            Assert.That(floor.PlayerAcceleration.x, Is.GreaterThan(0f).And.LessThan(a.PlayerAcceleration.x));
        }

        [Test]
        public void OverloadNeedsSustainedGraceAndReliefResetsItsTimer()
        {
            var state = TetherSimulation.Attach(10f, settings);
            Assert.That(Step(ref state, new Vector3(16f, 0f, 0f), dt: 0.3f).Broke, Is.False);
            Assert.That(state.OverloadSeconds, Is.EqualTo(0.3f));
            Step(ref state, new Vector3(10f, 0f, 0f), dt: 0.1f);
            Assert.That(state.OverloadSeconds, Is.Zero);
            Assert.That(Step(ref state, new Vector3(16f, 0f, 0f), dt: 0.3f).Broke, Is.False);
            Assert.That(Step(ref state, new Vector3(16f, 0f, 0f), dt: 0.3f).Broke, Is.False);
            var broken = Step(ref state, new Vector3(16f, 0f, 0f), dt: 0.1f);
            Assert.That(broken.Broke, Is.True);
            Assert.That(broken.PlayerAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(broken.FishAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(state.Connected, Is.False);
            Assert.That(state.Tension, Is.Zero);
        }

        [Test]
        public void ExcessiveRangeBreaksEvenWhenSpringWasTunedToZero()
        {
            settings.Stiffness = settings.Damping = 0f;
            var state = TetherSimulation.Attach(settings.MaxLength, settings);
            Vector3 beyondRange = Vector3.right * (settings.MaxLength + settings.MaxRangeStretch + 1f);
            Assert.That(Step(ref state, beyondRange, dt: 0.3f).Broke, Is.False);
            Assert.That(Step(ref state, beyondRange, dt: 0.4f).Broke, Is.True);
            Assert.That(state.Connected, Is.False);
        }

        [Test]
        public void InvalidTimeDoesNotAdvanceAndInvalidCoordinatesBreakWithoutNanForce()
        {
            var state = TetherSimulation.Attach(5f, settings);
            Step(ref state, Vector3.right * 10f, dt: float.NaN);
            Step(ref state, Vector3.right * 10f, dt: -1f);
            Assert.That(state.Length, Is.EqualTo(6f));
            Assert.That(state.OverloadSeconds, Is.Zero);
            Assert.That(Step(ref state, new Vector3(float.NaN, 0f, 0f)).Broke, Is.True);
            Assert.That(state.Connected, Is.False);
        }

        [Test]
        public void ReleasedRopeCannotApplyForcesOrReel()
        {
            var state = TetherSimulation.Attach(5f, settings);
            TetherSimulation.Release(ref state);
            var force = Step(ref state, Vector3.right * 20f, reel: true, dt: 1f);
            Assert.That(state.Connected, Is.False);
            Assert.That(state.Length, Is.Zero);
            Assert.That(force.PlayerAcceleration, Is.EqualTo(Vector3.zero));
            Assert.That(force.Broke, Is.False);
        }

        private TetherForces Step(ref TetherState state, Vector3 fishPosition,
            Vector3 fishVelocity = default, bool reel = false, int helpers = 0, float dt = 0.02f)
        {
            return TetherSimulation.Step(ref state, settings, Vector3.zero, fishPosition,
                Vector3.zero, fishVelocity, reel, helpers, 1f, dt);
        }
    }
}
