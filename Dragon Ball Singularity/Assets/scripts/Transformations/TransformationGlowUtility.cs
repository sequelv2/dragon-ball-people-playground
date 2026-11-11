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
    static class TransformationGlowUtility
    {
        public const float AlphaAt100Percent = 0.01f;
        public const float MaxBrightnessPercent = 5000f;

        public static float PercentToAlpha(float percent)
        {
            return Mathf.Max(0f, percent) / 100f * AlphaAt100Percent;
        }

        public static float AlphaToPercent(float alpha)
        {
            if (AlphaAt100Percent <= 0f) return 0f;
            return Mathf.Max(0f, alpha / AlphaAt100Percent * 100f);
        }

        public static float MigrateLegacyPercent(float value, float defaultPercent)
        {
            if (value <= 0f) return defaultPercent;
            if (value <= 1f) return AlphaToPercent(value);
            return value;
        }
    }
}
