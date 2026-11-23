#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Optional camera for recording the actual host simulation in a disposable QA session.
    // Track the displayed catch by CargoId throughout the hoist and both downstream routes.
    internal static class NereidTransitFilm
    {
        const long ProcessId = 20301, FeedId = 20302;
        public static IEnumerator Record(RaidWorld world, Camera playerCamera, string folder)
        {
            if (!world.IsServerInitialized) throw new InvalidOperationException("A local QA host is required.");
            var hull = NereidVessel.Active.transform;
            var shoal = UnityEngine.Object.FindAnyObjectByType<WetCargoShoal>();
            if (!shoal) throw new InvalidOperationException("The cage has no live catch presenter.");
            var camera = new GameObject("Transit inspection camera").AddComponent<Camera>();
            camera.CopyFrom(playerCamera);
            camera.enabled = false;
            camera.fieldOfView = 59;
            var target = RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            var image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            var request = new RenderPipeline.StandardRequest {destination=target};
            int previousRate = Time.captureFramerate;
            try
            {
                Time.captureFramerate = 24;
                world.PrepareTransitFilm(ProcessId,FeedId);
                yield return new WaitForSeconds(.6f);
                var first = shoal.ModelFor(ProcessId); var second = shoal.ModelFor(FeedId);
                if (!first || !second) throw new InvalidOperationException("No physical models for the staged catches.");
                if (!world.StartFilmHoist()) throw new InvalidOperationException("Hoist rejected loaded catches.");
                int index = 0;
                for(int segment=0;segment<3;segment++)
                {
                    long id = segment == 2 ? FeedId : ProcessId;
                    Transform model = segment == 2 ? second : first;
                    if (segment > 0 && !world.StartFilmDispatch(id,segment==2))
                        throw new InvalidOperationException("Confirmed draft did not enter requested pipe.");
                    string name = new[]{"01-hoist-inlet","02-processing","03-feeding"}[segment];
                    string dir = Path.Combine(folder,name); Directory.CreateDirectory(dir);
                    int frame = 0;
                    float start = Time.time;
                    do
                    {
                        yield return null;
                        if (Time.time-start > 70) throw new TimeoutException(name);
                        if (model)
                        {
                            if (model != shoal.ModelFor(id)) throw new InvalidOperationException("Catch model changed during transit.");
                            Vector3 focus = model.position;
                            Vector3 offset = segment == 0 && world.Team.hoistPhase == HoistPhase.Raising
                                ? new Vector3(4.3f,1.5f,-4.2f) : new Vector3(1.4f,.55f,-1.4f);
                            Vector3 desired = focus + hull.TransformDirection(offset);
                            if (Physics.Linecast(focus,desired,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                                obstruction.distance > .4f) desired = Vector3.MoveTowards(obstruction.point,focus,.16f);
                            camera.transform.position = frame == 0 ? desired : Vector3.Lerp(camera.transform.position,desired,1-Mathf.Exp(-Time.deltaTime*9));
                            camera.transform.LookAt(focus,Vector3.up);
                        }
                        var old = RenderTexture.active;
                        try
                        {
                            RenderPipeline.SubmitRenderRequest(camera,request);
                            RenderTexture.active=target;
                            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);
                            File.WriteAllBytes(Path.Combine(dir,frame.ToString("D5")+".jpg"),image.EncodeToJPG(87));
                        }
                        finally { RenderTexture.active=old; }
                        frame++; index++;
                    } while (segment==0 ? (world.Team.hoistPhase is HoistPhase.Raising or HoistPhase.Unloading) :
                        (world.FishRoute.active || world.Team.tank.Exists(x => x.id==id)));
                    if(segment==0 && (shoal.ModelFor(ProcessId)!=first || shoal.ModelFor(FeedId)!=second))
                        throw new InvalidOperationException("Unloading replaced a displayed fish.");
                    File.WriteAllText(Path.Combine(dir,"take.txt"),$"{frame} consecutive game frames at 24 fps");
                }
                Debug.Log($"[NEREID FILM] Recorded {index} frames for cargo {ProcessId} and {FeedId}.");
            }
            finally
            {
                Time.captureFramerate = previousRate; camera.targetTexture=null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(camera.gameObject);
            }
        }
    }
}

namespace Raid
{
    public sealed partial class RaidWorld
    {
        internal void PrepareTransitFilm(long first,long second)
        {
            if (!team.LoadCargo(new CatchItem(first,3,1,2,1,1,species:"Bristlefish"),progression) ||
                !team.LoadCargo(new CatchItem(second,2,1,3,1,1,species:"Needlefish"),progression))
                throw new InvalidOperationException("Film requires an empty working cage.");
            Publish();
        }
        internal bool StartFilmHoist() { bool ok=team.BeginHoist(progression); Publish(); return ok; }
        internal bool StartFilmDispatch(long id,bool feed)
        {
            var destination=feed ? CatchDestination.Feed : CatchDestination.Process;
            bool ok=ChooseFishRoute(id,destination) && CommitFishDispatch(destination);
            Publish(); return ok;
        }
    }
}
#endif
