using System;
using UnityEngine;

namespace Raid
{
    public enum DefensePhase : byte { Idle, Warning, Encirclement, Assault, Resolution, Recovery }
    public enum FieldCondition : byte { Normal, UnderAttack, Damaged, Critical, Broken, Restoring }
    public enum DefenseSpecies : byte { Rammer, Remora, Thief, Abductor, Stalker }
    public enum AttackerPose : byte { Approach, Windup, Strike, Latch, Fight, Carry, Retreat, Stagger }

    [CreateAssetMenu(menuName="RAID/Оборона подлодки")]
    public sealed class DefenseTuning : ScriptableObject
    {
        [Serializable]
        public sealed class Species
        {
            public DefenseSpecies kind;
            [Min(1)] public int cost;
            [Min(1)] public int simultaneous;
            [Min(1)] public float health, speed, burstSpeed, shieldDamage, diverDamage;
            [Min(.1f)] public float windup, cooldown, fightSeconds;
            [Range(0,1)] public float counterChance;
            [Range(.5f,2)] public float silhouetteScale=1;
        }

        [Header("Shield")]
        [Min(1)] public float maximumIntegrity=100;
        [Range(0,1)] public float damaged=.60f, critical=.25f;
        [Min(.25f)] public float restorationSeconds=5, theftSeconds=17;
        [Range(0,.6f)] public float minorLossFraction=.10f, severeLossFraction=.35f;
        [Min(1)] public int lateLossCap=24;
        [Header("Escalation and passes")]
        [Min(.5f)] public float warningSeconds=6, encirclementSeconds=5;
        [Min(.1f)] public float visibilitySeconds=6.5f, retreatSeconds=4;
        [Min(1)] public float baseBudget=14, lateBudgetCap=96;
        [Range(0,2)] public float appetiteExponent=.54f, crewMultiplier=.34f;
        [Min(1)] public int baseActive=4, activePerExtraDiver=2, maximumActive=12, maximumGroup=3;
        [Min(.1f)] public float firstGroupGap=5.7f, lateGroupGap=2.9f, maximumAssaultSeconds=104;
        [Min(4)] public float arrivalDistance=21, sceneryClearance=.32f;
        [Range(0,1)] public float concealment=.85f;
        [Header("Captures")]
        [Min(1)] public float carrySeconds=8, inkSeconds=5;
        [Min(.25f)] public float inkRadius=4;
        [Min(.5f)] public float captiveCarrySpeed=2.5f, harpoonRangeMargin=1.4f;
        [Min(.1f)] public float rescuePullSeconds=.74f;
        [Range(.3f,1)] public float small=.85f;
        [Range(1,2)] public float large=1.20f;
        [Range(0,1)] public float titanChance=.085f;
        [Min(0)] public int salvageBiomass=1, salvageOffering=1;
        public Species[] species={
            new(){kind=DefenseSpecies.Rammer,cost=3,simultaneous=3,health=85,speed=5.8f,burstSpeed=12.5f,shieldDamage=10,diverDamage=28,windup=1.2f,cooldown=5.8f,fightSeconds=5.5f,counterChance=.48f},
            new(){kind=DefenseSpecies.Remora,cost=2,simultaneous=4,health=53,speed=4.6f,burstSpeed=6.2f,shieldDamage=1.5f,diverDamage=9,windup=.42f,cooldown=4.5f,fightSeconds=5.1f,counterChance=.15f,silhouetteScale=.92f},
            new(){kind=DefenseSpecies.Thief,cost=1,simultaneous=4,health=36,speed=7.7f,burstSpeed=10.2f,shieldDamage=1.5f,diverDamage=6,windup=.42f,cooldown=6.4f,fightSeconds=3.1f,counterChance=.08f,silhouetteScale=.75f},
            new(){kind=DefenseSpecies.Abductor,cost=3,simultaneous=2,health=91,speed=4.9f,burstSpeed=8.3f,shieldDamage=2.5f,diverDamage=19,windup=.95f,cooldown=6.7f,fightSeconds=6.2f,counterChance=.5f,silhouetteScale=1.13f},
            new(){kind=DefenseSpecies.Stalker,cost=2,simultaneous=3,health=64,speed=6.3f,burstSpeed=11,shieldDamage=4,diverDamage=23,windup=.7f,cooldown=6,fightSeconds=4.6f,counterChance=.42f}
        };

        public int Budget(float deficit,int required,int initial,int crew,float mitigation)
        {
            if(!(deficit>0) || float.IsInfinity(deficit))return 0;
            float appetite=Mathf.Pow(Mathf.Max(1f,(float)required/Mathf.Max(1,initial)),appetiteExponent);
            float players=1+(Mathf.Clamp(crew,1,4)-1)*crewMultiplier;
            float size=baseBudget*Mathf.Clamp01(deficit)*appetite*players*(1-Mathf.Clamp(mitigation,0,.85f));
            return Mathf.Clamp(Mathf.CeilToInt(size),1,Mathf.Max(1,Mathf.RoundToInt(lateBudgetCap)));
        }

        internal Species For(DefenseSpecies kind)
        {
            foreach(var entry in species)if(entry!=null && entry.kind==kind)return entry;
            return null;
        }
    }
}
