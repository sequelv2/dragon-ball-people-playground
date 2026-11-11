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
    public class Configs : MonoBehaviour
    {
        static string OnOff(bool enabled) => enabled ? "<color=#55FF55>ON" : "<color=#FF5555>OFF";

        DbpuMenuButton Btn(string label, Action action) => new DbpuMenuButton(label, action);

        DbpuMenuSlider Slider(string label, Func<float> get, Action<float> set, float min = 0f, float max = 2f, bool percent = true)
            => new DbpuMenuSlider(label, get, set, min, max, percent);

        void OpenMenu(string title, params DbpuMenuButton[] buttons)
        {
            SaveConfigs();
            DbpuConfigMenuUI.Open(title, CloseConfigs, buttons);
        }

        void OpenMenu(string title, DbpuMenuSlider[] sliders, params DbpuMenuButton[] buttons)
        {
            SaveConfigs();
            DbpuConfigMenuUI.Open(title, CloseConfigs, buttons, sliders, SaveConfigs);
        }

        void CloseConfigs()
        {
            DbpuConfigMenuUI.Close();
            Destroy(gameObject);
        }

        void PromptFloat(float value, Action<float> apply, string title, string placeholder, Action reopen)
        {
            DbpuConfigMenuUI.Hide();
            Utils.OpenFloatInputDialog(value, gameObject.GetComponent<PhysicalBehaviour>(), delegate (PhysicalBehaviour phys, float v)
            {
                apply(v);
                reopen?.Invoke();
            }, title, placeholder);
        }

        void PromptInt(int value, Action<int> apply, string title, string placeholder, Action reopen)
        {
            DbpuConfigMenuUI.Hide();
            Utils.OpenIntInputDialog(value, gameObject.GetComponent<PhysicalBehaviour>(), delegate (PhysicalBehaviour phys, int v)
            {
                apply(v);
                reopen?.Invoke();
            }, title, placeholder);
        }

        private void ToggleGlobalAuras()
        {
            DBPU.AurasEnabled = !DBPU.AurasEnabled;

            ShowKiDialog();
        }

        private void ToggleExtraCharge()
        {
            DBPU.ExtraCharge = !DBPU.ExtraCharge;

            ShowKiDialog();
        }

        private void SetKiDrain()
        {
            PromptFloat(DBPU.KiDrainMultiplier, v => DBPU.KiDrainMultiplier = v, "Set Ki Drain", "Default: 0.5", ShowKiDialog);
        }

        private void SetKiRegen()
        {
            PromptFloat(DBPU.KiRegenMultiplier, v => DBPU.KiRegenMultiplier = v, "Set Ki Regen", "Default: 1", ShowKiDialog);
        }

        private void SetKiAttacksCost()
        {
            PromptFloat(DBPU.KiAttackCostMultiplier, v => DBPU.KiAttackCostMultiplier = v, "Set Ki Cost Multiplier", "Default: 1", ShowKiDialog);
        }

        private void ToggleKiDeflectEnabled()
        {
            DBPU.KiDeflectEnabled = !DBPU.KiDeflectEnabled;
            ShowKiDeflectDialog();
        }

        private void SetKiDeflectHybridThreshold()
        {
            PromptFloat(DBPU.KiDeflectHybridPowerThreshold, v => DBPU.KiDeflectHybridPowerThreshold = Mathf.Max(1f, v), "Set Deflect Hybrid Threshold", "Default: 1.35", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBlastThresholdMultiplier()
        {
            PromptFloat(DBPU.KiDeflectBlastThresholdMultiplier, v => DBPU.KiDeflectBlastThresholdMultiplier = Mathf.Max(1f, v), "Set Deflect Blast Threshold", "Default: 1", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamThresholdMultiplier()
        {
            PromptFloat(DBPU.KiDeflectBeamThresholdMultiplier, v => DBPU.KiDeflectBeamThresholdMultiplier = Mathf.Max(1f, v), "Set Deflect Beam Threshold", "Default: 1.2", ShowKiDeflectDialog);
        }

        private void ToggleKiDeflectCatchThenAim()
        {
            DBPU.KiDeflectBeamCatchThenAim = !DBPU.KiDeflectBeamCatchThenAim;
            ShowKiDeflectDialog();
        }

        private void SetKiDeflectCatchSeconds()
        {
            PromptFloat(DBPU.KiDeflectBeamCatchSeconds, v => DBPU.KiDeflectBeamCatchSeconds = Mathf.Max(0.05f, v), "Set Catch Window (seconds)", "Default: 0.22", ShowKiDeflectDialog);
        }

        private void ToggleKiDeflectBeamSplitEnabled()
        {
            DBPU.KiDeflectBeamSplitEnabled = !DBPU.KiDeflectBeamSplitEnabled;
            ShowKiDeflectDialog();
        }

        private void SetKiDeflectBeamSplitThresholdMultiplier()
        {
            PromptFloat(DBPU.KiDeflectBeamSplitThresholdMultiplier, v => DBPU.KiDeflectBeamSplitThresholdMultiplier = Mathf.Max(0.05f, v), "Beam Split Threshold Mul", "Default: 0.85", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamSplitRayCount()
        {
            PromptInt(DBPU.KiDeflectBeamSplitRayCount, v => DBPU.KiDeflectBeamSplitRayCount = Mathf.Clamp(v, 3, 24), "Beam Split Ray Count", "Default: 9 (3-24)", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamSplitWrapDistance()
        {
            PromptFloat(DBPU.KiDeflectBeamSplitWrapDistance, v => DBPU.KiDeflectBeamSplitWrapDistance = Mathf.Max(0.1f, v), "Beam Split Wrap Distance", "Default: 1.1", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamSplitContinuationLength()
        {
            PromptFloat(DBPU.KiDeflectBeamSplitContinuationLength, v => DBPU.KiDeflectBeamSplitContinuationLength = Mathf.Max(0.2f, v), "Beam Split Forward Length", "Default: 7", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamSplitArcDegrees()
        {
            PromptFloat(DBPU.KiDeflectBeamSplitArcDegrees, v => DBPU.KiDeflectBeamSplitArcDegrees = Mathf.Clamp(v, 30f, 300f), "Beam Split Arc Degrees", "Default: 150", ShowKiDeflectDialog);
        }

        private void SetKiDeflectBeamSplitLifetime()
        {
            PromptFloat(DBPU.KiDeflectBeamSplitLifetime, v => DBPU.KiDeflectBeamSplitLifetime = Mathf.Max(0.08f, v), "Beam Split VFX Lifetime", "Default: 0.42", ShowKiDeflectDialog);
        }

        private void KiDeflectReset()
        {
            DBPU.KiDeflectEnabled = true;
            DBPU.KiDeflectHybridPowerThreshold = 1.35f;
            DBPU.KiDeflectBlastThresholdMultiplier = 1f;
            DBPU.KiDeflectBeamThresholdMultiplier = 1.2f;
            DBPU.KiDeflectBeamCatchThenAim = false;
            DBPU.KiDeflectBeamCatchSeconds = 0.22f;
            DBPU.KiDeflectBeamSplitEnabled = true;
            DBPU.KiDeflectBeamSplitThresholdMultiplier = 0.85f;
            DBPU.KiDeflectBeamSplitRayCount = 9;
            DBPU.KiDeflectBeamSplitWrapDistance = 1.1f;
            DBPU.KiDeflectBeamSplitContinuationLength = 7f;
            DBPU.KiDeflectBeamSplitArcDegrees = 150f;
            DBPU.KiDeflectBeamSplitLifetime = 0.42f;
            ShowKiDeflectDialog();
        }

        private void KiReset()
        {
            DBPU.PowerMultiplier = 1f;
            DBPU.KiDrainMultiplier = 0.5f;
            DBPU.KiRegenMultiplier = 1f;
            DBPU.AurasEnabled = true;
            DBPU.KiAtkCanDestroy = true;
            DBPU.KiAtkCanDelete = false;
            DBPU.ExtraCharge = true;
            DBPU.KiBlast_ExplosionSizeMultiplier = 1f;
            DBPU.KiBlast_CustomColorEnabled = false;
            DBPU.KiBlast_CustomColorR = 1f;
            DBPU.KiBlast_CustomColorG = 0.85f;
            DBPU.KiBlast_CustomColorB = 0.3f;
            DBPU.QuickTransformEnabled = true;

            ShowKiDialog();
        }

        private void SetKiBlastExplosionSize()
        {
            PromptFloat(DBPU.KiBlast_ExplosionSizeMultiplier, v => DBPU.KiBlast_ExplosionSizeMultiplier = Mathf.Clamp(v, 0.1f, 20f), "Set Ki Explosion Size", "Default: 1 | Min: 0.1 | Max: 20", ShowKiDialog);
        }

        private void ToggleKiAtkCanDestroy()
        {
            DBPU.KiAtkCanDestroy = !DBPU.KiAtkCanDestroy;
            ShowKiDialog();
        }

        private void ToggleKiAtkCanDelete()
        {
            DBPU.KiAtkCanDelete = !DBPU.KiAtkCanDelete;
            ShowKiDialog();
        }

        private void ToggleKiBlastCustomColor()
        {
            DBPU.KiBlast_CustomColorEnabled = !DBPU.KiBlast_CustomColorEnabled;
            ShowKiDialog();
        }

        private void OpenKiBlastColorPicker()
        {
            ColorpickerDialogBehaviour picker = ColorpickerDialogBehaviour.Instance;
            if (picker == null)
            {
                DialogBoxManager.Notification("Color picker is not available right now.");
                return;
            }

            DbpuConfigMenuUI.Hide();

            Color current = new Color(DBPU.KiBlast_CustomColorR, DBPU.KiBlast_CustomColorG, DBPU.KiBlast_CustomColorB, 1f);
            if (picker.Title != null)
                picker.Title.text = "Ki Blast Color";
            if (picker.Description != null)
                picker.Description.text = "Pick a ki blast color and press Apply.";

            picker.OnApply.RemoveAllListeners();
            picker.OnCancel.RemoveAllListeners();
            picker.OnApply.AddListener(new UnityAction<Color>(color =>
            {
                DBPU.KiBlast_CustomColorR = Mathf.Clamp01(color.r);
                DBPU.KiBlast_CustomColorG = Mathf.Clamp01(color.g);
                DBPU.KiBlast_CustomColorB = Mathf.Clamp01(color.b);
                DBPU.KiBlast_CustomColorEnabled = true;
                SaveConfigs();
                ShowKiDialog();
            }));
            picker.OnCancel.AddListener(new UnityAction(() => ShowKiDialog()));
            picker.Open(current);
        }

        private void SetKiChargeHoldRegen()
        {
            PromptFloat(DBPU.KiChargeHoldRegenPerSecond, v => DBPU.KiChargeHoldRegenPerSecond = Mathf.Max(1f, v), "Set Ki Charge Regen / sec", "Default: 50000", ShowKiChargeRegenAuraDialog);
        }

        private void SetKiChargeAuraScale()
        {
            PromptFloat(DBPU.KiChargeAuraScaleMultiplier, v => DBPU.KiChargeAuraScaleMultiplier = Mathf.Max(0.1f, v), "Set Ki Charge Aura Scale", "Default: 1.25", ShowKiChargeRegenAuraDialog);
        }

        private void SetKiChargeAuraParticleSpeed()
        {
            PromptFloat(DBPU.KiChargeAuraParticleSpeedMultiplier, v => DBPU.KiChargeAuraParticleSpeedMultiplier = Mathf.Max(0.1f, v), "Set Ki Charge Aura Particle Speed", "Default: 1.35", ShowKiChargeRegenAuraDialog);
        }

        private void SetKiChargeDebrisMaxActive()
        {
            PromptFloat(DBPU.KiChargeDebrisMaxActive, v => DBPU.KiChargeDebrisMaxActive = Mathf.Max(1, Mathf.RoundToInt(v)), "Set Debris Max Active", "Default: 28", ShowKiChargeDebrisDialog);
        }

        private void SetKiChargeDebrisMaxSession()
        {
            PromptFloat(DBPU.KiChargeDebrisMaxSession, v => DBPU.KiChargeDebrisMaxSession = Mathf.Max(1, Mathf.RoundToInt(v)), "Set Debris Max Session", "Default: 80", ShowKiChargeDebrisDialog);
        }

        private void SetKiChargeDebrisSpawnInterval()
        {
            PromptFloat(DBPU.KiChargeDebrisSpawnInterval, v => DBPU.KiChargeDebrisSpawnInterval = Mathf.Max(0.1f, v), "Set Debris Spawn Interval", "Default: 0.52", ShowKiChargeDebrisDialog);
        }

        private void SetKiChargeDebrisRingMin()
        {
            PromptFloat(DBPU.KiChargeDebrisRingMin, v =>
            {
                DBPU.KiChargeDebrisRingMin = Mathf.Max(0.08f, v);
                DBPU.KiChargeDebrisRingMax = Mathf.Max(DBPU.KiChargeDebrisRingMin + 0.08f, DBPU.KiChargeDebrisRingMax);
            }, "Set Debris Ring Min", "Default: 0.35", ShowKiChargeDebrisDialog);
        }

        private void SetKiChargeDebrisRingMax()
        {
            PromptFloat(DBPU.KiChargeDebrisRingMax, v => DBPU.KiChargeDebrisRingMax = Mathf.Max(DBPU.KiChargeDebrisRingMin + 0.08f, v), "Set Debris Ring Max", "Default: 1.1", ShowKiChargeDebrisDialog);
        }

        private void SetKiChargeDebrisSpawnYOffset()
        {
            PromptFloat(DBPU.KiChargeDebrisSpawnYOffset, v => DBPU.KiChargeDebrisSpawnYOffset = v, "Set Debris Spawn Y Offset", "Default: -1", ShowKiChargeDebrisDialog);
        }

        private void SetSparkingOverloadSeconds()
        {
            PromptFloat(DBPU.SparkingOverloadSeconds, v => DBPU.SparkingOverloadSeconds = Mathf.Max(0f, v), "Set Sparking Overload Seconds", "Default: 180", ShowKiChargeSparkingDialog);
        }

        private void SetSparkingExhaustSeconds()
        {
            PromptFloat(DBPU.SparkingExhaustSeconds, v => DBPU.SparkingExhaustSeconds = Mathf.Max(0f, v), "Set Sparking Exhaust Seconds", "Default: 60", ShowKiChargeSparkingDialog);
        }

        private void SetSparkingCooldownSeconds()
        {
            PromptFloat(DBPU.SparkingCooldownSeconds, v => DBPU.SparkingCooldownSeconds = Mathf.Max(0f, v), "Set Sparking Cooldown Seconds", "Default: 0", ShowKiChargeSparkingDialog);
        }

        private void SetSparkingOverchargeHoldSeconds()
        {
            PromptFloat(DBPU.SparkingOverchargeHoldSeconds, v => DBPU.SparkingOverchargeHoldSeconds = Mathf.Max(0f, v), "Set Overcharge Hold Seconds", "Default: 3", ShowKiChargeSparkingDialog);
        }

        private void ToggleKiChargeSparkingOvercharge()
        {
            DBPU.KiChargeSparkingOverchargeEnabled = !DBPU.KiChargeSparkingOverchargeEnabled;
            ShowKiChargeSparkingDialog();
        }

        private void ToggleSparkingEnabled()
        {
            DBPU.SparkingEnabled = !DBPU.SparkingEnabled;
            ShowKiChargeSparkingDialog();
        }

        private void ToggleKiOverdriveOutputEnabled()
        {
            DBPU.KiOverdriveOutputEnabled = !DBPU.KiOverdriveOutputEnabled;
            ShowKiOverdriveDialog();
        }

        private void SetKiOverdriveMaxPercent()
        {
            PromptFloat(DBPU.KiOverdriveMaxPercent, v => DBPU.KiOverdriveMaxPercent = Mathf.Clamp(v, 101f, 250f), "Max Ki Output %", "Default: 200", ShowKiOverdriveDialog);
        }

        private void NotifyKeybindControlsHelp()
        {
            ModAPI.Notify("Change keybinds in Pause > Controls > Dragon Ball: Singularity");
            ShowKiChargeHubDialog();
        }

        private void SetKiOverdriveSecondsBeforeExhaust()
        {
            PromptFloat(DBPU.KiOverdriveSecondsBeforeExhaust, v => DBPU.KiOverdriveSecondsBeforeExhaust = Mathf.Max(0.35f, v), "Seconds Above 100% Before Sparking Exhaust", "Default: 2.75", ShowKiOverdriveDialog);
        }

        private void ToggleKiChargeFx()
        {
            DBPU.KiChargeFxEnabled = !DBPU.KiChargeFxEnabled;
            ShowKiChargeHubDialog();
        }

        private void ToggleKiChargeAutoTransformWhileCharging()
        {
            DBPU.KiChargeAutoTransformWhileChargingEnabled = !DBPU.KiChargeAutoTransformWhileChargingEnabled;
            ShowKiChargeTransformDialog();
        }

        private void SetKiChargeAutoTransformSecondsPerTier()
        {
            PromptFloat(DBPU.KiChargeAutoTransformSecondsPerTier, v => DBPU.KiChargeAutoTransformSecondsPerTier = Mathf.Max(0.5f, v), "Set Auto Transform Seconds / Tier", "Default: 12", ShowKiChargeTransformDialog);
        }

        private void KiChargeModeReset()
        {
            DBPU.KiChargeHoldRegenPerSecond = 50000f;
            DBPU.KiChargeAuraScaleMultiplier = 1.25f;
            DBPU.KiChargeAuraParticleSpeedMultiplier = 1.35f;
            DBPU.KiChargeDebrisMaxActive = 28;
            DBPU.KiChargeDebrisMaxSession = 80;
            DBPU.KiChargeDebrisSpawnInterval = 0.52f;
            DBPU.KiChargeDebrisRingMin = 0.35f;
            DBPU.KiChargeDebrisRingMax = 1.1f;
            DBPU.KiChargeDebrisSpawnYOffset = -1f;
            DBPU.KiChargeFxEnabled = true;
            DBPU.KiChargeAutoTransformWhileChargingEnabled = false;
            DBPU.KiChargeAutoTransformSecondsPerTier = 12f;
            DBPU.SparkingOverloadSeconds = 180f;
            DBPU.SparkingExhaustSeconds = 60f;
            DBPU.SparkingCooldownSeconds = 0f;
            DBPU.SparkingOverchargeHoldSeconds = 3f;
            DBPU.SparkingEnabled = true;
            DBPU.KiChargeSparkingOverchargeEnabled = true;
            DBPU.KiOverdriveOutputEnabled = true;
            DbpuInputRegistration.SetActionKey(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey);
            DBPU.KiOverdriveMaxPercent = 200f;
            DBPU.KiOverdriveSecondsBeforeExhaust = 2.75f;

            ShowKiChargeHubDialog();
        }

        private void ShowKiDeflectDialog()
        {
            OpenMenu("Ki Deflect Options",
                Btn("Deflect " + OnOff(DBPU.KiDeflectEnabled), ToggleKiDeflectEnabled),
                Btn("Hybrid Threshold", SetKiDeflectHybridThreshold),
                Btn("Blast Threshold Mul", SetKiDeflectBlastThresholdMultiplier),
                Btn("Beam Threshold Mul", SetKiDeflectBeamThresholdMultiplier),
                Btn("Catch Then Aim " + OnOff(DBPU.KiDeflectBeamCatchThenAim), ToggleKiDeflectCatchThenAim),
                Btn("Catch Window Seconds", SetKiDeflectCatchSeconds),
                Btn("Beam Split " + OnOff(DBPU.KiDeflectBeamSplitEnabled), ToggleKiDeflectBeamSplitEnabled),
                Btn("Split Threshold Mul", SetKiDeflectBeamSplitThresholdMultiplier),
                Btn("Split Ray Count", SetKiDeflectBeamSplitRayCount),
                Btn("Split Wrap Distance", SetKiDeflectBeamSplitWrapDistance),
                Btn("Split Forward Length", SetKiDeflectBeamSplitContinuationLength),
                Btn("Split Arc Degrees", SetKiDeflectBeamSplitArcDegrees),
                Btn("Split VFX Lifetime", SetKiDeflectBeamSplitLifetime),
                Btn("Reset to Default", KiDeflectReset),
                Btn("Back", ShowKiDialog));
        }

        private void ShowKiChargeHubDialog()
        {
            OpenMenu("Ki Charge & Sparking",
                Btn("Ki Charge Hold: " + DbpuInputRegistration.GetActionKeyLabel(DBPU.KiChargeHoldActionId, KeyCode.R), NotifyKeybindControlsHelp),
                Btn("Ki Output (head): " + DbpuInputRegistration.GetActionKeyLabel(CombatPowerController.ActionId, CombatPowerController.DefaultKey) + " / Shift reverses", NotifyKeybindControlsHelp),
                Btn("Ki Charge " + OnOff(DBPU.KiChargeFxEnabled), ToggleKiChargeFx),
                Btn("Regen & Aura", ShowKiChargeRegenAuraDialog),
                Btn("Debris", ShowKiChargeDebrisDialog),
                Btn("Sparking & Overload", ShowKiChargeSparkingDialog),
                Btn("Charge Transform", ShowKiChargeTransformDialog),
                Btn("Reset Ki Charge Defaults", KiChargeModeReset),
                Btn("Back", ShowKiDialog));
        }

        private void ShowKiChargeRegenAuraDialog()
        {
            OpenMenu("Ki Charge: Regen & Aura",
                Btn("Charge Regen / sec", SetKiChargeHoldRegen),
                Btn("Aura Scale", SetKiChargeAuraScale),
                Btn("Aura Particle Speed", SetKiChargeAuraParticleSpeed),
                Btn("Back", ShowKiChargeHubDialog));
        }

        private void ShowKiChargeDebrisDialog()
        {
            OpenMenu("Ki Charge: Debris",
                Btn("Max Active", SetKiChargeDebrisMaxActive),
                Btn("Max Session", SetKiChargeDebrisMaxSession),
                Btn("Spawn Interval", SetKiChargeDebrisSpawnInterval),
                Btn("Ring Min", SetKiChargeDebrisRingMin),
                Btn("Ring Max", SetKiChargeDebrisRingMax),
                Btn("Spawn Y Offset", SetKiChargeDebrisSpawnYOffset),
                Btn("Back", ShowKiChargeHubDialog));
        }

        private void ShowKiChargeTransformDialog()
        {
            OpenMenu("Ki Charge: Transform",
                Btn("Auto Transform " + OnOff(DBPU.KiChargeAutoTransformWhileChargingEnabled), ToggleKiChargeAutoTransformWhileCharging),
                Btn("Auto Transform Seconds / Tier", SetKiChargeAutoTransformSecondsPerTier),
                Btn("Back", ShowKiChargeHubDialog));
        }

        private void ShowKiChargeSparkingDialog()
        {
            OpenMenu("Ki Charge: Sparking",
                Btn("Sparking " + OnOff(DBPU.SparkingEnabled), ToggleSparkingEnabled),
                Btn("Overload Seconds", SetSparkingOverloadSeconds),
                Btn("Exhaust Seconds", SetSparkingExhaustSeconds),
                Btn("Cooldown Seconds", SetSparkingCooldownSeconds),
                Btn("Overcharge Hold Seconds", SetSparkingOverchargeHoldSeconds),
                Btn("Overcharge Trigger " + OnOff(DBPU.KiChargeSparkingOverchargeEnabled), ToggleKiChargeSparkingOvercharge),
                Btn("Back", ShowKiChargeHubDialog));
        }

        private void ShowKiOverdriveDialog()
        {
            OpenMenu("Ki Overdrive",
                Btn("Overdrive " + OnOff(DBPU.KiOverdriveOutputEnabled), ToggleKiOverdriveOutputEnabled),
                Btn("Hotkey: " + DbpuInputRegistration.GetActionKeyLabel(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey), NotifyKeybindControlsHelp),
                Btn("Max Output % (100-250)", SetKiOverdriveMaxPercent),
                Btn("Seconds Before Exhaust", SetKiOverdriveSecondsBeforeExhaust),
                Btn("Back", ShowKiChargeHubDialog));
        }


        private void SetStrainGain()
        {
            PromptFloat(DBPU.StrainGainMultiplier, v => DBPU.StrainGainMultiplier = v, "Set Strain Gain", "Default: 1", ShowStrainDialog);
        }

        private void SetStrainDrain()
        {
            PromptFloat(DBPU.StrainDrainMultiplier, v => DBPU.StrainDrainMultiplier = v, "Set Strain Drain", "Default: 1", ShowStrainDialog);
        }

        private void SetStrainDamage()
        {
            PromptFloat(DBPU.StrainDamageMultiplier, v => DBPU.StrainDamageMultiplier = v, "Set Strain Damage", "Default: 1", ShowStrainDialog);
        }

        private void SetKaiokenStrainMultiplier()
        {
            PromptFloat(DBPU.KaiokenStrainMultiplier, v => DBPU.KaiokenStrainMultiplier = Mathf.Max(v, 0f), "Set Kaioken Strain Multiplier", "Default: 1", ShowStrainDialog);
        }

        private void SetKaiokenMaxMultiplier()
        {
            PromptFloat(DBPU.KaiokenMaxMultiplier, v => DBPU.KaiokenMaxMultiplier = Mathf.Max(v, 1f), "Set Kaioken Max Multiplier", "Default: 20", ShowStrainDialog);
        }

        private void ToggleKaiokenStrainDamage()
        {
            DBPU.KaiokenStrainDamageEnabled = !DBPU.KaiokenStrainDamageEnabled;
            ShowStrainDialog();
        }

        private void StrainReset()
        {
            DBPU.StrainDrainMultiplier = 1f;
            DBPU.StrainGainMultiplier = 1f;
            DBPU.StrainDamageMultiplier = 1f;
            DBPU.KaiokenStrainMultiplier = 1f;
            DBPU.KaiokenMaxMultiplier = 20f;
            DBPU.KaiokenStrainDamageEnabled = true;

            ShowStrainDialog();
        }


        private void SetHealth()
        {
            PromptFloat(DBPU.HealthMultiplier, v => DBPU.HealthMultiplier = v, "Set Health Multiplier", "Default: 1", ShowPhysDialog);
        }

        private void SetDamage()
        {
            PromptFloat(DBPU.DamageMultiplier, v => DBPU.DamageMultiplier = v, "Set Damage Multiplier", "Default: 1", ShowPhysDialog);
        }

        private void ToggleDismemberment()
        {
            DBPU.DismembermentEnabled = !DBPU.DismembermentEnabled;

            ShowPhysDialog();
        }

        private void ToggleImpactStop()
        {
            DBPU.PhysAtkStopLimbs = !DBPU.PhysAtkStopLimbs;

            ShowPhysDialog();
        }

        private void TogglePhysCanDestroy()
        {
            DBPU.PhysAtkCanDestroy = !DBPU.PhysAtkCanDestroy;

            ShowPhysDialog();
        }

        private void ToggleAutoEnablePunchKick()
        {
            DBPU.AutoEnablePunchKick = !DBPU.AutoEnablePunchKick;
            if (DBPU.AutoEnablePunchKick)
                PowerSetup.ApplyAutoEnablePunchKickToAllAlive();
            ShowPhysDialog();
        }

        private void PhysReset()
        {
            DBPU.DamageMultiplier = 1f;
            DBPU.HealthMultiplier = 1f;
            DBPU.PhysAtkCanDestroy = true;
            DBPU.PhysAtkStopLimbs = true;
            DBPU.AutoEnablePunchKick = false;
            DBPU.Universal_KnockbackMultiplier = 1f;
            DBPU.DismembermentEnabled = true;

            ShowPhysDialog();
        }


        private void SetBeamKnockback()
        {
            PromptFloat(DBPU.Beam_KnockbackMultiplier, v => DBPU.Beam_KnockbackMultiplier = v, "Set Beam Force Multiplier", "Default: 1", ShowBeamDialog);
        }

        private void SetBeamSpeed()
        {
            PromptFloat(DBPU.Beam_SpeedMultiplier, v => DBPU.Beam_SpeedMultiplier = v, "Set Beam Speed Multiplier", "Default: 1", ShowBeamDialog);
        }

        private void SetBeamSize()
        {
            PromptFloat(DBPU.Beam_SizeMultiplier, v => DBPU.Beam_SizeMultiplier = v, "Set Beam Size Multiplier", "Default: 1", ShowBeamDialog);
        }

        private void ToggleBeamDelete()
        {
            DBPU.Beam_ToggleDelete = !DBPU.Beam_ToggleDelete;

            ShowBeamDialog();
        }

        private void ToggleBeamDestroy()
        {
            DBPU.Beam_ToggleDestroy = !DBPU.Beam_ToggleDestroy;

            ShowBeamDialog();
        }

        private void ToggleBeamHeat()
        {
            DBPU.Beam_ToggleHeat = !DBPU.Beam_ToggleHeat;

            ShowBeamDialog();
        }

        private void ToggleBeamAssist()
        {
            DBPU.BeamAssistEnabled = !DBPU.BeamAssistEnabled;

            ShowBeamDialog();
        }

        private void ToggleBeamClashShockwave()
        {
            DBPU.BeamClashShockwaveEnabled = !DBPU.BeamClashShockwaveEnabled;

            ShowBeamDialog();
        }

        private void BeamReset()
        {
            DBPU.Beam_KnockbackMultiplier = 1f;
            DBPU.Beam_SpeedMultiplier = 1f;
            DBPU.Beam_SizeMultiplier = 1f;
            DBPU.Beam_ToggleDelete = false;
            DBPU.Beam_ToggleDestroy = true;
            DBPU.Beam_ToggleHeat = false;
            DBPU.BeamAssistEnabled = true;
            DBPU.BeamClashShockwaveEnabled = false;

            ShowBeamDialog();
        }


        private void SetExponentialGrowth()
        {
            PromptFloat((DBPU.Special_ExponentialGrowthSpeed - 1f) * 100f, v => DBPU.Special_ExponentialGrowthSpeed = 1f + (Mathf.Clamp(v, 0.1f, 5f) / 100f), "Set Exponential Growth %", "Min: 0.1 | Max: 5", ShowSpecialDialog);
        }

        private void ToggleZenkaiBoost()
        {
            DBPU.Special_ZenkaiBoostEnabled = !DBPU.Special_ZenkaiBoostEnabled;
            ShowSpecialDialog();
        }

        private void SetZenkaiBoostScale()
        {
            PromptFloat(DBPU.Special_ZenkaiBoostScale, v => DBPU.Special_ZenkaiBoostScale = Mathf.Max(0.01f, v), "Set Zenkai Boost Scale", "Damage fraction of max health * scale is added to power. Default: 1", ShowSpecialDialog);
        }

        private void SetZenkaiBoostMax()
        {
            PromptFloat(DBPU.Special_ZenkaiBoostMax, v => DBPU.Special_ZenkaiBoostMax = Mathf.Max(1f, v), "Set Zenkai Boost Max", "Max BoostMultiplier. Default: 10", ShowSpecialDialog);
        }

        private void SpecialReset()
        {
            DBPU.Special_ExponentialGrowthSpeed = 1.005f;
            DBPU.Special_ZenkaiBoostEnabled = false;
            DBPU.Special_ZenkaiBoostScale = 1f;
            DBPU.Special_ZenkaiBoostMax = 10f;

            ShowSpecialDialog();
        }


        private void ToggleVoiceLines()
        {
            DBPU.VoiceLines = !DBPU.VoiceLines;

            ShowMiscDialog();
        }

        private void ToggleTransformationEyeGlow()
        {
            DBPU.TransformationEyeGlowEnabled = !DBPU.TransformationEyeGlowEnabled;
            TransformationEyeGlow.RefreshAll();
            ShowMiscDialog();
        }

        private void ToggleTransformationHairGlow()
        {
            DBPU.TransformationHairGlowEnabled = !DBPU.TransformationHairGlowEnabled;
            TransformationHairGlow.RefreshAll();
            ShowMiscDialog();
        }

        private void SetTransformationHairGlowBrightness()
        {
            PromptFloat(DBPU.TransformationHairGlowBrightness, v =>
            {
                DBPU.TransformationHairGlowBrightness = Mathf.Clamp(v, 0f, TransformationGlowUtility.MaxBrightnessPercent);
                TransformationEyeGlow.RefreshAll();
                TransformationHairGlow.RefreshAll();
            }, "Set Glow Brightness %", "100 = old 1% | Default: 100 | Max: 5000", ShowMiscDialog);
        }

        private void SetVLVolume()
        {
            PromptFloat(DBPU.VoiceLineVolume * 100f, v => DBPU.VoiceLineVolume = Mathf.Clamp(v / 100f, 0f, 1f), "Set Voice Line Volume %", "Min: 0 | Max: 1", ShowMiscDialog);
        }

        private void SetPowVolume()
        {
            PromptFloat(DBPU.PowersVolume * 100f, v => DBPU.PowersVolume = Mathf.Clamp(v / 100f, 0f, 1f), "Set Powers Volume %", "Min: 0 | Max: 1", ShowMiscDialog);
        }

        private void ToggleQuickTransform()
        {
            DBPU.QuickTransformEnabled = !DBPU.QuickTransformEnabled;
            ShowKiDialog();
        }

        private void MiscReset()
        {
            DBPU.VoiceLines = true;
            DBPU.VoiceLineVolume = 1f;
            DBPU.PowersVolume = 1f;
            DBPU.TransformationEyeGlowEnabled = true;
            DBPU.TransformationHairGlowEnabled = true;
            DBPU.TransformationHairGlowBrightness = 100f;
            DBPU.UiAuraScale = 3.4f;
            DBPU.UiAuraYOffset = -0.09f;
            TransformationEyeGlow.RefreshAll();
            TransformationHairGlow.RefreshAll();

            ShowMiscDialog();
        }


        private void ShowPhysDialog()
        {
            OpenMenu("Physical Options",
                new[]
                {
                    Slider("Knockback", () => DBPU.Universal_KnockbackMultiplier, v => DBPU.Universal_KnockbackMultiplier = Mathf.Clamp(v, 0f, 2f), 0f, 2f, true)
                },
                Btn("Health Multiplier", SetHealth),
                Btn("Damage Multiplier", SetDamage),
                Btn("Auto Punch/Kick " + OnOff(DBPU.AutoEnablePunchKick), ToggleAutoEnablePunchKick),
                Btn("Can Destroy " + OnOff(DBPU.PhysAtkCanDestroy), TogglePhysCanDestroy),
                Btn("Impact Stop " + OnOff(DBPU.PhysAtkStopLimbs), ToggleImpactStop),
                Btn("Dismemberment " + OnOff(DBPU.DismembermentEnabled), ToggleDismemberment),
                Btn("Reset to Default", PhysReset),
                Btn("Back", ShowDialog));
        }

        private void ShowKiDialog()
        {
            OpenMenu("Ki Options",
                Btn("Auras " + OnOff(DBPU.AurasEnabled), ToggleGlobalAuras),
                Btn("Tune Character Aura", BeginTuneCharacterAura),
                Btn("Extra Charge " + OnOff(DBPU.ExtraCharge), ToggleExtraCharge),
                Btn("Ki Drain", SetKiDrain),
                Btn("Ki Regen", SetKiRegen),
                Btn("Ki Cost", SetKiAttacksCost),
                Btn("Explosion Size", SetKiBlastExplosionSize),
                Btn("Can Destroy " + OnOff(DBPU.KiAtkCanDestroy), ToggleKiAtkCanDestroy),
                Btn("Can Delete " + OnOff(DBPU.KiAtkCanDelete), ToggleKiAtkCanDelete),
                Btn("Custom Ki Color " + OnOff(DBPU.KiBlast_CustomColorEnabled), ToggleKiBlastCustomColor),
                Btn("Pick Ki Color", OpenKiBlastColorPicker),
                Btn("Ki Deflect", ShowKiDeflectDialog),
                Btn("Ki Charge & Sparking", ShowKiChargeHubDialog),
                Btn("Quick Transform " + OnOff(DBPU.QuickTransformEnabled), ToggleQuickTransform),
                Btn("Reset to Default", KiReset),
                Btn("Back", ShowDialog));
        }

        private void ShowStrainDialog()
        {
            OpenMenu("Strain Options",
                Btn("Strain Gain", SetStrainGain),
                Btn("Strain Drain", SetStrainDrain),
                Btn("Strain Damage", SetStrainDamage),
                Btn("Kaioken Strain", SetKaiokenStrainMultiplier),
                Btn("Kaioken Max Multiplier", SetKaiokenMaxMultiplier),
                Btn("Kaioken Strain Damage " + OnOff(DBPU.KaiokenStrainDamageEnabled), ToggleKaiokenStrainDamage),
                Btn("Reset to Default", StrainReset),
                Btn("Back", ShowDialog));
        }

        private void ShowBeamDialog()
        {
            OpenMenu("Beams Options",
                Btn("Beam Force", SetBeamKnockback),
                Btn("Beam Speed", SetBeamSpeed),
                Btn("Beam Size", SetBeamSize),
                Btn("Can Delete " + OnOff(DBPU.Beam_ToggleDelete), ToggleBeamDelete),
                Btn("Can Destroy " + OnOff(DBPU.Beam_ToggleDestroy), ToggleBeamDestroy),
                Btn("Can Burn " + OnOff(DBPU.Beam_ToggleHeat), ToggleBeamHeat),
                Btn("Beam Assist " + OnOff(DBPU.BeamAssistEnabled), ToggleBeamAssist),
                Btn("Clash Shockwave " + OnOff(DBPU.BeamClashShockwaveEnabled), ToggleBeamClashShockwave),
                Btn("Reset to Default", BeamReset),
                Btn("Back", ShowDialog));
        }

        private void ShowSpecialDialog()
        {
            OpenMenu("Special Features Options",
                Btn("Exponential Growth", SetExponentialGrowth),
                Btn("Zenkai Boost " + OnOff(DBPU.Special_ZenkaiBoostEnabled), ToggleZenkaiBoost),
                Btn("Zenkai Scale", SetZenkaiBoostScale),
                Btn("Zenkai Max", SetZenkaiBoostMax),
                Btn("Reset to Default", SpecialReset),
                Btn("Back", ShowDialog));
        }

        private void ShowMiscDialog()
        {
            OpenMenu("Misc Options",
                Btn("Voice Lines " + OnOff(DBPU.VoiceLines), ToggleVoiceLines),
                Btn("Glowing Eyes " + OnOff(DBPU.TransformationEyeGlowEnabled), ToggleTransformationEyeGlow),
                Btn("Glowing Hair " + OnOff(DBPU.TransformationHairGlowEnabled), ToggleTransformationHairGlow),
                Btn("Glow Brightness", SetTransformationHairGlowBrightness),
                Btn("Voice Lines Volume", SetVLVolume),
                Btn("Ki Powers Volume", SetPowVolume),
                Btn("Change Power Level", BeginChangePowerLevel),
                Btn("Tune Character Aura", BeginTuneCharacterAura),
                Btn("Reset Character Powers", ResetAllCharacterPowers),
                Btn("Reset to Default", MiscReset),
                Btn("Back", ShowDialog));
        }

        private void ShowDialog()
        {
            OpenMenu("Mod Configurations",
                Btn("Physical Configs", ShowPhysDialog),
                Btn("Ki Configs", ShowKiDialog),
                Btn("Strain Configs", ShowStrainDialog),
                Btn("Beam Configs", ShowBeamDialog),
                Btn("Special Configs", ShowSpecialDialog),
                Btn("Misc Configs", ShowMiscDialog),
                Btn("Singularity Features", ShowSingularityDialog));
        }

        private void ShowSingularityDialog()
        {
            OpenMenu("Singularity Features", Singularity.MenuButtons(ShowSingularityDialog, ShowDialog));
        }

        private void BeginChangePowerLevel()
        {
            PowerGiverState.Disarm();
            AuraTunePickerState.Disarm();
            CharacterPowerPickerState.Arm();
            CharacterPowerPickerInput.Ensure();
            DbpuConfigMenuUI.Close();
            Destroy(gameObject);
        }

        private void BeginTuneCharacterAura()
        {
            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            AuraTunePickerState.Arm();
            AuraTunePickerInput.Ensure();
            DbpuConfigMenuUI.Close();
            Destroy(gameObject);
            ModAPI.Notify("Click a character to tune their aura.");
        }

        private void ResetAllCharacterPowers()
        {
            CharacterPowerCatalog.ResetAll();
            ShowMiscDialog();
        }

        private void SaveConfigs()
        {
            ModConfigs.configurations = new ModConfigs()
            {
                PowerMultiplier = DBPU.PowerMultiplier,
                KiDrainMultiplier = DBPU.KiDrainMultiplier,
                KiRegenMultiplier = DBPU.KiRegenMultiplier,
                KiAttackCostMultiplier = DBPU.KiAttackCostMultiplier,
                AurasEnabled = DBPU.AurasEnabled,
                KiAtkCanDestroy = DBPU.KiAtkCanDestroy,
                KiAtkCanDelete = DBPU.KiAtkCanDelete,
                ExtraCharge = DBPU.ExtraCharge,
                KiBlast_ExplosionSizeMultiplier = DBPU.KiBlast_ExplosionSizeMultiplier,
                KiBlast_CustomColorEnabled = DBPU.KiBlast_CustomColorEnabled,
                KiBlast_CustomColorR = DBPU.KiBlast_CustomColorR,
                KiBlast_CustomColorG = DBPU.KiBlast_CustomColorG,
                KiBlast_CustomColorB = DBPU.KiBlast_CustomColorB,

                DamageMultiplier = DBPU.DamageMultiplier,
                HealthMultiplier = DBPU.HealthMultiplier,
                PhysAtkCanDestroy = DBPU.PhysAtkCanDestroy,
                PhysAtkStopLimbs = DBPU.PhysAtkStopLimbs,
                AutoEnablePunchKick = DBPU.AutoEnablePunchKick,
                Universal_KnockbackMultiplier = DBPU.Universal_KnockbackMultiplier,
                DismembermentEnabled = DBPU.DismembermentEnabled,

                StrainDrainMultiplier = DBPU.StrainDrainMultiplier,
                StrainGainMultiplier = DBPU.StrainGainMultiplier,
                StrainDamageMultiplier = DBPU.StrainDamageMultiplier,
                KaiokenStrainMultiplier = DBPU.KaiokenStrainMultiplier,
                KaiokenMaxMultiplier = DBPU.KaiokenMaxMultiplier,
                KaiokenStrainDamageEnabled = DBPU.KaiokenStrainDamageEnabled,

                Beam_KnockbackMultiplier = DBPU.Beam_KnockbackMultiplier,
                Beam_SpeedMultiplier = DBPU.Beam_SpeedMultiplier,
                Beam_SizeMultiplier = DBPU.Beam_SizeMultiplier,
                Beam_ToggleDelete = DBPU.Beam_ToggleDelete,
                Beam_ToggleDestroy = DBPU.Beam_ToggleDestroy,
                Beam_ToggleHeat = DBPU.Beam_ToggleHeat,
                BeamAssistEnabled = DBPU.BeamAssistEnabled,
                BeamClashShockwaveEnabled = DBPU.BeamClashShockwaveEnabled,

                KiDeflectEnabled = DBPU.KiDeflectEnabled,
                KiDeflectHybridPowerThreshold = DBPU.KiDeflectHybridPowerThreshold,
                KiDeflectBlastThresholdMultiplier = DBPU.KiDeflectBlastThresholdMultiplier,
                KiDeflectBeamThresholdMultiplier = DBPU.KiDeflectBeamThresholdMultiplier,
                KiDeflectBeamCatchThenAim = DBPU.KiDeflectBeamCatchThenAim,
                KiDeflectBeamCatchSeconds = DBPU.KiDeflectBeamCatchSeconds,
                KiDeflectBeamSplitEnabled = DBPU.KiDeflectBeamSplitEnabled,
                KiDeflectBeamSplitThresholdMultiplier = DBPU.KiDeflectBeamSplitThresholdMultiplier,
                KiDeflectBeamSplitRayCount = DBPU.KiDeflectBeamSplitRayCount,
                KiDeflectBeamSplitWrapDistance = DBPU.KiDeflectBeamSplitWrapDistance,
                KiDeflectBeamSplitContinuationLength = DBPU.KiDeflectBeamSplitContinuationLength,
                KiDeflectBeamSplitArcDegrees = DBPU.KiDeflectBeamSplitArcDegrees,
                KiDeflectBeamSplitLifetime = DBPU.KiDeflectBeamSplitLifetime,

                Special_ExponentialGrowthSpeed = DBPU.Special_ExponentialGrowthSpeed,
                Special_ZenkaiBoostEnabled = DBPU.Special_ZenkaiBoostEnabled,
                Special_ZenkaiBoostScale = DBPU.Special_ZenkaiBoostScale,
                Special_ZenkaiBoostMax = DBPU.Special_ZenkaiBoostMax,

                VoiceLines = DBPU.VoiceLines,
                VoiceLineVolume = DBPU.VoiceLineVolume,
                PowersVolume = DBPU.PowersVolume,

                KiChargeHoldRegenPerSecond = DBPU.KiChargeHoldRegenPerSecond,
                KiChargeAuraScaleMultiplier = DBPU.KiChargeAuraScaleMultiplier,
                KiChargeAuraParticleSpeedMultiplier = DBPU.KiChargeAuraParticleSpeedMultiplier,
                KiChargeDebrisMaxActive = DBPU.KiChargeDebrisMaxActive,
                KiChargeDebrisMaxSession = DBPU.KiChargeDebrisMaxSession,
                KiChargeDebrisSpawnInterval = DBPU.KiChargeDebrisSpawnInterval,
                KiChargeDebrisRingMin = DBPU.KiChargeDebrisRingMin,
                KiChargeDebrisRingMax = DBPU.KiChargeDebrisRingMax,
                KiChargeDebrisSpawnYOffset = DBPU.KiChargeDebrisSpawnYOffset,
                CinematicVfxRevision = 1,
                KiChargeFxEnabled = DBPU.KiChargeFxEnabled,
                KiChargeAutoTransformWhileChargingEnabled = DBPU.KiChargeAutoTransformWhileChargingEnabled,
                KiChargeAutoTransformSecondsPerTier = DBPU.KiChargeAutoTransformSecondsPerTier,

                SparkingOverloadSeconds = DBPU.SparkingOverloadSeconds,
                SparkingExhaustSeconds = DBPU.SparkingExhaustSeconds,
                SparkingCooldownSeconds = DBPU.SparkingCooldownSeconds,
                SparkingOverchargeHoldSeconds = DBPU.SparkingOverchargeHoldSeconds,
                SparkingEnabled = DBPU.SparkingEnabled,
                KiChargeSparkingOverchargeEnabled = DBPU.KiChargeSparkingOverchargeEnabled,

                KiOverdriveOutputEnabled = DBPU.KiOverdriveOutputEnabled,
                KiOverdriveOutputKey = DbpuInputRegistration.GetActionKey(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey),
                KiChargeHoldKey = DbpuInputRegistration.GetActionKey(DBPU.KiChargeHoldActionId, KeyCode.R),
                KiOverdriveMaxPercent = DBPU.KiOverdriveMaxPercent,
                KiOverdriveSecondsBeforeExhaust = DBPU.KiOverdriveSecondsBeforeExhaust,

                TransformationEyeGlowEnabled = DBPU.TransformationEyeGlowEnabled,

                TransformationHairGlowEnabled = DBPU.TransformationHairGlowEnabled,
                TransformationHairGlowBrightness = DBPU.TransformationHairGlowBrightness,

                QuickTransformEnabled = DBPU.QuickTransformEnabled,
                UiAuraScale = DBPU.UiAuraScale,
                UiAuraYOffset = DBPU.UiAuraYOffset
            };

            ModConfigs.Save();
        }

        private void Awake()
        {
            ShowDialog();
        }

        private void OnDestroy()
        {
            DbpuConfigMenuUI.Close();
        }
    }
}
