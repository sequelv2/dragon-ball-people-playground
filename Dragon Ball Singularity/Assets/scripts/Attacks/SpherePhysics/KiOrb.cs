using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DBPU
{
    // The physical side of a flying sphere of ki, whatever technique made it: hands can stop it,
    // throw it back or knock it away, and other energy can lock with it. Each family of attack keeps
    // its own way of moving and hurting and plugs in through the abstract members below.
    public abstract class KiOrb : GuardTarget
    {
        public enum Weight { Tiny, Medium, Large }

        public readonly HashSet<PersonBehaviour> ImmuneTo = new HashSet<PersonBehaviour>();
        public OrbStyle Look { get; protected set; }
        public PersonBehaviour Owner { get; protected set; }
        public string Technique { get; protected set; }
        public Weight Class { get; protected set; }
        public float TypeFactor { get; protected set; }
        public bool Held { get; protected set; }
        public bool Dueling { get; set; }
        // Something that was sent back at this fighter blows up on touching them.
        public PersonBehaviour ThrownAt { get; protected set; }
        // Below this power ratio the sphere simply lands.
        public float MinimumRatio = BeamPhysics.CatchRatio;

        protected OrbView view;
        protected Vector2 heldThrust;
        protected float strain, pulse, collapse, collapseRadius, viewOpacity = 1f;
        protected Vector2 collapseAt;
        protected bool collapsing, finished;

        public abstract Vector2 Centre { get; }
        public abstract Vector2 Velocity { get; }
        public abstract float Radius { get; }
        public abstract double Power { get; }
        // Places it while hands or a clash have it.
        public abstract void Place(Vector2 centre);
        // Into free flight, possibly now belonging to someone else.
        protected abstract void Launch(Vector2 velocity, PersonBehaviour newOwner);
        protected virtual void OnHeld() { }
        protected virtual void OnFreed() { }
        // Ended by hands or by losing a clash, without going off.
        protected abstract void Remove();
        public abstract void Detonate(Vector2 normal);

        public bool Finished { get { return finished; } }
        // Nothing left to do or draw.
        public virtual bool Gone { get { return !Exists && !collapsing; } }
        public float LastSpeed { get { return lastSpeed; } }
        public PersonBehaviour PreviousOwner { get; protected set; }

        // ---------------------------------------------------------------------------- clashes
        public void EnterDuel(Vector2 pressing)
        {
            Dueling = true; heldThrust = -pressing;
            OnHeld();
        }

        public void Press(Vector2 centre, Vector2 pressing, float s)
        {
            strain = s; heldThrust = -pressing;
            Place(centre);
        }

        public void LeaveDuel(Vector2 velocity, PersonBehaviour newOwner, bool thrown)
        {
            Dueling = false; strain = 0f;
            OnFreed();
            PreviousOwner = Owner;
            if (thrown) ThrownAt = Owner;
            Launch(velocity, newOwner ?? Owner);
        }

        protected double OwnerPower(double reserve = 1d)
        {
            double power = Owner ? BeamPhysics.FighterPower(Owner) : 1d;
            return Math.Max(1e-6d, power * TypeFactor * reserve);
        }

        // ------------------------------------------------------------------- what hands see
        public override bool Interrupted { get { return Held || Dueling; } }
        public override Vector2 Travel { get { Vector2 v = Velocity; return v.sqrMagnitude > .01f ? v.normalized : Vector2.right; } }
        public override float Speed { get { return Velocity.magnitude; } }
        public override float ContactSize { get { return Mathf.Clamp(Radius * .3f, .5f, 3.2f); } }
        public override BeamStyle Style { get { return Look.Beam; } }
        public override PersonBehaviour Attacker { get { return Owner; } }
        public override KiSystem AttackerKi { get { return null; } }
        public override float BraceLimit { get { return Radius > 4f ? 2.4f : 1.1f; } }
        public override float RatioFor(PersonBehaviour holder)
        {
            double r = BeamPhysics.FighterPower(holder) / Math.Max(1e-6d, Power);
            if (double.IsNaN(r) || double.IsInfinity(r)) return 10f;
            return (float)Math.Max(.0001d, Math.Min(100000d, r));
        }
        public override Vector2 Thrust(Vector2 at)
        {
            if (Held) return heldThrust;
            Vector2 d = at - Centre;
            return d.sqrMagnitude > .001f ? d.normalized : Travel;
        }
        public override float GapFrom(Vector2 hand) { return Vector2.Distance(hand, Centre) - Radius; }
        public override float BodyGap(Vector2 core, float bodyRadius) { return GapFrom(core) - bodyRadius; }
        public override float OffLine(Vector2 point) { return Mathf.Abs(BeamPhysics.Cross(Travel, point - Centre)) - Radius; }
        public override Vector2 StrikePoint(Vector2 hand)
        {
            Vector2 d = hand - Centre;
            return Centre + (d.sqrMagnitude > .001f ? d.normalized : -Travel) * Radius;
        }

        // A big sphere will hit the ground long before it reaches someone standing on it, so they rise to meet it.
        public override Vector2 Intercept(Vector2 core)
        {
            if (Radius < 4f) return Vector2.zero;
            float toFighter = Mathf.Max(0f, Vector2.Distance(core, Centre) - Radius);
            var hit = Physics2D.CircleCast(Centre, Radius * .96f, Travel, toFighter + 40f, 1 << 11);
            if (!hit.collider || hit.distance > toFighter + 1f) return Vector2.zero;
            return (Centre - core).normalized * 34f;
        }

        public override void SetImmune(PersonBehaviour person, bool on) { if (on) ImmuneTo.Add(person); else ImmuneTo.Remove(person); }

        public override void Catch(Vector2 hands, Vector2 thrust)
        {
            Held = true; heldThrust = thrust; strain = 1f;
            OnHeld();
            Place(hands - thrust * Radius * .985f);
        }

        public override void Hold(Vector2 hands, Vector2 thrust, float s)
        {
            strain = s;
            Place(hands - heldThrust * Radius * .985f);
        }

        public override void Kick(float amount) { pulse = Mathf.Min(1f, pulse + .6f * amount); }

        public override void Crush(Vector2 hands, Vector2 thrust)
        {
            Held = false; Dueling = false;
            collapsing = true; collapse = 0f; collapseAt = hands; collapseRadius = Radius;
            finished = true;
            OrbFx.Implode(hands, Mathf.Min(collapseRadius, 9f), Look);
            OnFreed();
            Remove();
        }

        public override void Breakthrough(Vector2 thrust)
        {
            if (!Held) return;
            Held = false; strain = 0f;
            OnFreed();
            Launch(thrust * Mathf.Max(lastSpeed, 24f), Owner);
        }

        public override bool ThrowBack(Vector2 hands, Vector2 thrust, PersonBehaviour holder)
        {
            Held = false; strain = 0f;
            OnFreed();
            var target = Owner;
            Vector2 back = -thrust;
            if (target)
            {
                Vector2 aim = DbpuFx.Centre(target), ground, normal;
                if (BeamGround.Find(aim, 8f, target.transform.root, out ground, out normal)) aim.y = Mathf.Max(aim.y, ground.y + Radius * 1.04f);
                back = aim - Centre;
            }
            ThrownAt = target;
            Launch((back.sqrMagnitude > .01f ? back.normalized : -thrust) * Mathf.Max(ThrowSpeed, 46f), holder);
            if (target) DbpuWorldLabel.Show(target, "TURNED BACK ON THEM", "", 1.6f);
            return true;
        }

        public override void Swat(Vector2 point, float angle, bool ground, PersonBehaviour holder)
        {
            Held = false; strain = 0f;
            OnFreed();
            var previous = Owner;
            Launch(BeamBody.Direction(angle) * Mathf.Max(ThrowSpeed * 1.25f, 56f), holder);
            if (previous) DbpuWorldLabel.Show(previous, "SWATTED AWAY", "", 1.2f);
        }

        // Thrown around, a sphere travels no slower than it arrived.
        protected float lastSpeed;
        float ThrowSpeed { get { return Mathf.Max(lastSpeed * 1.2f, Speed * 1.2f); } }

        public override void Released(KiGuard.Outcome outcome)
        {
            if (!Held) return;
            Held = false; strain = 0f;
            OnFreed();
            if (Threat) Launch(heldThrust * Mathf.Max(lastSpeed, 20f), Owner);
        }

        // ------------------------------------------------------------------------ one frame
        public void Tick(float dt)
        {
            if (!Exists && !collapsing) return;
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.6f);
            if (!Held && !Dueling && Speed > .5f) lastSpeed = Speed;
            StepFrame(dt);
            DrawView(dt);
        }

        protected virtual void StepFrame(float dt) { }

        protected virtual void DrawView(float dt)
        {
            if (view == null) { collapsing = false; return; }
            if (collapsing)
            {
                collapse += dt / .26f;
                if (collapse >= 1f) { view.Destroy(); view = null; collapsing = false; return; }
                float k = 1f - collapse;
                view.Heat = collapse * .9f; view.Press = 0f; view.Opacity = 1f;
                view.Place(collapseAt, Mathf.Max(collapseRadius * k * k, .02f), dt);
                return;
            }
            if (!Exists) return;
            Vector2 v = Velocity;
            float s = v.magnitude;
            view.Move = s > .1f ? v / s : Vector2.zero;
            view.Stream = Mathf.Clamp01(s / 60f) * .9f;
            view.Press = Held || Dueling ? (.3f + strain * .7f) : 0f;
            view.PressDirection = Held ? heldThrust : view.PressDirection;
            view.Heat = pulse * .18f + (Held ? strain * .06f : 0f);
            view.Opacity = viewOpacity;
            float throb = 1f + .012f * Mathf.Sin(Time.time * 7f) + pulse * .03f;
            view.Place(Centre, Radius * throb, dt);
            if (Radius > 3f) BeamScene.Request(Centre, Vector2.up, Look.Deep, Look.Deep, Mathf.Clamp01(Radius / 18f) * .55f * viewOpacity);
            if (Look.Bolts > 0f && dt > 0f && Radius > 1.5f) PlayBolts(dt);
        }

        float boltDebt;
        void PlayBolts(float dt)
        {
            boltDebt += dt * Look.Bolts;
            int count = Mathf.Min(3, (int)boltDebt); boltDebt -= (int)boltDebt;
            Vector2 c = Centre; float r = Radius;
            Color tint = Color.Lerp(Look.Rim, Color.white, .3f);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                SingularityFx.Lightning(c + d * r * Random.Range(.86f, 1f), c + DbpuFx.Rotate(d, Random.Range(-.8f, .8f)) * r * Random.Range(1.06f, 1.34f), tint, .025f + r * .0028f, Random.Range(.08f, .18f), 9, .4f);
            }
        }

        public virtual void Dispose()
        {
            if (view != null) { view.Destroy(); view = null; }
            collapsing = false;
        }

        // ---------------------------------------------------------------------- blast damage
        static readonly Collider2D[] blastHits = new Collider2D[512];
        // Everything inside reach takes the blow, falling off towards the rim.
        protected void Blast(Vector2 at, float reach, float damage, float push, PersonBehaviour spare)
        {
            if (blastMask < 0) blastMask = LayerMask.GetMask("Objects", "CollidingDebris");
            int n = Physics2D.OverlapCircleNonAlloc(at, reach, blastHits, blastMask);
            for (int i = 0; i < n; i++)
            {
                var col = blastHits[i];
                if (!col || col.isTrigger) continue;
                var limb = col.GetComponent<LimbBehaviour>();
                var person = limb ? limb.Person : null;
                if (person && (person == spare || ImmuneTo.Contains(person))) continue;
                Vector2 away = (Vector2)col.bounds.center - at;
                float distance = away.magnitude;
                float fall = 1f - Mathf.Clamp01(distance / reach) * .75f;
                Vector2 dir = distance > .01f ? away / distance : Vector2.up;
                var body = col.attachedRigidbody;
                if (limb)
                {
                    if (person && KiShield.Protects(person, spare ? spare.gameObject : null, at, damage * fall / Mathf.Max(.001f, Time.deltaTime), GetHashCode())) continue;
                    EnergyAnnihilation.Expose(Owner, limb, KiCombatMath.Cap(Power * fall),
                        col.ClosestPoint(at), .12f, Look.Mid);
                    if (!limb.IsDismembered && !limb.ImmuneToDamage) limb.Damage(damage * fall);
                    if (body && body.bodyType == RigidbodyType2D.Dynamic && DBPU.EffectiveBeamKnockback > 0f) body.AddForce(dir * push * fall * body.mass * DBPU.EffectiveBeamKnockback, ForceMode2D.Impulse);
                    continue;
                }
                var physical = col.GetComponent<PhysicalBehaviour>();
                if (!physical || !body || body.bodyType != RigidbodyType2D.Dynamic) continue;
                body.AddForce(dir * push * .6f * fall * body.mass, ForceMode2D.Impulse);
                if (DBPU.Beam_ToggleHeat) physical.Temperature += 40f * fall;
                if (DBPU.Beam_ToggleDestroy)
                {
                    var destroyable = col.GetComponent<DestroyableBehaviour>();
                    if (destroyable) destroyable.Break();
                    var machine = col.GetComponent<DamagableMachineryBehaviour>();
                    if (machine && !machine.Destroyed) machine.ForceBreak();
                }
            }
        }
        static int blastMask = -1;
    }

    // Death Ball, Supernova, Beerus Ball and Gigantic Crusher: charged overhead, thrown, carried on
    // through whoever is in the way, and gone off against the ground or when their time is up.
    public sealed class BallOrb : KiOrb
    {
        public readonly BallBehaviour Ball;
        readonly float fullRadius, chargeTime;
        readonly Transform arm;
        // Death Ball, Supernova and Beerus Ball go on being drawn by their own prefab; only their physical side is new.
        public readonly bool OwnLook;
        float charged, chargeHum, gatherDebt, ringClock, touchClock = -1f, impactClock = -1f, eruptClock;
        float radiusNow;
        Vector2 impactNormal = Vector2.up;
        Vector2 hover; bool hoverSet;

        public BallOrb(BallBehaviour ball, Transform firingArm, string effect, float chargeSeconds)
        {
            Ball = ball; arm = firingArm; Technique = effect;
            Owner = ball.GetComponent<PersonBehaviour>();
            Look = OrbStyle.Of(effect);
            Class = Weight.Large;
            chargeTime = Mathf.Max(.2f, chargeSeconds);
            switch (effect)
            {
                case "Death Ball": fullRadius = 15f; TypeFactor = 1.2f; break;
                case "Supernova": fullRadius = 52f; TypeFactor = 1.5f; break;
                case "Beerus Ball": fullRadius = 72f; TypeFactor = 1.8f; break;
                default: fullRadius = 28f; TypeFactor = 1.5f; break;
            }
            radiusNow = .25f;
            OwnLook = effect == "Death Ball" || effect == "Supernova" || effect == "Beerus Ball";
            if (!OwnLook) { view = new OrbView(Look); HidePrefab(); }
            SpherePhysics.Register(this);
        }

        void HidePrefab()
        {
            var go = Ball.ProjectileObject;
            if (!go) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Transform child in go.transform) child.gameObject.SetActive(false);
        }

        public bool Charging { get { return Ball && (Ball.Charging || Ball.FullCharged) && !Ball.StillActive; } }
        public override bool Exists { get { return Ball && Ball.ProjectileObject && !finished; } }
        public override bool Threat { get { return Exists && Ball.StillActive && Ball.Firing && impactClock < 0f; } }
        public override Vector2 Centre { get { return Exists ? (Vector2)Ball.ProjectileObject.transform.position : collapseAt; } }
        public override Vector2 Velocity { get { return Exists && !Held && !Dueling && Ball.StillActive ? (Vector2)(Ball.BallDirection * (Ball.Speed * DBPU.Beam_SpeedMultiplier)) : Vector2.zero; } }
        public override float Radius { get { return radiusNow; } }
        public override double Power { get { return OwnerPower(Ball && Ball.KiSys && Ball.KiSys.maxKi > 0f ? Mathf.Clamp(Ball.KiSys.currentKi / Ball.KiSys.maxKi * 2f, .2f, 1f) * Mathf.Max(.01f, Ball.DamageOutput) : 1f); } }
        public float FullRadius { get { return fullRadius; } }

        // The world position of the sphere while it gathers: resting just above the raised hand, so
        // it swells upward out of the palm and never swallows the fighter holding it.
        void FollowCharge()
        {
            var go = Ball.ProjectileObject;
            if (!go) return;
            Vector2 hand = arm ? (Vector2)arm.position : DbpuFx.Centre(Owner);
            Vector2 want = hand + Vector2.up * (radiusNow + 3f);
            hover = hoverSet ? Vector2.Lerp(hover, want, 1f - Mathf.Exp(-Time.deltaTime * 6f)) : want;
            hoverSet = true;
            go.transform.position = new Vector3(hover.x, Mathf.Max(hover.y, want.y - .5f), go.transform.position.z);
        }

        public void LateStep()
        {
            if (!finished && Charging) FollowCharge();
        }

        // Fired from overhead at whatever the arm is pointing at, not parallel to it, or it would sail over them.
        public Vector3 AimDirection(Transform firingArm)
        {
            Vector2 origin = firingArm.position, line = -firingArm.up;
            Vector2 target = origin + line * 120f;
            var root = firingArm.root;
            foreach (var hit in Physics2D.RaycastAll(origin, line, 400f))
            {
                if (!hit.collider || hit.collider.isTrigger || hit.collider.transform.root == root) continue;
                if (hit.collider.GetComponent<LimbBehaviour>()) { target = hit.point + Vector2.up * fullRadius * .85f; break; }
                if (hit.collider.gameObject.layer == 11) { target = hit.point + hit.normal * fullRadius * .96f; break; }
            }
            Vector2 from = Ball.ProjectileObject.transform.position;
            Vector2 d = target - from;
            return d.sqrMagnitude > .01f ? (Vector3)d.normalized : (Vector3)line;
        }

        protected override void StepFrame(float dt)
        {
            if (!Exists) return;
            if (Charging)
            {
                if (dt > 0f) StepCharge(dt);
                return;
            }
            radiusNow = fullRadius;
            if (!Ball.StillActive) return;
            if (touchClock >= 0f && dt > 0f)
            {
                // Whoever it caught is carried along for a moment before it all goes up.
                touchClock += dt;
                if (touchClock > Mathf.Clamp(.5f + fullRadius * .01f, .5f, 1.1f)) { Detonate(-Travel); return; }
            }
            if (impactClock >= 0f && dt > 0f) { StepImpact(dt); return; }
            if (Threat && !Held && !Dueling) CheckTerrain(dt);
            if (Held && dt > 0f) StepGrind(dt);
        }

        void StepCharge(float dt)
        {
            charged += dt;
            float t = Ball.FullCharged ? 1f : Mathf.Clamp01(charged / chargeTime);
            radiusNow = Mathf.MoveTowards(radiusNow, Mathf.Max(.25f, fullRadius * t * t * (3f - 2f * t)), dt * fullRadius);
            if (OwnLook)
            {
                // The prefab grows itself; the sphere is as big as it looks, and nothing new is drawn round it.
                var go = Ball.ProjectileObject;
                if (go && Technique != "Death Ball") radiusNow = Mathf.Max(.25f, fullRadius * Mathf.Abs(go.transform.localScale.x) / (Technique == "Beerus Ball" ? 13.5f : 10f));
                return;
            }
            Vector2 c = Centre;
            var sparks = BeamParticles.Sparks;
            gatherDebt += dt * Mathf.Lerp(10f, 36f, t);
            int n = Mathf.Min(24, (int)gatherDebt); gatherDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                float from = radiusNow + Random.Range(2f, 6f + fullRadius * .35f), life = Random.Range(.25f, .55f);
                sparks.Emit(c + d * from, -d * ((from - radiusNow * .8f) / life), life, Random.Range(.05f, .13f), Color.Lerp(Look.Hot, Color.white, .5f), DbpuFx.A(Look.Mid, .2f), 0f, 0f, 0f, true);
            }
            ringClock -= dt;
            if (ringClock <= 0f)
            {
                ringClock = Mathf.Lerp(.7f, .42f, t);
                OrbFx.Ring(c, radiusNow * 1.6f + 2f, Look, .36f);
                DbpuFx.Shake(c, .08f + t * .3f);
            }
            OrbFx.Lift(c, radiusNow, Look, dt);
            // The ground under a gathering sphere starts to lift.
            Vector2 ground, normal;
            if (Random.value < dt * 12f * t && BeamGround.Find(DbpuFx.Centre(Owner), 6f, Owner.transform.root, out ground, out normal))
            {
                float x = Random.Range(-1f, 1f) * (2f + radiusNow * .35f);
                BeamGround.Tear(ground + new Vector2(x, 0f), Vector2.up, .4f, Look.Mid);
            }
        }

        void CheckTerrain(float dt)
        {
            Vector2 c = Centre, d = Travel;
            float step = Speed * Mathf.Max(dt, .016f) + .05f;
            var hits = Physics2D.CircleCastAll(c, Radius * .94f, d, step, 1 << 11);
            foreach (var hit in hits)
            {
                if (!hit.collider || hit.collider.transform.root == Ball.transform.root || hit.collider.GetComponent<KiShieldSurface>()) continue;
                BeginImpact(hit.point, hit.normal);
                return;
            }
        }

        void BeginImpact(Vector2 point, Vector2 normal)
        {
            // A sphere this size doesn't stop dead: it ploughs in, the ground splitting ahead of it.
            impactNormal = normal;
            impactClock = 0f;
            float slow = .32f;
            Ball.OriginalSpeed *= slow; Ball.Speed *= slow;
            BeamContact.Burst(point, normal, Look.Beam, Look.Beam, Mathf.Clamp(Radius * .07f, .8f, 3.2f));
            if (normal.y > .5f) BeamGround.Erupt(point, Mathf.Clamp(Radius * .12f, 1f, 5f), Look.Mid, normal);
            DbpuFx.Shake(point, Mathf.Min(1.5f + Radius * .05f, 4f));
            DbpuFx.Sound(point, ImpactFramesModule.SfxClash, .9f);
            impactPoint = point;
        }
        Vector2 impactPoint;

        void StepImpact(float dt)
        {
            impactClock += dt;
            eruptClock -= dt;
            float spread = Mathf.Clamp(Radius * .6f, 2f, 40f) * (1f + impactClock * 1.6f);
            if (eruptClock <= 0f)
            {
                eruptClock = .05f;
                Vector2 side = new Vector2(impactNormal.y, -impactNormal.x);
                Vector2 at = impactPoint + side * Random.Range(-spread, spread);
                Vector2 ground, n;
                if (BeamGround.Find(at + Vector2.up * 4f, 12f, null, out ground, out n))
                {
                    BeamSlabs.Raise(ground, Random.Range(.5f, 1.5f) * Mathf.Clamp(Radius * .06f, .8f, 3.4f), Random.Range(-30f, 30f), Look.Mid);
                    BeamGround.Tear(ground, Random.insideUnitCircle, 1f, Look.Mid);
                }
                if (Random.value < .3f) SingularityFx.Lightning(impactPoint, impactPoint + side * Random.Range(-spread, spread) + impactNormal * Random.Range(0f, spread * .5f), Color.Lerp(Look.Mid, Color.white, .6f), .1f, .16f, 11, .5f);
                DbpuFx.Shake(impactPoint, .5f);
            }
            BeamScene.Request(impactPoint, impactNormal, Look.Mid, Look.Mid, .7f);
            if (impactClock > Mathf.Clamp(Radius * .012f, .12f, .55f)) Detonate(impactNormal);
        }

        // Held against the ground its underside grinds a line of sparks through the rock.
        void StepGrind(float dt)
        {
            eruptClock -= dt;
            if (eruptClock > 0f) return;
            eruptClock = .07f;
            Vector2 ground, n;
            if (Centre.y - Radius < 4f && BeamGround.Find(Centre, Radius + 3f, null, out ground, out n))
            {
                BeamGround.Tear(ground + new Vector2(Random.Range(-1f, 1f) * Radius * .4f, 0f), Random.insideUnitCircle, strain, Look.Mid);
                if (Random.value < .4f) BeamSlabs.Raise(ground + new Vector2(Random.Range(-1f, 1f) * Radius * .4f, 0f), Random.Range(.4f, 1.2f), Random.Range(-30f, 30f), Look.Mid);
            }
        }

        // ------------------------------------------------------------------------- contact
        // Called for a limb inside the sphere. True when this contact should do no damage.
        public bool Contact(LimbBehaviour limb)
        {
            if (!limb.Person) return false;
            if (ImmuneTo.Contains(limb.Person)) return true;
            if (SpherePhysics.ReactToContact(this, limb)) return true;
            if (touchClock < 0f) touchClock = 0f;
            if (limb.Person == ThrownAt) { Detonate(-Travel); return true; }
            return false;
        }

        // ------------------------------------------------------------------------ movement
        public override void Place(Vector2 centre)
        {
            var go = Ball.ProjectileObject;
            if (!go) return;
            go.transform.position = new Vector3(centre.x, centre.y, go.transform.position.z);
            Ball.BallPosition = go.transform.position; Ball.Distance = 0f;
        }

        protected override void OnHeld() { Ball.Clashing = true; Ball.Speed = 0f; Ball.HeldOrb = true; }
        protected override void OnFreed() { Ball.HeldOrb = false; }

        protected override void Launch(Vector2 velocity, PersonBehaviour newOwner)
        {
            Ball.ReleaseArm();
            var go = Ball.ProjectileObject;
            if (!go) return;
            float speed = velocity.magnitude / Mathf.Max(.1f, DBPU.Beam_SpeedMultiplier);
            Vector2 d = velocity.sqrMagnitude > .01f ? velocity.normalized : Vector2.right;
            Ball.Clashing = false;
            Ball.BallPosition = go.transform.position; Ball.BallDirection = d; Ball.Distance = 0f;
            Ball.Speed = Ball.OriginalSpeed = speed; Ball.DelayTime = 0f;
            touchClock = -1f;
            var comp = go.GetComponent<BallBehaviour.BallComponent>();
            if (comp) { comp.RayDirection = d; comp.HitAWall = false; comp.DistanceOfWallContact = 0f; if (newOwner) comp.User = newOwner.gameObject; }
            if (newOwner) Owner = newOwner;
        }

        protected override void Remove()
        {
            Ball.ReleaseArm();
            if (OwnLook) HidePrefab();
            var go = Ball.ProjectileObject;
            if (go) foreach (var c in go.GetComponentsInChildren<Collider2D>()) c.enabled = false;
            Ball.Clashing = false;
            Ball.Firing = false;
        }

        public override void Detonate(Vector2 normal)
        {
            if (finished || !Ball) return;
            Vector2 at = Centre;
            finished = true;
            float damage = Ball.RealDamage * 100f;
            float reach = fullRadius * 1.6f + 6f;
            OrbFx.Explosion(at, fullRadius * 1.05f + 3f, Look, normal);
            Blast(at, reach, damage, Mathf.Clamp(30f + fullRadius, 30f, 90f), Owner);
            if (view != null) { view.Destroy(); view = null; }
            Remove();
        }

        // Its time ran out or the ki behind it did: it goes off wherever it is.
        public void FiringEnded() { if (!finished && Ball && Ball.StillActive) Detonate(Vector2.up); }
    }

    // Everything fired as a BlastComponent: single shots, barrages and the small special orbs.
    public sealed class BlastOrb : KiOrb
    {
        public readonly BlastComponent Source;
        readonly Rigidbody2D body; readonly float radius;
        public bool Rolled; public PersonBehaviour RolledBy;

        BlastOrb(BlastComponent blast, string technique)
        {
            Source = blast; Technique = technique;
            body = blast.GetComponent<Rigidbody2D>();
            Owner = blast.User ? blast.User.GetComponent<PersonBehaviour>() : null;
            var c = blast.GetComponent<CircleCollider2D>();
            radius = c ? Mathf.Abs(c.radius * blast.transform.lossyScale.x) : .2f;
        }

        public static BlastOrb Attach(BlastComponent blast)
        {
            string technique = string.IsNullOrEmpty(blast.Technique) ? "KiBlast" : blast.Technique;
            var orb = new BlastOrb(blast, technique);
            orb.Configure();
            SpherePhysics.Register(orb);
            return orb;
        }

        void Configure()
        {
            Color tint = Source.Tint.a > 0f ? Source.Tint : new Color(1f, .85f, .3f);
            switch (Technique)
            {
                case "StardustBreaker":
                    // Its prefab goes on drawing it; only the hands' side of things is new.
                    Class = Weight.Medium; TypeFactor = .9f; Look = OrbStyle.Of("Stardust Breaker");
                    break;
                case "CataclysmicOrb":
                    Class = Weight.Medium; TypeFactor = 1f; Look = OrbStyle.Of("Cataclysmic Orb");
                    view = new OrbView(Look);
                    break;
                default:
                    Class = Weight.Tiny; TypeFactor = Technique == "Barrage" ? .2f : .3f; Look = OrbStyle.Tinted(tint);
                    OrbShots.Add(Source.transform, tint, Source.ShotSize > 0f ? Source.ShotSize : .42f, Source.ShotTail > 0f ? Source.ShotTail : .05f);
                    break;
            }
        }

        public override bool Exists { get { return Source && !finished; } }
        public override bool Threat { get { return Exists && !Source.AlreadyHit && !Source.HasHit; } }
        public override Vector2 Centre { get { return Exists ? (Vector2)Source.transform.position : collapseAt; } }
        public override Vector2 Velocity { get { return body && body.simulated && body.bodyType != RigidbodyType2D.Static ? body.velocity : Vector2.zero; } }
        public override float Radius { get { return Class == Weight.Tiny ? radius : Mathf.Max(radius, ViewRadius); } }
        float ViewRadius { get { return Technique == "StardustBreaker" ? 1.2f : .5f; } }
        public override double Power { get { return OwnerPower(); } }

        public override void Place(Vector2 centre)
        {
            Source.transform.position = new Vector3(centre.x, centre.y, Source.transform.position.z);
            if (body) body.position = centre;
        }

        protected override void OnHeld() { Source.Held = true; if (body) { body.velocity = Vector2.zero; body.simulated = false; } }
        protected override void OnFreed() { Source.Held = false; if (body) body.simulated = true; }

        protected override void Launch(Vector2 velocity, PersonBehaviour newOwner)
        {
            if (!Source) return;
            Source.Redirect(velocity, newOwner);
            if (newOwner) Owner = newOwner;
        }

        // A quick slap: the blast is on its way somewhere else before anyone could hold it.
        public void Slap(Vector2 outDirection, PersonBehaviour fighter)
        {
            var previous = Owner;
            Launch(outDirection * Mathf.Max(Speed, 40f), fighter);
            ThrownAt = previous;
        }

        protected override void Remove()
        {
            finished = true;
            if (!Source) return;
            OrbShots.Remove(Source.transform);
            UnityEngine.Object.Destroy(Source.gameObject);
        }

        public override void Detonate(Vector2 normal)
        {
            if (Source) Source.HasHit = true;
        }

        // Fires as it goes off, from the blast itself.
        public void WentOff()
        {
            finished = true;
            if (view != null) { view.Destroy(); view = null; }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (Source) OrbShots.Remove(Source.transform);
        }
    }

    public sealed class SpiritOrb : KiOrb
    {
        public readonly SpiritBombProjectile Bomb;
        public SpiritOrb(SpiritBombProjectile bomb, PersonBehaviour owner, string technique)
        {
            Bomb = bomb; Owner = owner; Technique = technique;
            Class = Weight.Large; Look = OrbStyle.Of("Spirit Bomb"); TypeFactor = 1f;
            view = new OrbView(Look, "Background", -19);
            SpherePhysics.Register(this);
        }

        public override bool Exists { get { return Bomb && !finished && Bomb.Phase < SpiritBombPhase.Detonating; } }
        public override bool Threat { get { return Exists && Bomb.Phase == SpiritBombPhase.Flight; } }
        public override Vector2 Centre { get { return Bomb ? (Vector2)Bomb.transform.position : collapseAt; } }
        public override Vector2 Velocity { get { return Threat && !Held && !Dueling ? Bomb.CurrentVelocity : Vector2.zero; } }
        public override float Radius { get { return Bomb ? Bomb.Radius : 0f; } }
        public override double Power
        {
            get
            {
                float charge = Bomb ? Mathf.Sqrt(Mathf.Max(0f, Bomb.Energy / Mathf.Max(1f, Bomb.ReferenceKi))) : 1f;
                TypeFactor = Mathf.Clamp(.7f + charge * .7f, .7f, 3f);
                return OwnerPower();
            }
        }
        public override void Place(Vector2 centre) { if (Bomb) Bomb.HoldAt(centre); }
        protected override void OnHeld() { if (Bomb) Bomb.SetHeld(true); }
        protected override void OnFreed() { if (Bomb) Bomb.SetHeld(false); }
        protected override void Launch(Vector2 velocity, PersonBehaviour newOwner)
        {
            if (!Bomb) return;
            Bomb.Redirect(velocity, newOwner);
            if (newOwner) Owner = newOwner;
        }
        protected override void Remove() { finished = true; if (Bomb) Bomb.Vanish(); }
        public override void Detonate(Vector2 normal) { if (Bomb) Bomb.Detonate(); }
        public void WentOff()
        {
            finished = true;
            if (view != null) { view.Destroy(); view = null; }
        }
        public void SetOpacity(float value) { viewOpacity = value; }
        protected override void StepFrame(float dt) { if (dt > 0f && Exists) OrbFx.Lift(Centre, Radius, Look, dt); }
    }

    public sealed class HakaiOrb : KiOrb
    {
        public readonly HakaiBallOrb Ball;
        double power; float driven = .1f;
        public HakaiOrb(HakaiBallOrb ball, PersonBehaviour owner)
        {
            // The ball goes on drawing itself; this is only its physical side.
            Ball = ball; Owner = owner; Technique = "Hakai Ball";
            Class = Weight.Medium; Look = OrbStyle.Of("Hakai Ball"); TypeFactor = 1.6f;
            // Weaker fighters get the seal; only a real gulf in power lets someone bat it away.
            MinimumRatio = BeamPhysics.OneHandRatio;
            SpherePhysics.Register(this);
        }
        public override bool Exists { get { return Ball && !finished; } }
        public override bool Threat { get { return Exists && Ball.Flying; } }
        public override Vector2 Centre { get { return Ball ? (Vector2)Ball.transform.position : collapseAt; } }
        public override Vector2 Velocity { get { return Threat && !Held && !Dueling ? Ball.CurrentVelocity : Vector2.zero; } }
        public override float Radius { get { return driven; } }
        public override double Power { get { return Math.Max(1e-6d, power * 1.6d); } }
        public void Drive(float radius, float opacity, double attackPower) { driven = radius; power = attackPower; }
        public override void Place(Vector2 centre) { if (Ball) Ball.HoldAt(centre); }
        protected override void OnHeld() { if (Ball) Ball.SetHeld(true); }
        protected override void OnFreed() { if (Ball) Ball.SetHeld(false); }
        protected override void Launch(Vector2 velocity, PersonBehaviour newOwner)
        {
            if (!Ball) return;
            Ball.Redirect(velocity, newOwner);
            if (newOwner) Owner = newOwner;
        }
        protected override void Remove() { finished = true; if (Ball) Ball.Fizzle(); }
        public override void Detonate(Vector2 normal) { Remove(); }
    }
}
