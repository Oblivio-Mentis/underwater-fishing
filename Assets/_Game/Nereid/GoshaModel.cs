using System;
using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class GoshaModel : MonoBehaviour
    {
        public Transform creature, body;
        public MeshRenderer ball;
        [Tooltip("Eight animated leg pivots in order, left front to back and then right front to back.")]
        public Transform[] legs = new Transform[8];
        [Tooltip("Left and right animated feeler pivots.")]
        public Transform[] feelers = new Transform[2];

        internal bool IsComplete => creature && body && ball && ball.TryGetComponent<MeshFilter>(out _) &&
            legs is {Length:8} && feelers is {Length:2} &&
            Array.TrueForAll(legs, pivot => pivot) && Array.TrueForAll(feelers, pivot => pivot);
    }
}
