using System;
using UnderwaterFishing.Cargo;

namespace UnderwaterFishing.Progression
{
    [Serializable] public struct ResourceAmount
    {
        public string Id;
        public int Amount;
        public ResourceAmount(string id, int amount) { Id = id; Amount = amount; }
    }
    [Serializable] public sealed class ResourceDefinition
    {
        public string Id, Name;
        // A new resource can combine existing cargo yields without changing stock or recipe code.
        public int PerBio, PerRare, PerScrap, PerElectronics;
        public long Yield(CargoYield value) => (long)value.Bio * PerBio + (long)value.Rare * PerRare
            + (long)value.Scrap * PerScrap + (long)value.Electronics * PerElectronics;
    }
    public enum CrewUpgrade : byte { Oxygen, Reel, Suit, Winch }
    public enum OfferingResult : byte { None, Success, Failure }
    [Serializable] public sealed class UpgradeRecipe
    {
        public CrewUpgrade Upgrade;
        public ResourceAmount[] Costs;
        public float CostGrowth = 1.6f;
        public int MaxLevel = 10;
    }
    [Serializable] public sealed class ProgressionSettings
    {
        public ResourceDefinition[] Resources = {
            new ResourceDefinition { Id = "bio", Name = "Биоматериал", PerBio = 1 },
            new ResourceDefinition { Id = "rare", Name = "Редкая органика", PerRare = 1 },
            new ResourceDefinition { Id = "scrap", Name = "Металлолом", PerScrap = 1 },
            new ResourceDefinition { Id = "electronics", Name = "Электроника", PerElectronics = 1 }
        };
        public UpgradeRecipe[] Upgrades = {
            new UpgradeRecipe { Upgrade = CrewUpgrade.Oxygen, Costs = new[] { new ResourceAmount("bio", 4), new ResourceAmount("scrap", 2) } },
            new UpgradeRecipe { Upgrade = CrewUpgrade.Reel, Costs = new[] { new ResourceAmount("scrap", 4), new ResourceAmount("electronics", 1) } },
            new UpgradeRecipe { Upgrade = CrewUpgrade.Suit, Costs = new[] { new ResourceAmount("bio", 4), new ResourceAmount("rare", 1) } },
            new UpgradeRecipe { Upgrade = CrewUpgrade.Winch, Costs = new[] { new ResourceAmount("scrap", 5), new ResourceAmount("electronics", 2) } }
        };
        public ResourceAmount[] SonarCost = { new ResourceAmount("bio", 1), new ResourceAmount("electronics", 1) };
        public int MaxSonarCharges = 9, InitialQuota = 12;
        public float QuotaGrowth = 1.4f, OfferingAllocationSeconds = 45, SonarRange = 30, SonarDuration = 8, SonarCooldown = 1;
        public float OxygenPerLevel = .25f, ReelPerLevel = .25f, SuitDepthPerLevel = 8, WinchPerLevel = .35f;
        public float TerminalDistance = 3.2f, CommandCooldown = .25f;
    }
    [Serializable] public struct UpgradeEffects
    {
        public float OxygenMultiplier, ReelMultiplier, SafeDepthBonus, WinchMultiplier;
        public static UpgradeEffects From(ProgressionSettings settings, Func<CrewUpgrade, int> level) => new UpgradeEffects {
            OxygenMultiplier = 1 + level(CrewUpgrade.Oxygen) * settings.OxygenPerLevel,
            ReelMultiplier = 1 + level(CrewUpgrade.Reel) * settings.ReelPerLevel,
            SafeDepthBonus = level(CrewUpgrade.Suit) * settings.SuitDepthPerLevel,
            WinchMultiplier = 1 + level(CrewUpgrade.Winch) * settings.WinchPerLevel
        };
    }
}
