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
    public class QuickTransformKeybindInput : MonoBehaviour
    {
        static QuickTransformKeybindInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("QuickTransformKeybindInput");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<QuickTransformKeybindInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        static TransformationContextMenu FindTransformMenuFromPhysical(PhysicalBehaviour pb)
        {
            if (pb == null) return null;

            var person = pb.GetComponent<PersonBehaviour>() ?? pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return null;

            Transform head = person.transform.Find("Head");
            if (head == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb != null && limb.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        head = limb.transform;
                        break;
                    }
                }
            }

            if (head == null && person.Limbs.Length > 2)
                head = person.Limbs[2].transform;

            return head != null ? head.GetComponent<TransformationContextMenu>() : null;
        }

        static TransformationContextMenu GetForSelectedCharacter()
        {
            var selection = SelectionController.Main;
            if (selection != null && selection.SelectedObjects != null)
            {
                foreach (PhysicalBehaviour selected in selection.SelectedObjects)
                {
                    var menu = FindTransformMenuFromPhysical(selected);
                    if (menu != null) return menu;
                }
            }

            var tool = Global.main?.ToolControllerBehaviour?.CurrentTool;
            if (tool != null && tool.ActiveSingleSelected != null)
            {
                var menu = FindTransformMenuFromPhysical(tool.ActiveSingleSelected);
                if (menu != null) return menu;
            }

            if (selection != null && selection.CurrentlyUnderMouse != null)
                return FindTransformMenuFromPhysical(selection.CurrentlyUnderMouse);

            return null;
        }

        void Update()
        {
            if (!QuickTransformInputUtil.Down() || PointerOverUi()) return;

            var menu = GetForSelectedCharacter();
            if (menu == null) return;

            menu.QuickToggleTransform();
        }
    }
}
