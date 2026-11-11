using Mod;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DBPU
{
    // The Mod Configurations screen, written as data: each row names its value, range and default.
    public class Configs : MonoBehaviour
    {
        ConfigsWindow window;

        static CfgRow Toggle(string label, string hint, Func<bool> get, Action<bool> set) => CfgRow.Toggle(label, hint, get, set);

        static CfgRow Num(string label, string hint, Func<float> get, Action<float> set, float min, float max, float defaultValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, bool whole = false, bool log = false)
            => CfgRow.Number(label, hint, get, set, min, max, defaultValue, hardMin, hardMax, false, whole, log);

        static CfgRow Pct(string label, string hint, Func<float> get, Action<float> set, float max, float defaultValue)
            => CfgRow.Number(label, hint, get, set, 0f, max, defaultValue, 0f, max, true);

        List<CfgPage> BuildPages()
        {
            var pages = new List<CfgPage>
            {
                PhysicalPage(), KiPage(), DeflectPage(), ChargePage(), StrainPage(), BeamPage(), SpecialPage(), MiscPage(), FeaturesPage()
            };
            return pages;
        }

        CfgPage PhysicalPage()
        {
            return new CfgPage("Physical", DbAtlas.DragonBall[1], "Punches, kicks and how hard fighters are to break.", PhysReset).Add(
                Pct("Knockback", "How far physical hits send a target.", () => DBPU.Universal_KnockbackMultiplier, v => DBPU.Universal_KnockbackMultiplier = v, 2f, 1f),
                Num("Health Multiplier", "Multiplies the health of every fighter.", () => DBPU.HealthMultiplier, v => DBPU.HealthMultiplier = v, 0f, 10f, 1f, 0f),
                Num("Damage Multiplier", "Multiplies the damage of physical attacks.", () => DBPU.DamageMultiplier, v => DBPU.DamageMultiplier = v, 0f, 10f, 1f, 0f),
                Toggle("Auto Punch / Kick", "Fighters start with Punch Power and Kick Power switched on.", () => DBPU.AutoEnablePunchKick, v =>
                {
                    DBPU.AutoEnablePunchKick = v;
                    if (v) PowerSetup.ApplyAutoEnablePunchKickToAllAlive();
                }),
                Toggle("Can Destroy", "Physical hits can destroy objects.", () => DBPU.PhysAtkCanDestroy, v => DBPU.PhysAtkCanDestroy = v),
                Toggle("Impact Stop", "Limbs stop for a moment when a heavy hit lands.", () => DBPU.PhysAtkStopLimbs, v => DBPU.PhysAtkStopLimbs = v),
                Toggle("Dismemberment", "Strong hits can sever limbs.", () => DBPU.DismembermentEnabled, v => DBPU.DismembermentEnabled = v));
        }

        CfgPage KiPage()
        {
            return new CfgPage("Ki", DbAtlas.DragonBall[2], "Energy costs, ki attacks and auras.", KiReset).Add(
                Toggle("Auras", "Show transformation auras.", () => DBPU.AurasEnabled, v => DBPU.AurasEnabled = v),
                CfgRow.Button("Tune Character Aura", "Close this menu, then click a character to adjust their aura size and position.", BeginTuneCharacterAura),
                Toggle("Extra Charge", "Charging can push a ki attack past its normal maximum.", () => DBPU.ExtraCharge, v => DBPU.ExtraCharge = v),
                Toggle("Quick Transform", "Allow the quick-transform key.", () => DBPU.QuickTransformEnabled, v => DBPU.QuickTransformEnabled = v),
                Num("Ki Drain", "How fast active forms use up ki.", () => DBPU.KiDrainMultiplier, v => DBPU.KiDrainMultiplier = v, 0f, 5f, .5f, 0f),
                Num("Ki Regen", "How fast ki comes back on its own.", () => DBPU.KiRegenMultiplier, v => DBPU.KiRegenMultiplier = v, 0f, 5f, 1f, 0f),
                Num("Ki Cost", "How much ki each attack uses.", () => DBPU.KiAttackCostMultiplier, v => DBPU.KiAttackCostMultiplier = v, 0f, 5f, 1f, 0f),
                Num("Explosion Size", "Size of ki blast explosions.", () => DBPU.KiBlast_ExplosionSizeMultiplier, v => DBPU.KiBlast_ExplosionSizeMultiplier = v, .1f, 20f, 1f, .1f, 20f),
                Toggle("Can Destroy", "Ki attacks can destroy objects.", () => DBPU.KiAtkCanDestroy, v => DBPU.KiAtkCanDestroy = v),
                Toggle("Can Delete", "Ki attacks can erase objects outright.", () => DBPU.KiAtkCanDelete, v => DBPU.KiAtkCanDelete = v),
                CfgRow.Section("Ki colour"),
                Toggle("Custom Ki Color", "Use your own colour for ki blasts.", () => DBPU.KiBlast_CustomColorEnabled, v => DBPU.KiBlast_CustomColorEnabled = v),
                CfgRow.Button("Pick Ki Color", "Open the colour picker for ki blasts.", OpenKiBlastColorPicker));
        }

        CfgPage DeflectPage()
        {
            return new CfgPage("Ki Deflect", DbAtlas.DragonBall[3], "Swatting away blasts and beams.", KiDeflectReset).Add(
                Toggle("Deflect", "Strong fighters can knock ki attacks aside.", () => DBPU.KiDeflectEnabled, v => DBPU.KiDeflectEnabled = v),
                Num("Hybrid Threshold", "Power ratio at which a deflect turns into a full redirect.", () => DBPU.KiDeflectHybridPowerThreshold, v => DBPU.KiDeflectHybridPowerThreshold = v, 1f, 5f, 1.35f, 1f),
                Num("Blast Threshold", "Power needed to deflect a blast.", () => DBPU.KiDeflectBlastThresholdMultiplier, v => DBPU.KiDeflectBlastThresholdMultiplier = v, 1f, 5f, 1f, 1f),
                Num("Beam Threshold", "Power needed to deflect a beam.", () => DBPU.KiDeflectBeamThresholdMultiplier, v => DBPU.KiDeflectBeamThresholdMultiplier = v, 1f, 5f, 1.2f, 1f),
                CfgRow.Section("Beam catch"),
                Toggle("Catch Then Aim", "Hold a caught beam briefly before sending it back.", () => DBPU.KiDeflectBeamCatchThenAim, v => DBPU.KiDeflectBeamCatchThenAim = v),
                Num("Catch Window", "How long a beam stays caught, in seconds.", () => DBPU.KiDeflectBeamCatchSeconds, v => DBPU.KiDeflectBeamCatchSeconds = v, .05f, 2f, .22f, .05f),
                CfgRow.Section("Beam split"),
                Toggle("Beam Split", "A much stronger fighter can split a beam apart.", () => DBPU.KiDeflectBeamSplitEnabled, v => DBPU.KiDeflectBeamSplitEnabled = v),
                Num("Split Threshold", "Power needed to split a beam.", () => DBPU.KiDeflectBeamSplitThresholdMultiplier, v => DBPU.KiDeflectBeamSplitThresholdMultiplier = v, .05f, 5f, .85f, .05f),
                Num("Ray Count", "Number of rays a split beam breaks into.", () => DBPU.KiDeflectBeamSplitRayCount, v => DBPU.KiDeflectBeamSplitRayCount = Mathf.RoundToInt(v), 3f, 24f, 9f, 3f, 24f, true),
                Num("Wrap Distance", "How far the rays wrap around the fighter.", () => DBPU.KiDeflectBeamSplitWrapDistance, v => DBPU.KiDeflectBeamSplitWrapDistance = v, .1f, 5f, 1.1f, .1f),
                Num("Forward Length", "How far the rays continue past the fighter.", () => DBPU.KiDeflectBeamSplitContinuationLength, v => DBPU.KiDeflectBeamSplitContinuationLength = v, .2f, 30f, 7f, .2f),
                Num("Arc Degrees", "Width of the split fan.", () => DBPU.KiDeflectBeamSplitArcDegrees, v => DBPU.KiDeflectBeamSplitArcDegrees = v, 30f, 300f, 150f, 30f, 300f),
                Num("VFX Lifetime", "How long the split rays stay visible, in seconds.", () => DBPU.KiDeflectBeamSplitLifetime, v => DBPU.KiDeflectBeamSplitLifetime = v, .08f, 3f, .42f, .08f));
        }

        CfgPage ChargePage()
        {
            return new CfgPage("Ki Charge", DbAtlas.DragonBall[4], "Charging up, debris, Sparking and Overdrive.", KiChargeModeReset).Add(
                CfgRow.Note("Ki Charge Hold", () => DbpuInputRegistration.GetActionKeyLabel(DBPU.KiChargeHoldActionId, KeyCode.R), "Change key bindings in Pause > Controls > Dragon Ball: Singularity."),
                CfgRow.Note("Ki Output (head)", () => DbpuInputRegistration.GetActionKeyLabel(CombatPowerController.ActionId, CombatPowerController.DefaultKey) + "  /  Shift reverses", "Change key bindings in Pause > Controls > Dragon Ball: Singularity."),
                CfgRow.Note("Torso Techniques", () => DbpuInputRegistration.GetActionKeyLabel(TorsoTechniquePicker.ActionId, TorsoTechniquePicker.DefaultKey) + " on torso: next  /  Use: start", "Tap on the torso to show the fighter's torso techniques above the head and step through them (Shift steps back); Use starts the one shown."),
                Toggle("Ki Charge Effects", "Aura and debris effects while charging.", () => DBPU.KiChargeFxEnabled, v => DBPU.KiChargeFxEnabled = v),
                CfgRow.Section("Regen & aura"),
                Num("Charge Regen / sec", "Ki gained per second while holding charge.", () => DBPU.KiChargeHoldRegenPerSecond, v => DBPU.KiChargeHoldRegenPerSecond = v, 1f, 1000000f, 50000f, 1f, float.MaxValue, false, true),
                Num("Aura Scale", "Size of the charging aura.", () => DBPU.KiChargeAuraScaleMultiplier, v => DBPU.KiChargeAuraScaleMultiplier = v, .1f, 5f, 1.25f, .1f),
                Num("Aura Particle Speed", "Speed of the charging aura's particles.", () => DBPU.KiChargeAuraParticleSpeedMultiplier, v => DBPU.KiChargeAuraParticleSpeedMultiplier = v, .1f, 5f, 1.35f, .1f),
                CfgRow.Section("Debris"),
                Num("Max Active", "Most debris pieces in the air at once.", () => DBPU.KiChargeDebrisMaxActive, v => DBPU.KiChargeDebrisMaxActive = Mathf.RoundToInt(v), 1f, 200f, 28f, 1f, 100000f, true),
                Num("Max Session", "Most debris pieces in one charge.", () => DBPU.KiChargeDebrisMaxSession, v => DBPU.KiChargeDebrisMaxSession = Mathf.RoundToInt(v), 1f, 500f, 80f, 1f, 100000f, true),
                Num("Spawn Interval", "Seconds between debris pieces.", () => DBPU.KiChargeDebrisSpawnInterval, v => DBPU.KiChargeDebrisSpawnInterval = v, .1f, 5f, .52f, .1f),
                Num("Ring Min", "Closest distance debris appears from the fighter.", () => DBPU.KiChargeDebrisRingMin, v =>
                {
                    DBPU.KiChargeDebrisRingMin = v;
                    DBPU.KiChargeDebrisRingMax = Mathf.Max(DBPU.KiChargeDebrisRingMin + .08f, DBPU.KiChargeDebrisRingMax);
                }, .08f, 3f, .35f, .08f),
                Num("Ring Max", "Farthest distance debris appears from the fighter.", () => DBPU.KiChargeDebrisRingMax, v => DBPU.KiChargeDebrisRingMax = Mathf.Max(DBPU.KiChargeDebrisRingMin + .08f, v), .16f, 5f, 1.1f, .16f),
                Num("Spawn Y Offset", "Height where debris appears, relative to the fighter.", () => DBPU.KiChargeDebrisSpawnYOffset, v => DBPU.KiChargeDebrisSpawnYOffset = v, -5f, 5f, -1f),
                CfgRow.Section("Charge transform"),
                Toggle("Auto Transform", "Keep charging to climb through forms automatically.", () => DBPU.KiChargeAutoTransformWhileChargingEnabled, v => DBPU.KiChargeAutoTransformWhileChargingEnabled = v),
                Num("Seconds per Tier", "Charging time before moving up one form.", () => DBPU.KiChargeAutoTransformSecondsPerTier, v => DBPU.KiChargeAutoTransformSecondsPerTier = v, .5f, 60f, 12f, .5f),
                CfgRow.Section("Sparking"),
                Toggle("Sparking", "Allow the Sparking overload state.", () => DBPU.SparkingEnabled, v => DBPU.SparkingEnabled = v),
                Toggle("Overcharge Trigger", "Holding charge at full ki can trigger Sparking.", () => DBPU.KiChargeSparkingOverchargeEnabled, v => DBPU.KiChargeSparkingOverchargeEnabled = v),
                Num("Overload Seconds", "How long Sparking lasts.", () => DBPU.SparkingOverloadSeconds, v => DBPU.SparkingOverloadSeconds = v, 0f, 600f, 180f, 0f),
                Num("Exhaust Seconds", "How long exhaustion lasts after Sparking.", () => DBPU.SparkingExhaustSeconds, v => DBPU.SparkingExhaustSeconds = v, 0f, 300f, 60f, 0f),
                Num("Cooldown Seconds", "Wait before Sparking can start again.", () => DBPU.SparkingCooldownSeconds, v => DBPU.SparkingCooldownSeconds = v, 0f, 300f, 0f, 0f),
                Num("Overcharge Hold", "Seconds of holding charge needed to trigger Sparking.", () => DBPU.SparkingOverchargeHoldSeconds, v => DBPU.SparkingOverchargeHoldSeconds = v, 0f, 30f, 3f, 0f),
                CfgRow.Section("Overdrive"),
                Toggle("Overdrive", "Allow ki output above 100%.", () => DBPU.KiOverdriveOutputEnabled, v => DBPU.KiOverdriveOutputEnabled = v),
                CfgRow.Note("Overdrive Hotkey", () => DbpuInputRegistration.GetActionKeyLabel(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey), "Change key bindings in Pause > Controls > Dragon Ball: Singularity."),
                Num("Max Output %", "Highest ki output Overdrive can reach.", () => DBPU.KiOverdriveMaxPercent, v => DBPU.KiOverdriveMaxPercent = v, 101f, 250f, 200f, 101f, 250f),
                Num("Seconds Before Exhaust", "Time above 100% output before exhaustion sets in.", () => DBPU.KiOverdriveSecondsBeforeExhaust, v => DBPU.KiOverdriveSecondsBeforeExhaust = v, .35f, 30f, 2.75f, .35f));
        }

        CfgPage StrainPage()
        {
            return new CfgPage("Strain", DbAtlas.DragonBall[5], "The toll of pushing past your limits, including Kaioken.", StrainReset).Add(
                Num("Strain Gain", "How fast strain builds up.", () => DBPU.StrainGainMultiplier, v => DBPU.StrainGainMultiplier = v, 0f, 5f, 1f),
                Num("Strain Drain", "How fast strain fades.", () => DBPU.StrainDrainMultiplier, v => DBPU.StrainDrainMultiplier = v, 0f, 5f, 1f),
                Num("Strain Damage", "Damage taken from high strain.", () => DBPU.StrainDamageMultiplier, v => DBPU.StrainDamageMultiplier = v, 0f, 5f, 1f),
                CfgRow.Section("Kaioken"),
                Num("Kaioken Strain", "Strain caused by Kaioken.", () => DBPU.KaiokenStrainMultiplier, v => DBPU.KaiokenStrainMultiplier = v, 0f, 5f, 1f, 0f),
                Num("Kaioken Max Multiplier", "Highest Kaioken multiplier available. A quick tap of the ki charge key on the head steps an active Kaioken up to it (Shift steps down).", () => DBPU.KaiokenMaxMultiplier, v => DBPU.KaiokenMaxMultiplier = v, 1f, 100f, 20f, 1f),
                Toggle("Kaioken Strain Damage", "Overusing Kaioken hurts the fighter.", () => DBPU.KaiokenStrainDamageEnabled, v => DBPU.KaiokenStrainDamageEnabled = v));
        }

        CfgPage BeamPage()
        {
            return new CfgPage("Beams", DbAtlas.DragonBall[6], "Force, size and what beams can do to the world.", BeamReset).Add(
                Num("Beam Force", "How hard beams push.", () => DBPU.Beam_KnockbackMultiplier, v => DBPU.Beam_KnockbackMultiplier = v, 0f, 5f, 1f),
                Num("Beam Speed", "How fast beams travel.", () => DBPU.Beam_SpeedMultiplier, v => DBPU.Beam_SpeedMultiplier = v, 0f, 5f, 1f),
                Num("Beam Size", "How wide beams are.", () => DBPU.Beam_SizeMultiplier, v => DBPU.Beam_SizeMultiplier = v, 0f, 5f, 1f),
                Toggle("Can Delete", "Beams can erase objects outright.", () => DBPU.Beam_ToggleDelete, v => DBPU.Beam_ToggleDelete = v),
                Toggle("Can Destroy", "Beams can destroy objects.", () => DBPU.Beam_ToggleDestroy, v => DBPU.Beam_ToggleDestroy = v),
                Toggle("Can Burn", "Beams set things on fire.", () => DBPU.Beam_ToggleHeat, v => DBPU.Beam_ToggleHeat = v),
                Toggle("Beam Assist", "Helps beams find their target.", () => DBPU.BeamAssistEnabled, v => DBPU.BeamAssistEnabled = v),
                Toggle("Clash Shockwave", "Beam clashes send out a shockwave.", () => DBPU.BeamClashShockwaveEnabled, v => DBPU.BeamClashShockwaveEnabled = v));
        }

        CfgPage SpecialPage()
        {
            return new CfgPage("Special", DbAtlas.DragonBall[7], "Exponential growth and Zenkai boosts.", SpecialReset).Add(
                Num("Exponential Growth %", "Percent of power gained per step in exponential forms.", () => (DBPU.Special_ExponentialGrowthSpeed - 1f) * 100f, v => DBPU.Special_ExponentialGrowthSpeed = 1f + Mathf.Clamp(v, .1f, 5f) / 100f, .1f, 5f, .5f, .1f, 5f),
                CfgRow.Section("Zenkai"),
                Toggle("Zenkai Boost", "Surviving heavy damage makes a fighter stronger.", () => DBPU.Special_ZenkaiBoostEnabled, v => DBPU.Special_ZenkaiBoostEnabled = v),
                Num("Zenkai Scale", "Share of the damage taken that turns into power.", () => DBPU.Special_ZenkaiBoostScale, v => DBPU.Special_ZenkaiBoostScale = v, .01f, 10f, 1f, .01f),
                Num("Zenkai Max", "Highest boost a fighter can reach.", () => DBPU.Special_ZenkaiBoostMax, v => DBPU.Special_ZenkaiBoostMax = v, 1f, 100f, 10f, 1f));
        }

        CfgPage MiscPage()
        {
            return new CfgPage("Misc", DbAtlas.Star, "Voices, glow and per-character tools.", MiscReset) { IconTint = DbTheme.Gold }.Add(
                Toggle("Voice Lines", "Fighters speak when they use powers.", () => DBPU.VoiceLines, v => DBPU.VoiceLines = v),
                Pct("Voice Lines Volume", "Volume of voice lines.", () => DBPU.VoiceLineVolume, v => DBPU.VoiceLineVolume = v, 1f, 1f),
                Pct("Ki Powers Volume", "Volume of ki attacks.", () => DBPU.PowersVolume, v => DBPU.PowersVolume = v, 1f, 1f),
                CfgRow.Section("Glow"),
                Toggle("Glowing Eyes", "Eyes glow in transformed states.", () => DBPU.TransformationEyeGlowEnabled, v =>
                {
                    DBPU.TransformationEyeGlowEnabled = v;
                    TransformationEyeGlow.RefreshAll();
                }),
                Toggle("Glowing Hair", "Hair glows in transformed states.", () => DBPU.TransformationHairGlowEnabled, v =>
                {
                    DBPU.TransformationHairGlowEnabled = v;
                    TransformationHairGlow.RefreshAll();
                }),
                Num("Glow Brightness %", "Brightness of glowing hair and eyes.", () => DBPU.TransformationHairGlowBrightness, v =>
                {
                    DBPU.TransformationHairGlowBrightness = v;
                    TransformationEyeGlow.RefreshAll();
                    TransformationHairGlow.RefreshAll();
                }, 0f, 500f, 100f, 0f, TransformationGlowUtility.MaxBrightnessPercent),
                CfgRow.Section("Characters"),
                CfgRow.Button("Change Power Level", "Close this menu, then click a character to set their power.", BeginChangePowerLevel),
                CfgRow.Button("Tune Character Aura", "Close this menu, then click a character to adjust their aura size and position.", BeginTuneCharacterAura),
                CfgRow.Button("Reset Character Powers", "Restore every character's power level to its default.", CharacterPowerCatalog.ResetAll, true));
        }

        CfgPage FeaturesPage()
        {
            var page = new CfgPage("Singularity", DbAtlas.Star, "Optional features. Turn off anything you don't want.") { IconTint = DbTheme.Shenron };
            var modules = Singularity.Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                page.Add(Toggle(module.Title, module.Summary, () => module.Enabled, v => Singularity.SetEnabled(module.Id, v)));
            }
            return page;
        }

        private void OpenKiBlastColorPicker()
        {
            ColorpickerDialogBehaviour picker = ColorpickerDialogBehaviour.Instance;
            if (picker == null)
            {
                DialogBoxManager.Notification("Color picker is not available right now.");
                return;
            }

            window.SetHidden(true);

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
                window.SetHidden(false);
            }));
            picker.OnCancel.AddListener(new UnityAction(() => window.SetHidden(false)));
            picker.Open(current);
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
        }

        private void StrainReset()
        {
            DBPU.StrainDrainMultiplier = 1f;
            DBPU.StrainGainMultiplier = 1f;
            DBPU.StrainDamageMultiplier = 1f;
            DBPU.KaiokenStrainMultiplier = 1f;
            DBPU.KaiokenMaxMultiplier = 20f;
            DBPU.KaiokenStrainDamageEnabled = true;
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
        }

        private void SpecialReset()
        {
            DBPU.Special_ExponentialGrowthSpeed = 1.005f;
            DBPU.Special_ZenkaiBoostEnabled = false;
            DBPU.Special_ZenkaiBoostScale = 1f;
            DBPU.Special_ZenkaiBoostMax = 10f;
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
        }

        private void BeginChangePowerLevel()
        {
            PowerGiverState.Disarm();
            AuraTunePickerState.Disarm();
            CharacterPowerPickerState.Arm();
            CharacterPowerPickerInput.Ensure();
            CloseForWorldPick();
            ModAPI.Notify("Click a character to set their power.");
        }

        private void BeginTuneCharacterAura()
        {
            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            AuraTunePickerState.Arm();
            AuraTunePickerInput.Ensure();
            CloseForWorldPick();
            ModAPI.Notify("Click a character to tune their aura.");
        }

        void CloseForWorldPick()
        {
            if (window != null && window.Window) window.Window.Close();
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
            DbAtlas.Ensure();
            window = new ConfigsWindow(BuildPages(), SaveConfigs, () => Destroy(gameObject));
            if (!window.Window) Destroy(gameObject);
        }
    }
}
