using UnityEngine;

namespace UnderwaterFishing.Progression.Network
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Progression Settings")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        public ProgressionSettings Settings = new ProgressionSettings();
    }
}
