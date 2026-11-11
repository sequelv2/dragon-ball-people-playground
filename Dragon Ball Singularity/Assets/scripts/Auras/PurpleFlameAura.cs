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
    public class PurpleFlameAura : FlameAura
    {
        static readonly Color PurpleTint = new Color(3.4f, 0.72f, 3.25f);
        static readonly Color PurpleFade = new Color(0.55f, 0.28f, 0.62f);

        void Awake()
        {
            AuraBgSortOrder = -2;
            SetMaterial();
        }

        public override void ReadDataFromMaterial()
        {
            base.ReadDataFromMaterial();
            Tint = PurpleTint;
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            Tint = PurpleTint;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", Tint);
            block.SetColor("_ColorBase", PurpleFade);
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
