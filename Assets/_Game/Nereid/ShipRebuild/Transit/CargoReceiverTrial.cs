#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Raid.UI;

namespace Raid.Nereid
{
    // Optional paired Windows-player observation of a real replicated lift.
    [DefaultExecutionOrder(1500)]
    internal sealed partial class CargoReceiverTrial:MonoBehaviour
    {
        // InverseLerp is clamped. Only 1.0 is greater than the immediately preceding float.
        const float SeatedThreshold=.99999994f;
        const long First=2076301,Second=2076302;
        string directory,folder;
        bool host,initialized,started,dispatched,ended,seenRaise;
        Camera view;
        RenderTexture target;
        Texture2D photo;
        Transform fishA,fishB;
        CargoMeshClearance meshes;
        NereidCargoRail rail;
        readonly HashSet<string> photos=new();
        readonly List<string> timeline=new();
        float elapsed,startedAt,lastCheck=-1,fullAt=-1,entered=-1,departed=-1,closeAt=-1,maxPartial,minimumClosedGap=1000;
        float closestFunnel=float.PositiveInfinity,entryClearance,exitClearance;
        Renderer throat;
        int samples;
        readonly Dictionary<string,(MeshRenderer[] leaves,float closest)> otherDoors=new();
        string Number(float n)=>n.ToString("F3",CultureInfo.InvariantCulture);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-raid-hatch-probe");
            if(at<0 || at+1>=args.Length)return;
            if(Array.IndexOf(args,"-raid-save")<0 || Array.IndexOf(args,"-raid-profile")<0)
                throw new InvalidOperationException("The receiver probe requires a disposable team save and diver profile.");
            var trial=new GameObject("Recovery observation").AddComponent<CargoReceiverTrial>();
            trial.directory=Path.GetFullPath(args[at+1]);trial.host=Array.IndexOf(args,"-raid-host")>=0;
            trial.folder=Path.Combine(trial.directory,trial.host?"host":"peer");Directory.CreateDirectory(trial.folder);
        }

