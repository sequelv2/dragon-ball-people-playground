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
    public static class LimbBehaviour_ActOnImpact_UltraInstinctEvasion
    {
        static bool Prefix(LimbBehaviour __instance, float impulse, Vector3 globalPosition)
        {
            if (__instance == null) return true;
            return !UltraInstinctEvasion.TryEvade(__instance);
        }
    }
}
