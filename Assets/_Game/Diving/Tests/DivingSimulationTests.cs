using NUnit.Framework;
using UnityEngine;

namespace UnderwaterFishing.Diving.Tests
{
    public sealed class DivingSimulationTests
    {
        [Test]
        public void CreateStartsAliveWithFullTankAndSurfacePressure()
        {
            var settings = new DivingSettings { OxygenCapacity = 250f };
            var state = DivingSimulation.Create(settings);
            Assert.That(state.Oxygen, Is.EqualTo(250f));
            Assert.That(state.Life, Is.EqualTo(DiverLife.Active));
            Assert.That(state.AmbientPressure, Is.EqualTo(1f));
            Assert.That(state.Depth, Is.Zero);
            Assert.That(state.Stress, Is.Zero);
        }

        [TestCase(0f, 1f)]
        [TestCase(10f, 2f)]
        [TestCase(35f, 4.5f)]
        [TestCase(-10f, 1f)]
        public void PressureUsesNonnegativeDepth(float depth, float pressure)
        {
            var settings = new DivingSettings();
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, depth, 0f, false, false, 1f);
            Assert.That(state.AmbientPressure, Is.EqualTo(pressure));
            Assert.That(state.Depth, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SprintMultipliesOxygenDrain()
        {
            var settings = new DivingSettings { OxygenCapacity = 100f, OxygenDrain = 2f, SprintOxygenMultiplier = 3f };
            var normal = DivingSimulation.Create(settings);
            var sprint = normal;
            DivingSimulation.Step(ref normal, settings, 5f, 0f, false, false, 4f);
            DivingSimulation.Step(ref sprint, settings, 5f, 0f, false, true, 4f);
            Assert.That(normal.Oxygen, Is.EqualTo(92f));
            Assert.That(sprint.Oxygen, Is.EqualTo(76f));
        }

        [Test]
        public void SafeShelterRefillsAndRecoversWithoutExceedingBounds()
        {
            var settings = new DivingSettings { OxygenCapacity = 100f, RefillRate = 10f, StressRecovery = 4f };
            var state = DivingSimulation.Create(settings);
            state.Oxygen = 90f;
            state.Stress = 5f;
            DivingSimulation.Step(ref state, settings, 80f, 10f, true, true, 2f);
            Assert.That(state.Oxygen, Is.EqualTo(100f));
            Assert.That(state.Stress, Is.Zero);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Active));
        }

