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
    public class SetAuraBase : MonoBehaviour
    {
        public Material AuraOutlineMaterial;
        public bool isCustomSize;
        public Vector2 customSize = new Vector2(512, 512);
        SpriteRenderer originalSR;
        internal SpriteRenderer bgSR;
        internal SpriteRenderer cpSR;

        public float AuraBgScale = 2.5f;
        public float AuraStrength = 0f;
        protected float MaxAuraStrength = 0.7f;
        protected float DisplayAlpha = 1f;
        protected float CaptureAlpha = 1f;
        protected int AuraBgSortOrder = -1;
        internal bool ReadedData = false;
        public bool AuraOn = false;

        private void Awake()
        {
            SetMaterial();
        }

        void Start()
        {
            originalSR = GetComponent<SpriteRenderer>();
            if (originalSR == null || originalSR.sprite == null) return;
            if (AuraOutlineController.main == null || AuraOutlineController.FlameMat == null) return;

            Sprite originalSprite = originalSR.sprite;

            GameObject captureObj = new GameObject("CaptureSprite");
            captureObj.layer = LayerMask.NameToLayer(AuraOutlineController.main.AuraLayerName);
            captureObj.transform.SetParent(transform, false);

            cpSR = captureObj.AddComponent<SpriteRenderer>();
            cpSR.sprite = originalSprite;
            cpSR.material = originalSR.material;
            cpSR.sortingLayerName = "Bottom";
            cpSR.sortingOrder = -10;

            GameObject bgObj = new GameObject("BackgroundSprite");
            bgObj.transform.SetParent(transform, false);
            bgObj.transform.localScale = Vector2.one * AuraBgScale;

            bgSR = bgObj.AddComponent<SpriteRenderer>();
            bgSR.sprite = originalSprite;
            bgSR.material = AuraOutlineMaterial;
            bgSR.sortingLayerName = "Background";
            bgSR.sortingOrder = AuraBgSortOrder;

            if (!ReadedData)
            {
                ReadDataFromMaterial();
                ReadedData = true;
            }

            LateUpdate();
        }

        public void EnableAura()
        {
            if (originalSR.color == Color.clear) return;

            StopAllCoroutines();
            AuraOn = true;
            StartCoroutine(DoEnableAura());
        }

        public void DisableAura()
        {
            StopAllCoroutines();
            AuraOn = false;
            StartCoroutine(DoDisableAura());
        }

        public void SetAuraState(bool state)
        {
            AuraOn = state;
            if (!AuraOn) DisableAura();
            else EnableAura();
        }

        private IEnumerator DoEnableAura()
        {
            while (AuraOn == true && AuraStrength < MaxAuraStrength)
            {
                AuraStrength += 0.025f;
                yield return new WaitForSeconds(0.025f);
            }

            yield return null;
        }

        private IEnumerator DoDisableAura()
        {
            while (AuraOn == false && AuraStrength > 0f)
            {
                AuraStrength -= 0.025f;
                yield return new WaitForSeconds(0.025f);
            }

            yield return null;
        }

        private void LateUpdate()
        {
            if (originalSR == null || cpSR == null || bgSR == null) return;

            AuraStrength = Mathf.Clamp(AuraStrength, 0f, MaxAuraStrength);
            float alpha = AuraStrength * DisplayAlpha;
            cpSR.color = new Color(1f, 1f, 1f, AuraStrength * CaptureAlpha);
            bgSR.color = new Color(1f, 1f, 1f, alpha);
            cpSR.sprite = originalSR.sprite;
            bgSR.sprite = originalSR.sprite;
            bgSR.material = AuraOutlineMaterial;
            bgSR.sortingLayerName = "Background";
            bgSR.sortingOrder = AuraBgSortOrder;
            cpSR.material = originalSR.material;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            originalSR.GetPropertyBlock(block);
            cpSR.SetPropertyBlock(block);

            SetDataToMaterial();
        }

        public virtual void SetMaterial()
        {
        }

        public virtual void ReadDataFromMaterial()
        {
        }

        public virtual void SetDataToMaterial()
        {
        }

        void OnDestroy()
        {
            if (bgSR != null) Destroy(bgSR.gameObject);
            if (cpSR != null) Destroy(cpSR.gameObject);
        }
    }
}
