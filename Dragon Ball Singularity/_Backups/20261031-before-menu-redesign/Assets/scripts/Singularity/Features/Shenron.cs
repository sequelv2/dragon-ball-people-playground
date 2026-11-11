using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Shenron & the wish system. The Earth Dragon Balls have existed in the mod since the
    // start, but activating them only produced joke messages. Now: gather all seven within
    // a few metres, activate any of them, and the Eternal Dragon rises from a pillar of light.
    // Three wishes (a mass revival costs two, as with Dende's Shenron); afterwards the balls
    // scatter across the map and stay stone for a while.
    public sealed class ShenronModule : SingularityModule
    {
        public override string Id { get { return "shenron"; } }
        public override string Title { get { return "Shenron & Wishes"; } }
        public override string Summary { get { return "Gather all seven Earth Dragon Balls and activate one to summon Shenron. Three wishes; a mass revival costs two."; } }

        public const float GatherRadius = 3.4f, StoneSeconds = 90f;
        public static Sprite Head, Segment, Fin, Claw;
        public static readonly Sprite[] Stone = new Sprite[8];
        public static readonly Sprite[] Balls = new Sprite[8];
        public const string SfxRumble = "Assets/SFX/Singularity/Shenron/Rumble.wav";
        public const string SfxThunder = "Assets/SFX/Singularity/Shenron/Thunder.wav";
        public const string SfxRoar = "Assets/SFX/Singularity/Shenron/Roar.wav";
        public const string SfxGrant = "Assets/SFX/Singularity/Shenron/Grant.wav";
        public const string SfxScatter = "Assets/SFX/Singularity/Shenron/Scatter.wav";

        public override void Boot()
        {
            Head = Load("Assets/Sprites/Singularity/Shenron/Head.png");
            Segment = Load("Assets/Sprites/Singularity/Shenron/Segment.png");
            Fin = Load("Assets/Sprites/Singularity/Shenron/Fin.png");
            Claw = Load("Assets/Sprites/Singularity/Shenron/Claw.png");
            for (int i = 1; i <= 7; i++)
            {
                Stone[i] = Load("Assets/Sprites/Singularity/DragonBalls/DB_Stone_" + i + ".png");
                Balls[i] = Load("Assets/Sprites/Items/DB_" + i + ".png");
            }
            DbpuFx.PreloadExtra(SfxRumble, SfxThunder, SfxRoar, SfxGrant, SfxScatter);
        }

        static Sprite Load(string path)
        {
            try { return ModAPI.LoadSprite(path); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] sprite " + path + ": " + ex.Message); return null; }
        }

        // Called by the Earth Dragon Ball item when it is activated.
        public static void UseBall(GameObject ballObject)
        {
            var ball = ballObject ? ballObject.GetComponent<EarthDB>() : null;
            if (!ball) return;
            var module = Singularity.Get<ShenronModule>();
            if (module == null || !module.Enabled)
            {
                ModAPI.Notify("Shenron is switched off (Mod Configurations > Singularity Features).");
                return;
            }
            ShenronAltar.TrySummon(ball);
        }
    }

    // Per-ball state: stone cooldown after a wish.
    public sealed class SingularityDragonBall : MonoBehaviour
    {
        public float StoneUntil;
        public bool IsStone { get { return Time.time < StoneUntil; } }
        EarthDB ball; SpriteRenderer sr; bool wasStone;

        public static SingularityDragonBall Of(EarthDB ball)
        {
            if (!ball) return null;
            var state = ball.GetComponent<SingularityDragonBall>();
            if (!state) state = ball.gameObject.AddComponent<SingularityDragonBall>();
            return state;
        }

        void Awake() { ball = GetComponent<EarthDB>(); sr = GetComponent<SpriteRenderer>(); }

        public void TurnToStone(float seconds)
        {
            StoneUntil = Time.time + seconds;
            wasStone = true;
            if (sr && ball && ball.Stars >= 1 && ball.Stars <= 7 && ShenronModule.Stone[ball.Stars]) sr.sprite = ShenronModule.Stone[ball.Stars];
        }

        void Update()
        {
            if (!wasStone || IsStone) return;
            wasStone = false;
            if (sr && ball && ball.Stars >= 1 && ball.Stars <= 7 && ShenronModule.Balls[ball.Stars]) sr.sprite = ShenronModule.Balls[ball.Stars];
            SingularityFx.Flash(transform.position, new Color(1f, .62f, .2f, .9f), .6f, .5f);
        }
    }

    public static class ShenronAltar
    {
        public static void TrySummon(EarthDB used)
        {
            if (ShenronSummon.Current)
            {
                Say(used, "SHENRON IS ALREADY HERE", "STATE YOUR WISH");
                return;
            }
            var byStar = new Dictionary<int, EarthDB>();
            float stoneLeft = 0f;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<EarthDB>())
            {
                if (!b || !b.gameObject.activeInHierarchy) continue;
                var state = b.GetComponent<SingularityDragonBall>();
                if (state && state.IsStone) { stoneLeft = Mathf.Max(stoneLeft, state.StoneUntil - Time.time); continue; }
                if (b.Stars >= 1 && b.Stars <= 7 && !byStar.ContainsKey(b.Stars)) byStar[b.Stars] = b;
            }
            if (stoneLeft > 0f && byStar.Count < 7)
            {
                Say(used, "THE DRAGON BALLS ARE STONE", "THEY RETURN IN " + Mathf.CeilToInt(stoneLeft) + " SECONDS");
                return;
            }
            if (byStar.Count < 7)
            {
                var missing = new List<string>();
                for (int i = 1; i <= 7; i++) if (!byStar.ContainsKey(i)) missing.Add(i + "★");
                Say(used, "DRAGON BALLS  " + byStar.Count + " / 7", "MISSING  " + string.Join("  ", missing.ToArray()));
                return;
            }
            Vector2 centre = Vector2.zero;
            foreach (var b in byStar.Values) centre += (Vector2)b.transform.position;
            centre /= byStar.Count;
            float spread = 0f;
            foreach (var b in byStar.Values) spread = Mathf.Max(spread, Vector2.Distance(centre, b.transform.position));
            if (spread > ShenronModule.GatherRadius)
            {
                Say(used, "ALL SEVEN FOUND", "BRING THEM TOGETHER TO SUMMON THE DRAGON");
                return;
            }
            ShenronSummon.Begin(new List<EarthDB>(byStar.Values), centre);
        }

        static void Say(EarthDB ball, string title, string subtitle)
        {
            if (!ball) return;
            DbpuWorldLabel.Show(ball.GetInstanceID(), ball.transform, title, subtitle, 2.6f, -1f, new Color(1f, .7f, .25f), null);
        }
    }

    public sealed class ShenronSummon : MonoBehaviour
    {
        enum Phase { Gather, Pillar, Emerge, Speak, Menu, Granting, Farewell, Scatter }
        sealed class Ball { public EarthDB Db; public Rigidbody2D Body; public RigidbodyType2D Type; public SpriteRenderer Glow; public Vector2 Home; }

        public static ShenronSummon Current { get; private set; }
        readonly List<Ball> balls = new List<Ball>();
        Vector2 origin; Phase phase; float age, phaseAge, boltClock, menuDelay = -1f;
        int wishesLeft = 3, wishesGranted; bool roared, cameraSaved; Color savedBackground;
        ShenronBody body; SpriteRenderer pillar, pillarCore; LightSprite lamp;
        static readonly Color Gold = new Color(1f, .82f, .3f, 1f), DragonGreen = new Color(.45f, 1f, .45f, 1f);

        public static void Begin(List<EarthDB> found, Vector2 centre)
        {
            if (Current) return;
            var go = new GameObject("DBPU Shenron summoning");
            var s = go.AddComponent<ShenronSummon>();
            Current = s;
            s.origin = centre;
            foreach (var db in found)
            {
                var b = new Ball { Db = db, Body = db.GetComponent<Rigidbody2D>(), Home = db.transform.position };
                if (b.Body) { b.Type = b.Body.bodyType; b.Body.velocity = Vector2.zero; b.Body.angularVelocity = 0f; b.Body.bodyType = RigidbodyType2D.Kinematic; }
                b.Glow = DbpuFx.MakeSprite(go.transform, "Dragon Ball glow", DbpuFx.Glow, 70, true);
                s.balls.Add(b);
            }
            s.balls.Sort((x, y) => x.Db.Stars.CompareTo(y.Db.Stars));
            SingularityAnnouncer.Show("COME FORTH, SHENRON!", "AND GRANT MY WISH", 3f, Gold);
            DbpuFx.Sound(centre, ShenronModule.SfxRumble, 1f);
            try { s.lamp = ModAPI.CreateLight(go.transform, new Color(1f, .7f, .25f), 4f, 0f); } catch { s.lamp = null; }
        }

        void SetPhase(Phase p) { phase = p; phaseAge = 0f; }

        void Update()
        {
            float dt = Time.deltaTime > 0f ? Mathf.Min(Time.deltaTime, .05f) : 0f;
            if (DbpuFx.Paused) return;
            age += dt; phaseAge += dt;
            UpdateBalls(dt);
            UpdateSky(dt);
            switch (phase)
            {
                case Phase.Gather:
                    if (phaseAge >= 2.2f) { SetPhase(Phase.Pillar); Thunder(true); DbpuFx.Shake(origin, 3f); }
                    break;
                case Phase.Pillar:
                    UpdatePillar(Mathf.Clamp01(phaseAge / .5f), 1f);
                    if (phaseAge >= 1.1f) { SetPhase(Phase.Emerge); body = ShenronBody.Create(origin, UnityEngine.Random.value < .5f); }
                    break;
                case Phase.Emerge:
                    UpdatePillar(1f, 1f);
                    if (body) body.Progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseAge / 3.6f));
                    if (!roared && phaseAge >= 2.6f) { roared = true; DbpuFx.Sound(origin + Vector2.up * 10f, ShenronModule.SfxRoar, 1f); DbpuFx.Shake(origin, 4f); }
                    if (phaseAge >= 3.8f) { SetPhase(Phase.Speak); SingularityAnnouncer.Show("I AM THE ETERNAL DRAGON", "STATE YOUR WISH. I SHALL GRANT YOU THREE.", 4f, DragonGreen); menuDelay = 1.6f; }
                    break;
                case Phase.Speak:
                    UpdatePillar(1f - Mathf.Clamp01(phaseAge / .8f), 1f);
                    if (menuDelay >= 0f) { menuDelay -= dt; if (menuDelay < 0f) OpenMenu(); }
                    break;
                case Phase.Menu:
                    break;
                case Phase.Granting:
                    if (menuDelay >= 0f) { menuDelay -= dt; if (menuDelay < 0f) { if (wishesLeft > 0) OpenMenu(); else Farewell(); } }
                    break;
                case Phase.Farewell:
                    if (body) body.Progress = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phaseAge - .8f) / 2.4f));
                    UpdatePillar(phaseAge > .6f && phaseAge < 3.2f ? 1f : 0f, .8f);
                    if (phaseAge >= 3.6f) { if (wishesGranted > 0) Scatter(); else Finish(); }
                    break;
                case Phase.Scatter:
                    if (phaseAge >= 1.2f) Finish();
                    break;
            }
            if (lamp) { lamp.Brightness = Mathf.Lerp(lamp.Brightness, phase == Phase.Scatter ? 0f : 1.4f, dt * 2f); lamp.Radius = 6f; }
        }

        void UpdateBalls(float dt)
        {
            bool floating = phase != Phase.Scatter;
            for (int i = 0; i < balls.Count; i++)
            {
                var b = balls[i];
                if (!b.Db) continue;
                float a = i / (float)balls.Count * Mathf.PI * 2f + age * .9f;
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 1.6f));
                Vector2 target = origin + new Vector2(Mathf.Cos(a) * .55f, .75f + Mathf.Sin(a) * .18f) * lift + (Vector2)(b.Home - origin) * (1f - lift);
                if (floating && b.Body)
                {
                    b.Body.MovePosition(Vector2.Lerp(b.Body.position, target, 1f - Mathf.Exp(-8f * dt)));
                    b.Body.MoveRotation(b.Body.rotation + dt * 90f);
                }
                float pulse = .6f + .4f * Mathf.Sin(age * 7f + i);
                float glow = phase == Phase.Gather ? lift : (phase == Phase.Farewell ? 1.3f : .8f);
                DbpuFx.Place(b.Glow, b.Db.transform.position, -.5f, .9f * glow * pulse + .2f, .9f * glow * pulse + .2f, 0f, new Color(1f, .62f, .18f, .85f));
            }
        }

        void UpdateSky(float dt)
        {
            bool dark = phase != Phase.Scatter;
            if (dark) SingularityVeil.Show("shenron-sky", new Color(.02f, .05f, .04f, 1f), .82f * Mathf.Clamp01(age / 1.8f), 0f, "Background", 40);
            var cam = Global.main ? Global.main.camera : null;
            if (cam)
            {
                if (!cameraSaved) { savedBackground = cam.backgroundColor; cameraSaved = true; }
                Color night = new Color(.02f, .05f, .05f, 1f);
                cam.backgroundColor = Color.Lerp(cam.backgroundColor, dark ? night : savedBackground, 1f - Mathf.Exp(-2.5f * dt));
            }
            if (phase == Phase.Gather || phase == Phase.Pillar || phase == Phase.Emerge)
            {
                boltClock -= dt;
                if (boltClock <= 0f) { boltClock = UnityEngine.Random.Range(.15f, .45f); Thunder(false); }
            }
        }

        void Thunder(bool big)
        {
            var cam = Global.main ? Global.main.camera : null;
            float top = cam ? cam.transform.position.y + cam.orthographicSize * 1.2f : origin.y + 14f;
            Vector2 from = new Vector2(origin.x + UnityEngine.Random.Range(-9f, 9f), top);
            Vector2 to = big ? origin + Vector2.up * .8f : from + new Vector2(UnityEngine.Random.Range(-3f, 3f), -UnityEngine.Random.Range(4f, 10f));
            SingularityFx.Lightning(from, to, big ? Gold : new Color(.75f, 1f, .7f), big ? .12f : .05f, big ? .35f : .18f, 16, big ? .9f : .6f);
            if (big || UnityEngine.Random.value < .35f) DbpuFx.Sound(from, ShenronModule.SfxThunder, big ? 1f : .45f);
        }

        void UpdatePillar(float strength, float width)
        {
            if (!pillar)
            {
                pillar = DbpuFx.MakeSprite(transform, "Shenron pillar glow", DbpuFx.Streak, 66, true, "Background");
                pillarCore = DbpuFx.MakeSprite(transform, "Shenron pillar core", DbpuFx.Streak, 67, true, "Background");
            }
            float flicker = .85f + .15f * Mathf.Sin(age * 31f);
            Vector2 c = origin + Vector2.up * 14f;
            DbpuFx.Place(pillar, c, -.4f, 2.6f * width * strength + .001f, 30f, 90f, new Color(1f, .78f, .28f, .55f * strength * flicker));
            DbpuFx.Place(pillarCore, c, -.41f, .9f * width * strength + .001f, 30f, 90f, new Color(1f, 1f, .85f, .9f * strength * flicker));
        }

        void OpenMenu()
        {
            SetPhase(Phase.Menu);
            var nearest = NearestFighter();
            var buttons = new List<DbpuMenuButton>();
            buttons.Add(new DbpuMenuButton(wishesLeft >= 2 ? "Revive and heal every fighter  (2 wishes)" : "<color=#888888>Revive everyone  (needs 2 wishes)", () =>
            {
                if (wishesLeft < 2) { ModAPI.Notify("A mass revival takes two wishes."); return; }
                Grant(2, "EVERY FIGHTER LIVES AGAIN", ShenronWishes.ReviveEveryone);
            }));
            if (nearest)
            {
                string name = SingularityFighters.DisplayName(nearest);
                buttons.Add(new DbpuMenuButton("Make " + name + " immortal", () => Grant(1, name.ToUpperInvariant() + " CAN NO LONGER DIE", () => ShenronWishes.Immortality(nearest))));
            }
            buttons.Add(new DbpuMenuButton("Restore everyone's ki and stamina", () => Grant(1, "YOUR STRENGTH HAS RETURNED", ShenronWishes.RestoreKi)));
            buttons.Add(new DbpuMenuButton("A rain of Senzu Beans", () => Grant(1, "SENZU BEANS FROM THE HEAVENS", () => ShenronWishes.SenzuRain(origin))));
            buttons.Add(new DbpuMenuButton("Make me stronger than Lord Beerus", () =>
            {
                SingularityAnnouncer.Show("THAT WISH EXCEEDS MY POWER", "I CANNOT SURPASS A GOD OF DESTRUCTION", 3.4f, DragonGreen);
                DbpuFx.Sound(origin + Vector2.up * 10f, ShenronModule.SfxRoar, .5f);
            }));
            buttons.Add(new DbpuMenuButton(wishesGranted > 0 ? "That is all, Shenron" : "Never mind", () => { DbpuConfigMenuUI.Close(); Farewell(); }));
            string title = "SHENRON  —  " + wishesLeft + (wishesLeft == 1 ? " WISH LEFT" : " WISHES LEFT");
            DbpuConfigMenuUI.Open(title, () => Farewell(), buttons.ToArray());
        }

        void Grant(int cost, string line, Action effect)
        {
            DbpuConfigMenuUI.Close();
            wishesLeft = Mathf.Max(0, wishesLeft - cost);
            wishesGranted += cost;
            try { effect(); } catch (Exception ex) { Debug.LogWarning("[Singularity] wish: " + ex); }
            if (body) body.Flare();
            DbpuFx.Sound(origin + Vector2.up * 8f, ShenronModule.SfxGrant, 1f);
            SingularityAnnouncer.Show("YOUR WISH HAS BEEN GRANTED", line, 3.2f, DragonGreen);
            SetPhase(Phase.Granting);
            menuDelay = 3.2f;
        }

        void Farewell()
        {
            if (phase == Phase.Farewell || phase == Phase.Scatter) return;
            if (DbpuConfigMenuUI.IsOpen) DbpuConfigMenuUI.Close();
            SetPhase(Phase.Farewell);
            SingularityAnnouncer.Show(wishesGranted > 0 ? "FAREWELL" : "VERY WELL", wishesGranted > 0 ? "YOUR WISHES HAVE BEEN GRANTED" : "I SHALL RETURN TO MY SLUMBER", 3f, DragonGreen);
        }

        void Scatter()
        {
            SetPhase(Phase.Scatter);
            DbpuFx.Sound(origin, ShenronModule.SfxScatter, 1f);
            SingularityFx.Flash(origin + Vector2.up * .8f, Gold, 3f, .6f);
            SingularityFx.Ring(origin + Vector2.up * .8f, Gold, 6f, .7f, 2f);
            foreach (var b in balls)
            {
                if (!b.Db) continue;
                if (b.Body)
                {
                    b.Body.bodyType = b.Type == RigidbodyType2D.Static ? RigidbodyType2D.Dynamic : b.Type;
                    float a = UnityEngine.Random.Range(25f, 155f) * Mathf.Deg2Rad;
                    b.Body.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(18f, 30f);
                    b.Body.angularVelocity = UnityEngine.Random.Range(-720f, 720f);
                }
                SingularityDragonBall.Of(b.Db).TurnToStone(ShenronModule.StoneSeconds);
            }
            balls.Clear();
            ModAPI.Notify("The Dragon Balls scattered across the world and turned to stone for " + Mathf.RoundToInt(ShenronModule.StoneSeconds) + " seconds.");
        }

        void Finish()
        {
            foreach (var b in balls)
            {
                if (!b.Db || !b.Body) continue;
                b.Body.bodyType = b.Type;
            }
            balls.Clear();
            SingularityVeil.Hide("shenron-sky", 1.5f);
            if (body) body.Dismiss();
            enabled = false;
            Invoke("Restore", 1.6f);
        }

        void Restore()
        {
            var cam = Global.main ? Global.main.camera : null;
            if (cam && cameraSaved) cam.backgroundColor = savedBackground;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            var cam = Global.main ? Global.main.camera : null;
            if (cam && cameraSaved) cam.backgroundColor = savedBackground;
            foreach (var b in balls) if (b.Db && b.Body) b.Body.bodyType = b.Type;
            if (body) body.Dismiss();
            if (DbpuConfigMenuUI.IsOpen && phase == Phase.Menu) DbpuConfigMenuUI.Close();
        }

        PersonBehaviour NearestFighter()
        {
            PersonBehaviour best = null; float bestDistance = 18f;
            foreach (var p in SingularityRunner.Persons)
            {
                if (!p || !p.IsAlive()) continue;
                float d = Vector2.Distance(DbpuFx.Centre(p), origin);
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }
    }

    // The Eternal Dragon: a chain of pixel-art scale segments along a spline, head last.
    public sealed class ShenronBody : MonoBehaviour
    {
        const int Count = 80;
        const float Spacing = .62f;
        static readonly float[] Offsets = BuildOffsets();
        static float[] BuildOffsets()
        {
            // Distance of each scale behind the head; the tail packs tighter as it tapers.
            var o = new float[Count];
            float d = Spacing * .9f;
            for (int i = 0; i < Count; i++)
            {
                o[i] = d;
                d += Spacing * Mathf.Lerp(1f, .55f, Mathf.Pow(i / (float)(Count - 1), 1.4f));
            }
            return o;
        }
        const float WorldScale = 1.7f;
        static readonly Vector2[] Path =
        {
            new Vector2(0f, .4f), new Vector2(-2.4f, 2.8f), new Vector2(3.3f, 5.0f), new Vector2(-3.6f, 7.4f),
            new Vector2(2.6f, 9.6f), new Vector2(-1.6f, 12.0f), new Vector2(2.4f, 13.8f), new Vector2(5.6f, 12.9f), new Vector2(7.2f, 11.6f)
        };
        static readonly int[] ClawAt = { 7, 9, 31, 33 };
        public float Progress;
        Vector2 origin; bool mirror; float clock, flare, fade = 1f; bool leaving;
        SpriteRenderer[] segments, fins; SpriteRenderer head, eyeGlow, halo; SpriteRenderer[] claws;
        LineRenderer[] whiskers;
        readonly List<Vector2> samples = new List<Vector2>();
        readonly List<float> lengths = new List<float>();
        float total;

        public static ShenronBody Create(Vector2 origin, bool mirror)
        {
            var go = new GameObject("DBPU Shenron");
            var b = go.AddComponent<ShenronBody>();
            b.origin = origin; b.mirror = mirror;
            b.Build();
            return b;
        }

        public void Flare() { flare = 1f; }
        public void Dismiss() { leaving = true; }

        void Build()
        {
            segments = new SpriteRenderer[Count];
            fins = new SpriteRenderer[Count];
            for (int i = 0; i < Count; i++)
            {
                segments[i] = Part("Shenron scale " + i, ShenronModule.Segment, 120 + (Count - i));
                if (i % 3 == 1) fins[i] = Part("Shenron fin " + i, ShenronModule.Fin, 119 + (Count - i) - 1);
            }
            claws = new SpriteRenderer[4];
            for (int i = 0; i < claws.Length; i++) claws[i] = Part("Shenron claw " + i, ShenronModule.Claw, 118);
            halo = DbpuFx.MakeSprite(transform, "Shenron halo", DbpuFx.Glow, 110, true, "Background");
            head = Part("Shenron head", ShenronModule.Head, 260);
            eyeGlow = DbpuFx.MakeSprite(transform, "Shenron eye", DbpuFx.Glow, 261, true, "Background");
            whiskers = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                whiskers[i] = DbpuFx.Line(transform, "Shenron whisker", 259, null, "Background");
                whiskers[i].positionCount = 12;
                whiskers[i].startWidth = .07f; whiskers[i].endWidth = .02f;
            }
            BuildSamples();
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = "Background"; sr.sortingOrder = order;
            sr.sharedMaterial = DbpuFx.SoftSprite;
            go.transform.localScale = Vector3.one * WorldScale;
            return sr;
        }

        Vector2 Control(int i)
        {
            i = Mathf.Clamp(i, 0, Path.Length - 1);
            Vector2 p = Path[i];
            if (mirror) p.x = -p.x;
            return p * 1.05f;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void BuildSamples()
        {
            samples.Clear(); lengths.Clear(); total = 0f;
            int spans = Path.Length - 1;
            Vector2 last = Control(0);
            for (int s = 0; s < spans; s++)
            {
                for (int k = 0; k < 24; k++)
                {
                    float t = k / 24f;
                    Vector2 p = CatmullRom(Control(s - 1), Control(s), Control(s + 1), Control(s + 2), t);
                    if (samples.Count > 0) total += Vector2.Distance(last, p);
                    samples.Add(p); lengths.Add(total); last = p;
                }
            }
            Vector2 end = Control(Path.Length - 1);
            total += Vector2.Distance(last, end);
            samples.Add(end); lengths.Add(total);
        }

        // Point and tangent at a distance along the spline (0 = the balls).
        void At(float distance, out Vector2 point, out Vector2 tangent)
        {
            distance = Mathf.Clamp(distance, 0f, total);
            int lo = 0, hi = lengths.Count - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (lengths[mid] < distance) lo = mid; else hi = mid; }
            float span = Mathf.Max(.0001f, lengths[hi] - lengths[lo]);
            float t = (distance - lengths[lo]) / span;
            point = Vector2.Lerp(samples[lo], samples[hi], t);
            tangent = (samples[hi] - samples[lo]).normalized;
            if (tangent.sqrMagnitude < .5f) tangent = Vector2.up;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            clock += dt;
            flare = Mathf.MoveTowards(flare, 0f, dt * 1.2f);
            if (leaving) { fade -= dt * 1.4f; if (fade <= 0f) { Destroy(gameObject); return; } }
            float headDistance = Mathf.Lerp(0f, total, Mathf.Clamp01(Progress));
            Vector2 headPoint = origin, headTangent = Vector2.right;
            for (int i = 0; i < Count; i++)
            {
                float d = headDistance - Offsets[i];
                var sr = segments[i];
                if (d < 0f) { sr.enabled = false; if (fins[i]) fins[i].enabled = false; continue; }
                Vector2 p, tan;
                At(d, out p, out tan);
                Vector2 normal = new Vector2(-tan.y, tan.x);
                float wave = Mathf.Sin(clock * 1.6f - d * .55f) * .22f * Mathf.Clamp01(d / 3f);
                p += normal * wave;
                Vector2 world = origin + p;
                float taper = Mathf.Lerp(1f, .42f, Mathf.Pow(i / (float)(Count - 1), 1.4f));
                float born = Mathf.Clamp01((headDistance - d) / 2.5f);   // segments form out of light
                Color c = Color.Lerp(new Color(1f, 1f, .7f, 1f), Color.white, born);
                c = Color.Lerp(c, new Color(1f, 1f, .8f, 1f), flare * .6f);
                c.a = fade * Mathf.Clamp01(born * 2f);
                bool left = tan.x < 0f;   // keep the green back on top when the body runs leftwards
                sr.enabled = true;
                sr.color = c;
                sr.flipY = left;
                sr.transform.position = new Vector3(world.x, world.y, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg);
                sr.transform.localScale = Vector3.one * WorldScale * taper;
                if (fins[i])
                {
                    fins[i].enabled = true; fins[i].color = c;
                    Vector2 fp = world + normal * (left ? -.5f : .5f) * taper * WorldScale;
                    fins[i].flipY = left;
                    fins[i].transform.position = new Vector3(fp.x, fp.y, 0f);
                    fins[i].transform.rotation = sr.transform.rotation;
                    fins[i].transform.localScale = Vector3.one * WorldScale * taper;
                }
            }
            // Head rides at the front of the spline, facing along it and kept upright.
            At(headDistance, out headPoint, out headTangent);
            Vector2 headWorld = origin + headPoint;
            bool facingLeft = headTangent.x < 0f;
            float angle = Mathf.Atan2(headTangent.y, headTangent.x) * Mathf.Rad2Deg;
            head.enabled = Progress > .02f;
            head.flipY = facingLeft;
            head.transform.position = new Vector3(headWorld.x, headWorld.y, 0f);
            head.transform.rotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sin(clock * 1.1f) * 4f);
            head.color = DbpuFx.A(Color.Lerp(Color.white, new Color(1f, 1f, .75f), flare * .7f), fade * Mathf.Clamp01(Progress * 6f));
            // Eye and whiskers in head space (sprite drawn facing right).
            Vector2 fwd = headTangent, up = facingLeft ? new Vector2(headTangent.y, -headTangent.x) : new Vector2(-headTangent.y, headTangent.x);
            Vector2 eye = headWorld + fwd * .35f * WorldScale + up * .18f * WorldScale;
            float eyePulse = .75f + .25f * Mathf.Sin(clock * 5f) + flare;
            DbpuFx.Place(eyeGlow, eye, -.2f, .55f * eyePulse, .55f * eyePulse, 0f, new Color(1f, .15f, .1f, .85f * fade * Mathf.Clamp01(Progress * 4f)));
            DbpuFx.Place(halo, headWorld, -.1f, 7f, 7f, 0f, new Color(.45f, 1f, .4f, (.18f + flare * .3f) * fade * Mathf.Clamp01(Progress * 3f)));
            Vector2 snout = headWorld + fwd * .95f * WorldScale + up * .02f * WorldScale;
            for (int w = 0; w < whiskers.Length; w++)
            {
                var line = whiskers[w];
                line.enabled = head.enabled;
                float side = w == 0 ? 1f : -1f;
                for (int k = 0; k < line.positionCount; k++)
                {
                    float t = k / (float)(line.positionCount - 1);
                    Vector2 back = -fwd * t * 3.6f + up * (side * .25f * t) + up * Mathf.Sin(clock * 2.4f + t * 5f + w) * .45f * t;
                    Vector2 p = snout + back;
                    line.SetPosition(k, new Vector3(p.x, p.y, -.1f));
                }
                Color wc = new Color(1f, .95f, .65f, .95f * fade * Mathf.Clamp01(Progress * 4f));
                line.startColor = wc; line.endColor = DbpuFx.A(wc, wc.a * .4f);
            }
            // Four claws along the body.
            for (int c = 0; c < claws.Length; c++)
            {
                float d = headDistance - Offsets[ClawAt[c]];
                if (d < 0f || Progress <= .05f) { claws[c].enabled = false; continue; }
                Vector2 p, tan; At(d, out p, out tan);
                Vector2 normal = new Vector2(-tan.y, tan.x) * (c % 2 == 0 ? -1f : 1f);
                Vector2 cp = origin + p + normal * .55f * WorldScale;
                claws[c].enabled = true;
                claws[c].color = DbpuFx.A(Color.white, fade);
                claws[c].transform.position = new Vector3(cp.x, cp.y, 0f);
                claws[c].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f);
                claws[c].flipX = c % 2 == 1;
            }
        }
    }

    public static class ShenronWishes
    {
        static readonly Color Gold = new Color(1f, .85f, .35f, 1f);

        public static void ReviveEveryone()
        {
            var seen = new HashSet<int>();
            foreach (var p in UnityEngine.Object.FindObjectsOfType<PersonBehaviour>())
            {
                if (!p || !seen.Add(p.GetInstanceID())) continue;
                if (p.GetComponent<HakaiDissolveMarker>()) continue;
                SingularityLife.Revive(p);
                Vector2 c = DbpuFx.Centre(p);
                SingularityFx.Ring(c, Gold, 2.2f, .7f, 1f);
                SingularityFx.Burst(c, Gold, 1f, 22);
            }
        }

        public static void Immortality(PersonBehaviour person)
        {
            if (!person) return;
            SingularityLife.Revive(person);
            if (!person.GetComponent<SingularityImmortal>()) person.gameObject.AddComponent<SingularityImmortal>();
            Vector2 c = DbpuFx.Centre(person);
            SingularityFx.Ring(c, Gold, 3f, .8f, 2f);
            SingularityFx.Burst(c, Gold, 1.4f, 30);
            DbpuWorldLabel.Show(person, "IMMORTAL", "SHENRON'S GIFT", 3f, -1f, Gold);
        }

        public static void RestoreKi()
        {
            foreach (var p in SingularityRunner.Persons)
            {
                var ki = SingularityFighters.Ki(p);
                if (!ki) continue;
                ki.currentKi = ki.maxKi; ki.Strain = 0f; ki.isExhausted = false; ki.exhaustionTimer = 0f;
                SingularityFx.Burst(DbpuFx.Centre(p), new Color(.5f, .9f, 1f), 1f, 16);
            }
        }

        public static void SenzuRain(Vector2 origin)
        {
            SpawnableAsset senzu = null;
            try { senzu = ModAPI.FindSpawnable("[DBPU] Senzu Bean"); } catch { senzu = null; }
            if (senzu == null || !senzu.Prefab) { ModAPI.Notify("Senzu Bean item not found."); return; }
            for (int i = 0; i < 12; i++)
            {
                Vector2 at = origin + new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(6f, 10f));
                var bean = UnityEngine.Object.Instantiate(senzu.Prefab, at, Quaternion.identity);
                CatalogBehaviour.PerformMod(senzu, bean);
                var phys = bean.GetComponent<PhysicalBehaviour>();
                if (phys) phys.SpawnSpawnParticles = false;
            }
        }
    }

    // Revival and full healing shared by wishes, Zenkai tests and Zeno-free resets.
    public static class SingularityLife
    {
        public static void Revive(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return;
            try
            {
                person.Braindead = false; person.BrainDamaged = false; person.BrainDamagedTime = 0f; person.SeizureTime = 0f;
            }
            catch { }
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                try
                {
                    limb.HealBone();
                    limb.Health = limb.InitialHealth;
                    limb.Numbness = 0f;
                    limb.LungsPunctured = false;
                    limb.BruiseCount = 0;
                    if (limb.CirculationBehaviour)
                    {
                        limb.CirculationBehaviour.HealBleeding();
                        if (limb.CirculationBehaviour.WasInitiallyPumping) limb.CirculationBehaviour.IsPump = true;
                        limb.CirculationBehaviour.BloodFlow = 1f;
                        limb.CirculationBehaviour.BloodAmount = 1f;
                        limb.CirculationBehaviour.StabWoundCount = 0;
                        limb.CirculationBehaviour.GunshotWoundCount = 0;
                    }
                    if (limb.PhysicalBehaviour) limb.PhysicalBehaviour.Temperature = 29f;
                    limb.InternalTemperature = 37f;
                    if (!limb.GetComponent<SenzuBeanBehaviour.SenzuBeanRegen>()) limb.gameObject.AddComponent<SenzuBeanBehaviour.SenzuBeanRegen>();
                }
                catch (Exception ex) { Debug.LogWarning("[Singularity] heal limb: " + ex.Message); }
            }
            try
            {
                person.Consciousness = 1f; person.ShockLevel = 0f; person.PainLevel = 0f; person.OxygenLevel = 1f; person.AdrenalineLevel = 1f;
            }
            catch { }
            var ki = person.GetComponent<KiSystem>();
            if (ki) { ki.currentKi = ki.maxKi; ki.Strain = 0f; }
        }
    }

    // Garlic Jr., Frieza and Zamasu all wanted it: the fighter heals relentlessly and cannot die.
    public sealed class SingularityImmortal : MonoBehaviour
    {
        PersonBehaviour person; float sparkle;
        void Awake() { person = GetComponent<PersonBehaviour>(); }
        void FixedUpdate()
        {
            if (!person || person.Limbs == null) return;
            bool needsRevive = false;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                if (limb.Health < limb.InitialHealth) limb.Health = Mathf.Min(limb.InitialHealth, limb.Health + limb.InitialHealth * .35f * Time.fixedDeltaTime);
                if (limb.HasBrain && limb.Health <= limb.InitialHealth * .1f) needsRevive = true;
            }
            if (person.Braindead || !person.IsAlive() || needsRevive)
            {
                SingularityLife.Revive(person);
                SingularityFx.Flash(DbpuFx.Centre(person), new Color(1f, .85f, .35f, .8f), 1.6f, .4f);
            }
            sparkle -= Time.fixedDeltaTime;
            if (sparkle <= 0f)
            {
                sparkle = UnityEngine.Random.Range(1.5f, 3f);
                SingularityFx.Burst(DbpuFx.Centre(person) + UnityEngine.Random.insideUnitCircle * .4f, new Color(1f, .85f, .35f), .5f, 6);
            }
        }
    }
}
