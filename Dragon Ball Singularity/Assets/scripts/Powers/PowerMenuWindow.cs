using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // Powers screen: filterable grid on the left, details and live tuning on the right.
    public sealed class PowerMenuWindow
    {
        const float WindowW = 1200f, WindowH = 720f;
        const float DetailW = 420f, Gap = 18f;
        const float ToolbarH = 42f;
        const int Columns = 5;
        const float CellW = 130f, CellH = 150f, CellGap = 8f;

        static readonly Color Accent = DbTheme.Orange;

        readonly PowerContextMenu owner;
        readonly CharacterPowers powers;
        readonly PersonBehaviour person;
        readonly GameObject instance;
        readonly DbWindow window;
        readonly List<CharacterPowers.PowerStats> all;
        readonly List<CharacterPowers.PowerStats> shown = new List<CharacterPowers.PowerStats>();
        readonly DbScroll gridScroll;
        readonly DbRecycler grid;
        readonly DbChips filters;
        readonly DbField search;
        readonly RectTransform selector;
        readonly TextMeshProUGUI emptyLabel;

        readonly Image detailIcon, detailGlow, statusDot;
        readonly TextMeshProUGUI detailName, detailType, detailStatus, detailDescription, tuningLabel;
        readonly DbButton toggleButton;
        readonly DbScroll detailScroll;
        readonly RectTransform tuningHost;
        readonly List<Action> tuningRefreshers = new List<Action>();
        readonly CanvasGroup detailGroup;
        readonly RectTransform detailTop;

        CharacterPowers.PowerStats selected;
        string query = "";
        bool cellsAnimated;

        public DbWindow Window { get { return window; } }
        public CharacterPowers.PowerStats Selected { get { return selected; } }
        public int ShownCount { get { return shown.Count; } }

        sealed class Cell
        {
            public CanvasGroup Group;
            public DbButton Button;
            public Image Icon, Frame, Led, Glow;
            public TextMeshProUGUI Name, Tag;
            public CharacterPowers.PowerStats Power;
        }

        readonly Dictionary<RectTransform, Cell> cells = new Dictionary<RectTransform, Cell>();

        public PowerMenuWindow(PowerContextMenu owner, CharacterPowers powers, PersonBehaviour person, CharacterPowers.PowerStats initial)
        {
            this.owner = owner;
            this.powers = powers;
            this.person = person;
            instance = powers ? powers.gameObject : person.gameObject;
            all = powers.GetOrderedPowers();

            window = DbWindow.Create("powers", WindowW, WindowH, "Powers", FighterName(), Accent);
            if (!window) return;
            window.BindLifetime(owner.gameObject);
            window.OnClosed = owner.HandleWindowClosed;
            window.SetHint("Click to inspect  ·  Double-click to switch on or off  ·  Right-click to switch without selecting");

            float bodyW = WindowW - 48f, bodyH = WindowH - 118f;
            float gridW = bodyW - DetailW - Gap;

            string[] names = { "All", "Active", "Off", "Passive" };
            filters = new DbChips(window.Body, 0f, 2f, 36f, names, Accent);
            filters.OnChanged = _ => ApplyFilter(true);

            search = DbUi.Field(window.Body, "", "Search powers...", Accent, false, true, "Search");
            search.Rt().Pin(gridW - 250f, 0f, 250f, ToolbarH - 2f);
            search.OnChanged = v => { query = (v ?? "").Trim(); ApplyFilter(true); };

            gridScroll = DbUi.Scroll(window.Body, Accent, "Grid");
            gridScroll.Rt().Pin(0f, ToolbarH + 10f, gridW, bodyH - ToolbarH - 10f);

            var selectorImage = DbUi.Img(gridScroll.Content, DbAtlas.ChamferTightLine, Accent, "Selector");
            selector = selectorImage.Rt();
            selector.anchorMin = selector.anchorMax = new Vector2(0f, 1f);
            selector.pivot = new Vector2(.5f, .5f);
            selector.sizeDelta = new Vector2(CellW + 8f, CellH + 8f);
            selector.gameObject.SetActive(false);

            float innerW = Columns * CellW + (Columns - 1) * CellGap;
            float pad = Mathf.Max(4f, (gridW - 12f - innerW) * .5f);
            grid = new DbRecycler(gridScroll, Columns, CellW, CellH, CellGap, CellGap, CreateCell, BindCell, pad, 6f);

            emptyLabel = DbUi.Label(gridScroll.Viewport, "Nothing here.", DbTextStyle.Body, DbTheme.TextMuted, TextAlignmentOptions.Center, "Empty");
            emptyLabel.Rt().Stretch();
            emptyLabel.gameObject.SetActive(false);

            // Detail column.
            float dx = gridW + Gap;
            var detail = DbUi.Well(window.Body, "Detail");
            detail.Rt().Pin(dx, 0f, DetailW, bodyH);
            detailGroup = detail.gameObject.AddComponent<CanvasGroup>();

            detailTop = DbUi.Node("Top", detail.transform);
            detailTop.Pin(0f, 0f, DetailW, 164f);
            detailGlow = DbUi.Img(detailTop, DbAtlas.Glow, DbTheme.Alpha(Accent, .12f), "Glow");
            detailGlow.Rt().Pin(-40f, -30f, 240f, 240f);
            window.BindAccent(detailGlow, .12f);
            detailIcon = DbUi.Img(detailTop, null, Color.white, "Icon");
            detailIcon.preserveAspect = true;
            detailIcon.Rt().Pin(16f, 16f, 132f, 132f);
            var iconFrame = DbUi.Img(detailTop, DbAtlas.ChamferTightLine, DbTheme.Alpha(Accent, .8f), "IconFrame");
            iconFrame.Rt().Pin(16f, 16f, 132f, 132f);
            window.BindAccent(iconFrame, .8f);

            detailType = DbUi.Label(detailTop, "", DbTextStyle.Caps, DbTheme.Gold, TextAlignmentOptions.MidlineLeft, "Type");
            detailType.Rt().Pin(164f, 18f, DetailW - 180f, 20f);
            detailName = DbUi.Label(detailTop, "", DbTextStyle.Heading, DbTheme.Text, TextAlignmentOptions.TopLeft, "Name");
            detailName.fontSize = 25f;
            detailName.fontStyle = FontStyles.Bold | FontStyles.Italic;
            detailName.enableWordWrapping = true;
            detailName.overflowMode = TextOverflowModes.Ellipsis;
            detailName.Rt().Pin(164f, 40f, DetailW - 180f, 68f);
            statusDot = DbUi.Img(detailTop, DbAtlas.Disc, DbTheme.TextMuted, "StatusDot");
            statusDot.Rt().Pin(164f, 122f, 11f, 11f);
            detailStatus = DbUi.Label(detailTop, "", DbTextStyle.Small, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "Status");
            detailStatus.Rt().Pin(182f, 114f, DetailW - 198f, 26f);

            toggleButton = DbUi.Button(detail.transform, "Activate", DbButtonStyle.Primary, ToggleSelected, Accent, "Toggle");
            toggleButton.Rt().Pin(16f, 170f, DetailW - 32f, 46f);
            toggleButton.Label.fontSize = 15f;

            detailScroll = DbUi.Scroll(detail.transform, Accent, "DetailScroll");
            detailScroll.Rt().Pin(16f, 228f, DetailW - 26f, bodyH - 240f);

            detailDescription = DbUi.Label(detailScroll.Content, "", DbTextStyle.Body, DbTheme.TextSoft, TextAlignmentOptions.TopLeft, "Description");
            detailDescription.enableWordWrapping = true;
            detailDescription.overflowMode = TextOverflowModes.Overflow;
            detailDescription.Rt().Pin(0f, 0f, DetailW - 44f, 40f);

            tuningLabel = DbUi.SectionLabel(detailScroll.Content, "Tuning", Accent, "TuningLabel");
            tuningLabel.color = DbTheme.Gold;
            tuningLabel.Rt().Pin(0f, 60f, DetailW - 44f, 22f);

            tuningHost = DbUi.Node("Tuning", detailScroll.Content);
            tuningHost.Pin(0f, 86f, DetailW - 44f, 10f);

            window.SetTick(.25f, RefreshLive);

            ApplyFilter(false);
            Select(initial ?? (all.Count > 0 ? all[0] : null), false);
            AnimateVisibleCells();
        }

        string FighterName()
        {
            var identity = person ? person.GetComponent<DbpuCharacterIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.DisplayName)) return identity.DisplayName;
            return person ? person.name.Replace("(Clone)", "") : "";
        }

        static string TypeLabel(CharacterPowers.PowerStats power)
        {
            if (power.Passive) return "Passive";
            switch (power.PowerType)
            {
                case CharacterPowers.PowerGroups.KiBlast: return "Ki blast";
                case CharacterPowers.PowerGroups.Beam:
                case CharacterPowers.PowerGroups.StraightBeam: return "Beam";
                case CharacterPowers.PowerGroups.Solar: return "Solar Flare";
                case CharacterPowers.PowerGroups.Shield: return "Barrier";
                case CharacterPowers.PowerGroups.FinalExplosion: return "Explosion";
                case CharacterPowers.PowerGroups.Hellzone: return "Trap";
                case CharacterPowers.PowerGroups.Ball: return "Energy ball";
                case CharacterPowers.PowerGroups.GogetaSpecial: return "Gogeta special";
                case CharacterPowers.PowerGroups.Growth: return "Growth";
                case CharacterPowers.PowerGroups.DisasterRay: return "Disaster Ray";
                case CharacterPowers.PowerGroups.EyeLaser: return "Eye laser";
                case CharacterPowers.PowerGroups.BeerusSpecial: return "Destruction";
                default: return "Ability";
            }
        }

        RectTransform CreateCell(RectTransform parent, int index)
        {
            var rt = DbUi.Node("Cell", parent);
            var cell = new Cell();
            cell.Group = rt.gameObject.AddComponent<CanvasGroup>();
            cell.Button = rt.gameObject.AddComponent<DbButton>();
            cell.Button.Style = DbButtonStyle.Surface;
            cell.Button.Accent = Accent;
            cell.Button.HoverScale = 1.04f;
            cell.Button.PressScale = .96f;
            cell.Button.Hit = rt.gameObject.AddComponent<DbHit>();
            cell.Button.Back = DbUi.Img(rt, DbAtlas.ChamferTight, DbTheme.Surface, "Back");
            cell.Button.Back.Rt().Stretch();
            cell.Button.Outline = DbUi.Img(rt, DbAtlas.ChamferTightLine, DbTheme.Stroke, "Line");
            cell.Button.Outline.Rt().Stretch();
            cell.Glow = DbUi.Img(rt, DbAtlas.Glow, DbTheme.Alpha(DbTheme.Success, 0f), "Glow");
            cell.Glow.Rt().Pin(-10f, -18f, CellW + 20f, CellW + 20f);
            cell.Icon = DbUi.Img(rt, null, Color.white, "Icon");
            cell.Icon.preserveAspect = true;
            cell.Icon.Rt().Pin(15f, 10f, CellW - 30f, CellW - 30f);
            cell.Frame = DbUi.Img(rt, DbAtlas.RoundLine, DbTheme.Stroke, "IconFrame");
            cell.Frame.Rt().Pin(15f, 10f, CellW - 30f, CellW - 30f);
            cell.Led = DbUi.Img(rt, DbAtlas.Disc, DbTheme.Success, "Led");
            cell.Led.Rt().PinRight(9f, 7f, 12f, 12f);
            cell.Name = DbUi.Label(rt, "", DbTextStyle.Small, DbTheme.Text, TextAlignmentOptions.Center, "Name");
            cell.Name.fontSize = 13f;
            cell.Name.fontStyle = FontStyles.Bold;
            cell.Name.enableWordWrapping = true;
            cell.Name.lineSpacing = -8f;
            cell.Name.Rt().Pin(6f, CellW - 18f, CellW - 12f, 44f);
            cells[rt] = cell;
            return rt;
        }

        void BindCell(RectTransform rt, int index)
        {
            var cell = cells[rt];
            var power = shown[index];
            cell.Power = power;
            cell.Icon.sprite = DbArt.PowerIcon(power.Icon);
            cell.Name.text = DbTheme.StripTags(power.Name);
            cell.Button.OnClick = () => Select(power, true);
            cell.Button.OnDoubleClick = () => Toggle(power);
            cell.Button.OnRightClick = () => Toggle(power);
            cell.Button.OnHover = null;
            ShowCellState(cell, false);
            if (selected == power) MoveSelector(rt, true);
        }

        void ShowCellState(Cell cell, bool animate)
        {
            var power = cell.Power;
            if (power == null) return;
            bool on = power.Passive || power.Active;
            Color icon = on ? Color.white : new Color(.5f, .52f, .6f, .85f);
            Color led = power.Passive ? DbTheme.Ki : on ? DbTheme.Success : DbTheme.Alpha(DbTheme.TextMuted, .4f);
            Color glow = DbTheme.Alpha(power.Passive ? DbTheme.Ki : DbTheme.Success, on ? .05f : 0f);
            Color name = on ? DbTheme.Text : DbTheme.TextMuted;
            if (animate)
            {
                DbMotion.Tint(cell.Icon, icon, .2f);
                DbMotion.Tint(cell.Led, led, .2f);
                DbMotion.Tint(cell.Glow, glow, .2f);
                DbMotion.Tint(cell.Name, name, .2f);
            }
            else
            {
                cell.Icon.color = icon;
                cell.Led.color = led;
                cell.Glow.color = glow;
                cell.Name.color = name;
            }
        }

        void AnimateVisibleCells()
        {
            if (grid == null) return;
            cellsAnimated = true;
            grid.ForEachVisible((rt, index) =>
            {
                var cell = cells[rt];
                int row = index / Columns, col = index % Columns;
                float delay = .05f + (row + col) * .022f;
                cell.Group.alpha = 0f;
                rt.localScale = Vector3.one * .86f;
                DbMotion.Fade(cell.Group, 1f, .16f, DbEase.OutQuad, delay);
                DbMotion.Scale(rt, 1f, .26f, DbEase.OutBack, delay);
            });
        }

        void ApplyFilter(bool animate)
        {
            shown.Clear();
            int mode = filters != null ? filters.Selected : 0;
            for (int i = 0; i < all.Count; i++)
            {
                var power = all[i];
                bool keep;
                switch (mode)
                {
                    case 1: keep = power.Passive || power.Active; break;
                    case 2: keep = !power.Passive && !power.Active; break;
                    case 3: keep = power.Passive; break;
                    default: keep = true; break;
                }
                if (keep && query.Length > 0)
                    keep = (power.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || (power.Description ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                if (keep) shown.Add(power);
            }
            selector.gameObject.SetActive(false);
            grid.SetCount(shown.Count);
            emptyLabel.gameObject.SetActive(shown.Count == 0);
            int index = shown.IndexOf(selected);
            if (index >= 0)
            {
                var rt = grid.CellFor(index);
                if (rt) MoveSelector(rt, true);
            }
            if (animate) AnimateVisibleCells();
            UpdateSubtitle();
        }

        void UpdateSubtitle()
        {
            int active = 0;
            for (int i = 0; i < all.Count; i++) if (all[i].Passive || all[i].Active) active++;
            window.SetSubtitle(FighterName() + "   /   " + active + " of " + all.Count + " active");
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

        public void Select(CharacterPowers.PowerStats power, bool animate)
        {
            if (power == null)
            {
                detailGroup.alpha = 0f;
                return;
            }
            detailGroup.alpha = 1f;
            bool changed = selected != power;
            selected = power;
            owner.RememberSelection(all.IndexOf(power));

            int index = shown.IndexOf(power);
            if (index >= 0)
            {
                grid.Reveal(index);
                var rt = grid.CellFor(index);
                if (rt) MoveSelector(rt, !animate);
            }

            detailIcon.sprite = DbArt.PowerIcon(power.Icon);
            detailName.text = DbTheme.StripTags(power.Name);
            detailType.text = TypeLabel(power);
            detailDescription.text = power.Description ?? "";
            RefreshDetailState(false);
            BuildTuning(power);

            if (animate && changed)
            {
                var iconRt = detailIcon.Rt();
                iconRt.localScale = Vector3.one * .86f;
                DbMotion.Scale(iconRt, 1f, .26f, DbEase.OutBack);
                var nameRt = detailName.Rt();
                var home = nameRt.anchoredPosition;
                nameRt.anchoredPosition = home + new Vector2(24f, 0f);
                DbMotion.Move(nameRt, home, .22f, DbEase.OutQuint);
            }
        }

        void RefreshDetailState(bool animate)
        {
            var power = selected;
            if (power == null) return;
            bool on = power.Passive || power.Active;
            detailStatus.text = powers.GetStatus(power);
            Color dot = power.Passive ? DbTheme.Ki : on ? DbTheme.Success : DbTheme.Danger;
            if (animate) DbMotion.Tint(statusDot, dot, .18f);
            else statusDot.color = dot;
            toggleButton.Interactable = !power.Passive;
            toggleButton.SetText(power.Passive ? "Always active" : power.Active ? "Switch off" : "Activate");
            toggleButton.SetAccent(power.Passive ? DbTheme.Ki : power.Active ? DbTheme.Danger : Accent, animate);
        }

        void BuildTuning(CharacterPowers.PowerStats power)
        {
            for (int i = tuningHost.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(tuningHost.GetChild(i).gameObject);
            tuningRefreshers.Clear();

            float width = DetailW - 44f;
            float descHeight = Mathf.Max(24f, DbUi.PreferredHeight(detailDescription, detailDescription.text, width));
            detailDescription.Rt().sizeDelta = new Vector2(width, descHeight);
            float y = descHeight + 18f;

            var entries = new List<PowerMenuConfigEntry>();
            foreach (var page in PowerMenuConfigData.GetPowerPages(power, instance, person))
                entries.AddRange(page);

            bool hasTuning = entries.Count > 0;
            tuningLabel.gameObject.SetActive(hasTuning);
            tuningLabel.Rt().Pin(0f, y, width, 22f);
            float rowsTop = y + 26f;
            tuningHost.Pin(0f, rowsTop, width, 10f);

            float rowY = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                float rowHeight;
                var row = BuildEntry(entries[i], width, out rowHeight);
                row.Pin(0f, rowY, width, rowHeight);
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                DbMotion.Fade(group, 1f, .16f, DbEase.OutQuad, .03f + i * .025f);
                rowY += rowHeight + 6f;
            }
            detailScroll.SetContentHeight(rowsTop + rowY + 4f, false);
        }

        RectTransform BuildEntry(PowerMenuConfigEntry entry, float width, out float height)
        {
            var rt = DbUi.Node(entry.Name, tuningHost);
            var back = DbUi.Img(rt, DbAtlas.ChamferTight, DbTheme.Surface, "Back");
            back.Rt().Stretch();

            if (entry is PowerMenuConfigToggleEntry toggle)
            {
                height = 46f;
                var name = DbUi.Label(rt, entry.Name, DbTextStyle.Body, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Name");
                name.Rt().Pin(14f, 0f, width - 90f, height);
                var sw = DbUi.Switch(rt, toggle.GetValue, v => toggle.SetValue?.Invoke(v), Accent);
                sw.Rt().anchorMin = sw.Rt().anchorMax = sw.Rt().pivot = new Vector2(1f, .5f);
                sw.Rt().anchoredPosition = new Vector2(-12f, 0f);
                sw.Rt().sizeDelta = new Vector2(54f, 28f);
                tuningRefreshers.Add(sw.Refresh);
            }
            else if (entry is PowerMenuConfigSliderEntry slider)
            {
                height = 64f;
                var name = DbUi.Label(rt, entry.Name, DbTextStyle.Body, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Name");
                name.Rt().Pin(14f, 6f, width * .62f, 26f);
                var value = DbUi.Label(rt, slider.FormatDisplay(slider.ReadDisplay()), DbTextStyle.Heading, DbTheme.Gold, TextAlignmentOptions.MidlineRight, "Value");
                value.fontSize = 17f;
                value.Rt().PinRight(14f, 6f, width * .3f, 26f);
                var control = DbUi.Slider(rt, slider.Min, slider.Max, slider.ReadDisplay, v =>
                {
                    slider.WriteDisplay(Mathf.Clamp(v, slider.Min, slider.Max));
                    value.text = slider.FormatDisplay(slider.ReadDisplay());
                }, Accent);
                control.Rt().Pin(14f, 34f, width - 28f, 24f);
                tuningRefreshers.Add(() =>
                {
                    control.Refresh();
                    value.text = slider.FormatDisplay(slider.ReadDisplay());
                });
            }
            else
            {
                height = 46f;
                var action = entry as PowerMenuConfigActionEntry;
                UnityEngine.Object.Destroy(back.gameObject);
                var button = DbUi.Button(rt, entry.Name, DbButtonStyle.Surface, () => action?.OnClick?.Invoke(), Accent, "Action");
                button.Rt().Stretch();
            }
            return rt;
        }

        void ToggleSelected()
        {
            if (selected != null) Toggle(selected);
        }

        void Toggle(CharacterPowers.PowerStats power)
        {
            if (power == null || power.Passive) return;
            powers.ApplyPower(power.ID);
            RefreshStates(true);
            if (selected != power) Select(power, true);
            else RefreshDetailState(true);
            PulseCell(power);
        }

        void PulseCell(CharacterPowers.PowerStats power)
        {
            int index = shown.IndexOf(power);
            if (index < 0) return;
            var rt = grid.CellFor(index);
            if (!rt) return;
            rt.localScale = Vector3.one * 1.1f;
            DbMotion.Scale(rt, 1f, .28f, DbEase.OutBack);
        }

        void RefreshStates(bool animate)
        {
            powers.HarvestPowerStates();
            foreach (var pair in cells)
                if (pair.Key.gameObject.activeSelf) ShowCellState(pair.Value, animate);
            UpdateSubtitle();
        }

        // Powers can change from outside the menu (hotkeys, deaths); mirror them while open.
        void RefreshLive()
        {
            if (!powers) return;
            RefreshStates(true);
            RefreshDetailState(true);
            for (int i = 0; i < tuningRefreshers.Count; i++) tuningRefreshers[i]();
        }
    }
}
