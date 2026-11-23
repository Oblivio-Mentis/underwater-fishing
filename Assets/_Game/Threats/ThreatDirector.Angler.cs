using UnityEngine;

namespace Raid
{
    // Фонарник: the reef angler. Server-only hunt cycle; every number comes from PredatorProfile.
    // Patrol → (sight | struggle noise → Investigate) → Circle → Display → Pursuit → Overshoot → Circle again or Recovery.
    public sealed partial class ThreatDirector
    {
        public PredatorProfile anglerProfile;
        bool ownsProfile;
        readonly Collider[] biteOverlaps = new Collider[8];
        System.Random anglerRandom;
        DiverMotor anglerTarget;
        Vector3 anglerGoal, noisePoint, overshootAim;
        float anglerClock, anglerSenseAt, anglerNoticed, anglerBlind, anglerGoalAt;
        float circleRadius, circleHeight, arcRemaining, stalkRemaining, overshootTravelled;
        float noiseUntil, noiseCooldownUntil;
        int circleDirection = 1, territoryIndex, anglerHits, anglerBites, anglerPasses;
        bool anglerBit;

        PredatorProfile P => anglerProfile;
        float AnglerScale => depthTier > 1 ? P.deepSpeedMultiplier : 1;
        public int BiteSequence => state.Value.BiteSequence;
        public int BiteVictim => state.Value.BiteVictim;
        public Vector3 BiteDirection => state.Value.BiteDirection;
        static bool Hunting(HuntStage stage) => stage != HuntStage.Watching && stage != HuntStage.Recovery;

        void EnsureProfile()
        {
            if (anglerProfile != null) return;
            anglerProfile = ScriptableObject.CreateInstance<PredatorProfile>();
            ownsProfile = true;
        }

        void ResetAngler()
        {
            anglerRandom = new System.Random(anglerProfile == null ? 0 : anglerProfile.seed);
            anglerTarget = null;
            anglerClock = anglerSenseAt = anglerNoticed = anglerBlind = anglerGoalAt = 0;
            noiseUntil = noiseCooldownUntil = 0;
            territoryIndex = anglerHits = anglerBites = anglerPasses = 0;
            anglerBit = false;
            anglerGoal = Vector3.zero;
        }

        float Rand(float min, float max) => Mathf.Lerp(min, max, (float)anglerRandom.NextDouble());
        Vector3 TerritoryCentre(int index) => tuning.predatorSpawn + (P.territoryOffsets == null || P.territoryOffsets.Length == 0 ? Vector3.zero : P.territoryOffsets[Mathf.Clamp(index, 0, P.territoryOffsets.Length - 1)]);
        int TerritoryCount => P.territoryOffsets == null || P.territoryOffsets.Length == 0 ? 1 : P.territoryOffsets.Length;

        Vector3 Home(Vector3 from)
        {
            Vector3 best = TerritoryCentre(0);
            for (int i = 1; i < TerritoryCount; i++)
            {
                Vector3 centre = TerritoryCentre(i);
                if ((centre - from).sqrMagnitude < (best - from).sqrMagnitude) best = centre;
            }
            return best;
        }

        bool BeyondLeash(Vector3 position) => (Home(position) - position).sqrMagnitude > P.leashDistance * P.leashDistance;

        // A fish fighting on a line is loud. Only a patrolling angler within earshot reacts, and not more often than the cooldown.
        public static void ReportStruggle(Vector3 point)
        {
            var director = Instance;
            if (director == null || !director.IsServerInitialized || director.anglerProfile == null) return;
            director.HearStruggle(point);
        }

        void HearStruggle(Vector3 point)
        {
            if (Nereid.NereidVessel.Active == null || !reefState.Value.Active || Time.time < noiseCooldownUntil) return;
            var main = state.Value;
            if (main.PredatorHealth <= 0 || main.AnglerStage != HuntStage.Watching) return;
            if ((point - main.PredatorPosition).sqrMagnitude > P.noiseDistance * P.noiseDistance || ReefHabitats.InShelter(point)) return;
            noisePoint = point;
            noiseUntil = Time.time + P.noiseMemory;
            noiseCooldownUntil = Time.time + P.noiseCooldown;
            main.AnglerStage = HuntStage.Investigate;
            anglerTarget = null;
            anglerSenseAt = 0;
            state.Value = main;
        }

