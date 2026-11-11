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
    public class KiChargeRisingBlock : MonoBehaviour
    {
        public float targetHeight = 2.5f;
        public float riseDuration = 1.8f;
        public float rotationSpeed = 16f;
        public float fadeOutEndAtHeight = 0.92f;

        private Vector3 _startPos;
        private Vector3 _baseScale;
        private float _riseProgress;
        private bool _reachedTop;
        private float _phase;
        private SpriteRenderer _sr;
        private Color _baseColor;

        void Start()
        {
            _startPos = transform.position;
            _baseScale = transform.localScale;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.black;
        }

        private void ApplyFadeForHeightProgress(float smoothT)
        {
            if (_sr == null) return;
            float end = Mathf.Clamp(fadeOutEndAtHeight, 0.15f, 1f);
            float k = Mathf.Clamp01(smoothT / end);
            float alpha = _baseColor.a * (1f - k * k * (3f - 2f * k));
            _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
        }

        void Update()
        {
            if (!_reachedTop)
            {
                _riseProgress += Time.deltaTime / Mathf.Max(0.25f, riseDuration);
                float t = Mathf.Clamp01(_riseProgress);
                float smoothT = t * t * (3f - 2f * t);
                Vector3 top = _startPos + Vector3.up * targetHeight;
                transform.position = Vector3.Lerp(_startPos, top, smoothT);
                transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
                float pulse = 1f + Mathf.Sin(Time.time * 0.95f + _phase) * 0.042f;
                transform.localScale = _baseScale * pulse;
                ApplyFadeForHeightProgress(smoothT);
                if (_riseProgress >= 1f)
                    _reachedTop = true;
            }
            else
            {
                Vector3 top = _startPos + Vector3.up * targetHeight;
                float bob = Mathf.Sin(Time.time * 0.7f + _phase) * 0.05f;
                transform.position = top + Vector3.up * bob;
                transform.Rotate(0, 0, rotationSpeed * 0.32f * Time.deltaTime);
                float pulse = 1f + Mathf.Sin(Time.time * 0.8f + _phase) * 0.03f;
                transform.localScale = _baseScale * pulse;
                if (_sr != null)
                    _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
            }
        }
    }
}
