using UnityEngine;

namespace Raid
{
    static class DiveOutfit
    {
        public static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent,false);
            pivot.localPosition = position;
            return pivot;
        }
    }
}
