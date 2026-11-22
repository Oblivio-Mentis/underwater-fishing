#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Raid.UI
{
    internal sealed class Raid015SkillTrial : MonoBehaviour
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
            var trial=new GameObject("RAID015 local skills validation").AddComponent<Raid015SkillTrial>();
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

        IEnumerator Send(string action,string argument = "",float number=0)
        {
            string id=Guid.NewGuid().ToString("N");
            var request=new UiQaProbe.Request{id=id,action=action,name=argument,value=argument,number=number};
            string temp=Path.Combine(peerDirectory,"request.tmp");
            File.WriteAllText(temp,JsonUtility.ToJson(request));
            string requestPath=Path.Combine(peerDirectory,"request.json");
            if(File.Exists(requestPath))File.Delete(requestPath);
            File.Move(temp,requestPath);
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
            yield return AimHost(button.transform);
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
            a.wallet=b.wallet=new MaterialCost(64,64,64,64);
            a.revision++;b.revision++;
            world.RefreshCrewProfilesForTrial();
            station=ShipConsole.Stations.Single(s=>s.kind==ShipConsoleKind.SkillTree);
            Transform screen=station.screenAnchor;
            host.Teleport(screen.position-screen.forward*1.69f-Vector3.up*.38f);
            peer.Teleport(screen.position-screen.forward*1.32f-screen.right*.91f-Vector3.up*.39f);
            yield return Wait("Both players near skill monitor",()=>host.IsInside && peer.IsInside &&
                station.InRange(host) && station.InRange(peer),10);
            yield return new WaitForSecondsRealtime(.5f);

            yield return AimHost(screen);
            Check("LMB opens physical graph on host",Ui.QaClickReticle() && Ui.Page==UiPage.SkillTree);
            yield return Send("reticle_aim",station.name);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Send("reticle_click");
            yield return Wait("Both clients see all 36 purchasable nodes",()=>Remote()?.page=="SkillTree" &&
                Remote()?.skillTreeNodes==36 && Ui.GetComponentsInChildren<SkillPurchaseButton>().Length==36);
            Check("Monitor reserves normal gameplay",UiInputGate.Blocked && UiInputGate.SkillMonitorLook && Remote()?.inputBlocked==true);
            yield return Wait("Server confirms both real eye rays",()=>ShipConsole.CanUseKind(host,ShipConsoleKind.SkillTree) &&
                ShipConsole.CanUseKind(peer,ShipConsoleKind.SkillTree));
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"skill-host-720.png"));
            yield return Send("capture","skill-peer-1080.png");

            var aStart=a.wallet;
            var bStart=b.wallet;
            yield return ClickHost("H1");
            Check("LMB only selects H1",a.wallet.biomass==aStart.biomass && !a.skills.Contains(SkillId.H1));
            yield return ClickHost("H1",true);
            yield return Wait("RMB buys H1 for host",()=>a.skills.Contains(SkillId.H1) && world.LocalProgress.skills.Contains(SkillId.H1));
            yield return ClickPeer("S1",true);
            yield return Wait("RMB buys S1 for peer",()=>b.skills.Contains(SkillId.S1) && Remote()?.localProgress?.skills.Contains(SkillId.S1)==true);
            Check("Opposite private branches and separate balances",!a.skills.Contains(SkillId.S1) && !b.skills.Contains(SkillId.H1)
                && a.wallet.biomass==aStart.biomass-world.progression.Skill(SkillId.H1).Cost.biomass
                && b.wallet.biomass==bStart.biomass-world.progression.Skill(SkillId.S1).Cost.biomass);

            int capacity=world.Team.Capacity(world.progression);
            int treasury=world.Team.biomass, buyer=a.wallet.biomass, other=b.wallet.biomass;
            yield return ClickHost("K1",true);
            yield return Wait("K1 updates common capacity",()=>world.Team.teamSkills.Contains(SkillId.K1)
                && world.Team.Capacity(world.progression)==capacity+world.progression.Skill(SkillId.K1).Magnitude);
            yield return ClickPeer("K1",true);
            Check("K1 charged exactly one personal wallet and preserved treasury",
                a.wallet.biomass==buyer-world.progression.Skill(SkillId.K1).Cost.biomass && b.wallet.biomass==other && world.Team.biomass==treasury);

            yield return ClickHost("Treasury");
            for(int i=0;i<5;i++)yield return ClickHost("IncreaseB");
            buyer=a.wallet.biomass;
            yield return ClickHost("DepositResources");
            yield return Wait("Five biomass transferred",()=>a.wallet.biomass==buyer-5 && world.Team.biomass==treasury+5);
        }
    }
}
#endif
