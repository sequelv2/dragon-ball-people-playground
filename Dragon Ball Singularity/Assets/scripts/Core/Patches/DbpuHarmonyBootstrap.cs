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
    public static class DbpuHarmonyBootstrap
    {
        static bool _applied;

        public static void EnsureApplied()
        {
            if (_applied) return;
            _applied = true;
            try
            {
                new Harmony("DBPU.HumanController.HeatThreshold").PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Debug.LogError("DBPU Harmony PatchAll failed: " + ex);
            }
        }
    }
}
