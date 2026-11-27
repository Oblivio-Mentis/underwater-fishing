#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
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
            var shoal=FindAnyObjectByType<WetCargoShoal>();
            Check(shoal,"The live aquarium displays actual catch models");
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
                Check(world.threats.DefenseActive && world.threats.Defense.provoked,
                    "The active new ship raid was provoked by the kraken");

                Vector3 tank=factory.processRoute.WorldPoint(0);
                Vector3 tanker=factory.processRoute.WorldPoint(.012f)-tank;
                yield return StageTransfer(factory,false);
                yield return Wait("aquarium suction reaches full strength",()=>factory.Elapsed>.65f,4);
                BowFrame(camera,tank-tanker.normalized*1.3f+Vector3.up*.67f+right*.66f,tank-tanker.normalized*.28f,"pipe-suction.png");
                yield return Wait("water advances inside the processing pipe",()=>factory.Elapsed>3.65f,7);
                Check(VisiblePipeWater(factory.processRoute),"The processing pipe has a visible internal water mesh before discharge");
                Check(JoinedPipeWater(factory.processRoute),"One mesh connects the processing pipe bore and the falling water");
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
                yield return Wait("water reaches the final feeding sight glass",()=>factory.Elapsed>6.9f,13);
                Check(VisiblePipeWater(factory.feedRoute),"Water fills the feeding sight glass from the inside");
                Check(JoinedPipeWater(factory.feedRoute),"The feeding pipe bore shares actual mesh faces with the falling water");
                var barrier=mouth.Find("Mouth concealed throat barrier");
                var tunnel=mouth.Find("Deep living throat").GetComponent<MeshFilter>().sharedMesh;
                Check(barrier.GetComponent<Collider>().enabled && !barrier.GetComponent<MeshRenderer>().enabled &&
                    tunnel.bounds.max.z>4,"A curved four-metre gullet hides the collision barrier while keeping it solid");
                yield return Wait("full-size fish reach the tongue",()=>factory.Elapsed>7.41f,4);
                var fish=shoal.ModelFor(81010);
                Check(fish,"The first feeding fish is visible at the outlet");
                Vector3 originalSize=fish.localScale;
                var earlier=mouth.InverseTransformPoint(fish.position);
                BowFrame(camera,origin+forward*3.7f+right*1.13f+Vector3.up*.61f,
                    mouth.position+Vector3.up*.24f,"mouth-water-and-fish.png");
                yield return Wait("the same fish moves into the curved gullet",()=>factory.Elapsed>7.83f,4);
                Check(fish && shoal.ModelFor(81010)==fish && Vector3.Distance(originalSize,fish.localScale)<.0001f &&
                    mouth.InverseTransformPoint(fish.position).z>earlier.z+.8f,"The physical fish advances down the gullet at full size");
                BowFrame(camera,origin+forward*3.45f-right*.87f+Vector3.up*.48f,
                    mouth.TransformPoint(new Vector3(.04f,-.03f,1.35f)),"kraken-gullet.png");
                yield return Wait("feeding batch finishes",()=>!world.FishRoute.active,7);
                var runningWater=factory.feedRoute.transform.Find("Continuous pipe flow").GetComponent<MeshRenderer>();
                var discharge=new MaterialPropertyBlock();
                runningWater.GetPropertyBlock(discharge);
                Vector4 flow=discharge.GetVector("_Window");
                Check(runningWater.enabled && flow.x>flow.z+flow.w && flow.y<flow.z+flow.w,
                    "The same water surface keeps pouring after the final fish reaches the mouth");
                Check(world.Team.offeringPoints>foodBefore &&
                    !world.Team.tank.Exists(fish=>fish.id>=81000 && fish.id<=81013),
                    "Both routed batches completed and the fed fish contributed to the offering");
                Check(world.TrialBowSave(),"The completed batches left a valid campaign ledger");
                report.screenshot="kraken-fire.png";
            }
            finally { if(camera)Destroy(camera.gameObject); }
        }

        static bool VisiblePipeWater(Nereid.NereidPipeRoute route)
        {
            var water=route.transform.Find("Continuous pipe flow");
            return water && water.GetComponent<MeshRenderer>().enabled && water.GetComponent<MeshFilter>().sharedMesh.vertexCount>0;
        }

        static bool JoinedPipeWater(Nereid.NereidPipeRoute route)
        {
            var mesh=route.transform.Find("Continuous pipe flow").GetComponent<MeshFilter>().sharedMesh;
            var channels=new List<Vector4>();
            mesh.GetUVs(1,channels);
            var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                float a=channels[triangles[i]].x,b=channels[triangles[i+1]].x,c=channels[triangles[i+2]].x;
                if(Mathf.Min(a,b,c)<=1 && Mathf.Max(a,b,c)>1)return true;
            }
            return false;
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
