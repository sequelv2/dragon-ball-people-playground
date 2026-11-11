using System;
using UnityEngine;

namespace DBPU
{
    // Anything a fighter can meet with their hands: the head of a beam or a flying sphere of ki.
    public abstract class GuardTarget
    {
        public abstract bool Exists { get; }
        public abstract bool Threat { get; }
        // Already pinned by someone else or locked in a clash.
        public abstract bool Interrupted { get; }
        public abstract Vector2 Travel { get; }
        public abstract float Speed { get; }
        public abstract float ContactSize { get; }
        public abstract BeamStyle Style { get; }
        public abstract PersonBehaviour Attacker { get; }
        public abstract KiSystem AttackerKi { get; }
        public abstract float BraceLimit { get; }
        public abstract float RatioFor(PersonBehaviour holder);
        // The direction it presses at a point.
        public abstract Vector2 Thrust(Vector2 at);
        // How far it still has to come before it touches a hand.
        public abstract float GapFrom(Vector2 hand);
        // How far it still has to come before it touches the body itself, for those that can miss the hand.
        public virtual float BodyGap(Vector2 core, float bodyRadius) { return float.MaxValue; }
        // How far its line of travel passes a point, past its own size.
        public abstract float OffLine(Vector2 point);
        public abstract Vector2 StrikePoint(Vector2 hand);
        // Where a fighter should be heading while getting ready.
        public virtual Vector2 Intercept(Vector2 core) { return Vector2.zero; }
        public abstract void SetImmune(PersonBehaviour person, bool on);
        public abstract void Catch(Vector2 hands, Vector2 thrust);
        public abstract void Hold(Vector2 hands, Vector2 thrust, float strain);
        public abstract void Kick(float amount);
        public abstract void Crush(Vector2 hands, Vector2 thrust);
        public abstract void Breakthrough(Vector2 thrust);
        // True when it left under its own power; false when the hands have to walk it back.
        public abstract bool ThrowBack(Vector2 hands, Vector2 thrust, PersonBehaviour holder);
        public abstract void Swat(Vector2 point, float angle, bool ground, PersonBehaviour holder);
        public abstract void Released(KiGuard.Outcome outcome);
    }

    public sealed class BeamTarget : GuardTarget
    {
        public readonly BeamBehaviour Beam; public readonly BeamBody Body;
        public BeamTarget(BeamBehaviour beam) { Beam = beam; Body = beam.Body; }
        public override bool Exists { get { return Beam && Body; } }
        public override bool Threat { get { return Beam.StillActive && Beam.Firing && Body.Live; } }
        public override bool Interrupted { get { return Body.Pinned || Beam.Clashing; } }
        public override Vector2 Travel { get { return Body.TipTangent; } }
        public override float Speed { get { return Body.TipSpeed; } }
        public override float ContactSize { get { return Mathf.Max(Body.Width, .45f); } }
        public override BeamStyle Style { get { return Body.Style; } }
        public override PersonBehaviour Attacker { get { return Beam.GetComponent<PersonBehaviour>(); } }
        public override KiSystem AttackerKi { get { return Beam.KiSys; } }
        public override float BraceLimit { get { return 1.1f; } }
        public override float RatioFor(PersonBehaviour holder) { return BeamPhysics.Ratio(holder, Beam); }
        public override Vector2 Thrust(Vector2 at) { return Body.TipTangent; }
        public override float GapFrom(Vector2 hand) { return Vector2.Dot(hand - Body.Tip, Body.TipTangent); }
        public override float OffLine(Vector2 point) { return Mathf.Abs(BeamPhysics.Cross(Body.TipTangent, point - Body.Tip)) - Mathf.Max(Body.Width, .2f) * .5f; }
        public override Vector2 StrikePoint(Vector2 hand)
        {
            // The blow lands where the hand crosses the line of the beam.
            Vector2 point = Body.Tip + Body.TipTangent * Mathf.Max(0f, Vector2.Dot(hand - Body.Tip, Body.TipTangent));
            return Vector2.Distance(point, hand) > 1.6f ? hand : point;
        }
        public override void SetImmune(PersonBehaviour person, bool on) { if (on) Body.Immune.Add(person); else Body.Immune.Remove(person); }
        public override void Catch(Vector2 hands, Vector2 thrust) { Body.PinTo(hands); Beam.HeldByFighter = true; }
        public override void Hold(Vector2 hands, Vector2 thrust, float strain) { Body.PinTo(hands); Body.Strain = strain; }
        public override void Kick(float amount) { Body.Kick(Mathf.Max(.6f * amount, Body.Width * .6f * amount)); }
        public override void Crush(Vector2 hands, Vector2 thrust)
        {
            Body.Unpin();
            Beam.HeldByFighter = false;
            Beam.realSpeed = 0f;
            Beam.Firing = false;
            var attacker = Attacker;
            if (attacker)
            {
                SingularityFighters.Push(attacker, -thrust * 5f + Vector2.up * .8f);
                DbpuWorldLabel.Show(attacker, "BEAM CRUSHED", "", 1.2f);
            }
        }
        public override void Breakthrough(Vector2 thrust)
        {
            Body.Unpin();
            Beam.HeldByFighter = false;
            Body.Strain = 0f;
            Beam.realSpeed = Mathf.Max(Beam.OriginalSpeed * 2f, 80f);
        }
        public override bool ThrowBack(Vector2 hands, Vector2 thrust, PersonBehaviour holder) { return false; }
        public override void Swat(Vector2 point, float angle, bool ground, PersonBehaviour holder)
        {
            // Flung skyward it keeps curving up; driven down it goes straight into the ground.
            float toUp = Mathf.DeltaAngle(angle, 90f);
            float w = Mathf.Max(Body.Width, .3f);
            BeamBolt.Launch(point, angle, ground ? 0f : Mathf.Sign(toUp) * .55f, Mathf.Abs(toUp), Mathf.Max(Body.TipSpeed * .75f, 55f), w, Beam.RealDamage, Body.Style, holder);
            Beam.HeldByFighter = false;
            // Cut off at the source: the firer has nothing left to feed once the head is knocked away.
            Beam.realSpeed = 0f; Beam.Firing = false;
            var attacker = Attacker;
            if (attacker) DbpuWorldLabel.Show(attacker, "BEAM SWATTED", "", 1.2f);
        }
        public override void Released(KiGuard.Outcome outcome)
        {
            if (!Beam) return;
            Beam.HeldByFighter = false;
            if (!Body) return;
            // A beam that died in the hands doesn't get to fly on through them while it fades.
            if (outcome == KiGuard.Outcome.AttackerStopped) Beam.realSpeed = 0f;
            if (Body.Pinned && outcome != KiGuard.Outcome.AttackerStopped) Body.Unpin();
            Body.Strain = 0f;
        }
    }

