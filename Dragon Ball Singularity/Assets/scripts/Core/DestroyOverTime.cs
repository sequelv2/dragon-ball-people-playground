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
    public class DestroyOverTime : MonoBehaviour
    {
        public float Timer;

        public void Start()
        {
            StartCoroutine(StartTimer());
        }

        public IEnumerator StartTimer()
        {
            yield return new WaitForSeconds(Timer);

            Destroy(gameObject);
        }
    }
}
