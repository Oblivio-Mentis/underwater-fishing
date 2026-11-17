using UnityEngine;

namespace UnderwaterFishing.Fishing.Creatures
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Fish Species")]
    public sealed class FishSpecies : ScriptableObject
    {
        public string DisplayName = "Reef fish";
        public bool IsLarge;
        [Min(0f)] public float SwimSpeed = 1.1f;
        [Min(0f)] public float FleeSpeed = 2.5f;
        [Min(0f)] public float StruggleSpeed = 2f;
        [Min(0f)] public float DetectionRadius = 5f;
        [Min(0f)] public float EscapeRadius = 9f;
        [Min(0.1f)] public float RoamRadius = 7f;
        [Min(0.01f)] public float Stamina = 12f;
        [Min(0f)] public float ReelExhaustionPerSecond = 3f;
        [Range(.1f, 1f)] public float ReelWorkExponent = .5f;
        [Min(0f)] public float StruggleRecoveryPerSecond = 0.5f;
        [Min(0.1f)] public float RopeMass = 1f;
        [Min(0f)] public float RopeDrag = .8f;
        [Min(1)] public int CargoSize = 1;
        [Min(0)] public int OfferingValue = 3;
        [Min(0)] public int BioYield = 2;
        [Min(0)] public int RareYield;
        public Vector3 BodyScale = new Vector3(0.5f, 0.4f, 1.2f);
        [Min(.5f)] public float CarryForwardDistance = 2.2f;
        public float CarryDownOffset = .5f;
        public float CarrySideOffset = .55f;

        [Header("Behaviour timing and feel")]
        [Min(0f)] public float IdleSeconds = 0.7f;
        [Min(0f)] public float AlertSeconds = 0.35f;
        [Min(0.01f)] public float RoamRetargetSeconds = 2.6f;
        [Min(0.01f)] public float StruggleRetargetSeconds = 0.9f;
        [Min(0.01f)] public float StrugglePulseSeconds = 2f;
        [Range(0f, 1f)] public float StrugglePulseDuty = 0.4f;
        [Range(0f, 1f)] public float LargeDiveStrength = 0.8f;
        [Min(0f)] public float StruggleTurnJitter = 0.7f;
        [Min(0f)] public float BurstSpeedMultiplier = 1.4f;
        [Min(0f)] public float RestSpeedMultiplier = 0.55f;
        [Range(0f, 1f)] public float TiredSpeedFraction = 0.35f;
        [Range(0f, 1f)] public float MinimumWorkTension = 0.12f;
        [Min(0f)] public float HookReactionSeconds = 0.15f;
        [Min(0f)] public float EscapeCooldownSeconds = 3f;
    }
}
