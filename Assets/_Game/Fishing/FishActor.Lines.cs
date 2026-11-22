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
            public float Distance, Tension, BlockedTime, TugSpeed;
            public Vector3 TowardHand, PullDirection;
            public bool Working, Paired;
        }

        sealed class CrewHooks : FishingHooks<HookLine>
        {
            protected override int GetKeyForItem(HookLine line) => line.OwnerId;
        }

        readonly CrewHooks lines = new();
        HarpoonTool landingTool;
        float fightClock;

        internal bool TryHook(HarpoonTool tool, HarpoonContact contact)
        {
            if (!IsServerInitialized || !_depthActive || _runtime == null || tool == null || tool.Motor.IsHelpless
                || goshaReserved || tool.Owner == null || !tool.Owner.IsActive || tool.HookedFish != null || _carrier != null
                || contact.target != NetworkObject || !contact.Resolve(out _, out var point, out _, out _)) return false;
            bool first = lines.Count == 0;
            if (first && (Mode != FishMode.Roam || DesiredState != FishMode.Roam || Time.time < hookAvailableAt)) return false;
            if (!first && (!RequiresPartner || DesiredState < FishMode.Hooked || DesiredState > FishMode.Tired)) return false;
            float length = Vector3.Distance(tool.HandAnchor, point);
            if (!lines.Attach(new HookLine { OwnerId = tool.OwnerId, Tool = tool, Contact = contact,
                Spool = new FishingLine(length), Tension = .1f })) return false;
            tool.UpdateFishingLine(length, .1f);
            var state = _snapshot.Value;
            if (first)
            {
                DesiredState = FishMode.Hooked;
                fightClock = 0;
                state.Stamina = Mathf.Max(1, tuning.initialStamina * power);
                state.Tension = .1f;
            }
            state.HookCount = lines.Count;
            _snapshot.Value = state;
            return true;
        }

        IEnumerable<string> LandingOwners(HarpoonTool winner)
        {
            var world=RaidWorld.Instance;
            yield return world.ProgressKey(winner.Motor);
            if(!RequiresPartner)yield break;
            HookLine primary=null;
            foreach(var line in lines)if(line.Tool==winner){primary=line;break;}
            if(primary==null)yield break;
            foreach(var line in lines)
                if(line!=primary && line.Working && line.Tension<1 && line.Spool.WindingTime>=tuning.minimumWindingSeconds &&
                    Vector3.Dot(line.PullDirection,primary.PullDirection)>=tuning.pullDirectionDot)
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
                line.TowardHand = tool.HandAnchor - contact;
                line.Distance = line.TowardHand.magnitude;
                line.PullDirection = (tool.HandAnchor - transform.position).normalized;
                bool blocked = SolidAhead(tool.HandAnchor, contact);
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

        void SimulateLine(ref FishSnapshot state, bool tired, float dt)
        {
            SampleLines(dt);
            state.HookCount = lines.Count;
            state.Coordinated = false;
            if (lines.Count == 0) { DesiredState = FishMode.Escaped; return; }
            fightClock += dt;
            float period = Mathf.Max(tuning.warningSeconds + tuning.burstDuration + .1f, tuning.burstPeriod);
            float untilSurge = period - Mathf.Repeat(fightClock, period);
            state.SurgeWarning = !tired && untilSurge <= tuning.warningSeconds;
            state.Surging = !tired && fightClock >= period && Mathf.Repeat(fightClock, period) < tuning.burstDuration;
            HookLine leader = null;
            state.Tension = 0;
            foreach (var line in lines)
            {
                bool working = line.Working && (!RequiresPartner || line.Paired);
                var world=RaidWorld.Instance;
                float teamTiming=line.Paired?(world?.SkillFactor(line.Tool.Motor,SkillId.H4)??0):0;
                float reel=Mathf.Clamp(line.Tool.ReelMultiplier*(1+teamTiming),.1f,10);
                float lineStrength=1+(world?.SkillFactor(line.Tool.Motor,SkillId.H2)??0);
                float resistance = state.Surging ? tuning.burstTensionMultiplier : 1;
                if (working)
                {
                    line.Spool.Wind(line.Distance, tuning.slackAllowance, tuning.reelSpeed * reel, dt);
                    state.Coordinated |= line.Paired;
                    if (leader == null) leader = line;
                }
                else if (!line.Working || !line.Tool.Reeling) line.Spool.Slip(line.Distance);
                bool strained = line.Working || (state.SurgeWarning && line.Spool.Loaded(line.Distance, tuning.slackAllowance));
                if (strained)
                    line.Tension += tuning.tensionRise * resistance * power * dt / (lineStrength*(line.Paired ? 1 + tuning.assistBonus : 1));
                else line.Tension -= tuning.tensionRelease * dt;
                line.Tension = Mathf.Clamp01(line.Tension);
                state.Tension = Mathf.Max(state.Tension, line.Tension);
                line.Tool.UpdateFishingLine(line.Spool.PaidOut, line.Tension);
                float tug = state.Surging && line.BlockedTime == 0 && line.Spool.Loaded(line.Distance, tuning.slackAllowance)
                    ? Mathf.Min(tuning.maxTugSpeed, tuning.tugSpeed * power * (line.Paired ? .24f : 1)) : 0;
                line.TugSpeed = Mathf.MoveTowards(line.TugSpeed, tug, tuning.tugAcceleration * dt);
                if (line.TugSpeed > 0 && line.Distance > tuning.captureRange)
                {
                    Vector3 tugMotion = -line.TowardHand.normalized * line.TugSpeed * dt * (1-(world?.SkillFactor(line.Tool.Motor,SkillId.H3)??0));
                    float verticalLimit = Mathf.Max(0, line.Tool.Motor.tuning.verticalSpeed) * dt;
                    tugMotion.y = Mathf.Clamp(tugMotion.y, -verticalLimit, verticalLimit);
                    // Sweep the controller while exempting forced ascent from the swimmer's own ascent penalty.
                    line.Tool.Motor.ApplyTetherMotion(tugMotion);
                }
            }

            if (leader != null)
            {
                float speed = tuning.reelSpeed * Mathf.Clamp(leader.Tool.ReelMultiplier, .1f, 10)
                    * (state.Surging ? .25f : 1) * (leader.Paired ? 1 + tuning.assistBonus + (RaidWorld.Instance?.SkillFactor(leader.Tool.Motor,SkillId.H4)??0) : 1);
                // Move only the excess separation. A diver swimming towards a slack line cannot pull the fish.
                float excess = Mathf.Max(0, leader.Distance - leader.Spool.PaidOut);
                // Once exhausted, tow the body rather than steering on the off-centre
                // implant as it swings across the surface during animation.
                Vector3 pull=tired?leader.PullDirection:leader.TowardHand.normalized;
                Vector3 motion = pull * Mathf.Min(excess, speed * dt);
                if (!SolidAhead(transform.position, transform.position + motion)) transform.position += motion;
                if (Nereid.NereidVessel.Active != null)
                {
                    Vector3 away = -leader.PullDirection;
                    Vector3 lateral = Vector3.Cross(Vector3.up,away);
                    FaceSwim(away + lateral * Mathf.Sin(fightClock * (state.Surging ? 8 : 4)) * (state.Surging ? .45f : .16f),dt,1.5f);
                }
                state.Stamina = Mathf.Max(0, state.Stamina - tuning.exhaustionRate * dt * (leader.Paired ? 1 + tuning.assistBonus : 1));
                bool partnerReady = !RequiresPartner;
                foreach (var other in lines)
                    if (other != leader && other.Working && other.Tension < 1 && other.Spool.WindingTime >= tuning.minimumWindingSeconds
                        && Vector3.Dot(other.PullDirection, leader.PullDirection) >= tuning.pullDirectionDot)
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
                var lead = lines.At(0);
                float escape = tuning.escapeSpeed * power * (state.Surging ? 2 : tired ? .10f : 1);
                Vector3 away = -lead.PullDirection;
                if (away.sqrMagnitude < .01f) away = transform.forward;
                if (Nereid.NereidVessel.Active != null)
                {
                    Vector3 lateral = Vector3.Cross(Vector3.up,away);
                    away += lateral * Mathf.Sin(fightClock * 3.8f) * (state.Surging ? .48f : .17f);
                }
                Vector3 dir = ClearHeading(transform.position, away.normalized, 1);
                Vector3 move = dir * (escape * dt);
                if (!SolidAhead(transform.position, transform.position + move)) transform.position += move;
                FaceSwim(dir,dt,1.75f);
                if (!tired) state.Stamina = Mathf.Min(tuning.initialStamina * power, state.Stamina + tuning.staminaRecovery * dt);
            }
        }
    }
}
