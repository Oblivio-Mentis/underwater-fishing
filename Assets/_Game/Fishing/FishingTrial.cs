#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;

namespace Raid
{
    /// <summary>Explicit editor-host verification against an independent development player using its local UI probe.</summary>
    public sealed class FishingTrial : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string phase, status, error;
            public int host, client;
            public List<string> checks = new();
        }

        private readonly Result result = new();
        private string clientDir, reportPath;
        private RaidWorld world;
        private HarpoonTool host, helper;
        private float nextGrip;
        private bool refreshGrip;

        public static void Begin(string remoteProbeDirectory, string resultPath)
        {
            if (!Application.isPlaying || FindAnyObjectByType<FishingTrial>() != null)
                throw new InvalidOperationException("Begin the trial once on a running editor host.");
            if (!Directory.Exists(remoteProbeDirectory)) throw new DirectoryNotFoundException(remoteProbeDirectory);
            var trial = new GameObject("Two-peer fishing verification").AddComponent<FishingTrial>();
            trial.clientDir = Path.GetFullPath(remoteProbeDirectory);
            trial.reportPath = Path.GetFullPath(resultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(trial.reportPath));
            trial.StartCoroutine(trial.Verify());
        }

        private IEnumerator Verify()
        {
            // Flatten nested waits so failures in a sub-step also reach the reproducible report.
            var tasks = new Stack<IEnumerator>();
            tasks.Push(Run());
            result.status = "running";
            while (tasks.Count > 0)
            {
                object instruction = null;
                try
                {
                    var task = tasks.Peek();
                    if (!task.MoveNext()) { tasks.Pop(); continue; }
                    if (task.Current is IEnumerator child) { tasks.Push(child); continue; }
                    instruction = task.Current;
                }
                catch (Exception error)
                {
                    result.status = "failed";
                    result.error = error.Message;
                    break;
                }
                yield return instruction;
            }
            if (result.status == "running") result.status = "passed";
            if (host != null) host.QaHoldOwned(false, 0);
            if (helper != null)
            {
                try { Send("fish_grip", 0); } catch (IOException) { }
            }
            Write();
            Debug.Log($"[FishingTrial] {result.status} / {result.phase}: {result.error} ({reportPath})");
            Destroy(gameObject);
        }

        private IEnumerator Run()
        {
            Phase("Connect host and independent client");
            world = RaidWorld.Instance;
            Require(world != null && world.IsServerInitialized && world.IsClientInitialized, "Editor must be a network host.");
            var crew = DiverMotor.ServerDivers.Where(d => d != null && d.IsServerInitialized).ToArray();
            Require(crew.Length == 2, "Exactly two spawned divers required.");
            host = crew.Single(d => d.IsOwner).GetComponent<HarpoonTool>();
            helper = crew.Single(d => !d.IsOwner).GetComponent<HarpoonTool>();
            result.host = host.OwnerId;
            result.client = helper.OwnerId;
            var hello = Send("snapshot", 0);
            yield return Wait(() => ReadClient()?.request == hello, 12, "Remote development probe did not acknowledge this trial.");
            var remote = ReadClient();
            Require(remote != null && remote.owner == helper.OwnerId && remote.connected && remote.players == 2 && !remote.inputBlocked,
                "Remote must be connected, playing, and own the other diver.");
            Require(!UiInputGate.Blocked && !host.Motor.IsHelpless && !helper.Motor.IsHelpless, "Both peers must be alive with gameplay controls.");
            Require(host.CatchCount == 0 && host.HookedFish == null && !host.TetherActive, "Host must start with free hands and empty catch pockets.");
            Check("Distinct connected owners " + host.OwnerId + " and " + helper.OwnerId);

            if (world.AtBase)
            {
                Phase("Choose shallow expedition at chart console");
                var station = ShipConsole.Stations.First(s => s.kind == ShipConsoleKind.Depths);
                helper.Motor.Teleport(new Vector3(1, 1.2f, 3));
                bool aligned = false;
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    host.Motor.Teleport(station.transform.position + side * 1.45f + Vector3.up*.5f);
                    Physics.SyncTransforms();
                    if (host.Motor.IsInside && ShipConsole.CanUse(host.Motor, WorldAction.DiveOne)) { aligned = true; break; }
                }
                Require(aligned && helper.Motor.IsInside, "Could not reach the chart console from its dry interior.");
                world.Command(WorldAction.DiveOne, 0);
                yield return Wait(() => !world.AtBase, 12, "Dive command refused: " + world.Message);
            }
            Require(world.CurrentDepth == 1 && world.Team.hoistPhase == HoistPhase.Loading, "Shallow depth and available winch required.");
            Require(world.Team.Capacity(world.progression) - world.Team.CargoUsed >= 6, "The verified catches occupy six cage units.");
            host.SelectSlot(0);
            yield return Wait(() => host.ActiveSlot == 0, 3, "Host harpoon could not be equipped.");

            foreach (var species in new[] { "Needlefish", "Lanternfish", "Needlefish" })
            {
                Phase("Solo capture / " + species);
                var fish = Fish(species);
                yield return Wait(() => fish.Mode == FishMode.Roam, 16, "School did not repopulate " + species + " while the first catch was stowed.");
                yield return Hook(fish);
                yield return Reel(fish);
                Require(!host.HandsFull && host.HookedFish == null && host.CarriedFish != null,
                    "Small catch was not transferred independently of the harpoon.");
                Check(species + " captured and stowed by one diver.");
            }
            Require(host.FishInSlot(2)?.SpeciesId == "Needlefish" && host.FishInSlot(3)?.SpeciesId == "Lanternfish"
                && host.FishInSlot(4)?.SpeciesId == "Needlefish" && host.HasFullCreel && host.CatchIdInSlot(2) != host.CatchIdInSlot(4),
                "All three pockets must preserve independent identities, including two catches from the same shoal.");
            host.SelectSlot(2);
            yield return Wait(() => host.ActiveSlot == 2, 3, "Occupied pocket was not selectable.");
            Require(!host.QaFireOwned(), "Occupied pocket incorrectly fired the harpoon.");
            yield return Pause(.4f);
            Shot("catch-pockets.png");
            host.SelectSlot(0);
            yield return Wait(() => host.ActiveSlot == 0, 3, "Could not return to the harpoon.");
            Check("Three loaded pockets include two distinct catches from one repopulating shoal; selecting a fish suppresses harpoon fire.");

            Phase("Heavy solo attempt");
            var heavy = Fish("Bristlefish");
            yield return Hook(heavy);
            yield return Wait(() => heavy.Mode == FishMode.Struggling, 4, "Heavy fish did not enter its struggle.");
            float initial = heavy.Stamina;
            Require(host.QaHoldOwned(false, 2), "Host could not hold reel.");
            yield return Pause(1.5f);
            Require(!host.IsAssisted && host.HookedFish == heavy && heavy.Mode == FishMode.Struggling &&
                Mathf.Abs(heavy.Stamina - initial) < .025f, "Unassisted reeling reduced heavy stamina or changed state.");
            host.QaHoldOwned(false, 0);
            Check("Heavy fish resisted a sustained solo reel at stamina " + initial.ToString("F2") + ".");

            Phase("Remote diver grips shooter");
            helper.Motor.Teleport(host.transform.position - host.Motor.AimDirection*1.45f);
            Physics.SyncTransforms();
            Send("fish_grip", 15);
            nextGrip = Time.unscaledTime + 5;
            refreshGrip = true;
            yield return Wait(() => host.AssistingDiver == helper.Motor && helper.AssistTarget == host.Motor &&
                ReadClient()?.assistedOwner == host.OwnerId, 8, "No mutually observed physical grip.");
            Check("Remote controls initiated grip; both peers observed the same shooter.");
            Phase("Cooperative pull and heavy carry");
            yield return Reel(heavy);
            refreshGrip = false;
            Send("fish_grip", 0);
            Require(host.HandsFull && host.CarriedFish == heavy && host.CatchCount == 4,
                "Paired capture lost a pocket or failed to occupy both hands.");
            Require(!host.SelectSlot(1) && host.ActiveSlot == 0 && !host.QaFireOwned(), "Heavy catch allowed switching or firing.");
            yield return Pause(.5f);
            Shot("catch-heavy.png");
            Check("Heavy capture completed with all three small catches intact; tool switch and fire blocked.");

            Phase("Deposit and observe swimming cargo on both peers");
            host.Motor.Teleport(world.tuning.cargoPosition - host.Motor.AimDirection * 2.65f - Vector3.up * .6f);
            Physics.SyncTransforms();
            var cage = world.container.GetComponentInChildren<WetCargoShoal>();
            int before = world.Team.cargo.Count, beforeSwimmers = cage == null ? 0 : cage.Count;
            var caughtIds = new[] { host.CatchIdInSlot(2), host.CatchIdInSlot(3), host.CatchIdInSlot(4), heavy.CargoId };
            world.Command(WorldAction.LoadCargo, 0);
            yield return Wait(() => host.CatchCount == 0 && world.Team.cargo.Count == before + 4, 6, "Server did not deposit all four catches.");
            Require(caughtIds.All(id => world.Team.cargo.Count(item => item.id == id) == 1), "Deposit lost or duplicated a catch ID.");
            yield return Wait(() => cage != null && cage.Count == beforeSwimmers + 4 && ReadClient()?.cageSwimmers == cage.Count,
                10, "Host/client cage swimmers did not reflect four new entries.");
            Shot("catch-cage.png");
            Check("Four authoritative catch IDs now swim in both cages; hands and pockets are empty.");

            Phase("Hoist cargo into reservoir");
            yield return Pause(.3f);
            world.Command(WorldAction.Hoist, 0);
            yield return Wait(() => world.Team.hoistPhase != HoistPhase.Loading, 4, "Winch command refused.");
            yield return Wait(() => world.Team.hoistPhase == HoistPhase.Loading && caughtIds.All(id => world.Team.tank.Exists(item => item.id == id))
                && cage.Count == 0 && ReadClient()?.cageSwimmers == 0, 40, "Hoist did not deliver and clear both visible cages.");
            Check("Winch delivered each original ID to the reservoir and both cages emptied.");
        }

        private FishActor Fish(string species) => FindObjectsByType<FishActor>().Single(f => f.depthTier == 1 && f.SpeciesId == species);

        private IEnumerator Hook(FishActor fish)
        {
            Require(fish.Mode == FishMode.Roam && !host.Motor.IsHelpless, "Fish unavailable or host incapacitated.");
            Vector3 aim = host.Motor.AimDirection;
            host.Motor.Teleport(fish.transform.position - aim * 3.8f - Vector3.up * .6f);
            Physics.SyncTransforms();
            Require(Physics.Raycast(host.transform.position + Vector3.up*.6f, aim, out var hit, host.tuning.maxRange,
                host.tuning.hitMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FishActor>() == fish,
                "Fixture ray blocked before " + fish.SpeciesId + ": " + (hit.collider == null ? "miss" : hit.collider.name));
            Require(host.QaFireOwned(), "Host owned fire request rejected.");
            yield return Wait(() => host.HookedFish == fish, 3, "Normal harpoon RPC missed " + fish.SpeciesId + ".");
        }

        private IEnumerator Reel(FishActor fish)
        {
            bool pulling = true;
            float deadline = Time.unscaledTime + 65;
            long capturedId = fish.CargoId;
            while (fish.RequiresPartner ? fish.Mode != FishMode.Captured :
                host.CatchIdInSlot(2) != capturedId && host.CatchIdInSlot(3) != capturedId && host.CatchIdInSlot(4) != capturedId)
            {
                Require(Time.unscaledTime < deadline && host.HookedFish == fish && !host.Motor.IsHelpless,
                    "Line lost or expired: " + fish.SpeciesId + " / " + fish.Mode + " / stamina " + fish.Stamina.ToString("F2"));
                if (fish.Tension >= .58f) pulling = false;
                else if (fish.Tension < .24f) pulling = true;
                host.QaHoldOwned(false, pulling ? .6f : 0);
                if (refreshGrip && Time.unscaledTime >= nextGrip)
                {
                    Send("fish_grip", 15);
                    nextGrip = Time.unscaledTime + 5;
                }
                yield return null;
            }
            host.QaHoldOwned(false, 0);
        }

        private string Send(string action, float value)
        {
            var request = new UiQaProbe.Request { id=Guid.NewGuid().ToString("N"), action=action, number=value };
            string path=Path.Combine(clientDir,"request.json"), temp=path+"."+request.id+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(request));
            if(File.Exists(path)) File.Replace(temp,path,null);
            else File.Move(temp,path);
            return request.id;
        }

        private UiQaProbe.Snapshot ReadClient()
        {
            try
            {
                var path=Path.Combine(clientDir,"state.json");
                return File.Exists(path) ? JsonUtility.FromJson<UiQaProbe.Snapshot>(File.ReadAllText(path)) : null;
            }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
        }

        private static IEnumerator Pause(float duration) => WaitForDuration(duration);

        private static IEnumerator WaitForDuration(float duration)
        {
            float until=Time.unscaledTime+duration;
            while(Time.unscaledTime<until) yield return null;
        }

        private static IEnumerator Wait(Func<bool> done,float timeout,string error)
        {
            float until=Time.unscaledTime+timeout;
            while(!done())
            {
                Require(Time.unscaledTime<until,error);
                yield return null;
            }
        }

        private static void Require(bool value,string error) { if(!value) throw new InvalidOperationException(error); }
        private void Phase(string phase) { result.phase=phase; Write(); }
        private void Check(string message) { result.checks.Add(message); Write(); }
        private void Write() => File.WriteAllText(reportPath,JsonUtility.ToJson(result,true));
        private void Shot(string filename) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(reportPath),filename));
    }
}
#endif
