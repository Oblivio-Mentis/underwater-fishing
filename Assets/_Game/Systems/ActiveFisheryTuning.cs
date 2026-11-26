using System;
using UnityEngine;

namespace Raid
{
    [Serializable]
    internal sealed class CageSpecies
    {
        public string id;
        public bool deepOnly, allowAuto;
        public int minSize, biomass, chitin, offering, scrap, electronics, cargoSize;
        public CatchItem Catch(long idValue,int depth) =>
            new CatchItem(idValue, biomass, chitin, offering, cargoSize, depth, scrap, electronics, id);
    }

    [Serializable]
    internal sealed class ActiveFisheryTuning
    {
        public float chargeThrowSpeed, chargeDrag, chargeDrop, chargeDamage, chargeForce, chargeYield, chargeImpactRadius;
        public float chargeCloseRadius, chargeCloseMultiplier, chargeCorpseSpeed;
        public int chargeCap;
        public float trapPredatorLoss, trapPredatorDistance, trapIdleYield, trapActiveDepthBonus;
        public float vacuumReachCone, vacuumInlet, vacuumPull, vacuumPush, vacuumJamTime;
        public float netMinimumSpan, netCloseDistance, netThickness, netDamage;
        public float cageTetherHeight, cageTetherSpeed, cageSafeLeash, cageLureRadius, cageLurePulse;
        public float cageLatchCooldown, cageEscapeSeconds, cageStrugglePeriod, cageRepair, cageNearbyDamage;
        public CageSpecies[] monsters;
        static ActiveFisheryTuning loaded;
        public static ActiveFisheryTuning Get => loaded ??= JsonUtility.FromJson<ActiveFisheryTuning>(
            Resources.Load<TextAsset>("IndustrialFishery/active").text);
    }
}
