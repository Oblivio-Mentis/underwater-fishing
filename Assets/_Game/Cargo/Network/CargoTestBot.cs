using System;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    /// <summary>Opt-in owner-input hauler. All pickup, deposit and lift decisions go through real interaction RPCs.</summary>
    public sealed class CargoTestBot : MonoBehaviour
    {
        private NetworkDiver diver;
        private DiverCargo cargo;
        private DiverFishing fishing;
        private NetworkLoot target;
        private Vector3 firstPoint;
        private int waypoint, cycles;
        private WinchState previousState;
        private float began = -1, nextRequest, nextLog;
        private bool fishOnly, finished, depositedFish, capturedLift, capturedLoad;
        private string captures;
        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "--raid-cargo-bot");
            if ((!Application.isEditor && !Debug.isDebugBuild) || role < 0 || role + 1 >= args.Length) { enabled = false; return; }
            fishOnly = args[role + 1] == "fish";
            int capture = Array.IndexOf(args, "--raid-cargo-capture");
            if (capture >= 0 && capture + 1 < args.Length) captures = args[capture + 1];
            diver = GetComponent<NetworkDiver>(); cargo = GetComponent<DiverCargo>(); fishing = GetComponent<DiverFishing>();
        }
        private void Update()
        {
            if (!diver.IsOwner || !diver.IsClientStarted || finished) return;
            var station = NetworkCargoStation.Instance;
            if (station == null) return;
            if (!capturedLift && station.State.State == WinchState.Ascending && Vector3.Distance(diver.State.Position, station.State.Position) < 15)
            { capturedLift = true; Capture("ascending"); }
            if (!capturedLoad && station.State.State == WinchState.Deployed && station.State.ContainerCount > 0)
            { capturedLoad = true; Capture("loaded"); }
            if (fishOnly && !fishing.HasCatch && !depositedFish) return;
            if (began < 0) { began = Time.unscaledTime; firstPoint = new Vector3(diver.State.Position.x, -7, 0); if (fishOnly) waypoint = 2; }
            if (!diver.IsActive || Time.unscaledTime - began > 165)
            { Debug.LogError("RAID_CARGO_BOT_TIMEOUT inactive=" + !diver.IsActive); finished = true; return; }
            if (previousState == WinchState.Descending && station.State.State == WinchState.Deployed) cycles++;
            previousState = station.State.State;
            int requiredCycles = fishOnly ? 1 : 2;
            int requiredHold = fishOnly ? 1 : 2;
            if (cycles >= requiredCycles && station.State.HoldCount >= requiredHold && (!fishOnly || depositedFish))
            {
                var ids = new System.Collections.Generic.HashSet<ulong>();
                bool fishSeen = false;
                foreach (var item in station.ProtectedHold) { if (!ids.Add(item.Id)) { Debug.LogError("RAID_CARGO_DUPLICATE"); finished = true; return; } fishSeen |= item.IsFish; }
                if (ids.Count != station.State.HoldCount || (fishOnly && !fishSeen)) return;
                Debug.Log($"RAID_CARGO_BOT_PASS cycles={cycles} hold={ids.Count} fish={fishSeen} unique=true");
                Capture("delivered"); finished = true; Move(Vector3.zero, station.State.Position); return;
            }
            Vector3 movement = Vector3.zero, look = station.State.Position;
            bool request = false;
            if (waypoint < 2)
            {
                Vector3 point = waypoint == 0 ? firstPoint : new Vector3(11, -7, 0);
                movement = Toward(point); look = point;
                if (Vector3.Distance(diver.State.Position, point) < .55f) waypoint++;
            }
            else if (cargo.HasLoot || fishing.HasCatch || fishOnly || station.State.ContainerCount > 0)
            {
                Vector3 point = station.DeployedPosition + Vector3.back * 2.8f;
                movement = Toward(point); look = station.State.Position;
                if (station.State.State == WinchState.Deployed && Vector3.Distance(diver.State.Position, point) < .4f)
                {
                    request = true;
                    if (fishing.HasCatch) depositedFish = true;
                }
            }
            else
            {
                if (target == null || target.State.HolderId >= 0) target = FindLoot();
                if (target != null)
                {
                    look = target.State.Position;
                    Vector3 away = diver.State.Position - target.State.Position;
                    if (away.sqrMagnitude < .01f) away = Vector3.back;
                    Vector3 point = target.State.Position + away.normalized * 1.6f;
                    movement = Toward(point);
                    request = Vector3.Distance(diver.AimOrigin, target.State.Position) < 2.8f;
                }
            }
            Move(movement, look);
            if (request && Time.unscaledTime >= nextRequest) { nextRequest = Time.unscaledTime + .3f; cargo.RequestInteract(); }
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 3;
                Debug.Log($"RAID_CARGO_DIAG pos={diver.State.Position} loot={cargo.HasLoot} fish={fishing.HasCatch} winch={station.State.State} load={station.State.ContainerCount} hold={station.State.HoldCount} cycles={cycles}");
            }
        }
        private NetworkLoot FindLoot()
        {
            NetworkLoot closest = null; float best = float.MaxValue;
            foreach (var loot in NetworkLoot.All)
            {
                if (loot.State.HolderId >= 0) continue;
                float distance = (loot.State.Position - diver.State.Position).sqrMagnitude;
                if (distance < best) { best = distance; closest = loot; }
            }
            return closest;
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
            if (captures == null) return;
            System.IO.Directory.CreateDirectory(captures);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captures, "cargo-" + name + ".png"));
        }
    }
}
