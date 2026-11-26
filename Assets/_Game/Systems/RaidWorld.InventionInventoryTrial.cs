#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using UnityEngine.UI;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        static bool rackUiFixture;
        [Serializable] sealed class RackReport
        {
            public string status="running",phase,error;
            public int stackLimit,fieldSlot;
            public List<string> checks=new();
            public void Check(bool condition,string description)
            {
                if(!condition)throw new InvalidOperationException(description);
                checks.Add(description);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void BeginRackUiTrial()
        {
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"-raid-rack-ui");
            if(at<0 || at+1>=args.Length)return;
            var folder=Path.GetFullPath(args[at+1]);
            Directory.CreateDirectory(folder);
            var report=new RackReport();
            string path=Path.Combine(folder,"rack-ui-report.json");
            try
            {
                int s=Array.IndexOf(args,"-raid-save"),p=Array.IndexOf(args,"-raid-profile");
                var save=Path.Combine(folder,"rack-ui-campaign.json");
                if(s<0 || s+1>=args.Length || !args.Contains("-raid-host") || p<0 || p+1>=args.Length || !Guid.TryParse(args[p+1],out _) ||
                    !string.Equals(Path.GetFullPath(args[s+1]),save,StringComparison.OrdinalIgnoreCase) || File.Exists(save))
                    throw new InvalidOperationException("Rack UI trial needs a new rack-ui-campaign.json in its own folder, -raid-host and a fixture -raid-profile GUID.");
                rackUiFixture=true;
                await WaitRackUi(()=>Instance && Instance.LocalProgress!=null && Instance.localDiver && Instance.NetworkObject &&
                    Instance.IsServerInitialized && Instance.InventionRackTransform && UiRoot.Instance && UiRoot.Instance.Context.Connected,"private inventory and physical rack",95);
                await Instance.RunRackUiTrial(report,folder);
                report.status="passed";
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();}
            rackUiFixture=false;
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            UnityEngine.Debug.Log("[RackUI] "+report.status+" "+report.phase+" "+report.error);
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
        }

        static async Awaitable WaitRackUi(Func<bool> ready,string step,float timeout=8)
        {
            float until=Time.realtimeSinceStartup+timeout;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=until)
                {
                    var w=Instance;var owner=w?w.localDiver:null;
                    string status=w && owner?$" | focus={Application.isFocused} page={UiRoot.Instance?.Page} modal={UiInputGate.Modal} inside={owner.IsInside} near={w.NearInventionRack(owner)} cameraRay={w.RackFocused(owner,true)} serverRay={w.RackFocused(owner)} panel={w.InventionPanelOpen}":"";
                    if(w && owner && w.TryGetRackParcel(owner.OwnerId,out int id,out var pos,out bool resting))
                        status+=$" parcel={id} resting={resting} delta={pos-owner.transform.position}";
                    throw new TimeoutException(step+status);
                }
                await Awaitable.NextFrameAsync();
            }
        }

        static async Awaitable RackKey(Key key)
        {
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            await Awaitable.WaitForSecondsAsync(.15f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            await Awaitable.WaitForSecondsAsync(.16f);
        }

        async Awaitable RunRackUiTrial(RackReport report,string folder)
        {
            report.phase="ordinary inventory fixture";
            var host=localDiver;
            var gear=host.GetComponent<HarpoonTool>();
            int[] pockets=Enumerable.Range(2,gear.PocketCapacity).ToArray();
            report.Check(team.rigs.Count==0 && team.techPurchases.Count==0 && pockets.Length==3 &&
                pockets.All(i=>gear.InventionInSlot(i).count==0),"Fresh diver has three empty, ordinary equipment pockets.");
            team.goshaMet=team.goshaTamed=true;
            if(threats)threats.enabled=false;
            var trap=TechCatalog.Find("trap");
            foreach(string product in new[]{"trap","magnet","oxygen","robot"})
                team.techPurchases.Add(new TechPurchase{item=product,upgrade="unlock",rank=1});
            team.techPurchases.Add(new TechPurchase{item="trap",upgrade="stack",rank=1});
            Publish();
            await WaitRackUi(()=>view.TechRank("trap","stack")==1,"replicated stack upgrade");
            int limit=view.TechStackLimit("trap");
            report.stackLimit=limit;
            report.Check(limit>1,"The researched trap has an upgraded, multi-item ordinary pocket capacity.");
            const int ballSlot=2,trapSlot=3,otherSlot=4;
            report.fieldSlot=trapSlot+1;
            report.Check(gear.StoreBall() && gear.HasBallInSlot(ballSlot),"The actual ball occupies the first ordinary pocket.");
            gear.SelectSlot(1);
            await WaitRackUi(()=>gear.ActiveSlot==1,"ordinary free-hands slot for the rack camera");
            var rack=InventionRackTransform;
            var wideView=rack.position+rack.forward*2.3f+rack.right*.16f-Vector3.up*.63f;
            host.Teleport(wideView);
            host.LookAtConsole(rack.position-Vector3.up*.12f);
            await WaitRackUi(()=>host.IsInside && UiRoot.Instance.Page==UiPage.None,"clear cabin overview of the vending machine");
            await Awaitable.WaitForSecondsAsync(.35f);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"00-vending-machine.png"));
            host.Teleport(rack.position+rack.forward*1.77f+rack.right*.23f-Vector3.up*.65f);
            host.LookAtConsole(rack.position);
            await WaitRackUi(()=>InventionRackTargeted(host) && UiRoot.Instance.Page==UiPage.None,"owned camera aiming at the real rack");
            report.phase="open rack from physical E";
            await RackKey(Key.E);
            report.Check(InventionPanelOpen && rackBlueprints.Count==4 && rackBlueprints.All(item=>view.TechRank(item.id,"unlock")>0),
                "E at the physical vending machine opened product cards for its four researched items.");
            await RackKey(Key.DownArrow);
            report.Check(rackSelection==1,"Down arrow selected the next product card.");
            await RackKey(Key.UpArrow);
            report.Check(rackBlueprints[rackSelection].id==trap.id,"Selected the trap for physical dispensing.");
            await CaptureRackUi(folder,"01-open-rack.png");
            var balance=team.Treasury;
            var keyboard=Keyboard.current;
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                await WaitRackUi(()=>RackParcelInFlight(trap.id),"visible trap cargo ejected from the chute",7);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                await CaptureRackUi(folder,"03-ejected-trap.png",.11f);
                if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"04-trap-in-flight.png",.10f);
                if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"05-trap-in-flight.png",.09f);
                await WaitRackUi(()=>gear.InventionInSlot(trapSlot).count==1,"first delivery skipped the ball");
            }
            finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
            report.Check(gear.HasBallInSlot(ballSlot) && gear.InventionInSlot(trapSlot).kind=="trap",
                "The caught trap entered the next ordinary pocket and the ball stayed in its own slot.");
            // Leave an earlier pocket empty to distinguish stack-first selection from simply taking the first available slot.
            gear.RemoveBall();
            async Awaitable Order(Func<bool> arrived,string reason)
            {
                await Awaitable.WaitForSecondsAsync(.4f);
                await RackKey(Key.Enter);
                await WaitRackUi(arrived,reason);
            }
            for(int count=2;count<=limit;count++)
            {
                int expected=count;
                await Order(()=>gear.InventionInSlot(trapSlot).count==expected,"top up the existing trap stack to "+expected);
                report.Check(gear.InventionInSlot(ballSlot).count==0,"The earlier empty pocket stayed empty at trap count "+expected+".");
            }
            report.Check(gear.StoreBall() && gear.HasBallInSlot(ballSlot),"Replaced the ball into the free first ordinary pocket.");
            await RackKey(Key.DownArrow);
            string other=rackBlueprints[rackSelection].id;
            report.Check(other!=trap.id,"Selected a different researched item at the physical rack.");
            await Order(()=>gear.InventionInSlot(otherSlot).kind==other && gear.InventionInSlot(otherSlot).count==1,
                "different product placed in last free pocket");
            await RackKey(Key.UpArrow);
            int serialBefore=team.lastRigSerial,revision=LocalProgress.revision;
            bool? fullResponse=null;
            void FullReply(bool ok,string reason){fullResponse=ok;}
            SkillResponded+=FullReply;
            try
            {
                await Awaitable.WaitForSecondsAsync(.4f);
                host.CmdRackTake(trap.id);
                await WaitRackUi(()=>fullResponse.HasValue,"server replies to full ordinary inventory");
            }
            finally{SkillResponded-=FullReply;}
            report.Check(fullResponse==false && RackParcelCount==0 && gear.HasBallInSlot(ballSlot) &&
                gear.InventionInSlot(trapSlot).count==limit && gear.InventionInSlot(otherSlot).kind==other &&
                LocalProgress.revision==revision && team.lastRigSerial==serialBefore,
                "Full inventory refused another trap without ejecting a case, replacing the ball or modifying a stack.");
            gear.RemoveBall();
            await Order(()=>gear.InventionInSlot(ballSlot).kind=="trap" && gear.InventionInSlot(ballSlot).count==1,
                "extra trap starts a second stack in the newly freed ordinary pocket");
            report.Check(balance.Equals(team.Treasury),"All physical rack deliveries were free.");
            await RackKey(Key.E);
            report.Check(!InventionPanelOpen,"E closed the rack.");

            report.phase="ordinary hotbar underwater";
            host.Teleport(CargoLoadingPosition+new Vector3(10,2.5f,4));
            await WaitRackUi(()=>!NearInventionRack(host) && !host.IsInside && UiRoot.Instance.Page==UiPage.None,"diver outside");
            await RackKey(Key.Tab);
            report.Check(!InventionPanelOpen && UiRoot.Instance.Page==UiPage.None && !UiInputGate.Blocked,
                "Tab underwater opened no separate technology inventory.");
            await RackKey(Key.Digit4);
            await WaitRackUi(()=>gear.ActiveSlot==trapSlot,"number 4 selects the full trap stack on the ordinary HUD");
            var bar=GameObject.Find("HUD Equipment");
            bool HudStack(int slot,string kind,int count)
            {
                if(!bar)return false;
                var well=bar.transform.Find("Slot "+(slot+1)+"/Slot well");
                if(!well || !well.gameObject.activeInHierarchy)return false;
                var art=well.Find("Item")?.GetComponent<Image>();
                var label=well.Find("Stack count")?.GetComponent<TMP_Text>();
                return art && art.enabled && art.sprite && art.sprite.name==kind && label && label.enabled &&
                    label.text=="\u00d7"+count;
            }
            await WaitRackUi(()=>HudStack(trapSlot,"trap",limit) && HudStack(ballSlot,"trap",1) && HudStack(otherSlot,other,1),
                "ordinary HUD icons and counts match each of the three equipment pockets");
            report.Check(true,"The ordinary hotbar displays both trap stacks and the other product with their individual icons and counts.");
            await CaptureRackUi(folder,"02-full-inventory.png");
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1,position=mouse.position.ReadValue()});
                await WaitRackUi(()=>view.rigs.Exists(r=>r.kind=="trap" && r.operatorId==host.OwnerId) &&
                    gear.InventionInSlot(trapSlot).count==limit-1,"left click extracts a single trap from the selected ordinary pocket");
            }
            finally{InputSystem.QueueStateEvent(mouse,new MouseState{position=mouse.position.ReadValue()});}
            await Awaitable.WaitForSecondsAsync(.35f);
            await RackKey(Key.R);
            await WaitRackUi(()=>view.rigs.Count==0 && gear.InventionInSlot(trapSlot).count==limit,"R returns the clean rig to its source stack");
            report.Check(gear.InventionInSlot(ballSlot).count==1 && gear.InventionInSlot(otherSlot).kind==other,
                "Repacking restored the selected stack while preserving the other two ordinary pockets.");
            UiRoot.Instance.ShowPage(UiPage.Pause);
            await RackKey(Key.Tab);
            report.Check(!InventionPanelOpen && UiRoot.Instance.Page==UiPage.Pause,"Tab during pause left the dialog in control.");
            UiRoot.Instance.ClosePanel();
            var stored=TeamSave.Load(savePath,progression).Player(ProgressKey(host));
            report.Check(stored?.pocketStacks?.Length==7 && pockets.All(i=>stored.pocketStacks[i-2].count==gear.InventionInSlot(i).count &&
                stored.pocketStacks[i-2].kind==gear.InventionInSlot(i).kind),
                "Disk reload retains the exact two trap stacks and other item in their ordinary pocket positions.");
        }

        async Awaitable CaptureRackUi(string folder,string name,float settle=.25f)
        {
            await Awaitable.WaitForSecondsAsync(settle);
            Canvas.ForceUpdateCanvases();
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,name));
        }
    }
}
#endif
