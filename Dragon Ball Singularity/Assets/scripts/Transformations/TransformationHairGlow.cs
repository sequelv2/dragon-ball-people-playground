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
    public class TransformationHairGlow : MonoBehaviour
    {
        const int MinHairAlpha = 25;

        SpriteRenderer hairRenderer;
        SpriteRenderer overlayRenderer;
        string currentFormId;
        Sprite lastScannedSprite;

        public void SetForm(string formId)
        {
            currentFormId = formId;
            lastScannedSprite = null;
            Refresh();
        }

        public static void RefreshAll()
        {
            var all = FindObjectsOfType<TransformationHairGlow>();
            for (int i = 0; i < all.Length; i++)
                all[i].Refresh();
        }

        static bool ShouldGlowForForm(string formId)
        {
            return !string.IsNullOrEmpty(formId) && formId != "Base";
        }

        bool IsAntennaCharacter()
        {
            Transform root = transform.root;
            return root != null && root.GetComponent<PiccoloGiantForm>() != null;
        }

        static float GetGlowAlpha()
        {
            return TransformationGlowUtility.PercentToAlpha(DBPU.TransformationHairGlowBrightness);
        }

        public void Refresh()
        {
            ClearOverlay();
            lastScannedSprite = null;

            if (!DBPU.TransformationHairGlowEnabled) return;
            if (IsAntennaCharacter())
            {
                ClearOverlay();
                return;
            }
            if (!ShouldGlowForForm(currentFormId)) return;
            if (hairRenderer == null) hairRenderer = GetComponent<SpriteRenderer>();
            if (hairRenderer == null || hairRenderer.sprite == null) return;

            RebuildOverlay(hairRenderer.sprite);
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
            var go = new GameObject("TransformationHairGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            overlayRenderer = go.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = overlaySprite;
            overlayRenderer.material = bright != null ? bright : hairRenderer.sharedMaterial;
            overlayRenderer.sortingLayerName = hairRenderer.sortingLayerName;
            overlayRenderer.sortingOrder = hairRenderer.sortingOrder + 1;
            overlayRenderer.color = new Color(1f, 1f, 1f, GetGlowAlpha());
        }

        void ScanSprite(Sprite sprite, Texture2D source, ref Color32[] overlayPx, ref int overlayW, ref int overlayH, ref bool foundAny)
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

                    Color32 p = all[srcIdx];
                    if (p.a < MinHairAlpha) continue;

                    foundAny = true;
                    overlayPx[y * w + x] = p;
                }
            }
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
            if (!DBPU.TransformationHairGlowEnabled || !ShouldGlowForForm(currentFormId) || IsAntennaCharacter())
            {
                if (overlayRenderer != null)
                    overlayRenderer.gameObject.SetActive(false);
                return;
            }

            if (hairRenderer == null) hairRenderer = GetComponent<SpriteRenderer>();
            if (hairRenderer == null || hairRenderer.sprite == null) return;

            if (overlayRenderer != null && !overlayRenderer.gameObject.activeSelf)
                overlayRenderer.gameObject.SetActive(true);

            float glowAlpha = GetGlowAlpha();
            if (overlayRenderer != null)
            {
                Color c = overlayRenderer.color;
                if (Mathf.Abs(c.a - glowAlpha) > 0.001f)
                    overlayRenderer.color = new Color(c.r, c.g, c.b, glowAlpha);
            }

            if (hairRenderer.sprite != lastScannedSprite)
                RebuildOverlay(hairRenderer.sprite);
        }

        void OnDestroy()
        {
            ClearOverlay();
        }
    }
}
