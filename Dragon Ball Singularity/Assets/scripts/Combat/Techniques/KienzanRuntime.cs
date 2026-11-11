using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Eligibility and input helpers shared by the hand techniques added in this update.
    public static class DbpuHandTechniques
    {
        static bool Has(string text, string token) { return !string.IsNullOrEmpty(text) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0; }
        public static string CharacterId(PersonBehaviour person)
        {
            if (!person) return "";
            var identity = person.GetComponent<DbpuCharacterIdentity>();
            return identity && !string.IsNullOrEmpty(identity.CharacterId) ? identity.CharacterId : person.name;
        }
        public static bool CanKienzan(PersonBehaviour person)
        {
            if (!person || !person.GetComponent<KiSystem>()) return false;
            string id = CharacterId(person);
            return Has(id, "Krillin") || Has(id, "Kuririn") || Has(person.name, "Krillin") || DbpuGrantedTechniques.Has(person, KiHandLoadout.KienzanId);
        }
        // Characters who know destruction by identity (not by a form).
        public static bool KnowsHakai(PersonBehaviour person)
        {
            if (!person || !person.GetComponent<KiSystem>()) return false;
            string id = CharacterId(person);
            return Has(id, "Beerus") || Has(person.name, "Beerus") || Has(id, "Champa") || Has(id, "Belmod") || Has(id, "Toppo");
        }
        public static bool CanHakai(PersonBehaviour person)
        {
            return NativeHakai(person) || (person && person.GetComponent<KiSystem>() && DbpuGrantedTechniques.Has(person, KiHandLoadout.HakaiId));
        }
        public static bool CanHakaiBall(PersonBehaviour person)
        {
            return NativeHakai(person) || (person && person.GetComponent<KiSystem>() && DbpuGrantedTechniques.Has(person, KiHandLoadout.HakaiBallId));
        }
        static bool NativeHakai(PersonBehaviour person)
        {
            if (!person) return false;
            var ki = person.GetComponent<KiSystem>(); if (!ki) return false;
            if (KnowsHakai(person)) return true;
            string form = ki.CurrentForm ?? "";
            if (Has(form, "Hakai") || Has(form, "Destroy") || form == "UE" || Has(form, "UltraEgo")) return true;
            try
            {
                var custom = CustomTransformationCatalog.GetForm(CharacterId(person), form);
                if (custom != null && (Has(custom.Name, "Hakai") || Has(custom.Name, "Destroy") || Has(custom.Name, "Ultra Ego"))) return true;
            }
            catch { }
            var aura = person.GetComponent<AuraHandler>();
            return aura && aura.CurrentAuraID == "UE";
        }
        public static float CostScale { get { return Mathf.Clamp(KiCombatMath.Finite(DBPU.KiAttackCostMultiplier, 1f), 0f, 100f); } }
        public static bool Spend(KiSystem ki, float fraction)
        {
            if (!ki) return false;
            float cost = KiCombatMath.MaxKi(ki) * fraction * CostScale;
            if (cost > 0f && ki.currentKi < cost) return false;
            ki.currentKi = Mathf.Max(0f, ki.currentKi - cost); return true;
        }
        public static void Drain(KiSystem ki, float fractionPerSecond, float dt)
        {
            if (!ki) return;
            ki.currentKi = Mathf.Max(0f, ki.currentKi - KiCombatMath.MaxKi(ki) * fractionPerSecond * dt * CostScale);
        }
        public static LimbBehaviour UpperArmOf(LimbBehaviour hand)
        {
            if (!hand || !hand.Person || hand.Person.Limbs == null) return null;
            string wanted = hand.name.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0 ? "UpperArmFront" : "UpperArm";
            foreach (var limb in hand.Person.Limbs) if (limb && limb.name == wanted) return limb;
            return null;
        }
        // Gently draws a hand towards a world point (raised palm, extended arm) without fighting the ragdoll.
        public static void PullHand(LimbBehaviour hand, Vector2 target, float stiffness = 120f)
        {
            var body = hand ? hand.GetComponent<Rigidbody2D>() : null;
            if (!body || body.bodyType != RigidbodyType2D.Dynamic) return;
            Vector2 error = target - KiCombatMath.HandPoint(hand);
            Vector2 force = (error * stiffness - body.velocity * (stiffness * .1f)) * body.mass;
            body.AddForce(Vector2.ClampMagnitude(force, body.mass * 380f));
        }
        public static Vector2 Shoulder(LimbBehaviour hand)
        {
            var upper = UpperArmOf(hand);
            if (upper && upper.Joint) return upper.transform.TransformPoint(upper.Joint.anchor);
            return upper ? (Vector2)upper.transform.position : (hand ? (Vector2)hand.transform.position : Vector2.zero);
        }
    }

    // Use-key tracking identical to the existing Ki Fist/Share Ki hands: a tap toggles,
    // a held Use (or F held directly) releases on key-up.
    internal sealed class DbpuHandHold
    {
        KeyCode key = KeyCode.F, secondary = KeyCode.None;
        bool directHeld, seen; int startFrame;
        public void Begin()
        {
            key = KeyCode.F; secondary = KeyCode.None;
            InputAction action;
            if (InputSystem.Actions.TryGetValue("activateDirect", out action)) { key = action.Key; secondary = action.SecondaryKey; }
            directHeld = Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary));
            seen = false; startFrame = Time.frameCount;
        }
        public bool Continuous(LimbBehaviour limb)
        {
            bool direct = directHeld && Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary));
            bool held = (limb && limb.PhysicalBehaviour && limb.PhysicalBehaviour.IsBeingUsedContinuously()) || direct;
            if (held) seen = true;
            return held;
        }
        public bool Released(LimbBehaviour limb)
        {
            bool held = Continuous(limb);
            return (seen || directHeld) && !held && Time.frameCount > startFrame + 1;
        }
    }

    // Krillin's Kienzan (Destructo Disc): hold Use on the hand to form the spinning disc
    // above the raised palm, release to throw it at the cursor. It slices through every
    // limb it crosses. Tap Use again while it flies and hold to steer it. A long charge
    // throws a Double Kienzan.
    public sealed class KienzanHand : MonoBehaviour
    {
        public bool Equipped;
        public LimbBehaviour Limb { get; private set; }
        public PersonBehaviour Person { get; private set; }
        public bool IsCharging { get { return charging; } }
        public float Charge01 { get { return Mathf.Clamp01(charge / FullCharge); } }
        public const float FullCharge = 1.3f, DoubleCharge = 2.8f;
        public string Status
        {
            get
            {
                if (charging) return charge >= DoubleCharge ? "DOUBLE KIENZAN READY  ·  RELEASE OR CLICK" : "FORMING " + Mathf.RoundToInt(Charge01 * 100f) + "%  ·  AIM WITH THE CURSOR";
                if (thrown && thrown.Flying) return "HOLD USE TO STEER";
                return "HOLD USE TO FORM  ·  RELEASE OR CLICK TO THROW";
            }
        }
        KiSystem ki; UseEventTrigger use; KienzanDisc forming, thrown;
        readonly DbpuHandHold hold = new DbpuHandHold();
        readonly DbpuAimGuide aim = new DbpuAimGuide();
        static readonly Color AimColor = new Color(1f, .86f, .32f);
        bool charging, guiding; float charge, labelClock; int lastFrame = -1; GameObject buzz;

        public static KienzanHand GetOrCreate(LimbBehaviour limb)
        {
            if (!KiHandLoadout.IsHand(limb)) return null;
            var hand = limb.GetComponent<KienzanHand>(); if (!hand) hand = limb.gameObject.AddComponent<KienzanHand>();
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
        public void Activate()
        {
            if (!Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidHand(Limb, Person)) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (charging) { Throw(); return; }
            if (thrown && thrown.Flying) { guiding = true; hold.Begin(); return; }
            BeginCharge();
        }
        void BeginCharge()
        {
            if (!ki) ki = Person.GetComponent<KiSystem>();
            if (!DbpuHandTechniques.Spend(ki, .05f)) { DbpuWorldLabel.Show(Person, "KIENZAN", "NOT ENOUGH KI", 1.2f); return; }
            charging = true; charge = 0f; hold.Begin(); aim.Reset();
            forming = KienzanDisc.Create(Person);
            forming.Form(FormPoint(), 0f, false);
            buzz = DbpuFx.Sound(FormPoint(), "Assets/SFX/Techniques/Kienzan/Charge.wav", .75f, Limb.transform, true);
            DbpuWorldLabel.Show(Person, "KIENZAN", Status, .6f);
        }
        Vector2 FormPoint()
        {
            return KiCombatMath.HandPoint(Limb) + Vector2.up * (.30f + Charge01 * .16f);
        }
        void Update()
        {
            if (!Equipped || !KiCombatMath.ValidHand(Limb, Person)) { Cancel(); return; }
            if (!KiCombatMath.CanAct()) { if (DbpuFx.Paused && KiCombatMath.HasFocus) return; Cancel(); return; }
            float dt = KiCombatMath.Step(Time.deltaTime);
            if (charging)
            {
                Vector2 clicked;
                if (DbpuClickFire.TryConsume(out clicked)) { Throw(clicked); return; }
                if (hold.Released(Limb)) { Throw(); return; }
                charge += dt;
                if (charge < DoubleCharge) DbpuHandTechniques.Drain(ki, .035f, dt);
                if (ki && ki.currentKi <= 0f) { Throw(); return; }
                Vector2 shoulder = DbpuHandTechniques.Shoulder(Limb);
                DbpuHandTechniques.PullHand(Limb, shoulder + Vector2.up * .62f * Mathf.Max(.6f, Mathf.Abs(Person.transform.localScale.y)));
                if (forming) forming.Form(FormPoint(), Charge01, charge >= DoubleCharge);
                aim.Show(FormPoint(), aim.Direction(FormPoint(), Person), AimColor, Mathf.Lerp(.5f, 1f, Charge01));
                labelClock -= dt;
                if (labelClock <= 0f) { labelClock = .15f; DbpuWorldLabel.Show(Person, "KIENZAN", Status, .5f, Mathf.Clamp01(charge / DoubleCharge), null, new Color(1f, .82f, .25f)); }
            }
            else if (guiding)
            {
                bool held = hold.Continuous(Limb);
                if (thrown && thrown.Flying && held)
                {
                    thrown.SteerTowards(DbpuFx.MouseWorld(), dt);
                    Vector2 at = thrown.transform.position;
                    aim.Show(at, aim.Direction(at, Person), AimColor, .6f);
                }
                else if (!held || !thrown || !thrown.Flying) { guiding = false; aim.Hide(); }
            }
            else aim.Hide();
        }
        void Throw() { Throw(null); }
        void Throw(Vector2? target)
        {
            if (!charging) return;
            charging = false; StopBuzz();
            if (!forming) return;
            if (charge < .3f) { this.aim.Hide(); forming.Fizzle(); forming = null; DbpuWorldLabel.Show(Person, "KIENZAN", "TOO SMALL", 1f); return; }
            Vector2 from = forming.transform.position;
            // Cursor (or clicked point) relative to the disc; never the raised forearm's direction.
            Vector2 aim = target.HasValue ? this.aim.DirectionTo(from, target.Value, Person) : this.aim.Direction(from, Person);
            this.aim.Hide();
            float speed = 22f + 12f * Charge01;
            bool twin = charge >= DoubleCharge;
            forming.Throw(twin ? DbpuFx.Rotate(aim, -.09f) : aim, speed);
            thrown = forming; forming = null;
            if (twin)
            {
                var second = KienzanDisc.Create(Person);
                second.Form(from + new Vector2(-aim.y, aim.x) * .3f, 1f, true);
                second.Growth = thrown.Growth;
                second.Throw(DbpuFx.Rotate(aim, .09f), speed);
            }
            DbpuFx.Sound(from, "Assets/SFX/Techniques/Kienzan/Throw.wav", .9f);
            DbpuWorldLabel.Show(Person, twin ? "DOUBLE KIENZAN!" : "KIENZAN!", "TAP USE, THEN HOLD TO STEER", 1.6f);
            hold.Begin();
        }
        void StopBuzz() { if (buzz) Destroy(buzz); buzz = null; }
        public void Cancel()
        {
            charging = guiding = false; StopBuzz(); aim.Hide();
            if (forming) { forming.Fizzle(); forming = null; }
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); aim.Dispose(); if (use) Destroy(use); }
    }

    [DefaultExecutionOrder(300)]
    public sealed class KienzanDisc : MonoBehaviour
    {
        public bool Flying { get; private set; }
        PersonBehaviour owner;
        SpriteRenderer halo, disc, coreLine, glint;
        SpriteRenderer[] ghosts;
        readonly Vector2[] ghostPoints = new Vector2[6];
        DbpuParticles sparks;
        readonly HashSet<int> struck = new HashSet<int>();
        readonly RaycastHit2D[] hits = new RaycastHit2D[48];
        Vector2 velocity; float growth, life, clock, frameClock, ghostClock, fade = 1f, travelled, embedTimer = -1f, emitDebt;
        int frame; bool dying, twin; double power; GameObject buzz;
        static readonly Color Gold = new Color(1f, .84f, .3f, 1f);
        const float FullRadius = .6f;
        float Radius { get { return FullRadius * Mathf.Lerp(.18f, 1f, growth); } }
        public float Growth { get { return growth; } set { growth = Mathf.Clamp01(value); } }

        public static KienzanDisc Create(PersonBehaviour owner)
        {
            var go = new GameObject("DBPU Kienzan disc");
            var d = go.AddComponent<KienzanDisc>(); d.owner = owner; d.Build();
            return d;
        }
        void Build()
        {
            halo = DbpuFx.MakeSprite(transform, "Kienzan glow", DbpuFx.Glow, 43);
            ghosts = new SpriteRenderer[ghostPoints.Length];
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = DbpuFx.MakeSprite(transform, "Kienzan afterimage", DbpuFx.DiscFrame(i), 42);
            disc = DbpuFx.MakeSprite(transform, "Kienzan blade", DbpuFx.DiscFrame(0), 44);
            coreLine = DbpuFx.MakeSprite(transform, "Kienzan edge light", DbpuFx.Streak, 45, true);
            glint = DbpuFx.MakeSprite(transform, "Kienzan glint", DbpuFx.Star, 46, true);
            sparks = DbpuParticles.Create("DBPU Kienzan sparks", DbpuFx.Streak, 160, 45);
            sparks.StretchFactor = .04f;
        }
        public void Form(Vector2 at, float amount, bool isTwin)
        {
            if (Flying || dying) return;
            transform.position = new Vector3(at.x, at.y, -.35f);
            growth = Mathf.MoveTowards(growth, Mathf.Max(.15f, amount), Time.deltaTime * 2.5f);
            twin = isTwin;
        }
        public void Throw(Vector2 direction, float speed)
        {
            if (dying) return;
            Flying = true; velocity = direction.normalized * speed; life = 3.4f;
            growth = Mathf.Max(growth, .35f);
            power = DbpuFx.PowerOf(owner) * (.7f + .65f * growth);
            for (int i = 0; i < ghostPoints.Length; i++) ghostPoints[i] = transform.position;
            buzz = DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/Kienzan/Fly.wav", .55f, transform, true);
        }
        public void SteerTowards(Vector2 target, float dt)
        {
            if (!Flying || dying) return;
            Vector2 want = target - (Vector2)transform.position;
            if (want.sqrMagnitude < .05f) return;
            float current = Mathf.Atan2(velocity.y, velocity.x), wanted = Mathf.Atan2(want.y, want.x);
            float next = Mathf.MoveTowardsAngle(current * Mathf.Rad2Deg, wanted * Mathf.Rad2Deg, 260f * dt) * Mathf.Deg2Rad;
            velocity = new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * velocity.magnitude;
        }
        public void Fizzle()
        {
            if (dying) return;
            dying = true; Flying = false;
            if (buzz) Destroy(buzz); buzz = null;
        }
        void FixedUpdate()
        {
            if (!Flying || dying || DbpuFx.Paused) return;
            float dt = Time.fixedDeltaTime;
            Vector2 position = transform.position, step = velocity * dt, dir = velocity.normalized;
            float distance = step.magnitude;
            // Ki barriers first: a stronger shell deflects the disc, a weaker one is cut open.
            KiShield barrier; float entry; Vector2 contact;
            if (KiShield.TryTrace(owner ? owner.gameObject : null, position, dir, distance + Radius * .5f, Radius * .25f, out barrier, out entry, out contact))
            {
                if (barrier.RealDurability > power)
                {
                    Vector2 normal = (contact - barrier.Centre).normalized;
                    velocity = Vector2.Reflect(velocity, normal) * .8f;
                    barrier.ReceiveDamage(power * .55, contact, GetInstanceID());
                    transform.position = contact + normal * (Radius * .5f + .06f);
                    DbpuSlashFx.Spawn(contact, velocity, barrier.Tint, 1.1f);
                    DbpuFx.Sound(contact, "Assets/SFX/Techniques/Kienzan/Clang.wav", .9f);
                    return;
                }
                if (!struck.Contains(barrier.GetInstanceID()))
                {
                    struck.Add(barrier.GetInstanceID());
                    barrier.ReceiveDamage(power * 6 + barrier.Capacity * 2, contact, GetInstanceID());
                    DbpuSlashFx.Spawn(contact, velocity, Gold, 1.3f);
                }
            }
            int count = Physics2D.CircleCastNonAlloc(position, Radius * .55f, dir, hits, distance);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; var col = hit.collider;
                if (!col || col.isTrigger || col.GetComponent<KiShieldSurface>()) continue;
                var limb = col.GetComponent<LimbBehaviour>();
                if (limb)
                {
                    if (!limb.Person || limb.Person == owner) continue;
                    int id = limb.GetInstanceID(); if (struck.Contains(id)) continue; struck.Add(id);
                    if (DbpuDismemberment.Cut(limb, hit.point, dir, true))
                    {
                        DbpuSlashFx.Spawn(hit.point, velocity, Gold, 1f);
                        DbpuFx.Sound(hit.point, "Assets/SFX/Techniques/Kienzan/Slice.wav", .85f);
                    }
                    continue;
                }
                var body = col.attachedRigidbody;
                bool solid = !body || body.bodyType == RigidbodyType2D.Static || col.gameObject.isStatic;
                if (solid)
                {
                    if (hit.fraction <= 0f || hit.distance <= .001f) continue;
                    transform.position = hit.point - dir * Radius * .35f;
                    Flying = false; embedTimer = .55f;
                    if (buzz) Destroy(buzz); buzz = null;
                    DbpuSlashFx.Spawn(hit.point, velocity, Gold, 1.2f);
                    DbpuFx.Sound(hit.point, "Assets/SFX/Techniques/Kienzan/Clang.wav", .7f);
                    return;
                }
                int bodyId = body.GetInstanceID();
                if (!struck.Contains(bodyId) && body.bodyType == RigidbodyType2D.Dynamic)
                {
                    struck.Add(bodyId);
                    body.AddForceAtPosition(dir * Mathf.Min(body.mass, 40f) * 4f, hit.point, ForceMode2D.Impulse);
                    DbpuSlashFx.Spawn(hit.point, velocity, Gold, .8f);
                }
            }
            transform.position = position + step; travelled += distance; life -= dt;
            if (life <= 0f || travelled > 95f) Fizzle();
        }
        void LateUpdate()
        {
            float dt = DbpuFx.Dt; clock += dt;
            if (embedTimer > 0f) { embedTimer -= dt; if (embedTimer <= 0f) Fizzle(); }
            if (dying) { fade -= dt * 4f; if (fade <= 0f) { Destroy(gameObject); return; } }
            frameClock += dt; if (frameClock > .028f) { frameClock = 0f; frame++; disc.sprite = DbpuFx.SpriteOf(DbpuFx.DiscFrame(frame)); }
            Vector2 p = transform.position; float r = Radius;
            float tilt = Flying ? Mathf.Clamp(Mathf.Atan2(velocity.y, Mathf.Abs(velocity.x)) * Mathf.Rad2Deg * .25f, -18f, 18f) * Mathf.Sign(velocity.x == 0 ? 1 : velocity.x) : 0f;
            float flicker = .9f + .1f * Mathf.Sin(clock * 90f);
            DbpuFx.Place(halo, p, -.34f, r * 4.2f, r * 1.6f, tilt, DbpuFx.A(Gold, .42f * fade * flicker));
            DbpuFx.Place(disc, p, -.36f, r * 2f, r * 2f, tilt, DbpuFx.A(Color.white, fade));
            DbpuFx.Place(coreLine, p, -.37f, r * 1.9f, r * .14f, tilt, DbpuFx.A(Color.white, .9f * fade * flicker));
            float spin = clock * 28f;
            Vector2 g = p + DbpuFx.Rotate(new Vector2(Mathf.Cos(spin) * r * .92f, Mathf.Sin(spin) * r * .2f), tilt * Mathf.Deg2Rad);
            DbpuFx.Place(glint, g, -.38f, r * .7f, r * .7f, clock * 300f, DbpuFx.A(Color.white, (Mathf.Sin(spin) > 0f ? .95f : .35f) * fade));
            ghostClock -= dt;
            if (Flying && ghostClock <= 0f)
            {
                ghostClock = .022f;
                for (int i = ghostPoints.Length - 1; i > 0; i--) ghostPoints[i] = ghostPoints[i - 1];
                ghostPoints[0] = p;
            }
            for (int i = 0; i < ghosts.Length; i++)
            {
                float a = Flying ? (1f - (i + 1f) / (ghosts.Length + 1f)) * .38f * fade : 0f;
                DbpuFx.Place(ghosts[i], ghostPoints[i], -.33f, r * 2f, r * 2f, tilt, DbpuFx.A(Gold, a));
            }
            if (dt > 0f && fade > .2f)
            {
                emitDebt += dt * (Flying ? 70f : 28f + growth * 30f);
                while (emitDebt >= 1f)
                {
                    emitDebt -= 1f;
                    float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                    Vector2 rim = p + DbpuFx.Rotate(new Vector2(side * r * .98f, UnityEngine.Random.Range(-.1f, .1f) * r), tilt * Mathf.Deg2Rad);
                    Vector2 v = new Vector2(0, side * UnityEngine.Random.Range(2.5f, 6f)) + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) - velocity * .15f;
                    sparks.Emit(rim, v, UnityEngine.Random.Range(.08f, .2f), r * .16f, Color.white, DbpuFx.A(Gold, 0f), 4f, 2f, 0, true);
                }
            }
        }
        void OnDestroy() { if (buzz) Destroy(buzz); if (sparks) sparks.Release(); }
    }
}
