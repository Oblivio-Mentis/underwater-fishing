using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Creatures.Tests
{
    public sealed class FishBrainTests
    {
        private FishSpecies species;
        private FishBrain brain;
        private readonly Vector3 home = new Vector3(0f, -12f, 0f);
        private readonly Vector3 diver = new Vector3(-3f, -12f, 0f);

        [SetUp]
        public void SetUp()
        {
            species = ScriptableObject.CreateInstance<FishSpecies>();
            brain = new FishBrain(species, home, 27);
        }
        [TearDown]
        public void TearDown()
        {
            brain.Dispose();
            Object.DestroyImmediate(species);
        }
        private Vector3 Step(float dt = 0.05f, bool reeling = false, float tension = 0f, bool nearby = true)
            => brain.Step(dt, home, diver, nearby, reeling, tension);

        [Test]
        public void UniversalFsmOwnsStateAndPublishesHookAndStruggleTransitions()
        {
            int notifications = 0;
            brain.StateSource.LayerStateChanged += change => { notifications++; };
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Idle));
            Assert.That(brain.TryHook(), Is.True);
            Assert.That(brain.TryHook(), Is.False);
            Step(species.HookReactionSeconds + 0.01f);
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Struggling));
            var runtime = (IUniversalFsmRuntimeHandle)brain.StateSource;
            Assert.That(runtime.TryGetLayerSnapshot("Main", out var snapshot), Is.True);
            Assert.That(snapshot.CurrentStateId, Is.EqualTo((int)FishState.Struggling));
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void NearbyDiverTriggersAlertAndFleeThroughActualRuntime()
        {
            Step();
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Alerted));
            Step(species.AlertSeconds + 0.01f);
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Flee));
            Assert.That(Step().sqrMagnitude, Is.GreaterThan(0f));
            Step(0.05f, nearby: false);
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Roam));
        }

        [Test]
        public void ElapsedTimeAndSlackReelingCannotExhaustFish()
        {
            Assert.That(brain.TryHook(), Is.True);
            for (int i = 0; i < 400; i++) Step(0.05f, reeling: true, tension: 0f);
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Struggling));
            Assert.That(brain.Stamina, Is.EqualTo(species.Stamina).Within(0.001f));
            Assert.That(brain.Capture(), Is.False);
        }

        [Test]
        public void TautReelingExhaustsButDoesNotAutomaticallyCapture()
        {
            brain.TryHook();
            for (int i = 0; i < 600 && !brain.IsExhausted; i++) Step(0.05f, true, 0.75f);
            Assert.That(brain.IsExhausted, Is.True);
            Assert.That(brain.Stamina, Is.Zero);
            Assert.That(brain.TryHook(), Is.False);
            Assert.That(brain.Capture(), Is.True);
            brain.Release();
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Captured));
            Assert.That(brain.Capture(), Is.False);
            Assert.That(Step(), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void GivingSlackRecoversStaminaWhileTheFishStillMoves()
        {
            brain.TryHook();
            Step(species.HookReactionSeconds + 0.01f);
            Step(0.5f, true, 0.75f);
            float tired = brain.Stamina;
            Vector3 velocity = Step(0.5f, false, 0f);
            Assert.That(brain.Stamina, Is.GreaterThan(tired));
            Assert.That(velocity.sqrMagnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void ReleaseEscapesAndTemporarilyRejectsAnotherHook()
        {
            brain.TryHook();
            brain.Release();
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Escaped));
            Assert.That(brain.TryHook(), Is.False);
            Assert.That(brain.Capture(), Is.False);
            Step(species.EscapeCooldownSeconds + 0.01f);
            Assert.That(brain.CurrentState, Is.EqualTo(FishState.Flee));
            Assert.That(brain.TryHook(), Is.True);
        }

        [Test]
        public void LargeFishProducesDownwardBurstsAndDirectionChanges()
        {
            species.IsLarge = true;
            species.StruggleSpeed = 4f;
            brain.TryHook();
            Step(species.HookReactionSeconds + 0.01f);
            Vector3 first = Step();
            float lowestY = first.y;
            bool turned = false;
            for (int i = 0; i < 120; i++)
            {
                Vector3 velocity = Step();
                lowestY = Mathf.Min(lowestY, velocity.y);
                turned |= Vector3.Angle(first, velocity) > 15f;
            }
            Assert.That(lowestY, Is.LessThan(-0.5f));
            Assert.That(turned, Is.True);
        }
    }
}
