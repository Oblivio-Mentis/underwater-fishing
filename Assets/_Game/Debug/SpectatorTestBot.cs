using System;
using UnderwaterFishing.Diving;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Debugging
{
    /// <summary>Opt-in development owner inputs; death and revival remain entirely server-driven.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class SpectatorTestBot : MonoBehaviour
    {
        public string Status { get; private set; } = "Disabled";
        private NetworkDiver diver, other;
        private DiverExpedition actor;
        private bool captain, configured, finished, sawDive, sawReturn, wasOutside;
        private bool sawIncapacitated, sawDead, deadConfirmed, restored, requestedReturn;
        private float began = -1, deadAt = -1, unexpectedRevivalAt = -1, nextAction, nextLog;
        private float incapacitatedAt, observedGrace;
        private int waypoint;
        private Vector3 firstWaypoint, deadBodyPosition;
        private string captureDirectory;
        private readonly Vector3 terminalStand = new Vector3(-6, -7, 6.1f);

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "--raid-spectator-bot");
            if ((!Application.isEditor && !UnityEngine.Debug.isDebugBuild) || role < 0 || role + 1 >= args.Length)
            { enabled = false; return; }
            captain = args[role + 1] == "captain";
            configured = captain || args[role + 1] == "victim";
            if (!configured) { enabled = false; return; }
            int capture = Array.IndexOf(args, "--raid-spectator-capture");
            if (capture >= 0 && capture + 1 < args.Length) captureDirectory = args[capture + 1];
            diver = GetComponent<NetworkDiver>(); actor = GetComponent<DiverExpedition>();
            if (diver == null || actor == null) { configured = false; enabled = false; }
        }

        private void Update()
        {
            if (!configured || !diver.IsOwner || !diver.IsClientStarted || finished) return;
            var expedition = NetworkExpedition.Instance;
            if (expedition == null) return;
            float now = Time.unscaledTime;
            var state = expedition.State;
            if (began < 0)
            {
                began = now;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_STARTED role={Role} diver={diver.ObjectId}");
            }
            if (now - began > 140f) { Fail("TIMEOUT", "scenario deadline"); return; }
            if (state.RequiredCrew <= 0) { Move(Vector3.zero, diver.AimOrigin + Vector3.forward); return; }
            if (state.RequiredCrew != 2 || state.DevelopmentMode) { Fail("INVALID", "normal minimum-two session required"); return; }
            FindOther();
            if (state.Phase == ExpeditionPhase.Expedition && !sawDive)
            {
                sawDive = true;
                firstWaypoint = new Vector3(diver.State.Position.x, -7, 0);
                UnityEngine.Debug.Log($"RAID_SPECTATOR_DIVE role={Role} crew={state.Crew} oxygen={diver.State.Diving.Oxygen:0.0}");
            }
            if (state.Phase == ExpeditionPhase.Return) sawReturn = true;
            if (sawDive && !diver.State.Sheltered) wasOutside = true;
            if (captain) ObserveVictim(state, now);
            else ObserveOwnLife(state, now);
            if (finished) return;

            if (sawDive && sawReturn && state.Phase == ExpeditionPhase.Base && state.CompletedDays == 1)
            {
                bool valid = !state.Emergency && sawIncapacitated && sawDead && other != null
                    && diver.State.Diving.Life == DiverLife.Active && other.State.Diving.Life == DiverLife.Active
                    && (captain ? requestedReturn : deadConfirmed && restored && wasOutside);
                if (!valid) { Fail("INVALID", "base return did not satisfy death, camera and revival evidence"); return; }
                UnityEngine.Debug.Log($"RAID_SPECTATOR_PASS role={Role} completed={state.CompletedDays} phase={state.Phase} alive=True emergency=False");
                finished = true; Move(Vector3.zero, diver.AimOrigin + Vector3.forward); return;
            }

            Vector3 movement = Vector3.zero;
            Vector3 look = diver.AimOrigin + Vector3.forward;
            if (captain && (state.Phase == ExpeditionPhase.Base || state.Phase == ExpeditionPhase.Expedition))
            {
                if (sawDive && !diver.State.Sheltered) { Fail("INVALID", "captain left sheltered submarine"); return; }
                movement = Toward(terminalStand); look = expedition.transform.position;
                if (Vector3.Distance(diver.State.Position, terminalStand) < .4f && now >= nextAction)
                {
                    nextAction = now + .5f;
                    if (!sawDive && state.Phase == ExpeditionPhase.Base && state.Crew >= 2) actor.StartExpedition();
                    else if (state.Phase == ExpeditionPhase.Expedition && sawDead && now - deadAt >= 3.5f && !state.ReturnRequested)
                    {
                        actor.RequestReturn(); requestedReturn = true;
                        UnityEngine.Debug.Log("RAID_SPECTATOR_RETURN_REQUESTED victimDead=True");
                    }
                }
            }
            else if (!captain && state.Phase == ExpeditionPhase.Expedition && diver.State.Diving.Life == DiverLife.Active)
            {
                Vector3 point = waypoint == 0 ? firstWaypoint : waypoint == 1 ? new Vector3(11, -7, 0) : new Vector3(12, -10, 0);
                movement = Toward(point); look = point;
                if (waypoint < 2 && Vector3.Distance(diver.State.Position, point) < .55f) waypoint++;
                Status = waypoint < 2 ? "Navigate outside" : "Wait for real oxygen exhaustion";
            }
            Move(movement, look);
            if (now >= nextLog)
            {
                nextLog = now + 3f;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_DIAG role={Role} phase={state.Phase} life={diver.State.Diving.Life} oxygen={diver.State.Diving.Oxygen:0.0} pos={diver.State.Position} sheltered={diver.State.Sheltered} completed={state.CompletedDays}");
            }
        }

        private string Role => captain ? "captain" : "victim";
        private static bool EndPhase(ExpeditionPhase phase) => phase == ExpeditionPhase.Debrief || phase == ExpeditionPhase.Base;
        private void FindOther()
        {
            if (other != null) return;
            foreach (var candidate in NetworkDiver.All)
                if (candidate != diver) { other = candidate; break; }
        }
        private void ObserveVictim(ExpeditionSnapshot expedition, float now)
        {
            if (other == null || !sawDive) return;
            var life = other.State.Diving.Life;
            if (life == DiverLife.Incapacitated && !sawIncapacitated)
            {
                if (other.State.Diving.Oxygen > .05f) { Fail("INVALID", "victim incapacitated with oxygen remaining"); return; }
                sawIncapacitated = true; incapacitatedAt = now; observedGrace = other.State.Diving.IncapacitatedRemaining;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_CAPTAIN_SAW_INCAPACITATED victim={other.ObjectId} oxygen={other.State.Diving.Oxygen:0.0}");
            }
            if (life == DiverLife.Dead && !sawDead)
            {
                if (!sawIncapacitated || expedition.Phase != ExpeditionPhase.Expedition || now - incapacitatedAt < observedGrace - .5f)
                { Fail("INVALID", "server death did not follow incapacity during expedition"); return; }
                sawDead = true; deadAt = now; deadBodyPosition = other.State.Position;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_CAPTAIN_SAW_DEAD victim={other.ObjectId} phase={expedition.Phase} pos={deadBodyPosition} incapacitatedSeconds={now - incapacitatedAt:0.0}");
            }
            if (sawDead && !EndPhase(expedition.Phase))
            {
                if (life != DiverLife.Dead || other.State.Sheltered || Vector3.Distance(other.State.Position, deadBodyPosition) > .25f)
                    Fail("INVALID", "authoritative dead body moved or revived before debrief");
            }
        }
        private void ObserveOwnLife(ExpeditionSnapshot expedition, float now)
        {
            if (!sawDive) return;
            var life = diver.State.Diving.Life;
            if (life == DiverLife.Incapacitated && !sawIncapacitated)
            {
                if (diver.State.Diving.Oxygen > .05f || !wasOutside) { Fail("INVALID", "incapacity was not real oxygen exhaustion outside"); return; }
                sawIncapacitated = true; incapacitatedAt = now; observedGrace = diver.State.Diving.IncapacitatedRemaining;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_INCAPACITATED oxygen={diver.State.Diving.Oxygen:0.0} remaining={diver.State.Diving.IncapacitatedRemaining:0.0}");
            }
            if (life == DiverLife.Dead && !sawDead)
            {
                if (!sawIncapacitated || !wasOutside || expedition.Phase != ExpeditionPhase.Expedition || now - incapacitatedAt < observedGrace - .5f)
                { Fail("INVALID", "owner death did not follow oxygen incapacity during expedition"); return; }
                sawDead = true; deadAt = now; deadBodyPosition = diver.State.Position;
                Status = "Observe dead spectator camera";
            }
            if (sawDead && !EndPhase(expedition.Phase))
            {
                if (life == DiverLife.Active)
                {
                    // Independent SyncVars can arrive one frame apart; the host also checks this authoritatively.
                    if (unexpectedRevivalAt < 0) unexpectedRevivalAt = now;
                    if (now - unexpectedRevivalAt > .5f) Fail("INVALID", "owner revived before debrief");
                }
                else if (diver.State.Sheltered || Vector3.Distance(diver.State.Position, deadBodyPosition) > .25f)
                    Fail("INVALID", "dead owner body moved or respawned underwater");
            }
            if (sawDead && EndPhase(expedition.Phase) && !deadConfirmed)
                Fail("INVALID", "expedition ended before spectator camera was verified");
        }

        private void LateUpdate()
        {
            if (!configured || captain || finished || !diver.IsOwner || !diver.IsClientStarted || !sawDead) return;
            var expedition = NetworkExpedition.Instance;
            if (expedition == null) return;
            if (!deadConfirmed && diver.State.Diving.Life == DiverLife.Dead && Time.unscaledTime - deadAt >= 2f)
            {
                float fromBody = Vector3.Distance(diver.Eyes.transform.position, diver.State.Position);
                float toSurvivor = other == null ? float.PositiveInfinity : Vector3.Distance(diver.Eyes.transform.position, other.State.Position);
                bool cameraEnabled = diver.Eyes.enabled && diver.Eyes.gameObject.activeInHierarchy;
                if (!cameraEnabled || fromBody <= 5f || toSurvivor >= 5f || diver.State.Sheltered
                    || Vector3.Distance(diver.State.Position, deadBodyPosition) > .25f)
                { Fail("INVALID", "dead camera does not follow survivor while owner body stays outside"); return; }
                deadConfirmed = true;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_DEAD_CONFIRMED cameraEnabled=True bodyStationary=True outside=True fromBody={fromBody:0.00} toSurvivor={toSurvivor:0.00} deadSeconds={Time.unscaledTime - deadAt:0.0}");
                Capture("dead");
            }
            if (deadConfirmed && !restored && diver.State.Diving.Life == DiverLife.Active && EndPhase(expedition.State.Phase))
            {
                if (!diver.Eyes.enabled || (diver.Eyes.transform.localPosition - Vector3.up * .6f).magnitude > .15f) return;
                restored = true;
                UnityEngine.Debug.Log($"RAID_SPECTATOR_REVIVED phase={expedition.State.Phase} ownCameraRestored=True localPosition={diver.Eyes.transform.localPosition}");
                Capture("restored");
            }
        }
        private Vector3 Toward(Vector3 point) => Vector3.ClampMagnitude((point - diver.State.Position) * (1.3f / Mathf.Max(.1f, diver.Balance.SwimSpeed)), 1);
        private void Move(Vector3 world, Vector3 look)
        {
            Vector3 delta = look - diver.AimOrigin;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -85, 85);
            diver.SetAutomationInput(Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * world, yaw, pitch, false);
        }
        private void Capture(string phase)
        {
            if (captureDirectory == null) return;
            System.IO.Directory.CreateDirectory(captureDirectory);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, "spectator-" + phase + ".png"));
        }
        private void Fail(string kind, string reason)
        {
            finished = true; Status = "Failed: " + reason;
            Move(Vector3.zero, diver.AimOrigin + Vector3.forward);
            UnityEngine.Debug.LogError($"RAID_SPECTATOR_{kind} role={Role} reason={reason}");
        }
    }
}