    // One fighter meeting one beam or sphere. Raised as soon as it is close enough to see coming, it
    // gets the arms up in time, then either takes it in the hands and fights it to a finish or swats
    // it away with a single blow.
    public sealed class KiGuard
    {
        public enum Kind { TwoHands, OneHand, Swat }
        public enum Stage { Brace, Hold, Heave, Strike, Done }
        public enum Outcome { None, Crushed, PushedBack, KiOut, Overwhelmed, AttackerStopped, Swatted, Missed }
        const string FrontUpper = "UpperArmFront", FrontLower = "LowerArmFront", BackUpper = "UpperArm", BackLower = "LowerArm";

        public readonly GuardTarget Target; public readonly PersonBehaviour Holder, Attacker;
        public BeamBehaviour Beam { get { var b = Target as BeamTarget; return b != null ? b.Beam : null; } }
        public KiOrb Orb { get { return Target as KiOrb; } }
        public Kind Grip { get; private set; }
        public Stage Current { get; private set; }
        public Outcome Result { get; private set; }
        public bool TwoHands { get { return Grip == Kind.TwoHands; } }
        public float Age { get; private set; }
        public float Ratio { get; private set; }
        public float CrushProgress { get; private set; }
        public float PushProgress { get; private set; }
        public float Slid { get; private set; }
        public float StrainNow { get { return strain; } }
        public Vector2 HoldPoint { get { return pin; } }

        readonly BeamRig rig; readonly BeamStance stance; readonly KiSystem ki; readonly DbpuPalette palette;
        string upperA, lowerA, upperB, lowerB; float facing;
        BeamContact contact;
        Vector2 tangent, pin, lastPin, heaveTarget;
        float strain, impact, overload, powerClock, labelClock, burnClock, braceClock, strikeClock, recover, lastX;
        bool impactDone, immune;

        public KiGuard(GuardTarget target, PersonBehaviour holder, Kind grip, float ratio)
        {
            Target = target; Holder = holder; Attacker = target.Attacker;
            Grip = grip; Ratio = ratio; Current = Stage.Brace;
            rig = BeamRig.For(holder);
            ki = holder.GetComponent<KiSystem>();
            palette = ArtVivid(DbpuAuraPalette.Get(holder));
            stance = new BeamStance(rig);
            tangent = target.Thrust(DbpuFx.Centre(holder));
            // Nobody meets a beam with their back to it.
            if (Mathf.Abs(tangent.x) > .2f) rig.Face(-tangent.x);
            facing = holder.transform.localScale.x < 0f ? -1f : 1f;
            ChooseArms(grip == Kind.TwoHands);
            target.SetImmune(holder, true); immune = true;
        }

