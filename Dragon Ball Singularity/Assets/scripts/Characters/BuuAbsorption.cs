using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    [DefaultExecutionOrder(180)]
    public sealed class BuuAbsorption : MonoBehaviour, Messages.IOnBeforeSerialise, Messages.IOnAfterDeserialise
    {
        public const float Range = 8f;
        public double AddedPower;
        public double StrongestPower;
        public string StrongestName;
        public int AbsorbedCount;
        public OutfitPart[] Outfit;
        public float[] AppliedBonus;
        public double AppliedScale = 1d;

        [Serializable]
        public sealed class OutfitPart
        {
            public string LimbName;
            public string Png;
            public float PivotX, PivotY, PixelsPerUnit;
            public string AccessoryName;
            public Vector3 LocalPosition, LocalScale;
            public float Angle;
            public int SortingOffset;
        }

        sealed class CapturedBody
        {
            public Rigidbody2D Body;
            public bool Simulated;
            public Vector2 Velocity;
            public float Angular;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }
        sealed class CapturedRenderer { public Renderer View; public bool Enabled; }
        static readonly Dictionary<int, BuuAbsorption> claims = new Dictionary<int, BuuAbsorption>();
        [SkipSerialisation] PersonBehaviour person;
        [SkipSerialisation] PersonBehaviour target;
        [SkipSerialisation] int claimId;
        [SkipSerialisation] float age;
        [SkipSerialisation] float cooldown;
        [SkipSerialisation] double pendingPower;
        [SkipSerialisation] string pendingName;
        [SkipSerialisation] OutfitPart[] pendingOutfit;
        [SkipSerialisation] List<CapturedBody> bodies = new List<CapturedBody>();
        [SkipSerialisation] List<CapturedRenderer> renderers = new List<CapturedRenderer>();
        [SkipSerialisation] BuuAbsorptionFx effects;
        [SkipSerialisation] Vector2 victimCentre;
        [SkipSerialisation] Vector2 victimSize;
        [SkipSerialisation] Dictionary<string, Sprite> outfitSprites = new Dictionary<string, Sprite>();
        [SkipSerialisation] Dictionary<string, GameObject> accessories = new Dictionary<string, GameObject>();
        [SkipSerialisation] List<Sprite> retiredSprites = new List<Sprite>();
        [SkipSerialisation] bool menusBound;
        public bool IsAbsorbing { get { return claimId != 0; } }
        public float Progress { get { return IsAbsorbing ? Mathf.Clamp01(age / BuuAbsorptionFx.Duration) : 0f; } }

        public static BuuAbsorption Attach(PersonBehaviour owner)
        {
            var result = owner.GetComponent<BuuAbsorption>() ?? owner.gameObject.AddComponent<BuuAbsorption>();
            result.person = owner;
            return result;
        }

        void Start() { person = GetComponent<PersonBehaviour>(); BindMenus(); RestoreOutfit(); }
        static double Positive(double value, double fallback = 1d)
        { return double.IsNaN(value) || double.IsInfinity(value) || value <= 0d ? fallback : Math.Min(1e30d, value); }

        // Full output and full Ki, independent of temporary suppression/reserve.
        // Native health already contains the current form; replace it with the
        // greatest effective form available to this particular fighter.
        public static double MaximumPower(PersonBehaviour owner)
        {
            if (!owner || owner.Limbs == null) return 0d;
            var mult = owner.GetComponent<MultipliersAndStuffs>();
            double form = mult ? Positive(mult.FormMultiplier) : 1d, best = form;
            var transforms = owner.GetComponent<CharacterTransformations>();
            if (transforms && transforms.transformations != null)
                foreach (var entry in transforms.transformations.TransformationData)
                    if (entry.Value != null) best = Math.Max(best, Positive(transforms.GetEffectivePower(entry.Value)));
            double health = 0d; int count = 0;
            foreach (var limb in owner.Limbs)
                if (limb) { health += Math.Max(0d, KiCombatMath.Finite(limb.InitialHealth)); count++; }
            var support = owner.GetComponent<AllyKiReservoir>();
            double other = OtherPower(mult) * (support ? Positive(support.PowerMultiplier) : 1d);
            return Math.Min(1e30d, health / Math.Max(1, count) / form
                / CombatPowerController.HealthScale(owner) / CombatPowerController.KaiokenHealthScale(owner)
                / (support ? Positive(support.HealthMultiplier) : 1d) * best * other);
        }

        static double OtherPower(MultipliersAndStuffs mult)
        {
            return mult ? Positive(mult.BoostMultiplier) * Positive(mult.KaiokenMultiplier)
                * Positive(mult.RageMultiplier) * Positive(mult.MajinMultiplier)
                * Positive(mult.ExponentialMultiplier) * Positive(mult.OtherMultiplier) : 1d;
        }

        public void ReconcilePower()
        {
            if (!person) person = GetComponent<PersonBehaviour>();
            if (!person || AddedPower <= 0d) return;
            var mult = person.GetComponent<MultipliersAndStuffs>();
            var support = person.GetComponent<AllyKiReservoir>();
            double healthScale = CombatPowerController.HealthScale(person)
                * CombatPowerController.KaiokenHealthScale(person) * (support ? Positive(support.HealthMultiplier) : 1d);
            double scale = healthScale * (mult ? Positive(mult.FormMultiplier) : 1d);
            double bonus = AddedPower * healthScale / Positive(OtherPower(mult)
                * (support ? Positive(support.PowerMultiplier) : 1d));
            if (AppliedBonus == null || AppliedBonus.Length != person.Limbs.Length)
            { AppliedBonus = new float[person.Limbs.Length]; AppliedScale = scale; }
            bool changed = false;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (!limb) continue;
                double previous = AppliedBonus[i] * scale / Positive(AppliedScale);
                double desired = Math.Min(1e32d, Math.Max(1d, limb.InitialHealth - previous) + bonus);
                float value = (float)desired;
                AppliedBonus[i] = KiCombatMath.Cap(bonus);
                if (Math.Abs(value - limb.InitialHealth) <= Math.Max(1d, limb.InitialHealth) * 1e-7d) continue;
                double fraction = limb.InitialHealth > 0f ? Math.Max(0d, (double)limb.Health / limb.InitialHealth) : 0d;
                limb.InitialHealth = value;
                limb.Health = KiCombatMath.Cap(desired * fraction);
                changed = true;
            }
            AppliedScale = scale;
            if (changed) MCP_Utils.InvalidatePersonStatCache(person);
            var combat = person.GetComponent<CombatPowerController>();
            if (combat) combat.Refresh();
        }

        Vector2 Centre(PersonBehaviour owner)
        {
            foreach (var limb in owner.Limbs) if (limb && limb.name == "MiddleBody") return limb.transform.position;
            return owner.transform.position;
        }
        bool OwnerReady()
        {
            if (!person || !person.isActiveAndEnabled || !person.IsAlive()) return false;
            BuuAbsorption captor;
            if (claims.TryGetValue(person.GetInstanceID(), out captor) && captor && captor != this) return false;
            var regen = person.GetComponent<BuuRegeneration>();
            return !regen || !regen.IsReforming;
        }
        public bool CanAbsorb(PersonBehaviour other)
        {
            if (!person) person = GetComponent<PersonBehaviour>();
            if (!OwnerReady() || !other || other == person || !other.gameObject.activeInHierarchy || other.Limbs == null) return false;
            if (Vector2.Distance(Centre(person), Centre(other)) > Range) return false;
            BuuAbsorption claimant;
            if (claims.TryGetValue(other.GetInstanceID(), out claimant) && claimant && claimant != this) return false;
            var absorption = other.GetComponent<BuuAbsorption>();
            if (absorption && absorption.IsAbsorbing) return false;
            foreach (var limb in other.Limbs)
                if (limb && limb.gameObject.activeInHierarchy && limb.PhysicalBehaviour && !limb.PhysicalBehaviour.isDisintegrated) return true;
            return false;
        }
        public PersonBehaviour NearestTarget()
        {
            PersonBehaviour nearest = null; float distance = Range * Range;
            foreach (var other in FindObjectsOfType<PersonBehaviour>())
            {
                if (!CanAbsorb(other)) continue;
                float next = (Centre(other) - Centre(person)).sqrMagnitude;
                if (next <= distance) { nearest = other; distance = next; }
            }
            return nearest;
        }
        string TargetLabel()
        {
            if (IsAbsorbing) return "Cancel absorption";
            var next = NearestTarget();
            return next ? "Absorb " + next.name : "Absorb nearest person (within 8 m)";
        }
        void BindMenus()
        {
            if (menusBound || !person || person.Limbs == null) return;
            foreach (var limb in person.Limbs)
                if (limb && limb.PhysicalBehaviour && limb.PhysicalBehaviour.ContextMenuOptions)
                {
                    var buttons = limb.PhysicalBehaviour.ContextMenuOptions.Buttons;
                    buttons.RemoveAll(button => button.Identity == "buuAbsorb");
                    buttons.Add(new ContextMenuButton("buuAbsorb", TargetLabel,
                        "Super Buu Absorption: add the victim's highest Power Level and wear the strongest absorbed fighter's outfit", () =>
                        { if (IsAbsorbing) CancelAbsorption(); else TryAbsorb(NearestTarget()); }));
                }
            menusBound = true;
        }

        public bool TryAbsorb(PersonBehaviour other)
        {
            if (IsAbsorbing || cooldown > 0f || !CanAbsorb(other) || !BuuAbsorptionFx.AssetsReady) return false;
            // Capture all reviewable data before changing the victim's physics.
            pendingPower = MaximumPower(other);
            if (pendingPower <= 0d) return false;
            pendingOutfit = pendingPower > StrongestPower ? CaptureOutfit(other) : null;
            pendingName = other.name;
            target = other; claimId = other.GetInstanceID(); claims[claimId] = this; age = 0f;
            bodies.Clear(); renderers.Clear();
            Bounds bounds = new Bounds(Centre(other), Vector3.zero);
            foreach (var limb in other.Limbs)
            {
                if (!limb) continue;
                var view = limb.GetComponent<SpriteRenderer>();
                if (view && view.enabled) bounds.Encapsulate(view.bounds);
            }
            victimCentre = bounds.center; victimSize = new Vector2(Mathf.Max(.4f, bounds.size.x), Mathf.Max(.8f, bounds.size.y));
            foreach (var body in other.GetComponentsInChildren<Rigidbody2D>())
            {
                bodies.Add(new CapturedBody { Body = body, Simulated = body.simulated, Velocity = body.velocity, Angular = body.angularVelocity,
                    Position = body.transform.position, Rotation = body.transform.rotation, Scale = body.transform.localScale });
                body.simulated = false;
            }
            foreach (var view in other.GetComponentsInChildren<Renderer>(true))
                renderers.Add(new CapturedRenderer { View = view, Enabled = view.enabled });
            effects = BuuAbsorptionFx.Create(person, target, victimCentre, victimSize);
            return true;
        }

        void Update()
        {
            if (!person) person = GetComponent<PersonBehaviour>();
            BindMenus();
            if (Time.timeScale <= 0f || (Global.main && Global.main.Paused)) return;
            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
            ReconcilePower(); RestoreOutfit();
            if (!HasErosion()) ReleaseRetiredSprites();
            if (!IsAbsorbing) return;
            if (!target || !OwnerReady() || !CanAbsorb(target)) { CancelAbsorption(); return; }
            age += Time.deltaTime;
            if (effects) effects.Tick(age);
            if (effects && effects.CompletelyWrapped) foreach (var view in renderers) if (view.View) view.View.enabled = false;
            if (age >= BuuAbsorptionFx.Duration) CompleteAbsorption();
        }

        void CompleteAbsorption()
        {
            if (!target) { CancelAbsorption(); return; }
            var victim = target;
            AddedPower = Math.Min(1e30d, AddedPower + pendingPower);
            AbsorbedCount++;
            if (pendingPower > StrongestPower)
            {
                StrongestPower = pendingPower; StrongestName = pendingName;
                Outfit = pendingOutfit; ReleaseOutfitSprites();
            }
            // Detached native fragments belong to the absorbed person too.
            foreach (var limb in victim.Limbs)
                if (limb && limb.DestroyWith != null)
                    foreach (var piece in limb.DestroyWith) if (piece) Destroy(piece);
            target = null;
            claims.Remove(claimId); claimId = 0;
            bodies.Clear(); renderers.Clear(); pendingOutfit = null;
            Destroy(victim.gameObject);
            ClearEffects(); cooldown = .4f;
            ReconcilePower(); RestoreOutfit();
        }

        public void CancelAbsorption()
        {
            foreach (var saved in bodies)
                if (saved.Body)
                {
                    saved.Body.transform.position = saved.Position;
                    saved.Body.transform.rotation = saved.Rotation;
                    saved.Body.transform.localScale = saved.Scale;
                    saved.Body.position = saved.Position;
                    saved.Body.rotation = saved.Rotation.eulerAngles.z;
                    saved.Body.simulated = saved.Simulated;
                    saved.Body.velocity = saved.Velocity; saved.Body.angularVelocity = saved.Angular;
                }
            foreach (var saved in renderers) if (saved.View) saved.View.enabled = saved.Enabled;
            if (claimId != 0) claims.Remove(claimId);
            claimId = 0; target = null; pendingOutfit = null;
            bodies.Clear(); renderers.Clear(); ClearEffects();
        }

        void ClearEffects()
        {
            if (!effects) return;
            effects.gameObject.SetActive(false);
            effects.Dispose(); effects = null;
        }

        static Color32[] Read(Sprite sprite)
        {
            var rect = sprite.textureRect;
            int width = (int)rect.width, height = (int)rect.height;
            if (sprite.texture.isReadable)
            {
                var pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, width, height);
                var result = new Color32[pixels.Length];
                for (int i = 0; i < result.Length; i++) result[i] = pixels[i];
                return result;
            }
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(sprite.texture, target, new Vector2(rect.width / sprite.texture.width, rect.height / sprite.texture.height),
                    new Vector2(rect.x / sprite.texture.width, rect.y / sprite.texture.height));
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0); copy.Apply();
                return copy.GetPixels32();
            }
            finally
            { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (copy) Destroy(copy); }
        }
        static bool SkinColour(Color32 color, HashSet<int> skin)
        {
            if (skin.Contains(color.r << 16 | color.g << 8 | color.b)) return true;
            // Body and face art can use slightly different shades of bare skin.
            foreach (int shade in skin)
                if (Math.Abs(color.r - (shade >> 16)) <= 26 && Math.Abs(color.g - ((shade >> 8) & 255)) <= 26
                    && Math.Abs(color.b - (shade & 255)) <= 26) return true;
            return false;
        }
        OutfitPart[] CaptureOutfit(PersonBehaviour donor)
        {
            var skin = new HashSet<int>();
            foreach (var limb in donor.Limbs)
            {
                if (!limb || limb.name != "Head") continue;
                var head = limb.GetComponent<SpriteRenderer>();
                var pixels = Read(head.sprite);
                var counts = new Dictionary<int, int>(); Color32 dominant = Color.white; int maximum = 0;
                foreach (var color in pixels)
                {
                    int hi = Math.Max(color.r, Math.Max(color.g, color.b)), lo = Math.Min(color.r, Math.Min(color.g, color.b));
                    if (color.a < 128 || hi < 70) continue;
                    int key = color.r << 16 | color.g << 8 | color.b, count;
                    counts.TryGetValue(key, out count); counts[key] = ++count;
                    if (count > maximum) { maximum = count; dominant = color; }
                }
                float hue, saturation, value;
                Color.RGBToHSV(dominant, out hue, out saturation, out value);
                foreach (var color in pixels)
                {
                    float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
                    float difference = Mathf.Abs(h - hue); difference = Mathf.Min(difference, 1f - difference);
                    if (color.a > 128 && v > .2f && ((saturation < .12f && s < .12f) ||
                        (saturation >= .12f && s >= .12f && difference <= .045f)))
                        skin.Add(color.r << 16 | color.g << 8 | color.b);
                }
            }
            var parts = new List<OutfitPart>();
            foreach (var own in person.Limbs)
            {
                if (!own || own.name == "Head") continue;
                LimbBehaviour source = null;
                foreach (var limb in donor.Limbs) if (limb && limb.name == own.name) { source = limb; break; }
                if (!source) continue;
                var original = own.GetComponent<SpriteRenderer>().sprite;
                var sprite = source.GetComponent<SpriteRenderer>().sprite;
                if (!original || !sprite) continue;
                var pixels = Read(sprite);
                int sw = (int)sprite.textureRect.width, sh = (int)sprite.textureRect.height;
                int width = (int)original.textureRect.width, height = (int)original.textureRect.height;
                var output = new Color32[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var color = pixels[Math.Min(sh - 1, y * sh / height) * sw + Math.Min(sw - 1, x * sw / width)];
                    if (color.a > 0 && SkinColour(color, skin))
                    {
                        float light = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) / 255f;
                        color = person.name.IndexOf("Evil Buu", StringComparison.OrdinalIgnoreCase) >= 0
                            ? new Color(.62f * light, .59f * light, .65f * light, color.a / 255f)
                            : new Color(.96f * light, .47f * light, .67f * light, color.a / 255f);
                    }
                    output[y * width + x] = color;
                }
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(output); texture.Apply();
                parts.Add(new OutfitPart { LimbName = own.name, Png = Convert.ToBase64String(texture.EncodeToPNG()),
                    PivotX = original.pivot.x / width, PivotY = original.pivot.y / height, PixelsPerUnit = original.pixelsPerUnit });
                Destroy(texture);
                foreach (var view in source.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!view.enabled || !view.sprite || view.GetComponentInParent<LimbBehaviour>() != source || !Garment(view.name)) continue;
                    var garment = view.sprite;
                    var image = new Texture2D((int)garment.textureRect.width, (int)garment.textureRect.height, TextureFormat.RGBA32, false);
                    image.SetPixels32(Read(garment)); image.Apply();
                    parts.Add(new OutfitPart { LimbName = own.name, AccessoryName = view.name,
                        Png = Convert.ToBase64String(image.EncodeToPNG()), PivotX = garment.pivot.x / image.width,
                        PivotY = garment.pivot.y / image.height, PixelsPerUnit = garment.pixelsPerUnit,
                        LocalPosition = source.transform.InverseTransformPoint(view.transform.position),
                        LocalScale = view.transform.localScale, Angle = Mathf.DeltaAngle(source.transform.eulerAngles.z, view.transform.eulerAngles.z),
                        SortingOffset = view.sortingOrder - source.GetComponent<SpriteRenderer>().sortingOrder });
                    Destroy(image);
                }
            }
            return parts.ToArray();
        }

        static bool Garment(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("cape") || lower.Contains("cloak") || lower.Contains("coat") || lower.Contains("belt")
                || lower.Contains("scarf") || lower.Contains("jacket");
        }

        void RestoreOutfit()
        {
            if (!person || Outfit == null || Outfit.Length == 0) return;
            if (outfitSprites == null) outfitSprites = new Dictionary<string, Sprite>();
            if (accessories == null) accessories = new Dictionary<string, GameObject>();
            bool changed = false;
            foreach (var view in person.GetComponentsInChildren<SpriteRenderer>(true))
                if (Garment(view.name) && !view.name.StartsWith("Absorbed garment: ", StringComparison.Ordinal)) view.enabled = false;
            foreach (var part in Outfit)
            {
                string key = part.LimbName + "/" + part.AccessoryName;
                Sprite sprite;
                if (!outfitSprites.TryGetValue(key, out sprite) || !sprite)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    texture.LoadImage(Convert.FromBase64String(part.Png));
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(part.PivotX, part.PivotY), part.PixelsPerUnit);
                    sprite.name = "Absorbed outfit: " + StrongestName + "/" + part.LimbName;
                    outfitSprites[key] = sprite;
                }
                foreach (var limb in person.Limbs)
                    if (limb && limb.name == part.LimbName)
                    {
                        var view = limb.GetComponent<SpriteRenderer>();
                        if (string.IsNullOrEmpty(part.AccessoryName))
                        {
                            // Energy erosion owns its temporary sprite until that
                            // tissue heals/regrows. Replacing it every frame would
                            // continuously restart the existing destruction effect.
                            var erosion = limb.GetComponent<EnergyErosion>();
                            if (erosion && (erosion.Progress > 0f || erosion.Shown > 0f || erosion.Erased)) break;
                            if (view.sprite != sprite) { view.sprite = sprite; changed = true; }
                        }
                        else
                        {
                            GameObject garment;
                            if (!accessories.TryGetValue(key, out garment) || !garment)
                            {
                                garment = new GameObject("Absorbed garment: " + part.AccessoryName);
                                garment.transform.SetParent(limb.transform, false);
                                garment.transform.localPosition = part.LocalPosition; garment.transform.localScale = part.LocalScale;
                                garment.transform.localRotation = Quaternion.Euler(0f, 0f, part.Angle);
                                garment.AddComponent<Optout>();
                                var clothing = garment.AddComponent<SpriteRenderer>(); clothing.sprite = sprite;
                                clothing.sortingLayerID = view.sortingLayerID; clothing.sortingOrder = view.sortingOrder + part.SortingOffset;
                                accessories[key] = garment;
                                changed = true;
                            }
                        }
                        break;
                    }
            }
            if (changed) DeferredOutlineRefresh.RequestPerson(person);
        }
        bool HasErosion()
        {
            if (!person) return false;
            foreach (var erosion in person.GetComponentsInChildren<EnergyErosion>(true))
                if (erosion.Progress > 0f || erosion.Shown > 0f || erosion.Erased) return true;
            return false;
        }
        void ReleaseRetiredSprites()
        {
            if (retiredSprites == null) return;
            foreach (var sprite in retiredSprites) if (sprite) { Destroy(sprite.texture); Destroy(sprite); }
            retiredSprites.Clear();
        }
        void ReleaseOutfitSprites(bool disposing = false)
        {
            if (outfitSprites == null) return;
            if (accessories != null) { foreach (var garment in accessories.Values) if (garment) Destroy(garment); accessories.Clear(); }
            bool retained = !disposing && HasErosion();
            if (retiredSprites == null) retiredSprites = new List<Sprite>();
            foreach (var sprite in outfitSprites.Values) if (sprite)
            { if (retained) retiredSprites.Add(sprite); else { Destroy(sprite.texture); Destroy(sprite); } }
            outfitSprites.Clear();
            if (disposing) ReleaseRetiredSprites();
        }
        public void OnBeforeSerialise() { CancelAbsorption(); ReconcilePower(); }
        public void OnAfterDeserialise(List<GameObject> objects)
        {
            person = GetComponent<PersonBehaviour>(); menusBound = false;
            bodies = new List<CapturedBody>(); renderers = new List<CapturedRenderer>();
            outfitSprites = new Dictionary<string, Sprite>();
            accessories = new Dictionary<string, GameObject>();
            retiredSprites = new List<Sprite>();
            BindMenus(); RestoreOutfit();
        }
        void OnDisable() { CancelAbsorption(); }
        void OnDestroy() { CancelAbsorption(); ReleaseOutfitSprites(true); }
    }
}
