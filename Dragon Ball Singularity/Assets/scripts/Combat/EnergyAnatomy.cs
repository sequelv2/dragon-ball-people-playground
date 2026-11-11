using HarmonyLib;
using UnityEngine;

namespace DBPU
{
    public static class EnergyAnatomy
    {
        public static bool CanFight(PersonBehaviour person)
        {
            var failure = person ? person.GetComponent<EnergyVitalFailure>() : null;
            return !failure || !failure.Fatal;
        }

        public static bool CanUse(LimbBehaviour limb)
        {
            if (!limb || !CanFight(limb.Person)) return false;
            // A healthy hand below a destroyed upper arm also loses its muscles'
            // connection to the body. Follow the real remaining hinge chain.
            var current = limb;
            for (int i = 0; current && i < 24; i++)
            {
                var injury = current.GetComponent<EnergyErosion>();
                if (injury && injury.FunctionLost) return false;
                current = current.Joint && current.Joint.connectedBody
                    ? current.Joint.connectedBody.GetComponent<LimbBehaviour>() : null;
            }
            return true;
        }

        public static void SeverSupport(LimbBehaviour cut)
        {
            if (!cut || !cut.Person) return;
            // Mark descendants before severing updates the native connection graph.
            DisableBranch(cut, 0);
            CombatDismemberment.SeverLimb(cut);
            var looseTissue = cut.GetComponent<GoreStringBehaviour>();
            if (looseTissue) looseTissue.DestroyJoint();
        }

        static void DisableBranch(LimbBehaviour limb, int depth)
        {
            if (!limb || !limb.Person || depth >= 24) return;
            var body = limb.GetComponent<Rigidbody2D>();
            foreach (var child in limb.Person.Limbs)
                if (child && child != limb && child.Joint && child.Joint.connectedBody == body)
                    DisableBranch(child, depth + 1);
            var injury = limb.GetComponent<EnergyErosion>();
            if (!injury) injury = limb.gameObject.AddComponent<EnergyErosion>();
            injury.FunctionLost = true;
            injury.InhibitMovement();
        }
    }

    // Missing organs are not ordinary low health: pain/oxygen/brain-damage immunity
    // cannot bring absent tissue back. A Majin is handled by its own regeneration.
    [DefaultExecutionOrder(330)]
    public sealed class EnergyVitalFailure : MonoBehaviour
    {
        public bool Fatal;
        [SkipSerialisation] PersonBehaviour person;

        public static void Kill(PersonBehaviour person)
        {
            if (!person || person.GetComponent<BuuRegeneration>()) return;
            var failure = person.GetComponent<EnergyVitalFailure>();
            if (!failure) failure = person.gameObject.AddComponent<EnergyVitalFailure>();
            failure.person = person;
            failure.Fatal = true;
            failure.Enforce();
        }

        void Awake() { person = GetComponent<PersonBehaviour>(); }
        void FixedUpdate() { Enforce(); }
        void LateUpdate() { Enforce(); }

        void Enforce()
        {
            if (!Fatal || DbpuFx.Paused || Time.timeScale <= 0f) return;
            if (!person) person = GetComponent<PersonBehaviour>();
            if (!person) return;
            person.Braindead = true;
            person.BrainDamaged = true;
            person.Consciousness = 0f;
            person.OxygenLevel = 0f;
            person.AdrenalineLevel = 0f;
            person.DesiredWalkingDirection = 0f;
            foreach (var part in person.Limbs)
            {
                if (!part) continue;
                if (part.HasBrain) part.Health = 0f;
                if (part.Joint) part.Joint.useMotor = false;
            }
            var ki = person.GetComponent<KiSystem>();
            if (ki) ki.currentKi = 0f;
            var beam = person.GetComponent<BeamBehaviour>();
            if (beam && (beam.Firing || beam.Charging || beam.StillActive)) beam.CancelBeamState();
            var ball = person.GetComponent<BallBehaviour>();
            if (ball && ball.Charging) ball.CancelBallState();
            var blast = person.GetComponent<BlastBehaviour>();
            if (blast && (blast.Firing || blast.IsBarrage))
            { blast.StopAllCoroutines(); blast.Firing = blast.IsBarrage = false; }
            var spirit = person.GetComponent<SpiritBombChannel>();
            if (spirit && spirit.HasPendingBomb) spirit.CancelCharge();
            foreach (var flight in person.GetComponentsInChildren<Flight>())
                if (flight.IsFlying) flight.Use();
        }
    }

    [HarmonyPatch(typeof(PersonBehaviour), "IsAlive")]
    public static class PersonBehaviour_EnergyVitalFailure
    {
        static void Postfix(PersonBehaviour __instance, ref bool __result)
        { if (__result && !EnergyAnatomy.CanFight(__instance)) __result = false; }
    }

    [HarmonyPatch(typeof(LimbBehaviour), "get_MotorStrength")]
    public static class LimbBehaviour_EnergyMuscleLoss
    {
        static void Postfix(LimbBehaviour __instance, ref float __result)
        { if (__result > 0f && !EnergyAnatomy.CanUse(__instance)) __result = 0f; }
    }
}
