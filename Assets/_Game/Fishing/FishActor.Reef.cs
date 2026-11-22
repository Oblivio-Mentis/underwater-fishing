using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        internal static readonly List<FishActor> ServerSchool=new();
        readonly RaycastHit[] swimHits=new RaycastHit[12];
        bool arrivalPending,staggered;
        float arrivalAt,departureAt,fleeRemaining,swimSpeed,senseAt,hookAvailableAt,avoidUntil;
        Vector3 alarmPoint,avoidDirection;
        public bool InHabitat => _snapshot.Value.Active;
        public bool Startled => _snapshot.Value.Startled;

        public void EnterHabitat(bool available)
        {
            staggered=available && Nereid.NereidVessel.Active!=null;
            if(!staggered){SetDepthActive(available);return;}
            float delay=SpeciesId switch {"Needlefish"=>1.5f,"Lanternfish"=>6.8f,"Bristlefish"=>13.5f,_=>19f};
            ScheduleAt(Time.time+delay);
        }

        void ScheduleAt(float time)
        {
            SetDepthActive(false);
            arrivalPending=true;
            arrivalAt=time;
        }

        void TryArrive()
        {
            if(Time.time<arrivalAt)return;
            int occupying=0;
            foreach(var other in ServerSchool)
                if(other!=this && other.depthTier==depthTier && other._depthActive &&
                    other.Mode!=FishMode.Captured && other.Mode!=FishMode.Transported)occupying++;
            if(occupying>=3){arrivalAt=Time.time+1.3f;return;}

            // Swim in beyond the diver's immediate field of interaction.
            Vector3 entry=_origin;
            bool clear=false;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI*.25f+_clock;
                // A diver waiting at the shoal centre covers the entire inner entry ring.
                float radius=i<8?6.5f:10.5f;
                entry=_origin+new Vector3(Mathf.Cos(angle)*radius,.35f,Mathf.Sin(angle)*radius);
                if(NearestSwimmer(entry,7)!=null || Physics.CheckSphere(entry,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                clear=true;break;
            }
            if(!clear){arrivalAt=Time.time+1.5f;return;}

            SetDepthActive(true);
            transform.SetPositionAndRotation(entry,Quaternion.LookRotation(_origin-entry));
            departureAt=Time.time+52+power*8;
            senseAt=Time.time;
            swimSpeed=tuning.roamSpeed;
            var state=_snapshot.Value;
            state.Position=entry;
            state.Rotation=transform.rotation;
            _snapshot.Value=state;
        }

        bool HasRoamingReplacement()
        {
            foreach(var other in ServerSchool)
                if(other!=this && other.depthTier==depthTier && other._depthActive &&
                    other.Mode==FishMode.Roam && other.DesiredState==FishMode.Roam &&
                    (RequiresPartner || !other.RequiresPartner))return true;
            return false;
        }

        static DiverMotor NearestSwimmer(Vector3 point,float radius)
        {
            DiverMotor closest=null;
            float best=radius*radius;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(diver==null || diver.IsDead || diver.IsInside)continue;
                float squared=(diver.transform.position-point).sqrMagnitude;
                if(squared<best){closest=diver;best=squared;}
            }
            return closest;
        }

        public static void AlertNearShot(Vector3 start,Vector3 end)
        {
            Vector3 segment=end-start;
            if(segment.sqrMagnitude<.0001f || Nereid.NereidVessel.Active==null)return;
            foreach(var fish in ServerSchool)
            {
                if(!fish._depthActive || fish.Mode!=FishMode.Roam || fish.DesiredState!=FishMode.Roam)continue;
                float along=Mathf.Clamp01(Vector3.Dot(fish.transform.position-start,segment)/segment.sqrMagnitude);
                var nearest=start+segment*along;
                if((nearest-fish.transform.position).sqrMagnitude<10.2f && !fish.SolidAhead(nearest,fish.transform.position))
                    fish.Frighten(nearest-(end-start).normalized*.65f,3.4f);
            }
        }

        bool SolidAhead(Vector3 start,Vector3 end)
        {
            Vector3 path=end-start;
            if(path.sqrMagnitude<.0001f)return false;
            int count=Physics.RaycastNonAlloc(start,path.normalized,swimHits,path.magnitude,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var col=swimHits[i].collider;
                if(col.GetComponentInParent<FishActor>()==null && col.GetComponentInParent<DiverMotor>()==null)return true;
            }
            return false;
        }

        void Frighten(Vector3 point,float duration)
        {
            alarmPoint=point;
            fleeRemaining=Mathf.Max(fleeRemaining,duration);
        }

        void FleeFromLine(HarpoonTool angler)
        {
            Frighten(angler.transform.position+Vector3.up*.6f,Mathf.Max(3.1f,tuning.escapeRecoverySeconds));
            avoidUntil=0;
            hookAvailableAt=Time.time+Mathf.Max(.7f,tuning.escapeRecoverySeconds);
        }

        Vector3 PatrolHeading(Vector3 pos)
        {
            // A continuous lap across the fishing ground, with room to bank through
            // its turns. Each species keeps a distinct cruising height and phase.
            float phase=_clock*(RequiresPartner?.17f:.24f)+_origin.z;
            float radius=Mathf.Max(2,tuning.swimRadius);
            Vector3 point=_origin+radius*new Vector3(1.9f*Mathf.Cos(phase),
                .46f*Mathf.Sin(phase*1.6f),1.4f*Mathf.Sin(phase));
            return point-pos;
        }

        Vector3 ClearHeading(Vector3 pos,Vector3 desired,float distance)
        {
            if(Time.time<avoidUntil && !SolidAhead(pos,pos+avoidDirection*distance))return avoidDirection;
            if(!SolidAhead(pos,pos+desired*distance))return desired;
            // Commit to one open side for the duration of a turn instead of
            // alternating directions against the corner of a rock every tick.
            float side=Vector3.Dot(transform.right,desired)>=0?1f:-1f;
            for(int i=0;i<6;i++)
            {
                float angle=(38+42*(i/2))*(i%2==0?side:-side);
                var candidate=Quaternion.AngleAxis(angle,Vector3.up)*desired;
                if(SolidAhead(pos,pos+candidate*distance))continue;
                avoidDirection=candidate;
                avoidUntil=Time.time+.65f;
                return candidate;
            }
            return !SolidAhead(pos,pos+Vector3.up*distance)?Vector3.up:Vector3.down;
        }

        void SwimReef(float dt,ref FishSnapshot state)
        {
            Vector3 pos=transform.position;
            if(Time.time>=senseAt && Time.time>=hookAvailableAt)
            {
                senseAt=Time.time+.18f;
                var swimmer=NearestSwimmer(pos,RequiresPartner?2.45f:3.35f);
                if(swimmer!=null && !SolidAhead(pos,swimmer.transform.position))Frighten(swimmer.transform.position,2.8f);
            }
            bool fleeing=fleeRemaining>0;
            state.Startled=fleeing;
            fleeRemaining=Mathf.Max(0,fleeRemaining-dt);
            Vector3 target=fleeing?pos-alarmPoint:PatrolHeading(pos);
            var direction=target.sqrMagnitude>.001f?target.normalized:transform.forward;
            direction.y+=Mathf.Sin(_clock*1.35f)*.085f;
            direction.y=Mathf.Clamp(direction.y,-.38f,.38f);
            if(pos.y>_origin.y+2.4f)direction.y=-.44f;
            if(pos.y<_origin.y-2.4f)direction.y=.44f;
            if((pos-_origin).sqrMagnitude>155 && !fleeing)direction=(_origin-pos).normalized;
            direction=ClearHeading(pos,direction.normalized,fleeing?2.6f:1.8f);
            float speed=fleeing?Mathf.Lerp(2.8f,5.4f,Mathf.Clamp01(fleeRemaining/2f)):
                Mathf.Max(.5f,tuning.roamSpeed)*(RequiresPartner?.9f:1.15f)*(1+.12f*Mathf.Sin(_clock*2.3f));
            swimSpeed=Mathf.MoveTowards(swimSpeed,speed,(fleeing?14f:2f)*dt);
            var heading=Vector3.RotateTowards(transform.forward,direction,dt*(fleeing?8f:2.3f),0);
            transform.rotation=Quaternion.LookRotation(heading);
            Vector3 next=pos+transform.forward*(swimSpeed*dt);
            if(!SolidAhead(pos,pos+transform.forward*(swimSpeed*dt+.48f)))transform.position=next;
        }
    }
}
