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
    public class PowerMenuConfigSliderInteractor : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform trackRt;
        public RectTransform handleRt;
        public TextMeshProUGUI valueText;
        public float min;
        public float max;
        public float trackHalfW;
        public Func<float> readDisplay;
        public Action<float> writeDisplay;
        public Func<string> formatDisplay;
        public Action persist;

        bool _dirty;

        public void RefreshVisual()
        {
            if (readDisplay == null) return;
            float display = readDisplay();
            float t = max > min ? Mathf.Clamp01((display - min) / (max - min)) : 0f;
            if (handleRt != null)
                handleRt.anchoredPosition = new Vector2(-trackHalfW + t * trackHalfW * 2f, handleRt.anchoredPosition.y);
            if (valueText != null && formatDisplay != null)
                valueText.text = formatDisplay();
        }

        void ApplyFromEvent(PointerEventData e)
        {
            if (trackRt == null || writeDisplay == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRt, e.position, e.pressEventCamera, out Vector2 local);
            float half = Mathf.Max(0.5f, trackHalfW);
            float t = Mathf.Clamp01((local.x + half) / (half * 2f));
            writeDisplay(Mathf.Lerp(min, max, t));
            _dirty = true;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData) => ApplyFromEvent(eventData);
        public void OnDrag(PointerEventData eventData) => ApplyFromEvent(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dirty) return;
            _dirty = false;
            persist?.Invoke();
        }
    }
}
