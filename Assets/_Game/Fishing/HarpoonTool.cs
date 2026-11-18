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
        public Material ropeMaterial;
        public float ReelMultiplier = 1f;
        private static readonly List<HarpoonTool> ServerTools = new();
        private readonly SyncVar<NetworkObject> _fishObject = new();
        private FishActor _fish;
        private InputActionMap _actions;
        private InputAction _fire, _release, _assist;
        private LineRenderer _line;
        private float _nextHeartbeat, _nextFire, _lastControls;
        private bool _reelHeld, _assistHeld;
        private HarpoonTool _assistant;
        private float _assistedUntil;

        public DiverMotor Motor { get; private set; }
        public FishActor HookedFish => _fish;
        public FishActor CarriedFish => _fish != null && _fish.Mode == FishMode.Captured ? _fish : null;
        public bool Reeling => IsServerInitialized && _reelHeld && Time.unscaledTime - _lastControls < tuning.inputTimeout;
        public bool IsAssisted => IsServerInitialized && _assistant != null && _assistant._assistHeld
            && Time.unscaledTime < _assistedUntil && _assistant.Motor.Oxygen > 0f
            && Vector3.Distance(transform.position, _assistant.transform.position) <= tuning.assistRange;

        private void Awake()
        {
            Motor = GetComponent<DiverMotor>();
            _fishObject.OnChange += OnFishChanged;
        }

        public override void OnStartServer() => ServerTools.Add(this);

        public override void OnStopServer()
        {
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
            if (_line != null) Destroy(_line.gameObject);
        }

        private void Update()
        {
            if (!IsClientInitialized || !IsOwner || _actions == null || tuning == null) return;
            bool controlling = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UI.UiInputGate.Blocked;
            if (controlling && _fire.WasPressedThisFrame() && _fish == null) FireServer();
            if (controlling && _release.WasPressedThisFrame()) ReleaseServer();
            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + Mathf.Max(0.02f, tuning.heartbeatInterval);
            SetControlsServer(controlling && _fire.IsPressed(), controlling && _assist.IsPressed() && (!UI.UiInputGate.Active || UI.UiInputGate.AllowAssist));
        }

        [ServerRpc]
        private void FireServer()
        {
            if (tuning == null || _fish != null || Motor.Oxygen <= 0f || Time.unscaledTime < _nextFire) return;
            _nextFire = Time.unscaledTime + Mathf.Max(0.1f, tuning.fireCooldown);
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            bool didHit=Physics.Raycast(eye, Motor.AimDirection, out var hit, tuning.maxRange, tuning.hitMask, QueryTriggerInteraction.Ignore);
            var fish = didHit?hit.collider.GetComponentInParent<FishActor>():null;
            if (fish != null && fish.TryHook(this))
            {
                _fish = fish;
                _fishObject.Value = fish.NetworkObject;
            }
            else ThreatDirector.Instance?.Strike(eye,Motor.AimDirection,ReelMultiplier);
        }

        [ServerRpc]
        private void SetControlsServer(bool reel, bool assist)
        {
            _reelHeld = reel;
            _assistHeld = assist;
            _lastControls = Time.unscaledTime;
        }

        [ServerRpc]
        private void ReleaseServer() => DropCargo();

        public void DropCargo()
        {
            if (IsServerInitialized && _fish != null) _fish.ReleaseHook(this);
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
            if (!IsServerInitialized || tuning == null || !_assistHeld || Motor.Oxygen <= 0f
                || Time.unscaledTime - _lastControls >= tuning.inputTimeout) return;
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
            _line.enabled = _fish != null && _fish.Mode >= FishMode.Hooked && _fish.Mode <= FishMode.Tired;
            if (!_line.enabled) return;
            _line.SetPosition(0, transform.position + Vector3.up * 0.6f + transform.right * 0.2f);
            _line.SetPosition(1, _fish.transform.position);
        }

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

        private void OnDestroy() => _actions?.Dispose();
    }
}
