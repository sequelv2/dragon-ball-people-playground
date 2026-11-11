using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    // Dresses the large editor tools in the menu style without touching how they lay out their screens.
    // Plain greys become theme colours, flat boxes get the chamfered shape, buttons gain a hover response.
    public static class DbSkin
    {
        const float BorderPixels = 14f;

        public static void Attach(GameObject root)
        {
            if (!root) return;
            DbAtlas.Ensure();
            if (!root.GetComponent<DbSkinSweep>()) root.AddComponent<DbSkinSweep>();
        }

        // Canvas units per local unit: the tools draw in small units and scale their panels up.
        public static float UnitScale(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas.transform : null;
            float rootScale = root ? Mathf.Max(.0001f, root.lossyScale.x) : 1f;
            return Mathf.Max(.0001f, Mathf.Abs(t.lossyScale.x) / rootScale);
        }

        // Keeps a sliced corner the same on-screen size whatever scale the tool draws at.
        public static void Shape(Image image, Sprite sprite, float cornerUnits)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Mathf.Max(.01f, BorderPixels * UnitScale(image.transform) / Mathf.Max(1f, cornerUnits));
        }

        public static Color Remap(Color c)
        {
            float max = c.maxColorComponent, min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max - min > .09f || max > .45f) return c;
            Color mapped;
            if (max < .05f) mapped = DbTheme.Well;
            else if (max < .09f) mapped = DbTheme.Panel;
            else if (max < .135f) mapped = DbTheme.PanelTop;
            else if (max < .24f) mapped = DbTheme.Surface;
            else mapped = DbTheme.SurfaceHover;
            mapped.a = c.a;
            return mapped;
        }

        // Window frame: chamfered panel and a thin outline in place of the old white border.
        public static void Frame(Image panel)
        {
            DbAtlas.Ensure();
            Shape(panel, DbAtlas.Chamfer, 22f);
            panel.color = DbTheme.Alpha(DbTheme.Panel, .985f);
            var old = panel.GetComponent<Outline>();
            if (old) Object.Destroy(old);
            var line = DbUi.Img(panel.transform, DbAtlas.ChamferLine, DbTheme.StrokeStrong, "Outline");
            line.Rt().Stretch();
            line.pixelsPerUnitMultiplier = panel.pixelsPerUnitMultiplier;
            line.transform.SetAsFirstSibling();
        }

        public static void TitleBar(Image bar, TextMeshProUGUI title)
        {
            DbAtlas.Ensure();
            bar.color = DbTheme.PanelTop;
            // A sprite marks the bar as already styled, so the sweep leaves it rectangular.
            bar.sprite = DbAtlas.Hairline;
            bar.type = Image.Type.Simple;
            var rule = DbUi.Img(bar.transform, DbAtlas.Hairline, DbTheme.Alpha(DbTheme.Orange, .85f), "Rule");
            var ruleRt = rule.Rt();
            ruleRt.anchorMin = Vector2.zero;
            ruleRt.anchorMax = new Vector2(1f, 0f);
            ruleRt.pivot = new Vector2(.5f, 0f);
            ruleRt.offsetMin = Vector2.zero;
            ruleRt.offsetMax = new Vector2(0f, 2f / DbSkin.UnitScale(bar.transform));
            if (title)
            {
                title.font = DbTheme.Font;
                title.fontStyle = FontStyles.Bold | FontStyles.Italic | FontStyles.UpperCase;
                title.characterSpacing = 2f;
                title.color = DbTheme.Gold;
            }
        }

        public static DbButton CloseButton(Transform bar, float height, System.Action close)
        {
            var button = DbUi.IconButton(bar, DbAtlas.Close, close, DbTheme.TextSoft, DbTheme.Danger, "Close");
            var rt = button.Rt();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, .5f);
            rt.pivot = new Vector2(1f, .5f);
            rt.anchoredPosition = new Vector2(-height * .12f, 0f);
            rt.sizeDelta = new Vector2(height * .78f, height * .78f);
            button.Icon.Rt().sizeDelta = new Vector2(height * .55f, height * .55f);
            Shape(button.Back, DbAtlas.ChamferTight, 5f);
            button.HoverScale = 1.1f;
            return button;
        }

        internal static void StyleResizeHandle(Image handle, bool visible, float size)
        {
            DbAtlas.Ensure();
            handle.color = new Color(1f, 1f, 1f, 0f);
            handle.canvasRenderer.cullTransparentMesh = false;
            if (!visible) return;
            var grip = DbUi.Img(handle.transform, DbAtlas.Grip, DbTheme.TextMuted, "Grip");
            grip.Rt().anchorMin = grip.Rt().anchorMax = new Vector2(0f, 1f);
            grip.Rt().pivot = new Vector2(1f, 0f);
            grip.Rt().anchoredPosition = Vector2.zero;
            float units = Mathf.Min(size, 22f / UnitScale(handle.transform));
            grip.Rt().sizeDelta = new Vector2(units, units);
        }
    }

    // Applies the skin to whatever a tool has just built. Hierarchy size is checked each frame, a
    // constant-time read, so new screens are dressed before they are drawn; the full walk stays rare.
    public sealed class DbSkinSweep : MonoBehaviour
    {
        readonly HashSet<int> done = new HashSet<int>();
        readonly List<Image> images = new List<Image>();
        readonly List<Button> buttons = new List<Button>();
        readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        readonly List<TMP_InputField> fields = new List<TMP_InputField>();
        readonly List<Outline> outlines = new List<Outline>();
        int lastCount = -1;
        float nextSweep;

        void LateUpdate()
        {
            int count = transform.hierarchyCount;
            float now = Time.unscaledTime;
            if (count == lastCount && now < nextSweep) return;
            lastCount = count;
            nextSweep = now + .5f;
            Sweep();
        }

        void Sweep()
        {
            GetComponentsInChildren(true, fields);
            for (int i = 0; i < fields.Count; i++)
                if (done.Add(fields[i].GetInstanceID())) StyleField(fields[i]);

            GetComponentsInChildren(true, images);
            for (int i = 0; i < images.Count; i++)
                if (done.Add(images[i].GetInstanceID())) StyleImage(images[i]);

            GetComponentsInChildren(true, buttons);
            for (int i = 0; i < buttons.Count; i++)
                if (done.Add(buttons[i].GetInstanceID())) StyleButton(buttons[i]);

            GetComponentsInChildren(true, labels);
            for (int i = 0; i < labels.Count; i++)
                if (done.Add(labels[i].GetInstanceID())) labels[i].font = DbTheme.DenseFont;

            GetComponentsInChildren(true, outlines);
            for (int i = 0; i < outlines.Count; i++)
                if (done.Add(outlines[i].GetInstanceID()) && outlines[i].effectColor.maxColorComponent > .8f)
                    outlines[i].effectColor = DbTheme.StrokeStrong;
        }

        static void StyleField(TMP_InputField field)
        {
            var back = field.GetComponent<Image>();
            if (back) back.color = DbTheme.Well;
            if (field.textComponent) field.textComponent.color = DbTheme.Text;
            if (field.placeholder) field.placeholder.color = DbTheme.TextMuted;
            field.customCaretColor = true;
            field.caretColor = DbTheme.Orange;
            field.selectionColor = DbTheme.Alpha(DbTheme.Orange, .4f);
        }

        static void StyleImage(Image image)
        {
            if (image.sprite) return;
            Color color = image.color;
            if (color.a < .02f) return;
            image.color = DbSkin.Remap(color);

            var rt = image.rectTransform;
            float scale = DbSkin.UnitScale(rt);
            Vector2 size = rt.rect.size * scale;
            if (Mathf.Min(size.x, size.y) < 14f) return;
            // Full-screen dimmers stay rectangles, and shapes under a slider belong to the slider.
            if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && color.a < .7f) return;
            if (image.GetComponentInParent<Slider>()) return;
            DbSkin.Shape(image, DbAtlas.ChamferTight, 6f);
        }

        static void StyleButton(Button button)
        {
            var graphic = button.targetGraphic ? button.targetGraphic : button.GetComponent<Graphic>();
            if (!graphic) return;
            button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            // Multiplies on top of whatever colour the tool sets, so selected states stay intact.
            button.colors = new ColorBlock
            {
                normalColor = new Color(.72f, .72f, .72f, 1f),
                highlightedColor = new Color(1f, 1f, 1f, 1f),
                pressedColor = new Color(.52f, .52f, .52f, 1f),
                selectedColor = new Color(.72f, .72f, .72f, 1f),
                disabledColor = new Color(.42f, .42f, .42f, .7f),
                colorMultiplier = 1.39f,
                fadeDuration = .08f
            };
            if (!button.GetComponent<DbSkinPop>()) button.gameObject.AddComponent<DbSkinPop>();
        }
    }

    public sealed class DbSkinPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        bool over;

        float Hover
        {
            get
            {
                var rt = (RectTransform)transform;
                return rt.rect.width * DbSkin.UnitScale(rt) > 260f ? 1.01f : 1.035f;
            }
        }

        public void OnPointerEnter(PointerEventData e) { over = true; DbMotion.Scale(transform, Hover, .12f, DbEase.OutBack); }
        public void OnPointerExit(PointerEventData e) { over = false; DbMotion.Scale(transform, 1f, .14f); }
        public void OnPointerDown(PointerEventData e) { DbMotion.Scale(transform, .97f, .06f); }
        public void OnPointerUp(PointerEventData e) { DbMotion.Scale(transform, over ? Hover : 1f, .16f, DbEase.OutBack); }
    }
}
