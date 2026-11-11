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
    public class CharacterPowerSaveData
    {
        public Dictionary<string, string> DisplayNames = new Dictionary<string, string>();
        public Dictionary<string, Dictionary<string, float>> Defaults = new Dictionary<string, Dictionary<string, float>>();
        public Dictionary<string, Dictionary<string, string>> FormNames = new Dictionary<string, Dictionary<string, string>>();
        public Dictionary<string, float> BasePowerOverrides = new Dictionary<string, float>();
        public Dictionary<string, Dictionary<string, float>> Overrides = new Dictionary<string, Dictionary<string, float>>();
        public int PowerScaleVersion;
    }
}
