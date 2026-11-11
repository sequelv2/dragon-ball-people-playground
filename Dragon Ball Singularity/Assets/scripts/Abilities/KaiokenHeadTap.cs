using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DBPU
{
    // While Kaioken burns, a quick tap of the ki charge key (R) on the head steps the multiplier up by
    // one, x2 to the configured maximum (x20 by default) and round to x2 again; Shift steps down.
    // Holding the key only charges ki, as before: the multiplier changes on a release within the
    // Ki Fist tap window.
    [DefaultExecutionOrder(-160)]
    public sealed class KaiokenHeadTap : MonoBehaviour
    {
        static KaiokenHeadTap instance;
        Kaioken target;
        PersonBehaviour person;
        bool tracking, reverse;
        float pressedAt;
        KeyCode key = KeyCode.R, secondary = KeyCode.None;

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Kaioken head tap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KaiokenHeadTap>();
        }

        void OnDestroy() { if (instance == this) instance = null; }

        void Update()
        {
            if (tracking) { Track(); return; }
            if (!KiCombatMath.CanAct()) return;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            if (!DbpuInputUtil.Down(DBPU.KiChargeHoldActionId, KeyCode.R)) return;
            var limb = ChargeLimb();
            if (!limb) return;
            var kaioken = limb.Person.GetComponent<Kaioken>();
            if (!kaioken || !kaioken.Active) return;
            // R teleports instead while Instant Transmission waits for its key.
            var it = limb.Person.GetComponent<InstantTransmission>();
            if (it && it.Active && it.CanUse) return;
            target = kaioken; person = limb.Person; tracking = true; pressedAt = Time.unscaledTime;
            reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            InputAction action;
            if (InputSystem.Actions.TryGetValue(DBPU.KiChargeHoldActionId, out action)) { key = action.Key; secondary = action.SecondaryKey; }
            else { key = KeyCode.R; secondary = KeyCode.None; }
        }

        void Track()
        {
            if (!target || !person || !person.IsAlive() || !target.Active || !KiCombatMath.HasFocus) { tracking = false; return; }
            bool held = Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary));
            float elapsed = Time.unscaledTime - pressedAt;
            if (held)
            {
                // Past the tap window it is a ki charge, and the mode stays as it is.
                if (elapsed > KiFistInput.HoldThreshold) tracking = false;
                return;
            }
            tracking = false;
            if (elapsed <= KiFistInput.HoldThreshold && KiCombatMath.CanAct()) Step(person, target, reverse);
        }

        // The limb where R charges ki (the head; Cell Max's upper body), as KiChargeHeadHoldInput reads it.
        static LimbBehaviour ChargeLimb()
        {
            var selection = SelectionController.Main;
            var limb = FromPhysical(selection != null ? selection.CurrentlyUnderMouse : null);
            if (limb) return limb;
            var tool = Global.main && Global.main.ToolControllerBehaviour ? Global.main.ToolControllerBehaviour.CurrentTool : null;
            return FromPhysical(tool != null ? tool.ActiveSingleSelected : null);
        }

        static LimbBehaviour FromPhysical(PhysicalBehaviour physical)
        {
            var limb = physical ? physical.GetComponent<LimbBehaviour>() : null;
            return limb && limb.Person && limb.Person.IsAlive() && limb.GetComponent<KiChargeHeadHoldInput>() ? limb : null;
        }

        internal static void Step(PersonBehaviour person, Kaioken kaioken, bool reverse)
        {
            int max = Mathf.FloorToInt(Mathf.Max(1f, KiCombatMath.Finite(DBPU.KaiokenMaxMultiplier, 20f)) + .001f);
            if (max < 2) return;
            float current = KiCombatMath.Finite(kaioken.Multiplier, 2f);
            int next = reverse ? Mathf.CeilToInt(current - .001f) - 1 : Mathf.FloorToInt(current + .001f) + 1;
            if (next > max) next = 2;
            else if (next < 2) next = max;
            kaioken.ApplyConfiguredMultiplier(next);
            // An open torso picker already showing Kaioken takes the new multiplier itself.
            if (TorsoTechniquePicker.RefreshIfShowing(person, "Kaioken"))
            {
                TechniqueCaptions.Pulse(person, TechniqueCaptions.PickerSlot, CaptionPulse.Cycle);
                TechniqueCaptions.Close(person, TechniqueCaptions.ToastSlot, false);
                return;
            }
            var look = TorsoTechniquePicker.Collect(person).Find(t => t.Id == "Kaioken");
            TechniqueCaptions.Show(person, TechniqueCaptions.ToastSlot, new TechniqueCaptionContent
            {
                Title = "Kaioken",
                Icon = look != null ? look.Icon : DbAtlas.Bolt,
                IconIsGlyph = look == null || look.IconIsGlyph,
                Accent = TorsoTechniquePicker.KaiokenRed,
                Tag = "×" + Label(kaioken.Multiplier)
            }, 1.4f);
            TechniqueCaptions.Pulse(person, TechniqueCaptions.ToastSlot, CaptionPulse.Cycle);
        }

        internal static string Label(float multiplier)
        {
            multiplier = KiCombatMath.Finite(multiplier, 2f);
            return Mathf.Abs(multiplier - Mathf.Round(multiplier)) < .01f
                ? Mathf.RoundToInt(multiplier).ToString(CultureInfo.InvariantCulture)
                : multiplier.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
