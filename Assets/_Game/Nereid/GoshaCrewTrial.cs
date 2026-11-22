#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
            yield return new WaitForSeconds(.32f);
            var direction=(pad.transform.position-host.transform.position-Vector3.up*.6f).normalized;
            Check("Reach and server sightline at pad "+index,chamber.ValidatePress(host,(byte)index,direction,out _));
            Check("Requested direction accepted at pad "+index,world.Airlock.CanRequest(pad.Sea));
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
            yield return Wait("Observer reached framing mark for "+leaf,()=>{
                var r=Remote();return r!=null&&Vector3.Distance(peer.transform.position,new Vector3(r.x,r.y,r.z))<.24f;},6);
            var angles=Quaternion.LookRotation(target-peer.transform.position-Vector3.up*.6f).eulerAngles;
            Request("view","",angles.y,Mathf.DeltaAngle(0,angles.x));
            yield return Wait("Observer aimed at "+leaf,()=>{
                var r=Remote();return r!=null && Mathf.Abs(Mathf.DeltaAngle(r.aimYaw,angles.y))<3 && Mathf.Abs(Mathf.DeltaAngle(r.aimPitch,angles.x))<3;},6);
            Request("capture",leaf);yield return Wait("Remote camera captured "+leaf,()=>File.Exists(Path.Combine(remote,leaf)),12);
        }
        static long StockTrialPocket(HarpoonTool inventory)
        {
            // The fishing acceptance run verifies obtaining this cargo. Here the fixture
            // starts the tackle with an occupied pocket so losing a real ID is observable.
            var fish=FindObjectsByType<FishActor>().First(f=>f.IsServerInitialized && !f.RequiresPartner);
            if(inventory.CatchIdInSlot(2)>0)return inventory.CatchIdInSlot(2);
            long id=DateTime.UtcNow.Ticks;
            inventory.StagePocketForTrial(fish,id);
            return id;
        }
        void HostShot(string leaf)=>UiQaProbe.CapturePeerCamera(Path.Combine(folder,leaf));
        static IEnumerator PressSlotKey(Key key)
        {
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }

        IEnumerator ThrowWithPointer()
        {
            UiRoot.Instance.HandleBallPointer(true,false);
            yield return new WaitForSeconds(.52f);
            UiRoot.Instance.HandleBallPointer(false,true);
        }
        IEnumerator RecoverStairBall(Vector3 local,string label)
        {
            var hull=NereidVessel.Active.transform;
            var at=hull.TransformPoint(local);
            var observer=local.y>1?new Vector3(-4.7f,2.02f,1):new Vector3(-2.8f,.52f,local.z+(local.z>0?-.45f:.45f));
            Stage(peer,hull.TransformPoint(observer));
            world.QaStageLooseThrow(host,at+Vector3.up*.23f,Vector3.down*.22f);
            yield return Wait(label+" ball settles",()=>world.Gosha.Ball==GoshaBallMode.Deck,6);
            var rest=world.Gosha.BallPosition;
            var leaf=local.y>1?"gallery":local.z>0?"stairs-forward":"stairs-aft";
            yield return PeerShot(leaf+"-ball.png",rest);
            yield return Wait(label+" picked up at the real stair",()=>world.Gosha.Ball==GoshaBallMode.Carried,35);
            Request("capture",leaf+"-fetch.png");
            Check(label+" reachable without relocation",!world.QaBallRelocated && Vector3.Distance(rest,world.Gosha.Position)<1.25f);
            yield return Wait(label+" returned on foot",()=>world.HasGoshaBall(host),35);
        }
        IEnumerator WatchFirstMeeting()
        {
            var camera=host.GetComponentInChildren<Camera>();
            var view=FindAnyObjectByType<GoshaView>();
            var left=chamber.transform.right;
            host.LookAtConsole(camera.transform.position+left*10);
            Check("First exit has a pause before the distant reveal",world.Gosha.Mode==GoshaMode.Waiting && !view.PetVisible);
            yield return Wait("Gosha first appears in open water",()=>world.Gosha.Mode==GoshaMode.Swimming,8);
            Vector3 previous=world.Gosha.Position, radial=Vector3.ProjectOnPlane(previous-host.transform.position,Vector3.up);
            Check("First visible position is at least 12 m from the diver",radial.magnitude>=12);
            float sweep=0,nearest=float.PositiveInfinity,maxSpeed=0,lastTime=Time.fixedTime;
            bool captured=false,wasCircling=false,swamDuringLanding=false;
            float until=Time.realtimeSinceStartup+22;
            while(world.Gosha.Mode==GoshaMode.Swimming)
            {
                if(Time.realtimeSinceStartup>until)throw new TimeoutException("Gosha could not finish the swim: "+world.Gosha.Position);
                var at=world.Gosha.Position;
                var next=Vector3.ProjectOnPlane(at-world.QaMeetingCenter(host),Vector3.up);
                bool circling=world.QaMeetingCircles;
                if(wasCircling && circling)sweep+=Vector3.SignedAngle(radial,next,Vector3.up);
                float stepTime=Time.fixedTime-lastTime;
                if(stepTime>0)maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(at,previous)/stepTime);
                if(world.QaMeetingPerches)
                {
                    host.ApplyExternalMotion(left*3.6f*Time.fixedDeltaTime);
                    swamDuringLanding=true;
                }
                if(view.PetVisible)
                {
                    nearest=Mathf.Min(nearest,Vector3.Distance(view.PetCenter,camera.transform.position));
                    var screen=camera.WorldToViewportPoint(view.PetCenter);
                    if(!captured && screen.z>3 && screen.z<8 && screen.x>.14f && screen.x<.86f && screen.y>.2f && screen.y<.8f)
                    {HostShot("gosha-first-approach.png");captured=true;}
                }
                previous=at;radial=next;wasCircling=circling;lastTime=Time.fixedTime;
                yield return new WaitForFixedUpdate();
            }
            Check($"Underwater arc covered {Mathf.Abs(sweep):F0} degrees at speeds up to {maxSpeed:F2} m/s",
                Mathf.Abs(sweep)>210 && maxSpeed<12);
            Check("Soft landing caught up with a moving diver",swamDuringLanding);
            Check($"Gosha came into view, nearest visible distance {nearest:F2} m",captured && nearest>1.35f);
            Check("Gosha reached the intended diver's shoulder",world.Gosha.Mode==GoshaMode.Riding && world.Gosha.Rider==host.OwnerId);
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                host.LookAtConsole(camera.transform.position+Quaternion.AngleAxis(angle,Vector3.up)*left*8+Vector3.down*3);
                yield return new WaitForSeconds(.15f);
                Check("Rider camera stays clear while looking around at "+angle+" degrees",!view.PetVisible);
            }
            HostShot("gosha-rider-first-person.png");
        }

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
            yield return PressPad(2);
            yield return Wait("Outer gate open",()=>world.Airlock.Phase==AirlockPhase.OuterOpen,30);
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(10.5f,1,0)));
            yield return WatchFirstMeeting();
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(14.5f,2.1f,-1.5f)));
            Stage(peer,host.transform.position+host.transform.right*2.3f-host.transform.forward*1.85f+Vector3.up*.25f);
            yield return Wait("Remote view renders the pet on the other diver's shoulder",
                ()=>Remote()?.goshaModelVisible==true && Remote()?.gosha.Mode==GoshaMode.Riding);
            yield return PeerShot("gosha-shoulder.png",world.Gosha.Position);
            Stage(peer,NereidVessel.Active.transform.TransformPoint(new Vector3(4,.55f,-14)));
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(7,1,0)));
            Check("Shoulder occupied in flooded pressure lock",chamber.Contains(host.transform.position)&&world.Gosha.Mode==GoshaMode.Riding);
            yield return PressPad(1);
            yield return Wait("Inner gate open after draining",()=>world.Airlock.Phase==AirlockPhase.InnerOpen,30);
            yield return MoveThroughDoor(chamber.transform.TransformPoint(new Vector3(-1.25f,1,0)));
            yield return Wait("Pet returned through inner door and persisted to checksum save",()=>world.Team.goshaTamed&&TeamSave.Load(save).goshaTamed);
            yield return Wait("Dismounted onto the interior deck",()=>Mathf.Abs(world.Gosha.Position.y-NereidVessel.Active.transform.position.y)<.4f);
            Stage(peer,world.Gosha.Position+Vector3.back*1.2f+Vector3.up*.65f);
            Stage(host,world.Gosha.Position+new Vector3(-1.4f,.7f,-1.3f));
            host.LookAtConsole(world.Gosha.Position);yield return new WaitForSeconds(.2f);
            HostShot("gosha-deck.png");
            Stage(peer,world.Gosha.Position+Vector3.back*.95f+Vector3.up*.7f);
            yield return PeerShot("gosha-deck-1080.png",world.Gosha.Position);
            Stage(peer,world.Gosha.Position+Vector3.back*.95f+Vector3.up*.7f);
            Request("gosha_pet");
            yield return Wait("Remote owner's server-validated petting gesture",()=>world.Gosha.Pats>0 && world.Gosha.Mode==GoshaMode.Cradled && world.Gosha.Rider==peer.OwnerId);
            Stage(host,world.Gosha.Position+new Vector3(1.30f,.73f,-.65f));
            host.LookAtConsole(world.Gosha.Position);yield return new WaitForSeconds(.1f);HostShot("gosha-pet.png");
            Request("capture","gosha-pet-peer.png");
            yield return Wait("Petting captured from the stroking diver's camera",()=>File.Exists(Path.Combine(remote,"gosha-pet-peer.png")));
            yield return Wait("Diver sees Gosha in their arms",()=>Remote()?.gosha.Mode==GoshaMode.Cradled && Remote()?.gosha.Rider==peer.OwnerId);
            Check("Single carrier owns the pat",world.IsHoldingGosha(peer)&&!world.IsHoldingGosha(host));
            yield return new WaitForSeconds(world.tuning.patCooldown+.1f);
            Request("gosha_pet");
            yield return Wait("Second E sets Gosha on the deck and frees both hands",()=>!world.IsHoldingGosha(peer));
            var ball=world.Gosha.BallPosition;
            Stage(host,ball+Vector3.back*.92f+Vector3.up*.75f);
            host.LookAtConsole(ball);yield return new WaitForSeconds(.3f);
            world.RequestGosha(GoshaAction.PickUp);
            var hostInventory=host.GetComponent<HarpoonTool>();
            yield return Wait("Ball occupies and selects ordinary slot 3",()=>world.HasGoshaBall(host) &&
                hostInventory.ActiveSlot==2 && hostInventory.HasBallInSlot(2));
            Check("Ball uses five-slot hotbar and adds no catch",hostInventory.SlotCount==5 && hostInventory.CatchCount==0);
            var camera=host.GetComponentInChildren<Camera>().transform;
            UiRoot.Instance.HandleBallPointer(true,false);
            yield return Wait("LMB begins a wind-up with slot 3 selected",()=>world.Gosha.BallWind>0);
            Check("The wound-up ball owns the mouse pointer",UiInputGate.PointerReserved);
            yield return PressSlotKey(Key.Digit1);
            yield return Wait("Stowed ball leaves the harpoon usable and clears the wind-up",()=>hostInventory.HarpoonInHands &&
                world.HasGoshaBall(host) && world.Gosha.BallWind==0 && camera.Find("Ball in diving gloves")!=null &&
                !camera.Find("Ball in diving gloves").gameObject.activeSelf);
            UiRoot.Instance.HandleBallPointer(false,true);
            Check("Releasing LMB after stowing did not throw the ball",world.HasGoshaBall(host));
            yield return PressSlotKey(Key.Digit3);
            yield return Wait("Equipping the ordinary slot brings the ball back into both gloves",()=>world.GoshaBallInHands(host) &&
                camera.Find("Ball in diving gloves").gameObject.activeSelf);
            yield return GoshaGripTrial.Verify(world,host,Check,HostShot);
            host.LookAtConsole(host.transform.position+Vector3.forward*2-Vector3.up);
            yield return new WaitForSeconds(.3f);
            yield return ThrowWithPointer();
            yield return Wait("Thrown rubber ball in physical flight",()=>world.Gosha.Ball==GoshaBallMode.Flying);
            yield return Wait("Pet fetches the physical ball",()=>world.Gosha.Ball==GoshaBallMode.Carried,65);
            yield return Wait("Pet approaches the thrower carrying the ball",()=>world.Gosha.Ball==GoshaBallMode.Carried && Vector3.Distance(host.transform.position,world.Gosha.Position)<2.45f);
            host.LookAtConsole(world.Gosha.Position);yield return new WaitForSeconds(.08f);HostShot("gosha-fetch.png");
            yield return Wait("Returned to the diver who threw it",()=>world.HasGoshaBall(host),35);
            var hull=NereidVessel.Active.transform;
            yield return RecoverStairBall(new Vector3(-3.98f,.09f,5.88f),"Forward left stair cheek");
            yield return RecoverStairBall(new Vector3(-3.98f,.09f,-7.39f),"Aft left stair cheek");
            yield return RecoverStairBall(new Vector3(-4.7f,1.58f,-.7f),"Raised observation deck");
            var sphere=FindAnyObjectByType<GoshaBallBody>();
            Stage(host,hull.TransformPoint(new Vector3(0,.52f,3.8f)));
            host.LookAtConsole(hull.TransformPoint(new Vector3(0,.95f,6.45f)));
            yield return new WaitForSeconds(.3f);
            var spits=world.QaMouthReturns;
            world.QaStageLooseThrow(host,hull.TransformPoint(new Vector3(0,.85f,6.1f)),hull.forward*8);
            yield return Wait("Real physical ball reaches the creature and is spat out",()=>world.QaMouthReturns>spits);
            Check("Beast sends the existing sphere flying back in front of its lips",world.Gosha.Ball==GoshaBallMode.Flying && hull.InverseTransformPoint(world.Gosha.BallPosition).z<6.73f);
            HostShot("mouth-returns-ball.png");
            yield return Wait("Same ball recovered after entering the mouth",()=>world.HasGoshaBall(host),30);
            world.QaStageLooseThrow(host,hull.TransformPoint(new Vector3(0,.8f,-3)),Vector3.zero);
            yield return Wait("Ball inside sealed reservoir machinery lands back in a reachable aisle",()=>world.QaBallRelocated,8);
            yield return Wait("Gosha returns the recovered reservoir ball to its thrower",()=>world.HasGoshaBall(host),35);
            Check("Same single physical sphere after stairs, mouth and equipment recovery",FindAnyObjectByType<GoshaBallBody>()==sphere && FindObjectsByType<GoshaBallBody>().Length==1);
            Stage(peer,host.transform.position+Vector3.right*3);
            host.LookAtConsole(peer.transform.position);
            yield return new WaitForSeconds(.35f);
            float hp=peer.Health,oxygen=peer.Oxygen;
            var inventory=peer.GetComponent<HarpoonTool>();
            long pocketId=StockTrialPocket(inventory);
            int pockets=inventory.CatchCount;
            Check("Occupied fish pocket before the tackle (fixture)",pockets>0 && inventory.CatchIdInSlot(2)==pocketId);
            yield return ThrowWithPointer();
            yield return Wait("Direct server physics hit transfers ball to the other player",()=>world.HasGoshaBall(peer));
            Check("Catch stayed in slot 3 and shared ball entered the recipient's next normal pocket",
                inventory.CatchIdInSlot(2)==pocketId && inventory.HasBallInSlot(3) && !hostInventory.HasBallInSlot(2));
            host.LookAtConsole(peer.transform.position);
            yield return Wait("Pet catches and briefly knocks down that specific diver",()=>world.GoshaKnocked(peer),20);
            yield return new WaitForSeconds(.22f);HostShot("gosha-robbery.png");
            Check("Pet is escaping with the single ball",world.Gosha.Mode==GoshaMode.Escaping&&world.Gosha.Ball==GoshaBallMode.Carried);
            yield return Wait("Movement lock released",()=>!world.GoshaKnocked(peer),3);
            Check("Player survived with unchanged health and cargo, oxygen intact",!peer.IsDead && hp==peer.Health && peer.Oxygen>=oxygen-.1f && pockets==inventory.CatchCount && inventory.CatchIdInSlot(2)==pocketId);
            yield return Wait("Pet drops ball in a safe deck area",()=>world.Gosha.Ball==GoshaBallMode.Deck,15);
            Check("Tackle cleared the recipient's ball pocket",!inventory.HasBallInSlot(3));
        }
    }
}
#endif
