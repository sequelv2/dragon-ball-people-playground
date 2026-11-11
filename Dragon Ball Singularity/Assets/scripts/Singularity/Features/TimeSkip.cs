using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DBPU
{
    // Hit of Universe 6.
    //  Time Skip: the world stops for about a second; only the user moves. Every blow landed
    //  while time is stopped is stored and lands at once when it resumes.
    //  Time Prison: click a fighter to seal them in a pocket of stopped time for six seconds.
    //  A fighter far stronger than the caster can break out by charging ki (as Jiren did).
    // Granted with the Power Giver (or automatically to a character whose id contains "Hit").
    public sealed class TimeSkipModule : SingularityModule
    {
        public override string Id { get { return "timeskip"; } }
        public override string Title { get { return "Hit: Time Skip & Time Prison"; } }
        public override string Summary { get { return "Power Giver techniques. Time Skip (J with the fighter selected) freezes the world and delivers every stored blow at once; Time Prison seals a clicked fighter."; } }

        public const string SkipId = "TimeSkip", PrisonId = "TimePrison";
        public const string ActionId = "dbpu.singularity.timeskip";
        public const string SfxSkip = "Assets/SFX/Singularity/Time/Skip.wav";
        public const string SfxResume = "Assets/SFX/Singularity/Time/Resume.wav";
        public const string SfxPrison = "Assets/SFX/Singularity/Time/Prison.wav";
        public static readonly Color Violet = new Color(.68f, .45f, 1f, 1f);
        static Sprite skipIcon, prisonIcon;

        public override void Boot()
        {
            try { ModAPI.RegisterInput("Time Skip (Hit) - fighter selected; Shift = Time Prison", ActionId, KeyCode.J); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] Time Skip input: " + ex.Message); }
            try { skipIcon = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/TimeSkip.png"); } catch { skipIcon = null; }
            try { prisonIcon = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/TimePrison.png"); } catch { prisonIcon = null; }
            DbpuFx.PreloadExtra(SfxSkip, SfxResume, SfxPrison);
        }

        public static bool Knows(PersonBehaviour person, string id)
        {
            if (!person || !person.GetComponent<KiSystem>()) return false;
            if (DbpuGrantedTechniques.Has(person, id)) return true;
            string cid = SingularityFighters.CharacterId(person);
            return cid.Equals("Hit", StringComparison.OrdinalIgnoreCase) || cid.EndsWith("/Hit", StringComparison.OrdinalIgnoreCase);
        }

        // Power Giver entry point.
        public static void Grant(PersonBehaviour person, CharacterPowers powers, string id)
        {
            if (!person) return;
            if (!person.GetComponent<KiSystem>()) { ModAPI.Notify("Hit's techniques need a ki user (a Dragon Ball character)."); return; }
            DbpuGrantedTechniques.Add(person, id);
            Register(person, powers, id);
        }

        public static void Register(PersonBehaviour person, CharacterPowers powers, string id)
        {
            if (!person || !powers || powers.PowerData.ContainsKey(id)) return;
            bool skip = id == SkipId;
            var icon = skip ? skipIcon : prisonIcon;
            if (!icon) icon = skip ? DBPU.Icon_InstantTransmission : DBPU.Icon_KiShield;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = skip ? "Time Skip" : "Time Prison",
                Description = skip ? "Stop time for about a second. Blows landed meanwhile all strike at once when time resumes. Also: J with this fighter selected."
                                   : "Click a fighter to seal them in stopped time for six seconds. A much stronger fighter can break out by charging ki. Also: Shift+J.",
                Icon = icon, Illustration = icon,
                IsActive = () => skip ? SingularityWorldFreeze.Active : TimePrisonCaster.ArmedBy(person),
                GetStatusText = () => skip ? "Press to skip time" : (TimePrisonCaster.ArmedBy(person) ? "Armed: click a fighter" : "Press to arm"),
                Apply = () => { if (skip) TimeSkip.Trigger(person); else TimePrisonCaster.Arm(person); },
                Disable = () => { if (!skip) TimePrisonCaster.Disarm(); }
            });
        }

        public override void PersonReady(PersonBehaviour person)
        {
            var powers = person ? person.GetComponent<CharacterPowers>() : null;
            if (!powers) return;
            if (Knows(person, SkipId)) Register(person, powers, SkipId);
            if (Knows(person, PrisonId)) Register(person, powers, PrisonId);
        }

        public override void Tick(float dt)
        {
            TimePrisonCaster.Tick();
            if (KiHandLoadout.IsInputCaptured || DialogBox.IsAnyDialogboxOpen) return;
            if (!DbpuInputUtil.Down(ActionId, KeyCode.J)) return;
            bool prison = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            foreach (var p in SingularityRunner.Persons)
            {
                if (!p || !p.IsAlive() || !Selected(p)) continue;
                if (prison)
                {
                    if (Knows(p, PrisonId)) { TimePrisonCaster.Arm(p); return; }
                }
                else if (Knows(p, SkipId)) { TimeSkip.Trigger(p); return; }
            }
        }

        static bool Selected(PersonBehaviour person)
        {
            if (person.Limbs == null) return false;
            foreach (var limb in person.Limbs)
                if (limb && limb.PhysicalBehaviour && CeceUtils.IsSelected(limb.PhysicalBehaviour)) return true;
            return false;
        }

        public override void OnDisabled() { TimePrisonCaster.Disarm(); }
    }

    public static class TimeSkip
    {
        static readonly Dictionary<int, float> cooldown = new Dictionary<int, float>();
        public const float Duration = 1.1f, KiCost = .12f;

        public static void Trigger(PersonBehaviour person)
        {
            var module = Singularity.Get<TimeSkipModule>();
            if (module == null || !module.Enabled || !person || !person.IsAlive()) return;
            if (SingularityWorldFreeze.Active || RealityRift.Active) return;
            float until;
            if (cooldown.TryGetValue(person.GetInstanceID(), out until) && Time.unscaledTime < until) return;
            if (!SingularityFighters.Spend(person, KiCost)) { DbpuWorldLabel.Show(person, "TIME SKIP", "NOT ENOUGH KI", 1.2f); return; }
            cooldown[person.GetInstanceID()] = Time.unscaledTime + Duration + 1.2f;
            var freeze = SingularityWorldFreeze.Begin(new[] { person.transform }, Duration, f => Resume(person, f));
            if (!freeze) return;
            var trail = person.gameObject.AddComponent<TimeSkipTrail>();
            trail.Freeze = freeze;
            if (person.Limbs != null)
                foreach (var limb in person.Limbs)
                    if (limb) { var s = limb.gameObject.AddComponent<TimeSkipStriker>(); s.Freeze = freeze; s.Limb = limb; }
            SingularityVeil.Show("timeskip", new Color(.35f, .25f, .55f, 1f), .32f, .08f, "Top", 850);
            DbpuFx.Sound(DbpuFx.Centre(person), TimeSkipModule.SfxSkip, 1f);
            DbpuWorldLabel.Show(person, "TIME SKIP", "THE WORLD STANDS STILL", Duration, -1f, TimeSkipModule.Violet);
        }

        static void Resume(PersonBehaviour person, SingularityWorldFreeze freeze)
        {
            SingularityVeil.Hide("timeskip", .25f);
            if (person)
            {
                foreach (var s in person.GetComponentsInChildren<TimeSkipStriker>()) UnityEngine.Object.Destroy(s);
                var trail = person.GetComponent<TimeSkipTrail>();
                if (trail) UnityEngine.Object.Destroy(trail);
            }
            var points = TimeSkipStriker.TakePoints(freeze);
            Vector2 at = person ? DbpuFx.Centre(person) : Vector2.zero;
            DbpuFx.Sound(at, TimeSkipModule.SfxResume, 1f);
            foreach (var p in points)
            {
                SingularityFx.Flash(p, Color.white, .9f, .25f);
                SingularityFx.Ring(p, TimeSkipModule.Violet, 1.4f, .35f, 1f);
                SingularityFx.Burst(p, TimeSkipModule.Violet, .8f, 10);
            }
            if (points.Count > 0)
            {
                SingularityTime.Hitstop(.1f);
                DbpuFx.Shake(at, 1.5f + points.Count * .3f);
                DbpuFx.Sound(points[0], "Assets/SFX/Techniques/KiFist/HitHeavy.wav", 1f);
                if (person) DbpuWorldLabel.Show(person, "TIME SKIP", points.Count + (points.Count == 1 ? " STRIKE" : " STRIKES") + " LANDED AT ONCE", 1.8f, -1f, TimeSkipModule.Violet);
            }
        }
    }

    // Purple afterimages trace the skipper's motion through stopped time.
    public sealed class TimeSkipTrail : MonoBehaviour
    {
        public SingularityWorldFreeze Freeze;
        PersonBehaviour person; float clock;
        void Awake() { person = GetComponent<PersonBehaviour>(); }
        void Update()
        {
            if (!Freeze) { Destroy(this); return; }
            clock -= Time.unscaledDeltaTime;
            if (clock > 0f) return;
            clock = .07f;
            SingularityFx.Afterimage(person, new Color(.75f, .55f, 1f, .45f), .45f);
        }
    }

    public sealed class TimeSkipStriker : MonoBehaviour
    {
        public SingularityWorldFreeze Freeze;
        public LimbBehaviour Limb;
        static readonly Dictionary<SingularityWorldFreeze, List<Vector2>> points = new Dictionary<SingularityWorldFreeze, List<Vector2>>();
        static readonly ContactPoint2D[] contacts = new ContactPoint2D[2];
        float nextHit;

        public static List<Vector2> TakePoints(SingularityWorldFreeze freeze)
        {
            List<Vector2> list;
            if (freeze != null && points.TryGetValue(freeze, out list)) { points.Remove(freeze); return list; }
            return new List<Vector2>();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Freeze || !Limb || collision == null || Time.unscaledTime < nextHit) return;
            var other = collision.rigidbody;
            if (!other || Freeze.IsExempt(other.transform)) return;
            var mine = GetComponent<Rigidbody2D>();
            Vector2 v = mine ? mine.velocity : collision.relativeVelocity;
            if (v.magnitude < 2.5f) return;
            nextHit = Time.unscaledTime + .12f;
            Vector2 point = transform.position;
            if (collision.GetContacts(contacts) > 0) point = contacts[0].point;
            float power = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, (float)DbpuFx.PowerOf(Limb.Person))) * .02f, 1f, 6f);
            Vector2 impulse = v.normalized * Mathf.Clamp(v.magnitude, 4f, 30f) * (mine ? mine.mass : 1f) * 2.5f * power;
            Freeze.QueueHit(other, impulse, point);
            List<Vector2> list;
            if (!points.TryGetValue(Freeze, out list)) { list = new List<Vector2>(); points[Freeze] = list; }
            list.Add(point);
            // A white cross marks where a blow is waiting to land.
            SingularityFx.Flash(point, new Color(1f, 1f, 1f, .7f), .35f, Freeze.Remaining + .1f);
        }
    }

    // Time Prison targeting: armed from the powers menu or Shift+J, then click a fighter.
    public static class TimePrisonCaster
    {
        static PersonBehaviour caster;
        static SpriteRenderer reticle;
        public static bool ArmedBy(PersonBehaviour person) { return caster && caster == person; }

        public static void Arm(PersonBehaviour person)
        {
            if (!person || !person.IsAlive()) return;
            if (caster == person) { Disarm(); return; }
            caster = person;
            DbpuWorldLabel.Show(person, "TIME PRISON", "CLICK A FIGHTER  -  RIGHT CLICK CANCELS", 2.4f, -1f, TimeSkipModule.Violet);
        }

        public static void Disarm()
        {
            caster = null;
            if (reticle) reticle.enabled = false;
        }

        public static void Tick()
        {
            if (!caster) { if (reticle) reticle.enabled = false; return; }
            if (!caster.IsAlive() || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Disarm(); return; }
            Vector2 mouse = DbpuFx.MouseWorld();
            if (!reticle)
            {
                var go = new GameObject("DBPU time prison reticle");
                UnityEngine.Object.DontDestroyOnLoad(go);
                reticle = DbpuFx.MakeSprite(go.transform, "reticle", DbpuFx.RingThick, 70, true);
            }
            float pulse = 1f + .08f * Mathf.Sin(Time.unscaledTime * 8f);
            DbpuFx.Place(reticle, mouse, -.5f, .8f * pulse, .8f * pulse, Time.unscaledTime * 90f, DbpuFx.A(TimeSkipModule.Violet, .8f));
            Vector2 click;
            if (!DbpuClickFire.TryConsume(out click)) return;
            var hit = DbpuTechniqueTargeting.Resolve(caster, click);
            var limb = hit ? hit.GetComponent<LimbBehaviour>() : null;
            var victim = limb ? limb.Person : null;
            if (!victim || victim == caster) { DbpuWorldLabel.Show(caster, "TIME PRISON", "NO FIGHTER THERE", 1f); return; }
            var by = caster;
            Disarm();
            if (!SingularityFighters.Spend(by, .2f)) { DbpuWorldLabel.Show(by, "TIME PRISON", "NOT ENOUGH KI", 1.2f); return; }
            TimePrison.Seal(by, victim, 6f);
        }
    }

    public sealed class TimePrison : MonoBehaviour
    {
        struct Saved { public Rigidbody2D Body; public RigidbodyConstraints2D Constraints; }
        readonly List<Saved> saved = new List<Saved>();
        PersonBehaviour caster, victim; float life, age, breakout;
        SpriteRenderer bubble, ringA, ringB;

        public static void Seal(PersonBehaviour caster, PersonBehaviour victim, float seconds)
        {
            if (!victim || victim.GetComponent<TimePrison>()) return;
            var prison = victim.gameObject.AddComponent<TimePrison>();
            prison.caster = caster; prison.victim = victim; prison.life = seconds;
            foreach (var body in SingularityFighters.Bodies(victim))
            {
                prison.saved.Add(new Saved { Body = body, Constraints = body.constraints });
                body.velocity = Vector2.zero; body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            var go = new GameObject("DBPU time prison");
            go.transform.SetParent(prison.transform, false);
            prison.bubble = DbpuFx.MakeSprite(go.transform, "bubble", DbpuFx.Sphere, 58, true);
            prison.ringA = DbpuFx.MakeSprite(go.transform, "clock ring", DbpuFx.Ring, 59, true);
            prison.ringB = DbpuFx.MakeSprite(go.transform, "clock ticks", DbpuFx.Star, 59, true);
            Vector2 c = DbpuFx.Centre(victim);
            DbpuFx.Sound(c, TimeSkipModule.SfxPrison, 1f);
            SingularityFx.Ring(c, TimeSkipModule.Violet, 2.5f, .5f, 2f);
            DbpuWorldLabel.Show(victim, "TIME PRISON", "SEALED IN STOPPED TIME", 2f, 1f, TimeSkipModule.Violet, TimeSkipModule.Violet);
        }

        void Update()
        {
            if (!victim) { Destroy(this); return; }
            float dt = DbpuFx.Dt;
            age += dt;
            var bounds = DbpuFx.BodyBounds(victim);
            float r = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.25f + .2f;
            Vector2 c = bounds.center;
            float wobble = 1f + .03f * Mathf.Sin(age * 5f);
            DbpuFx.Place(bubble, c, -.3f, r * 2f * wobble, r * 2f * wobble, 0f, new Color(.6f, .45f, 1f, .28f));
            DbpuFx.Place(ringA, c, -.31f, r * 2.1f, r * 2.1f, -age * 40f, new Color(.85f, .75f, 1f, .7f));
            DbpuFx.Place(ringB, c, -.32f, r * 1.2f, r * 1.2f, age * 120f, new Color(1f, 1f, 1f, .35f));
            // Raw power can shatter the prison (Jiren): far stronger and charging ki.
            bool straining = DbpuFx.IsKiCharging(victim) && SingularityFighters.PowerRatio(victim, caster) > 1.4f;
            breakout = Mathf.Clamp01(breakout + (straining ? dt / 1.5f : -dt * .5f));
            float left = Mathf.Clamp01(1f - age / life);
            DbpuWorldLabel.Show(victim, breakout > .05f ? "IT'S BREAKING!" : "TIME PRISON", breakout > .05f ? "RAW POWER AGAINST STOPPED TIME" : "SEALED IN STOPPED TIME", .3f, breakout > .05f ? breakout : left, TimeSkipModule.Violet, breakout > .05f ? new Color(1f, .5f, .4f) : TimeSkipModule.Violet);
            if (breakout >= 1f) { Release(true); return; }
            if (age >= life) Release(false);
        }

        void Release(bool shattered)
        {
            Vector2 c = victim ? DbpuFx.Centre(victim) : (Vector2)transform.position;
            foreach (var s in saved) if (s.Body) s.Body.constraints = s.Constraints;
            saved.Clear();
            if (shattered)
            {
                SingularityFx.Burst(c, new Color(.8f, .7f, 1f), 1.6f, 30);
                SingularityFx.Ring(c, Color.white, 4f, .5f, 2f);
                DbpuFx.Shake(c, 3f);
                if (victim) DbpuWorldLabel.Show(victim, "SHATTERED", "THE PRISON COULD NOT HOLD", 1.8f, -1f, new Color(1f, .55f, .45f));
            }
            else SingularityFx.Flash(c, TimeSkipModule.Violet, 2f, .4f);
            if (bubble) Destroy(bubble.transform.parent.gameObject);
            Destroy(this);
        }

        void OnDestroy()
        {
            foreach (var s in saved) if (s.Body) s.Body.constraints = s.Constraints;
            saved.Clear();
            if (bubble) Destroy(bubble.transform.parent.gameObject);
        }
    }
}
