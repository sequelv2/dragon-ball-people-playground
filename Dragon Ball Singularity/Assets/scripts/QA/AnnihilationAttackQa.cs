using System;
using System.Collections;
using UnityEngine;

namespace DBPU
{
    public sealed partial class AnnihilationQa
    {
        // These fixtures enter the real attack callbacks. They intentionally never
        // call EnergyAnnihilation themselves, so missing combat hooks fail the QA.
        IEnumerator VerifyEnergyAttackHooks()
        {
            PersonBehaviour source = null, beamVictim = null, sphereVictim = null;
            BeamBehaviour beam = null;
            BallBehaviour ball = null;
            BeamComponent beamContact = null;
            BallBehaviour.BallComponent sphereContact = null;
            GameObject beamObject = null, sphereObject = null;
            LimbBehaviour beamLimb = null, sphereLimb = null;
            KiShield shield = null;
            bool savedSplit = DBPU.KiDeflectBeamSplitEnabled;
            try
            {
                if (!Check(() =>
                {
                    source = Spawn("[DBPU] (BM) Goku", 140f);
                    beamVictim = Spawn("[DBPU] (BM) Goku", 148f);
                    sphereVictim = Spawn("[DBPU] (BM) Goku", 165f);
                    DBPU.KiDeflectBeamSplitEnabled = false;
                })) yield break;
                yield return new WaitForSeconds(1.1f);
                if (!Check(() =>
                {
                    beamLimb = Part(beamVictim, "LowerArmFront");
                    sphereLimb = Part(sphereVictim, "LowerArmFront");
                    foreach (var collider in beamVictim.GetComponentsInChildren<Collider2D>())
                        collider.enabled = collider.GetComponent<LimbBehaviour>() == beamLimb;
                    foreach (var collider in sphereVictim.GetComponentsInChildren<Collider2D>())
                        collider.enabled = collider.GetComponent<LimbBehaviour>() == sphereLimb;

                    beam = source.GetComponent<BeamBehaviour>();
                    Assert(beam, "Native source has no beam controller");
                    beam.enabled = false;
                    beam.EffectID = "Kamehameha";
                    beam.KiSys = source.GetComponent<KiSystem>();
                    beam.Multipliers = source.GetComponent<MultipliersAndStuffs>();
                    beam.KiSys.currentKi = beam.KiSys.maxKi;
                    beam.DamageOutput = 1f;
                    beam.Damage = CombatPowerController.AttackHealth(source)
                        / Mathf.Max(.0001f, beam.Multipliers.FinalMultiplier) / 2000f;
                    beam.DamageOutput = KiCombatMath.Cap((double)Power(beamLimb, 4f) / BeamPhysics.BeamPower(beam));
                    beam.RealDamage = beamLimb.InitialHealth * .00001f;
                    beam.StartBeamPosition = beamLimb.transform.position + Vector3.left * 10f;
                    beam.BeamDirection = Vector3.right;
                    beam.Distance = 11f;
                    beam.MiddleMinusDistance = 0f;
                    beam.Firing = beam.StillActive = true;
                    beamObject = new GameObject("QA native beam contact field");
                    var line = beamObject.AddComponent<LineRenderer>();
                    line.enabled = false;
                    line.positionCount = 2;
                    line.startWidth = line.endWidth = .18f;
                    line.SetPosition(0, beam.StartBeamPosition);
                    line.SetPosition(1, beam.StartBeamPosition + Vector3.right * 11f);
                    beam.MiddleBeam = line;
                    beamContact = beamObject.AddComponent<BeamComponent>();
                    beamContact.enabled = false;
                    beamContact.beamBehaviour = beam;
                    beamContact.RayDirection = Vector3.right;

                    shield = beamVictim.GetComponent<KiShield>();
                    if (!shield) shield = beamVictim.gameObject.AddComponent<KiShield>();
                    shield.Durability = 1e30f;
                    var energy = beamVictim.GetComponent<KiSystem>();
                    energy.currentKi = energy.maxKi;
                    Assert(shield.TryRaise(), "Native shield fixture could not raise");
                    Physics2D.SyncTransforms();
                    beamContact.Update();
                    Assert(Progress(beamLimb) == 0f && shield.Using,
                        "Beam contact bypassed a surviving native shield");
                    Pass("real beam BoxCast contact is blocked by the native Ki Shield");
                    shield.Lower();
                })) yield break;
                yield return null;
                if (!Check(() =>
                {
                    beamContact.Update();
                    Assert(Progress(beamLimb) > 0f,
                        "Native beam BoxCast/OnHit path did not feed annihilation exposure");
                    Pass("real BeamComponent BoxCast and OnHit accumulate exposure on the touched limb");
                })) yield break;
                float beamProgress = Progress(beamLimb);
                if (!Check(() =>
                {
                    beam.StartBeamPosition += Vector3.up * 12f;
                    beam.MiddleBeam.SetPosition(0, beam.StartBeamPosition);
                    beam.MiddleBeam.SetPosition(1, beam.StartBeamPosition + Vector3.right * 11f);
                })) yield break;
                for (int i = 0; i < 6; i++)
                {
                    yield return null;
                    if (!Check(() => beamContact.Update())) yield break;
                }
                if (!Check(() =>
                {
                    Assert(Progress(beamLimb) <= beamProgress + .0001f,
                        "A beam moved away from its target kept adding exposure");
                    Pass("moving the real beam field away stops further contact exposure");
                    beam.Firing = beam.StillActive = false;

                    ball = source.GetComponent<BallBehaviour>();
                    Assert(ball, "Native source has no sphere controller");
                    ball.enabled = false;
                    ball.Orb = null;
                    ball.EffectID = "Death Ball";
                    ball.KiSys = source.GetComponent<KiSystem>();
                    ball.KiSys.currentKi = ball.KiSys.maxKi;
                    ball.DamageOutput = KiCombatMath.Cap((double)Power(sphereLimb, 4f) / DbpuFx.PowerOf(source));
                    ball.RealDamage = sphereLimb.InitialHealth * .00001f;
                    ball.BallPosition = sphereLimb.transform.position;
                    sphereObject = new GameObject("QA native sphere contact field");
                    sphereObject.layer = 9;
                    sphereObject.transform.position = sphereLimb.transform.position;
                    var trigger = sphereObject.AddComponent<CircleCollider2D>();
                    trigger.isTrigger = true;
                    trigger.radius = .45f;
                    sphereContact = sphereObject.AddComponent<BallBehaviour.BallComponent>();
                    sphereContact.enabled = false;
                    sphereContact.ballBehaviour = ball;
                    sphereContact.User = source.gameObject;
                    sphereContact.RayDirection = Vector3.right;
                    shield = sphereVictim.GetComponent<KiShield>();
                    if (!shield) shield = sphereVictim.gameObject.AddComponent<KiShield>();
                    shield.Durability = 1e30f;
                    var energy = sphereVictim.GetComponent<KiSystem>();
                    energy.currentKi = energy.maxKi;
                    Assert(shield.TryRaise(), "Native sphere shield fixture could not raise");
                    ball.ShieldBarrier = shield;
                    Physics2D.SyncTransforms();
                    sphereContact.OnTriggerStay2D(sphereLimb.GetComponent<Collider2D>());
                    Assert(Progress(sphereLimb) == 0f,
                        "Shield-constrained sphere contact bypassed the native barrier");
                    Pass("native sphere contact honors its surviving shield barrier");
                    shield.Lower();
                    ball.ShieldBarrier = null;
                })) yield break;
                yield return new WaitForFixedUpdate();
                if (!Check(() =>
                {
                    sphereContact.OnTriggerStay2D(sphereLimb.GetComponent<Collider2D>());
                    Assert(Progress(sphereLimb) > 0f,
                        "Native BallComponent trigger contact did not feed annihilation exposure");
                    Pass("real BallComponent trigger/OnHit path accumulates sphere exposure");
                })) yield break;
                yield return Capture(Part(beamVictim, "MiddleBody").transform.position, "08-native-beam-contact");
            }
            finally
            {
                DBPU.KiDeflectBeamSplitEnabled = savedSplit;
                if (shield) shield.Lower();
                if (beam) { beam.Firing = beam.StillActive = false; beam.MiddleBeam = null; }
                if (ball) { ball.Firing = ball.StillActive = false; ball.ShieldBarrier = null; }
                if (beamObject) Destroy(beamObject);
                if (sphereObject) Destroy(sphereObject);
                if (source) Destroy(source.gameObject);
                if (beamVictim) Destroy(beamVictim.gameObject);
                if (sphereVictim) Destroy(sphereVictim.gameObject);
            }
        }
    }
}
