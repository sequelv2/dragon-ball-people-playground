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
    public class GodPunisher : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;

        public bool Active = false;
        public bool Firing = false;
        public float RealDamage = 100f;
        public float CostPercent = 0.05f;

        private Quaternion TargetRotation;

        public float Damage = 0f;
        public float Speed = 80f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;
        public int blastsAmount = 100;
        public string ID = "Blast";

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 60f;

            ID = gameObject.name + "_Blast" + Random.Range(0, 999);

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, "GodPunisher")) return;
                        if (!limb.Person.IsAlive() || limb.Health <= 0f || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 4f)) return;

                        StartCoroutine(Fire(limb.transform));
                    };
                }
            }
        }

        private IEnumerator Fire(Transform Arm)
        {
            Firing = true;

            StartCoroutine(KiCost());

            RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

            TargetRotation = Arm.rotation;

            Arm.gameObject.AddComponent<FreezeBehaviour>();

            for (int i = 0; i < blastsAmount; i++)
            {
                GameObject Blast = new GameObject("KiBlast");
                Blast.gameObject.layer = 9;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.transform.position = Arm.position + (-Arm.up * 0.3f);
                float Angle = Random.Range(-10f, 10f);
                Blast.transform.rotation = TargetRotation * Quaternion.Euler(0, 0, Angle);
                Blast.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                Blast.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
                //Blast.gameObject.GetComponent<Rigidbody2D>().fixedAngle = true;
                if (Blast.gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) Blast.gameObject.GetComponent<Rigidbody2D>().velocity = -Blast.transform.up * Speed;
                Blast.gameObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
                Blast.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;
                Blast.gameObject.AddComponent<BlastVFX>();

                Blast.gameObject.AddComponent<BlastComponent>().User = gameObject;
                Blast.gameObject.GetComponent<BlastComponent>().Damage = RealDamage;
                Blast.gameObject.GetComponent<BlastComponent>().ID = ID;
                Blast.gameObject.GetComponent<BlastComponent>().KnockbackMultiplier = 0.08f * KnockbackOutput;
                Blast.gameObject.GetComponent<BlastComponent>().ExplosionSizeMultiplier = 3f;
                Blast.gameObject.GetComponent<BlastComponent>().HandleSelfMovement = false;
                Blast.gameObject.GetComponent<BlastComponent>().Technique = "GodPunisher";
                Blast.gameObject.GetComponent<BlastComponent>().Tint = new Color(0.25f, 0.65f, 1f);
                Blast.gameObject.GetComponent<BlastComponent>().ShotSize = .4f;

                StartCoroutine(DirectionChange(Blast.transform, Angle));

                foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                    Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Blast.gameObject.GetComponent<CircleCollider2D>(), true);

                yield return new WaitForSeconds(0.02f);
            }

            if (Arm.gameObject.GetComponent<FreezeBehaviour>()) Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());

            yield return new WaitForSeconds(2f);

            Firing = false;

            yield return null;
        }

        private IEnumerator DirectionChange(Transform Blast, float Angle)
        {
            Rigidbody2D rb = Blast.GetComponent<Rigidbody2D>();

            float maxSpeed = 25f;
            float minSpeed = 5f;

            float slowStartAngle = 10f;

            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(Blast.eulerAngles.z, TargetRotation.eulerAngles.z));
            float DesiredAngleDiff = angleDiff / 6f;
            BlastComponent comp = Blast.GetComponent<BlastComponent>();

            while (Blast && !comp.Redirected && !comp.Held && Mathf.Abs(Mathf.DeltaAngle(Blast.eulerAngles.z, TargetRotation.eulerAngles.z)) > 0.1f)
            {
                angleDiff = Mathf.Abs(Mathf.DeltaAngle(Blast.eulerAngles.z, TargetRotation.eulerAngles.z));

                float t = Mathf.Clamp01(angleDiff / slowStartAngle);
                float changeSpeed = Mathf.Lerp(minSpeed, maxSpeed, t);

                Blast.rotation = Quaternion.RotateTowards(
                    Blast.rotation,
                    TargetRotation,
                    changeSpeed * Time.fixedDeltaTime
                );

                if (rb.bodyType != RigidbodyType2D.Static) rb.velocity = -Blast.up * Speed;

                yield return new WaitForFixedUpdate();
            }
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

        public class BlastVFX : MonoBehaviour
        {
            private GameObject VFX;

            public void Start()
            {
                if (OrbFx.Active) { enabled = false; return; }
                VFX = new GameObject("KiBlastVFX");
                VFX.transform.SetParent(transform, true);
                VFX.transform.position = transform.position;
                VFX.transform.localScale = new Vector3(0.044f, 0.05f, 1f);
                VFX.AddComponent<SpriteRenderer>().sprite = DBPU.DenseKiBlast_Sprite;
                VFX.GetComponent<SpriteRenderer>().material = ModAPI.FindMaterial("VeryBright");
                VFX.GetComponent<SpriteRenderer>().color = new Color(0.25f, 0.65f, 1f, 0.05f) /*new Color(1f, 1f, 1f, 0.05f)*/;
            }

            public void Update()
            {
                VFX.transform.position = transform.position;
                VFX.transform.eulerAngles += new Vector3(0f, 0f, 1200f * Time.deltaTime);
            }
        }
    }
}
