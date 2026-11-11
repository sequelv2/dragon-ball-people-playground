using System;
using UnityEngine;

namespace DBPU
{
    public enum AiStrikePhase { Idle, Spacing, Chamber, Release, Contact, FollowThrough, Retract, Recovery }
    public enum AiStrikeResult { None, Hit, Stopped, Ignored, Whiff, Aborted }

    public struct AiStrikeRecord
    {
        public int Slot; public bool Kick, Flying, InWindow, Touched, Parry;
        // Commanded: asked of the limb. Peak: best centre-of-mass speed of the stroke. AtContact: the speed
        // going into the step that made contact. GateSpeed: what PunchPower/KickPower read after that step.
        public float Commanded, Peak, AtContact, GateSpeed, DamagePercent, StopAt, JointLoad, JointStress, At, Duration;
        public AiStrikeResult Result;
    }

    public sealed class AiStrike
    {
        public int Slot; public AiStrikePhase Phase; public float Clock, PhaseTime;
        public PersonBehaviour Target; public Rigidbody2D AimBody; public float AimRadius;
        public float Speed, ChamberTime, Through, Oblique, Recovery; public Vector2 Noise, Line; public bool Parry;
        public float Peak, LastSpeed, AtContact, GateSpeed, DamagePercent, StopAt, JointLoad, JointStress, LungeDone, Safety;
        public bool InWindow, Touched, Resolved, Flying, FreedShoulder, Released; public int HoldSteps; public MeleeOutcome Outcome;
    }

    // ---------------------------------------------------------------------------------
    // Kinetic strike engine.
    //
    // PunchPower and KickPower gate on the world speed of the striking limb's centre of mass
    // (5 m/s, full damage at 25), read after the physics step that made contact. So a strike
    // has to move the whole forearm or shin fast and still be moving once the contact is
    // solved. Three things do that here:
    //   1. a velocity servo: impulses m (v_wanted - v) on the forearm and the kinematically
    //      matching ones on the upper arm, from the two-link Jacobian solved by damped least
    //      squares, with the joint motors given the same speeds so the muscles help;
    //   2. the body behind it: every other limb gets one shared velocity step towards the
    //      target, so the contact meets the fighter's mass and not just an arm's;
    //   3. a line that runs through and slightly across the target, so the part of the speed
    //      that lies along the surface survives the contact.
    // ---------------------------------------------------------------------------------
    public sealed class KineticStrikeEngine
    {
        public const float Gate = 5f, MinSpeed = 9f, MaxSpeed = 28f;
        readonly KinematicsRig rig; readonly SingularityMind mind;
        public readonly AiStrike[] Strikes = new AiStrike[KinematicsRig.ChainCount];
        public readonly AiStrikeRecord[] Records = new AiStrikeRecord[256];
        public int RecordCount { get; private set; }

        // Self-calibration (section 5.6): actuation gain, extra speed against an opponent whose hits get
        // ignored, how far across the target the line runs, how much of the speed the body carries.
        public float Gain = 1f, SpeedBias, Oblique = 28f, LungeShare = .4f;
        public int StoppedStreak;
        public float HitRate = .5f, StopRate, WhiffRate;
        // Share of the commanded speed the limb still has when PunchPower/KickPower read it.
        public float GateRatio = .7f;
        int opponentId;

        public bool Releasing { get; private set; }
        public Vector2 ReleaseAxis { get; private set; }
        public float SoftUntil { get; private set; }

        public KineticStrikeEngine(KinematicsRig rig, SingularityMind mind)
        {
            this.rig = rig; this.mind = mind;
            for (int i = 0; i < Strikes.Length; i++) Strikes[i] = new AiStrike { Slot = i };
        }

        public bool Busy(int slot) { return Strikes[slot].Phase != AiStrikePhase.Idle; }

        public bool AnyBusy
        {
            get { for (int i = 0; i < Strikes.Length; i++) if (Strikes[i].Phase != AiStrikePhase.Idle) return true; return false; }
        }

