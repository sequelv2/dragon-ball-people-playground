using System;

namespace DBPU
{
    public struct KiDonationPlan
    {
        public float Restore, Support;
        public float Accepted { get { return Restore + Support; } }
    }

    // Pure accounting used by both the runtime and the conservation regression tests.
    public static class KiEnergyMath
    {
        static float Clean(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value); }
        public static float Step(float dt) { return Math.Min(0.1f, Clean(dt)); }
        public static float DonationBudget(float current, float maximum, float dt)
        {
            maximum = Clean(maximum);
            return Math.Min(Math.Max(0f, Clean(current) - maximum * 0.2f), maximum * 0.075f * Step(dt));
        }
        public static KiDonationPlan PlanDonation(float offered, float missing, float supportSpace)
        {
            missing = Clean(missing); supportSpace = Clean(supportSpace);
            float accepted = Math.Min(Clean(offered), missing + supportSpace);
            float restore = Math.Min(missing, accepted * 0.5f);
            float support = Math.Min(supportSpace, accepted - restore);
            restore += Math.Min(Math.Max(0f, missing - restore), Math.Max(0f, accepted - restore - support));
            return new KiDonationPlan { Restore = restore, Support = support };
        }
        public static float SupportMultiplier(float support, float capacity)
        { return Clean(capacity) > 0f ? Math.Min(1e12f, 1f + 4f * (float)Math.Sqrt((double)Clean(support) / capacity)) : 1f; }
        public static float HealthMultiplier(float support,float maximum)
        { return Clean(maximum) > 0f ? Math.Min(1e12f,1f+2f*(float)Math.Sqrt((double)Clean(support)/maximum)) : 1f; }
        public static float AdvanceCharge(float before, float dt, bool allowOvercharge)
        { return Math.Min(allowOvercharge ? 6f : 3f, Clean(before) + Step(dt)); }
        public static float ChargeCost(float maximum, float before, float after, float costMultiplier)
        {
            before = Math.Min(6f, Clean(before)); after = Math.Min(6f, Math.Max(before, Clean(after)));
            float normal = Math.Min(after, 3f) - Math.Min(before, 3f);
            float extra = Math.Max(0f, after - 3f) - Math.Max(0f, before - 3f);
            return Clean(maximum) * (normal * 0.04f + extra * 0.02f) * Math.Min(100f, Clean(costMultiplier));
        }
    }
}
