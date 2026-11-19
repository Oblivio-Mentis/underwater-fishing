using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class DiverMotor : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<DiverMotor> ServerDivers = new();
        public DiverTuning tuning;
        public float SpeedMultiplier = 1f, OxygenMultiplier = 1f, SafeDepthBonus, DepthOffset;
        private float _helplessTime, _trappedTime;

        public struct DiverState
        {
            public Vector3 Position;
            public float Yaw;
            public float Oxygen;
            public float Health, HypoxicSeconds;
            public float Stress;
            public bool Inside;
            public bool Trapped;
            public float Pitch;
            public Quaternion BodyRotation;
            public bool Dead, Consumed, Sprinting, MayReturn;
        }

        private readonly SyncVar<DiverState> _state = new();
        private CharacterController _controller;
        private InputActionMap _actions;
        private InputAction _move, _rise, _sink, _sprint, _look, _release, _capture;
        private Camera _camera;
        private Renderer[] _body;
        private Vector3 _serverInput;
        private float _serverYaw, _serverPitch, _lastInputTime, _nextSend;
        private float _yaw, _pitch;
        private bool _serverSprint, _ownsTuning;
        private Vector3 _previousPosition;
        private float _indoorVerticalVelocity;
        private bool _serverJumpHeld, _serverJumpQueued, _localJumpQueued, _wasWalking;

        public float Oxygen => _state.Value.Oxygen;
        public float LocalLookMultiplier {get;set;} = 1f;
        public void LookAtConsole(Vector3 point)
        {
            if(!IsOwner || _camera==null)return;
            Vector3 direction=point-_camera.transform.position;
            if(direction.sqrMagnitude<.01f)return;
            var angles=Quaternion.LookRotation(direction).eulerAngles;
            _yaw=angles.y;_pitch=Mathf.Clamp(Mathf.DeltaAngle(0,angles.x),-85,85);
        }
        public float PressureStress => _state.Value.Stress;
        public bool IsInside => _state.Value.Inside;
        public bool IsWalking => UsesIndoorWalking(_state.Value.Position);
        // Ground contacts are authoritative on the server; remote replicas do not run a CharacterController.
        public bool IsGrounded => NetworkObject!=null && IsServerInitialized && _controller!=null && _controller.enabled && _controller.isGrounded;
        public float Depth => Mathf.Max(0f, DepthOffset - _state.Value.Position.y);
        public float Health => _state.Value.Health;
        public bool IsHypoxic => !IsDead && _state.Value.HypoxicSeconds>0;
        public float HypoxiaSeverity => IsHypoxic?Mathf.Clamp01(.12f+_state.Value.HypoxicSeconds/20f):0;
        public bool IsHelpless => IsDead || (Nereid.NereidVessel.Active==null && Oxygen<=0);
        public bool IsTrapped => IsServerInitialized ? _trappedTime > 0f : _state.Value.Trapped;
        public Vector3 AimDirection => Quaternion.Euler(_serverPitch, _serverYaw, 0f) * Vector3.forward;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.enabled = false;
            _body = GetComponentsInChildren<Renderer>();
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<DiverTuning>();
                _ownsTuning = true;
            }
            _state.UpdateSettings(new SyncTypeSettings(Mathf.Max(0.02f, tuning.sendInterval)));
        }

        public override void OnStartServer()
        {
            ServerDivers.Add(this);
            _controller.enabled = true;
            _previousPosition = transform.position;
            _serverYaw = transform.eulerAngles.y;
            _lastInputTime = float.NegativeInfinity;
            ResetIndoorMovement();
            _wasWalking=UsesIndoorWalking(transform.position);
            _state.Value = new DiverState
            {
                Position = transform.position,
                Yaw = _serverYaw,
                BodyRotation = transform.rotation,
                Oxygen = tuning.oxygenCapacity,
                Health = 100f,
                Inside = IsSealedInterior(transform.position)
            };
        }

        public override void OnStopServer()
        {
            ServerDivers.Remove(this);
            RemoveDeathBody();
            _controller.enabled = false;
            _serverInput = Vector3.zero;
            ResetIndoorMovement();
        }

        public override void OnStartClient()
        {
            ClientCrew.Add(this);
            if (!IsServerInitialized)
            {
                transform.SetPositionAndRotation(_state.Value.Position, Quaternion.Euler(0f, _state.Value.Yaw, 0f));
            }
            if (!IsOwner) return;
            _yaw = _state.Value.Yaw;
            _localJumpQueued=false;
            var view = new GameObject("Diver Camera", typeof(Camera));
            view.transform.SetParent(transform, false);
            view.transform.localPosition = Vector3.up * 0.6f;
            view.tag = "MainCamera";
            _camera = view.GetComponent<Camera>();
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 160f;
            _camera.fieldOfView = 80f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = RenderSettings.fogColor;
            if(Nereid.NereidVessel.Active!=null)view.AddComponent<HypoxiaLens>().Bind(this);
            foreach (var bodyRenderer in _body) bodyRenderer.enabled = false;
            BuildInput();
            bool lockCursor=!UI.UiInputGate.Active || (Application.isFocused&&!UI.UiInputGate.Blocked);
            Cursor.lockState = lockCursor?CursorLockMode.Locked:CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        public override void OnStopClient()
        {
            ClientCrew.Remove(this);
            Spectating=null;
            if(deathShade!=null)Destroy(deathShade.transform.parent.gameObject);
            _localJumpQueued=false;
            if (_camera == null) return;
            _actions?.Disable();
            _actions?.Dispose();
            _actions = null;
            Destroy(_camera.gameObject);
            _camera = null;
            foreach (var bodyRenderer in _body) bodyRenderer.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void BuildInput()
        {
            _actions = new InputActionMap("Diver");
            _move = _actions.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _rise = _actions.AddAction("Rise", InputActionType.Button, "<Keyboard>/space");
            _sink = _actions.AddAction("Sink", InputActionType.Button, "<Keyboard>/leftCtrl");
            _sprint = _actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _look = _actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            _release = _actions.AddAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");
            _capture = _actions.AddAction("CaptureCursor", InputActionType.Button, "<Mouse>/rightButton");
            _actions.Enable();
        }

        private void Update()
        {
            if (!IsClientInitialized || !IsOwner || _actions == null) return;
            if (!UI.UiInputGate.Active && _release.WasPressedThisFrame()) SetCursor(false);
            if (!UI.UiInputGate.Active && _capture.WasPressedThisFrame() && Application.isFocused) SetCursor(true);
            bool controlling = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UI.UiInputGate.Blocked;
            if(IsDead)
            {
                if(controlling && _capture.WasPressedThisFrame())CycleSpectator();
                if(controlling && CanRespawn && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)RequestRespawn();
                return;
            }
            if (controlling)
            {
                Vector2 look = _look.ReadValue<Vector2>() * tuning.lookSensitivity * LocalLookMultiplier;
                _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
            }
            bool walking=UsesIndoorWalking(transform.position);
            if(!controlling||!walking)_localJumpQueued=false;
            else if(_rise.WasPressedThisFrame())_localJumpQueued=true;
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + Mathf.Max(0.02f, tuning.sendInterval);
            Vector2 move = controlling ? _move.ReadValue<Vector2>() : Vector2.zero;
            float vertical = walking ? (controlling&&_localJumpQueued?1f:0f) : controlling ? _rise.ReadValue<float>() - _sink.ReadValue<float>() : 0f;
            SubmitInput(new Vector3(move.x, vertical, move.y), _yaw, _pitch, controlling && _sprint.IsPressed());
            _localJumpQueued=false;
        }

        [ServerRpc]
        private void SubmitInput(Vector3 axes, float yaw, float pitch, bool sprint)
        {
            if(IsDead)return;
            if (!Finite(axes.x) || !Finite(axes.y) || !Finite(axes.z) || !Finite(yaw) || !Finite(pitch)) return;
            if(UsesIndoorWalking(transform.position))
            {
                // A jump request must not normalize away forward/strafe speed.
                Vector2 planar=Vector2.ClampMagnitude(new Vector2(axes.x,axes.z),1);
                _serverInput=new Vector3(planar.x,Mathf.Clamp(axes.y,-1,1),planar.y);
                bool jump=axes.y>.5f;
                if(jump&&!_serverJumpHeld)_serverJumpQueued=true;
                _serverJumpHeld=jump;
            }
            else
            {
                _serverInput = Vector3.ClampMagnitude(axes, 1f);
                _serverJumpQueued=false;_serverJumpHeld=axes.y>.5f;
            }
            _serverYaw = Mathf.Repeat(yaw, 360f);
            _serverPitch = Mathf.Clamp(pitch, -85f, 85f);
            _serverSprint = sprint;
            _lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized) return;
            DiverState state = _state.Value;
            if(state.Dead){AdvanceDeath(state);return;}
            bool active = Time.unscaledTime - _lastInputTime <= Mathf.Max(0.02f, tuning.inputTimeout);
            Vector3 axes = active && !IsHelpless && !IsTrapped ? _serverInput : Vector3.zero;
            float verticalLimit = Mathf.Max(0f, tuning.verticalSpeed);
            bool walking=UsesIndoorWalking(transform.position);
            if(walking)
            {
                var vessel=Nereid.NereidVessel.Active;
                if(!_wasWalking){_indoorVerticalVelocity=0;_serverJumpQueued=false;_serverJumpHeld=_serverInput.y>.5f;}
                bool grounded=_controller.isGrounded;
                if(grounded&&_indoorVerticalVelocity<0)_indoorVerticalVelocity=-2;
                if(_serverJumpQueued&&active&&!IsHelpless&&!IsTrapped&&grounded)
                    _indoorVerticalVelocity=Mathf.Sqrt(2*Mathf.Max(.1f,vessel.indoorGravity)*Mathf.Max(0,vessel.indoorJumpHeight));
                _serverJumpQueued=false;
                _indoorVerticalVelocity-=Mathf.Max(.1f,vessel.indoorGravity)*Time.fixedDeltaTime;
                Vector3 direction=Quaternion.Euler(0,_serverYaw,0)*new Vector3(axes.x,0,axes.z);
                float speed=Mathf.Max(0,vessel.indoorWalkSpeed)*SpeedMultiplier*(_serverSprint?Mathf.Max(1,tuning.sprintMultiplier):1);
                if(state.Stress>=tuning.stressDanger)speed*=.5f;
                var collision=_controller.Move((direction*speed+Vector3.up*_indoorVerticalVelocity)*Time.fixedDeltaTime);
                if((collision&CollisionFlags.Above)!=0&&_indoorVerticalVelocity>0)_indoorVerticalVelocity=0;
                if((collision&CollisionFlags.Below)!=0&&_indoorVerticalVelocity<0)_indoorVerticalVelocity=-2;
            }
            else
            {
                _indoorVerticalVelocity=0;_serverJumpQueued=false;
                // Previous scenes and open water retain their original pitch-driven swimming.
                Vector3 direction = Quaternion.Euler(_serverPitch, _serverYaw, 0f) * new Vector3(axes.x, 0f, axes.z);
                direction = Vector3.ClampMagnitude(direction + Vector3.up * axes.y, 1f);
                float speed = Mathf.Max(0f, tuning.swimSpeed) * SpeedMultiplier * (_serverSprint ? Mathf.Max(1f, tuning.sprintMultiplier) : 1f);
                if(state.Stress>=tuning.stressDanger) speed*=.5f;
                Vector3 velocity = direction * speed;
                velocity.y = Mathf.Clamp(velocity.y, -verticalLimit, verticalLimit);
                _controller.Move(velocity * Time.fixedDeltaTime);
            }
            _wasWalking=walking;
            transform.rotation = Quaternion.Euler(0f, _serverYaw, 0f);

            state.Position = transform.position;
            state.Yaw = _serverYaw;
            state.Pitch = _serverPitch;
            state.BodyRotation=transform.rotation;
            state.Sprinting=_serverSprint && active && axes.sqrMagnitude>.04f && (state.Position-_previousPosition).sqrMagnitude>.000001f;
            state.Inside = IsSealedInterior(state.Position);
            state.Trapped = _trappedTime > 0;
            state.Oxygen = Mathf.Clamp(state.Oxygen + (state.Inside ? tuning.oxygenRecovery : -1f) * Time.fixedDeltaTime,
                0f, Mathf.Max(1f, tuning.oxygenCapacity * OxygenMultiplier));
            float depthExcess = state.Inside ? 0f : Mathf.Max(0f, DepthOffset - state.Position.y - tuning.safeDepth - SafeDepthBonus);
            float verticalExcess = walking?0:Mathf.Max(0f, Mathf.Abs(state.Position.y - _previousPosition.y) / Time.fixedDeltaTime - verticalLimit - 0.01f);
            float stressChange = state.Inside ? -1f : depthExcess + verticalExcess;
            state.Stress = Mathf.Clamp(state.Stress + stressChange * Time.fixedDeltaTime, 0f, 100f);
            if(!state.Inside && state.Stress>=tuning.stressDanger)
                state.Oxygen=Mathf.Max(0,state.Oxygen-tuning.stressOxygenDrain*Time.fixedDeltaTime);
            _previousPosition = state.Position;
            var vesselHere=Nereid.NereidVessel.Active;
            if(vesselHere!=null)
            {
                if(state.Oxygen<=0)
                {
                    state.HypoxicSeconds+=Time.fixedDeltaTime;
                    float damage=tuning.suffocationDamagePerSecond*Mathf.Lerp(1,1.55f,Mathf.Clamp01(state.HypoxicSeconds/15));
                    state.Health=Mathf.Max(0,state.Health-damage*Time.fixedDeltaTime);
                }
                else
                {
                    state.HypoxicSeconds=0;
                    if(state.Inside)state.Health=Mathf.Min(100,state.Health+tuning.onboardHealingPerSecond*Time.fixedDeltaTime);
                }
                _state.Value=state;
                bool swallowed=vesselHere.InsideJaws(state.Position+Vector3.up*.35f);
                if(swallowed || state.Health<=0f){BeginDeath(swallowed);return;}
            }
            else _state.Value=state;
            _trappedTime = Mathf.Max(0f, _trappedTime-Time.fixedDeltaTime);
            _helplessTime = state.Oxygen <= 0f ? _helplessTime+Time.fixedDeltaTime : 0f;
            if (vesselHere==null && _helplessTime >= tuning.helplessWindow)
            {
                Teleport(new Vector3(0,1.2f,1));
                Rescue();
            }
        }

        public void ApplyExternalMotion(Vector3 displacement)
        {
            if (!IsServerInitialized || IsDead || !Finite(displacement.x) || !Finite(displacement.y) || !Finite(displacement.z)) return;
            _controller.Move(Vector3.ClampMagnitude(displacement, 2f));
        }
        public void ApplyTetherMotion(Vector3 displacement)
        {
            if(!IsServerInitialized)return;
            float before=transform.position.y;
            ApplyExternalMotion(displacement);
            // Friendly transport must not create a second rapid-ascent penalty on top
            // of the participant's own swim input. Actual depth pressure still applies.
            _previousPosition.y+=transform.position.y-before;
        }

        public void Teleport(Vector3 position)
        {
            if (!IsServerInitialized || IsDead) return;
            _controller.enabled=false; transform.position=position; _controller.enabled=true;
            _previousPosition=position;
            ResetIndoorMovement();_wasWalking=UsesIndoorWalking(position);
            _serverInput=Vector3.zero;_lastInputTime=float.NegativeInfinity;
            var state=_state.Value; state.Position=position; state.Inside=IsSealedInterior(position); _state.Value=state;
        }

        public void DamageOxygen(float amount)
        {
            if (!IsServerInitialized || IsDead) return;
            var state=_state.Value; state.Oxygen=Mathf.Max(0,state.Oxygen-Mathf.Max(0,amount)); _state.Value=state;
        }

        public void Trap() { if(IsServerInitialized && !IsDead) _trappedTime=tuning.trappedDuration; }
        public void Rescue()
        {
            if (!IsServerInitialized) return;
            if(IsDead)
            {
                if(!CanBeRescued)return;
                ReviveAt(transform.position);
                return;
            }
            _trappedTime=0; _helplessTime=0;
            var state=_state.Value; state.Oxygen=Mathf.Max(state.Oxygen,tuning.rescueOxygen); _state.Value=state;
        }

        private void LateUpdate()
        {
            if (!IsClientInitialized) return;
            if (!IsServerInitialized)
            {
                float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.02f, tuning.sendInterval));
                transform.position = Vector3.Lerp(transform.position, _state.Value.Position, blend);
                Quaternion rotation=IsDead?_state.Value.BodyRotation:Quaternion.Euler(0f,_state.Value.Yaw,0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
            }
            if (IsOwner && _camera != null) UpdateOwnerView();
        }

        private void OnGUI()
        {
            if(UI.UiInputGate.Active) return;
            if (!IsClientInitialized || !IsOwner) return;
            GUI.Box(new Rect(16f, Screen.height - 112f, 420f, 96f), "");
            GUI.Label(new Rect(28f, Screen.height - 104f, 400f, 24f),
                $"Oxygen {Oxygen:F0}/{tuning.oxygenCapacity:F0}   Depth {Depth:F1}m   Stress {PressureStress:F0}%");
            GUI.Label(new Rect(28f, Screen.height - 78f, 400f, 24f),
                Oxygen <= 0f ? "OUT OF OXYGEN — waiting for rescue" : IsTrapped ? "TRAPPED — teammate: hold F nearby" : PressureStress>=tuning.stressDanger ? "PRESSURE DANGER: slowed / oxygen loss — ascend slowly" : PressureStress>=tuning.stressWarning ? "PRESSURE WARNING: return to safe depth" : IsInside ? "SEALED SUBMARINE — oxygen recovering" : "UNDERWATER");
            GUI.Label(new Rect(28f, Screen.height - 52f, 400f, 24f), "WASD / Space / Ctrl / Shift   Esc: cursor   RMB: look");
            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
        }

        private static bool IsSealedInterior(Vector3 p) => Nereid.NereidVessel.Active!=null
            ? Nereid.NereidVessel.Active.ContainsSealed(p)
            : Mathf.Abs(p.x) < 6f && p.z > -8f && p.z < 8f && p.y > 0f && p.y < 4f;
        private static bool UsesIndoorWalking(Vector3 p) => Nereid.NereidVessel.Active!=null&&Nereid.NereidVessel.Active.ContainsWalkingDeck(p);
        private void ResetIndoorMovement()
        {_indoorVerticalVelocity=0;_serverJumpHeld=false;_serverJumpQueued=false;_localJumpQueued=false;_wasWalking=false;}
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void SetCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            if (_ownsTuning) Destroy(tuning);
        }
    }
}
