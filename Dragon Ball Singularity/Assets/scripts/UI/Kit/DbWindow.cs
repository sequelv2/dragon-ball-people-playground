using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    // Layer that holds every Singularity window. It is a child canvas of the game's UI, so it uses
    // the game's scaling and order, and its redraws never make the game's own canvas rebuild.
    [DefaultExecutionOrder(-1000)]
    public sealed class DbUiRoot : MonoBehaviour
    {
        static DbUiRoot instance;
        readonly List<DbWindow> stack = new List<DbWindow>();
        Canvas canvas;
        GraphicRaycaster raycaster;
        RectTransform rect;
        Global escapeBlocker;

        public static DbUiRoot Instance
        {
            get
            {
                if (instance) return instance;
                var parent = DbUi.GameCanvas();
                if (!parent) return null;
                var rt = DbUi.Node("DBS UI", parent.transform);
                rt.Stretch();
                instance = rt.gameObject.AddComponent<DbUiRoot>();
                instance.rect = rt;
                instance.canvas = rt.gameObject.AddComponent<Canvas>();
                instance.raycaster = rt.gameObject.AddComponent<GraphicRaycaster>();
                return instance;
            }
        }

        // Never creates the layer: safe from OnDestroy during scene teardown.
        public static DbUiRoot Current { get { return instance; } }
        public static bool AnyWindowOpen { get { return instance && instance.stack.Count > 0; } }

        public RectTransform Rect { get { return rect; } }

        // Fits windows authored at 1080p to whatever size the game's canvas has.
        public float DesignScale
        {
            get
            {
                var size = rect.rect.size;
                return Mathf.Max(.25f, Mathf.Min(size.x / DbUi.DesignWidth, size.y / DbUi.DesignHeight));
            }
        }

        internal void Register(DbWindow window)
        {
            stack.Remove(window);
            stack.Add(window);
            transform.SetAsLastSibling();
        }

        internal void Unregister(DbWindow window) { stack.Remove(window); }

        internal void Raise(DbWindow window)
        {
            if (stack.Count > 0 && stack[stack.Count - 1] == window) return;
            stack.Remove(window);
            stack.Add(window);
            window.transform.SetAsLastSibling();
        }

        void Update()
        {
            var main = Global.main;
            if (!main) return;
            bool paused = main.GetPausedMenu();
            if (canvas.enabled == paused) { canvas.enabled = !paused; raycaster.enabled = !paused; }
            if (paused || stack.Count == 0 || DialogBox.IsAnyDialogboxOpen) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            var system = EventSystem.current;
            var focused = system ? system.currentSelectedGameObject : null;
            if (focused && focused.GetComponent<TMP_InputField>())
            {
                system.SetSelectedGameObject(null);
            }
            else
            {
                stack[stack.Count - 1].Close();
            }
            // This key closed a menu: the game must not also open its pause menu this frame.
            if (!escapeBlocker) { escapeBlocker = main; escapeBlocker.AddUiBlocker(); }
        }

        void LateUpdate()
        {
            if (!escapeBlocker) return;
            DbUi.Release(escapeBlocker);
            escapeBlocker = null;
        }

        void OnDestroy()
        {
            DbUi.Release(escapeBlocker);
            if (instance == this) instance = null;
        }
    }

    // Standard frame: slanted title tab, drifting speed lines, drag, resize, remembered placement
    // and animated open/close. Content goes into Body.
    public sealed class DbWindow : MonoBehaviour, IPointerDownHandler
    {
        public RectTransform Panel, Body;
        public CanvasGroup Group;
        public TextMeshProUGUI Title, Subtitle, Hint;
        public Action OnClosed;
        public Color Accent { get; private set; }

        readonly List<AccentBinding> accents = new List<AccentBinding>();
        struct AccentBinding { public Graphic Graphic; public float Alpha; public float Lift; }

        string id;
        Vector2 size;
        Image backdrop, tab;
        RectTransform streaks, tabRt, subtitleRt;
        UnityEngine.Object lifetime;
        bool watchesLifetime, closing, modal;
        Global blocker;
        float userScale = 1f, lifetimeCheck;
        Action tick;
        float tickEvery, tickAt;
        DbButton closeButton;

        public bool IsClosing { get { return closing; } }

        public static DbWindow Create(string id, float width, float height, string title, string subtitle, Color accent, bool modal = false)
        {
            DbAtlas.Ensure();
            var root = DbUiRoot.Instance;
            if (!root) return null;

            var rt = DbUi.Node("Window " + id, root.transform);
            rt.Stretch();
            var window = rt.gameObject.AddComponent<DbWindow>();
            // Own canvas per window: animating one window never re-batches another.
            rt.gameObject.AddComponent<Canvas>();
            rt.gameObject.AddComponent<GraphicRaycaster>();
            window.id = id;
            window.size = new Vector2(width, height);
            window.modal = modal;
            window.Accent = accent;
            window.Build(title, subtitle);
            root.Register(window);
            window.AnimateIn();
            if (modal && Global.main)
            {
                window.blocker = Global.main;
                window.blocker.AddUiBlocker();
            }
            return window;
        }

        void Build(string title, string subtitle)
        {
            if (modal)
            {
                backdrop = DbUi.Img(transform, null, new Color(.01f, .015f, .03f, 0f), "Backdrop", true);
                backdrop.Rt().Stretch();
            }

            Panel = DbUi.Node("Panel", transform);
            Panel.Centre(0f, 0f, size.x, size.y);
            Group = Panel.gameObject.AddComponent<CanvasGroup>();

            var shadow = DbUi.Img(Panel, DbAtlas.EdgeGlow, new Color(0f, 0f, 0f, .72f), "Shadow");
            shadow.Rt().Stretch(-34f, -28f, -34f, -40f);

            var back = DbUi.Img(Panel, DbAtlas.Chamfer, DbTheme.Alpha(DbTheme.Panel, .985f), "Back", true);
            back.Rt().Stretch();

            var wash = DbUi.Img(Panel, DbAtlas.GradUp, DbTheme.Alpha(Accent, .04f), "AccentWash");
            wash.Rt().anchorMin = new Vector2(0f, 1f);
            wash.Rt().anchorMax = new Vector2(1f, 1f);
            wash.Rt().pivot = new Vector2(.5f, 1f);
            wash.Rt().offsetMin = new Vector2(0f, -170f);
            wash.Rt().offsetMax = new Vector2(-22f, 0f);
            BindAccent(wash, .04f);

            var mark = DbUi.Img(Panel, DbAtlas.Star, DbTheme.Alpha(DbTheme.Text, .0035f), "StarMark");
            mark.Rt().anchorMin = mark.Rt().anchorMax = new Vector2(1f, 0f);
            mark.Rt().pivot = new Vector2(.5f, .5f);
            mark.Rt().anchoredPosition = new Vector2(-165f, 165f);
            mark.Rt().sizeDelta = new Vector2(280f, 280f);
            mark.Rt().localRotation = Quaternion.Euler(0f, 0f, 14f);

            var line = DbUi.Img(Panel, DbAtlas.ChamferLine, DbTheme.StrokeStrong, "Outline");
            line.Rt().Stretch();

            // Speed lines live in their own canvas so their motion stays a transform-only update.
            var header = DbUi.Node("Header", Panel);
            header.Pin(0f, 0f, size.x, 66f);
            header.gameObject.AddComponent<RectMask2D>();
            header.gameObject.AddComponent<DbHit>();
            header.gameObject.AddComponent<DbWindowDrag>().Window = this;
            // Stops short of the cut corner so the lines never poke past the window's edge.
            var fxClip = DbUi.Node("HeaderClip", header);
            fxClip.Stretch(0f, 3f, 26f, 0f);
            fxClip.gameObject.AddComponent<RectMask2D>();
            var fx = DbUi.Node("HeaderFx", fxClip);
            fx.Stretch();
            fx.gameObject.AddComponent<Canvas>();
            var streakImage = DbUi.Img(fx, DbAtlas.Streaks, DbTheme.Alpha(Accent, .11f), "Streaks");
            streakImage.type = Image.Type.Tiled;
            streaks = streakImage.Rt();
            streaks.anchorMin = streaks.anchorMax = new Vector2(0f, .5f);
            streaks.pivot = new Vector2(0f, .5f);
            streaks.sizeDelta = new Vector2(size.x + 512f, 64f);
            streaks.anchoredPosition = new Vector2(-256f, 0f);
            BindAccent(streakImage, .11f);

            tab = DbUi.Img(Panel, DbAtlas.Slant, Accent, "TitleTab");
            tabRt = tab.Rt();
            BindAccent(tab, 1f);
            Title = DbUi.Label(tab.transform, title, DbTextStyle.Title, DbTheme.TextOnAccent, TextAlignmentOptions.MidlineLeft, "Title");
            Subtitle = DbUi.Label(Panel, subtitle, DbTextStyle.Caps, DbTheme.TextSoft, TextAlignmentOptions.MidlineLeft, "Subtitle");
            subtitleRt = Subtitle.Rt();
            LayoutTitle();

            var hair = DbUi.Img(Panel, DbAtlas.Hairline, DbTheme.Alpha(Accent, .75f), "HeaderLine");
            hair.Rt().Pin(0f, 66f, size.x - 22f, 2f);
            BindAccent(hair, .75f);

            closeButton = DbUi.IconButton(Panel, DbAtlas.Close, Close, DbTheme.TextSoft, DbTheme.Danger, "Close");
            closeButton.Rt().PinRight(30f, 16f, 36f, 36f);
            closeButton.HoverScale = 1.1f;

            Body = DbUi.Node("Body", Panel);
            Body.Stretch(24f, 80f, 24f, 18f);

            var grip = DbUi.Img(Panel, DbAtlas.Grip, DbTheme.TextMuted, "ResizeGrip", true);
            grip.Rt().anchorMin = grip.Rt().anchorMax = grip.Rt().pivot = new Vector2(1f, 0f);
            grip.Rt().anchoredPosition = new Vector2(-3f, 3f);
            grip.Rt().sizeDelta = new Vector2(24f, 24f);
            grip.gameObject.AddComponent<DbWindowGrip>().Window = this;

            LoadPlacement();
        }

        void LayoutTitle()
        {
            float titleWidth = DbUi.PreferredWidth(Title) + 8f;
            float tabWidth = titleWidth + 72f;
            tabRt.Pin(-14f, 13f, tabWidth, 44f);
            Title.Rt().Stretch(40f, 0f, 22f, 0f);
            subtitleRt.Pin(tabWidth + 8f, 13f, Mathf.Max(60f, size.x - tabWidth - 90f), 44f);
        }

        public void SetTitle(string title, string subtitle = null)
        {
            Title.text = title;
            if (subtitle != null) Subtitle.text = subtitle;
            LayoutTitle();
        }

        public void SetSubtitle(string subtitle) { if (Subtitle.text != subtitle) Subtitle.text = subtitle; }

        public void SetHint(string text)
        {
            if (!Hint)
            {
                Hint = DbUi.Label(Panel, text, DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Hint");
                var rt = Hint.Rt();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(.5f, 0f);
                rt.offsetMin = new Vector2(44f, 8f);
                rt.offsetMax = new Vector2(-40f, 32f);
                Body.offsetMin = new Vector2(Body.offsetMin.x, 38f);
            }
            else Hint.text = text;
        }

        // Graphics that follow the window's accent colour when it changes.
        public void BindAccent(Graphic graphic, float alpha = 1f, float lift = 0f)
        {
            if (!graphic) return;
            accents.Add(new AccentBinding { Graphic = graphic, Alpha = alpha, Lift = lift });
            graphic.color = DbTheme.Alpha(lift > 0f ? DbTheme.Lift(Accent, lift) : Accent, alpha);
        }

        public void SetAccent(Color accent, float duration = .35f)
        {
            Accent = accent;
            for (int i = accents.Count - 1; i >= 0; i--)
            {
                var binding = accents[i];
                if (!binding.Graphic) { accents.RemoveAt(i); continue; }
                Color target = DbTheme.Alpha(binding.Lift > 0f ? DbTheme.Lift(accent, binding.Lift) : accent, binding.Alpha);
                if (duration <= 0f) binding.Graphic.color = target;
                else DbMotion.Tint(binding.Graphic, target, duration);
            }
        }

        // Closes the window when this object disappears (the fighter it edits, for example).
        public void BindLifetime(UnityEngine.Object target)
        {
            lifetime = target;
            watchesLifetime = true;
        }

        // Periodic refresh while the window is open, e.g. to mirror live ki.
        public void SetTick(float interval, Action action)
        {
            tickEvery = interval;
            tick = action;
            tickAt = 0f;
        }

        void AnimateIn()
        {
            Group.alpha = 0f;
            Panel.localScale = Vector3.one * Fit(userScale) * .94f;
            var home = Panel.anchoredPosition;
            Panel.anchoredPosition = home + new Vector2(0f, -26f);
            DbMotion.Fade(Group, 1f, .16f);
            DbMotion.Move(Panel, home, .3f, DbEase.OutQuint);
            DbMotion.Scale(Panel, Fit(userScale), .3f, DbEase.OutBack);
            if (backdrop) DbMotion.Tint(backdrop, new Color(.01f, .015f, .03f, .62f), .2f);
        }

        float Fit(float scale)
        {
            var root = DbUiRoot.Current;
            return (root ? root.DesignScale : 1f) * scale;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            Group.blocksRaycasts = false;
            if (backdrop) { backdrop.raycastTarget = false; DbMotion.Tint(backdrop, new Color(.01f, .015f, .03f, 0f), .12f); }
            SavePlacement();
            ReleaseBlocker();
            if (DbUiRoot.Current) DbUiRoot.Current.Unregister(this);
            var callback = OnClosed;
            OnClosed = null;
            callback?.Invoke();
            DbMotion.Scale(Panel, Panel.localScale.x * .96f, .12f, DbEase.OutQuad);
            DbMotion.Fade(Group, 0f, .12f, DbEase.OutQuad, 0f, () => { if (this) Destroy(gameObject); });
        }

        // Hides without closing, e.g. while a native dialog is on screen.
        public void SetHidden(bool hidden)
        {
            gameObject.SetActive(!hidden);
            if (hidden) ReleaseBlocker();
            else if (modal && !blocker && Global.main) { blocker = Global.main; blocker.AddUiBlocker(); }
        }

        public void CloseImmediate()
        {
            closing = true;
            ReleaseBlocker();
            if (DbUiRoot.Current) DbUiRoot.Current.Unregister(this);
            OnClosed = null;
            Destroy(gameObject);
        }

        void ReleaseBlocker()
        {
            DbUi.Release(blocker);
            blocker = null;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (DbUiRoot.Current) DbUiRoot.Current.Raise(this);
        }

        internal void MoveBy(Vector2 delta)
        {
            Panel.anchoredPosition = Clamped(Panel.anchoredPosition + delta);
        }

        internal float UserScale
        {
            get { return userScale; }
            set
            {
                userScale = Mathf.Clamp(value, .6f, 1.6f);
                DbMotion.Stop(Panel);
                Panel.localScale = Vector3.one * Fit(userScale);
                Panel.anchoredPosition = Clamped(Panel.anchoredPosition);
            }
        }

        internal void ResetPlacement()
        {
            userScale = 1f;
            DbMotion.Scale(Panel, Fit(1f), .24f, DbEase.OutBack);
            DbMotion.Move(Panel, Vector2.zero, .24f, DbEase.OutCubic);
        }

        Vector2 Clamped(Vector2 position)
        {
            var parent = DbUiRoot.Current ? DbUiRoot.Current.Rect.rect.size : new Vector2(DbUi.DesignWidth, DbUi.DesignHeight);
            float scale = Panel.localScale.x;
            // Keep the title bar reachable; the rest may hang off screen.
            float marginX = parent.x * .5f + size.x * scale * .5f - 120f;
            float marginY = parent.y * .5f - size.y * scale * .5f;
            return new Vector2(Mathf.Clamp(position.x, -marginX, marginX), Mathf.Clamp(position.y, -marginY - size.y * scale + 90f, marginY));
        }

        void LoadPlacement()
        {
            if (!PlayerPrefs.HasKey("DBS.UI." + id + ".s")) return;
            userScale = Mathf.Clamp(PlayerPrefs.GetFloat("DBS.UI." + id + ".s", 1f), .6f, 1.6f);
            Panel.localScale = Vector3.one * Fit(userScale);
            Panel.anchoredPosition = Clamped(new Vector2(PlayerPrefs.GetFloat("DBS.UI." + id + ".x", 0f), PlayerPrefs.GetFloat("DBS.UI." + id + ".y", 0f)));
        }

        void SavePlacement()
        {
            PlayerPrefs.SetFloat("DBS.UI." + id + ".s", userScale);
            PlayerPrefs.SetFloat("DBS.UI." + id + ".x", Panel.anchoredPosition.x);
            PlayerPrefs.SetFloat("DBS.UI." + id + ".y", Panel.anchoredPosition.y);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (streaks)
            {
                float x = streaks.anchoredPosition.x + Time.unscaledDeltaTime * 42f;
                if (x > 0f) x -= 256f;
                streaks.anchoredPosition = new Vector2(x, 0f);
            }
            if (watchesLifetime && now >= lifetimeCheck)
            {
                lifetimeCheck = now + .25f;
                if (!lifetime) { Close(); return; }
            }
            if (tick != null && now >= tickAt)
            {
                tickAt = now + tickEvery;
                try { tick(); }
                catch (Exception ex) { Debug.LogWarning("[DBS UI] " + id + ": " + ex.Message); tick = null; }
            }
        }

        void OnDestroy()
        {
            ReleaseBlocker();
            if (DbUiRoot.Current) DbUiRoot.Current.Unregister(this);
        }
    }

    public sealed class DbWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        public DbWindow Window;
        Vector2 last;
        float lastClick = -10f;

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Window.Panel.parent, e.position, e.pressEventCamera, out last);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Window.Panel.parent, e.position, e.pressEventCamera, out var now);
            Window.MoveBy(now - last);
            last = now;
        }

        public void OnPointerClick(PointerEventData e)
        {
            float time = Time.unscaledTime;
            if (time - lastClick <= .3f) { Window.ResetPlacement(); lastClick = -10f; return; }
            lastClick = time;
        }
    }

    public sealed class DbWindowGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public DbWindow Window;
        Vector2 start;
        float startScale;

        public void OnBeginDrag(PointerEventData e)
        {
            start = e.position;
            startScale = Window.UserScale;
        }

        public void OnDrag(PointerEventData e)
        {
            var delta = e.position - start;
            Window.UserScale = startScale + (delta.x - delta.y) / Mathf.Max(400f, Screen.height);
        }

        public void OnPointerEnter(PointerEventData e) { DbMotion.Tint(GetComponent<Image>(), Window.Accent, .12f); }
        public void OnPointerExit(PointerEventData e) { DbMotion.Tint(GetComponent<Image>(), DbTheme.TextMuted, .16f); }
    }
}
