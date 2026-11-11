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
    public class PunchPower : MonoBehaviour
    {
        public MultipliersAndStuffs Multipliers;

        public bool Active = false;
        public float Damage = -1f;
        public float DamageOutput = 1f;
        public float knockbackOutput = 1f;
        public float maxKnockbackImpulse = 520f;
        public bool enableNuclearExplosion = false;
        public float maxKnockback = 150f;

        public bool CanNukeRN = true;

        private PersonBehaviour person;
        private PhysicalBehaviour physicalBehaviour;
        private Rigidbody2D rbSelf;
        private LimbBehaviour selfLimb;
        private bool isResettingMovement = false;
        private bool isTailSegment;
        private float nextHitAllowed;

        const float MeleeImpactStopPercent = 0.5f;
        const float PunchDamageHealthFactor = 0.01f;
        const float HitCooldownSeconds = 0.08f;

        public Dictionary<LimbBehaviour.BodyPart, float> LimbDamageReductions = new Dictionary<LimbBehaviour.BodyPart, float>
        {
            { LimbBehaviour.BodyPart.Arms, 0.5f },
            { LimbBehaviour.BodyPart.Legs, 0.25f }
            };

        void EnsureRefs()
        {
            if (person == null) person = transform.root.GetComponent<PersonBehaviour>();
            if (physicalBehaviour == null) physicalBehaviour = GetComponent<PhysicalBehaviour>();
            if (rbSelf == null) rbSelf = GetComponent<Rigidbody2D>();
            if (selfLimb == null) selfLimb = GetComponent<LimbBehaviour>();
            if (Multipliers == null)
            {
                Multipliers = transform.root.GetComponent<MultipliersAndStuffs>();
                if (Multipliers == null) Multipliers = transform.root.gameObject.AddComponent<MultipliersAndStuffs>();
            }
        }

        void EnsureMeleeDamageCached()
        {
            EnsureRefs();
            if (Damage >= 0f) return;
            Damage = MCP_Utils.ResolveMeleeBaseDamage(person, Multipliers, -1f, PunchDamageHealthFactor);
        }

        void Awake()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
            rbSelf = GetComponent<Rigidbody2D>();
            selfLimb = GetComponent<LimbBehaviour>();
            isTailSegment = GetComponent<CellMaxTailSegment>() != null;
            person = transform.root.GetComponent<PersonBehaviour>();
            if (!transform.root.gameObject.GetComponent<MultipliersAndStuffs>())
                transform.root.gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = transform.root.gameObject.GetComponent<MultipliersAndStuffs>();
        }

        void Start()
        {
            EnsureRefs();
            EnsureMeleeDamageCached();

            if (person != null)
            {
                PowerSetup.EnsureKickPowerComponents(person);
                var powers = transform.root.GetComponent<CharacterPowers>();
                if (powers != null)
                {
                    PowerSetup.RegisterUniversalPhysicalPowers(powers, person);
                    PowerSetup.ApplyAutoEnablePunchKick(person);
                    powers.HarvestPowerStates();
                }
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Active || isTailSegment) return;
            EnsureRefs();
            if (person == null || !person.IsAlive() || selfLimb == null || selfLimb.Health <= 0f) return;
            if (collision.transform.gameObject.layer == 11) return;
            if (Multipliers == null || rbSelf == null) return;
            if (Time.time < nextHitAllowed) return;

            float speed = rbSelf.velocity.magnitude;
            if (speed < 5f) return;

            EnsureMeleeDamageCached();
            float meleeDamage = Mathf.Max(0f, Damage);

            var limb = collision.gameObject.GetComponent<LimbBehaviour>();
            if (limb != null)
            {
                if (limb.Person == person) return;
                Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
                if (targetRb == null) return;

                nextHitAllowed = Time.time + HitCooldownSeconds;

                float speedMultiplier = Mathf.Clamp(speed, 5f, 25f) / 5f;
                float realDamage = (meleeDamage * Mathf.Max(0.01f, Multipliers.FinalMultiplier)) * DamageOutput * CombatPowerController.OutputFor(person);
                float finalDamage = realDamage * speedMultiplier * DBPU.DamageMultiplier;
                float victimMaxHealth = MCP_Utils.AverageMaxHealth(limb.Person);
                if (victimMaxHealth <= 0f) victimMaxHealth = 1f;
                float damagePercentage = (finalDamage / victimMaxHealth) * 100f;
                float attackerPower = MCP_Utils.AverageMaxHealth(person) / CombatPowerController.KaiokenHealthScale(person) * Multipliers.FinalMultiplier;
                float victimPower = victimMaxHealth / CombatPowerController.KaiokenHealthScale(limb.Person) * MCP_Utils.GetFinalMultiplier(limb.Person);
                float stopAt = MCP_Utils.MeleeStopThreshold(attackerPower, victimPower, MeleeImpactStopPercent);

                if (speed > 15f && finalDamage >= 10000f)
                    CheckSurroundings(targetRb, collision.contacts[0].point, Mathf.Clamp(damagePercentage * 200f, 20f, 100f), finalDamage);

                if (damagePercentage <= stopAt)
                {
                    if (!isResettingMovement && DBPU.PhysAtkStopLimbs) StartCoroutine(ResetMovement(limb));
                    return;
                }

                if (LimbDamageReductions.TryGetValue(limb.RoughClassification, out float reduction))
                    finalDamage *= reduction;

                CombatDismemberment.TryMeleeSever(person, limb, finalDamage, collision.relativeVelocity.magnitude,
                    collision.contacts[0].point, (Vector2)targetRb.transform.position - (Vector2)transform.position);
                DamageLimb(limb, finalDamage, collision.contacts[0].point);

                float kbImpulse = maxKnockbackImpulse * knockbackOutput * DBPU.Universal_KnockbackMultiplier;
                float kbCap = maxKnockback * knockbackOutput * DBPU.Universal_KnockbackMultiplier;
                float knockbackImpulse = (damagePercentage >= 25f) ? kbImpulse : kbImpulse * (damagePercentage / 25f);

                if (DBPU.hitParticle != null)
                {
                    GameObject particle = Instantiate(DBPU.hitParticle, collision.contacts[0].point, DBPU.hitParticle.transform.rotation);
                    Vector2 direction = ((Vector2)targetRb.transform.position - (Vector2)transform.position).normalized;
                    particle.transform.rotation = Quaternion.Euler(0, 0, (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f);
                    particle.transform.localScale = new Vector3(Mathf.Clamp(damagePercentage / 500, 0, 0.833332f), Mathf.Clamp(damagePercentage / 500, 0, 0.833332f), 0.5f);

                    foreach (Transform child in particle.transform)
                    {
                        child.localScale = new Vector3(Mathf.Clamp(damagePercentage / 150, 0, 2.777f), Mathf.Clamp(damagePercentage / 150, 0, 2.777f), 0.5f);
                        var ps1renderer = child.gameObject.GetComponent<ParticleSystemRenderer>();
                        if (ps1renderer != null) ps1renderer.sortingLayerName = "Top";
                    }

                    ParticleSystem ps = particle.GetComponent<ParticleSystem>();
                    var psrenderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (psrenderer != null) psrenderer.sortingLayerName = "Top";
                    if (ps != null)
                    {
                        ps.Play();
                        Destroy(particle, ps.main.duration);
                    }
                    else Destroy(particle, 1f);
                }

                if (speed > 15f && realDamage >= 50000000000f && enableNuclearExplosion == true && CanNukeRN == true) NukeShockwave(collision.contacts[0].point);

                Vector2 forceDirection = ((Vector2)targetRb.transform.position - (Vector2)transform.position).normalized;
                targetRb.AddForce(forceDirection * Mathf.Clamp(Mathf.Abs(knockbackImpulse - knockbackImpulse * 0.2f * (targetRb.mass / 0.14f)), 0, kbCap * (targetRb.mass / 0.5f)), ForceMode2D.Impulse);

                if (limb.name == "UpperBody" || limb.name == "MiddleBody")
                {
                    if (damagePercentage > 80f)
                    {
                        var head = limb.transform.root.Find("Head");
                        if (head != null)
                        {
                            var menu = head.GetComponent<TransformationContextMenu>();
                            if (menu != null && menu.Form != menu.BaseForm)
                                menu.TransformationsData.ApplyTransformation(limb.Person, menu.BaseForm, true);
                        }
                    }
                }
                else if (limb.name == "Head")
                {
                    if (damagePercentage > 80f)
                    {
                        var hc = limb.gameObject.GetComponent<HumanController>();
                        if (hc != null)
                        {
                            hc.Features.ForceKnockout = true;
                            hc.ForcedKnockout();
                        }

                        var menu = limb.gameObject.GetComponent<TransformationContextMenu>();
                        if (menu != null && menu.Form != menu.BaseForm)
                            menu.TransformationsData.ApplyTransformation(limb.Person, menu.BaseForm, true);
                    }
                }
                return;
            }

            var targetPhys = collision.gameObject.GetComponent<PhysicalBehaviour>();
            if (targetPhys == null) return;
            Rigidbody2D physRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (physRb == null) return;

            nextHitAllowed = Time.time + HitCooldownSeconds;

            float objSpeedMultiplier = Mathf.Clamp(speed, 5f, 100f) / 5f;
            float objRealDamage = (meleeDamage * Mathf.Max(0.01f, Multipliers.FinalMultiplier)) * DamageOutput * CombatPowerController.OutputFor(person);
            float objFinalDamage = objRealDamage * objSpeedMultiplier;
            float objDamagePercentage = (objFinalDamage / targetPhys.TrueInitialMass) / 100f;

            if (speed > 15f && objFinalDamage >= 10000f)
                CheckSurroundings(physRb, collision.contacts[0].point, Mathf.Clamp(objDamagePercentage * 200f, 20f, 100f), objFinalDamage);

            if (targetPhys.TrueInitialMass >= objRealDamage / 150f)
            {
                if (!isResettingMovement && DBPU.PhysAtkStopLimbs) StartCoroutine(ResetMovementForObject(targetPhys));
                if (DBPU.PhysAtkStopLimbs) return;
            }

            float objKbImpulse = maxKnockbackImpulse * knockbackOutput * DBPU.Universal_KnockbackMultiplier;
            float objKbCap = maxKnockback * knockbackOutput * DBPU.Universal_KnockbackMultiplier;
            float objKnockbackImpulse = (objDamagePercentage >= 25f) ? objKbImpulse : objKbImpulse * (objDamagePercentage / 25f);

            if (DBPU.hitParticle != null)
            {
                GameObject particle = Instantiate(DBPU.hitParticle, collision.contacts[0].point, DBPU.hitParticle.transform.rotation);
                Vector2 direction = ((Vector2)physRb.transform.position - (Vector2)transform.position).normalized;
                particle.transform.rotation = Quaternion.Euler(0, 0, (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f);
                particle.transform.localScale = new Vector3(Mathf.Clamp(objDamagePercentage / 500, 0, 0.833332f), Mathf.Clamp(objDamagePercentage / 500, 0, 0.833332f), 0.5f);

                foreach (Transform child in particle.transform)
                {
                    child.localScale = new Vector3(Mathf.Clamp(objDamagePercentage / 150, 0, 2.777f), Mathf.Clamp(objDamagePercentage / 150, 0, 2.777f), 0.5f);
                    var ps1renderer = child.gameObject.GetComponent<ParticleSystemRenderer>();
                    if (ps1renderer != null) ps1renderer.sortingLayerName = "Top";
                }

                ParticleSystem ps = particle.GetComponent<ParticleSystem>();
                var psrenderer = particle.GetComponent<ParticleSystemRenderer>();
                if (psrenderer != null) psrenderer.sortingLayerName = "Top";
                if (ps != null)
                {
                    ps.Play();
                    Destroy(particle, ps.main.duration);
                }
                else Destroy(particle, 1f);
            }

            if (speed > 10f && objRealDamage >= 50000000000f && enableNuclearExplosion == true && CanNukeRN == true) NukeShockwave(collision.contacts[0].point);

            Vector2 objForceDirection = ((Vector2)physRb.transform.position - (Vector2)transform.position).normalized;
            physRb.AddForce(objForceDirection * Mathf.Clamp(Mathf.Abs(objKnockbackImpulse - objKnockbackImpulse * 0.01f * (physRb.mass / 0.14f)), 0, objKbCap * (physRb.mass / 0.5f)), ForceMode2D.Impulse);

            if (objDamagePercentage > 0.5f && physRb.gameObject.GetComponent<DestroyableBehaviour>() && speed > 10f && DBPU.PhysAtkCanDestroy == true)
            {
                if (objDamagePercentage / 5f > UnityEngine.Random.Range(0, 1f)) physRb.gameObject.GetComponent<DestroyableBehaviour>().Break();
            }
        }

        public void CheckSurroundings(Rigidbody2D targetRb, Vector2 point, float range, float DamageDealt)
        {
            float cappedRange = range;
            foreach (Collider2D collider2D in Physics2D.OverlapCircleAll(point, 0.5f * cappedRange, MCP_Utils.ObjectsDebrisMask))
            {
                if ((bool)(UnityEngine.Object)collider2D.attachedRigidbody && collider2D.attachedRigidbody.transform.root.gameObject != targetRb.transform.root.gameObject)
                {
                    var targetLimb = collider2D.GetComponent<LimbBehaviour>();
                    if (targetLimb != null && targetLimb.Person != person)
                    {
                        Rigidbody2D newTargetRb = collider2D.attachedRigidbody;

                        float newDamagePercentage = (DamageDealt / targetLimb.InitialHealth) * 100f;
                        float newKnockbackImpulse = (newDamagePercentage >= 25f) ? maxKnockbackImpulse : maxKnockbackImpulse * (newDamagePercentage / 25f);
                        Vector2 newForceDirection = ((Vector2)newTargetRb.transform.position - point).normalized;

                        float rangePercentage = Mathf.Clamp(Vector3.Distance(newTargetRb.transform.position, point) / (0.4f * cappedRange), 0.2f, 1f);
                        rangePercentage /= 50f;

                        if (newDamagePercentage / 20f > 5f)
                        {
                            newTargetRb.AddForce(newForceDirection * Mathf.Clamp(Mathf.Abs(newKnockbackImpulse - newKnockbackImpulse * 0.2f * (newTargetRb.mass / 0.14f)) * rangePercentage, 0, maxKnockback * (newTargetRb.mass / 0.8f)), ForceMode2D.Impulse);
                        }
                    }

                    else if (targetLimb == null && collider2D.gameObject.GetComponent<PhysicalBehaviour>())
                    {
                        var targetPhys = collider2D.gameObject.GetComponent<PhysicalBehaviour>();
                        Rigidbody2D newTargetRb = collider2D.attachedRigidbody;

                        float newDamagePercentage = (DamageDealt / targetPhys.TrueInitialMass) / 100f;
                        float newKnockbackImpulse = (newDamagePercentage >= 25f) ? maxKnockbackImpulse : maxKnockbackImpulse * (newDamagePercentage / 25f);
                        Vector2 newForceDirection = ((Vector2)newTargetRb.transform.position - point).normalized;

                        float rangePercentage = Mathf.Clamp(Vector3.Distance(newTargetRb.transform.position, point) / (0.4f * cappedRange), 0.05f, 1f);
                        rangePercentage /= 5f;

                        if (newDamagePercentage / 20f > 5f)
                        {
                            newTargetRb.AddForce(newForceDirection * Mathf.Clamp(Mathf.Abs(newKnockbackImpulse - newKnockbackImpulse * 0.01f * (newTargetRb.mass / 0.14f)) * rangePercentage, 0, maxKnockback * (newTargetRb.mass / 0.8f)), ForceMode2D.Impulse);
                        }
                    }
                }
            }
        }

        public async void NukeShockwave(Vector2 point)
        {
            NukeCooldown();

            var nuke = Instantiate(ModAPI.FindSpawnable("Atom Bomb").Prefab, point, Quaternion.identity);
            var phys = nuke.gameObject.GetComponent<PhysicalBehaviour>();
            var atom = nuke.gameObject.GetComponent<AtomBombBehaviour>();

            phys.ForceSendUse();
            NewAtomBombExplosionBehaviour[] explosions = GameObject.FindObjectsOfType<NewAtomBombExplosionBehaviour>();

            foreach (var explosion in explosions)
            {
                explosion.VaporisationDistance = 0f;
                var child0 = explosion.transform.GetChild(0);
                var pas = child0.GetComponent<ParticleSystem>();
                pas.Stop();
            }
        }

        public async void NukeCooldown()
        {
            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject.GetComponent<PunchPower>())
                {
                    limb.gameObject.GetComponent<PunchPower>().CanNukeRN = false;
                }
            }

            await Task.Delay(1000);

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject.GetComponent<PunchPower>())
                {
                    limb.gameObject.GetComponent<PunchPower>().CanNukeRN = true;
                }
            }
        }

        private void DamageLimb(LimbBehaviour limb, float damage, Vector2 hitPoint)
        {
            float damagePercentage = damage / limb.InitialHealth;
            float bluntVisualDamage = (100f * Mathf.Clamp(damagePercentage, 0f, 0.5f)) / limb.SkinMaterialHandler.intensityMultiplier;
            float bulletVisualDamage = (100f * Mathf.Clamp(damagePercentage, 0f, 0.5f)) / limb.SkinMaterialHandler.intensityMultiplier;

            if (damagePercentage > 0.001f)
            {
                limb.Damage(damage);
                limb.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, hitPoint, bluntVisualDamage);
                limb.SkinMaterialHandler.AddDamagePoint(DamageType.Bullet, hitPoint, bulletVisualDamage);
            }
        }

        static void StopRigidbodyMotion(Rigidbody2D rb)
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        private IEnumerator ResetMovement(LimbBehaviour targetLimb)
        {
            if (targetLimb == null) yield break;

            Rigidbody2D targetRb = targetLimb.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Static) yield break;

            isResettingMovement = true;
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                StopRigidbodyMotion(rbSelf);
                StopRigidbodyMotion(targetRb);
                elapsed += Time.deltaTime;
                yield return null;
            }
            isResettingMovement = false;
        }

        private IEnumerator ResetMovementForObject(PhysicalBehaviour phys)
        {
            if (phys == null) yield break;

            Rigidbody2D targetRb = phys.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Static) yield break;

            isResettingMovement = true;
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                StopRigidbodyMotion(rbSelf);
                StopRigidbodyMotion(targetRb);
                elapsed += Time.deltaTime;
                yield return null;
            }

            isResettingMovement = false;
        }
    }
}
