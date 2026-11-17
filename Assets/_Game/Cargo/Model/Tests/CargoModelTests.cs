using NUnit.Framework;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Tests
{
    public sealed class CargoModelTests
    {
        private CargoSettings settings;
        private CargoModel cargo;
        [SetUp] public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<CargoSettings>();
            settings.Capacity = 5; settings.AscentSeconds = settings.UnloadSeconds = settings.DescentSeconds = 1;
            cargo = new CargoModel(settings);
        }
        [TearDown] public void TearDown() { cargo.Dispose(); Object.DestroyImmediate(settings); }
        private static CargoItem Fish(ulong id, int size = 2) => new CargoItem {
            Id = id, Name = "Fish", Size = size, IsFish = true, OfferingValue = 3, Yield = new CargoYield { Bio = 2 }
        };
        private void Deliver(CargoItem item)
        { Assert.That(cargo.TryLoad(item), Is.True); Assert.That(cargo.StartLift(), Is.True); cargo.Step(3); }

        [Test] public void CapacityRejectsOverfillWithoutLosingExistingLoad()
        {
            Assert.That(cargo.TryLoad(Fish(1, 4)), Is.True);
            Assert.That(cargo.TryLoad(Fish(2, 2)), Is.False);
            Assert.That(cargo.UsedCapacity, Is.EqualTo(4));
            Assert.That(cargo.Container.Count, Is.EqualTo(1));
        }
        [Test] public void DuplicateIdIsRejectedBeforeAndAfterDeliveryAndConsumption()
        {
            CargoItem item = Fish(1);
            Assert.That(cargo.TryLoad(item), Is.True);
            Assert.That(cargo.TryLoad(item), Is.False);
            cargo.StartLift(); cargo.Step(3);
            Assert.That(cargo.TryLoad(item), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out _), Is.True);
            Assert.That(cargo.TryLoad(item), Is.False);
        }
        [Test] public void InvalidIdsSizesAndYieldsAreRejected()
        {
            Assert.That(cargo.TryLoad(Fish(0)), Is.False);
            Assert.That(cargo.TryLoad(Fish(1, 0)), Is.False);
            var invalid = Fish(2); invalid.Yield.Bio = -1;
            Assert.That(cargo.TryLoad(invalid), Is.False);
            Assert.That(cargo.Container, Is.Empty);
        }
        [Test] public void WinchVisitsEveryStateAndTransfersOnlyAfterUnloading()
        {
            cargo.TryLoad(Fish(1)); cargo.StartLift();
            Assert.That(cargo.State, Is.EqualTo(WinchState.Ascending));
            cargo.Step(1); Assert.That(cargo.State, Is.EqualTo(WinchState.Unloading));
            Assert.That(cargo.Hold, Is.Empty);
            cargo.Step(1); Assert.That(cargo.State, Is.EqualTo(WinchState.Descending));
            Assert.That(cargo.Hold.Count, Is.EqualTo(1)); Assert.That(cargo.Container, Is.Empty);
            cargo.Step(1); Assert.That(cargo.State, Is.EqualTo(WinchState.Deployed));
            Assert.That(cargo.Hold.Count, Is.EqualTo(1));
        }
        [Test] public void EveryMovingStateRejectsLoadAndSecondLift()
        {
            cargo.TryLoad(Fish(1)); cargo.StartLift();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(cargo.TryLoad(Fish(2)), Is.False);
                Assert.That(cargo.StartLift(), Is.False);
                cargo.Step(1);
            }
            Assert.That(cargo.TryLoad(Fish(2)), Is.True);
        }
        [Test] public void RepeatCycleKeepsPriorHoldAndDoesNotDuplicateManifest()
        {
            Deliver(Fish(1)); Deliver(Fish(2)); cargo.Step(100);
            Assert.That(cargo.Hold.Count, Is.EqualTo(2));
            Assert.That(cargo.Hold[0].Id, Is.EqualTo(1)); Assert.That(cargo.Hold[1].Id, Is.EqualTo(2));
            Assert.That(cargo.UsedCapacity, Is.Zero);
        }
        [Test] public void FasterWinchActuallyCompletesTravelSooner()
        {
            cargo.TryLoad(Fish(1)); cargo.SpeedMultiplier = 2; cargo.StartLift(); cargo.Step(.5f);
            Assert.That(cargo.State, Is.EqualTo(WinchState.Unloading));
            cargo.Step(1); Assert.That(cargo.State, Is.EqualTo(WinchState.Deployed));
            Assert.That(cargo.Hold.Count, Is.EqualTo(1));
        }
        [Test] public void EmergencyReturnLosesOnlyUnprotectedLoadAndKeepsUsedIds()
        {
            Deliver(Fish(1)); cargo.TryLoad(Fish(2)); cargo.StartLift(); cargo.Step(1);
            cargo.ResetExpedition();
            Assert.That(cargo.State, Is.EqualTo(WinchState.Deployed));
            Assert.That(cargo.Hold.Count, Is.EqualTo(1)); Assert.That(cargo.Container, Is.Empty);
            Assert.That(cargo.TryLoad(Fish(2)), Is.False);
        }
        [Test] public void ProtectedItemCannotBeConsumedTwice()
        {
            Deliver(Fish(1));
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out var item), Is.True);
            Assert.That(item.Yield.Bio, Is.EqualTo(2));
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Offering, true, out _), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out _), Is.False);
        }
        [Test] public void ReservedFishCannotBeProcessedAndOfferingCannotBeProcessedAgain()
        {
            Deliver(Fish(1));
            Assert.That(cargo.TrySetReserved(1, true, true), Is.True);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out _), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Offering, true, out var item), Is.True);
            Assert.That(item.OfferingValue, Is.EqualTo(3));
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out _), Is.False);
        }
        [Test] public void ExpeditionCannotProcessOrReserveProtectedCargo()
        {
            Deliver(Fish(1));
            Assert.That(cargo.TrySetReserved(1, true, false), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, false, out _), Is.False);
            Assert.That(cargo.Hold.Count, Is.EqualTo(1));
        }
        [Test] public void TechnicalLootCannotBeReservedOrOffered()
        {
            var item = Fish(1); item.IsFish = false; item.OfferingValue = 0;
            Deliver(item);
            Assert.That(cargo.TrySetReserved(1, true, true), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Offering, true, out _), Is.False);
            Assert.That(cargo.TryConsumeProtected(1, CargoDisposition.Process, true, out _), Is.True);
        }
        [Test] public void NewRunClearsAllCargoAndReservations()
        {
            Deliver(Fish(1)); cargo.TrySetReserved(1, true, true); cargo.TryLoad(Fish(2));
            cargo.ResetRun();
            Assert.That(cargo.Hold, Is.Empty); Assert.That(cargo.Container, Is.Empty);
            Assert.That(cargo.UsedCapacity, Is.Zero); Assert.That(cargo.State, Is.EqualTo(WinchState.Deployed));
        }
        [Test] public void EmptyLiftAndInvalidTimeCannotChangeTheManifest()
        {
            Assert.That(cargo.StartLift(), Is.False);
            cargo.TryLoad(Fish(1)); cargo.StartLift(); cargo.Step(float.NaN); cargo.Step(-1);
            Assert.That(cargo.State, Is.EqualTo(WinchState.Ascending));
            Assert.That(cargo.Container.Count, Is.EqualTo(1));
        }
    }
}
