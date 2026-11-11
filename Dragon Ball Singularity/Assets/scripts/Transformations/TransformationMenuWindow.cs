using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Stats = DBPU.CharacterTransformations.Transformations.TransformationStats;

namespace DBPU
{
    // Character-select style screen: the form's art on a spotlight stage, its stats and a form grid.
    // The whole window takes on the colour of the form being previewed.
    public sealed class TransformationMenuWindow
    {
        const float WindowW = 1280f, WindowH = 760f, StageW = 430f, Gap = 18f;
        const int Columns = 7;
        const float CellW = 104f, CellH = 128f, CellGap = 8f;

        readonly TransformationContextMenu menu;
        readonly DbWindow window;
        readonly List<KeyValuePair<string, Stats>> forms;
        readonly DbStage stage;
        readonly TextMeshProUGUI nameLabel, description, powerLabel, drainValue, kiValue, formValue, countLabel;
        readonly RectTransform kiFill, activeBadge, selector, nameRt;
        readonly DbRecycler grid;
        readonly DbScroll descriptionScroll;
        readonly DbButton accept, cancel;
        readonly Dictionary<RectTransform, Cell> cells = new Dictionary<RectTransform, Cell>();
        readonly KiSystem ki;

        int selected = -1;
        float shownPower = 1f;
        bool counting;
        string lastForm;
        const float KiBarWidth = 196f;

        public DbWindow Window { get { return window; } }
        public int Selected { get { return selected; } }

        sealed class Cell
        {
            public CanvasGroup Group;
            public DbButton Button;
            public Image Icon, Current;
            public TextMeshProUGUI Name;
            public int Index;
        }

