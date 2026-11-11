using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // Command-palette for granting powers: type or filter, click a power, then click a person.
    public static class PowerGiverUI
    {
        const float WindowW = 820f, WindowH = 700f, RowH = 76f;
        static readonly Color Accent = DbTheme.Gold;

        static DbWindow window;
        static bool picked;
        static string query = "";
        static int category;
        static readonly List<PowerGrantCatalog.Entry> shown = new List<PowerGrantCatalog.Entry>();
        static readonly List<string> categories = new List<string>();
        static readonly Dictionary<RectTransform, Row> rows = new Dictionary<RectTransform, Row>();
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static DbRecycler list;
        static TextMeshProUGUI emptyLabel;

        public static bool IsOpen => window;
        public static int ShownCount => shown.Count;

        sealed class Row
        {
            public PowerGrantCatalog.Entry Entry;
            public Image Icon;
            public TextMeshProUGUI Name, Tag, Description;
        }

        public static void Open()
        {
            if (IsOpen) return;

            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            AuraTunePickerState.Disarm();
            PowerGiverInput.Ensure();

            window = DbWindow.Create("giver", WindowW, WindowH, "Power Giver", "Pick a power, then click a person", Accent);
            if (!window) return;
            picked = false;
            query = "";
            category = 0;
            rows.Clear();
            window.OnClosed = () =>
            {
                if (!picked) PowerGiverState.Disarm();
                window = null;
                list = null;
            };

            float bodyW = WindowW - 48f, bodyH = WindowH - 98f;

            categories.Clear();
            categories.Add("All");
            foreach (var entry in PowerGrantCatalog.Entries)
                if (!categories.Contains(entry.Category)) categories.Add(entry.Category);

            var search = DbUi.Field(window.Body, "", "Search powers...", Accent, false, true, "Search");
            search.Rt().Pin(0f, 0f, bodyW, 44f);
            search.OnChanged = v =>
            {
                query = (v ?? "").Trim();
                ApplyFilter();
            };

            var chips = new DbChips(window.Body, 0f, 52f, 32f, categories.ToArray(), Accent, bodyW);
            chips.OnChanged = index =>
            {
                category = index;
                ApplyFilter();
            };

            var scroll = DbUi.Scroll(window.Body, Accent, "List");
            float listTop = 52f + chips.Height + 10f;
            scroll.Rt().Pin(0f, listTop, bodyW, bodyH - listTop);
            list = new DbRecycler(scroll, 1, bodyW - 14f, RowH, 0f, 6f, CreateRow, BindRow);

            emptyLabel = DbUi.Label(scroll.Viewport, "No power matches your search.", DbTextStyle.Body, DbTheme.TextMuted, TextAlignmentOptions.Center, "Empty");
            emptyLabel.Rt().Stretch();
            emptyLabel.gameObject.SetActive(false);

            ApplyFilter();
            list.ForEachVisible((rt, index) =>
            {
                var group = rt.GetComponent<CanvasGroup>();
                float delay = .05f + index * .02f;
                var home = rt.anchoredPosition;
                group.alpha = 0f;
                rt.anchoredPosition = home + new Vector2(0f, -14f);
                DbMotion.Fade(group, 1f, .14f, DbEase.OutQuad, delay);
                DbMotion.Move(rt, home, .24f, DbEase.OutQuint, delay);
            });
            search.Activate();
        }

        static RectTransform CreateRow(RectTransform parent, int index)
        {
            var rt = DbUi.Node("Row", parent);
            rt.gameObject.AddComponent<CanvasGroup>();
            var row = new Row();
            var button = rt.gameObject.AddComponent<DbButton>();
            button.Style = DbButtonStyle.Surface;
            button.Accent = Accent;
            button.HoverScale = 1.012f;
            button.PressScale = .985f;
            button.Hit = rt.gameObject.AddComponent<DbHit>();
            button.Back = DbUi.Img(rt, DbAtlas.ChamferTight, DbTheme.Surface, "Back");
            button.Back.Rt().Stretch();
            button.Outline = DbUi.Img(rt, DbAtlas.ChamferTightLine, DbTheme.Stroke, "Line");
            button.Outline.Rt().Stretch();
            row.Icon = DbUi.Img(rt, null, Color.white, "Icon");
            row.Icon.preserveAspect = true;
            row.Icon.Rt().Pin(10f, 8f, RowH - 16f, RowH - 16f);
            row.Name = DbUi.Label(rt, "", DbTextStyle.Heading, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Name");
            row.Name.fontSize = 18f;
            row.Name.Rt().Pin(RowH + 8f, 8f, WindowW - 320f, 28f);
            row.Tag = DbUi.Label(rt, "", DbTextStyle.Caps, DbTheme.Gold, TextAlignmentOptions.MidlineRight, "Category");
            row.Tag.fontSize = 11f;
            row.Tag.Rt().PinRight(16f, 10f, 150f, 22f);
            row.Description = DbUi.Label(rt, "", DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Description");
            row.Description.Rt().Pin(RowH + 8f, 38f, WindowW - 150f, 26f);
            button.OnClick = () => Pick(row.Entry);
            button.ApplyInstant();
            rows[rt] = row;
            return rt;
        }

        static void BindRow(RectTransform rt, int index)
        {
            var row = rows[rt];
            var entry = shown[index];
            row.Entry = entry;
            row.Icon.sprite = DbArt.PowerIcon(IconFor(entry.Id));
            row.Name.text = entry.Name;
            row.Tag.text = entry.Category;
            row.Description.text = entry.Description;
        }

        // Every power icon is a DBPU.Icon_<Id> field; found once by name and remembered.
        static Sprite IconFor(string id)
        {
            if (icons.TryGetValue(id, out var sprite)) return sprite;
            var field = typeof(DBPU).GetField("Icon_" + id, BindingFlags.Public | BindingFlags.Static);
            sprite = field != null ? field.GetValue(null) as Sprite : null;
            if (!sprite) sprite = DBPU.PIconTemplate;
            icons[id] = sprite;
            return sprite;
        }

        static void ApplyFilter()
        {
            shown.Clear();
            string wanted = category > 0 && category < categories.Count ? categories[category] : null;
            foreach (var entry in PowerGrantCatalog.Entries)
            {
                if (wanted != null && entry.Category != wanted) continue;
                if (query.Length > 0
                    && entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                shown.Add(entry);
            }
            if (list != null) list.SetCount(shown.Count);
            if (emptyLabel) emptyLabel.gameObject.SetActive(shown.Count == 0);
        }

        static void Pick(PowerGrantCatalog.Entry entry)
        {
            picked = true;
            PowerGiverState.SelectedPowerId = entry.Id;
            PowerGiverState.SelectedPowerName = entry.Name;
            PowerGiverState.Armed = true;
            ModAPI.Notify("Click a person to grant " + entry.Name + ".");
            Close();
        }

        public static void Close()
        {
            if (window) window.Close();
        }
    }
}
