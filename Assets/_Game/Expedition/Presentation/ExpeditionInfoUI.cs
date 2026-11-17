using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Presentation
{
    public sealed class ExpeditionInfoUI : MonoBehaviour
    {
        private DiverInput input;
        private bool opened;
        private void Start() { input = GetComponent<DiverInput>(); input.CloseUiRequested += Close; }
        private void Close() { opened = false; if (input != null) input.SetUiCapture(false); }
        private void Update()
        {
            if (input.MenuOpen || NetworkDiver.Local == null || NetworkExpedition.Instance == null) { if (opened) Close(); return; }
            if (!opened && input.UiCaptured) return;
            if (input.InfoPressed) { opened = !opened; input.SetUiCapture(opened); }
        }
        private void OnGUI()
        {
            var expedition = NetworkExpedition.Instance;
            if (expedition == null || NetworkDiver.Local == null || input == null || input.MenuOpen) return;
            var state = expedition.State;
            GUI.Box(new Rect(Screen.width - 345, 12, 330, 72), $"{state.Phase} · День {state.Day}/3\n" +
                (state.Phase == ExpeditionPhase.Expedition ? $"Осталось {state.RemainingSeconds:0} с" : $"Экипаж {state.Crew}/{state.RequiredCrew}+") + "\nTab — экспедиция");
            if (!opened) return;
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 235, Screen.height / 2f - 150, 470, 300), GUI.skin.box);
            GUILayout.Label($"Затонувший остов · {state.Phase} · день {state.Day}/3");
            GUILayout.Label($"Экипаж: {state.Crew}, требуется {state.RequiredCrew}. " + (state.DevelopmentMode ? "Development Mode" : ""));
            if (state.ReturnRequested)
            {
                GUILayout.Label($"Возвращение запрошено. Ожидаем участников: {state.PendingCrew}");
                if (GUILayout.Button("Согласен на возвращение")) DiverExpedition.Local?.SetReturnConsent(true);
                if (GUILayout.Button("Отозвать согласие")) DiverExpedition.Local?.SetReturnConsent(false);
            }
            if (state.Phase == ExpeditionPhase.Failure && GUILayout.Button("Начать новый забег")) DiverExpedition.Local?.ResetRun();
            if (state.Phase == ExpeditionPhase.Success) GUILayout.Label("ПОДНОШЕНИЕ ПРИНЯТО · снаряжение и материалы сохранены");
            GUILayout.Label(DiverExpedition.Local?.Feedback ?? "");
            if (GUILayout.Button("Закрыть")) Close();
            GUILayout.EndArea();
        }
        private void OnDestroy() { if (input != null) input.CloseUiRequested -= Close; }
    }
}
