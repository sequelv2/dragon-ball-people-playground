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
        KiHandLoadout target;
        GameObject root, toast;
        RectTransform toastRect;
        CombatWheelView view;
        string description = "", cost = "";
        Global blockerOwner, pendingRelease;
        KeyCode primary, secondary;
        Vector2 center, mouseAnchor;
        float scale, nextRefresh, toastUntil;
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
            mouseAnchor = Input.mousePosition; nextRefresh = 0;
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
            target = null; entries.Clear(); view = null;
            if (entry != null && oldTarget && oldTarget.Equip(entry.Id)) ShowToast(oldTarget,entry);
        }
        IEnumerator ReleaseAfterFrame() { yield return new WaitForEndOfFrame(); ReleasePending(); }
        void ReleasePending() { if (pendingRelease) pendingRelease.RemoveUiBlocker(); pendingRelease = null; }

        void Build()
        {
            var identity = target.Person.GetComponent<DbpuCharacterIdentity>();
            string fighter = identity != null && !string.IsNullOrEmpty(identity.DisplayName) ? identity.DisplayName : target.Person.name;
            string hand = (target.Limb.name.Contains("Front") ? "Front " : "Back ")
                + (KiHandLoadout.IsFoot(target.Limb) ? "foot" : "arm");
            string confirm = primary == KeyCode.None ? "CLICK: EQUIP" : "RELEASE " + primary.ToString().ToUpperInvariant() + ": EQUIP";
            view = new CombatWheelView("DBPU Combat Ability Canvas", "Ability wheel", "Ki techniques / " + fighter + " / " + hand, confirm + " / WHEEL or ARROWS / ESC or RMB: CANCEL", center, scale);
            root = view.Root;
        }
        void RebuildSlots()
        {
            view.BeginSlots(selection.VisibleCount);
            for (int slot=0;slot<selection.VisibleCount;slot++)
            {
                var entry = entries[selection.Page*CombatWheelSelection.PageSize+slot];
                view.SetSlot(slot,DbArt.PowerIcon(entry.Icon),entry.Name,entry.Color,entry.Id == target.SelectedId);
            }
            shownPage = selection.Page; shownSelection = -2;
        }
        void Refresh()
        {
            if (!IsOpen || !Valid()) return;
            if (shownPage != selection.Page) RebuildSlots();
            if (shownSelection != selection.Selected || shownArmed != selection.Armed)
            {
                int index = selection.Selected >= 0 ? selection.Selected : entries.FindIndex(e=>e.Id == target.SelectedId);
                var entry = entries[Mathf.Clamp(index,0,entries.Count-1)];
                string intent = selection.Armed ? (entry.Id == target.SelectedId ? "Already equipped" : "Equip on release") : "Center / cancel";
                view.SetPreview(DbArt.PowerIcon(entry.Icon),entry.Name,intent,entry.Color);
                view.SetSelection(selection.Armed ? selection.Selected%CombatWheelSelection.PageSize : -1,entry.Color);
                description = entry.Description; cost = entry.Cost; nextRefresh = 0;
                shownSelection = selection.Selected; shownArmed = selection.Armed;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime+.1f;
                var ki = target.Person.GetComponent<KiSystem>();
                float fraction = ki && ki.maxKi > 0 ? Mathf.Clamp01(ki.currentKi/ki.maxKi) : 0;
                var allies = target.Person.GetComponent<AllyKiReservoir>();
                view.SetResource("KI " + (fraction*100).ToString("0") + "% / +" + (allies ? allies.StoredBars*100 : 0).ToString("0") + "%",fraction);
                view.SetDetail(description,cost,"ACTIVATE ARM: USE / HOLD TO CHARGE / 1-"+selection.VisibleCount+": SELECT / Q E: PAGE "+(selection.Page+1)+"/"+selection.Pages);
            }
        }
        static string LimbCaption(LimbBehaviour limb)
        {
            if (!limb) return "";
            bool front = limb.name.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0;
            return (front ? "RIGHT " : "LEFT ") + (KiHandLoadout.IsFoot(limb) ? "FOOT" : "LOWER ARM");
        }
        void ShowToast(KiHandLoadout hand,CombatAbilityEntry entry)
        {
            // Reference-style caption above the fighter: technique name over the limb.
            ClearToast();
            if (hand && hand.Person) { DbpuWorldLabel.Show(hand.Person, entry.Name, LimbCaption(hand.Limb), 1.4f); return; }
            toastTarget = hand; toastUntil = Time.unscaledTime+1.2f;
            toast = CombatWheelView.BuildToast("DBPU equipped ability",entry.Name.ToUpperInvariant(),entry.Color,out toastRect);
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
    }
}
