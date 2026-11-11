using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DBPU
{
    // Small modal prompt for typing a fighter's power level.
    public static class CharacterPowerInputUI
    {
        const float WindowW = 560f, WindowH = 330f;
        static DbWindow window;

        public static bool IsOpen => window;

        public static void Open(string title, string currentLine, string defaultLine, float initialValue, Action<float> onApply)
        {
            Close();

            window = DbWindow.Create("power-input", WindowW, WindowH, "Power level", title, DbTheme.Gold, true);
            if (!window) return;
            window.OnClosed = () => window = null;

            float bodyW = WindowW - 48f;

            var current = DbUi.Label(window.Body, currentLine, DbTextStyle.Heading, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Current");
            current.Rt().Pin(0f, 0f, bodyW, 32f);
            var fallback = DbUi.Label(window.Body, defaultLine, DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Default");
            fallback.Rt().Pin(0f, 32f, bodyW, 24f);

            var field = DbUi.Field(window.Body, initialValue.ToString(CultureInfo.InvariantCulture), "Enter a power value", DbTheme.Gold, true, false, "Value");
            field.Rt().Pin(0f, 70f, bodyW, 52f);

            Action apply = () =>
            {
                float value = DbFormat.TryParse(field.Text, out float typed) ? typed : initialValue;
                var closing = window;
                onApply?.Invoke(value);
                if (closing) closing.Close();
            };
            field.OnSubmit = _ => apply();

            var cancel = DbUi.Button(window.Body, "Cancel", DbButtonStyle.Ghost, Close, DbTheme.Gold, "Cancel");
            cancel.Rt().Pin(0f, 142f, 170f, 46f);
            var accept = DbUi.Button(window.Body, "Apply", DbButtonStyle.Primary, () => apply(), DbTheme.Gold, "Accept");
            accept.Rt().Pin(bodyW - 280f, 140f, 280f, 50f);
            accept.Label.fontSize = 16f;

            field.Activate();
        }

        public static void Close()
        {
            if (window) window.Close();
        }
    }
}
