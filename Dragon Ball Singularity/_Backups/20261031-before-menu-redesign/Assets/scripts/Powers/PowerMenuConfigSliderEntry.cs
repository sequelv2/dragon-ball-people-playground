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
    public class PowerMenuConfigSliderEntry : PowerMenuConfigEntry
    {
        public Func<float> GetValue;
        public Action<float> SetValue;
        public Func<float> GetDisplayValue;
        public Action<float> SetDisplayValue;
        public float Min;
        public float Max;
        public bool ShowAsPercent;

        public override float GetRowHeight(PowerMenuLayoutConfig layout)
        {
            float gap = Mathf.Max(0f, layout.configSliderNameBarGap);
            return MenuUI.SpritePixels(DBPU.PConfigTextSlider).y
                + gap
                + MenuUI.SpritePixels(DBPU.PSliderValue).y;
        }
        public override bool IsToggle => false;
        public override bool IsSlider => true;

        public float ReadDisplay()
        {
            if (GetDisplayValue != null) return GetDisplayValue();
            return GetValue != null ? GetValue() : 0f;
        }

        public void WriteDisplay(float v)
        {
            if (SetDisplayValue != null) SetDisplayValue(v);
            else SetValue?.Invoke(v);
        }

        public string FormatDisplay(float display)
        {
            if (ShowAsPercent)
            {
                float pct = display <= 1.01f && display >= 0f ? display * 100f : display;
                return Mathf.Round(pct) + "%";
            }
            return display.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
