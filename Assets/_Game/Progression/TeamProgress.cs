using System;
using System.Collections.Generic;

namespace Raid
{
    public enum CatchDestination : byte { Hold, Process, Feed }

    [Serializable]
    public sealed class CatchItem
    {
        public long id;
        public string species;
        public string[] caughtBy;
        public CatchDestination destination;
        public bool salvage;
        public int biomass, chitin, offering, cargoSize, depthTier, scrap, electronics;
        public bool IsSalvage => salvage;
        internal bool IsLegacySalvage => string.IsNullOrEmpty(species) && biomass==0 && chitin==0 && offering==0 && (scrap>0 || electronics>0);
        internal MaterialCost SalvageYield => new(0,0,scrap,electronics);
        public static CatchItem Salvage(long id,int tier,int scrap,int electronics) =>
            new CatchItem(id,0,0,0,1,tier,scrap,electronics){salvage=true};
        public CatchItem(long id, int biomass, int chitin, int offering, int cargoSize, int depthTier, int scrap=0, int electronics=0, string species=null)
        { this.id = id; this.biomass = biomass; this.chitin = chitin; this.offering = offering; this.cargoSize = cargoSize; this.depthTier = depthTier; this.scrap=scrap; this.electronics=electronics; this.species=species; }
        public bool IsValid => destination>=CatchDestination.Hold && destination<=CatchDestination.Feed && id > 0 && biomass >= 0 && chitin >= 0 && scrap>=0 && electronics>=0 && offering >= 0 && cargoSize > 0 && depthTier > 0
            && (!salvage || string.IsNullOrEmpty(species) && biomass==0 && chitin==0 && offering==0 && destination==CatchDestination.Hold);
        public CatchItem Copy()
        {
            var copy=PublicCopy();
            copy.caughtBy=caughtBy==null?null:(string[])caughtBy.Clone();
            return copy;
        }
        public CatchItem PublicCopy() => new CatchItem(id, biomass, chitin, offering, cargoSize, depthTier,scrap,electronics,species)
            {destination=destination,salvage=salvage};
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
    public sealed partial class TeamProgress
    {
        public const int CurrentSchema = 3;
        public int schemaVersion = CurrentSchema;
        public int biomass, chitin, scrap, electronics;
        public long lifetimeCatch;
        public int reelLevel, finsLevel, tankLevel, hullLevel, winchLevel;
        public bool goshaMet,goshaTamed;
        public int highestDepth = 1;
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
        public int Capacity(ProgressionTuning tuning) => checked(tuning.cargoCapacity + legacyCargoCapacityBonus + Bonus(SkillId.K1,tuning) + Bonus(SkillId.K2,tuning) + Bonus(SkillId.K3,tuning) + Bonus(SkillId.K4,tuning));
        public bool CanVisitDepth(int depth) => depth >= 1 && depth <= highestDepth;

        public bool Buy(Upgrade upgrade, bool onBoard, ProgressionTuning tuning)
        {
            UpgradeRecipe recipe = tuning.Recipe(upgrade);
            if (teamRevision==int.MaxValue || upgrade<Upgrade.Hull || !onBoard || recipe == null || Level(upgrade) >= recipe.maximumLevel) return false;
            if (upgrade == Upgrade.Hull && lifetimeCatch < tuning.depthTwoLifetimeCatch) return false;
            MaterialCost cost = Cost(upgrade, tuning);
            if (biomass < cost.biomass || chitin < cost.chitin || scrap < cost.scrap || electronics < cost.electronics) return false;
            biomass -= cost.biomass; chitin -= cost.chitin; scrap -= cost.scrap; electronics -= cost.electronics;
            teamRevision++;
            switch (upgrade)
            {
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

        public MaterialCost TickHoist(float dt, ProgressionTuning tuning)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            MaterialCost recovered=default;
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
                    recovered=Deliver(cargo);
                    cargo.Clear();
                    hoistPhase = HoistPhase.Returning;
                    hoistRemaining = Duration(tuning.returningSeconds, tuning);
                }
                else { hoistPhase = HoistPhase.Loading; hoistRemaining = 0f; }
            }
            if (hoistPhase != HoistPhase.Loading) hoistRemaining -= dt;
            return recovered;
        }

