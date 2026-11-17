using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Diving;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cooperation
{
    public struct RescueSnapshot
    {
        public int TargetId, EmergencyCharges;
        public float Progress;
        public bool Signalling;
    }

    [RequireComponent(typeof(NetworkDiver))]
    public sealed class DiverRescue : NetworkBehaviour
    {
        private readonly SyncVar<RescueSnapshot> snapshot = new();
        private NetworkDiver diver;
        private DiverInput input;
        private RescueSnapshot current;
        private bool interacting, helping;
        private float lastInput, nextInput, nextSignal, signalUntil;
        private TextMesh marker;
        public RescueSnapshot State => snapshot.Value;
        private void Awake() { diver = GetComponent<NetworkDiver>(); }
        public override void OnStartServer() { current.TargetId = -1; current.EmergencyCharges = 1; snapshot.Value = current; }
        public override void OnStartClient()
        {
            if (IsOwner) input = FindFirstObjectByType<DiverInput>();
            else
            {
                var label = new GameObject("Crew distress signal", typeof(TextMesh));
                label.transform.SetParent(transform, false); label.transform.localPosition = Vector3.up * 1.3f;
                marker = label.GetComponent<TextMesh>(); marker.fontSize = 40; marker.characterSize = .045f;
                marker.anchor = TextAnchor.LowerCenter; marker.color = new Color(1, .6f, .12f);
            }
        }
        private void Update()
        {
            if (marker != null)
            {
                marker.text = diver.State.Diving.Life == DiverLife.Incapacitated ? "НУЖЕН КИСЛОРОД\nУдерживать F" :
                    diver.IsPinned ? "ПРИДАВЛЕН ОБЛОМКОМ\nУдерживать E" : State.Signalling ? "НУЖНА ПОМОЩЬ" : "";
                if (NetworkDiver.Local != null) marker.transform.rotation = Quaternion.LookRotation(marker.transform.position - NetworkDiver.Local.Eyes.transform.position);
            }
            if (!IsOwner || !IsClientStarted || input == null || diver.AutomationControlled || Time.unscaledTime < nextInput) return;
            nextInput = Time.unscaledTime + .1f;
            Actions(input.InteractHeld, input.HelpHeld);
        }
        [ServerRpc] public void Actions(bool interact, bool help)
        {
            interacting = interact; helping = help; lastInput = Time.unscaledTime;
            if (help && (diver.IsPinned || diver.State.Diving.Life == DiverLife.Incapacitated) && Time.unscaledTime >= nextSignal)
            {
                signalUntil = Time.unscaledTime + 4; nextSignal = Time.unscaledTime + 3;
                Debug.Log($"RAID_HELP_SIGNAL diver={ObjectId}");
            }
        }
        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            current.Signalling = Time.unscaledTime < signalUntil;
            if (Time.unscaledTime - lastInput > .35f) interacting = helping = false;
            if (diver.State.Sheltered && diver.IsActive) current.EmergencyCharges = 1;
            int targetId = -1;
            NetworkDiver target = null;
            if (diver.IsActive && helping && current.EmergencyCharges > 0)
                foreach (var other in NetworkDiver.All)
                    if (other != diver && other.State.Diving.Life == DiverLife.Incapacitated && CanReach(diver, other.AimOrigin, other))
                    { target = other; targetId = other.ObjectId; break; }
            NetworkCollapseTrap trap = null;
            if (target == null && diver.IsActive && interacting)
                foreach (var hazard in NetworkCollapseTrap.All)
                    if (hazard.CanRescue(diver) && CanReach(diver, hazard.RescuePoint, hazard.Victim))
                    { trap = hazard; targetId = hazard.ObjectId; break; }
            if (targetId < 0 || current.TargetId != targetId) current.Progress = 0;
            current.TargetId = targetId;
            if (targetId >= 0)
            {
                float duration = target != null ? diver.Balance.OxygenRescueSeconds : diver.Balance.DebrisRescueSeconds;
                current.Progress = Mathf.Clamp01(current.Progress + Time.fixedDeltaTime / Mathf.Max(.1f, duration));
                if (current.Progress >= 1)
                {
                    if (target != null && target.Rescue())
                    { current.EmergencyCharges--; Debug.Log($"RAID_DIVER_RESCUE rescuer={ObjectId} victim={target.ObjectId}"); }
                    else if (trap != null) trap.Clear(diver);
                    current.TargetId = -1; current.Progress = 0;
                }
            }
            snapshot.Value = current;
        }
        public static bool CanReach(NetworkDiver helper, Vector3 point, NetworkDiver target)
        {
            Vector3 delta = point - helper.AimOrigin;
            float distance = delta.magnitude;
            if (distance > helper.Balance.InteractionDistance || Vector3.Dot(helper.AimDirection, delta.normalized) < .45f) return false;
            return !Physics.Raycast(helper.AimOrigin, delta.normalized, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
                || (target != null && hit.collider.GetComponentInParent<NetworkDiver>() == target);
        }
    }
}
