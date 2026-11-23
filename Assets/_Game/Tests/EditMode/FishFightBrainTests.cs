using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public class FishFightBrainTests
    {
        static FishFightProfile Profile()
        {
            var profile = ScriptableObject.CreateInstance<FishFightProfile>();
            profile.speciesId = "Test";
            return profile;
        }

        // One fight: the diver reels most of the time, stamina drains while reeling,
        // recovers a little when the line goes slack. Returns the phase sequence.
        static List<FishBehaviour> Fight(FishFightBrain brain, int seed, float seconds, List<float> surgeStarts = null, List<float> tugs = null)
        {
            var phases = new List<FishBehaviour> { brain.CurrentPhase };
            var pace = new System.Random(seed * 7919 + 13);
            float stamina = 1;
            var input = new FishFightInput { Dt = .02f, Hooks = 1, Distance01 = .6f };
            for (float t = 0; t < seconds; t += input.Dt)
            {
                input.Reeling = pace.NextDouble() > .2;
                stamina = Mathf.Clamp01(stamina + (input.Reeling ? -.02f : .01f) * input.Dt * 4);
                input.Stamina01 = stamina;
                input.Distance01 = Mathf.Lerp(.6f, .05f, t / seconds);
                input.NearHand = input.Distance01 < .1f;
                input.Exhausted = stamina <= 0;
                brain.Tick(in input);
                if (brain.PhaseChanged) phases.Add(brain.CurrentPhase);
                if (brain.SurgeStarted) surgeStarts?.Add(brain.Clock);
                tugs?.Add(brain.TugStrength);
            }
            return phases;
        }

        [Test] public void SameSeedProducesTheSameFight()
        {
            var profile = Profile();
            var a = Fight(new FishFightBrain(profile, 42), 42, 30);
            var b = Fight(new FishFightBrain(profile, 42), 42, 30);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Count, Is.GreaterThan(4));
        }

        [Test] public void FiveFightsVaryAndCoverEveryPhase()
        {
            var profile = Profile();
            var sequences = new HashSet<string>();
            bool lateral = false, surge = false, weakened = false;
            for (int seed = 1; seed <= 5; seed++)
            {
                var phases = Fight(new FishFightBrain(profile, seed), seed, 30);
                sequences.Add(string.Join(">", phases));
                lateral |= phases.Contains(FishBehaviour.Lateral);
                surge |= phases.Contains(FishBehaviour.Surge);
                weakened |= phases.Contains(FishBehaviour.Weakened);
                Assert.That(phases[0], Is.EqualTo(FishBehaviour.HookShock));
            }
            Assert.That(sequences.Count, Is.GreaterThanOrEqualTo(3), "fights look identical");
            Assert.That(lateral, "no Lateral phase in five fights");
            Assert.That(surge, "no Surge phase in five fights");
            Assert.That(weakened, "no Weakened phase in five fights");
        }

        [Test] public void SurgesNeverStartInsideTheCooldown()
        {
            var profile = Profile();
            for (int seed = 1; seed <= 12; seed++)
            {
                var starts = new List<float>();
                Fight(new FishFightBrain(profile, seed), seed, 45, starts);
                Assert.That(starts.Count, Is.GreaterThanOrEqualTo(2), $"seed {seed}: too few surges");
                for (int i = 1; i < starts.Count; i++)
                    Assert.That(starts[i] - starts[i - 1], Is.GreaterThanOrEqualTo(profile.surgeCooldown - 1e-4f), $"seed {seed}: surge gap");
            }
        }

        [Test] public void WindupAlwaysPrecedesASurgeAndTugFollowsIt()
        {
            var profile = Profile();
            var tugs = new List<float>();
            var phases = Fight(new FishFightBrain(profile, 3), 3, 30, null, tugs);
            for (int i = 1; i < phases.Count; i++)
                if (phases[i] == FishBehaviour.Surge) Assert.That(phases[i - 1], Is.EqualTo(FishBehaviour.SurgeWindup));
            Assert.That(tugs, Has.Some.GreaterThan(.9f));
            Assert.That(tugs[0], Is.EqualTo(0));
        }

        [Test] public void TwoHooksWeakenButDoNotSilenceTheTug()
        {
            var profile = Profile();
            var brain = new FishFightBrain(profile, 5);
            var input = new FishFightInput { Dt = .02f, Hooks = 2, Stamina01 = 1, Distance01 = .5f, Reeling = true };
            float peak = 0;
            for (int i = 0; i < 1500; i++) { brain.Tick(in input); peak = Mathf.Max(peak, brain.TugStrength); }
            Assert.That(peak, Is.GreaterThan(.05f).And.LessThan(1f));
            Assert.That(peak, Is.EqualTo(profile.twoHookTugFactor).Within(.02f));
        }

        [Test] public void ExhaustedFishGoesWeakenedAndStopsSurging()
        {
            var profile = Profile();
            var brain = new FishFightBrain(profile, 9);
            var input = new FishFightInput { Dt = .02f, Hooks = 1, Stamina01 = 0, Exhausted = true, Reeling = true };
            for (int i = 0; i < 400; i++) brain.Tick(in input);
            Assert.That(brain.CurrentPhase, Is.EqualTo(FishBehaviour.Weakened));
            for (int i = 0; i < 1000; i++) { brain.Tick(in input); Assert.That(brain.Surging, Is.False); }
        }
    }
}
