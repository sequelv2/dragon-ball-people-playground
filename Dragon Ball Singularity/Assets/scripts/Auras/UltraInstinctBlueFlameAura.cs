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
    public class UltraInstinctBlueFlameAura : FlameAura
    {
        static readonly Color BlueTint = new Color(1.05f, 1.28f, 3.0f);
        static readonly Color BlueFade = new Color(0.14f, 0.17f, 0.42f);

        void Awake()
        {
            AuraBgScale = 2.45f;
            AuraBgSortOrder = -2;
            SetMaterial();
        }

        public override void ReadDataFromMaterial()
        {
            base.ReadDataFromMaterial();
            Tint = BlueTint;
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            Tint = BlueTint;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", Tint);
            block.SetColor("_ColorBase", BlueFade);
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
