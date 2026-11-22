#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid.Nereid
{
    // Opt-in local host fixture. All frames come from the owned camera of a live network diver.
    public sealed class WaterLifeTrial : MonoBehaviour
    {
        [Serializable] sealed class Sample
        {
            public bool effects;
            public int frames;
            public float seconds, fps, medianMs, p95Ms;
            public Vector3 eye, angles;
        }
        [Serializable] sealed class Report
        {
            public string status="waiting for host and peer", gpu, api, quality, pipeline, error;
            public string timing="Completed offscreen URP camera stacks per real second, including a synchronous one-pixel GPU readback per frame. Screen overlay HUD excluded.";
            public int width, height, depth, msaa, vsync, targetFps;
            public float scale, nearestPassMetres;
            public bool drySuppressed, wetVisible, wetChamberVisible, deepWaterVisible;
            public float chamberFill;
            public List<Sample> samples=new();
        }
        string folder;
        readonly Report report=new();
        NereidWaterLife water;
        DiverMotor host, peer;
        Vector3 entryHost,entryPeer;
        int initialVsync, initialCap;
        bool staged;
        RenderTexture frameTarget;
        Texture2D syncPixel;
        readonly RenderPipeline.StandardRequest render = new();

        void RenderCompletedFrame(Camera cam)
        {
            var oldTarget = cam.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                cam.targetTexture = frameTarget;
                RenderPipeline.SubmitRenderRequest(cam,render);
                RenderTexture.active = frameTarget;
                // ReadPixels waits for the graphics device, including when Windows occludes the player.
                syncPixel.ReadPixels(new Rect(frameTarget.width/2,frameTarget.height/2,1,1),0,0,false);
            }
            finally { cam.targetTexture=oldTarget; RenderTexture.active=oldActive; }
        }
        ThreatDirector threats;
        bool threatsWereActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args=Environment.GetCommandLineArgs();
            int i=Array.IndexOf(args,"-raid-water-trial");
            if(i<0||i+1>=args.Length)return;
            Begin(args[i+1]);
        }

        public static void Begin(string outputDirectory)
        {
            if(!Application.isPlaying || FindAnyObjectByType<WaterLifeTrial>()!=null)
                throw new InvalidOperationException("Start this fixture once during Play Mode.");
            var go=new GameObject("RAID-014 water and kelp capture");
            var fixture=go.AddComponent<WaterLifeTrial>();
            fixture.folder=Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(fixture.folder);
            fixture.StartCoroutine(fixture.Guard());
        }

        IEnumerator Guard()
        {
            Write();
            var steps=Run();
            for (;;)
            {
                bool more;
                try { more=steps.MoveNext(); }
                catch(Exception ex) { report.error=ex.ToString(); report.status="failed"; break; }
                if(!more) { report.status="complete"; break; }
                yield return steps.Current;
            }
            Restore();
            Write();
            Destroy(gameObject);
        }

        IEnumerator Run()
        {
            if(Application.isBatchMode)
                throw new InvalidOperationException("Run the measuring host windowed with graphics. A batch player does not present frames to measure.");
            double until=Time.realtimeSinceStartupAsDouble+95;
            while(host==null || peer==null)
            {
                if(Time.realtimeSinceStartupAsDouble>until)throw new TimeoutException("Two living network players required.");
                host=DiverMotor.ServerDivers.FirstOrDefault(x=>x && x.IsOwner && !x.IsDead);
                peer=DiverMotor.ServerDivers.FirstOrDefault(x=>x && !x.IsOwner && !x.IsDead);
                yield return null;
            }
            water=FindAnyObjectByType<NereidWaterLife>();
            if(!water || !water.enabled || water.FirstHoldfast==Vector3.zero)throw new InvalidOperationException("No tall kelp in active scene.");
            initialVsync=QualitySettings.vSyncCount; initialCap=Application.targetFrameRate;
            entryHost=host.transform.position; entryPeer=peer.transform.position; staged=true;
            threats=RaidWorld.Instance.threats; threatsWereActive=threats && threats.enabled;
            if(threats) threats.enabled=false;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            host.Teleport(new Vector3(1,1.1f,3));
            peer.Teleport(new Vector3(3,1.1f,3));
            yield return new WaitForSecondsRealtime(1);
            report.drySuppressed=host.IsInside && !water.ShowingSuspension;
            if(!report.drySuppressed)throw new InvalidOperationException("Suspension visible inside or host did not reach dry deck.");
            Camera cam=host.GetComponentInChildren<Camera>();
            Capture("01-dry-deck");
            report.gpu=SystemInfo.graphicsDeviceName; report.api=SystemInfo.graphicsDeviceType.ToString();
            report.quality=QualitySettings.names[QualitySettings.GetQualityLevel()];
            report.width=Screen.width; report.height=Screen.height; report.depth=RaidWorld.Instance.CurrentDepth;
            report.vsync=QualitySettings.vSyncCount; report.targetFps=Application.targetFrameRate;
            var urp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(urp) { report.pipeline=urp.name; report.msaa=urp.msaaSampleCount; report.scale=urp.renderScale; }
            var blade=water.FirstHoldfast+Vector3.up*2.35f;
            host.Teleport(blade+new Vector3(-1.35f,.1f,-4.25f)); yield return null;
            host.LookAtConsole(blade);
            yield return new WaitForSecondsRealtime(1.5f);
            report.wetVisible=!host.IsInside && water.ShowingSuspension && water.VisibleGroves>0;
            if(!report.wetVisible)throw new InvalidOperationException("Wet observer did not see kelp and the local suspension volume.");
            frameTarget=RenderTexture.GetTemporary(report.width,report.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            syncPixel=new Texture2D(1,1,TextureFormat.RGB24,false);
            render.destination=frameTarget;
            if(!RenderPipeline.SupportsRenderRequest(cam,render))throw new InvalidOperationException("Camera-stack render requests unavailable.");
            report.status="measuring A/B/B/A"; Write();
            foreach(bool show in new[]{false,true,true,false})
            {
                water.enabled=show;
                double warmUntil=Time.realtimeSinceStartupAsDouble+1;
                do { yield return null; RenderCompletedFrame(cam); } while(Time.realtimeSinceStartupAsDouble<warmUntil);
                double sampleStart=Time.realtimeSinceStartupAsDouble, previous=sampleStart;
                var times=new List<float>(2048);
                var eye=cam.transform.position; var angles=cam.transform.eulerAngles;
                do {
                    yield return null;
                    RenderCompletedFrame(cam);
                    double now=Time.realtimeSinceStartupAsDouble;
                    times.Add((float)((now-previous)*1000)); previous=now;
                } while(previous-sampleStart<4.0);
                if(Vector3.Distance(eye,cam.transform.position)>.12f || Quaternion.Angle(Quaternion.Euler(angles),cam.transform.rotation)>1f)
                    throw new InvalidOperationException("Observer moved during frame sample.");
                times.Sort();
                float secs=(float)(previous-sampleStart);
                report.samples.Add(new Sample{effects=show,seconds=secs,frames=times.Count,fps=times.Count/secs,
                    medianMs=times[times.Count/2],p95Ms=times[(times.Count-1)*95/100],eye=eye,angles=angles});
                if(report.samples.Count<=2)Capture(show?"03-water-on":"02-water-off");
                Write();
            }
            water.enabled=true; yield return new WaitForSecondsRealtime(.4f);
            report.status="peer swimming through grove"; Write();
            peer.Teleport(blade+new Vector3(-3.7f,0,0)); yield return new WaitForSecondsRealtime(.4f);
            Capture("04-kelp-before");
            var film=Path.Combine(folder,"traverse");
            Directory.CreateDirectory(film);
            int frame=0;
            float elapsed=0; report.nearestPassMetres=100;
            bool contact=false;
            while(elapsed<4.6f)
            {
                yield return new WaitForFixedUpdate();
                peer.ApplyExternalMotion(Vector3.right*(1.8f*Time.fixedDeltaTime));
                elapsed+=Time.fixedDeltaTime;
                float range=Vector3.Distance(peer.transform.position,blade);
                report.nearestPassMetres=Mathf.Min(range,report.nearestPassMetres);
                if(!contact && range<.7f) { contact=true; yield return null; Capture("05-kelp-parting"); }
                if(elapsed>=frame*.18f) { yield return null; Capture(Path.Combine("traverse","frame-"+frame.ToString("D3"))); frame++; }
            }
            if(!contact)throw new InvalidOperationException("Second diver did not cross holdfast.");
            yield return new WaitForSecondsRealtime(.6f);
            Capture("06-kelp-returned");

            var world=RaidWorld.Instance;
            var chamber=NereidAirlock.Active;
            var pad=chamber.GetPad(2);
            host.Teleport(pad.transform.position-chamber.transform.right*1.2f-Vector3.up*.6f);
            yield return new WaitForSecondsRealtime(.25f);
            host.LookAtConsole(pad.transform.position);
            yield return new WaitForSecondsRealtime(.35f);
            if(AirlockPad.Aimed(host,cam)!=pad)throw new InvalidOperationException("Could not aim at the sea-cycle pad.");
            pad.Press(host);
            double floodLimit=Time.realtimeSinceStartupAsDouble+12;
            while(world.Airlock.Water<.97f && Time.realtimeSinceStartupAsDouble<floodLimit)yield return null;
            report.chamberFill=world.Airlock.Water;
            report.wetChamberVisible=report.chamberFill>.95f && chamber.Contains(cam.transform.position) &&
                chamber.IsSubmerged(cam.transform.position) && water.ShowingSuspension;
            if(!report.wetChamberVisible)throw new InvalidOperationException("Sea-cycle or flooded-chamber suspension failed.");
            host.LookAtConsole(chamber.transform.TransformPoint(new Vector3(1,1.9f,0)));
            yield return new WaitForSecondsRealtime(.25f);
            Capture("07-flooded-chamber");
            // Only test deep water when the supplied isolated team profile has already unlocked it.
            if(world.Team.CanVisitDepth(2))
            {
                var chart=ShipConsole.Stations.First(x=>x.kind==ShipConsoleKind.Depths);
                peer.Teleport(new Vector3(3,1.1f,3));
                foreach(var axis in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
                {
                    host.Teleport(chart.transform.position+axis*1.45f+Vector3.up*.5f);
                    Physics.SyncTransforms();
                    if(host.IsInside && ShipConsole.CanUse(host,WorldAction.DiveTwo))break;
                }
                world.Command(WorldAction.DiveTwo,0);
                double travelLimit=Time.realtimeSinceStartupAsDouble+12;
                while(world.CurrentDepth!=2 && Time.realtimeSinceStartupAsDouble<travelLimit)yield return null;
                if(world.CurrentDepth!=2)throw new InvalidOperationException("Chart refused the unlocked second fishing ground.");
                host.Teleport(blade+new Vector3(-1.35f,.1f,-4.25f)); yield return null; host.LookAtConsole(blade);
                yield return new WaitForSecondsRealtime(1);
                report.deepWaterVisible=water.ShowingSuspension && water.VisibleGroves>0;
                if(!report.deepWaterVisible)throw new InvalidOperationException("Deep-water suspension missing.");
                Capture("08-deep-water");
            }
        }

        void Capture(string name) => UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,name+".png"));
        void Write() => File.WriteAllText(Path.Combine(folder,"environment.json"), JsonUtility.ToJson(report,true));

        void Restore()
        {
            if(!staged)return;
            if(frameTarget)RenderTexture.ReleaseTemporary(frameTarget);
            if(syncPixel)Destroy(syncPixel);
            staged=false; QualitySettings.vSyncCount=initialVsync; Application.targetFrameRate=initialCap;
            if(water)water.enabled=true;
            if(threats)threats.enabled=threatsWereActive;
            if(host)host.Teleport(entryHost);
            if(peer)peer.Teleport(entryPeer);
        }
        void OnDestroy() => Restore();
    }
}
#endif
