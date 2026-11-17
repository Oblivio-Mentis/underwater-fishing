using System;
using System.Collections.Generic;
using System.Text;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Player;
using UnderwaterFishing.Progression.Network;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterFishing.Progression.UI
{
    public sealed class ProgressionUI : MonoBehaviour
    {
        public bool IsOpen => window != null && window.activeSelf;
        private DiverProgression actor;
        private DiverInput input;
        private ProgressionTerminal terminal;
        private GameObject window;
        private RectTransform canvasRect, content;
        private Text hud, title, stock, information, feedback;
        private Font font;
        private uint shownRevision = uint.MaxValue, shownCargoRevision = uint.MaxValue;
        private float nextRefresh;
        private readonly List<Marker> markers = new List<Marker>();
        private sealed class Marker { public SonarContact Contact; public Text Label; public float Until; }
        public void Initialize(DiverProgression owner, DiverInput controls)
        {
            actor = owner; input = controls;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 75;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>(); canvasRect = GetComponent<RectTransform>();
            hud = Label(transform, "Progression HUD", 16, TextAnchor.UpperRight);
            Place(hud.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -115), new Vector2(470, 180));
            window = new GameObject("Terminal panel", typeof(RectTransform), typeof(Image)); window.transform.SetParent(transform, false);
            window.GetComponent<Image>().color = new Color(.025f, .07f, .09f, .98f);
            Place(window.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 600));
            title = Label(window.transform, "Title", 26, TextAnchor.MiddleLeft); AtTop(title.rectTransform, 22, -18, 730, 40);
            Button(window.transform, "Закрыть", 758, -18, 120, 40, Close);
            stock = Label(window.transform, "Stocks", 18, TextAnchor.UpperLeft); AtTop(stock.rectTransform, 22, -70, 850, 48);
            information = Label(window.transform, "Instructions", 17, TextAnchor.UpperLeft); AtTop(information.rectTransform, 22, -120, 850, 70);
            var scroll = new GameObject("Manifest scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect)); scroll.transform.SetParent(window.transform, false);
            AtTop(scroll.GetComponent<RectTransform>(), 20, -202, 860, 330); scroll.GetComponent<Image>().color = new Color(.04f, .13f, .16f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(scroll.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            content = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var scrolling = scroll.GetComponent<ScrollRect>(); scrolling.viewport = viewport.GetComponent<RectTransform>(); scrolling.content = content;
            scrolling.horizontal = false; scrolling.vertical = true; scrolling.movementType = ScrollRect.MovementType.Clamped; scrolling.scrollSensitivity = 35;
            feedback = Label(window.transform, "Server reply", 17, TextAnchor.UpperLeft); AtTop(feedback.rectTransform, 22, -545, 850, 45);
            window.SetActive(false);
        }
        public void Open(ProgressionTerminal target)
        {
            terminal = target; window.SetActive(true); input?.SetUiCapture(true);
            shownRevision = shownCargoRevision = uint.MaxValue; nextRefresh = 0;
        }
        public void Close()
        {
            if (window != null) window.SetActive(false);
            terminal = null; input?.SetUiCapture(false);
        }
        private void OnDestroy() { Close(); }
        private void Update()
        {
            if (actor == null || !actor.IsClientStarted) { Close(); return; }
            var authority = NetworkProgression.Instance;
            if (hud != null) hud.gameObject.SetActive(input == null || !input.MenuOpen);
            if (authority == null) { Close(); if (hud != null) hud.text = ""; ClearSonar(); return; }
            if (IsOpen && (input != null && input.MenuOpen || terminal == null || !authority.CanUse(actor, terminal.Kind, true))) Close();
            if (authority.State.Phase != ExpeditionPhase.Expedition || !actor.Diver.IsActive) ClearSonar();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            var cargo = NetworkCargoStation.Instance;
            int reserved = ProgressionModel.ReservedValue(cargo?.ProtectedHold);
            string result = authority.State.LastOffering == OfferingResult.Success ? "Последнее подношение принято. Следующая квота выросла." : "";
            if (authority.State.Phase == ExpeditionPhase.Failure) result = "ЗАБЕГ ОКОНЧЕН\nTab — начать новый забег";
            if (authority.State.Phase == ExpeditionPhase.Offering) result = $"ПОДНОШЕНИЕ: выберите рыбу у терминала\nДо проверки: {authority.State.OfferingSeconds:0} с";
            hud.text = $"Подношение {reserved}/{authority.State.Quota} · Сонар Q: {authority.Charges(actor.OwnerId)}\n" + StockText(authority) + "\n" + result;
            if (!IsOpen) return;
            stock.text = StockText(authority); feedback.text = actor.Feedback;
            title.text = terminal.Kind == ProgressionTerminalKind.Processing ? "ПЕРЕРАБОТКА" : terminal.Kind == ProgressionTerminalKind.Workshop ? "МАСТЕРСКАЯ ЭКИПАЖА" : "ПОДНОШЕНИЕ";
            information.text = terminal.Kind == ProgressionTerminalKind.Processing
                ? "Выберите конкретный доставленный предмет. Переработка уничтожит его и добавит материалы всему экипажу. Резерв снимается у терминала подношения."
                : terminal.Kind == ProgressionTerminalKind.Workshop ? "Общие улучшения действуют со следующей экспедиции. Заряды сонара личные; Q показывает контакты только в экспедиции."
                : $"Резерв: {reserved}/{authority.State.Quota}. Нужна только рыба из защищённого отсека. " +
                    (authority.State.Phase == ExpeditionPhase.Offering ? $"До проверки {authority.State.OfferingSeconds:0} с. Можно передать полную квоту сейчас." : "Проверка после третьего дня. Остаток груза и материалы сохраняются.");
            uint cargoRevision = cargo == null ? 0 : cargo.State.Revision;
            if (shownRevision == authority.State.Revision && shownCargoRevision == cargoRevision) return;
            shownRevision = authority.State.Revision; shownCargoRevision = cargoRevision;
            Rebuild(authority, cargo);
        }
        private void Rebuild(NetworkProgression authority, NetworkCargoStation cargo)
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            float y = 0;
            if (terminal.Kind == ProgressionTerminalKind.Workshop)
            {
                foreach (CrewUpgrade upgrade in Enum.GetValues(typeof(CrewUpgrade)))
                {
                    CrewUpgrade chosen = upgrade; int expectedLevel = authority.Level(upgrade); uint revision = authority.State.Revision;
                    ResourceAmount[] cost = authority.UpgradeCost(upgrade);
                    string effect = upgrade == CrewUpgrade.Oxygen ? $"ёмкость +{authority.Settings.OxygenPerLevel * 100:0}% / уровень"
                        : upgrade == CrewUpgrade.Reel ? $"скорость и усилие +{authority.Settings.ReelPerLevel * 100:0}% / уровень"
                        : upgrade == CrewUpgrade.Suit ? $"безопасная глубина +{authority.Settings.SuitDepthPerLevel:0} м / уровень"
                        : $"скорость лебёдки +{authority.Settings.WinchPerLevel * 100:0}% / уровень";
                    string name = upgrade == CrewUpgrade.Oxygen ? "Кислород" : upgrade == CrewUpgrade.Reel ? "Катушка" : upgrade == CrewUpgrade.Suit ? "Костюм" : "Лебёдка";
                    Row(y, $"{name} · уровень {expectedLevel} · {effect}\n{CostText(authority, cost)}", "Улучшить", () => actor.Buy(chosen, expectedLevel, revision), cost != null);
                    y += 76;
                }
                int expectedCharges = authority.Charges(actor.OwnerId); uint currentRevision = authority.State.Revision;
                Row(y, $"Личный сонар · зарядов {expectedCharges} · {authority.Settings.SonarRange:0} м / {authority.Settings.SonarDuration:0} с\n{CostText(authority, authority.Settings.SonarCost)}", "Изготовить", () => actor.CraftSonar(expectedCharges, currentRevision), expectedCharges < authority.Settings.MaxSonarCharges);
                y += 76;
            }
            else
            {
                if (terminal.Kind == ProgressionTerminalKind.Offering && authority.State.Phase == ExpeditionPhase.Offering)
                {
                    uint revision = authority.State.Revision;
                    Row(y, "Передать зарезервированную рыбу чудовищу\nПотребуется только рыба до полной квоты; избыток останется.", "Передать", () => actor.SubmitOffering(revision), ProgressionModel.ReservedValue(cargo?.ProtectedHold) >= authority.State.Quota);
                    y += 76;
                }
                if (cargo != null) foreach (var entry in cargo.ProtectedHold)
                {
                    CargoItem selected = entry;
                    if (terminal.Kind == ProgressionTerminalKind.Offering && !entry.IsFish) continue;
                    bool reserve = !entry.Reserved;
                    string detail = terminal.Kind == ProgressionTerminalKind.Processing ? YieldText(authority, entry) : $"Ценность: {entry.OfferingValue} · {(entry.Reserved ? "В РЕЗЕРВЕ" : "свободна")}";
                    bool process = terminal.Kind == ProgressionTerminalKind.Processing;
                    Row(y, $"{entry.Name} · #{entry.Id}\n{detail}", process ? "Переработать" : reserve ? "В резерв" : "Снять резерв",
                        () => { if (process) actor.Process(selected.Id); else actor.Reserve(selected.Id, reserve); }, !process || !entry.Reserved);
                    y += 76;
                }
                if (y == 0)
                { var empty = Label(content, "Empty manifest", 18, TextAnchor.MiddleLeft); AtTop(empty.rectTransform, 14, 0, 800, 70); empty.text = "Подходящей доставленной добычи пока нет. Загрузите и поднимите контейнер в экспедиции."; y = 76; }
            }
            content.sizeDelta = new Vector2(0, y);
        }
        private void Row(float y, string description, string action, UnityEngine.Events.UnityAction click, bool enabled)
        {
            var text = Label(content, "Item", 17, TextAnchor.MiddleLeft); AtTop(text.rectTransform, 12, -y, 655, 70); text.text = description;
            var button = Button(content, action, 682, -y - 14, 158, 44, click); button.interactable = enabled;
        }
        private string StockText(NetworkProgression authority)
        {
            var value = new StringBuilder("Общий запас: ");
            foreach (var row in authority.Stocks) value.Append(ResourceName(authority, row.Id)).Append(' ').Append(row.Amount).Append("   ");
            return value.ToString();
        }
        private static string ResourceName(NetworkProgression authority, string id)
        { foreach (var resource in authority.Settings.Resources) if (resource.Id == id) return resource.Name; return id; }
        private static string CostText(NetworkProgression authority, ResourceAmount[] costs)
        {
            if (costs == null) return "Максимальный уровень";
            var text = new StringBuilder("Цена: ");
            foreach (var cost in costs) text.Append(ResourceName(authority, cost.Id)).Append(' ').Append(cost.Amount).Append("   ");
            return text.ToString();
        }
        private static string YieldText(NetworkProgression authority, CargoItem item)
        {
            if (item.Reserved) return "Зарезервирована для подношения";
            var text = new StringBuilder("Выход: ");
            foreach (var resource in authority.Settings.Resources)
            { long value = resource.Yield(item.Yield); if (value > 0) text.Append(resource.Name).Append(' ').Append(value).Append("   "); }
            return text.ToString();
        }
        public void ShowSonar(SonarContact[] contacts, float duration)
        {
            ClearSonar();
            foreach (var contact in contacts)
            {
                var label = Label(transform, "Sonar contact", 17, TextAnchor.MiddleCenter);
                label.color = contact.IsFish ? new Color(.3f, 1, .65f) : new Color(1, .8f, .2f);
                label.rectTransform.sizeDelta = new Vector2(250, 50);
                markers.Add(new Marker { Contact = contact, Label = label, Until = Time.unscaledTime + duration });
            }
        }
        private void LateUpdate()
        {
            if (actor == null || actor.Diver.Eyes == null) return;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var marker = markers[i];
                if (Time.unscaledTime >= marker.Until) { Destroy(marker.Label.gameObject); markers.RemoveAt(i); continue; }
                Vector3 screen = actor.Diver.Eyes.WorldToScreenPoint(marker.Contact.Position);
                marker.Label.gameObject.SetActive(screen.z > 0);
                if (screen.z <= 0) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var point);
                marker.Label.rectTransform.anchoredPosition = point;
                marker.Label.text = $"◇ {marker.Contact.Name}\n{Vector3.Distance(actor.Diver.transform.position, marker.Contact.Position):0} м";
            }
        }
        private void ClearSonar()
        { foreach (var marker in markers) if (marker.Label != null) Destroy(marker.Label.gameObject); markers.Clear(); }
        private Text Label(Transform parent, string name, int size, TextAnchor alignment)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); label.transform.SetParent(parent, false);
            label.font = font; label.fontSize = size; label.alignment = alignment; label.color = new Color(.85f, .96f, 1); label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var button = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>(); button.transform.SetParent(parent, false);
            button.targetGraphic = button.GetComponent<Image>(); button.targetGraphic.color = new Color(.08f, .35f, .41f);
            AtTop(button.GetComponent<RectTransform>(), x, y, width, height);
            var caption = Label(button.transform, "Caption", 17, TextAnchor.MiddleCenter); Stretch(caption.rectTransform); caption.text = text;
            button.onClick.AddListener(action); return button;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void AtTop(RectTransform rect, float x, float y, float width, float height)
        { Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(width, height)); }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
