using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public class CellMaxSpotGlow : MonoBehaviour
    {
        static readonly Color32[] MarkerGreenExact =
        {
            new Color32(0x27, 0x30, 0x17, 255)
        };

        static readonly Color32[] MarkerRedExact =
        {
            new Color32(0x22, 0x00, 0x04, 255),
            new Color32(0x2e, 0x01, 0x08, 255),
            new Color32(0x44, 0x05, 0x10, 255),
            new Color32(0x22, 0x00, 0x00, 255),
            new Color32(0x21, 0x00, 0x04, 255),
            new Color32(0x23, 0x00, 0x04, 255),
            new Color32(0x2d, 0x01, 0x08, 255),
            new Color32(0x2f, 0x01, 0x08, 255),
            new Color32(0x2e, 0x00, 0x08, 255),
            new Color32(0x2e, 0x02, 0x08, 255),
            new Color32(0x43, 0x05, 0x10, 255),
            new Color32(0x45, 0x05, 0x10, 255),
            new Color32(0x44, 0x04, 0x10, 255),
            new Color32(0x44, 0x06, 0x10, 255),
            new Color32(0x00, 0x22, 0x04, 255),
            new Color32(0x04, 0x00, 0x22, 255),
            new Color32(0x20, 0x00, 0x04, 255),
            new Color32(0x24, 0x00, 0x04, 255)
        };

        static readonly Color32[] MarkerTailFireExact =
        {
            new Color32(0x03, 0x04, 0x05, 255),
            new Color32(0x02, 0x04, 0x05, 255),
            new Color32(0x04, 0x04, 0x05, 255),
            new Color32(0x03, 0x03, 0x05, 255),
            new Color32(0x03, 0x05, 0x05, 255),
            new Color32(0x03, 0x04, 0x04, 255),
            new Color32(0x03, 0x04, 0x06, 255)
        };

        static readonly Color32 MarkerEyeLaserExact = new Color32(0x93, 0x42, 0x44, 255);
        static readonly Color32 MarkerPiccoloEyeExact = new Color32(0, 0, 0, 255);

        public enum EyeLaserMarkerMode { CellMax, Piccolo }

        const int SpotRoleNone = -1;
        const int SpotRoleGlowOnly = 0;
        const int SpotRoleFire = 1;
        const int GreenMatchTolerance = 6;
        const int RedMatchTolerance = 8;
        const int TailFireMatchTolerance = 4;
        const int PurpleMatchTolerance = 6;
        const int EyeLaserMatchTolerance = 4;

        static readonly Color GlowPurple = new Color(0.72f, 0.28f, 1f, 1f);
        static readonly Color GlowPurpleAlt = new Color(0.85f, 0.45f, 1f, 1f);
        static readonly Color PiccoloEyeGlow = new Color(1f, 0.92f, 0.25f, 1f);
        static readonly Color PiccoloEyeGlowAlt = new Color(1f, 0.98f, 0.45f, 1f);

        public int LimbSkinIndex = 1;
        public bool IsTail;
        public bool IsLeg;
        public bool DynamicSpriteRefresh;
        public bool ScanEyeLaserMarkers;
        public EyeLaserMarkerMode EyeLaserMarker = EyeLaserMarkerMode.CellMax;
        public bool GenericFallback;
        public bool SkipEyeGlowOverlay;
        public Color EyeGlowColor = GlowPurple;
        public Color EyeGlowAltColor = GlowPurpleAlt;

        SpriteRenderer limbRenderer;
        SpriteRenderer overlayRenderer;
        SpriteRenderer eyeOverlayRenderer;
        Sprite lastScannedSprite;
        Vector2[] fireEmissionPoints;
        Vector2[] eyeLaserEmissionPoints;
        float glowTarget;
        float glow01;
        float eyeGlowTarget;
        float eyeGlow01;
        float pulseSpeed = 6f;
        bool pulseWhileActive;
        bool eyePulseWhileActive;

        public bool HasSpots => overlayRenderer != null;
        public bool HasFireSpots => fireEmissionPoints != null && fireEmissionPoints.Length > 0;
        public bool HasEyeLaserSpots => eyeLaserEmissionPoints != null && eyeLaserEmissionPoints.Length > 0;

        public Vector2[] GetEyeLaserEmissionLocals() => eyeLaserEmissionPoints ?? new Vector2[0];

        public void Initialize()
        {
            limbRenderer = GetComponent<SpriteRenderer>();
            if (limbRenderer == null || limbRenderer.sprite == null) return;

            var limb = GetComponent<LimbBehaviour>();
            IsLeg = !IsTail && limb != null && limb.RoughClassification == LimbBehaviour.BodyPart.Legs;

            RebuildSpotData(true);
        }

        public void RefreshFromCurrentSprite()
        {
            if (limbRenderer == null) limbRenderer = GetComponent<SpriteRenderer>();
            if (limbRenderer == null || limbRenderer.sprite == null) return;
            if (lastScannedSprite == limbRenderer.sprite) return;
            RebuildSpotData(false);
        }

        public void ForceRefreshFromCurrentSprite()
        {
            if (limbRenderer == null) limbRenderer = GetComponent<SpriteRenderer>();
            if (limbRenderer == null || limbRenderer.sprite == null) return;
            RebuildSpotData(false);
        }

        void RebuildSpotData(bool fullInit)
        {
            Sprite sprite = limbRenderer.sprite;
            lastScannedSprite = sprite;

            float prevGlow = glow01;
            float prevTarget = glowTarget;
            bool prevPulse = pulseWhileActive;
            float prevEyeGlow = eyeGlow01;
            float prevEyeTarget = eyeGlowTarget;
            bool prevEyePulse = eyePulseWhileActive;

            ClearOverlay();
            ClearEyeOverlay();

            var fireLocals = new List<Vector2>(32);
            var overlayPx = new Color32[0];
            int overlayW = 0;
            int overlayH = 0;
            bool foundAnySpot = false;

            if (DynamicSpriteRefresh)
            {
                TryScanTexture(sprite, GetReadableTexture(sprite), ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
            }
            else
            {
                if (fullInit)
                    ScanLoadedLimbTextures(sprite, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);

                Material mat = limbRenderer.material;
                if (mat != null)
                {
                    if (mat.HasProperty("_FleshTex"))
                        TryScanTexture(sprite, mat.GetTexture("_FleshTex") as Texture2D, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
                    if (mat.HasProperty("_BoneTex"))
                        TryScanTexture(sprite, mat.GetTexture("_BoneTex") as Texture2D, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
                    if (mat.HasProperty("_DamageTex"))
                        TryScanTexture(sprite, mat.GetTexture("_DamageTex") as Texture2D, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
                }

                TryScanTexture(sprite, GetReadableTexture(sprite), ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
            }

            var eyeLaserLocals = new List<Vector2>();
            if (ScanEyeLaserMarkers)
            {
                ScanEyeLaserSources(sprite, eyeLaserLocals);
                if (!SkipEyeGlowOverlay)
                {
                    var eyeOverlayPx = new Color32[0];
                    int eyeOverlayW = 0;
                    int eyeOverlayH = 0;
                    ScanEyeLaserOverlay(sprite, ref eyeOverlayPx, ref eyeOverlayW, ref eyeOverlayH);
                    if (eyeOverlayW > 0 && eyeOverlayPx != null && eyeOverlayPx.Length > 0)
                        CreateEyeOverlay(sprite, eyeOverlayPx, eyeOverlayW, eyeOverlayH);
                }
            }

            if (ScanEyeLaserMarkers && EyeLaserMarker == EyeLaserMarkerMode.Piccolo && eyeLaserLocals.Count == 0 && sprite != null)
                eyeLaserLocals.Add(GetPiccoloEyeFallbackLocal(sprite));

            eyeLaserEmissionPoints = ScanEyeLaserMarkers && eyeLaserLocals.Count > 0
                ? BuildEyeLaserClusters(eyeLaserLocals, EyeLaserMarker == EyeLaserMarkerMode.Piccolo ? 0.05f : 0.22f)
                : new Vector2[0];

            if (ScanEyeLaserMarkers && EyeLaserMarker == EyeLaserMarkerMode.Piccolo && !SkipEyeGlowOverlay
                && eyeOverlayRenderer == null && eyeLaserEmissionPoints.Length > 0 && sprite != null)
                BuildFallbackOverlay(sprite, eyeLaserEmissionPoints, true);

            if (foundAnySpot)
            {
                fireEmissionPoints = fireLocals.ToArray();
                CreateOverlay(sprite, overlayPx, overlayW, overlayH);
            }
            else
            {
                fireEmissionPoints = new Vector2[0];
            }

            if (GenericFallback)
                ApplyGenericFallbacks(sprite, ref foundAnySpot);

            glow01 = prevGlow;
            glowTarget = prevTarget;
            pulseWhileActive = prevPulse;
            eyeGlow01 = prevEyeGlow;
            eyeGlowTarget = prevEyeTarget;
            eyePulseWhileActive = prevEyePulse;
        }

        void ClearOverlay()
        {
            if (overlayRenderer != null)
            {
                Destroy(overlayRenderer.gameObject);
                overlayRenderer = null;
            }
        }

        void ClearEyeOverlay()
        {
            if (eyeOverlayRenderer != null)
            {
                Destroy(eyeOverlayRenderer.gameObject);
                eyeOverlayRenderer = null;
            }
        }

        void CreateOverlay(Sprite sprite, Color32[] overlayPx, int overlayW, int overlayH)
        {
            ClearOverlay();

            var overlayTex = new Texture2D(overlayW, overlayH, TextureFormat.RGBA32, false);
            overlayTex.filterMode = FilterMode.Point;
            overlayTex.wrapMode = TextureWrapMode.Clamp;
            overlayTex.SetPixels32(overlayPx);
            overlayTex.Apply(false, false);

            Sprite overlaySprite = Sprite.Create(
                overlayTex,
                new Rect(0, 0, overlayW, overlayH),
                new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height),
                sprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                sprite.border);

            Material bright = ModAPI.FindMaterial("VeryBright");
            var go = new GameObject("CellMaxSpotGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            overlayRenderer = go.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = overlaySprite;
            overlayRenderer.material = bright != null ? bright : limbRenderer.sharedMaterial;
            overlayRenderer.sortingLayerName = limbRenderer.sortingLayerName;
            overlayRenderer.sortingOrder = limbRenderer.sortingOrder + 12;
            overlayRenderer.color = new Color(GlowPurple.r, GlowPurple.g, GlowPurple.b, glow01);
        }

        void CreateEyeOverlay(Sprite sprite, Color32[] overlayPx, int overlayW, int overlayH)
        {
            ClearEyeOverlay();

            var overlayTex = new Texture2D(overlayW, overlayH, TextureFormat.RGBA32, false);
            overlayTex.filterMode = FilterMode.Point;
            overlayTex.wrapMode = TextureWrapMode.Clamp;
            overlayTex.SetPixels32(overlayPx);
            overlayTex.Apply(false, false);

            Sprite overlaySprite = Sprite.Create(
                overlayTex,
                new Rect(0, 0, overlayW, overlayH),
                new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height),
                sprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                sprite.border);

            Material bright = ModAPI.FindMaterial("VeryBright");
            var go = new GameObject("CellMaxEyeGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            eyeOverlayRenderer = go.AddComponent<SpriteRenderer>();
            eyeOverlayRenderer.sprite = overlaySprite;
            eyeOverlayRenderer.material = bright != null ? bright : limbRenderer.sharedMaterial;
            eyeOverlayRenderer.sortingLayerName = limbRenderer.sortingLayerName;
            eyeOverlayRenderer.sortingOrder = limbRenderer.sortingOrder + 13;
            eyeOverlayRenderer.color = new Color(EyeGlowColor.r, EyeGlowColor.g, EyeGlowColor.b, eyeGlow01);
        }

        void ApplyGenericFallbacks(Sprite sprite, ref bool foundAnySpot)
        {
            if (sprite == null) return;

            if (ScanEyeLaserMarkers)
            {
                if (eyeLaserEmissionPoints == null || eyeLaserEmissionPoints.Length == 0)
                {
                    Bounds b = sprite.bounds;
                    float y = b.center.y + b.extents.y * 0.12f;
                    eyeLaserEmissionPoints = new Vector2[]
                    {
                        new Vector2(b.center.x - b.extents.x * 0.2f, y),
                        new Vector2(b.center.x + b.extents.x * 0.2f, y)
                    };
                    if (!SkipEyeGlowOverlay)
                        BuildFallbackOverlay(sprite, eyeLaserEmissionPoints, true);
                }
                return;
            }

            if (fireEmissionPoints == null || fireEmissionPoints.Length == 0)
            {
                if (overlayRenderer != null)
                {
                    fireEmissionPoints = new Vector2[0];
                    return;
                }

                fireEmissionPoints = new Vector2[] { sprite.bounds.center };
                BuildFallbackOverlay(sprite, fireEmissionPoints, false);
                foundAnySpot = true;
            }
        }

        void BuildFallbackOverlay(Sprite sprite, Vector2[] localPoints, bool forEyes)
        {
            if (localPoints == null || localPoints.Length == 0) return;

            Rect rect = sprite.rect;
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (w <= 0 || h <= 0) return;

            var px = new Color32[w * h];
            Color32 glow32 = (Color32)EyeGlowAltColor;
            glow32.a = 255;
            Bounds bounds = sprite.bounds;

            for (int i = 0; i < localPoints.Length; i++)
            {
                float u = bounds.size.x > 0.0001f ? (localPoints[i].x - bounds.min.x) / bounds.size.x : 0.5f;
                float v = bounds.size.y > 0.0001f ? (localPoints[i].y - bounds.min.y) / bounds.size.y : 0.5f;
                int pxX = Mathf.Clamp(Mathf.RoundToInt(u * w), 0, w - 1);
                int pxY = Mathf.Clamp(Mathf.RoundToInt(v * h), 0, h - 1);

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = pxX + dx;
                        int y = pxY + dy;
                        if (x < 0 || x >= w || y < 0 || y >= h) continue;
                        px[y * w + x] = glow32;
                    }
                }
            }

            if (forEyes)
                CreateEyeOverlay(sprite, px, w, h);
            else
                CreateOverlay(sprite, px, w, h);
        }

        void ScanLoadedLimbTextures(Sprite sprite, ref List<Vector2> fireLocals, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAnySpot)
        {
            if (LimbSkinIndex < 1) return;
            string folder = "Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/";
            string idx = LimbSkinIndex.ToString();
            TryScanLoaded(sprite, folder + "LimbSpriteSkin" + idx + ".png", ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
            TryScanLoaded(sprite, folder + "LimbSpriteFlesh" + idx + ".png", ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
            TryScanLoaded(sprite, folder + "LimbSpriteBone" + idx + ".png", ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
            TryScanLoaded(sprite, folder + "LimbSpriteDamage" + idx + ".png", ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
        }

        void TryScanLoaded(Sprite sprite, string path, ref List<Vector2> fireLocals, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAnySpot)
        {
            try
            {
                Sprite loaded = ModAPI.LoadSprite(path);
                if (loaded != null && loaded.texture != null)
                {
                    Sprite layout = loaded;
                    if (Mathf.RoundToInt(loaded.rect.width) == Mathf.RoundToInt(sprite.rect.width)
                        && Mathf.RoundToInt(loaded.rect.height) == Mathf.RoundToInt(sprite.rect.height))
                        layout = sprite;
                    TryScanTexture(layout, loaded.texture, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
                }
            }
            catch
            {
            }
        }

        void TryScanTexture(Sprite sprite, Texture2D source, ref List<Vector2> fireLocals, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAnySpot)
        {
            Texture2D readable = DuplicateTexture(source);
            if (readable != null)
                ScanSprite(sprite, readable, ref fireLocals, ref overlayPx, ref overlayW, ref overlayH, ref foundAnySpot);
        }

        void ScanSprite(Sprite sprite, Texture2D source, ref List<Vector2> fireLocals, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAnySpot)
        {
            if (sprite == null || source == null) return;

            Rect rect = sprite.rect;
            int texW = source.width;
            int texH = source.height;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            Color32[] all = source.GetPixels32();

            if (overlayW != w || overlayH != h || overlayPx == null || overlayPx.Length != w * h)
            {
                overlayW = w;
                overlayH = h;
                overlayPx = new Color32[w * h];
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcIdx = (y0 + y) * texW + (x0 + x);
                    if (srcIdx < 0 || srcIdx >= all.Length) continue;

                    int role = ClassifySpotRole(all[srcIdx]);
                    if (role == SpotRoleNone) continue;

                    foundAnySpot = true;
                    Color32 glow32 = (Color32)GlowPurpleAlt;
                    glow32.a = 255;
                    overlayPx[y * w + x] = glow32;

                    if (role == SpotRoleFire)
                    {
                        float px = x0 + x + 0.5f;
                        float py = y0 + y + 0.5f;
                        fireLocals.Add(PixelToSpriteLocal(sprite, px, py));
                    }
                }
            }
        }

        public void SetGlowLevel(float level, bool pulse = false)
        {
            glowTarget = Mathf.Clamp01(level);
            pulseWhileActive = pulse;
            if (glowTarget <= 0f) pulseWhileActive = false;
        }

        public void FadeOut()
        {
            glowTarget = 0f;
            pulseWhileActive = false;
        }

        public void SetEyeGlowLevel(float level, bool pulse = false)
        {
            eyeGlowTarget = Mathf.Clamp01(level);
            eyePulseWhileActive = pulse;
            if (eyeGlowTarget <= 0f) eyePulseWhileActive = false;
        }

        public void FadeOutEyeGlow()
        {
            eyeGlowTarget = 0f;
            eyePulseWhileActive = false;
        }

        public void ApplyPiccoloEyeGlowColors()
        {
            EyeGlowColor = PiccoloEyeGlow;
            EyeGlowAltColor = PiccoloEyeGlowAlt;
        }

        public Vector2 GetRandomFireEmissionLocal()
        {
            if (!HasFireSpots)
                return Vector2.zero;
            return fireEmissionPoints[UnityEngine.Random.Range(0, fireEmissionPoints.Length)];
        }

        void ScanEyeLaserSources(Sprite sprite, List<Vector2> eyeLocals)
        {
            if (DynamicSpriteRefresh)
            {
                CollectEyeLaserFromTexture(sprite, GetReadableTexture(sprite), eyeLocals);
                return;
            }

            if (LimbSkinIndex >= 1)
            {
                string folder = "Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/";
                string idx = LimbSkinIndex.ToString();
                TryCollectEyeLaserLoaded(sprite, folder + "LimbSpriteSkin" + idx + ".png", eyeLocals);
                TryCollectEyeLaserLoaded(sprite, folder + "LimbSpriteFlesh" + idx + ".png", eyeLocals);
                TryCollectEyeLaserLoaded(sprite, folder + "LimbSpriteBone" + idx + ".png", eyeLocals);
                TryCollectEyeLaserLoaded(sprite, folder + "LimbSpriteDamage" + idx + ".png", eyeLocals);
            }

            Material mat = limbRenderer.material;
            if (mat != null)
            {
                if (mat.HasProperty("_FleshTex"))
                    CollectEyeLaserFromTexture(sprite, mat.GetTexture("_FleshTex") as Texture2D, eyeLocals);
                if (mat.HasProperty("_BoneTex"))
                    CollectEyeLaserFromTexture(sprite, mat.GetTexture("_BoneTex") as Texture2D, eyeLocals);
                if (mat.HasProperty("_DamageTex"))
                    CollectEyeLaserFromTexture(sprite, mat.GetTexture("_DamageTex") as Texture2D, eyeLocals);
            }

            CollectEyeLaserFromTexture(sprite, GetReadableTexture(sprite), eyeLocals);
        }

        void ScanEyeLaserOverlay(Sprite sprite, ref Color32[] overlayPx, ref int overlayW, ref int overlayH)
        {
            if (DynamicSpriteRefresh)
            {
                CollectEyeLaserOverlayFromTexture(sprite, GetReadableTexture(sprite), ref overlayPx, ref overlayW, ref overlayH);
                return;
            }

            if (LimbSkinIndex >= 1)
            {
                string folder = "Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/";
                string idx = LimbSkinIndex.ToString();
                TryCollectEyeLaserOverlayLoaded(sprite, folder + "LimbSpriteSkin" + idx + ".png", ref overlayPx, ref overlayW, ref overlayH);
                TryCollectEyeLaserOverlayLoaded(sprite, folder + "LimbSpriteFlesh" + idx + ".png", ref overlayPx, ref overlayW, ref overlayH);
                TryCollectEyeLaserOverlayLoaded(sprite, folder + "LimbSpriteBone" + idx + ".png", ref overlayPx, ref overlayW, ref overlayH);
                TryCollectEyeLaserOverlayLoaded(sprite, folder + "LimbSpriteDamage" + idx + ".png", ref overlayPx, ref overlayW, ref overlayH);
            }

            Material mat = limbRenderer.material;
            if (mat != null)
            {
                if (mat.HasProperty("_FleshTex"))
                    CollectEyeLaserOverlayFromTexture(sprite, mat.GetTexture("_FleshTex") as Texture2D, ref overlayPx, ref overlayW, ref overlayH);
                if (mat.HasProperty("_BoneTex"))
                    CollectEyeLaserOverlayFromTexture(sprite, mat.GetTexture("_BoneTex") as Texture2D, ref overlayPx, ref overlayW, ref overlayH);
                if (mat.HasProperty("_DamageTex"))
                    CollectEyeLaserOverlayFromTexture(sprite, mat.GetTexture("_DamageTex") as Texture2D, ref overlayPx, ref overlayW, ref overlayH);
            }

            CollectEyeLaserOverlayFromTexture(sprite, GetReadableTexture(sprite), ref overlayPx, ref overlayW, ref overlayH);
        }

        void TryCollectEyeLaserOverlayLoaded(Sprite sprite, string path, ref Color32[] overlayPx, ref int overlayW, ref int overlayH)
        {
            try
            {
                Sprite loaded = ModAPI.LoadSprite(path);
                if (loaded != null && loaded.texture != null)
                {
                    Sprite layout = loaded;
                    if (Mathf.RoundToInt(loaded.rect.width) == Mathf.RoundToInt(sprite.rect.width)
                        && Mathf.RoundToInt(loaded.rect.height) == Mathf.RoundToInt(sprite.rect.height))
                        layout = sprite;
                    CollectEyeLaserOverlayFromTexture(layout, loaded.texture, ref overlayPx, ref overlayW, ref overlayH);
                }
            }
            catch
            {
            }
        }

        void CollectEyeLaserOverlayFromTexture(Sprite sprite, Texture2D source, ref Color32[] overlayPx, ref int overlayW, ref int overlayH)
        {
            if (sprite == null || source == null) return;

            Texture2D readable = DuplicateTexture(source);
            if (readable == null) return;

            Rect rect = sprite.rect;
            int texW = source.width;
            int texH = source.height;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            Color32[] all = readable.GetPixels32();

            if (overlayW != w || overlayH != h || overlayPx == null || overlayPx.Length != w * h)
            {
                overlayW = w;
                overlayH = h;
                overlayPx = new Color32[w * h];
            }

            Color32 glow32 = (Color32)EyeGlowAltColor;
            glow32.a = 255;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcIdx = (y0 + y) * texW + (x0 + x);
                    if (srcIdx < 0 || srcIdx >= all.Length) continue;
                    if (!MatchesEyeLaser(all[srcIdx])) continue;
                    overlayPx[y * w + x] = glow32;
                }
            }
        }

        void TryCollectEyeLaserLoaded(Sprite sprite, string path, List<Vector2> eyeLocals)
        {
            try
            {
                Sprite loaded = ModAPI.LoadSprite(path);
                if (loaded != null && loaded.texture != null)
                {
                    Sprite layout = loaded;
                    if (Mathf.RoundToInt(loaded.rect.width) == Mathf.RoundToInt(sprite.rect.width)
                        && Mathf.RoundToInt(loaded.rect.height) == Mathf.RoundToInt(sprite.rect.height))
                        layout = sprite;
                    CollectEyeLaserFromTexture(layout, loaded.texture, eyeLocals);
                }
            }
            catch
            {
            }
        }

        void CollectEyeLaserFromTexture(Sprite sprite, Texture2D source, List<Vector2> eyeLocals)
        {
            if (sprite == null || source == null) return;

            Texture2D readable = DuplicateTexture(source);
            if (readable == null) return;

            Rect rect = sprite.rect;
            int texW = source.width;
            int texH = source.height;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            Color32[] all = readable.GetPixels32();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcIdx = (y0 + y) * texW + (x0 + x);
                    if (srcIdx < 0 || srcIdx >= all.Length) continue;
                    if (!MatchesEyeLaser(all[srcIdx])) continue;
                    eyeLocals.Add(PixelToSpriteLocal(sprite, x0 + x + 0.5f, y0 + y + 0.5f));
                }
            }
        }

        bool MatchesEyeLaser(Color32 p)
        {
            if (p.a < 25) return false;
            if (EyeLaserMarker == EyeLaserMarkerMode.Piccolo)
                return p.r == MarkerPiccoloEyeExact.r && p.g == MarkerPiccoloEyeExact.g && p.b == MarkerPiccoloEyeExact.b;
            return Mathf.Abs(p.r - MarkerEyeLaserExact.r) <= EyeLaserMatchTolerance
                && Mathf.Abs(p.g - MarkerEyeLaserExact.g) <= EyeLaserMatchTolerance
                && Mathf.Abs(p.b - MarkerEyeLaserExact.b) <= EyeLaserMatchTolerance;
        }

        static Vector2 GetPiccoloEyeFallbackLocal(Sprite sprite)
        {
            Rect rect = sprite.rect;
            return PixelToSpriteLocal(sprite, rect.x + 8.5f, rect.y + 5.5f);
        }

        static Vector2[] BuildEyeLaserClusters(List<Vector2> points, float mergeRadius)
        {
            if (points == null || points.Count == 0) return new Vector2[0];

            var groups = new List<List<Vector2>>();
            float mergeRadiusSq = mergeRadius * mergeRadius;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                int best = -1;
                float bestDist = mergeRadiusSq;
                for (int g = 0; g < groups.Count; g++)
                {
                    Vector2 center = AveragePointList(groups[g]);
                    float dist = (p - center).sqrMagnitude;
                    if (dist <= bestDist)
                    {
                        best = g;
                        bestDist = dist;
                    }
                }

                if (best < 0)
                    groups.Add(new List<Vector2> { p });
                else
                    groups[best].Add(p);
            }

            var result = new Vector2[groups.Count];
            for (int i = 0; i < groups.Count; i++)
                result[i] = AveragePointList(groups[i]);
            return result;
        }

        static Vector2 AveragePointList(List<Vector2> points)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
                sum += points[i];
            return sum / points.Count;
        }

        void Update()
        {
            if (overlayRenderer != null)
            {
                float fadeSpeed = glowTarget > glow01 ? 2.5f : 5f;
                glow01 = Mathf.MoveTowards(glow01, glowTarget, Time.deltaTime * fadeSpeed);
                float pulse = pulseWhileActive && glow01 > 0.05f
                    ? 0.65f + 0.35f * Mathf.Sin(Time.time * pulseSpeed)
                    : glow01 > 0.05f ? 0.85f : 0f;
                float a = glow01 * pulse;
                overlayRenderer.color = new Color(GlowPurple.r, GlowPurple.g, GlowPurple.b, a);
            }

            if (eyeOverlayRenderer != null)
            {
                float fadeSpeed = eyeGlowTarget > eyeGlow01 ? 2.5f : 5f;
                eyeGlow01 = Mathf.MoveTowards(eyeGlow01, eyeGlowTarget, Time.deltaTime * fadeSpeed);
                float pulse = eyePulseWhileActive && eyeGlow01 > 0.05f
                    ? 0.65f + 0.35f * Mathf.Sin(Time.time * pulseSpeed)
                    : eyeGlow01 > 0.05f ? 0.85f : 0f;
                float a = eyeGlow01 * pulse;
                eyeOverlayRenderer.color = new Color(EyeGlowColor.r, EyeGlowColor.g, EyeGlowColor.b, a);
            }
        }

        void OnDestroy()
        {
            ClearOverlay();
            ClearEyeOverlay();
        }

        int ClassifySpotRole(Color32 p)
        {
            if (p.a < 25) return SpotRoleNone;

            if (MatchesTailFire(p))
                return SpotRoleFire;

            if (!MatchesAnySpot(p))
                return SpotRoleNone;

            return IsTail ? SpotRoleGlowOnly : SpotRoleFire;
        }

        static bool MatchesTailFire(Color32 p)
        {
            if (MatchesAnyExact(p, MarkerTailFireExact))
                return true;
            return MatchesAnyTolerance(p, MarkerTailFireExact, TailFireMatchTolerance);
        }

        static bool MatchesAnySpot(Color32 p)
        {
            if (MatchesAnyExact(p, MarkerGreenExact))
                return true;
            if (MatchesAnyExact(p, MarkerRedExact))
                return true;
            if (MatchesAnyTolerance(p, MarkerGreenExact, GreenMatchTolerance))
                return true;
            if (MatchesAnyTolerance(p, MarkerRedExact, RedMatchTolerance))
                return true;
            if (MatchesAnyExact(p, MarkerTailFireExact))
                return true;
            if (MatchesAnyTolerance(p, MarkerTailFireExact, TailFireMatchTolerance))
                return true;

            if (Mathf.Abs(p.r - 0x27) <= GreenMatchTolerance
                && Mathf.Abs(p.g - 0x30) <= GreenMatchTolerance
                && Mathf.Abs(p.b - 0x17) <= GreenMatchTolerance)
                return true;

            if (Mathf.Abs(p.r - 0x2e) <= RedMatchTolerance
                && p.g <= RedMatchTolerance + 2
                && p.b <= RedMatchTolerance + 4)
                return true;

            if (Mathf.Abs(p.r - 0x44) <= PurpleMatchTolerance
                && Mathf.Abs(p.g - 0x05) <= PurpleMatchTolerance
                && Mathf.Abs(p.b - 0x10) <= PurpleMatchTolerance)
                return true;

            if (MatchesRedHeuristic(p))
                return true;

            return false;
        }

        static bool MatchesAnyExact(Color32 p, Color32[] markers)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                Color32 m = markers[i];
                if (p.r == m.r && p.g == m.g && p.b == m.b)
                    return true;
            }
            return false;
        }

        static bool MatchesAnyTolerance(Color32 p, Color32[] markers, int tolerance)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                Color32 m = markers[i];
                if (Mathf.Abs(p.r - m.r) <= tolerance
                    && Mathf.Abs(p.g - m.g) <= tolerance
                    && Mathf.Abs(p.b - m.b) <= tolerance)
                    return true;
            }
            return false;
        }

        static bool MatchesRedHeuristic(Color32 p)
        {
            if (p.r >= 18 && p.r <= 58 && p.g <= RedMatchTolerance + 2 && p.b <= RedMatchTolerance + 4)
            {
                if (p.r > p.g + 8 && p.r > p.b + 8)
                    return true;
            }

            if (p.g >= 18 && p.g <= 48 && p.r <= RedMatchTolerance && p.b <= RedMatchTolerance)
            {
                if (p.g > p.r + 8 && p.g > p.b + 8)
                    return true;
            }

            if (p.b >= 18 && p.b <= 48 && p.r <= RedMatchTolerance && p.g <= RedMatchTolerance)
            {
                if (p.b > p.r + 8 && p.b > p.g + 8)
                    return true;
            }

            return false;
        }

        static Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.filterMode = FilterMode.Point;
                copy.wrapMode = source.wrapMode;
                copy.SetPixels32(source.GetPixels32());
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                return null;
            }
        }

        static Texture2D GetReadableTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;
            return DuplicateTexture(sprite.texture);
        }

        static Vector2 PixelToSpriteLocal(Sprite sprite, float pixelX, float pixelY)
        {
            Rect rect = sprite.rect;
            Bounds bounds = sprite.bounds;
            float u = (pixelX - rect.x) / rect.width;
            float v = (pixelY - rect.y) / rect.height;
            return new Vector2(
                Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                Mathf.Lerp(bounds.min.y, bounds.max.y, v));
        }
    }
}