        static DbpuPalette ArtVivid(DbpuPalette p)
        {
            // A pale base-form aura would wash these effects out.
            float h, sat, v;
            Color.RGBToHSV(p.Mid, out h, out sat, out v);
            if (sat >= .3f) return p;
            return new DbpuPalette { Deep = new Color(.1f, .22f, .55f, 1f), Mid = new Color(.45f, .72f, 1f, 1f), Hot = new Color(.92f, .97f, 1f, 1f) };
        }

        void ChooseArms(bool both)
        {
            bool front = rig.Usable(FrontUpper) && rig.Usable(FrontLower), back = rig.Usable(BackUpper) && rig.Usable(BackLower);
            // Reach with the arm that is already closer to it.
            bool useFront = front;
            if (front && back)
            {
                var f = rig.Limb(FrontLower); var b = rig.Limb(BackLower);
                useFront = Target.GapFrom(f.transform.position) <= Target.GapFrom(b.transform.position);
            }
            upperA = useFront ? FrontUpper : BackUpper; lowerA = useFront ? FrontLower : BackLower;
            if (both && front && back) { upperB = useFront ? BackUpper : FrontUpper; lowerB = useFront ? BackLower : FrontLower; }
            else { upperB = lowerB = null; if (both) Grip = Kind.OneHand; }
            rig.FreeShoulder(upperA);
            if (upperB != null) rig.FreeShoulder(upperB);
        }

        Vector2 Palm(string lower)
        {
            var limb = rig.Limb(lower);
            return limb ? KiCombatMath.HandPoint(limb) : rig.Core;
        }

        public Vector2 ContactPoint
        {
            get
            {
                Vector2 hands = upperB != null ? (Palm(lowerA) + Palm(lowerB)) * .5f : Palm(lowerA);
                return hands - tangent * Mathf.Min(.22f, Target.ContactSize * .1f);
            }
        }

        bool Capable
        {
            get
            {
                return Holder && Holder.IsAlive() && Holder.Consciousness > .3f && rig.Usable(upperA) && rig.Usable(lowerA) && rig.Limb("MiddleBody")
                    && rig.Limb("MiddleBody").GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic;
            }
        }

        // ---------------------------------------------------------------------------- motor
        public void FixedStep(float dt)
        {
            if (Current == Stage.Done || !rig || !Holder) return;
            if (!Capable) return;
            if (Target.Exists && (Current == Stage.Brace || Current == Stage.Strike)) tangent = Target.Thrust(Palm(lowerA));
            Vector2 aim = -tangent;
            switch (Current)
            {
                case Stage.Brace: PoseBrace(aim, dt); break;
                case Stage.Hold: PoseHold(aim, dt); break;
                case Stage.Heave: PoseHeave(aim, dt); break;
                case Stage.Strike: PoseStrike(aim, dt); break;
            }
            float toward = Mathf.Abs(aim.x) > .05f ? Mathf.Sign(aim.x) : facing;
            Vector2 down = Vector2.down;
            // Legs: the leading one bent into the thrust, the trailing one driven out straight.
            float load = Current == Stage.Hold ? Mathf.Clamp01(strain * 1.1f) : .5f;
            if (stance.Grounded || Current == Stage.Hold)
            {
                rig.Aim("UpperLegFront", DbpuFx.Rotate(down, toward * Mathf.Lerp(.35f, .7f, load)), 16f, 1.3f);
                rig.Aim("LowerLegFront", DbpuFx.Rotate(down, toward * Mathf.Lerp(.05f, -.1f, load)), 16f, 1.3f);
                rig.Aim("UpperLeg", DbpuFx.Rotate(down, -toward * Mathf.Lerp(.2f, .55f, load)), 16f, 1.3f);
                rig.Aim("LowerLeg", DbpuFx.Rotate(down, -toward * Mathf.Lerp(.3f, .85f, load)), 16f, 1.3f);
            }
        }

        void PoseBrace(Vector2 aim, float dt)
        {
            stance.Drive(Target.Exists ? Target.Intercept(rig.Core) : Vector2.zero, 150f, dt, 0f, palette.Mid);
            if (Grip == Kind.Swat)
            {
                // Hand drawn back across the body, ready to come round.
                float side = StrikeSide(aim);
                rig.Aim(upperA, DbpuFx.Rotate(aim, side * 1.9f), 26f, 1.6f);
                rig.Aim(lowerA, DbpuFx.Rotate(aim, side * 2.5f), 26f, 1.6f);
                rig.TorsoAngle(-toward(aim) * 5f, 12f);
                return;
            }
            rig.Aim(upperA, aim, 30f, 2f);
            rig.Aim(lowerA, aim, 30f, 2f);
            if (upperB != null) { rig.Aim(upperB, DbpuFx.Rotate(aim, .06f * facing), 30f, 2f); rig.Aim(lowerB, DbpuFx.Rotate(aim, -.05f * facing), 30f, 2f); }
            rig.TorsoAngle(toward(aim) * (TwoHands ? 14f : 5f), 12f);
        }

