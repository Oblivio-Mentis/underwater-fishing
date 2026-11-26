#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        [Serializable] sealed class IndustrialReport
        {
            public string status="running",error;
            public List<string> checks=new();
            public int installations,samples,fishActors,fullRigCount,fullSamples,publishedJsonBytes,hostPlayers;
            public double fullMeanTickMs,fullP95TickMs,meanPublishMs,p95PublishMs,meanTickAndPublishMs,p95TickAndPublishMs;
            public bool allocationCounterAvailable;
            public double meanTickMs,p95TickMs,worstTickMs,allocatedBytesPerTick;
            internal void Require(bool value,string description)
            {
                if(!value)throw new InvalidOperationException(description);
                checks.Add(description);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void StartIndustrialTrial()
        {
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"-raid-industrial-qa");
            if(at<0 || at+1>=args.Length)return;
            string folder=Path.GetFullPath(args[at+1]);
            Directory.CreateDirectory(folder);
            var report=new IndustrialReport();
            string reportPath=Path.Combine(folder,"industry-report.json");
            try
            {
                int saved=Array.IndexOf(args,"-raid-save");
                string fixture=Path.Combine(folder,"industry-campaign.json");
                if(!args.Contains("-raid-host") || saved<0 || saved+1>=args.Length ||
                   !string.Equals(Path.GetFullPath(args[saved+1]),fixture,StringComparison.OrdinalIgnoreCase) || File.Exists(fixture))
                    throw new InvalidOperationException("Industrial trial requires a new -raid-save <report folder>/industry-campaign.json and -raid-host.");
                await AwaitIndustry(()=>Instance && Instance.team!=null && Instance.localDiver && Instance.transitionRemaining<=0 &&
                    LootActor.VisibleStock.Any(x=>x && x.IsFreeOnSeabed) && FishActor.ServerSchool.Any(x=>x && x.CanHarvestRig(false)),"network, salvage and live school",65);
                await Instance.RunIndustrialTrial(report,folder);
                report.status="passed";
            }
            catch(Exception ex){report.status="failed";report.error=ex.ToString();}
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            UnityEngine.Debug.Log("[IndustrialTrial] "+report.status+" "+report.error+" "+reportPath);
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
        }

        static async Awaitable AwaitIndustry(Func<bool> condition,string description,float timeout=8)
        {
            float until=Time.realtimeSinceStartup+timeout;
            while(!condition())
            {
                if(Time.realtimeSinceStartup>=until)throw new TimeoutException(description);
                await Awaitable.NextFrameAsync();
            }
        }

        async Awaitable RunIndustrialTrial(IndustrialReport report,string folder)
        {
            var host=localDiver;
            report.Require(team.rigs.Count==0 && team.techPurchases.Count==0 && team.tank.Count==0,"Started a clean, isolated host campaign.");
            team.goshaMet=team.goshaTamed=true;
            if(threats)threats.enabled=false;
            void Upgrade(string kind,params string[] nodes)
            {
                foreach(var node in new[]{"unlock"}.Concat(nodes))
                    if(team.TechRank(kind,node)==0)team.techPurchases.Add(new TechPurchase{item=kind,upgrade=node,rank=1});
            }
            TechRig Install(string kind,Vector3 position)
            {
                var rig=team.AddRig(kind,position,-1);
                rig.mode="placed";
                return rig;
            }
            var school=FishActor.ServerSchool.Where(f=>f && f.depthTier==CurrentDepth && f.cargoSize==1 && f.CanHarvestRig(false)).ToList();
            var specimen=school.First();
            var site=specimen.transform.position;
            var forage=LootActor.VisibleStock.First(x=>x && x.tier==CurrentDepth && x.IsFreeOnSeabed);
            var dock=CargoLoadingPosition;
            Vector3 practice=default;
            bool open=false;
            foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                var point=dock+axis*9+Vector3.up*3;
                if(Physics.CheckSphere(point,.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                   !IndustryCanPull(point,point+Vector3.right*4,.6f))continue;
                host.Teleport(point);
                if(!host.IsInside){open=true;practice=point;break;}
            }
            report.Require(open,"Found an unobstructed underwater test bay within range of the ship.");
            Upgrade("lure");Upgrade("bubbles");
            var signal=Install("lure",site+Vector3.forward*2-Vector3.up*.8f);
            report.Require(UseIndustrialRig(signal,host,"use",Vector3.forward),"Switched on a real acoustic lure.");
            Vector3 before=specimen.transform.position;
            SoundAndBubbles(signal,.2f);
            report.Require(specimen.transform.position!=before && FishActor.ServerSchool.Count>=school.Count,"Existing fish steered toward the lure pulse.");
            signal.mode="placed";
            before=specimen.transform.position;
            var bubbles=Install("bubbles",before+Vector3.left-Vector3.up*.8f+Vector3.back*.35f);
            var partner=Install("bubbles",bubbles.position+Vector3.right*2);
            report.Require(UseIndustrialRig(bubbles,host,"alternate",Vector3.forward) && bubbles.targetSerial==partner.serial &&
                UseIndustrialRig(bubbles,host,"use",Vector3.forward),"Two field generators paired and started.");
            SoundAndBubbles(bubbles,.25f);
            report.Require(Vector3.Distance(specimen.transform.position,before)>.01f,"A live susceptible fish avoided the bubble curtain.");
            bubbles.mode="placed";

            Upgrade("farm","quality","auto","delivery");Upgrade("mail","auto","group");
            var farm=Install("farm",practice);
            var mail=Install("mail",practice+Vector3.right);
            var tool=host.GetComponent<HarpoonTool>();
            // The fixture stages two identified pocket catches from a spawned, eligible species.
            for(int i=0;i<2;i++)
            {
                long id=team.NewTechCatchId();
                tool.StagePocketForTrial(specimen,id);
                tool.SelectSlot(2);
                await AwaitIndustry(()=>tool.ActiveSlot==2,"Select stocked pocket");
                report.Require(UseIndustrialRig(farm,host,"use",Vector3.forward) && farm.brood[i].id==id &&
                    tool.CatchIdInSlot(2)==0,"Moved breeder "+i+" from the diver's inventory into the nursery.");
            }
            TickIndustrialRig(farm,IndustryPeriod(farm)+.1f);
            report.Require(farm.brood.Count==2 && farm.contents.Count==1,"Auto-care and auto-harvest retained both parents and produced one surplus catch.");
            long offspring=farm.contents[0].id;
            mail.timer=0;TickIndustrialRig(mail,.1f);
            report.Require(mail.flight=="transit" && mail.contents.Count==1 && farm.contents.Count==0,"Industrial mail took the same offspring ID and launched.");
            var saved=Path.Combine(folder,"capsule-in-flight.json");
            TeamSave.Save(saved,team);
            var loaded=TeamSave.Load(saved,progression);
            var restored=loaded.rigs.Single(x=>x.serial==mail.serial);
            report.Require(restored.flight=="transit" && restored.contents.Single().id==offspring,"Checksummed save restored the travelling capsule.");
            TickIndustrialRig(mail,IndustryPeriod(mail)+1);
            report.Require(team.tank.Count(x=>x.id==offspring)==1 && mail.contents.Count==0,"The ship credited the delivered offspring once.");
            report.Require(IndustryManifest.Arrive(loaded,restored,progression)==1 && IndustryManifest.Arrive(loaded,restored,progression)==0,"Reloaded campaign delivered once and rejected duplicate settlement.");

            Upgrade("magnet");
            var magnet=Install("magnet",forage.transform.position+Vector3.up*.3f);
            long salvaged=forage.CargoId;
            report.Require(UseIndustrialRig(magnet,host,"use",Vector3.forward),"Started the magnetic collector.");
            TickIndustrialRig(magnet,.1f);
            report.Require(!forage.IsVisible && magnet.contents.Exists(x=>x.id==salvaged && x.IsSalvage),"An actual salvage actor left the seabed and entered the magnetic hold.");
            Upgrade("oxygen","auto");
            var compressor=Install("oxygen",practice);
            host.Teleport(practice);host.DamageOxygen(15);
            float oxygen=host.Oxygen;compressor.air=12;
            TickIndustrialRig(compressor,1);
            float gained=host.Oxygen-oxygen;
            report.Require(gained>0 && Mathf.Abs(compressor.air+gained-12)<.001f,"The compressor transferred exactly the air removed from its reservoir.");

            Upgrade("winch","auto");Upgrade("robot","remote","quality");
            var cable=Install("winch",practice);
            var payload=Install("trap",practice+Vector3.right*3);
            host.Teleport(payload.position);
            HookIndustryCable(cable.serial);
            await AwaitIndustry(()=>cable.targetSerial==payload.serial,"Host attached the real winch cable through its RPC");
            before=payload.position;
            TickIndustrialRig(cable,.1f);
            report.Require((payload.position-cable.position).sqrMagnitude<(before-cable.position).sqrMagnitude,"The winch reeled in the physical installation.");
            ClearCable(cable);

            var robot=Install("robot",practice+Vector3.right*2);
            var station=ShipConsole.Stations.Single(x=>x && x.kind==ShipConsoleKind.SkillTree);
            var display=station.screenAnchor?station.screenAnchor:station.transform;
            host.Teleport(display.position-display.forward*1.71f+display.right*.64f-Vector3.up*.38f);
            host.LookAtConsole(display.position);
            await AwaitIndustry(()=>ShipConsole.CanUseKind(host,ShipConsoleKind.SkillTree),"Host looking at the real ship console");
            RequestRobotConsole(robot.serial);
            await AwaitIndustry(()=>robot.flight=="piloted" && robot.operatorId==host.OwnerId,"Bound authenticated pilot via the real robot console RPC");
            var weights=Install("basket",practice+Vector3.up);
            var crate=CatchItem.Salvage(team.NewTechCatchId(),2,1,2);
            var coil=CatchItem.Salvage(team.NewTechCatchId(),1,1,0);
            weights.contents.Add(crate);weights.contents.Add(coil);
            report.Require(TransferManifest(weights,robot) && weights.contents.Single().id==crate.id && robot.contents.Single().id==coil.id,
                "Robot capacity rejected a 75 kg crate but accepted a 30 kg coil on a 40 kg chassis.");
            weights.contents.Add(CatchItem.Salvage(team.NewTechCatchId(),1,1,0));
            report.Require(!TransferManifest(weights,robot),"A second 30 kg item exceeded the remaining robot payload.");
            before=robot.position;
            RobotControls(robot.serial,Vector3.forward,0);
            await AwaitIndustry(()=>(robot.position-before).sqrMagnitude>.0001f,"Server accepted movement from the pilot",3);
            Publish();
            await AwaitIndustry(()=>GetComponentsInChildren<Camera>(true).Any(cam=>cam.targetTexture && cam.targetTexture.name=="NEREID robot video"),"Robot video RenderTexture created by the client");
            report.Require(true,"Ship terminal acquired pilot authority, issued a movement RPC and started a dedicated video camera.");
            RobotControls(robot.serial,Vector3.zero,2);
            await AwaitIndustry(()=>robot.flight=="return" && robot.operatorId<0,"Robot accepted the purchased return command");

            RequestRobotConsole(robot.serial);
            await AwaitIndustry(()=>robot.flight=="piloted","Re-acquired the robot before an induced signal break");
            robot.position=CargoLoadingPosition+Vector3.up*(IndustryRange(robot)+1);
            TickIndustrialRig(robot,.02f);
            report.Require(robot.flight=="return" && robot.operatorId<0 && robot.contents.Single().id==coil.id,
                "Lost signal transferred control to the return program and preserved its cargo.");

            BenchmarkIndustry(report,practice);
            BenchmarkWholeFleet(report,practice);
            TeamSave.Save(savePath,team);
            var finalState=TeamSave.Load(savePath,progression);
            report.Require(finalState.rigs.Single(r=>r.serial==farm.serial).brood.Count==2 && finalState.deliveredIds.Count(id=>id==offspring)==1 &&
                Mathf.Approximately(finalState.rigs.Single(r=>r.serial==compressor.serial).air,compressor.air) &&
                finalState.rigs.Single(r=>r.serial==magnet.serial).contents.Single().id==salvaged,"Final isolated campaign recovered oxygen, nursery stock, salvage IDs and delivery ledger.");
        }

        void BenchmarkIndustry(IndustrialReport report,Vector3 origin)
        {
            int first=team.rigs.Count;
            string[] kinds={"lure","bubbles","farm","winch","magnet","oxygen","robot","mail"};
            const int entities=128,iterations=120;
            var measurements=new double[iterations];
            try
            {
                for(int i=0;i<entities;i++)
                {
                    string kind=kinds[i%kinds.Length];
                    var r=team.AddRig(kind,origin+new Vector3(i%8,0,i/8),-1);
                    r.mode=kind is "lure" or "bubbles" or "magnet"?"running":"placed";
                    r.care=100;r.progress=(i%11)*.01f;
                    if(kind=="robot")r.flight="collect";
                }
                void Sample()
                {
                    for(int i=first;i<team.rigs.Count;i++)TickIndustrialRig(team.rigs[i],.02f);
                }
                Sample();
                long counter=GC.GetAllocatedBytesForCurrentThread();
                var allocationProbe=new byte[4096];
                report.allocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()-counter>=allocationProbe.Length;
                GC.KeepAlive(allocationProbe);
                long allocated=GC.GetAllocatedBytesForCurrentThread();
                var watch=new Stopwatch();
                for(int i=0;i<iterations;i++)
                {
                    watch.Restart();Sample();watch.Stop();
                    measurements[i]=watch.Elapsed.TotalMilliseconds;
                }
                report.allocatedBytesPerTick=report.allocationCounterAvailable?(GC.GetAllocatedBytesForCurrentThread()-allocated)/(double)iterations:-1;
                Array.Sort(measurements);
                report.installations=entities;report.samples=iterations;report.fishActors=FishActor.ServerSchool.Count;
                report.meanTickMs=measurements.Average();report.p95TickMs=measurements[(iterations*95/100)-1];report.worstTickMs=measurements[iterations-1];
            }
            finally {team.rigs.RemoveRange(first,team.rigs.Count-first);}
        }

        void BenchmarkWholeFleet(IndustrialReport report,Vector3 origin)
        {
            const int iterations=60;
            var previous=team;
            var ticks=new double[iterations];
            var publication=new double[iterations];
            var combined=new double[iterations];
            var watch=new Stopwatch();
            try
            {
                // Snapshot only the maximum licensable fleet. A previous expedition's arbitrary
                // historical delivery ledger is deliberately excluded from this scale measurement.
                team=new TeamProgress(progression);
                foreach(var def in TechCatalog.Items)
                    foreach(var node in def.upgrades)
                        team.techPurchases.Add(new TechPurchase{item=def.id,upgrade=node.id,rank=node.values.Length});
                void Add(string kind,int i)
                {
                    var rig=team.AddRig(kind,origin+new Vector3(i%8,0,i/8),-1);
                    rig.mode=kind is "lure" or "bubbles" or "magnet"?"running":"placed";
                    if(kind=="robot")rig.flight="collect";
                    if(kind=="trap")rig.mode="working";
                    if(kind is "farm" or "trap" or "net" or "vacuum" or "basket" or "mail")
                    {
                        for(int j=0;j<3;j++)
                            rig.contents.Add(new CatchItem(team.NewTechCatchId(),2,0,1,1,1,species:"Needlefish"));
                    }
                    if(kind=="farm")
                        for(int j=0;j<2;j++)rig.brood.Add(new CatchItem(team.NewTechCatchId(),2,0,1,1,1,species:"Needlefish"));
                }
                foreach(var def in TechCatalog.Items)
                    for(int i=0;i<TechRig.TotalLimit(team,def);i++)Add(def.id,team.rigs.Count);
                for(int i=0;i<tuning.maxSampleBaskets;i++)Add("basket",team.rigs.Count);
                report.fullRigCount=team.rigs.Count;report.fullSamples=iterations;report.hostPlayers=DiverMotor.ServerDivers.Count;
                TickTech(.02f);Publish();
                for(int i=0;i<iterations;i++)
                {
                    watch.Restart();TickTech(.02f);watch.Stop();ticks[i]=watch.Elapsed.TotalMilliseconds;
                    watch.Restart();Publish();watch.Stop();publication[i]=watch.Elapsed.TotalMilliseconds;
                    combined[i]=ticks[i]+publication[i];
                }
                report.publishedJsonBytes=System.Text.Encoding.UTF8.GetByteCount(snapshot.Value);
                Array.Sort(ticks);Array.Sort(publication);Array.Sort(combined);
                int p95=iterations*95/100-1;
                report.fullMeanTickMs=ticks.Average();report.fullP95TickMs=ticks[p95];
                report.meanPublishMs=publication.Average();report.p95PublishMs=publication[p95];
                report.meanTickAndPublishMs=combined.Average();report.p95TickAndPublishMs=combined[p95];
            }
            finally {team=previous;Publish();}
        }
    }
}
#endif
