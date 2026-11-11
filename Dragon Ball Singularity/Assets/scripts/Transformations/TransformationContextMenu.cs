using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Owns a fighter's current form and applies form changes. The screen itself is TransformationMenuWindow.
    public class TransformationContextMenu : MonoBehaviour
    {
        public PersonBehaviour person;
        public CharacterTransformations TransformationsData;
        public SkinContextMenu skinContextMenu;
        public MultipliersAndStuffs Multipliers;
        public AuraHandler AuraHandler;

        public GameObject Aura;

        public int PositionSelectedFrame = 0;
        public int OriginalSelectedFrame = 0;
        public int LocalSelectedFrame = 0;
        public int FormSelected = 0;
        public int OriginalFormSelected = 0;

        public float PreviousMultiplier = 1f;
        public string Form = "Base";
        public string CurrentAuraID = "Base";
        public string BaseForm = "Base";
        public string LastUsedForm;

        public bool AuraOn = true;
        public bool AuraCanAppear = true;

        public Dictionary<string, GameObject> AurasStorage = new Dictionary<string, GameObject>();

        static TransformationMenuWindow open;
        static TransformationContextMenu openOwner;

        public static TransformationMenuWindow OpenWindow { get { return open != null && open.Window ? open : null; } }

        public void Start()
        {
            FormSelected = OriginalFormSelected;
            PositionSelectedFrame = OriginalSelectedFrame;

            EnsureRuntimeRefs();
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

        public void Awake()
        {
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
            MCP_Utils.AddContextButtonInOrder(contextButtons, new ContextMenuButton("!TrasformationsMenu", "Transformations Menu", "Transformations Menu", () => OpenTransformationsMenu()), transformInsertAt);
        }

        public void OpenTransformationsMenu()
        {
            EnsureRuntimeRefs();

            if (TransformationsData == null || TransformationsData.transformations == null
                || TransformationsData.transformations.TransformationData == null
                || TransformationsData.transformations.TransformationData.Count == 0)
            {
                ModAPI.Notify("No transformations available.");
                return;
            }

            if (OpenWindow != null)
            {
                if (openOwner == this) return;
                open.Window.CloseImmediate();
                open = null;
            }

            openOwner = this;
            open = new TransformationMenuWindow(this);
        }

        internal void HandleWindowClosed()
        {
            if (openOwner != this) return;
            open = null;
            openOwner = null;
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
            return FormatMultiplierValue(ResolveDisplayMultiplier(formId, formMult));
        }

        // The label for a value that is already resolved, e.g. while a number counts up.
        public string FormatMultiplierValue(float formMult)
        {
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

        float ResolveDisplayMultiplier(string formId, float fallback)
        {
            if (TransformationsData == null || TransformationsData.transformations == null
                || TransformationsData.transformations.TransformationData == null
                || string.IsNullOrEmpty(formId))
                return fallback;

            foreach (var kvp in TransformationsData.transformations.TransformationData)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value.ID == formId)
                    return TransformationsData.GetDisplayPower(kvp.Value);
            }

            return fallback;
        }

        public float GetStatsEffectivePower(CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (TransformationsData == null || stats == null)
                return stats != null ? stats.PowerMultiplier : 1f;
            return TransformationsData.GetEffectivePower(stats);
        }

        public float GetStatsDisplayPower(CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (TransformationsData == null || stats == null)
                return stats != null ? stats.PowerMultiplier : 1f;
            return TransformationsData.GetDisplayPower(stats);
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
                    LocalSelectedFrame = i % 6;
                    PositionSelectedFrame = LocalSelectedFrame;
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
                ApplyFormWithTitle(target);
            }
            else
            {
                ApplyFormWithTitle(BaseForm);
            }
        }

        // For form changes the player asked for; internal resets stay silent and call ApplyForm.
        public void ApplyFormWithTitle(string formKey)
        {
            ApplyForm(formKey);
            CombatTransformationWheel.Instance.ShowFormTitle(this, formKey);
        }
    }
}
