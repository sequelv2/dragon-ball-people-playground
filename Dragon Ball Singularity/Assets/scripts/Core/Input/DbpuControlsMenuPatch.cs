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
    static class DbpuControlsMenuPatch
    {
        const string SectionTitle = "Dragon Ball: Singularity";
        static Harmony _harmony;
        static bool _applied;

        public static void Apply()
        {
            if (_applied) return;
            _harmony = new Harmony("DBPU.Controls");
            _harmony.Patch(
                AccessTools.Method(typeof(ControlSchemeEditorBehaviour), nameof(ControlSchemeEditorBehaviour.InitialiseControlScheme)),
                prefix: new HarmonyMethod(typeof(DbpuControlsMenuPatch), nameof(Prefix)));
            _applied = true;
        }

        static void CreateSectionHeader(GameObject headerPrefab, Transform container, string title, Sprite icon = null)
        {
            var headerGo = UnityEngine.Object.Instantiate(headerPrefab, container);
            var text = headerGo.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null)
                return;

            text.text = title;
            if (icon == null)
                return;

            var headerRt = headerGo.GetComponent<RectTransform>();
            if (headerRt == null)
                headerRt = headerGo.AddComponent<RectTransform>();

            var iconGo = new GameObject("SectionIcon");
            iconGo.transform.SetParent(headerGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);

            float iconSize = headerRt.rect.height > 1f ? headerRt.rect.height * 0.82f : 16f;
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            iconGo.AddComponent<LayoutElement>().ignoreLayout = true;

            iconRt.localScale = new Vector3(0.9f, 0.9f, 1f);
            iconRt.localPosition = new Vector3(-514f, 0f, 0f);
        }

        public static bool Prefix(ControlSchemeEditorBehaviour __instance)
        {
            var container = AccessTools.Field(typeof(ControlSchemeEditorBehaviour), "Container").GetValue(__instance) as Transform;
            var headerPrefab = AccessTools.Field(typeof(ControlSchemeEditorBehaviour), "HeaderPrefab").GetValue(__instance) as GameObject;
            var controlPrefab = AccessTools.Field(typeof(ControlSchemeEditorBehaviour), "ControlPrefab").GetValue(__instance) as GameObject;

            foreach (Transform child in container)
                UnityEngine.Object.Destroy(child.gameObject);

            foreach (ActionCategory category in Enum.GetValues(typeof(ActionCategory)))
            {
                if (category == ActionCategory.MapEditor)
                    continue;

                if (category == ActionCategory.Modded)
                {
                    var dbpuActions = InputSystem.Actions
                        .Where(action => action.Value.Category == ActionCategory.Modded
                            && !action.Value.InvisibleInMenu
                            && action.Key.StartsWith("dbpu.", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(action => action.Value.Name)
                        .ToList();

                    var otherModded = InputSystem.Actions
                        .Where(action => action.Value.Category == ActionCategory.Modded
                            && !action.Value.InvisibleInMenu
                            && !action.Key.StartsWith("dbpu.", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (dbpuActions.Count > 0)
                    {
                        CreateSectionHeader(headerPrefab, container, SectionTitle, DBPU.ControlsSectionIcon);
                        foreach (var action in dbpuActions)
                            CreateControlObject(controlPrefab, container, action.Key, action.Value);
                    }

                    if (otherModded.Count > 0)
                    {
                        UnityEngine.Object.Instantiate(headerPrefab, container).GetComponentInChildren<TextMeshProUGUI>().text = "Modded";
                        foreach (var action in otherModded)
                            CreateControlObject(controlPrefab, container, action.Key, action.Value);
                    }

                    continue;
                }

                var categoryActions = InputSystem.Actions
                    .Where(action => action.Value.Category == category && !action.Value.InvisibleInMenu)
                    .ToList();

                if (categoryActions.Count == 0)
                    continue;

                UnityEngine.Object.Instantiate(headerPrefab, container).GetComponentInChildren<TextMeshProUGUI>().text =
                    Regex.Replace(category.ToString(), "(\\B[A-Z])", " $1");

                foreach (var action in categoryActions)
                    CreateControlObject(controlPrefab, container, action.Key, action.Value);
            }

            return false;
        }

        static void CreateControlObject(GameObject controlPrefab, Transform container, string key, InputAction action)
        {
            var control = UnityEngine.Object.Instantiate(controlPrefab, container).GetComponent<ActionControlBehaviour>();
            control.Action.text = action.Name;
            var trigger = control.Trigger.GetComponent<TriggerEditorBehaviour>();
            trigger.InputActionKey = key;
            trigger.InputAction = action;
        }
    }
}
