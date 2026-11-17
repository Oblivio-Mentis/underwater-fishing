using System;
using System.Collections.Generic;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Expedition;

namespace UnderwaterFishing.Progression
{
    // Implementations must be synchronous and owned by the same server thread as the model.
    public interface IProtectedCargo
    {
        IReadOnlyList<CargoItem> Hold { get; }
        bool TryConsume(ulong id, CargoDisposition disposition, out CargoItem consumed);
        bool TryReserve(ulong id, bool reserve);
    }

    public sealed class ProgressionModel
    {
        private readonly ProgressionSettings settings;
        private readonly Dictionary<string, int> stocks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<CrewUpgrade, int> levels = new Dictionary<CrewUpgrade, int>();
        private readonly Dictionary<int, int> charges = new Dictionary<int, int>();
        public IReadOnlyDictionary<string, int> Stocks => stocks;
        public IReadOnlyDictionary<int, int> SonarCharges => charges;
        public ExpeditionPhase Phase { get; private set; } = ExpeditionPhase.Base;
        public int Quota { get; private set; }
        public int SuccessfulOfferings { get; private set; }
        public int LastOfferingValue { get; private set; }
        public OfferingResult LastOffering { get; private set; }
        public bool OfferingResolved { get; private set; }
        public float OfferingSeconds { get; private set; }
        public uint Revision { get; private set; }
        public event Action Changed;
        public UpgradeEffects Effects => UpgradeEffects.From(settings, Level);

