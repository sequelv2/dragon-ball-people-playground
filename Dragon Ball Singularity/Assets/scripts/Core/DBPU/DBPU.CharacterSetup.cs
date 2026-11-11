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

        // Subcategories Code
        public struct ModAPIPlus // Timtam code struct only used for subcategories
        {
            public static void CreateHuman(string name, string description, string Thumbname, Action<GameObject> AfterSpawn = null, string Subcategory = null, string OrderOverride = null)
            {
                string thumb = Thumbname ?? "";
                thumb = thumb.Replace('\\', '/').Trim();
                while (thumb.StartsWith("Assets/Icons/", StringComparison.OrdinalIgnoreCase))
                    thumb = thumb.Substring("Assets/Icons/".Length);
                while (thumb.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    thumb = thumb.Substring(0, thumb.Length - 4);
                CreateCustomHuman(name, description, "Assets/Icons/" + thumb + ".png", AfterSpawn, Subcategory, OrderOverride);
            }

            public static void CreateCustomHuman(string name, string description, string thumbSpritePath, Action<GameObject> AfterSpawn = null, string Subcategory = null, string OrderOverride = null)
            {
                string suboverride = "";
                foreach (var catgor in Subcategories)
                {
                    if (catgor.Name == Subcategory)
                    {
                        suboverride = catgor.OrderOverride;
                    }
                }
                ModAPI.Register(
                    new Modification()
                    {
                        OriginalItem = ModAPI.FindSpawnable("Human"),
                        NameOverride = "[DBPU] " + name,
                        NameToOrderByOverride = suboverride + OrderOverride,
                        DescriptionOverride = description,
                        CategoryOverride = ModAPI.FindCategory("Nonexistent Category"),
                        ThumbnailOverride = ModAPI.LoadSprite(thumbSpritePath),
                        AfterSpawn = (Instance) =>
                        {
                            DbpuLimbDurability.EnsurePerson(Instance.GetComponent<PersonBehaviour>());
                            var identity = Instance.AddComponent<DbpuCharacterIdentity>();
                            identity.CharacterId = DeriveCharacterIdFromThumb(thumbSpritePath);
                            identity.DisplayName = name;
                            DbpuCharacterRegistry.Register(identity.CharacterId, name, thumbSpritePath);
                            AfterSpawn?.Invoke(Instance);
                            CustomTransformationCatalog.ApplyToPerson(Instance);
                            FormArtFromDisk.ApplyToPerson(Instance);
                            // Innate strength has to be live from spawn, not from the first transformation.
                            // A fighter restored from a save already carries it, so only a fresh one applies it.
                            var formMenu = Instance.transform.Find("Head")?.GetComponent<TransformationContextMenu>();
                            if (formMenu != null && CharacterPowerCatalog.GetInnatePower(identity.CharacterId) != 1f)
                                CeceUtils.DelayedInvoke(.2f, () =>
                                {
                                    if (formMenu && formMenu.Form == formMenu.BaseForm && Mathf.Approximately(formMenu.PreviousMultiplier, 1f))
                                        formMenu.ApplyForm(formMenu.BaseForm);
                                });
                            if (ZenkaiBoostBehaviour.IsSaiyan(identity.CharacterId, identity.DisplayName)
                                && Instance.GetComponent<ZenkaiBoostBehaviour>() == null)
                                Instance.AddComponent<ZenkaiBoostBehaviour>();
                        }
                    }
                );
            }

            static string DeriveCharacterIdFromThumb(string thumbSpritePath)
            {
                if (string.IsNullOrEmpty(thumbSpritePath)) return null;
                string path = thumbSpritePath.Replace('\\', '/').Trim();
                const string prefix = "Assets/Icons/";
                while (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(prefix.Length);
                while (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(0, path.Length - 4);
                return path;
            }

            public static void CreateObject(string originalObject, string name, string description, string ThumbnameFromIconsFolder, Action<GameObject> AfterSpawn = null, string Subcategory = null, string OrderOverride = null)
            {
                string suboverride = "";
                foreach (var catgor in Subcategories)
                {
                    if (catgor.Name == Subcategory)
                    {
                        suboverride = catgor.OrderOverride;
                    }
                }
                ModAPI.Register(
                    new Modification()
                    {
                        OriginalItem = ModAPI.FindSpawnable(originalObject),
                        NameOverride = "[DBPU] " + name,
                        NameToOrderByOverride = suboverride + Subcategory + OrderOverride,
                        DescriptionOverride = description,
                        CategoryOverride = ModAPI.FindCategory("Nonexistent Category"),
                        ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/" + ThumbnameFromIconsFolder + ".png"),
                        AfterSpawn = (Instance) =>
                        {
                            DbpuLimbDurability.EnsurePerson(Instance.GetComponent<PersonBehaviour>());
                            AfterSpawn?.Invoke(Instance);
                        }
                    }
                );
            }

            public static void RegisterSubcategory(string SubcategoryName, string CategoryDescription, string CategoryNameToOverride, string thumbnail)
            {
                ModAPI.Register(
                    new Modification()
                    {
                        OriginalItem = ModAPI.FindSpawnable("Brick"),
                        NameOverride = SubcategoryName,
                        NameToOrderByOverride = CategoryNameToOverride,
                        DescriptionOverride = CategoryDescription,
                        CategoryOverride = ModAPI.FindCategory(ModTab),
                        ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Closed.png"),
                        AfterSpawn = (Instance) =>
                        {
                            GameObject.Destroy(Instance);
                        }
                    }
                );
                var subcategory = new Subcategory()
                {
                    Name = SubcategoryName,
                    Description = CategoryDescription,
                    OrderOverride = CategoryNameToOverride,
                    Icon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Closed.png"),
                    OpenIcon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Open.png"),
                    EmptyIcon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Icon.png")
                };
                DBPU.Subcategories.Add(subcategory);
            }

            public static void RegisterSubSubcategory(string parentCategoryName, string SubcategoryName, string CategoryDescription, string CategoryNameToOverride, string thumbnail)
            {
                string parentOverride = Subcategories.FirstOrDefault(cat => cat.Name == parentCategoryName)?.OrderOverride ?? "";

                ModAPI.Register(
                    new Modification()
                    {
                        OriginalItem = ModAPI.FindSpawnable("Brick"),
                        NameOverride = SubcategoryName,
                        NameToOrderByOverride = parentOverride + CategoryNameToOverride,
                        DescriptionOverride = CategoryDescription,
                        CategoryOverride = ModAPI.FindCategory("Nonexistant Category"),
                        ThumbnailOverride = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Closed.png"),
                        AfterSpawn = (Instance) =>
                        {
                            GameObject.Destroy(Instance);
                        }
                    }
                );
                var subcategory = new Subcategory()
                {
                    Name = SubcategoryName,
                    Description = CategoryDescription,
                    OrderOverride = parentOverride + CategoryNameToOverride,
                    Icon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Closed.png"),
                    OpenIcon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Open.png"),
                    EmptyIcon = ModAPI.LoadSprite("Assets/Icons/" + thumbnail + "_Icon.png")
                };
                DBPU.Subcategories.Add(subcategory);
            }

        }

        static List<Sprite> LoadCharacterLimbSprites(string folder, string headFile = "Head.png")
        {
            return new List<Sprite>
            {
                ModAPI.LoadSprite(folder + headFile),
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
        }

        static void ApplySkinFormArt(CharacterSkins characterSkins, string characterName, string fileName, string skinKey = "Base")
        {
            if (characterSkins == null || string.IsNullOrEmpty(fileName)) return;
            string iconPath = CharacterSpriteRoot(characterName) + "Icons/" + fileName;
            if (!CharacterSpriteExists(iconPath)) return;
            if (!characterSkins.skins.SkinData.TryGetValue(skinKey, out var skinInfo) || skinInfo == null) return;
            if (skinInfo.Illustrations == null)
                skinInfo.Illustrations = new Dictionary<string, Sprite>();
            skinInfo.Illustrations["Damage0"] = LoadCharacterFormIllustration(characterName, fileName);
        }

        public static void ApplyGohanImmunities(Transform head, PersonBehaviour person)
        {
            head.gameObject.AddComponent<HumanController>().person = person;
            var hc = head.gameObject.GetComponent<HumanController>();
            hc.Features.ImmuneToKnockout = true;
            hc.Features.ImmuneToPain = true;
            hc.Features.ImmuneToOxigenLoss = true;
            hc.Features.ImmuneToBleeding = true;
            hc.Features.ImmuneToInternalBleeding = true;
            hc.Features.ImmuneToNumbness = true;
            hc.Features.ImmuneToFire = true;
            hc.Features.ImmuneToAcid = true;
            hc.Features.ImmuneToElectricity = true;
            hc.Features.ImmuneToStabbing = true;
            hc.Features.ImmuneToBullets = true;
            hc.Features.ImmuneToBrainDamage = true;
        }

        static void ApplyBmGohanPowers(GameObject Instance, Transform head, PersonBehaviour person)
        {
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
        }

        static void SetupBmTeenGohanSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Teen Gohan";

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            lower.gameObject.AddComponent<Flight>();

            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(head);
            hairObject.transform.localPosition = new Vector3(0f, 0f);
            hairObject.transform.rotation = head.rotation;
            hairObject.transform.localScale = new Vector3(1f, 1f);
            var hairSprite = hairObject.AddComponent<SpriteRenderer>();
            hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/teenhair.png");
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            var whiteThingObject = new GameObject("WhiteThing");
            whiteThingObject.transform.SetParent(head);
            whiteThingObject.transform.localPosition = new Vector3(0f, -0.27f, 0f);
            whiteThingObject.transform.rotation = head.rotation;
            whiteThingObject.transform.localScale = Vector3.one;
            var whiteThingSprite = whiteThingObject.AddComponent<SpriteRenderer>();
            whiteThingSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/whitething.png");
            whiteThingSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            whiteThingSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 2;

            string beltFolder = "Assets/Sprites/Characters/Gohan/Thingy1/";
            Sprite[] beltSprites1 = new Sprite[7]
            {
                ModAPI.LoadSprite(beltFolder + "stb.png"),
                ModAPI.LoadSprite(beltFolder + "stb1.png"),
                ModAPI.LoadSprite(beltFolder + "stb2.png"),
                ModAPI.LoadSprite(beltFolder + "stb3.png"),
                ModAPI.LoadSprite(beltFolder + "stb4.png"),
                ModAPI.LoadSprite(beltFolder + "stb5.png"),
                ModAPI.LoadSprite(beltFolder + "stb6.png")
            };

            beltFolder = "Assets/Sprites/Characters/Gohan/Thingy2/";
            Sprite[] beltSprites2 = new Sprite[6]
            {
                ModAPI.LoadSprite(beltFolder + "stf.png"),
                ModAPI.LoadSprite(beltFolder + "stf1.png"),
                ModAPI.LoadSprite(beltFolder + "stf2.png"),
                ModAPI.LoadSprite(beltFolder + "stf3.png"),
                ModAPI.LoadSprite(beltFolder + "stf4.png"),
                ModAPI.LoadSprite(beltFolder + "stf5.png")
            };

            const string agePrefix = "Assets/Sprites/Characters/Gohan/Teen/";
            var demonClothesSkins = LoadGohanAgeDemonClothesSkins(agePrefix);
            var turtleGiSkins = LoadGohanAgeTurtleGiSkins(agePrefix);

            person.ChangeSkin(demonClothesSkins["Damage0"]);
            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.56f);

            ApplyGohanImmunities(head, person);

            var kiSystem = Instance.AddComponent<KiSystem>();
            var characterTransformations = Instance.AddComponent<CharacterTransformations>();
            kiSystem.maxKi = 500000f;
            kiSystem.baseRegen = 5000f;

            Sprite teenBaseHead = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Teen/Head.png");
            if (teenBaseHead == null)
                teenBaseHead = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Teen/DemonClothesSkin/Head.png");
            Sprite teenSsj1Head = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Teen/SSJ head.png");
            if (teenSsj1Head == null) teenSsj1Head = teenBaseHead;
            Sprite teenSsj2Head = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Teen/SSJ2 head.png");
            if (teenSsj2Head == null) teenSsj2Head = teenBaseHead;

            var whiteThingAccessory = new List<(SpriteRenderer, bool)>
            {
                (whiteThingSprite, true)
            };

            var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.56f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/teenhair.png"),
                HeadSprite = teenBaseHead,
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = LoadCharacterFormIcon("Gohan", "teen.png"),
                Illustration = LoadCharacterFormIllustration("Gohan", "teen.png"),
                Name = "Base",
                Description = "Gohan during the Cell Games era.",
                ID = "Base",
                SkinDamageAmount = 0,
                SituationalAccessories = whiteThingAccessory
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformation);

            var ssj1Transformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 50f,
                Size = 1.56f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/ssj1teenhair1.png"),
                HeadSprite = teenSsj1Head,
                KiDrainRate = 4000f,
                AuraID = "SSJ1",
                Icon = LoadCharacterFormIcon("Gohan", "ssj1.png"),
                Illustration = LoadCharacterFormIllustration("Gohan", "ssj1.png"),
                Name = "Super Saiyan",
                Description = "Teen Gohan's Super Saiyan form.",
                ID = "SSJ1",
                SkinDamageAmount = 0,
                SituationalAccessories = whiteThingAccessory
            };
            characterTransformations.transformations.AddTransformation("SSJ1", ssj1Transformation);

            var ssj2Transformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 100f,
                Size = 1.56f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/teenssj2.png"),
                UseHairLocalPosition = true,
                HairLocalPosition = new Vector3(-0.0005f, -0.016f, 0f),
                HeadSprite = teenSsj2Head,
                KiDrainRate = 10000f,
                AuraID = "SSJ2",
                Icon = LoadCharacterFormIcon("Gohan", "ssj2.png"),
                Illustration = LoadCharacterFormIllustration("Gohan", "ssj2.png"),
                Name = "Super Saiyan 2",
                Description = "Teen Gohan's Super Saiyan 2 form from the Cell Games.",
                ID = "SSJ2",
                SkinDamageAmount = 1,
                SituationalAccessories = whiteThingAccessory
            };
            characterTransformations.transformations.AddTransformation("SSJ2", ssj2Transformation);

            CeceUtils.DelayedInvoke(0.1f, () => characterTransformations.ApplyTransformation(person, "Base"));

            head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
            head.gameObject.GetComponent<TransformationContextMenu>().person = person;

            var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
            var characterSkins = Instance.AddComponent<CharacterSkins>();
            skinsCM.SkinsData = characterSkins;

            var demonClothesSkin = new CharacterSkins.Skins.SkinInfo()
            {
                DisplayName = "Demon Clothes",
                Name = "DemonClothes"
            };
            demonClothesSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
            demonClothesSkin.Skins["DemonClothes"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", demonClothesSkins } };
            demonClothesSkin.Illustrations = new Dictionary<string, Sprite>
            {
                { "Damage0", LoadCharacterFormIllustration("Gohan", "teen.png") }
            };
            characterSkins.skins.AddSkin("DemonClothes", demonClothesSkin);

            var turtleGi = new CharacterSkins.Skins.SkinInfo()
            {
                DisplayName = "Turtle Gi",
                Name = "TurtleGi"
            };
            turtleGi.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
            turtleGi.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", turtleGiSkins } };
            turtleGi.Illustrations = new Dictionary<string, Sprite>
            {
                { "Damage0", LoadCharacterFormIllustration("Gohan", "teen.png") }
            };
            var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
            {
                objForAttach = lower.transform,
                connectedAnchor = new Vector2(0.1285f, 0.1286f),
                layerName = "Foreground",
                order = 1,
                sprites = beltSprites1,
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
                sprites = beltSprites2,
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
            turtleGi.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData> { belt1, belt2 };
            characterSkins.skins.AddSkin("TurtleGi", turtleGi);

            DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, agePrefix, "Head.png");

            skinsCM.CurrentSkin = "DemonClothes";
            skinsCM.PreviousSkin = "DemonClothes";
            head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

            ApplyBmGohanPowers(Instance, head, person);

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 1000000000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 1000000000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 1000000000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupSaiyanSagaTenshinhanSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Tenshinhan";

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            lower.gameObject.AddComponent<Flight>();

            const string folder = "Assets/Sprites/Characters/Tenshinhan/DBZ/";
            person.ChangeSkin(LoadCharacterLimbSprites(folder));
            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.87f);

            ApplyGohanImmunities(head, person);

            var kiSystem = Instance.AddComponent<KiSystem>();
            kiSystem.maxKi = 60000f;
            kiSystem.baseRegen = 600f;

            Instance.AddComponent<BlastBehaviour>();
            Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
            Instance.AddComponent<SolarFlare>().Character = "Tien";
            var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
            var r = pw.Refs;
            pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
            pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave");
            if (r.Solar != null) pw.Grant("SolarFlare");
            pw.Finish();
            PowerSetup.CompletePhysicalPowers(Instance, person);

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            Instance.AddComponent<AuraHandler>().person = person;

            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 3000000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 3000000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 3000000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupSaiyanSagaChiaotzuSpawn(GameObject Instance)
        {
            SetupChiaotzuSpawn(Instance, "Chiaotzu", "Assets/Sprites/Characters/Chiaotzu/", "Z Fit");
        }

        static void SetupBmChiaotzuSpawn(GameObject Instance)
        {
            SetupChiaotzuSpawn(Instance, "Chiaotzu", "Assets/Sprites/Characters/Chiaotzu/SuperFit/", "Super Fit");
        }

        static void SetupChiaotzuSpawn(GameObject Instance, string objectName, string skinFolder, string skinDisplayName)
        {
            Instance.gameObject.name = objectName;

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            const string chiaotzuRoot = "Assets/Sprites/Characters/Chiaotzu/";
            string folder = skinFolder.Replace('\\', '/');
            if (!folder.EndsWith("/")) folder += "/";

            ScouterChiaotzuAssets.EnsureLoaded();
            lower.gameObject.AddComponent<Flight>();
            person.ChangeSkin(LoadCharacterLimbSprites(folder));
            ApplyGohanImmunities(head, person);

            var identity = Instance.GetComponent<DbpuCharacterIdentity>();
            if (identity != null)
                identity.SpriteFolder = chiaotzuRoot.TrimEnd('/');

            var chiaotzu = head.gameObject.AddComponent<ChiaotzuBehaviour>();
            chiaotzu.SetupNow();

            var characterSkins = Instance.GetComponent<CharacterSkins>();
            if (characterSkins == null) characterSkins = Instance.AddComponent<CharacterSkins>();
            var skinsCM = head.gameObject.GetComponent<SkinContextMenu>();
            if (skinsCM == null) skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
            skinsCM.SkinsData = characterSkins;
            if (!characterSkins.skins.SkinData.ContainsKey("Base"))
            {
                var baseSprites = LoadCharacterLimbSprites(folder);
                var baseSkin = new CharacterSkins.Skins.SkinInfo
                {
                    DisplayName = skinDisplayName,
                    Name = "Base",
                    Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>(),
                    Illustrations = new Dictionary<string, Sprite>()
                };
                baseSkin.Skins["Base"] = new Dictionary<string, Dictionary<string, List<Sprite>>>
                {
                    { "None", new Dictionary<string, List<Sprite>> { { "Damage0", baseSprites } } }
                };
                baseSkin.Illustrations["Damage0"] = baseSprites != null && baseSprites.Count > 0 ? baseSprites[0] : null;
                characterSkins.skins.AddSkin("Base", baseSkin);
            }
            DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, chiaotzuRoot, "Head.png");
            skinsCM.CurrentSkin = "Base";
            skinsCM.PreviousSkin = "Base";

            var kiSystem = Instance.AddComponent<KiSystem>();
            kiSystem.maxKi = 40000f;
            kiSystem.baseRegen = 400f;

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

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            Instance.AddComponent<AuraHandler>().person = person;
            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 1500000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 1500000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 1500000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static List<Sprite> LoadAndroidOutfitSprites(string folder, bool useUArm2ForBackArm)
        {
            folder = folder.Replace('\\', '/');
            if (!folder.EndsWith("/")) folder += "/";
            Sprite Load(string file) => ModAPI.LoadSprite(folder + file);
            Sprite backUArm = useUArm2ForBackArm ? Load("UArm2.png") : Load("UArm.png");
            return new List<Sprite>
            {
                Load("Head.png"),
                Load("Upper.png"),
                Load("Middle.png"),
                Load("Lower.png"),
                Load("ULeg.png"),
                Load("LLeg.png"),
                Load("Foot.png"),
                Load("ULeg.png"),
                Load("LLeg.png"),
                Load("Foot.png"),
                Load("UArm.png"),
                Load("LArm.png"),
                backUArm,
                Load("LArm.png")
            };
        }

        static void SetupAndroidSpawn(
            GameObject Instance,
            string objectName,
            string characterFolder,
            string defaultOutfit,
            float heightMeters,
            float maxKi,
            float baseRegen,
            bool useUArm2ForBackArm)
        {
            Instance.gameObject.name = objectName;

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            characterFolder = characterFolder.Replace('\\', '/');
            if (!characterFolder.EndsWith("/")) characterFolder += "/";
            string dbsFolder = characterFolder + "DBS/";
            string dbzFolder = characterFolder + "DBZ/";
            string startFolder = characterFolder + defaultOutfit.Trim('/') + "/";

            lower.gameObject.AddComponent<Flight>();

            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(head);
            hairObject.transform.localPosition = new Vector3(0f, 0f);
            hairObject.transform.rotation = head.rotation;
            hairObject.transform.localScale = new Vector3(1f, 1f);
            var hairSprite = hairObject.AddComponent<SpriteRenderer>();
            Sprite hair = ModAPI.LoadSprite(startFolder + "Hair.png");
            if (hair == null) hair = ModAPI.LoadSprite(startFolder + "hair.png");
            if (hair == null) hair = ModAPI.LoadSprite(dbsFolder + "Hair.png");
            if (hair == null) hair = ModAPI.LoadSprite(dbsFolder + "hair.png");
            hairSprite.sprite = hair;
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            var dbsSprites = LoadAndroidOutfitSprites(dbsFolder, useUArm2ForBackArm);
            var dbzSprites = LoadAndroidOutfitSprites(dbzFolder, useUArm2ForBackArm);
            var startSprites = string.Equals(defaultOutfit, "DBZ", StringComparison.OrdinalIgnoreCase) ? dbzSprites : dbsSprites;
            person.ChangeSkin(startSprites);
            person.transform.root.transform.SetHeight(person, middle.gameObject, heightMeters);

            ApplyGohanImmunities(head, person);

            var identity = Instance.GetComponent<DbpuCharacterIdentity>();
            if (identity != null)
                identity.SpriteFolder = characterFolder.TrimEnd('/');

            var characterSkins = Instance.GetComponent<CharacterSkins>();
            if (characterSkins == null) characterSkins = Instance.AddComponent<CharacterSkins>();
            var skinsCM = head.gameObject.GetComponent<SkinContextMenu>();
            if (skinsCM == null) skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
            skinsCM.SkinsData = characterSkins;

            if (!characterSkins.skins.SkinData.ContainsKey("DBS"))
            {
                characterSkins.skins.AddSkin("DBS", new CharacterSkins.Skins.SkinInfo
                {
                    DisplayName = "DBS",
                    Name = "DBS",
                    PackFolderName = "DBS",
                    Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>
                    {
                        { "DBS", new Dictionary<string, Dictionary<string, List<Sprite>>>
                            {
                                { "None", new Dictionary<string, List<Sprite>> { { "Damage0", dbsSprites } } }
                            }
                        }
                    },
                    Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", dbsSprites != null && dbsSprites.Count > 0 ? dbsSprites[0] : null }
                    },
                    HairSprites = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite(dbsFolder + "Hair.png") ?? ModAPI.LoadSprite(dbsFolder + "hair.png") }
                    }
                });
            }

            if (!characterSkins.skins.SkinData.ContainsKey("DBZ"))
            {
                characterSkins.skins.AddSkin("DBZ", new CharacterSkins.Skins.SkinInfo
                {
                    DisplayName = "DBZ",
                    Name = "DBZ",
                    PackFolderName = "DBZ",
                    Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>
                    {
                        { "DBZ", new Dictionary<string, Dictionary<string, List<Sprite>>>
                            {
                                { "None", new Dictionary<string, List<Sprite>> { { "Damage0", dbzSprites } } }
                            }
                        }
                    },
                    Illustrations = new Dictionary<string, Sprite>
                    {
                        { "Damage0", dbzSprites != null && dbzSprites.Count > 0 ? dbzSprites[0] : null }
                    },
                    HairSprites = new Dictionary<string, Sprite>
                    {
                        { "Damage0", ModAPI.LoadSprite(dbzFolder + "Hair.png") ?? ModAPI.LoadSprite(dbzFolder + "hair.png")
                            ?? ModAPI.LoadSprite(dbsFolder + "Hair.png") ?? ModAPI.LoadSprite(dbsFolder + "hair.png") }
                    }
                });
            }

            DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, characterFolder, "Head.png");
            string startKey = string.Equals(defaultOutfit, "DBZ", StringComparison.OrdinalIgnoreCase) ? "DBZ" : "DBS";
            skinsCM.CurrentSkin = startKey;
            skinsCM.PreviousSkin = startKey;
            try { characterSkins.ApplySkin(person, startKey, "", 0); }
            catch { }

            var kiSystem = Instance.AddComponent<KiSystem>();
            kiSystem.maxKi = maxKi;
            kiSystem.baseRegen = baseRegen;
            kiSystem.CurrentForm = "Android";
            kiSystem.BaseForm = "Android";

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

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            Instance.AddComponent<AuraHandler>().person = person;
            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            float healthScale = maxKi >= 1200000f ? 7000000000000000f : 4000000000000000f;
            if (objectName == "Android 18" && maxKi < 1200000f) healthScale = 3800000000000000f;
            else if (objectName == "Android 18") healthScale = 6500000000000000f;

            foreach (var limb in person.Limbs)
            {
                limb.Health *= healthScale * DBPU.HealthMultiplier;
                limb.InitialHealth *= healthScale * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= healthScale;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupBmAndroid17Spawn(GameObject Instance)
        {
            SetupAndroidSpawn(Instance, "Android 17", "Assets/Sprites/Characters/Android 17/", "DBS", 1.69f, 1200000f, 12000f, true);
        }

        static void SetupBmAndroid18Spawn(GameObject Instance)
        {
            SetupAndroidSpawn(Instance, "Android 18", "Assets/Sprites/Characters/Android 18/", "DBS", 1.69f, 1150000f, 11500f, false);
        }

        static void SetupCellGamesAndroid17Spawn(GameObject Instance)
        {
            SetupAndroidSpawn(Instance, "Android 17", "Assets/Sprites/Characters/Android 17/", "DBZ", 1.69f, 900000f, 9000f, true);
        }

        static void SetupCellGamesAndroid18Spawn(GameObject Instance)
        {
            SetupAndroidSpawn(Instance, "Android 18", "Assets/Sprites/Characters/Android 18/", "DBZ", 1.69f, 850000f, 8500f, false);
        }

        static void SetupSaiyanSagaKidGohanSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Kid Gohan";

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            lower.gameObject.AddComponent<Flight>();

            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(head);
            hairObject.transform.localPosition = new Vector3(0f, 0f);
            hairObject.transform.rotation = head.rotation;
            hairObject.transform.localScale = new Vector3(1f, 1f);
            var hairSprite = hairObject.AddComponent<SpriteRenderer>();
            hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Kid/hair.png");
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            string beltFolder = "Assets/Sprites/Characters/Gohan/Thingy1/";
            Sprite[] beltSprites1 = new Sprite[7]
            {
                ModAPI.LoadSprite(beltFolder + "stb.png"),
                ModAPI.LoadSprite(beltFolder + "stb1.png"),
                ModAPI.LoadSprite(beltFolder + "stb2.png"),
                ModAPI.LoadSprite(beltFolder + "stb3.png"),
                ModAPI.LoadSprite(beltFolder + "stb4.png"),
                ModAPI.LoadSprite(beltFolder + "stb5.png"),
                ModAPI.LoadSprite(beltFolder + "stb6.png")
            };

            beltFolder = "Assets/Sprites/Characters/Gohan/Thingy2/";
            Sprite[] beltSprites2 = new Sprite[6]
            {
                ModAPI.LoadSprite(beltFolder + "stf.png"),
                ModAPI.LoadSprite(beltFolder + "stf1.png"),
                ModAPI.LoadSprite(beltFolder + "stf2.png"),
                ModAPI.LoadSprite(beltFolder + "stf3.png"),
                ModAPI.LoadSprite(beltFolder + "stf4.png"),
                ModAPI.LoadSprite(beltFolder + "stf5.png")
            };

            const string agePrefix = "Assets/Sprites/Characters/Gohan/Kid/";
            var demonClothesSkins = LoadGohanAgeDemonClothesSkins(agePrefix);
            var turtleGiSkins = LoadGohanAgeTurtleGiSkins(agePrefix);

            person.ChangeSkin(demonClothesSkins["Damage0"]);
            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.32f);

            ApplyGohanImmunities(head, person);

            var kiSystem = Instance.AddComponent<KiSystem>();
            var characterTransformations = Instance.AddComponent<CharacterTransformations>();
            kiSystem.maxKi = 8000f;
            kiSystem.baseRegen = 200f;

            var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.32f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Kid/hair.png"),
                HeadSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Gohan/Kid/Head.png"),
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = LoadCharacterFormIcon("Gohan", "kid.png"),
                Illustration = LoadCharacterFormIllustration("Gohan", "kid.png"),
                Name = "Base",
                Description = "Kid Gohan during the Saiyan Saga, training to face the incoming threat.",
                ID = "Base",
                SkinDamageAmount = 0
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformation);

            CeceUtils.DelayedInvoke(0.1f, () => characterTransformations.ApplyTransformation(person, "Base"));

            head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
            head.gameObject.GetComponent<TransformationContextMenu>().person = person;

            var skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
            var characterSkins = Instance.AddComponent<CharacterSkins>();
            skinsCM.SkinsData = characterSkins;

            var demonClothesSkin = new CharacterSkins.Skins.SkinInfo()
            {
                DisplayName = "Demon Clothes",
                Name = "DemonClothes"
            };
            demonClothesSkin.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
            demonClothesSkin.Skins["DemonClothes"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", demonClothesSkins } };
            demonClothesSkin.Illustrations = new Dictionary<string, Sprite>
            {
                { "Damage0", LoadCharacterFormIllustration("Gohan", "kid.png") }
            };
            characterSkins.skins.AddSkin("DemonClothes", demonClothesSkin);

            var turtleGi = new CharacterSkins.Skins.SkinInfo()
            {
                DisplayName = "Turtle Gi",
                Name = "TurtleGi"
            };
            turtleGi.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
            turtleGi.Skins["TurtleGi"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", turtleGiSkins } };
            turtleGi.Illustrations = new Dictionary<string, Sprite>
            {
                { "Damage0", LoadCharacterFormIllustration("Gohan", "kid.png") }
            };
            var belt1 = new CharacterSkins.Skins.PhyAccessoryData()
            {
                objForAttach = lower.transform,
                connectedAnchor = new Vector2(0.1285f, 0.1286f),
                layerName = "Foreground",
                order = 1,
                sprites = beltSprites1,
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
                sprites = beltSprites2,
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
            turtleGi.PhyAccessories = new List<CharacterSkins.Skins.PhyAccessoryData> { belt1, belt2 };
            characterSkins.skins.AddSkin("TurtleGi", turtleGi);

            DbpuCustomSkinLoader.RegisterDiscoveredSkins(characterSkins, agePrefix, "Head.png");

            skinsCM.CurrentSkin = "DemonClothes";
            skinsCM.PreviousSkin = "DemonClothes";
            head.gameObject.GetComponent<TransformationContextMenu>().skinContextMenu = skinsCM;

            Instance.AddComponent<BlastBehaviour>();
            Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
            var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
            var r = pw.Refs;
            pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
            pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave");
            pw.Finish();
            PowerSetup.CompletePhysicalPowers(Instance, person);

            Instance.AddComponent<AuraHandler>().person = person;

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 80000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 80000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 80000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupBasicDbpuFighter(
            GameObject Instance,
            string displayName,
            string spriteFolder,
            float height,
            float maxKi,
            float baseRegen,
            float healthMult,
            float formMult,
            string hairRelPath = null,
            bool addFlight = true,
            bool grantPowers = true)
        {
            Instance.gameObject.name = displayName;
            var identity = Instance.GetComponent<DbpuCharacterIdentity>();
            if (identity != null && !string.IsNullOrEmpty(spriteFolder))
                identity.SpriteFolder = spriteFolder.TrimEnd('/', '\\');

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            if (addFlight)
                lower.gameObject.AddComponent<Flight>();

            if (!string.IsNullOrEmpty(hairRelPath))
            {
                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = Vector3.zero;
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = Vector3.one;
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite(hairRelPath);
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;
            }

            string folder = spriteFolder.TrimEnd('/') + "/";
            person.ChangeSkin(LoadCharacterLimbSprites(folder));
            person.transform.root.transform.SetHeight(person, middle.gameObject, height);

            ApplyGohanImmunities(head, person);

            if (grantPowers)
            {
                var kiSystem = Instance.AddComponent<KiSystem>();
                kiSystem.maxKi = maxKi;
                kiSystem.baseRegen = baseRegen;

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
                Instance.GetComponent<MultipliersAndStuffs>().FormMultiplier = formMult;

                person.Limbs[1].gameObject.AddComponent<Barrage>();
                person.Limbs[11].gameObject.AddComponent<PunchPower>();
                person.Limbs[13].gameObject.AddComponent<PunchPower>();

                Instance.AddComponent<AuraHandler>().person = person;
                upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
                () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));
            }

            foreach (var limb in person.Limbs)
            {
                limb.Health *= healthMult * DBPU.HealthMultiplier;
                limb.InitialHealth *= healthMult * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= healthMult;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupSaiyanSagaNappaSpawn(GameObject Instance)
        {
            SetupBasicDbpuFighter(Instance, "Nappa", "Assets/Sprites/Characters/Nappa", 1.95f, 120000f, 1200f, 8000000000000f, 5000f);
        }

        public static void ApplyOozaruLimbSprite(LimbBehaviour limb, string fileName)
        {
            if (limb == null) return;
            var renderer = limb.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            string path = "Assets/Sprites/Characters/Oozaru/" + fileName;
            Sprite sprite = ModAPI.LoadSprite(path);
            Texture2D texture = ModAPI.LoadTexture(path);
            if (sprite == null) return;
            renderer.sprite = sprite;
            if (texture != null && renderer.material != null)
            {
                renderer.material.SetTexture("_FleshTex", texture);
                renderer.material.SetTexture("_BoneTex", texture);
            }
        }

        public static void ApplyOozaruLimbSprites(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            ApplyOozaruLimbSprite(person.Limbs[0], "Head.png");
            ApplyOozaruLimbSprite(person.Limbs[1], "Upper.png");
            ApplyOozaruLimbSprite(person.Limbs[2], "Middle.png");
            ApplyOozaruLimbSprite(person.Limbs[3], "Lower.png");
            ApplyOozaruLimbSprite(person.Limbs[4], "ULeg.png");
            ApplyOozaruLimbSprite(person.Limbs[5], "LLeg.png");
            ApplyOozaruLimbSprite(person.Limbs[6], "Foot.png");
            ApplyOozaruLimbSprite(person.Limbs[7], "ULeg.png");
            ApplyOozaruLimbSprite(person.Limbs[8], "LLeg.png");
            ApplyOozaruLimbSprite(person.Limbs[9], "Foot.png");
            ApplyOozaruLimbSprite(person.Limbs[10], "UArm.png");
            ApplyOozaruLimbSprite(person.Limbs[11], "LArm.png");
            ApplyOozaruLimbSprite(person.Limbs[12], "UArm.png");
            ApplyOozaruLimbSprite(person.Limbs[13], "LArm.png");
        }

        public static void ApplyOozaruLayout(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            person.Limbs[0].transform.localPosition = new Vector3(19.8697f, -3.1655f, 0f);
            person.Limbs[1].transform.localPosition = new Vector3(19.2758f, -5.2249f, 0f);
            person.Limbs[2].transform.localPosition = new Vector3(19.3848f, -7.7174f, 0f);
            person.Limbs[3].transform.localPosition = new Vector3(19.5615f, -10.0516f, 0f);
            person.Limbs[4].transform.localPosition = new Vector3(19.7466f, -11.4571f, 0f);
            person.Limbs[5].transform.localPosition = new Vector3(19.4438f, -14.297f, 0f);
            person.Limbs[6].transform.localPosition = new Vector3(19.9188f, -16.0853f, 0f);
            person.Limbs[7].transform.localPosition = new Vector3(19.7466f, -11.4571f, 0f);
            person.Limbs[8].transform.localPosition = new Vector3(19.4438f, -14.297f, 0f);
            person.Limbs[9].transform.localPosition = new Vector3(19.9188f, -16.0853f, 0f);
            person.Limbs[10].transform.localPosition = new Vector3(18.7679f, -6.7516f, 0f);
            person.Limbs[11].transform.localPosition = new Vector3(19.3049f, -10.7904f, 0f);
            person.Limbs[12].transform.localPosition = new Vector3(18.7679f, -6.7516f, 0f);
            person.Limbs[13].transform.localPosition = new Vector3(19.3049f, -10.7904f, 0f);
        }

        public static void ConfigureOozaruJoints(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            LimbBehaviour[] limbs = person.Limbs;

            SetOozaruJoint(limbs[0], new Vector2(-1.520986f, -1.901332f), true, -25f, 0f);
            Rigidbody2D headRb = limbs[0].GetComponent<Rigidbody2D>();
            if (headRb != null)
                headRb.angularDrag = Mathf.Max(headRb.angularDrag, 5f);

            SetOozaruJoint(limbs[1], new Vector2(-0.498209f, -3.329364f), true, -15f, 30f);

            HingeJoint2D middleJoint = limbs[2].GetComponent<HingeJoint2D>();
            if (middleJoint != null)
                UnityEngine.Object.Destroy(middleJoint);

            SetOozaruJoint(limbs[3], new Vector2(-0.447162f, 1.271698f), true, -30f, 15f);
            SetOozaruJoint(limbs[4], new Vector2(0.068143f, 1.448105f), true, -110f, 45f);
            SetOozaruJoint(limbs[5], new Vector2(-0.794684f, 2.144702f), true, 0f, 150f);
            SetOozaruJoint(limbs[6], new Vector2(-2.205267f, 0.711870f), true, 0f, 0f);
            SetOozaruJoint(limbs[7], new Vector2(0.035191f, 1.431630f), true, -110f, 45f);
            SetOozaruJoint(limbs[8], new Vector2(-0.761728f, 2.177653f), true, 0f, 150f);
            SetOozaruJoint(limbs[9], new Vector2(-2.244437f, 0.764918f), true, 0f, 0f);
            SetOozaruJoint(limbs[10], new Vector2(-0.042642f, 1.292986f), false, 0f, 360f);
            SetOozaruJoint(limbs[11], new Vector2(-2.276668f, 3.796033f), true, -120f, 0f);
            SetOozaruJoint(limbs[12], new Vector2(-0.042642f, 1.278007f), false, 0f, 360f);
            SetOozaruJoint(limbs[13], new Vector2(-2.326094f, 3.812509f), true, -120f, 0f);

            Physics2D.SyncTransforms();
            BakeOozaruStandingPoses(person);
        }

        public static void BakeOozaruStandingPoses(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.LinkedPoses == null) return;

            void Bake(PoseState state)
            {
                if (!person.LinkedPoses.ContainsKey(state) || person.LinkedPoses[state] == null) return;
                var pose = person.LinkedPoses[state];
                pose.Angles = new List<RagdollPose.LimbPose>();
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    LimbBehaviour limb = person.Limbs[i];
                    if (limb == null || limb.PhysicalBehaviour == null || limb.PhysicalBehaviour.isDisintegrated) continue;
                    if (!limb.HasJoint || limb.Joint == null || limb.Joint.connectedBody == null) continue;
                    float angle = limb.Joint.jointAngle
                        + (limb.transform.eulerAngles.z - limb.Joint.connectedBody.transform.eulerAngles.z)
                        * Mathf.Sign(person.transform.localScale.x);
                    pose.Angles.Add(new RagdollPose.LimbPose(limb, angle));
                }
                pose.ConstructDictionary();
            }

            Bake(PoseState.Rest);
            Bake(PoseState.Protective);
            if (person.OverridePoseIndex == 1 || person.OverridePoseIndex == 5)
                person.OverridePoseIndex = -1;
            if (person.LinkedPoses.ContainsKey(PoseState.Rest) && person.LinkedPoses[PoseState.Rest] != null)
                person.SetPose(person.LinkedPoses[PoseState.Rest]);
        }

        public static CharacterTransformations.Transformations.LimbLayoutData BuildOozaruLimbLayout()
        {
            return new CharacterTransformations.Transformations.LimbLayoutData
            {
                HeadScale = 0f,
                Positions = new Dictionary<string, Vector3>
                {
                    { "Head", new Vector3(19.8697f, -3.1655f, 0f) },
                    { "Upper", new Vector3(19.2758f, -5.2249f, 0f) },
                    { "Middle", new Vector3(19.3848f, -7.7174f, 0f) },
                    { "Lower", new Vector3(19.5615f, -10.0516f, 0f) },
                    { "ULegF", new Vector3(19.7466f, -11.4571f, 0f) },
                    { "LLegF", new Vector3(19.4438f, -14.297f, 0f) },
                    { "FootF", new Vector3(19.9188f, -16.0853f, 0f) },
                    { "ULegB", new Vector3(19.7466f, -11.4571f, 0f) },
                    { "LLegB", new Vector3(19.4438f, -14.297f, 0f) },
                    { "FootB", new Vector3(19.9188f, -16.0853f, 0f) },
                    { "UArmF", new Vector3(18.7679f, -6.7516f, 0f) },
                    { "LArmF", new Vector3(19.3049f, -10.7904f, 0f) },
                    { "UArmB", new Vector3(18.7679f, -6.7516f, 0f) },
                    { "LArmB", new Vector3(19.3049f, -10.7904f, 0f) }
                },
                Anchors = new Dictionary<string, Vector2>
                {
                    { "Head", new Vector2(-1.520986f, -1.901332f) },
                    { "Upper", new Vector2(-0.498209f, -3.329364f) },
                    { "Lower", new Vector2(-0.447162f, 1.271698f) },
                    { "ULegF", new Vector2(0.068143f, 1.448105f) },
                    { "LLegF", new Vector2(-0.794684f, 2.144702f) },
                    { "FootF", new Vector2(-2.205267f, 0.711870f) },
                    { "ULegB", new Vector2(0.035191f, 1.431630f) },
                    { "LLegB", new Vector2(-0.761728f, 2.177653f) },
                    { "FootB", new Vector2(-2.244437f, 0.764918f) },
                    { "UArmF", new Vector2(-0.042642f, 1.292986f) },
                    { "LArmF", new Vector2(-2.276668f, 3.796033f) },
                    { "UArmB", new Vector2(-0.042642f, 1.278007f) },
                    { "LArmB", new Vector2(-2.326094f, 3.812509f) }
                }
            };
        }

        public static void ApplyOozaruLimbMaterials(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null) return;
            PhysicalProperties props = ModAPI.FindPhysicalProperties("Metal");
            if (props == null) props = ModAPI.FindPhysicalProperties("Bulletproof");

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (limb == null) continue;
                limb.transform.localScale = Vector3.one * OozaruBodyScale;
                limb.gameObject.FixColliders();
                limb.IsAndroid = true;

                PhysicalBehaviour phys = limb.GetComponent<PhysicalBehaviour>();
                if (phys != null && props != null)
                    phys.Properties = props;
            }
        }

        public static void ApplyOozaruTitanStats(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null) return;
            float healthMult = 40000000000000f * DBPU.HealthMultiplier;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (limb == null) continue;

                limb.Health *= healthMult;
                limb.InitialHealth *= healthMult;
                limb.BreakingThreshold *= OozaruBreakingThresholdMult;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                if (limb.SkinMaterialHandler != null)
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;

                PhysicalBehaviour phys = limb.GetComponent<PhysicalBehaviour>();
                if (phys != null)
                {
                    float mult = Mathf.Max(1f, OozaruBaseMassFactor * GetOozaruLimbMassFactor(i));
                    phys.TrueInitialMass *= mult;
                    phys.RecalculateMassBasedOnSize();
                    limb.BaseStrength = phys.TrueInitialMass * OozaruStrengthPerMass;
                    limb.ImpactDamageMultiplier = Mathf.Max(1e-8f, limb.ImpactDamageMultiplier / mult);

                    Rigidbody2D rb = limb.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.drag = Mathf.Max(rb.drag, 0.12f);
                        rb.angularDrag = Mathf.Max(rb.angularDrag, i == 0 ? 5f : 1.2f);
                    }
                }
            }

            person.ShockLevel = 0f;
            person.PainLevel = 0f;
        }

        public static void ApplyFullOozaruBodySetup(PersonBehaviour person, GameObject root = null)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            if (root == null) root = person.transform.root.gameObject;

            ApplyOozaruLimbSprites(person);
            ApplyOozaruLimbMaterials(person);
            ApplyOozaruLayout(person);
            ConfigureOozaruJoints(person);
            ApplyOozaruTitanStats(person);
            IgnoreOozaruSelfCollisions(root);
            BakeOozaruStandingPoses(person);

            Transform head = person.transform.Find("Head");
            if (head != null)
            {
                foreach (var sr in head.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.gameObject == null) continue;
                    if (sr.gameObject.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    sr.enabled = false;
                }
            }
        }

        static void SetupSaiyanSagaOozaruSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Oozaru";
            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var person = Instance.GetComponent<PersonBehaviour>();
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;

            ApplyFullOozaruBodySetup(person, Instance);
            ApplyGohanImmunities(head, person);

            var kiSystem = Instance.AddComponent<KiSystem>();
            kiSystem.maxKi = 250000f;
            kiSystem.baseRegen = 2000f;

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

            PowerSetup.EnsurePunchPowerComponents(person);
            PowerSetup.EnsureKickPowerComponents(person);
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                var punch = limb.GetComponent<PunchPower>();
                if (punch != null) punch.Active = true;
                var kick = limb.GetComponent<KickPower>();
                if (kick != null) kick.Active = true;
            }
            PowerSetup.CompletePhysicalPowers(Instance, person);
            Instance.GetComponent<MultipliersAndStuffs>().FormMultiplier = 25000f;

            person.Limbs[1].gameObject.AddComponent<Barrage>();

            Instance.AddComponent<AuraHandler>().person = person;
            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton(
                "ToggleAura",
                (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"),
                "Toggle Aura",
                () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));
        }

        static void SetupSaiyanSagaVegetaSpawn(GameObject Instance)
        {
            const string vegetaFolder = "Assets/Sprites/Characters/Vegeta/SaiyanSaga/";
            const string oozaruFolder = "Assets/Sprites/Characters/Oozaru/";
            SetupBasicDbpuFighter(
                Instance,
                "Vegeta",
                "Assets/Sprites/Characters/Vegeta/SaiyanSaga",
                1.64f,
                180000f,
                1800f,
                12000000000000f,
                8000f,
                "Assets/Sprites/Characters/Vegeta/hair.png");

            var person = Instance.GetComponent<PersonBehaviour>();
            var head = Instance.transform.Find("Head");
            if (person == null || head == null) return;

            var characterTransformations = Instance.GetComponent<CharacterTransformations>();
            if (characterTransformations == null)
                characterTransformations = Instance.AddComponent<CharacterTransformations>();

            var baseSkin = LoadCharacterLimbSprites(vegetaFolder);
            var oozaruSkin = LoadCharacterLimbSprites(oozaruFolder);
            Sprite vegetaHair = ModAPI.LoadSprite("Assets/Sprites/Characters/Vegeta/hair.png");
            Sprite vegetaHead = ModAPI.LoadSprite(vegetaFolder + "Head.png");
            Sprite oozaruHead = ModAPI.LoadSprite(oozaruFolder + "Head.png");
            Sprite oozaruIcon = ModAPI.LoadSprite("Assets/Icons/SaiyanSaga/Oozaru.png");
            if (oozaruIcon == null) oozaruIcon = oozaruHead;

            var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.64f,
                HairSprite = vegetaHair,
                HeadSprite = vegetaHead,
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = vegetaHead,
                Illustration = vegetaHead,
                Name = "Base",
                Description = "Saiyan Saga Vegeta.",
                ID = "Base",
                RequiredSkin = baseSkin
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformation);

            var oozaruTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 10f,
                Size = 0f,
                HairSprite = null,
                HeadSprite = oozaruHead,
                KiDrainRate = 1200f,
                AuraID = "Base",
                Icon = oozaruIcon,
                Illustration = oozaruHead,
                Name = "Oozaru",
                Description = "Great Ape form.",
                ID = "Oozaru",
                RequiredSkin = oozaruSkin,
                SpecialBehaviours = new List<Type> { typeof(OozaruFormBehaviour) }
            };
            characterTransformations.transformations.AddTransformation("Oozaru", oozaruTransformation);

            CeceUtils.DelayedInvoke(0.1f, () => characterTransformations.ApplyTransformation(person, "Base"));

            var menu = head.gameObject.GetComponent<TransformationContextMenu>();
            if (menu == null) menu = head.gameObject.AddComponent<TransformationContextMenu>();
            menu.TransformationsData = characterTransformations;
            menu.person = person;
        }

        static void SetupStoryOfBardockSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Bardock";
            var identity = Instance.GetComponent<DbpuCharacterIdentity>();
            if (identity != null)
                identity.SpriteFolder = "Assets/Sprites/Characters/Bardock";

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            lower.gameObject.AddComponent<Flight>();

            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(head);
            hairObject.transform.localPosition = Vector3.zero;
            hairObject.transform.rotation = head.rotation;
            hairObject.transform.localScale = Vector3.one;
            var hairSprite = hairObject.AddComponent<SpriteRenderer>();
            hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Bardock/hair.png");
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            const string folder = "Assets/Sprites/Characters/Bardock/";
            person.ChangeSkin(LoadCharacterLimbSprites(folder));
            person.transform.root.transform.SetHeight(person, middle.gameObject, 1.75f);
            ApplyGohanImmunities(head, person);

            var kiSystem = Instance.AddComponent<KiSystem>();
            var characterTransformations = Instance.AddComponent<CharacterTransformations>();
            kiSystem.maxKi = 90000f;
            kiSystem.baseRegen = 900f;

            Sprite bardockHead = ModAPI.LoadSprite(folder + "Head.png");
            Sprite bardockSsjHead = ModAPI.LoadSprite(folder + "SSJ head.png");
            if (bardockSsjHead == null) bardockSsjHead = bardockHead;
            var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite(folder + "hair.png"),
                HeadSprite = bardockHead,
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = LoadCharacterFormIcon("Bardock", "Base.png", bardockHead),
                Illustration = LoadCharacterFormIllustration("Bardock", "Base.png", bardockHead),
                Name = "Base",
                Description = "Bardock",
                ID = "Base"
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformation);

            var ssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 50f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite(folder + "hair ssj1.png"),
                HeadSprite = bardockSsjHead,
                KiDrainRate = 800f,
                AuraID = "SSJ1",
                Icon = LoadCharacterFormIcon("Bardock", "SSJ1.png", bardockSsjHead),
                Illustration = LoadCharacterFormIllustration("Bardock", "SSJ1.png", bardockSsjHead),
                Name = "Super Saiyan",
                Description = "Super Saiyan",
                ID = "SSJ1"
            };
            characterTransformations.transformations.AddTransformation("SSJ1", ssjTransformation);

            CeceUtils.DelayedInvoke(0.1f, () => characterTransformations.ApplyTransformation(person, "Base"));

            head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
            head.gameObject.GetComponent<TransformationContextMenu>().person = person;

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
            Instance.GetComponent<MultipliersAndStuffs>().FormMultiplier = 4000f;

            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();
            Instance.AddComponent<AuraHandler>().person = person;
            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () => Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive));

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 6000000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 6000000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 6000000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void SetupBmBardockSpawn(GameObject Instance)
        {
            SetupBasicDbpuFighter(
                Instance,
                "Bardock",
                "Assets/Sprites/Characters/BardockSuper",
                1.75f,
                250000f,
                2500f,
                20000000000000f,
                25000f,
                "Assets/Sprites/Characters/BardockSuper/hair.png");

            var person = Instance.GetComponent<PersonBehaviour>();
            var head = Instance.transform.Find("Head");
            var identity = Instance.GetComponent<DbpuCharacterIdentity>();
            if (identity != null)
                identity.SpriteFolder = "Assets/Sprites/Characters/BardockSuper";

            if (Instance.GetComponent<CharacterTransformations>() != null)
            {
                FormArtFromDisk.ApplyToPerson(Instance);
                return;
            }

            var characterTransformations = Instance.AddComponent<CharacterTransformations>();

            const string folder = "Assets/Sprites/Characters/BardockSuper/";
            Sprite headSpr = ModAPI.LoadSprite(folder + "Head.png");
            var baseTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite(folder + "hair.png"),
                HeadSprite = headSpr,
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = LoadCharacterFormIcon("BardockSuper", "Base.png", headSpr),
                Illustration = LoadCharacterFormIllustration("BardockSuper", "Base.png", headSpr),
                Name = "Base",
                Description = "Bardock",
                ID = "Base"
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformation);

            var ssjTransformation = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 50f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite(folder + "hair ssj1.png"),
                HeadSprite = headSpr,
                KiDrainRate = 800f,
                AuraID = "SSJ1",
                Icon = LoadCharacterFormIcon("BardockSuper", "SSJ1.png", headSpr),
                Illustration = LoadCharacterFormIllustration("BardockSuper", "SSJ1.png", headSpr),
                Name = "Super Saiyan",
                Description = "Super Saiyan",
                ID = "SSJ1"
            };
            characterTransformations.transformations.AddTransformation("SSJ1", ssjTransformation);

            CeceUtils.DelayedInvoke(0.1f, () => characterTransformations.ApplyTransformation(person, "Base"));
            if (head != null)
            {
                var menu = head.gameObject.GetComponent<TransformationContextMenu>();
                if (menu == null) menu = head.gameObject.AddComponent<TransformationContextMenu>();
                menu.TransformationsData = characterTransformations;
                menu.person = person;
            }
            FormArtFromDisk.ApplyToPerson(Instance);
        }

        static void SetupDbsshFarmerSpawn(GameObject Instance)
        {
            SetupBasicDbpuFighter(
                Instance,
                "Farmer",
                "Assets/Sprites/Characters/Farmer",
                1.70f,
                1000f,
                10f,
                40f,
                1f,
                null,
                false,
                false);
            ApplyFarmerLayout(Instance.GetComponent<PersonBehaviour>());
        }

        public static void ApplyFarmerLayout(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            person.Limbs[0].transform.localPosition = new Vector3(-0.0212f, 0.4587f, 0f);
            person.Limbs[1].transform.localPosition = new Vector3(0.0191f, 0.1009f, 0f);
            person.Limbs[2].transform.localPosition = new Vector3(0.0181f, -0.1558f, 0f);
            person.Limbs[3].transform.localPosition = new Vector3(0.019f, -0.4698f, 0f);
            person.Limbs[4].transform.localPosition = new Vector3(0.0223f, -0.7837f, 0f);
            person.Limbs[5].transform.localPosition = new Vector3(0.0272f, -1.0832f, 0f);
            person.Limbs[6].transform.localPosition = new Vector3(0.0578f, -1.2686f, 0f);
            person.Limbs[7].transform.localPosition = new Vector3(0.0223f, -0.7837f, 0f);
            person.Limbs[8].transform.localPosition = new Vector3(0.0272f, -1.0832f, 0f);
            person.Limbs[9].transform.localPosition = new Vector3(0.0578f, -1.2686f, 0f);
            person.Limbs[10].transform.localPosition = new Vector3(0.0052f, -0.4616f, 0f);
            person.Limbs[11].transform.localPosition = new Vector3(0.0073f, -0.8536f, 0f);
            person.Limbs[12].transform.localPosition = new Vector3(0.0052f, -0.4616f, 0f);
            person.Limbs[13].transform.localPosition = new Vector3(0.0073f, -0.8536f, 0f);
        }

        public static void ApplyMajinBuuLayout(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            person.Limbs[0].transform.localPosition = new Vector3(-0.0144f, 0.6032f, 0f);
            person.Limbs[1].transform.localPosition = new Vector3(-0.0169f, 0.3063f, 0f);
            person.Limbs[2].transform.localPosition = new Vector3(-0.0159f, 0.0706f, 0f);
            person.Limbs[3].transform.localPosition = new Vector3(-0.0148f, -0.1942f, 0f);
            person.Limbs[4].transform.localPosition = new Vector3(-0.0131f, -0.542f, 0f);
            person.Limbs[5].transform.localPosition = new Vector3(-0.0121f, -0.8655f, 0f);
            person.Limbs[6].transform.localPosition = new Vector3(0.0572f, -1.0777f, 0f);
            person.Limbs[7].transform.localPosition = new Vector3(-0.0131f, -0.542f, 0f);
            person.Limbs[8].transform.localPosition = new Vector3(-0.0121f, -0.8655f, 0f);
            person.Limbs[9].transform.localPosition = new Vector3(0.0572f, -1.0777f, 0f);
            person.Limbs[10].transform.localPosition = new Vector3(-0.0142f, -0.256f, 0f);
            person.Limbs[11].transform.localPosition = new Vector3(-0.0116f, -0.648f, 0f);
            person.Limbs[12].transform.localPosition = new Vector3(-0.0142f, -0.256f, 0f);
            person.Limbs[13].transform.localPosition = new Vector3(-0.0116f, -0.648f, 0f);
        }

        public static void ApplyTeenGotenksLayout(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            person.Limbs[0].transform.localPosition = new Vector3(0f, 0.2861f, 0f);
            person.Limbs[1].transform.localPosition = new Vector3(-0.0289f, -0.0047f, 0f);
            person.Limbs[2].transform.localPosition = new Vector3(-0.0294f, -0.2465f, 0f);
            person.Limbs[3].transform.localPosition = new Vector3(-0.0089f, -0.5172f, 0f);
            person.Limbs[4].transform.localPosition = new Vector3(-0.0203f, -0.8609f, 0f);
            person.Limbs[5].transform.localPosition = new Vector3(-0.0212f, -1.2869f, 0f);
            person.Limbs[6].transform.localPosition = new Vector3(0.0448f, -1.4952f, 0f);
            person.Limbs[7].transform.localPosition = new Vector3(-0.0203f, -0.8609f, 0f);
            person.Limbs[8].transform.localPosition = new Vector3(-0.0212f, -1.2869f, 0f);
            person.Limbs[9].transform.localPosition = new Vector3(0.0448f, -1.4952f, 0f);
            person.Limbs[10].transform.localPosition = new Vector3(-0.0271f, -0.5668f, 0f);
            person.Limbs[11].transform.localPosition = new Vector3(-0.0257f, -0.9481f, 0f);
            person.Limbs[12].transform.localPosition = new Vector3(-0.0271f, -0.5668f, 0f);
            person.Limbs[13].transform.localPosition = new Vector3(-0.0257f, -0.9481f, 0f);
        }

        public static void ApplyGammaLayout(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;
            person.Limbs[0].transform.localPosition = new Vector3(-0.0222f, 0.8259f, 0f);
            person.Limbs[1].transform.localPosition = new Vector3(-0.0125f, 0.4755f, 0f);
            person.Limbs[2].transform.localPosition = new Vector3(0f, 0.191f, 0f);
            person.Limbs[3].transform.localPosition = new Vector3(-0.0064f, -0.0879f, 0f);
            person.Limbs[4].transform.localPosition = new Vector3(0f, -0.421f, 0f);
            person.Limbs[5].transform.localPosition = new Vector3(0f, -0.847f, 0f);
            person.Limbs[6].transform.localPosition = new Vector3(0.0572f, -1.0896f, 0f);
            person.Limbs[7].transform.localPosition = new Vector3(0f, -0.421f, 0f);
            person.Limbs[8].transform.localPosition = new Vector3(0f, -0.847f, 0f);
            person.Limbs[9].transform.localPosition = new Vector3(0.0572f, -1.0896f, 0f);
            person.Limbs[10].transform.localPosition = new Vector3(-0.0212f, -0.0708f, 0f);
            person.Limbs[11].transform.localPosition = new Vector3(-0.02f, -0.4806f, 0f);
            person.Limbs[12].transform.localPosition = new Vector3(-0.0212f, -0.0708f, 0f);
            person.Limbs[13].transform.localPosition = new Vector3(-0.02f, -0.4806f, 0f);

            Transform lower = person.transform.Find("Body")?.Find("LowerBody");
            if (lower != null)
            {
                Transform waist = lower.Find("Waist");
                if (waist != null)
                    waist.localPosition = new Vector3(0f, -0.09f, 0f);
            }
        }

        static void ApplyImmortalHumanSetup(GameObject Instance, PersonBehaviour person, Transform head, Transform middle, float healthMultiplier, float height = 1.75f)
        {
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

            person.transform.root.transform.SetHeight(person, middle.gameObject, height);

            foreach (var limb in person.Limbs)
            {
                limb.Health *= healthMultiplier * DBPU.HealthMultiplier;
                limb.InitialHealth *= healthMultiplier * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= healthMultiplier;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }
        }

        static void ApplyBuuSpawn(GameObject Instance, string folder, string displayName, bool addHair, bool addCape = false, float height = 1.75f)
        {
            Instance.gameObject.name = displayName;

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            person.ChangeSkin(LoadCharacterLimbSprites(folder, "Face.png"));

            if (addHair)
            {
                var hairObject = new GameObject("Hair");
                hairObject.transform.SetParent(head);
                hairObject.transform.localPosition = new Vector3(0f, 0f);
                hairObject.transform.rotation = head.rotation;
                hairObject.transform.localScale = new Vector3(1f, 1f);
                var hairSprite = hairObject.AddComponent<SpriteRenderer>();
                hairSprite.sprite = ModAPI.LoadSprite(folder + "hair.png");
                hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;
            }

            if (addCape)
            {
                var capeObject = new GameObject("Cape");
                capeObject.transform.SetParent(upper);
                capeObject.transform.localPosition = new Vector3(0f, 0f);
                capeObject.transform.rotation = upper.rotation;
                capeObject.transform.localScale = new Vector3(1f, 1f);
                var capeSprite = capeObject.AddComponent<SpriteRenderer>();
                capeSprite.sprite = ModAPI.LoadSprite(folder + "cape.png");
                capeSprite.sortingLayerName = upper.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                capeSprite.sortingOrder = upper.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;
            }

            ApplyImmortalHumanSetup(Instance, person, head, middle, 5000000000000000f, height);

            var kiSystem = Instance.AddComponent<KiSystem>();
            kiSystem.maxKi = 1000000f;
            kiSystem.baseRegen = 10000f;

            Instance.AddComponent<BlastBehaviour>();
            Instance.AddComponent<BeamBehaviour>().EffectID = "Kamehameha";
            Instance.GetComponent<BeamBehaviour>().Character = displayName;

            var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
            var r = pw.Refs;
            pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
            pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha");
            pw.Finish();
            PowerSetup.CompletePhysicalPowers(Instance, person);
            var buuAura = Instance.GetComponent<AuraHandler>() ?? Instance.AddComponent<AuraHandler>();
            buuAura.person = person;
            buuAura.CurrentAuraID = "Buu";

            lower.gameObject.AddComponent<Flight>();
            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();
            person.Limbs[13].gameObject.AddComponent<PunchPower>();

            upper.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura", (Func<string>)(() => !Instance.GetComponent<AuraHandler>().AuraActive ? "Enable Aura" : "Disable Aura"), "Toggle Aura",
            () =>
            {
                Instance.GetComponent<AuraHandler>().AuraActive = !Instance.GetComponent<AuraHandler>().AuraActive;
            }));

            DbpuCustomSkinLoader.SetupCharacterFolderSkins(Instance, head, folder, "Face.png", displayName);
            ApplySkinFormArt(Instance.GetComponent<CharacterSkins>(), "Buu", displayName + ".png");

            if (string.Equals(displayName, "Majin Buu", StringComparison.OrdinalIgnoreCase))
                ApplyMajinBuuLayout(person);

            BuuMatter.Configure(person);
            BuuRegeneration.Attach(person);
            BuuAbsorption.Attach(person);
        }

        static void ApplyGokuBlackSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Goku Black";

            var head = Instance.transform.Find("Head");
            var upper = Instance.transform.Find("Body").Find("UpperBody");
            var middle = Instance.transform.Find("Body").Find("MiddleBody");
            var lower = Instance.transform.Find("Body").Find("LowerBody");
            var person = Instance.GetComponent<PersonBehaviour>();

            string folder = "Assets/Sprites/Characters/Goku/Goku Black/";

            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(head);
            hairObject.transform.localPosition = new Vector3(0f, 0f);
            hairObject.transform.rotation = head.rotation;
            hairObject.transform.localScale = new Vector3(1f, 1f);
            var hairSprite = hairObject.AddComponent<SpriteRenderer>();
            hairSprite.sprite = ModAPI.LoadSprite(folder + "hair.png");
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            person.ChangeSkin(LoadCharacterLimbSprites(folder, "Head.png"));

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
            kiSystem.requiredColor = new Color(255f / 255f, 64f / 255f, 204f / 255f);

            var baseTransformationBlack = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 1f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite(folder + "hair.png"),
                HeadSprite = ModAPI.LoadSprite(folder + "Head.png"),
                KiDrainRate = 0f,
                AuraID = "Base",
                Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Blackbase.png"),
                Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/BlackBase.png"),
                Name = "Base",
                Description = "",
                ID = "Base",
                SkinDamageAmount = 0
            };
            characterTransformations.transformations.AddTransformation("Base", baseTransformationBlack);

            var ssj1TransformationBlack = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 50f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/hair ssj1.png"),
                HeadSprite = ModAPI.LoadSprite(folder + "SSJ1.png"),
                KiDrainRate = 4000f,
                AuraID = "SSJ1",
                Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/blackssj1.png"),
                Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/blackssj1.png"),
                Name = "Super Saiyan",
                Description = "",
                ID = "SSJ1",
                SkinDamageAmount = 1
            };
            characterTransformations.transformations.AddTransformation("SSJ1", ssj1TransformationBlack);

            var roseTransformationBlack = new CharacterTransformations.Transformations.TransformationStats()
            {
                PowerMultiplier = 10000f,
                Size = 1.75f,
                HairSprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/rose hair.png"),
                HeadSprite = ModAPI.LoadSprite(folder + "SSJRHead.png"),
                KiDrainRate = 5000f,
                AuraID = "SSJRose",
                Icon = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Rose.png"),
                Illustration = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Illustrations/Rose.png"),
                Name = "Super Saiyan Rosé",
                Description = "",
                ID = "SSJRose",
                SkinDamageAmount = 0
            };
            characterTransformations.transformations.AddTransformation("SSJRose", roseTransformationBlack);

            Instance.AddComponent<AuraHandler>().person = person;

            head.gameObject.AddComponent<TransformationContextMenu>().TransformationsData = characterTransformations;
            head.gameObject.GetComponent<TransformationContextMenu>().person = person;

            CeceUtils.DelayedInvoke(0.1f, () =>
            {
                characterTransformations.ApplyTransformation(person, "Base");
            });

            Instance.AddComponent<Kaioken>();
            Instance.AddComponent<BlastBehaviour>();

            var beamBehaviour = Instance.AddComponent<BeamBehaviour>();
            beamBehaviour.EffectID = "Kamehameha";
            beamBehaviour.Character = "Goku Black";

            CeceUtils.DelayedInvoke(0.15f, () =>
            {
                if (beamBehaviour != null) beamBehaviour.RegisterArmBeamTriggers();
            });

            var pw = PowerSetup.BeginPowers(head.gameObject, person, Instance);
            var r = pw.Refs;
            pw.Grant("KiBlast", PowerSetup.BeamBlock(r, () => r.Beam.Active = false));
            pw.GrantMany(PowerSetup.BeamBlock(r), "EnergyWave", "Kamehameha", "RoseKamehameha");
            pw.Finish();

            lower.gameObject.AddComponent<Flight>();
            pw.Grant("InstantTransmission");
            PowerSetup.CompletePhysicalPowers(Instance, person);

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
                limb.Health *= 5000000000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 5000000000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 5000000000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }

            person.ChangeWeightBasedOnHealth();
        }

        static void ApplyTeenGotenksSpawn(GameObject Instance)
        {
            Instance.gameObject.name = "Teen Gotenks";

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
            hairSprite.sprite = ModAPI.LoadSprite("Assets/Sprites/Characters/Teen Gotenks/hair.png");
            hairSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            hairSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1;

            string folder = "Assets/Sprites/Characters/Teen Gotenks/Skin/";
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

            person.ChangeSkin(skin);
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
            kiSystem.maxKi = 5000000f;
            kiSystem.baseRegen = 50000f;

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

            foreach (var limb in person.Limbs)
            {
                limb.Health *= 350000000000000f * DBPU.HealthMultiplier;
                limb.InitialHealth *= 350000000000000f * DBPU.HealthMultiplier;
                limb.BreakingThreshold *= 350000000000000f;
                limb.GForcePassoutThreshold *= 100f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier /= 10000f;
                limb.ShotDamageMultiplier = 0f;
                limb.FreezingTemperature = -300f;
                limb.DiscomfortingHeatTemperature = 2000f;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
            }

            ApplyTeenGotenksLayout(person);
        }
    }
}
