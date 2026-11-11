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
    public class FlameAura : SetAuraBase
    {
        public float Tile = 25.0f;
        public Color Tint = Color.yellow;
        public float OutlinePower = 0.15f;
        public float OutlineStrength = 1.15f;
        public float FlameSpeed = 1.5f;
        public float VerticleSpeed = 2.5f;
        public float EvolveSpeed = 1.5f;
        public float MaskThreshold = 0.5f;

        public override void SetMaterial()
        {
            AuraOutlineMaterial = AuraOutlineController.FlameMat;
        }

        public override void ReadDataFromMaterial()
        {
            ReadedData = true;

            if (AuraOutlineMaterial == null) return;

            Tile = AuraOutlineMaterial.GetFloat("_Tile");
            Tint = AuraOutlineMaterial.GetColor("_Tint");
            OutlinePower = AuraOutlineMaterial.GetFloat("_OutlinePower");
            OutlineStrength = AuraOutlineMaterial.GetFloat("_OutlineStrength");
            FlameSpeed = AuraOutlineMaterial.GetFloat("_FlameSpeed");
            VerticleSpeed = AuraOutlineMaterial.GetFloat("_VerticleSpeed");
            EvolveSpeed = AuraOutlineMaterial.GetFloat("_EvolveSpeed");
            MaskThreshold = AuraOutlineMaterial.GetFloat("_MaskThreshold");
        }

        public override void SetDataToMaterial()
        {
            if (!ReadedData) ReadDataFromMaterial();
            if (bgSR == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bgSR.GetPropertyBlock(block);

            block.SetFloat("_Tile", Tile);
            block.SetColor("_Tint", Tint);
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
