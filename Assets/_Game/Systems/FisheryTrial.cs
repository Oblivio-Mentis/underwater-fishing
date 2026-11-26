#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Raid
{
    // Explicit disposable host scenario. Spawns/positions are staged, interactions and catches use the server simulation.
    internal sealed class FisheryTrial : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public string status="running", phase, error, screenshot;
            public bool twoDivers;
            public long chargeCatch, trapCatch, monsterCatch, netCatch, vacuumCatch;
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
                if(!world || !world.IsServerInitialized || world.Team==null || world.TravelRemaining>0 || !world.threats || !world.threats.Reef.Active)return false;
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
            if(peer)yield return Nets();
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
            var id=FishAt(ground+Vector3.right*.6f);
            float hp=host.Health;
            Check(!Use(charge,host,"alternate"),"a base charge rejects the remote action");
            Check(Use(charge,host) && charge.mode=="fuse","a real placed charge arms its fuse");
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
            Check(drop.id==id && drop.Fish==sample && !sample.IsFreeSwimming,"affected school fish becomes an identifiable world drop");
            host.Teleport(drop.position-host.AimDirection*1.05f-Vector3.up*.6f);
            Physics.SyncTransforms();
            var tool=host.GetComponent<HarpoonTool>();
            Check(world.TryAimDrop(host,out var aimed) && aimed.id==id && tool.RequestPickup(id),"dropped catch responds to the normal owner pickup");
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
            string image=Path.Combine(output,"captured-angler.png");
            ScreenCapture.CaptureScreenshot(image);
            yield return Wait("in-game cage screenshot",()=>File.Exists(image) && new FileInfo(image).Length>512,20);
            report.screenshot=Path.GetFileName(image);
            float before=cage.health;
            world.TrialTickFishery(cage,5);
            Check(cage.health<before && cage.contents.Count==1,"a live captive strains its cage");
            world.TrialTickFishery(cage,ActiveFisheryTuning.Get.cageEscapeSeconds*2);
            Check(cage.mode=="damaged" && cage.contents.Count==0 && world.threats.CageAnimalNear(ActiveFisheryTuning.Get.monsters[0],local,5),
                "a failed cage releases its captive back into the real predator director");
        }

        IEnumerator Nets()
        {
            report.phase="two network owners close a seine and operate a pump";Write();
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
            host.Teleport(ground+Vector3.back*.7f);peer.Teleport(ground+Vector3.right*1.6f);
            var aim=host.AimDirection;
            id=FishAt(host.transform.position+Vector3.up*.3f+aim*1.4f);
            Check(Use(vacuum,host),"the first diver takes the hose");
            world.TrialTickFishery(vacuum,.02f);
            Check(vacuum.contents.Count==0 && sample.IsFreeSwimming,"an unaided base pump waits for the second operator");
            Check(Use(vacuum,peer),"a second authenticated diver grips the pump");
            world.TrialTickFishery(vacuum,.02f);
            Check(vacuum.contents.Any(c=>c.id==id) && !sample.IsFreeSwimming,"cooperative suction captures a real fish at the intake");
            report.vacuumCatch=id;
            yield break;
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool TrialUseFishery(TechRig rig,DiverMotor player,string action) => UseActiveRig(rig,player,action,player.AimDirection);
        internal void TrialTickFishery(TechRig rig,float elapsed) => TickActiveRig(rig,elapsed);
        internal void PublishFisheryTrial() => Publish();
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
}
#endif
