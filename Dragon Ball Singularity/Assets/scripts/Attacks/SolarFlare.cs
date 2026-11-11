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
    public class SolarFlare : MonoBehaviour
    {
        private KiSystem KiSys;
        private AudioSource audioSource;
        private AudioSource audioSource2;

        public bool Active = false;
        public bool Using = false;
        public bool Firing = false;
        public float CostPercent = 0.25f;
        public float flareDuration = 4.25f;
        public float brightnessOutput = 1f;

        public string Character = "Human";

        public List<PersonBehaviour> Persons = new List<PersonBehaviour>();

        public LightSprite Light;

        private void OnDestroy()
        {
            if (Light != null) Destroy(Light);
        }

        private void Awake()
        {
            audioSource = new GameObject("AudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Head"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.85f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = DBPU.PowersVolume;
            audioSource.maxDistance = 1000;

            audioSource2 = new GameObject("AudioSource").AddComponent<AudioSource>();
            audioSource2.transform.SetParent(transform.Find("Head"), false);
            Global global2 = FindObjectOfType<Global>();
            global2.AddAudioSource(audioSource2, false);
            audioSource2.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.85f;
            audioSource2.outputAudioMixerGroup = global2.SoundEffects;
            audioSource2.spatialBlend = 1;
            audioSource2.volume = DBPU.VoiceLineVolume;
            audioSource2.maxDistance = 1000;
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, "SolarFlare")) return;
                        if (!limb.Person.IsAlive() || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 1.5f)) return;

                        StartCoroutine(Fire());
                    };
                }
            }
        }

        private void FixedUpdate()
        {
            if (audioSource.volume != DBPU.PowersVolume) audioSource.volume = DBPU.PowersVolume;
            if (audioSource2.volume != DBPU.VoiceLineVolume) audioSource2.volume = DBPU.VoiceLineVolume;

            if (Using == true)
            {
                foreach (Collider2D collider2D in Physics2D.OverlapCircleAll(transform.Find("Head").transform.position, 20f, LayerMask.GetMask("Objects", "CollidingDebris")))
                {
                    if (collider2D.gameObject.GetComponent<LimbBehaviour>() && collider2D.transform.root.gameObject != gameObject && !Persons.Contains(collider2D.gameObject.GetComponent<LimbBehaviour>().Person))
                    {
                        Persons.Add(collider2D.gameObject.GetComponent<LimbBehaviour>().Person);
                        StartCoroutine(Stunned(collider2D.gameObject.GetComponent<LimbBehaviour>().Person));
                    }
                }
            }
        }

        private IEnumerator Stunned(PersonBehaviour Person)
        {
            while (Using == true)
            {
                if (Person.ActivePose.State != PoseState.Protective)
                {
                    /*Vector3 direction = (transform.Find("Head").transform.position - Person.transform.Find("Head").transform.position).normalized;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    float angle2 = Person.transform.Find("Head").transform.localEulerAngles.z + (Person.transform.localScale.x > 0f ? 90f : -90f);

                    ModAPI.Notify(Mathf.Abs(((angle2 % 360 + 360) % 360) - angle));

                    if (Mathf.Abs(angle - angle2) < 50)
                    {*/
                    Person.OverridePoseIndex = 1;
                    //}
                }

                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForSeconds(5f);

            if (Person.ActivePose.State != PoseState.Rest)
            {
                Person.OverridePoseIndex = 0;
            }

            yield return null;
        }

        private IEnumerator Fire()
        {
            Firing = true;
            Using = true;

            CostPercent = 0.25f;
            StartCoroutine(KiCost());

            if (DBPU.VoiceLines)
            {
                if (Character == "Goku") audioSource2.PlayOneShot(DBPU.Voice_Goku_SolarFlare);
                if (Character == "Krillin") audioSource2.PlayOneShot(DBPU.Voice_Krillin_SolarFlare);
                if (Character == "Tien") audioSource2.PlayOneShot(DBPU.Voice_Tien_SolarFlare);
            }

            audioSource.PlayOneShot(DBPU.Attack_SolarFlare_Fire);

            Light = ModAPI.CreateLight(transform.Find("Head").transform, new Color(1f, 1f, 1f, 1f), 1f, 0.1f);

            AudioSource audioSourceLoop = new GameObject("LoopAudioSource").AddComponent<AudioSource>();
            audioSourceLoop.transform.SetParent(Light.transform, false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSourceLoop, false);
            audioSourceLoop.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.85f;
            audioSourceLoop.outputAudioMixerGroup = global.SoundEffects;
            audioSourceLoop.spatialBlend = 1;
            audioSourceLoop.volume = 0;
            audioSourceLoop.maxDistance = 1000;

            audioSourceLoop.clip = DBPU.Attack_SolarFlare_Loop;
            audioSourceLoop.loop = true;
            audioSourceLoop.Play();

            StartCoroutine(SoundManagement(audioSourceLoop));

            if (GetComponent<PersonBehaviour>().ActivePose.State == PoseState.Protective)
            {
                GetComponent<PersonBehaviour>().OverridePoseIndex = 0;
            }

            float angle = 0f;

            float targetBrightness = 35f * brightnessOutput;

            while (Light.Brightness < targetBrightness * 0.85f)
            {
                Light.transform.localRotation = Quaternion.Euler(0f, 0f, angle + (25f * Time.deltaTime));

                Light.Brightness = Mathf.Lerp(Light.Brightness, targetBrightness, 2 * Time.deltaTime);
                Light.Radius = Mathf.Lerp(Light.Radius, 100f, 2f * Time.deltaTime);

                yield return null;
            }

            float time = 0f;

            while (time < flareDuration)
            {
                Light.transform.localRotation = Quaternion.Euler(0f, 0f, angle + (25f * Time.deltaTime));

                time += Time.deltaTime;

                yield return null;
            }

            Using = false;

            while (Light.Brightness > 0f)
            {
                Light.transform.localRotation = Quaternion.Euler(0f, 0f, angle + (25f * Time.deltaTime));

                Light.Brightness = Mathf.Lerp(Light.Brightness, -5f, 2f * Time.deltaTime);
                Light.Radius = Mathf.Lerp(Light.Radius, -6f, 2f * Time.deltaTime);

                yield return null;
            }

            Firing = false;
            Destroy(Light);

            yield return new WaitForSeconds(2f);

            Persons.Clear();

            yield return null;
        }

        private IEnumerator SoundManagement(AudioSource Loop)
        {
            while (Loop.volume < 1f)
            {
                Loop.volume += 0.8f * Time.deltaTime;

                yield return null;
            }

            yield return new WaitUntil(() => Using == false);

            while (Loop.volume > 0f)
            {
                Loop.volume -= 0.8f * Time.deltaTime;

                yield return null;
            }

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
                KiSys.currentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime * 0.25f);
                CurrentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime * 0.25f);

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;

            yield return null;
        }
    }
}