        public TransformationMenuWindow(TransformationContextMenu menu)
        {
            this.menu = menu;
            forms = menu.TransformationsData.transformations.GetOrderedEntries();
            ki = menu.person ? menu.person.GetComponent<KiSystem>() : null;

            int start = Mathf.Max(0, forms.FindIndex(f => f.Key == menu.Form || (f.Value != null && f.Value.ID == menu.Form)));
            Color accent = DbTheme.FormAccent(forms[start].Value);
            window = DbWindow.Create("forms", WindowW, WindowH, "Transformation", FighterName(), accent);
            if (!window) return;
            window.BindLifetime(menu.gameObject);
            window.OnClosed = menu.HandleWindowClosed;
            window.SetHint("Click to preview  ·  Double-click to transform  ·  Esc to cancel");

            float bodyW = WindowW - 48f, bodyH = WindowH - 118f;

            stage = new DbStage(window.Body, StageW, bodyH, window);
            stage.Root.Pin(0f, 0f, StageW, bodyH);

            var powerCaption = DbUi.Label(stage.Overlay, "Power", DbTextStyle.Caps, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "PowerCaption");
            powerCaption.Rt().Pin(22f, bodyH - 92f, StageW - 44f, 20f);
            powerLabel = DbUi.Label(stage.Overlay, "", DbTextStyle.Hero, DbTheme.Gold, TextAlignmentOptions.MidlineLeft, "Power");
            powerLabel.Rt().Pin(20f, bodyH - 72f, StageW - 40f, 58f);

            float x = StageW + Gap, w = bodyW - x;

            nameLabel = DbUi.Label(window.Body, "", DbTextStyle.Hero, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "FormName");
            nameRt = nameLabel.Rt();
            nameRt.Pin(x, 0f, w - 130f, 58f);
            window.BindAccent(nameLabel, 1f, .25f);

            var badge = DbUi.Img(window.Body, DbAtlas.Slant, DbTheme.Success, "ActiveBadge");
            activeBadge = badge.Rt();
            activeBadge.Pin(bodyW - 118f, 14f, 118f, 30f);
            var badgeText = DbUi.Label(activeBadge, "Active", DbTextStyle.Caps, DbTheme.TextOnAccent, TextAlignmentOptions.Center, "Text");
            badgeText.Rt().Stretch();

            var rule = DbUi.Img(window.Body, DbAtlas.Hairline, DbTheme.Alpha(accent, .8f), "Rule");
            rule.Rt().Pin(x, 60f, w, 2f);
            window.BindAccent(rule, .8f);

            float cardW = (w - 16f) / 3f;
            drainValue = StatCard(x, 72f, cardW, "Ki drain");
            kiValue = StatCard(x + cardW + 8f, 72f, cardW, "Fighter's ki");
            formValue = StatCard(x + (cardW + 8f) * 2f, 72f, cardW, "Form");

            var kiRail = DbUi.Img(window.Body, DbAtlas.Pill, DbTheme.SurfaceHover, "KiRail");
            DbUi.PillFit(kiRail, 6f);
            kiRail.Rt().Pin(x + cardW + 22f, 126f, KiBarWidth, 6f);
            var fill = DbUi.Img(window.Body, DbAtlas.Pill, DbTheme.Ki, "KiFill");
            DbUi.PillFit(fill, 6f);
            kiFill = fill.Rt();
            kiFill.Pin(x + cardW + 22f, 126f, KiBarWidth, 6f);

            descriptionScroll = DbUi.Scroll(window.Body, accent, "DescriptionScroll");
            descriptionScroll.Rt().Pin(x, 150f, w, 74f);
            window.BindAccent(descriptionScroll.transform.Find("Thumb").GetComponent<Image>(), .55f);
            description = DbUi.Label(descriptionScroll.Content, "", DbTextStyle.Body, DbTheme.TextSoft, TextAlignmentOptions.TopLeft, "Description");
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Overflow;
            description.Rt().Pin(0f, 0f, w - 14f, 74f);

            var gridCaption = DbUi.SectionLabel(window.Body, "Forms", accent, "FormsCaption");
            gridCaption.color = DbTheme.Gold;
            gridCaption.Rt().Pin(x, 228f, 200f, 20f);
            countLabel = DbUi.Label(window.Body, forms.Count + (forms.Count == 1 ? " form" : " forms"), DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineRight, "FormCount");
            countLabel.Rt().Pin(x + w - 200f, 228f, 200f, 20f);

            var scroll = DbUi.Scroll(window.Body, accent, "Grid");
            scroll.Rt().Pin(x, 252f, w, bodyH - 252f - 62f);
            window.BindAccent(scroll.transform.Find("Thumb").GetComponent<Image>(), .55f);

            var selectorImage = DbUi.Img(scroll.Content, DbAtlas.ChamferTightLine, accent, "Selector");
            selector = selectorImage.Rt();
            selector.anchorMin = selector.anchorMax = new Vector2(0f, 1f);
            selector.pivot = new Vector2(.5f, .5f);
            selector.sizeDelta = new Vector2(CellW + 6f, CellH + 6f);
            selector.gameObject.SetActive(false);
            window.BindAccent(selectorImage);

            float inner = Columns * CellW + (Columns - 1) * CellGap;
            float pad = Mathf.Max(3f, (w - 10f - inner) * .5f);
            grid = new DbRecycler(scroll, Columns, CellW, CellH, CellGap, CellGap, CreateCell, BindCell, pad, 4f);

            float barY = bodyH - 50f;
            var skin = menu.skinContextMenu;
            if (skin != null)
            {
                var toggle = DbUi.Switch(window.Body, () => skin.SkinDamage, v => skin.SkinDamage = v, accent, "ClothingDamage");
                toggle.Rt().Pin(x, barY + 11f, 54f, 28f);
                var toggleLabel = DbUi.Label(window.Body, "Clothing damage", DbTextStyle.Body, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "ClothingDamageLabel");
                toggleLabel.Rt().Pin(x + 66f, barY, 200f, 50f);
            }

            cancel = DbUi.Button(window.Body, "Cancel", DbButtonStyle.Ghost, () => window.Close(), accent, "Cancel");
            cancel.Rt().Pin(bodyW - 372f, barY + 3f, 120f, 44f);
            accept = DbUi.Button(window.Body, "Transform", DbButtonStyle.Primary, ApplySelected, accent, "Accept");
            accept.Rt().Pin(bodyW - 240f, barY, 240f, 50f);
            accept.Label.fontSize = 16f;

            grid.SetCount(forms.Count);
            lastForm = menu.Form;
            Select(start, false);
            grid.ForEachVisible((rt, index) =>
            {
                var cell = cells[rt];
                float delay = .06f + (index / Columns + index % Columns) * .022f;
                cell.Group.alpha = 0f;
                rt.localScale = Vector3.one * .86f;
                DbMotion.Fade(cell.Group, 1f, .16f, DbEase.OutQuad, delay);
                DbMotion.Scale(rt, 1f, .26f, DbEase.OutBack, delay);
            });

            window.SetTick(.2f, RefreshLive);
            RefreshLive();
        }

