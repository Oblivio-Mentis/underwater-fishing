using UnityEngine;

namespace UnderwaterFishing.Fishing.Tether
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Tether Settings")]
    public sealed class TetherSettings : ScriptableObject
    {
        [Min(0.1f)] public float MaxLength = 25f;
        [Min(0f)] public float InitialSlack = 1f;
        [Min(0f)] public float MinLength = 1.5f;
        [Min(0f)] public float ReelSpeed = 2f;
        [Min(0f)] public float Stiffness = 12f;
        [Min(0f)] public float Damping = 2f;
        [Min(0.01f)] public float BreakTension = 40f;
        [Min(0f)] public float BreakGrace = 0.65f;
        [Min(0f)] public float PlayerPullForce = 1f;
        [Min(0f)] public float FishPullForce = 1f;
        [Range(0f, 1f)] public float AssistanceReduction = 0.3f;
        [Min(0)] public int MaxAssistants = 2;
        [Min(0f)] public float HelperReelBonus = 0.15f;
        [Min(0.1f)] public float MaxAcceleration = 18f;
        [Min(0f)] public float MaxRangeStretch = 3f;
        [Range(0.05f, 1f)] public float MinimumAssistedPull = 0.25f;
    }
}
