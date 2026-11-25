#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using FishNet.Managing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    // Explicit local fixture for RAID-024 phase A. It arms only while the marker file exists in the
    // project folder, hosts the session itself, gives the team real ranks through the normal
    // team-manifest path and records what the server actually did. No production behaviour is bypassed:
    // the pet hunts the same live FishActor the divers can shoot.
    internal sealed class GoshaHuntTrial : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public string status = "running", phase = "", error = "";
            public string species = "";
            public long cargoId;
            public float fishMovedWhileChased, fishVisibilityMoved, chaseSeconds, deadline;
            public int petModeBefore, petModeAfter;
            public bool fishRoamingWhileChased, fishStartledWhileChased, captured, deliveredToPockets, sameId, petLeftDeck;
            public int carriedAfterCatch, pocketsWithId, shots, materialsDelivered, fishDelivered;
            public float petDistance;
            public string[] checks = Array.Empty<string>();
            public Vector3 fishStart, diverAt;
        }

        const string Marker = "/../.studioigor/gosha-trial.on";
        const string Output = "/../.studioigor/raid024";

        Report report = new Report();
        readonly System.Collections.Generic.List<string> checks = new();
        RaidWorld world;
        DiverMotor diver;
        FishActor target;
        Vector3 lastFish;
        float lastFishMoveAt;

        static string OutputFolder => Path.GetFullPath(Application.dataPath + Output);
        static string MarkerPath => Path.GetFullPath(Application.dataPath + Marker);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!File.Exists(MarkerPath)) return;
            Directory.CreateDirectory(OutputFolder);
            File.Delete(Path.Combine(OutputFolder, "hunt.json"));
            var go = new GameObject("RAID-024 Gosha hunt trial");
            DontDestroyOnLoad(go);
            go.AddComponent<GoshaHuntTrial>();
        }

        void Awake() => StartCoroutine(Drive());

        // Exceptions from the checks end the run as a recorded failure instead of an unhandled log line.
        IEnumerator Drive()
        {
            var task = Run();
            while (true)
            {
                object current;
                try
                {
                    if (!task.MoveNext()) break;
                    current = task.Current;
                }
                catch (Exception error)
                {
                    report.status = "failed";
                    report.error = error.ToString();
                    Save();
                    Debug.LogWarning("[RAID-024] Gosha hunt trial failed: " + error.Message);
                    yield break;
                }
                yield return current;
            }
        }

        void Update()
        {
            if (target == null) return;
            Vector3 at = target.transform.position;
            float moved = Vector3.Distance(at, lastFish);
            if (moved > .0005f) lastFishMoveAt = Time.time;
            lastFish = at;
        }

        IEnumerator Run()
        {
            {
                yield return Wait("host and world", () =>
                {
                    var session = FindAnyObjectByType<RaidSession>();
                    if (session != null && session.manager != null && session.manager.ServerManager != null &&
                        !session.manager.ServerManager.Started && !session.Busy) session.HostLocal();
                    world = RaidWorld.Instance;
                    return world != null && world.NetworkObject != null && world.IsServerInitialized && world.Team != null;
                }, 90);
                yield return Wait("expedition ground", () => world.TravelRemaining <= 0 && world.CurrentDepth == 1, 60);
                yield return Wait("a free small fish", () => FishActor.ServerSchool.Any(f => f != null && f.IsAvailableForGosha), 90);
                yield return Wait("the owner diver", () => DiverMotor.ServerDivers.Any(d => d != null && d.IsOwner && d.IsServerInitialized), 60);
                diver = DiverMotor.ServerDivers.First(d => d.IsOwner);
                target = FishActor.ServerSchool.Where(f => f.IsAvailableForGosha)
                    .OrderBy(f => (f.transform.position - diver.transform.position).sqrMagnitude).First();
                report.species = target.SpeciesId; report.cargoId = target.CargoId;
                report.fishStart = target.transform.position;
                var at = target.transform.position;
                diver.Teleport(at + new Vector3(1.4f, .9f, 1.2f));
                report.diverAt = diver.transform.position;
                yield return new WaitForSeconds(.4f);
                world.StageWorkForTrial(GoshaWork.Fish, 2, 4, 1, 0, 1);
                report.petModeBefore = (int)world.Gosha.Mode;
                float started = Time.time;
                lastFish = target.transform.position;
                bool sawChase = false, stunned = false;
                bool shot = false;
                while (Time.time - started < 90f)
                {
                    if (world.Gosha.Mode == GoshaMode.Fishing || world.Gosha.Mode == GoshaMode.BringingFish) sawChase = true;
                    var hunted = world.HuntTarget;
                    if (hunted != null)
                    {
                        // Follow whichever fish the server actually picked; the choice is Gosha's, not the fixture's.
                        if (hunted != target) { target = hunted; report.species = target.SpeciesId; report.fishStart = target.transform.position; lastFish = target.transform.position; lastFishMoveAt = Time.time; }
                        report.fishRoamingWhileChased |= target.Mode == FishMode.Roam && target.IsFreeSwimming;
                        report.fishStartledWhileChased |= target.Startled;
                        if (Time.time - lastFishMoveAt > .8f) stunned = true;
                        if (!shot && Screen.width > 0 && Vector3.Distance(world.Gosha.Position, target.transform.position) < 4f)
                        { shot = true; diver.Teleport(target.transform.position + new Vector3(2.2f, 1.1f, 2.2f)); diver.LookAtConsole(target.transform.position); ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, "chase.png")); }
                    }
                    if (world.CarriedCount > 0) break;
                    yield return null;
                }
                report.chaseSeconds = Time.time - started;
                report.captured = world.CarriedCount > 0;
                report.carriedAfterCatch = world.CarriedCount;
                report.fishMovedWhileChased = Vector3.Distance(target.transform.position, report.fishStart);
                report.petModeAfter = (int)world.Gosha.Mode;
                report.petDistance = Vector3.Distance(world.Gosha.Position, target.transform.position);
                Check("the fish swims while it is the hunt target", sawChase && report.fishRoamingWhileChased);
                report.cargoId = target != null ? target.CargoId : 0;
                Check("the fish is startled by the hunter", report.fishStartledWhileChased);
                Check("no freeze: the fish keeps changing position", !stunned && report.fishMovedWhileChased > .25f);
                Check("capture only after a real chase", report.captured && report.chaseSeconds > .35f);
                Check("the pet left the hull through the lock", world.Gosha.Mode == GoshaMode.Fishing);
                if (!report.captured) { Finish(); yield break; }
                // Stand in the water near the pet with free hands so the hand-over has a real receiver.
                // The pet's own position is open water; stand just above it with free hands.
                Vector3 Receiver() => world.Gosha.Position + Vector3.up * .9f + (world.Gosha.Heading.sqrMagnitude > .01f ? world.Gosha.Heading.normalized : Vector3.forward) * 1.1f;
                diver.Teleport(Receiver());
                float caught = Time.time;
                while (Time.time - caught < 60f)
                {
                    var tool = diver.GetComponent<HarpoonTool>();
                    report.pocketsWithId = Enumerable.Range(2, tool.PocketCapacity).Count(i => tool.CatchIdInSlot(i) == report.cargoId);
                    if (report.pocketsWithId > 0 || world.Gosha.Mode == GoshaMode.GoingHome || world.Gosha.Mode == GoshaMode.Resting) break;
                    if (Vector3.Distance(diver.transform.position, world.Gosha.Position) > 6f) diver.Teleport(Receiver());
                    yield return null;
                }
                report.sameId = report.pocketsWithId == 1;
                report.deliveredToPockets = report.pocketsWithId == 1;
                Check("the same cargo id lands in the diver's pocket once", report.sameId);
                yield return Logistics();
                Finish();
            }
        }

        // Phase B/C: a real winch drum off the seabed, loaded by the pet into the open cage; a second
        // fish straight into the aquarium through the pet hatch. Every id is counted once.
        IEnumerator Logistics()
        {
            world.StopWorkForTrial();
            yield return new WaitForSeconds(.5f);
            // Materials: Scent I, Delivery III (cage), capacity 2.
            world.StageWorkForTrial(GoshaWork.Materials, 3, 5, 2, 2, 3);
            diver.Teleport(NereidVessel.Active.transform.TransformPoint(new Vector3(9f, -4f, -16f)));
            var loot = FindObjectsByType<LootActor>().FirstOrDefault(l => l.IsFreeOnSeabed && l.tier == 1);
            Check("a real tier-1 salvage item lies on the seabed", loot != null);
            long lootId = loot.CargoId;
            int cageBefore = world.Team.cargo.Count;
            yield return Wait("the pet picked the drum up off the seabed", () => loot.CarriedByGosha, 120);
            Check("the drum keeps its id while carried", loot.CargoId == lootId && loot.IsVisible);
            yield return Wait("the drum entered the open cage manifest", () => world.Team.cargo.Any(x => x.id == lootId && x.IsSalvage), 120);
            Check("one manifest entry and the seabed item is gone", world.Team.cargo.Count(x => x.id == lootId) == 1 && !loot.IsVisible && world.Team.cargo.Count == cageBefore + 1);
            report.materialsDelivered = world.Gosha.MaterialsDelivered;
            world.StopWorkForTrial();
            yield return new WaitForSeconds(.5f);
            // Hatch: Delivery V, fish go straight to the tank.
            int tankBefore = world.Team.tank.Count;
            uint hatchBefore = world.Gosha.HatchDeliveries;
            world.StageWorkForTrial(GoshaWork.Fish, 4, 6, 3, 0, 5);
            diver.Teleport(NereidVessel.Active.transform.TransformPoint(new Vector3(9f, -4f, -16f)));
            yield return Wait("a fish passed the pet hatch into the real aquarium", () => world.Gosha.HatchDeliveries > hatchBefore, 150);
            long hatched = world.Gosha.HatchCargoId;
            Check("the hatched fish is in the tank exactly once and not in the cage", world.Team.tank.Count(x => x.id == hatched) == 1 && !world.Team.cargo.Any(x => x.id == hatched) && world.Team.tank.Count == tankBefore + 1);
            Check("the tank holds no salvage", world.Team.tank.All(x => !x.IsSalvage));
            report.fishDelivered = world.Gosha.FishDelivered;
            report.status = "running";
            world.StopWorkForTrial();
        }

        void Check(string message, bool ok)
        {
            if (!ok) throw new InvalidOperationException(message);
            checks.Add(message);
            report.checks = checks.ToArray();
            Save();
        }

        IEnumerator Wait(string phase, Func<bool> ready, float seconds)
        {
            report.phase = phase; Save();
            float until = Time.time + seconds;
            bool Ready()
            {
                // Network objects are not initialised on the first frames; a predicate that trips there just waits.
                try { return ready(); }
                catch (NullReferenceException) { return false; }
            }
            while (!Ready())
            {
                if (Time.time > until) throw new TimeoutException(phase + " · pet " + world?.Gosha.Mode + " at " + world?.Gosha.Position);
                yield return null;
            }
        }

        void Finish()
        {
            report.status = "passed";
            report.phase = "done";
            Save();
            Debug.Log("[RAID-024] Gosha hunt trial passed: " + string.Join(" | ", checks));
        }

        void Save()
        {
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllText(Path.Combine(OutputFolder, "hunt.json"), JsonUtility.ToJson(report, true));
        }
    }
}
#endif
