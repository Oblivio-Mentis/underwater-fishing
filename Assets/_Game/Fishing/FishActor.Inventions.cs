using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        Vector3 suctionInlet;
        float suctionAt=-10, suctionSpeed;
        Vector3 normalFishScale=Vector3.one;

        internal void PullIntoVacuum(Vector3 inlet,float speed)
        {
            if(suctionAt==Time.fixedTime &&
               (transform.position-suctionInlet).sqrMagnitude<=(transform.position-inlet).sqrMagnitude)return;
            suctionAt=Time.fixedTime;
            suctionInlet=inlet;
            suctionSpeed=speed;
        }

        bool FollowSuction(float dt,ref FishSnapshot state)
        {
            state.VacuumDraw=0;
            if(Time.fixedTime-suctionAt>.08f || !CanHarvestRig(true))return false;
            var delta=suctionInlet-transform.position;
            float distance=delta.magnitude;
            float pull=suctionSpeed*Mathf.Lerp(1.12f,.43f,Mathf.InverseLerp(.5f,8f,distance));
            Vector3 spiral=Vector3.Cross(Vector3.up,delta).normalized;
            Vector3 current=(delta.normalized+spiral*Mathf.Lerp(.75f,.18f,Mathf.InverseLerp(.4f,4f,distance))).normalized;
            var point=transform.position+current*Mathf.Min(distance,pull*dt);
            if(!SwimClear(transform.position,point))
            {
                point=Vector3.MoveTowards(transform.position,suctionInlet,pull*dt);
                if(!SwimClear(transform.position,point))return false;
            }
            transform.position=point;
            if(current.sqrMagnitude>.01f)
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(current),1-Mathf.Exp(-dt*12));
            state.VacuumDraw=1-Mathf.InverseLerp(.24f,.92f,distance);
            state.Behaviour=FishBehaviour.Flee;
            return true;
        }

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
            foreach (var collider in _colliders) collider.enabled = false;
            RetireGoshaCatch(null);
            return item;
        }
    }
}
