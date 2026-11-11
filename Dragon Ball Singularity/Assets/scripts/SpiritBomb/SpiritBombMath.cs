using System;

namespace DBPU
{
    // Ki units are conserved; charge time and donated energy enter the same pool.
    public static class SpiritBombMath
    {
        public const float MinimumCharge = .65f;
        public const float PulseInterval = .4f;
        public const int PulseCount = 8;
        public const float ContainmentDuration = 3.65f;
        static double Clean(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value); }
        public static float ChargeCost(float maximum, float dt, float costMultiplier)
        { return (float)Math.Min(1e30, Clean(maximum) * .018 * Math.Min(.1, Clean(dt)) * Math.Min(100, Clean(costMultiplier))); }
        public static float Radius(float energy, float reference)
        { return (float)Math.Min(64, .55 + 7 * Math.Sqrt(Clean(energy) / Math.Max(1, Clean(reference)))); }
        public static float DamageScale(float energy, float reference)
        { return (float)Math.Min(1e12, 4 + 20 * Math.Pow(Clean(energy) / Math.Max(1, Clean(reference)), .8)); }
        public static float Accept(float offered, float existing)
        { return (float)Math.Min(Clean(offered), Math.Max(0, 1e32 - Clean(existing))); }
        public static float Speed(float radius) { return (float)Math.Max(16, 38 - Math.Min(64, Clean(radius)) * .35); }
        public static float Falloff(float distance, float radius)
        { return (float)Math.Max(.35, 1 - .65 * Math.Min(1, Clean(distance) / Math.Max(.01, Clean(radius)))); }
    }
}
