using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Entry = System.Collections.Generic.KeyValuePair<string, DBPU.CharacterSkins.Skins.SkinInfo>;

namespace DBPU
{
    // Wardrobe screen: a searchable list, a large preview and battle-damage variants. Nothing changes
    // on the fighter until Apply; a skin's sprites load only when it is actually selected.
    public sealed class SkinMenuWindow
    {
        const float WindowW = 1180f, WindowH = 740f, ListW = 330f, StageW = 440f, Gap = 18f, RowH = 54f;
        const int MaxVariantButtons = 8;
        static readonly Color Accent = DbTheme.Ki;

        readonly SkinContextMenu menu;
        readonly DbWindow window;
        readonly List<Entry> skins;
        readonly List<int> shown = new List<int>();
        readonly DbStage stage;
        readonly DbScroll listScroll;
        readonly DbRecycler list;
        readonly RectTransform selector;
        readonly TextMeshProUGUI nameLabel, positionLabel, variantNote, emptyLabel;
        readonly List<DbButton> variantButtons = new List<DbButton>();
        readonly Dictionary<RectTransform, Row> rows = new Dictionary<RectTransform, Row>();
        readonly DbButton apply, previous, next;

        string query = "";
        int selected = -1;
        int variants;
        bool accepted;

        public DbWindow Window { get { return window; } }
        public int Selected { get { return selected; } }
        public int ShownCount { get { return shown.Count; } }

        sealed class Row
        {
            public DbButton Button;
            public TextMeshProUGUI Name;
            public Image Check;
            public int Index;
        }

