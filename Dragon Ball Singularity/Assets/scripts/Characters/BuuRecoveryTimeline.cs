namespace DBPU
{
    // Every Majin recovery timing in one place. The stages are physical: matter
    // first flies and lies still, then twitches, crawls together and regrows.
    public static class BuuRecoveryTimeline
    {
        // Damage has to persist this long before a recovery starts, so one hit
        // that the native solver immediately resolves never starts a reform.
        public const float DamageConfirmation = .16f;

        // Rest: ballistic flight, then about a second of stillness.
        public const float RestStillness = 1.0f, RestStillMassFraction = .55f, RestLongest = 2.25f;
        public const float StillSpeedSquared = .2025f, StillSpin = 45f;

        // Quiver: the first living twitches of genuine matter.
        public const float QuiverDuration = .78f, TwitchStagger = .32f, TwitchRamp = .38f;

        // Gather: matter crawls towards the surviving anchor.
        public const float GatherRamp = 1.5f, GatherShortest = 1.25f, GatherLongest = 1.85f;
        public const float GatherMassFraction = .4f, GatherRadiusSquared = 2.25f;

        // Grow: opaque pieces build each missing native part.
        public const float GrowShortest = 3.2f, GrowLongest = 4.0f, GrowPerMetre = .035f;

        // Matter this close to the growing body sinks into it.
        public const float MergeRadius = .42f, LateMergeRadius = .38f, MergeRelease = .95f;

        // The closed body settles with a short rubber shiver.
        public const float AfterShockDuration = .7f;

        public static float GrowDuration(float farthestMatter)
        {
            return UnityEngine.Mathf.Clamp(GrowShortest + farthestMatter * GrowPerMetre, GrowShortest, GrowLongest);
        }
    }
}
