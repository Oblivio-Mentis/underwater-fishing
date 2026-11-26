#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid
{
    // Explicit disposable host scenario. Spawns/positions are staged, interactions and catches use the server simulation.
    internal sealed partial class FisheryTrial : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public string status="running", phase, error, screenshot;
            public bool twoDivers;
            public long chargeCatch, trapCatch, monsterCatch, netCatch, vacuumCatch;
            public int vacuumSerial;
            public Vector3 vacuumOutlet;
            public List<string> checks=new();
        }

        readonly Report report=new();
        string output;
        bool requirePeer;
        RaidWorld world;
        DiverMotor host, peer;
        FishActor sample;
        Vector3 ground;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string option)
            {
                int i=Array.IndexOf(args,option);
                return i>=0 && i+1<args.Length?args[i+1]:null;
            }
            string folder=Arg("-raid-fishery-trial"), save=Arg("-raid-save");
            if(string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(save) || !args.Contains("-raid-host"))return;
            // A trial can never overwrite an existing campaign or use the ordinary player save.
            string path=Path.GetFullPath(save);
            if(File.Exists(path) || path.StartsWith(Path.GetFullPath(Application.persistentDataPath)+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            {Debug.LogError("[RAID-020] Fishery trial requires a fresh disposable save outside the player data directory.");return;}
            var test=new GameObject("RAID-020 active fishery trial").AddComponent<FisheryTrial>();
            DontDestroyOnLoad(test.gameObject);
            test.output=Path.GetFullPath(folder);
            test.requirePeer=args.Contains("-raid-fishery-duo");
            Directory.CreateDirectory(test.output);
            test.StartCoroutine(test.Drive());
        }

        IEnumerator Drive()
        {
            var stack=new Stack<IEnumerator>();
            stack.Push(Run());
            while(stack.Count>0)
            {
                object next;
                try
                {
                    var task=stack.Peek();
                    if(!task.MoveNext()){stack.Pop();continue;}
                    next=task.Current;
                    if(next is IEnumerator nested){stack.Push(nested);continue;}
                }
                catch(Exception e)
                {
                    report.status="failed";report.error=e.ToString();Write();
                    Debug.LogError("[RAID-020] "+report.phase+": "+e.Message);
                    yield break;
                }
                yield return next;
            }
            report.phase="done";report.status="passed";Write();
            Debug.Log("[RAID-020] Active fishery trial passed with "+report.checks.Count+" checks.");
        }

        void Write() => File.WriteAllText(Path.Combine(output,"fishery.json"),JsonUtility.ToJson(report,true));
        void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException(label);
            report.checks.Add(label);Write();
        }
        IEnumerator Wait(string phase,Func<bool> ready,float seconds=30f)
        {
            report.phase=phase;Write();
            float end=Time.realtimeSinceStartup+seconds;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=end)throw new TimeoutException(phase);
                yield return null;
            }
        }

        IEnumerator Run()
        {
            yield return Wait("authoritative expedition, reef and crew",()=>
            {
                world=RaidWorld.Instance;
                if(!world || !world.NetworkObject || !world.IsServerInitialized || world.Team==null || world.TravelRemaining>0 ||
                    !world.threats || !world.threats.NetworkObject || !world.threats.Reef.Active)return false;
                host=DiverMotor.ServerDivers.FirstOrDefault(d=>d && d.IsOwner && d.IsServerInitialized);
                peer=DiverMotor.ServerDivers.FirstOrDefault(d=>d && !d.IsOwner && d.IsServerInitialized);
                return host && (!requirePeer || peer);
            },120);
            report.twoDivers=peer;
            yield return Wait("a live school in open water",()=>
            {
                foreach(var candidate in FishActor.ServerSchool)
                {
                    if(!candidate || candidate.depthTier!=world.CurrentDepth || !candidate.CanHarvestRig(false))continue;
                    var at=candidate.transform.position;
                    for(int y=2;y<=6;y+=2)
                    {
                        var open=at+Vector3.up*y;
                        if(!MarineSpawn.InOpenWater(world.CurrentDepth,open,2.75f) || Vector3.Distance(open,world.CargoLoadingPosition)<14)continue;
                        sample=candidate;ground=open;return true;
                    }
                }
                return false;
            },90);
            host.Teleport(ground+Vector3.back*2);
            if(peer)peer.Teleport(ground+Vector3.right*2);
            Check(!host.IsInside && (!peer || !peer.IsInside),"crew is in the water");
            yield return Charge();
            yield return Trap();
            yield return Cage();
            yield return CageDelivery();
            if(peer)yield return Nets();
            yield return CrabHaulTrial();
            yield return GoshaHatchTrial();
        }

        TechRig Stage(string kind,Vector3 point)
        {
            // The catalog UI/purchase flow has separate coverage. Start with one physical unit on the ground.
            var rig=world.Team.AddRig(kind,point,-1);
            rig.Detach();
            return rig;
        }

        long FishAt(Vector3 point)
        {
            sample.SetDepthActive(true);
            sample.transform.position=point;
            Physics.SyncTransforms();
            return sample.CargoId;
        }

        bool Use(TechRig rig,DiverMotor player,string action="use") => world.TrialUseFishery(rig,player,action);

        IEnumerator Charge()
        {
            report.phase="local charge, drop and pickup";Write();
            var charge=Stage("charge",ground);
            host.Teleport(ground+Vector3.back*2.1f);
            if(peer)peer.Teleport(ground+Vector3.forward*.95f);
            var id=FishAt(ground+Vector3.right*.6f);
            float hp=host.Health;
            Check(!Use(charge,host,"alternate") && !Use(charge,host),"a placed charge rejects old remote activation");
            charge.mode="fuse";charge.timer=TechCatalog.Find("charge").interval;charge.anchor=Vector3.zero;
            // Select the normal preserved-fish outcome and leave the world's random stream untouched.
            var stream=UnityEngine.Random.state;
            try
            {
                for(int seed=0;;seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if(UnityEngine.Random.value>.1f)continue;
                    UnityEngine.Random.InitState(seed);
                    break;
                }
                world.TrialTickFishery(charge,TechCatalog.Find("charge").interval+.02f);
            }
            finally{UnityEngine.Random.state=stream;}
            var drop=Enumerable.Range(0,world.DroppedCount).Select(world.DroppedAt).FirstOrDefault(x=>x.id==id);
            Check(charge.mode=="spent" && host.Health<hp,"pulse consumes the charge and hurts the nearby diver");
            Check(drop.id==id && drop.Fish==sample && !sample.IsFreeSwimming && drop.dead && !drop.spoiled,
                "affected school fish becomes an identifiable corpse and preserves the same catch ID");
            if(peer)
            {
                Check(peer.ObservedBlast.Velocity.magnitude>30 && peer.ObservedBlast.Velocity.y>6,
                    "the diver next to the epicentre receives a strong upward and outward launch");
                yield return Wait("observer simulates the remote diver blast ragdoll",
                    ()=>peer.GetComponent<DiverSuitVisual>() is DiverSuitVisual visual && visual.HasBlastPose,1.2f);
            }
            Vector3 originalPosition=drop.position;
            DroppedItem Current() => Enumerable.Range(0,world.DroppedCount).Select(world.DroppedAt).FirstOrDefault(x=>x.id==id);
            yield return Wait("the fish drifts away from the explosion and rotates onto its side",
                ()=>Vector3.Distance(Current().position,originalPosition)>.55f && Mathf.Abs(Current().roll)>89,3);
            drop=Current();
            host.Teleport(drop.position-host.AimDirection*1.15f-Vector3.up*.6f);
            yield return Wait("a white outline appears around the nearby dead fish",()=>world.TrialFishOutline(id),3);
            host.LookAtConsole(Current().position);
            yield return null;
            Raid.UI.UiQaProbe.CapturePeerCamera(Path.Combine(output,"blast-catch-outline.png"));
            drop=Current();
            host.Teleport(drop.position-host.AimDirection*1.05f-Vector3.up*.6f);
            Physics.SyncTransforms();
            var tool=host.GetComponent<HarpoonTool>();
            bool visible=world.TryAimDrop(host,out var aimed);
            Check(visible && aimed.id==id,"owner sight targets the dropped catch (wanted "+id+", sight "+aimed.id+", available "+tool.CanPickUp(drop)+")");
            // The two QA player windows run in the background. Submit the same owner RPC as E without an OS focus dependency.
            tool.FisheryTrialPickup(id);
            yield return Wait("dropped fish in owner's inventory",()=>Enumerable.Range(2,tool.PocketCapacity).Any(slot=>tool.CatchIdInSlot(slot)==id),10);
            Check(!Enumerable.Range(0,world.DroppedCount).Any(i=>world.DroppedAt(i).id==id),"pickup retires exactly the same world drop");
            report.chargeCatch=id;
        }

        IEnumerator Trap()
        {
            report.phase="trap in fishing ground and manual cargo delivery";Write();
            var trap=Stage("trap",ground);
            var id=FishAt(ground+Vector3.right*.08f);
            world.TrialTickFishery(trap,TechCatalog.Find("trap").interval+1);
            Check(trap.contents.Count==1 && trap.contents[0].id==id && trap.localStock?.id==id,"trap catches a real free fish and remembers the sampled local species");
            report.trapCatch=id;
            var offshore=Stage("trap",ground);
            offshore.depth=world.CurrentDepth==1?2:1;
            float interval=TechCatalog.Find("trap").interval;
            world.TrialTickFishery(offshore,interval*3);
            Check(offshore.contents.Count==0,"a remote unsampled installation produces no fish");
            // Continue the same productive installation at an inactive depth for a simulated crew voyage.
            int realDepth=trap.depth;
            trap.depth=offshore.depth;world.TrialTickFishery(trap,interval*.25f);
            Check(trap.contents.Count==1,"an inactive-depth trap observes its slower interval");
            world.TrialTickFishery(trap,interval*3);
            Check(trap.contents.Count==2 && trap.contents[1].species==trap.localStock.species && trap.contents[1].id!=id,
                "a previously sampled site produces a uniquely marked catch during the expedition");
            trap.depth=realDepth;
            host.Teleport(ground+Vector3.back*.8f);Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            host.CmdTechUse(trap.serial,"unload");
            TechRig basket=null;
            yield return Wait("basket created by owner command",()=> (basket=world.Team.rigs.Find(r=>r.kind=="basket" && r.Carrying(host.OwnerId)))!=null,8);
            Check(trap.contents.Count==0 && basket.contents.Count(c=>c.id==id)==1,"manual unloading moves the original catch to the diver's basket");
            host.Teleport(world.CargoLoadingPosition);
            basket.position=world.CargoLoadingPosition;
            yield return new WaitForSecondsRealtime(.3f);
            host.CmdTechUse(basket.serial,"unload");
            yield return Wait("unique trap catch entered the ship's lift",()=>world.Team.cargo.Any(c=>c.id==id),8);
            Check(world.Team.cargo.Count(c=>c.id==id)==1 && !world.Team.rigs.Contains(basket),"ship accepts the catch once and consumes the empty basket");
        }

        IEnumerator Cage()
        {
            report.phase="volunteer, closure timing, catch and escape";Write();
            var cage=Stage("cage",ground);
            var local=ground+Vector3.up*ActiveFisheryTuning.Get.cageTetherHeight;
            host.Teleport(ground+Vector3.right*.8f);
            var trigger=peer?peer:host;
            if(peer)peer.Teleport(ground+Vector3.back*2);
            world.threats.StageFisheryTrialAngler(local+Vector3.forward*10);
            Check(Use(cage,host) && world.IsCageBait(host),"volunteer arms the cage");
            Check(Use(cage,trigger) && cage.mode=="closing","operator can close the armed cage");
            world.TrialTickFishery(cage,TechCatalog.Find("cage").interval+.01f);
            Check(cage.mode=="closed" && cage.contents.Count==0 && !world.IsCageBait(host),"early closure misses and releases the volunteer");
            world.TrialTickFishery(cage,ActiveFisheryTuning.Get.cageLatchCooldown+1);
            Check(Use(cage,host) && Use(cage,host,"alternate") && cage.mode=="placed" && !world.IsCageBait(host),
                "a volunteer can use the emergency release alone");
            if(peer)
            {
                Check(Use(cage,host) && cage.operatorId==peer.OwnerId && !Use(cage,host),
                    "the volunteer cannot trigger a remote held by another owner");
                Check(Use(cage,peer,"alternate") && cage.operatorId==host.OwnerId && cage.mode=="armed" &&
                    !Use(cage,peer) && Use(cage,host),"the operator hands the remote to the volunteer and loses trigger authority");
                Check(Use(cage,host,"alternate") && cage.mode=="placed","the volunteer can abort even while the jaws are closing");
            }
            Check(Use(cage,host),"volunteer re-arms after emergency release");
            world.threats.StageFisheryTrialAngler(local);
            Check(Use(cage,trigger),"operator signals closure while the creature is inside");
            world.threats.StageFisheryTrialAngler(local+Vector3.forward*10);
            world.TrialTickFishery(cage,TechCatalog.Find("cage").interval+.01f);
            Check(cage.mode=="closed" && cage.contents.Count==0,"a creature that leaves before the jaws meet escapes a late command");
            world.TrialTickFishery(cage,ActiveFisheryTuning.Get.cageLatchCooldown+1);
            world.threats.StageFisheryTrialAngler(local);
            Check(Use(cage,host) && Use(cage,trigger),"crew arms and closes the next cage");
            world.TrialTickFishery(cage,TechCatalog.Find("cage").interval+.01f);
            Check(cage.mode=="captured" && cage.contents.Count==1 && cage.contents[0].species=="ReefAngler" &&
                !world.IsCageBait(host) && !world.threats.CageAnimalNear(ActiveFisheryTuning.Get.monsters[0],local,5),
                "timed closure transfers the living angler from the predator director into one cage");
            report.monsterCatch=cage.contents[0].id;
            host.Teleport(ground+new Vector3(2,.8f,-2.4f));
            if(peer)peer.Teleport(ground+Vector3.right*2.3f);
            host.LookAtConsole(cage.position+Vector3.up*.5f);
            world.PublishFisheryTrial();
            yield return new WaitForSecondsRealtime(.6f);
            host.LookAtConsole(cage.position+Vector3.up*.5f);
            yield return new WaitForSecondsRealtime(.2f);
            yield return Wait("replicated cage rendered at the fishing ground",()=>world.VisibleFisheryTrialRig(cage.serial),10);
            CaptureCage(cage);
            float before=cage.health;
            world.TrialTickFishery(cage,5);
            Check(cage.health<before && cage.contents.Count==1,"a live captive strains its cage");
            world.TrialTickFishery(cage,ActiveFisheryTuning.Get.cageEscapeSeconds*2);
            Check(cage.mode=="damaged" && cage.contents.Count==0 && world.threats.CageAnimalNear(ActiveFisheryTuning.Get.monsters[0],local,5),
                "a failed cage releases its captive back into the real predator director");
        }

        void CaptureCage(TechRig cage)
        {
            var original=host.GetComponentInChildren<Camera>();
            var observer=new GameObject("Underwater fishery trial camera").AddComponent<Camera>();
            var target=RenderTexture.GetTemporary(1152,648,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            var active=RenderTexture.active;
            try
            {
                observer.CopyFrom(original);
                observer.GetUniversalAdditionalCameraData().renderPostProcessing=original.GetUniversalAdditionalCameraData().renderPostProcessing;
                observer.enabled=false;observer.fieldOfView=49;
                observer.transform.position=cage.position+new Vector3(1.75f,1.15f,-2.35f);
                observer.transform.LookAt(cage.position+Vector3.up*.17f);
                observer.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(observer,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                int low=765,high=0;
                var pixels=image.GetPixels32();
                for(int i=0;i<pixels.Length;i+=13)
                {
                    var pixel=pixels[i];int light=pixel.r+pixel.g+pixel.b;
                    low=Math.Min(low,light);high=Math.Max(high,light);
                }
                Check(high-low>45,"underwater camera renders actual scene detail into the captured frame");
                report.screenshot="captured-angler.png";
                File.WriteAllBytes(Path.Combine(output,report.screenshot),image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=active;
                observer.targetTexture=null;
                RenderTexture.ReleaseTemporary(target);Destroy(image);Destroy(observer.gameObject);
            }
        }

        void GrantModule(string kind,string module,int rank=1)
        {
            if(world.Team.TechRank(kind,module)>=rank)return;
            world.Team.techPurchases.Add(new TechPurchase{item=kind,upgrade=module,rank=rank});
        }

        void Hoist(long id)
        {
            Check(world.Team.BeginHoist(world.progression),"cargo lift accepts the monster for delivery");
            world.Team.TickHoist(100f,world.progression);
            Check(world.Team.tank.Count(f=>f.id==id)==1 && world.Team.deliveredIds.Count(i=>i==id)==1,
                "one live captive reaches the common ship reservoir");
        }

        IEnumerator CageDelivery()
        {
            report.phase="team towing and processing, followed by automatic capture and robot delivery";Write();
            var hand=Stage("cage",ground);
            host.Teleport(ground+Vector3.left*1.25f);
            if(peer)peer.Teleport(ground+Vector3.right*1.25f);
            world.threats.StageFisheryTrialAngler(ground+Vector3.up*ActiveFisheryTuning.Get.cageTetherHeight);
            Check(Use(hand,host) && Use(hand,peer?peer:host),"working crew secures a second live target");
            world.TrialTickFishery(hand,TechCatalog.Find("cage").interval+.1f);
            long processed=hand.contents.Single().id;
            yield return new WaitForSecondsRealtime(.3f);
            host.CmdTechUse(hand.serial,"carry");
            yield return Wait("diver attached for manual cage tow",()=>hand.Carrying(host.OwnerId),8);
            if(peer)
            {
                world.UseTechnology(peer,hand.serial,"carry");
                yield return Wait("second diver attached for manual cage tow",()=>hand.Carrying(peer.OwnerId),8);
                Vector3 before=hand.position;
                host.Teleport(before+new Vector3(-.8f,.5f,1.6f));
                peer.Teleport(before+new Vector3(.8f,.5f,1.6f));
                yield return Wait("two divers move the occupied cage",()=>Vector3.Distance(before,hand.position)>.4f,5);
                Check(hand.helperId==peer.OwnerId && hand.contents.Single().id==processed &&
                    Mathf.Abs(host.equipmentPace-world.tuning.teamTowSpeed)<.01f,
                    "both real owners share the occupied cage and its team towing speed");
            }
            // The verified tow above uses normal movement. Shorten the empty swim between reef and freight berth.
            var dock=world.CargoLoadingPosition;
            host.Teleport(dock);if(peer)peer.Teleport(dock+Vector3.right*.8f);
            hand.position=dock;
            yield return new WaitForSecondsRealtime(.3f);
            host.CmdTechUse(hand.serial,"unload");
            yield return Wait("manual captive at cargo berth",()=>world.Team.cargo.Any(c=>c.id==processed),8);
            Check(hand.contents.Count==0,"manual delivery empties the towed cage");
            Hoist(processed);
            int biomass=world.Team.biomass,chitin=world.Team.chitin;
            Check(world.Team.CreditProcessedCatch(processed,world.progression,out var materials) &&
                materials.biomass>0 && materials.chitin>0 && world.Team.biomass>biomass && world.Team.chitin>chitin &&
                !world.Team.Feed(processed),"processing a delivered reef angler pays materials once");
            world.Team.rigs.Remove(hand);

            var automatic=Stage("cage",ground);
            host.Teleport(ground+Vector3.left);
            Check(!Use(automatic,host,"alternate"),"base cage rejects an artificial lure");
            GrantModule("cage","unlock");GrantModule("cage","select");
            world.threats.StageFisheryTrialAngler(ground+Vector3.up*ActiveFisheryTuning.Get.cageTetherHeight);
            world.TrialTickFishery(automatic,.1f);
            Check(automatic.mode=="placed" && Use(automatic,host),"a purchased sensor alone still needs a volunteer");
            world.TrialTickFishery(automatic,.1f);
            Check(automatic.mode=="closing" && Use(automatic,host,"alternate"),"sensor recognizes the live angler and starts closure with a volunteer");
            GrantModule("cage","bait");GrantModule("cage","auto");GrantModule("cage","delivery");
            world.TrialTickFishery(automatic,.1f);
            Check(automatic.mode=="closing" && automatic.baitId<0 && !world.IsCageBait(host),"autonomous mode and artificial bait start a real catch without a volunteer");
            world.TrialTickFishery(automatic,TechCatalog.Find("cage").interval+.1f);
            long fed=automatic.contents.Single().id;
            Check(automatic.mode=="captured" && fed!=processed,"autonomous capture yields a separate live monster");

            // Find a physically unobstructed approach to the actual loading berth for the robot and the loaded cage.
            Vector3 berth=dock+Vector3.up;
            Vector3 start=default;
            bool clear=false;
            for(int height=0;height<=3 && !clear;height++)
                for(int i=0;i<16 && !clear;i++)
                {
                    var candidate=berth+Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*5+Vector3.up*height;
                    if(!RaidWorld.IndustryCanPull(candidate,berth,.6f) || ReefHabitats.InShelter(candidate))continue;
                    start=candidate;clear=true;
                }
            Check(clear,"cargo berth has a clear underwater approach for towing");
            automatic.position=start;
            GrantModule("robot","unlock");GrantModule("robot","auto");GrantModule("robot","capacity",2);GrantModule("robot","delivery");
            var robot=Stage("robot",start+Vector3.up*.65f);
            Check(world.TrialUseIndustry(robot,host,"alternate"),"operator starts the equipped autonomous transport robot");
            bool towed=false;
            for(int i=0;i<300 && automatic.contents.Count>0;i++)
            {
                world.TrialTickIndustry(robot,.05f);
                towed|=robot.targetSerial==automatic.serial;
            }
            Check(towed && Vector3.Distance(automatic.position,start)>.5f && automatic.contents.Count==0 &&
                world.Team.cargo.Count(f=>f.id==fed)==1,"robot physically tows the occupied cage and loads its unique captive into the lift");
            Hoist(fed);
            int food=world.Team.offeringPoints;
            Check(world.Team.Feed(fed,world.progression) && world.Team.offeringPoints>food && !world.Team.Process(fed),
                "the second industrial catch feeds the crew's offering reserve exactly once");
        }

        IEnumerator Nets()
        {
            report.phase="two network owners close a seine and verify autonomous suction";Write();
            var net=Stage("net",ground);
            host.Teleport(ground+new Vector3(-.55f,0,-2));peer.Teleport(ground+new Vector3(.55f,0,-2));
            Check(Use(net,host) && Use(net,peer) && net.operatorId!=net.helperId && net.helperId==peer.OwnerId,"two connected owners take opposite ends of the seine");
            world.TrialTickFishery(net,.02f);
            Check(!Use(net,host,"alternate"),"a net that has not enclosed a school cannot be closed");
            host.Teleport(ground+new Vector3(-2,0,0));peer.Teleport(ground+new Vector3(2,0,0));
            world.TrialTickFishery(net,.02f);
            host.Teleport(ground+new Vector3(-.55f,0,2));peer.Teleport(ground+new Vector3(.55f,0,2));
            world.TrialTickFishery(net,.02f);
            long id=FishAt(ground);
            Check(Use(net,host,"alternate") && net.contents.Any(c=>c.id==id) && !sample.IsFreeSwimming,"manual closure takes a school fish inside the swept polygon");
            report.netCatch=id;
            var vacuum=Stage("vacuum",ground);
            host.Teleport(ground+Vector3.back*6);peer.Teleport(ground+Vector3.right*6);
            id=FishAt(vacuum.position+Vector3.up*TechRigShell.VacuumInletHeight+Vector3.left*.1f);
            for(int i=0;i<30 && vacuum.contents.Count==0;i++)world.TrialTickIndustry(vacuum,.1f);
            Check(vacuum.contents.Any(c=>c.id==id) && !sample.IsFreeSwimming && vacuum.operatorId<0 && vacuum.helperId<0,
                "the installed vacuum captures a real fish at its intake with no operators nearby");
            report.vacuumCatch=id;
            if(Directory.Exists(Path.Combine(output,"guest-probe")))
            {
                net.position=ground+Vector3.back*7f;
                vacuum.timer=30;
                report.vacuumSerial=vacuum.serial;
                report.vacuumOutlet=vacuum.position+TechRigShell.VacuumPoint(Vector3.up*.17f);
                peer.Teleport(vacuum.position+Vector3.right*2f);
                world.PublishFisheryTrial();
                var guest=peer.GetComponent<HarpoonTool>();
                yield return Wait("guest observes and takes one vacuum fish",()=>!vacuum.contents.Any(item=>item.id==id) &&
                    Enumerable.Range(2,guest.PocketCapacity).Any(slot=>guest.CatchIdInSlot(slot)==id),25);
                Check(!Enumerable.Range(2,host.GetComponent<HarpoonTool>().PocketCapacity)
                        .Any(slot=>host.GetComponent<HarpoonTool>().CatchIdInSlot(slot)==id),
                    "The independent guest received the identified specimen exactly once; the host did not receive a copy.");
            }
            yield break;
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool TrialUseFishery(TechRig rig,DiverMotor player,string action) => UseActiveRig(rig,player,action,player.AimDirection);
        internal void TrialTickFishery(TechRig rig,float elapsed) => TickActiveRig(rig,elapsed);
        internal void PublishFisheryTrial() => Publish();
        internal bool TrialFishOutline(long id) => dropViews.TryGetValue(id,out var model) &&
            model.GetComponentsInChildren<MeshRenderer>().Any(render=>render.name=="Proximity outline" && render.enabled);
        internal bool VisibleFisheryTrialRig(int serial) => rigShells.TryGetValue(serial,out var shell) && shell && shell.gameObject.activeInHierarchy;
        internal bool TrialUseIndustry(TechRig rig,DiverMotor player,string action) => UseIndustrialRig(rig,player,action,player.AimDirection);
        internal void TrialTickIndustry(TechRig rig,float elapsed) => TickIndustrialRig(rig,elapsed);
    }

    public sealed partial class ThreatDirector
    {
        internal void StageFisheryTrialAngler(Vector3 p)
        {
            var s=state.Value;
            s.PredatorPosition=p;s.PredatorHealth=tuning.predatorHealth;
            s.AnglerStage=HuntStage.Watching;anglerTarget=null;
            state.Value=s;
        }
    }

    public sealed partial class HarpoonTool
    {
        internal void FisheryTrialPickup(long id) => PickupServer(id);
    }
}
#endif
