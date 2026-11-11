using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DBPU
{
    // Procedural textures, materials and small render helpers shared by the anime-style
    // techniques (Ki Fist, Kienzan, Hakai, Hakai Ball, Ki Shield). Everything is generated
    // once at runtime, so no extra asset bundle is required.
    public static class DbpuFx
    {
        static Material softSprite, bright;
        static readonly Dictionary<Texture2D, Material> meshMaterials = new Dictionary<Texture2D, Material>();
        static Texture2D glow, ring, ringThick, star, streak, square, shard, cross, sphere, swirl, ribbon, spark;
        static Texture2D[] discFrames;
        static readonly Dictionary<Texture2D, Sprite> sprites = new Dictionary<Texture2D, Sprite>();
        static readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();

        public static bool Paused { get { return Global.main && Global.main.Paused; } }
        public static float Dt { get { return Paused ? 0f : Mathf.Clamp(Time.deltaTime, 0f, .1f); } }

        public static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        public static float Hash(int n) { return Mathf.Repeat(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f, 1f); }
        public static Color A(Color c, float alpha) { c.a = Mathf.Clamp01(alpha); return c; }
        public static Vector2 Rotate(Vector2 v, float radians) { float c = Mathf.Cos(radians), s = Mathf.Sin(radians); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        public static Vector2 MouseWorld()
        {
            var camera = Global.main ? Global.main.camera : Camera.main;
            return camera ? (Vector2)camera.ScreenToWorldPoint(Input.mousePosition) : Vector2.zero;
        }

        static Texture2D Make(string name, int w, int h, Func<float, float, Color> f, bool mip = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mip);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f, v = (y + .5f) / h * 2f - 1f;
                    px[y * w + x] = f(u, v);
                }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            tex.name = "DBPU FX " + name; tex.hideFlags = HideFlags.DontSave;
            tex.Apply(mip, false);
            return tex;
        }

        public static Texture2D Glow
        {
            get
            {
                if (!glow) glow = Make("glow", 64, 64, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Exp(-r * r * 4.2f) * (1f - Smooth(.82f, 1f, r));
                    return new Color(1, 1, 1, a);
                });
                return glow;
            }
        }
        public static Texture2D Spark
        {
            get
            {
                if (!spark) spark = Make("spark", 32, 32, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Exp(-r * r * 9f) + Mathf.Exp(-r * r * 2.2f) * .25f;
                    return new Color(1, 1, 1, Mathf.Clamp01(a) * (1f - Smooth(.85f, 1f, r)));
                });
                return spark;
            }
        }
        public static Texture2D Ring
        {
            get
            {
                if (!ring) ring = Make("ring", 128, 128, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v), d = (r - .86f) / .055f;
                    return new Color(1, 1, 1, Mathf.Exp(-d * d) * (1f - Smooth(.97f, 1f, r)));
                });
                return ring;
            }
        }
        public static Texture2D RingThick
        {
            get
            {
                if (!ringThick) ringThick = Make("ring thick", 128, 128, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v), d = (r - .74f) / .17f, e = (r - .9f) / .035f;
                    return new Color(1, 1, 1, Mathf.Clamp01(Mathf.Exp(-d * d) * .65f + Mathf.Exp(-e * e) * .55f) * (1f - Smooth(.97f, 1f, r)));
                });
                return ringThick;
            }
        }
        public static Texture2D Star
        {
            get
            {
                if (!star) star = Make("sparkle", 64, 64, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v), au = Mathf.Abs(u), av = Mathf.Abs(v);
                    float arms = Mathf.Exp(-au * 16f) * Mathf.Exp(-av * 2.9f) + Mathf.Exp(-av * 16f) * Mathf.Exp(-au * 2.9f);
                    float core = Mathf.Exp(-r * r * 22f), halo = Mathf.Exp(-r * r * 5f) * .28f;
                    float a = Mathf.Clamp01(arms * .95f + core + halo) * (1f - Smooth(.9f, 1f, Mathf.Max(au, av)));
                    return new Color(1, 1, 1, a);
                });
                return star;
            }
        }
        public static Texture2D Streak
        {
            get
            {
                if (!streak) streak = Make("streak", 128, 16, (u, v) =>
                {
                    float along = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.15f);
                    float across = Mathf.Exp(-(v / .42f) * (v / .42f));
                    return new Color(1, 1, 1, along * across);
                });
                return streak;
            }
        }
        public static Texture2D Ribbon
        {
            get
            {
                if (!ribbon) ribbon = Make("ribbon", 8, 64, (u, v) =>
                {
                    float core = Mathf.Exp(-(v / .16f) * (v / .16f)), soft = Mathf.Exp(-(v / .55f) * (v / .55f)) * .45f;
                    return new Color(1, 1, 1, Mathf.Clamp01(core + soft) * (1f - Smooth(.92f, 1f, Mathf.Abs(v))));
                }, false);
                return ribbon;
            }
        }
        public static Texture2D Square
        {
            get
            {
                if (!square) square = Make("square", 32, 32, (u, v) =>
                {
                    float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    return new Color(1, 1, 1, 1f - Smooth(.58f, .82f, m));
                });
                return square;
            }
        }
        public static Texture2D Shard
        {
            get
            {
                if (!shard) shard = Make("shard", 64, 64, (u, v) =>
                {
                    // Soft-edged irregular triangle: a glass fragment.
                    Vector2 p = new Vector2(u, v), a = new Vector2(-.05f, .95f), b = new Vector2(-.85f, -.72f), c = new Vector2(.9f, -.5f);
                    float d = Mathf.Min(Edge(p, a, b), Mathf.Min(Edge(p, b, c), Edge(p, c, a)));
                    float alpha = Smooth(0f, .07f, d);
                    float sheen = .78f + .22f * Mathf.Clamp01(1f - d * 4f);
                    return new Color(sheen, sheen, 1f, alpha);
                });
                return shard;
            }
        }
        static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a, n = new Vector2(e.y, -e.x).normalized;
            return Vector2.Dot(p - a, n) * -1f;
        }
        public static Texture2D Cross
        {
            get
            {
                if (!cross) cross = Make("hakai x", 64, 64, (u, v) =>
                {
                    float d = Mathf.Min(Mathf.Abs(u - v), Mathf.Abs(u + v)) * .7071f;
                    float reach = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    float inside = 1f - Smooth(.70f, .80f, reach);
                    float core = 1f - Smooth(.10f, .14f, d), outline = 1f - Smooth(.17f, .21f, d);
                    Color col = Color.Lerp(new Color(.22f, 0f, .03f), new Color(1f, .12f, .18f), core);
                    return new Color(col.r, col.g, col.b, outline * inside);
                });
                return cross;
            }
        }
        public static Texture2D Sphere
        {
            get
            {
                if (!sphere) sphere = Make("hakai sphere", 192, 192, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r >= 1f) return new Color(0, 0, 0, 0);
                    Color pink = new Color(1f, .62f, 1f), purple = new Color(.66f, .2f, .86f), dark = new Color(.10f, .015f, .16f), rim = new Color(.9f, .55f, 1f);
                    float light = Mathf.Clamp01(.5f + .35f * (-u * .4f + v * .6f));
                    Color col = Color.Lerp(pink, purple, Smooth(0f, .78f, r));
                    col = Color.Lerp(col, Color.Lerp(purple, pink, light), .25f * (1f - r));
                    col = Color.Lerp(col, dark, Smooth(.70f, .94f, r));
                    float rr = (r - .968f) / .02f;
                    col += rim * Mathf.Exp(-rr * rr) * .9f;
                    float alpha = Mathf.Lerp(.50f, .96f, Smooth(.25f, .93f, r)) * (1f - Smooth(.988f, 1f, r));
                    return new Color(Mathf.Clamp01(col.r), Mathf.Clamp01(col.g), Mathf.Clamp01(col.b), alpha);
                });
                return sphere;
            }
        }
        public static Texture2D Swirl
        {
            get
            {
                if (!swirl) swirl = Make("swirl", 128, 128, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                    float band = Mathf.Exp(-Mathf.Pow((r - .58f - .12f * Mathf.Sin(ang * 2f)) / .11f, 2f));
                    float arc = Mathf.Clamp01(Mathf.Sin(ang + .4f));
                    return new Color(1, 1, 1, band * arc * arc * (1f - Smooth(.92f, 1f, r)));
                });
                return swirl;
            }
        }
        // Destructo disc seen almost edge-on: a serrated golden ellipse. Three tooth phases
        // flip quickly to sell the buzz-saw rotation.
        public static Texture2D DiscFrame(int frame)
        {
            if (discFrames == null)
            {
                discFrames = new Texture2D[3];
                for (int i = 0; i < 3; i++)
                {
                    float phase = i * 2.0944f;
                    discFrames[i] = Make("kienzan " + i, 160, 40, (u, v) =>
                    {
                        float e = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                        float tooth = Mathf.Pow(.5f + .5f * Mathf.Sin(ang * 26f + phase), 3f);
                        float edge = 1f - .13f * tooth;
                        float alpha = 1f - Smooth(edge - .07f, edge, e);
                        Color c = Color.Lerp(new Color(1f, 1f, .9f), new Color(1f, .88f, .28f), Smooth(.0f, .55f, e));
                        c = Color.Lerp(c, new Color(1f, .55f, .08f), Smooth(.6f, .98f, e));
                        float line = Mathf.Exp(-(v / .2f) * (v / .2f)) * (1f - Smooth(.55f, .95f, Mathf.Abs(u)));
                        c = Color.Lerp(c, Color.white, line * .85f);
                        return new Color(c.r, c.g, c.b, alpha);
                    });
                }
            }
            return discFrames[((frame % 3) + 3) % 3];
        }

        public static Sprite SpriteOf(Texture2D tex)
        {
            Sprite s;
            if (sprites.TryGetValue(tex, out s) && s) return s;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), tex.width);
            s.name = tex.name; s.hideFlags = HideFlags.DontSave; sprites[tex] = s;
            return s;
        }
        public static Material SoftSprite
        {
            get
            {
                if (!softSprite)
                {
                    softSprite = new Material(Shader.Find("Sprites/Default")) { name = "DBPU FX soft sprite" };
                    softSprite.mainTexture = Texture2D.whiteTexture;
                }
                return softSprite;
            }
        }
        public static Material Bright
        {
            get
            {
                if (!bright)
                {
                    Material source = null;
                    try { source = ModAPI.FindMaterial("VeryBright"); } catch { source = null; }
                    bright = source ? source : SoftSprite;
                }
                return bright;
            }
        }
        public static Material MeshMaterial(Texture2D tex)
        {
            Material m;
            if (meshMaterials.TryGetValue(tex, out m) && m) return m;
            m = new Material(Shader.Find("Sprites/Default")) { name = "DBPU FX mesh " + tex.name };
            m.mainTexture = tex; meshMaterials[tex] = m;
            return m;
        }

        public static SpriteRenderer MakeSprite(Transform parent, string name, Texture2D tex, int order, bool emissive = false, string layer = "Top")
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteOf(tex);
            sr.sharedMaterial = emissive ? Bright : SoftSprite;
            sr.sortingLayerName = layer; sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            return sr;
        }
        public static LineRenderer Line(Transform parent, string name, int order, Texture2D tex = null, string layer = "Top")
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = tex ? MeshMaterial(tex) : SoftSprite;
            line.textureMode = LineTextureMode.Stretch;
            line.useWorldSpace = true; line.positionCount = 0;
            line.numCapVertices = 2; line.numCornerVertices = 1;
            line.sortingLayerName = layer; line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        // Keeps a child in world units whatever the parent scale/flip is.
        public static void WorldScale(Transform t, Vector3 scale)
        {
            Vector3 inherited = t.parent ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(scale.x / Mathf.Max(.0001f, Mathf.Abs(inherited.x)), scale.y / Mathf.Max(.0001f, Mathf.Abs(inherited.y)), 1f);
        }
        public static void Place(SpriteRenderer sr, Vector2 position, float z, float sizeX, float sizeY, float angleDegrees, Color color)
        {
            if (!sr) return;
            var t = sr.transform; t.position = new Vector3(position.x, position.y, z);
            t.rotation = Quaternion.Euler(0, 0, angleDegrees);
            WorldScale(t, new Vector3(sizeX, sizeY, 1));
            sr.color = color; sr.enabled = color.a > .002f && sizeX > .0001f && sizeY > .0001f;
        }

        public static void Shake(Vector2 at, float intensity)
        {
            try { if (CameraShakeBehaviour.main) CameraShakeBehaviour.main.Shake(intensity, at, 1f); } catch { }
        }
        // ModAPI path functions only work during OnLoad/Main/AfterSpawn, so every clip
        // is loaded up front; later misses fall back to the absolute mod path.
        static string root;
        static readonly string[] SoundPaths =
        {
            "Assets/SFX/Techniques/Hakai/Cast.wav",
            "Assets/SFX/Techniques/Hakai/Dissolve.wav",
            "Assets/SFX/Techniques/Hakai/Resist.wav",
            "Assets/SFX/Techniques/HakaiBall/Charge.wav",
            "Assets/SFX/Techniques/HakaiBall/Clash.wav",
            "Assets/SFX/Techniques/HakaiBall/Erase.wav",
            "Assets/SFX/Techniques/HakaiBall/Fire.wav",
            "Assets/SFX/Techniques/HakaiBall/Hum.wav",
            "Assets/SFX/Techniques/HakaiBall/Mark.wav",
            "Assets/SFX/Techniques/HakaiBall/Seal.wav",
            "Assets/SFX/Techniques/HakaiBall/Shatter.wav",
            "Assets/SFX/Techniques/KiFist/Charge.wav",
            "Assets/SFX/Techniques/KiFist/HitHeavy.wav",
            "Assets/SFX/Techniques/KiFist/HitLight.wav",
            "Assets/SFX/Techniques/KiFist/HitMedium.wav",
            "Assets/SFX/Techniques/KiFist/Off.wav",
            "Assets/SFX/Techniques/KiFist/On.wav",
            "Assets/SFX/Techniques/KiFist/Ready.wav",
            "Assets/SFX/Techniques/KiShield/Break.wav",
            "Assets/SFX/Techniques/KiShield/Hit.wav",
            "Assets/SFX/Techniques/KiShield/Lower.wav",
            "Assets/SFX/Techniques/KiShield/Raise.wav",
            "Assets/SFX/Techniques/Kienzan/Charge.wav",
            "Assets/SFX/Techniques/Kienzan/Clang.wav",
            "Assets/SFX/Techniques/Kienzan/Fly.wav",
            "Assets/SFX/Techniques/Kienzan/Slice.wav",
            "Assets/SFX/Techniques/Kienzan/Throw.wav"
        };
        public static void Preload(string modDirectory)
        {
            root = modDirectory;
            foreach (var path in SoundPaths)
            {
                AudioClip existing;
                if (sounds.TryGetValue(path, out existing) && existing) continue;
                try { sounds[path] = ModAPI.LoadSound(path); }
                catch (Exception ex) { Debug.LogWarning("[DBPU FX] Sound " + path + ": " + ex.Message); }
            }
        }
        // Extra clips for Singularity modules; call during OnLoad so ModAPI.LoadSound accepts the path.
        public static void PreloadExtra(params string[] paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                AudioClip existing;
                if (sounds.TryGetValue(path, out existing) && existing) continue;
                try { sounds[path] = ModAPI.LoadSound(path); }
                catch (Exception ex) { Debug.LogWarning("[DBPU FX] Sound " + path + ": " + ex.Message); }
            }
        }
        public static AudioClip Clip(string relative)
        {
            AudioClip clip;
            if (sounds.TryGetValue(relative, out clip)) return clip;
            clip = null;
            if (!string.IsNullOrEmpty(root))
                try { clip = global::Utils.FileToAudioClip(System.IO.Path.Combine(root, relative)); } catch (Exception ex) { Debug.LogWarning("[DBPU FX] Sound " + relative + ": " + ex.Message); }
            sounds[relative] = clip; return clip;
        }
        public static GameObject Sound(Vector2 at, string relative, float volume = 1f, Transform follow = null, bool loop = false)
        {
            var clip = Clip(relative);
            if (!clip) return null;
            try { return MCP_Utils.PlayAudio(new Vector3(at.x, at.y, 0), clip, follow, !loop, Mathf.Clamp01(volume) * Mathf.Clamp(DBPU.PowersVolume, 0f, 2f), loop, 1f, 1000f, 0f, "DBPU technique sound"); }
            catch (Exception ex) { Debug.LogWarning("[DBPU FX] " + ex.Message); return null; }
        }
        public static Vector2 Centre(PersonBehaviour person)
        {
            if (!person) return Vector2.zero;
            var torso = person.transform.Find("Body/MiddleBody");
            if (torso && torso.gameObject.activeInHierarchy) return torso.position;
            var upper = person.transform.Find("Body/UpperBody");
            return upper ? (Vector2)upper.position : (Vector2)person.transform.position;
        }
        public static Bounds BodyBounds(PersonBehaviour person)
        {
            var bounds = new Bounds(Centre(person), Vector3.one * .5f); bool found = false;
            if (!person || person.Limbs == null) return bounds;
            foreach (var limb in person.Limbs)
            {
                if (!limb || !limb.gameObject.activeInHierarchy) continue;
                var sr = limb.GetComponent<SpriteRenderer>(); if (!sr || !sr.enabled) continue;
                if (!found) { bounds = sr.bounds; found = true; } else bounds.Encapsulate(sr.bounds);
            }
            return bounds;
        }
        public static double PowerOf(PersonBehaviour person)
        {
            if (!person || !person.GetComponent<KiSystem>()) return 1d;
            var power = CombatPowerController.Bind(person);
            if (!power) return 1d;
            try { power.Refresh(); } catch { }
            double value = power.PowerLevel;
            return double.IsNaN(value) || value <= 0d ? 1d : value;
        }
        public static bool IsKiCharging(PersonBehaviour person)
        {
            var charge = person ? person.GetComponent<KiChargeModeController>() : null;
            return charge && charge.IsActivelyCharging;
        }
    }

    public struct DbpuPalette
    {
        public Color Deep, Mid, Hot;
        public Color Accent { get { return Color.Lerp(Color.Lerp(Deep, Mid, .5f), Color.white, .3f); } }
        public Color Vivid
        {
            get
            {
                Color c = Color.Lerp(Mid, Hot, .18f); float max = Mathf.Max(.0001f, c.maxColorComponent);
                c = new Color(c.r / max, c.g / max, c.b / max, 1f);
                return Color.Lerp(c, Mid, .35f);
            }
        }
        public static DbpuPalette From(AuraLook look) { return new DbpuPalette { Deep = look.Deep, Mid = look.Mid, Hot = look.Light }; }
        public static DbpuPalette From(Color primary)
        {
            primary.a = 1f;
            return new DbpuPalette { Deep = Color.Lerp(Color.black, primary, .45f), Mid = primary, Hot = Color.Lerp(primary, Color.white, .62f) };
        }
    }

    // The current ki colour of a fighter, read from the same sources the aura uses:
    // the spawned aura palette, custom transformation colours, the form's aura id,
    // a forced ki colour and Kaioken. Cached briefly because shields query it per frame.
    public static class DbpuAuraPalette
    {
        sealed class Entry { public DbpuPalette Value; public float Until; public string Key; }
        static readonly Dictionary<int, Entry> cache = new Dictionary<int, Entry>();

        public static DbpuPalette Get(PersonBehaviour person)
        {
            if (!person) return DbpuPalette.From(RefinedAuras.Look("Base"));
            Entry entry;
            int id = person.GetInstanceID();
            if (cache.TryGetValue(id, out entry) && Time.unscaledTime < entry.Until) return entry.Value;
            if (entry == null) { entry = new Entry(); cache[id] = entry; }
            entry.Value = Resolve(person, out entry.Key); entry.Until = Time.unscaledTime + .2f;
            if (cache.Count > 256) cache.Clear();
            return entry.Value;
        }
        // Changes whenever the visible ki identity changes (form, aura, Kaioken).
        public static string Signature(PersonBehaviour person)
        {
            if (!person) return "";
            Get(person); Entry entry; return cache.TryGetValue(person.GetInstanceID(), out entry) ? entry.Key : "";
        }
        static DbpuPalette Resolve(PersonBehaviour person, out string key)
        {
            var handler = person.GetComponent<AuraHandler>();
            var ki = person.GetComponent<KiSystem>();
            string form = ki ? ki.CurrentForm : "Base";
            DbpuPalette result; bool found = false;
            result = DbpuPalette.From(RefinedAuras.Look("Base"));
            string auraId = handler && !string.IsNullOrEmpty(handler.CurrentAuraID) ? handler.CurrentAuraID : "Base";
            try { auraId = CinematicAssets.ResolveAura(person, form, auraId); } catch { }
            // 1. The aura that is actually on screen (includes custom tints).
            var spawned = handler ? handler.CurrentAuraObject : null;
            if (spawned)
            {
                var refined = spawned.GetComponent<RefinedAuraVisual>();
                if (refined && refined.TargetLook != null) { result = DbpuPalette.From(refined.TargetLook); found = true; }
                else
                {
                    foreach (var ps in spawned.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (!ps || ps.name.IndexOf("lightning", StringComparison.OrdinalIgnoreCase) >= 0 || ps.name.IndexOf("spark", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        Color tint = ps.main.startColor.Evaluate(.5f);
                        if (tint.maxColorComponent > .02f) { result = DbpuPalette.From(new Color(tint.r, tint.g, tint.b, 1)); found = true; break; }
                    }
                }
            }
            // 2. Custom transformation colours.
            if (!found && ki)
            {
                try
                {
                    var identity = person.GetComponent<DbpuCharacterIdentity>();
                    var dto = identity ? CustomTransformationCatalog.GetForm(identity.CharacterId, form) : null;
                    if (dto != null && dto.UseCustomAura)
                    {
                        Color primary = new Color(dto.AuraColorR, dto.AuraColorG, dto.AuraColorB, 1f);
                        Color secondary = dto.UseAuraSecondaryColor ? new Color(dto.AuraColor2R, dto.AuraColor2G, dto.AuraColor2B, 1f) : Color.Lerp(primary, Color.white, .35f);
                        result = new DbpuPalette { Deep = primary * .45f, Mid = primary, Hot = secondary }; result.Deep.a = 1; found = true;
                    }
                }
                catch { }
            }
            // 3. A forced ki colour, then the form's aura profile.
            if (!found && ki && ki.requiredColor.a > .01f && ki.requiredColor.maxColorComponent > .01f) { result = DbpuPalette.From(ki.requiredColor); found = true; }
            if (!found && RefinedAuras.Contains(auraId)) { result = DbpuPalette.From(RefinedAuras.Look(auraId)); found = true; }
            var kaioken = person.GetComponent<Kaioken>();
            bool kaio = kaioken && kaioken.Active;
            if (kaio)
            {
                // Base Kaioken is red; over a transformation the form colour stays and Kaioken
                // becomes the crimson depth (no muddy purple mix of blue and red).
                var red = DbpuPalette.From(RefinedAuras.Look("Kaioken"));
                if (CinematicAssets.KeepsInnerAura(form)) { result.Deep = red.Deep; result.Mid = Color.Lerp(result.Mid, red.Mid, .15f); }
                else result = red;
            }
            key = form + "|" + auraId + "|" + (kaio ? "K" : "") + "|" + ColorUtility.ToHtmlStringRGB(result.Mid);
            return result;
        }
    }

    // Custom lightweight particle field: one dynamic mesh per effect, no per-particle objects.
    [DefaultExecutionOrder(330)]
    public sealed class DbpuParticles : MonoBehaviour
    {
        struct P
        {
            public Vector2 Pos, Vel; public float Life, Max, Size, EndSize, Rot, Spin, Drag, Gravity, Twinkle, Seed;
            public Color C0, C1; public bool Stretch;
        }
        P[] items; int count; Mesh mesh; MeshRenderer meshRenderer;
        Vector3[] vertices; Color[] colors; Vector2[] uvs; float clock;
        public Vector2 Wind; public float Turbulence; public bool AutoDestroy; public float StretchFactor = .06f;
        bool emitted;
        public int Count { get { return count; } }
        // Destroy once every particle has finished (or immediately if none are alive).
        public void Release() { AutoDestroy = true; emitted = true; }

        public static DbpuParticles Create(string name, Texture2D texture, int capacity, int order, string layer = "Top")
        {
            var go = new GameObject(name);
            var field = go.AddComponent<DbpuParticles>(); field.Setup(texture, Mathf.Clamp(capacity, 8, 2048), order, layer);
            return field;
        }
        void Setup(Texture2D texture, int capacity, int order, string layer)
        {
            items = new P[capacity]; vertices = new Vector3[capacity * 4]; colors = new Color[capacity * 4]; uvs = new Vector2[capacity * 4];
            var triangles = new int[capacity * 6];
            for (int i = 0; i < capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2; triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(0, 1); uvs[v + 2] = new Vector2(1, 1); uvs[v + 3] = new Vector2(1, 0);
            }
            mesh = new Mesh { name = "DBPU particle field" }; mesh.MarkDynamic();
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uvs; mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = DbpuFx.MeshMaterial(texture);
            meshRenderer.sortingLayerName = layer; meshRenderer.sortingOrder = order;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off; meshRenderer.receiveShadows = false;
        }
        public void Emit(Vector2 position, Vector2 velocity, float life, float size, Color start, Color end,
            float drag = 0f, float gravity = 0f, float spin = 0f, bool stretch = false, float endSize = -1f, float twinkle = 0f)
        {
            if (items == null || life <= 0f) return;
            if (count >= items.Length)
            {
                // Replace the oldest particle rather than dropping the newest burst.
                int oldest = 0; float best = float.MaxValue;
                for (int i = 0; i < count; i++) { float left = items[i].Max - items[i].Life; if (left < best) { best = left; oldest = i; } }
                items[oldest] = items[count - 1]; count--;
            }
            items[count++] = new P
            {
                Pos = position, Vel = velocity, Life = 0, Max = life, Size = size, EndSize = endSize < 0 ? size : endSize,
                Rot = UnityEngine.Random.value * 360f, Spin = spin, Drag = drag, Gravity = gravity, C0 = start, C1 = end,
                Stretch = stretch, Twinkle = twinkle, Seed = UnityEngine.Random.value * 100f
            };
            emitted = true;
        }
        void LateUpdate()
        {
            if (items == null) return;
            float dt = DbpuFx.Dt; clock += dt;
            for (int i = count - 1; i >= 0; i--)
            {
                var p = items[i];
                p.Life += dt;
                if (p.Life >= p.Max) { items[i] = items[count - 1]; count--; continue; }
                if (dt > 0f)
                {
                    p.Vel += (Wind + Vector2.down * p.Gravity) * dt;
                    if (Turbulence > 0f)
                        p.Vel += new Vector2(Mathf.PerlinNoise(p.Seed, clock * 1.3f) - .5f, Mathf.PerlinNoise(clock * 1.3f, p.Seed) - .5f) * (Turbulence * dt * 6f);
                    if (p.Drag > 0f) p.Vel *= Mathf.Exp(-p.Drag * dt);
                    p.Pos += p.Vel * dt; p.Rot += p.Spin * dt;
                }
                items[i] = p;
            }
            for (int i = 0; i < items.Length; i++)
            {
                int v = i * 4;
                if (i >= count) { vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = Vector3.zero; continue; }
                var p = items[i]; float t = p.Life / p.Max;
                float size = Mathf.Lerp(p.Size, p.EndSize, t);
                if (p.Twinkle > 0f) size *= 1f - p.Twinkle * .5f + p.Twinkle * .5f * Mathf.Sin(clock * 14f + p.Seed * 9f);
                Color c = Color.Lerp(p.C0, p.C1, t);
                float fadeIn = Mathf.Clamp01(p.Life / Mathf.Max(.02f, p.Max * .12f));
                c.a *= fadeIn * (1f - DbpuFx.Smooth(.6f, 1f, t));
                Vector2 right, up;
                if (p.Stretch && p.Vel.sqrMagnitude > .0001f)
                {
                    Vector2 dir = p.Vel.normalized; float length = size + p.Vel.magnitude * StretchFactor;
                    right = dir * length * .5f; up = new Vector2(-dir.y, dir.x) * size * .22f;
                }
                else
                {
                    float r = p.Rot * Mathf.Deg2Rad; right = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * size * .5f; up = new Vector2(-right.y, right.x);
                }
                vertices[v] = p.Pos - right - up; vertices[v + 1] = p.Pos - right + up; vertices[v + 2] = p.Pos + right + up; vertices[v + 3] = p.Pos + right - up;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;
            }
            mesh.vertices = vertices; mesh.colors = colors;
            mesh.bounds = new Bounds(transform.InverseTransformPoint(Vector3.zero), Vector3.one * 20000f);
            meshRenderer.enabled = count > 0;
            if (AutoDestroy && emitted && count == 0) Destroy(gameObject);
        }
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }

    // Floating technique captions in the style "KI FIST / ON · SWING HARD" above a fighter,
    // with an optional progress bar (Hakai resistance, Hakai Ball break-out).
    [DefaultExecutionOrder(340)]
    public sealed class DbpuWorldLabel : MonoBehaviour
    {
        sealed class Item
        {
            public RectTransform Root; public CanvasGroup Group; public TextMeshProUGUI Title, TitleShadow, Sub, SubShadow;
            public Image BarBack, BarFill; public Transform Anchor; public float Until, Shown, Bar = -1; public bool Persistent; public float Lift;
        }
        static DbpuWorldLabel instance;
        static TMP_FontAsset font;
        readonly Dictionary<int, Item> items = new Dictionary<int, Item>();
        readonly List<int> stale = new List<int>();
        Canvas canvas;
        public static readonly Color Gold = new Color(.86f, .74f, .38f, 1f);
        public static readonly Color Subtle = new Color(.80f, .82f, .88f, 1f);

        static DbpuWorldLabel Ensure()
        {
            if (instance) return instance;
            var go = new GameObject("DBPU technique captions", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DbpuWorldLabel>();
            instance.canvas = go.GetComponent<Canvas>(); instance.canvas.renderMode = RenderMode.ScreenSpaceOverlay; instance.canvas.sortingOrder = 31990;
            return instance;
        }
        static TMP_FontAsset Font()
        {
            if (font) return font;
            try { font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font; } catch { font = null; }
            if (!font) font = TMP_Settings.defaultFontAsset;
            return font;
        }
        public static int KeyOf(UnityEngine.Object owner) { return owner ? owner.GetInstanceID() : 0; }
        public static Transform HeadOf(PersonBehaviour person)
        {
            if (!person) return null;
            var head = person.transform.Find("Head");
            return head ? head : person.transform;
        }
        public static void Show(PersonBehaviour person, string title, string subtitle, float seconds, float bar = -1f, Color? titleColor = null, Color? barColor = null)
        {
            if (!person) return;
            Show(KeyOf(person), HeadOf(person), title, subtitle, seconds, bar, titleColor, barColor);
        }
        public static void Show(int key, Transform anchor, string title, string subtitle, float seconds, float bar = -1f, Color? titleColor = null, Color? barColor = null)
        {
            if (!anchor) return;
            var self = Ensure(); Item item;
            if (!self.items.TryGetValue(key, out item) || !item.Root) { item = self.Build(); self.items[key] = item; }
            item.Anchor = anchor; item.Persistent = seconds <= 0f; item.Until = Time.unscaledTime + Mathf.Max(.1f, seconds);
            item.Title.text = item.TitleShadow.text = string.IsNullOrEmpty(title) ? "" : title.ToUpperInvariant();
            item.Sub.text = item.SubShadow.text = string.IsNullOrEmpty(subtitle) ? "" : subtitle.ToUpperInvariant();
            item.Title.color = titleColor ?? Gold;
            item.Bar = bar;
            if (barColor.HasValue) item.BarFill.color = barColor.Value;
            item.BarBack.gameObject.SetActive(bar >= 0f);
        }
        sealed class Pending { public PersonBehaviour Person; public float At, Seconds; public string Title, Subtitle; }
        readonly List<Pending> pending = new List<Pending>();
        public static void ShowLater(PersonBehaviour person, float delay, string title, string subtitle, float seconds)
        {
            if (!person) return;
            Ensure().pending.Add(new Pending { Person = person, At = Time.unscaledTime + delay, Seconds = seconds, Title = title, Subtitle = subtitle });
        }
        public static void Hide(PersonBehaviour person) { if (person) Hide(KeyOf(person)); }
        public static void Hide(int key)
        {
            if (!instance) return; Item item;
            if (instance.items.TryGetValue(key, out item)) { item.Persistent = false; item.Until = Mathf.Min(item.Until, Time.unscaledTime); }
        }
        Item Build()
        {
            var item = new Item();
            var go = new GameObject("Caption", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            item.Root = go.GetComponent<RectTransform>(); item.Root.anchorMin = item.Root.anchorMax = Vector2.zero; item.Root.sizeDelta = new Vector2(720, 90);
            item.Group = go.GetComponent<CanvasGroup>(); item.Group.blocksRaycasts = false; item.Group.interactable = false; item.Group.alpha = 0;
            item.TitleShadow = Text(item.Root, "Title shadow", new Vector2(2, 20), 30, new Color(0, 0, 0, .78f));
            item.Title = Text(item.Root, "Title", new Vector2(0, 22), 30, Gold);
            item.SubShadow = Text(item.Root, "Subtitle shadow", new Vector2(1.5f, -6.5f), 16, new Color(0, 0, 0, .8f));
            item.Sub = Text(item.Root, "Subtitle", new Vector2(0, -5), 16, Subtle);
            item.BarBack = Panel(item.Root, "Bar", new Vector2(0, 52), new Vector2(150, 12), new Color(.02f, .01f, .03f, .9f));
            item.BarFill = Panel(item.BarBack.rectTransform, "Fill", Vector2.zero, new Vector2(146, 8), new Color(.78f, .55f, 1f, 1f));
            item.BarFill.rectTransform.anchorMin = item.BarFill.rectTransform.anchorMax = new Vector2(0, .5f);
            item.BarFill.rectTransform.pivot = new Vector2(0, .5f); item.BarFill.rectTransform.anchoredPosition = new Vector2(2, 0);
            item.BarBack.gameObject.SetActive(false);
            return item;
        }
        static TextMeshProUGUI Text(Transform parent, string name, Vector2 at, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = at; rt.sizeDelta = new Vector2(720, size * 1.5f);
            var t = go.GetComponent<TextMeshProUGUI>(); var f = Font(); if (f) t.font = f;
            t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.characterSpacing = 2f;
            return t;
        }
        static Image Panel(Transform parent, string name, Vector2 at, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = at; rt.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        void LateUpdate()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (!p.Person) { pending.RemoveAt(i); continue; }
                if (Time.unscaledTime < p.At) continue;
                pending.RemoveAt(i); Show(p.Person, p.Title, p.Subtitle, p.Seconds);
            }
            var camera = Global.main ? Global.main.camera : Camera.main;
            float scale = Mathf.Clamp(Screen.height / 1080f, .6f, 2.4f);
            stale.Clear();
            foreach (var pair in items)
            {
                var item = pair.Value;
                if (!item.Root || !item.Anchor || !item.Anchor.gameObject.activeInHierarchy) { stale.Add(pair.Key); continue; }
                bool alive = item.Persistent || Time.unscaledTime < item.Until;
                item.Shown = Mathf.MoveTowards(item.Shown, alive ? 1f : 0f, Time.unscaledDeltaTime * (alive ? 7f : 3.2f));
                if (!alive && item.Shown <= 0f) { stale.Add(pair.Key); continue; }
                item.Group.alpha = item.Shown;
                if (!camera) continue;
                Vector3 screen = camera.WorldToScreenPoint(item.Anchor.position + Vector3.up * .95f);
                item.Root.gameObject.SetActive(screen.z > 0f);
                item.Root.anchoredPosition = new Vector2(Mathf.Clamp(screen.x, 160 * scale, Screen.width - 160 * scale), Mathf.Clamp(screen.y + 34f * scale, 40, Screen.height - 40));
                item.Root.localScale = Vector3.one * scale;
                if (item.Bar >= 0f)
                {
                    var fill = item.BarFill.rectTransform; fill.sizeDelta = new Vector2(146f * Mathf.Clamp01(item.Bar), 8f);
                }
            }
            foreach (int key in stale)
            {
                Item item;
                if (items.TryGetValue(key, out item) && item.Root) Destroy(item.Root.gameObject);
                items.Remove(key);
            }
        }
    }

    // Severs limbs through the game's own dismemberment path (LimbBehaviour.Slice).
    // DBPU characters carry an enormous BreakingThreshold, so the joint is also opened
    // directly; root limbs without their own joint split at the nearest child joint.
    public static class DbpuDismemberment
    {
        public static bool CanCut(LimbBehaviour limb)
        {
            return limb && limb.Person && limb.gameObject.activeInHierarchy && Target(limb, limb.transform.position) != null;
        }
        static LimbBehaviour Target(LimbBehaviour limb, Vector2 point)
        {
            if (!limb) return null;
            if (limb.HasJoint && limb.Joint && limb.Joint.enabled) return limb;
            if (!limb.Person || limb.Person.Limbs == null) return null;
            var body = limb.GetComponent<Rigidbody2D>();
            LimbBehaviour best = null; float distance = float.MaxValue;
            foreach (var other in limb.Person.Limbs)
            {
                if (!other || other == limb || !other.HasJoint || !other.Joint || !other.Joint.enabled || other.Joint.connectedBody != body) continue;
                float d = ((Vector2)other.transform.TransformPoint(other.Joint.anchor) - point).sqrMagnitude;
                if (d < distance) { distance = d; best = other; }
            }
            return best;
        }
        public static bool Cut(LimbBehaviour limb, Vector2 point, Vector2 direction, bool ignoreImmunity)
        {
            var target = Target(limb, point);
            if (!target) return false;
            bool immune = target.ImmuneToDamage;
            if (immune && !ignoreImmunity) return false;
            try
            {
                if (immune) target.ImmuneToDamage = false;
                target.Slice();
            }
            catch (Exception ex) { Debug.LogWarning("[DBPU Dismemberment] " + ex.Message); }
            finally { if (immune) target.ImmuneToDamage = true; }
            target.BreakingThreshold = 0f;
            if (target.Joint) { target.Joint.breakForce = 0f; target.Joint.breakTorque = 0f; }
            if (target.SkinMaterialHandler)
                try { target.SkinMaterialHandler.AddDamagePoint(DamageType.Dismemberment, point, 6f); } catch { }
            var body = target.GetComponent<Rigidbody2D>();
            if (body && body.bodyType == RigidbodyType2D.Dynamic && direction.sqrMagnitude > .0001f)
                body.AddForce(direction.normalized * body.mass * 2.2f + Vector2.up * body.mass * .6f, ForceMode2D.Impulse);
            return true;
        }
        public static void Attach(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return;
            foreach (var limb in person.Limbs)
                if (limb && !limb.GetComponent<DbpuSliceableLimb>()) limb.gameObject.AddComponent<DbpuSliceableLimb>();
        }
    }

    // Lets blades (any object whose physical properties are Sharp) cut DBPU fighters,
    // whose native joint strength would otherwise make them uncuttable.
    public sealed class DbpuSliceableLimb : MonoBehaviour
    {
        LimbBehaviour limb; float cooldown;
        void Awake() { limb = GetComponent<LimbBehaviour>(); }
        void Update() { if (cooldown > 0f) cooldown -= Time.deltaTime; }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!DBPU.DismembermentEnabled || cooldown > 0f || !limb || !limb.Person || !collision.collider) return;
            var physical = collision.collider.GetComponentInParent<PhysicalBehaviour>();
            if (!physical || !physical.Properties || !physical.Properties.Sharp) return;
            if (physical.GetComponent<LimbBehaviour>()) return;
            float speed = collision.relativeVelocity.magnitude;
            var otherBody = collision.rigidbody;
            float mass = otherBody ? otherBody.mass : 1f;
            float impulse = 0f;
            for (int i = 0; i < collision.contactCount; i++) impulse += collision.GetContact(i).normalImpulse;
            // A deliberate swing: fast, or a heavy blade driven with force.
            bool hard = speed >= 7.5f || (speed >= 4.5f && (impulse >= 4f || mass >= 4f));
            if (!hard) return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            if (DbpuDismemberment.Cut(limb, point, -collision.relativeVelocity, false))
            {
                cooldown = .35f;
                DbpuSlashFx.Spawn(point, collision.relativeVelocity, new Color(1f, .95f, .9f), .7f);
            }
        }
    }

    // A short bright slash flash with sparks, used for every cut.
    public static class DbpuSlashFx
    {
        public static void Spawn(Vector2 point, Vector2 velocity, Color tint, float scale = 1f)
        {
            Vector2 dir = velocity.sqrMagnitude > .0001f ? velocity.normalized : Vector2.right;
            var sparks = DbpuParticles.Create("DBPU slash sparks", DbpuFx.Streak, 48, 46);
            sparks.AutoDestroy = true; sparks.StretchFactor = .05f;
            for (int i = 0; i < 18; i++)
            {
                Vector2 v = DbpuFx.Rotate(dir, UnityEngine.Random.Range(-.9f, .9f)) * UnityEngine.Random.Range(3f, 11f) * scale;
                if (i % 3 == 0) v = -v * .5f;
                sparks.Emit(point, v, UnityEngine.Random.Range(.12f, .32f), UnityEngine.Random.Range(.08f, .2f) * scale,
                    Color.Lerp(Color.white, tint, .3f), DbpuFx.A(tint, 0f), 4f, 3f, 0, true);
            }
            var flash = DbpuParticles.Create("DBPU slash line", DbpuFx.Streak, 4, 47);
            flash.AutoDestroy = true; flash.StretchFactor = 0f;
            flash.Emit(point, dir * .01f, .16f, 1.4f * scale, Color.white, DbpuFx.A(tint, 0f), 0, 0, 0, true, 2.2f * scale);
        }
    }
}
