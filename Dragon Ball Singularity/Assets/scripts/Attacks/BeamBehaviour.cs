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
    [DefaultExecutionOrder(50)]
    public class BeamBehaviour : MonoBehaviour
    {
        public MultipliersAndStuffs Multipliers;
        public KiSystem KiSys;

        public AudioSource audioSource;
        public AudioSource BeamAudio;
        public bool CanSetVolume = true;

        public GameObject StartBeam;
        public LineRenderer MiddleBeam;
        public GameObject EndBeam;
        public Transform UsedArm;
        public string BeamType = "ArmBeam";

        public bool Active = false;
        public bool Charging = false;
        public bool FullCharged = false;
        public bool Firing = false;
        public bool StillActive = false;
        public KiShield ShieldBarrier;
        public bool BeamSplitConsumed = false;
        public bool Clashing = false;
        public BeamBody Body;
        public bool HeldByFighter = false;
        public BeamBehaviour ClashOpponent;
        public BeamBehaviour MergeHost;
        public List<BeamBehaviour> MergedBeams = new List<BeamBehaviour>();
        public bool MergeVisualActive = false;
        public float MergeWidthBonus = 0f;
        public float Charge = 0f;
        public float RealDamage = 7500f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;
        public float CostPercent = 0.15f;
        public float DrainPercent = 0.020f;
        public float StrainRateMultiplier = 1f;

        public string Character = "Human";

        public float Damage = 0f;
        public float Width = 2f;
        public float MinCharge = 0.2f;
        public float MaxCharge = 1f;
        public float ChargeTime = 5f;
        public float Speed = 50f;
        public float realSpeed = 50f;
        public float HoldingTime = 6f;
        public float FinalHoldingTime = 6f;
        public float TimeHolding = 0f;

        public float OriginalSpeed = 50f;
        private bool underWater = false;

        public float Distance = 0f;
        public float StartDistance = 0f;
        public float StartOffSet = 0.15f;
        public float ExtraWidth = 0.2f;
        public float WidthEndLoss = 4f;
        public float Brightness = 0.14f;
        public float Opacity = 0f;
        public float OpacityEndMulti = 1f;

        public Vector3 StartBeamPosition;
        public Vector3 EndBeamPosition;

        public Vector3 BeamDirection;

        bool IsGiganticRoarStyleBeam => EffectID == "Gigantic Roar" || EffectID == "Max Cannon";
        bool IsKamehamehaStyleBeam => EffectID == "Kamehameha" || EffectID == "Rose Kamehameha";
        static readonly Color RoseKamehamehaTint = new Color(1f, 0.4f, 0.78f);

        Vector3 GetMaxCannonMouthOffset(Transform arm)
        {
            float inverter = (transform.localScale.x > 0) ? 1f : -1f;
            float m = DBPU.Beam_SizeMultiplier;
            return (arm.right * (0.7f * inverter * m)) + (-arm.up * (1.55f * inverter * m));
        }

        public float FlollowStartDistance = 0f;
        public float MiddleOffSet = 2f;
        public float MiddleMinusDistance = 1.5f;

        public string EffectID;

        public bool SbcBlockedThisFire = false;

        private GameObject activeChargeVFX;
        private float beamCostSavedRegen;
        private bool beamCostSuppressesRegen;
        private GameObject activeChargeAuraVFX;

        static readonly Color SpecialBeamYellow = new Color(1f, 0.78f, 0.18f);
        static readonly Color SpecialBeamPink = new Color(1f, 0.34f, 0.72f);
        static readonly Color SpecialBeamChargeOrb = new Color(1f, 0.9f, 0.12f);
        static readonly Color SpecialBeamChargeStream = new Color(0.18f, 0.5f, 1f);
        static readonly Color BeerusEnergyWaveRgb = new Color(0.5647f, 0.3059f, 1f);
        static readonly Color BeerusEnergyWaveParticle = new Color(0.5647f, 0.3059f, 1f, 0.1f);
        static readonly Color EnergyWaveParticle = new Color(1f, 0.9f, 0f, 0.1f);

        bool IsBeerusEnergyWave => EffectID == "Energy Wave" && Character == "Beerus";

        /*
        public float Damage { get; set; }
        public float Width { get; set; }
        public float MinCharge { get; set; } 
        public float MaxCharge { get; set; }
        public float ChargeTime { get; set; }
        */

        private void OnDestroy()
        {
            CancelBeamState();
        }

        public void CancelBeamState()
        {
            StopAllCoroutines();
            if (beamCostSuppressesRegen && KiSys != null && KiSys.baseRegen == 0f)
                KiSys.baseRegen = beamCostSavedRegen;
            beamCostSuppressesRegen = false;

            if (activeChargeVFX != null)
            {
                Destroy(activeChargeVFX);
                activeChargeVFX = null;
            }

            if (activeChargeAuraVFX != null)
            {
                Destroy(activeChargeAuraVFX);
                activeChargeAuraVFX = null;
            }

            Charging = false;
            Firing = false;
            FullCharged = false;
            StillActive = false;
            Clashing = false;
            HeldByFighter = false;
            ClashOpponent = null;
            MergeHost = null;
            if (MergedBeams != null) MergedBeams.Clear();
            MergeWidthBonus = 0f;
            SbcBlockedThisFire = false;
            Charge = 0f;
            Distance = 0f;
            Transform cancelArm = UsedArm;
            UsedArm = null;

            if (StartBeam != null)
            {
                Destroy(StartBeam);
                StartBeam = null;
            }

            if (EndBeam != null)
            {
                Destroy(EndBeam);
                EndBeam = null;
            }

            MiddleBeam = null;

            var person = GetComponent<PersonBehaviour>();
            if (person != null && person.Limbs != null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb == null || limb.gameObject == null) continue;
                    var freeze = limb.gameObject.GetComponent<FreezeBehaviour>();
                    if (freeze != null) Destroy(freeze);
                }
            }

            if (BeamAudio != null)
            {
                BeamAudio.Stop();
                BeamAudio.loop = false;
            }

            CanSetVolume = true;

            if (EffectID == "Max Cannon" && cancelArm != null)
            {
                try
                {
                    var scream = cancelArm.GetComponent<CellMaxScream>();
                    if (scream != null) scream.FadeOutWithBeam();
                }
                catch { }
            }
        }

        private void Awake()
        {
            audioSource = new GameObject("BeamAudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Head"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = DBPU.VoiceLineVolume;
            audioSource.maxDistance = 1000;

            BeamAudio = new GameObject("BeamAudioSource").AddComponent<AudioSource>();
            BeamAudio.transform.SetParent(transform.Find("Body/UpperBody"), true);
            Global global2 = FindObjectOfType<Global>();
            global2.AddAudioSource(BeamAudio, false);
            BeamAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            BeamAudio.outputAudioMixerGroup = global2.SoundEffects;
            BeamAudio.spatialBlend = 1;
            BeamAudio.volume = DBPU.PowersVolume;
            BeamAudio.maxDistance = 1000;
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 1500f;

            RegisterArmBeamTriggers();

            var head = transform.Find("Head");
            if (head != null)
            {
                var headLimb = head.GetComponent<LimbBehaviour>();
                if (headLimb != null)
                {
                    var trigger = head.gameObject.GetOrAddComponent<UseEventTrigger>();
                    Action previous = trigger.Action;
                    trigger.Action = () => HandleHeadBeamUse(headLimb, previous);
                }
            }
        }

        public void RegisterArmBeamTriggers()
        {
            var person = GetComponent<PersonBehaviour>();
            if (person == null) return;

            var registered = new HashSet<int>();

            void RegisterIfNew(Transform armTransform)
            {
                if (armTransform == null) return;
                int id = armTransform.GetInstanceID();
                if (registered.Contains(id)) return;
                registered.Add(id);
                RegisterLowerArmTrigger(armTransform);
            }

            RegisterIfNew(person.transform.Find("FrontArm/LowerArmFront"));
            RegisterIfNew(person.transform.Find("BackArm/LowerArm"));

            foreach (var limb in person.Limbs)
            {
                if (!MCP_Utils.LimbCanUsePowers(limb) || !limb.name.Contains("LowerArm")) continue;
                RegisterIfNew(limb.transform);
            }
        }

        void RegisterLowerArmTrigger(Transform armTransform)
        {
            if (armTransform == null) return;

            var limb = armTransform.GetComponent<LimbBehaviour>();
            if (limb == null) return;

            var trigger = armTransform.gameObject.GetOrAddComponent<UseEventTrigger>();
            Action previous = trigger.Action;
            trigger.Action = () => HandleLowerArmBeamUse(limb, previous);
        }

        void HandleLowerArmBeamUse(LimbBehaviour limb, Action fallback)
        {
            if (!Active || BeamType != "ArmBeam" || !KiHandLoadout.LegacyAllowed(limb, EffectID))
            {
                fallback?.Invoke();
                return;
            }

            if (Charging == false && KiSys.currentKi < KiSys.maxKi * CostPercent) return;

            if (StillActive && EndBeam != null && Firing == false) return;

            if (Charging && UsedArm != null && UsedArm != limb.transform) return;

            if (limb.Health > 0f && Charging == false && Firing == false)
            {
                UsedArm = limb.transform;
                Charging = true;
                StartCoroutine(StartCharge(limb.transform));
            }

            else if (Charging == true && Firing == false && FullCharged == true && UsedArm == limb.transform)
            {
                Firing = true;
            }

            else if (Charging == false && Firing == true && FullCharged == false && UsedArm == limb.transform)
            {
                Firing = false;
            }
        }

        void HandleHeadBeamUse(LimbBehaviour limb, Action fallback)
        {
            if (!Active || BeamType != "MouthBeam")
            {
                fallback?.Invoke();
                return;
            }

            if (Charging == false && KiSys.currentKi < KiSys.maxKi * CostPercent) return;

            if (StillActive && EndBeam != null && Firing == false) return;

            if (Charging && UsedArm != null && UsedArm != limb.transform) return;

            if (limb.Health > 0f && Charging == false && Firing == false)
            {
                UsedArm = limb.transform;
                Charging = true;
                StartCoroutine(StartCharge(limb.transform));
            }

            else if (Charging == true && Firing == false && FullCharged == true && UsedArm == limb.transform)
            {
                Firing = true;
            }

            else if (Charging == false && Firing == true && FullCharged == false && UsedArm == limb.transform)
            {
                Firing = false;
            }
        }

        private void Update()
        {
            if (audioSource.volume != DBPU.VoiceLineVolume && CanSetVolume == true) audioSource.volume = DBPU.VoiceLineVolume;
            if (BeamAudio.volume != DBPU.PowersVolume && CanSetVolume == true) BeamAudio.volume = DBPU.PowersVolume;

            if (StillActive == true)
            {
                RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

                if (EffectID == "Special Beam Cannon" && UsedArm != null)
                    StartBeamPosition = GetSpecialBeamHandOrigin(UsedArm);

                if (Clashing == false && HeldByFighter == false) TimeHolding += 1f * Time.deltaTime;

                if (MergeHost != null && MergeHost.StillActive && MergeHost.Firing)
                {
                    SyncToMergeHost();
                }
                else if (EndBeam != null)
                {
                    if (!Clashing && !HeldByFighter)
                    {
                        if (IsUnderWater() && underWater == false)
                        {
                            realSpeed = Speed * 0.8f;
                        }

                        else if (!IsUnderWater() && underWater == true)
                        {
                            realSpeed = Speed;
                        }

                        Distance += (realSpeed * DBPU.Beam_SpeedMultiplier) * Time.deltaTime;
                    }

                    float shieldDistance = Distance;
                    if (Body != null && Body.Live) { if (Body.ClipShields(ref shieldDistance)) Distance = shieldDistance; }
                    else if (KiShield.ClipAttack(gameObject, GetInstanceID(), StartBeamPosition, BeamDirection, ref shieldDistance, 0, (double)RealDamage * 60, out ShieldBarrier)) Distance = shieldDistance;
                    EndBeam.transform.position = GetEndPosition();
                }

                if (MiddleBeam != null)
                {
                    if (EffectID == "Special Beam Cannon")
                    {
                        if (Opacity < 1f)
                            Opacity = 1f;
                        ApplySpecialBeamAlpha();
                    }
                    else if (Opacity < 1f)
                    {
                        if (Opacity < 0f)
                        {
                            Opacity += 8f * Time.deltaTime;
                        }

                        else
                        {
                            Opacity += 3.5f * Time.deltaTime;
                            if (Opacity > 1f) Opacity = 1f;
                            ApplyMiddleBeamBodyAlpha();
                        }
                    }

                    if (Body != null && Body.Live) { }
                    else if (EffectID == "Special Beam Cannon")
                        UpdateSpecialBeamMiddleGeometry();
                    else if (Distance > FlollowStartDistance && EndBeam != null)
                        MiddleBeam.SetPosition(1, EndBeam.transform.position + (BeamDirection * MiddleMinusDistance) + (BeamDirection * MiddleOffSet));

                    if (Firing == true)
                    {
                        if (MergeHost == null)
                            MiddlePartAnimation(EffectID);

                        if (KiSys.currentKi > (KiSys.maxKi * DrainPercent) * Time.deltaTime && DBPU.KiAttackCostMultiplier > 0f)
                            KiSys.currentKi -= (KiSys.maxKi * DrainPercent) * Time.deltaTime;

                        if (MergeHost == null && BeamType == "ArmBeam")
                        {
                            if (KiSys.StrainRate > 0f && DBPU.StrainGainMultiplier > 0f)
                            {
                                if (StrainRateMultiplier == 1f)
                                {
                                    StrainRateMultiplier = 4f * (Multipliers.KaiokenMultiplier / 4f);
                                    KiSys.StrainRateMultiplier *= StrainRateMultiplier;
                                }

                                if (KiSys.Strain == 1f) DamageArms((RealDamage * (Multipliers.KaiokenMultiplier * 6f)) * Time.deltaTime);
                            }

                            else if (StrainRateMultiplier != 1f && DBPU.StrainDamageMultiplier > 0f)
                            {
                                KiSys.StrainRateMultiplier /= StrainRateMultiplier;
                                StrainRateMultiplier = 1f;
                            }
                        }
                    }
                }

                if (StartBeam != null)
                {
                    BeamAudio.transform.position = GetSoundPosition();
                }
            }

            if (StillActive == true && Firing == false)
            {
                if (MergedBeams != null && MergedBeams.Count > 0 && EndBeam != null)
                {
                    BeamComponent comp = EndBeam.GetComponent<BeamComponent>();
                    if (comp != null) comp.ReleaseMergedBeams(this);
                }

                if (BeamType == "ArmBeam")
                {
                    if (StrainRateMultiplier != 1f)
                    {
                        KiSys.StrainRateMultiplier /= StrainRateMultiplier;
                        StrainRateMultiplier = 1f;
                    }
                }

                if (MiddleBeam != null && MiddleBeam.startWidth > 0f)
                {
                    if (EffectID == "Special Beam Cannon")
                    {
                        Opacity -= (9f * OpacityEndMulti) * Time.deltaTime;
                        ApplySpecialBeamAlpha();
                        MiddleBeam.startWidth -= (Width * WidthEndLoss) * Time.deltaTime;
                        MiddleBeam.endWidth -= (Width * WidthEndLoss) * Time.deltaTime;
                        if (SpecialBeamAlpha() <= 0.05f)
                        {
                            if (MiddleBeam != null) Destroy(MiddleBeam);
                            MiddleBeam = null;
                        }
                    }
                    else if (MiddleBeamBodyAlpha() > 0.01f)
                    {
                        Opacity -= (9f * OpacityEndMulti) * Time.deltaTime;
                        ApplyMiddleBeamBodyAlpha();
                        MiddleBeam.startWidth -= (Width * WidthEndLoss) * Time.deltaTime;
                        MiddleBeam.endWidth -= (Width * WidthEndLoss) * Time.deltaTime;
                    }
                }

                else
                {
                    if (MiddleBeam != null) Destroy(MiddleBeam);
                    MiddleBeam = null;
                }
            }
        }

        public bool IsUnderWater()
        {
            return Physics2D.OverlapCircle(EndBeam.transform.position, 0.15f, LayerMask.GetMask("Water"));
        }

        private IEnumerator StartCharge(Transform Arm)
        {
            Charging = true;
            Firing = false;
            Clashing = false;
            UsedArm = Arm;

            if (activeChargeVFX != null)
            {
                Destroy(activeChargeVFX);
                activeChargeVFX = null;
            }

            if (activeChargeAuraVFX != null)
            {
                Destroy(activeChargeAuraVFX);
                activeChargeAuraVFX = null;
            }

            BeamAudio.transform.position = transform.Find("Body/UpperBody").position;
            BeamAudio.transform.SetParent(transform.Find("Body/UpperBody"), false);

            if (BeamType == "MouthBeam")
            {
                if (EffectID == "Energy Wave")
                {
                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }

                else if (EffectID == "Gigantic Roar")
                {
                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }

                else if (EffectID == "Max Cannon")
                {
                    var scream = Arm.GetComponent<CellMaxScream>();
                    if (scream != null)
                        yield return scream.StartCoroutine(scream.PrepareForBeam());

                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }
            }

            else if (BeamType == "ArmBeam")
            {
                if (EffectID == "Energy Wave")
                {
                    CostPercent = 0.1f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }

                else if (IsKamehamehaStyleBeam)
                {
                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Goku")
                        {
                            audioSource.PlayOneShot(DBPU.Voice_Goku_KaMeHaMe);

                            yield return new WaitForSeconds(2.85f);
                        }

                        else BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Charging);
                    }

                    else BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Charging);

                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    activeChargeVFX = Instantiate(GetEffect(EffectID, "Charge"), Arm.position + (-Arm.up * 0.4f), Quaternion.identity);
                    activeChargeVFX.transform.SetParent(Arm);

                    foreach (Transform child in activeChargeVFX.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                        sp.sortingOrder += 10;
                    }

                    activeChargeVFX.transform.Find("Core").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    activeChargeVFX.transform.Find("Core (1)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                    if (EffectID == "Rose Kamehameha")
                        TintParticleSystemsUnder(activeChargeVFX.transform, RoseKamehamehaTint);

                    ChangePlayBackOfAllParticleSystems(activeChargeVFX.transform, 0.42f);

                    yield return new WaitForSeconds(7.15f);

                    FullCharged = true;

                    while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                    {
                        yield return null;
                    }

                    if (activeChargeVFX != null)
                    {
                        Destroy(activeChargeVFX);
                        activeChargeVFX = null;
                    }

                    if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BeamAudio));

                    else
                    {
                        FullCharged = false;
                        Charging = false;
                        UsedArm = null;
                    }

                    yield return null;
                }

                else if (EffectID == "Special Beam Cannon")
                {
                    BeamAudio.clip = DBPU.Attack_GalickGun_Charging;
                    BeamAudio.loop = true;
                    BeamAudio.Play();
                    BeamAudio.volume = 0f;

                    CostPercent = 0.2f;

                    StartCoroutine(KiCost());

                    activeChargeVFX = Instantiate(GetEffect(EffectID, "Charge"), Arm.position + (-Arm.up * 0.4f), Quaternion.identity);
                    activeChargeVFX.transform.SetParent(Arm);

                    foreach (Transform child in activeChargeVFX.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        if (sp != null)
                        {
                            sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                            sp.sortingOrder += 10;
                        }
                    }

                    var chargeCore = activeChargeVFX.transform.Find("Core");
                    if (chargeCore != null) chargeCore.gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    var chargeCore1 = activeChargeVFX.transform.Find("Core (1)");
                    if (chargeCore1 != null) chargeCore1.gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                    TintSpecialBeamCannonChargeParticles(activeChargeVFX.transform);
                    ChangePlayBackOfAllParticleSystems(activeChargeVFX.transform, 0.42f);

                    while (BeamAudio.volume < DBPU.PowersVolume)
                    {
                        BeamAudio.volume += 0.01f;
                        yield return new WaitForSeconds(0.0125f);
                    }

                    yield return new WaitForSeconds(5.5f);

                    FullCharged = true;

                    while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                    {
                        yield return null;
                    }

                    if (activeChargeVFX != null)
                    {
                        Destroy(activeChargeVFX);
                        activeChargeVFX = null;
                    }

                    if (activeChargeAuraVFX != null)
                    {
                        Destroy(activeChargeAuraVFX);
                        activeChargeAuraVFX = null;
                    }

                    if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BeamAudio));

                    else
                    {
                        FullCharged = false;
                        Charging = false;
                        UsedArm = null;
                    }

                    yield return null;
                }

                else if (EffectID == "Full Force Kamehameha")
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_FullForceKamehameha_Charging);

                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    GameObject ChargingAnimation = Instantiate(GetEffect(EffectID, "Charge"), Arm.position + (-Arm.up * 0.4f), Quaternion.identity);
                    ChargingAnimation.transform.SetParent(Arm);

                    foreach (Transform child in ChargingAnimation.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                        sp.sortingOrder += 10;
                    }

                    ChargingAnimation.transform.Find("Core").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    ChargingAnimation.transform.Find("Core (1)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                    ChangePlayBackOfAllParticleSystems(ChargingAnimation.transform, 0.25f);

                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Gogeta")
                        {
                            yield return new WaitForSeconds(1.5f);

                            audioSource.PlayOneShot(DBPU.Voice_Gogeta_KaMeHaMe);

                            yield return new WaitForSeconds(10f);
                        }

                        else yield return new WaitForSeconds(11.5f);
                    }

                    else yield return new WaitForSeconds(11.5f);

                    FullCharged = true;

                    while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                    {
                        yield return null;
                    }

                    Destroy(ChargingAnimation);

                    if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BeamAudio));

                    else
                    {
                        FullCharged = false;
                        Charging = false;
                    }

                    yield return null;
                }

                else if (EffectID == "Galick Gun")
                {
                    BeamAudio.clip = DBPU.Attack_GalickGun_Charging;
                    BeamAudio.loop = true;
                    BeamAudio.Play();
                    BeamAudio.volume = 0f;

                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    GameObject ChargingAnimation = Instantiate(GetEffect(EffectID, "Charge"), Arm.position + (-Arm.up * 0.4f), Quaternion.identity);
                    ChargingAnimation.transform.SetParent(Arm);

                    foreach (Transform child in ChargingAnimation.transform)
                    {
                        var ps = child.gameObject.GetComponent<ParticleSystem>();
                        ps.startColor = new Color(ps.startColor.r, ps.startColor.g, ps.startColor.b, 0.1f);
                    }

                    foreach (Transform child in ChargingAnimation.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                        sp.sortingOrder += 10;
                    }

                    ChangePlayBackOfAllParticleSystems(ChargingAnimation.transform, 0.42f);

                    GameObject ChargingAura = Instantiate(GetEffect(EffectID, "Aura"), GetComponent<PersonBehaviour>().Limbs[2].transform.position, Quaternion.identity);
                    ChargingAura.transform.SetParent(GetComponent<PersonBehaviour>().Limbs[2].transform);
                    ChargingAura.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

                    foreach (Transform child in ChargingAura.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = "Bottom";
                        sp.sortingOrder += 10;
                    }

                    ChargingAura.transform.Find("Lightning_2 (1)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    ChargingAura.transform.Find("fx_Lightning (1)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                    ChangePlayBackOfAllParticleSystems(ChargingAnimation.transform, 0.42f);

                    while (BeamAudio.volume < DBPU.PowersVolume)
                    {
                        BeamAudio.volume += 0.01f;

                        yield return new WaitForSeconds(0.0125f);
                    }

                    yield return new WaitForSeconds(4.15f);

                    FullCharged = true;

                    while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                    {
                        yield return null;
                    }

                    Destroy(ChargingAnimation);
                    Destroy(ChargingAura);

                    if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BeamAudio));

                    else
                    {
                        FullCharged = false;
                        Charging = false;
                    }

                    yield return null;
                }

                else if (EffectID == "Final Flash")
                {
                    BeamAudio.clip = DBPU.Attack_FinalFlash_Charging;
                    BeamAudio.loop = true;
                    BeamAudio.Play();
                    BeamAudio.volume = 0f;

                    CostPercent = 0.4f;

                    StartCoroutine(KiCost());

                    GameObject ChargingAnimation = Instantiate(GetEffect(EffectID, "Charge"), Arm.position + (-Arm.up * 0.4f), Quaternion.identity);
                    ChargingAnimation.transform.SetParent(Arm);
                    ChargingAnimation.transform.Find("start_pointCore").gameObject.GetComponent<ParticleSystem>().startColor = new Color(ChargingAnimation.transform.Find("start_pointCore").gameObject.GetComponent<ParticleSystem>().startColor.r, ChargingAnimation.transform.Find("start_pointCore").gameObject.GetComponent<ParticleSystem>().startColor.g, ChargingAnimation.transform.Find("start_pointCore").gameObject.GetComponent<ParticleSystem>().startColor.b, 0.01f);
                    ChargingAnimation.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(ChargingAnimation.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystem>().startColor.r, ChargingAnimation.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystem>().startColor.g, ChargingAnimation.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystem>().startColor.b, 0.01f);

                    foreach (Transform child in ChargingAnimation.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                        sp.sortingOrder += 10;
                    }

                    ChangePlayBackOfAllParticleSystems(ChargingAnimation.transform, 0.42f);

                    while (BeamAudio.volume < DBPU.PowersVolume)
                    {
                        BeamAudio.volume += 0.01f;

                        yield return new WaitForSeconds(0.0125f);
                    }

                    yield return new WaitForSeconds(4.15f);

                    FullCharged = true;

                    while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                    {
                        yield return null;
                    }

                    Destroy(ChargingAnimation);

                    if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BeamAudio));

                    else
                    {
                        FullCharged = false;
                        Charging = false;
                    }

                    yield return null;
                }

                else if (EffectID == "Last Emperor")
                {
                    CostPercent = 0.15f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }

                else if (EffectID == "Gigantic Full Blast")
                {
                    CostPercent = 0.2f;

                    StartCoroutine(KiCost());

                    FullCharged = true;
                    Firing = true;

                    StartCoroutine(Release(Arm, BeamAudio));

                    yield return null;
                }
            }

            yield return null;
        }

        private IEnumerator KiCost()
        {
            if (DBPU.KiAttackCostMultiplier == 0f) yield break;

            float OGKiDrain = KiSys.currentKiDrainRate;
            float KiRegen = KiSys.baseRegen;

            beamCostSavedRegen = KiRegen;
            beamCostSuppressesRegen = OGKiDrain != 0f;
            if (beamCostSuppressesRegen) KiSys.baseRegen = 0f;

            float FinalKi = KiSys.currentKi - (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier));
            float CurrentKi = KiSys.currentKi;

            while (CurrentKi > FinalKi)
            {
                KiSys.currentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * Time.deltaTime;
                CurrentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;
            beamCostSuppressesRegen = false;

            yield return null;
        }

        private IEnumerator Release(Transform Arm, AudioSource BeamAudio)
        {
            FullCharged = false;
            Firing = true;
            Charging = false;

            WidthEndLoss = 4f;
            OpacityEndMulti = 1f;

            BeamAudio.transform.SetParent(transform.Find("Body/UpperBody"), true);

            if (BeamType == "MouthBeam")
            {
                StartBeamPosition = Arm.position + (Arm.right * 0.25f);
                if (EffectID == "Max Cannon")
                    StartBeamPosition += GetMaxCannonMouthOffset(Arm);

                if (EffectID == "Energy Wave")
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    float Inverter = (transform.localScale.x > 0) ? 1f : -1f;

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0.85f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 8000f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (Arm.right * (10.1f * Inverter) * DBPU.Beam_SizeMultiplier) + (-Arm.up * (0.7f * Inverter) * DBPU.Beam_SizeMultiplier), (transform.localScale.x > 0) ? Arm.rotation : Arm.rotation * Quaternion.Euler(0, 0, 180));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;
                    if (IsBeerusEnergyWave) Brightness = 0.45f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    Color energyWaveRgb = EnergyWaveBeamRgb();
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(energyWaveRgb, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (Arm.right * (1f * Inverter) * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (Arm.right * (1.01f * Inverter) * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.right * (10f * Inverter)), (transform.localScale.x > 0) ? Arm.rotation * Quaternion.Euler(0, 0, 180) : Arm.rotation);
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = Arm.transform.right;

                    ApplyEnergyWaveVFXColors(StartBeam.transform, EndBeam.transform);

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (IsGiganticRoarStyleBeam)
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    float Inverter = (transform.localScale.x > 0) ? 1f : -1f;
                    Vector3 mouthOrigin = Arm.position;
                    if (EffectID == "Max Cannon")
                        mouthOrigin += GetMaxCannonMouthOffset(Arm);

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0.85f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 2500f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), mouthOrigin + (Arm.right * (10.1f * Inverter) * DBPU.Beam_SizeMultiplier) + (-Arm.up * (0.7f * Inverter) * DBPU.Beam_SizeMultiplier), (transform.localScale.x > 0) ? Arm.rotation : Arm.rotation * Quaternion.Euler(0, 0, 180));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, mouthOrigin + (Arm.right * (1f * Inverter) * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, mouthOrigin + (Arm.right * (1.01f * Inverter) * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), mouthOrigin + (Arm.right * (10f * Inverter)), (transform.localScale.x > 0) ? Arm.rotation * Quaternion.Euler(0, 0, 180) : Arm.rotation);
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = Arm.transform.right;

                    StartBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_S").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_L").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere Noise").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    EndBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    if (EffectID == "Max Cannon")
                    {
                        var scream = Arm.GetComponent<CellMaxScream>();
                        if (scream != null)
                            scream.FadeOutWithBeam();
                    }

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else
                {
                    yield return null;
                }
            }

            else if (BeamType == "ArmBeam")
            {
                StartBeamPosition = Arm.position + (-Arm.up * 0.35f);

                if (EffectID == "Energy Wave")
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 8000f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 10.1f * DBPU.Beam_SizeMultiplier) + (-Arm.right * 0.7f * DBPU.Beam_SizeMultiplier), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;
                    if (IsBeerusEnergyWave) Brightness = 0.45f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    Color energyWaveRgb = EnergyWaveBeamRgb();
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(energyWaveRgb, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 1f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 1.01f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 10f) + (Arm.right * (0.9f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    ApplyEnergyWaveVFXColors(StartBeam.transform, EndBeam.transform);

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (IsKamehamehaStyleBeam)
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Goku") audioSource.PlayOneShot(DBPU.Voice_Goku_HA);
                    }

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 2000f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 10.1f * DBPU.Beam_SizeMultiplier) + (-Arm.right * 0.7f * DBPU.Beam_SizeMultiplier), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    if (EffectID == "Rose Kamehameha")
                        MiddleBeam.material.color = MakeMiddleBeamBodyColor(RoseKamehamehaTint, Brightness, 0f);
                    else
                        MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 1f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 1.01f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 10f) + (Arm.right * (0.9f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    if (EffectID == "Rose Kamehameha")
                    {
                        TintParticleSystemsUnder(StartBeam.transform, RoseKamehamehaTint);
                        TintParticleSystemsUnder(EndBeam.transform, RoseKamehamehaTint);
                    }

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Full Force Kamehameha")
                {
                    WidthEndLoss = 1.95f;
                    OpacityEndMulti = 0.6f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Gogeta")
                        {
                            audioSource.PlayOneShot(DBPU.Voice_Gogeta_HA);

                            yield return new WaitForSeconds(1.25f);
                        }
                    }

                    BeamAudio.PlayOneShot(DBPU.Attack_FullForceKamehameha_Fire);

                    OriginalSpeed = 75f;

                    Width = 6.8f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = 2.5f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -2.1f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 4f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 1000f;
                    realSpeed = 75f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 0.8f * DBPU.Beam_SizeMultiplier)/* + (-Arm.right * 0.7f * DBPU.Beam_SizeMultiplier)*/, Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(24f * DBPU.Beam_SizeMultiplier, 24f * DBPU.Beam_SizeMultiplier, 24f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);
                    StartBeam.transform.GetChild(4).gameObject.GetComponent<Renderer>().sortingLayerName = "Background";
                    StartBeam.transform.GetChild(4).gameObject.GetComponent<Renderer>().sortingOrder = 0;
                    StartBeam.transform.GetChild(5).gameObject.GetComponent<Renderer>().sortingLayerName = "Background";
                    StartBeam.transform.GetChild(5).gameObject.GetComponent<Renderer>().sortingOrder = 0;

                    Brightness = 0.22f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.1f, 1f);
                    MiddleBeam.numCapVertices = 12;
                    MiddleBeam.numCornerVertices = 12;
                    //MiddleBeam.textureMode = LineTextureMode.Stretch;
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 1.1f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 1.11f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 10f)/* + (Arm.right * (0.9f * DBPU.Beam_SizeMultiplier))*/, Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(5, 5, 5);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    EndBeam.transform.GetChild(3).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(2f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StillActive = true;

                    float scale = 0f;

                    while (scale < 1f)
                    {
                        scale += 0.1f;

                        StartBeam.transform.localScale = new Vector3(scale * 24f * DBPU.Beam_SizeMultiplier, scale * 24f * DBPU.Beam_SizeMultiplier, scale * 24f * DBPU.Beam_SizeMultiplier);
                        EndBeam.transform.localScale = new Vector3(scale * 2f * DBPU.Beam_SizeMultiplier, scale * 1f * DBPU.Beam_SizeMultiplier, scale * 1f * DBPU.Beam_SizeMultiplier);

                        yield return new WaitForSeconds(0.025f);
                    }

                    realSpeed = 120f;

                    yield return new WaitForSeconds(0.8f);

                    while (scale < 2f)
                    {
                        scale += 0.1f;

                        StartBeam.transform.localScale = new Vector3(24f * DBPU.Beam_SizeMultiplier, scale * 24f * DBPU.Beam_SizeMultiplier, 24f * DBPU.Beam_SizeMultiplier);
                        EndBeam.transform.localScale = new Vector3(scale * 2f * DBPU.Beam_SizeMultiplier, scale * 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                        MiddleBeam.startWidth = (7.4f * DBPU.Beam_SizeMultiplier) * scale;
                        MiddleBeam.endWidth = (7.4f * DBPU.Beam_SizeMultiplier) * scale;
                        MiddleMinusDistance = (-2.1f * DBPU.Beam_SizeMultiplier) - (scale * 2f);
                        Width = (6.8f * DBPU.Beam_SizeMultiplier) * scale;

                        CameraShakeBehaviour.main.Shake(2f, audioSource.transform.position);

                        yield return new WaitForSeconds(0.025f);
                    }

                    yield return new WaitForSeconds(1.2f);

                    BeamAudio.clip = DBPU.Attack_FullForceKamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    //StopAllParticleSystems(StartBeam.transform);
                    //StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    float scale1 = 48f;
                    float scale2 = 2f;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.05f;
                        scale1 -= 2.4f;
                        scale2 -= 0.1f;

                        StartBeam.transform.localScale = new Vector3(24f * DBPU.Beam_SizeMultiplier, scale1 * DBPU.Beam_SizeMultiplier, 24f * DBPU.Beam_SizeMultiplier);
                        EndBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, scale2 * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Special Beam Cannon")
                {
                    BeamAudio.Stop();

                    BeamPhysics.PinFiringLimb(this, Arm);

                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    OriginalSpeed = 50f;

                    Width = 0.14f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 14f;
                    StartDistance = -1.5f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.18f;
                    MiddleMinusDistance = -0.15f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -1.2f;
                    FlollowStartDistance = 2f;
                    Distance = 0f;
                    Opacity = 1f;
                    SbcBlockedThisFire = false;
                    StartBeamPosition = GetSpecialBeamHandOrigin(Arm);

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 900f;
                    realSpeed = 50f;
                    Speed = 50f;

                    DrainPercent = 0.025f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), GetSpecialBeamHandOrigin(Arm), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    StartBeam.transform.SetParent(Arm);
                    GetAllChildrenRecursive(StartBeam.transform);
                    StartBeam.transform.localScale = new Vector3(1.85f * DBPU.Beam_SizeMultiplier, 1.85f * DBPU.Beam_SizeMultiplier, 1.85f * DBPU.Beam_SizeMultiplier);
                    TintSpecialBeamCannonStartVFX(StartBeam.transform);

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    SetupSpecialBeamLineRenderer(MiddleBeam);

                    MiddleBeam.startWidth = Width;
                    MiddleBeam.endWidth = Width;

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 6f) + (Arm.right * (0.35f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                    if (EndBeam.transform.childCount > 1) EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    GetAllChildrenRecursive(EndBeam.transform);
                    EndBeam.transform.localScale = new Vector3(0.55f * DBPU.Beam_SizeMultiplier, 0.55f * DBPU.Beam_SizeMultiplier, 0.55f * DBPU.Beam_SizeMultiplier);
                    TintSpecialBeamCannonParticles(EndBeam.transform, false);

                    BeamDirection = -EndBeam.transform.right;
                    ApplySpecialBeamAlpha();
                    UpdateSpecialBeamMiddleGeometry();

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(1.2f * DBPU.Beam_SizeMultiplier, 1.2f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(0.6f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    if (EndBeam != null) Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;
                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.45f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Galick Gun")
                {
                    BeamAudio.Stop();

                    BeamPhysics.PinFiringLimb(this, Arm);

                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Vegeta")
                        {
                            audioSource.PlayOneShot(DBPU.Voice_Vegeta_GALICKGUN);

                            yield return new WaitForSeconds(1.8f);
                        }
                    }

                    OriginalSpeed = 50f;

                    BeamAudio.PlayOneShot(DBPU.Attack_GalickGun_Fire);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = 0f;
                    StartOffSet = 0f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0f;
                    Opacity = 0.25f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 2000f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (Arm.up * 0.5f * DBPU.Beam_SizeMultiplier), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(25f, 25f, 1f);
                    //StartBeam.transform.SetParent(Arm);
                    GetAllChildrenRecursiveToBack(StartBeam.transform);
                    StartBeam.transform.Find("lighrning (3)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    StartBeam.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                    StartBeam.transform.Find("start_pointCore (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.5647f, 0.3059f, 1f, 0.002f);

                    Brightness = 0.18f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0.25f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position);
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 0.01f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.right * 0.85f), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    foreach (Transform child in EndBeam.transform)
                    {
                        var ps = child.gameObject.GetComponent<ParticleSystem>();
                        ps.startColor = new Color(ps.startColor.r, ps.startColor.g, ps.startColor.b, 0.25f);
                    }

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_GalickGun_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Final Flash")
                {
                    BeamAudio.Stop();

                    BeamPhysics.PinFiringLimb(this, Arm);

                    if (DBPU.VoiceLines)
                    {
                        if (Character == "Vegeta")
                        {
                            audioSource.PlayOneShot(DBPU.Voice_Vegeta_FINALFLASH);

                            yield return new WaitForSeconds(1.8f);
                        }
                    }

                    OriginalSpeed = 100f;

                    BeamAudio.PlayOneShot(DBPU.Attack_FinalFlash_Fire);

                    Width = 6f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = 0f;
                    StartOffSet = 0f;
                    MiddleMinusDistance = -8f * DBPU.Beam_SizeMultiplier;
                    FlollowStartDistance = 0f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 1200f;
                    realSpeed = 100f;

                    DrainPercent = 0.03f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 0.5f), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    StartBeam.transform.localScale = new Vector3(1.7f * DBPU.Beam_SizeMultiplier, 1.7f * DBPU.Beam_SizeMultiplier, 1.7f * DBPU.Beam_SizeMultiplier);
                    Destroy(StartBeam.transform.Find("Effect_SmashWhite_Mild").gameObject);
                    Destroy(StartBeam.transform.Find("Effect_SmashWhite_Sharp").gameObject);
                    Destroy(StartBeam.transform.Find("Surround Wave (1)").gameObject);

                    foreach (Transform child in StartBeam.transform)
                    {
                        var ps = child.gameObject.GetComponent<ParticleSystem>();
                        ps.startColor = new Color(ps.startColor.r, ps.startColor.g, ps.startColor.b, 0.25f);
                    }

                    ChangePlayBackOfAllParticleSystems(StartBeam.transform, 1.5f);

                    Brightness = 0.2f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.15f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 4.55f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 4.5501f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (-Arm.up * (0.75f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    GetAllChildrenRecursive(EndBeam.transform);
                    Destroy(EndBeam.transform.Find("Effect_SmashWhite_Mild").gameObject);
                    Destroy(EndBeam.transform.Find("Effect_SmashWhite_Sharp").gameObject);
                    Destroy(EndBeam.transform.Find("Surround Wave (1)").gameObject);

                    foreach (Transform child in EndBeam.transform)
                    {
                        var ps = child.gameObject.GetComponent<ParticleSystem>();
                        ps.startColor = new Color(ps.startColor.r, ps.startColor.g, ps.startColor.b, 0.25f);
                    }

                    ChangePlayBackOfAllParticleSystems(EndBeam.transform, 1.5f);

                    EndBeam.transform.localScale = new Vector3(2.4f * DBPU.Beam_SizeMultiplier, 2.4f * DBPU.Beam_SizeMultiplier, 2.4f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(-2.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;
                    EndBeam.GetComponent<BeamComponent>().SafeMargin = 0f;
                    EndBeam.GetComponent<BeamComponent>().MinDistanceToLose = 5f;

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 150f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(1.5f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Last Emperor")
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 2500f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 10.1f * DBPU.Beam_SizeMultiplier) + (-Arm.right * 0.7f * DBPU.Beam_SizeMultiplier), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 1f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 1.01f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 10f) + (Arm.right * (0.9f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StartBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_S").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_L").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere Noise").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);
                    EndBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.18f, 0.3f, 0.1f);

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else if (EffectID == "Gigantic Full Blast")
                {
                    BeamAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                    OriginalSpeed = 50f;

                    BeamPhysics.PinFiringLimb(this, Arm);

                    Width = 2.4f * DBPU.Beam_SizeMultiplier;
                    ExtraWidth = Width / 10f;
                    StartDistance = -10f * DBPU.Beam_SizeMultiplier;
                    StartOffSet = 0.85f;
                    MiddleMinusDistance = -0.25f * DBPU.Beam_SizeMultiplier;
                    MiddleOffSet = -2f;
                    FlollowStartDistance = 10f;
                    Distance = 0f;
                    Opacity = -0.15f;

                    Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 2500f;
                    realSpeed = 50f;

                    DrainPercent = 0.020f;

                    StartBeam = Instantiate(GetEffect(EffectID, "Start"), Arm.position + (-Arm.up * 10.1f * DBPU.Beam_SizeMultiplier) + (-Arm.right * 0.7f * DBPU.Beam_SizeMultiplier), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    GetAllChildrenRecursive(StartBeam.transform);
                    //StartBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //StartBeam.transform.rotation = Arm.rotation;
                    StartBeam.transform.localScale = new Vector3(4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier, 4f * DBPU.Beam_SizeMultiplier);
                    //StartBeam.transform.SetParent(Arm);

                    Brightness = 0.18f;

                    MiddleBeam = StartBeam.gameObject.AddComponent<LineRenderer>();
                    MiddleBeam.textureMode = LineTextureMode.Tile;
                    MiddleBeam.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    MiddleBeam.sortingLayerName = "Bubbles";
                    MiddleBeam.sortingOrder = 5;
                    MiddleBeam.material.color = MakeMiddleBeamBodyColor(Color.white, Brightness, 0f);
                    MiddleBeam.material.mainTexture = GetTexture2D(EffectID);
                    //MiddleBeam.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                    MiddleBeam.material.mainTextureScale = new Vector2(0.2f, 1f);
                    MiddleBeam.startWidth = 0f;
                    MiddleBeam.endWidth = 0f;
                    MiddleBeam.positionCount = 2;
                    MiddleBeam.SetPosition(0, Arm.position + (-Arm.up * 1f * DBPU.Beam_SizeMultiplier));
                    MiddleBeam.SetPosition(1, Arm.position + (-Arm.up * 1.01f * DBPU.Beam_SizeMultiplier));

                    EndBeam = Instantiate(GetEffect(EffectID, "End"), Arm.position + (Arm.up * 10f) + (Arm.right * (0.9f * DBPU.Beam_SizeMultiplier)), Arm.rotation * Quaternion.Euler(0, 0, 90));
                    EndBeam.transform.GetChild(0).transform.localScale = new Vector3(2, 2, 2);
                    EndBeam.transform.GetChild(1).gameObject.SetActive(false);
                    //EndBeam.transform.Find("Muzzle Flash (1)").transform.localEulerAngles = new Vector3(90, 0, 0);
                    //EndBeam.transform.rotation = Arm.rotation;
                    GetAllChildrenRecursive(EndBeam.transform);

                    EndBeam.transform.localScale = new Vector3(1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier, 1f * DBPU.Beam_SizeMultiplier);
                    //EndBeam.transform.SetParent(Arm);

                    BeamDirection = -EndBeam.transform.right;

                    EndBeam.gameObject.layer = 9;

                    EndBeam.AddComponent<BoxCollider2D>();
                    EndBeam.GetComponent<BoxCollider2D>().size = new Vector2(3f * DBPU.Beam_SizeMultiplier, 3f * DBPU.Beam_SizeMultiplier);
                    EndBeam.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0f);
                    EndBeam.GetComponent<BoxCollider2D>().isTrigger = true;

                    Rigidbody2D rb = EndBeam.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;

                    EndBeam.AddComponent<BeamComponent>().beamBehaviour = gameObject.GetComponent<BeamBehaviour>();
                    EndBeam.GetComponent<BeamComponent>().RayDirection = -Arm.transform.up;

                    StartBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_S").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Effect_OutWind_L").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    StartBeam.transform.Find("Muzzle_Sphere Noise").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);
                    EndBeam.transform.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0f, 1f, 0f, 0.1f);

                    StillActive = true;

                    yield return new WaitForSeconds(0.1f);

                    realSpeed = 100f;

                    yield return new WaitForSeconds(0.2f);

                    BeamAudio.clip = DBPU.Attack_Kamehameha_Loop;
                    BeamAudio.loop = true;
                    BeamAudio.Play();

                    StartCoroutine(HoldingBeam());

                    yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                    Firing = false;
                    Clashing = false;

                    StopAllParticleSystems(StartBeam.transform);
                    StopAllParticleSystems(EndBeam.transform);

                    Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                    yield return new WaitForSeconds(0.1f);
                    Destroy(EndBeam.GetComponent<BeamComponent>());

                    CanSetVolume = false;

                    while (BeamAudio.volume > 0f)
                    {
                        BeamAudio.volume -= 0.1f;

                        yield return new WaitForSeconds(0.025f);
                    }

                    BeamAudio.Stop();
                    CanSetVolume = true;

                    yield return new WaitForSeconds(0.6f);

                    StillActive = false;

                    Destroy(StartBeam);
                    Destroy(EndBeam);

                    yield return null;
                }

                else
                {
                    yield return null;
                }
            }

            yield return null;
        }

        private IEnumerator HoldingBeam()
        {
            FinalHoldingTime = HoldingTime;

            TimeHolding = 0f;

            yield return new WaitUntil(() => (TimeHolding > FinalHoldingTime || Firing == false));

            TimeHolding = 0f;
            Firing = false;

            yield return null;
        }

        void StopAllParticleSystems(Transform parent)
        {
            foreach (Transform child in parent)
            {
                GetAllChildrenRecursive(child);

                var ps = child.gameObject.GetComponent<ParticleSystem>();
                ps.Stop();
            }
        }

        void ChangePlayBackOfAllParticleSystems(Transform parent, float speed)
        {
            foreach (Transform child in parent)
            {
                var ps = child.gameObject.GetComponent<ParticleSystem>();
                ps.playbackSpeed *= speed;
            }
        }

        void TintParticleSystemsUnder(Transform parent, Color tint)
        {
            if (parent == null) return;
            var systems = parent.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                var main = ps.main;
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                {
                    Color c = main.startColor.color;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, c.a));
                }
                else if (main.startColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    Color a = main.startColor.colorMin;
                    Color b = main.startColor.colorMax;
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(tint.r, tint.g, tint.b, a.a),
                        new Color(tint.r, tint.g, tint.b, b.a));
                }
            }
        }

        void GetAllChildrenRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.position = new Vector3(child.position.x, child.position.y, 0f);
                //child.rotation =  Quaternion.Euler(child.rotation.x, child.rotation.y, 90f);
                // if(child.name.Contains("OutWind_L"))
                // {
                //     child.localScale /= 2f;

                // }
                var sp = child.gameObject.GetComponent<Renderer>();
                sp.sortingLayerName = "Bubbles";
                sp.sortingOrder = 100;

                GetAllChildrenRecursive(child);
            }
        }

        void GetAllChildrenRecursiveToBack(Transform parent)
        {
            foreach (Transform child in parent)
            {
                var sp = child.gameObject.GetComponent<Renderer>();
                sp.sortingLayerName = "Bottom";
                sp.sortingOrder += 100;

                GetAllChildrenRecursive(child);
            }
        }

        void SetupSpecialBeamLineRenderer(LineRenderer line)
        {
            line.textureMode = LineTextureMode.Tile;
            line.material = new Material(ModAPI.FindMaterial("VeryBright"));
            line.sortingLayerName = "Default";
            line.sortingOrder = 8;
            line.material.mainTexture = DBPU.SpecialBeamCannon_Beam_Middle;
            line.material.mainTextureScale = new Vector2(1f, 1f);
            line.material.color = new Color(1f, 0.82f, 0.15f, 8f);
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.startWidth = 0f;
            line.endWidth = 0f;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.positionCount = 2;
        }

        float SpecialBeamAlpha()
        {
            return Mathf.Clamp01((Opacity + 0.15f) / 1.15f) * 8f;
        }

        void ApplySpecialBeamAlpha()
        {
            float a = SpecialBeamAlpha();
            if (MiddleBeam != null)
                MiddleBeam.material.color = new Color(1f, 0.82f, 0.15f, a);
        }

        Color EnergyWaveBeamRgb() => IsBeerusEnergyWave ? BeerusEnergyWaveRgb : Color.white;

        const float BeamBodyBrightnessRef = 0.18f;
        const float BeamBodyAlphaScale = 6f;

        Color MakeMiddleBeamBodyColor(Color tint, float brightness, float opacity01)
        {
            float rgbScale = Mathf.Max(0.01f, brightness) / BeamBodyBrightnessRef;
            return new Color(tint.r * rgbScale, tint.g * rgbScale, tint.b * rgbScale, Mathf.Max(0f, opacity01) * BeamBodyAlphaScale);
        }

        float MiddleBeamBodyAlpha() => Mathf.Max(0f, Opacity) * BeamBodyAlphaScale;

        void ApplyMiddleBeamBodyAlpha()
        {
            if (MiddleBeam == null || MiddleBeam.material == null) return;
            Color c = MiddleBeam.material.color;
            MiddleBeam.material.color = new Color(c.r, c.g, c.b, MiddleBeamBodyAlpha());
        }

        void ApplyEnergyWaveVFXColors(Transform start, Transform end)
        {
            Color c = IsBeerusEnergyWave ? BeerusEnergyWaveParticle : EnergyWaveParticle;
            start.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = c;
            start.Find("Effect_OutWind_S").gameObject.GetComponent<ParticleSystem>().startColor = c;
            start.Find("Effect_OutWind_L").gameObject.GetComponent<ParticleSystem>().startColor = c;
            start.Find("Muzzle_Sphere").gameObject.GetComponent<ParticleSystem>().startColor = c;
            start.Find("Muzzle_Sphere Noise").gameObject.GetComponent<ParticleSystem>().startColor = c;
            end.Find("Muzzle Flash (1)").gameObject.GetComponent<ParticleSystem>().startColor = c;
        }

        void TintSpecialBeamCannonParticles(Transform root, bool lightningOnly)
        {
            int lightningIndex = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps == null) continue;

                bool isLightning = child.name.ToLower().Contains("lightning") || child.name.ToLower().Contains("lighrning");
                if (lightningOnly && !isLightning) continue;

                float blend = isLightning ? Mathf.PingPong(lightningIndex++ * 0.45f, 1f) : 0.3f;
                Color c = Color.Lerp(SpecialBeamYellow, SpecialBeamPink, blend);
                ps.startColor = new Color(c.r, c.g, c.b, isLightning ? 0.22f : 0.1f);
            }
        }

        void TintSpecialBeamCannonStartVFX(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    string n = child.name.ToLower();
                    float a = 0.04f;
                    if (n.Contains("flash") || n.Contains("muzzle") || n.Contains("wind") || n.Contains("sphere") || n.Contains("noise"))
                        a = 0.018f;
                    Color c = Color.Lerp(SpecialBeamYellow, SpecialBeamPink, 0.15f);
                    ps.startColor = new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, a);
                }

                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.color = new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, Mathf.Min(c.a, 0.08f));
                }
            }
        }

        void TintSpecialBeamCannonChargeParticles(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                var ps = child.GetComponent<ParticleSystem>();
                if (ps == null) continue;

                string n = child.name.ToLower();
                bool inward = n.Contains("start_point");
                bool orb = !inward && (n == "core" || n.StartsWith("core ") || n.Contains("muzzle") || n.Contains("sphere"));
                Color c = inward ? SpecialBeamChargeStream : (orb ? SpecialBeamChargeOrb : SpecialBeamChargeStream);
                float a = orb ? 0.32f : 0.24f;
                ps.startColor = new Color(c.r, c.g, c.b, a);
            }
        }

        Vector3 GetSpecialBeamHandOrigin(Transform arm)
        {
            float m = DBPU.Beam_SizeMultiplier;
            return arm.position + (-arm.up * 0.2f * m) + (-arm.right * 0.1f * m);
        }

        void UpdateSpecialBeamMiddleGeometry()
        {
            if (MiddleBeam == null || EndBeam == null || UsedArm == null) return;

            Vector3 start = GetSpecialBeamHandOrigin(UsedArm);
            Vector3 end = Distance > FlollowStartDistance
                ? EndBeam.transform.position + (BeamDirection * MiddleMinusDistance) + (BeamDirection * MiddleOffSet)
                : start + BeamDirection * Mathf.Max(Distance, 0.5f);

            MiddleBeam.SetPosition(0, start);
            MiddleBeam.SetPosition(1, end);

            float length = Vector3.Distance(start, end);
            MiddleBeam.material.mainTextureScale = new Vector2(Mathf.Max(length * 0.15f, 0.35f), 1f);
        }

        private GameObject GetEffect(string EffectID, string EffectPart)
        {
            if (EffectID == "Energy Wave")
            {
                if (EffectPart == "Start")
                {
                    return DBPU.EnergyWave_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.EnergyWave_Beam_End;
                }
            }

            if (EffectID == "Kamehameha" || EffectID == "Rose Kamehameha")
            {
                if (EffectPart == "Charge")
                {
                    return DBPU.Kamehameha_Charge;
                }

                else if (EffectPart == "Start")
                {
                    return DBPU.Kamehameha_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.Kamehameha_Beam_End;
                }
            }

            if (EffectID == "Full Force Kamehameha")
            {
                if (EffectPart == "Charge")
                {
                    return DBPU.Kamehameha_Charge;
                }

                else if (EffectPart == "Start")
                {
                    return DBPU.FullForceKamehameha_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.FullForceKamehameha_Beam_End;
                }
            }

            if (EffectID == "Galick Gun")
            {
                if (EffectPart == "Charge")
                {
                    return DBPU.GalickGun_Charge;
                }

                if (EffectPart == "Aura")
                {
                    return DBPU.GalickGun_Aura;
                }

                else if (EffectPart == "Start")
                {
                    return DBPU.GalickGun_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.GalickGun_Beam_End;
                }
            }

            if (EffectID == "Special Beam Cannon")
            {
                if (EffectPart == "Charge")
                {
                    return DBPU.Kamehameha_Charge;
                }

                if (EffectPart == "Aura")
                {
                    return DBPU.GalickGun_Aura;
                }

                else if (EffectPart == "Start")
                {
                    return DBPU.GalickGun_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.GalickGun_Beam_End;
                }
            }

            if (EffectID == "Final Flash")
            {
                if (EffectPart == "Charge")
                {
                    return DBPU.FinalFlash_Charge;
                }

                else if (EffectPart == "Start")
                {
                    return DBPU.FinalFlash_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.FinalFlash_Beam_End;
                }
            }

            if (EffectID == "Last Emperor")
            {
                if (EffectPart == "Start")
                {
                    return DBPU.LastEmperor_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.LastEmperor_Beam_End;
                }
            }

            if (EffectID == "Gigantic Full Blast" || EffectID == "Gigantic Roar" || EffectID == "Max Cannon")
            {
                if (EffectPart == "Start")
                {
                    return DBPU.GiganticFullBlast_Beam_Start;
                }

                else if (EffectPart == "End")
                {
                    return DBPU.GiganticFullBlast_Beam_End;
                }
            }

            else
            {
                return null;
            }

            return null;
        }

        private Texture2D GetTexture2D(string EffectID)
        {
            if (EffectID == "Energy Wave")
            {
                if (Character == "Beerus")
                    return DBPU.GalickGun_Beam_Middle;

                return DBPU.EnergyWave_Beam_Middle;
            }

            else if (EffectID == "Rose Kamehameha")
            {
                return DBPU.GalickGun_Beam_Middle;
            }

            else if (EffectID == "Kamehameha")
            {
                return DBPU.Kamehameha_Beam_Middle;
            }

            else if (EffectID == "Full Force Kamehameha")
            {
                return DBPU.FullForceKamehameha_Beam_Middle;
            }

            else if (EffectID == "Final Flash")
            {
                return DBPU.FinalFlash_Beam_Middle;
            }

            else if (EffectID == "Galick Gun")
            {
                return DBPU.GalickGun_Beam_Middle;
            }

            else if (EffectID == "Special Beam Cannon")
            {
                return DBPU.SpecialBeamCannon_Beam_Middle;
            }

            else if (EffectID == "Last Emperor")
            {
                return DBPU.LastEmperor_Beam_Middle;
            }

            else if (EffectID == "Gigantic Full Blast" || EffectID == "Gigantic Roar" || EffectID == "Max Cannon")
            {
                return DBPU.GiganticFullBlast_Beam_Middle;
            }

            else
            {
                return null;
            }

            return null;
        }

        private GameObject MiddlePartAnimation(string EffectID)
        {
            if (MergeVisualActive) return null;

            CameraShakeBehaviour.main.Shake(0.25f, audioSource.transform.position);

            if (EffectID == "Energy Wave")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Kamehameha" || EffectID == "Rose Kamehameha")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Full Force Kamehameha")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 4f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 4f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= (Width * 0.6f) * Time.deltaTime;
                    else ExtraWidth = Width / 16f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Galick Gun")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Special Beam Cannon")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 8f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 8f) * Time.deltaTime;
                }
                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * 0.4f * Time.deltaTime;
                    else ExtraWidth = Width / 16f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Final Flash")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Last Emperor")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            if (EffectID == "Gigantic Full Blast" || EffectID == "Gigantic Roar" || EffectID == "Max Cannon")
            {
                if (MiddleBeam.startWidth < Width)
                {
                    MiddleBeam.startWidth += (Width * 3.5f) * Time.deltaTime;
                    MiddleBeam.endWidth += (Width * 3.5f) * Time.deltaTime;
                }

                else
                {
                    if (ExtraWidth > 0f) ExtraWidth -= Width * Time.deltaTime;
                    else ExtraWidth = Width / 4f;

                    MiddleBeam.startWidth = Width + ExtraWidth;
                    MiddleBeam.endWidth = Width + ExtraWidth;
                }
            }

            else
            {
                return null;
            }

            return null;
        }

        void DamageArms(float DamageDealt)
        {
            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("Arm")) limb.Damage(DamageDealt);
            }
        }

        Vector3 GetEndPosition()
        {
            if (Body != null && Body.Live) return Body.Tip;
            return LegacyEndPosition();
        }

        // Where the head sits on a perfectly straight beam.
        public Vector3 LegacyEndPosition()
        {
            if (StartBeam == null) return EndBeamPosition;
            return StartBeam.transform.position + (BeamDirection * (StartDistance + Distance)) + (StartBeam.transform.up * StartOffSet);
        }

        public void SyncEndBeamTransform()
        {
            if (EndBeam == null) return;
            EndBeam.transform.position = GetEndPosition();
        }

        void SyncToMergeHost()
        {
            if (MergeHost == null) return;

            Distance = MergeHost.Distance;
            EndBeamPosition = MergeHost.EndBeamPosition;
            BeamDirection = MergeHost.BeamDirection;

            if (EndBeam != null)
            {
                if (MergeHost.EndBeam != null)
                    EndBeam.transform.position = MergeHost.EndBeam.transform.position;
                else if (MergeHost.StartBeam != null)
                    EndBeam.transform.position = MergeHost.GetEndPosition();
            }

            if (MiddleBeam != null && MergeHost.MiddleBeam != null && MiddleBeam.positionCount >= 2 && MergeHost.MiddleBeam.positionCount >= 2)
            {
                MiddleBeam.SetPosition(0, MergeHost.MiddleBeam.GetPosition(0));
                MiddleBeam.SetPosition(1, MergeHost.MiddleBeam.GetPosition(1));
            }
        }

        Vector3 GetSoundPosition()
        {
            float minX = Mathf.Min(StartBeam.transform.position.x, EndBeam.transform.position.x);
            float maxX = Mathf.Max(StartBeam.transform.position.x, EndBeam.transform.position.x);

            float minY = Mathf.Min(StartBeam.transform.position.y, EndBeam.transform.position.y);
            float maxY = Mathf.Max(StartBeam.transform.position.y, EndBeam.transform.position.y);

            float clampedX = Mathf.Clamp(Global.CameraPosition.x, minX, maxX);
            float clampedY = Mathf.Clamp(Global.CameraPosition.y, minY, maxY);

            return new Vector3(clampedX, clampedY);
        }
    }
}
