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
        [Serializable] sealed class RackReport
        {
            public string status="running",phase,error;
            public int stackLimit,fieldSlot;
            public List<string> checks=new(),backgroundInputRoutes=new();
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
                await WaitRackUi(()=>Instance && Instance.LocalProgress!=null && Instance.localDiver && Instance.NetworkObject &&
                    Instance.IsServerInitialized && Instance.InventionRackTransform && UiRoot.Instance && UiRoot.Instance.Context.Connected &&
                    UiRoot.Instance.Page==UiPage.None && !UiInputGate.Modal && Time.unscaledTime>=UiInputGate.ResumeAt,"private inventory and physical rack",95);
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
                if(Time.realtimeSinceStartup>=until)
                {
                    var w=Instance;var owner=w?w.localDiver:null;
                    string status=w && owner?$" | focus={Application.isFocused} cursor={Cursor.lockState} page={UiRoot.Instance?.Page} modal={UiInputGate.Modal} reserved={UiInputGate.PointerReserved} LMB={Mouse.current?.leftButton.isPressed} inside={owner.IsInside} placement={w.PlacementHint} near={w.NearInventionRack(owner)} cameraProduct={w.RackAimForTrial(owner,true)} serverProduct={w.RackAimForTrial(owner,false)}":"";
                    if(w && owner && w.TryGetRackParcel(owner.OwnerId,out int id,out var pos,out bool resting))
                        status+=$" parcel={id} resting={resting} delta={pos-owner.transform.position}";
                    throw new TimeoutException(step+status);
                }
                await Awaitable.NextFrameAsync();
            }
        }

        string RackAimForTrial(DiverMotor diver,bool camera) =>
            TryRackProduct(diver,camera,out var item)?item.id:"(none)";

        static bool RackNormalHud() => UiRoot.Instance.Page==UiPage.None && !UiInputGate.Modal &&
            !GameObject.Find("Vending machine UI") && !GameObject.Find("Technology inventory");

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
            if(!gear.SelectSlot(1) && gear.QaBackgroundPocketSelect(1))
                report.backgroundInputRoutes.Add("Select the ordinary free-hands slot");
            await WaitRackUi(()=>gear.ActiveSlot==1,"ordinary free-hands slot for the rack camera");
            var rack=InventionRackTransform;
            var wideView=rack.position+rack.forward*2.3f+rack.right*.16f-Vector3.up*.63f;
            host.Teleport(wideView);
            host.LookAtConsole(rack.position-Vector3.up*.12f);
            await WaitRackUi(()=>host.IsInside && UiRoot.Instance.Page==UiPage.None,"clear cabin overview of the vending machine");
            await Awaitable.WaitForSecondsAsync(.35f);
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"00-vending-machine.png"));
            var balance=team.Treasury;
            async Awaitable Aim(string kind)
            {
                host.LookAtConsole(RackProductPosition(kind));
                await WaitRackUi(()=>RackAimForTrial(host,true)==kind && RackAimForTrial(host,false)==kind,
                    "camera and authoritative crosshair both point at "+kind);
                await Awaitable.WaitForSecondsAsync(.12f);
            }
            async Awaitable ReadProduct(string kind,string expectedStatus)
            {
                var title=TechCatalog.Find(kind).title;
                var point=RackProductPosition(kind);
                await WaitRackUi(()=>rack.GetComponentsInChildren<TextMeshPro>().Any(label=>label.enabled && label.text==title) &&
                    rack.GetComponentsInChildren<TextMeshPro>().Any(label=>label.enabled && label.text==expectedStatus) &&
                    rack.GetComponentsInChildren<LineRenderer>().Any(line=>line.enabled && line.positionCount==4 &&
                        Vector3.Distance(line.transform.position,point)<.05f),
                    "machine shows the status, title and highlight at the physical cell for "+kind);
                report.Check(true,"Machine display and outline matched "+kind+" ("+expectedStatus+").");
            }
            async Awaitable Denied(string kind,string reason)
            {
                await Awaitable.WaitForSecondsAsync(.4f);
                int revision=LocalProgress.revision;
                bool? accepted=null;
                void Receipt(bool ok,string message){accepted=ok;}
                SkillResponded+=Receipt;
                try
                {
                    host.CmdRackTake(kind);
                    await WaitRackUi(()=>accepted.HasValue,reason+" server receipt");
                }
                finally{SkillResponded-=Receipt;}
                report.Check(accepted==false && LocalProgress.revision==revision && RackParcelCount==0 && team.Treasury.Equals(balance),reason);
            }
            async Awaitable ClickProduct(string kind)
            {
                if(RackAimForTrial(host,true)!=kind || !RackNormalHud())
                    throw new InvalidOperationException("HUD crosshair is not over "+kind);
                var pointer=Mouse.current??InputSystem.AddDevice<Mouse>();
                var pos=pointer.position.ReadValue();
                try
                {
                    InputSystem.QueueStateEvent(pointer,new MouseState{position=pos,buttons=1});
                    await Awaitable.WaitForSecondsAsync(.08f);
                }
                finally{InputSystem.QueueStateEvent(pointer,new MouseState{position=pos});}
                if(!RackParcelInFlight(kind) && Application.isBatchMode)
                {
                    if(!UiRoot.Instance.QaClickReticle())throw new InvalidOperationException("Physical pointer did not accept "+kind);
                    report.backgroundInputRoutes.Add("Rack click via the physical reticle handler: "+kind);
                }
                await WaitRackUi(()=>RackParcelInFlight(kind),"actual "+kind+" case left the vendor chute");
            }
            report.phase="distance, cabinet and product checks";
            host.Teleport(rack.position+rack.forward*(tuning.interactionRange+1.2f)-Vector3.up*.65f);
            host.LookAtConsole(RackProductPosition(trap.id));
            await WaitRackUi(()=>!NearInventionRack(host) && !RackFocused(host,true) && !RackFocused(host,false),
                "rack is outside the allowed range");
            await Denied(trap.id,"A request outside interaction range produced no case or inventory change.");
            host.Teleport(rack.position+rack.forward*1.77f+rack.right*.23f-Vector3.up*.65f);
            host.LookAtConsole(rack.TransformPoint(new Vector3(.50f,.09f,.10f)));
            await WaitRackUi(()=>RackFocused(host,true) && RackFocused(host,false) &&
                !TryRackProduct(host,true,out _) && !TryRackProduct(host,false,out _),"reticle on the cabinet keypad beside the product window");
            await Denied(trap.id,"Aiming at the cabinet body cannot issue an item.");
            await Aim("charge");
            await ReadProduct("charge","Нужен чертёж");
            report.Check(view.TechRank("charge","unlock")==0,"The crosshair recognized the locked charge in its own physical cell.");
            UiRoot.Instance.QaClickReticle();
            await Awaitable.WaitForSecondsAsync(.35f);
            report.Check(RackParcelCount==0 && pockets.All(i=>gear.InventionInSlot(i).count==0),"Clicking a locked product left both the chute and inventory empty.");
            await Denied("charge","The server rejected a locked product even when the reticle matched its cell.");
            await Aim(trap.id);
            await ReadProduct(trap.id,"ЛКМ · Получить");
            const string other="magnet";
            await Denied(other,"The server rejected a different researched item while the reticle pointed at the trap.");
            await RackKey(Key.E);
            await RackKey(Key.Enter);
            report.Check(RackNormalHud() && RackParcelCount==0 && pockets.All(i=>gear.InventionInSlot(i).count==0),
                "E and Enter at a product left the normal HUD active and did not place an order.");
            await CaptureRackUi(folder,"01-product-aim.png");
            report.phase="reticle purchases without a separate screen";
            await ClickProduct(trap.id);
            await CaptureRackUi(folder,"03-ejected-trap.png",.06f);
            if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"04-trap-in-flight.png",.10f);
            if(RackParcelInFlight(trap.id))await CaptureRackUi(folder,"05-trap-in-flight.png",.09f);
            await WaitRackUi(()=>gear.InventionInSlot(trapSlot).count==1,"first delivery skipped the ball");
            report.Check(gear.HasBallInSlot(ballSlot) && gear.InventionInSlot(trapSlot).kind=="trap" && RackNormalHud(),
                "The left-clicked 3D trap left the chute and entered the next ordinary pocket; the ball remained in place.");
            // Leave an earlier pocket empty to distinguish stack-first selection from simply taking the first available slot.
            gear.RemoveBall();
            async Awaitable Order(string kind,Func<bool> arrived,string reason)
            {
                await Awaitable.WaitForSecondsAsync(.4f);
                await Aim(kind);
                await ClickProduct(kind);
                await WaitRackUi(arrived,reason);
            }
            for(int count=2;count<=limit;count++)
            {
                int expected=count;
                await Order(trap.id,()=>gear.InventionInSlot(trapSlot).count==expected,"top up the existing trap stack to "+expected);
                report.Check(gear.InventionInSlot(ballSlot).count==0,"The earlier empty pocket stayed empty at trap count "+expected+".");
                if(count==2)
                {
                    await ReadProduct(trap.id,"Достигнут лимит команды");
                    await CaptureRackUi(folder,"07-team-limit.png");
                    await Denied(trap.id,"The server refused the third trap while there was room in the selected stack and a free pocket.");
                    team.techPurchases.Add(new TechPurchase{item="trap",upgrade="limit",rank=2});
                    Publish();
                    await WaitRackUi(()=>view.TechRank("trap","limit")==2,"team limit increased to four with the matching rank");
                    await ReadProduct(trap.id,"ЛКМ · Получить");
                }
            }
            report.Check(gear.StoreBall() && gear.HasBallInSlot(ballSlot),"Replaced the ball into the free first ordinary pocket.");
            await Aim(other);
            await ReadProduct(other,"ЛКМ · Получить");
            report.Check(Vector3.Distance(RackProductPosition(trap.id),RackProductPosition(other))>.10f,
                "The second researched product is in a physically different display cell.");
            await CaptureRackUi(folder,"06-second-product-aim.png");
            await Order(other,()=>gear.InventionInSlot(otherSlot).kind==other && gear.InventionInSlot(otherSlot).count==1,
                "left click on a different product placed it in the last free pocket");
            await Aim(trap.id);
            await ReadProduct(trap.id,"Нет места");
            await Denied(trap.id,"A perfectly aimed order was refused with all ordinary pockets occupied.");
            report.Check(gear.HasBallInSlot(ballSlot) && gear.InventionInSlot(trapSlot).count==limit &&
                gear.InventionInSlot(otherSlot).kind==other,"Full pockets preserved the ball, the full trap stack and the other item.");
            gear.RemoveBall();
            await Order(trap.id,()=>gear.InventionInSlot(ballSlot).kind=="trap" && gear.InventionInSlot(ballSlot).count==1,
                "extra trap starts a second stack in the newly freed ordinary pocket");
            await ReadProduct(trap.id,"Достигнут лимит команды");
            report.Check(balance.Equals(team.Treasury),"All physical rack deliveries were free.");
            report.Check(RackNormalHud(),"The normal HUD remained visible throughout both purchases.");

            report.phase="ordinary hotbar underwater";
            host.Teleport(CargoLoadingPosition+new Vector3(10,2.5f,4));
            await WaitRackUi(()=>!NearInventionRack(host) && !host.IsInside && UiRoot.Instance.Page==UiPage.None,"diver outside");
            await RackKey(Key.Tab);
            report.Check(RackNormalHud() &&
                Time.unscaledTime>=UiInputGate.ResumeAt && (Application.isFocused?!UiInputGate.Blocked:Application.isBatchMode),
                "Tab underwater opened no separate technology inventory.");
            await RackKey(Key.Digit4);
            if(gear.ActiveSlot!=trapSlot && gear.QaBackgroundPocketSelect(trapSlot))
                report.backgroundInputRoutes.Add("Digit 4: ordinary slot selector");
            await WaitRackUi(()=>gear.ActiveSlot==trapSlot,"number 4 selects the full trap stack on the ordinary HUD");
            var bar=GameObject.Find("HUD Equipment");
            bool HudStack(int slot,string kind,int count)
            {
                if(!bar)return false;
                var well=bar.transform.Find("Slot "+(slot+1)+"/Slot well");
                if(!well || !well.gameObject.activeInHierarchy)return false;
                var art=well.Find("Item")?.GetComponent<Image>();
                var label=well.Find("Stack count")?.GetComponent<TMP_Text>();
                return art && art.enabled && art.sprite && art.sprite.name==kind && art.sprite.texture.name=="NereidInventionIcons" && label && label.enabled &&
                    label.text=="\u00d7"+count;
            }
            await WaitRackUi(()=>HudStack(trapSlot,"trap",limit) && HudStack(ballSlot,"trap",1) && HudStack(otherSlot,other,1),
                "ordinary HUD icons and counts match each of the three equipment pockets");
            report.Check(true,"The ordinary hotbar displays both trap stacks and the other product with their individual icons and counts.");
            bool HandModel(string kind) =>
                host.GetComponentInChildren<Camera>().transform.Find("Diver's hands/Held invention · "+kind) is Transform model &&
                model.gameObject.activeInHierarchy && model.GetComponentInChildren<MeshRenderer>().enabled;
            await WaitRackUi(()=>HandModel("trap"),"selecting the trap slot draws the trap in first-person hands");
            report.Check(team.rigs.Count==0 && gear.InventionInSlot(trapSlot).count==limit,
                "Selecting a slot displays the equipment without deploying or spending it.");
            await CaptureRackUi(folder,"02-full-inventory.png");
            var search=host.transform.position;
            bool site=false;
            for(int i=0;i<36 && !site;i++)
            {
                float angle=i*2.4f, radius=3+i*.54f;
                var above=search+new Vector3(radius*Mathf.Sin(angle),6,radius*Mathf.Cos(angle));
                if(!Physics.Raycast(above,Vector3.down,out var floor,80,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                host.Teleport(floor.point+new Vector3(0,1.15f,-1.4f));
                host.LookAtConsole(floor.point);
                Physics.SyncTransforms();
                site=FindInstallationSite(host,"trap",0,host.AimDirection,team,out _,out _,out _);
            }
            report.Check(site,"Found a reachable level installation surface in the open sea.");
            await WaitRackUi(()=>placementProjection && placementProjection.activeInHierarchy && PlacementHint==null,
                "the equipment hologram confirms the same placement site");
            var installationPosition=host.transform.position;
            var installationAim=host.AimDirection;
            await CaptureRackUi(folder,"08-placement-preview.png");
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1,position=mouse.position.ReadValue()});
                await Awaitable.WaitForSecondsAsync(.45f);
                await WaitRackUi(()=>view.rigs.Exists(r=>r.kind=="trap" && r.mode=="placed" && r.operatorId<0) &&
                    gear.InventionInSlot(trapSlot).count==limit-1,"Mouse input in the regular UI mounts one trap from the hotbar");
            }
            finally{InputSystem.QueueStateEvent(mouse,new MouseState{position=mouse.position.ReadValue()});}
            await Awaitable.WaitForSecondsAsync(.35f);
            var installed=team.rigs.Single(r=>r.kind=="trap");
            host.LookAtConsole(installed.position);
            await RackKey(Key.R);
            if(team.rigs.Contains(installed) && gear.QaBackgroundPocketStow(installed.serial))
                report.backgroundInputRoutes.Add("R: ordinary clean-installation storage action");
            await WaitRackUi(()=>view.rigs.Count==0 && gear.InventionInSlot(trapSlot).count==limit,"R returns the clean rig to its source stack");
            report.Check(gear.InventionInSlot(ballSlot).count==1 && gear.InventionInSlot(otherSlot).kind==other,
                "Repacking restored the selected stack while preserving the other two ordinary pockets.");
            await RackKey(Key.Q);
            await WaitRackUi(()=>team.rigs.Any(r=>r.kind=="trap" && r.mode=="dropped") &&
                gear.InventionInSlot(trapSlot).count==limit-1,"Q drops one packed trap onto the seabed");
            var droppedTrap=team.rigs.Single(r=>r.kind=="trap" && r.mode=="dropped");
            report.Check(VendorIssuedCount("trap")==4,"A trap dropped on the ground still belongs to the team's issued stock.");
            host.LookAtConsole(droppedTrap.position);
            await WaitRackUi(()=>TryAimDrop(host,out var drop) && drop.kind==DroppedKind.Invention,
                "aiming exposes the packed trap as an ordinary pickup");
            await CaptureRackUi(folder,"08b-dropped-trap.png");
            await RackKey(Key.E);
            await WaitRackUi(()=>team.rigs.Count==0 && gear.InventionInSlot(trapSlot).count==limit,
                "E picks up the trap at the team cap and returns it to the selected stack");
            UiRoot.Instance.ShowPage(UiPage.Pause);
            await RackKey(Key.Tab);
            report.Check(UiRoot.Instance.Page==UiPage.Pause,"Tab during pause left the dialog in control.");
            UiRoot.Instance.ClosePanel();
            var stored=TeamSave.Load(savePath,progression).Player(ProgressKey(host));
            report.Check(stored?.pocketStacks?.Length==7 && pockets.All(i=>stored.pocketStacks[i-2].count==gear.InventionInSlot(i).count &&
                stored.pocketStacks[i-2].kind==gear.InventionInSlot(i).kind),
                "Disk reload retains the exact two trap stacks and other item in their ordinary pocket positions.");

            report.phase="throwing dynamite from its ordinary slot";
            var profile=team.Player(ProgressKey(host));
            team.techPurchases.Add(new TechPurchase{item="charge",upgrade="unlock",rank=1});
            profile.pocketStacks[trapSlot-2]=new TechSlot{kind="charge",count=2};
            SendProfile(host.Owner,profile);
            Publish();
            host.LookAtConsole(host.transform.position+Vector3.up*.7f+Vector3.forward*14f);
            await WaitRackUi(()=>gear.ActiveSlot==trapSlot && gear.InventionInSlot(trapSlot).kind=="charge" &&
                HandModel("charge"),"the charge appears between the gloves when its slot is selected");
            await CaptureRackUi(folder,"09-dynamite-in-hands.png");
            var mousePosition=mouse.position.ReadValue();
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1,position=mousePosition});
                await WaitRackUi(()=>gear.WindingCharge,"Mouse input in the regular UI starts winding the charge",3);
                await Awaitable.WaitForSecondsAsync(.13f);
                report.Check(team.rigs.Count==0 && gear.InventionInSlot(trapSlot).count==2,
                    "Holding left click winds up the charge in hand without lighting its fuse or consuming a stack.");
            }
            finally{InputSystem.QueueStateEvent(mouse,new MouseState{position=mousePosition});}
            await Awaitable.WaitForSecondsAsync(.13f);
            await WaitRackUi(()=>team.rigs.Any(r=>r.kind=="charge" && r.mode=="fuse") &&
                gear.InventionInSlot(trapSlot).count==1,"releasing the mouse throws one live charge");
            var charge=team.rigs.Single(r=>r.kind=="charge");
            var release=charge.position;
            await Awaitable.WaitForSecondsAsync(.45f);
            report.Check(charge.mode=="fuse" && Vector3.Distance(charge.position,release)>.3f && charge.timer>0,
                "The charge flies away from the hand while its fuse is burning.");
            await CaptureRackUi(folder,"10-dynamite-in-flight.png",.03f);
            await WaitRackUi(()=>!team.rigs.Contains(charge),"the server consumes the charge after the fuse");
            report.Check(charge.mode=="spent" && gear.InventionInSlot(trapSlot).count==1,
                "The timed detonation consumed the thrown charge and preserved the second one.");

            report.phase="technology branch on the ship monitor";
            team.techPurchases.Add(new TechPurchase{item="vacuum",upgrade="unlock",rank=1});
            team.Treasury=new MaterialCost(100,100,100,100);
            Publish();
            var monitor=ShipConsole.Stations.Single(s=>s.kind==ShipConsoleKind.SkillTree);
            var screen=monitor.screenAnchor;
            host.Teleport(screen.position-screen.forward*1.71f+screen.right*.64f-Vector3.up*.38f);
            host.LookAtConsole(screen.position);
            await WaitRackUi(()=>monitor.InRange(host) && host.IsInside && UiRoot.Instance.QaPointAtMonitor() &&
                UiRoot.Instance.Page==UiPage.SkillTree,"open the ship skill monitor");
            async Awaitable PointAtControl(string id)
            {
                Button FindControl()=>UiRoot.Instance.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==id);
                await WaitRackUi(()=>FindControl(),"visible monitor control "+id);
                await Awaitable.WaitForSecondsAsync(.45f);
                host.LookAtConsole(UiQaProbe.ReticleTarget(FindControl()));
                await Awaitable.WaitForSecondsAsync(.30f);
            }
            await PointAtControl("Sector0");
            report.Check(UiRoot.Instance.QaClickReticle(),"Opened the standard harpoon tree with the reticle.");
            var nativeGraph=UiRoot.Instance.GetComponentsInChildren<RectTransform>()
                .First(rect=>rect.name=="ProgressionGraphViewport");
            var nativeBranch=nativeGraph.GetComponentInParent<CanvasGroup>();
            await WaitRackUi(()=>nativeBranch.blocksRaycasts,"the original skill branch finished opening");
            host.LookAtConsole(screen.position);
            await CaptureRackUi(folder,"11a-native-harpoon.png");
            foreach(string id in new[]{"ProgressionOverview","Sector8","TechCategory0","TechItem-vacuum"})
            {
                await PointAtControl(id);
                report.Check(UiRoot.Instance.QaClickReticle(),"Aimed at and clicked "+id+" on the physical screen.");
            }
            await WaitRackUi(()=>nativeGraph.gameObject.activeInHierarchy && nativeBranch.blocksRaycasts &&
                UiRoot.Instance.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ProgressionStage"))==5,
                "the technology tree uses the original harpoon viewport and five stage keys");
            host.LookAtConsole(screen.position);
            await CaptureRackUi(folder,"11-technology-vacuum.png");
            await PointAtControl("TechNode-vacuum_capacity");
            report.Check(UiRoot.Instance.QaClickReticle(),"Selected a rank in the native skill graph.");
            host.LookAtConsole(screen.position);
            await CaptureRackUi(folder,"11b-technology-vacuum-rank.png");
            report.Check(TechRig.TotalLimit(team,TechCatalog.Find("vacuum"))==1,"The baseline limit is one vacuum for the team.");
            await PointAtControl("TechPurchase");
            report.Check(UiRoot.Instance.QaClickReticle(),"Clicked the available capacity upgrade on the physical screen.");
            await WaitRackUi(()=>team.TechRank("vacuum","vacuum_capacity")==1 &&
                TechRig.TotalLimit(team,TechCatalog.Find("vacuum"))==2,"a real monitor purchase raised the team limit to two");
            UiRoot.Instance.ClosePanel();

            report.phase="autonomous vacuum from the ordinary slot";
            profile.pocketStacks[trapSlot-2]=new TechSlot{kind="vacuum",count=1};
            SendProfile(host.Owner,profile);
            Publish();
            host.Teleport(installationPosition);
            host.LookAtConsole(installationPosition+installationAim*3+Vector3.up*.6f);
            await WaitRackUi(()=>gear.InventionInSlot(trapSlot).kind=="vacuum" && HandModel("vacuum") &&
                FindInstallationSite(host,"vacuum",0,host.AimDirection,team,out _,out _,out _),"a valid vacuum placement in the selected slot");
            await CaptureRackUi(folder,"12-vacuum-placement.png");
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1,position=mouse.position.ReadValue()});
                await Awaitable.WaitForSecondsAsync(.2f);
            }
            finally{InputSystem.QueueStateEvent(mouse,new MouseState{position=mouse.position.ReadValue()});}
            await WaitRackUi(()=>team.rigs.Any(r=>r.kind=="vacuum" && r.mode=="placed") &&
                gear.InventionInSlot(trapSlot).count==0,"an ordinary pocket item became an active vacuum installation");
            var vacuum=team.rigs.Single(r=>r.kind=="vacuum");
            var fish=FishActor.ServerSchool.First(f=>f && f.depthTier==CurrentDepth && f.CanHarvestRig(false));
            long idAtIntake=fish.CargoId;
            fish.transform.position=vacuum.position+Vector3.up*.22f+Vector3.left*.15f;
            host.Teleport(installationPosition+Vector3.up*7);
            await WaitRackUi(()=>vacuum.contents.Any(c=>c.id==idAtIntake),"the vacuum collects a real fish while the player is away");
            report.Check(vacuum.operatorId<0 && vacuum.helperId<0,"Suction ran with no diver holding a hose or operating the machine.");
        }

        async Awaitable CaptureRackUi(string folder,string name,float settle=.25f)
        {
            await Awaitable.WaitForSecondsAsync(settle);
            Canvas.ForceUpdateCanvases();
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,name));
        }
    }
    // Opt-in player fixtures still use the production key/mouse path first. A hidden
    // batch player can lack desktop focus (or cursor capture) even with the gameplay UI open.
    public sealed partial class HarpoonTool
    {
        bool QaBackgroundPocketAction(Func<bool> action,bool mouse=false)
        {
            if(!Application.isBatchMode || (Application.isFocused && (!mouse || Cursor.lockState==CursorLockMode.Locked)) ||
                !IsClientInitialized || !IsOwner || !UiRoot.Instance || UiRoot.Instance.Page!=UiPage.None ||
                UiInputGate.Modal || Time.unscaledTime<UiInputGate.ResumeAt || UiInputGate.MonitorLook ||
                mouse && UiInputGate.PointerReserved || CheatPanel.PointerActive)
                return false;
            // Blocked combines menu state and desktop focus. The menu conditions were checked above.
            // Restore it on the same call stack, before an Update or another input handler can run.
            bool previous=UiInputGate.Active;
            try{UiInputGate.Active=false;return action();}
            finally{UiInputGate.Active=previous;}
        }
        internal bool QaBackgroundPocketSelect(int slot) => QaBackgroundPocketAction(()=>SelectSlot(slot));
        internal bool QaBackgroundPocketStow(int serial) => QaBackgroundPocketAction(()=>
        {
            Motor.CmdTechStore(serial);
            return true;
        });
    }

}
#endif
