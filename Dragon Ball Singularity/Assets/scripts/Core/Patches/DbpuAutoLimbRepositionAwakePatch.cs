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
    [HarmonyPatch]
    public static class DbpuAutoLimbRepositionAwakePatch
    {
        static bool Prepare()
        {
            return AccessTools.TypeByName("Mod.AutoLimbReposition") != null;
        }

        static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Mod.AutoLimbReposition");
            return t == null ? null : AccessTools.Method(t, "Awake");
        }

        static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;
            try
            {
                __instance.StopCoroutine("Set");
            }
            catch { }

            try
            {
                var type = __instance.GetType();
                var limbField = AccessTools.Field(type, "limb");
                var limb = limbField != null ? limbField.GetValue(__instance) as LimbBehaviour : null;
                if (limb == null) limb = __instance.GetComponent<LimbBehaviour>();
                if (limb == null) return;

                if (limb.Joint != null)
                {
                    var anchorField = AccessTools.Field(type, "AnchorPosition");
                    if (anchorField != null)
                        anchorField.SetValue(__instance, limb.Joint.connectedAnchor);
                }

                var person = limb.Person;
                if (person != null)
                    DbpuAutoLimbCompat.SyncPersonDelayed(person);
            }
            catch { }
        }
    }
}