        TextMeshProUGUI StatCard(float x, float y, float width, string caption)
        {
            var card = DbUi.Well(window.Body, "Stat " + caption);
            card.Rt().Pin(x, y, width, 68f);
            var title = DbUi.Label(card.transform, caption, DbTextStyle.Caps, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Caption");
            title.fontSize = 11f;
            title.Rt().Pin(14f, 6f, width - 28f, 18f);
            var value = DbUi.Label(card.transform, "", DbTextStyle.Heading, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Value");
            value.Rt().Pin(14f, 24f, width - 28f, 28f);
            return value;
        }

        string FighterName()
        {
            var identity = menu.person ? menu.person.GetComponent<DbpuCharacterIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.DisplayName)) return identity.DisplayName;
            return menu.person ? menu.person.name.Replace("(Clone)", "") : "";
        }

        static string FormName(KeyValuePair<string, Stats> form)
        {
            string name = DbTheme.StripTags(form.Value.Name);
            return string.IsNullOrEmpty(name) ? form.Key : name;
        }

        bool IsCurrent(int index)
        {
            var form = forms[index];
            return form.Key == menu.Form || form.Value.ID == menu.Form;
        }

        RectTransform CreateCell(RectTransform parent, int index)
        {
            var rt = DbUi.Node("Cell", parent);
            var cell = new Cell();
            cell.Group = rt.gameObject.AddComponent<CanvasGroup>();
            cell.Button = rt.gameObject.AddComponent<DbButton>();
            cell.Button.Style = DbButtonStyle.Surface;
            cell.Button.Accent = window.Accent;
            cell.Button.HoverScale = 1.05f;
            cell.Button.Hit = rt.gameObject.AddComponent<DbHit>();
            cell.Button.Back = DbUi.Img(rt, DbAtlas.ChamferTight, DbTheme.Surface, "Back");
            cell.Button.Back.Rt().Stretch();
            cell.Button.Outline = DbUi.Img(rt, DbAtlas.ChamferTightLine, DbTheme.Stroke, "Line");
            cell.Button.Outline.Rt().Stretch();
            cell.Icon = DbUi.Img(rt, null, Color.white, "Icon");
            cell.Icon.preserveAspect = true;
            cell.Icon.Rt().Pin(10f, 8f, CellW - 20f, CellW - 20f);
            var frame = DbUi.Img(rt, DbAtlas.RoundLine, DbTheme.Stroke, "IconFrame");
            frame.Rt().Pin(10f, 8f, CellW - 20f, CellW - 20f);
            cell.Current = DbUi.Img(rt, DbAtlas.Star, DbTheme.Gold, "CurrentStar");
            cell.Current.Rt().Pin(5f, 3f, 22f, 22f);
            cell.Name = DbUi.Label(rt, "", DbTextStyle.Small, DbTheme.Text, TextAlignmentOptions.Center, "Name");
            cell.Name.fontSize = 12f;
            cell.Name.fontStyle = FontStyles.Bold;
            cell.Name.enableWordWrapping = true;
            cell.Name.lineSpacing = -10f;
            cell.Name.enableAutoSizing = true;
            cell.Name.fontSizeMin = 9f;
            cell.Name.fontSizeMax = 12f;
            cell.Name.Rt().Pin(4f, CellW - 13f, CellW - 8f, CellH - CellW + 11f);
            cells[rt] = cell;
            cell.Button.OnClick = () => Select(cell.Index, true);
            cell.Button.OnDoubleClick = () =>
            {
                Select(cell.Index, true);
                ApplySelected();
            };
            return rt;
        }

        void BindCell(RectTransform rt, int index)
        {
            var cell = cells[rt];
            var form = forms[index];
            cell.Index = index;
            cell.Icon.sprite = DbArt.Icon(form.Value.Icon);
            cell.Icon.enabled = cell.Icon.sprite;
            cell.Name.text = FormName(form);
            cell.Current.enabled = IsCurrent(index);
            if (selected == index) MoveSelector(rt, true);
        }

