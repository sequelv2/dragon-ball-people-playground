using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // Window behaviours for the editor tools, which manage their own panels.
    static class MenuUI
    {
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
            img.raycastTarget = true;
            DbSkin.StyleResizeHandle(img, corner == ModMenuDragResize.ResizeCorner.BottomRight, handleSize);
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
