using UnityEngine;

namespace UnderwaterFishing.Cargo
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Cargo Balance")]
    public sealed class CargoSettings : ScriptableObject
    {
        [Min(1)] public int Capacity = 12;
        [Min(.01f)] public float AscentSeconds = 8;
        [Min(.01f)] public float UnloadSeconds = 3;
        [Min(.01f)] public float DescentSeconds = 8;
        [Min(.5f)] public float InteractionDistance = 3.5f;
    }
}
