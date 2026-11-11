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
    public static class CharacterPowerCatalog
    {
        public const string Path = "DBPU_CharacterPowers.json";

        static readonly Dictionary<string, CharacterPowerProfile> Profiles = new Dictionary<string, CharacterPowerProfile>();
        static readonly Dictionary<string, float> BasePowerOverrides = new Dictionary<string, float>();
        static readonly Dictionary<string, Dictionary<string, float>> RuntimeFormMultipliers = new Dictionary<string, Dictionary<string, float>>();
        static CharacterPowerSaveData saveData = new CharacterPowerSaveData();

        public static void Load()
        {
            Profiles.Clear();
            BasePowerOverrides.Clear();
            saveData = new CharacterPowerSaveData();

            try
            {
                saveData = ModAPI.DeserialiseJSON<CharacterPowerSaveData>(Path) ?? new CharacterPowerSaveData();
            }
            catch
            {
                saveData = new CharacterPowerSaveData();
            }

            if (saveData.DisplayNames == null) saveData.DisplayNames = new Dictionary<string, string>();
            if (saveData.Defaults == null) saveData.Defaults = new Dictionary<string, Dictionary<string, float>>();
            if (saveData.FormNames == null) saveData.FormNames = new Dictionary<string, Dictionary<string, string>>();
            if (saveData.BasePowerOverrides == null) saveData.BasePowerOverrides = new Dictionary<string, float>();

            MigrateLegacyOverrides();

            foreach (var kvp in saveData.Defaults)
            {
                if (!Profiles.ContainsKey(kvp.Key))
                {
                    Profiles[kvp.Key] = new CharacterPowerProfile
                    {
                        CharacterId = kvp.Key,
                        DisplayName = saveData.DisplayNames.ContainsKey(kvp.Key) ? saveData.DisplayNames[kvp.Key] : kvp.Key
                    };
                }

                var profile = Profiles[kvp.Key];
                profile.Forms.Clear();

                Dictionary<string, string> names = saveData.FormNames.ContainsKey(kvp.Key)
                    ? saveData.FormNames[kvp.Key]
                    : new Dictionary<string, string>();

                foreach (var form in kvp.Value)
                {
                    profile.Forms.Add(new CharacterPowerFormInfo
                    {
                        FormKey = form.Key,
                        FormId = form.Key,
                        FormName = names.ContainsKey(form.Key) ? names[form.Key] : form.Key,
                        DefaultPower = form.Value
                    });
                }
            }

            foreach (var kvp in saveData.BasePowerOverrides)
                BasePowerOverrides[kvp.Key] = kvp.Value;
        }

        static void MigrateLegacyOverrides()
        {
            if (saveData.Overrides == null || saveData.Overrides.Count == 0) return;

            foreach (var kvp in saveData.Overrides)
            {
                if (saveData.BasePowerOverrides.ContainsKey(kvp.Key)) continue;
                if (kvp.Value != null && kvp.Value.TryGetValue("Base", out float baseOverride))
                    saveData.BasePowerOverrides[kvp.Key] = baseOverride;
            }

            saveData.Overrides = new Dictionary<string, Dictionary<string, float>>();
        }

        // Set only by disposable QA runs, which must leave the player's saved powers alone.
        public static bool SavingDisabled;

        public static void Save()
        {
            if (SavingDisabled) return;
            saveData.BasePowerOverrides = BasePowerOverrides.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            saveData.Overrides = new Dictionary<string, Dictionary<string, float>>();

            ModAPI.SerialiseJSON(saveData, Path);
        }

        public static void RegisterForm(string characterId, string displayName, string formKey, CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (string.IsNullOrEmpty(characterId) || stats == null || string.IsNullOrEmpty(stats.ID)) return;

            if (!Profiles.ContainsKey(characterId))
            {
                Profiles[characterId] = new CharacterPowerProfile
                {
                    CharacterId = characterId,
                    DisplayName = displayName
                };
            }

            var profile = Profiles[characterId];
            if (!string.IsNullOrEmpty(displayName)) profile.DisplayName = displayName;

            CharacterPowerFormInfo existing = null;
            for (int i = 0; i < profile.Forms.Count; i++)
            {
                if (profile.Forms[i].FormId == stats.ID)
                {
                    existing = profile.Forms[i];
                    break;
                }
            }

            if (existing == null)
            {
                existing = new CharacterPowerFormInfo();
                profile.Forms.Add(existing);
            }

            existing.FormKey = formKey;
            existing.FormId = stats.ID;
            existing.FormName = string.IsNullOrEmpty(stats.Name) ? formKey : stats.Name;
            existing.DefaultPower = stats.PowerMultiplier;

            if (!saveData.DisplayNames.ContainsKey(characterId))
                saveData.DisplayNames[characterId] = profile.DisplayName;
            else if (!string.IsNullOrEmpty(displayName))
                saveData.DisplayNames[characterId] = displayName;

            if (!saveData.Defaults.ContainsKey(characterId))
                saveData.Defaults[characterId] = new Dictionary<string, float>();
            saveData.Defaults[characterId][stats.ID] = stats.PowerMultiplier;

            if (!saveData.FormNames.ContainsKey(characterId))
                saveData.FormNames[characterId] = new Dictionary<string, string>();
            saveData.FormNames[characterId][stats.ID] = existing.FormName;

            Save();
        }

        public static List<CharacterPowerProfile> GetProfilesSorted()
        {
            return Profiles.Values.OrderBy(p => p.DisplayName).ToList();
        }

        public static CharacterPowerProfile GetProfile(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            Profiles.TryGetValue(characterId, out var profile);
            return profile;
        }

        public static float GetDefaultPower(string characterId, string formId, float fallback)
        {
            if (!string.IsNullOrEmpty(characterId)
                && saveData.Defaults.ContainsKey(characterId)
                && saveData.Defaults[characterId].ContainsKey(formId))
                return saveData.Defaults[characterId][formId];
            return fallback;
        }

        public static float GetDefaultBasePower(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId)
                && saveData.Defaults.ContainsKey(characterId)
                && saveData.Defaults[characterId].TryGetValue("Base", out float basePower)
                && basePower > 0f)
                return basePower;

            var profile = GetProfile(characterId);
            if (profile == null || profile.Forms.Count == 0) return 1f;

            float lowest = float.MaxValue;
            for (int i = 0; i < profile.Forms.Count; i++)
            {
                if (profile.Forms[i].DefaultPower < lowest)
                    lowest = profile.Forms[i].DefaultPower;
            }

            return lowest > 0f ? lowest : 1f;
        }

        public static float GetEffectiveBasePower(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && BasePowerOverrides.TryGetValue(characterId, out float value))
                return value;
            return GetDefaultBasePower(characterId);
        }

        public static float GetScaleFactor(string characterId)
        {
            float defaultBase = GetDefaultBasePower(characterId);
            if (defaultBase <= 0f) return 1f;
            return GetEffectiveBasePower(characterId) / defaultBase;
        }

        public static float GetEffectivePower(string characterId, string formId, float defaultPower)
        {
            float scale = GetScaleFactor(characterId);
            float value = defaultPower * scale;

            if (!string.IsNullOrEmpty(characterId)
                && !string.IsNullOrEmpty(formId)
                && RuntimeFormMultipliers.TryGetValue(characterId, out var forms)
                && forms != null
                && forms.TryGetValue(formId, out var mult)
                && mult > 0f
                && !Mathf.Approximately(mult, 1f))
            {
                value *= mult;
            }

            return value;
        }

        public static void SetRuntimeFormMultiplier(string characterId, string formId, float multiplier)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(formId)) return;
            multiplier = Mathf.Max(0f, multiplier);

            if (!RuntimeFormMultipliers.TryGetValue(characterId, out var forms) || forms == null)
            {
                forms = new Dictionary<string, float>();
                RuntimeFormMultipliers[characterId] = forms;
            }

            if (Mathf.Approximately(multiplier, 1f))
                forms.Remove(formId);
            else
                forms[formId] = multiplier;

            RefreshCharacterInWorld(characterId);
        }

        public static void ResetRuntimeFormMultiplier(string characterId, string formId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(formId)) return;
            if (RuntimeFormMultipliers.TryGetValue(characterId, out var forms) && forms != null)
            {
                if (forms.Remove(formId))
                    RefreshCharacterInWorld(characterId);
            }
        }

        public static bool HasBaseOverride(string characterId)
        {
            return !string.IsNullOrEmpty(characterId) && BasePowerOverrides.ContainsKey(characterId);
        }

        public static void SetBasePower(string characterId, float newBasePower)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            float defaultBase = GetDefaultBasePower(characterId);
            if (defaultBase <= 0f) defaultBase = 1f;

            newBasePower = Mathf.Max(0f, newBasePower);
            if (Mathf.Approximately(newBasePower, defaultBase))
                BasePowerOverrides.Remove(characterId);
            else
                BasePowerOverrides[characterId] = newBasePower;

            Save();
            RefreshCharacterInWorld(characterId);
        }

        public static void ResetCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            BasePowerOverrides.Remove(characterId);
            Save();
            RefreshCharacterInWorld(characterId);
        }

        public static void ResetAll()
        {
            BasePowerOverrides.Clear();
            Save();
            foreach (var profile in Profiles.Values)
                RefreshCharacterInWorld(profile.CharacterId);
        }

        public static void RefreshCharacterInWorld(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            var identities = UnityEngine.Object.FindObjectsOfType<DbpuCharacterIdentity>();
            for (int i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity.CharacterId != characterId) continue;

                var person = identity.GetComponent<PersonBehaviour>();
                if (person == null || !person.IsAlive()) continue;

                var head = person.transform.Find("Head");
                if (head == null) continue;

                var tm = head.GetComponent<TransformationContextMenu>();
                if (tm == null || tm.TransformationsData == null || string.IsNullOrEmpty(tm.Form)) continue;

                string formKey = null;
                foreach (var kvp in tm.TransformationsData.transformations.TransformationData)
                {
                    if (kvp.Value.ID == tm.Form)
                    {
                        formKey = kvp.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(formKey))
                    tm.ApplyForm(formKey);
            }
        }

        public static string FormatPower(float mult)
        {
            if (float.IsInfinity(mult)) return "INF";
            if (mult >= 100000000000f) return ((float)Math.Round(mult / 1000000000f, 0)).ToString() + "B";
            if (mult >= 1000000000f) return ((float)Math.Round(mult / 1000000000f, 1)).ToString() + "B";
            if (mult >= 100000000f) return ((float)Math.Round(mult / 1000000f, 0)).ToString() + "M";
            if (mult >= 1000000f) return ((float)Math.Round(mult / 1000000f, 1)).ToString() + "M";
            if (mult >= 100000f) return ((float)Math.Round(mult / 1000f, 0)).ToString() + "K";
            if (mult >= 1000f) return ((float)Math.Round(mult / 1000f, 1)).ToString() + "K";
            return ((float)Math.Round(mult, 0)).ToString();
        }
    }
}
