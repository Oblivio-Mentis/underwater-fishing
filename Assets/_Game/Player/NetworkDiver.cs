using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Data;
using UnderwaterFishing.Diving;
using UnityEngine;

namespace UnderwaterFishing.Player
{
    public struct DiverSnapshot
    {
        public Vector3 Position;
        public float Yaw, Pitch;
        public DivingState Diving;
        public bool Sheltered;
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkDiver : NetworkBehaviour
    {
        public static readonly List<NetworkDiver> All = new List<NetworkDiver>();
        public static NetworkDiver Local { get; private set; }
        [SerializeField] private GameBalance balance;
        [SerializeField] private Camera eyes;
        [SerializeField] private GameObject body;
        private readonly SyncVar<DiverSnapshot> snapshot = new SyncVar<DiverSnapshot>();
        private CharacterController motor;
        private DiverInput input;
        private Vector3 command, velocity, externalVelocity;
        private float yaw, pitch, commandYaw, commandPitch, lastCommand, nextSend;
        private bool sprint;
        private DivingState diving;
        private bool automationControlled, automationSprint;
        private Vector3 automationMovement;
        private float automationYaw, automationPitch;
        public DiverSnapshot State => snapshot.Value;
        public Vector3 AimOrigin => transform.position + Vector3.up * .6f;
        public Vector3 AimDirection => Quaternion.Euler(commandPitch, commandYaw, 0) * Vector3.forward;
        public bool IsActive => State.Diving.Life == DiverLife.Active;
        public GameBalance Balance => balance;
        public Camera Eyes => eyes;
        public Vector3 ServerVelocity { get; private set; }
        public float CarrySpeedMultiplier { get; set; } = 1f;
        public bool AutomationControlled => automationControlled;
        public void SetAutomationInput(Vector3 movement, float lookYaw, float lookPitch, bool fast)
        {
            if (!IsOwner || (!Application.isEditor && !Debug.isDebugBuild)) return;
            automationControlled = true; automationMovement = movement;
            automationYaw = lookYaw; automationPitch = lookPitch; automationSprint = fast;
        }

        public void Configure(GameBalance settings, Camera camera, GameObject mesh)
        { balance = settings; eyes = camera; body = mesh; }
        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            eyes.enabled = false;
            eyes.GetComponent<AudioListener>().enabled = false;
        }
        public override void OnStartNetwork() { All.Add(this); }
        public override void OnStopNetwork() { All.Remove(this); if (Local == this) Local = null; }
        public override void OnStartServer()
        {
            diving = DivingSimulation.Create(balance.Diving);
            commandYaw = transform.eulerAngles.y;
            Publish();
        }
        public override void OnStartClient()
        {
            motor.enabled = IsServerStarted;
            if (!IsOwner) return;
            Local = this;
            input = FindFirstObjectByType<DiverInput>();
            yaw = State.Yaw;
            body.SetActive(false);
            eyes.enabled = true;
            eyes.GetComponent<AudioListener>().enabled = true;
            input.SetMenu(false);
        }

        private void Update()
        {
            if (IsOwner && IsClientStarted && input != null)
            {
                if (automationControlled) { yaw = automationYaw; pitch = automationPitch; }
                else { yaw += input.Look.x * balance.LookSensitivity; pitch = Mathf.Clamp(pitch - input.Look.y * balance.LookSensitivity, -85, 85); }
                eyes.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                if (Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + .033f;
                    SubmitInput(automationControlled ? automationMovement : input.Movement, yaw, pitch, automationControlled ? automationSprint : input.Sprint);
                }
            }
            if (IsClientStarted && !IsServerStarted)
            {
                transform.position = Vector3.Lerp(transform.position, State.Position, 1f - Mathf.Exp(-20f * Time.deltaTime));
                transform.rotation = Quaternion.Euler(0, IsOwner ? yaw : State.Yaw, 0);
            }
        }

        [ServerRpc]
        private void SubmitInput(Vector3 movement, float lookYaw, float lookPitch, bool fast)
        {
            if (!Finite(movement.x) || !Finite(movement.y) || !Finite(movement.z) || !Finite(lookYaw) || !Finite(lookPitch)) return;
            command = Vector3.ClampMagnitude(movement, 1);
            commandYaw = Mathf.Repeat(lookYaw, 360);
            commandPitch = Mathf.Clamp(lookPitch, -85, 85);
            sprint = fast;
            lastCommand = Time.unscaledTime;
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        private void LateUpdate()
        {
            if (IsOwner && IsClientStarted) eyes.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            float dt = Time.fixedDeltaTime;
            if (Time.unscaledTime - lastCommand > .3f) { command = Vector3.zero; sprint = false; }
            bool active = diving.Life == DiverLife.Active;
            Vector3 target = Quaternion.Euler(0, commandYaw, 0) * command * (balance.SwimSpeed * CarrySpeedMultiplier * (sprint ? balance.SprintMultiplier : 1f));
            if (!active) target = Vector3.zero;
            target.y += active ? balance.Buoyancy : 0;
            target = SwimMath.ClampVoluntaryVelocity(target, balance.SwimSpeed * balance.SprintMultiplier, balance.Diving.SafeAscentSpeed, balance.Diving.SafeDescentSpeed);
            velocity = Vector3.MoveTowards(velocity, target, (command.sqrMagnitude > 0 ? balance.Acceleration : balance.Braking) * dt);
            Vector3 before = transform.position;
            motor.Move((velocity + externalVelocity) * dt);
            ServerVelocity = (transform.position - before) / dt;
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, balance.ExternalDrag * dt);
            transform.rotation = Quaternion.Euler(0, commandYaw, 0);
            bool sheltered = World.SubmarineInterior.Contains(transform.position);
            DivingSimulation.Step(ref diving, balance.Diving, Mathf.Max(0, -transform.position.y), (transform.position.y - before.y) / dt, sheltered, sprint, dt);
            Publish(sheltered);
        }

        private void Publish(bool sheltered = true)
        {
            snapshot.Value = new DiverSnapshot { Position = transform.position, Yaw = commandYaw, Pitch = commandPitch, Diving = diving, Sheltered = sheltered };
        }
        [Server] public void AddExternalVelocity(Vector3 force) { externalVelocity = Vector3.ClampMagnitude(externalVelocity + force, 15); }
        [Server] public void Relocate(Vector3 position)
        {
            motor.enabled = false;
            transform.position = position;
            motor.enabled = true;
            velocity = externalVelocity = Vector3.zero;
            diving = DivingSimulation.Create(balance.Diving);
            Publish();
        }
        [Server] public bool Rescue() => DivingSimulation.Rescue(ref diving, balance.Diving);
    }
}
