using UnderwaterFishing.Expedition.Network;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Presentation
{
    /// <summary>The prototype uses one physical scene; replicated phases select its base and dive dressing.</summary>
    public sealed class ExpeditionEnvironment : MonoBehaviour
    {
        public GameObject[] BaseObjects = System.Array.Empty<GameObject>(), ExpeditionObjects = System.Array.Empty<GameObject>();
        private bool applied, wasDiving;
        private void Update()
        {
            var expedition = NetworkExpedition.Instance;
            bool diving = expedition != null && (expedition.State.Phase == ExpeditionPhase.Expedition || expedition.State.Phase == ExpeditionPhase.Return);
            if (applied && diving == wasDiving) return;
            applied = true; wasDiving = diving;
            foreach (var go in BaseObjects) if (go != null) go.SetActive(!diving);
            foreach (var go in ExpeditionObjects) if (go != null) go.SetActive(diving);
        }
    }
}
