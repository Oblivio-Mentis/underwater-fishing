using System;
using System.Collections.Generic;

namespace Raid
{
    [Serializable]
    public sealed class CatchItem
    {
        public long id;
        public string species;
        public int biomass, chitin, offering, cargoSize, depthTier, scrap, electronics;
        public CatchItem(long id, int biomass, int chitin, int offering, int cargoSize, int depthTier, int scrap=0, int electronics=0, string species=null)
        { this.id = id; this.biomass = biomass; this.chitin = chitin; this.offering = offering; this.cargoSize = cargoSize; this.depthTier = depthTier; this.scrap=scrap; this.electronics=electronics; this.species=species; }
        public bool IsValid => id > 0 && biomass >= 0 && chitin >= 0 && scrap>=0 && electronics>=0 && offering >= 0 && cargoSize > 0 && depthTier > 0;
        public CatchItem Copy() => new CatchItem(id, biomass, chitin, offering, cargoSize, depthTier,scrap,electronics,species);
    }

    [Serializable]
    public struct HungerReport
    {
        public FeedingOutcome outcome;
        public int required, paid;
        public float deficit;
        public bool Attack => outcome == FeedingOutcome.None;
    }

    /// <summary>Host-owned rules. Call from the server thread; serialized fields are save/snapshot data.</summary>
    [Serializable]
    public sealed class TeamProgress
    {
        public const int CurrentSchema = 1;
        public int schemaVersion = CurrentSchema;
        public int biomass, chitin, scrap, electronics;
        public long lifetimeCatch;
        public int reelLevel, finsLevel, tankLevel, hullLevel, winchLevel;
        public bool goshaMet,goshaTamed;
        public int highestDepth = 1;
        public int offering;
        public float hungerRemaining = 180f;
        public List<CatchItem> tank = new List<CatchItem>();
        public List<CatchItem> cargo = new List<CatchItem>();
        public List<long> deliveredIds = new List<long>();
        public HoistPhase hoistPhase;
        public float hoistRemaining;
        public TeamProgress() { }
        public TeamProgress(ProgressionTuning tuning) { hungerRemaining = Math.Max(1f, tuning.hungerInterval); }
        public int CargoUsed { get { int used = 0; foreach (CatchItem fish in cargo) used = checked(used + fish.cargoSize); return used; } }

        public int Level(Upgrade upgrade)
        {
            switch (upgrade)
            {
                case Upgrade.Reel: return reelLevel;
                case Upgrade.Fins: return finsLevel;
                case Upgrade.Tank: return tankLevel;
                case Upgrade.Hull: return hullLevel;
                case Upgrade.Winch: return winchLevel;
                default: throw new ArgumentOutOfRangeException(nameof(upgrade));
            }
        }

        public MaterialCost Cost(Upgrade upgrade, ProgressionTuning tuning)
        {
            UpgradeRecipe recipe = tuning.Recipe(upgrade);
            if (recipe == null || !recipe.initialCost.IsValid || !recipe.additionalPerLevel.IsValid || recipe.costMultiplier < 1f)
                throw new ArgumentException("Invalid upgrade recipe: " + upgrade);
            double scale = Math.Pow(recipe.costMultiplier, Level(upgrade));
            int level=Level(upgrade);
            return new MaterialCost(checked(Price(recipe.initialCost.biomass, scale)+level*recipe.additionalPerLevel.biomass),
                checked(Price(recipe.initialCost.chitin, scale)+level*recipe.additionalPerLevel.chitin),
                checked(Price(recipe.initialCost.scrap, scale)+level*recipe.additionalPerLevel.scrap),
                checked(Price(recipe.initialCost.electronics, scale)+level*recipe.additionalPerLevel.electronics));
        }

        private static int Price(int value, double scale) => checked((int)Math.Ceiling(Math.Round(value * scale, 6)));
        public int Capacity(ProgressionTuning tuning) => checked(tuning.cargoCapacity + winchLevel * tuning.cargoCapacityPerWinch);
        public bool CanVisitDepth(int depth) => depth >= 1 && depth <= highestDepth;

        public bool Buy(Upgrade upgrade, bool onBoard, ProgressionTuning tuning)
        {
            UpgradeRecipe recipe = tuning.Recipe(upgrade);
            if (!onBoard || recipe == null || Level(upgrade) >= recipe.maximumLevel) return false;
            if (upgrade == Upgrade.Hull && lifetimeCatch < tuning.depthTwoLifetimeCatch) return false;
            MaterialCost cost = Cost(upgrade, tuning);
            if (biomass < cost.biomass || chitin < cost.chitin || scrap < cost.scrap || electronics < cost.electronics) return false;
            biomass -= cost.biomass; chitin -= cost.chitin; scrap -= cost.scrap; electronics -= cost.electronics;
            switch (upgrade)
            {
                case Upgrade.Reel: ++reelLevel; break;
                case Upgrade.Fins: ++finsLevel; break;
                case Upgrade.Tank: ++tankLevel; break;
                case Upgrade.Hull: ++hullLevel; highestDepth = Math.Max(highestDepth, 2); break;
                case Upgrade.Winch: ++winchLevel; break;
            }
            return true;
        }

        public bool LoadCargo(CatchItem item, ProgressionTuning tuning)
        {
            if (item == null || !item.IsValid || hoistPhase != HoistPhase.Loading || deliveredIds.Contains(item.id) ||
                cargo.Exists(f => f.id == item.id) || (long)CargoUsed + item.cargoSize > Capacity(tuning)) return false;
            cargo.Add(item.Copy());
            return true;
        }

        public bool BeginHoist(ProgressionTuning tuning)
        {
            if (hoistPhase != HoistPhase.Loading || cargo.Count == 0) return false;
            hoistPhase = HoistPhase.Raising;
            hoistRemaining = Duration(tuning.raisingSeconds, tuning);
            return true;
        }

