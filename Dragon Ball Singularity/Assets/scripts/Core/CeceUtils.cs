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
    public static class CeceUtils
    {
        public static void DelayedInvoke(float delay, Action action)
        {
            new GameObject("Dbu_Delay").AddComponent<DelayerCaller>().Init(action, delay);
        }

        public static bool IsSelected(PhysicalBehaviour pb)
        {
            return SelectionController.Main.CurrentlyUnderMouse == pb
                || Global.main.ToolControllerBehaviour.CurrentTool.ActiveSingleSelected == pb;
        }

        public static void CreateContextMenuButton(PhysicalBehaviour behaviour, string id, string enableText, string disableText,
                                    Func<bool> condition, UnityAction onClick)
        {
            ContextMenuButton button = new ContextMenuButton(id,
                () => condition() ? disableText : enableText,
                $"Enables/Disables {id}",
                onClick);
            behaviour.ContextMenuOptions.Buttons.Add(button);
        }

        public static void FixBruises(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null) return;
            foreach (var limb in person.Limbs)
            {
                if (limb == null || limb.SkinMaterialHandler == null || limb.SkinMaterialHandler.renderer == null) continue;
                Color bruiseColor = new Color(0.404f, 0.165f, 0.300f, 1.000f);
                limb.SkinMaterialHandler.renderer.material.SetColor("_BruiseColor", bruiseColor);
                limb.SkinMaterialHandler.renderer.material.SetColor("_SecondBruiseColor", bruiseColor);
                limb.SkinMaterialHandler.renderer.material.SetColor("_ThirdBruiseColor", bruiseColor);
            }
        }
    }
}
