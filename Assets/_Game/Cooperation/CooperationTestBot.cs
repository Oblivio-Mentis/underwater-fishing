using System;
using UnderwaterFishing.Diving;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cooperation
{
    /// <summary>Development actor: swims with ordinary owner input and holds the real rescue RPC.</summary>
    public sealed class CooperationTestBot : MonoBehaviour
    {
        private NetworkDiver diver;
        private DiverRescue rescue;
        private bool helper, finished, sawPinned, sawReleased, sawIncapacitated;
        private int waypoint;
        private float began = -1, nextAction, nextLog;
        private Vector3 firstPoint;
        private string captureDirectory;
        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "--raid-cooperation-bot");
            if ((!Application.isEditor && !Debug.isDebugBuild) || role < 0 || role + 1 >= args.Length) { enabled = false; return; }
            helper = args[role + 1] == "helper";
            int capture = Array.IndexOf(args, "--raid-cooperation-capture");
            if (capture >= 0 && capture + 1 < args.Length) captureDirectory = args[capture + 1];
            diver = GetComponent<NetworkDiver>(); rescue = GetComponent<DiverRescue>();
        }
        private void Update()
        {
            if (!diver.IsOwner || !diver.IsClientStarted || finished) return;
            if (began < 0) { began = Time.unscaledTime; firstPoint = new Vector3(diver.State.Position.x, -7, 0); }
            if (Time.unscaledTime - began > 95) { Debug.LogError("RAID_COOP_BOT_TIMEOUT"); finished = true; return; }
            NetworkDiver victim = diver;
            if (helper)
            {
                victim = null;
                foreach (var other in NetworkDiver.All) if (other != diver) { victim = other; break; }
                if (victim == null) { Move(Vector3.zero, diver.AimOrigin + Vector3.forward); return; }
            }
            if (victim.IsPinned && !sawPinned) { sawPinned = true; Capture("pinned"); }
            if (sawPinned && !victim.IsPinned) sawReleased = true;
            if (victim.State.Diving.Life == DiverLife.Incapacitated) sawIncapacitated = true;
            if (sawReleased && sawIncapacitated && victim.State.Diving.Life == DiverLife.Active)
            {
                Debug.Log($"RAID_COOP_BOT_PASS role={(helper ? "helper" : "victim")} trapReleased=true oxygenRescued=true");
                Capture("rescued"); finished = true; Move(Vector3.zero, victim.AimOrigin + Vector3.forward); return;
            }
            bool interact = false, help = false;
            Vector3 movement = Vector3.zero, look = diver.AimOrigin + Vector3.forward;
            if (helper && !sawPinned) { Move(movement, look); return; }
            if (waypoint < 2)
            {
                Vector3 point = waypoint == 0 ? firstPoint : new Vector3(11, -7, 0);
                movement = Toward(point); look = point;
                if (Vector3.Distance(diver.State.Position, point) < .55f) waypoint++;
            }
            else if (helper)
            {
                Vector3 stand = victim.State.Position + new Vector3(-2, .1f, -.4f);
                movement = Toward(stand); look = victim.AimOrigin;
                interact = victim.IsPinned;
                help = victim.State.Diving.Life == DiverLife.Incapacitated;
            }
            else if (!sawPinned)
            { var point = new Vector3(16, -12, 10); movement = Toward(point); look = point; }
            else { help = victim.IsPinned || victim.State.Diving.Life == DiverLife.Incapacitated; }
            Move(movement, look);
            if (Time.unscaledTime >= nextAction) { nextAction = Time.unscaledTime + .1f; rescue.Actions(interact, help); }
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 2;
                Debug.Log($"RAID_COOP_DIAG helper={helper} waypoint={waypoint} pos={diver.State.Position} oxygen={diver.State.Diving.Oxygen:0.0} victim={victim.State.Diving.Life} pinned={victim.IsPinned} progress={rescue.State.Progress:0.00}");
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
        private void Capture(string name)
        {
            if (captureDirectory == null) return;
            System.IO.Directory.CreateDirectory(captureDirectory);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, "cooperation-" + name + ".png"));
        }
    }
}
