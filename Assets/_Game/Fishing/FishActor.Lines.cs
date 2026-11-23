using UnityEngine;
using System.Collections.Generic;

namespace Raid
{
    public sealed partial class FishActor
    {
        sealed class HookLine
        {
            public int OwnerId;
            public HarpoonTool Tool;
            public HarpoonContact Contact;
            public FishingLine Spool;
            public float Distance, Tension, BlockedTime, TugSpeed, TurnLoad;
            public Vector3 PullDirection;
            public bool Working, Paired;
        }

        sealed class CrewHooks : FishingHooks<HookLine>
        {
            protected override int GetKeyForItem(HookLine line) => line.OwnerId;
        }

        readonly CrewHooks lines = new();
        HarpoonTool landingTool;
        FishFightBrain fight;
        FishFightInput fightInput;
        Vector3 fightHeading;
        int fightSeed;
        float deflectReadyAt;

        void DeflectOnce()
        {
            if (Time.time < deflectReadyAt) return;
            deflectReadyAt = Time.time + .6f;
            fight.Deflect();
        }

        public FishFightBrain FightBrain => fight;

        internal bool TryHook(HarpoonTool tool, HarpoonContact contact)
        {
            if (!IsServerInitialized || !_depthActive || _runtime == null || tool == null || tool.Motor.IsHelpless
                || goshaReserved || tool.Owner == null || !tool.Owner.IsActive || tool.HookedFish != null || _carrier != null
                || contact.target != NetworkObject || !contact.Resolve(out _, out var point, out _, out _)) return false;
            bool first = lines.Count == 0;
            if (first && (Mode != FishMode.Roam || DesiredState != FishMode.Roam || Time.time < hookAvailableAt)) return false;
            if (!first && (!RequiresPartner || DesiredState < FishMode.Hooked || DesiredState > FishMode.Tired)) return false;
            float length = Vector3.Distance(tool.FishingAnchor, point);
            if (!lines.Attach(new HookLine { OwnerId = tool.OwnerId, Tool = tool, Contact = contact,
                Spool = new FishingLine(length), Tension = .1f })) return false;
            tool.UpdateFishingLine(length, .1f);
            var state = _snapshot.Value;
            if (first)
            {
                DesiredState = FishMode.Hooked;
                // One brain per fight: seeded from the catch so a replay with the same cargo id fights the same way.
                fightSeed = unchecked((int)_cargoId ^ (int)(_cargoId >> 32));
                var profile = tuning.FightProfile(SpeciesId);
                if (fight == null || fight.Profile != profile) fight = new FishFightBrain(profile, fightSeed);
                else fight.Reset(fightSeed);
                fightHeading = transform.forward;
                state.Stamina = Mathf.Max(1, tuning.initialStamina * power);
                state.Tension = .1f;
                state.Behaviour = FishBehaviour.HookShock;
                state.TugStrength = 0;
            }
            else fight?.Shock();
            state.HookCount = lines.Count;
            state.HookSequence++;
            _snapshot.Value = state;
            return true;
        }

        IEnumerable<string> LandingOwners(HarpoonTool winner, bool tired)
        {
            var world=RaidWorld.Instance;
            yield return world.ProgressKey(winner.Motor);
            if(!RequiresPartner)yield break;
            HookLine primary=null;
            foreach(var line in lines)if(line.Tool==winner){primary=line;break;}
            if(primary==null)yield break;
            foreach(var line in lines)
                if(line!=primary && ReadyForLanding(line,primary,tired))
                    yield return world.ProgressKey(line.Tool.Motor);
        }

        void ClearFishing()
        {
            foreach (var line in lines) if (line.Tool != null) line.Tool.NotifyFishLost(this);
            lines.Clear();
            landingTool = null;
            if (_carrier != null) _carrier.NotifyFishLost(this);
            _carrier = null;
        }

