#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Stopwatch=System.Diagnostics.Stopwatch;
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
            public int width, height, msaa, vsync, fittings;
            public float renderScale;
            public List<Sample> views=new();
        }

        internal static event Action<Camera> PrepareShoalStill;
        readonly Result result=new();
        const int BeginGpuCamera=41,EndGpuCamera=42;
        [DllImport("Raid016Timing")]static extern IntPtr GetArtTimingEvent();
        [DllImport("Raid016Timing")]static extern int ArtTimingReady();
        [DllImport("Raid016Timing")]static extern ulong ReadArtTiming(out ulong nanoseconds);
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"--verify-nereid-surfaces")>=0)
            {
                var hull=NereidVessel.Active.transform;
                var outside=hull.Find("Nereid_Exterior");
                int CountTextured(Transform assembly) => assembly.GetComponentsInChildren<MeshRenderer>()
                    .Count(renderer=>renderer.sharedMaterials.Any(material=>material!=null &&
                        material.HasProperty("_SurfaceAtlas") && material.GetTexture("_SurfaceAtlas")!=null &&
                        material.GetVector("_SurfaceDetail").w>.5f && material.shader.isSupported));
                var exteriorCount=CountTextured(outside);
                var vesselCount=CountTextured(hull);
                if(exteriorCount<5 || vesselCount<30)
                    throw new InvalidOperationException($"Incomplete vessel surfaces: {vesselCount} total meshes, {exteriorCount} exterior.");
                Debug.Log($"[NEREID SURFACES] {vesselCount} textured meshes including {exteriorCount} exterior meshes");
            }
            var hardware=NereidVessel.Active.transform.Find("NEREID service fittings");
            result.fittings=hardware==null?0:hardware.GetComponentsInChildren<MeshRenderer>().Length;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-art-rebuild")>=0 && result.fittings==0)
                throw new InvalidOperationException("The rebuilt vessel did not restore its service models.");
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
            var inventory=owner.GetComponent<HarpoonTool>();
            if(inventory.ActiveSlot!=0)inventory.SelectSlot(0);
            ShipConsole station=null;
            FishActor featured=null;
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
                featured=FindObjectsByType<FishActor>().Single(f=>f.depthTier==mark.depth && f.SpeciesId=="Sailray");
            }
            yield return null;
            owner.LookAtConsole(look);
            yield return new WaitForSecondsRealtime(.65f);
            if(station!=null)
            {
                if(!station.InRange(owner) || ShipConsole.ResolveSight(owner,eye)!=station)
                    throw new InvalidOperationException("Viewmark must face physical monitor "+mark.station+" at "+owner.transform.position);
                if(!UiRoot.Instance.QaPointAtMonitor())
                    throw new InvalidOperationException("Reticle did not activate "+mark.station);
                yield return new WaitForSecondsRealtime(.25f);
                if(Mathf.Abs(eye.fieldOfView-UiRoot.Instance.Settings.fov)>.4f)
                    throw new InvalidOperationException("FOV changed while looking at "+mark.station);
            }
            if(mark.prop=="ball")
            {
                world.RequestGosha(GoshaAction.PickUp);
                yield return Await("Own the physical ball",()=>world.HasGoshaBall(owner),5);
                yield return new WaitForSecondsRealtime(.25f);
            }
            if(featured!=null)
            {
                featured.SetArtMark(look,Quaternion.Euler(0,6,0));
                Physics.SyncTransforms();
                var surface=featured.ImpactSurface(false).GetComponent<MeshCollider>();
                Vector3 center=surface.bounds.center;
                RaycastHit hit=default;
                bool found=false;
                for(int y=-2;y<=2 && !found;y++)
                for(int z=-2;z<=2 && !found;z++)
                {
                    Vector3 target=center+Vector3.up*surface.bounds.extents.y*y*.4f+
                        Vector3.forward*surface.bounds.extents.z*z*.4f;
                    found=Physics.Raycast(eye.transform.position,target-eye.transform.position,out hit,24,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                        hit.collider.GetComponentInParent<FishActor>()==featured;
                }
                if(!found)throw new InvalidOperationException("No first-hit ray to the featured fish at "+center);
                owner.LookAtConsole(hit.point);
                yield return new WaitForSecondsRealtime(.25f);
                if(!inventory.QaFireOwned())throw new InvalidOperationException("The owned harpoon could not fire at the featured fish.");
                yield return Await("Physical hunt connects to the ray",()=>inventory.HookedFish==featured,3);
                yield return new WaitForSecondsRealtime(.18f);
            }
            CaptureImage(mark);
            var sample=new Sample{name=mark.name,depth=mark.depth,eye=eye.transform.position,direction=eye.transform.forward,inside=owner.IsInside};
            if(mark.benchmark && Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-art-timing")>=0)
                yield return Measure(sample);
            result.views.Add(sample);Write();
        }

        void CaptureImage(Mark mark)
        {
            if(mark.prop=="shoal")PrepareShoalStill?.Invoke(eye);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,mark.name+".png"));
        }

        IEnumerator Measure(Sample sample)
        {
            if(ArtTimingReady()==0)throw new InvalidOperationException("The QA D3D11 timestamp plugin has no active device.");
            var callback=GetArtTimingEvent();
            var cpu=new List<double>();
            var gpu=new List<double>();
            var completed=new List<double>();
            ulong last=ReadArtTiming(out _);
            var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);
            var oldTarget=eye.targetTexture;
            var request=new RenderPipeline.StandardRequest{destination=target};
            try
            {
                eye.targetTexture=target;
                double begin=Time.realtimeSinceStartupAsDouble,end=begin+7;
                while(Time.realtimeSinceStartupAsDouble<end)
                {
                    yield return new WaitForEndOfFrame();
                    long start=Stopwatch.GetTimestamp();
                    GL.IssuePluginEvent(callback,BeginGpuCamera);
                    RenderPipeline.SubmitRenderRequest(eye,request);
                    GL.IssuePluginEvent(callback,EndGpuCamera);
                    double cpuTime=ElapsedMilliseconds(start);
                    var previous=RenderTexture.active;
                    try
                    {
                        RenderTexture.active=target;
                        pixel.ReadPixels(new Rect(target.width/2,target.height/2,1,1),0,0,false);
                    }
                    finally { RenderTexture.active=previous; }
                    ulong index=ReadArtTiming(out var nanos);
                    if(Time.realtimeSinceStartupAsDouble>=begin+2 && index>last && nanos>0)
                    {
                        cpu.Add(cpuTime);
                        gpu.Add(nanos/1e6);
                        completed.Add(ElapsedMilliseconds(start));
                    }
                    last=index;
                }
            }
            finally
            {
                eye.targetTexture=oldTarget;
                Destroy(pixel);
                RenderTexture.ReleaseTemporary(target);
            }
            if(cpu.Count<50 || gpu.Count<50)throw new InvalidOperationException(
                $"D3D11 camera timestamps captured {cpu.Count} usable samples, device total {last}.");
            if(Vector3.Distance(sample.eye,eye.transform.position)>.1f || Vector3.Angle(sample.direction,eye.transform.forward)>1)
                throw new InvalidOperationException("The measuring camera moved at "+sample.name);
            sample.samples=cpu.Count;
            (sample.frameMedianMs,sample.frameP95Ms)=Distribution(completed);
            (sample.cpuMedianMs,sample.cpuP95Ms)=Distribution(cpu);
            (sample.gpuMedianMs,sample.gpuP95Ms)=Distribution(gpu);
        }

        static double ElapsedMilliseconds(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;

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
            team.hullLevel=Mathf.Max(1,team.hullLevel);
            team.tank.Clear();
            team.tank.Add(new CatchItem(1101,4,0,2,1,1,species:"Needlefish"));
            team.tank.Add(new CatchItem(1102,6,1,3,2,1,species:"Sailray"));
            team.tank.Add(new CatchItem(1103,5,1,4,1,1,1,0,species:"Bristlefish"));
            foreach(var item in team.tank)
                if(!team.deliveredIds.Contains(item.id))team.deliveredIds.Add(item.id);
            team.Validate();
            Publish();
        }
    }

    public sealed partial class FishActor
    {
        float artPoseUntil;

        internal void SetArtMark(Vector3 center,Quaternion rotation)
        {
            // The sea staggers schools. Bring in the real actor before lining up the controlled shot.
            if(!InHabitat)SetDepthActive(true);
            artPoseUntil=Time.unscaledTime+.70f;
            _origin=center;
            transform.SetPositionAndRotation(center,rotation);
            var pose=_snapshot.Value;
            pose.Position=center;
            pose.Rotation=rotation;
            _snapshot.Value=pose;
        }
    }
}
#endif