        public SkinMenuWindow(SkinContextMenu menu)
        {
            this.menu = menu;
            skins = menu.RefreshSkinList();
            selected = Mathf.Clamp(menu.Page, 0, skins.Count - 1);

            window = DbWindow.Create("skins", WindowW, WindowH, "Skins", FighterName(), Accent);
            if (!window) return;
            window.BindLifetime(menu.gameObject);
            window.OnClosed = HandleClosed;
            window.SetHint("Pick a skin to preview it  ·  Double-click to apply  ·  Esc to cancel");

            float bodyW = WindowW - 48f, bodyH = WindowH - 118f;

            float listTop = 0f;
            if (skins.Count >= 8)
            {
                var search = DbUi.Field(window.Body, "", "Search skins...", Accent, false, true, "Search");
                search.Rt().Pin(0f, 0f, ListW, 40f);
                search.OnChanged = v => { query = (v ?? "").Trim(); ApplyFilter(); };
                listTop = 48f;
            }

            listScroll = DbUi.Scroll(window.Body, Accent, "List");
            listScroll.Rt().Pin(0f, listTop, ListW, bodyH - listTop);

            var selectorImage = DbUi.Img(listScroll.Content, DbAtlas.ChamferTight, DbTheme.Alpha(Accent, .08f), "Selector");
            selector = selectorImage.Rt();
            selector.anchorMin = selector.anchorMax = new Vector2(0f, 1f);
            selector.pivot = new Vector2(.5f, .5f);
            selector.sizeDelta = new Vector2(ListW - 14f, RowH);
            var selectorBar = DbUi.Img(selector, DbAtlas.Pill, Accent, "Bar");
            DbUi.PillFit(selectorBar, 4f);
            selectorBar.Rt().Pin(0f, 11f, 4f, RowH - 22f);
            selector.gameObject.SetActive(false);

            list = new DbRecycler(listScroll, 1, ListW - 14f, RowH, 0f, 4f, CreateRow, BindRow);

            emptyLabel = DbUi.Label(listScroll.Viewport, "No skin matches.", DbTextStyle.Body, DbTheme.TextMuted, TextAlignmentOptions.Center, "Empty");
            emptyLabel.Rt().Stretch();
            emptyLabel.gameObject.SetActive(false);

            float stageX = ListW + Gap;
            stage = new DbStage(window.Body, StageW, bodyH, window);
            stage.Root.Pin(stageX, 0f, StageW, bodyH);

            previous = NavButton(DbAtlas.ChevronLeft, () => Step(-1), "Previous");
            previous.Rt().anchorMin = previous.Rt().anchorMax = previous.Rt().pivot = new Vector2(0f, .5f);
            previous.Rt().anchoredPosition = new Vector2(8f, 0f);
            next = NavButton(DbAtlas.Chevron, () => Step(1), "Next");
            next.Rt().anchorMin = next.Rt().anchorMax = next.Rt().pivot = new Vector2(1f, .5f);
            next.Rt().anchoredPosition = new Vector2(-8f, 0f);

            positionLabel = DbUi.Label(stage.Overlay, "", DbTextStyle.Caps, DbTheme.TextSoft, TextAlignmentOptions.Center, "Position");
            positionLabel.Rt().PinBottom(20f, 20f, 14f, 22f);

            float x = stageX + StageW + Gap, w = bodyW - x;
            var caption = DbUi.Label(window.Body, "Skin", DbTextStyle.Caps, DbTheme.Gold, TextAlignmentOptions.MidlineLeft, "Caption");
            caption.Rt().Pin(x, 2f, w, 20f);
            nameLabel = DbUi.Label(window.Body, "", DbTextStyle.Heading, DbTheme.Text, TextAlignmentOptions.TopLeft, "SkinName");
            nameLabel.fontSize = 30f;
            nameLabel.fontStyle = FontStyles.Bold | FontStyles.Italic;
            nameLabel.enableWordWrapping = true;
            nameLabel.Rt().Pin(x, 26f, w, 118f);

            var rule = DbUi.Img(window.Body, DbAtlas.Hairline, DbTheme.Alpha(Accent, .8f), "Rule");
            rule.Rt().Pin(x, 150f, w, 2f);

            var damageCaption = DbUi.Label(window.Body, "Battle damage", DbTextStyle.Caps, DbTheme.Gold, TextAlignmentOptions.MidlineLeft, "DamageCaption");
            damageCaption.Rt().Pin(x, 166f, w, 20f);

            float size = Mathf.Min(44f, (w - (MaxVariantButtons - 1) * 6f) / MaxVariantButtons);
            for (int i = 0; i < MaxVariantButtons; i++)
            {
                int variant = i;
                var button = DbUi.Button(window.Body, i.ToString(), DbButtonStyle.Ghost, () => SelectVariant(variant), Accent, "Variant " + i);
                button.Rt().Pin(x + i * (size + 6f), 192f, size, 40f);
                variantButtons.Add(button);
            }
            variantNote = DbUi.Label(window.Body, "", DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.TopLeft, "VariantNote");
            variantNote.enableWordWrapping = true;
            variantNote.Rt().Pin(x, 240f, w, 44f);

            apply = DbUi.Button(window.Body, "Apply skin", DbButtonStyle.Primary, Apply, Accent, "Accept");
            apply.Rt().Pin(x, bodyH - 110f, w, 52f);
            apply.Label.fontSize = 16f;
            var cancel = DbUi.Button(window.Body, "Cancel", DbButtonStyle.Ghost, () => window.Close(), Accent, "Cancel");
            cancel.Rt().Pin(x, bodyH - 48f, w, 46f);

            ApplyFilter();
            Select(selected, 1, false);
            list.ForEachVisible((rt, index) =>
            {
                var group = rt.GetComponent<CanvasGroup>();
                float delay = .05f + index * .016f;
                var home = rt.anchoredPosition;
                group.alpha = 0f;
                rt.anchoredPosition = home + new Vector2(-18f, 0f);
                DbMotion.Fade(group, 1f, .14f, DbEase.OutQuad, delay);
                DbMotion.Move(rt, home, .24f, DbEase.OutQuint, delay);
            });
        }

