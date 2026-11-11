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
    public class MeteorExplosion : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;
        private AudioSource audioSource;

        public int barrageAmount = 0;

        public bool Active = false;
        public bool Firing = false;
        public GameObject Target;
        public float RealDamage = 100f;
        public float CostPercent = 0.2f;

        public float Damage = 100f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;

        private void Awake()
        {
            audioSource = new GameObject("VoiceLinesSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Head"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 0.98f;
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 8f;

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, "MeteorExplosion")) return;
                        if (!limb.Person.IsAlive() || limb.Health <= 0f || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 4f)) return;

                        var hit = Physics2D.Raycast(limb.transform.position, -limb.transform.up, 50f);

                        if (hit.transform == null || hit.transform.root.gameObject == gameObject || !hit.transform.root.gameObject.GetComponent<PersonBehaviour>() || hit.transform.gameObject.layer == 11 || hit.transform.gameObject.layer == 13) return;

                        Target = hit.transform.gameObject;

                        StartCoroutine(Fire(limb.transform));
                    };
                }
            }
        }

        private IEnumerator Fire(Transform Arm)
        {
            Firing = true;

            Vector3 TargetPosition = (Target.gameObject.GetComponent<LimbBehaviour>().Person.Limbs.Length >= 3) ? Target.gameObject.GetComponent<LimbBehaviour>().Person.Limbs[2].transform.position : MCP_Utils.GetMiddlePositionOfBody(Target.GetComponent<LimbBehaviour>().Person);

            Vector2 contactPoint = Physics2D.Raycast(TargetPosition, -Vector2.up, 1000f, LayerMask.GetMask("Bounds")).point;

            foreach (var limb in Target.GetComponent<LimbBehaviour>().Person.Limbs) limb.gameObject.AddComponent<FreezeBehaviour>();

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs) limb.gameObject.AddComponent<FreezeBehaviour>();

            CostPercent = 0.2f;
            StartCoroutine(KiCost());

            RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

            AudioSource audioSource2 = new GameObject("AttackSource").AddComponent<AudioSource>();
            audioSource2.transform.SetParent(transform.Find("Head"), true);
            audioSource2.transform.position = TargetPosition;
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource2, false);
            audioSource2.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.8f;
            audioSource2.outputAudioMixerGroup = global.SoundEffects;
            audioSource2.spatialBlend = 0.95f;
            audioSource2.volume = 1;
            audioSource2.maxDistance = 1000;

            if (DBPU.VoiceLines == true)
            {
                audioSource.PlayOneShot(DBPU.Voice_Gogeta_MeteorExplosion);
                audioSource2.PlayOneShot(DBPU.Attack_MeteorExplosion_Fire_Extended);

                yield return new WaitForSeconds(3.2f);

                GameObject Effect1 = GameObject.Instantiate(DBPU.MeteorExplosion_WindArea, contactPoint, Quaternion.identity);
                Effect1.layer = 9;
                Effect1.AddComponent<DestroyOverTime>().Timer = 5.7f;
            }

            else
            {
                GameObject Effect1 = GameObject.Instantiate(DBPU.MeteorExplosion_WindArea, contactPoint, Quaternion.identity);
                Effect1.layer = 9;
                Effect1.AddComponent<DestroyOverTime>().Timer = 5.7f;

                yield return new WaitForSeconds(0.15f);

                audioSource2.PlayOneShot(DBPU.Attack_MeteorExplosion_Fire);
            }

            GameObject Effect2 = GameObject.Instantiate(DBPU.MeteorExplosion_AuraDrag, TargetPosition, Quaternion.identity);
            Effect2.transform.localScale = new Vector3(1f, 1f, 1f);
            Effect2.layer = 9;
            //GetAllChildrenRecursive(Effect2.transform);
            Effect2.AddComponent<DestroyOverTime>().Timer = 3.8f;

            yield return new WaitForSeconds(1.1f);

            GameObject Effect3 = GameObject.Instantiate(DBPU.MeteorExplosion_LightBeams, TargetPosition, Quaternion.identity);
            Effect3.layer = 9;
            GetAllChildrenRecursive(Effect3.transform);
            Effect3.AddComponent<DestroyOverTime>().Timer = 3.2f;

            yield return new WaitForSeconds(2f);

            StopAllParticleSystems(Effect2.transform);

            yield return new WaitForSeconds(0.8f);

            GameObject Effect4 = GameObject.Instantiate(DBPU.MeteorExplosion_EnergyPillar, contactPoint, Quaternion.identity);
            Effect4.layer = 9;
            GetAllChildrenRecursive(Effect4.transform);
            Effect4.AddComponent<DestroyOverTime>().Timer = 1.9f;

            yield return new WaitForSeconds(1.7f);

            GameObject Effect5 = GameObject.Instantiate(DBPU.MeteorExplosion_GiganticExplosion, contactPoint, Quaternion.identity);
            Effect5.transform.localScale = new Vector3(20f, 20f, 20f);
            Effect5.layer = 9;
            GetAllChildrenRecursive(Effect5.transform);
            Effect5.AddComponent<DestroyOverTime>().Timer = 8f;

            foreach (Transform child in Effect5.transform)
            {
                var ps = child.gameObject.GetComponent<ParticleSystem>();
                ps.playbackSpeed *= 10f;
            }

            yield return new WaitForSeconds(0.2f);

            foreach (Transform child in Effect5.transform)
            {
                var ps = child.gameObject.GetComponent<ParticleSystem>();
                ps.playbackSpeed /= 10f;
            }

            foreach (var limb in Target.GetComponent<LimbBehaviour>().Person.Limbs)
            {
                limb.Health -= Mathf.Clamp(RealDamage, 0, limb.Health);
                Destroy(limb.gameObject.GetComponent<FreezeBehaviour>());
            }

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs) Destroy(limb.gameObject.GetComponent<FreezeBehaviour>());

            yield return new WaitForSeconds(5f);

            Destroy(audioSource2.gameObject);

            Firing = false;

            yield return null;
        }

        private IEnumerator KiCost()
        {
            if (DBPU.KiAttackCostMultiplier == 0f) yield break;

            float OGKiDrain = KiSys.currentKiDrainRate;
            float KiRegen = KiSys.baseRegen;

            if (OGKiDrain != 0f) KiSys.baseRegen = 0f;

            float FinalKi = KiSys.currentKi - (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier));
            float CurrentKi = KiSys.currentKi;

            while (CurrentKi > FinalKi)
            {
                KiSys.currentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime);
                CurrentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime);

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;

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

        void GetAllChildrenRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.position = new Vector3(child.position.x, child.position.y, 0f);
                var sp = child.gameObject.GetComponent<Renderer>();
                sp.sortingLayerName = "Bubbles";
                sp.sortingOrder += 100;

                GetAllChildrenRecursive(child);
            }
        }
    }
}
