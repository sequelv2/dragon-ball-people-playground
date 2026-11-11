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
    static class DbpuInputRegistration
    {
        public static void RegisterAll()
        {
            Register("Open powers menu", DBPU.OpenPowerMenuActionId, DBPU.OpenPowerMenuDefaultKey);
            Register("Quick transform", DBPU.QuickTransformActionId, KeyCode.H);
            ModAPI.RegisterInput("Combat transformation wheel (hold)", CombatTransformationWheel.ActionId, CombatTransformationWheel.DefaultKey);
            ModAPI.RegisterInput("Combat hand ability wheel (hold)", CombatAbilityWheel.ActionId, CombatAbilityWheel.DefaultKey);
            ModAPI.RegisterInput("Ki output on head (Shift reverses)", CombatPowerController.ActionId, CombatPowerController.DefaultKey);
            ModAPI.RegisterInput("Ki Fist mode (tap) / charge (hold)", KiFistInput.ActionId, KiFistInput.DefaultKey);
            ModAPI.RegisterInput("Ki Fist on/off (tap) / overcharge on upper arm (hold)", KiFistOverchargeInput.ActionId, KiFistOverchargeInput.DefaultKey);
            ModAPI.RegisterInput("Torso techniques: show / next on torso (Use confirms)", TorsoTechniquePicker.ActionId, TorsoTechniquePicker.DefaultKey);
            Register("Barrage", DBPU.BarrageActionId, KeyCode.H);
            Register("Kaioken", DBPU.KaiokenActionId, KeyCode.K);
            Register("Instant transmission", DBPU.InstantTransmissionActionId, KeyCode.R);
            Register("Cell Max scream", DBPU.CellMaxScreamActionId, KeyCode.R);
            Register("Ki charge hold", DBPU.KiChargeHoldActionId, KeyCode.R);
            Register("Ki output cycle", DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey);
        }

        static void Register(string label, string id, KeyCode defaultKey)
        {
            ModAPI.RegisterInput(label, id, defaultKey);
            FixLegacyDefaultKey(id, defaultKey);
        }

        public static KeyCode GetActionKey(string id, KeyCode fallback)
        {
            if (InputSystem.Actions.TryGetValue(id, out var active))
                return active.Key;
            return fallback;
        }

        public static string GetActionKeyLabel(string id, KeyCode fallback)
        {
            KeyCode key = GetActionKey(id, fallback);
            return key == KeyCode.None ? "Unbound" : key.ToString();
        }

        public static void SetActionKey(string id, KeyCode key)
        {
            var moddedField = AccessTools.Field(typeof(InputSystem), "Modded");
            var modded = moddedField?.GetValue(null) as Dictionary<string, InputAction>;
            if (modded != null && modded.TryGetValue(id, out var registered))
            {
                registered.Key = key;
                modded[id] = registered;
            }

            if (InputSystem.Actions.TryGetValue(id, out var active))
            {
                active.Key = key;
                InputSystem.Actions[id] = active;
            }

            AccessTools.Field(typeof(ControlSchemeEditorBehaviour), "ShouldReinitialise")?.SetValue(null, true);
        }

        public static void ApplyLegacyConfigKeybinds()
        {
            if (ModConfigs.configurations == null) return;

            if (ModConfigs.configurations.DBPUConfigVersion < 4)
            {
                if (ModConfigs.configurations.KiOverdriveOutputKey != KeyCode.None)
                    SetActionKey(DBPU.KiOverdriveOutputActionId, ModConfigs.configurations.KiOverdriveOutputKey);
                if (ModConfigs.configurations.KiChargeHoldKey != KeyCode.None)
                    SetActionKey(DBPU.KiChargeHoldActionId, ModConfigs.configurations.KiChargeHoldKey);

                ModConfigs.configurations.DBPUConfigVersion = 4;
                ModConfigs.configurations.KiOverdriveOutputKey = GetActionKey(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey);
                ModConfigs.configurations.KiChargeHoldKey = GetActionKey(DBPU.KiChargeHoldActionId, KeyCode.R);
                ModConfigs.Save();
            }
        }

        static void FixLegacyDefaultKey(string id, KeyCode defaultKey)
        {
            var moddedField = AccessTools.Field(typeof(InputSystem), "Modded");
            var modded = moddedField?.GetValue(null) as Dictionary<string, InputAction>;
            if (modded != null && modded.TryGetValue(id, out var registered) && registered.Key == KeyCode.L)
            {
                registered.Key = defaultKey;
                modded[id] = registered;
            }

            if (InputSystem.Actions.TryGetValue(id, out var active) && active.Key == KeyCode.L)
            {
                active.Key = defaultKey;
                InputSystem.Actions[id] = active;
            }

            AccessTools.Field(typeof(ControlSchemeEditorBehaviour), "ShouldReinitialise")?.SetValue(null, true);
        }
    }
}
