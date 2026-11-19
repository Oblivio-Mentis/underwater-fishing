using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    [RequireComponent(typeof(DiverMotor))]
    public sealed class HarpoonTool : NetworkBehaviour
    {
        public FishingTuning tuning;
        public FriendTetherTuning friendTetherTuning;
        public Material ropeMaterial;
        public float ReelMultiplier = 1f;
        public event System.Action<Vector3,Vector3> ShotVisual;
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
        private readonly SyncVar<NetworkObject> _pocketA = new(), _pocketB = new(), _pocketC = new();
        private readonly SyncVar<int> _slot = new(), _shotSequence = new();
        private InputAction[] _select;
        private InputAction _wheel;
        private float _friendObstructed, _friendLastPull, _friendRefuseUntil, _friendExpiresAt;
        private bool _ownsFriendTuning;

        public DiverMotor Motor { get; private set; }
        public FishActor HookedFish => _fish;
        public int ActiveSlot => _slot.Value;
        public int ShotSequence => _shotSequence.Value;
        public DiverMotor AssistTarget => MotorOf(_assistTarget.Value);
        public DiverMotor AssistingDiver => MotorOf(_assistant.Value);
        public bool HandsFull => _fish != null && _fish.RequiresPartner && _fish.Mode == FishMode.Captured;
        public int CatchCount => (HandsFull ? 1 : 0) + (_pocketA.Value != null ? 1 : 0)
            + (_pocketB.Value != null ? 1 : 0) + (_pocketC.Value != null ? 1 : 0);
        public bool HasFullCreel => _pocketA.Value != null && _pocketB.Value != null && _pocketC.Value != null;
        public int SmallestCatchSize => _pocketA.Value != null || _pocketB.Value != null || _pocketC.Value != null
            ? 1 : HandsFull ? _fish.cargoSize : 0;
        public FishActor CarriedFish => HandsFull ? _fish : FishInSlot(ActiveSlot) ?? FishInSlot(2) ?? FishInSlot(3) ?? FishInSlot(4);

        private SyncVar<NetworkObject> Pocket(int slot) => slot switch { 2 => _pocketA, 3 => _pocketB, 4 => _pocketC, _ => null };

        public FishActor FishInSlot(int slot)
        {
            var item = Pocket(slot)?.Value;
            return item == null ? null : item.GetComponent<FishActor>();
        }

        public bool SelectSlot(int slot)
        {
            if (!IsClientInitialized || !IsOwner || UI.UiInputGate.Blocked || !CanSelect(slot)) return false;
            SelectSlotServer(slot);
            return true;
        }

        private bool CanSelect(int slot) => slot >= 0 && slot < 5 && !Motor.IsHelpless && !HandsFull
            && !TetherActive && _fish == null && AssistTarget == null;

        [ServerRpc(RequireOwnership = true)]
        private void SelectSlotServer(int slot)
        {
            if (CanSelect(slot)) _slot.Value = slot;
        }
        public bool OwnsFriendTether => _friendTarget.Value != null;
        public bool IsPulled => _friendSource.Value != null;
        public bool TetherActive => OwnsFriendTether || IsPulled;
        public DiverMotor TetherOwner => IsPulled ? MotorOf(_friendSource.Value) : OwnsFriendTether ? Motor : null;
        public DiverMotor TetherTarget => OwnsFriendTether ? MotorOf(_friendTarget.Value) : IsPulled ? Motor : null;
        public float TetherOptOutRemaining => _optOutRemaining.Value;
        public float TetherMaxRange => Mathf.Min(Safe(tuning == null ? 0 : tuning.maxRange, 0, 0, 100), Safe(friendTetherTuning == null ? 12 : friendTetherTuning.maximumRange, 12, 2, 20));
        public bool Reeling => IsServerInitialized && _reelHeld && Time.unscaledTime - _lastControls < tuning.inputTimeout;
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
            if (!ServerTools.Contains(this)) ServerTools.Add(this);
            _friendTarget.Value = null; _friendSource.Value = null; _optOutRemaining.Value = 0;
            _friendRefuseUntil = _friendObstructed = 0;
            _friendLastPull = _lastControls = Time.unscaledTime;
            _reelHeld = _assistHeld = false;
        }

        public override void OnStopServer()
        {
            ReleaseAll();
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
            _select = new InputAction[5];
            for (int slot = 0; slot < _select.Length; slot++)
                _select[slot] = _actions.AddAction("Slot" + (slot + 1), InputActionType.Button, "<Keyboard>/" + (slot + 1));
            _wheel = _actions.AddAction("Cycle", InputActionType.Value, "<Mouse>/scroll/y");
            _actions.Enable();
        }

        public override void OnStopClient()
        {
            _actions?.Dispose();
            _actions = null;
            if (_line != null) { _line.gameObject.SetActive(false); Destroy(_line.gameObject); _line = null; }
        }

        private void Update()
        {
            if (!IsClientInitialized || !IsOwner || _actions == null || tuning == null) return;
            bool controlling = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UI.UiInputGate.Blocked;
            // A pulled player can always refuse with R while focused, including from a local menu.
            // All firing/reeling/assisting remains behind the shared gameplay-input gate.
            if (controlling && !Motor.IsHelpless)
            {
                for (int i = 0; i < _select.Length; i++)
                    if (_select[i].WasPressedThisFrame()) SelectSlot(i);
                float wheel = _wheel.ReadValue<float>();
                if (Mathf.Abs(wheel) > .01f) SelectSlot((ActiveSlot + (wheel > 0 ? 4 : 1)) % 5);
            }
            if (controlling && ActiveSlot == 0 && AssistTarget == null && _fire.WasPressedThisFrame() && _fish == null && !TetherActive) FireServer();
            if (Application.isFocused && _release.WasPressedThisFrame() && (controlling || IsPulled)) ReleaseServer();
            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + Mathf.Max(0.02f, tuning.heartbeatInterval);
            bool gripInput = _assist.IsPressed() && (!UI.UiInputGate.Active || UI.UiInputGate.AllowAssist || AssistTarget != null)
                || ActiveSlot == 1 && _fire.IsPressed();
            bool reel = controlling && ActiveSlot == 0 && _fire.IsPressed();
            bool assist = controlling && gripInput;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!UI.UiInputGate.Blocked)
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
            if (tuning == null || ActiveSlot != 0 || AssistTarget != null || _fish != null || TetherActive || Motor.IsHelpless || Motor.Oxygen <= 0f || Time.unscaledTime < _nextFire) return;
            _nextFire = Time.unscaledTime + Mathf.Max(0.1f, tuning.fireCooldown);
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            bool didHit=Physics.Raycast(eye, Motor.AimDirection, out var hit, tuning.maxRange, tuning.hitMask, QueryTriggerInteraction.Ignore);
            _shotSequence.Value++;
            ShotVisualObservers(eye,didHit?hit.point:eye+Motor.AimDirection*tuning.maxRange);
            var friend = didHit ? hit.collider.GetComponentInParent<DiverMotor>() : null;
            if (friend != null)
            {
                TryHookFriend(friend.GetComponent<HarpoonTool>());
                return; // Hitting a player never falls through to threat damage.
            }
            var fish = didHit?hit.collider.GetComponentInParent<FishActor>():null;
            if (fish != null && (fish.RequiresPartner || !HasFullCreel) && fish.TryHook(this))
            {
                _fish = fish;
                _fishObject.Value = fish.NetworkObject;
            }
            else if (fish == null) ThreatDirector.Instance?.Strike(eye,Motor.AimDirection,ReelMultiplier);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetControlsServer(bool reel, bool assist)
        {
            _reelHeld = reel && !Motor.IsHelpless && ActiveSlot == 0;
            _assistHeld = assist && !Motor.IsHelpless && ActiveSlot <= 1;
            _lastControls = Time.unscaledTime;
        }

        // Acknowledges a real accepted shot, including misses; attachment still comes from replicated hook state.
        [ObserversRpc]
        private void ShotVisualObservers(Vector3 origin, Vector3 endpoint)
        {
            ShotVisual?.Invoke(origin, endpoint);
        }

        [ServerRpc(RequireOwnership = true)]
        private void ReleaseServer()
        {
            // Refusal is owned by the target's RPC; the shooter cannot shorten or bypass this period.
            _friendRefuseUntil = Time.unscaledTime + Safe(friendTetherTuning.optOutSeconds, 3, 3, 10);
            _optOutRemaining.Value = _friendRefuseUntil - Time.unscaledTime;
            if (IsPulled) { ReleaseIncomingFriend(); return; }
            if (OwnsFriendTether) { ReleaseOutgoingFriend(); return; }
            if (AssistTarget != null) { DetachGrip(); _assistHeld = false; return; }
            if (_fish != null) _fish.ReleaseHook(this);
            else FishInSlot(ActiveSlot)?.ReleaseHook(this);
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
            if (!IsClientInitialized || !IsOwner || Motor.IsHelpless || UI.UiInputGate.Blocked) return false;
            float until = Time.unscaledTime + Mathf.Clamp(duration, 0, 15);
            if (grip) _qaGripUntil = until;
            else _qaReelUntil = until;
            return true;
        }

        // Explicit QA only: still dispatches the normal owned RPC and leaves hit/cooldown validation on the server.
        internal bool QaFireOwned()
        {
            if (!IsClientInitialized || !IsOwner || tuning == null || Motor == null || Motor.Oxygen <= 0
                || UI.UiInputGate.Blocked || ActiveSlot != 0 || Motor.IsHelpless || AssistTarget != null || _fish != null || TetherActive) return false;
            FireServer(); return true;
        }

        internal bool QaReleaseOwned()
        {
            if (!IsClientInitialized || !IsOwner || (UI.UiInputGate.Blocked && !IsPulled)
                || (_fish == null && FishInSlot(ActiveSlot) == null && AssistTarget == null && !TetherActive)) return false;
            ReleaseServer(); return true;
        }
