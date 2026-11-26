using System;
using UnityEngine;

namespace Raid
{
    [Serializable]
    public sealed class IndustryCalibration
    {
        public float towSpeed, overloadSeconds, robotSpeed, airFlow, airRegeneration, regenerationDepth;
        public float signalPull, bubbleThickness, bubbleHeight, bubblePush, manualDuration, hoseDockDistance, transferReach, careCycles;
        public float magnetSpeed, maxMagnetLoad, metalWeight, crateWeight, airFillDistance;
        public int robotBaseCapacity;
        static IndustryCalibration loaded;
        internal static IndustryCalibration Settings => loaded ??= JsonUtility.FromJson<IndustryCalibration>(Resources.Load<TextAsset>("TechIndustry/calibration").text);
    }
}
