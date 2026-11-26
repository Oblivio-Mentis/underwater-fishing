using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        float nextCagedEelArrival;

        // Only the animals represented in the fishery definition have a cargo form.
        // The kraken and the vessel's hunger-wave attacker keep their own combat loops.
        internal bool CageAnimalNear(CageSpecies species, Vector3 centre, float radius)
        {
            if (!IsServerInitialized || !reefState.Value.Active || (species.deepOnly && depthTier < 2)) return false;
            Vector3 position;
            switch (species.id)
            {
                case "ReefAngler":
                    if (state.Value.PredatorHealth <= 0) return false;
                    position = state.Value.PredatorPosition;
                    break;
                case "NetEel":
                    if (!reefState.Value.HasEel) return false;
                    position = reefState.Value.Eel;
                    break;
                default: return false;
            }
            return (position - centre).sqrMagnitude <= radius * radius && PredatorClear(position, centre, 0f);
        }

        internal bool TakeCageAnimal(CageSpecies species, Vector3 centre, float radius)
        {
            if (!CageAnimalNear(species, centre, radius)) return false;
            if (species.id == "ReefAngler")
            {
                var main = state.Value;
                main.PredatorHealth = 0;
                main.AnglerStage = HuntStage.Recovery;
                anglerTarget = null;
                respawn = Mathf.Max(1f, tuning.predatorRespawnSeconds);
                state.Value = main;
            }
            else
            {
                var reef = reefState.Value;
                reef.HasEel = false;
                reef.EelPhase = 0;
                eelTarget = null;
                reefState.Value = reef;
                nextCagedEelArrival = Time.time + Mathf.Max(1f, tuning.predatorRespawnSeconds);
            }
            return true;
        }

        internal void ReleaseCageAnimal(string id, Vector3 point)
        {
            if (!IsServerInitialized || !reefState.Value.Active || !PredatorClear(point,point+Vector3.up*.1f,0)) return;
            if (id == "ReefAngler" && state.Value.PredatorHealth <= 0)
            {
                var main = state.Value;
                main.PredatorPosition = point;
                main.PredatorHealth = tuning.predatorHealth * Strength;
                AnglerRecover(ref main, P.stunnedCooldown);
                state.Value = main;
            }
            else if (id == "NetEel" && !reefState.Value.HasEel)
            {
                var reef = reefState.Value;
                reef.HasEel = true;
                reef.Eel = point;
                reef.EelHeading = Vector3.forward;
                DistractEel(ref reef,point+Vector3.forward*3,4);
                reefState.Value = reef;
            }
        }

        internal void AdvertiseCage(Vector3 centre, DiverMotor bait, float radius, bool artificial, bool attractEel)
        {
            if (!IsServerInitialized || !reefState.Value.Active) return;
            var main = state.Value;
            if (main.PredatorHealth > 0 && (centre-main.PredatorPosition).sqrMagnitude < radius*radius)
            {
                // Keep an existing live chase. A careless operator can still draw aggro.
                if (bait && CanPursue(bait) && main.AnglerStage == HuntStage.Watching && ClearTo(main.PredatorPosition, bait))
                {
                    var nearer = NearestVisible(main.PredatorPosition, radius);
                    BeginCircle(ref main, nearer ? nearer : bait);
                    state.Value = main;
                }
                else if (artificial && main.AnglerStage == HuntStage.Watching && PredatorClear(main.PredatorPosition,centre,0))
                {
                    noisePoint = centre;
                    noiseUntil = Time.time + P.noiseMemory;
                    main.AnglerStage = HuntStage.Investigate;
                    state.Value = main;
                }
            }
            var reef = reefState.Value;
            if (attractEel && reef.HasEel && bait && eelTarget == null && (reef.Eel-centre).sqrMagnitude < radius*radius &&
                CanPursue(bait) && ClearTo(reef.Eel,bait))
            {
                eelTarget = bait;
                eelDelay = 1.38f;
                eelBlindTime = 0;
                eelSeen = eelPreviousTarget = BodyPoint(bait);
                reef.EelPhase = 1;
                reefState.Value = reef;
            }
        }
    }
}
