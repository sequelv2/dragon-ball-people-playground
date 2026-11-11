using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    // Native verification of Dynamic Beams: real fighters, real beams, real physics. Only runs in a
    // disposable game process started with -dbpu-ki-combat-qa <folder> -dbpu-beam-qa. Screenshots
    // land in the output folder so the look can be judged, and every step asserts real outcomes.
    public sealed partial class KiCombatQa
    {
        PersonBehaviour beamA, beamB; KiSystem beamKiA, beamKiB; BeamBehaviour fireA, fireB;
        readonly List<string> beamErrors = new List<string>();
        float beamFloor, beamX = 600f; bool beamLit; string beamOnly;

        void BeamLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && !(type == LogType.Warning && condition.Contains("[Singularity]"))) return;
            if (condition.Contains("Singularity") || stack.Contains("DBPU."))
                lock (beamErrors) beamErrors.Add(type + ": " + condition + "\n" + stack);
        }

        bool BeamWants(string test) { return string.IsNullOrEmpty(beamOnly) || beamOnly.IndexOf(test, StringComparison.OrdinalIgnoreCase) >= 0; }

        void BeamCamera(Vector2 centre, float size)
        {
            var camera = Global.main.camera;
            camera.transform.position = new Vector3(centre.x + beamX, centre.y, -10);
            camera.orthographicSize = size;
        }

        IEnumerator BeamShot(string file, float after = 0f)
        {
            if (after > 0f) yield return new WaitForSecondsRealtime(after);
            yield return new WaitForEndOfFrame();
            Check(() => Capture(file));
        }

        // Puts both fighters on the ground at x positions, whole and rested, at the given power gap.
        void BeamReset(float xA, float xB, float boostB)
        {
            foreach (var pair in new[] { new { P = beamA, K = beamKiA, X = xA }, new { P = beamB, K = beamKiB, X = xB } })
            {
                Vector2 c = DbpuFx.Centre(pair.P);
                foreach (var body in pair.P.GetComponentsInChildren<Rigidbody2D>()) { body.velocity = Vector2.zero; body.angularVelocity = 0f; }
                SingularityFighters.Translate(pair.P, new Vector2(pair.X + beamX, beamFloor + 1.5f) - c);
                pair.K.maxKi = 1000f; pair.K.currentKi = 1000f; pair.K.baseRegen = 0f;
                foreach (var limb in pair.P.Limbs) if (limb) { limb.Health = limb.InitialHealth; limb.ImmuneToDamage = false; limb.Broken = false; }
                pair.P.Consciousness = 1f;
                BeamRig.For(pair.P).ReleaseAll(); BeamRig.For(pair.P).Face(1f);
                var flight = pair.P.GetComponentInChildren<Flight>();
                if (flight && flight.IsFlying) flight.Use();
            }
            beamB.GetComponent<MultipliersAndStuffs>().BoostMultiplier = boostB;
            beamA.GetComponent<MultipliersAndStuffs>().BoostMultiplier = 1f;
            foreach (var p in new[] { beamA, beamB }) { var m = p.GetComponent<MultipliersAndStuffs>(); m.Recalculate(); CombatPowerController.Bind(p).Refresh(); }
            BeamPhysics.SwatPreference = 0;
        }

        // Stand the fighter up with the arm along dir, the way a player would pose them.
        IEnumerator BeamAim(PersonBehaviour person, Vector2 dir, float seconds = .45f)
        {
            var rig = BeamRig.For(person);
            float age = 0f;
            string upper = "UpperArmFront", lower = "LowerArmFront";
            rig.FreeShoulder(upper);
            while (age < seconds)
            {
                rig.Hover(true);
                rig.Steer(Vector2.zero, 90f);
                rig.Aim(upper, dir, 26f, 2f); rig.Aim(lower, dir, 26f, 2f);
                rig.TorsoAngle(0f, 9f);
                yield return new WaitForFixedUpdate();
                age += Time.fixedDeltaTime;
            }
        }

        void BeamRelease(PersonBehaviour person, BeamBehaviour beam, string effect, Transform arm = null)
        {
            var rig = BeamRig.For(person);
            if (!arm) arm = rig.Limb("LowerArmFront").transform;
            beam.EffectID = effect; beam.BeamType = "ArmBeam"; beam.Active = true; beam.UsedArm = arm;
            beam.Firing = false; beam.Charging = false; beam.Clashing = false; beam.StillActive = false; beam.TimeHolding = 0f; beam.SbcBlockedThisFire = false;
            var release = typeof(BeamBehaviour).GetMethod("Release", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert(release != null, "Native beam release method missing");
            beam.StartCoroutine((IEnumerator)release.Invoke(beam, new object[] { arm, beam.BeamAudio }));
            rig.ReleaseAll();
        }

        void BeamStop(params BeamBehaviour[] beams)
        {
            foreach (var guard in BeamPhysics.Guards.ToArray()) guard.Abort();
            foreach (var beam in beams) if (beam) beam.CancelBeamState();
        }

        void BeamFiniteBodies(string where)
        {
            foreach (var p in new[] { beamA, beamB })
                foreach (var body in p.GetComponentsInChildren<Rigidbody2D>())
                {
                    Vector2 v = body.position;
                    Assert(Finite(v.x) && Finite(v.y) && Finite(body.velocity.x) && Finite(body.velocity.y), "Non-finite body after " + where + ": " + body.name);
                }
            foreach (var body in BeamBody.Active)
                if (body && body.Live)
                    for (int i = 0; i < body.NodeCount; i++) Assert(Finite(body.Nodes[i].x) && Finite(body.Nodes[i].y), "Non-finite spline after " + where);
        }

        static float BeamHealth(PersonBehaviour p) { return SingularityFighters.HealthFraction(p); }

        // Largest distance of the spline from the straight line between its ends.
        static float BeamBow(BeamBody body)
        {
            if (!body || body.NodeCount < 3) return 0f;
            Vector2 a = body.Nodes[0], b = body.Nodes[body.NodeCount - 1], d = (b - a).normalized;
            float most = 0f;
            for (int i = 1; i < body.NodeCount - 1; i++) most = Mathf.Max(most, Mathf.Abs(BeamPhysics.Cross(d, body.Nodes[i] - a)));
            return most;
        }

        IEnumerator VerifyBeamPhysics()
        {
            Application.logMessageReceivedThreaded += BeamLog;
            GameObject spawnA = null, spawnB = null;
            if (!Check(() =>
            {
                var args = Environment.GetCommandLineArgs();
                int only = Array.IndexOf(args, "-dbpu-beam-qa-only");
                beamOnly = only >= 0 && only + 1 < args.Length ? args[only + 1] : null;
                foreach (var actor in FindObjectsOfType<PersonBehaviour>()) actor.transform.position += Vector3.left * 900f;
                DBPU.KiDrainMultiplier = 0f; DBPU.DamageMultiplier = 1f; DBPU.KiAttackCostMultiplier = 1f; DBPU.VoiceLines = false;
                Assert(Singularity.IsEnabled(BeamPhysics.ModuleId), "Dynamic Beams module is off");
                Assert(BeamFxAssets.Ready, "Beam shader bundle did not load");
                Vector2 ground, normal;
                beamLit = BeamGround.Find(new Vector2(beamX, 400f), 900f, null, out ground, out normal);
                beamFloor = beamLit ? ground.y : 28f;
                if (!beamLit)
                {
                    var floor = new GameObject("QA ground") { layer = 11 };
                    floor.transform.position = new Vector3(beamX, beamFloor - 2f, 0f);
                    floor.AddComponent<BoxCollider2D>().size = new Vector2(400f, 4f);
                    var view = new GameObject("QA ground view").AddComponent<SpriteRenderer>();
                    view.transform.SetParent(floor.transform, false);
                    view.sprite = DbpuFx.SpriteOf(DbpuFx.Square);
                    view.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    view.color = new Color(.28f, .23f, .19f, 1f);
                    view.sortingLayerName = "Default"; view.sortingOrder = -20;
                    DbpuFx.WorldScale(view.transform, new Vector3(800f, 8f, 1f));
                    Global.main.camera.backgroundColor = new Color(.42f, .6f, .84f);
                }
                Log("Ground at " + beamFloor + " (map ground: " + beamLit + "); sorting layers: " + string.Join(", ", SortingLayer.layers.Select(l => l.name)) + "; hdr=" + Global.main.camera.allowHDR);
                spawnA = Spawn("[DBPU] Gogito", new Vector3(beamX - 10f, beamFloor + 1.6f, 0f));
                spawnB = Spawn("[DBPU] Gogito", new Vector3(beamX + 10f, beamFloor + 1.6f, 0f));
                BeamCamera(new Vector2(0f, beamFloor + 5f), 13f);
            })) yield break;
            yield return new WaitForSecondsRealtime(1.6f);
            if (!Check(() =>
            {
                Prepare(spawnA, out beamA, out beamKiA); Prepare(spawnB, out beamB, out beamKiB);
                foreach (var actor in new[] { spawnA, spawnB })
                {
                    if (!beamLit) foreach (var r in actor.GetComponentsInChildren<SpriteRenderer>()) r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    foreach (var b in actor.GetComponentsInChildren<Rigidbody2D>()) b.bodyType = RigidbodyType2D.Dynamic;
                }
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                fireA = spawnA.GetComponent<BeamBehaviour>(); fireB = spawnB.GetComponent<BeamBehaviour>();
                Assert(fireA && fireB, "Fighters have no native beam controller");
                Log("Limbs: " + string.Join(",", beamA.Limbs.Select(l => l ? l.name : "null")));
                foreach (var limb in beamA.Limbs.Where(l => l && l.Joint))
                    Log("JOINT " + limb.name + " ref=" + limb.Joint.referenceAngle + " limits=" + limb.Joint.limits.min + ".." + limb.Joint.limits.max + " useLimits=" + limb.Joint.useLimits + " motor=" + limb.Joint.motor.maxMotorTorque + " parent=" + (limb.Joint.connectedBody ? limb.Joint.connectedBody.name : "none"));
                Log("Fixed dt " + Time.fixedDeltaTime + ", body power " + DbpuFx.PowerOf(beamA));
                var limbView = beamA.Limbs[0].GetComponent<SpriteRenderer>();
                Log("All limb layers: " + string.Join(", ", beamA.Limbs.Select(l => l.name + "=" + l.GetComponent<SpriteRenderer>().sortingLayerName + "/" + l.GetComponent<SpriteRenderer>().sortingOrder)));
                Log("Limb sorting " + limbView.sortingLayerName + "/" + limbView.sortingOrder + ", lower arm " + beamA.Limbs[11].GetComponent<SpriteRenderer>().sortingLayerName + "/" + beamA.Limbs[11].GetComponent<SpriteRenderer>().sortingOrder + ", back arm " + beamA.Limbs[13].GetComponent<SpriteRenderer>().sortingLayerName + "/" + beamA.Limbs[13].GetComponent<SpriteRenderer>().sortingOrder);
                foreach (var view in FindObjectsOfType<Renderer>().Where(v => v.transform.root != spawnA.transform && v.transform.root != spawnB.transform && v.enabled && v.bounds.size.x > 30f).Take(16)) Log("Big renderer " + view.name + " layer " + view.sortingLayerName + "/" + view.sortingOrder + " size " + view.bounds.size + " at " + view.bounds.center);
            })) yield break;
            yield return BeamShot("00-baseline.png", .2f);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-sphere-qa") >= 0) { yield return VerifySpheres(); yield break; }

            if (BeamWants("free")) { yield return VerifyFreeBeam(); if (finished) yield break; }

            if (BeamWants("two")) { yield return VerifyTwoHands(); if (finished) yield break; }
            if (BeamWants("one")) { yield return VerifyOneHand(); if (finished) yield break; }
            if (BeamWants("swat")) { yield return VerifySwat(true); if (finished) yield break; yield return VerifySwat(false); if (finished) yield break; }
            if (BeamWants("kiout")) { yield return VerifyKiOut(); if (finished) yield break; }
            if (BeamWants("weak")) { yield return VerifyWeakHit(); if (finished) yield break; }
            if (BeamWants("styles")) { yield return VerifyStyles(); if (finished) yield break; }
            if (BeamWants("rays")) { yield return VerifyRays(); if (finished) yield break; }
            if (BeamWants("legacy")) { yield return VerifyModuleOff(); if (finished) yield break; }
            // Last: the loser of a long struggle can come out of it in pieces.
            if (BeamWants("clash")) { yield return VerifyClash(); if (finished) yield break; }
            yield return new WaitForSecondsRealtime(1.6f);
            if (!Check(() =>
            {
                Application.logMessageReceivedThreaded -= BeamLog;
                Assert(BeamPhysics.Guards.Count == 0, "A beam guard leaked: " + BeamPhysics.Guards.Count);
                Assert(BeamDuel.All.Count == 0, "A beam duel leaked");
                Assert(BeamBolt.All.Count == 0, "A beam bolt leaked");
                Assert(BeamBody.Active.Count == 0, "A beam body stayed active: " + BeamBody.Active.Count);
                lock (beamErrors)
                {
                    foreach (var e in beamErrors.Take(30)) Log("LOGGED " + e);
                    Log("Beam errors logged: " + beamErrors.Count);
                    Assert(beamErrors.Count == 0, beamErrors.Count + " errors logged during beam QA");
                }
            })) yield break;
        }

        // Fired into empty air: the arm stays frozen where it was aimed, even when shoved, the column
        // stays dead straight, and the arm comes free again when the beam ends.
        IEnumerator VerifyFreeBeam()
        {
            if (!Check(() => { BeamReset(-22f, 60f, 1f); BeamCamera(new Vector2(0f, beamFloor + 6f), 18f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            float startRoot = 0f; BeamBody body = null; Vector2 armBefore = Vector2.zero;
            var arm = BeamRig.For(beamA).Limb("LowerArmFront").GetComponent<Rigidbody2D>();
            if (!Check(() => BeamRelease(beamA, fireA, "Kamehameha"))) yield break;
            yield return new WaitForSeconds(.55f);
            if (!Check(() =>
            {
                body = fireA.Body;
                Assert(body && body.Live && fireA.StillActive, "Beam body did not come alive");
                Assert(body.NodeCount >= 2 && body.Length > 3f, "Beam did not grow: " + body.Length);
                Assert(Vector2.Distance(body.Tip, fireA.EndBeam.transform.position) < .2f, "Head is not on the spline");
                Assert(arm.GetComponent<FreezeBehaviour>(), "Firing arm is not frozen");
                armBefore = body.Root; startRoot = BeamBody.Angle(body.RootDirection);
                Log("Free beam: length " + body.Length + ", nodes " + body.NodeCount + ", bow " + BeamBow(body));
            })) yield break;
            yield return BeamShot("10-free-straight.png");
            float bow = 0f, moved = 0f;
            for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime)
            {
                arm.AddTorque(arm.inertia * 900f * .12f, ForceMode2D.Impulse);
                yield return new WaitForFixedUpdate();
                bow = Mathf.Max(bow, BeamBow(body)); moved = Mathf.Max(moved, Vector2.Distance(armBefore, body.Root));
            }
            yield return BeamShot("11-free-steady.png");
            if (!Check(() =>
            {
                float turned = Mathf.Abs(Mathf.DeltaAngle(startRoot, BeamBody.Angle(body.RootDirection)));
                Log("Free beam under a shove: hand turned " + turned + " deg, bow " + bow + ", root moved " + moved);
                Assert(turned < .2f && moved < .02f, "The frozen hand moved");
                Assert(bow < .05f, "A free beam is shaking: bow " + bow);
                BeamFiniteBodies("free beam");
                BeamStop(fireA);
            })) yield break;
            yield return new WaitForSeconds(.3f);
            if (!Check(() =>
            {
                Assert(!arm.GetComponent<FreezeBehaviour>(), "The arm stayed frozen after the beam");
                Log("PASS the firing arm is frozen and the beam stays straight");
            })) yield break;
        }
        IEnumerator VerifyTwoHands()
        {
            float xBefore = 0f, healthBefore = 0f;
            if (!Check(() => { BeamReset(-14f, 8f, 1.05f); BeamCamera(new Vector2(-2f, beamFloor + 5f), 12f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() =>
            {
                Log("Two hands: ratio " + BeamPhysics.Ratio(beamB, fireA));
                xBefore = DbpuFx.Centre(beamB).x; healthBefore = BeamHealth(beamB);
                BeamRelease(beamA, fireA, "Kamehameha");
            })) yield break;
            KiGuard guard = null;
            float wait = 0f;
            while (wait < 2.5f && (guard == null || guard.Current != KiGuard.Stage.Hold))
            {
                yield return null; wait += Time.unscaledDeltaTime;
                guard = BeamPhysics.GuardOf(beamB);
            }
            if (!Check(() =>
            {
                Log("After wait: guard " + (guard == null ? "none" : guard.Current + " " + guard.Result) + ", beam live " + (fireA.Body && fireA.Body.Live) + ", firing " + fireA.Firing + ", tip " + (fireA.Body ? fireA.Body.Tip : Vector2.zero) + ", tangent " + (fireA.Body ? fireA.Body.TipTangent : Vector2.zero) + ", length " + (fireA.Body ? fireA.Body.Length : 0f) + ", fighter " + DbpuFx.Centre(beamB) + ", radius " + BeamPhysics.BodyRadius(beamB) + ", active " + BeamBody.Active.Count);
                Assert(guard != null && guard.Current == KiGuard.Stage.Hold, "The fighter never caught the beam");
                Assert(guard.Grip == KiGuard.Kind.TwoHands, "Evenly matched fighter did not brace with both hands: " + guard.Grip);
                Assert(fireA.Body.Pinned && fireA.HeldByFighter, "Caught beam is not pinned");
                Log("Caught after " + wait + " s at " + guard.HoldPoint);
            })) yield break;
            yield return BeamShot("20-two-hands-catch.png");
            yield return BeamShot("21-two-hands-slide.png", .45f);
            yield return new WaitForSeconds(1.1f);
            yield return BeamShot("22-two-hands-hold.png");
            if (!Check(() => { Vector2 c = DbpuFx.Centre(beamB); BeamCamera(new Vector2(c.x - beamX - 1f, c.y + .8f), 3.6f); })) yield break;
            yield return BeamShot("22b-two-hands-close.png");
            int frames = 0; float seconds = 0f;
            while (frames < 90) { yield return null; frames++; seconds += Time.unscaledDeltaTime; }
            if (!Check(() => { Log("Frame time during a hold: " + (seconds / frames * 1000f).ToString("F1") + " ms"); BeamCamera(new Vector2(-2f, beamFloor + 5f), 12f); })) yield break;
            float arm = 0f;
            if (!Check(() =>
            {
                Assert(guard.Current == KiGuard.Stage.Hold, "Hold ended too early: " + guard.Result);
                var fore = beamB.Limbs.First(l => l.name == "LowerArmFront");
                Vector2 pointing = -(Vector2)fore.transform.up;
                arm = Vector2.Dot(pointing, -guard.Beam.Body.TipTangent);
                float gap = Vector2.Distance(fireA.Body.Tip, guard.HoldPoint);
                Log("Holding: slid " + guard.Slid + ", arm line-up " + arm + ", tip gap " + gap + ", strain " + guard.StrainNow + ", health " + healthBefore + " -> " + BeamHealth(beamB) + ", ki " + SingularityFighters.KiFraction(beamB) + ", bow " + BeamBow(fireA.Body));
                Assert(gap < .3f, "Head drifted off the hands");
                Assert(Mathf.Abs(DbpuFx.Centre(beamB).x - xBefore) > .5f, "Fighter did not slide");
                Assert(arm > .5f, "Arm is not reaching for the beam");
                Assert(BeamHealth(beamB) > healthBefore - .25f, "Holder is taking the beam's full damage");
                BeamFiniteBodies("hold");
            })) yield break;
            // Hand position: shove the holder upward and the head must go with the hands.
            Vector2 beforeShove = Vector2.zero;
            if (!Check(() => { beforeShove = guard.HoldPoint; SingularityFighters.Push(beamB, Vector2.up * 16f); })) yield break;
            yield return new WaitForSeconds(.14f);
            if (!Check(() =>
            {
                Log("Hands moved " + Vector2.Distance(beforeShove, guard.HoldPoint) + "; bow " + BeamBow(fireA.Body));
                Assert(Vector2.Distance(fireA.Body.Tip, guard.HoldPoint) < .3f, "Head did not follow the hands");
            })) yield break;
            yield return BeamShot("23-two-hands-bent.png");
            // Stronger now: it must end by driving the beam home.
            if (!Check(() => { beamB.GetComponent<MultipliersAndStuffs>().BoostMultiplier = 1.5f; })) yield break;
            float healthA = BeamHealth(beamA);
            wait = 0f;
            while (wait < 12f && guard.Current == KiGuard.Stage.Hold) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("24-two-hands-heave.png", .12f);
            wait = 0f;
            while (wait < 4f && guard.Current != KiGuard.Stage.Done) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("25-two-hands-blast.png", .05f);
            if (!Check(() =>
            {
                Log("Hold outcome: " + guard.Result + ", progress " + guard.PushProgress + ", attacker health " + healthA + " -> " + BeamHealth(beamA) + ", attacker firing " + fireA.Firing);
                Assert(guard.Result == KiGuard.Outcome.PushedBack, "Expected the beam to be pushed back, got " + guard.Result);
                Assert(!fireA.Firing, "Beam kept firing after being driven home");
                Assert(BeamHealth(beamA) < healthA, "Beam driven home did not hurt its owner");
                BeamFiniteBodies("heave");
                Log("PASS both hands, sliding, spline pinned to the hands, ki-limited hold, pushed home");
            })) yield break;
            yield return new WaitForSeconds(1f);
            if (!Check(() => BeamStop(fireA))) yield break;
            yield return new WaitForSeconds(.3f);
        }

        IEnumerator VerifyOneHand()
        {
            float healthBefore = 0f;
            if (!Check(() => { BeamReset(-14f, 8f, 3.2f); BeamCamera(new Vector2(-2f, beamFloor + 5f), 12f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() =>
            {
                Log("One hand: ratio " + BeamPhysics.Ratio(beamB, fireA));
                healthBefore = BeamHealth(beamB);
                BeamRelease(beamA, fireA, "Galick Gun");
            })) yield break;
            KiGuard guard = null; float wait = 0f;
            while (wait < 2.5f && (guard == null || guard.Current != KiGuard.Stage.Hold)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            if (!Check(() =>
            {
                Assert(guard != null && guard.Current == KiGuard.Stage.Hold, "The stronger fighter never caught the beam");
                Assert(guard.Grip == KiGuard.Kind.OneHand, "Significantly stronger fighter did not catch one-handed: " + guard.Grip);
            })) yield break;
            yield return BeamShot("30-one-hand-catch.png", .1f);
            yield return BeamShot("31-one-hand-hold.png", .8f);
            wait = 0f;
            while (wait < 14f && guard.Current != KiGuard.Stage.Done) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("32-one-hand-crush.png", .02f);
            if (!Check(() =>
            {
                Log("One hand outcome: " + guard.Result + " after " + guard.Age + " s, health " + healthBefore + " -> " + BeamHealth(beamB) + ", ki " + SingularityFighters.KiFraction(beamB));
                Assert(guard.Result == KiGuard.Outcome.Crushed, "Expected a crushed beam, got " + guard.Result);
                Assert(!fireA.Firing, "Crushed beam kept firing");
                Assert(BeamHealth(beamB) > healthBefore - .05f, "Barely strained fighter was hurt");
                Assert(SingularityFighters.KiFraction(beamB) > .15f, "One-handed crush cost nearly all ki");
                Log("PASS one hand, crushed");
            })) yield break;
            yield return new WaitForSeconds(1f);
            if (!Check(() => BeamStop(fireA))) yield break;
            yield return new WaitForSeconds(.3f);
        }

        IEnumerator VerifySwat(bool sky)
        {
            float healthBefore = 0f; BeamBolt bolt = null; Vector2 launched = Vector2.zero;
            string name = sky ? "40-swat-sky" : "45-swat-ground";
            if (!Check(() => { BeamReset(-14f, 8f, 30f); BeamPhysics.SwatPreference = sky ? 1 : -1; BeamCamera(new Vector2(-2f, beamFloor + (sky ? 10f : 6f)), sky ? 19f : 13f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() =>
            {
                Log("Swat: ratio " + BeamPhysics.Ratio(beamB, fireA));
                healthBefore = BeamHealth(beamB);
                BeamRelease(beamA, fireA, sky ? "Kamehameha" : "Final Flash");
            })) yield break;
            KiGuard guard = null; float wait = 0f;
            while (wait < 3f && (guard == null || guard.Current != KiGuard.Stage.Strike)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            if (!Check(() =>
            {
                Assert(guard != null && guard.Current == KiGuard.Stage.Strike, "The overwhelming fighter never swatted the beam");
                Assert(guard.Grip == KiGuard.Kind.Swat, "Expected a swat: " + guard.Grip);
                Assert(!fireA.Firing, "The attacker is still firing after the swat");
                Assert(BeamBolt.All.Count == 1, "The swat launched " + BeamBolt.All.Count + " bolts");
                bolt = BeamBolt.All[0]; launched = bolt.Position;
                Log("Swatted: bolt heading " + BeamBody.Angle(bolt.Direction) + " deg from " + launched);
                if (sky) Assert(bolt.Direction.y > .3f, "Sky swat is not heading up");
                else Assert(bolt.Direction.y < -.3f, "Ground swat is not heading down");
            })) yield break;
            yield return BeamShot(name + "-strike.png", .04f);
            yield return BeamShot(name + "-flight.png", .25f);
            if (!Check(() => { if (sky) { Assert(bolt, "Sky bolt burst early"); Assert(bolt.Position.y > launched.y + 4f, "Sky bolt is not flying away: " + bolt.Position); } })) yield break;
            yield return new WaitForSeconds(.5f);
            yield return BeamShot(name + "-after.png");
            float fading = 0f;
            while (fading < 7f && (bolt || fireA.StillActive)) { yield return null; fading += Time.deltaTime; }
            yield return BeamShot(name + "-late.png");
            if (!Check(() =>
            {
                Assert(!bolt, "The bolt never ended");
                Log("Beam gone " + fading + " s after the last shot");
                Assert(!fireA.Firing && !fireA.StillActive, "The beam kept running after the swat");
                Assert(BeamHealth(beamB) >= healthBefore - .01f, "Swatting hurt the fighter");
                BeamFiniteBodies("swat");
                Log("PASS swat " + (sky ? "into the sky" : "into the ground") + ": attacker cut off, head flies away, health " + healthBefore + " -> " + BeamHealth(beamB));
            })) yield break;
            yield return new WaitForSeconds(.3f);
        }
        IEnumerator VerifyKiOut()
        {
            float healthBefore = 0f;
            if (!Check(() => { BeamReset(-14f, 8f, 1f); beamKiB.currentKi = 90f; BeamCamera(new Vector2(-2f, beamFloor + 5f), 12f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() =>
            {
                Log("Ki out: ratio " + BeamPhysics.Ratio(beamB, fireA) + " at 9% ki");
                beamKiB.currentKi = 90f;
                healthBefore = BeamHealth(beamB);
                BeamRelease(beamA, fireA, "Kamehameha");
            })) yield break;
            KiGuard guard = null; float wait = 0f;
            while (wait < 2.5f && (guard == null || guard.Current != KiGuard.Stage.Hold)) { yield return null; wait += Time.unscaledDeltaTime; guard = BeamPhysics.GuardOf(beamB); }
            if (!Check(() => Assert(guard != null && guard.Current == KiGuard.Stage.Hold, "Low-ki fighter never caught the beam"))) yield break;
            wait = 0f;
            while (wait < 8f && guard.Current != KiGuard.Stage.Done) { yield return null; wait += Time.unscaledDeltaTime; }
            yield return BeamShot("50-ki-out.png", .08f);
            yield return new WaitForSeconds(.5f);
            if (!Check(() =>
            {
                Log("Ki out outcome: " + guard.Result + " after " + guard.Age + " s, health " + healthBefore + " -> " + BeamHealth(beamB) + ", ki " + SingularityFighters.KiFraction(beamB));
                Assert(guard.Result == KiGuard.Outcome.KiOut || guard.Result == KiGuard.Outcome.Overwhelmed, "Expected the hold to break, got " + guard.Result);
                Assert(!fireA.Body.Pinned && !fireA.HeldByFighter, "Broken hold left the beam pinned");
                Assert(BeamHealth(beamB) < healthBefore, "The beam did not hit the fighter once the hold broke");
                Log("PASS ki runs out, the beam breaks through and lands");
                BeamStop(fireA);
            })) yield break;
            yield return new WaitForSeconds(.3f);
        }

        IEnumerator VerifyWeakHit()
        {
            float healthBefore = 0f;
            if (!Check(() => { BeamReset(-14f, 8f, .1f); BeamCamera(new Vector2(-2f, beamFloor + 5f), 12f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() =>
            {
                Log("Weak: ratio " + BeamPhysics.Ratio(beamB, fireA));
                healthBefore = BeamHealth(beamB);
                BeamRelease(beamA, fireA, "Kamehameha");
            })) yield break;
            yield return new WaitForSeconds(.9f);
            yield return BeamShot("55-weak-hit.png");
            if (!Check(() =>
            {
                Assert(BeamPhysics.GuardOf(beamB) == null, "A far weaker fighter tried to catch the beam");
                Assert(BeamHealth(beamB) < healthBefore, "The weak fighter was not hurt");
                Log("PASS a far weaker fighter just gets hit: health " + healthBefore + " -> " + BeamHealth(beamB));
                BeamStop(fireA);
            })) yield break;
            yield return new WaitForSeconds(.3f);
        }

        IEnumerator VerifyClash()
        {
            if (!Check(() =>
            {
                BeamReset(-20f, 20f, 1.7f); BeamCamera(new Vector2(0f, beamFloor + 5f), 16f);
                // These fighters would tear open a Reality Rift; this test is about the struggle itself.
                var cooldown = typeof(RealityRift).GetField("cooldownUntil", BindingFlags.NonPublic | BindingFlags.Static);
                Assert(cooldown != null, "Reality Rift cooldown field missing");
                cooldown.SetValue(null, float.MaxValue);
            })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            if (!Check(() => { BeamRig.For(beamB).Face(-1f); Log("Turned B round: centre " + DbpuFx.Centre(beamB) + ", scale " + beamB.transform.localScale.x); })) yield break;
            yield return BeamShot("59-turned.png", .3f);
            yield return BeamAim(beamB, Vector2.left);
            if (!Check(() =>
            {
                BeamRelease(beamA, fireA, "Kamehameha");
                BeamRelease(beamB, fireB, "Galick Gun");
            })) yield break;
            BeamDuel duel = null; float wait = 0f;
            while (wait < 3f && duel == null) { yield return null; wait += Time.unscaledDeltaTime; duel = BeamDuel.Of(fireA); }
            if (!Check(() =>
            {
                Assert(duel != null, "Head-on beams did not lock into a struggle: A dir " + fireA.BeamDirection + " firing " + fireA.Firing + ", B dir " + fireB.BeamDirection + " firing " + fireB.Firing + ", B torso " + BeamRig.For(beamB).CoreRotation);
                Assert(fireA.Body.Pinned && fireB.Body.Pinned, "Clashing beams are not pinned together");
                Log("Duel at " + duel.Point);
            })) yield break;
            yield return BeamShot("60-clash-start.png", .06f);
            yield return BeamShot("61-clash-build.png", .9f);
            Vector2 mid = Vector2.zero;
            if (!Check(() =>
            {
                mid = duel.Point;
                Assert(Vector2.Distance(fireA.Body.Tip, duel.Point) < .05f && Vector2.Distance(fireB.Body.Tip, duel.Point) < .05f, "Heads are not on the clash point");
                // Lift one fighter: both columns must bend to keep meeting.
                SingularityFighters.Push(beamA, Vector2.up * 34f);
            })) yield break;
            yield return new WaitForSeconds(.35f);
            yield return BeamShot("62-clash-bent.png");
            if (!Check(() =>
            {
                Log("Moved one fighter: duel " + mid + " -> " + duel.Point + ", bow A " + BeamBow(fireA.Body) + ", bow B " + BeamBow(fireB.Body) + ", balance " + duel.Balance);
                BeamFiniteBodies("duel");
            })) yield break;
            wait = 0f;
            while (wait < 26f && BeamDuel.Of(fireA) != null && fireA.Firing && fireB.Firing) { yield return null; wait += Time.unscaledDeltaTime; if (Mathf.Abs(wait - 3f) < .02f) yield return BeamShot("63-clash-peak.png"); }
            yield return BeamShot("64-clash-end.png", .05f);
            if (!Check(() =>
            {
                Log("Duel ended after " + wait + " s: A firing " + fireA.Firing + ", B firing " + fireB.Firing + ", A health " + BeamHealth(beamA));
                Assert(BeamDuel.All.Count == 0, "Duel never ended");
                Assert(fireB.Firing || !fireA.Firing, "The weaker side won a 1.7x struggle");
                Log("PASS head-on clash is a force balance: bends with the fighters and the stronger side wins");
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            yield return BeamShot("65-clash-after.png");
            if (!Check(() => BeamStop(fireA, fireB))) yield break;
            yield return new WaitForSeconds(.5f);
        }

        // Every technique has to survive the new body and look like itself.
        IEnumerator VerifyStyles()
        {
            string[] effects = { "Energy Wave", "Kamehameha", "Rose Kamehameha", "Full Force Kamehameha", "Galick Gun", "Final Flash", "Last Emperor", "Gigantic Full Blast", "Special Beam Cannon" };
            int index = 70;
            foreach (string effect in effects)
            {
                if (!Check(() => { BeamReset(-18f, 90f, 1f); BeamCamera(new Vector2(-4f, beamFloor + 5f), 13f); })) yield break;
                yield return BeamAim(beamA, Vector2.right, .7f);
                if (!Check(() => BeamRelease(beamA, fireA, effect))) yield break;
                yield return BeamShot((index++) + "-" + effect.Replace(" ", "") + ".png", .75f);
                if (!Check(() =>
                {
                    Assert(fireA.Body && fireA.Body.Live, effect + " has no live body");
                    Assert(fireA.Body.Length > 5f, effect + " did not grow: " + fireA.Body.Length);
                    BeamFiniteBodies(effect);
                    Log("STYLE " + effect + ": length " + fireA.Body.Length + ", width " + fireA.Body.Width);
                    BeamStop(fireA);
                })) yield break;
                yield return new WaitForSeconds(.25f);
            }
            if (!Check(() => Log("PASS every technique fires on the new body"))) yield break;
        }

        // A Death Beam has no head to catch, but a strong enough hand still sends it elsewhere.
        IEnumerator VerifyRays()
        {
            if (!Check(() => { BeamReset(-10f, 4f, 30f); BeamCamera(new Vector2(-2f, beamFloor + 5f), 11f); })) yield break;
            yield return BeamAim(beamA, Vector2.right);
            StraightBeamBehaviour ray = null; FieldInfo deflected = null;
            float healthBefore = 0f;
            if (!Check(() =>
            {
                ray = beamA.GetComponent<StraightBeamBehaviour>() ?? beamA.gameObject.AddComponent<StraightBeamBehaviour>(); ray.EffectID = "Death Beam";
                
                deflected = typeof(StraightBeamBehaviour).GetField("DeathBeamWasDeflected", BindingFlags.NonPublic | BindingFlags.Instance);
                healthBefore = BeamHealth(beamB);
                
                
            })) yield break;
            yield return new WaitForSeconds(.1f);
            if (!Check(() =>
            {
                var fire = typeof(StraightBeamBehaviour).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert(fire != null, "Death Beam fire method missing");
                ray.StartCoroutine((IEnumerator)fire.Invoke(ray, new object[] { BeamRig.For(beamA).Limb("LowerArmFront").transform }));
            })) yield break;
            yield return BeamShot("85-ray-deflect.png", DBPU.ExtraCharge ? 2.05f : .12f);
            if (!Check(() =>
            {
                Assert((bool)deflected.GetValue(ray), "Overwhelming fighter did not deflect the ray");
                Assert(BeamHealth(beamB) >= healthBefore - .001f, "Deflected ray hurt the fighter");
                Log("PASS instant rays are met by the hand");
            })) yield break;
            yield return new WaitForSeconds(.5f);
        }

        // With the module off, everything must be exactly the old system again.
        IEnumerator VerifyModuleOff()
        {
            if (!Check(() =>
            {
                BeamReset(-14f, 40f, 1f);
                Singularity.SetEnabled(BeamPhysics.ModuleId, false);
            })) yield break;
            yield return BeamAim(beamA, Vector2.right, .7f);
            if (!Check(() => BeamRelease(beamA, fireA, "Kamehameha"))) yield break;
            yield return new WaitForSeconds(.6f);
            if (!Check(() =>
            {
                Assert(beamA.GetComponentsInChildren<FreezeBehaviour>().Any(), "Legacy mode did not freeze the firing arm");
                Assert(fireA.MiddleBeam && fireA.MiddleBeam.enabled && fireA.MiddleBeam.positionCount == 2, "Legacy mode did not keep the straight line");
                Assert(!fireA.Body || !fireA.Body.Live, "Module off but the body is live");
                BeamStop(fireA);
                Singularity.SetEnabled(BeamPhysics.ModuleId, true);
                Log("PASS module off restores the old straight beams");
            })) yield break;
            yield return new WaitForSeconds(.3f);
        }
    }
}
