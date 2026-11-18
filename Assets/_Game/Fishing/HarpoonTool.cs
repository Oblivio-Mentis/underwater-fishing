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
        private HarpoonTool _assistant;
        private float _assistedUntil;
        private float _friendObstructed, _friendLastPull, _friendRefuseUntil, _friendExpiresAt;
        private bool _ownsFriendTuning;

        public DiverMotor Motor { get; private set; }
        public FishActor HookedFish => _fish;
        public FishActor CarriedFish => _fish != null && _fish.Mode == FishMode.Captured ? _fish : null;
        public bool OwnsFriendTether => _friendTarget.Value != null;
        public bool IsPulled => _friendSource.Value != null;
        public bool TetherActive => OwnsFriendTether || IsPulled;
        public DiverMotor TetherOwner => IsPulled ? MotorOf(_friendSource.Value) : OwnsFriendTether ? Motor : null;
        public DiverMotor TetherTarget => OwnsFriendTether ? MotorOf(_friendTarget.Value) : IsPulled ? Motor : null;
        public float TetherOptOutRemaining => _optOutRemaining.Value;
        public float TetherMaxRange => Mathf.Min(Safe(tuning == null ? 0 : tuning.maxRange, 0, 0, 100), Safe(friendTetherTuning == null ? 12 : friendTetherTuning.maximumRange, 12, 2, 20));
        public bool Reeling => IsServerInitialized && _reelHeld && Time.unscaledTime - _lastControls < tuning.inputTimeout;
        public bool IsAssisted => IsServerInitialized && _assistant != null && _assistant._assistHeld
            && Time.unscaledTime < _assistedUntil && _assistant.Motor.Oxygen > 0f
            && Vector3.Distance(transform.position, _assistant.transform.position) <= tuning.assistRange;

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
            ReleaseOutgoingFriend();
            ReleaseIncomingFriend();
            if (_fish != null) _fish.ReleaseHook(this);
            ServerTools.Remove(this);
            _assistant = null;
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
            if (controlling && _fire.WasPressedThisFrame() && _fish == null && !TetherActive) FireServer();
            if (Application.isFocused && _release.WasPressedThisFrame() && (controlling || IsPulled)) ReleaseServer();
            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + Mathf.Max(0.02f, tuning.heartbeatInterval);
            SetControlsServer(controlling && _fire.IsPressed(), controlling && _assist.IsPressed() && (!UI.UiInputGate.Active || UI.UiInputGate.AllowAssist));
        }

        [ServerRpc(RequireOwnership = true)]
        private void FireServer()
        {
            if (tuning == null || _fish != null || TetherActive || Motor.Oxygen <= 0f || Time.unscaledTime < _nextFire) return;
            _nextFire = Time.unscaledTime + Mathf.Max(0.1f, tuning.fireCooldown);
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            bool didHit=Physics.Raycast(eye, Motor.AimDirection, out var hit, tuning.maxRange, tuning.hitMask, QueryTriggerInteraction.Ignore);
            var friend = didHit ? hit.collider.GetComponentInParent<DiverMotor>() : null;
            if (friend != null)
            {
                TryHookFriend(friend.GetComponent<HarpoonTool>());
                return; // Hitting a player never falls through to threat damage.
            }
            var fish = didHit?hit.collider.GetComponentInParent<FishActor>():null;
            if (fish != null && fish.TryHook(this))
            {
                _fish = fish;
                _fishObject.Value = fish.NetworkObject;
            }
            else ThreatDirector.Instance?.Strike(eye,Motor.AimDirection,ReelMultiplier);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetControlsServer(bool reel, bool assist)
        {
            _reelHeld = reel;
            _assistHeld = assist;
            _lastControls = Time.unscaledTime;
        }

        [ServerRpc(RequireOwnership = true)]
        private void ReleaseServer()
        {
            // Refusal is owned by the target's RPC; the shooter cannot shorten or bypass this period.
            _friendRefuseUntil = Time.unscaledTime + Safe(friendTetherTuning.optOutSeconds, 3, 3, 10);
            _optOutRemaining.Value = _friendRefuseUntil - Time.unscaledTime;
            if (IsPulled) { ReleaseIncomingFriend(); return; }
            DropCargo();
        }

        /// <summary>Local-owner UI action. The server still checks ownership through ReleaseServer.</summary>
        public void CancelFriendTether()
        {
            if (IsClientInitialized && IsOwner && TetherActive) ReleaseServer();
        }

        public void DropCargo()
        {
            if (!IsServerInitialized) return;
            ReleaseOutgoingFriend();
            ReleaseIncomingFriend();
            if (_fish != null) _fish.ReleaseHook(this);
        }

        public bool TakeCargo()
        {
            if (!IsServerInitialized || CarriedFish == null) return false;
            return _fish.MarkTransported(this);
        }

        public void NotifyFishLost(FishActor fish)
        {
            if (!IsServerInitialized || fish != _fish) return;
            _fish = null;
            _fishObject.Value = null;
        }

        private void FixedUpdate()
        {
            if (IsServerInitialized)
            {
                _optOutRemaining.Value = Mathf.Max(0, _friendRefuseUntil - Time.unscaledTime);
                TickFriendTether();
            }
            if (!IsServerInitialized || tuning == null || !_assistHeld || Motor.Oxygen <= 0f
                || TetherActive || Time.unscaledTime - _lastControls >= tuning.inputTimeout) return;
            HarpoonTool nearest = null;
            float bestDistance = tuning.assistRange * tuning.assistRange;
            foreach (var tool in ServerTools)
            {
                if (tool == this || tool._fish == null || tool._fish.Mode == FishMode.Captured) continue;
                float distance = (tool.transform.position - transform.position).sqrMagnitude;
                if (distance > bestDistance || tool.gameObject.scene != gameObject.scene) continue;
                Vector3 from = transform.position + Vector3.up * 0.6f;
                Vector3 to = tool.transform.position + Vector3.up * 0.6f;
                if (Physics.Raycast(from, (to - from).normalized, out var hit, Vector3.Distance(from, to), tuning.hitMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<HarpoonTool>() != tool) continue;
                bestDistance = distance;
                nearest = tool;
            }
            if (nearest == null) return;
            nearest._assistant = this;
            nearest._assistedUntil = Time.unscaledTime + tuning.inputTimeout;
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
            if (TetherActive || _fish != null || target.TetherActive || HasFishingLine(target)) return false;
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
            target.Motor.ApplyExternalMotion(motion);
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
