using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        Vector3 suctionInlet;
        float suctionAt=-10, suctionSpeed;
        Vector3 suctionOrigin, suctionTarget, suctionBend;
        float suctionProgress, suctionDuration;
        bool onSuctionPath, straightSuction;
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
            if(Time.fixedTime-suctionAt>.08f || !CanHarvestRig(true))
            {
                onSuctionPath=false;
                return false;
            }
            Vector3 current=transform.position;
            if(!onSuctionPath || (suctionTarget-suctionInlet).sqrMagnitude>.01f)
            {
                suctionOrigin=current;
                suctionTarget=suctionInlet;
                Vector3 inward=suctionTarget-current;
                float distance=inward.magnitude;
                Vector3 tangent=Vector3.Cross(Vector3.up,inward).normalized;
                suctionBend=tangent*Mathf.Min(2.4f,distance*.55f)+Vector3.up*Mathf.Min(.76f,distance*.25f);
                suctionDuration=Mathf.Clamp(distance/Mathf.Max(1,suctionSpeed*.69f),.28f,2.5f);
                suctionProgress=0;
                straightSuction=false;
                onSuctionPath=true;
            }
            float previous=suctionProgress;
            suctionProgress=Mathf.Min(1,suctionProgress+dt/suctionDuration);
            float arc=SuctionEase(suctionProgress);
            float bow=3*arc*(1-arc);
            Vector3 point=Vector3.Lerp(suctionOrigin,suctionTarget,arc)+suctionBend*bow;
            if(straightSuction || !SwimClear(current,point))
            {
                straightSuction=true;
                float stride=(arc-SuctionEase(previous))*Vector3.Distance(suctionOrigin,suctionTarget);
                point=suctionProgress>=1?suctionTarget:Vector3.MoveTowards(current,suctionTarget,stride);
                if(!SwimClear(current,point)){onSuctionPath=false;return false;}
            }
            transform.position=point;
            Vector3 facing=point-current;
            if(facing.sqrMagnitude>.00001f)
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-dt*15));
            state.VacuumDraw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,1,arc));
            state.Behaviour=FishBehaviour.Flee;
            return true;
        }

        static float SuctionEase(float t) => t*(.16f+t*(.22f+.62f*t));

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
