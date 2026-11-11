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
    public class SenzuBeanBehaviour : MonoBehaviour
    {
        public void OnCollisionEnter2D(Collision2D other)
        {
            ContactPoint2D[] contacts = other.contacts;
            var averageImpact = Utils.GetAverageImpulse(contacts, contacts.Length);

            if (averageImpact > 0.01f)
            {
                if (other.gameObject.GetComponent<LimbBehaviour>())
                {
                    if (other.gameObject.GetComponent<LimbBehaviour>().name == "Head" && other.transform.root.GetComponent<PersonBehaviour>().IsAlive() && other.gameObject.GetComponent<LimbBehaviour>().Health > 0)
                    {
                        foreach (var limb in other.transform.root.GetComponent<PersonBehaviour>().Limbs)
                        {
                            limb.gameObject.AddComponent<SenzuBeanRegen>();
                        }

                        if (other.transform.root.GetComponent<KiSystem>())
                        {
                            other.transform.root.GetComponent<KiSystem>().currentKi = other.transform.root.GetComponent<KiSystem>().maxKi;
                            other.transform.root.GetComponent<KiSystem>().Strain = 0f;
                        }

                        other.gameObject.GetComponent<PhysicalBehaviour>().PlayClipOnce(DBPU.SenzuBeanEatingSounds[UnityEngine.Random.Range(0, 3)]);

                        Destroy(gameObject);
                    }
                }
            }
        }

        public class SenzuBeanRegen : MonoBehaviour
        {
            public LimbBehaviour limb;

            public void Start()
            {
                limb = GetComponent<LimbBehaviour>();
                StartCoroutine(waitheal());

                if (limb.IsDismembered == false)
                {
                    limb.Health = limb.InitialHealth;
                    limb.CirculationBehaviour.HealBleeding();
                    limb.HealBone();
                    limb.LungsPunctured = false;
                    limb.Numbness = 0f;
                    limb.CirculationBehaviour.StabWoundCount = 0;
                    limb.CirculationBehaviour.GunshotWoundCount = 0;
                    limb.BruiseCount = 0;
                    limb.CirculationBehaviour.BloodAmount = 1f;
                    limb.Person.PainLevel = 0f;
                    limb.Person.ShockLevel = 0f;
                    limb.Person.Consciousness = 1f;
                    limb.Person.OxygenLevel = 1f;
                    limb.Person.AdrenalineLevel = 0f;
                    limb.Person.BrainDamaged = false;
                    limb.PhysicalBehaviour.Temperature = 29f;
                    limb.InternalTemperature = 37f;
                    if (limb.CirculationBehaviour.WasInitiallyPumping == true) limb.CirculationBehaviour.IsPump = true;

                    limb.SkinMaterialHandler.Sync();
                }
            }

            private IEnumerator waitheal()
            {
                yield return new WaitForSeconds(2.5f);

                Destroy(gameObject.GetComponent<SenzuBeanRegen>());
            }

            public void FixedUpdate()
            {
                if (limb.IsDismembered == false)
                {
                    if (limb.PhysicalBehaviour.BurnProgress > 0f)
                    {
                        limb.PhysicalBehaviour.BurnProgress -= Time.deltaTime * 1f;
                    }

                    else
                    {
                        limb.PhysicalBehaviour.BurnProgress = 0f;
                    }

                    if (limb.SkinMaterialHandler.AcidProgress > 0f)
                    {
                        limb.SkinMaterialHandler.AcidProgress -= Time.deltaTime * 1f;
                    }

                    else
                    {
                        limb.SkinMaterialHandler.AcidProgress = 0f;
                    }

                    if (limb.SkinMaterialHandler.RottenProgress > 0f)
                    {
                        limb.SkinMaterialHandler.RottenProgress -= Time.deltaTime * 1f;
                    }

                    else
                    {
                        limb.SkinMaterialHandler.RottenProgress = 0f;
                    }

                    if (limb.SkinMaterialHandler.damagePoints.Max<Vector4>((Func<Vector4, float>)(c => c.z)) < 50.0)
                    {
                        for (int i = 0; i < limb.SkinMaterialHandler.damagePoints.Length; i++)
                        {
                            Vector4[] damagePoints = limb.SkinMaterialHandler.damagePoints;
                            int num = i;
                            damagePoints[num].z = damagePoints[num].z - Time.deltaTime * 6f;
                        }

                        limb.SkinMaterialHandler.Sync();
                    }
                }
            }
        }
    }
}
