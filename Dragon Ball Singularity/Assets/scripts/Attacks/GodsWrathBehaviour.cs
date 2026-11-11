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
    public class GodsWrathBehaviour : MonoBehaviour
    {
        private KiSystem KiSys;
        private MultipliersAndStuffs Multipliers;
        private PersonBehaviour person;
        private Transform middleBody;
        private PhysicalBehaviour middlePhys;
        private BlastBehaviour blastRef;
        private string blastId;

        public bool Active = false;
        public bool Firing = false;
        public float CostPercent = 0.004f;
        public float ShotInterval = 0.04f;
        public float Damage = 0f;
        public float DamageOutput = 1.35f;
        public float KnockbackMultiplier = 1.35f;
        public float ExplosionSizeMultiplier = 1.2f;
        public float Speed = 95f;

        float nextShotTime;

        private void Start()
        {
            person = GetComponent<PersonBehaviour>();
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();
            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();
            blastRef = GetComponent<BlastBehaviour>();
            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(person) / Multipliers.FinalMultiplier) / 120f;
            blastId = gameObject.name + "_GodsWrath";

            middleBody = person.transform.Find("Body/MiddleBody");
            if (middleBody == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb.name.Contains("MiddleBody"))
                    {
                        middleBody = limb.transform;
                        break;
                    }
                }
            }

            if (middleBody != null) middlePhys = middleBody.GetComponent<PhysicalBehaviour>();
        }

        // Set by the torso technique picker: fires as if the middle body were held in use.
        public float ExternalHoldUntil = -1f;

        public string HoldFromPicker(float seconds)
        {
            if (person == null || !person.IsAlive() || middlePhys == null) return "BLOCKED";
            var limb = middleBody != null ? middleBody.GetComponent<LimbBehaviour>() : null;
            if (limb != null && limb.Health <= 0f) return "HURT";
            if (KiSys != null && KiSys.currentKi < KiSys.maxKi * (CostPercent * 3f)) return "LOW KI";
            ExternalHoldUntil = Time.time + seconds;
            return null;
        }

        private void Update()
        {
            bool external = Time.time <= ExternalHoldUntil;
            if ((!Active && !external) || person == null || !person.IsAlive() || middlePhys == null) return;
            // Use still held from confirming another torso technique does not start the barrage.
            if (!external && (!middlePhys.IsBeingUsedContinuously() || TorsoTechniquePicker.HoldsUse(person))) return;
            if (Firing || KiSys.currentKi < KiSys.maxKi * (CostPercent * 3f)) return;
            if (Time.time < nextShotTime) return;

            nextShotTime = Time.time + ShotInterval;
            StartCoroutine(FireShot());
        }

        Color GetKiColor()
        {
            if (blastRef != null) return blastRef.KiColor(KiSys.CurrentForm);
            return new Color(0.75f, 0.35f, 1f, 0.12f);
        }

        static void SpeedUpParticles(Transform parent, float speed)
        {
            foreach (Transform child in parent)
            {
                var ps = child.gameObject.GetComponent<ParticleSystem>();
                if (ps != null) ps.playbackSpeed *= speed;
            }
        }

        IEnumerator FireShot()
        {
            Firing = true;
            KiSys.currentKi -= KiSys.maxKi * CostPercent * DBPU.KiAttackCostMultiplier;

            float realDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;
            float angle = Random.Range(0f, 360f);
            Vector3 origin = middleBody.position;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject blast = new GameObject("GodsWrathBlast");
            blast.layer = 9;
            blast.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            blast.transform.position = origin;
            blast.transform.rotation = rotation * Quaternion.Euler(0f, 0f, -90f);
            blast.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            blast.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
            blast.GetComponent<Rigidbody2D>().velocity = blast.transform.right * Speed;
            blast.GetComponent<Rigidbody2D>().fixedAngle = true;
            blast.AddComponent<CircleCollider2D>().radius = 0.55f;

            foreach (var limb in person.Limbs)
                Physics2D.IgnoreCollision(limb.gameObject.GetComponent<Collider2D>(), blast.GetComponent<CircleCollider2D>(), true);

            var blastComp = blast.AddComponent<BlastComponent>();
            blastComp.User = gameObject;
            blastComp.Damage = realDamage;
            blastComp.ID = blastId;
            blastComp.KnockbackMultiplier = KnockbackMultiplier;
            blastComp.ExplosionSizeMultiplier = ExplosionSizeMultiplier * Mathf.Max(0.1f, DBPU.KiBlast_ExplosionSizeMultiplier);
            blastComp.Technique = "GodsWrath"; blastComp.Tint = GetKiColor(); blastComp.ShotSize = .38f;

            GameObject fireEffect = Instantiate(DBPU.KiBlast_Start, origin, rotation * Quaternion.Euler(0f, 0f, -90f));
            fireEffect.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            SpeedUpParticles(fireEffect.transform, 2.2f);
            var blastColor = fireEffect.transform.Find("Blast Color");
            if (blastColor != null)
            {
                var ps = blastColor.GetComponent<ParticleSystem>();
                if (ps != null) ps.startColor = GetKiColor();
            }

            if (OrbFx.Active) OrbFx.Mute(fireEffect);
            yield return new WaitForSeconds(0.05f);
            if (fireEffect != null) Destroy(fireEffect);
            Firing = false;
        }
    }
}
