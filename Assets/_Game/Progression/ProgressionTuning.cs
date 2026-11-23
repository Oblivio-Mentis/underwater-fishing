using System;
using UnityEngine;

namespace Raid
{
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
        public bool IsEmpty => biomass==0 && chitin==0 && scrap==0 && electronics==0;
        public bool IsValid => biomass >= 0 && chitin >= 0 && scrap >= 0 && electronics >= 0;
    }

    [CreateAssetMenu(menuName = "RAID/Progression Tuning")]
    public sealed class ProgressionTuning : ScriptableObject
    {
        [Min(1)] public int cargoCapacity = 8;
        [Min(1)] public int reservoirCapacity = 24;
        [Min(.01f)] public float raisingSeconds = 8f;
        [Min(.01f)] public float unloadingSeconds = 3f;
        [Min(.01f)] public float returningSeconds = 6f;
        [Min(1)] public int foodRequired = 12;
        [Min(0)] public int foodPerDepth = 6;
        [Min(0)] public int maximumFoodCarry = 3;
        [Min(1f)] public float hungerInterval = 180f;
        [Range(0f, 1f)] public float maximumStockLossFraction = .35f;
        [Min(0)] public int maximumStockLossCount = 4;
        [Header("Personal active skills")]
        public string hydroBinding="<Keyboard>/b";
        [Min(.1f)] public float hydroSeconds=1f,hydroCooldown=12f;
        [Min(.1f)] public float swimStartAcceleration=22f, swimBrakingAcceleration=34f;
        [Min(.1f)] public float scanSeconds=3.5f,scanCooldown=5f;

        public SkillNode Skill(SkillId id) => SkillTreeDefinition.Get(id);
    }
}