        void TickAngler(ref ThreatSnapshot main, float dt)
        {
            if (main.PredatorHealth <= 0)
            {
                main.AnglerStage = HuntStage.Watching;
                anglerTarget = null;
                respawn -= dt;
                if (respawn <= 0) TryReturnAngler(ref main);
                return;
            }
            if (Hunting(main.AnglerStage) && BeyondLeash(main.PredatorPosition))
            {
                AnglerRecover(ref main, P.lostInterestCooldown);
                return;
            }
            switch (main.AnglerStage)
            {
                case HuntStage.Recovery:
                    AnglerPatrolStep(ref main, P.recoverySpeed, dt, true);
                    predatorCooldown -= dt;
                    if (predatorCooldown <= 0) main.AnglerStage = HuntStage.Watching;
                    return;
                case HuntStage.Watching: TickWatching(ref main, dt); return;
                case HuntStage.Investigate: TickInvestigate(ref main, dt); return;
                case HuntStage.Circle: TickCircle(ref main, dt); return;
                case HuntStage.Display: TickDisplay(ref main, dt); return;
                case HuntStage.Pursuit: TickPursuit(ref main, dt); return;
                case HuntStage.Overshoot: TickOvershoot(ref main, dt); return;
            }
        }

        void TickWatching(ref ThreatSnapshot main, float dt)
        {
            AnglerPatrolStep(ref main, P.patrolSpeed, dt, false);
            if (Time.time < anglerSenseAt) return;
            anglerSenseAt = Time.time + P.sensePeriod;
            var seen = NearestVisible(main.PredatorPosition, P.senseDistance * AnglerScale);
            if (seen == null) { anglerNoticed = 0; return; }
            anglerNoticed += P.sensePeriod;
            if (anglerNoticed < P.senseDelay) return;
            BeginCircle(ref main, seen);
        }

        void TickInvestigate(ref ThreatSnapshot main, float dt)
        {
            Vector3 step = SwimToward(main.PredatorPosition, noisePoint, P.investigateSpeed * AnglerScale * dt, P.bodyRadius);
            FaceTravel(ref main, step, dt);
            main.PredatorPosition = step;
            if (Time.time >= anglerSenseAt)
            {
                anglerSenseAt = Time.time + P.sensePeriod;
                var seen = NearestVisible(main.PredatorPosition, P.senseDistance * AnglerScale);
                if (seen != null) { BeginCircle(ref main, seen); return; }
            }
            bool arrived = (noisePoint - main.PredatorPosition).sqrMagnitude < P.bodyRadius * P.bodyRadius * 9;
            if (arrived || Time.time > noiseUntil) AnglerRecover(ref main, P.lostInterestCooldown);
        }

        void BeginCircle(ref ThreatSnapshot main, DiverMotor target)
        {
            anglerTarget = target;
            anglerBlind = 0;
            stalkRemaining = Rand(P.stalkSecondsMin, P.stalkSecondsMax);
            NewArc();
            main.AnglerStage = HuntStage.Circle;
        }

        void NewArc()
        {
            if (anglerRandom.NextDouble() < P.arcReverseChance) circleDirection = -circleDirection;
            circleRadius = Rand(P.circleRadiusMin, P.circleRadiusMax);
            circleHeight = Rand(-P.circleHeightVariation, P.circleHeightVariation);
            arcRemaining = Rand(P.arcSecondsMin, P.arcSecondsMax);
        }

        // Returns false when the hunt should end: shelter, death, long loss of sight.
        bool TrackTarget(ref ThreatSnapshot main, float dt)
        {
            if (!CanPursue(anglerTarget)) return false;
            if (ClearTo(main.PredatorPosition, anglerTarget)) anglerBlind = 0;
            else anglerBlind += dt;
            return anglerBlind <= P.losLossTimeout;
        }

        void TickCircle(ref ThreatSnapshot main, float dt)
        {
            if (!TrackTarget(ref main, dt)) { AnglerRecover(ref main, P.lostInterestCooldown); return; }
            Vector3 centre = BodyPoint(anglerTarget);
            Vector3 flat = main.PredatorPosition - centre;
            flat.y = 0;
            float radius = flat.magnitude;
            if (radius < .5f) { flat = Vector3.Cross(main.AnglerHeading, Vector3.up); radius = .5f; }
            float angle = Mathf.Atan2(flat.z, flat.x);
            float nextRadius = Mathf.MoveTowards(radius, circleRadius, P.circleRadialSpeed * AnglerScale * dt);
            angle += circleDirection * P.circleSpeed * AnglerScale * dt / Mathf.Max(nextRadius, 1);
            Vector3 goal = centre + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * nextRadius + Vector3.up * circleHeight;
            Vector3 step = SwimToward(main.PredatorPosition, goal, P.circleSpeed * AnglerScale * dt, P.bodyRadius);
            if ((step - main.PredatorPosition).sqrMagnitude < .000001f) { circleDirection = -circleDirection; NewArc(); }
            FaceTravel(ref main, step, dt);
            main.PredatorPosition = step;

            arcRemaining -= dt;
            if (arcRemaining <= 0) NewArc();
            stalkRemaining -= dt;
            flat = main.PredatorPosition - centre;
            flat.y = 0;
            bool close = (main.PredatorPosition - centre).sqrMagnitude <= P.closeStrikeDistance * P.closeStrikeDistance;
            if ((stalkRemaining > 0 && !close) || anglerBlind > 0 || flat.sqrMagnitude > P.circleRadiusMax * P.circleRadiusMax) return;
            main.AnglerStage = HuntStage.Display;
            anglerClock = Rand(P.telegraphMin, P.telegraphMax);
        }

