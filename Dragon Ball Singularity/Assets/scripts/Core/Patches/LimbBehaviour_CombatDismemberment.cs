using HarmonyLib;
using UnityEngine;

namespace DBPU
{
    // Physical strength remains usable with an empty hand loadout. Native power
    // components handle their own hits; this fallback never doubles those hits.
    [HarmonyPatch(typeof(LimbBehaviour), "OnCollisionEnter2D")]
    public static class LimbBehaviour_CombatDismemberment
    {
        static void Postfix(LimbBehaviour __instance, Collision2D __0)
        {
            if (!__instance || !__instance.isActiveAndEnabled || __instance.Health <= 0f
                || !__instance.Person || !__instance.Person.IsAlive() || __0 == null
                || Time.timeScale <= 0f || DbpuFx.Paused || !KiHandLoadout.IsStrikeLimb(__instance)) return;
            var attacker = __instance.Person;
            if (!attacker.GetComponent<KiSystem>() && !attacker.GetComponent<DbpuCharacterIdentity>()) return;
            var punch = __instance.GetComponent<PunchPower>();
            var kick = __instance.GetComponent<KickPower>();
            var fist = __instance.GetComponent<KiFistHand>();
            if ((punch && punch.isActiveAndEnabled && punch.Active)
                || (kick && kick.isActiveAndEnabled && kick.Active)
                || (fist && fist.isActiveAndEnabled && fist.Equipped)) return;
            var collider = __0.collider;
            if (!collider || collider.isTrigger) return;
            var target = collider.GetComponent<LimbBehaviour>();
            if (!target && collider.attachedRigidbody) target = collider.attachedRigidbody.GetComponent<LimbBehaviour>();
            if (!target || !target.Person || target.Person == attacker) return;
            float speed = __0.relativeVelocity.magnitude;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < CombatDismemberment.MinimumImpactSpeed) return;
            bool foot = KiHandLoadout.IsFoot(__instance);
            double damage = CombatDismemberment.PowerOf(attacker) * (foot ? .02d : .01d)
                * (Mathf.Clamp(speed, 5f, 25f) / 5f)
                * Mathf.Clamp(KiCombatMath.Finite(DBPU.DamageMultiplier, 1f), 0f, 1000f);
            if (target.RoughClassification == LimbBehaviour.BodyPart.Arms) damage *= .5d;
            else if (target.RoughClassification == LimbBehaviour.BodyPart.Legs) damage *= .25d;
            Vector2 point = __0.contactCount > 0 ? __0.GetContact(0).point : (Vector2)target.transform.position;
            Vector2 direction = (Vector2)target.transform.position - (Vector2)__instance.transform.position;
            CombatDismemberment.TryMeleeSever(attacker, target, KiCombatMath.Cap(damage, float.MaxValue), speed, point, direction);
        }
    }
}
