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
    public class FinalExplosion : MonoBehaviour
    {
        private MultipliersAndStuffs Multipliers;
        private KiSystem KiSys;

        private AudioSource audioSource;

        private GameObject sphere;
        private GameObject outerSphere;
        private GameObject glowAnchor;
        private LightSprite glow;
        private OrbView view;
        private float fxClock;

        public float maxScale = 580f;
        public float growSpeed = 80f;
        public float Damage = 0f;
        public float DamageOutput = 1f;
        public float KnockbackOutput = 1f;

        public bool Active = false;
        public bool stillActive = false;
        public bool Firing = false;
        public float RealDamage = 100f;
        public float CostPercent = 0.6f;

        private void OnDestroy()
        {
            if (Firing == true)
            {
                Destroy(sphere);
                Destroy(outerSphere);
                Destroy(glowAnchor);
                Destroy(glow);
            }
            DropView();
        }

        void DropView() { if (view != null) { view.Destroy(); view = null; } }

        // The dome matches the reach of the damage, and tears up the ground where it meets it.
        void DrawDome(float fade)
        {
            float dt = DbpuFx.Dt;
            Vector2 c = sphere.transform.position;
            float radius = sphere.transform.localScale.x / 3f * 1.05f;
            view.Opacity = fade;
            view.Heat = (1f - fade) * .2f;
            view.Place(c, Mathf.Max(radius, .05f), dt);
            var look = view.Look;
            BeamScene.Request(c, Vector2.up, look.Mid, look.Mid, Mathf.Clamp01(fade * 1.2f));
            fxClock -= dt;
            if (fxClock > 0f || fade < .5f) return;
            fxClock = .06f;
            DbpuFx.Shake(c, Mathf.Min(1f + radius * .05f, 3.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 ground, n;
                if (!BeamGround.Find(c + new Vector2(side * radius * .96f, 6f), radius + 12f, null, out ground, out n)) continue;
                BeamSlabs.Raise(ground, Random.Range(.8f, 2.2f) * Mathf.Clamp(radius * .05f, 1f, 3f), side * Random.Range(4f, 34f), look.Mid);
                BeamGround.Tear(ground, new Vector2(side, .5f), 1f, look.Mid);
                if (Random.value < .35f) BeamGround.Scorch(ground, Mathf.Clamp(radius * .12f, 2f, 7f), look.Mid);
            }
        }

        private void Awake()
        {
            audioSource = new GameObject("ExplosionAudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Head"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.85f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;
        }

        private void FixedUpdate()
        {
            if (stillActive == true)
            {
                foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
                {
                    limb.Health -= RealDamage / 2f;
                }
            }
        }

        private void Update()
        {
            if (audioSource.volume != DBPU.PowersVolume) audioSource.volume = DBPU.PowersVolume;

            if (stillActive == true)
            {
                RealDamage = Damage * Multipliers.FinalMultiplier * Multipliers.KiOutput * DamageOutput;

                float scaleIncrease = growSpeed * Time.deltaTime;

                if (Firing == true)
                {
                    sphere.transform.localScale += Vector3.one * scaleIncrease;
                    sphere.transform.position -= new Vector3(0f, 0f, 33f * Time.deltaTime);
                    sphere.transform.Rotate(-60f * Time.deltaTime, 200f * Time.deltaTime, 0f);

                    outerSphere.transform.localScale = sphere.transform.localScale * 1.025f;
                    outerSphere.transform.position = sphere.transform.position;
                    outerSphere.transform.Rotate(-80f * Time.deltaTime, 250f * Time.deltaTime, 0f);

                    float currentScale = sphere.transform.localScale.x;

                    if (glow != null)
                    {
                        glow.transform.localScale = Vector3.one * currentScale * 1.5f;
                        glow.Brightness = currentScale / 5f;
                    }

                    if (view != null) DrawDome(1f);

                    if (currentScale >= maxScale)
                    {
                        Firing = false;
                    }
                }

                else if (stillActive == true)
                {
                    sphere.transform.localScale += Vector3.one * scaleIncrease;
                    sphere.transform.position -= new Vector3(0f, 0f, 35f * Time.deltaTime);
                    sphere.transform.Rotate(60f * Time.deltaTime, 200f * Time.deltaTime, 0f);

                    outerSphere.transform.localScale = sphere.transform.localScale * 1.025f;
                    outerSphere.transform.position = sphere.transform.position;
                    outerSphere.transform.Rotate(80f * Time.deltaTime, 250f * Time.deltaTime, 0f);

                    float currentScale = sphere.transform.localScale.x;

                    sphere.GetComponent<Renderer>().material.color -= new Color(0f, 0f, 0f, 0.5f * Time.deltaTime);
                    outerSphere.GetComponent<Renderer>().material.color -= new Color(0f, 0f, 0f, 1f * Time.deltaTime);

                    if (glow != null)
                    {
                        glow.transform.localScale = Vector3.one * currentScale * 1.5f;
                        glow.Brightness = ((currentScale / 5f) * 2f) * sphere.GetComponent<Renderer>().material.color.a;
                    }

                    if (view != null) DrawDome(Mathf.Clamp01(sphere.GetComponent<Renderer>().material.color.a * 2f));

                    if (sphere.GetComponent<Renderer>().material.color.a <= 0f)
                    {
                        stillActive = false;
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

            if (Damage == 0f) Damage = (CombatPowerController.AttackHealth(GetComponent<PersonBehaviour>()) / Multipliers.FinalMultiplier) / 100f;

            GetComponent<PersonBehaviour>().Limbs[2].gameObject.AddComponent<UseEventTrigger>().Action = () =>
            {
                if (!GetComponent<PersonBehaviour>().IsAlive() || GetComponent<PersonBehaviour>().Limbs[2].Health <= 0f || Active == false || Firing == true || KiSys.currentKi < KiSys.maxKi * CostPercent) return;

                StartCoroutine(Fire(GetComponent<PersonBehaviour>().Limbs[2].gameObject.transform));
            };
        }

        public bool EnoughKi
        {
            get
            {
                var ki = KiSys ? KiSys : GetComponent<KiSystem>();
                return !ki || ki.currentKi >= ki.maxKi * CostPercent;
            }
        }

        // The torso technique picker detonates directly: the Use trigger's checks, without the
        // powers-menu arming step. Returns null once it fires, otherwise why it could not.
        public string TryDetonate()
        {
            var person = GetComponent<PersonBehaviour>();
            if (!person || !person.IsAlive() || person.Limbs == null || person.Limbs.Length < 3) return "BLOCKED";
            var middle = person.Limbs[2];
            if (!middle || middle.Health <= 0f) return "HURT";
            var beam = GetComponent<BeamBehaviour>();
            if (Firing || stillActive || (beam != null && beam.StillActive)) return "BUSY";
            if (!KiSys) KiSys = GetComponent<KiSystem>();
            if (!Multipliers) Multipliers = GetComponent<MultipliersAndStuffs>();
            if (!KiSys || !Multipliers) return "BLOCKED";
            if (!EnoughKi) return "LOW KI";
            StartCoroutine(Fire(middle.transform));
            return null;
        }

        private IEnumerator Fire(Transform Middle)
        {
            Firing = true;
            stillActive = true;

            StartCoroutine(KiCost());

            audioSource.PlayOneShot(DBPU.Attack_FinalExplosion);

            if (DBPU.VoiceLines == true) audioSource.PlayOneShot(DBPU.Voice_Vegeta_FinalExplosion);

            Material newMaterial = new Material(Shader.Find("Sprites/Default"));

            newMaterial.color = Color.white;

            if (DBPU.FinalExplosion_Texture != null)
            {
                newMaterial.mainTexture = DBPU.FinalExplosion_Texture.texture;
            }

            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = Middle.position + new Vector3(0f, -1f);
            sphere.transform.localScale = Vector3.zero;

            Renderer renderer = sphere.GetComponent<Renderer>();
            renderer.material = newMaterial;
            renderer.material.color = new Color(1f, 1f, 1f, 0.5f);
            renderer.sortingLayerName = "Bubbles";

            Material newMaterial2 = new Material(Shader.Find("Sprites/Default"));

            newMaterial2.color = Color.white;

            if (DBPU.FinalExplosion_OuterTexture != null)
            {
                newMaterial2.mainTexture = DBPU.FinalExplosion_OuterTexture.texture;
            }

            outerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            outerSphere.transform.position = sphere.transform.position;
            outerSphere.transform.localScale = Vector3.zero;

            Renderer renderer2 = outerSphere.GetComponent<Renderer>();
            renderer2.material = newMaterial2;
            renderer2.material.color = new Color(1f, 1f, 1f, 1f);
            renderer2.sortingLayerName = "Bubbles";
            renderer2.sortingOrder = renderer.sortingOrder + 1;

            sphere.AddComponent<BallCollision>().User = transform;
            sphere.GetComponent<BallCollision>().finalExplosion = gameObject.GetComponent<FinalExplosion>();

            glowAnchor = new GameObject("GlowAnchor");
            glowAnchor.transform.position = sphere.transform.position;
            glowAnchor.transform.SetParent(null);

            glow = ModAPI.CreateLight(glowAnchor.transform, new Color(252f / 255f, 255f / 255f, 94f / 255f, 0.15f), 3f, 3f);

            if (OrbFx.Active)
            {
                // Same reach and timing as ever; only the drawing changes.
                renderer.enabled = false; renderer2.enabled = false;
                view = new OrbView(OrbStyle.Of("Final Explosion"), "Bubbles", 80);
                var look = view.Look;
                BeamContact.Burst(sphere.transform.position, Vector2.up, look.Beam, look.Beam, 2.6f);
                SingularityTime.Hitstop(.14f, .05f);
            }

            yield return new WaitUntil(() => Firing == false && stillActive == false);

            DropView();

            Destroy(sphere);
            Destroy(outerSphere);
            Destroy(glowAnchor);
            Destroy(glow);
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

        private class BallCollision : MonoBehaviour
        {
            public FinalExplosion finalExplosion;
            public Transform User;

            public void FixedUpdate()
            {
                foreach (Collider2D collider2D in Physics2D.OverlapCircleAll(transform.position, finalExplosion.sphere.transform.localScale.x / 3f, LayerMask.GetMask("Objects", "CollidingDebris")))
                {
                    if (collider2D.isTrigger || collider2D.gameObject.transform.root.transform == User || !collider2D.gameObject.GetComponent<Rigidbody2D>()) continue;

                    if (collider2D.gameObject.GetComponent<BlastComponent>())
                    {
                        collider2D.gameObject.GetComponent<BlastComponent>().HasHit = true;
                        continue;
                    }

                    StartCoroutine(OnHit(collider2D.gameObject, (collider2D.gameObject.transform.position - transform.position).normalized));
                }
            }

            public IEnumerator OnHit(GameObject other, Vector2 direction)
            {
                float TimeScale = Global.main.SlowmotionTimescale;
                if (!Global.main.SlowMotion) TimeScale = 1f;

                Rigidbody2D rb = other.GetComponent<Rigidbody2D>();

                if (other.GetComponent<LimbBehaviour>()/* && other.GetComponent<Collider2D>().isTrigger != true*/)
                {
                    LimbBehaviour limb = other.GetComponent<LimbBehaviour>();
                    if (limb.Person && KiShield.Protects(limb.Person, finalExplosion.gameObject,
                        transform.position, (double)finalExplosion.RealDamage / Mathf.Max(.001f, Time.fixedDeltaTime),
                        finalExplosion.GetInstanceID())) yield break;
                    var energyOwner = finalExplosion.GetComponent<PersonBehaviour>();
                    EnergyAnnihilation.Expose(energyOwner, limb,
                        KiCombatMath.Cap(DbpuFx.PowerOf(energyOwner) * Mathf.Max(0f, finalExplosion.DamageOutput)),
                        limb.transform.position, Time.fixedDeltaTime, new Color(1f, .85f, .35f));

                    float ForcePercent = (finalExplosion.RealDamage * 10f) / MCP_Utils.AverageMaxHealth(limb.Person);

                    if (ForcePercent > 0.005f)
                    {
                        limb.Damage(finalExplosion.RealDamage * TimeScale);

                        float DistanceMultiplier = Mathf.Clamp((Vector3.Distance(other.transform.position, transform.position) / (finalExplosion.sphere.transform.localScale.x / 3f)), 0.02f, 1f);

                        rb.AddForce(direction * (((40f / DistanceMultiplier) * TimeScale) * rb.mass * Mathf.Clamp(ForcePercent, 0.25f, 1f)), ForceMode2D.Impulse);
                    }
                }

                else if (!other.GetComponent<LimbBehaviour>() && other.GetComponent<PhysicalBehaviour>())
                {
                    float DistanceMultiplier = Mathf.Clamp((Vector3.Distance(other.transform.position, transform.position) / (finalExplosion.sphere.transform.localScale.x / 3f)), 0.02f, 1f);

                    rb.AddForce(direction * (((5f / DistanceMultiplier) * TimeScale) * rb.mass), ForceMode2D.Impulse);

                    if (DBPU.Beam_ToggleDestroy == true)
                    {
                        if (other.GetComponent<DestroyableBehaviour>())
                        {
                            other.GetComponent<DestroyableBehaviour>().Break();
                        }

                        if (other.GetComponent<DamagableMachineryBehaviour>() && other.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                        {
                            other.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                        }
                    }
                }

                yield return null;
            }
        }
    }
}
