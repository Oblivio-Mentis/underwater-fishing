using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    public sealed class CheatPanel
    {
        static readonly string[] ResourceIds = { "biomass", "chitin", "scrap", "electronics", "offering" };
        static readonly string[] ResourceNames = { "Биомасса", "Хитин", "Лом", "Электроника", "Пища" };

        public static bool PointerActive => UiRoot.Instance != null && UiRoot.Instance.CheatsOpen;
        internal bool IsOpen => surface.gameObject.activeSelf;
        internal bool QuantityFocused => IsOpen && amount.isFocused;

        readonly UiRoot root;
        readonly RectTransform surface;
        readonly TMP_InputField amount;
        readonly TMP_Text resourceText, fishText, walletText, tankText, feedback;
        readonly Button grant, stock, noclip, fast;
        DiverMotor player;
        int resourceIndex, fishIndex;
        bool requestedNoclip, requestedFast;
        float movementDeadline;

        internal CheatPanel(UiRoot root)
        {
            this.root = root;
            surface = UiKit.Panel(root.transform, "CheatPanel", UiTheme.Steel, true).rectTransform;
            UiKit.Place(surface, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-34, 0), new Vector2(474, 510));
            var border = surface.gameObject.AddComponent<Outline>();
            border.effectColor = UiTheme.Edge;
            border.effectDistance = new Vector2(1, -1);
            var column = UiKit.Column(surface, "Controls", 7, 18);
            UiKit.Fill(column);

            var header = Row(column, "Header", 42);
            var title = UiKit.Label(header, "Title", "Панель читов", 32);
            UiKit.Size(title, flexibleWidth: 1);
            var close = MouseButton(header, "CheatClose", "Закрыть", root.CloseCheats);
            UiKit.Size(close, width: 108, flexibleWidth: 0);

            var quantityRow = Row(column, "Quantity", 44);
            var quantityLabel = UiKit.Label(quantityRow, "Quantity label", "Количество", 22, UiTheme.Muted);
            UiKit.Size(quantityLabel, width: 166);
            amount = UiKit.Input(quantityRow, "CheatAmountField", "1", "Введите число");
            amount.contentType = TMP_InputField.ContentType.IntegerNumber;
            amount.onValidateInput = (_, _, c) => c >= '0' && c <= '9' ? c : '\0';
            amount.onFocusSelectAll = true;
            amount.richText = false;
            amount.navigation = new Navigation { mode = Navigation.Mode.None };
            amount.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.Fill(amount.textViewport, 5);
            UiKit.Fill(amount.textComponent.rectTransform, 1);
            UiKit.Fill(amount.placeholder.rectTransform, 1);
            walletText = Section(column, "Казна экипажа");
            resourceText = Selector(column, "CheatResource", delta =>
                resourceIndex = Wrap(resourceIndex + delta, ResourceIds.Length));
            grant = MouseButton(column, "CheatGrant", "Выдать ресурс", GrantResource, true);
            tankText = Section(column, "Резервуар");
            fishText = Selector(column, "CheatFish", delta =>
                fishIndex = Wrap(fishIndex + delta, SpeciesCount));
            stock = MouseButton(column, "CheatStock", "Добавить рыбу", AddFish, true);

            Section(column, "Перемещение");
            var movement = Row(column, "Movement", 43);
            noclip = MouseButton(movement, "CheatNoclip", "Сквозь стены", () => ToggleMovement(true));
            fast = MouseButton(movement, "CheatFast", "Скорость ×4", () => ToggleMovement(false));
            feedback = UiKit.Label(column, "CheatFeedback", "", 19, UiTheme.Cyan);
            UiKit.Size(feedback, 42);
            amount.onValueChanged.AddListener(QuantityChanged);
            surface.gameObject.SetActive(false);
        }

        int Quantity => int.TryParse(amount.text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

        void QuantityChanged(string text)
        {
            bool invalid = text.Length > 0 && Quantity == 0;
            UiKit.Set(feedback, invalid ? "Введите число от 1 до 2 147 483 647." : "");
            feedback.color = invalid ? UiTheme.Amber : UiTheme.Cyan;
            Refresh();
        }

        int SpeciesCount => root.Context.World?.CheatFishSpecies.Count ?? 0;
        static int Wrap(int index, int count) => count == 0 ? 0 : (index + count) % count;

        static RectTransform Row(Transform parent, string name, int height)
        {
            var row = UiKit.Row(parent, name, 6);
            UiKit.Size(row, height);
            return row;
        }

        static TMP_Text Section(Transform parent, string title)
        {
            var label = UiKit.Label(parent, title, title, 20, UiTheme.Muted);
            UiKit.Size(label, 24);
            return label;
        }

        static Button MouseButton(Transform parent, string name, string title, Action clicked, bool primary = false)
        {
            var button = UiKit.Button(parent, name, title, () => clicked(), primary, 38);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = button.GetComponentInChildren<TMP_Text>();
            UiKit.Fill(label.rectTransform, 3);
            return button;
        }

        static TMP_Text Selector(Transform column, string name, Action<int> change)
        {
            var row = Row(column, name, 38);
            UiKit.Size(MouseButton(row, name + "Previous", "‹", () => change(-1)), width: 52, flexibleWidth: 0);
            var label = UiKit.Label(row, name + "Text", "", 23);
            label.alignment = TextAlignmentOptions.Center;
            UiKit.Size(label, flexibleWidth: 1);
            UiKit.Size(MouseButton(row, name + "Next", "›", () => change(1)), width: 52, flexibleWidth: 0);
            return label;
        }

        internal void Open(DiverMotor owner)
        {
            player = owner;
            player.CheatGrantResponded += GrantResponded;
            movementDeadline = 0;
            UiKit.Set(feedback, "");
            surface.gameObject.SetActive(true);
            surface.SetAsLastSibling();
            Refresh();
        }

        internal void Close()
        {
            if (player != null) player.CheatGrantResponded -= GrantResponded;
            player = null;
            surface.gameObject.SetActive(false);
        }

        internal void Refresh()
        {
            var view = root.Context.View;
            if (view == null || player == null) return;
            var wallet = view.Treasury;
            string balance = resourceIndex switch
            {
                0 => wallet.biomass.ToString(), 1 => wallet.chitin.ToString(),
                2 => wallet.scrap.ToString(), 3 => wallet.electronics.ToString(),
                _ => TeamProgress.FoodText(view.offeringPoints)
            };
            UiKit.Set(resourceText, ResourceNames[resourceIndex]);
            UiKit.Set(walletText, "В казне экипажа  ·  " + balance);
            var world = root.Context.World;
            var species = world.CheatFishSpecies;
            fishIndex = Wrap(fishIndex, species.Count);
            UiKit.Set(fishText, species.Count == 0 ? "Нет доступных видов" : ReelReadout.Species(species[fishIndex]));
            UiKit.Set(tankText, "Резервуар  ·  свободно " + world.CheatFishRoom);
            bool hasQuantity = Quantity > 0;
            grant.interactable = hasQuantity;
            stock.interactable = hasQuantity && species.Count > 0 && world.CheatFishRoom > 0;
            if (Time.unscaledTime >= movementDeadline ||
                (requestedNoclip == player.CheatNoclip && requestedFast == player.CheatFast))
            {
                requestedNoclip = player.CheatNoclip;
                requestedFast = player.CheatFast;
                movementDeadline = 0;
            }
            ShowMode(noclip, "Сквозь стены", requestedNoclip);
            ShowMode(fast, "Скорость ×4", requestedFast);
        }

        static void ShowMode(Button button, string title, bool enabled)
        {
            UiKit.Caption(button, title + (enabled ? " · вкл" : " · выкл"));
            button.targetGraphic.color = enabled ? new Color32(49, 103, 84, 255) : UiTheme.Panel;
        }

        void ToggleMovement(bool throughWalls)
        {
            if (player == null) return;
            if (throughWalls) requestedNoclip = !requestedNoclip;
            else requestedFast = !requestedFast;
            movementDeadline = Time.unscaledTime + 1.5f;
            player.RequestCheatMovement(requestedNoclip, requestedFast);
            Refresh();
        }

        void GrantResource()
        {
            int value = Quantity;
            if (player != null && value > 0)
                player.CmdCheatGrantResource(ResourceIds[resourceIndex], value);
        }

        void AddFish()
        {
            var world = root.Context.World;
            int value = Quantity;
            if (player != null && world != null && value > 0 && fishIndex < world.CheatFishSpecies.Count)
                player.CmdCheatGrantFish(world.CheatFishSpecies[fishIndex], value);
        }

        void GrantResponded(bool accepted, int granted, string message)
        {
            UiKit.Set(feedback, message);
            feedback.color = accepted ? UiTheme.Green : UiTheme.Amber;
        }
    }
}
