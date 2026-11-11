using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Poses a fighter by world-space limb angle through the joint motors the game already uses, so
    // the solver stays implicit and stable however fast the pose changes. The game writes its own
    // pose into the motors earlier in the same FixedUpdate; this runs last and overrides only the
    // limbs it has a target for, leaving the rest to the normal animation.
    [DefaultExecutionOrder(31000)]
    public sealed class BeamRig : MonoBehaviour
    {
        sealed class Part
        {
            public LimbBehaviour Limb; public Rigidbody2D Body, Parent; public HingeJoint2D Joint; public Part Above;
            public bool HasTarget, LimitsOff, SavedLimits; public float Target, Gain = 16f, Torque = 1f, Sign = -1f, Natural, Written = -1f;
        }
        public PersonBehaviour Person { get; private set; }
        readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        Rigidbody2D core; Vector2 steerVelocity; float steerAccel, torsoTarget, torsoGain = 12f, torsoAccel = 9000f; bool steering, hover, torsoActive, rigidPose;

        public static BeamRig For(PersonBehaviour person)
        {
            if (!person) return null;
            var rig = person.GetComponent<BeamRig>();
            if (!rig) { rig = person.gameObject.AddComponent<BeamRig>(); rig.Bind(person); }
            return rig;
        }

        void Bind(PersonBehaviour person)
        {
            Person = person;
            foreach (var limb in person.Limbs)
            {
                if (!limb) continue;
                var body = limb.GetComponent<Rigidbody2D>();
                if (!body) continue;
                parts[limb.name] = new Part { Limb = limb, Body = body, Joint = limb.Joint, Parent = limb.Joint ? limb.Joint.connectedBody : null };
                if (limb.name == "MiddleBody") core = body;
            }
            Part upper;
            if (!core && parts.TryGetValue("UpperBody", out upper)) core = upper.Body;
            foreach (var part in parts.Values)
                foreach (var other in parts.Values)
                    if (part.Parent && other.Body == part.Parent) part.Above = other;
        }

        // Rotation at which a limb hanging along -Y points in direction d.
        public static float PointAlong(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f; }

        public void Aim(string limb, Vector2 direction, float gain = 16f, float torque = 1f) { AimAt(limb, PointAlong(direction), gain, torque); }

        public void AimAt(string limb, float rotation, float gain = 16f, float torque = 1f)
        {
            Part part;
            if (!parts.TryGetValue(limb, out part)) return;
            part.HasTarget = true; part.Target = rotation; part.Gain = gain; part.Torque = torque;
        }

        // A shoulder can swing past its anatomical stop, so a blow from behind can still be met.
        public void FreeShoulder(string limb)
        {
            Part part;
            if (!parts.TryGetValue(limb, out part) || !part.Joint || part.LimitsOff) return;
            part.SavedLimits = part.Joint.useLimits; part.LimitsOff = true; part.Joint.useLimits = false;
        }

        public void ReleaseAll()
        {
            foreach (var part in parts.Values)
            {
                part.HasTarget = false;
                if (part.LimitsOff) { part.LimitsOff = false; if (part.Joint) part.Joint.useLimits = part.SavedLimits; }
            }
            torsoActive = false; steering = false; hover = false; rigidPose = false;
        }

        // Turns the fighter round on the spot: the same mirror the game applies to spawn someone flipped.
        public bool Face(float direction)
        {
            var root = Person.transform;
            if (root.localScale.x < 0f == direction < 0f) return false;
            float pivot = Core.x;
            foreach (var part in parts.Values)
            {
                if (!part.Body) continue;
                part.Body.velocity = new Vector2(-part.Body.velocity.x, part.Body.velocity.y);
                part.Body.angularVelocity = -part.Body.angularVelocity;
                // As HingeJointLimitAutofixBehaviour does for someone spawned mirrored: a joint's range flips with them.
                if (part.Joint)
                {
                    var limits = part.Joint.limits;
                    float min = limits.min;
                    limits.min = -limits.max; limits.max = -min;
                    part.Joint.limits = limits;
                }
                if (part.Limb) { Vector2 original = part.Limb.OriginalJointLimits; part.Limb.OriginalJointLimits = new Vector2(-original.y, -original.x); }
            }
            root.localScale = new Vector3(-root.localScale.x, root.localScale.y, root.localScale.z);
            // Limbs mirrored about the root; shift it so the fighter turns about their own centre.
            root.position += new Vector3(2f * (pivot - root.position.x), 0f, 0f);
            foreach (var fixedJoint in root.GetComponentsInChildren<FixedJoint2D>())
            {
                var connected = fixedJoint.connectedBody;
                fixedJoint.connectedBody = null; fixedJoint.connectedBody = connected;
            }
            Physics2D.SyncTransforms();
            return true;
        }

        // rigid: the pose is held as one piece while the body turns into it, for an effort that has to look
        // locked. Otherwise limbs keep their world angles and a shove can actually swing the fighter.
        public void TorsoAngle(float rotation, float gain = 12f, float acceleration = 9000f, bool rigid = true) { torsoTarget = rotation; torsoActive = true; torsoGain = gain; torsoAccel = acceleration; rigidPose = rigid; }
        public void Steer(Vector2 velocity, float acceleration) { steerVelocity = velocity; steerAccel = acceleration; steering = true; }
        public void Hover(bool on) { hover = on; }

        public LimbBehaviour Limb(string limb) { Part p; return parts.TryGetValue(limb, out p) ? p.Limb : null; }
        public float RotationOf(string limb) { Part p; return parts.TryGetValue(limb, out p) && p.Body ? p.Body.rotation : 0f; }
        public Vector2 Core { get { return core ? core.position : (Vector2)transform.position; } }
        public Vector2 CoreVelocity { get { return core ? core.velocity : Vector2.zero; } }
        public float CoreRotation { get { return core ? core.rotation : 0f; } }

        public bool Usable(string limb)
        {
            Part p;
            return parts.TryGetValue(limb, out p) && p.Limb && !p.Limb.IsDismembered && p.Limb.Health > 0f && !p.Limb.Broken && p.Joint && p.Joint.enabled && p.Body && p.Body.bodyType == RigidbodyType2D.Dynamic;
        }

        void FixedUpdate()
        {
            if (!Person || !Person.IsAlive() || DbpuFx.Paused) return;
            float dt = Time.fixedDeltaTime;
            Vector2 change = Vector2.zero;
            if (steering && core)
            {
                Vector2 v = core.velocity;
                change = Vector2.MoveTowards(v, steerVelocity, steerAccel * dt) - v;
            }
            foreach (var part in parts.Values)
            {
                var body = part.Body;
                if (!body || body.bodyType != RigidbodyType2D.Dynamic || !part.Limb || part.Limb.IsDismembered) continue;
                if (hover) body.velocity += -Physics2D.gravity * (body.gravityScale * dt);
                if (steering) body.velocity += change;
                if (part.HasTarget && part.Joint && part.Joint.enabled && part.Parent) Drive(part);
            }
            if (torsoActive && core && core.bodyType == RigidbodyType2D.Dynamic)
            {
                float error = Mathf.DeltaAngle(core.rotation, torsoTarget);
                float turn = Mathf.MoveTowards(core.angularVelocity, Mathf.Clamp(error * torsoGain, -900f, 900f), torsoAccel * dt) - core.angularVelocity;
                // Turned as one rigid body: spinning the torso alone just fights every joint on the way out.
                foreach (var part in parts.Values)
                {
                    var body = part.Body;
                    if (!body || body.bodyType != RigidbodyType2D.Dynamic || !part.Limb || part.Limb.IsDismembered) continue;
                    Vector2 r = body.position - core.position;
                    body.angularVelocity += turn;
                    body.velocity += new Vector2(-r.y, r.x) * (turn * Mathf.Deg2Rad);
                }
            }
        }

        void Drive(Part part)
        {
            var joint = part.Joint;
            float rel = Mathf.DeltaAngle(part.Parent.rotation, part.Body.rotation);
            float angle = joint.jointAngle;
            // jointAngle and relative rotation can run opposite ways; read the sign off the live joint.
            if (Mathf.Abs(rel) > 8f && Mathf.Abs(Mathf.DeltaAngle(angle, -rel)) > Mathf.Abs(Mathf.DeltaAngle(angle, rel)) + 4f) part.Sign = 1f;
            else if (Mathf.Abs(rel) > 8f && Mathf.Abs(Mathf.DeltaAngle(angle, rel)) > Mathf.Abs(Mathf.DeltaAngle(angle, -rel)) + 4f) part.Sign = -1f;
            // Measured from where the parent is meant to be, not where it happens to be, so joints do not chase
            // a torso that is still turning.
            float above = !rigidPose ? part.Parent.rotation : part.Above != null && part.Above.HasTarget ? part.Above.Target : part.Parent == core && torsoActive ? torsoTarget : part.Parent.rotation;
            float error = Mathf.DeltaAngle(rel, Mathf.DeltaAngle(above, part.Target));
            var motor = joint.motor;
            motor.motorSpeed = Mathf.Clamp(error * part.Sign * part.Gain, -2400f, 2400f);
            // The game normally rewrites the torque first; never compound on our own last value.
            if (!Mathf.Approximately(motor.maxMotorTorque, part.Written)) part.Natural = motor.maxMotorTorque;
            part.Written = Mathf.Max(part.Natural, part.Limb.BaseStrength * 2f) * part.Torque;
            motor.maxMotorTorque = part.Written;
            joint.motor = motor;
            joint.useMotor = true;
        }

        void OnDestroy() { ReleaseAll(); }
    }

    // Keeps a fighter planted while something shoves them: floats them just above their stance so
    // the steering decides the motion rather than foot friction, and rips up whatever is underfoot.
    public sealed class BeamStance
    {
        const float FootClearance = .16f;
        public readonly BeamRig Rig;
        public bool Grounded { get; private set; }
        public Vector2 GroundPoint { get; private set; }
        readonly Transform root; readonly float standing; float scan, footGap;
        BeamFurrow furrowA, furrowB; float slabA, slabB;
        Vector2 velocity;

        public BeamStance(BeamRig rig)
        {
            Rig = rig;
            root = rig.Person.transform.root;
            var foot = rig.Limb("FootFront") ?? rig.Limb("Foot");
            standing = foot ? Mathf.Clamp(Vector2.Distance(rig.Core, foot.transform.position), .8f, 6f) : 1.4f;
            Scan();
        }

        void Scan()
        {
            Vector2 point, normal;
            Grounded = BeamGround.Find(Rig.Core, standing + .75f, root, out point, out normal);
            if (Grounded) GroundPoint = point;
            footGap = float.MaxValue;
            MeasureFoot("FootFront");
            MeasureFoot("Foot");
        }

        void MeasureFoot(string name)
        {
            var foot = Rig.Limb(name);
            Vector2 point, normal;
            if (!foot || foot.IsDismembered || !BeamGround.Find(foot.transform.position, standing + 1f, root, out point, out normal)) return;
            footGap = Mathf.Min(footGap, foot.transform.position.y - point.y);
        }

        // velocity: where the whole body should be going. tear: how hard the feet are being dragged.
        public void Drive(Vector2 wanted, float acceleration, float dt, float tear, Color glow)
        {
            scan -= dt;
            if (scan <= 0f) { scan = .04f; Scan(); }
            Rig.Hover(true);
            if (Grounded && wanted.y < 7f)
            {
                // Hold the lowest foot just clear of the ground: pressing it in would lever the whole body over.
                float error = footGap == float.MaxValue ? 0f : FootClearance - footGap;
                wanted.y = Mathf.Clamp(error * 10f, -16f, 16f);
            }
            Rig.Steer(wanted, acceleration);
            velocity = wanted;
            if (tear <= 0f || !Grounded) return;
            TearFoot("FootFront", ref furrowA, ref slabA, tear, glow, dt);
            TearFoot("Foot", ref furrowB, ref slabB, tear, glow, dt);
        }

        void TearFoot(string name, ref BeamFurrow furrow, ref float slab, float tear, Color glow, float dt)
        {
            var foot = Rig.Limb(name);
            if (!foot || foot.IsDismembered) return;
            Vector2 at, normal;
            if (!BeamGround.Find(foot.transform.position, 1.1f, root, out at, out normal)) return;
            float speed = Mathf.Abs(velocity.x);
            if (speed < .35f) return;
            if (!furrow || furrow.Full) furrow = BeamFurrow.Begin(glow);
            if (furrow && Vector2.Distance(furrow.LastPoint, at) > .22f) furrow.Add(at);
            float s = Mathf.Clamp01(speed / 12f) * tear;
            if (Random.value < dt * (14f + 60f * s)) BeamGround.Tear(at, velocity, s, glow);
            slab -= speed * dt;
            if (slab <= 0f)
            {
                slab = Random.Range(.5f, 1.25f);
                float side = Mathf.Sign(velocity.x);
                BeamSlabs.Raise(at + new Vector2(side * Random.Range(.12f, .5f), -.02f), Random.Range(.25f, .7f) * (.5f + s * 1.3f), side * Random.Range(4f, 34f), glow);
            }
        }
    }
}
