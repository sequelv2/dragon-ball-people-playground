using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DBPU
{
    // Energy beams as physical things in the fight. Strong fighters catch them, hold them back and
    // crush them or drive them home; far stronger ones swat them aside; beams bend with the hands
    // firing them and with everything they push against.
    public sealed class BeamPhysicsModule : SingularityModule
    {
        public override string Id { get { return BeamPhysics.ModuleId; } }
        public override string Title { get { return "Dynamic Beams"; } }
        public override string Summary { get { return "Beams are physical: strong fighters catch them in one or both hands, hold them back and crush them or push them home; overwhelming fighters swat them into the sky or the ground; beams bend with the firing hand and in head-on clashes."; } }
        public override void Tick(float dt) { BeamPhysics.Tick(dt); }
        public override void FixedTick(float dt) { BeamPhysics.FixedTick(dt); }
        public override void OnDisabled() { BeamPhysics.Shutdown(); }
    }

    public static class BeamPhysics
    {
        public const string ModuleId = "beamphysics";
        // Power of the fighter over power of the beam: below Catch they are simply hit.
        public const float CatchRatio = .6f, OneHandRatio = 1.8f, SwatRatio = 6f;
        public static bool On { get { return Singularity.IsEnabled(ModuleId); } }
        // QA and special cases: -1 forces a swat into the ground, +1 into the sky, 0 lets it choose.
        public static int SwatPreference;

        public static readonly List<KiGuard> Guards = new List<KiGuard>();
        sealed class Power { public double Value; public float Until; }
        static readonly Dictionary<PersonBehaviour, Power> powers = new Dictionary<PersonBehaviour, Power>();
        static readonly Dictionary<PersonBehaviour, float> blocked = new Dictionary<PersonBehaviour, float>();
        static readonly List<BeamBody> scan = new List<BeamBody>();

        // Called where the old code froze the firing arm.
        public static void PinFiringLimb(BeamBehaviour beam, Transform arm)
        {
            if (On)
            {
                var body = BeamBody.Of(beam);
                if (body) body.PrepareFire(arm);
            }
            if (arm && !arm.GetComponent<FreezeBehaviour>()) arm.gameObject.AddComponent<FreezeBehaviour>();
        }

        // ------------------------------------------------------------------------------ power
        public static double FighterPower(PersonBehaviour person)
        {
            Power p;
            if (!powers.TryGetValue(person, out p)) { p = new Power(); powers[person] = p; }
            if (Time.time >= p.Until) { p.Until = Time.time + .15f; p.Value = DbpuFx.PowerOf(person); }
            return p.Value;
        }

        // One beam's strength in the same units as a fighter's power: its owner's power, scaled by
        // how hard the technique hits, how much ki is behind it and any beams merged into it.
        public static double BeamPower(BeamBehaviour beam)
        {
            if (!beam) return 1d;
            double total = OneBeam(beam);
            if (beam.MergedBeams != null)
                foreach (var merged in beam.MergedBeams)
                    if (merged && merged.StillActive) total += OneBeam(merged);
            return Math.Max(1e-6d, total);
        }

        static double OneBeam(BeamBehaviour beam)
        {
            var owner = beam.GetComponent<PersonBehaviour>();
            double power = owner ? FighterPower(owner) : 1d;
            var multipliers = beam.Multipliers;
            float divisor = 2000f;
            if (owner && multipliers && beam.Damage > 0f)
                divisor = CombatPowerController.AttackHealth(owner) / Mathf.Max(.0001f, multipliers.FinalMultiplier) / beam.Damage;
            float type = Mathf.Clamp(Mathf.Sqrt(2000f / Mathf.Max(1f, divisor)), .45f, 1.7f);
            float reserve = 1f;
            if (beam.KiSys && beam.KiSys.maxKi > 0f) reserve = Mathf.Clamp(beam.KiSys.currentKi / beam.KiSys.maxKi * 2f, .2f, 1f);
            return power * type * reserve * Mathf.Max(.01f, beam.DamageOutput);
        }

        public static float Ratio(PersonBehaviour person, BeamBehaviour beam)
        {
            double r = FighterPower(person) / BeamPower(beam);
            if (double.IsNaN(r) || double.IsInfinity(r)) return 10f;
            return (float)Math.Max(.0001d, Math.Min(100000d, r));
        }

        public static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        public static float BodyRadius(PersonBehaviour person)
        {
            var bounds = DbpuFx.BodyBounds(person);
            return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y) * .7f, .5f, 8f);
        }

        // ----------------------------------------------------------------------------- reacting
        public static void BlockReaction(PersonBehaviour person, float seconds) { if (person) blocked[person] = Time.time + seconds; }

        public static KiGuard GuardOf(PersonBehaviour person)
        {
            for (int i = 0; i < Guards.Count; i++) if (Guards[i].Holder == person) return Guards[i];
            return null;
        }

        public static KiGuard GuardOn(BeamBehaviour beam)
        {
            for (int i = 0; i < Guards.Count; i++) if (Guards[i].Beam == beam) return Guards[i];
            return null;
        }

        public static KiGuard GuardOn(KiOrb orb)
        {
            for (int i = 0; i < Guards.Count; i++) if (Guards[i].Orb == orb) return Guards[i];
            return null;
        }

        static bool CanReact(PersonBehaviour person, BeamBehaviour beam) { return CanReact(person, beam.transform.root); }

        public static bool CanReact(PersonBehaviour person, Transform attackerRoot)
        {
            if (!person || !person.IsAlive() || person.Braindead || person.Consciousness < .4f) return false;
            if (attackerRoot && attackerRoot == person.transform.root) return false;
            var ki = person.GetComponent<KiSystem>();
            if (!ki || ki.SparkingExhaustActive) return false;
            if (DBPU.KiAttackCostMultiplier > 0f && SingularityFighters.KiFraction(person) < .05f) return false;
            float until;
            if (blocked.TryGetValue(person, out until) && Time.time < until) return false;
            var shield = person.GetComponent<KiShield>();
            if (shield && shield.Using) return false;
            var own = person.GetComponent<BeamBehaviour>();
            if (own && (own.Firing || own.Charging)) return false;
            var ball = person.GetComponent<BallBehaviour>();
            if (ball && (ball.Firing || ball.Charging)) return false;
            if (person.GetComponent<HakaiDissolveMarker>() || person.GetComponent<TimePrison>() || SingularityWorldFreeze.Active || RealityRift.Active) return false;
            var rig = BeamRig.For(person);
            if (!rig || !rig.Limb("MiddleBody") || rig.Limb("MiddleBody").GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Dynamic) return false;
            bool front = rig.Usable("UpperArmFront") && rig.Usable("LowerArmFront"), back = rig.Usable("UpperArm") && rig.Usable("LowerArm");
            return front || back;
        }

        static KiGuard Raise(PersonBehaviour person, BeamBehaviour beam, float ratio)
        {
            var existing = GuardOf(person);
            if (existing != null) return existing.Beam == beam ? existing : null;
            return Begin(person, new BeamTarget(beam), ratio);
        }

        public static KiGuard RaiseGuard(PersonBehaviour person, KiOrb orb, float ratio)
        {
            var existing = GuardOf(person);
            if (existing != null) return existing.Orb == orb ? existing : null;
            return Begin(person, orb, ratio);
        }

        static KiGuard Begin(PersonBehaviour person, GuardTarget target, float ratio)
        {
            var kind = ratio >= SwatRatio ? KiGuard.Kind.Swat : ratio >= OneHandRatio ? KiGuard.Kind.OneHand : KiGuard.Kind.TwoHands;
            var guard = new KiGuard(target, person, kind, ratio);
            Guards.Add(guard);
            return guard;
        }

        // Watches every free beam head and gets strong enough fighters ready in time.
        static void ScanForHeads()
        {
            scan.Clear(); scan.AddRange(BeamBody.Active);
            var people = SingularityRunner.Persons;
            foreach (var body in scan)
            {
                if (!body || !body.Live || body.Pinned) continue;
                var beam = body.Beam;
                if (!beam || !beam.Firing || beam.Clashing || beam.MergeHost != null || GuardOn(beam) != null) continue;
                float speed = Mathf.Max(body.TipSpeed, 30f);
                float lead = speed * .24f + 2.5f;
                float half = Mathf.Max(body.Width, .2f) * .5f;
                for (int i = 0; i < people.Count; i++)
                {
                    var person = people[i];
                    if (!person || body.Immune.Contains(person)) continue;
                    Vector2 to = DbpuFx.Centre(person) - body.Tip;
                    float along = Vector2.Dot(to, body.TipTangent);
                    if (along < -1.5f || along > lead) continue;
                    if (Mathf.Abs(Cross(body.TipTangent, to)) > BodyRadius(person) + half + .35f) continue;
                    if (!CanReact(person, beam)) continue;
                    float ratio = Ratio(person, beam);
                    if (ratio < CatchRatio) continue;
                    Raise(person, beam, ratio);
                }
            }
        }

        // The beam touched a limb before any guard resolved: side-on contact, or a head that came
        // from too close. True when this contact has been dealt with and should do no damage.
        public static bool ReactToContact(BeamBehaviour beam, LimbBehaviour limb)
        {
            if (!On || !beam || !limb || !limb.Person || !beam.Body || !beam.Body.Live) return false;
            var person = limb.Person;
            if (beam.Body.Immune.Contains(person)) return true;
            var mine = GuardOf(person);
            if (mine != null && mine.Beam == beam) { mine.ContactNow(); return true; }
            if (mine != null || beam.Clashing || beam.Body.Pinned || !beam.Firing || !CanReact(person, beam)) return false;
            float ratio = Ratio(person, beam);
            if (ratio < CatchRatio) return false;
            // Caught mid-column: whatever had already gone past is cut off at the hands.
            var guard = Raise(person, beam, ratio);
            if (guard == null) return false;
            guard.ContactNow();
            return true;
        }

        public static bool Ignores(BeamBehaviour beam, PersonBehaviour person) { return On && beam && beam.Body && person && beam.Body.Immune.Contains(person); }

        public static bool TerrainAhead(Vector2 from, Vector2 direction, float reach)
        {
            var hit = Physics2D.Raycast(from, direction, reach, 1 << 11);
            return hit.collider;
        }

        // ------------------------------------------------------------------------------ frames
        public static void Tick(float dt)
        {
            if (dt > 0f) ScanForHeads();
            TickGuards(dt);
        }

        // Guards are shared with the sphere attacks, which tick them when beams are switched off.
        public static void TickGuards(float dt)
        {
            for (int i = Guards.Count - 1; i >= 0; i--)
            {
                bool alive = false;
                var guard = Guards[i];
                try { alive = guard.Tick(dt); }
                catch (Exception ex) { Debug.LogWarning("[Singularity] beam guard: " + ex); try { guard.Abort(); } catch { } }
                if (!alive) Guards.RemoveAt(i);
            }
            PruneDead();
        }

        static float pruneClock;
        static void PruneDead()
        {
            pruneClock -= Time.unscaledDeltaTime;
            if (pruneClock > 0f) return;
            pruneClock = 5f;
            var stale = new List<PersonBehaviour>();
            foreach (var pair in powers) if (!pair.Key) stale.Add(pair.Key);
            foreach (var key in stale) powers.Remove(key);
            stale.Clear();
            foreach (var pair in blocked) if (!pair.Key || Time.time > pair.Value) stale.Add(pair.Key);
            foreach (var key in stale) blocked.Remove(key);
        }

        public static void FixedTick(float dt)
        {
            for (int i = 0; i < Guards.Count; i++) Guards[i].FixedStep(dt);
        }

        public static void Shutdown()
        {
            foreach (var guard in Guards.ToArray()) if (guard.Beam) guard.Abort();
            Guards.RemoveAll(g => g.Beam);
            BeamBolt.DestroyAll();
        }

        // Runs a short-lived quad out to nothing.
        public static void Fade(BeamQuad quad, Vector2 at, float radius, float seconds, float growth = .22f, bool expandPower = false)
        {
            quad.Place(at, radius);
            quad.Apply(0f);
            BeamFxRunner.Run(FadeRoutine(quad, at, radius, seconds, growth, expandPower));
        }

        // Real time, so a burst still blooms during the hitstop that goes with it.
        static IEnumerator FadeRoutine(BeamQuad quad, Vector2 at, float radius, float seconds, float growth, bool expandPower)
        {
            float age = 0f;
            while (age < seconds)
            {
                float t = age / seconds;
                float grown = 1f - (1f - t) * (1f - t);
                quad.Power = expandPower ? Mathf.Lerp(.25f, 1f, grown) : 1f;
                quad.Opacity = 1f - t * t;
                quad.Place(at, radius * (1f + grown * growth));
                quad.Apply(Time.unscaledDeltaTime);
                yield return null;
                age += Time.unscaledDeltaTime;
            }
            quad.Destroy();
        }

        // ------------------------------------------------------------------------------ rays
        // Thin instant or continuous rays have no head to catch, but the hand can still meet them.
        // Returns None, or where the ray ended and, for a deflection, where it now goes.
        public enum RayResult { None, Blocked, Deflected }
        public struct RayState { public Vector2 Lag, OutDirection; public float OutUntil; }

        // For continuous rays: keeps a deflection going the same way for as long as the hand is there.
        public static RayResult MeetRay(GameObject attackerRoot, LimbBehaviour limb, Vector2 incoming, float typeFactor, ref RayState state, out Vector2 stopAt, out Vector2 outDirection)
        {
            stopAt = Vector2.zero; outDirection = incoming;
            if (!limb) return RayResult.None;
            var result = InterceptRay(attackerRoot, limb, incoming, typeFactor, out stopAt, out outDirection);
            if (result != RayResult.Deflected) return result;
            if (Time.time < state.OutUntil) outDirection = state.OutDirection;
            state.OutDirection = outDirection; state.OutUntil = Time.time + .25f;
            return result;
        }

        public static RayResult InterceptRay(GameObject attackerRoot, LimbBehaviour limb, Vector2 incoming, float typeFactor, out Vector2 stopAt, out Vector2 outDirection)
        {
            stopAt = Vector2.zero; outDirection = incoming;
            if (!On || !limb || !limb.Person || !attackerRoot) return RayResult.None;
            var person = limb.Person;
            var attacker = attackerRoot.GetComponent<PersonBehaviour>();
            if (!attacker || attacker == person) return RayResult.None;
            if (!person.IsAlive() || person.Braindead || person.Consciousness < .4f) return RayResult.None;
            var ki = person.GetComponent<KiSystem>();
            if (!ki || ki.SparkingExhaustActive) return RayResult.None;
            float until;
            if (blocked.TryGetValue(person, out until) && Time.time < until) return RayResult.None;
            var own = person.GetComponent<BeamBehaviour>();
            if (own && (own.Firing || own.Charging)) return RayResult.None;
            if (GuardOf(person) != null) return RayResult.None;
            double power = FighterPower(attacker) * Mathf.Max(.05f, typeFactor);
            float ratio = (float)Math.Min(100000d, FighterPower(person) / Math.Max(1e-6d, power));
            if (ratio < OneHandRatio * .7f) return RayResult.None;
            var rig = BeamRig.For(person);
            string lower = rig.Usable("LowerArmFront") && rig.Usable("UpperArmFront") ? "LowerArmFront" : rig.Usable("LowerArm") && rig.Usable("UpperArm") ? "LowerArm" : null;
            if (lower == null) return RayResult.None;
            var hand = rig.Limb(lower);
            Vector2 c = DbpuFx.Centre(person);
            Vector2 palm = c - incoming.normalized * 1.15f;
            stopAt = palm;
            bool deflect = ratio >= OneHandRatio;
            if (deflect)
            {
                float side = Random.value < .5f ? -1f : 1f;
                outDirection = DbpuFx.Rotate(incoming, side * Random.Range(.9f, 1.9f));
                if (outDirection.y < -.3f && Random.value < .7f) outDirection.y = -outDirection.y;
            }
            RaySnap.Play(rig, lower, -incoming, palm, deflect ? outDirection : -incoming, ki);
            return deflect ? RayResult.Deflected : RayResult.Blocked;
        }

        // Draws a ray as a shallow curve lagging behind its own sweep, optionally kinked at a hand.
        public static void DrawRay(LineRenderer core, LineRenderer glow, Vector2 origin, Vector2 end, ref Vector2 lag, float dt, bool hasKink = false, Vector2 kink = default(Vector2))
        {
            Vector2 dir = end - origin;
            float length = dir.magnitude;
            if (length < .001f) return;
            dir /= length;
            if (lag.sqrMagnitude < .5f) lag = dir;
            lag = ((Vector2)Vector3.RotateTowards(lag, dir, dt * 5f, 0f)).normalized;
            const int samples = 9;
            int count = hasKink ? samples + 1 : samples;
            SetLine(core, count, origin, end, lag, length, hasKink, kink);
            if (glow) SetLine(glow, count, origin, end, lag, length, hasKink, kink);
        }

        static readonly Vector3[] rayPoints = new Vector3[12];
        static void SetLine(LineRenderer line, int count, Vector2 origin, Vector2 end, Vector2 lag, float length, bool hasKink, Vector2 kink)
        {
            int samples = hasKink ? count - 1 : count;
            Vector2 target = hasKink ? kink : end;
            float l = Vector2.Distance(origin, target);
            Vector2 control = origin + (lag * l * .5f);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1), u = 1f - t;
                rayPoints[i] = u * u * origin + 2f * u * t * control + t * t * target;
            }
            if (hasKink) rayPoints[samples] = end;
            line.positionCount = count;
            line.SetPositions(rayPoints);
        }
    }

    // Owns coroutines for effects that outlive whatever started them.
    public sealed class BeamFxRunner : MonoBehaviour
    {
        static BeamFxRunner instance;
        public static void Run(IEnumerator routine)
        {
            if (!instance)
            {
                var go = new GameObject("DBPU beam fx runner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<BeamFxRunner>();
            }
            instance.StartCoroutine(routine);
        }
    }

    // A hand snapped up to meet a ray, held for a beat, then let go.
    public sealed class RaySnap : MonoBehaviour
    {
        BeamRig rig; string lower, upper; Vector2 aim; float left; BeamStance stance; Color tint;
        static float effectClock;

        public static void Play(BeamRig rig, string lowerArm, Vector2 aim, Vector2 point, Vector2 spray, KiSystem ki, float size = 1f)
        {
            var snap = rig.GetComponent<RaySnap>();
            if (!snap) snap = rig.gameObject.AddComponent<RaySnap>();
            snap.rig = rig; snap.lower = lowerArm; snap.upper = lowerArm == "LowerArmFront" ? "UpperArmFront" : "UpperArm";
            snap.aim = aim; snap.left = .38f;
            var palette = DbpuAuraPalette.Get(rig.Person);
            snap.tint = palette.Vivid;
            if (snap.stance == null) snap.stance = new BeamStance(rig);
            rig.FreeShoulder(snap.upper);
            if (ki && DBPU.KiAttackCostMultiplier > 0f) ki.currentKi = Mathf.Max(0f, ki.currentKi - KiCombatMath.MaxKi(ki) * .004f * DBPU.KiAttackCostMultiplier);
            if (Time.unscaledTime < effectClock) return;
            effectClock = Time.unscaledTime + .09f;
            SingularityFx.Flash(point, Color.white, .9f * size, .16f);
            SingularityFx.Ring(point, snap.tint, 1.1f * size, .22f);
            var sparks = BeamParticles.Sparks;
            for (int i = 0; i < 18; i++)
            {
                Vector2 dir = DbpuFx.Rotate(spray, Random.Range(-.9f, .9f));
                sparks.Emit(point, dir * Random.Range(7f, 26f), Random.Range(.12f, .34f), Random.Range(.04f, .1f), Color.white, DbpuFx.A(snap.tint, 0f), 2f, 2f, 0f, true);
            }
            DbpuFx.Shake(point, .5f);
            DbpuFx.Sound(point, "Assets/SFX/Techniques/Kienzan/Clang.wav", .5f);
        }

        void FixedUpdate()
        {
            if (!rig || left <= 0f || DbpuFx.Paused) return;
            left -= Time.fixedDeltaTime;
            if (left <= 0f) { rig.ReleaseAll(); return; }
            rig.Aim(upper, aim, 40f, 2f);
            rig.Aim(lower, aim, 40f, 2f);
            stance.Drive(Vector2.zero, 90f, Time.fixedDeltaTime, 0f, tint);
        }
    }
}