        public bool Begin(int slot, PersonBehaviour target, Rigidbody2D aimBody, float aimRadius, float speed, float chamber, bool parry)
        {
            var s = Strikes[slot];
            if (s.Phase != AiStrikePhase.Idle || !rig.ChainUsable(slot) || !target || !aimBody) return false;
            int id = target.GetInstanceID();
            if (id != opponentId) { opponentId = id; SpeedBias = 0f; StoppedStreak = 0; }
            var chain = rig.Chains[slot];
            s.Phase = AiStrikePhase.Spacing; s.Clock = s.PhaseTime = 0f;
            s.Target = target; s.AimBody = aimBody; s.AimRadius = Mathf.Max(.04f, aimRadius); s.Parry = parry;
            // The calibration may ask for more, never past what the difficulty allows.
            s.Speed = Mathf.Clamp(speed + SpeedBias, MinSpeed, Mathf.Min(MaxSpeed, mind.Profile.MaxStrikeSpeed));
            s.ChamberTime = chamber;
            s.Through = Mathf.Lerp(.25f, .45f, Mathf.InverseLerp(MinSpeed, MaxSpeed, s.Speed)) * Mathf.Clamp(chain.Reach / .9f, .6f, 6f);
            // Rising or falling across the target, never straight down the surface normal.
            s.Oblique = (UnityEngine.Random.value < (chain.IsLeg ? .8f : .5f) ? 1f : -1f) * Oblique * (parry ? .4f : 1f);
            s.Noise = UnityEngine.Random.insideUnitCircle * mind.Profile.StrikeNoise * Mathf.Clamp(chain.Reach / .9f, .6f, 6f);
            s.Peak = s.LastSpeed = s.AtContact = s.GateSpeed = s.DamagePercent = s.StopAt = s.JointLoad = s.JointStress = s.LungeDone = 0f;
            s.Safety = 1f; s.Recovery = 0f; s.HoldSteps = 0;
            s.InWindow = s.Touched = s.Resolved = s.FreedShoulder = s.Released = false;
            s.Flying = mind.Flying; s.Outcome = MeleeOutcome.Ignored;
            return true;
        }

        public void Abort(int slot)
        {
            var s = Strikes[slot];
            if (s.Phase != AiStrikePhase.Idle) Finish(s, s.Released ? Outcome(s) : AiStrikeResult.Aborted);
        }

        public void AbortAll() { for (int i = 0; i < Strikes.Length; i++) Abort(i); }

        public void Step(float dt)
        {
            Releasing = false;
            for (int i = 0; i < Strikes.Length; i++)
                if (Strikes[i].Phase != AiStrikePhase.Idle) StepStrike(Strikes[i], dt);
        }

        void Enter(AiStrike s, AiStrikePhase phase) { s.Phase = phase; s.PhaseTime = 0f; }

