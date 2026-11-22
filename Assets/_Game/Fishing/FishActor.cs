using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace Raid
{
    public sealed partial class FishActor : NetworkBehaviour, IUniversalFsmStateChangeSource
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
            public bool Active, Stowed, Startled, Coordinated, SurgeWarning, Surging;
            public int HookCount;
            public int Holder;
            public long CaptureId;
        }

        private readonly SyncVar<FishSnapshot> _snapshot = new();
        private IUniversalFsmRuntimeHandle _runtime;
        private HarpoonTool _carrier;
        private Vector3 _origin;
        private float _elapsed, _clock;
        private Renderer[] _renderers;
        private GameObject _model;
        private Collider[] _colliders;
        private MaterialPropertyBlock _tint;
        private FsmPresentationDriver _presentation;
        private bool _depthActive;
        private long _cargoId;

        public event Action<UniversalFsmLayerStateChange> LayerStateChanged;
        public FishMode Mode => _snapshot.Value.Mode;
        public float Tension => _snapshot.Value.Tension;
        public float Stamina => _snapshot.Value.Stamina;
        public FishMode DesiredState { get; private set; } = FishMode.Roam;
        public HarpoonTool Hooker => _carrier != null ? _carrier : lines.Count > 0 ? lines.At(0).Tool : null;
        public int HookCount => _snapshot.Value.HookCount;
        public bool Coordinated => _snapshot.Value.Coordinated;
        public bool SurgeWarning => _snapshot.Value.SurgeWarning;
        public bool Surging => _snapshot.Value.Surging;
        public string SpeciesId => fishId;
        public bool RequiresPartner => cargoSize > 1;
        public long CargoId => IsServerInitialized ? _cargoId : _snapshot.Value.CaptureId;
        public CatchItem ToCargo(long id) => new CatchItem(id, biomass, chitin, offering, cargoSize, depthTier, species: SpeciesId);
        public bool IsStowed => _snapshot.Value.Stowed;
        public bool IsHeldByLocalPlayer => Mode == FishMode.Captured && IsClientInitialized
            && (_snapshot.Value.Holder == NetworkManager.ClientManager.Connection.ClientId
                || UI.UiRoot.Instance?.Context?.Player?.Spectating?.OwnerId == _snapshot.Value.Holder);

        private void RenewCargoId() => _cargoId = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;

        Transform tailSurface;
        public Transform ImpactSurface(bool tail) => _model == null ? transform : tail ? tailSurface : _model.transform;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            if (CatchFishModel.Supports(SpeciesId))
            {
                foreach (var original in _renderers) original.enabled=false;
                foreach (var hull in _colliders) hull.enabled=false;
                _model=CatchFishModel.Create(transform,SpeciesId);
                tailSurface=_model.GetComponent<CatchFishModel>().Tail;
                _renderers=_model.GetComponentsInChildren<Renderer>();
                _colliders=new Collider[] { SurfaceCollider(_model.transform),SurfaceCollider(tailSurface) };
            }
            _tint = new MaterialPropertyBlock();
            _snapshot.OnChange += OnSnapshotChanged;
            if (tuning != null) _snapshot.UpdateSettings(new SyncTypeSettings(Mathf.Max(0.02f, tuning.snapshotInterval)));
        }

        private MeshCollider SurfaceCollider(Transform part)
        {
            part.gameObject.layer=gameObject.layer;
            var surface=part.gameObject.AddComponent<MeshCollider>();
            surface.sharedMesh=part.GetComponent<MeshFilter>().sharedMesh;
            surface.enabled=false;
            return surface;
        }

        public override void OnStartServer()
        {
            ServerSchool.Add(this);
            if (tuning == null || fsmDefinition == null || fsmProfile == null)
                throw new InvalidOperationException("Assign fishing tuning, native FSM definition and profile to FishActor.");
            _origin = transform.position;
            _depthActive=false;
            _clock = _origin.x + _origin.z;
            RenewCargoId();
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
            ServerSchool.Remove(this);
            arrivalPending=false;
            ClearFishing();
            _runtime?.Dispose();
            _runtime = null;
        }

        public override void OnStopClient()
        {
            _presentation?.Unbind();
            foreach (var body in _renderers) body.enabled = false;
        }

        public bool MarkTransported(HarpoonTool tool)
        {
            if (!IsServerInitialized || tool == null || tool != _carrier || Mode != FishMode.Captured || DesiredState != FishMode.Captured) return false;
            DesiredState = FishMode.Transported;
            _carrier = null;
            tool.NotifyFishLost(this);
            return true;
        }

        public void SetDepthActive(bool active)
        {
            if (!IsServerInitialized) return;
            ClearFishing();
            arrivalPending=false;
            fleeRemaining=0;
            hookAvailableAt=avoidUntil=0;
            _depthActive = active;
            transform.position = _origin;
            DesiredState = FishMode.Roam;
            _runtime?.Reset();
            var state = _snapshot.Value;
            state.Position = _origin;
            state.Rotation = transform.rotation;
            state.Startled = false;
            state.Active = active;
            state.Stowed = false;
            state.Holder = -1;
            state.Mode = FishMode.Roam;
            state.HookCount = 0;
            state.Coordinated = state.SurgeWarning = state.Surging = false;
            state.Tension = state.Stamina = 0f;
            if (active) RenewCargoId();
            _snapshot.Value = state;
            foreach (var fishCollider in _colliders) fishCollider.enabled = active;
        }

        public void EnterState(FishMode mode)
        {
            _elapsed = 0f;
            DesiredState = mode;
            var state = _snapshot.Value;
            state.Mode = mode;
            if (mode == FishMode.Captured)
            {
                // The collecting diver is chosen by the server when its own line reaches its hands.
                _carrier = landingTool;
                state.CaptureId = _cargoId;
                state.Holder = _carrier != null ? _carrier.OwnerId : -1;
                state.Stowed = _carrier != null && _carrier.StowCapture(this);
                foreach (var line in lines)
                {
                    if (line.Tool != _carrier || state.Stowed) line.Tool.NotifyFishLost(this);
                    else line.Tool.FinishFishingLine();
                }
                if (state.Stowed) _carrier = null;
                lines.Clear();
                landingTool = null;
                state.HookCount = 0;
                state.Coordinated = state.SurgeWarning = state.Surging = false;
            }
            else { state.Stowed = false; state.Holder = -1; }
            if (mode != FishMode.Roam && mode != FishMode.Escaped) {state.Startled=false;fleeRemaining=0;}
            if (mode == FishMode.Roam)
            {
                state.Tension = 0f;
                state.Stamina = 0f;
            }
            if (mode == FishMode.Escaped)
            {
                ClearFishing();
                state.HookCount = 0;
                state.Coordinated = state.SurgeWarning = state.Surging = false;
            }
            if (mode == FishMode.Transported || mode == FishMode.Captured) foreach (var fishCollider in _colliders) fishCollider.enabled = false;
            if (mode == FishMode.Roam) foreach (var fishCollider in _colliders) fishCollider.enabled = _depthActive;
            _snapshot.Value = state;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized || _runtime == null) return;
            if(arrivalPending)TryArrive();
            if(!_depthActive)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Mode==FishMode.Roam && HookCount==0 && Time.unscaledTime<artPoseUntil)return;
