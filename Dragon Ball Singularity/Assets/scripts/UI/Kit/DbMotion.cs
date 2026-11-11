using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    public enum DbEase { Linear, OutQuad, OutCubic, OutQuint, OutBack, OutExpo, InCubic, InOutCubic }

    // One runner animates every menu. It sleeps while nothing moves, allocates nothing per frame,
    // and uses unscaled time so menus stay alive during slow motion and pause.
    public sealed class DbMotion : MonoBehaviour
    {
        enum Kind : byte { Move, Scale, Fade, Tint, Size, Rotate, Fill, Value }

        struct Tween
        {
            public UnityEngine.Object Target;
            public Kind Kind;
            public DbEase Ease;
            public float Time, Duration, Delay;
            public Vector4 From, To;
            public bool Started;
            public Action<float> Value;
            public Action Done;
        }

        static DbMotion runner;
        readonly List<Tween> tweens = new List<Tween>(128);

        static DbMotion Runner
        {
            get
            {
                if (runner) return runner;
                var go = new GameObject("DBS UI Motion");
                go.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(go);
                runner = go.AddComponent<DbMotion>();
                return runner;
            }
        }

        public static float Apply(DbEase ease, float t)
        {
            switch (ease)
            {
                case DbEase.OutQuad: return 1f - (1f - t) * (1f - t);
                case DbEase.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case DbEase.OutQuint: { float u = 1f - t; return 1f - u * u * u * u * u; }
                case DbEase.OutBack: { const float s = 1.70158f; float u = t - 1f; return 1f + (s + 1f) * u * u * u + s * u * u; }
                case DbEase.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case DbEase.InCubic: return t * t * t;
                case DbEase.InOutCubic: return t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * .5f;
                default: return t;
            }
        }

        public static void Move(RectTransform rt, Vector2 to, float duration = .22f, DbEase ease = DbEase.OutCubic, float delay = 0f, Action done = null)
        {
            if (rt) Begin(rt, Kind.Move, Vector4.zero, to, duration, ease, delay, null, done);
        }

        public static void Scale(Transform t, float to, float duration = .16f, DbEase ease = DbEase.OutCubic, float delay = 0f, Action done = null)
        {
            if (t) Begin(t, Kind.Scale, Vector4.zero, new Vector4(to, to, 1f, 0f), duration, ease, delay, null, done);
        }

        public static void Fade(CanvasGroup group, float to, float duration = .16f, DbEase ease = DbEase.OutCubic, float delay = 0f, Action done = null)
        {
            if (group) Begin(group, Kind.Fade, Vector4.zero, new Vector4(to, 0f, 0f, 0f), duration, ease, delay, null, done);
        }

        public static void Tint(Graphic graphic, Color to, float duration = .14f, DbEase ease = DbEase.OutQuad, float delay = 0f)
        {
            if (graphic) Begin(graphic, Kind.Tint, Vector4.zero, to, duration, ease, delay, null, null);
        }

        public static void Size(RectTransform rt, Vector2 to, float duration = .2f, DbEase ease = DbEase.OutCubic, float delay = 0f)
        {
            if (rt) Begin(rt, Kind.Size, Vector4.zero, to, duration, ease, delay, null, null);
        }

        public static void Rotate(RectTransform rt, float degrees, float duration = .2f, DbEase ease = DbEase.OutCubic, float delay = 0f)
        {
            if (rt) Begin(rt, Kind.Rotate, Vector4.zero, new Vector4(degrees, 0f, 0f, 0f), duration, ease, delay, null, null);
        }

        public static void Fill(Image image, float to, float duration = .25f, DbEase ease = DbEase.OutCubic, float delay = 0f)
        {
            if (image) Begin(image, Kind.Fill, Vector4.zero, new Vector4(to, 0f, 0f, 0f), duration, ease, delay, null, null);
        }

        // Owner keeps the delegate alive only as long as the UI object it animates.
        public static void Value(UnityEngine.Object owner, float from, float to, Action<float> apply, float duration = .3f, DbEase ease = DbEase.OutCubic, float delay = 0f, Action done = null)
        {
            if (owner) Begin(owner, Kind.Value, new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f), duration, ease, delay, apply, done);
        }

        public static void Stop(UnityEngine.Object target)
        {
            if (!runner || !target) return;
            var list = runner.tweens;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Target == target) RemoveAt(list, i);
        }

        static void RemoveAt(List<Tween> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }

        static void Begin(UnityEngine.Object target, Kind kind, Vector4 from, Vector4 to, float duration, DbEase ease, float delay, Action<float> value, Action done)
        {
            var r = Runner;
            var list = r.tweens;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Kind == kind && list[i].Target == target) RemoveAt(list, i);

            list.Add(new Tween
            {
                Target = target, Kind = kind, Ease = ease,
                Duration = Mathf.Max(.0001f, duration), Delay = delay,
                From = from, To = to, Value = value, Done = done
            });
            r.enabled = true;
        }

        void Update()
        {
            float dt = Mathf.Min(UnityEngine.Time.unscaledDeltaTime, .05f);
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (i >= tweens.Count) continue;
                var tween = tweens[i];
                if (!tween.Target) { RemoveAt(tweens, i); continue; }
                if (tween.Delay > 0f)
                {
                    tween.Delay -= dt;
                    tweens[i] = tween;
                    continue;
                }
                if (!tween.Started)
                {
                    tween.Started = true;
                    if (tween.Kind != Kind.Value) tween.From = Read(tween);
                }
                tween.Time += dt;
                float t = Mathf.Clamp01(tween.Time / tween.Duration);
                bool finished = t >= 1f;
                try { Write(tween, Apply(tween.Ease, t)); }
                catch (Exception ex) { Debug.LogWarning("[DBS UI] " + ex.Message); finished = true; }

                if (!finished) { tweens[i] = tween; continue; }
                RemoveAt(tweens, i);
                if (tween.Done != null)
                {
                    try { tween.Done(); }
                    catch (Exception ex) { Debug.LogWarning("[DBS UI] " + ex.Message); }
                }
            }
            if (tweens.Count == 0) enabled = false;
        }

        static Vector4 Read(in Tween tween)
        {
            switch (tween.Kind)
            {
                case Kind.Move: return ((RectTransform)tween.Target).anchoredPosition;
                case Kind.Scale: return ((Transform)tween.Target).localScale;
                case Kind.Fade: return new Vector4(((CanvasGroup)tween.Target).alpha, 0f, 0f, 0f);
                case Kind.Tint: return ((Graphic)tween.Target).color;
                case Kind.Size: return ((RectTransform)tween.Target).sizeDelta;
                case Kind.Rotate: return new Vector4(((RectTransform)tween.Target).localEulerAngles.z, 0f, 0f, 0f);
                case Kind.Fill: return new Vector4(((Image)tween.Target).fillAmount, 0f, 0f, 0f);
                default: return tween.From;
            }
        }

        static void Write(in Tween tween, float eased)
        {
            switch (tween.Kind)
            {
                case Kind.Move:
                    ((RectTransform)tween.Target).anchoredPosition = Vector2.LerpUnclamped(tween.From, tween.To, eased);
                    break;
                case Kind.Scale:
                    ((Transform)tween.Target).localScale = Vector3.LerpUnclamped(tween.From, tween.To, eased);
                    break;
                case Kind.Fade:
                    ((CanvasGroup)tween.Target).alpha = Mathf.LerpUnclamped(tween.From.x, tween.To.x, eased);
                    break;
                case Kind.Tint:
                    ((Graphic)tween.Target).color = Color.LerpUnclamped(tween.From, tween.To, eased);
                    break;
                case Kind.Size:
                    ((RectTransform)tween.Target).sizeDelta = Vector2.LerpUnclamped(tween.From, tween.To, eased);
                    break;
                case Kind.Rotate:
                    ((RectTransform)tween.Target).localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(tween.From.x, tween.To.x, eased));
                    break;
                case Kind.Fill:
                    ((Image)tween.Target).fillAmount = Mathf.LerpUnclamped(tween.From.x, tween.To.x, eased);
                    break;
                default:
                    tween.Value?.Invoke(Mathf.LerpUnclamped(tween.From.x, tween.To.x, eased));
                    break;
            }
        }
    }
}
