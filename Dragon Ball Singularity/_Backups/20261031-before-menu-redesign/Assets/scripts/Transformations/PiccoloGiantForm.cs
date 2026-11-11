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
    public class PiccoloGiantForm : MonoBehaviour
    {
        public bool Active = false;
        public bool Growing = false;
        public float GrowthSpeed = 3f;
        public float ShrinkSpeed = 5f;
        public float HeightSmoothTime = 0.4f;
        public float targetSizeMeters = 15f;

        private PersonBehaviour person;
        private GameObject middleBody;
        private KiSystem kiSys;
        private HumanController humanController;
        private AuraHandler auraHandler;
        private string lastForm = "";
        private bool lastActive;
        private bool lastGrowing;
        private float lastAppliedHeight = -1f;
        private float animHeight;
        private float heightVelocity;
        private bool animating;
        private Coroutine heightAnim;

        private float GetFormBaseHeight(string form)
        {
            if (form == "OrangePiccolo") return 2.74f;
            return 2.26f;
        }

        private float GetMaxHeight(string form)
        {
            if (targetSizeMeters > 0f) return targetSizeMeters;
            if (form == "OrangePiccolo") return 20f;
            return 15f;
        }

        private string GetCurrentForm()
        {
            if (kiSys != null && !string.IsNullOrEmpty(kiSys.CurrentForm)) return kiSys.CurrentForm;
            return "Base";
        }

        private float GetTargetHeight()
        {
            string form = GetCurrentForm();
            return (Active && Growing) ? GetMaxHeight(form) : GetFormBaseHeight(form);
        }

        private float GetActualHeightMeters()
        {
            return Mathf.Abs(person.transform.localScale.y) * 1.68f;
        }

        private void Start()
        {
            person = GetComponent<PersonBehaviour>();
            kiSys = GetComponent<KiSystem>();
            middleBody = person.Limbs[2].gameObject;
            humanController = person.Limbs[0].GetComponent<HumanController>();
            auraHandler = GetComponent<AuraHandler>();

            lastForm = GetCurrentForm();
            lastActive = Active;
            lastGrowing = Growing;
            lastAppliedHeight = GetActualHeightMeters();

            StartCoroutine(SetupHeadActivation());
        }

        private IEnumerator SetupHeadActivation()
        {
            yield return null;

            var head = person.transform.Find("Head");
            System.Action growthAction = () =>
            {
                if (!person.IsAlive() || head.GetComponent<LimbBehaviour>().Health <= 0f || !Active) return;
                Growing = !Growing;
                NotifyStateChanged();
            };

            var trigger = head.GetComponent<UseEventTrigger>();
            if (trigger == null)
            {
                head.gameObject.AddComponent<UseEventTrigger>().Action = growthAction;
            }
            else
            {
                var previous = trigger.Action;
                trigger.Action = () =>
                {
                    growthAction();
                    previous?.Invoke();
                };
            }
        }

        private void FixedUpdate()
        {
            if (person == null || !person.IsAlive() || animating) return;

            string form = GetCurrentForm();
            bool formChanged = form != lastForm;
            bool stateChanged = Active != lastActive || Growing != lastGrowing;

            if (formChanged || stateChanged)
            {
                lastForm = form;
                lastActive = Active;
                lastGrowing = Growing;
                StartHeightAnimation();
            }
        }

        public void NotifyStateChanged()
        {
            lastActive = Active;
            lastGrowing = Growing;
            lastForm = GetCurrentForm();
            StartHeightAnimation();
        }

        private void StartHeightAnimation()
        {
            if (heightAnim != null)
                StopCoroutine(heightAnim);

            heightAnim = StartCoroutine(AnimateHeightCoroutine());
        }

        private void FreezeForResize()
        {
            foreach (var limb in person.Limbs)
            {
                if (limb?.gameObject == null) continue;
                if (!limb.gameObject.GetComponent<FreezeBehaviour>())
                    limb.gameObject.AddComponent<FreezeBehaviour>();
            }
        }

        private void UnfreezeAfterResize()
        {
            foreach (var limb in person.Limbs)
            {
                if (limb?.gameObject == null) continue;
                var freeze = limb.gameObject.GetComponent<FreezeBehaviour>();
                if (freeze != null) Destroy(freeze);
                if (limb.PhysicalBehaviour != null)
                    MCP_Utils.ForceClearViaExit(limb.PhysicalBehaviour);
            }
        }

        private IEnumerator AnimateHeightCoroutine()
        {
            animating = true;
            float target = GetTargetHeight();

            if (Mathf.Abs(GetActualHeightMeters() - target) < 0.02f)
            {
                ApplyHeightAt(target, true);
                animating = false;
                heightAnim = null;
                yield break;
            }

            FreezeForResize();
            animHeight = GetActualHeightMeters();
            heightVelocity = 0f;

            yield return new WaitForFixedUpdate();

            while (person != null && person.IsAlive())
            {
                target = GetTargetHeight();
                float formBase = GetFormBaseHeight(GetCurrentForm());
                float formMax = GetMaxHeight(GetCurrentForm());

                if (Active && Growing)
                {
                    animHeight = Mathf.SmoothDamp(animHeight, target, ref heightVelocity, HeightSmoothTime, GrowthSpeed * 1.5f, Time.fixedDeltaTime);
                }
                else
                {
                    heightVelocity = 0f;
                    animHeight = Mathf.MoveTowards(animHeight, target, ShrinkSpeed * Time.fixedDeltaTime);
                }

                animHeight = Mathf.Clamp(animHeight, formBase, formMax);

                if (Mathf.Abs(animHeight - lastAppliedHeight) > 0.02f)
                    ApplyHeightAt(animHeight, false);

                if (Mathf.Abs(animHeight - target) < 0.03f)
                    break;

                yield return new WaitForFixedUpdate();
            }

            if (person != null && person.IsAlive())
                ApplyHeightAt(GetTargetHeight(), true);

            UnfreezeAfterResize();
            animating = false;
            heightAnim = null;
        }

        private void ApplyHeightAt(float meters, bool updateWeight)
        {
            person.transform.root.transform.SetHeight(person, middleBody, meters);
            person.ForceAllLimbsClearViaExit();
            lastAppliedHeight = meters;

            if (auraHandler != null && auraHandler.AuraActive)
            {
                float personScale = Mathf.Abs(person.transform.localScale.x);
                float auraMult = personScale > 1.15f ? (1f + (personScale - 1f)) : 1f;
                auraHandler.SetAuraScale(auraMult);
            }

            if (updateWeight && humanController != null)
                humanController.StartChangeWeight();
        }

        public void SyncAfterTransformation(float formSizeMeters)
        {
            if (heightAnim != null)
            {
                StopCoroutine(heightAnim);
                heightAnim = null;
                animating = false;
                UnfreezeAfterResize();
            }

            lastForm = GetCurrentForm();
            float targetHeight = (!Active || !Growing) ? formSizeMeters : Mathf.Max(formSizeMeters, GetTargetHeight());
            ApplyHeightAt(targetHeight, true);
        }
    }
}
