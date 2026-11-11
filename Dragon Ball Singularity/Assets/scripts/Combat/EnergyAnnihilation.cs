using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Energy uses the same power units as beams, spheres and CombatPowerController.
    // This is independent of health damage: a dead fighter must still have matter
    // left to erode, and damage immunity must still be respected.
    public static class EnergyAnnihilation
    {
        public const float MinimumPowerRatio = 1.15f;

        public static float Resistance(LimbBehaviour limb)
        {
            if (!limb) return 1f;
            // Current combat power drops to zero on death. Native maximum health
            // keeps a corpse as hard to vaporise as its original living tissue.
            double power = Math.Max(1d, Math.Max(DbpuFx.PowerOf(limb.Person),
                Math.Max(KiCombatMath.Finite(limb.InitialHealth, 1e32f), CombatDismemberment.PowerOf(limb.Person))));
            float durability = Mathf.Max(1f, KiCombatMath.Finite(DbpuLimbDurability.GetResistance(limb), 25f));
            float tier = limb.name.Contains("Body") ? 1.45f : limb.HasBrain ? 1.2f : .9f;
            return Mathf.Max(1f, KiCombatMath.Cap(power * tier * Math.Max(1d, Math.Sqrt(durability / 25d))));
        }

        public static float ExposureSeconds(float ratio)
        {
            if (!Finite(ratio) || ratio <= MinimumPowerRatio) return float.PositiveInfinity;
            // Even extreme power leaves enough time to read skin, bone and ash.
            return Mathf.Clamp(3.2f / Mathf.Sqrt(ratio - 1f), .65f, 8f);
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool Valid(PersonBehaviour attacker, LimbBehaviour target, float power)
        {
            return attacker && target && target.Person && target.Person != attacker && !target.ImmuneToDamage
                && target.gameObject.activeInHierarchy && target.PhysicalBehaviour && !target.PhysicalBehaviour.isDisintegrated
                && !target.Person.GetComponent<HakaiDissolveMarker>() && Finite(power) && power > 0f
                && Time.timeScale > 0f && !DbpuFx.Paused;
        }

        public static void Expose(PersonBehaviour attacker, LimbBehaviour target, float attackPower,
            Vector2 contact, float seconds, Color color)
        {
            if (!Valid(attacker, target, attackPower) || !Finite(seconds) || seconds <= 0f
                || !Finite(contact.x) || !Finite(contact.y)) return;
            float ratio = attackPower / Resistance(target);
            if (ratio <= MinimumPowerRatio) return;
            var erosion = target.GetComponent<EnergyErosion>();
            if (!erosion) erosion = target.gameObject.AddComponent<EnergyErosion>();
            erosion.Expose(contact, Mathf.Min(.12f, seconds) / ExposureSeconds(ratio), color);
        }

        // A qualifying terminal explosion deposits a finite lethal dose, then lets
        // the same visual sequence finish. It never deletes all limbs at once.
        public static void Detonate(PersonBehaviour attacker, LimbBehaviour target, float attackPower,
            Vector2 contact, Color color)
        {
            if (!Valid(attacker, target, attackPower) || attackPower / Resistance(target) <= MinimumPowerRatio) return;
            var erosion = target.GetComponent<EnergyErosion>();
            if (!erosion) erosion = target.gameObject.AddComponent<EnergyErosion>();
            erosion.Expose(contact, 1f, color, true);
        }
    }

    // Each affected limb owns its texture. Shared skins and other fighters are
    // never mutated. Sprite pixels disappear outward from the actual hit point.
    [DefaultExecutionOrder(320)]
    public sealed class EnergyErosion : MonoBehaviour, Messages.IOnBeforeSerialise, Messages.IOnAfterDeserialise
    {
        public float Progress;
        public float Shown;
        public bool Erased;
        public Vector2 HitLocal;
        public Vector2 AshDirection;
        public Color HeatColor;
        public int PatternSeed;
        public bool FunctionLost;
        public bool SupportLost;
        public bool MotorCaptured;
        public bool MotorWasEnabled;
        public int RemovedPixels { get; private set; }
        public bool SkeletonVisible { get; private set; }
        public float TissueLoss { get; private set; }
        public float BoneLoss { get; private set; }
        public float ConnectionLoss { get; private set; }
        public bool BoneSupportBroken { get; private set; }

        [SkipSerialisation] LimbBehaviour limb;
        [SkipSerialisation] SpriteRenderer spriteRenderer;
        [SkipSerialisation] Sprite originalSprite, runtimeSprite;
        [SkipSerialisation] Material originalMaterial, runtimeMaterial;
        [SkipSerialisation] Texture2D texture;
        [SkipSerialisation] Color32[] skin, flesh, bone, pixels;
        [SkipSerialisation] float[] ranks;
        [SkipSerialisation] byte[] stages;
        [SkipSerialisation] int[] boneSlices, removedSlices;
        [SkipSerialisation] int width, height, recoveryCount;
        [SkipSerialisation] Vector2 contactLocal, direction;
        [SkipSerialisation] Color heat;
        [SkipSerialisation] float lastContact = -100f, lastSample = -100f, sampleDose, nextPaint;
        [SkipSerialisation] float painted = -1f;
        [SkipSerialisation] bool committed;
        [SkipSerialisation] BuuRegeneration majin;
        [SkipSerialisation] DbpuParticles particles;

        void Awake()
        {
            limb = GetComponent<LimbBehaviour>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            majin = limb && limb.Person ? limb.Person.GetComponent<BuuRegeneration>() : null;
            recoveryCount = majin ? majin.RecoveryCount : 0;
            if (PatternSeed == 0) PatternSeed = GetInstanceID();
        }

        public void Expose(Vector2 point, float dose, Color color, bool terminal = false)
        {
            if (Erased || committed || !limb || dose <= 0f) return;
            heat = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
            if (!originalSprite && spriteRenderer && spriteRenderer.sprite)
            {
                contactLocal = spriteRenderer.transform.InverseTransformPoint(point);
                direction = (Vector2)spriteRenderer.bounds.center - point;
                if (direction.sqrMagnitude < .001f) direction = Vector2.right;
                direction.Normalize();
                HitLocal = contactLocal; AshDirection = direction;
                Build();
            }
            HeatColor = heat;
            // Multiple colliders, beam segments or eye rays at the same physics
            // instant count the strongest sample once instead of multiplying time.
            if (lastSample != Time.time) { lastSample = Time.time; sampleDose = 0f; }
            float accepted = terminal ? 1f : Mathf.Max(0f, dose - sampleDose);
            sampleDose = Mathf.Max(sampleDose, dose);
            Progress = Mathf.Clamp01(Progress + accepted);
            lastContact = Time.time;
            committed = Progress >= 1f;
            enabled = true;
        }

        void Update()
        {
            if (!limb || !limb.PhysicalBehaviour) { Destroy(this); return; }
            float dt = DbpuFx.Dt;
            if (dt <= 0f) return;
            if (majin && majin.RecoveryCount != recoveryCount && !majin.IsReforming
                && !limb.PhysicalBehaviour.isDisintegrated && limb.gameObject.activeInHierarchy)
            { ResetTissue(); return; }
            if (Erased) return;
            // External deletion/disintegration owns the result; stop visual writes.
            if (limb.PhysicalBehaviour.isDisintegrated) { ReleaseParticles(); return; }
            // Transformations and custom skins can replace a sprite during combat.
            // Continue on that new skin and keep its normal native material intact.
            if (runtimeSprite && spriteRenderer && spriteRenderer.sprite != runtimeSprite)
            {
                if (spriteRenderer.sharedMaterial == runtimeMaterial) spriteRenderer.sharedMaterial = originalMaterial;
                DisposeVisuals();
                if (spriteRenderer.sprite) Build();
            }
            if (!committed && Time.time - lastContact > .3f)
            {
                // Lost tissue stays lost. Only harmless accumulated heat dissipates.
                float floor = RemovedPixels > 0 || SkeletonVisible ? Shown : 0f;
                Progress = Mathf.Max(floor, Progress - dt * .22f);
                if (majin && majin.RegenerationEnabled && !majin.IsReforming)
                {
                    // Majin tissue can heal progressive burns without a joint loss.
                    Progress = Mathf.Max(0f, Progress - dt * .35f);
                    Shown = Mathf.Min(Shown, Progress);
                }
            }
            Shown = Mathf.MoveTowards(Shown, Progress, dt * 1.25f);
            if (Time.time >= nextPaint)
            {
                nextPaint = Time.time + .045f;
                Paint();
            }
            ApplyStructuralDamage();
            if (committed && Shown >= .999f) Finish();
            else if (!FunctionLost && Progress <= 0f && Shown <= 0f && Time.time - lastContact > .3f) ResetTissue();
        }

        void Build()
        {
            originalSprite = spriteRenderer.sprite;
            originalMaterial = spriteRenderer.sharedMaterial;
            Rect rect = originalSprite.rect;
            width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            // Avoid allocating unbounded buffers for an unrelated oversized sprite.
            if ((long)width * height > 1048576) return;
            Vector2 uvMin = new Vector2(rect.x / originalSprite.texture.width, rect.y / originalSprite.texture.height);
            Vector2 uvSize = new Vector2(rect.width / originalSprite.texture.width, rect.height / originalSprite.texture.height);
            skin = Read(originalSprite.texture, uvMin, uvSize, width, height);
            if (skin == null) return;
            var material = spriteRenderer.material;
            flesh = material.HasProperty("_FleshTex") ? Read(material.GetTexture("_FleshTex"), uvMin, uvSize, width, height) : null;
            bone = material.HasProperty("_BoneTex") ? Read(material.GetTexture("_BoneTex"), uvMin, uvSize, width, height) : null;
            pixels = new Color32[skin.Length]; ranks = new float[skin.Length]; stages = new byte[skin.Length];
            boneSlices = new int[Mathf.Max(width, height)];
            removedSlices = new int[boneSlices.Length];
            float farthest = .001f;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (skin[i].a == 0) continue;
                Vector2 p = LocalPixel(x, y);
                float distance = Vector2.Distance(p, contactLocal);
                ranks[i] = distance;
                farthest = Mathf.Max(farthest, distance);
            }
            for (int i = 0; i < ranks.Length; i++)
                ranks[i] = Mathf.Clamp01(ranks[i] / farthest * .8f + DbpuFx.Hash(i / 3 + PatternSeed) * .2f);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "DBPU eroding tissue", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(skin); texture.Apply(false, false);
            runtimeSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(originalSprite.pivot.x / width, originalSprite.pivot.y / height),
                originalSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, originalSprite.border);
            runtimeSprite.name = "DBPU eroding " + originalSprite.name;
            runtimeSprite.hideFlags = HideFlags.DontSave;
            runtimeMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            spriteRenderer.sharedMaterial = runtimeMaterial;
            spriteRenderer.sprite = runtimeSprite;
        }

        // Reads a sprite's UV crop, including non-readable textures loaded from an
        // asset bundle. Temporary render targets are always returned to Unity.
        static Color32[] Read(Texture source, Vector2 offset, Vector2 size, int w, int h)
        {
            if (!source) return null;
            var target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, target, size, offset);
                RenderTexture.active = target;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy) Destroy(copy);
            }
        }

        Vector2 LocalPixel(int x, int y)
        {
            float lx = (x + .5f - originalSprite.pivot.x) / originalSprite.pixelsPerUnit;
            float ly = (y + .5f - originalSprite.pivot.y) / originalSprite.pixelsPerUnit;
            return new Vector2(spriteRenderer.flipX ? -lx : lx, spriteRenderer.flipY ? -ly : ly);
        }

        void Paint()
        {
            if (!texture || pixels == null || !spriteRenderer || spriteRenderer.sprite != runtimeSprite) return;
            if (Mathf.Abs(painted - Shown) < .0001f) return;
            painted = Shown;
            int removed = 0, emitted = 0, total = 0, exposed = 0, boneTotal = 0, boneRemoved = 0;
            Array.Clear(boneSlices, 0, boneSlices.Length);
            Array.Clear(removedSlices, 0, removedSlices.Length);
            bool skeleton = false;
            bool healing = majin && majin.RegenerationEnabled && Time.time - lastContact > .3f && !committed;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (skin[i].a == 0) { pixels[i] = skin[i]; continue; }
                total++;
                float front = Shown * 1.85f - ranks[i];
                byte stage = front > .7f ? (byte)4 : front > .3f ? (byte)3 : front > .13f ? (byte)2 : front > .015f ? (byte)1 : (byte)0;
                if (!healing) stage = Math.Max(stage, stages[i]);
                if (stage >= 3) exposed++;
                if (bone == null || bone[i].a > 16)
                {
                    boneTotal++;
                    int slice = height >= width ? i / width : i % width;
                    boneSlices[slice]++;
                    if (stage == 4) { boneRemoved++; removedSlices[slice]++; }
                }
                Color c = skin[i];
                if (stage == 4)
                {
                    removed++;
                    c = Color.clear;
                    if (stages[i] < 4 && emitted < 8)
                    {
                        Emit(i, skin[i]); emitted++;
                    }
                }
                else if (stage == 3)
                {
                    // Use the fighter's own bone atlas (androids and Majins also
                    // retain their custom interiors) instead of a human overlay.
                    c = bone != null ? (Color)bone[i] : Color.Lerp((Color)skin[i], new Color(.88f, .85f, .73f), .8f);
                    skeleton |= c.a > .05f;
                }
                else if (stage == 2) c = flesh != null ? (Color)flesh[i] : Color.Lerp((Color)skin[i], new Color(.48f, .08f, .05f), .75f);
                else if (stage == 1) c = Color.Lerp((Color)skin[i], new Color(.12f, .07f, .06f), .72f);
                if (stage < 4 && front > .63f)
                    c = Color.Lerp(c, Color.Lerp(heat, Color.white, .45f), .8f);
                pixels[i] = c; stages[i] = stage;
            }
            RemovedPixels = removed;
            SkeletonVisible = skeleton;
            TissueLoss = total > 0 ? (float)exposed / total : 0f;
            BoneLoss = boneTotal > 0 ? (float)boneRemoved / boneTotal : 0f;
            BoneSupportBroken = false;
            int gap = 0, requiredGap = Mathf.Max(2, Mathf.CeilToInt(boneSlices.Length * .08f));
            for (int i = 0; i < boneSlices.Length; i++)
            {
                gap = boneSlices[i] > 0 && removedSlices[i] >= boneSlices[i] * .8f ? gap + 1 : 0;
                if (gap >= requiredGap && BoneLoss >= .12f) BoneSupportBroken = true;
            }
            ConnectionLoss = limb.Joint ? AttachmentLoss(limb.transform.TransformPoint(limb.Joint.anchor)) : 0f;
            texture.SetPixels32(pixels); texture.Apply(false, false);
            ApplyStructuralDamage();
        }

        float AttachmentLoss(Vector2 world)
        {
            if (stages == null || !originalSprite) return 0f;
            Vector2 local = spriteRenderer.transform.InverseTransformPoint(world);
            if (spriteRenderer.flipX) local.x = -local.x;
            if (spriteRenderer.flipY) local.y = -local.y;
            Vector2 pixel = local * originalSprite.pixelsPerUnit + originalSprite.pivot;
            float radius = Mathf.Max(2f, Mathf.Min(width, height) * .65f);
            int total = 0, lost = 0;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(pixel.y - radius)); y < Mathf.Min(height, pixel.y + radius + 1); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(pixel.x - radius)); x < Mathf.Min(width, pixel.x + radius + 1); x++)
                {
                    int i = y * width + x;
                    if (skin[i].a == 0 || (new Vector2(x + .5f, y + .5f) - pixel).sqrMagnitude > radius * radius) continue;
                    total++; if (stages[i] >= 3) lost++;
                }
            return total > 0 ? (float)lost / total : 0f;
        }

        void ApplyStructuralDamage()
        {
            if (!limb || !limb.Person || Erased || !limb.PhysicalBehaviour || limb.PhysicalBehaviour.isDisintegrated
                || Time.timeScale <= 0f || DbpuFx.Paused || (majin && majin.IsReforming)) return;
            bool mechanical = limb.IsAndroid;
            bool muscleGone = !mechanical && TissueLoss >= .4f;
            if (muscleGone || BoneLoss >= .3f || SupportLost) FunctionLost = true;
            bool sever = (!mechanical && (TissueLoss >= .55f || ConnectionLoss >= .65f))
                || BoneSupportBroken || BoneLoss >= .45f;
            if (sever)
            {
                SupportLost = FunctionLost = true;
                if (limb.HasJoint && limb.Joint) EnergyAnatomy.SeverSupport(limb);
                var looseTissue = limb.GetComponent<GoreStringBehaviour>();
                if (looseTissue && looseTissue.Joint) looseTissue.DestroyJoint();
            }
            // The root torso has no parent hinge. Its children's joints still
            // depend on this segment's surviving soft tissue and bony support.
            if (stages != null && TissueLoss > .15f)
            {
                var body = limb.GetComponent<Rigidbody2D>();
                foreach (var child in limb.Person.Limbs)
                    if (child && child != limb && child.HasJoint && child.Joint && child.Joint.connectedBody == body
                        && (SupportLost || (!mechanical && AttachmentLoss(child.transform.TransformPoint(child.Joint.anchor)) >= .65f)))
                        EnergyAnatomy.SeverSupport(child);
            }
            if (FunctionLost) InhibitMovement();
            else if (!majin && TissueLoss > .1f)
            {
                float capacity = Mathf.Clamp01(1f - TissueLoss * 1.6f - BoneLoss);
                limb.Health = Mathf.Min(limb.Health, KiCombatMath.Cap((double)limb.InitialHealth * capacity));
            }
            if (majin) return;
            bool vital = limb.HasBrain || limb.HasLungs || limb.name == "UpperBody" || limb.name == "MiddleBody";
            bool destroyed = mechanical ? BoneLoss >= .5f
                : limb.HasBrain ? FunctionLost || BoneLoss >= .18f
                : TissueLoss >= .55f || BoneLoss >= .35f;
            if (vital && destroyed) EnergyVitalFailure.Kill(limb.Person);
        }

        public void InhibitMovement()
        {
            if (!FunctionLost || !limb || (majin && majin.IsReforming)) return;
            limb.Health = 0f;
            limb.Numbness = 1f;
            if (limb.Joint)
            {
                if (!MotorCaptured) { MotorCaptured = true; MotorWasEnabled = limb.Joint.useMotor; }
                limb.Joint.useMotor = false;
            }
            var owner = limb.Person;
            if (!owner) return;
            var beam = owner.GetComponent<BeamBehaviour>();
            if (beam && beam.UsedArm == transform && (beam.Firing || beam.Charging)) beam.CancelBeamState();
            var ball = owner.GetComponent<BallBehaviour>();
            if (ball && ball.CastingArm == transform && ball.Charging) ball.CancelBallState();
        }

        void Emit(int pixel, Color color)
        {
            if (!particles) particles = DbpuParticles.Create("DBPU energy ash", DbpuFx.Square, 128,
                spriteRenderer.sortingOrder + 3, spriteRenderer.sortingLayerName);
            Vector2 point = spriteRenderer.transform.TransformPoint(LocalPixel(pixel % width, pixel / width));
            float size = Mathf.Max(.015f, Mathf.Abs(spriteRenderer.transform.lossyScale.x) / originalSprite.pixelsPerUnit);
            Vector2 velocity = direction * UnityEngine.Random.Range(.3f, 2f) + Vector2.up * UnityEngine.Random.Range(.2f, 1.4f);
            particles.Emit(point, velocity, UnityEngine.Random.Range(.3f, .8f), size * 1.5f,
                Color.Lerp(color, heat, .6f), DbpuFx.A(heat, 0f), 1.4f, 0f, 160f, false, size * .25f);
        }

        void Finish()
        {
            if (Erased || !limb || !limb.PhysicalBehaviour) return;
            Erased = true;
            // Erasing a single Majin part removes only that matter. A surviving
            // part can still create new tissue; erasing all matter cannot revive it.
            BuuRegeneration.Erase(limb);
            CombatDismemberment.SeverLimb(limb);
            limb.Health = 0f;
            limb.PhysicalBehaviour.Disintegrate();
            ReleaseParticles();
        }

        void ReleaseParticles() { if (particles) { particles.Release(); particles = null; } }

        public void ResetTissue()
        {
            if (spriteRenderer && spriteRenderer.sprite == runtimeSprite)
            {
                spriteRenderer.sprite = originalSprite;
            }
            if (spriteRenderer && spriteRenderer.sharedMaterial == runtimeMaterial)
                spriteRenderer.sharedMaterial = originalMaterial;
            DisposeVisuals();
            if (MotorCaptured && limb && limb.Joint && !limb.IsDismembered) limb.Joint.useMotor = MotorWasEnabled;
            Progress = Shown = 0f; Erased = committed = false;
            FunctionLost = SupportLost = MotorCaptured = false;
            RemovedPixels = 0; SkeletonVisible = false;
            TissueLoss = BoneLoss = ConnectionLoss = 0f; BoneSupportBroken = false;
            lastContact = lastSample = -100f; sampleDose = 0f;
            recoveryCount = majin ? majin.RecoveryCount : 0;
        }

        void OnEnable()
        {
            if (Erased && limb && limb.PhysicalBehaviour && !limb.PhysicalBehaviour.isDisintegrated && majin)
                ResetTissue();
        }
        public void OnBeforeSerialise()
        {
            HitLocal = contactLocal; AshDirection = direction; HeatColor = heat;
        }
        public void OnAfterDeserialise(List<GameObject> objects)
        {
            limb = GetComponent<LimbBehaviour>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            majin = limb && limb.Person ? limb.Person.GetComponent<BuuRegeneration>() : null;
            recoveryCount = majin ? majin.RecoveryCount : 0;
            contactLocal = HitLocal; direction = AshDirection; heat = HeatColor;
            Progress = Mathf.Clamp01(KiCombatMath.Finite(Progress));
            Shown = Mathf.Clamp01(KiCombatMath.Finite(Shown));
            committed = Progress >= 1f && !Erased;
            lastSample = -100f; sampleDose = 0f; lastContact = Time.time - .31f;
            if (!Erased && limb && limb.PhysicalBehaviour && !limb.PhysicalBehaviour.isDisintegrated
                && spriteRenderer && spriteRenderer.sprite && (Shown > 0f || Progress > 0f))
            { Build(); Paint(); }
        }
        void OnDisable() { ReleaseParticles(); }
        void DisposeVisuals()
        {
            ReleaseParticles();
            if (runtimeSprite) Destroy(runtimeSprite);
            if (texture) Destroy(texture);
            if (runtimeMaterial) Destroy(runtimeMaterial);
            runtimeSprite = originalSprite = null; texture = null;
            runtimeMaterial = originalMaterial = null;
            skin = flesh = bone = pixels = null; ranks = null; stages = null;
            boneSlices = removedSlices = null;
            painted = -1f;
        }
        void OnDestroy()
        {
            if (spriteRenderer && spriteRenderer.sprite == runtimeSprite)
                spriteRenderer.sprite = originalSprite;
            if (spriteRenderer && spriteRenderer.sharedMaterial == runtimeMaterial)
                spriteRenderer.sharedMaterial = originalMaterial;
            DisposeVisuals();
        }
    }
}
