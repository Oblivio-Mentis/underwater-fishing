using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor
    {
        static readonly List<FishActor> ServerSchool=new();
        readonly RaycastHit[] swimHits=new RaycastHit[12];
        bool arrivalPending,staggered;
        float arrivalAt,departureAt,turnAt,fleeRemaining,swimSpeed,senseAt;
        Vector3 waypoint,alarmPoint;
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
            waypoint=_origin;
            turnAt=Time.time+6;
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

        void SwimReef(float dt,ref FishSnapshot state)
        {
            Vector3 pos=transform.position;
            if(Time.time>=senseAt)
            {
                senseAt=Time.time+.18f;
                var swimmer=NearestSwimmer(pos,RequiresPartner?2.45f:3.35f);
                if(swimmer!=null && !SolidAhead(pos,swimmer.transform.position))Frighten(swimmer.transform.position,2.8f);
            }
            if(Time.time>=turnAt || Vector3.SqrMagnitude(pos-waypoint)<1.25f)
            {
                float s=_clock+_origin.x*.73f;
                waypoint=_origin+new Vector3(Mathf.Sin(s*.71f)*4.4f,Mathf.Sin(s*.39f)*1.1f,Mathf.Cos(s*.89f)*4);
                turnAt=Time.time+3.4f+Mathf.Abs(Mathf.Sin(s)) * 3.1f;
            }

            bool fleeing=fleeRemaining>0;
            state.Startled=fleeing;
            fleeRemaining=Mathf.Max(0,fleeRemaining-dt);
            Vector3 target=fleeing?pos-alarmPoint:waypoint-pos;
            var direction=target.sqrMagnitude>.001f?target.normalized:transform.forward;
            direction.y+=Mathf.Sin(_clock*1.35f)*.085f;
            direction.y=Mathf.Clamp(direction.y,-.38f,.38f);
            if(pos.y>_origin.y+2.4f)direction.y=-.44f;
            if(pos.y<_origin.y-2.4f)direction.y=.44f;
            if((pos-_origin).sqrMagnitude>155 && !fleeing)direction=(_origin-pos).normalized;
            if(SolidAhead(pos,pos+direction*2))
            {
                var side=Vector3.Cross(Vector3.up,direction).normalized;
                if(SolidAhead(pos,pos+side*1.6f))side=-side;
                direction=(side+Vector3.up*.3f).normalized;
            }
            float speed=fleeing?Mathf.Lerp(2.4f,3.6f,Mathf.Clamp01(fleeRemaining/2)):Mathf.Max(.48f,tuning.roamSpeed)*(1+.1f*Mathf.Sin(_clock));
            swimSpeed=Mathf.MoveTowards(swimSpeed,speed,(fleeing?5.2f:1.3f)*dt);
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*(fleeing?220:66));
            Vector3 next=pos+transform.forward*(swimSpeed*dt);
            if(!SolidAhead(pos,pos+transform.forward*(swimSpeed*dt+.48f)))transform.position=next;
        }
    }
}