        void TickDisplay(ref ThreatSnapshot main, float dt)
        {
            // A telegraph survives brief occlusion; the charge itself is stopped by stone anyway.
            if (!TrackTarget(ref main, dt) || Vector3.Distance(main.PredatorPosition, BodyPoint(anglerTarget)) > P.circleRadiusMax)
            {
                AnglerRecover(ref main, P.lostInterestCooldown);
                return;
            }
            anglerClock -= dt;
            if (anglerClock > P.aimLockSeconds)
            {
                Vector3 at = BodyPoint(anglerTarget) - main.PredatorPosition;
                if (at.sqrMagnitude > .001f)
                    main.AnglerHeading = Vector3.RotateTowards(main.AnglerHeading, at.normalized, dt * P.telegraphTurnRate, 0);
            }
            // The body stops and rears back from the target: a readable wind-up before the lunge.
            Vector3 drift = main.PredatorPosition - main.AnglerHeading * (P.telegraphDrift * dt);
            if (PredatorClear(main.PredatorPosition, drift, P.bodyRadius)) main.PredatorPosition = drift;
            if (anglerClock > 0) return;
            main.AnglerStage = HuntStage.Pursuit;
            anglerClock = P.chargeMaxSeconds;
            anglerBit = false;
        }

        // The charge is committed: heading stays locked. A swept bite sphere along the step must touch a diver's body collider.
        void TickPursuit(ref ThreatSnapshot main, float dt)
        {
            float travel = P.chargeSpeed * AnglerScale * dt;
            Vector3 from = main.PredatorPosition;
            Vector3 to = from + main.AnglerHeading * travel;
            if (!anglerBit)
            {
                var victim = SweptBite(from, main.AnglerHeading, travel);
                if (victim != null) Bite(ref main, victim);
            }
            if (!PredatorClear(from, to, P.bodyRadius))
            {
                AnglerRecover(ref main, P.stunnedCooldown);
                return;
            }
            main.PredatorPosition = to;
            anglerClock -= dt;
            bool passed = anglerTarget == null || !CanPursue(anglerTarget) || Vector3.Dot(BodyPoint(anglerTarget) - to, main.AnglerHeading) < 0;
            if (!passed && anglerClock > 0) return;
            main.AnglerStage = HuntStage.Overshoot;
            overshootTravelled = 0;
            Vector3 side = Vector3.Cross(Vector3.up, main.AnglerHeading);
            overshootAim = side.sqrMagnitude < .001f ? Vector3.forward : side.normalized * circleDirection;
        }

        DiverMotor SweptBite(Vector3 from, Vector3 heading, float travel)
        {
            int count = Physics.OverlapSphereNonAlloc(from, P.biteRadius, biteOverlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var diver = biteOverlaps[i].GetComponentInParent<DiverMotor>();
                if (diver != null && CanPursue(diver)) return diver;
            }
            count = Physics.SphereCastNonAlloc(from, P.biteRadius, heading, huntHits, travel, obstacleMask, QueryTriggerInteraction.Ignore);
            DiverMotor best = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = huntHits[i];
                if (hit.distance >= nearest) continue;
                var diver = hit.collider.GetComponentInParent<DiverMotor>();
                if (diver == null)
                {
                    // Rock or hull in front of the diver: the jaw closes on stone, not on a body.
                    if (hit.collider.GetComponentInParent<FishActor>() == null) { nearest = hit.distance; best = null; }
                    continue;
                }
                if (!CanPursue(diver)) continue;
                nearest = hit.distance;
                best = diver;
            }
            return best;
        }

        void Bite(ref ThreatSnapshot main, DiverMotor victim)
        {
            anglerBit = true;
            anglerBites++;
            victim.DamageOxygen(tuning.attackDamage * Strength);
            victim.ApplyExternalMotion(main.AnglerHeading * P.biteShove);
            var inventory = victim.GetComponent<HarpoonTool>();
            if (inventory != null && !inventory.ResistPocketTheft()) inventory.DropOneCatch();
            main.BiteSequence++;
            main.BiteVictim = victim.OwnerId;
            main.BiteDirection = main.AnglerHeading;
            main.Message = "Фонарник укусил водолаза и ушёл в разворот.";
        }

