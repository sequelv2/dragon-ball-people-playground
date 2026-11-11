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
    public class PowerMenuConfigToggleEntry : PowerMenuConfigEntry
    {
        public Func<bool> GetValue;
        public Action<bool> SetValue;
        public override bool IsToggle => true;
        public override bool IsSlider => false;

        public void Toggle()
        {
            if (GetValue == null || SetValue == null) return;
            SetValue(!GetValue());
        }
    }
}
