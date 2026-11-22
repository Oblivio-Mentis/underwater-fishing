using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Editor
{
    // Batch entry points use a disposable save and the same single scene as the Windows player.
    [InitializeOnLoad]
    public static class Raid014Validation
    {
        const string Scene = "Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Armed = "raid014.validation";
        static readonly List<string> observations = new();
        static string Output => Arg("-raid-output");
        static string Arg(string key)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : "";
        }

        static Raid014Validation() { EditorApplication.update += StartWhenReady; }

        public static void Build()
        {
            var path = Arg("-raid-exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool timing=PlayerSettings.enableFrameTimingStats, jobs=PlayerSettings.graphicsJobs;
            BuildReport report;
            try
            {
                if(Arg("-raid-frame-timing")=="on")
                {
                    PlayerSettings.enableFrameTimingStats=true;
                    PlayerSettings.graphicsJobs=false;
                }
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { Scene }, target = BuildTarget.StandaloneWindows64,
                    locationPathName = path, options = Arg("-raid-flavor") == "qa" ? BuildOptions.Development : BuildOptions.None
                });
            }
            finally
            {
                if(PlayerSettings.enableFrameTimingStats!=timing)PlayerSettings.enableFrameTimingStats=timing;
                if(PlayerSettings.graphicsJobs!=jobs)PlayerSettings.graphicsJobs=jobs;
            }
            var s = report.summary;
            Debug.Log($"RAID014_BUILD {s.result} errors={s.totalErrors} warnings={s.totalWarnings} bytes={s.totalSize}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void Play()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-art-editor")>=0)
            {
                EditorSceneManager.OpenScene(Scene);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (string.IsNullOrEmpty(Output) || string.IsNullOrEmpty(Arg("-raid-save")))
                throw new ArgumentException("Supply -raid-output and -raid-save for a disposable local run.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(Scene);
            SessionState.SetBool(Armed, true);
            EditorApplication.EnterPlaymode();
        }

        static void StartWhenReady()
        {
            if (!SessionState.GetBool(Armed, false) || !EditorApplication.isPlaying || !Application.isPlaying) return;
            SessionState.SetBool(Armed, false);
            Run();
        }

        static async void Run()
        {
            try
            {
                await Until(() => DiverMotor.ServerDivers.Any(d => d.IsOwner) && RaidWorld.Instance.Team != null &&
                                  RaidWorld.Instance.TravelRemaining == 0, "host startup", 60);
                if (Arg("-raid-peer") != "")
                    await Until(() => DiverMotor.ServerDivers.Count == 2, "independent player", 90);
                var diver = DiverMotor.ServerDivers.First(d => d.IsOwner);
                if (Arg("-raid-case") == "reef")
                {
                    Raid.Nereid.Editor.NereidEncounterChecks.Start();
                    await Until(() => !Raid.Nereid.Editor.NereidEncounterChecks.Status.StartsWith("Running"), "reef regression", 240);
                    Check(Raid.Nereid.Editor.NereidEncounterChecks.Status.StartsWith("PASS"), Raid.Nereid.Editor.NereidEncounterChecks.Status);
                }
                else
                {
                    await AirAndDefense(diver);
                    if (DiverMotor.ServerDivers.Count == 2) await ShipRegression(diver);
                }
                File.WriteAllLines(Path.Combine(Output, "air-defense.txt"), observations);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                var t = ThreatDirector.Instance;
                if (t != null && t.IsServerInitialized)
                {
                    observations.Add($"Wave active={t.WaveActive}, position={t.WavePosition}, health={t.WaveHealth}, lock={RaidWorld.Instance.Airlock.Phase}");
                    var near = Physics.OverlapSphere(t.WavePosition, 1, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Where(c => !c.transform.IsChildOf(t.transform)).Select(c => c.name);
                    observations.Add("Nearby physical surfaces: " + string.Join(", ",near));
                }
                observations.Add("FAILED " + e);
                File.WriteAllLines(Path.Combine(Output, "air-defense.txt"), observations);
                Debug.LogException(e);
                EditorApplication.Exit(2);
            }
        }

        static async Awaitable Until(Func<bool> condition, string label, float timeout = 12)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException(label);
                await Awaitable.NextFrameAsync();
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            observations.Add(message);
        }

        static async Awaitable AirAndDefense(DiverMotor d)
        {
            var w = RaidWorld.Instance;
            var chamber = NereidAirlock.Active;
            var snapshot = (SyncVar<AirlockSnapshot>)typeof(RaidWorld).GetField("airlock", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(w);
            void SetLock(AirlockSnapshot s) { snapshot.Value = s; chamber.ApplyDoors(s); Physics.SyncTransforms(); }
            var a = AirlockSnapshot.Dry;
            SetLock(a);
            d.Teleport(chamber.transform.TransformPoint(new Vector3(4, 1, 0)));
            await Awaitable.WaitForSecondsAsync(.4f);
            // The zero-window control reproduces the previous build's wet-tick rule at this position.
            float limit = d.tuning.airlockBreathingWindow;
            d.tuning.airlockBreathingWindow = 0;
            float controlStart = d.Oxygen;
            try
            {
                a.Request(true); SetLock(a);
                await Until(() => w.Airlock.Phase == AirlockPhase.OpeningOuter, "control departure", 35);
                float debit = controlStart - d.Oxygen;
                Check(debit > 1, $"Zero-window departure spent {debit:F2} O2 before the opening");
            }
            finally { d.tuning.airlockBreathingWindow = limit; }
            a = AirlockSnapshot.Dry;
            SetLock(a);
            await Awaitable.WaitForSecondsAsync(.6f);
            float initial = d.Oxygen;
            a.Request(true); SetLock(a);
            await Until(() => !d.IsInside && w.Airlock.Phase == AirlockPhase.Flooding, "helmet submerged");
            float wetAt = Time.time;
            await Until(() => w.Airlock.Phase == AirlockPhase.OpeningOuter, "ocean door opening");
            Check(initial - d.Oxygen < .12f, $"Opening at {Time.time-wetAt:F2}s after immersion. O2 {initial:F2} -> {d.Oxygen:F2}");
            await Awaitable.WaitForSecondsAsync(1.4f);
            Check(initial-d.Oxygen > 1, $"Open outer door consumes real O2: {d.Oxygen:F2}");
            SetLock(AirlockSnapshot.Dry);
            await Awaitable.WaitForSecondsAsync(.4f);
            // A held pump is a fixture. DiverMotor continues ticking the real ten-second window.
            w.enabled = false;
            var held = new AirlockSnapshot { Phase=AirlockPhase.Flooding, Water=1 };
            SetLock(held);
            float stalled = d.Oxygen;
            await Awaitable.WaitForSecondsAsync(9.3f);
            Check(Mathf.Abs(stalled-d.Oxygen)<.12f, $"Stalled wet lock holds O2 until limit: {d.Oxygen:F2}");
            await Awaitable.WaitForSecondsAsync(2);
            Check(stalled-d.Oxygen>.7f, $"Stalled wet lock resumes consumption: {d.Oxygen:F2}");
            SetLock(AirlockSnapshot.Dry); w.enabled=true;

            var t = ThreatDirector.Instance;
            // Extend just this wave so two real pressure cycles and peer capture fit the same trial.
            t.tuning = UnityEngine.Object.Instantiate(t.tuning);
            t.tuning.waveSeconds = 100;
            int attacks = 0;
            t.StockAttacked += _ => attacks++;
            t.StartWave(1,1);
            var origin = t.WavePosition;
            var gate = chamber.GetPad(2).transform.position;
            await Awaitable.WaitForSecondsAsync(5);
            observations.Add($"Approach {origin} -> {t.WavePosition}, outer control {gate}, active={t.WaveActive}");
            await Awaitable.WaitForSecondsAsync(5);
            var stopped = t.WavePosition;
            await Awaitable.WaitForSecondsAsync(.6f);
            Check(t.WaveActive && (t.WavePosition-stopped).magnitude<.05f && stopped.x>gate.x && attacks==0 && w.StockLossCount==0,
                $"Closed ocean door stops raider at {stopped}, pad {gate}, stock loss {w.StockLossCount}");
            var aimPoint = t.transform.Find("Reservoir raider").GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Tapered asymmetric body").bounds.center;
            var tool = d.GetComponent<HarpoonTool>();
            var fire = typeof(HarpoonTool).GetMethod("QaFireOwned",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            async Awaitable Shoot(Vector3 feet)
            {
                d.Teleport(feet); d.LookAtConsole(aimPoint);
                await Awaitable.WaitForSecondsAsync(.55f);
                d.LookAtConsole(aimPoint);
                await Awaitable.WaitForSecondsAsync(.3f);
                int seq=tool.ShotSequence;
                if (!(bool)fire.Invoke(tool,null))
                    throw new InvalidOperationException($"Owner shot blocked: HUD={UI.UiInputGate.Modal}, pad={UI.UiInputGate.PointerReserved}, slot={tool.ActiveSlot}, helpless={d.IsHelpless}");
                await Until(()=>tool.ShotSequence!=seq,"server accepts shot");
            }
            float hp=t.WaveHealth;
            await Shoot(new Vector3(stopped.x-4,stopped.y-.6f,stopped.z));
            Check(t.WaveHealth==hp,"Closed door blocks harpoon damage");
            await Shoot(new Vector3(stopped.x+4,stopped.y-.6f,stopped.z));
            Check(t.WaveHealth<hp,$"Exposed render mesh takes one shot: {hp:F1} -> {t.WaveHealth:F1}");
            Capture(d,"raider-door.png");
            d.Teleport(stopped + new Vector3(1.0f,-.5f,1.7f));
            await Awaitable.WaitForSecondsAsync(.25f);
            d.LookAtConsole(aimPoint);
            await Awaitable.WaitForSecondsAsync(.4f);
            Capture(d,"raider-at-seal.png");
            if (DiverMotor.ServerDivers.Count == 2)
                await RemoteShot(d,t,aimPoint,stopped);
            a = w.Airlock; a.Request(true); SetLock(a);
            await Until(() => chamber.Contains(t.WavePosition) && w.Airlock.Phase == AirlockPhase.OuterOpen,
                "raider enters through open outer door", 40);
            await Awaitable.WaitForSecondsAsync(4);
            Check(t.WavePosition.x > chamber.transform.position.x && attacks == 0,
                $"Inner door holds the raider at {t.WavePosition}");
            a = w.Airlock; a.Request(false); SetLock(a);
            await Until(() => attacks > 0, "reachable reservoir attacked through cycled gates", 50);
            Check(!chamber.Contains(t.WavePosition) && Vector3.Distance(t.WavePosition, t.tuning.reservoirPoint) < 5,
                $"Physical contact at the tank: {t.WavePosition}");
            await Until(() => !t.WaveActive, "wave expires after bounded attacks", 30);
            int previousAttacks = attacks;
            t.StartWave(1,1);
            Check(t.WaveActive && t.WaveHealth == hp && Vector3.Distance(t.WavePosition, origin) < .1f,
                "Next wave spawns outside with full health");
            await Awaitable.WaitForSecondsAsync(5);
            Check(attacks == previousAttacks && t.WavePosition.x > gate.x,
                "Repeat wave is stopped again by the closed outer door");
        }

        static async Awaitable ShipRegression(DiverMotor host)
        {
            var w = RaidWorld.Instance;
            var peer = DiverMotor.ServerDivers.First(d => !d.IsOwner);
            var deck = new Vector3(-3, 1.2f, 1);
            host.Teleport(deck); peer.Teleport(deck + Vector3.right * 1.4f);
            host.Trap();
            await Until(() => host.IsTrapped, "staged trapped diver");
            await PeerRequest("key", 5, 0, "F");
            await Until(() => !host.IsTrapped, "remote F rescue", 9);
            Check(!host.IsDead, "Remote F rescued the immobilized diver on deck");

            foreach (bool feeding in new[] { false, true })
            {
                var action = feeding ? WorldAction.FeedAll : WorldAction.ProcessAll;
                var station = ShipConsole.Stations.First(s => s.Supports(action));
                bool reachable = false;
                foreach (var side in new[] {Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                {
                    host.Teleport(station.transform.position + side * 1.45f + Vector3.up * .5f);
                    Physics.SyncTransforms();
                    if (host.IsInside && ShipConsole.CanUse(host, action)) { reachable = true; break; }
                }
                Check(reachable, "Reached physical station " + action);
                long id = feeding ? 1400202 : 1400201;
                int before = feeding ? w.Team.offering : w.Team.biomass;
                Check(w.Team.Deposit(new CatchItem(id, 2, 0, 1, 1, 1)), "Staged one fish in the reservoir");
                int required = Mathf.Max(1, w.progression.foodRequired + (w.Team.highestDepth-1)*w.progression.foodPerDepth);
                w.Command(action, FishRouteQuote.Compute(w.Team.tank, required));
                await Until(() => w.FishRoute.active, "start " + action);
                await Until(() => !w.FishRoute.active, "complete " + action, 30);
                int added = (feeding ? w.Team.offering : w.Team.biomass) - before;
                Check(!w.Team.tank.Exists(x => x.id == id) && added == (feeding ? 1 : 2),
                    action + " consumed one fish and added " + added);
            }
        }

        static async Awaitable RemoteShot(DiverMotor host, ThreatDirector t, Vector3 point, Vector3 stopped)
        {
            var peer = DiverMotor.ServerDivers.First(d=>!d.IsOwner);
            host.Teleport(stopped+new Vector3(5,0,3));
            peer.Teleport(stopped+new Vector3(4,-.6f,0));
            await Awaitable.WaitForSecondsAsync(.6f);
            var rot=Quaternion.LookRotation(point-(peer.transform.position+Vector3.up*.6f)).eulerAngles;
            await PeerRequest("view",rot.y,Mathf.DeltaAngle(0,rot.x));
            await Awaitable.WaitForSecondsAsync(.5f);
            float hp=t.WaveHealth;
            await PeerRequest("fire");
            await Until(()=>t.WaveHealth<hp,"remote shot registers");
            Check(t.WaveHealth == hp-1,$"Remote owner hit visible raider once: {hp:F1} -> {t.WaveHealth:F1}");
        }

        [Serializable] sealed class Request { public string id, action, value; public float number, number2; }
        [Serializable] sealed class Response { public string request, error; }
        static async Awaitable PeerRequest(string action, float x=0, float y=0, string value="")
        {
            string dir=Arg("-raid-peer");
            var r=new Request { id=Guid.NewGuid().ToString("N"), action=action, number=x, number2=y, value=value };
            File.WriteAllText(Path.Combine(dir,"pending"),JsonUtility.ToJson(r));
            var p=Path.Combine(dir,"request.json"); if(File.Exists(p)) File.Delete(p);
            File.Move(Path.Combine(dir,"pending"),p);
            await Until(()=>{
                try {
                    var resp=JsonUtility.FromJson<Response>(File.ReadAllText(Path.Combine(dir,"state.json")));
                    if(resp==null || resp.request!=r.id)return false;
                    if(!string.IsNullOrEmpty(resp.error))throw new InvalidOperationException(resp.error);
                    return true;
                } catch(IOException){return false;}
            }, "peer "+action);
        }

        static void Capture(DiverMotor owner,string name)
        {
            var cam=owner.GetComponentInChildren<Camera>();
            var previous=cam.targetTexture;
            var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);
            var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {
                cam.targetTexture=rt;
                RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
                File.WriteAllBytes(Path.Combine(Output,name),tex.EncodeToPNG());
            } finally { cam.targetTexture=previous; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(tex); }
        }
    }
}