        void LateUpdate()
        {
            if(ended)return;
            try
            {
                if(Time.realtimeSinceStartup>195)throw new TimeoutException("Cargo observation timed out; samples="+samples);
                var w=RaidWorld.Instance;
                if(!w || w.NetworkObject==null || !w.container || !NereidVessel.Active || !w.IsClientInitialized)return;
                var ui=UiRoot.Instance;var player=ui ? ui.Context?.Player : null;
                if(!player || w.View==null)return;
                var shoal=w.container.GetComponentInChildren<WetCargoShoal>();if(!shoal)return;
                if(!initialized)
                {
                    rail=NereidVessel.Active.authoredRail;
                    if(!rail || !rail.Ready || !rail.cageAnchor)return;
                    meshes=new(rail);Setup(player);initialized=true;
                    if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("A rendered graphics device is required");
                    File.WriteAllText(Path.Combine(folder,"ready"),SystemInfo.graphicsDeviceType.ToString());
                }
                if(host && !started && DiverMotor.ServerDivers.Count>=2 && File.Exists(Path.Combine(directory,"peer","ready")))
                {
                    w.PrepareTransitFilm(First,Second);
                    started=true;startedAt=Time.realtimeSinceStartup;
                }
                var a=shoal.ModelFor(First);var b=shoal.ModelFor(Second);
                if(a && !fishA)fishA=a;if(b && !fishB)fishB=b;
                if(host && started && !dispatched && fishA && fishB && Time.realtimeSinceStartup-startedAt>.9f)
                {
                    dispatched=true;
                    if(!w.StartFilmHoist())throw new Exception("Host refused staged cage");
                }
                var phase=NereidVessel.Active.PresentedHoistPhase;float progress=NereidVessel.Active.HoistAnimation;
                ObserveDavit(phase, progress);
                if(!seenRaise && phase!=HoistPhase.Raising)return;
                if(!seenRaise){startedAt=Time.realtimeSinceStartup;seenRaise=true;}
                elapsed=Time.realtimeSinceStartup-startedAt;
                if(a!=fishA || b!=fishB || !a || !b)throw new Exception("A displayed fish changed identity");
                float open=Mathf.InverseLerp(rail.leftClosed.x,rail.DoorPosition(true,1).x,rail.leftDoor.localPosition.x);
                var box=meshes.VisibleCargoBounds();
                bool intersectsPlane=CargoMeshClearance.OccupiesAperture(rail,box,0,out float gap);
                bool near=CargoMeshClearance.OccupiesAperture(rail,box,.65f,out _);
                if(open>SeatedThreshold && fullAt<0 && phase==HoistPhase.Raising)fullAt=elapsed;
                if(phase==HoistPhase.Unloading)closestFunnel=Mathf.Min(closestFunnel,box.min.y-throat.bounds.max.y);
                if(intersectsPlane){
                    var local=rail.hatch.InverseTransformPoint(box.center);
                    float side=rail.hatch.TransformVector(Vector3.right*rail.aperture.x*.5f).magnitude
                        -Mathf.Abs(local.x)*rail.hatch.lossyScale.x-box.extents.x;
                    float fore=rail.hatch.TransformVector(Vector3.forward*rail.aperture.y*.5f).magnitude
                        -Mathf.Abs(local.z)*rail.hatch.lossyScale.z-box.extents.z;
                    float margin=Mathf.Min(side,fore);
                    if(entered<0){entered=elapsed;entryClearance=margin;}
                    exitClearance=margin;
                    departed=elapsed;maxPartial=Mathf.Max(maxPartial,1-open);
                }
                if(phase==HoistPhase.Returning && open<SeatedThreshold && closeAt<0)closeAt=elapsed;
                if(open<SeatedThreshold && near)minimumClosedGap=Mathf.Min(minimumClosedGap,gap);
                if(Time.realtimeSinceStartup-lastCheck>=.035f || phase==HoistPhase.Loading)
                {
                    samples++;lastCheck=Time.realtimeSinceStartup;
                    foreach(var id in new List<string>(otherDoors.Keys))
                    {
                        var probe=otherDoors[id];float nearest=probe.closest;
                        foreach(var leaf in probe.leaves){
                            Vector3 delta=Vector3.Max(Vector3.zero,Vector3.Max(box.min-leaf.bounds.max,leaf.bounds.min-box.max));
                            nearest=Mathf.Min(nearest,delta.magnitude);
                        }
                        otherDoors[id]=(probe.leaves,nearest);
                    }
                    meshes.Sample($"{phase} {Number(progress)} t={Number(elapsed)}");
                }
                if(phase==HoistPhase.Raising){
                    if(open<.01f && progress>.40f)Shot("01-approach",open,gap);
                    if(open>.01f)Shot("02-opening",open,gap);
                    if(open>.50f)Shot("03-half",open,gap);
                    if(open>SeatedThreshold)Shot("04-clear",open,gap);
                    if(intersectsPlane)Shot("05-leading",open,gap);
                }else if(phase==HoistPhase.Unloading && progress>.38f){
                    Shot("06-docked",open,gap);Shot("07-inside",open,gap,true);
                }else if(phase==HoistPhase.Returning){
                    if(entered>=0 && !intersectsPlane && gap>=0 && open>SeatedThreshold)Shot("08-trailing",open,gap);
                    if(open<SeatedThreshold)Shot("09-closing",open,gap);
                    if(open<.01f)Shot("10-sealed",open,gap);
                }else if(phase==HoistPhase.Loading){
                    if(shoal.TankCount!=2 || a!=fishA || b!=fishB)throw new Exception("Reservoir does not contain both original visible fish.");
                    timeline.Add($"Mesh samples {samples}; cargo renderers {meshes.CargoMeshes}; candidate structural renderers {meshes.StructuralMeshes}; full open {Number(fullAt)}s; leading edge enters plane {Number(entered)}s; trailing edge clears {Number(departed)}s; closing starts {Number(closeAt)}s; moving-lid violations of 0.65m reserve {(minimumClosedGap<999?"YES":"none")}; maximum missing opening inside plane {Number(maxPartial)}");
                    timeline.Add($"Entry lateral frame margin {Number(entryClearance)}m; exit frame margin {Number(exitClearance)}m; minimum visible open floor to receiver lip {Number(closestFunnel)}m");
                    timeline.AddRange(meshes.contacts);timeline.AddRange(meshes.unreadable);
                    foreach(var entry in otherDoors)timeline.Add(entry.Key+" minimum conservative clearance "+Number(entry.Value.closest)+"m");
                    timeline.Add("Cargo IDs "+First+" and "+Second+" in reservoir. GPU "+SystemInfo.graphicsDeviceName+"; server="+w.IsServerInitialized+" client="+w.IsClientInitialized);
                    bool pass=meshes.CargoMeshes>0 && meshes.StructuralMeshes>0 && otherDoors.Count==4 && meshes.contacts.Count==0 && meshes.unreadable.Count==0 && entered>fullAt && fullAt>=0 && closeAt>departed && maxPartial<.002f && minimumClosedGap>=999 && closestFunnel>.10f;
                    Finish(pass?"PASS":"FAIL");return;
                }
            }catch(Exception e){timeline.Add(e.ToString());Finish("ERROR");}
        }

