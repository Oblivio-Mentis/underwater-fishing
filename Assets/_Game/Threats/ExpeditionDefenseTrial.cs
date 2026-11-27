#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Raid
{
    // A fixture runs only with an isolated campaign path and a development player.
    static partial class ExpeditionDefenseTrial
    {
        [Serializable] sealed class Report
        {
            public string status="running", phase="", error="";
            public int peers, species, phases, hits, losses, recovered;
            public long cagedPredator, deliveredPredator;
            public float minimumIntegrity=100, assaultMeanFrameMs, assaultP95FrameMs;
            public List<string> checks=new();
            public void Check(bool valid,string description)
            {
                if(!valid)throw new InvalidOperationException(description);
                checks.Add(description);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"-raid-defense-trial"), observe=Array.IndexOf(args,"-raid-defense-observe");
            if(observe>=0 && observe+1<args.Length)
            {
                await Observe(Path.GetFullPath(args[observe+1]));
                return;
            }
            if(at<0 || at+2>=args.Length)return;
            var folder=Path.GetFullPath(args[at+1]);
            Directory.CreateDirectory(folder);
            var report=new Report();
            var path=Path.Combine(folder,"defense.json");
            try
            {
                string campaign=Path.Combine(folder,"campaign.json");
                int save=Array.IndexOf(args,"-raid-save");
                if(save<0 || save+1>=args.Length || Path.GetFullPath(args[save+1])!=campaign || File.Exists(campaign) ||
                    !int.TryParse(args[at+2],out int peers))throw new InvalidOperationException("A new isolated campaign and the crew size are required.");
                await Wait(()=>RaidWorld.Instance && RaidWorld.Instance.NetworkObject && RaidWorld.Instance.IsServerInitialized && RaidWorld.Instance.Team!=null &&
                    RaidWorld.Instance.TravelRemaining<=0 && DiverMotor.ServerDivers.Count==peers && UI.UiRoot.Instance &&
                    UI.UiRoot.Instance.Context.Player!=null,"LAN crew",90);
                if(Array.IndexOf(args,"-raid-defense-film")>=0)await Film(report,folder,args);
                else if(Array.IndexOf(args,"-raid-defense-solo")>=0)await PlaySolo(report,folder);
                else if(Array.IndexOf(args,"-raid-defense-combat")>=0)await PlayCombat(report,folder,peers);
                else await Play(report,folder,peers);
                report.status="passed";
            }
            catch(Exception ex){report.status="failed";report.error=ex.ToString();}
            Write(path,report);
            Debug.Log("[DefenseQA] "+report.status+" "+report.phase+" "+report.error);
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
        }

        static async Awaitable Wait(Func<bool> ready,string step,float timeout=20)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=deadline)throw new TimeoutException(step);
                await Awaitable.NextFrameAsync();
            }
        }

        static void Write(string path,Report report) => File.WriteAllText(path,JsonUtility.ToJson(report,true));

        static async Awaitable Observe(string path)
        {
            var report=new Report();
            WetCargoShoal shoal=null;
            float expires=Time.realtimeSinceStartup+260;
            while(Time.realtimeSinceStartup<expires)
            {
                var world=RaidWorld.Instance;
                if(world && world.NetworkObject && world.IsClientInitialized && world.threats)
                {
                    var d=world.threats;var s=d.Defense;
                    report.phases|=1<<(int)s.phase;
                    report.hits=Mathf.Max(report.hits,s.impact);
                    report.losses=Mathf.Max(report.losses,world.StockLossCount);
                    report.recovered=Mathf.Max(report.recovered,s.returned);
                    report.minimumIntegrity=Mathf.Min(report.minimumIntegrity,s.integrity);
                    for(int i=0;i<d.DefenseActorCount;i++)report.species|=1<<(int)d.DefenseActorAt(i).kind;
                    if(!shoal && world.container)shoal=world.container.GetComponentInChildren<WetCargoShoal>();
                    var view=world.View;
                    if(shoal && view!=null)
                    {
                        foreach(var item in view.cargo)
                            if(DefenseCreatureAssets.ReadKey(item.species,out _) && shoal.ModelFor(item.id))
                                report.cagedPredator=item.id;
                        foreach(var item in view.tank)
                            if(item.id==report.cagedPredator && shoal.ModelFor(item.id))report.deliveredPredator=item.id;
                    }
                    Write(path,report);
                }
                await Awaitable.WaitForSecondsAsync(.24f);
            }
        }

        static void WatchField(DiverMotor diver, ThreatDirector d, Vector3 look, float range=8.5f)
        {
            var center=d.DefenseCenter;
            for(int i=0;i<24;i++)
            {
                float angle=(i*15+45)*Mathf.Deg2Rad;
                var at=center+new Vector3(range*Mathf.Sin(angle),-2.15f,range*Mathf.Cos(angle));
                if(!MarineSpawn.InOpenWater(RaidWorld.Instance.CurrentDepth,at,.55f))continue;
                var eye=at+Vector3.up*.6f;
                if(!MarineSpawn.ClearLeg(eye,look,.12f,Mathf.Max(0,Vector3.Distance(eye,look)-.85f)))continue;
                diver.Teleport(at);
                diver.LookAtConsole(look);
                return;
            }
            diver.LookAtConsole(look);
        }

        static async Awaitable Shot(string output,DiverMotor host,ThreatDirector director,Vector3 point,string name)
        {
            WatchField(host,director,point);
            await Awaitable.WaitForSecondsAsync(.22f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(output,name+".png"));
        }

        static async Awaitable ShotPredator(string output,DiverMotor host,ThreatDirector director,int id,string name)
        {
            await Awaitable.NextFrameAsync();
            if(!director.TryDefenseSurface(id,out var model))return;
            var point=model.position;
            var heading=Vector3.ProjectOnPlane(model.forward,Vector3.up).normalized;
            bool placed=false;
            for(int i=0;i<18;i++)
            {
                float angle=(90-(i/2)*20)*(i%2==0?1:-1);
                var side=Quaternion.AngleAxis(angle,Vector3.up)*heading;
                var at=point+side*4.5f+Vector3.up*(i<12?.20f:-.50f);
                var eye=at+Vector3.up*.6f;
                if(!MarineSpawn.InOpenWater(RaidWorld.Instance.CurrentDepth,at,.63f) ||
                    !MarineSpawn.ClearLeg(eye,point,.12f,Vector3.Distance(eye,point)-.7f))continue;
                host.Teleport(at);
                host.LookAtConsole(point);
                placed=true;
                break;
            }
            if(!placed)WatchField(host,director,point);

            float end=Time.realtimeSinceStartup+.17f;
            while(model && Time.realtimeSinceStartup<end)
            {
                host.LookAtConsole(model.position);
                await Awaitable.NextFrameAsync();
            }
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(output,name+".png"));
        }

        static async Awaitable Broadside(string folder,DiverMotor player,ThreatDirector director)
        {
            var vessel=Nereid.NereidVessel.Active;
            var center=director.DefenseCenter;
            foreach(int side in new[]{1,-1})
            {
                var at=center+vessel.transform.right*(12*side)+Vector3.down*.36f-vessel.transform.forward*2.1f;
                if(!MarineSpawn.InOpenWater(RaidWorld.Instance.CurrentDepth,at,.56f))continue;
                player.Teleport(at);
                player.LookAtConsole(center-Vector3.up*.35f);
                await Awaitable.WaitForSecondsAsync(.22f);
                UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,side==1?"00-starboard.png":"00-port.png"));
            }
        }

        static async Awaitable SwarmFrame(string folder,DiverMotor player,ThreatDirector director,string file="02-swarm-through-glass")
        {
            var vessel=Nereid.NereidVessel.Active;
            var center=director.DefenseCenter;
            float side=Vector3.Dot(director.Defense.approachEnd-center,vessel.transform.right)<0?-1:1;
            var observation=vessel.ObservationRoom();
            if(!observation)throw new InvalidOperationException("The observation hall is absent.");
            var at=observation.TransformPoint(new Vector3(side<0?-4.68f:3.24f,side<0?1.55f:.22f,.55f));
            if(director.TryDefenseWindow(director.Defense.approachEnd,director.Defense.approachStart,out var window))
                at=window-Vector3.up*.6f;
            player.Teleport(at);
            float ready=Time.realtimeSinceStartup+.68f;
            do
            {
                await Awaitable.NextFrameAsync();
                player.LookAtConsole(director.DefenseApproachFocus);
            } while(Time.realtimeSinceStartup<ready);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,file+".png"));
            var eye=UnderwaterLook.Instance.DiverCamera;
            Debug.Log($"[DefenseSight] Camera {eye.transform.position:F2} focus {director.DefenseApproachFocus:F2} " +
                $"viewport {eye.WorldToViewportPoint(director.DefenseApproachFocus):F2} silhouettes {director.VisibleDefenseSwarm} " +
                $"start {director.Defense.approachStart:F2} arrival {director.Defense.approachEnd:F2}.");
        }

        static async Awaitable DomeFrame(string folder,DiverMotor player,ThreatDirector director)
        {
            var ship=Nereid.NereidVessel.Active;
            Vector3 center=director.DefenseCenter+Vector3.up*7;
            int side=Vector3.Dot(director.Defense.approachEnd-center,ship.transform.right)>0?1:-1;
            for(int i=0;i<12;i++)
            {
                float angle=side*(52+16*(i/2)*(i%2==0?1:-1));
                var heading=Quaternion.AngleAxis(angle,Vector3.up)*ship.transform.forward;
                var at=center+heading*(54-i*.8f)+Vector3.up*(3.5f+i*.45f);
                if(!MarineSpawn.InOpenWater(RaidWorld.Instance.CurrentDepth,at,.80f) ||
                    !MarineSpawn.ClearLeg(at,center,.15f,Vector3.Distance(at,center)*.68f))continue;
                player.Teleport(at);
                float end=Time.realtimeSinceStartup+.30f;
                while(Time.realtimeSinceStartup<end)
                {
                    player.LookAtConsole(center);
                    await Awaitable.NextFrameAsync();
                }
                UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"02-swarm-dome.png"));
                return;
            }
        }

        static async Awaitable RecordFrameTime(Report report)
        {
            var samples=new List<float>(150);
            float deadline=Time.realtimeSinceStartup+1.35f;
            float sum=0;
            while(Time.realtimeSinceStartup<deadline)
            {
                await Awaitable.NextFrameAsync();
                float ms=Time.unscaledDeltaTime*1000;
                samples.Add(ms);sum+=ms;
            }
            if(samples.Count==0)return;
            samples.Sort();
            report.assaultMeanFrameMs=sum/samples.Count;
            report.assaultP95FrameMs=samples[Mathf.Clamp(Mathf.CeilToInt(samples.Count*.95f)-1,0,samples.Count-1)];
        }

        static async Awaitable Play(Report report,string folder,int peers)
        {
            var world=RaidWorld.Instance;var team=world.Team;var d=world.threats;
            var host=UI.UiRoot.Instance.Context.Player;
            report.peers=peers;
            report.Check(d && d.defenseTuning && d.Defense.Ratio==1,"Field starts at full integrity in the live level.");
            report.Check(!Resources.Load<AudioClip>("DefenseAtmosphere/Audio/HullRoar") &&
                !Resources.Load<AudioClip>("DefenseAtmosphere/Audio/RingPressure") &&
                !Resources.Load<AudioClip>("KrakenFireBelch") && !Resources.Load<AudioClip>("KrakenRageRoar"),
                "Authored raid and retaliatory Kraken clips are absent from the player.");
            var prefab=Nereid.NereidVessel.Active.GetComponentInChildren<Nereid.DefenseFieldView>();
            report.Check(prefab && prefab.GetComponentsInChildren<Collider>().Length==0,"The ship has an authored, passable field.");
            FishActor specimen=null;
            foreach(var fish in FishActor.ServerSchool)if(fish && !fish.RequiresPartner){specimen=fish;break;}
            if(!specimen)throw new InvalidOperationException("No active fish specimen for the isolated catch manifest");
            for(int i=1;i<=20;i++)team.Deposit(specimen.ToCargo(800000+i),world.progression);
            ReportPhase("full meal",report,folder);
            int initial=team.RequiredFood(world.progression);
            team.offeringPoints=initial;
            team.hungerRemaining=.05f;
            await Wait(()=>team.feedingCycle==1,"first completed meal");
            report.Check(!d.DefenseActive && team.RequiredFood(world.progression)>initial,"A complete meal prevents a raid and advances appetite only once.");
            await Shot(folder,host,d,d.DefenseCenter,"01-field");
            await Awaitable.WaitForSecondsAsync(.75f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"01-field-pattern-motion.png"));
            await Broadside(folder,host,d);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"00-feeding-window.png"));
            ReportPhase("underfed cycle",report,folder);
            team.offeringPoints=team.RequiredFood(world.progression)/3;
            team.hungerRemaining=.05f;
            await Wait(()=>d.Defense.phase==DefensePhase.Warning,"underfeeding warning");
            report.Check(team.feedingCycle==2 && d.Defense.budget>0,"Partial feeding queues one raid for the finished meal.");
            report.Check(d.DefenseCountdown>44 && d.DefenseCountdown<=45.1f && !d.DefenseAlarm,
                "The raid begins with a 45-second countdown and the ship awaits the incoming school.");
            await Shot(folder,host,d,d.Defense.approachStart,"01-countdown");
            await Wait(()=>d.DefenseBlackout>.995f,"cabin night during the countdown",3);
            var lighting=prefab.GetComponentInChildren<Nereid.NereidDefenseLighting>();
            await SwarmFrame(folder,host,d,"01-preparation-cabin");
            float warningGain=lighting.WorkLightGain;
            float warningHaze=Shader.GetGlobalFloat("_RaidDryHaze");
            report.Check(d.Defense.phase==DefensePhase.Warning && warningGain<.085f && warningHaze>.32f,
                $"The dry cabin is already dark during the countdown (work lights {warningGain:F3}, haze {warningHaze:F2}).");
            var center=d.DefenseCenter;
            float departureRange=Vector3.ProjectOnPlane(d.Defense.approachStart-center,Vector3.up).magnitude;
            float wingRange=Nereid.DefenseAtmosphereView.Stock.handoffRadius;
            report.Check(departureRange is >=120 and <=151 && wingRange is >=28 and <=36,
                $"The distant formation begins at {departureRange:F0} m and hands off its wing near {wingRange:F0} m.");
            float interval=team.hungerRemaining;
            await Wait(()=>d.Defense.phase==DefensePhase.Encirclement,"school approach",d.defenseTuning.warningSeconds+4);
            await Awaitable.WaitForSecondsAsync(2f);
            int aimed=0, scanning=0;
            if(lighting)foreach(var lamp in lighting.searchlights)
            {
                if(lamp.enabled)scanning++;
                if(lamp.enabled && Vector3.Angle(lamp.transform.forward,lighting.Target-lamp.transform.position)<15)aimed++;
            }
            report.Check(lighting && lighting.AlarmActive && aimed>=1 && scanning==4 &&
                lighting.WorkLightGain<.29f && Vector3.Distance(lighting.Target,d.DefenseApproachFocus)<1 && d.DefenseCountdown>0,
                $"Cabin work lamps dimmed (gain {lighting?.WorkLightGain:F2}) while four searchlights cover different bearings (aimed {aimed}).");
            await SwarmFrame(folder,host,d);
            float glassHaze=Shader.GetGlobalFloat("_RaidDryHaze");
            var look=UnderwaterLook.Instance;
            bool dryCamera=look && look.DiverCamera && !look.EyeSubmerged;
            report.Check(dryCamera && glassHaze>.32f && d.VisibleDefenseSwarm>0,
                $"A dry observation deck camera sees exterior haze and the swarm. Dry={dryCamera}, haze={glassHaze:F2}, silhouettes={d.VisibleDefenseSwarm}.");
            report.Check(d.DefenseCloudCount>=400,$"The outer formation loads {d.DefenseCloudCount} shared-mesh figures around the vessel.");
            await DomeFrame(folder,host,d);
            Vector3 focus=d.DefenseApproachFocus;
            Vector3 towardsShip=(d.DefenseCenter-focus).normalized;
            Vector3 flank=Vector3.Cross(towardsShip,Vector3.up).normalized;
            for(int i=0;i<8;i++)
            {
                var exterior=focus+flank*(i%2==0?1:-1)*(4+i/2*2)+towardsShip*7+Vector3.down*.65f;
                if(!MarineSpawn.InOpenWater(world.CurrentDepth,exterior,.55f) ||
                    !MarineSpawn.ClearLeg(exterior,focus,.20f,Vector3.Distance(exterior,focus)))continue;
                host.Teleport(exterior);
                break;
            }
            await Awaitable.NextFrameAsync();
            host.LookAtConsole(d.DefenseApproachFocus);
            await Awaitable.WaitForSecondsAsync(.32f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"02-encirclement.png"));
            await Wait(()=>d.Defense.phase==DefensePhase.Assault && d.DefenseActorCount>0,"a naturally promoted attacker",30);
            var firstAttacker=d.DefenseActorAt(0);
            report.Check(firstAttacker.slot>=0 && (d.Defense.promoted&(1UL<<firstAttacker.slot))!=0,
                "The first active attacker continues from an existing swarm swimmer.");
            float liveRange=Vector3.ProjectOnPlane(firstAttacker.position-center,Vector3.up).magnitude;
            report.Check(liveRange is >=26 and <=39 && firstAttacker.velocity.sqrMagnitude>.25f,
                $"The live wing is already moving at handoff, {liveRange:F1} m from the submarine.");
            await SwarmFrame(folder,host,d,"03-assault-cabin");
            report.Check(Mathf.Abs(lighting.WorkLightGain-warningGain)<.02f &&
                Mathf.Abs(Shader.GetGlobalFloat("_RaidDryHaze")-warningHaze)<.025f,
                "Cabin work lighting and sea haze stay at the same levels when the assault begins.");
            report.Check(team.hungerRemaining==interval,"The next meal timer stays parked during the event.");
            ReportPhase("natural shield strike",report,folder);
            await Wait(()=>d.Defense.impact>0 && d.Defense.Ratio<1,"authentic monster hit",36);
            await Shot(folder,host,d,d.Defense.impactPoint,"03-impact");
            report.Check(d.Defense.impact>0 && d.Defense.Ratio<1,"An ordinary assault reduces integrity at an actual contact point.");
            report.Check(team.tank.Count==20,"A living field keeps the original catches.");
            report.hits=d.Defense.impact;
            report.minimumIntegrity=d.Defense.integrity;

            ReportPhase("five species",report,folder);
            foreach(DefenseSpecies kind in Enum.GetValues(typeof(DefenseSpecies)))
            {
                int group=(int)kind+7;
                if(!d.DefenseFixtureSpawn(kind,true,group,8f))throw new InvalidOperationException("Could not place "+kind);
                await Awaitable.NextFrameAsync();
                for(int i=0;i<d.DefenseActorCount;i++)
                {
                    var a=d.DefenseActorAt(i);
                    if(a.kind!=kind || a.group!=group)continue;
                    report.species|=1<<(int)kind;
                    await ShotPredator(folder,host,d,a.id,"04-"+kind);
                    break;
                }
            }
            report.Check(report.species==31,"All five interactive creature profiles appeared in the main level.");
            for(int i=0;i<d.DefenseActorCount;i++)
            {
                var a=d.DefenseActorAt(i);
                if(!Nereid.NereidVessel.Active.OutsideDefenseHull(a.position,a.forward,a.scale))
                    throw new InvalidOperationException("An attacker entered a sealed hull compartment: "+a.kind);
            }
            report.Check(true,"All sampled attackers swim outside the sealed hull.");
            await RecordFrameTime(report);
            d.DefenseFixtureEnd();
            await Wait(()=>!d.DefenseActive,"successful recovery",22);
            report.Check(team.tank.Count==20 && team.ongoingDefense==null && d.Defense.Ratio==1,"Successful defense keeps the manifest and restores the shield.");
            report.Check(d.DefenseBlackout>.9f,"The scene stays dark after the shield and gameplay have recovered.");
            report.Check(team.hungerRemaining<interval || Mathf.Abs(team.hungerRemaining-interval)<.05f,"The clock restarts with a single next cycle.");

            ReportPhase("breach and real theft",report,folder);
            host.Teleport(Nereid.NereidDeckNavigation.CrewSpawn(host.OwnerId,new Vector3(0,1.2f,1)));
            if(!d.DefenseFixtureBegin(1,team.RequiredFood(world.progression)*2))throw new InvalidOperationException("Failure fixture did not start");
            d.DefenseFixtureForceAssault();
            d.DefenseFixturePauseGroups();
            if(!d.DefenseFixtureThief(1,1,8.5f) || !d.DefenseFixtureThief(4,-1,14f))
                throw new InvalidOperationException("Could not stage fleeing carriers at the two lateral approaches");
            if(!d.DefenseFixtureSpawn(DefenseSpecies.Remora,false,9,9))
                throw new InvalidOperationException("Could not stage a remora before failure");
            float nextRemoraSample=0;
            await Wait(()=>
            {
                for(int i=0;i<d.DefenseActorCount;i++)
                {
                    var actor=d.DefenseActorAt(i);
                    if(actor.kind!=DefenseSpecies.Remora)continue;
                    if(Time.realtimeSinceStartup>=nextRemoraSample)
                    {
                        nextRemoraSample=Time.realtimeSinceStartup+1.2f;
                        Debug.Log("[BreachRemora] "+d.DefenseFixtureMotion(actor.id));
                    }
                    return actor.pose==AttackerPose.Latch;
                }
                return false;
            },"a remora attaches before its supporting field breaks",14);
            d.DefenseFixtureSetIntegrity(0);
            report.Check(d.Defense.breached && team.ongoingDefense.lossLimit>0 && team.tank.Count==20,"A breach opens theft without instantly removing stock.");
            WatchField(host,d,d.Defense.impactPoint,10);
            await Awaitable.WaitForSecondsAsync(.16f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"05-shield-collapse.png"));
            report.Check(world.DefenseFixtureFallingRemora(),"Attached remora dies and descends as a recoverable body.");
            bool sawSnatch=false;
            int carrier=0;
            bool CarryVisible()
            {
                for(int i=0;i<d.DefenseActorCount;i++)
                {
                    var actor=d.DefenseActorAt(i);
                    if(actor.pose==AttackerPose.Snatch)sawSnatch=true;
                    if(actor.stolenId<=0)continue;
                    carrier=actor.id;
                    return true;
                }
                return false;
            }
            await Wait(CarryVisible,"a thief collects the original fish and the snapshot shows it",13);
            report.Check(sawSnatch,"The jaw windup and lunge precede the replicated pickup.");
            var watching=host;
            foreach(var other in DiverMotor.ServerDivers)
                if(other!=host){watching=other;break;}
            d.TryDefenseAnchor(carrier,out var departure);
            Vector3 TrackCarrier()
            {
                if(!d.DefenseFixtureCarrier(carrier,out var at))
                    throw new InvalidOperationException("A watched carrier disappeared.");
                watching.Teleport(at+Vector3.left*.85f);
                watching.LookAtConsole(at);
                return at;
            }
            await Wait(()=>
            {
                TrackCarrier();
                return d.DefenseFixtureGrip(carrier,out float gap) && gap<.18f;
            },
                "the rendered prey reaches the moving mouth socket",1.6f);
            report.Check(true,"The stolen fish follows the moving mouth socket.");
            if(watching!=host)await ShotPredator(folder,host,d,carrier,"05-carrier");
            else
            {
                TrackCarrier();
                UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"05-carrier.png"));
            }
            await Wait(()=>Vector3.Distance(TrackCarrier(),departure)>5.5f,
                "the watched thief has sprinted away from the mouth of the cargo bay",5);
            report.Check(true,"The carrier accelerates away and remains while a diver watches it.");
            d.BlastDefense(TrackCarrier(),.20f,180);
            await Wait(()=>d.Defense.returned>0,"a defeated carrier returns its held fish",10);
            report.recovered=d.Defense.returned;
            await Wait(()=>world.StockLossCount>0,"a live carrier crosses the escape boundary",17);
            report.losses=world.StockLossCount;
            await Wait(()=>!d.DefenseActive,"failed defense recovery",24);
            report.Check(team.tank.Count==20-report.losses && team.cumulativeRaidLoss==report.losses,
                "The manifest loses only escaped catches and preserves recovered ones.");
            report.Check(team.ongoingDefense==null && d.Defense.Ratio==1,"Field and ordinary loop recover after theft.");
            await Shot(folder,host,d,d.DefenseCenter,"06-restored");
            report.phase="done";
        }

        static async Awaitable PlayCombat(Report report,string folder,int peers)
        {
            var world=RaidWorld.Instance;var d=world.threats;
            var host=UI.UiRoot.Instance.Context.Player;
            var tool=host.GetComponent<HarpoonTool>();
            report.peers=peers;
            if(!d.DefenseFixtureBegin(.7f,6000))throw new InvalidOperationException("Combat fixture could not start.");
            d.DefenseFixtureForceAssault();d.DefenseFixturePauseGroups();
            ReportPhase("latch and harpoon",report,folder);
            if(!d.DefenseFixtureSpawn(DefenseSpecies.Remora,false,1,8))throw new InvalidOperationException("No remora entry");
            float nextMotionLog=0;
            await Wait(()=>
            {
                if(d.DefenseActorCount!=1)return false;
                var state=d.DefenseActorAt(0);
                if(Time.realtimeSinceStartup>=nextMotionLog)
                {
                    nextMotionLog=Time.realtimeSinceStartup+1.2f;
                    Debug.Log("[RemoraContact] "+d.DefenseFixtureMotion(state.id));
                }
                return state.pose==AttackerPose.Latch;
            },"remora attached",14);
            var remora=d.DefenseActorAt(0);
            await Wait(()=>d.DefenseFixtureContact(remora.id,out _,out _),"remora jaw centered on a lit shield cell",3);
            d.DefenseFixtureContact(remora.id,out _,out var firstAnchor);
            var outward=(remora.position-firstAnchor).normalized;
            host.Teleport(remora.position+outward*2.8f-Vector3.up*.48f);
            host.LookAtConsole(remora.position);
            await Awaitable.WaitForSecondsAsync(.16f);
            tool.DefenseFixtureFire();
            await Wait(()=>tool.HasDefenseLine && tool.AttachedContact.defenseId==remora.id,"server validated the shot into the remora",3);
            await Wait(()=>GameObject.Find("Line tension") && tool.TryHookedDefense(out var fight) &&
                fight.hookCount==1 && fight.fightRemaining>.8f,"reused line gauge while the predator fights",2);
            report.Check(true,"Predator line reuses the real tension gauge and shows the replicated struggle.");
            report.Check(host.GetComponentInChildren<AudioSource>()==null &&
                Resources.LoadAll<AudioClip>("FishingAudio").Length==0,"The real harpoon shot loads no fishing audio and creates no emitter.");
            report.Check(d.DefenseActorCount==1,"A harpoon strike does not remove an attached remora on impact.");
            HarpoonShotFx flight=null;
            await Wait(()=>
            {
                foreach(var fx in UnityEngine.Object.FindObjectsByType<HarpoonShotFx>())
                    if(fx.VerifiedOwner==host.OwnerId && fx.VerifiedFrames>1 && fx.HasConfirmedAttachment){flight=fx;return true;}
                return false;
            },"the rendered barb and cable follow the predator",3);
            report.Check(flight.MaxTipError<.035f && flight.MaxKnotError<.035f,
                "The moving harpoon bolt stays in the creature with its cable attached.");
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-predator-harpoon.png"));
            var pullSide=Vector3.Cross(Vector3.up,outward).normalized;
            for(int sign=-1;sign<=1;sign+=2)
            {
                var position=host.transform.position+pullSide*(1.65f*sign);
                if(!MarineSpawn.InOpenWater(world.CurrentDepth,position+Vector3.up*.56f,.55f))continue;
                host.Teleport(position);
                host.LookAtConsole(remora.position);
                break;
            }
            await Awaitable.NextFrameAsync();
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-remora-at-rest.png"));
            float stretchDeadline=Time.realtimeSinceStartup+2;
            float extension=0;
            while(!d.DefenseFixtureStretch(remora.id,tool.FishingAnchor,out extension) &&
                Time.realtimeSinceStartup<stretchDeadline && tool.HasDefenseLine)
            {
                tool.DefenseFixtureWind(true);
                await Awaitable.NextFrameAsync();
            }
            report.Check(d.DefenseFixtureStretch(remora.id,tool.FishingAnchor,out extension),
                $"The remora stretches {extension:F2} metres toward the pulling line while its jaw stays anchored.");
            report.Check(d.DefenseFixtureContact(remora.id,out float jawGap,out var pullAnchor) &&
                (pullAnchor-firstAnchor).sqrMagnitude<.0001f,
                $"The glowing contact stays pinned while reeling. Wet jaw clearance {jawGap:F3} m.");
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-remora-stretched.png"));
            float deadline=Time.realtimeSinceStartup+15;
            float peakTension=0;
            bool winding=true;
            while(tool.HasDefenseLine && Time.realtimeSinceStartup<deadline)
            {
                peakTension=Mathf.Max(peakTension,tool.LineTension);
                if(tool.LineTension>.73f)winding=false;
                if(tool.LineTension<.42f)winding=true;
                tool.DefenseFixtureWind(winding);
                await Awaitable.NextFrameAsync();
            }
            tool.DefenseFixtureWind(false);
            report.Check(!tool.HasDefenseLine && d.DefenseInkAt(remora.position)>.1f && tool.CatchCount==1,
                $"Reeling tears the attached remora, releases the line and creates one catch and local ink. Line={tool.HasDefenseLine}, catch={tool.CatchCount}, ink={d.DefenseInkAt(remora.position)}, peak={peakTension:0.00}, actors={d.DefenseActorCount}");
            report.Check(tool.ActiveSlot==0 && tool.HarpoonInHands,"Landing the remora fills a pocket without changing the owner's equipped slot.");
            long remoraCatch=tool.CatchIdInSlot(2);
            await Awaitable.WaitForSecondsAsync(.36f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-remora-ink.png"));
            await LandHunter(folder,report,d,host,tool);

            ReportPhase("captive rescue",report,folder);
            DiverMotor guest=null;
            foreach(var diver in DiverMotor.ServerDivers)if(diver && diver!=host){guest=diver;break;}
            if(!guest)throw new InvalidOperationException("An independent diver is required");
            tool.DefenseFixtureSelect(0);
            await Wait(()=>tool.ActiveSlot==0,"equipped harpoon");
            if(!d.DefenseFixtureOpenWaterDuel(DefenseSpecies.Abductor,guest,3,out int squidId))
                throw new InvalidOperationException("No open-water abductor lane");
            var actor=d.DefenseActorAt(d.DefenseActorCount-1);
            report.Check(actor.id==squidId,"The open-water attacker was replicated to the crew.");
            Vector3 victimStart=guest.transform.position;
            outward=Vector3.ProjectOnPlane(victimStart-actor.position,Vector3.up).normalized;
            var side=Vector3.Cross(Vector3.up,outward).normalized;
            var gunnerStart=victimStart+side*3.2f+outward*.35f;
            if(!MarineSpawn.InOpenWater(world.CurrentDepth,gunnerStart+Vector3.up*.55f,.60f))
                gunnerStart=victimStart-side*3.2f+outward*.35f;
            host.Teleport(gunnerStart);
            Physics.SyncTransforms();
            if(!d.DefenseFixtureCapture(guest))throw new InvalidOperationException("Could not secure a victim in open water");
            float grabbedAt=Time.realtimeSinceStartup;
            await Wait(()=>d.DefenseFixtureTongueWrap(actor.id,out _,out var center) &&
                Vector3.Distance(center,guest.transform.position)<.25f,"the other diver is visibly wrapped by the tongue",2);
            host.LookAtConsole(guest.transform.position+Vector3.up*.23f);
            await Awaitable.NextFrameAsync();
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"08-squid-rescue.png"));
            while(Time.realtimeSinceStartup-grabbedAt<4.05f)
            {
                host.LookAtConsole(guest.transform.position+Vector3.up*.55f);
                await Awaitable.NextFrameAsync();
            }
            report.Check(guest.IsTrapped && !guest.IsDead,"The crew still has a chance to rescue after four seconds.");
            var captive=guest.transform.position;
            bool hasLane=false;
            for(int index=0;index<8;index++)
            {
                Vector3 around=Quaternion.AngleAxis(index*45f,Vector3.up)*side;
                Vector3 firingPoint=captive+around*3.1f+Vector3.up*.12f;
                Vector3 eye=firingPoint+Vector3.up*.6f;
                Vector3 target=captive+Vector3.up*.55f;
                if(!MarineSpawn.InOpenWater(world.CurrentDepth,firingPoint,.60f) ||
                    !HarpoonTool.AimCast(tool.tuning,eye,(target-eye).normalized,out var hit) ||
                    hit.collider.GetComponentInParent<DiverMotor>()!=guest)continue;
                host.Teleport(firingPoint);
                hasLane=true;
                break;
            }
            report.Check(hasLane,"The rescuer can get a clear shot next to the diver after the initial drag.");
            host.LookAtConsole(guest.transform.position+Vector3.up*.55f);
            deadline=Time.realtimeSinceStartup+3.5f;
            string sightline="no contact";
            while(!tool.OwnsFriendTether && Time.realtimeSinceStartup<deadline)
            {
                host.LookAtConsole(guest.transform.position+Vector3.up*.55f);
                Vector3 eye=host.transform.position+Vector3.up*.6f;
                Physics.SyncTransforms();
                if(HarpoonTool.AimCast(tool.tuning,eye,host.AimDirection,out var hit))
                {
                    var seen=hit.collider.GetComponentInParent<DiverMotor>();
                    sightline=hit.collider.name+" owner="+(seen?seen.OwnerId:-1)+" guest="+guest.OwnerId+
                        " captive="+guest.IsTrapped+" shooter slot="+tool.ActiveSlot+" sight="+Vector3.Distance(eye,guest.transform.position).ToString("0.0");
                    tool.DefenseFixtureWind(false);
                    if(seen==guest)tool.DefenseFixtureFire();
                }
                await Awaitable.NextFrameAsync();
            }
            report.Check(tool.OwnsFriendTether,"Existing friendly line attaches to captive. First contact: "+sightline);

            report.Check(guest.IsTrapped,"The diver remains captive before the teammate reels. "+sightline);
            await Wait(()=>d.TryDefenseFight(actor.id,out var held) && held.urgency==DefenseUrgency.Rescue,
                "the rescue contact is visible to the crew",1);
            deadline=Time.realtimeSinceStartup+4;
            while(guest.IsTrapped && Time.realtimeSinceStartup<deadline)
            {
                tool.DefenseFixtureWind(true);
                await Awaitable.NextFrameAsync();
            }
            tool.DefenseFixtureWind(false);
            report.Check(!guest.IsTrapped,"Winding a friendly line releases the other diver.");
            d.DefenseFixtureEnd();
            await Wait(()=>!d.DefenseActive && !guest.IsTrapped,"defense and all transport cleanup",13);
            await CarryDeadPredator(folder,report,world,host,tool,remoraCatch);
            report.phase="done";
        }

        static async Awaitable CarryDeadPredator(string folder,Report report,RaidWorld world,DiverMotor host,
            HarpoonTool tool,long catchId)
        {
            ReportPhase("corpse in the cage and aquarium",report,folder);
            var shoal=world.container.GetComponentInChildren<WetCargoShoal>();
            var cage=world.container.position;
            string species=DefenseCreatureAssets.Key(DefenseSpecies.Remora);
            report.Check(catchId>0 && shoal && tool.CatchSpeciesInSlot(2)==species,
                "The detached remora is stored as its own species in the pocket.");
            host.Teleport(cage+new Vector3(2.14f,-.18f,-.40f));
            host.LookAtConsole(cage);
            tool.DefenseFixtureSelect(2);
            await Wait(()=>tool.ActiveSlot==2 && CargoDispatch.CanLoad(world,host),"owner aiming into the cage opening",3);
            report.Check(tool.TrialLoadSelectedCatch(),"The owner can place the remora through the actual cage opening.");
            await Wait(()=>world.Team.cargo.Exists(item=>item.id==catchId && item.species==species) &&
                shoal.ModelFor(catchId)!=null,"the caught remora reaches the shared cage manifest",3);
            var model=shoal.ModelFor(catchId);
            var tail=model.Find("Tail");
            report.Check(tail!=null,"The cage uses the original articulated predator mesh.");
            var firstPose=tail.localRotation;
            await Wait(()=>Quaternion.Angle(firstPose,tail.localRotation)>3 && Mathf.Abs(model.localRotation.eulerAngles.z)>53,
                "the limp tail moves while the corpse lies on its side in the cage",3);
            report.Check(true,"Water moves the dead remora's joints and rolls the body in the cage.");
            host.LookAtConsole(model.position);
            await Awaitable.NextFrameAsync();
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"10-remora-cage.png"));

            var button=world.container.GetComponentInChildren<CargoDispatch>();
            host.Teleport(cage+new Vector3(3.33f,.19f,1.32f));
            host.LookAtConsole(button.transform.position);
            await Wait(()=>CargoDispatch.CanUse(host),"owner aiming at the hoist button",3);
            world.Command(WorldAction.Hoist,0);
            await Wait(()=>world.Team.tank.Exists(item=>item.id==catchId && item.species==species) &&
                shoal.TankCount>0,"normal lift sequence deposits the same catch in the aquarium",42);
            report.Check(shoal.ModelFor(catchId)==model &&
                world.Team.tank.FindAll(item=>item.id==catchId).Count==1,"The lift preserves the catch ID, species and exact view without duplication.");
            report.cagedPredator=report.deliveredPredator=catchId;
            var entryPose=tail.localRotation;
            var field=Nereid.NereidVessel.Active.GetComponentInChildren<Nereid.DefenseFieldView>();
            await Wait(()=>Quaternion.Angle(entryPose,tail.localRotation)>5 && field.ContainsWorld(model.position),
                "the dead remora settles within the aquarium and the tail responds",4);
            report.Check(true,"The rig stays limp and articulates inside the reservoir after delivery.");
            host.Teleport(model.position+new Vector3(2.1f,.32f,-1.8f));
            host.LookAtConsole(model.position);
            await Awaitable.WaitForSecondsAsync(.35f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"11-remora-aquarium.png"));
        }

        static async Awaitable LandHunter(string folder,Report report,ThreatDirector d,DiverMotor host,HarpoonTool tool)
        {
            ReportPhase("fast hunt and landing",report,folder);
            tool.DefenseFixtureSelect(0);
            await Wait(()=>tool.ActiveSlot==0,"re-equip the harpoon");
            if(!d.DefenseFixtureOpenWaterDuel(DefenseSpecies.Stalker,host,8,out int hunter))
                throw new InvalidOperationException("No clean Stalker entry");
            await Awaitable.NextFrameAsync();
            if(d.TryDefenseAnchor(hunter,out var at))host.LookAtConsole(at);
            float aimDeadline=Time.realtimeSinceStartup+3f;
            while(!tool.HasDefenseLine && Time.realtimeSinceStartup<aimDeadline)
            {
                if(!d.TryDefenseAnchor(hunter,out var point))break;
                host.LookAtConsole(point);
                Physics.SyncTransforms();
                Vector3 eye=host.transform.position+Vector3.up*.60f;
                if(HarpoonTool.AimCast(tool.tuning,eye,(point-eye).normalized,out var hit) && hit.collider.name=="Defense target")
                    tool.DefenseFixtureFire();
                await Awaitable.NextFrameAsync();
            }
            report.Check(tool.HasDefenseLine && tool.AttachedContact.defenseId==hunter,
                "The ordinary harpoon attaches to a swimming predator.");
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-stalker-hunt.png"));
            float start=Time.realtimeSinceStartup;
            int before=tool.CatchCount;
            bool winding=true;
            double warningAt=0;
            float maxNeedle=0;
            bool sawTug=false;
            var arc=UnityEngine.Object.FindAnyObjectByType<UI.LineTensionArc>();
            while(tool.HasDefenseLine && Time.realtimeSinceStartup-start<7)
            {
                if(!arc)arc=UnityEngine.Object.FindAnyObjectByType<UI.LineTensionArc>();
                if(tool.LineTension>.71f)winding=false;
                if(tool.LineTension<.40f)winding=true;
                if(tool.TryHookedDefense(out var fight))
                {
                    if(fight.fightPhase==FishBehaviour.SurgeWindup && arc && arc.Warning)
                    {
                        if(warningAt==0)UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-shared-reel-warning.png"));
                        warningAt=fight.fightAt;
                    }
                    if(fight.fightTug>0 && warningAt>0 && fight.fightAt-warningAt>.28)sawTug=true;
                    tool.DefenseFixtureWind(winding && fight.fightPhase!=FishBehaviour.Surge);
                }
                else tool.DefenseFixtureWind(winding);
                if(arc)maxNeedle=Mathf.Max(maxNeedle,arc.Load);
                if(d.TryDefenseAnchor(hunter,out var point))host.LookAtConsole(point);
                await Awaitable.NextFrameAsync();
            }
            tool.DefenseFixtureWind(false);
            report.Check(!tool.HasDefenseLine && tool.CatchCount==before+1,
                $"The swimming predator lands in {Time.realtimeSinceStartup-start:F2} seconds. " +
                $"Line {tool.HasDefenseLine}, spool {tool.LineLength:F2} m, catch {tool.CatchCount}, prior {before}.");
            report.Check(sawTug && maxNeedle>.08f,"The ordinary fishing needle loads and warns before a real predator tug.");
        }

        static async Awaitable PlaySolo(Report report,string folder)
        {
            var world=RaidWorld.Instance;
            var d=world.threats;
            var player=UI.UiRoot.Instance.Context.Player;
            report.peers=1;
            ReportPhase("autonomous abductor",report,folder);
            if(!d.DefenseFixtureBegin(.75f,6000))throw new InvalidOperationException("Cannot begin the solo raid");
            d.DefenseFixtureForceAssault();
            d.DefenseFixturePauseGroups();
            report.Check(d.DefenseFixtureOpenWaterDuel(DefenseSpecies.Abductor,player,3,out int attacker),
                "The solo player entered open water near a live abductor.");
            var actor=d.DefenseActorAt(0);
            player.CameraImpulse.BeginBackgroundQa(30);
            Debug.Log($"[SoloGrip] diver scene {player.gameObject.scene.name}, director scene {d.gameObject.scene.name}, diver {player.transform.position:F2}, predator {actor.position:F2}.");
            AttackerPose? lastPose=null;
            bool sawTongue=false;
            float nextMotionLog=0;
            await Wait(()=>
            {
                if(d.TryDefenseAnchor(attacker,out var point))player.LookAtConsole(point);
                for(int i=0;i<d.DefenseActorCount;i++)
                {
                    var sample=d.DefenseActorAt(i);
                    if(sample.id!=attacker || lastPose==sample.pose && Time.realtimeSinceStartup<nextMotionLog)continue;
                    nextMotionLog=Time.realtimeSinceStartup+1.1f;
                    lastPose=sample.pose;
                    sawTongue|=sample.pose==AttackerPose.Tongue;
                    Debug.Log("[SoloGrip] "+d.DefenseFixtureMotion(attacker,player));
                }
                return player.RaidGripVisual==attacker && player.CanBreakRaidGrip;
            },"natural abductor windup, strike and grip",14);
            report.Check(player.IsTrapped,"The ordinary attack grabbed a solo diver.");
            report.Check(sawTongue,"The natural windup launched the squid's tongue before the grip.");
            Vector3 grabbedAt=player.transform.position;
            await Wait(()=>
            {
                if(d.TryDefenseAnchor(attacker,out var point))player.LookAtConsole(point);
                return Vector3.Distance(grabbedAt,player.transform.position)>.65f;
            },"captured diver actively dragged in the current",2);
            report.Check(player.CanBreakRaidGrip,"The diver remained in the grip while being physically dragged.");
            await Wait(()=>d.DefenseFixtureTongueWrap(attacker,out _,out var center) &&
                Vector3.Distance(center,player.transform.position)<.25f,"the player's own torso is wrapped by the tongue",1);
            if(d.TryDefenseAnchor(attacker,out var squid))
            {
                Vector3 front=Vector3.ProjectOnPlane(squid-player.transform.position,Vector3.up).normalized;
                var oblique=Quaternion.AngleAxis(42,Vector3.up)*front;
                player.LookAtConsole(player.transform.position+oblique*.64f+Vector3.up*.18f);
                await Awaitable.NextFrameAsync();
                UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"08-first-person-tongue-wrap.png"));
            }
            var rescue=player.GetComponent<RescueTool>();
            rescue.QaStruggle(.37f);
            await Wait(()=>rescue.DisplayProgress>.07f,"F must build real server progress",1.1f);
            await Wait(()=>rescue.DisplayProgress==0,"released F must cancel progress",1.25f);
            report.Check(player.CanBreakRaidGrip,"A short tap or released hold cannot complete the breakout.");
            rescue.QaStruggle(5);
            await Wait(()=>
            {
                if(d.TryDefenseAnchor(attacker,out var point))player.LookAtConsole(point);
                return rescue.DisplayProgress>.37f;
            },"visible struggle before release",1.4f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"08-solo-struggle.png"));
            await Wait(()=>!player.CanBreakRaidGrip,"a full F hold releases the server grip",4.5f);
            await Wait(()=>!player.IsTrapped && player.RaidStrugglePose(out _,out float kick) && kick>.18f,
                "the animated kick follows the release",.58f);
            report.Check(player.RaidGripVisual==0,"A full F hold frees the diver and starts the release animation.");
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"09-solo-breakout.png"));
            ReportPhase("unrescued squid pull",report,folder);
            d.DefenseFixtureEnd();
            await Wait(()=>!d.DefenseActive && player.CanBeRaidCaptive,"next legitimate squid attempt",15);
            if(!d.DefenseFixtureBegin(.75f,6000))throw new InvalidOperationException("Cannot begin another squid attack");
            d.DefenseFixtureForceAssault();d.DefenseFixturePauseGroups();
            if(!d.DefenseFixtureOpenWaterDuel(DefenseSpecies.Abductor,player,4,out int next))
                throw new InvalidOperationException("The next squid needs an open approach");
            await Wait(()=>player.RaidGripVisual==next,"squid catches the unresisting diver",12);
            float startPull=Time.realtimeSinceStartup;
            await Wait(()=>player.IsDead && player.IsConsumed,"the squid pulls a captive into its mouth",12);
            float elapsed=Time.realtimeSinceStartup-startPull;
            report.Check(elapsed>=4.65f && elapsed<11,"The unrescued diver has a readable pull window and dies at the beak.");
            report.phase="done";
        }

        static void ReportPhase(string phase,Report report,string folder)
        {
            report.phase=phase;
            Write(Path.Combine(folder,"defense.json"),report);
            Debug.Log("[DefenseQA] "+phase);
        }
    }
}
#endif
