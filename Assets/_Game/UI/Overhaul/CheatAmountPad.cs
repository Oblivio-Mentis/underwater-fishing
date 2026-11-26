using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed class CheatAmountPad
    {
        readonly TMP_Text valueLabel;
        int value = 1;
        bool replaceDefault = true;

        public int Value => value;

        internal CheatAmountPad(Transform column)
        {
            var layout = UiKit.Column(column, "CheatAmountPad", 4);
            UiKit.Size(layout, 128);

            var display = UiKit.Row(layout, "CheatAmountDisplay", 8);
            UiKit.Size(display, 40);

            var title = UiKit.Label(display, "CheatAmountTitle", "Количество", 18, UiTheme.Muted);
            UiKit.Size(title, 40, 120);
            title.alignment = TextAlignmentOptions.Left;
            title.margin = new Vector4(4, 0, 0, 0);

            valueLabel = UiKit.Label(display, "CheatAmountValue", "1", 24, UiTheme.Cyan);
            UiKit.Size(valueLabel, 40, flexibleWidth: 1);
            valueLabel.alignment = TextAlignmentOptions.Right;
            valueLabel.textWrappingMode = TextWrappingModes.NoWrap;
            valueLabel.margin = new Vector4(0, 0, 4, 0);

            var firstRow = UiKit.Row(layout, "CheatAmountDigitsFirst", 6);
            UiKit.Size(firstRow, 40);
            for (int digit = 1; digit <= 5; digit++) AddDigit(firstRow, digit);
            AddAction(firstRow, "CheatClear", "Сброс", Clear);

            var secondRow = UiKit.Row(layout, "CheatAmountDigitsSecond", 6);
            UiKit.Size(secondRow, 40);
            for (int digit = 6; digit <= 9; digit++) AddDigit(secondRow, digit);
            AddDigit(secondRow, 0);
            AddAction(secondRow, "CheatBackspace", "Стереть", Backspace);
        }

        void AddDigit(Transform row, int digit)
        {
            int captured = digit;
            var button = UiKit.Button(row, "CheatDigit" + digit, digit.ToString(), () => Append(captured), height: 40);
            ConfigureButton(button, 20);
        }

        void AddAction(Transform row, string name, string caption, UnityEngine.Events.UnityAction clicked)
        {
            var button = UiKit.Button(row, name, caption, clicked, height: 40);
            ConfigureButton(button, 16);
        }

        static void ConfigureButton(Button button, float fontSize)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            var caption = button.GetComponentInChildren<TMP_Text>();
            UiKit.Fill(caption.rectTransform, 4);
            caption.fontSize = fontSize;
            caption.fontSizeMin = 12;
            caption.fontSizeMax = fontSize;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void Append(int digit)
        {
            if (replaceDefault)
            {
                value = digit;
                replaceDefault = false;
            }
            else
            {
                long next = (long)value * 10 + digit;
                if (next > int.MaxValue) return;
                value = (int)next;
            }
            Refresh();
        }

        void Clear()
        {
            value = 0;
            replaceDefault = false;
            Refresh();
        }

        void Backspace()
        {
            value /= 10;
            replaceDefault = false;
            Refresh();
        }

        void Refresh() => UiKit.Set(valueLabel, value.ToString());
    }
}
