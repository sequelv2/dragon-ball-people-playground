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
    public class BallBehaviour : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        public KiSystem KiSys;
        private AudioSource audioSource;

        private GameObject Ball;
        public GameObject ProjectileObject { get { return Ball; } }
        public KiShield ShieldBarrier;
        public bool ShieldDissolved;
        // The physical sphere and its new look, while the module and its shaders are available.
        public BallOrb Orb;
        public bool HeldOrb;
        public float ShieldStopDistance;
        private GameObject ballChargeWind;
        private Transform UsedArm;
        public Transform CastingArm { get { return UsedArm; } }
        private AudioSource activeChargeAudio;
        private float chargeSavedRegen;
        private bool chargeSuppressesRegen;

        public bool Active = false;
        public bool Charging = false;
        public bool FullCharged = false;
        public bool Firing = false;
        public bool StillActive = false;
        public bool Clashing = false;
        public float RealDamage = 7500f;
        public List<BeamBehaviour> PushingBeams = new List<BeamBehaviour>();
        public float CostPercent = 0.15f;

        public string Character = "Human";

        public float Damage = 0f;
        public float Speed = 40f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;
        public float UseDelay = 6f;
        public float FinalUseDelay = 6f;
        public float DelayTime = 0f;

        public float OriginalSpeed = 100f;

        public float Distance = 0f;
        private float Brightness = 0.14f;
        private float Opacity = 0f;

        public Vector3 BallPosition;
        public Vector3 BallDirection;

        public string EffectID;

        public static float DeathBallChargeAnimSeconds = 1.7f;

        private static readonly float[] s_deathBallAnimPresets = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f, 3f, 3.25f, 3.5f, 4f, 5f, 6f, 8f };

        // A fired sphere is still moved and damaged by this controller. Keep its
        // release coroutine alive when changing technique; cancel only a held charge.
        public void CancelBallState()
        {
            Active = false;
            var spirit = GetComponent<SpiritBombChannel>();
            if (spirit) spirit.CancelCharge();
            if (UsedArm != null)
            {
                var freeze = UsedArm.GetComponent<FreezeBehaviour>();
                if (freeze != null) Destroy(freeze);
            }
            UsedArm = null;
            if (StillActive) return;

            StopAllCoroutines();
            Charging = false;
            FullCharged = false;
            Firing = false;
            Clashing = false;
            Distance = 0f;
            DelayTime = 0f;
            PushingBeams.Clear();
            if (ballChargeWind != null) { Destroy(ballChargeWind); ballChargeWind = null; }
            if (Ball != null) { Destroy(Ball); Ball = null; }
            if (Orb != null) { Orb.Dispose(); Orb = null; }
            if (activeChargeAudio != null)
            {
                activeChargeAudio.Stop();
                Destroy(activeChargeAudio.gameObject);
                activeChargeAudio = null;
            }
            if (audioSource != null) { audioSource.Stop(); audioSource.loop = false; }
            if (chargeSuppressesRegen && KiSys != null && KiSys.baseRegen == 0f)
                KiSys.baseRegen = chargeSavedRegen;
            chargeSuppressesRegen = false;
        }

        public static void CycleDeathBallChargeAnimPreset()
        {
            float c = DeathBallChargeAnimSeconds;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < s_deathBallAnimPresets.Length; i++)
            {
                float d = Mathf.Abs(s_deathBallAnimPresets[i] - c);
                if (d < bestDiff)
                {
                    bestDiff = d;
                    best = i;
                }
            }

            best = (best + 1) % s_deathBallAnimPresets.Length;
            DeathBallChargeAnimSeconds = s_deathBallAnimPresets[best];
        }

        private void OnDestroy()
        {
            if (ballChargeWind != null) Destroy(ballChargeWind);
            if (Ball != null) Destroy(Ball);
            if (Orb != null) Orb.Dispose();
        }

        private void LateUpdate() { if (Orb != null) Orb.LateStep(); }

        void AttachOrb(Transform arm, float chargeSeconds)
        {
            UsedArm = arm;
            if (OrbFx.Active) Orb = new BallOrb(this, arm, EffectID, chargeSeconds);
        }

        // The hand is free again the moment the sphere stops being its business.
        public void ReleaseArm()
        {
            if (UsedArm == null) return;
            var freeze = UsedArm.GetComponent<FreezeBehaviour>();
            if (freeze != null) Destroy(freeze);
        }

        Vector3 AimFrom(Transform arm) { return Orb != null ? Orb.AimDirection(arm) : -arm.up; }

        float TriggerRadius(float legacy)
        {
            if (Orb == null || Ball == null) return legacy;
            return Orb.FullRadius / Mathf.Max(.001f, Mathf.Abs(Ball.transform.lossyScale.x));
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
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 350f;

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (StillActive || !KiHandLoadout.LegacyAllowed(limb, EffectID)) return;
                        if (IsSpiritBombEffect)
                        {
                            if (Active && limb.Health > 0) SpiritBombChannel.GetOrCreate(limb.Person).Activate(limb);
                            return;
                        }
                        if (Charging == false && KiSys.currentKi < KiSys.maxKi * CostPercent) return;

                        if (limb.Health > 0f && Active == true && Charging == false && Firing == false)
                        {
                            UsedArm = limb.transform;
                            StartCoroutine(StartCharge(limb.transform));
                        }

                        else if (Active == true && Charging == true && Firing == false && FullCharged == true && UsedArm == limb.transform)
                        {
                            Firing = true;
                        }
                    };
                }
            }
        }

        private bool IsSpiritBombEffect =>
            EffectID == "Spirit Bomb" || EffectID == "Super Spirit Bomb";

        private void Update()
        {
            if (StillActive == true)
            {
                RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

                if (Clashing == false) DelayTime += 1f * Time.deltaTime;

                if (Ball != null && !HeldOrb)
                {
                    Distance += (Speed * DBPU.Beam_SpeedMultiplier) * Time.deltaTime;

                    KiShield.ConstrainBall(this);
                    Vector3 pos = GetEndPosition();
                    if (IsSpiritBombEffect) pos.z = 27f;
                    Ball.transform.position = pos;
                }
            }
        }

        private IEnumerator StartCharge(Transform Arm)
        {
            if (StillActive) yield break;
            Charging = true;
            ShieldDissolved = false; ShieldBarrier = null;
            Firing = false;
            Clashing = false;

            AudioSource BallAudio = new GameObject("BallAudioSource").AddComponent<AudioSource>();
            activeChargeAudio = BallAudio;
            BallAudio.transform.SetParent(transform.Find("Body/UpperBody"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(BallAudio, false);
            BallAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            BallAudio.outputAudioMixerGroup = global.SoundEffects;
            BallAudio.spatialBlend = 1;
            BallAudio.volume = 1;
            BallAudio.maxDistance = 1000;

            if (EffectID == "Gigantic Crusher")
            {
                BallAudio.clip = DBPU.Attack_GalickGun_Charging;
                BallAudio.loop = true;
                //BallAudio.Play();
                BallAudio.volume = 0.25f;

                CostPercent = 0.4f;

                StartCoroutine(KiCost());

                Ball = Instantiate(GetEffect(EffectID, "Ball"), Arm.position + (Vector3.up * 36f), Quaternion.identity);
                Ball.layer = 9;

                if (!OrbFx.Active)
                {
                    ballChargeWind = Instantiate(DBPU.GiganticCrusherWind, Ball.transform.position, Quaternion.identity);
                    ballChargeWind.transform.SetParent(Ball.transform, false);
                }

                ApplyChargeBallSortingForCharge(Ball.transform, Arm);

                SetGiganticCrusherStage(Ball, 1);
                AttachOrb(Arm, 9.25f);

                while (BallAudio.volume < 1f)
                {
                    BallAudio.volume += 0.01f;

                    yield return new WaitForSeconds(0.0125f);
                }

                yield return new WaitForSeconds(8f);

                FullCharged = true;

                if (Ball != null) SetGiganticCrusherStage(Ball, 2);

                while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    yield return null;
                }

                if (Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    StartCoroutine(Release(Arm, BallAudio));
                }
                else
                {
                    FullCharged = false;
                    Charging = false;
                    Destroy(Ball);
                }

                yield return null;
            }

            else if (EffectID == "Death Ball")
            {
                BallAudio.clip = DBPU.Attack_GalickGun_Charging;
                BallAudio.loop = true;
                //BallAudio.Play();
                BallAudio.volume = 0.25f;

                CostPercent = 0.4f;

                StartCoroutine(KiCost());

                Ball = Instantiate(GetEffect(EffectID, "Ball"), Arm.position + (Vector3.up * 20f), Quaternion.identity);
                Ball.layer = 9;

                Ball.transform.localScale = Ball.transform.localScale * 1.18f;

                ApplyChargeBallSortingForCharge(Ball.transform, Arm);
                AttachOrb(Arm, 1.25f + Mathf.Clamp(DeathBallChargeAnimSeconds, 0.05f, 30f));

                while (BallAudio.volume < 1f)
                {
                    BallAudio.volume += 0.01f;

                    yield return new WaitForSeconds(0.0125f);
                }

                yield return new WaitForSeconds(Mathf.Clamp(DeathBallChargeAnimSeconds, 0.05f, 30f));

                DisableDeathBallAnimators(Ball);

                FullCharged = true;

                while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    yield return null;
                }

                if (Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    StartCoroutine(Release(Arm, BallAudio));
                }
                else
                {
                    FullCharged = false;
                    Charging = false;
                    Destroy(Ball);
                }

                yield return null;
            }

            else if (EffectID == "Supernova" || EffectID == "Beerus Ball")
            {
                BallAudio.clip = DBPU.Attack_GalickGun_Charging;
                BallAudio.loop = true;
                BallAudio.volume = 0.25f;

                CostPercent = EffectID == "Beerus Ball" ? 0.45f : 0.4f;

                StartCoroutine(KiCost());

                float scaleMult = EffectID == "Beerus Ball" ? 1.35f : 1f;
                Vector3 supernovaEndScale = new Vector3(10f * scaleMult, 10f * scaleMult, 10f * scaleMult);
                Vector3 supernovaStartScale = Vector3.zero;
                Vector3 supernovaArmBottom = Arm.position + (-Arm.up * 0.4f);
                Vector3 supernovaHoverStart = new Vector3(supernovaArmBottom.x, supernovaArmBottom.y, 27f);
                Vector3 supernovaHoverDelta = (-Arm.up) * 34f + Vector3.up * 22f;
                Vector3 supernovaHoverEnd = new Vector3(supernovaHoverStart.x + supernovaHoverDelta.x, supernovaHoverStart.y + supernovaHoverDelta.y, 27f);

                Ball = Instantiate(GetEffect(EffectID, "Ball"), supernovaHoverStart, Quaternion.identity);
                Ball.transform.localScale = supernovaStartScale;
                Ball.layer = 9;

                foreach (Transform child in Ball.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sp.sortingOrder += 10;
                }
                float supernovaGrowDuration = EffectID == "Beerus Ball" ? 3.5f : 3.15f;
                AttachOrb(Arm, supernovaGrowDuration);
                float supernovaGrowElapsed = 0f;
                float supernovaVolStepTimer = 0f;

                while (BallAudio.volume < 1f || supernovaGrowElapsed < supernovaGrowDuration)
                {
                    supernovaVolStepTimer += Time.deltaTime;
                    while (supernovaVolStepTimer >= 0.0125f && BallAudio.volume < 1f)
                    {
                        supernovaVolStepTimer -= 0.0125f;
                        BallAudio.volume += 0.01f;
                    }

                    supernovaGrowElapsed += Time.deltaTime;
                    float supernovaT = Mathf.Clamp01(supernovaGrowElapsed / supernovaGrowDuration);
                    float supernovaSmooth = supernovaT * supernovaT * supernovaT * (supernovaT * (supernovaT * 6f - 15f) + 10f);
                    Ball.transform.localScale = Vector3.Lerp(supernovaStartScale, supernovaEndScale, supernovaSmooth);
                    Vector3 supernovaBasePos = Vector3.Lerp(supernovaHoverStart, supernovaHoverEnd, supernovaSmooth);
                    Vector3 supernovaFromHand = (-Arm.up).normalized * (supernovaSmooth * 3.5f);
                    Ball.transform.position = new Vector3(supernovaBasePos.x + supernovaFromHand.x, supernovaBasePos.y + supernovaFromHand.y, 27f);

                    yield return null;
                }

                BallAudio.volume = 1f;
                Ball.transform.localScale = supernovaEndScale;
                Ball.transform.position = new Vector3(supernovaHoverEnd.x + ((-Arm.up).normalized * 3.5f).x, supernovaHoverEnd.y + ((-Arm.up).normalized * 3.5f).y, 27f);

                FullCharged = true;

                while (Firing == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    yield return null;
                }

                if (Arm.GetComponent<LimbBehaviour>().Health > 0f)
                {
                    StartCoroutine(Release(Arm, BallAudio));
                }
                else
                {
                    FullCharged = false;
                    Charging = false;
                    Destroy(Ball);
                }

                yield return null;
            }


            yield return null;
        }

        private IEnumerator KiCost()
        {
            float OGKiDrain = KiSys.currentKiDrainRate;
            float KiRegen = KiSys.baseRegen;

            chargeSavedRegen = KiRegen;
            chargeSuppressesRegen = OGKiDrain != 0f;
            if (chargeSuppressesRegen) KiSys.baseRegen = 0f;

            float FinalKi = KiSys.currentKi - (KiSys.maxKi * CostPercent);
            float CurrentKi = KiSys.currentKi;

            while (CurrentKi > FinalKi)
            {
                KiSys.currentKi -= (KiSys.maxKi * CostPercent) * (Time.deltaTime / 5f);
                CurrentKi -= (KiSys.maxKi * CostPercent) * (Time.deltaTime / 5f);

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;
            chargeSuppressesRegen = false;

            yield return null;
        }

        private IEnumerator Release(Transform Arm, AudioSource BallAudio)
        {
            FullCharged = false;
            Firing = true;
            Charging = false;

            BallPosition = Ball.transform.position;

            if (EffectID == "Gigantic Crusher")
            {
                BallAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                if (ballChargeWind != null)
                {
                    Destroy(ballChargeWind);
                    ballChargeWind = null;
                }

                OriginalSpeed = 40f;
                Speed = 40f;

                Distance = 0f;

                Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 300f;

                Ball.transform.parent = null;
                GetAllChildrenRecursive(Ball.transform);

                BallDirection = AimFrom(Arm);

                Ball.gameObject.layer = 9;

                Rigidbody2D rb = Ball.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;

                Ball.AddComponent<BallComponent>().ballBehaviour = gameObject.GetComponent<BallBehaviour>();
                Ball.GetComponent<BallComponent>().RayDirection = BallDirection;
                Ball.GetComponent<BallComponent>().User = this.gameObject;

                CircleCollider2D ballTrigger = Ball.AddComponent<CircleCollider2D>();
                ballTrigger.radius = TriggerRadius(1.4f)/* * DBPU.Beam_SizeMultiplier*/;
                ballTrigger.isTrigger = true;

                StillActive = true;

                SetGiganticCrusherStage(Ball, 3);

                yield return new WaitUntil(() => Distance > 600f || KiSys.currentKi < (KiSys.maxKi * 0.01f) || !Firing);

                if (Orb != null) Orb.FiringEnded();
                Firing = false;
                StillActive = false;
                Orb = null;

                if (Ball != null) Destroy(Ball);

                yield return null;
            }

            else if (EffectID == "Death Ball")
            {
                //BallAudio.PlayOneShot(DBPU.Attack_Kamehameha_Fire);

                OriginalSpeed = 60f;
                Speed = 60f;

                Arm.gameObject.AddComponent<FreezeBehaviour>();

                Distance = 0f;

                Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 1000f;

                if (Orb == null || Orb.OwnLook) RestoreDeathBallFiredVisualPlayback(Ball);

                Ball.transform.parent = null;
                GetAllChildrenRecursive(Ball.transform);

                BallDirection = AimFrom(Arm);

                Ball.gameObject.layer = 9;

                Rigidbody2D rb = Ball.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;

                Ball.AddComponent<BallComponent>().ballBehaviour = gameObject.GetComponent<BallBehaviour>();
                Ball.GetComponent<BallComponent>().RayDirection = BallDirection;
                Ball.GetComponent<BallComponent>().User = this.gameObject;

                CircleCollider2D ballTrigger = Ball.AddComponent<CircleCollider2D>();
                ballTrigger.radius = TriggerRadius(3.1f)/* * DBPU.Beam_SizeMultiplier*/;
                ballTrigger.isTrigger = true;

                StillActive = true;

                audioSource.clip = DBPU.Attack_Kamehameha_Loop;
                audioSource.loop = true;
                //audioSource.Play();

                StartCoroutine(HoldingBall());

                yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                if (Orb != null) Orb.FiringEnded();
                Firing = false;
                Clashing = false;

                if (Orb == null || Orb.OwnLook) StopAllParticleSystems(Ball.transform);

                Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());

                while (audioSource.volume > 0f)
                {
                    audioSource.volume -= 0.1f;

                    yield return new WaitForSeconds(0.025f);
                }

                audioSource.Stop();
                audioSource.volume = 1f;

                yield return new WaitForSeconds(0.6f);

                StillActive = false;
                Orb = null;

                Destroy(Ball);

                yield return null;
            }

            else if (EffectID == "Supernova" || EffectID == "Beerus Ball")
            {
                OriginalSpeed = EffectID == "Beerus Ball" ? 58f : 50f;
                Speed = OriginalSpeed;

                Arm.gameObject.AddComponent<FreezeBehaviour>();

                Distance = 0f;

                Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / (EffectID == "Beerus Ball" ? 120f : 250f);

                Ball.transform.parent = null;
                GetAllChildrenRecursiveNoZ(Ball.transform);

                BallDirection = AimFrom(Arm);

                Ball.gameObject.layer = 9;

                Rigidbody2D rb = Ball.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;

                Ball.AddComponent<BallComponent>().ballBehaviour = gameObject.GetComponent<BallBehaviour>();
                Ball.GetComponent<BallComponent>().RayDirection = BallDirection;
                Ball.GetComponent<BallComponent>().User = this.gameObject;

                CircleCollider2D ballTrigger = Ball.AddComponent<CircleCollider2D>();
                ballTrigger.radius = TriggerRadius(EffectID == "Beerus Ball" ? 7.2f : 5.4f);
                ballTrigger.isTrigger = true;

                StillActive = true;

                audioSource.clip = DBPU.Attack_Kamehameha_Loop;
                audioSource.loop = true;
                //audioSource.Play();

                StartCoroutine(HoldingBall());

                yield return new WaitUntil(() => Firing == false || (KiSys.currentKi < (KiSys.maxKi * 0.01f) && Clashing == false));

                if (Orb != null) Orb.FiringEnded();
                Firing = false;
                Clashing = false;

                //StopAllParticleSystems(Ball.transform);

                Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());
                Destroy(Ball.GetComponent<BallComponent>());

                /*while (audioSource.volume > 0f)
                {
                    audioSource.volume -= 0.1f;

                    yield return new WaitForSeconds(0.025f);
                }*/

                audioSource.Stop();
                audioSource.volume = 1f;

                yield return new WaitForSeconds(0.1f);

                StillActive = false;
                Orb = null;

                Destroy(Ball);

                yield return null;
            }


            else
            {
                yield return null;
            }

            yield return null;
        }

        private IEnumerator HoldingBall()
        {
            FinalUseDelay = UseDelay;

            DelayTime = 0f;

            yield return new WaitUntil(() => (DelayTime > FinalUseDelay || Firing == false));

            DelayTime = 0f;
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

                GetAllChildrenRecursive(child);
            }
        }

        void GetAllChildrenRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.position = new Vector3(child.position.x, child.position.y, 0f);
                var sp = child.gameObject.GetComponent<Renderer>();
                if (sp != null)
                {
                    sp.sortingLayerName = "Bubbles";
                    sp.sortingOrder = 100;
                }

                GetAllChildrenRecursive(child);
            }
        }

        void GetAllChildrenRecursiveNoZ(Transform parent)
        {
            foreach (Transform child in parent)
            {
                var sp = child.gameObject.GetComponent<Renderer>();
                if (sp != null)
                {
                    sp.sortingLayerName = "Bubbles";
                    sp.sortingOrder = 100;
                }

                GetAllChildrenRecursiveNoZ(child);
            }
        }

        void GetAllChildrenRecursiveToBack(Transform parent)
        {
            foreach (Transform child in parent)
            {
                var sp = child.gameObject.GetComponent<Renderer>();
                if (sp != null)
                {
                    sp.sortingLayerName = "Bottom";
                    sp.sortingOrder += 100;
                }

                GetAllChildrenRecursiveToBack(child);
            }
        }

        void ApplyChargeBallSortingForCharge(Transform root, Transform Arm)
        {
            var armSr = Arm.gameObject.GetComponent<SpriteRenderer>();
            if (armSr == null) return;

            root.position = new Vector3(root.position.x, root.position.y, 0f);
            var rootR = root.gameObject.GetComponent<Renderer>();
            if (rootR != null)
            {
                rootR.sortingLayerName = armSr.sortingLayerName;
                rootR.sortingOrder += 10;
            }

            foreach (Transform child in root)
                ApplyChargeBallSortingRecursive(child, armSr.sortingLayerName, 10);
        }

        void ApplyChargeBallSortingRecursive(Transform t, string sortingLayer, int orderBoost)
        {
            t.position = new Vector3(t.position.x, t.position.y, 0f);
            var sp = t.gameObject.GetComponent<Renderer>();
            if (sp != null)
            {
                sp.sortingLayerName = sortingLayer;
                sp.sortingOrder += orderBoost;
            }

            foreach (Transform child in t)
                ApplyChargeBallSortingRecursive(child, sortingLayer, orderBoost);
        }

        void DisableDeathBallAnimators(GameObject ball)
        {
            if (ball == null) return;

            foreach (Animator an in ball.GetComponentsInChildren<Animator>(true))
                an.enabled = false;

            foreach (Animation legacy in ball.GetComponentsInChildren<Animation>(true))
                legacy.enabled = false;
        }

        void RestoreDeathBallFiredVisualPlayback(GameObject ball)
        {
            if (ball == null) return;

            foreach (ParticleSystem ps in ball.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play(true);
        }

        private void SetGiganticCrusherStage(GameObject ball, int stage)
        {
            if (ball == null || Orb != null) return;

            Transform energyCollect = ball.transform.Find("EnergyCollect");
            Transform one = ball.transform.Find("1");
            Transform startSparks = ball.transform.Find("StartSparks");
            Transform physDebris = ball.transform.Find("PhysDebris");
            Transform fog = ball.transform.Find("FogLivelyWhite");

            bool enableEnergyCollect = (stage == 1);

            if (energyCollect != null) energyCollect.gameObject.SetActive(enableEnergyCollect);
            if (one != null) one.gameObject.SetActive(true);
            if (startSparks != null) startSparks.gameObject.SetActive(true);
            if (physDebris != null) physDebris.gameObject.SetActive(true);
            if (fog != null) fog.gameObject.SetActive(true);

            var coreR = one != null ? one.GetComponent<Renderer>() : null;
            var fogR = fog != null ? fog.GetComponent<Renderer>() : null;

            if (fogR != null)
            {
                fogR.sortingLayerName = "Bottom";
                fogR.sortingOrder = 0;
            }

            if (coreR != null)
            {
                coreR.sortingLayerName = "Bubbles";
                coreR.sortingOrder = 200;
            }
        }

        private GameObject GetEffect(string EffectID, string EffectPart)
        {
            if (EffectID == "Gigantic Crusher")
            {
                if (EffectPart == "Ball")
                {
                    return DBPU.GiganticCrusher;
                }
            }

            else if (EffectID == "Supernova" || EffectID == "Beerus Ball")
            {
                if (EffectPart == "Ball")
                {
                    return DBPU.Supernova;
                }
            }

            else if (EffectID == "Spirit Bomb")
            {
                if (EffectPart == "Ball")
                {
                    return DBPU.GenkiDama;
                }
            }

            else if (EffectID == "Super Spirit Bomb")
            {
                if (EffectPart == "Ball")
                {
                    return DBPU.SuperGenkiDama;
                }
            }

            else if (EffectID == "Death Ball")
            {
                if (EffectPart == "Ball")
                {
                    return DBPU.DeathBall_Ball;
                }
            }

            else
            {
                return null;
            }

            return null;
        }

        Vector3 GetEndPosition()
        {
            return BallPosition + (BallDirection * (Distance));
        }

        public class BallComponent : MonoBehaviour
        {
            public BallBehaviour ballBehaviour;
            public GameObject User;

            public float DistanceOfWallContact = 0f;
            public float MinDistanceToLose = 3f;
            public float SafeMargin = 1f;
            public bool HitAWall = false;
            public Vector3 RayDirection;

            public void Update()
            {
                if (ballBehaviour.Orb != null)
                {
                    if (ballBehaviour.Clashing == false && ballBehaviour.Speed != ballBehaviour.OriginalSpeed) ballBehaviour.Speed = ballBehaviour.OriginalSpeed;
                    return;
                }
                if (ballBehaviour.ShieldDissolved || (ballBehaviour.ShieldBarrier && ballBehaviour.ShieldBarrier.Using)) return;
                if (!Global.main.Paused && HitAWall == false)
                {
                    RaycastHit2D[] rayHits = Physics2D.RaycastAll(
                        origin: transform.position,
                        direction: RayDirection,
                        distance: GetComponent<CircleCollider2D>().radius
                        );

                    foreach (RaycastHit2D hit in rayHits)
                    {
                        if (hit.collider.gameObject.transform.root.transform != ballBehaviour.transform && hit.collider.gameObject.layer == 11 && !hit.collider.GetComponent<KiShieldSurface>())
                        {
                            StartCoroutine(HitWall());
                        }
                    }
                }

                if (ballBehaviour.Clashing == false && ballBehaviour.Speed != ballBehaviour.OriginalSpeed) ballBehaviour.Speed = ballBehaviour.OriginalSpeed;
            }

            public void OnTriggerEnter2D(Collider2D other)
            {
                if (ballBehaviour.Orb != null) return;
                if (ballBehaviour.ShieldDissolved || (ballBehaviour.ShieldBarrier && ballBehaviour.ShieldBarrier.Using)) return;
                if (other.gameObject.GetComponent<BallComponent>() && ballBehaviour.Clashing == false)
                {
                    BallComponent otherBallComp = other.gameObject.GetComponent<BallComponent>();
                    if (otherBallComp.ballBehaviour.Clashing == false)
                    {
                        ballBehaviour.Clashing = true;
                        otherBallComp.ballBehaviour.Clashing = true;
                        StartCoroutine(BallClash(otherBallComp.ballBehaviour));
                    }
                }
            }

            public void OnTriggerStay2D(Collider2D other)
            {
                var orb = ballBehaviour.Orb;
                if (orb != null && (orb.Held || orb.Dueling || orb.Finished)) return;
                if (other.gameObject.transform.root.gameObject != User && !other.gameObject.GetComponent<BallComponent>() && !other.gameObject.GetComponent<BeamComponent>() && !other.gameObject.GetComponent<BlastComponent>() && (other.gameObject.layer == 9 || other.gameObject.layer == 13))
                {
                    StartCoroutine(OnHit(other, RayDirection));
                }
            }

            public IEnumerator BallClash(BallBehaviour otherBall)
            {
                ballBehaviour.Speed = 0f;
                otherBall.Speed = 0f;

                yield return new WaitForSeconds(0.1f);

                StartCoroutine(BallStruggle(otherBall));

                yield return null;
            }

            public IEnumerator BallStruggle(BallBehaviour otherBall)
            {
                ballBehaviour.Speed = 0f;
                otherBall.Speed = 0f;

                float updateInterval = 0.01f;
                float elapsed = 0f;
                float timeBeforeAllowingWin = 1.0f;

                while (ballBehaviour.Firing == true && otherBall.Firing == true)
                {
                    float thisDamage = ballBehaviour.RealDamage;
                    float otherDamage = otherBall.RealDamage;

                    float thisRoll = Random.Range(1f, 100f) * thisDamage * PowerLostByKi(ballBehaviour);
                    float otherRoll = Random.Range(1f, 100f) * otherDamage * PowerLostByKi(otherBall);

                    float shiftAmount = (0.05f * DBPU.Beam_SpeedMultiplier) * Mathf.Clamp(thisRoll / otherRoll, 1f, 5f);

                    if (thisRoll > otherRoll)
                    {
                        ballBehaviour.Distance += shiftAmount;
                        otherBall.Distance = Mathf.Max(0f, otherBall.Distance - shiftAmount);
                    }
                    else
                    {
                        otherBall.Distance += shiftAmount;
                        ballBehaviour.Distance = Mathf.Max(0f, ballBehaviour.Distance - shiftAmount);
                    }

                    elapsed += updateInterval;

                    if (elapsed >= timeBeforeAllowingWin)
                    {
                        if (otherBall.Distance <= MinDistanceToLose)
                        {
                            ballBehaviour.Distance += otherBall.Distance;
                            ballBehaviour.DelayTime = 0f;
                            ballBehaviour.FinalUseDelay = ballBehaviour.UseDelay;
                            ballBehaviour.Speed = ballBehaviour.OriginalSpeed;
                            ballBehaviour.Clashing = false;

                            otherBall.Distance = 0f;
                            otherBall.Firing = false;
                            otherBall.Clashing = false;

                            break;
                        }

                        if (ballBehaviour.Distance <= MinDistanceToLose)
                        {
                            otherBall.Distance += ballBehaviour.Distance;
                            otherBall.DelayTime = 0f;
                            otherBall.FinalUseDelay = otherBall.UseDelay;
                            otherBall.Speed = otherBall.OriginalSpeed;
                            otherBall.Clashing = false;

                            ballBehaviour.Distance = 0f;
                            ballBehaviour.Firing = false;
                            ballBehaviour.Clashing = false;

                            break;
                        }
                    }

                    yield return new WaitForSeconds(updateInterval);
                }

                ballBehaviour.Clashing = false;
                ballBehaviour.DelayTime = 0f;
                ballBehaviour.FinalUseDelay = ballBehaviour.UseDelay;
                ballBehaviour.Speed = ballBehaviour.OriginalSpeed;

                otherBall.Clashing = false;
                otherBall.DelayTime = 0f;
                otherBall.FinalUseDelay = otherBall.UseDelay;
                otherBall.Speed = otherBall.OriginalSpeed;
            }

            float PowerLostByKi(BallBehaviour BB)
            {
                float Percent = Mathf.Clamp((BB.KiSys.currentKi / BB.KiSys.maxKi) * 2f, 0.20f, 1f);
                return Percent;
            }

            public IEnumerator HitWall()
            {
                if (ballBehaviour.Speed != 0f)
                {
                    if (DistanceOfWallContact == 0f) DistanceOfWallContact = ballBehaviour.Distance;

                    if (ballBehaviour.Distance > DistanceOfWallContact + (12f * DBPU.Beam_SizeMultiplier))
                    {
                        HitAWall = true;
                        ballBehaviour.Speed = 0f;
                    }
                }

                yield return null;
            }

            public IEnumerator OnHit(Collider2D col, Vector2 direction)
            {
                if (ballBehaviour.ShieldDissolved || (ballBehaviour.ShieldBarrier && ballBehaviour.ShieldBarrier.Using)) yield break;
                if (col.gameObject.layer == 9 || col.gameObject.layer == 13)
                {
                    if (col.gameObject.GetComponent<Rigidbody2D>())
                    {
                        float TimeScale = Global.main.SlowmotionTimescale;
                        if (!Global.main.SlowMotion) TimeScale = 1f;

                        Rigidbody2D rb = col.gameObject.GetComponent<Rigidbody2D>();

                        if (col.gameObject.GetComponent<LimbBehaviour>() && col.isTrigger != true)
                        {
                            LimbBehaviour limb = col.gameObject.GetComponent<LimbBehaviour>();
                            if (KiShield.Protects(limb.Person, User, ballBehaviour.BallPosition, (double)ballBehaviour.RealDamage * 60, ballBehaviour.GetInstanceID())) yield break;
                            if (ballBehaviour.Orb != null && ballBehaviour.Orb.Contact(limb)) yield break;
                            var energyOwner = ballBehaviour.Orb != null ? ballBehaviour.Orb.Owner : User ? User.GetComponent<PersonBehaviour>() : null;
                            double energyPower = ballBehaviour.Orb != null ? ballBehaviour.Orb.Power
                                : DbpuFx.PowerOf(energyOwner) * Mathf.Max(.01f, ballBehaviour.DamageOutput);
                            EnergyAnnihilation.Expose(energyOwner, limb, KiCombatMath.Cap(energyPower),
                                col.ClosestPoint(ballBehaviour.BallPosition), Time.fixedDeltaTime,
                                OrbStyle.Of(ballBehaviour.EffectID).Mid);

                            float ForcePercent = (ballBehaviour.RealDamage * 60f) / MCP_Utils.AverageMaxHealth(limb.Person);

                            if (KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                            {
                                if (DBPU.EffectiveBeamKnockback > 0f)
                                    rb.AddForce((Vector2)direction * ((((1.2f * TimeScale) * rb.mass) * DBPU.EffectiveBeamKnockback)), ForceMode2D.Impulse);
                            }
                            else
                            {
                                if (ForcePercent > 0.02f && DBPU.Beam_ToggleHeat == true)
                                {
                                    limb.PhysicalBehaviour.BurnProgress += (0.0008f * TimeScale);
                                }

                                if (ForcePercent > 0.005f)
                                {
                                    limb.Damage(ballBehaviour.RealDamage * TimeScale);

                                    if (DBPU.EffectiveBeamKnockback > 0f) rb.AddForce((Vector2)direction * ((((1.2f * TimeScale) * rb.mass * Mathf.Clamp(ForcePercent, 0.25f, 1f)) * DBPU.EffectiveBeamKnockback)), ForceMode2D.Impulse);
                                }
                            }
                        }

                        else if (!col.gameObject.GetComponent<LimbBehaviour>() && col.gameObject.GetComponent<PhysicalBehaviour>())
                        {
                            if (DBPU.Beam_ToggleDelete == true) Destroy(col.gameObject);

                            else
                            {
                                if (DBPU.EffectiveBeamKnockback > 0f)
                                {
                                    rb.AddForce((Vector2)direction * (((2f * TimeScale) * rb.mass * DBPU.EffectiveBeamKnockback)), ForceMode2D.Impulse);
                                }

                                if (DBPU.Beam_ToggleHeat == true)
                                {
                                    col.gameObject.GetComponent<PhysicalBehaviour>().Temperature += (0.4f * TimeScale);
                                }

                                if (DBPU.Beam_ToggleDestroy == true)
                                {
                                    if (col.gameObject.GetComponent<DestroyableBehaviour>())
                                    {
                                        col.gameObject.GetComponent<DestroyableBehaviour>().Break();
                                    }

                                    if (col.gameObject.GetComponent<DamagableMachineryBehaviour>() && col.gameObject.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                                    {
                                        col.gameObject.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                                    }
                                }
                            }
                        }
                    }
                }

                yield return null;
            }
        }
    }
}
