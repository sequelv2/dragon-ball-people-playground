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
    public class StardustFall : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;

        public bool Active = false;
        public bool Firing = false;
        public float RealDamage = 100f;
        public float CostPercent = 0.01f;

        private Quaternion TargetRotation;

        public float Damage = 0f;
        public float Speed = 120f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;
        public int blastsAmount = 160;
        public float durationSeconds = 2f;
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
                        if (!KiHandLoadout.LegacyAllowed(limb, "StardustFall")) return;
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
                Blast.gameObject.layer = 13;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.transform.position = Arm.position + (-Arm.up * 0.3f);
                float Angle = Random.Range(-78f, 78f);
                Blast.transform.rotation = TargetRotation * Quaternion.Euler(0, 0, Angle);
                Blast.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                Blast.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
                //Blast.gameObject.GetComponent<Rigidbody2D>().fixedAngle = true;
                if (Blast.gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) Blast.gameObject.GetComponent<Rigidbody2D>().velocity = -Blast.transform.up * Speed;
                Blast.gameObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
                Blast.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;
                var tr = Blast.gameObject.AddComponent<TrailRenderer>();
                Color starttrailColor = new Color(0f, Random.Range(0.6f, 0.85f), Random.Range(0.85f, 1f), Random.Range(0.005f, 0.02f));
                Color endtrailColor = new Color(0f, Random.Range(0.6f, 0.85f), Random.Range(0.85f, 1f), Random.Range(0.02f, 0.04f));
                tr.startColor = starttrailColor;
                tr.endColor = endtrailColor;
                //tr.widthMultiplier = 0.4f;
                tr.startWidth = 0.25f;
                tr.endWidth = 0.25f;
                tr.time = 1.5f;
                tr.numCapVertices = 8;
                tr.numCornerVertices = 8;
                tr.textureMode = LineTextureMode.Stretch;
                tr.material = ModAPI.FindMaterial("VeryBright");

                Blast.gameObject.AddComponent<BlastComponent>().User = gameObject;
                Blast.gameObject.GetComponent<BlastComponent>().Damage = RealDamage;
                Blast.gameObject.GetComponent<BlastComponent>().ID = ID;
                Blast.gameObject.GetComponent<BlastComponent>().KnockbackMultiplier = 0.1f * KnockbackOutput;
                Blast.gameObject.GetComponent<BlastComponent>().HandleSelfMovement = false;
                Blast.gameObject.GetComponent<BlastComponent>().Technique = "StardustFall";
                Blast.gameObject.GetComponent<BlastComponent>().Tint = new Color(.3f, .85f, 1f);
                Blast.gameObject.GetComponent<BlastComponent>().ShotSize = .3f;
                Blast.gameObject.GetComponent<BlastComponent>().ShotTail = .01f;

                StartCoroutine(Animation(tr));
                StartCoroutine(DirectionChange(Blast.transform, Angle));

                foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                    Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Blast.gameObject.GetComponent<CircleCollider2D>(), true);

                yield return new WaitForSeconds(0.02f);
            }

            if (Arm.gameObject.GetComponent<FreezeBehaviour>()) Destroy(Arm.gameObject.GetComponent<FreezeBehaviour>());

            yield return new WaitForSeconds(durationSeconds);

            Firing = false;

            yield return null;
        }

        private IEnumerator DirectionChange(Transform Blast, float Angle)
        {
            Rigidbody2D rb = Blast.GetComponent<Rigidbody2D>();

            float maxSpeed = 55f;
            float minSpeed = 2f;

            float slowStartAngle = 35f;
            BlastComponent comp = Blast.GetComponent<BlastComponent>();

            while (Blast && !comp.Redirected && !comp.Held && Mathf.Abs(Mathf.DeltaAngle(Blast.eulerAngles.z, TargetRotation.eulerAngles.z)) > 0.1f)
            {
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(Blast.eulerAngles.z, TargetRotation.eulerAngles.z));

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

        private IEnumerator Animation(TrailRenderer tr)
        {
            for (int i = 0; i < 80; i++)
            {
                if (!tr) yield break;
                tr.startWidth += Random.Range(0.005f, 0.02f);
                tr.gameObject.GetComponent<CircleCollider2D>().radius += 0.02f;

                yield return new WaitForFixedUpdate();
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
                KiSys.currentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime);
                CurrentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * (Time.deltaTime);

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;

            yield return null;
        }
    }
}
