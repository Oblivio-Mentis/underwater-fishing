using System;
using UnityEngine;

namespace Raid
{
    enum SkillUnit
    {
        BonusPercent, ReductionPercent, Metres, Seconds, ExtraSlots, RangeMetres,
        // A discrete ability: the rank itself is the value, the note names what it unlocks.
        Ability
    }

    [Serializable]
    struct SkillRequirement
    {
        public int atRank;
        public SkillLine line;
        public int rank;
    }

    [Serializable]
    struct SkillRankTuning
    {
        public int magnitude;
        [TextArea] public string note;
    }

    [Serializable]
    sealed class SkillLineTuning
    {
        public SkillLine line;
        public string key;
        public string title;
        public string effect;
        public bool team;
        public SkillUnit unit;
        public SkillRankTuning[] ranks;
        public MaterialCost firstCost;
        [Range(1, 100)] public int growthPercent = 40;
        [Range(0, 40)] public int stageGrowthPercent = 9;
        public MaterialCost extraPerStage;
        public SkillRequirement[] milestones;
    }

    [CreateAssetMenu(menuName = "RAID/Radial Progression Catalog")]
    public sealed class SkillCatalog : ScriptableObject
    {
        [SerializeField] SkillLineTuning[] lines;
        internal SkillLineTuning[] Lines => lines;
    }
}
