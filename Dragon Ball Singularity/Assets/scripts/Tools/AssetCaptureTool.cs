using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    public static class AssetCaptureTool
    {
        public static bool Armed;

        public static void Arm()
        {
            Armed = true;
            AssetCaptureInput.Ensure();
            AssetCaptureBrowser.Close();
            AssetCaptureSession.Cancel();
            ModAPI.Notify("Asset Capture armed. Click a character.");
        }

        public static void Disarm()
        {
            Armed = false;
        }
    }

    public class AssetCaptureInput : MonoBehaviour
    {
        static AssetCaptureInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("DbpuAssetCaptureInput");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AssetCaptureInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (DialogBox.IsAnyDialogboxOpen) return;
                if (AssetCaptureSession.IsOpen)
                {
                    AssetCaptureSession.Cancel();
                    return;
                }
                if (AssetCaptureBrowser.IsOpen)
                {
                    AssetCaptureBrowser.Close();
                    return;
                }
                if (AssetCaptureTool.Armed)
                {
                    AssetCaptureTool.Disarm();
                    ModAPI.Notify("Asset Capture cancelled");
                }
            }

            if (!AssetCaptureTool.Armed) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;
            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            AssetCaptureTool.Disarm();
            AssetCaptureBrowser.Open(person);
        }
    }

    public class AssetCaptureTarget
    {
        public string Label;
        public string FormId;
        public string DictKey;
        public string SkinKey;
        public string DamageKey;
        public Sprite Preview;
        public string SavePath;
        public AssetCaptureKind Kind;
        public bool Missing;
    }

    public enum AssetCaptureKind
    {
        Icon,
        Illustration,
        Thumbnail,
        SkinIllustration,
        PowerIcon,
        PowerIllustration
    }

    public static class AssetCaptureBrowser
    {
        static GameObject _root;
        static PersonBehaviour _person;
        static CharacterTransformations _ct;
        static CharacterSkins _skins;
        static CharacterPowers _powers;
        static string _characterId;
        static string _displayName;
        static string _characterFolder;
        static TMP_FontAsset _font;

        public static bool IsOpen => _root != null;

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static void Open(PersonBehaviour person)
        {
            Close();
            if (person == null) return;
            _person = person;
            var root = person.transform.root.gameObject;
            _ct = root.GetComponent<CharacterTransformations>();
            _skins = root.GetComponent<CharacterSkins>();
            _powers = root.GetComponent<CharacterPowers>()
                ?? root.GetComponentInChildren<CharacterPowers>(true);

            _characterId = null;
            _displayName = root.name;
            var identity = root.GetComponent<DbpuCharacterIdentity>();
            if (identity != null)
            {
                _characterId = identity.CharacterId;
                _displayName = string.IsNullOrEmpty(identity.DisplayName) ? _characterId : identity.DisplayName;
            }
            if (string.IsNullOrEmpty(_characterId))
                _characterId = _displayName;
            _characterId = FormArtPaths.NormalizeCharacterId(_characterId);

            string spriteFolder = identity != null ? identity.SpriteFolder : null;
            _characterFolder = FormArtPaths.ResolveCharacterFolder(_characterId, _displayName, spriteFolder);
            FormArtPaths.EnsureAssetFolders(_characterFolder);
            FormArtPaths.EnsurePowerAssetFolders();
            FormArtFromDisk.ApplyToPerson(root);

            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            _root = new GameObject("DbpuAssetCaptureBrowser");
            DbSkin.Attach(_root);
            _root.transform.SetParent(canvas.transform, false);
            var rt = _root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(_root.transform, false);
            var brt = blocker.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            blocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(920f, 620f);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(0f, 48f);
            titleGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);
            var titleTextGo = new GameObject("T");
            titleTextGo.transform.SetParent(titleGo.transform, false);
            var ttrt = titleTextGo.AddComponent<RectTransform>();
            ttrt.anchorMin = Vector2.zero;
            ttrt.anchorMax = Vector2.one;
            ttrt.offsetMin = new Vector2(16f, 0f);
            ttrt.offsetMax = new Vector2(-16f, 0f);
            var title = titleTextGo.AddComponent<TextMeshProUGUI>();
            title.text = "Asset Capture  |  " + _displayName;
            title.font = Font;
            title.fontSize = 22f;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = Color.white;
            title.raycastTarget = false;

            MakeButton(panel.transform, "Close", new Vector2(390f, 286f), Close);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(panel.transform, false);
            var srt = scrollGo.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, -18f);
            srt.sizeDelta = new Vector2(880f, 520f);
            scrollGo.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 1f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vrt = viewport.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(6f, 6f);
            vrt.offsetMax = new Vector2(-6f, -6f);
            viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(860f, 100f);
            scroll.viewport = vrt;
            scroll.content = crt;

            var targets = BuildTargets();
            float y = -8f;
            for (int i = 0; i < targets.Count; i++)
                PlaceCard(content.transform, targets[i], ref y);
            crt.sizeDelta = new Vector2(860f, Mathf.Max(120f, Mathf.Abs(y) + 20f));
            ModAPI.Notify("Folders ready: " + Path.GetFileName(_characterFolder));
        }

        static List<AssetCaptureTarget> BuildTargets()
        {
            var list = new List<AssetCaptureTarget>();
            string thumbPath = FormArtPaths.ThumbnailPath(_characterId);
            Sprite thumbSpr = null;
            try
            {
                if (File.Exists(thumbPath))
                    thumbSpr = CustomTransformationCatalog.LoadSpriteFromDisk(thumbPath);
            }
            catch { }

            list.Add(new AssetCaptureTarget
            {
                Label = "Catalog Thumbnail",
                FormId = null,
                Preview = thumbSpr,
                SavePath = thumbPath,
                Kind = AssetCaptureKind.Thumbnail,
                Missing = !FormArtPaths.DiskAssetExists(thumbPath)
            });

            if (_ct?.transformations?.TransformationData != null)
            {
                foreach (var kvp in _ct.transformations.TransformationData.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var stats = kvp.Value;
                    if (stats == null) continue;
                    string formId = string.IsNullOrEmpty(stats.ID) ? kvp.Key : stats.ID;
                    string formName = string.IsNullOrEmpty(stats.Name) ? formId : stats.Name;

                    string iconPath = FormArtPaths.ResolveFormAssetPath(
                        _characterFolder, "Icons", formId, stats.Icon, formName);
                    string illPath = FormArtPaths.ResolveFormAssetPath(
                        _characterFolder, "Illustrations", formId, stats.Illustration, formName);

                    Sprite iconDisk = FormArtPaths.DiskAssetExists(iconPath)
                        ? CustomTransformationCatalog.LoadSpriteFromDisk(iconPath)
                        : null;
                    Sprite illDisk = FormArtPaths.DiskAssetExists(illPath)
                        ? CustomTransformationCatalog.LoadSpriteFromDisk(illPath)
                        : null;

                    list.Add(new AssetCaptureTarget
                    {
                        Label = formName + "  Icon",
                        FormId = formId,
                        DictKey = kvp.Key,
                        Preview = iconDisk ?? stats.Icon,
                        SavePath = iconPath,
                        Kind = AssetCaptureKind.Icon,
                        Missing = iconDisk == null
                    });

                    list.Add(new AssetCaptureTarget
                    {
                        Label = formName + "  Illustration",
                        FormId = formId,
                        DictKey = kvp.Key,
                        Preview = illDisk ?? stats.Illustration ?? iconDisk ?? stats.Icon,
                        SavePath = illPath,
                        Kind = AssetCaptureKind.Illustration,
                        Missing = illDisk == null
                    });
                }
            }
            else
            {
                string iconPath = FormArtPaths.ResolveFormAssetPath(_characterFolder, "Icons", "Base");
                string illPath = FormArtPaths.ResolveFormAssetPath(_characterFolder, "Illustrations", "Base");
                Sprite iconDisk = FormArtPaths.DiskAssetExists(iconPath)
                    ? CustomTransformationCatalog.LoadSpriteFromDisk(iconPath)
                    : null;
                Sprite illDisk = FormArtPaths.DiskAssetExists(illPath)
                    ? CustomTransformationCatalog.LoadSpriteFromDisk(illPath)
                    : null;
                list.Add(new AssetCaptureTarget
                {
                    Label = "Base  Icon",
                    FormId = "Base",
                    Preview = iconDisk,
                    SavePath = iconPath,
                    Kind = AssetCaptureKind.Icon,
                    Missing = iconDisk == null
                });
                list.Add(new AssetCaptureTarget
                {
                    Label = "Base  Illustration",
                    FormId = "Base",
                    Preview = illDisk ?? iconDisk,
                    SavePath = illPath,
                    Kind = AssetCaptureKind.Illustration,
                    Missing = illDisk == null
                });
            }

            if (_skins?.skins?.SkinData != null)
            {
                foreach (var kvp in _skins.skins.SkinData.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var info = kvp.Value;
                    if (info == null) continue;
                    string skinKey = string.IsNullOrEmpty(info.Name) ? kvp.Key : info.Name;
                    string display = string.IsNullOrEmpty(info.DisplayName) ? skinKey : info.DisplayName;

                    var damageKeys = new List<string>();
                    if (info.Illustrations != null && info.Illustrations.Count > 0)
                    {
                        foreach (var dk in info.Illustrations.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            damageKeys.Add(dk);
                    }
                    if (damageKeys.Count == 0) damageKeys.Add("Damage0");

                    for (int i = 0; i < damageKeys.Count; i++)
                    {
                        string damageKey = damageKeys[i];
                        string savePath = FormArtPaths.ResolveSkinIllustrationPath(_characterFolder, skinKey, damageKey);
                        Sprite disk = FormArtPaths.DiskAssetExists(savePath)
                            ? CustomTransformationCatalog.LoadSpriteFromDisk(savePath)
                            : null;
                        Sprite preview = disk;
                        if (preview == null && info.Illustrations != null)
                            info.Illustrations.TryGetValue(damageKey, out preview);

                        string label = display + "  Skin Illust";
                        if (!string.Equals(damageKey, "Damage0", StringComparison.OrdinalIgnoreCase))
                            label += " (" + damageKey + ")";

                        list.Add(new AssetCaptureTarget
                        {
                            Label = label,
                            SkinKey = kvp.Key,
                            DamageKey = damageKey,
                            Preview = preview,
                            SavePath = savePath,
                            Kind = AssetCaptureKind.SkinIllustration,
                            Missing = disk == null
                        });
                    }
                }
            }

            AddPowerTargets(list);
            return list;
        }

        static void AddPowerTargets(List<AssetCaptureTarget> list)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_powers?.PowerData != null)
            {
                foreach (var id in _powers.PowerOrder)
                {
                    if (!_powers.PowerData.TryGetValue(id, out var stats) || stats == null) continue;
                    string powerId = string.IsNullOrEmpty(stats.ID) ? id : stats.ID;
                    if (!seen.Add(powerId)) continue;
                    AddPowerIconAndIllustration(list, powerId, stats.Name, stats.Icon, stats.Illustration, true);
                }
            }

            foreach (var def in PowerCatalog.All.OrderBy(d => d?.Name ?? d?.Id ?? "", StringComparer.OrdinalIgnoreCase))
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (!seen.Add(def.Id)) continue;
                AddPowerIconAndIllustration(list, def.Id, def.Name, def.Icon, null, false);
            }
        }

        static void AddPowerIconAndIllustration(
            List<AssetCaptureTarget> list,
            string powerId,
            string powerName,
            Sprite icon,
            Sprite illustration,
            bool onCharacter)
        {
            string display = string.IsNullOrEmpty(powerName) ? powerId : powerName;
            string prefix = onCharacter ? "Power  " : "Catalog Power  ";

            string iconPath = FormArtPaths.ResolvePowerIconPath(powerId, icon, powerName);
            Sprite iconDisk = FormArtPaths.DiskAssetExists(iconPath)
                ? CustomTransformationCatalog.LoadSpriteFromDisk(iconPath)
                : null;
            list.Add(new AssetCaptureTarget
            {
                Label = prefix + display + "  Icon",
                FormId = powerId,
                Preview = iconDisk ?? icon,
                SavePath = iconPath,
                Kind = AssetCaptureKind.PowerIcon,
                Missing = iconDisk == null
            });

            string illPath = FormArtPaths.ResolvePowerIllustrationPath(powerId, illustration, powerName);
            Sprite illDisk = FormArtPaths.DiskAssetExists(illPath)
                ? CustomTransformationCatalog.LoadSpriteFromDisk(illPath)
                : null;
            list.Add(new AssetCaptureTarget
            {
                Label = prefix + display + "  Illustration",
                FormId = powerId,
                Preview = illDisk ?? illustration ?? iconDisk ?? icon,
                SavePath = illPath,
                Kind = AssetCaptureKind.PowerIllustration,
                Missing = illDisk == null
            });
        }

        static void PlaceCard(Transform parent, AssetCaptureTarget target, ref float y)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(840f, 92f);
            go.AddComponent<Image>().color = target.Missing
                ? new Color(0.22f, 0.12f, 0.12f, 1f)
                : new Color(0.13f, 0.13f, 0.15f, 1f);

            if (target.Preview != null)
            {
                var icon = new GameObject("Preview");
                icon.transform.SetParent(go.transform, false);
                var irt = icon.AddComponent<RectTransform>();
                irt.anchoredPosition = new Vector2(-360f, 0f);
                irt.sizeDelta = new Vector2(72f, 72f);
                var img = icon.AddComponent<Image>();
                img.sprite = target.Preview;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(100f, 8f);
            lrt.offsetMax = new Vector2(-150f, -8f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = target.Label + (target.Missing ? "  (missing)" : "") + "\n" + target.SavePath;
            tmp.font = Font;
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            var capture = target;
            MakeButton(go.transform, "Capture", new Vector2(340f, 0f), () => BeginTargetCapture(capture), 120f, 40f);
            y -= 100f;
        }

        static void BeginTargetCapture(AssetCaptureTarget target)
        {
            if (target == null || _person == null) return;

            if (_ct != null
                && !string.IsNullOrEmpty(target.FormId)
                && target.Kind != AssetCaptureKind.Thumbnail
                && target.Kind != AssetCaptureKind.SkinIllustration
                && target.Kind != AssetCaptureKind.PowerIcon
                && target.Kind != AssetCaptureKind.PowerIllustration)
            {
                string key = !string.IsNullOrEmpty(target.DictKey) ? target.DictKey : target.FormId;
                try { _ct.ApplyTransformation(_person, key); }
                catch (Exception ex) { ModAPI.Notify("Could not apply form: " + ex.Message); }
            }

            if (_root != null) _root.SetActive(false);
            AssetCaptureSession.Start(target, () =>
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                }
                Open(_person);
            }, () =>
            {
                if (_root != null) _root.SetActive(true);
            });
        }

        static void MakeButton(Transform parent, string label, Vector2 pos, Action onClick, float w = 100f, float h = 36f)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);
            go.AddComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 1f);
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var tGo = new GameObject("T");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = tGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.font = Font;
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        public static void Close()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _person = null;
            _ct = null;
            _skins = null;
            _powers = null;
        }

        public static void NotifySaved(AssetCaptureTarget target, Sprite sprite)
        {
            if (target == null) return;

            Sprite diskSprite = null;
            try
            {
                if (!string.IsNullOrEmpty(target.SavePath) && File.Exists(target.SavePath))
                    diskSprite = CustomTransformationCatalog.LoadSpriteFromDisk(target.SavePath);
            }
            catch { }
            Sprite applied = diskSprite != null ? diskSprite : sprite;

            if (target.Kind == AssetCaptureKind.SkinIllustration)
            {
                if (_skins?.skins?.SkinData == null || string.IsNullOrEmpty(target.SkinKey)) return;
                if (!_skins.skins.SkinData.TryGetValue(target.SkinKey, out var info) || info == null) return;
                if (info.Illustrations == null)
                    info.Illustrations = new Dictionary<string, Sprite>();
                string damageKey = string.IsNullOrEmpty(target.DamageKey) ? "Damage0" : target.DamageKey;
                info.Illustrations[damageKey] = applied;
                return;
            }

            if (target.Kind == AssetCaptureKind.PowerIcon || target.Kind == AssetCaptureKind.PowerIllustration)
            {
                string powerId = target.FormId;
                if (_powers?.PowerData != null && !string.IsNullOrEmpty(powerId))
                {
                    if (_powers.PowerData.TryGetValue(powerId, out var powerStats) && powerStats != null)
                    {
                        if (target.Kind == AssetCaptureKind.PowerIcon) powerStats.Icon = applied;
                        else powerStats.Illustration = applied;
                    }
                }

                var def = PowerCatalog.Get(powerId);
                if (def != null && target.Kind == AssetCaptureKind.PowerIcon)
                    def.Icon = applied;
                return;
            }

            if (target.Kind == AssetCaptureKind.Thumbnail) return;

            if (_ct?.transformations?.TransformationData == null) return;
            if (string.IsNullOrEmpty(target.FormId) && string.IsNullOrEmpty(target.DictKey)) return;

            CharacterTransformations.Transformations.TransformationStats stats = null;
            if (!string.IsNullOrEmpty(target.DictKey))
                _ct.transformations.TransformationData.TryGetValue(target.DictKey, out stats);
            if (stats == null && !string.IsNullOrEmpty(target.FormId))
                _ct.transformations.TransformationData.TryGetValue(target.FormId, out stats);
            if (stats == null)
            {
                foreach (var kvp in _ct.transformations.TransformationData)
                {
                    if (kvp.Value != null && string.Equals(kvp.Value.ID, target.FormId, StringComparison.OrdinalIgnoreCase))
                    {
                        stats = kvp.Value;
                        break;
                    }
                }
            }
            if (stats == null) return;

            if (target.Kind == AssetCaptureKind.Icon) stats.Icon = applied;
            else if (target.Kind == AssetCaptureKind.Illustration) stats.Illustration = applied;
        }
    }

    public static class AssetCaptureSession
    {
        const string EmptyIconRel = "Assets/Icons/Empty icon.png";
        const string EmptyIconRelAlt = "Assets/Icons/EmptyIcon.png";
        const string EmptyIllustrationRel = "Assets/Icons/Empty illustration.png";
        const string EmptyIllustrationRelAlt = "Assets/Icons/EmptyIllustration.png";
        const int IconFrameW = 400;
        const int IconFrameH = 400;
        const int IconContentX = 30;
        const int IconContentY = 30;
        const int IconContentW = 340;
        const int IconContentH = 340;
        const int IllFrameW = 1008;
        const int IllFrameH = 1472;
        const int IllContentX = 48;
        const int IllContentY = 48;
        const int IllContentW = 912;
        const int IllContentH = 1376;

        static GameObject _root;
        static RectTransform _frameRt;
        static AssetCaptureTarget _target;
        static Action _onDone;
        static Action _onCancel;
        static bool _busy;
        static AssetCaptureDriver _driver;
        static int _sx, _sy, _sw, _sh;
        static TMP_FontAsset _font;

        public static bool IsOpen => _root != null;

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static void Start(AssetCaptureTarget target, Action onDone, Action onCancel)
        {
            Cancel();
            _target = target;
            _onDone = onDone;
            _onCancel = onCancel;
            _busy = false;

            bool iconLike = target.Kind != AssetCaptureKind.Illustration
                && target.Kind != AssetCaptureKind.SkinIllustration
                && target.Kind != AssetCaptureKind.PowerIllustration;
            int frameW = iconLike ? IconFrameW : IllFrameW;
            int frameH = iconLike ? IconFrameH : IllFrameH;
            string rel = iconLike ? EmptyIconRel : EmptyIllustrationRel;
            string relAlt = iconLike ? EmptyIconRelAlt : EmptyIllustrationRelAlt;
            Sprite frameSpr = LoadEmptyFrameSprite(rel, relAlt, frameW, frameH);
            if (frameSpr == null)
            {
                DialogBoxManager.Notification("Could not load empty frame.");
                onCancel?.Invoke();
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                onCancel?.Invoke();
                return;
            }

            _root = new GameObject("DbpuAssetCaptureSession");
            DbSkin.Attach(_root);
            _root.transform.SetParent(canvas.transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var canvasRt = canvas.GetComponent<RectTransform>();
            float canvasW = Mathf.Max(100f, canvasRt.rect.width);
            float canvasH = Mathf.Max(100f, canvasRt.rect.height);
            float maxW = canvasW * 0.48f;
            float maxH = canvasH * 0.72f;
            float aspect = frameW / (float)frameH;
            float dispH = maxH;
            float dispW = dispH * aspect;
            if (dispW > maxW)
            {
                dispW = maxW;
                dispH = dispW / aspect;
            }

            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(_root.transform, false);
            _frameRt = frameGo.AddComponent<RectTransform>();
            _frameRt.anchorMin = _frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            _frameRt.pivot = new Vector2(0.5f, 0.5f);
            _frameRt.anchoredPosition = new Vector2(0f, canvasH * 0.04f);
            _frameRt.sizeDelta = new Vector2(dispW, dispH);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = frameSpr;
            frameImg.preserveAspect = true;
            frameImg.raycastTarget = false;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_root.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(640f, 40f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Capture: " + target.Label;
            title.font = Font;
            title.fontSize = 24f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.55f, 0.75f, 1f, 1f);
            title.raycastTarget = false;

            MakeBtn("Take Photo", new Vector2(-95f, 28f), TakeShot);
            MakeBtn("Cancel", new Vector2(95f, 28f), () =>
            {
                var cancel = _onCancel;
                Cancel();
                cancel?.Invoke();
            });

            ModAPI.Notify("Line up the shot, then Take Photo");
        }

        static void MakeBtn(string label, Vector2 pos, Action act)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(_root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160f, 40f);
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.95f);
            go.AddComponent<Button>().onClick.AddListener(() => act?.Invoke());
            var tGo = new GameObject("T");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = tGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.font = Font;
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        static void TakeShot()
        {
            if (_busy || _frameRt == null) return;
            if (!TryGetScreenRect(out _sx, out _sy, out _sw, out _sh))
            {
                DialogBoxManager.Notification("Could not read capture frame.");
                return;
            }
            _busy = true;
            for (int i = 0; i < _root.transform.childCount; i++)
                _root.transform.GetChild(i).gameObject.SetActive(false);

            if (_driver == null)
            {
                var go = new GameObject("DbpuAssetCaptureDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _driver = go.AddComponent<AssetCaptureDriver>();
            }
            _driver.OnShot = FinishShot;
            _driver.BeginShot();
        }

        static void FinishShot()
        {
            string savedPath = null;
            try
            {
                if (_target == null || string.IsNullOrEmpty(_target.SavePath))
                    throw new Exception("Missing save target.");

                bool iconLike = _target.Kind != AssetCaptureKind.Illustration
                    && _target.Kind != AssetCaptureKind.SkinIllustration
                    && _target.Kind != AssetCaptureKind.PowerIllustration;
                int frameW = iconLike ? IconFrameW : IllFrameW;
                int frameH = iconLike ? IconFrameH : IllFrameH;
                int cx = iconLike ? IconContentX : IllContentX;
                int cyFromTop = iconLike ? IconContentY : IllContentY;
                int cw = iconLike ? IconContentW : IllContentW;
                int ch = iconLike ? IconContentH : IllContentH;
                int cy = frameH - cyFromTop - ch;

                int sx = Mathf.Clamp(_sx, 0, Screen.width - 8);
                int sy = Mathf.Clamp(_sy, 0, Screen.height - 8);
                int sw = Mathf.Clamp(_sw, 8, Screen.width - sx);
                int sh = Mathf.Clamp(_sh, 8, Screen.height - sy);

                var shot = new Texture2D(sw, sh, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(sx, sy, sw, sh), 0, 0);
                shot.Apply(false, false);

                string rel = iconLike ? EmptyIconRel : EmptyIllustrationRel;
                string relAlt = iconLike ? EmptyIconRelAlt : EmptyIllustrationRelAlt;
                var template = LoadTemplateTexture(rel) ?? LoadTemplateTexture(relAlt);
                if (template == null || template.width != frameW || template.height != frameH)
                {
                    if (template != null) UnityEngine.Object.Destroy(template);
                    template = CreateEmptyFrameTexture(frameW, frameH, cx, cyFromTop, cw, ch);
                }

                var scaled = ScaleTexture(shot, cw, ch);
                UnityEngine.Object.Destroy(shot);
                var pixels = scaled.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i].a = 1f;
                template.SetPixels(cx, cy, cw, ch, pixels);
                template.Apply(false, false);
                UnityEngine.Object.Destroy(scaled);

                savedPath = _target.SavePath;
                string dir = Path.GetDirectoryName(savedPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                byte[] png = template.EncodeToPNG();
                if (png == null || png.Length == 0)
                    throw new Exception("EncodeToPNG failed.");
                File.WriteAllBytes(savedPath, png);
                if (!File.Exists(savedPath))
                    throw new Exception("File was not written: " + savedPath);

                var sprite = Sprite.Create(template, new Rect(0, 0, template.width, template.height), new Vector2(0.5f, 0.5f), 35f);
                AssetCaptureBrowser.NotifySaved(_target, sprite);
                ModAPI.Notify("Saved " + savedPath);
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Capture failed: " + ex.Message + (savedPath != null ? ("\n" + savedPath) : ""));
            }

            var done = _onDone;
            Cancel();
            done?.Invoke();
        }

        public static void Cancel()
        {
            _busy = false;
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
                _driver = null;
            }
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            _frameRt = null;
            _target = null;
            _onDone = null;
            _onCancel = null;
        }

        static bool TryGetScreenRect(out int sx, out int sy, out int sw, out int sh)
        {
            sx = sy = sw = sh = 0;
            if (_frameRt == null || _target == null) return false;

            bool iconLike = _target.Kind != AssetCaptureKind.Illustration
                && _target.Kind != AssetCaptureKind.SkinIllustration
                && _target.Kind != AssetCaptureKind.PowerIllustration;
            int frameW = iconLike ? IconFrameW : IllFrameW;
            int frameH = iconLike ? IconFrameH : IllFrameH;
            int cx = iconLike ? IconContentX : IllContentX;
            int cyFromTop = iconLike ? IconContentY : IllContentY;
            int cw = iconLike ? IconContentW : IllContentW;
            int ch = iconLike ? IconContentH : IllContentH;
            float u0 = cx / (float)frameW;
            float u1 = (cx + cw) / (float)frameW;
            float v0 = (frameH - cyFromTop - ch) / (float)frameH;
            float v1 = (frameH - cyFromTop) / (float)frameH;

            var corners = new Vector3[4];
            _frameRt.GetWorldCorners(corners);
            Vector3 bl = FrameLerp(corners, u0, v0);
            Vector3 tr = FrameLerp(corners, u1, v1);
            var cam = GameObject.Find("Canvas")?.GetComponent<Canvas>()?.worldCamera;
            Vector2 sbl = RectTransformUtility.WorldToScreenPoint(cam, bl);
            Vector2 str = RectTransformUtility.WorldToScreenPoint(cam, tr);
            sx = Mathf.RoundToInt(Mathf.Min(sbl.x, str.x));
            sy = Mathf.RoundToInt(Mathf.Min(sbl.y, str.y));
            sw = Mathf.RoundToInt(Mathf.Abs(str.x - sbl.x));
            sh = Mathf.RoundToInt(Mathf.Abs(str.y - sbl.y));
            return sw > 4 && sh > 4;
        }

        static Vector3 FrameLerp(Vector3[] c, float u, float vFromBottom)
        {
            Vector3 bottom = Vector3.Lerp(c[0], c[3], u);
            Vector3 top = Vector3.Lerp(c[1], c[2], u);
            return Vector3.Lerp(bottom, top, vFromBottom);
        }

        static Texture2D LoadTemplateTexture(string relPath)
        {
            try
            {
                string abs = Path.Combine(FormArtPaths.ModRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(abs)) return null;
                byte[] bytes = File.ReadAllBytes(abs);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                return tex;
            }
            catch { return null; }
        }

        static Sprite LoadEmptyFrameSprite(string rel, string relAlt, int w, int h)
        {
            try
            {
                Sprite s = ModAPI.LoadSprite(rel);
                if (s != null) return s;
            }
            catch { }
            try
            {
                Sprite s = ModAPI.LoadSprite(relAlt);
                if (s != null) return s;
            }
            catch { }
            var tex = CreateEmptyFrameTexture(w, h,
                w == IconFrameW ? IconContentX : IllContentX,
                w == IconFrameW ? IconContentY : IllContentY,
                w == IconFrameW ? IconContentW : IllContentW,
                w == IconFrameW ? IconContentH : IllContentH);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D CreateEmptyFrameTexture(int w, int h, int contentX, int contentYFromTop, int contentW, int contentH)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var border = new Color(1f, 1f, 1f, 0.95f);
            var fill = new Color(0f, 0f, 0f, 0.15f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            int cy = h - contentYFromTop - contentH;
            for (int y = cy; y < cy + contentH; y++)
                for (int x = contentX; x < contentX + contentW; x++)
                    pixels[y * w + x] = fill;
            int t = 3;
            for (int i = 0; i < t; i++)
            {
                for (int x = contentX; x < contentX + contentW; x++)
                {
                    pixels[(cy + i) * w + x] = border;
                    pixels[(cy + contentH - 1 - i) * w + x] = border;
                }
                for (int y = cy; y < cy + contentH; y++)
                {
                    pixels[y * w + contentX + i] = border;
                    pixels[y * w + contentX + contentW - 1 - i] = border;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D ScaleTexture(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }
    }

    public class AssetCaptureDriver : MonoBehaviour
    {
        public Action OnShot;

        public void BeginShot()
        {
            StartCoroutine(ShotCo());
        }

        IEnumerator ShotCo()
        {
            yield return new WaitForEndOfFrame();
            OnShot?.Invoke();
        }
    }
}
