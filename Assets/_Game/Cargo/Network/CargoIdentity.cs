using System.Threading;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    /// <summary>Authority-assigned source identity; FishNet object IDs are deliberately not used for cargo.</summary>
    public sealed class CargoIdentity : MonoBehaviour
    {
        private static long sequence;
        private ulong id;
        public ulong Id => id == 0 ? id = Next() : id;
        internal static ulong Next() => unchecked((ulong)Interlocked.Increment(ref sequence));
        public static ulong For(GameObject source)
        {
            var identity = source.GetComponent<CargoIdentity>();
            if (identity == null) identity = source.AddComponent<CargoIdentity>();
            return identity.Id;
        }
    }
}
