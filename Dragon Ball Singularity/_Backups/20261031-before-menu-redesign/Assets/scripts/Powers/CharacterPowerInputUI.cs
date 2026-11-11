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
    public static class CharacterPowerInputUI
    {
        static GameObject _root;
        static bool _uiBlocked;
        static TMP_FontAsset _font;

        static readonly Color InputBgNormal = new Color(0.12f, 0.12f, 0.12f, 1f);
        static readonly Color InputBgFocused = new Color(0.2f, 0.22f, 0.28f, 1f);
        static readonly Color InputOutlineNormal = new Color(0.35f, 0.35f, 0.35f, 1f);
        static readonly Color InputOutlineFocused = new Color(0.45f, 0.75f, 1f, 1f);

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static void Open(string title, string currentLine, string defaultLine, float initialValue, Action<float> onApply)
        {
            Close();

            _root = new GameObject("CharacterPowerInputUI");
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

            GameObject panelHost = new GameObject("PanelHost");
            panelHost.transform.SetParent(_root.transform, false);
            RectTransform panelHostRt = panelHost.AddComponent<RectTransform>();
            panelHostRt.anchoredPosition = Vector2.zero;
            panelHostRt.localScale = new Vector3(9f, 9f, 1f);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(panelHost.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(54f, 38f);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.06f, 0.06f, 0.98f);
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = Color.white;
            panelOutline.effectDistance = new Vector2(1.2f, -1.2f);

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 14f);
            titleRt.sizeDelta = new Vector2(50f, 7f);
            TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.font = Font;
            titleTmp.fontSize = 3.6f;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = false;

            GameObject infoGo = new GameObject("Info");
            infoGo.transform.SetParent(panel.transform, false);
            RectTransform infoRt = infoGo.AddComponent<RectTransform>();
            infoRt.anchoredPosition = new Vector2(0f, 5.5f);
            infoRt.sizeDelta = new Vector2(50f, 9f);
            TextMeshProUGUI infoTmp = infoGo.AddComponent<TextMeshProUGUI>();
            infoTmp.text = currentLine + "\n" + defaultLine;
            infoTmp.font = Font;
            infoTmp.fontSize = 3f;
            infoTmp.alignment = TextAlignmentOptions.Left;
            infoTmp.color = Color.white;
            infoTmp.raycastTarget = false;

            GameObject inputGo = new GameObject("Input");
            inputGo.transform.SetParent(panel.transform, false);
            RectTransform inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchoredPosition = new Vector2(0f, -3.5f);
            inputRt.sizeDelta = new Vector2(46f, 7f);
            Image inputBg = inputGo.AddComponent<Image>();
            inputBg.color = InputBgNormal;
            Outline inputOutline = inputGo.AddComponent<Outline>();
            inputOutline.effectColor = InputOutlineNormal;
            inputOutline.effectDistance = new Vector2(1f, -1f);

            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            RectTransform textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(2f, 1f);
            textAreaRt.offsetMax = new Vector2(-2f, -1f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.font = Font;
            inputText.fontSize = 3.2f;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.color = Color.white;

            TMP_InputField inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = inputText;
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.caretBlinkRate = 0.85f;
            inputField.caretWidth = 2;
            inputField.customCaretColor = true;
            inputField.caretColor = Color.white;
            inputField.selectionColor = new Color(0.45f, 0.75f, 1f, 0.45f);
            inputField.text = initialValue.ToString(CultureInfo.InvariantCulture);

            inputField.onSelect.AddListener(_ =>
            {
                inputBg.color = InputBgFocused;
                inputOutline.effectColor = InputOutlineFocused;
            });
            inputField.onDeselect.AddListener(_ =>
            {
                inputBg.color = InputBgNormal;
                inputOutline.effectColor = InputOutlineNormal;
            });

            Action apply = () =>
            {
                if (!float.TryParse(inputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    && !float.TryParse(inputField.text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                    value = initialValue;
                onApply?.Invoke(value);
                Close();
            };

            GameObject applyBtn = new GameObject("Apply");
            applyBtn.transform.SetParent(panel.transform, false);
            RectTransform applyRt = applyBtn.AddComponent<RectTransform>();
            applyRt.anchoredPosition = new Vector2(-11f, -14f);
            applyRt.sizeDelta = new Vector2(20f, 7f);
            Image applyImg = applyBtn.AddComponent<Image>();
            applyImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button applyButton = applyBtn.AddComponent<Button>();
            applyButton.onClick.AddListener(() => apply());

            GameObject applyLabel = new GameObject("Label");
            applyLabel.transform.SetParent(applyBtn.transform, false);
            RectTransform applyLabelRt = applyLabel.AddComponent<RectTransform>();
            applyLabelRt.anchorMin = Vector2.zero;
            applyLabelRt.anchorMax = Vector2.one;
            applyLabelRt.offsetMin = Vector2.zero;
            applyLabelRt.offsetMax = Vector2.zero;
            TextMeshProUGUI applyTmp = applyLabel.AddComponent<TextMeshProUGUI>();
            applyTmp.text = "Apply";
            applyTmp.font = Font;
            applyTmp.fontSize = 3f;
            applyTmp.alignment = TextAlignmentOptions.Center;
            applyTmp.color = Color.white;
            applyTmp.raycastTarget = false;

            GameObject cancelBtn = new GameObject("Cancel");
            cancelBtn.transform.SetParent(panel.transform, false);
            RectTransform cancelRt = cancelBtn.AddComponent<RectTransform>();
            cancelRt.anchoredPosition = new Vector2(11f, -14f);
            cancelRt.sizeDelta = new Vector2(20f, 7f);
            Image cancelImg = cancelBtn.AddComponent<Image>();
            cancelImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            cancelBtn.AddComponent<Button>().onClick.AddListener(Close);

            GameObject cancelLabel = new GameObject("Label");
            cancelLabel.transform.SetParent(cancelBtn.transform, false);
            RectTransform cancelLabelRt = cancelLabel.AddComponent<RectTransform>();
            cancelLabelRt.anchorMin = Vector2.zero;
            cancelLabelRt.anchorMax = Vector2.one;
            cancelLabelRt.offsetMin = Vector2.zero;
            cancelLabelRt.offsetMax = Vector2.zero;
            TextMeshProUGUI cancelTmp = cancelLabel.AddComponent<TextMeshProUGUI>();
            cancelTmp.text = "Cancel";
            cancelTmp.font = Font;
            cancelTmp.fontSize = 3f;
            cancelTmp.alignment = TextAlignmentOptions.Center;
            cancelTmp.color = Color.white;
            cancelTmp.raycastTarget = false;

            inputField.onSubmit.AddListener(_ => apply());
            Global.main.AddUiBlocker();
            _uiBlocked = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            inputField.ActivateInputField();
            inputField.Select();
            inputBg.color = InputBgFocused;
            inputOutline.effectColor = InputOutlineFocused;
        }

        public static void Close()
        {
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
