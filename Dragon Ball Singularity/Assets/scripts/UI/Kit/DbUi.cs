using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    // Factory and layout helpers for the menu kit. Layout is absolute (no layout groups), so
    // opening a window never triggers layout rebuilds and rows can be recycled freely.
    public static class DbUi
    {
        public const float DesignWidth = 1920f, DesignHeight = 1080f;

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // Pins to the parent's top-left corner, in design units with y going down.
        public static RectTransform Pin(this RectTransform rt, float left, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Centre(this RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static RectTransform PinRight(this RectTransform rt, float right, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -top);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static RectTransform PinBottom(this RectTransform rt, float left, float right, float bottom, float height)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image", bool raycast = false)
        {
            DbAtlas.Ensure();
            var rt = Node(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.canvasRenderer.cullTransparentMesh = true;
            image.type = sprite && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        // Transparent graphic that only receives pointer input; produces no draw call.
        public static DbHit Hit(Transform parent, string name = "Hit")
        {
            var rt = Node(name, parent);
            return rt.gameObject.AddComponent<DbHit>();
        }

        public static RectTransform Rt(this Component c) { return (RectTransform)c.transform; }

        // Fully rounded ends at this thickness; otherwise the sliced caps squash into points.
        public static void PillFit(Image image, float thickness)
        {
            image.pixelsPerUnitMultiplier = 64f / Mathf.Max(1f, thickness);
        }

        static Material shadowMaterial;
        static TMP_FontAsset shadowFont;

        static Material ShadowMaterial(TMP_FontAsset font)
        {
            if (shadowMaterial && shadowFont == font) return shadowMaterial;
            shadowFont = font;
            shadowMaterial = new Material(font.material) { name = "DBS UI Shadowed", hideFlags = HideFlags.HideAndDontSave };
            shadowMaterial.EnableKeyword("UNDERLAY_ON");
            shadowMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, .55f));
            shadowMaterial.SetFloat("_UnderlayOffsetX", .35f);
            shadowMaterial.SetFloat("_UnderlayOffsetY", -.45f);
            shadowMaterial.SetFloat("_UnderlaySoftness", .35f);
            shadowMaterial.SetFloat("_UnderlayDilate", .1f);
            return shadowMaterial;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, DbTextStyle style, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, string name = "Text")
        {
            var rt = Node(name, parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = DbTheme.Font;
            label.font = font;
            label.raycastTarget = false;
            label.alignment = align;
            label.color = color;
            label.richText = true;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableKerning = true;
            ApplyStyle(label, style, font);
            label.text = text ?? "";
            return label;
        }

        public static void ApplyStyle(TextMeshProUGUI label, DbTextStyle style, TMP_FontAsset font = null)
        {
            var display = DbTheme.DisplayFont;
            switch (style)
            {
                case DbTextStyle.Hero:
                    label.font = display;
                    label.fontSize = 44f; label.fontStyle = FontStyles.Bold | FontStyles.Italic; label.characterSpacing = 1f;
                    label.fontSharedMaterial = ShadowMaterial(display);
                    break;
                case DbTextStyle.Title:
                    label.font = display;
                    label.fontSize = 26f; label.fontStyle = FontStyles.Bold | FontStyles.Italic | FontStyles.UpperCase; label.characterSpacing = 2f;
                    break;
                case DbTextStyle.Heading:
                    label.font = display;
                    label.fontSize = 20f; label.fontStyle = FontStyles.Normal; label.characterSpacing = .5f;
                    break;
                case DbTextStyle.Body:
                    label.fontSize = 16.5f; label.fontStyle = FontStyles.Normal; label.lineSpacing = 6f;
                    break;
                case DbTextStyle.Small:
                    label.fontSize = 13.5f; label.fontStyle = FontStyles.Normal;
                    break;
                case DbTextStyle.Caps:
                    label.font = display;
                    label.fontSize = 12.5f; label.fontStyle = FontStyles.Bold | FontStyles.UpperCase; label.characterSpacing = 2.5f;
                    break;
                case DbTextStyle.Number:
                    label.font = display;
                    label.fontSize = 32f; label.fontStyle = FontStyles.Bold | FontStyles.Italic; label.characterSpacing = 1f;
                    label.fontSharedMaterial = ShadowMaterial(display);
                    break;
            }
        }

        // The editor tools can reset the shared count, so a late release must not push it below zero.
        public static void Release(Global owner)
        {
            if (owner && owner.ActiveUiBlockers > 0) owner.RemoveUiBlocker();
        }

        public static void Blip()
        {
            if (UISoundBehaviour.Main) UISoundBehaviour.Main.Blip();
        }

        public static DbButton Button(Transform parent, string text, DbButtonStyle style, Action onClick, Color? accent = null, string name = "Button")
        {
            var rt = Node(name, parent);
            var button = rt.gameObject.AddComponent<DbButton>();
            Color tint = accent ?? DbTheme.Orange;
            bool slanted = style == DbButtonStyle.Primary;
            Sprite shape = slanted ? DbAtlas.Slant : DbAtlas.ChamferTight;
            button.Back = Img(rt, shape, Color.white, "Back");
            button.Back.Rt().Stretch();
            if (style != DbButtonStyle.Primary)
            {
                button.Outline = Img(rt, DbAtlas.ChamferTightLine, DbTheme.Stroke, "Line");
                button.Outline.Rt().Stretch();
            }
            button.Hit = rt.gameObject.AddComponent<DbHit>();
            if (!string.IsNullOrEmpty(text))
            {
                button.Label = Label(rt, text, DbTextStyle.Caps, Color.white, TextAlignmentOptions.Center, "Label");
                button.Label.Rt().Stretch(slanted ? 18f : 8f, 0f, slanted ? 18f : 8f, 0f);
            }
            button.Style = style;
            button.Accent = tint;
            button.OnClick = onClick;
            button.ApplyInstant();
            return button;
        }

        public static DbButton IconButton(Transform parent, Sprite icon, Action onClick, Color? tint = null, Color? hover = null, string name = "IconButton")
        {
            var rt = Node(name, parent);
            var button = rt.gameObject.AddComponent<DbButton>();
            button.Back = Img(rt, DbAtlas.ChamferTight, Color.white, "Back");
            button.Back.Rt().Stretch();
            button.Hit = rt.gameObject.AddComponent<DbHit>();
            button.Icon = Img(rt, icon, tint ?? DbTheme.TextSoft, "Icon");
            button.Icon.Rt().Centre(0f, 0f, 22f, 22f);
            button.Style = DbButtonStyle.Quiet;
            button.Accent = hover ?? DbTheme.Orange;
            button.OnClick = onClick;
            button.ApplyInstant();
            return button;
        }

        public static DbSwitch Switch(Transform parent, Func<bool> get, Action<bool> set, Color accent, string name = "Switch")
        {
            var rt = Node(name, parent);
            var sw = rt.gameObject.AddComponent<DbSwitch>();
            sw.Build(get, set, accent);
            return sw;
        }

        public static DbSlider Slider(Transform parent, float min, float max, Func<float> get, Action<float> set, Color accent, bool logarithmic = false, bool whole = false, string name = "Slider")
        {
            var rt = Node(name, parent);
            var slider = rt.gameObject.AddComponent<DbSlider>();
            slider.Build(min, max, get, set, accent, logarithmic, whole);
            return slider;
        }

        public static DbField Field(Transform parent, string value, string placeholder, Color accent, bool numeric = false, bool withIcon = false, string name = "Field")
        {
            var rt = Node(name, parent);
            var field = rt.gameObject.AddComponent<DbField>();
            field.Build(value, placeholder, accent, numeric, withIcon);
            return field;
        }

        public static DbScroll Scroll(Transform parent, Color accent, string name = "Scroll")
        {
            var rt = Node(name, parent);
            var scroll = rt.gameObject.AddComponent<DbScroll>();
            scroll.Build(accent);
            return scroll;
        }

        // Soft, slightly raised well used behind content blocks.
        public static Image Well(Transform parent, string name = "Well")
        {
            var image = Img(parent, DbAtlas.ChamferTight, DbTheme.Alpha(DbTheme.Well, .72f), name);
            var line = Img(image.transform, DbAtlas.ChamferTightLine, DbTheme.Stroke, "Line");
            line.Rt().Stretch();
            return image;
        }

        public static TextMeshProUGUI SectionLabel(Transform parent, string text, Color accent, string name = "Section")
        {
            var label = Label(parent, text, DbTextStyle.Caps, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, name);
            return label;
        }

        public static float PreferredWidth(TextMeshProUGUI label, string text = null)
        {
            return label.GetPreferredValues(text ?? label.text, 4000f, 200f).x;
        }

        public static float PreferredHeight(TextMeshProUGUI label, string text, float width)
        {
            return label.GetPreferredValues(text, width, 4000f).y;
        }

        public static GameObject GameCanvas()
        {
            var main = Global.main;
            if (main && main.CanvasScaler) return main.CanvasScaler.gameObject;
            return GameObject.Find("Canvas");
        }
    }

    // Raycast target with no visuals.
    public sealed class DbHit : Graphic
    {
        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
        protected override void OnPopulateMesh(VertexHelper vh) { vh.Clear(); }
    }

    // Icons and illustrations ship with a baked pixel-art frame. The menus draw their own, so the
    // art is shown without it. Sprites share the source texture; nothing is copied.
    public static class DbArt
    {
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        static Sprite Crop(Sprite source, float insetX, float insetY)
        {
            if (!source) return null;
            int key = source.GetInstanceID();
            if (cache.TryGetValue(key, out var cropped) && cropped) return cropped;
            var r = source.rect;
            var rect = new Rect(r.x + insetX, r.y + insetY, r.width - insetX * 2f, r.height - insetY * 2f);
            cropped = Sprite.Create(source.texture, rect, new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cropped.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = cropped;
            return cropped;
        }

        public static Sprite Icon(Sprite source)
        {
            if (!source) return null;
            var r = source.rect;
            if (Mathf.Abs(r.width - r.height) > 1f || r.width < 100f) return source;
            float inset = r.width * .075f;
            return Crop(source, inset, inset);
        }

        // Powers without their own art get a clean bolt instead of the old "ICON" placeholder.
        public static Sprite PowerIcon(Sprite source)
        {
            if (!source || source == DBPU.PIconTemplate) return DbAtlas.Bolt;
            return Icon(source);
        }

        public static Sprite Illustration(Sprite source)
        {
            if (!source) return null;
            var r = source.rect;
            // Some forms reuse their square icon as the illustration.
            if (Mathf.Abs(r.width - r.height) <= 1f) return Icon(source);
            if (r.width < 200f || Mathf.Abs(r.height / r.width - 1472f / 1008f) > .06f) return source;
            float scale = r.width / 1008f;
            return Crop(source, 48f * scale, 48f * scale);
        }
    }
}
