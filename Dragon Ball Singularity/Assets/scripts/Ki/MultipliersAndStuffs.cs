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
    public class MultipliersAndStuffs : MonoBehaviour
    {
        public float FormMultiplier = 1f;
        public float BoostMultiplier = 1f;
        public float KaiokenMultiplier = 1f;
        public float RageMultiplier = 1f;
        public float MajinMultiplier = 1f;
        public float ExponentialMultiplier = 1f;
        public float OtherMultiplier = 1f;

        public float FinalMultiplier = 1f;

        public float KiOutput = 1f;
        public float KiOverdriveUsageCostScale => KiOutput <= 1f ? 1f : KiOutput;
        private AllyKiReservoir allyKi;
        public float AllyKiMultiplier
        {
            get
            {
                if (!allyKi) allyKi = GetComponent<AllyKiReservoir>();
                return allyKi ? allyKi.PowerMultiplier : 1f;
            }
        }

        public void Update()
        {
            Recalculate();
        }

        public void Recalculate()
        {
            double total = (double)FormMultiplier * BoostMultiplier * KaiokenMultiplier * RageMultiplier
                * MajinMultiplier * ExponentialMultiplier * OtherMultiplier * AllyKiMultiplier * CombatPowerController.Mode(GetComponent<PersonBehaviour>());
            FinalMultiplier = double.IsNaN(total) ? 1f : (float)Math.Max(0d, Math.Min(float.MaxValue, total));
        }
    }
}
