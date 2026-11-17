using NUnit.Framework;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Tests
{
    public sealed class ExpeditionModelTests
    {
        private ExpeditionSettings settings;
        private ExpeditionModel model;
        private static ExpeditionCrewStatus Crew(int id, bool aboard = true, bool alive = true)
            => new ExpeditionCrewStatus { Id = id, Connected = true, Alive = alive, Aboard = aboard };
        [SetUp] public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<ExpeditionSettings>();
            settings.PreparingSeconds = settings.ReturnSeconds = settings.DebriefSeconds = .5f;
            settings.CargoWaitTimeoutSeconds = 2;
            model = new ExpeditionModel(settings, false, 20);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(settings); }
        private void Start(params ExpeditionCrewStatus[] crew)
        {
            model.UpdateCrew(crew); Assert.That(model.TryStart(1), Is.True); model.Step(.5f, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Expedition));
        }
        private void FinishDay()
        { Assert.That(model.TryRequestReturn(1), Is.True); model.Step(2, true); }
        [Test] public void NormalStartRequiresTwoConnectedPlayers()
        {
            model.UpdateCrew(new[] { Crew(1) }); Assert.That(model.TryStart(1), Is.False);
            model.UpdateCrew(new[] { Crew(1), Crew(2) }); Assert.That(model.TryStart(1), Is.True);
            Assert.That(model.TryStart(2), Is.False);
        }
        [Test] public void SoloStartRequiresExplicitDevelopmentMode()
        {
            model = new ExpeditionModel(settings, true, 20);
            Start(Crew(1)); Assert.That(model.RequiredCrew, Is.EqualTo(1));
        }
        [Test] public void DisconnectDuringPreparingCancelsWithoutUsingADay()
        {
            model.UpdateCrew(new[] { Crew(1), Crew(2) }); model.TryStart(1);
            model.UpdateCrew(new[] { Crew(1) }); model.Step(2, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base)); Assert.That(model.CompletedDays, Is.Zero);
        }
        [Test] public void LivingDiverInWaterMustReturnOrExplicitlyConsent()
        {
            Start(Crew(1), Crew(2, false)); model.TryRequestReturn(1); model.Step(.1f, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Expedition)); Assert.That(model.PendingCrewCount, Is.EqualTo(1));
            Assert.That(model.TrySetReturnConsent(2, true), Is.True); model.Step(.1f, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Return)); Assert.That(model.Emergency, Is.False);
        }
        [Test] public void DeadCrewDoNotBlockNormalReturn()
        {
            Start(Crew(1), Crew(2, false, false)); FinishDay();
            Assert.That(model.CompletedDays, Is.EqualTo(1)); Assert.That(model.Emergency, Is.False);
        }
        [Test] public void ReturningAboardSatisfiesPendingVoteWithoutConsent()
        {
            Start(Crew(1), Crew(2, false)); model.TryRequestReturn(1); model.Step(.1f, true);
            model.UpdateCrew(new[] { Crew(1), Crew(2) }); model.Step(.1f, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Return));
        }
        [Test] public void DisconnectedCrewAndTheirConsentsAreRemoved()
        {
            Start(Crew(1), Crew(2, false)); model.TryRequestReturn(1); model.TrySetReturnConsent(2, true);
            model.RemoveCrew(2); model.UpdateCrew(new[] { Crew(1), Crew(3, false) });
            Assert.That(model.HasConsented(2), Is.False); Assert.That(model.HasConsented(3), Is.False);
            model.Step(.1f, true); Assert.That(model.PendingCrewCount, Is.EqualTo(1));
        }
        [Test] public void ConsentCannotBeInjectedBeforeReturnRequestOrByUnknownCrew()
        {
            Start(Crew(1), Crew(2, false));
            Assert.That(model.TrySetReturnConsent(2, true), Is.False);
            model.TryRequestReturn(1); Assert.That(model.TrySetReturnConsent(99, true), Is.False);
            Assert.That(model.PendingCrewCount, Is.EqualTo(1));
        }
        [Test] public void TimeoutFinishesEvenWithUnprotectedCargoAndMissingConsent()
        {
            Start(Crew(1), Crew(2, false)); model.TryRequestReturn(1); model.Step(22, false);
            Assert.That(model.Emergency, Is.True); Assert.That(model.CompletedDays, Is.EqualTo(1));
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base));
        }
        [Test] public void NormalReturnWaitsForCurrentContainerUnload()
        {
            Start(Crew(1), Crew(2)); model.TryRequestReturn(1); model.Step(.75f, false);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Return)); Assert.That(model.CompletedDays, Is.Zero);
            model.Step(1, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base)); Assert.That(model.CompletedDays, Is.EqualTo(1));
            Assert.That(model.Emergency, Is.False);
        }
        [Test] public void StuckCargoFallsBackToBoundedEmergencyReturn()
        {
            Start(Crew(1), Crew(2)); model.TryRequestReturn(1); model.Step(4, false);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base)); Assert.That(model.CompletedDays, Is.EqualTo(1));
            Assert.That(model.Emergency, Is.True);
        }
        [Test] public void DayIsIncrementedOnceAndBaseClockCannotCreateExtraDays()
        {
            Start(Crew(1), Crew(2)); FinishDay(); model.Step(1000, true); model.Step(1000, true);
            Assert.That(model.CompletedDays, Is.EqualTo(1)); Assert.That(model.Day, Is.EqualTo(2));
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base));
        }
        private void ReachOffering()
        {
            for (int day = 0; day < 3; day++) { Start(Crew(1), Crew(2)); FinishDay(); }
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Offering));
        }
        [Test] public void ThirdCompletedDayRequiresOfferingBeforeAnotherExpedition()
        {
            ReachOffering(); Assert.That(model.CompletedDays, Is.EqualTo(3));
            Assert.That(model.TryStart(1), Is.False); model.Step(100, true);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Offering));
            Assert.That(model.CompleteOffering(true), Is.True);
            Assert.That(model.CompletedDays, Is.Zero); Assert.That(model.TryStart(1), Is.True);
        }
        [Test] public void FailedOfferingEndsRunUntilExplicitReset()
        {
            ReachOffering(); Assert.That(model.CompleteOffering(false), Is.True);
            Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Failure)); Assert.That(model.TryStart(1), Is.False);
            model.ResetRun(); Assert.That(model.CompletedDays, Is.Zero);
            Assert.That(model.TryStart(1), Is.True);
        }
        [Test] public void InvalidTimeAndOutOfPhaseOfferingCannotAdvanceDays()
        {
            Assert.That(model.CompleteOffering(true), Is.False);
            Start(Crew(1), Crew(2)); model.Step(float.NaN, true); model.Step(-1, true);
            Assert.That(model.ExpeditionRemaining, Is.EqualTo(20)); Assert.That(model.CompletedDays, Is.Zero);
        }
    }
}
