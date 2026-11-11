using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // Compact modal list of buttons and sliders, used by quick prompts such as Shenron's wishes
    // and aura tuning. The full settings screen lives in ConfigsWindow.
    public static class DbpuConfigMenuUI
    {
        const float Width = 620f;
        const float RowHeight = 52f, RowGap = 8f, SliderHeight = 62f;
        const float Chrome = 80f + 30f;
        const float MaxBody = 620f;

        static DbWindow window;
        static Action onClose;
        static Action onSliderChanged;
        static bool closingFromCode;
        static Color accent = DbTheme.Orange;

        public static bool IsOpen => window;

        public static void Open(string title, Action onCloseAction, params DbpuMenuButton[] buttons)
        {
            Open(title, onCloseAction, buttons, null, null);
        }

        public static void OpenAccented(Color accentColor, string title, Action onCloseAction, params DbpuMenuButton[] buttons)
        {
            accent = accentColor;
            Open(title, onCloseAction, buttons, null, null);
            accent = DbTheme.Orange;
        }

        public static void Open(string title, Action onCloseAction, DbpuMenuButton[] buttons, DbpuMenuSlider[] sliders, Action sliderChanged)
        {
            buttons = buttons ?? Array.Empty<DbpuMenuButton>();
            sliders = sliders ?? Array.Empty<DbpuMenuSlider>();
            if (buttons.Length == 0 && sliders.Length == 0) return;

            closingFromCode = true;
            if (window) window.CloseImmediate();
            closingFromCode = false;

            onClose = onCloseAction;
            onSliderChanged = sliderChanged;

            float contentHeight = sliders.Length * (SliderHeight + RowGap) + buttons.Length * (RowHeight + RowGap);
            float bodyHeight = Mathf.Min(contentHeight, MaxBody);
            window = DbWindow.Create("quick", Width, bodyHeight + Chrome, DbTheme.StripTags(title), "", accent, true);
            if (!window) return;
            window.OnClosed = HandleClosed;

            var scroll = DbUi.Scroll(window.Body, accent);
            scroll.Rt().Stretch();
            scroll.SetContentHeight(contentHeight);

            float y = 0f, delay = .03f;
            float rowWidth = Width - 48f - (contentHeight > MaxBody ? 14f : 0f);
            foreach (var entry in sliders)
            {
                var row = BuildSlider(scroll.Content, entry, rowWidth);
                Place(row, y, rowWidth, SliderHeight, delay);
                y += SliderHeight + RowGap;
                delay += .022f;
            }
            foreach (var entry in buttons)
            {
                var button = DbUi.Button(scroll.Content, entry.Label, DbButtonStyle.Surface, () => entry.Action?.Invoke(), accent, "Option");
                button.Label.fontStyle = FontStyles.Bold;
                button.Label.fontSize = 16.5f;
                button.Label.characterSpacing = .5f;
                Place(button.Rt(), y, rowWidth, RowHeight, delay);
                y += RowHeight + RowGap;
                delay += .022f;
            }
        }

        static void Place(RectTransform row, float y, float width, float height, float delay)
        {
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(.5f, .5f);
            row.sizeDelta = new Vector2(width, height);
            var home = new Vector2(width * .5f, -(y + height * .5f));
            row.anchoredPosition = home + new Vector2(0f, -14f);
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            DbMotion.Fade(group, 1f, .16f, DbEase.OutQuad, delay);
            DbMotion.Move(row, home, .26f, DbEase.OutQuint, delay);
        }

        static RectTransform BuildSlider(Transform parent, DbpuMenuSlider entry, float width)
        {
            var row = DbUi.Node("Slider " + entry.Label, parent);
            var back = DbUi.Img(row, DbAtlas.ChamferTight, DbTheme.Surface, "Back");
            back.Rt().Stretch();
            var name = DbUi.Label(row, entry.Label, DbTextStyle.Body, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Name");
            name.Rt().Pin(16f, 6f, width * .6f, 26f);
            var value = DbUi.Label(row, Format(entry, entry.GetValue != null ? entry.GetValue() : entry.Min), DbTextStyle.Heading, accent, TextAlignmentOptions.MidlineRight, "Value");
            value.Rt().PinRight(16f, 6f, 120f, 26f);

            var slider = DbUi.Slider(row, entry.Min, entry.Max,
                () => entry.GetValue != null ? entry.GetValue() : entry.Min,
                v =>
                {
                    entry.SetValue?.Invoke(v);
                    value.text = Format(entry, v);
                },
                accent);
            slider.Rt().Pin(16f, 34f, width - 32f, 22f);
            slider.OnCommit = () => onSliderChanged?.Invoke();
            return row;
        }

        static string Format(DbpuMenuSlider entry, float value)
        {
            return entry.ShowAsPercent ? DbFormat.Percent(value) : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static void HandleClosed()
        {
            window = null;
            var callback = onClose;
            onClose = null;
            onSliderChanged = null;
            if (!closingFromCode) callback?.Invoke();
        }

        // Temporarily gives the screen to a native dialog; the caller reopens the menu afterwards.
        public static void Hide()
        {
            if (!window) return;
            window.OnClosed = null;
            window.CloseImmediate();
            window = null;
            onClose = null;
            onSliderChanged = null;
        }

        public static void Close()
        {
            if (!window) return;
            var closing = window;
            window = null;
            onClose = null;
            onSliderChanged = null;
            closing.OnClosed = null;
            closing.Close();
        }
    }
}
