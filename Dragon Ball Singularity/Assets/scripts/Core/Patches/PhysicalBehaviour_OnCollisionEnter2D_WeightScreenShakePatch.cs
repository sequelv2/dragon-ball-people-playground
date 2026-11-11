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
    [HarmonyPatch(typeof(PhysicalBehaviour), "OnCollisionEnter2D")]
    public static class PhysicalBehaviour_OnCollisionEnter2D_WeightScreenShakePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float f && Mathf.Approximately(f, 0.01f))
                {
                    yield return instruction;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);

                    yield return CodeInstruction.Call(
                        typeof(PhysicalBehaviour_OnCollisionEnter2D_WeightScreenShakePatch),
                        nameof(GetScreenShakeMultiplier)
                    );

                    yield return new CodeInstruction(OpCodes.Mul);

                    continue;
                }

                yield return instruction;
            }
        }

        public static float GetScreenShakeMultiplier(PhysicalBehaviour phys)
        {
            if (phys == null) return 1f;

            LimbBehaviour limb = phys.GetComponent<LimbBehaviour>();
            if (limb == null || limb.Person == null) return 1f;

            var link = limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return 1f;

            float weight = link.GetCachedAverageWeight();

            if (weight <= 0.4f) return 1f;

            return 1f / Mathf.Max(1f, weight / 0.15f);
        }
    }
}
