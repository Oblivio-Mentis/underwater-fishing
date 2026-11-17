using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Cooperation;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Fishing.Network;
using UnityEngine;

namespace UnderwaterFishing.World
{
    public sealed class ExpeditionWorldLifecycle : MonoBehaviour
    {
        private ExpeditionWorld expedition;
        private void Start()
        {
            expedition = GetComponent<ExpeditionWorld>();
            expedition.ExpeditionStarted += BeginDive;
            expedition.ExpeditionEnded += EndDive;
        }
        private void BeginDive()
        {
            GetComponent<FishingWorld>().SpawnExpeditionFish();
            GetComponent<CargoWorld>().SpawnExpeditionLoot();
            GetComponent<CooperationWorld>().SpawnExpeditionTrap();
        }
        private void EndDive(bool emergency)
        {
            GetComponent<FishingWorld>().ClearExpeditionFish();
            GetComponent<CargoWorld>().ClearExpeditionLoot();
            GetComponent<CooperationWorld>().ClearExpeditionTraps();
        }
        private void OnDestroy()
        {
            if (expedition == null) return;
            expedition.ExpeditionStarted -= BeginDive; expedition.ExpeditionEnded -= EndDive;
        }
    }
}