        float toward(Vector2 aim) { return aim.x >= 0f ? -1f : 1f; }

        float StrikeSide(Vector2 aim)
        {
            // The hand travels across its path towards where it will be sent.
            float wanted = Mathf.DeltaAngle(BeamBody.Angle(-aim), swatAngle);
            return wanted >= 0f ? -1f : 1f;
        }

        void PoseHold(Vector2 aim, float dt)
        {
            float bend = Mathf.Clamp01(strain * 1.15f);
            // Loaded arms give at the elbow; the flex only exists on the side the fighter faces.
            float side = Vector2.Dot(aim, new Vector2(facing, 0f)) > 0f ? facing : 0f;
            rig.Aim(upperA, DbpuFx.Rotate(aim, -side * .34f * bend), 30f, 2.2f);
            rig.Aim(lowerA, DbpuFx.Rotate(aim, side * .3f * bend), 30f, 2.2f);
            if (upperB != null)
            {
                rig.Aim(upperB, DbpuFx.Rotate(aim, -side * (.34f * bend + .07f)), 30f, 2.2f);
                rig.Aim(lowerB, DbpuFx.Rotate(aim, side * (.3f * bend + .05f)), 30f, 2.2f);
            }
            else
            {
                // The free arm thrown out behind for balance, or a clenched guard when it's easy.
                string freeUpper = upperA == FrontUpper ? BackUpper : FrontUpper, freeLower = lowerA == FrontLower ? BackLower : FrontLower;
                rig.Aim(freeUpper, DbpuFx.Rotate(Vector2.down, -toward(aim) * -.55f), 12f);
                rig.Aim(freeLower, DbpuFx.Rotate(Vector2.down, -toward(aim) * -.2f), 12f);
            }
            float lean = (TwoHands ? 10f : 4f) + bend * (TwoHands ? 18f : 10f);
            rig.TorsoAngle(toward(aim) * lean, 10f);
            rig.AimAt("UpperBody", toward(aim) * (lean + 8f * bend), 12f, 1.3f);
            rig.AimAt("LowerBody", toward(aim) * lean * .6f, 12f, 1.3f);
            rig.AimAt("Head", toward(aim) * (lean * .35f), 10f);

            // Thrust, grip and footing settle how the whole body moves.
            float pressure = 1f / Mathf.Max(.05f, Ratio);
            Vector2 along = tangent;
            float sustained = Mathf.Clamp(pressure - .78f, 0f, 1.3f) * 9.5f;
            float forward = PushProgress > 0f ? Mathf.Clamp((Ratio - 1.12f) * 6f, 0f, 8f) * DbpuFx.Smooth(.8f, 1.8f, Age) : 0f;
            float speed = impact + sustained - forward;
            stance.Drive(along * speed, 180f, dt, Mathf.Clamp01(Mathf.Abs(speed) / 6f + strain * .35f), Target.Style.Mid);
        }

        void PoseHeave(Vector2 aim, float dt)
        {
            // Both arms locked out: the beam is on its way back.
            rig.Aim(upperA, aim, 40f, 2.4f); rig.Aim(lowerA, aim, 40f, 2.4f);
            if (upperB != null) { rig.Aim(upperB, aim, 40f, 2.4f); rig.Aim(lowerB, aim, 40f, 2.4f); }
            rig.TorsoAngle(toward(aim) * 24f, 12f);
            stance.Drive(aim * 5f, 90f, dt, .4f, Target.Style.Mid);
        }

        void PoseStrike(Vector2 aim, float dt)
        {
            float t = Mathf.Clamp01(1f - strikeClock / StrikeTime);
            float side = StrikeSide(aim);
            float sweep = Mathf.Lerp(side * .9f, -side * 1.7f, StFxOut(t * 1.3f));
            rig.Aim(upperA, DbpuFx.Rotate(aim, sweep), 42f, 2.8f);
            rig.Aim(lowerA, DbpuFx.Rotate(aim, sweep * 1.12f), 42f, 2.8f);
            rig.TorsoAngle(toward(aim) * Mathf.Lerp(4f, -8f, t), 14f);
            stance.Drive(tangent * Mathf.Lerp(3.5f, 0f, t), 120f, dt, .3f, palette.Mid);
        }

