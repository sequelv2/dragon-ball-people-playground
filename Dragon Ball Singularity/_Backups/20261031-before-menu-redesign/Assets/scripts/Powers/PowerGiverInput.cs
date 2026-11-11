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
    public class PowerGiverInput : MonoBehaviour
    {
        static PowerGiverInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PowerGiverInput");
            _instance = go.AddComponent<PowerGiverInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (!PowerGiverState.Armed || string.IsNullOrEmpty(PowerGiverState.SelectedPowerId)) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;

            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            PowerGrantCatalog.GrantToPerson(person, PowerGiverState.SelectedPowerId);
            PowerGiverState.Disarm();
        }
    }
}