        void StepStrike(AiStrike s, float dt)
        {
            var chain = rig.Chains[s.Slot];
            var upper = rig.Parts[chain.Upper]; var lower = rig.Parts[chain.Lower];
            // The limb was severed, broken or frozen, the target is gone, or the fighter cannot act.
            if (!rig.ChainUsable(s.Slot) || !s.AimBody || !s.Target || !mind.CanAct) { Finish(s, s.Released ? Outcome(s) : AiStrikeResult.Aborted); return; }
            s.Clock += dt; s.PhaseTime += dt;
            Vector2 shoulder = rig.Anchor(upper), com = lower.Body.worldCenterOfMass;
            float reach = chain.Reach;

            switch (s.Phase)
            {
                case AiStrikePhase.Spacing:
                {
                    float stroke = chain.StrikeReach * .6f / Mathf.Max(6f, s.Speed * .7f);
                    float ahead = s.ChamberTime + stroke;
                    Vector2 aim = mind.PredictPoint(s.AimBody, ahead) + s.Noise;
                    Vector2 shoulderThen = shoulder + rig.ComVelocity * ahead;
                    float distance = Vector2.Distance(aim, shoulderThen) - s.AimRadius;
                    float lunge = s.Parry ? 0f : Mathf.Clamp(LungeShare * s.Speed * stroke, .03f, .5f * reach);
                    bool inside = distance >= .55f * reach + lunge * .5f && distance <= .95f * reach + lunge;
                    // Close enough to be worth throwing even from too near, after a short wait for better spacing.
                    bool crowded = distance < .55f * reach + lunge * .5f && distance >= .3f * reach && s.PhaseTime > .2f;
                    if (inside || crowded || (s.Parry && distance <= 1.1f * reach)) { s.InWindow = inside; Enter(s, AiStrikePhase.Chamber); }
                    else if (s.PhaseTime > (s.Parry ? .12f : .9f)) Finish(s, AiStrikeResult.Aborted);
                    break;
                }
                case AiStrikePhase.Chamber:
                {
                    Vector2 aim = mind.PredictPoint(s.AimBody, Mathf.Max(0f, s.ChamberTime - s.PhaseTime) + .06f) + s.Noise;
                    Vector2 to = aim - shoulder;
                    if (to.sqrMagnitude < 1e-6f) to = Vector2.right * rig.Facing;
                    Vector2 dirTo = to.normalized;
                    s.Line = DbpuFx.Rotate(dirTo, s.Oblique * Mathf.Deg2Rad);
                    // Wound up behind the contact point along the line the limb is about to travel.
                    Vector2 contact = aim - dirTo * s.AimRadius;
                    Vector2 start = contact - s.Line * (reach * .85f) - shoulder;
                    float length = Mathf.Clamp(start.magnitude, .32f * reach, .58f * reach);
                    start = start.sqrMagnitude > 1e-6f ? start.normalized * length : -dirTo * length;
                    if (rig.Reach(s.Slot, shoulder + start, 1.5f, true) && !upper.Joint.useLimits) s.FreedShoulder = true;
                    if (s.PhaseTime >= s.ChamberTime)
                    {
                        s.Released = true;
                        if (s.Flying) rig.ExemptFromBrake(.4f);
                        Enter(s, AiStrikePhase.Release);
                    }
                    break;
                }
                case AiStrikePhase.Release:
                case AiStrikePhase.Contact:
                {
                    Releasing = true;
                    Drive(s, chain, upper, lower, shoulder, com, dt);
                    if (s.Phase == AiStrikePhase.Contact)
                    {
                        if (--s.HoldSteps <= 0) Enter(s, AiStrikePhase.FollowThrough);
                    }
                    else if (s.PhaseTime > .3f) { s.Recovery = Mathf.Lerp(.8f, .4f, mind.Tier01); Enter(s, AiStrikePhase.FollowThrough); }
                    break;
                }
                case AiStrikePhase.FollowThrough:
                    // The limb rides out on its own momentum; the pose keeps it near extension.
                    rig.Reach(s.Slot, com + s.Line * (reach * .5f), 0f, true);
                    if (s.PhaseTime >= .05f) Enter(s, AiStrikePhase.Retract);
                    break;
                case AiStrikePhase.Retract:
                    rig.Reach(s.Slot, mind.GuardPoint(s.Slot), 1f, false);
                    if (s.PhaseTime >= .14f)
                    {
                        if (s.Recovery <= 0f) s.Recovery = s.Touched ? Mathf.Lerp(.2f, .08f, mind.Tier01) : Mathf.Lerp(.8f, .4f, mind.Tier01);
                        if (!s.Touched) SoftUntil = Mathf.Max(SoftUntil, Time.time + s.Recovery);
                        Enter(s, AiStrikePhase.Recovery);
                    }
                    break;
                case AiStrikePhase.Recovery:
                    // A whiff leaves the arm slack for a moment: the opening the opponent can punish.
                    rig.Reach(s.Slot, mind.GuardPoint(s.Slot), s.Touched ? 0f : -.6f, false);
                    if (s.PhaseTime >= s.Recovery) Finish(s, Outcome(s));
                    break;
            }
        }

