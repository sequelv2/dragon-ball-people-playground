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
    public class KiSystem : MonoBehaviour
    {
        public float currentKi = 0;
        public float maxKi = 100;
        public float baseRegen = 1;
        private Color baseColor;
        public float chargeMult = 25;
        public float exhaustionCooldownTime = 10f;
        private bool isCharging = false;
        public float exhaustionTimer = 0f;
        public bool isExhausted = false;
        public Color barColor = new Color(1, 1, 1, 1);
        public Color requiredColor = Color.clear;
        private PersonBehaviour personBehaviour;
        private TransformationContextMenu transformationContextMenu;
        private CharacterTransformations characterTransformations;
        private CharacterTransformations.Transformations transformations;
        private CharacterTransformations.Transformations.TransformationStats currentTransformation;
        private Dictionary<string, LimbInitialValues> limbInitialValues = new Dictionary<string, LimbInitialValues>();
        private SpriteRenderer barRenderer;
        private GameObject head;
        public float currentKiDrainRate = 0;
        float previousKi;
        public string CurrentForm = "Base";
        public string BaseForm = "Base";
        private PhysicalBehaviour upperBody;

        List<GameObject> barStuff = new List<GameObject>();
        GameObject bar;

        private SpriteRenderer strainBarRenderer;
        List<GameObject> strainBarStuff = new List<GameObject>();
        GameObject strainBar;
        public Color strainBarColor = new Color(0.96f, 0.6f, 0.7f, 1f);

        public float Strain = 0f;
        public float StrainRate = 0f;
        public float StrainRateMultiplier = 1f;
        public bool StrainOn = false;

        public bool KiChargeActiveForRegen = false;
        public bool SparkingOverloadActive = false;
        public bool SparkingExhaustActive = false;
        public float SparkingOvercharge01 = 0f;
        private SpriteRenderer sparkingPrepBarRenderer;
        private SpriteRenderer sparkingOutlineOuterRenderer;
        private SpriteRenderer sparkingOutlineInnerRenderer;

        void Start()
        {
            currentKi = maxKi;
            personBehaviour = gameObject.GetComponent<PersonBehaviour>();
            CombatAbilityWheel.BindPerson(personBehaviour);
            CombatPowerController.Bind(personBehaviour);
            CombatHudView.Bind(personBehaviour);
            DbpuDismemberment.Attach(personBehaviour);
            DbpuTechniquePowers.AttachDefaults(personBehaviour);
            previousKi = currentKi;

            if (gameObject.GetComponent<CharacterTransformations>())
            {
                characterTransformations = gameObject.GetComponent<CharacterTransformations>();

                CeceUtils.DelayedInvoke(0.1f, () =>
                {
                    foreach (var limb in personBehaviour.Limbs)
                    {
                        limbInitialValues[limb.name] = new LimbInitialValues(limb);
                    }
                });

                transformations = characterTransformations.transformations;
            }

            if (personBehaviour.Limbs[0].gameObject.GetComponent<TransformationContextMenu>())
            {
                transformationContextMenu = gameObject.GetComponent<TransformationContextMenu>();
            }

            head = transform.root.Find("Head").gameObject;
            bar = new GameObject("KiBar");
            //bar.transform.SetParent(head.transform);
            bar.transform.localPosition = new Vector3(0, 1f, 0);
            bar.transform.localScale = new Vector3(1, 1f, 1);
            barRenderer = bar.AddComponent<SpriteRenderer>();
            barRenderer.sprite = DBPU.barSprite;
            barRenderer.sortingLayerName = "Top";
            barRenderer.sortingOrder = 2;
            barRenderer.material = DBPU.barMaterial;
            barRenderer.material.SetFloat("_RevealAmount", 1);
            bar.AddComponent<ExistInDetailView>();
            barStuff.Add(bar);
            var depletedBar = new GameObject("KiDepletedBar");
            depletedBar.transform.SetParent(bar.transform);
            depletedBar.transform.localPosition = new Vector3(0, 0f, 0);
            depletedBar.transform.localScale = new Vector3(1, 1f, 1);
            var depletedBarRenderer = depletedBar.AddComponent<SpriteRenderer>();
            depletedBarRenderer.sprite = DBPU.depletedBarSprite;
            depletedBarRenderer.sortingLayerName = "Top";
            depletedBarRenderer.sortingOrder = 1;
            depletedBar.AddComponent<ExistInDetailView>();
            barStuff.Add(depletedBar);
            var barFrame = new GameObject("KiBarFrame");
            barFrame.transform.SetParent(bar.transform);
            barFrame.transform.localPosition = new Vector3(0, 0f, 0);
            barFrame.transform.localScale = new Vector3(1, 1f, 1);
            var frameSpriteRenderer = barFrame.AddComponent<SpriteRenderer>();
            frameSpriteRenderer.sprite = DBPU.barFrameSprite;
            frameSpriteRenderer.sortingLayerName = "Top";
            barFrame.AddComponent<ExistInDetailView>();
            barStuff.Add(barFrame);
            var sparkPrepGo = new GameObject("KiSparkingPrepBar");
            sparkPrepGo.transform.SetParent(bar.transform);
            sparkPrepGo.transform.localPosition = new Vector3(0, 0f, 0);
            sparkPrepGo.transform.localScale = new Vector3(1, 1f, 1);
            sparkingPrepBarRenderer = sparkPrepGo.AddComponent<SpriteRenderer>();
            sparkingPrepBarRenderer.sprite = DBPU.barSprite;
            sparkingPrepBarRenderer.sortingLayerName = "Top";
            sparkingPrepBarRenderer.sortingOrder = 3;
            sparkingPrepBarRenderer.material = new Material(DBPU.barMaterial);
            sparkingPrepBarRenderer.material.SetFloat("_RevealAmount", 0f);
            sparkingPrepBarRenderer.material.SetColor("_TintColor", new Color(0.62f, 0.22f, 1f, 1f));
            // Must stay disabled when idle: same sprite sorts above the main fill (order 3) and would hide Ki at Reveal 0.
            sparkingPrepBarRenderer.enabled = false;
            sparkPrepGo.AddComponent<ExistInDetailView>();
            barStuff.Add(sparkPrepGo);

            var sparkingOutlineOuter = new GameObject("KiSparkingOutlineOuter");
            sparkingOutlineOuter.transform.SetParent(bar.transform);
            sparkingOutlineOuter.transform.localPosition = new Vector3(0, 0f, 0);
            sparkingOutlineOuter.transform.localScale = Vector3.one;
            sparkingOutlineOuterRenderer = sparkingOutlineOuter.AddComponent<SpriteRenderer>();
            sparkingOutlineOuterRenderer.sprite = DBPU.barFrameSprite;
            sparkingOutlineOuterRenderer.sortingLayerName = "Top";
            sparkingOutlineOuterRenderer.sortingOrder = 4;
            sparkingOutlineOuterRenderer.color = Color.clear;
            sparkingOutlineOuterRenderer.enabled = false;
            sparkingOutlineOuter.AddComponent<ExistInDetailView>();
            barStuff.Add(sparkingOutlineOuter);

            var sparkingOutlineInner = new GameObject("KiSparkingOutlineInner");
            sparkingOutlineInner.transform.SetParent(bar.transform);
            sparkingOutlineInner.transform.localPosition = new Vector3(0, 0f, 0);
            sparkingOutlineInner.transform.localScale = Vector3.one;
            sparkingOutlineInnerRenderer = sparkingOutlineInner.AddComponent<SpriteRenderer>();
            sparkingOutlineInnerRenderer.sprite = DBPU.barFrameSprite;
            sparkingOutlineInnerRenderer.sortingLayerName = "Top";
            sparkingOutlineInnerRenderer.sortingOrder = 5;
            sparkingOutlineInnerRenderer.color = Color.clear;
            sparkingOutlineInnerRenderer.enabled = false;
            sparkingOutlineInner.AddComponent<ExistInDetailView>();
            barStuff.Add(sparkingOutlineInner);
            UpdateKiBarColor(BaseForm);
            upperBody = personBehaviour.Limbs[1].gameObject.GetComponent<PhysicalBehaviour>();
            baseColor = barColor;

            strainBar = new GameObject("StrainBar");
            strainBar.transform.SetParent(bar.transform);
            strainBar.transform.localPosition = new Vector3(0, 0.3142f, 0);
            strainBar.transform.localScale = new Vector3(1, 1, 1);
            strainBarRenderer = strainBar.AddComponent<SpriteRenderer>();
            strainBarRenderer.sprite = DBPU.strainBarSprite;
            strainBarRenderer.sortingLayerName = "Top";
            strainBarRenderer.sortingOrder = 2;
            strainBarRenderer.material = ModAPI.FindMaterial("Sprites-Default");
            strainBarRenderer.material.SetFloat("_RevealAmount", 0);
            strainBar.AddComponent<ExistInDetailView>();
            strainBarStuff.Add(strainBar);
            var depletedStrainBar = new GameObject("StrainDepletedBar");
            depletedStrainBar.transform.SetParent(strainBar.transform);
            depletedStrainBar.transform.localPosition = new Vector3(0, 0, 0);
            depletedStrainBar.transform.localScale = new Vector3(1, 1, 1);
            var depletedStrainBarRenderer = depletedStrainBar.AddComponent<SpriteRenderer>();
            depletedStrainBarRenderer.sprite = DBPU.strainDepletedBarSprite;
            depletedStrainBarRenderer.sortingLayerName = "Top";
            depletedStrainBarRenderer.sortingOrder = 1;
            depletedStrainBar.AddComponent<ExistInDetailView>();
            strainBarStuff.Add(depletedStrainBar);
            var strainBarFrame = new GameObject("StrainBarFrame");
            strainBarFrame.transform.SetParent(strainBar.transform);
            strainBarFrame.transform.localPosition = new Vector3(0, 0, 0);
            strainBarFrame.transform.localScale = new Vector3(1, 1, 1);
            var strainframeSpriteRenderer = strainBarFrame.AddComponent<SpriteRenderer>();
            strainframeSpriteRenderer.sprite = DBPU.strainBarFrameSprite;
            strainframeSpriteRenderer.sortingLayerName = "Top";
            strainBarFrame.AddComponent<ExistInDetailView>();
            barStuff.Add(strainBarFrame);

            strainBarRenderer.color = Color.clear;
            depletedStrainBarRenderer.color = Color.clear;
            strainframeSpriteRenderer.color = Color.clear;

            if (gameObject.GetComponent<KiChargeModeController>() == null)
            {
                var kc = gameObject.AddComponent<KiChargeModeController>();
                
                }
            if (gameObject.GetComponent<KiSparkingController>() == null)
                gameObject.AddComponent<KiSparkingController>();
            if (gameObject.GetComponent<KiOverdriveController>() == null)
                gameObject.AddComponent<KiOverdriveController>();

            Transform kiChargeLimb = head != null ? head.transform : null;
            if (head != null && head.GetComponent<CellMaxScream>() != null)
            {
                var existingHeadCharge = head.GetComponent<KiChargeHeadHoldInput>();
                if (existingHeadCharge != null)
                    UnityEngine.Object.Destroy(existingHeadCharge);
                kiChargeLimb = personBehaviour != null && personBehaviour.Limbs != null && personBehaviour.Limbs.Length > 1
                    ? personBehaviour.Limbs[1].transform
                    : transform.Find("Body/UpperBody");
            }
            if (kiChargeLimb != null && kiChargeLimb.GetComponent<KiChargeHeadHoldInput>() == null)
                kiChargeLimb.gameObject.AddComponent<KiChargeHeadHoldInput>();

            var headPb = head != null ? head.GetComponent<PhysicalBehaviour>() : null;
            if (headPb != null && headPb.ContextMenuOptions != null)
            {
                headPb.ContextMenuOptions.Buttons.Add(new ContextMenuButton("KiChargeFxToggle",
                    (Func<string>)(() =>
                    {
                        var c = gameObject.GetComponent<KiChargeModeController>();
                        return (c != null && c.ModeEnabled) ? "Ki charge FX: Disable" : "Ki charge FX: Enable";
                    }),
                    "Enable then hold activation on the head (same as Ki Blast barrage on arms) — debris, Ki, aura.",
                    () =>
                    {
                        var c = gameObject.GetComponent<KiChargeModeController>();
                        if (c != null) c.ModeEnabled = !c.ModeEnabled;
                    }));
                headPb.ContextMenuOptions.Buttons.Add(new ContextMenuButton("SparkingActivate",
                    (Func<string>)(() => "Sparking: Activate (or overcharge)"),
                    "With charge FX on, hold activation on head at full Ki until purple fills. Or start Sparking here.",
                    () =>
                    {
                        if (DBPU.SparkingEnabled)
                            gameObject.GetComponent<KiSparkingController>()?.TryStartOverload();
                    }));
            }
        }

        public void ResetValues()
        {
            if (personBehaviour == null)
                personBehaviour = gameObject.GetComponent<PersonBehaviour>();
            if (personBehaviour == null) return;

            EnsureLimbBaselines();

            foreach (var limb in personBehaviour.Limbs)
            {
                if (limb == null) continue;
                if (limbInitialValues.TryGetValue(limb.name, out var values) && values != null)
                    values.RestoreInitialValues(limb);
            }
        }

        public void EnsureReadyForTransforms()
        {
            if (personBehaviour == null)
                personBehaviour = gameObject.GetComponent<PersonBehaviour>();

            if (characterTransformations == null)
                characterTransformations = gameObject.GetComponent<CharacterTransformations>();
            if (characterTransformations != null)
                transformations = characterTransformations.transformations;

            if (transformationContextMenu == null)
            {
                var head = transform.root != null ? transform.root.Find("Head") : null;
                if (head != null)
                    transformationContextMenu = head.GetComponent<TransformationContextMenu>();
                if (transformationContextMenu == null && personBehaviour != null && personBehaviour.Limbs != null && personBehaviour.Limbs.Length > 0)
                    transformationContextMenu = personBehaviour.Limbs[0].GetComponent<TransformationContextMenu>();
            }

            EnsureLimbBaselines();
        }

        void EnsureLimbBaselines()
        {
            if (personBehaviour == null || personBehaviour.Limbs == null) return;
            foreach (var limb in personBehaviour.Limbs)
            {
                if (limb == null || string.IsNullOrEmpty(limb.name)) continue;
                if (!limbInitialValues.ContainsKey(limb.name))
                    limbInitialValues[limb.name] = new LimbInitialValues(limb);
            }
        }

        public void SubtractKi(float amount)
        {
            StartCoroutine(LerpKi(currentKi, Mathf.Max(currentKi - amount, 0), 0.5f));
        }

        private IEnumerator LerpKi(float startValue, float endValue, float duration)
        {
            float time = 0;
            while (time < duration)
            {
                currentKi = Mathf.Lerp(startValue, endValue, time / duration);
                time += Time.deltaTime;
                yield return null;
            }
            currentKi = endValue;
        }

        public void ApplyTransformation(string transformationKey)
        {
            if (transformations == null || !transformations.TransformationData.ContainsKey(transformationKey))
            {
                return;
            }

            var newTransformation = transformations.TransformationData[transformationKey];
            currentTransformation = newTransformation;
            currentKiDrainRate = currentTransformation.KiDrainRate;
            CurrentForm = transformationKey;
            UpdateKiBarColor(transformationKey);
        }

        void OnDestroy()
        {
            Destroy(bar);
        }

        void Update()
        {
            Vector3 targetPosition = head.transform.position + new Vector3(0, 1f, 0);
            bar.transform.position = Vector3.Lerp(bar.transform.position, targetPosition, Time.deltaTime * 20f);
            bar.transform.rotation = Quaternion.identity;

            if (!isExhausted && DBPU.BaseConsideredForms.Contains(CurrentForm) && personBehaviour.IsAlive())
            {
                currentKi += (baseRegen * DBPU.KiRegenMultiplier) * Time.deltaTime;
                currentKi = Mathf.Min(currentKi, maxKi);
            }

            else if (isExhausted)
            {
                exhaustionTimer -= Time.deltaTime;
                if (exhaustionTimer <= 0)
                {
                    isExhausted = false;
                    currentKi = 0.1f;
                }
            }

            if (SparkingOverloadActive)
                currentKi = maxKi;
            else if (SparkingExhaustActive)
                currentKi = 0f;

            if (!SparkingExhaustActive && !isExhausted && DBPU.BaseConsideredForms.Contains(CurrentForm) && personBehaviour.IsAlive())
            {
                currentKi += (baseRegen * DBPU.KiRegenMultiplier) * Time.deltaTime;
                currentKi = Mathf.Min(currentKi, maxKi);
            }
            else if (isExhausted && !SparkingExhaustActive)
            {
                exhaustionTimer -= Time.deltaTime;
                if (exhaustionTimer <= 0)
                    isExhausted = false;
            }

            if (KiChargeActiveForRegen && !SparkingExhaustActive && !SparkingOverloadActive && personBehaviour.IsAlive())
            {
                currentKi += DBPU.KiChargeHoldRegenPerSecond * Time.deltaTime;
                currentKi = Mathf.Min(currentKi, maxKi);
            }

            if (!personBehaviour.IsAlive() && currentKi > 0)
                currentKi = 0f;

            if (currentTransformation != null)
            {
                if (!SparkingOverloadActive && !SparkingExhaustActive)
                {
                    currentKi -= (currentKiDrainRate * DBPU.KiDrainMultiplier) * Time.deltaTime;
                    currentKi = Mathf.Max(currentKi, 0);
                    if (previousKi > 0 && currentKi <= 0 && CurrentForm != BaseForm)
                        OnKiDepleted();
                    if (currentKi <= 0 && !isExhausted)
                    {
                        isExhausted = true;
                        exhaustionTimer = exhaustionCooldownTime;
                    }
                }
                else if (SparkingExhaustActive)
                    currentKi = 0f;
            }

            previousKi = currentKi;

            barRenderer.material.SetFloat("_RevealAmount", Mathf.Clamp01(currentKi / maxKi));
            ApplyKiBarSparkingPresentation();

            if (StrainRate > 0f || Strain > 0f)
            {
                StrainOn = true;

                Strain += ((StrainRate * StrainRateMultiplier) * DBPU.StrainGainMultiplier) * Time.deltaTime;
                Strain = Mathf.Min(Strain, 1f);

                strainBarRenderer.material = DBPU.barMaterial;
                strainBarRenderer.material.SetFloat("_RevealAmount", Mathf.Clamp01(Strain));

                strainBarRenderer.color = strainBarColor;
                strainBarRenderer.material.SetColor("_TintColor", new Color(strainBarColor.r, strainBarColor.g, strainBarColor.b, 1));
                strainBar.transform.Find("StrainDepletedBar").gameObject.GetComponent<SpriteRenderer>().color = Color.white;
                strainBar.transform.Find("StrainBarFrame").gameObject.GetComponent<SpriteRenderer>().color = Color.white;
            }

            else if (StrainOn == true)
            {
                StrainOn = false;
                strainBarRenderer.material = ModAPI.FindMaterial("Sprites-Default");
                strainBarRenderer.color = Color.clear;
                strainBar.transform.Find("StrainDepletedBar").gameObject.GetComponent<SpriteRenderer>().color = Color.clear;
                strainBar.transform.Find("StrainBarFrame").gameObject.GetComponent<SpriteRenderer>().color = Color.clear;
            }

            if (StrainRate == 0f)
            {
                Strain -= ((0.02f * DBPU.StrainDrainMultiplier) / Mathf.Clamp(4f * Strain, 1f, 4f)) * Time.deltaTime;
                Strain = Mathf.Max(Strain, 0f);
            }
        }

        public void SuppressLegacyHud(bool suppressed)
        {
            if (bar && bar.activeSelf == suppressed) bar.SetActive(!suppressed);
            if (strainBar && strainBar.activeSelf == suppressed) strainBar.SetActive(!suppressed);
        }

        private void ApplyKiBarSparkingPresentation()
        {
            Color normalTint = new Color(barColor.r, barColor.g, barColor.b, 1);
            bool outlineRenderersValid = sparkingOutlineOuterRenderer != null && sparkingOutlineInnerRenderer != null;

            Action disableSparkingOutline = () =>
            {
                if (!outlineRenderersValid) return;
                sparkingOutlineOuterRenderer.enabled = false;
                sparkingOutlineInnerRenderer.enabled = false;
                sparkingOutlineOuterRenderer.color = Color.clear;
                sparkingOutlineInnerRenderer.color = Color.clear;
                sparkingOutlineOuterRenderer.transform.localScale = Vector3.one;
                sparkingOutlineInnerRenderer.transform.localScale = Vector3.one;
            };

            if (sparkingPrepBarRenderer == null)
            {
                if (SparkingOverloadActive)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                    Color brightYellow = new Color(1f, 0.93f, 0.12f, 1f) * 1.25f;
                    Color purpleAcc = new Color(0.48f, 0.1f, 0.98f, 1f);
                    barRenderer.material.SetColor("_TintColor", Color.Lerp(brightYellow, purpleAcc, 0.22f + 0.18f * pulse));
                }
                else if (SparkingExhaustActive)
                    barRenderer.material.SetColor("_TintColor", Color.Lerp(normalTint * 0.35f, new Color(0.35f, 0.12f, 0.48f, 1f), 0.65f));
                else
                    barRenderer.material.SetColor("_TintColor", normalTint);
                disableSparkingOutline();
                return;
            }

            if (SparkingOverloadActive)
            {
                sparkingPrepBarRenderer.enabled = false;
                sparkingPrepBarRenderer.material.SetFloat("_RevealAmount", 0f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                Color brightYellow = new Color(1f, 0.93f, 0.12f, 1f) * 1.25f;
                Color purpleAcc = new Color(0.48f, 0.1f, 0.98f, 1f);
                barRenderer.material.SetColor("_TintColor", Color.Lerp(brightYellow, purpleAcc, 0.22f + 0.18f * pulse));

                if (outlineRenderersValid)
                {
                    float ripple = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
                    float ripple2 = 0.5f + 0.5f * Mathf.Sin(Time.time * 9.25f + 1.3f);
                    sparkingOutlineOuterRenderer.enabled = true;
                    sparkingOutlineInnerRenderer.enabled = true;
                    sparkingOutlineOuterRenderer.color = Color.Lerp(new Color(0.45f, 0.12f, 0.82f, 0.38f), new Color(0.84f, 0.56f, 1f, 0.88f), ripple);
                    sparkingOutlineInnerRenderer.color = Color.Lerp(new Color(0.3f, 0.08f, 0.62f, 0.3f), new Color(0.78f, 0.4f, 1f, 0.72f), ripple2);
                    sparkingOutlineOuterRenderer.transform.localScale = Vector3.one * (1.03f + 0.12f * ripple);
                    sparkingOutlineInnerRenderer.transform.localScale = Vector3.one * (1.01f + 0.065f * ripple2);
                }
                return;
            }

            if (SparkingExhaustActive)
            {
                sparkingPrepBarRenderer.enabled = false;
                sparkingPrepBarRenderer.material.SetFloat("_RevealAmount", 0f);
                barRenderer.material.SetColor("_TintColor", Color.Lerp(normalTint * 0.35f, new Color(0.35f, 0.12f, 0.48f, 1f), 0.65f));
                disableSparkingOutline();
                return;
            }

            if (SparkingOvercharge01 > 0.001f && currentKi >= maxKi * 0.99f)
            {
                barRenderer.material.SetColor("_TintColor", normalTint);
                sparkingPrepBarRenderer.enabled = true;
                sparkingPrepBarRenderer.material.SetFloat("_RevealAmount", Mathf.Clamp01(SparkingOvercharge01));
                disableSparkingOutline();
                return;
            }

            sparkingPrepBarRenderer.enabled = false;
            sparkingPrepBarRenderer.material.SetFloat("_RevealAmount", 0f);
            barRenderer.material.SetColor("_TintColor", normalTint);
            disableSparkingOutline();
        }

                private void OnKiDepleted()
        {
            characterTransformations.ApplyTransformation(personBehaviour, BaseForm, true);
            transformationContextMenu.PositionSelectedFrame = transformationContextMenu.OriginalSelectedFrame;
            transformationContextMenu.LocalSelectedFrame = transformationContextMenu.OriginalSelectedFrame;
            transformationContextMenu.FormSelected = transformationContextMenu.OriginalFormSelected;
            transformationContextMenu.Form = BaseForm;

            gameObject.GetComponent<AuraHandler>().CurrentAuraID = BaseForm;
            gameObject.GetComponent<AuraHandler>().AuraActive = false;

            currentKi += 1;
        }

        private void UpdateKiBarColor(string transformationKey, Sprite sprite = null)
        {
            if (requiredColor != Color.clear)
            {
                barColor = requiredColor * 1.75f;
                barRenderer.material.SetColor("_TintColor", new Color(barColor.r, barColor.g, barColor.b, 1));
                baseColor = barColor;
            }

            else
            {
                Dictionary<string, Color> ogColors = new Dictionary<string, Color>
                {
                    { "Base", Color.cyan },
                    { "SSJ1", new Color(235f / 255f, 175f / 255f, 35f / 255f) },
                    { "SSJGrade2", new Color(235f / 255f, 175f / 255f, 35f / 255f) },
                    { "PSSJ", new Color(235f / 255f, 190f / 255f, 50f / 255f) },
                    { "SSJ2", new Color(235f / 255f, 175f / 255f, 35f / 255f) },
                    { "SSJ3", new Color(255f / 255f, 165f / 255f, 0f / 255f) },
                    { "SSJGod", new Color(217f / 255f, 24f / 255f, 68f / 255f) },
                    { "SSJBlue", new Color(26f / 255f, 223f / 255f, 237f / 255f) },
                    { "PSSJBlue", new Color(46f / 255f, 243f / 255f, 255f / 255f) },
                    { "SSJBlueEvolution", new Color(21f / 255f, 92f / 255f, 245f / 255f) },
                    { "MUI", Color.white },
                    { "FirstForm", new Color(200f / 255f, 35f / 255f, 230f / 255f) },
                    { "SecondForm", new Color(200f / 255f, 35f / 255f, 230f / 255f) },
                    { "ThirdForm", new Color(200f / 255f, 35f / 255f, 230f / 255f) },
                    { "FinalForm", new Color(200f / 255f, 35f / 255f, 230f / 255f) },
                    { "FullPower", new Color(200f / 255f, 35f / 255f, 230f / 255f) },
                    { "GoldenForm", new Color(235f / 255f, 175f / 255f, 35f / 255f) },
                    { "BlackForm", new Color(140f / 255f, 60f / 255f, 220f / 255f) },
                    { "Ikari", new Color(0f / 255f, 255f / 255f, 0f / 255f) },
                    { "FPSSJ", new Color(0f / 255f, 255f / 255f, 0f / 255f) },
                    { "Android", new Color(125f / 255f, 125f / 255f, 125f / 255f) },
                    { "OrangePiccolo", new Color(255f / 255f, 210f / 255f, 50f / 255f) }
                    };

                if (ogColors.TryGetValue(transformationKey, out Color predefinedColor))
                {
                    barColor = predefinedColor * 1.75f;
                    barRenderer.material.SetColor("_TintColor", new Color(barColor.r, barColor.g, barColor.b, 1));
                    baseColor = barColor;
                }

                else
                {
                    barColor = GetAverageColor();
                    barRenderer.material.SetColor("_TintColor", new Color(barColor.r * 2.5f, barColor.g * 2.5f, barColor.b * 2.5f, 1));
                    Debug.LogError(new Color(barColor.r * 2.5f, barColor.g * 2.5f, barColor.b * 2.5f, 1));
                    baseColor = barColor;
                }
            }
        }

        private Color GenerateColor(string key, Color? influenceColor = null)
        {
            int hash = key.GetHashCode();
            float hue = Mathf.Abs(hash % 1000) / 1000f;
            float saturation = 0.8f;
            float brightness = 0.9f;
            Color generatedColor = Color.HSVToRGB(hue, saturation, brightness);

            if (influenceColor.HasValue)
            {
                float blendFactor = 0.5f;
                generatedColor = Color.Lerp(influenceColor.Value, generatedColor, blendFactor);
            }

            return generatedColor;
        }

        private Color GetAverageColor(float brightnessThreshold = 0.1f)
        {
            if (personBehaviour == null || personBehaviour.Limbs == null || personBehaviour.Limbs.Length == 0 || personBehaviour.Limbs[0] == null)
                return Color.black;

            Transform hair = personBehaviour.Limbs[0].transform.Find("Hair");
            if (hair == null)
                return Color.black;

            SpriteRenderer hairSr = hair.GetComponent<SpriteRenderer>();
            if (hairSr == null)
                return Color.black;

            Sprite sprite = hairSr.sprite;
            if (sprite == null)
            {
                return Color.black;
            }

            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                return Color.black;
            }

            Rect spriteRect = sprite.textureRect;

            Color[] pixels;
            try
            {
                pixels = texture.GetPixels(
                    (int)spriteRect.x,
                    (int)spriteRect.y,
                    (int)spriteRect.width,
                    (int)spriteRect.height
                    );
            }
            catch
            {
                return Color.black;
            }

            if (pixels == null || pixels.Length == 0)
            {
                return Color.black;
            }

            Color averageColor = new Color(0, 0, 0, 0);
            int validPixelCount = 0;

            foreach (Color pixel in pixels)
            {
                if (pixel.a > 0)
                {
                    //brightness = 0.2126 * R + 0.7152 * G + 0.0722 * B

                    float brightness = 0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b;

                    if (brightness >= brightnessThreshold)
                    {
                        averageColor += pixel;
                        validPixelCount++;
                    }
                }
            }

            if (validPixelCount > 0)
            {
                averageColor /= validPixelCount;
            }

            else
            {
                return Color.black;
            }

            return averageColor;
        }
    }
}
