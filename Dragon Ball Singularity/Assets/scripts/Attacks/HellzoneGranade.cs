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
    public class HellzoneGranade : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;

        public int barrageAmount = 0;

        public bool Active = false;
        public bool Firing = false;
        public bool Chase = false;
        public GameObject Target;
        public Vector3 TargetPosition;
        public float RealDamage = 100f;
        public float CostPercent = 0.1f;

        public List<GameObject> Blasts = new List<GameObject>();

        public float Damage = 0f;
        public float Speed = 40f;
        public float DamageOutput = 1f;
        public float KnockbackMultiplier = 1f;
        public int blastsAmount = 80;
        public string ID = "Blast";

        private void OnDestroy()
        {
            if (Blasts.Any())
            {
                foreach (var Blast in Blasts)
                {
                    if (Blast != null) Destroy(Blast);
                }
            }
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 50f;

            ID = gameObject.name + "_Blast" + UnityEngine.Random.Range(0, 999);

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, "HellzoneGranade")) return;
                        if (!limb.Person.IsAlive() || limb.Health <= 0f || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 4f)) return;

                        var hit = Physics2D.Raycast(limb.transform.position, -limb.transform.up, 50f);

                        if (hit.transform == null || hit.transform.root.gameObject == gameObject || hit.transform.gameObject.layer == 11 || hit.transform.gameObject.layer == 13) return;

                        Target = hit.transform.gameObject;

                        StartCoroutine(Fire(limb.transform));
                    };
                }
            }
        }

        private IEnumerator Fire(Transform Arm)
        {
            Firing = true;
            Chase = false;

            CostPercent = 0.1f;
            StartCoroutine(KiCost());

            RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;

            for (int i = 0; i < blastsAmount; i++)
            {
                Vector2 randomPoint = Random.insideUnitCircle * 20f;
                Vector3 finalPos = Target.transform.position + new Vector3(randomPoint.x, randomPoint.y);

                /*GameObject Blast = Instantiate(DBPU.KiBlast_End, Arm.position, Arm.rotation * Quaternion.Euler(0, 0, -90));
                Blast.gameObject.layer = 9;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.gameObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                Blast.gameObject.GetComponent<CircleCollider2D>().isTrigger = true;
                Blast.gameObject.GetComponent<CircleCollider2D>().radius = 2.2f;
                Blast.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = KiColor(KiSys.CurrentForm);*/

                GameObject Blast = new GameObject("KiBlast");
                Blast.gameObject.layer = 10;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.transform.position = Arm.position + (-Arm.up * 0.3f);
                Blast.transform.rotation = Arm.rotation * Quaternion.Euler(0, 0, -90);
                Blast.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                Blast.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
                Blast.gameObject.GetComponent<Rigidbody2D>().fixedAngle = true;
                Blast.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;
                Blast.gameObject.AddComponent<BlastVFX>().KiSys = KiSys;

                foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                    Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Blast.gameObject.GetComponent<CircleCollider2D>(), true);

                Vector3 direction = finalPos - Blast.transform.position;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Blast.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                Blasts.Add(Blast);

                /*GetAllChildrenRecursive(Blast.transform);

                foreach (Transform child in Blast.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sp.sortingOrder += 10;
                    }

                ChangePlayBackOfAllParticleSystems(Blast.transform, 2f);*/

                GameObject FireEffect = Instantiate(DBPU.KiBlast_Start, Arm.position - (Arm.up * 0.3f), Arm.rotation * Quaternion.Euler(0, 0, -90));
                FireEffect.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                ChangePlayBackOfAllParticleSystems(FireEffect.transform, 2f);
                FireEffect.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = KiColor(KiSys.CurrentForm);

                GetAllChildrenRecursive(FireEffect.transform);

                foreach (Transform child in FireEffect.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sp.sortingOrder += 10;
                }

                if (OrbFx.Active)
                {
                    OrbFx.Mute(FireEffect);
                    OrbShots.Add(Blast.transform, KiColor(KiSys.CurrentForm), .5f, .03f, 5f);
                }

                if (barrageAmount < 10)
                {
                    AudioSource BlastAudio = new GameObject("BlastAudioSource").AddComponent<AudioSource>();
                    BlastAudio.transform.SetParent(FireEffect.transform, false);
                    Global global = FindObjectOfType<Global>();
                    global.AddAudioSource(BlastAudio, false);
                    BlastAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
                    BlastAudio.outputAudioMixerGroup = global.SoundEffects;
                    BlastAudio.spatialBlend = 1;
                    BlastAudio.volume = DBPU.PowersVolume;
                    BlastAudio.maxDistance = 1000;
                    BlastAudio.PlayOneShot(DBPU.Attack_KiBlast_Fire);

                    barrageAmount += 1;
                }

                else
                {
                    barrageAmount = 0;
                }

                StartCoroutine(FollowTarget(Blast, FireEffect, finalPos));

                yield return new WaitForSeconds(0.025f);
            }

            yield return new WaitForSeconds(1.5f);

            TargetPosition = Target.transform.position;

            Chase = true;

            yield return new WaitForSeconds(5f);

            Blasts.Clear();
            Firing = false;

            yield return null;
        }

        private IEnumerator FollowTarget(GameObject Blast, GameObject FireEffect, Vector3 StopPosition)
        {
            while (Vector3.Distance(Blast.transform.position, StopPosition) > 0.2f)
            {
                Blast.transform.position = Vector3.Lerp(Blast.transform.position, StopPosition, Time.deltaTime * 2f);

                yield return new WaitForEndOfFrame();
            }

            Destroy(FireEffect);

            yield return new WaitUntil(() => Chase == true);

            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 1f));

            if (!Blast) yield break;
            Vector3 direction = TargetPosition - Blast.transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Blast.transform.rotation = Quaternion.Euler(0f, 0f, angle);


            Blast.gameObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            Blast.gameObject.layer = 13;

            BlastComponent shot = Blast.gameObject.AddComponent<BlastComponent>();
            shot.User = gameObject;
            shot.Damage = RealDamage;
            shot.ID = ID;
            shot.KnockbackMultiplier = 0.1f;
            shot.Technique = "Hellzone"; shot.Tint = KiColor(KiSys.CurrentForm); shot.ShotSize = .5f;

            yield return new WaitUntil(() => !Blast || shot.Redirected || Vector3.Distance(Blast.transform.position, TargetPosition) < 1f);

            yield return new WaitUntil(() => !Blast || shot.Redirected || Vector3.Distance(Blast.transform.position, TargetPosition) > 1f);

            if (Blast && !shot.Redirected) shot.HasHit = true;

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

        Color KiColor(string Form)
        {
            if (DBPU.KiBlast_CustomColorEnabled)
                return DBPU.GetKiBlastCustomColor(0.1f);

            if (KiSys.requiredColor != Color.clear)
            {
                return new Color(KiSys.requiredColor.r, KiSys.requiredColor.g, KiSys.requiredColor.b, 0.1f);
            }

            else
            {
                Dictionary<string, Color> ogColors = new Dictionary<string, Color>
                {
                    { "Base", new Color(1f, 0.85f, 0.3f, 0.1f) },
                    { "SSJ1", new Color(1f, 0.85f, 0.3f, 0.1f) },
                    { "PSSJ", new Color(1f, 0.85f, 0.3f, 0.1f) },
                    { "SSJ2", new Color(1f, 0.85f, 0.3f, 0.1f) },
                    { "SSJ3", new Color(1f, 0.75f, 0f, 0.1f) },
                    { "SSJGod", new Color(0f, 0.75f, 1f, 0.1f) },
                    { "SSJBlue", new Color(0f, 0.75f, 1f, 0.1f) },
                    { "PSSJBlue", new Color(0f, 0.75f, 1f, 0.1f) },
                    { "SSJBlueEvolution", new Color(0f, 0.3f, 0.6f, 0.1f) },
                    { "MUI", new Color(0f, 0.75f, 1f, 0.1f) },
                    { "FirstForm", new Color(1f, 0.4f, 1f, 0.1f) },
                    { "SecondForm", new Color(1f, 0.4f, 1f, 0.1f) },
                    { "ThirdForm", new Color(1f, 0.4f, 1f, 0.1f) },
                    { "FinalForm", new Color(1f, 0.4f, 1f, 0.1f) },
                    { "FullPower", new Color(1f, 0.4f, 1f, 0.1f) },
                    { "GoldenForm", new Color(1f, 0f, 0.2f, 0.1f) },
                    { "Android", new Color(0f, 0.75f, 1f, 0.1f) },
                    { "Ikari", new Color(0f, 1f, 0f, 0.1f) },
                    { "FPSSJ", new Color(0f, 1f, 0f, 0.1f) }
                    };

                if (ogColors.TryGetValue(Form, out Color predefinedColor))
                {
                    return predefinedColor;
                }

                else
                {
                    return new Color(1f, 0.85f, 0.3f, 0.1f);
                }
            }
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
                sp.sortingOrder = 100;

                GetAllChildrenRecursive(child);
            }
        }

        public class BlastVFX : MonoBehaviour
        {
            public Transform Blast;
            private GameObject VFX;

            public KiSystem KiSys;

            public void Start()
            {
                if (OrbFx.Active) { enabled = false; return; }
                VFX = new GameObject("KiBlastVFX");
                VFX.transform.SetParent(transform, true);
                VFX.transform.position = transform.position;
                VFX.transform.localScale = new Vector3(0.036f, 0.05f, 1f);
                VFX.AddComponent<SpriteRenderer>().sprite = DBPU.KiBlast_Sprite;
                VFX.GetComponent<SpriteRenderer>().material = ModAPI.FindMaterial("VeryBright");
                VFX.GetComponent<SpriteRenderer>().color = KiColor(KiSys.CurrentForm) /*new Color(1f, 1f, 1f, 0.05f)*/;
            }

            public Color KiColor(string Form)
            {
                if (DBPU.KiBlast_CustomColorEnabled)
                    return DBPU.GetKiBlastCustomColor(0.05f);

                if (KiSys.requiredColor != Color.clear)
                {
                    return new Color(KiSys.requiredColor.r, KiSys.requiredColor.g, KiSys.requiredColor.b, 0.05f);
                }

                else
                {
                    Dictionary<string, Color> ogColors = new Dictionary<string, Color>
                {
                    { "Base", new Color(1f, 0.85f, 0.3f, 0.05f) },
                    { "SSJ1", new Color(1f, 0.85f, 0.3f, 0.05f) },
                    { "PSSJ", new Color(1f, 0.85f, 0.3f, 0.05f) },
                    { "SSJ2", new Color(1f, 0.85f, 0.3f, 0.05f) },
                    { "SSJ3", new Color(1f, 0.75f, 0f, 0.05f) },
                    { "SSJGod", new Color(0f, 0.75f, 1f, 0.05f) },
                    { "SSJBlue", new Color(0f, 0.75f, 1f, 0.05f) },
                    { "PSSJBlue", new Color(0f, 0.75f, 1f, 0.05f) },
                    { "SSJBlueEvolution", new Color(0f, 0.3f, 0.6f, 0.05f) },
                    { "MUI", new Color(0f, 0.75f, 1f, 0.05f) },
                    { "FirstForm", new Color(1f, 0.4f, 1f, 0.05f) },
                    { "SecondForm", new Color(1f, 0.4f, 1f, 0.05f) },
                    { "ThirdForm", new Color(1f, 0.4f, 1f, 0.05f) },
                    { "FinalForm", new Color(1f, 0.4f, 1f, 0.05f) },
                    { "FullPower", new Color(1f, 0.4f, 1f, 0.05f) },
                    { "GoldenForm", new Color(1f, 0f, 0.2f, 0.05f) },
                    { "Android", new Color(0f, 0.75f, 1f, 0.05f) },
                    { "Ikari", new Color(0f, 1f, 0f, 0.05f) },
                    { "FPSSJ", new Color(0f, 1f, 0f, 0.05f) }
                    };

                    if (ogColors.TryGetValue(Form, out Color predefinedColor))
                    {
                        return predefinedColor;
                    }

                    else
                    {
                        return new Color(1f, 0.85f, 0.3f, 0.05f);
                    }
                }
            }

            public void Update()
            {
                VFX.transform.position = transform.position;
                VFX.transform.eulerAngles += new Vector3(0f, 0f, 1200f * Time.deltaTime);
            }
        }
    }
}