        // One servo step of the release (section 5.2).
        void Drive(AiStrike s, AiChain chain, AiPart upper, AiPart lower, Vector2 shoulder, Vector2 com, float dt)
        {
            Vector2 v2 = lower.Body.velocity;
            float remaining = Mathf.Max(0f, Vector2.Distance(com, s.AimBody.worldCenterOfMass) - s.AimRadius);
            float lead = Mathf.Clamp(remaining / Mathf.Max(s.LastSpeed, s.Speed * .5f), dt, .25f);
            Vector2 aim = mind.PredictPoint(s.AimBody, lead) + s.Noise;
            Vector2 to = aim - shoulder;
            Vector2 dirTo = to.sqrMagnitude > 1e-6f ? to.normalized : s.Line;
            // (a) aim through the target, so the limb is at full speed at contact rather than at full extension
            Vector2 through = aim - dirTo * s.AimRadius + s.Line * s.Through;
            Vector2 want = through - com;
            float wantDistance = want.magnitude;
            Vector2 direction = wantDistance > 1e-4f ? want / wantDistance : s.Line;
            ReleaseAxis = direction;

            // Joint safety: the hinge reaction against its break force, and the game's own stress counter.
            float load = Mathf.Max(JointLoad(upper, dt), JointLoad(lower, dt));
            float stress = Mathf.Max(StressShare(upper.Limb), StressShare(lower.Limb));
            if (load > s.JointLoad) s.JointLoad = load;
            if (stress > s.JointStress) s.JointStress = stress;
            if (load > .45f) s.Safety = Mathf.Max(.25f, s.Safety * .6f);
            if (stress > .35f) { s.Recovery = .5f; Enter(s, AiStrikePhase.FollowThrough); return; }

            // (c) the body behind the strike: everything but the striking chain steps towards the target together
            var torso = rig.Torso;
            Vector2 shoulderVelocity = upper.ParentBody.GetPointVelocity(shoulder);
            int steps = Mathf.RoundToInt(Mathf.Lerp(5f, 2f, mind.Tier01));
            if (!s.Parry && torso)
            {
                float wantClosing = Mathf.Min(9f, LungeShare * s.Speed);
                float closing = Vector2.Dot(torso.velocity - s.AimBody.velocity, dirTo);
                float add = Mathf.Clamp(wantClosing - closing, 0f, wantClosing / steps) * s.Safety;
                if (add > .05f && s.LungeDone < wantClosing * 1.25f)
                {
                    rig.Kick(dirTo * add, upper.Body, lower.Body);
                    s.LungeDone += add; shoulderVelocity += dirTo * add;
                }
            }

            // (b) two-link Jacobian of the forearm's centre of mass about the shoulder, damped least squares
            float q1 = rig.Pointing(upper) * Mathf.Deg2Rad, q2 = rig.Pointing(lower) * Mathf.Deg2Rad;
            float s1 = Mathf.Sin(q1), c1 = Mathf.Cos(q1), s2 = Mathf.Sin(q2), c2 = Mathf.Cos(q2);
            float j11 = -chain.L1 * s1, j12 = -chain.Lc2 * s2, j21 = chain.L1 * c1, j22 = chain.Lc2 * c2;
            Vector2 wanted = direction * s.Speed;
            Vector2 x = wanted - shoulderVelocity;
            float lambda = .12f * (chain.L1 + chain.Lc2), l2 = lambda * lambda;
            float a11 = j11 * j11 + j12 * j12 + l2, a12 = j11 * j21 + j12 * j22, a22 = j21 * j21 + j22 * j22 + l2;
            float det = a11 * a22 - a12 * a12;
            float qd1 = 0f, qd2 = 0f;
            if (Mathf.Abs(det) > 1e-9f)
            {
                float y1 = (a22 * x.x - a12 * x.y) / det, y2 = (a11 * x.y - a12 * x.x) / det;
                qd1 = Mathf.Clamp(j11 * y1 + j21 * y2, -80f, 80f);
                qd2 = Mathf.Clamp(j12 * y1 + j22 * y2, -80f, 80f);
            }

            float cap = Gain * s.Safety * s.Speed / steps;
            Vector2 dv2 = Vector2.ClampMagnitude(wanted - v2, cap);
            lower.Body.AddForce(dv2 * lower.Body.mass, ForceMode2D.Impulse);
            Vector2 v1Wanted = shoulderVelocity + new Vector2(-s1, c1) * (chain.Lc1 * qd1);
            Vector2 dv1 = Vector2.ClampMagnitude(v1Wanted - upper.Body.velocity, cap) * .8f;
            upper.Body.AddForce(dv1 * upper.Body.mass, ForceMode2D.Impulse);
            float w1 = qd1 * Mathf.Rad2Deg, w2 = qd2 * Mathf.Rad2Deg, turn = 1500f * s.Safety * Gain;
            upper.Body.angularVelocity = Mathf.MoveTowards(upper.Body.angularVelocity, w1, turn);
            lower.Body.angularVelocity = Mathf.MoveTowards(lower.Body.angularVelocity, w2, turn);

            // The muscles are told the same thing: the motors run at the joint speeds the servo wants, and
            // the pose points at the extended limb, so whichever of the two is applied last agrees.
            Motor(lower, w2 - w1);
            Motor(upper, w1 - upper.ParentBody.angularVelocity);
            if (rig.Reach(s.Slot, through, 2f, true) && !upper.Joint.useLimits) s.FreedShoulder = true;
            if (!s.Parry) CounterArm(s, dirTo);

            float speed = (v2 + dv2).magnitude;
            s.LastSpeed = speed;
            if (speed > s.Peak) s.Peak = speed;
            if (s.Phase != AiStrikePhase.Release) return;
            // Past the aim point or out of arm: a whiff.
            bool passed = Vector2.Dot(com - through, s.Line) > 0f || wantDistance < .03f;
            bool extended = Vector2.Distance(com, shoulder) >= .985f * chain.StrikeReach && s.PhaseTime > 2f * dt;
            if (passed || extended) { s.Recovery = Mathf.Lerp(.8f, .4f, mind.Tier01); Enter(s, AiStrikePhase.FollowThrough); }
        }

