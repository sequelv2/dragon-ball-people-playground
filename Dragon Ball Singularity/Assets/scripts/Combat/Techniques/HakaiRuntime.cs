using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DBPU
{
    // Hakai (God of Destruction): while equipped, click any limb or object to erase it.
    // Fighters at or above the caster's power resist with a struggle bar; weaker targets
    // turn pale and disintegrate into rising violet sparkles. Use on the hand erases the
    // first thing in front of the open palm.
    public sealed class HakaiHand : MonoBehaviour
    {
        public bool Equipped;
        public LimbBehaviour Limb { get; private set; }
        public PersonBehaviour Person { get; private set; }
        public const string Prompt = "CLICK A LIMB OR AN OBJECT TO DESTROY";
        static readonly List<HakaiHand> armed = new List<HakaiHand>();
        KiSystem ki; UseEventTrigger use; int lastFrame = -1; float cooldown, labelClock, destroyText;
        readonly RaycastHit2D[] hits = new RaycastHit2D[24];

        public static HakaiHand Current
        {
            get
            {
                for (int i = armed.Count - 1; i >= 0; i--)
                {
                    var hand = armed[i];
                    if (!hand || !hand.Equipped || !KiCombatMath.ValidHand(hand.Limb, hand.Person)) { armed.RemoveAt(i); continue; }
                    return hand;
                }
                return null;
            }
        }
        public static HakaiHand GetOrCreate(LimbBehaviour limb)
        {
            if (!KiHandLoadout.IsHand(limb)) return null;
            var hand = limb.GetComponent<HakaiHand>(); if (!hand) hand = limb.gameObject.AddComponent<HakaiHand>();
            hand.Bind(limb); return hand;
        }
        void Awake() { Bind(GetComponent<LimbBehaviour>()); }
        void Bind(LimbBehaviour limb)
        {
            Limb = limb; Person = limb ? limb.Person : null;
            if (!Person) return;
            ki = Person.GetComponent<KiSystem>();
            if (!use) { use = gameObject.AddComponent<UseEventTrigger>(); use.Action = Activate; }
        }
        public void Arm()
        {
            Equipped = true; armed.Remove(this); armed.Add(this);
            DbpuTechniqueTargeting.Ensure();
            labelClock = 1.35f;
        }
        void Update()
        {
            if (!Equipped) return;
            if (!KiCombatMath.ValidHand(Limb, Person)) { Cancel(); return; }
            float dt = Time.deltaTime; cooldown -= dt; destroyText -= dt; labelClock -= dt;
            if (labelClock <= 0f && !DbpuFx.Paused)
            {
                labelClock = .25f;
                if (Current == this || destroyText > 0f) DbpuWorldLabel.Show(Person, "HAKAI", destroyText > 0f ? "DESTROY" : Prompt, .45f);
            }
        }
        public void Activate()
        {
            if (!Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidHand(Limb, Person)) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            armed.Remove(this); armed.Add(this);
            // Touch/palm Hakai: the first object in front of the hand within reach.
            Vector2 origin = KiCombatMath.HandPoint(Limb), dir = KiCombatMath.StrikeDirection(Limb);
            int count = Physics2D.CircleCastNonAlloc(origin, .25f, dir, hits, 6f);
            PhysicalBehaviour best = null; float distance = float.MaxValue; Vector2 point = origin;
            for (int i = 0; i < count; i++)
            {
                var target = DbpuTechniqueTargeting.Physical(hits[i].collider, Person);
                if (!target || hits[i].distance >= distance) continue;
                best = target; distance = hits[i].distance; point = hits[i].point;
            }
            if (best) CastAt(best, point);
            else DbpuWorldLabel.Show(Person, "HAKAI", "NOTHING IN REACH  ·  " + Prompt, 1.4f);
        }
        public bool CastAt(PhysicalBehaviour target, Vector2 point)
        {
            if (!Equipped || !target || cooldown > 0f || !KiCombatMath.CanAct()) return false;
            if (!ki) ki = Person.GetComponent<KiSystem>();
            if (!DbpuHandTechniques.Spend(ki, .10f)) { DbpuWorldLabel.Show(Person, "HAKAI", "NOT ENOUGH KI", 1.2f); return false; }
            cooldown = .45f; destroyText = 1.1f;
            HakaiEffects.Cast(Person, Limb, target, point);
            DbpuWorldLabel.Show(Person, "HAKAI", "DESTROY", 1.1f);
            return true;
        }
        public void Cancel()
        {
            bool was = Equipped; Equipped = false; armed.Remove(this);
            if (was && Person) DbpuWorldLabel.Hide(Person);
        }
        void OnDisable() { armed.Remove(this); }
        void OnDestroy() { armed.Remove(this); if (use) Destroy(use); }
    }

    // Consumes left clicks for the armed Hakai hand (Alt+click keeps normal dragging).
    [DefaultExecutionOrder(-232)]
    public sealed class DbpuTechniqueTargeting : MonoBehaviour
    {
        static DbpuTechniqueTargeting instance;
        static readonly Collider2D[] overlap = new Collider2D[48];
        Global blocker; int releaseFrame = -1;
        PhysicalBehaviour hovered; LineRenderer ring; LineRenderer[] ticks; float clock;
        public static bool InputCaptured { get { return instance && (instance.blocker || Time.frameCount <= instance.releaseFrame); } }

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Hakai target selection"); DontDestroyOnLoad(go);
            instance = go.AddComponent<DbpuTechniqueTargeting>();
        }
        public static PhysicalBehaviour Physical(Collider2D col, PersonBehaviour owner)
        {
            if (!col || col.isTrigger || col.GetComponent<KiShieldSurface>()) return null;
            var physical = col.GetComponent<PhysicalBehaviour>();
            if (!physical && col.attachedRigidbody) physical = col.attachedRigidbody.GetComponent<PhysicalBehaviour>();
            if (!physical || !physical.gameObject.activeInHierarchy || physical.gameObject.isStatic || physical.isDisintegrated) return null;
            var limb = physical.GetComponent<LimbBehaviour>();
            if (limb && (!limb.Person || limb.Person == owner)) return null;
            if (owner && physical.transform.root == owner.transform.root) return null;
            return physical;
        }
        public static PhysicalBehaviour Resolve(PersonBehaviour owner, Vector2 point)
        {
            int count = Physics2D.OverlapPointNonAlloc(point, overlap);
            PhysicalBehaviour best = null; int bestOrder = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                var physical = Physical(overlap[i], owner);
                if (!physical) continue;
                var sr = physical.GetComponent<SpriteRenderer>();
                int order = sr ? sr.sortingLayerID * 100000 + sr.sortingOrder : int.MinValue + 1;
                if (physical.GetComponent<LimbBehaviour>()) order += 1000000;
                if (order > bestOrder) { bestOrder = order; best = physical; }
            }
            return best;
        }
        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (blocker)
            {
                if (!Input.GetMouseButton(0) || !KiCombatMath.HasFocus || blocker != Global.main || blocker.Paused)
                { blocker.RemoveUiBlocker(); blocker = null; releaseFrame = Time.frameCount; }
                return;
            }
            hovered = null;
            var hand = HakaiHand.Current;
            if (!hand || !KiCombatMath.CanAct() || SpiritBombTargeting.Active || (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
            Vector2 point = DbpuFx.MouseWorld();
            hovered = Resolve(hand.Person, point);
            if (hovered && Input.GetMouseButtonDown(0) && hand.CastAt(hovered, point))
            {
                if (Global.main) { blocker = Global.main; blocker.AddUiBlocker(); }
                hovered = null;
            }
        }
        void LateUpdate()
        {
            if (!ring)
            {
                ring = DbpuFx.Line(transform, "Hakai target ring", 70, DbpuFx.Ribbon);
                ticks = new LineRenderer[4];
                for (int i = 0; i < 4; i++) ticks[i] = DbpuFx.Line(transform, "Hakai target tick", 70);
            }
            bool show = hovered && hovered.gameObject.activeInHierarchy;
            ring.enabled = show; foreach (var t in ticks) t.enabled = show;
            if (!show) return;
            Bounds bounds; var limb = hovered.GetComponent<LimbBehaviour>();
            if (limb && limb.Person) bounds = DbpuFx.BodyBounds(limb.Person);
            else { var sr = hovered.GetComponent<SpriteRenderer>(); bounds = sr ? sr.bounds : new Bounds(hovered.transform.position, Vector3.one); }
            Vector2 c = bounds.center; float r = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f, .35f, 6f);
            float pulse = 1f + .04f * Mathf.Sin(clock * 6f);
            Color color = new Color(.85f, .72f, 1f, .85f);
            ring.positionCount = 65; ring.startWidth = ring.endWidth = Mathf.Max(.025f, r * .03f); ring.startColor = ring.endColor = color;
            for (int i = 0; i < 65; i++) { float a = i * Mathf.PI * 2f / 64f; ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r * pulse, c.y + Mathf.Sin(a) * r * pulse, -.5f)); }
            for (int i = 0; i < 4; i++)
            {
                float a = clock * .9f + i * Mathf.PI * .5f; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ticks[i].positionCount = 2; ticks[i].startWidth = ticks[i].endWidth = Mathf.Max(.02f, r * .022f);
                ticks[i].startColor = ticks[i].endColor = color;
                ticks[i].SetPosition(0, (Vector3)(c + d * r * .78f) + Vector3.forward * -.5f); ticks[i].SetPosition(1, (Vector3)(c + d * r * 1.22f) + Vector3.forward * -.5f);
            }
        }
        void OnDisable() { if (blocker) blocker.RemoveUiBlocker(); blocker = null; hovered = null; }
        void OnDestroy() { OnDisable(); if (instance == this) instance = null; }
    }

    public static class HakaiEffects
    {
        public static readonly Color Lavender = new Color(.80f, .66f, 1f, 1f);
        public static readonly Color Violet = new Color(.56f, .26f, .96f, 1f);
        public static readonly Color Pale = new Color(.95f, .9f, 1f, 1f);
        public static void Cast(PersonBehaviour caster, LimbBehaviour hand, PhysicalBehaviour target, Vector2 point)
        {
            if (hand) PalmBurst(KiCombatMath.HandPoint(hand), .7f);
            HakaiReticleFx.Spawn(point, .9f);
            DbpuFx.Sound(point, "Assets/SFX/Techniques/Hakai/Cast.wav", .9f);
            var limb = target ? target.GetComponent<LimbBehaviour>() : null;
            if (limb && limb.Person) HakaiOnPerson.Begin(caster, limb.Person, point);
            else if (target) HakaiDissolve.DissolveObject(target, point);
        }
        public static void PalmBurst(Vector2 at, float scale)
        {
            var burst = DbpuParticles.Create("DBPU Hakai palm", DbpuFx.Star, 40, 47);
            burst.Release();
            for (int i = 0; i < 16; i++)
            {
                Vector2 v = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * UnityEngine.Random.Range(.6f, 2.6f) * scale;
                burst.Emit(at, v, UnityEngine.Random.Range(.25f, .6f), UnityEngine.Random.Range(.08f, .2f) * scale, Color.white, DbpuFx.A(Violet, 0f), 3f, 0, 0, false, 0, .5f);
            }
            burst.Emit(at, Vector2.zero, .35f, 1.1f * scale, DbpuFx.A(Lavender, .9f), DbpuFx.A(Violet, 0f), 0, 0, 90f, false, 1.6f * scale);
        }
        public static DbpuParticles SparkleField(string name, int capacity)
        {
            var field = DbpuParticles.Create(name, DbpuFx.Star, capacity, 49);
            field.Wind = new Vector2(UnityEngine.Random.Range(-.35f, .1f), .45f); field.Turbulence = .7f;
            return field;
        }
        public static void Sparkle(DbpuParticles stars, DbpuParticles squares, Vector2 at, float scale)
        {
            Sparkle(stars, squares, at, scale, Lavender, Violet);
        }
        public static void Sparkle(DbpuParticles stars, DbpuParticles squares, Vector2 at, float scale, Color accent, Color fade)
        {
            Vector2 v = new Vector2(UnityEngine.Random.Range(-.45f, .45f), UnityEngine.Random.Range(.25f, 1.25f)) * scale;
            if (squares && UnityEngine.Random.value < .22f)
            {
                float s = UnityEngine.Random.Range(.035f, .07f) * scale;
                squares.Emit(at, v * .8f, UnityEngine.Random.Range(1.2f, 2.2f), s, DbpuFx.A(Color.Lerp(accent, Color.white, .4f), .95f), DbpuFx.A(fade, 0f), .2f, -.15f, UnityEngine.Random.Range(-90f, 90f));
                return;
            }
            float size = UnityEngine.Random.Range(.05f, .16f) * scale;
            Color start = UnityEngine.Random.value < .5f ? Color.white : Color.Lerp(Color.white, accent, .55f);
            stars.Emit(at, v, UnityEngine.Random.Range(1.1f, 2.4f), size, start, DbpuFx.A(fade, 0f), .35f, -.2f, UnityEngine.Random.Range(-60f, 60f), false, size * .5f, .7f);
        }
    }

    // White crosshair circle that snaps onto the clicked point.
    [DefaultExecutionOrder(296)]
    public sealed class HakaiReticleFx : MonoBehaviour
    {
        SpriteRenderer ring, star; SpriteRenderer[] ticks = new SpriteRenderer[4];
        float age, size; Vector2 at;
        public static void Spawn(Vector2 point, float size)
        {
            var go = new GameObject("DBPU Hakai reticle"); var fx = go.AddComponent<HakaiReticleFx>();
            fx.at = point; fx.size = size;
            fx.ring = DbpuFx.MakeSprite(go.transform, "ring", DbpuFx.Ring, 60);
            fx.star = DbpuFx.MakeSprite(go.transform, "flash", DbpuFx.Star, 61, true);
            for (int i = 0; i < 4; i++) fx.ticks[i] = DbpuFx.MakeSprite(go.transform, "tick", DbpuFx.Streak, 60);
            fx.Draw();
        }
        void LateUpdate() { age += DbpuFx.Dt; if (age > .5f) { Destroy(gameObject); return; } Draw(); }
        void Draw()
        {
            float t = age / .5f, r = size * Mathf.Lerp(1.5f, .8f, Mathf.Sqrt(t)), a = 1f - t * t;
            DbpuFx.Place(ring, at, -.6f, r * 2f, r * 2f, 0, DbpuFx.A(Color.white, a * .9f));
            DbpuFx.Place(star, at, -.61f, size * (1.2f - t), size * (1.2f - t), t * 90f, DbpuFx.A(Color.white, a));
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + i * 90f + t * 60f) * Mathf.Deg2Rad; Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                DbpuFx.Place(ticks[i], at + d * r * 1.05f, -.6f, r * .55f, r * .05f, ang * Mathf.Rad2Deg, DbpuFx.A(Color.white, a * .85f));
            }
        }
    }

    // The judgement on a fighter: pale glow, optional resistance struggle, then dissolution.
    public sealed class HakaiOnPerson : MonoBehaviour
    {
        PersonBehaviour caster, victim; Vector2 origin;
        float progress, age, ratio, emitDebt, labelClock, release = -1f; int phase;
        readonly Dictionary<SpriteRenderer, Color> original = new Dictionary<SpriteRenderer, Color>();
        DbpuParticles stars; KiSystem casterKi;
        public bool Resisting { get { return phase == 1 && ratio < 1.15f; } }

        public static HakaiOnPerson Begin(PersonBehaviour caster, PersonBehaviour victim, Vector2 point)
        {
            if (!victim || !victim.IsAlive() && victim.Limbs == null) return null;
            var effect = victim.GetComponent<HakaiOnPerson>();
            if (effect) { if (effect.phase == 1) effect.progress = Mathf.Min(1f, effect.progress + .2f); return effect; }
            effect = victim.gameObject.AddComponent<HakaiOnPerson>();
            effect.caster = caster; effect.victim = victim; effect.origin = point;
            effect.casterKi = caster ? caster.GetComponent<KiSystem>() : null;
            double a = DbpuFx.PowerOf(caster), b = victim.GetComponent<KiSystem>() ? DbpuFx.PowerOf(victim) : 1e-9;
            effect.ratio = (float)Math.Min(1000d, a / Math.Max(1e-9, b));
            effect.Capture();
            return effect;
        }
        void Capture()
        {
            foreach (var limb in victim.Limbs)
            {
                if (!limb) continue;
                foreach (var sr in limb.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr && !original.ContainsKey(sr) && !sr.name.StartsWith("DBPU", StringComparison.Ordinal)) original[sr] = sr.color;
            }
            stars = HakaiEffects.SparkleField("DBPU Hakai judgement sparkles", 220);
            if (ratio < .2f)
            {
                DbpuWorldLabel.Show(victim, "HAKAI", "NO EFFECT", 1.6f);
                release = .45f; phase = 3;
            }
        }
        void Update()
        {
            if (!victim) { Destroy(this); return; }
            float dt = DbpuFx.Dt; if (dt <= 0f) return;
            age += dt;
            if (phase == 3)
            {
                release -= dt; Tint(Mathf.Clamp01(release / .45f) * .7f);
                if (release <= 0f) { Restore(); Destroy(this); }
                return;
            }
            Tint(Mathf.Clamp01(age / .45f) * .78f);
            var bounds = DbpuFx.BodyBounds(victim);
            emitDebt += dt * 26f;
            while (emitDebt >= 1f)
            {
                emitDebt -= 1f;
                Vector2 p = new Vector2(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(bounds.min.y, bounds.max.y));
                p += (p - (Vector2)bounds.center) * .25f;
                stars.Emit(p, new Vector2(0, UnityEngine.Random.Range(.05f, .3f)), UnityEngine.Random.Range(.35f, .8f), UnityEngine.Random.Range(.05f, .12f), Color.white, DbpuFx.A(HakaiEffects.Lavender, 0f), 1f, 0, 0, false, 0, .8f);
            }
            if (phase == 0)
            {
                if (age < .5f) return;
                phase = 1; progress = ratio >= 1.15f ? .7f : .42f;
            }
            if (ratio >= 1.15f) progress += dt * 1.9f;
            else
            {
                float resist = (1f / Mathf.Max(.01f, ratio)) * (DbpuFx.IsKiCharging(victim) ? 2.2f : 1f);
                progress = Mathf.Clamp01(progress + (.62f * ratio - .36f * resist) * dt);
                DbpuHandTechniques.Drain(casterKi, .07f, dt);
                labelClock -= dt;
                if (labelClock <= 0f) { labelClock = .1f; DbpuWorldLabel.Show(victim, "HAKAI", "RESISTING!", .4f, progress, null, new Color(.84f, .78f, .96f)); }
                if (casterKi && casterKi.currentKi <= 0f) progress = 0f;
            }
            if (progress >= 1f) { phase = 2; Dissolve(); return; }
            if (progress <= 0f && age > 1f) Resisted();
        }
        void Tint(float amount)
        {
            foreach (var pair in original)
            {
                if (!pair.Key) continue;
                Color o = pair.Value;
                pair.Key.color = new Color(Mathf.Lerp(o.r, HakaiEffects.Pale.r, amount), Mathf.Lerp(o.g, HakaiEffects.Pale.g, amount), Mathf.Lerp(o.b, HakaiEffects.Pale.b, amount), o.a);
            }
        }
        void Restore() { foreach (var pair in original) if (pair.Key) pair.Key.color = pair.Value; }
        void Resisted()
        {
            Restore();
            var burst = HakaiEffects.SparkleField("DBPU Hakai resisted burst", 80); burst.Release();
            Vector2 c = DbpuFx.Centre(victim);
            for (int i = 0; i < 40; i++)
                burst.Emit(c, DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * UnityEngine.Random.Range(1.5f, 5f), UnityEngine.Random.Range(.4f, .9f), UnityEngine.Random.Range(.06f, .16f), Color.white, DbpuFx.A(HakaiEffects.Violet, 0f), 3f);
            DbpuWorldLabel.Show(victim, "HAKAI", "RESISTED!", 1.8f);
            if (caster) DbpuWorldLabel.Show(caster, "HAKAI", "THEY RESISTED", 1.6f);
            DbpuFx.Sound(c, "Assets/SFX/Techniques/Hakai/Resist.wav", .8f);
            Destroy(this);
        }
        void Dissolve()
        {
            DbpuWorldLabel.Show(victim, "HAKAI", "DESTROYED", 1f);
            HakaiDissolve.DissolvePerson(victim, original);
            original.Clear();
            Destroy(this);
        }
        void OnDestroy()
        {
            if (phase != 2) Restore();
            if (stars) stars.Release();
        }
    }

    // Palette and pacing of a disintegration: Hakai (lavender) or Zen-O's erasure (white light).
    public struct DissolveStyle
    {
        public Color Pale, Accent, Fade; public string Sound; public float TimeScale;
        public static DissolveStyle Hakai { get { return new DissolveStyle { Pale = HakaiEffects.Pale, Accent = HakaiEffects.Lavender, Fade = HakaiEffects.Violet, Sound = "Assets/SFX/Techniques/Hakai/Dissolve.wav", TimeScale = 1f }; } }
        public static DissolveStyle Erase { get { return new DissolveStyle { Pale = new Color(.96f, 1f, 1f, 1f), Accent = new Color(.72f, .95f, 1f, 1f), Fade = new Color(.4f, .8f, 1f, 1f), Sound = null, TimeScale = .55f }; } }
    }

    // Disintegration into rising sparkles. Fighters dissolve from the head down; objects
    // dissolve outward from the click point at a few metres per second.
    [DefaultExecutionOrder(310)]
    public sealed class HakaiDissolve : MonoBehaviour
    {
        sealed class Part
        {
            public PhysicalBehaviour Physical; public SpriteRenderer[] Renderers; public Color[] Colors; public Collider2D[] Colliders;
            public Rigidbody2D Body; public Bounds Bounds; public float Start, End, Debt; public bool Done;
        }
        readonly List<Part> parts = new List<Part>();
        DbpuParticles stars, squares; PersonBehaviour person; Vector2 origin; float clock, speed, finish = -1f;
        bool fighter; int done;
        DissolveStyle style = DissolveStyle.Hakai;

        public static void DissolvePerson(PersonBehaviour victim, Dictionary<SpriteRenderer, Color> colours = null)
        {
            DissolvePerson(victim, DissolveStyle.Hakai, colours);
        }
        public static void DissolvePerson(PersonBehaviour victim, DissolveStyle style, Dictionary<SpriteRenderer, Color> colours = null)
        {
            if (!victim || victim.GetComponent<HakaiDissolveMarker>()) return;
            victim.gameObject.AddComponent<HakaiDissolveMarker>();
            // Hakai erases the magic matter itself. Stop an in-progress Majin recovery
            // before the dissolution reads its body and renderer state.
            var majin = victim.GetComponent<BuuRegeneration>();
            if (majin) majin.RegenerationEnabled = false;
            var go = new GameObject("DBPU Hakai disintegration");
            var d = go.AddComponent<HakaiDissolve>(); d.person = victim; d.fighter = true; d.style = style;
            var bounds = DbpuFx.BodyBounds(victim); float height = Mathf.Max(.5f, bounds.size.y);
            foreach (var limb in victim.Limbs)
            {
                if (!limb || !limb.PhysicalBehaviour || !limb.gameObject.activeInHierarchy) continue;
                var part = d.Add(limb.PhysicalBehaviour, colours);
                float top = Mathf.Clamp01((bounds.max.y - part.Bounds.max.y) / height);
                part.Start = (.05f + top * .55f) * style.TimeScale; part.End = part.Start + .55f * style.TimeScale;
                limb.Health = 0f;
            }
            try { victim.Consciousness = 0f; } catch { }
            d.Begin(bounds.center);
            if (!string.IsNullOrEmpty(style.Sound)) DbpuFx.Sound(bounds.center, style.Sound, 1f);
        }
        public static void DissolveObject(PhysicalBehaviour target, Vector2 point)
        {
            DissolveObject(target, point, DissolveStyle.Hakai);
        }
        public static void DissolveObject(PhysicalBehaviour target, Vector2 point, DissolveStyle style)
        {
            if (!target) return;
            var root = target.transform.root;
            if (root.GetComponent<HakaiDissolveMarker>()) return;
            var person = root.GetComponent<PersonBehaviour>();
            if (person) { DissolvePerson(person, style); return; }
            root.gameObject.AddComponent<HakaiDissolveMarker>();
            var go = new GameObject("DBPU Hakai disintegration");
            var d = go.AddComponent<HakaiDissolve>(); d.origin = point; d.style = style;
            Bounds all = new Bounds(point, Vector3.zero);
            foreach (var physical in root.GetComponentsInChildren<PhysicalBehaviour>())
            {
                if (!physical || !physical.gameObject.activeInHierarchy || physical.gameObject.isStatic || physical.isDisintegrated) continue;
                var part = d.Add(physical, null); all.Encapsulate(part.Bounds);
            }
            if (d.parts.Count == 0) { Destroy(go); return; }
            d.speed = Mathf.Clamp(Mathf.Max(all.extents.x, all.extents.y) * 2.2f, 3.5f, 14f);
            foreach (var part in d.parts)
            {
                Vector2 near = part.Bounds.ClosestPoint(point); float far = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 corner = new Vector2(i % 2 == 0 ? part.Bounds.min.x : part.Bounds.max.x, i < 2 ? part.Bounds.min.y : part.Bounds.max.y);
                    far = Mathf.Max(far, Vector2.Distance(corner, point));
                }
                part.Start = Vector2.Distance(near, point) / d.speed * style.TimeScale; part.End = (far / d.speed + .45f) * style.TimeScale;
            }
            d.Begin(point);
            if (!string.IsNullOrEmpty(style.Sound)) DbpuFx.Sound(point, style.Sound, 1f);
        }
        Part Add(PhysicalBehaviour physical, Dictionary<SpriteRenderer, Color> colours)
        {
            var renderers = physical.GetComponentsInChildren<SpriteRenderer>();
            var part = new Part { Physical = physical, Renderers = renderers, Colors = new Color[renderers.Length], Colliders = physical.GetComponents<Collider2D>(), Body = physical.GetComponent<Rigidbody2D>() };
            bool found = false; Bounds bounds = new Bounds(physical.transform.position, Vector3.one * .2f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c; part.Colors[i] = colours != null && colours.TryGetValue(renderers[i], out c) ? c : renderers[i].color;
                if (!renderers[i].enabled) continue;
                if (!found) { bounds = renderers[i].bounds; found = true; } else bounds.Encapsulate(renderers[i].bounds);
            }
            part.Bounds = bounds; parts.Add(part);
            return part;
        }
        void Begin(Vector2 at)
        {
            origin = at;
            stars = HakaiEffects.SparkleField("DBPU Hakai sparkles", 1100);
            squares = DbpuParticles.Create("DBPU Hakai motes", DbpuFx.Square, 260, 48);
            squares.Wind = stars.Wind; squares.Turbulence = .5f;
            foreach (var part in parts)
                if (part.Body && part.Body.bodyType == RigidbodyType2D.Dynamic) { part.Body.velocity *= .2f; part.Body.angularVelocity *= .2f; part.Body.gravityScale = 0f; }
        }
        void Update()
        {
            float dt = DbpuFx.Dt; if (dt <= 0f) return;
            clock += dt;
            if (finish >= 0f)
            {
                finish -= dt;
                if (finish <= 0f)
                {
                    if (person) Destroy(person.gameObject);
                    if (stars) stars.Release(); if (squares) squares.Release();
                    Destroy(gameObject);
                }
                return;
            }
            foreach (var part in parts)
            {
                if (part.Done) continue;
                if (!part.Physical) { part.Done = true; done++; continue; }
                if (part.Body && part.Body.bodyType == RigidbodyType2D.Dynamic) { part.Body.velocity *= Mathf.Exp(-6f * dt); part.Body.angularVelocity *= Mathf.Exp(-6f * dt); }
                if (clock < part.Start) continue;
                float k = Mathf.Clamp01((clock - part.Start) / Mathf.Max(.05f, part.End - part.Start));
                float pale = Mathf.Clamp01(k * 2.2f), alpha = 1f - DbpuFx.Smooth(.3f, 1f, k);
                for (int i = 0; i < part.Renderers.Length; i++)
                {
                    var sr = part.Renderers[i]; if (!sr) continue; Color o = part.Colors[i];
                    sr.color = new Color(Mathf.Lerp(o.r, style.Pale.r, pale), Mathf.Lerp(o.g, style.Pale.g, pale), Mathf.Lerp(o.b, style.Pale.b, pale), o.a * alpha);
                }
                float area = Mathf.Clamp(part.Bounds.size.x * part.Bounds.size.y, .02f, 12f);
                part.Debt += dt * Mathf.Clamp(area * 160f, 18f, 420f) * (1f - k * .5f);
                float front = fighter ? 0f : clock * speed;
                while (part.Debt >= 1f)
                {
                    part.Debt -= 1f;
                    Vector2 p = Sample(part, front);
                    HakaiEffects.Sparkle(stars, squares, p, fighter ? 1f : Mathf.Clamp(Mathf.Sqrt(area) * .6f, .8f, 1.6f), style.Accent, style.Fade);
                }
                if (k >= 1f)
                {
                    part.Done = true; done++;
                    for (int i = 0; i < part.Renderers.Length; i++) if (part.Renderers[i]) part.Renderers[i].enabled = false;
                    try
                    {
                        if (part.Physical && !part.Physical.isDisintegrated)
                        {
                            part.Physical.Disintegratable = true; part.Physical.Deletable = true;
                            part.Physical.Disintegrate();
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("[DBPU Hakai] " + ex.Message); }
                    if (part.Physical && part.Physical.gameObject.activeSelf && !fighter) part.Physical.gameObject.SetActive(false);
                }
            }
            if (done >= parts.Count) finish = fighter ? .15f : .05f;
        }
        Vector2 Sample(Part part, float front)
        {
            var b = part.Bounds; Vector2 best = b.center;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                Vector2 p = new Vector2(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y));
                if (front > 0f)
                {
                    float d = Vector2.Distance(p, origin);
                    if (d > front + .15f) continue;
                    if (d < front - 1.2f && attempt < 3) continue;
                }
                bool inside = part.Colliders == null || part.Colliders.Length == 0;
                if (!inside) foreach (var c in part.Colliders) if (c && c.enabled && c.OverlapPoint(p)) { inside = true; break; }
                if (inside) return p;
                best = p;
            }
            return best;
        }
    }
    public sealed class HakaiDissolveMarker : MonoBehaviour { }
}
