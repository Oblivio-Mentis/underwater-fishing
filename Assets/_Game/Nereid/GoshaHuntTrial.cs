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
            public int carriedAfterCatch, pocketsWithId, shots;
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
                while (Time.time - started < 60f)
                {
                    if (world.Gosha.Mode == GoshaMode.Fishing || world.Gosha.Mode == GoshaMode.BringingFish) sawChase = true;
                    if (world.HuntTarget == target)
                    {
                        report.fishRoamingWhileChased |= target.Mode == FishMode.Roam && target.IsFreeSwimming;
                        report.fishStartledWhileChased |= target.Startled;
                        if (Time.time - lastFishMoveAt > .8f) stunned = true;
                        if (Screen.width > 0) ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, "chase.png"));
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
                Check("the fish is startled by the hunter", report.fishStartledWhileChased);
                Check("no freeze: the fish keeps changing position", !stunned && report.fishMovedWhileChased > .25f);
                Check("capture only after a real chase", report.captured && report.chaseSeconds > .35f);
                Check("the pet left the hull through the lock", world.Gosha.Mode == GoshaMode.Fishing);
                if (!report.captured) { Finish(); yield break; }
                report.cargoId = target.CargoId;
                float caught = Time.time;
                while (Time.time - caught < 60f)
                {
                    var tool = diver.GetComponent<HarpoonTool>();
                    report.pocketsWithId = Enumerable.Range(2, tool.PocketCapacity).Count(i => tool.CatchIdInSlot(i) == report.cargoId);
                    if (report.pocketsWithId > 0 || world.Gosha.Mode == GoshaMode.GoingHome || world.Gosha.Mode == GoshaMode.Resting) break;
                    yield return null;
                }
                report.sameId = report.pocketsWithId == 1;
                report.deliveredToPockets = report.pocketsWithId == 1;
                Check("the same cargo id lands in the diver's pocket once", report.sameId);
                Finish();
            }
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
