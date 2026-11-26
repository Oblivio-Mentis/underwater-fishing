using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        internal bool CanHarvestRig(bool allowHeavy)
        {
            return IsServerInitialized && IsFreeSwimming && !arrivalPending && lines.Count == 0 &&
                   !IsStowed && !(_snapshot.Value.Reserved) && Time.time >= hookAvailableAt &&
                   (allowHeavy || !RequiresPartner);
        }

        internal bool DrawTowardRig(Vector3 point, float distance)
        {
            if (!CanHarvestRig(true) || distance <= 0f) return false;
            Vector3 next = Vector3.MoveTowards(transform.position, point, distance);
            if (!SwimClear(transform.position, next)) return false;
            transform.position = next;
            var snapshot = _snapshot.Value;
            snapshot.Position = next;
            _snapshot.Value = snapshot;
            return true;
        }

        internal CatchItem HarvestRig(bool allowHeavy)
        {
            if (!CanHarvestRig(allowHeavy)) return null;
            var item = ToCargo(CargoId);
            RetireGoshaCatch(null);
            return item;
        }
    }
}
