using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Autonomous Ultra Instinct. The old UltraInstinctEvasion was switched off in code; this
    // replaces it. In UI/MUI the body reads incoming attacks itself: projectiles, thrown
    // objects and fast limbs are slipped with a silver afterimage. Mastered UI always dodges
    // and answers with an automatic counter. Each dodge costs a sliver of ki.
    // ---------------------------------------------------------------------------------
    public sealed class UltraInstinctModule : SingularityModule
    {
        // New id on purpose: an older settings file may still hold "ui=1" from when this was on by default.
        public override string Id { get { return "ui_dodge"; } }
        public override string Title { get { return "Ultra Instinct Auto-Dodge"; } }
        public override bool DefaultOn { get { return false; } }
        public override string Summary { get { return "Off by default. When on, fighters in UI or MUI slip incoming attacks with silver afterimages and Mastered UI counters automatically. Each fighter can also be switched on or off from its head's context menu."; } }
        public const string SfxDodge = "Assets/SFX/Singularity/UI/Dodge.wav";
        public override void Boot()
        {
            DbpuFx.PreloadExtra(SfxDodge);
            // Attached to every fighter even while the module is off, so the per-fighter switch is always there.
            Singularity.PersonReadyEvent += Attach;
        }
        static void Attach(PersonBehaviour person)
        {
            if (person && person.GetComponent<KiSystem>() && !person.GetComponent<SingularityUltraInstinct>())
                person.gameObject.AddComponent<SingularityUltraInstinct>();
        }
    }

    public sealed class SingularityUltraInstinct : MonoBehaviour
    {
        static readonly Color Silver = new Color(.78f, .88f, 1f, .6f);
        readonly Collider2D[] hits = new Collider2D[48];
        readonly RaycastHit2D[] rays = new RaycastHit2D[16];
        readonly Dictionary<int, Vector2> lastCentre = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> rolled = new Dictionary<int, float>();
        PersonBehaviour person; UltraInstinctModule module;
        float cooldown, shimmer, labelCooldown, cleanup;
        PersonBehaviour counterTarget; float counterAt;

        // 0 = follow the global toggle, 1 = forced on, -1 = forced off for this fighter.
        int overrideMode;

        bool Active
        {
            get
            {
                if (overrideMode != 0) return overrideMode > 0;
                if (module == null) module = Singularity.Get<UltraInstinctModule>();
                return module != null && module.Enabled;
            }
        }

        void Awake() { person = GetComponent<PersonBehaviour>(); }

        void Start()
        {
            if (!person || person.Limbs == null || person.Limbs.Length == 0 || !person.Limbs[0]) return;
            var headPb = person.Limbs[0].GetComponent<PhysicalBehaviour>();
            if (headPb == null || headPb.ContextMenuOptions == null) return;
            headPb.ContextMenuOptions.Buttons.Add(new ContextMenuButton("SingularityUiDodgeToggle",
                (Func<string>)(() => Active ? "UI auto-dodge: turn off" : "UI auto-dodge: turn on"),
                "Ultra Instinct / MUI automatic dodging for this fighter (overrides the global setting)",
                () => { overrideMode = Active ? -1 : 1; }));
        }

        void FixedUpdate()
        {
            if (!Active || !person || !person.IsAlive()) return;
            string form = SingularityFighters.Form(person);
            bool mui = form == "MUI";
            if (form != "UI" && !mui) return;
            float dt = Time.fixedDeltaTime;
            cooldown -= dt; labelCooldown -= dt; cleanup -= dt;
            if (cleanup <= 0f) { cleanup = 2f; lastCentre.Clear(); rolled.Clear(); }
            Shimmer(dt, mui);
            if (counterTarget && Time.time >= counterAt) { Counter(counterTarget, mui); counterTarget = null; }
            if (cooldown > 0f) return;

            Vector2 centre = DbpuFx.Centre(person);
            var torso = SingularityFighters.Torso(person);
            Vector2 mine = torso && torso.PhysicalBehaviour && torso.PhysicalBehaviour.rigidbody ? torso.PhysicalBehaviour.rigidbody.velocity : Vector2.zero;
            int n = Physics2D.OverlapCircleNonAlloc(centre, 3.6f, hits);
            Collider2D threat = null; Vector2 threatVelocity = Vector2.zero; float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                if (!col || col.isTrigger || col.transform.root == person.transform.root) continue;
                Vector2 pos = col.bounds.center;
                var rb = col.attachedRigidbody;
                Vector2 vel;
                int id = col.GetInstanceID();
                if (rb && rb.bodyType == RigidbodyType2D.Dynamic) vel = rb.velocity;
                else
                {
                    Vector2 prev;
                    vel = lastCentre.TryGetValue(id, out prev) ? (pos - prev) / dt : Vector2.zero;
                    lastCentre[id] = pos;
                }
                if (rb && rb.bodyType == RigidbodyType2D.Static) continue;
                Vector2 rel = vel - mine;
                float speed = rel.magnitude;
                if (speed < 6f) continue;
                Vector2 to = centre - pos;
                float dist = Mathf.Max(.05f, to.magnitude);
                float closing = Vector2.Dot(rel, to / dist);
                if (closing < 5f) continue;
                float tti = dist / closing;
                if (tti > .3f) continue;
                Vector2 dir = rel / speed;
                float miss = Mathf.Abs(dir.x * to.y - dir.y * to.x);
                if (miss > 1.15f) continue;
                if (tti < best) { best = tti; threat = col; threatVelocity = rel; }
            }
            if (!threat) return;
            int tid = threat.GetInstanceID();
            float seen;
            if (rolled.TryGetValue(tid, out seen) && Time.time - seen < .6f) return;
            rolled[tid] = Time.time;
            if (!mui && UnityEngine.Random.value > .7f) return;           // the Sign is not yet perfect
            if (!SingularityFighters.Spend(person, mui ? .006f : .012f)) return;
            Dodge(threat, threatVelocity, centre, mui);
        }

        void Dodge(Collider2D threat, Vector2 velocity, Vector2 centre, bool mui)
        {
            Vector2 dir = velocity.normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 away = centre - (Vector2)threat.bounds.center;
            if (Vector2.Dot(side, away) < 0f) side = -side;
            Vector2 offset = side * (mui ? 1.25f : .95f) + Vector2.up * .2f;
            // Don't slip into solid ground.
            int found = Physics2D.RaycastNonAlloc(centre, offset.normalized, rays, offset.magnitude + .4f);
            for (int i = 0; i < found; i++)
            {
                var wall = rays[i].collider;
                if (wall && !wall.isTrigger && wall.attachedRigidbody == null) { offset = -offset * .8f; break; }
            }
            SingularityFx.Afterimage(person, Silver, mui ? .45f : .35f);
            SingularityFighters.Translate(person, offset);
            cooldown = mui ? .1f : .2f;
            DbpuFx.Sound(centre, UltraInstinctModule.SfxDodge, .7f);
            if (labelCooldown <= 0f)
            {
                labelCooldown = 4f;
                DbpuWorldLabel.Show(person, mui ? "MASTERED ULTRA INSTINCT" : "ULTRA INSTINCT", "THE BODY MOVES ON ITS OWN", 1.6f, -1f, new Color(.85f, .92f, 1f));
            }
            var limb = threat.GetComponent<LimbBehaviour>();
            var attacker = limb ? limb.Person : null;
            if (attacker && attacker != person && attacker.IsAlive() && (mui || UnityEngine.Random.value < .3f)
                && Vector2.Distance(DbpuFx.Centre(attacker), centre) < 3.2f)
            {
                counterTarget = attacker; counterAt = Time.time + .05f;
            }
        }

        void Counter(PersonBehaviour attacker, bool mui)
        {
            if (!attacker || !attacker.IsAlive()) return;
            Vector2 from = DbpuFx.Centre(person), to = DbpuFx.Centre(attacker);
            Vector2 dir = (to - from).normalized;
            if (dir.sqrMagnitude < .5f) dir = Vector2.right;
            float ratio = SingularityFighters.PowerRatio(person, attacker);
            float k = Mathf.Clamp(4f + Mathf.Log10(1f + ratio) * 6f, 4f, 16f) * (mui ? 1.25f : 1f);
            SingularityFighters.Push(attacker, dir * k + Vector2.up * k * .35f);
            LimbBehaviour hand = null; float nearest = float.MaxValue;
            if (person.Limbs != null)
                foreach (var limb in person.Limbs)
                {
                    if (!KiHandLoadout.IsStrikeLimb(limb)) continue;
                    float d = Vector2.Distance(limb.transform.position, to);
                    if (d < nearest) { nearest = d; hand = limb; }
                }
            if (hand) DbpuHandTechniques.PullHand(hand, to, 260f);
            SingularityFx.Flash(to, new Color(.9f, .95f, 1f, .95f), 1.1f, .25f);
            SingularityFx.Ring(to, new Color(.75f, .88f, 1f, .9f), 1.8f, .3f, 1f);
            SingularityFx.Burst(to, new Color(.75f, .88f, 1f), 1f, 14);
            SingularityTime.Hitstop(.07f);
            DbpuFx.Shake(to, 1.5f);
            DbpuFx.Sound(to, "Assets/SFX/Techniques/KiFist/HitHeavy.wav", .9f);
        }

        void Shimmer(float dt, bool mui)
        {
            shimmer -= dt;
            if (shimmer > 0f) return;
            shimmer = mui ? .12f : .2f;
            var b = DbpuFx.BodyBounds(person);
            Vector2 p = new Vector2(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y));
            SingularityFx.Burst(p, new Color(.8f, .9f, 1f), .35f, 3);
        }
    }

    // ---------------------------------------------------------------------------------
    // Ultra Ego: Battle Lust. The existing UltraEgoDamageBoost already converts missing
    // health into power; this makes it visible and dangerous. A lust meter rises with every
    // hit taken, destruction sparks grow, and at 80% lust each strike carries Hakai energy:
    // a clearly weaker opponent is marked for destruction (the Hakai resist rules apply).
    // ---------------------------------------------------------------------------------
    public sealed class UltraEgoModule : SingularityModule
    {
        public override string Id { get { return "ue"; } }
        public override string Title { get { return "Ultra Ego: Battle Lust"; } }
        public override string Summary { get { return "In Ultra Ego every hit taken feeds a Battle Lust meter: more power, hungrier destruction sparks, and at 80% lust strikes carry Hakai energy."; } }

        public override void Boot() { Singularity.LimbImpact += OnImpact; }

        public override void PersonReady(PersonBehaviour person)
        {
            if (person && person.GetComponent<KiSystem>() && !person.GetComponent<SingularityUltraEgo>())
                person.gameObject.AddComponent<SingularityUltraEgo>();
        }

        void OnImpact(SingularityImpact i)
        {
            if (!Enabled || i.OtherPerson == null || i.Speed < 6f) return;
            Strike(i.Person, i.Limb, i.OtherPerson, i.Point);
            if (i.OtherLimb) Strike(i.OtherPerson, i.OtherLimb, i.Person, i.Point);
        }

        static void Strike(PersonBehaviour attacker, LimbBehaviour limb, PersonBehaviour victim, Vector2 point)
        {
            if (!attacker || !victim || attacker == victim || !KiHandLoadout.IsStrikeLimb(limb)) return;
            var ue = attacker.GetComponent<SingularityUltraEgo>();
            if (ue) ue.TryDestructionStrike(victim, point);
        }
    }

    public sealed class SingularityUltraEgo : MonoBehaviour
    {
        public static readonly Color Purple = new Color(.72f, .3f, 1f, 1f);
        public float Lust { get; private set; }
        public bool Active { get; private set; }
        PersonBehaviour person; UltraEgoModule module; DbpuParticles sparks;
        float baseline, shown = -1f, labelClock, emitDebt, strikeCooldown; bool awakened;

        void Awake() { person = GetComponent<PersonBehaviour>(); }

        void Update()
        {
            if (module == null) module = Singularity.Get<UltraEgoModule>();
            bool on = module != null && module.Enabled && person && person.IsAlive() && SingularityFighters.Form(person) == "UE";
            if (on && !Active)
            {
                Active = true; awakened = false; Lust = 0f; shown = -1f;
                baseline = Mathf.Max(.3f, SingularityFighters.HealthFraction(person));
                DbpuWorldLabel.Show(person, "ULTRA EGO", "THE MORE I'M HIT, THE STRONGER I GET", 2.4f, 0f, Purple, Purple);
                if (!sparks) sparks = HakaiEffects.SparkleField("DBPU Ultra Ego sparks", 400);
            }
            else if (!on && Active)
            {
                Active = false;
                if (sparks) { sparks.Release(); sparks = null; }
            }
            if (!Active) return;
            float dt = DbpuFx.Dt;
            strikeCooldown -= dt;
            float hf = SingularityFighters.HealthFraction(person);
            Lust = Mathf.Clamp01((baseline - hf) / Mathf.Max(.2f, baseline * .8f));
            float multiplier = 1f + Lust;   // matches UltraEgoDamageBoost's default 2x ceiling
            if (sparks)
            {
                var b = DbpuFx.BodyBounds(person);
                emitDebt += dt * (8f + 90f * Lust);
                while (emitDebt >= 1f)
                {
                    emitDebt -= 1f;
                    Vector2 p = new Vector2(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y));
                    HakaiEffects.Sparkle(sparks, null, p, .7f + Lust * .8f, Color.Lerp(HakaiEffects.Lavender, Purple, Lust), HakaiEffects.Violet);
                }
            }
            labelClock -= dt;
            if (labelClock <= 0f && Mathf.Abs(Lust - shown) > .03f)
            {
                labelClock = .5f; shown = Lust;
                DbpuWorldLabel.Show(person, "ULTRA EGO", "BATTLE LUST  ×" + multiplier.ToString("0.00"), 1.2f, Lust, Purple, Purple);
            }
            if (!awakened && Lust >= .8f)
            {
                awakened = true;
                Vector2 c = DbpuFx.Centre(person);
                SingularityFx.Ring(c, Purple, 3.5f, .6f, 2f);
                SingularityFx.Burst(c, Purple, 1.5f, 34);
                DbpuFx.Shake(c, 2.5f);
                DbpuFx.Sound(c, "Assets/SFX/Techniques/Hakai/Cast.wav", 1f);
                DbpuWorldLabel.Show(person, "DESTRUCTION AWAKENS", "EVERY STRIKE CARRIES HAKAI", 2.4f, Lust, Purple, Purple);
            }
        }

        public void TryDestructionStrike(PersonBehaviour victim, Vector2 point)
        {
            if (!Active || Lust < .8f || strikeCooldown > 0f || !victim || !victim.IsAlive()) return;
            if (victim.GetComponent<HakaiDissolveMarker>() || victim.GetComponent<HakaiOnPerson>()) return;
            strikeCooldown = 5f;
            float ratio = SingularityFighters.PowerRatio(person, victim) * (1f + Lust);
            HakaiEffects.PalmBurst(point, 1.2f);
            SingularityTime.Hitstop(.09f);
            if (ratio >= 1.6f)
            {
                HakaiOnPerson.Begin(person, victim, point);
                DbpuWorldLabel.Show(person, "DESTRUCTION STRIKE", "HAKAI", 1.6f, Lust, Purple, Purple);
            }
            else
            {
                Vector2 dir = ((Vector2)DbpuFx.Centre(victim) - (Vector2)DbpuFx.Centre(person)).normalized;
                SingularityFighters.Push(victim, dir * 9f + Vector2.up * 3f);
                DbpuWorldLabel.Show(victim, "RESISTED", "TOO STRONG TO DESTROY", 1.4f, -1f, Purple);
            }
        }

        void OnDestroy() { if (sparks) sparks.Release(); }
    }

    // ---------------------------------------------------------------------------------
    // Zenkai, lore-accurate. A Saiyan beaten to the brink who recovers (Senzu Bean, a wish,
    // regeneration) comes back permanently stronger. Repeated Zenkai give diminishing gains.
    // Replaces the old continuous damage-to-power trickle while this module is on.
    // ---------------------------------------------------------------------------------
    public sealed class ZenkaiModule : SingularityModule
    {
        public override string Id { get { return "zenkai"; } }
        public override string Title { get { return "Zenkai Revival"; } }
        public override string Summary { get { return "Saiyans beaten to the brink who recover (Senzu, wish, regeneration) come back permanently stronger."; } }
        public const string SfxZenkai = "Assets/SFX/Singularity/Zenkai/Zenkai.wav";
        public override void Boot() { DbpuFx.PreloadExtra(SfxZenkai); }
        public override void PersonReady(PersonBehaviour person)
        {
            if (person && person.GetComponent<KiSystem>() && SingularityFighters.IsSaiyan(person) && !person.GetComponent<SingularityZenkai>())
                person.gameObject.AddComponent<SingularityZenkai>();
        }
    }

    public sealed class SingularityZenkai : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, .82f, .3f, 1f);
        PersonBehaviour person; ZenkaiModule module;
        bool brink; float lowest = 1f, clock; int count;
        public int Count { get { return count; } }

        void Awake() { person = GetComponent<PersonBehaviour>(); }

        void Update()
        {
            clock -= Time.unscaledDeltaTime;
            if (clock > 0f) return;
            clock = .2f;
            if (module == null) module = Singularity.Get<ZenkaiModule>();
            if (module == null || !module.Enabled || !person) return;
            float hf = SingularityFighters.HealthFraction(person);
            bool alive = person.IsAlive();
            bool conscious = person.Consciousness > .5f;
            if (hf < .22f || !alive || (!conscious && hf < .45f))
            {
                if (!brink && alive) DbpuWorldLabel.Show(person, "ON THE BRINK", "SURVIVE THIS...", 1.6f, -1f, new Color(1f, .5f, .4f));
                brink = true; lowest = Mathf.Min(lowest, hf);
            }
            else if (brink && alive && conscious && hf > .7f) Trigger();
        }

        void Trigger()
        {
            brink = false; count++;
            float gain = Mathf.Clamp(.25f + (.22f - lowest) * 1.2f, .25f, .5f) / Mathf.Sqrt(count);
            lowest = 1f;
            var root = person.transform.root.gameObject;
            var multipliers = root.GetComponent<MultipliersAndStuffs>();
            if (!multipliers) multipliers = root.AddComponent<MultipliersAndStuffs>();
            float max = Mathf.Max(1.5f, DBPU.Special_ZenkaiBoostMax);
            multipliers.BoostMultiplier = Mathf.Min(Mathf.Max(1f, multipliers.BoostMultiplier) * (1f + gain), max);
            Vector2 c = DbpuFx.Centre(person);
            SingularityFx.Flash(c, Gold, 3f, .6f);
            SingularityFx.Ring(c, Gold, 4f, .7f, 2f);
            SingularityFx.Burst(c, Gold, 1.6f, 36);
            DbpuFx.Shake(c, 2f);
            DbpuFx.Sound(c, ZenkaiModule.SfxZenkai, 1f);
            DbpuWorldLabel.Show(person, "ZENKAI BOOST", "+" + Mathf.RoundToInt(gain * 100f) + "% POWER  ·  ×" + multipliers.BoostMultiplier.ToString("0.00") + " TOTAL", 3f, -1f, Gold);
        }
    }
}
