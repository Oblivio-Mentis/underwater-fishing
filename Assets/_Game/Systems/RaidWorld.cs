using System;
using System.IO;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public enum WorldAction { DiveOne, DiveTwo, Return, Process, Feed, Reel, Fins, Tank, Hull, Winch, LoadCargo, Hoist, Collect }

    public sealed class RaidWorld : NetworkBehaviour
    {
        public static RaidWorld Instance {get; private set;}
        public WorldTuning tuning;
        public ProgressionTuning progression;
        public ThreatDirector threats;
        public Transform container;
        private readonly SyncVar<string> snapshot=new();
        private readonly SyncVar<int> depth=new();
        private readonly SyncVar<bool> atBase=new(true);
        private readonly SyncVar<string> message=new("Choose depth at the reservoir terminal");
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
        public bool AtBase=>atBase.Value;
        public int CurrentDepth=>depth.Value;
        public TeamProgress View=>view;
        public string Message=>message.Value;
        public bool CanUseTerminal=>localDiver!=null && Vector3.Distance(localDiver.transform.position,tuning.terminalPosition)<tuning.interactionRange;
        public void ToggleTerminal()
        {
            if(!CanUseTerminal) return;
            showTerminal=!showTerminal;
            Cursor.lockState=showTerminal?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=showTerminal;
        }
        private void Awake() { Instance=this; }

        public override void OnStartServer()
        {
            saveFailed=false;
            atBase.Value=true; depth.Value=0; transitionRemaining=0; pendingDepth=0; waveLossBudget=0; commandTimes.Clear();
            savePath=Path.Combine(Application.persistentDataPath,"team-progress.json");
            string[] args=Environment.GetCommandLineArgs();
            int saveArg=Array.IndexOf(args,"-raid-save");
            if(saveArg>=0 && saveArg+1<args.Length) savePath=args[saveArg+1];
            try { team=File.Exists(savePath)?TeamSave.Load(savePath):new TeamProgress(progression); }
            catch(Exception e) { saveFailed=true; team=new TeamProgress(progression); message.Value="SAVE ERROR: original preserved; progress is temporary"; Debug.LogError("[RAID Save] "+e.Message); }
            if(threats!=null) threats.StockAttacked+=OnStockAttacked;
            Publish();
        }
        public override void OnStartClient()
        {
            snapshot.OnChange+=OnSnapshot;
            OnSnapshot("",snapshot.Value,false);
            interact=new InputAction("Use",InputActionType.Button,"<Keyboard>/e"); interact.Enable();
        }
        public override void OnStopServer()
        {
            Save(); if(threats!=null) threats.StockAttacked-=OnStockAttacked;
        }
        public override void OnStopClient() { snapshot.OnChange-=OnSnapshot; interact?.Dispose(); interact=null; localDiver=null; }
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
            localDiver.OxygenMultiplier=1+view.tankLevel*progression.oxygenPerTankLevel;
            localDiver.DepthOffset=AtBase?0:depth.Value==2?tuning.deepDepth:tuning.shallowDepth;
        }
        private void Update()
        {
            if(!IsClientInitialized) return;
            if(localDiver==null && ClientManager.Connection.FirstObject!=null)
                localDiver=ClientManager.Connection.FirstObject.GetComponent<DiverMotor>();
            ApplyLocalTuning();
            if(localDiver!=null && interact!=null && interact.WasPressedThisFrame())
            {
                if(Vector3.Distance(localDiver.transform.position,tuning.terminalPosition)<tuning.interactionRange)
                { ToggleTerminal(); }
                else if(Vector3.Distance(localDiver.transform.position,tuning.cargoPosition)<tuning.interactionRange)
                    Command(WorldAction.LoadCargo,0);
                else Command(WorldAction.Collect,0);
            }
        }
        private void FixedUpdate()
        {
            if(!IsServerInitialized || team==null) return;
            team.TickHoist(Time.fixedDeltaTime,progression);
            if(team.TickHunger(Time.fixedDeltaTime,progression))
            {
                var report=team.EvaluateHunger(progression);
                message.Value="Feeding: "+report.outcome+" ("+report.paid+"/"+report.required+")";
                if(report.Attack && threats!=null && !threats.WaveActive)
                {
                    waveLossBudget=Mathf.Min(progression.maximumStockLossCount,Mathf.FloorToInt(team.tank.Count*progression.maximumStockLossFraction));
                    threats.StartWave(report.deficit,Mathf.Max(1,team.highestDepth-1));
                }
            }
            if(transitionRemaining>0)
            {
                transitionRemaining-=Time.fixedDeltaTime;
                if(transitionRemaining<=0) CompleteTravel();
            }
            foreach(var diver in DiverMotor.ServerDivers)
            {
                diver.SpeedMultiplier=1+team.finsLevel*progression.finsSpeedPerLevel;
                diver.OxygenMultiplier=1+team.tankLevel*progression.oxygenPerTankLevel;
                diver.SafeDepthBonus=team.hullLevel*tuning.deepDepth;
                diver.DepthOffset=AtBase?0:depth.Value==2?tuning.deepDepth:tuning.shallowDepth;
                var tool=diver.GetComponent<HarpoonTool>();
                if(tool!=null) { tool.ReelMultiplier=1+team.reelLevel*progression.reelPowerPerLevel; if(diver.IsHelpless) tool.DropCargo(); }
            }
            if(Time.unscaledTime>=nextSnapshot) { nextSnapshot=Time.unscaledTime+tuning.snapshotInterval; Publish(); }
            if(Time.unscaledTime>=nextSave) { nextSave=Time.unscaledTime+tuning.autoSaveInterval; Save(); }
        }
        private void LateUpdate()
        {
            var state=IsServerInitialized?team:view;
            if(container==null || state==null) return;
            Vector3 destination=state.hoistPhase==HoistPhase.Raising || state.hoistPhase==HoistPhase.Unloading?tuning.terminalPosition:tuning.cargoPosition;
            container.position=Vector3.MoveTowards(container.position,destination,Time.deltaTime*4);
        }
        public void Command(WorldAction action,long id) { Act(action,id); }
        [ServerRpc(RequireOwnership=false)]
        private void Act(WorldAction action,long id,NetworkConnection sender=null)
        {
            if(sender==null || sender.FirstObject==null || team==null || transitionRemaining>0 || !Enum.IsDefined(typeof(WorldAction),action)) return;
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(diver==null || diver.IsHelpless) return;
            if(commandTimes.TryGetValue(sender.ClientId,out float last) && Time.unscaledTime-last<.15f) return;
            commandTimes[sender.ClientId]=Time.unscaledTime;
            if(action==WorldAction.Collect)
            {
                if(AtBase) return;
                if(Physics.Raycast(diver.transform.position+Vector3.up*.6f,diver.AimDirection,out var hit,3f))
                    hit.collider.GetComponent<LootActor>()?.Pickup(diver);
                return;
            }
            bool cargo=action==WorldAction.LoadCargo || action==WorldAction.Hoist;
            Vector3 target=cargo?tuning.cargoPosition:tuning.terminalPosition;
            if(Vector3.Distance(diver.transform.position,target)>tuning.interactionRange) return;
            bool success=false;
            switch(action)
            {
                case WorldAction.DiveOne: case WorldAction.DiveTwo:
                    int chosen=action==WorldAction.DiveOne?1:2;
                    if(!AtBase || !team.CanVisitDepth(chosen) || !AllInside() || team.hoistPhase!=HoistPhase.Loading || team.cargo.Count>0) break;
                    pendingDepth=chosen; transitionRemaining=tuning.transitionSeconds; message.Value="Submarine descending…"; success=true; break;
                case WorldAction.Return:
                    if(AtBase || !AllInside() || team.hoistPhase!=HoistPhase.Loading || team.cargo.Count>0) break;
                    pendingDepth=0; transitionRemaining=tuning.transitionSeconds; message.Value="Returning to base…"; success=true; break;
                case WorldAction.Process: if(AtBase) success=team.Process(id); break;
                case WorldAction.Feed: success=team.Feed(id); break;
                case WorldAction.LoadCargo:
                    var tool=diver.GetComponent<HarpoonTool>(); var fish=tool!=null?tool.CarriedFish:null;
                    if(AtBase || team.hoistPhase!=HoistPhase.Loading) break;
                    if(fish==null)
                    {
                        foreach(var loot in FindObjectsByType<LootActor>())
                            if(loot.IsCarriedBy(diver) && team.LoadCargo(new CatchItem(loot.itemId,0,0,0,1,loot.tier,loot.scrap,loot.electronics),progression))
                            { loot.Consume(); success=true; break; }
                        break;
                    }
                    var item=new CatchItem(fish.CargoId,fish.biomass,fish.chitin,fish.offering,fish.cargoSize,fish.depthTier);
                    if(team.LoadCargo(item,progression)) { success=tool.TakeCargo(); if(!success) team.cargo.RemoveAll(x=>x.id==item.id); }
                    break;
                case WorldAction.Hoist: success=team.BeginHoist(progression); break;
                default:
                    var upgrade=(Upgrade)((int)action-(int)WorldAction.Reel);
                    success=team.Buy(upgrade,AtBase,progression); break;
            }
            if(success) { Publish(); Save(); }
            else message.Value="Action unavailable: distance, team inside, resources, capacity or phase";
        }
        private bool AllInside() { foreach(var diver in DiverMotor.ServerDivers) if(!diver.IsInside) return false; return true; }
        private void CompleteTravel()
        {
            atBase.Value=pendingDepth==0; depth.Value=pendingDepth;
            for(int i=0;i<DiverMotor.ServerDivers.Count;i++) DiverMotor.ServerDivers[i].Teleport(new Vector3((i-2)*1.5f,1.2f,1));
            foreach(var fish in FishActor.FindObjectsByType<FishActor>()) fish.SetDepthActive(!AtBase && fish.depthTier==pendingDepth);
            foreach(var loot in FindObjectsByType<LootActor>()) loot.SetDepth(pendingDepth);
            if(threats!=null) threats.SetDepth(pendingDepth);
            message.Value=AtBase?"Base: process catch and upgrade equipment":"Depth "+depth.Value+": catch fish and deliver cargo";
        }
        private void OnStockAttacked(float severity)
        {
            if(!IsServerInitialized || waveLossBudget<=0) return;
            int count=Mathf.Min(waveLossBudget,Mathf.Max(1,Mathf.CeilToInt(severity)));
            count=Mathf.Min(count,team.tank.Count);
            if(count>0) { team.tank.RemoveRange(team.tank.Count-count,count); waveLossBudget-=count; Publish(); Save(); }
        }
        private void Publish() { if(team!=null) snapshot.Value=JsonUtility.ToJson(team); }
        private void Save()
        {
            if(saveFailed || team==null || string.IsNullOrEmpty(savePath)) return;
            try { TeamSave.Save(savePath,team); }
            catch(Exception e) { message.Value="SAVE ERROR: "+e.Message; Debug.LogError("[RAID Save] "+e.Message); }
        }
        private void OnDestroy() { interact?.Dispose(); if(Instance==this) Instance=null; }
        private void OnGUI()
        {
            if(!IsClientInitialized || view==null || localDiver==null) return;
            GUILayout.BeginArea(new Rect(Screen.width-420,16,400,Screen.height-160),GUI.skin.box);
            GUILayout.Label("DEPTH "+depth.Value+" / "+(AtBase?"BASE":"DIVE"));
            GUILayout.Label(message.Value);
            GUILayout.Label("B "+view.biomass+" · Chitin "+view.chitin+" · Scrap "+view.scrap+" · Electronics "+view.electronics);
            GUILayout.Label("Lifetime "+view.lifetimeCatch+" · Tank "+view.tank.Count+" · Food "+view.offering+" · Hunger "+view.hungerRemaining.ToString("F0")+"s");
            GUILayout.Label("Cargo "+view.CargoUsed+"/"+view.Capacity(progression)+" · "+view.hoistPhase+" "+view.hoistRemaining.ToString("F0"));
            if(threats!=null) GUILayout.Label(threats.Message);
            bool nearCargo=Vector3.Distance(localDiver.transform.position,tuning.cargoPosition)<tuning.interactionRange;
            bool nearTerminal=Vector3.Distance(localDiver.transform.position,tuning.terminalPosition)<tuning.interactionRange;
            if(nearCargo && Cursor.lockState!=CursorLockMode.Locked)
            {
                if(GUILayout.Button("Load carried catch [E]")) Command(WorldAction.LoadCargo,0);
                if(GUILayout.Button("Start hoist")) Command(WorldAction.Hoist,0);
            }
            if(nearTerminal) GUILayout.Label("[E] Reservoir terminal · Esc opens cursor");
            if(nearTerminal && showTerminal)
            {
                if(AtBase)
                {
                    if(GUILayout.Button("Depth 1 · Biomass / Scrap · light threats")) Command(WorldAction.DiveOne,0);
                    GUI.enabled=view.CanVisitDepth(2);
                    if(GUILayout.Button("Depth 2 · Chitin / Electronics · dangerous predator")) Command(WorldAction.DiveTwo,0);
                    GUI.enabled=true;
                    if(!view.CanVisitDepth(2)) GUILayout.Label("Depth 2 requires Hull + Lifetime "+progression.depthTwoLifetimeCatch);
                    foreach(Upgrade upgrade in Enum.GetValues(typeof(Upgrade)))
                    {
                        var cost=view.Cost(upgrade,progression);
                        if(GUILayout.Button(upgrade+" L"+view.Level(upgrade)+" · "+cost.biomass+"B "+cost.chitin+"C "+cost.scrap+"S "+cost.electronics+"E"))
                            Command((WorldAction)((int)WorldAction.Reel+(int)upgrade),0);
                    }
                }
                else if(GUILayout.Button("Return to base · all divers inside")) Command(WorldAction.Return,0);
                for(int i=0;i<Mathf.Min(view.tank.Count,8);i++)
                {
                    var fish=view.tank[i]; GUILayout.BeginHorizontal(); GUILayout.Label("Catch #"+fish.id+" ("+fish.biomass+"B / "+fish.offering+" food)");
                    GUI.enabled=AtBase;
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
