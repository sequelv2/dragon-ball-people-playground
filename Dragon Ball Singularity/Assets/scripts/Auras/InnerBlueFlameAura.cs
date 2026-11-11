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
    public class InnerBlueFlameAura : FlameAura
    {
        static readonly Color BlueTint = new Color(0.45f, 1.05f, 3.6f);
        static readonly Color BlueBase = new Color(0.04f, 0.12f, 0.6f);

        void Awake()
        {
            AuraBgScale = 1.92f;
            AuraBgSortOrder = -1;
            SetMaterial();
        }

        public override void ReadDataFromMaterial()
        {
            base.ReadDataFromMaterial();
            Tint = BlueTint;
            Tile = 38f;
            OutlinePower = 0.1f;
            OutlineStrength = 0.85f;
            FlameSpeed = 1.8f;
            VerticleSpeed = 3.2f;
            EvolveSpeed = 1.8f;
            MaskThreshold = 0.46f;
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", BlueTint);
            block.SetColor("_ColorBase", BlueBase);
            block.SetFloat("_OutlinePower", OutlinePower);
            block.SetFloat("_OutlineStrength", OutlineStrength);
            block.SetFloat("_FlameSpeed", FlameSpeed);
            block.SetFloat("_VerticleSpeed", VerticleSpeed);
            block.SetFloat("_EvolveSpeed", EvolveSpeed);
            block.SetFloat("_MaskThreshold", MaskThreshold);

            bgSR.SetPropertyBlock(block);
        }
    }
}
