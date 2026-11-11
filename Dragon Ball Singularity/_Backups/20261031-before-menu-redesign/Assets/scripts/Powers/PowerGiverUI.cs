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
    public static class PowerGiverUI
    {
        const int RowsPerPage = 6;
        const float RowScale = 0.78f;
        const float TitleY = 54f;
        const float CloseX = 0f;
        const float CloseY = -66f;
        const float SearchY = 35f;
        const float NavY = -52f;
        const float ListTopY = 24f;
        static float RowStep => MenuUI.SpritePixels(DBPU.PConfigTextSlider).y * RowScale + 1f;
        static GameObject _canvasRoot;
        static TMP_FontAsset _font;
        static bool _uiBlocked;

        public static bool IsOpen => _canvasRoot != null;

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static void Open()
        {
            if (IsOpen) return;

            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            AuraTunePickerState.Disarm();
            PowerGiverInput.Ensure();

            _canvasRoot = new GameObject("PowerGiverCanvas");
            _canvasRoot.transform.SetParent(GameObject.Find("Canvas").transform, false);
            var canvasRt = _canvasRoot.AddComponent<RectTransform>();
            canvasRt.anchoredPosition = new Vector2(-120f, 40f);
            canvasRt.localScale = new Vector3(8.5f, 8.5f, 1f);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(_canvasRoot.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = DBPU.PCanvas;
            panelImg.color = Color.white;
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = MenuUI.SpritePixels(DBPU.PCanvas);
            panelRt.localScale = Vector3.one;

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, TitleY);
            titleRt.sizeDelta = new Vector2(80f, 12f);
            TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Power Giver";
            titleTmp.font = Font;
            titleTmp.fontSize = 8f;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.black;
            MenuUI.ConfigureTitleDragArea(titleGo, canvasRt, new Vector2(80f, 12f));

            string searchFilter = "";
            int page = 0;
            List<PowerGrantCatalog.Entry> filtered = new List<PowerGrantCatalog.Entry>(PowerGrantCatalog.Entries);
            string selectedId = PowerGiverState.SelectedPowerId;

            GameObject listHolder = new GameObject("ListHolder");
            listHolder.transform.SetParent(panel.transform, false);
            listHolder.AddComponent<RectTransform>();

            Action applyFilter = () =>
            {
                filtered.Clear();
                string q = (searchFilter ?? "").Trim().ToLowerInvariant();
                foreach (var e in PowerGrantCatalog.Entries)
                {
                    if (string.IsNullOrEmpty(q)
                        || e.Name.ToLowerInvariant().Contains(q)
                        || e.Category.ToLowerInvariant().Contains(q)
                        || e.Description.ToLowerInvariant().Contains(q)
                        || e.Id.ToLowerInvariant().Contains(q))
                        filtered.Add(e);
                }
                page = 0;
            };

            TextMeshProUGUI pageLabel = null;
            GameObject pageLabelGo = new GameObject("PageLabel");
            pageLabelGo.transform.SetParent(panel.transform, false);
            RectTransform pageLabelRt = pageLabelGo.AddComponent<RectTransform>();
            pageLabelRt.anchoredPosition = new Vector2(0f, NavY);
            pageLabelRt.sizeDelta = new Vector2(30f, 8f);
            pageLabel = pageLabelGo.AddComponent<TextMeshProUGUI>();
            pageLabel.font = Font;
            pageLabel.fontSize = 3.5f;
            pageLabel.alignment = TextAlignmentOptions.Center;
            pageLabel.color = Color.black;

            Action rebuildList = null;
            rebuildList = () =>
            {
                for (int i = listHolder.transform.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(listHolder.transform.GetChild(i).gameObject);

                int totalPages = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)RowsPerPage));
                if (page >= totalPages) page = totalPages - 1;
                if (page < 0) page = 0;
                int start = page * RowsPerPage;
                int end = Mathf.Min(start + RowsPerPage, filtered.Count);
                float y = ListTopY;

                for (int i = start; i < end; i++)
                {
                    var entry = filtered[i];
                    string capId = entry.Id;
                    bool isSel = capId == selectedId;

                    GameObject row = new GameObject("Row_" + entry.Id);
                    row.transform.SetParent(listHolder.transform, false);
                    MenuUI.ApplyImage(row, DBPU.PConfigTextSlider, new Vector2(0f, y), RowScale);

                    if (isSel)
                    {
                        GameObject selGo = new GameObject("SelectFrame");
                        selGo.transform.SetParent(row.transform, false);
                        MenuUI.ApplyImage(selGo, DBPU.PConfigTextSliderSel, Vector2.zero, 1f);
                        MenuUI.DisableRaycast(selGo.GetComponent<Image>());
                        selGo.transform.SetAsLastSibling();
                    }

                    GameObject labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(row.transform, false);
                    RectTransform labelRt = labelGo.AddComponent<RectTransform>();
                    labelRt.anchoredPosition = Vector2.zero;
                    labelRt.sizeDelta = new Vector2(76f, 11f);
                    TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
                    label.text = entry.Name + "  <size=70%><color=#444>" + entry.Category;
                    label.font = Font;
                    label.fontSize = 4.5f;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.color = Color.black;
                    label.raycastTarget = false;
                    label.margin = new Vector4(6f, 0f, 0f, 0f);

                    row.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        var sel = PowerGrantCatalog.Find(capId);
                        PowerGiverState.SelectedPowerId = capId;
                        PowerGiverState.SelectedPowerName = sel != null ? sel.Name : capId;
                        PowerGiverState.Armed = true;
                        Close();
                    });

                    y -= RowStep;
                }

                pageLabel.text = filtered.Count == 0 ? "0 / 0" : (page + 1) + " / " + totalPages;
            };

            GameObject searchGo = new GameObject("SearchField");
            searchGo.transform.SetParent(panel.transform, false);
            RectTransform searchRt = searchGo.AddComponent<RectTransform>();
            searchRt.anchoredPosition = new Vector2(0f, SearchY);
            searchRt.sizeDelta = new Vector2(72f, 9f);
            Image searchBg = searchGo.AddComponent<Image>();
            searchBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(searchGo.transform, false);
            RectTransform taRt = textArea.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(3f, 1f);
            taRt.offsetMax = new Vector2(-3f, -1f);

            GameObject phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            TextMeshProUGUI phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = "Search...";
            phTmp.font = Font;
            phTmp.fontSize = 3.5f;
            phTmp.color = new Color(0.45f, 0.45f, 0.45f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI inputTmp = textGo.AddComponent<TextMeshProUGUI>();
            inputTmp.font = Font;
            inputTmp.fontSize = 3.5f;
            inputTmp.color = Color.black;
            inputTmp.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField searchInput = searchGo.AddComponent<TMP_InputField>();
            searchInput.textViewport = taRt;
            searchInput.textComponent = inputTmp;
            searchInput.placeholder = phTmp;
            searchInput.onValueChanged.AddListener(v =>
            {
                searchFilter = v;
                applyFilter();
                rebuildList();
            });
            searchInput.onSelect.AddListener(_ =>
            {
                try { if (!_uiBlocked) { Global.main.AddUiBlocker(); _uiBlocked = true; } } catch { }
            });
            searchInput.onDeselect.AddListener(_ =>
            {
                try { if (_uiBlocked) { Global.main.RemoveUiBlocker(); _uiBlocked = false; } } catch { }
            });

            Sprite prevSprite = DBPU.PPrevious != null ? DBPU.PPrevious : DBPU.TPrevious;
            Sprite nextSprite = DBPU.PNext != null ? DBPU.PNext : DBPU.TNext;

            GameObject prevBtn = new GameObject("Prev");
            prevBtn.transform.SetParent(panel.transform, false);
            MenuUI.ApplyImage(prevBtn, prevSprite, new Vector2(-22f, NavY), 0.45f);
            prevBtn.AddComponent<Button>().onClick.AddListener(() => { if (page > 0) { page--; rebuildList(); } });

            GameObject nextBtn = new GameObject("Next");
            nextBtn.transform.SetParent(panel.transform, false);
            MenuUI.ApplyImage(nextBtn, nextSprite, new Vector2(22f, NavY), 0.45f);
            nextBtn.AddComponent<Button>().onClick.AddListener(() =>
            {
                int totalPages = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)RowsPerPage));
                if (page < totalPages - 1) { page++; rebuildList(); }
            });

            GameObject closeBtn = new GameObject("Close");
            closeBtn.transform.SetParent(panel.transform, false);
            MenuUI.ApplyImage(closeBtn, DBPU.PCloseButton, new Vector2(CloseX, CloseY), 0.55f);
            closeBtn.AddComponent<Button>().onClick.AddListener(() =>
            {
                PowerGiverState.Disarm();
                Close();
            });

            MenuUI.AttachResizeHandle(panel.transform, () => canvasRt.localScale.x, u => canvasRt.localScale = new Vector3(u, u, 1f), 5f, 14f, 8f);

            applyFilter();
            rebuildList();
            searchInput.ActivateInputField();
            searchInput.Select();
        }

        public static void Close()
        {
            if (_uiBlocked)
            {
                try { Global.main.RemoveUiBlocker(); } catch { }
                _uiBlocked = false;
            }
            if (_canvasRoot != null)
            {
                GameObject.Destroy(_canvasRoot);
                _canvasRoot = null;
            }
        }
    }
}
