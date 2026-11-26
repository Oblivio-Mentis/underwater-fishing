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
        public bool dispatchCommitted;
        public bool salvage;
        public int biomass, chitin, offering, cargoSize, depthTier, scrap, electronics;
        public bool IsSalvage => salvage;
        internal MaterialCost SalvageYield => new(0,0,scrap,electronics);
        public static CatchItem Salvage(long id,int tier,int scrap,int electronics) =>
            new CatchItem(id,0,0,0,1,tier,scrap,electronics){salvage=true};
        public CatchItem(long id, int biomass, int chitin, int offering, int cargoSize, int depthTier, int scrap=0, int electronics=0, string species=null)
        { this.id = id; this.biomass = biomass; this.chitin = chitin; this.offering = offering; this.cargoSize = cargoSize; this.depthTier = depthTier; this.scrap=scrap; this.electronics=electronics; this.species=species; }
        public bool IsValid => destination>=CatchDestination.Hold && destination<=CatchDestination.Feed &&
            id>0 && biomass>=0 && chitin>=0 && scrap>=0 && electronics>=0 && offering>=0 && cargoSize>0 && depthTier>0 &&
            (!dispatchCommitted || destination!=CatchDestination.Hold) &&
            (!salvage || string.IsNullOrEmpty(species) && biomass==0 && chitin==0 && offering==0 && destination==CatchDestination.Hold);
        public CatchItem Copy()
        {
            var copy=PublicCopy();
            copy.caughtBy=caughtBy==null?null:(string[])caughtBy.Clone();
            return copy;
        }
        public CatchItem PublicCopy() => new CatchItem(id, biomass, chitin, offering, cargoSize, depthTier,scrap,electronics,species)
            {destination=destination,dispatchCommitted=dispatchCommitted,salvage=salvage};
    }

    [Serializable]
    public struct HungerReport
    {
        public FeedingOutcome outcome;
        public int required, paid;
        public float deficit, mitigation;
        public bool Attack => outcome == FeedingOutcome.None;
    }

    /// <summary>Host-owned rules. Call from the server thread; serialized fields are save/snapshot data.</summary>
    [Serializable]
    public sealed partial class TeamProgress
    {
        public const int CurrentSchema = 6;
        public int schemaVersion = CurrentSchema;
        public int biomass, chitin, scrap, electronics;
        public long lifetimeCatch;
        public bool goshaMet,goshaTamed;
        public int expeditionDepth = 1;
        public int highestDepth => TeamRank(SkillLine.Hull)>0 ? 2 : 1;
        public int rationCyclesUntilReady;
        public float hungerRemaining = 180f;
        public List<CatchItem> tank = new List<CatchItem>();
        public List<CatchItem> cargo = new List<CatchItem>();
        public List<long> deliveredIds = new List<long>();
        public HoistPhase hoistPhase;
        public float hoistRemaining;
        public TeamProgress() { }
        public TeamProgress(ProgressionTuning tuning) { hungerRemaining = Math.Max(1f, tuning.hungerInterval); }
        public int CargoUsed { get { int used = 0; foreach (CatchItem fish in cargo) used = checked(used + fish.cargoSize); return used; } }

        public int Capacity(ProgressionTuning tuning) => checked(tuning.cargoCapacity + TeamValue(SkillLine.Cage));
        public int ReservoirCapacity(ProgressionTuning tuning=null) =>
            checked((tuning!=null ? tuning.reservoirCapacity : 24) + TeamValue(SkillLine.Reservoir));
        public bool CanVisitDepth(int depth) => depth >= 1 && depth <= highestDepth;

        public float HungerInterval(ProgressionTuning tuning) => Math.Max(1f, tuning.hungerInterval*(1+TeamFactor(SkillLine.Satiety)));

        public int StockLossLimit(ProgressionTuning tuning) => Math.Min(Math.Max(0,tuning.maximumStockLossCount),
            (int)Math.Floor(tank.Count * Math.Max(0f,Math.Min(1f,tuning.maximumStockLossFraction)) * (1-TeamFactor(SkillLine.CargoDefense))));

        public bool LoadCargo(CatchItem item, ProgressionTuning tuning)
        {
            if (item == null || !item.IsValid || hoistPhase != HoistPhase.Loading || deliveredIds.Contains(item.id) ||
                cargo.Exists(f => f.id == item.id) || (long)CargoUsed + item.cargoSize > Capacity(tuning)) return false;
            cargo.Add(item.Copy());
            return true;
        }

        int CargoFishCount
        {
            get
            {
                int count=0;
                foreach(var item in cargo) if(!item.IsSalvage) count++;
                return count;
            }
        }

        public bool FitsReservoir(ProgressionTuning tuning) =>
            (long)tank.Count+CargoFishCount<=ReservoirCapacity(tuning);

        public bool BeginHoist(ProgressionTuning tuning)
        {
            if (hoistPhase != HoistPhase.Loading || cargo.Count == 0 || !FitsReservoir(tuning) || !CanCreditCargo()) return false;
            hoistPhase = HoistPhase.Raising;
            hoistRemaining = HoistDuration(HoistPhase.Raising,tuning);
            return true;
        }

        public float HoistDuration(HoistPhase phase,ProgressionTuning tuning)
        {
            float interval=phase switch
            {
                HoistPhase.Raising=>tuning.raisingSeconds,
                HoistPhase.Unloading=>tuning.unloadingSeconds,
                HoistPhase.Returning=>tuning.returningSeconds*(1-TeamFactor(SkillLine.Industrial)),
                _=>0
            };
            return Math.Max(.01f, interval/(1+TeamFactor(SkillLine.Winch)));
        }

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
                    hoistRemaining = HoistDuration(HoistPhase.Unloading,tuning);
                }
                else if (hoistPhase == HoistPhase.Unloading)
                {
                    if(!TryDeliver(cargo,out recovered))
                    {
                        // A treasury grant may have filled the wallet after the lift departed.
                        // Keep the manifest on the lift and allow a fresh hoist after spending.
                        hoistPhase=HoistPhase.Loading;
                        hoistRemaining=0;
                        return default;
                    }
                    cargo.Clear();
                    hoistPhase = HoistPhase.Returning;
                    hoistRemaining = HoistDuration(HoistPhase.Returning,tuning);
                }
                else { hoistPhase = HoistPhase.Loading; hoistRemaining = 0f; }
            }
            if (hoistPhase != HoistPhase.Loading) hoistRemaining -= dt;
            return recovered;
        }

        /// <summary>Direct delivery for a server-authorized delivery point; cargo uses the timed hoist.</summary>
        public bool Deposit(CatchItem item, ProgressionTuning tuning=null)
        {
            if (item == null || !item.IsValid || deliveredIds.Contains(item.id) || cargo.Exists(f => f.id == item.id) ||
                (!item.IsSalvage && ReservoirRoom(tuning)==0)) return false;
            return TryDeliver(new[]{item},out _);
        }

        public bool CanCreditCargo() => QuoteDelivery(cargo,out _,out _);

        bool QuoteDelivery(IReadOnlyList<CatchItem> stock,out long total,out MaterialCost salvage)
        {
            total=lifetimeCatch;
            salvage=default;
            foreach(var item in stock)
            {
                long value=(long)item.biomass+item.chitin+item.scrap+item.electronics;
                if(value>long.MaxValue-total)return false;
                total+=value;
                if(!item.IsSalvage)continue;
                if(!salvage.CanAdd(item.SalvageYield))return false;
                salvage=salvage.Add(item.SalvageYield);
            }
            return (salvage.IsEmpty || teamRevision<int.MaxValue) && Treasury.CanAdd(salvage);
        }

        bool TryDeliver(IReadOnlyList<CatchItem> stock,out MaterialCost credited)
        {
            credited=default;
            if(!QuoteDelivery(stock,out long total,out var salvage))return false;
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
            credited=salvage;
            return true;
        }

        public bool Process(long id) => CreditProcessedCatch(id,null,out _);

        public bool Feed(long id, ProgressionTuning tuning=null)
        {
            int index = tank.FindIndex(item => item.id == id);
            if (index < 0 || tank[index].offering<=0) return false;
            int food=FoodValue(tank[index],tuning);
            if(food>int.MaxValue-offeringPoints)return false;
            int next=offeringPoints+food;
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
            bool ready=rationCyclesUntilReady==0;
            if(!ready)rationCyclesUntilReady--;
            if(ready && report.Attack && HasTeamLine(SkillLine.PreparedRation))
            {
                report.mitigation=TeamFactor(SkillLine.PreparedRation);
                rationCyclesUntilReady=2;
            }
            long retained=(long)Math.Max(0,offeringPoints-required)*TeamValue(SkillLine.Carryover)/100;
            offeringPoints=(int)Math.Min((long)Math.Max(0,tuning.maximumFoodCarry)*FoodScale,retained);
            hungerRemaining=HungerInterval(tuning);
            return report;
        }

        public int ApplyStockLoss(ProgressionTuning tuning, float severity = 1f)
        {
            if (float.IsNaN(severity) || float.IsInfinity(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            float fraction = Math.Max(0f, Math.Min(1f, tuning.maximumStockLossFraction)) * Math.Max(0f, Math.Min(1f, severity)) * (1-TeamFactor(SkillLine.CargoDefense));
            int count = Math.Min(Math.Max(0, tuning.maximumStockLossCount), (int)Math.Floor(tank.Count * fraction));
            if (count > 0) tank.RemoveRange(tank.Count - count, count);
            return count;
        }

        public void Validate()
        {
            if (schemaVersion != CurrentSchema) throw new InvalidOperationException("Unsupported team save schema: " + schemaVersion);
            if (biomass < 0 || chitin < 0 || scrap < 0 || electronics < 0 || lifetimeCatch < 0 || offeringPoints < 0 ||
                expeditionDepth < 1 || expeditionDepth > highestDepth || rationCyclesUntilReady < 0 || rationCyclesUntilReady > 2 ||
                !Enum.IsDefined(typeof(HoistPhase), hoistPhase) ||
                float.IsNaN(hoistRemaining) || float.IsInfinity(hoistRemaining) || hoistRemaining < 0 ||
                float.IsNaN(hungerRemaining) || float.IsInfinity(hungerRemaining) || hungerRemaining < 0 ||
                tank == null || cargo == null || deliveredIds == null)
                throw new InvalidOperationException("Invalid team progress data.");
            ValidateSkills();
            ValidateTechnologies();
            ValidateTechInventory();
            ValidateTechRigs();
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
