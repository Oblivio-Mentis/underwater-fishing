using FishNet.Connection;
using FishNet.Object;
using UnderwaterFishing.Player;
using UnderwaterFishing.Progression.UI;
using UnityEngine;

namespace UnderwaterFishing.Progression.Network
{
    [RequireComponent(typeof(NetworkDiver))]
    public sealed class DiverProgression : NetworkBehaviour
    {
        public static DiverProgression Local { get; private set; }
        public NetworkDiver Diver { get; private set; }
        public string Feedback { get; private set; } = "";
        public bool PanelOpen => panel != null && panel.IsOpen;
        private ProgressionUI panel;
        private DiverInput input;
        private float nextRequest, nextSonar;
        private void Awake() { Diver = GetComponent<NetworkDiver>(); }
        public override void OnStartClient()
        {
            if (!IsOwner) return;
            Local = this; input = FindAnyObjectByType<DiverInput>();
            if (input != null) input.CloseUiRequested += Close;
            panel = new GameObject("Progression UI").AddComponent<ProgressionUI>(); panel.Initialize(this, input);
        }
        public override void OnStopClient()
        { Close(); if (input != null) input.CloseUiRequested -= Close; if (panel != null) Destroy(panel.gameObject); if (Local == this) Local = null; }
        public override void OnStopServer() { NetworkProgression.Instance?.RemoveCrew(OwnerId); }
        private void Update()
        {
            if (IsOwner && IsClientStarted && !Diver.AutomationControlled && input != null && input.SonarPressed && NetworkProgression.Instance != null)
                UseSonar(NetworkProgression.Instance.Charges(OwnerId));
        }
        public bool TryInteract()
        {
            var authority = NetworkProgression.Instance;
            if (!IsOwner || !IsClientStarted || authority == null) return false;
            var terminal = ProgressionTerminal.Focus(Diver, authority.Settings.TerminalDistance);
            if (terminal == null) return false;
            if (authority.CanUse(this, terminal.Kind, true)) panel.Open(terminal);
            else Feedback = "Терминалы доступны активному водолазу на базе.";
            return true;
        }
        public string InteractionHint
        {
            get
            {
                var authority = NetworkProgression.Instance;
                if (authority == null || ProgressionTerminal.Focus(Diver, authority.Settings.TerminalDistance) == null) return "";
                return "E — терминал ресурсов / оборудования / подношения";
            }
        }
        public void Close() { if (panel != null) panel.Close(); }
        private bool CanRequest(bool sonar = false)
        {
            var authority = NetworkProgression.Instance;
            if (authority == null || Time.unscaledTime < nextRequest || (sonar && Time.unscaledTime < nextSonar)) return false;
            nextRequest = Time.unscaledTime + Mathf.Max(.1f, authority.Settings.CommandCooldown);
            if (sonar) nextSonar = Time.unscaledTime + Mathf.Max(.25f, authority.Settings.SonarCooldown);
            return true;
        }
        [ServerRpc] public void Process(ulong itemId)
        {
            if (!CanRequest()) return;
            Reply(Owner, NetworkProgression.Instance.Process(this, itemId) ? "Добыча переработана в общий запас." : "Не удалось: предмет уже использован, зарезервирован или терминал недоступен.");
        }
        [ServerRpc] public void Reserve(ulong itemId, bool reserved)
        {
            if (!CanRequest()) return;
            Reply(Owner, NetworkProgression.Instance.Reserve(this, itemId, reserved) ? reserved ? "Рыба оставлена для подношения." : "Резерв снят." : "Рыба недоступна или терминал подношения недоступен.");
        }
        [ServerRpc] public void Buy(CrewUpgrade upgrade, int expectedLevel, uint expectedRevision)
        {
            if (!CanRequest()) return;
            Reply(Owner, NetworkProgression.Instance.Buy(this, upgrade, expectedLevel, expectedRevision)
                ? "Улучшение куплено. Действует со следующей экспедиции." : "Покупка отклонена: запас / уровень изменился, не хватает материалов или терминал недоступен.");
        }
        [ServerRpc] public void CraftSonar(int expectedCharges, uint expectedRevision)
        {
            if (!CanRequest()) return;
            Reply(Owner, NetworkProgression.Instance.CraftSonar(this, expectedCharges, expectedRevision)
                ? "Личный заряд сонара изготовлен. Используйте Q в экспедиции." : "Не хватает материалов, список изменился или терминал недоступен.");
        }
        [ServerRpc] public void SubmitOffering(uint expectedRevision)
        {
            if (!CanRequest()) return;
            Reply(Owner, NetworkProgression.Instance.SubmitOffering(this, expectedRevision)
                ? "Подношение принято." : "Нужна полная квота и актуальный резерв у терминала подношения.");
        }
        [ServerRpc] public void UseSonar(int expectedCharges)
        {
            if (!CanRequest(true)) return;
            var authority = NetworkProgression.Instance;
            if (!authority.UseSonar(this, expectedCharges, out var contacts)) { Reply(Owner, "Сонар требует заряд и доступен только в экспедиции."); return; }
            Pulse(Owner, contacts, Mathf.Max(.1f, authority.Settings.SonarDuration));
        }
        [TargetRpc] private void Reply(NetworkConnection connection, string message) { Feedback = message; }
        [TargetRpc] private void Pulse(NetworkConnection connection, SonarContact[] contacts, float duration)
        { if (panel != null) panel.ShowSonar(contacts, duration); Feedback = $"Сонар: {contacts.Length} контактов."; }
    }
}
