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
    internal class DelayerCaller : MonoBehaviour
    {
        private Action _ActionForInvoke;
        private float _Delay;

        public void Init(Action actionForInvoke, float delay)
        {
            _ActionForInvoke = actionForInvoke;
            _Delay = delay;
        }

        private void Start()
        {
            StartCoroutine(Delayer());
        }

        private IEnumerator Delayer()
        {
            yield return new WaitForSeconds(_Delay);
            _ActionForInvoke.Invoke();
            Destroy(gameObject);
        }
    }
}
