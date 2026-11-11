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
    public class WhisFlameAura : FlameAura
    {
        static readonly Color WhiteBlueTint = new Color(0.88f, 0.98f, 3.45f);
        static readonly Color WhiteBlueBase = new Color(0.1f, 0.16f, 0.52f);

        void Awake()
        {
            AuraBgScale = 2.45f;
            AuraBgSortOrder = -2;
            SetMaterial();
        }

        public override void ReadDataFromMaterial()
        {
            base.ReadDataFromMaterial();
            Tint = WhiteBlueTint;
            Tile = 36f;
            OutlinePower = 0.12f;
            OutlineStrength = 1.05f;
            FlameSpeed = 1.65f;
            VerticleSpeed = 3.0f;
            EvolveSpeed = 1.65f;
            MaskThreshold = 0.48f;
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", WhiteBlueTint);
            block.SetColor("_ColorBase", WhiteBlueBase);
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
