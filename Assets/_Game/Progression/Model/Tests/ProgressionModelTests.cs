using System.Collections.Generic;
using NUnit.Framework;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Expedition;
using UnityEngine;

namespace UnderwaterFishing.Progression.Tests
{
    public sealed class ProgressionModelTests
    {
        private ProgressionSettings settings;
        private ProgressionModel model;
        private CargoSettings cargoSettings;
        private CargoModel cargo;
        private Adapter adapter;
        [SetUp] public void SetUp()
        {
            cargoSettings = ScriptableObject.CreateInstance<CargoSettings>();
            Initialize(cargoSettings);
        }
        // Also permits the standalone managed verifier to supply settings without creating a Unity object.
        public void Initialize(CargoSettings configuration)
        {
            cargoSettings = configuration; cargoSettings.Capacity = 1000;
            cargoSettings.AscentSeconds = cargoSettings.UnloadSeconds = cargoSettings.DescentSeconds = 1;
            cargo = new CargoModel(cargoSettings); adapter = new Adapter(cargo);
            settings = new ProgressionSettings(); model = new ProgressionModel(settings);
        }
        [TearDown] public void TearDown() { DisposeModel(); Object.DestroyImmediate(cargoSettings); }
        public void DisposeModel() { cargo?.Dispose(); }
        private sealed class Adapter : IProtectedCargo
        {
            private readonly CargoModel cargo;
            public Adapter(CargoModel value) { cargo = value; }
            public IReadOnlyList<CargoItem> Hold => cargo.Hold;
            public bool TryConsume(ulong id, CargoDisposition disposition, out CargoItem item) => cargo.TryConsumeProtected(id, disposition, true, out item);
            public bool TryReserve(ulong id, bool reserve) => cargo.TrySetReserved(id, reserve, true);
        }
        private static CargoItem Item(ulong id, bool fish = true, int value = 12) => new CargoItem {
            Id = id, Name = "Test cargo", Size = 1, IsFish = fish, OfferingValue = fish ? value : 0,
            Yield = new CargoYield { Bio = 20, Rare = 20, Scrap = 20, Electronics = 20 }
        };
        private void Deliver(CargoItem item)
        { Assert.That(cargo.TryLoad(item), Is.True); Assert.That(cargo.StartLift(), Is.True); cargo.Step(3); }
        private void Fund() { Deliver(Item(100)); Assert.That(model.TryProcess(adapter, 100), Is.True); }

