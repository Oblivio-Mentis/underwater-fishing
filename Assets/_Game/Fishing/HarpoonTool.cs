using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    [RequireComponent(typeof(DiverMotor))]
    public sealed partial class HarpoonTool : NetworkBehaviour
    {
        public FishingTuning tuning;
        public FriendTetherTuning friendTetherTuning;
        public Material ropeMaterial;
        public float ReelMultiplier = 1f;
        public event System.Action<Vector3,Vector3> ShotVisual;
        private readonly SyncVar<HarpoonContact> _impact = new();
        public HarpoonContact LastShotContact { get; private set; }
        public HarpoonContact AttachedContact => _impact.Value;
        private static readonly List<HarpoonTool> ServerTools = new();
        private readonly SyncVar<NetworkObject> _fishObject = new();
        private readonly SyncVar<NetworkObject> _friendTarget = new();
        private readonly SyncVar<NetworkObject> _friendSource = new();
        private readonly SyncVar<float> _optOutRemaining = new();
        private FishActor _fish;
        private InputActionMap _actions;
        private InputAction _fire, _release, _assist;
        private LineRenderer _line;
        private float _nextHeartbeat, _nextFire, _lastControls;
        private bool _reelHeld, _assistHeld;
        private readonly SyncVar<NetworkObject> _assistTarget = new();
        private readonly SyncVar<NetworkObject> _assistant = new();
        private readonly SyncVar<bool> _reelingActive = new();
        public struct PocketItem
        {
            public NetworkObject species;
            public long id;
            public bool ball;
            public byte defenseSpecies;
            public TechSlot invention;
        }
        private readonly SyncList<PocketItem> pockets = new();
        private readonly SyncVar<int> pocketCapacity = new(3);
        public int PocketCapacity => pocketCapacity.Value;
        public int SlotCount => PocketCapacity+2;
        internal const int MaximumSlots=2+TechSlot.PocketCount;
        bool inventionPocketsRestored;
        internal void SetPocketCapacity(int value)
        {
            value=Mathf.Clamp(value,3,MaximumSlots-2);
            for(int slot=MaximumSlots-1;slot>=value+2;slot--)
                if(PocketOccupied(slot)){value=slot-1;break;}
            if(pocketCapacity.Value!=value)pocketCapacity.Value=value;
            if(ActiveSlot>=value+2)EquipSlot(1);
        }
        private readonly SyncVar<int> _slot = new(), _shotSequence = new();
        private InputAction[] _select;
        private InputAction _wheel;
        private float _friendObstructed, _friendLastPull, _friendRefuseUntil, _friendExpiresAt;
        private bool _ownsFriendTuning;
        private bool _primaryConsumedUntilRelease;
        float raidRescueWinding;

        public struct LineReadout { public float length, tension; }
        readonly SyncVar<LineReadout> _lineReadout = new();
        readonly SyncVar<int> _stabilizerCooldown = new();
        float stabilizerReadyAt;
        public float LineLength => _lineReadout.Value.length;
        public float LineTension => _lineReadout.Value.tension;
        public int StabilizerCooldown => _stabilizerCooldown.Value;
        internal Vector3 FishingAnchor => transform.position + Vector3.up * .6f;
        internal Vector3 HandAnchor => FishingAnchor + transform.right * .16f + Motor.AimDirection * .32f;
        internal void UpdateFishingLine(float length, float load)
        {
            _lineReadout.Value = new LineReadout { length = length, tension = load };
            _stabilizerCooldown.Value=Mathf.CeilToInt(Mathf.Max(0,stabilizerReadyAt-Time.time));
        }
        internal bool TryStabilizeDeepLine()
        {
            if(!IsServerInitialized || Time.time<stabilizerReadyAt)return false;
            int cooldown=RaidWorld.Instance?.SkillValue(Motor,SkillLine.HarpoonModule)??0;
            if(cooldown<=0)return false;
            stabilizerReadyAt=Time.time+cooldown;
            return true;
        }
        internal void FinishFishingLine()
        {
            _impact.Value = default;
            _lineReadout.Value = default;
            _reelingActive.Value = false;
        }

        public DiverMotor Motor { get; private set; }
        public FishActor HookedFish => _fish;
        public int ActiveSlot => _slot.Value;
        public bool HarpoonInHands => OwnsHarpoon && !(RaidWorld.Instance?.IsHoldingGosha(Motor)??false) && ActiveSlot == 0 && !HandsFull && _assistTarget.Value == null;
        public int ShotSequence => _shotSequence.Value;
        public DiverMotor AssistTarget => MotorOf(_assistTarget.Value);
        public DiverMotor AssistingDiver => MotorOf(_assistant.Value);
        public LootActor CarriedSalvage => LootActor.HeldBy(Motor);
        public bool HoldingHeavyCatch => _liftedHeavy.Value.id > 0 || _fish != null && _fish.RequiresPartner && _fish.Mode == FishMode.Captured;
        public bool HandsFull => HoldingHeavyCatch || CarriedSalvage != null || (RaidWorld.Instance?.HaulingEquipment(Motor)??false);
        public int CatchCount => PocketCount+(HoldingHeavyCatch?1:0);
        int PocketCount
        {
            get
            {
                int count=0;
                for(int slot=2;slot<MaximumSlots;slot++)if(CatchIdInSlot(slot)>0)count++;
                return count;
            }
        }

        public bool HasFullCreel => FreePocket()<0;
        public int SmallestCatchSize => HoldingHeavyCatch?HeavySpecies.cargoSize:PocketCount>0?1:0;
        public FishActor CarriedFish => HoldingHeavyCatch?HeavySpecies:FishInSlot(FirstOccupiedPocket());

        PocketItem Pocket(int slot) => slot>=2 && slot-2<pockets.Count ? pockets[slot-2] : default;
        void SetPocket(int slot,PocketItem item) => pockets[slot-2]=item;
        // A pocketed fish can already be torn down when the scene unloads: a destroyed object reads as empty.
        public string CatchSpeciesInSlot(int slot)
        {
            byte kind=Pocket(slot).defenseSpecies;
            return kind>0?DefenseCreatureAssets.Key((DefenseSpecies)(kind-1)):FishInSlot(slot)?.SpeciesId;
        }
        public FishActor FishInSlot(int slot) { var species = Pocket(slot).species; return species != null ? species.GetComponent<FishActor>() : null; }
        public long CatchIdInSlot(int slot) => Pocket(slot).id;
        public bool HasBallInSlot(int slot) => Pocket(slot).ball;
        bool PocketOccupied(int slot)
        {
            var item=Pocket(slot);
            return item.ball || item.id>0 || item.invention.count>0;
        }

        internal int BallSlot
        {
            get
            {
                for(int slot=2;slot<MaximumSlots;slot++)if(HasBallInSlot(slot))return slot;
                return -1;
            }
        }

        internal bool StoreBall()
        {
            int slot=FreePocket();
            if(!IsServerInitialized || HandsFull || slot<0 || BallSlot>=0)return false;
            SetPocket(slot,new PocketItem{ball=true});
            EquipSlot(slot);
            return true;
        }

        internal void RemoveBall()
        {
            int slot=BallSlot;
            if(slot>=0)SetPocket(slot,default);
        }

        void EquipSlot(int slot)
        {
            if(slot!=ActiveSlot && HasBallInSlot(ActiveSlot))RaidWorld.Instance?.CancelBallWind(Motor);
            _slot.Value=slot;
        }

        public bool SelectSlot(int slot)
        {
            if (!IsClientInitialized || !IsOwner || UI.UiInputGate.Blocked || !CanSelect(slot)) return false;
            if(slot!=ActiveSlot)CancelInventionGesture();
            SelectSlotServer(slot);
            return true;
        }

        private bool CanSelect(int slot) => slot >= 0 && slot < SlotCount && !Motor.IsHelpless && !HandsFull
            && !TetherActive && AssistTarget == null;

        [ServerRpc(RequireOwnership = true)]
        private void SelectSlotServer(int slot)
        {
            if (!CanSelect(slot) || slot == ActiveSlot) return;
            ReleaseBallLineOnServer();
            DetachDefenseLine();
            if (_fish != null) _fish.ReleaseHook(this);
            EquipSlot(slot);
        }
        public bool HasBallLine => _impact.Value.ball && _impact.Value.target!=null;
        internal void ReleaseBallLineOnServer()
        {
            if(IsServerInitialized && HasBallLine) RaidWorld.Instance?.ReleaseBallLine(this);
        }
        public bool OwnsFriendTether => _friendTarget.Value != null;
        public bool IsPulled => _friendSource.Value != null;
        public bool TetherActive => OwnsFriendTether || IsPulled;
        public DiverMotor TetherOwner => IsPulled ? MotorOf(_friendSource.Value) : OwnsFriendTether ? Motor : null;
        public DiverMotor TetherTarget => OwnsFriendTether ? MotorOf(_friendTarget.Value) : IsPulled ? Motor : null;
        public float TetherOptOutRemaining => _optOutRemaining.Value;
        public float TetherMaxRange => Mathf.Min(Safe(tuning == null ? 0 : tuning.maxRange, 0, 0, 100), Safe(friendTetherTuning == null ? 12 : friendTetherTuning.maximumRange, 12, 2, 20));
        public bool Reeling => IsServerInitialized
            ? _reelHeld && Time.unscaledTime - _lastControls < tuning.inputTimeout
            : _reelingActive.Value;
        public bool IsAssisted => _assistant.Value != null;

        private void Awake()
        {
            Motor = GetComponent<DiverMotor>();
            _fishObject.OnChange += OnFishChanged;
            if (friendTetherTuning == null)
            {
                friendTetherTuning = ScriptableObject.CreateInstance<FriendTetherTuning>();
                _ownsFriendTuning = true;
            }
            _friendTarget.UpdateSettings(new SyncTypeSettings(.05f));
            _friendSource.UpdateSettings(new SyncTypeSettings(.05f));
            _optOutRemaining.UpdateSettings(new SyncTypeSettings(.1f));
        }

        public override void OnStartServer()
        {
            pockets.Clear();
            inventionPocketsRestored=false;
            for(int slot=2;slot<MaximumSlots;slot++)pockets.Add(default);
            SetPocketCapacity(3);
            pocketTheftShieldUsed=false;
            _harpoonId.Value = NewToolId();
            if (!ServerTools.Contains(this)) ServerTools.Add(this);
            _friendTarget.Value = null; _friendSource.Value = null; _optOutRemaining.Value = 0;
            _impact.Value=default;
            _reelingActive.Value=false;
            _friendRefuseUntil = _friendObstructed = 0;
            _friendLastPull = _lastControls = Time.unscaledTime;
            _reelHeld = _assistHeld = false;
        }

        public override void OnStopServer()
        {
            CarriedSalvage?.DropAtFeet(Motor);
            ReleaseAll(false);
            ServerTools.Remove(this);
        }

        public override void OnStartClient()
        {
            _fish = _fishObject.Value == null ? null : _fishObject.Value.GetComponent<FishActor>();
            var rope = new GameObject("Harpoon line");
            rope.transform.SetParent(transform, false);
            _line = rope.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = _line.endWidth = 0.025f;
            _line.startColor = _line.endColor = Color.yellow;
            if (ropeMaterial != null) _line.sharedMaterial = ropeMaterial;
            _line.enabled = false;
            if (!IsOwner) return;
            _actions = new InputActionMap("Harpoon");
            _fire = _actions.AddAction("FireAndReel", InputActionType.Button, "<Mouse>/leftButton");
            _release = _actions.AddAction("Release", InputActionType.Button, "<Keyboard>/r");
            _assist = _actions.AddAction("Assist", InputActionType.Button, "<Keyboard>/e");
            _select = new InputAction[MaximumSlots];
            for (int slot = 0; slot < _select.Length; slot++)
                _select[slot] = _actions.AddAction("Slot" + (slot + 1), InputActionType.Button, "<Keyboard>/" + (slot + 1));
            _wheel = _actions.AddAction("Cycle", InputActionType.Value, "<Mouse>/scroll/y");
            _actions.Enable();
        }

        public override void OnStopClient()
        {
            chargeSlot=-1;
            _actions?.Dispose();
            _actions = null;
            if (_line != null) { _line.gameObject.SetActive(false); Destroy(_line.gameObject); _line = null; }
        }

        private void Update()
        {
            if (!IsClientInitialized || !IsOwner || _actions == null || tuning == null) return;
            bool gameplay = Application.isFocused && !UI.UiInputGate.Blocked;
            if(!gameplay || UI.CheatPanel.PointerActive || UI.UiInputGate.MonitorLook)
                CancelInventionGesture();
            if(gameplay && !UI.UiInputGate.MonitorLook && Keyboard.current?.qKey.wasPressedThisFrame==true)
            {
                CancelInventionGesture();
                RequestDropSelected();
            }
            bool rackPointer = !UI.UiInputGate.Active && RaidWorld.Instance && RaidWorld.Instance.RackFocused(Motor,true);
            bool controlling = gameplay && Cursor.lockState == CursorLockMode.Locked && !UI.UiInputGate.PointerReserved && !rackPointer;
            if(!_fire.IsPressed())_primaryConsumedUntilRelease=false;
            bool primaryPressed=controlling && _fire.WasPressedThisFrame();
            bool itemConsumed=!UI.UiInputGate.Active && controlling &&
                HandleInventionPointer(primaryPressed,_fire.WasReleasedThisFrame());
            itemConsumed|=primaryPressed && !itemConsumed && ConsumeCargoPrimaryInput();
            // The ball owns LMB for throwing. Number keys and the wheel can still stow it.
            bool cheatPointer = UI.CheatPanel.PointerActive;
            bool maySelect=gameplay && (cheatPointer || WindingCharge || !UI.UiInputGate.PointerReserved ||
                RaidWorld.Instance?.GoshaBallInHands(Motor)==true);
            if (maySelect && !Motor.IsHelpless && UI.UiRoot.Instance?.EditingCheatQuantity != true)
            {
                for (int i = 0; i < _select.Length; i++)
                    if (_select[i].WasPressedThisFrame()) SelectSlot(i);
                float wheel = cheatPointer ? 0 : _wheel.ReadValue<float>();
                if (Mathf.Abs(wheel) > .01f) SelectSlot((ActiveSlot + (wheel > 0 ? SlotCount-1 : 1)) % SlotCount);
            }
            if (primaryPressed && !itemConsumed && ActiveSlot == 0 && AssistTarget == null && _fish == null && !HasBallLine && !HasDefenseLine && !TetherActive) FireServer();
            bool keyboardControls = controlling || (gameplay && cheatPointer);
            if (Application.isFocused && _release.WasPressedThisFrame())
            {
                var carried=CarriedInvention;
                if(carried!=null && gameplay)TryStowCarriedInvention(carried);
                else if(keyboardControls || IsPulled)ReleaseServer();
            }
            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + Mathf.Max(0.02f, tuning.heartbeatInterval);
            bool reel = controlling && !_primaryConsumedUntilRelease && ActiveSlot == 0 && _fire.IsPressed();
            bool assist = keyboardControls && _assist.IsPressed() && (!UI.UiInputGate.Active || UI.UiInputGate.AllowAssist || AssistTarget != null)
                || controlling && ActiveSlot == 1 && _fire.IsPressed();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!UI.UiInputGate.Blocked && !UI.UiInputGate.PointerReserved)
            {
                reel |= Time.unscaledTime < _qaReelUntil;
                assist |= Time.unscaledTime < _qaGripUntil;
            }
#endif
            SetControlsServer(reel, assist);
        }

        [ServerRpc(RequireOwnership = true)]
        private void FireServer()
        {
            if (HasBallLine || HasDefenseLine || (RaidWorld.Instance?.IsHoldingGosha(Motor)??false) || (RaidWorld.Instance?.GoshaKnocked(Motor)??false))return;
            if (!OwnsHarpoon || HandsFull || tuning == null || ActiveSlot != 0 || AssistTarget != null || _fish != null || TetherActive || Motor.IsHelpless || Time.unscaledTime < _nextFire) return;
            if(FishLoadTarget(out _,out _) && CargoDispatch.ServerSeesOpening(RaidWorld.Instance,Motor,Motor.AimDirection))return;
            _nextFire = Time.unscaledTime + Mathf.Max(0.1f, tuning.fireCooldown);
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            // Sample the same pose for PhysX and the network contact even between fixed ticks.
            Physics.SyncTransforms();
            bool didHit=AimCast(tuning, eye, Motor.AimDirection, out var hit);
            var endpoint=didHit?hit.point:eye+Motor.AimDirection*tuning.maxRange;
            var friend=didHit?hit.collider.GetComponentInParent<DiverMotor>():null;
            var fish=didHit?hit.collider.GetComponentInParent<FishActor>():null;
            HarpoonContact contact=default;
            if(didHit && hit.collider.TryGetComponent<Nereid.GoshaBallBody>(out var ball))
                RaidWorld.Instance?.TryHookBall(this,ball,hit,out contact);
            else if(friend!=null)
            {
                if(TryHookFriend(friend.GetComponent<HarpoonTool>()))
                    contact=HarpoonContact.FromHit(friend.NetworkObject,friend.transform,false,hit,Motor.AimDirection);
            }
            else if(fish!=null && (fish.RequiresPartner || !HasFullCreel))
            {
                bool tail = hit.collider.transform == fish.ImpactSurface(true)
                    && fish.ImpactSurface(true) != fish.ImpactSurface(false);
                var candidate = HarpoonContact.FromHit(fish.NetworkObject,fish.ImpactSurface(tail),tail,hit,Motor.AimDirection);
                if (fish.TryHook(this, candidate))
                {
                    _fish = fish;
                    _fishObject.Value = fish.NetworkObject;
                    contact = candidate;
                }
            }
            else if(fish==null)
            {
                var director=ThreatDirector.Instance;
                if(director && !director.HookDefense(this,hit))director.Strike(eye,Motor.AimDirection,1f,hit);
            }
            if(contact.target==null)FishActor.AlertNearShot(eye,endpoint);
            _impact.Value=contact;
            _shotSequence.Value++;
            ShotVisualObservers(eye,endpoint,contact);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetControlsServer(bool reel, bool assist)
        {
            _reelHeld = reel && OwnsHarpoon && !HandsFull && !Motor.IsHelpless && ActiveSlot == 0;
            _assistHeld = assist && !Motor.IsHelpless && ActiveSlot <= 1;
            _lastControls = Time.unscaledTime;
        }

        // Acknowledges a real accepted shot, including misses; attachment still comes from replicated hook state.
        [ObserversRpc]
        private void ShotVisualObservers(Vector3 origin, Vector3 endpoint, HarpoonContact contact)
        {
            LastShotContact=contact;
            ShotVisual?.Invoke(origin, endpoint);
        }

        [ServerRpc(RequireOwnership = true)]
        private void ReleaseServer()
        {
            if(CarriedInvention!=null && !TetherActive)return;
            // Refusal is owned by the target's RPC; the shooter cannot shorten or bypass this period.
            _friendRefuseUntil = Time.unscaledTime + Safe(friendTetherTuning.optOutSeconds, 3, 3, 10);
            _optOutRemaining.Value = _friendRefuseUntil - Time.unscaledTime;
            if (HasBallLine) { ReleaseBallLineOnServer(); return; }
            if (HasDefenseLine) { DetachDefenseLine(); return; }
            if (IsPulled) { ReleaseIncomingFriend(); return; }
            if (OwnsFriendTether) { ReleaseOutgoingFriend(); return; }
            if (AssistTarget != null) { DetachGrip(); _assistHeld = false; return; }
            if (_fish != null) _fish.ReleaseHook(this);
            else EmptyPocket(ActiveSlot, true);
        }

        /// <summary>Local-owner UI action. The server still checks ownership through ReleaseServer.</summary>
        public void CancelFriendTether()
        {
            if (IsClientInitialized && IsOwner && TetherActive) ReleaseServer();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float _qaReelUntil, _qaGripUntil;

        internal bool QaHoldOwned(bool grip, float duration)
        {
            if (!IsClientInitialized || !IsOwner) return false;
            // A held ball reserves the pointer immediately. Releasing a previous synthetic hold
            // still has to clear the owner timer before the next independent shot.
            if (duration > 0 && (Motor.IsHelpless || UI.UiInputGate.Blocked || UI.UiInputGate.PointerReserved)) return false;
            float until = Time.unscaledTime + Mathf.Clamp(duration, 0, 15);
            if (grip) _qaGripUntil = until;
            else _qaReelUntil = until;
            return true;
        }

        // A batch Editor has no focused Game view. Synthetic owner requests still respect modal menus.
        private static bool QaMenuBlocked => UI.UiInputGate.Active &&
            (UI.UiInputGate.Modal || Time.unscaledTime < UI.UiInputGate.ResumeAt ||
             (!Application.isBatchMode && !Application.isFocused));

        internal bool QaPrimaryOwned()
        {
            if(!IsClientInitialized || !IsOwner || QaMenuBlocked)return false;
            return HandleInventionPointer(true,true) || ConsumeCargoPrimaryInput() || QaFireOwned();
        }

        // Explicit QA only: still dispatches the normal owned RPC and leaves hit/cooldown validation on the server.
        internal bool QaFireOwned()
        {
            if (!IsClientInitialized || !IsOwner || tuning == null || Motor == null
                || HasBallLine || !OwnsHarpoon || HandsFull || QaMenuBlocked || UI.UiInputGate.PointerReserved || ActiveSlot != 0 || Motor.IsHelpless || AssistTarget != null || _fish != null || TetherActive) return false;
            FireServer(); return true;
        }

        internal bool QaReleaseOwned()
        {
            if (!IsClientInitialized || !IsOwner || (QaMenuBlocked && !IsPulled)
                || (_fish == null && FishInSlot(ActiveSlot) == null && AssistTarget == null && !HasBallLine && !HasDefenseLine && !TetherActive)) return false;
            ReleaseServer(); return true;
        }
#endif

        public void DropCargo()
        {
            if (!IsServerInitialized) return;
            ReleaseAll();
        }

        private void ReleaseAll(bool showDrops = true)
        {
            ReleaseBallLineOnServer();
            DetachDefenseLine();
            _liftedHeavy.Value = default;
            ReleaseOutgoingFriend();
            ReleaseIncomingFriend();
            DetachGrip();
            ToolOf(_assistant.Value)?.DetachGrip();
            if (_fish != null) _fish.ReleaseHook(this);
            for (int slot = 2; slot < SlotCount; slot++) EmptyPocket(slot, showDrops);
        }

        public bool DropOneCatch()
        {
            if (!IsServerInitialized) return false;
            if (_liftedHeavy.Value.id > 0) { _liftedHeavy.Value = default; return true; }
            if (HoldingHeavyCatch) { _fish.ReleaseHook(this); return true; }
            return DropPocketCatch();
        }

        internal bool DropPocketCatch() => IsServerInitialized && EmptyPocket(FirstOccupiedPocket(), true);

        private int FirstOccupiedPocket()
        {
            if (CatchIdInSlot(ActiveSlot) > 0) return ActiveSlot;
            for (int slot = 2; slot < SlotCount; slot++)
                if (CatchIdInSlot(slot) > 0) return slot;
            return -1;
        }

        private bool EmptyPocket(int slot, bool showEscape)
        {
            if(CatchIdInSlot(slot)==0)return false;
            var item=Pocket(slot);
            var fish=FishInSlot(slot);
            SetPocket(slot,default);
            if(showEscape && item.defenseSpecies>0 && RaidWorld.Instance && RaidWorld.Instance.CanRegisterDrop(item.id))
                RaidWorld.Instance.RegisterDrop(new DroppedItem{id=item.id,kind=DroppedKind.Catch,species=item.species,
                    position=transform.position,yaw=transform.eulerAngles.y,dead=true,roll=78,defenseSpecies=item.defenseSpecies});
            else if (showEscape && fish != null && !Motor.IsInside)
                FishReleasedObservers(fish.SpeciesId, transform.position, transform.rotation);
            return true;
        }

        [ObserversRpc]
        private void FishReleasedObservers(string species, Vector3 position, Quaternion rotation) =>
            CatchFishModel.Release(species, position, rotation);

        public bool TakeCargo()
        {
            if (!IsServerInitialized) return false;
            return HoldingHeavyCatch ? RemoveHeavy() : EmptyPocket(FirstOccupiedPocket(), false);
        }

        internal bool UnloadCatches(TeamProgress team, ProgressionTuning progression)
        {
            if (!IsServerInitialized) return false;
            bool loaded = false;
            if (HoldingHeavyCatch)
            {
                var item = HeavySpecies.ToCargo(HeavyId);
                if (team.LoadCargo(item, progression))
                {
                    loaded = RemoveHeavy();
                    if (!loaded) team.cargo.RemoveAll(entry => entry.id == item.id);
                }
            }
            for (int slot = 2; slot < SlotCount; slot++)
            {
                var species = FishInSlot(slot);
                if (species == null || !team.LoadCargo(species.ToCargo(CatchIdInSlot(slot)), progression)) continue;
                EmptyPocket(slot, false);
                loaded = true;
            }
            return loaded;
        }

        internal bool StowCapture(FishActor fish)
        {
            if (!IsServerInitialized || _fish != fish || fish.RequiresPartner) return false;
            int slot=FreePocket();
            if(slot<0)return false;
            // The scene actor repopulates the shoal; the immutable captured ID travels with its owner.
            SetPocket(slot,new PocketItem { species = fish.NetworkObject, id = fish.CargoId });
            _fish = null;
            _fishObject.Value = null;
            FinishFishingLine();
            return true;
        }

        public void NotifyFishLost(FishActor fish)
        {
            if (!IsServerInitialized || fish == null) return;
            if (_fish == fish)
            {
                _fish = null;
                _fishObject.Value = null;
                FinishFishingLine();
                ToolOf(_assistant.Value)?.DetachGrip();
            }
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized) return;
            _reelingActive.Value=!Motor.IsHelpless && (OwnsFriendTether || HasBallLine || HasDefenseLine || HasFishingLine(this)) && Reeling;
            if (Motor.IsHelpless)
            {
                ReleaseAll();
                _reelHeld = _assistHeld = false;
                return;
            }
            _optOutRemaining.Value = Mathf.Max(0, _friendRefuseUntil - Time.unscaledTime);
            TickFriendTether();
            TickGrip();
        }

        private void TickGrip()
        {
            if (!_assistHeld || HandsFull || tuning == null || HasBallLine || HasDefenseLine || TetherActive || _fish != null || ActiveSlot > 1
                || Time.unscaledTime - _lastControls >= tuning.inputTimeout)
            { DetachGrip(); return; }
            var shooter = ToolOf(_assistTarget.Value);
            float reach = Mathf.Min(2.4f, tuning.assistRange);
            if (shooter != null)
            {
                if (!CanGrip(shooter, reach + .45f)) { DetachGrip(); return; }
            }
            else
            {
                float nearest = reach;
                foreach (var candidate in ServerTools)
                {
                    if (!CanGrip(candidate, nearest)) continue;
                    shooter = candidate;
                    nearest = Vector3.Distance(transform.position, candidate.transform.position);
                }
                if (shooter == null) return;
                _assistTarget.Value = shooter.NetworkObject;
                shooter._assistant.Value = NetworkObject;
            }
            // Pull both divers as one team. The motor sweeps collisions even when the helper is tugged toward the shooter.
            Vector3 toShooter = shooter.transform.position - transform.position;
            if (toShooter.magnitude > 1.15f)
                Motor.ApplyTetherMotion(toShooter.normalized * Mathf.Min(toShooter.magnitude - 1.15f, 5f * Time.fixedDeltaTime));
        }

        private bool CanGrip(HarpoonTool shooter, float reach) => shooter != null && shooter != this
            && shooter.gameObject.scene == gameObject.scene && (shooter.HasDefenseLine || shooter._fish != null && shooter._fish.Mode != FishMode.Captured)
            && !shooter.Motor.IsHelpless && !shooter.TetherActive && (shooter._assistant.Value == null || shooter._assistant.Value == NetworkObject)
            && (shooter.transform.position - transform.position).sqrMagnitude <= reach * reach && FriendLineVisible(shooter);

        private void DetachGrip()
        {
            var shooter = ToolOf(_assistTarget.Value);
            if (shooter != null && shooter._assistant.Value == NetworkObject) shooter._assistant.Value = null;
            _assistTarget.Value = null;
        }

        private void LateUpdate()
        {
            if (_line == null) return;
            bool attached = _impact.Value.Resolve(out _,out var contact,out _,out _)
                && (OwnsFriendTether || HasBallLine || HasFishingLine(this));
            if(HasDefenseLine && ThreatDirector.Instance)attached=ThreatDirector.Instance.TryDefenseVisualAnchor(defenseHook.Value,out contact);
            _line.enabled=attached;
            if(!attached)return;
            _line.SetPosition(0,transform.position+Vector3.up*.6f+transform.right*.2f);
            _line.SetPosition(1,contact);
        }

        bool TryHookFriend(HarpoonTool target)
        {
            if (target == null || target == this || !CanTetherParticipant(this) || !CanTetherParticipant(target)) return false;
            if (HasBallLine || target.HasBallLine || target.HasDefenseLine || HasDefenseLine || TetherActive || _fish != null || target.TetherActive || target.AssistTarget != null || target.HandsFull) return false;
            if (target.gameObject.scene != gameObject.scene || Time.unscaledTime < target._friendRefuseUntil) return false;
            if (Vector3.Distance(transform.position, target.transform.position) > TetherMaxRange || !FriendLineVisible(target)) return false;
            _friendTarget.Value = target.NetworkObject;
            target._friendSource.Value = NetworkObject;
            _friendObstructed = 0; _friendLastPull = Time.unscaledTime;raidRescueWinding=0;
            _friendExpiresAt = Time.unscaledTime + Safe(friendTetherTuning.maximumDuration, 10, 1, 20);
            _assistHeld = false;
            return true;
        }

        void TickFriendTether()
        {
            if (IsPulled)
            {
                var source = ToolOf(_friendSource.Value);
                if (!CanTetherParticipant(this) || !CanTetherParticipant(source) || source._friendTarget.Value != NetworkObject)
                { ReleaseIncomingFriend(); return; }
            }
            if (!OwnsFriendTether) return;
            var target = ToolOf(_friendTarget.Value);
            float timeout = Safe(tuning == null ? .35f : tuning.inputTimeout, .35f, .05f, 1);
            if (!CanTetherParticipant(this) || !CanTetherParticipant(target) || target._friendSource.Value != NetworkObject
                || target.gameObject.scene != gameObject.scene || Time.unscaledTime < target._friendRefuseUntil
                || Time.unscaledTime >= _friendExpiresAt
                || Time.unscaledTime - _lastControls > timeout || Time.unscaledTime - target._lastControls > timeout)
            { ReleaseOutgoingFriend(); return; }
            Vector3 offset = transform.position - target.transform.position;
            float distance = offset.magnitude;
            if (!Finite(distance) || distance > TetherMaxRange) { ReleaseOutgoingFriend(); return; }
            if (!FriendLineVisible(target))
            {
                _friendObstructed += Time.fixedDeltaTime;
                if (_friendObstructed > Safe(friendTetherTuning.lineOfSightGrace, .15f, 0, .5f)) ReleaseOutgoingFriend();
                return; // Never move through an obstruction, even during the short release grace.
            }
            _friendObstructed = 0;
            if (!Reeling)
            {
                if (Time.unscaledTime - _friendLastPull > Safe(friendTetherTuning.idleReleaseSeconds, 1, .2f, 3)) ReleaseOutgoingFriend();
                return;
            }
            _friendLastPull = Time.unscaledTime;
            if(target.Motor.RaidCaptive>0)
            {
                var raid=ThreatDirector.Instance;
                raidRescueWinding+=Time.fixedDeltaTime*Mathf.Max(.5f,ReelMultiplier);
                if(!raid || raidRescueWinding<raid.defenseTuning.rescuePullSeconds)return;
                raid.RescueDefenseVictim(target.Motor);
                raidRescueWinding=0;
            }
            float separation = Safe(friendTetherTuning.stopDistance, 1.8f, .8f, Mathf.Max(.8f, TetherMaxRange - .25f));
            if (distance <= separation) return;
            float step = Mathf.Min(distance - separation, Safe(friendTetherTuning.pullSpeed, 2.5f, .1f, 4) * Time.fixedDeltaTime);
            Vector3 motion = offset / distance * step;
            float vertical = Safe(target.Motor.tuning.verticalSpeed, 2, 0, 4) * Time.fixedDeltaTime;
            motion.y = Mathf.Clamp(motion.y, -vertical, vertical);
            target.Motor.ApplyTetherMotion(motion);
        }

        // The exact ray first; when it misses a fish, a fat sphere-cast gives thin bodies a fair hit.
        // The sphere's contact point is on the collider surface, so the anchor still sits on the skin.
        public static bool AimCast(FishingTuning tuning, Vector3 eye, Vector3 direction, out RaycastHit hit)
        {
            bool didHit=Physics.Raycast(eye, direction, out hit, tuning.maxRange, tuning.hitMask, QueryTriggerInteraction.Ignore);
            if(didHit && hit.collider.GetComponentInParent<FishActor>()!=null) return true;
            if(tuning.hitAssistRadius<=0) return didHit;
            if(Physics.SphereCast(eye, tuning.hitAssistRadius, direction, out var wide, tuning.maxRange, tuning.hitMask, QueryTriggerInteraction.Ignore)
                && wide.collider.GetComponentInParent<FishActor>()!=null
                && (!didHit || wide.distance<=hit.distance+tuning.hitAssistRadius))
            { hit=wide; return true; }
            return didHit;
        }

        bool FriendLineVisible(HarpoonTool target)
        {
            Vector3 from = transform.position + Vector3.up * .6f, to = target.transform.position + Vector3.up * .6f;
            Vector3 offset = to - from;
            if (offset.sqrMagnitude < .0001f) return true;
            return !Physics.Raycast(from, offset.normalized, out var hit, offset.magnitude, tuning.hitMask, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<DiverMotor>() == target.Motor;
        }

        void ReleaseOutgoingFriend()
        {
            var target = ToolOf(_friendTarget.Value);
            _friendTarget.Value = null;
            _impact.Value = default; _friendObstructed = 0; _friendLastPull = _friendExpiresAt = 0;
            if (target != null && target._friendSource.Value == NetworkObject) target._friendSource.Value = null;
        }

        void ReleaseIncomingFriend()
        {
            var source = ToolOf(_friendSource.Value);
            _friendSource.Value = null;
            if (source != null && source._friendTarget.Value == NetworkObject) source.ReleaseOutgoingFriend();
        }

        static bool CanTetherParticipant(HarpoonTool tool) => tool != null && tool.IsServerInitialized && tool.Owner.IsActive
            && tool.tuning != null && tool.friendTetherTuning != null && tool.Motor != null && tool.Motor.tuning != null
            && !tool.Motor.IsHelpless;
        static bool HasFishingLine(HarpoonTool tool) => tool._fish != null && tool._fish.Mode >= FishMode.Hooked && tool._fish.Mode <= FishMode.Tired;
        static HarpoonTool ToolOf(NetworkObject target) => target == null ? null : target.GetComponent<HarpoonTool>();
        static DiverMotor MotorOf(NetworkObject target) => target == null ? null : target.GetComponent<DiverMotor>();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Safe(float value, float fallback, float minimum, float maximum) => Mathf.Clamp(Finite(value) ? value : fallback, minimum, maximum);

        private void OnFishChanged(NetworkObject previous, NetworkObject next, bool asServer)
            => _fish = next == null ? null : next.GetComponent<FishActor>();

        private void OnGUI()
        {
            if(UI.UiInputGate.Active) return;
            if (!IsClientInitialized || !IsOwner) return;
            GUI.Box(new Rect(Screen.width - 376f, Screen.height - 112f, 360f, 96f), "HARPOON");
            string status = _fish == null ? "Aim at a fish — LMB to fire" : _fish.Mode == FishMode.Captured
                ? $"CARGO: {_fish.SpeciesId} — bring to container" : $"{_fish.Mode}   Tension {_fish.Tension:P0}   Stamina {_fish.Stamina:F1}";
            GUI.Label(new Rect(Screen.width - 364f, Screen.height - 82f, 340f, 24f), status);
            GUI.Label(new Rect(Screen.width - 364f, Screen.height - 54f, 340f, 24f), "Hold LMB: reel / ease during bursts   E: help   R: drop");
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            if (_ownsFriendTuning && friendTetherTuning != null) Destroy(friendTetherTuning);
        }
    }
}
