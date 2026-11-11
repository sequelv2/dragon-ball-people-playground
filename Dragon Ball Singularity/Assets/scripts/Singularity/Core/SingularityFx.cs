using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Shared visual language for Singularity modules. Everything is built from the mod's
    // existing procedural textures (DbpuFx) and sprites, so new effects match the old ones.
    public static class SingularityFx
    {
        static readonly List<SpriteRenderer> scratch = new List<SpriteRenderer>();

        // Sprite copy of a whole fighter, left behind as an afterimage (Zanzoken / Ultra Instinct).
        public static void Afterimage(PersonBehaviour person, Color tint, float life, Vector2 drift = default(Vector2))
        {
            if (!person) return;
            scratch.Clear();
            person.GetComponentsInChildren(false, scratch);
            var root = new GameObject("DBPU afterimage");
            var ghost = root.AddComponent<SingularityGhost>();
            ghost.Life = Mathf.Max(.05f, life); ghost.Drift = drift; ghost.Tint = tint;
            foreach (var sr in scratch)
            {
                if (!sr || !sr.enabled || !sr.sprite) continue;
                if (sr.name.StartsWith("DBPU", StringComparison.Ordinal) || sr.name == "UIDodgeGhost") continue;
                if (sr.color.a <= .02f) continue;
                var t = sr.transform;
                var go = new GameObject("ghost");
                go.transform.SetParent(root.transform, false);
                go.transform.position = t.position;
                go.transform.rotation = t.rotation;
                go.transform.localScale = t.lossyScale;
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sprite = sr.sprite; copy.flipX = sr.flipX; copy.flipY = sr.flipY;
                copy.sharedMaterial = DbpuFx.SoftSprite;
                copy.sortingLayerID = sr.sortingLayerID; copy.sortingOrder = sr.sortingOrder - 1;
                ghost.Add(copy);
            }
            scratch.Clear();
            if (ghost.Count == 0) UnityEngine.Object.Destroy(root);
        }

        public static void Lightning(Vector2 from, Vector2 to, Color color, float width = .06f, float life = .22f, int segments = 12, float jitter = .35f)
        {
            var go = new GameObject("DBPU lightning");
            var bolt = go.AddComponent<SingularityBolt>();
            bolt.Setup(from, to, color, width, life, segments, jitter);
        }

        public static void Ring(Vector2 at, Color color, float radius, float life = .45f, float thickness = 1f, string layer = "Top", int order = 60)
        {
            var go = new GameObject("DBPU shockwave ring");
            var ring = go.AddComponent<SingularityPulse>();
            ring.Setup(at, color, radius, life, thickness >= 1.5f ? DbpuFx.RingThick : DbpuFx.Ring, layer, order, .15f, true);
        }

        public static void Flash(Vector2 at, Color color, float size, float life = .3f, string layer = "Top", int order = 62)
        {
            var go = new GameObject("DBPU flash");
            var pulse = go.AddComponent<SingularityPulse>();
            pulse.Setup(at, color, size, life, DbpuFx.Glow, layer, order, .65f, false);
        }

        public static void Burst(Vector2 at, Color color, float scale, int count = 18)
        {
            var field = DbpuParticles.Create("DBPU burst", DbpuFx.Spark, Mathf.Max(8, count + 4), 61);
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float speed = UnityEngine.Random.Range(3f, 9f) * scale;
                field.Emit(at, dir * speed, UnityEngine.Random.Range(.25f, .55f), UnityEngine.Random.Range(.05f, .12f) * scale,
                    DbpuFx.A(Color.Lerp(color, Color.white, .5f), 1f), DbpuFx.A(color, 0f), 4f, 0f, 0f, true);
            }
            field.Release();
        }

        public static void Dust(Vector2 at, float scale)
        {
            var field = DbpuParticles.Create("DBPU dust", DbpuFx.Glow, 40, 55);
            for (int i = 0; i < 28; i++)
            {
                float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                Vector2 v = new Vector2(side * UnityEngine.Random.Range(1.5f, 6f), UnityEngine.Random.Range(.2f, 2.2f)) * scale;
                Color c = new Color(.62f, .58f, .52f, .55f);
                field.Emit(at, v, UnityEngine.Random.Range(.5f, 1.1f), UnityEngine.Random.Range(.25f, .55f) * scale, c, DbpuFx.A(c, 0f), 2.5f, -.4f, 0f, false, .9f * scale);
            }
            field.Release();
        }

        public static GameObject Sound(Vector2 at, string relative, float volume = 1f)
        {
            return DbpuFx.Sound(at, relative, volume);
        }
    }

    public sealed class SingularityGhost : MonoBehaviour
    {
        public float Life = .35f; public Vector2 Drift; public Color Tint = Color.white;
        readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        float age;
        public int Count { get { return parts.Count; } }
        public void Add(SpriteRenderer sr) { parts.Add(sr); sr.color = Tint; }
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            float k = Mathf.Clamp01(age / Life);
            transform.position += (Vector3)(Drift * dt);
            Color c = Tint; c.a = Tint.a * (1f - k) * (1f - k);
            for (int i = 0; i < parts.Count; i++) if (parts[i]) parts[i].color = c;
            if (k >= 1f) Destroy(gameObject);
        }
    }

    public sealed class SingularityPulse : MonoBehaviour
    {
        SpriteRenderer sr; Vector2 at; Color color; float size, life, age, startScale; bool grow;
        public void Setup(Vector2 position, Color c, float targetSize, float duration, Texture2D tex, string layer, int order, float startFraction, bool expand)
        {
            at = position; color = c; size = targetSize; life = Mathf.Max(.05f, duration); grow = expand; startScale = startFraction;
            sr = DbpuFx.MakeSprite(transform, "pulse", tex, order, true, layer);
            Apply(0f);
        }
        void Apply(float k)
        {
            float s = grow ? Mathf.Lerp(size * startScale, size, 1f - (1f - k) * (1f - k)) : size * Mathf.Lerp(1f, 1.25f, k);
            float a = grow ? (1f - k) : (1f - k * k);
            DbpuFx.Place(sr, at, -.6f, s * 2f, s * 2f, 0f, DbpuFx.A(color, color.a * a));
        }
        void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(age / life);
            Apply(k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    public sealed class SingularityBolt : MonoBehaviour
    {
        LineRenderer core, glow; float life, age; Color color;
        public void Setup(Vector2 from, Vector2 to, Color c, float width, float duration, int segments, float jitter)
        {
            color = c; life = Mathf.Max(.05f, duration);
            glow = DbpuFx.Line(transform, "bolt glow", 63, DbpuFx.Ribbon);
            core = DbpuFx.Line(transform, "bolt core", 64);
            int n = Mathf.Clamp(segments, 3, 40);
            glow.positionCount = core.positionCount = n + 1;
            Vector2 dir = to - from; Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float off = (i == 0 || i == n) ? 0f : UnityEngine.Random.Range(-1f, 1f) * jitter * Mathf.Sin(t * Mathf.PI);
                Vector2 p = from + dir * t + normal * off;
                glow.SetPosition(i, new Vector3(p.x, p.y, -.6f));
                core.SetPosition(i, new Vector3(p.x, p.y, -.61f));
            }
            glow.startWidth = glow.endWidth = width * 4f;
            core.startWidth = width; core.endWidth = width * .6f;
            Apply(0f);
        }
        void Apply(float k)
        {
            float a = 1f - k;
            glow.startColor = glow.endColor = DbpuFx.A(color, .45f * a);
            core.startColor = core.endColor = DbpuFx.A(Color.Lerp(color, Color.white, .7f), a);
        }
        void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(age / life);
            Apply(k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    // Full-view colour veils that follow the camera (darkened sky, time-stop tint, white-out).
    public sealed class SingularityVeil : MonoBehaviour
    {
        static readonly Dictionary<string, SingularityVeil> veils = new Dictionary<string, SingularityVeil>();
        SpriteRenderer sr; Color color; float alpha, target, speed;

        public static void Show(string key, Color color, float alpha, float fadeSeconds, string layer = "Top", int order = 900)
        {
            SingularityVeil veil;
            if (!veils.TryGetValue(key, out veil) || !veil)
            {
                var go = new GameObject("DBPU veil " + key);
                veil = go.AddComponent<SingularityVeil>();
                veil.sr = DbpuFx.MakeSprite(go.transform, "veil", DbpuFx.Square, order, false, layer);
                veils[key] = veil;
            }
            veil.color = color; veil.target = Mathf.Clamp01(alpha);
            veil.speed = fadeSeconds <= 0f ? 1000f : 1f / fadeSeconds;
            if (fadeSeconds <= 0f) veil.alpha = veil.target;
            veil.Apply();
        }

        public static void Hide(string key, float fadeSeconds)
        {
            SingularityVeil veil;
            if (!veils.TryGetValue(key, out veil) || !veil) return;
            veil.target = 0f; veil.speed = fadeSeconds <= 0f ? 1000f : 1f / fadeSeconds;
        }

        public static float Level(string key)
        {
            SingularityVeil veil;
            return veils.TryGetValue(key, out veil) && veil ? veil.alpha : 0f;
        }

        void LateUpdate()
        {
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * speed);
            Apply();
            if (alpha <= 0f && target <= 0f)
            {
                foreach (var pair in new List<KeyValuePair<string, SingularityVeil>>(veils))
                    if (pair.Value == this) veils.Remove(pair.Key);
                Destroy(gameObject);
            }
        }

        void Apply()
        {
            var cam = Global.main ? Global.main.camera : Camera.main;
            if (!cam || !sr) return;
            float h = cam.orthographicSize * 2.4f, w = h * Mathf.Max(.5f, cam.aspect);
            Vector3 p = cam.transform.position;
            DbpuFx.Place(sr, new Vector2(p.x, p.y), -.9f, w, h, 0f, DbpuFx.A(color, color.a * alpha));
        }
    }

    // Big centred messages ("ELIMINATED!", "YOUR WISH HAS BEEN GRANTED") in the mod's gold label style.
    public sealed class SingularityAnnouncer : MonoBehaviour
    {
        static SingularityAnnouncer instance;
        public static Transform Anchor { get { Ensure(); return instance.transform; } }
        const int Key = 0x5149;

        static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU announcer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SingularityAnnouncer>();
            instance.Follow();
        }

        public static void Show(string title, string subtitle, float seconds, Color? color = null)
        {
            Ensure();
            DbpuWorldLabel.Show(Key, instance.transform, title, subtitle, seconds, -1f, color, null);
        }

        void LateUpdate() { Follow(); }

        void Follow()
        {
            var cam = Global.main ? Global.main.camera : Camera.main;
            if (!cam) return;
            // The caption draws .95 units above its anchor; land it around 70% screen height.
            Vector3 p = cam.transform.position;
            transform.position = new Vector3(p.x, p.y + cam.orthographicSize * .42f - .95f, 0f);
        }
    }
}
