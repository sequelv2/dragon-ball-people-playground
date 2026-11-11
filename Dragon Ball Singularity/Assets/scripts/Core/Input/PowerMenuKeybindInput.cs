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
    public class PowerMenuKeybindInput : MonoBehaviour
    {
        static PowerMenuKeybindInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PowerMenuKeybindInput");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<PowerMenuKeybindInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (!PowerMenuInputUtil.Down() || PointerOverUi()) return;

            var menu = PowerContextMenu.GetForSelectedLimb();
            if (menu == null) return;
            menu.OpenPowersMenu();
        }
    }
}