        static float StFxOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }

        // ---------------------------------------------------------------------------- logic
        // Returns false once finished.
        public bool Tick(float dt)
        {
            Age += dt;
            if (Current == Stage.Done) return false;
            if (!Target.Exists || !Holder) { Finish(Outcome.Missed); return false; }
            if (!Capable)
            {
                if (Current == Stage.Hold) Fail(Outcome.Overwhelmed);
                else Finish(Outcome.Missed);
                return false;
            }
            powerClock -= dt;
            if (powerClock <= 0f) { powerClock = .12f; Ratio = Target.RatioFor(Holder) * (Grip == Kind.OneHand ? .9f : 1f); }
            switch (Current)
            {
                case Stage.Brace: TickBrace(dt); break;
                case Stage.Hold: TickHold(dt); break;
                case Stage.Heave: TickHeave(dt); break;
                case Stage.Strike:
                    strikeClock -= dt;
                    if (strikeClock <= 0f) Finish(Outcome.Swatted);
                    break;
            }
            return Current != Stage.Done;
        }

        void TickBrace(float dt)
        {
            braceClock += dt;
            if (!Target.Threat || Target.Interrupted) { Finish(Outcome.Missed); return; }
            Vector2 hand = Grip == Kind.Swat ? Palm(lowerA) : ContactPoint;
            tangent = Target.Thrust(hand);
            float gap = Mathf.Min(Target.GapFrom(hand), Target.BodyGap(rig.Core, BeamPhysics.BodyRadius(Holder)));
            float step = Target.Speed * Mathf.Max(dt, .016f) * 1.4f + .15f;
            if (Grip == Kind.Swat && swatAngle == 0f) PickSwat();
            if (gap <= step)
            {
                // Too far off the line: this was never going to reach them.
                if (Target.OffLine(DbpuFx.Centre(Holder)) > BeamPhysics.BodyRadius(Holder) + .9f) { Finish(Outcome.Missed); return; }
                if (Grip == Kind.Swat) Swat(); else Catch();
                return;
            }
            if (braceClock > Target.BraceLimit) Finish(Outcome.Missed);
        }

        public void ContactNow()
        {
            if (Current != Stage.Brace) return;
            if (Grip == Kind.Swat) { if (swatAngle == 0f) PickSwat(); Swat(); }
            else Catch();
        }

        void Catch()
        {
            Current = Stage.Hold;
            Age = 0f;
            tangent = Target.Thrust(ContactPoint);
            pin = lastPin = ContactPoint;
            lastX = rig.Core.x;
            Target.Catch(pin, tangent);
            float pressure = 1f / Mathf.Max(.05f, Ratio);
            impact = Mathf.Clamp(Target.Speed * .18f * Mathf.Sqrt(pressure), 5f, 26f) * (TwoHands ? 1f : .7f);
            strain = 1f;
            contact = new BeamContact(Target.Style, Target.Style, false, pin);
            var at = pin;
            SingularityTime.Hitstop(TwoHands ? .11f : .08f, .06f);
            BeamContact.Burst(at, tangent, Target.Style, Target.Style, TwoHands ? 1.1f : .75f);
            DbpuFx.Sound(at, ImpactFramesModule.SfxClash, .9f);
            DbpuFx.Sound(at, "Assets/SFX/Techniques/KiFist/HitHeavy.wav", .8f);
            Target.Kick(1f);
            Label(TwoHands ? "CAUGHT" : "ONE-HANDED CATCH", TwoHands ? "BOTH HANDS" : "NOT EVEN TRYING", 1.4f, -1f, palette.Hot);
            Vector2 ground, n;
            if (BeamGround.Find(rig.Core, 3.2f, Holder.transform.root, out ground, out n)) BeamGround.Tear(ground, tangent, 1f, Target.Style.Mid);
        }