        static void Motor(AiPart part, float relativeDegreesPerSecond)
        {
            if (!part.Joint) return;
            var motor = part.Joint.motor;
            motor.motorSpeed = Mathf.Clamp(part.Sign * relativeDegreesPerSecond, -3000f, 3000f);
            part.Joint.motor = motor;
            part.Joint.useMotor = true;
        }

        // The other arm swings back as this one goes out, as a body does to keep its balance.
        void CounterArm(AiStrike s, Vector2 dirTo)
        {
            if (rig.Chains[s.Slot].IsLeg) return;
            int other = s.Slot == KinematicsRig.ArmFront ? KinematicsRig.ArmBack : KinematicsRig.ArmFront;
            if (Strikes[other].Phase != AiStrikePhase.Idle || !rig.ChainUsable(other)) return;
            var chain = rig.Chains[other];
            Vector2 root = rig.Anchor(rig.Parts[chain.Upper]);
            rig.Reach(other, root - dirTo * (chain.Reach * .45f) + Vector2.down * (chain.Reach * .35f), .5f, false);
        }

        static float JointLoad(AiPart part, float dt)
        {
            if (!part.Joint) return 0f;
            float limit = part.Joint.breakForce;
            if (float.IsInfinity(limit) || limit <= 0f) return 0f;
            return part.Joint.GetReactionForce(dt).magnitude / limit;
        }

        static float StressShare(LimbBehaviour limb)
        {
            if (!limb || !limb.PhysicalBehaviour || !limb.PhysicalBehaviour.rigidbody) return 0f;
            float ratio = limb.PhysicalBehaviour.rigidbody.mass / Mathf.Max(1e-4f, limb.PhysicalBehaviour.TrueInitialMass);
            float limit = limb.BreakingThreshold * ratio;
            return limit > 1e-4f ? limb.JointStress / limit : 0f;
        }

        // ---------------------------------------------------------------------------- events
        int SlotOf(LimbBehaviour limb)
        {
            if (!limb) return -1;
            for (int i = 0; i < rig.Chains.Length; i++)
            {
                var chain = rig.Chains[i];
                if (rig.Parts[chain.Lower].Limb == limb || (chain.End >= 0 && rig.Parts[chain.End].Limb == limb)) return i;
            }
            return -1;
        }

        bool Live(AiStrike s) { return s.Phase == AiStrikePhase.Release || s.Phase == AiStrikePhase.Contact || s.Phase == AiStrikePhase.FollowThrough; }

        // True while this limb is the business end of a strike in flight.
        public bool IsStriking(LimbBehaviour limb)
        {
            int slot = SlotOf(limb);
            return slot >= 0 && Live(Strikes[slot]);
        }

        void Touch(AiStrike s)
        {
            if (!s.Touched) { s.Touched = true; s.AtContact = s.LastSpeed; }
            if (s.Phase == AiStrikePhase.Release) { s.HoldSteps = 2; Enter(s, AiStrikePhase.Contact); }
        }

