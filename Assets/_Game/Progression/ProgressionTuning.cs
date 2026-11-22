using System;
using UnityEngine;

namespace Raid
{
    public enum Upgrade { Reel, Fins, Tank, Hull, Winch }
    public enum HoistPhase { Loading, Raising, Unloading, Returning }
    public enum FeedingOutcome { None, Underfed, Enough, Overfed }

    [Serializable]
    public struct MaterialCost
    {
        public int biomass, chitin, scrap, electronics;
        public MaterialCost(int biomass, int chitin = 0, int scrap = 0, int electronics = 0)
        { this.biomass = biomass; this.chitin = chitin; this.scrap = scrap; this.electronics = electronics; }
        public bool Covers(MaterialCost cost) => cost.IsValid && biomass>=cost.biomass && chitin>=cost.chitin &&
            scrap>=cost.scrap && electronics>=cost.electronics;
        public MaterialCost Add(MaterialCost other) => new(checked(biomass+other.biomass),checked(chitin+other.chitin),
            checked(scrap+other.scrap),checked(electronics+other.electronics));
        public MaterialCost Subtract(MaterialCost other) => new(biomass-other.biomass,chitin-other.chitin,
            scrap-other.scrap,electronics-other.electronics);
        public MaterialCost Share(int count,int index) => new(Portion(biomass,count,index),Portion(chitin,count,index),
            Portion(scrap,count,index),Portion(electronics,count,index));
        static int Portion(int amount,int count,int index) => amount/count + (index<amount%count ? 1 : 0);
        public bool IsEmpty => biomass==0 && chitin==0 && scrap==0 && electronics==0;
        public bool IsValid => biomass >= 0 && chitin >= 0 && scrap >= 0 && electronics >= 0;
    }

    [Serializable]
    public sealed class UpgradeRecipe
    {
        public Upgrade upgrade;
        public MaterialCost initialCost;
        public MaterialCost additionalPerLevel;
        [Min(1f)] public float costMultiplier = 1.7f;
        [Min(1)] public int maximumLevel = 3;
        public UpgradeRecipe(Upgrade upgrade, MaterialCost cost, int maximumLevel = 3)
        { this.upgrade = upgrade; initialCost = cost; this.maximumLevel = maximumLevel; }
    }

    [CreateAssetMenu(menuName = "RAID/Progression Tuning")]
    public sealed class ProgressionTuning : ScriptableObject
    {
        [Min(1)] public int cargoCapacity = 8;
        [Min(0)] public int cargoCapacityPerWinch = 4;
        [Min(.01f)] public float raisingSeconds = 8f;
        [Min(.01f)] public float unloadingSeconds = 3f;
        [Min(.01f)] public float returningSeconds = 6f;
        [Min(0f)] public float winchSpeedPerLevel = .35f;
        [Min(0)] public int depthTwoLifetimeCatch = 24;
        [Min(1)] public int foodRequired = 12;
        [Min(0)] public int foodPerDepth = 6;
        [Min(0)] public int maximumFoodCarry = 3;
        [Min(1f)] public float hungerInterval = 180f;
        [Range(0f, 1f)] public float maximumStockLossFraction = .35f;
        [Min(0)] public int maximumStockLossCount = 4;
        [Min(0f)] public float reelPowerPerLevel = .3f;
        [Min(0f)] public float finsSpeedPerLevel = .2f;
        [Min(0f)] public float oxygenPerTankLevel = .4f;
        public UpgradeRecipe[] upgrades =
        {
            new UpgradeRecipe(Upgrade.Reel, new MaterialCost(6)),
            new UpgradeRecipe(Upgrade.Fins, new MaterialCost(8)),
            new UpgradeRecipe(Upgrade.Tank, new MaterialCost(10)),
            // The first hull requires only Depth 1 resources: no Depth 2 income deadlock.
            new UpgradeRecipe(Upgrade.Hull, new MaterialCost(18), 1),
            new UpgradeRecipe(Upgrade.Winch, new MaterialCost(12))
        };

        [Header("Personal active skills")]
        public string hydroBinding="<Keyboard>/b";
        [Min(.1f)] public float hydroSeconds=1f,hydroCooldown=12f;
        [Min(.1f)] public float scanSeconds=3.5f,scanCooldown=5f;

        [Header("Skill tree overrides")]
        public SkillNode[] skills = Array.Empty<SkillNode>();

        public SkillNode Skill(SkillId id)
        {
            if(skills!=null)
                foreach(var node in skills)
                    if(node!=null && node.Id==id && node.Cost.IsValid && node.Magnitude>=0)return node;
            return SkillTreeDefinition.Get(id);
        }

        public UpgradeRecipe Recipe(Upgrade upgrade)
        {
            if (upgrades != null)
                foreach (UpgradeRecipe recipe in upgrades)
                    if (recipe != null && recipe.upgrade == upgrade) return recipe;
            return null;
        }
    }
}
