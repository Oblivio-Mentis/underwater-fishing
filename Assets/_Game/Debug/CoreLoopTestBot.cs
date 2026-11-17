using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Fishing.Debugging;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnderwaterFishing.Progression;
using UnderwaterFishing.Progression.Network;
using UnderwaterFishing.World;
using UnityEngine;

namespace UnderwaterFishing.Debugging
{
    public enum CoreLoopRole { Disabled, Captain, Hunter }

    /// <summary>Development-only owner driver. Observes replicated results; every gameplay action uses normal owner RPCs.</summary>
    public sealed class CoreLoopTestBot : MonoBehaviour
    {
        private enum Stage { Connecting, StartDay, HaulMaterials, Hunt, HelpHunter, DeliverFish, ReturnAboard,
            WaitPhase, ProcessCargo, BuyOxygen, CraftSonar, ReserveFood, SubmitOffering, AwaitResult, ResetRun, Finished }
        private enum DelegateActor { None, Fishing, Cargo }
        public CoreLoopRole Role { get; private set; }
        public bool Configured => Role != CoreLoopRole.Disabled;
        private NetworkDiver diver;
        private DiverFishing fishing;
        private DiverCargo cargoActor;
        private DiverExpedition expeditionActor;
        private DiverProgression progressionActor;
        private FishingTestBot fishingBot;
        private CargoTestBot cargoBot;
        private NetworkExpedition expedition;
        private NetworkProgression progression;
        private NetworkCargoStation station;
        private Stage stage;
        private DelegateActor delegated;
        private ExpeditionPhase previousPhase;
        private bool initialized, failureMode, sawFailure, sawSuccess, gearConfirmed, sonarCrafted, sonarUsed, sawReplicatedCharge, buySent, craftSent;
        private int startedDays, completedDays, initialQuota, haulTargetCount, processedCount;
        private float began, stageAt, nextTick, nextRequest, nextLog, arrivedAt = -1, baselineOxygen;
        private string captureDirectory;
        private ulong pendingProcess;
        private float pendingAt;
        private readonly HashSet<ulong> processedIds = new HashSet<ulong>();
        private readonly Dictionary<string, int> expectedStocks = new Dictionary<string, int>();
        private readonly Dictionary<string, int> retainedStocks = new Dictionary<string, int>();
        private readonly List<Vector3> route = new List<Vector3>();
        private Vector3 routeGoal;
        private int routeIndex;
        private bool routeSet;
        private static readonly Vector3 ExpeditionTerminal = new Vector3(-6, -7.2f, 8.78f);
        private static readonly Vector3 ProcessTerminal = new Vector3(-2, -7.2f, 8.8f);
        private static readonly Vector3 WorkshopTerminal = new Vector3(2, -7.2f, 8.8f);
        private static readonly Vector3 OfferingTerminal = new Vector3(6, -7.2f, 8.8f);
        private static readonly Vector3 HunterParking = new Vector3(3, -7, -4);

        private void Awake()
        {
            if (!Application.isEditor && !UnityEngine.Debug.isDebugBuild) { enabled = false; return; }
            string[] args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "--raid-core-bot");
            if (role < 0) { enabled = false; return; }
            string value = role + 1 < args.Length ? args[role + 1] : "";
            Role = value == "captain" ? CoreLoopRole.Captain : value == "hunter" ? CoreLoopRole.Hunter : CoreLoopRole.Disabled;
            if (!Configured) { UnityEngine.Debug.LogError("RAID_CORE_LOOP_FAIL role=Disabled reason=invalid-role"); enabled = false; return; }
            failureMode = Array.IndexOf(args, "--raid-core-failure") >= 0;
            int capture = Array.IndexOf(args, "--raid-core-capture");
            if (capture >= 0 && capture + 1 < args.Length) captureDirectory = args[capture + 1];
            diver = GetComponent<NetworkDiver>(); fishing = GetComponent<DiverFishing>(); cargoActor = GetComponent<DiverCargo>();
            expeditionActor = GetComponent<DiverExpedition>(); progressionActor = GetComponent<DiverProgression>();
            fishingBot = GetComponent<FishingTestBot>(); cargoBot = GetComponent<CargoTestBot>();
            began = Time.unscaledTime; stage = Stage.Connecting; stageAt = began;
        }