        void TickHold(float dt)
        {
            if (!Target.Threat) { Finish(Outcome.AttackerStopped, .55f); return; }
            Vector2 next = ContactPoint;
            // A fighter who blinked away has let go, whatever the hands were doing.
            if (Vector2.Distance(next, lastPin) > 7f) { Finish(Outcome.Missed); return; }
            lastPin = pin; pin = next;
            Slid += Mathf.Abs(rig.Core.x - lastX); lastX = rig.Core.x;
            tangent = Target.Thrust(pin);
            float pressure = 1f / Mathf.Max(.05f, Ratio);
            impact = Mathf.MoveTowards(impact * Mathf.Exp(-dt * 2.1f), 0f, dt * .6f);
            float targetStrain = Mathf.Clamp01(pressure * .78f - .1f + Mathf.PerlinNoise(Age * 1.3f, 3.7f) * .22f);
            strain = Mathf.MoveTowards(strain, Mathf.Max(targetStrain, impact / 20f), dt * 1.5f);
            Target.Hold(pin, tangent, strain);

            // Holding back this much energy costs ki, and scorches the hands of anyone struggling.
            float cost = .07f * Mathf.Pow(pressure, 1.15f) * Mathf.Max(0f, DBPU.KiAttackCostMultiplier);
            if (ki)
            {
                float max = KiCombatMath.MaxKi(ki);
                ki.currentKi = Mathf.Max(0f, ki.currentKi - max * cost * dt);
                if (cost > 0f && ki.currentKi <= max * .025f) { Fail(Outcome.KiOut); return; }
                var attackerKi = Target.AttackerKi;
                if (attackerKi && DBPU.KiAttackCostMultiplier > 0f) attackerKi.currentKi = Mathf.Max(0f, attackerKi.currentKi - KiCombatMath.MaxKi(attackerKi) * .018f * dt);
            }
            burnClock -= dt;
            if (burnClock <= 0f)
            {
                burnClock = .2f;
                float burn = Mathf.Clamp(pressure - .72f, 0f, 1.1f);
                if (burn > 0f)
                {
                    HurtHand(lowerA, burn);
                    if (lowerB != null) HurtHand(lowerB, burn);
                }
            }

            overload = pressure > 1.28f ? overload + dt * (pressure - 1.28f) * 1.1f : Mathf.Max(0f, overload - dt * .25f);
            if (overload >= 1f) { Fail(Outcome.Overwhelmed); return; }

            if (Grip == Kind.OneHand && pressure > .95f && upperB == null && rig.Usable(BackLower) && rig.Usable(FrontLower)) AddSecondHand();

            bool oneHanded = Grip == Kind.OneHand;
            if (oneHanded) CrushProgress += dt * Mathf.Clamp((Ratio - 1.2f) * .42f, 0f, 1.3f);
            else
            {
                // Nobody's will is constant: an even contest swings back and forth before it breaks.
                float will = Mathf.Lerp(.8f, 1.25f, Mathf.PerlinNoise(Age * .55f, Holder.GetInstanceID() * .001f));
                float rate = Mathf.Clamp((Ratio * will - 1f) * .55f, -.15f, .7f);
                PushProgress = Mathf.Clamp01(PushProgress + dt * rate * DbpuFx.Smooth(.6f, 1.6f, Age));
            }
            float progress = oneHanded ? CrushProgress : PushProgress;
            labelClock -= dt;
            if (labelClock <= 0f && Age > 1.4f)
            {
                labelClock = .18f;
                Label(oneHanded ? "CRUSHING" : pressure > 1f ? "STRAINING" : "PUSHING BACK", KiLine, .35f, Mathf.Clamp01(progress), palette.Hot);
            }
            contact.Step(dt, pin, tangent, strain, Target.ContactSize);
            contact.Flare(0, Palm(lowerA), strain, palette, dt);
            if (lowerB != null) contact.Flare(1, Palm(lowerB), strain, palette, dt); else contact.HideFlare(1);
            BeamScene.Request(pin, -tangent, Target.Style.Mid, palette.Vivid, .3f + strain * .7f);

            if (oneHanded && CrushProgress >= 1f) { Crush(); return; }
            if (!oneHanded && PushProgress >= 1f) BeginHeave();
        }

        string KiLine
        {
            get { return ki ? "KI " + Mathf.RoundToInt(SingularityFighters.KiFraction(Holder) * 100f) + "%" : ""; }
        }

