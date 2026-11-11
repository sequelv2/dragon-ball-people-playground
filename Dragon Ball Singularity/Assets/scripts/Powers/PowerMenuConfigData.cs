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
    static class PowerMenuConfigData
    {
        static List<List<PowerMenuConfigEntry>> WrapPage(List<PowerMenuConfigEntry> entries)
        {
            return new List<List<PowerMenuConfigEntry>> { entries ?? new List<PowerMenuConfigEntry>() };
        }

        static void AddSlider(List<PowerMenuConfigEntry> list, string id, string name, string desc, Func<float> get, Action<float> set, float min, float max, bool percent = false)
        {
            list.Add(new PowerMenuConfigSliderEntry { ID = id, Name = name, Description = desc, GetValue = get, SetValue = set, Min = min, Max = max, ShowAsPercent = percent });
        }

        static void AddSliderAsPercentOfMax(List<PowerMenuConfigEntry> list, string id, string name, string desc, Func<float> get, Action<float> set, float min, float max)
        {
            float range = Mathf.Max(max - min, 0.0001f);
            list.Add(new PowerMenuConfigSliderEntry
            {
                ID = id,
                Name = name,
                Description = desc,
                GetValue = get,
                SetValue = set,
                GetDisplayValue = () => Mathf.Clamp((get() - min) / range, 0.0001f, 1f),
                SetDisplayValue = v => set(Mathf.Clamp(min + v * range, min, max)),
                Min = 0.0001f,
                Max = 1f,
                ShowAsPercent = true
            });
        }

        static void AddToggle(List<PowerMenuConfigEntry> list, string id, string name, string desc, Func<bool> get, Action<bool> set)
        {
            list.Add(new PowerMenuConfigToggleEntry { ID = id, Name = name, Description = desc, GetValue = get, SetValue = set });
        }

        static PunchPower GetPunchPower(PersonBehaviour person)
        {
            if (person == null) return null;
            foreach (var limb in person.Limbs)
            {
                if (limb.name.Contains("LowerArm"))
                {
                    var pp = limb.GetComponent<PunchPower>();
                    if (pp != null) return pp;
                }
            }
            return null;
        }

        static void SetAllPunchPower(PersonBehaviour person, Action<PunchPower> apply)
        {
            if (person == null || apply == null) return;
            foreach (var limb in person.Limbs)
            {
                var pp = limb.GetComponent<PunchPower>();
                if (pp != null) apply(pp);
            }
        }

        static KickPower GetKickPower(PersonBehaviour person)
        {
            if (person == null) return null;
            foreach (var limb in person.Limbs)
            {
                if (PowerSetup.IsKickLimb(limb.name))
                {
                    var kp = limb.GetComponent<KickPower>();
                    if (kp != null) return kp;
                }
            }
            return null;
        }

        static void SetAllKickPower(PersonBehaviour person, Action<KickPower> apply)
        {
            if (person == null || apply == null) return;
            foreach (var limb in person.Limbs)
            {
                if (PowerSetup.IsKickLimb(limb.name))
                {
                    var kp = limb.GetComponent<KickPower>();
                    if (kp != null) apply(kp);
                }
            }
        }

        const float BlastKnockbackMax = 5f;
        const float BeamKnockbackMax = 1f;
        const float BarrageSpeedMax = 1f;

        static void AddDamageOutputPercent(List<PowerMenuConfigEntry> list, Func<float> get, Action<float> set)
        {
            AddSlider(list, "DamageOutput", "Damage Output", "Damage output multiplier.", get, v => set(Mathf.Clamp(v, 0.0001f, 1f)), 0.0001f, 1f, true);
        }

        static void AddKnockbackPercent(List<PowerMenuConfigEntry> list, Func<float> get, Action<float> set, float max = 1f)
        {
            float range = Mathf.Max(max, 0.0001f);
            AddSlider(list, "Knockback", "Knockback", "Knockback strength multiplier.", get, v => set(Mathf.Clamp(v, 0f, max)), 0f, max, true);
        }

        static void AddSpeedPercent(List<PowerMenuConfigEntry> list, Func<float> get, Action<float> set, float max = 1f)
        {
            AddSlider(list, "Speed", "Speed", "Speed multiplier.", get, v => set(Mathf.Clamp(v, 0.0001f, max)), 0.0001f, max, true);
        }

        static void AddBlastKiConfigs(List<PowerMenuConfigEntry> list, BlastBehaviour blast)
        {
            if (blast == null) return;
            AddDamageOutputPercent(list, () => blast.DamageOutput, v => blast.DamageOutput = v);
            AddKnockbackPercent(list, () => blast.KnockbackMultiplier / BlastKnockbackMax, v => blast.KnockbackMultiplier = v * BlastKnockbackMax, 1f);
        }

        static void AddBeamKiConfigs(List<PowerMenuConfigEntry> list, BeamBehaviour beam, bool damageOutput = true)
        {
            if (beam == null) return;
            if (damageOutput)
                AddDamageOutputPercent(list, () => beam.DamageOutput, v => beam.DamageOutput = v);
            AddKnockbackPercent(list, () => beam.KnockbackOutput, v => beam.KnockbackOutput = v, BeamKnockbackMax);
        }

        static void AddBarrageConfigs(List<PowerMenuConfigEntry> list, PersonBehaviour person)
        {
            var barrage = person != null ? person.GetComponentInChildren<Barrage>(true) : null;
            if (barrage == null) return;
            AddDamageOutputPercent(list, () => barrage.damageOutput, v => barrage.damageOutput = v);
            AddSpeedPercent(list, () => barrage.speedOutput, v => barrage.speedOutput = v, BarrageSpeedMax);
        }

        static void AddMeleePowerConfigs(List<PowerMenuConfigEntry> list, PersonBehaviour person, PunchPower punch, KickPower kick)
        {
            if (punch != null)
            {
                AddDamageOutputPercent(list, () => punch.DamageOutput, v => SetAllPunchPower(person, p => p.DamageOutput = v));
                AddKnockbackPercent(list, () => punch.knockbackOutput, v => SetAllPunchPower(person, p => p.knockbackOutput = v), 1f);
                AddToggle(list, "NukeShockwave", "Nuke Shockwave", "Enable shockwave on heavy hits.", () => punch.enableNuclearExplosion, v => SetAllPunchPower(person, p => p.enableNuclearExplosion = v));
            }
            else if (kick != null)
            {
                AddDamageOutputPercent(list, () => kick.DamageOutput, v => SetAllKickPower(person, k => k.DamageOutput = v));
                AddKnockbackPercent(list, () => kick.knockbackOutput, v => SetAllKickPower(person, k => k.knockbackOutput = v), 1f);
                AddToggle(list, "NukeShockwave", "Nuke Shockwave", "Enable shockwave on heavy hits.", () => kick.enableNuclearExplosion, v => SetAllKickPower(person, k => k.enableNuclearExplosion = v));
            }
        }

        static void AddFinalExplosionConfigs(List<PowerMenuConfigEntry> list, FinalExplosion fx)
        {
            if (fx == null) return;
            AddDamageOutputPercent(list, () => fx.DamageOutput, v => fx.DamageOutput = v);
            AddKnockbackPercent(list, () => fx.KnockbackOutput, v => fx.KnockbackOutput = v, 1f);
        }

        static void AddSolarConfigs(List<PowerMenuConfigEntry> list, SolarFlare solar)
        {
            if (solar == null) return;
            AddSlider(list, "Time", "Time", "Solar flare duration in seconds.", () => solar.flareDuration, v => solar.flareDuration = Mathf.Clamp(v, 0.5f, 15f), 0.5f, 15f);
            AddSlider(list, "Brightness", "Brightness", "Solar flare brightness.", () => solar.brightnessOutput, v => solar.brightnessOutput = Mathf.Clamp(v, 0.0001f, 1f), 0.0001f, 1f, true);
        }

        static void AddShieldConfigs(List<PowerMenuConfigEntry> list, KiShield shield)
        {
            if (shield == null) return;
            AddSlider(list, "Diameter", "Barrier size", "Barrier size; automatically kept large enough for this fighter.", () => shield.shieldDiameterMeters, v => shield.shieldDiameterMeters = Mathf.Clamp(v, 5f, 60f), 5f, 60f);
        }

        static void AddGrowthConfigs(List<PowerMenuConfigEntry> list, PiccoloGiantForm growth)
        {
            if (growth == null) return;
            AddSlider(list, "TargetSize", "Target Size", "Giant form target height in meters.", () => growth.targetSizeMeters, v => growth.targetSizeMeters = Mathf.Clamp(v, 3f, 40f), 3f, 40f);
            AddSlider(list, "Speed", "Speed", "Growth transition time in seconds.", () => growth.HeightSmoothTime, v => growth.HeightSmoothTime = Mathf.Clamp(v, 0.05f, 5f), 0.05f, 5f);
        }

        static void AddKaiokenConfigs(List<PowerMenuConfigEntry> list, Kaioken kaioken)
        {
            if (kaioken == null) return;
            float max = Mathf.Max(DBPU.KaiokenMaxMultiplier, 1f);
            AddSlider(list, "Multiplier", "Multiplier", "Kaioken power multiplier.", () => kaioken.Multiplier, v => kaioken.ApplyConfiguredMultiplier(v), 1f, max);
        }

        static void AddHellzoneConfigs(List<PowerMenuConfigEntry> list, HellzoneGranade hz)
        {
            if (hz == null) return;
            AddDamageOutputPercent(list, () => hz.DamageOutput, v => hz.DamageOutput = v);
            AddKnockbackPercent(list, () => hz.KnockbackMultiplier / BlastKnockbackMax, v => hz.KnockbackMultiplier = v * BlastKnockbackMax, 1f);
            AddSlider(list, "BlastsAmount", "Blasts Amount", "Number of trap blasts.", () => hz.blastsAmount, v => hz.blastsAmount = Mathf.RoundToInt(Mathf.Clamp(v, 10f, 200f)), 10f, 200f);
        }

        static void AddBallKiConfigs(List<PowerMenuConfigEntry> list, BallBehaviour ball)
        {
            if (ball == null) return;
            AddDamageOutputPercent(list, () => ball.DamageOutput, v => ball.DamageOutput = v);
            AddKnockbackPercent(list, () => ball.KnockbackOutput, v => ball.KnockbackOutput = v, 1f);
        }

        static void AddGogetaSpecialConfigs(List<PowerMenuConfigEntry> list, Func<float> getDamageOutput, Action<float> setDamageOutput, Func<float> getKnockbackOutput, Action<float> setKnockbackOutput, Func<int> getBlastsAmount = null, Action<int> setBlastsAmount = null, Func<float> getDuration = null, Action<float> setDuration = null)
        {
            AddDamageOutputPercent(list, getDamageOutput, setDamageOutput);
            AddKnockbackPercent(list, getKnockbackOutput, setKnockbackOutput, 1f);
            if (getBlastsAmount != null && setBlastsAmount != null)
                AddSlider(list, "BlastsAmount", "Blasts Amount", "Number of projectiles.", () => getBlastsAmount(), v => setBlastsAmount(Mathf.RoundToInt(Mathf.Clamp(v, 10f, 300f))), 10f, 300f);
            if (getDuration != null && setDuration != null)
                AddSlider(list, "Duration", "Duration", "Attack duration in seconds.", getDuration, v => setDuration(Mathf.Clamp(v, 0.5f, 10f)), 0.5f, 10f);
        }

        public static List<List<PowerMenuConfigEntry>> GetPowerPages(CharacterPowers.PowerStats power, GameObject instance, PersonBehaviour person)
        {
            var entries = new List<PowerMenuConfigEntry>();
            if (power == null || instance == null) return WrapPage(entries);

            string id = power.ID;
            string type = power.PowerType;

            if (id == "Barrage")
                AddBarrageConfigs(entries, person);
            else if (id == "KiBlast" || id == "KiBlastBarrage")
                AddBlastKiConfigs(entries, instance.GetComponent<BlastBehaviour>());
            else if (id == "FullForceKamehameha")
                AddBeamKiConfigs(entries, instance.GetComponent<BeamBehaviour>(), false);
            else if (type == CharacterPowers.PowerGroups.Beam)
                AddBeamKiConfigs(entries, instance.GetComponent<BeamBehaviour>());
            else if (id == "FinalExplosion")
                AddFinalExplosionConfigs(entries, instance.GetComponent<FinalExplosion>());
            else if (id == "SolarFlare")
                AddSolarConfigs(entries, instance.GetComponent<SolarFlare>());
            else if (id == "KiShield")
                AddShieldConfigs(entries, instance.GetComponent<KiShield>());
            else if (id == "HellzoneGranade")
                AddHellzoneConfigs(entries, instance.GetComponent<HellzoneGranade>());
            else if (type == CharacterPowers.PowerGroups.Ball || id == "GiganticCrusher")
                AddBallKiConfigs(entries, instance.GetComponent<BallBehaviour>());
            else if (id == "StardustFall")
            {
                var c = instance.GetComponent<StardustFall>();
                if (c != null)
                    AddGogetaSpecialConfigs(entries, () => c.DamageOutput, v => c.DamageOutput = v, () => c.KnockbackOutput, v => c.KnockbackOutput = v, () => c.blastsAmount, v => c.blastsAmount = v, () => c.durationSeconds, v => c.durationSeconds = v);
            }
            else if (id == "GodPunisher")
            {
                var c = instance.GetComponent<GodPunisher>();
                if (c != null)
                    AddGogetaSpecialConfigs(entries, () => c.DamageOutput, v => c.DamageOutput = v, () => c.KnockbackOutput, v => c.KnockbackOutput = v, () => c.blastsAmount, v => c.blastsAmount = v);
            }
            else if (id == "MeteorExplosion")
            {
                var c = instance.GetComponent<MeteorExplosion>();
                if (c != null)
                    AddGogetaSpecialConfigs(entries, () => c.DamageOutput, v => c.DamageOutput = v, () => c.KnockbackOutput, v => c.KnockbackOutput = v);
            }
            else if (id == "StardustBreaker")
            {
                var c = instance.GetComponent<StardustBreaker>();
                if (c != null)
                    AddGogetaSpecialConfigs(entries, () => c.DamageOutput, v => c.DamageOutput = v, () => c.KnockbackOutput, v => c.KnockbackOutput = v);
            }
            else if (id == "PunchPower")
                AddMeleePowerConfigs(entries, person, GetPunchPower(person), null);
            else if (id == "KickPower")
                AddMeleePowerConfigs(entries, person, null, GetKickPower(person));
            else if (id == "GreatNamekianGrowth" || type == CharacterPowers.PowerGroups.Growth)
                AddGrowthConfigs(entries, instance.GetComponent<PiccoloGiantForm>());
            else if (id == "Kaioken")
                AddKaiokenConfigs(entries, instance.GetComponent<Kaioken>());

            return WrapPage(entries);
        }
    }
}
