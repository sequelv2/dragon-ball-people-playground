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
        public static bool AFWEnabled = true;


        //Ki

        public static float PowerMultiplier = 1f;
        public static float KiDrainMultiplier = 0.5f;
        public static float KiRegenMultiplier = 1f;
        public static float KiAttackCostMultiplier = 1f;
        public static bool AurasEnabled = true;
        public static bool KiAtkCanDestroy = true;
        public static bool KiAtkCanDelete = true;
        public static bool ExtraCharge = true;
        public static float KiBlast_ExplosionSizeMultiplier = 1f;
        public static bool KiBlast_CustomColorEnabled = false;
        public static float KiBlast_CustomColorR = 1f;
        public static float KiBlast_CustomColorG = 0.85f;
        public static float KiBlast_CustomColorB = 0.3f;

        public static Color GetKiBlastCustomColor(float alpha = 0.1f)
        {
            return new Color(KiBlast_CustomColorR, KiBlast_CustomColorG, KiBlast_CustomColorB, alpha);
        }

        //Physical

        public static float DamageMultiplier = 1f;
        public static float HealthMultiplier = 1f;
        public static bool PhysAtkCanDestroy = true;
        public static bool PhysAtkStopLimbs = true;
        public static bool AutoEnablePunchKick = false;
        public static float Universal_KnockbackMultiplier = 1f;
        // Blades (sharp objects) can sever DBPU fighters' limbs. Kienzan always cuts.
        public static bool DismembermentEnabled = true;
        public static float EffectiveBeamKnockback => Beam_KnockbackMultiplier * Universal_KnockbackMultiplier;

        //Strain

        public static float StrainDrainMultiplier = 1f;
        public static float StrainGainMultiplier = 1f;
        public static float StrainDamageMultiplier = 1f;
        public static float KaiokenStrainMultiplier = 1f;
        public static float KaiokenMaxMultiplier = 20f;
        public static bool KaiokenStrainDamageEnabled = true;

        //Beams

        public static float Beam_KnockbackMultiplier = 1f;
        public static float Beam_SpeedMultiplier = 1f;
        public static float Beam_SizeMultiplier = 1f;
        public static bool Beam_ToggleDelete = false;
        public static bool Beam_ToggleDestroy = true;
        public static bool Beam_ToggleHeat = false;
        public static bool BeamAssistEnabled = true;
        public static bool BeamClashShockwaveEnabled = false;

        public static bool Beam_BurnAwayOnKill = true;
        public static bool KiDeflectEnabled = true;
        public static float KiDeflectHybridPowerThreshold = 1.35f;
        public static float KiDeflectBlastThresholdMultiplier = 1f;
        public static float KiDeflectBeamThresholdMultiplier = 1.2f;
        public static bool KiDeflectBeamCatchThenAim = false;
        public static float KiDeflectBeamCatchSeconds = 0.22f;
        public static bool KiDeflectBeamSplitEnabled = true;
        public static float KiDeflectBeamSplitThresholdMultiplier = 0.85f;
        public static int KiDeflectBeamSplitRayCount = 9;
        public static float KiDeflectBeamSplitWrapDistance = 1.1f;
        public static float KiDeflectBeamSplitContinuationLength = 7f;
        public static float KiDeflectBeamSplitArcDegrees = 150f;
        public static float KiDeflectBeamSplitLifetime = 0.42f;
        public static float SparkingOverloadSeconds = 180f;
        public static float SparkingExhaustSeconds = 60f;
        public static float SparkingCooldownSeconds = 0f;
        public static float SparkingOverchargeHoldSeconds = 3f;
        public static bool SparkingEnabled = true;
        public static bool KiChargeSparkingOverchargeEnabled = true;
        public static bool KiOverdriveOutputEnabled = true;
        public static float KiOverdriveMaxPercent = 200f;
        public static float KiOverdriveSecondsBeforeExhaust = 2.75f;

        public static float Special_ExponentialGrowthSpeed = 1.005f;
        public static bool Special_ZenkaiBoostEnabled = false;
        public static float Special_ZenkaiBoostScale = 1f;
        public static float Special_ZenkaiBoostMax = 10f;

        //Misc

        public static bool VoiceLines = true;
        public static float VoiceLineVolume = 1f;
        public static float PowersVolume = 1f;

        public static float KiChargeHoldRegenPerSecond = 50000f;
        public static float KiChargeAuraScaleMultiplier = 1.25f;
        public static float KiChargeAuraParticleSpeedMultiplier = 1.35f;
        public static int KiChargeDebrisMaxActive = 28;
        public static int KiChargeDebrisMaxSession = 80;
        public static float KiChargeDebrisSpawnInterval = 0.52f;
        public static float KiChargeDebrisRingMin = 0.35f;
        public static float KiChargeDebrisRingMax = 1.1f;
        public static float KiChargeDebrisSpawnYOffset = -1f;
        public static bool KiChargeFxEnabled = true;
        public static bool KiChargeAutoTransformWhileChargingEnabled = false;
        public static float KiChargeAutoTransformSecondsPerTier = 12f;

        public static bool TransformationEyeGlowEnabled = true;
        public static bool TransformationHairGlowEnabled = true;
        public static float TransformationHairGlowBrightness = 100f;

        public static bool QuickTransformEnabled = true;

        public static float UiAuraScale = 3.4f;
        public static float UiAuraYOffset = -0.09f;

        public const string OpenPowerMenuActionId = "dbpu.openpowermenu";
        public const KeyCode OpenPowerMenuDefaultKey = KeyCode.Semicolon;
        public const string QuickTransformActionId = "dbpu.quicktransform";
        public const string BarrageActionId = "dbpu.barrage";
        public const string KaiokenActionId = "dbpu.kaioken";
        public const string InstantTransmissionActionId = "dbpu.instanttransmission";
        public const string CellMaxScreamActionId = "dbpu.cellmaxscream";
        public const string KiChargeHoldActionId = "dbpu.kichargehold";
        public const string KiOverdriveOutputActionId = "dbpu.kioverdriveoutput";
        public const KeyCode KiOverdriveOutputDefaultKey = KeyCode.R;

        public static Sprite ControlsSectionIcon;



        public static Material barMaterial;

        public static Sprite barSprite;
        public static Sprite depletedBarSprite;
        public static Sprite barFrameSprite;

        public static Sprite strainBarSprite;
        public static Sprite strainDepletedBarSprite;
        public static Sprite strainBarFrameSprite;

        public static GameObject hitParticle;
        public static GameObject barrageImpact;


        public static Sprite Icon_PunchPower;
        public static Sprite Icon_KickPower;
        public static Sprite Icon_Barrage;
        public static Sprite Icon_KiBlast;
        public static Sprite Icon_KiBlastBarrage;
        public static Sprite Icon_EnergyWave;
        public static Sprite Icon_Kamehameha;
        public static Sprite Icon_RoseKamehameha;
        public static Sprite Icon_GalickGun;
        public static Sprite Icon_FinalFlash;
        public static Sprite Icon_LastEmperor;
        public static Sprite Icon_GiganticFullBlast;
        public static Sprite Icon_FullForceKamehameha;
        public static Sprite Icon_GiganticRoar;
        public static Sprite Icon_MaxCannon;
        public static Sprite Icon_HellzoneGranade;
        public static Sprite Icon_DeathBeam;
        public static Sprite Icon_DeathBall;
        public static Sprite Icon_Supernova;
        public static Sprite Icon_GiganticCrusher;
        public static Sprite Icon_SpiritBomb;
        public static Sprite Icon_SuperSpiritBomb;
        public static Sprite Icon_StardustFall;
        public static Sprite Icon_StardustBreaker;
        public static Sprite Icon_GodPunisher;
        public static Sprite Icon_MeteorExplosion;
        public static Sprite Icon_FinalExplosion;
        public static Sprite Icon_SolarFlare;
        public static Sprite Icon_KiShield;
        public static Sprite Icon_InstantTransmission;
        public static Sprite Icon_Flight;
        public static Sprite Icon_Kaioken;
        public static Sprite Icon_GreatNamekianGrowth;
        public static Sprite Icon_DisasterRay;
        public static Sprite Icon_EyeLaser;
        public static Sprite Icon_CataclysmicOrb;
        public static Sprite Icon_GodsWrath;
        public static Sprite Icon_WrathOfTheGod;
        public static Sprite Icon_BeerusBall;



        public static GameObject Aura_Kaioken;
        public static GameObject Aura_White;
        public static GameObject Aura_SSJ1;
        public static GameObject Aura_SSJ2;
        public static GameObject Aura_SSJ3;
        public static GameObject Aura_SSJG;
        public static GameObject Aura_SSJB;
        public static GameObject Aura_SSJBE;
        public static GameObject Aura_Mystic;
        public static GameObject Aura_Frieza;
        public static GameObject Aura_GoldenFrieza;
        public static GameObject Aura_Broly;
        public static GameObject Aura_MUI;

        public static GameObject Aura_MetamoroFusion;



        public static GameObject KiBlast_Start;
        public static GameObject KiBlast_End;
        public static GameObject KiBlast_Hit;

        public static GameObject DeathBeamCharge1;
        public static GameObject DeathBeamCharge2;

        public static GameObject FinalExplosion_Ball;
        public static GameObject FinalExplosion_Debris;

        public static GameObject MeteorExplosion_WindArea;
        public static GameObject MeteorExplosion_AuraDrag;
        public static GameObject MeteorExplosion_LightBeams;
        public static GameObject MeteorExplosion_EnergyPillar;
        public static GameObject MeteorExplosion_GiganticExplosion;



        public static GameObject EnergyWave_Beam_Start;
        public static GameObject EnergyWave_Beam_End;

        public static GameObject Kamehameha_Charge;
        public static GameObject Kamehameha_Beam_Start;
        public static GameObject Kamehameha_Beam_End;

        public static GameObject FinalFlash_Charge;
        public static GameObject FinalFlash_Beam_Start;
        public static GameObject FinalFlash_Beam_End;

        public static GameObject GalickGun_Charge;
        public static GameObject GalickGun_Aura;
        public static GameObject GalickGun_Beam_Start;
        public static GameObject GalickGun_Beam_End;

        public static GameObject LastEmperor_Beam_Start;
        public static GameObject LastEmperor_Beam_End;

        public static GameObject GiganticFullBlast_Beam_Start;
        public static GameObject GiganticFullBlast_Beam_End;

        public static GameObject FullForceKamehameha_Beam_Start;
        public static GameObject FullForceKamehameha_Beam_End;



        public static GameObject GenkiDama;
        public static GameObject SuperGenkiDama;

        public static GameObject Supernova;

        public static GameObject GiganticCrusher;
        public static GameObject GiganticCrusherWind;

        public static GameObject DeathBall_Ball;
        public static GameObject DeathBall_Hit;

        public static GameObject StardustBreaker;



        public static void OnLoad()
        {
            NarutoSpritePreview.StartIfRequested();
            GogitoPreview.StartIfRequested();
            CombatWheelQa.StartIfRequested();
            KiCombatQa.StartIfRequested();
            MenuQa.StartIfRequested();
            ReduxAuraQa.StartIfRequested();
            PowerScaleQa.StartIfRequested();
            BuuRegenerationQa.StartIfRequested();
            BuuAbsorptionQa.StartIfRequested();
            AnnihilationQa.StartIfRequested();
            SparkingOutlineQa.StartIfRequested();
            BuuMatter.Register();
            SparkingOutlineAssets.Initialize(ModAPI.Metadata.MetaLocation);
            SpiritBombVfx.Initialize(ModAPI.Metadata.MetaLocation);
            KiCombatVfxAssets.Initialize(ModAPI.Metadata.MetaLocation);
            DbpuFx.Preload(ModAPI.Metadata.MetaLocation);
            KiHandLoadout.PreloadIcons();
            RealityRift.Initialize(ModAPI.Metadata.MetaLocation);
            RefinedAuras.Initialize(ModAPI.Metadata.MetaLocation);
            Singularity.Boot(ModAPI.Metadata.MetaLocation);
            NarutoChakraController.SetDirectory(ModAPI.Metadata.MetaLocation);
            ModConfigs.Load();
            LimbsStorage.Load();
            CharacterPowerCatalog.Load();
            CustomTransformationCatalog.Load();
            try { DbpuWorkshop.ImportSubscribedTransformations(); }
            catch (Exception ex) { Debug.LogWarning("[DB Singularity] Workshop transform sync failed: " + ex.Message); }
            DbpuInputRegistration.RegisterAll();
            DbpuInputRegistration.ApplyLegacyConfigKeybinds();
            DbpuControlsMenuPatch.Apply();
            DbpuHarmonyBootstrap.EnsureApplied();

            AnimationFramework.EnsureManager();
            AFWEnabled = true;

            global::DBPU.LiveSpriteEditor.LiveSpriteEditorEntry.EnsureInstalled();

            barMaterial = AssetBundleLoader.LoadMaterialFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/barshader", "Unlit_NewUnlitShader 1");

            barSprite = ModAPI.LoadSprite("Assets/Sprites/UI/bar.png");
            barFrameSprite = ModAPI.LoadSprite("Assets/Sprites/UI/barFrame.png");
            depletedBarSprite = ModAPI.LoadSprite("Assets/Sprites/UI/depletedbar.png");
            ControlsSectionIcon = ModAPI.LoadSprite("Assets/Sprites/Items/DB_4.png");

            strainBarSprite = ModAPI.LoadSprite("Assets/Sprites/UI/strainbar.png");
            strainBarFrameSprite = ModAPI.LoadSprite("Assets/Sprites/UI/strainbarFrame.png");
            strainDepletedBarSprite = ModAPI.LoadSprite("Assets/Sprites/UI/depletedstrainbar.png");

            hitParticle = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/impact", "DefaultHit poolable");

            Icon_PunchPower = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/PunchPower.png");
            Icon_KickPower = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/KickPower.png");
            Icon_Barrage = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/Barrage.png");
            Icon_KiBlast = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/KiBlast.png");
            Icon_KiBlastBarrage = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/KiBlastBarrage.png");
            Icon_EnergyWave = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/EnergyWave.png");
            Icon_Kamehameha = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/Kamehameha.png");
            try { Icon_RoseKamehameha = ModAPI.LoadSprite("Assets/Sprites/Characters/Goku/Icons/Rose.png"); }
            catch { Icon_RoseKamehameha = Icon_Kamehameha; }
            Icon_GalickGun = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GalickGun.png");
            Icon_FinalFlash = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/FinalFlash.png");
            Icon_LastEmperor = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/LastEmperor.png");
            Icon_GiganticFullBlast = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GiganticFullBlast.png");
            Icon_FullForceKamehameha = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/FullForceKamehameha.png");
            Icon_GiganticRoar = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GiganticRoar.png");
            try { Icon_MaxCannon = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/MaxCannon.png"); }
            catch { Icon_MaxCannon = Icon_GiganticRoar; }
            Icon_HellzoneGranade = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/HellzoneGranade.png");
            Icon_DeathBeam = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/DeathBeam.png");
            Icon_DeathBall = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/DeathBall.png");
            Icon_Supernova = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/Supernova.png");
            Icon_GiganticCrusher = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GiganticCrusher.png");
            Icon_SpiritBomb = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/SpiritBomb.png");
            Icon_SuperSpiritBomb = Icon_SpiritBomb;
            Icon_StardustFall = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/StardustFall.png");
            Icon_StardustBreaker = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/StardustBreaker.png");
            Icon_GodPunisher = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GodPunisher.png");
            Icon_MeteorExplosion = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/MeteorExplosion.png");
            Icon_FinalExplosion = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/FinalExplosion.png");
            Icon_SolarFlare = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/SolarFlare.png");
            Icon_KiShield = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/KiShield.png");
            Icon_InstantTransmission = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/InstantTransmission.png");
            Icon_Flight = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/Flight.png");
            try { Icon_Kaioken = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/Kaioken.png"); }
            catch { Icon_Kaioken = Icon_FinalFlash; }
            Icon_GreatNamekianGrowth = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GreatNamekianGrowth.png");
            try { Icon_DisasterRay = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/DisasterRay.png"); }
            catch { Icon_DisasterRay = Icon_DeathBeam; }
            try { Icon_EyeLaser = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/EyeLaser.png"); }
            catch { Icon_EyeLaser = Icon_DisasterRay ?? Icon_DeathBeam; }
            try { Icon_CataclysmicOrb = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/CataclysmicOrb.png"); }
            catch { Icon_CataclysmicOrb = Icon_DeathBall; }
            try { Icon_GodsWrath = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/GodsWrath.png"); }
            catch { Icon_GodsWrath = Icon_KiBlastBarrage; }
            try { Icon_WrathOfTheGod = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/WrathOfTheGod.png"); }
            catch { Icon_WrathOfTheGod = Icon_EnergyWave; }
            try { Icon_BeerusBall = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/Icons/BeerusBall.png"); }
            catch { Icon_BeerusBall = Icon_Supernova; }

            // Auras

            Aura_Kaioken = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "kaioken");
            Aura_White = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "base");
            Aura_SSJ1 = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "ssj aura");
            Aura_SSJ2 = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "ssj2 aura");
            Aura_SSJ3 = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "ssj3 aura");
            Aura_SSJG = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "kaioken");
            Aura_SSJB = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "ssjb");
            Aura_SSJBE = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "ssjbe");
            Aura_Mystic = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "mystic");
            Aura_Frieza = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "frieza base");
            Aura_GoldenFrieza = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "gold frieza");
            Aura_MUI = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "MUI");
            if (Aura_MUI == null)
                Aura_MUI = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/db", "mui");
            Aura_Broly = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/broly", "broly");

            Aura_MetamoroFusion = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Auras/gabe_fusion", "Fusion_PREFAB");

            AuraOutlineController.softEdgeMaterial = AssetBundleLoader.LoadMaterialFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/aura", "SoftEdge");
            AuraOutlineController.FlameMat = AssetBundleLoader.LoadMaterialFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/aura", "FlameAuraOutline");
            ModAPI.Register<AuraOutlineController>();

            // Ki Attacks

            KiBlast_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Ki Blast Start");
            KiBlast_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Ki Blast End");
            KiBlast_Hit = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/blast", "Ki Blast Hit");

            DeathBeamCharge1 = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/mc db vfx", "freeza beam charge 1");
            DeathBeamCharge2 = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/mc db vfx", "freeza beam charge 2");

            DeathBall_Ball = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/deathball", "DB");
            //DeathBall_Hit = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/deathball", "Death Ball Hit");

            FinalExplosion_Ball = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/fexplosion", "FinalExplosion");
            FinalExplosion_Debris = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/fexplosion", "FinExp");

            MeteorExplosion_WindArea = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/gogetaall", "AreaWind");
            MeteorExplosion_AuraDrag = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/gogetaall", "AuraDrag");
            MeteorExplosion_LightBeams = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/gogetaall", "LightBeams");
            MeteorExplosion_EnergyPillar = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/gogetaall", "EnergyPillar");
            MeteorExplosion_GiganticExplosion = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/gogetaall", "GiganticExplosion");

            // Beams

            EnergyWave_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha Start");
            EnergyWave_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha End");

            Kamehameha_Charge = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha Ball");
            Kamehameha_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha Start");
            Kamehameha_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha End");

            FinalFlash_Charge = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Final Flash Ball");
            FinalFlash_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Final Flash Start");
            FinalFlash_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Final Flash End");

            GalickGun_Charge = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Galick Gun Ball");
            GalickGun_Aura = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Galick Aura");
            GalickGun_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Galick Gun Start");
            GalickGun_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Galick Gun End");

            LastEmperor_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha Start");
            LastEmperor_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha End");

            GiganticFullBlast_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha Start");
            GiganticFullBlast_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/kamehamehabundle", "Kamehameha End");

            FullForceKamehameha_Beam_Start = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/dragon", "Kame Root");
            FullForceKamehameha_Beam_End = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/dragon", "KameEnd");

            // Balls

            GenkiDama = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/dragon ball balls", "Spirit Bomb");
            SuperGenkiDama = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/dragon ball balls", "Super Spirit Bomb");
            if (SuperGenkiDama == null) SuperGenkiDama = GenkiDama;
            Supernova = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/sdseffects", "Solar");

            GiganticCrusher = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/gigacrusher", "GiganticCrusher");
            GiganticCrusherWind = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ball/gigacrusher", "GiganticCrusherWindUp");

            StardustBreaker = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/Powers/Ki/dragon", "RainbowOrb");

            // Others

            barrageImpact = AssetBundleLoader.LoadGameObjectFromAssetBundle("UnityEngine.AssetBundleModule.dll", "Assets/VFX/AssetBundles/barrage", "hit-white-4");

            // properaties
            /*
            PhysProperties.StrongLimbProperties.ImpactIntensityMutliplier = 6f;
            PhysProperties.StrongLimbProperties.HitVolumeMultiplier = 0.025f;
            PhysProperties.StrongLimbProperties.IsPoolable = false;
            PhysProperties.StrongLimbProperties.ShotImpact = null;
            PhysProperties.StrongLimbProperties.HardImpact = PhysProperties.StrongLimbProperties.SoftImpact;

            PhysProperties.SuperLimbProperties.Softness = 0;
            PhysProperties.SuperLimbProperties.Brittleness = 0f;
            PhysProperties.SuperLimbProperties.ImpactIntensityMutliplier = 0.6f;
            PhysProperties.SuperLimbProperties.HitVolumeMultiplier = 0.25f;
            PhysProperties.SuperLimbProperties.IsPoolable = false;
            PhysProperties.SuperLimbProperties.ShotImpact = null;
            PhysProperties.SuperLimbProperties.BulletSpeedAbsorptionPower = 0f;
            PhysProperties.SuperLimbProperties.HardImpact = PhysProperties.SuperLimbProperties.SoftImpact;

            PhysProperties.GodlyLimbProperties.Softness = 0;
            PhysProperties.GodlyLimbProperties.Brittleness = 0f;
            PhysProperties.GodlyLimbProperties.ImpactIntensityMutliplier = 0.02f;
            PhysProperties.GodlyLimbProperties.HitVolumeMultiplier = 20f;
            PhysProperties.GodlyLimbProperties.IsPoolable = false;
            PhysProperties.GodlyLimbProperties.ShotImpact = null;
            PhysProperties.GodlyLimbProperties.BulletSpeedAbsorptionPower = 0f;
            PhysProperties.GodlyLimbProperties.Flammability = 0f;
            PhysProperties.GodlyLimbProperties.Conducting = false;
            PhysProperties.GodlyLimbProperties.HardImpact = PhysProperties.GodlyLimbProperties.SoftImpact;

            PhysProperties.GodLimbProperties.Softness = 0;
            PhysProperties.GodLimbProperties.Brittleness = 0f;
            PhysProperties.GodLimbProperties.ImpactIntensityMutliplier = 0.0001f;
            PhysProperties.GodLimbProperties.HitVolumeMultiplier = 1000f;
            PhysProperties.GodLimbProperties.IsPoolable = false;
            PhysProperties.GodLimbProperties.ShotImpact = null;
            PhysProperties.GodLimbProperties.BulletSpeedAbsorptionPower = 0f;
            PhysProperties.GodLimbProperties.Flammability = 0f;
            PhysProperties.GodLimbProperties.Conducting = false;
            PhysProperties.GodLimbProperties.HardImpact = PhysProperties.GodLimbProperties.SoftImpact;
            */

            PowerMenuKeybindInput.Ensure();
            QuickTransformKeybindInput.Ensure();
            CombatTransformationWheel.Ensure();
            CombatAbilityWheel.Ensure();
        }


        public static Sprite Null = ModAPI.LoadSprite("Nothing.png");

        // GUI



        public static Sprite PIconTemplate = ModAPI.LoadSprite("Assets/Sprites/Menus/Powers/IconTemplate.png");

        // Effects

        public static Sprite TeleportEffect_1 = ModAPI.LoadSprite("Assets/VFX/Sprites/TeleportEffect_1.png");
        public static Sprite TeleportEffect_2 = ModAPI.LoadSprite("Assets/VFX/Sprites/TeleportEffect_2.png");
        public static Sprite TeleportEffect_3 = ModAPI.LoadSprite("Assets/VFX/Sprites/TeleportEffect_3.png");
        public static Sprite TeleportEffect_4 = ModAPI.LoadSprite("Assets/VFX/Sprites/TeleportEffect_4.png");
        public static Sprite TeleportEffect_5 = ModAPI.LoadSprite("Assets/VFX/Sprites/TeleportEffect_5.png");

        // Attacks

        public static Texture2D EnergyWave_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/EnergyWave_Beam_Middle.png");

        public static Texture2D Kamehameha_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/Kamehameha_Beam_Middle.png");

        public static Texture2D FullForceKamehameha_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/FullForceKamehameha_Beam_Middle.png");

        public static Texture2D FinalFlash_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/FinalFlash_Beam_Middle.png");

        public static Texture2D GalickGun_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/GalickGun_Beam_Middle.png");

        public static Texture2D LastEmperor_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/LastEmperor_Beam_Middle.png");

        public static Texture2D GiganticFullBlast_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/AssetBundles/Powers/Ki/GiganticFullBlast_Beam_Middle.png");

        public static Texture2D StraightBeam_Texture = ModAPI.LoadTexture("Assets/VFX/Sprites/StraightBeam_Texture.png");

        public static Texture2D SpecialBeamCannon_Beam_Middle = ModAPI.LoadTexture("Assets/VFX/Sprites/StraightBeam_Texture.png");


        public static Sprite FinalExplosion_Texture = ModAPI.LoadSprite("Assets/VFX/Sprites/FinalExplosion_Texture.png");
        public static Sprite FinalExplosion_OuterTexture = ModAPI.LoadSprite("Assets/VFX/Sprites/FinalExplosion_OuterTexture.png");

        public static Sprite KiShield_Sprite = ModAPI.LoadSprite("Assets/VFX/Sprites/KiShield.png");

        public static Sprite KiBlast_Sprite = ModAPI.LoadSprite("Assets/VFX/Sprites/KiBlast.png");
        public static Sprite DenseKiBlast_Sprite = ModAPI.LoadSprite("Assets/VFX/Sprites/DenseKiBlast.png");



        public static AudioClip SubcategoryOpen = ModAPI.LoadSound("Assets/SFX/Misc/blip.wav");

        public static List<AudioClip> SenzuBeanEatingSounds = new List<AudioClip>
        {
            ModAPI.LoadSound("Assets/SFX/SenzuBean/SenzuBean_Sound1.mp3"),
            ModAPI.LoadSound("Assets/SFX/SenzuBean/SenzuBean_Sound2.mp3"),
            ModAPI.LoadSound("Assets/SFX/SenzuBean/SenzuBean_Sound3.mp3")
            };

        public static AudioClip Aura_Kaioken_Start = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Kaioken_Start.mp3");
        public static AudioClip Aura_Kaioken_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Kaioken_Loop.wav");
        public static AudioClip Aura_Kaioken_End = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Kaioken_End.mp3");

        public static AudioClip Aura_Burst_Basic = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Burst_Basic.mp3");

        public static AudioClip Aura_SSJ1_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_SSJ1_Loop.wav");
        public static AudioClip Aura_SSJ2_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_SSJ2_Loop.wav");
        public static AudioClip Aura_SSJGod_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_SSJGod_Loop.wav");
        public static AudioClip Aura_SSJBlue_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_SSJBlue_Loop.wav");
        public static AudioClip Aura_SSJBlueEvolution_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_SSJBlueEvolution_Loop.wav");
        public static AudioClip Aura_Frieza_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Frieza_Loop.wav");
        public static AudioClip Aura_GoldenFrieza_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_GoldenFrieza_Loop.wav");
        public static AudioClip Aura_Broly_Loop = ModAPI.LoadSound("Assets/SFX/Auras/Aura_Broly_Loop.wav");

        // Misc

        public static AudioClip Misc_IT = ModAPI.LoadSound("Assets/SFX/Misc/IT.mp3");

        public static AudioClip Misc_MetamoroFusion = ModAPI.LoadSound("Assets/SFX/Misc/MetamoroFusion.mp3");
        public static AudioClip Misc_Movement1 = ModAPI.LoadSound("Assets/SFX/Misc/Movement1.mp3");

        // Attacks

        public static AudioClip Attack_KiBlast_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Ki Blast/Fire.mp3");
        public static AudioClip Attack_KiBlast_Impact = ModAPI.LoadSound("Assets/SFX/Attacks/Ki Blast/Impact.mp3");

        public static AudioClip Attack_Kamehameha_Charging = ModAPI.LoadSound("Assets/SFX/Attacks/Kamehameha/Charge.mp3");
        public static AudioClip Attack_Kamehameha_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Kamehameha/Fire.mp3");
        public static AudioClip Attack_Kamehameha_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Kamehameha/Loop.wav");

        public static AudioClip Attack_FullForceKamehameha_Charging = ModAPI.LoadSound("Assets/SFX/Attacks/Full Force Kamehameha/Charge.mp3");
        public static AudioClip Attack_FullForceKamehameha_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Full Force Kamehameha/Fire.mp3");
        public static AudioClip Attack_FullForceKamehameha_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Full Force Kamehameha/Loop.wav");

        public static AudioClip Attack_FinalFlash_Charging = ModAPI.LoadSound("Assets/SFX/Attacks/Final Flash/Charge.wav");
        public static AudioClip Attack_FinalFlash_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Final Flash/Fire.mp3");
        public static AudioClip Attack_FinalFlash_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Final Flash/Loop.wav");

        public static AudioClip Attack_GalickGun_Charging = ModAPI.LoadSound("Assets/SFX/Attacks/Galick Gun/Charge.wav");
        public static AudioClip Attack_GalickGun_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Galick Gun/Fire.mp3");
        public static AudioClip Attack_GalickGun_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Galick Gun/Loop.wav");

        public static AudioClip Attack_DeathBeam_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Death Beam/Fire.mp3");
        public static AudioClip Attack_DeathBeam_Hit = ModAPI.LoadSound("Assets/SFX/Attacks/Death Beam/Hit.mp3");

        public static AudioClip Attack_FinalExplosion = ModAPI.LoadSound("Assets/SFX/Attacks/Final Explosion/Final Explosion Sound.mp3");

        public static AudioClip Attack_SolarFlare_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Solar Flare/Fire.mp3");
        public static AudioClip Attack_SolarFlare_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Solar Flare/Loop.wav");

        public static AudioClip Attack_StardustBreaker_Charging = ModAPI.LoadSound("Assets/SFX/Attacks/Stardust Breaker/Charge.mp3");
        public static AudioClip Attack_StardustBreaker_Loop = ModAPI.LoadSound("Assets/SFX/Attacks/Stardust Breaker/Loop.wav");

        public static AudioClip Attack_MeteorExplosion_Fire = ModAPI.LoadSound("Assets/SFX/Attacks/Meteor Explosion/Fire.mp3");
        public static AudioClip Attack_MeteorExplosion_Fire_Extended = ModAPI.LoadSound("Assets/SFX/Attacks/Meteor Explosion/Fire_Extended.mp3");

        // Voices

        public static AudioClip Voice_Goku_KaMeHaMe = ModAPI.LoadSound("Assets/SFX/Voices/Goku/KA-ME-HA-ME.mp3");
        public static AudioClip Voice_Goku_HA = ModAPI.LoadSound("Assets/SFX/Voices/Goku/HAAA.mp3");

        public static AudioClip Voice_Vegeta_FINALFLASH = ModAPI.LoadSound("Assets/SFX/Voices/Vegeta/FINAL-FLASH.mp3");
        public static AudioClip Voice_Vegeta_GALICKGUN = ModAPI.LoadSound("Assets/SFX/Voices/Vegeta/GALICK-GUN.mp3");

        public static AudioClip Voice_Vegeta_FinalExplosion = ModAPI.LoadSound("Assets/SFX/Attacks/Final Explosion/Final Explosion Voice.mp3");

        public static AudioClip Voice_Goku_SolarFlare = ModAPI.LoadSound("Assets/SFX/Voices/Goku/SolarFlare.mp3");
        public static AudioClip Voice_Krillin_SolarFlare = ModAPI.LoadSound("Assets/SFX/Voices/Krillin/SolarFlare.mp3");
        public static AudioClip Voice_Tien_SolarFlare = ModAPI.LoadSound("Assets/SFX/Voices/Tien/SolarFlare.mp3");

        public static AudioClip Voice_Gogeta_KaMeHaMe = ModAPI.LoadSound("Assets/SFX/Voices/Gogeta/KA-ME-HA-ME.mp3");
        public static AudioClip Voice_Gogeta_HA = ModAPI.LoadSound("Assets/SFX/Voices/Gogeta/HAAAAAA.mp3");
        public static AudioClip Voice_Gogeta_MeteorExplosion = ModAPI.LoadSound("Assets/SFX/Voices/Gogeta/Meteor Explosion.mp3");

        public static AudioClip Voice_Fusion_GokuXVegeta = ModAPI.LoadSound("Assets/SFX/Voices/Fusion_GokuXVegeta.mp3");


        public static List<string> BaseConsideredForms = new List<string>
        {
            "Base",
            "FirstForm",
            "SecondForm",
            "ThirdForm",
            "FinalForm",
            "PSSJ",
            "Ikari"
            };

        public static string ModTag = "[DBPU] ";
        public static string SecretTag = "<color=yellow>[Secret] <color=white>";
        public static string ModTab = "Dragon Ball <color=yellow>Singularity<color=white>";

        public static string VehiclesTag = "00-";
        public static string CosmeticTag = "01-";
        public static string ItemsTag = "02-";
        public static string ZFighterTag = "03-";
        public static string HumanTag = "04-";
        public static string VillanTag = "05-";
        public static string UniqueTag = "DBPU9001";

        public static string ModFolder
        {
            get
            {
                try
                {
                    string meta = ModAPI.Metadata.MetaLocation;
                    if (!string.IsNullOrEmpty(meta))
                        return meta;
                }
                catch { }
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", "Dragon Ball Singularity"));
            }
        }

        public static List<Subcategory> Subcategories = new List<Subcategory>();

        static string CharacterSpriteRoot(string characterName) => "Assets/Sprites/Characters/" + characterName + "/";

        static bool CharacterSpriteExists(string modRelativePath)
        {
            return File.Exists(Path.Combine(ModFolder, modRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static Sprite LoadCharacterFormIcon(string characterName, string fileName, Sprite fallback = null)
        {
            string iconPath = CharacterSpriteRoot(characterName) + "Icons/" + fileName;
            if (!CharacterSpriteExists(iconPath))
                return fallback;
            return ModAPI.LoadSprite(iconPath);
        }

        static Sprite LoadCharacterFormIllustration(string characterName, string fileName, Sprite fallback = null)
        {
            string illustrationPath = CharacterSpriteRoot(characterName) + "Illustrations/" + fileName;
            if (CharacterSpriteExists(illustrationPath))
                return ModAPI.LoadSprite(illustrationPath);
            return LoadCharacterFormIcon(characterName, fileName, fallback);
        }

        static Dictionary<string, List<Sprite>> LoadGohanAgeDemonClothesSkins(string prefix)
        {
            var dict = new Dictionary<string, List<Sprite>>
            {
                { "Damage0", LoadCharacterLimbSprites(prefix + "DemonClothesSkin/") }
            };
            for (int i = 1; i <= 4; i++)
                dict["Damage" + i] = LoadCharacterLimbSprites(prefix + "DemonClothesDamage" + i + "/");
            return dict;
        }

        static Dictionary<string, List<Sprite>> LoadGohanAgeTurtleGiSkins(string prefix)
        {
            var dict = new Dictionary<string, List<Sprite>>
            {
                { "Damage0", LoadCharacterLimbSprites(prefix + "TurtleGI/") }
            };
            for (int i = 1; i <= 5; i++)
                dict["Damage" + i] = LoadCharacterLimbSprites(prefix + "TurtleGIDamage" + i + "/");
            return dict;
        }

        // Great Ape body setup mirrors Evangelion Eva standing (Metal props, foot-heavy mass, autoConfigure joints).
        public const float OozaruBodyScale = 0.6f;
        public const float OozaruBaseMassFactor = 104f;
        public const float OozaruStrengthPerMass = 120f;
        public const float OozaruBreakingThresholdMult = 9000000f;

        public static float GetOozaruLimbMassFactor(int limbIndex)
        {
            if (limbIndex == 0) return 0.55f;
            if (limbIndex >= 1 && limbIndex <= 3) return 0.8f;
            if (limbIndex == 6 || limbIndex == 9) return 2.5f;
            if (limbIndex == 4 || limbIndex == 5 || limbIndex == 7 || limbIndex == 8) return 1.72f;
            if (limbIndex >= 10 && limbIndex <= 13) return 1.08f;
            return 1f;
        }

        public static void SetOozaruJoint(
            LimbBehaviour limb,
            Vector2 anchor,
            bool useLimits,
            float minLimit,
            float maxLimit)
        {
            if (limb == null) return;
            HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
            if (joint == null) return;
            joint.anchor = anchor;
            joint.autoConfigureConnectedAnchor = true;
            joint.useLimits = useLimits;
            if (useLimits)
                joint.limits = new JointAngleLimits2D { min = minLimit, max = maxLimit };
        }

        public static void IgnoreOozaruSelfCollisions(GameObject instance)
        {
            Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                    Physics2D.IgnoreCollision(colliders[i], colliders[j], true);
            }
        }
    }
}
