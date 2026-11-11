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
    // Floating Bug Fix (and similar) can clear IsTouchingSomething, which clears IsOnFloor.
    // Then PPG's airborne pose branch picks Protective (cower) for normal standing speeds.
    // Also fires when grabbed/picked up. Redirect to Rest, and for androids/Oozaru-style
    // bodies keep Rest even at higher speeds while held.
    [HarmonyPatch(typeof(PersonBehaviour), "DetermineActivePose")]
    public static class DbpuPersonBehaviour_FalseCowerPatch
    {
        static void Postfix(PersonBehaviour __instance)
        {
            try
            {
                if (__instance == null || __instance.ActivePose == null) return;
                if (!__instance.IsAlive()) return;
                if (__instance.ActivePose.State != PoseState.Protective) return;
                if (__instance.OverridePoseIndex != -1) return;
                if (__instance.ShockLevel > 0.5f || __instance.PainLevel > 0.8f) return;

                bool androidBody = false;
                if (__instance.Limbs != null)
                {
                    for (int i = 0; i < __instance.Limbs.Length; i++)
                    {
                        if (__instance.Limbs[i] != null && __instance.Limbs[i].IsAndroid)
                        {
                            androidBody = true;
                            break;
                        }
                    }
                }

                float speedCap = androidBody ? 120f : 40f;
                if (__instance.AverageSpeed > speedCap) return;
                if (__instance.LinkedPoses == null || !__instance.LinkedPoses.ContainsKey(PoseState.Rest)) return;
                RagdollPose rest = __instance.LinkedPoses[PoseState.Rest];
                if (rest == null) return;
                __instance.SetPose(rest);
            }
            catch { }
        }
    }
}
