#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid.Nereid
{
    // Opt-in run from an owned network camera. The same marks file is used for baseline and candidate.
    internal sealed class ArtPassTrial : MonoBehaviour
    {
        [Serializable] sealed class Marks { public Mark[] cameras; }
        [Serializable] sealed class Mark
        {
            public string name, station, prop;
            public int depth;
            public Vector3 feet, look;
            public bool shipLocal, benchmark;
        }
        [Serializable] sealed class Sample
        {
            public string name;
            public int depth, samples;
            public Vector3 eye, direction;
            public bool inside;
            public double frameMedianMs, frameP95Ms, cpuMedianMs, cpuP95Ms, gpuMedianMs, gpuP95Ms;
        }
        [Serializable] sealed class Result
        {
            public string status, phase, error, gpu, api, profile, pipeline;
            public int width, height, msaa, vsync;
            public float renderScale;
            public List<Sample> views=new();
        }

        readonly Result result=new();
        readonly FrameTiming[] timingBuffer=new FrameTiming[10];
        string folder,marksFile;
        DiverMotor owner,peer;
        RaidWorld world;
        Camera eye;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args=Environment.GetCommandLineArgs();
            int i=Array.IndexOf(args,"-raid-art-pass");
            if(i<0 || i+2>=args.Length)return;
            if(Array.IndexOf(args,"-raid-save")<0)throw new InvalidOperationException("Art capture requires a disposable host save.");
            var driver=new GameObject("RAID-016 art comparison").AddComponent<ArtPassTrial>();
            driver.folder=Path.GetFullPath(args[i+1]);driver.marksFile=Path.GetFullPath(args[i+2]);
            Directory.CreateDirectory(driver.folder);
            driver.StartCoroutine(driver.Run());
        }

        IEnumerator Run()
        {
            result.status="running";
            var stack=new Stack<IEnumerator>();stack.Push(Capture());
            while(stack.Count>0)
            {
                object step=null;
                try
                {
                    IEnumerator top=stack.Peek();
                    if(!top.MoveNext()){stack.Pop();continue;}
                    if(top.Current is IEnumerator nested){stack.Push(nested);continue;}
                    step=top.Current;
                }
                catch(Exception e){result.status="failed";result.error=e.ToString();break;}
                yield return step;
            }
            if(result.status=="running")result.status="passed";
            Write();
            foreach(var frame in result.views)
                Debug.Log($"[RAID016] {frame.name} {frame.samples} timings CPU {frame.cpuMedianMs:F2} GPU {frame.gpuMedianMs:F2}");
            Debug.Log("[RAID016] "+result.status+" "+result.error);
#if UNITY_EDITOR
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-art-editor")>=0)
                UnityEditor.EditorApplication.Exit(result.status=="passed"?0:1);
#endif
        }

        void Write()=>File.WriteAllText(Path.Combine(folder,"views.json"),JsonUtility.ToJson(result,true));
        IEnumerator Await(string phase,Func<bool> ready,float seconds)
        {
            result.phase=phase;Write();
            float until=Time.realtimeSinceStartup+seconds;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>until)throw new TimeoutException(phase);
                yield return null;
            }
        }

        IEnumerator Capture()
        {
            yield return Await("Owned diver and world ready",()=>
            {
                world=RaidWorld.Instance;owner=UiRoot.Instance?.Context.Player;
                return world && world.NetworkObject && world.IsServerInitialized && world.View!=null &&
                    owner && owner.NetworkObject && owner.IsOwner && world.TravelRemaining==0;
            },75);
            var marks=JsonUtility.FromJson<Marks>(File.ReadAllText(marksFile)).cameras;
            eye=owner.GetComponentInChildren<Camera>();
            peer=DiverMotor.ServerDivers.FirstOrDefault(d=>d && !d.IsOwner);
            if(world.threats)world.threats.enabled=false;
            world.StageArtStock();
            Application.runInBackground=true;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            result.gpu=SystemInfo.graphicsDeviceName;result.api=SystemInfo.graphicsDeviceType.ToString();
            result.profile=QualitySettings.names[QualitySettings.GetQualityLevel()];
            result.width=Screen.width;result.height=Screen.height;result.vsync=QualitySettings.vSyncCount;
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {result.pipeline=urp.name;result.msaa=urp.msaaSampleCount;result.renderScale=urp.renderScale;}
            foreach(var mark in marks)yield return CaptureMark(mark);
        }

        IEnumerator CaptureMark(Mark mark)
        {
            result.phase=mark.name;Write();
            if(UiRoot.Instance.Page!=UiPage.None)UiRoot.Instance.ShowPage(UiPage.None);
            if(world.CurrentDepth!=mark.depth)
            {
                world.StageArtDepth(mark.depth);
                yield return new WaitForSecondsRealtime(.9f);
            }
            var hull=NereidVessel.Active.transform;
            Vector3 feet=mark.shipLocal?hull.TransformPoint(mark.feet):mark.feet;
            Vector3 look=mark.shipLocal?hull.TransformPoint(mark.look):mark.look;
            ShipConsole station=null;
            if(!string.IsNullOrEmpty(mark.station))
            {
                station=ShipConsole.Stations.Single(s=>s.IsMonitor && s.kind.ToString()==mark.station);
                look=station.screenAnchor.position;
                feet=look-station.screenAnchor.forward*1.7f-Vector3.up*.6f;
            }
            if(mark.prop=="kelp-peer")
            {
                // Keep the passer out of the comparison camera until the kelp photograph.
                peer=DiverMotor.ServerDivers.FirstOrDefault(d=>d && !d.IsOwner);
                if(peer)peer.Teleport(look+Vector3.left*.6f);
            }
            else if(peer)peer.Teleport(hull.TransformPoint(new Vector3(2,.52f,-9)));
            owner.Teleport(feet);
            if(mark.prop=="ball")world.StageBallTrial(feet+Vector3.forward*.85f);
            if(mark.prop=="large")
            {
                var fish=FindObjectsByType<FishActor>().First(f=>f.depthTier==mark.depth && f.RequiresPartner);
                fish.SetArtMark(look,Quaternion.Euler(0,105,0));
            }
            yield return null;
            owner.LookAtConsole(look);
            yield return new WaitForSecondsRealtime(.65f);
            if(station!=null)
            {
                if(!station.InRange(owner) || ShipConsole.ResolveSight(owner,eye)!=station)
                    throw new InvalidOperationException("Viewmark must face physical monitor "+mark.station+" at "+owner.transform.position);
                UiRoot.Instance.UseConsole(station);
                yield return new WaitForSecondsRealtime(.25f);
            }
            if(mark.prop=="ball")
            {
                world.RequestGosha(GoshaAction.PickUp);
                yield return Await("Own the physical ball",()=>world.HasGoshaBall(owner),5);
                yield return new WaitForSecondsRealtime(.25f);
            }
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,mark.name+".png"));
            var sample=new Sample{name=mark.name,depth=mark.depth,eye=eye.transform.position,direction=eye.transform.forward,inside=owner.IsInside};
            if(mark.benchmark && Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-art-timing")>=0)
                yield return Measure(sample);
            result.views.Add(sample);Write();
        }

        IEnumerator Measure(Sample sample)
        {
            var cpu=new List<double>();var gpu=new List<double>();var presented=new List<double>();
            ulong last=0;
            int readings=0,cpuValues=0,gpuValues=0;
            double begin=Time.realtimeSinceStartupAsDouble,end=begin+7;
            while(Time.realtimeSinceStartupAsDouble<end)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return new WaitForEndOfFrame();
                if(Time.realtimeSinceStartupAsDouble<begin+2)continue;
                uint count=FrameTimingManager.GetLatestTimings((uint)timingBuffer.Length,timingBuffer);
                readings+=(int)count;
                for(int i=(int)count-1;i>=0;i--)
                {
                    FrameTiming timing=timingBuffer[i];
                    if(timing.cpuFrameTime>0)cpuValues++;
                    if(timing.gpuFrameTime>0)gpuValues++;
                    if(timing.cpuFrameTime<=0 || timing.gpuFrameTime<=0)continue;
                    if(timing.frameStartTimestamp!=0)
                    {
                        if(timing.frameStartTimestamp<=last)continue;
                        last=timing.frameStartTimestamp;
                    }
                    else if(i!=0)continue;
                    cpu.Add(timing.cpuFrameTime);gpu.Add(timing.gpuFrameTime);
                }
                presented.Add(Time.unscaledDeltaTime*1000);
            }
            if(cpu.Count<50 || gpu.Count<50)throw new InvalidOperationException(
                $"Frame Timing Manager enabled={FrameTimingManager.IsFeatureEnabled()}, readings={readings}, CPU={cpuValues}, GPU={gpuValues}, pairs={cpu.Count}.");
            if(Vector3.Distance(sample.eye,eye.transform.position)>.1f || Vector3.Angle(sample.direction,eye.transform.forward)>1)
                throw new InvalidOperationException("The measuring camera moved at "+sample.name);
            sample.samples=cpu.Count;
            (sample.frameMedianMs,sample.frameP95Ms)=Distribution(presented);
            (sample.cpuMedianMs,sample.cpuP95Ms)=Distribution(cpu);
            (sample.gpuMedianMs,sample.gpuP95Ms)=Distribution(gpu);
        }

        static (double,double) Distribution(List<double> values)
        {
            values.Sort();
            return (values[values.Count/2],values[(values.Count-1)*95/100]);
        }
    }
}

namespace Raid
{
    public sealed partial class RaidWorld
    {
        internal void StageArtDepth(int tier)
        {
            pendingDepth=tier;
            CompleteTravel();
        }

        internal void StageArtStock()
        {
            team.biomass=24;team.chitin=7;team.scrap=13;team.electronics=3;
            team.hungerRemaining=160;
            team.tank.Clear();
            team.tank.Add(new CatchItem(1101,4,0,2,1,1,species:"Needlefish"));
            team.tank.Add(new CatchItem(1102,6,1,3,2,1,species:"Sailray"));
            team.tank.Add(new CatchItem(1103,0,0,0,1,1,3,2));
            Publish();
        }
    }

    public sealed partial class FishActor
    {
        internal void SetArtMark(Vector3 center,Quaternion rotation)
        {
            _origin=center;
            transform.SetPositionAndRotation(center,rotation);
        }
    }
}
#endif
