using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    // Free swimming on the server: habitat zones from data, a seeded per-fish random,
    // acceleration- and turn-limited velocity with inertia, Cruise/Hover/Forage/Alert/Flee/Recover.
    public sealed partial class FishActor
    {
        internal static readonly List<FishActor> ServerSchool=new();
        static readonly FishHabitat.Zone[] zoneBuffer=new FishHabitat.Zone[24];
        static readonly int[] zoneRank=new int[zoneBuffer.Length];
        readonly RaycastHit[] swimHits=new RaycastHit[12];
        bool arrivalPending,staggered;
        float arrivalAt,departureAt,fleeRemaining,senseAt,hookAvailableAt,avoidUntil;
        Vector3 alarmPoint,avoidDirection;
        FishHabitat.Zone habitat,lastHabitat;
        FishSpeciesProfile legacyProfile;
        System.Random rng;
        int station;
        float bank, dartUntil, dartRollAt;
        bool leaving;
        // Steering state.
        Vector3 velocity, target, homePoint;
        FishBehaviour swim=FishBehaviour.Cruise;
        float pauseUntil,alertTime,recoverUntil,stuckTime,targetAge;
        bool hasTarget;
        static Transform cageFrame;
        static float cageLookedUpAt=-10;

        public string HabitatName => habitat?.name ?? "";
        public Vector3 SwimVelocity => velocity;
        public float FleeSpeed => Profile.fleeSpeed;
        public bool InHabitat => _snapshot.Value.Active;
        public int HabitatPopulationLimit => Profile.habitat ? Mathf.Max(1,Profile.habitat.populationPerDepth) : 20;
        public bool Startled => _snapshot.Value.Startled;
        FishSpeciesProfile Profile => speciesProfile!=null ? speciesProfile : legacyProfile!=null ? legacyProfile : legacyProfile=FishSpeciesProfile.Legacy(SpeciesId);
        System.Random Rng => rng ??= MarineSpawn.NewStream(ObjectId * 7919 + depthTier);
        float Rand(float min,float max) => min+(float)Rng.NextDouble()*(max-min);

        public void EnterHabitat(bool available)
        {
            staggered=available && Nereid.NereidVessel.Active!=null;
            if(!staggered){SetDepthActive(available);return;}
            ScheduleAt(Time.time+Profile.arrivalDelay*Rand(.65f,1.15f)+Rand(0,2.5f));
            lastHabitat=null;
        }

        void ScheduleAt(float time)
        {
            if (habitat != null) lastHabitat=habitat;
            SetDepthActive(false);
            arrivalPending=true;
            arrivalAt=time;
        }

        void ResetSteering()
        {
            habitat=null;
            leaving=hasTarget=false;
            bank=0;
            velocity=Vector3.zero;
            swim=FishBehaviour.Cruise;
            pauseUntil=alertTime=recoverUntil=stuckTime=targetAge=0;
        }

        int GatherZones(out FishHabitat.Zone[] zones)
        {
            var asset=Profile.habitat;
            if(asset!=null)
            {
                int n=asset.Collect(depthTier,zoneBuffer);
                if(n>0){zones=zoneBuffer;return n;}
            }
            zones=FishHabitat.Legacy(depthTier);
            return zones.Length;
        }

        void TryArrive()
        {
            if(Time.time<arrivalAt)return;
            int budget = 0;
            foreach (var other in ServerSchool)
                if (other != this && other.depthTier == depthTier && other.IsWildInHabitat) budget++;
            if (budget >= HabitatPopulationLimit) { arrivalAt = Time.time + Rand(1f,2.5f); return; }

            var spec=Profile;
            int count=GatherZones(out var zones);
            float visibility = depthTier > 1 ? 14 : 22;
            if (count == 0) { arrivalAt = Time.time + 2; return; }
            if (zones != zoneBuffer) System.Array.Copy(zones,zoneBuffer,count);
            for (int i = count-1; i > 0; i--)
            {
                int j=Rng.Next(i+1);
                (zoneBuffer[i],zoneBuffer[j])=(zoneBuffer[j],zoneBuffer[i]);
            }
            int capacity=spec.habitat ? Mathf.Max(1,spec.habitat.fishPerZone) : 4;
            for (int i = 0; i < count; i++)
            {
                var zone = zoneBuffer[i];
                int localCount = 0;
                foreach (var other in ServerSchool)
                    if (other != this && other.IsWildInHabitat && other.habitat == zone) localCount++;
                // First seed the approach to the dive, then spread each wave across the map.
                int entryBonus=zone.entrySchool && !RequiresPartner && localCount<3 ? 16 : 0;
                zoneRank[i]=localCount>=capacity || zone.anchors==null || zone.anchors.Length==0
                    ? int.MaxValue : 4*localCount+(zone.role==spec.preferredHabitat?0:2)+(zone==lastHabitat?1:0)-entryBonus;
            }
            System.Array.Sort(zoneRank,zoneBuffer,0,count);
            for (int i = 0; i < count && zoneRank[i]!=int.MaxValue; i++)
            {
                var zone=zoneBuffer[i];
                int stride = zone.anchors.Length;
                int firstEntry = Rng.Next(stride);
                for (int attempt = 0; attempt < Mathf.Max(8, stride * 3); attempt++)
                {
                    int entry = (firstEntry + attempt) % stride;
                    int next = (entry + 1) % stride;
                    Vector3 lane = Vector3.Lerp(zone.anchors[entry], zone.anchors[next], Rand(0, 1));
                    lane += MarineSpawn.Scatter(Rng, zone.radius);
                    Vector3 point = LanePoint(zone, lane, Rand(.08f, .92f));
                    Vector3 firstLeg = LanePoint(zone, zone.anchors[next], .5f) - point;
                    if (firstLeg.sqrMagnitude < .01f) firstLeg = Vector3.forward;
                    if (!ReefHabitats.CanArrive(point, 12, visibility) ||
                        !MarineSpawn.InOpenWater(depthTier, point, BodyRadius + .17f) ||
                        !SwimClear(point, point + firstLeg.normalized * Mathf.Min(2.8f, firstLeg.magnitude)))
                        continue;
                    SetDepthActive(true);
                    fixedFrames = 0;
                    habitat = zone;
                    station = next;
                    homePoint = LanePoint(zone, zone.Anchor, .5f);
                    transform.SetPositionAndRotation(point, Quaternion.LookRotation(firstLeg));
                    velocity = firstLeg.normalized * Mathf.Max(.5f, spec.cruiseSpeed * .6f);
                    departureAt = Time.time + spec.dwellSeconds * Rand(.65f,1.05f);
                    senseAt = Time.time + .6f;
                    swim = FishBehaviour.Cruise;
                    PickTarget();
                    var state = _snapshot.Value;
                    state.Position = point;
                    state.Rotation = transform.rotation;
                    state.Behaviour = swim;
                    _snapshot.Value = state;
                    return;
                }
            }
            arrivalAt = Time.time + 1.7f;
        }

        bool IsWildInHabitat => _depthActive && Mode != FishMode.Captured && Mode != FishMode.Transported;

        // The recovery cage is a sealed-off volume too; the basket is looked up rarely, not per sweep.
        internal static bool InCage(Vector3 point)
        {
            if (Time.time > cageLookedUpAt + 2f)
            {
                cageLookedUpAt = Time.time;
                var world = RaidWorld.Instance;
                var basket = world != null && world.container != null ? world.container.GetComponentInChildren<Nereid.CargoBasket>() : null;
                cageFrame = basket != null ? basket.transform : null;
            }
            return cageFrame != null && CargoDispatch.ThisBasketContains(cageFrame, point);
        }

        // Resolve a height above the sea floor under `at`: t=0 is the low edge of the band, 1 the high edge.
        Vector3 LanePoint(FishHabitat.Zone zone,Vector3 at,float t)
        {
            var spec=Profile;
            float y;
            if (FloorUnder(at, out float floor))
                y = Mathf.Max(floor+BodyRadius+.2f, Mathf.Clamp(floor + Mathf.Lerp(spec.floorClearanceMin, spec.floorClearanceMax, t), at.y - 2.5f, at.y + 2.5f));
            else y = at.y + zone.fallbackLane;
            at.y = Mathf.Min(y, zone.ceiling - BodyRadius);
            return at;
        }

        bool FloorUnder(Vector3 at,out float floor)
        {
            floor = 0;
            int count = Physics.RaycastNonAlloc(at + Vector3.up * 1.5f, Vector3.down, swimHits, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = swimHits[i].collider;
                if (swimHits[i].distance >= best || col.GetComponentInParent<FishActor>() != null || col.GetComponentInParent<DiverMotor>() != null) continue;
                best = swimHits[i].distance;
                floor = swimHits[i].point.y;
            }
            return best < float.MaxValue;
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
            if (Time.time <= departureAt) return false;
            // Wild population turnover must not depend on a fish reaching an exact point
            // behind reef geometry. Let it depart from clear water out of the divers' view.
            if (fleeRemaining > 0 || !HasRoamingReplacement() ||
                !ReefHabitats.CanArrive(transform.position,19,depthTier > 1 ? 23 : 28)) return false;
            if (!leaving)
            {
                leaving=true;
                PickTarget();
                departureAt=Time.time+Rand(2f,5f);
                return false;
            }
            ScheduleAt(Time.time + Rand(6f,12f));
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
                    fish.Frighten(nearest-(end-start).normalized*.65f,fish.Profile.shotFleeDuration);
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

        float BodyRadius => (RequiresPartner ? .36f : .19f) * Profile.bodyRadiusMultiplier;

        bool SwimClear(Vector3 start, Vector3 end)
        {
            if (ReefHabitats.InShelter(end) || InCage(end)) return false;
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
            if(swim!=FishBehaviour.Flee){swim=FishBehaviour.Flee;pauseUntil=0;}
        }

        void FleeFromLine(HarpoonTool angler)
        {
            Frighten(angler.transform.position+Vector3.up*.6f,Mathf.Max(3.1f,tuning.escapeRecoverySeconds));
            avoidUntil=0;
            hookAvailableAt=Time.time+Mathf.Max(.7f,tuning.escapeRecoverySeconds);
        }

        // Choose the next patrol point: a scatter around the next zone anchor, validated against
        // the reef, the vessel, the airlock and the cage. Falls back to the anchor lane itself.
        void PickTarget()
        {
            var spec=Profile;
            hasTarget=true;
            targetAge=0;
            if (habitat == null)
            {
                float radius = Mathf.Max(2, tuning.swimRadius);
                float angle = Rand(0, Mathf.PI * 2);
                target = _origin + new Vector3(Mathf.Cos(angle) * radius * 1.9f, Rand(-.5f, .5f), Mathf.Sin(angle) * radius * 1.4f);
                return;
            }
            if (leaving) { target = homePoint; return; }
            Vector3 anchor = habitat.anchors[station];
            float scatter = Mathf.Min(habitat.radius, spec.patrolRadius);
            for (int i = 0; i < 6; i++)
            {
                float a = Rand(0, Mathf.PI * 2), r = scatter * Mathf.Sqrt(Rand(0, 1));
                Vector3 candidate = LanePoint(habitat, anchor + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), Rand(0, 1));
                if (ReefHabitats.InShelter(candidate) || InCage(candidate) ||
                    Physics.CheckSphere(candidate, BodyRadius + .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                target = candidate;
                return;
            }
            target = LanePoint(habitat, anchor, .5f);
        }

        void AdvanceStation()
        {
            if (habitat != null) station = (station + 1) % habitat.anchors.Length;
            PickTarget();
        }

        // Reached a waypoint: maybe pause there, otherwise keep cruising to the next one.
        void ArriveAtTarget()
        {
            var spec=Profile;
            float roll = Rand(0, 1);
            if (!leaving && swim == FishBehaviour.Cruise && roll < spec.hoverProbability)
            {
                swim = FishBehaviour.Hover;
                pauseUntil = Time.time + Rand(spec.hoverDurationMin, spec.hoverDurationMax);
            }
            else if (!leaving && swim == FishBehaviour.Cruise && roll < spec.hoverProbability + spec.forageProbability)
            {
                swim = FishBehaviour.Forage;
                pauseUntil = Time.time + Rand(spec.forageDurationMin, spec.forageDurationMax);
                if (FloorUnder(transform.position, out float floor))
                    target = new Vector3(transform.position.x, Mathf.Max(floor + spec.floorClearanceMin * .6f + BodyRadius, transform.position.y - 2f), transform.position.z);
            }
            else AdvanceStation();
        }

        Vector3 ClearHeading(Vector3 pos,Vector3 desired,float distance)
        {
            if(Time.time<avoidUntil && SwimClear(pos,pos+avoidDirection*distance))return avoidDirection;
            if(SwimClear(pos,pos+desired*distance))return desired;
            // Commit to one open side for the duration of a turn instead of
            // alternating directions against the corner of a rock every tick.
            float side=Vector3.Dot(transform.right,desired)>=0?1f:-1f;
            Vector3 lateral=Vector3.Cross(Vector3.up,desired).sqrMagnitude>.01f?Vector3.Cross(Vector3.up,desired).normalized:Vector3.right;
            for(int i=0;i<8;i++)
            {
                // Yaw candidates widen in pairs; every third sample pitches around the obstacle instead.
                Vector3 candidate = i % 3 == 2
                    ? Quaternion.AngleAxis((i < 4 ? 34 : 62) * (i / 3 % 2 == 0 ? 1 : -1), lateral) * desired
                    : Quaternion.AngleAxis((38 + 42 * (i / 2)) * (i % 2 == 0 ? side : -side), Vector3.up) * desired;
                if(!SwimClear(pos,pos+candidate*distance))continue;
                avoidDirection=candidate;
                avoidUntil=Time.time+1.1f;
                return candidate;
            }
            if (SwimClear(pos,pos+Vector3.up*distance)) return Vector3.up;
            if (SwimClear(pos,pos+Vector3.down*distance)) return Vector3.down;
            return -transform.forward;
        }

        // Divers and the hunting pet are the same kind of threat: the species' own senses decide.
        // Nothing here freezes the fish; it only raises the same alarm a diver would.
        void Sense(Vector3 position,float dt)
        {
            var spec=Profile;
            if (Time.time < senseAt || Time.time < hookAvailableAt || fleeRemaining > .25f) return;
            senseAt = Time.time + .18f;
            var world=RaidWorld.Instance;
            Vector3 hunterAt=default,hunterVelocity=default;
            bool hasHunter=world!=null && world.HunterAt(position,spec.senseDistance,out hunterAt,out hunterVelocity);
            var swimmer = NearestSwimmer(position, spec.senseDistance);
            float hunterDistance=hasHunter?(hunterAt-position).sqrMagnitude:float.MaxValue;
            float swimmerDistance=swimmer==null?float.MaxValue:(swimmer.transform.position-position).sqrMagnitude;
            if (swimmer == null && !hasHunter) { alertTime = Mathf.Max(0, alertTime - .18f * 2); if (swim == FishBehaviour.Alert && alertTime <= 0) swim = FishBehaviour.Cruise; return; }
            Vector3 threat; bool close;
            if(hunterDistance<swimmerDistance)
            {
                threat=hunterAt;
                bool fast=Vector3.Dot(hunterVelocity,(position-hunterAt).normalized)>1.2f;
                close=(position-hunterAt).sqrMagnitude<spec.closeDistance*spec.closeDistance||fast;
            }
            else
            {
                threat=swimmer.transform.position;
                Vector3 fromSwimmer = position - threat;
                bool approachingFast = swimmer.IsSprinting && swimmer.TryGetComponent<CharacterController>(out var controller) &&
                    Vector3.Dot(controller.velocity, fromSwimmer.normalized) > 1.2f;
                close = fromSwimmer.sqrMagnitude < spec.closeDistance * spec.closeDistance || approachingFast;
            }
            if (SolidAhead(position, threat)) return;
            alertTime += .18f;
            if (swim == FishBehaviour.Cruise || swim == FishBehaviour.Hover || swim == FishBehaviour.Forage)
            { swim = FishBehaviour.Alert; pauseUntil = 0; }
            alarmPoint = threat;
            if (close && alertTime >= spec.perceptionDelay) Frighten(threat, spec.fleeDuration);
        }

        void SwimReef(float dt,ref FishSnapshot state)
        {
            var spec=Profile;
            Vector3 position = transform.position;
            if (!hasTarget) PickTarget();
            Sense(position, dt);

            bool fleeing = fleeRemaining > 0;
            state.Startled = fleeing;
            float fleePower = Mathf.SmoothStep(0, 1, Mathf.Clamp01((fleeRemaining - .45f) / 1.55f));
            fleeRemaining = Mathf.Max(0, fleeRemaining - dt);
            if (fleeing) swim = FishBehaviour.Flee;
            else if (swim == FishBehaviour.Flee)
            {
                // Scare over: a cautious stretch heading back to the patrol before cruising again.
                swim = FishBehaviour.Recover;
                recoverUntil = Time.time + spec.recoverSeconds;
                alertTime = 0;
                PickTarget();
            }
            else if (swim == FishBehaviour.Recover && Time.time >= recoverUntil) swim = FishBehaviour.Cruise;
            else if ((swim == FishBehaviour.Hover || swim == FishBehaviour.Forage) && Time.time >= pauseUntil)
            { swim = FishBehaviour.Cruise; AdvanceStation(); }

            Vector3 home = habitat != null ? homePoint : _origin;
            Vector3 toTarget = target - position;
            float desiredSpeed;
            Vector3 direction;
            switch (swim)
            {
                case FishBehaviour.Flee:
                    direction = position - alarmPoint;
                    if (direction.sqrMagnitude < .01f) direction = -transform.forward;
                    direction.Normalize();
                    // Timid bottom fish dive for cover; open-water fish bolt level or climb a little.
                    direction.y = Mathf.Clamp(direction.y, -.3f, .3f) + Mathf.Lerp(.08f, -.34f, spec.hidingPreference);
                    if ((home - position).sqrMagnitude > spec.returnDistance * spec.returnDistance)
                        direction = Vector3.Slerp(direction.normalized, (home - position).normalized, .72f);
                    desiredSpeed = Mathf.Lerp(spec.cruiseSpeed * 1.3f, spec.fleeSpeed, fleePower);
                    break;
                case FishBehaviour.Hover:
                    direction = transform.forward;
                    direction.y = Mathf.Sin(_clock * 1.1f) * .12f;
                    desiredSpeed = spec.hoverSpeed;
                    break;
                case FishBehaviour.Forage:
                    direction = toTarget.sqrMagnitude > .09f ? toTarget : transform.forward + Vector3.Cross(Vector3.up, transform.forward) * Mathf.Sin(_clock * 2.4f) * .6f;
                    desiredSpeed = spec.forageSpeed;
                    break;
                case FishBehaviour.Alert:
                    // Drift slowly away from the swimmer while still tracking the patrol: a readable "noticed you".
                    direction = Vector3.Slerp(toTarget.sqrMagnitude > .01f ? toTarget.normalized : transform.forward, (position - alarmPoint).normalized, .55f);
                    desiredSpeed = spec.cruiseSpeed * .55f;
                    break;
                case FishBehaviour.Recover:
                    direction = toTarget;
                    desiredSpeed = spec.cruiseSpeed * .75f;
                    break;
                default:
                    direction = toTarget;
                    desiredSpeed = (leaving ? spec.cruiseSpeed * 1.5f : spec.cruiseSpeed) * (1 + .09f * Mathf.Sin(_clock * 2.3f));
                    // A living fish does not hold one speed: short darts punctuate the cruise.
                    if (Time.time >= dartRollAt)
                    {
                        dartRollAt = Time.time + 1f;
                        if (Rand(0, 1) < spec.dartChance) dartUntil = Time.time + spec.dartSeconds;
                    }
                    if (Time.time < dartUntil) desiredSpeed *= spec.dartSpeedMultiplier;
                    break;
            }
            if (direction.sqrMagnitude < .0001f) direction = transform.forward;
            direction.Normalize();
            direction.y += Mathf.Sin(_clock * 1.35f) * .05f;
            direction.y = Mathf.Clamp(direction.y, -.5f, .5f);
            if (habitat != null && position.y > habitat.ceiling - BodyRadius) direction.y = Mathf.Min(direction.y, -.3f);
            direction.Normalize();

            // Waypoint reached or stale: move on. Stale targets keep the fish from circling a point it can't quite hit.
            targetAge += dt;
            bool patrolling = swim == FishBehaviour.Cruise || swim == FishBehaviour.Recover || swim == FishBehaviour.Alert;
            if (patrolling && (toTarget.sqrMagnitude < 1.1f || targetAge > 18f)) ArriveAtTarget();

            // Look ahead along the predicted path by body size and sweep; commit to a clear side when blocked.
            float speedNow = velocity.magnitude;
            float lookahead = Mathf.Max(1.2f, speedNow * 1.1f) + BodyRadius;
            direction = ClearHeading(position, direction, lookahead);

            // Inertia: the heading turns no faster than the species allows at its current speed,
            // and speed changes by acceleration (braking harder, bolting much harder).
            Vector3 heading = speedNow > .02f ? velocity / speedNow : transform.forward;
            float turnLimit = Mathf.Max(spec.turnRate * .3f, Mathf.Min(spec.turnRate, Mathf.Rad2Deg * speedNow / spec.turnRadius));
            if (fleeing) turnLimit *= 1.8f;
            heading = Vector3.RotateTowards(heading, direction, turnLimit * Mathf.Deg2Rad * dt, 0);
            float accel = spec.acceleration * (desiredSpeed < speedNow ? 1.6f : 1f) * (fleeing ? 4f : 1f);
            float speed = Mathf.MoveTowards(speedNow, desiredSpeed, accel * dt);
            velocity = heading * speed;

            Vector3 step = velocity * dt;
            bool wantsMotion = desiredSpeed > .3f;
            if (SwimClear(position, position + step + heading * .20f))
            {
                transform.position += step;
                stuckTime = wantsMotion && step.sqrMagnitude < (desiredSpeed * dt * .15f) * (desiredSpeed * dt * .15f) ? stuckTime + dt : Mathf.Max(0, stuckTime - dt);
            }
            else
            {
                velocity *= .25f;
                stuckTime += dt * 2;
            }
            if (stuckTime > spec.stuckSeconds) Unstick(position);
            FaceSwim(heading, dt, 6f, direction);
            state.Behaviour = swim;
        }

        // Cornered against geometry: back out along a clear direction and choose a fresh waypoint.
        void Unstick(Vector3 position)
        {
            stuckTime = 0;
            avoidUntil = 0;
            Vector3 back = ClearHeading(position, -transform.forward + Vector3.up * .3f, 1.4f);
            velocity = back.normalized * Mathf.Max(.6f, Profile.cruiseSpeed * .7f);
            if (swim != FishBehaviour.Flee) { swim = FishBehaviour.Recover; recoverUntil = Time.time + Profile.recoverSeconds * .6f; }
            AdvanceStation();
        }

        // Rotate the body toward `direction`; roll into the turn toward `intent` (defaults to the same direction).
        void FaceSwim(Vector3 direction,float dt,float turnRate,Vector3 intent=default)
        {
            if (direction.sqrMagnitude < .0001f) return;
            if (intent.sqrMagnitude < .0001f) intent = direction;
            Vector3 heading = Vector3.RotateTowards(transform.forward,direction.normalized,dt * turnRate,0);
            float turn = Vector3.Dot(Vector3.Cross(transform.forward,intent.normalized),Vector3.up);
            // Bank eases in and out instead of slamming to the target each tick.
            bank = Mathf.Lerp(bank,-turn * Profile.bankStrength,1 - Mathf.Exp(-dt * 5f));
            transform.rotation = Quaternion.LookRotation(heading) * Quaternion.AngleAxis(bank,Vector3.forward);
        }
    }
}
