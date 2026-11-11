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
    public class DeferredOutlineRefresh : MonoBehaviour
    {
        PersonBehaviour person;
        PhysicalBehaviour single;
        int waitFrames;

        public static void RequestPerson(PersonBehaviour target)
        {
            if (target == null) return;
            var host = target.gameObject.GetComponent<DeferredOutlineRefresh>();
            if (host == null) host = target.gameObject.AddComponent<DeferredOutlineRefresh>();
            host.person = target;
            host.single = null;
            host.waitFrames = 2;
            host.enabled = true;
        }

        public static void RequestSingle(PhysicalBehaviour phys)
        {
            if (phys == null) return;
            var host = phys.gameObject.GetComponent<DeferredOutlineRefresh>();
            if (host == null) host = phys.gameObject.AddComponent<DeferredOutlineRefresh>();
            host.single = phys;
            host.person = null;
            host.waitFrames = 2;
            host.enabled = true;
        }

        void Update()
        {
            if (waitFrames > 0)
            {
                waitFrames--;
                return;
            }

            if (person != null && person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    var limb = person.Limbs[i];
                    if (limb == null || limb.gameObject == null) continue;
                    var phys = limb.PhysicalBehaviour;
                    if (phys == null) phys = limb.gameObject.GetComponent<PhysicalBehaviour>();
                    if (phys != null) phys.RefreshOutline();
                }
            }
            else if (single != null)
            {
                single.RefreshOutline();
            }

            enabled = false;
        }
    }
}
