using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Per-form values for the Super Saiyan aura (Assets/VFX/SaiyanAura/saiyan_aura, built by
    // _Pipeline/saiyan_flipbook.py (the lightning) and _Pipeline/build_saiyanaura.ps1).
    public sealed class SaiyanAuraProfile
    {
        // Size of the column of light relative to Super Saiyan 1, and extra height on top of that.
        public float Scale, Stretch;
        // Lightning passes: 0 none, 1 the reference's lightning, 2 a second pass, offset and mirrored.
        public int Bolts;
        // How far charging swells it, across and up.
        public Vector2 Swell;
        // How many particles rise through it, relative to Super Saiyan 1.
        public float Energy;
    }

    public static class SaiyanAuraAssets
    {
        static AssetBundle bundle;
        static bool attempted;
        static string directory;
        public static Material Flipbook;
        public static GameObject Core;
        // The user's SSJ1 aura material (SSJ1Aura.shader), the body of every Super Saiyan aura, and the SSJ2/3
        // lightning (SaiyanLightning.shader, drawn by SaiyanLightningBolts).
        public static Material UserAura, UserLightning;
        // Each lightning drawing's lines for _BoltPoly: x, y, half thickness, kind (0 starts a line).
        public static Vector4[][] BoltLines;
        public static int[] BoltCounts;

        // Reads saiyanaura_bolts.bytes: drawing count, then per drawing its point count and four numbers per point.
        public static void LoadBolts(byte[] data)
        {
            int at = 0;
            int drawings = BitConverter.ToInt32(data, at); at += 4;
            if (drawings != SaiyanAuraFrames.BoltFrames) throw new InvalidOperationException("Lightning lines do not match the atlas");
            BoltLines = new Vector4[drawings][];
            BoltCounts = new int[drawings];
            for (int d = 0; d < drawings; d++)
            {
                int count = BitConverter.ToInt32(data, at); at += 4;
                if (count < 2 || count > 256) throw new InvalidOperationException("Bad lightning drawing " + d);
                var entries = new Vector4[256];
                for (int k = 0; k < count; k++)
                {
                    entries[k] = new Vector4(BitConverter.ToSingle(data, at), BitConverter.ToSingle(data, at + 4), BitConverter.ToSingle(data, at + 8), BitConverter.ToSingle(data, at + 12));
                    at += 16;
                }
                BoltLines[d] = entries;
                BoltCounts[d] = count;
            }
        }

        public static readonly Dictionary<string, SaiyanAuraProfile> Profiles = new Dictionary<string, SaiyanAuraProfile>
        {
            { "SSJ1", new SaiyanAuraProfile { Scale = 1f, Stretch = 1f, Bolts = 0, Swell = new Vector2(.2f, .16f), Energy = 1f } },
            { "SSJ2", new SaiyanAuraProfile { Scale = 1.07f, Stretch = 1.03f, Bolts = 1, Swell = new Vector2(.22f, .18f), Energy = 1.25f } },
            { "SSJ3", new SaiyanAuraProfile { Scale = 1.15f, Stretch = 1.1f, Bolts = 2, Swell = new Vector2(.24f, .2f), Energy = 1.5f } },
        };

        public static void Initialize(string path) { directory = path; }
        public static bool Handles(string id) { return id != null && Profiles.ContainsKey(id); }

        public static bool Prepare()
        {
            if (Flipbook) return true;
            if (attempted) return false;
            attempted = true;
            try
            {
                bundle = AssetBundle.LoadFromFile(Path.Combine(directory, "Assets/VFX/SaiyanAura/saiyan_aura"));
                if (!bundle) foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles()) if (loaded.Contains("saiyanaura_flipbook")) { bundle = loaded; break; }
                if (!bundle) throw new InvalidOperationException("AssetBundle not loaded");
                Flipbook = bundle.LoadAsset<Material>("saiyanaura_flipbook");
                Core = bundle.LoadAsset<GameObject>("saiyanaura_core");
                var bolts = bundle.LoadAsset<TextAsset>("saiyanaura_bolts");
                if (!Flipbook || !bolts) throw new InvalidOperationException("Material or lightning missing");
                if (!Flipbook.shader.isSupported) throw new InvalidOperationException("Unsupported GPU shader");
                if (!Flipbook.GetTexture("_Bolts")) throw new InvalidOperationException("Lightning atlas missing");
                LoadBolts(bolts.bytes);
                var user = bundle.LoadAsset<Material>("ssj1aura");
                if (!user || !user.shader.isSupported) throw new InvalidOperationException("SSJ1 aura material missing or unsupported");
                UserAura = user;
                var lightning = bundle.LoadAsset<Material>("saiyanlightning");
                if (lightning && lightning.shader.isSupported) UserLightning = lightning;
                else Debug.LogWarning("[DBPU SaiyanAura] SSJ2/3 lightning material missing or unsupported; the aura plays without it.");
                if (!Core) Debug.LogWarning("[DBPU SaiyanAura] Energy particles missing; the aura plays without them.");
                return true;
            }
            catch (Exception ex)
            {
                Flipbook = null; BoltLines = null;
                Debug.LogWarning("[DBPU SaiyanAura] Previous aura retained: " + ex.Message);
                return false;
            }
        }

        public static GameObject Create(string id, bool transition)
        {
            if (!Handles(id) || !Prepare()) return null;
            return new GameObject((transition ? "Saiyan transformation veil/" : "SaiyanAura/") + id);
        }
    }

    // Plays one Super Saiyan aura: a column of light behind the fighter that rises from the ground and boils at
    // its edges, smooth at the game's frame rate; rising particles of ki; the flash over the fighter when it
    // ignites; and for SSJ2/3 the reference's lightning. Depends on UnityEngine only, so the preview project
    // can render it without the game.
    public sealed class SaiyanAuraRig : MonoBehaviour
    {
        // The fighter in the rig's local space as RefinedAuraVisual.FeetRoot places it (the same root and the
        // same in-game measurement as BlueAuraRig): soles at -1.4, 1.86 tall, MiddleBody 1.35 above the soles
        // per unit of the fighter's scale.
        public const float Feet = -1.4f, Height = 1.86f, MiddleAboveFeet = 1.35f;
        // The lightning drawings' frame: their glowing oval (BodyBox) spans this many rig units, its bottom just
        // under the soles. The column is laid out in the same frame pixels.
        const float BodyHeight = 2.3f;
        const float Foot = Feet - .06f;
        // The column: half width in frame pixels, and how far its quad reaches past the frame (it rises well
        // above the fighter and its soft edges spread wide).
        const float ColumnHalfWidth = 84f, ExtendSide = .35f, ExtendTop = .1f;
        // The column is as tall as the fighter: it fades out over this much (rig units) above the head.
        const float ColumnOverHead = .45f;
        // Lightning line thickness relative to the strokes of the reference.
        const float BoltThickness = .9f;
        sealed class Layer { public MeshRenderer Renderer; public MaterialPropertyBlock Block = new MaterialPropertyBlock(); public float Mode; public int Pass; }
        struct Emitter { public ParticleSystem System; public ParticleSystemRenderer Renderer; public float Rate; public MaterialPropertyBlock Block; }

        SaiyanAuraProfile profile;
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Emitter> emitters = new List<Emitter>();
        Transform core;
        Mesh frameQuad, columnQuad;
        bool transition, stopping, mirrored, paused;
        float age, boltPosition, lastClock, localClock, smoothedCharge, smoothedFury;
        bool hasClock;
        int boltOffset;
        Color deep = new Color(.957f, .584f, .071f), mid = new Color(1f, .859f, .271f), light = new Color(1f, .976f, .722f);
        bool recolored;
        Color recolorDeep, recolorMid, recolorLight;

        public float Charge { get { return smoothedCharge; } }
        public float Overdrive { get { return smoothedFury; } }
        // The game's camera value for SOut (see SaiyanCommon.cginc); 0 in the preview.
        public float Hdr;
        public int CurrentBolt { get; private set; }

        // A quad over frame pixels x0..x1, y0..y1 (y down), with uv such that the shader recovers those pixels.
        static Mesh Quad(string name, float x0, float x1, float y0, float y1)
        {
            var box = SaiyanAuraFrames.BodyBox;
            float k = BodyHeight / (box.w - box.y), cx = (box.x + box.z) * .5f;
            float W = SaiyanAuraFrames.Width, H = SaiyanAuraFrames.Height;
            Func<float, float> lx = x => (x - cx) * k;
            Func<float, float> ly = y => Foot + (box.w - y) * k;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(lx(x0), ly(y1), 0), new Vector3(lx(x1), ly(y1), 0), new Vector3(lx(x0), ly(y0), 0), new Vector3(lx(x1), ly(y0), 0) };
            mesh.uv = new[] { new Vector2(x0 / W, 1 - y1 / H), new Vector2(x1 / W, 1 - y1 / H), new Vector2(x0 / W, 1 - y0 / H), new Vector2(x1 / W, 1 - y0 / H) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        void AddLayer(string childName, Mesh mesh, bool over, int order, float mode, int pass)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = SaiyanAuraAssets.Flipbook;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingLayerName = over ? "Top" : "Background"; r.sortingOrder = order;
            if (mode > 1.5f) r.enabled = false;
            layers.Add(new Layer { Renderer = r, Mode = mode, Pass = pass });
        }

        void AddCore()
        {
            if (!SaiyanAuraAssets.Core) return;
            var go = Instantiate(SaiyanAuraAssets.Core, transform, false);
            go.name = "Saiyan aura energy";
            core = go.transform;
            foreach (var system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = system.GetComponent<ParticleSystemRenderer>();
                bool front = system.name.IndexOf("front", StringComparison.OrdinalIgnoreCase) >= 0;
                r.sortingLayerName = front ? "Top" : "Background"; r.sortingOrder = front ? 6 : -10;
                var emission = system.emission;
                emitters.Add(new Emitter { System = system, Renderer = r, Rate = emission.rateOverTime.constant, Block = new MaterialPropertyBlock() });
            }
        }

        public void Setup(string id, bool isTransition, int identity = 0)
        {
            profile = SaiyanAuraAssets.Profiles[id];
            transition = isTransition;
            var random = new System.Random(identity * 7919 + 17);
            boltOffset = random.Next(SaiyanAuraFrames.BoltSequence.Length);
            mirrored = random.NextDouble() < .5;
            localClock = (float)random.NextDouble() * 50;
            float W = SaiyanAuraFrames.Width, H = SaiyanAuraFrames.Height;
            frameQuad = Quad("Saiyan aura frame", 0, W, 0, H);
            // The aura and its SSJ2/3 lightning are the user's effects (SSJ1AuraController, attached by
            // RefinedAuraVisual); this rig now only keeps the charge envelope. The GIF lightning below stays
            // available but is no longer added.
            if (transition || SaiyanAuraAssets.UserLightning) return;
            for (int pass = 0; pass < profile.Bolts; pass++) AddLayer("Saiyan lightning " + (pass + 1), frameQuad, true, 7 + pass, 2, pass);
        }

        public void SetPalette(Color a, Color b, Color c) { deep = a; mid = b; light = c; }

        // A custom transformation colour replaces the gold everywhere.
        public void Recolor(Color primary, Color bright)
        {
            recolored = true;
            recolorDeep = primary * .8f; recolorMid = Color.Lerp(primary, bright, .55f); recolorLight = Color.Lerp(bright, Color.white, .55f);
            recolorDeep.a = recolorMid.a = recolorLight.a = 1;
        }

        static Vector4 FrameRect(int frame, int columns, int rows)
        {
            float fw = SaiyanAuraFrames.Width + SaiyanAuraFrames.Pad * 2, fh = SaiyanAuraFrames.Height + SaiyanAuraFrames.Pad * 2;
            float aw = columns * fw, ah = rows * fh;
            int c = frame % columns, r = frame / columns;
            float vSize = SaiyanAuraFrames.Height / ah;
            float vTop = 1 - (r * fh + SaiyanAuraFrames.Pad) / ah;
            return new Vector4((c * fw + SaiyanAuraFrames.Pad) / aw, vTop - vSize, SaiyanAuraFrames.Width / aw, vSize);
        }

        void Pause(bool value)
        {
            if (value == paused) return;
            paused = value;
            foreach (var e in emitters) { if (!e.System) continue; if (value) e.System.Pause(true); else e.System.Play(true); }
        }

        public void Tick(float clock, float delta, float charge, float cover, float opacity, bool freeze, float overdrive = 0)
        {
            if (profile == null) return;
            Pause(freeze);
            if (freeze) return;
            age += delta;
            smoothedFury = Mathf.MoveTowards(smoothedFury, Mathf.Clamp01(overdrive), delta * 3.5f);
            float target = Mathf.Max(Mathf.Clamp01(charge), smoothedFury * .9f);
            smoothedCharge = Mathf.MoveTowards(smoothedCharge, target, delta * (target > smoothedCharge ? 2.5f : 3f));
            float step = hasClock ? Mathf.Max(0, clock - lastClock) : 0;
            lastClock = clock; hasClock = true;
            localClock += step;
            boltPosition += step * SaiyanAuraFrames.BoltFramesPerSecond * (1 + smoothedCharge * .25f);
            if (boltPosition > 1e5f) boltPosition -= SaiyanAuraFrames.BoltSequence.Length * 1000;
            if (localClock > 1e4f) localClock -= 1e4f;
            var sequence = SaiyanAuraFrames.BoltSequence;
            int boltStep = (int)boltPosition + boltOffset;

            float appear = transition ? cover : Mathf.SmoothStep(0, 1, age / .14f);
            float flash = transition ? cover * .8f : Mathf.Clamp01(1 - age / .3f) * .5f;
            // A short pop when it ignites, then the charge envelope, rooted at the feet.
            float pop = transition ? cover * .1f : Mathf.Sin(Mathf.Clamp01(age / .3f) * Mathf.PI) * .07f;
            float gx = profile.Scale * (1 + smoothedCharge * profile.Swell.x + smoothedFury * .1f + pop);
            // As tall as the fighter in every form; SSJ2/3 are wider, and charging swells it a little.
            float gy = 1 + smoothedCharge * profile.Swell.y * .5f + smoothedFury * .05f + pop * .5f;
            float alpha = Mathf.Clamp01(opacity) * appear;
            Color m = recolored ? recolorMid : mid, l = recolored ? recolorLight : light;
            m.a = l.a = 1;
            // The column: lemon-gold at its edges, near white in the middle. Core.a holds the wide light back
            // a little from the bloom so the fighter's outline in front of it stays sharp.
            var edge = Color.Lerp(l, m, .55f);
            edge.r *= .96f; // lemon rather than gold
            var coreColour = Color.Lerp(l, Color.white, .5f);
            coreColour.a = Hdr > 1 ? .95f : 1f;
            var box = SaiyanAuraFrames.BodyBox;
            float unit = (box.w - box.y) / BodyHeight;
            float headY = box.w - (Height + Feet - Foot) * unit;
            var pillar = new Vector4((box.x + box.z) * .5f, ColumnHalfWidth, box.w, headY - ColumnOverHead * unit);
            var framePx = new Vector4(SaiyanAuraFrames.Width, SaiyanAuraFrames.Height, 0, 0);
            var shape = new Vector4(0, 0, SaiyanAuraFrames.BoltRange, SaiyanAuraFrames.BoltWidth);
            var hole = new Vector4(0, 0, 0, BoltThickness * (1 + smoothedCharge * .25f));
            CurrentBolt = -1;

            foreach (var layer in layers)
            {
                var t = layer.Renderer.transform;
                bool bolt = layer.Mode > 1.5f;
                t.localScale = new Vector3(gx * (bolt && mirrored != (layer.Pass == 1) ? -1 : 1), gy, 1);
                t.localPosition = new Vector3(0, Foot * (1 - gy), 0);
                var block = layer.Block;
                if (bolt)
                {
                    int drawing = stopping ? -1 : sequence[(boltStep + layer.Pass * 37) % sequence.Length];
                    if (layer.Pass == 0) CurrentBolt = drawing;
                    layer.Renderer.enabled = drawing >= 0 && alpha > .01f;
                    if (!layer.Renderer.enabled) continue;
                    block.SetVector("_BoltFrame", FrameRect(drawing, SaiyanAuraFrames.BoltColumns, SaiyanAuraFrames.BoltRows));
                    block.SetVectorArray("_BoltPoly", SaiyanAuraAssets.BoltLines[drawing]);
                    block.SetFloat("_BoltCount", SaiyanAuraAssets.BoltCounts[drawing]);
                    block.SetColor("_Electric", new Color(.55f, .8f, 1f, 1));
                }
                else
                {
                    block.SetVector("_Pillar", pillar);
                    block.SetColor("_Edge", edge);
                    block.SetColor("_Core", coreColour);
                    block.SetFloat("_Flash", flash);
                    block.SetFloat("_Clock", localClock);
                    block.SetFloat("_Charge", smoothedCharge);
                }
                block.SetVector("_FramePx", framePx);
                block.SetVector("_Shape", shape);
                block.SetVector("_Hole", hole);
                block.SetFloat("_Mode", layer.Mode);
                block.SetFloat("_Hdr", Hdr);
                block.SetFloat("_Opacity", alpha);
                layer.Renderer.SetPropertyBlock(block);
            }

            if (core)
            {
                core.localScale = new Vector3(gx, gy, 1);
                core.localPosition = new Vector3(0, Foot * (1 - gy), 0);
                foreach (var e in emitters)
                {
                    if (!e.System) continue;
                    var emission = e.System.emission;
                    emission.rateOverTime = stopping ? 0 : e.Rate * profile.Energy * (1 + smoothedCharge * 1.6f + smoothedFury);
                    var main = e.System.main;
                    main.simulationSpeed = 1 + smoothedCharge * .5f;
                    e.Block.SetFloat("_Hdr", Hdr);
                    e.Block.SetFloat("_Opacity", alpha);
                    e.Renderer.SetPropertyBlock(e.Block);
                }
            }
        }

        // The preview project has no player loop to run the particles; it steps them itself.
        public void StepPreview(float seconds)
        {
            foreach (var e in emitters) if (e.System) e.System.Simulate(seconds, true, false, false);
        }

        public void StopEmission() { stopping = true; }

        void OnDestroy() { if (frameQuad) Destroy(frameQuad); if (columnQuad) Destroy(columnQuad); }
    }
}
