using UnderwaterFishing.Diving;
using UnderwaterFishing.Fishing.Tether;
using UnityEngine;

namespace UnderwaterFishing.Data
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        public DivingSettings Diving = new DivingSettings();
        public float SwimSpeed = 4f;
        public float SprintMultiplier = 1.5f;
        public float Acceleration = 4f;
        public float Braking = 3f;
        public float Buoyancy = 0.03f;
        public float ExternalDrag = 1.2f;
        public float LookSensitivity = 0.1f;
        public float InteractionDistance = 3f;
        public float ExpeditionSeconds = 420f;
        public bool DevelopmentMode;
        public TetherSettings Tether;
        public float HarpoonCooldown = .7f;
        public float FishPickupDistance = 2.5f;
        public float CarrySpeedSmall = .9f;
        public float CarrySpeedLarge = .65f;
        public float OxygenRescueSeconds = 2f;
        public float DebrisRescueSeconds = 3f;
    }
}
