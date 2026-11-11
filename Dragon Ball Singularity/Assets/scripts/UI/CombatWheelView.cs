using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // The look shared by both combat wheels. Slots are reused across pages, and the highlight turns
    // as one rotated mesh, so moving the selection rebuilds nothing.
    internal sealed class CombatWheelView
    {
        const float SlotRadius = 172f;
        const int SlotCount = CombatWheelSelection.PageSize;

        sealed class Slot
        {
            public RectTransform Root;
            public Image Icon, Frame, Star;
            public TextMeshProUGUI Name, Number;
            public Color Tint;
            public bool Current;
        }

        readonly List<Slot> slots = new List<Slot>();
        readonly RectTransform panel, slotHost, wedgeRt, hubIconRt;
        readonly CanvasGroup group;
        readonly CombatWheelRing ring;
        readonly CombatWheelWedge wedge;
        readonly Image glow, outerRing, hubRing, kiArc, hubIcon, burst;
        readonly TextMeshProUGUI resource, previewName, intent, line1, line2, line3;
        readonly float scale;
        int count, selectedSlot = -2;
        bool wedgeShown;

        public GameObject Root { get; private set; }

        public CombatWheelView(string canvasName, string panelName, string title, string controls, Vector2 center, float uiScale)
        {
            DbAtlas.Ensure();
            scale = uiScale;
            Root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var shield = DbUi.Img(Root.transform, null, new Color(.01f, .015f, .03f, .06f), "Input shield", true);
            shield.Rt().Stretch();

            panel = new GameObject(panelName, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(Root.transform, false);
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.anchoredPosition = center;
            panel.sizeDelta = new Vector2(600f, 652f);
            group = panel.GetComponent<CanvasGroup>();

            Color accent = DbTheme.Orange;
            glow = Box(DbAtlas.Glow, Vector2.zero, new Vector2(760f, 760f), DbTheme.Alpha(accent, .12f), "Glow");
            Box(DbAtlas.Disc, Vector2.zero, new Vector2(500f, 500f), DbTheme.Alpha(DbTheme.Void, .9f), "Backing");

            var rays = DbUi.Node("Rays", panel);
            rays.Centre(0f, 0f, 660f, 660f);
            rays.gameObject.AddComponent<Canvas>();
            burst = DbUi.Img(rays, DbAtlas.Burst, DbTheme.Alpha(accent, .06f), "Burst");
            burst.Rt().Stretch();
            rays.gameObject.AddComponent<DbSpin>().Speed = 12f;

            ring = new GameObject("Selection sectors", typeof(RectTransform), typeof(CombatWheelRing)).GetComponent<CombatWheelRing>();
            ring.transform.SetParent(panel, false);
            ring.rectTransform.sizeDelta = new Vector2(520f, 520f);
            ring.raycastTarget = false;

            wedge = new GameObject("Selection highlight", typeof(RectTransform), typeof(CombatWheelWedge)).GetComponent<CombatWheelWedge>();
            wedge.transform.SetParent(panel, false);
            wedgeRt = wedge.rectTransform;
            wedgeRt.sizeDelta = new Vector2(520f, 520f);
            wedge.raycastTarget = false;
            wedge.color = DbTheme.Alpha(accent, 0f);

            outerRing = Box(DbAtlas.RingThin, Vector2.zero, new Vector2(484f, 484f), DbTheme.Alpha(accent, .55f), "Outer ring");

            slotHost = DbUi.Node("Slots", panel);
            slotHost.Centre(0f, 0f, 10f, 10f);
            for (int i = 0; i < SlotCount; i++) slots.Add(BuildSlot(i));

            Box(DbAtlas.Disc, Vector2.zero, new Vector2(182f, 182f), DbTheme.Panel, "Hub");
            hubRing = Box(DbAtlas.Ring, Vector2.zero, new Vector2(198f, 198f), DbTheme.Alpha(DbTheme.Text, .08f), "Hub ring");
            kiArc = Box(DbAtlas.Ring, Vector2.zero, new Vector2(198f, 198f), DbTheme.Ki, "Ki arc");
            kiArc.type = Image.Type.Filled;
            kiArc.fillMethod = Image.FillMethod.Radial360;
            kiArc.fillOrigin = (int)Image.Origin360.Top;
            kiArc.fillClockwise = true;
            kiArc.fillAmount = 1f;

            hubIcon = Box(null, new Vector2(0f, 22f), new Vector2(76f, 76f), Color.white, "Preview icon");
            hubIcon.preserveAspect = true;
            hubIcon.enabled = false;
            hubIconRt = hubIcon.Rt();
            previewName = Text(new Vector2(0f, -37f), new Vector2(150f, 34f), "", 15f, Color.white, "Preview name", FontStyles.Bold);
            intent = Text(new Vector2(0f, -62f), new Vector2(150f, 16f), "", 9f, DbTheme.TextSoft, "Intent", FontStyles.Bold | FontStyles.UpperCase);

            var tab = Box(DbAtlas.Slant, new Vector2(0f, 287f), new Vector2(476f, 44f), accent, "Header");
            tab.Rt().SetAsLastSibling();
            headerTab = tab;
            var header = DbUi.Label(tab.transform, title, DbTextStyle.Title, DbTheme.TextOnAccent, TextAlignmentOptions.Center, "Fighter");
            header.Rt().Stretch(28f, 0f, 28f, 0f);
            header.fontSize = 18f;
            header.enableAutoSizing = true;
            header.fontSizeMin = 11f;
            header.fontSizeMax = 18f;

            resource = Text(new Vector2(0f, 253f), new Vector2(300f, 18f), "", 11f, DbTheme.Ki, "Resource", FontStyles.Bold);

            var detail = Box(DbAtlas.ChamferTight, new Vector2(0f, -274f), new Vector2(556f, 72f), DbTheme.Alpha(DbTheme.Panel, .97f), "Detail panel");
            var detailLine = DbUi.Img(detail.transform, DbAtlas.ChamferTightLine, DbTheme.StrokeStrong, "Line");
            detailLine.Rt().Stretch();
            line1 = Text(new Vector2(0f, -253f), new Vector2(530f, 26f), "", 15f, DbTheme.Text, "Description", FontStyles.Bold);
            line2 = Text(new Vector2(0f, -276f), new Vector2(530f, 20f), "", 13f, DbTheme.Gold, "Cost", FontStyles.Normal);
            line3 = Text(new Vector2(0f, -297f), new Vector2(530f, 16f), "", 9.5f, DbTheme.TextMuted, "Page", FontStyles.Normal);
            Text(new Vector2(0f, -320f), new Vector2(590f, 16f), controls, 10f, DbTheme.TextSoft, "Controls", FontStyles.Normal);

            group.alpha = 0f;
            panel.localScale = Vector3.one * scale * .86f;
            DbMotion.Fade(group, 1f, .1f);
            DbMotion.Scale(panel, scale, .2f, DbEase.OutBack);
        }

        readonly Image headerTab;

        Image Box(Sprite sprite, Vector2 position, Vector2 size, Color color, string name)
        {
            var image = DbUi.Img(panel, sprite, color, name);
            image.Rt().anchoredPosition = position;
            image.Rt().sizeDelta = size;
            return image;
        }

        TextMeshProUGUI Text(Vector2 position, Vector2 size, string text, float fontSize, Color color, string name, FontStyles style)
        {
            return Configure(DbUi.Label(panel, text, DbTextStyle.Body, color, TextAlignmentOptions.Center, name), position, size, fontSize, style);
        }

        static TextMeshProUGUI Configure(TextMeshProUGUI label, Vector2 position, Vector2 size, float fontSize, FontStyles style)
        {
            label.Rt().anchoredPosition = position;
            label.Rt().sizeDelta = size;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.lineSpacing = 0f;
            // Long names shrink to fit, as these labels are not resized by the wheel.
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * .7f;
            label.fontSizeMax = fontSize;
            return label;
        }

        Slot BuildSlot(int index)
        {
            var slot = new Slot();
            slot.Root = DbUi.Node("Slot " + (index + 1), slotHost);
            slot.Root.sizeDelta = new Vector2(112f, 104f);
            slot.Icon = DbUi.Img(slot.Root, null, Color.white, "Icon");
            slot.Icon.preserveAspect = true;
            slot.Icon.Rt().anchoredPosition = new Vector2(0f, 12f);
            slot.Icon.Rt().sizeDelta = new Vector2(66f, 66f);
            slot.Frame = DbUi.Img(slot.Root, DbAtlas.RoundLine, DbTheme.StrokeStrong, "Frame");
            slot.Frame.Rt().anchoredPosition = new Vector2(0f, 12f);
            slot.Frame.Rt().sizeDelta = new Vector2(68f, 68f);
            slot.Star = DbUi.Img(slot.Root, DbAtlas.Star, DbTheme.Gold, "Current");
            slot.Star.Rt().anchoredPosition = new Vector2(32f, 42f);
            slot.Star.Rt().sizeDelta = new Vector2(18f, 18f);
            slot.Number = Configure(DbUi.Label(slot.Root, (index + 1).ToString(), DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.Center, "Slot"), new Vector2(-42f, 40f), new Vector2(16f, 15f), 10f, FontStyles.Bold);
            slot.Name = Configure(DbUi.Label(slot.Root, "", DbTextStyle.Small, DbTheme.Text, TextAlignmentOptions.Center, "Name"), new Vector2(0f, -38f), new Vector2(108f, 28f), 11f, FontStyles.Bold);
            slot.Name.enableWordWrapping = true;
            slot.Root.gameObject.SetActive(false);
            return slot;
        }

        public void BeginSlots(int visible)
        {
            count = Mathf.Clamp(visible, 0, SlotCount);
            ring.Set(count);
            wedge.Set(count);
            selectedSlot = -2;
            for (int i = 0; i < slots.Count; i++)
                slots[i].Root.gameObject.SetActive(i < count);
        }

        public void SetSlot(int index, Sprite icon, string name, Color tint, bool current)
        {
            var slot = slots[index];
            slot.Icon.sprite = icon;
            slot.Icon.enabled = icon;
            slot.Name.text = name;
            slot.Tint = tint;
            slot.Current = current;
            slot.Star.enabled = current;
            slot.Name.color = current ? DbTheme.Success : DbTheme.Text;
            slot.Frame.color = DbTheme.Alpha(tint, .5f);

            float angle = index * Mathf.PI * 2f / count;
            var home = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * SlotRadius;
            slot.Root.anchoredPosition = home * .45f;
            slot.Root.localScale = Vector3.one * .5f;
            float delay = index * .012f;
            DbMotion.Move(slot.Root, home, .22f, DbEase.OutBack, delay);
            DbMotion.Scale(slot.Root, 1f, .22f, DbEase.OutBack, delay);
        }

        public void SetSelection(int slot, Color accent)
        {
            DbMotion.Tint(glow, DbTheme.Alpha(accent, .12f), .16f);
            DbMotion.Tint(burst, DbTheme.Alpha(accent, .06f), .16f);
            DbMotion.Tint(outerRing, DbTheme.Alpha(accent, .55f), .16f);
            DbMotion.Tint(headerTab, accent, .16f);

            if (slot == selectedSlot) return;
            int previous = selectedSlot;
            selectedSlot = slot;
            for (int i = 0; i < count; i++)
            {
                var entry = slots[i];
                bool chosen = i == slot;
                if (i == previous || chosen)
                {
                    DbMotion.Scale(entry.Root, chosen ? 1.17f : 1f, .16f, DbEase.OutBack);
                    DbMotion.Tint(entry.Frame, chosen ? entry.Tint : DbTheme.Alpha(entry.Tint, .5f), .12f);
                    DbMotion.Tint(entry.Name, chosen ? DbTheme.Lift(entry.Tint, .25f) : entry.Current ? DbTheme.Success : DbTheme.Text, .12f);
                }
            }

            if (slot < 0 || count == 0)
            {
                wedgeShown = false;
                DbMotion.Tint(wedge, DbTheme.Alpha(accent, 0f), .1f);
                return;
            }
            float degrees = -slot * 360f / count;
            if (wedgeShown) DbMotion.Rotate(wedgeRt, degrees, .15f, DbEase.OutCubic);
            else
            {
                DbMotion.Stop(wedgeRt);
                wedgeRt.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
            wedgeShown = true;
            DbMotion.Tint(wedge, DbTheme.Alpha(accent, 1f), .12f);
        }

        public void SetPreview(Sprite icon, string name, string intentText, Color accent)
        {
            if (hubIcon.sprite != icon)
            {
                hubIcon.sprite = icon;
                hubIcon.enabled = icon;
                hubIconRt.localScale = Vector3.one * .84f;
                DbMotion.Scale(hubIconRt, 1f, .18f, DbEase.OutBack);
            }
            previewName.text = name;
            previewName.color = DbTheme.Lift(accent, .2f);
            intent.text = intentText;
        }

        public void SetDetail(string first, string second, string third)
        {
            line1.text = first;
            line2.text = second;
            line3.text = third;
        }

        public void SetResource(string text, float ratio)
        {
            resource.text = text;
            kiArc.fillAmount = Mathf.Clamp01(ratio);
        }

        // Fighter caption that follows the target for a moment after a choice is made.
        public static GameObject BuildToast(string name, string text, Color color, out RectTransform title)
        {
            DbAtlas.Ensure();
            var toast = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = toast.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31999;
            title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            title.SetParent(toast.transform, false);
            title.anchorMin = title.anchorMax = Vector2.zero;
            var back = DbUi.Img(title, DbAtlas.Slant, color, "Background");
            back.Rt().sizeDelta = new Vector2(300f, 44f);
            var label = DbUi.Label(title, text, DbTextStyle.Title, DbTheme.TextOnAccent, TextAlignmentOptions.Center, "Name");
            label.Rt().sizeDelta = new Vector2(250f, 38f);
            label.fontSize = 17f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 17f;
            title.localScale = Vector3.one * .82f;
            DbMotion.Scale(title, 1f, .2f, DbEase.OutBack);
            return toast;
        }
    }

    // Dark sectors under the slots; rebuilt only when the slot count changes.
    public sealed class CombatWheelRing : MaskableGraphic
    {
        int count;

        public void Set(int value)
        {
            if (count == value) return;
            count = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (count <= 0) return;
            int steps = Mathf.Clamp(112 / count, 12, 112);
            Color32 tint = new Color(.09f, .12f, .2f, .78f);
            for (int slot = 0; slot < count; slot++)
            {
                float start = (slot - .5f) * Mathf.PI * 2f / count + .014f;
                float end = (slot + .5f) * Mathf.PI * 2f / count - .014f;
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Lerp(start, end, i / (float)steps);
                    float b = Mathf.Lerp(start, end, (i + 1) / (float)steps);
                    int n = vh.currentVertCount;
                    vh.AddVert(new Vector3(Mathf.Sin(a) * 96f, Mathf.Cos(a) * 96f), tint, Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(a) * 240f, Mathf.Cos(a) * 240f), tint, Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(b) * 240f, Mathf.Cos(b) * 240f), tint, Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(b) * 96f, Mathf.Cos(b) * 96f), tint, Vector2.zero);
                    vh.AddTriangle(n, n + 1, n + 2);
                    vh.AddTriangle(n, n + 2, n + 3);
                }
            }
        }
    }

    // One sector centred on twelve o'clock; the view turns it instead of rebuilding it.
    public sealed class CombatWheelWedge : MaskableGraphic
    {
        int count;

        public void Set(int value)
        {
            if (count == value) return;
            count = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (count <= 0) return;
            int steps = Mathf.Clamp(128 / count, 16, 128);
            const float inner = 96f, outer = 240f, rim = 233f;
            float density = count <= 2 ? .4f : 1f;
            Color base32 = color;
            Color32 innerTint = DbTheme.Alpha(base32, base32.a * .02f * density);
            Color32 outerTint = DbTheme.Alpha(base32, base32.a * .14f * density);
            Color32 rimTint = DbTheme.Alpha(base32, base32.a);
            float half = Mathf.PI / count - .014f;
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)steps);
                float b = Mathf.Lerp(-half, half, (i + 1) / (float)steps);
                Band(vh, a, b, inner, rim, innerTint, outerTint);
                Band(vh, a, b, rim, outer, rimTint, rimTint);
            }
        }

        static void Band(VertexHelper vh, float a, float b, float from, float to, Color32 fromTint, Color32 toTint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(Mathf.Sin(a) * from, Mathf.Cos(a) * from), fromTint, Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Sin(a) * to, Mathf.Cos(a) * to), toTint, Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Sin(b) * to, Mathf.Cos(b) * to), toTint, Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Sin(b) * from, Mathf.Cos(b) * from), fromTint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
