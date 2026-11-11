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
    public partial class DBPU
    {

        public static void Main()
        {
            PotaraFusionHelpers.CachedPotaraSprite = ModAPI.LoadSprite("Assets/Sprites/Items/Potara Earring.png");
            ScouterChiaotzuAssets.EnsureLoaded();

            CategoryBuilder.Create(ModTab, "", ModAPI.LoadSprite("Cthumb.png"));
            RegisterNaruto();
            RegisterGogito();

            // Some Details:
            // I have a specific way of organizing the character's NameToOrderByOverride, and its always a number that represents the category, a "-" and the number of the character on that category, some examples:

            // 00-0001 = Vehicles like Freeza's Little Space Ship
            // 01-0001 = Items that are only cosmetic
            // 02-0001 = Items that have some use, like Senzu Beans or the Dragon Balls
            // 03-0001 = Usually Z Fighters
            // 04-0001 = Humans like Chichi or Bulma
            // 05-0001 = Villans/Enemies

            // There are Tags to use in each character in order to be easier to anyone to understand and memorize which number is which category


            //Config
            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Rod"),
                    NameOverride = ModTag + "Config",
                    NameToOrderByOverride = "&",
                    DescriptionOverride = "Mod Configurations",
                    CategoryOverride = ModAPI.FindCategory(ModTab),
                    ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/Configs Icon.png"),
                    AfterSpawn = (Instance) =>
                    {
                        GameObject Menu = new GameObject("Config Menu");
                        Menu.gameObject.AddComponent<Configs>();

                        GameObject.Destroy(Instance);
                    }
                }
                );

            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Rod"),
                    NameOverride = ModTag + "Power Giver",
                    NameToOrderByOverride = UniqueTag + "0000",
                    DescriptionOverride = "Grant mod powers to any person. Select a power, then click them.",
                    CategoryOverride = ModAPI.FindCategory(ModTab),
                    ThumbnailOverride = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/powergiver.png"),
                    AfterSpawn = (Instance) =>
                    {
                        PowerGiverUI.Open();
                        GameObject.Destroy(Instance);
                    }
                });

            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Rod"),
                    NameOverride = ModTag + "Transformation Maker",
                    NameToOrderByOverride = "&1",
                    DescriptionOverride = "Create and edit custom transformations. Pick a character, then a form, then edit sprites and properties with a live preview.",
                    CategoryOverride = ModAPI.FindCategory(ModTab),
                    ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/Configs Icon.png"),
                    AfterSpawn = (Instance) =>
                    {
                        TransformationMakerUI.Open();
                        GameObject.Destroy(Instance);
                    }
                });

            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Rod"),
                    NameOverride = ModTag + "Skin Maker",
                    NameToOrderByOverride = "&2",
                    DescriptionOverride = "Create and edit custom skin packs. Pick a character, edit limbs by category, auto-generate damage variants, and preview live.",
                    CategoryOverride = ModAPI.FindCategory(ModTab),
                    ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/Configs Icon.png"),
                    AfterSpawn = (Instance) =>
                    {
                        CustomSkinMakerUI.Open();
                        GameObject.Destroy(Instance);
                    }
                });

            ModAPIPlus.RegisterSubcategory("Items", "Dragon Ball Items", UniqueTag + "1", "Items");

            ModAPIPlus.RegisterSubcategory("DBS", "Dragon Ball Super Series", UniqueTag + "2", "DBS");
            ModAPIPlus.RegisterSubSubcategory("DBS", "Broly Movie", "Broly Movie Arc", UniqueTag + "2.1", "BrolyMovie");
            ModAPIPlus.RegisterSubSubcategory("DBS", "DBSSH", "Dragon Ball Super: Super Hero", UniqueTag + "2.2", "DBSSH");
            ModAPIPlus.RegisterSubSubcategory("DBS", "Goku Black Arc", "Goku Black Arc", UniqueTag + "2.4", "Goku Black Arc");

            ModAPIPlus.RegisterSubcategory("DBZ", "Dragon Ball Z Series", UniqueTag + "3", "DBZ");
            ModAPIPlus.RegisterSubSubcategory("DBZ", "Buu Saga", "Dragon Ball Z Majin Buu Saga", UniqueTag + "3.1", "Buu Saga");
            ModAPIPlus.RegisterSubSubcategory("DBZ", "Saiyan Saga", "Dragon Ball Z Saiyan Saga", UniqueTag + "3.2", "SaiyanSaga");
            ModAPIPlus.RegisterSubSubcategory("DBZ", "Story of Bardock", "Dragon Ball Z: Bardock - The Father of Goku", UniqueTag + "3.3", "StoryOfBardock");
            ModAPIPlus.RegisterSubSubcategory("DBZ", "Cell Games", "Dragon Ball Z Cell Games", UniqueTag + "3.4", "CellGames");


            //BM Goku
            ModAPIPlus.CreateHuman(
                "(BM) Goku",
                "Hey, It's me, Goku!",
                "BM/Goku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Goku";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Goku/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Goku/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGI/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage1/";
                    List<Sprite> TurtleGIDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage2/";
                    List<Sprite> TurtleGIDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage3/";
                    List<Sprite> TurtleGIDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage4/";
                    List<Sprite> TurtleGIDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage5/";
                    List<Sprite> TurtleGIDamage5 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGI_Buffed/";
                    List<Sprite> TurtleGISkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage1_Buffed/";
                    List<Sprite> TurtleGIDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage2_Buffed/";
                    List<Sprite> TurtleGIDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage3_Buffed/";
                    List<Sprite> TurtleGIDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage4_Buffed/";
                    List<Sprite> TurtleGIDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage5_Buffed/";
                    List<Sprite> TurtleGIDamage5_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };




                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkin/";
                    List<Sprite> SuitSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage1/";
                    List<Sprite> SuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage2/";
                    List<Sprite> SuitDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage3/";
                    List<Sprite> SuitDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage4/";
                    List<Sprite> SuitDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkin_Buffed/";
                    List<Sprite> SuitSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage1_Buffed/";
                    List<Sprite> SuitDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage2_Buffed/";
                    List<Sprite> SuitDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage3_Buffed/";
                    List<Sprite> SuitDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage4_Buffed/";
                    List<Sprite> SuitDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/FarmerSkin/";
                    List<Sprite> FarmerSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/FarmerSkin_Buffed/";
                    List<Sprite> FarmerSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/CasualSkin/";
                    List<Sprite> CasualSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/CasualSkin_Buffed/";
                    List<Sprite> CasualSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/YardratSkin/";
                    List<Sprite> Yardrat = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/YardratSkin_Buffed/";
                    List<Sprite> Yardrat_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGINoWeightSkin/";
                    List<Sprite> TurtleGINoWeight = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGINoWeightSkin_Buffed/";
                    List<Sprite> TurtleGINoWeight_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    person.ChangeSkin(TurtleGISkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Dictionary<string, List<Sprite>> TurtleGiSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", TurtleGISkin },
                        { "Damage1", TurtleGIDamage1 },
                        { "Damage2", TurtleGIDamage2 },
                        { "Damage3", TurtleGIDamage3 },
                        { "Damage4", TurtleGIDamage4 },
                        { "Damage5", TurtleGIDamage5 }
                    };

                    Dictionary<string, List<Sprite>> TurtleGiSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", TurtleGISkin_Buffed },
                        { "Damage1", TurtleGIDamage1_Buffed },
                        { "Damage2", TurtleGIDamage2_Buffed },
                        { "Damage3", TurtleGIDamage3_Buffed },
                        { "Damage4", TurtleGIDamage4_Buffed },
                        { "Damage5", TurtleGIDamage5_Buffed }
                    };

                    Dictionary<string, List<Sprite>> SuitSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", SuitSkin },
                        { "Damage1", SuitDamage1 },
                        { "Damage2", SuitDamage2 },
                        { "Damage3", SuitDamage3 },
                        { "Damage4", SuitDamage4 }
                    };

                    Dictionary<string, List<Sprite>> SuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", SuitSkin_Buffed },
                        { "Damage1", SuitDamage1_Buffed },
                        { "Damage2", SuitDamage2_Buffed },
                        { "Damage3", SuitDamage3_Buffed },
                        { "Damage4", SuitDamage4_Buffed }
                    };

                    Dictionary<string, List<Sprite>> FarmerSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", FarmerSkin }
                    };

                    Dictionary<string, List<Sprite>> FarmerSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                                    { "Damage0", FarmerSkin_Buffed }
                    };

                    Dictionary<string, List<Sprite>> CasualSkins = new Dictionary<string, List<Sprite>>
                    {
                                    { "Damage0", CasualSkin }
                    };

                    Dictionary<string, List<Sprite>> CasualSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                                    { "Damage0", CasualSkin_Buffed }
                    };

                    Dictionary<string, List<Sprite>> YardratSkins = new Dictionary<string, List<Sprite>>
                    {
                                    { "Damage0", Yardrat }
                    };

                    Dictionary<string, List<Sprite>> YardratSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", Yardrat_Buffed }
                    };

                    Dictionary<string, List<Sprite>> TurtleGINoWeightSkins = new Dictionary<string, List<Sprite>>
                {
                                { "Damage0", TurtleGINoWeight }
                    };

                    Dictionary<string, List<Sprite>> TurtleGINoWeightSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                                { "Damage0", TurtleGINoWeight_Buffed }
                    };


                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "Goku's Base form, nothing special about it, just the good old Goku we know.",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "The first Super Saiyan of the series, described as the Legendary Super Saiyan, Goku unleashed this Form after losing his best friend Krillin to the hand of Lord Frieza.\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var grade1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 70f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj grade1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJGrade1.png"),
                        KiDrainRate = 8000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/grade1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/grade1.png"),
                        Name = "Super Saiyan Grade 2",
                        Description = "After training with Gohan for a while, Goku learn how to buff himself into a stronger stage of Super Saiyan 1, and although it gives more power, it takes away the speed of the user. \n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger, but slower.",

                        ID = "SSJGrade2",
                        SkinDamageAmount = 1,
                        RequiredSkin_Suffix = "_Buffed"
                    };
                    characterTransformations.transformations.AddTransformation("SSJGrade2", grade1Transformation);

                    var pssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 80f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair pssj.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ1.png"),
                        KiDrainRate = 0f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/pssj.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/pssj.png"),
                        Name = "Perfect Super Saiyan",
                        Description = "The Perfect version of Super Saiyan 1, this form was acquired after Goku and his Son Gohan trained for 3 Years, to achieve the Perfect version of a Regular Super Saiyan rather then buffing his muscles.\n\nThis form drains no Ki, and so it can be used freely.",

                        ID = "PSSJ",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("PSSJ", pssjTransformation);

                    var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj2.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ2.png"),
                        KiDrainRate = 10000f,
                        AuraID = "SSJ2",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj2.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj2.png"),
                        Name = "Super Saiyan 2",
                        Description = "A Super Saiyan that went beyond the limits of a regular Super Saiyan, Goku unleashed this transformation sometime during his training on the Otherworld.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger and faster.",

                        ID = "SSJ2",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                    var ssj3Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 400f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj3.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ3.png"),
                        KiDrainRate = 25000f,
                        AuraID = "SSJ3",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj3.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj3.png"),
                        Name = "Super Saiyan 3",
                        Description = "A transformation that goes even further beyond, the iconic transformation of long golden hair, that was obtained during his time on the Otherworld, just like Super Sayian 2.\n\nThis is Goku's most expensive form out of all regular Saiyan transformations, but it's also the strongest out of all Non-Divine forms.",

                        ID = "SSJ3",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("SSJ3", ssj3Transformation);

                    var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 10000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssjg.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJG.png"),
                        KiDrainRate = 5000f,
                        AuraID = "SSJGod",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssjg.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssjg.png"),
                        Name = "Super Saiyan God",
                        Description = "The form that results from a Saiyan using Divine Ki, was acquired by Goku by making a ritual with 6 pure hearted saiyans, to fight Lord Beerus and stop him from destroying the planet.\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                        ID = "SSJGod",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                    var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssjb.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJB.png"),
                        KiDrainRate = 12000f,
                        AuraID = "SSJBlue",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssjb.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssjb.png"),
                        Name = "Super Saiyan Blue",
                        Description = "Essentially a Super Saiyan God but merged with the regular Super Saiyan, or also known as Super Saiyan God Super Saiyan, was obtained by Goku when he was training with Whis to master Ki Control, discovering the ability to use both transformations at once.\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                        ID = "SSJBlue",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                    var pssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 600000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair pssjb.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/PSSJB.png"),
                        KiDrainRate = 6000f,
                        AuraID = null,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/pssjb.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/pssjb.png"),
                        Name = "Perfected Super Saiyan Blue",
                        Description = "The Perfected version of Super Saiyan Blue, this transformation is slightly stronger then the regular one, but when comes to Ki Drain, it's way better and more stable then regular Super Saiyan Blue.\n\nThe Ki Drain it's low, and still has an immense power.",

                        ID = "PSSJBlue",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("PSSJBlue", pssjbTransformation);

                    var uiTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1000000000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ui.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/UI.png"),
                        KiDrainRate = 8000f,
                        AuraID = "UI",

                        Icon = LoadCharacterFormIcon("Goku", "UI.png"),
                        Illustration = LoadCharacterFormIllustration("Goku", "UI.png"),
                        Name = "Ultra Instinct",
                        Description = "Ultra Instinct",

                        ID = "UI",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("UI", uiTransformation);

                    var muiTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1500000000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair mui.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/MUI.png"),
                        KiDrainRate = 10000f,
                        AuraID = "MUI",

                        Icon = LoadCharacterFormIcon("Goku", "MUI.png"),
                        Illustration = LoadCharacterFormIllustration("Goku", "MUI.png"),
                        Name = "Mastered Ultra Instinct",
                        Description = "Mastered Ultra Instinct",

                        ID = "MUI",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("MUI", muiTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    var characterSkins = Instance.AddComponent<CharacterSkins>();
                    var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();

                    skinsCM.SkinsData = characterSkins;

                    var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Turtle Gi",
                        Name = "TurtleGi"
                    };
                    BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BaseSkin.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins } };
                    BaseSkin.Skins["TurtleGi_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins_Buffed } };
                    BaseSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage4.png") },
                        { "Damage5", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage5.png") }
                    };
                    var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1285f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites1,
                        minAngleFirstPiece = -15f,
                        maxAngleFirstPiece = 32f,
                        minAngle = -17f,
                        maxAngle = 25f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    var belt2 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites2,
                        minAngleFirstPiece = -32f,
                        maxAngleFirstPiece = 15f,
                        minAngle = -25f,
                        maxAngle = 17f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    BaseSkin.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData>();
                    BaseSkin.PhyAccessories.Add(belt1);
                    BaseSkin.PhyAccessories.Add(belt2);
                    characterSkins.skins.AddSkin("TurtleGi", BaseSkin);

                    var SaiyanSuitSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Battle Armor",
                        Name = "SaiyanSuit"
                    };
                    SaiyanSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    SaiyanSuitSkin.Skins["SaiyanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins } };
                    SaiyanSuitSkin.Skins["SaiyanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins_Buffed } };
                    SaiyanSuitSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage4.png") },
                    };
                    characterSkins.skins.AddSkin("SaiyanSuit", SaiyanSuitSkin);

                    var FarmSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Farmer Skin",
                        Name = "FarmerSkin"
                    };
                    FarmSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    FarmSkin.Skins["FarmerSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", FarmerSkins } };
                    FarmSkin.Skins["FarmerSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", FarmerSkins_Buffed } };
                    FarmSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/FarmerSkin.png") }
                    };
                    characterSkins.skins.AddSkin("FarmerSkin", FarmSkin);

                    var CasualSkin_ = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Casual Skin",
                        Name = "CasualSkin"
                    };
                    CasualSkin_.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    CasualSkin_.Skins["CasualSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", CasualSkins } };
                    CasualSkin_.Skins["CasualSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", CasualSkins_Buffed } };
                    CasualSkin_.Illustrations = new Dictionary<string, Sprite>
                {
                                { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/CasualSkin.png") }
                    };
                    characterSkins.skins.AddSkin("CasualSkin", CasualSkin_);

                    var YardratSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Yardrat Skin",
                        Name = "YardratSkin"
                    };
                    YardratSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    YardratSkin.Skins["YardratSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", YardratSkins } };
                    YardratSkin.Skins["YardratSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", YardratSkins_Buffed } };
                    YardratSkin.Illustrations = new Dictionary<string, Sprite>
                {
                                { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/YardratSkin.png") }
                    };
                    characterSkins.skins.AddSkin("YardratSkin", YardratSkin);

                    var TurtleGINoWeightSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Turtle GI No Weights",
                        Name = "TurtleGINoWeightSkin"
                    };
                    TurtleGINoWeightSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    TurtleGINoWeightSkin.Skins["TurtleGINoWeightSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGINoWeightSkins } };
                    TurtleGINoWeightSkin.Skins["TurtleGINoWeightSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGINoWeightSkins_Buffed } };
                    TurtleGINoWeightSkin.Illustrations = new Dictionary<string, Sprite>
                {
                                { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGINoWeightSkin.png") }
                    };
                    characterSkins.skins.AddSkin("TurtleGINoWeightSkin", TurtleGINoWeightSkin);

                    DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Goku/", "Head.png");

                    skinsCM.CurrentSkin = "TurtleGi";
                    skinsCM.PreviousSkin = "TurtleGi";

                    head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();

                    var beamBehaviour = Instance.AddComponent<BeamBehaviour>();
                    beamBehaviour.EffectID = "Kamehameha";
                    beamBehaviour.Character = "Goku";

                    CeceUtils.DelayedInvoke(0.15f, () =>
                    {
                        if (beamBehaviour != null) beamBehaviour.RegisterArmBeamTriggers();
                    });

                    Instance.AddComponent<SolarFlare>().Character = "Goku";

                    Instance.AddComponent<KiShield>();

                    var gokuBall = Instance.GetComponent<BallBehaviour>() ?? Instance.AddComponent<BallBehaviour>();
                    gokuBall.EffectID = "Spirit Bomb";
                    gokuBall.Character = "Goku";

                    // Ultra Instinct dodge disabled


                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    PowerSetup.RegisterGokuCombatPowers(pw);
                    pw.Finish();

                    lower.gameObject.AddComponent<Flight>();

                    pw.Grant("InstantTransmission");
                    pw.Powers.MovePowerBlockToStart(new[] { "SpiritBomb", "SuperSpiritBomb" });

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 5000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "001");

            //BM Vegeta
            ModAPIPlus.CreateHuman(
                "(BM) Vegeta",
                "Meaningless, huh?! What do you know of meaningless?! Spend most of your life ruled by another! Watch your race dwindle to a handful! And then tell me what has more meaning then your own strength!",
                "BM/Vegeta",
                (Instance) =>
                {
                    Instance.gameObject.name = "Vegeta";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;



                    string Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkin/";
                    List<Sprite> SuitSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/";
                    List<Sprite> SuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/";
                    List<Sprite> SuitDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/";
                    List<Sprite> SuitDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage4/";
                    List<Sprite> SuitDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/LArm.png") };


                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkin_Buffed/";
                    List<Sprite> SuitSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/";
                    List<Sprite> SuitDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/";
                    List<Sprite> SuitDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/";
                    List<Sprite> SuitDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage4_Buffed/";
                    List<Sprite> SuitDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/LArm.png") };



                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirt/";
                    List<Sprite> BlueShirt = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/";
                    List<Sprite> BlueShirtDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/";
                    List<Sprite> BlueShirtDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/";
                    List<Sprite> BlueShirtDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage4/";
                    List<Sprite> BlueShirtDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/LArm.png") };


                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirt_Buffed/";
                    List<Sprite> BlueShirt_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/";
                    List<Sprite> BlueShirtDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/";
                    List<Sprite> BlueShirtDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/";
                    List<Sprite> BlueShirtDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage4_Buffed/";
                    List<Sprite> BlueShirtDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/LArm.png") };



                    Folder = "Assets/Sprites/Characters/Vegeta/BadmanSuit/";
                    List<Sprite> BadmanSuit = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Vegeta/BadmanSuit_Buffed/";
                    List<Sprite> BadmanSuit_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };



                    person.ChangeSkin(SuitSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.64f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;


                    Dictionary<string, List<Sprite>> SuitSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", SuitSkin },
                        { "Damage1", SuitDamage1 },
                        { "Damage2", SuitDamage2 },
                        { "Damage3", SuitDamage3 },
                        { "Damage4", SuitDamage4 }
                        };

                    Dictionary<string, List<Sprite>> SuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", SuitSkin_Buffed },
                        { "Damage1", SuitDamage1_Buffed },
                        { "Damage2", SuitDamage2_Buffed },
                        { "Damage3", SuitDamage3_Buffed },
                        { "Damage4", SuitDamage4_Buffed }
                        };


                    Dictionary<string, List<Sprite>> BlueShirtSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BlueShirt },
                        { "Damage1", BlueShirtDamage1 },
                        { "Damage2", BlueShirtDamage2 },
                        { "Damage3", BlueShirtDamage3 },
                        { "Damage4", BlueShirtDamage4 }
                        };

                    Dictionary<string, List<Sprite>> BlueShirtSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BlueShirt_Buffed },
                        { "Damage1", BlueShirtDamage1_Buffed },
                        { "Damage2", BlueShirtDamage2_Buffed },
                        { "Damage3", BlueShirtDamage3_Buffed },
                        { "Damage4", BlueShirtDamage4_Buffed }
                        };


                    Dictionary<string, List<Sprite>> BadmanSuitSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BadmanSuit }
                        };

                    Dictionary<string, List<Sprite>> BadmanSuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BadmanSuit_Buffed }
                        };


                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "Vegeta in his Base form, the Prince of Saiyans with his unbeatable pride.",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "Only a Pure Hearted Saiyan can become a Super Saiyan, and he was pure, Pure Evil. He obtained this transformation after owning the self-hatred of his physical limits causing the Super Saiyan within him to awaken.\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var superTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 70f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj grade1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ1.png"),
                        KiDrainRate = 8000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/super.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/super.png"),
                        Name = "Super Vegeta (SSJ Grade 2)",
                        Description = "With his rigorous self-training, Vegeta was able to surpass the power of a regular Super Saiyan, only to try and become stronger then Goku.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger, but slower.",

                        ID = "SSJGrade2",
                        SkinDamageAmount = 1,
                        RequiredSkin_Suffix = "_Buffed"
                    };
                    characterTransformations.transformations.AddTransformation("SSJGrade2", superTransformation);

                    var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj2.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ2.png"),
                        KiDrainRate = 10000f,
                        AuraID = "SSJ2",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssj2.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssj2.png"),
                        Name = "Super Saiyan 2",
                        Description = "With his rigorous self-training, Vegeta was able to surpass the power of a regular Super Saiyan, only to try and become stronger then Goku.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger and faster.",

                        ID = "SSJ2",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                    var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 10000f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjg.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJG.png"),
                        KiDrainRate = 5000f,
                        AuraID = "SSJGod",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjg.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjg.png"),
                        Name = "Super Saiyan God",
                        Description = "Unlike Goku, that obtained Divine Ki from the God Ritual, Vegeta obtained it from hard training with Whis, archiving this transformation all by himself.\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                        ID = "SSJGod",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                    var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500000f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjb.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJB.png"),
                        KiDrainRate = 12000f,
                        AuraID = "SSJBlue",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjb.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjb.png"),
                        Name = "Super Saiyan Blue",
                        Description = "Just like Goku, Vegeta learned how to infuse the power of a regular Super Saiyan into the Divine Form of a Saiyan.\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                        ID = "SSJBlue",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                    var pssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 600000f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair pssjb.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/PSSJB.png"),
                        KiDrainRate = 6000f,
                        AuraID = null,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/pssjb.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/pssjb.png"),
                        Name = "Perfected Super Saiyan Blue",
                        Description = "The Perfected version of Super Saiyan Blue, this transformation is slightly stronger then the regular one, but when comes to Ki Drain, it's way better and more stable then regular Super Saiyan Blue.\n\nThe Ki Drain it's low, and still has an immense power.",

                        ID = "PSSJBlue",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("PSSJBlue", pssjbTransformation);

                    var ssjbeTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 10000000f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjbe.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJBE.png"),
                        KiDrainRate = 16000f,
                        AuraID = "SSJBlueEvolution",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjbe.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjbe.png"),
                        Name = "Super Saiyan Blue Evolution",
                        Description = "The direct evolution of Super Saiyan Blue, much more powerful, was obtained during Vegeta's Fight agains't Jiren on the Tournament of Power, after pushing through his limits several times.\n\nThis transformation can be extremly powerful, but also consumes a lot of Ki as a down side.",

                        ID = "SSJBlueEvolution",
                        SkinDamageAmount = 2,
                        RequiredSkin_Suffix = "_Buffed"
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlueEvolution", ssjbeTransformation);

                    var ueTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1200000000f,
                        Size = 1.64f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ue.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/UE.png"),
                        KiDrainRate = 20000f,
                        AuraID = "UE",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ue.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ue.png"),
                        Name = "Ultra Ego",
                        Description = "Ultra Ego",

                        ID = "UE",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("UE", ueTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
                    var characterSkins = Instance.AddComponent<CharacterSkins>();
                    skinsCM.SkinsData = characterSkins;

                    var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Battle Armor",
                        Name = "SaiyanSuit"
                    };
                    BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BaseSkin.Skins["SaiyanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins } };
                    BaseSkin.Skins["SaiyanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins_Buffed } };
                    BaseSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage4.png") },
                        };
                    characterSkins.skins.AddSkin("SaiyanSuit", BaseSkin);

                    var BlueShirtSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Blue Shirt",
                        Name = "BlueShirt"
                    };
                    BlueShirtSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BlueShirtSkin.Skins["BlueShirt"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlueShirtSkins } };
                    BlueShirtSkin.Skins["BlueShirt_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlueShirtSkins_Buffed } };
                    BlueShirtSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage4.png") },
                        };
                    characterSkins.skins.AddSkin("BlueShirt", BlueShirtSkin);

                    var BadmanSuitSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Badman Suit",
                        Name = "BadmanSuit"
                    };
                    BadmanSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BadmanSuitSkin.Skins["BadmanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BadmanSuitSkins } };
                    BadmanSuitSkin.Skins["BadmanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BadmanSuitSkins_Buffed } };
                    BadmanSuitSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BadmanSuit.png") }
                        };
                    characterSkins.skins.AddSkin("BadmanSuit", BadmanSuitSkin);

                    DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Vegeta/", "Head.png");

                    if (!Instance.GetComponent<UltraEgoDamageBoost>()) Instance.AddComponent<UltraEgoDamageBoost>();

                    skinsCM.CurrentSkin = "SaiyanSuit";
                    skinsCM.PreviousSkin = "SaiyanSuit";

                    head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                    Instance.AddComponent<BlastBehaviour>();

                    //Instance.AddComponent<BeamBehaviour>().EffectID = "Final Flash";
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Galick Gun";
                    Instance.GetComponent<BeamBehaviour>().Character = "Vegeta";

                    Instance.AddComponent<FinalExplosion>();
                    Instance.AddComponent<KiShield>();

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    var vegetaBeam = new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = PowerSetup.VegetaBeamOnEnable(r) };
                    pw.Grant("KiBlast", new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = PowerSetup.VegetaKiOnEnable(r) });
                    pw.GrantMany(vegetaBeam, "EnergyWave", "GalickGun", "FinalFlash");
                    pw.Grant("FinalExplosion");
                    pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; r.FinalExplosion.Active = false; }
                    });
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();
                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        //limb.BaseStrength *= 22f;
                        //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                        //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 5000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "002");

            //BM Gohan
            ModAPIPlus.CreateHuman(
                "(BM) Gohan",
                "I'm going to pursue an ultimate form that no Saiyan has ever achieved before. I won't rely on Super Saiyan transformations, instead, I'll find a different kind of ultimate power.",
                "BM/Gohan",
                (Instance) =>
                {
                    Instance.gameObject.name = "Gohan";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Gohan/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                        };

                    BeltFolder = "Assets/Sprites/Characters/Gohan/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                        };

                    string Folder = "Assets/Sprites/Characters/Gohan/DemonClothesSkin/";
                    List<Sprite> DemonClothesSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage1/";
                    List<Sprite> DemonClothesDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesSkin/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesSkin/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage2/";
                    List<Sprite> DemonClothesDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage3/";
                    List<Sprite> DemonClothesDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage4/";
                    List<Sprite> DemonClothesDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage3/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGI/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage1/";
                    List<Sprite> TurtleGIDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage2/";
                    List<Sprite> TurtleGIDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage3/";
                    List<Sprite> TurtleGIDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage4/";
                    List<Sprite> TurtleGIDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage5/";
                    List<Sprite> TurtleGIDamage5 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Gohan/BlackSuit/";
                    List<Sprite> BlackSuit = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/BlackSuitDamage1/";
                    List<Sprite> BlackSuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    person.ChangeSkin(DemonClothesSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Dictionary<string, List<Sprite>> DemonClothesSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", DemonClothesSkin },
                        { "Damage1", DemonClothesDamage1 },
                        { "Damage2", DemonClothesDamage2 },
                        { "Damage3", DemonClothesDamage3 },
                        { "Damage4", DemonClothesDamage4 }
                        };

                    Dictionary<string, List<Sprite>> TurtleGiSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", TurtleGISkin },
                        { "Damage1", TurtleGIDamage1 },
                        { "Damage2", TurtleGIDamage2 },
                        { "Damage3", TurtleGIDamage3 },
                        { "Damage4", TurtleGIDamage4 },
                        { "Damage5", TurtleGIDamage5 }
                        };

                    Dictionary<string, List<Sprite>> BlackSuitSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BlackSuit },
                        { "Damage1", BlackSuitDamage1 }
                        };

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "Nothing special about it, other than being a Saiyan with amazing potential.",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "The basic Super Saiyan form of any Saiyan. Gohan achieved this transformation during his training in the Hyperbolic Time Chamber with Goku, which pushed his limits and allowed him to ascend to a Super Saiyan like his father.\n\nThis form has the least Ki drain among all transformations.",

                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var pssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 80f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair pssj.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/PSSJ.png"),
                        KiDrainRate = 0f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/pssj.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/pssj.png"),
                        Name = "Perfect Super Saiyan",
                        Description = "The Perfect version of Super Saiyan 1, this form was acquired after Gohan and his Father Goku trained for 3 Years, in order to becomer stronger and defeat the Enemy of that time, Perfect Cell.\n\nThis form drains no Ki, and so it can be used freely.",

                        ID = "PSSJ",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("PSSJ", pssjTransformation);

                    var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ssj2.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/SSJ2.png"),
                        KiDrainRate = 10000f,
                        AuraID = "SSJ2",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ssj2.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ssj2.png"),
                        Name = "Super Saiyan 2",
                        Description = "Gohan's iconic transformation as a child, obtained after witnessing Perfect Cell mercilessly crush Android 16. This act filled Gohan's heart with pure rage, triggering an explosive boost in power.\n\nThis transformation drains more Ki than Super Saiyan 1 but compensates with increased strength and speed.",

                        ID = "SSJ2",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                    var ultimateTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ultimate.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/Ultimate.png"),
                        KiDrainRate = 6000f,
                        AuraID = "Mystic",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ultimate.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ultimate.png"),
                        Name = "Mystic Gohan",
                        Description = "AKA Ultimate Gohan, this transformation represents Gohan's alternative path among Saiyan transformations, bypassing the traditional Super Saiyan forms as a base for advanced power-ups.\n\nThis form has minimal Ki drain compared to Super Saiyan 2 and is significantly stronger as well.",

                        ID = "Ultimate",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Ultimate", ultimateTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
                    var characterSkins = Instance.AddComponent<CharacterSkins>();
                    skinsCM.SkinsData = characterSkins;

                    var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Demon Clothes",
                        Name = "DemonClothes"
                    };
                    BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BaseSkin.Skins["DemonClothes"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", DemonClothesSkins } };
                    BaseSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage4.png") },
                        };
                    characterSkins.skins.AddSkin("DemonClothes", BaseSkin);

                    var TurtleGi = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Turtle Gi",
                        Name = "TurtleGi"
                    };
                    TurtleGi.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    TurtleGi.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins } };
                    TurtleGi.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage4.png") },
                        { "Damage5", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage5.png") }
                        };
                    var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1285f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites1,
                        minAngleFirstPiece = -15f,
                        maxAngleFirstPiece = 32f,
                        minAngle = -17f,
                        maxAngle = 25f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    var belt2 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites2,
                        minAngleFirstPiece = -32f,
                        maxAngleFirstPiece = 15f,
                        minAngle = -25f,
                        maxAngle = 17f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    TurtleGi.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData>();
                    TurtleGi.PhyAccessories.Add(belt1);
                    TurtleGi.PhyAccessories.Add(belt2);
                    characterSkins.skins.AddSkin("TurtleGi", TurtleGi);

                    var BlackSuitSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Black Suit",
                        Name = "BlackSuit"
                    };
                    BlackSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BlackSuitSkin.Skins["BlackSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackSuitSkins } };
                    BlackSuitSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/BlackSuit_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/BlackSuit_Damage1.png") }
                        };
                    characterSkins.skins.AddSkin("BlackSuit", BlackSuitSkin);

                    DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Gohan/", "Head.png");

                    skinsCM.CurrentSkin = "DemonClothes";
                    skinsCM.PreviousSkin = "DemonClothes";

                    head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = head.gameObject.GetComponent<SkinContextMenu>();

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.AddComponent<HellzoneGranade>();
                    Instance.AddComponent<KiShield>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    var gohanBeam = PowerSetup.BeamBlock(r, PowerSetup.StandardBeamOnEnable(r, true));
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.GrantMany(gohanBeam, "EnergyWave", "Kamehameha");
                    if (r.Hellzone != null) pw.Grant("HellzoneGranade");
                    if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
                    });
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 4800000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 4800000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 4800000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "003");

            //BM Krillin
            ModAPIPlus.CreateHuman(
                "(BM) Krillin",
                "GOKUUUUU!!",
                "BM/Krillin",
                (Instance) =>
                {
                    Instance.gameObject.name = "Krillin";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    string BeltFolder = "Assets/Sprites/Characters/Krillin/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Krillin/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                                {
                                ModAPI.LoadSprite(BeltFolder + "stf.png"),
                                ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                                ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                                ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                                ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                                ModAPI.LoadSprite(BeltFolder + "stf5.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Krillin/BaseGi/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };
                    person.ChangeSkin(TurtleGISkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.53f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 200000f;
                    kiSystem.baseRegen = 2500f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.AddComponent<SolarFlare>().Character = "Krillin";
                    Instance.AddComponent<KiShield>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.Beam.StillActive,
                        OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; if (r.Solar != null) r.Solar.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.GrantMany(PowerSetup.GokuBeamStyle(), "EnergyWave", "Kamehameha");
                    if (r.Solar != null) pw.Grant("SolarFlare");
                    if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; r.Blast.Active = false; if (r.Solar != null) r.Solar.Active = false; }
                    });
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    Instance.AddComponent<AuraHandler>().person = person;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                                () =>
                                {
                                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                                }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        //limb.BaseStrength *= 22f;
                        //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                        //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        limb.Health *= 90000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 90000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 90000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.05f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.StrongLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "004");

            //BM Master Roshi
            ModAPIPlus.CreateHuman(
                "(BM) Master Roshi",
                "Work hard, study well, and eat and sleep plenty! That is the Turtle Hermit way! We must master the art of peace in addition to the art of war! The Turtle Hermit School will be with you...always!",
                "BM/Roshi",
                (Instance) =>
                {
                    Instance.gameObject.name = "Roshi";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Master Roshi/beard.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Master Roshi/Base/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Master Roshi/Buffed/";
                    List<Sprite> BuffedSkins = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.46f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 150000f;
                    kiSystem.baseRegen = 2000f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    Instance.AddComponent<AuraHandler>().person = person;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    bool Buffed = false;

                    head.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleBuffedForm", (Func<string>)(() => !Buffed ? "Buffed Form" : "Base Form"), "Toggle Buffed Form",
                    () =>
                    {
                        if (Buffed == false)
                        {
                            Buffed = true;

                            Vector3 OgPos = middle.gameObject.transform.position;

                            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.92f);

                            Vector3 NewPos = middle.gameObject.transform.position - OgPos;

                            foreach (var body in person.Limbs)
                            {
                                body.transform.position -= NewPos;

                                body.BaseStrength *= 1.925f;
                                body.InitialHealth *= 3f;
                                body.Health *= 3f;
                                body.PhysicalBehaviour.TrueInitialMass *= 1.75f;
                                body.PhysicalBehaviour.RecalculateMassBasedOnSize();
                                body.ImpactDamageMultiplier /= 3f;
                            }

                            Instance.gameObject.GetComponent<MultipliersAndStuffs>().FormMultiplier = 3f;

                            person.ChangeSkin(BuffedSkins);
                        }

                        else
                        {
                            Buffed = false;

                            Vector3 OgPos = middle.gameObject.transform.position;

                            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.46f);

                            Vector3 NewPos = middle.gameObject.transform.position - OgPos;

                            foreach (var body in person.Limbs)
                            {
                                body.transform.position -= NewPos;

                                body.BaseStrength /= 1.925f;
                                body.InitialHealth /= 3f;
                                body.Health /= 3f;
                                body.PhysicalBehaviour.TrueInitialMass /= 1.75f;
                                body.PhysicalBehaviour.RecalculateMassBasedOnSize();
                                body.ImpactDamageMultiplier *= 3f;
                            }

                            Instance.gameObject.GetComponent<MultipliersAndStuffs>().FormMultiplier = 1f;

                            person.ChangeSkin(BaseSkin);
                        }
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        //limb.BaseStrength *= 22f;
                        //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                        //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        limb.Health *= 15000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 15000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 15000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.05f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.SuperLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "005");

            //BM Yamcha
            ModAPIPlus.CreateHuman(
                "(BM) Yamcha",
                "I guess I'll just be on the sidelines... again.",
                "BM/Yamcha",
                (Instance) =>
                {
                    Instance.gameObject.name = "Yamcha";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Yamcha/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    lower.gameObject.AddComponent<Flight>();

                    string BeltFolder = "Assets/Sprites/Characters/Yamcha/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                            ModAPI.LoadSprite(BeltFolder + "stb.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb6.png")
                        };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Yamcha/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                            ModAPI.LoadSprite(BeltFolder + "stf.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf5.png")
                        };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Yamcha/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.83f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1500f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    Instance.AddComponent<AuraHandler>().person = person;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        //limb.BaseStrength *= 22f;
                        //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                        //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        limb.Health *= 800000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 800000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 800000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.2f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 1000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.SuperLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "006");

            //BM Tenshinhan
            ModAPIPlus.CreateHuman(
                "(BM) Tenshinhan",
                "I HATE EVERYTHING ABOUT YOU, WHY DO I LOVE YOU",
                "BM/Tenshinhan",
                (Instance) =>
                {
                    Instance.gameObject.name = "Tenshinhan";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    string WaistFolder = "Assets/Sprites/Characters/Tenshinhan/DBS Skin/Waist/";
                    Sprite[] WaistSprites = new Sprite[18]
                    {
                            ModAPI.LoadSprite(WaistFolder + "dr.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr1.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr2.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr3.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr4.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr5.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr6.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr7.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr8.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr9.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr10.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr11.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr12.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr13.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr14.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr15.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr16.png"),
                            ModAPI.LoadSprite(WaistFolder + "dr17.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.015f, 0.1f), "Foreground", 1, WaistSprites, -7f, 12f, -5f, 10f, 0.0018f, 0.001f, "Waist");

                    WaistFolder = "Assets/Sprites/Characters/Tenshinhan/DBS Skin/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                            ModAPI.LoadSprite(WaistFolder + "stb.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb1.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb2.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb3.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb4.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb5.png"),
                            ModAPI.LoadSprite(WaistFolder + "stb6.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    WaistFolder = "Assets/Sprites/Characters/Tenshinhan/DBS Skin/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                            ModAPI.LoadSprite(WaistFolder + "stf.png"),
                            ModAPI.LoadSprite(WaistFolder + "stf1.png"),
                            ModAPI.LoadSprite(WaistFolder + "stf2.png"),
                            ModAPI.LoadSprite(WaistFolder + "stf3.png"),
                            ModAPI.LoadSprite(WaistFolder + "stf4.png"),
                            ModAPI.LoadSprite(WaistFolder + "stf5.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.87f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 400000f;
                    kiSystem.baseRegen = 5000f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.AddComponent<SolarFlare>().Character = "Tien";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    if (r.Solar != null) pw.Grant("SolarFlare");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    Instance.AddComponent<AuraHandler>().person = person;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    string Folder = "Assets/Sprites/Characters/Tenshinhan/DBS Skin/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 120000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 120000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 120000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.05f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.StrongLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "007");

            //BM Piccolo
            ModAPIPlus.CreateHuman(
                "(BM) Piccolo",
                "Piccolo, you need to stop aura farming!",
                "BM/Piccolo",
                (Instance) =>
                {
                    Instance.gameObject.name = "Piccolo";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    lower.gameObject.AddComponent<Flight>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Base/head thingy.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 2.26f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 750000f;
                    kiSystem.baseRegen = 8000f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Energy Wave";
                    Instance.AddComponent<HellzoneGranade>();
                    Instance.AddComponent<KiShield>();
                    Instance.AddComponent<PiccoloGiantForm>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    PowerSetup.AddPiccoloGrowthPower(pw.Powers, Instance.GetComponent<PiccoloGiantForm>());
                    var piccoloBeam = PowerSetup.BeamBlock(r, PowerSetup.StandardBeamOnEnable(r, true));
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.GrantMany(piccoloBeam, "EnergyWave", "SpecialBeamCannon");
                    if (r.Hellzone != null) pw.Grant("HellzoneGranade");
                    if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
                    });
                    PowerSetup.GrantPiccoloEyeLaser(person, Instance, pw.Powers);
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    Instance.AddComponent<AuraHandler>().person = person;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    string Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Base/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        //limb.BaseStrength *= 22f;
                        //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                        //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        limb.Health *= 3500000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 3500000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 3500000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "008");

            //BM Android 17
            ModAPIPlus.CreateHuman(
                "(BM) Android 17",
                "Sacrificing myself to help others. I like human nature like that.",
                "BM/17",
                SetupBmAndroid17Spawn,
                "Broly Movie",
                UniqueTag + ZFighterTag + "009");

            //BM Android 18
            ModAPIPlus.CreateHuman(
                "(BM) Android 18",
                "What are you staring at? Are you in love with me or something?",
                "BM/18",
                SetupBmAndroid18Spawn,
                "Broly Movie",
                UniqueTag + ZFighterTag + "010");

            //BM Kid Goten
            ModAPIPlus.CreateHuman(
                "(BM) Kid Goten",
                "",
                "BM/Kid Goten",
                (Instance) =>
                {
                    Instance.gameObject.name = "Kid Goten";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Kid Goten/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Kid Goten/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Kid Goten/TurtleGI/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(TurtleGISkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.32f);

                    head.gameObject.AddComponent<Child>();

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.32f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.32f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Goten/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "",

                        ID = "SSJ1",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goten";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 25000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 25000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 25000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "011");

            //BM Kid Trunks
            ModAPIPlus.CreateHuman(
                "(BM) Kid Trunks",
                "",
                "BM/Kid Trunks",
                (Instance) =>
                {
                    Instance.gameObject.name = "Kid Trunks";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Kid Trunks/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Kid Trunks/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Kid Trunks/Skin/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.38f);

                    head.gameObject.AddComponent<Child>();

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.38f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.38f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Kid Trunks/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "",

                        ID = "SSJ1",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Kid Trunks";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 28000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 28000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 28000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "012");

            ModAPIPlus.CreateHuman(
                "Goku Black",
                "My form is justice.",
                "Assets/Sprites/Characters/Goku/Icons/Rose.png",
                ApplyGokuBlackSpawn,
                "Goku Black Arc",
                UniqueTag + ZFighterTag + "GokuBlack");

            //BM Broly
            ModAPIPlus.CreateHuman(
                "(BM) Broly",
                "",
                "BM/Broly",
                (Instance) =>
                {
                    Instance.gameObject.name = "Broly";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var bahEarObject_Front = new GameObject("Bah Ear Front");
                    bahEarObject_Front.transform.SetParent(lower);
                    bahEarObject_Front.transform.localPosition = new Vector3(0f, 0f);
                    bahEarObject_Front.transform.rotation = lower.rotation;
                    bahEarObject_Front.transform.localScale = new Vector3(1f, 1f);
                    var bahEarSprite_Front = bahEarObject_Front.AddComponent<SpriteRenderer>();
                    bahEarSprite_Front.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/BahEar_Front.png");
                    bahEarSprite_Front.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    bahEarSprite_Front.sortingOrder = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var bahEarObject_Back = new GameObject("Bah Ear Back");
                    bahEarObject_Back.transform.SetParent(lower);
                    bahEarObject_Back.transform.localPosition = new Vector3(0f, 0f);
                    bahEarObject_Back.transform.rotation = lower.rotation;
                    bahEarObject_Back.transform.localScale = new Vector3(1f, 1f);
                    var bahEarSprite_Back = bahEarObject_Back.AddComponent<SpriteRenderer>();
                    bahEarSprite_Back.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/BahEar_Back.png");
                    bahEarSprite_Back.sortingLayerName = larmb.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    bahEarSprite_Back.sortingOrder = larmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder;

                    uarmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder = -1;
                    larmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder = -1;

                    string Folder = "Assets/Sprites/Characters/Broly/BattleArmor/";
                    List<Sprite> BattleArmorSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/BattleArmor_Buffed/";
                    List<Sprite> BattleArmorSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/BattleArmor_Buffed2/";
                    List<Sprite> BattleArmorSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Broly/BlackShirt/";
                    List<Sprite> BlackShirtSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/BlackShirt_Buffed/";
                    List<Sprite> BlackShirtSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/BlackShirt_Buffed2/";
                    List<Sprite> BlackShirtSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Broly/Shirtless/";
                    List<Sprite> ShirtlessSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/Shirtless_Buffed/";
                    List<Sprite> ShirtlessSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Broly/Shirtless_Buffed2/";
                    List<Sprite> ShirtlessSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BlackShirtSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.85f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 2500000f;
                    kiSystem.baseRegen = 20000f;
                    kiSystem.requiredColor = new Color(0f / 255f, 255f / 255f, 0f / 255f);

                    Dictionary<string, List<Sprite>> BlackShirtSkins = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin } };

                    Dictionary<string, List<Sprite>> BlackShirtSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin_Buffed } };

                    Dictionary<string, List<Sprite>> BlackShirtSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin_Buffed2 } };


                    Dictionary<string, List<Sprite>> BattleArmorSkins = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin } };

                    Dictionary<string, List<Sprite>> BattleArmorSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin_Buffed } };

                    Dictionary<string, List<Sprite>> BattleArmorSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin_Buffed2 } };


                    Dictionary<string, List<Sprite>> ShirtlessSkins = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin } };

                    Dictionary<string, List<Sprite>> ShirtlessSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin_Buffed } };

                    Dictionary<string, List<Sprite>> ShirtlessSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin_Buffed2 } };


                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.85f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Broly",
                        requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "Broly in his calmest state, with an unimaginable potential deep inside.",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ikariTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 10f,
                        Size = 2.13f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair ikari.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/Ikari.png"),
                        KiDrainRate = 0f,
                        AuraID = "Broly",
                        requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/ikari.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/ikari.png"),
                        Name = "Ikari",
                        Description = "Also known as Berserk State, Broly loses control over his actions and starts a rampage. This form is essentially Broly's version of oozaru.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation drains no Ki.",

                        ID = "Ikari",
                        SkinDamageAmount = 0,
                        RequiredSkin_Suffix = "_Buffed"
                    };
                    ikariTransformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                    characterTransformations.transformations.AddTransformation("Ikari", ikariTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 2.24f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/SSJ1.png"),
                        KiDrainRate = 8000f,
                        AuraID = "Broly",
                        requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "The basic Super Saiyan state, after seeing his dad Paragus, laid on the ground, killed by Frieza, he loses his mind completely and gets consumed by unstoppable rage.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation drains a low amount of Ki.",

                        ID = "SSJ1",
                        SkinDamageAmount = 0,
                        RequiredSkin_Suffix = "_Buffed2"
                    };
                    ssj1Transformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var fpssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500f,
                        Size = 2.74f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair fpssj.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/FPSSJ.png"),
                        KiDrainRate = 15000f,
                        AuraID = "Broly",
                        requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/fpssj.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/fpssj.png"),
                        Name = "Full Power Super Saiyan",
                        Description = "The combination between Broly's mastery over Oozaru's power with the Super Saiyan power. He Awakened this form in the middle of his Fight agains't Gogeta.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation is the stronguest of them all, and it's also the most Ki expensive one.",

                        ID = "FPSSJ",
                        SkinDamageAmount = 0,
                        RequiredSkin_Suffix = "_Buffed2"
                    };
                    fpssjTransformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                    characterTransformations.transformations.AddTransformation("FPSSJ", fpssjTransformation);

                    //GameObject.Destroy(temp);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    var characterSkins = Instance.AddComponent<CharacterSkins>();
                    var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();

                    skinsCM.SkinsData = characterSkins;

                    var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Black Shirt",
                        Name = "BlackShirt"
                    };
                    BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BaseSkin.Skins["BlackShirt"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins } };
                    BaseSkin.Skins["BlackShirt_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins_Buffed } };
                    BaseSkin.Skins["BlackShirt_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins_Buffed2 } };
                    BaseSkin.Illustrations = new Dictionary<string, Sprite>
                        {
                                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/BlackShirt_Damage0.png") }
                            };
                    characterSkins.skins.AddSkin("BlackShirt", BaseSkin);

                    var BattleArmor = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Battle Armor",
                        Name = "BattleArmor"
                    };
                    BattleArmor.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BattleArmor.Skins["BattleArmor"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins } };
                    BattleArmor.Skins["BattleArmor_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins_Buffed } };
                    BattleArmor.Skins["BattleArmor_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins_Buffed2 } };
                    BattleArmor.Illustrations = new Dictionary<string, Sprite>
                        {
                                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/BattleArmor_Damage0.png") }
                            };
                    characterSkins.skins.AddSkin("BattleArmor", BattleArmor);

                    var Shirtless = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Shirtless",
                        Name = "Shirtless"
                    };
                    Shirtless.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    Shirtless.Skins["Shirtless"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins } };
                    Shirtless.Skins["Shirtless_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins_Buffed } };
                    Shirtless.Skins["Shirtless_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins_Buffed2 } };
                    Shirtless.Illustrations = new Dictionary<string, Sprite>
                        {
                                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/Shirtless_Damage0.png") }
                            };
                    characterSkins.skins.AddSkin("Shirtless", Shirtless);

                    DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Broly/", "Head.png");

                    skinsCM.CurrentSkin = "BlackShirt";
                    skinsCM.PreviousSkin = "BlackShirt";

                    head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Gigantic Full Blast";
                    Instance.GetComponent<BeamBehaviour>().Character = "Broly";
                    Instance.AddComponent<BallBehaviour>().EffectID = "Gigantic Crusher";
                    Instance.GetComponent<BallBehaviour>().Character = "Broly";
                    Instance.AddComponent<KiShield>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    Action brolyBeamOff = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; };
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.Grant("EnergyWave", new PowerCatalog.Overrides { BlockWhen = () => r.BeamStillBlocked, OnEnable = brolyBeamOff, BeamType = "ArmBeam" });
                    pw.GrantMany(PowerSetup.BeamBlock(r, brolyBeamOff), "GiganticFullBlast", "GiganticRoar");
                    if (r.Ball != null) pw.Grant("GiganticCrusher");
                    if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
                    });
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    lower.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleBahEar", (Func<string>)(() => (bahEarSprite_Front.color == Color.clear) ? "Add Bah's Ear" : "Remove Bah's Ear"), "Toggle Bah's Ear",
                    () =>
                    {
                        if (bahEarSprite_Front.color == Color.clear)
                        {
                            bahEarSprite_Front.color = Color.white;
                            bahEarSprite_Back.color = Color.white;
                        }

                        else
                        {
                            bahEarSprite_Front.color = Color.clear;
                            bahEarSprite_Back.color = Color.clear;
                        }
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 50000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 50000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 50000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "013");

            //BM Gogeta
            ModAPIPlus.CreateHuman(
                "(BM) Gogeta",
                "",
                "BM/Gogeta",
                (Instance) =>
                {
                    Instance.gameObject.name = "Gogeta";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Gogeta/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                        };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                    BeltFolder = "Assets/Sprites/Characters/Gogeta/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                        };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                    string Folder = "Assets/Sprites/Characters/Gogeta/Metamoro/";
                    List<Sprite> MetamoroSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(MetamoroSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000000f;
                    kiSystem.baseRegen = 1000000f;

                    Dictionary<string, List<Sprite>> MetamoroSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", MetamoroSkin }
                        };

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJ1.png"),
                        KiDrainRate = 400000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 10000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssjg.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJG.png"),
                        KiDrainRate = 500000f,
                        AuraID = "SSJGod",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssjg.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssjg.png"),
                        Name = "Super Saiyan God",
                        Description = "\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                        ID = "SSJGod",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                    var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssjb.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJB.png"),
                        KiDrainRate = 1200000f,
                        AuraID = "SSJBlue",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssjb.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssjb.png"),
                        Name = "Super Saiyan Blue",
                        Description = "\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                        ID = "SSJBlue",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Gogeta";
                    Instance.gameObject.AddComponent<StardustFall>();
                    Instance.gameObject.AddComponent<MeteorExplosion>();
                    Instance.gameObject.AddComponent<GodPunisher>();
                    Instance.gameObject.AddComponent<StardustBreaker>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    Action gogetaOff = () => { r.Blast.Active = false; PowerSetup.DisableGogetaSpecials(r); };
                    var gogetaBeam = PowerSetup.BeamBlock(r, gogetaOff);
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Beam.Active = false; PowerSetup.DisableGogetaSpecials(r); }
                    });
                    pw.GrantMany(gogetaBeam, "EnergyWave", "Kamehameha", "FullForceKamehameha");
                    if (r.StardustFall != null) pw.Grant("StardustFall");
                    if (r.MeteorExplosion != null) pw.Grant("MeteorExplosion");
                    if (r.GodPunisher != null) pw.Grant("GodPunisher");
                    if (r.StardustBreaker != null) pw.Grant("StardustBreaker");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 50000000000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 50000000000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 50000000000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "014");

            ModAPIPlus.CreateHuman(
                "Vegito",
                "The gloves are off. Fight me! YOSHAAAA!!!",
                "DBZ/Vegito",
                (Instance) =>
                {
                    Instance.gameObject.name = "Vegito";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegito/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var potaraObject = new GameObject("Potara");
                    potaraObject.transform.SetParent(head);
                    potaraObject.transform.localPosition = PotaraFusionHelpers.PotaraWornLocalPosition;
                    potaraObject.transform.rotation = head.rotation;
                    potaraObject.transform.localScale = new Vector3(1f, 1f);
                    var potaraSprite = potaraObject.AddComponent<SpriteRenderer>();
                    potaraSprite.sprite = PotaraFusionHelpers.CachedPotaraSprite;
                    potaraObject.transform.localScale = new Vector3(PotaraFusionHelpers.PotaraWornVisualScale, PotaraFusionHelpers.PotaraWornVisualScale, 1f);
                    potaraSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    potaraSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 2;

                    string folder = "Assets/Sprites/Characters/Vegito/GiUndamaged/";
                    person.ChangeSkin(LoadCharacterLimbSprites(folder));

                    ApplyImmortalHumanSetup(Instance, person, head, middle, 50000000000000000000000f);

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();
                    kiSystem.maxKi = 100000000f;
                    kiSystem.baseRegen = 1000000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegito/hair.png"),
                        HeadSprite = ModAPI.LoadSprite(folder + "Head.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",
                        Icon = LoadCharacterFormIcon("Vegito", "Base.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Illustration = LoadCharacterFormIllustration("Vegito", "Base.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Name = "Base",
                        Description = "",
                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegito/ssjhair.png"),
                        HeadSprite = ModAPI.LoadSprite(folder + "ssjHead.png"),
                        KiDrainRate = 400000f,
                        AuraID = "SSJ1",
                        Icon = LoadCharacterFormIcon("Vegito", "ssj1.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Illustration = LoadCharacterFormIllustration("Vegito", "ssj1.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Name = "Super Saiyan",
                        Description = "",
                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssjTransformation);

                    var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegito/ssjbhair.png"),
                        HeadSprite = ModAPI.LoadSprite(folder + "ssjbHead.png"),
                        KiDrainRate = 1200000f,
                        AuraID = "SSJBlue",
                        Icon = LoadCharacterFormIcon("Vegito", "SSJBlue.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Illustration = LoadCharacterFormIllustration("Vegito", "SSJBlue.png", ModAPI.LoadSprite("Assets/Icons/DBZ/Vegito.png")),
                        Name = "Super Saiyan Blue",
                        Description = "",
                        ID = "SSJBlue",
                        SkinDamageAmount = 2
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () => { characterTransformations.ApplyTransformation(person, "Base"); });
                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<Kaioken>();
                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Gogeta";

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    Action vegitoOff = () => { r.Blast.Active = false; PowerSetup.DisableGogetaSpecials(r); };
                    var vegitoBeam = PowerSetup.BeamBlock(r, vegitoOff);
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Beam.Active = false; PowerSetup.DisableGogetaSpecials(r); }
                    });
                    pw.GrantMany(vegitoBeam, "EnergyWave", "Kamehameha", "FullForceKamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();
                    Instance.AddComponent<InstantTransmission>();
                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();
                    person.Limbs[1].gameObject.AddComponent<UnFuseBehaviour>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));
                },
                "Buu Saga",
                UniqueTag + ZFighterTag + "015");

            ModAPIPlus.CreateHuman(
                "Kid Buu",
                "Me Buu, not nice!",
                "DBZ/Kid Buu",
                (Instance) => ApplyBuuSpawn(Instance, "Assets/Sprites/Characters/Buu/Kid Buu/", "Kid Buu", true, false, 1.35f),
                "Buu Saga",
                UniqueTag + VillanTag + "006");

            ModAPIPlus.CreateHuman(
                "Majin Buu",
                "Me give you nice beatdown!",
                "DBZ/Majin Buu",
                (Instance) => ApplyBuuSpawn(Instance, "Assets/Sprites/Characters/Buu/Majin Buu/", "Majin Buu", true, false, 1.54f),
                "Buu Saga",
                UniqueTag + VillanTag + "007");

            ModAPIPlus.CreateHuman(
                "Super Buu",
                "You are not worthy to fight me!",
                "DBZ/Super Buu",
                (Instance) => ApplyBuuSpawn(Instance, "Assets/Sprites/Characters/Buu/Super Buu/", "Super Buu", true, false, 2.74f),
                "Buu Saga",
                UniqueTag + VillanTag + "008");

            ModAPIPlus.CreateHuman(
                "Evil Buu",
                "Give up your candy!",
                "DBZ/Evil Buu",
                (Instance) => ApplyBuuSpawn(Instance, "Assets/Sprites/Characters/Buu/Evil Buu/", "Evil Buu", true, true, 1.66f),
                "Buu Saga",
                UniqueTag + VillanTag + "009");

            //BM Frieza
            ModAPIPlus.CreateHuman(
                "Frieza",
                "I am the Emperor of the Universe. Kneel before me!",
                "BM/Freeza",
                (Instance) =>
                {
                    Instance.gameObject.name = "Frieza";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    var hornObject = new GameObject("Horn");
                    hornObject.transform.SetParent(head);
                    hornObject.transform.localPosition = new Vector3(0f, 0f);
                    hornObject.transform.rotation = head.rotation;
                    hornObject.transform.localScale = new Vector3(1f, 1f);
                    var hornSprite = hornObject.AddComponent<SpriteRenderer>();
                    hornSprite.color = Color.clear;
                    hornSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/SecondForm/Horn.png");
                    hornSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hornSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var spikeObject1 = new GameObject("Spike");
                    spikeObject1.transform.SetParent(upper);
                    spikeObject1.transform.localPosition = new Vector3(0f, 0f);
                    spikeObject1.transform.rotation = upper.rotation;
                    spikeObject1.transform.localScale = new Vector3(1f, 1f);
                    var spikeSprite1 = spikeObject1.AddComponent<SpriteRenderer>();
                    spikeSprite1.color = Color.clear;
                    spikeSprite1.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/ThirdForm/UpperSpike.png");
                    spikeSprite1.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    spikeSprite1.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var spikeObject2 = new GameObject("Spike");
                    spikeObject2.transform.SetParent(middle);
                    spikeObject2.transform.localPosition = new Vector3(0f, 0f);
                    spikeObject2.transform.rotation = middle.rotation;
                    spikeObject2.transform.localScale = new Vector3(1f, 1f);
                    var spikeSprite2 = spikeObject2.AddComponent<SpriteRenderer>();
                    spikeSprite2.color = Color.clear;
                    spikeSprite2.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/ThirdForm/MiddleSpike.png");
                    spikeSprite2.sortingLayerName = middle.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    spikeSprite2.sortingOrder = middle.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string TailFolder = "Assets/Sprites/Characters/Freeza/FirstForm/Tail/";
                    Sprite[] FirstFormTail = new Sprite[25]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png"),
                            ModAPI.LoadSprite(TailFolder + "/t24.png")
                        };

                    TailFolder = "Assets/Sprites/Characters/Freeza/SecondForm/Tail/";
                    Sprite[] SecondFormTail = new Sprite[26]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png"),
                            ModAPI.LoadSprite(TailFolder + "/t24.png"),
                            ModAPI.LoadSprite(TailFolder + "/t25.png")
                        };

                    TailFolder = "Assets/Sprites/Characters/Freeza/ThirdForm/Tail/";
                    Sprite[] ThirdFormTail = new Sprite[26]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png"),
                            ModAPI.LoadSprite(TailFolder + "/t24.png"),
                            ModAPI.LoadSprite(TailFolder + "/t25.png")
                        };

                    TailFolder = "Assets/Sprites/Characters/Freeza/FinalForm/Tail/";
                    Sprite[] FinalFormTail = new Sprite[25]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png"),
                            ModAPI.LoadSprite(TailFolder + "/t24.png")
                        };

                    TailFolder = "Assets/Sprites/Characters/Freeza/FullPower/Tail/";
                    Sprite[] FullPowerTail = new Sprite[24]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png")
                        };

                    TailFolder = "Assets/Sprites/Characters/Freeza/Golden/Tail/";
                    Sprite[] GoldenFormTail = new Sprite[25]
                    {
                            ModAPI.LoadSprite(TailFolder + "/t.png"),
                            ModAPI.LoadSprite(TailFolder + "/t1.png"),
                            ModAPI.LoadSprite(TailFolder + "/t2.png"),
                            ModAPI.LoadSprite(TailFolder + "/t3.png"),
                            ModAPI.LoadSprite(TailFolder + "/t4.png"),
                            ModAPI.LoadSprite(TailFolder + "/t5.png"),
                            ModAPI.LoadSprite(TailFolder + "/t6.png"),
                            ModAPI.LoadSprite(TailFolder + "/t7.png"),
                            ModAPI.LoadSprite(TailFolder + "/t8.png"),
                            ModAPI.LoadSprite(TailFolder + "/t9.png"),
                            ModAPI.LoadSprite(TailFolder + "/t10.png"),
                            ModAPI.LoadSprite(TailFolder + "/t11.png"),
                            ModAPI.LoadSprite(TailFolder + "/t12.png"),
                            ModAPI.LoadSprite(TailFolder + "/t13.png"),
                            ModAPI.LoadSprite(TailFolder + "/t14.png"),
                            ModAPI.LoadSprite(TailFolder + "/t15.png"),
                            ModAPI.LoadSprite(TailFolder + "/t16.png"),
                            ModAPI.LoadSprite(TailFolder + "/t17.png"),
                            ModAPI.LoadSprite(TailFolder + "/t18.png"),
                            ModAPI.LoadSprite(TailFolder + "/t19.png"),
                            ModAPI.LoadSprite(TailFolder + "/t20.png"),
                            ModAPI.LoadSprite(TailFolder + "/t21.png"),
                            ModAPI.LoadSprite(TailFolder + "/t22.png"),
                            ModAPI.LoadSprite(TailFolder + "/t23.png"),
                            ModAPI.LoadSprite(TailFolder + "/t24.png")
                        };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.115f, 0f), "Default", 1, FinalFormTail, 80f, 90f, -7f, 15f, 0.002f, 0.001f, "Tail", true, false);

                    string Folder = "Assets/Sprites/Characters/Freeza/FirstForm/";
                    List<Sprite> FirstFormSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    Folder = "Assets/Sprites/Characters/Freeza/SecondForm/";
                    List<Sprite> SecondFormSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    Folder = "Assets/Sprites/Characters/Freeza/ThirdForm/";
                    List<Sprite> ThirdFormSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    Folder = "Assets/Sprites/Characters/Freeza/FinalForm/";
                    List<Sprite> FinalFormSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    Folder = "Assets/Sprites/Characters/Freeza/FullPower/";
                    List<Sprite> FullPowerSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    Folder = "Assets/Sprites/Characters/Freeza/Golden/";
                    List<Sprite> GoldenSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    person.ChangeSkin(FinalFormSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.58f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.CurrentForm = "FinalForm";
                    kiSystem.BaseForm = "FinalForm";
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();
                    var characterSkins = Instance.AddComponent<CharacterSkins>();

                    kiSystem.maxKi = 2500000f;
                    kiSystem.baseRegen = 25000f;

                    var firstFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 0.125f,
                        Size = 1.40f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 0f,
                        AuraID = "Frieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = FirstFormSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/FirstForm.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/FirstForm.png"),
                        Name = "1st Form",
                        Description = "Frieza's weakest form, used to suppress his power in order to control it, although nowadays this form isn't needed anymore, since Frieza has mastered his Base Form.\n\nThis transformation doesn't take any Ki from the user, since it's weaker than the Base Form.",

                        ID = "FirstForm"
                    };
                    var firstFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = FirstFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    firstFormTransformation.PhyAccessory = firstFormTail;
                    characterTransformations.transformations.AddTransformation("FirstForm", firstFormTransformation);

                    var secondFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 0.25f,
                        Size = 2.40f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 0f,
                        AuraID = "Frieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, true),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = SecondFormSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/SecondForm.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/SecondForm.png"),
                        Name = "2nd Form",
                        Description = "The second form of Frieza, taller, with a pair of long horns in his head, it used to be a way to keep suppressing Frieza's power, but it isn't used anymore.\n\nThis transformation doesn't take any Ki from the user, since it's weaker than the Base Form.",

                        ID = "SecondForm"
                    };
                    var secondFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = SecondFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    secondFormTransformation.PhyAccessory = secondFormTail;
                    characterTransformations.transformations.AddTransformation("SecondForm", secondFormTransformation);

                    var thirdFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 0.5f,
                        Size = 2f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 0f,
                        AuraID = "Frieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, true),
                                (spikeSprite2, true)
                                },

                        RequiredSkin = ThirdFormSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/ThirdForm.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/ThirdForm.png"),
                        Name = "3rd Form",
                        Description = "Maybe one of the strangest forms in Dragon Ball, Frieza's Third Form is once again another way to suppress his power, but just like the others, it was forgotten over time. This isn't his Final Form yet!\n\nThis transformation doesn't take any Ki from the user, since it's weaker than the Base Form.",

                        ID = "ThirdForm"
                    };
                    var thirdFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = SecondFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    thirdFormTransformation.PhyAccessory = thirdFormTail;
                    characterTransformations.transformations.AddTransformation("ThirdForm", thirdFormTransformation);

                    var finalFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.58f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 0f,
                        AuraID = "Frieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = FinalFormSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/FinalForm.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/FinalForm.png"),
                        Name = "Final Form",
                        Description = "Frieza's Final Form (or so it was meant to be), beside it's name, this is actually Frieza's True Form, him in his natural state, but not at 100% of his power yet.",

                        ID = "FinalForm"
                    };
                    var finalFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = FinalFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    finalFormTransformation.PhyAccessory = finalFormTail;
                    characterTransformations.transformations.AddTransformation("FinalForm", finalFormTransformation);

                    var fullPowerTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 2f,
                        Size = 1.58f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 18000f,
                        AuraID = "Frieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = FullPowerSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/FullPower.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/FullPower.png"),
                        Name = "Full Power",
                        Description = "The Full Power of Frieza was only used twice in the series, once in DBZ, and once in DBS, it sure gives a boost to Frieza's Power, but it also comes with a downside, it's lack of speed.\n\nThis Form has quite small Ki Drain, since Frieza has mastered his True Form, using his Full Power isn't as tiring as it used to be.",

                        ID = "FullPower"
                    };
                    var fullPowerTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = FullPowerTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -4f,
                        maxAngle = 8f,
                        pieceMass = 0.0008f,
                        pieceInertia = 0.0015f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    fullPowerTransformation.PhyAccessory = fullPowerTail;
                    characterTransformations.transformations.AddTransformation("FullPower", fullPowerTransformation);

                    var goldenFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 250000f,
                        Size = 1.74f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 30000f,
                        AuraID = "GoldenFrieza",

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = GoldenSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/Golden.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/Golden.png"),
                        Name = "Golden Form",
                        Description = "An ironic tribute to Goku's Super Saiyan, Frieza unleashed such power by simply training for a couple months, being able to become stronger way faster than Goku or Vegeta.\n\nThis transformation has a medium Ki Drain amount, but taking in account it's power increase, it's very worth it.",

                        ID = "GoldenForm"
                    };
                    var goldenFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = GoldenFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    goldenFormTransformation.PhyAccessory = goldenFormTail;
                    characterTransformations.transformations.AddTransformation("GoldenForm", goldenFormTransformation);

                    TailFolder = "Assets/Sprites/Characters/Freeza/Black/Tail/";
                    Sprite[] BlackFormTail = new Sprite[25]
                    {
                            ModAPI.LoadSprite(TailFolder + "t.png"),
                            ModAPI.LoadSprite(TailFolder + "t1.png"),
                            ModAPI.LoadSprite(TailFolder + "t2.png"),
                            ModAPI.LoadSprite(TailFolder + "t3.png"),
                            ModAPI.LoadSprite(TailFolder + "t4.png"),
                            ModAPI.LoadSprite(TailFolder + "t5.png"),
                            ModAPI.LoadSprite(TailFolder + "t6.png"),
                            ModAPI.LoadSprite(TailFolder + "t7.png"),
                            ModAPI.LoadSprite(TailFolder + "t8.png"),
                            ModAPI.LoadSprite(TailFolder + "t9.png"),
                            ModAPI.LoadSprite(TailFolder + "t10.png"),
                            ModAPI.LoadSprite(TailFolder + "t11.png"),
                            ModAPI.LoadSprite(TailFolder + "t12.png"),
                            ModAPI.LoadSprite(TailFolder + "t13.png"),
                            ModAPI.LoadSprite(TailFolder + "t14.png"),
                            ModAPI.LoadSprite(TailFolder + "t15.png"),
                            ModAPI.LoadSprite(TailFolder + "t16.png"),
                            ModAPI.LoadSprite(TailFolder + "t17.png"),
                            ModAPI.LoadSprite(TailFolder + "t18.png"),
                            ModAPI.LoadSprite(TailFolder + "t19.png"),
                            ModAPI.LoadSprite(TailFolder + "t20.png"),
                            ModAPI.LoadSprite(TailFolder + "t21.png"),
                            ModAPI.LoadSprite(TailFolder + "t22.png"),
                            ModAPI.LoadSprite(TailFolder + "t23.png"),
                            ModAPI.LoadSprite(TailFolder + "t24.png")
                        };

                    Folder = "Assets/Sprites/Characters/Freeza/Black/";
                    List<Sprite> BlackSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm2.png") };

                    var blackFormTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 2500000000f,
                        Size = 1.74f,
                        HairSprite = null,
                        HeadSprite = null,
                        KiDrainRate = 50000f,
                        AuraID = "BlackFrieza",
                        requiredKiColor = new Color(140f / 255f, 60f / 255f, 220f / 255f),

                        SituationalAccessories = new List<(SpriteRenderer, bool)>
                            {
                                (hornSprite, false),
                                (spikeSprite1, false),
                                (spikeSprite2, false)
                                },

                        RequiredSkin = BlackSkin,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Icons/Black.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Freeza/Illustrations/Black.png"),
                        Name = "Black Frieza",
                        Description = "Frieza's black form, far beyond Golden Frieza.",

                        ID = "BlackForm"
                    };
                    var blackFormTail = new CharacterTransformations.Transformations.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(-0.115f, 0f),
                        layerName = "Default",
                        order = 1,
                        sprites = BlackFormTail,
                        minAngleFirstPiece = 80f,
                        maxAngleFirstPiece = 90f,
                        minAngle = -7f,
                        maxAngle = 15f,
                        pieceMass = 0.002f,
                        pieceInertia = 0.001f,
                        Name = "Tail",
                        HasCollision = true,
                        CollideJustWithGround = false
                    };
                    blackFormTransformation.PhyAccessory = blackFormTail;
                    characterTransformations.transformations.AddTransformation("BlackForm", blackFormTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "FinalForm");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;
                    head.gameObject.GetComponent<TransformationContextMenu>().PositionSelectedFrame = 3;
                    head.gameObject.GetComponent<TransformationContextMenu>().OriginalSelectedFrame = 3;
                    head.gameObject.GetComponent<TransformationContextMenu>().OriginalFormSelected = 3;
                    head.gameObject.GetComponent<TransformationContextMenu>().Form = "FinalForm";
                    head.gameObject.GetComponent<TransformationContextMenu>().BaseForm = "FinalForm";
                    head.gameObject.GetComponent<TransformationContextMenu>().CurrentAuraID = "Frieza";

                    PowerSetup.EnsureFriezaCombatComponents(Instance);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    PowerSetup.RegisterFriezaCombatPowers(pw);
                    pw.Finish();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 10000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 10000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 10000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        //limb.CirculationBehaviour.ImmuneToDamage = true;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "Broly Movie",
                UniqueTag + VillanTag + "001");

            //BM Whis
            ModAPIPlus.CreateHuman(
                "(BM) Whis",
                "The Angel attendant and martial arts teacher of Beerus, the God of Destruction. Whis moves faster than instant transmission and trains Goku and Vegeta on Beerus's planet.",
                "BM/Whis",
                (Instance) =>
                {
                    Instance.gameObject.name = "Whis";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    uarmf.GetComponent<SpriteRenderer>().sortingOrder += 4;
                    larmf.GetComponent<SpriteRenderer>().sortingOrder += 4;

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Whis/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(upper);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = lower.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Whis/Thingy.png");
                    capeSprite.sortingLayerName = "Foreground";
                    capeSprite.sortingOrder = 5;

                    lower.gameObject.AddComponent<Flight>();

                    string WaistFolder = "Assets/Sprites/Characters/Whis/Waist/";
                    Sprite[] WaistSprites = new Sprite[18]
                    {
                        ModAPI.LoadSprite(WaistFolder + "dr.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr1.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr2.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr3.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr4.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr5.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr6.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr7.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr8.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr9.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr10.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr11.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr12.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr13.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr14.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr15.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr16.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr17.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.015f, 0.1f), "Foreground", 1, WaistSprites, -7f, 12f, -5f, 10f, 0.0018f, 0.001f, "Waist");

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 2.26f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 50000000000f;
                    kiSystem.baseRegen = 1250000000f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    Instance.AddComponent<InstantTransmission>().Active = true;

                    var whisAura = Instance.AddComponent<FlameOutlineAuraHandler>();
                    whisAura.AuraType = typeof(WhisFlameAura);
                    whisAura.InnerAuraType = typeof(WhisInnerFlameAura);
                    whisAura.AccentAuraType = null;
                    whisAura.Setup();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !whisAura.AuraOn ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        whisAura.ToggleAura();
                    }));

                    string Folder = "Assets/Sprites/Characters/Whis/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health = float.MaxValue;
                        limb.InitialHealth = float.MaxValue;
                        limb.BreakingThreshold = float.MaxValue;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }

                    CeceUtils.DelayedInvoke(0.15f, () =>
                    {
                        var multipliers = Instance.GetComponent<MultipliersAndStuffs>();
                        if (multipliers != null)
                            multipliers.FormMultiplier = 10000000000f;
                    });
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "015");

            //BM Beerus
            ModAPIPlus.CreateHuman(
                "(BM) Beerus",
                "The God of Destruction of Universe 7.",
                "BM/Beerus",
                (Instance) =>
                {
                    Instance.gameObject.name = "Beerus";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Beerus/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    uarmf.GetComponent<SpriteRenderer>().sortingOrder += 4;
                    larmf.GetComponent<SpriteRenderer>().sortingOrder += 4;

                    ulegf.GetComponent<SpriteRenderer>().sortingOrder += 3;
                    llegf.GetComponent<SpriteRenderer>().sortingOrder += 3;
                    footf.GetComponent<SpriteRenderer>().sortingOrder += 3;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(lower);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = lower.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Beerus/Thingy.png");
                    capeSprite.sortingLayerName = "Default";
                    capeSprite.sortingOrder = 2;

                    string TailFolder = "Assets/Sprites/Characters/Beerus/tail/";
                    Sprite[] TailSprites = new Sprite[18]
                    {
                        ModAPI.LoadSprite(TailFolder + "tail_00.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_01.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_02.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_03.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_04.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_05.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_06.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_07.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_08.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_09.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_10.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_11.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_12.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_13.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_14.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_15.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_16.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_17.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.115f, 0f), "Default", 1, TailSprites, 80f, 90f, -9f, 12f, 0.002f, 0.001f, "Tail", true, false, false, 0.6f, false);

                    string Folder = "Assets/Sprites/Characters/Beerus/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 25000000f;

                    Instance.AddComponent<BlastBehaviour>();
                    var beerusBeam = Instance.AddComponent<BeamBehaviour>();
                    beerusBeam.EffectID = "Energy Wave";
                    beerusBeam.Character = "Beerus";
                    var beerusBall = Instance.AddComponent<BallBehaviour>();
                    beerusBall.EffectID = "Beerus Ball";
                    beerusBall.Character = "Beerus";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    PowerSetup.RegisterBeerusCombatPowers(pw.Powers, Instance);
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    var beerusAura = Instance.AddComponent<FlameOutlineAuraHandler>();
                    beerusAura.AuraType = typeof(PurpleFlameAura);
                    beerusAura.InnerAuraType = typeof(InnerBlueFlameAura);
                    beerusAura.Setup();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !beerusAura.AuraOn ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        beerusAura.ToggleAura();
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health = float.MaxValue / 100f;
                        limb.InitialHealth = float.MaxValue / 100f;
                        limb.BreakingThreshold = float.MaxValue / 100f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }

                    CeceUtils.DelayedInvoke(0.15f, () =>
                    {
                        var multipliers = Instance.GetComponent<MultipliersAndStuffs>();
                        if (multipliers != null)
                            multipliers.FormMultiplier = 5000000000f;
                    });
                },
                "Broly Movie",
                UniqueTag + ZFighterTag + "016");

            //BM Chichi
            ModAPIPlus.CreateHuman(
                "Chichi",
                "Goku! You promised me we'd live a normal life! Fighting isn't normal!",
                "BM/Chichi",
                (Instance) =>
                {
                    Instance.gameObject.name = "Chichi";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Chichi/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var scarftObject = new GameObject("Scarft");
                    scarftObject.transform.SetParent(upper);
                    scarftObject.transform.localPosition = new Vector3(0f, 0f);
                    scarftObject.transform.rotation = upper.rotation;
                    scarftObject.transform.localScale = new Vector3(1f, 1f);
                    var scarftSprite = scarftObject.AddComponent<SpriteRenderer>();
                    scarftSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Chichi/Scarf.png");
                    scarftSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    scarftSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var waistObject = new GameObject("Waist");
                    waistObject.transform.SetParent(lower);
                    waistObject.transform.localPosition = new Vector3(0f, 0f);
                    waistObject.transform.rotation = lower.rotation;
                    waistObject.transform.localScale = new Vector3(1f, 1f);
                    var waisttSprite = waistObject.AddComponent<SpriteRenderer>();
                    waisttSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Chichi/Waist.png");
                    waisttSprite.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    waisttSprite.sortingOrder = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder;

                    string AccessoriesFolder = "Assets/Sprites/Characters/Chichi/";

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "UpperBody").transform, new Vector2(-0.13f, 0.03f), "Foreground", 1, new Sprite[4] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/sn.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/sn1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/sn2.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/sn3.png"),
                    }
                    , 0f, 15f, -2f, 20f, 0.0018f, 0.001f, "Scarf");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "UpperBody").transform, new Vector2(0.07f, 0.085f), "Foreground", 1, new Sprite[3] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snb.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snb1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snb2.png"),
                    }
                    , -15f, 18f, -10f, 11f, 0.0018f, 0.001f, "Scarf");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "UpperBody").transform, new Vector2(0.07f, 0.115f), "Foreground", 1, new Sprite[3] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snf.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snf1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/snf2.png"),
                    }
                    , -15f, 18f, -10f, 6f, 0.0018f, 0.001f, "Scarf");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.045f, 0.13f), "Foreground", 1, new Sprite[16] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf2.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf3.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf4.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf5.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf6.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf7.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf8.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf9.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf10.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf11.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf12.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf13.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf14.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drf15.png"),
                    }
                    , -7f, 0f, -10f, 0f, 0.0018f, 0.001f, "Waist");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.045f, 0.13f), "Foreground", 1, new Sprite[15] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb2.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb3.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb4.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb5.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb6.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb7.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb8.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb9.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb10.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb11.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb12.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb13.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/drb14.png"),
                    }
                    , 0f, 7f, 0f, 10f, 0.0018f, 0.001f, "Waist");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.13f), "Foreground", 1, new Sprite[5] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stb.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stb1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stb2.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stb3.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stb4.png"),
                    }
                    , -20f, 32f, -17f, 25f, 0.0018f, 0.001f, "Waist");

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.13f, 0.13f), "Foreground", 1, new Sprite[6] {
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf1.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf2.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf3.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf4.png"),
                    ModAPI.LoadSprite(AccessoriesFolder + "Chen/stf5.png"),
                    }
                    , -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Waist");

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.63f);

                    string Folder = "Assets/Sprites/Characters/Chichi/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 800f;
                        limb.InitialHealth *= 800f;
                        limb.BreakingThreshold *= 5f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.6f;
                        limb.GForcePassoutThreshold *= 10f;
                    }
                },
                "Broly Movie",
                UniqueTag + HumanTag + "001");

            //BM Bulma
            ModAPIPlus.CreateHuman(
                "Bulma",
                "You might be the Prince of Saiyans, but I'm the Queen of Attitude!",
                "BM/Bulma",
                (Instance) =>
                {
                    Instance.gameObject.name = "Bulma";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Bulma/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var scarftObject = new GameObject("Scarft");
                    scarftObject.transform.SetParent(upper);
                    scarftObject.transform.localPosition = new Vector3(0f, 0f);
                    scarftObject.transform.rotation = upper.rotation;
                    scarftObject.transform.localScale = new Vector3(1f, 1f);
                    var scarftSprite = scarftObject.AddComponent<SpriteRenderer>();
                    scarftSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Bulma/Scarf.png");
                    scarftSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    scarftSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.65f);

                    string Folder = "Assets/Sprites/Characters/Bulma/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 50f;
                        limb.InitialHealth *= 50f;
                        limb.BreakingThreshold *= 1.5f;
                    }
                },
                "Broly Movie",
                UniqueTag + HumanTag + "002");

            //BM Cheelai
            ModAPIPlus.CreateHuman(
                "Cheelai",
                "",
                "BM/Cheelai",
                (Instance) =>
                {
                    Instance.gameObject.name = "Cheelai";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cheelai/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.52f);

                    string Folder = "Assets/Sprites/Characters/Cheelai/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 8000f;
                        limb.InitialHealth *= 8000f;
                        limb.BreakingThreshold *= 10f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.5f;
                        limb.GForcePassoutThreshold *= 10f;
                    }
                },
                "Broly Movie",
                UniqueTag + HumanTag + "003");

            //BM Lemo
            ModAPIPlus.CreateHuman(
                "Lemo",
                "",
                "BM/Lemo",
                (Instance) =>
                {
                    Instance.gameObject.name = "Lemo";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hatObject = new GameObject("Hat");
                    hatObject.transform.SetParent(head);
                    hatObject.transform.localPosition = new Vector3(0f, 0f);
                    hatObject.transform.rotation = head.rotation;
                    hatObject.transform.localScale = new Vector3(1f, 1f);
                    var hatSprite = hatObject.AddComponent<SpriteRenderer>();
                    hatSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Lemo/Hat.png");
                    hatSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hatSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.54f);

                    string Folder = "Assets/Sprites/Characters/Lemo/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head2.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin, false);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 40000f;
                        limb.InitialHealth *= 40000f;
                        limb.BreakingThreshold *= 40f;

                        limb.SkinMaterialHandler.intensityMultiplier = 0.36f;
                        limb.GForcePassoutThreshold *= 10f;
                    }
                },
                "Broly Movie",
                UniqueTag + HumanTag + "004");

            //BM Paragus
            ModAPIPlus.CreateHuman(
                "Paragus",
                "",
                "BM/Paragus",
                (Instance) =>
                {
                    Instance.gameObject.name = "Paragus";

                    var person = Instance.GetComponent<PersonBehaviour>();
                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Paragus/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var sholderObject = new GameObject("Sholder Pad");
                    sholderObject.transform.SetParent(uarmf);
                    sholderObject.transform.localPosition = new Vector3(0f, 0f);
                    sholderObject.transform.rotation = uarmf.rotation;
                    sholderObject.transform.localScale = new Vector3(1f, 1f);
                    var sholderSprite = sholderObject.AddComponent<SpriteRenderer>();
                    sholderSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Paragus/SholderPad.png");
                    sholderSprite.sortingLayerName = uarmf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sholderSprite.sortingOrder = uarmf.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var sholderObject2 = new GameObject("Sholder Pad");
                    sholderObject2.transform.SetParent(uarmb);
                    sholderObject2.transform.localPosition = new Vector3(0f, 0f);
                    sholderObject2.transform.rotation = uarmb.rotation;
                    sholderObject2.transform.localScale = new Vector3(1f, 1f);
                    var sholderSprite2 = sholderObject2.AddComponent<SpriteRenderer>();
                    sholderSprite2.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Paragus/SholderPad.png");
                    sholderSprite2.sortingLayerName = uarmb.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sholderSprite2.sortingOrder = uarmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var waistObject = new GameObject("Waist");
                    waistObject.transform.SetParent(lower);
                    waistObject.transform.localPosition = new Vector3(0f, 0f);
                    waistObject.transform.rotation = lower.rotation;
                    waistObject.transform.localScale = new Vector3(1f, 1f);
                    var waisttSprite = waistObject.AddComponent<SpriteRenderer>();
                    waisttSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Paragus/Waist.png");
                    waisttSprite.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    waisttSprite.sortingOrder = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.81f);

                    string Folder = "Assets/Sprites/Characters/Paragus/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 50000000000f;
                        limb.InitialHealth *= 50000000000f;
                        limb.BreakingThreshold *= 50000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.6f;
                        limb.GForcePassoutThreshold *= 10f;
                    }
                },
                "Broly Movie",
                UniqueTag + HumanTag + "005");

            ModAPIPlus.CreateHuman(
                "(BM) Bardock",
                "Bardock from Dragon Ball Super: Broly.",
                "BM/Bardock",
                SetupBmBardockSpawn,
                "Broly Movie",
                UniqueTag + ZFighterTag + "020");

            ModAPIPlus.CreateHuman(
                "(BM) Chiaotzu",
                "Chiaotzu in his Super Fit look from Dragon Ball Super: Broly.",
                "BM/Chiaotzu",
                SetupBmChiaotzuSpawn,
                "Broly Movie",
                UniqueTag + ZFighterTag + "021");



            ModAPIPlus.CreateHuman(
                "Tenshinhan",
                "A disciplined martial artist from the Crane School, ready to face the Saiyans.",
                "SaiyanSaga/Tenshinhan",
                SetupSaiyanSagaTenshinhanSpawn,
                "Saiyan Saga",
                UniqueTag + ZFighterTag + "001");

            ModAPIPlus.CreateHuman(
                "Kid Gohan",
                "Gohan as a child during the Saiyan Saga, training under Piccolo.",
                "SaiyanSaga/Kid Gohan",
                SetupSaiyanSagaKidGohanSpawn,
                "Saiyan Saga",
                UniqueTag + ZFighterTag + "002");

            ModAPIPlus.CreateHuman(
                "Nappa",
                "Vegeta's towering Saiyan partner from the Saiyan Saga.",
                "SaiyanSaga/Nappa",
                SetupSaiyanSagaNappaSpawn,
                "Saiyan Saga",
                UniqueTag + VillanTag + "001");

            ModAPIPlus.CreateHuman(
                "Vegeta",
                "The Prince of all Saiyans in his Saiyan Saga armor.",
                "SaiyanSaga/Vegeta",
                SetupSaiyanSagaVegetaSpawn,
                "Saiyan Saga",
                UniqueTag + VillanTag + "002");

            ModAPIPlus.CreateHuman(
                "Oozaru",
                "A Great Ape Saiyan. Titan-scale body for now; joints will be tuned before form wiring.",
                "SaiyanSaga/Oozaru",
                SetupSaiyanSagaOozaruSpawn,
                "Saiyan Saga",
                UniqueTag + VillanTag + "003");

            ModAPIPlus.CreateHuman(
                "Farmer",
                "A farmer from the Saiyan Saga era.",
                "DBSSH/Farmer",
                SetupDbsshFarmerSpawn,
                "Saiyan Saga",
                UniqueTag + HumanTag + "001");

            ModAPIPlus.CreateHuman(
                "Chiaotzu",
                "Tien's small psychic partner from the Saiyan Saga.",
                "SaiyanSaga/Chiaotzu",
                SetupSaiyanSagaChiaotzuSpawn,
                "Saiyan Saga",
                UniqueTag + ZFighterTag + "021");



            ModAPIPlus.CreateHuman(
                "Teen Gohan",
                "Gohan during the Cell Games era.",
                "BM/Teen Gohan",
                SetupBmTeenGohanSpawn,
                "Cell Games",
                UniqueTag + ZFighterTag + "001");

            ModAPIPlus.CreateHuman(
                "Android 17",
                "Android 17 in his Cell Games look.",
                "CellGames/17",
                SetupCellGamesAndroid17Spawn,
                "Cell Games",
                UniqueTag + ZFighterTag + "017");

            ModAPIPlus.CreateHuman(
                "Android 18",
                "Android 18 in her Cell Games look.",
                "CellGames/18",
                SetupCellGamesAndroid18Spawn,
                "Cell Games",
                UniqueTag + ZFighterTag + "018");



            ModAPIPlus.CreateHuman(
                "Bardock",
                "Goku's father from Dragon Ball Z: Bardock - The Father of Goku.",
                "StoryOfBardock/Bardock",
                SetupStoryOfBardockSpawn,
                "Story of Bardock",
                UniqueTag + ZFighterTag + "001");



            //DBSSH Gohan
            ModAPIPlus.CreateHuman(
                "(DBSSH) Gohan",
                "Gohan from Dragon Ball Super: Super Hero, including his Beast transformation.",
                "DBSSH/Gohan",
                (Instance) =>
                {
                    Instance.gameObject.name = "Gohan_Beast";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string BeltFolder = "Assets/Sprites/Characters/Gohan/Thingy1/";
                    Sprite[] BeltSprites1 = new Sprite[7]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stb.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                        ModAPI.LoadSprite(BeltFolder + "stb6.png")
                        };

                    BeltFolder = "Assets/Sprites/Characters/Gohan/Thingy2/";
                    Sprite[] BeltSprites2 = new Sprite[6]
                    {
                        ModAPI.LoadSprite(BeltFolder + "stf.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                        ModAPI.LoadSprite(BeltFolder + "stf5.png")
                        };

                    string Folder = "Assets/Sprites/Characters/Gohan/DemonClothesSkin/";
                    List<Sprite> DemonClothesSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage1/";
                    List<Sprite> DemonClothesDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesSkin/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesSkin/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage2/";
                    List<Sprite> DemonClothesDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage3/";
                    List<Sprite> DemonClothesDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage1/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/DemonClothesDamage4/";
                    List<Sprite> DemonClothesDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/DemonClothesDamage3/LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGI/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage1/";
                    List<Sprite> TurtleGIDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage2/";
                    List<Sprite> TurtleGIDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage3/";
                    List<Sprite> TurtleGIDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage4/";
                    List<Sprite> TurtleGIDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/TurtleGIDamage5/";
                    List<Sprite> TurtleGIDamage5 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    Folder = "Assets/Sprites/Characters/Gohan/BlackSuit/";
                    List<Sprite> BlackSuit = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Gohan/BlackSuitDamage1/";
                    List<Sprite> BlackSuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                    person.ChangeSkin(DemonClothesSkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Dictionary<string, List<Sprite>> DemonClothesSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", DemonClothesSkin },
                        { "Damage1", DemonClothesDamage1 },
                        { "Damage2", DemonClothesDamage2 },
                        { "Damage3", DemonClothesDamage3 },
                        { "Damage4", DemonClothesDamage4 }
                        };

                    Dictionary<string, List<Sprite>> TurtleGiSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", TurtleGISkin },
                        { "Damage1", TurtleGIDamage1 },
                        { "Damage2", TurtleGIDamage2 },
                        { "Damage3", TurtleGIDamage3 },
                        { "Damage4", TurtleGIDamage4 },
                        { "Damage5", TurtleGIDamage5 }
                        };

                    Dictionary<string, List<Sprite>> BlackSuitSkins = new Dictionary<string, List<Sprite>>
                    {
                        { "Damage0", BlackSuit },
                        { "Damage1", BlackSuitDamage1 }
                        };

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "Nothing special about it, other than being a Saiyan with amazing potential.",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "The basic Super Saiyan form of any Saiyan. Gohan achieved this transformation during his training in the Hyperbolic Time Chamber with Goku, which pushed his limits and allowed him to ascend to a Super Saiyan like his father.\n\nThis form has the least Ki drain among all transformations.",

                        ID = "SSJ1",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    var pssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 80f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair pssj.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/PSSJ.png"),
                        KiDrainRate = 0f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/pssj.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/pssj.png"),
                        Name = "Perfect Super Saiyan",
                        Description = "The Perfect version of Super Saiyan 1, this form was acquired after Gohan and his Father Goku trained for 3 Years, in order to becomer stronger and defeat the Enemy of that time, Perfect Cell.\n\nThis form drains no Ki, and so it can be used freely.",

                        ID = "PSSJ",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("PSSJ", pssjTransformation);

                    var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ssj2.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/SSJ2.png"),
                        KiDrainRate = 10000f,
                        AuraID = "SSJ2",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ssj2.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ssj2.png"),
                        Name = "Super Saiyan 2",
                        Description = "Gohan's iconic transformation as a child, obtained after witnessing Perfect Cell mercilessly crush Android 16. This act filled Gohan's heart with pure rage, triggering an explosive boost in power.\n\nThis transformation drains more Ki than Super Saiyan 1 but compensates with increased strength and speed.",

                        ID = "SSJ2",
                        SkinDamageAmount = 1
                    };
                    characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                    var ultimateTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair ultimate.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/Ultimate.png"),
                        KiDrainRate = 6000f,
                        AuraID = "Mystic",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/ultimate.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/ultimate.png"),
                        Name = "Mystic Gohan",
                        Description = "AKA Ultimate Gohan, this transformation represents Gohan's alternative path among Saiyan transformations, bypassing the traditional Super Saiyan forms as a base for advanced power-ups.\n\nThis form has minimal Ki drain compared to Super Saiyan 2 and is significantly stronger as well.",

                        ID = "Ultimate",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Ultimate", ultimateTransformation);
                    var beastTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1000000000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Hair beast.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/HeadSkins/Beast.png"),
                        KiDrainRate = 15000f,
                        AuraID = "BeastGohan",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Icons/beast.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Illustrations/beast.png"),
                        Name = "Beast Gohan",
                        Description = "Gohan's ultimate awakening against Cell Max, unleashing his Beast form with overwhelming power from his dormant potential.\n\nThis transformation drains a large amount of Ki while active.",

                        ID = "BeastGohan",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("BeastGohan", beastTransformation);


                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
                    var characterSkins = Instance.AddComponent<CharacterSkins>();
                    skinsCM.SkinsData = characterSkins;

                    var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Demon Clothes",
                        Name = "DemonClothes"
                    };
                    BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BaseSkin.Skins["DemonClothes"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", DemonClothesSkins } };
                    BaseSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/DemonClothes_Damage4.png") },
                        };
                    characterSkins.skins.AddSkin("DemonClothes", BaseSkin);

                    var TurtleGi = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Turtle Gi",
                        Name = "TurtleGi"
                    };
                    TurtleGi.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    TurtleGi.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins } };
                    TurtleGi.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage1.png") },
                        { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage2.png") },
                        { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage3.png") },
                        { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage4.png") },
                        { "Damage5", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/TurtleGi_Damage5.png") }
                        };
                    var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1285f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites1,
                        minAngleFirstPiece = -15f,
                        maxAngleFirstPiece = 32f,
                        minAngle = -17f,
                        maxAngle = 25f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    var belt2 = new CharacterSkins.Skins.PhyAccessoryData()
                    {
                        objForAttach = lower.transform,
                        connectedAnchor = new Vector2(0.1f, 0.1286f),
                        layerName = "Foreground",
                        order = 1,
                        sprites = BeltSprites2,
                        minAngleFirstPiece = -32f,
                        maxAngleFirstPiece = 15f,
                        minAngle = -25f,
                        maxAngle = 17f,
                        pieceMass = 0.0018f,
                        pieceInertia = 0.001f,
                        Name = "Belt",
                        HasCollision = true,
                        CollideJustWithGround = true
                    };
                    TurtleGi.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData>();
                    TurtleGi.PhyAccessories.Add(belt1);
                    TurtleGi.PhyAccessories.Add(belt2);
                    characterSkins.skins.AddSkin("TurtleGi", TurtleGi);

                    var BlackSuitSkin = new CharacterSkins.Skins.SkinInfo()
                    {
                        DisplayName = "Black Suit",
                        Name = "BlackSuit"
                    };
                    BlackSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    BlackSuitSkin.Skins["BlackSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackSuitSkins } };
                    BlackSuitSkin.Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/BlackSuit_Damage0.png") },
                        { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/SkinIllustrations/BlackSuit_Damage1.png") }
                        };
                    characterSkins.skins.AddSkin("BlackSuit", BlackSuitSkin);

                    DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Gohan/", "Head.png");

                    skinsCM.CurrentSkin = "DemonClothes";
                    skinsCM.PreviousSkin = "DemonClothes";

                    head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = head.gameObject.GetComponent<SkinContextMenu>();

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.AddComponent<HellzoneGranade>();
                    Instance.AddComponent<KiShield>();
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    var gohanBeam = PowerSetup.BeamBlock(r, PowerSetup.StandardBeamOnEnable(r, true));
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.GrantMany(gohanBeam, "EnergyWave", "Kamehameha");
                    if (r.Hellzone != null) pw.Grant("HellzoneGranade");
                    if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                    {
                        OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
                    });
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 4800000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 4800000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 4800000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "002");

            //DBSSH Piccolo
            ModAPIPlus.CreateHuman(
            "(DBSSH) Piccolo",
            "Piccolo with his Power Awakened transformation from Dragon Ball Super: Super Hero.",
            "DBSSH/Piccolo",
            (Instance) =>
            {
                Instance.gameObject.name = "Piccolo_Awakened";

                var person = Instance.GetComponent<PersonBehaviour>();
                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");

                lower.gameObject.AddComponent<Flight>();

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Base/head thingy.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 2.26f);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                kiSystem.maxKi = 750000f;
                kiSystem.baseRegen = 8000f;

                string Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Base/";
                List<Sprite> baseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/BaseDamage1/";
                List<Sprite> baseDamage1Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Awakened/";
                List<Sprite> awakenedSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/AwakenedDamage1/";
                List<Sprite> awakenedDamage1Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Orange/";
                List<Sprite> orangeSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/OrangeDamage1/";
                List<Sprite> orangeDamage1Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                person.ChangeSkin(baseSkin);

                var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1f,
                    Size = 2.26f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Base/head thingy.png"),
                    HeadSprite = null,
                    KiDrainRate = 0f,
                    AuraID = "Base",

                    RequiredSkin = baseSkin,

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Icons/Base.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Illustrations/Base.png"),
                    Name = "Base",
                    Description = "Piccolo's regular form before awakening his latent power.",

                    ID = "Base"
                };
                characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                var powerAwakenedTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 100000f,
                    Size = 2.26f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Awakened/head thingy.png"),
                    HeadSprite = null,
                    KiDrainRate = 1000f,
                    AuraID = "PowerAwakened",

                    RequiredSkin = awakenedSkin,

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Icons/Awaken.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Illustrations/Awaken.png"),
                    Name = "Power Awakened",
                    Description = "Piccolo awakens his latent power, taking on his empowered appearance with a yellow-green aura.\n\nThis transformation drains a moderate amount of Ki while active.",

                    ID = "PowerAwakened"
                };
                characterTransformations.transformations.AddTransformation("PowerAwakened", powerAwakenedTransformation);

                var orangeTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 50000000f,
                    Size = 2.52f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Skins/DemonClothes/Orange/head thingy.png"),
                    HeadSprite = null,
                    KiDrainRate = 5000f,
                    AuraID = "OrangePiccolo",
                    requiredKiColor = new Color(255f / 255f, 210f / 255f, 50f / 255f),

                    RequiredSkin = orangeSkin,

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Icons/Orange.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/Illustrations/Orange.png"),
                    Name = "Orange Piccolo",
                    Description = "Piccolo pushes his awakened power even further, becoming gigantic with a blazing golden-orange aura in his fight against Gamma 2.\n\nThis is his strongest form and drains the most Ki.",

                    ID = "OrangePiccolo",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("OrangePiccolo", orangeTransformation);

                CeceUtils.DelayedInvoke(0.1f, () =>
                {
                    characterTransformations.ApplyTransformation(person, "Base");
                });

                head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                head.gameObject.GetComponent<TransformationContextMenu>().person = person;





                Dictionary<string, List<Sprite>> DemonClothesSkins_Base = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", baseSkin },
                    { "Damage1", baseDamage1Skin }
                    };

                Dictionary<string, List<Sprite>> DemonClothesSkins_Awakened = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", awakenedSkin },
                    { "Damage1", awakenedDamage1Skin }
                    };

                Dictionary<string, List<Sprite>> DemonClothesSkins_Orange = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", orangeSkin },
                    { "Damage1", orangeDamage1Skin }
                    };

                var characterSkins = Instance.AddComponent<CharacterSkins>();
                var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();

                skinsCM.SkinsData = characterSkins;

                var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Demon Clothes",
                    Name = "DemonClothes"
                };
                BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BaseSkin.Skins["DemonClothes"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "Base", DemonClothesSkins_Base }, { "PowerAwakened", DemonClothesSkins_Awakened }, { "OrangePiccolo", DemonClothesSkins_Orange } };
                BaseSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/SkinIllustrations/DemonClothes_Damage0.png") },
                    { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Piccolo/SkinIllustrations/DemonClothes_Damage1.png") },
                    };
                BaseSkin.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData>();
                characterSkins.skins.AddSkin("DemonClothes", BaseSkin);

                DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Piccolo/", "Head.png");

                skinsCM.CurrentSkin = "DemonClothes";
                skinsCM.PreviousSkin = "DemonClothes";

                head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;





                Instance.AddComponent<BlastBehaviour>();
                Instance.AddComponent<BeamBehaviour>().EffectID = "Energy Wave";
                Instance.AddComponent<HellzoneGranade>();
                Instance.AddComponent<KiShield>();
                Instance.AddComponent<PiccoloGiantForm>();
                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                PowerSetup.AddPiccoloGrowthPower(pw.Powers, Instance.GetComponent<PiccoloGiantForm>());
                var piccoloBeam = PowerSetup.BeamBlock(r, PowerSetup.StandardBeamOnEnable(r, true));
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.BeamStillBlocked,
                    OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.GrantMany(piccoloBeam, "EnergyWave", "SpecialBeamCannon");
                if (r.Hellzone != null) pw.Grant("HellzoneGranade");
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
                });
                PowerSetup.GrantPiccoloEyeLaser(person, Instance, pw.Powers);
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                Instance.AddComponent<AuraHandler>().person = person;

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 3500000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 3500000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 3500000000000000f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "001");

            //DBSSH Goku
            ModAPIPlus.CreateHuman(
            "(DBSSH) Goku",
            "Hey, It's me, Goku!",
            "DBSSH/Goku",
            (Instance) =>
            {
                Instance.gameObject.name = "Goku";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                var person = Instance.GetComponent<PersonBehaviour>();

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                string BeltFolder = "Assets/Sprites/Characters/Goku/Thingy1/";
                Sprite[] BeltSprites1 = new Sprite[7]
                {
                    ModAPI.LoadSprite(BeltFolder + "stb.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                    ModAPI.LoadSprite(BeltFolder + "stb6.png")
                };

                PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                BeltFolder = "Assets/Sprites/Characters/Goku/Thingy2/";
                Sprite[] BeltSprites2 = new Sprite[6]
                {
                    ModAPI.LoadSprite(BeltFolder + "stf.png"),
                    ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                    ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                    ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                    ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                    ModAPI.LoadSprite(BeltFolder + "stf5.png")
                };

                PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                string Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGI/";
                List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage1/";
                List<Sprite> TurtleGIDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage2/";
                List<Sprite> TurtleGIDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage3/";
                List<Sprite> TurtleGIDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage4/";
                List<Sprite> TurtleGIDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage5/";
                List<Sprite> TurtleGIDamage5 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGI_Buffed/";
                List<Sprite> TurtleGISkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage1_Buffed/";
                List<Sprite> TurtleGIDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage2_Buffed/";
                List<Sprite> TurtleGIDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage3_Buffed/";
                List<Sprite> TurtleGIDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage4_Buffed/";
                List<Sprite> TurtleGIDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage3/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGIDamage5_Buffed/";
                List<Sprite> TurtleGIDamage5_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/TurtleGIDamage4/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };




                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkin/";
                List<Sprite> SuitSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage1/";
                List<Sprite> SuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage2/";
                List<Sprite> SuitDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage3/";
                List<Sprite> SuitDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage4/";
                List<Sprite> SuitDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkin_Buffed/";
                List<Sprite> SuitSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage1_Buffed/";
                List<Sprite> SuitDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage2_Buffed/";
                List<Sprite> SuitDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage3_Buffed/";
                List<Sprite> SuitDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/SayanSuitSkinDamage4_Buffed/";
                List<Sprite> SuitDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/FarmerSkin/";
                List<Sprite> FarmerSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/FarmerSkin_Buffed/";
                List<Sprite> FarmerSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/CasualSkin/";
                List<Sprite> CasualSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/CasualSkin_Buffed/";
                List<Sprite> CasualSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/YardratSkin/";
                List<Sprite> Yardrat = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/YardratSkin_Buffed/";
                List<Sprite> Yardrat_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGINoWeightSkin/";
                List<Sprite> TurtleGINoWeight = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Goku/Skins/TurtleGINoWeightSkin_Buffed/";
                List<Sprite> TurtleGINoWeight_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                person.ChangeSkin(TurtleGISkin);

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                kiSystem.maxKi = 1000000f;
                kiSystem.baseRegen = 10000f;

                Dictionary<string, List<Sprite>> TurtleGiSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", TurtleGISkin },
                    { "Damage1", TurtleGIDamage1 },
                    { "Damage2", TurtleGIDamage2 },
                    { "Damage3", TurtleGIDamage3 },
                    { "Damage4", TurtleGIDamage4 },
                    { "Damage5", TurtleGIDamage5 }
                    };

                Dictionary<string, List<Sprite>> TurtleGiSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", TurtleGISkin_Buffed },
                    { "Damage1", TurtleGIDamage1_Buffed },
                    { "Damage2", TurtleGIDamage2_Buffed },
                    { "Damage3", TurtleGIDamage3_Buffed },
                    { "Damage4", TurtleGIDamage4_Buffed },
                    { "Damage5", TurtleGIDamage5_Buffed }
                    };

                Dictionary<string, List<Sprite>> SuitSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", SuitSkin },
                    { "Damage1", SuitDamage1 },
                    { "Damage2", SuitDamage2 },
                    { "Damage3", SuitDamage3 },
                    { "Damage4", SuitDamage4 }
                    };

                Dictionary<string, List<Sprite>> SuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", SuitSkin_Buffed },
                    { "Damage1", SuitDamage1_Buffed },
                    { "Damage2", SuitDamage2_Buffed },
                    { "Damage3", SuitDamage3_Buffed },
                    { "Damage4", SuitDamage4_Buffed }
                    };

                Dictionary<string, List<Sprite>> FarmerSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", FarmerSkin }
                    };

                Dictionary<string, List<Sprite>> FarmerSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", FarmerSkin_Buffed }
                    };

                Dictionary<string, List<Sprite>> CasualSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", CasualSkin }
                    };

                Dictionary<string, List<Sprite>> CasualSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", CasualSkin_Buffed }
                    };

                Dictionary<string, List<Sprite>> YardratSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", Yardrat }
                    };

                Dictionary<string, List<Sprite>> YardratSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", Yardrat_Buffed }
                    };

                Dictionary<string, List<Sprite>> TurtleGINoWeightSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", TurtleGINoWeight }
                    };

                Dictionary<string, List<Sprite>> TurtleGINoWeightSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", TurtleGINoWeight_Buffed }
                    };


                var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/Base.png"),
                    KiDrainRate = 0f,
                    AuraID = "Base",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Base.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Base.png"),
                    Name = "Base",
                    Description = "Goku's Base form, nothing special about it, just the good old Goku we know.",

                    ID = "Base",
                    SkinDamageAmount = 0
                };
                characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 50f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj1.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ1.png"),
                    KiDrainRate = 4000f,
                    AuraID = "SSJ1",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj1.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj1.png"),
                    Name = "Super Saiyan 1",
                    Description = "The first Super Saiyan of the series, described as the Legendary Super Saiyan, Goku unleashed this Form after losing his best friend Krillin to the hand of Lord Frieza.\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                    ID = "SSJ1",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                var grade1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 70f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj grade1.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJGrade1.png"),
                    KiDrainRate = 8000f,
                    AuraID = "SSJ1",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/grade1.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/grade1.png"),
                    Name = "Super Saiyan Grade 2",
                    Description = "After training with Gohan for a while, Goku learn how to buff himself into a stronger stage of Super Saiyan 1, and although it gives more power, it takes away the speed of the user. \n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger, but slower.",

                    ID = "SSJGrade2",
                    SkinDamageAmount = 1,
                    RequiredSkin_Suffix = "_Buffed"
                };
                characterTransformations.transformations.AddTransformation("SSJGrade2", grade1Transformation);

                var pssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 80f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair pssj.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ1.png"),
                    KiDrainRate = 0f,
                    AuraID = "SSJ1",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/pssj.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/pssj.png"),
                    Name = "Perfect Super Saiyan",
                    Description = "The Perfect version of Super Saiyan 1, this form was acquired after Goku and his Son Gohan trained for 3 Years, to achieve the Perfect version of a Regular Super Saiyan rather then buffing his muscles.\n\nThis form drains no Ki, and so it can be used freely.",

                    ID = "PSSJ",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("PSSJ", pssjTransformation);

                var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 100f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj2.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ2.png"),
                    KiDrainRate = 10000f,
                    AuraID = "SSJ2",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj2.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj2.png"),
                    Name = "Super Saiyan 2",
                    Description = "A Super Saiyan that went beyond the limits of a regular Super Saiyan, Goku unleashed this transformation sometime during his training on the Otherworld.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger and faster.",

                    ID = "SSJ2",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                var ssj3Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 400f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj3.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJ3.png"),
                    KiDrainRate = 25000f,
                    AuraID = "SSJ3",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssj3.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssj3.png"),
                    Name = "Super Saiyan 3",
                    Description = "A transformation that goes even further beyond, the iconic transformation of long golden hair, that was obtained during his time on the Otherworld, just like Super Sayian 2.\n\nThis is Goku's most expensive form out of all regular Saiyan transformations, but it's also the strongest out of all Non-Divine forms.",

                    ID = "SSJ3",
                    SkinDamageAmount = 2
                };
                characterTransformations.transformations.AddTransformation("SSJ3", ssj3Transformation);

                var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 10000f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssjg.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJG.png"),
                    KiDrainRate = 5000f,
                    AuraID = "SSJGod",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssjg.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssjg.png"),
                    Name = "Super Saiyan God",
                    Description = "The form that results from a Saiyan using Divine Ki, was acquired by Goku by making a ritual with 6 pure hearted saiyans, to fight Lord Beerus and stop him from destroying the planet.\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                    ID = "SSJGod",
                    SkinDamageAmount = 0
                };
                characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 500000f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssjb.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/SSJB.png"),
                    KiDrainRate = 12000f,
                    AuraID = "SSJBlue",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/ssjb.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/ssjb.png"),
                    Name = "Super Saiyan Blue",
                    Description = "Essentially a Super Saiyan God but merged with the regular Super Saiyan, or also known as Super Saiyan God Super Saiyan, was obtained by Goku when he was training with Whis to master Ki Control, discovering the ability to use both transformations at once.\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                    ID = "SSJBlue",
                    SkinDamageAmount = 2
                };
                characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                var pssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 600000f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair pssjb.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/PSSJB.png"),
                    KiDrainRate = 6000f,
                    AuraID = null,

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/pssjb.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/pssjb.png"),
                    Name = "Perfected Super Saiyan Blue",
                    Description = "The Perfected version of Super Saiyan Blue, this transformation is slightly stronger then the regular one, but when comes to Ki Drain, it's way better and more stable then regular Super Saiyan Blue.\n\nThe Ki Drain it's low, and still has an immense power.",

                    ID = "PSSJBlue",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("PSSJBlue", pssjbTransformation);

                var uiTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1000000000f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ui.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/UI.png"),
                    KiDrainRate = 8000f,
                    AuraID = "UI",

                    Icon = LoadCharacterFormIcon("Goku", "UI.png"),
                    Illustration = LoadCharacterFormIllustration("Goku", "UI.png"),
                    Name = "Ultra Instinct",
                    Description = "Ultra Instinct",

                    ID = "UI",
                    SkinDamageAmount = 2
                };
                characterTransformations.transformations.AddTransformation("UI", uiTransformation);

                var muiTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1500000000f,
                    Size = 1.75f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair mui.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/MUI.png"),
                    KiDrainRate = 10000f,
                    AuraID = "MUI",

                    Icon = LoadCharacterFormIcon("Goku", "MUI.png"),
                    Illustration = LoadCharacterFormIllustration("Goku", "MUI.png"),
                    Name = "Mastered Ultra Instinct",
                    Description = "Mastered Ultra Instinct",

                    ID = "MUI",
                    SkinDamageAmount = 2
                };
                characterTransformations.transformations.AddTransformation("MUI", muiTransformation);

                CeceUtils.DelayedInvoke(0.1f, () =>
                {
                    characterTransformations.ApplyTransformation(person, "Base");
                });

                head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                var characterSkins = Instance.AddComponent<CharacterSkins>();
                var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();

                skinsCM.SkinsData = characterSkins;

                var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Turtle Gi",
                    Name = "TurtleGi"
                };
                BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BaseSkin.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins } };
                BaseSkin.Skins["TurtleGi_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGiSkins_Buffed } };
                BaseSkin.Illustrations = new Dictionary<string, Sprite>
                        {
                            { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage0.png") },
                            { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage1.png") },
                            { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage2.png") },
                            { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage3.png") },
                            { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage4.png") },
                            { "Damage5", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGi_Damage5.png") }
                            };
                var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
                {
                    objForAttach = lower.transform,
                    connectedAnchor = new Vector2(0.1285f, 0.1286f),
                    layerName = "Foreground",
                    order = 1,
                    sprites = BeltSprites1,
                    minAngleFirstPiece = -15f,
                    maxAngleFirstPiece = 32f,
                    minAngle = -17f,
                    maxAngle = 25f,
                    pieceMass = 0.0018f,
                    pieceInertia = 0.001f,
                    Name = "Belt",
                    HasCollision = true,
                    CollideJustWithGround = true
                };
                var belt2 = new CharacterSkins.Skins.PhyAccessoryData()
                {
                    objForAttach = lower.transform,
                    connectedAnchor = new Vector2(0.1f, 0.1286f),
                    layerName = "Foreground",
                    order = 1,
                    sprites = BeltSprites2,
                    minAngleFirstPiece = -32f,
                    maxAngleFirstPiece = 15f,
                    minAngle = -25f,
                    maxAngle = 17f,
                    pieceMass = 0.0018f,
                    pieceInertia = 0.001f,
                    Name = "Belt",
                    HasCollision = true,
                    CollideJustWithGround = true
                };
                BaseSkin.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData>();
                BaseSkin.PhyAccessories.Add(belt1);
                BaseSkin.PhyAccessories.Add(belt2);
                characterSkins.skins.AddSkin("TurtleGi", BaseSkin);

                var SaiyanSuitSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Battle Armor",
                    Name = "SaiyanSuit"
                };
                SaiyanSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                SaiyanSuitSkin.Skins["SaiyanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins } };
                SaiyanSuitSkin.Skins["SaiyanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins_Buffed } };
                SaiyanSuitSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage0.png") },
                    { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage1.png") },
                    { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage2.png") },
                    { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage3.png") },
                    { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/SaiyanSuit_Damage4.png") },
                    };
                characterSkins.skins.AddSkin("SaiyanSuit", SaiyanSuitSkin);

                var FarmSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Farmer Skin",
                    Name = "FarmerSkin"
                };
                FarmSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                FarmSkin.Skins["FarmerSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", FarmerSkins } };
                FarmSkin.Skins["FarmerSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", FarmerSkins_Buffed } };
                FarmSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/FarmerSkin.png") }
                    };
                characterSkins.skins.AddSkin("FarmerSkin", FarmSkin);

                var CasualSkin_ = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Casual Skin",
                    Name = "CasualSkin"
                };
                CasualSkin_.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                CasualSkin_.Skins["CasualSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", CasualSkins } };
                CasualSkin_.Skins["CasualSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", CasualSkins_Buffed } };
                CasualSkin_.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/CasualSkin.png") }
                    };
                characterSkins.skins.AddSkin("CasualSkin", CasualSkin_);

                var YardratSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Yardrat Skin",
                    Name = "YardratSkin"
                };
                YardratSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                YardratSkin.Skins["YardratSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", YardratSkins } };
                YardratSkin.Skins["YardratSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", YardratSkins_Buffed } };
                YardratSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/YardratSkin.png") }
                    };
                characterSkins.skins.AddSkin("YardratSkin", YardratSkin);

                var TurtleGINoWeightSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Turtle GI No Weights",
                    Name = "TurtleGINoWeightSkin"
                };
                TurtleGINoWeightSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                TurtleGINoWeightSkin.Skins["TurtleGINoWeightSkin"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGINoWeightSkins } };
                TurtleGINoWeightSkin.Skins["TurtleGINoWeightSkin_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", TurtleGINoWeightSkins_Buffed } };
                TurtleGINoWeightSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/SkinIllustrations/TurtleGINoWeightSkin.png") }
                    };
                characterSkins.skins.AddSkin("TurtleGINoWeightSkin", TurtleGINoWeightSkin);

                DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Goku/", "Head.png");

                skinsCM.CurrentSkin = "TurtleGi";
                skinsCM.PreviousSkin = "TurtleGi";

                head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                Instance.AddComponent<Kaioken>();

                Instance.AddComponent<BlastBehaviour>();

                var beamBehaviour = Instance.AddComponent<BeamBehaviour>();
                beamBehaviour.EffectID = "Kamehameha";
                beamBehaviour.Character = "Goku";

                CeceUtils.DelayedInvoke(0.15f, () =>
                {
                    if (beamBehaviour != null) beamBehaviour.RegisterArmBeamTriggers();
                });

                Instance.AddComponent<SolarFlare>().Character = "Goku";

                Instance.AddComponent<KiShield>();

                var gokuBall = Instance.GetComponent<BallBehaviour>() ?? Instance.AddComponent<BallBehaviour>();
                gokuBall.EffectID = "Spirit Bomb";
                gokuBall.Character = "Goku";

                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                PowerSetup.RegisterGokuCombatPowers(pw);
                pw.Finish();

                lower.gameObject.AddComponent<Flight>();

                pw.Grant("InstantTransmission");
                pw.Powers.MovePowerBlockToStart(new[] { "SpiritBomb", "SuperSpiritBomb" });

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                () =>
                {
                    Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                    person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                    person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                }));

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 5000000000000000f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "003");

            //DBSSH Vegeta
            ModAPIPlus.CreateHuman(
            "(DBSSH) Vegeta",
            "Meaningless, huh?! What do you know of meaningless?! Spend most of your life ruled by another! Watch your race dwindle to a handful! And then tell me what has more meaning then your own strength!",
            "DBSSH/Vegeta",
            (Instance) =>
            {
                Instance.gameObject.name = "Vegeta";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                var person = Instance.GetComponent<PersonBehaviour>();

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;



                string Folder = "Assets/Sprites/Characters/Vegeta/BlueShirt/";
                List<Sprite> BlueShirt = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/";
                List<Sprite> BlueShirtDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/";
                List<Sprite> BlueShirtDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/";
                List<Sprite> BlueShirtDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage4/";
                List<Sprite> BlueShirtDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3/LArm.png") };


                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirt_Buffed/";
                List<Sprite> BlueShirt_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/";
                List<Sprite> BlueShirtDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/";
                List<Sprite> BlueShirtDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/";
                List<Sprite> BlueShirtDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BlueShirtDamage4_Buffed/";
                List<Sprite> BlueShirtDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/BlueShirtDamage3_Buffed/LArm.png") };



                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkin/";
                List<Sprite> SuitSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/";
                List<Sprite> SuitDamage1 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/";
                List<Sprite> SuitDamage2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/";
                List<Sprite> SuitDamage3 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage4/";
                List<Sprite> SuitDamage4 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3/LArm.png") };


                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkin_Buffed/";
                List<Sprite> SuitSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/";
                List<Sprite> SuitDamage1_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/";
                List<Sprite> SuitDamage2_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/";
                List<Sprite> SuitDamage3_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage1_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage4_Buffed/";
                List<Sprite> SuitDamage4_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/ULeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/LLeg.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage2_Buffed/Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/UArm.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SayanSuitSkinDamage3_Buffed/LArm.png") };



                Folder = "Assets/Sprites/Characters/Vegeta/BadmanSuit/";
                List<Sprite> BadmanSuit = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Vegeta/BadmanSuit_Buffed/";
                List<Sprite> BadmanSuit_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };



                person.ChangeSkin(BlueShirt);

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.64f);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                kiSystem.maxKi = 1000000f;
                kiSystem.baseRegen = 10000f;


                Dictionary<string, List<Sprite>> SuitSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", SuitSkin },
                    { "Damage1", SuitDamage1 },
                    { "Damage2", SuitDamage2 },
                    { "Damage3", SuitDamage3 },
                    { "Damage4", SuitDamage4 }
                    };

                Dictionary<string, List<Sprite>> SuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", SuitSkin_Buffed },
                    { "Damage1", SuitDamage1_Buffed },
                    { "Damage2", SuitDamage2_Buffed },
                    { "Damage3", SuitDamage3_Buffed },
                    { "Damage4", SuitDamage4_Buffed }
                    };


                Dictionary<string, List<Sprite>> BlueShirtSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", BlueShirt },
                    { "Damage1", BlueShirtDamage1 },
                    { "Damage2", BlueShirtDamage2 },
                    { "Damage3", BlueShirtDamage3 },
                    { "Damage4", BlueShirtDamage4 }
                    };

                Dictionary<string, List<Sprite>> BlueShirtSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", BlueShirt_Buffed },
                    { "Damage1", BlueShirtDamage1_Buffed },
                    { "Damage2", BlueShirtDamage2_Buffed },
                    { "Damage3", BlueShirtDamage3_Buffed },
                    { "Damage4", BlueShirtDamage4_Buffed }
                    };


                Dictionary<string, List<Sprite>> BadmanSuitSkins = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", BadmanSuit }
                    };

                Dictionary<string, List<Sprite>> BadmanSuitSkins_Buffed = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", BadmanSuit_Buffed }
                    };


                var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/Base.png"),
                    KiDrainRate = 0f,
                    AuraID = "Base",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/Base.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/Base.png"),
                    Name = "Base",
                    Description = "Vegeta in his Base form, the Prince of Saiyans with his unbeatable pride.",

                    ID = "Base",
                    SkinDamageAmount = 0
                };
                characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 50f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj1.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ1.png"),
                    KiDrainRate = 4000f,
                    AuraID = "SSJ1",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssj1.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssj1.png"),
                    Name = "Super Saiyan 1",
                    Description = "Only a Pure Hearted Saiyan can become a Super Saiyan, and he was pure, Pure Evil. He obtained this transformation after owning the self-hatred of his physical limits causing the Super Saiyan within him to awaken.\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                    ID = "SSJ1",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                var superTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 70f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj grade1.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ1.png"),
                    KiDrainRate = 8000f,
                    AuraID = "SSJ1",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/super.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/super.png"),
                    Name = "Super Vegeta (SSJ Grade 2)",
                    Description = "With his rigorous self-training, Vegeta was able to surpass the power of a regular Super Saiyan, only to try and become stronger then Goku.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger, but slower.",

                    ID = "SSJGrade2",
                    SkinDamageAmount = 1,
                    RequiredSkin_Suffix = "_Buffed"
                };
                characterTransformations.transformations.AddTransformation("SSJGrade2", superTransformation);

                var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 100f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssj2.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJ2.png"),
                    KiDrainRate = 10000f,
                    AuraID = "SSJ2",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssj2.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssj2.png"),
                    Name = "Super Saiyan 2",
                    Description = "With his rigorous self-training, Vegeta was able to surpass the power of a regular Super Saiyan, only to try and become stronger then Goku.\n\nThis transformation drains more Ki than a Super Saiyan 1, in exchange of being stronger and faster.",

                    ID = "SSJ2",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

                var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 10000f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjg.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJG.png"),
                    KiDrainRate = 5000f,
                    AuraID = "SSJGod",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjg.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjg.png"),
                    Name = "Super Saiyan God",
                    Description = "Unlike Goku, that obtained Divine Ki from the God Ritual, Vegeta obtained it from hard training with Whis, archiving this transformation all by himself.\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                    ID = "SSJGod",
                    SkinDamageAmount = 0
                };
                characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 500000f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjb.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJB.png"),
                    KiDrainRate = 12000f,
                    AuraID = "SSJBlue",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjb.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjb.png"),
                    Name = "Super Saiyan Blue",
                    Description = "Just like Goku, Vegeta learned how to infuse the power of a regular Super Saiyan into the Divine Form of a Saiyan.\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                    ID = "SSJBlue",
                    SkinDamageAmount = 2
                };
                characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                var pssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 600000f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair pssjb.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/PSSJB.png"),
                    KiDrainRate = 6000f,
                    AuraID = null,

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/pssjb.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/pssjb.png"),
                    Name = "Perfected Super Saiyan Blue",
                    Description = "The Perfected version of Super Saiyan Blue, this transformation is slightly stronger then the regular one, but when comes to Ki Drain, it's way better and more stable then regular Super Saiyan Blue.\n\nThe Ki Drain it's low, and still has an immense power.",

                    ID = "PSSJBlue",
                    SkinDamageAmount = 1
                };
                characterTransformations.transformations.AddTransformation("PSSJBlue", pssjbTransformation);

                var ssjbeTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 10000000f,
                    Size = 1.64f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair ssjbe.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/HeadSkins/SSJBE.png"),
                    KiDrainRate = 16000f,
                    AuraID = "SSJBlueEvolution",

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Icons/ssjbe.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/Illustrations/ssjbe.png"),
                    Name = "Super Saiyan Blue Evolution",
                    Description = "The direct evolution of Super Saiyan Blue, much more powerful, was obtained during Vegeta's Fight agains't Jiren on the Tournament of Power, after pushing through his limits several times.\n\nThis transformation can be extremly powerful, but also consumes a lot of Ki as a down side.",

                    ID = "SSJBlueEvolution",
                    SkinDamageAmount = 2,
                    RequiredSkin_Suffix = "_Buffed"
                };
                characterTransformations.transformations.AddTransformation("SSJBlueEvolution", ssjbeTransformation);

                CeceUtils.DelayedInvoke(0.1f, () =>
                {
                    characterTransformations.ApplyTransformation(person, "Base");
                });

                head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
                var characterSkins = Instance.AddComponent<CharacterSkins>();
                skinsCM.SkinsData = characterSkins;

                var BlueShirtSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Blue Shirt",
                    Name = "BlueShirt"
                };
                BlueShirtSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BlueShirtSkin.Skins["BlueShirt"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlueShirtSkins } };
                BlueShirtSkin.Skins["BlueShirt_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlueShirtSkins_Buffed } };
                BlueShirtSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage0.png") },
                    { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage1.png") },
                    { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage2.png") },
                    { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage3.png") },
                    { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BlueShirt_Damage4.png") },
                    };
                characterSkins.skins.AddSkin("BlueShirt", BlueShirtSkin);

                var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Battle Armor",
                    Name = "SaiyanSuit"
                };
                BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BaseSkin.Skins["SaiyanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins } };
                BaseSkin.Skins["SaiyanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", SuitSkins_Buffed } };
                BaseSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage0.png") },
                    { "Damage1", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage1.png") },
                    { "Damage2", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage2.png") },
                    { "Damage3", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage3.png") },
                    { "Damage4", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/SaiyanSuit_Damage4.png") },
                    };
                characterSkins.skins.AddSkin("SaiyanSuit", BaseSkin);

                var BadmanSuitSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Badman Suit",
                    Name = "BadmanSuit"
                };
                BadmanSuitSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BadmanSuitSkin.Skins["BadmanSuit"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BadmanSuitSkins } };
                BadmanSuitSkin.Skins["BadmanSuit_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BadmanSuitSkins_Buffed } };
                BadmanSuitSkin.Illustrations = new Dictionary<string, Sprite>
                {
                    { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/SkinIllustrations/BadmanSuit.png") }
                    };
                characterSkins.skins.AddSkin("BadmanSuit", BadmanSuitSkin);

                DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Vegeta/", "Head.png");

                skinsCM.CurrentSkin = "SaiyanSuit";
                skinsCM.PreviousSkin = "SaiyanSuit";

                head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                Instance.AddComponent<BlastBehaviour>();

                //Instance.AddComponent<BeamBehaviour>().EffectID = "Final Flash";
                Instance.AddComponent<BeamBehaviour>().EffectID = "Galick Gun";
                Instance.GetComponent<BeamBehaviour>().Character = "Vegeta";

                Instance.AddComponent<FinalExplosion>();
                Instance.AddComponent<KiShield>();

                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                var vegetaBeam = new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = PowerSetup.VegetaBeamOnEnable(r) };
                pw.Grant("KiBlast", new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = PowerSetup.VegetaKiOnEnable(r) });
                pw.GrantMany(vegetaBeam, "EnergyWave", "GalickGun", "FinalFlash");
                pw.Grant("FinalExplosion");
                pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; r.FinalExplosion.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);

                lower.gameObject.AddComponent<Flight>();
                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                () =>
                {
                    Instance.GetComponent<BlastBehaviour>().Active = !Instance.GetComponent<BlastBehaviour>().Active;

                    person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                    person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                }));

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    //limb.BaseStrength *= 22f;
                    //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                    //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                    limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 5000000000000000f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                    //limb.CirculationBehaviour.ImmuneToDamage = true;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "004");

            //DBSSH Broly
            ModAPIPlus.CreateHuman(
            "(DBSSH) Broly",
            "",
            "DBSSH/Broly",
            (Instance) =>
            {
                Instance.gameObject.name = "Broly";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                var person = Instance.GetComponent<PersonBehaviour>();

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                var bahEarObject_Front = new GameObject("Bah Ear Front");
                bahEarObject_Front.transform.SetParent(lower);
                bahEarObject_Front.transform.localPosition = new Vector3(0f, 0f);
                bahEarObject_Front.transform.rotation = lower.rotation;
                bahEarObject_Front.transform.localScale = new Vector3(1f, 1f);
                var bahEarSprite_Front = bahEarObject_Front.AddComponent<SpriteRenderer>();
                bahEarSprite_Front.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/BahEar_Front.png");
                bahEarSprite_Front.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                bahEarSprite_Front.sortingOrder = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                var bahEarObject_Back = new GameObject("Bah Ear Back");
                bahEarObject_Back.transform.SetParent(lower);
                bahEarObject_Back.transform.localPosition = new Vector3(0f, 0f);
                bahEarObject_Back.transform.rotation = lower.rotation;
                bahEarObject_Back.transform.localScale = new Vector3(1f, 1f);
                var bahEarSprite_Back = bahEarObject_Back.AddComponent<SpriteRenderer>();
                bahEarSprite_Back.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/BahEar_Back.png");
                bahEarSprite_Back.sortingLayerName = larmb.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                bahEarSprite_Back.sortingOrder = larmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder;

                uarmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder = -1;
                larmb.gameObject.GetComponent<SpriteRenderer>().sortingOrder = -1;

                string Folder = "Assets/Sprites/Characters/Broly/BattleArmor/";
                List<Sprite> BattleArmorSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/BattleArmor_Buffed/";
                List<Sprite> BattleArmorSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/BattleArmor_Buffed2/";
                List<Sprite> BattleArmorSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Broly/BlackShirt/";
                List<Sprite> BlackShirtSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/BlackShirt_Buffed/";
                List<Sprite> BlackShirtSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/BlackShirt_Buffed2/";
                List<Sprite> BlackShirtSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };


                Folder = "Assets/Sprites/Characters/Broly/Shirtless/";
                List<Sprite> ShirtlessSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/Shirtless_Buffed/";
                List<Sprite> ShirtlessSkin_Buffed = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                Folder = "Assets/Sprites/Characters/Broly/Shirtless_Buffed2/";
                List<Sprite> ShirtlessSkin_Buffed2 = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                person.ChangeSkin(BlackShirtSkin);

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.85f);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                kiSystem.maxKi = 2500000f;
                kiSystem.baseRegen = 20000f;
                kiSystem.requiredColor = new Color(0f / 255f, 255f / 255f, 0f / 255f);

                Dictionary<string, List<Sprite>> BlackShirtSkins = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin } };

                Dictionary<string, List<Sprite>> BlackShirtSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin_Buffed } };

                Dictionary<string, List<Sprite>> BlackShirtSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", BlackShirtSkin_Buffed2 } };


                Dictionary<string, List<Sprite>> BattleArmorSkins = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin } };

                Dictionary<string, List<Sprite>> BattleArmorSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin_Buffed } };

                Dictionary<string, List<Sprite>> BattleArmorSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", BattleArmorSkin_Buffed2 } };


                Dictionary<string, List<Sprite>> ShirtlessSkins = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin } };

                Dictionary<string, List<Sprite>> ShirtlessSkins_Buffed = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin_Buffed } };

                Dictionary<string, List<Sprite>> ShirtlessSkins_Buffed2 = new Dictionary<string, List<Sprite>> { { "Damage0", ShirtlessSkin_Buffed2 } };


                var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 1f,
                    Size = 1.85f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/Base.png"),
                    KiDrainRate = 0f,
                    AuraID = "Broly",
                    requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/Base.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/Base.png"),
                    Name = "Base",
                    Description = "Broly in his calmest state, with an unimaginable potential deep inside.",

                    ID = "Base",
                    SkinDamageAmount = 0
                };
                characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                var ikariTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 10f,
                    Size = 2.13f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair ikari.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/Ikari.png"),
                    KiDrainRate = 0f,
                    AuraID = "Broly",
                    requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/ikari.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/ikari.png"),
                    Name = "Ikari",
                    Description = "Also known as Berserk State, Broly loses control over his actions and starts a rampage. This form is essentially Broly's version of oozaru.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation drains no Ki.",

                    ID = "Ikari",
                    SkinDamageAmount = 0,
                    RequiredSkin_Suffix = "_Buffed"
                };
                ikariTransformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                characterTransformations.transformations.AddTransformation("Ikari", ikariTransformation);

                var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 50f,
                    Size = 2.24f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair ssj1.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/SSJ1.png"),
                    KiDrainRate = 8000f,
                    AuraID = "Broly",
                    requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/ssj1.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/ssj1.png"),
                    Name = "Super Saiyan 1",
                    Description = "The basic Super Saiyan state, after seeing his dad Paragus, laid on the ground, killed by Frieza, he loses his mind completely and gets consumed by unstoppable rage.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation drains a low amount of Ki.",

                    ID = "SSJ1",
                    SkinDamageAmount = 0,
                    RequiredSkin_Suffix = "_Buffed2"
                };
                ssj1Transformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                var fpssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                {
                    PowerMultiplier = 500f,
                    Size = 2.74f,
                    HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/hair fpssj.png"),
                    HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/HeadSkins/FPSSJ.png"),
                    KiDrainRate = 15000f,
                    AuraID = "Broly",
                    requiredKiColor = new Color(0f / 255f, 255f / 255f, 0f / 255f),

                    Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Icons/fpssj.png"),
                    Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/Illustrations/fpssj.png"),
                    Name = "Full Power Super Saiyan",
                    Description = "The combination between Broly's mastery over Oozaru's power with the Super Saiyan power. He Awakened this form in the middle of his Fight agains't Gogeta.\n\nThis Form provides an Exponential Growth over time, up to a certain point.\n\nThis transformation is the stronguest of them all, and it's also the most Ki expensive one.",

                    ID = "FPSSJ",
                    SkinDamageAmount = 0,
                    RequiredSkin_Suffix = "_Buffed2"
                };
                fpssjTransformation.SpecialBehaviours.Add(typeof(ExponentialGrowth));
                characterTransformations.transformations.AddTransformation("FPSSJ", fpssjTransformation);

                //GameObject.Destroy(temp);

                CeceUtils.DelayedInvoke(0.1f, () =>
                {
                    characterTransformations.ApplyTransformation(person, "Base");
                });

                head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                var characterSkins = Instance.AddComponent<CharacterSkins>();
                var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();

                skinsCM.SkinsData = characterSkins;

                var BaseSkin = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Black Shirt",
                    Name = "BlackShirt"
                };
                BaseSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BaseSkin.Skins["BlackShirt"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins } };
                BaseSkin.Skins["BlackShirt_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins_Buffed } };
                BaseSkin.Skins["BlackShirt_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BlackShirtSkins_Buffed2 } };
                BaseSkin.Illustrations = new Dictionary<string, Sprite>
                        {
                            { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/BlackShirt_Damage0.png") }
                            };
                characterSkins.skins.AddSkin("BlackShirt", BaseSkin);

                var BattleArmor = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Battle Armor",
                    Name = "BattleArmor"
                };
                BattleArmor.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                BattleArmor.Skins["BattleArmor"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins } };
                BattleArmor.Skins["BattleArmor_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins_Buffed } };
                BattleArmor.Skins["BattleArmor_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", BattleArmorSkins_Buffed2 } };
                BattleArmor.Illustrations = new Dictionary<string, Sprite>
                        {
                            { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/BattleArmor_Damage0.png") }
                            };
                characterSkins.skins.AddSkin("BattleArmor", BattleArmor);

                var Shirtless = new CharacterSkins.Skins.SkinInfo()
                {
                    DisplayName = "Shirtless",
                    Name = "Shirtless"
                };
                Shirtless.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                Shirtless.Skins["Shirtless"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins } };
                Shirtless.Skins["Shirtless_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins_Buffed } };
                Shirtless.Skins["Shirtless_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", ShirtlessSkins_Buffed2 } };
                Shirtless.Illustrations = new Dictionary<string, Sprite>
                        {
                            { "Damage0", ModAPI.LoadSprite("Assets/Sprites/Characters/Broly/SkinIllustrations/Shirtless_Damage0.png") }
                            };
                characterSkins.skins.AddSkin("Shirtless", Shirtless);

                DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, "Assets/Sprites/Characters/Broly/", "Head.png");

                skinsCM.CurrentSkin = "BlackShirt";
                skinsCM.PreviousSkin = "BlackShirt";

                head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

                Instance.AddComponent<BlastBehaviour>();



                Instance.AddComponent<BeamBehaviour>().EffectID = "Gigantic Full Blast";
                Instance.GetComponent<BeamBehaviour>().Character = "Broly";
                Instance.AddComponent<BallBehaviour>().EffectID = "Gigantic Crusher";
                Instance.GetComponent<BallBehaviour>().Character = "Broly";
                Instance.AddComponent<KiShield>();
                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                Action brolyBeamOff = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; };
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.Grant("EnergyWave", new PowerCatalog.Overrides { BlockWhen = () => r.BeamStillBlocked, OnEnable = brolyBeamOff, BeamType = "ArmBeam" });
                pw.GrantMany(PowerSetup.BeamBlock(r, brolyBeamOff), "GiganticFullBlast", "GiganticRoar");
                if (r.Ball != null) pw.Grant("GiganticCrusher");
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);

                lower.gameObject.AddComponent<Flight>();

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                lower.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleBahEar", (Func<string>)(() => (bahEarSprite_Front.color == Color.clear) ? "Add Bah's Ear" : "Remove Bah's Ear"), "Toggle Bah's Ear",
                () =>
                {
                    if (bahEarSprite_Front.color == Color.clear)
                    {
                        bahEarSprite_Front.color = Color.white;
                        bahEarSprite_Back.color = Color.white;
                    }

                    else
                    {
                        bahEarSprite_Front.color = Color.clear;
                        bahEarSprite_Back.color = Color.clear;
                    }
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 50000000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 50000000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 50000000000000000f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "005");

            //DBSSH Krillin
            ModAPIPlus.CreateHuman(
            "(DBSSH) Krillin",
            "GOKUUUUU!!",
            "DBSSH/Krillin",
            (Instance) =>
            {
                Instance.gameObject.name = "Krillin";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                var person = Instance.GetComponent<PersonBehaviour>();

                head.CreateAccessory("Helmet", ModAPI.LoadSprite("Assets/Sprites/Characters/Krillin/PoliceManSkin/Helmet.png"));

                lower.gameObject.AddComponent<Flight>();

                string Folder = "Assets/Sprites/Characters/Krillin/PoliceManSkin/";
                List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };
                person.ChangeSkin(TurtleGISkin);

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.53f);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = 200000f;
                kiSystem.baseRegen = 2500f;

                Instance.AddComponent<BlastBehaviour>();
                Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                Instance.AddComponent<SolarFlare>().Character = "Krillin";
                Instance.AddComponent<KiShield>();
                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.Beam.StillActive,
                    OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; if (r.Solar != null) r.Solar.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.GrantMany(PowerSetup.GokuBeamStyle(), "EnergyWave", "Kamehameha");
                if (r.Solar != null) pw.Grant("SolarFlare");
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; r.Blast.Active = false; if (r.Solar != null) r.Solar.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                Instance.AddComponent<AuraHandler>().person = person;

                bool Helmet = true;

                head.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleHelmet", (Func<string>)(() => !Helmet ? "Wear Helmet" : "Take Off Helmet"), "Toggle Helmet",
                () =>
                {
                    Helmet = !Helmet;
                    head.ModifyAccessory("Helmet", color: (Helmet) ? Color.white : Color.clear);
                }));

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    //limb.BaseStrength *= 22f;
                    //limb.PhysicalBehaviour.TrueInitialMass *= 20f;
                    //limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                    limb.Health *= 90000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 90000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 90000000000000f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.05f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                    //limb.CirculationBehaviour.ImmuneToDamage = true;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.StrongLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "006");

            //DBSSH Teen Goten
            ModAPIPlus.CreateHuman(
                "(DBSSH) Teen Goten",
                "",
                "DBSSH/Teen Goten",
                (Instance) =>
                {
                    Instance.gameObject.name = "Teen Goten";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Teen Goten/Skin/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.72f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.72f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.72f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Goten/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "",

                        ID = "SSJ1",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goten";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 180000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 180000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 180000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "007");

            //DBSSH Teen Trunks
            ModAPIPlus.CreateHuman(
                "(DBSSH) Teen Trunks",
                "",
                "DBSSH/Teen Trunks",
                (Instance) =>
                {
                    Instance.gameObject.name = "Teen Trunks";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Teen Trunks/Skin/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.70f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.70f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/HeadSkins/Base.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/Icons/Base.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/Illustrations/Base.png"),
                        Name = "Base",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 50f,
                        Size = 1.70f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/hair ssj1.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/HeadSkins/SSJ1.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJ1",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/Icons/ssj1.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Trunks/Illustrations/ssj1.png"),
                        Name = "Super Saiyan 1",
                        Description = "",

                        ID = "SSJ1",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Kid Trunks";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    person.Limbs[11].gameObject.AddComponent<FusionHandler>();
                    person.Limbs[13].gameObject.AddComponent<FusionHandler>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleFusion", (Func<string>)(() => person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse ? "Disable Fusion" : "Enable Fusion"), "Toggle Fusion",
                    () =>
                    {
                        person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[11].gameObject.GetComponent<FusionHandler>().canFuse;
                        person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse = !person.Limbs[13].gameObject.GetComponent<FusionHandler>().canFuse;
                    }));

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 200000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 200000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 200000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "008");

            //DBSSH Pan
            ModAPIPlus.CreateHuman(
                "(DBSSH) Pan",
                "Gohan and Videl's daughter from Dragon Ball Super: Super Hero.",
                "DBSSH/Pan",
                (Instance) =>
                {
                    Instance.gameObject.name = "Pan";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    string folder = "Assets/Sprites/Characters/Pan/Superhero/Casual/";
                    string hairPath = folder + "hair.png";

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(-0.0281f, 0.04f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite(hairPath);
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    person.ChangeSkin(LoadCharacterLimbSprites(folder));

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.32f);

                    head.gameObject.AddComponent<Child>();

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 1000f;

                    var casualSkin = LoadCharacterLimbSprites(folder);

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.32f,
                        HairSprite = ModAPI.LoadSprite(hairPath),
                        HeadSprite = ModAPI.LoadSprite(folder + "Head.png"),
                        UseHairLocalPosition = true,
                        HairLocalPosition = new Vector3(-0.0281f, 0.04f, 0f),
                        KiDrainRate = 0f,
                        AuraID = "Base",
                        RequiredSkin = casualSkin,

                        Icon = ModAPI.LoadSprite(folder + "Head.png"),
                        Illustration = ModAPI.LoadSprite(folder + "Head.png"),
                        Name = "Casual",
                        Description = "",

                        ID = "Base",
                        SkinDamageAmount = 0
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    DbpuCustomSkinLoader.SetupCharacterFolderSkins(Instance, head, folder, "Head.png", "Casual");

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goten";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 25000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 25000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 25000000000000f;

                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                    }
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "Pan");

            //DBSSH Cell Max
            ModAPIPlus.CreateHuman(
            "(DBSSH) Cell Max",
            "The colossal ultimate form from Dragon Ball Super: Super Hero, rampaging with overwhelming destructive power.",
            "DBSSH/Cell Max",
            (Instance) =>
            {

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    if (!limb.HasBrain)
                    {
                        limb.transform.parent.localPosition = Vector3.zero;
                    }
                    else
                    {
                        limb.transform.GetChild(0).gameObject.SetActive(false); //brain matter stuff
                    }
                    if (limb.gameObject.GetComponent<GripBehaviour>() != null)
                    {
                        GameObject.Destroy(limb.gameObject.GetComponent<GripBehaviour>());
                    }
                }

                PersonBehaviour person = Instance.GetComponent<PersonBehaviour>();
                person.CountDeathAsStat = false;
                GameObject Limb1 = person.Limbs[0].gameObject;
                Limb1.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin1.png");
                Limb1.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh1.png").texture);
                Limb1.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone1.png").texture);
                Limb1.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage1.png").texture);
                Limb1.transform.localPosition = new Vector3(0.3564368f, 4.965211f, 0f);
                Limb1.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb1.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Head;
                Limb1.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb1.GetComponent<SpriteRenderer>().sortingOrder = 12;
                Limb1.GetComponent<LimbBehaviour>().BaseStrength = 0.6f;
                Limb1.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb1.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb1.GetComponent<LimbBehaviour>().Health = 50f;
                Limb1.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb1.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb1.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb1.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb1.GetComponent<CirculationBehaviour>().IsPump = true;
                Limb1.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb1.GetComponent<LimbBehaviour>().IsLethalToBreak = true;
                Limb1.GetComponent<LimbBehaviour>().HasBrain = true;
                GameObject Limb2 = person.Limbs[1].gameObject;
                Limb2.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin2.png");
                Limb2.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh2.png").texture);
                Limb2.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone2.png").texture);
                Limb2.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage2.png").texture);
                Limb2.transform.localPosition = new Vector3(0.1267636f, 1.937831f, 0f);
                Limb2.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb2.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Torso;
                Limb2.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb2.GetComponent<SpriteRenderer>().sortingOrder = 14;
                Limb2.GetComponent<LimbBehaviour>().BaseStrength = 1f;
                Limb2.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb2.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb2.GetComponent<LimbBehaviour>().Health = 50f;
                Limb2.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb2.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb2.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb2.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb2.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb2.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb2.GetComponent<LimbBehaviour>().IsLethalToBreak = true;
                Limb2.GetComponent<LimbBehaviour>().HasBrain = true;
                GameObject Limb3 = person.Limbs[2].gameObject;
                Limb3.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin3.png");
                Limb3.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh3.png").texture);
                Limb3.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone3.png").texture);
                Limb3.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage3.png").texture);
                Limb3.transform.localPosition = new Vector3(-0.02989229f, -0.004373431f, 0f);
                Limb3.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb3.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Torso;
                Limb3.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb3.GetComponent<SpriteRenderer>().sortingOrder = 13;
                Limb3.GetComponent<LimbBehaviour>().BaseStrength = 1f;
                Limb3.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb3.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb3.GetComponent<LimbBehaviour>().Health = 50f;
                Limb3.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb3.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb3.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb3.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb3.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb3.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb3.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb3.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb4 = person.Limbs[3].gameObject;
                Limb4.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin4.png");
                Limb4.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh4.png").texture);
                Limb4.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone4.png").texture);
                Limb4.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage4.png").texture);
                Limb4.transform.localPosition = new Vector3(0.2701987f, -3.018804f, 0f);
                Limb4.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb4.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Torso;
                Limb4.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb4.GetComponent<SpriteRenderer>().sortingOrder = 11;
                Limb4.GetComponent<LimbBehaviour>().BaseStrength = 1f;
                Limb4.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb4.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb4.GetComponent<LimbBehaviour>().Health = 50f;
                Limb4.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb4.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb4.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb4.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb4.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb4.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb4.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb4.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb5 = person.Limbs[4].gameObject;
                Limb5.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin5.png");
                Limb5.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh5.png").texture);
                Limb5.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone5.png").texture);
                Limb5.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage5.png").texture);
                Limb5.transform.localPosition = new Vector3(0.3293337f, -5.976364f, -0.1f);
                Limb5.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb5.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb5.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
                Limb5.GetComponent<SpriteRenderer>().sortingOrder = 1;
                Limb5.GetComponent<LimbBehaviour>().BaseStrength = 0.6f;
                Limb5.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb5.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb5.GetComponent<LimbBehaviour>().Health = 50f;
                Limb5.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb5.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb5.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb5.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb5.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb5.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb5.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb5.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb6 = person.Limbs[5].gameObject;
                Limb6.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin6.png");
                Limb6.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh6.png").texture);
                Limb6.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone6.png").texture);
                Limb6.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage6.png").texture);
                Limb6.transform.localPosition = new Vector3(0.210327f, -10.17383f, -0.1f);
                Limb6.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb6.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb6.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
                Limb6.GetComponent<SpriteRenderer>().sortingOrder = 2;
                Limb6.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb6.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb6.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb6.GetComponent<LimbBehaviour>().Health = 50f;
                Limb6.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb6.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb6.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb6.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb6.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb6.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb6.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb6.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb7 = person.Limbs[6].gameObject;
                Limb7.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin7.png");
                Limb7.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh7.png").texture);
                Limb7.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone7.png").texture);
                Limb7.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage7.png").texture);
                Limb7.transform.localPosition = new Vector3(1.167726f, -12.70155f, -0.1f);
                Limb7.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb7.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb7.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
                Limb7.GetComponent<SpriteRenderer>().sortingOrder = 0;
                Limb7.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb7.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb7.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb7.GetComponent<LimbBehaviour>().Health = 50f;
                Limb7.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb7.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb7.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb7.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb7.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb7.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb7.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb7.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb8 = person.Limbs[7].gameObject;
                Limb8.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin8.png");
                Limb8.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh8.png").texture);
                Limb8.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone8.png").texture);
                Limb8.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage8.png").texture);
                Limb8.transform.localPosition = new Vector3(0.3298419f, -5.976082f, 0.1f);
                Limb8.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb8.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb8.GetComponent<SpriteRenderer>().sortingLayerName = "Background";
                Limb8.GetComponent<SpriteRenderer>().sortingOrder = 3;
                Limb8.GetComponent<LimbBehaviour>().BaseStrength = 0.6f;
                Limb8.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb8.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb8.GetComponent<LimbBehaviour>().Health = 50f;
                Limb8.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb8.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb8.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb8.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb8.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb8.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb8.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb8.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb9 = person.Limbs[8].gameObject;
                Limb9.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin9.png");
                Limb9.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh9.png").texture);
                Limb9.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone9.png").texture);
                Limb9.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage9.png").texture);
                Limb9.transform.localPosition = new Vector3(0.2105838f, -10.17304f, 0.1f);
                Limb9.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb9.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb9.GetComponent<SpriteRenderer>().sortingLayerName = "Background";
                Limb9.GetComponent<SpriteRenderer>().sortingOrder = 4;
                Limb9.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb9.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb9.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb9.GetComponent<LimbBehaviour>().Health = 50f;
                Limb9.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb9.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb9.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb9.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb9.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb9.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb9.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb9.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb10 = person.Limbs[9].gameObject;
                Limb10.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin10.png");
                Limb10.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh10.png").texture);
                Limb10.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone10.png").texture);
                Limb10.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage10.png").texture);
                Limb10.transform.localPosition = new Vector3(1.166202f, -12.702f, 0.1f);
                Limb10.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb10.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb10.GetComponent<SpriteRenderer>().sortingLayerName = "Background";
                Limb10.GetComponent<SpriteRenderer>().sortingOrder = 2;
                Limb10.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb10.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb10.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb10.GetComponent<LimbBehaviour>().Health = 50f;
                Limb10.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb10.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb10.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb10.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb10.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb10.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb10.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb10.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb11 = person.Limbs[10].gameObject;
                Limb11.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin11.png");
                Limb11.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh11.png").texture);
                Limb11.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone11.png").texture);
                Limb11.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage11.png").texture);
                Limb11.transform.localPosition = new Vector3(-0.1775571f, 0.3547351f, -0.1f);
                Limb11.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb11.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Arms;
                Limb11.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
                Limb11.GetComponent<SpriteRenderer>().sortingOrder = 3;
                Limb11.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb11.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb11.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb11.GetComponent<LimbBehaviour>().Health = 50f;
                Limb11.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb11.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb11.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb11.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb11.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb11.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb11.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb11.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb12 = person.Limbs[11].gameObject;
                Limb12.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin12.png");
                Limb12.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh12.png").texture);
                Limb12.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone12.png").texture);
                Limb12.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage12.png").texture);
                Limb12.transform.localPosition = new Vector3(-0.4069352f, -3.615456f, -0.1f);
                Limb12.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb12.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Arms;
                Limb12.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
                Limb12.GetComponent<SpriteRenderer>().sortingOrder = 4;
                Limb12.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb12.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb12.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb12.GetComponent<LimbBehaviour>().Health = 50f;
                Limb12.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb12.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb12.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb12.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb12.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb12.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb12.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb12.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb13 = person.Limbs[12].gameObject;
                Limb13.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin13.png");
                Limb13.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh13.png").texture);
                Limb13.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone13.png").texture);
                Limb13.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage13.png").texture);
                Limb13.transform.localPosition = new Vector3(-0.178601f, 0.3551872f, 0.1f);
                Limb13.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb13.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Arms;
                Limb13.GetComponent<SpriteRenderer>().sortingLayerName = "Background";
                Limb13.GetComponent<SpriteRenderer>().sortingOrder = 0;
                Limb13.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb13.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb13.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb13.GetComponent<LimbBehaviour>().Health = 50f;
                Limb13.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb13.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb13.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb13.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb13.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb13.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb13.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb13.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb14 = person.Limbs[13].gameObject;
                Limb14.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin14.png");
                Limb14.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh14.png").texture);
                Limb14.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone14.png").texture);
                Limb14.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage14.png").texture);
                Limb14.transform.localPosition = new Vector3(-0.406993f, -3.613419f, 0.1f);
                Limb14.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb14.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Arms;
                Limb14.GetComponent<SpriteRenderer>().sortingLayerName = "Background";
                Limb14.GetComponent<SpriteRenderer>().sortingOrder = 1;
                Limb14.GetComponent<LimbBehaviour>().BaseStrength = 0.4f;
                Limb14.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb14.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb14.GetComponent<LimbBehaviour>().Health = 50f;
                Limb14.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb14.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb14.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb14.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb14.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb14.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb14.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb14.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb15 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos15 = person.Limbs.ToList();
                limbos15.Add(Limb15.GetComponent<LimbBehaviour>());
                person.Limbs = limbos15.ToArray();
                GameObject.Destroy(Limb15.GetComponent<HingeJoint2D>());
                Limb15.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin15.png");
                Limb15.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh15.png").texture);
                Limb15.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone15.png").texture);
                Limb15.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage15.png").texture);
                Limb15.transform.localPosition = new Vector3(-4.251093f, -3.648158f, 0f);
                Limb15.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb15.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Torso;
                Limb15.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb15.GetComponent<SpriteRenderer>().sortingOrder = 15;
                Limb15.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb15.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb15.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb15.GetComponent<LimbBehaviour>().Health = 50f;
                Limb15.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb15.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb15.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb15.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb15.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb15.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb15.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb15.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb16 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos16 = person.Limbs.ToList();
                limbos16.Add(Limb16.GetComponent<LimbBehaviour>());
                person.Limbs = limbos16.ToArray();
                GameObject.Destroy(Limb16.GetComponent<HingeJoint2D>());
                Limb16.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin16.png");
                Limb16.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh16.png").texture);
                Limb16.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone16.png").texture);
                Limb16.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage16.png").texture);
                Limb16.transform.localPosition = new Vector3(-4.251758f, -3.647722f, 0f);
                Limb16.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb16.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Torso;
                Limb16.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb16.GetComponent<SpriteRenderer>().sortingOrder = 0;
                Limb16.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb16.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb16.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb16.GetComponent<LimbBehaviour>().Health = 50f;
                Limb16.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb16.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb16.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb16.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb16.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb16.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb16.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb16.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb17 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos17 = person.Limbs.ToList();
                limbos17.Add(Limb17.GetComponent<LimbBehaviour>());
                person.Limbs = limbos17.ToArray();
                GameObject.Destroy(Limb17.GetComponent<HingeJoint2D>());
                Limb17.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin17.png");
                Limb17.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh17.png").texture);
                Limb17.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone17.png").texture);
                Limb17.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage17.png").texture);
                Limb17.transform.localPosition = new Vector3(-2.629219f, -2.661656f, 0f);
                Limb17.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb17.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb17.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb17.GetComponent<SpriteRenderer>().sortingOrder = 10;
                Limb17.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb17.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb17.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb17.GetComponent<LimbBehaviour>().Health = 50f;
                Limb17.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb17.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb17.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb17.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb17.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb17.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb17.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb17.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb18 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos18 = person.Limbs.ToList();
                limbos18.Add(Limb18.GetComponent<LimbBehaviour>());
                person.Limbs = limbos18.ToArray();
                GameObject.Destroy(Limb18.GetComponent<HingeJoint2D>());
                Limb18.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin18.png");
                Limb18.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh18.png").texture);
                Limb18.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone18.png").texture);
                Limb18.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage18.png").texture);
                Limb18.transform.localPosition = new Vector3(-4.97981f, -2.705817f, 0f);
                Limb18.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb18.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb18.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb18.GetComponent<SpriteRenderer>().sortingOrder = 9;
                Limb18.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb18.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb18.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb18.GetComponent<LimbBehaviour>().Health = 50f;
                Limb18.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb18.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb18.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb18.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb18.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb18.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb18.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb18.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb19 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos19 = person.Limbs.ToList();
                limbos19.Add(Limb19.GetComponent<LimbBehaviour>());
                person.Limbs = limbos19.ToArray();
                GameObject.Destroy(Limb19.GetComponent<HingeJoint2D>());
                Limb19.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin19.png");
                Limb19.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh19.png").texture);
                Limb19.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone19.png").texture);
                Limb19.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage19.png").texture);
                Limb19.transform.localPosition = new Vector3(-7.621775f, -2.706101f, 0f);
                Limb19.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb19.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb19.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb19.GetComponent<SpriteRenderer>().sortingOrder = 8;
                Limb19.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb19.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb19.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb19.GetComponent<LimbBehaviour>().Health = 50f;
                Limb19.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb19.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb19.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb19.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb19.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb19.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb19.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb19.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb20 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos20 = person.Limbs.ToList();
                limbos20.Add(Limb20.GetComponent<LimbBehaviour>());
                person.Limbs = limbos20.ToArray();
                GameObject.Destroy(Limb20.GetComponent<HingeJoint2D>());
                Limb20.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin20.png");
                Limb20.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh20.png").texture);
                Limb20.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone20.png").texture);
                Limb20.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage20.png").texture);
                Limb20.transform.localPosition = new Vector3(-10.2621f, -2.72021f, 0f);
                Limb20.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb20.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb20.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb20.GetComponent<SpriteRenderer>().sortingOrder = 7;
                Limb20.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb20.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb20.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb20.GetComponent<LimbBehaviour>().Health = 50f;
                Limb20.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb20.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb20.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb20.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb20.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb20.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb20.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb20.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb21 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos21 = person.Limbs.ToList();
                limbos21.Add(Limb21.GetComponent<LimbBehaviour>());
                person.Limbs = limbos21.ToArray();
                GameObject.Destroy(Limb21.GetComponent<HingeJoint2D>());
                Limb21.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin21.png");
                Limb21.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh21.png").texture);
                Limb21.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone21.png").texture);
                Limb21.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage21.png").texture);
                Limb21.transform.localPosition = new Vector3(-12.64555f, -2.719755f, 0f);
                Limb21.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb21.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb21.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb21.GetComponent<SpriteRenderer>().sortingOrder = 6;
                Limb21.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb21.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb21.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb21.GetComponent<LimbBehaviour>().Health = 50f;
                Limb21.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb21.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb21.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb21.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb21.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb21.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb21.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb21.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb22 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos22 = person.Limbs.ToList();
                limbos22.Add(Limb22.GetComponent<LimbBehaviour>());
                person.Limbs = limbos22.ToArray();
                GameObject.Destroy(Limb22.GetComponent<HingeJoint2D>());
                Limb22.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin22.png");
                Limb22.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh22.png").texture);
                Limb22.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone22.png").texture);
                Limb22.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage22.png").texture);
                Limb22.transform.localPosition = new Vector3(-14.58636f, -2.720695f, 0f);
                Limb22.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb22.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb22.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb22.GetComponent<SpriteRenderer>().sortingOrder = 5;
                Limb22.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb22.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb22.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb22.GetComponent<LimbBehaviour>().Health = 50f;
                Limb22.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb22.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb22.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb22.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb22.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb22.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb22.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb22.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb23 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos23 = person.Limbs.ToList();
                limbos23.Add(Limb23.GetComponent<LimbBehaviour>());
                person.Limbs = limbos23.ToArray();
                GameObject.Destroy(Limb23.GetComponent<HingeJoint2D>());
                Limb23.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin23.png");
                Limb23.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh23.png").texture);
                Limb23.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone23.png").texture);
                Limb23.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage23.png").texture);
                Limb23.transform.localPosition = new Vector3(-16.41503f, -2.705335f, 0f);
                Limb23.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb23.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb23.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb23.GetComponent<SpriteRenderer>().sortingOrder = 4;
                Limb23.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb23.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb23.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb23.GetComponent<LimbBehaviour>().Health = 50f;
                Limb23.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb23.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb23.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb23.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb23.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb23.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb23.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb23.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb24 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos24 = person.Limbs.ToList();
                limbos24.Add(Limb24.GetComponent<LimbBehaviour>());
                person.Limbs = limbos24.ToArray();
                GameObject.Destroy(Limb24.GetComponent<HingeJoint2D>());
                Limb24.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin24.png");
                Limb24.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh24.png").texture);
                Limb24.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone24.png").texture);
                Limb24.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage24.png").texture);
                Limb24.transform.localPosition = new Vector3(-18.28483f, -2.676066f, 0f);
                Limb24.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb24.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb24.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb24.GetComponent<SpriteRenderer>().sortingOrder = 3;
                Limb24.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb24.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb24.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb24.GetComponent<LimbBehaviour>().Health = 50f;
                Limb24.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb24.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb24.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb24.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb24.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb24.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb24.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb24.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb25 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos25 = person.Limbs.ToList();
                limbos25.Add(Limb25.GetComponent<LimbBehaviour>());
                person.Limbs = limbos25.ToArray();
                GameObject.Destroy(Limb25.GetComponent<HingeJoint2D>());
                Limb25.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin25.png");
                Limb25.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh25.png").texture);
                Limb25.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone25.png").texture);
                Limb25.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage25.png").texture);
                Limb25.transform.localPosition = new Vector3(-20.24796f, -2.689542f, 0f);
                Limb25.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb25.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb25.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb25.GetComponent<SpriteRenderer>().sortingOrder = 2;
                Limb25.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb25.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb25.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb25.GetComponent<LimbBehaviour>().Health = 50f;
                Limb25.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb25.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb25.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb25.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb25.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb25.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb25.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb25.GetComponent<LimbBehaviour>().HasBrain = false;
                GameObject Limb26 = GameObject.Instantiate<GameObject>(person.Limbs[13].gameObject, person.Limbs[13].transform.parent);
                var limbos26 = person.Limbs.ToList();
                limbos26.Add(Limb26.GetComponent<LimbBehaviour>());
                person.Limbs = limbos26.ToArray();
                GameObject.Destroy(Limb26.GetComponent<HingeJoint2D>());
                Limb26.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteSkin26.png");
                Limb26.GetComponent<SpriteRenderer>().material.SetTexture("_FleshTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteFlesh26.png").texture);
                Limb26.GetComponent<SpriteRenderer>().material.SetTexture("_BoneTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteBone26.png").texture);
                Limb26.GetComponent<SpriteRenderer>().material.SetTexture("_DamageTex", ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/CellMaxCreatureSprites/LimbSpriteDamage26.png").texture);
                Limb26.transform.localPosition = new Vector3(-24.50661f, -2.761111f, 0f);
                Limb26.transform.eulerAngles = new Vector3(0f, 0f, 0f) * Mathf.Sign(person.transform.localScale.x);
                Limb26.GetComponent<LimbBehaviour>().RoughClassification = LimbBehaviour.BodyPart.Legs;
                Limb26.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
                Limb26.GetComponent<SpriteRenderer>().sortingOrder = 1;
                Limb26.GetComponent<LimbBehaviour>().BaseStrength = 0.1f;
                Limb26.GetComponent<LimbBehaviour>().BloodMuscleStrengthRatio = 1f;
                Limb26.GetComponent<LimbBehaviour>().InitialHealth = 50f;
                Limb26.GetComponent<LimbBehaviour>().Health = 50f;
                Limb26.GetComponent<LimbBehaviour>().FakeUprightForce = 0.005f;
                Limb26.GetComponent<LimbBehaviour>().BreakingThreshold = 4f;
                Limb26.GetComponent<LimbBehaviour>().Vitality = 2f;
                Limb26.GetComponent<LimbBehaviour>().VitalParts = null;
                Limb26.GetComponent<CirculationBehaviour>().IsPump = false;
                Limb26.GetComponent<LimbBehaviour>().HasLungs = false;
                Limb26.GetComponent<LimbBehaviour>().IsLethalToBreak = false;
                Limb26.GetComponent<LimbBehaviour>().HasBrain = false;
                int limbCount = 26;

                Limb1.GetComponent<GoreStringBehaviour>().Other = Limb2.GetComponent<Rigidbody2D>();
                var Limb1Joint = Limb1.GetComponent<LimbBehaviour>().Joint;
                Limb1Joint.anchor = new Vector2(-0.5440655f, -1.718728f);
                Limb1Joint.autoConfigureConnectedAnchor = true; Limb1Joint.connectedBody = Limb2.GetComponent<Rigidbody2D>();
                var lim1 = Limb1Joint.limits;
                lim1.min = -80f;
                lim1.max = 15f;
                Limb1Joint.limits = lim1;
                Limb1.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb2.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb1.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb2.GetComponent<CirculationBehaviour>(),
                    };
                Limb1.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb2.GetComponent<LimbBehaviour>(),
                    };
                Limb1.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb2.GetComponent<SkinMaterialHandler>(),
                    };
                Limb1Joint.useLimits = true;
                Limb2.GetComponent<GoreStringBehaviour>().Other = Limb3.GetComponent<Rigidbody2D>();
                var Limb2Joint = Limb2.GetComponent<LimbBehaviour>().Joint;
                Limb2Joint.anchor = new Vector2(-0.2739373f, -2f);
                Limb2Joint.autoConfigureConnectedAnchor = true; Limb2Joint.connectedBody = Limb3.GetComponent<Rigidbody2D>();
                var lim2 = Limb2Joint.limits;
                lim2.min = -20f;
                lim2.max = 20f;
                Limb2Joint.limits = lim2;
                Limb2.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb1.GetComponent<ConnectedNodeBehaviour>(),
                    Limb3.GetComponent<ConnectedNodeBehaviour>(),
                    Limb11.GetComponent<ConnectedNodeBehaviour>(),
                    Limb13.GetComponent<ConnectedNodeBehaviour>(),
                    Limb15.GetComponent<ConnectedNodeBehaviour>(),
                    Limb16.GetComponent<ConnectedNodeBehaviour>()
                    };
                Limb2.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb1.GetComponent<CirculationBehaviour>(),
                    Limb3.GetComponent<CirculationBehaviour>(),
                    Limb11.GetComponent<CirculationBehaviour>(),
                    Limb13.GetComponent<CirculationBehaviour>(),
                    Limb15.GetComponent<CirculationBehaviour>(),
                    Limb16.GetComponent<CirculationBehaviour>()
                    };
                Limb2.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb1.GetComponent<LimbBehaviour>(),
                    Limb3.GetComponent<LimbBehaviour>(),
                    Limb11.GetComponent<LimbBehaviour>(),
                    Limb13.GetComponent<LimbBehaviour>(),
                    Limb15.GetComponent<LimbBehaviour>(),
                    Limb16.GetComponent<LimbBehaviour>()
                    };
                Limb2.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb1.GetComponent<SkinMaterialHandler>(),
                    Limb3.GetComponent<SkinMaterialHandler>(),
                    Limb11.GetComponent<SkinMaterialHandler>(),
                    Limb13.GetComponent<SkinMaterialHandler>(),
                    Limb15.GetComponent<SkinMaterialHandler>(),
                    Limb16.GetComponent<SkinMaterialHandler>()
                    };
                Limb2Joint.useLimits = true;
                Limb4.GetComponent<GoreStringBehaviour>().Other = Limb3.GetComponent<Rigidbody2D>();
                var Limb4Joint = Limb4.GetComponent<LimbBehaviour>().Joint;
                Limb4Joint.anchor = new Vector2(-0.1074856f, 1.056971f);
                Limb4Joint.autoConfigureConnectedAnchor = true; Limb4Joint.connectedBody = Limb3.GetComponent<Rigidbody2D>();
                var lim4 = Limb4Joint.limits;
                lim4.min = -15f;
                lim4.max = 20f;
                Limb4Joint.limits = lim4;
                Limb4.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb3.GetComponent<ConnectedNodeBehaviour>(),
                    Limb5.GetComponent<ConnectedNodeBehaviour>(),
                    Limb8.GetComponent<ConnectedNodeBehaviour>(),
                    Limb17.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb4.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb3.GetComponent<CirculationBehaviour>(),
                    Limb5.GetComponent<CirculationBehaviour>(),
                    Limb8.GetComponent<CirculationBehaviour>(),
                    Limb17.GetComponent<CirculationBehaviour>(),
                    };
                Limb4.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb3.GetComponent<LimbBehaviour>(),
                    Limb5.GetComponent<LimbBehaviour>(),
                    Limb8.GetComponent<LimbBehaviour>(),
                    Limb17.GetComponent<LimbBehaviour>(),
                    };
                Limb4.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb3.GetComponent<SkinMaterialHandler>(),
                    Limb5.GetComponent<SkinMaterialHandler>(),
                    Limb8.GetComponent<SkinMaterialHandler>(),
                    Limb17.GetComponent<SkinMaterialHandler>(),
                    };
                Limb4Joint.useLimits = true;
                Limb5.GetComponent<GoreStringBehaviour>().Other = Limb4.GetComponent<Rigidbody2D>();
                var Limb5Joint = Limb5.GetComponent<LimbBehaviour>().Joint;
                Limb5Joint.anchor = new Vector2(0.07807565f, 1.590693f);
                Limb5Joint.autoConfigureConnectedAnchor = true; Limb5Joint.connectedBody = Limb4.GetComponent<Rigidbody2D>();
                var lim5 = Limb5Joint.limits;
                lim5.min = -100f;
                lim5.max = 15f;
                Limb5Joint.limits = lim5;
                Limb5.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb4.GetComponent<ConnectedNodeBehaviour>(),
                    Limb6.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb5.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb4.GetComponent<CirculationBehaviour>(),
                    Limb6.GetComponent<CirculationBehaviour>(),
                    };
                Limb5.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb4.GetComponent<LimbBehaviour>(),
                    Limb6.GetComponent<LimbBehaviour>(),
                    };
                Limb5.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb4.GetComponent<SkinMaterialHandler>(),
                    Limb6.GetComponent<SkinMaterialHandler>(),
                    };
                Limb5Joint.useLimits = true;
                Limb6.GetComponent<GoreStringBehaviour>().Other = Limb5.GetComponent<Rigidbody2D>();
                var Limb6Joint = Limb6.GetComponent<LimbBehaviour>().Joint;
                Limb6Joint.anchor = new Vector2(0.0124805f, 1.523443f);
                Limb6Joint.autoConfigureConnectedAnchor = true; Limb6Joint.connectedBody = Limb5.GetComponent<Rigidbody2D>();
                var lim6 = Limb6Joint.limits;
                lim6.min = 0f;
                lim6.max = 120f;
                Limb6Joint.limits = lim6;
                Limb6.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb5.GetComponent<ConnectedNodeBehaviour>(),
                    Limb7.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb6.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb5.GetComponent<CirculationBehaviour>(),
                    Limb7.GetComponent<CirculationBehaviour>(),
                    };
                Limb6.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb5.GetComponent<LimbBehaviour>(),
                    Limb7.GetComponent<LimbBehaviour>(),
                    };
                Limb6.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb5.GetComponent<SkinMaterialHandler>(),
                    Limb7.GetComponent<SkinMaterialHandler>(),
                    };
                Limb6Joint.useLimits = true;
                Limb7.GetComponent<GoreStringBehaviour>().Other = Limb6.GetComponent<Rigidbody2D>();
                var Limb7Joint = Limb7.GetComponent<LimbBehaviour>().Joint;
                Limb7Joint.anchor = new Vector2(-1.038804f, 0.247879f);
                Limb7Joint.autoConfigureConnectedAnchor = true; Limb7Joint.connectedBody = Limb6.GetComponent<Rigidbody2D>();
                var lim7 = Limb7Joint.limits;
                lim7.min = -25f;
                lim7.max = 45f;
                Limb7Joint.limits = lim7;
                Limb7.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb6.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb7.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb6.GetComponent<CirculationBehaviour>(),
                    };
                Limb7.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb6.GetComponent<LimbBehaviour>(),
                    };
                Limb7.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb6.GetComponent<SkinMaterialHandler>(),
                    };
                Limb7Joint.useLimits = true;
                Limb8.GetComponent<GoreStringBehaviour>().Other = Limb4.GetComponent<Rigidbody2D>();
                var Limb8Joint = Limb8.GetComponent<LimbBehaviour>().Joint;
                Limb8Joint.anchor = new Vector2(0.08629489f, 1.596985f);
                Limb8Joint.autoConfigureConnectedAnchor = true; Limb8Joint.connectedBody = Limb4.GetComponent<Rigidbody2D>();
                var lim8 = Limb8Joint.limits;
                lim8.min = -100f;
                lim8.max = 15f;
                Limb8Joint.limits = lim8;
                Limb8.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb4.GetComponent<ConnectedNodeBehaviour>(),
                    Limb9.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb8.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb4.GetComponent<CirculationBehaviour>(),
                    Limb9.GetComponent<CirculationBehaviour>(),
                    };
                Limb8.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb4.GetComponent<LimbBehaviour>(),
                    Limb9.GetComponent<LimbBehaviour>(),
                    };
                Limb8.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb4.GetComponent<SkinMaterialHandler>(),
                    Limb9.GetComponent<SkinMaterialHandler>(),
                    };
                Limb8Joint.useLimits = true;
                Limb9.GetComponent<GoreStringBehaviour>().Other = Limb8.GetComponent<Rigidbody2D>();
                var Limb9Joint = Limb9.GetComponent<LimbBehaviour>().Joint;
                Limb9Joint.anchor = new Vector2(0.0001461506f, 1.522196f);
                Limb9Joint.autoConfigureConnectedAnchor = true; Limb9Joint.connectedBody = Limb8.GetComponent<Rigidbody2D>();
                var lim9 = Limb9Joint.limits;
                lim9.min = 0f;
                lim9.max = 120f;
                Limb9Joint.limits = lim9;
                Limb9.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb8.GetComponent<ConnectedNodeBehaviour>(),
                    Limb10.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb9.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb8.GetComponent<CirculationBehaviour>(),
                    Limb10.GetComponent<CirculationBehaviour>(),
                    };
                Limb9.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb8.GetComponent<LimbBehaviour>(),
                    Limb10.GetComponent<LimbBehaviour>(),
                    };
                Limb9.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb8.GetComponent<SkinMaterialHandler>(),
                    Limb10.GetComponent<SkinMaterialHandler>(),
                    };
                Limb9Joint.useLimits = true;
                Limb10.GetComponent<GoreStringBehaviour>().Other = Limb9.GetComponent<Rigidbody2D>();
                var Limb10Joint = Limb10.GetComponent<LimbBehaviour>().Joint;
                Limb10Joint.anchor = new Vector2(-1.039349f, 0.2215576f);
                Limb10Joint.autoConfigureConnectedAnchor = true; Limb10Joint.connectedBody = Limb9.GetComponent<Rigidbody2D>();
                var lim10 = Limb10Joint.limits;
                lim10.min = -25f;
                lim10.max = 45f;
                Limb10Joint.limits = lim10;
                Limb10.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb9.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb10.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb9.GetComponent<CirculationBehaviour>(),
                    };
                Limb10.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb9.GetComponent<LimbBehaviour>(),
                    };
                Limb10.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb9.GetComponent<SkinMaterialHandler>(),
                    };
                Limb10Joint.useLimits = true;
                Limb11.GetComponent<GoreStringBehaviour>().Other = Limb2.GetComponent<Rigidbody2D>();
                var Limb11Joint = Limb11.GetComponent<LimbBehaviour>().Joint;
                Limb11Joint.anchor = new Vector2(0.01340342f, 1.334106f);
                Limb11Joint.autoConfigureConnectedAnchor = true; Limb11Joint.connectedBody = Limb2.GetComponent<Rigidbody2D>();
                Limb11.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb2.GetComponent<ConnectedNodeBehaviour>(),
                    Limb12.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb11.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb2.GetComponent<CirculationBehaviour>(),
                    Limb12.GetComponent<CirculationBehaviour>(),
                    };
                Limb11.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb2.GetComponent<LimbBehaviour>(),
                    Limb12.GetComponent<LimbBehaviour>(),
                    };
                Limb11.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb2.GetComponent<SkinMaterialHandler>(),
                    Limb12.GetComponent<SkinMaterialHandler>(),
                    };
                Limb11Joint.useLimits = false;
                Limb12.GetComponent<GoreStringBehaviour>().Other = Limb11.GetComponent<Rigidbody2D>();
                var Limb12Joint = Limb12.GetComponent<LimbBehaviour>().Joint;
                Limb12Joint.anchor = new Vector2(-0.02618885f, 1.601221f);
                Limb12Joint.autoConfigureConnectedAnchor = true; Limb12Joint.connectedBody = Limb11.GetComponent<Rigidbody2D>();
                var lim12 = Limb12Joint.limits;
                lim12.min = -120f;
                lim12.max = 0f;
                Limb12Joint.limits = lim12;
                Limb12.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb11.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb12.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb11.GetComponent<CirculationBehaviour>(),
                    };
                Limb12.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb11.GetComponent<LimbBehaviour>(),
                    };
                Limb12.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb11.GetComponent<SkinMaterialHandler>(),
                    };
                Limb12Joint.useLimits = true;
                Limb13.GetComponent<GoreStringBehaviour>().Other = Limb2.GetComponent<Rigidbody2D>();
                var Limb13Joint = Limb13.GetComponent<LimbBehaviour>().Joint;
                Limb13Joint.anchor = new Vector2(-0.03817654f, 1.383843f);
                Limb13Joint.autoConfigureConnectedAnchor = true; Limb13Joint.connectedBody = Limb2.GetComponent<Rigidbody2D>();
                Limb13.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb2.GetComponent<ConnectedNodeBehaviour>(),
                    Limb14.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb13.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb2.GetComponent<CirculationBehaviour>(),
                    Limb14.GetComponent<CirculationBehaviour>(),
                    };
                Limb13.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb2.GetComponent<LimbBehaviour>(),
                    Limb14.GetComponent<LimbBehaviour>(),
                    };
                Limb13.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb2.GetComponent<SkinMaterialHandler>(),
                    Limb14.GetComponent<SkinMaterialHandler>(),
                    };
                Limb13Joint.useLimits = false;
                Limb14.GetComponent<GoreStringBehaviour>().Other = Limb13.GetComponent<Rigidbody2D>();
                var Limb14Joint = Limb14.GetComponent<LimbBehaviour>().Joint;
                Limb14Joint.anchor = new Vector2(-0.01339531f, 1.545663f);
                Limb14Joint.autoConfigureConnectedAnchor = true; Limb14Joint.connectedBody = Limb13.GetComponent<Rigidbody2D>();
                var lim14 = Limb14Joint.limits;
                lim14.min = -120f;
                lim14.max = 0f;
                Limb14Joint.limits = lim14;
                Limb14.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb13.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb14.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb13.GetComponent<CirculationBehaviour>(),
                    };
                Limb14.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb13.GetComponent<LimbBehaviour>(),
                    };
                Limb14.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb13.GetComponent<SkinMaterialHandler>(),
                    };
                Limb14Joint.useLimits = true;
                Limb15.GetComponent<LimbBehaviour>().Joint = Limb15.AddComponent<HingeJoint2D>();
                Limb15.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb15.GetComponent<GoreStringBehaviour>().Other = Limb2.GetComponent<Rigidbody2D>();
                var Limb15Joint = Limb15.GetComponent<LimbBehaviour>().Joint;
                Limb15Joint.anchor = new Vector2(1.804363f, 4.735215f);
                Limb15Joint.autoConfigureConnectedAnchor = true; Limb15Joint.connectedBody = Limb2.GetComponent<Rigidbody2D>();
                var lim15 = Limb15Joint.limits;
                lim15.min = 0f;
                lim15.max = 50f;
                Limb15Joint.limits = lim15;
                Limb15.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb2.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb15.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb2.GetComponent<CirculationBehaviour>(),
                    };
                Limb15.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb2.GetComponent<LimbBehaviour>(),
                    };
                Limb15.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb2.GetComponent<SkinMaterialHandler>(),
                    };
                Limb15Joint.useLimits = true;
                Limb16.GetComponent<LimbBehaviour>().Joint = Limb16.AddComponent<HingeJoint2D>();
                Limb16.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb16.GetComponent<GoreStringBehaviour>().Other = Limb2.GetComponent<Rigidbody2D>();
                var Limb16Joint = Limb16.GetComponent<LimbBehaviour>().Joint;
                Limb16Joint.anchor = new Vector2(1.790156f, 4.775765f);
                Limb16Joint.autoConfigureConnectedAnchor = true; Limb16Joint.connectedBody = Limb2.GetComponent<Rigidbody2D>();
                var lim16 = Limb16Joint.limits;
                lim16.min = 0f;
                lim16.max = 50f;
                Limb16Joint.limits = lim16;
                Limb16.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb2.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb16.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb2.GetComponent<CirculationBehaviour>(),
                    };
                Limb16.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb2.GetComponent<LimbBehaviour>(),
                    };
                Limb16.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb2.GetComponent<SkinMaterialHandler>(),
                    };
                Limb16Joint.useLimits = true;
                Limb17.GetComponent<LimbBehaviour>().Joint = Limb17.AddComponent<HingeJoint2D>();
                Limb17.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb17.GetComponent<GoreStringBehaviour>().Other = Limb4.GetComponent<Rigidbody2D>();
                var Limb17Joint = Limb17.GetComponent<LimbBehaviour>().Joint;
                Limb17Joint.anchor = new Vector2(1.159812f, 0.07709599f);
                Limb17Joint.autoConfigureConnectedAnchor = true; Limb17Joint.connectedBody = Limb4.GetComponent<Rigidbody2D>();
                var lim17 = Limb17Joint.limits;
                lim17.min = -40f;
                lim17.max = 40f;
                Limb17Joint.limits = lim17;
                Limb17.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb4.GetComponent<ConnectedNodeBehaviour>(),
                    Limb18.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb17.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb4.GetComponent<CirculationBehaviour>(),
                    Limb18.GetComponent<CirculationBehaviour>(),
                    };
                Limb17.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb4.GetComponent<LimbBehaviour>(),
                    Limb18.GetComponent<LimbBehaviour>(),
                    };
                Limb17.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb4.GetComponent<SkinMaterialHandler>(),
                    Limb18.GetComponent<SkinMaterialHandler>(),
                    };
                Limb17Joint.useLimits = true;
                Limb18.GetComponent<LimbBehaviour>().Joint = Limb18.AddComponent<HingeJoint2D>();
                Limb18.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb18.GetComponent<GoreStringBehaviour>().Other = Limb17.GetComponent<Rigidbody2D>();
                var Limb18Joint = Limb18.GetComponent<LimbBehaviour>().Joint;
                Limb18Joint.anchor = new Vector2(0.9088645f, 0.08887768f);
                Limb18Joint.autoConfigureConnectedAnchor = true; Limb18Joint.connectedBody = Limb17.GetComponent<Rigidbody2D>();
                var lim18 = Limb18Joint.limits;
                lim18.min = -50f;
                lim18.max = 50f;
                Limb18Joint.limits = lim18;
                Limb18.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb17.GetComponent<ConnectedNodeBehaviour>(),
                    Limb19.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb18.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb17.GetComponent<CirculationBehaviour>(),
                    Limb19.GetComponent<CirculationBehaviour>(),
                    };
                Limb18.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb17.GetComponent<LimbBehaviour>(),
                    Limb19.GetComponent<LimbBehaviour>(),
                    };
                Limb18.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb17.GetComponent<SkinMaterialHandler>(),
                    Limb19.GetComponent<SkinMaterialHandler>(),
                    };
                Limb18Joint.useLimits = true;
                Limb19.GetComponent<LimbBehaviour>().Joint = Limb19.AddComponent<HingeJoint2D>();
                Limb19.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb19.GetComponent<GoreStringBehaviour>().Other = Limb18.GetComponent<Rigidbody2D>();
                var Limb19Joint = Limb19.GetComponent<LimbBehaviour>().Joint;
                Limb19Joint.anchor = new Vector2(0.6794863f, 0.08279943f);
                Limb19Joint.autoConfigureConnectedAnchor = true; Limb19Joint.connectedBody = Limb18.GetComponent<Rigidbody2D>();
                var lim19 = Limb19Joint.limits;
                lim19.min = 50f;
                lim19.max = -50f;
                Limb19Joint.limits = lim19;
                Limb19.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb18.GetComponent<ConnectedNodeBehaviour>(),
                    Limb20.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb19.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb18.GetComponent<CirculationBehaviour>(),
                    Limb20.GetComponent<CirculationBehaviour>(),
                    };
                Limb19.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb18.GetComponent<LimbBehaviour>(),
                    Limb20.GetComponent<LimbBehaviour>(),
                    };
                Limb19.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb18.GetComponent<SkinMaterialHandler>(),
                    Limb20.GetComponent<SkinMaterialHandler>(),
                    };
                Limb19Joint.useLimits = true;
                Limb20.GetComponent<LimbBehaviour>().Joint = Limb20.AddComponent<HingeJoint2D>();
                Limb20.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb20.GetComponent<GoreStringBehaviour>().Other = Limb19.GetComponent<Rigidbody2D>();
                var Limb20Joint = Limb20.GetComponent<LimbBehaviour>().Joint;
                Limb20Joint.anchor = new Vector2(0.941905f, 0.07957792f);
                Limb20Joint.autoConfigureConnectedAnchor = true; Limb20Joint.connectedBody = Limb19.GetComponent<Rigidbody2D>();
                var lim20 = Limb20Joint.limits;
                lim20.min = -50f;
                lim20.max = 50f;
                Limb20Joint.limits = lim20;
                Limb20.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb19.GetComponent<ConnectedNodeBehaviour>(),
                    Limb21.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb20.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb19.GetComponent<CirculationBehaviour>(),
                    Limb21.GetComponent<CirculationBehaviour>(),
                    };
                Limb20.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb19.GetComponent<LimbBehaviour>(),
                    Limb21.GetComponent<LimbBehaviour>(),
                    };
                Limb20.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb19.GetComponent<SkinMaterialHandler>(),
                    Limb21.GetComponent<SkinMaterialHandler>(),
                    };
                Limb20Joint.useLimits = true;
                Limb21.GetComponent<LimbBehaviour>().Joint = Limb21.AddComponent<HingeJoint2D>();
                Limb21.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb21.GetComponent<GoreStringBehaviour>().Other = Limb20.GetComponent<Rigidbody2D>();
                var Limb21Joint = Limb21.GetComponent<LimbBehaviour>().Joint;
                Limb21Joint.anchor = new Vector2(0.4909573f, 0.06998682f);
                Limb21Joint.autoConfigureConnectedAnchor = true; Limb21Joint.connectedBody = Limb20.GetComponent<Rigidbody2D>();
                var lim21 = Limb21Joint.limits;
                lim21.min = -50f;
                lim21.max = 50f;
                Limb21Joint.limits = lim21;
                Limb21.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb20.GetComponent<ConnectedNodeBehaviour>(),
                    Limb22.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb21.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb20.GetComponent<CirculationBehaviour>(),
                    Limb22.GetComponent<CirculationBehaviour>(),
                    };
                Limb21.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb20.GetComponent<LimbBehaviour>(),
                    Limb22.GetComponent<LimbBehaviour>(),
                    };
                Limb21.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb20.GetComponent<SkinMaterialHandler>(),
                    Limb22.GetComponent<SkinMaterialHandler>(),
                    };
                Limb21Joint.useLimits = true;
                Limb22.GetComponent<LimbBehaviour>().Joint = Limb22.AddComponent<HingeJoint2D>();
                Limb22.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb22.GetComponent<GoreStringBehaviour>().Other = Limb21.GetComponent<Rigidbody2D>();
                var Limb22Joint = Limb22.GetComponent<LimbBehaviour>().Joint;
                Limb22Joint.anchor = new Vector2(0.4748859f, 0.0740931f);
                Limb22Joint.autoConfigureConnectedAnchor = true; Limb22Joint.connectedBody = Limb21.GetComponent<Rigidbody2D>();
                var lim22 = Limb22Joint.limits;
                lim22.min = -50f;
                lim22.max = 50f;
                Limb22Joint.limits = lim22;
                Limb22.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb21.GetComponent<ConnectedNodeBehaviour>(),
                    Limb23.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb22.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb21.GetComponent<CirculationBehaviour>(),
                    Limb23.GetComponent<CirculationBehaviour>(),
                    };
                Limb22.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb21.GetComponent<LimbBehaviour>(),
                    Limb23.GetComponent<LimbBehaviour>(),
                    };
                Limb22.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb21.GetComponent<SkinMaterialHandler>(),
                    Limb23.GetComponent<SkinMaterialHandler>(),
                    };
                Limb22Joint.useLimits = true;
                Limb23.GetComponent<LimbBehaviour>().Joint = Limb23.AddComponent<HingeJoint2D>();
                Limb23.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb23.GetComponent<GoreStringBehaviour>().Other = Limb22.GetComponent<Rigidbody2D>();
                var Limb23Joint = Limb23.GetComponent<LimbBehaviour>().Joint;
                Limb23Joint.anchor = new Vector2(0.4848232f, 0.05567312f);
                Limb23Joint.autoConfigureConnectedAnchor = true; Limb23Joint.connectedBody = Limb22.GetComponent<Rigidbody2D>();
                var lim23 = Limb23Joint.limits;
                lim23.min = -50f;
                lim23.max = 50f;
                Limb23Joint.limits = lim23;
                Limb23.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb22.GetComponent<ConnectedNodeBehaviour>(),
                    Limb24.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb23.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb22.GetComponent<CirculationBehaviour>(),
                    Limb24.GetComponent<CirculationBehaviour>(),
                    };
                Limb23.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb22.GetComponent<LimbBehaviour>(),
                    Limb24.GetComponent<LimbBehaviour>(),
                    };
                Limb23.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb22.GetComponent<SkinMaterialHandler>(),
                    Limb24.GetComponent<SkinMaterialHandler>(),
                    };
                Limb23Joint.useLimits = true;
                Limb24.GetComponent<LimbBehaviour>().Joint = Limb24.AddComponent<HingeJoint2D>();
                Limb24.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb24.GetComponent<GoreStringBehaviour>().Other = Limb23.GetComponent<Rigidbody2D>();
                var Limb24Joint = Limb24.GetComponent<LimbBehaviour>().Joint;
                Limb24Joint.anchor = new Vector2(0.591383f, 0.0509572f);
                Limb24Joint.autoConfigureConnectedAnchor = true; Limb24Joint.connectedBody = Limb23.GetComponent<Rigidbody2D>();
                var lim24 = Limb24Joint.limits;
                lim24.min = -50f;
                lim24.max = 50f;
                Limb24Joint.limits = lim24;
                Limb24.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb23.GetComponent<ConnectedNodeBehaviour>(),
                    Limb25.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb24.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb23.GetComponent<CirculationBehaviour>(),
                    Limb25.GetComponent<CirculationBehaviour>(),
                    };
                Limb24.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb23.GetComponent<LimbBehaviour>(),
                    Limb25.GetComponent<LimbBehaviour>(),
                    };
                Limb24.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb23.GetComponent<SkinMaterialHandler>(),
                    Limb25.GetComponent<SkinMaterialHandler>(),
                    };
                Limb24Joint.useLimits = true;
                Limb25.GetComponent<LimbBehaviour>().Joint = Limb25.AddComponent<HingeJoint2D>();
                Limb25.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb25.GetComponent<GoreStringBehaviour>().Other = Limb24.GetComponent<Rigidbody2D>();
                var Limb25Joint = Limb25.GetComponent<LimbBehaviour>().Joint;
                Limb25Joint.anchor = new Vector2(0.7018604f, 0.04134059f);
                Limb25Joint.autoConfigureConnectedAnchor = true; Limb25Joint.connectedBody = Limb24.GetComponent<Rigidbody2D>();
                var lim25 = Limb25Joint.limits;
                lim25.min = -50f;
                lim25.max = 50f;
                Limb25Joint.limits = lim25;
                Limb25.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb24.GetComponent<ConnectedNodeBehaviour>(),
                    Limb26.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb25.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb24.GetComponent<CirculationBehaviour>(),
                    Limb26.GetComponent<CirculationBehaviour>(),
                    };
                Limb25.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb24.GetComponent<LimbBehaviour>(),
                    Limb26.GetComponent<LimbBehaviour>(),
                    };
                Limb25.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb24.GetComponent<SkinMaterialHandler>(),
                    Limb26.GetComponent<SkinMaterialHandler>(),
                    };
                Limb25Joint.useLimits = true;
                Limb26.GetComponent<LimbBehaviour>().Joint = Limb26.AddComponent<HingeJoint2D>();
                Limb26.GetComponent<LimbBehaviour>().HasJoint = true;
                Limb26.GetComponent<GoreStringBehaviour>().Other = Limb25.GetComponent<Rigidbody2D>();
                var Limb26Joint = Limb26.GetComponent<LimbBehaviour>().Joint;
                Limb26Joint.anchor = new Vector2(3.013832f, 0.1003876f);
                Limb26Joint.autoConfigureConnectedAnchor = true; Limb26Joint.connectedBody = Limb25.GetComponent<Rigidbody2D>();
                var lim26 = Limb26Joint.limits;
                lim26.min = -50f;
                lim26.max = 50f;
                Limb26Joint.limits = lim26;
                Limb26.GetComponent<ConnectedNodeBehaviour>().Connections = new ConnectedNodeBehaviour[]{
                    Limb25.GetComponent<ConnectedNodeBehaviour>(),
                    };
                Limb26.GetComponent<CirculationBehaviour>().PushesTo = new CirculationBehaviour[]{
                    Limb25.GetComponent<CirculationBehaviour>(),
                    };
                Limb26.GetComponent<LimbBehaviour>().ConnectedLimbs = new List<LimbBehaviour>(){
                    Limb25.GetComponent<LimbBehaviour>(),
                    };
                Limb26.GetComponent<SkinMaterialHandler>().adjacentLimbs = new SkinMaterialHandler[]{
                    Limb25.GetComponent<SkinMaterialHandler>(),
                    };
                Limb26Joint.useLimits = true;

                for (int i = limbCount; i < person.Limbs.Length; i++)
                {
                    person.Limbs[i].PhysicalBehaviour.Disintegrate();
                }

                foreach (var Limb in person.Limbs)
                {
                    Limb.gameObject.FixColliders();
                }
                var nocol = Instance.AddComponent<NoCollide>();
                nocol.NoCollideSetA = Instance.GetComponentsInChildren<Collider2D>();
                nocol.NoCollideSetB = Instance.GetComponentsInChildren<Collider2D>();

                person.LinkedPoses[PoseState.Rest].Angles = new List<RagdollPose.LimbPose>();
                foreach (var limb in person.Limbs)
                {
                    if (limb != null && !limb.PhysicalBehaviour.isDisintegrated && limb.HasJoint)
                    {
                        person.LinkedPoses[PoseState.Rest].Angles.Add(new RagdollPose.LimbPose(limb, limb.Joint.jointAngle + (limb.transform.eulerAngles.z - limb.Joint.connectedBody.transform.eulerAngles.z) * Mathf.Sign(person.transform.localScale.x)));
                    }
                }
                person.LinkedPoses[PoseState.Rest].ConstructDictionary();

                person.LinkedPoses[PoseState.Protective].Angles = new List<RagdollPose.LimbPose>();
                foreach (var limb in person.Limbs)
                {
                    if (limb != null && !limb.PhysicalBehaviour.isDisintegrated && limb.HasJoint)
                    {
                        person.LinkedPoses[PoseState.Protective].Angles.Add(new RagdollPose.LimbPose(limb, limb.Joint.jointAngle + (limb.transform.eulerAngles.z - limb.Joint.connectedBody.transform.eulerAngles.z) * Mathf.Sign(person.transform.localScale.x)));
                    }
                }
                person.LinkedPoses[PoseState.Protective].ConstructDictionary();

                foreach (var item in person.Limbs)
                {
                    if (item.RoughClassification == LimbBehaviour.BodyPart.Head)
                    {
                        item.NodeBehaviour.IsRoot = true;
                        item.NodeBehaviour.Value = 1000000;
                    }
                    else
                    {
                        item.NodeBehaviour.IsRoot = false;
                        item.NodeBehaviour.Value = 100000;
                    }
                }

                foreach (var item in person.Limbs)
                {
                    if (item.RoughClassification == LimbBehaviour.BodyPart.Head)
                    {
                        item.NodeBehaviour.RootPropagation();
                    }
                }

                person.transform.root.transform.localScale = new Vector3(1.1f * Mathf.Sign(Instance.transform.localScale.x), 1.1f, person.transform.root.transform.localScale.z);

                Limb1.gameObject.AddComponent<HumanController>().person = person;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                Limb1.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                Instance.gameObject.name = "Cell_Max";

                for (int ti = 14; ti < 26 && ti < person.Limbs.Length; ti++)
                {
                    var tailGo = person.Limbs[ti].gameObject;
                    tailGo.name = "CellMaxTail_" + (ti - 13);
                    var tailPunch = tailGo.GetComponent<PunchPower>();
                    if (tailPunch != null) GameObject.Destroy(tailPunch);
                    var tailBarrage = tailGo.GetComponent<Barrage>();
                    if (tailBarrage != null) GameObject.Destroy(tailBarrage);
                    var tailTrigger = tailGo.GetComponent<UseEventTrigger>();
                    if (tailTrigger != null) GameObject.Destroy(tailTrigger);
                    if (tailGo.GetComponent<CellMaxTailSegment>() == null)
                        tailGo.AddComponent<CellMaxTailSegment>();
                }

                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = 8000000f;
                kiSystem.baseRegen = 80000f;
                kiSystem.requiredColor = new Color(0f, 1f, 0f);
                Instance.AddComponent<BlastBehaviour>();
                Instance.AddComponent<BeamBehaviour>().EffectID = "Energy Wave";
                var cellMaxShield = Instance.AddComponent<KiShield>();
                cellMaxShield.OverrideShieldScale = true;
                cellMaxShield.OverrideShieldScaleValue = new Vector3(1.9f, 1.9f, 1.9f);
                var pw = PowerSetup.BeginPowers(Limb1.gameObject, person, Instance);
                var r = pw.Refs;
                Action brolyBeamOff = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; };
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.Grant("EnergyWave", new PowerCatalog.Overrides { BlockWhen = () => r.BeamStillBlocked, OnEnable = brolyBeamOff, BeamType = "ArmBeam" });
                pw.GrantMany(PowerSetup.BeamBlock(r, brolyBeamOff), "GiganticFullBlast", "MaxCannon");
                if (r.Ball != null) pw.Grant("GiganticCrusher");
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);
                Limb2.gameObject.AddComponent<Flight>();
                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[10].gameObject.AddComponent<PunchPower>();
                person.Limbs[12].gameObject.AddComponent<PunchPower>();
                var auraHandler = Instance.AddComponent<AuraHandler>();
                auraHandler.person = person;
                auraHandler.CurrentAuraID = "Broly";
                Instance.AddComponent<CellMaxIgnoresTailWeight>();

                for (int si = 0; si < 13 && si < person.Limbs.Length; si++)
                {
                    var spotGlow = person.Limbs[si].gameObject.AddComponent<CellMaxSpotGlow>();
                    spotGlow.LimbSkinIndex = si + 1;
                    spotGlow.IsTail = false;
                    spotGlow.DynamicSpriteRefresh = si == 0;
                    spotGlow.ScanEyeLaserMarkers = si == 0;
                    spotGlow.Initialize();
                }

                for (int ti = 14; ti < 26 && ti < person.Limbs.Length; ti++)
                {
                    var spotGlow = person.Limbs[ti].gameObject.AddComponent<CellMaxSpotGlow>();
                    spotGlow.LimbSkinIndex = ti + 1;
                    spotGlow.IsTail = true;
                    spotGlow.Initialize();
                }

                PowerSetup.GrantDisasterRay(person, Instance, pw.Powers);
                PowerSetup.GrantEyeLaser(person, Instance, pw.Powers);

                Limb1.gameObject.AddComponent<CellMaxScream>().Sprites = new Sprite[] { ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/Scream/1.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/Scream/2.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/Scream/3.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/Scream/4.png"), ModAPI.LoadSprite("Assets/Sprites/Characters/Cell Max/Scream/5.png") };
                Limb1.gameObject.GetComponent<CellMaxScream>().Scream = ModAPI.LoadSound("Assets/SFX/CellMax/Scream.mp3");

                var headKiCharge = Limb1.GetComponent<KiChargeHeadHoldInput>();
                if (headKiCharge != null)
                    UnityEngine.Object.Destroy(headKiCharge);
                if (Limb2.GetComponent<KiChargeHeadHoldInput>() == null)
                    Limb2.AddComponent<KiChargeHeadHoldInput>();

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 5000000000000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 5000000000000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 5000000000000000000000f;
                    limb.BaseStrength *= 2.5f;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                    //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                }
            },
            "DBSSH",
            UniqueTag + ZFighterTag + "012");

            //DBSSH Whis
            ModAPIPlus.CreateHuman(
                "(DBSSH) Whis",
                "The Angel attendant and martial arts teacher of Beerus, the God of Destruction. Whis moves faster than instant transmission and trains Goku and Vegeta on Beerus's planet.",
                "DBSSH/Whis",
                (Instance) =>
                {
                    Instance.gameObject.name = "Whis";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    uarmf.GetComponent<SpriteRenderer>().sortingOrder += 4;
                    larmf.GetComponent<SpriteRenderer>().sortingOrder += 4;

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Whis/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(upper);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = lower.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Whis/Thingy.png");
                    capeSprite.sortingLayerName = "Foreground";
                    capeSprite.sortingOrder = 5;

                    lower.gameObject.AddComponent<Flight>();

                    string WaistFolder = "Assets/Sprites/Characters/Whis/Waist/";
                    Sprite[] WaistSprites = new Sprite[18]
                    {
                        ModAPI.LoadSprite(WaistFolder + "dr.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr1.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr2.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr3.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr4.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr5.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr6.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr7.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr8.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr9.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr10.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr11.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr12.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr13.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr14.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr15.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr16.png"),
                        ModAPI.LoadSprite(WaistFolder + "dr17.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.015f, 0.1f), "Foreground", 1, WaistSprites, -7f, 12f, -5f, 10f, 0.0018f, 0.001f, "Waist");

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 2.26f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 50000000000f;
                    kiSystem.baseRegen = 1250000000f;

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    Instance.AddComponent<InstantTransmission>().Active = true;

                    var whisAura = Instance.AddComponent<FlameOutlineAuraHandler>();
                    whisAura.AuraType = typeof(WhisFlameAura);
                    whisAura.InnerAuraType = typeof(WhisInnerFlameAura);
                    whisAura.AccentAuraType = null;
                    whisAura.Setup();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !whisAura.AuraOn ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        whisAura.ToggleAura();
                    }));

                    string Folder = "Assets/Sprites/Characters/Whis/";
                    List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(BaseSkin);

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health = float.MaxValue;
                        limb.InitialHealth = float.MaxValue;
                        limb.BreakingThreshold = float.MaxValue;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }

                    CeceUtils.DelayedInvoke(0.15f, () =>
                    {
                        var multipliers = Instance.GetComponent<MultipliersAndStuffs>();
                        if (multipliers != null)
                            multipliers.FormMultiplier = 10000000000f;
                    });
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "015");

            //DBSSH Beerus
            ModAPIPlus.CreateHuman(
                "(DBSSH) Beerus",
                "The God of Destruction of Universe 7.",
                "DBSSH/Beerus",
                (Instance) =>
                {
                    Instance.gameObject.name = "Beerus";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Beerus/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    uarmf.GetComponent<SpriteRenderer>().sortingOrder += 4;
                    larmf.GetComponent<SpriteRenderer>().sortingOrder += 4;

                    ulegf.GetComponent<SpriteRenderer>().sortingOrder += 3;
                    llegf.GetComponent<SpriteRenderer>().sortingOrder += 3;
                    footf.GetComponent<SpriteRenderer>().sortingOrder += 3;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(lower);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = lower.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Beerus/Thingy.png");
                    capeSprite.sortingLayerName = "Default";
                    capeSprite.sortingOrder = 2;

                    string TailFolder = "Assets/Sprites/Characters/Beerus/tail/";
                    Sprite[] TailSprites = new Sprite[18]
                    {
                        ModAPI.LoadSprite(TailFolder + "tail_00.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_01.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_02.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_03.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_04.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_05.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_06.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_07.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_08.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_09.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_10.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_11.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_12.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_13.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_14.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_15.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_16.png"),
                        ModAPI.LoadSprite(TailFolder + "tail_17.png")
                    };

                    PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(-0.115f, 0f), "Default", 1, TailSprites, 80f, 90f, -9f, 12f, 0.002f, 0.001f, "Tail", true, false, false, 0.6f, false);

                    string Folder = "Assets/Sprites/Characters/Beerus/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 25000000f;

                    Instance.AddComponent<BlastBehaviour>();
                    var beerusBeam = Instance.AddComponent<BeamBehaviour>();
                    beerusBeam.EffectID = "Energy Wave";
                    beerusBeam.Character = "Beerus";
                    var beerusBall = Instance.AddComponent<BallBehaviour>();
                    beerusBall.EffectID = "Beerus Ball";
                    beerusBall.Character = "Beerus";
                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    PowerSetup.RegisterBeerusCombatPowers(pw.Powers, Instance);
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    var beerusAura = Instance.AddComponent<FlameOutlineAuraHandler>();
                    beerusAura.AuraType = typeof(PurpleFlameAura);
                    beerusAura.InnerAuraType = typeof(InnerBlueFlameAura);
                    beerusAura.Setup();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !beerusAura.AuraOn ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        beerusAura.ToggleAura();
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health = float.MaxValue / 100f;
                        limb.InitialHealth = float.MaxValue / 100f;
                        limb.BreakingThreshold = float.MaxValue / 100f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }

                    CeceUtils.DelayedInvoke(0.15f, () =>
                    {
                        var multipliers = Instance.GetComponent<MultipliersAndStuffs>();
                        if (multipliers != null)
                            multipliers.FormMultiplier = 5000000000f;
                    });
                },
                "DBSSH",
                UniqueTag + ZFighterTag + "016");

            //DBSSH Bulma
            ModAPIPlus.CreateHuman(
            "(DBSSH) Bulma",
            "You might be the Prince of Saiyans, but I'm the Queen of Attitude!",
            "DBSSH/Bulma",
            (Instance) =>
            {
                Instance.gameObject.name = "Bulma";

                var person = Instance.GetComponent<PersonBehaviour>();
                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Bulma/SuperHero/Hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.65f);

                string Folder = "Assets/Sprites/Characters/Bulma/SuperHero/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "upper.png"), ModAPI.LoadSprite(Folder + "middle.png"), ModAPI.LoadSprite(Folder + "lower.png"), ModAPI.LoadSprite(Folder + "uleg.png"), ModAPI.LoadSprite(Folder + "lleg.png"), ModAPI.LoadSprite(Folder + "foot.png"), ModAPI.LoadSprite(Folder + "uleg.png"), ModAPI.LoadSprite(Folder + "lleg.png"), ModAPI.LoadSprite(Folder + "foot.png"), ModAPI.LoadSprite(Folder + "uarm.png"), ModAPI.LoadSprite(Folder + "larm.png"), ModAPI.LoadSprite(Folder + "uarm.png"), ModAPI.LoadSprite(Folder + "larm.png") };

                person.ChangeSkin(BaseSkin);

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 50f;
                    limb.InitialHealth *= 50f;
                    limb.BreakingThreshold *= 1.5f;
                }
            },
            "DBSSH",
            UniqueTag + HumanTag + "006");

            //DBSSH Cheelai
            ModAPIPlus.CreateHuman(
            "(DBSSH) Cheelai",
            "",
            "DBSSH/Cheelai",
            (Instance) =>
            {
                Instance.gameObject.name = "Cheelai";

                var person = Instance.GetComponent<PersonBehaviour>();
                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Cheelai/Hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.52f);

                string Folder = "Assets/Sprites/Characters/Cheelai/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                person.ChangeSkin(BaseSkin);

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 8000f;
                    limb.InitialHealth *= 8000f;
                    limb.BreakingThreshold *= 10f;

                    limb.SkinMaterialHandler.intensityMultiplier = 0.5f;
                    limb.GForcePassoutThreshold *= 10f;
                }
            },
            "DBSSH",
            UniqueTag + HumanTag + "007");

            //DBSSH Lemo
            ModAPIPlus.CreateHuman(
            "(DBSSH) Lemo",
            "",
            "DBSSH/Lemo",
            (Instance) =>
            {
                Instance.gameObject.name = "Lemo";

                var person = Instance.GetComponent<PersonBehaviour>();
                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                var footb = Instance.transform.Find("BackLeg").Find("Foot");
                var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                var larmb = Instance.transform.Find("BackArm").Find("LowerArm");

                var hatObject = new GameObject("Hat");
                hatObject.transform.SetParent(head);
                hatObject.transform.localPosition = new Vector3(0f, 0f);
                hatObject.transform.rotation = head.rotation;
                hatObject.transform.localScale = new Vector3(1f, 1f);
                var hatSprite = hatObject.AddComponent<SpriteRenderer>();
                hatSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Lemo/Hat.png");
                hatSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hatSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.54f);

                string Folder = "Assets/Sprites/Characters/Lemo/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head2.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                person.ChangeSkin(BaseSkin, false);

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 40000f;
                    limb.InitialHealth *= 40000f;
                    limb.BreakingThreshold *= 40f;

                    limb.SkinMaterialHandler.intensityMultiplier = 0.36f;
                    limb.GForcePassoutThreshold *= 10f;
                }
            },
            "DBSSH",
            UniqueTag + HumanTag + "008");

            //DBSSH Dendê
            ModAPIPlus.CreateHuman(
            "(DBSSH) Dendê",
            "The young Namekian healer who tends the Lookout and supports Earth's defenders from the sidelines.",
            "DBSSH/Dendê",
            (Instance) =>
            {
                Instance.gameObject.name = "Dende";

                var person = Instance.GetComponent<PersonBehaviour>();
                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");

                lower.gameObject.AddComponent<Flight>();

                var headThingyObject = new GameObject("HeadThingy");
                headThingyObject.transform.SetParent(head);
                headThingyObject.transform.localPosition = new Vector3(0f, 0f);
                headThingyObject.transform.rotation = head.rotation;
                headThingyObject.transform.localScale = new Vector3(1f, 1f);
                var headThingySprite = headThingyObject.AddComponent<SpriteRenderer>();
                headThingySprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Dendê/head thingy.png");
                headThingySprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                headThingySprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                var scarfObject = new GameObject("Scarf");
                scarfObject.transform.SetParent(upper);
                scarfObject.transform.localPosition = new Vector3(0f, 0f);
                scarfObject.transform.rotation = upper.rotation;
                scarfObject.transform.localScale = new Vector3(1f, 1f);
                var scarfSprite = scarfObject.AddComponent<SpriteRenderer>();
                scarfSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Dendê/Scarf.png");
                scarfSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                scarfSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                var waistObject = new GameObject("Waist");
                waistObject.transform.SetParent(lower);
                waistObject.transform.localPosition = new Vector3(0f, 0f);
                waistObject.transform.rotation = lower.rotation;
                waistObject.transform.localScale = new Vector3(1f, 1f);
                var waistSprite = waistObject.AddComponent<SpriteRenderer>();
                waistSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Dendê/Waist.png");

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.53f);

                string Folder = "Assets/Sprites/Characters/Dendê/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };
                person.ChangeSkin(BaseSkin);

                waistSprite.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                waistSprite.sortingOrder = Mathf.Max(ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder, llegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder) + 1;

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = 50000f;
                kiSystem.baseRegen = 500f;

                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                pw.Finish();

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 2000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 2000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 2000000000f;

                    limb.GForcePassoutThreshold *= 10f;
                }
            },
            "DBSSH",
            UniqueTag + HumanTag + "009");

            //DBSSH Gamma 1
            ModAPIPlus.CreateHuman(
            "(DBSSH) Gamma 1",
            "One of the Red Ribbon Army's new android supersoldiers from Dragon Ball Super: Super Hero.",
            "DBSSH/Gamma 1",
            (Instance) =>
            {
                Instance.gameObject.name = "Gamma 1";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var person = Instance.GetComponent<PersonBehaviour>();

                lower.gameObject.AddComponent<Flight>();

                var waistObject = new GameObject("Waist");
                waistObject.transform.SetParent(lower);
                waistObject.transform.localPosition = new Vector3(0f, 0f);
                waistObject.transform.rotation = lower.rotation;
                waistObject.transform.localScale = new Vector3(1f, 1f);
                var waistSprite = waistObject.AddComponent<SpriteRenderer>();
                waistSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gamma 1/Waist.png");

                string Folder = "Assets/Sprites/Characters/Gamma 1/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm2.png"), ModAPI.LoadSprite(Folder + "LArm.png") };
                person.ChangeSkin(BaseSkin);

                waistSprite.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                waistSprite.sortingOrder = Mathf.Max(ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder, llegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder) + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.70f);
                ApplyGammaLayout(person);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = 750000f;
                kiSystem.baseRegen = 8000f;

                Instance.AddComponent<BlastBehaviour>();
                Instance.AddComponent<BeamBehaviour>().EffectID = "Energy Wave";
                Instance.AddComponent<KiShield>();
                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.BeamStillBlocked,
                    OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.Grant("EnergyWave", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.BeamStillBlocked,
                    OnEnable = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);
                Instance.GetComponent<MultipliersAndStuffs>().FormMultiplier = 100000f;

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                Instance.AddComponent<AuraHandler>().person = person;

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 4800000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 4800000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 4800000000000000f;

                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                }
            },
            "DBSSH",
            UniqueTag + VillanTag + "002");

            //DBSSH Gamma 2
            ModAPIPlus.CreateHuman(
            "(DBSSH) Gamma 2",
            "The stronger of the Red Ribbon Army's Gamma android duo from Dragon Ball Super: Super Hero.",
            "DBSSH/Gamma 2",
            (Instance) =>
            {
                Instance.gameObject.name = "Gamma 2";

                var head = Instance.transform.Find("Head");
                var upper = Instance.transform.Find("Body").Find("UpperBody");
                var middle = Instance.transform.Find("Body").Find("MiddleBody");
                var lower = Instance.transform.Find("Body").Find("LowerBody");
                var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                var person = Instance.GetComponent<PersonBehaviour>();

                lower.gameObject.AddComponent<Flight>();

                var waistObject = new GameObject("Waist");
                waistObject.transform.SetParent(lower);
                waistObject.transform.localPosition = new Vector3(0f, 0f);
                waistObject.transform.rotation = lower.rotation;
                waistObject.transform.localScale = new Vector3(1f, 1f);
                var waistSprite = waistObject.AddComponent<SpriteRenderer>();
                waistSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gamma 2/Waist.png");

                string Folder = "Assets/Sprites/Characters/Gamma 2/";
                List<Sprite> BaseSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm2.png"), ModAPI.LoadSprite(Folder + "LArm.png") };
                person.ChangeSkin(BaseSkin);

                waistSprite.sortingLayerName = ulegf.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                waistSprite.sortingOrder = Mathf.Max(ulegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder, llegf.gameObject.GetComponent<SpriteRenderer>().sortingOrder) + 1;

                person.transform.root.transform.SetHeight(person, middle.gameObject, 1.70f);
                ApplyGammaLayout(person);

                head.gameObject.AddComponent<HumanController>().person = person;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = 750000f;
                kiSystem.baseRegen = 8000f;

                Instance.AddComponent<BlastBehaviour>();
                Instance.AddComponent<BeamBehaviour>().EffectID = "Energy Wave";
                Instance.AddComponent<KiShield>();
                var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                var r = pw.Refs;
                pw.Grant("KiBlast", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.BeamStillBlocked,
                    OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                pw.Grant("EnergyWave", new PowerCatalog.Overrides
                {
                    BlockWhen = () => r.BeamStillBlocked,
                    OnEnable = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                });
                if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
                {
                    OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
                });
                pw.Finish();
                PowerSetup.CompletePhysicalPowers(Instance, person);
                Instance.GetComponent<MultipliersAndStuffs>().FormMultiplier = 100000f;

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                Instance.AddComponent<AuraHandler>().person = person;

                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () =>
                {
                    Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                }));

                foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health *= 4800000000000000f * DBPU.HealthMultiplier;
                    limb.InitialHealth *= 4800000000000000f * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= 4800000000000000f;

                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                }
            },
            "DBSSH",
            UniqueTag + VillanTag + "003");



            ModAPIPlus.CreateObject(
                "Rod",
                "Potara Earring",
                "Earrings worn by Supreme Kais. Fuse two compatible fighters when each wears one and they meet.",
                "Potara Earring",
                (Instance) =>
                {
                    Instance.GetComponent<SpriteRenderer>().sprite = PotaraFusionHelpers.CachedPotaraSprite;
                    Instance.gameObject.FixColliders();
                    Instance.transform.localScale = new Vector3(PotaraFusionHelpers.PotaraWorldVisualScale, PotaraFusionHelpers.PotaraWorldVisualScale, 1f);
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().RecalculateMassBasedOnSize();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().Properties = ModAPI.FindPhysicalProperties("Soft");
                    Instance.gameObject.AddComponent<PotaraSpawnableBehaviour>();
                },
                "Items",
                UniqueTag + ItemsTag + "004");

            //Roshi's Staff
            ModAPIPlus.CreateObject(
                "Rod",
                "Roshi's Staff",
                "A simple but useful weapon, used by Master Roshi.",
                "Roshi Staff",
                (Instance) =>
                {
                    Instance.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Items/Roshi Staff.png");
                    Instance.gameObject.FixColliders();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().TrueInitialMass *= 5f;
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().RecalculateMassBasedOnSize();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().Properties = ModAPI.FindPhysicalProperties("Wood");

                    Instance.GetComponent<PhysicalBehaviour>().HoldingPositions = new[]
                    {
                        new Vector3(0.05f, -0.32f)
                        };
                },
                "Items",
                UniqueTag + CosmeticTag + "001");

            //Senzu Bean Bag
            ModAPIPlus.CreateObject(
                "Rod",
                "Senzu Bean Bag",
                "A bag full of Senzu Beans",
                "Senzu Bean Bag",
                (Instance) =>
                {
                    Instance.gameObject.name = "Senzu Bean Bag";

                    Instance.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Items/SenzuBag_Closed.png");
                    Instance.gameObject.FixColliders();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().RecalculateMassBasedOnSize();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().Properties = ModAPI.FindPhysicalProperties("Soft");
                    Instance.gameObject.AddComponent<SenzuBeanBagBehaviour>();

                    Sprite OpenSprite = ModAPI.LoadSprite("Assets/Sprites/Items/SenzuBag_Open.png");
                    Sprite ClosedSprite = ModAPI.LoadSprite("Assets/Sprites/Items/SenzuBag_Closed.png");

                    Instance.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleBag", (Func<string>)(() => !Instance.gameObject.GetComponent<SenzuBeanBagBehaviour>().Open ? "Open" : "Close"), "Toggle Bag",
                    () =>
                    {
                        if (Instance.gameObject.GetComponent<SenzuBeanBagBehaviour>().Open == true)
                        {
                            Instance.gameObject.GetComponent<SenzuBeanBagBehaviour>().Open = false;
                            Instance.GetComponent<SpriteRenderer>().sprite = ClosedSprite;
                        }

                        else
                        {
                            Instance.gameObject.GetComponent<SenzuBeanBagBehaviour>().Open = true;
                            Instance.GetComponent<SpriteRenderer>().sprite = OpenSprite;
                        }
                    }));
                },
                "Items",
                UniqueTag + ItemsTag + "001");

            //Senzu Bean
            ModAPIPlus.CreateObject(
                "Rod",
                "Senzu Bean",
                "A magic Bean that has the ability to restore someone's state to it's peak.",
                "Senzu Bean",
                (Instance) =>
                {
                    Instance.gameObject.name = "Senzu Bean";

                    Instance.GetComponent<SpriteRenderer>().sprite = ModAPI.LoadSprite("Assets/Sprites/Items/Senzu Bean.png");
                    Instance.gameObject.FixColliders();
                    Instance.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().RecalculateMassBasedOnSize();
                    Instance.gameObject.GetComponent<PhysicalBehaviour>().Properties = ModAPI.FindPhysicalProperties("Soft");
                    Instance.gameObject.AddComponent<SenzuBeanBehaviour>();
                },
                "Items",
                UniqueTag + ItemsTag + "002");

            //Earth Dragon Balls
            ModAPIPlus.CreateObject(
                "Bowling Ball",
                "Earth Dragon Balls",
                "Gather all seven and activate one: Shenron rises and grants three wishes.",
                "DragonBalls",
                (Instance) =>
                {
                    UnityEngine.GameObject.Destroy(Instance.GetComponent<RandomSpriteBehaviour>());
                    var StarsList = new List<int>();
                    int Stars;

                    foreach (var DB in UnityEngine.GameObject.FindObjectsOfType<EarthDB>())
                    {
                        StarsList.Add(DB.Stars);
                    }

                    if (StarsList.Count >= 7)
                    {
                        ModAPI.Notify("You've already found all the Dragon Balls!");
                        UnityEngine.GameObject.Destroy(Instance.gameObject);
                    }

                    else
                    {
                        Instance.AddComponent<EarthDB>();
                        Stars = UnityEngine.Random.Range(1, 8);

                        while (StarsList.Contains(Stars))
                        {
                            Stars = UnityEngine.Random.Range(1, 8);
                        }

                        Instance.GetComponent<EarthDB>().Stars = Stars;
                        SpriteRenderer spriteRenderer = Instance.GetComponent<SpriteRenderer>();
                        spriteRenderer.sprite = ModAPI.LoadSprite("Assets/Sprites/Items/DB_" + Stars.ToString() + ".png");
                        Instance.transform.localScale = new Vector2(0.5f, 0.5f);
                        Instance.gameObject.name = Stars.ToString() + (Stars > 1 ? " Stars" : " Star") + " Dragon Ball";
                    }

                    foreach (var ObjectColliders in Instance.GetComponents<Collider2D>())
                    {
                        GameObject.Destroy(ObjectColliders);
                    }

                    // Singularity: activating any ball checks the set and summons Shenron.
                    Instance.gameObject.AddComponent<UseEventTrigger>().Action = () => ShenronModule.UseBall(Instance);

                    Instance.AddComponent<CircleCollider2D>();
                    Instance.GetComponent<PhysicalBehaviour>().colliders = Instance.GetComponentsInChildren<Collider2D>();
                },
                "Items",
                UniqueTag + ItemsTag + "003");

            // Singularity modules: Zen-O Button, Tournament of Power arena, ...
            Singularity.RegisterSpawnables();



            //Gogeta
            ModAPI.Register(
               new Modification()
               {
                   OriginalItem = ModAPI.FindSpawnable("Human"),
                   NameOverride = "Gogeta",
                   NameToOrderByOverride = "",
                   DescriptionOverride = "",
                   CategoryOverride = ModAPI.FindCategory(""),
                   ThumbnailOverride = ModAPI.LoadSprite(""),
                   AfterSpawn = (Instance) =>
                   {
                       Instance.gameObject.name = "Gogeta";

                       var head = Instance.transform.Find("Head");
                       var upper = Instance.transform.Find("Body").Find("UpperBody");
                       var middle = Instance.transform.Find("Body").Find("MiddleBody");
                       var lower = Instance.transform.Find("Body").Find("LowerBody");
                       var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                       var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                       var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                       var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                       var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                       var footb = Instance.transform.Find("BackLeg").Find("Foot");
                       var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                       var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                       var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                       var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                       var person = Instance.GetComponent<PersonBehaviour>();

                       var hairObject = new GameObject("Hair");
                       hairObject.transform.SetParent(head);
                       hairObject.transform.localPosition = new Vector3(0f, 0f);
                       hairObject.transform.rotation = head.rotation;
                       hairObject.transform.localScale = new Vector3(1f, 1f);
                       var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                       hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair.png");
                       hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                       hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                       string BeltFolder = "Assets/Sprites/Characters/Gogeta/Thingy1/";
                       Sprite[] BeltSprites1 = new Sprite[7]
                       {
                            ModAPI.LoadSprite(BeltFolder + "stb.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb6.png")
                       };

                       PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                       BeltFolder = "Assets/Sprites/Characters/Gogeta/Thingy2/";
                       Sprite[] BeltSprites2 = new Sprite[6]
                       {
                            ModAPI.LoadSprite(BeltFolder + "stf.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf5.png")
                       };

                       PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                       string Folder = "Assets/Sprites/Characters/Gogeta/Metamoro/";
                       List<Sprite> MetamoroSkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                       person.ChangeSkin(MetamoroSkin);

                       person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                       head.gameObject.AddComponent<HumanController>().person = person;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                       var kiSystem = Instance.AddComponent<KiSystem>();
                       var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                       kiSystem.maxKi = 100000000f;
                       kiSystem.baseRegen = 1000000f;

                       var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                       {
                           PowerMultiplier = 1f,
                           Size = 1.75f,
                           HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair.png"),
                           HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/Base.png"),
                           KiDrainRate = 0f,
                           AuraID = "Base",

                           Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/Base.png"),
                           Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/Base.png"),
                           Name = "Base",
                           Description = "",

                           ID = "Base",
                           SkinDamageAmount = 0
                       };
                       characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                       var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
                       {
                           PowerMultiplier = 50f,
                           Size = 1.75f,
                           HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssj1.png"),
                           HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJ1.png"),
                           KiDrainRate = 400000f,
                           AuraID = "SSJ1",

                           Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssj1.png"),
                           Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssj1.png"),
                           Name = "Super Saiyan 1",
                           Description = "\n\nThis Form is the least expensive of all in terms of Ki Drain.",

                           ID = "SSJ1",
                           SkinDamageAmount = 1
                       };
                       characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

                       var ssjgTransformation = new CharacterTransformations.Transformations.TransformationStats()
                       {
                           PowerMultiplier = 10000f,
                           Size = 1.75f,
                           HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssjg.png"),
                           HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJG.png"),
                           KiDrainRate = 500000f,
                           AuraID = "SSJGod",

                           Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssjg.png"),
                           Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssjg.png"),
                           Name = "Super Saiyan God",
                           Description = "\n\nThis form's Ki Drain isn't much, so it's a very useful transformations, and besides that, it provides the user an amazing Regeneration ability.",

                           ID = "SSJGod",
                           SkinDamageAmount = 0
                       };
                       characterTransformations.transformations.AddTransformation("SSJGod", ssjgTransformation);

                       var ssjbTransformation = new CharacterTransformations.Transformations.TransformationStats()
                       {
                           PowerMultiplier = 500000f,
                           Size = 1.75f,
                           HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/hair ssjb.png"),
                           HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/HeadSkins/SSJB.png"),
                           KiDrainRate = 1200000f,
                           AuraID = "SSJBlue",

                           Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Icons/ssjb.png"),
                           Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Gogeta/Illustrations/ssjb.png"),
                           Name = "Super Saiyan Blue",
                           Description = "\n\nThe Ki Drain it's medium, but it's power is worth it to use for raw strength.",

                           ID = "SSJBlue",
                           SkinDamageAmount = 2
                       };
                       characterTransformations.transformations.AddTransformation("SSJBlue", ssjbTransformation);

                       CeceUtils.DelayedInvoke(0.1f, () =>
                       {
                           characterTransformations.ApplyTransformation(person, "Base");
                       });

                       head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                       head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                       Instance.AddComponent<Kaioken>();

                       Instance.AddComponent<BlastBehaviour>();



                       Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                       Instance.GetComponent<BeamBehaviour>().Character = "Gogeta";
                       Instance.gameObject.AddComponent<StardustFall>();
                       Instance.gameObject.AddComponent<MeteorExplosion>();
                       Instance.gameObject.AddComponent<GodPunisher>();
                       Instance.gameObject.AddComponent<StardustBreaker>();
                       var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                       var r = pw.Refs;
                       Action gogetaOff = () => { r.Blast.Active = false; PowerSetup.DisableGogetaSpecials(r); };
                       var gogetaBeam = PowerSetup.BeamBlock(r, gogetaOff);
                       pw.Grant("KiBlast", new PowerCatalog.Overrides
                       {
                           BlockWhen = () => r.BeamStillBlocked,
                           OnEnable = () => { r.Beam.Active = false; PowerSetup.DisableGogetaSpecials(r); }
                       });
                       pw.GrantMany(gogetaBeam, "EnergyWave", "Kamehameha", "FullForceKamehameha");
                       if (r.StardustFall != null) pw.Grant("StardustFall");
                       if (r.MeteorExplosion != null) pw.Grant("MeteorExplosion");
                       if (r.GodPunisher != null) pw.Grant("GodPunisher");
                       if (r.StardustBreaker != null) pw.Grant("StardustBreaker");
                       pw.Finish();
                       PowerSetup.CompletePhysicalPowers(Instance, person);

                       lower.gameObject.AddComponent<Flight>();

                       Instance.AddComponent<InstantTransmission>();

                       person.Limbs[1].gameObject.AddComponent<Barrage>();
                       person.Limbs[11].gameObject.AddComponent<PunchPower>();
                       person.Limbs[13].gameObject.AddComponent<PunchPower>();

                       upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                       () =>
                       {
                           Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                       }));

                       foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                       {
                           limb.Health *= 50000000000000000000000f * DBPU.HealthMultiplier;
                           limb.InitialHealth *= 50000000000000000000000f * DBPU.HealthMultiplier;
                           limb.BreakingThreshold *= 50000000000000000000000f;

                           //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                           limb.GForcePassoutThreshold *= 100f;
                           limb.ImpactPainMultiplier = 0f;
                           limb.ImpactDamageMultiplier /= 10000f;
                           limb.ShotDamageMultiplier = 0f;

                           limb.FreezingTemperature = -300f;
                           limb.DiscomfortingHeatTemperature = 2000f;
                           limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                           //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                       }
                   }
               });

            //Tiencha
            ModAPI.Register(
               new Modification()
               {
                   OriginalItem = ModAPI.FindSpawnable("Human"),
                   NameOverride = "Tiencha",
                   NameToOrderByOverride = "",
                   DescriptionOverride = "",
                   CategoryOverride = ModAPI.FindCategory(""),
                   ThumbnailOverride = ModAPI.LoadSprite(""),
                   AfterSpawn = (Instance) =>
                   {
                       Instance.gameObject.name = "Tiencha";

                       var head = Instance.transform.Find("Head");
                       var upper = Instance.transform.Find("Body").Find("UpperBody");
                       var middle = Instance.transform.Find("Body").Find("MiddleBody");
                       var lower = Instance.transform.Find("Body").Find("LowerBody");
                       var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                       var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                       var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                       var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                       var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                       var footb = Instance.transform.Find("BackLeg").Find("Foot");
                       var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                       var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                       var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                       var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                       var person = Instance.GetComponent<PersonBehaviour>();

                       var hairObject = new GameObject("Hair");
                       hairObject.transform.SetParent(head);
                       hairObject.transform.localPosition = new Vector3(0f, 0f);
                       hairObject.transform.rotation = head.rotation;
                       hairObject.transform.localScale = new Vector3(1f, 1f);
                       var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                       hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Tiencha/Hair.png");
                       hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                       hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                       string BeltFolder = "Assets/Sprites/Characters/Tiencha/Thingy1/";
                       Sprite[] BeltSprites1 = new Sprite[7]
                       {
                            ModAPI.LoadSprite(BeltFolder + "stb.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb5.png"),
                            ModAPI.LoadSprite(BeltFolder + "stb6.png")
                       };

                       PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1285f, 0.1286f), "Foreground", 1, BeltSprites1, -15f, 32f, -17f, 25f, 0.0018f, 0.001f, "Belt");

                       BeltFolder = "Assets/Sprites/Characters/Tiencha/Thingy2/";
                       Sprite[] BeltSprites2 = new Sprite[6]
                       {
                            ModAPI.LoadSprite(BeltFolder + "stf.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf1.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf2.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf3.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf4.png"),
                            ModAPI.LoadSprite(BeltFolder + "stf5.png")
                       };

                       PHY.Cro.CreatePhysicalCloth(person.Limbs.First((LimbBehaviour limb) => limb.name == "LowerBody").transform, new Vector2(0.1f, 0.1286f), "Foreground", 1, BeltSprites2, -32f, 15f, -25f, 17f, 0.0018f, 0.001f, "Belt");

                       string Folder = "Assets/Sprites/Characters/Tiencha/";
                       List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                       person.ChangeSkin(Skin);

                       person.transform.root.transform.SetHeight(person, middle.gameObject, 1.85f);

                       head.gameObject.AddComponent<HumanController>().person = person;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                       head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                       var kiSystem = Instance.AddComponent<KiSystem>();

                       kiSystem.maxKi = 100000000f;
                       kiSystem.baseRegen = 1000000f;

                       Instance.AddComponent<BlastBehaviour>();
                       Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                       Instance.GetComponent<BeamBehaviour>().Character = "Tiencha";
                       var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                       var r = pw.Refs;
                       pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                       pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                       pw.Finish();
                       PowerSetup.CompletePhysicalPowers(Instance, person);

                       lower.gameObject.AddComponent<Flight>();

                       person.Limbs[1].gameObject.AddComponent<Barrage>();
                       person.Limbs[11].gameObject.AddComponent<PunchPower>();
                       person.Limbs[13].gameObject.AddComponent<PunchPower>();

                       upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                       () =>
                       {
                           Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                       }));

                       foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                       {
                           limb.Health *= 40000000000000000f * DBPU.HealthMultiplier;
                           limb.InitialHealth *= 40000000000000000f * DBPU.HealthMultiplier;
                           limb.BreakingThreshold *= 400000000000000000000000f;

                           //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                           limb.GForcePassoutThreshold *= 100f;
                           limb.ImpactPainMultiplier = 0f;
                           limb.ImpactDamageMultiplier /= 10000f;
                           limb.ShotDamageMultiplier = 0f;

                           limb.FreezingTemperature = -300f;
                           limb.DiscomfortingHeatTemperature = 2000f;
                           limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                           //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                       }
                   }
               });

            //Teen Gotenks
            ModAPI.Register(
               new Modification()
               {
                   OriginalItem = ModAPI.FindSpawnable("Human"),
                   NameOverride = "Teen Gotenks",
                   NameToOrderByOverride = "",
                   DescriptionOverride = "The Metamoran fusion of Teen Goten and Teen Trunks.",
                   CategoryOverride = ModAPI.FindCategory(""),
                   ThumbnailOverride = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Gotenks/Icons/Base.png"),
                   AfterSpawn = (Instance) =>
                   {
                       ApplyTeenGotenksSpawn(Instance);
                   }
               });

            ModAPIPlus.CreateHuman(
                "(DBSSH) Teen Gotenks",
                "The Metamoran fusion of Teen Goten and Teen Trunks.",
                "DBSSH/Teen Gotenks",
                ApplyTeenGotenksSpawn,
                "DBSSH",
                UniqueTag + ZFighterTag + "009");




            //Drip Goku
            ModAPIPlus.CreateHuman(
                SecretTag + "Drip Goku",
                "",
                "Secrets/Drip Goku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Drip Goku";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    lower.gameObject.AddComponent<Flight>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Drip/";
                    List<Sprite> Drip = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    Folder = "Assets/Sprites/Characters/Goku/Drip MUI/";
                    List<Sprite> DripMUI = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Drip);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;
                    head.gameObject.AddTrueDelete();

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 10000000f;

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Drip/Head.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        RequiredSkin = Drip,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Base Drip.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Base Drip.png"),
                        Name = "Base",
                        Description = "Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip Drip",

                        ID = "Base"
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var dripTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1000000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ui.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/UI.png"),
                        KiDrainRate = 5000000f,
                        AuraID = "UI",

                        RequiredSkin = Drip,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Dripstinct.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Dripstinct.png"),
                        Name = "Ultra Dripstinct",
                        Description = "The Ultimate Drip",

                        ID = "Dripstinct"
                    };
                    characterTransformations.transformations.AddTransformation("Dripstinct", dripTransformation);

                    var dripMUITransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 100000000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair mui.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/HeadSkins/MUI.png"),
                        KiDrainRate = 20000000f,
                        AuraID = "MUI",

                        RequiredSkin = DripMUI,

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/MUI Dripstinct.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/MUI Dripstinct.png"),
                        Name = "Mastered Ultra Dripstinct",
                        Description = "Drip at it's Peak, no one is Drippier!",

                        ID = "MasteredDripstinct"
                    };
                    characterTransformations.transformations.AddTransformation("MasteredDripstinct", dripMUITransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<BlastBehaviour>().Damage = 2500000000000000000f;



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goku";
                    Instance.GetComponent<BeamBehaviour>().Damage = 50000000000000000000f;


                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>().Damage = 10000000000000000000f;
                    person.Limbs[13].gameObject.AddComponent<PunchPower>().Damage = 10000000000000000000f;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in person.Limbs)
                    {
                        limb.Health = float.PositiveInfinity;
                        limb.InitialHealth = float.MaxValue;
                        limb.BreakingThreshold = float.PositiveInfinity;

                        limb.SkinMaterialHandler.intensityMultiplier = 0f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier = 0f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.PhysicalBehaviour.Disintegratable = false;
                        limb.PhysicalBehaviour.Deletable = false;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = float.PositiveInfinity;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "Dripstinct");

            //Nerfed Goku
            ModAPIPlus.CreateHuman(
                SecretTag + "Goku with No Limbs, Blindfolded, Earplugged, Sick and Tired",
                "Even in this state, Naruto has no chance against Goku!!",
                "Secrets/Nerfed Goku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Nerfed Goku for short";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Nerfed/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Nerfed/";
                    List<Sprite> TurtleGISkin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), null, null, null, null, null, null, null, null, null, null, null };

                    person.ChangeSkin(TurtleGISkin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();

                    kiSystem.maxKi = 100000f;
                    kiSystem.baseRegen = 2500f;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in person.Limbs)
                    {
                        limb.Health *= 5000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 5000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 5000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }

                    foreach (var limb in person.Limbs)
                    {
                        if (limb.name.Contains("Lower") || limb.name.Contains("Leg") || limb.name.Contains("Foot") || limb.name.Contains("Arm"))
                        {
                            limb.gameObject.SetActive(false);
                            //GameObject.Destroy(limb.gameObject);
                        }
                    }
                },
                null,
                "Very Nerfed");

            //Goku with Saitama's Clothes
            ModAPIPlus.CreateHuman(
                SecretTag + "Goku with Saitama's Clothes",
                "I stole Saitama's Clothes!",
                "Secrets/Saitama Goku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Goku with Saitama's Clothes";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(upper);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = upper.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Saitama Skin/cape.png");
                    capeSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    capeSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Saitama Skin/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goku";


                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 2500000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 2500000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 2500000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "Now he can One Punch");

            //Bobku
            ModAPIPlus.CreateHuman(
                SecretTag + "Bobku",
                "BOB!!",
                "Secrets/Bobku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Bobku";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Bob/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Bob/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Dictionary<string, List<Sprite>> Skins = new Dictionary<string, List<Sprite>> { { "Damage0", Skin } };

                    var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 1f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Bob/Hair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Bob/Head.png"),
                        KiDrainRate = 0f,
                        AuraID = "Base",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Base Bob.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Base Bob.png"),
                        Name = "Base",
                        Description = "Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob Bob",

                        ID = "Base"
                    };
                    characterTransformations.transformations.AddTransformation("Base", baseTransformation);

                    var ssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
                    {
                        PowerMultiplier = 500000f,
                        Size = 1.75f,
                        HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Bob/SSJHair.png"),
                        HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Bob/SSJHead.png"),
                        KiDrainRate = 4000f,
                        AuraID = "SSJBlue",

                        Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/SSJBob.png"),
                        Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/SSJBob.png"),
                        Name = "Super Saiyan Bob",
                        Description = "BOO!!",

                        ID = "SSJBlue"
                    };
                    characterTransformations.transformations.AddTransformation("SSJBlue", ssjTransformation);

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        characterTransformations.ApplyTransformation(person, "Base");
                    });

                    head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
                    head.gameObject.GetComponent<TransformationContextMenu>().person = person;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Bob";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goku";


                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 80000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 80000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 80000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "BOB");

            //Funny Goku
            ModAPIPlus.CreateHuman(
                SecretTag + "Funny Goku",
                "HAHAHAHAHAHAAHAHAHAHAHAAHAHAHAHAAAHAHAHA",
                "Secrets/Funny Goku",
                (Instance) =>
                {
                    Instance.gameObject.name = "Funny Goku";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Funny/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Funny/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;
                    head.gameObject.AddTrueDelete();

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();
                    Instance.GetComponent<BlastBehaviour>().Damage = 100000000000000000f;



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Damage = 2500000000000000000f;


                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>().Damage = 1500000000000000000f;
                    person.Limbs[13].gameObject.AddComponent<PunchPower>().Damage = 1500000000000000000f;

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    pw.Grant("KiBlast");
                    pw.Grant("Kamehameha", new PowerCatalog.Overrides { EffectId = "Bob", Name = "Bob", GokuStyleBeam = true });
                    pw.Finish();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health = float.PositiveInfinity;
                        limb.InitialHealth = float.MaxValue;
                        limb.BreakingThreshold = float.PositiveInfinity;

                        limb.SkinMaterialHandler.intensityMultiplier = 0f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier = 0f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.PhysicalBehaviour.Disintegratable = false;
                        limb.PhysicalBehaviour.Deletable = false;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = float.PositiveInfinity;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "So Funny");

            //G.O.D. Yamcha
            ModAPIPlus.CreateHuman(
                SecretTag + "G.O.D. Yamcha",
                "It's Yamcha as the God of Destruction!",
                "Secrets/GOD Yamcha",
                (Instance) =>
                {
                    Instance.gameObject.name = "G.O.D. Yamcha";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Yamcha/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(lower);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = lower.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Yamcha/GOD/Thingy.png");
                    capeSprite.sortingLayerName = lower.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    capeSprite.sortingOrder = lower.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Yamcha/GOD/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.83f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();

                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 25000000f;

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Yamcha";


                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 500000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 500000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 500000000000000000000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "TRUE POWER!!");

            //Full Power Tenshinhan
            ModAPIPlus.CreateHuman(
                SecretTag + "Full Power Tenshinhan",
                "I- HATE EVERYTHING ABOUT YOU- WHY- DO I- LOVE YOU!",
                "Secrets/FP Tien",
                (Instance) =>
                {
                    Instance.gameObject.name = "Full Power Tenshinhan";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var capeObject = new GameObject("Cape");
                    capeObject.transform.SetParent(upper);
                    capeObject.transform.localPosition = new Vector3(0f, 0f);
                    capeObject.transform.rotation = upper.rotation;
                    capeObject.transform.localScale = new Vector3(1f, 1f);
                    var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                    capeSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Tenshinhan/Full Power/Cape.png");
                    capeSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    capeSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Tenshinhan/Full Power/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.83f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 25000000f;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";



                    Instance.AddComponent<SolarFlare>().Character = "Tien";


                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 12000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 12000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 12000000000000000000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "TRUE POWER OF TIEN");

            //Black Goku Black
            ModAPIPlus.CreateHuman(
                SecretTag + "Black Goku Black",
                "Goku Black who is actually Black!",
                "Secrets/Black Goku Black",
                (Instance) =>
                {
                    Instance.gameObject.name = "Black Goku Black";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Black Goku Black/hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Goku/Black Goku Black/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    var characterTransformations = Instance.AddComponent<CharacterTransformations>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goku";


                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 5000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "DON'T HOLD YOUR FIRE, THIS MAN IS BLACK!");

            //Sigma!!
            ModAPIPlus.CreateHuman(
                SecretTag + "Sigma!!",
                "The Sigmiest of them all!!",
                "Secrets/Sigma",
                (Instance) =>
                {
                    Instance.gameObject.name = "Sigma!";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    var hairObject = new GameObject("Hair");
                    hairObject.transform.SetParent(head);
                    hairObject.transform.localPosition = new Vector3(0f, 0f);
                    hairObject.transform.rotation = head.rotation;
                    hairObject.transform.localScale = new Vector3(1f, 1f);
                    var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Sigma/Hair.png");
                    hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

                    string Folder = "Assets/Sprites/Characters/Sigma/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.78f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();

                    kiSystem.maxKi = 1000000000f;
                    kiSystem.baseRegen = 25000000f;

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Sigma";

                    lower.gameObject.AddComponent<Flight>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    Action sigmaBeamOff = () => r.Beam.Active = false;
                    pw.Grant("KiBlast", new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = sigmaBeamOff
                    });
                    pw.GrantMany(new PowerCatalog.Overrides
                    {
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => r.Blast.Active = false
                    }, "EnergyWave");
                    pw.Grant("Kamehameha", new PowerCatalog.Overrides
                    {
                        Name = "Sigmahameha",
                        BlockWhen = () => r.BeamStillBlocked,
                        OnEnable = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; }
                    });
                    pw.Finish();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in Instance.GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health *= 50000000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 50000000000000000000000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 50000000000000000000000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "SIGMAAAA");

            //Ultra Brickstinct
            ModAPIPlus.CreateHuman(
                SecretTag + "Ultra Brickstinct",
                "Only the true OGs know about this!",
                "Secrets/Ultra Brickstinct",
                (Instance) =>
                {
                    Instance.gameObject.name = "Ultra Brickstinct";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var ulegb = Instance.transform.Find("BackLeg").Find("UpperLeg");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var llegb = Instance.transform.Find("BackLeg").Find("LowerLeg");
                    var footf = Instance.transform.Find("FrontLeg").Find("FootFront");
                    var footb = Instance.transform.Find("BackLeg").Find("Foot");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var uarmb = Instance.transform.Find("BackArm").Find("UpperArm");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var larmb = Instance.transform.Find("BackArm").Find("LowerArm");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    head.transform.CreateAccessory("Brick Hair", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/Hair.png"));
                    upper.transform.CreateAccessory("Bricks", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/UpperBrick.png"));
                    middle.transform.CreateAccessory("Bricks", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/MiddleBrick.png"));
                    lower.transform.CreateAccessory("Bricks", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/LowerBrick.png"));
                    uarmf.transform.CreateAccessory("Bricks", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/UArmBrick.png"));
                    uarmb.transform.CreateAccessory("Bricks", ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Ultra Brickstinct/UArmBrick.png"));

                    string Folder = "Assets/Sprites/Characters/Goku/Ultra Brickstinct/";
                    List<Sprite> Skin = new List<Sprite> { ModAPI.LoadSprite(Folder + "Head.png"), ModAPI.LoadSprite(Folder + "Upper.png"), ModAPI.LoadSprite(Folder + "Middle.png"), ModAPI.LoadSprite(Folder + "Lower.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "ULeg.png"), ModAPI.LoadSprite(Folder + "LLeg.png"), ModAPI.LoadSprite(Folder + "Foot.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png"), ModAPI.LoadSprite(Folder + "UArm.png"), ModAPI.LoadSprite(Folder + "LArm.png") };

                    person.ChangeSkin(Skin);

                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToKnockout = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToPain = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToOxigenLoss = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToInternalBleeding = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToNumbness = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToFire = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToAcid = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToElectricity = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToStabbing = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBullets = true;
                    head.gameObject.GetComponent<HumanController>().Features.ImmuneToBrainDamage = true;

                    CeceUtils.DelayedInvoke(0.1f, () =>
                    {
                        foreach (var limb in person.Limbs)
                        {
                            limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = ModAPI.FindPhysicalProperties("Brick");
                            limb.gameObject.GetComponent<Rigidbody2D>().mass /= 280f;
                            limb.PhysicalBehaviour.TrueInitialMass /= 280f;
                            limb.BaseStrength = limb.PhysicalBehaviour.TrueInitialMass * 120f;
                            limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                        }
                    });

                    var kiSystem = Instance.AddComponent<KiSystem>();

                    kiSystem.maxKi = 1000000f;
                    kiSystem.baseRegen = 10000f;

                    Instance.AddComponent<Kaioken>();

                    Instance.AddComponent<BlastBehaviour>();



                    Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
                    Instance.GetComponent<BeamBehaviour>().Character = "Goku";


                    lower.gameObject.AddComponent<Flight>();

                    Instance.AddComponent<InstantTransmission>();

                    person.Limbs[1].gameObject.AddComponent<Barrage>();
                    person.Limbs[11].gameObject.AddComponent<PunchPower>();
                    person.Limbs[13].gameObject.AddComponent<PunchPower>();

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in person.Limbs)
                    {
                        limb.Health *= 80000000000000000000000f * DBPU.HealthMultiplier;
                        limb.InitialHealth *= 80000000000000000000000f * DBPU.HealthMultiplier;
                        limb.BreakingThreshold *= 80000000000000000000000f;

                        //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier /= 10000f;
                        limb.ShotDamageMultiplier = 0f;

                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = 2000f;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                        //limb.gameObject.GetComponent<PhysicalBehaviour>().Properties = PhysProperties.GodlyLimbProperties;
                    }
                },
                null,
                "MADE BY LR");

            //Jayraiya
            ModAPIPlus.CreateHuman(
                SecretTag + "Jayraiya",
                "",
                "Secrets/Jayraiya",
                (Instance) =>
                {
                    Instance.gameObject.name = "Jayraiya";

                    var head = Instance.transform.Find("Head");
                    var upper = Instance.transform.Find("Body").Find("UpperBody");
                    var middle = Instance.transform.Find("Body").Find("MiddleBody");
                    var lower = Instance.transform.Find("Body").Find("LowerBody");
                    var ulegf = Instance.transform.Find("FrontLeg").Find("UpperLegFront");
                    var llegf = Instance.transform.Find("FrontLeg").Find("LowerLegFront");
                    var uarmf = Instance.transform.Find("FrontArm").Find("UpperArmFront");
                    var larmf = Instance.transform.Find("FrontArm").Find("LowerArmFront");
                    var person = Instance.GetComponent<PersonBehaviour>();

                    uarmf.GetComponent<SpriteRenderer>().sortingOrder += 4;
                    larmf.GetComponent<SpriteRenderer>().sortingOrder += 4;

                    string folder = "Assets/Sprites/Characters/Jayraiya/Skin/";
                    List<Sprite> skin = new List<Sprite>
                    {
                        ModAPI.LoadSprite(folder + "Head.png"),
                        ModAPI.LoadSprite(folder + "Upper.png"),
                        ModAPI.LoadSprite(folder + "Middle.png"),
                        ModAPI.LoadSprite(folder + "Lower.png"),
                        ModAPI.LoadSprite(folder + "ULeg.png"),
                        ModAPI.LoadSprite(folder + "LLeg.png"),
                        ModAPI.LoadSprite(folder + "Foot.png"),
                        ModAPI.LoadSprite(folder + "ULeg.png"),
                        ModAPI.LoadSprite(folder + "LLeg.png"),
                        ModAPI.LoadSprite(folder + "Foot.png"),
                        ModAPI.LoadSprite(folder + "UArm.png"),
                        ModAPI.LoadSprite(folder + "LArm.png"),
                        ModAPI.LoadSprite(folder + "UArm.png"),
                        ModAPI.LoadSprite(folder + "LArm.png")
                    };

                    person.SetBodyTextures(
                        ModAPI.LoadTexture("Assets/Sprites/Characters/Jayraiya/Kskin.png"),
                        ModAPI.LoadTexture("Assets/Sprites/Characters/Jayraiya/flesh.png"),
                        ModAPI.LoadTexture("Assets/Sprites/Characters/Jayraiya/bone.png"),
                        1f);
                    person.ChangeSkin(skin);
                    lower.transform.CreateAccessory(
                        "Skirt",
                        ModAPI.LoadSprite("Assets/Sprites/Characters/Jayraiya/Skirt.png"),
                        sortingLayerName: ulegf.GetComponent<SpriteRenderer>().sortingLayerName,
                        sortingOrder: Mathf.Max(ulegf.GetComponent<SpriteRenderer>().sortingOrder, llegf.GetComponent<SpriteRenderer>().sortingOrder) + 1);
                    person.transform.root.transform.SetHeight(person, middle.gameObject, 1.76f);

                    head.gameObject.AddComponent<HumanController>().person = person;
                    var hc = head.gameObject.GetComponent<HumanController>().Features;
                    hc.ImmuneToKnockout = true;
                    hc.ImmuneToPain = true;
                    hc.ImmuneToOxigenLoss = true;
                    hc.ImmuneToBleeding = true;
                    hc.ImmuneToInternalBleeding = true;
                    hc.ImmuneToNumbness = true;
                    hc.ImmuneToFire = true;
                    hc.ImmuneToAcid = true;
                    hc.ImmuneToElectricity = true;
                    hc.ImmuneToStabbing = true;
                    hc.ImmuneToBullets = true;
                    hc.ImmuneToBrainDamage = true;

                    var kiSystem = Instance.AddComponent<KiSystem>();
                    kiSystem.maxKi = float.PositiveInfinity;
                    kiSystem.baseRegen = 1e18f;
                    kiSystem.currentKi = kiSystem.maxKi;
                    Instance.AddComponent<AuraHandler>().person = person;

                    var multipliers = Instance.GetComponent<MultipliersAndStuffs>();
                    if (multipliers == null) multipliers = Instance.AddComponent<MultipliersAndStuffs>();
                    multipliers.FormMultiplier = 1e30f;
                    multipliers.BoostMultiplier = 1e30f;
                    multipliers.KaiokenMultiplier = 1e30f;
                    multipliers.RageMultiplier = 1e30f;
                    multipliers.MajinMultiplier = 1e30f;
                    multipliers.ExponentialMultiplier = 1e30f;
                    multipliers.OtherMultiplier = 1e30f;
                    multipliers.KiOutput = 1e30f;

                    var blast = Instance.AddComponent<BlastBehaviour>();
                    blast.Damage = 1e30f;
                    var beam = Instance.AddComponent<BeamBehaviour>();
                    beam.EffectID = "Kamehameha";
                    beam.Character = "Human";
                    beam.Damage = 1e30f;

                    var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
                    var r = pw.Refs;
                    pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
                    pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
                    pw.Finish();
                    PowerSetup.CompletePhysicalPowers(Instance, person);

                    lower.gameObject.AddComponent<Flight>();
                    Instance.AddComponent<InstantTransmission>();

                    var barrage = person.Limbs[1].gameObject.AddComponent<Barrage>();
                    barrage.projectileSprite = ModAPI.LoadSprite(folder + "LArm.png");
                    person.Limbs[11].gameObject.AddComponent<PunchPower>().Damage = 1e30f;
                    person.Limbs[13].gameObject.AddComponent<PunchPower>().Damage = 1e30f;

                    upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                    () =>
                    {
                        Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
                    }));

                    foreach (var limb in person.Limbs)
                    {
                        limb.Health = float.PositiveInfinity;
                        limb.InitialHealth = float.MaxValue;
                        limb.BreakingThreshold = float.PositiveInfinity;
                        limb.GForcePassoutThreshold *= 100f;
                        limb.ImpactPainMultiplier = 0f;
                        limb.ImpactDamageMultiplier = 0f;
                        limb.ShotDamageMultiplier = 0f;
                        limb.FreezingTemperature = -300f;
                        limb.DiscomfortingHeatTemperature = float.PositiveInfinity;
                        limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                        limb.PhysicalBehaviour.Disintegratable = false;
                        limb.PhysicalBehaviour.Deletable = false;
                    }
                },
                null,
                "Jayraiya");

            ModAPI.Register<CategoryButtonEditor>();

            /*
            //Effect Test
            ModAPI.Register(
                new Modification()
                {
                    OriginalItem = ModAPI.FindSpawnable("Rod"),
                    NameOverride = "Meteor Explosion",
                    NameToOrderByOverride = "Meteor Explosion",
                    DescriptionOverride = "Meteor Explosion",
                    CategoryOverride = ModAPI.FindCategory(ModTab),
                    ThumbnailOverride = ModAPI.LoadSprite(""),
                    AfterSpawn = (Instance) =>
                    {
                        Instance.gameObject.AddComponent<FreezeBehaviour>();

                        GameObject Effect1 = GameObject.Instantiate(DBPU.MeteorExplosion_WindArea, Instance.transform.position + (Vector3.up * 0f), Quaternion.identity);
                        //Effect1.transform.localScale = new Vector3(1f, 1f, 1f);
                        Effect1.layer = 9;
                        Effect1.transform.SetParent(Instance.transform, true);

                        CeceUtils.DelayedInvoke(6.2f, () =>
                        {
                            GameObject.Destroy(Effect1);
                        });

                        CeceUtils.DelayedInvoke(0.5f, () =>
                        {
                            GameObject Effect2 = GameObject.Instantiate(DBPU.MeteorExplosion_AuraDrag, Instance.transform.position + (Vector3.up * 25f), Quaternion.identity);
                            //Effect2.transform.localScale = new Vector3(1f, 1f, 1f);
                            Effect2.layer = 9;
                            Effect2.transform.SetParent(Instance.transform, true);

                            CeceUtils.DelayedInvoke(4.6f, () =>
                            {
                                GameObject.Destroy(Effect2);
                            });
                        });

                        CeceUtils.DelayedInvoke(1.6f, () =>
                        {
                            GameObject Effect2 = GameObject.Instantiate(DBPU.MeteorExplosion_LightBeams, Instance.transform.position + (Vector3.up * 25f), Quaternion.identity);
                            //Effect2.transform.localScale = new Vector3(1f, 1f, 1f);
                            Effect2.layer = 9;
                            Effect2.transform.SetParent(Instance.transform, true);

                            CeceUtils.DelayedInvoke(4f, () =>
                            {
                                GameObject.Destroy(Effect2);
                            });
                        });

                        CeceUtils.DelayedInvoke(5.2f, () =>
                        {
                            GameObject Effect2 = GameObject.Instantiate(DBPU.MeteorExplosion_EnergyPillar, Instance.transform.position + (Vector3.up * 0f), Quaternion.identity);
                            //Effect2.transform.localScale = new Vector3(1f, 1f, 1f);
                            Effect2.layer = 9;
                            Effect2.transform.SetParent(Instance.transform, true);

                            CeceUtils.DelayedInvoke(1.9f, () =>
                            {
                                GameObject.Destroy(Effect2);
                            });
                        });

                        CeceUtils.DelayedInvoke(6.8f, () =>
                        {
                            GameObject Effect2 = GameObject.Instantiate(DBPU.MeteorExplosion_GiganticExplosion, Instance.transform.position + (Vector3.up * 0f), Quaternion.identity);
                            Effect2.transform.localScale = new Vector3(20f, 20f, 20f);
                            Effect2.layer = 9;
                            Effect2.transform.SetParent(Instance.transform, true);

                            foreach (Transform child in Effect2.transform)
                            {
                                var ps = child.gameObject.GetComponent<ParticleSystem>();
                                ps.playbackSpeed *= 10f;
                            }

                            CeceUtils.DelayedInvoke(0.1f, () =>
                            {
                                foreach (Transform child in Effect2.transform)
                                {
                                    var ps = child.gameObject.GetComponent<ParticleSystem>();
                                    ps.playbackSpeed /= 10f;
                                }
                            });

                            CeceUtils.DelayedInvoke(8f, () =>
                            {
                                GameObject.Destroy(Effect2);
                            });
                        });
                    }
                }
            );
            */
        }
    }
}