        public ProgressionModel(ProgressionSettings configuration)
        {
            settings = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (settings.Resources == null || settings.Resources.Length == 0) throw new ArgumentException("Resource definitions are required.");
            foreach (var resource in settings.Resources)
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || stocks.ContainsKey(resource.Id)
                    || resource.PerBio < 0 || resource.PerRare < 0 || resource.PerScrap < 0 || resource.PerElectronics < 0)
                    throw new ArgumentException("Resource IDs must be unique and yields nonnegative.");
                stocks.Add(resource.Id, 0);
            }
            ResetRun();
        }
        public int Stock(string id) => id != null && stocks.TryGetValue(id, out int count) ? count : 0;
        public int Level(CrewUpgrade upgrade) => levels.TryGetValue(upgrade, out int level) ? level : 0;
        public int Charges(int ownerId) => charges.TryGetValue(ownerId, out int count) ? count : 0;
        public void SetPhase(ExpeditionPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            if (phase == ExpeditionPhase.Expedition) OfferingResolved = false;
            if (phase == ExpeditionPhase.Offering && !OfferingResolved)
                OfferingSeconds = Finite(settings.OfferingAllocationSeconds) ? Math.Max(1, settings.OfferingAllocationSeconds) : 45;
            Touch();
        }
        public bool StepOffering(float dt)
        {
            if (Phase != ExpeditionPhase.Offering || OfferingResolved || !Finite(dt) || dt <= 0) return false;
            OfferingSeconds = Math.Max(0, OfferingSeconds - dt);
            return OfferingSeconds <= 0;
        }
        public bool TryProcess(IProtectedCargo cargo, ulong id)
        {
            if (Phase != ExpeditionPhase.Base || cargo == null) return false;
            CargoItem selected = default;
            foreach (var item in cargo.Hold) if (item.Id == id) { selected = item; break; }
            if (!selected.IsValid || selected.Reserved) return false;
            var yield = new Dictionary<string, int>();
            foreach (var resource in settings.Resources)
            {
                long amount = resource.Yield(selected.Yield);
                if (amount < 0 || amount > int.MaxValue - Stock(resource.Id)) return false;
                yield.Add(resource.Id, (int)amount);
            }
            if (!cargo.TryConsume(id, CargoDisposition.Process, out var consumed)) return false;
            if (consumed.Id != id) throw new InvalidOperationException("Cargo adapter consumed a different item.");
            foreach (var pair in yield) stocks[pair.Key] += pair.Value;
            Touch(); return true;
        }
        public bool TryReserve(IProtectedCargo cargo, ulong id, bool reserve)
        {
            if ((Phase != ExpeditionPhase.Base && Phase != ExpeditionPhase.Offering) || OfferingResolved && Phase == ExpeditionPhase.Offering
                || cargo == null || !cargo.TryReserve(id, reserve)) return false;
            Touch(); return true;
        }
        public ResourceAmount[] UpgradeCost(CrewUpgrade upgrade, int level)
        {
            if (settings.Upgrades == null || level < 0) return null;
            foreach (var recipe in settings.Upgrades)
            {
                if (recipe == null || recipe.Upgrade != upgrade) continue;
                if (level >= Math.Max(0, recipe.MaxLevel) || !Finite(recipe.CostGrowth) || recipe.CostGrowth <= 1) return null;
                double multiplier = Math.Pow(recipe.CostGrowth, level);
                if (recipe.Costs == null || recipe.Costs.Length == 0) return null;
                var result = new ResourceAmount[recipe.Costs.Length];
                for (int i = 0; i < result.Length; i++)
                {
                    double amount = Math.Ceiling(recipe.Costs[i].Amount * multiplier);
                    if (amount < 0 || amount > int.MaxValue || double.IsNaN(amount)) return null;
                    result[i] = new ResourceAmount(recipe.Costs[i].Id, (int)amount);
                }
                return result;
            }
            return null;
        }
        public bool TryBuy(CrewUpgrade upgrade, int expectedLevel, uint expectedRevision)
        {
            if (Phase != ExpeditionPhase.Base || expectedRevision != Revision || expectedLevel != Level(upgrade)
                || !TrySpend(UpgradeCost(upgrade, expectedLevel))) return false;
            levels[upgrade] = expectedLevel + 1; Touch(); return true;
        }
        public bool TryCraftSonar(int ownerId, int expectedCharges, uint expectedRevision)
        {
            if (Phase != ExpeditionPhase.Base || ownerId < 0 || expectedRevision != Revision
                || expectedCharges != Charges(ownerId) || expectedCharges >= settings.MaxSonarCharges
                || !TrySpend(settings.SonarCost)) return false;
            charges[ownerId] = expectedCharges + 1; Touch(); return true;
        }
        public bool TryUseSonar(int ownerId, int expectedCharges)
        {
            if (Phase != ExpeditionPhase.Expedition || ownerId < 0 || expectedCharges <= 0 || Charges(ownerId) != expectedCharges) return false;
            charges[ownerId] = expectedCharges - 1; Touch(); return true;
        }
        public void RemoveCrew(int ownerId) { if (charges.Remove(ownerId)) Touch(); }
        private bool TrySpend(ResourceAmount[] cost)
        {
            if (cost == null || cost.Length == 0) return false;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            long sum = 0;
            foreach (var part in cost)
            {
                if (part.Id == null || !stocks.ContainsKey(part.Id) || part.Amount < 0) return false;
                totals.TryGetValue(part.Id, out long current); totals[part.Id] = current + part.Amount; sum += part.Amount;
            }
            if (sum == 0) return false;
            foreach (var pair in totals) if (pair.Value > Stock(pair.Key)) return false;
            foreach (var pair in totals) stocks[pair.Key] -= (int)pair.Value;
            return true;
        }
        public static int ReservedValue(IReadOnlyList<CargoItem> hold)
        {
            long total = 0;
            if (hold != null) foreach (var item in hold) if (item.IsFish && item.Reserved) total += Math.Max(0, item.OfferingValue);
            return (int)Math.Min(int.MaxValue, total);
        }
        public bool ResolveOffering(IProtectedCargo cargo, out bool success)
        {
            success = false;
            if (Phase != ExpeditionPhase.Offering || OfferingResolved || cargo == null) return false;
            OfferingResolved = true; OfferingSeconds = 0; LastOfferingValue = 0;
            if (ReservedValue(cargo.Hold) < Quota)
            { LastOffering = OfferingResult.Failure; Touch(); return true; }
            var selected = new List<ulong>();
            long value = 0;
            foreach (var item in cargo.Hold)
            {
                if (!item.IsFish || !item.Reserved || item.OfferingValue <= 0) continue;
                selected.Add(item.Id); value += item.OfferingValue;
                if (value >= Quota) break;
            }
            foreach (ulong id in selected)
                if (!cargo.TryConsume(id, CargoDisposition.Offering, out _))
                    throw new InvalidOperationException("Protected cargo changed inside a synchronous offering transaction.");
            LastOfferingValue = (int)Math.Min(int.MaxValue, value);
            SuccessfulOfferings++; LastOffering = OfferingResult.Success;
            double next = Math.Ceiling(Quota * (double)(Finite(settings.QuotaGrowth) && settings.QuotaGrowth > 1 ? settings.QuotaGrowth : 1.4f));
            Quota = (int)Math.Min(int.MaxValue, Math.Max((long)Quota + 1, next));
            success = true; Touch(); return true;
        }
        public void ResetRun()
        {
            foreach (var resource in settings.Resources) stocks[resource.Id] = 0;
            levels.Clear(); charges.Clear(); Quota = Math.Max(1, settings.InitialQuota);
            SuccessfulOfferings = 0; LastOfferingValue = 0; LastOffering = OfferingResult.None;
            OfferingResolved = false; OfferingSeconds = 0; Phase = ExpeditionPhase.Base; Touch();
        }
        private void Touch() { Revision++; Changed?.Invoke(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