        void Setup(DiverMotor motor)
        {
            var original=motor.GetComponentInChildren<Camera>();view=new GameObject("Observer at receiver").AddComponent<Camera>();
            view.CopyFrom(original);view.enabled=false;view.fieldOfView=55;
            view.GetUniversalAdditionalCameraData().renderPostProcessing=original.GetUniversalAdditionalCameraData().renderPostProcessing;
            target=new RenderTexture(1152,648,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            target.Create();view.targetTexture=target;photo=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            throat=rail.hatch.Find("Receiver throat").GetComponent<Renderer>();
            foreach(var door in FindObjectsByType<NereidPassageDoor>())
                AddDoor("Crew passage "+door.passage,door.portLeaf,door.starboardLeaf);
            var sea=FindAnyObjectByType<NereidPressureDoors>();
            if(sea){AddDoor("Airlock inner",sea.innerPort,sea.innerStarboard);AddDoor("Airlock outer",sea.outerPort,sea.outerStarboard);}
        }
        void AddDoor(string name,Transform port,Transform starboard)
        {
            var parts=new List<MeshRenderer>();parts.AddRange(port.GetComponentsInChildren<MeshRenderer>());
            parts.AddRange(starboard.GetComponentsInChildren<MeshRenderer>());if(parts.Count>0)otherDoors[name]=(parts.ToArray(),float.MaxValue);
        }
        void Shot(string mark,float amount,float gap,bool inside=false)
        {
            if(!photos.Add(mark))return;
            var hull=rail.transform;
            view.transform.position=hull.TransformPoint(inside?new Vector3(2.84f,1.65f,-6.53f):new Vector3(6.30f,11.48f,2.63f));
            view.transform.LookAt(hull.TransformPoint(inside?new Vector3(0,4.25f,-3.02f):new Vector3(0,5.03f,-3.77f)));
            var prev=RenderTexture.active;
            try{
                RenderPipeline.SubmitRenderRequest(view,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;photo.ReadPixels(new Rect(0,0,target.width,target.height),0,0);photo.Apply();
                File.WriteAllBytes(Path.Combine(folder,mark+".jpg"),photo.EncodeToJPG(91));
            }finally{RenderTexture.active=prev;}
            timeline.Add($"{mark} {Number(elapsed)}s  opening={Number(amount)}  cage underside to visible door top={Number(gap)}m  cage={rail.transform.InverseTransformPoint(rail.cageAnchor.position):F2}");
        }
        void Finish(string status)
        {
            ended=true;timeline.Insert(0,status);File.WriteAllLines(Path.Combine(folder,"recovery.txt"),timeline);
            Debug.Log("RECEIVER TRIAL "+status+" "+folder);
            StartCoroutine(ExitAfterPeer(status=="PASS"?0:1));
        }

        System.Collections.IEnumerator ExitAfterPeer(int code)
        {
            float limit=Time.realtimeSinceStartup+10f;
            while(host && code==0 && !File.Exists(Path.Combine(directory,"peer","recovery.txt")) && Time.realtimeSinceStartup<limit)yield return null;
            Application.Quit(code);
        }
    }
}
#endif
