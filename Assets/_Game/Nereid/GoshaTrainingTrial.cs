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
    // Explicit two-process fixture. Requires a separate team save and profile arguments.
    internal sealed class GoshaTrainingTrial : MonoBehaviour
    {
        [Serializable] sealed class Result
        {
            public string status,phase,error,species,cargoId;
            public List<string> checks=new();
            public int innerCrossings,outerCrossings,closedDoorCrossings;
            public float distance,maxStep,baseRadius,expertRadius,baseCooldown,expertCooldown;
        }
        [Serializable] sealed class Signal {public string reply,hint;}
        string path,peer;RaidWorld world;DiverMotor host,guest;HarpoonTool bag;
        Result result=new();FishActor fish;long cargoId;bool track;Vector3 previous;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] a=Environment.GetCommandLineArgs();
            int run=Array.IndexOf(a,"-raid-gosha-orders"),remote=Array.IndexOf(a,"-raid-gosha-orders-peer");
            if(run<0||run+1>=a.Length || Array.IndexOf(a,"-raid-profile")<0)return;
            var t=new GameObject("Gosha orders trial").AddComponent<GoshaTrainingTrial>();
            t.path=Path.GetFullPath(a[run+1]);Directory.CreateDirectory(t.path);
            if(remote<0 || remote+1>=a.Length)return;
            if(Array.IndexOf(a,"-raid-save")<0)throw new InvalidOperationException("Set a disposable team save");
            t.peer=Path.GetFullPath(a[remote+1]);Directory.CreateDirectory(t.peer);
            t.StartCoroutine(t.Verify());
        }
        string previousSignal;
        void LateUpdate()
        {
            if(peer!=null || UiRoot.Instance?.Context.Player==null || RaidWorld.Instance==null)return;
            var w=RaidWorld.Instance;
            var signal=new Signal {reply=w.LastGoshaOrderReply,hint=w.GoshaTrainingHint(UiRoot.Instance.Context.Player)};
            string json=JsonUtility.ToJson(signal);
            if(previousSignal==json)return;
            previousSignal=json;File.WriteAllText(Path.Combine(path,"signal.json"),json);
        }
        void FixedUpdate()
        {
            if(!track || world==null)return;
            Vector3 position=world.Gosha.Position;
            float step=Vector3.Distance(previous,position);
            result.maxStep=Mathf.Max(step,result.maxStep);result.distance+=step;
            var door=NereidAirlock.Active.transform;
            var old=door.InverseTransformPoint(previous);var at=door.InverseTransformPoint(position);
            if(Mathf.Abs(at.z)<1.6f && at.y>0 && at.y<3.2f)
            {
                if((old.x<0)!=(at.x<0)){result.innerCrossings++;if(world.Airlock.Inner<.97f)result.closedDoorCrossings++;}
                if((old.x<8.65f)!=(at.x<8.65f)){result.outerCrossings++;if(world.Airlock.Outer<.97f)result.closedDoorCrossings++;}
            }
            previous=position;
        }
        IEnumerator Verify()
        {
            result.status="running";
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object current=null;
                try
                {
                    var task=stack.Peek();if(!task.MoveNext()){stack.Pop();continue;}
                    if(task.Current is IEnumerator inner){stack.Push(inner);continue;}
                    current=task.Current;
                }
                catch(Exception e){result.status="failed";result.error=e.ToString();break;}
                yield return current;
            }
            if(result.status=="running")result.status="passed";
            Save();Debug.Log("[GoshaTrainingTrial] "+result.status+" "+result.phase+" "+result.error);
        }
        void Save()=>File.WriteAllText(Path.Combine(path,"gosha-orders.json"),JsonUtility.ToJson(result,true));
        void Check(string message,bool ok)
        {
            if(!ok)throw new InvalidOperationException(message);
            result.checks.Add(message);Save();
        }
        IEnumerator Wait(string message,Func<bool> ready,float seconds=12)
        {
            result.phase=message;Save();float until=Time.realtimeSinceStartup+seconds;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=until)throw new TimeoutException(message+"; "+world?.Gosha.Mode+" at "+world?.Gosha.Position+
                    "; gate "+world?.Airlock.Phase+"; fish "+fish?.transform.position+"; guest "+guest?.transform.position+"; reply "+PeerSignal()?.reply);
                yield return null;
            }
            Check(message,true);
        }
        Signal PeerSignal()
        {
            string file=Path.Combine(peer,"signal.json");
            try{return File.Exists(file)?JsonUtility.FromJson<Signal>(File.ReadAllText(file)):null;}
            catch(IOException){return null;}catch(ArgumentException){return null;}
        }
        void PeerKey(string key)=>PeerCommand("key",key,.12f);
        void PeerCommand(string action,string value="",float amount=0,float pitch=0)
        {
            var command=new UiQaProbe.Request {id=Guid.NewGuid().ToString("N"),action=action,value=value,number=amount,number2=pitch};
            string file=Path.Combine(peer,"request.json"),tmp=file+".tmp";
            File.WriteAllText(tmp,JsonUtility.ToJson(command));
            if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);
        }
        bool EvadesReef()
        {
            // Both swimmers encounter the same live seabed trap. The trained swimmer rejects a clear route towards the jaws.
            for(int i=0;i<12;i++)
            {
                var axis=Quaternion.Euler(0,30*i,0)*Vector3.forward;
                var center=ThreatDirector.ClamBed+Vector3.up*.7f;var from=center-axis*4.85f;
                var direct=new GoshaSwimRoute().Step(from,center+axis,.65f,true,0);
                var safe=new GoshaSwimRoute().Step(from,center+axis,.65f,true,.40f);
                if(Vector3.Dot(direct-from,axis)>.63f && Vector3.Distance(direct,safe)>.2f && GoshaSwimRoute.Risk(safe)<GoshaSwimRoute.Risk(direct))return true;
            }
            return false;
        }
        IEnumerator Run()
        {
            yield return Wait("Two separately authenticated profiles",()=>{
                world=RaidWorld.Instance;
                if(world?.Team==null || !world.IsServerInitialized || DiverMotor.ServerDivers.Count!=2)return false;
                host=DiverMotor.ServerDivers.FirstOrDefault(d=>d.IsOwner);
                guest=DiverMotor.ServerDivers.FirstOrDefault(d=>!d.IsOwner);
                return host!=null&&guest!=null&&!string.IsNullOrEmpty(world.ProgressKey(host))&&!string.IsNullOrEmpty(world.ProgressKey(guest))
                    && world.ProgressKey(host)!=world.ProgressKey(guest) && world.Gosha.Active && PeerSignal()!=null;},140);
            bag=guest.GetComponent<HarpoonTool>();
            Check("Fresh save starts without a tame pet",!world.Team.goshaTamed);
            world.RequestGoshaOrder(GoshaOrder.CatchFish);
            yield return Wait("Untamed pet rejects an order",()=>world.LastGoshaOrderReply?.Contains("подружитесь")==true);
            var startingTreasury=world.Team.Treasury;
            world.PreparePetOrderTrial(host,guest);
            yield return Wait("Remote owner sees their two unlocked keys on the HUD",()=>PeerSignal()?.hint?.Contains("H —")==true);
            world.RequestGoshaOrder(GoshaOrder.CatchFish);
            yield return Wait("Recall alone cannot order fish",()=>world.LastGoshaOrderReply=="Эта команда ещё не изучена");
            Check("Caller has only the recall hint",world.GoshaTrainingHint(host).Contains("Ко мне",StringComparison.OrdinalIgnoreCase) && !world.GoshaTrainingHint(host).Contains("H"));
            var hull=NereidVessel.Active.transform;
            host.Teleport(hull.TransformPoint(new Vector3(2.8f,.52f,2.85f)));
            guest.Teleport(hull.TransformPoint(new Vector3(2.8f,.52f,-12.6f)));
            world.RequestGoshaOrder(GoshaOrder.Come);
            yield return Wait("Recall started coming to its caller",()=>world.Gosha.Mode==GoshaMode.Coming);
            yield return Wait("Recall reached the caller on foot",()=>world.LastGoshaOrderReply=="Гоша рядом");
            world.ResetGosha();
            Check("Depth-transition reset puts Gosha back on board and preserves the shared cooldown",
                world.Gosha.Mode==GoshaMode.Roaming && world.OrderRemaining(host)>0);
            world.Team.Player(world.ProgressKey(host)).skills.Add(SkillTreeDefinition.Id(SkillLine.Fetch,1));
            result.baseRadius=world.OrderRadius(host);result.expertRadius=world.OrderRadius(guest);
            result.baseCooldown=world.OrderCooldown(host);result.expertCooldown=world.OrderCooldown(guest);
            Check("Trained recall and delivery shorten cooldown and trained fetch extends range",result.expertCooldown<result.baseCooldown && result.expertRadius>result.baseRadius);
            PeerKey("H");
            yield return Wait("Other player shares the current order cooldown",()=>PeerSignal()?.reply?.StartsWith("Гоша отдыхает")==true);
            Check("Trained swimmer diverts around the real reef trap",EvadesReef());
            yield return Wait("A free real small fish entered the current habitat",()=>FindObjectsByType<FishActor>().Any(f=>f.IsAvailableForGosha),60);
            yield return Wait("Expert cooldown ended before ordinary cooldown",()=>world.OrderRemaining(guest)<=0 && world.OrderRemaining(host)>0,result.baseCooldown+4);
            PeerKey("H");
            yield return Wait("Independent client orders the same shared Gosha",()=>world.Gosha.Mode==GoshaMode.Fishing && world.Gosha.OrderOwner==guest.OwnerId,8);
            fish=world.TrialReservedFish;cargoId=fish.CargoId;result.cargoId=cargoId.ToString();result.species=fish.SpeciesId;
            Check("One reserved free small fish with a positive cargo identity",cargoId>0 && !fish.RequiresPartner && fish.HookCount==0 && !fish.TryReserveForGosha());
            previous=world.Gosha.Position;track=true;
            world.RequestGoshaOrder(GoshaOrder.CatchFish);
            yield return Wait("Concurrent order rejected with Gosha busy",()=>world.LastGoshaOrderReply=="Гоша занят");
            yield return Wait("Gosha reached the real fish in water",()=>world.Gosha.Mode==GoshaMode.BringingFish,world.tuning.petOrderTimeout);
            Check("Visible reserved fish travels at Gosha's mandibles",Vector3.Distance(fish.transform.position,world.Gosha.Position)<1.3f && fish.CargoId==cargoId);
            var stock=FindObjectsByType<FishActor>().Where(f=>f!=fish && f.CargoId>0).Take(bag.PocketCapacity).ToArray();
            foreach(var item in stock)bag.AcceptGoshaCatch(item);
            Check("All recipient pockets occupied during the return fixture",bag.HasFullCreel);
            yield return Wait("Gosha waited beside the real buyer with a full creel",()=>PeerSignal()?.reply?.Contains("освободите карман")==true,world.tuning.petOrderTimeout);
            host.Teleport(guest.transform.position+new Vector3(-1.65f,.18f,-1.25f));
            host.LookAtConsole((world.Gosha.Position+fish.transform.position)*.5f);
            yield return new WaitForSeconds(.28f);
            UiQaProbe.CapturePeerCamera(Path.Combine(path,"gosha-order-delivery.png"));
            Check("No delivery or duplicate while pockets are full",world.TrialReservedFish==fish && !Enumerable.Range(2,bag.PocketCapacity).Any(i=>bag.CatchIdInSlot(i)==cargoId));
            Check("One existing pocket catch removed to accept the delivery",bag.DropOneCatch());
            yield return Wait("Remote creel received exactly the reserved cargo ID",()=>Enumerable.Range(2,bag.PocketCapacity).Count(i=>bag.CatchIdInSlot(i)==cargoId)==1);
            track=false;
            Check("Crossed both open pressure doors in both directions without teleporting",result.closedDoorCrossings==0&&result.innerCrossings>=2&&result.outerCrossings>=2&&result.distance>20&&result.maxStep<.52f);
            Check("One pet and no cargo granted to the other player",FindObjectsByType<GoshaView>().Length==1&&!Enumerable.Range(2,host.GetComponent<HarpoonTool>().PocketCapacity).Any(i=>host.GetComponent<HarpoonTool>().CatchIdInSlot(i)==cargoId));
            var cargo=world.AttributeCatch(fish.ToCargo(cargoId));
            Check("Normal cargo attribution belongs only to the requesting saved profile",cargo.caughtBy?.Length==1 && cargo.caughtBy[0]==world.ProgressKey(guest));
            Check("Harvest alone awards no processing materials",world.Team.Treasury.Subtract(startingTreasury).IsEmpty);
            Check("The same fish cannot be collected a second time",!fish.CollectForGosha(bag,world.ProgressKey(guest)));
            yield return ProcessDelivery(cargo);
        }

        IEnumerator ProcessDelivery(CatchItem cargo)
        {
            var cage=world.container.position;
            int slot=Enumerable.Range(2,bag.PocketCapacity).Single(i=>bag.CatchIdInSlot(i)==cargoId);
            guest.Teleport(cage+new Vector3(2.14f,-.18f,-.40f));
            yield return new WaitForSeconds(.42f);
            PeerCommand("slot","",slot);
            yield return Wait("Buyer selected its Gosha catch in the normal equipment slot",()=>bag.HeldCargoId==cargoId);
            var euler=Quaternion.LookRotation(cage-guest.transform.position-Vector3.up*.6f).eulerAngles;
            PeerCommand("view","",euler.y,Mathf.DeltaAngle(0,euler.x));
            yield return Wait("Real cage opening is in the buyer's drop reach",()=>CargoDispatch.CanLoad(world,guest));
            PeerCommand("drop_selected");
            yield return Wait("Buyer's drop placed the same ID into the physical cage",()=>bag.CatchIdInSlot(slot)==0 && world.Team.cargo.Any(x=>x.id==cargoId));
            var button=world.container.GetComponentInChildren<CargoDispatch>();
            host.Teleport(cage+new Vector3(3.33f,.19f,1.32f));
            yield return new WaitForSeconds(.28f);
            host.LookAtConsole(button.transform.position);
            yield return Wait("Other diver can operate the real cage dispatch button",()=>CargoDispatch.CanUse(host));
            world.Command(WorldAction.Hoist,0);
            yield return Wait("Hoist delivered that Gosha catch to the tank",()=>world.Team.tank.Any(x=>x.id==cargoId),60);
            Check("The cage and tank have only one copy",!world.Team.cargo.Any(x=>x.id==cargoId) && world.Team.tank.Count(x=>x.id==cargoId)==1);
            var monitor=ShipConsole.Stations.First(s=>s.kind==ShipConsoleKind.Reservoir).screenAnchor;
            host.Teleport(monitor.position-monitor.forward*1.4f-Vector3.up*.45f);host.LookAtConsole(monitor.position);
            yield return Wait("Reservoir selection in range on the processing operator",()=>host.IsInside&&ShipConsole.CanUse(host,WorldAction.Process));
            var treasury=world.Team.Treasury;
            world.Command(WorldAction.Process,cargoId);
            yield return Wait("The Gosha catch is marked for processing",()=>world.Team.tank.Any(x=>x.id==cargoId && x.destination==CatchDestination.Process));
            yield return new WaitForSeconds(.2f);
            world.Command(WorldAction.SendFish,FishRouteQuote.ComputeDraft(world.Team.tank));
            yield return Wait("Physical processing route consumed the exact catch",()=>!world.Team.tank.Any(x=>x.id==cargoId),35);
            var received=world.Team.Treasury.Subtract(treasury);
            Check("Processing paid the complete Gosha catch yield once into the shared NEREID wallet",
                received.biomass==cargo.biomass && received.chitin==cargo.chitin && received.scrap==cargo.scrap && received.electronics==cargo.electronics);
        }
    }
}
#endif
