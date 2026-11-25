#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;

namespace Raid
{
    /// <summary>Explicit editor-host verification against an independent development player using its local UI probe.</summary>
    public sealed partial class FishingTrial : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string phase, status, error;
            public int host, client;
            public List<string> checks = new();
        }

        private readonly Result result = new();
        private string clientDir, reportPath;
        private RaidWorld world;
        private HarpoonTool host, helper;
        private float nextRemoteReel;
        private bool remoteReeling;
        private FishActor fishToFollow;

        private void LateUpdate()
        {
            if (fishToFollow && host && host.HookedFish == fishToFollow)
            {
                var target = fishToFollow.transform.position;
                host.Motor.QaSwimToward(target, Vector3.Distance(host.FishingAnchor, target) > 6f ? .35f : 0);
            }
        }
        // RAID-018: fight phase sequences per landed fish (host) and what the independent client observed.
        private readonly List<string> fightSequences = new();
        private readonly HashSet<string> peerBehaviours = new();
        private List<FishBehaviour> currentFight;
        private ThreatDirector pausedThreats;
        private float peakEyeOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int peer = Array.IndexOf(args, "-raid-fishing-peer"), report = Array.IndexOf(args, "-raid-fishing-report");
            if (peer < 0 || report < 0 || peer+1 >= args.Length || report+1 >= args.Length) return;
            var trial = new GameObject("Two-peer fishing verification").AddComponent<FishingTrial>();
            trial.clientDir = Path.GetFullPath(args[peer+1]);
            trial.reportPath = Path.GetFullPath(args[report+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(trial.reportPath));
            trial.StartCoroutine(trial.Verify());
        }

        public static void Begin(string remoteProbeDirectory, string resultPath)
        {
            if (!Application.isPlaying || FindAnyObjectByType<FishingTrial>() != null)
                throw new InvalidOperationException("Begin the trial once on a running editor host.");
            if (!Directory.Exists(remoteProbeDirectory)) throw new DirectoryNotFoundException(remoteProbeDirectory);
            var trial = new GameObject("Two-peer fishing verification").AddComponent<FishingTrial>();
            trial.clientDir = Path.GetFullPath(remoteProbeDirectory);
            trial.reportPath = Path.GetFullPath(resultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(trial.reportPath));
            trial.StartCoroutine(trial.Verify());
        }

        private IEnumerator Verify()
        {
            // Flatten nested waits so failures in a sub-step also reach the reproducible report.
            var tasks = new Stack<IEnumerator>();
            tasks.Push(Run());
            result.status = "running";
            while (tasks.Count > 0)
            {
                object instruction = null;
                try
                {
                    var task = tasks.Peek();
                    if (!task.MoveNext()) { tasks.Pop(); continue; }
                    if (task.Current is IEnumerator child) { tasks.Push(child); continue; }
                    instruction = task.Current;
                }
                catch (Exception error)
                {
                    result.status = "failed";
                    result.error = error.ToString();
                    break;
                }
                yield return instruction;
            }
            if (result.status == "running") result.status = "passed";
            if (host != null)
            {
                host.QaHoldOwned(false, 0);
                host.Motor.QaSwimToward(default, 0);
            }
            if (helper != null && remoteReeling)
            {
                try { Send("fish_reel", 0); } catch (IOException) { }
            }
            RestoreThreats();
            Write();
            Debug.Log($"[FishingTrial] {result.status} / {result.phase}: {result.error} ({reportPath})");
            Destroy(gameObject);
        }

        private IEnumerator Run()
        {
            Phase("Connect host and independent client");
            yield return Wait(() => UiRoot.Instance?.Context.Player != null && RaidWorld.Instance != null
                && RaidWorld.Instance.NetworkObject != null && RaidWorld.Instance.IsServerInitialized
                && DiverMotor.ServerDivers.Count(d=>d!=null && d.IsServerInitialized)==2 && !UiInputGate.Blocked
                && File.Exists(Path.Combine(clientDir,"state.json")),120,"Two independent connected peers did not arrive.");
            world = RaidWorld.Instance;
            Require(world != null && world.IsServerInitialized && world.IsClientInitialized, "Editor must be a network host.");
            var crew = DiverMotor.ServerDivers.Where(d => d != null && d.IsServerInitialized).ToArray();
            Require(crew.Length == 2, "Exactly two spawned divers required.");
            host = crew.Single(d => d.IsOwner).GetComponent<HarpoonTool>();
            helper = crew.Single(d => !d.IsOwner).GetComponent<HarpoonTool>();
            result.host = host.OwnerId;
            result.client = helper.OwnerId;
            var hello = Send("snapshot", 0);
            yield return Wait(() =>
            {
                var peer = ReadClient();
                return peer != null && peer.request == hello && peer.connected && peer.owner == helper.OwnerId
                    && peer.players == 2 && !peer.inputBlocked;
            }, 15, "Remote did not enter gameplay as the second owner.");
            Require(!UiInputGate.Blocked && !host.Motor.IsHelpless && !helper.Motor.IsHelpless, "Both peers must be alive with gameplay controls.");
            Require(host.CatchCount == 0 && host.HookedFish == null && !host.TetherActive, "Host must start with free hands and empty catch pockets.");
            Check("Distinct connected owners " + host.OwnerId + " and " + helper.OwnerId);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-raid-ball-harpoon") >= 0)
            { yield return RunBallHarpoon(); yield break; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-reservoir-only")>=0)
            {
                var staged=new[]
                {
                    new CatchItem(18001,4,0,2,1,1,species:"Needlefish"),
                    new CatchItem(18002,3,1,2,1,1,species:"Lanternfish"),
                    new CatchItem(18003,5,1,3,1,1,species:"Bristlefish"),
                    new CatchItem(18004,6,1,3,2,1,1,0,"Sailray")
                };
                foreach(var item in staged)Require(world.Team.Deposit(item),"Reservoir test requires a fresh save.");
                yield return Wait(()=>staged.All(item=>world.View.tank.Exists(fish=>fish.id==item.id)),4,"Staged catches were not published.");
                yield return ChooseReservoirRoutes(staged.Select(item=>item.id).ToArray(),true);
                yield break;
            }

            if (world.CurrentDepth != 1)
            {
                Phase("Choose shallow expedition at chart console");
                var station = ShipConsole.Stations.First(s => s.kind == ShipConsoleKind.Depths);
                helper.Motor.Teleport(new Vector3(1, 1.2f, 3));
                bool aligned = false;
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    host.Motor.Teleport(station.transform.position + side * 1.45f + Vector3.up*.5f);
                    Physics.SyncTransforms();
                    if (host.Motor.IsInside && ShipConsole.CanUse(host.Motor, WorldAction.DiveOne)) { aligned = true; break; }
                }
                Require(aligned && helper.Motor.IsInside, "Could not reach the chart console from its dry interior.");
                world.Command(WorldAction.DiveOne, 0);
                yield return Wait(() => world.CurrentDepth == 1, 12, "Dive command refused: " + world.Message);
            }
            Require(world.CurrentDepth == 1 && world.Team.hoistPhase == HoistPhase.Loading, "Shallow depth and available winch required.");
            Require(world.Team.Capacity(world.progression) - world.Team.CargoUsed >= 6, "The verified catches occupy six cage units.");
            // Predator encounters have their own trial. A bite can legitimately remove the exact fish being audited.
            if(world.threats!=null && world.threats.enabled)
            {
                pausedThreats=world.threats;
                pausedThreats.enabled=false;
                Check("Isolated catch/hoist trial: reef hunter AI paused while the four catch identities are tracked.");
            }
            if (VerifyDropTransfers) yield return SalvageHandoff();
            host.SelectSlot(0);
            yield return Wait(() => host.ActiveSlot == 0, 3, "Host harpoon could not be equipped.");

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-raid-tug-only") >= 0)
            {
                yield return RefillOnDeck();
                var fish = Fish("Bristlefish");
                yield return Wait(() => fish.InHabitat && fish.Mode == FishMode.Roam, 65, "Heavy school did not arrive.");
                var settings=UiRoot.Instance.Settings;
                settings.fishingCamera=settings.cameraShake=settings.hitImpulse=1;
                foreach(var impulse in FindObjectsByType<Nereid.NereidFeedingCameraImpulse>())
                    impulse.BeginBackgroundQa(30);
                yield return Hook(fish);
                yield return VerifySoloTow(fish);
                yield return VerifyViewAfterLineBreak(fish);
                yield break;
            }

            foreach (var species in new[] { "Needlefish", "Lanternfish", "Needlefish" })
            {
                Phase("Solo capture / " + species);
                yield return RefillOnDeck();
                var fish = Fish(species);
                yield return Wait(() => fish.InHabitat && fish.Mode == FishMode.Roam, 65, "School did not repopulate " + species + " while the first catch was stowed.");
                bool closeShot=host.CatchCount==1;
                int occupied=host.CatchCount;
                yield return Hook(fish,closeShot);
                if(closeShot)
                {
                    Require(host.LineLength<=host.tuning.captureRange,"Fixture failed to place the impact inside landing radius.");
                    yield return Pause(host.tuning.hookSettleSeconds+host.tuning.minimumWindingSeconds);
                    Require(host.HookedFish==fish && host.CatchCount==occupied,
                        "Close shot auto-collected without any reel input.");
                    Check("Inside the hand radius a confirmed shot alone did not collect the fish.");
                }
                if(host.CatchCount==0)
                {
                    var stand=host.transform.position;
                    float length=host.LineLength;
                    host.Motor.Teleport(fish.transform.position-host.Motor.AimDirection*.5f-Vector3.up*.6f);
                    yield return Pause(.65f);
                    Require(host.CatchCount==0 && host.HookedFish==fish && host.LineLength>=length-.01f,
                        "Swimming to the impact wound the spool or delivered cargo.");
                    host.Motor.Teleport(stand);
                    Check("Approach with a slack line and no reel did not wind or collect the fish.");
                }
                yield return Reel(fish);
                Require(!host.HandsFull && host.HookedFish == null && host.CarriedFish != null,
                    "Small catch was not transferred independently of the harpoon.");
                if(host.CatchCount==1)
                {
                    Require(fish.Stamina>0,"First catch unexpectedly required total exhaustion.");
                    Check("First fish reached the hand with "+fish.Stamina.ToString("F2")+" stamina remaining.");
                }
                Check(species + " captured and stowed by one diver.");
            }
            RequireFightVariety();
            Require(host.FishInSlot(2)?.SpeciesId == "Needlefish" && host.FishInSlot(3)?.SpeciesId == "Lanternfish"
                && host.FishInSlot(4)?.SpeciesId == "Needlefish" && host.HasFullCreel && host.CatchIdInSlot(2) != host.CatchIdInSlot(4),
                "All three pockets must preserve independent identities, including two catches from the same shoal.");
            host.SelectSlot(2);
            yield return Wait(() => host.ActiveSlot == 2, 3, "Occupied pocket was not selectable.");
            Require(!host.QaFireOwned(), "Occupied pocket incorrectly fired the harpoon.");
            yield return Pause(.4f);
            yield return Shot("catch-pockets.png");
            host.SelectSlot(0);
            yield return Wait(() => host.ActiveSlot == 0, 3, "Could not return to the harpoon.");
            Check("Three loaded pockets include two distinct catches from one repopulating shoal; selecting a fish suppresses harpoon fire.");

            var camera = host.Motor.GetComponentInChildren<Camera>();
            float pref = UiRoot.Instance.Settings.fishingCamera;
            Phase("Full creel rejects a fourth small catch");
            var spare=FindObjectsByType<FishActor>().FirstOrDefault(f=>f.InHabitat && f.Mode==FishMode.Roam && !f.RequiresPartner);
            if(spare==null)
            {
                spare=Fish("Lanternfish");
                yield return Wait(()=>spare.InHabitat && spare.Mode==FishMode.Roam,65,"No small fish to test full pockets.");
            }
            yield return SightWildCatch(spare, false);
            int shots=host.ShotSequence;
            Require(host.QaFireOwned(),"Full creel host could not fire equipped harpoon.");
            yield return Wait(()=>host.ShotSequence>shots,3,"Server did not receive full-creel shot.");
            Require(host.HookedFish==null && spare.HookCount==0 && host.CatchCount==3,"Fourth small fish was attached with full pockets.");
            Check("Server accepted the shot on a visible fourth small fish and retained exactly three pockets.");
            Phase("Heavy solo attempt");
            yield return RefillOnDeck();
            var heavy = Fish("Bristlefish");
            yield return Wait(()=>heavy.InHabitat && heavy.Mode==FishMode.Roam,65,"Heavy school did not arrive.");
            yield return Hook(heavy);
            yield return Wait(() => heavy.Mode == FishMode.Struggling, 4, "Heavy fish did not enter its struggle.");
            var ui=UiRoot.Instance;
            ui.Settings.fishingCamera=1;
            yield return Wait(()=>camera.fieldOfView>ui.Settings.fov+4,3,"Owned camera did not widen after the confirmed hit.");
            ui.Settings.fishingCamera=0;
            yield return Wait(()=>Mathf.Abs(camera.fieldOfView-ui.Settings.fov)<.3f,3,"Disabling fishing camera did not restore the configured FOV.");
            ui.Settings.fishingCamera=pref;
            Check("Confirmed hook widened the owned camera; zero intensity restored base FOV.");
            yield return VerifySoloTow(heavy);

            Phase("Two independent impacts");
            // The host can still swim while the other client reads the impacts and changes tools.
            fishToFollow = heavy;
            yield return HookPeer(heavy);
            yield return Wait(()=>ReadClient()?.activeBoltOwners?.Distinct().Count()==2,6,"Independent view did not render both attachments.");
            // Opposed handles stay loaded and aim away from one another.
            var axis=(host.FishingAnchor-heavy.transform.position).normalized;
            helper.Motor.Teleport(heavy.transform.position-axis*5f-Vector3.up*.6f);
            host.QaHoldOwned(false,1.5f);
            Send("fish_reel",1.5f);
            float startEnergy=heavy.Stamina;
            yield return Pause(.85f);
            Require(heavy.HookCount==2 && !heavy.Coordinated && heavy.SoloTow && heavy.Stamina>=startEnergy-.05f,
                "Opposed reels advanced the catch or escaped the uncoordinated tow state.");
            host.QaHoldOwned(false,0);
            Send("fish_reel",0);
            yield return Pause(.35f);
            yield return VerifyBothBolts();
            Check("Both rendered and attached independent server-confirmed projectiles; opposing directions did not exhaust the fish.");

            // Release the helper's line by switching tools, then reacquire it through another real shot.
            Send("slot",1);
            yield return Wait(()=>helper.ActiveSlot==1 && helper.HookedFish==null && heavy.HookCount==1 && host.HookedFish==heavy,
                4,"Switching the partner's tool failed to remove only their line.");
            // Reset the fixture's shooting lane before the second shot. Waiting at the previous
            // inspection point can put the stationary first shooter beyond the 20 m line range.
            Send("slot",0);yield return Wait(()=>helper.ActiveSlot==0,4,"Partner could not re-equip harpoon.");
            yield return VerifyGripAndFriendTether(heavy);
            yield return HookPeer(heavy);
            Require(host.HookedFish == heavy && heavy.HookCount == 2,
                "Both lines must still be attached before staging the cooperative pull.");
            Phase("Cooperative pull and heavy carry");
            fishToFollow = null;
            StageCooperativePull(heavy);
            remoteReeling=true;
            nextRemoteReel=0;
            yield return Reel(heavy);
            remoteReeling=false;
            Send("fish_reel",0);
            long landedHeavyId = heavy.CargoId;
            var carrier = host.CarriedFish == heavy ? host : helper;
            var otherDiver = carrier == host ? helper : host;
            Require(!carrier.Motor.IsDead && carrier.HandsFull && carrier.CarriedFish == heavy && host.CatchCount + helper.CatchCount == 4
                && otherDiver.HookedFish == null && host.LineLength == 0 && helper.LineLength == 0 && heavy.HookCount == 0,
                $"Heavy carry mismatch: mode {heavy.Mode}, hands {host.HandsFull}/{helper.HandsFull}, catches {host.CatchCount}/{helper.CatchCount}.");
            if (carrier == host)
                Require(!host.SelectSlot(1) && host.ActiveSlot == 0 && !host.QaFireOwned(), "Heavy catch allowed switching or firing.");
            else
            {
                yield return Wait(() => ReadClient()?.handsFull == true, 5, "Second owner did not observe the heavy catch in their hands.");
                var slotAttempt = Send("slot", 1);
                yield return Wait(() => ReadClient() is { } peer && peer.request == slotAttempt && !string.IsNullOrEmpty(peer.error),
                    5, "Heavy catch allowed the second owner to switch tools.");
                var fireAttempt = Send("fire", 0);
                yield return Wait(() => ReadClient() is { } peer && peer.request == fireAttempt && !string.IsNullOrEmpty(peer.error),
                    5, "Heavy catch allowed the second owner to fire.");
                helper.Motor.Teleport(HandoffWater);
                yield return Pause(.35f);
                Send("drop_selected", 0);
                yield return Wait(() => !helper.HandsFull && WorldHas(landedHeavyId), 5, "Second owner did not drop the original heavy catch.");
                helper.Motor.Teleport(HandoffWater + Vector3.right * 12);
                yield return TakeBack(landedHeavyId);
                Check("The second diver landed the heavy fish and transferred the original catch to the host for cage loading.");
            }
            yield return Pause(.5f);
            yield return Shot("catch-heavy.png");
            Check("Heavy capture completed with all three small catches intact; tool switch and fire blocked.");
            Require(peerBehaviours.Count >= 2, "Independent client did not observe replicated fight behaviours: " + string.Join(",", peerBehaviours));
            Check("Independent client observed replicated fight behaviours: " + string.Join(",", peerBehaviours) + ".");

            Vector3 fishingAim=host.Motor.AimDirection;
            if (VerifyDropTransfers) yield return CatchHandoffs(landedHeavyId);
            Phase("Deposit and observe swimming cargo on both peers");
            var control=world.container.GetComponentInChildren<CargoDispatch>();
            var cage=world.container.GetComponentInChildren<WetCargoShoal>();
            var center=control.Frame.position;
            int before=world.Team.cargo.Count,beforeSwimmers=cage==null?0:cage.Count;
            var caughtIds=new[]{host.CatchIdInSlot(2),host.CatchIdInSlot(3),host.CatchIdInSlot(4),landedHeavyId};
            // Try every open face in turn: a roaming fish between the eye and the basket
            // must not stall the fixture, and the deposit itself is still the normal secondary action.
            string loadDiagnostics="";
            foreach(var offset in new[]{new Vector3(2.14f,-.18f,-.4f),new Vector3(2.14f,-.18f,-.92f),new Vector3(1.12f,-.30f,-.35f)})
            {
                host.Motor.Teleport(control.Frame.TransformPoint(offset));
                yield return Pause(.18f);
                float attempt=Time.unscaledTime+.9f;
                do
                {
                    host.Motor.LookAtConsole(center);
                    yield return null;
                } while(!CargoDispatch.CanLoad(world,host.Motor) && Time.unscaledTime<attempt);
                if(CargoDispatch.CanLoad(world,host.Motor)) break;
                var eye=host.Motor.transform.position+Vector3.up*.6f;
                string obstruction=Physics.Raycast(eye,host.Motor.AimDirection,out var obstructionHit,1.6f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?obstructionHit.collider.name:"open water";
                loadDiagnostics+=$" {control.Frame.InverseTransformPoint(eye)} insideShip={host.Motor.IsInside} {obstruction};";
            }
            Require(CargoDispatch.CanLoad(world,host.Motor),"Cannot use the loading opening."+loadDiagnostics);
            Require(host.QaPrimaryOwned(),"Owned LMB refused the heavy catch.");
            yield return Wait(()=>!host.HandsFull && world.Team.cargo.Count==before+1,4,"LMB did not deposit the heavy catch.");
            for(int slot=2;slot<5;slot++)
            {
                Require(host.SelectSlot(slot),"Cannot select the full pocket.");
                yield return Wait(()=>host.ActiveSlot==slot,3,"Pocket selection not confirmed.");
                Require(host.QaPrimaryOwned(),"Owned LMB refused a pocket catch.");
                int expected=before+slot;
                yield return Wait(()=>world.Team.cargo.Count==expected,5,"LMB did not deposit exactly one fish.");
            }
            Require(host.CatchCount==0 && caughtIds.All(id=>world.Team.cargo.Count(item=>item.id==id)==1),
                "LMB lost or duplicated a catch ID.");
            yield return Wait(() => cage != null && cage.Count == beforeSwimmers + 4 && ReadClient()?.cageSwimmers == cage.Count,
                10, "Host/client cage swimmers did not reflect four new entries.");
            yield return Shot("catch-cage.png");
            Check("Four authoritative catch IDs now swim in both cages; hands and pockets are empty.");

            Phase("Hoist cargo into reservoir");
            yield return Pause(.3f);
            var button=control.transform.position;
            string buttonDiagnostics="";
            foreach(var offset in new[]{new Vector3(1.05f,-.60f,0),new Vector3(.85f,-.60f,-.72f),new Vector3(.9f,-.05f,-.45f)})
            {
                host.Motor.Teleport(button+control.Frame.TransformDirection(offset));
                yield return Pause(.18f);
                float attempt=Time.unscaledTime+.90f;
                do
                {
                    host.Motor.LookAtConsole(button);
                    yield return null;
                } while(!CargoDispatch.CanUse(host.Motor) && Time.unscaledTime<attempt);
                if(CargoDispatch.CanUse(host.Motor))break;
                var eye=host.Motor.transform.position+Vector3.up*.6f;
                string obstruction=Physics.Raycast(eye,host.Motor.AimDirection,out var obstructionHit,2.65f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?obstructionHit.collider.name:"open water";
                buttonDiagnostics+=$" {control.Frame.InverseTransformPoint(eye)} insideShip={host.Motor.IsInside} {obstruction};";
            }
            Require(CargoDispatch.CanUse(host.Motor),"Actual dispatch button is not on the server sightline."+buttonDiagnostics);
            var walletBeforeHoist=world.Team.Treasury;
            world.Command(WorldAction.Hoist,0);
            yield return Wait(() => world.Team.hoistPhase != HoistPhase.Loading, 4, "Winch command refused.");
            yield return RefillOnDeck();
            yield return Wait(() => world.Team.hoistPhase == HoistPhase.Loading && caughtIds.All(id => world.Team.tank.Exists(item => item.id == id))
                && cage.Count == 0 && ClientReceivedDelivery(caughtIds), 45, "Host and client did not return to Loading with the same four delivered identities.");
            Check("Winch delivered each original ID to the reservoir and both cages emptied.");
            if(loadedSalvageId>0)
            {
                Require(world.Team.deliveredIds.Contains(loadedSalvageId) && !world.Team.tank.Exists(x=>x.id==loadedSalvageId) &&
                    world.Team.Treasury.Subtract(walletBeforeHoist).Equals(loadedSalvageYield),
                    "Mixed hoist added salvage to the aquarium or credited the wrong materials.");
                Check("Mixed hoist preserved all four fish and credited only the salvage to the shared wallet.");
            }
            Require(world.Team.tank.Find(item=>item.id==landedHeavyId)?.caughtBy?.Length==2,
                "Heavy catch lost one of its two confirmed participants.");
            Require(caughtIds.Take(3).All(id=>world.Team.tank.Find(item=>item.id==id)?.caughtBy?.Length==1),
                "A pocket catch lost its single owner.");
            Check("The exact LMB deposits retained all personal ownership across the real hoist.");
            yield return ChooseReservoirRoutes(caughtIds);
            ui.ClosePanel();
            yield return Wait(()=>Mathf.Abs(camera.fieldOfView-ui.Settings.fov)<.25f,3,"FOV did not restore after closing the aquarium monitor.");

            yield return Pause(.3f);
            yield return RearmAfterHandoffs();
            Phase("Owner death and reconnect with two active lines");
            var ownerCamera=host.Motor.GetComponentInChildren<Camera>();
            host.Motor.LookAtConsole(ownerCamera.transform.position+fishingAim*10);
            yield return Wait(()=>Vector3.Dot(host.Motor.AimDirection,fishingAim)>.999f,4,"Fishing aim did not restore after the monitor.");
            var departingKey=world.ProgressKey(helper.Motor);
            var balanceBeforeReconnect=world.Team.Treasury;
            var retainedCatchIds=world.Team.tank.Select(item=>item.id).OrderBy(id=>id).ToArray();
            yield return Wait(()=>heavy.InHabitat && heavy.Mode==FishMode.Roam,100,"Heavy shoal did not return.");
            yield return Hook(heavy);
            yield return HookPeer(heavy);
            // Exercise the normal death transition without forcing a diver to suffocate for minutes.
            helper.Motor.SendMessage("BeginDeath",false,SendMessageOptions.RequireReceiver);
            yield return Wait(()=>helper.Motor.IsDead && helper.HookedFish==null && host.HookedFish==heavy && heavy.HookCount==1,
                3,"Death left stale line ownership.");
            helper.Motor.Rescue();
            yield return Wait(()=>!helper.Motor.IsDead,3,"Real rescue did not restore helper.");
            yield return HookPeer(heavy);
            var previousPeer=helper;
            Send("rejoin_local",0);
            yield return Wait(()=>previousPeer==null && host.HookedFish==heavy && heavy.HookCount==1,8,"Disconnect left a second line.");
            yield return Wait(()=>DiverMotor.ServerDivers.Any(d=>d!=null && !d.IsOwner && d.IsServerInitialized),15,"Remote failed to reconnect.");
            helper=DiverMotor.ServerDivers.Single(d=>d!=null && !d.IsOwner && d.IsServerInitialized).GetComponent<HarpoonTool>();
            Require(helper.HookedFish==null && helper.CatchCount==0,
                "Reconnection restored another player's line or pockets.");
            Require(world.Team.tank.Select(item=>item.id).OrderBy(id=>id).SequenceEqual(retainedCatchIds) &&
                caughtIds.All(id=>world.Team.deliveredIds.Count(delivered=>delivered==id)==1),
                "Reconnection changed the fish left in the tank or duplicated a delivered catch.");
            yield return Wait(()=>world.ProgressKey(helper.Motor)==departingKey,4,"Remote did not restore its campaign identity.");
            Require(world.Team.Treasury.Equals(balanceBeforeReconnect),"The shared balance changed on reconnect.");
            host.QaReleaseOwned();
            yield return Wait(()=>heavy.HookCount==0 && host.HookedFish==null,3,"Last hook failed to release.");
            Check("Partner death and connection teardown each removed only its own harpoon. Rejoined owner had free hands, and all four delivered IDs remained unique. The same campaign profile returned and the shared wallet was unchanged.");
        }

        private IEnumerator RefillOnDeck()
        {
            // Same dry crew arrival deck as RaidWorld.CompleteTravel; leave O2 replenishment to the motor.
            host.Motor.Teleport(new Vector3(-3,1.2f,1));
            helper.Motor.Teleport(new Vector3(-1.5f,1.2f,1));
            Physics.SyncTransforms();
            float capacity(DiverMotor diver) => Mathf.Max(1,diver.tuning.oxygenCapacity*diver.OxygenMultiplier);
            float timeout(DiverMotor diver) => (capacity(diver)-diver.Oxygen)/Mathf.Max(.01f,diver.tuning.oxygenRecovery);
            bool ready(DiverMotor diver) => diver.IsInside && diver.Oxygen>=capacity(diver)-.05f;
            Require(!host.Motor.IsDead && !helper.Motor.IsDead && host.Motor.IsInside && helper.Motor.IsInside,
                "Both live crew must start the rest break on the dry arrival deck.");
            yield return Wait(()=>ready(host.Motor) && ready(helper.Motor),
                8+3*Mathf.Max(timeout(host.Motor),timeout(helper.Motor)), "Dry-deck oxygen recovery failed.");
            Check($"Both divers ready on the dry deck: host O2 {host.Motor.Oxygen:F1}, peer O2 {helper.Motor.Oxygen:F1}.");
        }

        private bool ClientReceivedDelivery(long[] ids)
        {
            var peer=ReadClient();
            if(peer==null || peer.cageSwimmers!=0 || string.IsNullOrEmpty(peer.team))return false;
            var manifest=JsonUtility.FromJson<TeamProgress>(peer.team);
            return manifest!=null && manifest.hoistPhase==HoistPhase.Loading
                && manifest.tank!=null && ids.All(id=>manifest.tank.Exists(item=>item.id==id));
        }

        private void RestoreThreats()
        {
            if(pausedThreats==null)return;
            pausedThreats.enabled=true;
            pausedThreats=null;
        }

        private void OnDestroy() => RestoreThreats();

        private FishActor Fish(string species) => FindObjectsByType<FishActor>().OrderBy(f=>f.ObjectId).First(f => f.depthTier == 1 && f.SpeciesId == species);

        private IEnumerator Hook(FishActor fish,bool closeShot=false)
        {
            yield return Pause(host.tuning.fireCooldown+.12f);
            Require(fish.Mode == FishMode.Roam && !host.Motor.IsHelpless, "Fish unavailable or host incapacitated.");
            yield return SightWildCatch(fish, closeShot);
            Require(host.QaFireOwned(), "Host owned fire request rejected.");
            yield return Wait(() => host.HookedFish == fish, 3, "Normal harpoon RPC missed " + fish.SpeciesId + ".");
        }

        // A fresh population can put the same fish on either side of an outcrop.
        // Place the QA diver on a clear approach and still fire the ordinary owned harpoon.
        private IEnumerator SightWildCatch(FishActor fish, bool closeShot)
        {
            var forward = Vector3.ProjectOnPlane(host.Motor.AimDirection, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            var camera = host.GetComponentInChildren<Camera>();
            var lift = Vector3.up * .6f;
            bool ClearSight(Vector3 eye, Vector3 direction, out RaycastHit hit) =>
                Physics.Raycast(eye, direction, out hit, host.tuning.maxRange, host.tuning.hitMask,
                    QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FishActor>() == fish;

            foreach (float incline in new[] { 0f, -.55f, .35f })
            foreach (float yaw in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                var direction = (Quaternion.AngleAxis(yaw, Vector3.up) * forward + Vector3.up * incline).normalized;
                var eye = fish.transform.position - direction * 3.8f;
                if (Physics.CheckSphere(eye - lift, .48f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || !ClearSight(eye, direction, out _)) continue;
                host.Motor.LookAtConsole(camera.transform.position + direction * 10f);
                yield return Wait(() => Vector3.Dot(host.Motor.AimDirection, direction) > .999f, 3, "Cannot align the owner on the approach.");
                direction = host.Motor.AimDirection;
                eye = fish.transform.position - direction * 3.8f;
                if (!ClearSight(eye, direction, out var impact)) continue;
                if (closeShot) eye = impact.point - direction * .65f;
                host.Motor.Teleport(eye - lift);
                Physics.SyncTransforms();
                if (ClearSight(eye, direction, out _)) yield break;
            }
            throw new InvalidOperationException("No visible firing position around " + fish.SpeciesId + " in " + fish.HabitatName);
        }

        private IEnumerator VerifySoloTow(FishActor fish)
        {
            Phase("Continuous solo tow without reel input");
            var camera = host.Motor.GetComponentInChildren<Camera>();
            Vector3 initialAim = camera.transform.forward;
            float initialStamina = fish.Stamina;
            int initialCatches = host.CatchCount;
            int initialTugs = fish.TugSequence;
            host.QaHoldOwned(false, 0);
            var feel = host.GetComponent<FishingFeel>();
            Require(feel != null && feel.AudioCueCount >= 2, "Confirmed shot and attachment did not produce distinct audio cues.");
            yield return Wait(() => fish.SoloTow && host.HookedFish == fish, 4, "Heavy fish did not enter continuous solo tow.");

            const int steps = 36;
            float forward = 0, backwards = 0, transverse = 0, escape = 0;
            var waitPhysics = new WaitForFixedUpdate();
            for (int step = 0; step < steps; step++)
            {
                Vector3 diverBefore = host.transform.position;
                Vector3 fishBefore = fish.transform.position;
                Vector3 towardFish = (fishBefore - host.FishingAnchor).normalized;
                var side = Vector3.Cross(towardFish, Vector3.up).normalized;
                var direction = Quaternion.AngleAxis(115f * (step+1)/steps, Vector3.up) * initialAim;
                host.Motor.LookAtConsole(camera.transform.position + direction * 10f);
                yield return waitPhysics;
                peakEyeOffset=Mathf.Max(peakEyeOffset,Vector3.Distance(camera.transform.position,host.FishingAnchor));
                Vector3 movement = host.transform.position - diverBefore;
                float advance = Vector3.Dot(movement, towardFish);
                forward += Mathf.Max(0, advance);
                backwards += Mathf.Max(0, -advance);
                transverse += Mathf.Abs(Vector3.Dot(movement, side));
                escape += Vector3.Dot(fish.transform.position - fishBefore, towardFish);
                Require(fish.SoloTow && !fish.Surging, "Solo tow incorrectly alternated with the ordinary surge state.");
            }
            yield return null;
            float yaw = Vector3.Angle(Vector3.ProjectOnPlane(initialAim, Vector3.up),
                Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up));
            string measured = $"camera {yaw:F0}°; toward fish {forward:F3} m; away {backwards:F3} m; sideways {transverse:F3} m; fish escape {escape:F3} m.";
            Require(yaw > 90f && forward > .18f && backwards < .035f && transverse < .065f && escape > .12f,
                "Continuous tow did not follow the expected pull axes: " + measured);
            Require(host.HookedFish==fish && fish.HookCount==1 && host.CatchCount==initialCatches &&
                fish.Stamina>=initialStamina-.05f && fish.TugSequence==initialTugs && host.LineTension<1,
                $"Solo tow changed catch/stamina/surge state: hooks={fish.HookCount}, catches={initialCatches}->{host.CatchCount}, "+
                $"stamina={initialStamina:F2}->{fish.Stamina:F2}, tugs={initialTugs}->{fish.TugSequence}, tension={host.LineTension:F2}.");
            Check("Continuous solo tow without LMB: " + measured);
            yield return Shot("catch-solo-pulled.png");
            host.Motor.LookAtConsole(camera.transform.position + initialAim * 10f);
            yield return Pause(.2f);
        }

        private IEnumerator VerifyGripAndFriendTether(FishActor fish)
        {
            Phase("Grip stabilization and friend tether rescue");
            StageCooperativePull(fish);
            host.QaHoldOwned(false, 0);
            yield return Pause(.15f);
            Vector3 before = host.transform.position;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            float unassisted = Vector3.Distance(before, host.transform.position);

            Require(helper.QaHoldOwned(true, 2), "Helper could not hold the active fisherman.");
            yield return Wait(() => helper.AssistTarget == host && host.IsAssisted, 3, "E grip did not attach to the active fisherman.");
            before = host.transform.position;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            float assisted = Vector3.Distance(before, host.transform.position);
            helper.QaHoldOwned(true, 0);
            yield return Wait(() => helper.AssistTarget == null && !host.IsAssisted, 3, "E grip did not detach.");
            Require(unassisted > .03f && assisted < unassisted * .85f,
                $"E grip did not reduce towing: unassisted={unassisted:F3}, assisted={assisted:F3}.");
            Require(fish.HookCount == 1 && host.HookedFish == fish && fish.SoloTow,
                "E grip incorrectly replaced the second harpoon or advanced the heavy catch.");
            Check($"E grip reduced towing from {unassisted:F3} m to {assisted:F3} m without replacing the second hook.");

            helper.Motor.LookAtConsole(host.FishingAnchor);
            yield return Wait(() => Vector3.Dot(helper.Motor.AimDirection,
                (host.FishingAnchor - helper.FishingAnchor).normalized) > .999f, 3, "Helper could not aim at the towed fisherman.");
            Require(helper.QaFireOwned(), "Friend tether shot was rejected.");
            yield return Wait(() => helper.OwnsFriendTether && host.IsPulled && host.HookedFish == fish, 3,
                "Friend tether did not attach without breaking the fishing line.");
            Require(!host.IsAssisted, "Grip and friend tether remained stacked.");
            float separation = Vector3.Distance(host.transform.position, helper.transform.position);
            helper.QaHoldOwned(false, 1.2f);
            yield return Pause(.65f);
            float rescuedSeparation = Vector3.Distance(host.transform.position, helper.transform.position);
            Require(rescuedSeparation < separation && fish.HookCount == 1 && host.HookedFish == fish,
                $"Friend tether did not counter towing: {separation:F3} -> {rescuedSeparation:F3} m.");
            Require(host.QaReleaseOwned(), "The towed fisherman could not opt out with R.");
            yield return Wait(() => !host.IsPulled && !helper.OwnsFriendTether && host.HookedFish == fish, 3,
                "Opt-out released the fishing line or left a stale friend tether.");
            helper.QaHoldOwned(false, 0);
            Check($"Friend tether reduced separation from {separation:F3} to {rescuedSeparation:F3} m; R released only the rescue line.");
        }

        private IEnumerator VerifyViewAfterLineBreak(FishActor fish)
        {
            Phase("Camera position after the active line breaks");
            var camera=host.Motor.GetComponentInChildren<Camera>();
            var frame=new WaitForEndOfFrame();
            Require(host.QaHoldOwned(false,20),"Could not reel until the line breaks.");
            var arc = FindAnyObjectByType<LineTensionArc>();
            Require(arc != null && arc.isActiveAndEnabled, "The attached line has no visible HUD arc.");
            bool recordedLoad = false;
            float deadline=Time.unscaledTime+18, peakTension=0;
            while(host.HookedFish==fish)
            {
                Require(Time.unscaledTime<deadline,"Held reel did not break or lose the line.");
                peakTension=Mathf.Max(peakTension,host.LineTension);
                if (!recordedLoad && host.LineTension >= .36f)
                {
                    recordedLoad = true;
                    yield return Shot("catch-line-load.png");
                }
                peakEyeOffset=Mathf.Max(peakEyeOffset,Vector3.Distance(camera.transform.position,host.FishingAnchor));
                yield return frame;
            }
            Require(host.HookedFish==null && fish.HookCount==0,"The fish must detach before the camera sweep.");
            host.QaHoldOwned(false,0);
            Vector3 anchor=host.FishingAnchor;
            Vector3 startDirection=camera.transform.forward;
            float maxYaw=0, maxBodyDrift=0, maxDetachedEyeOffset=0;
            float start=Time.unscaledTime;
            while(Time.unscaledTime-start<1.25f)
            {
                float progress=(Time.unscaledTime-start)/1.25f;
                var direction=Quaternion.AngleAxis(135f*Mathf.Sin(progress*Mathf.PI),Vector3.up)*startDirection;
                host.Motor.LookAtConsole(camera.transform.position+direction*10f);
                yield return frame;
                maxYaw=Mathf.Max(maxYaw,Vector3.Angle(Vector3.ProjectOnPlane(startDirection,Vector3.up),
                    Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up)));
                maxBodyDrift=Mathf.Max(maxBodyDrift,Vector3.Distance(anchor,host.FishingAnchor));
                maxDetachedEyeOffset=Mathf.Max(maxDetachedEyeOffset,Vector3.Distance(camera.transform.position,host.FishingAnchor));
            }
            Require(!arc.isActiveAndEnabled, "The tension arc remained visible after detachment.");
            yield return Shot("catch-line-detached.png");
            Check("The HUD arc appeared on attachment and cleared on release.");
            string measured=$"line tension {peakTension:F2}; eye offset while hooked {peakEyeOffset:F3} m; "+
                $"after detach {maxDetachedEyeOffset:F3} m; body drift {maxBodyDrift:F3} m; camera swept {maxYaw:F0}°.";
            Check(measured);
            Require(maxYaw>110f && maxBodyDrift<.04f && peakEyeOffset<.09f && maxDetachedEyeOffset<.06f,
                "Camera did not stay at the diver's head across the break: "+measured);
        }

        private IEnumerator HookPeer(FishActor fish)
        {
            // Stage only the pose. The second process fires the normal owned RPC.
            // Aim across the first shooter's ray to put the second bolt on another patch of skin.
            float yaw = Quaternion.LookRotation(host.Motor.AimDirection).eulerAngles.y-24;
            string view=Send("view",yaw);
            yield return Wait(()=>ReadClient()?.request==view && Mathf.Abs(Mathf.DeltaAngle(
                Quaternion.LookRotation(helper.Motor.AimDirection).eulerAngles.y,yaw))<1,3,"Client aim did not reach the second sightline.");
            Vector3 aim=helper.Motor.AimDirection;
            float until=Time.unscaledTime+5, retry=0;
            while(helper.HookedFish!=fish)
            {
                Require(Time.unscaledTime<until,"Independent owner failed to hit "+fish.SpeciesId+": "+ReadClient()?.error);
                helper.Motor.Teleport(fish.transform.position-aim*2.7f-Vector3.up*.6f);
                Physics.SyncTransforms();
                if(Time.unscaledTime>=retry)
                {
                    Send("fire",0);
                    retry=Time.unscaledTime+1;
                }
                yield return null;
            }
        }

        private void StageCooperativePull(FishActor fish)
        {
            // Find a clear water lane for the pair. Cliff-side hooks stay attached but
            // can require swimmers to reposition before both reels can close in.
            Vector3 center=fish.transform.position;
            Vector3 current=Vector3.ProjectOnPlane(host.FishingAnchor-center,Vector3.up).normalized;
            if(current.sqrMagnitude<.1f)current=Vector3.back;
            int mask=Physics.DefaultRaycastLayers;
            bool SeesFish(Vector3 eye)
                => !Physics.Linecast(eye,center,out var hit,mask,QueryTriggerInteraction.Ignore) ||
                   hit.collider.GetComponentInParent<FishActor>()==fish;
            foreach(float yaw in new[]{0f,45f,-45f,90f,-90f,135f,-135f,180f})
            {
                var outward=(Quaternion.AngleAxis(yaw,Vector3.up)*current+Vector3.up*.48f).normalized;
                var lateral=Vector3.Cross(Vector3.up,outward).normalized;
                Vector3 lead=center+outward*3.9f,support=center+outward*6.2f+lateral*.65f;
                if(Physics.CheckSphere(lead,.85f,mask,QueryTriggerInteraction.Ignore) ||
                    Physics.CheckSphere(support,.85f,mask,QueryTriggerInteraction.Ignore) ||
                    !SeesFish(lead) || !SeesFish(support))continue;
                host.Motor.Teleport(lead-Vector3.up*.6f);
                helper.Motor.Teleport(support-Vector3.up*.6f);
                Physics.SyncTransforms();
                return;
            }
            Require(false,"No clear cooperative fishing lane near "+fish.HabitatName);
        }

        // The two human inputs start from the same side of the fish. If a surge
        // leaves slack, ease away until the line takes load before winding again.
        private static bool CooperativeSwimTarget(HarpoonTool angler,FishActor fish,out Vector3 target)
        {
            Vector3 centre=fish.transform.position;
            Vector3 offset=angler.FishingAnchor-centre;
            float distance=offset.magnitude;
            target=centre;
            if(distance<.01f)return false;
            if(fish.Mode==FishMode.Tired && fish.Stamina<=0)
            {
                target=centre-offset/distance*3.4f;
                return distance>1.1f;
            }
            if(distance>15f)return true;
            if(distance+.38f>=angler.LineLength)return false;
            target=angler.FishingAnchor+offset/distance*4.5f;
            return true;
        }

        private IEnumerator Reel(FishActor fish)
        {
            bool pulling = true;
            float deadline = Time.unscaledTime + (fish.RequiresPartner ? 130 : 65);
            long capturedId = fish.CargoId;
            currentFight = new List<FishBehaviour>();
            float nextPeerSample = 0;
            bool cooperationConfirmed = false;
            while (fish.RequiresPartner ? fish.Mode != FishMode.Captured :
                host.CatchIdInSlot(2) != capturedId && host.CatchIdInSlot(3) != capturedId && host.CatchIdInSlot(4) != capturedId)
            {
                var phase = fish.Behaviour;
                if (phase != FishBehaviour.None && (currentFight.Count == 0 || currentFight[currentFight.Count-1] != phase)) currentFight.Add(phase);
                if (Time.unscaledTime >= nextPeerSample)
                {
                    nextPeerSample = Time.unscaledTime + .5f;
                    var peerView = ReadClient();
                    if (peerView != null && !string.IsNullOrEmpty(peerView.fishBehaviour)) peerBehaviours.Add(peerView.fishBehaviour);
                }
                Require(Time.unscaledTime < deadline && host.HookedFish == fish && !host.Motor.IsHelpless,
                    $"Landing lost or expired at {fish.HabitatName}: {fish.SpeciesId} / {fish.Mode}, stamina {fish.Stamina:F2}, "+
                    $"hooks {fish.HookCount}, paired {fish.Coordinated}, range {Vector3.Distance(host.FishingAnchor,fish.transform.position):F2} m, "+
                    $"lines {host.LineLength:F2}/{helper.LineLength:F2} m.");
                if (fish.Tension >= .58f) pulling = false;
                else if (fish.Tension < .24f) pulling = true;
                host.QaHoldOwned(false, pulling ? .6f : 0);
                if (remoteReeling)
                {
                    bool swim=CooperativeSwimTarget(host,fish,out var target);
                    host.Motor.QaSwimToward(target,swim ? .35f : 0);
                }
                if (remoteReeling && Time.unscaledTime >= nextRemoteReel)
                {
                    bool follow=CooperativeSwimTarget(helper,fish,out var target);
                    Send("fish_reel",pulling ? 2 : 0,follow ? "follow" : null,target);
                    nextRemoteReel = Time.unscaledTime + .35f;
                }
                if (remoteReeling && fish.Coordinated && !cooperationConfirmed)
                {
                    cooperationConfirmed = true;
                    Require(!fish.SoloTow && fish.HookCount >= 2, "Coordinated pull retained the solo tow state.");
                    var feel = host.GetComponent<FishingFeel>();
                    Require(feel != null && feel.ReelAudioPlaying, "The working reel has no audible loop while the coordinated pull is active.");
                    Check("Coordinated pull changed the server state and started the reel audio loop.");
                }
                if (remoteReeling && fish.Coordinated && Vector3.Distance(host.transform.position,fish.transform.position)<6
                    && !File.Exists(Path.Combine(Path.GetDirectoryName(reportPath),"catch-two-harpoons.png")))
                {
                    bool hostAttached=host.AttachedContact.Resolve(out _,out var hostPoint,out _,out _);
                    bool peerAttached=helper.AttachedContact.Resolve(out _,out var peerPoint,out _,out _);
                    Require(hostAttached && peerAttached
                        && Vector3.Distance(hostPoint,peerPoint)>.001f,"Two harpoons did not resolve distinct surface points.");
                    Check($"Simultaneous contacts are {Vector3.Distance(hostPoint,peerPoint):F3} m apart on the moving fish.");
                    yield return Shot("catch-two-harpoons.png");
                    string file="catch-two-harpoons-peer.png", peerPath=Path.Combine(clientDir,file);
                    Send("capture",0,file);
                    yield return Wait(()=>File.Exists(peerPath) && new FileInfo(peerPath).Length>0,6,"Client camera capture timed out.");
                    File.Copy(peerPath,Path.Combine(Path.GetDirectoryName(reportPath),file),true);
                }
                yield return null;
            }
            host.QaHoldOwned(false, 0);
            host.Motor.QaSwimToward(default, 0);
            fightSequences.Add(string.Join(">", currentFight));
            Check($"{fish.SpeciesId} fight phases: {fightSequences[fightSequences.Count-1]}");
        }

        private void RequireFightVariety()
        {
            // Fast solo catches may finish before a surge. VerifySoloTow checks the warning and surge explicitly with the heavy fish.
            Require(fightSequences.Count >= 3, "Fewer than three recorded fights.");
            var all = new HashSet<string>();
            foreach (var sequence in fightSequences) foreach (var phase in sequence.Split('>')) all.Add(phase);
            Require(new HashSet<string>(fightSequences).Count >= 2, "All recorded fights followed the same phase sequence: " + string.Join(" | ", fightSequences));
            Require(all.Contains("Lateral") && (all.Contains("Weakened") || all.Contains("HookShock")),
                "Recorded solo fights lacked a lateral arc or a calm phase: " + string.Join(",", all));
            Check($"Fight variety over {fightSequences.Count} fights: {new HashSet<string>(fightSequences).Count} distinct sequences, phases {string.Join(",", all)}.");
        }

        private IEnumerator VerifyBothBolts()
        {
            var local=FindObjectsByType<HarpoonShotFx>().FirstOrDefault(fx=>fx.VerifiedOwner==host.OwnerId
                && fx.VerifiedShot==host.ShotSequence && fx.VerifiedFrames>=6 && fx.AnchorTravel>.2f);
            Require(local!=null,"No moving host bolt was measured during the cooperative pull.");
            Require(local.MaxTipError<.001f && local.MaxKnotError<.001f,"Host bolt separated from the fish or its cable.");
            var request=Send("snapshot",0);
            yield return Wait(()=>ReadClient()?.request==request,8,"No independent bolt snapshot.");
            var peer=ReadClient();
            Require(peer!=null && peer.activeBoltOwners!=null && peer.activeBoltOwners.Contains(host.OwnerId)
                && peer.activeBoltOwners.Contains(helper.OwnerId) && peer.anchorFrames>=6 && peer.anchorTravel>.05f,
                "Peer did not observe the same two moving hooks.");
            Require(peer.anchorTipError<.001f && peer.anchorKnotError<.001f,"Remote bolt separated from the fish or its cable.");
            Check($"Host hook {host.OwnerId}/#{host.ShotSequence}, remote measured hook {peer.anchorOwner}/#{peer.anchorShot}, host/peer frames {local.VerifiedFrames}/{peer.anchorFrames}, attached travel {local.AnchorTravel:F3}/{peer.anchorTravel:F3} m, worst tip drift {local.MaxTipError:F6}/{peer.anchorTipError:F6} m, worst nock drift {local.MaxKnotError:F6}/{peer.anchorKnotError:F6} m.");
        }

        private string Send(string action, float value, string filename=null, Vector3 point=default)
        {
            var request = new UiQaProbe.Request { id=Guid.NewGuid().ToString("N"), action=action, number=value, value=filename, point=point };
            string path=Path.Combine(clientDir,"request.json"), temp=path+"."+request.id+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(request));
            if(File.Exists(path)) File.Replace(temp,path,null);
            else File.Move(temp,path);
            return request.id;
        }

        private UiQaProbe.Snapshot ReadClient()
        {
            try
            {
                var path=Path.Combine(clientDir,"state.json");
                return File.Exists(path) ? JsonUtility.FromJson<UiQaProbe.Snapshot>(File.ReadAllText(path)) : null;
            }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
        }

        private static IEnumerator Pause(float duration) => WaitForDuration(duration);

        private static IEnumerator WaitForDuration(float duration)
        {
            float until=Time.unscaledTime+duration;
            while(Time.unscaledTime<until) yield return null;
        }

        private static IEnumerator Wait(Func<bool> done,float timeout,string error)
        {
            float until=Time.unscaledTime+timeout;
            while(!done())
            {
                Require(Time.unscaledTime<until,error);
                yield return null;
            }
        }

        private static void Require(bool value,string error) { if(!value) throw new InvalidOperationException(error); }
        private void Phase(string phase) { result.phase=phase; Write(); }
        private void Check(string message) { result.checks.Add(message); Write(); }
        private void Write() => File.WriteAllText(reportPath,JsonUtility.ToJson(result,true));
        private IEnumerator Shot(string filename)
        {
            string path = Path.Combine(Path.GetDirectoryName(reportPath), filename);
            DateTime previous = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            yield return null;
            if (Application.isBatchMode) UiQaProbe.CapturePeerCamera(path);
            else ScreenCapture.CaptureScreenshot(path);
            // The screenshot is asynchronous. Keep the camera and held item in place until the frame reaches disk.
            yield return Wait(() => File.Exists(path) && File.GetLastWriteTimeUtc(path) != previous
                && new FileInfo(path).Length > 0, 8, "Game View did not finish capture " + filename);
        }
    }
}
#endif
