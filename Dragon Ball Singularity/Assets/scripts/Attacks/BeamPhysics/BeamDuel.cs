using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DBPU
{
    // A head-on struggle as a force balance. Both heads are pinned to one free point; each beam
    // pushes it along its own line with its current power, so a stronger side drives it home, an
    // off-line shot slides it sideways, and moving either fighter bends both columns at once.
    public sealed class BeamDuel
    {
        const float MaxSpeed = 10f;
        public readonly BeamBehaviour A, B; public readonly BeamBody BodyA, BodyB;
        public Vector2 Point { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Age { get; private set; }
        public float Strain { get; private set; }
        public float Balance { get; private set; }
        readonly BeamContact contact; readonly BeamStyle styleA, styleB;
        readonly float wanderSeed = Random.value * 20f;
        Vector2 surge; float surgeClock = 1f, groundClock;
        public static readonly List<BeamDuel> All = new List<BeamDuel>();

        public static BeamDuel Begin(BeamBehaviour a, BeamBehaviour b, Vector2 point)
        {
            if (!BeamPhysics.On || !a || !b || !a.Body || !b.Body || !a.Body.Live || !b.Body.Live) return null;
            var clash = new BeamDuel(a, b, point);
            All.Add(clash);
            return clash;
        }

        public static BeamDuel Of(BeamBehaviour beam)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].A == beam || All[i].B == beam) return All[i];
            return null;
        }

        BeamDuel(BeamBehaviour a, BeamBehaviour b, Vector2 point)
        {
            A = a; B = b; BodyA = a.Body; BodyB = b.Body; Point = point;
            styleA = BodyA.Style; styleB = BodyB.Style;
            BodyA.PinTo(point); BodyB.PinTo(point);
            BodyA.Kick(Mathf.Max(.6f, BodyA.Width * .5f)); BodyB.Kick(Mathf.Max(.6f, BodyB.Width * .5f));
            contact = new BeamContact(styleA, styleB, true, point);
            SingularityTime.Hitstop(.09f, .06f);
            BeamContact.Burst(point, ((Vector2)a.BeamDirection).normalized, styleA, styleB, 1.5f);
        }

        public void Step(float dt)
        {
            // A clash strong enough to tear open a Reality Rift is being fought in there for now.
            if (dt <= 0f || RealityRift.Active) return;
            Age += dt;
            Vector2 toA = Point - BodyA.SpanStart, toB = Point - BodyB.SpanStart;
            Vector2 dirA = toA.sqrMagnitude > .01f ? toA.normalized : Vector2.right, dirB = toB.sqrMagnitude > .01f ? toB.normalized : Vector2.left;
            double powerA = BeamPhysics.BeamPower(A), powerB = BeamPhysics.BeamPower(B);
            float shareA = (float)(powerA / Math.Max(1e-9d, powerA + powerB)), shareB = 1f - shareA;
            Vector2 force = dirA * shareA + dirB * shareB;
            Balance = shareA - shareB;

            // Neither side holds perfectly steady: the point breathes back and forth, and every so
            // often one fighter throws everything they have into it.
            // The swings grow until something gives, so even an equal contest ends.
            float tide = (Mathf.PerlinNoise(Age * .9f, wanderSeed) - .5f) * .62f * (1f + Age * .22f);
            Vector2 lateral = new Vector2(-dirA.y, dirA.x) * (Mathf.PerlinNoise(wanderSeed, Age * .7f) - .5f) * .34f;
            surgeClock -= dt;
            if (surgeClock <= 0f)
            {
                surgeClock = Random.Range(1.1f, 2.5f);
                bool first = Random.value < .5f + Balance * .35f;
                Vector2 direction = first ? dirA : dirB;
                surge += direction * Random.Range(9f, 17f);
                (first ? BodyA : BodyB).Kick(Mathf.Max(.45f, (first ? BodyA : BodyB).Width * .4f));
                var spender = (first ? A : B).KiSys;
                if (spender && DBPU.KiAttackCostMultiplier > 0f) spender.currentKi = Mathf.Max(0f, spender.currentKi - KiCombatMath.MaxKi(spender) * .012f);
                BeamContact.Shock(Point, first ? styleA : styleB, 4f + Strain * 6f, .45f);
                KiSpaceRefraction.Pulse(Point, 2.6f + Strain * 2.6f, .45f + Strain * .3f, .5f, direction);
                DbpuFx.Sound(Point, "Assets/SFX/Techniques/KiFist/HitMedium.wav", .55f);
            }
            surge = Vector2.MoveTowards(surge, Vector2.zero, dt * 26f);
            Vector2 target = (force + dirA * tide + lateral) * MaxSpeed * Mathf.Max(.2f, DBPU.Beam_SpeedMultiplier) + surge;
            Velocity = Vector2.Lerp(Velocity, target, 1f - Mathf.Exp(-dt * 5.5f));
            Point += Velocity * dt;

            // The struggle never wanders far off the line between the two fighters.
            Vector2 span = BodyB.SpanStart - BodyA.SpanStart;
            if (span.sqrMagnitude > .01f)
            {
                float along = Mathf.Clamp01(Vector2.Dot(Point - BodyA.SpanStart, span) / span.sqrMagnitude);
                Vector2 onLine = BodyA.SpanStart + span * along;
                Point = Vector2.Lerp(Point, onLine, 1f - Mathf.Exp(-dt * 1.5f));
            }
            BodyA.PinTo(Point); BodyB.PinTo(Point);

            Strain = Mathf.Clamp01(.42f + Age / 7f + Mathf.Abs(Balance) * .2f);
            BodyA.Strain = BodyB.Strain = Strain;

            float size = Mathf.Max(BodyA.Width, BodyB.Width, .5f);
            contact.Step(dt, Point, dirA, Strain, size);
            BeamScene.Request(Point, dirB, styleA.Mid, styleB.Mid, .45f + Strain * .55f);

            groundClock -= dt;
            if (groundClock <= 0f)
            {
                groundClock = Mathf.Lerp(.5f, .16f, Strain);
                Vector2 ground, n;
                if (BeamGround.Find(Point, size + 5f + Strain * 5f, null, out ground, out n))
                {
                    BeamGround.Tear(ground + new Vector2(Random.Range(-size, size), 0f), Random.insideUnitCircle, Strain, styleA.Mid);
                    if (Random.value < .32f + Strain * .4f)
                        BeamSlabs.Raise(ground + new Vector2(Random.Range(-size * 1.6f, size * 1.6f), -.02f), Random.Range(.4f, 1.2f) * (.5f + Strain), Random.Range(-30f, 30f), Color.Lerp(styleA.Mid, styleB.Mid, Random.value));
                    if (Random.value < .2f) BeamGround.Scorch(ground, 1.2f + Strain * 2.4f, Color.Lerp(styleA.Mid, styleB.Mid, .5f));
                }
            }
        }

        public float GapTo(BeamBehaviour side) { return side == A ? Vector2.Distance(Point, BodyA.SpanStart) : Vector2.Distance(Point, BodyB.SpanStart); }

        public void End(BeamBehaviour winner)
        {
            All.Remove(this);
            BodyA.Unpin(); BodyB.Unpin();
            BodyA.Strain = BodyB.Strain = 0f;

            contact.End(winner ? 2.6f : 1.4f);
            if (!winner) return;
            SingularityTime.Hitstop(.12f, .05f);
            DbpuFx.Sound(Point, ImpactFramesModule.SfxClash, 1f);
            Vector2 ground, n;
            if (BeamGround.Find(Point, 9f, null, out ground, out n)) BeamGround.Erupt(ground, 1.8f, winner == A ? styleA.Mid : styleB.Mid, n);
            (winner == A ? BodyA : BodyB).Kick(Mathf.Max(.8f, (winner == A ? BodyA : BodyB).Width * .6f));
            var loser = (winner == A ? B : A).GetComponent<PersonBehaviour>();
            BeamPhysics.BlockReaction(loser, 1.6f);
        }
    }
}
