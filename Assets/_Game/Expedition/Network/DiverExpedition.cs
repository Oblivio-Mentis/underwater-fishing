using FishNet.Connection;
using FishNet.Object;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Network
{
    [RequireComponent(typeof(NetworkDiver))]
    public sealed class DiverExpedition : NetworkBehaviour
    {
        public static DiverExpedition Local { get; private set; }
        private NetworkDiver diver;
        private float nextRequest;
        public NetworkDiver Diver => diver;
        public string Feedback { get; private set; } = "";
        public bool Consented => NetworkExpedition.Instance != null && NetworkExpedition.Instance.HasConsented(OwnerId);
        private void Awake() { diver = GetComponent<NetworkDiver>(); }
        public override void OnStartClient() { if (IsOwner) Local = this; }
        public override void OnStopClient() { if (Local == this) Local = null; }
        public override void OnStopServer() { if (NetworkExpedition.Instance != null) NetworkExpedition.Instance.RemoveCrew(OwnerId); }
        public bool TryInteract()
        {
            var expedition = NetworkExpedition.Instance;
            if (!IsOwner || !IsClientStarted || expedition == null || !expedition.IsFocused(diver)) return false;
            if (expedition.State.Phase == ExpeditionPhase.Base) StartExpedition();
            else if (expedition.State.Phase == ExpeditionPhase.Expedition) RequestReturn();
            else if (expedition.State.Phase == ExpeditionPhase.Failure) ResetRun();
            return true;
        }
        public string InteractionHint => NetworkExpedition.Instance != null && NetworkExpedition.Instance.IsFocused(diver)
            ? NetworkExpedition.Instance.TerminalHint : "";
        private bool CanRequest()
        {
            if (Time.unscaledTime < nextRequest) return false;
            nextRequest = Time.unscaledTime + .25f; return true;
        }
        [ServerRpc] public void StartExpedition()
        {
            if (!CanRequest()) return;
            bool accepted = NetworkExpedition.Instance != null && NetworkExpedition.Instance.TryStart(this);
            Reply(Owner, accepted ? "Подготовка к экспедиции" : "Старт недоступен: база, терминал и минимум два игрока (или Development Mode).");
        }
        [ServerRpc] public void RequestReturn()
        {
            if (!CanRequest()) return;
            bool accepted = NetworkExpedition.Instance != null && NetworkExpedition.Instance.TryRequestReturn(this);
            Reply(Owner, accepted ? "Запрошено возвращение: экипаж должен вернуться на борт или подтвердить согласие." : "Запросите возвращение через терминал подлодки во время экспедиции.");
        }
        [ServerRpc] public void SetReturnConsent(bool consent)
        {
            if (!CanRequest()) return;
            bool accepted = NetworkExpedition.Instance != null && NetworkExpedition.Instance.TrySetConsent(this, consent);
            Reply(Owner, accepted ? consent ? "Возвращение подтверждено" : "Согласие отозвано" : "Сейчас нет запроса возвращения для подтверждения.");
        }
        [ServerRpc] public void ResetRun()
        {
            if (!CanRequest()) return;
            bool accepted = NetworkExpedition.Instance != null && NetworkExpedition.Instance.TryResetRun(this);
            Reply(Owner, accepted ? "Новый забег" : "Новый забег доступен у терминала после поражения.");
        }
        [TargetRpc] private void Reply(NetworkConnection connection, string message) { Feedback = message; }
    }
}
