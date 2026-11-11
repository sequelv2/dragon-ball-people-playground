using System;
using UnityEngine;

namespace DBPU
{
    // One source for every Majin colour. Evil Buu is grey rubber; every other
    // Buu is pink. Nothing here is ki: these are matte material colours.
    public struct BuuPalette
    {
        public static readonly Color PinkMatter = new Color(1f, .43f, .66f), GreyMatter = new Color(.61f, .62f, .63f);
        public static readonly Color PinkTissue = new Color(.93f, .48f, .68f), GreyTissue = new Color(.48f, .46f, .50f);
        public static readonly Color PinkLiquid = new Color(.91f, .43f, .64f), GreyLiquid = GreyTissue;

        public bool Evil;
        // Living surface matter: droplets, tremors and closing wounds.
        public Color Matter;
        // Interior rubber: crumbs, shards and the fragment flesh tint.
        public Color Tissue;
        // Steam escaping the body while it closes.
        public Color Vapour;

        public static bool IsEvil(Component owner)
        {
            return owner && owner.name.IndexOf("Evil", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static BuuPalette For(Component owner)
        {
            bool evil = IsEvil(owner);
            var matter = evil ? GreyMatter : PinkMatter;
            return new BuuPalette
            {
                Evil = evil, Matter = matter, Tissue = evil ? GreyTissue : PinkTissue,
                Vapour = Color.Lerp(matter, new Color(.95f, .89f, .94f), .86f)
            };
        }
    }
}
