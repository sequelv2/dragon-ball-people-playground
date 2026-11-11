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
    public class LimbInitialValues
    {
        //public float InitialHealth;
        //public float Health;
        public float BreakingThreshold;
        public float ImpactPainMultiplier;
        public float ImpactDamageMultiplier;
        public float ShotDamageMultiplier;
        public float RegenerationSpeed;
        public float BloodRegenerationPerSecond;
        public float RegenerateBurnProgressSpeed;
        public float SkinMaterialHandlerIntensityMultiplier;
        public bool IsAndroid;
        //public float TrueInitialMass;
        //public float BaseStrength;

        public float discomfortTemperature;

        public LimbInitialValues(LimbBehaviour limb)
        {
            //InitialHealth = limb.InitialHealth;
            //Health = limb.Health;
            BreakingThreshold = limb.BreakingThreshold;
            ImpactPainMultiplier = limb.ImpactPainMultiplier;
            ImpactDamageMultiplier = limb.ImpactDamageMultiplier;
            ShotDamageMultiplier = limb.ShotDamageMultiplier;
            RegenerationSpeed = limb.RegenerationSpeed;
            BloodRegenerationPerSecond = limb.CirculationBehaviour.BloodRegenerationPerSecond;
            RegenerateBurnProgressSpeed = limb.RegenerateBurnProgressSpeed;
            SkinMaterialHandlerIntensityMultiplier = limb.SkinMaterialHandler.intensityMultiplier;
            IsAndroid = limb.IsAndroid;
            //TrueInitialMass = limb.PhysicalBehaviour.TrueInitialMass;
            //BaseStrength = limb.BaseStrength;
            discomfortTemperature = limb.DiscomfortingHeatTemperature;
        }

        public void RestoreInitialValues(LimbBehaviour limb)
        {
            //limb.InitialHealth = InitialHealth;
            //limb.Health = Health;
            limb.BreakingThreshold = BreakingThreshold;
            limb.ImpactPainMultiplier = ImpactPainMultiplier;
            limb.ImpactDamageMultiplier = ImpactDamageMultiplier;
            limb.ShotDamageMultiplier = ShotDamageMultiplier;
            limb.RegenerationSpeed = RegenerationSpeed;
            limb.CirculationBehaviour.BloodRegenerationPerSecond = BloodRegenerationPerSecond;
            limb.RegenerateBurnProgressSpeed = RegenerateBurnProgressSpeed;
            limb.SkinMaterialHandler.intensityMultiplier = SkinMaterialHandlerIntensityMultiplier;
            limb.IsAndroid = IsAndroid;
            //limb.PhysicalBehaviour.TrueInitialMass = TrueInitialMass;
            //limb.PhysicalBehaviour.rigidbody.mass = TrueInitialMass;
            //limb.BaseStrength = BaseStrength;
            //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
            limb.DiscomfortingHeatTemperature = discomfortTemperature;
        }
    }
}
