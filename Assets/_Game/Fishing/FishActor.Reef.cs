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
        ReefHabitats.Route habitat;
        int station, nextHabitat;
        float bank,stationUntil;
        bool leaving;
        public string HabitatName => habitat?.Name ?? "";
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
            var routes = depthTier > 1 ? ReefHabitats.Rift : ReefHabitats.Shelf;
            int budget = 0;
            foreach (var other in ServerSchool)
                if (other != this && other.depthTier == depthTier && other.IsWildInHabitat) budget++;
            if (budget >= 3) { arrivalAt = Time.time + 2; return; }

            int preference = SpeciesId switch { "Needlefish" => 0, "Lanternfish" => 1, "Bristlefish" => 2, _ => 2 };
            float visibility = depthTier > 1 ? 14 : 22;
            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[(preference + nextHabitat + i) % routes.Length];
                int localCount = 0;
                foreach (var other in ServerSchool)
                    if (other != this && other.IsWildInHabitat && other.habitat == route) localCount++;
                if (localCount >= 2) continue;

                int stride = route.Waypoints.Length;
                for (int j = 0; j < stride; j++)
                {
                    int entry = (j + (RequiresPartner ? 2 : 0)) % stride;
                    Vector3 point = RoutePoint(route, entry);
                    int next = (entry + 1) % stride;
                    Vector3 firstLeg = RoutePoint(route, next) - point;
                    if (!ReefHabitats.CanArrive(point, 12, visibility) ||
                        Physics.CheckSphere(point, BodyRadius + .17f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        !SwimClear(point, point + firstLeg.normalized * Mathf.Min(2.8f, firstLeg.magnitude)))
                        continue;
                    SetDepthActive(true);
                    habitat = route;
                    station = next;
                    stationUntil = Time.time + 14;
                    transform.SetPositionAndRotation(point, Quaternion.LookRotation(firstLeg));
                    departureAt = Time.time + (SpeciesId == "Bristlefish" ? 44 : 63) + power * 8;
                    senseAt = Time.time + .6f;
                    swimSpeed = Mathf.Max(.5f,tuning.roamSpeed);
                    var state = _snapshot.Value;
                    state.Position = point;
                    state.Rotation = transform.rotation;
                    _snapshot.Value = state;
                    return;
                }
            }
            arrivalAt = Time.time + 1.7f;
        }

        bool IsWildInHabitat => _depthActive && Mode != FishMode.Captured && Mode != FishMode.Transported;

        Vector3 RoutePoint(ReefHabitats.Route route,int index)
        {
            // Different vertical lanes keep two species sharing a habitat readable.
            float height = SpeciesId switch { "Needlefish" => .35f, "Lanternfish" => .15f, "Bristlefish" => -.6f, _ => .75f };
            return route.Waypoints[index] + Vector3.up * height;
        }

        bool HasRoamingReplacement()
        {
            foreach(var other in ServerSchool)
                if(other!=this && other.depthTier==depthTier && other._depthActive &&
                    other.Mode==FishMode.Roam && other.DesiredState==FishMode.Roam &&
                    (RequiresPartner || !other.RequiresPartner))return true;
            return false;
        }

        bool TryMigrate()
        {
            if (!staggered || habitat == null || Mode != FishMode.Roam || DesiredState != FishMode.Roam || lines.Count > 0)
                return false;
            if (Time.time <= departureAt || !HasRoamingReplacement()) return false;
            leaving = NearestSwimmer(transform.position,19) == null &&
                ReefHabitats.CanArrive(transform.position,19,depthTier > 1 ? 23 : 28);
            if (!leaving || fleeRemaining > 0 || Vector3.Distance(transform.position, RoutePoint(habitat,0)) > 1.8f) return false;
            nextHabitat++;
            ScheduleAt(Time.time + 9 + power * 1.4f);
            return true;
        }

        static DiverMotor NearestSwimmer(Vector3 point,float radius)
        {
            DiverMotor closest=null;
            float best=radius*radius;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(diver==null || diver.IsDead || diver.IsInside || ReefHabitats.InShelter(diver.transform.position))continue;
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

        float BodyRadius => RequiresPartner ? .36f : .19f;

        bool SwimClear(Vector3 start, Vector3 end)
        {
            if (ReefHabitats.InShelter(end)) return false;
            Vector3 path = end - start;
            if (path.sqrMagnitude < .0001f) return true;
            int count = Physics.SphereCastNonAlloc(start, BodyRadius, path.normalized, swimHits, path.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = swimHits[i].collider;
                if (collider.GetComponentInParent<FishActor>() == null && collider.GetComponentInParent<DiverMotor>() == null)
                    return false;
            }
            return true;
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
            if (habitat != null)
            {
                if (leaving) return RoutePoint(habitat,0) - pos;
                Vector3 next = RoutePoint(habitat,station);
                if ((next - pos).sqrMagnitude < 3.8f || Time.time > stationUntil)
                {
                    station = (station + 1) % habitat.Waypoints.Length;
                    stationUntil = Time.time + 14;
                    next = RoutePoint(habitat,station);
                }
                return next - pos;
            }
            float phase = _clock * (RequiresPartner ? .17f : .24f) + _origin.z;
            float radius = Mathf.Max(2,tuning.swimRadius);
            return _origin + radius * new Vector3(1.9f*Mathf.Cos(phase),.46f*Mathf.Sin(phase*1.6f),1.4f*Mathf.Sin(phase)) - pos;
        }

        Vector3 ClearHeading(Vector3 pos,Vector3 desired,float distance)
        {
            if(Time.time<avoidUntil && SwimClear(pos,pos+avoidDirection*distance))return avoidDirection;
            if(SwimClear(pos,pos+desired*distance))return desired;
            // Commit to one open side for the duration of a turn instead of
            // alternating directions against the corner of a rock every tick.
            float side=Vector3.Dot(transform.right,desired)>=0?1f:-1f;
            for(int i=0;i<6;i++)
            {
                float angle=(38+42*(i/2))*(i%2==0?side:-side);
                var candidate=Quaternion.AngleAxis(angle,Vector3.up)*desired;
                if(!SwimClear(pos,pos+candidate*distance))continue;
                avoidDirection=candidate;
                avoidUntil=Time.time+.65f;
                return candidate;
            }
            if (SwimClear(pos,pos+Vector3.up*distance)) return Vector3.up;
            if (SwimClear(pos,pos+Vector3.down*distance)) return Vector3.down;
            return -transform.forward;
        }

        void SwimReef(float dt,ref FishSnapshot state)
        {
            Vector3 position = transform.position;
            if (Time.time >= senseAt && Time.time >= hookAvailableAt && fleeRemaining <= .25f)
            {
                senseAt = Time.time + .18f;
                var swimmer = NearestSwimmer(position,RequiresPartner ? 3f : 4.1f);
                if (swimmer != null)
                {
                    Vector3 fromSwimmer = position - swimmer.transform.position;
                    float close = RequiresPartner ? 1.9f : 2.5f;
                    bool approachingFast = swimmer.IsSprinting && swimmer.TryGetComponent<CharacterController>(out var controller) &&
                        Vector3.Dot(controller.velocity,fromSwimmer.normalized) > 1.2f;
                    if ((fromSwimmer.sqrMagnitude < close * close || approachingFast) && !SolidAhead(position,swimmer.transform.position))
                        Frighten(swimmer.transform.position,2.6f);
                }
            }
            bool fleeing = fleeRemaining > 0;
            state.Startled = fleeing;
            float fleePower = Mathf.SmoothStep(0,1,Mathf.Clamp01((fleeRemaining - .45f) / 1.55f));
            fleeRemaining = Mathf.Max(0,fleeRemaining - dt);
            Vector3 target = fleeing ? (position - alarmPoint) : PatrolHeading(position);
            Vector3 direction = target.sqrMagnitude > .001f ? target.normalized : transform.forward;
            Vector3 lane = habitat != null ? RoutePoint(habitat,station) : _origin;
            if (fleeing && (lane - position).sqrMagnitude > 154)
                direction = Vector3.Slerp(direction,(lane - position).normalized,.72f);
            direction.y += Mathf.Sin(_clock * 1.35f) * .075f;
            direction.y = Mathf.Clamp(direction.y,-.45f,.45f);
            if (position.y > lane.y + 2.6f) direction.y = -.32f;
            if (position.y < lane.y - 2.6f) direction.y = .32f;
            direction = ClearHeading(position,direction.normalized,fleeing ? 2.7f : 2.1f);
            float speed = fleeing ? Mathf.Lerp(2.15f,RequiresPartner ? 5.35f : 5.9f,fleePower) :
                Mathf.Max(.5f,tuning.roamSpeed) * (RequiresPartner ? .9f : 1.15f) * (1 + .11f * Mathf.Sin(_clock * 2.3f));
            if (leaving && !fleeing) speed = 2.7f;
            swimSpeed = Mathf.MoveTowards(swimSpeed,speed,(fleeing ? 10f : 2.6f) * dt);
            FaceSwim(direction,dt,fleeing ? 5.4f : 2.8f);
            Vector3 step = transform.forward * (swimSpeed * dt);
            if (SwimClear(position,position + step + transform.forward * .20f)) transform.position += step;
            else swimSpeed = Mathf.MoveTowards(swimSpeed,0,10 * dt);
        }

        void FaceSwim(Vector3 direction,float dt,float turnRate)
        {
            if (direction.sqrMagnitude < .0001f) return;
            Vector3 heading = Vector3.RotateTowards(transform.forward,direction.normalized,dt * turnRate,0);
            float turn = Vector3.Dot(Vector3.Cross(transform.forward,direction.normalized),Vector3.up);
            bank = Mathf.MoveTowards(bank,-turn * (RequiresPartner ? 29 : 22),90 * dt);
            transform.rotation = Quaternion.LookRotation(heading) * Quaternion.AngleAxis(bank,Vector3.forward);
        }
    }
}
