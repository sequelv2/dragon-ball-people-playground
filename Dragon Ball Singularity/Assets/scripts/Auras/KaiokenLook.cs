using UnityEngine;

namespace DBPU
{
    // Standard Kaioken body tint: the classic red flush (base form and Super Saiyan 1/2/3).
    // Over SSJ Blue only, hair keeps its own colour, so Blue hair stays blue.
    public static class KaiokenLook
    {
        public static void ApplyBodyTint(PersonBehaviour person, float multiplier)
        {
            if (!person || person.Limbs == null) return;
            bool layered = CinematicAssets.KaiokenProfile(person) == "KaiokenBlue";
            float m = Mathf.Max(1f, multiplier);
            // Over Blue the body still flushes deep red (as in the anime), only the hair keeps its colour.
            float body = layered ? Mathf.Clamp(.72f - m / 150f, .40f, .72f) : Mathf.Max(0f, .8f - m / 100f);
            float hair = layered ? Mathf.Clamp(.96f - m / 600f, .88f, .96f) : body;
            foreach (var limb in person.Limbs)
            {
                if (!limb) continue;
                foreach (var sr in limb.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!sr || sr.name.StartsWith("DBPU")) continue;
                    float g = IsHair(sr.transform, limb.transform) ? hair : body;
                    sr.color = new Color(1f, g, g, sr.color.a);
                }
            }
        }
        static bool IsHair(Transform node, Transform limb)
        {
            for (; node != null; node = node.parent)
            {
                if (node.name.ToLowerInvariant().Contains("hair")) return true;
                if (node == limb) break;
            }
            return false;
        }
    }
}
