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
    public static class DbpuConfigMenuUI
    {
        static GameObject _root;
        static GameObject _panelHost;
        static RectTransform _panelHostRt;
        static RectTransform _panelRt;
        static GameObject _titleBarGo;
        static TextMeshProUGUI _titleTmp;
        static Action _onClose;
        static Action _onSliderChanged;
        static bool _uiBlocked;
        static TMP_FontAsset _font;
        static DbpuMenuSlider[] _sliders;

        const float UiScale = 9f;
        const float PanelWidth = 56f;
        const float BtnWidth = 50f;
        const float BtnHeight = 6f;
        const float BtnStep = 6.8f;
        const float SliderRowHeight = 12f;
        const float SliderStep = 12.8f;
        const float TitleBarH = 8f;
        const float BodyGap = 1.5f;
        const float PadBottom = 4f;
        const float MaxBodyHeight = 76f;
        const float CloseBtnScale = 0.45f;
        const float CloseBtnGap = 2.5f;

        static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        static readonly Color TitleBarBg = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color BtnBg = new Color(0.18f, 0.18f, 0.18f, 1f);
        static readonly Color BtnHighlight = new Color(0.28f, 0.3f, 0.34f, 1f);
        static readonly Color BtnPressed = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color SliderTrackBg = new Color(0.1f, 0.1f, 0.1f, 1f);
        static readonly Color SliderFillBg = new Color(0.35f, 0.55f, 0.85f, 1f);
        static readonly Color SliderHandleBg = new Color(0.92f, 0.92f, 0.92f, 1f);

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static bool IsOpen => _root != null;

        public static void Open(string title, Action onClose, params DbpuMenuButton[] buttons)
        {
            Open(title, onClose, buttons, null, null);
        }

        public static void Open(string title, Action onClose, DbpuMenuButton[] buttons, DbpuMenuSlider[] sliders, Action onSliderChanged)
        {
            if ((buttons == null || buttons.Length == 0) && (sliders == null || sliders.Length == 0)) return;

            _onClose = onClose;
            _onSliderChanged = onSliderChanged;
            _sliders = sliders;

            if (_root != null)
            {
                _root.SetActive(true);
                EnsureUiBlocker();
                SetContent(title, buttons ?? Array.Empty<DbpuMenuButton>(), sliders);
                return;
            }

            _root = new GameObject("DbpuConfigMenuUI");
            _root.transform.SetParent(GameObject.Find("Canvas").transform, false);
            RectTransform rootRt = _root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            GameObject blocker = new GameObject("Blocker");
            blocker.transform.SetParent(_root.transform, false);
            RectTransform blockerRt = blocker.AddComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            Image blockerImg = blocker.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.35f);
            blockerImg.raycastTarget = true;

            _panelHost = new GameObject("PanelHost");
            _panelHost.transform.SetParent(_root.transform, false);
            _panelHostRt = _panelHost.AddComponent<RectTransform>();
            _panelHostRt.anchoredPosition = Vector2.zero;
            _panelHostRt.localScale = new Vector3(UiScale, UiScale, 1f);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(_panelHost.transform, false);
            _panelRt = panel.AddComponent<RectTransform>();
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelBg;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = Color.white;
            panelOutline.effectDistance = new Vector2(1.2f, -1.2f);

            _titleBarGo = new GameObject("TitleBar");
            _titleBarGo.transform.SetParent(panel.transform, false);
            RectTransform titleBarRt = _titleBarGo.AddComponent<RectTransform>();
            titleBarRt.anchorMin = new Vector2(0.5f, 1f);
            titleBarRt.anchorMax = new Vector2(0.5f, 1f);
            titleBarRt.pivot = new Vector2(0.5f, 1f);
            titleBarRt.anchoredPosition = Vector2.zero;
            titleBarRt.sizeDelta = new Vector2(PanelWidth, TitleBarH);
            Image titleBarImg = _titleBarGo.AddComponent<Image>();
            titleBarImg.color = TitleBarBg;
            Outline titleBarOutline = _titleBarGo.AddComponent<Outline>();
            titleBarOutline.effectColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            titleBarOutline.effectDistance = new Vector2(0.8f, -0.8f);

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_titleBarGo.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(4f, 0f);
            titleRt.offsetMax = new Vector2(-4f, 0f);
            _titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            _titleTmp.font = Font;
            _titleTmp.fontSize = 3.6f;
            _titleTmp.alignment = TextAlignmentOptions.Center;
            _titleTmp.color = Color.white;
            _titleTmp.raycastTarget = false;

            MenuUI.ConfigureTitleDragArea(_titleBarGo, _panelHostRt, new Vector2(PanelWidth, TitleBarH));

            EnsureUiBlocker();
            SetContent(title, buttons ?? Array.Empty<DbpuMenuButton>(), sliders);
        }

        static void EnsureUiBlocker()
        {
            if (_uiBlocked) return;
            Global.main.AddUiBlocker();
            _uiBlocked = true;
        }

        static void ClearResizeHandles()
        {
            if (_panelRt == null) return;
            for (int i = _panelRt.childCount - 1; i >= 0; i--)
            {
                Transform child = _panelRt.GetChild(i);
                if (child.name.StartsWith("MenuResizeHandle_"))
                    GameObject.Destroy(child.gameObject);
            }
        }

        static float ContentHeight(int buttonCount, int sliderCount)
        {
            return buttonCount * BtnStep + sliderCount * SliderStep + 1f;
        }

        static bool TryRefreshButtons(Transform listRoot, DbpuMenuButton[] buttons, out float panelHeight)
        {
            panelHeight = 0f;
            if (_sliders != null && _sliders.Length > 0)
                return false;

            Transform buttonParent = listRoot;
            ScrollRect scroll = listRoot.GetComponent<ScrollRect>();
            if (scroll != null && scroll.content != null)
                buttonParent = scroll.content;

            if (buttonParent.childCount != buttons.Length)
                return false;

            for (int i = 0; i < buttons.Length; i++)
            {
                Transform btn = buttonParent.GetChild(i);
                if (btn.name != "Button") return false;
                TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label == null) return false;
                label.text = buttons[i].Label;

                Button button = btn.GetComponent<Button>();
                if (button == null) return false;
                button.onClick.RemoveAllListeners();
                DbpuMenuButton entry = buttons[i];
                button.onClick.AddListener(() => entry.Action?.Invoke());
            }

            float contentHeight = ContentHeight(buttons.Length, 0);
            float bodyHeight = Mathf.Min(contentHeight, MaxBodyHeight);
            panelHeight = TitleBarH + BodyGap + bodyHeight + PadBottom;
            _panelRt.sizeDelta = new Vector2(PanelWidth, panelHeight);

            RectTransform listRt = listRoot.GetComponent<RectTransform>();
            if (listRt != null)
            {
                listRt.anchoredPosition = new Vector2(0f, -(TitleBarH + BodyGap));
                listRt.sizeDelta = new Vector2(PanelWidth - 4f, bodyHeight);
            }

            if (scroll != null && scroll.content != null)
                scroll.content.sizeDelta = new Vector2(BtnWidth, contentHeight);

            Transform closeChild = _panelHost.transform.Find("Close");
            if (closeChild != null && DBPU.PCloseButton != null)
            {
                float closeHalfH = MenuUI.SpritePixels(DBPU.PCloseButton).y * CloseBtnScale * 0.5f;
                closeChild.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -panelHeight * 0.5f - CloseBtnGap - closeHalfH);
            }

            return true;
        }

        static void SetContent(string title, DbpuMenuButton[] buttons, DbpuMenuSlider[] sliders)
        {
            _titleTmp.text = title;
            if (buttons == null) buttons = Array.Empty<DbpuMenuButton>();
            if (sliders == null) sliders = Array.Empty<DbpuMenuSlider>();

            Transform listChild = _panelRt.Find("List");
            if (listChild != null && sliders.Length == 0 && TryRefreshButtons(listChild, buttons, out _))
                return;

            if (listChild != null)
            {
                listChild.gameObject.SetActive(false);
                GameObject.Destroy(listChild.gameObject);
            }

            Transform closeChild = _panelHost.transform.Find("Close");
            if (closeChild != null) GameObject.Destroy(closeChild.gameObject);

            ClearResizeHandles();

            float contentHeight = ContentHeight(buttons.Length, sliders.Length);
            float bodyHeight = Mathf.Min(contentHeight, MaxBodyHeight);
            float panelHeight = TitleBarH + BodyGap + bodyHeight + PadBottom;
            _panelRt.sizeDelta = new Vector2(PanelWidth, panelHeight);

            GameObject listGo = new GameObject("List");
            listGo.transform.SetParent(_panelRt, false);
            RectTransform listRt = listGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.5f, 1f);
            listRt.anchorMax = new Vector2(0.5f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.anchoredPosition = new Vector2(0f, -(TitleBarH + BodyGap));
            listRt.sizeDelta = new Vector2(PanelWidth - 4f, bodyHeight);

            bool scroll = contentHeight > MaxBodyHeight;
            Transform buttonParent;
            ScrollRect scrollRect = null;
            if (scroll)
            {
                Image listBg = listGo.AddComponent<Image>();
                listBg.color = new Color(0.04f, 0.04f, 0.04f, 0.35f);
                Mask mask = listGo.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                scrollRect = listGo.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 12f;

                GameObject viewport = new GameObject("Viewport");
                viewport.transform.SetParent(listGo.transform, false);
                RectTransform viewportRt = viewport.AddComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero;
                viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = Vector2.zero;
                viewportRt.offsetMax = Vector2.zero;
                viewport.AddComponent<RectMask2D>();

                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                RectTransform contentRt = content.AddComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0.5f, 1f);
                contentRt.anchorMax = new Vector2(0.5f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(BtnWidth, contentHeight);

                scrollRect.viewport = viewportRt;
                scrollRect.content = contentRt;
                buttonParent = content.transform;
            }
            else
            {
                buttonParent = listGo.transform;
            }

            float stackHeight = contentHeight - 1f;
            float topInset = scroll ? 0f : Mathf.Max(0f, (bodyHeight - stackHeight) * 0.5f);
            float y = -topInset;

            for (int i = 0; i < sliders.Length; i++)
            {
                y -= SliderStep * 0.5f;
                CreateSlider(buttonParent, sliders[i], y);
                y -= SliderStep * 0.5f;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                y -= BtnStep * 0.5f;
                CreateButton(buttonParent, buttons[i], y);
                y -= BtnStep * 0.5f;
            }

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            if (DBPU.PCloseButton != null)
            {
                GameObject closeBtn = new GameObject("Close");
                closeBtn.transform.SetParent(_panelHost.transform, false);
                float closeHalfH = MenuUI.SpritePixels(DBPU.PCloseButton).y * CloseBtnScale * 0.5f;
                MenuUI.ApplyImage(closeBtn, DBPU.PCloseButton, new Vector2(0f, -panelHeight * 0.5f - CloseBtnGap - closeHalfH), CloseBtnScale);
                closeBtn.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Action cb = _onClose;
                    Close();
                    cb?.Invoke();
                });
            }

            MenuUI.AttachResizeHandle(_panelRt, () => _panelHostRt.localScale.x, u => _panelHostRt.localScale = new Vector3(u, u, 1f), 5f, 14f, 8f);
        }

        public static void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            if (_uiBlocked)
            {
                Global.main.RemoveUiBlocker();
                _uiBlocked = false;
            }
        }

        static void CreateButton(Transform parent, DbpuMenuButton entry, float y)
        {
            GameObject btnGo = new GameObject("Button");
            btnGo.transform.SetParent(parent, false);
            RectTransform btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0f, y);
            btnRt.sizeDelta = new Vector2(BtnWidth, BtnHeight);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = BtnBg;
            Button btn = btnGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            ColorBlock colors = btn.colors;
            colors.normalColor = BtnBg;
            colors.highlightedColor = BtnHighlight;
            colors.pressedColor = BtnPressed;
            colors.selectedColor = BtnHighlight;
            btn.colors = colors;
            btn.onClick.AddListener(() => entry.Action?.Invoke());

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            RectTransform labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(2f, 0f);
            labelRt.offsetMax = new Vector2(-2f, 0f);
            TextMeshProUGUI labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = entry.Label;
            labelTmp.font = Font;
            labelTmp.fontSize = 3f;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = Color.white;
            labelTmp.richText = true;
            labelTmp.raycastTarget = false;
        }

        static Image CreateSolidImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        static string FormatSliderValue(DbpuMenuSlider entry, float value)
        {
            if (entry.ShowAsPercent)
                return Mathf.RoundToInt(value * 100f) + "%";
            return value.ToString("0.##");
        }

        static void CreateSlider(Transform parent, DbpuMenuSlider entry, float y)
        {
            GameObject rowGo = new GameObject("SliderRow");
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, y);
            rowRt.sizeDelta = new Vector2(BtnWidth, SliderRowHeight);
            Image rowBg = rowGo.AddComponent<Image>();
            rowBg.color = BtnBg;
            rowBg.raycastTarget = false;

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            RectTransform labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.55f);
            labelRt.anchorMax = new Vector2(0.72f, 1f);
            labelRt.offsetMin = new Vector2(2f, 0f);
            labelRt.offsetMax = new Vector2(-1f, -0.4f);
            TextMeshProUGUI labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = entry.Label;
            labelTmp.font = Font;
            labelTmp.fontSize = 2.6f;
            labelTmp.alignment = TextAlignmentOptions.Left;
            labelTmp.color = Color.white;
            labelTmp.raycastTarget = false;

            GameObject valueGo = new GameObject("Value");
            valueGo.transform.SetParent(rowGo.transform, false);
            RectTransform valueRt = valueGo.AddComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(0.72f, 0.55f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.offsetMin = new Vector2(0f, 0f);
            valueRt.offsetMax = new Vector2(-2f, -0.4f);
            TextMeshProUGUI valueTmp = valueGo.AddComponent<TextMeshProUGUI>();
            float initial = entry.GetValue != null ? entry.GetValue() : entry.Min;
            valueTmp.text = FormatSliderValue(entry, initial);
            valueTmp.font = Font;
            valueTmp.fontSize = 2.6f;
            valueTmp.alignment = TextAlignmentOptions.Right;
            valueTmp.color = Color.white;
            valueTmp.raycastTarget = false;

            GameObject sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(rowGo.transform, false);
            RectTransform sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.5f, 0f);
            sliderRt.anchorMax = new Vector2(0.5f, 0f);
            sliderRt.pivot = new Vector2(0.5f, 0f);
            sliderRt.anchoredPosition = new Vector2(0f, 1.2f);
            sliderRt.sizeDelta = new Vector2(BtnWidth - 4f, 4.2f);

            Image bg = CreateSolidImage(sliderGo.transform, "Background", SliderTrackBg);
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.offsetMin = new Vector2(0f, -0.8f);
            bgRt.offsetMax = new Vector2(0f, 0.8f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.offsetMin = new Vector2(0f, -0.8f);
            fillAreaRt.offsetMax = new Vector2(0f, 0.8f);

            Image fill = CreateSolidImage(fillArea.transform, "Fill", SliderFillBg);
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRt.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRt.offsetMin = new Vector2(1.2f, -2f);
            handleAreaRt.offsetMax = new Vector2(-1.2f, 2f);

            Image handle = CreateSolidImage(handleArea.transform, "Handle", SliderHandleBg);
            RectTransform handleRt = handle.rectTransform;
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(2.4f, 4f);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = entry.Min;
            slider.maxValue = entry.Max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(initial, entry.Min, entry.Max);
            slider.onValueChanged.AddListener(v =>
            {
                entry.SetValue?.Invoke(v);
                valueTmp.text = FormatSliderValue(entry, v);
                _onSliderChanged?.Invoke();
            });
        }

        public static void Close()
        {
            _panelHost = null;
            _panelHostRt = null;
            _panelRt = null;
            _titleBarGo = null;
            _titleTmp = null;
            _onClose = null;
            _onSliderChanged = null;
            _sliders = null;
            if (_root != null)
            {
                GameObject.Destroy(_root);
                _root = null;
            }
            if (_uiBlocked)
            {
                Global.main.RemoveUiBlocker();
                _uiBlocked = false;
            }
        }
    }
}
