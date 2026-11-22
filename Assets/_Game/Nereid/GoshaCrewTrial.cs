#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;

namespace Raid.Nereid
{
    // Opt-in fixture on a local host with an explicit, disposable save. Crossings use
    // the real door cycle and the server's CharacterController; peer interaction uses RPCs.
    internal sealed class GoshaCrewTrial : MonoBehaviour
    {
        [Serializable] sealed class Result
        {
            public string status,phase,error;public List<string> checks=new();
        }
        readonly Result result=new();
        RaidWorld world;DiverMotor host,peer;NereidAirlock chamber;
        string folder,remote,save;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args=Environment.GetCommandLineArgs();
            int trial=Array.IndexOf(args,"-raid-gosha-trial"),other=Array.IndexOf(args,"-raid-gosha-peer"),team=Array.IndexOf(args,"-raid-save");
            if(trial<0||trial+1>=args.Length||other<0||other+1>=args.Length||team<0||team+1>=args.Length)return;
            var driver=new GameObject("Gosha LAN trial").AddComponent<GoshaCrewTrial>();
            driver.folder=Path.GetFullPath(args[trial+1]);driver.remote=Path.GetFullPath(args[other+1]);driver.save=Path.GetFullPath(args[team+1]);
            Directory.CreateDirectory(driver.folder);Directory.CreateDirectory(driver.remote);
            driver.StartCoroutine(driver.Verify());
        }
        IEnumerator Verify()
        {
            result.status="running";var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object next=null;
                try
                {
                    var task=stack.Peek();
                    if(!task.MoveNext()){stack.Pop();continue;}
                    if(task.Current is IEnumerator child){stack.Push(child);continue;}
                    next=task.Current;
                }
                catch(Exception e){result.status="failed";result.error=e.ToString();break;}
                yield return next;
            }
            if(result.status=="running")result.status="passed";
            Write();Debug.Log("[GoshaCrewTrial] "+result.status+" "+result.phase+" "+result.error);
        }
        void Write()=>File.WriteAllText(Path.Combine(folder,"gosha-crew.json"),JsonUtility.ToJson(result,true));
        void Check(string label,bool pass)
        {if(!pass)throw new InvalidOperationException(label);result.checks.Add(label);Write();}
        IEnumerator Wait(string label,Func<bool> ready,float timeout=15)
        {
            result.phase=label;Write();float until=Time.realtimeSinceStartup+timeout;
            while(!ready())
            {if(Time.realtimeSinceStartup>until)throw new TimeoutException(label+"; pet "+world?.Gosha.Mode+" at "+world?.Gosha.Position+" ball "+world?.Gosha.Ball+" at "+world?.Gosha.BallPosition);yield return null;}
            Check(label,true);
        }
        void Stage(DiverMotor who,Vector3 position)
        {who.Teleport(position);Physics.SyncTransforms();}
        IEnumerator MoveThroughDoor(Vector3 target)
        {
            float end=Time.realtimeSinceStartup+12;
            while((host.transform.position-target).magnitude>.38f)
            {
                if(Time.realtimeSinceStartup>end)throw new TimeoutException("Controller stalled crossing open door "+host.transform.position+" to "+target);
                host.ApplyExternalMotion(Vector3.ClampMagnitude(target-host.transform.position,5.4f*Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
        }
        IEnumerator PressPad(int index)
        {
            var pad=chamber.GetPad(index);
            var facing=index==1?Vector3.right:Vector3.left;
            Stage(host,pad.transform.position+facing*1.25f-Vector3.up*.6f);
            host.LookAtConsole(pad.transform.position);
            yield return new WaitForSeconds(.30f);
            var direction=(pad.transform.position-host.transform.position-Vector3.up*.6f).normalized;
            world.RequestAirlockPad((byte)index,direction);
            yield return Wait("Pressure pad "+index+" starts its cycle",()=>world.Airlock.Busy);
        }
        UiQaProbe.Snapshot Remote()
        {
            try{var path=Path.Combine(remote,"state.json");return File.Exists(path)?JsonUtility.FromJson<UiQaProbe.Snapshot>(File.ReadAllText(path)):null;}
            catch(IOException){return null;}catch(ArgumentException){return null;}
        }
        void Request(string action,string value="",float yaw=0,float pitch=0)
        {
            var r=new UiQaProbe.Request{id=Guid.NewGuid().ToString("N"),action=action,value=value,number=yaw,number2=pitch};
            string path=Path.Combine(remote,"request.json"),tmp=path+"."+r.id;
            File.WriteAllText(tmp,JsonUtility.ToJson(r));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
        }
        IEnumerator PeerShot(string leaf,Vector3 target)
        {
            var angles=Quaternion.LookRotation(target-peer.transform.position-Vector3.up*.6f).eulerAngles;
            Request("view","",angles.y,Mathf.DeltaAngle(0,angles.x));yield return new WaitForSeconds(.45f);
            Request("capture",leaf);yield return Wait("Remote camera captured "+leaf,()=>File.Exists(Path.Combine(remote,leaf)),12);
        }
        void HostShot(string leaf)=>UiQaProbe.CapturePeerCamera(Path.Combine(folder,leaf));
        IEnumerator Run()
        {
            yield return Wait("Distinct host and Windows peer ready",()=>RaidWorld.Instance!=null&&RaidWorld.Instance.NetworkObject!=null&&RaidWorld.Instance.IsServerInitialized&&
                DiverMotor.ServerDivers.Count==2&&Remote()?.players==2,150);
            world=RaidWorld.Instance;host=DiverMotor.ServerDivers.Single(d=>d.IsOwner);peer=DiverMotor.ServerDivers.Single(d=>!d.IsOwner);
            chamber=NereidAirlock.Active;
            yield return Wait("Host world and local camera ready",()=>world.Gosha.Active&&UiRoot.Instance?.Context.Player!=null);
            if(world.Team.goshaTamed)
            {
                Check("Loaded team save retains pet",TeamSave.Load(save).goshaTamed);
                yield return Wait("Exactly one model and deck pet replicated to returning crew",()=>FindObjectsByType<GoshaView>().Length==1 && world.Gosha.Mode!=GoshaMode.Waiting && Remote()?.gosha.Active==true && Remote()?.gosha.Mode==world.Gosha.Mode);
                yield break;
            }
            yield return PressPad(1);
            yield return Wait("Outer gate open",()=>world.Airlock.Phase==AirlockPhase.OuterOpen,30);
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(10.5f,1,0)));
            yield return Wait("Gosha rides the first expedition diver",()=>world.Gosha.Mode==GoshaMode.Riding && world.Gosha.Rider==host.OwnerId,24);
            Stage(peer,host.transform.position+new Vector3(1.5f,.1f,-1.65f));
            yield return PeerShot("gosha-shoulder.png",world.Gosha.Position);
            Stage(peer,NereidVessel.Active.transform.TransformPoint(new Vector3(4,.55f,-14)));
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(7,1,0)));
            Check("Shoulder occupied in flooded pressure lock",chamber.Contains(host.transform.position)&&world.Gosha.Mode==GoshaMode.Riding);
            yield return PressPad(2);
            yield return Wait("Inner gate open after draining",()=>world.Airlock.Phase==AirlockPhase.InnerOpen,30);
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(-1.25f,1,0)));
            yield return Wait("Pet returned through inner door and persisted to checksum save",()=>world.Team.goshaTamed&&TeamSave.Load(save).goshaTamed);
            Stage(peer,world.Gosha.Position+Vector3.back*1.2f+Vector3.up*.65f);
            Stage(host,world.Gosha.Position+new Vector3(-1.4f,.7f,-1.3f));
            host.LookAtConsole(world.Gosha.Position);yield return new WaitForSeconds(.2f);
            HostShot("gosha-deck.png");
            Request("gosha_pet");
            yield return Wait("Remote owner's server-validated petting gesture",()=>world.Gosha.Pats>0 && world.Gosha.Mode==GoshaMode.Petted);
            host.LookAtConsole(world.Gosha.Position);HostShot("gosha-pet.png");
            yield return Wait("Pet reaction replicated on second process",()=>Remote()?.gosha.Pats>0);
            var ball=world.Gosha.BallPosition;
            Stage(host,ball+Vector3.back*.92f+Vector3.up*.75f);
            host.LookAtConsole(ball);yield return new WaitForSeconds(.3f);
            world.RequestGosha(GoshaAction.PickUp);
            yield return Wait("Separate ball slot is held by host",()=>world.HasGoshaBall(host));
            host.LookAtConsole(host.transform.position+Vector3.forward*2-Vector3.up);
            yield return new WaitForSeconds(.3f);
            world.RequestGosha(GoshaAction.Throw);
            yield return Wait("Thrown rubber ball in physical flight",()=>world.Gosha.Ball==GoshaBallMode.Flying);
            yield return Wait("Pet fetches the physical ball",()=>world.Gosha.Ball==GoshaBallMode.Carried,65);
            host.LookAtConsole(world.Gosha.Position);yield return new WaitForSeconds(.1f);HostShot("gosha-fetch.png");
            yield return Wait("Returned to the diver who threw it",()=>world.HasGoshaBall(host),35);
            Stage(peer,host.transform.position+Vector3.right*3);
            host.LookAtConsole(peer.transform.position);
            yield return new WaitForSeconds(.35f);
            float hp=peer.Health,oxygen=peer.Oxygen;
            int pockets=peer.GetComponent<HarpoonTool>().CatchCount;
            world.RequestGosha(GoshaAction.Throw);
            yield return Wait("Direct server physics hit transfers ball to the other player",()=>world.HasGoshaBall(peer));
            host.LookAtConsole(peer.transform.position);
            yield return Wait("Pet catches and briefly knocks down that specific diver",()=>world.GoshaKnocked(peer),20);
            yield return new WaitForSeconds(.22f);HostShot("gosha-robbery.png");
            Check("Pet is escaping with the single ball",world.Gosha.Mode==GoshaMode.Escaping&&world.Gosha.Ball==GoshaBallMode.Carried);
            yield return Wait("Movement lock released",()=>!world.GoshaKnocked(peer),3);
            Check("Player survived with unchanged health and cargo, oxygen intact",!peer.IsDead && hp==peer.Health && peer.Oxygen>=oxygen-.1f && pockets==peer.GetComponent<HarpoonTool>().CatchCount);
            yield return Wait("Pet drops ball in a safe deck area",()=>world.Gosha.Ball==GoshaBallMode.Deck,15);
        }
    }
}
#endif
