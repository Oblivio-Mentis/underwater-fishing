using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    [CreateAssetMenu(menuName = "Underwater Fishing/Loot")]
    public sealed class LootDefinition : ScriptableObject
    {
        public string DisplayName = "Обломок оборудования";
        [Min(1)] public int CargoSize = 2;
        public CargoYield Yield = new CargoYield { Scrap = 3 };
        [Range(.1f, 1f)] public float CarrySpeedMultiplier = .75f;
        public Vector3 Shape = new Vector3(.8f, .7f, .8f);
    }
}
