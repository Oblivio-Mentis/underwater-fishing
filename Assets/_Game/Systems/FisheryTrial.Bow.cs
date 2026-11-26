#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using Raid.Factory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed partial class FisheryTrial
    {
        IEnumerator BowAndPipeTrial()
        {
            var factory=FindAnyObjectByType<FactoryVisuals>();
            Check(factory && factory.upperJaw && factory.feedThroat && factory.feedRoute && factory.processRoute,
                "The authored mouth and both physical pipe routes are present");
            var mouth=factory.upperJaw.parent;
            var origin=mouth.TransformPoint(new Vector3(0,-.12f,-1.03f));
            var forward=-mouth.forward;
            var right=mouth.right;
            var camera=new GameObject("RAID bow QA camera").AddComponent<Camera>();
            camera.CopyFrom(host.GetComponentInChildren<Camera>());
            camera.enabled=false;
            camera.fieldOfView=61;

            try
            {
                float hostHealth=host.Health, peerHealth=peer?peer.Health:0;
                host.Teleport(origin+forward*3.5f-Vector3.up*.9f);
                if(peer)peer.Teleport(origin+forward*5f+right*4.6f-Vector3.up*.9f);
                host.LookAtConsole(origin);
                var charge=Stage("charge",mouth.TransformPoint(new Vector3(0,-.12f,-2.24f)));
                charge.mode="fuse";charge.timer=3.7f;charge.anchor=mouth.forward*10;
                yield return Wait("live charge crosses the mouth aperture",()=>world.BowBreath.sequence>0,3);
                Check(charge.mode=="spent" && charge.anchor==Vector3.zero,"Swept live charge was swallowed and consumed");
                Check(host.Health==hostHealth && (!peer || peer.Health==peerHealth),"The swallowed charge deals no ordinary pulse");
                yield return Wait("clients see the fire jet",()=>FindAnyObjectByType<Nereid.KrakenFlameFx>()!=null &&
                    world.BreathClock-world.BowBreath.startedAt>KrakenBreathState.Ignite+.29f,4);
                BowFrame(camera,origin+forward*5.7f+right*2.15f+Vector3.up*.72f,origin+forward*2.3f,"kraken-fire.png");
                bool hitHost=hostHealth-host.Health>35;
                Check(hitHost,$"Exposed host struck by the fire ({hostHealth:0.0} to {host.Health:0.0} health at {host.transform.position})");
                if(peer)
                {
                    Check(peer.Health==peerHealth,"Second player outside the cone remained unhurt");
                    peer.Teleport(origin+forward*5.6f-right*.32f-Vector3.up*.9f);
                    yield return Wait("the second diver enters the same flame",()=>peerHealth-peer.Health>36,1.1f);
                    Check(true,"Both independent players suffered server-authoritative flame damage");
                }
                yield return Wait("new raid after the angry roar",()=>world.threats.WaveActive,6);
                Check(world.threats.Message.Contains("рев") || world.threats.Message.Contains("рёв"),
                    "The active new ship raid was provoked by the kraken");

                Vector3 tank=factory.processRoute.WorldPoint(0);
                Vector3 tanker=factory.processRoute.WorldPoint(.012f)-tank;
                yield return StageTransfer(factory,false);
                yield return Wait("aquarium suction reaches full strength",()=>factory.Elapsed>.65f,4);
                BowFrame(camera,tank-tanker.normalized*1.3f+Vector3.up*.67f+right*.66f,tank-tanker.normalized*.28f,"pipe-suction.png");
                yield return Wait("processing outlet expels fish and water",()=>factory.Elapsed>5.28f,8);
                var outlet=factory.processRoute.WorldPoint(1);
                BowFrame(camera,outlet+Vector3.up*.45f+right*1.82f+forward*1.03f,
                    Vector3.Lerp(outlet,factory.processorDrop.position,.49f),"processing-water.png");
                yield return Wait("the grinder contacts multiple fish",()=>factory.Elapsed>7.83f,7);
                BowFrame(camera,factory.processorIntake.position+Vector3.up*.82f+right*.58f+forward*1.64f,
                    factory.processorIntake.position+Vector3.down*.12f,"grinder-fragments.png");
                yield return Wait("processing batch finishes",()=>!world.FishRoute.active,7);
                int foodBefore=world.Team.offeringPoints;
                yield return StageTransfer(factory,true);
                yield return Wait("fish land on the tongue under the feeding outlet",()=>factory.Elapsed>7.64f,14);
                BowFrame(camera,origin+forward*3.7f+right*1.13f+Vector3.up*.61f,
                    mouth.position+Vector3.up*.24f,"mouth-water-and-fish.png");
                yield return Wait("feeding batch finishes",()=>!world.FishRoute.active,7);
                var runningWater=factory.transform.Find("Feeding pipe water discharge")?.GetComponent<MeshRenderer>();
                Check(runningWater && runningWater.enabled,"Water continues to pour after the final fish reaches the mouth");
                Check(world.Team.offeringPoints>foodBefore &&
                    !world.Team.tank.Exists(fish=>fish.id>=81000 && fish.id<=81013),
                    "Both routed batches completed and the fed fish contributed to the offering");
                Check(world.TrialBowSave(),"The completed batches left a valid campaign ledger");
                report.screenshot="kraken-fire.png";
            }
            finally { if(camera)Destroy(camera.gameObject); }
        }

        IEnumerator StageTransfer(FactoryVisuals factory,bool feeding)
        {
            Check(world.StageBowTransfer(feeding),feeding?"Feeding batch dispatched":"Processing batch dispatched");
            yield return Wait("batch accepted by the presentation",()=>
                world.FishRoute.active && world.FishRoute.feeding==feeding &&
                factory.IsPlaying && factory.IsProcessing!=feeding,4);
        }

        void BowFrame(Camera camera,Vector3 eye,Vector3 focus,string name)
        {
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye,Vector3.up));
            var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                camera.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);
                File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old;camera.targetTexture=null;
                RenderTexture.ReleaseTemporary(target);Destroy(image);
            }
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool TrialBowSave()=>SaveTransaction();

        internal bool StageBowTransfer(bool feeding)
        {
            int first=feeding?81010:81000;
            var destination=feeding?CatchDestination.Feed:CatchDestination.Process;
            for(int n=0;n<4;n++)
            {
                var fish=new CatchItem(first+n,2,1,3,1,1,species:n%2==0?"Bristlefish":"Needlefish");
                if(!team.Deposit(fish,progression))return false;
                if(!ChooseFishRoute(fish.id,destination))return false;
            }
            bool started=CommitFishDispatch(destination);
            Publish();
            return started;
        }
    }
}
#endif
