#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Raid
{
    public sealed partial class RaidWorld
    {
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
                await WaitRackUi(()=>Instance && Instance.LocalProgress?.techSlots?.Length==4 && Instance.localDiver &&
                    Instance.IsServerInitialized && Instance.InventionRackTransform && UiRoot.Instance && UiRoot.Instance.Context.Connected,"private inventory and physical rack",95);
                await Instance.RunRackUiTrial(report,folder);
                report.status="passed";
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();}
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            UnityEngine.Debug.Log("[RackUI] "+report.status+" "+report.phase+" "+report.error);
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
        }

        static async Awaitable WaitRackUi(Func<bool> ready,string step,float timeout=8)
        {
            float until=Time.realtimeSinceStartup+timeout;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=until)throw new TimeoutException(step);
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
            report.phase="fixture";
            report.Check(team.rigs.Count==0 && team.techPurchases.Count==0 && LocalProgress.techSlots.All(s=>s.count==0),"Fresh campaign exposes four empty private equipment slots.");
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
            report.Check(limit>1,"The researched trap has an upgraded, multi-item slot capacity.");
            var host=localDiver;
            var rack=InventionRackTransform;
            var wideView=rack.position+rack.forward*2.3f+rack.right*.16f-Vector3.up*.63f;
            host.Teleport(wideView);
            host.LookAtConsole(rack.position-Vector3.up*.12f);
            await WaitRackUi(()=>host.IsInside && UiRoot.Instance.Page==UiPage.None,"clear cabin overview of the vending machine");
            await Awaitable.WaitForSecondsAsync(.35f);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"00-vending-machine.png"));
            host.Teleport(rack.position+rack.forward*1.77f+rack.right*.23f-Vector3.up*.65f);
            host.LookAtConsole(rack.position);
            await WaitRackUi(()=>InventionRackTargeted(host) && InventoryContextAllowed(),"owned camera aiming at the real rack");
            report.phase="open rack from physical E";
            await RackKey(Key.E);
            report.Check(inventoryPanel==InventoryPanel.Rack && rackBlueprints.Count==4 && rackBlueprints.All(item=>view.TechRank(item.id,"unlock")>0),
                "E at the physical vending machine opened product cards for its four researched items.");
            await RackKey(Key.DownArrow);
            report.Check(rackSelection==1,"Down arrow selected the next product card.");
            await RackKey(Key.UpArrow);
            report.Check(rackBlueprints[rackSelection].id==trap.id,"Selected the trap for physical dispensing.");
            await RackKey(Key.RightArrow);
            int slot=inventorySlot;
            report.fieldSlot=slot+1;
            report.Check(slot==1,"Right arrow selected the second real slot.");
            await CaptureRackUi(folder,"01-open-rack.png");
            var balance=team.Treasury;
            var keyboard=Keyboard.current;
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                await WaitRackUi(()=>RackParcelInFlight(trap.id),"visible trap cargo ejected from the chute",7);
                await CaptureRackUi(folder,"03-ejected-trap.png",.11f);
                if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"04-trap-in-flight.png",.10f);
                if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"05-trap-in-flight.png",.09f);
                report.Check(RackParcelCount>0 || LocalProgress.techSlots[slot].count>0,"The actual server-issued trap was observed leaving the vending chute.");
                await WaitRackUi(()=>LocalProgress.techSlots[slot].count==limit,"hold Enter to fill selected slot",12);
                await Awaitable.WaitForSecondsAsync(.4f);
            }
            finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());}
            report.Check(LocalProgress.techSlots[slot].kind=="trap" && LocalProgress.techSlots[slot].count==limit && balance.Equals(team.Treasury) &&
                LocalProgress.techSlots.Where((_,i)=>i!=slot).All(x=>x.count==0),"Free rack transfers filled exactly one selected slot and stopped at capacity.");
            await RackKey(Key.E);
            report.Check(!InventionPanelOpen,"E closed the rack.");

            report.phase="inventory underwater";
            host.Teleport(CargoLoadingPosition+new Vector3(10,2.5f,4));
            await WaitRackUi(()=>!NearInventionRack(host) && !host.IsInside && InventoryContextAllowed(),"diver outside with no rack focus");
            await RackKey(Key.Tab);
            report.Check(inventoryPanel==InventoryPanel.Backpack && inventorySlot==slot,"Tab underwater opened the separate technology inventory.");
            await CaptureRackUi(folder,"02-full-inventory.png");
            await RackKey(Key.Enter);
            await WaitRackUi(()=>HeldInventoryRig()?.kind=="trap" && LocalProgress.techSlots[slot].count==limit-1,"deploy a trap from slot");
            await RackKey(Key.Enter);
            await WaitRackUi(()=>HeldInventoryRig()==null && LocalProgress.techSlots[slot].count==limit,"pack the same clean trap");
            report.Check(true,"Enter deployed and repacked one physical trap, conserving the stack.");
            await RackKey(Key.Escape);
            report.Check(!InventionPanelOpen && UiRoot.Instance.Page==UiPage.None,"Esc closed the personal inventory without opening pause.");
            UiRoot.Instance.ShowPage(UiPage.Pause);
            await RackKey(Key.Tab);
            report.Check(!InventionPanelOpen && UiRoot.Instance.Page==UiPage.Pause,"Tab during pause left the existing dialog in control.");
            UiRoot.Instance.ClosePanel();
            var stored=TeamSave.Load(savePath,progression).Player(ProgressKey(host));
            report.Check(stored!=null && stored.techSlots[slot].count==limit && stored.techSlots[slot].kind=="trap","The selected full stack persisted in the player's private campaign profile.");
        }

        async Awaitable CaptureRackUi(string folder,string name,float settle=.25f)
        {
            await Awaitable.WaitForSecondsAsync(settle);
            RefreshInventoryCanvas();
            Canvas.ForceUpdateCanvases();
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,name));
        }
    }
}
#endif
