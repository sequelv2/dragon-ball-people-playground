using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DBPU
{
    internal enum CaptionPulse { None, Cycle, Confirm, Fail }

    // What one caption shows. The torso technique picker fills every field; the Kaioken toast
    // uses only the tab (icon, title, tag).
    internal sealed class TechniqueCaptionContent
    {
        public string Title = "";
        public Sprite Icon;
        // A kit glyph is tinted with the accent; power art keeps its own colours.
        public bool IconIsGlyph;
        public Color Accent = DbTheme.Orange;
        public string Tag;
        public Color TagColor = DbTheme.TextSoft;
        public int Count, Index;
        public string NextKey, UseKey;

        public bool HasPanel { get { return Count > 1 || !string.IsNullOrEmpty(NextKey) || !string.IsNullOrEmpty(Tag) && Count > 0; } }

        public bool Same(TechniqueCaptionContent o)
        {
            return o != null && o.Title == Title && o.Icon == Icon && o.IconIsGlyph == IconIsGlyph && o.Accent == Accent && o.Tag == Tag
                && o.TagColor == TagColor && o.Count == Count && o.Index == Index && o.NextKey == NextKey && o.UseKey == UseKey;
        }
    }

    // Captions above fighters' heads in the look of the mod's menus and combat wheels: the slanted
    // accent tab of the wheel toasts with the technique's icon and name, and for the picker a slim
    // chamfered panel under it with the position dots, the controls and the technique's state.
    // Screen-space, laid out at 1080p and scaled to the screen, so the size never follows the zoom.
    public sealed class TechniqueCaptions : MonoBehaviour
    {
        public const int PickerSlot = 0, ToastSlot = 1;
        const float TabHeight = 38f, PanelHeight = 28f, Gap = 5f, MinWidth = 236f, MaxWidth = 420f;
        const float TitleLeft = 64f, IconDisc = 44f;
        static TechniqueCaptions instance;

        sealed class View
        {
            public PersonBehaviour Person;
            public Transform Head;
            public int Slot;
            public RectTransform Root, Body;
            public CanvasGroup Group;
            public Image Tab, IconBack, IconRing, Icon, Panel;
            public TextMeshProUGUI Title, Status, Hint;
            public readonly List<Image> Dots = new List<Image>();
            public TechniqueCaptionContent Content;
            public bool Persistent, Closing;
            public float Until, CloseAt, ShakeAt = -10f, NextBounds, TopAbove = .35f, Height;
        }

        readonly Dictionary<long, View> views = new Dictionary<long, View>();
        readonly Dictionary<int, float> stackOffset = new Dictionary<int, float>();
        readonly List<long> stale = new List<long>();

        static TechniqueCaptions Ensure()
        {
            if (instance) return instance;
            DbAtlas.Ensure();
            var go = new GameObject("DBPU technique captions", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31998;
            instance = go.AddComponent<TechniqueCaptions>();
            return instance;
        }

        void Awake() { SceneManager.activeSceneChanged += SceneChanged; }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; Clear(); if (instance == this) instance = null; }
        void SceneChanged(Scene before, Scene after) { Clear(); }

        void Clear()
        {
            foreach (var v in views.Values) if (v.Root) Destroy(v.Root.gameObject);
            views.Clear();
        }

        static long Key(PersonBehaviour person, int slot) { return person.GetInstanceID() * 4L + slot; }

        View Get(PersonBehaviour person, int slot, bool create)
        {
            View v;
            if (views.TryGetValue(Key(person, slot), out v) && v.Root) return v;
            if (!create) return null;
            v = Build(person, slot);
            views[Key(person, slot)] = v;
            return v;
        }

        View Build(PersonBehaviour person, int slot)
        {
            var v = new View { Person = person, Slot = slot, Head = DbpuWorldLabel.HeadOf(person) };
            v.Root = DbUi.Node(slot == PickerSlot ? "Technique picker" : "Technique toast", transform);
            v.Root.anchorMin = v.Root.anchorMax = Vector2.zero;
            v.Root.pivot = new Vector2(.5f, 0f);
            v.Body = DbUi.Node("Body", v.Root);
            v.Body.Stretch();
            v.Body.pivot = new Vector2(.5f, 0f);
            v.Group = v.Body.gameObject.AddComponent<CanvasGroup>();
            v.Group.blocksRaycasts = v.Group.interactable = false;

            v.Panel = DbUi.Img(v.Body, DbAtlas.ChamferTight, DbTheme.Alpha(DbTheme.Panel, .95f), "Panel");
            DbUi.Img(v.Panel.transform, DbAtlas.ChamferTightLine, DbTheme.StrokeStrong, "Line").Rt().Stretch();
            v.Hint = DbUi.Label(v.Panel.transform, "", DbTextStyle.Small, DbTheme.TextMuted, TextAlignmentOptions.MidlineLeft, "Controls");
            v.Hint.fontSize = 12f;
            v.Status = DbUi.Label(v.Panel.transform, "", DbTextStyle.Caps, DbTheme.TextSoft, TextAlignmentOptions.MidlineRight, "State");
            v.Status.fontSize = 11.5f;

            v.Tab = DbUi.Img(v.Body, DbAtlas.Slant, DbTheme.Orange, "Tab");
            v.Title = DbUi.Label(v.Tab.transform, "", DbTextStyle.Title, DbTheme.TextOnAccent, TextAlignmentOptions.MidlineLeft, "Name");
            v.Title.fontSize = 17f;
            v.Title.enableAutoSizing = true;
            v.Title.fontSizeMin = 11f;
            v.Title.fontSizeMax = 17f;

            // The icon sits in a rounded tile with an accent frame, as in the combat wheel slots.
            v.IconBack = DbUi.Img(v.Body, DbAtlas.Round, DbTheme.Alpha(DbTheme.Void, .96f), "Icon tile");
            v.Icon = DbUi.Img(v.IconBack.transform, null, Color.white, "Icon");
            v.Icon.preserveAspect = true;
            v.Icon.Rt().Centre(0f, 0f, IconDisc - 6f, IconDisc - 6f);
            v.IconRing = DbUi.Img(v.IconBack.transform, DbAtlas.RoundLine, DbTheme.Orange, "Icon frame");
            v.IconRing.Rt().Stretch(-1f, -1f, -1f, -1f);

            v.Group.alpha = 0f;
            v.Body.localScale = Vector3.one * .82f;
            DbMotion.Fade(v.Group, 1f, .1f);
            DbMotion.Scale(v.Body, 1f, .2f, DbEase.OutBack);
            return v;
        }

        static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

        static void Apply(View v, TechniqueCaptionContent k, bool animateAccent)
        {
            bool panel = v.Slot == PickerSlot && k.HasPanel;
            v.Title.text = v.Slot == ToastSlot && !string.IsNullOrEmpty(k.Tag) ? k.Title + "  " + k.Tag : k.Title;

            if (animateAccent) DbMotion.Tint(v.Tab, k.Accent, .14f);
            else { DbMotion.Stop(v.Tab); v.Tab.color = k.Accent; }
            v.IconRing.color = DbTheme.Lift(k.Accent, .15f);
            v.Icon.sprite = k.Icon;
            v.Icon.enabled = k.Icon;
            v.Icon.color = k.IconIsGlyph ? DbTheme.Lift(k.Accent, .2f) : Color.white;
            v.Icon.Rt().sizeDelta = Vector2.one * (k.IconIsGlyph ? IconDisc - 20f : IconDisc - 6f);

            v.Panel.gameObject.SetActive(panel);
            float titleWidth = Mathf.Min(DbUi.PreferredWidth(v.Title), MaxWidth - TitleLeft - 30f);
            float width = Mathf.Clamp(TitleLeft + titleWidth + 34f, MinWidth, MaxWidth);
            if (panel)
            {
                v.Hint.text = string.IsNullOrEmpty(k.NextKey) ? "" :
                    "<b><color=#" + Hex(DbTheme.Text) + ">" + k.NextKey + "</color></b>  next     <b><color=#" + Hex(DbTheme.Text) + ">" + k.UseKey + "</color></b>  use";
                v.Status.text = k.Tag ?? "";
                v.Status.color = k.TagColor;
                int dots = k.Count > 1 ? k.Count : 0;
                float dotsWidth = dots > 0 ? dots * 11f + 6f : 0f;
                float needed = 28f + 14f + dotsWidth + DbUi.PreferredWidth(v.Hint) + 18f + DbUi.PreferredWidth(v.Status) + 14f;
                width = Mathf.Clamp(Mathf.Max(width, needed), MinWidth, MaxWidth);
                float panelWidth = width - 28f;
                v.Panel.Rt().Pin(14f, TabHeight + Gap, panelWidth, PanelHeight);
                while (v.Dots.Count < dots) v.Dots.Add(DbUi.Img(v.Panel.transform, DbAtlas.Disc, DbTheme.TextMuted, "Dot"));
                for (int i = 0; i < v.Dots.Count; i++)
                {
                    bool shown = i < dots;
                    v.Dots[i].gameObject.SetActive(shown);
                    if (!shown) continue;
                    bool current = i == k.Index;
                    float size = current ? 8f : 6f;
                    v.Dots[i].Rt().Pin(14f + i * 11f + (8f - size) * .5f, (PanelHeight - size) * .5f, size, size);
                    v.Dots[i].color = current ? DbTheme.Lift(k.Accent, .2f) : DbTheme.Alpha(DbTheme.TextMuted, .55f);
                }
                v.Hint.Rt().Pin(14f + dotsWidth, 0f, panelWidth - 28f - dotsWidth, PanelHeight);
                v.Status.Rt().Pin(14f, 0f, panelWidth - 28f, PanelHeight);
            }

            v.Height = TabHeight + (panel ? Gap + PanelHeight : 0f);
            v.Root.sizeDelta = new Vector2(width, v.Height);
            v.Tab.Rt().Pin(0f, 0f, width, TabHeight);
            v.Title.Rt().Pin(TitleLeft, 0f, width - TitleLeft - 28f, TabHeight);
            v.IconBack.Rt().Pin(8f, (TabHeight - IconDisc) * .5f, IconDisc, IconDisc);
        }

        internal static void Show(PersonBehaviour person, int slot, TechniqueCaptionContent content, float seconds)
        {
            if (!person || content == null) return;
            var self = Ensure();
            var v = self.Get(person, slot, true);
            if (v.Closing)
            {
                v.Closing = false;
                DbMotion.Stop(v.Group); DbMotion.Stop(v.Body);
                DbMotion.Fade(v.Group, 1f, .1f);
                DbMotion.Scale(v.Body, 1f, .16f, DbEase.OutBack);
            }
            if (!content.Same(v.Content)) { Apply(v, content, false); v.Content = content; }
            v.Persistent = seconds <= 0f;
            v.Until = Time.unscaledTime + Mathf.Max(.1f, seconds);
        }

        internal static bool IsShown(PersonBehaviour person, int slot)
        {
            if (!instance || !person) return false;
            var v = instance.Get(person, slot, false);
            return v != null && !v.Closing;
        }

        internal static void Pulse(PersonBehaviour person, int slot, CaptionPulse pulse)
        {
            if (!instance || !person) return;
            var v = instance.Get(person, slot, false);
            if (v == null || v.Closing) return;
            if (pulse == CaptionPulse.Cycle)
            {
                var icon = v.IconBack.Rt();
                icon.localScale = Vector3.one * .84f;
                DbMotion.Scale(icon, 1f, .18f, DbEase.OutBack);
                var title = v.Title.Rt();
                var home = new Vector2(TitleLeft, 0f);
                DbMotion.Stop(title);
                title.anchoredPosition = home + new Vector2(10f, 0f);
                DbMotion.Move(title, home, .16f, DbEase.OutCubic);
                if (v.Content != null)
                {
                    v.Tab.color = DbTheme.Lift(v.Content.Accent, .35f);
                    DbMotion.Tint(v.Tab, v.Content.Accent, .18f);
                }
            }
            else if (pulse == CaptionPulse.Fail) v.ShakeAt = Time.unscaledTime;
        }

        internal static void Close(PersonBehaviour person, int slot, bool confirmed)
        {
            if (!instance || !person) return;
            var v = instance.Get(person, slot, false);
            if (v == null || v.Closing) return;
            v.Closing = true;
            v.CloseAt = Time.unscaledTime;
            if (confirmed && v.Content != null)
            {
                v.Tab.color = DbTheme.Lift(v.Content.Accent, .6f);
                DbMotion.Tint(v.Tab, v.Content.Accent, .2f);
                DbMotion.Scale(v.Body, 1.07f, .2f, DbEase.OutCubic);
                DbMotion.Fade(v.Group, 0f, .22f, DbEase.OutCubic, .08f);
            }
            else DbMotion.Fade(v.Group, 0f, .16f);
        }

        void LateUpdate()
        {
            var cam = Global.main ? Global.main.camera : Camera.main;
            float s = Mathf.Clamp(Screen.height / 1080f, .7f, 2.2f);
            float now = Time.unscaledTime;
            stale.Clear();
            stackOffset.Clear();
            // Pickers first, so a toast for the same fighter can sit above its picker.
            for (int pass = 0; pass < 2; pass++)
                foreach (var pair in views)
                {
                    var v = pair.Value;
                    if ((v.Slot == PickerSlot) != (pass == 0)) continue;
                    if (!Step(v, cam, s, now)) stale.Add(pair.Key);
                }
            foreach (long key in stale)
            {
                View v;
                if (views.TryGetValue(key, out v) && v.Root) Destroy(v.Root.gameObject);
                views.Remove(key);
            }
        }

        bool Step(View v, Camera cam, float s, float now)
        {
            if (!v.Root || !v.Person || !v.Head || !v.Head.gameObject.activeInHierarchy || v.Content == null) return false;
            if (!v.Closing && !v.Persistent && now > v.Until) Close(v.Person, v.Slot, false);
            if (v.Closing && now - v.CloseAt > .4f) return false;

            if (now >= v.NextBounds) { v.NextBounds = now + .5f; v.TopAbove = TopAbove(v.Head); }
            if (!cam) { v.Root.gameObject.SetActive(false); return true; }
            Vector3 head = v.Head.position;
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(head.x, head.y + v.TopAbove, head.z));
            v.Root.gameObject.SetActive(screen.z > 0f);

            float shake = 0f, t = now - v.ShakeAt;
            if (t < .3f) shake = Mathf.Sin(t * 70f) * 7f * (1f - t / .3f);

            int id = v.Person.GetInstanceID();
            float below;
            stackOffset.TryGetValue(id, out below);
            float half = v.Root.sizeDelta.x * s * .5f + 6f;
            float x = Mathf.Clamp(screen.x, half, Mathf.Max(half, Screen.width - half)) + shake * s;
            float y = Mathf.Clamp(screen.y + 14f * s + below, 4f, Mathf.Max(4f, Screen.height - (v.Height + 6f) * s));
            v.Root.localScale = Vector3.one * s;
            v.Root.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            if (v.Slot == PickerSlot && !v.Closing) stackOffset[id] = below + (v.Height + 8f) * s;
            return true;
        }

        // Height of the head's art (hair included) above the head's pivot, in world units.
        static float TopAbove(Transform head)
        {
            float top = float.NegativeInfinity;
            foreach (var r in head.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r || !r.enabled || !r.sprite || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.y > 3f || b.size.x > 3f) continue;
                top = Mathf.Max(top, b.max.y);
            }
            return float.IsInfinity(top) ? .35f : Mathf.Clamp(top - head.position.y, .05f, 2.5f);
        }
    }
}
