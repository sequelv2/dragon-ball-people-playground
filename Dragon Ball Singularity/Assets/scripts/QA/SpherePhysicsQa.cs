using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    // Native verification of the sphere attacks: real fighters, real projectiles, real physics. Only
    // runs in a disposable game process started with -dbpu-ki-combat-qa <folder> -dbpu-beam-qa -dbpu-sphere-qa,
    // on the arena the beam checks set up. Screenshots land in the output folder so the look can be judged.
    public sealed partial class KiCombatQa
    {
        string sphereOnly;
        bool SphereWants(string test) { return string.IsNullOrEmpty(sphereOnly) || sphereOnly.IndexOf(test, StringComparison.OrdinalIgnoreCase) >= 0; }

        static T Component<T>(PersonBehaviour person) where T : Component
        {
            var c = person.GetComponent<T>();
            return c ? c : person.gameObject.AddComponent<T>();
        }

        void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert(info != null, "Missing method " + method);
            var result = info.Invoke(target, args);
            var routine = result as IEnumerator;
            if (routine != null) ((MonoBehaviour)target).StartCoroutine(routine);
        }

        Transform ArmOf(PersonBehaviour person) { return BeamRig.For(person).Limb("LowerArmFront").transform; }

        void ClearBalls(params PersonBehaviour[] people)
        {
            foreach (var person in people)
            {
                var ball = person.GetComponent<BallBehaviour>();
                if (!ball) continue;
                ball.Firing = false; ball.Active = false;
            }
        }

        void Frozen(PersonBehaviour person, string where, bool expected)
        {
            bool frozen = person.Limbs.Any(l => l && l.GetComponent<FreezeBehaviour>());
            Assert(frozen == expected, where + ": firing arm frozen = " + frozen);
        }

        // Charges a ball with the arm held along dir and lets it go. Leaves `ball` in flight.
        IEnumerator ThrowBall(PersonBehaviour person, string effect, Vector2 dir, string shots)
        {
            BallBehaviour ball = null;
            if (!Check(() => { ball = Component<BallBehaviour>(person); })) yield break;
            yield return BeamAim(person, dir, .5f);
            if (!Check(() =>
            {
                ball.EffectID = effect; ball.Active = true; ball.Firing = false; ball.Charging = false; ball.FullCharged = false; ball.StillActive = false;
                Invoke(ball, "StartCharge", ArmOf(person));
            })) yield break;
            if (shots != null) yield return BeamShot(shots + "-1-charge.png", .8f);
            float wait = 0f;
            while (wait < 12f && !ball.FullCharged) { yield return null; wait += Time.deltaTime; }
            if (shots != null) yield return BeamShot(shots + "-2-charged.png", .15f);
            if (!Check(() =>
            {
                Assert(ball.FullCharged, effect + " never finished charging");
                Assert(ball.Orb != null, effect + " has no physical sphere");
                ball.Firing = true;
            })) yield break;
            yield return new WaitForSeconds(.1f);
            if (!Check(() => BeamRig.For(person).ReleaseAll())) yield break;
        }

        IEnumerator VerifySpheres()
        {
            var args = Environment.GetCommandLineArgs();
            int only = Array.IndexOf(args, "-dbpu-sphere-qa-only");
            sphereOnly = only >= 0 && only + 1 < args.Length ? args[only + 1] : null;
            if (!Check(() =>
            {
                Assert(Singularity.IsEnabled(SpherePhysics.ModuleId), "Dynamic Ki Spheres module is off");
                Assert(OrbFx.Ready, "Sphere shader bundle did not load"); Log("Colour space " + QualitySettings.activeColorSpace + ", HDR factor " + BeamFxAssets.Hdr);
            })) yield break;
            if (SphereWants("look")) { yield return VerifyBallLooks(); if (finished) yield break; }
            if (SphereWants("two")) { yield return VerifySphereTwoHands(); if (finished) yield break; }
            if (SphereWants("one")) { yield return VerifySphereOneHand(); if (finished) yield break; }
            if (SphereWants("swat")) { yield return VerifySphereSwat(true); if (finished) yield break; yield return VerifySphereSwat(false); if (finished) yield break; }
            if (SphereWants("kiout")) { yield return VerifySphereKiOut(); if (finished) yield break; }
            if (SphereWants("weak")) { yield return VerifySphereWeak(); if (finished) yield break; }
            if (SphereWants("blast")) { yield return VerifyBlasts(); if (finished) yield break; }
            if (SphereWants("medium")) { yield return VerifyMediumOrbs(); if (finished) yield break; }
            if (SphereWants("spirit")) { yield return VerifySpiritOrb(); if (finished) yield break; }
            if (SphereWants("hakai")) { yield return VerifyHakai(); if (finished) yield break; }
            if (SphereWants("final")) { yield return VerifyFinal(); if (finished) yield break; }
            if (SphereWants("beam")) { yield return VerifyBeamAgainstBall(); if (finished) yield break; }
            if (SphereWants("ballclash")) { yield return VerifyBallClash(); if (finished) yield break; }
            if (SphereWants("legacy")) { yield return VerifySpheresOff(); if (finished) yield break; }
            yield return new WaitForSecondsRealtime(1.2f);
            if (!Check(() =>
            {
                Assert(OrbDuel.All.Count == 0, "A clash leaked");
                Assert(BeamPhysics.Guards.Count == 0, "A guard leaked: " + BeamPhysics.Guards.Count);
                Application.logMessageReceivedThreaded -= BeamLog;
                lock (beamErrors)
                {
                    foreach (var e in beamErrors.Take(30)) Log("LOGGED " + e);
                    Log("Errors logged: " + beamErrors.Count);
                    Assert(beamErrors.Count == 0, beamErrors.Count + " errors logged during sphere QA");
                }
                Log("ALL SPHERE CHECKS PASSED");
            })) yield break;
        }

        // Every big sphere from the first spark to the ground going up.
        IEnumerator VerifyBallLooks()
        {
            foreach (string effect in new[] { "Death Ball", "Supernova", "Beerus Ball", "Gigantic Crusher" })
            {
                string tag = "l-" + effect.Replace(" ", "");
                float zoom = effect == "Death Ball" ? 44f : effect == "Gigantic Crusher" ? 62f : effect == "Supernova" ? 96f : 124f;
                if (!Check(() => { BeamReset(-50f, 340f, 1f); BeamCamera(new Vector2(-20f, beamFloor + zoom * .55f), zoom); })) yield break;
                yield return ThrowBall(beamA, effect, new Vector2(1f, -.02f).normalized, tag);
                var ball = beamA.GetComponent<BallBehaviour>();
                var orb = ball.Orb;
                float frames = 0f, took = 0f;
                while (frames < 30f) { yield return null; frames++; took += Time.unscaledDeltaTime; }
                Log("Frame time with " + effect + " on screen: " + (took / frames * 1000f).ToString("F1") + " ms");
                yield return BeamShot(tag + "-3-flight.png", .2f);
                yield return BeamShot(tag + "-3b-flight.png", .25f);
                float wait = 0f;
                while (wait < 9f && orb != null && !orb.Finished && ball.Firing) { yield return null; wait += Time.deltaTime; }
                yield return BeamShot(tag + "-4-blast.png", .06f);
                yield return BeamShot(tag + "-5-blast.png", .35f);
                yield return BeamShot(tag + "-6-blast.png", .8f);
                yield return new WaitForSeconds(1f);
                yield return BeamShot(tag + "-7-after.png");
                if (!Check(() =>
                {
                    Assert(orb.Finished, effect + " never went off");
                    Frozen(beamA, effect + " after going off", false);
                    Log("PASS " + effect + " charged, flew and went off after " + wait + " s");
                    ball.CancelBallState();
                })) yield break;
                yield return new WaitForSeconds(.8f);
            }
        }

        // Evenly matched: both hands, a slide, and thrown back into whoever fired it.
        IEnumerator VerifySphereTwoHands()
        {
            float healthA = 0f, healthB = 0f;
            if (!Check(() => { BeamReset(-28f, 26f, 1.4f); BeamCamera(new Vector2(0f, beamFloor + 22f), 32f); healthA = BeamHealth(beamA); healthB = BeamHealth(beamB); })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var ball = beamA.GetComponent<BallBehaviour>();
            var orb = ball.Orb;
            KiGuard guard = null; float wait = 0f;
            while (wait < 3f && (guard == null || guard.Current != KiGuard.Stage.Hold)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            if (!Check(() =>
            {
                Log("Two hands: ratio " + orb.RatioFor(beamB) + ", guard " + (guard != null ? guard.Current + "/" + guard.Grip : "none") + " after " + wait + " s; sphere at " + orb.Centre + ", fighter " + DbpuFx.Centre(beamB));
                Assert(guard != null && guard.Current == KiGuard.Stage.Hold, "The fighter never took the sphere in their hands");
                Assert(guard.TwoHands, "Expected two hands: " + guard.Grip);
                Assert(orb.Held, "The sphere is not held");
            })) yield break;
            yield return BeamShot("h-two-1-catch.png", .06f);
            yield return BeamShot("h-two-2-slide.png", .7f);
            if (!Check(() => { Vector2 c = DbpuFx.Centre(beamB); BeamCamera(new Vector2(c.x - beamX - 3f, c.y + 4f), 9f); })) yield break;
            yield return BeamShot("h-two-3-close.png", .1f);
            if (!Check(() =>
            {
                Assert(Vector2.Distance(orb.Centre, guard.HoldPoint) < orb.Radius * 1.2f, "The sphere drifted off the hands");
                Assert(BeamHealth(beamB) > healthB - .25f, "The holder is taking the sphere's full damage");
                BeamFiniteBodies("sphere hold");
                Log("Holding: slid " + guard.Slid + ", strain " + guard.StrainNow + ", health " + healthB + " -> " + BeamHealth(beamB));
                BeamCamera(new Vector2(0f, beamFloor + 22f), 32f);
            })) yield break;
            wait = 0f;
            while (wait < 12f && guard.Current == KiGuard.Stage.Hold) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("h-two-4-thrown.png", .1f);
            wait = 0f;
            while (wait < 5f && orb != null && !orb.Finished) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot("h-two-5-back.png", .2f);
            yield return new WaitForSeconds(.5f);
            if (!Check(() =>
            {
                Log("Two hands outcome: " + guard.Result + ", attacker health " + healthA + " -> " + BeamHealth(beamA));
                Assert(guard.Result == KiGuard.Outcome.PushedBack, "Expected it thrown back: " + guard.Result);
                Assert(BeamHealth(beamA) < healthA, "The sphere did not hurt whoever threw it");
                Frozen(beamA, "after being thrown back", false);
                Log("PASS sphere caught in both hands, slid and thrown back");
            })) yield break;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator VerifySphereOneHand()
        {
            if (!Check(() => { BeamReset(-28f, 26f, 3.8f); BeamCamera(new Vector2(0f, beamFloor + 22f), 32f); })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var orb = beamA.GetComponent<BallBehaviour>().Orb;
            KiGuard guard = null; float wait = 0f;
            while (wait < 3f && (guard == null || guard.Current != KiGuard.Stage.Hold)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            if (!Check(() =>
            {
                Log("One hand: ratio " + orb.RatioFor(beamB) + ", guard " + (guard != null ? guard.Current + "/" + guard.Grip : "none"));
                Assert(guard != null && guard.Current == KiGuard.Stage.Hold && guard.Grip == KiGuard.Kind.OneHand, "Expected a one-handed catch");
            })) yield break;
            yield return BeamShot("h-one-1-catch.png", .1f);
            wait = 0f;
            while (wait < 8f && guard.Current == KiGuard.Stage.Hold) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("h-one-2-crushed.png", .06f);
            yield return new WaitForSeconds(.1f);
            if (!Check(() =>
            {
                Log("One hand outcome: " + guard.Result + " after " + wait + " s");
                Assert(guard.Result == KiGuard.Outcome.Crushed, "Expected crushed: " + guard.Result);
                Assert(orb.Finished, "The sphere is still around");
                Frozen(beamA, "after being crushed", false);
                Log("PASS sphere crushed in one hand, attacker freed at once");
            })) yield break;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator VerifySphereSwat(bool sky)
        {
            string name = sky ? "h-swat-sky" : "h-swat-ground";
            if (!Check(() => { BeamReset(-28f, 26f, 40f); BeamPhysics.SwatPreference = sky ? 1 : -1; BeamCamera(new Vector2(0f, beamFloor + 26f), 38f); })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var orb = beamA.GetComponent<BallBehaviour>().Orb;
            KiGuard guard = null; float wait = 0f;
            while (wait < 3f && (guard == null || guard.Current != KiGuard.Stage.Strike)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            float healthB = BeamHealth(beamB);
            if (!Check(() =>
            {
                Assert(guard != null && guard.Current == KiGuard.Stage.Strike, "The overwhelming fighter never swatted the sphere");
                Log("Swatted: heading " + BeamBody.Angle(orb.Travel) + " deg, speed " + orb.Speed);
                if (sky) Assert(orb.Travel.y > .3f, "Sky swat is not heading up");
                else Assert(orb.Travel.y < -.1f, "Ground swat is not heading down");
                Frozen(beamA, "after the swat", false);
            })) yield break;
            yield return BeamShot(name + "-1-strike.png", .04f);
            yield return BeamShot(name + "-2-flight.png", .3f);
            wait = 0f;
            while (wait < 9f && !orb.Finished) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot(name + "-3-end.png", .1f);
            yield return new WaitForSeconds(.3f);
            if (!Check(() =>
            {
                Assert(orb.Finished, "The swatted sphere never ended");
                Assert(BeamHealth(beamB) >= healthB - .01f, "Swatting hurt the fighter: " + healthB + " -> " + BeamHealth(beamB));
                Log("PASS sphere swatted " + (sky ? "into the sky" : "into the ground") + ", gone after " + wait + " s");
            })) yield break;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator VerifySphereKiOut()
        {
            float healthB = 0f;
            if (!Check(() => { BeamReset(-28f, 26f, 1.05f); beamKiB.currentKi = 90f; BeamCamera(new Vector2(0f, beamFloor + 22f), 32f); healthB = BeamHealth(beamB); })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var orb = beamA.GetComponent<BallBehaviour>().Orb;
            if (!Check(() => beamKiB.currentKi = 90f)) yield break;
            KiGuard guard = null; float wait = 0f, watched = 0f;
            KiGuard.Outcome result = KiGuard.Outcome.None;
            while (wait < 8f && result == KiGuard.Outcome.None) { yield return null; wait += Time.unscaledDeltaTime; var now = BeamPhysics.GuardOf(beamB); if (now != null) guard = now; if (guard != null && guard.Current == KiGuard.Stage.Done) result = guard.Result; if (guard != null) watched += Time.unscaledDeltaTime; }
            yield return BeamShot("h-kiout-1.png", .1f);
            yield return new WaitForSeconds(1.5f);
            if (!Check(() =>
            {
                Log("Ki out: ratio " + orb.RatioFor(beamB) + ", outcome " + result + " after " + watched + " s, health " + healthB + " -> " + BeamHealth(beamB));
                Assert(result == KiGuard.Outcome.KiOut, "Expected the holder to run out of ki: " + result);
                Assert(BeamHealth(beamB) < healthB, "The sphere did not land once the holder gave out");
                Log("PASS ki runs out, the sphere breaks through and lands");
            })) yield break;
            yield return new WaitForSeconds(1f);
        }

        IEnumerator VerifySphereWeak()
        {
            float healthB = 0f;
            if (!Check(() => { BeamReset(-28f, 26f, .1f); BeamCamera(new Vector2(0f, beamFloor + 22f), 32f); healthB = BeamHealth(beamB); })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var orb = beamA.GetComponent<BallBehaviour>().Orb;
            float wait = 0f;
            while (wait < 6f && !orb.Finished) { yield return null; wait += Time.deltaTime; if (BeamPhysics.GuardOf(beamB) != null) break; }
            if (!Check(() =>
            {
                Assert(BeamPhysics.GuardOf(beamB) == null, "A far weaker fighter should not react");
                Assert(orb.Finished, "The sphere never went off");
            })) yield break;
            yield return new WaitForSeconds(.3f);
            if (!Check(() =>
            {
                Assert(BeamHealth(beamB) < healthB, "The blast did not hurt the fighter");
                Log("PASS a far weaker fighter just gets hit: health " + healthB + " -> " + BeamHealth(beamB));
            })) yield break;
            yield return new WaitForSeconds(1f);
        }

        // Small blasts: a stronger fighter slaps them aside, a weaker one takes them.
        IEnumerator VerifyBlasts()
        {
            BlastBehaviour blast = null;
            float healthB = 0f;
            if (!Check(() => { BeamReset(-12f, 8f, 30f); BeamCamera(new Vector2(-2f, beamFloor + 5f), 11f); blast = Component<BlastBehaviour>(beamA); healthB = BeamHealth(beamB); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .5f);
            yield return new WaitForSeconds(.1f);
            int slapped = 0, fired = 0;
            for (int i = 0; i < 6; i++)
            {
                BlastComponent shot = null;
                if (!Check(() =>
                {
                    blast.Active = true; blast.Firing = false;
                    Invoke(blast, "Fire", ArmOf(beamA));
                    fired++;
                    shot = FindObjectsOfType<BlastComponent>().OrderByDescending(b => b.GetInstanceID()).FirstOrDefault(b => b.User == beamA.gameObject && !b.Redirected);
                })) yield break;
                if (i == 0) { yield return BeamShot("b-blast-1-flight.png", .09f); }
                yield return new WaitForSeconds(.28f);
                if (shot && shot.Redirected) slapped++;
                if (i == 1) yield return BeamShot("b-blast-2-slap.png");
                yield return new WaitForSeconds(.2f);
            }
            yield return BeamShot("b-blast-3-after.png", .3f);
            if (!Check(() =>
            {
                Log("Blasts: " + slapped + " of " + fired + " slapped away, health " + healthB + " -> " + BeamHealth(beamB));
                Assert(slapped >= 4, "A far stronger fighter let too many blasts through: " + slapped);
                Assert(BeamHealth(beamB) >= healthB - .05f, "The slapped blasts hurt the fighter");
                blast.Active = false;
                Log("PASS small blasts are slapped aside");
            })) yield break;
            yield return new WaitForSeconds(.3f);
            // The techniques built on blasts all fly on the new look and leave nothing behind.
            var special = new Dictionary<string, Type> { { "GodPunisher", typeof(GodPunisher) }, { "StardustFall", typeof(StardustFall) }, { "GodsWrath", typeof(GodsWrathBehaviour) } };
            foreach (var pair in special)
            {
                Component comp = null;
                if (!Check(() => { BeamReset(-24f, 200f, 1f); BeamCamera(new Vector2(-2f, beamFloor + 9f), 26f); comp = beamA.GetComponent(pair.Value) ?? beamA.gameObject.AddComponent(pair.Value); })) yield break;
                yield return BeamAim(beamA, new Vector2(1f, .05f).normalized, .5f);
                yield return new WaitForSeconds(.3f);
                if (!Check(() =>
                {
                    pair.Value.GetField("Active").SetValue(comp, true);
                    if (pair.Key == "GodsWrath") Invoke(comp, "FireShot");
                    else { BeamRig.For(beamA).ReleaseAll(); Invoke(comp, "Fire", ArmOf(beamA)); }
                })) yield break;
                if (pair.Key == "GodsWrath") for (int i = 0; i < 20; i++) { yield return new WaitForSeconds(.05f); Check(() => Invoke(comp, "FireShot")); }
                yield return BeamShot("b-" + pair.Key + "-1.png", .3f);
                yield return BeamShot("b-" + pair.Key + "-2.png", .9f);
                if (!Check(() => pair.Value.GetField("Active").SetValue(comp, false))) yield break;
                yield return new WaitForSeconds(3.4f);
            }
            HellzoneGranade hell = null;
            if (!Check(() => { BeamReset(-20f, 8f, .1f); BeamCamera(new Vector2(-2f, beamFloor + 9f), 26f); hell = Component<HellzoneGranade>(beamA); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .5f);
            yield return new WaitForSeconds(.2f);
            if (!Check(() => { hell.Active = true; hell.Target = beamB.Limbs[2].gameObject; BeamRig.For(beamA).ReleaseAll(); Invoke(hell, "Fire", ArmOf(beamA)); })) yield break;
            yield return BeamShot("b-Hellzone-1.png", 2.4f);
            yield return BeamShot("b-Hellzone-2.png", 1.6f);
            yield return BeamShot("b-Hellzone-3.png", 1.2f);
            yield return new WaitForSeconds(3.5f);
            if (!Check(() => { hell.Active = false; Log("PASS barrage techniques fly and land on the new look"); })) yield break;
        }

        IEnumerator VerifyMediumOrbs()
        {
            // Stardust Breaker: charged in the hand, swatted by a far stronger fighter.
            StardustBreaker breaker = null;
            if (!Check(() => { BeamReset(-16f, 12f, 40f); BeamPhysics.SwatPreference = 1; BeamCamera(new Vector2(-2f, beamFloor + 6f), 13f); breaker = Component<StardustBreaker>(beamA); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .5f);
            if (!Check(() => { breaker.Active = true; typeof(StardustBreaker).GetField("UsedArm", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(breaker, ArmOf(beamA)); Invoke(breaker, "StartCharge", ArmOf(beamA)); })) yield break;
            yield return BeamShot("m-Breaker-1-charge.png", 1f);
            float wait = 0f;
            while (wait < 8f && !breaker.FullCharged) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot("m-Breaker-2-charged.png", .1f);
            if (!Check(() => breaker.StillActive = true)) yield break;
            KiGuard guard = null; wait = 0f;
            while (wait < 3f && (guard == null || guard.Current != KiGuard.Stage.Strike)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            yield return BeamShot("m-Breaker-3-swat.png", .05f);
            if (!Check(() =>
            {
                Assert(guard != null && guard.Result == KiGuard.Outcome.None && guard.Current == KiGuard.Stage.Strike, "The Stardust Breaker was not swatted");
                BeamRig.For(beamA).ReleaseAll();
                breaker.Active = false;
                Log("PASS Stardust Breaker swatted");
            })) yield break;
            yield return new WaitForSeconds(2.4f);
            // Cataclysmic Orb: tiny, very fast, and caught in one hand; then let through for the blast.
            CataclysmicOrbBehaviour orb = null;
            if (!Check(() => { BeamReset(-20f, 12f, 4f); BeamPhysics.SwatPreference = 0; BeamCamera(new Vector2(-4f, beamFloor + 6f), 16f); orb = Component<CataclysmicOrbBehaviour>(beamA); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .5f);
            yield return new WaitForSeconds(.3f);
            if (!Check(() => { orb.Active = true; orb.Firing = false; Invoke(orb, "Fire", ArmOf(beamA)); })) yield break;
            guard = null; wait = 0f;
            while (wait < 2f && (guard == null || guard.Current != KiGuard.Stage.Hold)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            yield return BeamShot("m-Orb-1-caught.png", .1f);
            if (!Check(() => Assert(guard != null && guard.Current == KiGuard.Stage.Hold, "The Cataclysmic Orb was not caught"))) yield break;
            wait = 0f;
            while (wait < 8f && guard.Current == KiGuard.Stage.Hold) { yield return null; wait += Time.unscaledDeltaTime; }
            if (!Check(() => { Log("Cataclysmic Orb caught: " + guard.Result); Assert(guard.Result == KiGuard.Outcome.Crushed, "Expected it crushed: " + guard.Result); })) yield break;
            yield return new WaitForSeconds(.5f);
            if (!Check(() => { BeamReset(-20f, 12f, .1f); BeamCamera(new Vector2(-4f, beamFloor + 18f), 40f); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .5f);
            yield return new WaitForSeconds(.3f);
            if (!Check(() => { orb.Active = true; orb.Firing = false; Invoke(orb, "Fire", ArmOf(beamA)); })) yield break;
            yield return BeamShot("m-Orb-2-blast.png", .32f);
            yield return BeamShot("m-Orb-3-blast.png", .4f);
            yield return BeamShot("m-Orb-4-blast.png", .8f);
            if (!Check(() => { orb.Active = false; BeamRig.For(beamA).ReleaseAll(); Log("PASS Cataclysmic Orb caught in hand, and lands when it isn't"); })) yield break;
            yield return new WaitForSeconds(1.6f);
        }

        IEnumerator VerifySpiritOrb()
        {
            SpiritBombChannel channel = null; BallBehaviour native = null; SpiritBombProjectile bomb = null;
            if (!Check(() =>
            {
                BeamReset(-22f, 22f, 60f); BeamPhysics.SwatPreference = 1; BeamCamera(new Vector2(0f, beamFloor + 12f), 22f);
                native = Component<BallBehaviour>(beamA); native.EffectID = "Spirit Bomb"; native.Active = true;
                channel = SpiritBombChannel.GetOrCreate(beamA);
                Assert(channel.BeginCharge(BeamRig.For(beamA).Limb("LowerArmFront")), "The Spirit Bomb would not start");
                bomb = channel.ChargingBomb;
            })) yield break;
            for (int i = 0; i < 60; i++) { yield return new WaitForSeconds(.04f); if (!Check(() => bomb.AddEnergy(beamKiA.maxKi * .05f, true))) yield break; }
            yield return BeamShot("s-spirit-1-gather.png");
            if (!Check(() =>
            {
                Log("Spirit Bomb radius " + bomb.Radius);
                Assert(channel.FreezeCharge(), "The Spirit Bomb would not stop gathering");
                Assert(channel.LaunchAt(beamB), "The Spirit Bomb would not launch at its target");
            })) yield break;
            yield return BeamShot("s-spirit-2-flight.png", .2f);
            KiGuard guard = null; float wait = 0f;
            while (wait < 5f && (guard == null || guard.Current != KiGuard.Stage.Strike)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            yield return BeamShot("s-spirit-3-swat.png", .05f);
            if (!Check(() =>
            {
                Assert(guard != null && guard.Current == KiGuard.Stage.Strike, "The overwhelming fighter did not swat the Spirit Bomb");
                Assert(bomb.Owner == beamB, "The swatted bomb still belongs to the attacker");
            })) yield break;
            yield return BeamShot("s-spirit-4-away.png", .6f);
            wait = 0f;
            while (wait < 14f && bomb && bomb.Phase < SpiritBombPhase.Detonating) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot("s-spirit-5-end.png", .1f);
            yield return new WaitForSeconds(2f);
            if (!Check(() => { native.Active = false; channel.CancelCharge(); Log("PASS Spirit Bomb swatted away"); })) yield break;
            yield return new WaitForSeconds(.3f);
        }

        IEnumerator VerifyHakai()
        {
            HakaiBallOrb orb = null;
            HakaiBallHand hand = null;
            if (!Check(() =>
            {
                BeamReset(-8f, 6f, 60f); BeamPhysics.SwatPreference = 1; BeamCamera(new Vector2(-2f, beamFloor + 4f), 8f);
                var limb = BeamRig.For(beamA).Limb("LowerArmFront");
                hand = HakaiBallHand.GetOrCreate(limb);
                orb = HakaiBallOrb.Create(beamA, hand);
                orb.Gather(KiCombatMath.HandPoint(limb), 1f);
                orb.Launch(Vector2.right, 18f, 1f);
            })) yield break;
            yield return BeamShot("k-hakai-2-flight.png", .25f);
            KiGuard guard = null; float wait = 0f; bool struck = false; string seen = "";
            while (wait < 3f && !struck)
            {
                yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB);
                if (guard != null && !seen.EndsWith(guard.Current.ToString())) seen += " " + guard.Current;
                struck = guard != null && guard.Current == KiGuard.Stage.Strike;
            }
            yield return BeamShot("k-hakai-3-swat.png", .05f);
            if (!Check(() =>
            {
                Log("Hakai Ball guard stages:" + seen + " after " + wait + " s, flying " + orb.Flying + ", sealing " + orb.Sealing);
                Assert(struck, "The overwhelming fighter did not swat the Hakai Ball");
                Assert(!orb.Sealing, "The Hakai Ball sealed a far stronger fighter");
                Log("PASS Hakai Ball swatted by a far stronger fighter");
            })) yield break;
            yield return new WaitForSeconds(1.5f);
            // A weaker fighter is sealed, as ever.
            if (!Check(() =>
            {
                BeamReset(-8f, 6f, .2f); BeamCamera(new Vector2(-2f, beamFloor + 4f), 8f);
                orb = HakaiBallOrb.Create(beamA, hand);
                orb.Gather(KiCombatMath.HandPoint(BeamRig.For(beamA).Limb("LowerArmFront")), 1f);
                orb.Launch(Vector2.right, 18f, 1f);
            })) yield break;
            wait = 0f;
            while (wait < 3f && !orb.Sealing) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot("k-hakai-4-seal.png", .4f);
            if (!Check(() =>
            {
                Assert(orb.Sealing, "The Hakai Ball did not seal a weaker fighter");
                orb.Fizzle();
                Log("PASS Hakai Ball still seals the weak");
            })) yield break;
            yield return new WaitForSeconds(.8f);
        }

        IEnumerator VerifyFinal()
        {
            FinalExplosion final = null;
            if (!Check(() => { BeamReset(-4f, 300f, 1f); BeamCamera(new Vector2(-4f, beamFloor + 26f), 60f); final = Component<FinalExplosion>(beamA); })) yield break;
            yield return new WaitForSeconds(.2f);
            if (!Check(() => { final.Active = true; Invoke(final, "Fire", beamA.Limbs[2].transform); })) yield break;
            yield return BeamShot("f-final-1.png", .4f);
            yield return BeamShot("f-final-2.png", 1.2f);
            yield return BeamShot("f-final-3.png", 2.4f);
            float wait = 0f;
            while (wait < 8f && final.stillActive) { yield return null; wait += Time.deltaTime; }
            yield return BeamShot("f-final-4.png", .1f);
            if (!Check(() =>
            {
                Assert(!final.stillActive, "Final Explosion never ended");
                final.Active = false;
                Log("PASS Final Explosion plays out");
                foreach (var limb in beamA.Limbs) if (limb) { limb.Health = limb.InitialHealth; limb.Broken = false; }
                beamA.Consciousness = 1f;
            })) yield break;
            yield return new WaitForSeconds(.4f);
        }

        IEnumerator VerifyBeamAgainstBall()
        {
            if (!Check(() =>
            {
                BeamReset(-40f, 40f, .5f); BeamCamera(new Vector2(0f, beamFloor + 22f), 40f);
                var cooldown = typeof(RealityRift).GetField("cooldownUntil", BindingFlags.NonPublic | BindingFlags.Static);
                if (cooldown != null) cooldown.SetValue(null, float.MaxValue);
            })) yield break;
            yield return ThrowBall(beamA, "Death Ball", new Vector2(1f, .0f), null);
            var ball = beamA.GetComponent<BallBehaviour>();
            var orb = ball.Orb;
            if (!Check(() => BeamRig.For(beamB).Face(-1f))) yield break;
            yield return BeamAim(beamB, new Vector2(-1f, .3f).normalized, .2f);
            if (!Check(() => BeamRelease(beamB, fireB, "Galick Gun"))) yield break;
            OrbDuel duel = null; float wait = 0f;
            while (wait < 4f && duel == null) { yield return null; wait += Time.unscaledDeltaTime; duel = OrbDuel.Of(orb); }
            if (!Check(() =>
            {
                Assert(duel != null, "The beam and the sphere never locked: tip " + (fireB.Body ? fireB.Body.Tip : Vector2.zero) + ", sphere " + orb.Centre + " r " + orb.Radius);
                Assert(fireB.Body.Pinned, "The beam is not pressed against the sphere");
                Log("Beam against sphere at " + duel.Point + ", balance " + duel.Balance);
            })) yield break;
            yield return BeamShot("c-beam-1-lock.png", .1f);
            yield return BeamShot("c-beam-2-push.png", 1.4f);
            wait = 0f;
            while (wait < 30f && OrbDuel.All.Contains(duel)) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("c-beam-3-end.png", .1f);
            yield return new WaitForSeconds(.8f);
            if (!Check(() =>
            {
                Assert(!OrbDuel.All.Contains(duel), "The clash never ended");
                Log("Beam against sphere ended after " + wait + " s, score " + duel.Score + ", beam firing " + fireB.Firing + ", sphere " + (orb.Finished ? "finished" : "flying"));
                BeamStop(fireB);
                ball.Firing = false;
                Log("PASS a beam and a sphere lock and one wins");
            })) yield break;
            yield return new WaitForSeconds(1.6f);
        }

        IEnumerator VerifyBallClash()
        {
            if (!Check(() =>
            {
                BeamReset(-50f, 50f, 1.2f); BeamCamera(new Vector2(0f, beamFloor + 24f), 46f);
                BeamRig.For(beamB).Face(-1f);
            })) yield break;
            BallBehaviour ballA = null, ballB = null;
            if (!Check(() => { ballA = Component<BallBehaviour>(beamA); ballB = Component<BallBehaviour>(beamB); })) yield break;
            yield return BeamAim(beamA, Vector2.right, .2f);
            yield return BeamAim(beamB, Vector2.left, .2f);
            if (!Check(() =>
            {
                foreach (var b in new[] { ballA, ballB }) { b.EffectID = "Death Ball"; b.Active = true; b.Firing = false; b.Charging = false; b.FullCharged = false; b.StillActive = false; }
                Invoke(ballA, "StartCharge", ArmOf(beamA));
                Invoke(ballB, "StartCharge", ArmOf(beamB));
            })) yield break;
            float gathering = 0f;
            while (gathering < 12f && !(ballA.FullCharged && ballB.FullCharged)) { yield return null; gathering += Time.deltaTime; }
            if (!Check(() => { Assert(ballA.FullCharged && ballB.FullCharged, "The spheres never finished gathering"); ballA.Firing = true; ballB.Firing = true; })) yield break;
            yield return new WaitForSeconds(.1f);
            if (!Check(() => { BeamRig.For(beamA).ReleaseAll(); BeamRig.For(beamB).ReleaseAll(); })) yield break;
            var orbA = ballA.Orb; var orbB = ballB.Orb;
            OrbDuel duel = null; float wait = 0f;
            while (wait < 10f && duel == null) { yield return null; wait += Time.unscaledDeltaTime; if (orbA != null) duel = OrbDuel.Of(orbA); }
            if (!Check(() =>
            {
                Assert(duel != null, "The spheres never locked: A at " + orbA.Centre + " threat " + orbA.Threat + " moving " + orbA.Velocity + ", B at " + orbB.Centre + " threat " + orbB.Threat + " moving " + orbB.Velocity + ", after " + wait + " s");
                Log("Spheres locked at " + duel.Point);
            })) yield break;
            yield return BeamShot("c-ball-1-lock.png", .1f);
            yield return BeamShot("c-ball-2-push.png", 1.4f);
            wait = 0f;
            while (wait < 30f && OrbDuel.All.Contains(duel)) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("c-ball-3-end.png", .1f);
            yield return new WaitForSeconds(.5f);
            if (!Check(() =>
            {
                Assert(!OrbDuel.All.Contains(duel), "The clash never ended");
                Assert(orbA.Finished != orbB.Finished || (orbA.Finished && orbB.Finished), "Neither sphere gave way");
                Log("Spheres ended after " + wait + " s, score " + duel.Score);
                ClearBalls(beamA, beamB);
                Log("PASS two spheres lock and the stronger carries on");
            })) yield break;
            yield return new WaitForSeconds(1.8f);
        }

        // Switched off, the sphere attacks are exactly what they were.
        IEnumerator VerifySpheresOff()
        {
            if (!Check(() =>
            {
                Singularity.SetEnabled(SpherePhysics.ModuleId, false);
                Assert(!SpherePhysics.On, "The module did not switch off");
                BeamReset(-30f, 60f, 1f); BeamCamera(new Vector2(0f, beamFloor + 20f), 30f);
            })) yield break;
            var ball = Component<BallBehaviour>(beamA);
            yield return BeamAim(beamA, Vector2.right, .4f);
            if (!Check(() =>
            {
                ball.EffectID = "Death Ball"; ball.Active = true; ball.Firing = false; ball.Charging = false; ball.FullCharged = false; ball.StillActive = false;
                Invoke(ball, "StartCharge", ArmOf(beamA));
            })) yield break;
            float wait = 0f;
            while (wait < 6f && !ball.FullCharged) { yield return null; wait += Time.deltaTime; }
            if (!Check(() =>
            {
                Assert(ball.Orb == null, "A physical sphere appeared with the module off");
                Assert(ball.ProjectileObject.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "The original look is hidden");
                ball.Firing = true;
            })) yield break;
            yield return new WaitForSeconds(1f);
            if (!Check(() =>
            {
                Assert(beamA.Limbs.Any(l => l && l.GetComponent<FreezeBehaviour>()), "The old frozen arm is gone");
                ball.Firing = false;
                Log("PASS module off restores the old spheres");
            })) yield break;
            yield return new WaitForSeconds(1f);
            if (!Check(() => { Singularity.SetEnabled(SpherePhysics.ModuleId, true); ball.CancelBallState(); BeamRig.For(beamA).ReleaseAll(); })) yield break;
        }
    }
}
