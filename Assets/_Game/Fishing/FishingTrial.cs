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
        private ThreatDirector pausedThreats;

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
            if (host != null) host.QaHoldOwned(false, 0);
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
            host.SelectSlot(0);
            yield return Wait(() => host.ActiveSlot == 0, 3, "Host harpoon could not be equipped.");

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
            host.Motor.Teleport(spare.transform.position-host.Motor.AimDirection*3.5f-Vector3.up*.6f);
            Physics.SyncTransforms();
            Require(Physics.Raycast(host.transform.position+Vector3.up*.6f,host.Motor.AimDirection,out var pocketHit,host.tuning.maxRange,
                host.tuning.hitMask,QueryTriggerInteraction.Ignore) && pocketHit.collider.GetComponentInParent<FishActor>()==spare,
                "Full creel ray did not reach the small fish.");
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
            var beforeTug = host.transform.position;
            float initial = heavy.Stamina;
            Require(host.QaHoldOwned(false, 6), "Host could not hold reel.");
            yield return Wait(()=>heavy.SurgeWarning,6,"No advance warning of solo surge.");
            yield return Shot("catch-solo-warning.png");
            yield return Wait(()=>heavy.Surging,3,"No solo surge.");
            yield return Pause(.12f);
            yield return Shot("catch-solo-surge.png");
            yield return Wait(()=>Vector3.Distance(beforeTug,host.transform.position)>.45f,3,"Solo surge did not move the diver.");
            yield return Shot("catch-solo-pulled.png");
            Require(host.HookedFish==heavy && heavy.HookCount==1 && host.CatchCount==3 && heavy.Stamina>=initial-.05f,
                "One harpoon progressed to a heavy catch.");
            host.QaHoldOwned(false,0);
            Check("Solo surge displaced the shooter "+Vector3.Distance(beforeTug,host.transform.position).ToString("F2")+" m.");

            Phase("Two independent impacts");
            yield return HookPeer(heavy);
            yield return Wait(()=>ReadClient()?.activeBoltOwners?.Distinct().Count()==2,6,"Independent view did not render both attachments.");
            // Opposed handles stay loaded and aim away from one another.
            var axis=(host.HandAnchor-heavy.transform.position).normalized;
            helper.Motor.Teleport(heavy.transform.position-axis*5f-Vector3.up*.6f);
            host.QaHoldOwned(false,1.5f);
            Send("fish_reel",1.5f);
            float startEnergy=heavy.Stamina;
            yield return Pause(.85f);
            Require(heavy.HookCount==2 && !heavy.Coordinated && heavy.Stamina>=startEnergy-.05f,
                "Opposed reels advanced the catch.");
            host.QaHoldOwned(false,0);
            Send("fish_reel",0);
            yield return Pause(.35f);
            yield return VerifyBothBolts();
            Check("Both rendered and attached independent server-confirmed projectiles; opposing directions did not exhaust the fish.");

            // Release the helper's line by switching tools, then reacquire it through another real shot.
            Send("slot",1);
            yield return Wait(()=>helper.ActiveSlot==1 && helper.HookedFish==null && heavy.HookCount==1 && host.HookedFish==heavy,
                4,"Switching the partner's tool failed to remove only their line.");
            Send("slot",0);yield return Wait(()=>helper.ActiveSlot==0,4,"Partner could not re-equip harpoon.");
            yield return HookPeer(heavy);
            Phase("Cooperative pull and heavy carry");
            axis=(host.HandAnchor-heavy.transform.position).normalized;
            helper.Motor.Teleport(host.transform.position+axis*2.2f+host.transform.right*2.0f);
            Physics.SyncTransforms();
            remoteReeling=true;
            nextRemoteReel=0;
            yield return Reel(heavy);
            remoteReeling=false;
            Send("fish_reel",0);
            Require(!host.Motor.IsDead && host.HandsFull && host.CarriedFish == heavy && host.CatchCount == 4
                && helper.HookedFish==null && heavy.HookCount==0,
                $"Heavy carry mismatch: HP {host.Motor.Health:F1}, O2 {host.Motor.Oxygen:F1}, mode {heavy.Mode}, hands {host.HandsFull}, catches {host.CatchCount}.");
            Require(!host.SelectSlot(1) && host.ActiveSlot == 0 && !host.QaFireOwned(), "Heavy catch allowed switching or firing.");
            yield return Pause(.5f);
            yield return Shot("catch-heavy.png");
            Check("Heavy capture completed with all three small catches intact; tool switch and fire blocked.");

            Phase("Deposit and observe swimming cargo on both peers");
            host.Motor.Teleport(world.tuning.cargoPosition - host.Motor.AimDirection * 2.65f - Vector3.up * .6f);
            Physics.SyncTransforms();
            var cage = world.container.GetComponentInChildren<WetCargoShoal>();
            int before = world.Team.cargo.Count, beforeSwimmers = cage == null ? 0 : cage.Count;
            var caughtIds = new[] { host.CatchIdInSlot(2), host.CatchIdInSlot(3), host.CatchIdInSlot(4), heavy.CargoId };
            world.Command(WorldAction.LoadCargo, 0);
            yield return Wait(() => host.CatchCount == 0 && world.Team.cargo.Count == before + 4, 6, "Server did not deposit all four catches.");
            Require(caughtIds.All(id => world.Team.cargo.Count(item => item.id == id) == 1), "Deposit lost or duplicated a catch ID.");
            yield return Wait(() => cage != null && cage.Count == beforeSwimmers + 4 && ReadClient()?.cageSwimmers == cage.Count,
                10, "Host/client cage swimmers did not reflect four new entries.");
            yield return Shot("catch-cage.png");
            Check("Four authoritative catch IDs now swim in both cages; hands and pockets are empty.");

            Phase("Hoist cargo into reservoir");
            yield return Pause(.3f);
            world.Command(WorldAction.Hoist, 0);
            yield return Wait(() => world.Team.hoistPhase != HoistPhase.Loading, 4, "Winch command refused.");
            yield return RefillOnDeck();
            yield return Wait(() => world.Team.hoistPhase == HoistPhase.Loading && caughtIds.All(id => world.Team.tank.Exists(item => item.id == id))
                && cage.Count == 0 && ClientReceivedDelivery(caughtIds), 45, "Host and client did not return to Loading with the same four delivered identities.");
            Check("Winch delivered each original ID to the reservoir and both cages emptied.");
            yield return Wait(()=>Mathf.Abs(camera.fieldOfView-ui.Settings.fov)<.25f,3,"FOV did not restore after landing.");

            Phase("Owner death and reconnect with two active lines");
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
            Require(helper.HookedFish==null && helper.CatchCount==0 && world.Team.tank.Count(x=>caughtIds.Contains(x.id))==4,
                "Reconnection restored another player's line or duplicated cargo.");
            host.QaReleaseOwned();
            yield return Wait(()=>heavy.HookCount==0 && host.HookedFish==null,3,"Last hook failed to release.");
            Check("Partner death and connection teardown each removed only its own harpoon. Rejoined owner had free hands, and all four delivered IDs remained unique.");
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

        private FishActor Fish(string species) => FindObjectsByType<FishActor>().Single(f => f.depthTier == 1 && f.SpeciesId == species);

        private IEnumerator Hook(FishActor fish,bool closeShot=false)
        {
            yield return Pause(host.tuning.fireCooldown+.12f);
            Require(fish.Mode == FishMode.Roam && !host.Motor.IsHelpless, "Fish unavailable or host incapacitated.");
            Vector3 aim = host.Motor.AimDirection;
            host.Motor.Teleport(fish.transform.position - aim * 3.8f - Vector3.up * .6f);
            Physics.SyncTransforms();
            Require(Physics.Raycast(host.transform.position + Vector3.up*.6f, aim, out var hit, host.tuning.maxRange,
                host.tuning.hitMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FishActor>() == fish,
                "Fixture ray blocked before " + fish.SpeciesId + ": " + (hit.collider == null ? "miss" : hit.collider.name));
            if(closeShot)
            {
                host.Motor.Teleport(hit.point - aim * .65f - Vector3.up * .6f);
                Physics.SyncTransforms();
            }
            Require(host.QaFireOwned(), "Host owned fire request rejected.");
            yield return Wait(() => host.HookedFish == fish, 3, "Normal harpoon RPC missed " + fish.SpeciesId + ".");
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

        private IEnumerator Reel(FishActor fish)
        {
            bool pulling = true;
            float deadline = Time.unscaledTime + 65;
            long capturedId = fish.CargoId;
            while (fish.RequiresPartner ? fish.Mode != FishMode.Captured :
                host.CatchIdInSlot(2) != capturedId && host.CatchIdInSlot(3) != capturedId && host.CatchIdInSlot(4) != capturedId)
            {
                Require(Time.unscaledTime < deadline && host.HookedFish == fish && !host.Motor.IsHelpless,
                    "Line lost or expired: " + fish.SpeciesId + " / " + fish.Mode + " / stamina " + fish.Stamina.ToString("F2"));
                if (fish.Tension >= .58f) pulling = false;
                else if (fish.Tension < .24f) pulling = true;
                host.QaHoldOwned(false, pulling ? .6f : 0);
                if (remoteReeling && Time.unscaledTime >= nextRemoteReel)
                {
                    Send("fish_reel",pulling?2:0);
                    nextRemoteReel=Time.unscaledTime+.65f;
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

        private string Send(string action, float value, string filename=null)
        {
            var request = new UiQaProbe.Request { id=Guid.NewGuid().ToString("N"), action=action, number=value, value=filename };
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
