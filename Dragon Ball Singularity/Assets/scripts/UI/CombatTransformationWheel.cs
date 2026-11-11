using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Stats = DBPU.CharacterTransformations.Transformations.TransformationStats;

namespace DBPU
{
    [DefaultExecutionOrder(-200)]
    public sealed class CombatTransformationWheel : MonoBehaviour
    {
        public const string ActionId = "dbpu.combattransform";
        public const KeyCode DefaultKey = KeyCode.J;
        static CombatTransformationWheel instance;
        public static bool InputCaptured { get { return instance && (instance.IsOpen || Time.frameCount <= instance.releaseFrame); } }
        public static CombatTransformationWheel Instance { get { Ensure(); return instance; } }
        public bool IsOpen { get { return root != null; } }
        public TransformationContextMenu Target { get { return target; } }
        public CombatWheelSelection Selection { get { return selection; } }

        readonly CombatWheelSelection selection = new CombatWheelSelection();
        readonly List<KeyValuePair<string, Stats>> forms = new List<KeyValuePair<string, Stats>>();
        TransformationContextMenu target;
        PersonBehaviour person;
        Global blockerOwner, deferredBlocker;
        GameObject root, toast;
        CombatWheelView view;
        KeyCode heldKey, secondaryKey;
        Vector2 pointerAnchor, center;
        float uiScale = 1, toastUntil, refreshAt;
        int renderedPage = -1, renderedSelection = -2, releaseFrame = -1, screenWidth, screenHeight;
        bool renderedArmed, ownsBlocker;
        TransformationContextMenu toastTarget;
        RectTransform toastRect;

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Combat Transformation Wheel");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatTransformationWheel>();
        }

