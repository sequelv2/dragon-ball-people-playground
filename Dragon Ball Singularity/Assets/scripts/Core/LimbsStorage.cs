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
    public class LimbsStorage
    {
        public static Texture2D FHead;
        public static Texture2D BHead;
        public static Texture2D DHead;

        public static Texture2D FUpper;
        public static Texture2D BUpper;
        public static Texture2D DUpper;

        public static Texture2D FMiddle;
        public static Texture2D BMiddle;
        public static Texture2D DMiddle;

        public static Texture2D FLower;
        public static Texture2D BLower;
        public static Texture2D DLower;

        public static Texture2D FULeg;
        public static Texture2D BULeg;
        public static Texture2D DULeg;

        public static Texture2D FLLeg;
        public static Texture2D BLLeg;
        public static Texture2D DLLeg;

        public static Texture2D FFoot;
        public static Texture2D BFoot;
        public static Texture2D DFoot;

        public static Texture2D FUArm;
        public static Texture2D BUArm;
        public static Texture2D DUArm;

        public static Texture2D FLArm;
        public static Texture2D BLArm;
        public static Texture2D DLArm;

        public static List<Texture2D> FleshLayer;
        public static List<Texture2D> BoneLayer;
        public static List<Texture2D> DamageLayer;

        public static DecalDescriptor HumanBloodDecal;

        public static void Load()
        {
            if (FHead != null) return;

            FHead = ModAPI.LoadTexture("Assets/Sprites/damage/flesh head.png");
            BHead = ModAPI.LoadTexture("Assets/Sprites/damage/bone head.png");
            DHead = ModAPI.LoadTexture("Assets/Sprites/damage/damage head.png");

            FUpper = ModAPI.LoadTexture("Assets/Sprites/damage/flesh upper.png");
            BUpper = ModAPI.LoadTexture("Assets/Sprites/damage/bone upper.png");
            DUpper = ModAPI.LoadTexture("Assets/Sprites/damage/damage upper.png");

            FMiddle = ModAPI.LoadTexture("Assets/Sprites/damage/flesh middle.png");
            BMiddle = ModAPI.LoadTexture("Assets/Sprites/damage/bone middle.png");
            DMiddle = ModAPI.LoadTexture("Assets/Sprites/damage/damage middle.png");

            FLower = ModAPI.LoadTexture("Assets/Sprites/damage/flesh lower.png");
            BLower = ModAPI.LoadTexture("Assets/Sprites/damage/bone lower.png");
            DLower = ModAPI.LoadTexture("Assets/Sprites/damage/damage lower.png");

            FULeg = ModAPI.LoadTexture("Assets/Sprites/damage/flesh uleg.png");
            BULeg = ModAPI.LoadTexture("Assets/Sprites/damage/bone uleg.png");
            DULeg = ModAPI.LoadTexture("Assets/Sprites/damage/damage uleg.png");

            FLLeg = ModAPI.LoadTexture("Assets/Sprites/damage/flesh lleg.png");
            BLLeg = ModAPI.LoadTexture("Assets/Sprites/damage/bone lleg.png");
            DLLeg = ModAPI.LoadTexture("Assets/Sprites/damage/damage lleg.png");

            FFoot = ModAPI.LoadTexture("Assets/Sprites/damage/flesh foot.png");
            BFoot = ModAPI.LoadTexture("Assets/Sprites/damage/bone foot.png");
            DFoot = ModAPI.LoadTexture("Assets/Sprites/damage/damage foot.png");

            FUArm = ModAPI.LoadTexture("Assets/Sprites/damage/flesh uarm.png");
            BUArm = ModAPI.LoadTexture("Assets/Sprites/damage/bone uarm.png");
            DUArm = ModAPI.LoadTexture("Assets/Sprites/damage/damage uarm.png");

            FLArm = ModAPI.LoadTexture("Assets/Sprites/damage/flesh larm.png");
            BLArm = ModAPI.LoadTexture("Assets/Sprites/damage/bone larm.png");
            DLArm = ModAPI.LoadTexture("Assets/Sprites/damage/damage larm.png");

            FleshLayer = new List<Texture2D>
            {
                FHead, FUpper, FMiddle, FLower, FULeg, FLLeg, FFoot, FULeg, FLLeg, FFoot, FUArm, FLArm, FUArm, FLArm
            };

            BoneLayer = new List<Texture2D>
            {
                BHead, BUpper, BMiddle, BLower, BULeg, BLLeg, BFoot, BULeg, BLLeg, BFoot, BUArm, BLArm, BUArm, BLArm
            };

            DamageLayer = new List<Texture2D>
            {
                DHead, DUpper, DMiddle, DLower, DULeg, DLLeg, DFoot, DULeg, DLLeg, DFoot, DUArm, DLArm, DUArm, DLArm
            };
        }
    }
}
