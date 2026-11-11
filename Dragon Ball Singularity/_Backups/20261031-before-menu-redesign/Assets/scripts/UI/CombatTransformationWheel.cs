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
        readonly List<Image> cards = new List<Image>();
        readonly List<TextMeshProUGUI> cardNames = new List<TextMeshProUGUI>();
        TransformationContextMenu target;
        PersonBehaviour person;
        Global blockerOwner, deferredBlocker;
        GameObject root, toast;
        RectTransform panel, kiFill;
        CanvasGroup group;
        Image portrait, accent;
        TextMeshProUGUI formName, detail, pageText, intent, kiText;
        CombatWheelRing ring;
        TMP_FontAsset font;
        KeyCode heldKey, secondaryKey;
        Vector2 pointerAnchor, center;
        float uiScale = 1, openedAt, toastUntil, refreshAt;
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
            renderedPage = -1; renderedSelection = -2; refreshAt = 0; openedAt = Time.unscaledTime;
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
            target = null; person = null; forms.Clear(); cards.Clear(); cardNames.Clear();
            if (apply && oldTarget)
            {
                oldTarget.ApplyForm(key);
                ShowToast(oldTarget, key);
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
            font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            root = new GameObject("DBPU Combat Wheel Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var shield = Box("Input shield", root.transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .045f));
            var sr = shield.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero; shield.raycastTarget = true;
            panel = new GameObject("Wheel", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false); panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.anchoredPosition = center; panel.sizeDelta = new Vector2(600, 652); group = panel.GetComponent<CanvasGroup>();
            Box("Header", panel, new Vector2(0, 285), new Vector2(444, 58), new Color(.035f,.045f,.075f,.96f));
            var identity = person.GetComponent<DbpuCharacterIdentity>();
            string name = identity != null && !string.IsNullOrEmpty(identity.DisplayName) ? identity.DisplayName : person.name.Replace("(Clone)", "");
            Label("Target", panel, new Vector2(0, 297), new Vector2(416, 22), "TRANSFORM  /  " + name.ToUpperInvariant(), 15, new Color(.88f,.91f,1));
            Box("Ki rail", panel, new Vector2(-61, 273), new Vector2(294, 3), new Color(.16f,.20f,.28f));
            kiFill = Box("Ki fill", panel, new Vector2(-208, 273), new Vector2(294, 3), new Color(.32f,.8f,1)).rectTransform; kiFill.pivot = new Vector2(0,.5f);
            kiText = Label("Ki", panel, new Vector2(156, 274), new Vector2(112, 18), "", 11, new Color(.57f,.78f,.92f));
            ring = new GameObject("Selection sectors", typeof(RectTransform), typeof(CombatWheelRing)).GetComponent<CombatWheelRing>();
            ring.transform.SetParent(panel, false); ring.rectTransform.sizeDelta = new Vector2(520,520); ring.raycastTarget = false;
            Box("Center", panel, Vector2.zero, new Vector2(144,156), new Color(.035f,.045f,.075f,.96f));
            accent = Box("Accent", panel, new Vector2(0, 77), new Vector2(144, 2), Color.white);
            portrait = Box("Portrait", panel, new Vector2(0, 22), new Vector2(76,76), Color.white); portrait.preserveAspect = true;
            formName = Label("Preview name", panel, new Vector2(0,-40), new Vector2(138,40), "", 15, Color.white);
            intent = Label("Intent", panel, new Vector2(0,-67), new Vector2(138,16), "", 9, new Color(.6f,.72f,.85f));
            Box("Detail panel", panel, new Vector2(0,-274), new Vector2(516,52), new Color(.035f,.045f,.075f,.96f));
            detail = Label("Details", panel, new Vector2(0,-264), new Vector2(490,24), "", 14, Color.white);
            pageText = Label("Page", panel, new Vector2(0,-287), new Vector2(490,16), "", 10, new Color(.58f,.66f,.78f));
            string hold = heldKey == KeyCode.None ? "CLICK TO APPLY" : "RELEASE " + KeyLabel().ToUpperInvariant() + " TO APPLY";
            Label("Controls", panel, new Vector2(0,-314), new Vector2(590,20), hold + "   /   WHEEL or ARROWS   /   ESC or RMB: CANCEL", 10, new Color(.74f,.8f,.9f));
        }

        void RebuildCards()
        {
            foreach (var card in cards) if (card) Destroy(card.gameObject);
            cards.Clear(); cardNames.Clear();
            for (int slot = 0; slot < selection.VisibleCount; slot++)
            {
                int index = selection.Page * CombatWheelSelection.PageSize + slot;
                var entry = forms[index];
                float angle = slot * Mathf.PI * 2 / selection.VisibleCount;
                Vector2 pos = new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)) * 178;
                var card = Box("Form " + entry.Key, panel, pos, new Vector2(108,88), new Color(.05f,.065f,.10f,.96f));
                cards.Add(card);
                Box("Color", card.transform, new Vector2(0,43), new Vector2(108,2), ColorFor(entry.Value));
                var icon = Box("Icon", card.transform, new Vector2(0,10), new Vector2(44,44), Color.white); icon.sprite = entry.Value.Icon ? entry.Value.Icon : entry.Value.Illustration; icon.enabled = icon.sprite; icon.preserveAspect = true;
                Label("Slot", card.transform, new Vector2(-44,31), new Vector2(15,15), (slot+1).ToString(), 10, new Color(.6f,.68f,.8f));
                cardNames.Add(Label("Name", card.transform, new Vector2(0,-27), new Vector2(102,30), CleanName(entry.Value,entry.Key), 10, Color.white));
            }
            renderedPage = selection.Page; renderedSelection = -2;
        }

        void Refresh()
        {
            if (!IsOpen || !ValidTarget()) return;
            if (renderedPage != selection.Page) RebuildCards();
            float animation = Mathf.Clamp01((Time.unscaledTime - openedAt) / .12f);
            group.alpha = animation; panel.localScale = Vector3.one * uiScale * Mathf.Lerp(.93f,1,Mathf.SmoothStep(0,1,animation));
            if (renderedSelection != selection.Selected || renderedArmed != selection.Armed)
            {
                int index = selection.Selected >= 0 ? selection.Selected : forms.FindIndex(x=>x.Key == target.Form || x.Value.ID == target.Form);
                index = Mathf.Clamp(index,0,forms.Count-1);
                var entry = forms[index]; Color color = ColorFor(entry.Value);
                portrait.sprite = entry.Value.Illustration ? entry.Value.Illustration : entry.Value.Icon; portrait.enabled = portrait.sprite;
                formName.text = CleanName(entry.Value,entry.Key); formName.color = color; accent.color = color;
                intent.text = selection.Armed ? (target.Form == entry.Key || target.Form == entry.Value.ID ? "ALREADY ACTIVE" : "READY TO TRANSFORM") : "CENTER / CANCEL";
                for (int slot = 0; slot < cards.Count; slot++)
                {
                    int idx = selection.Page * CombatWheelSelection.PageSize + slot;
                    bool chosen = selection.Armed && idx == selection.Selected;
                    bool current = forms[idx].Key == target.Form || forms[idx].Value.ID == target.Form;
                    cards[slot].color = chosen ? Color.Lerp(new Color(.08f,.10f,.16f),color,.30f) : new Color(.045f,.06f,.095f,.97f);
                    cards[slot].rectTransform.localScale = Vector3.one * (chosen ? 1.09f : 1);
                    cardNames[slot].color = chosen ? color : current ? new Color(.54f,.9f,.74f) : new Color(.86f,.89f,.96f);
                }
                ring.Set(selection.VisibleCount, selection.Armed ? selection.Selected % CombatWheelSelection.PageSize : -1, color);
                renderedSelection = selection.Selected; renderedArmed = selection.Armed; refreshAt = 0;
            }
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + .1f;
                int index = Mathf.Clamp(selection.Selected,0,forms.Count-1);
                var entry = forms[index];
                detail.text = selection.Armed ? CleanName(entry.Value,entry.Key) + "  /  " + target.FormatInfoMultiplier(entry.Value.PowerMultiplier,entry.Value.ID) + "  /  KI DRAIN " + entry.Value.KiDrainRate.ToString("0.##") : "MOVE TO A FORM  /  SCROLL TO CYCLE";
                pageText.text = "1-" + selection.VisibleCount + ": SELECT  /  Q E: PAGE  " + (selection.Page+1) + " / " + selection.Pages + "   /   " + forms.Count + " FORMS";
                var ki = person.GetComponent<KiSystem>();
                float ratio = ki && ki.maxKi > 0 ? Mathf.Clamp01(ki.currentKi / ki.maxKi) : 1;
                kiFill.sizeDelta = new Vector2(294 * ratio,3); kiText.text = "KI " + (ratio * 100).ToString("0") + "%";
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

        void ShowToast(TransformationContextMenu menu, string key)
        {
            ClearToast();
            Stats stats; if (!menu.TransformationsData.transformations.TransformationData.TryGetValue(key,out stats)) return;
            toast = new GameObject("DBPU form title",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            toast.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; toast.GetComponent<Canvas>().sortingOrder = 31999;
            toastRect = new GameObject("Title",typeof(RectTransform)).GetComponent<RectTransform>(); toastRect.SetParent(toast.transform,false);
            toastRect.anchorMin = toastRect.anchorMax = Vector2.zero;
            Box("Background",toastRect,Vector2.zero,new Vector2(290,46),new Color(.035f,.045f,.075f,.9f));
            Box("Line",toastRect,new Vector2(0,22),new Vector2(290,2),ColorFor(stats));
            Label("Name",toastRect,Vector2.zero,new Vector2(278,38),CleanName(stats,key).ToUpperInvariant(),17,ColorFor(stats));
            toastTarget = menu; toastUntil = Time.unscaledTime + 1.2f;
        }

        void ClearToast() { if (toast) Destroy(toast); toast = null; toastTarget = null; }
        static string CleanName(Stats stats,string fallback) { return string.IsNullOrEmpty(stats.Name) ? fallback : System.Text.RegularExpressions.Regex.Replace(stats.Name,"<[^>]*>",""); }
        static Color ColorFor(Stats stats)
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
        static Image Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        TextMeshProUGUI Label(string name,Transform parent,Vector2 position,Vector2 size,string text,float fontSize,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = color; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = fontSize*.75f; label.fontSizeMax = fontSize; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
    }

    // One bounded UI mesh; no per-frame textures, materials, particles or world objects.
    public sealed class CombatWheelRing : MaskableGraphic
    {
        int count, selected = -1; Color accent;
        public void Set(int count,int selected,Color accent) { this.count=count; this.selected=selected; this.accent=accent; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (count <= 0) return;
            const int steps=16;
            for (int slot=0;slot<count;slot++)
            {
                Color tint = slot==selected ? new Color(accent.r,accent.g,accent.b,.22f) : new Color(.08f,.11f,.18f,.68f);
                float start=(slot-.5f)*Mathf.PI*2/count+.018f, end=(slot+.5f)*Mathf.PI*2/count-.018f;
                for (int i=0;i<steps;i++)
                {
                    float a=Mathf.Lerp(start,end,i/(float)steps),b=Mathf.Lerp(start,end,(i+1)/(float)steps);
                    int n=vh.currentVertCount;
                    vh.AddVert(new Vector3(Mathf.Sin(a)*94,Mathf.Cos(a)*94),tint,Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(a)*234,Mathf.Cos(a)*234),tint,Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(b)*234,Mathf.Cos(b)*234),tint,Vector2.zero);
                    vh.AddVert(new Vector3(Mathf.Sin(b)*94,Mathf.Cos(b)*94),tint,Vector2.zero);
                    vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
                }
            }
        }
    }
}
