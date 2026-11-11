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
    public class HumanController : MonoBehaviour
    {
        public PersonBehaviour person;

        public FeatureControllers Features = new FeatureControllers();
        public List<LimbInitialValues> LimbValues = new List<LimbInitialValues>();

        private bool FirstRun = true;

        private void Awake()
        {
            DbpuHarmonyBootstrap.EnsureApplied();
        }

        public void Start()
        {
            StartCoroutine(ChangeWeight());

            if (person != null)
            {
                DbpuLimbDurability.EnsurePerson(person);
                foreach (var limb in person.Limbs)
                {
                    if (limb == null) continue;

                    var link = limb.gameObject.GetOrAddComponent<HarmonyPatchLink>();
                    link.Controller = this;
                }
            }
        }

        public void Update()
        {
            foreach (var limb in person.Limbs)
            {
                limb.DiscomfortingHeatTemperature = 800;
            }

            if (Features.ImmuneToKnockout == true && person.IsAlive())
            {
                if (gameObject.GetComponent<LimbBehaviour>().Health > gameObject.GetComponent<LimbBehaviour>().InitialHealth * 0.15f && person.Consciousness < 0.8f && Features.ForceKnockout == false)
                {
                    person.Consciousness = 1f;
                }

                if (person.ShockLevel > 0.2f)
                {
                    person.ShockLevel = 0f;
                }
            }

            if (Features.ImmuneToPain == true)
            {
                if (person.ShockLevel > 0f)
                {
                    person.ShockLevel = 0f;
                }

                if (person.PainLevel > 0.2f)
                {
                    person.PainLevel = 0f;
                }

                // Cower = OverridePoseIndex 1. Clear it so pain-immune chars do not stay locked.
                if (person.OverridePoseIndex == 1 || person.OverridePoseIndex == 5)
                {
                    person.OverridePoseIndex = -1;
                }
            }

            if (person.OxygenLevel < 0.8f && Features.ImmuneToOxigenLoss == true)
            {
                person.OxygenLevel = 1f;
            }

            if (person.BrainDamaged == true && (Features.ImmuneToBrainDamage == true || Features.Immortal == true || Features.TrueImmortal == true))
            {
                person.BrainDamaged = false;
            }

            if (person.Braindead == true && Features.ImmuneToBrainDamage == true && gameObject.GetComponent<LimbBehaviour>().Health > 0f)
            {
                person.Braindead = false;
            }

            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;

                if (limb.CirculationBehaviour.ImmuneToDamage == false && Features.ImmuneToBleeding == true)
                {
                    limb.CirculationBehaviour.ImmuneToDamage = true;
                }

                else if (limb.CirculationBehaviour.ImmuneToDamage == true && Features.ImmuneToBleeding == false)
                {
                    limb.CirculationBehaviour.ImmuneToDamage = false;
                }

                if (Features.ImmuneToBullets == true && limb.gameObject.GetComponent<PhysicalBehaviour>().BulletPenetration == true)
                {
                    limb.gameObject.GetComponent<PhysicalBehaviour>().BulletPenetration = false;
                    //limb.ImmuneToDamage = true;

                    //limb.gameObject.AddComponent<StabbingDetector>().humanController = GetComponent<HumanController>();
                    //limb.gameObject.GetComponent<StabbingDetector>().limb = limb;
                }

                else if (Features.ImmuneToBullets == false && limb.gameObject.GetComponent<PhysicalBehaviour>().BulletPenetration == false)
                {
                    limb.gameObject.GetComponent<PhysicalBehaviour>().BulletPenetration = true;
                    //limb.ImmuneToDamage = false;

                    //Destroy(limb.gameObject.GetComponent<StabbingDetector>());
                }

                if ((limb.CirculationBehaviour.BleedingPointCount > 0 || limb.CirculationBehaviour.BloodAmount < 0.95f) && Features.ImmuneToBleeding == true)
                {
                    limb.CirculationBehaviour.HealBleeding();
                    if (Features.ImmuneToFire == true || Features.ImmuneToAcid == true) limb.CirculationBehaviour.BloodAmount = 1f;
                    limb.CirculationBehaviour.BleedingPointCount = 0;
                }

                if (limb.CirculationBehaviour.InternalBleedingIntensity > 0f && Features.ImmuneToInternalBleeding == true)
                {
                    limb.CirculationBehaviour.InternalBleedingIntensity = 0f;
                }

                if (limb.Numbness > 0f && Features.ImmuneToNumbness == true)
                {
                    limb.Numbness = 0f;
                }

                if (limb.SkinMaterialHandler.AcidProgress > 0f && Features.ImmuneToAcid == true)
                {
                    limb.SkinMaterialHandler.AcidProgress = 0f;
                }

                if (limb.PhysicalBehaviour.BurnProgress > 0f && Features.ImmuneToFire == true)
                {
                    limb.PhysicalBehaviour.BurnProgress = 0f;
                }

                if (limb.PhysicalBehaviour.charge > 0f && Features.ImmuneToElectricity == true)
                {
                    limb.PhysicalBehaviour.charge = 0f;
                }

                if (Features.Immortal == true || Features.TrueImmortal == true)
                {
                    if (limb.Broken == true)
                    {
                        limb.HealBone();
                    }

                    if (!float.IsInfinity(limb.Health))
                    {
                        limb.Health = float.PositiveInfinity;
                    }

                    /*if (limb.InitialHealth != float.MaxValue)
                    {
                        limb.InitialHealth = float.MaxValue;
                        }*/

                    if (limb.PhysicalBehaviour.Disintegratable == false)
                    {
                        limb.PhysicalBehaviour.Disintegratable = true;
                    }
                }

                if (Features.TrueImmortal == true)
                {
                    if (limb.PhysicalBehaviour.Deletable == false)
                    {
                        limb.PhysicalBehaviour.Deletable = true;
                    }
                }
            }
        }

        public class StabbingDetector : MonoBehaviour
        {
            public HumanController humanController;
            public LimbBehaviour limb;

            public void Stabbed(Stabbing stab)
            {
                if (humanController.Features.ImmuneToStabbing == true || limb.ImmuneToDamage == false) return;

                if (limb.HasLungs && !limb.IsAndroid && UnityEngine.Random.value > 0.9f)
                {
                    limb.LungsPunctured = true;
                }

                bool flag = limb.IsWorldPointInVitalPart(stab.point) && UnityEngine.Random.value > 0.05f;
                limb.Damage(limb.Health * 0.5f * (limb.IsZombie ? 0.1f : 1f) * (float)((!flag) ? 1 : 2) * UserPreferenceManager.Current.FragilityMultiplier);
                if (flag && !limb.IsZombie && humanController.Features.ImmuneToInternalBleeding == false)
                {
                    limb.CirculationBehaviour.InternalBleedingIntensity += 5f * UnityEngine.Random.value;
                }

                limb.Wince(165f);

                if (!limb.IsZombie && limb.NodeBehaviour.IsConnectedToRoot && humanController.Features.ImmuneToPain == false)
                {
                    limb.Person.ShockLevel += UnityEngine.Random.value;
                }

                if (humanController.Features.ImmuneToNumbness == false) limb.Numbness = 1f;
                if (humanController.Features.ImmuneToPain == false) limb.Person.AdrenalineLevel += 1f;

                if (limb.HasBrain && flag && (!limb.IsZombie || !(UnityEngine.Random.value > 0.5f)))
                {
                    if (humanController.Features.ImmuneToPain == false) limb.Person.AddPain(90f);
                    if (humanController.Features.ImmuneToInternalBleeding == false) limb.CirculationBehaviour.InternalBleedingIntensity += 5f * UnityEngine.Random.value;
                    limb.Health = 0f;

                    if (UnityEngine.Random.value > 0.25f)
                    {
                        limb.Person.Consciousness = 0f;
                    }
                }
            }
        }

        public sealed class HarmonyPatchLink : MonoBehaviour
        {
            public HumanController Controller;
            public float CachedAverageWeight = -1f;
            float _weightCachedUntil;

            public float GetCachedAverageWeight()
            {
                if (Controller == null || Controller.person == null) return 1f;
                float now = Time.unscaledTime;
                if (CachedAverageWeight >= 0f && now < _weightCachedUntil)
                    return CachedAverageWeight;
                CachedAverageWeight = MCP_Utils.AverageWeight(Controller.person);
                _weightCachedUntil = now + 0.5f;
                return CachedAverageWeight;
            }
        }

        public void StartChangeWeight()
        {
            StartCoroutine(ChangeWeight());
        }

        public IEnumerator ChangeWeight()
        {
            yield return new WaitForFixedUpdate();

            if (FirstRun == true)
            {
                FirstRun = false;

                foreach (var limb in person.Limbs)
                {
                    if (limb == null) continue;

                    LimbInitialValues values = new LimbInitialValues();
                    values.thisLimb = limb;
                    values.SaveInitialValues(limb);

                    LimbValues.Add(values);
                }
            }

            if (MCP_Utils.AverageMaxHealth(person) < 1000f)
            {
                person.ChangeWeightBasedOnHealth();

                foreach (LimbInitialValues values in LimbValues)
                {
                    if (values == null) continue;
                    values.RestoreInitialValues();
                }

                MCP_Utils.InvalidatePersonStatCache(person);
                MCP_Utils.PhysicalPropertiesController physicalPropertiesController = new MCP_Utils.PhysicalPropertiesController();
                physicalPropertiesController.ImmuneToFire = Features.ImmuneToFire;
                physicalPropertiesController.ImmuneToElectricity = Features.ImmuneToElectricity;
                physicalPropertiesController.ImmuneToBullets = Features.ImmuneToBullets;
                physicalPropertiesController.ImmuneToStabbing = Features.ImmuneToStabbing;
                physicalPropertiesController.Weight = MCP_Utils.AverageWeight(person);

                foreach (var limb in person.Limbs)
                {
                    if (limb == null) continue;

                    limb.PhysicalBehaviour.Properties = MCP_Utils.NewPhysicalProperties(physicalPropertiesController, limb.PhysicalBehaviour);
                }
            }

            else
            {
                person.ChangeWeightBasedOnHealth();

                MCP_Utils.PhysicalPropertiesController physicalPropertiesController = new MCP_Utils.PhysicalPropertiesController();
                physicalPropertiesController.ImmuneToFire = Features.ImmuneToFire;
                physicalPropertiesController.ImmuneToElectricity = Features.ImmuneToElectricity;
                physicalPropertiesController.ImmuneToBullets = Features.ImmuneToBullets;
                physicalPropertiesController.ImmuneToStabbing = Features.ImmuneToStabbing;
                physicalPropertiesController.Weight = MCP_Utils.AverageWeight(person);

                foreach (var limb in person.Limbs)
                {
                    if (limb == null) continue;

                    limb.PhysicalBehaviour.Properties = MCP_Utils.NewPhysicalProperties(physicalPropertiesController, limb.PhysicalBehaviour);
                }
            }

            yield return null;
        }

        public void StartForcedKnockout()
        {
            StartCoroutine(ForcedKnockout());
        }

        public IEnumerator ForcedKnockout()
        {
            yield return new WaitForFixedUpdate();

            person.Consciousness = 0f;

            yield return new WaitUntil(() => person.Consciousness > 0.8f);

            Features.ForceKnockout = false;

            yield return null;
        }

        public class LimbInitialValues
        {
            public float BreakingThreshold;
            public float ImpactPainMultiplier;
            public float ImpactDamageMultiplier;
            public float ShotDamageMultiplier;
            public float RegenerationSpeed;
            public float BloodRegenerationPerSecond;
            public float GForcePassoutThreshold;
            public float RegenerateBurnProgressSpeed;
            public float SkinMaterialHandlerIntensityMultiplier;
            public float TrueInitialMass;
            public float RBMass;
            public float BaseStrength;
            public float discomfortTemperature;

            public LimbBehaviour thisLimb;

            public void SaveInitialValues(LimbBehaviour limb)
            {
                BreakingThreshold = limb.BreakingThreshold;
                ImpactPainMultiplier = limb.ImpactPainMultiplier;
                ImpactDamageMultiplier = limb.ImpactDamageMultiplier;
                ShotDamageMultiplier = limb.ShotDamageMultiplier;
                RegenerationSpeed = limb.RegenerationSpeed;
                BloodRegenerationPerSecond = limb.CirculationBehaviour.BloodRegenerationPerSecond;
                GForcePassoutThreshold = limb.GForcePassoutThreshold;
                RegenerateBurnProgressSpeed = limb.RegenerateBurnProgressSpeed;
                SkinMaterialHandlerIntensityMultiplier = limb.SkinMaterialHandler.intensityMultiplier;
                TrueInitialMass = limb.PhysicalBehaviour.TrueInitialMass;
                RBMass = limb.PhysicalBehaviour.rigidbody.mass;
                BaseStrength = limb.BaseStrength;
                discomfortTemperature = limb.DiscomfortingHeatTemperature;
            }

            public void RestoreInitialValues()
            {
                //thisLimb.BreakingThreshold = BreakingThreshold;
                //thisLimb.ImpactPainMultiplier = ImpactPainMultiplier;
                //thisLimb.ImpactDamageMultiplier = ImpactDamageMultiplier;
                thisLimb.ShotDamageMultiplier = ShotDamageMultiplier;
                thisLimb.RegenerationSpeed = RegenerationSpeed;
                thisLimb.CirculationBehaviour.BloodRegenerationPerSecond = BloodRegenerationPerSecond;
                thisLimb.GForcePassoutThreshold = GForcePassoutThreshold;
                thisLimb.RegenerateBurnProgressSpeed = RegenerateBurnProgressSpeed;
                thisLimb.SkinMaterialHandler.intensityMultiplier = SkinMaterialHandlerIntensityMultiplier;
                thisLimb.PhysicalBehaviour.TrueInitialMass = TrueInitialMass;
                thisLimb.PhysicalBehaviour.rigidbody.mass = RBMass;
                thisLimb.BaseStrength = BaseStrength;
                thisLimb.DiscomfortingHeatTemperature = discomfortTemperature;
            }
        }

        public class FeatureControllers
        {
            public bool ImmuneToKnockout { get; set; } = false;
            public float KnockoutResistance { get; set; } = 1f;
            public bool ImmuneToPain { get; set; } = false;
            public bool ImmuneToOxigenLoss { get; set; } = false;
            public bool ImmuneToBleeding { get; set; } = false;
            public bool ImmuneToInternalBleeding { get; set; } = false;
            public bool ImmuneToBrainDamage { get; set; } = false;
            public bool ImmuneToNumbness { get; set; } = false;
            public bool ImmuneToFire { get; set; } = false;
            public bool ImmuneToBurn { get; set; } = false;
            public float HighTemperatureResistance { get; set; } = 70f;
            public bool ImmuneToFreeze { get; set; } = false;
            public float LowTemperatureResistance { get; set; } = 0f;
            public bool ImmuneToAcid { get; set; } = false;
            public bool ImmuneToElectricity { get; set; } = false;
            public bool ImmuneToStabbing { get; set; } = false;
            public bool ImmuneToBullets { get; set; } = false;
            public bool Immortal { get; set; } = false;
            public bool TrueImmortal { get; set; } = false;

            public bool ForceKnockout { get; set; } = false;

            public float LimbBreakResistance { get; set; } = 25f;
        }
    }
}
