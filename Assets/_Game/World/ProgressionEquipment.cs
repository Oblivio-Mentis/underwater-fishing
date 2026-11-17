using System.Collections.Generic;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Player;
using UnderwaterFishing.Progression;
using UnderwaterFishing.Progression.Network;
using UnityEngine;

namespace UnderwaterFishing.World
{
    /// <summary>Applies shared purchases to each authority-owned runtime equipment copy.</summary>
    public sealed class ProgressionEquipment : MonoBehaviour
    {
        private ProgressionWorld progression;
        private readonly HashSet<NetworkDiver> equipped = new();
        private void Start()
        {
            progression = GetComponent<ProgressionWorld>();
            progression.ApplyForNextDive += Apply;
        }
        private void Apply(UpgradeEffects effects)
        {
            foreach (var diver in NetworkDiver.All)
            {
                if (!diver.IsServerStarted) continue;
                diver.ApplyGear(effects.OxygenMultiplier, effects.ReelMultiplier, effects.SafeDepthBonus);
                equipped.Add(diver);
            }
        }
        private void FixedUpdate()
        {
            equipped.RemoveWhere(diver => diver == null || !diver.IsServerStarted);
            var authority = NetworkProgression.Instance;
            if (authority == null || !authority.IsServerStarted || NetworkExpedition.Instance == null || !NetworkExpedition.Instance.GameplayEnabled) return;
            var effects = authority.Effects;
            foreach (var diver in NetworkDiver.All)
            {
                if (!diver.IsServerStarted || equipped.Contains(diver)) continue;
                diver.ApplyGear(effects.OxygenMultiplier, effects.ReelMultiplier, effects.SafeDepthBonus);
                diver.Relocate(diver.transform.position);
                equipped.Add(diver);
            }
        }
        private void OnDestroy() { if (progression != null) progression.ApplyForNextDive -= Apply; }
    }
}
