using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU.LiveSpriteEditor
{
    public sealed class SpriteEditorRuntime : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.4f);
        private static readonly Color WindowColor = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.14f, 0.14f, 0.14f, 1f);
        private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.06f, 0f);
        private static readonly Color PanelInnerColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color PanelBorderColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color PanelHighlightBorderColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        private static readonly Color CanvasFrameColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color CanvasBackColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color AccentColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        private static readonly Color AccentSoftColor = new Color(0.22f, 0.36f, 0.58f, 1f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color ButtonSelectedColor = new Color(0.28f, 0.42f, 0.68f, 1f);
        private static readonly Color ButtonDangerColor = new Color(0.45f, 0.12f, 0.14f, 1f);
        private static readonly Color ButtonDangerBorderColor = new Color(0.85f, 0.25f, 0.28f, 1f);
        private static readonly Color ButtonBorderColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color IconTintColor = new Color(0.82f, 0.86f, 0.95f, 1f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color MutedTextColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        private static readonly Color DisabledTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color TransparentColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Vector3 PreviewWorldOrigin = new Vector3(100000f, 100000f, 0f);
        private const float WindowDesignWidth = 1480f;
        private const float WindowDesignHeight = 860f;
        private const float HeaderHeight = 34f;
        private const float ToolbarHeight = 38f;
        private const float UiScaleMin = 0.65f;
        private const float UiScaleMax = 1.45f;
        private const string UiScalePrefsKey = "DBPU.LiveSpriteEditor.UiScale";
        private const int CornerRadius = 0;
        private const float CanvasZoomMin = 0.35f;
        private const float CanvasZoomMax = 10f;
        private const float PreviewPadding = 0.08f;
        private const int PreviewLayer = 31;
        private const int PreviewMaxTextureSize = 512;
        private const int PreviewMinTextureSize = 96;

        private static readonly Color[] PresetColors =
        {
            new Color32(0, 0, 0, 255),
            new Color32(255, 255, 255, 255),
            new Color32(228, 65, 75, 255),
            new Color32(255, 154, 0, 255),
            new Color32(255, 214, 10, 255),
            new Color32(65, 190, 90, 255),
            new Color32(66, 157, 245, 255),
            new Color32(108, 92, 231, 255),
            new Color32(232, 67, 147, 255),
            new Color32(124, 77, 34, 255),
            new Color32(99, 110, 114, 255),
            new Color32(0, 206, 201, 255),
            new Color32(129, 236, 236, 255),
            new Color32(162, 155, 254, 255),
            new Color32(253, 203, 110, 255),
            new Color32(214, 48, 49, 255),
            new Color32(255, 118, 117, 255),
            new Color32(174, 231, 90, 255)
        };

        private static SpriteEditorRuntime instance;
        private static LimbSpriteState.LayerSnapshot layerClipboard;
        private static Color32[] pixelClipboard;
        private static int pixelClipboardWidth;
        private static int pixelClipboardHeight;

        private readonly List<Button> limbButtons = new List<Button>();
        private readonly Dictionary<Button, LimbBehaviour> limbByButton = new Dictionary<Button, LimbBehaviour>();
        private readonly List<Button> objectPartButtons = new List<Button>();
        private readonly Dictionary<Button, SpriteRenderer> objectRendererByButton = new Dictionary<Button, SpriteRenderer>();
        private Button hairButton;
        private SpriteRenderer hairRendererByButton;
        private readonly List<Button> layerRowButtons = new List<Button>();
        private readonly Dictionary<ToolMode, Button> toolButtons = new Dictionary<ToolMode, Button>();
        private readonly List<SwatchView> swatchViews = new List<SwatchView>();
        private readonly List<Color> colorHistory = new List<Color>();
        private readonly List<SwatchView> colorHistoryViews = new List<SwatchView>();
        private readonly List<SpriteRenderer> previewSpriteRenderers = new List<SpriteRenderer>();
        private readonly Dictionary<int, Material> previewMaterialCache = new Dictionary<int, Material>();
        private readonly List<Button> presetBrowserButtons = new List<Button>();
        private readonly Dictionary<Button, SpritePresetBrowserItem> presetBrowserItemByButton = new Dictionary<Button, SpritePresetBrowserItem>();
        private readonly List<Texture2D> presetBrowserTextures = new List<Texture2D>();
        private readonly Dictionary<string, Sprite> roundedSpriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<UiIconKind, Sprite> iconSpriteCache = new Dictionary<UiIconKind, Sprite>();
        private readonly List<Texture2D> generatedUiTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedUiSprites = new List<Sprite>();

        private GameObject uiRoot;
        private GameObject presetModalRoot;
        private GameObject layerRenameModalRoot;
        private GameObject keybindModalRoot;
        private RectTransform panelHostRect;
        private RectTransform windowRect;
        private RawImage canvasImage;
        private RawImage previewImage;
        private RawImage presetDetailPreviewImage;
        private RectTransform canvasHolderRect;
        private RectTransform selectionOverlayRect;
        private RawImage selectionOverlayRawImage;
        private Texture2D selectionOverlayTexture;
        private AspectRatioFitter canvasAspect;
        private AspectRatioFitter previewAspect;
        private CanvasGroup windowCanvasGroup;
        private Image selectedColorImage;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private TextMeshProUGUI canvasStatusText;
        private TextMeshProUGUI infoText;
        private TextMeshProUGUI brushSizeText;
        private TextMeshProUGUI uiScaleText;
        private TextMeshProUGUI colorDetailsText;
        private TextMeshProUGUI presetDetailText;
        private TextMeshProUGUI presetStatusText;
        private Transform limbButtonContainer;
        private Transform layerButtonContainer;
        private Transform presetBrowserListContainer;
        private Slider brushSlider;
        private Slider opacitySlider;
        private Slider uiScaleSlider;
        private Button undoButton;
        private Button redoButton;
        private Button resetLimbButton;
        private Button resetCharacterButton;
        private Button copyPartButton;
        private Button pastePartButton;
        private Button copyTargetButton;
        private Button pasteTargetButton;
        private Button keybindsButton;
        private GameObject targetClipboardRowObject;
        private Button selectedColorButton;
        private Button pickColorButton;
        private Button doneButton;
        private Button closeButton;
        private Button presetLibraryTabButton;
        private Button presetBundleImportsTabButton;
        private Button presetPngImportsTabButton;
        private Button saveTargetPresetButton;
        private Button saveSelectedPartPresetButton;
        private Button applyPresetButton;
        private Button deletePresetButton;
        private Button importBundleButton;
        private Button exportBundleButton;
        private Button importPngButton;
        private Button exportPngButton;
        private Button headerImportPngButton;
        private Button headerExportPngButton;
        private Button headerExportBodyPngButton;
        private Button resizeCanvasButton;
        private Button expandCanvas1Button;
        private Button expandCanvas2Button;
        private Button expandCanvas4Button;
        private Button shrinkCanvas1Button;
        private Button presetModalCloseButton;
        private GameObject resizeModalRoot;
        private TMP_InputField layerRenameInput;
        private TMP_InputField resizeWidthInput;
        private TMP_InputField resizeHeightInput;
        private int resizeAnchorX = 1;
        private int resizeAnchorY = 1;
        private readonly Button[,] resizeAnchorButtons = new Button[3, 3];
        private TMP_FontAsset uiFont;
        private Material uiFontMaterial;
        private GameObject editorCanvasObject;
        private SpriteEditorCanvasSurface canvasSurface;
        private LimbSpriteState activeStrokeState;
        private Button objectSelectionButton;
        private Transform previewRendererRoot;
        private Sprite externalPencilSprite;
        private Camera previewCamera;
        private RenderTexture previewRenderTexture;

        private CharacterSpriteEditorTarget currentTarget;
        private ObjectSpriteEditorTarget currentObjectTarget;
        private LimbBehaviour currentLimb;
        private ToolMode currentTool = ToolMode.Select;
        private bool[] marqueeBaseMask;
        private bool marqueeAdds;
        private bool marqueeSubtracts;
        private EditorLayerKind activeEditorLayer = EditorLayerKind.Paint;
        private int activeEditorLayerIndex;
        private int layerRenameIndex = -1;
        private Color selectedColor = Color.black;
        private int brushSize = 1;
        private Vector2Int previousPixel;
        private Vector2Int selectionStartPixel;
        private Vector2Int previousSelectionPixel;
        private Vector2Int moveStartPixel;
        private Vector2Int moveStartRectPosition;
        private RectInt selectedPixelRect;
        private bool[] selectedPixelMask;
        private bool[] movingSelectionMask;
        private int selectedPixelMaskWidth;
        private int selectedPixelMaskHeight;
        private int movingSelectionMaskWidth;
        private int movingSelectionMaskHeight;
        private Color32[] movingSelectionPixels;
        private Color32[] moveBasePixels;
        private Vector2 panStartLocalPoint;
        private Vector2 panStartOffset;
        private Vector2 canvasPanOffset;
        private bool editorOpen;
        private bool uiBlockerApplied;
        private bool strokeInProgress;
        private bool strokeChanged;
        private bool suppressColorCallbacks;
        private bool suppressUiScaleCallbacks;
        private bool nativeColorPickerOpen;
        private bool nativePngFileDialogOpen;
        private bool presetModalOpen;
        private bool hasPixelSelection;
        private bool selectionDragInProgress;
        private bool moveSelectionInProgress;
        private bool panInProgress;
        private bool paintOpaqueOnlyStroke;
        private bool canvasResizeMode;
        private bool canvasResizeDragInProgress;
        private CanvasResizeHandleKind canvasResizeDragHandle = CanvasResizeHandleKind.None;
        private int canvasResizeBaseWidth;
        private int canvasResizeBaseHeight;
        private int canvasResizePadLeft;
        private int canvasResizePadRight;
        private int canvasResizePadBottom;
        private int canvasResizePadTop;
        private Vector2 canvasResizeDragStartLocal;
        private GameObject canvasResizeHandlesRoot;
        private RectTransform canvasResizePreviewRect;
        private Image canvasResizePreviewImage;
        private RectMask2D canvasHolderMask;
        private TextMeshProUGUI canvasResizeModeText;
        private float canvasZoom = 1f;
        private float uiScale = 1f;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private string presetStatusMessage = string.Empty;
        private SpritePresetBrowserMode presetBrowserMode = SpritePresetBrowserMode.Library;
        private SpritePresetBrowserItem selectedPresetItem;

        public static void Open(CharacterSpriteEditorTarget target, LimbBehaviour limb)
        {
            if (instance != null)
            {
                instance.OpenInternal(target, limb, false);
            }
        }

        public static bool IsOpen
        {
            get { return instance != null && instance.editorOpen; }
        }

        public static bool BlocksGameInput
        {
            get { return IsOpen; }
        }

        public static void OpenHair(CharacterSpriteEditorTarget target)
        {
            if (instance != null)
            {
                instance.OpenInternal(target, null, true);
            }
        }

        public static void Open(ObjectSpriteEditorTarget target)
        {
            if (instance != null)
            {
                instance.OpenInternal(target);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(UiScalePrefsKey, 1f), UiScaleMin, UiScaleMax);
        }

        private void OnDisable()
        {
            FinalizeEditorForTeardown();
            ReleaseUiBlocker();
        }

        private void OnDestroy()
        {
            FinalizeEditorForTeardown();
            ReleaseUiBlocker();

            DestroyEditorUi();

            DestroyPreviewWorld();
            DestroyGeneratedUiAssets();

            if (instance == this)
            {
                instance = null;
            }
        }

        private void FinalizeEditorForTeardown()
        {
            ClosePresetModal();
            CloseResizeModal();
            CloseLayerRenameModal();
            CloseKeybindModal();
            CloseNativeColorPickerIfNeeded();
            FinalizeActiveStroke();

            if (currentTarget != null)
            {
                currentTarget.FinalizeAllStatesForWorldDisplay();
            }
            else if (currentObjectTarget != null)
            {
                currentObjectTarget.FinalizeStateForWorldDisplay();
            }

            editorOpen = false;
            strokeInProgress = false;
            strokeChanged = false;
            currentTarget = null;
            currentObjectTarget = null;
            currentLimb = null;

            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
            }

            ClearPresetBrowser();
        }

        private void Update()
        {
            if (!editorOpen)
            {
                return;
            }

            if (!IsCurrentTargetValid())
            {
                CloseEditor();
                return;
            }

            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            {
                UpdateWindowSize();
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
            }

            HandleKeyboardShortcuts();
        }

        private void OpenInternal(CharacterSpriteEditorTarget target, LimbBehaviour limb, bool preferHair)
        {
            if (target == null)
            {
                return;
            }

            currentTarget = target;
            currentObjectTarget = null;
            currentLimb = null;
            target.ClearAccessorySelection();
            ClearSelectionState();
            ResetLayerState();

            if (preferHair)
            {
                SpriteRenderer hair = target.GetHairRenderer(true);
                if (hair != null)
                {
                    target.SelectAccessory(hair);
                }
            }

            if (!target.IsAccessorySelected)
            {
                currentLimb = target.GetPreferredLimb(limb);
            }

            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                DialogBoxManager.Notification("No editable sprite was found on that character.");
                currentTarget = null;
                currentLimb = null;
                target.ClearAccessorySelection();
                return;
            }

            state.PrepareForEditing();

            if (uiRoot == null)
            {
                try
                {
                    BuildUi();
                }
                catch (Exception exception)
                {
                Debug.LogError("[DB Singularity] Failed to build Sprite Editor UI: " + exception);

                    if (uiRoot != null || editorCanvasObject != null)
                    {
                        DestroyEditorUi();
                    }
                }
            }

            if (uiRoot == null)
            {
                DialogBoxManager.Notification("Could not build the sprite editor UI.");
                return;
            }

            editorOpen = true;
            strokeInProgress = false;
            strokeChanged = false;
            SetTool(ToolMode.Select);
            uiRoot.SetActive(true);
            uiRoot.transform.SetAsLastSibling();
            ApplyUiBlocker();
            RebuildLimbButtons();
            UpdateWindowSize();
            RefreshAll();
            if (currentTarget != null && currentTarget.Person != null)
                global::DBPU.DbpuMakerPhysics.CalmPerson(currentTarget.Person);
        }

        private void OpenInternal(ObjectSpriteEditorTarget target)
        {
            if (target == null)
            {
                return;
            }

            currentTarget = null;
            currentObjectTarget = target;
            currentLimb = null;
            ClearSelectionState();
            ResetLayerState();

            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                DialogBoxManager.Notification("No editable sprite was found on that object.");
                currentObjectTarget = null;
                return;
            }

            state.PrepareForEditing();

            if (uiRoot == null)
            {
                try
                {
                    BuildUi();
                }
                catch (Exception exception)
                {
                Debug.LogError("[DB Singularity] Failed to build Sprite Editor UI: " + exception);

                    if (uiRoot != null || editorCanvasObject != null)
                    {
                        DestroyEditorUi();
                    }
                }
            }

            if (uiRoot == null)
            {
                DialogBoxManager.Notification("Could not build the sprite editor UI.");
                return;
            }

            editorOpen = true;
            strokeInProgress = false;
            strokeChanged = false;
            SetTool(ToolMode.Select);
            uiRoot.SetActive(true);
            uiRoot.transform.SetAsLastSibling();
            ApplyUiBlocker();
            RebuildLimbButtons();
            UpdateWindowSize();
            RefreshAll();
            if (currentObjectTarget != null && currentObjectTarget.PhysicalBehaviour != null)
            {
                var rb = currentObjectTarget.PhysicalBehaviour.rigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    try { rb.Sleep(); } catch { }
                }
            }
        }

        private void CloseEditor()
        {
            ClosePresetModal();
            CloseResizeModal();
            CloseLayerRenameModal();
            CloseKeybindModal();
            CloseNativeColorPickerIfNeeded();
            if (canvasResizeMode)
            {
                SetCanvasResizeMode(false);
            }

            FinalizeActiveStroke();

            if (currentTarget != null)
            {
                currentTarget.FinalizeAllStatesForWorldDisplay();
                if (currentTarget.Person != null)
                    global::DBPU.DbpuMakerPhysics.CalmPerson(currentTarget.Person);
            }
            else if (currentObjectTarget != null)
            {
                currentObjectTarget.FinalizeStateForWorldDisplay();
            }

            editorOpen = false;
            strokeInProgress = false;
            strokeChanged = false;
            currentTarget = null;
            currentObjectTarget = null;
            currentLimb = null;

            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
            }

            ReleaseUiBlocker();
            EventSystem currentSystem = EventSystem.current;
            if (currentSystem != null)
            {
                currentSystem.SetSelectedGameObject(null);
            }
        }

        private bool IsCurrentTargetValid()
        {
            if (currentTarget != null)
            {
                return currentTarget.Person != null &&
                       currentTarget.gameObject != null &&
                       currentTarget.gameObject.scene.IsValid() &&
                       currentTarget.gameObject.scene.isLoaded;
            }

            if (currentObjectTarget != null)
            {
                PhysicalBehaviour physical = currentObjectTarget.PhysicalBehaviour;
                return physical != null &&
                       physical.gameObject != null &&
                       physical.gameObject.scene.IsValid() &&
                       physical.gameObject.scene.isLoaded;
            }

            return false;
        }

        private void HandleKeyboardShortcuts()
        {
            if (presetModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ClosePresetModal();
                }

                return;
            }

            if (resizeModalRoot != null && resizeModalRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseResizeModal();
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ApplyResizeFromModal();
                }

                return;
            }

            if (layerRenameModalRoot != null && layerRenameModalRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseLayerRenameModal();
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ApplyLayerRenameFromModal();
                }

                return;
            }

            if (keybindModalRoot != null && keybindModalRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseKeybindModal();
                }

                return;
            }

            if (nativeColorPickerOpen || nativePngFileDialogOpen)
            {
                return;
            }

            LimbSpriteState state = GetSelectedState();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (canvasResizeMode)
                {
                    if (canvasResizeDragInProgress)
                    {
                        CancelCanvasResizeDrag();
                    }
                    else
                    {
                        SetCanvasResizeMode(false);
                    }

                    return;
                }

                CloseEditor();
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (shift && !ctrl)
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    CopyWholeTargetLayers();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.V))
                {
                    PasteWholeTargetLayers();
                    return;
                }
            }

            if (ctrl && state != null)
            {
                if (Input.GetKeyDown(KeyCode.D))
                {
                    ClearSelectionState();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Z))
                {
                    state.Undo();
                    RefreshAll();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Y))
                {
                    state.Redo();
                    RefreshAll();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.C))
                {
                    if (shift)
                    {
                        CopyActiveLayerToClipboard();
                        return;
                    }

                    CopyPixelSelection(false);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.X))
                {
                    CopyPixelSelection(true);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.V))
                {
                    if (shift)
                    {
                        PasteLayerFromClipboard();
                        return;
                    }

                    PastePixelSelection();
                    return;
                }
            }

            if (!ctrl && !shift && Input.GetKeyDown(KeyCode.C))
            {
                SetCanvasResizeMode(!canvasResizeMode);
                return;
            }

            if (canvasResizeMode)
            {
                return;
            }

            if (state != null && hasPixelSelection && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
            {
                ClearPixelSelectionPixels(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                SetTool(ToolMode.Brush);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                SetTool(ToolMode.Eraser);
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                SetTool(ToolMode.Fill);
            }
            else if (Input.GetKeyDown(KeyCode.I))
            {
                SetTool(ToolMode.Eyedropper);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                SetTool(ToolMode.Select);
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                SetTool(ToolMode.Move);
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                brushSize = Mathf.Clamp(brushSize - 1, 1, 8);
                SyncBrushSlider();
                RefreshStatusText();
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                brushSize = Mathf.Clamp(brushSize + 1, 1, 8);
                SyncBrushSlider();
                RefreshStatusText();
            }
            else
            {
                for (int size = 1; size <= 8; size++)
                {
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + size)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + size)))
                    {
                        brushSize = size;
                        SyncBrushSlider();
                        RefreshStatusText();
                        break;
                    }
                }
            }
        }

        private void BuildUi()
        {
            GameObject canvasObject = EnsureEditorCanvas();
            if (canvasObject == null)
            {
                return;
            }

            if (!InitializeTextResources())
            {
                return;
            }

            uiRoot = CreateUiObject("DbpuSpriteEditorUi", canvasObject.transform);
            RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
            Stretch(rootRect);
            Image overlay = uiRoot.AddComponent<Image>();
            overlay.color = BackgroundColor;
            uiRoot.SetActive(false);

            GameObject panelHost = CreateUiObject("PanelHost", uiRoot.transform);
            panelHostRect = panelHost.GetComponent<RectTransform>();
            panelHostRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelHostRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelHostRect.pivot = new Vector2(0.5f, 0.5f);
            panelHostRect.anchoredPosition = new Vector2(0f, 28f);
            panelHostRect.sizeDelta = Vector2.zero;

            GameObject windowObject = CreateUiObject("Panel", panelHost.transform);
            windowRect = windowObject.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(WindowDesignWidth, WindowDesignHeight);
            Image windowImage = windowObject.AddComponent<Image>();
            windowImage.color = WindowColor;
            Outline windowOutline = windowObject.AddComponent<Outline>();
            windowOutline.effectColor = Color.white;
            windowOutline.effectDistance = new Vector2(1.2f, -1.2f);
            windowCanvasGroup = windowObject.AddComponent<CanvasGroup>();

            GameObject headerObject = CreateUiObject("TitleBar", windowObject.transform);
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(WindowDesignWidth, HeaderHeight);
            Image headerImage = headerObject.AddComponent<Image>();
            headerImage.color = HeaderColor;
            headerImage.raycastTarget = true;
            var titleDrag = headerObject.AddComponent<ModMenuDragResize>();
            titleDrag.moveRoot = panelHostRect;

            titleText = CreateText("Title", headerObject.transform, "Sprite Editor", 22, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            titleText.characterSpacing = 0f;
            titleText.raycastTarget = false;
            Stretch(titleText.rectTransform);
            titleText.margin = new Vector4(8f, 0f, 8f, 0f);

            GameObject toolbarObject = CreateUiObject("Toolbar", windowObject.transform);
            RectTransform toolbarRect = toolbarObject.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0f, 1f);
            toolbarRect.anchorMax = new Vector2(1f, 1f);
            toolbarRect.pivot = new Vector2(0.5f, 1f);
            toolbarRect.offsetMin = new Vector2(6f, -(HeaderHeight + ToolbarHeight));
            toolbarRect.offsetMax = new Vector2(-6f, -HeaderHeight);
            Image toolbarImage = toolbarObject.AddComponent<Image>();
            toolbarImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            subtitleText = CreateText("Subtitle", toolbarObject.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            SetRect(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 4f), new Vector2(-520f, -4f));

            headerImportPngButton = CreateButton("ImportButton", toolbarObject.transform, "Import", new UnityEngine.Events.UnityAction(ImportPngFromFileDialog));
            SetRect(headerImportPngButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-508f, -15f), new Vector2(-420f, 15f));
            SetButtonFontSize(headerImportPngButton, 15f);
            StyleButton(headerImportPngButton, false, false);

            headerExportPngButton = CreateButton("ExportButton", toolbarObject.transform, "Export", new UnityEngine.Events.UnityAction(ExportSelectedPngFromHeader));
            SetRect(headerExportPngButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-412f, -15f), new Vector2(-324f, 15f));
            SetButtonFontSize(headerExportPngButton, 15f);
            StyleButton(headerExportPngButton, false, false);

            headerExportBodyPngButton = CreateButton("ExportAllButton", toolbarObject.transform, "Export All", new UnityEngine.Events.UnityAction(ExportBodyPngsFromHeader));
            SetRect(headerExportBodyPngButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-316f, -15f), new Vector2(-208f, 15f));
            SetButtonFontSize(headerExportBodyPngButton, 15f);
            StyleButton(headerExportBodyPngButton, false, false);

            doneButton = CreateButton("ApplyButton", toolbarObject.transform, "Apply", new UnityEngine.Events.UnityAction(ApplyPendingHitboxesAndClose));
            SetRect(doneButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-200f, -15f), new Vector2(-10f, 15f));
            SetButtonFontSize(doneButton, 16f);
            StyleButton(doneButton, true, false);

            if (global::DBPU.DBPU.PCloseButton != null)
            {
                const float closeTargetHeight = 52f;
                const float closeGap = 8f;
                Vector2 closePx = MenuUI.SpritePixels(global::DBPU.DBPU.PCloseButton);
                float closeScale = closeTargetHeight / Mathf.Max(1f, closePx.y);
                GameObject closeObject = new GameObject("Close", typeof(RectTransform));
                closeObject.transform.SetParent(panelHost.transform, false);
                RectTransform closeRt = closeObject.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0.5f, 0.5f);
                closeRt.anchorMax = new Vector2(0.5f, 0.5f);
                closeRt.pivot = new Vector2(0.5f, 0.5f);
                float closeHalfH = closePx.y * closeScale * 0.5f;
                MenuUI.ApplyImage(closeObject, global::DBPU.DBPU.PCloseButton, new Vector2(0f, -WindowDesignHeight * 0.5f - closeGap - closeHalfH), closeScale);
                closeButton = closeObject.AddComponent<Button>();
                closeButton.onClick.AddListener(new UnityEngine.Events.UnityAction(CloseEditor));
            }
            else
            {
                closeButton = CreateButton("CloseButton", toolbarObject.transform, "X", new UnityEngine.Events.UnityAction(CloseEditor));
                SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, -13f), new Vector2(-10f, 13f));
                SetButtonFontSize(closeButton, 16f);
                StyleButton(closeButton, false, true);
            }

            GameObject bodyObject = CreateUiObject("Body", windowObject.transform);
            RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(6f, 6f);
            bodyRect.offsetMax = new Vector2(-6f, -(HeaderHeight + ToolbarHeight + 4f));
            HorizontalLayoutGroup bodyLayout = bodyObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(0, 0, 0, 0);
            bodyLayout.spacing = 6f;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;

            Transform leftPanel = CreateMainColumn("LeftPanel", bodyObject.transform, 280f, 0f);
            Transform centerPanel = CreateMainColumn("CenterPanel", bodyObject.transform, 0f, 1f);
            Transform rightPanel = CreateMainColumn("RightPanel", bodyObject.transform, 300f, 0f);

            Transform limbSection = CreateSection(leftPanel, "Limbs", 0f, 1f);
            ScrollRect limbScroll = limbSection.gameObject.AddComponent<ScrollRect>();
            limbScroll.horizontal = false;
            limbScroll.vertical = true;
            limbScroll.movementType = ScrollRect.MovementType.Clamped;
            limbScroll.scrollSensitivity = 24f;

            GameObject limbViewport = CreateUiObject("LimbViewport", limbSection);
            RectTransform limbViewportRect = limbViewport.GetComponent<RectTransform>();
            Stretch(limbViewportRect);
            Image limbViewportImage = limbViewport.AddComponent<Image>();
            limbViewportImage.color = new Color(1f, 1f, 1f, 0.002f);
            Mask limbViewportMask = limbViewport.AddComponent<Mask>();
            limbViewportMask.showMaskGraphic = false;
            limbScroll.viewport = limbViewportRect;

            GameObject limbContent = CreateUiObject("LimbContent", limbViewport.transform);
            RectTransform limbContentRect = limbContent.GetComponent<RectTransform>();
            limbContentRect.anchorMin = new Vector2(0f, 1f);
            limbContentRect.anchorMax = new Vector2(1f, 1f);
            limbContentRect.pivot = new Vector2(0.5f, 1f);
            limbContentRect.anchoredPosition = Vector2.zero;
            limbContentRect.sizeDelta = Vector2.zero;
            limbScroll.content = limbContentRect;

            VerticalLayoutGroup limbLayout = limbContent.AddComponent<VerticalLayoutGroup>();
            limbLayout.spacing = 4f;
            limbLayout.childAlignment = TextAnchor.UpperCenter;
            limbLayout.childControlWidth = true;
            limbLayout.childControlHeight = true;
            limbLayout.childForceExpandWidth = true;
            limbLayout.childForceExpandHeight = false;
            ContentSizeFitter limbContentFitter = limbContent.AddComponent<ContentSizeFitter>();
            limbContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            limbContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            limbButtonContainer = limbContent.transform;

            Transform toolSection = CreateSection(leftPanel, "Tools", 168f, 0f);
            CreateToolSection(toolSection);

            Transform colorSection = CreateSection(leftPanel, "Color", 210f, 0f);
            CreateColorSection(colorSection);

            Transform canvasSection = CreateSection(centerPanel, "Sprite", 0f, 1f);
            CreateCanvasSection(canvasSection);

            Transform layersSection = CreateSection(rightPanel, "Layers", 200f, 0f);
            CreateLayersSection(layersSection);

            Transform previewSection = CreateSection(rightPanel, "Preview", 200f, 0f);
            CreatePreviewSection(previewSection);

            Transform actionSection = CreateSection(rightPanel, "Actions", 0f, 1f);
            CreateActionSection(actionSection);

            GameObject scaleRow = CreateUiObject("ScaleRow", actionSection);
            HorizontalLayoutGroup scaleLayout = scaleRow.AddComponent<HorizontalLayoutGroup>();
            scaleLayout.spacing = 8f;
            scaleLayout.childControlWidth = true;
            scaleLayout.childControlHeight = true;
            scaleLayout.childForceExpandWidth = true;
            scaleLayout.childForceExpandHeight = false;
            LayoutElement scaleRowLayout = scaleRow.AddComponent<LayoutElement>();
            scaleRowLayout.preferredHeight = 28f;

            uiScaleText = CreateText("UiScaleLabel", scaleRow.transform, "Scale 100%", 14, FontStyle.Bold, TextAnchor.MiddleLeft, MutedTextColor);
            LayoutElement uiScaleLabelLayout = uiScaleText.gameObject.AddComponent<LayoutElement>();
            uiScaleLabelLayout.preferredWidth = 90f;

            uiScaleSlider = CreateSlider("UiScaleSlider", scaleRow.transform, false);
            uiScaleSlider.minValue = UiScaleMin;
            uiScaleSlider.maxValue = UiScaleMax;
            uiScaleSlider.wholeNumbers = false;
            uiScaleSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetUiScale));
            SyncUiScaleSlider();

            BuildPresetModal(uiRoot.transform);
            BuildResizeModal(uiRoot.transform);
            BuildLayerRenameModal(uiRoot.transform);
            BuildKeybindModal(uiRoot.transform);

            infoText = CreateText("HiddenStatusText", uiRoot.transform, string.Empty, 1, FontStyle.Normal, TextAnchor.UpperLeft, TextColor);
            infoText.gameObject.SetActive(false);
            UpdateWindowSize();
        }

        private Transform CreateMainColumn(string name, Transform parent, float preferredWidth, float flexibleWidth)
        {
            GameObject columnObject = CreateUiObject(name, parent);
            Image columnImage = columnObject.AddComponent<Image>();
            columnImage.color = PanelColor;
            VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            LayoutElement element = columnObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                element.preferredWidth = preferredWidth;
                element.minWidth = preferredWidth;
            }
            element.flexibleWidth = flexibleWidth;

            return columnObject.transform;
        }

        private Transform CreateSection(Transform parent, string title, float preferredHeight, float flexibleHeight)
        {
            GameObject sectionObject = CreateUiObject(title.Replace(" ", string.Empty) + "Section", parent);
            Image sectionImage = sectionObject.AddComponent<Image>();
            sectionImage.color = PanelInnerColor;
            LayoutElement sectionLayout = sectionObject.AddComponent<LayoutElement>();
            if (preferredHeight > 0f)
            {
                sectionLayout.preferredHeight = preferredHeight;
                sectionLayout.minHeight = preferredHeight;
            }
            sectionLayout.flexibleHeight = flexibleHeight;

            VerticalLayoutGroup layout = sectionObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 6);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI label = CreateText("Label", sectionObject.transform, title, 16, FontStyle.Bold, TextAnchor.MiddleLeft, AccentColor);
            label.characterSpacing = 0f;
            label.enableWordWrapping = false;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 20f;
            labelLayout.minHeight = 20f;

            GameObject contentObject = CreateUiObject("Content", sectionObject.transform);
            LayoutElement contentLayout = contentObject.AddComponent<LayoutElement>();
            contentLayout.flexibleHeight = 1f;
            contentLayout.flexibleWidth = 1f;
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.localScale = Vector3.one;

            return contentObject.transform;
        }

        private void CreateToolSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject buttonGrid = CreateUiObject("ToolButtons", parent);
            GridLayoutGroup grid = buttonGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(82f, 30f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            LayoutElement gridLayout = buttonGrid.AddComponent<LayoutElement>();
            gridLayout.preferredHeight = 56f;

            CreateToolButton(buttonGrid.transform, ToolMode.Brush, "Brush");
            CreateToolButton(buttonGrid.transform, ToolMode.Eraser, "Eraser");
            CreateToolButton(buttonGrid.transform, ToolMode.Fill, "Fill");
            CreateToolButton(buttonGrid.transform, ToolMode.Eyedropper, "Picker");
            CreateToolButton(buttonGrid.transform, ToolMode.Select, "Marquee");
            CreateToolButton(buttonGrid.transform, ToolMode.Move, "Move");

            GameObject brushRow = CreateUiObject("BrushSizeRow", parent);
            HorizontalLayoutGroup brushRowLayout = brushRow.AddComponent<HorizontalLayoutGroup>();
            brushRowLayout.spacing = 6f;
            brushRowLayout.childControlWidth = true;
            brushRowLayout.childControlHeight = true;
            brushRowLayout.childForceExpandWidth = false;
            brushRowLayout.childForceExpandHeight = false;
            brushRowLayout.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement brushRowElement = brushRow.AddComponent<LayoutElement>();
            brushRowElement.preferredHeight = 24f;

            brushSizeText = CreateText("BrushSize", brushRow.transform, "Size", 14, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement brushLabelLayout = brushSizeText.gameObject.AddComponent<LayoutElement>();
            brushLabelLayout.preferredWidth = 36f;
            brushLabelLayout.preferredHeight = 24f;

            GameObject brushValueObject = CreateUiObject("BrushValue", brushRow.transform);
            LayoutElement brushValueLayout = brushValueObject.AddComponent<LayoutElement>();
            brushValueLayout.preferredWidth = 28f;
            brushValueLayout.preferredHeight = 24f;
            Image brushValueBack = brushValueObject.AddComponent<Image>();
            ApplyRoundedImage(brushValueBack, ButtonColor, ButtonBorderColor, CornerRadius, 1);
            TextMeshProUGUI brushValue = CreateText("BrushValueLabel", brushValueObject.transform, "1", 14, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            Stretch(brushValue.rectTransform);
            brushSizeText = brushValue;

            brushSlider = CreateSlider("BrushSlider", brushRow.transform, true);
            brushSlider.minValue = 1f;
            brushSlider.maxValue = 8f;
            brushSlider.wholeNumbers = true;
            brushSlider.value = 1f;
            brushSlider.onValueChanged.AddListener(delegate(float value)
            {
                brushSize = Mathf.RoundToInt(value);
                RefreshStatusText();
            });
        }

        private void CreateColorSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject previewRow = CreateUiObject("SelectedColorRow", parent);
            HorizontalLayoutGroup previewLayout = previewRow.AddComponent<HorizontalLayoutGroup>();
            previewLayout.spacing = 8f;
            previewLayout.childControlWidth = true;
            previewLayout.childControlHeight = true;
            previewLayout.childForceExpandWidth = false;
            previewLayout.childForceExpandHeight = false;
            LayoutElement previewRowLayout = previewRow.AddComponent<LayoutElement>();
            previewRowLayout.preferredHeight = 42f;

            GameObject swatchObject = CreateUiObject("SelectedColorButton", previewRow.transform);
            Image swatchButtonImage = swatchObject.AddComponent<Image>();
            swatchButtonImage.color = ButtonColor;
            selectedColorButton = swatchObject.AddComponent<Button>();
            selectedColorButton.targetGraphic = swatchButtonImage;
            selectedColorButton.onClick.AddListener(OpenNativeColorPicker);
            pickColorButton = selectedColorButton;
            LayoutElement swatchLayout = swatchObject.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 42f;
            swatchLayout.preferredHeight = 38f;
            ApplyRoundedImage(swatchButtonImage, CanvasFrameColor, AccentColor, CornerRadius, 1);

            GameObject swatchFillObject = CreateUiObject("SelectedColorFill", swatchObject.transform);
            selectedColorImage = swatchFillObject.AddComponent<Image>();
            selectedColorImage.color = selectedColor;
            RectTransform swatchFillRect = swatchFillObject.GetComponent<RectTransform>();
            StretchWithPadding(swatchFillRect, 3f);
            ApplyRoundedImage(selectedColorImage, Color.white, Color.white, 0, 0);

            GameObject detailColumn = CreateUiObject("ColorDetailColumn", previewRow.transform);
            VerticalLayoutGroup detailLayoutGroup = detailColumn.AddComponent<VerticalLayoutGroup>();
            detailLayoutGroup.spacing = 4f;
            detailLayoutGroup.childControlWidth = true;
            detailLayoutGroup.childControlHeight = true;
            detailLayoutGroup.childForceExpandWidth = true;
            detailLayoutGroup.childForceExpandHeight = false;
            LayoutElement detailColumnLayout = detailColumn.AddComponent<LayoutElement>();
            detailColumnLayout.flexibleWidth = 1f;
            detailColumnLayout.preferredHeight = 40f;

            colorDetailsText = CreateText("ColorDetails", detailColumn.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft, TextColor);
            LayoutElement detailsLayout = colorDetailsText.gameObject.AddComponent<LayoutElement>();
            detailsLayout.preferredHeight = 18f;

            opacitySlider = CreateSlider("OpacitySlider", detailColumn.transform, false);
            opacitySlider.minValue = 0f;
            opacitySlider.maxValue = 1f;
            opacitySlider.wholeNumbers = false;
            opacitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(delegate(float value)
            {
                if (!suppressColorCallbacks)
                {
                    SetSelectedColor(new Color(selectedColor.r, selectedColor.g, selectedColor.b, value), false);
                }
            }));

            GameObject historyRow = CreateUiObject("ColorHistoryRow", parent);
            HorizontalLayoutGroup historyLayout = historyRow.AddComponent<HorizontalLayoutGroup>();
            historyLayout.spacing = 4f;
            historyLayout.childControlWidth = true;
            historyLayout.childControlHeight = true;
            historyLayout.childForceExpandWidth = false;
            historyLayout.childForceExpandHeight = false;
            historyLayout.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement historyRowLayout = historyRow.AddComponent<LayoutElement>();
            historyRowLayout.preferredHeight = 22f;

            TextMeshProUGUI historyLabel = CreateText("ColorHistoryLabel", historyRow.transform, "Recent", 13, FontStyle.Bold, TextAnchor.MiddleLeft, MutedTextColor);
            LayoutElement historyLabelLayout = historyLabel.gameObject.AddComponent<LayoutElement>();
            historyLabelLayout.preferredWidth = 44f;
            historyLabelLayout.preferredHeight = 22f;

            for (int i = 0; i < 6; i++)
            {
                int historyIndex = i;
                Button historyButton = CreateButton("ColorHistory" + i, historyRow.transform, string.Empty, new UnityEngine.Events.UnityAction(delegate
                {
                    if (historyIndex >= 0 && historyIndex < colorHistory.Count)
                    {
                        SetSelectedColor(colorHistory[historyIndex], true);
                    }
                }));
                LayoutElement historyButtonLayout = historyButton.gameObject.AddComponent<LayoutElement>();
                historyButtonLayout.preferredWidth = 22f;
                historyButtonLayout.preferredHeight = 22f;
                Image historyButtonImage = historyButton.GetComponent<Image>();
                ApplyRoundedImage(historyButtonImage, CanvasFrameColor, PanelBorderColor, CornerRadius, 1);

                GameObject historyFill = CreateUiObject("HistoryFill", historyButton.transform);
                Image historyFillImage = historyFill.AddComponent<Image>();
                ApplyRoundedImage(historyFillImage, TransparentColor, TransparentColor, 0, 0);
                StretchWithPadding(historyFill.GetComponent<RectTransform>(), 2f);

                TextMeshProUGUI label = historyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = string.Empty;
                }

                colorHistoryViews.Add(new SwatchView { Button = historyButton, Fill = historyFillImage, Color = TransparentColor });
            }

            GameObject swatchGrid = CreateUiObject("SwatchGrid", parent);
            GridLayoutGroup grid = swatchGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(28f, 18f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            LayoutElement gridLayout = swatchGrid.AddComponent<LayoutElement>();
            gridLayout.preferredHeight = 40f;

            int visiblePresetCount = Mathf.Min(12, PresetColors.Length);
            for (int i = 0; i < visiblePresetCount; i++)
            {
                Color swatchColor = PresetColors[i];
                Button swatchButton = CreateButton("Swatch" + i, swatchGrid.transform, string.Empty, null);
                Image swatchImage = swatchButton.GetComponent<Image>();
                ApplyRoundedImage(swatchImage, CanvasFrameColor, PanelBorderColor, CornerRadius, 1);
                GameObject swatchFill = CreateUiObject("SwatchFill", swatchButton.transform);
                Image swatchFillImage = swatchFill.AddComponent<Image>();
                ApplyRoundedImage(swatchFillImage, swatchColor, swatchColor, 0, 0);
                StretchWithPadding(swatchFill.GetComponent<RectTransform>(), 2f);
                TextMeshProUGUI label = swatchButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = string.Empty;
                }
                swatchButton.onClick.AddListener(delegate
                {
                    SetSelectedColor(new Color(swatchColor.r, swatchColor.g, swatchColor.b, selectedColor.a), true);
                });
                swatchViews.Add(new SwatchView { Button = swatchButton, Fill = swatchFillImage, Color = swatchColor });
            }
        }

        private void SetSelectedColor(Color color, bool remember)
        {
            selectedColor = color;
            if (remember)
            {
                AddColorHistory(color);
            }

            RefreshColorUi();
        }

        private void AddColorHistory(Color color)
        {
            Color32 next = color;
            for (int i = colorHistory.Count - 1; i >= 0; i--)
            {
                Color32 existing = colorHistory[i];
                if (existing.r == next.r && existing.g == next.g && existing.b == next.b && existing.a == next.a)
                {
                    colorHistory.RemoveAt(i);
                }
            }

            colorHistory.Insert(0, color);
            while (colorHistory.Count > colorHistoryViews.Count && colorHistory.Count > 0)
            {
                colorHistory.RemoveAt(colorHistory.Count - 1);
            }
        }

        private void RefreshColorHistoryUi()
        {
            for (int i = 0; i < colorHistoryViews.Count; i++)
            {
                SwatchView view = colorHistoryViews[i];
                bool available = i < colorHistory.Count;
                if (view.Fill != null)
                {
                    Color fillColor = available ? colorHistory[i] : (Color)new Color32(20, 20, 20, 255);
                    ApplyRoundedImage(view.Fill, fillColor, fillColor, 4, 0);
                }

                if (view.Button != null)
                {
                    view.Button.interactable = available;
                    Image image = view.Button.GetComponent<Image>();
                    if (image != null)
                    {
                        ApplyRoundedImage(image, CanvasFrameColor, available ? AccentColor : PanelBorderColor, CornerRadius, 1);
                    }
                }
            }
        }

        private void CreateActionSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            GameObject mainGrid = CreateUiObject("ActionMainGrid", parent);
            GridLayoutGroup mainGridLayout = mainGrid.AddComponent<GridLayoutGroup>();
            mainGridLayout.cellSize = new Vector2(128f, 30f);
            mainGridLayout.spacing = new Vector2(4f, 4f);
            mainGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            mainGridLayout.constraintCount = 2;
            LayoutElement mainGridElement = mainGrid.AddComponent<LayoutElement>();
            mainGridElement.preferredHeight = 132f;

            undoButton = CreateActionButton("UndoButton", mainGrid.transform, "Undo", UiIconKind.Undo, new UnityEngine.Events.UnityAction(delegate
            {
                LimbSpriteState state = GetSelectedState();
                if (state != null)
                {
                    state.Undo();
                    RefreshAll();
                }
            }));

            redoButton = CreateActionButton("RedoButton", mainGrid.transform, "Redo", UiIconKind.Redo, new UnityEngine.Events.UnityAction(delegate
            {
                LimbSpriteState state = GetSelectedState();
                if (state != null)
                {
                    state.Redo();
                    RefreshAll();
                }
            }));

            resetLimbButton = CreateActionButton("ResetLimbButton", mainGrid.transform, "Reset Limb", UiIconKind.Reset, new UnityEngine.Events.UnityAction(delegate
            {
                LimbSpriteState state = GetSelectedState();
                if (state != null)
                {
                    state.ResetToOriginal();
                    RefreshAll();
                }
            }));

            resetCharacterButton = CreateActionButton("ResetCharacterButton", mainGrid.transform, "Reset All", UiIconKind.Person, new UnityEngine.Events.UnityAction(delegate
            {
                if (currentTarget != null)
                {
                    currentTarget.ResetAllStates();
                }
                else if (currentObjectTarget != null)
                {
                    currentObjectTarget.ResetState();
                }

                if (currentTarget != null || currentObjectTarget != null)
                {
                    RefreshAll();
                }
            }));

            copyPartButton = CreateActionButton("CopyPartButton", mainGrid.transform, "Copy Limb", UiIconKind.Copy, new UnityEngine.Events.UnityAction(CopySelectedSprite));
            pastePartButton = CreateActionButton("PastePartButton", mainGrid.transform, "Paste Limb", UiIconKind.Paste, new UnityEngine.Events.UnityAction(PasteSelectedSprite));

            copyTargetButton = CreateActionButton("CopyTargetButton", mainGrid.transform, "Copy Body", UiIconKind.Copy, new UnityEngine.Events.UnityAction(CopyWholeTarget));
            pasteTargetButton = CreateActionButton("PasteTargetButton", mainGrid.transform, "Paste Body", UiIconKind.Paste, new UnityEngine.Events.UnityAction(PasteWholeTarget));
            targetClipboardRowObject = mainGrid;

            keybindsButton = CreateButton("KeybindsButton", parent, "Keybinds", new UnityEngine.Events.UnityAction(OpenKeybindModal));
            LayoutElement keybindsLayout = keybindsButton.gameObject.AddComponent<LayoutElement>();
            keybindsLayout.preferredHeight = 30f;
            SetButtonFontSize(keybindsButton, 14f);
            StyleButton(keybindsButton, false, false);
        }

        private Button CreateActionButton(string name, Transform parent, string label, UiIconKind icon, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, label, action);
            SetButtonFontSize(button, 14f);
            StyleButton(button, false, false);
            return button;
        }

        private void CreateLayersSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject layerList = CreateUiObject("LayerList", parent);
            LayoutElement layerListLayout = layerList.AddComponent<LayoutElement>();
            layerListLayout.preferredHeight = 109f;
            layerListLayout.flexibleWidth = 1f;
            ScrollRect layerScroll = layerList.AddComponent<ScrollRect>();
            layerScroll.horizontal = false;
            layerScroll.vertical = true;
            layerScroll.movementType = ScrollRect.MovementType.Clamped;
            layerScroll.scrollSensitivity = 24f;

            GameObject layerViewport = CreateUiObject("LayerViewport", layerList.transform);
            RectTransform layerViewportRect = layerViewport.GetComponent<RectTransform>();
            Stretch(layerViewportRect);
            Image layerViewportImage = layerViewport.AddComponent<Image>();
            layerViewportImage.color = new Color(1f, 1f, 1f, 0.002f);
            Mask layerViewportMask = layerViewport.AddComponent<Mask>();
            layerViewportMask.showMaskGraphic = false;
            layerScroll.viewport = layerViewportRect;

            GameObject layerContent = CreateUiObject("LayerContent", layerViewport.transform);
            RectTransform layerContentRect = layerContent.GetComponent<RectTransform>();
            layerContentRect.anchorMin = new Vector2(0f, 1f);
            layerContentRect.anchorMax = new Vector2(1f, 1f);
            layerContentRect.pivot = new Vector2(0.5f, 1f);
            layerContentRect.anchoredPosition = Vector2.zero;
            layerContentRect.sizeDelta = Vector2.zero;
            layerScroll.content = layerContentRect;

            VerticalLayoutGroup layerContentLayout = layerContent.AddComponent<VerticalLayoutGroup>();
            layerContentLayout.spacing = 5f;
            layerContentLayout.childControlWidth = true;
            layerContentLayout.childControlHeight = true;
            layerContentLayout.childForceExpandWidth = true;
            layerContentLayout.childForceExpandHeight = false;
            ContentSizeFitter layerContentFitter = layerContent.AddComponent<ContentSizeFitter>();
            layerContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            layerContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            layerButtonContainer = layerContent.transform;

            GameObject buttonRow = CreateUiObject("LayerActionButtons", parent);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            LayoutElement buttonRowLayout = buttonRow.AddComponent<LayoutElement>();
            buttonRowLayout.preferredHeight = 28f;

            CreateLayerActionButton(buttonRow.transform, "AddLayerButton", UiIconKind.Add, new UnityEngine.Events.UnityAction(AddEditorLayer));
            CreateLayerActionButton(buttonRow.transform, "DuplicateLayerButton", UiIconKind.Duplicate, new UnityEngine.Events.UnityAction(DuplicateEditorLayer));
            CreateLayerActionButton(buttonRow.transform, "MergeLayerButton", UiIconKind.Merge, new UnityEngine.Events.UnityAction(MergeEditorLayerDown));
            CreateLayerActionButton(buttonRow.transform, "DeleteLayerButton", UiIconKind.Trash, new UnityEngine.Events.UnityAction(DeleteEditorLayer));

            RefreshLayerButtons();
        }

        private Button CreateLayerRow(Transform parent, int layerIndex, string label, bool selected)
        {
            Button rowButton = CreateButton("Layer" + label + "Row", parent, string.Empty, new UnityEngine.Events.UnityAction(delegate
            {
                SetActiveEditorLayer(layerIndex);
            }));
            LayoutElement rowLayout = rowButton.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 33f;
            StyleButton(rowButton, selected, false);
            LayerRowDragSurface dragSurface = rowButton.gameObject.AddComponent<LayerRowDragSurface>();
            dragSurface.Runtime = this;
            dragSurface.LayerIndex = layerIndex;

            TextMeshProUGUI baseLabel = rowButton.GetComponentInChildren<TextMeshProUGUI>();
            if (baseLabel != null)
            {
                baseLabel.text = string.Empty;
            }

            GameObject eyeButton = CreateUiObject("Eye", rowButton.transform);
            Image eyeBack = eyeButton.AddComponent<Image>();
            ApplyRoundedImage(eyeBack, selected ? AccentSoftColor : ButtonColor, ButtonBorderColor, CornerRadius, 1);
            SetRect(eyeButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -13f), new Vector2(38f, 13f));
            CreateAnchoredIcon("Icon", eyeButton.transform, UiIconKind.Eye, TextColor, 18f, Vector2.zero);
            Button eyeToggle = eyeButton.AddComponent<Button>();
            ColorBlock eyeColors = eyeToggle.colors;
            eyeColors.normalColor = Color.white;
            eyeColors.highlightedColor = new Color32(225, 225, 225, 255);
            eyeColors.pressedColor = new Color32(190, 190, 190, 255);
            eyeColors.disabledColor = Color.white;
            eyeColors.colorMultiplier = 1f;
            eyeToggle.colors = eyeColors;
            eyeToggle.onClick.AddListener(new UnityEngine.Events.UnityAction(delegate
            {
                ToggleEditorLayerVisibility(layerIndex);
            }));

            GameObject thumbnail = CreateUiObject("Thumbnail", rowButton.transform);
            Image thumbnailImage = thumbnail.AddComponent<Image>();
            ApplyRoundedImage(thumbnailImage, CanvasFrameColor, ButtonBorderColor, CornerRadius, 1);
            SetRect(thumbnail.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, -13f), new Vector2(74f, 13f));

            GameObject thumbnailFill = CreateUiObject("ThumbnailFill", thumbnail.transform);
            RawImage thumbnailFillImage = thumbnailFill.AddComponent<RawImage>();
            thumbnailFillImage.texture = GetCheckerTexture(new Color32(22, 22, 22, 255), new Color32(28, 28, 28, 255), 8);
            thumbnailFillImage.uvRect = new Rect(0f, 0f, 2f, 2f);
            thumbnailFillImage.raycastTarget = false;
            StretchWithPadding(thumbnailFill.GetComponent<RectTransform>(), 4f);

            TextMeshProUGUI rowLabel = CreateText("LayerLabel", rowButton.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            SetRect(rowLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(88f, 2f), new Vector2(-34f, -2f));

            CreateAnchoredIcon("MenuIcon", rowButton.transform, UiIconKind.Menu, TextColor, 16f, new Vector2(0f, 0f));
            Transform menu = rowButton.transform.Find("MenuIcon");
            if (menu != null)
            {
                RectTransform menuRect = menu.GetComponent<RectTransform>();
                SetRect(menuRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -8f), new Vector2(-14f, 8f));
            }

            return rowButton;
        }

        private void CreateLayerActionButton(Transform parent, string name, UiIconKind icon, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, string.Empty, action);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 28f;
            AddCenteredButtonIcon(button, icon, 20f);
            StyleButton(button, false, false);
        }

        private void ResetLayerState()
        {
            activeEditorLayer = EditorLayerKind.Paint;
            activeEditorLayerIndex = 0;
        }

        private void SetActiveEditorLayer(int layerIndex)
        {
            LimbSpriteState state = GetSelectedState();
            if (state != null && !state.SetActiveLayer(layerIndex))
            {
                return;
            }

            activeEditorLayerIndex = layerIndex;
            activeEditorLayer = GetLayerKindFromIndex(layerIndex);
            RefreshLayerButtons();
            RefreshStatusText();
        }

        private void ToggleEditorLayerVisibility(int layerIndex)
        {
            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                state.ToggleLayerVisibility(layerIndex);
            }

            RefreshLayerButtons();
            RefreshTextures();
        }

        private string GetLayerLabel(int layerIndex)
        {
            LimbSpriteState state = GetSelectedState();
            return state != null && layerIndex >= 0 && layerIndex < state.LayerCount ? state.GetLayerName(layerIndex) : "Layer " + (layerIndex + 1);
        }

        public void HandleLayerRowRightClick(int layerIndex)
        {
            OpenLayerRenameModal(layerIndex);
        }

        private bool CanEditActiveLayer(bool notify)
        {
            LimbSpriteState state = GetSelectedState();
            int index = activeEditorLayerIndex;
            if (state == null || !state.SetActiveLayer(index))
            {
                if (notify)
                {
                    DialogBoxManager.Notification("Select an editable layer first.");
                }

                return false;
            }

            if (!state.IsLayerVisible(index))
            {
                if (notify)
                {
                    DialogBoxManager.Notification("Show this layer before editing pixels.");
                }

                return false;
            }

            return true;
        }

        private void AddEditorLayer()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.AddEmptyLayer())
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            ClearSelectionState();
            RefreshAll();
        }

        private void DuplicateEditorLayer()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.DuplicateActiveLayer())
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            ClearSelectionState();
            RefreshAll();
        }

        private void MergeEditorLayerDown()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.MergeActiveLayerDown())
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            ClearSelectionState();
            RefreshAll();
        }

        private void DeleteEditorLayer()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.DeleteActiveLayer())
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            ClearSelectionState();
            RefreshAll();
        }

        private void CopyActiveLayerToClipboard()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                return;
            }

            layerClipboard = state.CaptureActiveLayerSnapshot();
        }

        private void PasteLayerFromClipboard()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || layerClipboard == null || !state.PasteLayerSnapshot(layerClipboard))
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            ClearSelectionState();
            RefreshAll();
        }

        private void OpenLayerRenameModal(int layerIndex)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || layerRenameModalRoot == null || layerRenameInput == null || layerIndex < 0 || layerIndex >= state.LayerCount)
            {
                return;
            }

            layerRenameIndex = layerIndex;
            layerRenameInput.text = state.GetLayerName(layerIndex);
            layerRenameModalRoot.SetActive(true);
            layerRenameModalRoot.transform.SetAsLastSibling();
            layerRenameInput.ActivateInputField();
            layerRenameInput.Select();
            layerRenameInput.caretPosition = layerRenameInput.text != null ? layerRenameInput.text.Length : 0;
        }

        private void CloseLayerRenameModal()
        {
            if (layerRenameModalRoot != null)
            {
                layerRenameModalRoot.SetActive(false);
            }

            layerRenameIndex = -1;
        }

        private void ApplyLayerRenameFromModal()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || layerRenameInput == null || layerRenameIndex < 0 || layerRenameIndex >= state.LayerCount)
            {
                CloseLayerRenameModal();
                return;
            }

            string nextName = layerRenameInput.text;
            if (state.RenameLayer(layerRenameIndex, nextName))
            {
                activeEditorLayerIndex = state.ActiveLayerIndex;
                activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
                RefreshAll();
            }

            CloseLayerRenameModal();
        }

        private void OpenKeybindModal()
        {
            if (keybindModalRoot == null)
            {
                return;
            }

            keybindModalRoot.SetActive(true);
            keybindModalRoot.transform.SetAsLastSibling();
        }

        private void CloseKeybindModal()
        {
            if (keybindModalRoot != null)
            {
                keybindModalRoot.SetActive(false);
            }
        }

        public void HandleLayerRowDragEnd(int layerIndex, PointerEventData eventData)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || eventData == null)
            {
                return;
            }

            int sourceIndex = layerIndex;
            int destinationIndex = GetLayerDropIndex(sourceIndex, eventData);
            if (sourceIndex == destinationIndex || !state.MoveLayer(sourceIndex, destinationIndex))
            {
                return;
            }

            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(activeEditorLayerIndex);
            RefreshAll();
        }

        private int GetLayerDropIndex(int sourceIndex, PointerEventData eventData)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                return sourceIndex;
            }

            int destination;
            if (TryGetLayerDropIndexForPosition(eventData, out destination))
            {
                return Mathf.Clamp(destination, 0, state.LayerCount - 1);
            }

            float deltaY = eventData.position.y - eventData.pressPosition.y;
            if (Mathf.Abs(deltaY) < 8f)
            {
                return sourceIndex;
            }

            return Mathf.Clamp(sourceIndex + (deltaY > 0f ? -1 : 1), 0, state.LayerCount - 1);
        }

        private bool TryGetLayerDropIndexForPosition(PointerEventData eventData, out int destination)
        {
            destination = 0;
            if (eventData == null)
            {
                return false;
            }

            for (int i = 0; i < layerRowButtons.Count; i++)
            {
                Button button = layerRowButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                LayerRowDragSurface dragSurface = button.GetComponent<LayerRowDragSurface>();
                if (rect != null &&
                    dragSurface != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera))
                {
                    destination = dragSurface.LayerIndex;
                    return true;
                }
            }

            return false;
        }

        private EditorLayerKind GetLayerKindFromIndex(int index)
        {
            if (index <= 0)
            {
                return EditorLayerKind.Paint;
            }

            if (index == 1)
            {
                return EditorLayerKind.Base;
            }

            return EditorLayerKind.Damage;
        }

        private void CreateCanvasSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject statusRow = CreateUiObject("CanvasStatusRow", parent);
            HorizontalLayoutGroup statusRowLayout = statusRow.AddComponent<HorizontalLayoutGroup>();
            statusRowLayout.spacing = 6f;
            statusRowLayout.childControlWidth = true;
            statusRowLayout.childControlHeight = true;
            statusRowLayout.childForceExpandWidth = false;
            statusRowLayout.childForceExpandHeight = false;
            statusRowLayout.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement statusRowElement = statusRow.AddComponent<LayoutElement>();
            statusRowElement.preferredHeight = 30f;

            canvasStatusText = CreateText("CanvasStatus", statusRow.transform, string.Empty, 15, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement statusLayout = canvasStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.flexibleWidth = 1f;
            statusLayout.preferredHeight = 30f;

            shrinkCanvas1Button = CreateCanvasSizeQuickButton(statusRow.transform, "Shrink1", "-1", new UnityEngine.Events.UnityAction(delegate { ExpandCanvasBy(-1); }));
            expandCanvas1Button = CreateCanvasSizeQuickButton(statusRow.transform, "Expand1", "+1", new UnityEngine.Events.UnityAction(delegate { ExpandCanvasBy(1); }));
            expandCanvas2Button = CreateCanvasSizeQuickButton(statusRow.transform, "Expand2", "+2", new UnityEngine.Events.UnityAction(delegate { ExpandCanvasBy(2); }));
            expandCanvas4Button = CreateCanvasSizeQuickButton(statusRow.transform, "Expand4", "+4", new UnityEngine.Events.UnityAction(delegate { ExpandCanvasBy(4); }));

            resizeCanvasButton = CreateButton("ResizeCanvasButton", statusRow.transform, "Canvas Size", new UnityEngine.Events.UnityAction(OpenResizeModal));
            LayoutElement resizeLayout = resizeCanvasButton.gameObject.AddComponent<LayoutElement>();
            resizeLayout.preferredWidth = 96f;
            resizeLayout.preferredHeight = 26f;
            SetButtonFontSize(resizeCanvasButton, 14f);
            StyleButton(resizeCanvasButton, false, false);

            GameObject canvasFrame = CreateUiObject("CanvasFrame", parent);
            Image frameImage = canvasFrame.AddComponent<Image>();
            ApplyRoundedImage(frameImage, CanvasFrameColor, PanelHighlightBorderColor, CornerRadius, 1);
            LayoutElement frameLayout = canvasFrame.AddComponent<LayoutElement>();
            frameLayout.flexibleHeight = 1f;
            frameLayout.flexibleWidth = 1f;

            RectTransform frameRect = canvasFrame.GetComponent<RectTransform>();
            GameObject canvasHolder = CreateUiObject("CanvasHolder", canvasFrame.transform);
            RectTransform holderRect = canvasHolder.GetComponent<RectTransform>();
            canvasHolderRect = holderRect;
            StretchWithPadding(holderRect, 16f);

            Image holderImage = canvasHolder.AddComponent<Image>();
            ApplyRoundedImage(holderImage, CanvasBackColor, ButtonBorderColor, CornerRadius, 1);
            canvasHolderMask = canvasHolder.AddComponent<RectMask2D>();

            GameObject canvasBackObject = CreateUiObject("CanvasSolidBack", canvasHolder.transform);
            RectTransform canvasBackRect = canvasBackObject.GetComponent<RectTransform>();
            Stretch(canvasBackRect);
            RawImage canvasBackImage = canvasBackObject.AddComponent<RawImage>();
            canvasBackImage.texture = GetCheckerTexture(new Color32(24, 24, 24, 255), new Color32(30, 30, 30, 255), 18);
            canvasBackImage.uvRect = new Rect(0f, 0f, 24f, 24f);
            canvasBackImage.color = Color.white;
            canvasBackImage.raycastTarget = false;

            GameObject rawObject = CreateUiObject("CanvasImage", canvasHolder.transform);
            canvasImage = rawObject.AddComponent<RawImage>();
            canvasImage.color = Color.white;
            RectTransform canvasRect = rawObject.GetComponent<RectTransform>();
            Stretch(canvasRect);
            canvasAspect = rawObject.AddComponent<AspectRatioFitter>();
            canvasAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            canvasAspect.aspectRatio = 1f;
            canvasSurface = rawObject.AddComponent<SpriteEditorCanvasSurface>();
            canvasSurface.Runtime = this;

            GameObject boundsObject = CreateUiObject("CanvasBoundsIndicator", rawObject.transform);
            RectTransform boundsRect = boundsObject.GetComponent<RectTransform>();
            Stretch(boundsRect);
            Image boundsImage = boundsObject.AddComponent<Image>();
            boundsImage.raycastTarget = false;
            ApplyRoundedImage(boundsImage, new Color32(0, 0, 0, 0), new Color32(89, 140, 217, 210), 2, 2);

            GameObject selectionObject = CreateUiObject("SelectionIndicator", rawObject.transform);
            selectionOverlayRect = selectionObject.GetComponent<RectTransform>();
            selectionOverlayRawImage = selectionObject.AddComponent<RawImage>();
            selectionOverlayRawImage.raycastTarget = false;
            selectionOverlayRawImage.color = Color.white;
            Stretch(selectionOverlayRect);
            selectionObject.SetActive(false);

            GameObject resizePreviewObject = CreateUiObject("CanvasResizePreview", rawObject.transform);
            canvasResizePreviewRect = resizePreviewObject.GetComponent<RectTransform>();
            Stretch(canvasResizePreviewRect);
            canvasResizePreviewImage = resizePreviewObject.AddComponent<Image>();
            canvasResizePreviewImage.raycastTarget = false;
            canvasResizePreviewImage.color = new Color(0.35f, 0.55f, 0.85f, 0.18f);
            Outline previewOutline = resizePreviewObject.AddComponent<Outline>();
            previewOutline.effectColor = AccentColor;
            previewOutline.effectDistance = new Vector2(2f, -2f);
            resizePreviewObject.SetActive(false);

            canvasResizeHandlesRoot = CreateUiObject("CanvasResizeHandles", rawObject.transform);
            Stretch(canvasResizeHandlesRoot.GetComponent<RectTransform>());
            CreateCanvasResizeHandle(CanvasResizeHandleKind.Left, new Vector2(0f, 0.5f), new Vector2(14f, -28f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.Right, new Vector2(1f, 0.5f), new Vector2(14f, -28f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.Bottom, new Vector2(0.5f, 0f), new Vector2(-28f, 14f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.Top, new Vector2(0.5f, 1f), new Vector2(-28f, 14f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.BottomLeft, new Vector2(0f, 0f), new Vector2(16f, 16f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.BottomRight, new Vector2(1f, 0f), new Vector2(16f, 16f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.TopLeft, new Vector2(0f, 1f), new Vector2(16f, 16f));
            CreateCanvasResizeHandle(CanvasResizeHandleKind.TopRight, new Vector2(1f, 1f), new Vector2(16f, 16f));
            canvasResizeHandlesRoot.SetActive(false);

            canvasResizeModeText = CreateText("CanvasResizeModeHint", statusRow.transform, string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleRight, AccentColor);
            LayoutElement modeHintLayout = canvasResizeModeText.gameObject.AddComponent<LayoutElement>();
            modeHintLayout.preferredWidth = 150f;
            modeHintLayout.preferredHeight = 26f;
            canvasResizeModeText.gameObject.SetActive(false);

            ApplyCanvasZoom();
        }

        private void CreateCanvasResizeHandle(CanvasResizeHandleKind kind, Vector2 anchor, Vector2 size)
        {
            GameObject handleObject = CreateUiObject("ResizeHandle_" + kind, canvasResizeHandlesRoot.transform);
            RectTransform rect = handleObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = handleObject.AddComponent<Image>();
            image.color = AccentColor;
            image.raycastTarget = true;

            SpriteEditorCanvasResizeHandle handle = handleObject.AddComponent<SpriteEditorCanvasResizeHandle>();
            handle.Runtime = this;
            handle.Kind = kind;
        }

        private void CreatePreviewSection(Transform parent)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject previewFrame = CreateUiObject("PreviewFrame", parent);
            Image frameImage = previewFrame.AddComponent<Image>();
            ApplyRoundedImage(frameImage, new Color32(8, 8, 8, 255), PanelBorderColor, CornerRadius, 1);
            LayoutElement frameLayout = previewFrame.AddComponent<LayoutElement>();
            frameLayout.flexibleHeight = 1f;
            frameLayout.minHeight = 320f;

            GameObject previewHolder = CreateUiObject("PreviewHolder", previewFrame.transform);
            RectTransform holderRect = previewHolder.GetComponent<RectTransform>();
            StretchWithPadding(holderRect, 12f);
            RawImage holderImage = previewHolder.AddComponent<RawImage>();
            holderImage.texture = GetCheckerTexture(new Color32(18, 18, 18, 255), new Color32(24, 24, 24, 255), 18);
            holderImage.uvRect = new Rect(0f, 0f, 12f, 18f);
            holderImage.raycastTarget = false;

            GameObject rawObject = CreateUiObject("PreviewImage", previewHolder.transform);
            previewImage = rawObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            RectTransform rawRect = rawObject.GetComponent<RectTransform>();
            Stretch(rawRect);
            previewAspect = rawObject.AddComponent<AspectRatioFitter>();
            previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewAspect.aspectRatio = 1f;
        }

        private void BuildResizeModal(Transform parent)
        {
            resizeModalRoot = CreateUiObject("ResizeCanvasModal", parent);
            RectTransform overlayRect = resizeModalRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image overlay = resizeModalRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.72f);
            resizeModalRoot.SetActive(false);

            GameObject modalWindow = CreateUiObject("ResizeCanvasWindow", resizeModalRoot.transform);
            RectTransform modalRect = modalWindow.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(460f, 340f);
            Image modalImage = modalWindow.AddComponent<Image>();
            modalImage.color = WindowColor;
            Outline modalOutline = modalWindow.AddComponent<Outline>();
            modalOutline.effectColor = Color.white;
            modalOutline.effectDistance = new Vector2(1.2f, -1.2f);

            VerticalLayoutGroup modalLayout = modalWindow.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(16, 16, 14, 16);
            modalLayout.spacing = 10f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText("ResizeTitle", modalWindow.transform, "Canvas Size", 18, FontStyle.Bold, TextAnchor.MiddleLeft, AccentColor);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 26f;

            TextMeshProUGUI hint = CreateText("ResizeHint", modalWindow.transform, "Set size, then pick where the current art stays.", 12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            LayoutElement hintLayout = hint.gameObject.AddComponent<LayoutElement>();
            hintLayout.preferredHeight = 20f;

            GameObject fieldsRow = CreateUiObject("ResizeFields", modalWindow.transform);
            HorizontalLayoutGroup fieldsLayout = fieldsRow.AddComponent<HorizontalLayoutGroup>();
            fieldsLayout.spacing = 12f;
            fieldsLayout.childControlWidth = true;
            fieldsLayout.childControlHeight = true;
            fieldsLayout.childForceExpandWidth = true;
            fieldsLayout.childForceExpandHeight = false;
            LayoutElement fieldsRowLayout = fieldsRow.AddComponent<LayoutElement>();
            fieldsRowLayout.preferredHeight = 58f;

            resizeWidthInput = CreateResizeInputField(fieldsRow.transform, "Width");
            resizeHeightInput = CreateResizeInputField(fieldsRow.transform, "Height");

            TextMeshProUGUI anchorLabel = CreateText("AnchorLabel", modalWindow.transform, "Content Position", 13, FontStyle.Bold, TextAnchor.MiddleLeft, AccentColor);
            LayoutElement anchorLabelLayout = anchorLabel.gameObject.AddComponent<LayoutElement>();
            anchorLabelLayout.preferredHeight = 20f;

            GameObject anchorGrid = CreateUiObject("AnchorGrid", modalWindow.transform);
            GridLayoutGroup anchorLayout = anchorGrid.AddComponent<GridLayoutGroup>();
            anchorLayout.cellSize = new Vector2(54f, 28f);
            anchorLayout.spacing = new Vector2(4f, 4f);
            anchorLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            anchorLayout.constraintCount = 3;
            anchorLayout.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement anchorGridLayout = anchorGrid.AddComponent<LayoutElement>();
            anchorGridLayout.preferredHeight = 96f;

            for (int y = 2; y >= 0; y--)
            {
                for (int x = 0; x < 3; x++)
                {
                    int anchorX = x;
                    int anchorY = y;
                    string label = x == 1 && y == 1 ? "Center" : string.Empty;
                    Button button = CreateButton("Anchor_" + x + "_" + y, anchorGrid.transform, label, new UnityEngine.Events.UnityAction(delegate
                    {
                        SetResizeAnchor(anchorX, anchorY);
                    }));
            SetButtonFontSize(button, 13f);
                    resizeAnchorButtons[x, y] = button;
                }
            }

            GameObject quickRow = CreateUiObject("QuickExpandRow", modalWindow.transform);
            HorizontalLayoutGroup quickLayout = quickRow.AddComponent<HorizontalLayoutGroup>();
            quickLayout.spacing = 6f;
            quickLayout.childControlWidth = true;
            quickLayout.childControlHeight = true;
            quickLayout.childForceExpandWidth = true;
            quickLayout.childForceExpandHeight = false;
            LayoutElement quickRowLayout = quickRow.AddComponent<LayoutElement>();
            quickRowLayout.preferredHeight = 28f;

            CreateButton("QuickExpand1", quickRow.transform, "Grow +1", new UnityEngine.Events.UnityAction(delegate
            {
                CloseResizeModal();
                ExpandCanvasBy(1);
            }));
            CreateButton("QuickExpand2", quickRow.transform, "Grow +2", new UnityEngine.Events.UnityAction(delegate
            {
                CloseResizeModal();
                ExpandCanvasBy(2);
            }));
            CreateButton("QuickExpand4", quickRow.transform, "Grow +4", new UnityEngine.Events.UnityAction(delegate
            {
                CloseResizeModal();
                ExpandCanvasBy(4);
            }));

            GameObject buttonRow = CreateUiObject("ResizeButtons", modalWindow.transform);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            LayoutElement buttonRowLayout = buttonRow.AddComponent<LayoutElement>();
            buttonRowLayout.preferredHeight = 34f;

            Button cancelButton = CreateButton("ResizeCancel", buttonRow.transform, "Cancel", new UnityEngine.Events.UnityAction(CloseResizeModal));
            SetButtonFontSize(cancelButton, 14f);

            Button applyButton = CreateButton("ResizeApply", buttonRow.transform, "Apply", new UnityEngine.Events.UnityAction(ApplyResizeFromModal));
            SetButtonFontSize(applyButton, 14f);
            StyleButton(applyButton, true, false);
            RefreshResizeAnchorButtons();
        }

        private Button CreateCanvasSizeQuickButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, label, action);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 36f;
            layout.preferredHeight = 30f;
            SetButtonFontSize(button, 14f);
            StyleButton(button, false, false);
            return button;
        }

        private void SetResizeAnchor(int x, int y)
        {
            resizeAnchorX = Mathf.Clamp(x, 0, 2);
            resizeAnchorY = Mathf.Clamp(y, 0, 2);
            RefreshResizeAnchorButtons();
        }

        private void RefreshResizeAnchorButtons()
        {
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Button button = resizeAnchorButtons[x, y];
                    if (button == null)
                    {
                        continue;
                    }

                    StyleButton(button, x == resizeAnchorX && y == resizeAnchorY, false);
                }
            }
        }

        private void ExpandCanvasBy(int amount)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.IsValid)
            {
                DialogBoxManager.Notification("No editable sprite was selected.");
                return;
            }

            int pad = Mathf.Abs(amount) * 2;
            int newWidth = amount >= 0
                ? Mathf.Clamp(state.Width + pad, 1, 512)
                : Mathf.Clamp(state.Width - pad, 1, 512);
            int newHeight = amount >= 0
                ? Mathf.Clamp(state.Height + pad, 1, 512)
                : Mathf.Clamp(state.Height - pad, 1, 512);
            if (newWidth == state.Width && newHeight == state.Height)
            {
                return;
            }

            int offsetX = (newWidth - state.Width) / 2;
            int offsetY = (newHeight - state.Height) / 2;

            FinalizeActiveStroke();
            if (state.ResizeCanvas(newWidth, newHeight, offsetX, offsetY))
            {
                RefreshAll();
            }
        }

        private void BuildLayerRenameModal(Transform parent)
        {
            layerRenameModalRoot = CreateUiObject("LayerRenameModal", parent);
            RectTransform overlayRect = layerRenameModalRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image overlay = layerRenameModalRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.62f);
            layerRenameModalRoot.SetActive(false);

            GameObject modalWindow = CreateUiObject("LayerRenameWindow", layerRenameModalRoot.transform);
            RectTransform modalRect = modalWindow.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(420f, 190f);
            Image modalImage = modalWindow.AddComponent<Image>();
            ApplyRoundedImage(modalImage, WindowColor, PanelHighlightBorderColor, CornerRadius, 1);

            VerticalLayoutGroup modalLayout = modalWindow.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(16, 16, 14, 16);
            modalLayout.spacing = 10f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText("LayerRenameTitle", modalWindow.transform, "Rename Layer", 21, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 30f;

            layerRenameInput = CreateTextInputField(modalWindow.transform, "LayerNameInput", 48);

            GameObject buttonRow = CreateUiObject("LayerRenameButtons", modalWindow.transform);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            LayoutElement buttonRowLayout = buttonRow.AddComponent<LayoutElement>();
            buttonRowLayout.preferredHeight = 34f;

            Button cancelButton = CreateButton("LayerRenameCancel", buttonRow.transform, "Cancel", new UnityEngine.Events.UnityAction(CloseLayerRenameModal));
            SetButtonFontSize(cancelButton, 14f);

            Button applyButton = CreateButton("LayerRenameApply", buttonRow.transform, "Rename", new UnityEngine.Events.UnityAction(ApplyLayerRenameFromModal));
            SetButtonFontSize(applyButton, 14f);
            ApplyRoundedImage(applyButton.GetComponent<Image>(), ButtonSelectedColor, AccentColor, CornerRadius, 1);
        }

        private void BuildKeybindModal(Transform parent)
        {
            keybindModalRoot = CreateUiObject("KeybindModal", parent);
            RectTransform overlayRect = keybindModalRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image overlay = keybindModalRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.68f);
            keybindModalRoot.SetActive(false);

            GameObject modalWindow = CreateUiObject("KeybindWindow", keybindModalRoot.transform);
            RectTransform modalRect = modalWindow.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(640f, 570f);
            Image modalImage = modalWindow.AddComponent<Image>();
            ApplyRoundedImage(modalImage, WindowColor, PanelHighlightBorderColor, CornerRadius, 1);

            VerticalLayoutGroup modalLayout = modalWindow.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(16, 16, 14, 16);
            modalLayout.spacing = 10f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            GameObject headerRow = CreateUiObject("KeybindHeader", modalWindow.transform);
            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            LayoutElement headerElement = headerRow.AddComponent<LayoutElement>();
            headerElement.preferredHeight = 34f;

            TextMeshProUGUI title = CreateText("KeybindTitle", headerRow.transform, "Keybinds", 22, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.preferredHeight = 34f;

            Button close = CreateButton("KeybindClose", headerRow.transform, "Close", new UnityEngine.Events.UnityAction(CloseKeybindModal));
            LayoutElement closeLayout = close.gameObject.AddComponent<LayoutElement>();
            closeLayout.preferredWidth = 92f;
            closeLayout.preferredHeight = 32f;
            SetButtonFontSize(close, 14f);

            GameObject listPanel = CreateUiObject("KeybindListPanel", modalWindow.transform);
            Image listPanelImage = listPanel.AddComponent<Image>();
            ApplyRoundedImage(listPanelImage, PanelInnerColor, PanelBorderColor, CornerRadius, 1);
            LayoutElement listPanelLayout = listPanel.AddComponent<LayoutElement>();
            listPanelLayout.flexibleHeight = 1f;
            listPanelLayout.flexibleWidth = 1f;

            ScrollRect scroll = listPanel.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;

            GameObject viewport = CreateUiObject("KeybindViewport", listPanel.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            scroll.viewport = viewportRect;

            GameObject content = CreateUiObject("KeybindContent", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            scroll.content = contentRect;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(10, 10, 10, 10);
            contentLayout.spacing = 7f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddKeybindRow(content.transform, "C", "Toggle canvas resize mode (drag edges)");
            AddKeybindRow(content.transform, "B / E / G / I", "Brush, eraser, fill, picker");
            AddKeybindRow(content.transform, "S / M", "Select pixels, move selection");
            AddKeybindRow(content.transform, "[ / ]", "Brush size down or up");
            AddKeybindRow(content.transform, "1 - 8", "Set brush size");
            AddKeybindRow(content.transform, "Ctrl + Z / Ctrl + Y", "Undo and redo");
            AddKeybindRow(content.transform, "Ctrl + C", "Copy selected pixels");
            AddKeybindRow(content.transform, "Ctrl + X", "Cut selected pixels");
            AddKeybindRow(content.transform, "Ctrl + V", "Paste selected pixels as a new layer");
            AddKeybindRow(content.transform, "Ctrl + D", "Deselect pixels");
            AddKeybindRow(content.transform, "Delete / Backspace", "Clear selected pixels");
            AddKeybindRow(content.transform, "Ctrl + Shift + C", "Copy the selected layer");
            AddKeybindRow(content.transform, "Ctrl + Shift + V", "Paste the selected layer");
            AddKeybindRow(content.transform, "Shift + C", "Copy all layers from the target");
            AddKeybindRow(content.transform, "Shift + V", "Paste all target layers as overlays");
            AddKeybindRow(content.transform, "Shift + Click", "Fill an enclosed pixel selection");
            AddKeybindRow(content.transform, "Right Brush", "Paint only existing opaque pixels");
            AddKeybindRow(content.transform, "Mouse Wheel", "Zoom canvas");
            AddKeybindRow(content.transform, "Middle Mouse Drag", "Pan canvas");
            AddKeybindRow(content.transform, "Right Click Layer", "Rename layer");
            AddKeybindRow(content.transform, "Escape", "Close the active popup or editor");
        }

        private void AddKeybindRow(Transform parent, string key, string action)
        {
            GameObject row = CreateUiObject("KeybindRow", parent);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 32f;

            GameObject keyObject = CreateUiObject("Key", row.transform);
            LayoutElement keyLayout = keyObject.AddComponent<LayoutElement>();
            keyLayout.preferredWidth = 185f;
            keyLayout.preferredHeight = 30f;
            Image keyBack = keyObject.AddComponent<Image>();
            ApplyRoundedImage(keyBack, ButtonColor, ButtonBorderColor, CornerRadius, 1);
            TextMeshProUGUI keyText = CreateText("KeyLabel", keyObject.transform, key, 14, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            Stretch(keyText.rectTransform);

            TextMeshProUGUI actionText = CreateText("Action", row.transform, action, 14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            LayoutElement actionLayout = actionText.gameObject.AddComponent<LayoutElement>();
            actionLayout.flexibleWidth = 1f;
            actionLayout.preferredHeight = 30f;
        }

        private void BuildPresetModal(Transform parent)
        {
            presetModalRoot = CreateUiObject("PresetModal", parent);
            RectTransform overlayRect = presetModalRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image overlay = presetModalRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.78f);
            presetModalRoot.SetActive(false);

            GameObject modalWindow = CreateUiObject("PresetModalWindow", presetModalRoot.transform);
            RectTransform modalRect = modalWindow.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(960f, 680f);
            Image modalImage = modalWindow.AddComponent<Image>();
            modalImage.color = WindowColor;

            VerticalLayoutGroup modalLayout = modalWindow.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(14, 14, 14, 14);
            modalLayout.spacing = 10f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            GameObject headerRow = CreateUiObject("PresetModalHeader", modalWindow.transform);
            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 8f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            LayoutElement headerElement = headerRow.AddComponent<LayoutElement>();
            headerElement.preferredHeight = 34f;

            TextMeshProUGUI headerText = CreateText("PresetModalTitle", headerRow.transform, "Preset Library", 22, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement headerTextLayout = headerText.gameObject.AddComponent<LayoutElement>();
            headerTextLayout.flexibleWidth = 1f;
            headerTextLayout.preferredHeight = 34f;

            presetModalCloseButton = CreateButton("PresetModalClose", headerRow.transform, "Close", new UnityEngine.Events.UnityAction(ClosePresetModal));
            LayoutElement modalCloseLayout = presetModalCloseButton.gameObject.AddComponent<LayoutElement>();
            modalCloseLayout.preferredWidth = 90f;
            modalCloseLayout.preferredHeight = 32f;

            GameObject modeRow = CreateUiObject("PresetModeRow", modalWindow.transform);
            HorizontalLayoutGroup modeLayout = modeRow.AddComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 8f;
            modeLayout.childControlWidth = true;
            modeLayout.childControlHeight = true;
            modeLayout.childForceExpandWidth = false;
            modeLayout.childForceExpandHeight = false;
            LayoutElement modeRowElement = modeRow.AddComponent<LayoutElement>();
            modeRowElement.preferredHeight = 34f;

            presetLibraryTabButton = CreateButton("PresetLibraryTab", modeRow.transform, "Saved Presets", new UnityEngine.Events.UnityAction(delegate
            {
                SetPresetBrowserMode(SpritePresetBrowserMode.Library);
            }));
            LayoutElement libraryTabLayout = presetLibraryTabButton.gameObject.AddComponent<LayoutElement>();
            libraryTabLayout.preferredWidth = 150f;
            libraryTabLayout.preferredHeight = 32f;

            presetBundleImportsTabButton = CreateButton("PresetBundleImportsTab", modeRow.transform, "Preset Files", new UnityEngine.Events.UnityAction(delegate
            {
                SetPresetBrowserMode(SpritePresetBrowserMode.BundleFiles);
            }));
            LayoutElement bundleTabLayout = presetBundleImportsTabButton.gameObject.AddComponent<LayoutElement>();
            bundleTabLayout.preferredWidth = 140f;
            bundleTabLayout.preferredHeight = 32f;

            presetPngImportsTabButton = CreateButton("PresetPngImportsTab", modeRow.transform, "Image Files", new UnityEngine.Events.UnityAction(delegate
            {
                SetPresetBrowserMode(SpritePresetBrowserMode.PngFiles);
            }));
            LayoutElement pngTabLayout = presetPngImportsTabButton.gameObject.AddComponent<LayoutElement>();
            pngTabLayout.preferredWidth = 130f;
            pngTabLayout.preferredHeight = 32f;

            GameObject bodyRow = CreateUiObject("PresetModalBody", modalWindow.transform);
            HorizontalLayoutGroup bodyLayout = bodyRow.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 12f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;
            LayoutElement bodyElement = bodyRow.AddComponent<LayoutElement>();
            bodyElement.flexibleHeight = 1f;
            bodyElement.flexibleWidth = 1f;

            GameObject listPanel = CreateUiObject("PresetListPanel", bodyRow.transform);
            Image listPanelImage = listPanel.AddComponent<Image>();
            listPanelImage.color = PanelInnerColor;
            VerticalLayoutGroup listPanelLayout = listPanel.AddComponent<VerticalLayoutGroup>();
            listPanelLayout.padding = new RectOffset(8, 8, 8, 8);
            listPanelLayout.spacing = 8f;
            listPanelLayout.childControlWidth = true;
            listPanelLayout.childControlHeight = true;
            listPanelLayout.childForceExpandWidth = true;
            listPanelLayout.childForceExpandHeight = false;
            LayoutElement listPanelElement = listPanel.AddComponent<LayoutElement>();
            listPanelElement.preferredWidth = 320f;
            listPanelElement.minWidth = 320f;
            listPanelElement.flexibleHeight = 1f;

            TextMeshProUGUI listTitle = CreateText("PresetListTitle", listPanel.transform, "Items", 16, FontStyle.Bold, TextAnchor.MiddleLeft, AccentColor);
            LayoutElement listTitleLayout = listTitle.gameObject.AddComponent<LayoutElement>();
            listTitleLayout.preferredHeight = 26f;

            ScrollRect listScroll = listPanel.AddComponent<ScrollRect>();
            listScroll.horizontal = false;
            listScroll.vertical = true;
            listScroll.movementType = ScrollRect.MovementType.Clamped;
            listScroll.scrollSensitivity = 24f;

            GameObject listViewport = CreateUiObject("PresetListViewport", listPanel.transform);
            RectTransform listViewportRect = listViewport.GetComponent<RectTransform>();
            listViewportRect.anchorMin = new Vector2(0f, 0f);
            listViewportRect.anchorMax = new Vector2(1f, 1f);
            listViewportRect.offsetMin = new Vector2(0f, 0f);
            listViewportRect.offsetMax = new Vector2(0f, -34f);
            Image listViewportImage = listViewport.AddComponent<Image>();
            listViewportImage.color = new Color(1f, 1f, 1f, 0.002f);
            Mask listViewportMask = listViewport.AddComponent<Mask>();
            listViewportMask.showMaskGraphic = false;
            listScroll.viewport = listViewportRect;

            GameObject listContent = CreateUiObject("PresetListContent", listViewport.transform);
            RectTransform listContentRect = listContent.GetComponent<RectTransform>();
            listContentRect.anchorMin = new Vector2(0f, 1f);
            listContentRect.anchorMax = new Vector2(1f, 1f);
            listContentRect.pivot = new Vector2(0.5f, 1f);
            listContentRect.anchoredPosition = Vector2.zero;
            listContentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listContentLayout = listContent.AddComponent<VerticalLayoutGroup>();
            listContentLayout.spacing = 6f;
            listContentLayout.childControlWidth = true;
            listContentLayout.childControlHeight = true;
            listContentLayout.childForceExpandWidth = true;
            listContentLayout.childForceExpandHeight = false;
            ContentSizeFitter listContentFitter = listContent.AddComponent<ContentSizeFitter>();
            listContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            listContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listScroll.content = listContentRect;
            presetBrowserListContainer = listContent.transform;

            GameObject detailPanel = CreateUiObject("PresetDetailPanel", bodyRow.transform);
            Image detailPanelImage = detailPanel.AddComponent<Image>();
            detailPanelImage.color = PanelInnerColor;
            VerticalLayoutGroup detailLayout = detailPanel.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(10, 10, 10, 10);
            detailLayout.spacing = 10f;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            LayoutElement detailPanelElement = detailPanel.AddComponent<LayoutElement>();
            detailPanelElement.flexibleWidth = 1f;
            detailPanelElement.flexibleHeight = 1f;

            GameObject detailPreviewFrame = CreateUiObject("PresetDetailPreviewFrame", detailPanel.transform);
            Image detailPreviewFrameImage = detailPreviewFrame.AddComponent<Image>();
            detailPreviewFrameImage.color = new Color32(10, 10, 10, 255);
            LayoutElement detailPreviewFrameLayout = detailPreviewFrame.AddComponent<LayoutElement>();
            detailPreviewFrameLayout.preferredHeight = 220f;

            GameObject detailPreviewHolder = CreateUiObject("PresetDetailPreviewHolder", detailPreviewFrame.transform);
            RectTransform detailPreviewHolderRect = detailPreviewHolder.GetComponent<RectTransform>();
            StretchWithPadding(detailPreviewHolderRect, 12f);
            Image detailPreviewHolderImage = detailPreviewHolder.AddComponent<Image>();
            detailPreviewHolderImage.color = new Color32(28, 28, 28, 255);

            GameObject detailPreviewImageObject = CreateUiObject("PresetDetailPreviewImage", detailPreviewHolder.transform);
            presetDetailPreviewImage = detailPreviewImageObject.AddComponent<RawImage>();
            presetDetailPreviewImage.color = Color.white;
            RectTransform detailPreviewImageRect = detailPreviewImageObject.GetComponent<RectTransform>();
            Stretch(detailPreviewImageRect);
            AspectRatioFitter detailPreviewAspect = detailPreviewImageObject.AddComponent<AspectRatioFitter>();
            detailPreviewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            detailPreviewAspect.aspectRatio = 1f;

            presetDetailText = CreateText("PresetDetailText", detailPanel.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft, TextColor);
            LayoutElement detailTextLayout = presetDetailText.gameObject.AddComponent<LayoutElement>();
            detailTextLayout.flexibleHeight = 1f;

            GameObject actionGrid = CreateUiObject("PresetActionGrid", detailPanel.transform);
            GridLayoutGroup actionGridLayout = actionGrid.AddComponent<GridLayoutGroup>();
            actionGridLayout.cellSize = new Vector2(190f, 34f);
            actionGridLayout.spacing = new Vector2(8f, 8f);
            actionGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            actionGridLayout.constraintCount = 2;
            LayoutElement actionGridElement = actionGrid.AddComponent<LayoutElement>();
            actionGridElement.preferredHeight = 174f;

            saveTargetPresetButton = CreateButton("SaveTargetPresetButton", actionGrid.transform, "Save Full Preset", new UnityEngine.Events.UnityAction(SaveTargetPresetFromModal));
            saveSelectedPartPresetButton = CreateButton("SaveSelectedPartPresetButton", actionGrid.transform, "Save Part Preset", new UnityEngine.Events.UnityAction(SaveSelectedPartPatchFromModal));
            applyPresetButton = CreateButton("ApplyPresetButton", actionGrid.transform, "Apply to Target", new UnityEngine.Events.UnityAction(ApplySelectedPresetFromModal));
            deletePresetButton = CreateButton("DeletePresetButton", actionGrid.transform, "Delete Saved Preset", new UnityEngine.Events.UnityAction(DeleteSelectedPresetFromModal));
            importBundleButton = CreateButton("ImportBundleButton", actionGrid.transform, "Add to Saved Presets", new UnityEngine.Events.UnityAction(ImportSelectedBundleFromModal));
            exportBundleButton = CreateButton("ExportBundleButton", actionGrid.transform, "Write Preset Folder", new UnityEngine.Events.UnityAction(ExportSelectedBundleFromModal));
            importPngButton = CreateButton("ImportPngButton", actionGrid.transform, "Load Image to Part", new UnityEngine.Events.UnityAction(ImportSelectedPngFromModal));
            exportPngButton = CreateButton("ExportPngButton", actionGrid.transform, "Write Part PNG", new UnityEngine.Events.UnityAction(ExportSelectedPngFromModal));

            presetStatusText = CreateText("PresetStatusText", modalWindow.transform, string.Empty, 12, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            LayoutElement presetStatusLayout = presetStatusText.gameObject.AddComponent<LayoutElement>();
            presetStatusLayout.preferredHeight = 84f;
        }

        private void OpenPresetModal()
        {
            if (presetModalRoot == null)
            {
                return;
            }

            presetModalOpen = true;
            if (string.IsNullOrWhiteSpace(presetStatusMessage))
            {
                presetStatusMessage = "Saved presets live in:\n" + SpritePresetLibraryService.GetPresetsRoot();
            }

            presetModalRoot.SetActive(true);
            presetModalRoot.transform.SetAsLastSibling();
            RebuildPresetBrowser(selectedPresetItem != null ? selectedPresetItem.SourcePath : null);
            RefreshPresetModal();
        }

        private void OpenPngImportModal()
        {
            presetBrowserMode = SpritePresetBrowserMode.PngFiles;
            selectedPresetItem = null;
            presetStatusMessage = "Choose Import to open a PNG file picker. One PNG loads into the selected sprite; multiple PNGs load onto matching character limbs.";
            OpenPresetModal();
        }

        private void ClosePresetModal()
        {
            presetModalOpen = false;
            if (presetModalRoot != null)
            {
                presetModalRoot.SetActive(false);
            }
        }

        private void OpenResizeModal()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.IsValid)
            {
                DialogBoxManager.Notification("No editable sprite was selected.");
                return;
            }

            FinalizeActiveStroke();
            ClosePresetModal();

            if (resizeModalRoot == null)
            {
                return;
            }

            resizeModalRoot.SetActive(true);
            resizeModalRoot.transform.SetAsLastSibling();
            SetResizeAnchor(1, 1);
            if (resizeWidthInput != null)
            {
                resizeWidthInput.text = state.Width.ToString();
                resizeWidthInput.Select();
                resizeWidthInput.ActivateInputField();
            }

            if (resizeHeightInput != null)
            {
                resizeHeightInput.text = state.Height.ToString();
            }
        }

        private void CloseResizeModal()
        {
            if (resizeModalRoot != null)
            {
                resizeModalRoot.SetActive(false);
            }
        }

        private void ApplyResizeFromModal()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.IsValid)
            {
                DialogBoxManager.Notification("No editable sprite was selected.");
                return;
            }

            int width = ParseResizeValue(resizeWidthInput != null ? resizeWidthInput.text : string.Empty);
            int height = ParseResizeValue(resizeHeightInput != null ? resizeHeightInput.text : string.Empty);
            if (width <= 0 || height <= 0)
            {
                DialogBoxManager.Notification("Enter a canvas size from 1 to 512 pixels.");
                return;
            }

            if (currentTarget != null)
            {
                int visibleWidth;
                int visibleHeight;
                if (state.TryGetVisiblePixelSize(out visibleWidth, out visibleHeight))
                {
                    width = SnapDimensionToVisibleParity(width, state.Width, visibleWidth);
                    height = SnapDimensionToVisibleParity(height, state.Height, visibleHeight);
                }
            }

            if (width == state.Width && height == state.Height)
            {
                CloseResizeModal();
                return;
            }

            FinalizeActiveStroke();
            int offsetX = GetResizeOffset(state.Width, width, resizeAnchorX);
            int offsetY = GetResizeOffset(state.Height, height, resizeAnchorY);
            if (state.ResizeCanvas(width, height, offsetX, offsetY))
            {
                CloseResizeModal();
                RefreshAll();
                DialogBoxManager.Notification("Canvas resized to " + width + " x " + height + " px.");
            }
            else
            {
                DialogBoxManager.Notification("Could not resize that canvas.");
            }
        }

        private static int GetResizeOffset(int oldSize, int newSize, int anchor)
        {
            int delta = newSize - oldSize;
            if (anchor <= 0)
            {
                return 0;
            }

            if (anchor >= 2)
            {
                return delta;
            }

            return delta / 2;
        }

        private static int ParseResizeValue(string value)
        {
            int parsed;
            if (!int.TryParse(value, out parsed))
            {
                return 0;
            }

            return Mathf.Clamp(parsed, 1, 512);
        }

        private static int SnapDimensionToVisibleParity(int requested, int current, int visible)
        {
            requested = Mathf.Clamp(requested, 1, 512);
            if (visible <= 0 || (requested & 1) == (visible & 1))
            {
                return requested;
            }

            if (requested > current && requested < 512)
            {
                return requested + 1;
            }

            if (requested < current && requested > 1)
            {
                return requested - 1;
            }

            return requested < 512 ? requested + 1 : requested - 1;
        }

        private void SetPresetBrowserMode(SpritePresetBrowserMode mode)
        {
            if (presetBrowserMode == mode)
            {
                RefreshPresetModal();
                return;
            }

            presetBrowserMode = mode;
            selectedPresetItem = null;
            RebuildPresetBrowser(null);
            RefreshPresetModal();
        }

        private void RebuildPresetBrowser(string preferredSelectionPath)
        {
            ClearPresetBrowser();

            if (presetBrowserListContainer == null)
            {
                return;
            }

            List<SpritePresetBrowserItem> items = SpritePresetLibraryService.EnumerateBrowserItems(presetBrowserMode);
            SpritePresetBrowserItem fallbackSelection = null;

            for (int i = 0; i < items.Count; i++)
            {
                SpritePresetBrowserItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (fallbackSelection == null)
                {
                    fallbackSelection = item;
                }

                if (item.PreviewTexture != null)
                {
                    presetBrowserTextures.Add(item.PreviewTexture);
                }

                Button button = CreatePresetBrowserButton(item, i);
                presetBrowserButtons.Add(button);
                presetBrowserItemByButton[button] = item;

                if (!string.IsNullOrEmpty(preferredSelectionPath) &&
                    string.Equals(item.SourcePath, preferredSelectionPath, StringComparison.OrdinalIgnoreCase))
                {
                    selectedPresetItem = item;
                }
            }

            if (selectedPresetItem == null)
            {
                selectedPresetItem = fallbackSelection;
            }
        }

        private void ClearPresetBrowser()
        {
            if (presetBrowserListContainer != null)
            {
                for (int i = presetBrowserListContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(presetBrowserListContainer.GetChild(i).gameObject);
                }
            }

            for (int i = 0; i < presetBrowserTextures.Count; i++)
            {
                if (presetBrowserTextures[i] != null)
                {
                    Destroy(presetBrowserTextures[i]);
                }
            }

            presetBrowserTextures.Clear();
            presetBrowserButtons.Clear();
            presetBrowserItemByButton.Clear();
        }

        private Button CreatePresetBrowserButton(SpritePresetBrowserItem item, int index)
        {
            GameObject buttonObject = CreateUiObject("PresetItem" + index, presetBrowserListContainer);
            Image background = buttonObject.AddComponent<Image>();
            background.color = ButtonColor;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(190, 190, 190, 255);
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(delegate
            {
                selectedPresetItem = item;
                RefreshPresetModal();
            });

            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
            buttonLayout.preferredHeight = 72f;

            HorizontalLayoutGroup rowLayout = buttonObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(6, 6, 6, 6);
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            GameObject previewObject = CreateUiObject("Preview", buttonObject.transform);
            Image previewFrame = previewObject.AddComponent<Image>();
            previewFrame.color = new Color32(10, 10, 10, 255);
            LayoutElement previewLayout = previewObject.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = 58f;
            previewLayout.preferredHeight = 58f;

            GameObject previewFillObject = CreateUiObject("PreviewFill", previewObject.transform);
            RawImage previewFill = previewFillObject.AddComponent<RawImage>();
            previewFill.color = Color.white;
            previewFill.texture = item != null ? item.PreviewTexture : null;
            RectTransform previewFillRect = previewFillObject.GetComponent<RectTransform>();
            StretchWithPadding(previewFillRect, 4f);
            AspectRatioFitter previewAspectFitter = previewFillObject.AddComponent<AspectRatioFitter>();
            previewAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewAspectFitter.aspectRatio = item != null && item.PreviewTexture != null && item.PreviewTexture.height > 0
                ? (float)item.PreviewTexture.width / item.PreviewTexture.height
                : 1f;

            GameObject textColumn = CreateUiObject("TextColumn", buttonObject.transform);
            VerticalLayoutGroup textLayout = textColumn.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2f;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
            textColumnLayout.flexibleWidth = 1f;

            TextMeshProUGUI nameText = CreateText("Name", textColumn.transform, item != null ? item.DisplayName : "Unknown", 14, FontStyle.Bold, TextAnchor.UpperLeft, TextColor);
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI detailText = CreateText("Detail", textColumn.transform, item != null ? item.Description : string.Empty, 12, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            detailText.enableWordWrapping = true;
            detailText.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement detailLayout = detailText.gameObject.AddComponent<LayoutElement>();
            detailLayout.flexibleHeight = 1f;

            return button;
        }

        private void RefreshPresetModal()
        {
            if (presetModalRoot == null)
            {
                return;
            }

            if (!presetModalOpen)
            {
                presetModalRoot.SetActive(false);
                return;
            }

            presetModalRoot.SetActive(true);

            StyleButton(presetLibraryTabButton, presetBrowserMode == SpritePresetBrowserMode.Library, false);
            StyleButton(presetBundleImportsTabButton, presetBrowserMode == SpritePresetBrowserMode.BundleFiles, false);
            StyleButton(presetPngImportsTabButton, presetBrowserMode == SpritePresetBrowserMode.PngFiles, false);

            for (int i = 0; i < presetBrowserButtons.Count; i++)
            {
                Button button = presetBrowserButtons[i];
                SpritePresetBrowserItem item;
                if (presetBrowserItemByButton.TryGetValue(button, out item))
                {
                    StyleButton(button, item == selectedPresetItem, false);
                }
            }

            if (presetDetailPreviewImage != null)
            {
                presetDetailPreviewImage.texture = selectedPresetItem != null ? selectedPresetItem.PreviewTexture : null;
            }

            if (presetDetailText != null)
            {
                presetDetailText.text = BuildPresetDetailText();
            }

            if (presetStatusText != null)
            {
                presetStatusText.text = BuildPresetStatusText();
            }

            RefreshPresetActionButtons();
        }

        private string BuildPresetDetailText()
        {
            if (selectedPresetItem == null)
            {
                return "No item selected.\n\nSaved Presets are reusable inside the mod. Preset Files are shareable preset folders. Image Files are raw PNGs for external editors.";
            }

            if (selectedPresetItem.Metadata == null)
            {
                return selectedPresetItem.DisplayName + "\n" +
                       selectedPresetItem.Description + "\n" +
                       "Path: " + selectedPresetItem.SourcePath;
            }

            SpritePresetBundleMetadata metadata = selectedPresetItem.Metadata;
            return metadata.name + "\n" +
                   "Target: " + metadata.targetKind + "\n" +
                   "Scope: " + metadata.scope + "\n" +
                   "Source: " + metadata.sourceName + "\n" +
                   "Entries: " + (metadata.entries != null ? metadata.entries.Count : 0) + "\n" +
                   "Created: " + metadata.createdAt + "\n" +
                   "Path: " + selectedPresetItem.SourcePath;
        }

        private string BuildPresetStatusText()
        {
            string baseHelp;
            if (presetBrowserMode == SpritePresetBrowserMode.Library)
            {
                baseHelp = "Saved presets:\n" + SpritePresetLibraryService.GetPresetsRoot() +
                           "\nUse Save Full Preset or Save Part Preset for quick in-game reuse.";
            }
            else if (presetBrowserMode == SpritePresetBrowserMode.BundleFiles)
            {
                baseHelp = "Preset folders:\n" + SpritePresetLibraryService.GetBundleFilesRoot() +
                           "\nExported folders show up in this same tab. You can apply them directly or add them to Saved Presets.";
            }
            else
            {
                LimbSpriteState state = GetSelectedState();
                string dimensions = state != null ? state.Width + "x" + state.Height : "no selected sprite";
                baseHelp = "Image files:\n" + SpritePresetLibraryService.GetPngFilesRoot() +
                           "\nExported PNGs show up in this same tab. Visible pixels auto-fit onto the current part when they can.\nCurrent selected sprite size: " + dimensions;
            }

            if (string.IsNullOrWhiteSpace(presetStatusMessage))
            {
                return baseHelp;
            }

            return presetStatusMessage + "\n\n" + baseHelp;
        }

        private void RefreshPresetActionButtons()
        {
            bool hasTarget = currentTarget != null || currentObjectTarget != null;
            bool hasState = GetSelectedState() != null;
            bool savedPresetSelected = selectedPresetItem != null && selectedPresetItem.ItemKind == SpritePresetItemKind.PresetBundle;
            bool bundleItemSelected = selectedPresetItem != null &&
                                      (selectedPresetItem.ItemKind == SpritePresetItemKind.PresetBundle ||
                                       selectedPresetItem.ItemKind == SpritePresetItemKind.ExternalBundle);
            bool bundleFileSelected = selectedPresetItem != null && selectedPresetItem.ItemKind == SpritePresetItemKind.ExternalBundle;
            bool pngFileSelected = selectedPresetItem != null && selectedPresetItem.ItemKind == SpritePresetItemKind.ExternalPng;
            bool presetTargetMatches = bundleItemSelected &&
                ((currentTarget != null && string.Equals(selectedPresetItem.Metadata.targetKind, "character", StringComparison.OrdinalIgnoreCase)) ||
                 (currentObjectTarget != null && string.Equals(selectedPresetItem.Metadata.targetKind, "object", StringComparison.OrdinalIgnoreCase)));

            SetButtonLabel(saveTargetPresetButton, currentObjectTarget != null ? "Save Object Preset" : "Save Full Preset");
            SetButtonLabel(saveSelectedPartPresetButton, currentObjectTarget != null ? "Save Sprite Preset" : "Save Part Preset");
            SetButtonLabel(applyPresetButton, "Apply to Target");
            SetButtonLabel(deletePresetButton, "Delete Saved Preset");
            SetButtonLabel(importBundleButton, "Add to Saved Presets");
            SetButtonLabel(exportBundleButton, "Write Preset Folder");
            SetButtonLabel(importPngButton, currentObjectTarget != null ? "Load Image to Sprite" : "Load Image to Part");
            SetButtonLabel(exportPngButton, currentObjectTarget != null ? "Write Sprite PNG" : "Write Part PNG");

            SetButtonInteractable(saveTargetPresetButton, hasTarget);
            SetButtonInteractable(saveSelectedPartPresetButton, hasState);
            SetButtonInteractable(applyPresetButton, bundleItemSelected && presetTargetMatches);
            SetButtonInteractable(deletePresetButton, presetBrowserMode == SpritePresetBrowserMode.Library && savedPresetSelected);
            SetButtonInteractable(importBundleButton, presetBrowserMode == SpritePresetBrowserMode.BundleFiles && bundleFileSelected);
            SetButtonInteractable(exportBundleButton, presetBrowserMode == SpritePresetBrowserMode.Library && savedPresetSelected);
            SetButtonInteractable(importPngButton, presetBrowserMode == SpritePresetBrowserMode.PngFiles && pngFileSelected && hasState);
            SetButtonInteractable(exportPngButton, hasState);
        }

        private void SaveTargetPresetFromModal()
        {
            SpritePresetOperationResult result = null;
            byte[] previewPng = CaptureCurrentPreviewPng();

            if (currentTarget != null)
            {
                result = SpritePresetLibraryService.SaveCharacterPreset(currentTarget, currentLimb, false, previewPng);
            }
            else if (currentObjectTarget != null)
            {
                result = SpritePresetLibraryService.SaveObjectPreset(currentObjectTarget, false, previewPng);
            }

            HandlePresetOperationResult(result, true, SpritePresetBrowserMode.Library);
        }

        private void SaveSelectedPartPatchFromModal()
        {
            SpritePresetOperationResult result = null;
            byte[] previewPng = CaptureCurrentPreviewPng();

            if (currentTarget != null)
            {
                result = SpritePresetLibraryService.SaveCharacterPreset(currentTarget, currentLimb, true, previewPng);
            }
            else if (currentObjectTarget != null)
            {
                result = SpritePresetLibraryService.SaveObjectPreset(currentObjectTarget, true, previewPng);
            }

            HandlePresetOperationResult(result, true, SpritePresetBrowserMode.Library);
        }

        private void ApplySelectedPresetFromModal()
        {
            SpritePresetOperationResult result = null;

            if (currentTarget != null)
            {
                result = SpritePresetLibraryService.ApplyPresetToCharacter(selectedPresetItem, currentTarget, currentLimb);
            }
            else if (currentObjectTarget != null)
            {
                result = SpritePresetLibraryService.ApplyPresetToObject(selectedPresetItem, currentObjectTarget);
            }

            HandlePresetOperationResult(result, true, selectedPresetItem != null ? selectedPresetItem.Mode : presetBrowserMode);
        }

        private void DeleteSelectedPresetFromModal()
        {
            string selectedPath = selectedPresetItem != null ? selectedPresetItem.SourcePath : null;
            SpritePresetOperationResult result = SpritePresetLibraryService.DeletePreset(selectedPresetItem);
            HandlePresetOperationResult(result, true, SpritePresetBrowserMode.Library, selectedPath);
        }

        private void ImportSelectedBundleFromModal()
        {
            SpritePresetOperationResult result = SpritePresetLibraryService.ImportBundle(selectedPresetItem);
            HandlePresetOperationResult(result, false, SpritePresetBrowserMode.Library);
        }

        private void ExportSelectedBundleFromModal()
        {
            SpritePresetOperationResult result = SpritePresetLibraryService.ExportBundle(selectedPresetItem);
            HandlePresetOperationResult(result, false, presetBrowserMode);
        }

        private void ImportSelectedPngFromModal()
        {
            SpritePresetOperationResult result = SpritePresetLibraryService.ImportPngToState(selectedPresetItem, GetSelectedState(), GetCurrentSelectionName());
            HandlePresetOperationResult(result, true, presetBrowserMode);
        }

        private void ImportPngFromFileDialog()
        {
            if (GetSelectedState() == null)
            {
                DialogBoxManager.Notification("No editable sprite was selected.");
                return;
            }

            if (nativePngFileDialogOpen)
            {
                DialogBoxManager.Notification("A sprite file picker is already open.");
                return;
            }

            StartCoroutine(ImportPngFromFileDialogRoutine());
        }

        private System.Collections.IEnumerator ImportPngFromFileDialogRoutine()
        {
            nativePngFileDialogOpen = true;
            RefreshActionButtons();

            bool uiWasActive = uiRoot != null && uiRoot.activeSelf;
            if (uiRoot != null)
                uiRoot.SetActive(false);

            string[] files = null;
            Exception dialogException = null;
            System.Threading.Thread thread = null;

            try
            {
                SpritePresetLibraryService.EnsureFolders();
                string initialDirectory = SpritePresetLibraryService.GetPngFilesRoot();
                bool allowMultiple = currentTarget != null;

                thread = new System.Threading.Thread(new System.Threading.ThreadStart(delegate
                {
                    try
                    {
                        files = ShowNativePngFileDialog(initialDirectory, allowMultiple);
                    }
                    catch (Exception exception)
                    {
                        dialogException = exception;
                    }
                }));

                try
                {
                    thread.SetApartmentState(System.Threading.ApartmentState.STA);
                    thread.Start();
                }
                catch (Exception exception)
                {
                    dialogException = exception;
                }
            }
            catch (Exception exception)
            {
                dialogException = exception;
            }

            if (dialogException == null && thread != null)
            {
                while (thread.IsAlive)
                {
                    yield return null;
                }

                thread.Join();
            }

            if (uiRoot != null && uiWasActive && editorOpen)
                uiRoot.SetActive(true);

            nativePngFileDialogOpen = false;
            RefreshActionButtons();

            if (dialogException != null)
            {
                Debug.LogWarning("[DB Singularity] Native PNG file dialog unavailable: " + dialogException);
                presetStatusMessage = "Native file picker was unavailable. Put PNG files in:\n" + SpritePresetLibraryService.GetPngFilesRoot();
                if (editorOpen)
                {
                    OpenPngImportModal();
                }

                yield break;
            }

            if (files == null || files.Length <= 0)
            {
                yield break;
            }

            if (GetSelectedState() == null)
            {
                DialogBoxManager.Notification("No editable sprite was selected.");
                yield break;
            }

            SpritePresetOperationResult result;
            try
            {
                if (currentTarget != null && files.Length > 1)
                {
                    result = SpritePresetLibraryService.ImportPngFilesToCharacter(files, currentTarget, currentLimb);
                }
                else
                {
                    result = SpritePresetLibraryService.ImportPngFileToState(files[0], GetSelectedState(), GetCurrentSelectionName());
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[DB Singularity] Sprite PNG import failed: " + exception);
                result = new SpritePresetOperationResult();
                result.Success = false;
                result.Message = "Sprite import failed: " + exception.Message;
            }

            HandlePresetOperationResult(result, true, SpritePresetBrowserMode.PngFiles);
        }

        private void ExportSelectedPngFromModal()
        {
            HandlePresetOperationResult(ExportCurrentPng(), false, presetBrowserMode);
        }

        private void ExportSelectedPngFromHeader()
        {
            HandlePresetOperationResult(ExportCurrentPng(), false, SpritePresetBrowserMode.PngFiles);
        }

        private void ExportBodyPngsFromHeader()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpritePresetLibraryService.ExportCharacterPngs(currentTarget)
                : ExportCurrentPng();

            HandlePresetOperationResult(result, false, SpritePresetBrowserMode.PngFiles);
        }

        private SpritePresetOperationResult ExportCurrentPng()
        {
            string targetName = currentTarget != null ? currentTarget.GetCharacterName() : currentObjectTarget != null ? currentObjectTarget.GetObjectName() : "Target";
            return SpritePresetLibraryService.ExportStatePng(GetSelectedState(), targetName, GetCurrentSelectionName());
        }

        private string[] ShowNativePngFileDialog(string initialDirectory, bool allowMultiple)
        {
            Type dialogType = Type.GetType("System.Windows.Forms.OpenFileDialog, System.Windows.Forms");
            if (dialogType == null)
            {
                throw new InvalidOperationException("System.Windows.Forms.OpenFileDialog is unavailable.");
            }

            object dialog = Activator.CreateInstance(dialogType);
            try
            {
                dialogType.GetProperty("Title").SetValue(dialog, "Import sprite PNG", null);
                dialogType.GetProperty("Filter").SetValue(dialog, "PNG files (*.png)|*.png|All files (*.*)|*.*", null);
                dialogType.GetProperty("Multiselect").SetValue(dialog, allowMultiple, null);
                dialogType.GetProperty("InitialDirectory").SetValue(dialog, initialDirectory, null);

                object result = dialogType.GetMethod("ShowDialog", Type.EmptyTypes).Invoke(dialog, null);
                return result != null && string.Equals(result.ToString(), "OK", StringComparison.OrdinalIgnoreCase)
                    ? (string[])dialogType.GetProperty("FileNames").GetValue(dialog, null)
                    : null;
            }
            finally
            {
                IDisposable disposable = dialog as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }

        private void HandlePresetOperationResult(SpritePresetOperationResult result, bool refreshEditor, SpritePresetBrowserMode modeAfterOperation)
        {
            HandlePresetOperationResult(result, refreshEditor, modeAfterOperation, null);
        }

        private void HandlePresetOperationResult(SpritePresetOperationResult result, bool refreshEditor, SpritePresetBrowserMode modeAfterOperation, string previousPath)
        {
            if (result == null)
            {
                return;
            }

            presetStatusMessage = result.Message;
            DialogBoxManager.Notification(result.Message);

            string preferredSelectionPath = result.BrowserItem != null ? result.BrowserItem.SourcePath : null;
            if (result.BrowserItem != null && result.BrowserItem.PreviewTexture != null)
            {
                Destroy(result.BrowserItem.PreviewTexture);
                result.BrowserItem.PreviewTexture = null;
            }

            if (refreshEditor)
            {
                RefreshAll();
            }

            if (result.Success || modeAfterOperation != presetBrowserMode)
            {
                presetBrowserMode = modeAfterOperation;
            }

            if (result.Success)
            {
                selectedPresetItem = result.BrowserItem;
            }
            else if (!string.IsNullOrEmpty(previousPath) && selectedPresetItem != null && string.Equals(selectedPresetItem.SourcePath, previousPath, StringComparison.OrdinalIgnoreCase))
            {
                selectedPresetItem = null;
            }

            RebuildPresetBrowser(selectedPresetItem != null ? selectedPresetItem.SourcePath : preferredSelectionPath);
            RefreshPresetModal();
        }

        private void CreateToolButton(Transform parent, ToolMode mode, string label)
        {
            Button button = CreateButton(mode.ToString() + "Button", parent, label, new UnityEngine.Events.UnityAction(delegate
            {
                SetTool(mode);
            }));
            SetButtonFontSize(button, 14f);
            toolButtons[mode] = button;
        }

        private void CreatePlaceholderToolButton(Transform parent, string label, UiIconKind icon)
        {
            Button button = CreateButton(label + "PlaceholderButton", parent, label, new UnityEngine.Events.UnityAction(delegate
            {
                DialogBoxManager.Notification(label + " is a placeholder for now.");
            }));
            SetButtonFontSize(button, 14f);
            StyleButton(button, false, false);
        }

        private Slider CreateLabeledSlider(Transform parent, string label, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject row = CreateUiObject(label + "Row", parent);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 22f;

            TextMeshProUGUI text = CreateText(label + "Label", row.transform, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = 70f;
            textLayout.preferredHeight = 22f;

            Slider slider = CreateSlider(label + "Slider", row.transform, false);
            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(onValueChanged);
            }

            return slider;
        }

        private Slider CreateSlider(string name, Transform parent, bool wholeNumbers)
        {
            GameObject sliderObject = CreateUiObject(name, parent);
            LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
            sliderLayout.preferredHeight = 14f;
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minWidth = 0f;

            Image background = sliderObject.AddComponent<Image>();
            ApplyRoundedImage(background, ButtonColor, ButtonBorderColor, CornerRadius, 1);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.wholeNumbers = wholeNumbers;

            GameObject fillArea = CreateUiObject("FillArea", sliderObject.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(4f, 3f);
            fillAreaRect.offsetMax = new Vector2(-6f, -3f);

            GameObject fill = CreateUiObject("Fill", fillArea.transform);
            Image fillImage = fill.AddComponent<Image>();
            ApplyRoundedImage(fillImage, AccentColor, AccentColor, CornerRadius, 0);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);

            GameObject handleArea = CreateUiObject("HandleArea", sliderObject.transform);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(0f, 0f);
            handleAreaRect.offsetMax = new Vector2(0f, 0f);

            GameObject handle = CreateUiObject("Handle", handleArea.transform);
            Image handleImage = handle.AddComponent<Image>();
            ApplyRoundedImage(handleImage, AccentColor, AccentColor, CornerRadius, 1);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10f, 10f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private Slider CreateHeaderSlider(string name, Transform parent)
        {
            Slider slider = CreateSlider(name, parent, false);
            LayoutElement layout = slider.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }

            return slider;
        }

        private TMP_InputField CreateResizeInputField(Transform parent, string label)
        {
            GameObject column = CreateUiObject(label + "Column", parent);
            VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 5f;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            LayoutElement columnElement = column.AddComponent<LayoutElement>();
            columnElement.flexibleWidth = 1f;
            columnElement.preferredHeight = 58f;

            TextMeshProUGUI labelText = CreateText(label + "Label", column.transform, label, 12, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 18f;

            GameObject inputObject = CreateUiObject(label + "Input", column.transform);
            Image image = inputObject.AddComponent<Image>();
            ApplyRoundedImage(image, ButtonColor, ButtonBorderColor, CornerRadius, 1);
            LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
            inputLayout.preferredHeight = 32f;

            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 4;
            input.caretColor = TextColor;
            input.selectionColor = new Color(0.35f, 0.55f, 0.85f, 0.42f);

            TextMeshProUGUI text = CreateText("Text", inputObject.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleCenter, TextColor);
            text.margin = new Vector4(8f, 2f, 8f, 2f);
            Stretch(text.rectTransform);
            input.textComponent = text;
            input.textViewport = inputObject.GetComponent<RectTransform>();

            return input;
        }

        private TMP_InputField CreateTextInputField(Transform parent, string name, int characterLimit)
        {
            GameObject inputObject = CreateUiObject(name, parent);
            Image image = inputObject.AddComponent<Image>();
            ApplyRoundedImage(image, ButtonColor, ButtonBorderColor, CornerRadius, 1);
            LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
            inputLayout.preferredHeight = 38f;

            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = Mathf.Max(1, characterLimit);
            input.caretColor = TextColor;
            input.selectionColor = new Color(0.35f, 0.55f, 0.85f, 0.42f);

            TextMeshProUGUI text = CreateText("Text", inputObject.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);
            text.margin = new Vector4(10f, 2f, 10f, 2f);
            Stretch(text.rectTransform);
            input.textComponent = text;
            input.textViewport = inputObject.GetComponent<RectTransform>();

            return input;
        }

        private void RebuildLimbButtons()
        {
            if (limbButtonContainer == null)
            {
                return;
            }

            for (int i = limbButtonContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(limbButtonContainer.GetChild(i).gameObject);
            }

            limbButtons.Clear();
            limbByButton.Clear();
            objectPartButtons.Clear();
            objectRendererByButton.Clear();
            objectSelectionButton = null;
            hairButton = null;
            hairRendererByButton = null;

            if (currentTarget != null)
            {
                SpriteRenderer hair = currentTarget.GetHairRenderer(true);
                if (hair != null)
                {
                    hairButton = CreateButton("HairButton", limbButtonContainer, "Hair", new UnityEngine.Events.UnityAction(SelectHair));
                    LayoutElement hairLayout = hairButton.gameObject.AddComponent<LayoutElement>();
                    hairLayout.preferredHeight = 30f;
                    hairLayout.minHeight = 30f;
                    TextMeshProUGUI hairLabel = hairButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (hairLabel != null)
                    {
                        hairLabel.fontSize = 14;
                        hairLabel.fontStyle = FontStyles.Bold;
                        hairLabel.alignment = TextAlignmentOptions.Center;
                        hairLabel.overflowMode = TextOverflowModes.Truncate;
                        hairLabel.margin = new Vector4(6f, 0f, 6f, 0f);
                    }

                    hairRendererByButton = hair;
                    limbButtons.Add(hairButton);
                }

                List<LimbBehaviour> limbs = currentTarget.GetAvailableLimbs();

                for (int i = 0; i < limbs.Count; i++)
                {
                    LimbBehaviour limb = limbs[i];
                    Button button = CreateButton("LimbButton" + i, limbButtonContainer, string.Empty, new UnityEngine.Events.UnityAction(delegate
                    {
                        SelectLimb(limb);
                    }));
                    LayoutElement limbLayout = button.gameObject.AddComponent<LayoutElement>();
                    limbLayout.preferredHeight = 30f;
                    limbLayout.minHeight = 30f;
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.fontSize = 14;
                        label.fontStyle = FontStyles.Bold;
                        label.alignment = TextAlignmentOptions.Center;
                        label.overflowMode = TextOverflowModes.Truncate;
                        label.margin = new Vector4(6f, 0f, 6f, 0f);
                    }
                    limbButtons.Add(button);
                    limbByButton[button] = limb;
                }
            }
            else if (currentObjectTarget != null)
            {
                List<SpriteRenderer> renderers = currentObjectTarget.GetEditableRenderers();
                for (int i = 0; i < renderers.Count; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    Button button = CreateButton("ObjectPartButton" + i, limbButtonContainer, string.Empty, new UnityEngine.Events.UnityAction(delegate
                    {
                        SelectObjectPart(renderer);
                    }));
                    LayoutElement partLayout = button.gameObject.AddComponent<LayoutElement>();
                    partLayout.preferredHeight = 30f;
                    partLayout.minHeight = 30f;

                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.fontSize = 14;
                        label.fontStyle = FontStyles.Bold;
                        label.alignment = TextAlignmentOptions.Center;
                        label.overflowMode = TextOverflowModes.Truncate;
                        label.margin = new Vector4(6f, 0f, 6f, 0f);
                    }

                    objectPartButtons.Add(button);
                    objectRendererByButton[button] = renderer;
                    if (i == 0)
                    {
                        objectSelectionButton = button;
                    }
                }
            }
        }

        private void SelectObjectPart(SpriteRenderer renderer)
        {
            FinalizeActiveStroke();
            ClearSelectionState();

            if (currentObjectTarget == null)
            {
                return;
            }

            LimbSpriteState previousState = GetSelectedState();
            if (previousState != null)
            {
                previousState.FinalizeForWorldDisplay();
            }

            currentObjectTarget.SelectRenderer(renderer);
            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                state.PrepareForEditing();
            }

            RefreshAll();
        }

        private void SelectHair()
        {
            FinalizeActiveStroke();
            ClearSelectionState();

            if (currentTarget == null)
            {
                return;
            }

            if (currentLimb != null)
            {
                LimbSpriteState previousLimbState;
                if (currentTarget.TryGetExistingState(currentLimb, out previousLimbState) && previousLimbState != null)
                {
                    previousLimbState.FinalizeForWorldDisplay();
                }
            }

            if (currentTarget.IsAccessorySelected)
            {
                LimbSpriteState previousHair;
                if (currentTarget.TryGetExistingAccessoryState(currentTarget.SelectedAccessory, out previousHair) && previousHair != null)
                {
                    previousHair.FinalizeForWorldDisplay();
                }
            }

            SpriteRenderer hair = currentTarget.GetHairRenderer(true);
            if (hair == null)
            {
                DialogBoxManager.Notification("No hair sprite was found on that character.");
                return;
            }

            currentLimb = null;
            currentTarget.SelectAccessory(hair);
            hairRendererByButton = hair;

            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                state.PrepareForEditing();
            }

            RefreshAll();
        }

        private void SelectLimb(LimbBehaviour limb)
        {
            FinalizeActiveStroke();
            ClearSelectionState();

            if (currentTarget != null && currentTarget.IsAccessorySelected)
            {
                LimbSpriteState previousHair;
                if (currentTarget.TryGetExistingAccessoryState(currentTarget.SelectedAccessory, out previousHair) && previousHair != null)
                {
                    previousHair.FinalizeForWorldDisplay();
                }

                currentTarget.ClearAccessorySelection();
            }

            if (currentTarget != null && currentLimb != null)
            {
                LimbSpriteState previousState;
                if (currentTarget.TryGetExistingState(currentLimb, out previousState) && previousState != null)
                {
                    previousState.FinalizeForWorldDisplay();
                }
            }

            currentLimb = currentTarget.GetPreferredLimb(limb);
            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                state.PrepareForEditing();
            }

            RefreshAll();
        }

        private void SetTool(ToolMode mode)
        {
            currentTool = mode;
            RefreshToolButtons();
            RefreshStatusText();
        }

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshLimbButtons();
            RefreshToolButtons();
            RefreshColorUi();
            RefreshStatusText();
            RefreshTextures();
            RefreshActionButtons();
            RefreshLayerButtons();
            RefreshInfo();
            RefreshPresetModal();
        }

        private void RefreshHeader()
        {
            if (titleText == null || subtitleText == null)
            {
                return;
            }

            titleText.text = "Sprite Editor";

            if (currentTarget == null && currentObjectTarget == null)
            {
                subtitleText.text = "No target selected";
                return;
            }

            if (currentTarget != null)
            {
                LimbSpriteState state = GetSelectedState();
                string selection = GetCurrentSelectionName();
                string size = state != null ? state.Width + " x " + state.Height + " px" : "no sprite";
                subtitleText.text = StripRichTextTags(currentTarget.GetCharacterName()) + "  |  " + StripRichTextTags(selection) + "  |  " + size + "  |  " + GetToolLabel(currentTool);
            }
            else
            {
                LimbSpriteState state = GetSelectedState();
                string selection = GetCurrentSelectionName();
                string size = state != null ? state.Width + " x " + state.Height + " px" : "no sprite";
                subtitleText.text = StripRichTextTags(currentObjectTarget.GetObjectName()) + "  |  " + StripRichTextTags(selection) + "  |  " + size + "  |  " + GetToolLabel(currentTool);
            }
        }

        private void RefreshLimbButtons()
        {
            if (currentObjectTarget != null)
            {
                for (int i = 0; i < objectPartButtons.Count; i++)
                {
                    Button button = objectPartButtons[i];
                    SpriteRenderer renderer;
                    if (!objectRendererByButton.TryGetValue(button, out renderer))
                    {
                        continue;
                    }

                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                    {
                        string text = currentObjectTarget.GetPartLabel(renderer);
                        LimbSpriteState state = renderer == currentObjectTarget.SelectedRenderer ? GetSelectedState() : null;
                        if (state != null && state.HasChanges)
                        {
                            text += " *";
                        }

                        label.text = text;
                    }

                    StyleButton(button, renderer == currentObjectTarget.SelectedRenderer, false);
                }

                return;
            }

            for (int i = 0; i < limbButtons.Count; i++)
            {
                Button button = limbButtons[i];
                if (button == hairButton)
                {
                    TextMeshProUGUI hairLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (hairLabel != null)
                    {
                        string text = "Hair";
                        LimbSpriteState hairState;
                        if (currentTarget != null &&
                            currentTarget.SelectedAccessory != null &&
                            currentTarget.TryGetExistingAccessoryState(currentTarget.SelectedAccessory, out hairState) &&
                            hairState != null &&
                            hairState.HasChanges)
                        {
                            text += " *";
                        }
                        else if (currentTarget != null &&
                                 hairRendererByButton != null &&
                                 currentTarget.TryGetExistingAccessoryState(hairRendererByButton, out hairState) &&
                                 hairState != null &&
                                 hairState.HasChanges)
                        {
                            text += " *";
                        }

                        hairLabel.text = text;
                    }

                    StyleButton(button, currentTarget != null && currentTarget.IsAccessorySelected, false);
                    continue;
                }

                LimbBehaviour limb;

                if (!limbByButton.TryGetValue(button, out limb))
                {
                    continue;
                }

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    string text = currentTarget.GetLimbDisplayName(limb);
                    LimbSpriteState existingState;
                    if (currentTarget.TryGetExistingState(limb, out existingState) && existingState.HasChanges)
                    {
                        text += " *";
                    }
                    label.text = text;
                }

                StyleButton(button, !currentTarget.IsAccessorySelected && limb == currentLimb, false);
            }
        }

        private void RefreshToolButtons()
        {
            foreach (KeyValuePair<ToolMode, Button> pair in toolButtons)
            {
                StyleButton(pair.Value, pair.Key == currentTool, false);
            }
        }

        private void RefreshColorUi()
        {
            suppressColorCallbacks = true;

            if (opacitySlider != null) { opacitySlider.value = selectedColor.a; }

            suppressColorCallbacks = false;

            SyncBrushSlider();
            bool pickerAvailable = IsNativeColorPickerAvailable();

            if (selectedColorImage != null)
            {
                selectedColorImage.color = selectedColor;
            }

            if (selectedColorButton != null)
            {
                selectedColorButton.interactable = pickerAvailable && !nativeColorPickerOpen;
            }

            if (pickColorButton != null)
            {
                pickColorButton.interactable = pickerAvailable && !nativeColorPickerOpen;
            }

            if (colorDetailsText != null)
            {
                colorDetailsText.text = "HEX " + GetHexString((Color32)selectedColor) + "\nOpacity " + Mathf.RoundToInt(selectedColor.a * 100f) + "%";
            }

            for (int i = 0; i < swatchViews.Count; i++)
            {
                SwatchView swatch = swatchViews[i];
                StyleSwatch(swatch);
            }

            RefreshColorHistoryUi();
        }

        private void RefreshStatusText()
        {
            if (brushSizeText != null)
            {
                brushSizeText.text = brushSize.ToString();
            }

            LimbSpriteState state = GetSelectedState();
            if (canvasStatusText == null)
            {
                return;
            }

            if (state == null)
            {
                canvasStatusText.text = "No editable sprite available.";
                return;
            }

            string targetName = GetCurrentSelectionName();
            if (canvasResizeMode)
            {
                int width = canvasResizeBaseWidth + canvasResizePadLeft + canvasResizePadRight;
                int height = canvasResizeBaseHeight + canvasResizePadBottom + canvasResizePadTop;
                if (!canvasResizeDragInProgress)
                {
                    width = state.Width;
                    height = state.Height;
                }

                canvasStatusText.text = targetName + "  |  Canvas Resize  |  " + width + " x " + height + " px  |  drag edges  |  C/Esc exit";
                return;
            }

            canvasStatusText.text = targetName + "  |  " + state.Width + " x " + state.Height + " px  |  " + GetToolLabel(currentTool) + "  |  " + Mathf.RoundToInt(canvasZoom * 100f) + "%";
        }

        private void SetCanvasResizeMode(bool enabled)
        {
            if (enabled)
            {
                LimbSpriteState state = GetSelectedState();
                if (state == null || !state.IsValid)
                {
                    DialogBoxManager.Notification("No editable sprite was selected.");
                    return;
                }

                FinalizeActiveStroke();
                EndActiveStroke();
                canvasResizeMode = true;
                canvasResizeDragInProgress = false;
                canvasResizeDragHandle = CanvasResizeHandleKind.None;
                canvasResizeBaseWidth = state.Width;
                canvasResizeBaseHeight = state.Height;
                canvasResizePadLeft = 0;
                canvasResizePadRight = 0;
                canvasResizePadBottom = 0;
                canvasResizePadTop = 0;
            }
            else
            {
                CancelCanvasResizeDrag();
                canvasResizeMode = false;
            }

            if (canvasHolderMask != null)
            {
                canvasHolderMask.enabled = !canvasResizeMode;
            }

            if (canvasResizeHandlesRoot != null)
            {
                canvasResizeHandlesRoot.SetActive(canvasResizeMode);
            }

            if (canvasResizePreviewRect != null)
            {
                canvasResizePreviewRect.gameObject.SetActive(false);
            }

            if (canvasResizeModeText != null)
            {
                canvasResizeModeText.gameObject.SetActive(canvasResizeMode);
                canvasResizeModeText.text = canvasResizeMode ? "RESIZE MODE" : string.Empty;
            }

            RefreshStatusText();
        }

        public void BeginCanvasResizeDrag(CanvasResizeHandleKind kind, PointerEventData eventData)
        {
            if (!canvasResizeMode || eventData == null || canvasImage == null)
            {
                return;
            }

            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.IsValid)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasImage.rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            canvasResizeDragInProgress = true;
            canvasResizeDragHandle = kind;
            canvasResizeDragStartLocal = localPoint;
            canvasResizeBaseWidth = state.Width;
            canvasResizeBaseHeight = state.Height;
            canvasResizePadLeft = 0;
            canvasResizePadRight = 0;
            canvasResizePadBottom = 0;
            canvasResizePadTop = 0;
            UpdateCanvasResizePreview();
            RefreshStatusText();
        }

        public void UpdateCanvasResizeDrag(PointerEventData eventData)
        {
            if (!canvasResizeMode || !canvasResizeDragInProgress || eventData == null || canvasImage == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasImage.rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            Rect rect = canvasImage.rectTransform.rect;
            if (rect.width < 1f || rect.height < 1f || canvasResizeBaseWidth < 1 || canvasResizeBaseHeight < 1)
            {
                return;
            }

            float deltaX = localPoint.x - canvasResizeDragStartLocal.x;
            float deltaY = localPoint.y - canvasResizeDragStartLocal.y;
            int pixelDeltaX = Mathf.RoundToInt(deltaX * canvasResizeBaseWidth / rect.width);
            int pixelDeltaY = Mathf.RoundToInt(deltaY * canvasResizeBaseHeight / rect.height);

            int padLeft = 0;
            int padRight = 0;
            int padBottom = 0;
            int padTop = 0;

            switch (canvasResizeDragHandle)
            {
                case CanvasResizeHandleKind.Left:
                    padLeft = -pixelDeltaX;
                    break;
                case CanvasResizeHandleKind.Right:
                    padRight = pixelDeltaX;
                    break;
                case CanvasResizeHandleKind.Bottom:
                    padBottom = -pixelDeltaY;
                    break;
                case CanvasResizeHandleKind.Top:
                    padTop = pixelDeltaY;
                    break;
                case CanvasResizeHandleKind.BottomLeft:
                    padLeft = -pixelDeltaX;
                    padBottom = -pixelDeltaY;
                    break;
                case CanvasResizeHandleKind.BottomRight:
                    padRight = pixelDeltaX;
                    padBottom = -pixelDeltaY;
                    break;
                case CanvasResizeHandleKind.TopLeft:
                    padLeft = -pixelDeltaX;
                    padTop = pixelDeltaY;
                    break;
                case CanvasResizeHandleKind.TopRight:
                    padRight = pixelDeltaX;
                    padTop = pixelDeltaY;
                    break;
            }

            ClampCanvasResizePads(ref padLeft, ref padRight, ref padBottom, ref padTop);
            canvasResizePadLeft = padLeft;
            canvasResizePadRight = padRight;
            canvasResizePadBottom = padBottom;
            canvasResizePadTop = padTop;
            UpdateCanvasResizePreview();
            RefreshStatusText();
        }

        public void EndCanvasResizeDrag(PointerEventData eventData)
        {
            if (!canvasResizeMode || !canvasResizeDragInProgress)
            {
                return;
            }

            int padLeft = canvasResizePadLeft;
            int padRight = canvasResizePadRight;
            int padBottom = canvasResizePadBottom;
            int padTop = canvasResizePadTop;
            canvasResizeDragInProgress = false;
            canvasResizeDragHandle = CanvasResizeHandleKind.None;

            if (padLeft == 0 && padRight == 0 && padBottom == 0 && padTop == 0)
            {
                if (canvasResizePreviewRect != null)
                {
                    canvasResizePreviewRect.gameObject.SetActive(false);
                }

                RefreshStatusText();
                return;
            }

            LimbSpriteState state = GetSelectedState();
            if (state == null || !state.IsValid)
            {
                CancelCanvasResizeDrag();
                return;
            }

            int newWidth = Mathf.Clamp(canvasResizeBaseWidth + padLeft + padRight, 1, 512);
            int newHeight = Mathf.Clamp(canvasResizeBaseHeight + padBottom + padTop, 1, 512);
            int offsetX = padLeft;
            int offsetY = padBottom;

            FinalizeActiveStroke();
            if (state.ResizeCanvas(newWidth, newHeight, offsetX, offsetY))
            {
                canvasResizeBaseWidth = newWidth;
                canvasResizeBaseHeight = newHeight;
                canvasResizePadLeft = 0;
                canvasResizePadRight = 0;
                canvasResizePadBottom = 0;
                canvasResizePadTop = 0;
                if (canvasResizePreviewRect != null)
                {
                    canvasResizePreviewRect.gameObject.SetActive(false);
                }

                RefreshAll();
            }
            else
            {
                CancelCanvasResizeDrag();
            }
        }

        private void CancelCanvasResizeDrag()
        {
            canvasResizeDragInProgress = false;
            canvasResizeDragHandle = CanvasResizeHandleKind.None;
            canvasResizePadLeft = 0;
            canvasResizePadRight = 0;
            canvasResizePadBottom = 0;
            canvasResizePadTop = 0;
            if (canvasResizePreviewRect != null)
            {
                canvasResizePreviewRect.gameObject.SetActive(false);
            }

            RefreshStatusText();
        }

        private void ClampCanvasResizePads(ref int padLeft, ref int padRight, ref int padBottom, ref int padTop)
        {
            int maxExpandX = 512 - canvasResizeBaseWidth;
            int maxExpandY = 512 - canvasResizeBaseHeight;
            int minWidth = 1;
            int minHeight = 1;

            padLeft = Mathf.Clamp(padLeft, -(canvasResizeBaseWidth - minWidth), maxExpandX);
            padRight = Mathf.Clamp(padRight, -(canvasResizeBaseWidth - minWidth), maxExpandX);
            while (canvasResizeBaseWidth + padLeft + padRight > 512)
            {
                if (Mathf.Abs(padLeft) >= Mathf.Abs(padRight))
                {
                    padLeft -= padLeft > 0 ? 1 : -1;
                }
                else
                {
                    padRight -= padRight > 0 ? 1 : -1;
                }
            }

            while (canvasResizeBaseWidth + padLeft + padRight < minWidth)
            {
                if (padLeft <= padRight)
                {
                    padLeft++;
                }
                else
                {
                    padRight++;
                }
            }

            padBottom = Mathf.Clamp(padBottom, -(canvasResizeBaseHeight - minHeight), maxExpandY);
            padTop = Mathf.Clamp(padTop, -(canvasResizeBaseHeight - minHeight), maxExpandY);
            while (canvasResizeBaseHeight + padBottom + padTop > 512)
            {
                if (Mathf.Abs(padBottom) >= Mathf.Abs(padTop))
                {
                    padBottom -= padBottom > 0 ? 1 : -1;
                }
                else
                {
                    padTop -= padTop > 0 ? 1 : -1;
                }
            }

            while (canvasResizeBaseHeight + padBottom + padTop < minHeight)
            {
                if (padBottom <= padTop)
                {
                    padBottom++;
                }
                else
                {
                    padTop++;
                }
            }
        }

        private void UpdateCanvasResizePreview()
        {
            if (canvasResizePreviewRect == null || canvasImage == null)
            {
                return;
            }

            Rect rect = canvasImage.rectTransform.rect;
            if (rect.width < 1f || rect.height < 1f || canvasResizeBaseWidth < 1 || canvasResizeBaseHeight < 1)
            {
                return;
            }

            float unitX = rect.width / canvasResizeBaseWidth;
            float unitY = rect.height / canvasResizeBaseHeight;
            float left = rect.xMin - canvasResizePadLeft * unitX;
            float right = rect.xMax + canvasResizePadRight * unitX;
            float bottom = rect.yMin - canvasResizePadBottom * unitY;
            float top = rect.yMax + canvasResizePadTop * unitY;

            canvasResizePreviewRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasResizePreviewRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasResizePreviewRect.pivot = new Vector2(0.5f, 0.5f);
            canvasResizePreviewRect.sizeDelta = new Vector2(Mathf.Max(1f, right - left), Mathf.Max(1f, top - bottom));
            canvasResizePreviewRect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            canvasResizePreviewRect.gameObject.SetActive(true);
        }

        private void RefreshTextures()
        {
            LimbSpriteState state = GetSelectedState();
            Texture texture = state != null ? state.WorkingTexture : null;
            float canvasAspectRatio = state != null && state.Height > 0 ? (float)state.Width / state.Height : 1f;

            if (canvasImage != null)
            {
                canvasImage.texture = texture;
                canvasImage.color = Color.white;
            }

            if (canvasAspect != null)
            {
                canvasAspect.aspectRatio = canvasAspectRatio;
            }

            ApplyCanvasZoom();

            Texture previewTexture = texture;
            float previewAspectRatio = canvasAspectRatio;
            if (TryRenderTargetPreview(out previewTexture, out previewAspectRatio))
            {
                if (previewImage != null)
                {
                    previewImage.texture = previewTexture;
                }
            }
            else if (previewImage != null)
            {
                previewImage.texture = previewTexture;
            }

            if (previewAspect != null)
            {
                previewAspect.aspectRatio = previewAspectRatio;
            }
        }

        private bool TryRenderTargetPreview(out Texture previewTexture, out float previewAspectRatio)
        {
            previewTexture = null;
            previewAspectRatio = 1f;

            List<SpriteRenderer> sourceRenderers = BuildPreviewSourceRenderers();
            if (sourceRenderers.Count <= 0)
            {
                return false;
            }

            EnsurePreviewWorld();
            if (previewRendererRoot == null || previewCamera == null)
            {
                return false;
            }

            Vector3 referencePosition = GetPreviewReferencePosition();
            Bounds bounds = default(Bounds);
            bool hasBounds = false;

            EnsurePreviewRendererCount(sourceRenderers.Count);

            for (int i = 0; i < sourceRenderers.Count; i++)
            {
                SpriteRenderer source = sourceRenderers[i];
                SpriteRenderer preview = previewSpriteRenderers[i];

                SyncPreviewRenderer(preview, source, referencePosition);

                if (!hasBounds)
                {
                    bounds = preview.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(preview.bounds);
                }
            }

            for (int i = sourceRenderers.Count; i < previewSpriteRenderers.Count; i++)
            {
                if (previewSpriteRenderers[i] != null)
                {
                    previewSpriteRenderers[i].enabled = false;
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            float width = Mathf.Max(0.25f, bounds.size.x);
            float height = Mathf.Max(0.25f, bounds.size.y);
            previewAspectRatio = width / height;

            int textureWidth;
            int textureHeight;
            GetPreviewTextureSize(previewAspectRatio, out textureWidth, out textureHeight);
            EnsurePreviewRenderTexture(textureWidth, textureHeight);
            if (previewRenderTexture == null)
            {
                return false;
            }

            float paddedWidth = width * (1f + PreviewPadding);
            float paddedHeight = height * (1f + PreviewPadding);
            float cameraAspect = (float)textureWidth / textureHeight;
            float halfWidth = paddedWidth * 0.5f;
            float halfHeight = paddedHeight * 0.5f;
            float orthographicSize = Mathf.Max(halfHeight, halfWidth / cameraAspect, 0.5f);

            previewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            previewCamera.orthographicSize = orthographicSize;
            previewCamera.targetTexture = previewRenderTexture;
            previewCamera.Render();

            previewTexture = previewRenderTexture;
            return true;
        }

        private byte[] CaptureCurrentPreviewPng()
        {
            Texture previewTexture;
            float previewAspectRatio;
            if (!TryRenderTargetPreview(out previewTexture, out previewAspectRatio))
            {
                return null;
            }

            RenderTexture renderTexture = previewTexture as RenderTexture;
            if (renderTexture == null)
            {
                return null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            byte[] bytes = SpritePresetLibraryService.EncodeTextureToPng(texture);
            Destroy(texture);
            RenderTexture.active = previous;
            return bytes;
        }

        private List<SpriteRenderer> BuildPreviewSourceRenderers()
        {
            List<SpriteRenderer> result = new List<SpriteRenderer>();

            if (currentTarget != null)
            {
                List<LimbBehaviour> limbs = currentTarget.GetAvailableLimbs();
                for (int i = 0; i < limbs.Count; i++)
                {
                    SpriteRenderer renderer = GetPreviewSourceRenderer(limbs[i]);
                    if (renderer != null && renderer.sprite != null)
                    {
                        result.Add(renderer);
                    }
                }

                SpriteRenderer hair = currentTarget.GetHairRenderer(false);
                if (hair == null && currentTarget.IsAccessorySelected)
                {
                    hair = currentTarget.SelectedAccessory;
                }

                if (hair != null && hair.sprite != null && !result.Contains(hair))
                {
                    result.Add(hair);
                }
            }
            else if (currentObjectTarget != null && currentObjectTarget.PhysicalBehaviour != null)
            {
                List<SpriteRenderer> renderers = currentObjectTarget.GetEditableRenderers();
                for (int i = 0; i < renderers.Count; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer != null && renderer.sprite != null)
                    {
                        result.Add(renderer);
                    }
                }
            }

            return result;
        }

        private Vector3 GetPreviewReferencePosition()
        {
            if (currentTarget != null && currentTarget.Person != null)
            {
                return currentTarget.Person.transform.position;
            }

            if (currentObjectTarget != null && currentObjectTarget.PhysicalBehaviour != null)
            {
                return currentObjectTarget.PhysicalBehaviour.transform.position;
            }

            return Vector3.zero;
        }

        private void EnsurePreviewWorld()
        {
            if (previewRendererRoot != null && previewCamera != null)
            {
                return;
            }

            GameObject root = new GameObject("HypermodifiedLiveSpriteEditorPreviewRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = PreviewWorldOrigin;
            root.layer = PreviewLayer;
            DontDestroyOnLoad(root);
            previewRendererRoot = root.transform;

            GameObject cameraObject = new GameObject("HypermodifiedLiveSpriteEditorPreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(previewRendererRoot, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            cameraObject.layer = PreviewLayer;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.hideFlags = HideFlags.HideAndDontSave;
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;
            previewCamera.cullingMask = unchecked((int)(1u << PreviewLayer));
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
        }

        private void DestroyPreviewWorld()
        {
            if (previewRenderTexture != null)
            {
                previewRenderTexture.Release();
                Destroy(previewRenderTexture);
                previewRenderTexture = null;
            }

            for (int i = 0; i < previewSpriteRenderers.Count; i++)
            {
                if (previewSpriteRenderers[i] != null)
                {
                    Destroy(previewSpriteRenderers[i].gameObject);
                }
            }

            previewSpriteRenderers.Clear();

            foreach (KeyValuePair<int, Material> entry in previewMaterialCache)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value);
                }
            }

            previewMaterialCache.Clear();

            if (previewCamera != null)
            {
                Destroy(previewCamera.gameObject);
                previewCamera = null;
            }

            if (previewRendererRoot != null)
            {
                Destroy(previewRendererRoot.gameObject);
                previewRendererRoot = null;
            }
        }

        private void EnsurePreviewRendererCount(int requiredCount)
        {
            for (int i = previewSpriteRenderers.Count; i < requiredCount; i++)
            {
                GameObject rendererObject = new GameObject("PreviewSprite" + i);
                rendererObject.hideFlags = HideFlags.HideAndDontSave;
                rendererObject.layer = PreviewLayer;
                rendererObject.transform.SetParent(previewRendererRoot, false);
                SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
                spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
                previewSpriteRenderers.Add(spriteRenderer);
            }
        }

        private void SyncPreviewRenderer(SpriteRenderer preview, SpriteRenderer source, Vector3 referencePosition)
        {
            if (preview == null || source == null)
            {
                return;
            }

            preview.enabled = source.enabled && source.sprite != null;
            preview.sprite = source.sprite;
            preview.color = source.color;
            preview.flipX = source.flipX;
            preview.flipY = source.flipY;
            preview.drawMode = source.drawMode;
            preview.size = source.size;
            preview.sortingLayerID = source.sortingLayerID;
            preview.sortingOrder = source.sortingOrder;
            preview.maskInteraction = source.maskInteraction;
            preview.sharedMaterial = GetPreviewMaterial(source);

            Vector3 worldOffset = source.transform.position - referencePosition;
            preview.transform.position = PreviewWorldOrigin + worldOffset;
            preview.transform.rotation = source.transform.rotation;
            preview.transform.localScale = source.transform.lossyScale;
        }

        private Material GetPreviewMaterial(SpriteRenderer source)
        {
            if (source == null || source.sharedMaterial == null)
            {
                return null;
            }

            int materialId = source.sharedMaterial.GetInstanceID();
            Material cachedMaterial;
            if (previewMaterialCache.TryGetValue(materialId, out cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            Material clone = new Material(source.sharedMaterial);
            clone.hideFlags = HideFlags.HideAndDontSave;
            previewMaterialCache[materialId] = clone;
            return clone;
        }

        private void EnsurePreviewRenderTexture(int width, int height)
        {
            if (previewRenderTexture != null &&
                previewRenderTexture.width == width &&
                previewRenderTexture.height == height)
            {
                return;
            }

            if (previewRenderTexture != null)
            {
                previewRenderTexture.Release();
                Destroy(previewRenderTexture);
                previewRenderTexture = null;
            }

            previewRenderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            previewRenderTexture.hideFlags = HideFlags.HideAndDontSave;
            previewRenderTexture.filterMode = FilterMode.Point;
            previewRenderTexture.wrapMode = TextureWrapMode.Clamp;
            previewRenderTexture.Create();
        }

        private static void GetPreviewTextureSize(float aspect, out int width, out int height)
        {
            aspect = Mathf.Max(0.125f, aspect);

            if (aspect >= 1f)
            {
                width = PreviewMaxTextureSize;
                height = Mathf.Clamp(Mathf.RoundToInt(PreviewMaxTextureSize / aspect), PreviewMinTextureSize, PreviewMaxTextureSize);
            }
            else
            {
                height = PreviewMaxTextureSize;
                width = Mathf.Clamp(Mathf.RoundToInt(PreviewMaxTextureSize * aspect), PreviewMinTextureSize, PreviewMaxTextureSize);
            }
        }

        private static SpriteRenderer GetPreviewSourceRenderer(LimbBehaviour limb)
        {
            if (limb == null)
            {
                return null;
            }

            if (limb.SkinMaterialHandler != null &&
                limb.SkinMaterialHandler.renderer != null &&
                limb.SkinMaterialHandler.renderer.sprite != null)
            {
                return limb.SkinMaterialHandler.renderer;
            }

            if (limb.PhysicalBehaviour != null &&
                limb.PhysicalBehaviour.spriteRenderer != null &&
                limb.PhysicalBehaviour.spriteRenderer.sprite != null)
            {
                return limb.PhysicalBehaviour.spriteRenderer;
            }

            return null;
        }

        private void RefreshActionButtons()
        {
            LimbSpriteState state = GetSelectedState();
            bool hasState = state != null;
            bool isCharacter = currentTarget != null;
            bool isObject = currentObjectTarget != null;
            bool canUndo = hasState && state.CanUndo;
            bool canRedo = hasState && state.CanRedo;
            bool canResetLimb = hasState && state.CanResetToOriginal;
            bool canResetCharacter = currentTarget != null
                ? currentTarget.HasAnyResettableChanges()
                : currentObjectTarget != null && currentObjectTarget.HasResettableChanges();
            bool canPastePart = hasState && SpriteClipboardService.HasPartClipboard(isCharacter);
            bool canPasteTarget = isCharacter && SpriteClipboardService.HasTargetClipboard(true);

            if (headerImportPngButton != null)
            {
                bool canImportPng = hasState && !nativePngFileDialogOpen;
                headerImportPngButton.interactable = canImportPng;
                SetButtonLabelColor(headerImportPngButton, canImportPng ? TextColor : MutedTextColor);
                SetButtonIconColor(headerImportPngButton, canImportPng ? (Color)IconTintColor : MutedTextColor);
            }

            if (headerExportPngButton != null)
            {
                headerExportPngButton.interactable = hasState;
                SetButtonLabelColor(headerExportPngButton, hasState ? TextColor : MutedTextColor);
                SetButtonIconColor(headerExportPngButton, hasState ? (Color)IconTintColor : MutedTextColor);
            }

            if (headerExportBodyPngButton != null)
            {
                bool canExportBody = hasState && isCharacter;
                headerExportBodyPngButton.gameObject.SetActive(isCharacter);
                headerExportBodyPngButton.interactable = canExportBody;
                SetButtonLabelColor(headerExportBodyPngButton, canExportBody ? TextColor : MutedTextColor);
                SetButtonIconColor(headerExportBodyPngButton, canExportBody ? (Color)IconTintColor : MutedTextColor);
            }

            if (resizeCanvasButton != null)
            {
                resizeCanvasButton.interactable = hasState;
                SetButtonLabelColor(resizeCanvasButton, hasState ? TextColor : MutedTextColor);
            }

            if (expandCanvas1Button != null)
            {
                expandCanvas1Button.interactable = hasState;
                SetButtonLabelColor(expandCanvas1Button, hasState ? TextColor : MutedTextColor);
            }

            if (expandCanvas2Button != null)
            {
                expandCanvas2Button.interactable = hasState;
                SetButtonLabelColor(expandCanvas2Button, hasState ? TextColor : MutedTextColor);
            }

            if (expandCanvas4Button != null)
            {
                expandCanvas4Button.interactable = hasState;
                SetButtonLabelColor(expandCanvas4Button, hasState ? TextColor : MutedTextColor);
            }

            if (shrinkCanvas1Button != null)
            {
                shrinkCanvas1Button.interactable = hasState;
                SetButtonLabelColor(shrinkCanvas1Button, hasState ? TextColor : MutedTextColor);
            }

            if (undoButton != null)
            {
                undoButton.interactable = canUndo;
                SetButtonLabelColor(undoButton, canUndo ? TextColor : MutedTextColor);
            }

            if (redoButton != null)
            {
                redoButton.interactable = canRedo;
                SetButtonLabelColor(redoButton, canRedo ? TextColor : MutedTextColor);
            }

            if (resetLimbButton != null)
            {
                resetLimbButton.interactable = canResetLimb;
                SetButtonLabelColor(resetLimbButton, canResetLimb ? TextColor : MutedTextColor);
                SetButtonLabel(resetLimbButton, currentObjectTarget != null ? "Reset Sprite" : "Reset Limb");
            }

            if (resetCharacterButton != null)
            {
                resetCharacterButton.interactable = canResetCharacter;
                SetButtonLabelColor(resetCharacterButton, canResetCharacter ? TextColor : MutedTextColor);
                SetButtonLabel(resetCharacterButton, currentObjectTarget != null ? "Reset Object" : "Reset Character");
            }

            if (copyPartButton != null)
            {
                copyPartButton.interactable = hasState;
                SetButtonLabelColor(copyPartButton, hasState ? TextColor : MutedTextColor);
                SetButtonLabel(copyPartButton, isObject ? "Copy Sprite" : "Copy Limb");
            }

            if (pastePartButton != null)
            {
                pastePartButton.interactable = canPastePart;
                SetButtonLabelColor(pastePartButton, canPastePart ? TextColor : MutedTextColor);
                SetButtonLabel(pastePartButton, isObject ? "Paste Sprite" : "Paste Limb");
            }

            if (targetClipboardRowObject != null)
            {
                targetClipboardRowObject.SetActive(true);
            }

            if (copyTargetButton != null)
            {
                copyTargetButton.gameObject.SetActive(isCharacter);
                copyTargetButton.interactable = isCharacter;
                SetButtonLabelColor(copyTargetButton, isCharacter ? TextColor : MutedTextColor);
                SetButtonLabel(copyTargetButton, "Copy Body");
            }

            if (pasteTargetButton != null)
            {
                pasteTargetButton.gameObject.SetActive(isCharacter);
                pasteTargetButton.interactable = canPasteTarget;
                SetButtonLabelColor(pasteTargetButton, canPasteTarget ? TextColor : MutedTextColor);
                SetButtonLabel(pasteTargetButton, "Paste Body");
            }
        }

        private void RefreshLayerButtons()
        {
            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                activeEditorLayerIndex = state.ActiveLayerIndex;
                activeEditorLayer = GetLayerKindFromIndex(state.ActiveLayerIndex);
            }

            RebuildLayerRows(state);
        }

        private void RebuildLayerRows(LimbSpriteState state)
        {
            if (layerButtonContainer == null)
            {
                return;
            }

            for (int i = layerButtonContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(layerButtonContainer.GetChild(i).gameObject);
            }

            layerRowButtons.Clear();
            int count = state != null ? state.LayerCount : 0;
            for (int i = 0; i < count; i++)
            {
                Button row = CreateLayerRow(layerButtonContainer, i, state.GetLayerName(i), activeEditorLayerIndex == i);
                RefreshLayerButton(row, i, state);
                layerRowButtons.Add(row);
            }
        }

        private void RefreshLayerButton(Button rowButton, int index, LimbSpriteState state)
        {
            bool selected = activeEditorLayerIndex == index;
            bool visible = state != null && state.IsLayerVisible(index);

            if (rowButton == null)
            {
                return;
            }

            rowButton.interactable = state != null;
            StyleButton(rowButton, selected, false);

            TextMeshProUGUI[] labels = rowButton.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && string.Equals(labels[i].gameObject.name, "LayerLabel", StringComparison.Ordinal))
                {
                    labels[i].text = GetLayerLabel(index);
                    labels[i].color = visible ? TextColor : DisabledTextColor;
                }
            }

            Transform eyeTransform = rowButton.transform.Find("Eye");
            Button eyeButton = eyeTransform != null ? eyeTransform.GetComponent<Button>() : null;
            if (eyeButton == null)
            {
                return;
            }

            Image image = eyeButton.GetComponent<Image>();
            if (image != null)
            {
                Color fill = visible
                    ? (selected ? AccentSoftColor : ButtonColor)
                    : (Color)new Color32(18, 18, 18, 255);
                Color border = visible
                    ? (Color)ButtonBorderColor
                    : (Color)new Color32(46, 46, 46, 255);
                ApplyRoundedImage(image, fill, border, CornerRadius, 1);
            }

            SetButtonIconColor(eyeButton, visible ? TextColor : DisabledTextColor);
        }

        private void CopySelectedSprite()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.CopyCharacterPart(currentTarget, currentLimb)
                : currentObjectTarget != null ? SpriteClipboardService.CopyObjectPart(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void PasteSelectedSprite()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.PasteCharacterPart(currentTarget, currentLimb)
                : currentObjectTarget != null ? SpriteClipboardService.PasteObjectPart(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void CopyWholeTarget()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.CopyCharacterTarget(currentTarget)
                : currentObjectTarget != null ? SpriteClipboardService.CopyObjectPart(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void PasteWholeTarget()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.PasteCharacterTarget(currentTarget, currentLimb)
                : currentObjectTarget != null ? SpriteClipboardService.PasteObjectPart(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void CopyWholeTargetLayers()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.CopyCharacterTargetLayers(currentTarget)
                : currentObjectTarget != null ? SpriteClipboardService.CopyObjectLayers(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void PasteWholeTargetLayers()
        {
            SpritePresetOperationResult result = currentTarget != null
                ? SpriteClipboardService.PasteCharacterTargetLayers(currentTarget, currentLimb)
                : currentObjectTarget != null ? SpriteClipboardService.PasteObjectLayers(currentObjectTarget) : null;

            HandleClipboardOperation(result);
        }

        private void HandleClipboardOperation(SpritePresetOperationResult result)
        {
            if (result == null)
            {
                return;
            }

            DialogBoxManager.Notification(result.Message);
            RefreshAll();
        }

        private void RefreshInfo()
        {
            if (infoText == null)
            {
                return;
            }

            LimbSpriteState state = GetSelectedState();
            string targetName = currentTarget != null
                ? currentTarget.GetCharacterName()
                : currentObjectTarget != null ? currentObjectTarget.GetObjectName() : "Object";
            string selectionName = GetCurrentSelectionName();
            targetName = StripRichTextTags(targetName);
            selectionName = StripRichTextTags(selectionName);
            string history = state != null ? state.HistoryDescription : "0 / 0";
            string dirty = state != null && state.HasChanges ? "unsaved" : "saved";
            string hitbox = state != null && state.HasPendingHitboxChanges() ? "pending" : "synced";
            string size = state != null ? state.Width + " x " + state.Height + " px" : "none";
            string color = GetHexString((Color32)selectedColor) + "  " + Mathf.RoundToInt(selectedColor.a * 100f) + "%";

            infoText.text =
                "Target: " + targetName + "\n" +
                (currentObjectTarget != null ? "Sprite: " : "Limb: ") + selectionName + "\n" +
                "Size: " + size + "\n" +
                "Tool: " + GetToolLabel(currentTool) + "\n" +
                "Brush: " + brushSize + " px\n" +
                "Color: " + color + "\n" +
                "History: " + history + "\n" +
                "Changes: " + dirty + "\n" +
                "Hitbox: " + hitbox;
        }

        private void SyncColorSliders()
        {
            suppressColorCallbacks = true;

            if (opacitySlider != null) { opacitySlider.value = selectedColor.a; }

            suppressColorCallbacks = false;
        }

        private void OpenNativeColorPicker()
        {
            if (nativeColorPickerOpen)
            {
                return;
            }

            ColorpickerDialogBehaviour picker = ColorpickerDialogBehaviour.Instance;
            if (picker == null)
            {
                RefreshColorUi();
                return;
            }

            try
            {
                nativeColorPickerOpen = true;

                if (picker.Title != null)
                {
                    picker.Title.text = "Brush Color";
                }

                if (picker.Description != null)
                {
                    picker.Description.text = "Choose a brush color. Opacity stays in the Sprite Editor sidebar.";
                }

                picker.OnApply.RemoveAllListeners();
                picker.OnCancel.RemoveAllListeners();

                picker.OnApply.AddListener(delegate(Color pickedColor)
                {
                    nativeColorPickerOpen = false;
                    SetSelectedColor(new Color(pickedColor.r, pickedColor.g, pickedColor.b, selectedColor.a), true);
                    if (uiRoot != null)
                    {
                        uiRoot.transform.SetAsLastSibling();
                    }
                });

                picker.OnCancel.AddListener(delegate
                {
                    nativeColorPickerOpen = false;
                    if (uiRoot != null)
                    {
                        uiRoot.transform.SetAsLastSibling();
                    }
                    RefreshColorUi();
                });

                picker.Open(new Color(selectedColor.r, selectedColor.g, selectedColor.b, 1f));

                if (picker.transform.parent != null)
                {
                    picker.transform.parent.SetAsLastSibling();
                }

                picker.transform.SetAsLastSibling();
                RefreshColorUi();
            }
            catch (Exception exception)
            {
                nativeColorPickerOpen = false;

                if (uiRoot != null)
                {
                    uiRoot.transform.SetAsLastSibling();
                }

                Debug.LogError("[DB Singularity] Failed to open native color picker: " + exception);
                RefreshColorUi();
            }
        }

        private void CloseNativeColorPickerIfNeeded()
        {
            if (!nativeColorPickerOpen)
            {
                return;
            }

            nativeColorPickerOpen = false;

            if (uiRoot != null)
            {
                uiRoot.transform.SetAsLastSibling();
            }

            ColorpickerDialogBehaviour picker = ColorpickerDialogBehaviour.Instance;
            if (picker == null)
            {
                return;
            }

            picker.OnApply.RemoveAllListeners();
            picker.OnCancel.RemoveAllListeners();
            picker.Close();
        }

        private static bool IsNativeColorPickerAvailable()
        {
            return ColorpickerDialogBehaviour.Instance != null;
        }

        private void SyncBrushSlider()
        {
            if (brushSlider != null)
            {
                brushSlider.value = brushSize;
            }
        }

        private void SyncUiScaleSlider()
        {
            suppressUiScaleCallbacks = true;

            if (uiScaleSlider != null)
            {
                uiScaleSlider.value = uiScale;
            }

            if (uiScaleText != null)
            {
                uiScaleText.text = "Scale " + Mathf.RoundToInt(uiScale * 100f) + "%";
            }

            suppressUiScaleCallbacks = false;
        }

        private void SetUiScale(float value)
        {
            if (suppressUiScaleCallbacks)
            {
                return;
            }

            uiScale = Mathf.Clamp(value, UiScaleMin, UiScaleMax);
            PlayerPrefs.SetFloat(UiScalePrefsKey, uiScale);
            PlayerPrefs.Save();
            SyncUiScaleSlider();
            UpdateWindowSize();
        }

        private void UpdateWindowSize()
        {
            if (windowRect == null)
            {
                return;
            }

            Vector2 availableSize = GetAvailableUiSize();
            float availableWidth = Mathf.Max(600f, availableSize.x - 28f);
            float availableHeight = Mathf.Max(420f, availableSize.y - 28f);
            float maxFitScale = Mathf.Min(availableWidth / WindowDesignWidth, availableHeight / WindowDesignHeight);
            float scale = Mathf.Clamp(uiScale, UiScaleMin, UiScaleMax);
            scale = Mathf.Clamp(Mathf.Min(scale, maxFitScale), 0.45f, UiScaleMax);

            windowRect.sizeDelta = new Vector2(WindowDesignWidth, WindowDesignHeight);
            if (panelHostRect != null)
            {
                panelHostRect.localScale = new Vector3(scale, scale, 1f);
                windowRect.localScale = Vector3.one;
            }
            else
            {
                windowRect.localScale = new Vector3(scale, scale, 1f);
            }

            RefreshUiTextMeshes();
        }

        private void RefreshUiTextMeshes()
        {
            if (uiRoot == null)
            {
                return;
            }

            TextMeshProUGUI[] labels = uiRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                {
                    labels[i].ForceMeshUpdate(true);
                }
            }
        }

        private Vector2 GetAvailableUiSize()
        {
            RectTransform rootRect = uiRoot != null ? uiRoot.GetComponent<RectTransform>() : null;
            if (rootRect != null && rootRect.rect.width > 0f && rootRect.rect.height > 0f)
            {
                return rootRect.rect.size;
            }

            Canvas canvas = windowRect != null ? windowRect.GetComponentInParent<Canvas>() : null;
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        public void HandleCanvasPointerDown(PointerEventData eventData)
        {
            if (presetModalOpen || IsResizeModalOpen() || IsLayerRenameModalOpen() || IsKeybindModalOpen())
            {
                return;
            }

            if (canvasResizeMode)
            {
                return;
            }

            if (eventData != null && eventData.button == PointerEventData.InputButton.Middle)
            {
                BeginCanvasPan(eventData);
                return;
            }

            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                return;
            }

            Vector2Int pixel;
            if (!TryGetPixelFromPointer(eventData, state, out pixel))
            {
                return;
            }

            if (currentTool == ToolMode.Select)
            {
                bool shiftSelect = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrlSelect = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (shiftSelect && !ctrlSelect)
                {
                    FillPixelSelection(pixel);
                    return;
                }

                BeginMarqueeSelection(pixel);
                return;
            }

            if (currentTool == ToolMode.Move)
            {
                if (!CanEditActiveLayer(true))
                {
                    return;
                }

                BeginMoveSelection(state, pixel);
                return;
            }

            if (currentTool == ToolMode.Eyedropper)
            {
                EndActiveStroke();
                SetSelectedColor(state.GetPixel(pixel.x, pixel.y), true);
                SetTool(ToolMode.Brush);
                SyncColorSliders();
                RefreshStatusText();
                return;
            }

            if (currentTool == ToolMode.Fill)
            {
                if (!CanEditActiveLayer(true))
                {
                    return;
                }

                EndActiveStroke();
                if (state.FloodFill(pixel.x, pixel.y, selectedColor))
                {
                    state.CommitCurrentPixels();
                    RefreshAll();
                }
                return;
            }

            if (!CanEditActiveLayer(true))
            {
                return;
            }

            EndActiveStroke();
            activeStrokeState = state;
            activeStrokeState.BeginStroke();
            previousPixel = pixel;
            strokeInProgress = true;
            strokeChanged = false;
            paintOpaqueOnlyStroke = eventData != null &&
                                    eventData.button == PointerEventData.InputButton.Right &&
                                    currentTool == ToolMode.Brush;

            if (ApplyPaint(activeStrokeState, pixel))
            {
                strokeChanged = true;
                RefreshTextures();
            }
        }

        public void HandleCanvasDrag(PointerEventData eventData)
        {
            if (presetModalOpen || IsResizeModalOpen() || IsLayerRenameModalOpen() || IsKeybindModalOpen())
            {
                return;
            }

            if (panInProgress)
            {
                UpdateCanvasPan(eventData);
                return;
            }

            if (selectionDragInProgress)
            {
                LimbSpriteState selectionState = GetSelectedState();
                Vector2Int selectionPixel;
                if (selectionState != null && TryGetPixelFromPointer(eventData, selectionState, out selectionPixel))
                {
                    UpdateMarqueeSelection(selectionPixel);
                }

                return;
            }

            if (moveSelectionInProgress)
            {
                LimbSpriteState moveState = GetSelectedState();
                Vector2Int movePixel;
                if (moveState != null && TryGetPixelFromPointer(eventData, moveState, out movePixel))
                {
                    UpdateMoveSelection(moveState, movePixel);
                }

                return;
            }

            if (!strokeInProgress)
            {
                return;
            }

            LimbSpriteState state = activeStrokeState ?? GetSelectedState();
            if (state == null)
            {
                return;
            }

            Vector2Int pixel;
            if (!TryGetPixelFromPointer(eventData, state, out pixel))
            {
                return;
            }

            if (ApplyPaint(state, pixel))
            {
                strokeChanged = true;
                RefreshTextures();
            }
        }

        public void HandleCanvasPointerUp(PointerEventData eventData)
        {
            if (presetModalOpen || IsResizeModalOpen() || IsLayerRenameModalOpen() || IsKeybindModalOpen())
            {
                return;
            }

            if (panInProgress)
            {
                panInProgress = false;
                return;
            }

            if (selectionDragInProgress)
            {
                selectionDragInProgress = false;
                RefreshSelectionOverlay();
                return;
            }

            if (moveSelectionInProgress)
            {
                CommitMoveSelection();
                return;
            }

            LimbSpriteState state = activeStrokeState ?? GetSelectedState();

            if (strokeInProgress && strokeChanged && state != null)
            {
                state.CommitCurrentPixels();
            }

            EndActiveStroke();
            strokeInProgress = false;
            strokeChanged = false;
            paintOpaqueOnlyStroke = false;
            RefreshAll();
        }

        public void HandleCanvasScroll(PointerEventData eventData)
        {
            if (presetModalOpen || IsResizeModalOpen() || IsLayerRenameModalOpen() || IsKeybindModalOpen() || eventData == null || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }

            float multiplier = eventData.scrollDelta.y > 0f ? 1.12f : 1f / 1.12f;
            canvasZoom = Mathf.Clamp(canvasZoom * multiplier, CanvasZoomMin, CanvasZoomMax);
            ApplyCanvasZoom();
            RefreshStatusText();
        }

        private bool IsResizeModalOpen()
        {
            return resizeModalRoot != null && resizeModalRoot.activeSelf;
        }

        private bool IsLayerRenameModalOpen()
        {
            return layerRenameModalRoot != null && layerRenameModalRoot.activeSelf;
        }

        private bool IsKeybindModalOpen()
        {
            return keybindModalRoot != null && keybindModalRoot.activeSelf;
        }

        private void ApplyCanvasZoom()
        {
            if (canvasImage == null)
            {
                return;
            }

            canvasImage.rectTransform.localScale = new Vector3(canvasZoom, canvasZoom, 1f);
            canvasImage.rectTransform.anchoredPosition = canvasPanOffset;
            RefreshSelectionOverlay();
        }

        private void BeginCanvasPan(PointerEventData eventData)
        {
            if (eventData == null || canvasHolderRect == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasHolderRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            FinalizeActiveStroke();
            panStartLocalPoint = localPoint;
            panStartOffset = canvasPanOffset;
            panInProgress = true;
        }

        private void UpdateCanvasPan(PointerEventData eventData)
        {
            if (eventData == null || canvasHolderRect == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasHolderRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            canvasPanOffset = panStartOffset + localPoint - panStartLocalPoint;
            ApplyCanvasZoom();
        }

        private void BeginMarqueeSelection(Vector2Int pixel)
        {
            FinalizeActiveStroke();
            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                return;
            }

            EnsureSelectionMask(state);
            marqueeAdds = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            marqueeSubtracts = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (marqueeAdds && marqueeSubtracts)
            {
                marqueeSubtracts = false;
            }

            if (!marqueeAdds && !marqueeSubtracts)
            {
                Array.Clear(selectedPixelMask, 0, selectedPixelMask.Length);
                marqueeBaseMask = new bool[selectedPixelMask.Length];
            }
            else
            {
                marqueeBaseMask = CloneSelectionMask();
            }

            selectionStartPixel = pixel;
            previousSelectionPixel = pixel;
            selectionDragInProgress = true;
            ApplyMarqueeRect(pixel);
        }

        private void UpdateMarqueeSelection(Vector2Int pixel)
        {
            previousSelectionPixel = pixel;
            ApplyMarqueeRect(pixel);
        }

        private void ApplyMarqueeRect(Vector2Int endPixel)
        {
            if (selectedPixelMask == null || marqueeBaseMask == null || marqueeBaseMask.Length != selectedPixelMask.Length)
            {
                return;
            }

            Array.Copy(marqueeBaseMask, selectedPixelMask, selectedPixelMask.Length);

            int minX = Mathf.Clamp(Mathf.Min(selectionStartPixel.x, endPixel.x), 0, selectedPixelMaskWidth - 1);
            int maxX = Mathf.Clamp(Mathf.Max(selectionStartPixel.x, endPixel.x), 0, selectedPixelMaskWidth - 1);
            int minY = Mathf.Clamp(Mathf.Min(selectionStartPixel.y, endPixel.y), 0, selectedPixelMaskHeight - 1);
            int maxY = Mathf.Clamp(Mathf.Max(selectionStartPixel.y, endPixel.y), 0, selectedPixelMaskHeight - 1);

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * selectedPixelMaskWidth;
                for (int x = minX; x <= maxX; x++)
                {
                    int index = row + x;
                    if (marqueeSubtracts)
                    {
                        selectedPixelMask[index] = false;
                    }
                    else
                    {
                        selectedPixelMask[index] = true;
                    }
                }
            }

            RecalculateSelectionBounds();
            RefreshSelectionOverlay();
        }

        private void FillPixelSelection(Vector2Int startPixel)
        {
            FinalizeActiveStroke();
            LimbSpriteState state = GetSelectedState();
            if (state == null)
            {
                return;
            }

            EnsureSelectionMask(state);
            if (selectedPixelMask == null ||
                startPixel.x < 0 ||
                startPixel.y < 0 ||
                startPixel.x >= selectedPixelMaskWidth ||
                startPixel.y >= selectedPixelMaskHeight ||
                IsMaskSelected(startPixel.x, startPixel.y))
            {
                return;
            }

            bool[] visited = new bool[selectedPixelMask.Length];
            List<Vector2Int> pending = new List<Vector2Int>();
            pending.Add(startPixel);
            visited[startPixel.x + startPixel.y * selectedPixelMaskWidth] = true;

            int cursor = 0;
            while (cursor < pending.Count)
            {
                Vector2Int pixel = pending[cursor++];
                selectedPixelMask[pixel.x + pixel.y * selectedPixelMaskWidth] = true;
                AddSelectionFillNeighbor(pixel.x + 1, pixel.y, visited, pending);
                AddSelectionFillNeighbor(pixel.x - 1, pixel.y, visited, pending);
                AddSelectionFillNeighbor(pixel.x, pixel.y + 1, visited, pending);
                AddSelectionFillNeighbor(pixel.x, pixel.y - 1, visited, pending);
            }

            RecalculateSelectionBounds();
            RefreshSelectionOverlay();
        }

        private void AddSelectionFillNeighbor(int x, int y, bool[] visited, List<Vector2Int> pending)
        {
            if (selectedPixelMask == null ||
                x < 0 ||
                y < 0 ||
                x >= selectedPixelMaskWidth ||
                y >= selectedPixelMaskHeight)
            {
                return;
            }

            int index = x + y * selectedPixelMaskWidth;
            if (visited[index] || selectedPixelMask[index])
            {
                return;
            }

            visited[index] = true;
            pending.Add(new Vector2Int(x, y));
        }

        private void BeginMoveSelection(LimbSpriteState state, Vector2Int pixel)
        {
            if (state == null || !hasPixelSelection || !IsPixelSelected(pixel))
            {
                return;
            }

            Color32[] selectedPixels;
            int width;
            int height;
            RectInt bounds;
            if (!state.TryCopyActiveLayerPixels(selectedPixelMask, selectedPixelMaskWidth, selectedPixelMaskHeight, out selectedPixels, out width, out height, out bounds))
            {
                return;
            }

            moveBasePixels = state.GetActiveLayerPixelsSnapshot();
            if (moveBasePixels == null)
            {
                return;
            }

            movingSelectionPixels = selectedPixels;
            movingSelectionMask = CloneSelectionMask();
            movingSelectionMaskWidth = selectedPixelMaskWidth;
            movingSelectionMaskHeight = selectedPixelMaskHeight;
            moveStartPixel = pixel;
            moveStartRectPosition = new Vector2Int(bounds.x, bounds.y);
            selectedPixelRect = bounds;
            moveSelectionInProgress = true;
            state.PreviewMoveActiveLayerPixels(moveBasePixels, movingSelectionMask, movingSelectionMaskWidth, movingSelectionMaskHeight, selectedPixelRect, movingSelectionPixels, width, height, selectedPixelRect.x, selectedPixelRect.y);
            RefreshTextures();
            RefreshSelectionOverlay();
        }

        private void UpdateMoveSelection(LimbSpriteState state, Vector2Int pixel)
        {
            if (!moveSelectionInProgress || state == null || movingSelectionPixels == null || moveBasePixels == null)
            {
                return;
            }

            int nextX = moveStartRectPosition.x + pixel.x - moveStartPixel.x;
            int nextY = moveStartRectPosition.y + pixel.y - moveStartPixel.y;
            selectedPixelRect = new RectInt(nextX, nextY, selectedPixelRect.width, selectedPixelRect.height);
            state.PreviewMoveActiveLayerPixels(moveBasePixels, movingSelectionMask, movingSelectionMaskWidth, movingSelectionMaskHeight, new RectInt(moveStartRectPosition.x, moveStartRectPosition.y, selectedPixelRect.width, selectedPixelRect.height), movingSelectionPixels, selectedPixelRect.width, selectedPixelRect.height, nextX, nextY);
            OffsetSelectionMask(nextX - moveStartRectPosition.x, nextY - moveStartRectPosition.y);
            RefreshTextures();
            RefreshSelectionOverlay();
        }

        private void CommitMoveSelection()
        {
            LimbSpriteState state = GetSelectedState();
            if (state != null)
            {
                state.CommitCurrentPixels();
            }

            moveSelectionInProgress = false;
            movingSelectionPixels = null;
            moveBasePixels = null;
            movingSelectionMask = null;
            RefreshAll();
        }

        private void ClearSelectionState()
        {
            hasPixelSelection = false;
            selectionDragInProgress = false;
            moveSelectionInProgress = false;
            movingSelectionPixels = null;
            moveBasePixels = null;
            movingSelectionMask = null;
            marqueeBaseMask = null;
            selectedPixelMask = null;
            selectedPixelMaskWidth = 0;
            selectedPixelMaskHeight = 0;
            RefreshSelectionOverlay();
        }

        private void RefreshSelectionOverlay()
        {
            if (selectionOverlayRect == null || selectionOverlayRawImage == null)
            {
                return;
            }

            LimbSpriteState state = GetSelectedState();
            if (!hasPixelSelection || state == null || state.Width <= 0 || state.Height <= 0 || selectedPixelMask == null)
            {
                selectionOverlayRawImage.gameObject.SetActive(false);
                return;
            }

            EnsureSelectionOverlayTexture(state.Width, state.Height);
            if (selectionOverlayTexture == null)
            {
                selectionOverlayRawImage.gameObject.SetActive(false);
                return;
            }

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 fill = new Color32(89, 140, 217, 72);
            Color32 edge = new Color32(140, 175, 230, 235);
            Color32[] pixels = new Color32[state.Width * state.Height];
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    int index = x + y * state.Width;
                    if (index >= selectedPixelMask.Length || !selectedPixelMask[index])
                    {
                        pixels[index] = clear;
                        continue;
                    }

                    bool border = !IsMaskSelected(x - 1, y) || !IsMaskSelected(x + 1, y) || !IsMaskSelected(x, y - 1) || !IsMaskSelected(x, y + 1);
                    pixels[index] = border ? edge : fill;
                }
            }

            selectionOverlayTexture.SetPixels32(pixels);
            selectionOverlayTexture.Apply();
            selectionOverlayRawImage.texture = selectionOverlayTexture;
            selectionOverlayRawImage.gameObject.SetActive(true);
        }

        private void EnsureSelectionMask(LimbSpriteState state)
        {
            if (state == null || state.Width <= 0 || state.Height <= 0)
            {
                selectedPixelMask = null;
                selectedPixelMaskWidth = 0;
                selectedPixelMaskHeight = 0;
                return;
            }

            int length = state.Width * state.Height;
            if (selectedPixelMask == null || selectedPixelMaskWidth != state.Width || selectedPixelMaskHeight != state.Height || selectedPixelMask.Length != length)
            {
                selectedPixelMask = new bool[length];
                selectedPixelMaskWidth = state.Width;
                selectedPixelMaskHeight = state.Height;
                hasPixelSelection = false;
            }
        }

        private bool IsPixelSelected(Vector2Int pixel)
        {
            return IsMaskSelected(pixel.x, pixel.y);
        }

        private bool IsMaskSelected(int x, int y)
        {
            if (selectedPixelMask == null || x < 0 || y < 0 || x >= selectedPixelMaskWidth || y >= selectedPixelMaskHeight)
            {
                return false;
            }

            return selectedPixelMask[x + y * selectedPixelMaskWidth];
        }

        private void PaintSelectionLine(Vector2Int from, Vector2Int to, bool selected)
        {
            if (selectedPixelMask == null)
            {
                return;
            }

            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetSelectionPixel(x0, y0, selected);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void SetSelectionPixel(int x, int y, bool selected)
        {
            if (selectedPixelMask == null || x < 0 || y < 0 || x >= selectedPixelMaskWidth || y >= selectedPixelMaskHeight)
            {
                return;
            }

            selectedPixelMask[x + y * selectedPixelMaskWidth] = selected;
        }

        private bool RecalculateSelectionBounds()
        {
            if (selectedPixelMask == null)
            {
                hasPixelSelection = false;
                selectedPixelRect = new RectInt(0, 0, 0, 0);
                return false;
            }

            int minX = selectedPixelMaskWidth;
            int minY = selectedPixelMaskHeight;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < selectedPixelMaskHeight; y++)
            {
                for (int x = 0; x < selectedPixelMaskWidth; x++)
                {
                    if (!selectedPixelMask[x + y * selectedPixelMaskWidth])
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                hasPixelSelection = false;
                selectedPixelRect = new RectInt(0, 0, 0, 0);
                return false;
            }

            selectedPixelRect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            hasPixelSelection = true;
            return true;
        }

        private bool[] CloneSelectionMask()
        {
            if (selectedPixelMask == null)
            {
                return null;
            }

            bool[] clone = new bool[selectedPixelMask.Length];
            Array.Copy(selectedPixelMask, clone, selectedPixelMask.Length);
            return clone;
        }

        private void OffsetSelectionMask(int offsetX, int offsetY)
        {
            if (movingSelectionMask == null || selectedPixelMask == null)
            {
                return;
            }

            Array.Clear(selectedPixelMask, 0, selectedPixelMask.Length);
            for (int y = 0; y < movingSelectionMaskHeight; y++)
            {
                for (int x = 0; x < movingSelectionMaskWidth; x++)
                {
                    int sourceIndex = x + y * movingSelectionMaskWidth;
                    if (!movingSelectionMask[sourceIndex])
                    {
                        continue;
                    }

                    int destinationX = x + offsetX;
                    int destinationY = y + offsetY;
                    if (destinationX < 0 || destinationY < 0 || destinationX >= selectedPixelMaskWidth || destinationY >= selectedPixelMaskHeight)
                    {
                        continue;
                    }

                    selectedPixelMask[destinationX + destinationY * selectedPixelMaskWidth] = true;
                }
            }

            RecalculateSelectionBounds();
        }

        private void EnsureSelectionOverlayTexture(int width, int height)
        {
            if (selectionOverlayTexture != null && selectionOverlayTexture.width == width && selectionOverlayTexture.height == height)
            {
                return;
            }

            if (selectionOverlayTexture != null)
            {
                Destroy(selectionOverlayTexture);
                generatedUiTextures.Remove(selectionOverlayTexture);
                selectionOverlayTexture = null;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            selectionOverlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            selectionOverlayTexture.filterMode = FilterMode.Point;
            selectionOverlayTexture.wrapMode = TextureWrapMode.Clamp;
            generatedUiTextures.Add(selectionOverlayTexture);
        }

        private void CopyPixelSelection(bool cut)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !hasPixelSelection)
            {
                return;
            }

            if (cut && !CanEditActiveLayer(true))
            {
                return;
            }

            Color32[] copiedPixels;
            int width;
            int height;
            RectInt bounds;
            if (!state.TryCopyPixels(selectedPixelMask, selectedPixelMaskWidth, selectedPixelMaskHeight, out copiedPixels, out width, out height, out bounds))
            {
                return;
            }

            pixelClipboard = copiedPixels;
            pixelClipboardWidth = width;
            pixelClipboardHeight = height;

            if (cut)
            {
                ClearPixelSelectionPixels(false);
            }
        }

        private void ClearPixelSelectionPixels(bool showMessage)
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || !hasPixelSelection)
            {
                return;
            }

            if (!CanEditActiveLayer(showMessage))
            {
                return;
            }

            if (state.ClearPixels(selectedPixelMask, selectedPixelMaskWidth, selectedPixelMaskHeight, true))
            {
                if (showMessage)
                {
                    DialogBoxManager.Notification("Cleared selected pixels.");
                }

                RefreshAll();
            }
        }

        private void PastePixelSelection()
        {
            LimbSpriteState state = GetSelectedState();
            if (state == null || pixelClipboard == null || pixelClipboardWidth <= 0 || pixelClipboardHeight <= 0)
            {
                return;
            }

            if (!CanEditActiveLayer(true))
            {
                return;
            }

            int x = Mathf.Clamp((state.Width - pixelClipboardWidth) / 2, -pixelClipboardWidth + 1, Mathf.Max(0, state.Width - 1));
            int y = Mathf.Clamp((state.Height - pixelClipboardHeight) / 2, -pixelClipboardHeight + 1, Mathf.Max(0, state.Height - 1));
            RectInt pastedBounds;
            if (!state.PastePixelsAsLayer(pixelClipboard, pixelClipboardWidth, pixelClipboardHeight, x, y, out pastedBounds))
            {
                return;
            }

            EnsureSelectionMask(state);
            Array.Clear(selectedPixelMask, 0, selectedPixelMask.Length);
            for (int py = 0; py < pixelClipboardHeight; py++)
            {
                int destinationY = y + py;
                if (destinationY < 0 || destinationY >= selectedPixelMaskHeight)
                {
                    continue;
                }

                for (int px = 0; px < pixelClipboardWidth; px++)
                {
                    int destinationX = x + px;
                    if (destinationX < 0 || destinationX >= selectedPixelMaskWidth)
                    {
                        continue;
                    }

                    if (pixelClipboard[px + py * pixelClipboardWidth].a > 0)
                    {
                        selectedPixelMask[destinationX + destinationY * selectedPixelMaskWidth] = true;
                    }
                }
            }

            selectedPixelRect = pastedBounds;
            hasPixelSelection = true;
            activeEditorLayerIndex = state.ActiveLayerIndex;
            activeEditorLayer = GetLayerKindFromIndex(state.ActiveLayerIndex);
            SetTool(ToolMode.Move);
            RefreshAll();
        }

        private bool ApplyPaint(LimbSpriteState state, Vector2Int pixel)
        {
            bool erase = currentTool == ToolMode.Eraser;
            bool changed = state.PaintLine(previousPixel, pixel, selectedColor, brushSize, erase, paintOpaqueOnlyStroke);
            previousPixel = pixel;
            return changed;
        }

        private bool TryGetPixelFromPointer(PointerEventData eventData, LimbSpriteState state, out Vector2Int pixel)
        {
            pixel = Vector2Int.zero;

            if (canvasImage == null || state == null)
            {
                return false;
            }

            RectTransform rect = canvasImage.rectTransform;
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return false;
            }

            Rect bounds = rect.rect;
            float normalizedX = Mathf.Clamp01((localPoint.x - bounds.x) / bounds.width);
            float normalizedY = Mathf.Clamp01((localPoint.y - bounds.y) / bounds.height);

            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * state.Width), 0, state.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedY * state.Height), 0, state.Height - 1);
            pixel = new Vector2Int(x, y);
            return true;
        }

        private LimbSpriteState GetSelectedState()
        {
            if (currentTarget != null)
            {
                if (currentTarget.IsAccessorySelected)
                {
                    return currentTarget.GetAccessoryState(currentTarget.SelectedAccessory);
                }

                return currentTarget.GetState(currentLimb);
            }

            if (currentObjectTarget != null)
            {
                return currentObjectTarget.GetState();
            }

            return null;
        }

        private string GetCurrentSelectionName()
        {
            if (currentTarget != null)
            {
                if (currentTarget.IsAccessorySelected)
                {
                    return "Hair";
                }

                return currentTarget.GetLimbDisplayName(currentLimb);
            }

            if (currentObjectTarget != null)
            {
                return currentObjectTarget.GetPartLabel();
            }

            return "Unknown";
        }

        private static void SetButtonLabel(Button button, string content)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = content;
            }
        }

        private static void SetButtonFontSize(Button button, float fontSize)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.fontStyle = FontStyles.Bold;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void ApplyUiBlocker()
        {
            if (uiBlockerApplied || Global.main == null)
            {
                return;
            }

            Global.main.AddUiBlocker();
            uiBlockerApplied = true;
        }

        private void ReleaseUiBlocker()
        {
            if (!uiBlockerApplied || Global.main == null)
            {
                uiBlockerApplied = false;
                return;
            }

            Global.main.RemoveUiBlocker();
            uiBlockerApplied = false;
        }

        private void StyleButton(Button button, bool selected, bool danger)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();

            if (image != null)
            {
                Color fill = danger ? ButtonDangerColor : (selected ? ButtonSelectedColor : ButtonColor);
                Color border = danger ? ButtonDangerBorderColor : (selected ? AccentColor : (Color)ButtonBorderColor);
                ApplyRoundedImage(image, fill, border, CornerRadius, 1);
            }

            if (label != null)
            {
                label.color = button.interactable ? TextColor : DisabledTextColor;
            }

            SetButtonIconColor(button, button.interactable ? (selected ? TextColor : (Color)IconTintColor) : DisabledTextColor);
        }

        private static void SetButtonLabelColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = color;
            }

            SetButtonIconColor(button, color);
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            SetButtonLabelColor(button, interactable ? TextColor : DisabledTextColor);
            SetButtonIconColor(button, interactable ? (Color)IconTintColor : DisabledTextColor);
        }

        private void StyleSwatch(SwatchView swatch)
        {
            if (swatch.Button == null)
            {
                return;
            }

            Color32 left = swatch.Color;
            Color32 right = selectedColor;
            bool selected = left.r == right.r &&
                            left.g == right.g &&
                            left.b == right.b;

            Image image = swatch.Button.GetComponent<Image>();
            if (image != null)
            {
                ApplyRoundedImage(image, CanvasFrameColor, selected ? AccentColor : (Color)ButtonBorderColor, CornerRadius, selected ? 2 : 1);
            }

            if (swatch.Fill != null)
            {
                ApplyRoundedImage(swatch.Fill, swatch.Color, swatch.Color, 4, 0);
            }
        }

        private Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            ApplyRoundedImage(image, ButtonColor, ButtonBorderColor, CornerRadius, 1);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(190, 190, 190, 255);
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(6f, 2f, 6f, 2f);
            RectTransform textRect = text.rectTransform;
            Stretch(textRect);

            return button;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string content, int fontSize, FontStyle style, TextAnchor anchor, Color color)
        {
            GameObject textObject = CreateUiObject(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = uiFont;
            if (uiFontMaterial != null)
            {
                text.fontSharedMaterial = uiFontMaterial;
            }
            text.fontSize = fontSize;
            text.fontStyle = ConvertFontStyle(style);
            text.alignment = ConvertAlignment(anchor);
            text.color = color;
            text.text = content;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            text.extraPadding = true;
            text.richText = false;
            text.isOrthographic = true;
            return text;
        }

        private void ApplyRoundedImage(Image image, Color fill, Color border, int radius, int borderWidth)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fill;

            Outline outline = image.GetComponent<Outline>();
            if (borderWidth > 0 && border.a > 0.01f)
            {
                if (outline == null)
                {
                    outline = image.gameObject.AddComponent<Outline>();
                }

                outline.effectColor = border;
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
                outline.enabled = true;
            }
            else if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private Sprite GetRoundedSprite(Color fill, Color border, int radius, int borderWidth)
        {
            Color32 fill32 = fill;
            Color32 border32 = border;
            string key = ColorKey(fill32) + "|" + ColorKey(border32) + "|" + radius + "|" + borderWidth;
            Sprite cached;
            if (roundedSpriteCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            const int size = 32;
            int safeRadius = Mathf.Clamp(radius, 0, 15);
            int safeBorder = Mathf.Clamp(borderWidth, 0, 8);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    bool insideOuter = IsInsideRoundedRect(px, py, size, 0f, safeRadius);
                    if (!insideOuter)
                    {
                        pixels[x + y * size] = clear;
                        continue;
                    }

                    bool insideInner = safeBorder <= 0 || IsInsideRoundedRect(px, py, size, safeBorder, Mathf.Max(0, safeRadius - safeBorder));
                    pixels[x + y * size] = insideInner ? fill32 : border32;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0U,
                SpriteMeshType.FullRect,
                new Vector4(12f, 12f, 12f, 12f));

            roundedSpriteCache[key] = sprite;
            generatedUiTextures.Add(texture);
            generatedUiSprites.Add(sprite);
            return sprite;
        }

        private static bool IsInsideRoundedRect(float x, float y, int size, float inset, int radius)
        {
            float min = inset;
            float max = size - inset;
            if (x < min || y < min || x >= max || y >= max)
            {
                return false;
            }

            float width = max - min;
            float safeRadius = Mathf.Min(radius, width * 0.5f);
            if (safeRadius <= 0f)
            {
                return true;
            }

            float closestX = Mathf.Clamp(x, min + safeRadius, max - safeRadius);
            float closestY = Mathf.Clamp(y, min + safeRadius, max - safeRadius);
            float dx = x - closestX;
            float dy = y - closestY;
            return dx * dx + dy * dy <= safeRadius * safeRadius;
        }

        private static string ColorKey(Color32 color)
        {
            return color.r + "," + color.g + "," + color.b + "," + color.a;
        }

        private Texture2D GetGridTexture(Color background, Color line, int size)
        {
            int safeSize = Mathf.Max(2, size);
            Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            Color32 background32 = background;
            Color32 line32 = line;
            Color32[] pixels = new Color32[safeSize * safeSize];
            for (int y = 0; y < safeSize; y++)
            {
                for (int x = 0; x < safeSize; x++)
                {
                    pixels[x + y * safeSize] = (x == 0 || y == 0) ? line32 : background32;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            generatedUiTextures.Add(texture);
            return texture;
        }

        private Texture2D GetCheckerTexture(Color first, Color second, int cellSize)
        {
            int safeCell = Mathf.Max(2, cellSize);
            int size = safeCell * 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            Color32 first32 = first;
            Color32 second32 = second;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool alternate = ((x / safeCell) + (y / safeCell)) % 2 == 0;
                    pixels[x + y * size] = alternate ? first32 : second32;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            generatedUiTextures.Add(texture);
            return texture;
        }

        private Image CreateIconGraphic(string name, Transform parent, UiIconKind kind, Color color)
        {
            GameObject iconObject = CreateUiObject(name, parent);
            Image image = iconObject.AddComponent<Image>();
            image.sprite = GetIconSprite(kind);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void CreateAnchoredIcon(string name, Transform parent, UiIconKind kind, Color color, float size, Vector2 offset)
        {
            Image image = CreateIconGraphic(name, parent, kind, color);
            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offset;
        }

        private void AddInlineButtonIcon(Button button, UiIconKind kind, float size)
        {
            if (button == null)
            {
                return;
            }

            Image icon = CreateIconGraphic("Icon", button.transform, kind, IconTintColor);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            SetRect(
                iconRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(14f, -size * 0.5f),
                new Vector2(14f + size, size * 0.5f));

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.margin = new Vector4(size + 24f, 1f, 8f, 1f);
            }
        }

        private void AddCenteredButtonIcon(Button button, UiIconKind kind, float size)
        {
            if (button == null)
            {
                return;
            }

            Image icon = CreateIconGraphic("Icon", button.transform, kind, Color.white);
            icon.gameObject.name = kind == UiIconKind.Resize ? "StaticIcon" : "Icon";
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(size, size);
            iconRect.anchoredPosition = Vector2.zero;
        }

        private void AddStackedButtonIcon(Button button, UiIconKind kind, float size)
        {
            if (button == null)
            {
                return;
            }

            Image icon = CreateIconGraphic("Icon", button.transform, kind, IconTintColor);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            SetRect(
                iconRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-size * 0.5f, -12f - size),
                new Vector2(size * 0.5f, -12f));

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Bottom;
                label.margin = new Vector4(2f, 1f, 2f, 6f);
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -30f));
            }
        }

        private static void SetButtonIconColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            Image[] images = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && string.Equals(images[i].gameObject.name, "Icon", StringComparison.Ordinal))
                {
                    images[i].color = color;
                }
            }
        }

        private static string StripRichTextTags(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool insideTag = false;
            string result = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (current == '>')
                {
                    insideTag = false;
                    continue;
                }

                if (!insideTag)
                {
                    result += current;
                }
            }

            return result.Trim();
        }

        private UiIconKind GetSectionIconKind(string title)
        {
            if (string.Equals(title, "Limb Select", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Limb;
            }

            if (string.Equals(title, "Tools", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Tools;
            }

            if (string.Equals(title, "Color", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Color;
            }

            if (string.Equals(title, "Actions", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Actions;
            }

            if (string.Equals(title, "Canvas", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Canvas;
            }

            if (string.Equals(title, "Preview", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Eye;
            }

            if (string.Equals(title, "Layers", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Layers;
            }

            if (string.Equals(title, "Notes", StringComparison.OrdinalIgnoreCase))
            {
                return UiIconKind.Notes;
            }

            return UiIconKind.Canvas;
        }

        private static UiIconKind GetToolIconKind(ToolMode mode)
        {
            switch (mode)
            {
                case ToolMode.Eraser:
                    return UiIconKind.Eraser;
                case ToolMode.Fill:
                    return UiIconKind.Fill;
                case ToolMode.Eyedropper:
                    return UiIconKind.Picker;
                case ToolMode.Select:
                    return UiIconKind.Select;
                case ToolMode.Move:
                    return UiIconKind.Move;
                default:
                    return UiIconKind.Brush;
            }
        }

        private Sprite GetIconSprite(UiIconKind kind)
        {
            if (kind == UiIconKind.Pencil)
            {
                Sprite external = GetExternalPencilSprite();
                if (external != null)
                {
                    return external;
                }
            }

            string internetIconFileName = GetInternetIconFileName(kind);
            if (!string.IsNullOrEmpty(internetIconFileName))
            {
                Sprite internetIcon = GetExternalAssetIconSprite(kind, Path.Combine("ui-icons", internetIconFileName), false);
                if (internetIcon != null)
                {
                    return internetIcon;
                }
            }

            if (kind == UiIconKind.Import || kind == UiIconKind.Export || kind == UiIconKind.Resize)
            {
                string iconFileName = kind == UiIconKind.Import
                    ? "import.png"
                    : kind == UiIconKind.Export ? "export.png" : "expand-arrows.png";
                Sprite external = GetExternalAssetIconSprite(kind, iconFileName, kind != UiIconKind.Resize);
                if (external != null)
                {
                    return external;
                }
            }

            Sprite cached;
            if (iconSpriteCache.TryGetValue(kind, out cached) && cached != null)
            {
                return cached;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[size * size];
            DrawIcon(kind, pixels, size);
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            iconSpriteCache[kind] = sprite;
            generatedUiTextures.Add(texture);
            generatedUiSprites.Add(sprite);
            return sprite;
        }

        private static string GetInternetIconFileName(UiIconKind kind)
        {
            switch (kind)
            {
                case UiIconKind.Limb:
                    return "icon-limb.png";
                case UiIconKind.Tools:
                    return "icon-tools.png";
                case UiIconKind.Color:
                    return "icon-color.png";
                case UiIconKind.Actions:
                    return "icon-actions.png";
                case UiIconKind.Canvas:
                    return "icon-canvas.png";
                case UiIconKind.Preview:
                    return "icon-preview.png";
                case UiIconKind.Resize:
                    return "icon-resize.png";
                case UiIconKind.Done:
                    return "icon-done.png";
                case UiIconKind.Layers:
                    return "icon-layers.png";
                case UiIconKind.Brush:
                    return "icon-brush.png";
                case UiIconKind.Eraser:
                    return "icon-eraser.png";
                case UiIconKind.Fill:
                    return "icon-fill.png";
                case UiIconKind.Picker:
                    return "icon-picker.png";
                case UiIconKind.Select:
                    return "icon-select.png";
                case UiIconKind.Move:
                    return "icon-move.png";
                case UiIconKind.Eye:
                    return "icon-eye.png";
                case UiIconKind.Add:
                    return "icon-add.png";
                case UiIconKind.Duplicate:
                    return "icon-duplicate.png";
                case UiIconKind.Merge:
                    return "icon-merge.png";
                case UiIconKind.Trash:
                    return "icon-trash.png";
                case UiIconKind.Undo:
                    return "icon-undo.png";
                case UiIconKind.Redo:
                    return "icon-redo.png";
                case UiIconKind.Reset:
                    return "icon-reset.png";
                case UiIconKind.Copy:
                    return "icon-copy.png";
                case UiIconKind.Paste:
                    return "icon-paste.png";
                case UiIconKind.Person:
                    return "icon-person.png";
                case UiIconKind.Menu:
                    return "icon-menu.png";
                default:
                    return null;
            }
        }

        private Sprite GetExternalPencilSprite()
        {
            if (externalPencilSprite != null)
            {
                return externalPencilSprite;
            }

            string path = Path.Combine(GetModSourceFolder(), "Assets", "pencil.png");
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length <= 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            if (!LoadImageBytes(texture, bytes))
            {
                Destroy(texture);
                return null;
            }

            externalPencilSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            generatedUiTextures.Add(texture);
            generatedUiSprites.Add(externalPencilSprite);
            return externalPencilSprite;
        }

        private Sprite GetExternalAssetIconSprite(UiIconKind kind, string fileName, bool recolorToWhite)
        {
            Sprite cached;
            if (iconSpriteCache.TryGetValue(kind, out cached) && cached != null)
            {
                return cached;
            }

            string path = Path.Combine(GetModSourceFolder(), "Assets", fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length <= 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            if (!LoadImageBytes(texture, bytes))
            {
                Destroy(texture);
                return null;
            }

            if (recolorToWhite)
            {
                RecolorTextureToWhite(texture);
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            iconSpriteCache[kind] = sprite;
            generatedUiTextures.Add(texture);
            generatedUiSprites.Add(sprite);
            return sprite;
        }

        private static void RecolorTextureToWhite(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0)
                {
                    continue;
                }

                pixels[i].r = 255;
                pixels[i].g = 255;
                pixels[i].b = 255;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private static string GetModSourceFolder()
        {
            return HypermodifiedModPathResolver.GetModSourceFolder();
        }

        private static bool LoadImageBytes(Texture2D texture, byte[] bytes)
        {
            if (texture == null || bytes == null)
            {
                return false;
            }

            Type imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversionType == null)
            {
                return false;
            }

            System.Reflection.MethodInfo method = imageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]) },
                null);

            if (method != null)
            {
                return (bool)method.Invoke(null, new object[] { texture, bytes });
            }

            method = imageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);

            return method != null && (bool)method.Invoke(null, new object[] { texture, bytes, false });
        }

        private static void DrawIcon(UiIconKind kind, Color32[] pixels, int size)
        {
            Color32 white = new Color32(255, 255, 255, 255);
            switch (kind)
            {
                case UiIconKind.Workshop:
                    DrawRect(pixels, size, 18, 22, 28, 26, white, 4);
                    FillRect(pixels, size, 25, 34, 5, 5, white);
                    FillRect(pixels, size, 36, 34, 5, 5, white);
                    DrawLine(pixels, size, 32, 48, 32, 58, 4, white);
                    FillRect(pixels, size, 28, 56, 8, 4, white);
                    DrawRect(pixels, size, 24, 8, 16, 10, white, 4);
                    DrawLine(pixels, size, 18, 18, 10, 10, 4, white);
                    DrawLine(pixels, size, 46, 18, 54, 10, 4, white);
                    break;
                case UiIconKind.Pencil:
                    DrawLine(pixels, size, 18, 14, 48, 44, 5, white);
                    DrawLine(pixels, size, 23, 9, 53, 39, 5, white);
                    DrawLine(pixels, size, 15, 17, 20, 12, 5, white);
                    DrawLine(pixels, size, 45, 47, 54, 56, 4, white);
                    DrawLine(pixels, size, 48, 44, 54, 38, 3, white);
                    break;
                case UiIconKind.Limb:
                case UiIconKind.Person:
                    FillCircle(pixels, size, 32, 48, 6, white);
                    DrawLine(pixels, size, 32, 42, 32, 25, 5, white);
                    DrawLine(pixels, size, 32, 36, 20, 27, 4, white);
                    DrawLine(pixels, size, 32, 36, 44, 27, 4, white);
                    DrawLine(pixels, size, 32, 25, 23, 10, 4, white);
                    DrawLine(pixels, size, 32, 25, 41, 10, 4, white);
                    break;
                case UiIconKind.Tools:
                    DrawLine(pixels, size, 18, 16, 46, 44, 5, white);
                    DrawRect(pixels, size, 40, 40, 12, 12, white, 4);
                    DrawLine(pixels, size, 16, 46, 26, 56, 5, white);
                    DrawLine(pixels, size, 16, 56, 26, 46, 5, white);
                    break;
                case UiIconKind.Color:
                    DrawCircle(pixels, size, 32, 32, 23, white, 4);
                    FillCircle(pixels, size, 23, 39, 4, white);
                    FillCircle(pixels, size, 34, 45, 4, white);
                    FillCircle(pixels, size, 43, 36, 4, white);
                    FillCircle(pixels, size, 28, 25, 4, white);
                    DrawCircle(pixels, size, 42, 23, 5, white, 3);
                    break;
                case UiIconKind.Actions:
                    DrawLine(pixels, size, 36, 56, 22, 34, 5, white);
                    DrawLine(pixels, size, 22, 34, 34, 34, 5, white);
                    DrawLine(pixels, size, 34, 34, 24, 8, 5, white);
                    DrawLine(pixels, size, 24, 8, 46, 40, 5, white);
                    DrawLine(pixels, size, 46, 40, 34, 40, 5, white);
                    break;
                case UiIconKind.Canvas:
                    DrawRect(pixels, size, 12, 12, 16, 16, white, 4);
                    DrawRect(pixels, size, 36, 12, 16, 16, white, 4);
                    DrawRect(pixels, size, 12, 36, 16, 16, white, 4);
                    DrawRect(pixels, size, 36, 36, 16, 16, white, 4);
                    break;
                case UiIconKind.Preview:
                    DrawLine(pixels, size, 8, 32, 22, 44, 4, white);
                    DrawLine(pixels, size, 22, 44, 42, 44, 4, white);
                    DrawLine(pixels, size, 42, 44, 56, 32, 4, white);
                    DrawLine(pixels, size, 8, 32, 22, 20, 4, white);
                    DrawLine(pixels, size, 22, 20, 42, 20, 4, white);
                    DrawLine(pixels, size, 42, 20, 56, 32, 4, white);
                    FillCircle(pixels, size, 32, 32, 7, white);
                    break;
                case UiIconKind.Notes:
                    DrawRect(pixels, size, 18, 10, 30, 44, white, 4);
                    DrawLine(pixels, size, 27, 42, 41, 42, 3, white);
                    DrawLine(pixels, size, 27, 32, 41, 32, 3, white);
                    DrawLine(pixels, size, 27, 22, 36, 22, 3, white);
                    DrawLine(pixels, size, 42, 16, 52, 26, 4, white);
                    DrawLine(pixels, size, 52, 26, 58, 20, 4, white);
                    break;
                case UiIconKind.Brush:
                    DrawLine(pixels, size, 22, 14, 47, 39, 6, white);
                    DrawLine(pixels, size, 17, 10, 24, 17, 7, white);
                    DrawLine(pixels, size, 43, 40, 52, 49, 4, white);
                    break;
                case UiIconKind.Eraser:
                    DrawLine(pixels, size, 18, 18, 42, 42, 9, white);
                    DrawLine(pixels, size, 34, 50, 50, 34, 9, white);
                    DrawLine(pixels, size, 16, 18, 28, 6, 5, white);
                    DrawLine(pixels, size, 28, 6, 47, 25, 5, white);
                    break;
                case UiIconKind.Fill:
                    DrawRect(pixels, size, 18, 28, 24, 20, white, 4);
                    DrawLine(pixels, size, 18, 28, 42, 52, 4, white);
                    DrawLine(pixels, size, 42, 28, 52, 18, 5, white);
                    FillCircle(pixels, size, 52, 12, 5, white);
                    break;
                case UiIconKind.Picker:
                    DrawLine(pixels, size, 18, 14, 48, 44, 5, white);
                    DrawLine(pixels, size, 42, 50, 52, 40, 5, white);
                    DrawLine(pixels, size, 14, 10, 25, 21, 4, white);
                    DrawLine(pixels, size, 11, 8, 20, 8, 3, white);
                    break;
                case UiIconKind.Undo:
                    DrawLine(pixels, size, 20, 34, 44, 34, 5, white);
                    DrawLine(pixels, size, 20, 34, 30, 44, 5, white);
                    DrawLine(pixels, size, 20, 34, 30, 24, 5, white);
                    DrawLine(pixels, size, 44, 34, 52, 26, 4, white);
                    break;
                case UiIconKind.Redo:
                    DrawLine(pixels, size, 20, 34, 44, 34, 5, white);
                    DrawLine(pixels, size, 44, 34, 34, 44, 5, white);
                    DrawLine(pixels, size, 44, 34, 34, 24, 5, white);
                    DrawLine(pixels, size, 20, 34, 12, 26, 4, white);
                    break;
                case UiIconKind.Reset:
                    DrawCircle(pixels, size, 32, 32, 18, white, 4);
                    DrawLine(pixels, size, 45, 45, 53, 45, 5, white);
                    DrawLine(pixels, size, 45, 45, 45, 53, 5, white);
                    break;
                case UiIconKind.Copy:
                    DrawRect(pixels, size, 18, 22, 26, 28, white, 4);
                    DrawRect(pixels, size, 26, 14, 26, 28, white, 4);
                    break;
                case UiIconKind.Paste:
                    DrawRect(pixels, size, 20, 12, 28, 38, white, 4);
                    DrawRect(pixels, size, 25, 46, 18, 8, white, 4);
                    DrawLine(pixels, size, 28, 34, 40, 34, 3, white);
                    DrawLine(pixels, size, 28, 25, 40, 25, 3, white);
                    break;
                case UiIconKind.Import:
                    DrawRect(pixels, size, 14, 12, 36, 42, white, 4);
                    DrawLine(pixels, size, 32, 18, 32, 40, 5, white);
                    DrawLine(pixels, size, 32, 40, 22, 30, 5, white);
                    DrawLine(pixels, size, 32, 40, 42, 30, 5, white);
                    DrawLine(pixels, size, 22, 48, 42, 48, 4, white);
                    break;
                case UiIconKind.Export:
                    DrawRect(pixels, size, 14, 12, 36, 42, white, 4);
                    DrawLine(pixels, size, 32, 42, 32, 20, 5, white);
                    DrawLine(pixels, size, 32, 20, 22, 30, 5, white);
                    DrawLine(pixels, size, 32, 20, 42, 30, 5, white);
                    DrawLine(pixels, size, 22, 48, 42, 48, 4, white);
                    break;
                case UiIconKind.Resize:
                    DrawLine(pixels, size, 20, 20, 34, 34, 5, white);
                    DrawLine(pixels, size, 20, 20, 20, 34, 5, white);
                    DrawLine(pixels, size, 20, 20, 34, 20, 5, white);
                    DrawLine(pixels, size, 44, 44, 30, 30, 5, white);
                    DrawLine(pixels, size, 44, 44, 30, 44, 5, white);
                    DrawLine(pixels, size, 44, 44, 44, 30, 5, white);
                    break;
                case UiIconKind.Done:
                    DrawLine(pixels, size, 14, 32, 27, 19, 6, white);
                    DrawLine(pixels, size, 27, 19, 52, 46, 6, white);
                    break;
                case UiIconKind.Close:
                    DrawLine(pixels, size, 17, 17, 47, 47, 6, white);
                    DrawLine(pixels, size, 47, 17, 17, 47, 6, white);
                    break;
            }
        }

        private static void FillRect(Color32[] pixels, int size, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetIconPixel(pixels, size, px, py, color);
                }
            }
        }

        private static void DrawRect(Color32[] pixels, int size, int x, int y, int width, int height, Color32 color, int thickness)
        {
            FillRect(pixels, size, x, y, width, thickness, color);
            FillRect(pixels, size, x, y + height - thickness, width, thickness, color);
            FillRect(pixels, size, x, y, thickness, height, color);
            FillRect(pixels, size, x + width - thickness, y, thickness, height, color);
        }

        private static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int half = Mathf.Max(0, thickness / 2);

            while (true)
            {
                FillRect(pixels, size, x0 - half, y0 - half, thickness, thickness, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void DrawCircle(Color32[] pixels, int size, int centerX, int centerY, int radius, Color32 color, int thickness)
        {
            int outer = radius * radius;
            int innerRadius = Mathf.Max(0, radius - thickness);
            int inner = innerRadius * innerRadius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distance = dx * dx + dy * dy;
                    if (distance <= outer && distance >= inner)
                    {
                        SetIconPixel(pixels, size, x, y, color);
                    }
                }
            }
        }

        private static void FillCircle(Color32[] pixels, int size, int centerX, int centerY, int radius, Color32 color)
        {
            int outer = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= outer)
                    {
                        SetIconPixel(pixels, size, x, y, color);
                    }
                }
            }
        }

        private static void SetIconPixel(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
            {
                return;
            }

            pixels[x + y * size] = color;
        }

        private void DestroyGeneratedUiAssets()
        {
            for (int i = 0; i < generatedUiSprites.Count; i++)
            {
                if (generatedUiSprites[i] != null)
                {
                    Destroy(generatedUiSprites[i]);
                }
            }

            for (int i = 0; i < generatedUiTextures.Count; i++)
            {
                if (generatedUiTextures[i] != null)
                {
                    Destroy(generatedUiTextures[i]);
                }
            }

            generatedUiSprites.Clear();
            generatedUiTextures.Clear();
            roundedSpriteCache.Clear();
            iconSpriteCache.Clear();
            externalPencilSprite = null;
        }

        private void DestroyEditorUi()
        {
            if (uiRoot != null)
            {
                Destroy(uiRoot);
                uiRoot = null;
            }

            if (editorCanvasObject != null)
            {
                Destroy(editorCanvasObject);
                editorCanvasObject = null;
            }

            windowRect = null;
            panelHostRect = null;
        }

        private GameObject EnsureEditorCanvas()
        {
            if (editorCanvasObject != null)
            {
                return editorCanvasObject;
            }

            editorCanvasObject = new GameObject("DbpuSpriteEditorCanvas");
            editorCanvasObject.transform.SetParent(transform, false);

            Canvas canvas = editorCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 32000;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            CanvasScaler scaler = editorCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            editorCanvasObject.AddComponent<GraphicRaycaster>();
            return editorCanvasObject;
        }

        private bool InitializeTextResources()
        {
            if (uiFont != null)
            {
                return true;
            }

            try
            {
                var hologram = ModAPI.FindSpawnable("Holographic Display");
                if (hologram != null && hologram.Prefab != null)
                {
                    Transform textTf = hologram.Prefab.transform.Find("Text");
                    if (textTf != null)
                    {
                        var sampleTmp = textTf.GetComponent<TextMeshPro>();
                        if (sampleTmp != null && sampleTmp.font != null)
                        {
                            uiFont = sampleTmp.font;
                            uiFontMaterial = uiFont != null ? uiFont.material : null;
                        }
                    }
                }
            }
            catch { }

            if (uiFont == null)
            {
                TextMeshProUGUI sample = null;
                GameObject canvasObject = GameObject.Find("Canvas");
                if (canvasObject != null)
                {
                    sample = canvasObject.GetComponentInChildren<TextMeshProUGUI>(true);
                }

                if (sample == null)
                {
                    TextMeshProUGUI[] samples = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (samples[i] != null && samples[i].font != null)
                        {
                            sample = samples[i];
                            break;
                        }
                    }
                }

                if (sample != null)
                {
                    uiFont = sample.font;
                    uiFontMaterial = uiFont != null ? uiFont.material : null;
                }
            }

            if (uiFont == null)
            {
                uiFont = TMP_Settings.defaultFontAsset;
            }

            if (uiFont != null && uiFontMaterial == null)
            {
                uiFontMaterial = uiFont.material;
            }

            return uiFont != null;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localScale = Vector3.one;
            return gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchWithPadding(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static FontStyles ConvertFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.TopLeft;
            }
        }

        private static string GetToolLabel(ToolMode mode)
        {
            switch (mode)
            {
                case ToolMode.Brush:
                    return "Brush";
                case ToolMode.Eraser:
                    return "Eraser";
                case ToolMode.Fill:
                    return "Fill";
                case ToolMode.Eyedropper:
                    return "Picker";
                case ToolMode.Select:
                    return "Marquee";
                case ToolMode.Move:
                    return "Move";
                default:
                    return mode.ToString();
            }
        }

        private static string GetHexString(Color32 color)
        {
            return "#" +
                   color.r.ToString("X2") +
                   color.g.ToString("X2") +
                   color.b.ToString("X2");
        }

        private void EndActiveStroke()
        {
            if (activeStrokeState != null)
            {
                activeStrokeState.EndStroke();
                activeStrokeState = null;
            }
        }

        private void FinalizeActiveStroke()
        {
            if (strokeInProgress &&
                strokeChanged &&
                activeStrokeState != null)
            {
                activeStrokeState.CommitCurrentPixels();
            }

            EndActiveStroke();
        }

        private void ApplyPendingHitboxesAndClose()
        {
            CloseNativeColorPickerIfNeeded();
            FinalizeActiveStroke();

            if (currentTarget != null)
            {
                try
                {
                    currentTarget.ApplyPendingHitboxChanges();
                    global::DBPU.DbpuSpriteEditorBridge.NotifyCharacterApply(currentTarget);
                }
                catch (Exception exception)
                {
                Debug.LogError("[DB Singularity] Failed to apply pending limb hitboxes: " + exception);
                }
            }
            else if (currentObjectTarget != null)
            {
                try
                {
                    currentObjectTarget.ApplyPendingHitboxChanges();
                }
                catch (Exception exception)
                {
                Debug.LogError("[DB Singularity] Failed to apply pending object hitboxes: " + exception);
                }
            }

            CloseEditor();
        }

        public enum CanvasResizeHandleKind
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private enum ToolMode
        {
            Brush,
            Eraser,
            Fill,
            Eyedropper,
            Select,
            Move
        }

        public enum EditorLayerKind
        {
            Paint,
            Base,
            Damage
        }

        private enum UiIconKind
        {
            Workshop,
            Pencil,
            Limb,
            Tools,
            Color,
            Actions,
            Canvas,
            Preview,
            Layers,
            Notes,
            Brush,
            Eraser,
            Fill,
            Picker,
            Select,
            Move,
            Eye,
            Add,
            Duplicate,
            Merge,
            Trash,
            Menu,
            Undo,
            Redo,
            Reset,
            Copy,
            Paste,
            Import,
            Export,
            Resize,
            Done,
            Close,
            Person
        }

        private struct SwatchView
        {
            public Button Button;
            public Image Fill;
            public Color Color;
        }
    }

    public sealed class SpriteEditorCanvasSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public SpriteEditorRuntime Runtime;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.HandleCanvasPointerDown(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.HandleCanvasDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.HandleCanvasPointerUp(eventData);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.HandleCanvasScroll(eventData);
            }
        }
    }

    public sealed class SpriteEditorCanvasResizeHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public SpriteEditorRuntime Runtime;
        public SpriteEditorRuntime.CanvasResizeHandleKind Kind;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.BeginCanvasResizeDrag(Kind, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.UpdateCanvasResizeDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Runtime != null)
            {
                Runtime.EndCanvasResizeDrag(eventData);
            }
        }
    }

    public sealed class LayerRowDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public SpriteEditorRuntime Runtime;
        public int LayerIndex;
        private RectTransform rectTransform;
        private Vector2 startPosition;

        public void OnBeginDrag(PointerEventData eventData)
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                startPosition = rectTransform.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform != null && eventData != null)
            {
                rectTransform.anchoredPosition += eventData.delta;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = startPosition;
            }

            if (Runtime != null)
            {
                Runtime.HandleLayerRowDragEnd(LayerIndex, eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Runtime != null && eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                Runtime.HandleLayerRowRightClick(LayerIndex);
            }
        }
    }

    public sealed class LimbSpriteState : IDisposable
    {
        private const int MaxHistoryStates = 40;
        private const int FillColorTolerance = 18;

        private sealed class HistorySnapshot
        {
            public Sprite BasisSprite;
            public Color32[] Pixels;
            public LayerSnapshot[] Layers;
            public int ActiveLayerIndex;
            public int PasteLayerSerial;
        }

        public sealed class LayerStackSnapshot
        {
            public LayerSnapshot[] Layers;
            public int ActiveLayerIndex;
            public int PasteLayerSerial;
        }

        public sealed class LayerSnapshot
        {
            public string Name;
            public Color32[] Pixels;
            public bool Visible;
        }

        private sealed class PixelLayer
        {
            public string Name;
            public Color32[] Pixels;
            public bool Visible = true;
        }

        private struct HitboxRect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private readonly List<HistorySnapshot> history = new List<HistorySnapshot>();
        private readonly List<PixelLayer> pixelLayers = new List<PixelLayer>();
        private readonly List<Sprite> ownedBasisSprites = new List<Sprite>();
        private readonly List<Texture2D> ownedBasisTextures = new List<Texture2D>();
        private Sprite originalSprite;
        private Sprite resetSprite;
        private Color32[] originalPixels;
        private HistorySnapshot sessionBaseSnapshot;

        private Texture2D workingTexture;
        private Texture2D displayTexture;
        private Sprite workingSprite;
        private PhysicalBehaviour physicalTarget;
        private SpriteRenderer spriteRendererTarget;
        private bool usingPreviewSprite = true;
        private HistorySnapshot lastAppliedHitboxSnapshot;
        private Color32[] strokeBasePixels;
        private Sprite replacementBodyLayerBasisSprite;
        private Color32[] replacementFleshPixels;
        private Color32[] replacementBonePixels;
        private Color32[] replacementDamagePixels;
        private int replacementFleshWidth;
        private int replacementFleshHeight;
        private int replacementBoneWidth;
        private int replacementBoneHeight;
        private int replacementDamageWidth;
        private int replacementDamageHeight;
        private int historyIndex;
        private int activeLayerIndex;
        private int pasteLayerSerial = 1;

        public LimbSpriteState(LimbBehaviour limb)
        {
            Limb = limb;
            physicalTarget = limb != null ? limb.PhysicalBehaviour : null;
            InitializeFromDisplayedSprite(GetDisplayedSprite(limb));
        }

        public LimbSpriteState(PhysicalBehaviour physical)
        {
            physicalTarget = physical;
            spriteRendererTarget = physical != null ? physical.spriteRenderer : null;
            InitializeFromDisplayedSprite(GetDisplayedSprite(physical));
        }

        public LimbSpriteState(SpriteRenderer renderer)
        {
            spriteRendererTarget = renderer;
            InitializeFromDisplayedSprite(GetDisplayedSprite(renderer));
        }

        public LimbSpriteState(LimbBehaviour limb, Sprite baseSprite, Color32[] basePixels, Sprite currentSprite)
        {
            Limb = limb;
            physicalTarget = limb != null ? limb.PhysicalBehaviour : null;
            InitializeFromCommittedSprite(baseSprite, basePixels, currentSprite);
        }

        public LimbSpriteState(PhysicalBehaviour physical, Sprite baseSprite, Color32[] basePixels, Sprite currentSprite)
        {
            physicalTarget = physical;
            spriteRendererTarget = physical != null ? physical.spriteRenderer : null;
            InitializeFromCommittedSprite(baseSprite, basePixels, currentSprite);
        }

        public LimbSpriteState(SpriteRenderer renderer, Sprite baseSprite, Color32[] basePixels, Sprite currentSprite)
        {
            spriteRendererTarget = renderer;
            InitializeFromCommittedSprite(baseSprite, basePixels, currentSprite);
        }

        private void InitializeFromDisplayedSprite(Sprite displayedSprite)
        {
            if (!IsUsableTarget())
            {
                return;
            }

            originalSprite = displayedSprite;
            resetSprite = displayedSprite;
            if (originalSprite == null)
            {
                return;
            }

            workingTexture = CreateEditableTexture(originalSprite);
            if (workingTexture == null)
            {
                return;
            }

            workingSprite = CreatePreviewSprite(originalSprite, workingTexture);
            if (workingSprite == null)
            {
                return;
            }

            originalPixels = ClonePixels(workingTexture.GetPixels32());
            InitializePixelLayers(originalPixels, "Sprite");
            sessionBaseSnapshot = CaptureCurrentSnapshot();
            lastAppliedHitboxSnapshot = CaptureCurrentSnapshot();
            history.Add(CaptureCurrentSnapshot());
            historyIndex = 0;
        }

        private void InitializeFromCommittedSprite(Sprite baseSprite, Color32[] basePixels, Sprite currentSprite)
        {
            if (!IsUsableTarget())
            {
                return;
            }

            resetSprite = baseSprite != null ? baseSprite : currentSprite;
            originalSprite = currentSprite != null ? currentSprite : resetSprite;
            if (originalSprite == null)
            {
                return;
            }

            workingTexture = CreateEditableTexture(originalSprite);
            if (workingTexture == null)
            {
                return;
            }

            int resetPixelCount = GetSpritePixelCount(resetSprite);
            Color32[] resolvedOriginalPixels = NormalizePixels(basePixels, resetPixelCount);
            if (resolvedOriginalPixels == null)
            {
                resolvedOriginalPixels = CreatePixelsFromSprite(
                    resetSprite,
                    Mathf.RoundToInt(resetSprite.rect.width),
                    Mathf.RoundToInt(resetSprite.rect.height));
            }

            if (resolvedOriginalPixels == null)
            {
                resolvedOriginalPixels = ClonePixels(workingTexture.GetPixels32());
            }

            Color32[] resolvedCurrentPixels = CreatePixelsFromSprite(
                currentSprite != null ? currentSprite : originalSprite,
                workingTexture.width,
                workingTexture.height);
            if (resolvedCurrentPixels == null)
            {
                resolvedCurrentPixels = ClonePixels(workingTexture.GetPixels32());
            }

            workingTexture.SetPixels32(resolvedCurrentPixels);
            workingTexture.Apply();

            workingSprite = CreatePreviewSprite(originalSprite, workingTexture);
            if (workingSprite == null)
            {
                return;
            }

            originalPixels = ClonePixels(resolvedOriginalPixels);
            InitializePixelLayers(resolvedCurrentPixels, "Sprite");
            sessionBaseSnapshot = CaptureCurrentSnapshot();
            lastAppliedHitboxSnapshot = CaptureCurrentSnapshot();
            history.Add(CaptureCurrentSnapshot());
            historyIndex = 0;
        }

        public LimbBehaviour Limb { get; private set; }

        public PhysicalBehaviour PhysicalTarget
        {
            get { return physicalTarget; }
        }

        public SpriteRenderer SpriteRendererTarget
        {
            get { return spriteRendererTarget; }
        }

        public Texture2D WorkingTexture
        {
            get { return workingTexture; }
        }

        public Sprite OriginalSprite
        {
            get { return originalSprite; }
        }

        public Sprite ResetSprite
        {
            get { return resetSprite != null ? resetSprite : originalSprite; }
        }

        public Color32[] ResetPixelsSnapshot
        {
            get { return ClonePixels(originalPixels); }
        }

        public Color32[] GetCurrentPixelsSnapshot()
        {
            return workingTexture != null
                ? ClonePixels(workingTexture.GetPixels32())
                : null;
        }

        public int LayerCount
        {
            get
            {
                EnsurePixelLayers();
                return pixelLayers.Count;
            }
        }

        public int ActiveLayerIndex
        {
            get
            {
                EnsurePixelLayers();
                return Mathf.Clamp(activeLayerIndex, 0, Mathf.Max(0, pixelLayers.Count - 1));
            }
        }

        public string GetLayerName(int index)
        {
            EnsurePixelLayers();
            if (index < 0 || index >= pixelLayers.Count)
            {
                return "Layer " + (index + 1);
            }

            return pixelLayers[index].Name;
        }

        public bool IsLayerVisible(int index)
        {
            EnsurePixelLayers();
            return index >= 0 && index < pixelLayers.Count && pixelLayers[index].Visible;
        }

        public bool SetActiveLayer(int index)
        {
            EnsurePixelLayers();
            if (index < 0 || index >= pixelLayers.Count)
            {
                return false;
            }

            activeLayerIndex = index;
            return true;
        }

        public bool RenameLayer(int index, string name)
        {
            EnsurePixelLayers();
            if (index < 0 || index >= pixelLayers.Count)
            {
                return false;
            }

            string trimmed = string.IsNullOrWhiteSpace(name) ? "Layer " + (index + 1) : name.Trim();
            if (trimmed.Length > 48)
            {
                trimmed = trimmed.Substring(0, 48);
            }

            if (string.Equals(pixelLayers[index].Name, trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            pixelLayers[index].Name = trimmed;
            activeLayerIndex = index;
            CommitCurrentPixels();
            return true;
        }

        public bool ToggleLayerVisibility(int index)
        {
            EnsurePixelLayers();
            if (index < 0 || index >= pixelLayers.Count)
            {
                return false;
            }

            pixelLayers[index].Visible = !pixelLayers[index].Visible;
            RecomposeWorkingTextureFromLayers(true);
            return true;
        }

        public bool AddEmptyLayer()
        {
            if (workingTexture == null)
            {
                return false;
            }

            EnsurePixelLayers();
            PixelLayer layer = new PixelLayer();
            layer.Name = "Layer " + (pixelLayers.Count + 1);
            layer.Visible = true;
            layer.Pixels = new Color32[workingTexture.width * workingTexture.height];
            pixelLayers.Insert(0, layer);
            activeLayerIndex = 0;
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool DuplicateActiveLayer()
        {
            PixelLayer source = GetActivePixelLayer();
            if (source == null || source.Pixels == null)
            {
                return false;
            }

            PixelLayer layer = new PixelLayer();
            layer.Name = source.Name + " Copy";
            layer.Visible = source.Visible;
            layer.Pixels = ClonePixels(source.Pixels);
            pixelLayers.Insert(activeLayerIndex, layer);
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool MergeActiveLayerDown()
        {
            EnsurePixelLayers();
            if (activeLayerIndex < 0 || activeLayerIndex >= pixelLayers.Count - 1)
            {
                return false;
            }

            PixelLayer lower = pixelLayers[activeLayerIndex + 1];
            PixelLayer upper = pixelLayers[activeLayerIndex];
            if (lower == null || upper == null || lower.Pixels == null || upper.Pixels == null || lower.Pixels.Length != upper.Pixels.Length)
            {
                return false;
            }

            for (int i = 0; i < upper.Pixels.Length; i++)
            {
                if (upper.Pixels[i].a > 0)
                {
                    lower.Pixels[i] = upper.Pixels[i];
                }
            }

            pixelLayers.RemoveAt(activeLayerIndex);
            activeLayerIndex = Mathf.Clamp(activeLayerIndex, 0, pixelLayers.Count - 1);
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool DeleteActiveLayer()
        {
            EnsurePixelLayers();
            if (pixelLayers.Count <= 1 || activeLayerIndex < 0 || activeLayerIndex >= pixelLayers.Count)
            {
                return false;
            }

            pixelLayers.RemoveAt(activeLayerIndex);
            activeLayerIndex = Mathf.Clamp(activeLayerIndex, 0, pixelLayers.Count - 1);
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public LayerSnapshot CaptureActiveLayerSnapshot()
        {
            PixelLayer layer = GetActivePixelLayer();
            if (layer == null || layer.Pixels == null)
            {
                return null;
            }

            LayerSnapshot snapshot = new LayerSnapshot();
            snapshot.Name = layer.Name;
            snapshot.Pixels = ClonePixels(layer.Pixels);
            snapshot.Visible = layer.Visible;
            return snapshot;
        }

        public bool PasteLayerSnapshot(LayerSnapshot snapshot)
        {
            if (workingTexture == null || snapshot == null || snapshot.Pixels == null)
            {
                return false;
            }

            EnsurePixelLayers();
            int expectedLength = workingTexture.width * workingTexture.height;
            if (snapshot.Pixels.Length != expectedLength)
            {
                return false;
            }

            PixelLayer layer = new PixelLayer();
            layer.Name = string.IsNullOrEmpty(snapshot.Name) ? "Layer Copy" : snapshot.Name + " Copy";
            layer.Pixels = ClonePixels(snapshot.Pixels);
            layer.Visible = snapshot.Visible;

            int insertIndex = Mathf.Clamp(activeLayerIndex, 0, pixelLayers.Count);
            pixelLayers.Insert(insertIndex, layer);
            activeLayerIndex = insertIndex;
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool PasteLayerStackSnapshot(LayerStackSnapshot stack, string sourceName)
        {
            if (workingTexture == null || stack == null || stack.Layers == null)
            {
                return false;
            }

            EnsurePixelLayers();
            int expectedLength = workingTexture.width * workingTexture.height;
            List<PixelLayer> pastedLayers = new List<PixelLayer>();
            string prefix = string.IsNullOrWhiteSpace(sourceName) ? "Copied" : sourceName.Trim();
            if (prefix.Length > 24)
            {
                prefix = prefix.Substring(0, 24);
            }

            for (int i = 0; i < stack.Layers.Length; i++)
            {
                LayerSnapshot snapshot = stack.Layers[i];
                if (snapshot == null || snapshot.Pixels == null || snapshot.Pixels.Length != expectedLength)
                {
                    continue;
                }

                PixelLayer layer = new PixelLayer();
                string snapshotName = string.IsNullOrWhiteSpace(snapshot.Name) ? "Layer" : snapshot.Name.Trim();
                if (snapshotName.Length > 32)
                {
                    snapshotName = snapshotName.Substring(0, 32);
                }

                layer.Name = prefix + " " + snapshotName;
                layer.Pixels = ClonePixels(snapshot.Pixels);
                layer.Visible = snapshot.Visible;
                pastedLayers.Add(layer);
            }

            if (pastedLayers.Count <= 0)
            {
                return false;
            }

            pixelLayers.InsertRange(0, pastedLayers);
            activeLayerIndex = 0;
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool MoveLayer(int sourceIndex, int destinationIndex)
        {
            EnsurePixelLayers();
            if (sourceIndex < 0 ||
                sourceIndex >= pixelLayers.Count ||
                destinationIndex < 0 ||
                destinationIndex >= pixelLayers.Count ||
                sourceIndex == destinationIndex)
            {
                return false;
            }

            PixelLayer layer = pixelLayers[sourceIndex];
            pixelLayers.RemoveAt(sourceIndex);
            pixelLayers.Insert(destinationIndex, layer);
            activeLayerIndex = destinationIndex;
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool HasLayerStack
        {
            get
            {
                EnsurePixelLayers();
                return pixelLayers.Count > 1;
            }
        }

        public LayerStackSnapshot CaptureLayerStackSnapshot()
        {
            LayerStackSnapshot stack = new LayerStackSnapshot();
            stack.Layers = CaptureLayerSnapshots();
            stack.ActiveLayerIndex = activeLayerIndex;
            stack.PasteLayerSerial = pasteLayerSerial;
            return stack;
        }

        public bool RestoreLayerStackSnapshot(LayerStackSnapshot stack)
        {
            return RestoreLayerStackSnapshot(stack, true);
        }

        public bool RestoreLayerStackSnapshot(LayerStackSnapshot stack, bool acceptAsSessionBase)
        {
            if (stack == null || stack.Layers == null || workingTexture == null)
            {
                return false;
            }

            int expectedLength = workingTexture.width * workingTexture.height;
            List<PixelLayer> restoredLayers = new List<PixelLayer>();
            for (int i = 0; i < stack.Layers.Length; i++)
            {
                LayerSnapshot snapshot = stack.Layers[i];
                if (snapshot == null || snapshot.Pixels == null || snapshot.Pixels.Length != expectedLength)
                {
                    continue;
                }

                PixelLayer layer = new PixelLayer();
                layer.Name = string.IsNullOrEmpty(snapshot.Name) ? "Layer " + (i + 1) : snapshot.Name;
                layer.Pixels = ClonePixels(snapshot.Pixels);
                layer.Visible = snapshot.Visible;
                restoredLayers.Add(layer);
            }

            if (restoredLayers.Count <= 0)
            {
                return false;
            }

            pixelLayers.Clear();
            pixelLayers.AddRange(restoredLayers);
            activeLayerIndex = Mathf.Clamp(stack.ActiveLayerIndex, 0, pixelLayers.Count - 1);
            pasteLayerSerial = Mathf.Max(1, stack.PasteLayerSerial);
            RecomposeWorkingTextureFromLayers(true);

            HistorySnapshot snapshotBase = CaptureCurrentSnapshot();
            if (acceptAsSessionBase)
            {
                sessionBaseSnapshot = snapshotBase;
                lastAppliedHitboxSnapshot = snapshotBase;
                history.Clear();
                history.Add(snapshotBase);
                historyIndex = 0;
            }
            else
            {
                CommitCurrentPixels();
            }

            return true;
        }

        private void InitializePixelLayers(Color32[] pixels, string name)
        {
            pixelLayers.Clear();
            PixelLayer layer = new PixelLayer();
            layer.Name = string.IsNullOrEmpty(name) ? "Sprite" : name;
            layer.Pixels = ClonePixels(pixels);
            layer.Visible = true;
            pixelLayers.Add(layer);
            activeLayerIndex = 0;
            pasteLayerSerial = 1;
        }

        private void EnsurePixelLayers()
        {
            if (workingTexture == null || pixelLayers.Count > 0)
            {
                return;
            }

            InitializePixelLayers(workingTexture.GetPixels32(), "Sprite");
        }

        private PixelLayer GetActivePixelLayer()
        {
            EnsurePixelLayers();
            if (pixelLayers.Count <= 0)
            {
                return null;
            }

            activeLayerIndex = Mathf.Clamp(activeLayerIndex, 0, pixelLayers.Count - 1);
            return pixelLayers[activeLayerIndex];
        }

        private bool RecomposeWorkingTextureFromLayers(bool assign)
        {
            if (workingTexture == null)
            {
                return false;
            }

            EnsurePixelLayers();
            Color32[] composed = new Color32[workingTexture.width * workingTexture.height];
            for (int i = pixelLayers.Count - 1; i >= 0; i--)
            {
                PixelLayer layer = pixelLayers[i];
                if (layer == null || !layer.Visible || layer.Pixels == null || layer.Pixels.Length != composed.Length)
                {
                    continue;
                }

                for (int p = 0; p < composed.Length; p++)
                {
                    if (layer.Pixels[p].a > 0)
                    {
                        composed[p] = layer.Pixels[p];
                    }
                }
            }

            workingTexture.SetPixels32(composed);
            workingTexture.Apply();
            if (assign)
            {
                EnsureAssigned();
            }

            return true;
        }

        public bool TryGetVisiblePixelSize(out int width, out int height)
        {
            width = 0;
            height = 0;
            if (workingTexture == null)
            {
                return false;
            }

            RectInt bounds;
            if (!TryGetVisibleBounds(workingTexture.GetPixels32(), workingTexture.width, workingTexture.height, out bounds))
            {
                return false;
            }

            width = bounds.width;
            height = bounds.height;
            return true;
        }

        public bool TryGetBodyLayerSnapshots(
            out Color32[] fleshPixels,
            out int fleshWidth,
            out int fleshHeight,
            out Color32[] bonePixels,
            out int boneWidth,
            out int boneHeight)
        {
            fleshPixels = null;
            fleshWidth = 0;
            fleshHeight = 0;
            bonePixels = null;
            boneWidth = 0;
            boneHeight = 0;

            if (HasReplacementBodyLayerSnapshots())
            {
                fleshPixels = ClonePixels(replacementFleshPixels);
                fleshWidth = replacementFleshWidth;
                fleshHeight = replacementFleshHeight;
                bonePixels = ClonePixels(replacementBonePixels);
                boneWidth = replacementBoneWidth;
                boneHeight = replacementBoneHeight;
                return true;
            }

            Material material = GetTargetMaterial();
            if (material == null)
            {
                return false;
            }

            Texture2D fleshTexture = material.GetTexture("_FleshTex") as Texture2D;
            Texture2D boneTexture = material.GetTexture("_BoneTex") as Texture2D;
            if (fleshTexture == null || boneTexture == null)
            {
                return false;
            }

            Texture2D readableFlesh = CreateReadableCopy(fleshTexture);
            Texture2D readableBone = CreateReadableCopy(boneTexture);
            if (readableFlesh == null || readableBone == null)
            {
                if (readableFlesh != null)
                {
                    UnityEngine.Object.Destroy(readableFlesh);
                }

                if (readableBone != null)
                {
                    UnityEngine.Object.Destroy(readableBone);
                }

                return false;
            }

            bool extractedFlesh = TryExtractSpriteLocalLayerPixels(readableFlesh, out fleshPixels, out fleshWidth, out fleshHeight);
            bool extractedBone = TryExtractSpriteLocalLayerPixels(readableBone, out bonePixels, out boneWidth, out boneHeight);

            UnityEngine.Object.Destroy(readableFlesh);
            UnityEngine.Object.Destroy(readableBone);
            return extractedFlesh && extractedBone;
        }

        public bool TryGetDamageLayerSnapshot(out Color32[] damagePixels, out int damageWidth, out int damageHeight)
        {
            damagePixels = null;
            damageWidth = 0;
            damageHeight = 0;

            if (HasReplacementDamageLayerSnapshot())
            {
                damagePixels = ClonePixels(replacementDamagePixels);
                damageWidth = replacementDamageWidth;
                damageHeight = replacementDamageHeight;
                return true;
            }

            Material material = GetTargetMaterial();
            if (material == null)
            {
                return false;
            }

            Texture2D damageTexture = material.GetTexture("_DamageTex") as Texture2D;
            if (damageTexture == null)
            {
                return false;
            }

            Texture2D readableDamage = CreateReadableCopy(damageTexture);
            if (readableDamage == null)
            {
                return false;
            }

            bool extractedDamage = TryExtractSpriteLocalLayerPixels(readableDamage, out damagePixels, out damageWidth, out damageHeight);
            UnityEngine.Object.Destroy(readableDamage);
            return extractedDamage;
        }

        public bool TryCreateReplacementBodyLayerTextures(out Texture2D fleshTexture, out Texture2D boneTexture, out bool ownsTextures)
        {
            fleshTexture = null;
            boneTexture = null;
            ownsTextures = false;

            if (!HasReplacementBodyLayerSnapshots())
            {
                return false;
            }

            fleshTexture = CreateTextureFromPixels(replacementFleshWidth, replacementFleshHeight, replacementFleshPixels);
            boneTexture = CreateTextureFromPixels(replacementBoneWidth, replacementBoneHeight, replacementBonePixels);
            if (fleshTexture == null || boneTexture == null)
            {
                if (fleshTexture != null)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                }

                if (boneTexture != null)
                {
                    UnityEngine.Object.Destroy(boneTexture);
                }

                fleshTexture = null;
                boneTexture = null;
                return false;
            }

            fleshTexture.name = (Limb != null ? Limb.name : "Object") + "_LiveSpriteWorkshopFlesh";
            boneTexture.name = (Limb != null ? Limb.name : "Object") + "_LiveSpriteWorkshopBone";
            ownsTextures = true;
            return true;
        }

        public bool TryCreateReplacementDamageLayerTexture(out Texture2D damageTexture, out bool ownsTexture)
        {
            damageTexture = null;
            ownsTexture = false;

            if (!HasReplacementDamageLayerSnapshot())
            {
                return false;
            }

            damageTexture = CreateTextureFromPixels(replacementDamageWidth, replacementDamageHeight, replacementDamagePixels);
            if (damageTexture == null)
            {
                return false;
            }

            damageTexture.name = (Limb != null ? Limb.name : "Object") + "_LiveSpriteWorkshopDamage";
            ownsTexture = true;
            return true;
        }

        public int Width
        {
            get { return workingTexture != null ? workingTexture.width : 0; }
        }

        public int Height
        {
            get { return workingTexture != null ? workingTexture.height : 0; }
        }

        public bool IsValid
        {
            get
            {
                return IsUsableTarget() &&
                       workingTexture != null &&
                       workingSprite != null;
            }
        }

        public bool MatchesDisplayedSprite(Sprite displayedSprite)
        {
            if (displayedSprite == null)
            {
                return false;
            }

            if (displayedSprite == originalSprite || displayedSprite == workingSprite)
            {
                return true;
            }

            if (workingTexture == null)
            {
                return false;
            }

            Color32[] displayedPixels = CreatePixelsFromSprite(displayedSprite, workingTexture.width, workingTexture.height);
            return displayedPixels != null && PixelsEqual(displayedPixels, workingTexture.GetPixels32());
        }

        public bool HasChanges
        {
            get
            {
                if (workingTexture == null)
                {
                    return false;
                }

                return !CurrentMatchesSnapshot(sessionBaseSnapshot);
            }
        }

        public bool CanResetToOriginal
        {
            get
            {
                if (workingTexture == null || originalPixels == null || ResetSprite == null)
                {
                    return false;
                }

                return !SpriteLayoutsEqual(originalSprite, ResetSprite) ||
                       !PixelsEqual(workingTexture.GetPixels32(), originalPixels);
            }
        }

        public bool CanUndo
        {
            get { return historyIndex > 0; }
        }

        public bool CanRedo
        {
            get { return historyIndex < history.Count - 1; }
        }

        public bool HasPendingHitboxChanges()
        {
            if (workingTexture == null)
            {
                return false;
            }

            if (lastAppliedHitboxSnapshot == null)
            {
                return true;
            }

            return !CurrentMatchesSnapshot(lastAppliedHitboxSnapshot);
        }

        public void PrepareForEditing()
        {
            bool wasAssigned = IsAssignedToTarget();
            RebuildWorkingSprite(true);

            if (HasChanges || wasAssigned || IsAssignedToTarget())
            {
                EnsureAssigned();
            }
        }

        public void FinalizeForWorldDisplay()
        {
            bool wasAssigned = IsAssignedToTarget();
            RebuildWorkingSprite(false);

            if (HasChanges || wasAssigned || IsAssignedToTarget())
            {
                EnsureAssigned();
            }
        }

        public void AcceptCurrentAsSessionBase()
        {
            if (workingTexture == null || originalSprite == null)
            {
                return;
            }

            sessionBaseSnapshot = CaptureCurrentSnapshot();
            lastAppliedHitboxSnapshot = CaptureCurrentSnapshot();
        }

        public string HistoryDescription
        {
            get { return (historyIndex + 1) + " / " + history.Count; }
        }

        public void EnsureAssigned()
        {
            if (!IsValid)
            {
                return;
            }

            AssignSpriteToTarget(workingSprite);
        }

        private void AssignSpriteToTarget(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer physicalRenderer = physicalTarget != null ? physicalTarget.spriteRenderer : null;
            SpriteRenderer directRenderer = spriteRendererTarget != null ? spriteRendererTarget : physicalRenderer;
            if (directRenderer != null && directRenderer.sprite != sprite)
            {
                directRenderer.sprite = sprite;
            }

            if (physicalRenderer != null && physicalRenderer != directRenderer && physicalRenderer.sprite != sprite)
            {
                physicalRenderer.sprite = sprite;
            }

            SpriteRenderer skinRenderer = Limb != null && Limb.SkinMaterialHandler != null ? Limb.SkinMaterialHandler.renderer : null;
            if (skinRenderer != null && skinRenderer != physicalRenderer && skinRenderer.sprite != sprite)
            {
                skinRenderer.sprite = sprite;
            }

            if (Limb != null && Limb.SkinMaterialHandler != null)
            {
                Limb.SkinMaterialHandler.Sync();
            }

            if (physicalTarget != null)
            {
                physicalTarget.RefreshOutline();
            }
        }

        public Color GetPixel(int x, int y)
        {
            if (workingTexture == null)
            {
                return Color.clear;
            }

            return workingTexture.GetPixel(x, y);
        }

        public void BeginStroke()
        {
            if (workingTexture == null)
            {
                return;
            }

            strokeBasePixels = ClonePixels(workingTexture.GetPixels32());
        }

        public void EndStroke()
        {
            strokeBasePixels = null;
        }

        public bool PaintLine(Vector2Int from, Vector2Int to, Color color, int brushSize, bool erase)
        {
            return PaintLine(from, to, color, brushSize, erase, false);
        }

        public bool PaintLine(Vector2Int from, Vector2Int to, Color color, int brushSize, bool erase, bool opaqueOnly)
        {
            if (workingTexture == null)
            {
                return false;
            }

            PixelLayer layer = GetActivePixelLayer();
            if (layer == null || layer.Pixels == null || layer.Pixels.Length != workingTexture.width * workingTexture.height)
            {
                return false;
            }

            bool changed = false;
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                changed |= PaintCircle(layer, x0, y0, brushSize, color, erase, opaqueOnly);

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = err * 2;

                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            if (changed)
            {
                RecomposeWorkingTextureFromLayers(true);
            }

            return changed;
        }

        public bool FloodFill(int startX, int startY, Color replacementColor)
        {
            if (workingTexture == null)
            {
                return false;
            }

            PixelLayer layer = GetActivePixelLayer();
            if (layer == null || layer.Pixels == null || layer.Pixels.Length != workingTexture.width * workingTexture.height)
            {
                return false;
            }

            Color32 targetColor = workingTexture.GetPixel(startX, startY);
            if (targetColor.a <= 8)
            {
                return false;
            }

            Color32 fillColor = ResolvePaintColor(targetColor, replacementColor);

            if (ColorsEqual(targetColor, fillColor))
            {
                return false;
            }

            List<Vector2Int> queue = new List<Vector2Int>();
            int queueIndex = 0;
            bool[,] visited = new bool[workingTexture.width, workingTexture.height];
            bool changed = false;

            queue.Add(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while (queueIndex < queue.Count)
            {
                Vector2Int current = queue[queueIndex];
                queueIndex++;
                Color32 pixel = workingTexture.GetPixel(current.x, current.y);

                if (!ColorsClose(pixel, targetColor, FillColorTolerance))
                {
                    continue;
                }

                layer.Pixels[current.x + current.y * workingTexture.width] = fillColor;
                changed = true;

                TryEnqueue(queue, visited, current.x + 1, current.y);
                TryEnqueue(queue, visited, current.x - 1, current.y);
                TryEnqueue(queue, visited, current.x, current.y + 1);
                TryEnqueue(queue, visited, current.x, current.y - 1);
            }

            if (changed)
            {
                RecomposeWorkingTextureFromLayers(true);
            }

            return changed;
        }

        public void CommitCurrentPixels()
        {
            if (workingTexture == null)
            {
                return;
            }

            EndStroke();

            HistorySnapshot snapshot = CaptureCurrentSnapshot();
            HistorySnapshot currentHistory = history[historyIndex];

            if (SnapshotsEqual(snapshot, currentHistory))
            {
                return;
            }

            if (historyIndex < history.Count - 1)
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            }

            history.Add(snapshot);

            if (history.Count > MaxHistoryStates)
            {
                history.RemoveAt(0);
            }

            historyIndex = history.Count - 1;
        }

        public void Undo()
        {
            if (historyIndex <= 0)
            {
                return;
            }

            EndStroke();
            historyIndex--;
            ApplySnapshot(history[historyIndex]);
        }

        public void Redo()
        {
            if (historyIndex >= history.Count - 1)
            {
                return;
            }

            EndStroke();
            historyIndex++;
            ApplySnapshot(history[historyIndex]);
        }

        public void ResetToOriginal()
        {
            if (workingTexture == null || originalPixels == null || ResetSprite == null)
            {
                return;
            }

            EndStroke();
            ClearReplacementBodyLayerSnapshots();

            if (!SpriteLayoutsEqual(originalSprite, ResetSprite))
            {
                if (!RebuildWorkingState(ResetSprite, originalPixels))
                {
                    return;
                }
            }
            else
            {
                workingTexture.SetPixels32(ClonePixels(originalPixels));
                workingTexture.Apply();
                InitializePixelLayers(originalPixels, "Sprite");
                EnsureAssigned();
            }

            CommitCurrentPixels();
        }

        public bool ApplyPendingHitboxChanges()
        {
            if (!IsValid || !HasPendingHitboxChanges())
            {
                return false;
            }

            return ForceApplyHitboxChanges();
        }

        public bool ForceApplyHitboxChanges()
        {
            if (!IsValid)
            {
                return false;
            }

            List<Vector2[]> paths = BuildColliderPaths();
            bool applied = ApplyColliderShape(paths);

            if (applied)
            {
                lastAppliedHitboxSnapshot = CaptureCurrentSnapshot();
            }

            return applied;
        }

        public bool ReplaceWorkingPixels(Color32[] pixels, int width, int height)
        {
            if (workingTexture == null ||
                pixels == null ||
                width != workingTexture.width ||
                height != workingTexture.height ||
                pixels.Length != width * height)
            {
                return false;
            }

            EndStroke();
            workingTexture.SetPixels32(ClonePixels(pixels));
            workingTexture.Apply();
            InitializePixelLayers(pixels, "Sprite");
            EnsureAssigned();
            CommitCurrentPixels();
            return true;
        }

        public bool TryCopyPixels(RectInt rect, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (workingTexture == null)
            {
                return false;
            }

            RectInt safeRect = ClampPixelRect(rect, workingTexture.width, workingTexture.height);
            if (safeRect.width <= 0 || safeRect.height <= 0)
            {
                return false;
            }

            width = safeRect.width;
            height = safeRect.height;
            pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[x + y * width] = workingTexture.GetPixel(safeRect.x + x, safeRect.y + y);
                }
            }

            return true;
        }

        public bool TryCopyPixels(bool[] mask, int maskWidth, int maskHeight, out Color32[] pixels, out int width, out int height, out RectInt bounds)
        {
            pixels = null;
            width = 0;
            height = 0;
            bounds = new RectInt(0, 0, 0, 0);

            if (workingTexture == null || mask == null || maskWidth != workingTexture.width || maskHeight != workingTexture.height || mask.Length != maskWidth * maskHeight)
            {
                return false;
            }

            if (!TryGetMaskBounds(mask, maskWidth, maskHeight, out bounds))
            {
                return false;
            }

            width = bounds.width;
            height = bounds.height;
            pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sourceY = bounds.y + y;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = bounds.x + x;
                    int sourceIndex = sourceX + sourceY * maskWidth;
                    if (!mask[sourceIndex])
                    {
                        pixels[x + y * width] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    pixels[x + y * width] = workingTexture.GetPixel(sourceX, sourceY);
                }
            }

            return true;
        }

        public Color32[] GetActiveLayerPixelsSnapshot()
        {
            PixelLayer layer = GetActivePixelLayer();
            return layer != null && layer.Pixels != null ? ClonePixels(layer.Pixels) : null;
        }

        public bool TryCopyActiveLayerPixels(bool[] mask, int maskWidth, int maskHeight, out Color32[] pixels, out int width, out int height, out RectInt bounds)
        {
            pixels = null;
            width = 0;
            height = 0;
            bounds = new RectInt(0, 0, 0, 0);

            PixelLayer layer = GetActivePixelLayer();
            if (workingTexture == null || layer == null || layer.Pixels == null || mask == null || maskWidth != workingTexture.width || maskHeight != workingTexture.height || mask.Length != maskWidth * maskHeight)
            {
                return false;
            }

            if (!TryGetMaskBounds(mask, maskWidth, maskHeight, out bounds))
            {
                return false;
            }

            width = bounds.width;
            height = bounds.height;
            pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sourceY = bounds.y + y;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = bounds.x + x;
                    int sourceIndex = sourceX + sourceY * maskWidth;
                    pixels[x + y * width] = mask[sourceIndex] ? layer.Pixels[sourceIndex] : new Color32(0, 0, 0, 0);
                }
            }

            return true;
        }

        public bool ClearPixels(RectInt rect, bool commit)
        {
            if (workingTexture == null)
            {
                return false;
            }

            RectInt safeRect = ClampPixelRect(rect, workingTexture.width, workingTexture.height);
            if (safeRect.width <= 0 || safeRect.height <= 0)
            {
                return false;
            }

            bool changed = false;
            for (int y = safeRect.yMin; y < safeRect.yMax; y++)
            {
                for (int x = safeRect.xMin; x < safeRect.xMax; x++)
                {
                    Color32 existing = workingTexture.GetPixel(x, y);
                    if (existing.a == 0)
                    {
                        continue;
                    }

                    workingTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            workingTexture.Apply();
            EnsureAssigned();
            if (commit)
            {
                CommitCurrentPixels();
            }

            return true;
        }

        public bool ClearPixels(bool[] mask, int maskWidth, int maskHeight, bool commit)
        {
            PixelLayer layer = GetActivePixelLayer();
            if (workingTexture == null || layer == null || layer.Pixels == null || mask == null || maskWidth != workingTexture.width || maskHeight != workingTexture.height || mask.Length != maskWidth * maskHeight)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i] || layer.Pixels[i].a == 0)
                {
                    continue;
                }

                layer.Pixels[i] = new Color32(0, 0, 0, 0);
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            RecomposeWorkingTextureFromLayers(true);
            if (commit)
            {
                CommitCurrentPixels();
            }

            return true;
        }

        public bool PastePixels(Color32[] pixels, int width, int height, int targetX, int targetY, bool commit)
        {
            if (workingTexture == null ||
                pixels == null ||
                width <= 0 ||
                height <= 0 ||
                pixels.Length != width * height)
            {
                return false;
            }

            bool changed = false;
            for (int y = 0; y < height; y++)
            {
                int destinationY = targetY + y;
                if (destinationY < 0 || destinationY >= workingTexture.height)
                {
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int destinationX = targetX + x;
                    if (destinationX < 0 || destinationX >= workingTexture.width)
                    {
                        continue;
                    }

                    Color32 source = pixels[x + y * width];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    Color32 existing = workingTexture.GetPixel(destinationX, destinationY);
                    if (ColorsEqual(existing, source))
                    {
                        continue;
                    }

                    workingTexture.SetPixel(destinationX, destinationY, source);
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            workingTexture.Apply();
            EnsureAssigned();
            if (commit)
            {
                CommitCurrentPixels();
            }

            return true;
        }

        public bool PastePixelsAsLayer(Color32[] pixels, int width, int height, int targetX, int targetY, out RectInt pastedBounds)
        {
            pastedBounds = new RectInt(0, 0, 0, 0);

            if (workingTexture == null ||
                pixels == null ||
                width <= 0 ||
                height <= 0 ||
                pixels.Length != width * height)
            {
                return false;
            }

            EnsurePixelLayers();
            PixelLayer layer = new PixelLayer();
            layer.Name = "Paste " + pasteLayerSerial;
            pasteLayerSerial++;
            layer.Visible = true;
            layer.Pixels = new Color32[workingTexture.width * workingTexture.height];

            bool changed = false;
            int minX = workingTexture.width;
            int minY = workingTexture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int destinationY = targetY + y;
                if (destinationY < 0 || destinationY >= workingTexture.height)
                {
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int destinationX = targetX + x;
                    if (destinationX < 0 || destinationX >= workingTexture.width)
                    {
                        continue;
                    }

                    Color32 source = pixels[x + y * width];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    layer.Pixels[destinationX + destinationY * workingTexture.width] = source;
                    minX = Mathf.Min(minX, destinationX);
                    minY = Mathf.Min(minY, destinationY);
                    maxX = Mathf.Max(maxX, destinationX);
                    maxY = Mathf.Max(maxY, destinationY);
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            pixelLayers.Insert(0, layer);
            activeLayerIndex = 0;
            pastedBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            RecomposeWorkingTextureFromLayers(true);
            CommitCurrentPixels();
            return true;
        }

        public bool PreviewMovePixels(Color32[] basePixels, RectInt originalRect, Color32[] pixels, int width, int height, int targetX, int targetY)
        {
            if (workingTexture == null ||
                basePixels == null ||
                pixels == null ||
                basePixels.Length != workingTexture.width * workingTexture.height ||
                width <= 0 ||
                height <= 0 ||
                pixels.Length != width * height)
            {
                return false;
            }

            Color32[] next = ClonePixels(basePixels);
            RectInt safeOriginal = ClampPixelRect(originalRect, workingTexture.width, workingTexture.height);
            for (int y = safeOriginal.yMin; y < safeOriginal.yMax; y++)
            {
                for (int x = safeOriginal.xMin; x < safeOriginal.xMax; x++)
                {
                    next[x + y * workingTexture.width] = new Color32(0, 0, 0, 0);
                }
            }

            for (int y = 0; y < height; y++)
            {
                int destinationY = targetY + y;
                if (destinationY < 0 || destinationY >= workingTexture.height)
                {
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int destinationX = targetX + x;
                    if (destinationX < 0 || destinationX >= workingTexture.width)
                    {
                        continue;
                    }

                    Color32 source = pixels[x + y * width];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    next[destinationX + destinationY * workingTexture.width] = source;
                }
            }

            workingTexture.SetPixels32(next);
            workingTexture.Apply();
            EnsureAssigned();
            return true;
        }

        public bool PreviewMoveActiveLayerPixels(Color32[] baseLayerPixels, bool[] mask, int maskWidth, int maskHeight, RectInt originalBounds, Color32[] pixels, int width, int height, int targetX, int targetY)
        {
            PixelLayer layer = GetActivePixelLayer();
            if (workingTexture == null ||
                layer == null ||
                layer.Pixels == null ||
                baseLayerPixels == null ||
                mask == null ||
                pixels == null ||
                maskWidth != workingTexture.width ||
                maskHeight != workingTexture.height ||
                mask.Length != maskWidth * maskHeight ||
                baseLayerPixels.Length != workingTexture.width * workingTexture.height ||
                width <= 0 ||
                height <= 0 ||
                pixels.Length != width * height)
            {
                return false;
            }

            Color32[] next = ClonePixels(baseLayerPixels);
            RectInt safeOriginal = ClampPixelRect(originalBounds, workingTexture.width, workingTexture.height);
            for (int y = safeOriginal.yMin; y < safeOriginal.yMax; y++)
            {
                for (int x = safeOriginal.xMin; x < safeOriginal.xMax; x++)
                {
                    int index = x + y * maskWidth;
                    if (index >= 0 && index < mask.Length && mask[index])
                    {
                        next[x + y * workingTexture.width] = new Color32(0, 0, 0, 0);
                    }
                }
            }

            for (int y = 0; y < height; y++)
            {
                int destinationY = targetY + y;
                if (destinationY < 0 || destinationY >= workingTexture.height)
                {
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int destinationX = targetX + x;
                    if (destinationX < 0 || destinationX >= workingTexture.width)
                    {
                        continue;
                    }

                    Color32 source = pixels[x + y * width];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    next[destinationX + destinationY * workingTexture.width] = source;
                }
            }

            layer.Pixels = next;
            RecomposeWorkingTextureFromLayers(true);
            return true;
        }

        private static bool TryGetMaskBounds(bool[] mask, int width, int height, out RectInt bounds)
        {
            bounds = new RectInt(0, 0, 0, 0);
            if (mask == null || width <= 0 || height <= 0 || mask.Length != width * height)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[x + y * width])
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static RectInt ClampPixelRect(RectInt rect, int width, int height)
        {
            if (width <= 0 || height <= 0 || rect.width <= 0 || rect.height <= 0)
            {
                return new RectInt(0, 0, 0, 0);
            }

            int xMin = Mathf.Clamp(rect.xMin, 0, width);
            int yMin = Mathf.Clamp(rect.yMin, 0, height);
            int xMax = Mathf.Clamp(rect.xMax, 0, width);
            int yMax = Mathf.Clamp(rect.yMax, 0, height);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        public bool ResizeCanvas(int newWidth, int newHeight)
        {
            if (workingTexture == null || originalSprite == null)
            {
                return false;
            }

            newWidth = Mathf.Clamp(newWidth, 1, 512);
            newHeight = Mathf.Clamp(newHeight, 1, 512);

            int oldWidth = workingTexture.width;
            int oldHeight = workingTexture.height;
            if (newWidth == oldWidth && newHeight == oldHeight)
            {
                return true;
            }

            Color32[] oldPixels = ClonePixels(workingTexture.GetPixels32());
            int offsetX;
            int offsetY;
            GetCenteredVisibleResizeOffset(oldPixels, oldWidth, oldHeight, newWidth, newHeight, out offsetX, out offsetY);
            return ResizeCanvas(newWidth, newHeight, offsetX, offsetY);
        }

        public bool ResizeCanvas(int newWidth, int newHeight, int offsetX, int offsetY)
        {
            if (workingTexture == null || originalSprite == null)
            {
                return false;
            }

            newWidth = Mathf.Clamp(newWidth, 1, 512);
            newHeight = Mathf.Clamp(newHeight, 1, 512);

            int oldWidth = workingTexture.width;
            int oldHeight = workingTexture.height;
            if (newWidth == oldWidth && newHeight == oldHeight)
            {
                return true;
            }

            Color32[] oldPixels = ClonePixels(workingTexture.GetPixels32());
            Color32[] resizedPixels = ResizePixelsCentered(oldPixels, oldWidth, oldHeight, newWidth, newHeight, offsetX, offsetY);
            int pivotX = Limb != null
                ? Mathf.RoundToInt(newWidth * 0.5f)
                : Mathf.Clamp(Mathf.RoundToInt(originalSprite.pivot.x) + offsetX, 0, newWidth);
            int pivotY = Limb != null
                ? Mathf.RoundToInt(newHeight * 0.5f)
                : Mathf.Clamp(Mathf.RoundToInt(originalSprite.pivot.y) + offsetY, 0, newHeight);
            float pixelsPerUnit = originalSprite.pixelsPerUnit;

            Color32[] fleshPixels;
            int fleshWidth;
            int fleshHeight;
            Color32[] bonePixels;
            int boneWidth;
            int boneHeight;
            Color32[] damagePixels;
            int damageWidth;
            int damageHeight;
            if (TryCreateResizedBodyLayerSnapshots(
                oldPixels,
                oldWidth,
                oldHeight,
                resizedPixels,
                newWidth,
                newHeight,
                offsetX,
                offsetY,
                out fleshPixels,
                out fleshWidth,
                out fleshHeight,
                out bonePixels,
                out boneWidth,
                out boneHeight))
            {
                bool hasDamage = TryCreateResizedDamageLayerSnapshot(
                    oldPixels,
                    oldWidth,
                    oldHeight,
                    resizedPixels,
                    newWidth,
                    newHeight,
                    offsetX,
                    offsetY,
                    out damagePixels,
                    out damageWidth,
                    out damageHeight);

                return ReplaceWorkingSprite(
                    resizedPixels,
                    newWidth,
                    newHeight,
                    pivotX,
                    pivotY,
                    pixelsPerUnit,
                    fleshPixels,
                    fleshWidth,
                    fleshHeight,
                    bonePixels,
                    boneWidth,
                    boneHeight,
                    hasDamage ? damagePixels : null,
                    hasDamage ? damageWidth : 0,
                    hasDamage ? damageHeight : 0);
            }

            return ReplaceWorkingSprite(resizedPixels, newWidth, newHeight, pivotX, pivotY, pixelsPerUnit);
        }

        private static Color32[] ResizePixelsCentered(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            int offsetX;
            int offsetY;
            GetCenteredVisibleResizeOffset(source, sourceWidth, sourceHeight, targetWidth, targetHeight, out offsetX, out offsetY);
            return ResizePixelsCentered(source, sourceWidth, sourceHeight, targetWidth, targetHeight, offsetX, offsetY);
        }

        private static void GetCenteredVisibleResizeOffset(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, out int offsetX, out int offsetY)
        {
            offsetX = Mathf.RoundToInt((targetWidth - sourceWidth) * 0.5f);
            offsetY = Mathf.RoundToInt((targetHeight - sourceHeight) * 0.5f);

            RectInt visibleBounds;
            if (!TryGetVisibleBounds(source, sourceWidth, sourceHeight, out visibleBounds))
            {
                return;
            }

            int targetVisibleX = Mathf.RoundToInt((targetWidth - visibleBounds.width) * 0.5f);
            int targetVisibleY = Mathf.RoundToInt((targetHeight - visibleBounds.height) * 0.5f);
            offsetX = targetVisibleX - visibleBounds.xMin;
            offsetY = targetVisibleY - visibleBounds.yMin;
        }

        private static bool TryGetVisibleBounds(Color32[] pixels, int width, int height, out RectInt bounds)
        {
            bounds = new RectInt(0, 0, 0, 0);
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 0)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static Color32[] ResizePixelsCentered(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int offsetX, int offsetY)
        {
            Color32[] result = new Color32[targetWidth * targetHeight];
            if (source == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                targetWidth <= 0 ||
                targetHeight <= 0 ||
                source.Length != sourceWidth * sourceHeight)
            {
                return result;
            }

            for (int y = 0; y < sourceHeight; y++)
            {
                int targetY = y + offsetY;
                if (targetY < 0 || targetY >= targetHeight)
                {
                    continue;
                }

                for (int x = 0; x < sourceWidth; x++)
                {
                    int targetX = x + offsetX;
                    if (targetX < 0 || targetX >= targetWidth)
                    {
                        continue;
                    }

                    result[targetY * targetWidth + targetX] = source[y * sourceWidth + x];
                }
            }

            return result;
        }

        private bool TryCreateResizedBodyLayerSnapshots(
            Color32[] oldPixels,
            int oldWidth,
            int oldHeight,
            Color32[] resizedPixels,
            int newWidth,
            int newHeight,
            int offsetX,
            int offsetY,
            out Color32[] fleshPixels,
            out int fleshWidth,
            out int fleshHeight,
            out Color32[] bonePixels,
            out int boneWidth,
            out int boneHeight)
        {
            fleshPixels = null;
            fleshWidth = 0;
            fleshHeight = 0;
            bonePixels = null;
            boneWidth = 0;
            boneHeight = 0;

            if (Limb == null ||
                oldPixels == null ||
                resizedPixels == null ||
                oldWidth <= 0 ||
                oldHeight <= 0 ||
                newWidth <= 0 ||
                newHeight <= 0 ||
                oldPixels.Length != oldWidth * oldHeight ||
                resizedPixels.Length != newWidth * newHeight)
            {
                return false;
            }

            Color32[] sourceFleshPixels;
            int sourceFleshWidth;
            int sourceFleshHeight;
            Color32[] sourceBonePixels;
            int sourceBoneWidth;
            int sourceBoneHeight;
            if (!TryGetBodyLayerSnapshots(
                out sourceFleshPixels,
                out sourceFleshWidth,
                out sourceFleshHeight,
                out sourceBonePixels,
                out sourceBoneWidth,
                out sourceBoneHeight) ||
                sourceFleshPixels == null ||
                sourceBonePixels == null ||
                sourceFleshWidth <= 0 ||
                sourceFleshHeight <= 0 ||
                sourceBoneWidth <= 0 ||
                sourceBoneHeight <= 0)
            {
                return false;
            }

            fleshWidth = newWidth;
            fleshHeight = newHeight;
            boneWidth = newWidth;
            boneHeight = newHeight;
            fleshPixels = new Color32[newWidth * newHeight];
            bonePixels = new Color32[newWidth * newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int destinationIndex = y * newWidth + x;
                    if (resizedPixels[destinationIndex].a <= 0)
                    {
                        continue;
                    }

                    int sourceX = x - offsetX;
                    int sourceY = y - offsetY;
                    if (sourceX < 0 || sourceY < 0 || sourceX >= oldWidth || sourceY >= oldHeight)
                    {
                        continue;
                    }

                    int sourceIndex = sourceY * oldWidth + sourceX;
                    if (oldPixels[sourceIndex].a <= 0)
                    {
                        continue;
                    }

                    int fleshX;
                    int fleshY;
                    int boneX;
                    int boneY;
                    MapSpriteLocalPixelToLayerPixel(sourceX, sourceY, oldWidth, oldHeight, sourceFleshWidth, sourceFleshHeight, out fleshX, out fleshY);
                    MapSpriteLocalPixelToLayerPixel(sourceX, sourceY, oldWidth, oldHeight, sourceBoneWidth, sourceBoneHeight, out boneX, out boneY);

                    fleshPixels[destinationIndex] = sourceFleshPixels[Mathf.Clamp(fleshY, 0, sourceFleshHeight - 1) * sourceFleshWidth + Mathf.Clamp(fleshX, 0, sourceFleshWidth - 1)];
                    bonePixels[destinationIndex] = sourceBonePixels[Mathf.Clamp(boneY, 0, sourceBoneHeight - 1) * sourceBoneWidth + Mathf.Clamp(boneX, 0, sourceBoneWidth - 1)];
                }
            }

            return true;
        }

        private bool TryCreateResizedDamageLayerSnapshot(
            Color32[] oldPixels,
            int oldWidth,
            int oldHeight,
            Color32[] resizedPixels,
            int newWidth,
            int newHeight,
            int offsetX,
            int offsetY,
            out Color32[] damagePixels,
            out int damageWidth,
            out int damageHeight)
        {
            damagePixels = null;
            damageWidth = 0;
            damageHeight = 0;

            if (oldPixels == null ||
                resizedPixels == null ||
                oldWidth <= 0 ||
                oldHeight <= 0 ||
                newWidth <= 0 ||
                newHeight <= 0 ||
                oldPixels.Length != oldWidth * oldHeight ||
                resizedPixels.Length != newWidth * newHeight)
            {
                return false;
            }

            Color32[] sourceDamagePixels;
            int sourceDamageWidth;
            int sourceDamageHeight;
            if (!TryGetDamageLayerSnapshot(out sourceDamagePixels, out sourceDamageWidth, out sourceDamageHeight) ||
                sourceDamagePixels == null ||
                sourceDamageWidth <= 0 ||
                sourceDamageHeight <= 0)
            {
                return false;
            }

            damageWidth = newWidth;
            damageHeight = newHeight;
            damagePixels = new Color32[newWidth * newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int destinationIndex = y * newWidth + x;
                    if (resizedPixels[destinationIndex].a <= 0)
                    {
                        continue;
                    }

                    int sourceX = x - offsetX;
                    int sourceY = y - offsetY;
                    if (sourceX < 0 || sourceY < 0 || sourceX >= oldWidth || sourceY >= oldHeight)
                    {
                        continue;
                    }

                    int sourceIndex = sourceY * oldWidth + sourceX;
                    if (oldPixels[sourceIndex].a <= 0)
                    {
                        continue;
                    }

                    int damageX;
                    int damageY;
                    MapSpriteLocalPixelToLayerPixel(sourceX, sourceY, oldWidth, oldHeight, sourceDamageWidth, sourceDamageHeight, out damageX, out damageY);
                    damagePixels[destinationIndex] = sourceDamagePixels[Mathf.Clamp(damageY, 0, sourceDamageHeight - 1) * sourceDamageWidth + Mathf.Clamp(damageX, 0, sourceDamageWidth - 1)];
                }
            }

            return true;
        }

        private void MapSpriteLocalPixelToLayerPixel(int localX, int localY, int localWidth, int localHeight, int layerWidth, int layerHeight, out int layerX, out int layerY)
        {
            layerX = localX;
            layerY = localY;

            if (layerWidth == localWidth && layerHeight == localHeight)
            {
                return;
            }

            if (originalSprite != null)
            {
                RectInt region = GetOriginalTextureRegion(originalSprite);
                if (region.x >= 0 &&
                    region.y >= 0 &&
                    region.x + localWidth <= layerWidth &&
                    region.y + localHeight <= layerHeight)
                {
                    layerX = region.x + localX;
                    layerY = region.y + localY;
                    return;
                }
            }

            layerX = MapCoordinate(localX, 0, localWidth, 0, layerWidth);
            layerY = MapCoordinate(localY, 0, localHeight, 0, layerHeight);
        }

        private static int MapCoordinate(int value, int sourceStart, int sourceLength, int targetStart, int targetLength)
        {
            if (sourceLength <= 1 || targetLength <= 1)
            {
                return targetStart;
            }

            float normalized = (value - sourceStart) / (float)(sourceLength - 1);
            return targetStart + Mathf.RoundToInt(Mathf.Clamp01(normalized) * (targetLength - 1));
        }

        public bool ReplaceWorkingSprite(Color32[] pixels, int width, int height, int pivotX, int pivotY, float pixelsPerUnit)
        {
            if (pixels == null ||
                width <= 0 ||
                height <= 0 ||
                pixels.Length != width * height)
            {
                return false;
            }

            EndStroke();

            Sprite basisSprite = CreateOwnedBasisSprite(
                width,
                height,
                pivotX,
                pivotY,
                pixelsPerUnit,
                pixels,
                (Limb != null ? Limb.name : (physicalTarget != null ? physicalTarget.name : "Sprite")) + "_ClipboardBasis");

            if (basisSprite == null)
            {
                return false;
            }

            if (!RebuildWorkingState(basisSprite, pixels))
            {
                return false;
            }

            lastAppliedHitboxSnapshot = null;
            CommitCurrentPixels();
            return true;
        }

        public bool ReplaceWorkingSprite(
            Color32[] pixels,
            int width,
            int height,
            int pivotX,
            int pivotY,
            float pixelsPerUnit,
            Color32[] fleshPixels,
            int fleshWidth,
            int fleshHeight,
            Color32[] bonePixels,
            int boneWidth,
            int boneHeight)
        {
            return ReplaceWorkingSprite(
                pixels,
                width,
                height,
                pivotX,
                pivotY,
                pixelsPerUnit,
                fleshPixels,
                fleshWidth,
                fleshHeight,
                bonePixels,
                boneWidth,
                boneHeight,
                null,
                0,
                0);
        }

        public bool ReplaceWorkingSprite(
            Color32[] pixels,
            int width,
            int height,
            int pivotX,
            int pivotY,
            float pixelsPerUnit,
            Color32[] fleshPixels,
            int fleshWidth,
            int fleshHeight,
            Color32[] bonePixels,
            int boneWidth,
            int boneHeight,
            Color32[] damagePixels,
            int damageWidth,
            int damageHeight)
        {
            if (!ReplaceWorkingSprite(pixels, width, height, pivotX, pivotY, pixelsPerUnit))
            {
                return false;
            }

            SetReplacementBodyLayerSnapshots(fleshPixels, fleshWidth, fleshHeight, bonePixels, boneWidth, boneHeight);
            SetReplacementDamageLayerSnapshot(damagePixels, damageWidth, damageHeight);
            return true;
        }

        public bool TryWriteCurrentPixelsToAtlas(Texture2D atlasTexture)
        {
            if (atlasTexture == null || workingTexture == null || originalSprite == null)
            {
                return false;
            }

            RectInt region = GetOriginalTextureRegion(originalSprite);
            if (region.width != workingTexture.width || region.height != workingTexture.height)
            {
                return false;
            }

            atlasTexture.SetPixels(
                region.x,
                region.y,
                region.width,
                region.height,
                workingTexture.GetPixels(0, 0, region.width, region.height));
            return true;
        }

        public bool TryCreateCommittedDisplay(out Texture2D atlasTexture, out Sprite sprite)
        {
            atlasTexture = null;
            sprite = null;

            if (workingTexture == null || originalSprite == null)
            {
                return false;
            }

            atlasTexture = CreateAtlasDisplayTexture(originalSprite, workingTexture);
            if (atlasTexture == null)
            {
                return false;
            }

            sprite = CreateAtlasDisplaySprite(originalSprite, atlasTexture);
            if (sprite == null)
            {
                UnityEngine.Object.Destroy(atlasTexture);
                atlasTexture = null;
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            RestoreAssignedSpriteBeforeDispose();

            usingPreviewSprite = true;
            lastAppliedHitboxSnapshot = null;
            sessionBaseSnapshot = null;
            strokeBasePixels = null;
            pixelLayers.Clear();
            ClearReplacementBodyLayerSnapshots();

            if (workingSprite != null)
            {
                UnityEngine.Object.Destroy(workingSprite);
                workingSprite = null;
            }

            if (workingTexture != null)
            {
                UnityEngine.Object.Destroy(workingTexture);
                workingTexture = null;
            }

            if (displayTexture != null)
            {
                UnityEngine.Object.Destroy(displayTexture);
                displayTexture = null;
            }

            for (int i = 0; i < ownedBasisSprites.Count; i++)
            {
                if (ownedBasisSprites[i] != null)
                {
                    UnityEngine.Object.Destroy(ownedBasisSprites[i]);
                }
            }

            ownedBasisSprites.Clear();

            for (int i = 0; i < ownedBasisTextures.Count; i++)
            {
                if (ownedBasisTextures[i] != null)
                {
                    UnityEngine.Object.Destroy(ownedBasisTextures[i]);
                }
            }

            ownedBasisTextures.Clear();
        }

        private void RestoreAssignedSpriteBeforeDispose()
        {
            if (!IsAssignedToTarget())
            {
                return;
            }

            Sprite fallback = resetSprite != null ? resetSprite : originalSprite;
            if (fallback == null || fallback == workingSprite)
            {
                return;
            }

            AssignSpriteToTarget(fallback);
        }

        private void ApplySnapshot(HistorySnapshot snapshot)
        {
            if (snapshot == null || snapshot.BasisSprite == null || snapshot.Pixels == null)
            {
                return;
            }

            if (replacementBodyLayerBasisSprite != null && replacementBodyLayerBasisSprite != snapshot.BasisSprite)
            {
                ClearReplacementBodyLayerSnapshots();
            }

            if (!SpriteLayoutsEqual(originalSprite, snapshot.BasisSprite) ||
                workingTexture == null ||
                snapshot.Pixels.Length != workingTexture.width * workingTexture.height)
            {
                RebuildWorkingState(snapshot.BasisSprite, snapshot.Pixels);
                RestoreLayerSnapshots(snapshot);
                RecomposeWorkingTextureFromLayers(true);
                return;
            }

            workingTexture.SetPixels32(ClonePixels(snapshot.Pixels));
            workingTexture.Apply();
            RestoreLayerSnapshots(snapshot);
            RecomposeWorkingTextureFromLayers(false);
            EnsureAssigned();
        }

        private void RebuildWorkingSprite(bool previewMode)
        {
            if (workingTexture == null || originalSprite == null)
            {
                return;
            }

            if (workingSprite != null && usingPreviewSprite == previewMode)
            {
                return;
            }

            bool wasAssigned = IsAssignedToTarget();
            Sprite previousSprite = workingSprite;
            Texture2D previousDisplayTexture = displayTexture;
            Texture2D spriteTexture = workingTexture;
            Sprite rebuilt = null;

            if (previewMode)
            {
                rebuilt = CreatePreviewSprite(originalSprite, spriteTexture);
            }
            else
            {
                spriteTexture = CreateAtlasDisplayTexture(originalSprite, workingTexture);
                rebuilt = CreateAtlasDisplaySprite(originalSprite, spriteTexture);
            }

            if (rebuilt == null)
            {
                if (!previewMode && spriteTexture != null && spriteTexture != workingTexture)
                {
                    UnityEngine.Object.Destroy(spriteTexture);
                }

                return;
            }

            workingSprite = rebuilt;
            usingPreviewSprite = previewMode;
            displayTexture = previewMode ? null : spriteTexture;

            if (wasAssigned)
            {
                EnsureAssigned();
            }

            if (previousSprite != null)
            {
                UnityEngine.Object.Destroy(previousSprite);
            }

            if (previousDisplayTexture != null)
            {
                UnityEngine.Object.Destroy(previousDisplayTexture);
            }
        }

        private HistorySnapshot CaptureCurrentSnapshot()
        {
            if (workingTexture == null || originalSprite == null)
            {
                return null;
            }

            HistorySnapshot snapshot = CreateSnapshot(originalSprite, workingTexture.GetPixels32());
            if (snapshot != null)
            {
                snapshot.Layers = CaptureLayerSnapshots();
                snapshot.ActiveLayerIndex = activeLayerIndex;
                snapshot.PasteLayerSerial = pasteLayerSerial;
            }

            return snapshot;
        }

        private static HistorySnapshot CreateSnapshot(Sprite basisSprite, Color32[] pixels)
        {
            if (basisSprite == null || pixels == null)
            {
                return null;
            }

            HistorySnapshot snapshot = new HistorySnapshot();
            snapshot.BasisSprite = basisSprite;
            snapshot.Pixels = ClonePixels(pixels);
            return snapshot;
        }

        private LayerSnapshot[] CaptureLayerSnapshots()
        {
            EnsurePixelLayers();
            LayerSnapshot[] snapshots = new LayerSnapshot[pixelLayers.Count];
            for (int i = 0; i < pixelLayers.Count; i++)
            {
                PixelLayer layer = pixelLayers[i];
                LayerSnapshot snapshot = new LayerSnapshot();
                snapshot.Name = layer != null ? layer.Name : "Layer " + (i + 1);
                snapshot.Pixels = layer != null ? ClonePixels(layer.Pixels) : null;
                snapshot.Visible = layer == null || layer.Visible;
                snapshots[i] = snapshot;
            }

            return snapshots;
        }

        private void RestoreLayerSnapshots(HistorySnapshot snapshot)
        {
            pixelLayers.Clear();

            if (snapshot != null && snapshot.Layers != null)
            {
                int expectedLength = workingTexture != null ? workingTexture.width * workingTexture.height : 0;
                for (int i = 0; i < snapshot.Layers.Length; i++)
                {
                    LayerSnapshot layerSnapshot = snapshot.Layers[i];
                    if (layerSnapshot == null || layerSnapshot.Pixels == null || layerSnapshot.Pixels.Length != expectedLength)
                    {
                        continue;
                    }

                    PixelLayer layer = new PixelLayer();
                    layer.Name = string.IsNullOrEmpty(layerSnapshot.Name) ? "Layer " + (i + 1) : layerSnapshot.Name;
                    layer.Pixels = ClonePixels(layerSnapshot.Pixels);
                    layer.Visible = layerSnapshot.Visible;
                    pixelLayers.Add(layer);
                }

                pasteLayerSerial = Mathf.Max(1, snapshot.PasteLayerSerial);
            }

            if (pixelLayers.Count <= 0 && snapshot != null && snapshot.Pixels != null)
            {
                InitializePixelLayers(snapshot.Pixels, "Sprite");
            }

            activeLayerIndex = Mathf.Clamp(snapshot != null ? snapshot.ActiveLayerIndex : 0, 0, Mathf.Max(0, pixelLayers.Count - 1));
        }

        private bool CurrentMatchesSnapshot(HistorySnapshot snapshot)
        {
            return SnapshotsEqual(CaptureCurrentSnapshot(), snapshot);
        }

        private static bool SnapshotsEqual(HistorySnapshot left, HistorySnapshot right)
        {
            return left != null &&
                   right != null &&
                   SpriteLayoutsEqual(left.BasisSprite, right.BasisSprite) &&
                   PixelsEqual(left.Pixels, right.Pixels) &&
                   LayerSnapshotsEqual(left, right);
        }

        private static bool LayerSnapshotsEqual(HistorySnapshot left, HistorySnapshot right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.ActiveLayerIndex != right.ActiveLayerIndex || left.PasteLayerSerial != right.PasteLayerSerial)
            {
                return false;
            }

            if (left.Layers == null || right.Layers == null)
            {
                return left.Layers == null && right.Layers == null;
            }

            if (left.Layers.Length != right.Layers.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Layers.Length; i++)
            {
                LayerSnapshot leftLayer = left.Layers[i];
                LayerSnapshot rightLayer = right.Layers[i];
                if (leftLayer == null || rightLayer == null)
                {
                    if (leftLayer != rightLayer)
                    {
                        return false;
                    }

                    continue;
                }

                if (!string.Equals(leftLayer.Name, rightLayer.Name, StringComparison.Ordinal) ||
                    leftLayer.Visible != rightLayer.Visible ||
                    !PixelsEqual(leftLayer.Pixels, rightLayer.Pixels))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SpriteLayoutsEqual(Sprite left, Sprite right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            Rect leftRect = left.rect;
            Rect rightRect = right.rect;
            return Mathf.RoundToInt(leftRect.width) == Mathf.RoundToInt(rightRect.width) &&
                   Mathf.RoundToInt(leftRect.height) == Mathf.RoundToInt(rightRect.height) &&
                   Mathf.RoundToInt(left.pivot.x) == Mathf.RoundToInt(right.pivot.x) &&
                   Mathf.RoundToInt(left.pivot.y) == Mathf.RoundToInt(right.pivot.y) &&
                   Mathf.RoundToInt(left.pixelsPerUnit * 100f) == Mathf.RoundToInt(right.pixelsPerUnit * 100f);
        }

        private bool RebuildWorkingState(Sprite basisSprite, Color32[] pixels)
        {
            if (basisSprite == null || pixels == null)
            {
                return false;
            }

            int width = Mathf.RoundToInt(basisSprite.rect.width);
            int height = Mathf.RoundToInt(basisSprite.rect.height);
            if (width <= 0 || height <= 0 || pixels.Length != width * height)
            {
                return false;
            }

            Texture2D nextWorkingTexture = CreateTextureFromPixels(width, height, pixels);
            if (nextWorkingTexture == null)
            {
                return false;
            }

            Sprite nextWorkingSprite = CreatePreviewSprite(basisSprite, nextWorkingTexture);
            if (nextWorkingSprite == null)
            {
                UnityEngine.Object.Destroy(nextWorkingTexture);
                return false;
            }

            bool wasAssigned = IsAssignedToTarget();
            Texture2D previousWorkingTexture = workingTexture;
            Texture2D previousDisplayTexture = displayTexture;
            Sprite previousWorkingSprite = workingSprite;

            originalSprite = basisSprite;
            workingTexture = nextWorkingTexture;
            workingSprite = nextWorkingSprite;
            displayTexture = null;
            usingPreviewSprite = true;
            InitializePixelLayers(pixels, "Sprite");

            if (wasAssigned)
            {
                EnsureAssigned();
            }

            if (previousWorkingSprite != null)
            {
                UnityEngine.Object.Destroy(previousWorkingSprite);
            }

            if (previousDisplayTexture != null)
            {
                UnityEngine.Object.Destroy(previousDisplayTexture);
            }

            if (previousWorkingTexture != null)
            {
                UnityEngine.Object.Destroy(previousWorkingTexture);
            }

            return true;
        }

        private void SetReplacementBodyLayerSnapshots(
            Color32[] fleshPixels,
            int fleshWidth,
            int fleshHeight,
            Color32[] bonePixels,
            int boneWidth,
            int boneHeight)
        {
            ClearReplacementBodyLayerSnapshots();

            if (fleshPixels == null ||
                bonePixels == null ||
                fleshWidth <= 0 ||
                fleshHeight <= 0 ||
                boneWidth <= 0 ||
                boneHeight <= 0 ||
                fleshPixels.Length != fleshWidth * fleshHeight ||
                bonePixels.Length != boneWidth * boneHeight)
            {
                return;
            }

            replacementBodyLayerBasisSprite = originalSprite;
            replacementFleshPixels = ClonePixels(fleshPixels);
            replacementFleshWidth = fleshWidth;
            replacementFleshHeight = fleshHeight;
            replacementBonePixels = ClonePixels(bonePixels);
            replacementBoneWidth = boneWidth;
            replacementBoneHeight = boneHeight;
        }

        private void SetReplacementDamageLayerSnapshot(Color32[] damagePixels, int damageWidth, int damageHeight)
        {
            replacementDamagePixels = null;
            replacementDamageWidth = 0;
            replacementDamageHeight = 0;

            if (damagePixels == null ||
                damageWidth <= 0 ||
                damageHeight <= 0 ||
                damagePixels.Length != damageWidth * damageHeight)
            {
                return;
            }

            replacementBodyLayerBasisSprite = originalSprite;
            replacementDamagePixels = ClonePixels(damagePixels);
            replacementDamageWidth = damageWidth;
            replacementDamageHeight = damageHeight;
        }

        private void ClearReplacementBodyLayerSnapshots()
        {
            replacementBodyLayerBasisSprite = null;
            replacementFleshPixels = null;
            replacementBonePixels = null;
            replacementDamagePixels = null;
            replacementFleshWidth = 0;
            replacementFleshHeight = 0;
            replacementBoneWidth = 0;
            replacementBoneHeight = 0;
            replacementDamageWidth = 0;
            replacementDamageHeight = 0;
        }

        private bool HasReplacementBodyLayerSnapshots()
        {
            return replacementBodyLayerBasisSprite != null &&
                   replacementBodyLayerBasisSprite == originalSprite &&
                   replacementFleshPixels != null &&
                   replacementBonePixels != null &&
                   replacementFleshWidth > 0 &&
                   replacementFleshHeight > 0 &&
                   replacementBoneWidth > 0 &&
                   replacementBoneHeight > 0 &&
                   replacementFleshPixels.Length == replacementFleshWidth * replacementFleshHeight &&
                   replacementBonePixels.Length == replacementBoneWidth * replacementBoneHeight;
        }

        private bool HasReplacementDamageLayerSnapshot()
        {
            return replacementBodyLayerBasisSprite != null &&
                   replacementBodyLayerBasisSprite == originalSprite &&
                   replacementDamagePixels != null &&
                   replacementDamageWidth > 0 &&
                   replacementDamageHeight > 0 &&
                   replacementDamagePixels.Length == replacementDamageWidth * replacementDamageHeight;
        }

        private Material GetTargetMaterial()
        {
            if (Limb != null && Limb.SkinMaterialHandler != null && Limb.SkinMaterialHandler.renderer != null)
            {
                return Limb.SkinMaterialHandler.renderer.material;
            }

            if (spriteRendererTarget != null)
            {
                return spriteRendererTarget.material;
            }

            if (physicalTarget != null && physicalTarget.spriteRenderer != null)
            {
                return physicalTarget.spriteRenderer.material;
            }

            return null;
        }

        private bool TryExtractSpriteLocalLayerPixels(Texture2D layerTexture, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (layerTexture == null || layerTexture.width <= 0 || layerTexture.height <= 0)
            {
                return false;
            }

            int targetWidth = Width;
            int targetHeight = Height;
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return false;
            }

            Color32[] layerPixels = layerTexture.GetPixels32();
            if (layerPixels == null || layerPixels.Length != layerTexture.width * layerTexture.height)
            {
                return false;
            }

            if (layerTexture.width == targetWidth && layerTexture.height == targetHeight)
            {
                pixels = ClonePixels(layerPixels);
                width = targetWidth;
                height = targetHeight;
                return true;
            }

            RectInt region;
            if (!TryResolveLayerRegion(layerTexture, targetWidth, targetHeight, out region))
            {
                return false;
            }

            Color32[] extracted = ExtractPixels(layerPixels, layerTexture.width, layerTexture.height, region);
            if (extracted == null)
            {
                return false;
            }

            if (region.width != targetWidth || region.height != targetHeight)
            {
                extracted = ResizePixelsNearest(extracted, region.width, region.height, targetWidth, targetHeight);
            }

            pixels = extracted;
            width = targetWidth;
            height = targetHeight;
            return true;
        }

        private bool TryResolveLayerRegion(Texture2D layerTexture, int targetWidth, int targetHeight, out RectInt region)
        {
            region = new RectInt();
            if (layerTexture == null || originalSprite == null || originalSprite.texture == null)
            {
                return false;
            }

            Rect spriteRect = originalSprite.rect;
            int sourceTextureWidth = Mathf.Max(1, originalSprite.texture.width);
            int sourceTextureHeight = Mathf.Max(1, originalSprite.texture.height);

            float scaleX = layerTexture.width / (float)sourceTextureWidth;
            float scaleY = layerTexture.height / (float)sourceTextureHeight;

            int x = Mathf.RoundToInt(spriteRect.x * scaleX);
            int y = Mathf.RoundToInt(spriteRect.y * scaleY);
            int width = Mathf.RoundToInt(spriteRect.width * scaleX);
            int height = Mathf.RoundToInt(spriteRect.height * scaleY);

            if (width <= 0 || height <= 0)
            {
                width = targetWidth;
                height = targetHeight;
            }

            x = Mathf.Clamp(x, 0, Mathf.Max(0, layerTexture.width - 1));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, layerTexture.height - 1));
            width = Mathf.Clamp(width, 1, layerTexture.width - x);
            height = Mathf.Clamp(height, 1, layerTexture.height - y);

            region = new RectInt(x, y, width, height);
            return region.width > 0 && region.height > 0;
        }

        private static Color32[] ExtractPixels(Color32[] source, int sourceWidth, int sourceHeight, RectInt region)
        {
            if (source == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                source.Length != sourceWidth * sourceHeight ||
                region.width <= 0 ||
                region.height <= 0)
            {
                return null;
            }

            Color32[] result = new Color32[region.width * region.height];
            for (int y = 0; y < region.height; y++)
            {
                int sourceY = Mathf.Clamp(region.y + y, 0, sourceHeight - 1);
                int sourceRow = sourceY * sourceWidth;
                int targetRow = y * region.width;
                for (int x = 0; x < region.width; x++)
                {
                    int sourceX = Mathf.Clamp(region.x + x, 0, sourceWidth - 1);
                    result[targetRow + x] = source[sourceRow + sourceX];
                }
            }

            return result;
        }

        private static Color32[] ResizePixelsNearest(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            if (source == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                targetWidth <= 0 ||
                targetHeight <= 0 ||
                source.Length != sourceWidth * sourceHeight)
            {
                return null;
            }

            Color32[] result = new Color32[targetWidth * targetHeight];
            for (int y = 0; y < targetHeight; y++)
            {
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y / (float)Mathf.Max(1, targetHeight - 1)) * Mathf.Max(0, sourceHeight - 1)), 0, sourceHeight - 1);
                int sourceRow = sourceY * sourceWidth;
                int targetRow = y * targetWidth;
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt((x / (float)Mathf.Max(1, targetWidth - 1)) * Mathf.Max(0, sourceWidth - 1)), 0, sourceWidth - 1);
                    result[targetRow + x] = source[sourceRow + sourceX];
                }
            }

            return result;
        }

        private Sprite CreateOwnedBasisSprite(int width, int height, int pivotX, int pivotY, float pixelsPerUnit, Color32[] pixels, string name)
        {
            Texture2D basisTexture = CreateTextureFromPixels(width, height, pixels);
            if (basisTexture == null)
            {
                return null;
            }

            Sprite sprite = CreateStandaloneSprite(basisTexture, pivotX, pivotY, pixelsPerUnit, name);
            if (sprite == null)
            {
                UnityEngine.Object.Destroy(basisTexture);
                return null;
            }

            ownedBasisTextures.Add(basisTexture);
            ownedBasisSprites.Add(sprite);
            return sprite;
        }

        private bool PaintCircle(PixelLayer layer, int centerX, int centerY, int brushSize, Color color, bool erase, bool opaqueOnly)
        {
            if (layer == null || layer.Pixels == null)
            {
                return false;
            }

            int radius = Mathf.Max(0, brushSize - 1);
            bool changed = false;

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x < 0 || y < 0 || x >= workingTexture.width || y >= workingTexture.height)
                    {
                        continue;
                    }

                    int dx = x - centerX;
                    int dy = y - centerY;

                    if (dx * dx + dy * dy > radius * radius)
                    {
                        continue;
                    }

                    int index = x + y * workingTexture.width;
                    if (index < 0 || index >= layer.Pixels.Length)
                    {
                        continue;
                    }

                    Color32 existing = layer.Pixels[index];
                    Color32 basePixel = GetStrokeBasePixel(x, y);
                    if (opaqueOnly && !erase && basePixel.a <= 8)
                    {
                        continue;
                    }

                    Color32 replacement = erase ? new Color32(0, 0, 0, 0) : ResolvePaintColor(basePixel, color);

                    if (ColorsEqual(existing, replacement))
                    {
                        continue;
                    }

                    layer.Pixels[index] = replacement;
                    changed = true;
                }
            }

            return changed;
        }

        private bool ApplyColliderShape(List<Vector2[]> paths)
        {
            if (workingSprite == null || physicalTarget == null)
            {
                return false;
            }

            Collider2D replacedCollider;
            PolygonCollider2D polygonCollider = GetOrCreatePolygonCollider(out replacedCollider);
            if (polygonCollider != null)
            {
                polygonCollider.pathCount = paths.Count;

                for (int i = 0; i < paths.Count; i++)
                {
                    polygonCollider.SetPath(i, paths[i]);
                }

                polygonCollider.enabled = paths.Count > 0;

                if (replacedCollider != null && replacedCollider != polygonCollider)
                {
                    replacedCollider.enabled = false;
                }

                if (Limb != null)
                {
                    Limb.Collider = polygonCollider;
                }
            }
            else
            {
                string targetName = Limb != null ? Limb.name : physicalTarget.name;
                Debug.LogWarning("[DB Singularity] No supported collider was found for sprite target " + targetName + ". Hitbox update skipped.");
                return false;
            }

            if (paths.Count > 0)
            {
                workingSprite.OverridePhysicsShape(paths);
            }

            physicalTarget.ResetColliderArray();
            physicalTarget.BakeColliderGridPoints();
            physicalTarget.RecalculateMassBasedOnSize();
            physicalTarget.RefreshOutline();
            EnsureAssigned();
            return true;
        }

        private PolygonCollider2D GetOrCreatePolygonCollider(out Collider2D replacedCollider)
        {
            replacedCollider = null;

            if (physicalTarget == null)
            {
                return null;
            }

            PolygonCollider2D polygonCollider = Limb != null ? Limb.Collider as PolygonCollider2D : null;
            if (polygonCollider != null)
            {
                return polygonCollider;
            }

            polygonCollider = physicalTarget.GetComponent<PolygonCollider2D>();
            if (polygonCollider != null)
            {
                replacedCollider = Limb != null ? Limb.Collider : null;
                return polygonCollider;
            }

            replacedCollider = Limb != null ? Limb.Collider : physicalTarget.GetComponent<Collider2D>();
            polygonCollider = physicalTarget.gameObject.AddComponent<PolygonCollider2D>();
            polygonCollider.offset = Vector2.zero;

            if (replacedCollider != null)
            {
                polygonCollider.sharedMaterial = replacedCollider.sharedMaterial;
                polygonCollider.isTrigger = replacedCollider.isTrigger;
                polygonCollider.usedByEffector = replacedCollider.usedByEffector;
                polygonCollider.density = replacedCollider.density;
            }

            return polygonCollider;
        }

        private List<Vector2[]> BuildColliderPaths()
        {
            List<Vector2[]> result = new List<Vector2[]>();

            if (workingTexture == null || workingSprite == null)
            {
                return result;
            }

            int width = workingTexture.width;
            int height = workingTexture.height;
            Color32[] pixels = workingTexture.GetPixels32();
            bool[] solid = new bool[pixels.Length];
            bool anySolid = false;

            for (int i = 0; i < pixels.Length; i++)
            {
                solid[i] = pixels[i].a > 0;
                anySolid |= solid[i];
            }

            if (!anySolid)
            {
                return result;
            }

            bool[] visited = new bool[solid.Length];
            List<int> queue = new List<int>();

            for (int index = 0; index < solid.Length; index++)
            {
                if (!solid[index] || visited[index])
                {
                    continue;
                }

                HashSet<int> component = new HashSet<int>();
                int queueIndex = 0;
                visited[index] = true;
                queue.Clear();
                queue.Add(index);

                while (queueIndex < queue.Count)
                {
                    int current = queue[queueIndex];
                    queueIndex++;
                    component.Add(current);

                    int x = current % width;
                    int y = current / width;

                    TryEnqueuePixel(solid, visited, queue, width, height, x + 1, y);
                    TryEnqueuePixel(solid, visited, queue, width, height, x - 1, y);
                    TryEnqueuePixel(solid, visited, queue, width, height, x, y + 1);
                    TryEnqueuePixel(solid, visited, queue, width, height, x, y - 1);
                }

                Vector2 colliderPivot = Limb != null
                    ? new Vector2(width * 0.5f, height * 0.5f)
                    : originalSprite.pivot;
                AddComponentRectPaths(result, component, width, height, colliderPivot, originalSprite.pixelsPerUnit);
            }

            return result;
        }

        private static void TryEnqueuePixel(bool[] solid, bool[] visited, List<int> queue, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int index = x + y * width;
            if (!solid[index] || visited[index])
            {
                return;
            }

            visited[index] = true;
            queue.Add(index);
        }

        private static void AddComponentPaths(List<Vector2[]> result, HashSet<int> component, int width, int height, Vector2 pivot, float pixelsPerUnit)
        {
            Dictionary<Vector2Int, List<Vector2Int>> edges = new Dictionary<Vector2Int, List<Vector2Int>>();

            foreach (int index in component)
            {
                int x = index % width;
                int y = index / width;

                if (!ContainsPixel(component, width, height, x, y + 1))
                {
                    AddEdge(edges, new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1));
                }

                if (!ContainsPixel(component, width, height, x + 1, y))
                {
                    AddEdge(edges, new Vector2Int(x + 1, y + 1), new Vector2Int(x + 1, y));
                }

                if (!ContainsPixel(component, width, height, x, y - 1))
                {
                    AddEdge(edges, new Vector2Int(x + 1, y), new Vector2Int(x, y));
                }

                if (!ContainsPixel(component, width, height, x - 1, y))
                {
                    AddEdge(edges, new Vector2Int(x, y), new Vector2Int(x, y + 1));
                }
            }

            while (edges.Count > 0)
            {
                Vector2Int start = default(Vector2Int);
                foreach (KeyValuePair<Vector2Int, List<Vector2Int>> entry in edges)
                {
                    start = entry.Key;
                    break;
                }

                List<Vector2Int> loop = BuildLoop(edges, start);
                if (loop == null || loop.Count < 3)
                {
                    continue;
                }

                loop = SimplifyLoop(loop);
                if (loop.Count < 3 || CalculateSignedArea(loop) >= 0f)
                {
                    continue;
                }

                Vector2[] path = new Vector2[loop.Count];
                for (int i = 0; i < loop.Count; i++)
                {
                    path[i] = new Vector2(
                        (loop[i].x - pivot.x) / pixelsPerUnit,
                        (loop[i].y - pivot.y) / pixelsPerUnit);
                }

                result.Add(path);
            }
        }

        private static void AddComponentRectPaths(List<Vector2[]> result, HashSet<int> component, int width, int height, Vector2 pivot, float pixelsPerUnit)
        {
            List<HitboxRect> rects = new List<HitboxRect>();

            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x < width)
                {
                    while (x < width && !ContainsPixel(component, width, height, x, y))
                    {
                        x++;
                    }

                    if (x >= width)
                    {
                        break;
                    }

                    int startX = x;
                    while (x < width && ContainsPixel(component, width, height, x, y))
                    {
                        x++;
                    }

                    int runWidth = x - startX;
                    bool merged = false;
                    for (int i = 0; i < rects.Count; i++)
                    {
                        HitboxRect rect = rects[i];
                        if (rect.X == startX && rect.Width == runWidth && rect.Y + rect.Height == y)
                        {
                            rect.Height++;
                            rects[i] = rect;
                            merged = true;
                            break;
                        }
                    }

                    if (!merged)
                    {
                        HitboxRect rect = new HitboxRect();
                        rect.X = startX;
                        rect.Y = y;
                        rect.Width = runWidth;
                        rect.Height = 1;
                        rects.Add(rect);
                    }
                }
            }

            for (int i = 0; i < rects.Count; i++)
            {
                HitboxRect rect = rects[i];
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    continue;
                }

                Vector2[] path = new Vector2[4];
                path[0] = ToColliderPoint(rect.X, rect.Y, pivot, pixelsPerUnit);
                path[1] = ToColliderPoint(rect.X, rect.Y + rect.Height, pivot, pixelsPerUnit);
                path[2] = ToColliderPoint(rect.X + rect.Width, rect.Y + rect.Height, pivot, pixelsPerUnit);
                path[3] = ToColliderPoint(rect.X + rect.Width, rect.Y, pivot, pixelsPerUnit);
                result.Add(path);
            }
        }

        private static Vector2 ToColliderPoint(int x, int y, Vector2 pivot, float pixelsPerUnit)
        {
            return new Vector2(
                (x - pivot.x) / pixelsPerUnit,
                (y - pivot.y) / pixelsPerUnit);
        }

        private static bool ContainsPixel(HashSet<int> component, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            return component.Contains(x + y * width);
        }

        private static void AddEdge(Dictionary<Vector2Int, List<Vector2Int>> edges, Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> next;
            if (!edges.TryGetValue(start, out next))
            {
                next = new List<Vector2Int>();
                edges[start] = next;
            }

            next.Add(end);
        }

        private static List<Vector2Int> BuildLoop(Dictionary<Vector2Int, List<Vector2Int>> edges, Vector2Int start)
        {
            List<Vector2Int> loop = new List<Vector2Int>();
            Vector2Int current = start;
            int guard = 0;

            while (guard++ < 65536)
            {
                loop.Add(current);

                List<Vector2Int> next;
                if (!edges.TryGetValue(current, out next) || next.Count == 0)
                {
                    return loop;
                }

                Vector2Int destination = next[0];
                next.RemoveAt(0);

                if (next.Count == 0)
                {
                    edges.Remove(current);
                }

                current = destination;
                if (current == start)
                {
                    break;
                }
            }

            return loop;
        }

        private static List<Vector2Int> SimplifyLoop(List<Vector2Int> loop)
        {
            if (loop == null || loop.Count < 3)
            {
                return loop;
            }

            List<Vector2Int> simplified = new List<Vector2Int>();

            for (int i = 0; i < loop.Count; i++)
            {
                Vector2Int previous = loop[(i + loop.Count - 1) % loop.Count];
                Vector2Int current = loop[i];
                Vector2Int next = loop[(i + 1) % loop.Count];
                Vector2Int incoming = current - previous;
                Vector2Int outgoing = next - current;

                if (incoming.x * outgoing.y - incoming.y * outgoing.x == 0)
                {
                    continue;
                }

                simplified.Add(current);
            }

            return simplified.Count >= 3 ? simplified : loop;
        }

        private static float CalculateSignedArea(List<Vector2Int> loop)
        {
            float area = 0f;

            for (int i = 0; i < loop.Count; i++)
            {
                Vector2Int current = loop[i];
                Vector2Int next = loop[(i + 1) % loop.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }

        private static Color32 ResolvePaintColor(Color32 existing, Color brushColor)
        {
            float sourceAlpha = Mathf.Clamp01(brushColor.a);
            if (sourceAlpha <= 0f)
            {
                return existing;
            }

            Color destination = existing;
            float destinationAlpha = destination.a;
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);

            if (outputAlpha <= 0f)
            {
                return Color.clear;
            }

            float preservedDestination = destinationAlpha * (1f - sourceAlpha);
            Color blended = new Color(
                ((brushColor.r * sourceAlpha) + (destination.r * preservedDestination)) / outputAlpha,
                ((brushColor.g * sourceAlpha) + (destination.g * preservedDestination)) / outputAlpha,
                ((brushColor.b * sourceAlpha) + (destination.b * preservedDestination)) / outputAlpha,
                outputAlpha);

            return (Color32)blended;
        }

        private Color32 GetStrokeBasePixel(int x, int y)
        {
            if (workingTexture != null && strokeBasePixels != null)
            {
                int index = x + y * workingTexture.width;
                if (index >= 0 && index < strokeBasePixels.Length)
                {
                    return strokeBasePixels[index];
                }
            }

            if (workingTexture != null)
            {
                return (Color32)workingTexture.GetPixel(x, y);
            }

            return new Color32(0, 0, 0, 0);
        }

        private void TryEnqueue(List<Vector2Int> queue, bool[,] visited, int x, int y)
        {
            if (x < 0 || y < 0 || x >= workingTexture.width || y >= workingTexture.height || visited[x, y])
            {
                return;
            }

            visited[x, y] = true;
            queue.Add(new Vector2Int(x, y));
        }

        private static Texture2D CreateEditableTexture(Sprite sprite)
        {
            Texture2D readableCopy = CreateReadableCopy(sprite.texture);
            if (readableCopy == null)
            {
                return null;
            }

            int width = Mathf.RoundToInt(sprite.rect.width);
            int height = Mathf.RoundToInt(sprite.rect.height);
            int trimmedWidth = Mathf.RoundToInt(sprite.textureRect.width);
            int trimmedHeight = Mathf.RoundToInt(sprite.textureRect.height);
            int offsetX = Mathf.RoundToInt(sprite.textureRectOffset.x);
            int offsetY = Mathf.RoundToInt(sprite.textureRectOffset.y);

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;

            Color32[] clearPixels = new Color32[width * height];
            result.SetPixels32(clearPixels);

            Color[] trimmedPixels = readableCopy.GetPixels(
                Mathf.RoundToInt(sprite.textureRect.x),
                Mathf.RoundToInt(sprite.textureRect.y),
                trimmedWidth,
                trimmedHeight);

            result.SetPixels(offsetX, offsetY, trimmedWidth, trimmedHeight, trimmedPixels);
            result.Apply();

            UnityEngine.Object.Destroy(readableCopy);
            return result;
        }

        private static Texture2D CreateTextureFromPixels(int width, int height, Color32[] pixels)
        {
            if (width <= 0 || height <= 0 || pixels == null || pixels.Length != width * height)
            {
                return null;
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;
            result.SetPixels32(ClonePixels(pixels));
            result.Apply();
            return result;
        }

        private static Sprite CreateStandaloneSprite(Texture2D texture, int pivotX, int pivotY, float pixelsPerUnit, string name)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return null;
            }

            float safePpu = Mathf.Abs(pixelsPerUnit) > 0.001f ? pixelsPerUnit : 1f;
            Vector2 normalizedPivot = new Vector2(
                Mathf.Clamp01((float)pivotX / texture.width),
                Mathf.Clamp01((float)pivotY / texture.height));

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                normalizedPivot,
                safePpu,
                0U,
                SpriteMeshType.FullRect);

            sprite.name = string.IsNullOrWhiteSpace(name) ? "LiveSpriteWorkshopBasis" : name;
            return sprite;
        }

        private static int GetSpritePixelCount(Sprite sprite)
        {
            if (sprite == null)
            {
                return 0;
            }

            return Mathf.RoundToInt(sprite.rect.width) * Mathf.RoundToInt(sprite.rect.height);
        }

        private static Color32[] CreatePixelsFromSprite(Sprite sprite, int expectedWidth, int expectedHeight)
        {
            if (sprite == null)
            {
                return null;
            }

            Texture2D texture = CreateEditableTexture(sprite);
            if (texture == null)
            {
                return null;
            }

            Color32[] pixels = null;
            if (texture.width == expectedWidth && texture.height == expectedHeight)
            {
                pixels = ClonePixels(texture.GetPixels32());
            }

            UnityEngine.Object.Destroy(texture);
            return pixels;
        }

        private static Color32[] NormalizePixels(Color32[] pixels, int expectedLength)
        {
            if (pixels == null || pixels.Length != expectedLength)
            {
                return null;
            }

            return ClonePixels(pixels);
        }

        private static Sprite GetDisplayedSprite(LimbBehaviour limb)
        {
            if (limb == null)
            {
                return null;
            }

            if (limb.SkinMaterialHandler != null && limb.SkinMaterialHandler.renderer != null && limb.SkinMaterialHandler.renderer.sprite != null)
            {
                return limb.SkinMaterialHandler.renderer.sprite;
            }

            if (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.spriteRenderer != null)
            {
                return limb.PhysicalBehaviour.spriteRenderer.sprite;
            }

            return null;
        }

        private static Sprite GetDisplayedSprite(PhysicalBehaviour physical)
        {
            if (physical == null || physical.spriteRenderer == null)
            {
                return null;
            }

            return physical.spriteRenderer.sprite;
        }

        private static Sprite GetDisplayedSprite(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            return renderer.sprite;
        }

        private bool IsUsableTarget()
        {
            return Limb != null
                ? CharacterSpriteEditorTarget.IsUsableLimb(Limb)
                : physicalTarget != null
                    ? ObjectSpriteEditorTarget.IsUsableObject(physicalTarget)
                    : ObjectSpriteEditorTarget.IsUsableRenderer(spriteRendererTarget);
        }

        private bool IsAssignedToTarget()
        {
            if (workingSprite == null)
            {
                return false;
            }

            if (spriteRendererTarget != null && spriteRendererTarget.sprite == workingSprite)
            {
                return true;
            }

            SpriteRenderer physicalRenderer = physicalTarget != null ? physicalTarget.spriteRenderer : null;
            if (physicalRenderer != null && physicalRenderer.sprite == workingSprite)
            {
                return true;
            }

            if (Limb != null && Limb.SkinMaterialHandler != null && Limb.SkinMaterialHandler.renderer != null)
            {
                return Limb.SkinMaterialHandler.renderer.sprite == workingSprite;
            }

            return false;
        }

        public static Texture2D CreateReadableTextureCopy(Texture2D source)
        {
            return CreateReadableCopy(source);
        }

        private static Texture2D CreateAtlasDisplayTexture(Sprite original, Texture2D localTexture)
        {
            if (original == null || localTexture == null)
            {
                return null;
            }

            RectInt atlasRegion = GetOriginalTextureRegion(original);
            if (atlasRegion.width <= 0 || atlasRegion.height <= 0)
            {
                return null;
            }

            if (atlasRegion.width != localTexture.width || atlasRegion.height != localTexture.height)
            {
                return null;
            }

            Texture2D atlasTexture = CreateReadableCopy(original.texture);
            if (atlasTexture == null)
            {
                return null;
            }

            if (atlasRegion.x < 0 ||
                atlasRegion.y < 0 ||
                atlasRegion.x + atlasRegion.width > atlasTexture.width ||
                atlasRegion.y + atlasRegion.height > atlasTexture.height)
            {
                UnityEngine.Object.Destroy(atlasTexture);
                return null;
            }

            atlasTexture.SetPixels(
                atlasRegion.x,
                atlasRegion.y,
                atlasRegion.width,
                atlasRegion.height,
                localTexture.GetPixels(0, 0, atlasRegion.width, atlasRegion.height));
            atlasTexture.Apply();
            return atlasTexture;
        }

        private static RectInt GetOriginalTextureRegion(Sprite original)
        {
            Rect rect = original.rect;
            return new RectInt(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height));
        }

        private static Sprite CreatePreviewSprite(Sprite original, Texture2D texture)
        {
            Vector2 pivot = new Vector2(
                original.pivot.x / original.rect.width,
                original.pivot.y / original.rect.height);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                original.pixelsPerUnit,
                0U,
                SpriteMeshType.FullRect,
                original.border);

            sprite.name = original.name + "_LiveSpriteWorkshopPreview";
            return sprite;
        }

        private static Sprite CreateAtlasDisplaySprite(Sprite original, Texture2D atlasTexture)
        {
            if (original == null || atlasTexture == null)
            {
                return null;
            }

            Rect region = original.rect;
            Vector2 pivot = new Vector2(
                original.pivot.x / region.width,
                original.pivot.y / region.height);

            Sprite sprite = Sprite.Create(
                atlasTexture,
                region,
                pivot,
                original.pixelsPerUnit,
                0U,
                SpriteMeshType.FullRect,
                original.border);

            sprite.name = original.name + "_LiveSpriteWorkshop";
            return sprite;
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.filterMode = FilterMode.Point;
            readable.wrapMode = TextureWrapMode.Clamp;
            readable.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return readable;
        }

        private static Color32[] ClonePixels(Color32[] pixels)
        {
            Color32[] clone = new Color32[pixels.Length];
            Array.Copy(pixels, clone, pixels.Length);
            return clone;
        }

        private static bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r &&
                   a.g == b.g &&
                   a.b == b.b &&
                   a.a == b.a;
        }

        private static bool ColorsClose(Color32 a, Color32 b, int tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance &&
                   Mathf.Abs(a.a - b.a) <= tolerance;
        }

        private static bool PixelsEqual(Color32[] left, Color32[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!ColorsEqual(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

// Originally uploaded by 'hypermodified'. Do not reupload without their explicit permission.
