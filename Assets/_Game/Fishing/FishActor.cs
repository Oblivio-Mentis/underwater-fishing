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
        public FishSpeciesProfile speciesProfile;
        public string fishId = "shallow-small";
        public int biomass = 2, chitin, offering = 1, cargoSize = 1, depthTier = 1;
        [Min(0.1f)] public float power = 1f;

        public struct FishSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public FishMode Mode;
            public FishBehaviour Behaviour;
            public float Tension, Stamina, TugStrength;
            public bool Active, Stowed, Reserved, Startled, Coordinated, SurgeWarning, Surging, SoloTow;
            public int HookCount, HookSequence, TugSequence;
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
        private MaterialPropertyBlock _tint, _flashBlock;
        private float appliedFlash = -1;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private FsmPresentationDriver _presentation;
        private bool _depthActive;
        private long _cargoId;
        // Client-side follow: the replicated position is extrapolated by its own velocity and
        // smoothed, so a 20 Hz snapshot stream reads as continuous swimming.
        private Vector3 netVelocity, followVelocity;
        private float netReceivedAt;

        public event Action<UniversalFsmLayerStateChange> LayerStateChanged;
        public FishMode Mode => _snapshot.Value.Mode;
        public float Tension => _snapshot.Value.Tension;
        public float Stamina => _snapshot.Value.Stamina;
        public FishMode DesiredState { get; private set; } = FishMode.Roam;
        public HarpoonTool Hooker => _carrier != null ? _carrier : lines.Count > 0 ? lines.At(0).Tool : null;
        public int HookCount => _snapshot.Value.HookCount;
        public bool Coordinated => _snapshot.Value.Coordinated;
        public bool SoloTow => _snapshot.Value.SoloTow;
        public bool SurgeWarning => _snapshot.Value.SurgeWarning;
        public bool Surging => _snapshot.Value.Surging;
        public FishBehaviour Behaviour => _snapshot.Value.Behaviour;
        /// <summary>Increments once per server-confirmed hook attachment (any line).</summary>
        public int HookSequence => _snapshot.Value.HookSequence;
        /// <summary>Increments once at the start of each confirmed power surge or tug.</summary>
        public int TugSequence => _snapshot.Value.TugSequence;
        /// <summary>0..1 normalized current pull on the lines, for viewmodel and camera only.</summary>
        public float TugStrength => _snapshot.Value.TugStrength;
        public string SpeciesId => fishId;
        public bool RequiresPartner => cargoSize > 1;
        public long CargoId => IsServerInitialized ? _cargoId : _snapshot.Value.CaptureId;
        public CatchItem ToCargo(long id)
        {
            var (scrap,electronics) = SpeciesId switch
            {
                "Bristlefish" => (depthTier > 1 ? 2 : 1,0),
                "Sailray" => (depthTier > 1 ? 2 : 1,depthTier > 1 ? 1 : 0),
                "Lanternfish" when depthTier > 1 => (0,1),
                _ => (0,0)
            };
            return new CatchItem(id,biomass,chitin,offering,cargoSize,depthTier,scrap,electronics,SpeciesId);
        }
        public bool IsStowed => _snapshot.Value.Stowed;
        public bool IsHeldByLocalPlayer => Mode == FishMode.Captured && IsClientInitialized
            && (_snapshot.Value.Holder == NetworkManager.ClientManager.Connection.ClientId
                || UI.UiRoot.Instance?.Context?.Player?.Spectating?.OwnerId == _snapshot.Value.Holder);

        private void RenewCargoId()
        {
            _cargoId=BitConverter.ToInt64(Guid.NewGuid().ToByteArray(),0)&long.MaxValue;
            var state=_snapshot.Value;
            state.CaptureId=_cargoId;
            _snapshot.Value=state;
        }

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
            _snapshot.Value = new FishSnapshot { Position = _origin, Rotation = transform.rotation, Mode = FishMode.Roam, Active = _depthActive, CaptureId=_cargoId };
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
            lastVisualActive = false;
            PresentWake(default,false,true);
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
            goshaReserved=false;goshaRespawn=0;
            ClearFishing();
            arrivalPending=false;
            fleeRemaining=0;
            ResetSteering();
            hookAvailableAt=avoidUntil=0;
            _depthActive = active;
            transform.position = _origin;
            DesiredState = FishMode.Roam;
            _runtime?.Reset();
            var state = _snapshot.Value;
            state.Position = _origin;
            state.Rotation = transform.rotation;
            state.Startled = state.Reserved = false;
            state.Active = active;
            state.Stowed = false;
            state.Holder = -1;
            state.Mode = FishMode.Roam;
            state.HookCount = 0;
            state.SoloTow = false; state.Coordinated = state.SurgeWarning = state.Surging = false; state.TugStrength = 0; state.Behaviour = FishBehaviour.None;
            state.Tension = state.Stamina = 0f;
            state.Behaviour = FishBehaviour.None;
            _snapshot.Value = state;
            if (active) RenewCargoId();
            foreach (var fishCollider in _colliders) fishCollider.enabled = active;
        }

        public void EnterState(FishMode mode)
        {
            _elapsed = 0f;
            DesiredState = mode;
            var state = _snapshot.Value;
            bool exhausted=state.Mode==FishMode.Tired;
            state.Mode = mode;
            if (mode == FishMode.Captured)
            {
                // The collecting diver is chosen by the server when its own line reaches its hands.
                _carrier = landingTool;
                if(_carrier!=null && RaidWorld.Instance!=null)
                    RaidWorld.Instance.RecordCatch(_cargoId,LandingOwners(_carrier,exhausted));
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
                state.SoloTow = false; state.Coordinated = state.SurgeWarning = state.Surging = false; state.TugStrength = 0; state.Behaviour = FishBehaviour.Carried;
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
                state.SoloTow = false; state.Coordinated = state.SurgeWarning = state.Surging = false; state.TugStrength = 0; state.Behaviour = FishBehaviour.Flee;
            }
            if (mode == FishMode.Transported || mode == FishMode.Captured) foreach (var fishCollider in _colliders) fishCollider.enabled = false;
            if (mode == FishMode.Roam) foreach (var fishCollider in _colliders) fishCollider.enabled = _depthActive;
            _snapshot.Value = state;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized || _runtime == null) return;
            if(goshaReserved)return;
            if(goshaRespawn>0 && Time.time>=goshaRespawn){SetDepthActive(true);return;}
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
            if (TryMigrate()) return;
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
                    state.Behaviour=FishBehaviour.Cruise;
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
            state.CaptureId=_cargoId;
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
            var visual = _snapshot.Value;
            bool snap = visual.Active && (!lastVisualActive || (transform.position - visual.Position).sqrMagnitude > 64);
            if (!IsServerInitialized && visual.Active)
            {
                if (snap) { transform.position = visual.Position; followVelocity = Vector3.zero; netVelocity = Vector3.zero; }
                else
                {
                    bool carried = visual.Mode == FishMode.Captured || visual.Mode == FishMode.Transported;
                    float age = Mathf.Clamp(Time.time - netReceivedAt, 0, .25f);
                    Vector3 target = carried ? visual.Position : visual.Position + netVelocity * age;
                    transform.position = Vector3.SmoothDamp(transform.position, target, ref followVelocity, carried ? .03f : .14f, 40f, Time.deltaTime);
                }
                // Heading settles over a longer window than position so turns read as arcs, not steps.
                float turnBlend = snap ? 1 : 1f - Mathf.Exp(-Time.deltaTime / .22f);
                transform.rotation = Quaternion.Slerp(transform.rotation, visual.Rotation, turnBlend);
            }
            lastVisualActive = visual.Active;
            bool collidable = !visual.Reserved && visual.Active && Mode != FishMode.Transported && Mode != FishMode.Captured;
            bool visible = visual.Active && Mode != FishMode.Transported && !visual.Stowed && !IsHeldByLocalPlayer;
            PresentWake(visual,visible && !visual.Reserved,snap);
            PresentFx(visual,visible && !visual.Reserved);
            if(_model!=null && _model.TryGetComponent<CatchFishModel>(out var specimen) && specimen.CargoId!=CargoId)specimen.CargoId=CargoId;
            foreach (var body in _renderers) body.enabled = visible;
            if (!IsServerInitialized) foreach (var fishCollider in _colliders) fishCollider.enabled = collidable;
        }

        private void OnDestroy() { DisposeWake(); DisposeFx(); }

        private void OnSnapshotChanged(FishSnapshot previous, FishSnapshot next, bool asServer)
        {
            if (!asServer && previous.Mode != next.Mode)
                LayerStateChanged?.Invoke(new UniversalFsmLayerStateChange(0, "Default", (int)previous.Mode, (int)next.Mode));
            if (!asServer)
            {
                float gap = Time.time - netReceivedAt;
                if (gap > .001f && gap < .5f && previous.Active && next.Active)
                    netVelocity = Vector3.Lerp(netVelocity, (next.Position - previous.Position) / gap, .6f);
                else netVelocity = Vector3.zero;
                netReceivedAt = Time.time;
            }
        }

        // 0..1 pale flash over the whole body. Slot 1 shares the tint block so the stripe keeps its colour.
        internal void ApplyFlash(float amount)
        {
            if (_model == null) return;
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - appliedFlash) < .004f) return;
            appliedFlash = amount;
            _flashBlock ??= new MaterialPropertyBlock();
            _tint.SetFloat(FlashId, amount);
            _flashBlock.SetFloat(FlashId, amount);
            foreach (var body in _renderers)
            {
                int slots = body.sharedMaterials.Length;
                for (int i = 0; i < slots; i++) body.SetPropertyBlock(i == 1 ? _tint : _flashBlock, i);
            }
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
