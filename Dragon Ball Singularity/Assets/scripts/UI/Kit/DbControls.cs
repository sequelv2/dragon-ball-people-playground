using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    public enum DbButtonStyle { Primary, Surface, Ghost, Danger, Quiet }

    // Event-driven: no Update, so idle buttons cost nothing.
    public sealed class DbButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Image Back, Outline, Icon;
        public TextMeshProUGUI Label;
        public DbHit Hit;
        public DbButtonStyle Style;
        public Color Accent = DbTheme.Orange;
        public Action OnClick, OnDoubleClick, OnRightClick, OnHover, OnLeave;
        public float HoverScale = 1.025f, PressScale = .965f;
        public bool Silent;

        bool interactable = true, hovered, pressed, selected;
        float lastClick = -10f;

        public bool Interactable
        {
            get { return interactable; }
            set
            {
                if (interactable == value) return;
                interactable = value;
                if (!value) { hovered = pressed = false; }
                Refresh(.12f);
            }
        }

        public bool Selected
        {
            get { return selected; }
            set { if (selected == value) return; selected = value; Refresh(.14f); }
        }

        public void SetText(string text) { if (Label) Label.text = text; }

        public void SetAccent(Color accent, bool animate = true)
        {
            Accent = accent;
            Refresh(animate ? .22f : 0f);
        }

        public void ApplyInstant() { Refresh(0f); }

        void Colors(out Color back, out Color content, out Color line)
        {
            switch (Style)
            {
                case DbButtonStyle.Primary:
                    back = pressed ? DbTheme.Dim(Accent, .2f) : hovered ? DbTheme.Lift(Accent, .22f) : Accent;
                    content = DbTheme.TextOnAccent;
                    line = Color.clear;
                    break;
                case DbButtonStyle.Danger:
                    back = pressed ? DbTheme.Dim(DbTheme.Danger, .3f) : hovered ? DbTheme.Danger : DbTheme.Alpha(DbTheme.Danger, .05f);
                    content = hovered || pressed ? DbTheme.TextOnAccent : DbTheme.Danger;
                    line = DbTheme.Alpha(DbTheme.Danger, .55f);
                    break;
                case DbButtonStyle.Ghost:
                    back = pressed ? DbTheme.Alpha(Accent, .14f) : hovered ? DbTheme.Alpha(Accent, .07f) : selected ? DbTheme.Alpha(Accent, .1f) : Color.clear;
                    content = hovered || selected ? DbTheme.Lift(Accent, .3f) : DbTheme.TextSoft;
                    line = hovered || selected ? DbTheme.Alpha(Accent, .8f) : DbTheme.StrokeStrong;
                    break;
                case DbButtonStyle.Quiet:
                    back = pressed ? DbTheme.Alpha(Accent, .14f) : hovered ? DbTheme.Alpha(Accent, .06f) : Color.clear;
                    content = hovered ? DbTheme.Lift(Accent, .35f) : DbTheme.TextSoft;
                    line = Color.clear;
                    break;
                default:
                    back = pressed ? DbTheme.SurfacePress : hovered ? DbTheme.SurfaceHover : selected ? DbTheme.Mix(DbTheme.Surface, Accent, .22f) : DbTheme.Surface;
                    content = selected ? DbTheme.Lift(Accent, .35f) : DbTheme.Text;
                    line = hovered || selected ? DbTheme.Alpha(Accent, .7f) : DbTheme.Stroke;
                    break;
            }
            if (!interactable)
            {
                back = DbTheme.Alpha(back, back.a * .35f);
                content = DbTheme.Alpha(DbTheme.TextMuted, .75f);
                line = DbTheme.Alpha(line, line.a * .4f);
            }
        }

        void Refresh(float duration)
        {
            Colors(out var back, out var content, out var line);
            Apply(Back, back, duration);
            Apply(Outline, line, duration);
            Apply(Label, content, duration);
            Apply(Icon, content, duration);
        }

        static void Apply(Graphic graphic, Color color, float duration)
        {
            if (!graphic) return;
            if (duration <= 0f) { DbMotion.Stop(graphic); graphic.color = color; }
            else DbMotion.Tint(graphic, color, duration);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!interactable) return;
            hovered = true;
            Refresh(.09f);
            if (HoverScale != 1f) DbMotion.Scale(transform, HoverScale, .14f, DbEase.OutBack);
            OnHover?.Invoke();
        }

        public void OnPointerExit(PointerEventData e)
        {
            bool was = hovered;
            hovered = pressed = false;
            if (!interactable) return;
            Refresh(.16f);
            if (HoverScale != 1f) DbMotion.Scale(transform, 1f, .16f);
            if (was) OnLeave?.Invoke();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable || e.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            Refresh(.05f);
            DbMotion.Scale(transform, PressScale, .07f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!pressed) return;
            pressed = false;
            Refresh(.12f);
            DbMotion.Scale(transform, hovered ? HoverScale : 1f, .18f, DbEase.OutBack);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || !e.eligibleForClick) return;
            if (e.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke();
                return;
            }
            if (e.button != PointerEventData.InputButton.Left) return;
            float now = Time.unscaledTime;
            if (OnDoubleClick != null && now - lastClick <= .3f)
            {
                lastClick = -10f;
                if (!Silent) DbUi.Blip();
                OnDoubleClick();
                return;
            }
            lastClick = now;
            if (!Silent) DbUi.Blip();
            OnClick?.Invoke();
        }

        void OnDisable() { hovered = pressed = false; }
    }

    public sealed class DbSwitch : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        const float Width = 54f, Height = 28f, Knob = 20f;
        Func<bool> get;
        Action<bool> set;
        Image back, knob, glow;
        Color accent;
        bool state, hovered;

        public void Build(Func<bool> getter, Action<bool> setter, Color accentColor)
        {
            get = getter; set = setter; accent = accentColor;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width, Height);
            glow = DbUi.Img(transform, DbAtlas.Glow, DbTheme.Alpha(accent, 0f), "Glow");
            glow.Rt().Centre(0f, 0f, Width + 42f, Height + 42f);
            back = DbUi.Img(transform, DbAtlas.Pill, DbTheme.Well, "Track");
            DbUi.PillFit(back, Height);
            back.Rt().Centre(0f, 0f, Width, Height);
            knob = DbUi.Img(transform, DbAtlas.Disc, DbTheme.TextMuted, "Knob");
            knob.Rt().Centre(0f, 0f, Knob, Knob);
            gameObject.AddComponent<DbHit>();
            state = get != null && get();
            Show(0f);
        }

        public void SetAccent(Color value) { accent = value; Show(.2f); }

        public void Refresh()
        {
            bool now = get != null && get();
            if (now == state) return;
            state = now;
            Show(.2f);
        }

        void Show(float duration)
        {
            float x = (state ? 1f : -1f) * (Width - Knob - 8f) * .5f;
            Color trackColor = state ? accent : hovered ? DbTheme.SurfaceHover : DbTheme.Surface;
            Color knobColor = state ? DbTheme.TextOnAccent : DbTheme.TextSoft;
            Color glowColor = DbTheme.Alpha(accent, state ? .1f : 0f);
            if (duration <= 0f)
            {
                knob.Rt().anchoredPosition = new Vector2(x, 0f);
                back.color = trackColor; knob.color = knobColor; glow.color = glowColor;
                return;
            }
            DbMotion.Move(knob.Rt(), new Vector2(x, 0f), duration, DbEase.OutBack);
            DbMotion.Tint(back, trackColor, duration);
            DbMotion.Tint(knob, knobColor, duration);
            DbMotion.Tint(glow, glowColor, duration);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !e.eligibleForClick) return;
            set?.Invoke(!state);
            state = get != null ? get() : !state;
            DbUi.Blip();
            Show(.2f);
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; Show(.12f); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Show(.16f); }
    }

    public sealed class DbSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IInitializePotentialDragHandler
    {
        Func<float> get;
        Action<float> set;
        float min, max;
        bool logarithmic, whole, dragging, hovered;
        Image track, fill, thumb, halo;
        RectTransform rt;
        Color accent;
        float shown = -1f;

        public Action OnCommit;
        public Action<float> OnDragged;

        public void Build(float minValue, float maxValue, Func<float> getter, Action<float> setter, Color accentColor, bool log, bool wholeNumbers)
        {
            min = minValue; max = maxValue; get = getter; set = setter; accent = accentColor;
            logarithmic = log && minValue > 0f; whole = wholeNumbers;
            rt = (RectTransform)transform;
            track = DbUi.Img(transform, DbAtlas.Pill, DbTheme.SurfaceHover, "Track");
            DbUi.PillFit(track, 6f);
            StretchBar(track.Rt(), 3f);
            fill = DbUi.Img(transform, DbAtlas.Pill, accent, "Fill");
            DbUi.PillFit(fill, 6f);
            StretchBar(fill.Rt(), 3f);
            fill.Rt().anchorMax = new Vector2(0f, .5f);
            halo = DbUi.Img(transform, DbAtlas.Glow, DbTheme.Alpha(accent, .0f), "Halo");
            halo.Rt().Centre(0f, 0f, 44f, 44f);
            thumb = DbUi.Img(transform, DbAtlas.Disc, DbTheme.Text, "Thumb");
            thumb.Rt().Centre(0f, 0f, 16f, 16f);
            thumb.Rt().anchorMin = thumb.Rt().anchorMax = new Vector2(0f, .5f);
            halo.Rt().anchorMin = halo.Rt().anchorMax = new Vector2(0f, .5f);
            gameObject.AddComponent<DbHit>();
            Refresh(true);
        }

        static void StretchBar(RectTransform bar, float halfHeight)
        {
            bar.anchorMin = new Vector2(0f, .5f);
            bar.anchorMax = new Vector2(1f, .5f);
            bar.pivot = new Vector2(0f, .5f);
            bar.offsetMin = new Vector2(0f, -halfHeight);
            bar.offsetMax = new Vector2(0f, halfHeight);
        }

        public void SetAccent(Color value)
        {
            accent = value;
            DbMotion.Tint(fill, accent, .22f);
        }

        float ToPosition(float value)
        {
            if (max <= min) return 0f;
            if (logarithmic)
                return Mathf.Clamp01(Mathf.InverseLerp(Mathf.Log(min), Mathf.Log(max), Mathf.Log(Mathf.Max(value, min))));
            return Mathf.Clamp01((value - min) / (max - min));
        }

        float ToValue(float position)
        {
            float value = logarithmic ? Mathf.Exp(Mathf.Lerp(Mathf.Log(min), Mathf.Log(max), position)) : Mathf.Lerp(min, max, position);
            return whole ? Mathf.Round(value) : value;
        }

        public void Refresh(bool instant = false)
        {
            if (dragging || get == null) return;
            float position = ToPosition(get());
            if (Mathf.Abs(position - shown) < .0005f) return;
            if (instant || shown < 0f) { Show(position); return; }
            DbMotion.Value(this, shown, position, Show, .22f);
        }

        void Show(float position)
        {
            shown = position;
            float width = rt.rect.width;
            float x = position * width;
            var thumbRt = thumb.Rt();
            thumbRt.anchoredPosition = new Vector2(x, 0f);
            halo.Rt().anchoredPosition = new Vector2(x, 0f);
            fill.Rt().sizeDelta = new Vector2(Mathf.Max(0f, x), fill.Rt().sizeDelta.y);
        }

        void OnRectTransformDimensionsChange()
        {
            if (rt && shown >= 0f) Show(shown);
        }

        void Apply(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local);
            var rect = rt.rect;
            float position = Mathf.Clamp01((local.x - rect.xMin) / Mathf.Max(1f, rect.width));
            float value = ToValue(position);
            set?.Invoke(value);
            DbMotion.Stop(this);
            Show(get != null ? ToPosition(get()) : position);
            OnDragged?.Invoke(value);
        }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            DbMotion.Scale(thumb.transform, 1.3f, .12f, DbEase.OutBack);
            DbMotion.Tint(halo, DbTheme.Alpha(accent, .22f), .12f);
            Apply(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (dragging) Apply(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            DbMotion.Scale(thumb.transform, hovered ? 1.15f : 1f, .16f, DbEase.OutBack);
            DbMotion.Tint(halo, DbTheme.Alpha(accent, hovered ? .1f : 0f), .18f);
            OnCommit?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (dragging) return;
            DbMotion.Scale(thumb.transform, 1.15f, .12f);
            DbMotion.Tint(halo, DbTheme.Alpha(accent, .1f), .12f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            if (dragging) return;
            DbMotion.Scale(thumb.transform, 1f, .16f);
            DbMotion.Tint(halo, DbTheme.Alpha(accent, 0f), .16f);
        }
    }

    public sealed class DbField : MonoBehaviour
    {
        public TMP_InputField Input;
        public Action<string> OnChanged, OnSubmit;
        public Action OnFocusLost;
        Image back, line;
        Color accent;
        Global blocker;

        public string Text
        {
            get { return Input.text; }
            set { Input.SetTextWithoutNotify(value ?? ""); }
        }

        public bool Focused { get { return Input && Input.isFocused; } }

        public void Build(string value, string placeholder, Color accentColor, bool numeric, bool withIcon)
        {
            accent = accentColor;
            back = DbUi.Img(transform, DbAtlas.ChamferTight, DbTheme.Well, "Back", true);
            back.Rt().Stretch();
            line = DbUi.Img(transform, DbAtlas.ChamferTightLine, DbTheme.StrokeStrong, "Line");
            line.Rt().Stretch();
            float inset = 10f;
            if (withIcon)
            {
                var icon = DbUi.Img(transform, DbAtlas.Search, DbTheme.TextMuted, "SearchIcon");
                var iconRt = icon.Rt();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, .5f);
                iconRt.pivot = new Vector2(0f, .5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(20f, 20f);
                inset = 36f;
            }
            var area = DbUi.Node("TextArea", transform);
            area.Stretch(inset, 2f, 8f, 2f);
            area.gameObject.AddComponent<RectMask2D>();

            var holder = DbUi.Label(area, placeholder, DbTextStyle.Body, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Placeholder");
            holder.Rt().Stretch();
            holder.fontStyle = FontStyles.Italic;
            var text = DbUi.Label(area, "", DbTextStyle.Body, DbTheme.Text, TextAlignmentOptions.MidlineLeft, "Value");
            text.Rt().Stretch();

            Input = gameObject.AddComponent<TMP_InputField>();
            Input.targetGraphic = back;
            Input.transition = Selectable.Transition.None;
            Input.textViewport = area;
            Input.textComponent = text;
            Input.placeholder = holder;
            Input.fontAsset = DbTheme.Font;
            Input.lineType = TMP_InputField.LineType.SingleLine;
            Input.contentType = numeric ? TMP_InputField.ContentType.DecimalNumber : TMP_InputField.ContentType.Standard;
            Input.customCaretColor = true;
            Input.caretColor = accent;
            Input.caretWidth = 2;
            Input.selectionColor = DbTheme.Alpha(accent, .4f);
            Input.navigation = new Navigation { mode = Navigation.Mode.None };
            Input.SetTextWithoutNotify(value ?? "");
            Input.onValueChanged.AddListener(v => OnChanged?.Invoke(v));
            Input.onSubmit.AddListener(v => OnSubmit?.Invoke(v));
            Input.onSelect.AddListener(_ => Focus(true));
            Input.onDeselect.AddListener(_ => { Focus(false); OnFocusLost?.Invoke(); });
        }

        void Focus(bool focused)
        {
            DbMotion.Tint(line, focused ? accent : DbTheme.StrokeStrong, .12f);
            DbMotion.Tint(back, focused ? DbTheme.Mix(DbTheme.Well, accent, .12f) : DbTheme.Well, .12f);
            // Typing must not reach the game's hotkeys.
            if (focused) { if (!blocker && Global.main) { blocker = Global.main; blocker.AddUiBlocker(); } }
            else ReleaseBlocker();
        }

        void ReleaseBlocker()
        {
            DbUi.Release(blocker);
            blocker = null;
        }

        public void SetAccent(Color value)
        {
            accent = value;
            if (Input) { Input.caretColor = value; Input.selectionColor = DbTheme.Alpha(value, .4f); }
        }

        public void Activate()
        {
            if (!Input) return;
            Input.Select();
            Input.ActivateInputField();
        }

        void OnDisable() { ReleaseBlocker(); }
        void OnDestroy() { ReleaseBlocker(); }
    }

    // Inertial vertical scroll. Sleeps when settled; uses clipping instead of stencil masks.
    public sealed class DbScroll : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        public RectTransform Viewport, Content;
        public Action<float> OnScrolled;
        public float WheelStep = 90f;

        float height, offset, target, velocity, grabOffset;
        bool dragging;
        Image thumb;
        RectTransform thumbRt;
        DbHit surface;

        public float Offset { get { return offset; } }
        public float ContentHeight { get { return height; } }
        public float MaxOffset { get { return Mathf.Max(0f, height - Viewport.rect.height); } }

        public void Build(Color accent)
        {
            var rt = (RectTransform)transform;
            Viewport = DbUi.Node("Viewport", rt);
            Viewport.Stretch();
            Viewport.gameObject.AddComponent<RectMask2D>();
            surface = Viewport.gameObject.AddComponent<DbHit>();
            Content = DbUi.Node("Content", Viewport);
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = new Vector2(1f, 1f);
            Content.pivot = new Vector2(.5f, 1f);
            Content.anchoredPosition = Vector2.zero;
            Content.sizeDelta = Vector2.zero;
            thumb = DbUi.Img(rt, DbAtlas.Pill, DbTheme.Alpha(accent, .55f), "Thumb", true);
            DbUi.PillFit(thumb, 5f);
            thumbRt = thumb.Rt();
            thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(1f, 1f);
            thumbRt.pivot = new Vector2(1f, 1f);
            thumb.gameObject.AddComponent<DbScrollThumb>().Owner = this;
            enabled = false;
            UpdateThumb();
        }

        public void SetAccent(Color accent) { if (thumb) thumb.color = DbTheme.Alpha(accent, .55f); }

        public void SetContentHeight(float value, bool keepOffset = true)
        {
            height = value;
            Content.sizeDelta = new Vector2(0f, value);
            if (!keepOffset) { offset = target = 0f; Content.anchoredPosition = Vector2.zero; OnScrolled?.Invoke(0f); }
            target = Mathf.Clamp(target, 0f, MaxOffset);
            if (Mathf.Abs(offset - target) > .01f) enabled = true;
            UpdateThumb();
        }

        public void ScrollTo(float value, bool instant = false)
        {
            target = Mathf.Clamp(value, 0f, MaxOffset);
            if (instant) { velocity = 0f; SetOffset(target); enabled = false; return; }
            enabled = true;
        }

        public void Reveal(float top, float bottom)
        {
            float view = Viewport.rect.height;
            if (top < target) ScrollTo(top - 6f);
            else if (bottom > target + view) ScrollTo(bottom - view + 6f);
        }

        void SetOffset(float value)
        {
            offset = value;
            Content.anchoredPosition = new Vector2(0f, offset);
            UpdateThumb();
            OnScrolled?.Invoke(offset);
        }

        void UpdateThumb()
        {
            if (!thumbRt) return;
            float view = Viewport.rect.height;
            bool show = height > view + 1f && view > 1f;
            if (thumb.enabled != show) thumb.enabled = show;
            if (!show) return;
            float h = Mathf.Max(32f, view * view / height);
            float t = MaxOffset > 0f ? offset / MaxOffset : 0f;
            thumbRt.sizeDelta = new Vector2(5f, h);
            thumbRt.anchoredPosition = new Vector2(-1f, -(view - h) * t);
        }

        internal void DragThumb(float deltaY)
        {
            float view = Viewport.rect.height;
            float h = Mathf.Max(32f, view * view / Mathf.Max(1f, height));
            float travel = Mathf.Max(1f, view - h);
            ScrollTo(target - deltaY / travel * MaxOffset, true);
        }

        public void OnScroll(PointerEventData e)
        {
            target = Mathf.Clamp(target - e.scrollDelta.y * WheelStep, 0f, MaxOffset);
            enabled = true;
        }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = true; velocity = 0f; }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            grabOffset = offset;
            enabled = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, e.position, e.pressEventCamera, out var now);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, e.pressPosition, e.pressEventCamera, out var start);
            target = Mathf.Clamp(grabOffset + (now.y - start.y), 0f, MaxOffset);
        }

        public void OnEndDrag(PointerEventData e) { dragging = false; }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float next = Mathf.SmoothDamp(offset, target, ref velocity, dragging ? .03f : .09f, Mathf.Infinity, dt);
            if (!dragging && Mathf.Abs(next - target) < .25f)
            {
                next = target;
                velocity = 0f;
                enabled = false;
            }
            if (next != offset) SetOffset(next);
        }

        void OnRectTransformDimensionsChange() { if (thumbRt) UpdateThumb(); }
    }

    public sealed class DbScrollThumb : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public DbScroll Owner;
        Vector2 last;

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Owner.Viewport, e.position, e.pressEventCamera, out last);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Owner.Viewport, e.position, e.pressEventCamera, out var now);
            Owner.DragThumb(now.y - last.y);
            last = now;
        }
    }

    // Grid/list that creates only as many cells as fit on screen and rebinds them while scrolling,
    // so a list of hundreds of entries costs the same as a list of twenty.
    public sealed class DbRecycler
    {
        readonly DbScroll scroll;
        readonly Func<RectTransform, int, RectTransform> create;
        readonly Action<RectTransform, int> bind;
        readonly List<RectTransform> pool = new List<RectTransform>();
        readonly List<int> bound = new List<int>();
        int count, columns;
        float cellW, cellH, gapX, gapY, padX, padY;

        public int Count { get { return count; } }

        public DbRecycler(DbScroll scroll, int columns, float cellW, float cellH, float gapX, float gapY, Func<RectTransform, int, RectTransform> create, Action<RectTransform, int> bind, float padX = 0f, float padY = 0f)
        {
            this.scroll = scroll;
            this.columns = Mathf.Max(1, columns);
            this.cellW = cellW; this.cellH = cellH; this.gapX = gapX; this.gapY = gapY;
            this.padX = padX; this.padY = padY;
            this.create = create; this.bind = bind;
            scroll.OnScrolled += _ => Sync();
        }

        public Vector2 CellPosition(int index)
        {
            int row = index / columns, col = index % columns;
            return new Vector2(padX + col * (cellW + gapX), padY + row * (cellH + gapY));
        }

        public void SetCount(int value, bool resetScroll = true)
        {
            count = value;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float height = rows == 0 ? 0f : padY * 2f + rows * cellH + (rows - 1) * gapY;
            scroll.SetContentHeight(height, !resetScroll);
            Ensure();
            for (int i = 0; i < bound.Count; i++) bound[i] = -1;
            Sync();
        }

        public void Rebind()
        {
            for (int i = 0; i < pool.Count; i++)
                if (bound[i] >= 0) bind(pool[i], bound[i]);
        }

        public RectTransform CellFor(int index)
        {
            for (int i = 0; i < bound.Count; i++)
                if (bound[i] == index) return pool[i];
            return null;
        }

        public void Reveal(int index)
        {
            var p = CellPosition(index);
            scroll.Reveal(p.y, p.y + cellH);
        }

        // Fires for the cells currently on screen; used for the staggered entrance.
        public void ForEachVisible(Action<RectTransform, int> visit)
        {
            for (int i = 0; i < pool.Count; i++)
                if (bound[i] >= 0) visit(pool[i], bound[i]);
        }

        void Ensure()
        {
            float view = Mathf.Max(scroll.Viewport.rect.height, cellH);
            int rows = Mathf.CeilToInt(view / (cellH + gapY)) + 2;
            int need = Mathf.Min(rows * columns, Mathf.Max(count, columns));
            while (pool.Count < need)
            {
                var cell = create(scroll.Content, pool.Count);
                cell.anchorMin = cell.anchorMax = new Vector2(0f, 1f);
                cell.pivot = new Vector2(.5f, .5f);
                cell.sizeDelta = new Vector2(cellW, cellH);
                pool.Add(cell);
                bound.Add(-1);
            }
        }

        void Sync()
        {
            if (pool.Count == 0) return;
            float offset = scroll.Offset;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt((offset - padY) / (cellH + gapY)));
            int first = firstRow * columns;
            int size = pool.Count;
            // Slot = index modulo pool size, so scrolling touches only the rows that changed.
            for (int slot = 0; slot < size; slot++)
            {
                int index = first + ((slot - first) % size + size) % size;
                var cell = pool[slot];
                if (index >= count)
                {
                    if (cell.gameObject.activeSelf) cell.gameObject.SetActive(false);
                    bound[slot] = -1;
                    continue;
                }
                if (bound[slot] == index) continue;
                bound[slot] = index;
                if (!cell.gameObject.activeSelf) cell.gameObject.SetActive(true);
                var p = CellPosition(index);
                cell.anchoredPosition = new Vector2(p.x + cellW * .5f, -(p.y + cellH * .5f));
                bind(cell, index);
            }
        }
    }

