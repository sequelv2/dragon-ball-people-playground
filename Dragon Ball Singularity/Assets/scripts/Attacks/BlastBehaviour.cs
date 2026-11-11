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
    [DefaultExecutionOrder(-50)]
    public class BlastBehaviour : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;

        public bool IsBarrage = false;
        public int barrageAmount = 0;

        public bool Active = false;
        public bool Firing = false;
        public float RealDamage = 100f;
        public float CostPercent = 0.005f;
        public float BarrageCostPercent = 0.0025f;
        public float KnockbackMultiplier = 1f;
        public float ExplosionSizeMultiplier = 1f;
        public float DamageOutput = 1f;

        public float Damage = 0f;
        public float Speed = 80f;
        public string ID = "Blast";

        private void Update()
        {
            if (Firing == true)
            {
                RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput * DamageOutput;
            }

            if (Active == true && IsBarrage == true)
            {
                foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
                {
                    if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, IsBarrage ? "KiBlastBarrage" : "KiBlast")) continue;
                        if (limb.PhysicalBehaviour.IsBeingUsedContinuously())
                        {
                            if (!limb.Person.IsAlive() || limb.Health <= 0f || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 4f)) return;

                            StartCoroutine(Fire(limb.transform));
                        }
                        ;
                    }
                }
            }
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            if (!GetComponent<MultipliersAndStuffs>()) gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = GetComponent<MultipliersAndStuffs>();

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 200f;

            ID = gameObject.name + "_Blast" + UnityEngine.Random.Range(0, 999);

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    var trigger = limb.gameObject.GetOrAddComponent<UseEventTrigger>();
                    Action previous = trigger.Action;
                    LimbBehaviour capturedLimb = limb;
                    trigger.Action = () =>
                    {
                        if (!KiHandLoadout.LegacyAllowed(capturedLimb, IsBarrage ? "KiBlastBarrage" : "KiBlast"))
                        { previous?.Invoke(); return; }
                        if (IsBarrage == true || !capturedLimb.Person.IsAlive() || capturedLimb.Health <= 0f || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * (CostPercent * 4f))
                        {
                            previous?.Invoke();
                            return;
                        }

                        StartCoroutine(Fire(capturedLimb.transform));
                    };
                }
            }
        }

        private IEnumerator Fire(Transform Arm)
        {
            Firing = true;

            if (IsBarrage == false)
            {
                CostPercent = 0.005f;
                StartCoroutine(KiCost());

                //GameObject Blast = Instantiate(DBPU.KiBlast_End, Arm.position, Arm.rotation * Quaternion.Euler(0, 0, -90));
                GameObject Blast = new GameObject("KiBlast");
                Blast.gameObject.layer = 9;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.transform.position = Arm.position + (-Arm.up * 0.3f);
                Blast.transform.rotation = Arm.rotation * Quaternion.Euler(0, 0, -90);
                Blast.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                Blast.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
                if (Blast.gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) Blast.gameObject.GetComponent<Rigidbody2D>().velocity = Blast.transform.right * Speed;
                Blast.gameObject.GetComponent<Rigidbody2D>().fixedAngle = true;
                Blast.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;
                Blast.gameObject.AddComponent<BlastVFX>().KiSys = KiSys;

                foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                    Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Blast.gameObject.GetComponent<CircleCollider2D>(), true);


                /*Blast.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = KiColor(KiSys.CurrentForm);

                GetAllChildrenRecursive(Blast.transform);

                foreach (Transform child in Blast.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sp.sortingOrder += 10;
                    }

                ChangePlayBackOfAllParticleSystems(Blast.transform, 2f);*/
                var blastComp = Blast.gameObject.AddComponent<BlastComponent>();
                blastComp.User = gameObject;
                blastComp.Damage = RealDamage;
                blastComp.ID = ID;
                blastComp.KnockbackMultiplier = KnockbackMultiplier;
                blastComp.ExplosionSizeMultiplier = ExplosionSizeMultiplier * Mathf.Max(0.1f, DBPU.KiBlast_ExplosionSizeMultiplier);
                blastComp.Technique = "KiBlast"; blastComp.Tint = KiColor(KiSys.CurrentForm); blastComp.ShotSize = .42f;

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
                if (OrbFx.Active) { OrbFx.Mute(FireEffect); OrbFx.Muzzle(Arm.position - (Arm.up * 0.3f), -Arm.up, KiColor(KiSys.CurrentForm), .6f); }

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

                yield return new WaitForSeconds(0.1f);

                Firing = false;

                yield return new WaitForSeconds(0.9f);

                Destroy(FireEffect.gameObject);
            }

            else
            {
                CostPercent = BarrageCostPercent;
                StartCoroutine(KiCost());

                /*GameObject Blast = Instantiate(DBPU.KiBlast_End, Arm.position, Arm.rotation * Quaternion.Euler(0, 0, UnityEngine.Random.Range(-75, -105)));
                Blast.gameObject.layer = 9;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.gameObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                Blast.gameObject.GetComponent<CircleCollider2D>().isTrigger = true;
                Blast.gameObject.GetComponent<CircleCollider2D>().radius = 1.4f;
                Blast.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = KiColor(KiSys.CurrentForm);

                GetAllChildrenRecursive(Blast.transform);

                foreach (Transform child in Blast.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                    sp.sortingOrder += 10;
                    }

                ChangePlayBackOfAllParticleSystems(Blast.transform, 2f);*/

                GameObject Blast = new GameObject("KiBlast");
                Blast.gameObject.layer = 9;
                Blast.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Blast.transform.position = Arm.position + (-Arm.up * 0.3f);
                Blast.transform.rotation = Arm.rotation * Quaternion.Euler(0, 0, UnityEngine.Random.Range(-75, -105));
                Blast.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
                Blast.gameObject.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
                if (Blast.gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) Blast.gameObject.GetComponent<Rigidbody2D>().velocity = Blast.transform.right * Speed;
                Blast.gameObject.GetComponent<Rigidbody2D>().fixedAngle = true;
                Blast.gameObject.AddComponent<CircleCollider2D>().radius = 0.6f;
                Blast.gameObject.AddComponent<BlastVFX>().KiSys = KiSys;

                foreach (var c1 in GetComponent<PersonBehaviour>().Limbs)
                    Physics2D.IgnoreCollision(c1.gameObject.GetComponent<Collider2D>(), Blast.gameObject.GetComponent<CircleCollider2D>(), true);

                var barrageBlastComp = Blast.gameObject.AddComponent<BlastComponent>();
                barrageBlastComp.User = gameObject;
                barrageBlastComp.Damage = RealDamage;
                barrageBlastComp.ID = ID;
                barrageBlastComp.KnockbackMultiplier = KnockbackMultiplier;
                barrageBlastComp.ExplosionSizeMultiplier = ExplosionSizeMultiplier * Mathf.Max(0.1f, DBPU.KiBlast_ExplosionSizeMultiplier);
                barrageBlastComp.Technique = "Barrage"; barrageBlastComp.Tint = KiColor(KiSys.CurrentForm); barrageBlastComp.ShotSize = .34f;

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
                if (OrbFx.Active) { OrbFx.Mute(FireEffect); OrbFx.Muzzle(Arm.position - (Arm.up * 0.3f), -Arm.up, KiColor(KiSys.CurrentForm), .42f); }

                if (barrageAmount == 0)
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

                yield return new WaitForSeconds(0.05f);

                Firing = false;

                yield return new WaitForSeconds(0.9f);

                Destroy(FireEffect.gameObject);
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

        public Color KiColor(string Form)
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