        void MoveSelector(RectTransform cell, bool instant)
        {
            selector.gameObject.SetActive(true);
            selector.SetAsLastSibling();
            if (instant)
            {
                DbMotion.Stop(selector);
                selector.anchoredPosition = cell.anchoredPosition;
                return;
            }
            DbMotion.Move(selector, cell.anchoredPosition, .2f, DbEase.OutCubic);
        }

        public void Select(int index, bool animate)
        {
            if (index < 0 || index >= forms.Count) return;
            int direction = index >= selected ? 1 : -1;
            bool changed = index != selected;
            selected = index;
            var form = forms[index];
            var stats = form.Value;

            Color accent = DbTheme.FormAccent(stats);
            window.SetAccent(accent, animate ? .3f : 0f);
            accept.SetAccent(accent, animate);
            cancel.Accent = accent;
            foreach (var pair in cells) pair.Value.Button.Accent = accent;

            stage.Show(DbArt.Illustration(stats.Illustration), direction, animate);
            nameLabel.text = FormName(form);
            description.text = stats.Description ?? "";
            float textWidth = description.Rt().sizeDelta.x;
            float textHeight = Mathf.Max(24f, DbUi.PreferredHeight(description, description.text, textWidth));
            description.Rt().sizeDelta = new Vector2(textWidth, textHeight);
            descriptionScroll.SetContentHeight(textHeight, false);
            drainValue.text = stats.KiDrainRate.ToString("0.##", CultureInfo.InvariantCulture);
            formValue.text = (index + 1) + " of " + forms.Count;
            UpdatePower(animate);
            UpdateCurrentState();

            grid.Reveal(index);
            var cellRt = grid.CellFor(index);
            if (cellRt) MoveSelector(cellRt, !animate);

            if (animate && changed)
            {
                var home = nameRt.anchoredPosition;
                nameRt.anchoredPosition = home + new Vector2(30f * direction, 0f);
                DbMotion.Move(nameRt, home, .28f, DbEase.OutQuint);
            }
        }

        void UpdatePower(bool animate)
        {
            float target = Mathf.Max(.0001f, menu.GetStatsDisplayPower(forms[selected].Value));
            if (!animate || shownPower <= 0f)
            {
                DbMotion.Stop(powerLabel);
                counting = false;
                shownPower = target;
                powerLabel.text = menu.FormatMultiplierValue(target);
                return;
            }
            // Counts in log space, since forms range from x1 to the trillions.
            float from = Mathf.Log(Mathf.Max(.0001f, shownPower)), to = Mathf.Log(target);
            counting = true;
            DbMotion.Value(powerLabel, 0f, 1f, t =>
            {
                shownPower = Mathf.Exp(Mathf.Lerp(from, to, t));
                powerLabel.text = menu.FormatMultiplierValue(shownPower);
            }, .42f, DbEase.OutCubic, 0f, () =>
            {
                counting = false;
                shownPower = target;
                powerLabel.text = menu.FormatMultiplierValue(target);
            });
        }

        void UpdateCurrentState()
        {
            bool current = IsCurrent(selected);
            activeBadge.gameObject.SetActive(current);
            accept.Interactable = !current;
            accept.SetText(current ? "Current form" : "Transform");
        }

        void ApplySelected()
        {
            if (selected < 0 || selected >= forms.Count || IsCurrent(selected)) return;
            string key = forms[selected].Key;
            window.Close();
            menu.ApplyFormWithTitle(key);
        }

        // Follows live ki, exponential growth and forms changed from outside the menu.
        void RefreshLive()
        {
            if (ki)
            {
                float ratio = ki.maxKi > 0f ? Mathf.Clamp01(ki.currentKi / ki.maxKi) : 0f;
                kiValue.text = DbFormat.Percent(ratio);
                kiFill.sizeDelta = new Vector2(Mathf.Max(6f, KiBarWidth * ratio), 6f);
            }
            if (lastForm != menu.Form)
            {
                lastForm = menu.Form;
                grid.Rebind();
                UpdateCurrentState();
            }
            if (!counting && selected >= 0)
            {
                float target = menu.GetStatsDisplayPower(forms[selected].Value);
                shownPower = target;
                string text = menu.FormatMultiplierValue(target);
                if (powerLabel.text != text) powerLabel.text = text;
            }
        }
    }
}