#endif
            if(staggered && Mode==FishMode.Transported && _elapsed>=tuning.respawnSeconds)
            {
                ScheduleAt(Time.time+2.5f);
                return;
            }
            if(staggered && Mode==FishMode.Roam && DesiredState==FishMode.Roam && Time.time>departureAt &&
                NearestSwimmer(transform.position,14)==null && HasRoamingReplacement())
            {
                ScheduleAt(Time.time+18);
                return;
            }
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
                    if(Nereid.NereidVessel.Active!=null || fleeRemaining>0){SwimReef(dt,ref state);break;}
                    state.Startled=false;
                    Vector3 patrol = _origin + new Vector3(Mathf.Sin(_clock * tuning.roamSpeed), Mathf.Sin(_clock * tuning.roamSpeed * 0.5f) * 0.25f, Mathf.Cos(_clock * tuning.roamSpeed)) * tuning.swimRadius;
                    MoveTowards(patrol, tuning.roamSpeed * dt);
                    break;
                case FishMode.Hooked:
                    if (lines.Count == 0) DesiredState = FishMode.Escaped;
                    else if (_elapsed >= tuning.hookSettleSeconds) DesiredState = FishMode.Struggling;
                    break;
                case FishMode.Struggling:
                case FishMode.Tired:
                    SimulateLine(ref state, mode == FishMode.Tired, dt);
                    break;
                case FishMode.Captured:
                    if (state.Stowed) DesiredState = FishMode.Transported;
                    else if (_carrier == null) DesiredState = FishMode.Roam;
                    else
                    {
                        transform.position = _carrier.transform.TransformPoint(new Vector3(0, .05f, .7f));
                        transform.rotation = _carrier.transform.rotation * Quaternion.Euler(0, 90, 0);
                    }
                    break;
                case FishMode.Escaped:
                    SwimReef(dt,ref state);
                    if (_elapsed >= tuning.escapeRecoverySeconds) DesiredState = FishMode.Roam;
                    break;
                case FishMode.Transported:
                    if (!staggered && _elapsed >= tuning.respawnSeconds)
                    {
                        transform.position = _origin;
                        RenewCargoId();
                        DesiredState = FishMode.Roam;
                    }
                    break;
            }
            state.Position = transform.position;
            state.Rotation = transform.rotation;
            _snapshot.Value = state;
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
            bool collidable = _snapshot.Value.Active && Mode != FishMode.Transported && Mode != FishMode.Captured;
            bool visible = _snapshot.Value.Active && Mode != FishMode.Transported && !_snapshot.Value.Stowed && !IsHeldByLocalPlayer;
            foreach (var body in _renderers) body.enabled = visible;
            if (!IsServerInitialized) foreach (var fishCollider in _colliders) fishCollider.enabled = collidable;
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
                if (_model != null)
                {
                    // Flash lateral markings during the fight while retaining each species' back and belly colors.
                    var stripe = body.sharedMaterials[1].GetColor("_BaseColor");
                    _tint.SetColor("_BaseColor", Color.Lerp(stripe, color, Mode == FishMode.Roam ? .10f : .5f));
                    body.SetPropertyBlock(_tint, 1);
                }
                else
                {
                    _tint.SetColor("_BaseColor", color);
                    _tint.SetColor("_Color", color);
                    body.SetPropertyBlock(_tint);
                }
            }
        }
    }
}
