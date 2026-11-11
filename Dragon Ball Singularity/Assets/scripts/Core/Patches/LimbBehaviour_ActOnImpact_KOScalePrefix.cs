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
    [HarmonyPatch(typeof(LimbBehaviour), "ActOnImpact")]
    public static class LimbBehaviour_ActOnImpact_KOScalePrefix
    {
        static void Prefix(LimbBehaviour __instance, ref float impulse)
        {
            if (__instance == null || !__instance.HasBrain) return;

            var link = __instance.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return;

            float r = link.Controller.Features.KnockoutResistance;

            if (float.IsNaN(r) || float.IsInfinity(r)) r = 1f;

            r = Mathf.Clamp(r, 1f, 1000f);

            impulse /= r;
        }
    }
}
