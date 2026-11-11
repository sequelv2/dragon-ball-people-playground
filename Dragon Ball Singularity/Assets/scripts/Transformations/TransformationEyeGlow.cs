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
    public class TransformationEyeGlow : MonoBehaviour
    {
        enum EyeGlowTier { None, SuperSaiyan, God, Blue, Orange, BeastRed }

        static readonly Color32[] SuperSaiyanMarkers =
        {
            new Color32(0x75, 0xcf, 0xac, 255),
            new Color32(0x18, 0x64, 0x68, 255),
            new Color32(0x5e, 0xb4, 0x9b, 255)
        };

        static readonly Color32[] GodMarkers =
        {
            new Color32(0xfb, 0x5a, 0x7e, 255),
            new Color32(0xde, 0x40, 0x64, 255)
        };

        static readonly Color32[] BlueMarkers =
        {
            new Color32(0x0d, 0xc3, 0xd0, 255),
            new Color32(0x67, 0xda, 0xe4, 255),
            new Color32(0x15, 0xcb, 0xe1, 255),
            new Color32(0x0d, 0xb1, 0xc6, 255),
            new Color32(0xa6, 0xf3, 0xf9, 255),
            new Color32(0x59, 0xd3, 0xdf, 255),
            new Color32(0x78, 0xeb, 0xf4, 255)
        };

        static readonly Color32[] BlueExcludeMarkers =
        {
            new Color32(0x07, 0x9d, 0xb5, 255),
            new Color32(0x09, 0xac, 0xc4, 255)
        };

        static readonly Color32[] OrangeEyeMarkers =
        {
            new Color32(0xff, 0xca, 0x18, 255)
        };

        static readonly Color32[] BeastRedMarkers =
        {
            new Color32(0xdc, 0x06, 0x1b, 255)
        };

        const int MatchTolerance = 5;

        SpriteRenderer limbRenderer;
        SpriteRenderer overlayRenderer;
        string currentFormId;
        Sprite lastScannedSprite;
        Color32[] activeMarkers;
        Color32[] activeExcludeMarkers;

        static float GetGlowAlpha()
        {
            return TransformationGlowUtility.PercentToAlpha(DBPU.TransformationHairGlowBrightness);
        }

        public void Initialize(string formId)
        {
            limbRenderer = GetComponent<SpriteRenderer>();
            SetForm(formId);
        }

        public void SetForm(string formId)
        {
            currentFormId = formId;
            activeMarkers = GetMarkersForForm(formId);
            activeExcludeMarkers = GetExcludeMarkersForForm(formId);
            lastScannedSprite = null;
            Refresh();
        }

        public static void RefreshAll()
        {
            var all = FindObjectsOfType<TransformationEyeGlow>();
            for (int i = 0; i < all.Length; i++)
                all[i].Refresh();
        }

        public void Refresh()
        {
            ClearOverlay();
            lastScannedSprite = null;

            if (!DBPU.TransformationEyeGlowEnabled) return;
            if (activeMarkers == null || activeMarkers.Length == 0) return;
            if (limbRenderer == null) limbRenderer = GetComponent<SpriteRenderer>();
            if (limbRenderer == null || limbRenderer.sprite == null) return;

            RebuildOverlay(limbRenderer.sprite);
        }

        void RebuildOverlay(Sprite sprite)
        {
            lastScannedSprite = sprite;

            var overlayPx = new Color32[0];
            int overlayW = 0;
            int overlayH = 0;
            bool foundAny = false;

            Texture2D readable = GetReadableTexture(sprite);
            if (readable != null)
                ScanSprite(sprite, readable, ref overlayPx, ref overlayW, ref overlayH, ref foundAny);

            if (!foundAny) return;

            CreateOverlay(sprite, overlayPx, overlayW, overlayH);
        }

        void ClearOverlay()
        {
            if (overlayRenderer != null)
            {
                Destroy(overlayRenderer.gameObject);
                overlayRenderer = null;
            }
        }

        void CreateOverlay(Sprite sprite, Color32[] overlayPx, int overlayW, int overlayH)
        {
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
            var go = new GameObject("TransformationEyeGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            overlayRenderer = go.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = overlaySprite;
            overlayRenderer.material = bright != null ? bright : limbRenderer.sharedMaterial;
            overlayRenderer.sortingLayerName = limbRenderer.sortingLayerName;
            overlayRenderer.sortingOrder = limbRenderer.sortingOrder + 8;
            overlayRenderer.color = new Color(1f, 1f, 1f, GetGlowAlpha());
        }

        void ScanSprite(Sprite sprite, Texture2D source, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAny)
        {
            if (sprite == null || source == null || activeMarkers == null) return;

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

                    Color32 p = all[srcIdx];
                    if (p.a < 25 || !MatchesAnyMarker(p)) continue;

                    foundAny = true;
                    Color32 glow = p;
                    glow.a = 255;
                    overlayPx[y * w + x] = glow;
                }
            }
        }

        bool MatchesAnyMarker(Color32 p)
        {
            if (activeExcludeMarkers != null && activeExcludeMarkers.Length > 0)
            {
                if (MatchesAnyExact(p, activeExcludeMarkers)) return false;
                if (MatchesAnyTolerance(p, activeExcludeMarkers, MatchTolerance)) return false;
            }

            if (MatchesAnyExact(p, activeMarkers)) return true;
            return MatchesAnyTolerance(p, activeMarkers, MatchTolerance);
        }

        static EyeGlowTier GetTier(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return EyeGlowTier.None;

            switch (formId)
            {
                case "Ikari":
                    return EyeGlowTier.Orange;
                case "BeastGohan":
                    return EyeGlowTier.BeastRed;
                case "SSJ1":
                case "SSJGrade2":
                case "PSSJ":
                case "SSJ2":
                case "SSJ3":
                case "Ultimate":
                case "FirstForm":
                case "SecondForm":
                case "ThirdForm":
                case "FinalForm":
                case "FullPower":
                case "Dripstinct":
                case "MasteredDripstinct":
                    return EyeGlowTier.SuperSaiyan;
                case "SSJGod":
                case "GoldenForm":
                    return EyeGlowTier.God;
                case "SSJBlue":
                case "PSSJBlue":
                case "SSJBlueEvolution":
                    return EyeGlowTier.Blue;
                default:
                    return EyeGlowTier.None;
            }
        }

        static Color32[] GetMarkersForForm(string formId)
        {
            switch (GetTier(formId))
            {
                case EyeGlowTier.SuperSaiyan: return SuperSaiyanMarkers;
                case EyeGlowTier.God: return GodMarkers;
                case EyeGlowTier.Blue: return BlueMarkers;
                case EyeGlowTier.Orange: return OrangeEyeMarkers;
                case EyeGlowTier.BeastRed: return BeastRedMarkers;
                default: return null;
            }
        }

        static Color32[] GetExcludeMarkersForForm(string formId)
        {
            if (GetTier(formId) == EyeGlowTier.Blue) return BlueExcludeMarkers;
            return null;
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

        void Update()
        {
            if (!DBPU.TransformationEyeGlowEnabled)
            {
                if (overlayRenderer != null)
                    overlayRenderer.gameObject.SetActive(false);
                return;
            }

            if (limbRenderer == null) limbRenderer = GetComponent<SpriteRenderer>();
            if (limbRenderer == null || limbRenderer.sprite == null) return;

            if (overlayRenderer != null && !overlayRenderer.gameObject.activeSelf)
                overlayRenderer.gameObject.SetActive(true);

            if (overlayRenderer != null)
            {
                float glowAlpha = GetGlowAlpha();
                Color c = overlayRenderer.color;
                if (Mathf.Abs(c.a - glowAlpha) > 0.001f)
                    overlayRenderer.color = new Color(c.r, c.g, c.b, glowAlpha);
            }

            if (limbRenderer.sprite != lastScannedSprite && activeMarkers != null && activeMarkers.Length > 0)
                RebuildOverlay(limbRenderer.sprite);
        }

        void OnDestroy()
        {
            ClearOverlay();
        }
    }
}
