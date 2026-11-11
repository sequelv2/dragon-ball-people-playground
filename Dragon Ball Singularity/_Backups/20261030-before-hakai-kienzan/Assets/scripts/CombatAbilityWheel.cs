using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DBPU
{
    [DefaultExecutionOrder(-210)]
    public sealed class CombatAbilityWheel : MonoBehaviour
    {
        public const string ActionId = "dbpu.combatability";
        public const KeyCode DefaultKey = KeyCode.U;
        static CombatAbilityWheel instance;
        static readonly bool scriptedQa=Array.IndexOf(Environment.GetCommandLineArgs(),"-dbpu-ki-combat-qa")>=0;
        public static CombatAbilityWheel Instance { get { Ensure(); return instance; } }
        public static bool InputCaptured { get { return instance && (instance.IsOpen || Time.frameCount <= instance.closedFrame); } }
        public static bool AnyMenuCaptured { get { return InputCaptured || CombatTransformationWheel.InputCaptured || SpiritBombTargeting.InputCaptured || CombatHudView.InputCaptured; } }
        public bool IsOpen { get { return root; } }
        public KiHandLoadout Target { get { return target; } }
        public CombatWheelSelection Selection { get { return selection; } }

        readonly CombatWheelSelection selection = new CombatWheelSelection();
        readonly List<CombatAbilityEntry> entries = new List<CombatAbilityEntry>();
        readonly List<Image> cards = new List<Image>();
        readonly List<TextMeshProUGUI> names = new List<TextMeshProUGUI>();
        KiHandLoadout target;
        GameObject root, toast;
        RectTransform panel, kiFill, toastRect;
        CanvasGroup group;
        Image portrait, accent;
        CombatWheelRing ring;
        TextMeshProUGUI title, intent, description, cost, page, resource;
        TMP_FontAsset font;
        Global blockerOwner, pendingRelease;
        KeyCode primary, secondary;
        Vector2 center, mouseAnchor;
        float scale, openedAt, nextRefresh, toastUntil;
        int shownPage = -1, shownSelection = -2, closedFrame = -1, width, height;
        bool shownArmed, ownsBlocker;
        KiHandLoadout toastTarget;

        public static void Ensure()
        {
            KiFistInput.Ensure();
            if (instance) return;
            var go = new GameObject("DBPU Combat Ability Wheel");
            DontDestroyOnLoad(go); instance = go.AddComponent<CombatAbilityWheel>();
        }
        void Awake() { SceneManager.activeSceneChanged += SceneChanged; }
        void SceneChanged(Scene before, Scene after) { Close(false); ClearToast(); }
        void OnApplicationFocus(bool focused) { if (!focused && !scriptedQa) Close(false); }
        void OnDisable() { Close(false); ReleasePending(); ClearToast(); }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            Close(false); ReleasePending(); ClearToast(); if (instance == this) instance = null;
        }

        public static void BindPerson(PersonBehaviour person)
        {
            if (!person || !person.GetComponent<KiSystem>() || person.Limbs == null) return;
            AllyKiReservoir.GetOrCreate(person);
            foreach (var limb in person.Limbs)
            {
                var strikeLimb = ResolveAbilityLimb(limb);
                if (!strikeLimb) continue;
                KiHandLoadout.GetOrCreate(strikeLimb);
                if (strikeLimb != limb && KiHandLoadout.IsFoot(strikeLimb)) KiStrikeLimbAlias.Bind(limb, strikeLimb);
                var physical = limb.PhysicalBehaviour;
                if (!physical || physical.ContextMenuOptions == null || MCP_Utils.CheckContextButton(physical.ContextMenuOptions,"!CombatKiAbilities")) continue;
                var hand = strikeLimb;
                physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("!CombatKiAbilities","Ki Techniques","Fast limb ability selector (hold U)",() =>
                {
                    var context = UnityEngine.Object.FindObjectOfType<global::ContextMenuBehaviour>();
                    if (context) context.Hide();
                    Instance.OpenFor(KiHandLoadout.GetOrCreate(hand),Input.mousePosition,KeyCode.None);
                }));
                physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("!KiFistMode",()=>
                {
                    var fist=hand.GetComponent<KiFistHand>();
                    return !fist || !fist.Equipped ? "Ki Fist: equip" : "Ki Fist mode: "+KiFistInput.ModeLabel(fist.Mode);
                },"Tap the Ki Fist key to change mode; hold it to charge this limb.",()=>
                {
                    var loadout=KiHandLoadout.GetOrCreate(hand);var fist=hand.GetComponent<KiFistHand>();
                    if(!fist || !fist.Equipped)loadout.Equip(KiHandLoadout.FistId);else fist.CycleMode();
                }));
            }
        }

        static LimbBehaviour ResolveHand(PhysicalBehaviour physical)
        {
            if (!physical) return null;
            return ResolveAbilityLimb(physical.GetComponent<LimbBehaviour>());
        }
        public static LimbBehaviour ResolveAbilityLimb(LimbBehaviour limb)
        {
            if (!limb || !limb.Person || !limb.Person.IsAlive() || !limb.Person.GetComponent<KiSystem>()) return null;
            if (KiHandLoadout.IsStrikeLimb(limb)) return limb;
            bool leg = limb.name.StartsWith("UpperLeg", StringComparison.OrdinalIgnoreCase)
                || limb.name.StartsWith("LowerLeg", StringComparison.OrdinalIgnoreCase);
            if (!leg && !limb.name.StartsWith("UpperArm", StringComparison.OrdinalIgnoreCase)) return null;
            string suffix = limb.name.IndexOf("Front",StringComparison.OrdinalIgnoreCase) >= 0 ? "Front" : "";
            foreach (var part in limb.Person.Limbs)
                if (part && part.name == (leg ? "Foot" : "LowerArm") + suffix && KiHandLoadout.IsStrikeLimb(part)) return part;
            return null;
        }
        static KiHandLoadout ResolveTarget()
        {
            var selection = SelectionController.Main;
            var hand = selection != null ? ResolveHand(selection.CurrentlyUnderMouse) : null;
            if (hand) return KiHandLoadout.GetOrCreate(hand);
            var tool = Global.main && Global.main.ToolControllerBehaviour ? Global.main.ToolControllerBehaviour.CurrentTool : null;
            hand = tool != null ? ResolveHand(tool.ActiveSingleSelected) : null;
            if (hand) return KiHandLoadout.GetOrCreate(hand);
            LimbBehaviour result = null;
            if (selection != null && selection.SelectedObjects != null)
                foreach (var physical in selection.SelectedObjects)
                {
                    var candidate = ResolveHand(physical);
                    if (!candidate) continue;
                    if (result && candidate != result) return null;
                    result = candidate;
                }
            return result ? KiHandLoadout.GetOrCreate(result) : null;
        }
        static bool CanOpen()
        {
            if (!Global.main || Global.main.GetPausedMenu() || Global.ActiveUiBlock || DialogBox.IsAnyDialogboxOpen
                || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput) return false;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return false;
            var focused = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return !focused || (!focused.GetComponent<TMP_InputField>() && !focused.GetComponent<InputField>());
        }
        bool Valid()
        {
            return target && target.Limb && target.Limb.gameObject.activeInHierarchy && target.Person
                && target.Person.IsAlive() && target.Person.GetComponent<KiSystem>()
                && KiHandLoadout.IsStrikeLimb(target.Limb)
                && target.Limb.transform.IsChildOf(target.Person.transform);
        }

        void Update()
        {
            if (!IsOpen)
            {
                if (DbpuInputUtil.Down(ActionId, DefaultKey) && CanOpen())
                {
                    var hand = ResolveTarget();
                    if (hand) OpenFor(hand, Input.mousePosition, DbpuInputRegistration.GetActionKey(ActionId, DefaultKey));
                    else ModAPI.Notify("Hover over a DBPU arm or leg, then hold " + DbpuInputRegistration.GetActionKeyLabel(ActionId,DefaultKey));
                }
                return;
            }
            if (!Valid() || !blockerOwner || blockerOwner != Global.main || blockerOwner.GetPausedMenu()
                || blockerOwner.ActiveUiBlockers > 1 || DialogBox.IsAnyDialogboxOpen || width != Screen.width || height != Screen.height)
            { Close(false); return; }
            if(scriptedQa){Refresh();return;}
            if ((primary != KeyCode.Escape && Input.GetKeyDown(KeyCode.Escape)) || (primary != KeyCode.Mouse1 && Input.GetMouseButtonDown(1)))
            { Close(false); return; }
            Vector2 mouse = Input.mousePosition;
            if ((mouse - mouseAnchor).sqrMagnitude > 9)
            {
                mouseAnchor = mouse; PointAt((mouse-center)/scale);
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f) selection.Step(scroll > 0 ? 1 : -1);
            if ((primary != KeyCode.RightArrow && Input.GetKeyDown(KeyCode.RightArrow)) || (primary != KeyCode.DownArrow && Input.GetKeyDown(KeyCode.DownArrow))) selection.Step(1);
            if ((primary != KeyCode.LeftArrow && Input.GetKeyDown(KeyCode.LeftArrow)) || (primary != KeyCode.UpArrow && Input.GetKeyDown(KeyCode.UpArrow))) selection.Step(-1);
            if (primary != KeyCode.Q && Input.GetKeyDown(KeyCode.Q)) selection.ChangePage(-1);
            if (primary != KeyCode.E && Input.GetKeyDown(KeyCode.E)) selection.ChangePage(1);
            for (int slot=0;slot<selection.VisibleCount;slot++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1+slot);
                if (primary != key && Input.GetKeyDown(key)) selection.SelectSlot(slot);
            }
            if (primary != KeyCode.None && (!Input.GetKey(primary) || (secondary != KeyCode.None && !Input.GetKey(secondary)))) { Close(true); return; }
            if (primary == KeyCode.None && Input.GetMouseButtonDown(0)) { Close(selection.Armed); return; }
            Refresh();
        }

        public bool OpenFor(KiHandLoadout loadout, Vector2 position, KeyCode heldKey)
        {
            if (!loadout || !Global.main || Global.ActiveUiBlock || Global.main.GetPausedMenu() || DialogBox.IsAnyDialogboxOpen) return false;
            target = loadout;
            if (!Valid()) { target = null; return false; }
            entries.Clear(); entries.AddRange(loadout.GetEntries());
            if (entries.Count == 0) { target = null; return false; }
            selection.Reset(entries.Count, Math.Max(0, entries.FindIndex(e=>e.Id == loadout.SelectedId)));
            primary = heldKey; secondary = KeyCode.None;
            InputAction action;
            if (heldKey != KeyCode.None && InputSystem.Actions.TryGetValue(ActionId,out action)) secondary = action.SecondaryKey;
            width = Screen.width; height = Screen.height;
            scale = Mathf.Min(1.05f,Mathf.Min(width/900f,height/700f));
            center = new Vector2(Mathf.Clamp(position.x,300*scale,width-300*scale),Mathf.Clamp(position.y,326*scale,height-326*scale));
            mouseAnchor = Input.mousePosition; openedAt = Time.unscaledTime; nextRefresh = 0;
            shownPage = -1; shownSelection = -2; ClearToast();
            try
            {
                Build(); blockerOwner = Global.main; blockerOwner.AddUiBlocker(); ownsBlocker = true; Refresh(); return true;
            }
            catch { Close(false); throw; }
        }
        public void Navigate(int direction) { selection.Step(direction); Refresh(); }
        public void ChangePage(int direction) { selection.ChangePage(direction); Refresh(); }
        public void ChooseSlot(int slot) { selection.SelectSlot(slot); Refresh(); }
        public void PointAt(Vector2 local) { selection.Select(selection.Hit(local.x,local.y,84,312)); Refresh(); }
        public void Close(bool commit)
        {
            var oldTarget = target;
            CombatAbilityEntry entry = null;
            if (commit && Valid() && selection.Armed && selection.Selected >= 0 && selection.Selected < entries.Count) entry = entries[selection.Selected];
            if (root) { root.SetActive(false); Destroy(root); root = null; }
            if (ownsBlocker)
            {
                if (blockerOwner)
                {
                    if (isActiveAndEnabled) { pendingRelease = blockerOwner; StartCoroutine(ReleaseAfterFrame()); }
                    else blockerOwner.RemoveUiBlocker();
                }
                ownsBlocker = false;
            }
            blockerOwner = null; if (target) closedFrame = Time.frameCount;
            target = null; entries.Clear(); cards.Clear(); names.Clear();
            if (entry != null && oldTarget && oldTarget.Equip(entry.Id)) ShowToast(oldTarget,entry);
        }
        IEnumerator ReleaseAfterFrame() { yield return new WaitForEndOfFrame(); ReleasePending(); }
        void ReleasePending() { if (pendingRelease) pendingRelease.RemoveUiBlocker(); pendingRelease = null; }

        void Build()
        {
            font = TMP_Settings.defaultFontAsset;
            if(!font)font=ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            root = new GameObject("DBPU Combat Ability Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var shield = Box("Input shield",root.transform,Vector2.zero,Vector2.zero,new Color(0,0,0,.045f));
            var sr = shield.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero; shield.raycastTarget = true;
            panel = new GameObject("Ability wheel",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(root.transform,false); panel.anchorMin = panel.anchorMax = Vector2.zero; panel.anchoredPosition = center;
            panel.sizeDelta = new Vector2(600,652); group = panel.GetComponent<CanvasGroup>();
            Box("Header",panel,new Vector2(0,285),new Vector2(476,58),new Color(.035f,.045f,.075f,.97f));
            var identity = target.Person.GetComponent<DbpuCharacterIdentity>();
            string fighter = identity != null && !string.IsNullOrEmpty(identity.DisplayName) ? identity.DisplayName : target.Person.name;
            string hand = (target.Limb.name.Contains("Front") ? "FRONT " : "BACK ")
                + (KiHandLoadout.IsFoot(target.Limb) ? "FOOT" : "ARM");
            Label("Fighter",panel,new Vector2(0,297),new Vector2(456,22),"KI TECHNIQUES / " + fighter.ToUpperInvariant() + " / " + hand,18,new Color(.85f,.93f,1));
            Box("Ki rail",panel,new Vector2(-72,273),new Vector2(302,3),new Color(.14f,.19f,.27f));
            kiFill = Box("Ki fill",panel,new Vector2(-223,273),new Vector2(302,3),new Color(.27f,.84f,1)).rectTransform; kiFill.pivot = new Vector2(0,.5f);
            resource = Label("Resource",panel,new Vector2(157,274),new Vector2(136,18),"",10,new Color(.5f,.8f,.95f));
            ring = new GameObject("Ability sectors",typeof(RectTransform),typeof(CombatWheelRing)).GetComponent<CombatWheelRing>();
            ring.transform.SetParent(panel,false); ring.rectTransform.sizeDelta = new Vector2(520,520); ring.raycastTarget = false;
            Box("Center",panel,Vector2.zero,new Vector2(144,156),new Color(.035f,.045f,.075f,.97f));
            accent = Box("Center accent",panel,new Vector2(0,77),new Vector2(144,2),Color.white);
            portrait = Box("Ability icon",panel,new Vector2(0,23),new Vector2(68,68),Color.white); portrait.preserveAspect = true;
            title = Label("Preview name",panel,new Vector2(0,-34),new Vector2(138,43),"",20,Color.white);
            intent = Label("Intent",panel,new Vector2(0,-67),new Vector2(138,16),"",10,new Color(.6f,.72f,.85f));
            Box("Technique description",panel,new Vector2(0,-274),new Vector2(550,70),new Color(.035f,.045f,.075f,.97f));
            description = Label("Description",panel,new Vector2(0,-252),new Vector2(526,28),"",15,new Color(.87f,.91f,1));
            cost = Label("Cost",panel,new Vector2(0,-277),new Vector2(526,20),"",13,new Color(1,.81f,.43f));
            page = Label("Page",panel,new Vector2(0,-298),new Vector2(526,16),"",9,new Color(.58f,.68f,.80f));
            string confirm = primary == KeyCode.None ? "CLICK: EQUIP" : "RELEASE " + primary.ToString().ToUpperInvariant() + ": EQUIP";
            Label("Controls",panel,new Vector2(0,-322),new Vector2(590,16),confirm + " / WHEEL or ARROWS / ESC or RMB: CANCEL",10,new Color(.75f,.82f,.91f));
        }
        void RebuildCards()
        {
            foreach (var card in cards) if (card) Destroy(card.gameObject);
            cards.Clear(); names.Clear();
            for (int slot=0;slot<selection.VisibleCount;slot++)
            {
                var entry = entries[selection.Page*CombatWheelSelection.PageSize+slot];
                float angle = slot*Mathf.PI*2/selection.VisibleCount;
                var card = Box("Ability " + entry.Id,panel,new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*178,new Vector2(108,88),new Color(.045f,.06f,.095f,.97f));
                cards.Add(card); Box("Color",card.transform,new Vector2(0,43),new Vector2(108,2),entry.Color);
                var icon = Box("Icon",card.transform,new Vector2(0,10),new Vector2(44,44),Color.white); icon.sprite = entry.Icon; icon.enabled = icon.sprite; icon.preserveAspect = true;
                Label("Slot",card.transform,new Vector2(-44,31),new Vector2(15,15),(slot+1).ToString(),10,new Color(.6f,.68f,.8f));
                names.Add(Label("Name",card.transform,new Vector2(0,-27),new Vector2(102,30),entry.Name,14,Color.white));
            }
            shownPage = selection.Page; shownSelection = -2;
        }
        void Refresh()
        {
            if (!IsOpen || !Valid()) return;
            if (shownPage != selection.Page) RebuildCards();
            float animation = Mathf.Clamp01((Time.unscaledTime-openedAt)/.12f);
            group.alpha = animation; panel.localScale = Vector3.one*scale*Mathf.Lerp(.93f,1,Mathf.SmoothStep(0,1,animation));
            if (shownSelection != selection.Selected || shownArmed != selection.Armed)
            {
                int index = selection.Selected >= 0 ? selection.Selected : entries.FindIndex(e=>e.Id == target.SelectedId);
                var entry = entries[Mathf.Clamp(index,0,entries.Count-1)];
                portrait.sprite = entry.Icon; portrait.enabled = portrait.sprite; title.text = entry.Name;
                title.color = entry.Color; accent.color = entry.Color;
                intent.text = selection.Armed ? (entry.Id == target.SelectedId ? "ALREADY EQUIPPED" : "EQUIP ON RELEASE") : "CENTER / CANCEL";
                description.text = entry.Description; cost.text = entry.Cost;
                for (int slot=0;slot<cards.Count;slot++)
                {
                    int idx = selection.Page*CombatWheelSelection.PageSize+slot;
                    bool chosen = selection.Armed && idx == selection.Selected;
                    bool current = entries[idx].Id == target.SelectedId;
                    cards[slot].color = chosen ? Color.Lerp(new Color(.08f,.1f,.16f),entry.Color,.28f) : new Color(.045f,.06f,.095f,.97f);
                    cards[slot].rectTransform.localScale = Vector3.one*(chosen ? 1.09f : 1);
                    names[slot].color = chosen ? entry.Color : current ? new Color(.52f,.92f,.75f) : new Color(.85f,.9f,.96f);
                }
                ring.Set(selection.VisibleCount,selection.Armed ? selection.Selected%CombatWheelSelection.PageSize : -1,entry.Color);
                shownSelection = selection.Selected; shownArmed = selection.Armed;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime+.1f;
                var ki = target.Person.GetComponent<KiSystem>();
                float fraction = ki && ki.maxKi > 0 ? Mathf.Clamp01(ki.currentKi/ki.maxKi) : 0;
                kiFill.sizeDelta = new Vector2(302*fraction,3);
                var allies = target.Person.GetComponent<AllyKiReservoir>();
                resource.text = "KI " + (fraction*100).ToString("0") + "% / +" + (allies ? allies.StoredBars*100 : 0).ToString("0") + "%";
                page.text = "ACTIVATE ARM: USE / HOLD TO CHARGE / 1-"+selection.VisibleCount+": SELECT / Q E: PAGE "+(selection.Page+1)+"/"+selection.Pages;
            }
        }
        void ShowToast(KiHandLoadout hand,CombatAbilityEntry entry)
        {
            ClearToast(); toastTarget = hand; toastUntil = Time.unscaledTime+1.2f;
            toast = new GameObject("DBPU equipped ability",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            toast.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; toast.GetComponent<Canvas>().sortingOrder = 31999;
            toastRect = new GameObject("Title",typeof(RectTransform)).GetComponent<RectTransform>(); toastRect.SetParent(toast.transform,false);
            toastRect.anchorMin = toastRect.anchorMax = Vector2.zero;
            Box("Background",toastRect,Vector2.zero,new Vector2(270,40),new Color(.035f,.045f,.075f,.93f));
            Label("Equipped",toastRect,Vector2.zero,new Vector2(260,34),entry.Name.ToUpperInvariant(),16,entry.Color);
        }
        void LateUpdate()
        {
            if (toast && (!toastTarget || !toastTarget.Limb || Time.unscaledTime >= toastUntil)) ClearToast();
            if (!toast) return;
            var camera = Global.main ? Global.main.camera : Camera.main; if (!camera) return;
            var at = camera.WorldToScreenPoint(toastTarget.Limb.transform.position+Vector3.up*.6f);
            toast.SetActive(at.z>0); toastRect.anchoredPosition = new Vector2(Mathf.Clamp(at.x,145,Screen.width-145),Mathf.Clamp(at.y,28,Screen.height-28));
            toast.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((toastUntil-Time.unscaledTime)/.22f);
        }
        void ClearToast() { if (toast) Destroy(toast); toast = null; toastTarget = null; }
        static Image Box(string name,Transform parent,Vector2 at,Vector2 size,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = at; rt.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        TextMeshProUGUI Label(string name,Transform parent,Vector2 at,Vector2 size,string text,float fontSize,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = at; rt.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.enableAutoSizing = true;
            label.fontSizeMin = fontSize*.70f; label.fontSizeMax = fontSize; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
    }
}
