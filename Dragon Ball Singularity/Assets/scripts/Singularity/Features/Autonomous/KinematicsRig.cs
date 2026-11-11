using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    public static class AiLimb
    {
        public const int Head = 0, UpperBody = 1, MiddleBody = 2, LowerBody = 3, UpperArmFront = 4, LowerArmFront = 5,
            UpperArm = 6, LowerArm = 7, UpperLegFront = 8, LowerLegFront = 9, FootFront = 10, UpperLeg = 11, LowerLeg = 12,
            Foot = 13, Count = 14;

        public static readonly string[] Names =
        {
            "Head", "UpperBody", "MiddleBody", "LowerBody", "UpperArmFront", "LowerArmFront", "UpperArm", "LowerArm",
            "UpperLegFront", "LowerLegFront", "FootFront", "UpperLeg", "LowerLeg", "Foot"
        };
    }

    // ---------------------------------------------------------------------------------
    // Restore ledger. Every value the AI changes on a fighter is written here with what it
    // was before, and put back on every way out: strike end, timeout, AI off, module off,
    // death, a destroyed limb, a form change. Restoring twice is harmless.
    // ---------------------------------------------------------------------------------
    public sealed class AiRestoreLedger
    {
        public enum Kind
        {
            PoseOverride, PunchActive, KickActive, JointLimits, UiDodge, FlightState, BrakeExempt, Shield, PowerState,
            FighterLoadout, HandLoadout, KiCharge, Barrage, Beam, KiFist
        }

        struct Entry
        {
            public Kind What; public UnityEngine.Object Target; public bool Flag; public int Number; public string Text; public object Extra;
        }

        readonly List<Entry> entries = new List<Entry>(64);
        public int Count { get { return entries.Count; } }

        public bool Has(Kind what, UnityEngine.Object target, string text = null)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].What == what && ReferenceEquals(entries[i].Target, target) && (text == null || entries[i].Text == text)) return true;
            return false;
        }

        // Only the first value seen for a target is kept: that is the one from before the AI.
        public void Record(Kind what, UnityEngine.Object target, bool flag = false, int number = 0, string text = null, object extra = null)
        {
            if (ReferenceEquals(target, null) || Has(what, target, what == Kind.PowerState ? text : null)) return;
            entries.Add(new Entry { What = what, Target = target, Flag = flag, Number = number, Text = text, Extra = extra });
        }

        // Puts one entry back early, for example a shoulder freed for a single strike.
        public void Restore(Kind what, UnityEngine.Object target)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].What != what || !ReferenceEquals(entries[i].Target, target)) continue;
                var e = entries[i];
                entries.RemoveAt(i);
                Apply(e);
            }
        }

        public void RestoreKind(Kind what)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].What != what) continue;
                var e = entries[i];
                entries.RemoveAt(i);
                Apply(e);
            }
        }

        public void RestoreAll()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                entries.RemoveAt(i);
                Apply(e);
            }
            entries.Clear();
        }

        static void Apply(Entry e)
        {
            // A target destroyed meanwhile (severed limb, deleted fighter) has nothing left to restore.
            if (!e.Target) return;
            try
            {
                switch (e.What)
                {
                    case Kind.PoseOverride:
                    {
                        var person = (PersonBehaviour)e.Target; var pose = e.Extra as RagdollPose;
                        if (person.Poses != null && pose != null) person.Poses.Remove(pose);
                        int count = person.Poses != null ? person.Poses.Count : 0;
                        person.OverridePoseIndex = e.Number >= 0 && e.Number < count ? e.Number : -1;
                        RagdollPose rest;
                        if (ReferenceEquals(person.ActivePose, pose) && person.LinkedPoses != null && person.LinkedPoses.TryGetValue(PoseState.Rest, out rest)) person.SetPose(rest);
                        break;
                    }
                    case Kind.PunchActive: ((PunchPower)e.Target).Active = e.Flag; break;
                    case Kind.KickActive: ((KickPower)e.Target).Active = e.Flag; break;
                    case Kind.JointLimits: ((HingeJoint2D)e.Target).useLimits = e.Flag; break;
                    case Kind.UiDodge: ((SingularityUltraInstinct)e.Target).SuppressedByAI = e.Flag; break;
                    case Kind.FlightState: { var flight = (Flight)e.Target; if (flight.IsFlying != e.Flag) flight.Toggle(); break; }
                    case Kind.BrakeExempt: Flight.ExemptFromBrake((Rigidbody2D)e.Target, 0f); break;
                    case Kind.Shield: { var shield = (KiShield)e.Target; if (shield.Using && !e.Flag) shield.Lower(false); break; }
                    case Kind.PowerState:
                    {
                        var powers = (CharacterPowers)e.Target; CharacterPowers.PowerStats stats;
                        if (e.Text == null || !powers.PowerData.TryGetValue(e.Text, out stats) || stats == null) break;
                        bool now = stats.IsActive != null ? stats.IsActive() : stats.Active;
                        if (now == e.Flag) break;
                        if (e.Flag) { if (stats.Apply != null) stats.Apply(); }
                        else if (stats.Disable != null) stats.Disable();
                        powers.HarvestPowerStates();
                        break;
                    }
                    case Kind.FighterLoadout: ((KiFighterLoadout)e.Target).NativeSelectedId = e.Text; break;
                    case Kind.HandLoadout: ((KiHandLoadout)e.Target).SelectedId = e.Text; break;
                    case Kind.KiCharge: ((KiChargeModeController)e.Target).SetHoldDesired(false); break;
                    case Kind.Barrage: ((Barrage)e.Target).StopFromAI(); break;
                    case Kind.Beam: { var beam = (BeamBehaviour)e.Target; if (beam.Charging || beam.Firing) beam.CancelBeamState(); break; }
                    case Kind.KiFist: { var fist = (KiFistHand)e.Target; fist.Cancel(); fist.Equipped = e.Flag; break; }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI restore " + e.What + ": " + ex.Message); }
        }
    }

    public sealed class AiPart
    {
        public int Index, Parent = -1;
        public LimbBehaviour Limb; public Transform Tf; public Rigidbody2D Body, ParentBody; public HingeJoint2D Joint;
        public float Length, ComDistance, RotOffset;      // joint to far end, joint to centre of mass, body rotation minus pointing angle
        public float Sign = -1f; public int SignVotes = -3; // jointAngle runs with (+1) or against (-1) the relative body rotation
        public bool HasTarget; public float TargetDir, Rigidity;
        public RagdollPose.LimbPose Pose; public bool InPose; public float RestAngle;
        public bool Usable;
    }

    // Two segments that end in a striking limb: an arm, or a thigh and shin.
    public sealed class AiChain
    {
        public int Upper, Lower, End = -1; public bool IsLeg; public float Bend = 1f;
        public float L1, L2, Lc1, Lc2;
        public float Reach { get { return L1 + L2; } }
        public float StrikeReach { get { return L1 + Lc2; } }
    }

    // ---------------------------------------------------------------------------------
    // Kinematics rig. Maps the 14 standard limbs by name, measures them live, solves the
    // two-bone IK and writes the result into a pose of its own that the game's joint motors
    // follow (PersonBehaviour.Poses / OverridePoseIndex). Holding, guarding and aiming are
    // therefore done by the fighter's real muscles, which weaken with injury like anyone's.
    // Movement is force on every body in proportion to its mass, so joints carry no load.
    // ---------------------------------------------------------------------------------
    public sealed class KinematicsRig
    {
        public const int ArmFront = 0, ArmBack = 1, LegFront = 2, LegBack = 3, ChainCount = 4;

        public readonly PersonBehaviour Person;
        public readonly AiRestoreLedger Ledger;
        public readonly AiPart[] Parts = new AiPart[AiLimb.Count];
        public readonly AiChain[] Chains = new AiChain[ChainCount];
        readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>(24);
        readonly List<LimbBehaviour> bodyLimbs = new List<LimbBehaviour>(24);

        RagdollPose aiPose; float baseRigidity = 5f; int limbCount; Vector3 builtScale; bool layoutCustom;
        PersonLimbLayout layout;
        public Vector2 Com { get; private set; }
        public Vector2 ComVelocity { get; private set; }
        public float Mass { get; private set; }
        public float Height { get; private set; }
        public bool Built { get; private set; }
        public float Facing { get { return Person && Person.transform.localScale.x < 0f ? -1f : 1f; } }
        public RagdollPose Pose { get { return aiPose; } }
        public bool PoseHeld { get { return aiPose != null && Person && Person.Poses != null && Person.OverridePoseIndex >= 0 && Person.OverridePoseIndex < Person.Poses.Count && ReferenceEquals(Person.Poses[Person.OverridePoseIndex], aiPose); } }

        public KinematicsRig(PersonBehaviour person, AiRestoreLedger ledger)
        {
            Person = person; Ledger = ledger;
            for (int i = 0; i < Parts.Length; i++) Parts[i] = new AiPart { Index = i };
            Chains[ArmFront] = new AiChain { Upper = AiLimb.UpperArmFront, Lower = AiLimb.LowerArmFront };
            Chains[ArmBack] = new AiChain { Upper = AiLimb.UpperArm, Lower = AiLimb.LowerArm };
            Chains[LegFront] = new AiChain { Upper = AiLimb.UpperLegFront, Lower = AiLimb.LowerLegFront, End = AiLimb.FootFront, IsLeg = true };
            Chains[LegBack] = new AiChain { Upper = AiLimb.UpperLeg, Lower = AiLimb.LowerLeg, End = AiLimb.Foot, IsLeg = true };
        }

        // ------------------------------------------------------------------------- building
        public static bool LimbUsable(LimbBehaviour limb)
        {
            if (!limb || limb.IsDismembered || limb.Health <= 0f) return false;
            var phys = limb.PhysicalBehaviour;
            if (!phys || phys.isDisintegrated || !phys.rigidbody || phys.rigidbody.bodyType != RigidbodyType2D.Dynamic) return false;
            return !limb.NodeBehaviour || limb.NodeBehaviour.IsConnectedToRoot;
        }

        // Called on AI start, after a form change, a severed or regrown limb, a resize.
        public void Build()
        {
            Built = false;
            if (!Person || Person.Limbs == null) return;
            limbCount = Person.Limbs.Length;
            builtScale = Person.transform.localScale;
            layout = Person.GetComponent<PersonLimbLayout>();
            layoutCustom = layout && layout.CustomLayoutActive;
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                p.Limb = null; p.Tf = null; p.Body = null; p.ParentBody = null; p.Joint = null; p.Parent = -1; p.Usable = false; p.HasTarget = false;
            }
            bodies.Clear(); bodyLimbs.Clear();
            for (int i = 0; i < Person.Limbs.Length; i++)
            {
                var limb = Person.Limbs[i];
                if (!limb) continue;
                // Tail segments and other extra limbs ride along but are never driven.
                bool extra = limb.GetComponent<CellMaxTailSegment>();
                if (LimbUsable(limb)) { bodies.Add(limb.PhysicalBehaviour.rigidbody); bodyLimbs.Add(limb); }
                if (extra) continue;
                int index = Array.IndexOf(AiLimb.Names, limb.name);
                if (index < 0 || Parts[index].Limb) continue;
                var part = Parts[index];
                part.Limb = limb; part.Tf = limb.transform;
                part.Body = limb.PhysicalBehaviour ? limb.PhysicalBehaviour.rigidbody : limb.GetComponent<Rigidbody2D>();
                part.Joint = limb.HasJoint ? limb.Joint : null;
                part.ParentBody = part.Joint ? part.Joint.connectedBody : null;
            }
            for (int i = 0; i < Parts.Length; i++)
            {
                var part = Parts[i];
                if (!part.Limb) continue;
                for (int j = 0; j < Parts.Length; j++)
                    if (j != i && Parts[j].Body && part.ParentBody == Parts[j].Body) { part.Parent = j; break; }
                part.Usable = PartUsable(part);
            }
            for (int c = 0; c < Chains.Length; c++) Measure(Chains[c]);
            MeasureSingle(Parts[AiLimb.Head]);
            MeasureSingle(Parts[AiLimb.UpperBody]); MeasureSingle(Parts[AiLimb.MiddleBody]); MeasureSingle(Parts[AiLimb.LowerBody]);
            var head = Parts[AiLimb.Head]; var foot = Parts[AiLimb.FootFront].Limb ? Parts[AiLimb.FootFront] : Parts[AiLimb.Foot];
            Height = head.Body && foot.Body ? Mathf.Clamp(Vector2.Distance(head.Body.position, foot.Body.position), .6f, 40f) : 1.7f * Mathf.Abs(builtScale.y);
            BuildPose();
            UpdateMass();
            Built = Parts[AiLimb.UpperBody].Body || Parts[AiLimb.MiddleBody].Body;
        }

        static bool PartUsable(AiPart part)
        {
            return LimbUsable(part.Limb) && !part.Limb.Broken && part.Body && (!part.Limb.HasJoint || (part.Joint && part.Joint.enabled && part.ParentBody));
        }

        public Vector2 Anchor(AiPart part)
        {
            if (part.Joint && part.Tf) return part.Tf.TransformPoint(part.Joint.anchor);
            return part.Body ? part.Body.worldCenterOfMass : Vector2.zero;
        }

        static float AngleOf(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
        public static Vector2 Dir(float degrees) { float r = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        // The direction a segment points, from its joint towards its far end.
        public float Pointing(AiPart part) { return part.Body ? part.Body.rotation - part.RotOffset : 0f; }

        void MeasureSingle(AiPart part)
        {
            if (!part.Body) return;
            Vector2 a = Anchor(part), com = part.Body.worldCenterOfMass;
            Vector2 d = com - a;
            part.ComDistance = d.magnitude; part.Length = part.ComDistance * 2f;
            // A segment without a joint (the root) is taken to point down its own -Y, like every limb sprite.
            part.RotOffset = d.sqrMagnitude > 1e-6f ? Mathf.DeltaAngle(AngleOf(d), part.Body.rotation) : 90f;
        }

        void Measure(AiChain chain)
        {
            var upper = Parts[chain.Upper]; var lower = Parts[chain.Lower];
            chain.L1 = chain.L2 = chain.Lc1 = chain.Lc2 = 0f;
            if (!upper.Body || !lower.Body) { MeasureSingle(upper); MeasureSingle(lower); return; }
            Vector2 shoulder = Anchor(upper), elbow = Anchor(lower);
            Vector2 d1 = elbow - shoulder;
            upper.Length = d1.magnitude; upper.ComDistance = Vector2.Distance(shoulder, upper.Body.worldCenterOfMass);
            upper.RotOffset = d1.sqrMagnitude > 1e-6f ? Mathf.DeltaAngle(AngleOf(d1), upper.Body.rotation) : 90f;
            Vector2 d2;
            var end = chain.End >= 0 ? Parts[chain.End] : null;
            if (end != null && end.Body && end.Joint) d2 = Anchor(end) - elbow;
            else d2 = (lower.Body.worldCenterOfMass - elbow) * 2f;
            lower.Length = d2.magnitude; lower.ComDistance = Vector2.Distance(elbow, lower.Body.worldCenterOfMass);
            lower.RotOffset = d2.sqrMagnitude > 1e-6f ? Mathf.DeltaAngle(AngleOf(d2), lower.Body.rotation) : 90f;
            if (end != null && end.Body) MeasureSingle(end);
            chain.L1 = upper.Length; chain.L2 = lower.Length; chain.Lc1 = upper.ComDistance; chain.Lc2 = lower.ComDistance;
        }

        // The AI's own pose starts as a copy of the fighter's rest pose, so untouched limbs stand as usual.
        void BuildPose()
        {
            RagdollPose rest = null;
            if (Person.LinkedPoses != null) Person.LinkedPoses.TryGetValue(PoseState.Rest, out rest);
            if (rest == null && Person.Poses != null && Person.Poses.Count > 0) rest = Person.Poses[0];
            if (aiPose == null)
            {
                aiPose = new RagdollPose { Name = "DBPU Singularity Mind", State = PoseState.Rest, Angles = new List<RagdollPose.LimbPose>(), AngleDictionary = new Dictionary<LimbBehaviour, RagdollPose.LimbPose>() };
            }
            aiPose.Angles.Clear(); aiPose.AngleDictionary.Clear();
            baseRigidity = rest != null ? Mathf.Max(1f, rest.Rigidity) : 5f;
            aiPose.Rigidity = baseRigidity * 3f;
            aiPose.ForceMultiplier = rest != null ? rest.ForceMultiplier : 1f;
            aiPose.ShouldStandUpright = true; aiPose.ShouldStumble = false;
            aiPose.UprightForceMultiplier = rest != null ? rest.UprightForceMultiplier : 1f;
            aiPose.DragInfluence = rest != null ? rest.DragInfluence : 1f;
            aiPose.AnimationSpeedMultiplier = 1f;
            for (int i = 0; i < Parts.Length; i++) Parts[i].InPose = false;
            if (rest != null && rest.Angles != null)
            {
                for (int i = 0; i < rest.Angles.Count; i++)
                {
                    var source = rest.Angles[i];
                    if (!source.Limb || aiPose.AngleDictionary.ContainsKey(source.Limb)) continue;
                    var copy = source;
                    copy.Angle = source.Animated ? source.StartAngle : source.Angle;
                    copy.Animated = false; copy.PoseRigidityModifier = 0f;
                    aiPose.Angles.Add(copy);
                    aiPose.AngleDictionary.Add(copy.Limb, copy);
                    for (int j = 0; j < Parts.Length; j++)
                        if (Parts[j].Limb == copy.Limb) { Parts[j].Pose = copy; Parts[j].InPose = true; Parts[j].RestAngle = copy.Angle; }
                }
            }
            // A limb the rest pose never listed still needs an entry before it can be aimed.
            for (int j = 0; j < Parts.Length; j++)
            {
                var part = Parts[j];
                if (part.InPose || !part.Limb || !part.Joint || aiPose.AngleDictionary.ContainsKey(part.Limb)) continue;
                var made = new RagdollPose.LimbPose(part.Limb, (part.Joint.jointAngle - part.Joint.referenceAngle) * Person.AngleOffset);
                aiPose.Angles.Add(made); aiPose.AngleDictionary.Add(part.Limb, made);
                part.Pose = made; part.InPose = true; part.RestAngle = made.Angle;
            }
            // The game caches per limb whether the active pose lists it; keep that true to the rebuilt pose.
            if (ReferenceEquals(Person.ActivePose, aiPose))
                for (int i = 0; i < Person.Limbs.Length; i++)
                    if (Person.Limbs[i]) Person.Limbs[i].IsActiveInCurrentPose = aiPose.AngleDictionary.ContainsKey(Person.Limbs[i]);
        }

        // True when the body is no longer the one that was measured.
        public bool Stale()
        {
            if (!Person || Person.Limbs == null) return true;
            if (Person.Limbs.Length != limbCount) return true;
            Vector3 s = Person.transform.localScale;
            if (Mathf.Abs(Mathf.Abs(s.x) - Mathf.Abs(builtScale.x)) > .01f * Mathf.Abs(builtScale.x) || Mathf.Abs(s.y - builtScale.y) > .01f * Mathf.Abs(builtScale.y)) return true;
            if ((layout && layout.CustomLayoutActive) != layoutCustom) return true;
            for (int i = 0; i < Parts.Length; i++)
            {
                var part = Parts[i];
                if (ReferenceEquals(part.Limb, null)) continue;
                if (!part.Limb) return true;                                            // destroyed
                if (part.Limb.HasJoint && !ReferenceEquals(part.Limb.Joint, part.Joint)) return true;   // rebuilt joint (regeneration)
                if (PartUsable(part) != part.Usable) return true;                        // severed, broken, healed
            }
            return false;
        }

        // ------------------------------------------------------------------- pose ownership
        public void TakePose()
        {
            if (aiPose == null || !Person || Person.Poses == null) return;
            if (!Person.Poses.Contains(aiPose))
            {
                Ledger.Record(AiRestoreLedger.Kind.PoseOverride, Person, false, Person.OverridePoseIndex, null, aiPose);
                Person.Poses.Add(aiPose);
            }
            int index = Person.Poses.IndexOf(aiPose);
            if (Person.OverridePoseIndex != index) Person.OverridePoseIndex = index;
        }

        public void ReleasePose() { Ledger.Restore(AiRestoreLedger.Kind.PoseOverride, Person); }

        // ------------------------------------------------------------------------ per step
        public void BeginStep()
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                var part = Parts[i];
                part.HasTarget = false; part.Rigidity = 0f;
                if (!part.Usable || !part.Joint || !part.ParentBody) continue;
                // Which way this joint counts its angle is read off the live joint, never assumed.
                float relative = part.Body.angularVelocity - part.ParentBody.angularVelocity, speed = part.Joint.jointSpeed;
                if (Mathf.Abs(relative) > 25f && Mathf.Abs(speed) > 25f)
                {
                    part.SignVotes = Mathf.Clamp(part.SignVotes + (relative * speed > 0f ? 1 : -1), -12, 12);
                    part.Sign = part.SignVotes > 0 ? 1f : -1f;
                }
            }
            if (aiPose != null) { aiPose.Rigidity = baseRigidity * 3f; }
            UpdateMass();
        }

        public void UpdateMass()
        {
            float mass = 0f; Vector2 com = Vector2.zero, velocity = Vector2.zero;
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                var body = bodies[i];
                if (!body || !LimbUsable(bodyLimbs[i])) { bodies.RemoveAt(i); bodyLimbs.RemoveAt(i); continue; }
                float m = body.mass;
                mass += m; com += body.worldCenterOfMass * m; velocity += body.velocity * m;
            }
            if (mass > 1e-6f) { Com = com / mass; ComVelocity = velocity / mass; }
            Mass = mass;
        }

        public void Aim(int part, float pointingDegrees, float rigidity = 0f)
        {
            var p = Parts[part];
            if (!p.Usable || !p.InPose) return;
            p.HasTarget = true; p.TargetDir = pointingDegrees; p.Rigidity = rigidity;
        }

        public void AimAt(int part, Vector2 direction, float rigidity = 0f)
        {
            if (direction.sqrMagnitude < 1e-8f) return;
            Aim(part, AngleOf(direction), rigidity);
        }

        // The joint angle that would put this segment on the wanted pointing direction.
        float JointAngleFor(AiPart part, float pointing, out bool insideLimits)
        {
            float relative = Mathf.DeltaAngle(part.ParentBody.rotation, part.Body.rotation);
            float wanted = Mathf.DeltaAngle(part.ParentBody.rotation, pointing + part.RotOffset);
            float joint = part.Joint.jointAngle + part.Sign * Mathf.DeltaAngle(relative, wanted);
            insideLimits = true;
            if (!part.Joint.useLimits) return joint;
            var limits = part.Joint.limits;
            float min = Mathf.Min(limits.min, limits.max), max = Mathf.Max(limits.min, limits.max);
            // The same pointing direction repeats every turn; one of the repeats may lie inside the joint's range.
            if (joint < min && joint + 360f <= max) joint += 360f;
            else if (joint > max && joint - 360f >= min) joint -= 360f;
            if (joint >= min && joint <= max) return joint;
            insideLimits = false;
            return Mathf.Clamp(joint, min + 1f, max - 1f);
        }

        public bool WithinLimits(int part, float pointing)
        {
            var p = Parts[part];
            if (!p.Usable || !p.Joint || !p.ParentBody) return false;
            bool inside; JointAngleFor(p, pointing, out inside);
            return inside;
        }

        // Writes this step's targets into the AI pose. Limbs without a target hold the rest angle.
        public void WritePose(float drag)
        {
            if (aiPose == null) return;
            aiPose.DragInfluence = drag;
            float offset = Person.AngleOffset == 0f ? 1f : Person.AngleOffset;
            for (int i = 0; i < Parts.Length; i++)
            {
                var part = Parts[i];
                if (!part.InPose || !part.Limb) continue;
                float angle = part.RestAngle; float rigidity = 0f;
                if (part.HasTarget && part.Usable && part.Joint && part.ParentBody)
                {
                    bool inside;
                    float joint = JointAngleFor(part, part.TargetDir, out inside);
                    angle = (joint - part.Joint.referenceAngle) * offset;
                    rigidity = part.Rigidity;
                }
                if (part.Pose.Angle == angle && part.Pose.PoseRigidityModifier == rigidity) continue;
                part.Pose.Angle = angle; part.Pose.PoseRigidityModifier = rigidity;
                aiPose.AngleDictionary[part.Limb] = part.Pose;
            }
        }

        // ------------------------------------------------------------------------------- IK
        // Analytic two-bone solution. q1 and q2 are pointing angles in degrees.
        public static void SolveTwoBone(Vector2 root, float l1, float l2, Vector2 target, float bend, out float q1, out float q2)
        {
            Vector2 d = target - root;
            float distance = d.magnitude;
            if (distance < 1e-4f) { d = Vector2.right; distance = 1e-4f; }
            Vector2 unit = d / distance;
            float reach = Mathf.Clamp(distance, Mathf.Abs(l1 - l2) + .01f, l1 + l2 - .004f);
            float cos = Mathf.Clamp((l1 * l1 + reach * reach - l2 * l2) / (2f * l1 * reach), -1f, 1f);
            float a = Mathf.Acos(cos) * Mathf.Rad2Deg;
            q1 = AngleOf(unit) + bend * a;
            Vector2 elbow = root + Dir(q1) * l1, hand = root + unit * reach;
            q2 = AngleOf(hand - elbow);
        }

        // Brings the far end of a chain to a point through the pose. Returns false when the chain cannot act.
        public bool Reach(int chainIndex, Vector2 target, float rigidity = 0f, bool mayFreeShoulder = false)
        {
            var chain = Chains[chainIndex];
            var upper = Parts[chain.Upper]; var lower = Parts[chain.Lower];
            if (!ChainUsable(chainIndex)) return false;
            Vector2 root = Anchor(upper);
            float q1, q2;
            SolveTwoBone(root, chain.L1, chain.L2, target, chain.Bend, out q1, out q2);
            // The elbow or knee only folds one way: take the other solution when this one is out of range.
            if (!ElbowInside(upper, lower, q1, q2))
            {
                float o1, o2;
                SolveTwoBone(root, chain.L1, chain.L2, target, -chain.Bend, out o1, out o2);
                if (ElbowInside(upper, lower, o1, o2)) { q1 = o1; q2 = o2; chain.Bend = -chain.Bend; }
            }
            if (mayFreeShoulder && upper.Joint && upper.Joint.useLimits && !WithinLimits(chain.Upper, q1)) FreeLimits(chain.Upper);
            Aim(chain.Upper, q1, rigidity);
            Aim(chain.Lower, q2, rigidity);
            return true;
        }

        bool ElbowInside(AiPart upper, AiPart lower, float q1, float q2)
        {
            if (!lower.Joint || !lower.Joint.useLimits) return true;
            // Judged as if the upper segment were already on its target.
            float relative = Mathf.DeltaAngle(lower.ParentBody.rotation, lower.Body.rotation);
            float wanted = Mathf.DeltaAngle(q1 + upper.RotOffset, q2 + lower.RotOffset);
            float joint = lower.Joint.jointAngle + lower.Sign * Mathf.DeltaAngle(relative, wanted);
            var limits = lower.Joint.limits;
            float min = Mathf.Min(limits.min, limits.max), max = Mathf.Max(limits.min, limits.max);
            if (joint < min && joint + 360f <= max) joint += 360f;
            else if (joint > max && joint - 360f >= min) joint -= 360f;
            return joint >= min - 2f && joint <= max + 2f;
        }

        // A shoulder or hip may swing past its stop to meet something behind the fighter. The
        // game counts joint stress only while limits are on, so nothing is strained meanwhile.
        public void FreeLimits(int part)
        {
            var p = Parts[part];
            if (!p.Joint || !p.Joint.useLimits) return;
            Ledger.Record(AiRestoreLedger.Kind.JointLimits, p.Joint, true);
            p.Joint.useLimits = false;
        }

        // Limits come back once the joint is inside its range again; switching them on while it is far
        // outside would have the solver snap the limb back. The ledger still restores them unconditionally
        // when the AI lets go of the body.
        public void RestoreLimits(int part)
        {
            var p = Parts[part];
            if (ReferenceEquals(p.Joint, null) || !Ledger.Has(AiRestoreLedger.Kind.JointLimits, p.Joint)) return;
            if (p.Joint)
            {
                var limits = p.Joint.limits; float angle = p.Joint.jointAngle;
                float min = Mathf.Min(limits.min, limits.max), max = Mathf.Max(limits.min, limits.max);
                if (angle < min && angle + 360f <= max) angle += 360f;
                else if (angle > max && angle - 360f >= min) angle -= 360f;
                if (angle < min - 3f || angle > max + 3f) return;
            }
            Ledger.Restore(AiRestoreLedger.Kind.JointLimits, p.Joint);
        }

        public Vector2 Tip(int chainIndex)
        {
            var chain = Chains[chainIndex]; var lower = Parts[chain.Lower];
            if (!lower.Body) return Com;
            return Anchor(lower) + Dir(Pointing(lower)) * chain.L2;
        }

        public bool ChainUsable(int chainIndex)
        {
            var chain = Chains[chainIndex];
            var upper = Parts[chain.Upper]; var lower = Parts[chain.Lower];
            return upper.Usable && lower.Usable && upper.Joint && lower.Joint && upper.ParentBody && lower.ParentBody && chain.L1 > 0f && chain.L2 > 0f;
        }

        public Rigidbody2D Torso
        {
            get
            {
                var upper = Parts[AiLimb.UpperBody];
                if (upper.Usable) return upper.Body;
                var middle = Parts[AiLimb.MiddleBody];
                return middle.Usable ? middle.Body : (bodies.Count > 0 ? bodies[0] : null);
            }
        }

        // --------------------------------------------------------------------------- forces
        // The same acceleration for every body: thrust that loads no joint.
        public void Accelerate(Vector2 acceleration)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body) body.AddForce(acceleration * body.mass, ForceMode2D.Force);
            }
        }

        // One velocity step for every body except the two that are mid-strike.
        public void Kick(Vector2 deltaVelocity, Rigidbody2D skipA = null, Rigidbody2D skipB = null)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body || body == skipA || body == skipB) continue;
                body.AddForce(deltaVelocity * body.mass, ForceMode2D.Impulse);
            }
        }

        // Turns the whole fighter about its centre of mass towards a lean angle, as one piece. The
        // velocity changes add up to a pure torque: no net push, no load on any joint. Used in the air,
        // where the game's own upright force (floor contact only) does nothing.
        public void Attitude(float leanDegrees, float gain, float maxRate, float maxAcceleration, float dt)
        {
            var torso = Torso;
            if (!torso) return;
            var reference = Parts[AiLimb.UpperBody].Usable ? Parts[AiLimb.UpperBody] : Parts[AiLimb.MiddleBody];
            if (!reference.Tf) return;
            float error = Mathf.DeltaAngle(reference.Tf.eulerAngles.z, leanDegrees);
            float wanted = Mathf.Clamp(error * gain, -maxRate, maxRate);
            float turn = Mathf.MoveTowards(torso.angularVelocity, wanted, maxAcceleration * dt) - torso.angularVelocity;
            if (Mathf.Abs(turn) < .01f) return;
            Vector2 pivot = Com; float radians = turn * Mathf.Deg2Rad;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body) continue;
                Vector2 r = body.worldCenterOfMass - pivot;
                body.angularVelocity += turn;
                body.velocity += new Vector2(-r.y, r.x) * radians;
            }
        }

        public void ExemptFromBrake(float seconds)
        {
            float until = Time.time + seconds;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body) continue;
                Ledger.Record(AiRestoreLedger.Kind.BrakeExempt, body);
                Flight.ExemptFromBrake(body, until);
            }
        }

        public int BodyCount { get { return bodies.Count; } }
        public Rigidbody2D BodyAt(int index) { return bodies[index]; }
    }
}
