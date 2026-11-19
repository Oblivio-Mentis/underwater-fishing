using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class TeamProgressTests
    {
        private ProgressionTuning tuning;
        private TeamProgress team;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<ProgressionTuning>();
            team = new TeamProgress();
            savePath = Path.Combine(Path.GetTempPath(), "raid-team-test-" + Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(tuning);
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
        }

        private static CatchItem Fish(long id, int size = 1) => new CatchItem(id, 4, 1, 3, size, 1);

        [Test]
        public void LaterTierUpgradeRequiresNewMaterialsWithoutPartiallyCharging()
        {
            tuning.Recipe(Upgrade.Reel).additionalPerLevel=new MaterialCost(0,2,1,1);
            team.biomass=100;
            Assert.That(team.Buy(Upgrade.Reel,true,tuning),Is.True);
            int balance=team.biomass;
            Assert.That(team.Buy(Upgrade.Reel,true,tuning),Is.False);
            Assert.That(team.biomass,Is.EqualTo(balance));
            team.chitin=2;team.scrap=1;team.electronics=1;
            Assert.That(team.Buy(Upgrade.Reel,true,tuning),Is.True);
            Assert.That(team.chitin+team.scrap+team.electronics,Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FishCanBeConsumedOnlyOnce(bool processFirst)
        {
            Assert.That(team.Deposit(Fish(1)), Is.True);
            Assert.That(processFirst ? team.Process(1) : team.Feed(1), Is.True);
            Assert.That(team.Process(1), Is.False);
            Assert.That(team.Feed(1), Is.False);
            Assert.That(team.Deposit(Fish(1)), Is.False, "A spent ID cannot be reintroduced.");
            Assert.That(team.biomass, Is.EqualTo(processFirst ? 4 : 0));
            Assert.That(team.chitin, Is.EqualTo(processFirst ? 1 : 0));
            Assert.That(team.offering, Is.EqualTo(processFirst ? 0 : 3));
            Assert.That(team.lifetimeCatch, Is.EqualTo(5));
        }

        [Test]
        public void DeliverySnapshotsItemAndCountsLifetimeOnlyOnce()
        {
            CatchItem fish = Fish(1);
            Assert.That(team.Deposit(fish), Is.True);
            fish.biomass = 900;
            Assert.That(team.Deposit(Fish(1)), Is.False);
            team.Process(1);
            Assert.That(team.biomass, Is.EqualTo(4), "External mutable item cannot change delivered yields.");
            Assert.That(team.lifetimeCatch, Is.EqualTo(5));
        }

        [Test]
        public void UpgradePriceScalesAndChargesExactMaterials()
        {
            tuning.Recipe(Upgrade.Reel).initialCost = new MaterialCost(3, 2, 1, 1);
            tuning.Recipe(Upgrade.Reel).costMultiplier = 1.5f;
            team.biomass = 30; team.chitin = 20; team.scrap = 10; team.electronics = 10;
            Assert.That(team.Buy(Upgrade.Reel, true, tuning), Is.True);
            MaterialCost next = team.Cost(Upgrade.Reel, tuning);
            Assert.That(next.biomass, Is.EqualTo(5));
            Assert.That(next.chitin, Is.EqualTo(3));
            Assert.That(next.scrap, Is.EqualTo(2));
            Assert.That(next.electronics, Is.EqualTo(2));
            Assert.That(team.biomass, Is.EqualTo(27));
            Assert.That(team.chitin, Is.EqualTo(18));
            Assert.That(team.scrap, Is.EqualTo(9));
            Assert.That(team.electronics, Is.EqualTo(9));
        }

        [Test]
        public void DecimalPriceMultiplierDoesNotAddSpuriousUnitFromFloatRounding()
        {
            tuning.Recipe(Upgrade.Reel).initialCost = new MaterialCost(10);
            tuning.Recipe(Upgrade.Reel).costMultiplier = 1.7f;
            team.reelLevel = 1;
            Assert.That(team.Cost(Upgrade.Reel, tuning).biomass, Is.EqualTo(17));
        }

        [Test]
        public void MissingOneMaterialOrBeingUnderwaterDoesNotChargeAnything()
        {
            tuning.Recipe(Upgrade.Reel).initialCost = new MaterialCost(3, 2, 1, 1);
            team.biomass = 30; team.chitin = 20; team.scrap = 10;
            Assert.That(team.Buy(Upgrade.Reel, true, tuning), Is.False);
            team.electronics = 1;
            Assert.That(team.Buy(Upgrade.Reel, false, tuning), Is.False);
            Assert.That(team.biomass, Is.EqualTo(30));
            Assert.That(team.chitin, Is.EqualTo(20));
            Assert.That(team.scrap, Is.EqualTo(10));
            Assert.That(team.electronics, Is.EqualTo(1));
            Assert.That(team.reelLevel, Is.Zero);
        }

        [Test]
        public void MaximumUpgradeLevelCannotSpendResourcesAgain()
        {
            tuning.Recipe(Upgrade.Fins).maximumLevel = 1;
            team.biomass = 100;
            Assert.That(team.Buy(Upgrade.Fins, true, tuning), Is.True);
            int balance = team.biomass;
            Assert.That(team.Buy(Upgrade.Fins, true, tuning), Is.False);
            Assert.That(team.biomass, Is.EqualTo(balance));
            Assert.That(team.finsLevel, Is.EqualTo(1));
        }

        [Test]
        public void DepthTwoUnlockUsesCumulativeCatchAndOnlyAccessibleFirstDepthMaterials()
        {
            team.biomass = 100;
            Assert.That(team.Buy(Upgrade.Hull, true, tuning), Is.False);
            team.biomass = 0;
            for (int i = 1; i <= 6; i++) { team.Deposit(Fish(i)); team.Process(i); }
            Assert.That(team.lifetimeCatch, Is.EqualTo(30));
            MaterialCost cost = team.Cost(Upgrade.Hull, tuning);
            Assert.That(cost.chitin + cost.scrap + cost.electronics, Is.Zero);
            Assert.That(team.Buy(Upgrade.Hull, true, tuning), Is.True);
            Assert.That(team.CanVisitDepth(1), Is.True);
            Assert.That(team.CanVisitDepth(2), Is.True);
            Assert.That(team.lifetimeCatch, Is.EqualTo(30), "Paying materials does not reduce technology progress.");
        }

        [Test]
        public void CargoCapacityAndDuplicateIdentityAreEnforced()
        {
            tuning.cargoCapacity = 3;
            Assert.That(team.LoadCargo(Fish(1, 2), tuning), Is.True);
            Assert.That(team.LoadCargo(Fish(1), tuning), Is.False);
            Assert.That(team.Deposit(Fish(1)), Is.False, "Cargo cannot bypass delivery by direct deposit.");
            Assert.That(team.LoadCargo(Fish(2, 2), tuning), Is.False);
            Assert.That(team.LoadCargo(Fish(2), tuning), Is.True);
            Assert.That(team.CargoUsed, Is.EqualTo(3));
            Assert.That(team.lifetimeCatch, Is.Zero);
        }

        [Test]
        public void HoistDeliversOnlyAfterUnloadingAndUnlocksAfterReturn()
        {
            tuning.raisingSeconds = 2; tuning.unloadingSeconds = 3; tuning.returningSeconds = 4;
            team.LoadCargo(Fish(1), tuning);
            Assert.That(team.BeginHoist(tuning), Is.True);
            team.TickHoist(2, tuning);
            Assert.That(team.hoistPhase, Is.EqualTo(HoistPhase.Unloading));
            Assert.That(team.tank, Is.Empty);
            Assert.That(team.LoadCargo(Fish(2), tuning), Is.False);
            team.TickHoist(3, tuning);
            Assert.That(team.hoistPhase, Is.EqualTo(HoistPhase.Returning));
            Assert.That(team.tank.Count, Is.EqualTo(1));
            Assert.That(team.lifetimeCatch, Is.EqualTo(5));
            Assert.That(team.LoadCargo(Fish(2), tuning), Is.False);
            team.TickHoist(4, tuning);
            Assert.That(team.hoistPhase, Is.EqualTo(HoistPhase.Loading));
            Assert.That(team.LoadCargo(Fish(2), tuning), Is.True);
            Assert.That(team.BeginHoist(tuning), Is.True);
            team.TickHoist(90, tuning);
            Assert.That(team.hoistPhase, Is.EqualTo(HoistPhase.Loading));
            Assert.That(team.tank.Count, Is.EqualTo(2));
            Assert.That(team.lifetimeCatch, Is.EqualTo(10));
        }

        [Test]
        public void WinchUpgradeActuallyIncreasesCapacityAndShortensCycle()
        {
            team.biomass = 100;
            int capacity = team.Capacity(tuning);
            Assert.That(team.Buy(Upgrade.Winch, true, tuning), Is.True);
            Assert.That(team.Capacity(tuning), Is.GreaterThan(capacity));
            team.LoadCargo(Fish(1), tuning);
            team.BeginHoist(tuning);
            Assert.That(team.hoistRemaining, Is.LessThan(tuning.raisingSeconds));
        }

        [TestCase(0, FeedingOutcome.None, 1f)]
        [TestCase(6, FeedingOutcome.Underfed, .5f)]
        [TestCase(12, FeedingOutcome.Enough, 0f)]
        [TestCase(20, FeedingOutcome.Overfed, 0f)]
        public void FeedingHasFourOutcomesAndBoundedCarry(int offered, FeedingOutcome outcome, float deficit)
        {
            team.offering = offered;
            HungerReport report = team.EvaluateHunger(tuning);
            Assert.That(report.outcome, Is.EqualTo(outcome));
            Assert.That(report.deficit, Is.EqualTo(deficit).Within(.001));
            Assert.That(report.Attack, Is.EqualTo(offered < 12));
            Assert.That(team.offering, Is.EqualTo(Math.Min(3, Math.Max(0, offered - 12))));
        }

        [Test]
        public void FailedDefenseLimitsStockLossAndPreservesPermanentProgress()
        {
            for (int i = 1; i <= 20; i++) team.Deposit(Fish(i));
            team.biomass = 100;
            team.Buy(Upgrade.Hull, true, tuning);
            team.Buy(Upgrade.Reel, true, tuning);
            int resources = team.biomass;
            Assert.That(team.ApplyStockLoss(tuning), Is.EqualTo(4));
            Assert.That(team.tank.Count, Is.EqualTo(16));
            Assert.That(team.lifetimeCatch, Is.EqualTo(100));
            Assert.That(team.highestDepth, Is.EqualTo(2));
            Assert.That(team.hullLevel, Is.EqualTo(1));
            Assert.That(team.reelLevel, Is.EqualTo(1));
            Assert.That(team.biomass, Is.EqualTo(resources));
        }

        [Test]
        public void StockLossAlsoObeysFractionalCapForSmallStock()
        {
            for (int i = 1; i <= 3; i++) team.Deposit(Fish(i));
            tuning.maximumStockLossFraction = .35f;
            Assert.That(team.ApplyStockLoss(tuning), Is.EqualTo(1));
            Assert.That(team.ApplyStockLoss(tuning, 0f), Is.Zero);
        }

        [Test]
        public void SaveRoundTripPreservesProgressFeedingAndMidHoistCargo()
        {
            team.Deposit(Fish(1)); team.Feed(1);
            team.Deposit(Fish(2)); team.Process(2);
            team.LoadCargo(Fish(3), tuning); team.BeginHoist(tuning); team.TickHoist(2, tuning);
            TeamSave.Save(savePath, team);
            TeamProgress restored = TeamSave.Load(savePath);
            Assert.That(restored.biomass, Is.EqualTo(4));
            Assert.That(restored.offering, Is.EqualTo(3));
            Assert.That(restored.lifetimeCatch, Is.EqualTo(10));
            Assert.That(restored.hoistPhase, Is.EqualTo(HoistPhase.Raising));
            Assert.That(restored.hoistRemaining, Is.EqualTo(team.hoistRemaining));
            Assert.That(restored.Deposit(Fish(1)), Is.False);
            restored.TickHoist(100, tuning);
            Assert.That(restored.tank.Count, Is.EqualTo(1));
            Assert.That(restored.lifetimeCatch, Is.EqualTo(15));
            TeamSave.Save(savePath, restored);
            Assert.That(TeamSave.Load(savePath + ".bak").lifetimeCatch, Is.EqualTo(10));
        }

        [Test]
        public void DeliveredLootSurvivesSaveAndProcessesScrapAndElectronicsExactlyOnce()
        {
            Assert.That(team.Deposit(new CatchItem(7001, 0, 0, 0, 2, 2, 5, 3)), Is.True);
            TeamSave.Save(savePath, team);
            TeamProgress restored = TeamSave.Load(savePath);
            Assert.That(restored.tank.Count, Is.EqualTo(1));
            Assert.That(restored.tank[0].scrap, Is.EqualTo(5));
            Assert.That(restored.tank[0].electronics, Is.EqualTo(3));
            Assert.That(restored.tank[0].depthTier, Is.EqualTo(2));
            Assert.That(restored.Process(7001), Is.True);
            Assert.That(restored.Process(7001), Is.False);
            TeamSave.Save(savePath, restored);
            TeamProgress processed = TeamSave.Load(savePath);
            Assert.That(processed.scrap, Is.EqualTo(5));
            Assert.That(processed.electronics, Is.EqualTo(3));
            Assert.That(processed.biomass + processed.chitin, Is.Zero);
            Assert.That(processed.lifetimeCatch, Is.EqualTo(8));
            Assert.That(processed.tank, Is.Empty);
            Assert.That(processed.Deposit(new CatchItem(7001, 0, 0, 0, 2, 2, 5, 3)), Is.False);
        }

        [Test]
        public void ZeroOfferingLootCannotBeFedOrDiscardedByFeeding()
        {
            team.offering = 3;
            Assert.That(team.Deposit(new CatchItem(7002, 0, 0, 0, 1, 1, 2, 1)), Is.True);
            Assert.That(team.Feed(7002), Is.False);
            Assert.That(team.offering, Is.EqualTo(3));
            Assert.That(team.tank.Count, Is.EqualTo(1), "A rejected feed must preserve the resource item.");
            Assert.That(team.Process(7002), Is.True);
            Assert.That(team.scrap, Is.EqualTo(2));
            Assert.That(team.electronics, Is.EqualTo(1));
        }

        [Test]
        public void NamedCatchSurvivesBasketSaveAndHoistAlongsideLegacyCatch()
        {
            Assert.That(team.LoadCargo(new CatchItem(7511, 4, 0, 2, 1, 1, species: "Needlefish"), tuning), Is.True);
            Assert.That(team.LoadCargo(Fish(7512), tuning), Is.True);
            TeamSave.Save(savePath, team);
            var restored = TeamSave.Load(savePath);
            Assert.That(restored.cargo[0].species, Is.EqualTo("Needlefish"));
            Assert.That(restored.cargo[1].species, Is.Null.Or.Empty);
            Assert.That(restored.BeginHoist(tuning), Is.True);
            restored.TickHoist(1000f, tuning);
            Assert.That(restored.tank.Count, Is.EqualTo(2));
            Assert.That(restored.tank.Find(catchItem => catchItem.id == 7511).species, Is.EqualTo("Needlefish"));
            Assert.That(restored.cargo, Is.Empty);
        }

        [Test]
        public void FifteenCatchesOfOneSpeciesRetainEachIdentityAcrossTwoFullCrewDeliveries()
        {
            const int crewSize = 5, pocketsPerDiver = 3;
            tuning.cargoCapacity = 8;
            var catches = new CatchItem[crewSize * pocketsPerDiver];
            for (int diver = 0; diver < crewSize; diver++)
                for (int pocket = 0; pocket < pocketsPerDiver; pocket++)
                {
                    int index = diver * pocketsPerDiver + pocket;
                    catches[index] = new CatchItem(9000 + index, 3, 0, 3, 1, 1, species: "Lanternfish");
                }

            for (int i = 0; i < 8; i++) Assert.That(team.LoadCargo(catches[i], tuning), Is.True);
            Assert.That(team.LoadCargo(catches[8], tuning), Is.False);
            Assert.That(team.BeginHoist(tuning), Is.True);
            team.TickHoist(1000, tuning);
            for (int i = 8; i < catches.Length; i++) Assert.That(team.LoadCargo(catches[i], tuning), Is.True);
            Assert.That(team.BeginHoist(tuning), Is.True);
            team.TickHoist(1000, tuning);
            TeamSave.Save(savePath, team);
            var saved = TeamSave.Load(savePath);
            Assert.That(saved.tank.Count, Is.EqualTo(15));
            Assert.That(saved.lifetimeCatch, Is.EqualTo(45));
            foreach (var item in catches)
            {
                Assert.That(saved.tank.FindAll(entry => entry.id == item.id && entry.species == "Lanternfish").Count, Is.EqualTo(1));
                Assert.That(saved.LoadCargo(item, tuning), Is.False, "A previously delivered ID must stay spent.");
            }
        }

        [Test]
        public void MixedHoistDeliversAllResourceYieldsAndCountsLifetimeOnce()
        {
            tuning.raisingSeconds = 2; tuning.unloadingSeconds = 3; tuning.returningSeconds = 4;
            Assert.That(team.LoadCargo(new CatchItem(7003, 4, 1, 3, 1, 1, 2, 3), tuning), Is.True);
            Assert.That(team.LoadCargo(new CatchItem(7004, 0, 0, 0, 2, 2, 5, 2), tuning), Is.True);
            Assert.That(team.BeginHoist(tuning), Is.True);
            team.TickHoist(2, tuning);
            Assert.That(team.lifetimeCatch, Is.Zero, "Raising alone must not count undelivered resources.");
            team.TickHoist(3, tuning);
            Assert.That(team.tank.Count, Is.EqualTo(2));
            Assert.That(team.lifetimeCatch, Is.EqualTo(17));
            team.TickHoist(100, tuning);
            Assert.That(team.lifetimeCatch, Is.EqualTo(17), "Returning/idle ticks cannot deliver twice.");
            Assert.That(team.Process(7003), Is.True);
            Assert.That(team.Process(7004), Is.True);
            Assert.That(team.biomass, Is.EqualTo(4));
            Assert.That(team.chitin, Is.EqualTo(1));
            Assert.That(team.scrap, Is.EqualTo(7));
            Assert.That(team.electronics, Is.EqualTo(5));
            Assert.That(team.lifetimeCatch, Is.EqualTo(17));
        }

        [Test]
        public void CorruptOrUnsupportedSaveIsReportedAndOriginalIsPreserved()
        {
            TeamSave.Save(savePath, team);
            string corrupt = File.ReadAllText(savePath).Replace("RAID.TeamProgress", "Broken.TeamProgress");
            File.WriteAllText(savePath, corrupt);
            Assert.Throws<InvalidDataException>(() => TeamSave.Load(savePath));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(corrupt));
            team.schemaVersion = 999;
            Assert.Throws<InvalidOperationException>(() => TeamSave.Save(savePath, team));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(corrupt));
        }
    }
}
