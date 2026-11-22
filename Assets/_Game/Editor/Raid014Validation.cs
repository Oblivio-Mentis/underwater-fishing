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
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Scene }, target = BuildTarget.StandaloneWindows64,
                locationPathName = path, options = Arg("-raid-flavor") == "qa" ? BuildOptions.Development : BuildOptions.None
            });
            var s = report.summary;
            Debug.Log($"RAID014_BUILD {s.result} errors={s.totalErrors} warnings={s.totalWarnings} bytes={s.totalSize}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void Play()
        {
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
                else await AirAndDefense(diver);
                File.WriteAllLines(Path.Combine(Output, "air-defense.txt"), observations);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
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
            t.StartWave(1,1);
            var origin = t.WavePosition;
            var gate = chamber.GetPad(2).transform.position;
            await Until(() => Vector3.Distance(t.WavePosition,origin)>4,"raider approach");
            await Awaitable.WaitForSecondsAsync(5);
            var stopped = t.WavePosition;
            await Awaitable.WaitForSecondsAsync(.6f);
            Check(t.WaveActive && (t.WavePosition-stopped).magnitude<.05f && stopped.x>gate.x && w.StockLossCount==0,
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
                Check((bool)fire.Invoke(tool,null),"Owner dispatched shot");
                await Until(()=>tool.ShotSequence!=seq,"server accepts shot");
            }
            float hp=t.WaveHealth;
            await Shoot(new Vector3(stopped.x-4,stopped.y-.6f,stopped.z));
            Check(t.WaveHealth==hp,"Closed door blocks harpoon damage");
            await Shoot(new Vector3(stopped.x+4,stopped.y-.6f,stopped.z));
            Check(t.WaveHealth<hp,$"Exposed render mesh takes one shot: {hp:F1} -> {t.WaveHealth:F1}");
            Capture(d,"raider-door.png");
            if (DiverMotor.ServerDivers.Count == 2)
                await RemoteShot(d,t,aimPoint,stopped);
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

        [Serializable] sealed class Request { public string id, action; public float number, number2; }
        [Serializable] sealed class Response { public string request, error; }
        static async Awaitable PeerRequest(string action, float x=0, float y=0)
        {
            string dir=Arg("-raid-peer");
            var r=new Request { id=Guid.NewGuid().ToString("N"), action=action, number=x, number2=y };
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
