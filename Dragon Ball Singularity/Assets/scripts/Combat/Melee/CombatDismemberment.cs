using System;
using UnityEngine;

namespace DBPU
{
    public static class CombatDismemberment
    {
        public const float MinimumImpactSpeed = 8f;
        public const double MinimumPowerRatio = 3d;

        public static double PowerOf(PersonBehaviour person)
        {
            if (!person) return 0d;
            double health = CombatPowerController.AttackHealth(person)
                / Math.Max(.001d, CombatPowerController.KaiokenHealthScale(person));
            var support = person.GetComponent<AllyKiReservoir>();
            double power = health * KiCombatMath.NativeMultiplier(person)
                * (support ? Math.Max(1d, support.PowerMultiplier) : 1d)
                * CombatPowerController.OutputFor(person);
            return double.IsNaN(power) || power <= 0d ? 0d : Math.Min(1e250d, power);
        }

        public static bool TryMeleeSever(PersonBehaviour attacker, LimbBehaviour target, float damage,
            float impactSpeed, Vector2 point, Vector2 direction)
        {
            if (!attacker || !attacker.IsAlive() || !target || !target.Person || target.Person == attacker
                || !target.gameObject.activeInHierarchy || target.ImmuneToDamage || !target.PhysicalBehaviour
                || target.PhysicalBehaviour.isDisintegrated || target.IsDismembered) return false;
            if (!Finite(damage) || !Finite(impactSpeed) || damage <= 0f || impactSpeed < MinimumImpactSpeed) return false;

            float maximumHealth = Mathf.Max(1f, KiCombatMath.Finite(target.InitialHealth, 1e32f));
            float healthFraction = Mathf.Clamp01(KiCombatMath.Finite(target.Health) / maximumHealth);
            // Existing wounds make a limb easier to tear; strength alone never
            // turns a resting contact into a severing strike.
            double minimumDamage = maximumHealth * Mathf.Lerp(.2f, .65f, healthFraction);
            if (damage < minimumDamage) return false;
            double resistance = Math.Max(1d, KiCombatMath.Finite(DbpuLimbDurability.GetResistance(target), 25f));
            double requiredRatio = MinimumPowerRatio * Math.Max(1d, Math.Sqrt(resistance / DbpuLimbDurability.DefaultResistance));
            if (PowerOf(attacker) < Math.Max(1d, PowerOf(target.Person)) * requiredRatio) return false;

            var cut = FindCut(target, point);
            if (!cut || !SeverLimb(cut)) return false;
            var body = cut.GetComponent<Rigidbody2D>();
            if (body && body.bodyType == RigidbodyType2D.Dynamic)
            {
                if (!Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < .001f)
                    direction = (Vector2)cut.transform.position - point;
                if (direction.sqrMagnitude < .001f) direction = Vector2.right;
                body.AddForce(direction.normalized * body.mass * Mathf.Clamp(impactSpeed * .3f, 3f, 14f), ForceMode2D.Impulse);
            }
            return true;
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        static LimbBehaviour FindCut(LimbBehaviour target, Vector2 point)
        {
            if (target.HasJoint && target.Joint && target.Joint.connectedBody) return target;
            // The native torso root has no parent hinge. A hit there can tear
            // its nearest attached segment instead of silently doing nothing.
            var parentBody = target.GetComponent<Rigidbody2D>();
            LimbBehaviour nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var candidate in target.Person.Limbs)
            {
                if (!candidate || candidate.ImmuneToDamage || candidate.IsDismembered || !candidate.HasJoint
                    || !candidate.Joint || candidate.Joint.connectedBody != parentBody) continue;
                float distance = ((Vector2)candidate.transform.TransformPoint(candidate.Joint.anchor) - point).sqrMagnitude;
                if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
            }
            return nearest;
        }

        public static bool SeverLimb(LimbBehaviour target)
        {
            if (!target || !target.Person || target.ImmuneToDamage || target.IsDismembered || !target.HasJoint
                || !target.Joint || !target.Joint.connectedBody || !target.PhysicalBehaviour
                || target.PhysicalBehaviour.isDisintegrated) return false;
            if (DbpuLimbDurability.IsDbpuFighter(target) && !DbpuLimbDurability.CanLoseLimbs(target.Person)) return false;
            using (DbpuLimbDurability.AllowDismemberment(target))
            {
                var joint = target.Joint;
                target.Slice();
                // Native Slice only weakens the hinge. Its break force is
                // deliberately unlimited on DBPU fighters, so complete the
                // native break callback while this exact limb is authorized.
                // It updates circulation, connected nodes, wounds and sound.
                if (target.HasJoint && target.Joint == joint)
                    target.SendMessage("OnJointBreak2D", joint, SendMessageOptions.DontRequireReceiver);
                return target.IsDismembered && !target.HasJoint;
            }
        }
    }
}
