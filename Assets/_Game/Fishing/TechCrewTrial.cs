#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Raid
{
    // One opt-in fixture per independent player process. Only the host seeds the disposable campaign.
    internal sealed partial class TechCrewTrial : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public int seat, owner, players;
            public int fleetCount, lastRigSerial, trackedCatchIds, snapshotBytes;
            public string fleetHash;
            public string status="running", phase, error;
            public List<string> checks=new();
        }
        readonly Report report=new();
        readonly MaterialCost initialWallet=new(1000,1000,1000,1000);
        string folder,save;
        RaidWorld world;
        DiverMotor local;
        ShipConsole station;
        bool Host => report.seat==0;
        int PeerId => int.Parse(File.ReadAllText(FileFor("seat-1")));
        static UiRoot UI => UiRoot.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            string Read(string flag)
            {
                int index=Array.IndexOf(args,flag);
                return index>=0 && index+1<args.Length?args[index+1]:null;
            }
            if(Read("-raid-tech-crew")==null)return;
            string dir=Path.GetFullPath(Read("-raid-tech-crew"));
            if(!Directory.Exists(dir) || !Guid.TryParse(Read("-raid-profile"),out _) ||
                !int.TryParse(Read("-raid-tech-seat"),out int seat) || !int.TryParse(Read("-raid-tech-players"),out int players) ||
                (players!=2 && players!=4) || seat<0 || seat>=players ||
                !string.Equals(Path.GetFullPath(Read("-raid-save")),Path.Combine(dir,"trial-save.json"),StringComparison.OrdinalIgnoreCase) ||
                !args.Contains(seat==0?"-raid-host":"-raid-client"))
                throw new ArgumentException("Technology network trial requires its own folder, trial-save.json, profile, seat, player count and role.");
            var trial=new GameObject("Technology network crew trial").AddComponent<TechCrewTrial>();
            trial.folder=dir;trial.save=Path.Combine(dir,"trial-save.json");
            trial.report.seat=seat;trial.report.players=players;
            Application.runInBackground=true;
            trial.StartCoroutine(trial.Verify());
        }

        string FileFor(string name) => Path.Combine(folder,name);
        void Mark(string name,string value="ready")
        {
            var file=FileFor(name);
            File.WriteAllText(file+".tmp",value);
            File.Move(file+".tmp",file);
        }
        bool Has(string name) => File.Exists(FileFor(name));
        bool All(string prefix) => Enumerable.Range(0,report.players).All(i=>Has(prefix+i));
        void Write() => File.WriteAllText(FileFor($"result-{report.seat}.json"),JsonUtility.ToJson(report,true));
        void Assert(bool pass,string description)
        {
            if(!pass)throw new InvalidOperationException(description);
            report.checks.Add(description);Write();
        }

        IEnumerator Wait(string phase,Func<bool> condition,float timeout=35)
        {
            report.phase=phase;Write();
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!condition())
            {
                if(Time.realtimeSinceStartup>=deadline)throw new TimeoutException(phase);
                yield return new WaitForSecondsRealtime(.04f);
            }
        }

        IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>();
            stack.Push(Run());
            while(stack.Count>0)
            {
                object delay;
                try
                {
                    var step=stack.Peek();
                    if(!step.MoveNext()){stack.Pop();continue;}
                    if(step.Current is IEnumerator next){stack.Push(next);continue;}
                    delay=step.Current;
                }
                catch(Exception e){report.status="failed";report.error=e.ToString();break;}
                yield return delay;
            }
            if(report.status=="running")report.status="passed";
            Write();Debug.Log($"[TechCrew] seat {report.seat}: {report.status}; {report.phase}; {report.error}");
            // Allow the remote player to acknowledge the departing carrier before the host shuts down.
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
            Destroy(gameObject);
        }

        IEnumerator Run()
        {
            yield return Wait("local owned diver with bound private profile",()=>
            {
                world=RaidWorld.Instance;
                local=UI?.Context?.Player;
                return local && local.IsOwner && world && world.View!=null && world.LocalProgress!=null;
            },105);
            report.owner=local.OwnerId;
            Mark("seat-"+report.seat,report.owner.ToString());
            yield return Wait("all independent profiles",()=>All("seat-"),100);
            station=ShipConsole.Stations.Single(s=>s.kind==ShipConsoleKind.SkillTree);
            var screen=station.screenAnchor;
            if(Host)
            {
                Assert(world.Team.techPurchases.Count==0 && world.Team.rigs.Count==0 &&
                    world.Team.Treasury.Equals(default(MaterialCost)),"fresh fixture campaign");
                Assert(Enumerable.Range(0,report.players).Select(i=>File.ReadAllText(FileFor("seat-"+i))).Distinct().Count()==report.players,
                    "each process bound a distinct network owner");
                local.Teleport(screen.position-screen.forward*1.71f+screen.right*.64f-Vector3.up*.38f);
                var peer=DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId);
                peer.Teleport(screen.position-screen.forward*1.66f-screen.right*.72f-Vector3.up*.39f);
                world.Team.Treasury=initialWallet;
                Mark("setup",world.Team.teamRevision.ToString());
            }
            yield return Wait("replicated starting treasury",()=>Has("setup") && world.View.Treasury.Equals(initialWallet));
            if(report.seat<2)
            {
                yield return Wait("at the vessel research monitor",()=>station.InRange(local) && local.IsInside);
                yield return new WaitForSecondsRealtime(.55f);
                local.LookAtConsole(screen.position);
                yield return new WaitForSecondsRealtime(.45f);
                Assert(UI.QaPointAtMonitor() && UI.Page==UiPage.SkillTree,"opens the vessel skill terminal");
                if(Host)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    var sector=UI.GetComponentsInChildren<Button>().Single(b=>b.name=="Sector8");
                    local.LookAtConsole(UiQaProbe.ReticleTarget(sector));
                    yield return new WaitForSecondsRealtime(.35f);
                    Assert(UI.QaClickReticle(),"clicks ninth sector with the physical reticle");
                    yield return Wait("technology workshop is visible",()=>UI.GetComponentsInChildren<Button>().Any(b=>b.name=="TechCategory3"));
                    local.LookAtConsole(screen.position);
                    yield return new WaitForSecondsRealtime(.35f);
                    if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                        UiQaProbe.CapturePeerCamera(FileFor("workshop.png"));
                }
                local.LookAtConsole(screen.position);
            }
            Mark("ready-"+report.seat);
            if(Host)
            {
                yield return Wait("both physical sight lines validated by the server",()=>All("ready-") &&
                    ShipConsole.CanUseKind(local,ShipConsoleKind.SkillTree) &&
                    ShipConsole.CanUseKind(DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId),ShipConsoleKind.SkillTree));
                Mark("race");
            }
            yield return Wait("simultaneous purchase barrier",()=>Has("race"));
            int revision=int.Parse(File.ReadAllText(FileFor("setup")));
            if(report.seat<2)
            {
                bool? accepted=null;
                string explanation=null;
                void Receipt(bool ok,string reason){accepted=ok;explanation=reason;}
                world.SkillResponded+=Receipt;
                local.CmdTechBuy("trap","unlock",0,revision);
                yield return Wait("server replied to this owner's request",()=>accepted.HasValue);
                world.SkillResponded-=Receipt;
                Mark("receipt-"+report.seat,accepted==true?"accepted":"rejected");
                Assert(!string.IsNullOrEmpty(explanation),"server receipt: "+explanation);
            }
            yield return Wait("both purchase receipts",()=>Has("receipt-0") && Has("receipt-1"));
            Assert(new[]{"receipt-0","receipt-1"}.Count(key=>File.ReadAllText(FileFor(key))=="accepted")==1,
                "server accepted exactly one contender and explicitly rejected the other");
            yield return Wait("first research accepted on every seat",()=>world.View.TechRank("trap","unlock")==1);
            var balance=initialWallet.Subtract(TechCatalog.Find("trap").Upgrade("unlock").NextCost(0));
            Assert(world.View.teamRevision==revision+1 && world.View.Treasury.Equals(balance) && world.View.rigs.Count==0,
                "two requests for the same rank cost one recipe and issue zero units");
            Mark("purchased-"+report.seat);
            yield return Wait("all clients verified the transaction",()=>All("purchased-"));
            yield return new WaitForSecondsRealtime(.5f);
            if(report.seat==1)local.CmdTechBuy("cage","unlock",0,world.View.teamRevision);
            yield return Wait("shared cage blueprint",()=>world.View.TechRank("cage","unlock")==1);
            Assert(world.View.rigs.Count==0,"new blueprint requires explicit issuance");
            Mark("blueprint-"+report.seat);
            yield return Wait("everyone received the blueprint",()=>All("blueprint-"));
            yield return VerifyDispenser();
            yield return Wait("issued cage replicated",()=>world.View.rigs.Any(r=>r.kind=="cage" && r.operatorId==PeerId));
            int serial=world.View.rigs.Single(r=>r.kind=="cage").serial;
            Assert(serial>0,"one issued heavy cage has the same carrier on this peer");
            Mark("issued-"+report.seat);
            yield return Wait("all clients observed issuance",()=>All("issued-"));
            if(Host)
            {
                // Select an unobstructed approach beside the cage for the helper's real carry RPC.
                var rig=world.Team.rigs.Single(r=>r.serial==serial);
                var carrier=DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId);
                Vector3 side=carrier.transform.right;
                var candidates=new[]{ side,-side,-carrier.transform.forward,carrier.transform.forward };
                foreach(var approach in candidates)
                {
                    var target=rig.position+approach*.95f-Vector3.up*.35f;
                    if(Physics.Linecast(target+Vector3.up*.65f,rig.position,~0,QueryTriggerInteraction.Ignore))continue;
                    local.Teleport(target);break;
                }
                yield return new WaitForSecondsRealtime(.5f);
                local.CmdTechUse(serial,"carry");
            }
            yield return Wait("second owner takes a handle",()=>world.View.rigs.Exists(r=>r.serial==serial && r.helperId>=0 && r.operatorId==PeerId));
            if(Host && SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                local.LookAtConsole(world.View.rigs.Single(r=>r.serial==serial).position);
                yield return new WaitForSecondsRealtime(.3f);
                UiQaProbe.CapturePeerCamera(FileFor("cooperative-cage.png"));
            }
            Mark("paired-"+report.seat);
            yield return Wait("crew observed both handles",()=>All("paired-"));
            string peerProfile=Host?world.ProgressKey(DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId)):null;
            yield return VerifyFleet();
            if(report.seat==1)
            {
                FindAnyObjectByType<RaidSession>().Disconnect();
                yield return Wait("client actually disconnected",()=>!FindAnyObjectByType<RaidSession>().Connected);
                Mark("left");
            }
            else
            {
                yield return Wait("surviving helper becomes owner",()=>Has("left") && world.View.rigs.Exists(r=>
                    r.serial==serial && r.mode=="carried" && r.operatorId==int.Parse(File.ReadAllText(FileFor("seat-0"))) && r.helperId<0));
                Assert(world.View.rigs.Count(r=>r.serial==serial)==1,"disconnect preserves exactly one physical installation and transfers the handle");
                Mark("handoff-"+report.seat);
                if(Host)
                {
                    yield return Wait("remaining crew confirmed handoff",()=>Enumerable.Range(0,report.players).All(i=>i==1 || Has("handoff-"+i)));
                    local.CmdTechUse(serial,"release");
                    yield return Wait("owner set down the installation",()=>world.View.rigs.Exists(r=>r.serial==serial && r.CanRecall));
                    var saved=TeamSave.Load(save);
                    Assert(saved.TechRank("cage","unlock")==1 && saved.rigs.Count==report.fleetCount &&
                        saved.rigs.Single(r=>r.serial==serial).CanRecall && FleetDigest(saved)==report.fleetHash,
                        "disk reload retains the released cage, fleet and complete catch manifest");
                    Assert(InventoryDigest(saved.Player(peerProfile))==File.ReadAllText(FileFor("private-stacks")),
                        "disk reload retains the private slot types, counts and inventory revision");
                    Mark("finished");
                }
            }
            yield return Wait("host completed persistence checks",()=>Has("finished"));
            yield return VerifyReconnection(serial,peerProfile);
        }
    }
}
#endif
