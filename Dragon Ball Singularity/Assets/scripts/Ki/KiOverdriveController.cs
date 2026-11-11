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
    [DefaultExecutionOrder(-190)]
    public class KiOverdriveController : MonoBehaviour
    {
        private PersonBehaviour _person;
        private MultipliersAndStuffs _mul;
        private KiSparkingController _spark;

        private void Awake()
        {
            _person = GetComponent<PersonBehaviour>();
            _mul = GetComponent<MultipliersAndStuffs>();
            if (_mul == null) _mul = gameObject.AddComponent<MultipliersAndStuffs>();
            _spark = GetComponent<KiSparkingController>();
        }

        private void Update()
        {
            if (!DBPU.KiOverdriveOutputEnabled || _person == null || !_person.IsAlive())
                return;
            if (_mul == null) return;
            if (GetComponent<CombatPowerController>()) return;

            float maxOut = Mathf.Clamp(DBPU.KiOverdriveMaxPercent / 100f, 1.01f, 2.5f);
            _mul.KiOutput = Mathf.Clamp(_mul.KiOutput, 0.0001f, Mathf.Max(maxOut, 10f));

            TryHandOverdriveInput();
        }

        private bool TryHandOverdriveInput()
        {
            if (!DbpuInputUtil.Down(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey)) return false;

            var chargeCtrl = GetComponent<KiChargeModeController>();
            if (chargeCtrl != null && chargeCtrl.IsActivelyCharging)
            {
                CycleKiOutput(_mul);
                StartCoroutine(OverdriveOutputPopupRoutine(_mul.KiOutput));
                return true;
            }

            if (_spark != null && (_spark.CurrentPhase == KiSparkingController.SparkPhase.Overload
                || _spark.CurrentPhase == KiSparkingController.SparkPhase.Exhaust))
            {
                CycleKiOutput(_mul);
                StartCoroutine(OverdriveOutputPopupRoutine(_mul.KiOutput));
                return true;
            }

            foreach (var limb in _person.Limbs)
            {
                if (limb == null || limb.PhysicalBehaviour == null) continue;
                if (!limb.name.Contains("LowerArm")) continue;
                var pb = limb.PhysicalBehaviour;
                if (!pb.IsBeingUsedContinuously() && !CeceUtils.IsSelected(pb)) continue;
                CycleKiOutput(_mul);
                StartCoroutine(OverdriveOutputPopupRoutine(_mul.KiOutput));
                return true;
            }

            return false;
        }

        private static TMP_FontAsset GetHolographicUiFont()
        {
            try
            {
                var s = ModAPI.FindSpawnable("Holographic Display");
                if (s?.Prefab == null) return null;
                var tr = s.Prefab.transform.Find("Text");
                if (tr == null) return null;
                var tmp = tr.GetComponent<TextMeshPro>();
                return tmp != null ? tmp.font : null;
            }
            catch
            {
                return null;
            }
        }

        private Transform GetKiOutputPopupAnchor()
        {
            if (_person != null)
            {
                foreach (var limb in _person.Limbs)
                {
                    if (limb == null || limb.PhysicalBehaviour == null) continue;
                    if (!limb.name.Contains("LowerArm")) continue;
                    var pb = limb.PhysicalBehaviour;
                    if (pb.IsBeingUsedContinuously() || CeceUtils.IsSelected(pb))
                        return limb.transform;
                }
            }

            Transform head = transform.root.Find("Head");
            return head != null ? head : transform;
        }

        private IEnumerator OverdriveOutputPopupRoutine(float output01)
        {
            string msg = "Ki output: " + Mathf.RoundToInt(output01 * 100f) + "%";
            Transform anchor = GetKiOutputPopupAnchor();
            float anchorScaleY = anchor.localScale.y;
            Vector3 startPos = anchor.position + (-anchor.up * (0.25f * anchorScaleY)) + (Vector3.up * 0.15f);

            var textObj = new GameObject("DBPU_KiOutputText");
            textObj.transform.position = startPos;
            var textStorage = textObj.AddComponent<TextMeshPro>();
            textStorage.text = msg;
            textStorage.fontSize = 1.5f;
            TMP_FontAsset f = GetHolographicUiFont();
            if (f != null) textStorage.font = f;
            textStorage.alignment = TextAlignmentOptions.Center;
            textStorage.color = Color.white;
            textStorage.horizontalAlignment = HorizontalAlignmentOptions.Center;
            textStorage.verticalAlignment = VerticalAlignmentOptions.Middle;
            textObj.GetComponent<MeshRenderer>().sortingLayerName = "Bubbles";
            textObj.GetComponent<MeshRenderer>().sortingOrder += 1;

            float t = 0f;
            float a = 1f;
            Vector3 finalPos = textObj.transform.position + (Vector3.up * 0.75f);

            while (t < 1.2f)
            {
                t += Time.deltaTime;
                textObj.transform.position = Vector3.Lerp(textObj.transform.position, finalPos, 2.4f * Time.deltaTime);

                if (t > 0.6f)
                {
                    a = Mathf.Lerp(a, -0.1f, 4.8f * Time.deltaTime);
                    textStorage.color = new Color(1f, 1f, 1f, a);
                }

                yield return new WaitForFixedUpdate();
            }

            Destroy(textObj);
        }

        private static void CycleKiOutput(MultipliersAndStuffs m)
        {
            float max = Mathf.Clamp(DBPU.KiOverdriveMaxPercent / 100f, 1.01f, 2.5f);
            float[] steps = { 1f, 1.25f, 1.5f, 1.75f, 2f };
            float cur = m.KiOutput;
            int idx = -1;
            for (int i = 0; i < steps.Length; i++)
            {
                if (Mathf.Abs(steps[i] - cur) < 0.045f) idx = i;
            }

            if (idx < 0)
            {
                if (cur <= 1.01f)
                    m.KiOutput = Mathf.Min(1.25f, max);
                else
                    m.KiOutput = 1f;
                return;
            }

            int next = idx + 1;
            if (next >= steps.Length || steps[next] > max + 0.01f)
                m.KiOutput = 1f;
            else
                m.KiOutput = Mathf.Min(steps[next], max);
        }
    }
}
