using System;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Network
{
    /// <summary>Development actor for real terminal travel, remote consent and timeout return.</summary>
    public sealed class ExpeditionTestBot : MonoBehaviour
    {
        private NetworkDiver diver;
        private DiverExpedition expeditionActor;
        private bool captain, timeout, finished, sawDive, sawReturn, sawPending, wasOutside;
        private float began = -1, enteredPhase, pendingSince = -1, nextAction, nextLog;
        private int waypoint;
        private ExpeditionPhase previous;
        private Vector3 firstPoint;
        private string captureDirectory;
        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "--raid-expedition-bot");
            if ((!Application.isEditor && !Debug.isDebugBuild) || role < 0 || role + 1 >= args.Length) { enabled = false; return; }
            captain = args[role + 1] == "captain";
            timeout = Array.IndexOf(args, "--raid-expedition-timeout-test") >= 0;
            int capture = Array.IndexOf(args, "--raid-expedition-capture");
            if (capture >= 0 && capture + 1 < args.Length) captureDirectory = args[capture + 1];
            diver = GetComponent<NetworkDiver>(); expeditionActor = GetComponent<DiverExpedition>();
        }
        private void Update()
        {
            if (!diver.IsOwner || !diver.IsClientStarted || finished) return;
            var expedition = NetworkExpedition.Instance;
            if (expedition == null) return;
            var state = expedition.State;
            float now = Time.unscaledTime;
            if (began < 0) { began = now; enteredPhase = now; }
            if (now - began > 110) { Debug.LogError("RAID_EXP_BOT_TIMEOUT"); finished = true; return; }
            if (previous != state.Phase)
            {
                previous = state.Phase; enteredPhase = now; waypoint = 0;
                firstPoint = new Vector3(diver.State.Position.x, -7, 0);
                Capture(state.Phase.ToString());
            }
            if (state.Phase == ExpeditionPhase.Expedition) sawDive = true;
            if (state.Phase == ExpeditionPhase.Return) sawReturn = true;
            if (sawDive && !diver.State.Sheltered) wasOutside = true;
            if (state.ReturnRequested && state.PendingCrew > 0)
            { sawPending = true; if (pendingSince < 0) pendingSince = now; }
            if (sawDive && sawReturn && state.Phase == ExpeditionPhase.Base && state.CompletedDays == 1)
            {
                if (state.Emergency != timeout || (!captain && !wasOutside) || (!timeout && !sawPending))
                { Debug.LogError("RAID_EXP_BOT_INVALID_RETURN"); finished = true; return; }
                Debug.Log($"RAID_EXP_BOT_PASS captain={captain} timeout={timeout} completed={state.CompletedDays} day={state.Day} outside={wasOutside} pending={sawPending}");
                finished = true; Move(Vector3.zero, diver.AimOrigin + Vector3.forward); return;
            }
            Vector3 movement = Vector3.zero, look = diver.AimOrigin + Vector3.forward;
            if (captain && (state.Phase == ExpeditionPhase.Base || state.Phase == ExpeditionPhase.Expedition))
            {
                var stand = new Vector3(-6, -7, 6.1f);
                movement = Toward(stand); look = expedition.transform.position;
                if (Vector3.Distance(diver.State.Position, stand) < .4f && now >= nextAction)
                {
                    nextAction = now + .5f;
                    if (state.Phase == ExpeditionPhase.Base && !sawDive) expeditionActor.StartExpedition();
                    else if (!timeout && now - enteredPhase > 20 && !state.ReturnRequested) expeditionActor.RequestReturn();
                }
            }
            else if (!captain && state.Phase == ExpeditionPhase.Expedition)
            {
                var point = waypoint == 0 ? firstPoint : waypoint == 1 ? new Vector3(11, -7, 0) : new Vector3(12, -10, 0);
                movement = Toward(point); look = point;
                if (waypoint < 2 && Vector3.Distance(diver.State.Position, point) < .55f) waypoint++;
                if (!timeout && state.ReturnRequested && pendingSince > 0 && now - pendingSince > 2 && !expeditionActor.Consented && now >= nextAction)
                { nextAction = now + .5f; expeditionActor.SetReturnConsent(true); }
            }
            Move(movement, look);
            if (now >= nextLog)
            {
                nextLog = now + 3;
                Debug.Log($"RAID_EXP_DIAG captain={captain} phase={state.Phase} pos={diver.State.Position} aboard={diver.State.Sheltered} remaining={state.RemainingSeconds:0} pending={state.PendingCrew}");
            }
        }
        private Vector3 Toward(Vector3 point) => Vector3.ClampMagnitude((point - diver.State.Position) * (1.3f / diver.Balance.SwimSpeed), 1);
        private void Move(Vector3 world, Vector3 point)
        {
            Vector3 delta = point - diver.AimOrigin;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -85, 85);
            diver.SetAutomationInput(Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * world, yaw, pitch, false);
        }
        private void Capture(string phase)
        {
            if (captureDirectory == null) return;
            System.IO.Directory.CreateDirectory(captureDirectory);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, "expedition-" + phase + ".png"));
        }
    }
}
