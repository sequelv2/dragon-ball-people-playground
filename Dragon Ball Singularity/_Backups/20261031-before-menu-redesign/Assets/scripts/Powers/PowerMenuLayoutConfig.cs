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
    public class PowerMenuLayoutConfig
    {
        public float menuRootUniform = 9.33f;
        public float gridStepX = 20f;
        public float gridGapX = 3f;
        public float gridRowGapY = 3f;
        public float gridRowSpacing = 14f;
        public float gridEdgePaddingX = 6f;
        public float gridEdgePaddingY = 8f;
        public bool gridAutoFitSlots = true;
        public float gridCenterX = 0f;
        public float gridCenterY = 0f;
        public bool gridAlignToCenterCanvas = true;
        public float framesHolderX = 0f;
        public float framesHolderY = -10.1037f;
        public float framesHolderScale = 1.25f;
        public float infoNameLocalX = -0.7254f;
        public float infoNameLocalY = 40.2326f;
        public float infoNameLocalZ = 0f;
        public float infoNameTextW = 72f;
        public float infoNameTextH = 20f;
        public float infoDescLocalX = 1.6637f;
        public float infoDescLocalY = -6.3511f;
        public float infoDescLocalZ = 0f;
        public float infoDescLocalScale = 0.8f;
        public float infoDescTextW = 72f;
        public float infoDescTextH = 70f;
        public float powersTitleLocalX = -0.8f;
        public float powersTitleLocalY = 52.787f;
        public float powersTitleLocalZ = 0f;
        public float powersTitleLocalScale = 1.1f;
        public float powersTitleTextW = 80f;
        public float powersTitleTextH = 20f;
        public float gridRow0Y = 12f;
        public float gridRow1Y = -12f;
        public float gridRow2Y = -30f;
        public float gridSpacingScale = 1f;
        public float iconPadding = 0.008f;
        public float iconFill = 1.12f;
        public float iconScale = 0.94f;
        public float iconScaleX = 0.94f;
        public float iconScaleY = 0.94f;
        public float pageLabelOffsetY = 0f;
        public float configTopY = 44.12391f;
        public float configRowGap = 0f;
        public float configRowScale = 0.9f;
        public float configToggleRowHeight = 11.5f;
        public float configSliderRowHeight = 11.5f;
        public float configPanelOffsetX = -6f;
        public float configNameBarX = -8f;
        public float configRowsLeftOffset = -2.5f;
        public float configRowsOffsetX = 0f;
        public float configRowsOffsetY = 0f;
        public float configSliderRowX = 7.0f;
        public float configToggleLocalNameX = -14f;
        public float configToggleLocalStateX = 43f;
        public float configToggleLocalStateY = 0f;
        public float configActionLocalNameX = 0f;
        public float configSliderValueToTrackGap = 1.5f;
        public float configSliderLocalTrackY = 0f;
        public float configSliderLocalHandleScale = 0.7f;
        public float configSliderNameBarGap = 0f;
        public float configSliderNameBarLeftPad = 10f;
        public float configSliderNameTextInset = 8f;
        public float configSliderHitW = 44f;
        public float configSliderHitH = 7f;
        public float configNameTextSize = 4f;
        public float configValueTextSize = 3.5f;
        public float configPrevX = -34f;
        public float configNextX = -23f;
        public float configNavY = -52.98365f;
        public float configNavScale = 0.55f;
        public float slotSizeMultiplier = 1.1f;
        public PowerMenuLayoutElement centerCanvas = new PowerMenuLayoutElement { x = -5.8521f, y = -7.2151f, targetW = 98.53f, targetH = 111.5434f, uniformScale = 0.9295f };
        public PowerMenuLayoutElement illustration = new PowerMenuLayoutElement { x = -88.3943f, y = -14.4904f, targetW = 65.9646f, targetH = 96.6242f, uniformScale = 0.9291f };
        public PowerMenuLayoutElement info = new PowerMenuLayoutElement { x = 72.5307f, y = -14.8154f, targetW = 57.8617f, targetH = 95.5178f, uniformScale = 0.9184f };
        public PowerMenuLayoutElement slot = new PowerMenuLayoutElement { targetW = 22f, targetH = 22f, uniformScale = 0f };
        public PowerMenuLayoutElement selectFrame = new PowerMenuLayoutElement { targetW = 24f, targetH = 24f, uniformScale = 0f };

        public float GridGapY => gridRowGapY > 0f ? gridRowGapY : gridGapX;

        float SpacingScale => gridSpacingScale > 0.01f ? gridSpacingScale : 1f;

        public Vector2 GetRenderedFrameSize()
        {
            float u = GetFrameUniformScale();
            Vector2 px = MenuUI.SpritePixels(DBPU.PGreenFrame);
            if (px.x <= 0f || px.y <= 0f)
                return new Vector2(slot.targetW, slot.targetH);
            return new Vector2(px.x * u, px.y * u);
        }

        static float PackAxisStep(float inner, float gap, float tight) =>
            ((inner - gap * 2f) / 3f + gap) * tight;

        public void GetGridSteps(out float colStep, out float rowStep, float innerW = 0f, float innerH = 0f)
        {
            float gapX = Mathf.Max(0f, gridGapX);
            float gapY = Mathf.Max(0f, GridGapY);
            float tight = Mathf.Clamp(SpacingScale, 0.65f, 1f);

            if (innerW > 1f && innerH > 1f && gridAutoFitSlots && gridAlignToCenterCanvas)
            {
                colStep = PackAxisStep(innerW, gapX, tight);
                rowStep = colStep;
                return;
            }

            Vector2 frame = GetRenderedFrameSize();
            colStep = frame.x * tight + gapX;
            rowStep = colStep;
        }

        public float ColumnStep
        {
            get { GetGridSteps(out float c, out _); return c; }
        }

        public float RowStep
        {
            get { GetGridSteps(out _, out float r); return r; }
        }

        public float GetFrameUniformScale()
        {
            if (slot.uniformScale > 0f) return slot.uniformScale;
            return MenuUI.UniformScaleToFit(DBPU.PGreenFrame, new Vector2(slot.targetW, slot.targetH));
        }

        public void ApplyAutoSlotSizing(float panelWidth = 0f, float panelHeight = 0f)
        {
            if (!gridAutoFitSlots) return;
            float gapX = Mathf.Max(0f, gridGapX);
            float gapY = Mathf.Max(0f, GridGapY);
            float innerW = panelWidth > 0f ? panelWidth : centerCanvas.targetW;
            float innerH = panelHeight > 0f ? panelHeight : centerCanvas.targetH;
            float holderScale = framesHolderScale > 0.01f ? framesHolderScale : 1f;
            innerW /= holderScale;
            innerH /= holderScale;
            innerW -= gridEdgePaddingX * 2f;
            innerH -= gridEdgePaddingY * 2f;
            float maxW = (innerW - gapX * 2f) / 3f;
            float maxH = (innerH - gapY * 2f) / 3f;
            float cell = Mathf.Min(maxW, maxH);
            float mult = slotSizeMultiplier > 0.01f ? slotSizeMultiplier : 1f;
            mult = Mathf.Min(mult, 1.35f);
            slot.targetW = Mathf.Max(8f, cell * mult);
            slot.targetH = Mathf.Max(8f, cell * mult);
            selectFrame.targetW = slot.targetW;
            selectFrame.targetH = slot.targetH;
        }

        public void ApplyFramesHolderTransform(RectTransform rt)
        {
            if (rt == null) return;
            rt.localPosition = new Vector3(framesHolderX, framesHolderY, 0f);
            rt.anchoredPosition = new Vector2(framesHolderX, framesHolderY);
            rt.localScale = new Vector3(framesHolderScale, framesHolderScale, 1f);
        }

        public void ApplyMenuTextLocalRect(RectTransform rt, float x, float y, float z, float w, float h)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.localPosition = new Vector3(x, y, z);
            rt.localScale = Vector3.one;
        }

        public void ApplyConfigTabPlacement()
        {
            configTab.x = illustration.x;
            configTab.y = onOff.y;
        }

        public PowerMenuLayoutElement close = new PowerMenuLayoutElement { x = 72.2481f, y = -66.79405f, targetW = 28f, targetH = 10.9268f, uniformScale = 0.6829f };
        public PowerMenuLayoutElement onOff = new PowerMenuLayoutElement { x = 23.6938f, y = -67.46935f, targetW = 21f, targetH = 12f, uniformScale = 0.75f };
        public PowerMenuLayoutElement configTab = new PowerMenuLayoutElement { x = -88.3943f, y = -67.46935f, targetW = 28f, targetH = 12f, uniformScale = 0.75f };
        public PowerMenuLayoutElement pageArea = new PowerMenuLayoutElement { x = -35.6431f, y = -67.46935f, targetW = 19.5f, targetH = 12f, uniformScale = 0.75f };
        public PowerMenuLayoutElement previous = new PowerMenuLayoutElement { x = -56f, y = -67.46935f, targetW = 14f, targetH = 12f, uniformScale = 0.75f };
        public PowerMenuLayoutElement next = new PowerMenuLayoutElement { x = -15f, y = -67.46935f, targetW = 14f, targetH = 12f, uniformScale = 0.75f };

        public static PowerMenuLayoutConfig Active;

        public static void Load()
        {
            Active = new PowerMenuLayoutConfig();
        }

        static Vector2 TargetSize(PowerMenuLayoutElement e) => new Vector2(e.targetW, e.targetH);

        public float ScaleFor(Sprite sprite, PowerMenuLayoutElement e) =>
            MenuUI.ResolveUniformScale(sprite, TargetSize(e), e.uniformScale);

        public GameObject CreateElement(Transform parent, string name, Sprite sprite, PowerMenuLayoutElement e)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RefreshElement(go, sprite, e);
            return go;
        }

        public void RefreshElement(GameObject go, Sprite sprite, PowerMenuLayoutElement e)
        {
            if (go == null || sprite == null || e == null) return;
            MenuUI.ApplyImage(go, sprite, new Vector2(e.x, e.y), ScaleFor(sprite, e));
        }

        public void SyncElementFromRect(PowerMenuLayoutElement e, RectTransform rt)
        {
            if (e == null || rt == null) return;
            e.x = rt.anchoredPosition.x;
            e.y = rt.anchoredPosition.y;
            float u = rt.localScale.x;
            e.uniformScale = u;
            e.targetW = rt.sizeDelta.x * u;
            e.targetH = rt.sizeDelta.y * u;
        }

    }
}
