using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class GoshaHuntTests
    {
        static GoshaHuntTuning Tuning => GoshaHuntTuning.Default;

        // A needlefish by its species numbers: cruises, bolts when the hunter is close for a third of a
        // second, keeps the bolt for 2.6 s, then recovers slowly. It never stops and always turns away.
        sealed class TestFish
        {
            public Vector3 At, Velocity;
            float alert, flee, recover;
            const float Cruise = 1.65f, Flee = 5.9f, Recover = 1.24f, Accel = 2.6f;
            public TestFish(Vector3 at, Vector3 heading) { At = at; Velocity = heading.normalized * Cruise; }
            public void Tick(float dt, Vector3 hunter)
            {
                Vector3 away = At - hunter;
                float distance = away.magnitude;
                if (flee > 0) flee -= dt;
                else if (recover > 0) recover -= dt;
                if (distance < 2.5f) { alert += dt; if (alert >= .3f && flee <= 0) { flee = 2.6f; recover = 2.2f; } }
                else alert = Mathf.Max(0, alert - dt * 2);
                float target = flee > 0 ? Flee : recover > 0 ? Recover : Cruise;
                Vector3 heading = Velocity.sqrMagnitude > .001f ? Velocity.normalized : Vector3.forward;
                Vector3 desired = flee > 0 ? Vector3.Slerp(heading, away.normalized, .5f) : heading;
                float speed = Mathf.MoveTowards(Velocity.magnitude, target, Accel * (flee > 0 ? 4f : 1f) * dt);
                Velocity = desired.normalized * speed;
                At += Velocity * dt;
            }
        }

        static bool Simulate(GoshaHuntBrain brain, Vector3 fishStart, out float seconds, float scale = .365f)
        {
            Vector3 hunter = Vector3.zero, heading = Vector3.forward;
            var fish = new TestFish(fishStart, new Vector3(.4f, 0, 1f));
            brain.Begin(Vector3.zero);
            seconds = 0;
            const float dt = .02f;
            for (int i = 0; i < 1500; i++)
            {
                var next = brain.Step(dt, hunter, heading, scale, fish.At, fish.Velocity, null, out bool grab);
                brain.Confirm(hunter, next, dt);
                heading = brain.Velocity.sqrMagnitude > .001f ? brain.Velocity.normalized : heading;
                hunter = next;
                seconds += dt;
                if (grab) { brain.MarkCaught(); return true; }
                if (brain.Phase == GoshaHuntPhase.Failed) return false;
                fish.Tick(dt, hunter);
            }
            return false;
        }

        [Test]
        public void TrainedHunterCatchesWhatTheUntrainedOneLoses()
        {
            var slow = new GoshaHuntBrain(); slow.Configure(Tuning.Stats(0, 0, 0, 0));
            var fast = new GoshaHuntBrain(); fast.Configure(Tuning.Stats(5, 7, 1, 0));
            int slowWins = 0, fastWins = 0;
            for (int i = 0; i < 8; i++)
            {
                var start = new Vector3(1.5f + i * .3f, 0, 4f + i * .25f);
                if (Simulate(slow, start, out _)) slowWins++;
                if (Simulate(fast, start, out _)) fastWins++;
                slow.Reset(); fast.Reset();
            }
            Assert.That(slowWins, Is.LessThan(fastWins), "speed rank changes the outcome");
            Assert.That(slowWins, Is.LessThanOrEqualTo(4), "an untrained pet often loses a bolting needlefish");
            Assert.That(fastWins, Is.GreaterThanOrEqualTo(6), "the torpedo rank catches it");
        }

        [Test]
        public void GrabRequiresTheJawsOnTheFishNotATimer()
        {
            var brain = new GoshaHuntBrain(); brain.Configure(Tuning.Stats(2, 2, 1, 0));
            Vector3 hunter = Vector3.zero, heading = Vector3.forward, fish = new Vector3(0, 0, 3f);
            brain.Begin(Vector3.zero);
            bool grabbed = false; float elapsed = 0; const float dt = .02f;
            for (int i = 0; i < 600 && !grabbed; i++)
            {
                var next = brain.Step(dt, hunter, heading, .365f, fish, Vector3.zero, null, out grabbed);
                float jawDistance = Vector3.Distance(GoshaHuntBrain.Jaws(next, brain.Velocity.normalized, .365f), fish);
                if (!grabbed) Assert.That(jawDistance, Is.GreaterThan(brain.Stats.CaptureReach), "no grab before contact");
                hunter = next; heading = brain.Velocity.sqrMagnitude > .001f ? brain.Velocity.normalized : heading; elapsed += dt;
            }
            Assert.That(grabbed, Is.True);
            Assert.That(elapsed, Is.GreaterThan(.3f), "the hunter had to swim there");
        }

        [Test]
        public void BlockedPathDeniesTheGrabAndTheChaseTimesOut()
        {
            var brain = new GoshaHuntBrain(); brain.Configure(Tuning.Stats(1, 0, 0, 0));
            Vector3 hunter = Vector3.zero, heading = Vector3.forward; var fish = new Vector3(0, 0, 2f);
            brain.Begin(Vector3.zero);
            bool grabbed = false; const float dt = .02f;
            for (int i = 0; i < 1400 && brain.Phase != GoshaHuntPhase.Failed; i++)
            {
                var next = brain.Step(dt, hunter, heading, .365f, fish, Vector3.zero, (a, b) => false, out bool grab);
                grabbed |= grab;
                hunter = next; heading = brain.Velocity.sqrMagnitude > .001f ? brain.Velocity.normalized : heading;
            }
            Assert.That(grabbed, Is.False, "a wall between the jaws and the fish blocks the grab");
            Assert.That(brain.Phase, Is.EqualTo(GoshaHuntPhase.Failed));
            Assert.That(brain.FailReason, Is.EqualTo("timeout"));
        }

        [Test]
        public void LungeOpensAtSpeedRankThree()
        {
            var trained = new GoshaHuntBrain(); trained.Configure(Tuning.Stats(3, 0, 0, 0));
            var plain = new GoshaHuntBrain(); plain.Configure(Tuning.Stats(2, 0, 0, 0));
            Assert.That(trained.Stats.HasLunge, Is.True);
            Assert.That(plain.Stats.HasLunge, Is.False);
            Simulate(trained, new Vector3(.5f, 0, 5f), out _);
            Assert.That(trained.LungeSequence, Is.GreaterThanOrEqualTo(1u));
        }

        [Test]
        public void LoadSlowsThePetAndClawsAddCapacity()
        {
            Assert.That(Tuning.Stats(0, 0, 0, 0).Capacity, Is.EqualTo(1));
            Assert.That(Tuning.Stats(0, 0, 2, 0).Capacity, Is.EqualTo(2));
            Assert.That(Tuning.Stats(0, 0, 3, 0).Capacity, Is.EqualTo(3));
            Assert.That(Tuning.Stats(0, 0, 4, 0).Capacity, Is.EqualTo(4));
            float empty = Tuning.Stats(2, 0, 3, 1).MaxSpeed, loaded = Tuning.Stats(2, 0, 3, 3).MaxSpeed, eased = Tuning.Stats(2, 0, 4, 3).MaxSpeed;
            Assert.That(loaded, Is.LessThan(empty));
            Assert.That(eased, Is.GreaterThan(loaded), "the fourth claws rank halves the load penalty");
        }
    }

    public sealed class GoshaSectorCatalogTests
    {
        [Test]
        public void GoshaSectorIsOneSharedTreeOfThirtyOneAbilities()
        {
            var sector = SkillTreeDefinition.All.Where(n => n.Branch == 7).ToList();
            Assert.That(sector.Count, Is.EqualTo(31));
            Assert.That(sector.All(n => n.Team), Is.True, "one physical pet, one shared tree");
            Assert.That(sector.All(n => SkillTreeDefinition.IsAbility(n.Line)), Is.True);
            Assert.That(sector.All(n => !n.ValueText.Contains("%")), Is.True, "no fake percent on discrete abilities");
            Assert.That(SkillTreeDefinition.MaxRank(SkillLine.GoshaAutonomy), Is.EqualTo(10));
            Assert.That(SkillTreeDefinition.MaxRank(SkillLine.GoshaHarvest), Is.EqualTo(1));
            var first = SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.GoshaAutonomy, 1));
            Assert.That(first.Cost.biomass + first.Cost.chitin + first.Cost.scrap + first.Cost.electronics, Is.LessThanOrEqualTo(12), "the first useful rank is cheap");
            Assert.That(first.Effect, Does.Contain("Первый улов"));
        }

        [Test]
        public void CapstoneNeedsEveryLineAtItsTop()
        {
            var capstone = SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.GoshaHarvest, 1));
            var deps = SkillTreeDefinition.Dependencies(capstone.Id).Select(SkillTreeDefinition.Get).ToList();
            Assert.That(deps.Any(d => d.Line == SkillLine.GoshaAutonomy && d.Rank == 10));
            Assert.That(deps.Any(d => d.Line == SkillLine.GoshaSpeed && d.Rank == 5));
            Assert.That(deps.Any(d => d.Line == SkillLine.GoshaClaws && d.Rank == 5));
            // The catalog forbids a capstone that exhausts its sector: the nose stops one rank short.
            Assert.That(deps.Any(d => d.Line == SkillLine.GoshaScent && d.Rank == 4));
            Assert.That(deps.Any(d => d.Line == SkillLine.GoshaDelivery && d.Rank == 5));
            Assert.That(SkillTreeDefinition.IsAvailable(capstone.Id, new List<SkillId>()), Is.False);
        }

        [Test]
        public void SharedPurchaseChargesTheTreasuryOnceForEveryone()
        {
            var tuning = ScriptableObject.CreateInstance<ProgressionTuning>();
            var team = new TeamProgress(tuning) { biomass = 400, chitin = 200, scrap = 100, electronics = 50 };
            var one = team.RegisterPlayer("lan:1");
            var two = team.RegisterPlayer("lan:2");
            var id = SkillTreeDefinition.Id(SkillLine.GoshaAutonomy, 1);
            var before = team.Treasury;
            Assert.That(team.TryPurchase(one, id, team.teamRevision, one.revision, tuning, () => true, out _), Is.True);
            Assert.That(team.TeamRank(SkillLine.GoshaAutonomy), Is.EqualTo(1));
            Assert.That(team.TryPurchase(two, id, team.teamRevision, two.revision, tuning, () => true, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("Ранг уже куплен"));
            Assert.That(team.Treasury.biomass, Is.EqualTo(before.biomass - SkillTreeDefinition.Get(id).Cost.biomass));
            team.Validate();
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void DirectDepositKeepsOneCopyAndRejectsSalvageInTheTank()
        {
            var tuning = ScriptableObject.CreateInstance<ProgressionTuning>();
            var team = new TeamProgress(tuning);
            var fish = new CatchItem(77, 2, 0, 1, 1, 1, 0, 0, "Needlefish");
            Assert.That(team.Deposit(fish, tuning), Is.True);
            Assert.That(team.Deposit(fish, tuning), Is.False, "the same cargo id never lands twice");
            Assert.That(team.tank.Count(x => x.id == 77), Is.EqualTo(1));
            int scrap = team.scrap;
            Assert.That(team.Deposit(CatchItem.Salvage(78, 1, 2, 0), tuning), Is.True);
            Assert.That(team.tank.Any(x => x.id == 78), Is.False, "materials never enter the water");
            Assert.That(team.scrap, Is.EqualTo(scrap + 2));
            Assert.That(team.LoadCargo(fish, tuning), Is.False, "a delivered id cannot enter the cage again");
            team.Validate();
            Object.DestroyImmediate(tuning);
        }
    }
}
