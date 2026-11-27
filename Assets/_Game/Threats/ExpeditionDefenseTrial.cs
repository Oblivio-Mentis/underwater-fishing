#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Raid
{
    // A fixture runs only with an isolated campaign path and a development player.
    static class ExpeditionDefenseTrial
    {
        [Serializable] sealed class Report
        {
            public string status="running", phase="", error="";
            public int peers, species, phases, hits, losses, recovered;
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
                if(Array.IndexOf(args,"-raid-defense-combat")>=0)await PlayCombat(report,folder,peers);
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

        static async Awaitable SwarmFrame(string folder,DiverMotor player,ThreatDirector director)
        {
            var vessel=Nereid.NereidVessel.Active;
            var center=director.DefenseCenter;
            float side=Vector3.Dot(director.Defense.approachEnd-center,vessel.transform.right)<0?-1:1;
            var observation=vessel.ObservationRoom();
            if(!observation)throw new InvalidOperationException("The observation hall is absent.");
            var at=observation.TransformPoint(new Vector3(side<0?-4.68f:3.24f,side<0?1.55f:.22f,.55f));
            player.Teleport(at);
            float ready=Time.realtimeSinceStartup+.68f;
            do
            {
                await Awaitable.NextFrameAsync();
                player.LookAtConsole(director.DefenseApproachFocus);
            } while(Time.realtimeSinceStartup<ready);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"02-swarm-through-glass.png"));
            var eye=UnderwaterLook.Instance.DiverCamera;
            Debug.Log($"[DefenseSight] Camera {eye.transform.position:F2} focus {director.DefenseApproachFocus:F2} " +
                $"viewport {eye.WorldToViewportPoint(director.DefenseApproachFocus):F2} silhouettes {director.VisibleDefenseSwarm} " +
                $"start {director.Defense.approachStart:F2} arrival {director.Defense.approachEnd:F2}.");
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
            ReportPhase("underfed cycle",report,folder);
            team.offeringPoints=team.RequiredFood(world.progression)/3;
            team.hungerRemaining=.05f;
            await Wait(()=>d.Defense.phase==DefensePhase.Warning,"underfeeding warning");
            report.Check(team.feedingCycle==2 && d.Defense.budget>0,"Partial feeding queues one raid for the finished meal.");
            report.Check(d.DefenseCountdown>44 && d.DefenseCountdown<=45.1f && !d.DefenseAlarm,
                "The raid begins with a 45-second countdown and the ship awaits the incoming school.");
            await Shot(folder,host,d,d.Defense.approachStart,"01-countdown");
            float interval=team.hungerRemaining;
            await Wait(()=>d.Defense.phase==DefensePhase.Encirclement,"school approach",d.defenseTuning.warningSeconds+4);
            await Awaitable.WaitForSecondsAsync(2f);
            var lighting=prefab.GetComponentInChildren<Nereid.NereidDefenseLighting>();
            int aimed=0;
            if(lighting)foreach(var lamp in lighting.searchlights)
                if(lamp.enabled && Vector3.Angle(lamp.transform.forward,lighting.Target-lamp.transform.position)<12)aimed++;
            report.Check(lighting && lighting.AlarmActive && aimed==2 && Vector3.Distance(lighting.Target,d.DefenseApproachFocus)<1 && d.DefenseCountdown>0,
                "The alarm and exterior spotlights follow the synchronized approaching school before the first attack.");
            await SwarmFrame(folder,host,d);
            float glassHaze=Shader.GetGlobalFloat("_RaidDryHaze");
            var look=UnderwaterLook.Instance;
            bool dryCamera=look && look.DiverCamera && !look.EyeSubmerged;
            report.Check(dryCamera && glassHaze>.32f && d.VisibleDefenseSwarm>0,
                $"A dry observation deck camera sees exterior haze and the swarm. Dry={dryCamera}, haze={glassHaze:F2}, silhouettes={d.VisibleDefenseSwarm}.");
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
            report.Check(team.hungerRemaining<interval || Mathf.Abs(team.hungerRemaining-interval)<.05f,"The clock restarts with a single next cycle.");

            ReportPhase("breach and real theft",report,folder);
            if(!d.DefenseFixtureBegin(1,team.RequiredFood(world.progression)*2))throw new InvalidOperationException("Failure fixture did not start");
            d.DefenseFixtureForceAssault();
            if(!d.DefenseFixtureThief(1,1,8.5f) || !d.DefenseFixtureThief(4,-1,14f))
                throw new InvalidOperationException("Could not stage fleeing carriers at the two lateral approaches");
            d.DefenseFixtureSetIntegrity(0);
            report.Check(d.Defense.breached && team.ongoingDefense.lossLimit>0 && team.tank.Count==20,"A breach opens theft without instantly removing stock.");
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
            watching.Teleport(departure+Vector3.left*.85f);
            await Wait(()=>d.DefenseFixtureGrip(carrier,out float gap) && gap<.18f,
                "the rendered prey reaches the moving mouth socket",1.6f);
            report.Check(true,"The stolen fish follows the moving mouth socket.");
            await ShotPredator(folder,host,d,carrier,"05-carrier");
            float watchesUntil=Time.realtimeSinceStartup+1.55f;
            while(Time.realtimeSinceStartup<watchesUntil)
            {
                if(!d.TryDefenseAnchor(carrier,out var at))throw new InvalidOperationException("A guest watched the carrier disappear.");
                watching.Teleport(at+Vector3.left*.85f);
                await Awaitable.NextFrameAsync();
            }
            if(!d.TryDefenseAnchor(carrier,out var current))throw new InvalidOperationException("No recoverable carrier");
            report.Check(Vector3.Distance(current,departure)>5,"The carrier accelerates away and remains while a guest is close enough to see it.");
            d.BlastDefense(current,.20f,180);
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
            await Wait(()=>d.DefenseActorCount==1 && d.DefenseActorAt(0).pose==AttackerPose.Latch,"remora attached",14);
            var remora=d.DefenseActorAt(0);
            var outward=(remora.position-d.DefenseCenter).normalized;
            host.Teleport(remora.position+outward*2.8f-Vector3.up*.48f);
            host.LookAtConsole(remora.position);
            await Awaitable.WaitForSecondsAsync(.16f);
            tool.DefenseFixtureFire();
            await Wait(()=>tool.HasDefenseLine && tool.AttachedContact.defenseId==remora.id,"server validated the shot into the remora",3);
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
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,"07-remora-ink.png"));

            ReportPhase("captive rescue",report,folder);
            DiverMotor guest=null;
            foreach(var diver in DiverMotor.ServerDivers)if(diver && diver!=host){guest=diver;break;}
            if(!guest)throw new InvalidOperationException("An independent diver is required");
            tool.DefenseFixtureSelect(0);
            await Wait(()=>tool.ActiveSlot==0,"equipped harpoon");
            if(!d.DefenseFixtureSpawn(DefenseSpecies.Abductor,false,3,12))throw new InvalidOperationException("No abductor entry");
            var actor=d.DefenseActorAt(d.DefenseActorCount-1);
            outward=(actor.position-d.DefenseCenter).normalized;
            var side=Vector3.Cross(Vector3.up,outward).normalized;
            Vector3 victimStart=actor.position+side*.9f-Vector3.up*1.13f;
            Vector3 gunnerStart=victimStart+side*3.2f+outward*.55f;
            if(!MarineSpawn.InOpenWater(world.CurrentDepth,gunnerStart+Vector3.up*.55f,.60f))
            {
                victimStart-=side*1.8f;
                gunnerStart=victimStart-side*3.2f+outward*.55f;
            }
            guest.Teleport(victimStart);
            host.Teleport(gunnerStart);
            Physics.SyncTransforms();
            if(!d.DefenseFixtureCapture(guest))throw new InvalidOperationException("Could not secure a victim in open water");
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