        DbButton NavButton(Sprite icon, Action click, string name)
        {
            var button = DbUi.IconButton(stage.Overlay, icon, click, DbTheme.Text, Accent, name);
            button.Rt().sizeDelta = new Vector2(46f, 84f);
            button.Icon.Rt().sizeDelta = new Vector2(30f, 30f);
            button.HoverScale = 1.08f;
            return button;
        }

        string FighterName()
        {
            var person = menu.person;
            var identity = person ? person.GetComponent<DbpuCharacterIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.DisplayName)) return identity.DisplayName;
            return person ? person.name.Replace("(Clone)", "") : "";
        }

        static string SkinName(Entry entry)
        {
            string name = entry.Value != null ? DbTheme.StripTags(entry.Value.DisplayName) : "";
            if (string.IsNullOrEmpty(name) && entry.Value != null) name = entry.Value.Name;
            return string.IsNullOrEmpty(name) ? entry.Key : name;
        }

        RectTransform CreateRow(RectTransform parent, int index)
        {
            var rt = DbUi.Node("Row", parent);
            rt.gameObject.AddComponent<CanvasGroup>();
            var row = new Row();
            row.Button = rt.gameObject.AddComponent<DbButton>();
            row.Button.Style = DbButtonStyle.Quiet;
            row.Button.Accent = Accent;
            row.Button.HoverScale = 1f;
            row.Button.PressScale = .985f;
            row.Button.Hit = rt.gameObject.AddComponent<DbHit>();
            row.Button.Back = DbUi.Img(rt, DbAtlas.ChamferTight, Color.clear, "Back");
            row.Button.Back.Rt().Stretch();
            row.Name = DbUi.Label(rt, "", DbTextStyle.Body, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Name");
            row.Name.Rt().Stretch(18f, 0f, 44f, 0f);
            row.Check = DbUi.Img(rt, DbAtlas.Check, DbTheme.Success, "Applied");
            row.Check.Rt().anchorMin = row.Check.Rt().anchorMax = row.Check.Rt().pivot = new Vector2(1f, .5f);
            row.Check.Rt().anchoredPosition = new Vector2(-12f, 0f);
            row.Check.Rt().sizeDelta = new Vector2(22f, 22f);
            row.Button.ApplyInstant();
            row.Button.OnClick = () => Select(row.Index, row.Index >= selected ? 1 : -1, true);
            row.Button.OnDoubleClick = () =>
            {
                Select(row.Index, 1, true);
                Apply();
            };
            rows[rt] = row;
            return rt;
        }

        void BindRow(RectTransform rt, int position)
        {
            var row = rows[rt];
            int index = shown[position];
            row.Index = index;
            var entry = skins[index];
            row.Name.text = SkinName(entry);
            row.Check.enabled = IsApplied(entry);
            if (index == selected) MoveSelector(rt, true);
        }

        bool IsApplied(Entry entry)
        {
            return entry.Value != null && !string.IsNullOrEmpty(menu.CurrentSkin)
                && (entry.Value.Name == menu.CurrentSkin || entry.Key == menu.CurrentSkin);
        }

        void ApplyFilter()
        {
            shown.Clear();
            for (int i = 0; i < skins.Count; i++)
                if (query.Length == 0 || SkinName(skins[i]).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    shown.Add(i);
            selector.gameObject.SetActive(false);
            list.SetCount(shown.Count);
            emptyLabel.gameObject.SetActive(shown.Count == 0);
            int position = shown.IndexOf(selected);
            if (position >= 0)
            {
                var rt = list.CellFor(position);
                if (rt) MoveSelector(rt, true);
            }
        }

        void MoveSelector(RectTransform row, bool instant)
        {
            selector.gameObject.SetActive(true);
            selector.SetAsFirstSibling();
            if (instant)
            {
                DbMotion.Stop(selector);
                selector.anchoredPosition = row.anchoredPosition;
                return;
            }
            DbMotion.Move(selector, row.anchoredPosition, .2f, DbEase.OutCubic);
        }

        void Step(int direction)
        {
            int position = shown.IndexOf(selected);
            if (position < 0 || shown.Count == 0) return;
            int next = position + direction;
            if (next < 0 || next >= shown.Count) return;
            Select(shown[next], direction, true);
        }

        public void Select(int index, int direction, bool animate)
        {
            if (index < 0 || index >= skins.Count) return;
            selected = index;
            menu.Page = index;
            DBPU.DbpuCustomSkinLoader.EnsureSkinSpritesLoaded(menu.SkinsData, skins[index].Key);
            var info = skins[index].Value;
            if (info != null && info.Illustrations == null) info.Illustrations = new Dictionary<string, Sprite>();
            variants = info != null ? info.Illustrations.Count : 0;
            menu.CurrentDamage = variants > 0 ? Mathf.Clamp(menu.CurrentDamage, 0, variants - 1) : 0;

            nameLabel.text = SkinName(skins[index]);
            stage.Show(DbArt.Illustration(ResolveIllustration(info, menu.CurrentDamage)), direction, animate);
            RefreshVariants();
            RefreshPosition();

            int position = shown.IndexOf(index);
            if (position >= 0)
            {
                list.Reveal(position);
                var rt = list.CellFor(position);
                if (rt) MoveSelector(rt, !animate);
            }
        }

        void RefreshPosition()
        {
            int position = shown.IndexOf(selected);
            positionLabel.text = position >= 0 ? (position + 1) + " / " + shown.Count : "";
            previous.Interactable = position > 0;
            next.Interactable = position >= 0 && position < shown.Count - 1;
            bool applied = IsApplied(skins[selected]) && menu.CurrentDamage == menu.PreviousDamage;
            apply.Interactable = !applied;
            apply.SetText(applied ? "Currently worn" : "Apply skin");
        }

        void RefreshVariants()
        {
            for (int i = 0; i < variantButtons.Count; i++)
            {
                bool visible = variants > 1 && i < variants;
                variantButtons[i].gameObject.SetActive(visible);
                if (visible) variantButtons[i].Selected = i == menu.CurrentDamage;
            }
            if (variants <= 1) variantNote.text = "This skin has no damage variants.";
            else if (menu.CurrentDamage == 0) variantNote.text = "Undamaged.";
            else variantNote.text = "Damage level " + menu.CurrentDamage + " of " + (variants - 1) + ".";
        }

        public void SelectVariant(int variant)
        {
            if (variant < 0 || variant >= variants) return;
            int direction = variant >= menu.CurrentDamage ? 1 : -1;
            menu.CurrentDamage = variant;
            stage.Show(DbArt.Illustration(ResolveIllustration(skins[selected].Value, variant)), direction, true);
            RefreshVariants();
            RefreshPosition();
        }

        static Sprite ResolveIllustration(CharacterSkins.Skins.SkinInfo info, int damage)
        {
            if (info == null || info.Illustrations == null) return null;
            if (info.Illustrations.TryGetValue("Damage" + damage, out var sprite) && sprite != null) return sprite;
            if (info.Illustrations.TryGetValue("Damage0", out var first) && first != null) return first;
            foreach (var pair in info.Illustrations)
                if (pair.Value != null) return pair.Value;
            return null;
        }

        void Apply()
        {
            if (selected < 0 || selected >= skins.Count) return;
            var info = skins[selected].Value;
            if (info == null) return;
            accepted = true;
            menu.SkinsData.ApplySkin(menu.person, info.Name, menu.Skin_Suffix, menu.CurrentDamage);
            menu.PreviousDamage = menu.CurrentDamage;
            menu.CurrentSkin = info.Name;
            menu.PreviousSkin = menu.CurrentSkin;
            menu.PreviousPage = selected;
            window.Close();
        }

        void HandleClosed()
        {
            if (!accepted)
            {
                menu.CurrentDamage = menu.PreviousDamage;
                menu.Page = menu.PreviousPage;
            }
            menu.HandleWindowClosed();
        }
    }
}
