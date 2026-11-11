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
    public class UltraEgoDamageBoost : MonoBehaviour
    {
        public string CharacterId = "Vegeta";
        public string FormId = "UE";
        public float MaxBonusMultiplier = 2f;
        public float UpdateInterval = 0.2f;

        float _nextUpdate;
        float _baselineHealthSum;
        PersonBehaviour _person;
        TransformationContextMenu _menu;
        bool _wasActive;

        void Start()
        {
            _person = GetComponent<PersonBehaviour>();
            if (_person == null) _person = GetComponentInChildren<PersonBehaviour>();

            if (_person != null)
            {
                var head = _person.transform.Find("Head");
                if (head != null) _menu = head.GetComponent<TransformationContextMenu>();
            }
        }

        void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + Mathf.Max(0.05f, UpdateInterval);

            if (_person == null || _menu == null || !_person.IsAlive())
            {
                if (_wasActive)
                {
                    CharacterPowerCatalog.ResetRuntimeFormMultiplier(CharacterId, FormId);
                    _wasActive = false;
                }
                return;
            }

            bool active = _menu.Form == FormId;
            if (!active)
            {
                if (_wasActive)
                {
                    CharacterPowerCatalog.ResetRuntimeFormMultiplier(CharacterId, FormId);
                    _baselineHealthSum = 0f;
                    _wasActive = false;
                }
                return;
            }

            if (!_wasActive)
            {
                _baselineHealthSum = SumHealth(_person, useInitial: true);
                if (_baselineHealthSum <= 0f) _baselineHealthSum = 1f;
                _wasActive = true;
            }

            float current = SumHealth(_person, useInitial: false);
            float missingFrac = Mathf.Clamp01(1f - (current / _baselineHealthSum));
            float mult = 1f + missingFrac * Mathf.Max(0f, MaxBonusMultiplier - 1f);
            CharacterPowerCatalog.SetRuntimeFormMultiplier(CharacterId, FormId, mult);
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
