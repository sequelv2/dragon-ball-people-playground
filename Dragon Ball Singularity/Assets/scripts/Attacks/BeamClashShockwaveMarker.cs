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
    public class BeamClashShockwaveMarker : MonoBehaviour
    {
        public float BaseScale = 3f;
        public float OrbVisualRadius = 0.35f;
        public float MinRingRadius = 0.4f;
        public float MaxRingRadius = 12f;
        public float RingExpandSpeed = 9f;
        public LineRenderer[] Rings;
        public float[] RingRadii;
        public SpriteRenderer CoreOrb;
    }
}