        void HurtHand(string lower, float amount)
        {
            var limb = rig.Limb(lower);
            if (!limb || limb.ImmuneToDamage) return;
            float scale = Mathf.Max(.01f, DBPU.DamageMultiplier);
            limb.Damage(limb.InitialHealth * .012f * amount * scale);
            if (limb.SkinMaterialHandler && UnityEngine.Random.value < .4f)
                try { limb.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, limb.transform.position, 4f + amount * 8f); } catch { }
        }

        void AddSecondHand()
        {
            upperB = upperA == FrontUpper ? BackUpper : FrontUpper;
            lowerB = lowerA == FrontLower ? BackLower : FrontLower;
            rig.FreeShoulder(upperB);
            Grip = Kind.TwoHands;
            strain = Mathf.Max(strain, .85f);
            Label("BOTH HANDS!", "", .9f, -1f, palette.Hot);
            DbpuFx.Sound(pin, "Assets/SFX/Techniques/KiFist/HitMedium.wav", .7f);
        }

        void Crush()
        {
            Vector2 at = pin;
            SingularityTime.Hitstop(.13f, .05f);
            Target.Crush(at, tangent);
            contact.End(1.5f);
            contact = null;
            DbpuFx.Sound(at, "Assets/SFX/Techniques/HakaiBall/Shatter.wav", 1f);
            DbpuFx.Sound(at, ImpactFramesModule.SfxClash, .8f);
            Label("CRUSHED", "", 1.6f, -1f, palette.Hot);
            Finish(Outcome.Crushed);
        }

        void BeginHeave()
        {
            Vector2 at = pin;
            SingularityTime.Hitstop(.09f, .08f);
            BeamContact.Burst(at, tangent, Target.Style, Target.Style, 1.2f);
            DbpuFx.Sound(at, "Assets/SFX/Techniques/KiFist/HitHeavy.wav", 1f);
            Target.Kick(1.15f);
            Label("HAAAA!", "SENDING IT BACK", 1.6f, -1f, palette.Hot);
            if (Target.ThrowBack(at, tangent, Holder))
            {
                contact.End(.6f);
                contact = null;
                Finish(Outcome.PushedBack);
                return;
            }
            Current = Stage.Heave;
            heaveTarget = ((BeamTarget)Target).Body.SpanStart;
        }

        void TickHeave(float dt)
        {
            var beam = (BeamTarget)Target;
            if (!Target.Threat) { Finish(Outcome.AttackerStopped, .55f); return; }
            heaveTarget = beam.Body.SpanStart;
            pin = Vector2.MoveTowards(pin, heaveTarget, dt * Mathf.Max(52f, Target.Speed * .7f));
            beam.Body.PinTo(pin);
            beam.Body.Strain = Mathf.MoveTowards(beam.Body.Strain, 1f, dt * 2f);
            strain = beam.Body.Strain;
            contact.Step(dt, pin, (pin - beam.Body.Root).normalized, 1f, Target.ContactSize);
            contact.HideFlare(0); contact.HideFlare(1);
            BeamScene.Request(pin, -tangent, Target.Style.Mid, palette.Vivid, 1f);
            var end = beam.Beam.EndBeam;
            float reach = (end && end.GetComponent<BeamComponent>() ? end.GetComponent<BeamComponent>().MinDistanceToLose : 3f) + 1.5f;
            if (Vector2.Distance(pin, heaveTarget) <= reach) Detonate();
        }

        // The beam's own head arrives back in its owner's hands.
        void Detonate()
        {
            var beam = (BeamTarget)Target;
            Vector2 at = pin;
            var attacker = Attacker;
            float damage = beam.Beam.RealDamage;
            SingularityTime.Hitstop(.16f, .05f);
            beam.Body.Unpin();
            beam.Beam.HeldByFighter = false;
            beam.Beam.realSpeed = 0f; beam.Beam.Firing = false;
            contact.End(3f);
            contact = null;
            Vector2 ground, n;
            if (BeamGround.Find(at, 6f, null, out ground, out n)) BeamGround.Erupt(ground, 2.4f, Target.Style.Mid, n);
            DbpuFx.Sound(at, ImpactFramesModule.SfxClash, 1f);
            DbpuFx.Sound(at, "Assets/SFX/Techniques/HakaiBall/Shatter.wav", .8f);
            if (attacker)
            {
                Vector2 back = (DbpuFx.Centre(attacker) - at).sqrMagnitude > .01f ? (DbpuFx.Centre(attacker) - at).normalized : -tangent;
                foreach (var limb in attacker.Limbs)
                {
                    if (!limb || limb.IsDismembered || limb.ImmuneToDamage) continue;
                    limb.Damage(damage * 300f);
                }
                SingularityFighters.Push(attacker, back * 24f + Vector2.up * 4f);
                DbpuWorldLabel.Show(attacker, "TURNED BACK ON THEM", "", 1.6f);
            }
            Finish(Outcome.PushedBack);
        }

        void Fail(Outcome outcome)
        {
            Vector2 at = pin;
            Target.Breakthrough(tangent);
            Target.SetImmune(Holder, false); immune = false;
            if (contact != null) { contact.End(1.6f); contact = null; }
            SingularityTime.Hitstop(.1f, .06f);
            DbpuFx.Sound(at, "Assets/SFX/Techniques/KiShield/Break.wav", 1f);
            Label(outcome == Outcome.KiOut ? "OUT OF KI" : "OVERWHELMED", "", 1.5f, -1f, new Color(1f, .45f, .35f));
            SingularityFighters.Push(Holder, tangent * 22f + Vector2.up * 3f);
            BeamPhysics.BlockReaction(Holder, 2.5f);
            Finish(outcome);
        }

        // ---------------------------------------------------------------------------- swat
        const float StrikeTime = .32f;
        float swatAngle; bool swatGround;

        // Up into the sky or down into the terrain, whichever the swing and the surroundings allow.
        void PickSwat()
        {
            Vector2 incoming = Target.Travel;
            float inAngle = BeamBody.Angle(incoming);
            float skyTurn = UnityEngine.Random.Range(82f, 118f), groundTurn = UnityEngine.Random.Range(104f, 132f);
            float a = inAngle + skyTurn, b = inAngle - skyTurn;
            bool aIsUp = BeamBody.Direction(a).y >= BeamBody.Direction(b).y;
            float upward = aIsUp ? a : b;
            float downward = aIsUp ? inAngle - groundTurn : inAngle + groundTurn;
            Vector2 from = Palm(lowerA);
            bool aimGround = (BeamPhysics.SwatPreference == 0 ? UnityEngine.Random.value < .48f : BeamPhysics.SwatPreference < 0) && BeamPhysics.TerrainAhead(from, BeamBody.Direction(downward), 70f);
            swatGround = aimGround;
            swatAngle = aimGround ? downward : upward;
            if (Mathf.Approximately(swatAngle, 0f)) swatAngle = .01f;
        }

        void Swat()
        {
            Current = Stage.Strike;
            strikeClock = StrikeTime;
            Vector2 hand = Palm(lowerA);
            tangent = Target.Thrust(hand);
            Vector2 point = Target.StrikePoint(hand);
            var limb = rig.Limb(lowerA);
            var limbBody = limb ? limb.GetComponent<Rigidbody2D>() : null;
            if (limbBody && braceClock < .03f && limbBody.velocity.sqrMagnitude > 90f)
            {
                // A real swing decides for itself where it goes.
                float swing = BeamBody.Angle(limbBody.velocity);
                if (Mathf.Abs(Mathf.DeltaAngle(swing, BeamBody.Angle(-tangent))) > 40f && Mathf.Abs(Mathf.DeltaAngle(swing, BeamBody.Angle(tangent))) > 25f) swatAngle = swing;
            }
            Target.Swat(point, swatAngle, swatGround, Holder);
            SpendSwat();
            Vector2 outward = BeamBody.Direction(swatAngle);
            var slash = new BeamQuad("DBPU swat slash", BeamQuad.Slash, "Top", 70);
            slash.Style(Target.Style); slash.Hot = Color.white; slash.Mid = palette.Vivid; slash.Dir = outward; slash.Spread = .5f;
            BeamPhysics.Fade(slash, point, 2.3f, .2f);
            SingularityTime.Hitstop(.07f, .05f);
            BeamContact.Burst(point, outward, Target.Style, Target.Style, 1.1f);
            var sparks = BeamParticles.Sparks;
            for (int i = 0; i < 40; i++)
            {
                Vector2 dir = DbpuFx.Rotate(outward, UnityEngine.Random.Range(-.7f, .7f));
                sparks.Emit(point, dir * UnityEngine.Random.Range(14f, 46f), UnityEngine.Random.Range(.16f, .45f), UnityEngine.Random.Range(.06f, .15f), Color.white, DbpuFx.A(Target.Style.Mid, 0f), 1.6f, 3f, 0f, true);
            }
            DbpuFx.Sound(point, "Assets/SFX/Techniques/Kienzan/Clang.wav", 1f);
            DbpuFx.Sound(point, "Assets/SFX/Techniques/KiFist/HitHeavy.wav", .9f);
            Label("SWATTED", swatGround ? "INTO THE GROUND" : "INTO THE SKY", 1.3f, -1f, palette.Hot);
            SingularityFighters.Push(Holder, tangent * 2.5f);
        }

        void SpendSwat()
        {
            if (ki && DBPU.KiAttackCostMultiplier > 0f) ki.currentKi = Mathf.Max(0f, ki.currentKi - KiCombatMath.MaxKi(ki) * .012f * DBPU.KiAttackCostMultiplier);
        }

        // ----------------------------------------------------------------------------- end
        void Label(string title, string subtitle, float seconds, float bar, Color color) { DbpuWorldLabel.Show(Holder, title, subtitle, seconds, bar, color); }

        void Finish(Outcome outcome, float burst = 0f)
        {
            if (Current == Stage.Done) return;
            Result = outcome; Current = Stage.Done;
            if (Target.Exists)
            {
                Target.Released(outcome);
                if (immune && outcome == Outcome.Missed) Target.SetImmune(Holder, false);
            }
            if (contact != null) { contact.End(burst); contact = null; }
            if (rig) rig.ReleaseAll();
        }

        public void Abort() { Finish(Outcome.Missed); }
    }
}
