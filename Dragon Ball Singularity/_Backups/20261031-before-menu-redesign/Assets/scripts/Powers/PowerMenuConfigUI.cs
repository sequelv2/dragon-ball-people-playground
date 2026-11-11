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
    static class PowerMenuConfigUI
    {
        static TMP_FontAsset _configFont;

        static List<PowerMenuConfigEntry> OrderPageEntries(List<PowerMenuConfigEntry> page)
        {
            var ordered = new List<PowerMenuConfigEntry>();
            if (page == null) return ordered;
            for (int i = 0; i < page.Count; i++)
                if (!(page[i] is PowerMenuConfigActionEntry))
                    ordered.Add(page[i]);
            for (int i = 0; i < page.Count; i++)
                if (page[i] is PowerMenuConfigActionEntry)
                    ordered.Add(page[i]);
            return ordered;
        }

        static float GetRenderedRowHeight(PowerMenuConfigEntry entry, PowerMenuLayoutConfig layout, float rowScale)
        {
            if (entry is PowerMenuConfigSliderEntry)
                return GetSliderStackHeight(layout, rowScale);
            return entry.GetRowHeight(layout) * rowScale;
        }

        static float SliderNameBarGap(PowerMenuLayoutConfig layout, float rowScale)
        {
            return Mathf.Max(0f, layout.configSliderNameBarGap) * rowScale;
        }

        static float GetConfigRowStep(PowerMenuConfigEntry entry, PowerMenuLayoutConfig layout, float rowScale)
        {
            return GetRenderedRowHeight(entry, layout, rowScale) + layout.configRowGap;
        }

        static float GetConfigRowsMaxHeight(PowerMenuLayoutConfig layout, float rowScale)
        {
            float panelHalfH = layout.illustration.targetH * 0.5f;
            float navScale = layout.configNavScale > 0.01f ? layout.configNavScale : 0.55f;
            Sprite navSprite = DBPU.PPrevious != null ? DBPU.PPrevious : DBPU.TPrevious;
            float navTop = layout.configNavY;
            if (navSprite != null)
                navTop += MenuUI.SpritePixels(navSprite).y * navScale * 0.5f;
            float bottomLimit = Mathf.Max(-panelHalfH, navTop + layout.configRowGap);
            return Mathf.Max(20f, layout.configTopY - bottomLimit - layout.configRowGap * 2f);
        }

        static void AddEntryToRowPages(List<List<PowerMenuConfigEntry>> result, ref List<PowerMenuConfigEntry> current, ref float currentH, PowerMenuConfigEntry entry, float step, float maxH)
        {
            if (current.Count > 0 && currentH + step > maxH)
            {
                result.Add(current);
                current = new List<PowerMenuConfigEntry>();
                currentH = 0f;
            }
            current.Add(entry);
            currentH += step;
        }

        static List<List<PowerMenuConfigEntry>> BuildConfigRowPages(List<PowerMenuConfigEntry> ordered, PowerMenuLayoutConfig layout, float rowScale)
        {
            var result = new List<List<PowerMenuConfigEntry>>();
            if (ordered == null || ordered.Count == 0)
            {
                result.Add(new List<PowerMenuConfigEntry>());
                return result;
            }

            float maxH = GetConfigRowsMaxHeight(layout, rowScale);
            var current = new List<PowerMenuConfigEntry>();
            float currentH = 0f;

            for (int i = 0; i < ordered.Count; i++)
            {
                float step = GetConfigRowStep(ordered[i], layout, rowScale);
                AddEntryToRowPages(result, ref current, ref currentH, ordered[i], step, maxH);
            }

            if (current.Count > 0)
                result.Add(current);

            return result;
        }

        static List<List<PowerMenuConfigEntry>> GetConfigRowPages(List<PowerMenuConfigEntry> page, PowerMenuLayoutConfig layout, float rowScale)
        {
            return BuildConfigRowPages(OrderPageEntries(page), layout, rowScale);
        }

        static int GetRowPageCount(List<PowerMenuConfigEntry> page, PowerMenuLayoutConfig layout, float rowScale)
        {
            return GetConfigRowPages(page, layout, rowScale).Count;
        }

        static List<PowerMenuConfigEntry> GetRowPageSlice(List<PowerMenuConfigEntry> page, int rowPage, PowerMenuLayoutConfig layout, float rowScale)
        {
            var rowPages = GetConfigRowPages(page, layout, rowScale);
            if (rowPage < 0 || rowPage >= rowPages.Count)
                return new List<PowerMenuConfigEntry>();
            return rowPages[rowPage];
        }

        public class BuildResult
        {
            public GameObject Root;
            public Action Refresh;
            public Action ClearSelection;
            public Action SelectFirstEntry;
        }

        static TMP_FontAsset GetConfigFont()
        {
            if (_configFont == null)
                _configFont = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            return _configFont;
        }

        static void PersistConfigs()
        {
            if (ModConfigs.configurations == null)
                ModConfigs.configurations = new ModConfigs();
            var c = ModConfigs.configurations;
            c.PowerMultiplier = DBPU.PowerMultiplier;
            c.KiDrainMultiplier = DBPU.KiDrainMultiplier;
            c.KiRegenMultiplier = DBPU.KiRegenMultiplier;
            c.KiAttackCostMultiplier = DBPU.KiAttackCostMultiplier;
            c.AurasEnabled = DBPU.AurasEnabled;
            c.KiAtkCanDestroy = DBPU.KiAtkCanDestroy;
            c.KiAtkCanDelete = DBPU.KiAtkCanDelete;
            c.ExtraCharge = DBPU.ExtraCharge;
            c.KiBlast_ExplosionSizeMultiplier = DBPU.KiBlast_ExplosionSizeMultiplier;
            c.KiBlast_CustomColorEnabled = DBPU.KiBlast_CustomColorEnabled;
            c.KiBlast_CustomColorR = DBPU.KiBlast_CustomColorR;
            c.KiBlast_CustomColorG = DBPU.KiBlast_CustomColorG;
            c.KiBlast_CustomColorB = DBPU.KiBlast_CustomColorB;
            c.DamageMultiplier = DBPU.DamageMultiplier;
            c.HealthMultiplier = DBPU.HealthMultiplier;
            c.PhysAtkCanDestroy = DBPU.PhysAtkCanDestroy;
            c.PhysAtkStopLimbs = DBPU.PhysAtkStopLimbs;
            c.AutoEnablePunchKick = DBPU.AutoEnablePunchKick;
            c.Universal_KnockbackMultiplier = DBPU.Universal_KnockbackMultiplier;
            c.DismembermentEnabled = DBPU.DismembermentEnabled;
            c.StrainDrainMultiplier = DBPU.StrainDrainMultiplier;
            c.StrainGainMultiplier = DBPU.StrainGainMultiplier;
            c.StrainDamageMultiplier = DBPU.StrainDamageMultiplier;
            c.KaiokenStrainMultiplier = DBPU.KaiokenStrainMultiplier;
            c.KaiokenMaxMultiplier = DBPU.KaiokenMaxMultiplier;
            c.KaiokenStrainDamageEnabled = DBPU.KaiokenStrainDamageEnabled;
            c.Beam_KnockbackMultiplier = DBPU.Beam_KnockbackMultiplier;
            c.Beam_SpeedMultiplier = DBPU.Beam_SpeedMultiplier;
            c.Beam_SizeMultiplier = DBPU.Beam_SizeMultiplier;
            c.Beam_ToggleDelete = DBPU.Beam_ToggleDelete;
            c.Beam_ToggleDestroy = DBPU.Beam_ToggleDestroy;
            c.Beam_ToggleHeat = DBPU.Beam_ToggleHeat;
            c.BeamAssistEnabled = DBPU.BeamAssistEnabled;
            c.BeamClashShockwaveEnabled = DBPU.BeamClashShockwaveEnabled;
            c.Special_ExponentialGrowthSpeed = DBPU.Special_ExponentialGrowthSpeed;
            c.Special_ZenkaiBoostEnabled = DBPU.Special_ZenkaiBoostEnabled;
            c.Special_ZenkaiBoostScale = DBPU.Special_ZenkaiBoostScale;
            c.Special_ZenkaiBoostMax = DBPU.Special_ZenkaiBoostMax;
            c.VoiceLines = DBPU.VoiceLines;
            c.VoiceLineVolume = DBPU.VoiceLineVolume;
            c.PowersVolume = DBPU.PowersVolume;
            c.KiChargeHoldRegenPerSecond = DBPU.KiChargeHoldRegenPerSecond;
            c.KiChargeAuraScaleMultiplier = DBPU.KiChargeAuraScaleMultiplier;
            c.KiChargeAuraParticleSpeedMultiplier = DBPU.KiChargeAuraParticleSpeedMultiplier;
            c.KiChargeDebrisMaxActive = DBPU.KiChargeDebrisMaxActive;
            c.KiChargeDebrisMaxSession = DBPU.KiChargeDebrisMaxSession;
            c.KiChargeDebrisSpawnInterval = DBPU.KiChargeDebrisSpawnInterval;
            c.KiChargeDebrisRingMin = DBPU.KiChargeDebrisRingMin;
            c.KiChargeDebrisRingMax = DBPU.KiChargeDebrisRingMax;
            c.KiChargeDebrisSpawnYOffset = DBPU.KiChargeDebrisSpawnYOffset;
            c.CinematicVfxRevision = 1;
            c.KiChargeFxEnabled = DBPU.KiChargeFxEnabled;
            c.KiChargeAutoTransformWhileChargingEnabled = DBPU.KiChargeAutoTransformWhileChargingEnabled;
            c.KiChargeAutoTransformSecondsPerTier = DBPU.KiChargeAutoTransformSecondsPerTier;
            c.TransformationEyeGlowEnabled = DBPU.TransformationEyeGlowEnabled;
            c.TransformationHairGlowEnabled = DBPU.TransformationHairGlowEnabled;
            c.TransformationHairGlowBrightness = DBPU.TransformationHairGlowBrightness;
            c.QuickTransformEnabled = DBPU.QuickTransformEnabled;
            c.UiAuraScale = DBPU.UiAuraScale;
            c.UiAuraYOffset = DBPU.UiAuraYOffset;
            c.KiDeflectEnabled = DBPU.KiDeflectEnabled;
            c.KiDeflectHybridPowerThreshold = DBPU.KiDeflectHybridPowerThreshold;
            c.KiDeflectBlastThresholdMultiplier = DBPU.KiDeflectBlastThresholdMultiplier;
            c.KiDeflectBeamThresholdMultiplier = DBPU.KiDeflectBeamThresholdMultiplier;
            c.KiDeflectBeamCatchThenAim = DBPU.KiDeflectBeamCatchThenAim;
            c.KiDeflectBeamCatchSeconds = DBPU.KiDeflectBeamCatchSeconds;
            c.KiDeflectBeamSplitEnabled = DBPU.KiDeflectBeamSplitEnabled;
            c.KiDeflectBeamSplitThresholdMultiplier = DBPU.KiDeflectBeamSplitThresholdMultiplier;
            c.KiDeflectBeamSplitRayCount = DBPU.KiDeflectBeamSplitRayCount;
            c.KiDeflectBeamSplitWrapDistance = DBPU.KiDeflectBeamSplitWrapDistance;
            c.KiDeflectBeamSplitContinuationLength = DBPU.KiDeflectBeamSplitContinuationLength;
            c.KiDeflectBeamSplitArcDegrees = DBPU.KiDeflectBeamSplitArcDegrees;
            c.KiDeflectBeamSplitLifetime = DBPU.KiDeflectBeamSplitLifetime;
            c.SparkingOverloadSeconds = DBPU.SparkingOverloadSeconds;
            c.SparkingExhaustSeconds = DBPU.SparkingExhaustSeconds;
            c.SparkingCooldownSeconds = DBPU.SparkingCooldownSeconds;
            c.SparkingOverchargeHoldSeconds = DBPU.SparkingOverchargeHoldSeconds;
            c.KiChargeSparkingOverchargeEnabled = DBPU.KiChargeSparkingOverchargeEnabled;
            c.KiOverdriveOutputEnabled = DBPU.KiOverdriveOutputEnabled;
            c.KiOverdriveOutputKey = DbpuInputRegistration.GetActionKey(DBPU.KiOverdriveOutputActionId, DBPU.KiOverdriveOutputDefaultKey);
            c.KiChargeHoldKey = DbpuInputRegistration.GetActionKey(DBPU.KiChargeHoldActionId, KeyCode.R);
            c.KiOverdriveMaxPercent = DBPU.KiOverdriveMaxPercent;
            c.KiOverdriveSecondsBeforeExhaust = DBPU.KiOverdriveSecondsBeforeExhaust;
            ModConfigs.Save();
        }

        static GameObject PlaceSprite(Transform parent, string name, Sprite sprite, Vector2 pos, float uniform)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MenuUI.ApplyImage(go, sprite, pos, uniform);
            return go;
        }

        static GameObject PlaceLocalSprite(Transform parent, string name, Sprite sprite, Vector2 localPos, float uniform)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta = MenuUI.SpritePixels(sprite);
            rt.localScale = new Vector3(uniform, uniform, 1f);
            return go;
        }

        static GameObject PlaceConfigBar(Transform parent, string name, Sprite sprite, Vector2 pos, float uniform)
        {
            GameObject go = PlaceSprite(parent, name, sprite, pos, uniform);
            MenuUI.DisableRaycast(go.GetComponent<Image>());
            return go;
        }

        static float GetConfigRowsLeftX(PowerMenuLayoutConfig layout)
        {
            Sprite prevSprite = DBPU.PPrevious != null ? DBPU.PPrevious : DBPU.TPrevious;
            float navScale = layout.configNavScale > 0.01f ? layout.configNavScale : 0.55f;
            float prevHalfW = MenuUI.SpritePixels(prevSprite).x * navScale * 0.5f;
            return layout.configPrevX - prevHalfW + layout.configRowsLeftOffset;
        }

        static float GetSliderRowCenterX(PowerMenuLayoutConfig layout, float rowScale)
        {
            float sliderW = MenuUI.SpritePixels(DBPU.PConfigTextSlider).x * rowScale;
            return GetConfigRowsLeftX(layout) + sliderW * 0.5f;
        }

        static float GetToggleBarCenterX(PowerMenuLayoutConfig layout, float rowScale)
        {
            float toggleW = MenuUI.SpritePixels(DBPU.PConfigTextToggle).x * rowScale;
            return GetConfigRowsLeftX(layout) + toggleW * 0.5f;
        }

        static TextMeshProUGUI PlaceRowText(Transform parent, string name, string text, Vector2 localPos, float fontSize, TextAlignmentOptions align, float boxW = 36f, bool autoFit = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(boxW, 10f);
            rt.localScale = Vector3.one;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = GetConfigFont();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.black;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (autoFit)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 2f;
                tmp.fontSizeMax = fontSize;
            }
            tmp.raycastTarget = false;
            return tmp;
        }

        static float GetSliderStackHeight(PowerMenuLayoutConfig layout, float rowScale)
        {
            float gap = SliderNameBarGap(layout, rowScale);
            float nameH = MenuUI.SpritePixels(DBPU.PConfigTextSlider).y * rowScale;
            float valueH = MenuUI.SpritePixels(DBPU.PSliderValue).y * rowScale;
            return nameH + gap + valueH;
        }

        static void BuildSliderRow(GameObject rowGo, PowerMenuLayoutConfig layout, PowerMenuConfigSliderEntry slider, float rowScale, Action persist)
        {
            float gap = SliderNameBarGap(layout, rowScale);
            Vector2 namePx = MenuUI.SpritePixels(DBPU.PConfigTextSlider);
            Vector2 valuePx = MenuUI.SpritePixels(DBPU.PSliderValue);
            float nameH = namePx.y * rowScale;
            float valueH = valuePx.y * rowScale;
            float totalH = nameH + gap + valueH;
            float nameY = totalH * 0.5f - nameH * 0.5f;
            float bottomY = -totalH * 0.5f + valueH * 0.5f;

            GameObject nameBarGo = new GameObject("CfgNameBar");
            nameBarGo.transform.SetParent(rowGo.transform, false);
            Image nameImg = nameBarGo.AddComponent<Image>();
            nameImg.sprite = DBPU.PConfigTextSlider;
            nameImg.color = Color.white;
            nameImg.raycastTarget = false;
            RectTransform nameRt = nameBarGo.GetComponent<RectTransform>();
            nameRt.pivot = new Vector2(0.5f, 0.5f);
            nameRt.anchorMin = new Vector2(0.5f, 0.5f);
            nameRt.anchorMax = new Vector2(0.5f, 0.5f);
            nameRt.anchoredPosition = new Vector2(0f, nameY);
            nameRt.sizeDelta = namePx;
            nameRt.localScale = new Vector3(rowScale, rowScale, 1f);

            float nameSize = layout.configNameTextSize > 0.01f ? layout.configNameTextSize : 4f;
            float textPad = layout.configSliderNameTextInset > 0f ? layout.configSliderNameTextInset * 0.25f : 2f;
            float nameTextW = Mathf.Max(8f, namePx.x - textPad * 2f);
            PlaceRowText(nameBarGo.transform, "CfgNameText", slider.Name, Vector2.zero, nameSize, TextAlignmentOptions.Midline, nameTextW);

            float nameRenderedW = namePx.x * rowScale;
            float valueRenderedW = valuePx.x * rowScale;
            float rowLeftX = -nameRenderedW * 0.5f;
            float valueCenterX = rowLeftX + valueRenderedW * 0.5f;

            GameObject valueBarGo = new GameObject("CfgValueBar");
            valueBarGo.transform.SetParent(rowGo.transform, false);
            Image valueBarImg = valueBarGo.AddComponent<Image>();
            valueBarImg.sprite = DBPU.PSliderValue;
            valueBarImg.color = Color.white;
            valueBarImg.raycastTarget = false;
            RectTransform valueBarRt = valueBarGo.GetComponent<RectTransform>();
            valueBarRt.pivot = new Vector2(0.5f, 0.5f);
            valueBarRt.anchorMin = new Vector2(0.5f, 0.5f);
            valueBarRt.anchorMax = new Vector2(0.5f, 0.5f);
            valueBarRt.anchoredPosition = new Vector2(valueCenterX, bottomY);
            valueBarRt.sizeDelta = valuePx;
            valueBarRt.localScale = new Vector3(rowScale, rowScale, 1f);

            float valueSize = layout.configValueTextSize > 0.01f ? layout.configValueTextSize : 3.5f;
            float valueTextW = Mathf.Max(4f, valuePx.x - 2f);
            TextMeshProUGUI valueTmp = PlaceRowText(valueBarGo.transform, "CfgValueText", slider.FormatDisplay(slider.ReadDisplay()), Vector2.zero, valueSize, TextAlignmentOptions.Center, valueTextW, false);

            float handleScale = layout.configSliderLocalHandleScale;
            float slotW = layout.configSliderHitW;
            float slotH = layout.configSliderHitH;
            float halfW = slotW * 0.5f;
            float trackGap = layout.configSliderValueToTrackGap;
            float trackCenterX = rowLeftX + valueRenderedW + trackGap + halfW;

            GameObject slotGo = new GameObject("CfgSliderSlot");
            slotGo.transform.SetParent(rowGo.transform, false);
            RectTransform slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.anchoredPosition = new Vector2(trackCenterX, bottomY + layout.configSliderLocalTrackY);
            slotRt.sizeDelta = new Vector2(slotW, slotH);
            slotRt.localScale = Vector3.one;

            GameObject trackGo = new GameObject("CfgTrack");
            trackGo.transform.SetParent(slotGo.transform, false);
            Image trackImg = trackGo.AddComponent<Image>();
            trackImg.sprite = DBPU.PSliderBar;
            trackImg.color = Color.white;
            trackImg.raycastTarget = false;
            RectTransform trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(slotW, MenuUI.SpritePixels(DBPU.PSliderBar).y);

            float display = slider.ReadDisplay();
            float t = slider.Max > slider.Min ? Mathf.Clamp01((display - slider.Min) / (slider.Max - slider.Min)) : 0f;
            float handleX = -halfW + t * slotW;
            GameObject handleGo = PlaceLocalSprite(slotGo.transform, "CfgHandle", DBPU.PSliderHandle, new Vector2(handleX, 0f), handleScale);
            MenuUI.DisableRaycast(handleGo.GetComponent<Image>());

            GameObject hitGo = new GameObject("CfgSliderHit");
            hitGo.transform.SetParent(slotGo.transform, false);
            RectTransform hitRt = hitGo.AddComponent<RectTransform>();
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;
            hitRt.localScale = Vector3.one;
            Image hitImg = hitGo.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;

            var interactor = hitGo.AddComponent<PowerMenuConfigSliderInteractor>();
            interactor.trackRt = slotRt;
            interactor.handleRt = handleGo.GetComponent<RectTransform>();
            interactor.valueText = valueTmp;
            interactor.min = slider.Min;
            interactor.max = slider.Max;
            interactor.trackHalfW = halfW;
            interactor.readDisplay = slider.ReadDisplay;
            interactor.writeDisplay = v => slider.WriteDisplay(Mathf.Clamp(v, slider.Min, slider.Max));
            interactor.formatDisplay = () => slider.FormatDisplay(slider.ReadDisplay());
            interactor.persist = persist;
            interactor.RefreshVisual();
            hitGo.transform.SetAsLastSibling();
        }

        static void RefreshConfigPageValues(Transform rowsHolder, List<PowerMenuConfigEntry> page)
        {
            if (rowsHolder == null || page == null) return;
            int count = Mathf.Min(rowsHolder.childCount, page.Count);
            for (int row = 0; row < count; row++)
            {
                var entry = page[row];
                Transform bar = rowsHolder.GetChild(row);
                if (entry is PowerMenuConfigToggleEntry toggle)
                {
                    Transform stateTr = bar.Find("CfgState");
                    if (stateTr == null) continue;
                    Image stateImg = stateTr.GetComponent<Image>();
                    if (stateImg == null) continue;
                    bool state = toggle.GetValue != null && toggle.GetValue();
                    stateImg.sprite = state ? DBPU.PGreenButton : DBPU.PRedButton;
                }
                else if (entry is PowerMenuConfigSliderEntry)
                {
                    Transform hitTr = bar.Find("CfgSliderSlot/CfgSliderHit");
                    if (hitTr == null) continue;
                    var interactor = hitTr.GetComponent<PowerMenuConfigSliderInteractor>();
                    if (interactor != null) interactor.RefreshVisual();
                }
            }
        }

        static void PlaceBarClickAction(GameObject barGo, Action onClick)
        {
            GameObject hitGo = new GameObject("CfgActionHit");
            hitGo.transform.SetParent(barGo.transform, false);
            RectTransform hitRt = hitGo.AddComponent<RectTransform>();
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;
            hitRt.localScale = Vector3.one;
            Image hitImg = hitGo.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;
            Button btn = hitGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            hitGo.transform.SetAsFirstSibling();
        }

        static Sprite GetSelectFrameSprite(PowerMenuConfigEntry entry)
        {
            if (entry is PowerMenuConfigSliderEntry)
                return DBPU.PConfigTextSliderSel;
            return DBPU.PConfigTextToggleSel;
        }

        static Transform GetSelectFrameParent(Transform rowBar, PowerMenuConfigEntry entry)
        {
            if (entry is PowerMenuConfigSliderEntry)
            {
                Transform nameBar = rowBar.Find("CfgNameBar");
                if (nameBar != null) return nameBar;
            }
            return rowBar;
        }

        static void SetBarSelectFrame(Transform barGo, bool selected, Sprite frameSprite)
        {
            Transform existing = barGo.Find("SelectFrame");
            if (!selected)
            {
                if (existing != null) GameObject.Destroy(existing.gameObject);
                return;
            }
            if (existing != null) return;
            GameObject selGo = new GameObject("SelectFrame");
            selGo.transform.SetParent(barGo, false);
            MenuUI.ApplyImage(selGo, frameSprite, Vector2.zero, 1f);
            MenuUI.DisableRaycast(selGo.GetComponent<Image>());
            selGo.transform.SetAsLastSibling();
        }

        static void RefreshSelectFrames(Transform rowsHolder, List<PowerMenuConfigEntry> page, string selectedId)
        {
            if (rowsHolder == null || page == null) return;
            int count = Mathf.Min(rowsHolder.childCount, page.Count);
            for (int row = 0; row < count; row++)
            {
                var entry = page[row];
                Transform bar = rowsHolder.GetChild(row);
                bool sel = !string.IsNullOrEmpty(selectedId) && entry.ID == selectedId;
                SetBarSelectFrame(GetSelectFrameParent(bar, entry), sel, GetSelectFrameSprite(entry));
            }
        }

        public static BuildResult Build(Transform canvasParent, PowerMenuLayoutConfig layout, PhysicalBehaviour phys, TextMeshProUGUI infoNameText, TextMeshProUGUI infoDescText, Func<List<List<PowerMenuConfigEntry>>> getPages)
        {
            GameObject root = new GameObject("ConfigPanel");
            root.transform.SetParent(canvasParent, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchoredPosition = new Vector2(layout.illustration.x + layout.configPanelOffsetX, layout.illustration.y);
            rootRt.sizeDelta = new Vector2(layout.illustration.targetW, layout.illustration.targetH);
            root.SetActive(false);

            GameObject rowsHolder = new GameObject("ConfigRows");
            rowsHolder.transform.SetParent(root.transform, false);
            RectTransform rowsHolderRt = rowsHolder.AddComponent<RectTransform>();
            rowsHolderRt.anchoredPosition = new Vector2(layout.configRowsOffsetX, layout.configRowsOffsetY);

            List<List<PowerMenuConfigEntry>> pages = getPages != null ? getPages() : new List<List<PowerMenuConfigEntry>>();
            int configPage = 0;
            int configRowPage = 0;
            float rowScale = layout.configRowScale > 0.01f ? layout.configRowScale : 0.9f;

            Image prevNavImg = null;
            Image nextNavImg = null;
            TextMeshProUGUI configPageLabel = null;
            string selectedEntryId = null;

            Action clearSelection = () =>
            {
                selectedEntryId = null;
                if (infoDescText != null) infoDescText.text = "";
            };

            Action<PowerMenuConfigEntry> showEntryInfo = (entry) =>
            {
                if (entry == null) return;
                if (infoDescText != null) infoDescText.text = entry.Description ?? entry.Name ?? "";
            };

            List<PowerMenuConfigEntry> currentRowPage = new List<PowerMenuConfigEntry>();

            Action<PowerMenuConfigEntry> selectEntry = (entry) =>
            {
                if (entry == null) return;
                selectedEntryId = entry.ID;
                showEntryInfo(entry);
                RefreshSelectFrames(rowsHolder.transform, currentRowPage, selectedEntryId);
            };

            Action reloadPages = () =>
            {
                pages = getPages != null ? getPages() : new List<List<PowerMenuConfigEntry>>();
                if (configPage >= pages.Count) configPage = Mathf.Max(0, pages.Count - 1);
                if (configPage < 0) configPage = 0;
                configRowPage = 0;
            };

            Action rebuildPage = null;
            rebuildPage = () =>
            {
                for (int i = rowsHolder.transform.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(rowsHolder.transform.GetChild(i).gameObject);

                if (pages == null || pages.Count == 0)
                {
                    if (infoDescText != null) infoDescText.text = "This power has no configurable options.";
                    return;
                }

                if (configPage < 0 || configPage >= pages.Count) return;

                var fullPage = OrderPageEntries(pages[configPage]);
                int rowPageCount = GetRowPageCount(fullPage, layout, rowScale);
                if (configRowPage >= rowPageCount) configRowPage = rowPageCount - 1;
                if (configRowPage < 0) configRowPage = 0;
                var page = GetRowPageSlice(pages[configPage], configRowPage, layout, rowScale);
                currentRowPage = page;

                float rowTopY = layout.configTopY;

                for (int row = 0; row < page.Count; row++)
                {
                    var entry = page[row];
                    float rowH = GetRenderedRowHeight(entry, layout, rowScale);
                    float rowCenterY = rowTopY - rowH * 0.5f;

                    if (entry is PowerMenuConfigToggleEntry toggle)
                    {
                        GameObject barGo = PlaceConfigBar(rowsHolder.transform, "CfgBar" + row, DBPU.PConfigTextToggle, new Vector2(GetToggleBarCenterX(layout, rowScale), rowCenterY), rowScale);
                        float nameSize = layout.configNameTextSize > 0.01f ? layout.configNameTextSize : 4f;
                        float toggleBarHalfW = MenuUI.SpritePixels(DBPU.PConfigTextToggle).x * 0.5f;
                        float toggleNameLeftX = -toggleBarHalfW + 1f;
                        float toggleNameRightX = layout.configToggleLocalStateX - 2f;
                        float toggleTextW = Mathf.Max(8f, toggleNameRightX - toggleNameLeftX);
                        float toggleTextX = (toggleNameLeftX + toggleNameRightX) * 0.5f;
                        PlaceRowText(barGo.transform, "CfgNameText", toggle.Name, new Vector2(toggleTextX, 0f), nameSize, TextAlignmentOptions.Midline, toggleTextW);
                        var toggleEntry = toggle;
                        PlaceBarClickAction(barGo, () => selectEntry(toggleEntry));

                        bool state = toggle.GetValue != null && toggle.GetValue();
                        float stateY = layout.configToggleLocalStateY;
                        GameObject stateGo = PlaceLocalSprite(barGo.transform, "CfgState", state ? DBPU.PGreenButton : DBPU.PRedButton, new Vector2(layout.configToggleLocalStateX, stateY), 0.95f);
                        Button stateBtn = stateGo.AddComponent<Button>();
                        stateBtn.transition = Selectable.Transition.None;
                        string toggleId = toggle.ID;
                        stateBtn.onClick.AddListener(() =>
                        {
                            toggle.Toggle();
                            rebuildPage();
                        });
                    }
                    else if (entry is PowerMenuConfigSliderEntry slider)
                    {
                        Vector2 namePx = MenuUI.SpritePixels(DBPU.PConfigTextSlider);
                        float stackH = rowH;

                        GameObject rowGo = new GameObject("CfgBar" + row);
                        rowGo.transform.SetParent(rowsHolder.transform, false);
                        RectTransform rowRt = rowGo.AddComponent<RectTransform>();
                        rowRt.anchoredPosition = new Vector2(GetSliderRowCenterX(layout, rowScale), rowCenterY);
                        rowRt.sizeDelta = new Vector2(namePx.x * rowScale, stackH);
                        BuildSliderRow(rowGo, layout, slider, rowScale, null);
                        var sliderEntry = slider;
                        Transform nameBar = rowGo.transform.Find("CfgNameBar");
                        if (nameBar != null)
                            PlaceBarClickAction(nameBar.gameObject, () => selectEntry(sliderEntry));
                    }
                    else if (entry is PowerMenuConfigActionEntry action)
                    {
                        GameObject barGo = PlaceConfigBar(rowsHolder.transform, "CfgBar" + row, DBPU.PConfigTextToggle, new Vector2(GetToggleBarCenterX(layout, rowScale), rowCenterY), rowScale);
                        float actionNameSize = layout.configNameTextSize > 0.01f ? layout.configNameTextSize : 4f;
                        float actionTextW = Mathf.Max(8f, MenuUI.SpritePixels(DBPU.PConfigTextToggle).x - 2f);
                        PlaceRowText(barGo.transform, "CfgActionText", action.Name, new Vector2(0f, 0f), actionNameSize, TextAlignmentOptions.Midline, actionTextW);

                        string actionId = action.ID;
                        var actionEntry = action;
                        PlaceBarClickAction(barGo, () =>
                        {
                            selectEntry(actionEntry);
                            action.OnClick?.Invoke();
                            RefreshConfigPageValues(rowsHolder.transform, page);
                        });
                    }

                    rowTopY -= GetConfigRowStep(entry, layout, rowScale);
                }

                if (prevNavImg != null)
                {
                    bool canPrev = configPage > 0 || configRowPage > 0;
                    bool canNext = configPage < pages.Count - 1 || configRowPage < rowPageCount - 1;
                    prevNavImg.color = canPrev ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    nextNavImg.color = canNext ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                }

                if (configPageLabel != null)
                {
                    if (rowPageCount > 1)
                    {
                        configPageLabel.gameObject.SetActive(true);
                        configPageLabel.text = (configRowPage + 1) + " / " + rowPageCount;
                    }
                    else
                        configPageLabel.gameObject.SetActive(false);
                }

                RefreshSelectFrames(rowsHolder.transform, page, selectedEntryId);
            };

            Sprite prevSprite = DBPU.PPrevious != null ? DBPU.PPrevious : DBPU.TPrevious;
            Sprite nextSprite = DBPU.PNext != null ? DBPU.PNext : DBPU.TNext;
            GameObject cfgPrev = PlaceSprite(root.transform, "ConfigPrev", prevSprite, new Vector2(layout.configPrevX, layout.configNavY), layout.configNavScale);
            prevNavImg = cfgPrev.GetComponent<Image>();
            cfgPrev.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (configRowPage > 0)
                {
                    configRowPage--;
                }
                else if (configPage > 0)
                {
                    configPage--;
                    configRowPage = GetRowPageCount(OrderPageEntries(pages[configPage]), layout, rowScale) - 1;
                }
                else return;
                clearSelection();
                rebuildPage();
            });

            GameObject cfgNext = PlaceSprite(root.transform, "ConfigNext", nextSprite, new Vector2(layout.configNextX, layout.configNavY), layout.configNavScale);
            nextNavImg = cfgNext.GetComponent<Image>();

            GameObject cfgPageLabelGo = new GameObject("ConfigPageLabel");
            cfgPageLabelGo.transform.SetParent(root.transform, false);
            RectTransform cfgPageLabelRt = cfgPageLabelGo.AddComponent<RectTransform>();
            cfgPageLabelRt.anchoredPosition = new Vector2((layout.configPrevX + layout.configNextX) * 0.5f, layout.configNavY + 6f);
            cfgPageLabelRt.sizeDelta = new Vector2(24f, 8f);
            configPageLabel = cfgPageLabelGo.AddComponent<TextMeshProUGUI>();
            configPageLabel.font = GetConfigFont();
            configPageLabel.fontSize = 5;
            configPageLabel.alignment = TextAlignmentOptions.Center;
            configPageLabel.color = Color.black;
            configPageLabel.raycastTarget = false;
            configPageLabel.gameObject.SetActive(false);

            cfgNext.AddComponent<Button>().onClick.AddListener(() =>
            {
                var fullPage = OrderPageEntries(pages[configPage]);
                int rowPageCount = GetRowPageCount(fullPage, layout, rowScale);
                if (configRowPage < rowPageCount - 1)
                {
                    configRowPage++;
                }
                else if (configPage < pages.Count - 1)
                {
                    configPage++;
                    configRowPage = 0;
                }
                else return;
                clearSelection();
                rebuildPage();
            });

            Action selectFirstEntry = () =>
            {
                if (configPage < 0 || configPage >= pages.Count) return;
                var page = GetRowPageSlice(pages[configPage], configRowPage, layout, rowScale);
                if (page.Count > 0) selectEntry(page[0]);
            };

            Action refreshAll = () =>
            {
                reloadPages();
                rebuildPage();
            };

            refreshAll();

            return new BuildResult { Root = root, Refresh = refreshAll, ClearSelection = clearSelection, SelectFirstEntry = selectFirstEntry };
        }
    }
}
