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
    static class DbpuInputUtil
    {
        public static bool Down(string inputId, KeyCode fallback)
        {
            if (KiFistInput.BlocksAction(inputId, fallback)) return false;
            if (TorsoTechniquePicker.BlocksAction(inputId, fallback)) return false;
            if (CombatAbilityWheel.AnyMenuCaptured) return false;
            if (InputSystem.Has(inputId))
                return InputSystem.Down(inputId);

            return Input.GetKeyDown(fallback);
        }

        public static bool Held(string inputId, KeyCode fallback)
        {
            if (KiFistInput.BlocksAction(inputId, fallback)) return false;
            if (CombatAbilityWheel.AnyMenuCaptured) return false;
            if (InputSystem.Has(inputId))
                return InputSystem.Held(inputId);

            return Input.GetKey(fallback);
        }

        public static bool Up(string inputId, KeyCode fallback)
        {
            if (KiFistInput.BlocksAction(inputId, fallback)) return true;
            // UI capture releases held abilities so their cleanup cannot lose a physical key-up edge.
            if (CombatAbilityWheel.AnyMenuCaptured) return true;
            if (InputSystem.Has(inputId))
                return InputSystem.Up(inputId);

            return Input.GetKeyUp(fallback);
        }
    }
}