        private void Update()
        {
            if (!Configured || diver == null || !diver.IsOwner || !diver.IsClientStarted || stage == Stage.Finished) return;
            float now = Time.unscaledTime;
            if (now < nextTick) return;
            nextTick = now + .1f;
            if (now - began > 870) { Fail("overall-deadline"); return; }
            expedition = NetworkExpedition.Instance; progression = NetworkProgression.Instance; station = NetworkCargoStation.Instance;
            if (expedition == null || progression == null || station == null || NetworkDiver.All.Count < 2)
            { if (now - began > 60) Fail("missing-authorities-or-two-owners"); return; }
            if (fishingBot == null || cargoBot == null || expeditionActor == null || progressionActor == null || cargoActor == null)
            { Fail("missing-owner-components"); return; }
            if (!initialized)
            {
                if (progression.State.Quota <= 0 || progression.Stocks.Count != progression.Settings.Resources.Length || station.State.Capacity <= 0)
                { if (now - began > 60) Fail("initial-snapshot-deadline"); return; }
                initialized = true; previousPhase = expedition.State.Phase; initialQuota = progression.State.Quota;
                baselineOxygen = diver.Balance.Diving.OxygenCapacity;
                if (initialQuota <= 0 || expedition.State.Phase != ExpeditionPhase.Base || station.ProtectedHold.Count != 0 || !StocksEmpty()
                    || progression.Level(CrewUpgrade.Oxygen) != 0 || progression.Charges(diver.OwnerId) != 0)
                { Fail("run-not-initially-empty"); return; }
                Log($"START mode={(failureMode ? "failure" : "success")} owner={diver.OwnerId} quota={initialQuota} baselineOxygen={Number(baselineOxygen)} lanOnly=true");
                Enter(Stage.StartDay);
            }
            if (!diver.IsActive) { Fail("owner-incapacitated-or-dead"); return; }
            ObservePhase();
            if (stage == Stage.Finished) return;
            ObserveGearAndSonar();
            if (stage == Stage.Finished) return;
            if (expedition.State.Emergency && expedition.State.Phase != ExpeditionPhase.Base) { Fail("unexpected-emergency-return"); return; }
            if (expedition.State.ReturnRequested && !diver.State.Sheltered && RequestReady()) expeditionActor.SetReturnConsent(true);
            switch (stage)
            {
                case Stage.StartDay: StartDay(); break;
                case Stage.HaulMaterials: HaulMaterials(); break;
                case Stage.Hunt:
                    if (fishing.HasCatch) { StopDelegates(); cargoBot.BeginAutomation(true, 1); delegated = DelegateActor.Cargo; Enter(Stage.DeliverFish); }
                    break;
                case Stage.HelpHunter:
                    if (FoodValue() >= initialQuota && (startedDays != 2 || sonarUsed)) BeginReturn();
                    break;
                case Stage.DeliverFish:
                    if (FoodValue() >= initialQuota && !fishing.HasCatch && !cargoActor.HasLoot) BeginReturn();
                    break;
                case Stage.ReturnAboard: ReturnAboard(); break;
                case Stage.WaitPhase: Park(); break;
                case Stage.ProcessCargo: ProcessCargo(); break;
                case Stage.BuyOxygen: BuyOxygen(); break;
                case Stage.CraftSonar: CraftSonar(); break;
                case Stage.ReserveFood: ReserveFood(); break;
                case Stage.SubmitOffering: SubmitOffering(); break;
                case Stage.AwaitResult: AwaitResult(); break;
                case Stage.ResetRun: ResetRun(); break;
            }
            if (now - stageAt > 210 && stage != Stage.Finished) Fail("stage-deadline-" + stage);
            if (now >= nextLog && stage != Stage.Finished)
            {
                nextLog = now + 8;
                Log($"DIAG stage={stage} phase={expedition.State.Phase} days={startedDays}/{completedDays} delegated={delegated} pos={diver.State.Position} oxygen={Number(diver.State.Diving.Oxygen)} hold={station.ProtectedHold.Count} stocks={StockSummary()} feedback={progressionActor.Feedback}");
            }
        }