        // What PunchPower or KickPower made of a contact by one of this fighter's striking limbs.
        public void OnMelee(MeleeHitInfo info)
        {
            int slot = SlotOf(info.Attacker);
            if (slot < 0) return;
            var s = Strikes[slot];
            if (!Live(s) || !info.Victim || info.Victim.Person != s.Target) return;
            Touch(s);
            if (!s.Resolved || info.Outcome > s.Outcome)
            {
                s.Resolved = true; s.Outcome = info.Outcome; s.GateSpeed = info.Speed; s.DamagePercent = info.DamagePercent; s.StopAt = info.StopAt;
            }
            // A stopped hit has the game hold the limb still for a quarter of a second: do not fight it.
            if (info.Outcome == MeleeOutcome.Stopped && DBPU.PhysAtkStopLimbs) { s.Recovery = .3f; Enter(s, AiStrikePhase.Retract); }
        }

        public void OnImpact(LimbBehaviour mine, PersonBehaviour other)
        {
            int slot = SlotOf(mine);
            if (slot < 0) return;
            var s = Strikes[slot];
            if (Live(s) && other == s.Target) Touch(s);
        }

        AiStrikeResult Outcome(AiStrike s)
        {
            if (s.Resolved) return s.Outcome == MeleeOutcome.Damaged ? AiStrikeResult.Hit : s.Outcome == MeleeOutcome.Stopped ? AiStrikeResult.Stopped : AiStrikeResult.Ignored;
            return s.Touched ? AiStrikeResult.Ignored : AiStrikeResult.Whiff;
        }

        void Finish(AiStrike s, AiStrikeResult result)
        {
            var chain = rig.Chains[s.Slot];
            if (s.FreedShoulder) rig.RestoreLimits(chain.Upper);
            var record = new AiStrikeRecord
            {
                Slot = s.Slot, Kick = chain.IsLeg, Flying = s.Flying, InWindow = s.InWindow, Touched = s.Touched, Parry = s.Parry,
                Commanded = s.Speed, Peak = s.Peak, AtContact = s.AtContact, GateSpeed = s.GateSpeed, DamagePercent = s.DamagePercent,
                StopAt = s.StopAt, JointLoad = s.JointLoad, JointStress = s.JointStress, At = Time.time, Duration = s.Clock, Result = result
            };
            Records[RecordCount % Records.Length] = record;
            RecordCount++;
            s.Phase = AiStrikePhase.Idle; s.Target = null; s.AimBody = null; s.FreedShoulder = false;
            if (result != AiStrikeResult.Aborted) Calibrate(ref record);
            mind.OnStrikeFinished(ref record);
        }

        // Section 5.6.
        void Calibrate(ref AiStrikeRecord r)
        {
            if (r.Peak < .85f * r.Commanded) Gain = Mathf.Min(2.2f, Gain * 1.1f);
            else if (r.Peak > 1.12f * r.Commanded) Gain = Mathf.Max(.6f, Gain * .94f);
            if (r.JointLoad > .45f || r.JointStress > .2f) { Gain = Mathf.Max(.6f, Gain * .85f); LungeShare = Mathf.Max(.25f, LungeShare - .03f); }
            if (r.GateSpeed > 0f && r.Commanded > 0f) GateRatio = Mathf.Clamp(Mathf.Lerp(GateRatio, r.GateSpeed / r.Commanded, .2f), .2f, 1.1f);
            const float a = .15f;
            HitRate = Mathf.Lerp(HitRate, r.Result == AiStrikeResult.Hit ? 1f : 0f, a);
            StopRate = Mathf.Lerp(StopRate, r.Result == AiStrikeResult.Stopped ? 1f : 0f, a);
            WhiffRate = Mathf.Lerp(WhiffRate, r.Result == AiStrikeResult.Whiff ? 1f : 0f, a);
            switch (r.Result)
            {
                case AiStrikeResult.Ignored:
                    // Touched but too slow after the contact was solved: more speed, more weight, a flatter line.
                    SpeedBias = Mathf.Min(8f, SpeedBias + 1.5f); Oblique = Mathf.Min(55f, Oblique + 4f); LungeShare = Mathf.Min(.6f, LungeShare + .03f);
                    break;
                case AiStrikeResult.Stopped:
                    StoppedStreak++;
                    SpeedBias = Mathf.Min(8f, SpeedBias + 1f);
                    break;
                case AiStrikeResult.Hit:
                    StoppedStreak = 0;
                    SpeedBias = Mathf.Max(0f, SpeedBias - .25f); Oblique = Mathf.Lerp(Oblique, 28f, .05f);
                    break;
            }
        }
    }
}
