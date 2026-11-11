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
    public class TransformationContextMenu : MonoBehaviour
    {
        public PersonBehaviour person;
        public CharacterTransformations TransformationsData;
        public SkinContextMenu skinContextMenu;
        public MultipliersAndStuffs Multipliers;
        public AuraHandler AuraHandler;

        public GameObject Aura;

        public GameObject FramesHolder;

        private int ActualSelectedFrame = 0;
        public int PositionSelectedFrame = 0;
        public int OriginalSelectedFrame = 0;
        public int LocalSelectedFrame = 0;
        private int Pages = 0;
        private int CurrentPage = 0;
        private int PreviousPage = 0;
        public int FormSelected = 0;
        public int OriginalFormSelected = 0;

        public float PreviousMultiplier = 1f;
        public string Form = "Base";
        public string CurrentAuraID = "Base";
        public string BaseForm = "Base";
        public string LastUsedForm;

        public bool AuraOn = true;
        public bool AuraCanAppear = true;

        private bool MenuOpen = false;
        private float SelectedMultiplier = 1f;
        private string SelectedFormId = "";
        private TextMeshProUGUI MultiplierText;
        private TransformationMenuMultiplierTicker multiplierTicker;

        public Dictionary<string, GameObject> AurasStorage = new Dictionary<string, GameObject>();

        private List<Vector2> FramesPos = new List<Vector2>();

        public void Start()
        {
            FormSelected = OriginalFormSelected;
            PositionSelectedFrame = OriginalSelectedFrame;

            EnsureRuntimeRefs();
            RecalculatePages();
        }

        void EnsureRuntimeRefs()
        {
            var root = transform.root != null ? transform.root.gameObject : gameObject;
            if (person == null)
            {
                person = root.GetComponent<PersonBehaviour>();
                if (person == null && transform.root != null)
                    person = transform.root.GetComponentInChildren<PersonBehaviour>();
            }

            if (TransformationsData == null)
                TransformationsData = root.GetComponent<CharacterTransformations>();

            if (!root.GetComponent<MultipliersAndStuffs>())
                root.AddComponent<MultipliersAndStuffs>();
            Multipliers = root.GetComponent<MultipliersAndStuffs>();

            var aura = root.GetComponent<AuraHandler>();
            if (aura == null) aura = root.AddComponent<AuraHandler>();
            if (aura.person == null) aura.person = person;
            if (!string.IsNullOrEmpty(CurrentAuraID)) aura.CurrentAuraID = CurrentAuraID;
            AuraHandler = aura;
        }

        void RecalculatePages()
        {
            Pages = 0;
            if (TransformationsData == null || TransformationsData.transformations == null
                || TransformationsData.transformations.TransformationData == null)
                return;

            int localPage = 0;
            int count = TransformationsData.transformations.TransformationData.Count;
            for (int i = 0; i < count; i++)
            {
                localPage += 1;
                if (localPage == 6)
                {
                    localPage = 0;
                    Pages += 1;
                }
            }
        }

        void SyncSelectionToCurrentForm(CharacterTransformations.Transformations t)
        {
            if (t == null || t.TransformationData == null || t.TransformationData.Count == 0)
            {
                FormSelected = 0;
                CurrentPage = 0;
                LocalSelectedFrame = 0;
                PositionSelectedFrame = 0;
                return;
            }

            var list = t.GetOrderedEntries();
            int index = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i].Value;
                if (entry == null) continue;
                if (list[i].Key == Form || entry.ID == Form)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || index >= list.Count) index = 0;
            FormSelected = index;
            CurrentPage = index / 6;
            LocalSelectedFrame = index % 6;
            PositionSelectedFrame = LocalSelectedFrame;

            var stats = list[index].Value;
            if (stats == null) return;
            SelectedFormId = stats.ID;
            SelectedMultiplier = TransformationsData != null
                ? TransformationsData.GetEffectivePower(stats)
                : stats.PowerMultiplier;
        }

        void CloseTransformMenu(GameObject canvas)
        {
            MenuOpen = false;
            MultiplierText = null;
            multiplierTicker = null;
            Destroy(canvas);
        }

        public void Awake()
        {
            if (FramesPos.Count == 0)
            {
                FramesPos.Add(new Vector2(-20f, 12f));
                FramesPos.Add(new Vector2(0f, 12f));
                FramesPos.Add(new Vector2(20f, 12f));
                FramesPos.Add(new Vector2(-20f, -22f));
                FramesPos.Add(new Vector2(0f, -22f));
                FramesPos.Add(new Vector2(20f, -22f));
            }

            var phys = gameObject.GetComponent<PhysicalBehaviour>();
            if (phys == null || phys.ContextMenuOptions == null) return;
            var contextButtons = phys.ContextMenuOptions;
            if (!MCP_Utils.CheckContextButton(contextButtons, "!CombatTransformations"))
                contextButtons.Buttons.Add(new ContextMenuButton("!CombatTransformations", "Combat Forms", "Fast combat transformation selector", () =>
                {
                    var context = UnityEngine.Object.FindObjectOfType<global::ContextMenuBehaviour>();
                    if (context) context.Hide();
                    CombatTransformationWheel.Instance.OpenFor(this, Input.mousePosition, KeyCode.None);
                }));
            if (MCP_Utils.CheckContextButton(contextButtons, "!TrasformationsMenu")) return;
            int transformInsertAt = GetTransformationsMenuInsertIndex(contextButtons);
            MCP_Utils.AddContextButtonInOrder(contextButtons, new ContextMenuButton("!TrasformationsMenu", "Trasformations Menu", "Trasformations Menu", () =>
            {
                EnsureRuntimeRefs();
                RecalculatePages();

                if (TransformationsData == null || TransformationsData.transformations == null
                    || TransformationsData.transformations.TransformationData == null
                    || TransformationsData.transformations.TransformationData.Count == 0)
                {
                    ModAPI.Notify("No transformations available.");
                    return;
                }

                MenuOpen = true;

                PreviousPage = CurrentPage;

                if (CurrentPage < 0) CurrentPage = 0;
                if (CurrentPage > Pages) CurrentPage = Pages;

                var t = TransformationsData.transformations;
                SyncSelectionToCurrentForm(t);
                var firstList = t.GetOrderedEntries();
                if (FormSelected < 0 || FormSelected >= firstList.Count) FormSelected = 0;
                var firstValue = firstList[FormSelected].Value;
                if (firstValue == null)
                {
                    ModAPI.Notify("Transformation data is missing.");
                    MenuOpen = false;
                    return;
                }

                GameObject Canvas = new GameObject("TCanvas");
                Canvas.transform.SetParent(GameObject.Find("Canvas").transform);
                Canvas.GetOrAddComponent<Image>().sprite = DBPU.TCanvas;
                Canvas.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                Canvas.GetComponent<RectTransform>().localScale = new Vector3(12.34f, 7.056f, 1f);
                Canvas.AddComponent<ExistIf>().obj = gameObject;

                GameObject textstorageobject = new GameObject("textstorageobject");
                textstorageobject.transform.SetParent(Canvas.transform, false);

                RectTransform textRectTransform = textstorageobject.AddComponent<RectTransform>();
                textRectTransform.anchoredPosition = new Vector2(0f, 43.75f);
                textRectTransform.localScale = new Vector3(0.55f, 1f);
                //textRectTransform.anchorMin = new Vector2(0.1f, 0.1f);
                //textRectTransform.anchorMax = new Vector2(0.9f, 0.9f);

                TextMeshProUGUI textstorage = textstorageobject.AddComponent<TextMeshProUGUI>();
                textstorage.text = "TRANSFORMATION";
                textstorage.fontSize = 10;
                textstorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage.alignment = TextAlignmentOptions.Center;
                textstorage.color = new Color(0f, 0f, 0f);
                textstorage.enableWordWrapping = false;
                MenuUI.ConfigureTitleDragArea(textstorageobject, Canvas.GetComponent<RectTransform>(), new Vector2(190f, 18f));

                RectTransform canvasRt = Canvas.GetComponent<RectTransform>();
                float transformMenuScale = canvasRt.localScale.x;
                MenuUI.AttachResizeHandle(
                    Canvas.transform,
                    () => transformMenuScale,
                    (u) =>
                    {
                        transformMenuScale = u;
                        float aspect = canvasRt.localScale.y / Mathf.Max(0.01f, canvasRt.localScale.x);
                        canvasRt.localScale = new Vector3(u, u * aspect, 1f);
                    },
                    4f,
                    20f,
                    12f);

                FramesHolder = new GameObject("FramesHolder");
                FramesHolder.transform.SetParent(Canvas.transform, false);

                GameObject Info = new GameObject("Info");
                Info.transform.SetParent(Canvas.transform);
                Info.GetOrAddComponent<Image>().sprite = DBPU.TInfo;
                Info.GetComponent<RectTransform>().anchoredPosition = new Vector2(66.5f, -7f);
                Info.GetComponent<RectTransform>().localScale = new Vector3(0.3216f, 0.8215f, 1f);

                GameObject InfoNameText = new GameObject("Info Name Text");
                InfoNameText.transform.SetParent(Info.transform, false);

                RectTransform InfoNameTextRectTransform = InfoNameText.AddComponent<RectTransform>();
                InfoNameTextRectTransform.anchoredPosition = new Vector2(0f, 0f);
                InfoNameTextRectTransform.localScale = new Vector3(1.25f, 1f);
                InfoNameTextRectTransform.anchorMin = new Vector2(0, 0);
                InfoNameTextRectTransform.anchorMax = new Vector2(1, 1);
                InfoNameTextRectTransform.pivot = new Vector2(0, 1);
                InfoNameTextRectTransform.offsetMax = new Vector2(-25f, 49f);
                InfoNameTextRectTransform.offsetMin = new Vector2(8f, 35f);

                TextMeshProUGUI InfoNameTextStorage = InfoNameText.AddComponent<TextMeshProUGUI>();
                InfoNameTextStorage.text = firstValue.Name;
                InfoNameTextStorage.fontSize = 8;
                InfoNameTextStorage.enableAutoSizing = true;
                InfoNameTextStorage.fontSizeMin = 4;
                InfoNameTextStorage.fontSizeMax = 8;
                InfoNameTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                InfoNameTextStorage.alignment = TextAlignmentOptions.Center;
                InfoNameTextStorage.color = new Color(0f, 0f, 0f);
                InfoNameTextStorage.enableWordWrapping = false;

                GameObject InfoDescriptionText = new GameObject("Info Description Text");
                InfoDescriptionText.transform.SetParent(Info.transform, false);

                RectTransform InfoDescriptionTextRectTransform = InfoDescriptionText.AddComponent<RectTransform>();
                InfoDescriptionTextRectTransform.anchoredPosition = new Vector2(0f, 0f);
                InfoDescriptionTextRectTransform.localScale = new Vector3(1.25f, 1f);
                InfoDescriptionTextRectTransform.anchorMin = new Vector2(0, 0);
                InfoDescriptionTextRectTransform.anchorMax = new Vector2(1, 1);
                InfoDescriptionTextRectTransform.pivot = new Vector2(0, 1);
                InfoDescriptionTextRectTransform.offsetMax = new Vector2(-23f, -20f);
                InfoDescriptionTextRectTransform.offsetMin = new Vector2(7f, -35f);

                TextMeshProUGUI InfoDescriptionTextStorage = InfoDescriptionText.AddComponent<TextMeshProUGUI>();
                InfoDescriptionTextStorage.text = firstValue.Description;
                InfoDescriptionTextStorage.fontSize = 4;
                InfoDescriptionTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                InfoDescriptionTextStorage.alignment = TextAlignmentOptions.TopLeft;
                InfoDescriptionTextStorage.enableWordWrapping = true;
                InfoDescriptionTextStorage.color = new Color(0f, 0f, 0f);

                GameObject InfoMultiplierText = new GameObject("Info Multiplier Text");
                InfoMultiplierText.transform.SetParent(Info.transform, false);

                RectTransform InfoMultiplierTextRectTransform = InfoMultiplierText.AddComponent<RectTransform>();
                InfoMultiplierTextRectTransform.anchoredPosition = new Vector2(0f, -41.5f);
                InfoMultiplierTextRectTransform.localScale = new Vector3(1.25f, 1f);

                TextMeshProUGUI InfoMultiplierTextStorage = InfoMultiplierText.AddComponent<TextMeshProUGUI>();
                SelectedFormId = firstValue.ID;
                float firstEffectiveMult = GetStatsEffectivePower(firstValue);
                SelectedMultiplier = firstEffectiveMult;
                InfoMultiplierTextStorage.text = FormatInfoMultiplier(firstEffectiveMult, firstValue.ID);
                InfoMultiplierTextStorage.fontSize = 8;
                InfoMultiplierTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                InfoMultiplierTextStorage.alignment = TextAlignmentOptions.Center;
                InfoMultiplierTextStorage.color = new Color(0f, 0f, 0f);
                InfoMultiplierTextStorage.enableWordWrapping = false;

                MultiplierText = InfoMultiplierTextStorage;
                multiplierTicker = Canvas.AddComponent<TransformationMenuMultiplierTicker>();
                multiplierTicker.Init(this, InfoMultiplierTextStorage, firstEffectiveMult, firstValue.ID);

                GameObject Illustration = new GameObject("Illustration");
                Illustration.transform.SetParent(Canvas.transform);
                //Illustration.GetOrAddComponent<Image>().sprite = DBPU.TIllustration;
                Illustration.GetOrAddComponent<Image>().sprite = firstValue.Illustration;
                Illustration.GetComponent<RectTransform>().anchoredPosition = new Vector2(-66.55f, -7f);
                Illustration.GetComponent<RectTransform>().localScale = new Vector3(0.3216f, 0.8215f, 1f);



                GameObject DamageToggle = new GameObject("DamageToggle");
                DamageToggle.transform.SetParent(Canvas.transform);
                DamageToggle.GetOrAddComponent<Image>().sprite = DBPU.TDamage_NULL;
                if (skinContextMenu != null)
                {
                    if (skinContextMenu.SkinDamage == true) DamageToggle.GetComponent<Image>().sprite = DBPU.TDamage_ON;
                    else if (skinContextMenu.SkinDamage == true) DamageToggle.GetComponent<Image>().sprite = DBPU.TDamage_OFF;
                    else DamageToggle.GetComponent<Image>().sprite = DBPU.TDamage_NULL;
                }
                DamageToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(-27.5f, -59.5f);
                DamageToggle.GetComponent<RectTransform>().localScale = new Vector3(0.275f, 0.348f, 1f);
                DamageToggle.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    if (skinContextMenu == null) return;

                    if (skinContextMenu.SkinDamage == true)
                    {
                        DamageToggle.GetComponent<Image>().sprite = DBPU.TDamage_OFF;
                        skinContextMenu.SkinDamage = false;
                    }

                    else
                    {
                        DamageToggle.GetComponent<Image>().sprite = DBPU.TDamage_ON;
                        skinContextMenu.SkinDamage = true;
                    }
                });

                GameObject DamageToggleNameText = new GameObject("Damage Name Text");
                DamageToggleNameText.transform.SetParent(DamageToggle.transform, false);

                RectTransform DamageToggleTextRectTransform = DamageToggleNameText.AddComponent<RectTransform>();
                DamageToggleTextRectTransform.anchoredPosition = new Vector2(0f, 22f);
                DamageToggleTextRectTransform.localScale = new Vector3(1f, 1.25f);

                TextMeshProUGUI DamageToggleNameTextStorage = DamageToggleNameText.AddComponent<TextMeshProUGUI>();
                DamageToggleNameTextStorage.text = "Clothing Damage";
                DamageToggleNameTextStorage.fontSize = 9;
                DamageToggleNameTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                DamageToggleNameTextStorage.alignment = TextAlignmentOptions.Center;
                DamageToggleNameTextStorage.color = new Color(0f, 0f, 0f);
                DamageToggleNameTextStorage.enableWordWrapping = true;



                GameObject SelectFrame = new GameObject("SelectFrame");
                SelectFrame.transform.SetParent(Canvas.transform);
                SelectFrame.GetOrAddComponent<Image>().sprite = DBPU.TSelected;
                SelectFrame.transform.SetAsLastSibling();
                if (PositionSelectedFrame < 0 || PositionSelectedFrame >= FramesPos.Count)
                    PositionSelectedFrame = 0;
                SelectFrame.GetComponent<RectTransform>().anchoredPosition = FramesPos[PositionSelectedFrame];
                SelectFrame.GetComponent<RectTransform>().localScale = new Vector3(0.164f, 0.286f, 1f);
                SelectFrame.GetComponent<Image>().color = Color.white;
                MenuUI.DisableRaycast(SelectFrame.GetComponent<Image>());

                int FirstFramesMissing = t.TransformationData.Count - (6 * CurrentPage);
                int FirstCurrentFrames = 0;

                while (FirstFramesMissing > 0 && FirstCurrentFrames < 6)
                {
                    var list = t.GetOrderedEntries();
                    int idx = FirstCurrentFrames + (6 * CurrentPage);
                    if (idx < 0 || idx >= list.Count) break;
                    var value = list[idx].Value;
                    if (value == null)
                    {
                        FirstCurrentFrames += 1;
                        FirstFramesMissing -= 1;
                        continue;
                    }

                    FirstCurrentFrames += 1;

                    GameObject Frame = new GameObject("Frame" + FirstCurrentFrames);
                    Frame.transform.SetParent(FramesHolder.gameObject.transform);
                    Frame.GetOrAddComponent<Image>().sprite = value.Icon;
                    Frame.GetComponent<RectTransform>().anchoredPosition = FramesPos[FirstCurrentFrames - 1];
                    Frame.GetComponent<RectTransform>().localScale = new Vector3(0.1535f, 0.267f, 1f);

                    Frame.AddComponent<FrameInfo>().ID = FirstCurrentFrames - 1;

                    Frame.GetOrAddComponent<Button>().onClick.AddListener(() =>
                    {
                        SelectedMultiplier = GetStatsEffectivePower(value);
                        SelectedFormId = value.ID;

                        SelectFrame.GetComponent<RectTransform>().anchoredPosition = FramesPos[Frame.GetComponent<FrameInfo>().ID];
                        SelectFrame.GetComponent<Image>().color = Color.white;

                        LocalSelectedFrame = Frame.GetComponent<FrameInfo>().ID;
                        FormSelected = Frame.GetComponent<FrameInfo>().ID + (6 * CurrentPage);

                        Illustration.GetComponent<Image>().sprite = value.Illustration;
                        InfoNameTextStorage.text = value.Name;
                        InfoDescriptionTextStorage.text = value.Description;
                        InfoMultiplierTextStorage.text = FormatInfoMultiplier(SelectedMultiplier, value.ID);
                        if (multiplierTicker != null) multiplierTicker.SetForm(SelectedMultiplier, value.ID);
                    });

                    FirstFramesMissing -= 1;
                }

                if (t.TransformationData.Count > 6)
                {
                    GameObject NextPage = new GameObject("NextPage");
                    NextPage.transform.SetParent(Canvas.transform);
                    NextPage.GetOrAddComponent<Image>().sprite = DBPU.TNext;
                    NextPage.GetComponent<Image>().color = Color.clear;
                    if (CurrentPage < Pages) NextPage.GetComponent<Image>().color = Color.white;
                    NextPage.GetComponent<RectTransform>().anchoredPosition = new Vector2(38f, -5.1f);
                    NextPage.GetComponent<RectTransform>().localScale = new Vector3(0.0818f, 0.625f, 1f);

                    GameObject PrevioursPage = new GameObject("PreviousPage");
                    PrevioursPage.transform.SetParent(Canvas.transform);
                    PrevioursPage.GetOrAddComponent<Image>().sprite = DBPU.TPrevious;
                    PrevioursPage.GetComponent<Image>().color = Color.clear;
                    if (CurrentPage > 0) PrevioursPage.GetComponent<Image>().color = Color.white;
                    PrevioursPage.GetComponent<RectTransform>().anchoredPosition = new Vector2(-38f, -5.1f);
                    PrevioursPage.GetComponent<RectTransform>().localScale = new Vector3(0.0818f, 0.625f, 1f);

                    NextPage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                    {
                        if (CurrentPage >= Pages) return;

                        CurrentPage += 1;

                        int FramesMissing = t.TransformationData.Count - (6 * CurrentPage);
                        int CurrentFrames = 0;

                        if (Math.Truncate(((float)(FormSelected) / 6f)) == CurrentPage) SelectFrame.GetComponent<Image>().color = Color.white;
                        else SelectFrame.GetComponent<Image>().color = Color.clear;

                        Destroy(FramesHolder.gameObject);

                        FramesHolder = new GameObject("FramesHolder");
                        FramesHolder.transform.SetParent(Canvas.transform, false);

                        while (FramesMissing > 0 && CurrentFrames < 6)
                        {
                            var list = t.GetOrderedEntries();
                            var value = list[CurrentFrames + (6 * CurrentPage)].Value;

                            CurrentFrames += 1;

                            GameObject Frame = new GameObject("Frame" + CurrentFrames);
                            Frame.transform.SetParent(FramesHolder.transform);
                            Frame.GetOrAddComponent<Image>().sprite = value.Icon;
                            Frame.GetComponent<RectTransform>().anchoredPosition = FramesPos[CurrentFrames - 1];
                            Frame.GetComponent<RectTransform>().localScale = new Vector3(0.1535f, 0.267f, 1f);

                            Frame.AddComponent<FrameInfo>().ID = CurrentFrames - 1;

                            Frame.GetOrAddComponent<Button>().onClick.AddListener(() =>
                            {
                                SelectedMultiplier = GetStatsEffectivePower(value);
                                SelectedFormId = value.ID;

                                SelectFrame.GetComponent<RectTransform>().anchoredPosition = FramesPos[Frame.GetComponent<FrameInfo>().ID];
                                SelectFrame.GetComponent<Image>().color = Color.white;

                                LocalSelectedFrame = Frame.GetComponent<FrameInfo>().ID;
                                FormSelected = Frame.GetComponent<FrameInfo>().ID + (6 * CurrentPage);

                                Illustration.GetComponent<Image>().sprite = value.Illustration;
                                InfoNameTextStorage.text = value.Name;
                                InfoDescriptionTextStorage.text = value.Description;
                                InfoMultiplierTextStorage.text = FormatInfoMultiplier(SelectedMultiplier, value.ID);
                                if (multiplierTicker != null) multiplierTicker.SetForm(SelectedMultiplier, value.ID);
                            });

                            FramesMissing -= 1;
                        }

                        if (CurrentPage > 0)
                        {
                            PrevioursPage.GetComponent<Image>().color = Color.white;
                        }

                        if (CurrentPage >= Pages)
                        {
                            NextPage.GetComponent<Image>().color = Color.clear;
                        }

                        SelectFrame.transform.SetAsLastSibling();
                    });

                    PrevioursPage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                    {
                        if (CurrentPage <= 0) return;

                        CurrentPage -= 1;

                        int FramesMissing = t.TransformationData.Count - (6 * CurrentPage);
                        int CurrentFrames = 0;

                        if (Math.Truncate(((float)(FormSelected) / 6f)) == CurrentPage) SelectFrame.GetComponent<Image>().color = Color.white;
                        else SelectFrame.GetComponent<Image>().color = Color.clear;

                        Destroy(FramesHolder.gameObject);

                        FramesHolder = new GameObject("FramesHolder");
                        FramesHolder.transform.SetParent(Canvas.transform, false);

                        while (FramesMissing > 0 && CurrentFrames < 6)
                        {
                            var list = t.GetOrderedEntries();
                            var value = list[CurrentFrames + (6 * CurrentPage)].Value;

                            CurrentFrames += 1;

                            GameObject Frame = new GameObject("Frame" + CurrentFrames);
                            Frame.transform.SetParent(FramesHolder.transform);
                            Frame.GetOrAddComponent<Image>().sprite = value.Icon;
                            Frame.GetComponent<RectTransform>().anchoredPosition = FramesPos[CurrentFrames - 1];
                            Frame.GetComponent<RectTransform>().localScale = new Vector3(0.1535f, 0.267f, 1f);

                            Frame.AddComponent<FrameInfo>().ID = CurrentFrames - 1;

                            Frame.GetOrAddComponent<Button>().onClick.AddListener(() =>
                            {
                                SelectedMultiplier = GetStatsEffectivePower(value);
                                SelectedFormId = value.ID;

                                SelectFrame.GetComponent<RectTransform>().anchoredPosition = FramesPos[Frame.GetComponent<FrameInfo>().ID];
                                SelectFrame.GetComponent<Image>().color = Color.white;

                                LocalSelectedFrame = Frame.GetComponent<FrameInfo>().ID;
                                FormSelected = Frame.GetComponent<FrameInfo>().ID + (6 * CurrentPage);

                                Illustration.GetComponent<Image>().sprite = value.Illustration;
                                InfoNameTextStorage.text = value.Name;
                                InfoDescriptionTextStorage.text = value.Description;
                                InfoMultiplierTextStorage.text = FormatInfoMultiplier(SelectedMultiplier, value.ID);
                                if (multiplierTicker != null) multiplierTicker.SetForm(SelectedMultiplier, value.ID);
                            });

                            FramesMissing -= 1;
                        }

                        if (CurrentPage < Pages)
                        {
                            NextPage.GetComponent<Image>().color = Color.white;
                        }

                        if (CurrentPage <= 0)
                        {
                            PrevioursPage.GetComponent<Image>().color = Color.clear;
                        }

                        SelectFrame.transform.SetAsLastSibling();
                    });
                }

                GameObject Cancel = new GameObject("Cancel");
                Cancel.transform.SetParent(Canvas.transform);
                Cancel.GetOrAddComponent<Image>().sprite = DBPU.TCancel;
                Cancel.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    CurrentPage = PreviousPage;
                    CloseTransformMenu(Canvas);
                });
                Cancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(7f, -50f);
                Cancel.GetComponent<RectTransform>().localScale = new Vector3(0.25f, 0.1607f, 1f);

                GameObject textstorageobject1 = new GameObject("cancel text");
                textstorageobject1.transform.SetParent(Cancel.transform, false);

                RectTransform textRectTransform1 = textstorageobject1.AddComponent<RectTransform>();
                textRectTransform1.anchoredPosition = new Vector2(0f, 0f);
                textRectTransform1.localScale = new Vector3(0.55f, 1f);
                textRectTransform1.localScale *= 3f;
                //textRectTransform1.anchorMin = new Vector2(0.1f, 0.1f);
                //textRectTransform1.anchorMax = new Vector2(0.9f, 0.9f);

                TextMeshProUGUI textstorage1 = textstorageobject1.AddComponent<TextMeshProUGUI>();
                textstorage1.text = "CANCEL";
                textstorage1.fontSize = 10;
                textstorage1.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage1.alignment = TextAlignmentOptions.Center;
                textstorage1.color = new Color(1f, 1f, 1f);
                textstorage1.enableWordWrapping = false;
                textstorage1.autoSizeTextContainer = true;

                GameObject Accept = new GameObject("Accept");
                Accept.transform.SetParent(Canvas.transform);
                Accept.GetOrAddComponent<Image>().sprite = DBPU.TAccept;
                Accept.GetComponent<RectTransform>().anchoredPosition = new Vector2(34.4f, -50f);
                Accept.GetComponent<RectTransform>().localScale = new Vector3(0.25f, 0.1607f, 1f);
                Accept.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    var list = t.GetOrderedEntries();
                    if (list.Count == 0) return;

                    CharacterTransformations.Transformations.TransformationStats value = null;
                    if (!string.IsNullOrEmpty(SelectedFormId))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var entry = list[i].Value;
                            if (entry == null) continue;
                            if (list[i].Key == SelectedFormId || entry.ID == SelectedFormId)
                            {
                                value = entry;
                                FormSelected = i;
                                break;
                            }
                        }
                    }
                    if (value == null)
                    {
                        if (FormSelected < 0 || FormSelected >= list.Count) FormSelected = 0;
                        value = list[FormSelected].Value;
                    }
                    if (value == null || string.IsNullOrEmpty(value.ID)) return;

                    ApplyForm(value.ID);
                    PositionSelectedFrame = LocalSelectedFrame;

                    CloseTransformMenu(Canvas);
                });

                GameObject textstorageobject2 = new GameObject("accept text");
                textstorageobject2.transform.SetParent(Accept.transform, false);

                RectTransform textRectTransform2 = textstorageobject2.AddComponent<RectTransform>();
                textRectTransform2.anchoredPosition = new Vector2(0f, 0f);
                textRectTransform2.localScale = new Vector3(0.55f, 1f);
                textRectTransform2.localScale *= 3f;
                //textRectTransform2.anchorMin = new Vector2(0.1f, 0.1f);
                //textRectTransform2.anchorMax = new Vector2(0.9f, 0.9f);

                TextMeshProUGUI textstorage2 = textstorageobject2.AddComponent<TextMeshProUGUI>();
                textstorage2.text = "ACCEPT";
                textstorage2.fontSize = 10;
                textstorage2.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage2.alignment = TextAlignmentOptions.Center;
                textstorage2.color = new Color(1f, 1f, 1f);
                textstorage2.enableWordWrapping = false;
                textstorage2.autoSizeTextContainer = true;
            }), transformInsertAt);
        }

        static int GetTransformationsMenuInsertIndex(ContextMenuOptionComponent buttons)
        {
            if (buttons == null || buttons.Buttons == null) return 0;
            for (int i = 0; i < buttons.Buttons.Count; i++)
            {
                if (buttons.Buttons[i].Identity == "!PowersMenu")
                    return i + 1;
            }
            return 0;
        }

        public void EnsureBesidePowersMenu()
        {
            var phys = gameObject.GetComponent<PhysicalBehaviour>();
            if (phys == null || phys.ContextMenuOptions == null) return;
            var buttons = phys.ContextMenuOptions;
            if (!MCP_Utils.CheckContextButton(buttons, "!TrasformationsMenu")) return;

            int powersIdx = -1;
            int transformIdx = -1;
            for (int i = 0; i < buttons.Buttons.Count; i++)
            {
                string id = buttons.Buttons[i].Identity;
                if (id == "!PowersMenu") powersIdx = i;
                if (id == "!TrasformationsMenu") transformIdx = i;
            }
            if (transformIdx < 0) return;
            int desired = powersIdx >= 0 ? powersIdx + 1 : 0;
            if (transformIdx == desired) return;
            if (powersIdx >= 0 && transformIdx < powersIdx)
                desired = powersIdx;
            MCP_Utils.MoveContextButton(buttons, "!TrasformationsMenu", desired);
        }

        public string FormatInfoMultiplier(float formMult, string formId)
        {
            formMult = ResolveEffectiveMultiplier(formId, formMult);

            var multipliers = transform.root.GetComponent<MultipliersAndStuffs>();
            if (multipliers != null && multipliers.ExponentialMultiplier > 1f)
            {
                float ex = multipliers.ExponentialMultiplier;
                string exLabel = ex < 100f ? ((float)Math.Round(ex, 2)).ToString() : GetExMultLabel(Mathf.Abs(ex));
                return "X" + GetMultLabel(formMult) + " * " + exLabel;
            }

            return "X" + GetMultLabel(formMult);
        }

        string GetMultLabel(float Mult)
        {
            float newMult = Mult;
            string label = "";

            if (Mult >= 100000000000000f) label = ((float)Math.Round(newMult / 1000000000f, 0)).ToString() + "T";
            else if (Mult >= 1000000000000f) label = ((float)Math.Round(newMult / 1000000000f, 1)).ToString() + "T";
            else if (Mult >= 100000000000f) label = ((float)Math.Round(newMult / 1000000000f, 0)).ToString() + "B";
            else if (Mult >= 1000000000f) label = ((float)Math.Round(newMult / 1000000000f, 1)).ToString() + "B";
            else if (Mult >= 100000000f) label = ((float)Math.Round(newMult / 1000000f, 0)).ToString() + "M";
            else if (Mult >= 1000000f) label = ((float)Math.Round(newMult / 1000000f, 1)).ToString() + "M";
            else if (Mult < 1f) label = newMult.ToString();
            else label = ((float)Math.Round(newMult, 0)).ToString();

            return label;
        }

        string GetExMultLabel(float Mult)
        {
            float newMult = Mult;
            string label = "";

            if (Mult >= 100000000000f) label = ((float)Math.Round(newMult / 1000000000f, 0)).ToString() + "B";
            else if (Mult >= 1000000000f) label = ((float)Math.Round(newMult / 1000000000f, 1)).ToString() + "B";
            else if (Mult >= 100000000f) label = ((float)Math.Round(newMult / 1000000f, 0)).ToString() + "M";
            else if (Mult >= 1000000f) label = ((float)Math.Round(newMult / 1000000f, 1)).ToString() + "M";
            else if (Mult >= 100000f) label = ((float)Math.Round(newMult / 1000f, 0)).ToString() + "K";
            else if (Mult >= 1000f) label = ((float)Math.Round(newMult / 1000f, 1)).ToString() + "K";
            else label = ((float)Math.Round(newMult, 0)).ToString();

            return label;
        }

        float ResolveEffectiveMultiplier(string formId, float fallback)
        {
            if (TransformationsData == null || TransformationsData.transformations == null
                || TransformationsData.transformations.TransformationData == null
                || string.IsNullOrEmpty(formId))
                return fallback;

            foreach (var kvp in TransformationsData.transformations.TransformationData)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value.ID == formId)
                    return TransformationsData.GetEffectivePower(kvp.Value);
            }

            return fallback;
        }

        float GetStatsEffectivePower(CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (TransformationsData == null || stats == null)
                return stats != null ? stats.PowerMultiplier : 1f;
            return TransformationsData.GetEffectivePower(stats);
        }

        public void ApplyForm(string formKey)
        {
            EnsureRuntimeRefs();
            if (person == null || !person.IsAlive() || TransformationsData == null) return;
            if (TransformationsData.transformations == null) return;

            var data = TransformationsData.transformations.TransformationData;
            if (data == null || !data.ContainsKey(formKey)) return;

            var value = data[formKey];
            if (value == null) return;
            var kiForVfx = person.GetComponent<KiSystem>();
            string previousForVfx = kiForVfx != null && !string.IsNullOrEmpty(kiForVfx.CurrentForm) ? kiForVfx.CurrentForm : Form;
            if (KiFormTransition.Begin(TransformationsData, person, previousForVfx, formKey, () => ApplyForm(formKey))) return;

            float effectivePower = TransformationsData.GetEffectivePower(value);
            float prev = PreviousMultiplier <= 0f ? 1f : PreviousMultiplier;

            TransformationsData.ApplyTransformation(person, value.ID);
            if (Multipliers != null) Multipliers.FormMultiplier = effectivePower;

            if (person.Limbs != null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb == null) continue;
                    if (!GogitoVitality.Scale(limb, effectivePower / prev))
                    {
                        if (!float.IsInfinity(limb.InitialHealth * (effectivePower / prev))) limb.InitialHealth *= effectivePower / prev;
                        if (!float.IsInfinity(limb.Health * (effectivePower / prev))) limb.Health *= effectivePower / prev;
                    }
                }
            }

            MCP_Utils.InvalidatePersonStatCache(person);

            person.ChangeWeightBasedOnHealth();

            PreviousMultiplier = effectivePower;
            Form = value.ID;
            CurrentAuraID = value.AuraID;

            if (AuraHandler != null)
            {
                if (CurrentAuraID != null && DBPU.BaseConsideredForms != null && !DBPU.BaseConsideredForms.Contains(Form))
                {
                    AuraHandler.AuraActive = DBPU.AurasEnabled;
                    AuraHandler.CreateAura(CurrentAuraID);
                }
                else
                {
                    AuraHandler.CurrentAuraID = CurrentAuraID;
                    AuraHandler.AuraActive = false;
                    AuraHandler.DestroyAura();
                }
            }

            if (Form != BaseForm)
                LastUsedForm = Form;

            var list = TransformationsData.transformations.GetOrderedEntries();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == formKey || (list[i].Value != null && list[i].Value.ID == formKey))
                {
                    FormSelected = i;
                    CurrentPage = i / 6;
                    LocalSelectedFrame = i % 6;
                    PositionSelectedFrame = LocalSelectedFrame;
                    SelectedFormId = value.ID;
                    SelectedMultiplier = effectivePower;
                    break;
                }
            }
        }

        public string GetFirstNonBaseForm()
        {
            if (TransformationsData == null || TransformationsData.transformations == null) return null;

            var data = TransformationsData.transformations.TransformationData;
            if (data == null) return null;

            foreach (var kvp in data)
            {
                if (kvp.Key != BaseForm) return kvp.Key;
            }

            return null;
        }

        public void QuickToggleTransform()
        {
            if (person == null || !person.IsAlive() || TransformationsData == null) return;

            if (Form == BaseForm)
            {
                string target = null;
                if (!string.IsNullOrEmpty(LastUsedForm)
                    && LastUsedForm != BaseForm
                    && TransformationsData.transformations.TransformationData.ContainsKey(LastUsedForm))
                    target = LastUsedForm;
                else
                    target = GetFirstNonBaseForm();

                if (string.IsNullOrEmpty(target) || target == BaseForm) return;
                ApplyForm(target);
            }
            else
            {
                ApplyForm(BaseForm);
            }
        }

        public class FrameInfo : MonoBehaviour
        {
            public int ID { get; set; }
        }
    }
}