        public void ReleaseHook(HarpoonTool tool)
        {
            if (!IsServerInitialized || tool == null) return;
            if (_carrier == tool && Mode == FishMode.Captured)
            {
                _carrier = null;
                tool.NotifyFishLost(this);
                FleeFromLine(tool);
                DesiredState = FishMode.Roam;
                return;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines.At(i).Tool != tool) continue;
                RemoveLine(i);
                var state = _snapshot.Value;
                state.HookCount = lines.Count;
                state.Coordinated = false;
                _snapshot.Value = state;
                return;
            }
        }

        void RemoveLine(int index)
        {
            var tool = lines.At(index).Tool;
            lines.RemoveAt(index);
            if (tool != null) tool.NotifyFishLost(this);
            if (landingTool != null)
            {
                landingTool = null;
                DesiredState = Mode;
            }
            if (lines.Count != 0) return;
            if (tool != null) FleeFromLine(tool);
            DesiredState = Mode == FishMode.Roam ? FishMode.Roam : FishMode.Escaped;
        }

        void SampleLines(float dt)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines.At(i);
                var tool = line.Tool;
                if (tool == null || !tool.IsServerInitialized || tool.Owner == null || !tool.Owner.IsActive
                    || tool.Motor.IsHelpless || tool.ActiveSlot != 0 || tool.HookedFish != this
                    || !line.Contact.Resolve(out _, out var contact, out _, out _))
                { RemoveLine(i); continue; }
                // The spool and the force use the diver's body, independent of where the gun is pointing.
                Vector3 anchor = tool.FishingAnchor;
                line.Distance = Vector3.Distance(anchor, contact);
                var direction=(anchor-transform.position).normalized;
                line.TurnLoad=line.PullDirection.sqrMagnitude>.01f && direction.sqrMagnitude>.01f
                    ? Mathf.Clamp01(Vector3.Angle(line.PullDirection,direction)/Mathf.Max(.01f,120*dt)) : 0;
                line.PullDirection=direction;
                bool blocked = SolidAhead(anchor, contact);
                line.BlockedTime = blocked ? line.BlockedTime + dt : 0;
                if (line.Distance > tuning.maxRange || line.BlockedTime > tuning.obstructionGrace || line.Tension >= 1)
                { RemoveLine(i); continue; }
                line.Working = !blocked && tool.Reeling && line.Spool.Loaded(line.Distance, tuning.slackAllowance);
                line.Paired = false;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines.At(i).Working) continue;
                for (int j = i+1; j < lines.Count; j++)
                    if (lines.At(j).Working && Vector3.Dot(lines.At(i).PullDirection, lines.At(j).PullDirection) >= tuning.pullDirectionDot)
                        lines.At(i).Paired = lines.At(j).Paired = true;
            }
        }

        bool ReadyForLanding(HookLine line, HookLine leader, bool tired)
        {
            // For heavy fish, winding time only accrues while two lines genuinely
            // pull together. A line may go slack when the exhausted fish reaches a hand.
            if (!line.Tool.Reeling || line.BlockedTime > 0 || line.Tension >= 1 ||
                line.Spool.WindingTime < tuning.minimumWindingSeconds) return false;
            return tired || (line.Working && Vector3.Dot(line.PullDirection, leader.PullDirection) >= tuning.pullDirectionDot);
        }

        bool GripTiredFish()
        {
            foreach (var candidate in lines)
            {
                if (!ReadyForLanding(candidate, candidate, true) || !WithinHandReach(candidate.Tool.FishingAnchor)) continue;
                foreach (var support in lines)
                {
                    if (support == candidate || !ReadyForLanding(support, candidate, true)) continue;
                    landingTool = candidate.Tool;
                    DesiredState = FishMode.Captured;
                    return true;
                }
            }
            return false;
        }

        bool WithinHandReach(Vector3 hand)
        {
            Vector3 toFish = transform.position - hand;
            if (toFish.sqrMagnitude <= .25f * tuning.captureRange * tuning.captureRange)
                return !SolidAhead(hand, transform.position);
            // A mesh ray can grab a near flank when the harpoon itself is on the far side.
            var approach = new Ray(hand, toFish.normalized);
            foreach (var surface in _colliders)
                if (surface.Raycast(approach, out var grip, tuning.captureRange) && !SolidAhead(hand, grip.point))
                    return true;
            return false;
        }

        void SimulateLine(ref FishSnapshot state, bool tired, float dt)
        {
            SampleLines(dt);
            state.HookCount = lines.Count;
            state.Coordinated = false;
            if (lines.Count == 0) { DesiredState = FishMode.Escaped; return; }
            if (tired && RequiresPartner && lines.Count >= 2 && GripTiredFish()) return;
            if (fight == null) fight = new FishFightBrain(tuning.FightProfile(SpeciesId), fightSeed);
            var profile = fight.Profile;

            // Decide the sub-phase first; every rule below reads the brain, never the clock.
            float maxStamina = Mathf.Max(1, tuning.initialStamina * power);
            float nearest = float.MaxValue;
            bool anyReeling = false, anyBlocked = false, anyPaired = false;
            foreach (var line in lines)
            {
                nearest = Mathf.Min(nearest, line.Distance);
                anyReeling |= line.Tool.Reeling;
                anyBlocked |= line.BlockedTime > 0;
                anyPaired |= line.Paired;
            }
            fightInput.Dt = dt;
            fightInput.Stamina01 = Mathf.Clamp01(state.Stamina / maxStamina);
            fightInput.Distance01 = Mathf.Clamp01(nearest / Mathf.Max(1, tuning.maxRange));
            fightInput.Hooks = lines.Count;
            fightInput.Coordinated = anyPaired;
            fightInput.Obstructed = anyBlocked;
            fightInput.Reeling = anyReeling;
            fightInput.NearHand = nearest <= tuning.captureRange * profile.lastEscapeRange;
            fightInput.Exhausted = tired || state.Stamina <= 0;
            fight.Tick(in fightInput);
            state.Behaviour = fight.CurrentPhase;
            state.SurgeWarning = !tired && fight.SurgeWarning;
            state.Surging = !tired && fight.Surging;
            state.TugStrength = tired ? 0 : fight.TugStrength;
            if (fight.SurgeStarted && !tired)
            {
                state.TugSequence++;
                // A thrashing fish is heard by the reef angler; the director rate-limits how often it reacts.
                ThreatDirector.ReportStruggle(transform.position);
            }
            bool weakened = fight.Weakened;
            bool bolting = state.Surging && !tired;

            HookLine leader = null;
            state.Tension = 0;
            var world=RaidWorld.Instance;
            foreach (var line in lines)
            {
                bool working = line.Working && (!RequiresPartner || line.Paired);
                var motor=line.Tool.Motor;
                float teamTiming=line.Paired?(world?.SkillFactor(motor,SkillLine.SyncPull)??0):0;
                float reel=Mathf.Clamp(line.Tool.ReelMultiplier*(1+teamTiming),.1f,2.2f);
                float lineStrength=1+Mathf.Clamp(world?.SkillFactor(motor,SkillLine.Cable)??0,0,.85f);
                float tip=Mathf.Clamp(world?.SkillFactor(motor,SkillLine.Tip)??0,0,.40f);
                float resistance = state.Surging ? Mathf.Lerp(1, tuning.burstTensionMultiplier, state.TugStrength) : weakened ? profile.weakenedTension : 1;
                if (working)
                {
                    line.Spool.Wind(line.Distance, tuning.slackAllowance, tuning.reelSpeed * reel, dt);
                    state.Coordinated |= line.Paired;
                    if (leader == null) leader = line;
                }
                else if (!line.Working || !line.Tool.Reeling) line.Spool.Slip(line.Distance);
                bool strained = line.Working || (state.SurgeWarning && line.Spool.Loaded(line.Distance, tuning.slackAllowance));
                if (strained)
                    line.Tension += tuning.tensionRise * resistance * power * dt * (1+.20f*line.TurnLoad*(1-tip)) /
                        (lineStrength*(line.Paired ? 1 + tuning.assistBonus : 1));
                else line.Tension -= tuning.tensionRelease * dt;
                if(state.Surging && line.Tension>=1 && line.Tool.TryStabilizeDeepLine())line.Tension=.65f;
                line.Tension = Mathf.Clamp01(line.Tension);
                state.Tension = Mathf.Max(state.Tension, line.Tension);
                line.Tool.UpdateFishingLine(line.Spool.PaidOut, line.Tension);
                // Two hooks share the body's pull: weaker per diver, never zero (the brain scales TugStrength).
                bool taut = line.BlockedTime == 0 && line.Tension < 1 &&
                    line.Spool.Loaded(line.Distance, tuning.slackAllowance);
                float tug = bolting ? Mathf.Min(tuning.maxTugSpeed, tuning.tugSpeed * power * state.TugStrength) : 0;
                line.TugSpeed = taut ? Mathf.MoveTowards(line.TugSpeed, tug, tuning.tugAcceleration * dt) : 0;
                if (line.TugSpeed > 0 && line.Distance > tuning.captureRange)
                {
                    var towardFish = -line.PullDirection;
                    float damp=Mathf.Clamp(world?.SkillFactor(motor,SkillLine.Damper)??0,0,.42f);
                    float stability=Mathf.Clamp(world?.SkillFactor(motor,SkillLine.TugStability)??0,0,.31f);
                    // Reduce the strength of horizontal pulls while keeping the force on the same axis.
                    float horizontal = Vector3.ProjectOnPlane(towardFish, Vector3.up).magnitude;
                    Vector3 tugMotion = towardFish * (line.TugSpeed * dt * (1-damp) * (1-stability*horizontal));
                    float verticalLimit = Mathf.Max(0, motor.tuning.verticalSpeed) * dt;
                    tugMotion.y = Mathf.Clamp(tugMotion.y, -verticalLimit, verticalLimit);
                    // Sweep the controller while exempting forced ascent from the swimmer's own ascent penalty.
                    motor.ApplyTetherMotion(tugMotion);
                }
            }

            var lead = leader ?? lines.At(0);
            Vector3 away = -lead.PullDirection;
            if (away.sqrMagnitude < .01f) away = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            if (side.sqrMagnitude < .01f) side = transform.right;
            side.Normalize();

            // During a surge the fish bolts away and the taut line drags the angler after it.
            if (leader != null && !bolting)
            {
                float assistance=leader.Paired?1+tuning.assistBonus+(world?.SkillFactor(leader.Tool.Motor,SkillLine.SyncPull)??0):1;
                float speed = tuning.reelSpeed * Mathf.Clamp(leader.Tool.ReelMultiplier,.1f,2.2f)
                    * (weakened ? 1.35f : 1) * assistance;
                // Move only the excess separation. A diver swimming towards a slack line cannot pull the fish.
                float excess = Mathf.Max(0, leader.Distance - leader.Spool.PaidOut);
                Vector3 motion = leader.PullDirection * Mathf.Min(excess, speed * dt);
                if (!SolidAhead(transform.position, transform.position + motion)) transform.position += motion;
                // Lateral arcs under the reel: sideways within the paid-out line, so the fish
                // swings across the diver instead of walking straight to the hand.
                if (!tired && Nereid.NereidVessel.Active != null) StruggleSideways(leader, side, dt);
                if (!tired) StruggleFace(away, side, dt);
                float fatigue=1+.5f*(world?.SkillFactor(leader.Tool.Motor,SkillLine.Tip)??0);
                state.Stamina = Mathf.Max(0, state.Stamina - tuning.exhaustionRate * dt * assistance * fatigue);
                bool partnerReady = !RequiresPartner;
                foreach (var other in lines)
                    if (other != leader && ReadyForLanding(other, leader, false))
                        partnerReady = true;
                if (leader.Tension < 1 && partnerReady && (RequiresPartner || !leader.Tool.HasFullCreel)
                    && leader.Spool.AtHand(leader.Distance, tuning.captureRange, tuning.minimumWindingSeconds))
                {
                    landingTool = leader.Tool;
                    DesiredState = FishMode.Captured;
                }
                else if (state.Stamina <= 0 && !tired) DesiredState = FishMode.Tired;
            }
            else
            {
                float escape = tuning.escapeSpeed * power * (tired ? .10f
                    : state.Surging ? Mathf.Lerp(1, profile.surgeSpeedMultiplier, state.TugStrength)
                    : weakened ? profile.weakenedSpeedMultiplier : 1);
                Vector3 desired = away;
                if (Nereid.NereidVessel.Active != null && !tired && !bolting)
                    desired = (away + side * fight.Lateral * profile.lateralStrength).normalized;
                Vector3 dir = ClearHeading(transform.position, desired, 1);
                if (dir != desired && Vector3.Dot(dir, side * Mathf.Sign(fight.Lateral)) < 0) DeflectOnce();
                if (state.Behaviour == FishBehaviour.Lateral) escape *= 1.6f;
                // A surge starts in the escape direction. Normal swimming retains the species' turn inertia.
                fightHeading = bolting || fightHeading.sqrMagnitude < .01f ? dir :
                    Vector3.RotateTowards(fightHeading, dir, profile.turnRate * dt, 0);
                Vector3 move = fightHeading * (escape * dt);
                if ((!bolting || Vector3.Dot(fightHeading, away) > 0f) &&
                    !SolidAhead(transform.position, transform.position + move)) transform.position += move;
                else DeflectOnce();
                FaceSwim(fightHeading, dt, tired ? 1.75f : profile.turnRate);
                // Not being reeled: the fish breathes. A weakened fish recovers fastest, so slacking off costs the diver.
                if (!tired && !state.Surging) state.Stamina = Mathf.Min(maxStamina, state.Stamina + tuning.staminaRecovery * dt * (weakened ? profile.weakenedRecovery : 1));
            }
        }

        // Sideways drift while being wound in. Bounded by the paid-out line and the reef.
        void StruggleSideways(HookLine leader, Vector3 side, float dt)
        {
            float lateral = fight.Lateral * fight.Profile.lateralStrength * (Behaviour == FishBehaviour.Surge ? 1.4f : 1);
            if (Mathf.Abs(lateral) < .01f) return;
            Vector3 step = side * (lateral * dt);
            Vector3 next = transform.position + step;
            // Slide across the taut line while the reel closes any existing overrun at its own speed.
            Vector3 anchor = leader.Tool.FishingAnchor;
            float reach = Mathf.Max(leader.Spool.PaidOut + tuning.slackAllowance,
                Vector3.Distance(transform.position, anchor));
            Vector3 offset = next - anchor;
            if (offset.sqrMagnitude > reach * reach)
            {
                next = anchor + offset.normalized * reach;
                if ((next - transform.position).sqrMagnitude < step.sqrMagnitude * .04f) { DeflectOnce(); return; }
            }
            if (SolidAhead(transform.position, next)) { DeflectOnce(); return; }
            transform.position = next;
        }

        void StruggleFace(Vector3 away, Vector3 side, float dt)
        {
            Vector3 face = away + side * fight.Lateral * (Behaviour == FishBehaviour.Surge ? .6f : .35f);
            fightHeading = Vector3.RotateTowards(fightHeading.sqrMagnitude < .01f ? face.normalized : fightHeading, face.normalized, fight.Profile.turnRate * dt, 0);
            FaceSwim(fightHeading, dt, fight.Profile.turnRate);
        }
    }
}
