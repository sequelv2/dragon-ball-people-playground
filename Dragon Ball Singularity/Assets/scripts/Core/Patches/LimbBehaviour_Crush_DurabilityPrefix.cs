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
    [HarmonyPatch(typeof(LimbBehaviour), "Crush")]
    public static class LimbBehaviour_Crush_DurabilityPrefix
    {
        static bool Prefix(LimbBehaviour __instance)
        {
            if (__instance == null) return true;
            if (UltraInstinctEvasion.TryEvade(__instance)) return false;
            return !DbpuLimbDurability.ShouldBlockDismemberment(__instance);
        }
    }
}
