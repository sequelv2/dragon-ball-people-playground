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
    [HarmonyPatch(typeof(LimbBehaviour), "ExitShot")]
    public static class LimbBehaviour_ExitShot_BulletImmunity
    {
        static bool Prefix(LimbBehaviour __instance, ref Shot shot)
        {
            if (__instance == null) return true;
            if (UltraInstinctEvasion.TryEvade(__instance)) return false;

            var link = __instance.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return true;

            if (!link.Controller.Features.ImmuneToBullets) return true;

            return false;
        }
    }
}
