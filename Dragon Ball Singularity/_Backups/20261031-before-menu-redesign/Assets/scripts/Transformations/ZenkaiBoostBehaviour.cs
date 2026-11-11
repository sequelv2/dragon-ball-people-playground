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
    public class ZenkaiBoostBehaviour : MonoBehaviour
    {
        static readonly string[] SaiyanMarkers =
        {
            "Goku", "Vegeta", "Gohan", "Broly", "Trunks", "Goten", "Gogeta", "Vegito", "Gogito",
            "Pan", "Paragus", "Gotenks", "Bardock", "Raditz", "Nappa", "Oozaru", "Cabba", "Kale",
            "Caulifla", "Shallot", "Turles", "Tarble", "Cumber", "Future Trunks"
        };

        PersonBehaviour _person;
        MultipliersAndStuffs _multipliers;
        float _baselineHealthSum;
        float _pendingDamage;

        public static bool IsSaiyan(string characterId, string displayName)
        {
            string hay = ((characterId ?? "") + " " + (displayName ?? "")).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(hay)) return false;
            for (int i = 0; i < SaiyanMarkers.Length; i++)
            {
                if (hay.IndexOf(SaiyanMarkers[i].ToLowerInvariant(), StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public void RegisterDamage(float damage)
        {
            if (!DBPU.Special_ZenkaiBoostEnabled) return;
            if (damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            _pendingDamage += damage;
        }

        void Start()
        {
            EnsureRefs();
            CaptureBaseline();
        }

        void Update()
        {
            // Singularity's lore Zenkai (near-death -> recovery) supersedes the damage trickle.
            if (Singularity.IsEnabled("zenkai")) { _pendingDamage = 0f; return; }
            if (!DBPU.Special_ZenkaiBoostEnabled || _pendingDamage <= 0f) return;
            EnsureRefs();
            if (_multipliers == null) return;
            if (_baselineHealthSum <= 0f) CaptureBaseline();
            if (_baselineHealthSum <= 0f) _baselineHealthSum = 1f;

            float frac = _pendingDamage / _baselineHealthSum;
            _pendingDamage = 0f;
            float gain = frac * Mathf.Max(0f, DBPU.Special_ZenkaiBoostScale);
            if (gain <= 0f) return;

            float max = Mathf.Max(1f, DBPU.Special_ZenkaiBoostMax);
            _multipliers.BoostMultiplier = Mathf.Min(_multipliers.BoostMultiplier + gain, max);
        }

        void EnsureRefs()
        {
            if (_person == null)
            {
                _person = GetComponent<PersonBehaviour>();
                if (_person == null) _person = GetComponentInChildren<PersonBehaviour>();
            }
            if (_multipliers == null)
            {
                var root = transform.root.gameObject;
                _multipliers = root.GetComponent<MultipliersAndStuffs>();
                if (_multipliers == null) _multipliers = root.AddComponent<MultipliersAndStuffs>();
            }
        }

        void CaptureBaseline()
        {
            _baselineHealthSum = SumHealth(_person, true);
        }

        static float SumHealth(PersonBehaviour person, bool useInitial)
        {
            if (person == null || person.Limbs == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                float v = useInitial ? limb.InitialHealth : limb.Health;
                if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f) continue;
                sum += v;
            }
            return sum;
        }
    }
}
