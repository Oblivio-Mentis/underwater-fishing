using UnityEngine;

namespace UnderwaterFishing.Expedition
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Expedition Balance")]
    public sealed class ExpeditionSettings : ScriptableObject
    {
        [Min(.1f)] public float PreparingSeconds = 3;
        [Min(.1f)] public float ReturnSeconds = 3;
        [Min(.1f)] public float DebriefSeconds = 4;
        [Min(1f)] public float CargoWaitTimeoutSeconds = 45;
        [Min(.5f)] public float TerminalDistance = 3.5f;
        [Min(5f)] public float FastExpeditionSeconds = 45;
        [Min(1)] public int DaysPerOffering = 3;
    }
}