        [TestCase(20f, 0f, 0f)]
        [TestCase(30f, 0f, 20f)]
        [TestCase(20f, 4f, 6f)]
        [TestCase(20f, -5f, 3f)]
        public void DepthAndAsymmetricVerticalLimitsCauseStress(float depth, float vertical, float expected)
        {
            var settings = new DivingSettings
            {
                SafeDepth = 20f, SafeAscentSpeed = 2f, SafeDescentSpeed = 4f,
                StressGainDepth = 2f, StressGainSpeed = 3f
            };
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, depth, vertical, false, false, 1f);
            Assert.That(state.Stress, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void CriticalStressIncapacitatesBeforeOxygenRunsOut()
        {
            var settings = new DivingSettings { CriticalStress = 10f, SafeDepth = 10f, StressGainDepth = 1f };
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, 20f, 0f, false, false, 1f);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Incapacitated));
            Assert.That(state.Oxygen, Is.GreaterThan(0f));
            Assert.That(state.IncapacitatedRemaining, Is.EqualTo(settings.IncapacitatedSeconds));
        }

        [Test]
        public void OxygenExhaustionAllowsRescueThenDiesWhenTimerExpires()
        {
            var settings = new DivingSettings { OxygenCapacity = 2f, OxygenDrain = 1f, IncapacitatedSeconds = 3f };
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, 0f, 0f, false, false, 2f);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Incapacitated));
            Assert.That(state.Oxygen, Is.Zero);
            DivingSimulation.Step(ref state, settings, 0f, 0f, false, false, 3f);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Dead));
            Assert.That(state.IncapacitatedRemaining, Is.Zero);
            Assert.That(DivingSimulation.Rescue(ref state, settings), Is.False);
        }

        [Test]
        public void LargeStepAndSubstepsAgreeAcrossIncapacitationAndDeath()
        {
            var settings = new DivingSettings { OxygenCapacity = 2f, OxygenDrain = 1f, IncapacitatedSeconds = 3f };
            var large = DivingSimulation.Create(settings);
            var small = large;
            DivingSimulation.Step(ref large, settings, 5f, 0f, false, false, 6f);
            for (int i = 0; i < 24; i++)
                DivingSimulation.Step(ref small, settings, 5f, 0f, false, false, 0.25f);
            Assert.That(large.Life, Is.EqualTo(DiverLife.Dead));
            Assert.That(large.Life, Is.EqualTo(small.Life));
            Assert.That(large.Oxygen, Is.EqualTo(small.Oxygen));
            Assert.That(large.IncapacitatedRemaining, Is.EqualTo(small.IncapacitatedRemaining));
        }

        [Test]
        public void ShelterDoesNotAutomaticallyRescueIncapacitatedDiver()
        {
            var settings = new DivingSettings();
            var state = DivingSimulation.Create(settings);
            state.Oxygen = 0f;
            DivingSimulation.Step(ref state, settings, 0f, 0f, true, false, 1f);
            Assert.That(state.Oxygen, Is.GreaterThan(0f));
            Assert.That(state.Life, Is.EqualTo(DiverLife.Incapacitated));
            Assert.That(DivingSimulation.Rescue(ref state, settings), Is.True);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Active));
            Assert.That(state.Oxygen, Is.GreaterThanOrEqualTo(settings.RescueOxygen));
            Assert.That(state.Stress, Is.LessThan(settings.CriticalStress));
            Assert.That(state.IncapacitatedRemaining, Is.Zero);
            Assert.That(DivingSimulation.Rescue(ref state, settings), Is.False);
        }

        [Test]
        public void EquipmentSettingsImmediatelyChangeCapacityAndDepthTolerance()
        {
            var settings = new DivingSettings { OxygenCapacity = 300f, SafeDepth = 60f };
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, 50f, 0f, false, false, 1f);
            Assert.That(state.Oxygen, Is.EqualTo(299f));
            Assert.That(state.Stress, Is.Zero);
            settings.OxygenCapacity = 100f;
            settings.SafeDepth = 40f;
            DivingSimulation.Step(ref state, settings, 50f, 0f, false, false, 1f);
            Assert.That(state.Oxygen, Is.EqualTo(99f));
            Assert.That(state.Stress, Is.GreaterThan(0f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotAdvanceState(float dt)
        {
            var settings = new DivingSettings();
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, 100f, 50f, false, true, dt);
            Assert.That(state.Oxygen, Is.EqualTo(settings.OxygenCapacity));
            Assert.That(state.Stress, Is.Zero);
            Assert.That(state.Depth, Is.Zero);
            Assert.That(state.Life, Is.EqualTo(DiverLife.Active));
        }

        [Test]
        public void NextExpeditionCreateResetsDeadState()
        {
            var settings = new DivingSettings();
            var dead = new DivingState { Life = DiverLife.Dead, Stress = 100f, Depth = 80f };
            DivingSimulation.Step(ref dead, settings, 0f, 0f, true, false, 100f);
            Assert.That(dead.Life, Is.EqualTo(DiverLife.Dead));
            Assert.That(dead.Oxygen, Is.Zero);
            dead = DivingSimulation.Create(settings);
            Assert.That(dead.Life, Is.EqualTo(DiverLife.Active));
            Assert.That(dead.Oxygen, Is.EqualTo(settings.OxygenCapacity));
            Assert.That(dead.Stress, Is.Zero);
        }

        [Test]
        public void SwimmingClampsDiagonalAndVerticalInputSeparately()
        {
            Vector3 clamped = SwimMath.ClampVoluntaryVelocity(new Vector3(6f, 5f, 8f), 5f, 2f, 4f);
            Assert.That(clamped.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(clamped.z, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(2f));
            Assert.That(SwimMath.ClampVoluntaryVelocity(Vector3.down * 10f, 5f, 2f, 4f).y, Is.EqualTo(-4f));
        }

        [Test]
        public void ExternalFishPullCanExceedVoluntarySpeedLimit()
        {
            Vector3 voluntary = SwimMath.ClampVoluntaryVelocity(Vector3.up * 5f, 4f, 2f, 3f);
            Vector3 fishPull = Vector3.up * 6f;
            Vector3 total = voluntary + fishPull;
            Assert.That(total.y, Is.EqualTo(8f));
            var settings = new DivingSettings { SafeAscentSpeed = 2f };
            var state = DivingSimulation.Create(settings);
            DivingSimulation.Step(ref state, settings, 10f, total.y, false, false, 1f);
            Assert.That(state.Stress, Is.GreaterThan(0f));
        }
    }
}
