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
    // PPG signature is LoadSprite(string path, float scale = 1f, bool pixelated = true).
    [HarmonyPatch(typeof(ModAPI), nameof(ModAPI.LoadSprite), new Type[] { typeof(string), typeof(float), typeof(bool) })]
    public static class DbpuModApiLoadSpriteFallbackPatch
    {
        static void Postfix(string path, float scale, bool pixelated, ref Sprite __result)
        {
            if (__result != null || string.IsNullOrEmpty(path)) return;
            try
            {
                string resolved = CustomTransformationCatalog.ResolveModPath(path);
                if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                    __result = CustomTransformationCatalog.LoadSpriteFromDisk(resolved);
            }
            catch { }
        }
    }
}
