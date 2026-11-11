using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    public enum CfgKind { Section, Toggle, Number, Action, Info }

    // One setting, described as data. The window builds and refreshes its row from this.
    public sealed class CfgRow
    {
        public CfgKind Kind;
        public string Label, Hint;
        public Func<bool> GetBool;
        public Action<bool> SetBool;
        public Func<float> GetNumber;
        public Action<float> SetNumber;
        public float Min, Max, HardMin = float.MinValue, HardMax = float.MaxValue, Default;
        public bool Percent, Whole, Log, Danger;
        public Action Click;
        public Func<string> Info;

        public static CfgRow Section(string label) { return new CfgRow { Kind = CfgKind.Section, Label = label }; }
        public static CfgRow Note(string label, Func<string> info, string hint) { return new CfgRow { Kind = CfgKind.Info, Label = label, Info = info, Hint = hint }; }
        public static CfgRow Button(string label, string hint, Action click, bool danger = false) { return new CfgRow { Kind = CfgKind.Action, Label = label, Hint = hint, Click = click, Danger = danger }; }

        public static CfgRow Toggle(string label, string hint, Func<bool> get, Action<bool> set)
        {
            return new CfgRow { Kind = CfgKind.Toggle, Label = label, Hint = hint, GetBool = get, SetBool = set };
        }

        // The slider covers the usual range (min..max); typing may go up to the hard limits.
        public static CfgRow Number(string label, string hint, Func<float> get, Action<float> set, float min, float max, float defaultValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, bool percent = false, bool whole = false, bool log = false)
        {
            return new CfgRow
            {
                Kind = CfgKind.Number, Label = label, Hint = hint, GetNumber = get, SetNumber = set,
                Min = min, Max = max, Default = defaultValue,
                HardMin = hardMin, HardMax = hardMax, Percent = percent, Whole = whole, Log = log
            };
        }
    }

    public sealed class CfgPage
    {
        public string Title, Description;
        public Sprite Icon;
        public Action Reset;
        public Color IconTint = Color.white;
        public readonly List<CfgRow> Rows = new List<CfgRow>();

        public CfgPage(string title, Sprite icon, string description, Action reset = null)
        {
            Title = title; Icon = icon; Description = description; Reset = reset;
        }

        public CfgPage Add(params CfgRow[] rows) { Rows.AddRange(rows); return this; }
    }

    // Two-pane settings screen. Pages are built the first time they are visited and then kept, so
    // switching tabs only toggles them. Values save themselves shortly after a change.
    public sealed class ConfigsWindow
    {
        const float WindowW = 1200f, WindowH = 760f, SidebarW = 262f, RowH = 54f, SectionH = 40f, InfoH = 66f;
        static readonly Color Accent = DbTheme.Orange;

        readonly List<CfgPage> pages;
        readonly Action save;
        readonly DbWindow window;
        readonly RectTransform content, glider;
        readonly CanvasGroup gliderGroup;
        readonly TextMeshProUGUI pageTitle, pageDescription, hintText;
        readonly DbField search;
        readonly DbButton resetButton;
        readonly float contentW;
        readonly Dictionary<CfgPage, PageView> views = new Dictionary<CfgPage, PageView>();
        readonly List<DbButton> tabs = new List<DbButton>();
        PageView current, results;
        CfgPage currentPage;
        bool dirty, searching;
        float saveAt, searchAt = -1f;

        public DbWindow Window { get { return window; } }

        sealed class PageView
        {
            public DbScroll Scroll;
            public CanvasGroup Group;
            public readonly List<Action> Refreshers = new List<Action>();
            public readonly List<RectTransform> Rows = new List<RectTransform>();
        }

        public ConfigsWindow(List<CfgPage> pages, Action save, Action onClosed)
        {
            this.pages = pages;
            this.save = save;
            window = DbWindow.Create("settings", WindowW, WindowH, "Mod Configurations", "Dragon Ball Singularity", Accent, true);
            if (!window) return;
            window.OnClosed = () =>
            {
                save?.Invoke();
                onClosed?.Invoke();
            };

            var body = window.Body;
            float bodyW = WindowW - 48f, bodyH = WindowH - 98f;

            var sidebar = DbUi.Well(body, "Sidebar");
            sidebar.Rt().Pin(0f, 0f, SidebarW, bodyH);
            var gliderImage = DbUi.Img(sidebar.transform, DbAtlas.ChamferTight, DbTheme.Alpha(Accent, .08f), "Glider");
            glider = gliderImage.Rt();
            glider.Pin(8f, 8f, SidebarW - 16f, 46f);
            gliderGroup = glider.gameObject.AddComponent<CanvasGroup>();
            var gliderBar = DbUi.Img(glider, DbAtlas.Pill, Accent, "Bar");
            DbUi.PillFit(gliderBar, 4f);
            gliderBar.Rt().Pin(0f, 9f, 4f, 28f);

            for (int i = 0; i < pages.Count; i++)
            {
                int index = i;
                var tab = DbUi.Button(sidebar.transform, null, DbButtonStyle.Quiet, () => ShowPage(pages[index], true), Accent, "Tab " + pages[i].Title);
                tab.Rt().Pin(8f, 8f + i * 50f, SidebarW - 16f, 46f);
                tab.HoverScale = 1.015f;
                var icon = DbUi.Img(tab.transform, pages[i].Icon, pages[i].IconTint, "Icon");
                icon.Rt().Pin(12f, 8f, 30f, 30f);
                var name = DbUi.Label(tab.transform, pages[i].Title, DbTextStyle.Heading, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "Name");
                name.fontSize = 17f;
                name.Rt().Pin(52f, 0f, SidebarW - 84f, 46f);
                tab.Label = name;
                tab.ApplyInstant();
                tabs.Add(tab);
            }

            float x = SidebarW + 18f;
            contentW = bodyW - x;
            pageTitle = DbUi.Label(body, "", DbTextStyle.Title, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "PageTitle");
            pageTitle.Rt().Pin(x, 0f, contentW - 300f, 34f);
            pageDescription = DbUi.Label(body, "", DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "PageDescription");
            pageDescription.Rt().Pin(x, 34f, contentW - 300f, 22f);

            search = DbUi.Field(body, "", "Search settings...", Accent, false, true, "Search");
            search.Rt().Pin(bodyW - 280f, 8f, 280f, 42f);
            search.OnChanged = _ => { searchAt = Time.unscaledTime + .14f; };

            var contentHost = DbUi.Node("ContentHost", body);
            contentHost.Pin(x, 64f, contentW, bodyH - 64f - InfoH - 10f);
            content = contentHost;

            var info = DbUi.Well(body, "InfoBar");
            info.Rt().Pin(x, bodyH - InfoH, contentW, InfoH);
            hintText = DbUi.Label(info.transform, "", DbTextStyle.Small, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "Hint");
            hintText.enableWordWrapping = true;
            hintText.overflowMode = TextOverflowModes.Truncate;
            hintText.lineSpacing = -4f;
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = 10.5f;
            hintText.fontSizeMax = 13.5f;
            hintText.Rt().Stretch(16f, 4f, 170f, 4f);

            resetButton = DbUi.Button(info.transform, "Reset page", DbButtonStyle.Danger, ResetCurrent, null, "ResetPage");
            resetButton.Rt().PinRight(10f, 12f, 150f, 36f);

            window.SetTick(.12f, Tick);
            ShowPage(pages[0], false);
        }

        void Tick()
        {
            float now = Time.unscaledTime;
            if (searchAt > 0f && now >= searchAt)
            {
                searchAt = -1f;
                ApplySearch(search.Text);
            }
            if (dirty && now >= saveAt)
            {
                dirty = false;
                save?.Invoke();
            }
        }

        void MarkDirty()
        {
            dirty = true;
            saveAt = Time.unscaledTime + .6f;
        }

        void ShowPage(CfgPage page, bool animate)
        {
            if (searching)
            {
                search.Text = "";
                ClearResults();
                DbMotion.Fade(gliderGroup, 1f, .15f);
            }
            if (currentPage == page && current != null && !animate) return;

            int index = pages.IndexOf(page);
            DbMotion.Move(glider, new Vector2(8f, -(8f + index * 50f)), .24f, DbEase.OutCubic);
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Label) DbMotion.Tint(tabs[i].Label, i == index ? DbTheme.Text : DbTheme.TextSoft, .15f);

            if (current != null && current.Group) current.Group.gameObject.SetActive(false);
            currentPage = page;
            if (!views.TryGetValue(page, out current))
            {
                current = BuildPage(page.Rows);
                views[page] = current;
            }
            current.Group.gameObject.SetActive(true);
            pageTitle.text = page.Title;
            pageDescription.text = page.Description;
            resetButton.gameObject.SetActive(page.Reset != null);
            hintText.text = page.Description;
            RefreshCurrent();
            AnimateRows(current, animate ? .0f : .1f);
        }

        void AnimateRows(PageView view, float baseDelay)
        {
            float delay = baseDelay;
            for (int i = 0; i < view.Rows.Count && i < 14; i++)
            {
                var row = view.Rows[i];
                var home = row.anchoredPosition;
                var group = row.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                row.anchoredPosition = home + new Vector2(22f, 0f);
                DbMotion.Fade(group, 1f, .14f, DbEase.OutQuad, delay);
                DbMotion.Move(row, home, .24f, DbEase.OutQuint, delay);
                delay += .014f;
            }
        }

        PageView BuildPage(List<CfgRow> rows)
        {
            var view = new PageView();
            view.Scroll = DbUi.Scroll(content, Accent, "Page");
            view.Scroll.Rt().Stretch();
            view.Group = view.Scroll.gameObject.AddComponent<CanvasGroup>();
            float y = 0f, width = contentW - 14f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float height = row.Kind == CfgKind.Section ? SectionH : RowH;
                var rt = BuildRow(view, row, width, height);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(0f, -y);
                rt.gameObject.AddComponent<CanvasGroup>();
                view.Rows.Add(rt);
                y += height + (row.Kind == CfgKind.Section ? 0f : 4f);
            }
            view.Scroll.SetContentHeight(y);
            return view;
        }

        RectTransform BuildRow(PageView view, CfgRow row, float width, float height)
        {
            if (row.Kind == CfgKind.Section)
            {
                var section = DbUi.Node("Section " + row.Label, view.Scroll.Content);
                var mark = DbUi.Img(section, DbAtlas.Star, Accent, "Star");
                mark.Rt().Pin(2f, 16f, 14f, 14f);
                var title = DbUi.Label(section, row.Label, DbTextStyle.Caps, DbTheme.Gold, TextAlignmentOptions.MidlineLeft, "Title");
                title.Rt().Pin(24f, 12f, width * .6f, 22f);
                var hair = DbUi.Img(section, DbAtlas.GradRight, DbTheme.Alpha(DbTheme.Text, .08f), "Line");
                hair.Rt().Pin(0f, 36f, width, 1f);
                hair.Rt().localRotation = Quaternion.Euler(0f, 0f, 180f);
                hair.Rt().pivot = new Vector2(.5f, .5f);
                hair.Rt().anchoredPosition = new Vector2(width * .5f, -36f);
                return section;
            }

            var rt = DbUi.Node("Row " + row.Label, view.Scroll.Content);
            var button = rt.gameObject.AddComponent<DbButton>();
            button.Style = DbButtonStyle.Quiet;
            button.Accent = row.Danger ? DbTheme.Danger : Accent;
            button.HoverScale = 1f;
            button.PressScale = 1f;
            button.Silent = true;
            button.Hit = rt.gameObject.AddComponent<DbHit>();
            button.Back = DbUi.Img(rt, DbAtlas.ChamferTight, Color.clear, "Hover");
            button.Back.Rt().Stretch();
            button.ApplyInstant();
            string hint = string.IsNullOrEmpty(row.Hint) ? row.Label : row.Hint;
            button.OnHover = () => hintText.text = hint;

            var label = DbUi.Label(rt, row.Label, DbTextStyle.Body, row.Danger ? DbTheme.Danger : DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Label");
            label.Rt().Pin(14f, 0f, row.Kind == CfgKind.Number ? width * .36f : width - 120f, height);

            switch (row.Kind)
            {
                case CfgKind.Toggle:
                {
                    var toggle = DbUi.Switch(rt, row.GetBool, v => { row.SetBool?.Invoke(v); MarkDirty(); RefreshCurrent(); }, Accent);
                    toggle.Rt().anchorMin = toggle.Rt().anchorMax = toggle.Rt().pivot = new Vector2(1f, .5f);
                    toggle.Rt().anchoredPosition = new Vector2(-14f, 0f);
                    toggle.Rt().sizeDelta = new Vector2(54f, 28f);
                    button.OnClick = () =>
                    {
                        row.SetBool?.Invoke(!row.GetBool());
                        DbUi.Blip();
                        MarkDirty();
                        RefreshCurrent();
                    };
                    view.Refreshers.Add(toggle.Refresh);
                    break;
                }
                case CfgKind.Number:
                    BuildNumber(view, row, rt, width, height, label);
                    break;
                case CfgKind.Action:
                {
                    var chevron = DbUi.Img(rt, DbAtlas.Chevron, row.Danger ? DbTheme.Danger : Accent, "Chevron");
                    chevron.Rt().anchorMin = chevron.Rt().anchorMax = chevron.Rt().pivot = new Vector2(1f, .5f);
                    chevron.Rt().anchoredPosition = new Vector2(-14f, 0f);
                    chevron.Rt().sizeDelta = new Vector2(24f, 24f);
                    button.Silent = false;
                    button.OnClick = () => { row.Click?.Invoke(); RefreshCurrent(); };
                    break;
                }
                case CfgKind.Info:
                {
                    var value = DbUi.Label(rt, row.Info != null ? row.Info() : "", DbTextStyle.Heading, DbTheme.Gold, TextAlignmentOptions.MidlineRight, "Value");
                    value.Rt().PinRight(14f, 0f, width * .5f, height);
                    view.Refreshers.Add(() => { if (row.Info != null) value.text = row.Info(); });
                    break;
                }
            }
            return rt;
        }

        void BuildNumber(PageView view, CfgRow row, RectTransform rt, float width, float height, TextMeshProUGUI label)
        {
            float fieldW = 92f, resetW = 30f, right = 10f;
            float suffix = row.Percent ? 20f : 0f;
            float sliderX = width * .38f;
            float sliderW = width - sliderX - fieldW - suffix - resetW - right - 24f;

            var slider = DbUi.Slider(rt, row.Min, row.Max, row.GetNumber, v => row.SetNumber?.Invoke(Clamp(row, v)), Accent, row.Log, row.Whole);
            slider.Rt().Pin(sliderX, (height - 26f) * .5f, sliderW, 26f);

            var field = DbUi.Field(rt, FormatValue(row, row.GetNumber()), "", Accent, true, false, "Value");
            field.Rt().PinRight(right + resetW + suffix + 4f, (height - 36f) * .5f, fieldW, 36f);
            field.Input.textComponent.alignment = TextAlignmentOptions.MidlineRight;

            if (row.Percent)
            {
                var percent = DbUi.Label(rt, "%", DbTextStyle.Body, DbTheme.TextMuted, TextAlignmentOptions.Center, "Percent");
                percent.Rt().PinRight(right + resetW + 2f, 0f, suffix, height);
            }

            var reset = DbUi.IconButton(rt, DbAtlas.Reset, null, DbTheme.TextSoft, DbTheme.Gold, "ResetValue");
            reset.Rt().PinRight(right, (height - 30f) * .5f, resetW, 30f);
            reset.Icon.Rt().sizeDelta = new Vector2(18f, 18f);
            reset.HoverScale = 1.12f;
            var resetGroup = reset.gameObject.AddComponent<CanvasGroup>();

            Action show = () =>
            {
                float value = row.GetNumber();
                if (!field.Focused) field.Text = FormatValue(row, value);
                bool changed = Mathf.Abs(value - row.Default) > Mathf.Max(.0005f, Mathf.Abs(row.Default) * .0005f);
                resetGroup.alpha = changed ? 1f : 0f;
                resetGroup.blocksRaycasts = changed;
            };

            slider.OnDragged = _ =>
            {
                show();
                MarkDirty();
            };
            slider.OnCommit = () => { MarkDirty(); RefreshCurrent(); };

            Action commit = () =>
            {
                if (!DbFormat.TryParse(field.Text, out float typed)) { show(); return; }
                if (row.Percent) typed /= 100f;
                row.SetNumber?.Invoke(Clamp(row, typed));
                MarkDirty();
                RefreshCurrent();
            };
            field.OnSubmit = _ => commit();
            field.OnFocusLost = commit;

            reset.OnClick = () =>
            {
                row.SetNumber?.Invoke(row.Default);
                MarkDirty();
                RefreshCurrent();
            };

            view.Refreshers.Add(() => { slider.Refresh(); show(); });
            show();
        }

        static float Clamp(CfgRow row, float value)
        {
            value = Mathf.Clamp(value, row.HardMin, row.HardMax);
            return row.Whole ? Mathf.Round(value) : value;
        }

        static string FormatValue(CfgRow row, float value)
        {
            if (row.Percent) return Mathf.Round(value * 100f).ToString("0.#", CultureInfo.InvariantCulture);
            if (row.Whole) return Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture);
            if (Mathf.Abs(value) >= 1000f) return value.ToString("0", CultureInfo.InvariantCulture);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public void RefreshCurrent()
        {
            var view = searching ? results : current;
            if (view == null) return;
            for (int i = 0; i < view.Refreshers.Count; i++) view.Refreshers[i]();
        }

        void ResetCurrent()
        {
            if (currentPage == null || currentPage.Reset == null) return;
            currentPage.Reset();
            MarkDirty();
            RefreshCurrent();
            hintText.text = currentPage.Title + " restored to defaults.";
        }

        void ClearResults()
        {
            searching = false;
            if (results != null && results.Scroll) UnityEngine.Object.Destroy(results.Scroll.gameObject);
            results = null;
        }

        void ApplySearch(string query)
        {
            query = (query ?? "").Trim();
            ClearResults();
            if (query.Length == 0)
            {
                DbMotion.Fade(gliderGroup, 1f, .15f);
                if (current != null) current.Group.gameObject.SetActive(true);
                pageTitle.text = currentPage.Title;
                pageDescription.text = currentPage.Description;
                resetButton.gameObject.SetActive(currentPage.Reset != null);
                RefreshCurrent();
                return;
            }

            var found = new List<CfgRow>();
            for (int p = 0; p < pages.Count && found.Count < 40; p++)
            {
                bool headed = false;
                foreach (var row in pages[p].Rows)
                {
                    if (row.Kind == CfgKind.Section) continue;
                    bool match = row.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || (row.Hint != null && row.Hint.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) continue;
                    if (!headed) { found.Add(CfgRow.Section(pages[p].Title)); headed = true; }
                    found.Add(row);
                }
            }

            searching = true;
            DbMotion.Fade(gliderGroup, .3f, .15f);
            if (current != null) current.Group.gameObject.SetActive(false);
            results = BuildPage(found);
            pageTitle.text = "Search";
            int matches = 0;
            foreach (var row in found) if (row.Kind != CfgKind.Section) matches++;
            pageDescription.text = matches == 0 ? "Nothing matches \"" + query + "\"." : matches + (matches == 1 ? " setting" : " settings") + " found.";
            resetButton.gameObject.SetActive(false);
            AnimateRows(results, 0f);
        }

        public void SetHidden(bool hidden)
        {
            if (window) window.SetHidden(hidden);
            if (!hidden) RefreshCurrent();
        }
    }
}
