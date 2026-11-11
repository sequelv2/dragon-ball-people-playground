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
    public class StardustBreaker : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;
        private AudioSource audioSource;

        private GameObject Ball;
        private Transform UsedArm;
        private float chargeSavedRegen;
        private bool chargeSuppressesRegen;

        public bool Active = false;
        public bool Charging = false;
        public bool FullCharged = false;
        public bool StillActive = false;
        public float RealDamage = 7500f;
        public float CostPercent = 0.1f;

        public float Damage = 0f;
        public float Speed = 80f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;

        // The launched projectile has its own Rigidbody2D and BlastComponent.
        // Cancel only the orb still held by the hand; fired attacks remain in flight.
        public void CancelHeldCharge()
        {
            Active = false;
            if (!Charging) return;
            StopAllCoroutines();
            Charging = false;
            FullCharged = false;
            StillActive = false;
            UsedArm = null;
            if (Ball != null) { Destroy(Ball); Ball = null; }
            if (chargeSuppressesRegen && KiSys != null && KiSys.baseRegen == 0f)
                KiSys.baseRegen = chargeSavedRegen;
            chargeSuppressesRegen = false;
        }
        private void OnDestroy()
        {
            CancelHeldCharge();
            if (Ball != null) Destroy(Ball);
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

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 40f;

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, "StardustBreaker")) return;
                        if ((Charging == false && KiSys.currentKi < KiSys.maxKi * CostPercent) || (StillActive == true && Charging == false)) return;

                        if (limb.Health > 0f && Active == true && Charging == false)
                        {
                            UsedArm = limb.transform;
                            StartCoroutine(StartCharge(limb.transform));
                        }

                        else if (Active == true && Charging == true && FullCharged == true && UsedArm == limb.transform)
                        {
                            StillActive = true;
                        }
                    };
                }
            }
        }

        private void FixedUpdate()
        {
            if (Charging && (!Active || UsedArm == null || Ball == null || UsedArm.GetComponent<LimbBehaviour>().Health <= 0f))
            { CancelHeldCharge(); return; }
            if (Charging == true)
            {
                RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

                if (Ball != null)
                {
                    Ball.GetComponent<Rigidbody2D>().MovePosition(Vector3.Lerp(Ball.transform.position, (UsedArm.position + (-UsedArm.up * 0.175f) + (Vector3.up * 0.4f)), 20f * Time.deltaTime));

                    if (Active == false) Destroy(Ball);
                }
            }
        }

        private IEnumerator StartCharge(Transform Arm)
        {
            Charging = true;

            StartCoroutine(KiCost());

            Ball = Instantiate(DBPU.StardustBreaker, Arm.position + (-UsedArm.up * 0.175f) + (Vector3.up * 0.4f), Quaternion.identity);
            Ball.transform.localScale = new Vector3(0f, 0f, 0f);
            Ball.layer = 9;
            //Ball.transform.SetParent(Arm, false);

            Ball.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Ball.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;

            //ChangePlayBackOfAllParticleSystems(Ball.transform, 0.42f);

            AudioSource BallAudio = new GameObject("BallAudioSource").AddComponent<AudioSource>();
            BallAudio.transform.SetParent(Ball.transform, false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(BallAudio, false);
            BallAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            BallAudio.outputAudioMixerGroup = global.SoundEffects;
            BallAudio.spatialBlend = 1;
            BallAudio.volume = 1;
            BallAudio.maxDistance = 1000;

            BallAudio.PlayOneShot(DBPU.Attack_StardustBreaker_Charging);

            float size = 0f;

            while (size < 0.2f)
            {
                size = Mathf.Lerp(size, 0.205f, 1.5f * Time.deltaTime);
                Ball.transform.localScale = new Vector3(size, size, size);

                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForSeconds(0.6f);

            AudioSource AudioLoop = new GameObject("BallAudioSource").AddComponent<AudioSource>();
            AudioLoop.transform.SetParent(Ball.transform, false);
            global = FindObjectOfType<Global>();
            global.AddAudioSource(AudioLoop, false);
            AudioLoop.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            AudioLoop.outputAudioMixerGroup = global.SoundEffects;
            AudioLoop.spatialBlend = 1;
            AudioLoop.volume = 0.2f;
            AudioLoop.maxDistance = 1000;
            AudioLoop.clip = DBPU.Attack_StardustBreaker_Loop;
            AudioLoop.loop = true;
            AudioLoop.Play();

            FullCharged = true;

            while (StillActive == false && Arm.GetComponent<LimbBehaviour>().Health > 0f)
            {
                yield return null;
            }

            if (Arm.GetComponent<LimbBehaviour>().Health > 0f) StartCoroutine(Release(Arm, BallAudio));

            else
            {
                FullCharged = false;
                Charging = false;

                Destroy(Ball);
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
            Charging = false;

            BallAudio.PlayOneShot(DBPU.Attack_KiBlast_Fire);

            /*if (DBPU.VoiceLines)
            {
                if (Character == "Frieza") BeamAudio.PlayOneShot(DBPU.Voice_Goku_HA);
                }*/

            Rigidbody2D rb = Ball.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (rb.bodyType != RigidbodyType2D.Static) rb.velocity = -Arm.up * Speed;
            Ball.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;

            foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Ball.gameObject.GetComponent<CircleCollider2D>(), true);

            Ball.gameObject.AddComponent<BlastComponent>().User = gameObject;
            Ball.gameObject.GetComponent<BlastComponent>().Damage = RealDamage;
            Ball.gameObject.GetComponent<BlastComponent>().ID = "Gogeta" + Random.Range(0, 100);
            Ball.gameObject.GetComponent<BlastComponent>().HandleSelfMovement = false;
            Ball.gameObject.GetComponent<BlastComponent>().ExplosionSizeMultiplier = 5f;
            Ball.gameObject.GetComponent<BlastComponent>().KnockbackMultiplier = 0.5f * KnockbackOutput;
            Ball.gameObject.GetComponent<BlastComponent>().Technique = "StardustBreaker";

            StillActive = true;

            yield return new WaitWhile(() => Ball != null);

            StillActive = false;

            yield return null;
        }
    }
}
