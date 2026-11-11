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
    public class DbpuMenuSlider
    {
        public string Label;
        public Func<float> GetValue;
        public Action<float> SetValue;
        public float Min = 0f;
        public float Max = 2f;
        public bool ShowAsPercent = true;

        public DbpuMenuSlider(string label, Func<float> getValue, Action<float> setValue, float min = 0f, float max = 2f, bool showAsPercent = true)
        {
            Label = label;
            GetValue = getValue;
            SetValue = setValue;
            Min = min;
            Max = max;
            ShowAsPercent = showAsPercent;
        }
    }
}
