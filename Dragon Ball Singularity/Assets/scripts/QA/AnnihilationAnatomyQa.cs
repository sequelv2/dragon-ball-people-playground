using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace DBPU
{
    public sealed partial class AnnihilationQa
    {
        IEnumerator VerifyAnatomicalConsequences()
        {
            PersonBehaviour body = null;
            LimbBehaviour arm = null, spare = null;
            if (!Check(() => body = Spawn("[DBPU] (BM) Goku", 235f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => { arm = Part(body, "LowerArmFront"); spare = Part(body, "LowerArm"); })) yield break;
            float deadline = Time.time + 8f;
            while (!Gone(arm) && !arm.IsDismembered && Time.time < deadline)
            {
                if (!Check(() => Dose(arm, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
            }
            // Native Destroy(hinge) and root-graph propagation settle on the
            // following frame; do not confuse a stale node cache with a joint.
            yield return new WaitForFixedUpdate();
            yield return null;
            if (!Check(() =>
            {
                var injury = arm.GetComponent<EnergyErosion>();
                Log("ANATOMICAL ARM progress=" + (injury ? injury.Progress : 0f) + " tissue=" +
                    (injury ? injury.TissueLoss : 0f) + " bone=" + (injury ? injury.BoneLoss : 0f) +
                    " detached=" + arm.IsDismembered + " hasJoint=" + arm.HasJoint + " hinge=" + (bool)arm.Joint +
                    " connectedRoot=" + arm.NodeBehaviour.IsConnectedToRoot);
                Assert(injury && injury.FunctionLost && injury.SupportLost && injury.Progress < .99f && !Gone(arm),
                    "Skeletal tissue did not lose support until complete visual deletion");
                Assert(arm.IsDismembered && !arm.HasJoint && !arm.Joint && !arm.NodeBehaviour.IsConnectedToRoot,
                    "Exposed skeletal arm retained a real physical attachment to its body");
                var loose = arm.GetComponent<GoreStringBehaviour>();
                Assert(!loose || !loose.Joint, "Destroyed tissue still held a skeletal limb with a native gore-string joint");
                Assert(arm.Health <= 0f && arm.MotorStrength == 0f && !MCP_Utils.LimbCanUsePowers(arm),
                    "Muscle-free arm retained movement or combat power");
                Assert(body.IsAlive() && spare.Joint && spare.HasJoint && MCP_Utils.LimbCanUsePowers(spare),
                    "Local arm destruction disabled a healthy fighter or unrelated arm");
                Pass("loss of flesh detaches the actual joint before disappearance and disables only the injured arm");
            })) yield break;
            yield return Capture(Part(body, "MiddleBody").transform.position, "09-anatomical-arm-separation");
            yield return new WaitForSeconds(.4f);
            if (!Check(() =>
            {
                Assert(arm.Health <= 0f && arm.MotorStrength == 0f && !MCP_Utils.LimbCanUsePowers(arm),
                    "Ordinary immunity/health recovery revived muscle-free detached tissue");
                Pass("broken contact does not restore muscle function to a skeletal severed limb");
            })) yield break;

            // A mechanical segment retains its exposed frame until that frame
            // itself develops a transverse gap; skin loss alone is insufficient.
            PersonBehaviour mechanical = null;
            LimbBehaviour frame = null;
            if (!Check(() => mechanical = Spawn("[DBPU] (BM) Goku", 255f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => { frame = Part(mechanical, "LowerArmFront"); frame.IsAndroid = true; })) yield break;
            deadline = Time.time + 8f;
            while (!Gone(frame) && Time.time < deadline)
            {
                if (!Check(() => Dose(frame, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
                var erosion = frame.GetComponent<EnergyErosion>();
                if (erosion && erosion.BoneSupportBroken) break;
            }
            if (!Check(() =>
            {
                var injury = frame.GetComponent<EnergyErosion>();
                Assert(injury && injury.BoneSupportBroken && injury.BoneLoss > 0f && !Gone(frame),
                    "Bone/frame gap was not observable before complete annihilation");
                Assert(frame.IsDismembered && !frame.HasJoint && injury.FunctionLost,
                    "Annihilated load-bearing bone/frame still held its limb on the body");
                Pass("a transverse gap in annihilated bone/frame physically breaks limb support");
            })) yield break;

            PersonBehaviour vital = null;
            LimbBehaviour head = null, hand = null;
            KiFistHand fist = null;
            Flight flight = null;
            if (!Check(() => vital = Spawn("[DBPU] (BM) Goku", 275f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                head = vital.Limbs.First(part => part && part.HasBrain);
                hand = Part(vital, "LowerArmFront");
                Assert(KiHandLoadout.GetOrCreate(hand).Equip(KiHandLoadout.FistId), "Could not equip vital-damage test fist");
                fist = KiFistHand.GetOrCreate(hand);
                var ki = vital.GetComponent<KiSystem>();
                ki.maxKi = ki.currentKi = 1000f; ki.baseRegen = 0f;
                Assert(fist.BeginHeldCharge(), "Native fist could not begin charging before vital injury");
                flight = vital.GetComponentInChildren<Flight>();
                if (flight && !flight.IsFlying) flight.Use();
                var beam = vital.GetComponent<BeamBehaviour>();
                beam.Firing = beam.StillActive = true;
                beam.UsedArm = hand.transform;
            })) yield break;
            deadline = Time.time + 8f;
            while (EnergyAnatomy.CanFight(vital) && !Gone(head) && Time.time < deadline)
            {
                if (!Check(() => Dose(head, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
            }
            yield return null;
            if (!Check(() =>
            {
                var injury = head.GetComponent<EnergyErosion>();
                Assert(!Gone(head) && injury && injury.SkeletonVisible && injury.Progress < .99f,
                    "Vital injury only took effect after the entire head vanished");
                Assert(!vital.IsAlive() && vital.Braindead && vital.Consciousness <= 0f && head.Health <= 0f,
                    "A skeletal destroyed head still belonged to a living conscious fighter");
                Assert(!KiCombatMath.ValidStrikeLimb(hand, vital) && !MCP_Utils.LimbCanUsePowers(hand) && hand.MotorStrength == 0f,
                    "A fighter with missing vital tissue can still use a healthy hand to fight");
                Assert(!fist.IsReady && vital.GetComponent<KiSystem>().currentKi == 0f
                    && !vital.GetComponent<BeamBehaviour>().Firing && (!flight || !flight.IsFlying),
                    "Vital destruction failed to end a live beam, flight or Ki strike");
                Pass("skeletal head is fatal before disappearance and stops consciousness, motors, beam, flight and Ki combat");
            })) yield break;
            yield return Capture(head.transform.position, "10-skeletal-head-is-dead");
            if (!Check(() =>
            {
                head.Health = head.InitialHealth;
                vital.Braindead = vital.BrainDamaged = false;
                vital.Consciousness = vital.OxygenLevel = 1f;
                vital.GetComponent<KiSystem>().currentKi = 1000f;
            })) yield break;
            yield return new WaitForFixedUpdate();
            yield return null;
            if (!Check(() =>
            {
                Assert(!vital.IsAlive() && head.Health <= 0f && !MCP_Utils.LimbCanUsePowers(hand) && hand.MotorStrength == 0f,
                    "Healing/immunity flags revived missing vital organs");
                Pass("health and immunity resets cannot revive absent vital tissue");
            })) yield break;

            PersonBehaviour torso = null;
            LimbBehaviour chest = null;
            if (!Check(() => torso = Spawn("[DBPU] (BM) Goku", 295f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => chest = Part(torso, "UpperBody"))) yield break;
            deadline = Time.time + 8f;
            while (EnergyAnatomy.CanFight(torso) && !Gone(chest) && Time.time < deadline)
            {
                if (!Check(() => Dose(chest, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
            }
            if (!Check(() =>
            {
                Assert(!torso.IsAlive() && !EnergyAnatomy.CanFight(torso) && !Gone(chest),
                    "Destroyed vital torso organs did not stop an otherwise unexposed head/body");
                Assert(torso.Limbs.Where(part => part && part.name.Contains("Arm")).All(part => !MCP_Utils.LimbCanUsePowers(part)),
                    "A destroyed ribcage still supports combat by its attached arms");
                Pass("fatal torso tissue loss disables the fighter even with an unexposed head");
            })) yield break;
        }
    }
}
