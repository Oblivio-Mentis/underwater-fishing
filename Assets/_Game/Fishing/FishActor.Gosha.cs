using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        bool goshaReserved;
        public bool IsAvailableForGosha => IsServerInitialized && !goshaReserved && _depthActive &&
            !RequiresPartner && Mode==FishMode.Roam && DesiredState==FishMode.Roam && lines.Count==0 && !arrivalPending;

        public bool TryReserveForGosha()
        {
            if(!IsAvailableForGosha)return false;
            goshaReserved=true;
            SetReservationView(true);
            foreach(var shape in _colliders)shape.enabled=false;
            return true;
        }

        public void CarryForGosha(Vector3 position,Quaternion rotation)
        {
            if(!IsServerInitialized || !goshaReserved)return;
            transform.SetPositionAndRotation(position,rotation);
            var state=_snapshot.Value;
            state.Position=position;
            state.Rotation=rotation;
            _snapshot.Value=state;
        }

        public void ReleaseGoshaReservation()
        {
            if(!IsServerInitialized || !goshaReserved)return;
            goshaReserved=false;
            SetReservationView(false);
            foreach(var shape in _colliders)shape.enabled=_depthActive;
        }

        public bool CollectForGosha(HarpoonTool tool,string owner)
        {
            if(!IsServerInitialized || !goshaReserved || tool==null || !tool.AcceptGoshaCatch(this))return false;
            RaidWorld.Instance.RecordCatch(CargoId,new[]{owner});
            goshaReserved=false;
            SetReservationView(false);
            if(staggered)ScheduleAt(Time.time+tuning.respawnSeconds);
            else
            {
                _runtime.Reset();
                // Hide for the normal respawn time instead of minting a new catch in the pet's jaws.
                goshaRespawn=Time.time+tuning.respawnSeconds;
                _depthActive=false;
                var state=_snapshot.Value;
                state.Active=false;
                _snapshot.Value=state;
            }
            return true;
        }

        void SetReservationView(bool reserved)
        {
            var state=_snapshot.Value;
            state.Reserved=reserved;
            _snapshot.Value=state;
        }

        float goshaRespawn;
    }
}
