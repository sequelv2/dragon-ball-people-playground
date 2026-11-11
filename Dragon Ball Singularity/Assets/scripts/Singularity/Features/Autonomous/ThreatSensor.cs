using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Six-state extended Kalman filter [px, py, vx, vy, ax, ay] for one moving thing.
    // Position and velocity are both measured. The motion model knows gravity and a drag
    // term (the flight brake is exactly a linear drag), and lets acceleration die away, so a
    // dodge shows up as acceleration instead of being smoothed out. The two axes never
    // couple in this model, so the 6x6 covariance is two symmetric 3x3 blocks, written out
    // term by term below; the cross terms are exactly zero and are not stored.
    // ---------------------------------------------------------------------------------
    public struct AiEkf
    {
        public float Px, Py, Vx, Vy, Ax, Ay;
        // Per axis: [pp pv pa; pv vv va; pa va aa]
        float xpp, xpv, xpa, xvv, xva, xaa, ypp, ypv, ypa, yvv, yva, yaa;
        float noise;                 // adaptive process noise scale
        public float Drag, Gravity;  // 1/s, and the share of world gravity this thing feels
        public bool Started;
        const float AccelerationMemory = .35f, Jerk = 60f, MeasurePosition = .0004f, MeasureVelocity = .04f;

        public Vector2 Position { get { return new Vector2(Px, Py); } }
        public Vector2 Velocity { get { return new Vector2(Vx, Vy); } }
        public Vector2 Acceleration { get { return new Vector2(Ax, Ay); } }
        public float Manoeuvre { get { return noise; } }

        public void Reset(Vector2 position, Vector2 velocity)
        {
            Px = position.x; Py = position.y; Vx = velocity.x; Vy = velocity.y; Ax = Ay = 0f;
            xpp = ypp = .01f; xvv = yvv = 1f; xaa = yaa = 25f;
            xpv = xpa = xva = ypv = ypa = yva = 0f;
            noise = 1f; Started = true;
        }

        public void Step(float dt, Vector2 position, Vector2 velocity)
        {
            if (!Started) { Reset(position, velocity); return; }
            if (dt <= 0f) return;
            float gravity = Physics2D.gravity.y * Gravity;
            float nisX = Axis(dt, 0f, position.x, velocity.x, ref Px, ref Vx, ref Ax, ref xpp, ref xpv, ref xpa, ref xvv, ref xva, ref xaa);
            float nisY = Axis(dt, gravity, position.y, velocity.y, ref Py, ref Vy, ref Ay, ref ypp, ref ypv, ref ypa, ref yvv, ref yva, ref yaa);
            // A measurement far outside what the filter expected means a manoeuvre: trust the model less for a while.
            float nis = nisX + nisY;
            if (float.IsNaN(nis) || float.IsInfinity(nis)) { Reset(position, velocity); return; }
            noise = nis > 9f ? Mathf.Min(noise * 1.6f, 40f) : Mathf.Max(1f, noise * .92f);
        }

        float Axis(float t, float g, float zp, float zv, ref float p, ref float v, ref float a,
            ref float pp, ref float pv, ref float pa, ref float vv, ref float va, ref float aa)
        {
            // Predict. F = [1 t h; 0 m t; 0 0 f], h = t^2/2, m = 1 - drag t, f = 1 - t/memory.
            float h = .5f * t * t, m = Mathf.Max(0f, 1f - Drag * t), f = Mathf.Max(0f, 1f - t / AccelerationMemory);
            p = p + v * t + a * h;
            v = v * m + (a + g) * t;
            a = a * f;
            float r00 = pp + t * pv + h * pa, r01 = pv + t * vv + h * va, r02 = pa + t * va + h * aa;
            float r11 = m * vv + t * va, r12 = m * va + t * aa;
            float r22 = f * aa;
            float q = Jerk * Jerk * noise, t2 = t * t, t3 = t2 * t;
            pp = r00 + t * r01 + h * r02 + q * t3 * t2 / 20f;
            pv = m * r01 + t * r02 + q * t2 * t2 / 8f;
            pa = f * r02 + q * t3 / 6f;
            vv = m * r11 + t * r12 + q * t3 / 3f;
            va = f * r12 + q * t2 / 2f;
            aa = f * r22 + q * t;

            // Update with z = [position, velocity]. S = H P H' + R, K = P H' S^-1.
            float s00 = pp + MeasurePosition, s01 = pv, s11 = vv + MeasureVelocity;
            float det = s00 * s11 - s01 * s01;
            if (Mathf.Abs(det) < 1e-12f) return 0f;
            float i00 = s11 / det, i01 = -s01 / det, i11 = s00 / det;
            float k00 = pp * i00 + pv * i01, k01 = pp * i01 + pv * i11;
            float k10 = pv * i00 + vv * i01, k11 = pv * i01 + vv * i11;
            float k20 = pa * i00 + va * i01, k21 = pa * i01 + va * i11;
            float y0 = zp - p, y1 = zv - v;
            p += k00 * y0 + k01 * y1;
            v += k10 * y0 + k11 * y1;
            a += k20 * y0 + k21 * y1;
            // P = P - K S K'
            float m00 = k00 * s00 + k01 * s01, m01 = k00 * s01 + k01 * s11;
            float m10 = k10 * s00 + k11 * s01, m11 = k10 * s01 + k11 * s11;
            float m20 = k20 * s00 + k21 * s01, m21 = k20 * s01 + k21 * s11;
            pp -= m00 * k00 + m01 * k01;
            pv -= m00 * k10 + m01 * k11;
            pa -= m00 * k20 + m01 * k21;
            vv -= m10 * k10 + m11 * k11;
            va -= m10 * k20 + m11 * k21;
            aa -= m20 * k20 + m21 * k21;
            if (pp < 1e-7f) pp = 1e-7f;
            if (vv < 1e-6f) vv = 1e-6f;
            if (aa < 1e-4f) aa = 1e-4f;
            return y0 * (i00 * y0 + i01 * y1) + y1 * (i01 * y0 + i11 * y1);
        }

        // Where this will be after `seconds`, by the same motion model.
        public void Predict(float seconds, out Vector2 position, out Vector2 velocity)
        {
            float px = Px, py = Py, vx = Vx, vy = Vy, ax = Ax, ay = Ay;
            float left = Mathf.Clamp(seconds, 0f, 1.5f), g = Physics2D.gravity.y * Gravity;
            while (left > 1e-4f)
            {
                float t = Mathf.Min(left, .04f), h = .5f * t * t, m = Mathf.Max(0f, 1f - Drag * t), f = Mathf.Max(0f, 1f - t / AccelerationMemory);
                px += vx * t + ax * h; py += vy * t + ay * h;
                vx = vx * m + ax * t; vy = vy * m + (ay + g) * t;
                ax *= f; ay *= f;
                left -= t;
            }
            position = new Vector2(px, py); velocity = new Vector2(vx, vy);
        }
    }

    public enum AiTrackKind { None, Person, Blast, Orb, Beam, Prop }

    public sealed class AiTrack
    {
        public AiTrackKind Kind; public int Key;
        public PersonBehaviour Person, Owner; public Rigidbody2D Body; public BlastComponent Blast; public KiOrb Orb; public BeamBody Beam;
        public Flight Flight; public bool KiFighter, Counted;
        public AiEkf Filter;
        public float Radius, FirstSeen, LastSeen, Tti = float.MaxValue, Cpa = float.MaxValue, Hostility, LastHealth = -1f, DamageRate, PeakSpeed;
        public Vector2 Position, Velocity, Heading;

        public void Clear()
        {
            Kind = AiTrackKind.None; Key = 0; Person = Owner = null; Body = null; Blast = null; Orb = null; Beam = null; Flight = null;
            KiFighter = Counted = false;
            Filter = default(AiEkf); Radius = 0f; Tti = Cpa = float.MaxValue; Hostility = 0f; LastHealth = -1f; DamageRate = 0f; PeakSpeed = 0f;
        }
    }

    // The current opponent's body, looked up once when it becomes the target.
    public sealed class AiFoe
    {
        public PersonBehaviour Person; public Rigidbody2D Torso, Head, Middle;
        public float TorsoRadius = .14f, HeadRadius = .12f, Height = 1.7f;
        public readonly Rigidbody2D[] Strikers = new Rigidbody2D[4];
        public readonly GripBehaviour[] Grips = new GripBehaviour[2];
        public KiSystem Ki; public MultipliersAndStuffs Multipliers; public TransformationContextMenu Menu; public KiShield Shield; public BeamBehaviour Beam;
        public Kaioken Kaioken;
        public string CharacterId = ""; public bool KiFighter;

        public void Bind(PersonBehaviour person)
        {
            Person = person; Torso = Head = Middle = null; Ki = null; Multipliers = null; Menu = null; Shield = null; Beam = null; Kaioken = null;
            for (int i = 0; i < Strikers.Length; i++) Strikers[i] = null;
            Grips[0] = Grips[1] = null;
            CharacterId = ""; KiFighter = false;
            if (!person || person.Limbs == null) return;
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (!KinematicsRig.LimbUsable(limb)) continue;
                var body = limb.PhysicalBehaviour.rigidbody;
                float y = body.position.y;
                if (y > top) top = y;
                if (y < bottom) bottom = y;
                switch (limb.name)
                {
                    case "UpperBody": Torso = body; TorsoRadius = Thickness(limb); break;
                    case "MiddleBody": Middle = body; break;
                    case "Head": Head = body; HeadRadius = Thickness(limb); break;
                    case "LowerArmFront": Strikers[0] = body; Grips[0] = limb.GripBehaviour; break;
                    case "LowerArm": Strikers[1] = body; Grips[1] = limb.GripBehaviour; break;
                    case "LowerLegFront": Strikers[2] = body; break;
                    case "LowerLeg": Strikers[3] = body; break;
                }
            }
            if (!Torso) Torso = Middle;
            if (!Torso) { var any = SingularityFighters.Torso(person); Torso = any && any.PhysicalBehaviour ? any.PhysicalBehaviour.rigidbody : null; }
            if (top > bottom) Height = Mathf.Clamp(top - bottom + .3f, .5f, 60f);
            Ki = person.GetComponent<KiSystem>(); KiFighter = Ki;
            Multipliers = person.GetComponent<MultipliersAndStuffs>();
            Shield = person.GetComponent<KiShield>(); Beam = person.GetComponent<BeamBehaviour>(); Kaioken = person.GetComponent<Kaioken>();
            var head = AutonomousMindModule.HeadOf(person);
            Menu = head ? head.GetComponent<TransformationContextMenu>() : null;
            CharacterId = SingularityFighters.CharacterId(person);
        }

        static float Thickness(LimbBehaviour limb)
        {
            var collider = limb.Collider;
            if (!collider) return .14f;
            Vector3 e = collider.bounds.extents;
            return Mathf.Clamp(Mathf.Min(e.x, e.y), .05f, 4f);
        }

        public bool Valid { get { return Person && Torso; } }
        public bool Down { get { return !Person || !Person.IsAlive() || Person.Consciousness < .4f; } }
        public string Form { get { return Menu ? Menu.Form : (Ki ? Ki.CurrentForm : "Base"); } }
    }

    // ---------------------------------------------------------------------------------
    // Threat sensor. Fighters come from the Singularity runner's list; shots, spheres and
    // thrown things from one throttled non-allocating overlap query; beams and big spheres
    // from the lists their own systems keep. Each gets an EKF track, a time to impact and a
    // closest point of approach against this fighter.
    // ---------------------------------------------------------------------------------
    public sealed class ThreatSensor
    {
        public const float Range = 35f;
        const int Capacity = 40;
        struct Seen { public byte Kind; public BlastComponent Blast; public Rigidbody2D Body; }

        readonly KinematicsRig rig; readonly PersonBehaviour self; readonly Transform root;
        public readonly AiTrack[] Tracks = new AiTrack[Capacity];
        readonly Collider2D[] overlap = new Collider2D[128];
        readonly RaycastHit2D[] rays = new RaycastHit2D[8];
        readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>(512);
        ContactFilter2D filter; const int TerrainMask = 1 << 11;
        float scanClock, peopleClock, probeClock;

        public AiTrack TopThreat { get; private set; }
        public float ThreatLevel { get; private set; }
        public float GroundDistance { get; private set; }
        public float WallLeft { get; private set; }
        public float WallRight { get; private set; }
        public float Ceiling { get; private set; }
        public Vector2 GroundPoint { get; private set; }
        public float BodyRadius { get { return Mathf.Clamp(rig.Height * .22f, .25f, 12f); } }

        public ThreatSensor(KinematicsRig rig)
        {
            this.rig = rig; self = rig.Person; root = self.transform.root;
            for (int i = 0; i < Tracks.Length; i++) Tracks[i] = new AiTrack();
            filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = true;
            GroundDistance = WallLeft = WallRight = Ceiling = 30f;
        }

        public void Reset()
        {
            for (int i = 0; i < Tracks.Length; i++) Tracks[i].Clear();
            seen.Clear(); TopThreat = null; ThreatLevel = 0f;
        }

        AiTrack Find(AiTrackKind kind, int key)
        {
            for (int i = 0; i < Tracks.Length; i++) if (Tracks[i].Kind == kind && Tracks[i].Key == key) return Tracks[i];
            return null;
        }

        AiTrack Open(AiTrackKind kind, int key)
        {
            for (int i = 0; i < Tracks.Length; i++)
            {
                if (Tracks[i].Kind != AiTrackKind.None) continue;
                var t = Tracks[i];
                t.Clear(); t.Kind = kind; t.Key = key; t.FirstSeen = t.LastSeen = Time.time;
                return t;
            }
            return null;
        }

        public AiTrack PersonTrack(PersonBehaviour person)
        {
            return person ? Find(AiTrackKind.Person, person.GetInstanceID()) : null;
        }

        public void Step(float dt)
        {
            float now = Time.time;
            peopleClock -= dt; scanClock -= dt; probeClock -= dt;
            if (peopleClock <= 0f) { peopleClock = .25f; ScanPeople(now); }
            if (scanClock <= 0f) { scanClock = .08f; ScanShots(now); ScanBeams(now); ScanOrbs(now); }
            if (probeClock <= 0f) { probeClock = .2f; Probe(); }

            Vector2 me = rig.Com, mine = rig.ComVelocity; float radius = BodyRadius;
            AiTrack top = null; float level = 0f;
            for (int i = 0; i < Tracks.Length; i++)
            {
                var t = Tracks[i];
                if (t.Kind == AiTrackKind.None) continue;
                if (!Measure(t, now)) { t.Clear(); continue; }
                t.Filter.Step(dt, t.Position, t.Velocity);
                float speed = t.Velocity.magnitude;
                if (speed > t.PeakSpeed) t.PeakSpeed = speed; else t.PeakSpeed = Mathf.Max(speed, t.PeakSpeed - dt * 4f);
                if (t.Hostility > 0f) t.Hostility = Mathf.Max(0f, t.Hostility - dt * .02f);
                if (t.Kind == AiTrackKind.Person) { t.Tti = t.Cpa = float.MaxValue; continue; }
                Approach(t, me, mine, radius);
                if (t.Owner == self || t.Tti > 1.5f) continue;
                float danger = Mathf.Clamp01(1f - t.Tti / 1.5f);
                if (danger > level) level = danger;
                if (top == null || t.Tti < top.Tti) top = t;
            }
            TopThreat = top; ThreatLevel = level;
        }

        // Refreshes position and velocity from the thing itself. False once it is gone or out of range.
        bool Measure(AiTrack t, float now)
        {
            switch (t.Kind)
            {
                case AiTrackKind.Person:
                    if (!t.Person) return false;
                    if (!t.Body || !t.Body.gameObject.activeInHierarchy)
                    {
                        var torso = SingularityFighters.Torso(t.Person);
                        t.Body = torso && torso.PhysicalBehaviour ? torso.PhysicalBehaviour.rigidbody : null;
                        if (!t.Body) return false;
                    }
                    t.Position = t.Body.worldCenterOfMass; t.Velocity = t.Body.velocity;
                    bool flying = t.Flight && t.Flight.IsFlying;
                    t.Filter.Drag = flying ? .05f / Mathf.Max(.005f, Time.fixedDeltaTime) : 0f;
                    t.Filter.Gravity = flying || t.Person.IsTouchingFloor ? 0f : 1f;
                    t.LastSeen = now;
                    return (t.Position - rig.Com).sqrMagnitude < (Range + 8f) * (Range + 8f);
                case AiTrackKind.Blast:
                    if (!t.Blast || !t.Body || t.Blast.AlreadyHit) return false;
                    t.Position = t.Body.position; t.Velocity = t.Body.velocity;
                    return now - t.LastSeen < .4f;
                case AiTrackKind.Prop:
                    if (!t.Body) return false;
                    t.Position = t.Body.worldCenterOfMass; t.Velocity = t.Body.velocity;
                    t.Filter.Gravity = t.Body.gravityScale;
                    return now - t.LastSeen < .4f;
                case AiTrackKind.Orb:
                    if (t.Orb == null || t.Orb.Gone || !t.Orb.Exists) return false;
                    t.Position = t.Orb.Centre; t.Velocity = t.Orb.Velocity; t.Radius = t.Orb.Radius;
                    return now - t.LastSeen < .4f;
                case AiTrackKind.Beam:
                    if (!t.Beam || !t.Beam.Live || !t.Beam.Beam || !t.Beam.Beam.Firing) return false;
                    t.Heading = t.Beam.TipTangent;
                    t.Position = t.Beam.Tip; t.Velocity = t.Heading * t.Beam.TipSpeed; t.Radius = Mathf.Max(t.Beam.Width, .2f) * .5f;
                    return now - t.LastSeen < .4f;
            }
            return false;
        }

        // Time to impact and closest approach of a track against this fighter, from the filtered state.
        void Approach(AiTrack t, Vector2 me, Vector2 mine, float radius)
        {
            Vector2 r = t.Filter.Position - me, w = t.Filter.Velocity - mine;
            float reach = radius + t.Radius;
            t.Tti = float.MaxValue; t.Cpa = r.magnitude;
            if (t.Kind == AiTrackKind.Beam)
            {
                // A beam is a corridor from its head along its tangent.
                float along = Vector2.Dot(me - t.Position, t.Heading);
                float off = Mathf.Abs(t.Heading.x * (me.y - t.Position.y) - t.Heading.y * (me.x - t.Position.x));
                t.Cpa = off;
                if (along > -1.5f && off < reach + .35f) t.Tti = Mathf.Max(0f, along) / Mathf.Max(1f, t.Velocity.magnitude);
                return;
            }
            float ww = w.sqrMagnitude;
            if (ww < 1e-4f) return;
            float closest = -Vector2.Dot(r, w) / ww;
            if (closest <= 0f) return;
            t.Cpa = (r + w * closest).magnitude;
            if (t.Cpa > reach + .25f) return;
            // First time the gap closes to the sum of the two radii.
            float b = Vector2.Dot(r, w), c = r.sqrMagnitude - reach * reach, disc = b * b - ww * c;
            t.Tti = disc > 0f && c > 0f ? Mathf.Max(0f, (-b - Mathf.Sqrt(disc)) / ww) : closest;
        }

        void ScanPeople(float now)
        {
            var people = SingularityRunner.Persons;
            float limit = Range * Range; Vector2 me = rig.Com;
            for (int i = 0; i < people.Count; i++)
            {
                var p = people[i];
                if (!p || p == self) continue;
                int key = p.GetInstanceID();
                var track = Find(AiTrackKind.Person, key);
                if (track != null) continue;
                var torso = SingularityFighters.Torso(p);
                var body = torso && torso.PhysicalBehaviour ? torso.PhysicalBehaviour.rigidbody : null;
                if (!body || (body.worldCenterOfMass - me).sqrMagnitude > limit) continue;
                track = Open(AiTrackKind.Person, key);
                if (track == null) return;
                track.Person = p; track.Body = body; track.Radius = .3f;
                track.Flight = p.GetComponentInChildren<Flight>(true); track.KiFighter = p.GetComponent<KiSystem>();
            }
        }

        void ScanShots(float now)
        {
            int count = Physics2D.OverlapCircle(rig.Com, Range, filter, overlap);
            if (seen.Count > 480) seen.Clear();
            for (int i = 0; i < count; i++)
            {
                var collider = overlap[i];
                if (!collider) continue;
                int id = collider.GetInstanceID();
                Seen entry;
                if (!seen.TryGetValue(id, out entry))
                {
                    // Classified once per collider; later scans only look the answer up.
                    entry = new Seen();
                    var blast = collider.GetComponent<BlastComponent>();
                    if (blast) { entry.Kind = 1; entry.Blast = blast; entry.Body = collider.attachedRigidbody; }
                    else
                    {
                        var body = collider.attachedRigidbody;
                        if (body && body.bodyType == RigidbodyType2D.Dynamic && !collider.isTrigger && collider.transform.root != root
                            && !collider.GetComponent<LimbBehaviour>() && collider.gameObject.layer != 11) { entry.Kind = 2; entry.Body = body; }
                    }
                    seen[id] = entry;
                }
                if (entry.Kind == 0) continue;
                if (entry.Kind == 1)
                {
                    if (!entry.Blast || !entry.Body) continue;
                    var track = Find(AiTrackKind.Blast, id) ?? Open(AiTrackKind.Blast, id);
                    if (track == null) continue;
                    if (!track.Blast)
                    {
                        track.Blast = entry.Blast; track.Body = entry.Body; track.Radius = .3f;
                        track.Owner = entry.Blast.User ? entry.Blast.User.GetComponent<PersonBehaviour>() : null;
                    }
                    track.LastSeen = now;
                }
                else
                {
                    if (!entry.Body) continue;
                    // Only something thrown hard is worth a track.
                    if (entry.Body.velocity.sqrMagnitude < 64f) continue;
                    var track = Find(AiTrackKind.Prop, id) ?? Open(AiTrackKind.Prop, id);
                    if (track == null) continue;
                    if (!track.Body) { track.Body = entry.Body; track.Radius = Mathf.Clamp(Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y), .1f, 3f); }
                    track.LastSeen = now;
                }
            }
        }

        void ScanBeams(float now)
        {
            var active = BeamBody.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var body = active[i];
                if (!body || !body.Live || !body.Beam || !body.Beam.Firing) continue;
                if (body.Beam.transform.root == root) continue;
                int key = body.GetInstanceID();
                var track = Find(AiTrackKind.Beam, key) ?? Open(AiTrackKind.Beam, key);
                if (track == null) return;
                if (!track.Beam) { track.Beam = body; track.Owner = body.Beam.GetComponent<PersonBehaviour>(); }
                track.LastSeen = now;
            }
        }

        void ScanOrbs(float now)
        {
            var orbs = SpherePhysics.Orbs;
            for (int i = 0; i < orbs.Count; i++)
            {
                var orb = orbs[i];
                // Small shots already have a track of their own through their collider.
                if (orb == null || orb.Class == KiOrb.Weight.Tiny || !orb.Exists || !orb.Threat || orb.Owner == self) continue;
                AiTrack track = null;
                for (int j = 0; j < Tracks.Length; j++)
                    if (Tracks[j].Kind == AiTrackKind.Orb && ReferenceEquals(Tracks[j].Orb, orb)) { track = Tracks[j]; break; }
                if (track == null) { track = Open(AiTrackKind.Orb, i + 1); if (track == null) return; track.Orb = orb; track.Owner = orb.Owner; }
                track.LastSeen = now;
            }
        }

        float Cast(Vector2 from, Vector2 direction, float distance, out Vector2 point)
        {
            point = from + direction * distance;
            int n = Physics2D.RaycastNonAlloc(from, direction, rays, distance, TerrainMask);
            float best = distance;
            for (int i = 0; i < n; i++)
            {
                var hit = rays[i];
                if (!hit.collider || hit.collider.isTrigger || hit.collider.transform.root == root) continue;
                if (hit.distance < best) { best = hit.distance; point = hit.point; }
            }
            return best;
        }

        void Probe()
        {
            Vector2 me = rig.Com, point, unused;
            GroundDistance = Cast(me, Vector2.down, 30f, out point); GroundPoint = point;
            WallLeft = Cast(me, Vector2.left, 30f, out unused);
            WallRight = Cast(me, Vector2.right, 30f, out unused);
            Ceiling = Cast(me, Vector2.up, 30f, out unused);
        }

        // True when the fighter's body can travel this far that way without meeting the map.
        public bool PathClear(Vector2 direction, float distance)
        {
            Vector2 size = new Vector2(Mathf.Max(.3f, rig.Height * .3f), Mathf.Max(.5f, rig.Height * .8f));
            int n = Physics2D.BoxCastNonAlloc(rig.Com, size, 0f, direction, rays, distance, TerrainMask);
            for (int i = 0; i < n; i++)
            {
                var hit = rays[i];
                if (hit.collider && !hit.collider.isTrigger && hit.collider.transform.root != root) return false;
            }
            return true;
        }

        // The opponent's fastest limb coming at this fighter, if any is close to landing.
        public bool IncomingStrike(AiFoe foe, out Rigidbody2D limb, out float tti)
        {
            limb = null; tti = float.MaxValue;
            if (foe == null || !foe.Valid) return false;
            Vector2 me = rig.Com, mine = rig.ComVelocity; float radius = BodyRadius + .15f;
            for (int i = 0; i < foe.Strikers.Length; i++)
            {
                var body = foe.Strikers[i];
                if (!body) continue;
                Vector2 r = body.worldCenterOfMass - me, w = body.velocity - mine;
                float distance = r.magnitude;
                if (distance > radius + 2.2f || distance < 1e-3f) continue;
                float closing = -Vector2.Dot(w, r / distance);
                if (closing < 6f) continue;
                float t = Mathf.Max(0f, distance - radius) / closing;
                if (t < tti && t < .35f) { tti = t; limb = body; }
            }
            return limb;
        }
    }
}