        [Test] public void NewRunHasNoFreeResourcesLevelsOrCharges()
        {
            Assert.That(model.Stock("bio"), Is.Zero); Assert.That(model.Stock("electronics"), Is.Zero);
            Assert.That(model.Level(CrewUpgrade.Oxygen), Is.Zero); Assert.That(model.Charges(1), Is.Zero);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.False);
        }
        [Test] public void ProcessingDeliveredLootCreditsAllConfiguredYieldsOnce()
        {
            Deliver(Item(1, false)); Assert.That(model.TryProcess(adapter, 1), Is.True);
            Assert.That(model.Stock("bio"), Is.EqualTo(20)); Assert.That(model.Stock("rare"), Is.EqualTo(20));
            Assert.That(model.Stock("scrap"), Is.EqualTo(20)); Assert.That(model.Stock("electronics"), Is.EqualTo(20));
            Assert.That(model.TryProcess(adapter, 1), Is.False); Assert.That(cargo.Hold, Is.Empty);
            Assert.That(cargo.TryLoad(Item(1)), Is.False);
        }
        [Test] public void NewResourceDefinitionCanReceiveCargoWithoutModelChanges()
        {
            settings.Resources = new[] { new ResourceDefinition { Id = "resin", Name = "Resin", PerBio = 2, PerScrap = 1 } };
            model = new ProgressionModel(settings); Deliver(Item(1));
            Assert.That(model.TryProcess(adapter, 1), Is.True); Assert.That(model.Stock("resin"), Is.EqualTo(60));
        }
        [Test] public void CargoStillInContainerCannotBeProcessed()
        {
            cargo.TryLoad(Item(1)); Assert.That(model.TryProcess(adapter, 1), Is.False);
            Assert.That(model.Stock("bio"), Is.Zero); Assert.That(cargo.Container.Count, Is.EqualTo(1));
        }
        [Test] public void ReservedFishCannotBeProcessedButCanBeUnreservedAtBase()
        {
            Deliver(Item(1)); Assert.That(model.TryReserve(adapter, 1, true), Is.True);
            Assert.That(model.TryProcess(adapter, 1), Is.False);
            Assert.That(model.TryReserve(adapter, 1, false), Is.True); Assert.That(model.TryProcess(adapter, 1), Is.True);
            Assert.That(model.TryReserve(adapter, 1, true), Is.False);
        }
        [Test] public void TechnicalLootCannotBeReserved()
        { Deliver(Item(1, false)); Assert.That(model.TryReserve(adapter, 1, true), Is.False); }
        [TestCase(ExpeditionPhase.Expedition)]
        [TestCase(ExpeditionPhase.Return)]
        [TestCase(ExpeditionPhase.Offering)]
        public void ProcessingPurchasesAndCraftingAreBaseOnly(ExpeditionPhase phase)
        {
            Fund(); Deliver(Item(1)); model.SetPhase(phase);
            Assert.That(model.TryProcess(adapter, 1), Is.False);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.False);
            Assert.That(model.TryCraftSonar(7, 0, model.Revision), Is.False);
        }
        [Test] public void InsufficientPurchaseDoesNotPartiallySpend()
        {
            var item = Item(1); item.Yield = new CargoYield { Bio = 4 }; Deliver(item); model.TryProcess(adapter, 1);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.False);
            Assert.That(model.Stock("bio"), Is.EqualTo(4)); Assert.That(model.Level(CrewUpgrade.Oxygen), Is.Zero);
        }
        [Test] public void DuplicateCostRowsAreAggregatedBeforeSpending()
        {
            settings.Upgrades[0].Costs = new[] { new ResourceAmount("bio", 15), new ResourceAmount("bio", 15) };
            Fund(); Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.False);
            Assert.That(model.Stock("bio"), Is.EqualTo(20));
        }
        [Test] public void PurchaseCostsIncreaseAndChangeMeasuredEffects()
        {
            Fund(); int initial = model.UpgradeCost(CrewUpgrade.Oxygen, 0)[0].Amount;
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.True);
            Assert.That(model.UpgradeCost(CrewUpgrade.Oxygen, 1)[0].Amount, Is.GreaterThan(initial));
            Assert.That(model.Effects.OxygenMultiplier, Is.EqualTo(1.25f));
            Assert.That(model.TryBuy(CrewUpgrade.Reel, 0, model.Revision), Is.True);
            Assert.That(model.TryBuy(CrewUpgrade.Suit, 0, model.Revision), Is.True);
            Assert.That(model.TryBuy(CrewUpgrade.Winch, 0, model.Revision), Is.True);
            Assert.That(model.Effects.ReelMultiplier, Is.EqualTo(1.25f)); Assert.That(model.Effects.SafeDepthBonus, Is.EqualTo(8));
            Assert.That(model.Effects.WinchMultiplier, Is.EqualTo(1.35f));
        }
        [Test] public void SimultaneousPurchaseWithSameLevelAndRevisionSpendsOnce()
        {
            Fund(); uint revision = model.Revision;
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, revision), Is.True); int after = model.Stock("bio");
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, revision), Is.False);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.False);
            Assert.That(model.Stock("bio"), Is.EqualTo(after)); Assert.That(model.Level(CrewUpgrade.Oxygen), Is.EqualTo(1));
        }
        [Test] public void OtherClientsResourceChangeInvalidatesOldPurchaseRevision()
        {
            Fund(); uint revision = model.Revision; model.TryBuy(CrewUpgrade.Reel, 0, revision);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, revision), Is.False);
            Assert.That(model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision), Is.True);
        }
        [Test] public void SonarIsPersonalCostsMaterialsAndConsumesOnlyInExpedition()
        {
            Fund(); uint revision = model.Revision;
            Assert.That(model.TryCraftSonar(7, 0, revision), Is.True);
            Assert.That(model.TryCraftSonar(7, 0, revision), Is.False);
            Assert.That(model.Stock("electronics"), Is.EqualTo(19)); Assert.That(model.Charges(8), Is.Zero);
            Assert.That(model.TryUseSonar(7, 1), Is.False); model.SetPhase(ExpeditionPhase.Expedition);
            Assert.That(model.TryUseSonar(8, 1), Is.False); Assert.That(model.TryUseSonar(7, 1), Is.True);
            Assert.That(model.TryUseSonar(7, 1), Is.False); Assert.That(model.Charges(7), Is.Zero);
        }
        [Test] public void DisconnectDropsChargesWithoutRefundOrAffectingOthers()
        {
            Fund(); model.TryCraftSonar(7, 0, model.Revision); model.TryCraftSonar(8, 0, model.Revision);
            model.RemoveCrew(7); Assert.That(model.Charges(7), Is.Zero); Assert.That(model.Charges(8), Is.EqualTo(1));
            Assert.That(model.Stock("electronics"), Is.EqualTo(18));
        }
        [Test] public void ThirdDayFishCanBeReservedDuringOfferingWindow()
        {
            Deliver(Item(1)); model.SetPhase(ExpeditionPhase.Offering);
            Assert.That(model.OfferingSeconds, Is.EqualTo(45)); Assert.That(model.StepOffering(10), Is.False);
            Assert.That(model.TryReserve(adapter, 1, true), Is.True);
            Assert.That(model.ResolveOffering(adapter, out bool success), Is.True); Assert.That(success, Is.True);
            Assert.That(cargo.Hold, Is.Empty); Assert.That(model.Stock("bio"), Is.Zero);
        }
        [Test] public void OfferingDeadlineExpiresAndInsufficientReservationFailsWithoutConsumption()
        {
            Deliver(Item(1, true, 3)); model.TryReserve(adapter, 1, true); model.SetPhase(ExpeditionPhase.Offering);
            Assert.That(model.StepOffering(45), Is.True); Assert.That(model.ResolveOffering(adapter, out bool success), Is.True);
            Assert.That(success, Is.False); Assert.That(model.LastOffering, Is.EqualTo(OfferingResult.Failure));
            Assert.That(cargo.Hold.Count, Is.EqualTo(1)); Assert.That(model.Quota, Is.EqualTo(12));
        }
        [Test] public void SuccessfulOfferingGrowsQuotaAndRetainsStockLevelsChargesAndExcessHold()
        {
            Fund(); model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision); model.TryCraftSonar(7, 0, model.Revision);
            int stock = model.Stock("bio"); Deliver(Item(1)); Deliver(Item(2)); Deliver(Item(3, false));
            model.TryReserve(adapter, 1, true); model.TryReserve(adapter, 2, true); model.SetPhase(ExpeditionPhase.Offering);
            model.ResolveOffering(adapter, out bool success); model.SetPhase(ExpeditionPhase.Base);
            Assert.That(success, Is.True); Assert.That(model.Quota, Is.EqualTo(17)); Assert.That(model.SuccessfulOfferings, Is.EqualTo(1));
            Assert.That(model.Stock("bio"), Is.EqualTo(stock)); Assert.That(model.Level(CrewUpgrade.Oxygen), Is.EqualTo(1));
            Assert.That(model.Charges(7), Is.EqualTo(1)); Assert.That(cargo.Hold.Count, Is.EqualTo(2));
            Assert.That(model.TryProcess(adapter, 1), Is.False); Assert.That(cargo.TryLoad(Item(1)), Is.False);
        }
        [Test] public void OfferingCannotBeResolvedTwiceOrConsumeProcessedFish()
        {
            Deliver(Item(1)); model.TryProcess(adapter, 1); model.SetPhase(ExpeditionPhase.Offering);
            model.ResolveOffering(adapter, out bool success); Assert.That(success, Is.False);
            Assert.That(model.ResolveOffering(adapter, out _), Is.False); Assert.That(model.Stock("bio"), Is.EqualTo(20));
        }
        [Test] public void RunResetClearsResourcesLevelsChargesQuotaAndCargo()
        {
            Fund(); model.TryBuy(CrewUpgrade.Oxygen, 0, model.Revision); model.TryCraftSonar(7, 0, model.Revision);
            Deliver(Item(1)); model.TryReserve(adapter, 1, true); model.SetPhase(ExpeditionPhase.Offering); model.ResolveOffering(adapter, out _);
            model.SetPhase(ExpeditionPhase.Failure); model.ResetRun(); cargo.ResetRun();
            Assert.That(model.Stock("bio"), Is.Zero); Assert.That(model.Level(CrewUpgrade.Oxygen), Is.Zero);
            Assert.That(model.Charges(7), Is.Zero); Assert.That(model.Quota, Is.EqualTo(12)); Assert.That(model.LastOffering, Is.EqualTo(OfferingResult.None));
            Assert.That(cargo.Hold, Is.Empty); Assert.That(model.Phase, Is.EqualTo(ExpeditionPhase.Base));
        }
        [Test] public void OverflowingResourceYieldRejectsBeforeCargoConsumption()
        {
            settings.Resources[0].PerBio = int.MaxValue; Deliver(Item(1));
            Assert.That(model.TryProcess(adapter, 1), Is.False); Assert.That(cargo.Hold.Count, Is.EqualTo(1)); Assert.That(model.Stock("bio"), Is.Zero);
        }
        [Test] public void ReservationCannotChangeDuringDive()
        {
            Deliver(Item(1)); model.SetPhase(ExpeditionPhase.Expedition);
            Assert.That(model.TryReserve(adapter, 1, true), Is.False); Assert.That(cargo.Hold[0].Reserved, Is.False);
        }
    }
}
