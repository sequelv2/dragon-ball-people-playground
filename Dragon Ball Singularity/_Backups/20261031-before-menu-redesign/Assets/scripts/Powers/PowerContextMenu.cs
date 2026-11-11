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
    public class PowerContextMenu : MonoBehaviour
    {
        public const int PowersPerPage = 9;

        public PersonBehaviour person;
        public CharacterPowers PowersData;

        public GameObject FramesHolder;
        private readonly List<Vector2> FramesPos = new List<Vector2>();

        public int PositionSelectedFrame = 0;
        public int PowerSelected = 0;
        private int LocalSelectedFrame = 0;
        private int Pages = 0;
        private int CurrentPage = 0;
        private int PreviousPage = 0;
        private bool MenuOpen = false;

        static TMP_FontAsset MenuFont;

        static TMP_FontAsset GetMenuFont()
        {
            if (MenuFont == null)
                MenuFont = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            return MenuFont;
        }

        void RecalculatePages()
        {
            int count = PowersData != null ? PowersData.PowerOrder.Count : 0;
            Pages = count <= PowersPerPage ? 0 : Mathf.CeilToInt(count / (float)PowersPerPage) - 1;
        }

        static void SetupPowerPageNavButton(GameObject go, Sprite sprite, Vector2 anchoredPos, Vector2 scale)
        {
            Image img = go.GetOrAddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = true;
            img.color = Color.white;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = MenuUI.SpritePixels(sprite);
            rt.localScale = new Vector3(scale.x, scale.y, 1f);
            if (go.GetComponent<Button>() == null)
                go.AddComponent<Button>();
        }

        public void Start()
        {
            RecalculatePages();
        }

        static void GetGridInnerSize(PowerMenuLayoutConfig layout, float panelW, float panelH, out float innerW, out float innerH)
        {
            float holderScale = layout.framesHolderScale > 0.01f ? layout.framesHolderScale : 1f;
            innerW = panelW / holderScale - layout.gridEdgePaddingX * 2f;
            innerH = panelH / holderScale - layout.gridEdgePaddingY * 2f;
        }

        void BuildGridPositions(PowerMenuLayoutConfig layout, float panelW = 0f, float panelH = 0f)
        {
            FramesPos.Clear();
            float cx = layout.gridCenterX;
            float cy = layout.gridCenterY;
            if (layout.gridAlignToCenterCanvas)
            {
                cx = 0f;
                cy = 0f;
            }

            float innerW = 0f;
            float innerH = 0f;
            if (panelW > 1f && panelH > 1f)
                GetGridInnerSize(layout, panelW, panelH, out innerW, out innerH);

            layout.GetGridSteps(out float colStep, out float rowStep, innerW, innerH);
            float[] gridY;
            if (layout.gridAlignToCenterCanvas)
            {
                if (!layout.gridAutoFitSlots)
                    rowStep = layout.gridRowSpacing;
                gridY = new float[] { cy + rowStep, cy, cy - rowStep };
            }
            else
                gridY = new float[] { layout.gridRow0Y, layout.gridRow1Y, layout.gridRow2Y };

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                    FramesPos.Add(new Vector2(cx - colStep + col * colStep, gridY[row]));
            }
        }

        public void Awake()
        {
            PowerMenuLayoutConfig.Load();
            BuildGridPositions(PowerMenuLayoutConfig.Active);

            var contextButtons = gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions;
            MCP_Utils.AddContextButtonInOrder(contextButtons, new ContextMenuButton("!PowersMenu", "Powers Menu", "Powers Menu", () => OpenPowersMenu()), 0);
        }

        public static PowerContextMenu GetForSelectedLimb()
        {
            var selection = SelectionController.Main;
            if (selection != null && selection.SelectedObjects != null)
            {
                foreach (PhysicalBehaviour selected in selection.SelectedObjects)
                {
                    var menu = FindPowerMenuFromPhysical(selected);
                    if (menu != null) return menu;
                }
            }

            var tool = Global.main?.ToolControllerBehaviour?.CurrentTool;
            if (tool != null && tool.ActiveSingleSelected != null)
            {
                var menu = FindPowerMenuFromPhysical(tool.ActiveSingleSelected);
                if (menu != null) return menu;
            }

            if (selection != null && selection.CurrentlyUnderMouse != null)
                return FindPowerMenuFromPhysical(selection.CurrentlyUnderMouse);

            return null;
        }

        static PowerContextMenu FindPowerMenuFromPhysical(PhysicalBehaviour pb)
        {
            if (pb == null) return null;

            var limb = pb.GetComponent<LimbBehaviour>();
            if (limb != null && limb.Person != null && limb.Person.Limbs != null && limb.Person.Limbs.Length > 0)
                return limb.Person.Limbs[0].GetComponent<PowerContextMenu>();

            var person = pb.GetComponent<PersonBehaviour>() ?? pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null || person.Limbs == null || person.Limbs.Length == 0) return null;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                if (person.Limbs[i] != null && person.Limbs[i].gameObject == pb.gameObject)
                    return person.Limbs[0].GetComponent<PowerContextMenu>();
            }

            return null;
        }

        public void OpenPowersMenu()
        {
                if (MenuOpen || PowersData == null) return;

                PowerSetup.RegisterUniversalPhysicalPowers(PowersData, person);
                PowersData.HarvestPowerStates();
                RecalculatePages();
                MenuOpen = true;
                PreviousPage = CurrentPage;
                if (CurrentPage < 0) CurrentPage = 0;
                if (CurrentPage > Pages) CurrentPage = Pages;

                var powerList = PowersData.GetOrderedPowers();
                if (powerList.Count == 0)
                {
                    MenuOpen = false;
                    return;
                }

                int idx = Mathf.Clamp(PositionSelectedFrame + (PowersPerPage * CurrentPage), 0, powerList.Count - 1);
                PositionSelectedFrame = Mathf.Clamp(PositionSelectedFrame, 0, PowersPerPage - 1);
                var firstValue = powerList[idx];
                PowerSelected = idx;
                LocalSelectedFrame = idx - (PowersPerPage * CurrentPage);

                PowerMenuLayoutConfig layout = new PowerMenuLayoutConfig();
                PowerMenuLayoutConfig.Active = layout;

                GameObject Canvas = new GameObject("PCanvas");
                Canvas.transform.SetParent(GameObject.Find("Canvas").transform);
                Image canvasBg = Canvas.GetOrAddComponent<Image>();
                canvasBg.color = Color.clear;
                canvasBg.raycastTarget = false;
                Canvas.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                float rootUniform = layout.menuRootUniform;
                Canvas.GetComponent<RectTransform>().localScale = new Vector3(rootUniform, rootUniform, 1f);
                Canvas.AddComponent<ExistIf>().obj = gameObject;

                GameObject centerCanvas = layout.CreateElement(Canvas.transform, "CenterCanvas", DBPU.PCanvas, layout.centerCanvas);
                MenuUI.DisableRaycast(centerCanvas.GetComponent<Image>());
                centerCanvas.transform.SetAsFirstSibling();
                Vector2 panelSize = MenuUI.GetRenderedSize(centerCanvas.GetComponent<RectTransform>());
                layout.ApplyAutoSlotSizing(panelSize.x, panelSize.y);
                BuildGridPositions(layout, panelSize.x, panelSize.y);

                GameObject PowersTitleText = new GameObject("Powers Title Text");
                PowersTitleText.transform.SetParent(centerCanvas.transform, false);
                RectTransform powersTitleRt = PowersTitleText.AddComponent<RectTransform>();
                layout.ApplyMenuTextLocalRect(
                    powersTitleRt,
                    layout.powersTitleLocalX, layout.powersTitleLocalY, layout.powersTitleLocalZ,
                    layout.powersTitleTextW, layout.powersTitleTextH);
                float titleScale = layout.powersTitleLocalScale > 0.01f ? layout.powersTitleLocalScale : 1f;
                powersTitleRt.localScale = new Vector3(titleScale, titleScale, 1f);
                TextMeshProUGUI powersTitleLabel = PowersTitleText.AddComponent<TextMeshProUGUI>();
                powersTitleLabel.text = "Powers";
                powersTitleLabel.fontSize = 10;
                powersTitleLabel.enableAutoSizing = false;
                powersTitleLabel.raycastTarget = false;
                powersTitleLabel.font = GetMenuFont();
                powersTitleLabel.alignment = TextAlignmentOptions.Center;
                powersTitleLabel.color = Color.black;
                MenuUI.ConfigureTitleDragArea(PowersTitleText, Canvas.GetComponent<RectTransform>(), new Vector2(80f, 14f));

                Transform gridParent = centerCanvas.transform;
                FramesHolder = new GameObject("FramesHolder");
                FramesHolder.transform.SetParent(gridParent, false);
                layout.ApplyFramesHolderTransform(FramesHolder.AddComponent<RectTransform>());

                GameObject illustrationGo = layout.CreateElement(Canvas.transform, "Illustration", DBPU.PIllustration, layout.illustration);

                GameObject Info = layout.CreateElement(Canvas.transform, "Info", DBPU.PInfo, layout.info);

                GameObject InfoNameText = new GameObject("Info Name Text");
                InfoNameText.transform.SetParent(Info.transform, false);
                layout.ApplyMenuTextLocalRect(
                    InfoNameText.AddComponent<RectTransform>(),
                    layout.infoNameLocalX, layout.infoNameLocalY, layout.infoNameLocalZ,
                    layout.infoNameTextW, layout.infoNameTextH);
                TextMeshProUGUI InfoNameTextStorage = InfoNameText.AddComponent<TextMeshProUGUI>();
                InfoNameTextStorage.text = firstValue.Name;
                InfoNameTextStorage.fontSize = 8;
                InfoNameTextStorage.enableAutoSizing = false;
                InfoNameTextStorage.raycastTarget = false;
                InfoNameTextStorage.font = GetMenuFont();
                InfoNameTextStorage.alignment = TextAlignmentOptions.Center;
                InfoNameTextStorage.color = Color.black;

                GameObject InfoDescriptionText = new GameObject("Info Description Text");
                InfoDescriptionText.transform.SetParent(Info.transform, false);
                RectTransform descRt = InfoDescriptionText.AddComponent<RectTransform>();
                layout.ApplyMenuTextLocalRect(
                    descRt,
                    layout.infoDescLocalX, layout.infoDescLocalY, layout.infoDescLocalZ,
                    layout.infoDescTextW, layout.infoDescTextH);
                float descScale = layout.infoDescLocalScale > 0.01f ? layout.infoDescLocalScale : 1f;
                descRt.localScale = new Vector3(descScale, descScale, 1f);
                TextMeshProUGUI InfoDescriptionTextStorage = InfoDescriptionText.AddComponent<TextMeshProUGUI>();
                InfoDescriptionTextStorage.text = firstValue.Description;
                InfoDescriptionTextStorage.fontSize = 4;
                InfoDescriptionTextStorage.font = GetMenuFont();
                InfoDescriptionTextStorage.alignment = TextAlignmentOptions.TopLeft;
                InfoDescriptionTextStorage.enableWordWrapping = true;
                InfoDescriptionTextStorage.color = Color.black;
                InfoDescriptionTextStorage.raycastTarget = false;

                float frameUniform = layout.GetFrameUniformScale();

                GameObject SelectFrame = new GameObject("SelectFrame");
                SelectFrame.transform.SetParent(FramesHolder.transform, false);
                MenuUI.ApplyImage(SelectFrame, DBPU.PSelectFrame, FramesPos[Mathf.Clamp(LocalSelectedFrame, 0, FramesPos.Count - 1)], frameUniform);
                MenuUI.DisableRaycast(SelectFrame.GetComponent<Image>());

                GameObject CloseBtn = layout.CreateElement(Canvas.transform, "Close", DBPU.PCloseButton, layout.close);
                CloseBtn.AddComponent<Button>().onClick.AddListener(() =>
                {
                    MenuOpen = false;
                    CurrentPage = PreviousPage;
                    PositionSelectedFrame = LocalSelectedFrame;
                    Destroy(Canvas);
                });

                Sprite initialOnOffSprite = firstValue.Passive ? DBPU.PButtonIdle
                    : (firstValue.Active ? DBPU.PButtonOn : DBPU.PButtonOff);
                GameObject OnOffBtn = layout.CreateElement(Canvas.transform, "OnOff", initialOnOffSprite, layout.onOff);
                Image onOffImg = OnOffBtn.GetComponent<Image>();
                Button onOffButton = OnOffBtn.AddComponent<Button>();

                layout.pageArea.y = layout.onOff.y;
                GameObject PageArea = layout.CreateElement(Canvas.transform, "PageArea", DBPU.PPageArea, layout.pageArea);

                layout.ApplyConfigTabPlacement();
                GameObject TabBtn = layout.CreateElement(Canvas.transform, "MenuTab", DBPU.PConfigsTabButton, layout.configTab);
                Image tabBtnImg = TabBtn.GetComponent<Image>();
                Button tabBtn = TabBtn.AddComponent<Button>();

                GameObject powerInstance = PowersData != null ? PowersData.gameObject : person.gameObject;
                Func<List<List<PowerMenuConfigEntry>>> getPowerConfigPages = () =>
                {
                    if (PowerSelected < 0 || PowerSelected >= powerList.Count)
                        return new List<List<PowerMenuConfigEntry>>();
                    return PowerMenuConfigData.GetPowerPages(powerList[PowerSelected], powerInstance, person);
                };

                var configUi = PowerMenuConfigUI.Build(
                    Canvas.transform,
                    layout,
                    gameObject.GetComponent<PhysicalBehaviour>(),
                    InfoNameTextStorage,
                    InfoDescriptionTextStorage,
                    getPowerConfigPages);
                GameObject configPanelRoot = configUi.Root;

                bool showingConfig = false;
                GameObject prevNavGo = null;
                GameObject nextNavGo = null;

                Action setTabSprite = () =>
                {
                    Sprite tabSprite = showingConfig ? DBPU.PPowerTabButton : DBPU.PConfigsTabButton;
                    layout.RefreshElement(TabBtn, tabSprite, layout.configTab);
                    tabBtnImg = TabBtn.GetComponent<Image>();
                };

                Action bringTabToFront = () => TabBtn.transform.SetAsLastSibling();

                Action applyPowersView = () =>
                {
                    showingConfig = false;
                    setTabSprite();
                    illustrationGo.SetActive(true);
                    configPanelRoot.SetActive(false);
                    if (PowerSelected >= 0 && PowerSelected < powerList.Count)
                    {
                        InfoNameTextStorage.text = powerList[PowerSelected].Name;
                        InfoDescriptionTextStorage.text = powerList[PowerSelected].Description;
                    }
                    bringTabToFront();
                };

                Action applyConfigView = () =>
                {
                    showingConfig = true;
                    setTabSprite();
                    illustrationGo.SetActive(false);
                    configPanelRoot.SetActive(true);
                    if (PowerSelected >= 0 && PowerSelected < powerList.Count)
                        InfoNameTextStorage.text = powerList[PowerSelected].Name;
                    configUi.Refresh?.Invoke();
                    configUi.SelectFirstEntry?.Invoke();
                    bringTabToFront();
                };

                tabBtn.onClick.AddListener(() =>
                {
                    if (showingConfig) applyPowersView();
                    else applyConfigView();
                });

                int totalPageCount = Mathf.Max(1, Mathf.CeilToInt(powerList.Count / (float)PowersPerPage));
                bool hasMultiplePages = powerList.Count > PowersPerPage;
                Pages = hasMultiplePages ? totalPageCount - 1 : 0;

                Image prevImg = null;
                Image nextImg = null;
                TextMeshProUGUI pageLabel = null;
                Button prevButton = null;
                Button nextButton = null;

                GameObject PageLabel = new GameObject("PageLabel");
                PageLabel.transform.SetParent(PageArea.transform, false);
                RectTransform pageLabelRt = PageLabel.AddComponent<RectTransform>();
                pageLabelRt.anchorMin = Vector2.zero;
                pageLabelRt.anchorMax = Vector2.one;
                pageLabelRt.offsetMin = Vector2.zero;
                pageLabelRt.offsetMax = Vector2.zero;
                pageLabelRt.anchoredPosition = new Vector2(0f, layout.pageLabelOffsetY);
                pageLabel = PageLabel.AddComponent<TextMeshProUGUI>();
                pageLabel.font = GetMenuFont();
                pageLabel.fontSize = 7;
                pageLabel.alignment = TextAlignmentOptions.Center;
                pageLabel.color = Color.black;
                pageLabel.raycastTarget = false;

                Action refreshPagination = () =>
                {
                    if (pageLabel != null)
                        pageLabel.text = (CurrentPage + 1) + " / " + totalPageCount;
                    if (!hasMultiplePages || prevImg == null || nextImg == null || prevButton == null || nextButton == null) return;
                    bool canPrev = CurrentPage > 0;
                    bool canNext = CurrentPage < Pages;
                    prevImg.color = canPrev ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    nextImg.color = canNext ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    prevButton.interactable = canPrev;
                    nextButton.interactable = canNext;
                };
                refreshPagination();

                Action refreshOnOffButton = () =>
                {
                    if (PowerSelected < 0 || PowerSelected >= powerList.Count)
                    {
                        onOffImg.sprite = DBPU.PButtonIdle;
                        onOffButton.interactable = false;
                        return;
                    }
                    var sel = powerList[PowerSelected];
                    if (sel.Passive)
                    {
                        onOffImg.sprite = DBPU.PButtonIdle;
                        onOffButton.interactable = false;
                        return;
                    }
                    onOffButton.interactable = true;
                    onOffImg.sprite = sel.Active ? DBPU.PButtonOn : DBPU.PButtonOff;
                };

                Image selectImg = SelectFrame.GetComponent<Image>();

                Action refreshSelectFrame = () =>
                {
                    int localIdx = Mathf.Clamp(LocalSelectedFrame, 0, FramesPos.Count - 1);
                    MenuUI.ApplyImage(SelectFrame, DBPU.PSelectFrame, FramesPos[localIdx], layout.GetFrameUniformScale());
                };

                Action<int> selectPower = (globalIndex) =>
                {
                    if (globalIndex < 0 || globalIndex >= powerList.Count) return;
                    var value = powerList[globalIndex];
                    int localFrame = globalIndex - (PowersPerPage * CurrentPage);
                    if (localFrame < 0 || localFrame >= FramesPos.Count) return;
                    LocalSelectedFrame = localFrame;
                    PowerSelected = globalIndex;
                    refreshSelectFrame();
                    selectImg.color = Color.white;
                    if (!showingConfig)
                    {
                        InfoNameTextStorage.text = value.Name;
                        InfoDescriptionTextStorage.text = value.Description;
                    }
                    refreshOnOffButton();
                    if (showingConfig)
                    {
                        InfoNameTextStorage.text = value.Name;
                        configUi.Refresh?.Invoke();
                        configUi.SelectFirstEntry?.Invoke();
                    }
                };

                float lastPowerClickTime = -1f;
                int lastPowerClickIndex = -1;
                const float powerDoubleClickWindow = 0.32f;

                Action rebuildFrames = null;

                Action<int> togglePowerAt = (globalIndex) =>
                {
                    if (globalIndex < 0 || globalIndex >= powerList.Count) return;
                    var value = powerList[globalIndex];
                    if (value.Passive) return;
                    PowersData.ApplyPower(value.ID);
                    rebuildFrames?.Invoke();
                    selectPower(globalIndex);
                };

                Action<int> handlePowerFrameClick = (globalIndex) =>
                {
                    float now = Time.unscaledTime;
                    if (globalIndex == lastPowerClickIndex && now - lastPowerClickTime <= powerDoubleClickWindow)
                    {
                        togglePowerAt(globalIndex);
                        lastPowerClickIndex = -1;
                        lastPowerClickTime = -1f;
                        return;
                    }

                    lastPowerClickIndex = globalIndex;
                    lastPowerClickTime = now;
                    selectPower(globalIndex);
                };

                rebuildFrames = () =>
                {
                    PowersData.HarvestPowerStates();
                    if (FramesHolder == null)
                    {
                        FramesHolder = new GameObject("FramesHolder");
                        FramesHolder.transform.SetParent(gridParent, false);
                        FramesHolder.AddComponent<RectTransform>();
                    }
                    else
                    {
                        for (int c = FramesHolder.transform.childCount - 1; c >= 0; c--)
                        {
                            Transform child = FramesHolder.transform.GetChild(c);
                            if (child.name.StartsWith("Frame"))
                                Destroy(child.gameObject);
                        }
                    }

                    layout.ApplyFramesHolderTransform(FramesHolder.GetComponent<RectTransform>());
                    if (SelectFrame.transform.parent != FramesHolder.transform)
                        SelectFrame.transform.SetParent(FramesHolder.transform, false);

                    float slotUniform = layout.GetFrameUniformScale();

                    int framesMissing = powerList.Count - (PowersPerPage * CurrentPage);
                    int currentFrames = 0;
                    while (framesMissing > 0 && currentFrames < PowersPerPage)
                    {
                        int globalIndex = currentFrames + (PowersPerPage * CurrentPage);
                        var value = powerList[globalIndex];
                        currentFrames += 1;
                        int capturedIndex = globalIndex;
                        int localId = currentFrames - 1;
                        bool active = value.Passive || value.Active;

                        Sprite frameSprite = active ? DBPU.PGreenFrame : DBPU.PRedFrame;
                        GameObject Frame = new GameObject("Frame" + currentFrames);
                        Frame.transform.SetParent(FramesHolder.transform, false);
                        MenuUI.ApplyImage(Frame, frameSprite, FramesPos[localId], slotUniform);

                        GameObject icon = new GameObject("Icon");
                        icon.transform.SetParent(Frame.transform, false);
                        Image iconImg = icon.AddComponent<Image>();
                        bool isDefaultIcon = value.Icon == null;
                        Sprite iconSprite = value.Icon ?? DBPU.PIconTemplate;
                        iconImg.sprite = iconSprite;
                        iconImg.preserveAspect = true;
                        iconImg.color = Color.white;
                        MenuUI.DisableRaycast(iconImg);
                        MenuUI.FitIconInsideFrame(icon.GetComponent<RectTransform>(), iconSprite, Frame.GetComponent<RectTransform>(), layout.iconPadding, layout.iconScaleX, layout.iconScaleY, isDefaultIcon);

                        Frame.AddComponent<PowerFrameInfo>().ID = localId;
                        Button frameBtn = Frame.GetOrAddComponent<Button>();
                        frameBtn.onClick.AddListener(() => handlePowerFrameClick(capturedIndex));
                        Image frameImg = Frame.GetComponent<Image>();
                        if (frameImg != null) frameBtn.targetGraphic = frameImg;
                        framesMissing -= 1;
                    }

                    if (Mathf.FloorToInt(PowerSelected / (float)PowersPerPage) == CurrentPage)
                        selectImg.color = Color.white;
                    else
                        selectImg.color = Color.clear;

                    SelectFrame.transform.SetAsLastSibling();
                    refreshSelectFrame();
                    refreshOnOffButton();
                };

                rebuildFrames();
                selectPower(idx);

                if (hasMultiplePages)
                {
                    float barY = layout.pageArea.y;
                    float pageX = layout.pageArea.x;
                    Vector2 navScale = new Vector2(0.75f, 0.75f);
                    Sprite prevSprite = DBPU.PPrevious != null ? DBPU.PPrevious : DBPU.TPrevious;
                    Sprite nextSprite = DBPU.PNext != null ? DBPU.PNext : DBPU.TNext;
                    float pageHalfW = layout.pageArea.targetW * layout.ScaleFor(DBPU.PPageArea, layout.pageArea) * 0.5f;
                    float btnHalfW = MenuUI.SpritePixels(prevSprite).x * navScale.x * 0.5f;
                    float navGap = 6f;
                    float prevX = pageX - pageHalfW - btnHalfW - navGap;
                    float nextX = pageX + pageHalfW + btnHalfW + navGap;

                    GameObject PreviousPageBtn = new GameObject("PreviousPage");
                    PreviousPageBtn.transform.SetParent(Canvas.transform, false);
                    SetupPowerPageNavButton(PreviousPageBtn, prevSprite, new Vector2(prevX, barY), navScale);
                    prevImg = PreviousPageBtn.GetComponent<Image>();
                    prevButton = PreviousPageBtn.GetComponent<Button>();
                    prevNavGo = PreviousPageBtn;

                    GameObject NextPageBtn = new GameObject("NextPage");
                    NextPageBtn.transform.SetParent(Canvas.transform, false);
                    SetupPowerPageNavButton(NextPageBtn, nextSprite, new Vector2(nextX, barY), navScale);
                    nextImg = NextPageBtn.GetComponent<Image>();
                    nextButton = NextPageBtn.GetComponent<Button>();
                    nextNavGo = NextPageBtn;

                    PreviousPageBtn.transform.SetAsLastSibling();
                    NextPageBtn.transform.SetAsLastSibling();
                    refreshPagination();

                    prevButton.onClick.AddListener(() =>
                    {
                        if (CurrentPage <= 0) return;
                        CurrentPage -= 1;
                        rebuildFrames();
                        refreshPagination();
                        if (PowerSelected >= PowersPerPage * CurrentPage && PowerSelected < PowersPerPage * (CurrentPage + 1))
                            selectPower(PowerSelected);
                        else
                            selectPower(Mathf.Min(powerList.Count - 1, PowersPerPage * CurrentPage));
                    });

                    nextButton.onClick.AddListener(() =>
                    {
                        if (CurrentPage >= Pages) return;
                        CurrentPage += 1;
                        rebuildFrames();
                        refreshPagination();
                        if (PowerSelected >= PowersPerPage * CurrentPage && PowerSelected < PowersPerPage * (CurrentPage + 1))
                            selectPower(PowerSelected);
                        else
                            selectPower(Mathf.Min(powerList.Count - 1, PowersPerPage * CurrentPage));
                    });
                }

                onOffButton.onClick.AddListener(() =>
                {
                    if (PowerSelected < 0 || PowerSelected >= powerList.Count) return;
                    var sel = powerList[PowerSelected];
                    if (sel.Passive) return;
                    PowersData.ApplyPower(sel.ID);
                    rebuildFrames();
                    selectPower(PowerSelected);
                });

                bringTabToFront();
                applyPowersView();

                Action rebuildGridLayout = () =>
                {
                    Vector2 panelSz = centerCanvas != null
                        ? MenuUI.GetRenderedSize(centerCanvas.GetComponent<RectTransform>())
                        : Vector2.zero;
                    if (panelSz.x > 1f && panelSz.y > 1f)
                        layout.ApplyAutoSlotSizing(panelSz.x, panelSz.y);
                    BuildGridPositions(layout, panelSz.x, panelSz.y);
                    refreshSelectFrame();
                    rebuildFrames();
                    if (PowerSelected >= 0 && PowerSelected < powerList.Count)
                        selectPower(PowerSelected);
                };

                MenuUI.AttachResizeHandle(
                    centerCanvas.transform,
                    () => layout.menuRootUniform,
                    (u) =>
                    {
                        layout.menuRootUniform = u;
                        Canvas.GetComponent<RectTransform>().localScale = new Vector3(u, u, 1f);
                        rebuildGridLayout();
                    });
        }

        public class PowerFrameInfo : MonoBehaviour
        {
            public int ID { get; set; }
        }
    }
}
