using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Hakai Ball: hold Use on the hand to gather a dark violet orb at the palm, release to
    // fire it at the cursor. A fighter it touches is sealed inside a destruction sphere:
    // weaker victims are marked limb by limb with red crosses and erased, stronger ones
    // can charge ki to crack it open. Hold Use again while someone is sealed to pour
    // lightning into the sphere. A raised Ki Shield stops the orb until one side gives.
    public sealed class HakaiBallHand : MonoBehaviour
    {
        public bool Equipped;
        public LimbBehaviour Limb { get; private set; }
        public PersonBehaviour Person { get; private set; }
        public const float FullCharge = 1.4f;
        KiSystem ki; UseEventTrigger use; HakaiBallOrb orb;
        readonly DbpuHandHold hold = new DbpuHandHold();
        readonly DbpuAimGuide aim = new DbpuAimGuide();
        static readonly Color AimColor = new Color(.82f, .5f, 1f);
        bool charging, pushing; float charge, labelClock; int lastFrame = -1; GameObject loop;
        public bool IsCharging { get { return charging; } }
        public float Charge01 { get { return Mathf.Clamp01(charge / FullCharge); } }

        public static HakaiBallHand GetOrCreate(LimbBehaviour limb)
        {
            if (!KiHandLoadout.IsHand(limb)) return null;
            var hand = limb.GetComponent<HakaiBallHand>(); if (!hand) hand = limb.gameObject.AddComponent<HakaiBallHand>();
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
        public void Announce() { DbpuWorldLabel.ShowLater(Person, 1.35f, "HAKAI BALL", "HOLD USE TO GATHER  ·  AIM WITH THE CURSOR  ·  RELEASE OR CLICK TO FIRE", 2.6f); }
        public void Activate()
        {
            if (!Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidHand(Limb, Person)) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (charging) { Launch(); return; }
            if (orb && orb.Sealing) { pushing = !pushing; hold.Begin(); return; }
            if (orb && orb.Busy) return;
            BeginCharge();
        }
        void BeginCharge()
        {
            if (!ki) ki = Person.GetComponent<KiSystem>();
            if (!DbpuHandTechniques.Spend(ki, .08f)) { DbpuWorldLabel.Show(Person, "HAKAI BALL", "NOT ENOUGH KI", 1.2f); return; }
            charging = true; charge = 0f; hold.Begin(); aim.Reset();
            orb = HakaiBallOrb.Create(Person, this);
            orb.Gather(Palm(), 0f);
            loop = DbpuFx.Sound(Palm(), "Assets/SFX/Techniques/HakaiBall/Charge.wav", .75f, Limb.transform, true);
        }
        // Cursor relative to the shoulder (stable: the hand itself is pulled along the aim, so
        // measuring from the hand made aim and arm chase each other). Dead zone keeps the last aim.
        Vector2 Aim() { return aim.Direction(DbpuHandTechniques.Shoulder(Limb), Person); }
        Vector2 Palm() { return KiCombatMath.HandPoint(Limb) + Aim() * (.22f + Charge01 * .16f); }
        void Update()
        {
            if (!Equipped || !KiCombatMath.ValidHand(Limb, Person)) { Cancel(); return; }
            if (!KiCombatMath.CanAct()) { if (DbpuFx.Paused && KiCombatMath.HasFocus) return; Cancel(); return; }
            float dt = KiCombatMath.Step(Time.deltaTime);
            Vector2 shoulder = DbpuHandTechniques.Shoulder(Limb);
            float reach = .62f * Mathf.Max(.6f, Mathf.Abs(Person.transform.localScale.y));
            if (charging)
            {
                Vector2 clicked;
                if (DbpuClickFire.TryConsume(out clicked)) { Launch(clicked); return; }
                if (hold.Released(Limb)) { Launch(); return; }
                charge += dt;
                if (charge < FullCharge) DbpuHandTechniques.Drain(ki, .04f, dt);
                DbpuHandTechniques.PullHand(Limb, shoulder + Aim() * reach, 90f);
                if (orb) { orb.Gather(Palm(), Charge01); aim.Show(orb.transform.position, aim.Direction((Vector2)orb.transform.position, Person), AimColor, Mathf.Lerp(.5f, 1f, Charge01)); }
                labelClock -= dt;
                if (labelClock <= 0f) { labelClock = .12f; DbpuWorldLabel.Show(Person, "HAKAI BALL", Charge01 >= 1f ? "READY  ·  RELEASE TO FIRE" : "GATHERING " + Mathf.RoundToInt(Charge01 * 100f) + "%", .45f, Charge01, null, new Color(.8f, .45f, 1f)); }
            }
            else aim.Hide();
            if (pushing && !charging)
            {
                bool held = hold.Continuous(Limb);
                if (!orb || !orb.Sealing || (!held && hold.Released(Limb)) || (ki && ki.currentKi <= 0f)) { pushing = false; return; }
                DbpuHandTechniques.Drain(ki, .06f, dt);
                Vector2 toward = ((Vector2)orb.transform.position - shoulder).normalized;
                DbpuHandTechniques.PullHand(Limb, shoulder + toward * reach, 90f);
                orb.Push(KiCombatMath.HandPoint(Limb) + toward * .2f);
            }
        }
        void Launch() { Launch(null); }
        void Launch(Vector2? target)
        {
            if (!charging) return;
            charging = false; StopLoop(); aim.Hide();
            if (!orb) return;
            if (charge < .25f) { orb.Fizzle(); orb = null; DbpuWorldLabel.Show(Person, "HAKAI BALL", "TOO WEAK", 1f); return; }
            Vector2 from = orb.transform.position;
            // Fire from the orb itself towards the cursor / clicked point.
            Vector2 direction = target.HasValue ? aim.DirectionTo(from, target.Value, Person) : aim.Direction(from, Person);
            orb.Launch(direction, 12f + 6f * Charge01, Charge01);
            DbpuFx.Sound(orb.transform.position, "Assets/SFX/Techniques/HakaiBall/Fire.wav", .9f);
            DbpuWorldLabel.Show(Person, "HAKAI BALL", "TAP USE WHILE SEALED, THEN HOLD TO OVERWHELM", 1.8f);
        }
        void StopLoop() { if (loop) Destroy(loop); loop = null; }
        public void Cancel()
        {
            pushing = false; aim.Hide();
            if (charging) { charging = false; StopLoop(); if (orb) orb.Fizzle(); orb = null; }
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); aim.Dispose(); if (use) Destroy(use); }
    }

    [DefaultExecutionOrder(305)]
    public sealed class HakaiBallOrb : MonoBehaviour
    {
        enum State { Gathering, Flying, Clashing, Sealing, Erasing, Shattering, Fizzling, Held }
        sealed class Captive { public Rigidbody2D Body; public float Gravity, Drag, AngularDrag; public Vector2 Offset; }
        sealed class Mark { public LimbBehaviour Limb; public SpriteRenderer Sprite; public float Shown = -1f; }

        static readonly List<HakaiBallOrb> live = new List<HakaiBallOrb>();
        static readonly Color Deep = new Color(.32f, .06f, .5f, 1f), Violet = new Color(.62f, .2f, .92f, 1f), Pink = new Color(1f, .62f, 1f, 1f);
        PersonBehaviour owner, victim; HakaiBallHand hand; State state = State.Gathering;
        SpriteRenderer halo, bloom, body, core, flare, chargeRing; SpriteRenderer[] swirls, spikes;
        LineRenderer[] orbits, bolts, cracks; LineRenderer pushBolt;
        DbpuParticles motes, sparkles, trail, shards;
        readonly List<Captive> captives = new List<Captive>();
        readonly List<Mark> marks = new List<Mark>();
        readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        readonly HashSet<int> erased = new HashSet<int>();
        Vector2 velocity, pushFrom, clashPoint; KiShield clashShield;
        float radius = .1f, visualRadius = .1f, charge, clock, life, timer, destroy, breakout, ratio, push, boltClock, labelClock, emitDebt, fade = 1f, crackSeed;
        double power; GameObject hum;

        // The physical sphere, while the module and its shaders are available.
        HakaiOrb body2;
        public bool Flying { get { return state == State.Flying; } }
        public Vector2 CurrentVelocity { get { return velocity; } }
        public void SetHeld(bool on)
        {
            if (on) { if (state == State.Flying) state = State.Held; }
            else if (state == State.Held) { state = State.Flying; life = Mathf.Max(life, 1.5f); }
        }
        public void HoldAt(Vector2 centre) { transform.position = new Vector3(centre.x, centre.y, -.5f); }
        public void Redirect(Vector2 newVelocity, PersonBehaviour newOwner)
        {
            velocity = newVelocity; life = 4f; state = State.Flying;
            if (newOwner) owner = newOwner;
        }
        public bool Sealing { get { return state == State.Sealing; } }
        public bool Busy { get { return state == State.Flying || state == State.Held || state == State.Clashing || state == State.Sealing || state == State.Erasing || state == State.Shattering; } }

        public static HakaiBallOrb Create(PersonBehaviour owner, HakaiBallHand hand)
        {
            var go = new GameObject("DBPU Hakai Ball");
            var orb = go.AddComponent<HakaiBallOrb>(); orb.owner = owner; orb.hand = hand; orb.Build();
            live.Add(orb);
            return orb;
        }
        void Build()
        {
            halo = DbpuFx.MakeSprite(transform, "Destruction halo", DbpuFx.Glow, 50);
            bloom = DbpuFx.MakeSprite(transform, "Destruction bloom", DbpuFx.Glow, 51);
            body = DbpuFx.MakeSprite(transform, "Destruction sphere", DbpuFx.Sphere, 52);
            swirls = new SpriteRenderer[3];
            for (int i = 0; i < swirls.Length; i++) swirls[i] = DbpuFx.MakeSprite(transform, "Energy swirl", DbpuFx.Swirl, 53);
            core = DbpuFx.MakeSprite(transform, "Pink core", DbpuFx.Glow, 54, true);
            chargeRing = DbpuFx.MakeSprite(transform, "Gather ring", DbpuFx.Ring, 55);
            flare = DbpuFx.MakeSprite(transform, "Collapse flare", DbpuFx.Star, 60, true);
            spikes = new SpriteRenderer[8];
            for (int i = 0; i < spikes.Length; i++) spikes[i] = DbpuFx.MakeSprite(transform, "Flare ray", DbpuFx.Streak, 59, true);
            orbits = new LineRenderer[3];
            for (int i = 0; i < orbits.Length; i++) orbits[i] = DbpuFx.Line(transform, "Orbit ring " + i, 56);
            bolts = new LineRenderer[3];
            for (int i = 0; i < bolts.Length; i++) bolts[i] = DbpuFx.Line(transform, "Inner lightning " + i, 57);
            cracks = new LineRenderer[6];
            for (int i = 0; i < cracks.Length; i++) cracks[i] = DbpuFx.Line(transform, "Crack " + i, 58);
            pushBolt = DbpuFx.Line(transform, "Overwhelm lightning", 61);
            motes = DbpuParticles.Create("DBPU Hakai Ball motes", DbpuFx.Square, 120, 55);
            sparkles = DbpuParticles.Create("DBPU Hakai Ball sparks", DbpuFx.Star, 200, 58);
            trail = DbpuParticles.Create("DBPU Hakai Ball trail", DbpuFx.Glow, 160, 49);
            crackSeed = UnityEngine.Random.value * 100f;
            if (OrbFx.Active) body2 = new HakaiOrb(this, owner);
        }
        public void Gather(Vector2 at, float amount)
        {
            if (state != State.Gathering) return;
            transform.position = new Vector3(at.x, at.y, -.5f); charge = amount;
            radius = .16f + .2f * amount;
        }
        public void Launch(Vector2 direction, float speed, float amount)
        {
            if (state != State.Gathering) return;
            state = State.Flying; velocity = direction.normalized * speed; life = 4f; charge = amount;
            power = DbpuFx.PowerOf(owner) * (.75f + .5f * amount);
            hum = DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/HakaiBall/Hum.wav", .55f, transform, true);
        }
        public void Push(Vector2 from) { push = 1f; pushFrom = from; }
        public void Fizzle()
        {
            if (state == State.Fizzling) return;
            ReleaseCaptives(false); state = State.Fizzling; timer = 0f;
            if (hum) Destroy(hum); hum = null;
        }
        void FixedUpdate()
        {
            if (state != State.Flying || DbpuFx.Paused) return;
            float dt = Time.fixedDeltaTime;
            Vector2 position = transform.position, dir = velocity.normalized; float distance = velocity.magnitude * dt;
            // Opposing Hakai Balls annihilate each other.
            foreach (var other in live)
            {
                if (!other || other == this || other.state != State.Flying) continue;
                if (Vector2.Distance(other.transform.position, position) < radius + other.radius + .1f)
                {
                    Vector2 mid = ((Vector2)other.transform.position + position) * .5f;
                    Flash(mid, 2.2f); DbpuFx.Shake(mid, 2.2f);
                    DbpuFx.Sound(mid, "Assets/SFX/Techniques/HakaiBall/Erase.wav", 1f);
                    other.Fizzle(); Fizzle(); return;
                }
            }
            KiShield barrier; float entry; Vector2 contact;
            if (KiShield.TryTrace(owner ? owner.gameObject : null, position, dir, distance + radius, radius * .5f, out barrier, out entry, out contact))
            {
                state = State.Clashing; clashShield = barrier; clashPoint = contact; timer = 0f;
                transform.position = contact + (contact - barrier.Centre).normalized * radius * .6f;
                DbpuFx.Sound(contact, "Assets/SFX/Techniques/HakaiBall/Clash.wav", .9f);
                return;
            }
            int count = Physics2D.CircleCastNonAlloc(position, radius, dir, hits, distance);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (!col || col.isTrigger || col.GetComponent<KiShieldSurface>()) continue;
                var limb = col.GetComponent<LimbBehaviour>();
                if (limb)
                {
                    if (!limb.Person || limb.Person == owner || limb.Person.GetComponent<HakaiDissolveMarker>()) continue;
                    if (body2 != null && (body2.ImmuneTo.Contains(limb.Person) || SpherePhysics.ReactToContact(body2, limb))) { if (state != State.Flying) return; continue; }
                    Seal(limb.Person); return;
                }
                var physical = DbpuTechniqueTargeting.Physical(col, owner);
                var rb = col.attachedRigidbody;
                if (physical && rb && rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    int id = physical.transform.root.GetInstanceID();
                    if (erased.Add(id)) HakaiDissolve.DissolveObject(physical, hits[i].point);
                    continue;
                }
                if ((!rb || rb.bodyType == RigidbodyType2D.Static) && hits[i].fraction > 0f) { Flash(hits[i].point, .9f); Fizzle(); return; }
            }
            transform.position = position + velocity * dt; life -= dt;
            if (life <= 0f) Fizzle();
        }
        void Seal(PersonBehaviour target)
        {
            victim = target; state = State.Sealing; timer = 0f; destroy = 0f; breakout = 0f;
            var bounds = DbpuFx.BodyBounds(target);
            transform.position = new Vector3(bounds.center.x, bounds.center.y, -.5f);
            radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.22f, 1.25f, 7f);
            bool hasKi = target.GetComponent<KiSystem>();
            ratio = hasKi ? (float)Math.Min(1000d, power / Math.Max(1d, DbpuFx.PowerOf(target))) : 1000f;
            foreach (var limb in target.Limbs)
            {
                if (!limb) continue;
                var rb = limb.GetComponent<Rigidbody2D>();
                if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    captives.Add(new Captive { Body = rb, Gravity = rb.gravityScale, Drag = rb.drag, AngularDrag = rb.angularDrag, Offset = rb.position - (Vector2)bounds.center });
                    rb.gravityScale = 0f; rb.drag = 7f; rb.angularDrag = 6f; rb.velocity *= .15f;
                }
                var x = DbpuFx.MakeSprite(null, "DBPU Hakai mark", DbpuFx.Cross, 80);
                x.enabled = false; marks.Add(new Mark { Limb = limb, Sprite = x });
            }
            marks.Sort((a, b) => b.Limb.transform.position.y.CompareTo(a.Limb.transform.position.y));
            if (hum) Destroy(hum);
            DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/HakaiBall/Seal.wav", .9f);
            hum = DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/HakaiBall/Hum.wav", .5f, transform, true);
            Flash(transform.position, radius * .7f);
            DbpuFx.Shake(transform.position, 1.2f);
        }
        void ReleaseCaptives(bool fling)
        {
            Vector2 c = transform.position;
            foreach (var cap in captives)
            {
                if (!cap.Body) continue;
                cap.Body.gravityScale = cap.Gravity; cap.Body.drag = cap.Drag; cap.Body.angularDrag = cap.AngularDrag;
                if (fling) cap.Body.velocity = (cap.Body.position - c).normalized * 4f + Vector2.up * 2f;
            }
            captives.Clear();
            foreach (var m in marks) if (m.Sprite) Destroy(m.Sprite.gameObject);
            marks.Clear();
        }
        void Update()
        {
            float dt = DbpuFx.Dt;
            if (dt <= 0f) { Draw(0f); return; }
            clock += dt; timer += dt; push = Mathf.MoveTowards(push, 0f, dt * 2.2f);
            switch (state)
            {
                case State.Gathering:
                    if (!hand || !hand.IsCharging) { Fizzle(); break; }
                    break;
                case State.Clashing: UpdateClash(dt); break;
                case State.Sealing: UpdateSeal(dt); break;
                case State.Erasing:
                    if (timer > .5f) { state = State.Fizzling; timer = 0f; }
                    break;
                case State.Shattering:
                    if (timer > .35f) { state = State.Fizzling; timer = .2f; }
                    break;
                case State.Fizzling:
                    fade = Mathf.Clamp01(1f - timer / .4f);
                    if (timer > .45f) { Destroy(gameObject); return; }
                    break;
            }
            Draw(dt);
        }
        void UpdateClash(float dt)
        {
            if (!clashShield || !clashShield.Using)
            {
                state = State.Flying; life = Mathf.Max(life, 1.5f); return;
            }
            float held;
            bool stopped = clashShield.ReceivePressure(GetInstanceID(), power * .85, clashPoint, dt, out held);
            Vector2 normal = (clashPoint - clashShield.Centre).normalized;
            clashPoint = clashShield.Centre + normal * clashShield.Radius;
            transform.position = clashPoint + normal * radius * .6f;
            if (!stopped)
            {
                state = State.Flying; life = Mathf.Max(life, 1.5f);
                Flash(clashPoint, 1.2f); return;
            }
            if (timer > 3.6f) { Flash(clashPoint, 1f); Fizzle(); if (clashShield.Person) DbpuWorldLabel.Show(clashShield.Person, "KI SHIELD", "HELD!", 1.4f); }
        }
        void UpdateSeal(float dt)
        {
            if (!victim || victim.GetComponent<HakaiDissolveMarker>()) { Fizzle(); return; }
            Vector2 centre = transform.position;
            foreach (var cap in captives)
            {
                if (!cap.Body) continue;
                Vector2 want = centre + cap.Offset * .92f, error = want - cap.Body.position;
                cap.Body.AddForce((error * 18f - cap.Body.velocity * 2f) * cap.Body.mass);
            }
            bool hasKi = victim.GetComponent<KiSystem>();
            float rate = .30f * Mathf.Sqrt(Mathf.Max(.01f, ratio)) * (1f + 1.7f * push);
            float escape = hasKi ? .24f / Mathf.Sqrt(Mathf.Max(.01f, ratio)) * (DbpuFx.IsKiCharging(victim) ? 2.6f : 1f) : 0f;
            if (ratio >= 50f) escape = 0f;
            destroy += Mathf.Min(rate, 3f) * dt;
            breakout += Mathf.Min(escape, 3f) * dt;
            labelClock -= dt;
            if (labelClock <= 0f)
            {
                labelClock = .1f;
                if (hasKi) DbpuWorldLabel.Show(victim, "HAKAI BALL", "CHARGE KI TO BREAK OUT!", .4f, breakout, null, new Color(.86f, .5f, 1f));
                if (owner) DbpuWorldLabel.Show(owner, "HAKAI BALL", breakout > .45f && breakout > destroy ? "IT'S CRACKING" : push > .05f ? "OVERWHELMING" : "ERASING " + Mathf.RoundToInt(Mathf.Clamp01(destroy) * 100f) + "%", .4f);
            }
            float reveal = Mathf.Clamp01((destroy - .55f) / .45f);
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (m.Shown < 0f && reveal * marks.Count > i) { m.Shown = clock; DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/HakaiBall/Mark.wav", .35f); }
            }
            if (destroy >= 1f) { Erase(); return; }
            if (breakout >= 1f) Shatter();
        }
        void Erase()
        {
            state = State.Erasing; timer = 0f;
            Flash(transform.position, radius * 1.4f);
            DbpuFx.Shake(transform.position, 2f);
            DbpuFx.Sound(transform.position, "Assets/SFX/Techniques/HakaiBall/Erase.wav", 1f);
            var burst = DbpuParticles.Create("DBPU Hakai Ball burst", DbpuFx.Square, 80, 61); burst.Release();
            for (int i = 0; i < 46; i++)
            {
                Vector2 v = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * UnityEngine.Random.Range(2f, 8f) * (radius / 2f);
                float s = UnityEngine.Random.Range(.07f, .2f);
                burst.Emit(transform.position, v, UnityEngine.Random.Range(.8f, 1.8f), s, Color.white, DbpuFx.A(Violet, 0f), 2.4f, -.3f, UnityEngine.Random.Range(-120f, 120f));
            }
            foreach (var m in marks) if (m.Sprite) Destroy(m.Sprite.gameObject);
            marks.Clear(); captives.Clear();
            if (victim) { DbpuWorldLabel.Hide(victim); HakaiDissolve.DissolvePerson(victim); }
            if (owner) DbpuWorldLabel.Show(owner, "HAKAI BALL", "ERASED", 1.4f);
            if (hum) Destroy(hum); hum = null;
        }
        void Shatter()
        {
            state = State.Shattering; timer = 0f;
            Vector2 c = transform.position;
            shards = DbpuParticles.Create("DBPU Hakai Ball shards", DbpuFx.Shard, 90, 62); shards.Release();
            for (int i = 0; i < 64; i++)
            {
                Vector2 dir = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f);
                Vector2 at = c + dir * radius * UnityEngine.Random.Range(.6f, 1f);
                float s = UnityEngine.Random.Range(.15f, .5f) * Mathf.Clamp(radius / 1.6f, .7f, 2f);
                Color tint = UnityEngine.Random.value < .5f ? Color.white : Color.Lerp(Pink, Color.white, .5f);
                shards.Emit(at, dir * UnityEngine.Random.Range(4f, 11f), UnityEngine.Random.Range(.8f, 1.4f), s, DbpuFx.A(tint, .95f), DbpuFx.A(Violet, 0f), 1.2f, 5f, UnityEngine.Random.Range(-500f, 500f));
            }
            Flash(c, radius);
            DbpuFx.Shake(c, 1.6f);
            DbpuFx.Sound(c, "Assets/SFX/Techniques/HakaiBall/Shatter.wav", 1f);
            ReleaseCaptives(true);
            if (victim) DbpuWorldLabel.Show(victim, "HAKAI BALL", "BROKE FREE!", 1.6f);
            if (owner) DbpuWorldLabel.Show(owner, "HAKAI BALL", "IT BROKE!", 1.4f);
            if (hum) Destroy(hum); hum = null;
        }
        void Flash(Vector2 at, float size)
        {
            var f = DbpuParticles.Create("DBPU Hakai flash", DbpuFx.Star, 6, 63); f.Release();
            f.Emit(at, Vector2.zero, .45f, size * 1.8f, Color.white, DbpuFx.A(Pink, 0f), 0, 0, 40f, false, size * 3.6f);
            var g = DbpuParticles.Create("DBPU Hakai flash glow", DbpuFx.Glow, 4, 62); g.Release();
            g.Emit(at, Vector2.zero, .5f, size * 2.4f, DbpuFx.A(Pink, .9f), DbpuFx.A(Violet, 0f), 0, 0, 0, false, size * 4f);
            var r = DbpuParticles.Create("DBPU Hakai flash ring", DbpuFx.Ring, 4, 62); r.Release();
            r.Emit(at, Vector2.zero, .5f, size * 1.2f, DbpuFx.A(Color.Lerp(Pink, Color.white, .5f), .9f), DbpuFx.A(Violet, 0f), 0, 0, 0, false, size * 3.8f);
        }

        void Draw(float dt)
        {
            Vector2 p = transform.position;
            float target = state == State.Erasing ? Mathf.Lerp(radius, 0f, Mathf.Clamp01(timer / .35f)) : state == State.Shattering ? radius * (1f + timer * .4f) : radius;
            visualRadius = state == State.Sealing && timer < .3f ? Mathf.Lerp(.35f, radius, DbpuFx.Smooth(0, .3f, timer)) : (dt > 0f ? Mathf.Lerp(visualRadius, target, 1f - Mathf.Exp(-18f * dt)) : visualRadius);
            float r = Mathf.Max(.01f, visualRadius), f = fade * (state == State.Shattering ? Mathf.Clamp01(1f - timer / .3f) : 1f);
            bool big = state == State.Sealing || state == State.Erasing || state == State.Shattering;
            if (body2 != null) body2.Drive(r, f, power > 0d ? power : DbpuFx.PowerOf(owner));
            float throb = 1f + .03f * Mathf.Sin(clock * 9f) + (push > 0f ? .03f * Mathf.Sin(clock * 47f) : 0f);
            DbpuFx.Place(halo, p, -.48f, r * (big ? 4.4f : 3.4f) * throb, r * (big ? 4.4f : 3.4f) * throb, 0, DbpuFx.A(Violet, (big ? .5f : .55f) * f));
            DbpuFx.Place(bloom, p, -.49f, r * 2.5f, r * 2.5f, 0, DbpuFx.A(Color.Lerp(Violet, Pink, .4f), .35f * f));
            DbpuFx.Place(body, p, -.5f, r * 2f * throb, r * 2f * throb, 0, DbpuFx.A(Color.white, f));
            for (int i = 0; i < swirls.Length; i++)
                DbpuFx.Place(swirls[i], p, -.51f, r * 1.75f, r * 1.75f, clock * (40f + i * 23f) * (i % 2 == 0 ? 1f : -1f) + i * 120f, DbpuFx.A(Pink, .32f * f));
            DbpuFx.Place(core, p, -.52f, r * (big ? .9f : 1.3f), r * (big ? .9f : 1.3f), 0, DbpuFx.A(Pink, (big ? .35f : .55f) * f));
            bool gathering = state == State.Gathering;
            DbpuFx.Place(chargeRing, p, -.53f, r * (2.6f + .5f * Mathf.Repeat(clock * 1.6f, 1f)), r * (2.6f + .5f * Mathf.Repeat(clock * 1.6f, 1f)), 0, DbpuFx.A(Pink, gathering ? .55f * (1f - Mathf.Repeat(clock * 1.6f, 1f)) : 0f));
            float flareA = state == State.Erasing ? Mathf.Clamp01(1f - timer / .45f) : 0f;
            DbpuFx.Place(flare, p, -.6f, radius * 3.4f, radius * 3.4f, clock * 30f, DbpuFx.A(Color.white, flareA));
            for (int i = 0; i < spikes.Length; i++)
            {
                float a = i * 45f + 22f + clock * 10f, len = radius * (i % 2 == 0 ? 4.4f : 2.6f) * Mathf.Clamp01(1f - timer / .5f);
                DbpuFx.Place(spikes[i], p, -.59f, Mathf.Max(.001f, len), radius * .1f, a, DbpuFx.A(Color.white, flareA * .9f));
            }
            DrawOrbits(p, r, f * (big ? 1f : .55f));
            DrawBolts(p, r, f, dt);
            DrawCracks(p, r, state == State.Sealing ? Mathf.Clamp01((breakout - .25f) / .75f) : state == State.Shattering ? 1f : 0f);
            DrawMarks();
            DrawPush(p, r, dt);
            if (dt > 0f) Emit(p, r, dt, big);
        }
        void DrawOrbits(Vector2 c, float r, float alpha)
        {
            for (int i = 0; i < orbits.Length; i++)
            {
                var line = orbits[i];
                if (alpha <= .01f) { line.positionCount = 0; continue; }
                const int n = 49; line.positionCount = n;
                float tilt = (i * 62f + clock * (7f + i * 4f)) * Mathf.Deg2Rad, squash = .26f + .1f * i, phase = clock * (1.1f + .4f * i);
                float a = r * (1.12f + .06f * i);
                for (int k = 0; k < n; k++)
                {
                    float t = k * Mathf.PI * 2f / (n - 1) + phase;
                    Vector2 e = DbpuFx.Rotate(new Vector2(Mathf.Cos(t) * a, Mathf.Sin(t) * a * squash), tilt);
                    line.SetPosition(k, new Vector3(c.x + e.x, c.y + e.y, -.54f));
                }
                line.startWidth = line.endWidth = Mathf.Max(.012f, r * .02f);
                line.startColor = line.endColor = new Color(.07f, .01f, .1f, .88f * alpha);
            }
        }
        void DrawBolts(Vector2 c, float r, float alpha, float dt)
        {
            boltClock -= dt; bool refresh = boltClock <= 0f; if (refresh) boltClock = .06f;
            for (int i = 0; i < bolts.Length; i++)
            {
                var line = bolts[i];
                bool clash = state == State.Clashing && clashShield;
                if (alpha <= .05f || state == State.Fizzling) { line.positionCount = 0; continue; }
                if (!refresh) continue;
                const int n = 9; line.positionCount = n;
                Vector2 a, b;
                if (clash)
                {
                    // Lightning crawling over the shield shell from the contact point.
                    float baseAngle = Mathf.Atan2(clashPoint.y - clashShield.Centre.y, clashPoint.x - clashShield.Centre.x);
                    float sweep = UnityEngine.Random.Range(-.9f, .9f);
                    for (int k = 0; k < n; k++)
                    {
                        float t = k / (float)(n - 1), ang = baseAngle + sweep * t;
                        float rr = clashShield.Radius * (1f + UnityEngine.Random.Range(-.04f, .04f) * t);
                        line.SetPosition(k, new Vector3(clashShield.Centre.x + Mathf.Cos(ang) * rr, clashShield.Centre.y + Mathf.Sin(ang) * rr, -.56f));
                    }
                    line.startWidth = .05f; line.endWidth = .015f;
                    line.startColor = DbpuFx.A(Color.white, .95f); line.endColor = DbpuFx.A(Pink, .5f);
                    continue;
                }
                a = c + DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * r * UnityEngine.Random.Range(.1f, .5f);
                b = c + DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * r * UnityEngine.Random.Range(.6f, .92f);
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    Vector2 q = Vector2.Lerp(a, b, t) + normal * (k == 0 || k == n - 1 ? 0f : UnityEngine.Random.Range(-.12f, .12f) * r);
                    line.SetPosition(k, new Vector3(q.x, q.y, -.55f));
                }
                line.startWidth = Mathf.Max(.01f, r * .025f); line.endWidth = Mathf.Max(.006f, r * .01f);
                line.startColor = DbpuFx.A(Color.white, .85f * alpha); line.endColor = DbpuFx.A(Pink, .35f * alpha);
            }
        }
        void DrawCracks(Vector2 c, float r, float amount)
        {
            for (int i = 0; i < cracks.Length; i++)
            {
                var line = cracks[i];
                if (amount <= .01f) { line.positionCount = 0; continue; }
                const int n = 6; line.positionCount = n;
                float ang = (i * 60f + crackSeed * 10f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), side = new Vector2(-dir.y, dir.x);
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    float depth = 1f - t * (.25f + .55f * amount);
                    float jag = (Mathf.PerlinNoise(crackSeed + i * 3.1f, k * .9f) - .5f) * .35f * t;
                    Vector2 q = c + dir * r * depth + side * r * jag;
                    line.SetPosition(k, new Vector3(q.x, q.y, -.57f));
                }
                line.startWidth = Mathf.Max(.012f, r * .022f); line.endWidth = Mathf.Max(.005f, r * .008f);
                line.startColor = line.endColor = new Color(1f, .95f, 1f, .9f * amount);
            }
        }
        void DrawMarks()
        {
            foreach (var m in marks)
            {
                if (!m.Sprite) continue;
                if (m.Shown < 0f || !m.Limb || !m.Limb.gameObject.activeInHierarchy) { m.Sprite.enabled = false; continue; }
                var sr = m.Limb.GetComponent<SpriteRenderer>();
                float size = sr ? Mathf.Clamp(Mathf.Min(sr.bounds.size.x, sr.bounds.size.y) * 1.25f + .08f, .16f, .5f) : .25f;
                float pop = Mathf.Clamp01((clock - m.Shown) / .18f);
                float scale = size * (pop < 1f ? Mathf.Lerp(2.2f, 1f, pop) : 1f);
                Vector2 at = sr ? (Vector2)sr.bounds.center : (Vector2)m.Limb.transform.position;
                DbpuFx.Place(m.Sprite, at, -.62f, scale, scale, 0, DbpuFx.A(Color.white, pop));
            }
        }
        void DrawPush(Vector2 c, float r, float dt)
        {
            if (push <= .02f || state != State.Sealing) { pushBolt.positionCount = 0; return; }
            if (dt > 0f && UnityEngine.Random.value > .4f || pushBolt.positionCount == 0)
            {
                const int n = 14; pushBolt.positionCount = n;
                Vector2 to = c + (pushFrom - c).normalized * r, d = to - pushFrom, normal = new Vector2(-d.y, d.x).normalized;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    Vector2 q = pushFrom + d * t + normal * (k == 0 || k == n - 1 ? 0f : UnityEngine.Random.Range(-.25f, .25f) * Mathf.Sin(t * Mathf.PI));
                    pushBolt.SetPosition(k, new Vector3(q.x, q.y, -.58f));
                }
            }
            pushBolt.startWidth = .07f; pushBolt.endWidth = .04f;
            pushBolt.startColor = DbpuFx.A(Color.white, push); pushBolt.endColor = DbpuFx.A(Pink, push * .8f);
            if (dt > 0f) sparkles.Emit(pushFrom, DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * 2f, .25f, .35f, Color.white, DbpuFx.A(Pink, 0f), 4f);
        }
        void Emit(Vector2 c, float r, float dt, bool big)
        {
            if (state == State.Fizzling) return;
            emitDebt += dt * (big ? 30f : 16f);
            while (emitDebt >= 1f)
            {
                emitDebt -= 1f;
                Vector2 inside = c + DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * r * Mathf.Sqrt(UnityEngine.Random.value) * .85f;
                motes.Emit(inside, new Vector2(UnityEngine.Random.Range(-.2f, .2f), UnityEngine.Random.Range(-.1f, .35f)) * r, UnityEngine.Random.Range(.6f, 1.4f), r * UnityEngine.Random.Range(.03f, .06f), DbpuFx.A(Color.white, .85f), DbpuFx.A(Pink, 0f), 1f, 0, UnityEngine.Random.Range(-60f, 60f));
                if (state == State.Gathering)
                {
                    Vector2 d = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f);
                    sparkles.Emit(c + d * r * 3f, -d * r * 3f / .25f, .25f, r * .3f, DbpuFx.A(Color.white, .9f), DbpuFx.A(Pink, .2f), 0, 0, 0);
                }
            }
            if (state == State.Flying) trail.Emit(c, -velocity * .05f, .32f, r * 2.2f, DbpuFx.A(Violet, .5f), DbpuFx.A(Deep, 0f), 2f, 0, 0, false, r * .6f);
            if (state == State.Clashing && UnityEngine.Random.value < .5f)
                sparkles.Emit(clashPoint, DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.283f) * UnityEngine.Random.Range(1f, 4f), .3f, .2f, Color.white, DbpuFx.A(Pink, 0f), 3f);
        }
        void OnDestroy()
        {
            live.Remove(this);
            ReleaseCaptives(false);
            if (body2 != null) body2.Dispose();
            if (hum) Destroy(hum);
            if (motes) motes.Release(); if (sparkles) sparkles.Release(); if (trail) trail.Release();
        }
    }
}
