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
    [HarmonyPatch(typeof(SkinMaterialHandler), "Stabbed")]
    public static class SkinMaterialHandler_Stabbed_DBPUCancelPatch
    {
        static bool Prefix(SkinMaterialHandler __instance, Stabbing stabbing)
        {
            if (__instance == null || __instance.limb == null) return true;
            if (UltraInstinctEvasion.TryEvade(__instance.limb)) return false;
            if (__instance.limb.Person && __instance.limb.Person.GetComponent<BuuRegeneration>()) return true;

            var link = __instance.limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return true;

            HumanController hc = link.Controller;

            if (hc.Features.ImmuneToStabbing)
                return false;

            if (link.GetCachedAverageWeight() > 0.4f)
                return false;

            return true;
        }
    }
}
