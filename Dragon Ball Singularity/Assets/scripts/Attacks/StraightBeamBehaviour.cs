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
    public class StraightBeamBehaviour : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;
        private AudioSource audioSource;

        private Transform UsedArm;

        public bool IsBarrage = false;
        public int barrageAmount = 0;

        private Vector3 StopPos;
        private bool DeathBeamWasDeflected = false;
        private Vector3 DeathBeamDeflectPoint;
        private Vector3 DeathBeamDeflectStopPos;

        public bool Active = false;
        public bool Firing = false;
        public float CostPercent = 0.05f;
        public bool DeathBeamSplitConsumed = false;

        public string Character = "Human";

        public float Damage = 0f;
        public float RealDamage = 15000f;
        public float Width = 0.08f;

        public string EffectID;

        private void Awake()
        {
            audioSource = new GameObject("straightBeamAudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Body/UpperBody"), false);
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
                        if (!KiHandLoadout.LegacyAllowed(limb, EffectID)) return;
                        if (Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * CostPercent || limb.Health <= 0f) return;

                        if (limb.Health > 0f && Active == true && Firing == false)
                        {
                            RealDamage = Damage * Multipliers.FinalMultiplier * Multipliers.KiOutput;

                            UsedArm = limb.transform;
                            StartCoroutine(Fire(limb.transform));
                        }
                    };
                }
            }
        }

        public void Update()
        {
            if (audioSource.volume != DBPU.PowersVolume) audioSource.volume = DBPU.PowersVolume;

            if (Active == true && IsBarrage == true)
            {
                foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
                {
                    if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                    {
                        if (!KiHandLoadout.LegacyAllowed(limb, EffectID)) continue;
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
                KiSys.currentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * Time.deltaTime;
                CurrentKi -= (KiSys.maxKi * (CostPercent * DBPU.KiAttackCostMultiplier)) * Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            if (OGKiDrain != 0f) KiSys.baseRegen = KiRegen;

            yield return null;
        }

        private IEnumerator Fire(Transform Arm)
        {
            Firing = true;

            RealDamage = (Damage * Multipliers.FinalMultiplier) * Multipliers.KiOutput;

            if (EffectID == "Death Beam")
            {
                if (IsBarrage == false)
                {
                    CostPercent = 0.01f;
                }

                else
                {

                    CostPercent = 0.005f;
                }

                StartCoroutine(KiCost());

                float RandomPos = (IsBarrage == true) ? UnityEngine.Random.Range(-0.12f, 0.12f) : 0f;

                GameObject FireEffect;

                yield return new WaitForFixedUpdate();

                if (DBPU.ExtraCharge == true && IsBarrage == false)
                {
                    FireEffect = Instantiate(DBPU.DeathBeamCharge1, Arm.position - (Arm.up * 0.3f) + (Arm.right * RandomPos), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    FireEffect.transform.SetParent(Arm, false);
                    FireEffect.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    FireEffect.transform.position = Arm.position - (Arm.up * 0.3f) + (Arm.right * RandomPos);
                    //GetAllChildrenRecursive(FireEffect.transform);
                    FireEffect.AddComponent<DestroyOverTime>().Timer = 2f;
                    //ChangePlayBackOfAllParticleSystems(FireEffect.transform, 2.5f);
                    //FireEffect.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.3f, 0.3f, 0.3f);

                    yield return new WaitForSeconds(0.5f);

                    GameObject FireEffect2 = Instantiate(DBPU.DeathBeamCharge2, Arm.position - (Arm.up * 0.3f) + (Arm.right * RandomPos), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    FireEffect2.transform.SetParent(Arm, false);
                    FireEffect2.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    FireEffect2.transform.position = Arm.position - (Arm.up * 0.3f) + (Arm.right * RandomPos);
                    //GetAllChildrenRecursive(FireEffect.transform);
                    FireEffect2.AddComponent<DestroyOverTime>().Timer = 2f;

                    yield return new WaitForSeconds(1.3f);
                }

                else
                {
                    FireEffect = Instantiate(DBPU.KiBlast_Start, Arm.position - (Arm.up * 0.3f) + (Arm.right * RandomPos), Arm.rotation * Quaternion.Euler(0, 0, -90));
                    FireEffect.transform.SetParent(Arm.transform, true);
                    FireEffect.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    ChangePlayBackOfAllParticleSystems(FireEffect.transform, 2.5f);
                    FireEffect.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystem>().startColor = new Color(0.95f, 0.3f, 0.3f, 0.3f);
                    FireEffect.AddComponent<DestroyOverTime>().Timer = 2f;

                    GetAllChildrenRecursive(FireEffect.transform);

                    foreach (Transform child in FireEffect.transform)
                    {
                        var sp = child.gameObject.GetComponent<Renderer>();
                        sp.sortingLayerName = Arm.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
                        sp.sortingOrder += 10;
                    }
                }

                DeathBeamWasDeflected = false;
                DeathBeamSplitConsumed = false;
                yield return StartCoroutine(RayCast(Arm, RandomPos));

                audioSource.PlayOneShot(DBPU.Attack_DeathBeam_Fire);

                GameObject BeamLineObj = new GameObject("BeamLine");
                BeamLineObj.transform.SetParent(Arm);
                LineRenderer BeamLine = BeamLineObj.AddComponent<LineRenderer>();
                BeamLine.textureMode = LineTextureMode.Tile;
                BeamLine.material = new Material(ModAPI.FindMaterial("VeryBright"));
                BeamLine.sortingLayerName = "Default";
                BeamLine.sortingOrder = 0;
                BeamLine.material.mainTexture = DBPU.StraightBeam_Texture;
                BeamLine.material.color = new Color(0.95f, 0.3f, 0.3f, 8f);
                BeamLine.material.mainTextureScale = new Vector2(1f, 1f);
                BeamLine.numCapVertices = 2;
                BeamLine.startWidth = 0f;
                BeamLine.endWidth = 0f;
                BeamLine.positionCount = 2;
                BeamLine.SetPosition(0, Arm.position + (-Arm.up * 0.4f) + (Arm.right * RandomPos));
                BeamLine.SetPosition(1, DeathBeamWasDeflected ? DeathBeamDeflectPoint : StopPos);

                StartCoroutine(MiddlePartAnimation(EffectID, BeamLine));

                GameObject DeflectBeamLineObj = null;
                if (DeathBeamWasDeflected)
                {
                    DeflectBeamLineObj = new GameObject("BeamLineDeflect");
                    DeflectBeamLineObj.transform.SetParent(Arm);
                    LineRenderer DeflectBeamLine = DeflectBeamLineObj.AddComponent<LineRenderer>();
                    DeflectBeamLine.textureMode = LineTextureMode.Tile;
                    DeflectBeamLine.material = new Material(ModAPI.FindMaterial("VeryBright"));
                    DeflectBeamLine.sortingLayerName = "Default";
                    DeflectBeamLine.sortingOrder = 0;
                    DeflectBeamLine.material.mainTexture = DBPU.StraightBeam_Texture;
                    DeflectBeamLine.material.color = new Color(0.95f, 0.3f, 0.3f, 8f);
                    DeflectBeamLine.material.mainTextureScale = new Vector2(1f, 1f);
                    DeflectBeamLine.numCapVertices = 2;
                    DeflectBeamLine.startWidth = 0f;
                    DeflectBeamLine.endWidth = 0f;
                    DeflectBeamLine.positionCount = 2;
                    DeflectBeamLine.SetPosition(0, DeathBeamDeflectPoint);
                    DeflectBeamLine.SetPosition(1, DeathBeamDeflectStopPos);
                    StartCoroutine(MiddlePartAnimation(EffectID, DeflectBeamLine));
                }

                if (IsBarrage == false)
                {
                    yield return new WaitForSeconds(0.5f);

                    Destroy(BeamLineObj);
                    if (DeflectBeamLineObj != null) Destroy(DeflectBeamLineObj);
                    Destroy(FireEffect);

                    Firing = false;

                    yield return null;
                }

                else
                {
                    yield return new WaitForSeconds(0.1f);

                    Firing = false;

                    yield return new WaitForSeconds(0.4f);

                    Destroy(BeamLineObj);
                    if (DeflectBeamLineObj != null) Destroy(DeflectBeamLineObj);
                    Destroy(FireEffect);

                    yield return null;
                }
            }

            yield return null;
        }

        public IEnumerator RayCast(Transform Arm, float RandomPos)
        {
            RaycastHit2D[] rayHits = Physics2D.RaycastAll(Arm.position + (-Arm.up * 0.3f) + (Arm.right * RandomPos), -Arm.up, 10000f);

            StopPos = Arm.position + (-Arm.up * 10000f) + (Arm.right * RandomPos);

            foreach (RaycastHit2D hit in rayHits)
            {
                if (hit.collider.gameObject.transform.root.transform != transform && (hit.collider.gameObject.layer == 11 || hit.collider.gameObject.layer == 9 || hit.collider.gameObject.layer == 13))
                {
                    Rigidbody2D rb = hit.collider.gameObject.GetComponent<Rigidbody2D>();

                    var shell = hit.collider.GetComponent<KiShieldSurface>();
                    if (shell && shell.Owner)
                    {
                        if (shell.Owner.transform.root == transform.root) continue;
                        if (shell.Owner.ReceiveDamage(RealDamage, hit.point, GetInstanceID())) { StopPos = hit.point; break; }
                        continue;
                    }
                    if (hit.collider.gameObject.layer == 11)
                    {
                        StopPos = hit.point;

                        var Explosion = Instantiate(DBPU.KiBlast_Hit, StopPos, Quaternion.identity);
                        Explosion.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                        Explosion.gameObject.AddComponent<DestroyOverTime>().Timer = 2f;

                        foreach (Transform child in Explosion.transform)
                        {
                            var sp = child.gameObject.GetComponent<Renderer>();
                            sp.sortingLayerName = "Bubbles";
                            sp.sortingOrder += 10;

                            var ps = child.gameObject.GetComponent<ParticleSystem>();
                            ps.playbackSpeed *= 0.8f;
                        }

                        AudioSource audioSource2 = Explosion.AddComponent<AudioSource>();
                        Global global = FindObjectOfType<Global>();
                        global.AddAudioSource(audioSource2, false);
                        audioSource2.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
                        audioSource2.outputAudioMixerGroup = global.SoundEffects;
                        audioSource2.spatialBlend = 1;
                        audioSource2.volume = 1;
                        audioSource2.maxDistance = 1000;
                        audioSource2.PlayOneShot(DBPU.Attack_DeathBeam_Hit);

                        break;
                    }

                    else if (hit.collider.gameObject.GetComponent<LimbBehaviour>())
                    {
                        var limb = hit.collider.gameObject.GetComponent<LimbBehaviour>();

                        Vector2 incomingDir = (-Arm.up).normalized;
                        Vector2 rayStop, rayOut;
                        var intercepted = BeamPhysics.InterceptRay(transform.root.gameObject, limb, incomingDir, 1f, out rayStop, out rayOut);
                        if (intercepted == BeamPhysics.RayResult.Blocked)
                        {
                            StopPos = rayStop;
                            break;
                        }

                        if (intercepted == BeamPhysics.RayResult.Deflected)
                        {
                            RaycastHit2D wall = Physics2D.Raycast(rayStop, rayOut, 10000f, 1 << 11);
                            DeathBeamWasDeflected = true;
                            DeathBeamDeflectPoint = rayStop;
                            DeathBeamDeflectStopPos = wall.collider ? (Vector3)wall.point : (Vector3)rayStop + ((Vector3)rayOut * 10000f);
                            StopPos = DeathBeamDeflectPoint;
                            if (wall.collider) BeamGround.Erupt(wall.point, .4f, new Color(0.95f, 0.3f, 0.3f), wall.normal);
                            break;
                        }

                        if (!BeamPhysics.On && KiDeflectUtils.TryDeflectStraightBeam(transform.root.gameObject, limb, incomingDir, out var deflectDir))
                        {
                            DeathBeamWasDeflected = true;
                            DeathBeamDeflectPoint = hit.point;
                            DeathBeamDeflectStopPos = (Vector3)hit.point + ((Vector3)deflectDir.normalized * 10000f);
                            StopPos = DeathBeamDeflectPoint;
                            break;
                        }

                        if (KiDeflectUtils.TryBeamSplitStraightBeamHit(this, limb, hit.point, incomingDir))
                        {
                            StopPos = hit.point;
                            break;
                        }

                        float DamagePercent = RealDamage / MCP_Utils.AverageMaxHealth(limb.Person);
                        if (KiShield.Protects(limb.Person, gameObject, Arm.position,
                            RealDamage / .12f, GetInstanceID())) { StopPos = hit.point; break; }
                        var energyOwner = GetComponent<PersonBehaviour>();
                        EnergyAnnihilation.Expose(energyOwner, limb,
                            KiCombatMath.Cap(DbpuFx.PowerOf(energyOwner)), hit.point,
                            .12f, new Color(1f, .22f, .32f));
                        if (KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                        {
                            if (rb != null && DBPU.EffectiveBeamKnockback > 0f)
                                rb.AddForce(-Arm.up * ((15f * (rb.mass * 0.9f) * DBPU.EffectiveBeamKnockback)), ForceMode2D.Impulse);
                            StopPos = hit.point;
                            break;
                        }
                        limb.Damage(RealDamage);

                        if (DamagePercent > 0.05f)
                        {
                            rb.AddForce(-Arm.up * ((15f * (rb.mass * 0.9f) * Mathf.Clamp(DamagePercent, 0.4f, 1f))), ForceMode2D.Impulse);
                        }

                        if (DamagePercent < 0.2f)
                        {
                            StopPos = hit.point;

                            var Explosion = Instantiate(DBPU.KiBlast_Hit, StopPos, Quaternion.identity);
                            Explosion.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                            Explosion.gameObject.AddComponent<DestroyOverTime>().Timer = 2f;

                            foreach (Transform child in Explosion.transform)
                            {
                                var sp = child.gameObject.GetComponent<Renderer>();
                                sp.sortingLayerName = "Bubbles";
                                sp.sortingOrder += 10;

                                var ps = child.gameObject.GetComponent<ParticleSystem>();
                                ps.playbackSpeed *= 1f;
                            }

                            AudioSource audioSource2 = Explosion.AddComponent<AudioSource>();
                            Global global = FindObjectOfType<Global>();
                            global.AddAudioSource(audioSource2, false);
                            audioSource2.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
                            audioSource2.outputAudioMixerGroup = global.SoundEffects;
                            audioSource2.spatialBlend = 1;
                            audioSource2.volume = 1;
                            audioSource2.maxDistance = 1000;
                            audioSource2.PlayOneShot(DBPU.Attack_DeathBeam_Hit);

                            break;
                        }

                        else if (DamagePercent >= 0.6f)
                        {
                            if (hit.collider.gameObject.GetComponent<LimbBehaviour>().name == "UpperBody")
                            {
                                ModAPI.CreateParticleEffect("BloodExplosion", hit.point);

                                hit.collider.gameObject.GetComponent<LimbBehaviour>().BreakBone();

                                yield return new WaitForFixedUpdate();

                                hit.collider.gameObject.GetComponent<LimbBehaviour>().HealBone();
                            }

                            float VisualDamage = (10f * Mathf.Clamp(DamagePercent, 0f, 0.5f)) / limb.SkinMaterialHandler.intensityMultiplier;

                            if (hit.collider.gameObject.GetComponent<LimbBehaviour>().name.Contains("Body")) limb.SkinMaterialHandler.AddDamagePoint(DamageType.Bullet, hit.point, VisualDamage * 5f);
                            limb.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, hit.point, VisualDamage);
                        }

                        else if (DamagePercent >= 0.8f)
                        {
                            if (hit.collider.gameObject.GetComponent<LimbBehaviour>().name == "UpperBody")
                            {
                                ModAPI.CreateParticleEffect("BloodExplosion", hit.point);

                                hit.collider.gameObject.GetComponent<LimbBehaviour>().BreakBone();

                                yield return new WaitForFixedUpdate();

                                hit.collider.gameObject.GetComponent<LimbBehaviour>().HealBone();
                            }

                            else if (hit.collider.gameObject.GetComponent<LimbBehaviour>().name == "Head")
                            {
                                var headLimb = hit.collider.gameObject.GetComponent<LimbBehaviour>();
                                if (!UltraInstinctEvasion.TryEvade(headLimb, hit.point))
                                    headLimb.Health = 0f;

                                ModAPI.CreateParticleEffect("BloodExplosion", hit.point);
                            }

                            float VisualDamage = (25f * Mathf.Clamp(DamagePercent, 0f, 0.5f)) / limb.SkinMaterialHandler.intensityMultiplier;

                            if (hit.collider.gameObject.GetComponent<LimbBehaviour>().name.Contains("Body")) limb.SkinMaterialHandler.AddDamagePoint(DamageType.Bullet, hit.point, VisualDamage * 5f);
                            limb.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, hit.point, VisualDamage);
                        }
                    }

                    else if (hit.collider.gameObject.GetComponent<PhysicalBehaviour>())
                    {
                        var Explosion = Instantiate(DBPU.KiBlast_Hit, StopPos, Quaternion.identity);
                        Explosion.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                        Explosion.gameObject.AddComponent<DestroyOverTime>().Timer = 2.5f;

                        foreach (Transform child in Explosion.transform)
                        {
                            var sp = child.gameObject.GetComponent<Renderer>();
                            sp.sortingLayerName = "Bubbles";
                            sp.sortingOrder += 10;

                            var ps = child.gameObject.GetComponent<ParticleSystem>();
                            ps.playbackSpeed *= 0.8f;
                        }

                        AudioSource audioSource2 = Explosion.AddComponent<AudioSource>();
                        Global global = FindObjectOfType<Global>();
                        global.AddAudioSource(audioSource2, false);
                        audioSource2.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
                        audioSource2.outputAudioMixerGroup = global.SoundEffects;
                        audioSource2.spatialBlend = 1;
                        audioSource2.volume = 1;
                        audioSource2.maxDistance = 1000;
                        audioSource2.PlayOneShot(DBPU.Attack_DeathBeam_Hit);

                        Vector3 direction = (hit.transform.position - (Vector3)hit.point).normalized;

                        StartCoroutine(Hit(hit.collider.gameObject));

                        if (rb.velocity.magnitude < 12f) rb.AddForce(direction * (50f * rb.mass), ForceMode2D.Impulse);

                        if (hit.collider.gameObject.GetComponent<DestroyableBehaviour>()) hit.collider.gameObject.GetComponent<DestroyableBehaviour>().Break();

                        if (hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>() && hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>().Destroyed == false) hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                    }
                }
            }

            yield return null;
        }

        public IEnumerator Hit(GameObject other)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                point: other.transform.position,
                radius: 15f,
                layerMask: LayerMask.GetMask("Objects", "CollidingDebris")
                );

            foreach (var hit in hits)
            {
                if (hit.gameObject != other.gameObject && hit.transform.root.gameObject != transform.root.gameObject && hit.gameObject.GetComponent<Rigidbody2D>())
                {
                    Vector3 direction = (hit.transform.position - other.transform.position).normalized;
                    float Distance = Mathf.Clamp(Vector2.Distance(other.transform.position, hit.transform.position), 1f, 20f);

                    Rigidbody2D rb = hit.gameObject.GetComponent<Rigidbody2D>();

                    if (hit.gameObject.GetComponent<LimbBehaviour>() && hit.isTrigger == false)
                    {
                        var hitLimb = hit.gameObject.GetComponent<LimbBehaviour>();
                        if (MCP_Utils.MeetsMinLimbDamage(hitLimb, Damage))
                        {
                            if (Distance < 5f)
                            {
                                if (KiShield.Protects(hitLimb.Person, gameObject, other.transform.position,
                                    Damage / .12f, GetInstanceID())) continue;
                                var energyOwner = GetComponent<PersonBehaviour>();
                                EnergyAnnihilation.Expose(energyOwner, hitLimb,
                                    KiCombatMath.Cap(DbpuFx.PowerOf(energyOwner) * .25d / Mathf.Clamp(Distance * Distance, 1f, 25f)),
                                    hit.ClosestPoint(other.transform.position), .12f, new Color(1f, .22f, .32f));
                                hitLimb.Damage(Damage / Mathf.Clamp(Distance * Distance, 1f, 25f));
                            }

                            float ForcePercent = (Damage * 5f) / MCP_Utils.AverageMaxHealth(hitLimb.Person);
                            if (rb.velocity.magnitude < 8f) rb.AddForce(direction * ((Mathf.Clamp(150f / Distance, 2f, 50f) * rb.mass * Mathf.Clamp(ForcePercent, 0.25f, 1f))), ForceMode2D.Impulse);
                        }
                    }

                    else if (hit.gameObject.GetComponent<PhysicalBehaviour>())
                    {
                        if (rb.velocity.magnitude < 12f) rb.AddForce(direction * (Mathf.Clamp(150f / Distance, 5f, 50f) * rb.mass), ForceMode2D.Impulse);

                        if (hit.gameObject.GetComponent<DestroyableBehaviour>())
                        {
                            if (UnityEngine.Random.Range(5, 20) > Distance) hit.gameObject.GetComponent<DestroyableBehaviour>().Break();
                        }

                        if (hit.gameObject.GetComponent<DamagableMachineryBehaviour>() && hit.gameObject.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                        {
                            if (UnityEngine.Random.Range(10, 20) > Distance) hit.gameObject.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                        }
                    }
                }
            }

            yield return null;
        }

        public IEnumerator MiddlePartAnimation(string EffectID, LineRenderer BeamLine)
        {
            if (EffectID == "Death Beam")
            {
                while (BeamLine.startWidth < Width)
                {
                    BeamLine.startWidth += (Width * 20f) * Time.deltaTime;
                    BeamLine.endWidth += (Width * 20f) * Time.deltaTime;

                    yield return new WaitForFixedUpdate();
                }

                BeamLine.startWidth = Width;
                BeamLine.endWidth = Width;

                while (BeamLine.startWidth > 0f)
                {
                    BeamLine.startWidth -= (Width * 12f) * Time.deltaTime;
                    BeamLine.endWidth -= (Width * 12f) * Time.deltaTime;

                    yield return new WaitForFixedUpdate();
                }

                BeamLine.startWidth = 0f;
                BeamLine.endWidth = 0f;
            }

            else
            {
                yield return null;
            }

            yield return null;
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
    }
}
