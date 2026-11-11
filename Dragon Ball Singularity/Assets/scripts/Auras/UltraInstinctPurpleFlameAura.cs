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
    public class UltraInstinctPurpleFlameAura : FlameAura
    {
        static readonly Color PurpleTint = new Color(1.35f, 1.15f, 3.2f);
        static readonly Color PurpleBase = new Color(0.22f, 0.18f, 0.48f);
        static readonly Color PurpleShineA = new Color(1.5f, 1.25f, 3.4f);
        static readonly Color PurpleShineB = new Color(1.2f, 1.0f, 2.9f);

        void Awake()
        {
            AuraBgScale = 3.85f;
            AuraBgSortOrder = -3;
            MaxAuraStrength = 0.58f;
            DisplayAlpha = 0.62f;
            CaptureAlpha = 0.9f;
            SetMaterial();
        }

        public override void ReadDataFromMaterial()
        {
            base.ReadDataFromMaterial();
            Tint = PurpleTint;
            Tile = 42f;
            OutlinePower = 0.06f;
            OutlineStrength = 0.95f;
            FlameSpeed = 0.75f;
            VerticleSpeed = 6.5f;
            EvolveSpeed = 2.5f;
            MaskThreshold = 0.54f;
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", PurpleTint);
            block.SetColor("_ColorBase", PurpleBase);
            block.SetColor("_ColorShineA", PurpleShineA);
            block.SetColor("_ColorShineB", PurpleShineB);
            block.SetFloat("_GradientPower", 4.5f);
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
