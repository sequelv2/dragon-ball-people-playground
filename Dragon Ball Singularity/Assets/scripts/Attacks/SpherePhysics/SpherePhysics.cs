using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DBPU
{
    // Spheres of ki as physical things in the fight, with the same rules as beams: strong fighters
    // catch them, hold them back and crush them or throw them home; far stronger ones swat them
    // away; small blasts get slapped aside; big spheres lock with beams and with each other.
    public sealed class SpherePhysicsModule : SingularityModule
    {
        public override string Id { get { return SpherePhysics.ModuleId; } }
        public override string Title { get { return "Dynamic Ki Spheres"; } }
        public override string Summary { get { return "Spheres are physical and redrawn: strong fighters catch them in one or both hands, hold them back and crush them or throw them home; overwhelming fighters swat them away; small blasts get slapped aside; big spheres lock with beams and each other, and go off against the ground."; } }
        public override void Tick(float dt) { SpherePhysics.Tick(dt); }
        public override void FixedTick(float dt) { SpherePhysics.FixedTick(dt); }
        public override void OnDisabled() { SpherePhysics.Shutdown(); }
    }

    public static class SpherePhysics
    {
        public const string ModuleId = "spherephysics";
        public static bool On { get { return Singularity.IsEnabled(ModuleId); } }
        // A blast is slapped away at this power ratio some of the time, and at the second one always.
        public const float SlapFrom = 2f, SlapAlways = 7f;

        public static readonly List<KiOrb> Orbs = new List<KiOrb>();
        static readonly List<KiOrb> tick = new List<KiOrb>();
        static readonly Dictionary<PersonBehaviour, float> slapped = new Dictionary<PersonBehaviour, float>();

        public static void Register(KiOrb orb) { Orbs.Add(orb); }

        public static void Tick(float dt)
        {
            tick.Clear(); tick.AddRange(Orbs);
            foreach (var orb in tick)
            {
                try { orb.Tick(dt); }
                catch (Exception ex) { Debug.LogWarning("[Singularity] sphere: " + ex); orb.Dispose(); Orbs.Remove(orb); }
            }
            foreach (var orb in tick) if (orb.Gone) orb.Dispose();
            Orbs.RemoveAll(o => o.Gone);
            if (dt > 0f)
            {
                ScanAhead();
                MeetEachOther();
                OrbDuel.StepAll(dt);
            }
            if (!BeamPhysics.On) BeamPhysics.TickGuards(dt);
        }

        public static void FixedTick(float dt)
        {
            if (!BeamPhysics.On) BeamPhysics.FixedTick(dt);
        }

        public static void Shutdown()
        {
            OrbDuel.EndAll();
            foreach (var guard in BeamPhysics.Guards.ToArray()) if (guard.Orb != null) guard.Abort();
            foreach (var orb in Orbs) orb.Dispose();
            Orbs.Clear();
        }

        // ---------------------------------------------------------------------------- reacting
        // Watches everything in flight and gets strong enough fighters ready in time.
        static void ScanAhead()
        {
            var people = SingularityRunner.Persons;
            foreach (var orb in Orbs)
            {
                if (!orb.Threat || orb.Interrupted) continue;
                if (orb.Class == KiOrb.Weight.Tiny) { WatchBlast((BlastOrb)orb, people); continue; }
                if (BeamPhysics.GuardOn(orb) != null) continue;
                Vector2 velocity = orb.Velocity, c = orb.Centre;
                float speed = Mathf.Max(velocity.magnitude, 12f), r = orb.Radius;
                // The bigger it is the sooner they see it coming, and the further they have to rise.
                float lead = speed * (r > 4f ? .75f : .28f) + 2.5f;
                Vector2 dir = orb.Travel;
                for (int i = 0; i < people.Count; i++)
                {
                    var person = people[i];
                    if (!person || orb.Owner == person || orb.ImmuneTo.Contains(person)) continue;
                    Vector2 to = DbpuFx.Centre(person) - c;
                    float radius = BeamPhysics.BodyRadius(person);
                    if (Vector2.Dot(to, dir) < -r - radius) continue;
                    if (to.magnitude - r - radius > lead) continue;
                    if (Mathf.Abs(BeamPhysics.Cross(dir, to)) > r + radius + .35f) continue;
                    if (!BeamPhysics.CanReact(person, orb.Owner ? orb.Owner.transform.root : null)) continue;
                    float ratio = orb.RatioFor(person);
                    if (ratio < orb.MinimumRatio) continue;
                    BeamPhysics.RaiseGuard(person, orb, ratio);
                }
            }
        }

        static void WatchBlast(BlastOrb orb, IList<PersonBehaviour> people)
        {
            Vector2 c = orb.Centre, velocity = orb.Velocity;
            float speed = velocity.magnitude;
            for (int i = 0; i < people.Count; i++)
            {
                var person = people[i];
                if (!person || orb.Owner == person || orb.RolledBy == person) continue;
                Vector2 to = DbpuFx.Centre(person) - c;
                if (to.magnitude - BeamPhysics.BodyRadius(person) > speed * .05f + .6f) continue;
                if (Vector2.Dot(to, velocity) <= 0f) continue;
                TrySlap(orb, person);
                return;
            }
        }

        // One roll per blast: a fighter of the same strength gets some of them, a far stronger one all.
        static bool TrySlap(BlastOrb orb, PersonBehaviour person)
        {
            if (orb.RolledBy == person) return false;
            orb.RolledBy = person;
            if (!orb.Threat || !BeamPhysics.CanReact(person, orb.Owner ? orb.Owner.transform.root : null) || BeamPhysics.GuardOf(person) != null) return false;
            float until;
            if (slapped.TryGetValue(person, out until) && Time.time < until) return false;
            float ratio = orb.RatioFor(person);
            float chance = Mathf.Clamp01((ratio - SlapFrom) / (SlapAlways - SlapFrom));
            if (chance <= 0f || Random.value > chance) return false;
            var rig = BeamRig.For(person);
            string lower = rig.Usable("LowerArmFront") && rig.Usable("UpperArmFront") && (Random.value < .6f || !rig.Usable("LowerArm")) ? "LowerArmFront" : rig.Usable("LowerArm") && rig.Usable("UpperArm") ? "LowerArm" : null;
            if (lower == null) return false;
            slapped[person] = Time.time + .07f;
            Vector2 incoming = orb.Travel;
            float side = Random.value < .5f ? -1f : 1f;
            Vector2 outward = DbpuFx.Rotate(incoming, side * Random.Range(1.7f, 2.7f));
            if (outward.y < -.25f) outward.y = -outward.y;
            RaySnap.Play(rig, lower, -incoming, orb.Centre, outward, person.GetComponent<KiSystem>(), .34f);
            orb.Slap(outward, person);
            return true;
        }

        // A blast touching a limb before anyone reacted. True when it should not go off.
        public static bool SlapOnContact(BlastOrb orb, LimbBehaviour limb)
        {
            if (!On || orb == null || !limb || !limb.Person || limb.Person == orb.Owner) return false;
            return TrySlap(orb, limb.Person);
        }

        // Something bigger touched a limb before any guard resolved. True when this contact has been
        // dealt with and should do no damage.
        public static bool ReactToContact(KiOrb orb, LimbBehaviour limb)
        {
            if (!On || orb == null || !limb || !limb.Person) return false;
            var person = limb.Person;
            if (orb.ImmuneTo.Contains(person)) return true;
            var mine = BeamPhysics.GuardOf(person);
            if (mine != null && mine.Orb == orb) { mine.ContactNow(); return true; }
            if (mine != null || orb.Interrupted || !orb.Threat || !BeamPhysics.CanReact(person, orb.Owner ? orb.Owner.transform.root : null)) return false;
            float ratio = orb.RatioFor(person);
            if (ratio < orb.MinimumRatio) return false;
            var guard = BeamPhysics.RaiseGuard(person, orb, ratio);
            if (guard == null) return false;
            guard.ContactNow();
            return true;
        }

        // --------------------------------------------------------------- energy meeting energy
        static void MeetEachOther()
        {
            for (int i = 0; i < Orbs.Count; i++)
            {
                var a = Orbs[i];
                if (!a.Threat || a.Class != KiOrb.Weight.Large) continue;
                Vector2 c = a.Centre;
                float r = a.Radius;
                // Small things flying into it are simply swallowed.
                for (int k = 0; k < Orbs.Count; k++)
                {
                    var b = Orbs[k];
                    if (b == a || !b.Threat || b.Owner == a.Owner) continue;
                    float distance = Vector2.Distance(c, b.Centre);
                    if (distance > r + b.Radius) continue;
                    if (b.Class != KiOrb.Weight.Large)
                    {
                        if (b is BlastOrb && !a.Interrupted) b.Detonate(Vector2.up);
                        continue;
                    }
                    if (k > i && !a.Interrupted && !b.Interrupted) OrbDuel.Begin(a, b);
                }
                if (a.Interrupted) continue;
                // A beam running into it, head first or side on.
                foreach (var body in BeamBody.Active)
                {
                    if (!body || !body.Live || body.Pinned) continue;
                    var beam = body.Beam;
                    if (!beam || !beam.Firing || beam.Clashing || beam.HeldByFighter || beam.MergeHost != null) continue;
                    if (beam.GetComponent<PersonBehaviour>() == a.Owner) continue;
                    float arc; Vector2 tangent;
                    Vector2 near = body.ClosestPoint(c, out arc, out tangent);
                    if (Vector2.Distance(near, c) > r + body.Width * .3f) continue;
                    OrbDuel.Begin(beam, a);
                    break;
                }
            }
            // Beams arriving at a sphere that is already locked with another beam add their weight.
            foreach (var duel in OrbDuel.All)
            {
                if (duel.Orb == null || duel.Beam == null) continue;
                foreach (var body in BeamBody.Active)
                {
                    if (!body || !body.Live || body.Pinned || !body.Beam || body.Beam == duel.Beam) continue;
                    var beam = body.Beam;
                    if (!beam.Firing || beam.Clashing || beam.HeldByFighter || beam.GetComponent<PersonBehaviour>() == duel.Orb.Owner) continue;
                    if (Vector2.Distance(body.Tip, duel.Point) > duel.Orb.Radius * 1.1f + 2f) continue;
                    duel.Join(beam);
                    break;
                }
            }
        }
    }

    // Two sides pressing on one point: a beam against a sphere, or a sphere against a sphere. Each
    // pushes with its current power, neither holds perfectly steady, and when one gives the other
    // carries on through.
    public sealed class OrbDuel
    {
        const float MaxSpeed = 9f;
        public static readonly List<OrbDuel> All = new List<OrbDuel>();
        static readonly List<OrbDuel> step = new List<OrbDuel>();

        public readonly KiOrb Orb, Other; public readonly BeamBehaviour Beam;
        readonly List<BeamBehaviour> helpers = new List<BeamBehaviour>();
        public Vector2 Point { get; private set; }
        public float Age { get; private set; }
        public float Score { get; private set; }
        public float Balance { get; private set; }
        readonly BeamContact contact;
        readonly float seed = Random.value * 20f;
        readonly Vector2 axis;
        Vector2 velocity, surge;
        float surgeClock = 1f, groundClock, strain;

        public static OrbDuel Of(KiOrb orb)
        {
            foreach (var d in All) if (d.Orb == orb || d.Other == orb) return d;
            return null;
        }

        public static OrbDuel Begin(BeamBehaviour beam, KiOrb orb)
        {
            if (orb.Interrupted || beam.Clashing) return null;
            var duel = new OrbDuel(beam, orb);
            All.Add(duel);
            return duel;
        }

        public static OrbDuel Begin(KiOrb a, KiOrb b)
        {
            if (a.Interrupted || b.Interrupted) return null;
            var duel = new OrbDuel(a, b);
            All.Add(duel);
            return duel;
        }

        OrbDuel(BeamBehaviour beam, KiOrb orb)
        {
            Beam = beam; Orb = orb;
            var body = beam.Body;
            Vector2 start = body.SpanStart, centre = orb.Centre;
            axis = (centre - start).sqrMagnitude > .01f ? (centre - start).normalized : body.TipTangent;
            Point = centre - axis * orb.Radius * .97f;
            orb.EnterDuel(axis * -1f);
            beam.Clashing = true; beam.realSpeed = 0f;
            body.PinTo(Point);
            body.Kick(Mathf.Max(.6f, body.Width * .5f));
            contact = new BeamContact(body.Style, orb.Style, true, Point);
            Open(body.Style, orb.Style);
        }

        OrbDuel(KiOrb a, KiOrb b)
        {
            Orb = a; Other = b;
            Vector2 d = b.Centre - a.Centre;
            axis = d.sqrMagnitude > .01f ? d.normalized : Vector2.right;
            Point = a.Centre + axis * a.Radius;
            a.EnterDuel(axis);
            b.EnterDuel(-axis);
            contact = new BeamContact(a.Style, b.Style, true, Point);
            Open(a.Style, b.Style);
        }

        void Open(BeamStyle first, BeamStyle second)
        {
            SingularityTime.Hitstop(.09f, .06f);
            BeamContact.Burst(Point, axis, first, second, 1.6f);
            DbpuFx.Sound(Point, ImpactFramesModule.SfxClash, 1f);
        }

        public void Join(BeamBehaviour beam)
        {
            if (helpers.Contains(beam) || Beam == null) return;
            helpers.Add(beam);
            beam.Clashing = true; beam.realSpeed = 0f;
            beam.Body.PinTo(Point);
            beam.Body.Kick(Mathf.Max(.6f, beam.Body.Width * .5f));
        }

        double FirstPower()
        {
            if (Beam == null) return Orb.Power;
            double total = BeamPhysics.BeamPower(Beam);
            foreach (var h in helpers) if (h && h.Firing) total += BeamPhysics.BeamPower(h);
            return total;
        }
        double SecondPower() { return Other != null ? Other.Power : Orb.Power; }

        public static void StepAll(float dt)
        {
            step.Clear(); step.AddRange(All);
            foreach (var d in step) d.Step(dt);
        }

        public static void EndAll()
        {
            step.Clear(); step.AddRange(All);
            foreach (var d in step) d.Close(null, null);
        }

        void Step(float dt)
        {
            if (RealityRift.Active) return;
            Age += dt;
            // Either side vanishing by itself ends it.
            if (Beam != null)
            {
                if (!Beam || !Beam.Firing || !Beam.Body || !Beam.Body.Live) { Close(null, Orb); return; }
                if (!Orb.Exists) { Close(Beam, null); return; }
            }
            else
            {
                if (!Orb.Exists) { Close(null, Other); return; }
                if (!Other.Exists) { Close(null, Orb); return; }
            }
            double first = FirstPower(), second = SecondPower();
            float share = (float)(first / Math.Max(1e-9d, first + second));
            Balance = share * 2f - 1f;
            // The swings grow until something gives, so even an equal contest ends.
            float tide = (Mathf.PerlinNoise(Age * .9f, seed) - .5f) * .62f * (1f + Age * .22f);
            surgeClock -= dt;
            if (surgeClock <= 0f)
            {
                surgeClock = Random.Range(1.1f, 2.5f);
                bool firstSide = Random.value < .5f + Balance * .35f;
                surge += axis * (firstSide ? 1f : -1f) * Random.Range(6f, 12f);
                BeamContact.Shock(Point, firstSide ? FirstStyle : SecondStyle, 4f + strain * 6f, .45f);
                KiSpaceRefraction.Pulse(Point, 2.6f + strain * 2.6f, .45f + strain * .3f, .5f, axis);
                DbpuFx.Sound(Point, "Assets/SFX/Techniques/KiFist/HitMedium.wav", .55f);
                if (Beam != null) Beam.Body.Kick(Mathf.Max(.45f, Beam.Body.Width * .4f));
            }
            surge = Vector2.MoveTowards(surge, Vector2.zero, dt * 26f);
            Vector2 target = axis * (Balance + tide) * MaxSpeed * Mathf.Max(.2f, DBPU.Beam_SpeedMultiplier) + surge;
            velocity = Vector2.Lerp(velocity, target, 1f - Mathf.Exp(-dt * 5.5f));
            Point += velocity * dt;
            Score += (Balance + tide) * dt * .55f;
            strain = Mathf.Clamp01(.42f + Age / 7f + Mathf.Abs(Balance) * .2f);

            if (Beam != null)
            {
                Beam.Body.PinTo(Point); Beam.Body.Strain = strain;
                foreach (var h in helpers) if (h && h.Body && h.Firing) { h.Body.PinTo(Point); h.Body.Strain = strain; }
                Orb.Press(Point + axis * Orb.Radius * .97f, -axis, strain);
            }
            else
            {
                Orb.Press(Point - axis * Orb.Radius * .97f, axis, strain);
                Other.Press(Point + axis * Other.Radius * .97f, -axis, strain);
            }
            float size = Mathf.Clamp(Mathf.Max(Orb.Radius * .6f, Beam != null && Beam.Body ? Beam.Body.Width : 1f), .8f, 7f);
            contact.Step(dt, Point, axis, strain, size);
            BeamScene.Request(Point, axis, FirstStyle.Mid, SecondStyle.Mid, .45f + strain * .55f);
            groundClock -= dt;
            if (groundClock <= 0f)
            {
                groundClock = Mathf.Lerp(.5f, .16f, strain);
                Vector2 ground, n;
                if (BeamGround.Find(Point, size + 5f + strain * 5f, null, out ground, out n))
                {
                    BeamGround.Tear(ground + new Vector2(Random.Range(-size, size), 0f), Random.insideUnitCircle, strain, FirstStyle.Mid);
                    if (Random.value < .32f + strain * .4f) BeamSlabs.Raise(ground + new Vector2(Random.Range(-size * 1.6f, size * 1.6f), -.02f), Random.Range(.4f, 1.2f) * (.5f + strain), Random.Range(-30f, 30f), Color.Lerp(FirstStyle.Mid, SecondStyle.Mid, Random.value));
                }
            }
            if (Beam != null)
            {
                // Driven all the way back into the hands that fired the beam.
                float gap = Vector2.Distance(Point, Beam.Body.SpanStart);
                if (Score <= -1f || gap < 2.5f) { Close(null, Orb); return; }
                if (Score >= 1f) { Close(Beam, null); return; }
            }
            else
            {
                if (Score >= 1f) { Close(null, Orb); return; }
                if (Score <= -1f) { Close(null, Other); return; }
            }
        }

        BeamStyle FirstStyle { get { return Beam != null && Beam && Beam.Body ? Beam.Body.Style : Orb.Style; } }
        BeamStyle SecondStyle { get { return Other != null ? Other.Style : Orb.Style; } }

        void Close(BeamBehaviour beamWon, KiOrb orbWon)
        {
            All.Remove(this);
            bool closing = beamWon == null && orbWon == null;
            contact.End(closing ? 0f : 2.4f);
            if (!closing)
            {
                SingularityTime.Hitstop(.12f, .05f);
                DbpuFx.Sound(Point, ImpactFramesModule.SfxClash, 1f);
                Vector2 ground, n;
                if (BeamGround.Find(Point, 9f, null, out ground, out n)) BeamGround.Erupt(ground, 1.8f, FirstStyle.Mid, n);
            }
            if (Beam != null)
            {
                var all = new List<BeamBehaviour>(helpers) { Beam };
                foreach (var b in all)
                {
                    if (!b || !b.Body) continue;
                    b.Body.Unpin(); b.Body.Strain = 0f; b.Clashing = false;
                    if (orbWon != null || closing && !b.Firing) { if (orbWon != null) b.Firing = false; }
                    else { b.TimeHolding = 0f; b.FinalHoldingTime = b.HoldingTime; if (!b.SbcBlockedThisFire) b.realSpeed = b.OriginalSpeed; }
                }
                if (beamWon != null && Orb.Exists)
                {
                    // Blown back the way it came, into whoever threw it.
                    var by = Beam.GetComponent<PersonBehaviour>();
                    Orb.LeaveDuel(axis * Mathf.Max(Orb.LastSpeed * 1.5f, 50f), by, true);
                    if (Orb.PreviousOwner) DbpuWorldLabel.Show(Orb.PreviousOwner, "OVERPOWERED", "", 1.3f);
                }
                else if (Orb.Exists) Orb.LeaveDuel(-axis * Mathf.Max(Orb.LastSpeed, 30f), null, false);
                return;
            }
            KiOrb loser = orbWon == Orb ? Other : orbWon == Other ? Orb : null;
            if (loser != null && loser.Exists) loser.Crush(Point, axis);
            if (Orb.Exists && Orb != loser) Orb.LeaveDuel(axis * Mathf.Max(Orb.LastSpeed, 30f), null, false);
            if (Other.Exists && Other != loser) Other.LeaveDuel(-axis * Mathf.Max(Other.LastSpeed, 30f), null, false);
        }
    }
}
