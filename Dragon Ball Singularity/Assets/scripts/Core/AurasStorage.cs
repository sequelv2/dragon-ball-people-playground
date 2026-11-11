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
    /*
    public class PhysProperties
    {
        public static PhysicalProperties StrongLimbProperties = ModAPI.FindPhysicalProperties("Human");
        public static PhysicalProperties SuperLimbProperties = ModAPI.FindPhysicalProperties("Human");
        public static PhysicalProperties GodlyLimbProperties = ModAPI.FindPhysicalProperties("Human");
        public static PhysicalProperties GodLimbProperties = ModAPI.FindPhysicalProperties("Human");
    }
    */

    public class AurasStorage
    {
        public static Dictionary<string, GameObject> AurasVFX = new Dictionary<string, GameObject>();
        public static Dictionary<string, AudioClip> AurasSFX = new Dictionary<string, AudioClip>();
        public static Dictionary<string, Color> ogColors = new Dictionary<string, Color>();
    }
}
