using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // One technique started from the torso: Ki Shield, Flight, Kaioken, G.O.D's Wrath, Final Explosion.
    internal sealed class TorsoTechnique
    {
        public string Id, Name;
        public Sprite Icon;
        public bool IconIsGlyph;
        public Color Accent;
        public Func<string> Tag;
        public Func<bool> TagLit;
        // Starts the technique; returns null when it started, otherwise the reason it could not.
        public Func<string> Execute;
        // Keeps firing while Use stays held after the confirming press.
        public GodsWrathBehaviour Hold;
    }

    // R on a fighter's torso opens a small picker above the head; R again steps through the
    // torso techniques that fighter has (Shift steps back) and Use (F) starts the shown one. The
    // Use press that confirms belongs to the picker: the fighter's own Use triggers skip that frame,
    // and held Use does not reach the torso's continuous techniques until it is released.
    [DefaultExecutionOrder(-235)]
    public sealed class TorsoTechniquePicker : MonoBehaviour
    {
        public const string ActionId = "dbpu.torsotechniques";
        public const KeyCode DefaultKey = KeyCode.R;
        const string UseActionId = "activateDirect";
        const float IdleSeconds = 5f;
        internal static readonly Color KaiokenRed = DbTheme.Hex(0xFF5048);
        static readonly Color KiCyan = DbTheme.Ki;
        static TorsoTechniquePicker instance;
        static int consumedFrame = -1, blockFrame = -1;
        static Transform blockRoot;
        static KeyCode consumedKey = DefaultKey, consumedSecondary = KeyCode.None;
        static PersonBehaviour heldPerson;
        static GodsWrathBehaviour heldWrath;
        static KeyCode useKey = KeyCode.F, useSecondary = KeyCode.None;

        PersonBehaviour person;
        List<TorsoTechnique> techniques = new List<TorsoTechnique>();
        int index;
        float lastInput, nextRefresh, failureUntil;
        string failure;
        readonly Dictionary<int, string> remembered = new Dictionary<int, string>();

        public static void Ensure()
        {
            KaiokenHeadTap.Ensure();
            if (instance) return;
            var go = new GameObject("DBPU torso technique picker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TorsoTechniquePicker>();
        }

        void Awake() { SceneManager.activeSceneChanged += SceneChanged; }
        void SceneChanged(Scene before, Scene after) { Close(false); ReleaseHold(); }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            Close(false); ReleaseHold();
            if (instance == this) instance = null;
        }

        public static bool IsOpenFor(PersonBehaviour p) { return instance && p && instance.person == p; }

        // Redraws an open picker now; true when it is showing the given technique.
        internal static bool RefreshIfShowing(PersonBehaviour p, string id)
        {
            if (!IsOpenFor(p) || instance.techniques.Count == 0) return false;
            instance.Show();
            return instance.techniques[Mathf.Clamp(instance.index, 0, instance.techniques.Count - 1)].Id == id;
        }

        // Other actions bound to the same key skip the frame in which the picker took it.
        public static bool BlocksAction(string id, KeyCode fallback)
        {
            if (consumedFrame != Time.frameCount || id == ActionId) return false;
            InputAction action;
            KeyCode primary = fallback, secondary = KeyCode.None;
            if (InputSystem.Actions.TryGetValue(id, out action)) { primary = action.Key; secondary = action.SecondaryKey; }
            return primary == consumedKey && (secondary == KeyCode.None || secondary == consumedSecondary || Input.GetKey(secondary));
        }

        // True for the Use press that confirmed a technique, on every part of that fighter.
        public static bool BlocksUse(Component c)
        {
            return blockFrame == Time.frameCount && blockRoot && c && c.transform.root == blockRoot;
        }

        // After a confirm, held Use belongs to the picker until it is released.
        public static bool HoldsUse(PersonBehaviour p) { return heldPerson && p && heldPerson == p; }

        public static bool IsTorso(LimbBehaviour limb)
        {
            if (!limb) return false;
            string n = limb.name;
            return n == "UpperBody" || n == "MiddleBody" || n == "LowerBody";
        }

        static bool PointerOverUi() { return EventSystem.current && EventSystem.current.IsPointerOverGameObject(); }
        static bool ShiftHeld() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }

        void Update()
        {
            TickHold();
            if (person && (!person.IsAlive() || !person.gameObject.activeInHierarchy)) Close(false);
            if (!KiCombatMath.CanAct())
            {
                // Menus, dialogs, the wheels and the pause screen close it; it never acts behind them.
                if (person) Close(false);
                return;
            }
            if (person && Time.unscaledTime - lastInput > IdleSeconds) Close(false);
            if (person && InputSystem.Down(UseActionId) && !PointerOverUi() && UseReachesPerson()) { Confirm(); return; }
            if (!PointerOverUi() && DbpuInputUtil.Down(ActionId, DefaultKey)) Next(ShiftHeld());
            if (person && Time.unscaledTime >= nextRefresh) Refresh();
        }

        // A torso limb of a live fighter, unless R already charges ki there (Cell Max's upper body).
        static LimbBehaviour TorsoOf(PhysicalBehaviour physical)
        {
            var limb = physical ? physical.GetComponent<LimbBehaviour>() : null;
            if (!IsTorso(limb) || !limb.Person || !limb.Person.IsAlive() || limb.GetComponent<KiChargeHeadHoldInput>()) return null;
            return limb;
        }

        // QA only: stands in for the pointer, so the scripted check runs the same resolution paths.
        internal static bool QaPointerActive;
        internal static PhysicalBehaviour QaPointer;
        internal static PersonBehaviour QaOpenPerson { get { return instance ? instance.person : null; } }
        internal static string QaCurrentId { get { return instance && instance.person && instance.techniques.Count > 0 ? instance.techniques[instance.index].Id : null; } }
        internal static string QaFailure { get { return instance ? instance.failure : null; } }
        internal static float QaIdle { get { return instance ? Time.unscaledTime - instance.lastInput : -1f; } }
        internal static void QaPressNext(bool back) { Ensure(); instance.Next(back); }
        internal static bool QaPressUse()
        {
            if (!instance || !instance.person || !instance.UseReachesPerson()) return false;
            instance.Confirm();
            return true;
        }

        static PhysicalBehaviour Dragged()
        {
            if (QaPointerActive) return null;
            var tool = Global.main && Global.main.ToolControllerBehaviour ? Global.main.ToolControllerBehaviour.CurrentTool : null;
            return tool != null ? tool.ActiveSingleSelected : null;
        }

        static PhysicalBehaviour UnderMouse()
        {
            if (QaPointerActive) return QaPointer;
            var selection = SelectionController.Main;
            return selection != null ? selection.CurrentlyUnderMouse : null;
        }

        static LimbBehaviour ResolveTorso(out bool pointerOnSomething)
        {
            var under = UnderMouse();
            pointerOnSomething = under != null;
            var limb = TorsoOf(under);
            if (limb) return limb;
            limb = TorsoOf(Dragged());
            if (limb || under) return limb;
            var selection = SelectionController.Main;
            LimbBehaviour unique = null;
            if (selection != null && selection.SelectedObjects != null)
                foreach (var physical in selection.SelectedObjects)
                {
                    var candidate = TorsoOf(physical);
                    if (!candidate) continue;
                    if (unique && candidate.Person != unique.Person) return null;
                    unique = candidate;
                }
            return unique;
        }

        // Use goes to what is dragged, else to what is under the pointer; empty space reaches no one.
        bool UseReachesPerson()
        {
            var dragged = Dragged();
            var target = dragged ? dragged : UnderMouse();
            if (!target) return true;
            var limb = target.GetComponent<LimbBehaviour>();
            return limb && limb.Person == person;
        }

        void Next(bool back)
        {
            bool onSomething;
            var torso = ResolveTorso(out onSomething);
            if (person)
            {
                if (torso && torso.Person != person) { if (Open(torso.Person)) Consume(); return; }
                // R over the head or a hand keeps its own meaning (ki charge, ki output).
                if (!torso && onSomething) return;
                Step(back ? -1 : 1);
                Consume();
                return;
            }
            if (torso && Open(torso.Person)) Consume();
        }

        static void Consume()
        {
            consumedFrame = Time.frameCount;
            InputAction action;
            if (InputSystem.Actions.TryGetValue(ActionId, out action)) { consumedKey = action.Key; consumedSecondary = action.SecondaryKey; }
            else { consumedKey = DefaultKey; consumedSecondary = KeyCode.None; }
        }

        bool Open(PersonBehaviour p)
        {
            var list = Collect(p);
            if (list.Count == 0) return false;
            if (person && person != p) TechniqueCaptions.Close(person, TechniqueCaptions.PickerSlot, false);
            person = p; techniques = list; failure = null; index = 0;
            string last;
            if (remembered.TryGetValue(p.GetInstanceID(), out last))
            {
                int i = list.FindIndex(t => t.Id == last);
                if (i >= 0) index = i;
            }
            lastInput = Time.unscaledTime; nextRefresh = Time.unscaledTime + .15f;
            Show();
            DbUi.Blip();
            return true;
        }

        void Step(int direction)
        {
            lastInput = Time.unscaledTime; failure = null;
            if (techniques.Count > 1) index = (index + direction + techniques.Count) % techniques.Count;
            remembered[person.GetInstanceID()] = techniques[index].Id;
            Show();
            TechniqueCaptions.Pulse(person, TechniqueCaptions.PickerSlot, CaptionPulse.Cycle);
            DbUi.Blip();
        }

        void Confirm()
        {
            var p = person;
            var technique = techniques[Mathf.Clamp(index, 0, techniques.Count - 1)];
            remembered[p.GetInstanceID()] = technique.Id;
            blockFrame = Time.frameCount; blockRoot = p.transform.root;
            BeginHold(p);
            string result;
            try { result = technique.Execute(); }
            catch (Exception e) { Debug.LogWarning("[DB Singularity] Torso technique " + technique.Id + ": " + e); result = "ERROR"; }
            if (result == null)
            {
                if (technique.Hold) heldWrath = technique.Hold;
                TechniqueCaptions.Close(p, TechniqueCaptions.PickerSlot, true);
                DbUi.Blip();
                person = null; techniques = new List<TorsoTechnique>(); failure = null;
                return;
            }
            failure = result; failureUntil = Time.unscaledTime + 1.2f; lastInput = Time.unscaledTime;
            Show();
            TechniqueCaptions.Pulse(p, TechniqueCaptions.PickerSlot, CaptionPulse.Fail);
            DbUi.Blip();
        }

        void Close(bool confirmed)
        {
            if (person) TechniqueCaptions.Close(person, TechniqueCaptions.PickerSlot, confirmed);
            person = null; techniques = new List<TorsoTechnique>(); failure = null;
        }

        static void BeginHold(PersonBehaviour p)
        {
            heldPerson = p; heldWrath = null;
            InputAction action;
            if (InputSystem.Actions.TryGetValue(UseActionId, out action)) { useKey = action.Key; useSecondary = action.SecondaryKey; }
            else { useKey = KeyCode.F; useSecondary = KeyCode.None; }
        }

        static void TickHold()
        {
            if (!heldPerson && !heldWrath) return;
            bool held = KiCombatMath.HasFocus && Input.GetKey(useKey) && (useSecondary == KeyCode.None || Input.GetKey(useSecondary));
            if (!held) { ReleaseHold(); return; }
            if (heldWrath) heldWrath.ExternalHoldUntil = Mathf.Max(heldWrath.ExternalHoldUntil, Time.time + .08f);
        }

        static void ReleaseHold() { heldPerson = null; heldWrath = null; }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + .15f;
            string current = techniques.Count > 0 ? techniques[Mathf.Clamp(index, 0, techniques.Count - 1)].Id : null;
            var list = Collect(person);
            if (list.Count == 0) { Close(false); return; }
            techniques = list;
            int i = list.FindIndex(t => t.Id == current);
            index = i >= 0 ? i : Mathf.Clamp(index, 0, list.Count - 1);
            Show();
        }

        void Show()
        {
            if (!person || techniques.Count == 0) return;
            var t = techniques[Mathf.Clamp(index, 0, techniques.Count - 1)];
            bool failing = failure != null && Time.unscaledTime < failureUntil;
            string tag = failing ? failure : (t.Tag != null ? t.Tag() : null);
            bool lit = t.TagLit != null && t.TagLit();
            TechniqueCaptions.Show(person, TechniqueCaptions.PickerSlot, new TechniqueCaptionContent
            {
                Title = t.Name,
                Icon = t.Icon,
                IconIsGlyph = t.IconIsGlyph,
                Accent = t.Accent,
                Tag = tag,
                TagColor = failing ? DbTheme.Danger : lit ? DbTheme.Lift(t.Accent, .25f) : DbTheme.TextSoft,
                Count = techniques.Count,
                Index = Mathf.Clamp(index, 0, techniques.Count - 1),
                NextKey = DbpuInputRegistration.GetActionKeyLabel(ActionId, DefaultKey),
                UseKey = DbpuInputRegistration.GetActionKeyLabel(UseActionId, KeyCode.F)
            }, 0f);
        }

        // The torso techniques this fighter has, safest first and the self-destruct last.
        internal static List<TorsoTechnique> Collect(PersonBehaviour p)
        {
            var list = new List<TorsoTechnique>();
            if (!p) return list;

            var shield = p.GetComponent<KiShield>();
            if (shield && shield.enabled)
                list.Add(new TorsoTechnique
                {
                    Id = "KiShield", Name = "Ki Shield", Icon = Art(DBPU.Icon_KiShield, null), Accent = AuraAccent(p),
                    Tag = () => shield.Using ? "Raised" : null, TagLit = () => true,
                    Execute = () => ToggleShield(p, shield)
                });

            var flight = FlightOf(p);
            if (flight && flight.enabled)
                list.Add(new TorsoTechnique
                {
                    Id = "Flight", Name = "Flight", Icon = Art(DBPU.Icon_Flight, null), Accent = DbTheme.Hex(0x8FE3FF),
                    Tag = () => flight.IsFlying ? "Flying" : null, TagLit = () => true,
                    Execute = () => { flight.Toggle(); return null; }
                });

            var kaioken = p.GetComponent<Kaioken>();
            if (kaioken && kaioken.enabled)
                list.Add(new TorsoTechnique
                {
                    Id = "Kaioken", Name = "Kaioken", Icon = Art(DBPU.Icon_Kaioken, DBPU.Icon_FinalFlash), Accent = KaiokenRed,
                    Tag = () => "×" + KaiokenHeadTap.Label(kaioken.Multiplier), TagLit = () => kaioken.Active,
                    Execute = () => ToggleKaioken(p, kaioken)
                });

            var wrath = p.GetComponent<GodsWrathBehaviour>();
            if (wrath && wrath.enabled)
                list.Add(new TorsoTechnique
                {
                    Id = "GodsWrath", Name = "G.O.D's Wrath", Icon = Art(DBPU.Icon_GodsWrath, DBPU.Icon_KiBlastBarrage), Accent = DbTheme.Violet,
                    Tag = () => "Hold " + DbpuInputRegistration.GetActionKeyLabel(UseActionId, KeyCode.F), TagLit = () => false,
                    Execute = () => wrath.HoldFromPicker(.3f), Hold = wrath
                });

            var explosion = p.GetComponent<FinalExplosion>();
            if (explosion && explosion.enabled)
                list.Add(new TorsoTechnique
                {
                    Id = "FinalExplosion", Name = "Final Explosion", Icon = Art(DBPU.Icon_FinalExplosion, null), Accent = DbTheme.Gold,
                    Tag = () => explosion.EnoughKi ? null : "Low ki", TagLit = () => false,
                    Execute = explosion.TryDetonate
                });

            // Powers without their own art get a kit glyph in their accent, as in the powers menu.
            foreach (var t in list)
                if (!t.Icon)
                {
                    t.Icon = t.Id == "Kaioken" ? DbAtlas.Bolt : t.Id == "KiShield" ? DbAtlas.Ring : t.Id == "Flight" ? DbAtlas.Chevron : DbAtlas.Star;
                    t.IconIsGlyph = true;
                }
            return list;
        }

        // The power's menu art without its baked frame; null when it has none of its own (a missing
        // file falls back to another power's art at load).
        static Sprite Art(Sprite source, Sprite substitute)
        {
            if (!source || source == DBPU.PIconTemplate || (substitute && source == substitute)) return null;
            DbAtlas.Ensure();
            return DbArt.Icon(source);
        }

        static Flight FlightOf(PersonBehaviour p)
        {
            var lower = p.transform.Find("Body/LowerBody");
            var flight = lower ? lower.GetComponent<Flight>() : null;
            return flight ? flight : p.GetComponentInChildren<Flight>();
        }

        // The shield takes the fighter's aura colour; a very dark aura falls back to ki cyan.
        static Color AuraAccent(PersonBehaviour p)
        {
            try
            {
                Color c = Color.Lerp(DbpuAuraPalette.Get(p).Vivid, Color.white, .25f);
                if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < .45f) return KiCyan;
                c.a = 1f;
                return c;
            }
            catch { return KiCyan; }
        }

        static string ToggleShield(PersonBehaviour p, KiShield shield)
        {
            if (shield.Using) { shield.Lower(false); return null; }
            if (shield.TryRaise()) return null;
            var middle = p.transform.Find("Body/MiddleBody");
            var torso = middle ? middle.GetComponent<LimbBehaviour>() : null;
            if (torso && torso.Health <= 0f) return "HURT";
            if (shield.Cooldown > 0f || shield.Integrity01 < .2f) return "RECOVERING";
            var ki = shield.Energy;
            if (ki && DBPU.KiAttackCostMultiplier > 0f && ki.currentKi < KiCombatMath.MaxKi(ki) * .12f) return "LOW KI";
            return "BLOCKED";
        }

        static string ToggleKaioken(PersonBehaviour p, Kaioken kaioken)
        {
            var head = p.Limbs != null && p.Limbs.Length > 0 ? p.Limbs[0] : null;
            var menu = head ? head.GetComponent<TransformationContextMenu>() : null;
            if (menu && !kaioken.CompatibleForms.Contains(menu.Form)) return "WRONG FORM";
            bool was = kaioken.Active;
            kaioken.ToggleFromPowersMenu();
            return kaioken.Active != was ? null : "BLOCKED";
        }
    }
}
