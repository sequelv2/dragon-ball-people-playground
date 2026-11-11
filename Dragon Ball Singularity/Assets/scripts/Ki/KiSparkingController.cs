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
    [DefaultExecutionOrder(-200)]
    public class KiSparkingController : MonoBehaviour
    {
        public enum SparkPhase { Idle, Overload, Exhaust }

        public SparkPhase CurrentPhase { get; private set; } = SparkPhase.Idle;

        private KiSystem kiSystem;
        private PersonBehaviour person;
        private float phaseTimer;
        private float cooldownTimer;
        private AuraHandler auraHandler;

        private struct OverloadLimbBackup
        {
            public LimbBehaviour Limb;
            public float BaseStrength;
            public float BreakingThreshold;
            public float ImpactPainMultiplier;
        }

        private readonly List<OverloadLimbBackup> _overloadLimbBackups = new List<OverloadLimbBackup>();
        private bool _overloadBuffApplied;
        private const float OverloadStrengthMultiplier = 1.22f;
        private const float OverloadBreakThresholdMultiplier = 1.14f;
        private const float OverloadImpactPainMultiplier = 0.78f;

        void Awake()
        {
            kiSystem = GetComponent<KiSystem>();
            person = GetComponent<PersonBehaviour>();
            auraHandler = GetComponent<AuraHandler>();
            if (GetComponent<SparkingOutline>() == null) gameObject.AddComponent<SparkingOutline>();
        }

        void PushSparkingKiFlags()
        {
            if (kiSystem == null) return;
            kiSystem.SparkingOverloadActive = CurrentPhase == SparkPhase.Overload;
            kiSystem.SparkingExhaustActive = CurrentPhase == SparkPhase.Exhaust;
        }

        private void ApplyOverloadBuffs()
        {
            if (_overloadBuffApplied || person == null || person.Limbs == null) return;
            _overloadLimbBackups.Clear();
            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                _overloadLimbBackups.Add(new OverloadLimbBackup
                {
                    Limb = limb,
                    BaseStrength = limb.BaseStrength,
                    BreakingThreshold = limb.BreakingThreshold,
                    ImpactPainMultiplier = limb.ImpactPainMultiplier
                });

                limb.BaseStrength *= OverloadStrengthMultiplier;
                limb.BreakingThreshold *= OverloadBreakThresholdMultiplier;
                limb.ImpactPainMultiplier *= OverloadImpactPainMultiplier;
            }
            _overloadBuffApplied = true;
        }

        private void ClearOverloadBuffs()
        {
            if (!_overloadBuffApplied) return;
            for (int i = 0; i < _overloadLimbBackups.Count; i++)
            {
                var b = _overloadLimbBackups[i];
                if (b.Limb == null) continue;
                b.Limb.BaseStrength = b.BaseStrength;
                b.Limb.BreakingThreshold = b.BreakingThreshold;
                b.Limb.ImpactPainMultiplier = b.ImpactPainMultiplier;
            }
            _overloadLimbBackups.Clear();
            _overloadBuffApplied = false;
        }

        void Update()
        {
            if (kiSystem == null || person == null) return;

            if (!DBPU.SparkingEnabled)
            {
                if (CurrentPhase != SparkPhase.Idle)
                {
                    CurrentPhase = SparkPhase.Idle;
                    phaseTimer = 0f;
                    if (kiSystem != null) kiSystem.SparkingOvercharge01 = 0f;
                    if (auraHandler != null) auraHandler.SetSparkingPulse(false);
                    ClearOverloadBuffs();
                    PushSparkingKiFlags();
                }
                return;
            }

            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (!person.IsAlive())
            {
                CurrentPhase = SparkPhase.Idle;
                phaseTimer = 0f;
                if (kiSystem != null) kiSystem.SparkingOvercharge01 = 0f;
                if (auraHandler != null) auraHandler.SetSparkingPulse(false);
                ClearOverloadBuffs();
                PushSparkingKiFlags();
                return;
            }

            switch (CurrentPhase)
            {
                case SparkPhase.Overload:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        CurrentPhase = SparkPhase.Exhaust;
                        phaseTimer = Mathf.Max(0.1f, DBPU.SparkingExhaustSeconds);
                        kiSystem.currentKi = 0f;
                        GetComponent<KiChargeModeController>()?.ForceEndCharge();
                        if (auraHandler != null) auraHandler.SetSparkingPulse(true);
                        ClearOverloadBuffs();
                    }
                    break;
                case SparkPhase.Exhaust:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        CurrentPhase = SparkPhase.Idle;
                        cooldownTimer = Mathf.Max(0f, DBPU.SparkingCooldownSeconds);
                        if (auraHandler != null) auraHandler.SetSparkingPulse(false);
                        ClearOverloadBuffs();
                    }
                    break;
            }

            PushSparkingKiFlags();
        }

        public bool TryStartOverload()
        {
            if (!DBPU.SparkingEnabled) return false;
            if (person == null || !person.IsAlive()) return false;
            if (CurrentPhase != SparkPhase.Idle) return false;
            if (cooldownTimer > 0f) return false;
            CurrentPhase = SparkPhase.Overload;
            phaseTimer = Mathf.Max(0.1f, DBPU.SparkingOverloadSeconds);
            kiSystem.currentKi = kiSystem.maxKi;
            if (kiSystem != null) kiSystem.SparkingOvercharge01 = 0f;
            GetComponent<KiChargeModeController>()?.ForceEndCharge(true);
            if (auraHandler != null) auraHandler.SetSparkingPulse(true);
            ApplyOverloadBuffs();
            PushSparkingKiFlags();
            return true;
        }

        /// <summary>Ki overdrive held above 100% too long — same exhaust phase as post-Sparking.</summary>
        public bool ForceEnterExhaustFromOverdrive()
        {
            if (!DBPU.SparkingEnabled) return false;
            if (person == null || !person.IsAlive()) return false;
            if (CurrentPhase == SparkPhase.Overload) return false;

            if (CurrentPhase == SparkPhase.Exhaust)
                phaseTimer = Mathf.Max(phaseTimer, Mathf.Max(0.1f, DBPU.SparkingExhaustSeconds));
            else
            {
                CurrentPhase = SparkPhase.Exhaust;
                phaseTimer = Mathf.Max(0.1f, DBPU.SparkingExhaustSeconds);
            }

            kiSystem.currentKi = 0f;
            GetComponent<KiChargeModeController>()?.ForceEndCharge();
            if (auraHandler != null) auraHandler.SetSparkingPulse(true);
            ClearOverloadBuffs();
            PushSparkingKiFlags();
            return true;
        }

        void OnDestroy()
        {
            ClearOverloadBuffs();
        }
    }
}
