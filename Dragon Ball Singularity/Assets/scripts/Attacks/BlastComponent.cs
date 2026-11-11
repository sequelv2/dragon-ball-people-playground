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
    public class BlastComponent : MonoBehaviour
    {
        public GameObject User;
        public bool HasHit = false;
        public bool AlreadyHit = false;
        public float Damage = 10f;
        public float KnockbackMultiplier = 1f;
        public float ExplosionSizeMultiplier = 1f;
        public string ID = "Blast";
        public bool HandleSelfMovement = true;
        public float speed = 80f;
        public float realSpeed = 80f;
        private bool underWater = false;
        private bool waterStateChange = false;
        // What it is and how it looks, set by whoever fires it.
        public string Technique;
        public Color Tint = Color.clear;
        public float ShotSize, ShotTail;
        public BlastOrb Orb;
        public bool Held, Redirected;
        bool wentOff;

        public void Start()
        {
            gameObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
            gameObject.GetComponent<Rigidbody2D>().mass = 0.0001f;
            gameObject.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (OrbFx.Active && Orb == null) Orb = BlastOrb.Attach(this);
        }

        // Knocked somewhere else, possibly now on someone else's side.
        public void Redirect(Vector2 velocity, PersonBehaviour newOwner)
        {
            Redirected = true;
            HandleSelfMovement = true;
            AlreadyHit = false; HasHit = false;
            speed = realSpeed = velocity.magnitude;
            if (velocity.sqrMagnitude > .01f) transform.right = velocity.normalized;
            var body = GetComponent<Rigidbody2D>();
            if (body && body.bodyType != RigidbodyType2D.Static) body.velocity = velocity;
            if (!newOwner) return;
            var own = GetComponent<Collider2D>();
            var previous = User ? User.GetComponent<PersonBehaviour>() : null;
            if (own && previous && previous != newOwner)
                foreach (var limb in previous.Limbs) if (limb) { var c = limb.GetComponent<Collider2D>(); if (c) Physics2D.IgnoreCollision(own, c, false); }
            if (own)
                foreach (var limb in newOwner.Limbs) if (limb) { var c = limb.GetComponent<Collider2D>(); if (c) Physics2D.IgnoreCollision(own, c, true); }
            User = newOwner.gameObject;
            ID = "Redirected" + Random.Range(0, 100000);
        }

        public void Update()
        {
            if (Held) return;
            if (KiShield.HandleBlast(this)) return;
            if (HandleSelfMovement == true)
            {
                waterStateChange = false;
                if (gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) gameObject.GetComponent<Rigidbody2D>().velocity = transform.right * realSpeed;
            }

            else if (HandleSelfMovement == false)
            {
                if (waterStateChange == true)
                {
                    waterStateChange = false;
                    if (gameObject.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static) gameObject.GetComponent<Rigidbody2D>().velocity = -transform.up * realSpeed;
                }
            }

            if (IsUnderWater() && underWater == false)
            {
                waterStateChange = true;
                realSpeed = speed * 0.5f;
            }

            else if (!IsUnderWater() && underWater == true)
            {
                waterStateChange = true;
                realSpeed = speed;
            }

            if (HasHit) Collide();
        }

        void FixedUpdate() { if (!Held) KiShield.HandleBlast(this, null, Time.fixedDeltaTime); }

        public bool IsUnderWater()
        {
            return Physics2D.OverlapCircle(transform.position, 0.1f, LayerMask.GetMask("Water"));
        }

        public void OnCollisionEnter2D(Collision2D other)
        {
            if (KiShield.HandleBlast(this, other.collider)) return;
            if (Orb != null)
            {
                var met = other.gameObject.GetComponent<LimbBehaviour>();
                if (met)
                {
                    if (Orb.ImmuneTo.Contains(met.Person)) return;
                    if (Orb.Class == KiOrb.Weight.Tiny ? SpherePhysics.SlapOnContact(Orb, met) : SpherePhysics.ReactToContact(Orb, met)) return;
                }
            }
            else if (KiDeflectUtils.TryDeflectBlast(this, other))
                return;
            if (AlreadyHit == false && other.transform.root.gameObject != User && ((other.gameObject.GetComponent<BlastComponent>() && other.gameObject.GetComponent<BlastComponent>().ID != ID) || !other.gameObject.GetComponent<BlastComponent>()) && (other.gameObject.layer == 9 || other.gameObject.layer == 11 || other.gameObject.layer == 13))
            {
                AlreadyHit = true;
                StartCoroutine(OnHit(other.gameObject));
            }
        }

        public IEnumerator OnHit(GameObject other)
        {
            if (other.gameObject.GetComponent<BlastComponent>())
            {
                if (other.gameObject.GetComponent<BlastComponent>().Damage >= Damage / 10f) Collide();
                else AlreadyHit = false;
            }

            else if (other.gameObject.layer == 9 || other.gameObject.layer == 11 || other.gameObject.layer == 13)
            {
                if (other.gameObject.GetComponent<LimbBehaviour>())
                {
                    var hitLimb = other.gameObject.GetComponent<LimbBehaviour>();
                    if (KiShield.Protects(hitLimb.Person, User, transform.position,
                        Damage / .12f, GetInstanceID())) { Collide(); yield break; }
                    var energyOwner = Orb != null ? Orb.Owner : User ? User.GetComponent<PersonBehaviour>() : null;
                    double energyPower = Orb != null ? Orb.Power : DbpuFx.PowerOf(energyOwner) * .3d;
                    EnergyAnnihilation.Expose(energyOwner, hitLimb, KiCombatMath.Cap(energyPower),
                        hitLimb.transform.position, .12f, Tint.a > 0f ? Tint : new Color(1f, .85f, .3f));
                    if (MCP_Utils.MeetsMinLimbDamage(hitLimb, Damage)) hitLimb.Damage(Damage);
                }

                Collider2D[] hits = Physics2D.OverlapCircleAll(
                    point: transform.position,
                    radius: 20f * Mathf.Max(0.1f, ExplosionSizeMultiplier),
                    layerMask: LayerMask.GetMask("Objects", "CollidingDebris")
                    );

                float maxDistance = 20f * Mathf.Max(0.1f, ExplosionSizeMultiplier);

                foreach (var hit in hits)
                {
                    if (hit.gameObject == other.gameObject || hit.transform.root.gameObject == User || !hit.gameObject.GetComponent<Rigidbody2D>() || other.gameObject.layer == 11) continue;

                    Vector3 direction = (hit.transform.position - transform.position).normalized;
                    float Distance = Mathf.Clamp(Vector2.Distance(transform.position, hit.transform.position), 1f, maxDistance);

                    Rigidbody2D rb = hit.gameObject.GetComponent<Rigidbody2D>();

                    if (hit.gameObject.GetComponent<LimbBehaviour>() && hit.isTrigger == false)
                    {
                        var hitLimb = hit.gameObject.GetComponent<LimbBehaviour>();
                        if (KiShield.Protects(hitLimb.Person, User, transform.position, Damage / Mathf.Max(.001f, Time.deltaTime), GetInstanceID())) continue;
                        if (MCP_Utils.MeetsMinLimbDamage(hitLimb, Damage))
                        {
                            if (Distance < 5f * Mathf.Max(0.1f, ExplosionSizeMultiplier))
                            {
                                var energyOwner = Orb != null ? Orb.Owner : User ? User.GetComponent<PersonBehaviour>() : null;
                                double energyPower = Orb != null ? Orb.Power : DbpuFx.PowerOf(energyOwner) * .3d;
                                EnergyAnnihilation.Expose(energyOwner, hitLimb,
                                    KiCombatMath.Cap(energyPower / Mathf.Clamp(Distance * Distance, 1f, 25f)),
                                    hit.ClosestPoint(transform.position), .12f, Tint.a > 0f ? Tint : new Color(1f, .85f, .3f));
                                hitLimb.Damage(Damage / Mathf.Clamp(Distance * Distance, 1f, 25f));
                            }

                            float ForcePercent = (Damage * 5f) / MCP_Utils.AverageMaxHealth(hitLimb.Person);

                            rb.AddForce(direction * ((Mathf.Clamp(1000f / Distance, 10f, 250f) * rb.mass * Mathf.Clamp(ForcePercent, 0.25f, 1f) * KnockbackMultiplier * DBPU.Universal_KnockbackMultiplier)), ForceMode2D.Impulse);
                        }
                    }

                    else if (hit.gameObject.GetComponent<PhysicalBehaviour>())
                    {
                        rb.AddForce(direction * (Mathf.Clamp(100f / Distance, 1f, 20f) * rb.mass), ForceMode2D.Impulse);

                        if (hit.gameObject.GetComponent<PhysicalBehaviour>())
                        {
                            if (DBPU.KiAtkCanDestroy)
                            {
                                if (hit.gameObject.GetComponent<DestroyableBehaviour>())
                                {
                                    if (UnityEngine.Random.Range(5, 20) > Distance) hit.gameObject.GetComponent<DestroyableBehaviour>().Break();
                                }

                                if (hit.gameObject.GetComponent<DamagableMachineryBehaviour>() && hit.gameObject.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                                {
                                    if (UnityEngine.Random.Range(10, 20) > Distance) hit.gameObject.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                                }
                            }

                            if (DBPU.KiAtkCanDelete && Distance < 8f * Mathf.Max(0.1f, ExplosionSizeMultiplier) && !hit.gameObject.GetComponent<LimbBehaviour>())
                            {
                                Destroy(hit.gameObject);
                            }
                        }
                    }
                }

                Collide();
            }

            yield return null;
        }

        public void Collide()
        {
            if (wentOff) return;
            if (OrbFx.Active && Technique != "StardustBreaker")
            {
                wentOff = true;
                GoOff();
                return;
            }
            if (Orb != null) { wentOff = true; Orb.WentOff(); }
            if (gameObject.GetComponent<TrailRenderer>())
            {
                GameObject Boom = Instantiate(DBPU.KiBlast_Hit, transform.position, Quaternion.identity);
                float scale = Random.Range(6f, 9f);
                Boom.transform.localScale = new Vector3(scale, scale, scale);
                Boom.gameObject.AddComponent<BlastImpactCooldown>().time = 2.4f;
                //Blast.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                foreach (Transform child in Boom.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = "Bubbles";
                    sp.sortingOrder += 10;

                    var ps = child.gameObject.GetComponent<ParticleSystem>();
                    ps.playbackSpeed *= 0.2f;
                }

                AudioSource BoomAudio = new GameObject("ImpactAudioSource").AddComponent<AudioSource>();
                BoomAudio.transform.SetParent(Boom.transform, false);
                Global global = FindObjectOfType<Global>();
                global.AddAudioSource(BoomAudio, false);
                BoomAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.8f;
                BoomAudio.outputAudioMixerGroup = global.SoundEffects;
                BoomAudio.spatialBlend = 1;
                BoomAudio.volume = DBPU.PowersVolume;
                BoomAudio.maxDistance = 1000;
                BoomAudio.PlayOneShot(DBPU.Attack_KiBlast_Impact);

                gameObject.AddComponent<FreezeBehaviour>();
                gameObject.GetComponent<CircleCollider2D>().enabled = false;

                CeceUtils.DelayedInvoke(gameObject.GetComponent<TrailRenderer>().time, () =>
                {
                    Destroy(gameObject);
                });
            }

            else
            {
                GameObject Boom = Instantiate(DBPU.KiBlast_Hit, transform.position, Quaternion.identity);
                Boom.transform.localScale = new Vector3(2.3f * ExplosionSizeMultiplier, 2.3f * ExplosionSizeMultiplier, 2.3f * ExplosionSizeMultiplier);
                Boom.gameObject.AddComponent<BlastImpactCooldown>().time = 2f * ExplosionSizeMultiplier;
                //Blast.transform.Find("Blast Color").gameObject.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;

                foreach (Transform child in Boom.transform)
                {
                    var sp = child.gameObject.GetComponent<Renderer>();
                    sp.sortingLayerName = "Bubbles";
                    sp.sortingOrder += 10;

                    var ps = child.gameObject.GetComponent<ParticleSystem>();
                    ps.playbackSpeed *= 0.75f / ExplosionSizeMultiplier;
                }

                AudioSource BoomAudio = new GameObject("ImpactAudioSource").AddComponent<AudioSource>();
                BoomAudio.transform.SetParent(Boom.transform, false);
                Global global = FindObjectOfType<Global>();
                global.AddAudioSource(BoomAudio, false);
                BoomAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.8f;
                BoomAudio.outputAudioMixerGroup = global.SoundEffects;
                BoomAudio.spatialBlend = 1;
                BoomAudio.volume = DBPU.PowersVolume;
                BoomAudio.maxDistance = 1000;
                BoomAudio.PlayOneShot(DBPU.Attack_KiBlast_Impact);

                Destroy(gameObject);
            }
        }

        void GoOff()
        {
            Vector2 at = transform.position;
            var look = Orb != null ? Orb.Look : OrbStyle.Tinted(Tint.a > 0f ? Tint : new Color(1f, .85f, .3f));
            float radius = 2.2f * Mathf.Pow(Mathf.Max(.1f, ExplosionSizeMultiplier), .8f);
            if (Orb != null && Orb.Class != KiOrb.Weight.Tiny) OrbFx.Explosion(at, radius, look, Vector2.up);
            else OrbFx.SmallBurst(at, radius, look.Mid);
            if (Orb != null) Orb.WentOff();
            OrbShots.Remove(transform);
            AudioSource boom = new GameObject("ImpactAudioSource").AddComponent<AudioSource>();
            boom.transform.position = at;
            Global global = Global.main ? Global.main : FindObjectOfType<Global>();
            global.AddAudioSource(boom, false);
            boom.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.8f;
            boom.outputAudioMixerGroup = global.SoundEffects;
            boom.spatialBlend = 1;
            boom.volume = DBPU.PowersVolume;
            boom.maxDistance = 1000;
            boom.PlayOneShot(DBPU.Attack_KiBlast_Impact);
            boom.gameObject.AddComponent<BlastImpactCooldown>().time = 2.4f;
            var trail = GetComponent<TrailRenderer>();
            if (trail)
            {
                var still = GetComponent<Rigidbody2D>();
                if (still) still.simulated = false;
                GetComponent<CircleCollider2D>().enabled = false;
                CeceUtils.DelayedInvoke(trail.time, () => { if (this) Destroy(gameObject); });
            }
            else Destroy(gameObject);
        }

        public class BlastImpactCooldown : MonoBehaviour
        {
            public float time = 2f;
            public void Start()
            {
                StartCoroutine(Cooldown());
            }

            public IEnumerator Cooldown()
            {
                yield return new WaitForSeconds(time);

                Destroy(gameObject);
            }
        }
    }
}
