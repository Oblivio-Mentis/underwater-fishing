using System;
using System.IO;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    // Command IDs are used on the wire. Value 2 belonged to a retired navigation command.
    public enum WorldAction { DiveOne, DiveTwo, Process=3, Feed, LoadCargo=10, Hoist, Collect, ProcessAll, FeedAll, StopProcessing, StopFeeding, HoldFish, SendFish, SendProcessingDraft, SendFeedingDraft }

    public sealed partial class RaidWorld : NetworkBehaviour
    {
        public static RaidWorld Instance {get; private set;}
        public WorldTuning tuning;
        public Vector3 CargoLoadingPosition
        {
            get
            {
                var vessel=Nereid.NereidVessel.Active;
                if(vessel && vessel.authoredRail && vessel.authoredRail.Ready) return vessel.authoredRail.stops[0].marker.position;
                return tuning.cargoPosition;
            }
        }
        public ProgressionTuning progression;
        public ThreatDirector threats;
        public Transform container;
        private readonly SyncVar<string> snapshot=new();
        private readonly SyncVar<int> depth=new(1);
        private readonly SyncVar<string> message=new("Depth 1: catch fish and deliver cargo");
        private readonly SyncVar<float> travelDisplay=new();
        private readonly SyncVar<int> stockLossDisplay=new();
        public int StockLossCount => stockLossDisplay.Value;
        public float TravelRemaining => travelDisplay.Value;
        public event Action<int,bool,string> CommandResponded;
        private TeamProgress team,view;
        private DiverMotor localDiver;
        private InputAction interact;
        private bool showTerminal;
        private float nextSnapshot,nextSave,transitionRemaining;
        private int pendingDepth,waveLossBudget;
        private readonly System.Collections.Generic.Dictionary<int,float> commandTimes=new();
        private string savePath;
        private bool saveFailed;
        public TeamProgress Team=>team;
        public int CurrentDepth=>depth.Value;
        public TeamProgress View=>view;
        public string Message=>message.Value;
        public bool CanUseTerminal=>localDiver!=null && localDiver.IsInside && Vector3.Distance(localDiver.transform.position,tuning.terminalPosition)<tuning.interactionRange;
        public void ToggleTerminal()
        {
            if(UI.UiInputGate.Active) return;
            if(!CanUseTerminal) return;
            showTerminal=!showTerminal;
            Cursor.lockState=showTerminal?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=showTerminal;
        }
        private void Awake() { Instance=this; }

        public override void OnStartServer()
        {
            crewProfiles.Clear();
            ServerManager.OnRemoteConnectionState+=UnbindProfile;
            catchOwners.Clear();
            looseFishMotion.Clear();
            blastCatches.Clear();
            ResetKrakenBreath();
            ResetCrabs();
            ResetFishRoute();
            ResetAirlock();
            message.Value="Depth 1: catch fish and deliver cargo";
            saveFailed=false;
            travelDisplay.Value=0; stockLossDisplay.Value=0;
            // Activate the fishing ground on the first tick after all network actors have started.
            depth.Value=1; pendingDepth=1; transitionRemaining=Time.fixedDeltaTime;
            waveLossBudget=0; commandTimes.Clear();
            savePath=Path.Combine(Application.persistentDataPath,"raid019-team-progress.json");
            string[] args=Environment.GetCommandLineArgs();
            int saveArg=Array.IndexOf(args,"-raid-save");
            if(saveArg>=0 && saveArg+1<args.Length) savePath=args[saveArg+1];
            if(Array.IndexOf(args,"-raid-new-campaign")>=0)
            {
                var absolute=Path.GetFullPath(savePath);
                savePath=Path.Combine(Path.GetDirectoryName(absolute),"raid019-"+Guid.NewGuid().ToString("N")+".json");
            }
            try { team=File.Exists(savePath)?TeamSave.Load(savePath,progression):new TeamProgress(progression); }
            catch(Exception e) { saveFailed=true; team=new TeamProgress(progression); message.Value="Сохранение несовместимо или повреждено. Запустите новую кампанию из Start-New-Campaign.cmd"; Debug.LogError("[RAID Save] "+e.Message); }
            pendingDepth=team.expeditionDepth;
            StartTech();
            ClearRackOrders();
            StartGosha();
            if(threats!=null) threats.StockAttacked+=OnStockAttacked;
            Publish();
        }
        public override void OnStartClient()
        {
            if(Nereid.NereidAirlock.Active!=null)Nereid.NereidAirlock.Active.ApplyDoors(airlock.Value);
            StartDroppedViews();
            WetCargoShoal.Attach(this);
            snapshot.OnChange+=OnSnapshot;
            OnSnapshot("",snapshot.Value,false);
            interact=new InputAction("Use",InputActionType.Button,"<Keyboard>/e"); interact.Enable();
        }
        public override void OnStopServer()
        {
            ServerManager.OnRemoteConnectionState-=UnbindProfile;
            StopGosha();
            Save(); if(threats!=null) threats.StockAttacked-=OnStockAttacked;
        }
        public override void OnStopClient() { ClearVendingAim();ClearRackParcelViews();StopTechPresentation();StopCrabViews();StopDroppedViews();HideGosha();snapshot.OnChange-=OnSnapshot; interact?.Dispose(); interact=null; localDiver=null; LocalProgress=null; }
        private void OnSnapshot(string oldValue,string newValue,bool server)
        {
            if(string.IsNullOrEmpty(newValue)) return;
            view=JsonUtility.FromJson<TeamProgress>(newValue);
            if(rackDisplay)rackDisplay.RefreshLabels(view);
            ApplyLocalTuning();
        }
        private void ApplyLocalTuning()
        {
            if(localDiver==null || view==null) return;
            localDiver.OxygenMultiplier=1+SkillTreeDefinition.Value(LocalProgress?.skills,SkillLine.Oxygen)*.01f;
            localDiver.DepthOffset=depth.Value==2?tuning.deepDepth:tuning.shallowDepth;
        }
        private void Update()
        {
            if(!IsClientInitialized) return;
            if(localDiver==null && ClientManager.Connection.FirstObject!=null)
            {
                localDiver=ClientManager.Connection.FirstObject.GetComponent<DiverMotor>();
                if(localDiver!=null)ConnectProfile();
            }
            ApplyLocalTuning();
            EnsureInventionRack();
            PresentRackParcels();
            PresentCrabs();
            if(!UI.UiInputGate.Active && localDiver!=null)
                PointAtVendingMachine(localDiver,Application.isFocused && Mouse.current?.leftButton.wasPressedThisFrame==true);
            if(!UI.UiInputGate.Active && localDiver!=null && !AimingAtVendingMachine && interact!=null && interact.WasPressedThisFrame())
            {
                if(TryAimDrop(localDiver, out var item)) localDiver.GetComponent<HarpoonTool>()?.RequestPickup(item.id);
                else if(CanUseTerminal)
                { ToggleTerminal(); }
                else if(Vector3.Distance(localDiver.transform.position,CargoLoadingPosition)<tuning.interactionRange)
                    Command(WorldAction.LoadCargo,0);
                else Command(WorldAction.Collect,0);
            }
        }
        private void FixedUpdate()
        {
            if(!IsServerInitialized || team==null) return;
            EnsureInventionRack();
            TickRackOrders(Time.fixedDeltaTime);
            TickLooseFish(Time.fixedDeltaTime);
            TickCrabs();
            TickAirlock();
            TickTech(Time.fixedDeltaTime);
            TickKrakenBreath(Time.fixedDeltaTime);
            TickGosha();
            TickRouteClock();
            AbsorbCageDrops();
            var beforeHoist=team.Treasury;
            var previousHoist=team.hoistPhase;
            var salvageYield=team.TickHoist(Time.fixedDeltaTime,progression);
            if(previousHoist!=team.hoistPhase)
            {
                if(team.hoistPhase==HoistPhase.Returning && team.cargo.Count>0)
                    message.Value=$"Клеть возвращает остаток груза: {team.cargo.Count} шт.";
                Publish();
                Save();
            }
            if(!salvageYield.IsEmpty)
                SendProcessingReceipt(salvageYield,beforeHoist,team.Treasury,fromSalvage:true);
            // A defense belongs to the deadline that just expired. The next feeding
            // interval stays full in the save and begins ticking when defense ends.
            if((threats==null || !threats.WaveActive) && team.TickHunger(Time.fixedDeltaTime,progression))
            {
                var report=team.EvaluateHunger(progression);
                message.Value="Feeding: "+report.outcome+" ("+TeamProgress.FoodText(report.paid)+"/"+TeamProgress.FoodText(report.required)+")";
                if(report.Attack && threats!=null)
                {
                    waveLossBudget=team.StockLossLimit(progression);
                    threats.StartWave(report.deficit*(1-report.mitigation),Mathf.Max(1,team.highestDepth-1));
                }
                Publish();
            }
            if(transitionRemaining>0)
            {
                transitionRemaining-=Time.fixedDeltaTime;
                if(transitionRemaining<=0) CompleteTravel();
            }
            travelDisplay.Value=Mathf.Max(0,transitionRemaining);
            foreach(var diver in DiverMotor.ServerDivers)
            {
                diver.OxygenMultiplier=1+SkillFactor(diver,SkillLine.Oxygen);
                diver.SafeDepthBonus=(team.HasTeamLine(SkillLine.Hull)?tuning.deepDepth:0)+team.TeamValue(SkillLine.Hull)+SkillValue(diver,SkillLine.Pressure);
                diver.DepthOffset=depth.Value==2?tuning.deepDepth:tuning.shallowDepth;
                var tool=diver.GetComponent<HarpoonTool>();
                if(tool!=null) { tool.ReelMultiplier=1+SkillFactor(diver,SkillLine.Reel); tool.SetPocketCapacity(3+SkillValue(diver,SkillLine.Pockets)); if(diver.IsHelpless) tool.DropCargo(); }
            }
            if(Time.unscaledTime>=nextSnapshot) { nextSnapshot=Time.unscaledTime+tuning.snapshotInterval; Publish(); }
            if(Time.unscaledTime>=nextSave) { nextSave=Time.unscaledTime+tuning.autoSaveInterval; Save(); }
        }
        private void LateUpdate()
        {
            PresentDrops();
            PresentTech();
            PresentGosha();
            var state=IsServerInitialized?team:view;
            if(container==null || state==null) return;
            var hull=Nereid.NereidVessel.Active;
            if(hull && hull.authoredRail && hull.authoredRail.Ready) return;
            Vector3 destination=state.hoistPhase==HoistPhase.Raising || state.hoistPhase==HoistPhase.Unloading?tuning.terminalPosition:CargoLoadingPosition;
            container.position=Vector3.MoveTowards(container.position,destination,Time.deltaTime*4);
        }
        static ShipConsoleKind? DispatchControl(WorldAction action) => action switch
        {
            WorldAction.Hoist => ShipConsoleKind.CargoControl,
            WorldAction.SendProcessingDraft or WorldAction.StopProcessing => ShipConsoleKind.Process,
            WorldAction.SendFeedingDraft or WorldAction.StopFeeding => ShipConsoleKind.Feed,
            _ => null
        };

        public void Command(WorldAction action,long id) => RequestCommand(action,id,0);

        public void RequestCommand(WorldAction action,long id,int requestId)
        {
            var pointedAt=Vector3.zero;
            bool hasContact=false;
            if(DispatchControl(action).HasValue)
            {
                var actor=NetworkManager.ClientManager.Connection?.FirstObject;
                var camera=actor ? actor.GetComponentInChildren<Camera>() : null;
                if(camera && Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var hit,
                    4f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    pointedAt=hit.point;
                    hasContact=true;
                }
            }
            Act(action,id,requestId,pointedAt,hasContact);
        }
        [ServerRpc(RequireOwnership=false)]
        private void Act(WorldAction action,long id,int requestId,Vector3 pointedAt,bool hasContact,NetworkConnection sender=null)
        {
            if(sender==null || sender.FirstObject==null || team==null || !Enum.IsDefined(typeof(WorldAction),action)) return;
            if(transitionRemaining>0) { Respond(sender,requestId,false,"travel"); return; }
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(diver==null || diver.IsHelpless) { Respond(sender,requestId,false,"helpless"); return; }
            if(commandTimes.TryGetValue(sender.ClientId,out float last) && Time.unscaledTime-last<.15f) { Respond(sender,requestId,false,"cooldown"); return; }
            commandTimes[sender.ClientId]=Time.unscaledTime;
            if(action==WorldAction.Collect)
            {
                if(Physics.Raycast(diver.transform.position+Vector3.up*.6f,diver.AimDirection,out var hit,3f))
                { bool picked=hit.collider.GetComponent<LootActor>()?.Pickup(diver)??false; Respond(sender,requestId,picked,picked?"ok":"unavailable"); }
                else Respond(sender,requestId,false,"range");
                return;
            }
            bool cargo=action==WorldAction.LoadCargo || action==WorldAction.Hoist;
            if((action==WorldAction.ProcessAll || action==WorldAction.FeedAll || action==WorldAction.StopProcessing || action==WorldAction.StopFeeding) && ShipConsole.Stations.Count==0)
            {Respond(sender,requestId,false,"unavailable");return;}
            Vector3 target=cargo?CargoLoadingPosition:tuning.terminalPosition;
            bool factory=Nereid.NereidVessel.Active!=null;
            if(factory && (action==WorldAction.LoadCargo || action==WorldAction.ProcessAll || action==WorldAction.FeedAll))
            {Respond(sender,requestId,false,"unavailable");return;}
            bool stationCommand=!cargo && ShipConsole.Stations.Count>0;
            if(!cargo && !diver.IsInside) { Respond(sender,requestId,false,"onboard"); return; }
            var switchKind=DispatchControl(action);
            // A legacy hoist or the button on the submerged cage has no interior plunger to animate.
            if(action==WorldAction.Hoist && (!factory || CargoDispatch.CanUse(diver))) switchKind=null;
            if(switchKind.HasValue)
            {
                // Reliable click and its target travel together. Look/turn updates arrive on another tick.
                if(!hasContact || !ShipConsole.CanPressAt(diver,switchKind.Value,pointedAt))
                {Respond(sender,requestId,false,"range");return;}
            }
            else if(!(factory && action==WorldAction.Hoist) && (stationCommand?!ShipConsole.CanUse(diver,action):Vector3.Distance(diver.transform.position,target)>tuning.interactionRange))
            { Respond(sender,requestId,false,"range"); return; }
            bool dispatch=action is WorldAction.SendFish or WorldAction.SendProcessingDraft or WorldAction.SendFeedingDraft;
            CatchDestination? dispatchDestination=action switch
            {
                WorldAction.SendProcessingDraft=>CatchDestination.Process,
                WorldAction.SendFeedingDraft=>CatchDestination.Feed,
                _=>null
            };
            if((FishRoute.active || HasCommittedDispatch) &&
                (dispatch || action is WorldAction.ProcessAll or WorldAction.FeedAll or WorldAction.DiveOne or WorldAction.DiveTwo))
            {Respond(sender,requestId,false,"route_busy");return;}
            if(dispatch && (!factory || id==0 || id!=FishRouteQuote.ComputeDraft(team.tank,dispatchDestination)))
            {Respond(sender,requestId,false,"changed");return;}
            if((action==WorldAction.ProcessAll || action==WorldAction.FeedAll) && id!=FishRouteQuote.Compute(team.tank,Mathf.Max(1,progression.foodRequired+(team.highestDepth-1)*progression.foodPerDepth)))
            {Respond(sender,requestId,false,"changed");return;}
            if(action==WorldAction.Hoist && team.cargo.Count>0 && team.HoistFishCount(progression)==0)
            {
                if(!team.HasHoistSalvage){Respond(sender,requestId,false,"reservoir_full");return;}
                if(!team.CanCreditCargo()){Respond(sender,requestId,false,"resource_full");return;}
            }
            bool success=false;
            switch(action)
            {
                case WorldAction.DiveOne: case WorldAction.DiveTwo:
                    int chosen=action==WorldAction.DiveOne?1:2;
                    if(chosen==CurrentDepth || !team.CanVisitDepth(chosen) || !AllInside() || team.hoistPhase!=HoistPhase.Loading || team.cargo.Count>0) break;
                    pendingDepth=chosen; transitionRemaining=tuning.transitionSeconds; message.Value="Changing depth…"; success=true; break;
                case WorldAction.Process: success=factory?ChooseFishRoute(id,CatchDestination.Process):ProcessDeliveredFish(id); break;
                case WorldAction.Feed: success=factory?ChooseFishRoute(id,CatchDestination.Feed):team.Feed(id,progression); break;
                case WorldAction.HoldFish: success=ChooseFishRoute(id,CatchDestination.Hold);break;
                case WorldAction.SendFish:
                case WorldAction.SendProcessingDraft:
                case WorldAction.SendFeedingDraft:
                    success=CommitFishDispatch(dispatchDestination);break;
                case WorldAction.ProcessAll: success=BeginFishRoute(false); break;
                case WorldAction.FeedAll: success=BeginFishRoute(true); break;
                case WorldAction.StopProcessing: success=StopFishRoute(false,id); break;
                case WorldAction.StopFeeding: success=StopFishRoute(true,id); break;
                case WorldAction.LoadCargo:
                    var tool=diver.GetComponent<HarpoonTool>();
                    if(tool==null || team.hoistPhase!=HoistPhase.Loading)break;
                    var salvage=tool.CarriedSalvage;
                    success=salvage!=null ? salvage.LoadInto(tool,team,progression,salvage.CargoId)
                        : tool.UnloadCatches(team,progression);
                    break;
                case WorldAction.Hoist: success=team.BeginHoist(progression); break;
            }
            if(success)
            {
                diver.ConfirmInteraction();
                if(switchKind.HasValue) ShowDispatchPress(switchKind.Value,sender.ClientId);
                Publish(); Save();
            }
            else if(!factory && action is (WorldAction.Process or WorldAction.Feed) &&
                team.tank.Exists(item=>item.id==id && CanRoute(item,action==WorldAction.Feed?CatchDestination.Feed:CatchDestination.Process)))
            {Respond(sender,requestId,false,"resource_full");return;}
            else message.Value="Action unavailable: distance, team inside, resources, capacity or phase";
            Respond(sender,requestId,success,success?"ok":"unavailable");
        }
        [ObserversRpc]
        private void ShowDispatchPress(ShipConsoleKind kind,int operatorId)
        {
            // The operator starts their hand and cap together on input.
            if(NetworkManager.ClientManager.Connection.ClientId==operatorId)return;
            foreach(var station in ShipConsole.Stations)
                if(station && station.kind==kind && station.gameObject.scene==gameObject.scene)
                    station.GetComponent<Nereid.NereidDispatchButton>()?.Press(null);
        }
        [TargetRpc] private void Respond(NetworkConnection target,int requestId,bool success,string reason) { CommandResponded?.Invoke(requestId,success,reason); }
        // A catch lying inside the recovery cage is part of the cage manifest: the count, the hoist
        // and the tank must agree with every fish the crew sees in the basket.
        private void AbsorbCageDrops()
        {
            if(team==null || team.hoistPhase!=HoistPhase.Loading) return;
            for(int i=dropped.Count-1;i>=0;i--)
            {
                var item=dropped[i];
                if(item.handDropped || item.spoiled || item.crabCarried || item.depth!=CurrentDepth || item.kind!=DroppedKind.Catch || item.Fish==null) continue;
                if(!CargoDispatch.ContainsDrop(this,item.position)) continue;
                if(!team.LoadCargo(AttributeCatch(item.Fish.ToCargo(item.id)),progression)) continue;
                RemoveDrop(item.id);
                Publish();
                Save();
            }
        }
        internal void ReleaseLoadedCatchOwner(long id)=>catchOwners.Remove(id);
        internal int LoadAllCargo(HarpoonTool tool)
        {
            if(!IsServerInitialized || transitionRemaining>0 || team.hoistPhase!=HoistPhase.Loading)return 0;
            int moved=tool.LoadAllFish(team,progression,SaveTransaction);
            if(moved>0)Publish();
            return moved;
        }

        internal bool LoadSelectedCargo(HarpoonTool tool,DroppedKind kind,long id)
        {
            if(!IsServerInitialized || transitionRemaining>0 || team.hoistPhase!=HoistPhase.Loading)return false;
            if(kind==DroppedKind.Catch)
            {
                if(!tool.PlaceSelectedCatch(team,progression,id))return false;
                catchOwners.Remove(id);
            }
            else if(kind!=DroppedKind.Salvage || tool.CarriedSalvage?.LoadInto(tool,team,progression,id)!=true)
                return false;
            Publish();
            Save();
            return true;
        }
        private bool AllInside() { foreach(var diver in DiverMotor.ServerDivers) if(!diver.IsInside) return false; return true; }
        private void CompleteTravel()
        {
            ResetKrakenBreath();
            ResetCrabs();
            ResetGosha();
            ResetGoshaJunk();
            depth.Value=pendingDepth;
            team.expeditionDepth=pendingDepth;
            for(int i=0;i<DiverMotor.ServerDivers.Count;i++) DiverMotor.ServerDivers[i].Teleport(Nereid.NereidDeckNavigation.CrewSpawn(i,new Vector3((i-2)*1.5f,1.2f,1)));
            foreach(var rig in team.rigs)
            {
                var rider=rig.mode=="carried"?TechDiver(rig.operatorId):null;
                if(!rider)continue;
                rig.depth=pendingDepth;
                rig.position=rig.anchor=rider.transform.position+Vector3.up*.45f;
            }
            Save();
            foreach(var fish in FishActor.FindObjectsByType<FishActor>()) fish.EnterHabitat(fish.depthTier==pendingDepth);
            foreach(var loot in FindObjectsByType<LootActor>()) loot.SetDepth(pendingDepth);
            if(threats!=null) threats.SetDepth(pendingDepth);
            if(!saveFailed) message.Value="Depth "+depth.Value+": catch fish and deliver cargo";
        }
        private void OnStockAttacked(float severity)
        {
            if(!IsServerInitialized || waveLossBudget<=0) return;
            int count=Mathf.Min(waveLossBudget,Mathf.Max(1,Mathf.CeilToInt(severity)));
            // Committed pipe batches are behind closed valves; only stock still
            // available in the reservoir can be stolen. Original non-batch loss is unchanged.
            var available=team.tank.FindAll(x=>!x.dispatchCommitted && !IsRouting(x.id));
            count=Mathf.Min(count,available.Count);
            if(count>0)
            {
                for(int i=0;i<count;i++)team.tank.Remove(available[available.Count-1-i]);
                waveLossBudget-=count;stockLossDisplay.Value+=count;Publish();Save();
            }
        }
        private void Publish()
        {
            if(team==null)return;
            if(IsServerInitialized)RefreshVendorStock();
            snapshot.Value=JsonUtility.ToJson(team.PublicView());
        }
        private void Save()
        {
            if(saveFailed || team==null || string.IsNullOrEmpty(savePath)) return;
            try { TeamSave.Save(savePath,team); }
            catch(Exception e) { message.Value="SAVE ERROR: "+e.Message; Debug.LogError("[RAID Save] "+e.Message); }
        }
        private void OnDestroy() { interact?.Dispose(); if(Instance==this) Instance=null; }
        private void OnGUI()
        {
            if(UI.UiInputGate.Active) return;
            if(!IsClientInitialized || view==null || localDiver==null) return;
            GUILayout.BeginArea(new Rect(Screen.width-420,16,400,Screen.height-160),GUI.skin.box);
            GUILayout.Label("DEPTH "+depth.Value);
            GUILayout.Label(message.Value);
            GUILayout.Label("B "+view.biomass+" · Chitin "+view.chitin+" · Scrap "+view.scrap+" · Electronics "+view.electronics);
            GUILayout.Label("Lifetime "+view.lifetimeCatch+" · Tank "+view.tank.Count+" · Food "+view.offering+" · Hunger "+view.hungerRemaining.ToString("F0")+"s");
            GUILayout.Label("Cargo "+view.CargoUsed+"/"+view.Capacity(progression)+" · "+view.hoistPhase+" "+view.hoistRemaining.ToString("F0"));
            if(threats!=null) GUILayout.Label(threats.Message);
            bool nearCargo=Vector3.Distance(localDiver.transform.position,CargoLoadingPosition)<tuning.interactionRange;
            bool nearTerminal=CanUseTerminal;
            if(nearCargo && Cursor.lockState!=CursorLockMode.Locked)
            {
                if(GUILayout.Button("Load carried catch [E]")) Command(WorldAction.LoadCargo,0);
                if(GUILayout.Button("Start hoist")) Command(WorldAction.Hoist,0);
            }
            if(nearTerminal) GUILayout.Label("[E] Reservoir terminal · Esc opens cursor");
            if(nearTerminal && showTerminal)
            {
                GUI.enabled=CurrentDepth!=1;
                if(GUILayout.Button("Depth 1 · Biomass / Scrap · light threats")) Command(WorldAction.DiveOne,0);
                GUI.enabled=CurrentDepth!=2 && view.CanVisitDepth(2);
                if(GUILayout.Button("Depth 2 · Chitin / Electronics · dangerous predator")) Command(WorldAction.DiveTwo,0);
                GUI.enabled=true;
                if(!view.CanVisitDepth(2)) GUILayout.Label("Depth 2 requires Hull I");
                for(int i=0;i<Mathf.Min(view.tank.Count,8);i++)
                {
                    var fish=view.tank[i]; GUILayout.BeginHorizontal(); GUILayout.Label("Catch #"+fish.id+" ("+fish.biomass+"B / "+fish.offering+" food)");
                    if(GUILayout.Button("Process")) Command(WorldAction.Process,fish.id);
                    GUI.enabled=true;
                    if(GUILayout.Button("Feed")) Command(WorldAction.Feed,fish.id);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();
        }
    }
}