#endif

        public void DropCargo()
        {
            if (!IsServerInitialized) return;
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            ReleaseOutgoingFriend();
            ReleaseIncomingFriend();
            DetachGrip();
            ToolOf(_assistant.Value)?.DetachGrip();
            if (_fish != null) _fish.ReleaseHook(this);
            for (int slot = 2; slot < 5; slot++) FishInSlot(slot)?.ReleaseHook(this);
        }

        public bool DropOneCatch()
        {
            var fish = CarriedFish;
            if (!IsServerInitialized || fish == null) return false;
            fish.ReleaseHook(this);
            return true;
        }

        public bool TakeCargo()
        {
            var fish = CarriedFish;
            return IsServerInitialized && fish != null && fish.MarkTransported(this);
        }

        internal bool StowCapture(FishActor fish)
        {
            if (!IsServerInitialized || _fish != fish || fish.RequiresPartner) return false;
            for (int slot = 2; slot < 5; slot++)
            {
                var pocket = Pocket(slot);
                if (pocket.Value != null) continue;
                pocket.Value = fish.NetworkObject;
                _fish = null;
                _fishObject.Value = null;
                return true;
            }
            return false;
        }

        public void NotifyFishLost(FishActor fish)
        {
            if (!IsServerInitialized || fish == null) return;
            if (_fish == fish)
            {
                _fish = null;
                _fishObject.Value = null;
                ToolOf(_assistant.Value)?.DetachGrip();
            }
            for (int slot = 2; slot < 5; slot++)
                if (Pocket(slot).Value == fish.NetworkObject) Pocket(slot).Value = null;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized) return;
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
            if (!_assistHeld || tuning == null || TetherActive || _fish != null || ActiveSlot > 1
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
            && shooter.gameObject.scene == gameObject.scene && shooter._fish != null && shooter._fish.Mode != FishMode.Captured
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
            var friend = TetherTarget;
            bool outgoingFriend = OwnsFriendTether && friend != null;
            _line.enabled = outgoingFriend || _fish != null && _fish.Mode >= FishMode.Hooked && _fish.Mode <= FishMode.Tired;
            if (!_line.enabled) return;
            _line.SetPosition(0, transform.position + Vector3.up * 0.6f + transform.right * 0.2f);
            _line.SetPosition(1, outgoingFriend ? friend.transform.position + Vector3.up * .6f : _fish.transform.position);
        }

        bool TryHookFriend(HarpoonTool target)
        {
            if (target == null || target == this || !CanTetherParticipant(this) || !CanTetherParticipant(target)) return false;
            if (TetherActive || _fish != null || target.TetherActive || target.AssistTarget != null || target.HandsFull || HasFishingLine(target)) return false;
            if (target.gameObject.scene != gameObject.scene || Time.unscaledTime < target._friendRefuseUntil) return false;
            if (Vector3.Distance(transform.position, target.transform.position) > TetherMaxRange || !FriendLineVisible(target)) return false;
            _friendTarget.Value = target.NetworkObject;
            target._friendSource.Value = NetworkObject;
            _friendObstructed = 0; _friendLastPull = Time.unscaledTime;
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
            float separation = Safe(friendTetherTuning.stopDistance, 1.8f, .8f, Mathf.Max(.8f, TetherMaxRange - .25f));
            if (distance <= separation) return;
            float step = Mathf.Min(distance - separation, Safe(friendTetherTuning.pullSpeed, 2.5f, .1f, 4) * Time.fixedDeltaTime);
            Vector3 motion = offset / distance * step;
            float vertical = Safe(target.Motor.tuning.verticalSpeed, 2, 0, 4) * Time.fixedDeltaTime;
            motion.y = Mathf.Clamp(motion.y, -vertical, vertical);
            target.Motor.ApplyTetherMotion(motion);
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
            _friendTarget.Value = null; _friendObstructed = 0; _friendLastPull = _friendExpiresAt = 0;
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
            && !tool.Motor.IsHelpless && tool.Motor.Oxygen > 0;
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
