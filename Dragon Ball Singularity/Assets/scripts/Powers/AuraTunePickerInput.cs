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
    public class AuraTunePickerInput : MonoBehaviour
    {
        static AuraTunePickerInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("AuraTunePickerInput");
            _instance = go.AddComponent<AuraTunePickerInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (!AuraTunePickerState.Armed) return;
            if (DialogBox.IsAnyDialogboxOpen || Global.ActiveUiBlock) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;

            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            var aura = person.GetComponent<AuraHandler>();
            if (aura == null)
                aura = person.GetComponentInChildren<AuraHandler>();
            if (aura == null)
            {
                ModAPI.Notify("That character has no aura handler.");
                return;
            }

            AuraTunePickerState.Disarm();
            if (aura.person == null)
                aura.person = person;
            aura.OpenAuraTuneMenu();
        }
    }
}