        private float Duration(float seconds, ProgressionTuning tuning) =>
            Math.Max(.01f, seconds / Math.Max(1f, 1f + winchLevel * tuning.winchSpeedPerLevel));

        public void TickHoist(float dt, ProgressionTuning tuning)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            while (hoistPhase != HoistPhase.Loading && dt >= hoistRemaining)
            {
                dt -= hoistRemaining;
                if (hoistPhase == HoistPhase.Raising)
                {
                    hoistPhase = HoistPhase.Unloading;
                    hoistRemaining = Duration(tuning.unloadingSeconds, tuning);
                }
                else if (hoistPhase == HoistPhase.Unloading)
                {
                    foreach (CatchItem item in cargo) Deliver(item);
                    cargo.Clear();
                    hoistPhase = HoistPhase.Returning;
                    hoistRemaining = Duration(tuning.returningSeconds, tuning);
                }
                else { hoistPhase = HoistPhase.Loading; hoistRemaining = 0f; }
            }
            if (hoistPhase != HoistPhase.Loading) hoistRemaining -= dt;
        }

        /// <summary>Direct delivery for a server-authorized delivery point; cargo uses the timed hoist.</summary>
        public bool Deposit(CatchItem item)
        {
            if (item == null || !item.IsValid || deliveredIds.Contains(item.id) || cargo.Exists(f => f.id == item.id)) return false;
            Deliver(item);
            return true;
        }

        private void Deliver(CatchItem item)
        {
            long total = checked(lifetimeCatch + item.biomass + item.chitin + item.scrap + item.electronics);
            deliveredIds.Add(item.id);
            tank.Add(item.Copy());
            lifetimeCatch = total;
        }

        public bool Process(long id)
        {
            int index = tank.FindIndex(item => item.id == id);
            if (index < 0) return false;
            CatchItem fish = tank[index];
            int nextBiomass = checked(biomass + fish.biomass), nextChitin = checked(chitin + fish.chitin);
            int nextScrap=checked(scrap+fish.scrap), nextElectronics=checked(electronics+fish.electronics);
            tank.RemoveAt(index);
            biomass = nextBiomass; chitin = nextChitin;
            scrap=nextScrap; electronics=nextElectronics;
            return true;
        }

        public bool Feed(long id)
        {
            int index = tank.FindIndex(item => item.id == id);
            if (index < 0 || tank[index].offering<=0) return false;
            int next = checked(offering + tank[index].offering);
            tank.RemoveAt(index);
            offering = next;
            return true;
        }

        public bool TickHunger(float dt, ProgressionTuning tuning)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            hungerRemaining = Math.Max(0f, hungerRemaining - dt);
            return hungerRemaining <= 0f;
        }

        public HungerReport EvaluateHunger(ProgressionTuning tuning)
        {
            int required = Math.Max(1, checked(tuning.foodRequired + (highestDepth - 1) * tuning.foodPerDepth));
            HungerReport report = new HungerReport { required = required, paid = offering,
                deficit = Math.Max(0f, 1f - (float)offering / required),
                outcome = offering == 0 ? FeedingOutcome.None : offering < required ? FeedingOutcome.Underfed :
                    offering == required ? FeedingOutcome.Enough : FeedingOutcome.Overfed };
            offering = Math.Min(Math.Max(0, tuning.maximumFoodCarry), Math.Max(0, offering - required));
            hungerRemaining = Math.Max(1f, tuning.hungerInterval);
            return report;
        }

        public int ApplyStockLoss(ProgressionTuning tuning, float severity = 1f)
        {
            if (float.IsNaN(severity) || float.IsInfinity(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            float fraction = Math.Max(0f, Math.Min(1f, tuning.maximumStockLossFraction)) * Math.Max(0f, Math.Min(1f, severity));
            int count = Math.Min(Math.Max(0, tuning.maximumStockLossCount), (int)Math.Floor(tank.Count * fraction));
            if (count > 0) tank.RemoveRange(tank.Count - count, count);
            return count;
        }

        public void Validate()
        {
            if (schemaVersion != CurrentSchema) throw new InvalidOperationException("Unsupported team save schema: " + schemaVersion);
            if (biomass < 0 || chitin < 0 || scrap < 0 || electronics < 0 || lifetimeCatch < 0 || offering < 0 ||
                reelLevel < 0 || finsLevel < 0 || tankLevel < 0 || hullLevel < 0 || winchLevel < 0 || highestDepth < 1 ||
                (highestDepth >= 2 && hullLevel < 1) || !Enum.IsDefined(typeof(HoistPhase), hoistPhase) ||
                float.IsNaN(hoistRemaining) || float.IsInfinity(hoistRemaining) || hoistRemaining < 0 ||
                float.IsNaN(hungerRemaining) || float.IsInfinity(hungerRemaining) || hungerRemaining < 0 ||
                tank == null || cargo == null || deliveredIds == null)
                throw new InvalidOperationException("Invalid team progress data.");
            HashSet<long> delivered = new HashSet<long>();
            foreach (long id in deliveredIds) if (id <= 0 || !delivered.Add(id)) throw new InvalidOperationException("Invalid delivery ledger.");
            HashSet<long> stock = new HashSet<long>();
            foreach (CatchItem item in tank)
                if (item == null || !item.IsValid || !delivered.Contains(item.id) || !stock.Add(item.id))
                    throw new InvalidOperationException("Invalid tank contents.");
            foreach (CatchItem item in cargo)
                if (item == null || !item.IsValid || delivered.Contains(item.id) || !stock.Add(item.id))
                    throw new InvalidOperationException("Invalid cargo contents.");
        }
    }
}
