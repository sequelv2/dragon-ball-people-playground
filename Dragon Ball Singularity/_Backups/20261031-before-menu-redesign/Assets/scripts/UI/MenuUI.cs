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
    static class MenuUI
    {
        public static Vector2 SpritePixels(Sprite sprite)
        {
            if (sprite == null) return Vector2.one;
            return new Vector2(sprite.rect.width, sprite.rect.height);
        }

        public static float UniformScaleToFit(Sprite sprite, Vector2 targetSize)
        {
            Vector2 px = SpritePixels(sprite);
            if (px.x <= 0f || px.y <= 0f) return 1f;
            return Mathf.Min(targetSize.x / px.x, targetSize.y / px.y);
        }

        public static float ResolveUniformScale(Sprite sprite, Vector2 targetSize, float uniformScaleOverride)
        {
            if (uniformScaleOverride > 0f) return uniformScaleOverride;
            return UniformScaleToFit(sprite, targetSize);
        }

        public static RectTransform ApplyImage(GameObject go, Sprite sprite, Vector2 anchoredPos, float uniformScale)
        {
            Image img = go.GetOrAddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = SpritePixels(sprite);
            rt.localScale = new Vector3(uniformScale, uniformScale, 1f);
            return rt;
        }

        public static Vector2 GetRenderedSize(RectTransform rt)
        {
            return new Vector2(
                Mathf.Abs(rt.sizeDelta.x * rt.localScale.x),
                Mathf.Abs(rt.sizeDelta.y * rt.localScale.y));
        }

        public static void FitIconInsideFrame(RectTransform iconRt, Sprite iconSprite, RectTransform frameRt, float paddingFraction, float scaleX, float scaleY, bool IsDefaultIcon = false)
        {
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = SpritePixels(iconSprite);
            float sx = scaleX > 0.001f ? scaleX : 0.94f;
            float sy = scaleY > 0.001f ? scaleY : sx;
            iconRt.localScale = new Vector3(IsDefaultIcon ? sx : sx / 14.2857f, IsDefaultIcon ? sy : sy / 14.2857f, 1f);
        }

        public static void DisableRaycast(Image img)
        {
            if (img != null) img.raycastTarget = false;
        }

        public static void ConfigureTitleDragArea(GameObject titleGo, RectTransform moveRoot, Vector2 minSize)
        {
            if (titleGo == null) return;

            var rt = titleGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(
                    Mathf.Max(Mathf.Abs(rt.sizeDelta.x), minSize.x),
                    Mathf.Max(Mathf.Abs(rt.sizeDelta.y), minSize.y));
            }

            var text = titleGo.GetComponent<TextMeshProUGUI>();
            if (text != null) text.raycastTarget = true;

            var img = titleGo.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;

            var drag = titleGo.GetComponent<ModMenuDragResize>();
            if (drag == null) drag = titleGo.AddComponent<ModMenuDragResize>();
            drag.moveRoot = moveRoot;
        }

        public static void AttachResizeHandle(Transform panelParent, Func<float> getUniform, Action<float> setUniform, float minUniform = 5f, float maxUniform = 18f, float handleSize = 10f)
        {
            AttachOneResizeHandle(panelParent, getUniform, setUniform, minUniform, maxUniform, handleSize, ModMenuDragResize.ResizeCorner.BottomRight);
            AttachOneResizeHandle(panelParent, getUniform, setUniform, minUniform, maxUniform, handleSize, ModMenuDragResize.ResizeCorner.BottomLeft);
            AttachOneResizeHandle(panelParent, getUniform, setUniform, minUniform, maxUniform, handleSize, ModMenuDragResize.ResizeCorner.TopRight);
            AttachOneResizeHandle(panelParent, getUniform, setUniform, minUniform, maxUniform, handleSize, ModMenuDragResize.ResizeCorner.TopLeft);
        }

        static void AttachOneResizeHandle(Transform panelParent, Func<float> getUniform, Action<float> setUniform, float minUniform, float maxUniform, float handleSize, ModMenuDragResize.ResizeCorner corner)
        {
            Vector2 anchor;
            switch (corner)
            {
                case ModMenuDragResize.ResizeCorner.BottomLeft: anchor = new Vector2(0f, 0f); break;
                case ModMenuDragResize.ResizeCorner.TopRight: anchor = new Vector2(1f, 1f); break;
                case ModMenuDragResize.ResizeCorner.TopLeft: anchor = new Vector2(0f, 1f); break;
                default: anchor = new Vector2(1f, 0f); break;
            }

            GameObject handle = new GameObject("MenuResizeHandle_" + corner);
            handle.transform.SetParent(panelParent, false);
            RectTransform rt = handle.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(1f - anchor.x, 1f - anchor.y);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(handleSize, handleSize);
            Image img = handle.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);
            img.raycastTarget = true;
            var drag = handle.AddComponent<ModMenuDragResize>();
            drag.resize = true;
            drag.resizeCorner = corner;
            drag.minUniform = minUniform;
            drag.maxUniform = maxUniform;
            drag.getUniform = getUniform;
            drag.setUniform = setUniform;
            handle.transform.SetAsLastSibling();
        }
    }
}
