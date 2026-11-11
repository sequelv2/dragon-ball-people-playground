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
    public class ExponentialGrowth : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;
        private TransformationContextMenu Transformation;
        private PersonBehaviour person;

        public float Limit = 7000f;

        public float Health = 1f;

        public void OnDestroy()
        {
            if (Multipliers != null) Multipliers.ExponentialMultiplier = 1f;

            if (person == null) person = GetComponent<PersonBehaviour>();
            if (person == null) return;

            foreach (var limb in person.Limbs)
            {
                limb.InitialHealth /= Health;
                limb.Health /= Health;
            }

            person.ChangeWeightBasedOnHealth();
        }

        public void Kickstart()
        {
            if (person == null) person = GetComponent<PersonBehaviour>();
            if (Multipliers == null)
            {
                if (!transform.root.GetComponent<MultipliersAndStuffs>()) transform.root.gameObject.AddComponent<MultipliersAndStuffs>();
                Multipliers = transform.root.GetComponent<MultipliersAndStuffs>();
            }

            if (Transformation == null)
            {
                var head = transform.Find("Head");
                if (head != null) Transformation = head.GetComponent<TransformationContextMenu>();
            }

            StopAllCoroutines();
            StartCoroutine(Evolve());
        }

        public void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!transform.root.GetComponent<MultipliersAndStuffs>()) transform.root.gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = transform.root.GetComponent<MultipliersAndStuffs>();

            person = GetComponent<PersonBehaviour>();

            var head = transform.Find("Head");
            if (head != null) Transformation = head.GetComponent<TransformationContextMenu>();
        }

        public IEnumerator Evolve()
        {
            if (Transformation == null || Multipliers == null || person == null) yield break;

            string PreviousForm = Transformation.Form;

            while (true)
            {
                if (Transformation.Form == "Ikari") Limit = 7000f;
                else if (Transformation.Form == "SSJ1") Limit = 800000f;
                else if (Transformation.Form == "FPSSJ") Limit = 1000000000f;

                while (Multipliers.ExponentialMultiplier < Limit)
                {
                    if (Transformation.Form == "Ikari") Limit = 7000f;
                    else if (Transformation.Form == "SSJ1") Limit = 800000f;
                    else if (Transformation.Form == "FPSSJ") Limit = 1000000000f;

                    yield return new WaitForSecondsRealtime(0.1f);

                    if (Multipliers.ExponentialMultiplier * DBPU.Special_ExponentialGrowthSpeed < Limit)
                    {
                        foreach (var limb in person.Limbs)
                        {
                            if (!float.IsInfinity(limb.InitialHealth * DBPU.Special_ExponentialGrowthSpeed)) limb.InitialHealth *= DBPU.Special_ExponentialGrowthSpeed;
                            if (!float.IsInfinity(limb.Health * DBPU.Special_ExponentialGrowthSpeed)) limb.Health *= DBPU.Special_ExponentialGrowthSpeed;
                        }

                        Health *= DBPU.Special_ExponentialGrowthSpeed;
                        Multipliers.ExponentialMultiplier *= DBPU.Special_ExponentialGrowthSpeed;
                    }

                    else
                    {
                        foreach (var limb in person.Limbs)
                        {
                            if (!float.IsInfinity(limb.InitialHealth * DBPU.Special_ExponentialGrowthSpeed)) limb.InitialHealth *= Limit / Multipliers.ExponentialMultiplier;
                            if (!float.IsInfinity(limb.Health * DBPU.Special_ExponentialGrowthSpeed)) limb.Health *= Limit / Multipliers.ExponentialMultiplier;
                        }

                        Health *= Limit / Multipliers.ExponentialMultiplier;
                        Multipliers.ExponentialMultiplier *= Limit / Multipliers.ExponentialMultiplier;
                    }

                    person.ChangeWeightBasedOnHealth();
                }

                yield return new WaitUntil(() => Transformation.Form != PreviousForm);
                PreviousForm = Transformation.Form;
            }
        }
    }
}