        void TickOvershoot(ref ThreatSnapshot main, float dt)
        {
            float travel = P.chargeSpeed * AnglerScale * dt;
            main.AnglerHeading = Vector3.RotateTowards(main.AnglerHeading, overshootAim, P.overshootTurnRate * Mathf.Deg2Rad * dt, 0);
            Vector3 step = SwimToward(main.PredatorPosition, main.PredatorPosition + main.AnglerHeading * (travel * 2), travel, P.bodyRadius);
            if ((step - main.PredatorPosition).sqrMagnitude < .000001f) { circleDirection = -circleDirection; overshootAim = -overshootAim; }
            overshootTravelled += Vector3.Distance(step, main.PredatorPosition);
            FaceTravel(ref main, step, dt);
            main.PredatorPosition = step;
            if (overshootTravelled < P.overshootDistance) return;
            anglerPasses++;
            if (anglerBites >= P.bitesToAggressionReset)
            {
                AnglerRecover(ref main, P.satiatedCooldown);
                main.Message = "Фонарник насытился и ушёл в глубину.";
            }
            else if (anglerPasses >= P.passesPerHunt || !CanPursue(anglerTarget)) AnglerRecover(ref main, P.lostInterestCooldown);
            else BeginCircle(ref main, anglerTarget);
        }

        // Confirmed harpoon hits (Strike already ray-checks the body) push a hunting angler off its prey.
        void AnglerStruck(ref ThreatSnapshot main)
        {
            if (!Hunting(main.AnglerStage)) return;
            anglerHits++;
            if (anglerHits < P.hitsToDisengage) return;
            AnglerRecover(ref main, P.disengageCooldown);
            main.Message = "Фонарник отпрянул от гарпунов.";
        }

        void AnglerRecover(ref ThreatSnapshot main, float seconds)
        {
            main.AnglerStage = HuntStage.Recovery;
            anglerTarget = null;
            anglerHits = anglerBites = anglerPasses = 0;
            anglerNoticed = anglerBlind = 0;
            predatorCooldown = seconds;
        }

        void FaceTravel(ref ThreatSnapshot main, Vector3 step, float dt)
        {
            Vector3 travel = step - main.PredatorPosition;
            if (travel.sqrMagnitude > .00001f) main.AnglerHeading = Vector3.RotateTowards(main.AnglerHeading, travel.normalized, dt * P.swimTurnRate, 0);
        }

        // Free 3D swim inside the territory: a fresh random point in the current volume every few seconds or on arrival.
        void AnglerPatrolStep(ref ThreatSnapshot main, float speed, float dt, bool homeward)
        {
            Vector3 home = TerritoryCentre(territoryIndex);
            if (homeward && (home - main.PredatorPosition).sqrMagnitude > P.territoryRadius * P.territoryRadius) anglerGoal = Home(main.PredatorPosition);
            else if (Time.time >= anglerGoalAt || (anglerGoal - main.PredatorPosition).sqrMagnitude < P.bodyRadius * P.bodyRadius * 4)
            {
                if (anglerGoalAt > 0 && anglerRandom.NextDouble() < P.territorySwitchChance) territoryIndex = anglerRandom.Next(TerritoryCount);
                anglerGoal = PickTerritoryPoint(TerritoryCentre(territoryIndex));
                anglerGoalAt = Time.time + P.patrolGoalSeconds;
            }
            Vector3 step = SwimToward(main.PredatorPosition, anglerGoal, speed * dt, P.bodyRadius);
            if ((step - main.PredatorPosition).sqrMagnitude < .000001f) anglerGoalAt = 0;
            FaceTravel(ref main, step, dt);
            main.PredatorPosition = step;
        }

        Vector3 PickTerritoryPoint(Vector3 centre)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = new(Rand(-1, 1), Rand(-1, 1), Rand(-1, 1));
                if (offset.sqrMagnitude > 1) offset.Normalize();
                Vector3 point = centre + new Vector3(offset.x * P.territoryRadius, offset.y * P.territoryHeight, offset.z * P.territoryRadius);
                if (ReefHabitats.InShelter(point) || Physics.CheckSphere(point, P.bodyRadius, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                return point;
            }
            return centre;
        }

        void TryReturnAngler(ref ThreatSnapshot main)
        {
            for (int i = 0; i < TerritoryCount; i++)
            {
                Vector3 point = TerritoryCentre(i);
                if (!ReefHabitats.CanArrive(point, P.respawnMinDistance, P.respawnClearView) ||
                    Physics.CheckSphere(point, P.bodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                main.PredatorPosition = point;
                main.PredatorHealth = tuning.predatorHealth * Strength;
                main.AnglerHeading = Vector3.forward;
                territoryIndex = i;
                anglerGoalAt = 0;
                AnglerRecover(ref main, P.lostInterestCooldown);
                return;
            }
            respawn = P.respawnRetrySeconds;
        }
    }
}
