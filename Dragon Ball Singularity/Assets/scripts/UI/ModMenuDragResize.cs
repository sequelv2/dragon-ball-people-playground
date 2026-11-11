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
    public class ModMenuDragResize : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public enum ResizeCorner { BottomRight, BottomLeft, TopRight, TopLeft }

        public bool resize;
        public ResizeCorner resizeCorner = ResizeCorner.BottomRight;
        public RectTransform moveRoot;
        public float minUniform = 5f;
        public float maxUniform = 18f;
        public Func<float> getUniform;
        public Action<float> setUniform;

        Vector2 _startScreen;
        Vector2 _startAnchoredPos;
        float _startUniform;

        public void OnPointerDown(PointerEventData eventData)
        {
            _startScreen = eventData.position;
            if (resize)
                _startUniform = getUniform != null ? getUniform() : 1f;
            else if (moveRoot != null)
                _startAnchoredPos = moveRoot.anchoredPosition;
        }

        float GetResizeDragDelta(Vector2 screenDelta)
        {
            switch (resizeCorner)
            {
                case ResizeCorner.BottomRight:
                    return screenDelta.x - screenDelta.y;

                case ResizeCorner.BottomLeft:
                    return -screenDelta.x - screenDelta.y;

                case ResizeCorner.TopRight:
                    return screenDelta.x + screenDelta.y;

                case ResizeCorner.TopLeft:
                    return -screenDelta.x + screenDelta.y;

                default:
                    return screenDelta.x + screenDelta.y;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (resize)
            {
                if (setUniform == null) return;
                Vector2 screenDelta = eventData.position - _startScreen;
                float delta = GetResizeDragDelta(screenDelta) * 0.012f;
                setUniform(Mathf.Clamp(_startUniform + delta, minUniform, maxUniform));
                return;
            }

            if (moveRoot == null || moveRoot.parent == null) return;
            var parentRt = moveRoot.parent as RectTransform;
            if (parentRt == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, eventData.pressEventCamera, out Vector2 localNow);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, _startScreen, eventData.pressEventCamera, out Vector2 localStart);
            moveRoot.anchoredPosition = _startAnchoredPos + (localNow - localStart);
        }
    }
}
