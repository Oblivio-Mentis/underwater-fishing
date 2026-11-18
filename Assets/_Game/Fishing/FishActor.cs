using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace Raid
{
    public sealed class FishActor : NetworkBehaviour, IUniversalFsmStateChangeSource
    {
        public FishingTuning tuning;
        public UniversalFsmDefinitionAsset fsmDefinition;
        public FishFsmProfile fsmProfile;
        public FsmPresentationProfile presentationProfile;
        public string fishId = "shallow-small";
        public int biomass = 2, chitin, offering = 1, cargoSize = 1, depthTier = 1;
        [Min(0.1f)] public float power = 1f;

        public struct FishSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public FishMode Mode;
            public float Tension, Stamina;
            public bool Active;
        }

        private readonly SyncVar<FishSnapshot> _snapshot = new();
        private IUniversalFsmRuntimeHandle _runtime;
        private HarpoonTool _hooker;
        private Vector3 _origin;
        private float _elapsed, _clock, _obstructed;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _tint;
        private FsmPresentationDriver _presentation;
        private bool _depthActive;
        private string _speciesName;

        public event Action<UniversalFsmLayerStateChange> LayerStateChanged;
        public FishMode Mode => _snapshot.Value.Mode;
        public float Tension => _snapshot.Value.Tension;
        public float Stamina => _snapshot.Value.Stamina;
        public FishMode DesiredState { get; private set; } = FishMode.Roam;
        public HarpoonTool Hooker => _hooker;
        public string SpeciesId => _speciesName ?? fishId;
        public long CargoId => BitConverter.ToInt64(Guid.Parse(fishId).ToByteArray(),0)&long.MaxValue;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            _tint = new MaterialPropertyBlock();
            _snapshot.OnChange += OnSnapshotChanged;
            if (tuning != null) _snapshot.UpdateSettings(new SyncTypeSettings(Mathf.Max(0.02f, tuning.snapshotInterval)));
        }

        public override void OnStartServer()
        {
            if (tuning == null || fsmDefinition == null || fsmProfile == null)
                throw new InvalidOperationException("Assign fishing tuning, native FSM definition and profile to FishActor.");
            _origin = transform.position;
            _clock = _origin.x + _origin.z;
            _speciesName = fishId;
            fishId = Guid.NewGuid().ToString("N");
            _snapshot.Value = new FishSnapshot { Position = _origin, Rotation = transform.rotation, Mode = FishMode.Roam, Active = _depthActive };
            DesiredState = FishMode.Roam;
            _runtime = fsmProfile.Build(gameObject, fsmDefinition);
            _runtime.Initialize();
            foreach (var fishCollider in _colliders) fishCollider.enabled = _depthActive;
        }

        public override void OnStartClient()
        {
            if (presentationProfile == null) return;
            _presentation = GetComponent<FsmPresentationDriver>();
            if (_presentation == null) _presentation = gameObject.AddComponent<FsmPresentationDriver>();
            _presentation.profile = presentationProfile;
            _presentation.Bind(this);
            LayerStateChanged?.Invoke(new UniversalFsmLayerStateChange(0, "Default", -1, (int)Mode));
        }

        public override void OnStopServer()
        {
            if (_hooker != null) _hooker.NotifyFishLost(this);
            _hooker = null;
            _runtime?.Dispose();
            _runtime = null;
        }

        public override void OnStopClient() => _presentation?.Unbind();

        public bool TryHook(HarpoonTool tool)
        {
            if (!IsServerInitialized || !_depthActive || _runtime == null || tool == null || _hooker != null || Mode != FishMode.Roam || DesiredState != FishMode.Roam) return false;
            _hooker = tool;
            DesiredState = FishMode.Hooked;
            var state = _snapshot.Value;
            state.Stamina = Mathf.Max(1f, tuning.initialStamina * power);
            state.Tension = 0.1f;
            _snapshot.Value = state;
            return true;
        }

        public void ReleaseHook(HarpoonTool tool)
        {
            if (!IsServerInitialized || tool != _hooker || DesiredState == FishMode.Transported) return;
            _hooker = null;
            tool.NotifyFishLost(this);
            DesiredState = Mode == FishMode.Captured ? FishMode.Roam : FishMode.Escaped;
        }

        public bool MarkTransported(HarpoonTool tool)
        {
            if (!IsServerInitialized || tool == null || tool != _hooker || Mode != FishMode.Captured || DesiredState != FishMode.Captured) return false;
            DesiredState = FishMode.Transported;
            _hooker = null;
            tool.NotifyFishLost(this);
            return true;
        }

        public void SetDepthActive(bool active)
        {
            if (!IsServerInitialized) return;
            if (_hooker != null) ReleaseHook(_hooker);
            _depthActive = active;
            transform.position = _origin;
            DesiredState = FishMode.Roam;
            _runtime?.Reset();
            var state = _snapshot.Value;
            state.Position = _origin;
            state.Active = active;
            state.Mode = FishMode.Roam;
            state.Tension = state.Stamina = 0f;
            if (active) fishId = Guid.NewGuid().ToString("N");
            _snapshot.Value = state;
            foreach (var fishCollider in _colliders) fishCollider.enabled = active;
        }

        public void EnterState(FishMode mode)
        {
            _elapsed = 0f;
            _obstructed = 0f;
            DesiredState = mode;
            var state = _snapshot.Value;
            state.Mode = mode;
            if (mode == FishMode.Roam)
            {
                state.Tension = 0f;
                state.Stamina = 0f;
            }
            if (mode == FishMode.Escaped && _hooker != null)
            {
                _hooker.NotifyFishLost(this);
                _hooker = null;
            }
            if (mode == FishMode.Transported) foreach (var fishCollider in _colliders) fishCollider.enabled = false;
            if (mode == FishMode.Roam) foreach (var fishCollider in _colliders) fishCollider.enabled = _depthActive;
            _snapshot.Value = state;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized || !_depthActive || _runtime == null) return;
            _runtime.Tick(Time.fixedDeltaTime);
            _runtime.FixedTick(Time.fixedDeltaTime);
        }

        public void SimulateState(FishMode mode, float dt)
        {
            _elapsed += dt;
            _clock += dt;
            var state = _snapshot.Value;
            switch (mode)
            {
                case FishMode.Roam:
                    Vector3 patrol = _origin + new Vector3(Mathf.Sin(_clock * tuning.roamSpeed), Mathf.Sin(_clock * tuning.roamSpeed * 0.5f) * 0.25f, Mathf.Cos(_clock * tuning.roamSpeed)) * tuning.swimRadius;
                    MoveTowards(patrol, tuning.roamSpeed * dt);
                    break;
                case FishMode.Hooked:
                    if (_hooker == null) DesiredState = FishMode.Escaped;
                    else if (_elapsed >= tuning.hookSettleSeconds) DesiredState = FishMode.Struggling;
                    break;
                case FishMode.Struggling:
                case FishMode.Tired:
                    SimulateLine(ref state, mode == FishMode.Tired, dt);
                    break;
                case FishMode.Captured:
                    if (_hooker == null) DesiredState = FishMode.Roam;
                    else transform.position = _hooker.transform.position + _hooker.transform.forward * 1.2f + Vector3.up * 0.25f;
                    break;
                case FishMode.Escaped:
                    MoveTowards(_origin, tuning.escapeSpeed * dt);
                    if (_elapsed >= tuning.escapeRecoverySeconds) DesiredState = FishMode.Roam;
                    break;
                case FishMode.Transported:
                    if (_elapsed >= tuning.respawnSeconds)
                    {
                        transform.position = _origin;
                        fishId = Guid.NewGuid().ToString("N");
                        DesiredState = FishMode.Roam;
                    }
                    break;
            }
            state.Position = transform.position;
            state.Rotation = transform.rotation;
            _snapshot.Value = state;
        }

        private void SimulateLine(ref FishSnapshot state, bool tired, float dt)
        {
            if (_hooker == null) { DesiredState = FishMode.Escaped; return; }
            Vector3 anchor = _hooker.transform.position + Vector3.up * 0.6f;
            Vector3 offset = transform.position - anchor;
            float distance = offset.magnitude;
            if (distance > tuning.maxRange) { DesiredState = FishMode.Escaped; return; }
            if (Physics.Raycast(anchor, offset.normalized, out var hit, distance, tuning.hitMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<FishActor>() != this)
                _obstructed += dt;
            else _obstructed = 0f;
            if (_obstructed > tuning.obstructionGrace) { DesiredState = FishMode.Escaped; return; }

            float cooperation = _hooker.IsAssisted ? 1f + tuning.assistBonus : 1f;
            float reel = Mathf.Clamp(_hooker.ReelMultiplier, 0.1f, 10f);
            bool burst = !tired && Mathf.Repeat(_elapsed, Mathf.Max(0.1f, tuning.burstPeriod)) < tuning.burstDuration;
            if (_hooker.Reeling)
            {
                state.Tension += tuning.tensionRise * power * (burst ? tuning.burstTensionMultiplier : 1f) / (cooperation * reel) * dt;
                state.Stamina = Mathf.Max(0f, state.Stamina - tuning.exhaustionRate * reel * cooperation * (burst ? 0.25f : 1f) * dt);
                MoveTowards(anchor, tuning.reelSpeed * reel * cooperation * (burst ? 0.2f : 1f) * dt);
            }
            else
            {
                state.Tension -= tuning.tensionRelease * dt;
                if (!tired)
                {
                    state.Stamina = Mathf.Min(tuning.initialStamina * power, state.Stamina + tuning.staminaRecovery * dt);
                    Vector3 escape = offset.normalized + Vector3.down * (burst ? 0.5f : 0f);
                    transform.position += escape.normalized * tuning.escapeSpeed * power * dt;
                }
            }
            state.Tension = Mathf.Max(0f, state.Tension);
            if (state.Tension >= 1f) { DesiredState = FishMode.Escaped; return; }
            if (!tired)
            {
                _hooker.Motor.ApplyExternalMotion(offset.normalized * tuning.tugSpeed * power * (burst ? 1f : 0.1f) / cooperation * dt);
                if (state.Stamina <= 0f) DesiredState = FishMode.Tired;
            }
            else if (distance <= tuning.captureRange) DesiredState = FishMode.Captured;
        }

        private void MoveTowards(Vector3 target, float distance)
        {
            Vector3 movement = target - transform.position;
            if (movement.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(movement.normalized);
            transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(0f, distance));
        }

        private void LateUpdate()
        {
            if (!IsClientInitialized) return;
            if (!IsServerInitialized)
            {
                float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.02f, tuning.snapshotInterval));
                transform.position = Vector3.Lerp(transform.position, _snapshot.Value.Position, blend);
                transform.rotation = Quaternion.Slerp(transform.rotation, _snapshot.Value.Rotation, blend);
            }
            bool visible = _snapshot.Value.Active && Mode != FishMode.Transported;
            foreach (var body in _renderers) body.enabled = visible;
            if (!IsServerInitialized) foreach (var fishCollider in _colliders) fishCollider.enabled = visible;
        }

        private void OnSnapshotChanged(FishSnapshot previous, FishSnapshot next, bool asServer)
        {
            if (!asServer && previous.Mode != next.Mode)
                LayerStateChanged?.Invoke(new UniversalFsmLayerStateChange(0, "Default", (int)previous.Mode, (int)next.Mode));
        }

        public void SetTint(Color color)
        {
            foreach (var body in _renderers)
            {
                body.GetPropertyBlock(_tint);
                _tint.SetColor("_BaseColor", color);
                _tint.SetColor("_Color", color);
                body.SetPropertyBlock(_tint);
            }
        }
    }
}