        /// <summary>Direct delivery for a server-authorized delivery point; cargo uses the timed hoist.</summary>
        public bool Deposit(CatchItem item)
        {
            if (item == null || !item.IsValid || deliveredIds.Contains(item.id) || cargo.Exists(f => f.id == item.id)) return false;
            Deliver(new[]{item});
            return true;
        }

        private MaterialCost Deliver(IReadOnlyList<CatchItem> stock)
        {
            long total=lifetimeCatch;
            MaterialCost salvage=default;
            foreach(var item in stock)
            {
                total=checked(total+item.biomass+item.chitin+item.scrap+item.electronics);
                if(item.IsSalvage)salvage=salvage.Add(item.SalvageYield);
            }
            var balance=Treasury.Add(salvage);
            int revision=salvage.IsEmpty?teamRevision:checked(teamRevision+1);
            foreach(var item in stock)
            {
                deliveredIds.Add(item.id);
                if(!item.IsSalvage)tank.Add(item.Copy());
            }
            lifetimeCatch=total;
            Treasury=balance;
            teamRevision=revision;
            return salvage;
        }

        internal void RecoverLegacySalvage()
        {
            MaterialCost recovered=default;
            foreach(var item in tank)
                if(item.IsLegacySalvage)recovered=recovered.Add(item.SalvageYield);
            if(!recovered.IsEmpty)
            {
                Treasury=Treasury.Add(recovered);
                // These deliveries already contributed to the ledger and the depth unlock.
                tank.RemoveAll(item=>item.IsLegacySalvage);
            }
            foreach(var item in cargo)
                if(item.IsLegacySalvage)
                {
                    item.salvage=true;
                    item.destination=CatchDestination.Hold;
                }
        }

        public bool Process(long id) => CreditProcessedCatch(id,null,out _);

        public bool Feed(long id, ProgressionTuning tuning=null)
        {
            int index = tank.FindIndex(item => item.id == id);
            if (index < 0 || tank[index].offering<=0) return false;
            int next = checked(offeringPoints + FoodValue(tank[index],tuning));
            tank.RemoveAt(index);
            offeringPoints = next;
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
            int required=RequiredFood(tuning);
            HungerReport report = new HungerReport { required = required, paid = offeringPoints,
                deficit = Math.Max(0f, 1f - (float)offeringPoints / required),
                outcome = offeringPoints == 0 ? FeedingOutcome.None : offeringPoints < required ? FeedingOutcome.Underfed :
                    offeringPoints == required ? FeedingOutcome.Enough : FeedingOutcome.Overfed };
            offeringPoints = Math.Min(checked(Math.Max(0,tuning.maximumFoodCarry)*FoodScale), Math.Max(0,offeringPoints-required));
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
            if (biomass < 0 || chitin < 0 || scrap < 0 || electronics < 0 || lifetimeCatch < 0 || offeringPoints < 0 ||
                reelLevel < 0 || finsLevel < 0 || tankLevel < 0 || hullLevel < 0 || winchLevel < 0 || highestDepth < 1 ||
                (highestDepth >= 2 && hullLevel < 1) || !Enum.IsDefined(typeof(HoistPhase), hoistPhase) ||
                float.IsNaN(hoistRemaining) || float.IsInfinity(hoistRemaining) || hoistRemaining < 0 ||
                float.IsNaN(hungerRemaining) || float.IsInfinity(hungerRemaining) || hungerRemaining < 0 ||
                tank == null || cargo == null || deliveredIds == null)
                throw new InvalidOperationException("Invalid team progress data.");
            ValidateSkills();
            HashSet<long> delivered = new HashSet<long>();
            foreach (long id in deliveredIds) if (id <= 0 || !delivered.Add(id)) throw new InvalidOperationException("Invalid delivery ledger.");
            HashSet<long> stock = new HashSet<long>();
            foreach (CatchItem item in tank)
                if (item == null || !item.IsValid || item.IsSalvage || !delivered.Contains(item.id) || !stock.Add(item.id))
                    throw new InvalidOperationException("Invalid tank contents.");
            foreach (CatchItem item in cargo)
                if (item == null || !item.IsValid || delivered.Contains(item.id) || !stock.Add(item.id))
                    throw new InvalidOperationException("Invalid cargo contents.");
        }
    }
}