// Row of small selectable tags with a single highlight that glides between them.
    public sealed class DbChips
    {
        readonly List<DbButton> chips = new List<DbButton>();
        readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        readonly Image glider;
        Color accent;
        public int Selected { get; private set; }
        public Action<int> OnChanged;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public RectTransform Root { get; private set; }

        public DbChips(Transform parent, float x, float y, float height, string[] names, Color accentColor, float maxWidth = 0f)
        {
            accent = accentColor;
            Root = DbUi.Node("Chips", parent);
            var measure = DbUi.Label(Root, "", DbTextStyle.Caps, Color.clear);
            glider = DbUi.Img(Root, DbAtlas.Pill, accent, "Glider");
            DbUi.PillFit(glider, height);
            float cursor = 0f, rowTop = 0f, widest = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                float width = Mathf.Max(52f, DbUi.PreferredWidth(measure, names[i]) + 28f);
                if (maxWidth > 0f && cursor > 0f && cursor + width > maxWidth)
                {
                    widest = Mathf.Max(widest, cursor);
                    cursor = 0f;
                    rowTop += height + 6f;
                }
                int index = i;
                var rt = DbUi.Node("Chip " + names[i], Root);
                rt.Pin(cursor, rowTop, width, height);
                var button = rt.gameObject.AddComponent<DbButton>();
                button.Hit = rt.gameObject.AddComponent<DbHit>();
                button.Back = DbUi.Img(rt, DbAtlas.Pill, Color.clear, "Back");
                DbUi.PillFit(button.Back, height);
                button.Back.Rt().Stretch();
                var label = DbUi.Label(rt, names[i], DbTextStyle.Caps, DbTheme.TextSoft, TextAlignmentOptions.Center, "Label");
                label.Rt().Stretch();
                labels.Add(label);
                button.Style = DbButtonStyle.Quiet;
                button.Accent = accent;
                button.HoverScale = 1.04f;
                button.OnClick = () => Select(index);
                button.ApplyInstant();
                chips.Add(button);
                cursor += width + 6f;
            }
            Width = Mathf.Max(widest, cursor);
            Height = rowTop + height;
            Root.Pin(x, y, Width, Height);
            UnityEngine.Object.Destroy(measure.gameObject);
            MoveGlider(0, true);
        }

        public void SetAccent(Color value)
        {
            accent = value;
            DbMotion.Tint(glider, value, .22f);
            for (int i = 0; i < chips.Count; i++) chips[i].Accent = value;
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= chips.Count) return;
            Selected = index;
            MoveGlider(index, false);
            if (notify) OnChanged?.Invoke(index);
        }

        void MoveGlider(int index, bool instant)
        {
            if (chips.Count == 0) { glider.enabled = false; return; }
            var target = chips[index].Rt();
            var g = glider.Rt();
            if (instant)
            {
                g.anchorMin = g.anchorMax = new Vector2(0f, 1f);
                g.pivot = new Vector2(0f, 1f);
                g.anchoredPosition = target.anchoredPosition;
                g.sizeDelta = target.sizeDelta;
            }
            else
            {
                DbMotion.Move(g, target.anchoredPosition, .22f, DbEase.OutCubic);
                DbMotion.Size(g, target.sizeDelta, .22f, DbEase.OutCubic);
            }
            for (int i = 0; i < labels.Count; i++)
            {
                Color color = i == Selected ? DbTheme.TextOnAccent : DbTheme.TextSoft;
                if (instant) labels[i].color = color;
                else DbMotion.Tint(labels[i], color, .15f);
            }
        }
    }

    public static class DbFormat
    {
        public static string Number(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs >= 1e12f) return (value / 1e12f).ToString("0.##", CultureInfo.InvariantCulture) + "T";
            if (abs >= 1e9f) return (value / 1e9f).ToString("0.##", CultureInfo.InvariantCulture) + "B";
            if (abs >= 1e6f) return (value / 1e6f).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            if (abs >= 1e5f) return (value / 1e3f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            if (abs >= 100f) return Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Percent(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static bool TryParse(string text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
