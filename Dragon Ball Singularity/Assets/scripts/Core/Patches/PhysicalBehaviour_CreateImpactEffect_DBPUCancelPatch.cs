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
    [HarmonyPatch(typeof(PhysicalBehaviour), "CreateImpactEffect", new Type[] { typeof(Vector2), typeof(Vector2), typeof(float) })]
    public static class PhysicalBehaviour_CreateImpactEffect_DBPUCancelPatch
    {
        static bool Prefix(PhysicalBehaviour __instance)
        {
            return !DBPU_ShouldCancelImpactEffects(__instance);
        }

        static bool DBPU_ShouldCancelImpactEffects(PhysicalBehaviour phys)
        {
            if (phys == null) return false;

            LimbBehaviour limb = phys.GetComponent<LimbBehaviour>();
            if (limb == null) return false;
            if (limb.Person && limb.Person.GetComponent<BuuRegeneration>()) return false;

            var link = limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null) return true;

            HumanController hc = link.Controller;

            if (hc.Features.ImmuneToBullets)
                return true;

            if (link.GetCachedAverageWeight() > 0.4f)
                return true;

            return false;
        }
    }
}
