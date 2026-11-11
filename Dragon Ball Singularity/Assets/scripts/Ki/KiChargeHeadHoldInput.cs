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
    [DefaultExecutionOrder(-150)]
    public class KiChargeHeadHoldInput : MonoBehaviour
    {
        private PhysicalBehaviour phys;
        private KiChargeModeController controller;
        private PersonBehaviour person;

        void Awake()
        {
            phys = GetComponent<PhysicalBehaviour>();
            person = transform.root.GetComponent<PersonBehaviour>();
            controller = transform.root.GetComponent<KiChargeModeController>();
        }

        void Update()
        {
            if (controller == null)
                controller = transform.root.GetComponent<KiChargeModeController>();
            if (controller == null || phys == null || person == null || !person.IsAlive())
            {
                if (controller != null) controller.SetHoldDesired(false);
                return;
            }
            if (!DBPU.KiChargeFxEnabled)
            {
                controller.SetHoldDesired(false);
                return;
            }
            if (GetComponent<CellMaxScream>() != null)
                return;
            if (!CeceUtils.IsSelected(phys))
            {
                controller.SetHoldDesired(false);
                return;
            }
            var it = transform.root.GetComponent<InstantTransmission>();
            if (it != null && it.Active && it.CanUse)
            {
                controller.SetHoldDesired(false);
                return;
            }
            bool hold = DbpuInputUtil.Held(DBPU.KiChargeHoldActionId, KeyCode.R);
            controller.SetHoldDesired(hold);
        }
    }
}
