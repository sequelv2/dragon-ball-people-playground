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
    public class CellMaxDisasterRayBehaviour : MonoBehaviour
    {
        class DisasterBeam
        {
            public LineRenderer core;
            public LineRenderer glow;
            public CellMaxSpotGlow originSpot;
            public Vector2 localOrigin;
            public float baseAngle;
            public float sweepPhase;
            public float sweepSpeed;
            public float sweepRange;
            public float age;
            public float damageTimer;
            public float hitFxTimer;
            public Vector2 lastHitPoint;
            public bool hasLastHit;
            public bool fadingOut;
            public float fadeAlpha = 1f;
            public bool finished;
            public BeamPhysics.RayState ray;
        }

        static readonly Color CoreColor = new Color(0.85f, 0.45f, 1f, 0.95f);
        static readonly Color GlowColor = new Color(0.72f, 0.28f, 1f, 0.55f);

        const float GlowRampDuration = 1.1f;
        const float BeamMaxRange = 250f;
        const int MaxConcurrentBeams = 22;
        const float BeamFadeOutDuration = 0.4f;
        const float DamageTickInterval = 0.05f;
        const float HitFxCooldown = 0.22f;
        const float HitFxMinMove = 0.5f;
        const float SweepSpeedMin = 1.2f;
        const float SweepSpeedMax = 2.4f;
        const float SweepRangeMin = 30f;
        const float SweepRangeMax = 58f;
        const float DefaultFireDuration = 7f;
        const float DisasterRayDamageScale = 3.5f;
        const float DisasterRayKnockbackScale = 5f;
        const float DisasterRayForceScale = 140f;

        public bool Active;
        public bool Firing;

        PersonBehaviour person;
        CellMaxScream scream;
        CellMaxSpotGlow[] allSpotGlows;
        CellMaxSpotGlow[] fireSpotGlows;
        CellMaxSpotGlow headSpotGlow;
        KiSystem kiSys;
        MultipliersAndStuffs multipliers;
        BeamBehaviour beamRef;
        Coroutine fireRoutine;
        Coroutine fadeRoutine;
        readonly List<DisasterBeam> beams = new List<DisasterBeam>();
        float damage;
        float realDamage;
        Material beamMaterial;
        bool lasersActive;
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
                StopFiring();
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
            var head = PowerSetup.ResolveHeadLimb(person);
            scream = head != null ? head.GetComponent<CellMaxScream>() : null;

            RefreshSpotGlowsInternal();
            if (head != null)
                headSpotGlow = head.GetComponent<CellMaxSpotGlow>();

            if (multipliers == null) multipliers = gameObject.AddComponent<MultipliersAndStuffs>();
            if (kiSys == null) kiSys = gameObject.AddComponent<KiSystem>();
            if (beamRef == null) beamRef = gameObject.AddComponent<BeamBehaviour>();
            if (person != null && multipliers != null)
                damage = CombatPowerController.AttackHealth(person) / multipliers.FinalMultiplier / 1500f;

            StartCoroutine(BindHeadTrigger());
        }

        public void RefreshSpotGlows()
        {
            RefreshSpotGlowsInternal();
        }

        void RefreshSpotGlowsInternal()
        {
            var glowList = new List<CellMaxSpotGlow>();
            var fireList = new List<CellMaxSpotGlow>();
            if (person != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    var spot = person.Limbs[i].GetComponent<CellMaxSpotGlow>();
                    if (spot == null) continue;
                    if (spot.HasSpots) glowList.Add(spot);
                    if (spot.HasFireSpots) fireList.Add(spot);
                }
            }
            allSpotGlows = glowList.ToArray();
            fireSpotGlows = fireList.ToArray();
        }

        void RefreshDynamicSpots()
        {
            if (headSpotGlow != null)
                headSpotGlow.RefreshFromCurrentSprite();
            RefreshSpotGlowsInternal();
        }

        void SetAllSpotGlowLevel(float level, bool pulse)
        {
            if (allSpotGlows == null) return;
            for (int i = 0; i < allSpotGlows.Length; i++)
            {
                if (allSpotGlows[i] != null)
                    allSpotGlows[i].SetGlowLevel(level, pulse);
            }
        }

        void FadeAllSpotGlow()
        {
            if (allSpotGlows == null) return;
            for (int i = 0; i < allSpotGlows.Length; i++)
            {
                if (allSpotGlows[i] != null)
                    allSpotGlows[i].FadeOut();
            }
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
            RefreshSpotGlowsInternal();
            if (fireSpotGlows == null || fireSpotGlows.Length == 0)
            {
                PowerSetup.EnsureDisasterRayRig(person);
                RefreshSpotGlowsInternal();
            }
            if (fireSpotGlows == null || fireSpotGlows.Length == 0) return;
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
            lasersActive = false;
            if (scream != null) scream.FadeOutWithBeam();
            FadeAllSpotGlow();
            UnfreezePerson();
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

        void FreezePerson()
        {
            if (person == null) return;
            int coreCount = Mathf.Min(14, person.Limbs.Length);
            for (int i = 0; i < coreCount; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null || limb.gameObject == null) continue;
                if (limb.gameObject.GetComponent<FreezeBehaviour>() == null)
                    limb.gameObject.AddComponent<FreezeBehaviour>();
            }
        }

        void UnfreezePerson()
        {
            if (person == null) return;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null || limb.gameObject == null) continue;
                var freeze = limb.gameObject.GetComponent<FreezeBehaviour>();
                if (freeze != null) Destroy(freeze);
            }
        }

        Vector2 GetBodyCenter()
        {
            if (person == null) return transform.position;
            if (person.Limbs.Length > 2 && person.Limbs[2] != null)
                return person.Limbs[2].transform.position;
            return person.transform.position;
        }

        Vector2 GetOutwardDirection(Vector2 originWorld)
        {
            Vector2 center = GetBodyCenter();
            Vector2 away = originWorld - center;
            if (away.sqrMagnitude < 0.0004f)
                away = UnityEngine.Random.insideUnitCircle;
            away.Normalize();

            float baseAngle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
            float spread = UnityEngine.Random.Range(-55f, 55f);
            float finalAngle = (baseAngle + spread) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
        }

        float GetRealDamage()
        {
            float damageOutput = beamRef != null ? beamRef.DamageOutput : 1f;
            return damage * multipliers.FinalMultiplier * multipliers.KiOutput * damageOutput * DBPU.DamageMultiplier * DisasterRayDamageScale;
        }

        float GetKnockbackOutput()
        {
            float kb = beamRef != null ? beamRef.KnockbackOutput : 1f;
            return kb * DisasterRayKnockbackScale;
        }

        IEnumerator FireRoutine()
        {
            Firing = true;
            lasersActive = false;
            SetAllSpotGlowLevel(0f, false);
            FreezePerson();

            if (scream != null)
            {
                scream.ForceScreaming = true;
                scream.PlayForcedScream();
            }

            float elapsed = 0f;
            float kiTick = 0f;

            while (Firing && Active && person != null && person.IsAlive())
            {
                elapsed += Time.deltaTime;
                if (elapsed >= DefaultFireDuration)
                    break;

                realDamage = GetRealDamage();
                RefreshDynamicSpots();

                float glowLevel = Mathf.Clamp01(elapsed / GlowRampDuration);
                SetAllSpotGlowLevel(glowLevel, false);

                if (!lasersActive && elapsed >= GlowRampDuration)
                {
                    lasersActive = true;
                    SpawnPersistentBeams();
                }

                if (lasersActive)
                {
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
                }

                yield return null;
            }

            StopFiring();
        }

        void SpawnPersistentBeams()
        {
            ClearBeams();
            if (fireSpotGlows == null || fireSpotGlows.Length == 0) return;

            var eligible = new List<CellMaxSpotGlow>(fireSpotGlows.Length);
            for (int i = 0; i < fireSpotGlows.Length; i++)
            {
                var spot = fireSpotGlows[i];
                if (spot != null && spot.HasFireSpots)
                    eligible.Add(spot);
            }

            if (eligible.Count == 0) return;

            eligible.Sort((a, b) =>
            {
                int legCompare = a.IsLeg.CompareTo(b.IsLeg);
                return legCompare != 0 ? legCompare : 0;
            });

            int spawnCount = Mathf.Min(MaxConcurrentBeams, eligible.Count);
            for (int i = 0; i < spawnCount; i++)
                TrySpawnBeamFromSpot(eligible[i]);
        }

        bool TrySpawnBeamFromSpot(CellMaxSpotGlow spot)
        {
            if (spot == null || !spot.HasFireSpots) return false;

            Vector2 localOrigin = spot.GetRandomFireEmissionLocal();
            if (localOrigin == Vector2.zero) return false;

            Vector2 originWorld = spot.transform.TransformPoint(localOrigin);
            Vector2 dir = GetOutwardDirection(originWorld);
            float baseAngle = Mathf.Atan2(dir.y, dir.x);

            var beam = new DisasterBeam
            {
                originSpot = spot,
                localOrigin = localOrigin,
                baseAngle = baseAngle,
                sweepPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                sweepSpeed = UnityEngine.Random.Range(SweepSpeedMin, SweepSpeedMax),
                sweepRange = UnityEngine.Random.Range(SweepRangeMin, SweepRangeMax) * Mathf.Deg2Rad,
                core = CreateBeamLine("DisasterRayCore", 0.22f, CoreColor, 146),
                glow = CreateBeamLine("DisasterRayGlow", 0.55f, GlowColor, 145)
            };

            beam.core.transform.SetParent(transform, false);
            beam.glow.transform.SetParent(transform, false);
            beams.Add(beam);
            return true;
        }

        Vector2 GetBeamDirection(DisasterBeam beam)
        {
            float angleOffset = Mathf.Sin(beam.age * beam.sweepSpeed + beam.sweepPhase) * beam.sweepRange;
            float angle = beam.baseAngle + angleOffset;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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
            lr.startWidth = width * DBPU.Beam_SizeMultiplier;
            lr.endWidth = width * 0.85f * DBPU.Beam_SizeMultiplier;
            lr.positionCount = 2;
            return lr;
        }

        void UpdateBeam(DisasterBeam beam)
        {
            if (beam == null || beam.core == null || beam.glow == null) return;

            beam.age += Time.deltaTime;

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

            Vector2 origin = beam.originSpot != null
                ? (Vector2)beam.originSpot.transform.TransformPoint(beam.localOrigin)
                : (Vector2)transform.position;

            Vector2 dir = GetBeamDirection(beam);
            Vector2 castOrigin = origin + dir * 0.08f;
            float maxDist = GetBeamLength(castOrigin, dir);
            Vector2 end = castOrigin + dir * Mathf.Max(0.05f, maxDist - 0.08f);

            Vector2 stop = end, outDir = dir;
            var met = BeamPhysics.MeetRay(transform.root.gameObject, rayLimb, dir, 0.5f, ref beam.ray, out stop, out outDir);
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
                beam.glow.SetPosition(0, origin);
                beam.glow.SetPosition(1, end);
            }

            ApplyBeamAlpha(beam, 1f);

            beam.hitFxTimer += Time.deltaTime;
            beam.damageTimer += Time.deltaTime;
            if (beam.damageTimer >= DamageTickInterval)
            {
                beam.damageTimer = 0f;
                if (met == BeamPhysics.RayResult.Deflected) ApplyDamageAlongBeam(stop + outDir * 1.4f, outDir, outLength, beam);
                else if (met == BeamPhysics.RayResult.None) ApplyDamageAlongBeam(castOrigin, dir, maxDist, beam);
            }
        }

        void ApplyBeamAlpha(DisasterBeam beam, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (beam.core != null && beam.core.material != null)
            {
                Color c = CoreColor;
                c.a *= alpha;
                beam.core.material.color = c;
            }
            if (beam.glow != null && beam.glow.material != null)
            {
                Color c = GlowColor;
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

        void ApplyDamageAlongBeam(Vector2 origin, Vector2 dir, float maxLen, DisasterBeam beam)
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
                        DamageTickInterval, CoreColor);

                    if (KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                    {
                        float beamKnockCorpse = DBPU.EffectiveBeamKnockback * GetKnockbackOutput();
                        if (beamKnockCorpse > 0f)
                            rb.AddForce(dir * ((4.5f * timeScale) * rb.mass * beamKnockCorpse), ForceMode2D.Impulse);
                        break;
                    }

                    float forcePercent = (realDamage * DisasterRayForceScale) / MCP_Utils.AverageMaxHealth(limb.Person);

                    if (forcePercent > 0.02f && DBPU.Beam_ToggleHeat)
                        limb.PhysicalBehaviour.BurnProgress += 0.0025f * timeScale;

                    if (forcePercent > 0.005f)
                    {
                        limb.Damage(realDamage * timeScale * 1.25f);

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

        void TrySpawnBeamHitFx(DisasterBeam beam, Vector2 point, float scale)
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
