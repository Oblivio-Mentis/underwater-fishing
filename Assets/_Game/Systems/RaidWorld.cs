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
    public enum WorldAction { DiveOne, DiveTwo, Process=3, Feed, Reel, Fins, Tank, Hull, Winch, LoadCargo, Hoist, Collect, ProcessAll, FeedAll, StopProcessing, StopFeeding, HoldFish }

    public sealed partial class RaidWorld : NetworkBehaviour
    {
        public static RaidWorld Instance {get; private set;}
        public WorldTuning tuning;
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
            ResetFishRoute();
            ResetAirlock();
            message.Value="Depth 1: catch fish and deliver cargo";
            saveFailed=false;
            travelDisplay.Value=0; stockLossDisplay.Value=0;
            // Activate the fishing ground on the first tick after all network actors have started.
            depth.Value=1; pendingDepth=1; transitionRemaining=Time.fixedDeltaTime;
            waveLossBudget=0; commandTimes.Clear();
            savePath=Path.Combine(Application.persistentDataPath,UI.UiInputGate.Active?"raid011-team-progress.json":"team-progress.json");
            string[] args=Environment.GetCommandLineArgs();
            int saveArg=Array.IndexOf(args,"-raid-save");
            if(saveArg>=0 && saveArg+1<args.Length) savePath=args[saveArg+1];
            try { team=File.Exists(savePath)?TeamSave.Load(savePath,progression):new TeamProgress(progression); }
            catch(Exception e) { saveFailed=true; team=new TeamProgress(progression); message.Value="SAVE ERROR: original preserved; progress is temporary"; Debug.LogError("[RAID Save] "+e.Message); }
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
        public override void OnStopClient() { StopDroppedViews();HideGosha();snapshot.OnChange-=OnSnapshot; interact?.Dispose(); interact=null; localDiver=null; LocalProgress=null; }
        private void OnSnapshot(string oldValue,string newValue,bool server)
        {
            if(string.IsNullOrEmpty(newValue)) return;
            view=JsonUtility.FromJson<TeamProgress>(newValue);
            ApplyLocalTuning();
        }
        private void ApplyLocalTuning()
        {
            if(localDiver==null || view==null) return;
            localDiver.SpeedMultiplier=1+view.finsLevel*progression.finsSpeedPerLevel;
            localDiver.OxygenMultiplier=1+Mathf.Min(1.2f,view.tankLevel*progression.oxygenPerTankLevel)+(LocalProgress?.skills.Contains(SkillId.S1)==true?progression.Skill(SkillId.S1).Magnitude*.01f:0);
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
            if(!UI.UiInputGate.Active && localDiver!=null && interact!=null && interact.WasPressedThisFrame())
            {
                if(TryAimDrop(localDiver, out var item)) localDiver.GetComponent<HarpoonTool>()?.RequestPickup(item.id);
                else if(CanUseTerminal)
                { ToggleTerminal(); }
                else if(Vector3.Distance(localDiver.transform.position,tuning.cargoPosition)<tuning.interactionRange)
                    Command(WorldAction.LoadCargo,0);
                else Command(WorldAction.Collect,0);
            }
        }
        private void FixedUpdate()
        {
            if(!IsServerInitialized || team==null) return;
            TickAirlock();
            TickGosha();
            TickRouteClock();
            team.TickHoist(Time.fixedDeltaTime,progression);
            // A defense belongs to the deadline that just expired. The next feeding
            // interval stays full in the save and begins ticking when defense ends.
            if((threats==null || !threats.WaveActive) && team.TickHunger(Time.fixedDeltaTime,progression))
            {
                var report=team.EvaluateHunger(progression);
                message.Value="Feeding: "+report.outcome+" ("+TeamProgress.FoodText(report.paid)+"/"+TeamProgress.FoodText(report.required)+")";
                if(report.Attack && threats!=null)
                {
                    waveLossBudget=Mathf.Min(progression.maximumStockLossCount,Mathf.FloorToInt(team.tank.Count*progression.maximumStockLossFraction));
                    threats.StartWave(report.deficit,Mathf.Max(1,team.highestDepth-1));
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
                diver.SpeedMultiplier=1+Mathf.Min(.6f,team.finsLevel*progression.finsSpeedPerLevel);
                diver.OxygenMultiplier=1+Mathf.Min(1.2f,team.tankLevel*progression.oxygenPerTankLevel)+SkillFactor(diver,SkillId.S1);
                diver.SafeDepthBonus=team.hullLevel*tuning.deepDepth+SkillValue(diver,SkillId.S2);
                diver.DepthOffset=depth.Value==2?tuning.deepDepth:tuning.shallowDepth;
                var tool=diver.GetComponent<HarpoonTool>();
                if(tool!=null) { tool.ReelMultiplier=1+Mathf.Min(.9f,team.reelLevel*progression.reelPowerPerLevel)+SkillFactor(diver,SkillId.H1); tool.SetPocketCapacity(3+SkillValue(diver,SkillId.C1)+SkillValue(diver,SkillId.C4)); if(diver.IsHelpless) tool.DropCargo(); }
            }
            if(Time.unscaledTime>=nextSnapshot) { nextSnapshot=Time.unscaledTime+tuning.snapshotInterval; Publish(); }
            if(Time.unscaledTime>=nextSave) { nextSave=Time.unscaledTime+tuning.autoSaveInterval; Save(); }
        }
        private void LateUpdate()
        {
            PresentDrops();
            PresentGosha();
            var state=IsServerInitialized?team:view;
            if(container==null || state==null) return;
            Vector3 destination=state.hoistPhase==HoistPhase.Raising || state.hoistPhase==HoistPhase.Unloading?tuning.terminalPosition:tuning.cargoPosition;
            container.position=Vector3.MoveTowards(container.position,destination,Time.deltaTime*4);
        }
        public void Command(WorldAction action,long id) { Act(action,id); }
        public void RequestCommand(WorldAction action,long id,int requestId,int expectedLevel=-1) { Act(action,id,requestId,expectedLevel); }
        [ServerRpc(RequireOwnership=false)]
        private void Act(WorldAction action,long id,int requestId=0,int expectedLevel=-1,NetworkConnection sender=null)
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
            Vector3 target=cargo?tuning.cargoPosition:tuning.terminalPosition;
            bool factory=Nereid.NereidVessel.Active!=null;
            if(factory && (action==WorldAction.LoadCargo || action==WorldAction.ProcessAll || action==WorldAction.FeedAll))
            {Respond(sender,requestId,false,"unavailable");return;}
            if(factory && action==WorldAction.Hoist && !CargoDispatch.CanUse(diver))
            {Respond(sender,requestId,false,"range");return;}
            bool stationCommand=!cargo && ShipConsole.Stations.Count>0;
            if(!cargo && !diver.IsInside) { Respond(sender,requestId,false,"onboard"); return; }
            if(!(factory && action==WorldAction.Hoist) && (stationCommand?!ShipConsole.CanUse(diver,action):Vector3.Distance(diver.transform.position,target)>tuning.interactionRange)) { Respond(sender,requestId,false,"range"); return; }
            if(FishRoute.active && (action==WorldAction.ProcessAll || action==WorldAction.FeedAll || action==WorldAction.DiveOne || action==WorldAction.DiveTwo || ((action==WorldAction.Process || action==WorldAction.Feed)&&IsRouting(id))))
            {Respond(sender,requestId,false,"route_busy");return;}
            if((action==WorldAction.ProcessAll || action==WorldAction.FeedAll) && id!=FishRouteQuote.Compute(team.tank,Mathf.Max(1,progression.foodRequired+(team.highestDepth-1)*progression.foodPerDepth)))
            {Respond(sender,requestId,false,"changed");return;}
            if(action>=WorldAction.Reel && action<=WorldAction.Winch && expectedLevel>=0 && team.Level((Upgrade)((int)action-(int)WorldAction.Reel))!=expectedLevel)
            { Respond(sender,requestId,false,"changed"); return; }
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
                case WorldAction.ProcessAll: success=BeginFishRoute(false); break;
                case WorldAction.FeedAll: success=BeginFishRoute(true); break;
                case WorldAction.StopProcessing: success=StopFishRoute(false,id); break;
                case WorldAction.StopFeeding: success=StopFishRoute(true,id); break;
                case WorldAction.LoadCargo:
                    var tool=diver.GetComponent<HarpoonTool>(); var fish=tool!=null?tool.CarriedFish:null;
                    if(team.hoistPhase!=HoistPhase.Loading) break;
                    if(fish==null)
                    {
                        foreach(var loot in FindObjectsByType<LootActor>())
                            if(loot.IsCarriedBy(diver) && team.LoadCargo(new CatchItem(loot.itemId,0,0,0,1,loot.tier,loot.scrap,loot.electronics),progression))
                            { loot.Consume(); success=true; break; }
                        break;
                    }
                    success = tool.UnloadCatches(team, progression);
                    break;
                case WorldAction.Hoist: success=team.BeginHoist(progression); break;
                default:
                    var upgrade=(Upgrade)((int)action-(int)WorldAction.Reel);
                    success=team.Buy(upgrade,diver.IsInside,progression); break;
            }
            if(success) { diver.ConfirmInteraction(); Publish(); Save(); }
            else message.Value="Action unavailable: distance, team inside, resources, capacity or phase";
            Respond(sender,requestId,success,success?"ok":"unavailable");
        }
        [TargetRpc] private void Respond(NetworkConnection target,int requestId,bool success,string reason) { CommandResponded?.Invoke(requestId,success,reason); }
        internal bool LoadSelectedCargo(HarpoonTool tool,DroppedKind kind,long id)
        {
            if(!IsServerInitialized || transitionRemaining>0 || team.hoistPhase!=HoistPhase.Loading)return false;
            if(kind==DroppedKind.Catch)
            {
                if(!tool.PlaceSelectedCatch(team,progression,id))return false;
                catchOwners.Remove(id);
            }
            else
            {
                var loot=LootActor.HeldBy(tool.Motor);
                if(loot==null || !loot.IsCarriedBy(tool.Motor))return false;
                var item=new CatchItem(loot.itemId,0,0,0,1,loot.tier,loot.scrap,loot.electronics);
                if(loot.Collector!=null)item.caughtBy=new[]{loot.Collector};
                if(!team.LoadCargo(item,progression))return false;
                loot.Consume();
            }
            Publish();
            Save();
            return true;
        }
        private bool AllInside() { foreach(var diver in DiverMotor.ServerDivers) if(!diver.IsInside) return false; return true; }
        private void CompleteTravel()
        {
            ResetGosha();
            depth.Value=pendingDepth;
            for(int i=0;i<DiverMotor.ServerDivers.Count;i++) DiverMotor.ServerDivers[i].Teleport(new Vector3((i-2)*1.5f,1.2f,1));
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
            var available=team.tank.FindAll(x=>!IsRouting(x.id));
            count=Mathf.Min(count,available.Count);
            if(count>0)
            {
                for(int i=0;i<count;i++)team.tank.Remove(available[available.Count-1-i]);
                waveLossBudget-=count;stockLossDisplay.Value+=count;Publish();Save();
            }
        }
        private void Publish() { if(team!=null) snapshot.Value=JsonUtility.ToJson(team.PublicView()); }
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
            bool nearCargo=Vector3.Distance(localDiver.transform.position,tuning.cargoPosition)<tuning.interactionRange;
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
                if(!view.CanVisitDepth(2)) GUILayout.Label("Depth 2 requires Hull + Lifetime "+progression.depthTwoLifetimeCatch);
                foreach(Upgrade upgrade in Enum.GetValues(typeof(Upgrade)))
                {
                    var cost=view.Cost(upgrade,progression);
                    if(GUILayout.Button(upgrade+" L"+view.Level(upgrade)+" · "+cost.biomass+"B "+cost.chitin+"C "+cost.scrap+"S "+cost.electronics+"E"))
                        Command((WorldAction)((int)WorldAction.Reel+(int)upgrade),0);
                }
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
