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
            public float minimumIntegrity=100;
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
                await Play(report,folder,peers);
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
                if(!MarineSpawn.ClearLeg(at+Vector3.up*.6f,look,.12f,float.PositiveInfinity))continue;
                diver.Teleport(at);
                diver.LookAtConsole(look);
                return;
            }
            diver.LookAtConsole(look);
        }

        static async Awaitable Shot(string output,DiverMotor host,ThreatDirector director,Vector3 point,string name)
        {
            WatchField(host,director,point);
            await Awaitable.WaitForSecondsAsync(.27f);
            UI.UiQaProbe.CapturePeerCamera(Path.Combine(output,name+".png"));
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
            ReportPhase("underfed cycle",report,folder);
            team.offeringPoints=team.RequiredFood(world.progression)/3;
            team.hungerRemaining=.05f;
            await Wait(()=>d.Defense.phase==DefensePhase.Warning,"underfeeding warning");
            report.Check(team.feedingCycle==2 && d.Defense.budget>0,"Partial feeding queues one raid for the finished meal.");
            float interval=team.hungerRemaining;
            await Wait(()=>d.Defense.phase==DefensePhase.Encirclement,"encirclement");
            await Shot(folder,host,d,d.DefenseCenter,"02-encirclement");
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
                    await Shot(folder,host,d,a.position,"04-"+kind);
                    break;
                }
            }
            report.Check(report.species==31,"All five interactive creature profiles appeared in the main level.");
            d.DefenseFixtureEnd();
            await Wait(()=>!d.DefenseActive,"successful recovery",22);
            report.Check(team.tank.Count==20 && team.ongoingDefense==null && d.Defense.Ratio==1,"Successful defense keeps the manifest and restores the shield.");
            report.Check(team.hungerRemaining<interval || Mathf.Abs(team.hungerRemaining-interval)<.05f,"The clock restarts with a single next cycle.");

            ReportPhase("breach and real theft",report,folder);
            if(!d.DefenseFixtureBegin(1,team.RequiredFood(world.progression)*2))throw new InvalidOperationException("Failure fixture did not start");
            d.DefenseFixtureForceAssault();
            if(!d.DefenseFixtureSpawn(DefenseSpecies.Thief,false,1,8.5f) ||
                !d.DefenseFixtureSpawn(DefenseSpecies.Thief,false,4,14f))throw new InvalidOperationException("Could not stage fleeing carriers");
            d.DefenseFixtureSetIntegrity(0);
            report.Check(d.Defense.breached && team.ongoingDefense.lossLimit>0 && team.tank.Count==20,"A breach opens theft without instantly removing stock.");
            await Wait(()=>d.DefenseActorCount>0 && team.ongoingDefense?.inTransit.Count>0,"a thief collects the original fish",13);
            for(int i=0;i<d.DefenseActorCount;i++)
            {
                var actor=d.DefenseActorAt(i);
                if(actor.stolenId==0)continue;
                await Shot(folder,host,d,actor.position,"05-carrier");
                if(d.TryDefenseAnchor(actor.id,out var current))d.BlastDefense(current,.9f,180);
                break;
            }
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

        static void ReportPhase(string phase,Report report,string folder)
        {
            report.phase=phase;
            Write(Path.Combine(folder,"defense.json"),report);
            Debug.Log("[DefenseQA] "+phase);
        }
    }
}
#endif