        private void ObservePhase()
        {
            var phase = expedition.State.Phase;
            if (phase == previousPhase) return;
            previousPhase = phase; StopDelegates(); progressionActor.Close(); routeSet = false;
            Log($"PHASE phase={phase} day={expedition.State.Day} completed={expedition.State.CompletedDays}");
            Capture("phase-" + phase + "-" + startedDays);
            if (phase == ExpeditionPhase.Expedition)
            {
                startedDays++;
                if (startedDays > 3) { Fail("unexpected-fourth-expedition"); return; }
                if (expedition.State.RemainingSeconds < 170 && !failureMode) { Fail("expedition-duration-below-170-use-raid-expedition-seconds-180"); return; }
                if (failureMode || startedDays == 3)
                {
                    if (!failureMode) { retainedStocks.Clear(); foreach (var row in progression.Stocks) retainedStocks.Add(row.Id, row.Amount); }
                    Enter(Stage.ReturnAboard); return;
                }
                if (Role == CoreLoopRole.Hunter) BeginFishing(false);
                else if (startedDays == 1)
                {
                    haulTargetCount = station.ProtectedHold.Count + 2;
                    cargoBot.BeginAutomation(false, 2); delegated = DelegateActor.Cargo; Enter(Stage.HaulMaterials);
                }
                else BeginFishing(true);
                return;
            }
            if (phase == ExpeditionPhase.Debrief)
            {
                completedDays++;
                if (expedition.State.CompletedDays != completedDays) { Fail("day-counter-mismatch"); return; }
                Log($"DAY_COMPLETE count={completedDays} hold={station.ProtectedHold.Count}");
                Enter(Stage.WaitPhase); return;
            }
            if (phase == ExpeditionPhase.Base)
            {
                if (sawFailure || sawSuccess) { VerifyFinished(); return; }
                if (completedDays == 1 && !failureMode) Enter(Role == CoreLoopRole.Captain ? Stage.ProcessCargo : Stage.WaitPhase);
                else if (completedDays == 2 && !failureMode) Enter(Role == CoreLoopRole.Captain ? Stage.ReserveFood : Stage.WaitPhase);
                else Enter(Stage.StartDay);
                return;
            }
            if (phase == ExpeditionPhase.Offering)
            {
                if (completedDays != 3) { Fail("offering-before-three-days"); return; }
                Enter(failureMode ? Stage.AwaitResult : Role == CoreLoopRole.Captain ? Stage.SubmitOffering : Stage.AwaitResult); return;
            }
            if (phase == ExpeditionPhase.Failure)
            {
                if (!failureMode) { Fail("offering-failed-in-success-scenario"); return; }
                sawFailure = true; Log("FAILURE_CONFIRMED"); Enter(Role == CoreLoopRole.Captain ? Stage.ResetRun : Stage.AwaitResult); return;
            }
            // The root integration adds Success to ExpeditionPhase; use its stable name while this worktree targets the earlier contract.
            if (phase.ToString() == "Success")
            { if (failureMode) { Fail("unexpected-success-in-failure-scenario"); return; } sawSuccess = true; Log("SUCCESS_CONFIRMED"); Enter(Stage.AwaitResult); return; }
            Enter(Stage.WaitPhase);
        }
        private void StartDay()
        {
            if (expedition.State.Phase != ExpeditionPhase.Base) return;
            if (Role == CoreLoopRole.Hunter) { Park(); return; }
            if (AtTerminal(ExpeditionTerminal) && expedition.CanUseTerminal(diver, true) && RequestReady())
            { Log($"START_REQUEST nextDay={completedDays + 1}"); expeditionActor.StartExpedition(); }
        }
        private void BeginFishing(bool helper)
        {
            StopDelegates(); fishingBot.BeginAutomation(helper, true, true); delegated = DelegateActor.Fishing;
            Enter(helper ? Stage.HelpHunter : Stage.Hunt);
        }
        private void HaulMaterials()
        {
            if (TechnicalMaterialsReady() && !cargoActor.HasLoot)
            { Log("MATERIALS_DELIVERED stocksPlusProtected=" + ProjectedSummary()); BeginFishing(true); return; }
            if (station.ProtectedHold.Count >= haulTargetCount && station.State.State == WinchState.Deployed && !cargoActor.HasLoot)
            {
                if (NetworkLoot.All.Count == 0) { Fail("no-remaining-technical-loot-for-configured-cost"); return; }
                StopDelegates(); haulTargetCount = station.ProtectedHold.Count + 1;
                cargoBot.BeginAutomation(false, 1); delegated = DelegateActor.Cargo; Log("EXTRA_TECHNICAL_HAUL");
            }
        }
        private bool TechnicalMaterialsReady()
        {
            var needed = CombinedFirstPurchaseCost();
            foreach (var resource in progression.Settings.Resources)
                if ((resource.PerScrap > 0 || resource.PerElectronics > 0) && needed.TryGetValue(resource.Id, out long amount)
                    && Projected(resource) < amount) return false;
            return true;
        }
        private Dictionary<string, long> CombinedFirstPurchaseCost()
        {
            var result = new Dictionary<string, long>();
            AddCost(result, progression.UpgradeCost(CrewUpgrade.Oxygen)); AddCost(result, progression.Settings.SonarCost); return result;
        }
        private static void AddCost(Dictionary<string, long> result, ResourceAmount[] cost)
        {
            if (cost == null) return;
            foreach (var row in cost) { result.TryGetValue(row.Id, out long value); result[row.Id] = value + row.Amount; }
        }
        private long Projected(ResourceDefinition resource)
        { long value = Stock(resource.Id); foreach (var item in station.ProtectedHold) if (!item.Reserved) value += resource.Yield(item.Yield); return value; }
        private void BeginReturn() { StopDelegates(); Enter(Stage.ReturnAboard); }
        private void ReturnAboard()
        {
            if (expedition.State.Phase != ExpeditionPhase.Expedition) return;
            if (Role == CoreLoopRole.Hunter) { Park(); return; }
            if (!failureMode && startedDays == 1)
            {
                if (FoodValue() < initialQuota) { Fail("missing-first-day-fish"); return; }
                foreach (var pair in CombinedFirstPurchaseCost())
                {
                    long available = Stock(pair.Key);
                    foreach (var resource in progression.Settings.Resources) if (resource.Id == pair.Key) available = Projected(resource);
                    if (available < pair.Value) { Fail("insufficient-real-cargo-for-" + pair.Key); return; }
                }
            }
            if (AtTerminal(ExpeditionTerminal) && Time.unscaledTime - stageAt > 4 && expedition.CanUseTerminal(diver, true) && RequestReady())
            { Log($"RETURN_REQUEST day={startedDays} protected={station.ProtectedHold.Count}"); expeditionActor.RequestReturn(); }
        }
        private void ProcessCargo()
        {
            if (!AtTerminal(ProcessTerminal)) return;
            if (pendingProcess != 0)
            {
                bool exists = false; foreach (var item in station.ProtectedHold) if (item.Id == pendingProcess) exists = true;
                bool credited = true; foreach (var pair in expectedStocks) if (Stock(pair.Key) != pair.Value) credited = false;
                if (!exists && credited)
                {
                    if (!processedIds.Add(pendingProcess)) { Fail("duplicate-processed-id"); return; }
                    processedCount++; Log($"PROCESS_CONFIRMED id={pendingProcess} stocks={StockSummary()}"); pendingProcess = 0; Capture("processed-" + processedCount);
                }
                else if (Time.unscaledTime - pendingAt > 8) Fail("process-not-confirmed-id-" + pendingProcess);
                return;
            }
            if (station.ProtectedHold.Count == 0) { if (processedCount < 2) Fail("missing-real-fish-and-technical-processing"); else Enter(Stage.BuyOxygen); return; }
            if (!progression.CanUse(progressionActor, ProgressionTerminalKind.Processing, true) || !RequestReady()) return;
            CargoItem selected = station.ProtectedHold[0];
            if (selected.Reserved) { Fail("unexpected-reserved-day-one-cargo"); return; }
            expectedStocks.Clear();
            foreach (var resource in progression.Settings.Resources) expectedStocks[resource.Id] = checked(Stock(resource.Id) + (int)resource.Yield(selected.Yield));
            pendingProcess = selected.Id; pendingAt = Time.unscaledTime;
            Log($"PROCESS_REQUEST id={selected.Id} fish={selected.IsFish} expected={DictionarySummary(expectedStocks)}");
            progressionActor.Process(selected.Id);
        }
        private void BuyOxygen()
        {
            if (buySent && progression.Level(CrewUpgrade.Oxygen) == 1 && ExpectedStocksConfirmed())
            { Log($"UPGRADE_CONFIRMED level={progression.Level(CrewUpgrade.Oxygen)} multiplier={Number(progression.Effects.OxygenMultiplier)} stocks={StockSummary()}"); Capture("oxygen-upgrade"); Enter(Stage.CraftSonar); return; }
            if (buySent) { if (Time.unscaledTime - pendingAt > 8) Fail("oxygen-purchase-or-cost-not-confirmed"); return; }
            if (!AtTerminal(WorkshopTerminal) || !progression.CanUse(progressionActor, ProgressionTerminalKind.Workshop, true) || !RequestReady()) return;
            var cost = progression.UpgradeCost(CrewUpgrade.Oxygen);
            if (!CanAfford(cost)) { Fail("insufficient-oxygen-materials"); return; }
            ExpectedAfterSpend(cost); buySent = true; pendingAt = Time.unscaledTime;
            Log("BUY_REQUEST upgrade=Oxygen expectedLevel=0 revision=" + progression.State.Revision + " cost=" + CostSummary(cost));
            progressionActor.Buy(CrewUpgrade.Oxygen, 0, progression.State.Revision);
        }
        private void CraftSonar()
        {
            int charges = progression.Charges(diver.OwnerId);
            if (craftSent && charges == 1 && ExpectedStocksConfirmed()) { sonarCrafted = true; Log($"SONAR_CRAFT_CONFIRMED charges={charges} stocks={StockSummary()}"); Capture("sonar-crafted"); Enter(Stage.StartDay); return; }
            if (craftSent) { if (Time.unscaledTime - pendingAt > 8) Fail("sonar-craft-or-cost-not-confirmed"); return; }
            if (!AtTerminal(WorkshopTerminal) || !progression.CanUse(progressionActor, ProgressionTerminalKind.Workshop, true) || !RequestReady()) return;
            if (!CanAfford(progression.Settings.SonarCost)) { Fail("insufficient-sonar-materials"); return; }
            ExpectedAfterSpend(progression.Settings.SonarCost); craftSent = true; pendingAt = Time.unscaledTime;
            Log("SONAR_CRAFT_REQUEST revision=" + progression.State.Revision + " cost=" + CostSummary(progression.Settings.SonarCost));
            progressionActor.CraftSonar(charges, progression.State.Revision);
        }
        private void ObserveGearAndSonar()
        {
            if (failureMode) return;
            int captainId = CaptainId();
            if (captainId >= 0 && progression.Charges(captainId) > 0) sawReplicatedCharge = true;
            if (startedDays == 2 && !gearConfirmed && diver.State.Diving.Oxygen > baselineOxygen + 1)
            { gearConfirmed = true; Log($"GEAR_CONFIRMED oxygen={Number(diver.State.Diving.Oxygen)} baseline={Number(baselineOxygen)} level={progression.Level(CrewUpgrade.Oxygen)}"); }
            if (Role != CoreLoopRole.Captain || startedDays != 2 || expedition.State.Phase != ExpeditionPhase.Expedition || sonarUsed) return;
            int charges = progression.Charges(diver.OwnerId);
            if (sonarCrafted && charges == 0 && progressionActor.Feedback.StartsWith("Сонар: ", StringComparison.Ordinal))
            {
                string number = progressionActor.Feedback.Substring(7).Split(' ')[0];
                if (!int.TryParse(number, out int contacts) || contacts < 1) { Fail("sonar-did-not-reveal-any-contact"); return; }
                sonarUsed = true; Log($"SONAR_CONFIRMED contacts={contacts} charges=0"); Capture("sonar-pulse"); return;
            }
            if (charges <= 0 || diver.State.Sheltered || !NearbyContact() || !RequestReady()) return;
            progressionActor.UseSonar(charges); Log("SONAR_REQUEST charges=" + charges);
        }
        private bool NearbyContact()
        {
            float range = progression.Settings.SonarRange * .9f;
            foreach (var fish in NetworkFish.All) if (fish.State.HolderId < 0 && Vector3.Distance(fish.State.Position, diver.State.Position) < range) return true;
            foreach (var loot in NetworkLoot.All) if (loot.State.HolderId < 0 && Vector3.Distance(loot.State.Position, diver.State.Position) < range) return true;
            return false;
        }
        private void ReserveFood()
        {
            if (ProgressionModel.ReservedValue(station.ProtectedHold) >= progression.State.Quota)
            { Log($"RESERVE_CONFIRMED value={ProgressionModel.ReservedValue(station.ProtectedHold)} quota={progression.State.Quota}"); Capture("food-reserved"); Enter(Stage.StartDay); return; }
            if (!AtTerminal(OfferingTerminal) || !progression.CanUse(progressionActor, ProgressionTerminalKind.Offering, true) || !RequestReady()) return;
            foreach (var item in station.ProtectedHold) if (item.IsFish && !item.Reserved)
            { Log($"RESERVE_REQUEST id={item.Id} value={item.OfferingValue}"); progressionActor.Reserve(item.Id, true); return; }
            Fail("insufficient-day-two-food-for-quota");
        }
        private void SubmitOffering()
        {
            if (!AtTerminal(OfferingTerminal) || !progression.CanUse(progressionActor, ProgressionTerminalKind.Offering, true) || !RequestReady()) return;
            if (ProgressionModel.ReservedValue(station.ProtectedHold) < progression.State.Quota) { Fail("reserved-quota-missing-at-offering"); return; }
            Log($"OFFERING_SUBMIT revision={progression.State.Revision} quota={progression.State.Quota} reserved={ProgressionModel.ReservedValue(station.ProtectedHold)}");
            progressionActor.SubmitOffering(progression.State.Revision);
        }
        private void AwaitResult()
        {
            Park();
            if (failureMode && expedition.State.Phase == ExpeditionPhase.Offering && ProgressionModel.ReservedValue(station.ProtectedHold) != 0) Fail("failure-mode-unexpected-food");
        }
        private void ResetRun()
        {
            if (AtTerminal(ExpeditionTerminal) && expedition.CanUseTerminal(diver, true) && RequestReady())
            { Log("RESET_REQUEST"); expeditionActor.ResetRun(); }
        }
        private void VerifyFinished()
        {
            if (startedDays != 3 || completedDays != 3) { Fail("incomplete-three-day-history"); return; }
            if (failureMode)
            {
                if (!sawFailure || !StocksEmpty() || station.ProtectedHold.Count != 0 || station.Container.Count != 0
                    || progression.Level(CrewUpgrade.Oxygen) != 0 || progression.Charges(CaptainId()) != 0
                    || progression.State.Quota != initialQuota || progression.State.LastOffering != OfferingResult.None)
                { WaitForFinalSnapshot("reset-did-not-clear-run"); return; }
                Pass("failure-reset-confirmed"); return;
            }
            int expectedQuota = (int)Math.Min(int.MaxValue, Math.Max((long)initialQuota + 1, Math.Ceiling(initialQuota * (double)progression.Settings.QuotaGrowth)));
            if (!sawSuccess || progression.State.LastOffering != OfferingResult.Success || progression.State.SuccessfulOfferings != 1
                || progression.State.Quota != expectedQuota || progression.Level(CrewUpgrade.Oxygen) != 1 || !gearConfirmed
                || !sawReplicatedCharge || progression.Charges(CaptainId()) != 0 || (Role == CoreLoopRole.Captain && (!sonarUsed || processedCount < 2)))
            { WaitForFinalSnapshot("success-retention-or-personal-proof-missing"); return; }
            foreach (var pair in retainedStocks) if (Stock(pair.Key) != pair.Value) { Fail("offering-changed-retained-stock-" + pair.Key); return; }
            Pass("success-retained-upgrade-and-grown-quota");
        }
        private void WaitForFinalSnapshot(string reason)
        {
            if (stage != Stage.AwaitResult) Enter(Stage.AwaitResult);
            else if (Time.unscaledTime - stageAt > 15) Fail(reason);
        }

