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
    public class CataclysmicOrbBehaviour : MonoBehaviour
    {
        private KiSystem KiSys;
        private MultipliersAndStuffs Multipliers;
        private PersonBehaviour person;
        private BlastBehaviour blastRef;
        private string blastId;

        public bool Active = false;
        public bool Firing = false;
        public float CostPercent = 0.08f;
        public float Damage = 0f;
        public float DamageOutput = 2.5f;
        public float Speed = 110f;

        private void Start()
        {
            person = GetComponent<PersonBehaviour>();
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();
            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();
            blastRef = GetComponent<BlastBehaviour>();
            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(person) / Multipliers.FinalMultiplier) / 30f;
            blastId = gameObject.name + "_CataclysmicOrb";

            CeceUtils.DelayedInvoke(0.25f, RegisterArmTriggers);
        }

        void RegisterArmTriggers()
        {
            if (person == null) return;

            foreach (var limb in person.Limbs)
            {
                if (!MCP_Utils.LimbCanUsePowers(limb) || !limb.name.Contains("LowerArm")) continue;

                var trigger = limb.gameObject.GetOrAddComponent<UseEventTrigger>();
                Action previous = trigger.Action;
                LimbBehaviour capturedLimb = limb;
                trigger.Action = () =>
                {
                    if (!Active || !KiHandLoadout.LegacyAllowed(capturedLimb, "CataclysmicOrb"))
                    {
                        previous?.Invoke();
                        return;
                    }

                    if (!person.IsAlive() || Firing || capturedLimb.Health <= 0f) return;
                    if (KiSys.currentKi < KiSys.maxKi * (CostPercent * 1.5f)) return;
                    StartCoroutine(Fire(capturedLimb.transform));
                };
            }
        }

        Color GetKiColor()
        {
            if (blastRef != null) return blastRef.KiColor(KiSys.CurrentForm);
            return new Color(0.85f, 0.45f, 1f, 0.2f);
        }

        static void SpeedUpParticles(Transform parent, float speed)
        {
            foreach (Transform child in parent)
            {
                var ps = child.gameObject.GetComponent<ParticleSystem>();
                if (ps != null) ps.playbackSpeed *= speed;
            }
        }

        IEnumerator Fire(Transform arm)
        {
            Firing = true;
            KiSys.currentKi -= KiSys.maxKi * CostPercent * DBPU.KiAttackCostMultiplier;

            float realDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;
            Vector3 spawnPos = arm.position + (-arm.up * 0.25f);
            Quaternion rotation = arm.rotation * Quaternion.Euler(0f, 0f, -90f);

            GameObject orb = new GameObject("CataclysmicOrb");
            orb.layer = 9;
            orb.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            orb.transform.position = spawnPos;
            orb.transform.rotation = rotation;
            orb.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            orb.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
            orb.GetComponent<Rigidbody2D>().velocity = orb.transform.right * Speed;
            orb.GetComponent<Rigidbody2D>().fixedAngle = true;
            orb.AddComponent<CircleCollider2D>().radius = 0.45f;

            foreach (var limb in person.Limbs)
                Physics2D.IgnoreCollision(limb.gameObject.GetComponent<Collider2D>(), orb.GetComponent<CircleCollider2D>(), true);

            var blastComp = orb.AddComponent<BlastComponent>();
            blastComp.User = gameObject;
            blastComp.Damage = realDamage;
            blastComp.ID = blastId;
            blastComp.KnockbackMultiplier = 2.5f;
            blastComp.ExplosionSizeMultiplier = 14f;
            blastComp.Technique = "CataclysmicOrb";

            GameObject fireEffect = Instantiate(DBPU.KiBlast_Start, spawnPos, rotation);
            fireEffect.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            SpeedUpParticles(fireEffect.transform, 2.5f);
            var blastColor = fireEffect.transform.Find("Blast Color");
            if (blastColor != null)
            {
                var ps = blastColor.GetComponent<ParticleSystem>();
                if (ps != null) ps.startColor = GetKiColor();
            }

            if (OrbFx.Active) { OrbFx.Mute(fireEffect); OrbFx.Muzzle(spawnPos, -arm.up, new Color(.62f, .26f, 1f), .8f); }
            audioSourcePlay(arm);

            yield return new WaitForSeconds(0.08f);
            if (fireEffect != null) Destroy(fireEffect);
            Firing = false;
        }

        void audioSourcePlay(Transform arm)
        {
            var audio = new GameObject("CataclysmicOrbAudio").AddComponent<AudioSource>();
            audio.transform.SetParent(arm, false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audio, false);
            audio.outputAudioMixerGroup = global.SoundEffects;
            audio.spatialBlend = 1;
            audio.volume = DBPU.PowersVolume;
            audio.maxDistance = 1000;
            audio.PlayOneShot(DBPU.Attack_KiBlast_Fire);
        }
    }
}
