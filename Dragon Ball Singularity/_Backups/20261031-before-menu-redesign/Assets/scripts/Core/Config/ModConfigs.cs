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
    public class ModConfigs
    {
        public float PowerMultiplier = 1f;
        public float KiDrainMultiplier = 0.5f;
        public float KiRegenMultiplier = 1f;
        public float KiAttackCostMultiplier = 1f;
        public bool AurasEnabled = true;
        public bool KiAtkCanDestroy = true;
        public bool KiAtkCanDelete = false;
        public bool ExtraCharge = true;
        public float KiBlast_ExplosionSizeMultiplier = 1f;
        public bool KiBlast_CustomColorEnabled = false;
        public float KiBlast_CustomColorR = 1f;
        public float KiBlast_CustomColorG = 0.85f;
        public float KiBlast_CustomColorB = 0.3f;

        public float DamageMultiplier = 1f;
        public float HealthMultiplier = 1f;
        public bool PhysAtkCanDestroy = true;
        public bool PhysAtkStopLimbs = true;
        public bool AutoEnablePunchKick = false;
        public float Universal_KnockbackMultiplier = 1f;
        public bool DismembermentEnabled = true;

        public float StrainDrainMultiplier = 1;
        public float StrainGainMultiplier = 1f;
        public float StrainDamageMultiplier = 1f;
        public float KaiokenStrainMultiplier = 1f;
        public float KaiokenMaxMultiplier = 20f;
        public bool KaiokenStrainDamageEnabled = true;

        public float Beam_KnockbackMultiplier = 1f;
        public float Beam_SpeedMultiplier = 1f;
        public float Beam_SizeMultiplier = 1f;
        public bool Beam_ToggleDelete = false;
        public bool Beam_ToggleDestroy = true;
        public bool Beam_ToggleHeat = false;

        public bool Beam_BurnAwayOnKill = true;
        public bool KiDeflectEnabled = true;
        public float KiDeflectHybridPowerThreshold = 1.35f;
        public float KiDeflectBlastThresholdMultiplier = 1f;
        public float KiDeflectBeamThresholdMultiplier = 1.2f;
        public bool KiDeflectBeamCatchThenAim = false;
        public float KiDeflectBeamCatchSeconds = 0.22f;
        public bool KiDeflectBeamSplitEnabled = true;
        public float KiDeflectBeamSplitThresholdMultiplier = 0.85f;
        public int KiDeflectBeamSplitRayCount = 9;
        public float KiDeflectBeamSplitWrapDistance = 1.1f;
        public float KiDeflectBeamSplitContinuationLength = 7f;
        public float KiDeflectBeamSplitArcDegrees = 150f;
        public float KiDeflectBeamSplitLifetime = 0.42f;
        public bool BeamAssistEnabled = true;
        public bool BeamClashShockwaveEnabled = false;

        public float Special_ExponentialGrowthSpeed = 1.005f;
        public bool Special_ZenkaiBoostEnabled = false;
        public float Special_ZenkaiBoostScale = 1f;
        public float Special_ZenkaiBoostMax = 10f;

        public bool VoiceLines = false;
        public float VoiceLineVolume = 1f;
        public float PowersVolume = 1f;

        public float KiChargeHoldRegenPerSecond = 50000f;
        public float KiChargeAuraScaleMultiplier = 1.25f;
        public float KiChargeAuraParticleSpeedMultiplier = 1.35f;
        public int KiChargeDebrisMaxActive = 28;
        public int KiChargeDebrisMaxSession = 80;
        public float KiChargeDebrisSpawnInterval = 0.52f;
        public float KiChargeDebrisRingMin = 0.35f;
        public float KiChargeDebrisRingMax = 1.1f;
        public float KiChargeDebrisSpawnYOffset = -1f;

        public KeyCode KiChargeHoldKey = KeyCode.None;
        public float SparkingOverloadSeconds = 180f;
        public float SparkingExhaustSeconds = 60f;
        public float SparkingCooldownSeconds = 0f;
        public float SparkingOverchargeHoldSeconds = 3f;
        public bool SparkingEnabled = true;
        public bool KiChargeSparkingOverchargeEnabled = true;
        public int DBPUConfigVersion = 4;
        public bool KiOverdriveOutputEnabled = true;
        public KeyCode KiOverdriveOutputKey = KeyCode.R;
        public float KiOverdriveMaxPercent = 200f;
        public float KiOverdriveSecondsBeforeExhaust = 2.75f;
        public int CinematicVfxRevision = 0;
        public bool KiChargeFxEnabled = true;
        public bool KiChargeAutoTransformWhileChargingEnabled = false;
        public float KiChargeAutoTransformSecondsPerTier = 12f;

        public bool TransformationEyeGlowEnabled = true;
        public bool TransformationHairGlowEnabled = true;
        public float TransformationHairGlowBrightness = 100f;

        public bool QuickTransformEnabled = true;

        public float UiAuraScale = 3.4f;
        public float UiAuraYOffset = -0.09f;

        public static ModConfigs configurations;

        public const string Path = "DBPU_Configs.json";

        public static void Load()
        {
            try
            {
                configurations = ModAPI.DeserialiseJSON<ModConfigs>(Path);
                if(configurations.CinematicVfxRevision<1)
                {
                    configurations.CinematicVfxRevision=1;
                    configurations.KiChargeFxEnabled=true;
                    try{ModAPI.SerialiseJSON(configurations,Path);}
                    catch(Exception ex){Debug.LogWarning("[DBPU Cinematic] Configuration migration: "+ex.Message);}
                }

                DBPU.PowerMultiplier = configurations.PowerMultiplier;
                DBPU.KiDrainMultiplier = configurations.KiDrainMultiplier;
                DBPU.KiRegenMultiplier = configurations.KiRegenMultiplier;
                DBPU.KiAttackCostMultiplier = configurations.KiAttackCostMultiplier;
                DBPU.AurasEnabled = configurations.AurasEnabled;
                DBPU.KiAtkCanDestroy = configurations.KiAtkCanDestroy;
                DBPU.KiAtkCanDelete = configurations.KiAtkCanDelete;
                DBPU.ExtraCharge = configurations.ExtraCharge;
                DBPU.KiBlast_ExplosionSizeMultiplier = configurations.KiBlast_ExplosionSizeMultiplier < 0.05f ? 1f : configurations.KiBlast_ExplosionSizeMultiplier;
                DBPU.KiBlast_CustomColorEnabled = configurations.KiBlast_CustomColorEnabled;
                DBPU.KiBlast_CustomColorR = Mathf.Clamp01(configurations.KiBlast_CustomColorR);
                DBPU.KiBlast_CustomColorG = Mathf.Clamp01(configurations.KiBlast_CustomColorG);
                DBPU.KiBlast_CustomColorB = Mathf.Clamp01(configurations.KiBlast_CustomColorB);

                DBPU.DamageMultiplier = configurations.DamageMultiplier;
                DBPU.HealthMultiplier = configurations.HealthMultiplier;
                DBPU.PhysAtkCanDestroy = configurations.PhysAtkCanDestroy;
                DBPU.PhysAtkStopLimbs = configurations.PhysAtkStopLimbs;
                DBPU.AutoEnablePunchKick = configurations.AutoEnablePunchKick;
                DBPU.Universal_KnockbackMultiplier = configurations.Universal_KnockbackMultiplier < 0f ? 1f : configurations.Universal_KnockbackMultiplier;
                DBPU.DismembermentEnabled = configurations.DismembermentEnabled;

                DBPU.StrainDrainMultiplier = configurations.StrainDrainMultiplier;
                DBPU.StrainGainMultiplier = configurations.StrainGainMultiplier;
                DBPU.StrainDamageMultiplier = configurations.StrainDamageMultiplier;
                DBPU.KaiokenStrainMultiplier = configurations.KaiokenStrainMultiplier;
                DBPU.KaiokenMaxMultiplier = configurations.KaiokenMaxMultiplier < 1f ? 20f : configurations.KaiokenMaxMultiplier;
                DBPU.KaiokenStrainDamageEnabled = configurations.KaiokenStrainDamageEnabled;

                DBPU.Beam_KnockbackMultiplier = configurations.Beam_KnockbackMultiplier;
                DBPU.Beam_SpeedMultiplier = configurations.Beam_SpeedMultiplier;
                DBPU.Beam_SizeMultiplier = configurations.Beam_SizeMultiplier;
                DBPU.Beam_ToggleDelete = configurations.Beam_ToggleDelete;
                DBPU.Beam_ToggleDestroy = configurations.Beam_ToggleDestroy;
                DBPU.Beam_ToggleHeat = configurations.Beam_ToggleHeat;
                DBPU.BeamAssistEnabled = configurations.BeamAssistEnabled;
                DBPU.BeamClashShockwaveEnabled = configurations.BeamClashShockwaveEnabled;

                DBPU.Beam_BurnAwayOnKill = configurations.Beam_BurnAwayOnKill;
                DBPU.KiDeflectEnabled = configurations.KiDeflectEnabled;
                DBPU.KiDeflectHybridPowerThreshold = configurations.KiDeflectHybridPowerThreshold;
                DBPU.KiDeflectBlastThresholdMultiplier = configurations.KiDeflectBlastThresholdMultiplier;
                DBPU.KiDeflectBeamThresholdMultiplier = configurations.KiDeflectBeamThresholdMultiplier;
                DBPU.KiDeflectBeamCatchThenAim = configurations.KiDeflectBeamCatchThenAim;
                DBPU.KiDeflectBeamCatchSeconds = Mathf.Max(0.05f, configurations.KiDeflectBeamCatchSeconds);
                DBPU.KiDeflectBeamSplitEnabled = configurations.KiDeflectBeamSplitEnabled;
                DBPU.KiDeflectBeamSplitThresholdMultiplier = Mathf.Max(0.05f, configurations.KiDeflectBeamSplitThresholdMultiplier);
                DBPU.KiDeflectBeamSplitRayCount = Mathf.Clamp(configurations.KiDeflectBeamSplitRayCount, 3, 24);
                DBPU.KiDeflectBeamSplitWrapDistance = Mathf.Max(0.1f, configurations.KiDeflectBeamSplitWrapDistance);
                DBPU.KiDeflectBeamSplitContinuationLength = Mathf.Max(0.2f, configurations.KiDeflectBeamSplitContinuationLength);
                DBPU.KiDeflectBeamSplitArcDegrees = Mathf.Clamp(configurations.KiDeflectBeamSplitArcDegrees, 30f, 300f);
                DBPU.KiDeflectBeamSplitLifetime = Mathf.Max(0.08f, configurations.KiDeflectBeamSplitLifetime);
                DBPU.SparkingOverloadSeconds = configurations.SparkingOverloadSeconds;
                DBPU.SparkingExhaustSeconds = configurations.SparkingExhaustSeconds;
                DBPU.SparkingCooldownSeconds = configurations.SparkingCooldownSeconds;
                DBPU.SparkingOverchargeHoldSeconds = configurations.SparkingOverchargeHoldSeconds;
                DBPU.SparkingEnabled = configurations.SparkingEnabled;
                DBPU.KiChargeSparkingOverchargeEnabled = configurations.KiChargeSparkingOverchargeEnabled;
                DBPU.KiOverdriveOutputEnabled = configurations.KiOverdriveOutputEnabled;
                DBPU.KiOverdriveMaxPercent = Mathf.Clamp(configurations.KiOverdriveMaxPercent, 101f, 250f);
                DBPU.KiOverdriveSecondsBeforeExhaust = Mathf.Max(0.35f, configurations.KiOverdriveSecondsBeforeExhaust);

                DBPU.Special_ExponentialGrowthSpeed = configurations.Special_ExponentialGrowthSpeed;
                DBPU.Special_ZenkaiBoostEnabled = configurations.Special_ZenkaiBoostEnabled;
                DBPU.Special_ZenkaiBoostScale = configurations.Special_ZenkaiBoostScale <= 0f ? 1f : configurations.Special_ZenkaiBoostScale;
                DBPU.Special_ZenkaiBoostMax = configurations.Special_ZenkaiBoostMax < 1f ? 10f : configurations.Special_ZenkaiBoostMax;

                DBPU.VoiceLines = configurations.VoiceLines;
                DBPU.VoiceLineVolume = configurations.VoiceLineVolume;
                DBPU.PowersVolume = configurations.PowersVolume;

                DBPU.KiChargeHoldRegenPerSecond = configurations.KiChargeHoldRegenPerSecond;
                DBPU.KiChargeAuraScaleMultiplier = configurations.KiChargeAuraScaleMultiplier;
                DBPU.KiChargeAuraParticleSpeedMultiplier = configurations.KiChargeAuraParticleSpeedMultiplier;
                DBPU.KiChargeDebrisMaxActive = configurations.KiChargeDebrisMaxActive;
                DBPU.KiChargeDebrisMaxSession = configurations.KiChargeDebrisMaxSession;
                DBPU.KiChargeDebrisSpawnInterval = configurations.KiChargeDebrisSpawnInterval;
                DBPU.KiChargeDebrisRingMin = configurations.KiChargeDebrisRingMin;
                DBPU.KiChargeDebrisRingMax = configurations.KiChargeDebrisRingMax;
                DBPU.KiChargeDebrisSpawnYOffset = configurations.KiChargeDebrisSpawnYOffset;
                DBPU.KiChargeFxEnabled = configurations.KiChargeFxEnabled;
                DBPU.KiChargeAutoTransformWhileChargingEnabled = configurations.KiChargeAutoTransformWhileChargingEnabled;
                DBPU.KiChargeAutoTransformSecondsPerTier = configurations.KiChargeAutoTransformSecondsPerTier > 0.5f
                    ? configurations.KiChargeAutoTransformSecondsPerTier
                    : 12f;
                DBPU.TransformationEyeGlowEnabled = configurations.TransformationEyeGlowEnabled;
                DBPU.TransformationHairGlowEnabled = configurations.TransformationHairGlowEnabled;
                DBPU.TransformationHairGlowBrightness = TransformationGlowUtility.MigrateLegacyPercent(
                    configurations.TransformationHairGlowBrightness, 100f);

                DBPU.QuickTransformEnabled = configurations.QuickTransformEnabled;
                DBPU.UiAuraScale = configurations.UiAuraScale > 0.05f ? configurations.UiAuraScale : 3.4f;
                DBPU.UiAuraYOffset = configurations.UiAuraYOffset;

                if (configurations.KiChargeHoldRegenPerSecond < 1f)
                    configurations.KiChargeHoldRegenPerSecond = 50000f;
                if (configurations.KiChargeAuraScaleMultiplier < 1f)
                    configurations.KiChargeAuraScaleMultiplier = 1.25f;
                if (configurations.KiChargeAuraParticleSpeedMultiplier < 1f)
                    configurations.KiChargeAuraParticleSpeedMultiplier = 1.35f;
                if (configurations.KiChargeDebrisMaxActive < 1)
                    configurations.KiChargeDebrisMaxActive = 28;
                if (configurations.KiChargeDebrisMaxSession < 1)
                    configurations.KiChargeDebrisMaxSession = 80;
                if (configurations.KiChargeDebrisSpawnInterval < 0.05f)
                    configurations.KiChargeDebrisSpawnInterval = 0.52f;
            }

            catch
            {
                Save();
            }
        }

        public static void Save()
        {
            if (configurations == null) configurations = new ModConfigs()
            {
                PowerMultiplier = 1f,
                KiDrainMultiplier = 0.5f,
                KiRegenMultiplier = 1f,
                KiAttackCostMultiplier = 1f,
                AurasEnabled = true,
                KiAtkCanDestroy = true,
                KiAtkCanDelete = true,
                ExtraCharge = true,
                KiBlast_ExplosionSizeMultiplier = 1f,
                KiBlast_CustomColorEnabled = false,
                KiBlast_CustomColorR = 1f,
                KiBlast_CustomColorG = 0.85f,
                KiBlast_CustomColorB = 0.3f,

                DamageMultiplier = 1f,
                HealthMultiplier = 1f,
                PhysAtkCanDestroy = true,
                PhysAtkStopLimbs = true,
                AutoEnablePunchKick = false,
                Universal_KnockbackMultiplier = 1f,
                DismembermentEnabled = true,

                StrainDrainMultiplier = 1f,
                StrainGainMultiplier = 1f,
                StrainDamageMultiplier = 1f,
                KaiokenStrainMultiplier = 1f,
                KaiokenMaxMultiplier = 20f,
                KaiokenStrainDamageEnabled = true,

                Beam_KnockbackMultiplier = 1f,
                Beam_SpeedMultiplier = 1f,
                Beam_SizeMultiplier = 1f,
                Beam_ToggleDelete = false,
                Beam_ToggleDestroy = true,
                Beam_ToggleHeat = true,
                BeamAssistEnabled = true,
                BeamClashShockwaveEnabled = false,

                Special_ExponentialGrowthSpeed = 1.005f,
                Special_ZenkaiBoostEnabled = false,
                Special_ZenkaiBoostScale = 1f,
                Special_ZenkaiBoostMax = 10f,

                VoiceLines = true,
                VoiceLineVolume = 1f,
                PowersVolume = 1f,

                KiChargeHoldRegenPerSecond = 50000f,
                KiChargeAuraScaleMultiplier = 1.25f,
                KiChargeAuraParticleSpeedMultiplier = 1.35f,
                KiChargeDebrisMaxActive = 28,
                KiChargeDebrisMaxSession = 80,
                KiChargeDebrisSpawnInterval = 0.52f,
                KiChargeDebrisRingMin = 0.35f,
                KiChargeDebrisRingMax = 1.1f,
                KiChargeDebrisSpawnYOffset = -1f,
                CinematicVfxRevision = 1,
                KiChargeFxEnabled = true,
                KiChargeAutoTransformWhileChargingEnabled = false,
                KiChargeAutoTransformSecondsPerTier = 12f,

                TransformationEyeGlowEnabled = true,

                TransformationHairGlowEnabled = true,
                TransformationHairGlowBrightness = 100f,

                QuickTransformEnabled = true
            };

            ModAPI.SerialiseJSON(configurations, Path);

            Load();
        }
    }
}
