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
    public class CellMaxEyeLaserBehaviour : MonoBehaviour
    {
        class EyeLaserBeam
        {
            public LineRenderer core;
            public LineRenderer glow;
            public Transform head;
            public int eyeIndex;
            public Vector2 localOrigin;
            public float damageTimer;
            public float hitFxTimer;
            public Vector2 lastHitPoint;
            public bool hasLastHit;
            public bool fadingOut;
            public float fadeAlpha = 1f;
            public bool finished;
            public BeamPhysics.RayState ray;
        }

        static readonly Color DefaultCoreColor = new Color(0.85f, 0.45f, 1f, 0.95f);
        static readonly Color DefaultGlowColor = new Color(0.72f, 0.28f, 1f, 0.55f);
        static readonly Color PiccoloCoreColor = new Color(1f, 0.95f, 0.22f, 0.95f);
        static readonly Color PiccoloGlowColor = new Color(1f, 0.82f, 0.08f, 0.5f);
        const float PiccoloCoreWidth = 0.08f;

        const float BeamMaxRange = 250f;
        const float BeamFadeOutDuration = 0.4f;
        const float EyeLaserDamageTickInterval = 0.03f;
        const float HitFxCooldown = 0.22f;
        const float HitFxMinMove = 0.5f;
        const float DefaultFireDuration = 7f;
        const float EyeLaserDamageDivisor = 250f;
        const float EyeLaserDamageScale = 4f;
        const float EyeLaserTickDamageScale = 1.6f;
        const float EyeLaserKnockbackScale = 5f;
        const float EyeLaserForceScale = 140f;

        public bool Active;
        public bool Firing;
        public bool EnableEyeGlow = true;
        public bool UsePiccoloEyeMarkers;

        public Color BeamCoreColor = DefaultCoreColor;
        public Color BeamGlowColor = DefaultGlowColor;
        public float BeamCoreWidth = 0.22f;
        public float BeamGlowWidth = 0.55f;

        PersonBehaviour person;
        CellMaxSpotGlow headSpotGlow;
        Transform headTransform;
        KiSystem kiSys;
        MultipliersAndStuffs multipliers;
        BeamBehaviour beamRef;
        Coroutine fireRoutine;
        Coroutine fadeRoutine;
        readonly List<EyeLaserBeam> beams = new List<EyeLaserBeam>();
        float damage;
        float realDamage;
        Material beamMaterial;
        float lastHitAudioTime;
        LimbBehaviour rayLimb;

        public void Setup(PersonBehaviour p)
        {
            person = p;
        }

        public void SetActive(bool active)
        {
            Active = active;
            if (!Active)
            {
                StopFiring();
                if (EnableEyeGlow && headSpotGlow != null)
                    headSpotGlow.FadeOutEyeGlow();
            }
        }

        public void RefreshHeadSpots()
        {
            var head = PowerSetup.ResolveHeadLimb(person);
            if (head != null)
            {
                headTransform = head.transform;
                headSpotGlow = head.GetComponent<CellMaxSpotGlow>();
            }
        }

        public void ApplyPiccoloBeamStyle(bool piccoloEyeMarkers)
        {
            UsePiccoloEyeMarkers = piccoloEyeMarkers;
            BeamCoreColor = PiccoloCoreColor;
            BeamGlowColor = PiccoloGlowColor;
            BeamCoreWidth = PiccoloCoreWidth;
            BeamGlowWidth = 0f;
        }

        void Awake()
        {
            kiSys = GetComponent<KiSystem>();
            multipliers = GetComponent<MultipliersAndStuffs>();
            beamRef = GetComponent<BeamBehaviour>();
            beamMaterial = new Material(ModAPI.FindMaterial("VeryBright"));
        }

        void Start()
        {
            if (person == null) person = GetComponent<PersonBehaviour>();
            RefreshHeadSpots();

            if (multipliers == null) multipliers = gameObject.AddComponent<MultipliersAndStuffs>();
            if (kiSys == null) kiSys = gameObject.AddComponent<KiSystem>();
            if (beamRef == null) beamRef = gameObject.AddComponent<BeamBehaviour>();
            if (person != null && multipliers != null)
                damage = CombatPowerController.AttackHealth(person) / multipliers.FinalMultiplier / EyeLaserDamageDivisor;

            StartCoroutine(BindHeadTrigger());
        }

        IEnumerator BindHeadTrigger()
        {
            yield return null;
            if (person == null) yield break;

            var head = PowerSetup.ResolveHeadLimb(person);
            if (head == null) yield break;

            var trigger = head.gameObject.GetOrAddComponent<UseEventTrigger>();
            Action previous = trigger.Action;
            trigger.Action = () =>
            {
                if (Active && head.Health > 0f && person.IsAlive())
                {
                    if (Firing) StopFiring();
                    else StartFiring();
                }
                previous?.Invoke();
            };
        }

        void StartFiring()
        {
            if (Firing || !Active || person == null || !person.IsAlive()) return;
            RefreshHeadSpots();
            if (headSpotGlow == null)
            {
                if (UsePiccoloEyeMarkers) PowerSetup.EnsurePiccoloEyeLaserHeadRig(person);
                else PowerSetup.EnsureEyeLaserHeadRig(person, EnableEyeGlow);
                RefreshHeadSpots();
            }
            if (headSpotGlow == null || !headSpotGlow.HasEyeLaserSpots) return;
            if (kiSys != null && kiSys.currentKi < kiSys.maxKi * 0.02f) return;

            fireRoutine = StartCoroutine(FireRoutine());
        }

        void StopFiring()
        {
            if (fireRoutine != null)
            {
                StopCoroutine(fireRoutine);
                fireRoutine = null;
            }

            Firing = false;
            if (EnableEyeGlow && headSpotGlow != null)
                headSpotGlow.FadeOutEyeGlow();
            BeginBeamFadeOut();
        }

        void BeginBeamFadeOut()
        {
            for (int i = 0; i < beams.Count; i++)
                beams[i].fadingOut = true;

            if (beams.Count > 0 && fadeRoutine == null)
                fadeRoutine = StartCoroutine(FadeBeamsRoutine());
        }

        IEnumerator FadeBeamsRoutine()
        {
            while (beams.Count > 0)
            {
                for (int i = beams.Count - 1; i >= 0; i--)
                {
                    UpdateBeam(beams[i]);
                    if (beams[i].finished)
                        RemoveBeam(i);
                }
                yield return null;
            }
            fadeRoutine = null;
        }

        Vector2 GetBeamDirection()
        {
            float facing = 1f;
            if (person != null)
                facing = Mathf.Sign(person.transform.localScale.x);
            else if (transform != null)
                facing = Mathf.Sign(transform.localScale.x);
            if (Mathf.Abs(facing) < 0.01f) facing = 1f;

            if (headTransform == null)
                return facing < 0f ? Vector2.left : Vector2.right;

            // transform.right ignores negative scale flips used by PPG facing.
            Vector2 dir = headTransform.right;
            if (facing < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.0001f)
                return facing < 0f ? Vector2.left : Vector2.right;
            return dir.normalized;
        }

        float GetBaseDamage()
        {
            if (person == null || multipliers == null) return damage;
            return CombatPowerController.AttackHealth(person) / multipliers.FinalMultiplier / EyeLaserDamageDivisor;
        }

        float GetRealDamage()
        {
            return GetBaseDamage() * multipliers.FinalMultiplier * multipliers.KiOutput * DBPU.DamageMultiplier * EyeLaserDamageScale;
        }

        float GetKnockbackOutput()
        {
            float kb = beamRef != null ? beamRef.KnockbackOutput : 1f;
            return kb * EyeLaserKnockbackScale;
        }

        IEnumerator FireRoutine()
        {
            Firing = true;
            RefreshHeadSpots();
            if (headSpotGlow != null)
            {
                headSpotGlow.ForceRefreshFromCurrentSprite();
                if (EnableEyeGlow)
                    headSpotGlow.SetEyeGlowLevel(1f, true);
            }
            SpawnEyeBeams();

            float elapsed = 0f;
            float kiTick = 0f;

            while (Firing && Active && person != null && person.IsAlive())
            {
                elapsed += Time.deltaTime;
                if (elapsed >= DefaultFireDuration)
                    break;

                realDamage = GetRealDamage();

                if (headSpotGlow != null)
                    headSpotGlow.ForceRefreshFromCurrentSprite();

                if (kiSys != null)
                {
                    kiTick += Time.deltaTime;
                    if (kiTick >= 0.15f)
                    {
                        kiTick = 0f;
                        kiSys.currentKi -= kiSys.maxKi * 0.01f * DBPU.KiAttackCostMultiplier;
                        if (kiSys.currentKi < kiSys.maxKi * 0.01f)
                        {
                            StopFiring();
                            yield break;
                        }
                    }
                }

                for (int i = beams.Count - 1; i >= 0; i--)
                {
                    UpdateBeam(beams[i]);
                    if (beams[i].finished)
                        RemoveBeam(i);
                }

                yield return null;
            }

            StopFiring();
        }

        void SpawnEyeBeams()
        {
            ClearBeams();
            if (headSpotGlow == null || headTransform == null || !headSpotGlow.HasEyeLaserSpots) return;

            Vector2[] origins = headSpotGlow.GetEyeLaserEmissionLocals();
            for (int i = 0; i < origins.Length; i++)
            {
                var beam = new EyeLaserBeam
                {
                    head = headTransform,
                    eyeIndex = i,
                    localOrigin = origins[i],
                    core = CreateBeamLine("EyeLaserCore", BeamCoreWidth, BeamCoreColor, 146),
                    glow = BeamGlowWidth > 0f ? CreateBeamLine("EyeLaserGlow", BeamGlowWidth, BeamGlowColor, 145) : null
                };
                beam.core.transform.SetParent(headTransform, false);
                if (beam.glow != null)
                    beam.glow.transform.SetParent(headTransform, false);
                beams.Add(beam);
            }
        }

        LineRenderer CreateBeamLine(string name, float width, Color color, int sortOrder)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.textureMode = LineTextureMode.Tile;
            lr.material = new Material(beamMaterial != null ? beamMaterial : ModAPI.FindMaterial("VeryBright"));
            lr.sortingLayerName = "Default";
            lr.sortingOrder = sortOrder;
            if (DBPU.StraightBeam_Texture != null)
                lr.material.mainTexture = DBPU.StraightBeam_Texture;
            lr.material.color = color;
            lr.numCapVertices = 2;
            float endScale = BeamGlowWidth > 0f ? 0.85f : 1f;
            lr.startWidth = width * DBPU.Beam_SizeMultiplier;
            lr.endWidth = width * endScale * DBPU.Beam_SizeMultiplier;
            lr.positionCount = 2;
            return lr;
        }

        void UpdateBeam(EyeLaserBeam beam)
        {
            if (beam == null || beam.core == null) return;

            if (beam.head == null && headTransform != null)
                beam.head = headTransform;

            Vector2 localOrigin = beam.localOrigin;
            if (headSpotGlow != null && headSpotGlow.HasEyeLaserSpots)
            {
                Vector2[] origins = headSpotGlow.GetEyeLaserEmissionLocals();
                if (beam.eyeIndex >= 0 && beam.eyeIndex < origins.Length)
                    localOrigin = origins[beam.eyeIndex];
            }

            Vector2 origin = beam.head != null
                ? (Vector2)beam.head.TransformPoint(localOrigin)
                : (Vector2)transform.position;

            Vector2 dir = GetBeamDirection();
            Vector2 castOrigin = origin + dir * 0.08f;
            float maxDist = GetBeamLength(castOrigin, dir);
            Vector2 end = castOrigin + dir * Mathf.Max(0.05f, maxDist - 0.08f);

            Vector2 stop = end, outDir = dir;
            var met = beam.fadingOut ? BeamPhysics.RayResult.None : BeamPhysics.MeetRay(transform.root.gameObject, rayLimb, dir, 0.7f, ref beam.ray, out stop, out outDir);
            float outLength = 0f;
            if (met == BeamPhysics.RayResult.Deflected) outLength = GetBeamLength(stop + outDir * 1.4f, outDir);
            if (met == BeamPhysics.RayResult.Blocked) end = stop;

            if (BeamPhysics.On)
            {
                bool kinked = met == BeamPhysics.RayResult.Deflected;
                BeamPhysics.DrawRay(beam.core, beam.glow, origin, kinked ? stop + outDir * (1.4f + outLength) : end, ref beam.ray.Lag, Time.deltaTime, kinked, stop);
            }
            else
            {
                beam.core.SetPosition(0, origin);
                beam.core.SetPosition(1, end);
                if (beam.glow != null)
                {
                    beam.glow.SetPosition(0, origin);
                    beam.glow.SetPosition(1, end);
                }
            }

            if (beam.fadingOut)
            {
                beam.fadeAlpha -= Time.deltaTime / BeamFadeOutDuration;
                if (beam.fadeAlpha <= 0f)
                {
                    beam.fadeAlpha = 0f;
                    beam.finished = true;
                }
                ApplyBeamAlpha(beam, beam.fadeAlpha);
                return;
            }

            ApplyBeamAlpha(beam, 1f);

            beam.hitFxTimer += Time.deltaTime;
            beam.damageTimer += Time.deltaTime;
            if (beam.damageTimer >= EyeLaserDamageTickInterval)
            {
                beam.damageTimer = 0f;
                if (met == BeamPhysics.RayResult.Deflected) ApplyDamageAlongBeam(stop + outDir * 1.4f, outDir, outLength, beam);
                else if (met == BeamPhysics.RayResult.None) ApplyDamageAlongBeam(castOrigin, dir, maxDist, beam);
            }
        }

        void ApplyBeamAlpha(EyeLaserBeam beam, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (beam.core != null && beam.core.material != null)
            {
                Color c = BeamCoreColor;
                c.a *= alpha;
                beam.core.material.color = c;
            }
            if (beam.glow != null && beam.glow.material != null)
            {
                Color c = BeamGlowColor;
                c.a *= alpha;
                beam.glow.material.color = c;
            }
        }

        void RemoveBeam(int index)
        {
            if (index < 0 || index >= beams.Count) return;
            var beam = beams[index];
            if (beam.core != null) Destroy(beam.core.gameObject);
            if (beam.glow != null) Destroy(beam.glow.gameObject);
            beams.RemoveAt(index);
        }

        float GetBeamLength(Vector2 origin, Vector2 direction)
        {
            rayLimb = null;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, BeamMaxRange);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.transform.root == transform.root) continue;
                var shell = hits[i].collider.GetComponent<KiShieldSurface>();
                if (shell && shell.Owner && shell.Owner.transform.root == transform.root) continue;
                if (hits[i].collider.gameObject.layer == 11
                    || hits[i].collider.gameObject.layer == 9
                    || hits[i].collider.gameObject.layer == 13)
                {
                    rayLimb = hits[i].collider.GetComponent<LimbBehaviour>();
                    return hits[i].distance;
                }
            }
            return BeamMaxRange;
        }

        void ApplyDamageAlongBeam(Vector2 origin, Vector2 dir, float maxLen, EyeLaserBeam beam)
        {
            float timeScale = Global.main.SlowMotion ? Global.main.SlowmotionTimescale : 1f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxLen);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;

                int layer = hit.collider.gameObject.layer;

                if (layer == 11)
                {
                    var shell = hit.collider.GetComponent<KiShieldSurface>();
                    if (shell && shell.Owner)
                    {
                        if (shell.Owner.transform.root == transform.root) continue;
                        float held;
                        if (!shell.Owner.ReceivePressure(GetInstanceID() ^ beam.GetHashCode(), (double)realDamage * 30, hit.point, .05f, out held)) continue;
                    }
                    TrySpawnBeamHitFx(beam, hit.point, 1.2f);
                    break;
                }

                if (layer != 9 && layer != 13) continue;

                Rigidbody2D rb = hit.collider.attachedRigidbody;
                if (rb == null) continue;

                if (hit.collider.GetComponent<BlastComponent>() != null)
                    hit.collider.GetComponent<BlastComponent>().HasHit = true;

                var limb = hit.collider.GetComponent<LimbBehaviour>();
                if (limb != null && !hit.collider.isTrigger)
                {
                    if (limb.Person == person) continue;
                    if (KiShield.Protects(limb.Person, gameObject, origin,
                        (double)realDamage * 30, GetInstanceID() ^ beam.GetHashCode())) break;
                    EnergyAnnihilation.Expose(person, limb,
                        KiCombatMath.Cap(DbpuFx.PowerOf(person)), hit.point,
                        EyeLaserDamageTickInterval, BeamCoreColor);

                    if (KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                    {
                        float beamKnockCorpse = DBPU.EffectiveBeamKnockback * GetKnockbackOutput();
                        if (beamKnockCorpse > 0f)
                            rb.AddForce(dir * ((4.5f * timeScale) * rb.mass * beamKnockCorpse), ForceMode2D.Impulse);
                        break;
                    }

                    float forcePercent = (realDamage * EyeLaserForceScale) / MCP_Utils.AverageMaxHealth(limb.Person);

                    if (forcePercent > 0.02f && DBPU.Beam_ToggleHeat)
                        limb.PhysicalBehaviour.BurnProgress += 0.0025f * timeScale;

                    if (forcePercent > 0.005f)
                    {
                        limb.Damage(realDamage * timeScale * EyeLaserTickDamageScale);

                        float beamKnock = DBPU.EffectiveBeamKnockback * GetKnockbackOutput();
                        if (beamKnock > 0f)
                        {
                            float knockStrength = (4.5f * timeScale) * rb.mass * Mathf.Clamp(forcePercent, 0.75f, 3f) * beamKnock;
                            rb.AddForce(dir * knockStrength, ForceMode2D.Impulse);
                        }

                        if (forcePercent > 0.05f)
                        {
                            float visualDamage = (18f * Mathf.Clamp(forcePercent, 0f, 1f)) / limb.SkinMaterialHandler.intensityMultiplier;
                            if (limb.name.Contains("Body"))
                                limb.SkinMaterialHandler.AddDamagePoint(DamageType.Bullet, hit.point, visualDamage * 5f);
                            limb.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, hit.point, visualDamage);
                        }

                        TrySpawnBeamHitFx(beam, hit.point, Mathf.Clamp(forcePercent * 2f, 0.5f, 1.5f));
                    }
                    break;
                }

                if (hit.collider.GetComponent<PhysicalBehaviour>() != null)
                {
                    if (DBPU.Beam_ToggleDelete)
                    {
                        Destroy(hit.collider.gameObject);
                        break;
                    }

                    float beamKnock = DBPU.EffectiveBeamKnockback * GetKnockbackOutput();
                    if (beamKnock > 0f)
                        rb.AddForce(dir * ((3.5f * timeScale) * rb.mass * beamKnock), ForceMode2D.Impulse);

                    if (DBPU.Beam_ToggleHeat)
                        hit.collider.GetComponent<PhysicalBehaviour>().Temperature += 1.2f * timeScale;

                    if (DBPU.Beam_ToggleDestroy)
                    {
                        if (hit.collider.GetComponent<DestroyableBehaviour>() != null)
                            hit.collider.GetComponent<DestroyableBehaviour>().Break();
                        if (hit.collider.GetComponent<DamagableMachineryBehaviour>() != null
                            && !hit.collider.GetComponent<DamagableMachineryBehaviour>().Destroyed)
                            hit.collider.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                    }

                    TrySpawnBeamHitFx(beam, hit.point, 0.9f);
                    break;
                }
            }
        }

        void TrySpawnBeamHitFx(EyeLaserBeam beam, Vector2 point, float scale)
        {
            if (beam.hitFxTimer < HitFxCooldown) return;
            if (beam.hasLastHit && Vector2.Distance(beam.lastHitPoint, point) < HitFxMinMove) return;

            beam.hitFxTimer = 0f;
            beam.lastHitPoint = point;
            beam.hasLastHit = true;
            SpawnBeamHitFx(point, scale);
        }

        void SpawnBeamHitFx(Vector2 point, float scale)
        {
            if (DBPU.KiBlast_Hit == null) return;

            var fx = Instantiate(DBPU.KiBlast_Hit, point, Quaternion.identity);
            fx.transform.localScale = new Vector3(scale, scale, scale);
            fx.AddComponent<DestroyOverTime>().Timer = 1f;

            foreach (Transform child in fx.transform)
            {
                var sp = child.GetComponent<Renderer>();
                if (sp != null)
                {
                    sp.sortingLayerName = "Bubbles";
                    sp.sortingOrder += 10;
                }

                var ps = child.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = new Color(0.85f, 0.45f, 1f, 0.6f);
                }
            }

            if (Time.time - lastHitAudioTime < 0.35f || DBPU.Attack_DeathBeam_Hit == null) return;

            lastHitAudioTime = Time.time;
            var audioSource = fx.AddComponent<AudioSource>();
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1f;
            audioSource.volume = DBPU.PowersVolume * 0.65f;
            audioSource.maxDistance = 1000f;
            audioSource.PlayOneShot(DBPU.Attack_DeathBeam_Hit);
        }

        void ClearBeams()
        {
            for (int i = beams.Count - 1; i >= 0; i--)
                RemoveBeam(i);
        }

        void OnDestroy()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
            ClearBeams();
        }
    }
}
