#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Raid.UI
{
    internal sealed class Raid019SkillTrial : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string status = "running", phase, error;
            public List<string> checks = new();
        }

        readonly Report report = new();
        string folder, peerDirectory;
        RaidWorld world;
        DiverMotor host, peer;
        ShipConsole station;
        static UiRoot Ui => UiRoot.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var arguments=Environment.GetCommandLineArgs();
            string Read(string flag)
            {
                int index=Array.IndexOf(arguments,flag);
                return index>=0 && index+1<arguments.Length?arguments[index+1]:"";
            }
            string output=Read("-raid-skills-trial"), remote=Read("-raid-skills-peer");
            if(output=="" || remote=="" || Read("-raid-save")=="") return;
            var trial=new GameObject("RAID019 radial progression validation").AddComponent<Raid019SkillTrial>();
            trial.folder=Path.GetFullPath(output);
            trial.peerDirectory=Path.GetFullPath(remote);
            Directory.CreateDirectory(trial.folder);
            Directory.CreateDirectory(trial.peerDirectory);
            trial.StartCoroutine(trial.Verify());
        }

        IEnumerator Verify()
        {
            var routines=new Stack<IEnumerator>();
            routines.Push(RunTrial());
            while(routines.Count>0)
            {
                object next;
                try
                {
                    var routine=routines.Peek();
                    if(!routine.MoveNext()){routines.Pop();continue;}
                    next=routine.Current;
                    if(next is IEnumerator child){routines.Push(child);continue;}
                }
                catch(Exception e){report.status="failed";report.error=e.ToString();break;}
                yield return next;
            }
            if(report.status=="running")report.status="passed";
            Write();
        }

        void Write() => File.WriteAllText(Path.Combine(folder,"skill-results.json"),JsonUtility.ToJson(report,true));
        void Check(string text,bool pass)
        {
            if(!pass)throw new InvalidOperationException(text);
            report.checks.Add(text);
            Write();
        }

        IEnumerator Wait(string text,Func<bool> condition,float timeout=9)
        {
            report.phase=text;
            Write();
            float until=Time.realtimeSinceStartup+timeout;
            while(!condition())
            {
                if(Time.realtimeSinceStartup>until)throw new TimeoutException(text);
                yield return null;
            }
        }

        UiQaProbe.Snapshot Remote()
        {
            try {return JsonUtility.FromJson<UiQaProbe.Snapshot>(File.ReadAllText(Path.Combine(peerDirectory,"state.json")));}
            catch(IOException){return null;}
            catch(ArgumentException){return null;}
        }

        TeamProgress RemoteTeam()
        {
            string summary=Remote()?.team;
            return string.IsNullOrEmpty(summary)?null:JsonUtility.FromJson<TeamProgress>(summary);
        }

        string QueuePeer(string action,string argument="",float number=0)
        {
            string id=Guid.NewGuid().ToString("N");
            var request=new UiQaProbe.Request{id=id,action=action,name=argument,value=argument,number=number};
            string temp=Path.Combine(peerDirectory,"request.tmp");
            File.WriteAllText(temp,JsonUtility.ToJson(request));
            string requestPath=Path.Combine(peerDirectory,"request.json");
            if(File.Exists(requestPath))File.Delete(requestPath);
            File.Move(temp,requestPath);
            return id;
        }

        IEnumerator Send(string action,string argument="",float number=0)
        {
            string id=QueuePeer(action,argument,number);
            yield return Wait("peer "+action+" "+argument,()=>Remote()?.request==id,12);
            string failure=Remote()?.error;
            if(!string.IsNullOrEmpty(failure))throw new InvalidOperationException(failure);
        }

        IEnumerator AimHost(Transform control)
        {
            var rect=control as RectTransform;
            Vector3 point=rect==null?control.position:rect.TransformPoint(rect.rect.center);
            host.LookAtConsole(point);
            yield return new WaitForSecondsRealtime(.25f);
        }

        string HitDescription(DiverMotor diver)
        {
            var view=diver.GetComponentInChildren<Camera>();
            var ray=view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            string surface=Physics.Raycast(ray,out var hit,4f,~0,QueryTriggerInteraction.Ignore)?
                hit.collider.name+"/"+hit.collider.GetComponentInParent<ShipConsole>()?.kind : "nothing";
            var aimed=ShipConsole.ResolveSight(diver,view);
            return $"page {Ui.Page}; reticle {Ui.ReticleControl}; first hit {surface}; client {aimed?.kind}; server {ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree)}; eye {ray.origin}; direction {ray.direction}";
        }

        IEnumerator ClickHost(string control,bool buy=false)
        {
            var button=Ui.GetComponentsInChildren<UnityEngine.UI.Button>()
                .First(item=>item.gameObject.name==control && item.IsActive());
            host.LookAtConsole(UiQaProbe.ReticleTarget(button));
            yield return new WaitForSecondsRealtime(.25f);
            var before=HitDescription(host);
            bool clicked=buy?Ui.QaBuyReticle():Ui.QaClickReticle();
            if(!clicked)throw new InvalidOperationException("Host could not click "+control+"; "+before);
            Check("Host reticle " + (buy?"RMB ":"LMB ") + control,true);
        }

        IEnumerator ClickPeer(string control,bool buy=false)
        {
            yield return Send("reticle_aim",control);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Send(buy?"reticle_buy":"reticle_click");
        }

        IEnumerator RunTrial()
        {
            yield return Wait("Distinct connected Windows players with private progress",
                () =>
                {
                    world=RaidWorld.Instance;
                    if(world?.Team==null || Ui?.Context?.Connected!=true)return false;
                    host=Ui.Context.Player;
                    peer=DiverMotor.ServerDivers.Find(d=>d!=null && !d.IsOwner);
                    return peer!=null && world.Team.Player(world.ProgressKey(host))!=null
                        && world.Team.Player(world.ProgressKey(peer))!=null && Remote()?.localProgress!=null;
                },55);
            var a=world.Team.Player(world.ProgressKey(host));
            var b=world.Team.Player(world.ProgressKey(peer));
            Check("Two distinct profile bindings",a!=b);
            world.Team.Treasury=new MaterialCost(200,200,200,200);
            yield return Wait("Both screens receive the shared treasury",
                ()=>world.View?.Treasury.biomass==200 && RemoteTeam()?.Treasury.biomass==200);
            station=ShipConsole.Stations.Single(s=>s.kind==ShipConsoleKind.SkillTree);
            Check("One progression station, decorative workshop cannot receive commands",
                !ShipConsole.Stations.Any(s=>s.kind==ShipConsoleKind.Workshop));
            Transform screen=station.screenAnchor;
            host.Teleport(screen.position-screen.forward*1.71f+screen.right*.64f-Vector3.up*.38f);
            peer.Teleport(screen.position-screen.forward*1.66f-screen.right*.72f-Vector3.up*.39f);
            yield return Wait("Both players near the physical monitor",()=>host.IsInside && peer.IsInside &&
                station.InRange(host) && station.InRange(peer),10);
            yield return new WaitForSecondsRealtime(.5f);

            yield return AimHost(screen);
            Check("Left click opens the physical dial",Ui.QaClickReticle() && Ui.Page==UiPage.SkillTree);
            yield return Send("reticle_aim",station.name);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Send("reticle_click");
            yield return Wait("Both clients show the physical dial",()=>Remote()?.page=="SkillTree" &&
                Ui.GetComponentsInChildren<SkillSectorButton>().Count(v=>v.IsActive())==8);
            Check("Physical monitor reserves normal gameplay",UiInputGate.Blocked && UiInputGate.SkillMonitorLook && Remote()?.inputBlocked==true);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"01-radial-720.png"));
            yield return Send("capture","01-radial-1080.png");

            yield return ClickHost("Sector0");
            yield return Wait("Harpoon detail displays 31 real rank nodes",()=>Ui.GetComponentsInChildren<SkillPurchaseButton>().Length==31);
            yield return new WaitForSecondsRealtime(.63f);
            yield return ClickHost("harpoon.cable.03");
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"02-cable-iii-locked-720.png"));
            var before=world.Team.Treasury;
            var cableIII=SkillTreeDefinition.Id(SkillLine.Cable,3);
            world.RequestSkill(cableIII,world.Team.teamRevision,a.revision);
            yield return new WaitForSecondsRealtime(.35f);
            Check("Server refuses III without II",!a.skills.Contains(cableIII) && world.Team.Treasury.Equals(before));

            var inner=Ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="ProgressionGraphViewport");
            var graph=inner.Find("Skill branch graph") as RectTransform;
            Vector2 openSpace=inner.rect.center+new Vector2(0,-241);
            host.LookAtConsole(inner.TransformPoint(openSpace));
            yield return new WaitForSecondsRealtime(.30f);
            Ui.QaScrollReticle(1);
            yield return new WaitForSecondsRealtime(.48f);
            var origin=graph.anchoredPosition;
            Ui.QaDragReticle(true,false);
            host.LookAtConsole(inner.TransformPoint(openSpace+new Vector2(105,0)));
            yield return new WaitForSecondsRealtime(.35f);
            Ui.QaDragReticle(false,true);
            yield return Wait("Empty-field drag shifts the same graph",()=>Vector2.Distance(graph.anchoredPosition,origin)>10);
            Check("Zoom and pan do not change ownership or treasury",a.skills.Count==0 && world.Team.Treasury.Equals(before));
            yield return ClickHost("ProgressionCenter");
            yield return new WaitForSecondsRealtime(.45f);

            for(int rank=1;rank<=3;rank++)
            {
                var id=SkillTreeDefinition.Id(SkillLine.Cable,rank);
                var node=SkillTreeDefinition.Get(id);
                yield return ClickHost(node.Key);
                yield return ClickHost(node.Key,true);
                yield return Wait("Confirmed Cable "+SkillTreeDefinition.Roman(rank),()=>a.skills.Contains(id) &&
                    world.LocalProgress.skills.Contains(id) && world.View.teamRevision==world.Team.teamRevision &&
                    RemoteTeam()?.teamRevision==world.Team.teamRevision);
                before=before.Subtract(node.Cost);
                Check("Confirmed price matches the debit at Cable "+rank,world.Team.Treasury.Equals(before));
                UiQaProbe.CapturePeerCamera(Path.Combine(folder,"03-cable-rank-"+rank+"-720.png"));
            }
            Check("Cable III applies +36% on the server for only the buyer",
                Mathf.Abs(world.SkillFactor(host,SkillLine.Cable)-.36f)<.001f && world.SkillValue(peer,SkillLine.Cable)==0);
            yield return ClickHost("ProgressionStage5");
            yield return new WaitForSecondsRealtime(.55f);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"04-harpoon-stage-v-720.png"));
            yield return ClickPeer("Sector1");
            yield return new WaitForSecondsRealtime(.63f);
            var oxygen=SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.Oxygen,1));
            yield return ClickPeer(oxygen.Key,true);
            yield return Wait("Only peer's oxygen capacity increases",()=>b.skills.Contains(oxygen.Id) &&
                Remote()?.localProgress?.skills.Contains(oxygen.Id)==true &&
                Mathf.Abs(peer.OxygenMultiplier-1.20f)<.002f && Mathf.Abs(host.OxygenMultiplier-1)<.002f);
            before=before.Subtract(oxygen.Cost);
            Check("Different private profiles spend the same treasury",!a.skills.Contains(oxygen.Id) &&
                !b.skills.Contains(cableIII) && world.Team.Treasury.Equals(before));
            yield return Send("capture","05-oxygen-peer-1080.png");

            yield return ClickHost("ProgressionOverview");
            yield return ClickPeer("ProgressionOverview");
            yield return new WaitForSecondsRealtime(.63f);
            yield return ClickHost("Sector4");
            yield return ClickPeer("Sector4");
            yield return new WaitForSecondsRealtime(.63f);
            var cageI=SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.Cage,1));
            var cageII=SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.Cage,2));
            yield return ClickHost(cageI.Key,true);
            yield return Wait("Both clients confirm cage rank I",()=>world.Team.TeamRank(SkillLine.Cage)==1 &&
                world.View.TeamRank(SkillLine.Cage)==1 && RemoteTeam()?.TeamRank(SkillLine.Cage)==1);
            yield return Send("reticle_aim",cageII.Key);
            var cageControl=Ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(x=>x.name==cageII.Key && x.IsActive());
            host.LookAtConsole(UiQaProbe.ReticleTarget(cageControl));
            yield return new WaitForSecondsRealtime(.28f);
            int revision=world.Team.teamRevision;
            string pending=QueuePeer("reticle_buy");
            Check("Both Windows players submitted the second cage rank",Ui.QaBuyReticle());
            yield return Wait("Peer action completed",()=>Remote()?.request==pending);
            yield return Wait("Both clients confirm cage rank II",()=>world.View?.TeamRank(SkillLine.Cage)==2 &&
                RemoteTeam()?.TeamRank(SkillLine.Cage)==2);
            before=before.Subtract(cageI.Cost).Subtract(cageII.Cost);
            Check("Concurrent Cage II is charged once and capacity is 16 for both",
                revision+1==world.Team.teamRevision && world.Team.Treasury.Equals(before) &&
                RemoteTeam().Treasury.Equals(before) && RemoteTeam().Capacity(world.progression)==16);
            yield return AimHost(screen);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"06-crew-capacity-720.png"));
            yield return Send("capture","06-crew-capacity-1080.png");

            var args=Environment.GetCommandLineArgs();
            var loaded=TeamSave.Load(args[Array.IndexOf(args,"-raid-save")+1]);
            Check("Saved campaign contains owned private ranks and one shared capacity",
                loaded.TeamRank(SkillLine.Cage)==2 && loaded.Treasury.Equals(before) &&
                loaded.Player(a.key).skills.Contains(cableIII) && loaded.Player(b.key).skills.Contains(oxygen.Id));
            Check("Remote treasury snapshot carries no private profiles",RemoteTeam().players.Count==0);
            host.Teleport(new Vector3(0,1.2f,1));
            yield return new WaitForSecondsRealtime(.45f);
            world.RequestSkill(SkillTreeDefinition.Id(SkillLine.Reel,1),world.Team.teamRevision,a.revision);
            yield return new WaitForSecondsRealtime(.4f);
            Check("A valid rank cannot be purchased away from the physical console",
                !a.skills.Contains(SkillTreeDefinition.Id(SkillLine.Reel,1)) && world.Team.Treasury.Equals(before));
        }
    }
}
#endif
