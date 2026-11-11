using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace DBPU
{
    // Shared look of every Singularity menu: midnight panels, Gi-orange accent, ki-cyan highlights.
    public static class DbTheme
    {
        public static readonly Color Void = Hex(0x070A14);
        public static readonly Color Panel = Hex(0x0E1322);
        public static readonly Color PanelTop = Hex(0x161D33);
        public static readonly Color Surface = Hex(0x171E33);
        public static readonly Color SurfaceHover = Hex(0x222B47);
        public static readonly Color SurfacePress = Hex(0x111729);
        public static readonly Color Well = Hex(0x0A0E1A);
        public static readonly Color Stroke = new Color(1f, 1f, 1f, .03f);
        public static readonly Color StrokeStrong = new Color(1f, 1f, 1f, .08f);

        public static readonly Color Orange = Hex(0xFF8A1F);
        public static readonly Color Gold = Hex(0xFFC93C);
        public static readonly Color Ki = Hex(0x38D6FF);
        public static readonly Color Violet = Hex(0xB46BFF);
        public static readonly Color Shenron = Hex(0x4DE38C);
        public static readonly Color Success = Hex(0x4CF09A);
        public static readonly Color Danger = Hex(0xFF4D5E);

        public static readonly Color Text = Hex(0xF3F6FF);
        public static readonly Color TextSoft = Hex(0xB6BFD8);
        public static readonly Color TextMuted = Hex(0x7C86A5);
        public static readonly Color TextOnAccent = Hex(0x120A03);

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
        }

        public static Color Alpha(Color c, float alpha) { c.a = alpha; return c; }
        public static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, t); }
        public static Color Dim(Color c, float t) { var r = Color.Lerp(c, Void, t); r.a = c.a; return r; }
        public static Color Lift(Color c, float t) { var r = Color.Lerp(c, Color.white, t); r.a = c.a; return r; }

        // Accent for a transformation: its ki colour when it has one, otherwise guessed from the aura.
        public static Color FormAccent(CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (stats == null) return Orange;
            if (stats.requiredKiColor.a > .01f && stats.requiredKiColor.maxColorComponent > .1f)
                return Color.Lerp(stats.requiredKiColor, Color.white, .22f);
            string id = (stats.AuraID + " " + stats.ID + " " + stats.Name).ToLowerInvariant();
            if (id.Contains("blue")) return Hex(0x40C7FF);
            if (id.Contains("rose") || id.Contains("ego")) return Hex(0xD666FF);
            if (id.Contains("god") || id.Contains("kaioken")) return Hex(0xFF6150);
            if (id.Contains("ultra") || id.Contains("mui")) return Hex(0xD9E6FF);
            if (id.Contains("ssj") || id.Contains("gold") || id.Contains("super")) return Gold;
            return Orange;
        }

        static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        public static string StripTags(string text) { return string.IsNullOrEmpty(text) ? "" : Tags.Replace(text, ""); }

        static TMP_FontAsset font, displayFont;

        // Clean sans for running text. Both fonts ship with the game; only loaded assets can be
        // found, so a miss falls back to the default and is looked up again next time.
        public static TMP_FontAsset Font
        {
            get
            {
                if (!font) font = FindGameFont("IstokWeb-Regular SDF");
                return font ? font : DefaultFont;
            }
        }

        // True bold cut for titles and numbers, which reads cleaner than a synthesized bold.
        public static TMP_FontAsset DisplayFont
        {
            get
            {
                if (!displayFont) displayFont = FindGameFont("arial SDF bold");
                return displayFont ? displayFont : DefaultFont;
            }
        }

        // The font the editor tools were laid out around; their dense screens have no slack for a wider one.
        public static TMP_FontAsset DenseFont { get { return DefaultFont; } }

        static TMP_FontAsset FindGameFont(string name)
        {
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (asset && asset.name == name) return asset;
            return null;
        }

        static TMP_FontAsset DefaultFont
        {
            get
            {
                var fallback = TMP_Settings.defaultFontAsset;
                if (fallback) return fallback;
                var display = ModAPI.FindSpawnable("Holographic Display");
                return display ? display.Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font : null;
            }
        }
    }

    public enum DbTextStyle { Hero, Title, Heading, Body, Small, Caps, Number }
}
