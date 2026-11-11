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
    public class TransformationMenuMultiplierTicker : MonoBehaviour
    {
        private TransformationContextMenu menu;
        private TextMeshProUGUI label;
        private float formMult;
        private string formId;

        public void Init(TransformationContextMenu menu, TextMeshProUGUI label, float formMult, string formId)
        {
            this.menu = menu;
            this.label = label;
            SetForm(formMult, formId);
        }

        public void SetForm(float formMult, string formId)
        {
            this.formMult = formMult;
            this.formId = formId;
        }

        private void Update()
        {
            if (menu != null && label != null)
                label.text = menu.FormatInfoMultiplier(formMult, formId);
        }
    }
}
