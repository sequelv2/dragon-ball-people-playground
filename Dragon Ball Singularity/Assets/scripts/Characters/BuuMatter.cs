using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Buu's interior is the same magic rubber as his skin: no human blood or skeleton.
    public sealed class BuuMatterLiquid : Liquid
    {
        public BuuMatterLiquid(Color colour) { Color = colour; }
        public override string GetDisplayName() { return "Majin matter"; }
        public override void OnEnterLimb(LimbBehaviour limb) { }
        public override void OnEnterContainer(BloodContainer container) { }
        public override void OnExitContainer(BloodContainer container) { }
    }

    public static class BuuMatter
    {
        const string PinkId = "DBS_MAJIN_MATTER", GreyId = "DBS_EVIL_MAJIN_MATTER";
        static Texture2D pink, grey;
        static readonly Dictionary<Sprite, Sprite> pinkFragments = new Dictionary<Sprite, Sprite>();
        static readonly Dictionary<Sprite, Sprite> greyFragments = new Dictionary<Sprite, Sprite>();

        public static void Register()
        {
            if (!Liquid.HasID(PinkId)) Liquid.Register(PinkId, new BuuMatterLiquid(BuuPalette.PinkLiquid));
            if (!Liquid.HasID(GreyId)) Liquid.Register(GreyId, new BuuMatterLiquid(BuuPalette.GreyLiquid));
        }

        static Texture2D Tissue(bool evil)
        {
            var existing = evil ? grey : pink;
            if (existing) return existing;
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = evil ? "DBS evil Majin interior" : "DBS Majin interior";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[64];
            Color baseColour = evil ? BuuPalette.GreyTissue : BuuPalette.PinkTissue;
            for (int i = 0; i < pixels.Length; i++)
            {
                float shade = .92f + ((i * 13 + i / 8 * 7) % 9) * .01f;
                pixels[i] = new Color(baseColour.r * shade, baseColour.g * shade, baseColour.b * shade, 1);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            if (evil) grey = texture; else pink = texture;
            return texture;
        }

        static Sprite FragmentSprite(Sprite source, bool evil)
        {
            var cache = evil ? greyFragments : pinkFragments;
            Sprite result;
            if (cache.TryGetValue(source, out result) && result) return result;
            var rect = source.rect;
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width)), height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var pixels = new Color[width * height];
            Color[] mask = null;
            if (source.texture && source.texture.isReadable)
                mask = source.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
            Color tissue = evil ? BuuPalette.GreyTissue : BuuPalette.PinkTissue;
            for (int i = 0; i < pixels.Length; i++)
            {
                float shade = .92f + ((i * 13 + i / width * 7) % 9) * .01f;
                pixels[i] = new Color(tissue.r * shade, tissue.g * shade, tissue.b * shade, mask == null ? 1f : mask[i].a);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "DBS Majin tissue shard texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            result = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height), source.pixelsPerUnit);
            result.name = "DBS Majin tissue shard";
            cache[source] = result;
            return result;
        }

        // Skin/costume replacement rebuilds native materials. Restore only the
        // Majin interior; do not replace the costume, liquids or character settings.
        public static void ReapplyInterior(PersonBehaviour person)
        {
            if (!person) return;
            bool evil = BuuPalette.IsEvil(person);
            var texture = Tissue(evil);
            foreach (var limb in person.Limbs)
            {
                if (!limb || !limb.SkinMaterialHandler || !limb.SkinMaterialHandler.renderer) continue;
                var renderer = limb.SkinMaterialHandler.renderer;
                renderer.material.SetTexture("_FleshTex", texture);
                renderer.material.SetTexture("_BoneTex", texture);
                // Native crushing otherwise uses cached human bone/flesh sprites
                // even when the visible material has already become Majin rubber.
                var fragments = limb.GetComponent<ShatteredObjectSpriteInitialiser>();
                if (fragments && renderer.sprite)
                {
                    var flesh = FragmentSprite(renderer.sprite, evil);
                    if (fragments.BoneGenerator)
                    {
                        fragments.BoneGenerator.Sprite = flesh;
                        if (fragments.FleshGenerator)
                            fragments.BoneGenerator.PartsPhysicalProperties = fragments.FleshGenerator.PartsPhysicalProperties;
                    }
                    if (fragments.FleshGenerator) fragments.FleshGenerator.Sprite = flesh;
                }
            }
        }

        public static void Configure(PersonBehaviour person)
        {
            if (!person) return;
            Register();
            var controller = person.GetComponentInChildren<HumanController>();
            if (controller)
            {
                // Rubber can be perforated and scorched even when the fighter's
                // enormous combat health makes ordinary bullets insignificant.
                controller.Features.ImmuneToBullets = false;
                controller.Features.ImmuneToStabbing = false;
                controller.Features.ImmuneToFire = false;
                controller.Features.ImmuneToAcid = false;
            }
            bool evil = BuuPalette.IsEvil(person);
            string id = evil ? GreyId : PinkId;
            foreach (var limb in person.Limbs)
            {
                if (!limb) continue;
                limb.ShotDamageMultiplier = 1f;
                limb.BloodLiquidType = id;
                var circulation = limb.CirculationBehaviour;
                if (circulation)
                {
                    circulation.ClearLiquid();
                    circulation.AddLiquid(Liquid.GetLiquid(id), 1f);
                }
            }
            ReapplyInterior(person);
            if (evil)
            {
                person.SetBloodColour(137, 128, 145);
                person.SetBruiseColor(102, 96, 110);
                person.SetSecondBruiseColor(122, 113, 130);
                person.SetThirdBruiseColor(147, 138, 155);
                person.SetRottenColour(123, 117, 132);
            }
            else
            {
                person.SetBloodColour(238, 127, 177);
                person.SetBruiseColor(178, 72, 121);
                person.SetSecondBruiseColor(214, 100, 150);
                person.SetThirdBruiseColor(239, 147, 188);
                person.SetRottenColour(224, 121, 163);
            }
        }
    }
}