        void Awake() { SceneManager.activeSceneChanged += SceneChanged; }
        void SceneChanged(Scene oldScene, Scene newScene) { Close(false); ClearToast(); }
        void OnApplicationFocus(bool focused) { if (!focused) Close(false); }
        void OnDisable() { Close(false); ReleaseDeferredBlocker(); ClearToast(); }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            Close(false); ReleaseDeferredBlocker(); ClearToast();
            if (instance == this) instance = null;
        }

        static TransformationContextMenu MenuFor(PhysicalBehaviour physical)
        {
            if (!physical) return null;
            var limb = physical.GetComponent<LimbBehaviour>();
            var p = limb && limb.Person ? limb.Person : physical.GetComponentInParent<PersonBehaviour>();
            if (!p || !p.IsAlive()) return null;
            var head = p.transform.Find("Head");
            var menu = head ? head.GetComponent<TransformationContextMenu>() : null;
            return menu ? menu : p.GetComponentInChildren<TransformationContextMenu>();
        }

        static TransformationContextMenu ResolveTarget()
        {
            var s = SelectionController.Main;
            var hovered = s != null ? MenuFor(s.CurrentlyUnderMouse) : null;
            if (hovered) return hovered;
            var tool = Global.main && Global.main.ToolControllerBehaviour ? Global.main.ToolControllerBehaviour.CurrentTool : null;
            var active = tool != null ? MenuFor(tool.ActiveSingleSelected) : null;
            if (active) return active;
            TransformationContextMenu result = null;
            if (s != null && s.SelectedObjects != null)
                foreach (var pb in s.SelectedObjects)
                {
                    var candidate = MenuFor(pb);
                    if (!candidate) continue;
                    if (result && result != candidate) return null;
                    result = candidate;
                }
            return result;
        }

        static bool CanOpen()
        {
            if (!Global.main || Global.main.GetPausedMenu() || Global.ActiveUiBlock || DialogBox.IsAnyDialogboxOpen) return false;
            if (LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput) return false;
            var focused = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (focused && (focused.GetComponent<TMP_InputField>() || focused.GetComponent<InputField>())) return false;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return false;
            return true;
        }

        void Update()
        {
            if (!IsOpen)
            {
                if (DbpuInputUtil.Down(ActionId, DefaultKey) && CanOpen())
                {
                    var menu = ResolveTarget();
                    if (menu) OpenFor(menu, Input.mousePosition, DbpuInputRegistration.GetActionKey(ActionId, DefaultKey));
                    else ModAPI.Notify("Hover over or select one DBPU fighter, then hold " + DbpuInputRegistration.GetActionKeyLabel(ActionId, DefaultKey));
                }
                return;
            }

            // The key is captured before taking the UI blocker; InputSystem may filter held actions in a UI.
            if (!ValidTarget() || !blockerOwner || blockerOwner != Global.main || blockerOwner.GetPausedMenu()
                || blockerOwner.ActiveUiBlockers > 1 || DialogBox.IsAnyDialogboxOpen)
            { Close(false); return; }
            if ((heldKey != KeyCode.Escape && Input.GetKeyDown(KeyCode.Escape)) || (heldKey != KeyCode.Mouse1 && Input.GetMouseButtonDown(1))) { Close(false); return; }
            if (screenWidth != Screen.width || screenHeight != Screen.height) { Close(false); return; }

            Vector2 mouse = Input.mousePosition;
            if ((mouse - pointerAnchor).sqrMagnitude > 9f)
            {
                pointerAnchor = mouse;
                Vector2 local = (mouse - center) / uiScale;
                selection.Select(selection.Hit(local.x, local.y, 84, 312));
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f) selection.Step(scroll > 0 ? 1 : -1);
            if ((heldKey != KeyCode.RightArrow && Input.GetKeyDown(KeyCode.RightArrow)) || (heldKey != KeyCode.DownArrow && Input.GetKeyDown(KeyCode.DownArrow))) selection.Step(1);
            if ((heldKey != KeyCode.LeftArrow && Input.GetKeyDown(KeyCode.LeftArrow)) || (heldKey != KeyCode.UpArrow && Input.GetKeyDown(KeyCode.UpArrow))) selection.Step(-1);
            if (heldKey != KeyCode.E && Input.GetKeyDown(KeyCode.E)) selection.ChangePage(1);
            if (heldKey != KeyCode.Q && Input.GetKeyDown(KeyCode.Q)) selection.ChangePage(-1);
            for (int i = 0; i < selection.VisibleCount; i++)
                if (heldKey != (KeyCode)((int)KeyCode.Alpha1 + i) && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) selection.SelectSlot(i);

            // Releasing in the same frame as the last movement still commits that movement.
            if (heldKey != KeyCode.None && (!Input.GetKey(heldKey) || (secondaryKey != KeyCode.None && !Input.GetKey(secondaryKey)))) { Close(true); return; }
            if (heldKey == KeyCode.None && Input.GetMouseButtonDown(0)) { Close(selection.Armed); return; }
            Refresh();
        }

        bool ValidTarget()
        {
            return target && person && person.gameObject.activeInHierarchy && person.IsAlive()
                && target.TransformationsData && target.TransformationsData.transformations != null;
        }

        public bool OpenFor(TransformationContextMenu menu, Vector2 screenPosition, KeyCode key)
        {
            if (!menu || !Global.main || Global.main.GetPausedMenu() || Global.ActiveUiBlock || DialogBox.IsAnyDialogboxOpen) return false;
            if (IsOpen) Close(false);
            person = menu.GetComponentInParent<PersonBehaviour>();
            if (!person || !person.IsAlive()) return false;
            var data = person.GetComponent<CharacterTransformations>();
            if (!data || data.transformations == null) return false;
            target = menu; target.person = person; target.TransformationsData = data;
            forms.Clear();
            foreach (var entry in data.transformations.GetOrderedEntries())
                if (entry.Value != null && !string.IsNullOrEmpty(entry.Key)) forms.Add(entry);
            if (forms.Count == 0) { target = null; person = null; return false; }
            int current = forms.FindIndex(x => x.Key == menu.Form || x.Value.ID == menu.Form);
            selection.Reset(forms.Count, Math.Max(0, current));
            heldKey = key; secondaryKey = KeyCode.None;
            InputAction action;
            if (key != KeyCode.None && InputSystem.Actions.TryGetValue(ActionId,out action)) secondaryKey = action.SecondaryKey;
            pointerAnchor = Input.mousePosition;
            screenWidth = Screen.width; screenHeight = Screen.height;
            uiScale = Mathf.Min(1.12f, Mathf.Min(Screen.width / 900f, Screen.height / 700f));
            float halfW = 300 * uiScale, halfH = 326 * uiScale;
            center = new Vector2(Mathf.Clamp(screenPosition.x, halfW, Screen.width - halfW), Mathf.Clamp(screenPosition.y, halfH, Screen.height - halfH));
            renderedPage = -1; renderedSelection = -2; refreshAt = 0;
            ClearToast();
            try
            {
                Build();
                blockerOwner = Global.main; blockerOwner.AddUiBlocker(); ownsBlocker = true;
                Refresh();
                return true;
            }
            catch { Close(false); throw; }
        }

        public void Navigate(int direction) { selection.Step(direction); Refresh(); }
        public void ChangePage(int direction) { selection.ChangePage(direction); Refresh(); }
        public void ChooseSlot(int slot) { selection.SelectSlot(slot); Refresh(); }
        public void PointAt(Vector2 local) { selection.Select(selection.Hit(local.x, local.y, 84, 312)); Refresh(); }

        public void Close(bool commit)
        {
            var oldTarget = target;
            string key = null;
            bool apply = commit && ValidTarget() && selection.Armed && selection.Selected >= 0 && selection.Selected < forms.Count;
            if (apply)
            {
                key = forms[selection.Selected].Key;
                var entry = forms[selection.Selected].Value;
                var pending = person.GetComponent<KiFormTransition>();
                apply = target.TransformationsData.transformations.TransformationData.ContainsKey(key)
                    && ((target.Form != key && target.Form != entry.ID) || (pending && pending.Busy));
            }
            if (root) { root.SetActive(false); Destroy(root); root = null; }
            if (ownsBlocker)
            {
                if (blockerOwner)
                {
                    if (isActiveAndEnabled)
                    {
                        deferredBlocker = blockerOwner;
                        StartCoroutine(ReleaseAfterFrame());
                    }
                    else blockerOwner.RemoveUiBlocker();
                }
                ownsBlocker = false;
            }
            blockerOwner = null;
            if (target) releaseFrame = Time.frameCount;
            target = null; person = null; forms.Clear(); view = null;
            if (apply && oldTarget)
            {
                oldTarget.ApplyFormWithTitle(key);
            }
        }

        IEnumerator ReleaseAfterFrame()
        {
            yield return new WaitForEndOfFrame();
            ReleaseDeferredBlocker();
        }
        void ReleaseDeferredBlocker()
        {
            if (deferredBlocker) deferredBlocker.RemoveUiBlocker();
            deferredBlocker = null;
        }
        string KeyLabel()
        {
            return heldKey + (secondaryKey == KeyCode.None ? "" : "+" + secondaryKey);
        }

        void Build()
        {
            var identity = person.GetComponent<DbpuCharacterIdentity>();
            string name = identity != null && !string.IsNullOrEmpty(identity.DisplayName) ? identity.DisplayName : person.name.Replace("(Clone)", "");
            string hold = heldKey == KeyCode.None ? "CLICK TO APPLY" : "RELEASE " + KeyLabel().ToUpperInvariant() + " TO APPLY";
            view = new CombatWheelView("DBPU Combat Wheel Canvas", "Wheel", "Transform  /  " + name, hold + "   /   WHEEL or ARROWS   /   ESC or RMB: CANCEL", center, uiScale);
            root = view.Root;
        }

        void RebuildSlots()
        {
            view.BeginSlots(selection.VisibleCount);
            for (int slot = 0; slot < selection.VisibleCount; slot++)
            {
                var entry = forms[selection.Page * CombatWheelSelection.PageSize + slot];
                Sprite icon = entry.Value.Icon ? DbArt.Icon(entry.Value.Icon) : DbArt.Illustration(entry.Value.Illustration);
                bool current = entry.Key == target.Form || entry.Value.ID == target.Form;
                view.SetSlot(slot, icon, CleanName(entry.Value, entry.Key), DbTheme.FormAccent(entry.Value), current);
            }
            renderedPage = selection.Page; renderedSelection = -2;
        }

        void Refresh()
        {
            if (!IsOpen || !ValidTarget()) return;
            if (renderedPage != selection.Page) RebuildSlots();
            if (renderedSelection != selection.Selected || renderedArmed != selection.Armed)
            {
                int index = selection.Selected >= 0 ? selection.Selected : forms.FindIndex(x => x.Key == target.Form || x.Value.ID == target.Form);
                index = Mathf.Clamp(index, 0, forms.Count - 1);
                var entry = forms[index];
                Color color = DbTheme.FormAccent(entry.Value);
                Sprite icon = entry.Value.Icon ? DbArt.Icon(entry.Value.Icon) : DbArt.Illustration(entry.Value.Illustration);
                string intent = selection.Armed ? (target.Form == entry.Key || target.Form == entry.Value.ID ? "Already active" : "Ready to transform") : "Center / cancel";
                view.SetPreview(icon, CleanName(entry.Value, entry.Key), intent, color);
                view.SetSelection(selection.Armed ? selection.Selected % CombatWheelSelection.PageSize : -1, color);
                renderedSelection = selection.Selected; renderedArmed = selection.Armed; refreshAt = 0;
            }
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + .1f;
                int index = Mathf.Clamp(selection.Selected, 0, forms.Count - 1);
                var entry = forms[index];
                string first = selection.Armed ? CleanName(entry.Value, entry.Key) : "MOVE TO A FORM  /  SCROLL TO CYCLE";
                string second = selection.Armed ? target.FormatInfoMultiplier(entry.Value.PowerMultiplier, entry.Value.ID) + "  /  KI DRAIN " + entry.Value.KiDrainRate.ToString("0.##") : "";
                string third = "1-" + selection.VisibleCount + ": SELECT  /  Q E: PAGE  " + (selection.Page + 1) + " / " + selection.Pages + "   /   " + forms.Count + " FORMS";
                view.SetDetail(first, second, third);
                var ki = person.GetComponent<KiSystem>();
                float ratio = ki && ki.maxKi > 0 ? Mathf.Clamp01(ki.currentKi / ki.maxKi) : 1;
                view.SetResource("KI " + (ratio * 100).ToString("0") + "%", ratio);
            }
        }

        void LateUpdate()
        {
            if (toast && (!toastTarget || Time.unscaledTime >= toastUntil)) ClearToast();
            if (toast && toastTarget)
            {
                var cam = Global.main ? Global.main.camera : Camera.main; if (!cam) return;
                Vector3 at = cam.WorldToScreenPoint(toastTarget.transform.position + Vector3.up * .62f);
                toast.SetActive(at.z > 0);
                toastRect.anchoredPosition = new Vector2(Mathf.Clamp(at.x,150,Screen.width-150),Mathf.Clamp(at.y,32,Screen.height-32));
                toast.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((toastUntil-Time.unscaledTime)/.22f);
            }
        }

        public void ShowFormTitle(TransformationContextMenu menu, string key)
        {
            if (!menu || !menu.person || !menu.person.IsAlive() || !menu.TransformationsData) return;
            ClearToast();
            Stats stats; if (!menu.TransformationsData.transformations.TransformationData.TryGetValue(key,out stats)) return;
            toast = new GameObject("DBPU form title",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            toast.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; toast.GetComponent<Canvas>().sortingOrder = 31999;
            toastRect = new GameObject("Title",typeof(RectTransform)).GetComponent<RectTransform>(); toastRect.SetParent(toast.transform,false);
            toastRect.anchorMin = toastRect.anchorMax = Vector2.zero;
            Color color = TitleColor(stats);
            TitleBox("Background",new Vector2(0,0),new Vector2(290,46),new Color(.035f,.045f,.075f,.9f));
            TitleBox("Line",new Vector2(0,22),new Vector2(290,2),color);
            var label = new GameObject("Name",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(toastRect,false);
            var labelRect = label.GetComponent<RectTransform>(); labelRect.anchoredPosition = Vector2.zero; labelRect.sizeDelta = new Vector2(278,38);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            text.text = CleanName(stats,key).ToUpperInvariant(); text.fontSize = 17;
            text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false;
            text.enableAutoSizing = true; text.fontSizeMin = 17*.75f; text.fontSizeMax = 17; text.overflowMode = TextOverflowModes.Ellipsis;
            toastTarget = menu; toastUntil = Time.unscaledTime + 1.2f;
        }

        void TitleBox(string name,Vector2 position,Vector2 size,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(toastRect,false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        }

        static Color TitleColor(Stats stats)
        {
            if (stats.requiredKiColor.a > .01f && stats.requiredKiColor.maxColorComponent > .1f)
                return Color.Lerp(stats.requiredKiColor,Color.white,.22f);
            string id = (stats.AuraID ?? stats.ID ?? "").ToLowerInvariant();
            if (id.Contains("blue")) return new Color(.25f,.78f,1);
            if (id.Contains("rose") || id.Contains("ego")) return new Color(.84f,.4f,1);
            if (id.Contains("god") || id.Contains("kaioken")) return new Color(1,.38f,.3f);
            if (id.Contains("ssj") || id.Contains("gold")) return new Color(1,.82f,.32f);
            return new Color(.76f,.87f,1);
        }

        void ClearToast() { if (toast) Destroy(toast); toast = null; toastTarget = null; }
        static string CleanName(Stats stats,string fallback) { return string.IsNullOrEmpty(stats.Name) ? fallback : System.Text.RegularExpressions.Regex.Replace(stats.Name,"<[^>]*>",""); }
    }
}