        private bool AtTerminal(Vector3 point)
        {
            bool arrived = Navigate(new Vector3(point.x, -7, point.z - 2.1f), point);
            if (arrived && captureDirectory != null && !progressionActor.PanelOpen && point != ExpeditionTerminal) progressionActor.TryInteract();
            return arrived;
        }
        private void Park()
        {
            if (delegated != DelegateActor.None) return;
            Navigate(Role == CoreLoopRole.Hunter ? HunterParking : new Vector3(-4, -7, -4), ExpeditionTerminal);
            if (expedition.State.Phase == ExpeditionPhase.Base && (sawFailure || sawSuccess)) VerifyFinished();
        }
        private bool Navigate(Vector3 target, Vector3 look)
        {
            if (delegated != DelegateActor.None) throw new InvalidOperationException("Core driver cannot move while a delegate owns input.");
            if (!routeSet || (routeGoal - target).sqrMagnitude > .01f)
            {
                routeSet = true; routeGoal = target; route.Clear(); routeIndex = 0; arrivedAt = -1;
                Vector3 position = diver.State.Position;
                if (!SubmarineInterior.Contains(position))
                {
                    // Reach the external hatch at the current depth before ascending; never rise into the underside of the deck.
                    route.Add(new Vector3(11, position.y, 0)); route.Add(new Vector3(11, -7, 0)); route.Add(new Vector3(7, -7, 0));
                }
                else route.Add(new Vector3(position.x, -7, 0));
                route.Add(new Vector3(target.x, -7, 0)); route.Add(target);
            }
            while (routeIndex < route.Count && Vector3.Distance(diver.State.Position, route[routeIndex]) < .5f) routeIndex++;
            if (routeIndex < route.Count)
            {
                Vector3 point = route[routeIndex];
                Move(Vector3.ClampMagnitude((point - diver.State.Position) * (1.3f / Mathf.Max(.1f, diver.Balance.SwimSpeed)), 1), routeIndex == route.Count - 1 ? look : point);
                arrivedAt = -1; return false;
            }
            Move(Vector3.ClampMagnitude((target - diver.State.Position) * (.8f / Mathf.Max(.1f, diver.Balance.SwimSpeed)), .15f), look);
            if (arrivedAt < 0) arrivedAt = Time.unscaledTime;
            return Time.unscaledTime - arrivedAt >= .6f;
        }
        private void Move(Vector3 world, Vector3 look)
        {
            Vector3 delta = look - diver.AimOrigin;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -85, 85);
            diver.SetAutomationInput(Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * world, yaw, pitch, false);
        }
        private void StopDelegates()
        {
            if (delegated == DelegateActor.Fishing) fishingBot.StopAutomation();
            if (delegated == DelegateActor.Cargo) cargoBot.StopAutomation();
            delegated = DelegateActor.None;
        }
        private void Enter(Stage next)
        { stage = next; stageAt = Time.unscaledTime; routeSet = false; arrivedAt = -1; Log("STAGE value=" + next); }
        private bool RequestReady()
        { if (Time.unscaledTime < nextRequest) return false; nextRequest = Time.unscaledTime + 1; return true; }
        private int FoodValue()
        { int value = 0; foreach (var item in station.ProtectedHold) if (item.IsFish) value += item.OfferingValue; return value; }
        private int CaptainId()
        { if (Role == CoreLoopRole.Captain) return diver.OwnerId; foreach (var other in NetworkDiver.All) if (other != diver) return other.OwnerId; return -1; }
        private int Stock(string id) { foreach (var row in progression.Stocks) if (row.Id == id) return row.Amount; return 0; }
        private bool StocksEmpty() { foreach (var row in progression.Stocks) if (row.Amount != 0) return false; return true; }
        private bool CanAfford(ResourceAmount[] cost)
        { if (cost == null) return false; var totals = new Dictionary<string, long>(); AddCost(totals, cost); foreach (var pair in totals) if (Stock(pair.Key) < pair.Value) return false; return true; }
        private void ExpectedAfterSpend(ResourceAmount[] cost)
        {
            expectedStocks.Clear(); foreach (var row in progression.Stocks) expectedStocks.Add(row.Id, row.Amount);
            foreach (var row in cost) expectedStocks[row.Id] -= row.Amount;
        }
        private bool ExpectedStocksConfirmed()
        { foreach (var pair in expectedStocks) if (Stock(pair.Key) != pair.Value) return false; return true; }
        private string StockSummary() { var text = new StringBuilder(); foreach (var row in progression.Stocks) text.Append(row.Id).Append(':').Append(row.Amount).Append(','); return text.ToString(); }
        private string ProjectedSummary() { var text = new StringBuilder(); foreach (var row in progression.Settings.Resources) text.Append(row.Id).Append(':').Append(Projected(row)).Append(','); return text.ToString(); }
        private static string DictionarySummary(Dictionary<string, int> values) { var text = new StringBuilder(); foreach (var pair in values) text.Append(pair.Key).Append(':').Append(pair.Value).Append(','); return text.ToString(); }
        private static string CostSummary(ResourceAmount[] cost) { var text = new StringBuilder(); if (cost != null) foreach (var row in cost) text.Append(row.Id).Append(':').Append(row.Amount).Append(','); return text.ToString(); }
        private static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private void Log(string message) { UnityEngine.Debug.Log("RAID_CORE_" + message + " role=" + Role); }
        private void Fail(string reason)
        {
            StopDelegates(); progressionActor?.Close(); Move(Vector3.zero, diver.AimOrigin + Vector3.forward);
            UnityEngine.Debug.LogError($"RAID_CORE_LOOP_FAIL role={Role} stage={stage} reason={reason}"); stage = Stage.Finished;
        }
        private void Pass(string reason)
        {
            StopDelegates(); progressionActor.Close(); Move(Vector3.zero, ExpeditionTerminal); Capture("complete");
            UnityEngine.Debug.Log($"RAID_CORE_LOOP_PASS role={Role} mode={(failureMode ? "failure" : "success")} days={completedDays} quota={progression.State.Quota} oxygenLevel={progression.Level(CrewUpgrade.Oxygen)} sonarCharges={progression.Charges(CaptainId())} stocks={StockSummary()} reason={reason} steamVerified=false");
            stage = Stage.Finished;
        }
        private void Capture(string name)
        { if (captureDirectory != null) StartCoroutine(CaptureFrame(name)); }
        private IEnumerator CaptureFrame(string name)
        {
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory(captureDirectory);
            string path = System.IO.Path.Combine(captureDirectory, Role.ToString().ToLowerInvariant() + "-" + name + ".png");
            ScreenCapture.CaptureScreenshot(path); Log("CAPTURE path=" + path);
        }
    }
}
