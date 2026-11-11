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
    public class CharacterPowerPickerInput : MonoBehaviour
    {
        static CharacterPowerPickerInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("CharacterPowerPickerInput");
            _instance = go.AddComponent<CharacterPowerPickerInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (!CharacterPowerPickerState.Armed) return;
            if (DialogBox.IsAnyDialogboxOpen || Global.ActiveUiBlock) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;

            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            var identity = person.GetComponent<DbpuCharacterIdentity>();
            if (identity == null || string.IsNullOrEmpty(identity.CharacterId)) return;

            if (person.GetComponent<CharacterTransformations>() == null) return;

            CharacterPowerPickerState.Disarm();

            string characterId = identity.CharacterId;
            string displayName = string.IsNullOrEmpty(identity.DisplayName) ? characterId : identity.DisplayName;

            var ct = person.GetComponent<CharacterTransformations>();
            var head = person.transform.Find("Head");
            var tm = head != null ? head.GetComponent<TransformationContextMenu>() : null;

            string formId = tm != null && !string.IsNullOrEmpty(tm.Form) ? tm.Form : "Base";
            string formName = formId;
            float defaultFormPower = 1f;
            bool formFound = false;

            void TryResolveForm(string candidate)
            {
                if (formFound || string.IsNullOrEmpty(candidate) || ct == null || ct.transformations == null) return;
                foreach (var kvp in ct.transformations.TransformationData)
                {
                    if (kvp.Value == null) continue;
                    if (kvp.Value.ID == candidate || kvp.Key == candidate
                        || (!string.IsNullOrEmpty(kvp.Value.Name) && string.Equals(kvp.Value.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                    {
                        defaultFormPower = kvp.Value.PowerMultiplier > 0f ? kvp.Value.PowerMultiplier : 1f;
                        formName = !string.IsNullOrEmpty(kvp.Value.Name) ? kvp.Value.Name : kvp.Key;
                        formId = !string.IsNullOrEmpty(kvp.Value.ID) ? kvp.Value.ID : kvp.Key;
                        formFound = true;
                        return;
                    }
                }
            }

            TryResolveForm(formId);
            if (!formFound && tm != null) TryResolveForm(tm.BaseForm);
            if (!formFound) TryResolveForm("Base");
            if (!formFound && ct != null && ct.transformations != null)
            {
                var ordered = ct.transformations.GetOrderedEntries();
                if (ordered != null && ordered.Count > 0 && ordered[0].Value != null)
                {
                    var stats = ordered[0].Value;
                    defaultFormPower = stats.PowerMultiplier > 0f ? stats.PowerMultiplier : 1f;
                    formName = !string.IsNullOrEmpty(stats.Name) ? stats.Name : ordered[0].Key;
                    formId = !string.IsNullOrEmpty(stats.ID) ? stats.ID : ordered[0].Key;
                    formFound = true;
                }
            }

            float catalogPower = CharacterPowerCatalog.GetDisplayPower(characterId, formId, defaultFormPower);
            float currentFormPower = catalogPower;
            var liveMult = person.GetComponent<MultipliersAndStuffs>();
            if (liveMult == null) liveMult = person.transform.root.GetComponent<MultipliersAndStuffs>();
            if (liveMult != null && liveMult.FormMultiplier > 0f)
                currentFormPower = Mathf.Max(catalogPower, liveMult.FormMultiplier / CharacterPowerCatalog.GetInnatePower(characterId));

            float defaultBase = CharacterPowerCatalog.GetDefaultBasePower(characterId);
            if (defaultBase <= 0f) defaultBase = 1f;

            CharacterPowerInputUI.Open(
                "Set " + displayName + " Power",
                "Current: " + CharacterPowerCatalog.FormatPower(currentFormPower),
                "Default " + formName + ": " + CharacterPowerCatalog.FormatPower(defaultFormPower),
                currentFormPower,
                value =>
                {
                    float newBase = defaultFormPower > 0f
                        ? defaultBase * (value / defaultFormPower)
                        : value;
                    CharacterPowerCatalog.SetBasePower(characterId, newBase);
                });
        }
    }
}
