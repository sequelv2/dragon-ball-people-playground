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
    [HarmonyPatch(typeof(LimbBehaviour), "OnCollisionEnter2D")]
    public static class LimbBehaviour_OnCollisionEnter2D_TemperatureResistancePatch
    {
        private static float GetContactBurnThreshold(LimbBehaviour limb)
        {
            if (limb == null) return 70f;

            var link = limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return 70f;

            // Live value: if you change it at runtime, this returns the new value immediately.
            float v = link.Controller.Features.HighTemperatureResistance;

            // safety clamp (optional)
            if (v < 0f) v = 0f;

            return v;
        }

        public static IEnumerable<HarmonyLib.CodeInstruction> Transpiler(IEnumerable<HarmonyLib.CodeInstruction> instructions)
        {
            foreach (var ins in instructions)
            {
                // Replace hardcoded "70f" with GetContactBurnThreshold(__instance)
                if (ins.opcode == OpCodes.Ldc_R4 && ins.operand is float f && Mathf.Approximately(f, 70f))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // LimbBehaviour __instance
                    yield return CodeInstruction.Call(
                        typeof(LimbBehaviour_OnCollisionEnter2D_TemperatureResistancePatch),
                        nameof(GetContactBurnThreshold)
                        );
                    continue;
                }

                yield return ins;
            }
        }
    }
}
