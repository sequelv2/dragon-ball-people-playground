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
    public class BeamComponent : MonoBehaviour
    {
        public BeamBehaviour beamBehaviour;

        static readonly List<(BeamBehaviour a, BeamBehaviour b)> ActiveBeamClashes = new List<(BeamBehaviour, BeamBehaviour)>();

        static void RegisterBeamClash(BeamBehaviour a, BeamBehaviour b)
        {
            if (a == null || b == null) return;
            for (int i = 0; i < ActiveBeamClashes.Count; i++)
            {
                var pair = ActiveBeamClashes[i];
                if ((pair.a == a && pair.b == b) || (pair.a == b && pair.b == a))
                    return;
            }
            ActiveBeamClashes.Add((a, b));
        }

        static void UnregisterBeamClash(BeamBehaviour a, BeamBehaviour b)
        {
            for (int i = ActiveBeamClashes.Count - 1; i >= 0; i--)
            {
                var pair = ActiveBeamClashes[i];
                if ((pair.a == a && pair.b == b) || (pair.a == b && pair.b == a))
                    ActiveBeamClashes.RemoveAt(i);
            }
        }

        static void PruneActiveBeamClashes()
        {
            for (int i = ActiveBeamClashes.Count - 1; i >= 0; i--)
            {
                var pair = ActiveBeamClashes[i];
                if (pair.a == null || pair.b == null || !pair.a.Firing || !pair.b.Firing || !pair.a.Clashing || !pair.b.Clashing)
                    ActiveBeamClashes.RemoveAt(i);
            }
        }

        public float DistanceOfWallContact = 0f;
        public float MinDistanceToLose = 3f;
        public float SafeMargin = 1f;
        public bool HitAWall = false;
        public Vector3 RayDirection;

        bool IsSpecialBeamCannon => beamBehaviour != null && beamBehaviour.EffectID == "Special Beam Cannon";

        float GetPersonCombatPower(PersonBehaviour person)
        {
            if (person == null) return 0f;
            var mult = person.GetComponent<MultipliersAndStuffs>();
            float formMult = mult != null ? mult.FinalMultiplier : 1f;
            float kiOut = mult != null ? mult.KiOutput : 1f;
            return MCP_Utils.AverageMaxHealth(person) / CombatPowerController.HealthScale(person) / CombatPowerController.KaiokenHealthScale(person) * formMult * kiOut;
        }

        bool IsPersonStrongerThanAttacker(PersonBehaviour target)
        {
            if (target == null || !IsSpecialBeamCannon) return false;
            var attacker = beamBehaviour.GetComponent<PersonBehaviour>();
            if (attacker == null || target == attacker) return false;
            return GetPersonCombatPower(target) > GetPersonCombatPower(attacker) * 1.08f;
        }

        // A beam's path ran into solid ground.
        public void BodyHitWall(Vector2 point)
        {
            if (IsSpecialBeamCannon && !beamBehaviour.SbcBlockedThisFire)
                TriggerSpecialBeamBlock(point, beamBehaviour.Distance);
        }

        float GetDistAlongBeam(Vector3 worldPoint)
        {
            if (beamBehaviour.Body != null && beamBehaviour.Body.Live)
            {
                float arc; Vector2 tangent;
                beamBehaviour.Body.ClosestPoint(worldPoint, out arc, out tangent);
                return beamBehaviour.Body.DistanceFor(arc);
            }
            if (beamBehaviour.MiddleBeam == null || beamBehaviour.MiddleBeam.positionCount < 2)
                return beamBehaviour.Distance;
            Vector3 a = beamBehaviour.MiddleBeam.GetPosition(0);
            Vector3 b = beamBehaviour.MiddleBeam.GetPosition(1);
            Vector3 dir = (b - a).normalized;
            return Mathf.Clamp(Vector3.Dot(worldPoint - a, dir), 0f, beamBehaviour.Distance);
        }

        Vector3 GetBeamTipWorld(BeamBehaviour beam)
        {
            if (beam == null) return Vector3.zero;
            if (beam.EndBeam != null)
                return beam.EndBeam.transform.position;
            return beam.StartBeamPosition + beam.BeamDirection.normalized * beam.Distance;
        }

        Vector3 GetBeamClashPoint(BeamBehaviour a, BeamBehaviour b)
        {
            return (GetBeamTipWorld(a) + GetBeamTipWorld(b)) * 0.5f;
        }

        void SyncBeamTipTransform(BeamBehaviour beam)
        {
            if (beam == null) return;
            beam.SyncEndBeamTransform();
        }

        LineRenderer CreateExpandingRing(Transform parent, Material mat, float radius, float zRot)
        {
            GameObject go = new GameObject("ClashRing");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.material = new Material(mat != null ? mat : ModAPI.FindMaterial("VeryBright"));
            lr.sortingLayerName = "Bubbles";
            lr.sortingOrder = 525;
            lr.textureMode = LineTextureMode.Tile;
            if (DBPU.StraightBeam_Texture != null)
                lr.material.mainTexture = DBPU.StraightBeam_Texture;

            int segments = 40;
            lr.positionCount = segments;
            SetRingPositions(lr, radius, segments);

            Color ringColor = new Color(1f, 1f, 1f, 0.9f);
            lr.startColor = ringColor;
            lr.endColor = ringColor;
            float thin = 0.1f * DBPU.Beam_SizeMultiplier;
            lr.startWidth = thin;
            lr.endWidth = thin;
            lr.numCapVertices = 2;
            return lr;
        }

        void SetRingPositions(LineRenderer lr, float radius, int segments)
        {
            if (lr == null) return;
            if (segments <= 0) segments = lr.positionCount;
            for (int i = 0; i < segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, -0.01f));
            }
        }

        SpriteRenderer CreateClashOrb(Transform parent, float scale)
        {
            GameObject orb = new GameObject("ClashOrb");
            orb.transform.SetParent(parent, false);
            orb.transform.localPosition = Vector3.zero;

            SpriteRenderer sr = orb.AddComponent<SpriteRenderer>();
            sr.sprite = DBPU.KiShield_Sprite;
            sr.material = ModAPI.FindMaterial("VeryBright");
            sr.color = new Color(0.88f, 0.92f, 0.98f, 0.09f);
            sr.sortingLayerName = "Bubbles";
            sr.sortingOrder = 515;
            float orbSize = scale * 0.065f;
            orb.transform.localScale = new Vector3(orbSize, orbSize, 1f);
            return sr;
        }

        void BoostParticleVisibility(GameObject root)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                r.sortingLayerName = "Bubbles";
                r.sortingOrder += 520;
            }
        }

        void SpawnClashImpactBurst(Vector3 position, float scale)
        {
            if (DBPU.KiBlast_Hit == null) return;

            GameObject core = Instantiate(DBPU.KiBlast_Hit, position, Quaternion.identity);
            core.transform.localScale = Vector3.one * scale * 1.9f;
            BoostParticleVisibility(core);
            foreach (Transform child in core.transform)
            {
                ParticleSystem ps = child.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play(true);
            }
            core.AddComponent<DestroyOverTime>().Timer = 0.5f;
        }

        float GetClashOrbVisualRadius(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return 0.5f;
            return sr.sprite.bounds.extents.x * sr.transform.localScale.x;
        }

        GameObject SpawnClashShockwave(Vector3 position, BeamBehaviour a, BeamBehaviour b)
        {
            GameObject root = new GameObject("BeamClashShockwave");
            root.transform.position = position;

            float scale = Mathf.Clamp(2f + (a.Width + b.Width) * 0.35f, 2.5f, 10f) * DBPU.Beam_SizeMultiplier;
            Material mat = ModAPI.FindMaterial("VeryBright");

            BeamClashShockwaveMarker marker = root.AddComponent<BeamClashShockwaveMarker>();
            marker.BaseScale = scale;
            marker.CoreOrb = CreateClashOrb(root.transform, scale);

            float orbVisualRadius = GetClashOrbVisualRadius(marker.CoreOrb);
            float startRadius = orbVisualRadius * 1.5f;
            float maxRingRadius = orbVisualRadius * 6.5f;

            LineRenderer ringA = CreateExpandingRing(root.transform, mat, startRadius, 0f);
            LineRenderer ringB = CreateExpandingRing(root.transform, mat, startRadius * 0.92f, 0f);

            marker.OrbVisualRadius = orbVisualRadius;
            marker.MinRingRadius = startRadius;
            marker.MaxRingRadius = maxRingRadius;
            marker.RingExpandSpeed = 13f * DBPU.Beam_SizeMultiplier;
            marker.Rings = new[] { ringA, ringB };
            marker.RingRadii = new[] { startRadius, startRadius * 0.92f };

            SpawnClashImpactBurst(position, scale);

            if (CameraShakeBehaviour.main != null)
                CameraShakeBehaviour.main.Shake(0.55f, position);

            AudioSource shockAudio = root.AddComponent<AudioSource>();
            Global global = FindObjectOfType<Global>();
            if (global != null)
            {
                global.AddAudioSource(shockAudio, false);
                shockAudio.outputAudioMixerGroup = global.SoundEffects;
            }
            shockAudio.spatialBlend = 1f;
            shockAudio.volume = DBPU.PowersVolume * 0.5f;
            shockAudio.maxDistance = 1200f;
            shockAudio.PlayOneShot(DBPU.Attack_KiBlast_Impact);

            return root;
        }

        IEnumerator PlayClashShockwaveOneShot(Vector3 position, BeamBehaviour a, BeamBehaviour b)
        {
            GameObject root = SpawnClashShockwave(position, a, b);
            ApplyClashShockwaveDestruction(position, a, b, GetClashSideRoll(a) + GetClashSideRoll(b));

            float duration = 0.82f;
            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                UpdateClashShockwaveOneShot(root, Time.deltaTime, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (root != null)
                Destroy(root);
        }

        void UpdateClashShockwaveOneShot(GameObject root, float delta, float normalizedTime)
        {
            if (root == null) return;

            BeamClashShockwaveMarker marker = root.GetComponent<BeamClashShockwaveMarker>();
            if (marker == null) return;

            if (marker.CoreOrb != null)
            {
                float swell = Mathf.Clamp01(normalizedTime / 0.18f);
                float fade = 1f - Mathf.Clamp01((normalizedTime - 0.22f) / 0.48f);
                float orbSize = marker.BaseScale * 0.065f;
                float scalePulse = Mathf.Lerp(0.78f, 1.2f, swell);
                marker.CoreOrb.transform.localScale = new Vector3(orbSize * scalePulse, orbSize * scalePulse, 1f);
                float alphaPulse = Mathf.Lerp(0.14f, 0.1f, swell) * fade;
                marker.CoreOrb.color = new Color(0.88f, 0.92f, 0.98f, alphaPulse);
                marker.OrbVisualRadius = GetClashOrbVisualRadius(marker.CoreOrb);
            }

            if (marker.Rings == null || marker.RingRadii == null) return;

            float thin = 0.11f * DBPU.Beam_SizeMultiplier;

            for (int i = 0; i < marker.Rings.Length; i++)
            {
                LineRenderer lr = marker.Rings[i];
                if (lr == null) continue;

                marker.RingRadii[i] += marker.RingExpandSpeed * delta;
                marker.RingRadii[i] = Mathf.Min(marker.RingRadii[i], marker.MaxRingRadius);

                float travel = Mathf.InverseLerp(marker.MinRingRadius, marker.MaxRingRadius, marker.RingRadii[i]);
                float fade = travel < 0.2f ? 1f : 1f - ((travel - 0.2f) / 0.8f);
                Color c = new Color(0.9f, 0.94f, 1f, fade * 0.75f);
                lr.startColor = c;
                lr.endColor = c;
                lr.startWidth = thin;
                lr.endWidth = thin;
                SetRingPositions(lr, marker.RingRadii[i], lr.positionCount);
            }
        }

        void ApplyClashShockwaveDestruction(Vector3 origin, BeamBehaviour a, BeamBehaviour b, float intensity)
        {
            if (!DBPU.Beam_ToggleDestroy) return;

            float radius = (3.5f + (a.Width + b.Width) * 0.58f) * DBPU.Beam_SizeMultiplier * Mathf.Clamp(intensity / 250000f, 0.85f, 2.1f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, LayerMask.GetMask("Objects", "CollidingDebris"));
            Transform rootA = a.transform.root;
            Transform rootB = b.transform.root;

            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                if (hit.transform.root == rootA || hit.transform.root == rootB) continue;

                Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                Vector3 direction = (hit.transform.position - origin).normalized;

                if (hit.GetComponent<DestroyableBehaviour>() != null)
                    hit.GetComponent<DestroyableBehaviour>().Break();
                else if (hit.GetComponent<DamagableMachineryBehaviour>() != null && hit.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                    hit.GetComponent<DamagableMachineryBehaviour>().ForceBreak();

                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    float dist = Mathf.Max(Vector2.Distance(origin, hit.transform.position), 0.5f);
                    rb.AddForce(direction * (Mathf.Clamp(38f / dist, 6f, 55f) * rb.mass), ForceMode2D.Impulse);
                }
            }
        }

        void TriggerSpecialBeamBlock(Vector3 worldPos, float clampDistance)
        {
            if (!IsSpecialBeamCannon || beamBehaviour.SbcBlockedThisFire) return;

            beamBehaviour.SbcBlockedThisFire = true;
            beamBehaviour.realSpeed = 0f;
            beamBehaviour.Distance = Mathf.Min(beamBehaviour.Distance, Mathf.Max(clampDistance, 0.5f));
            beamBehaviour.Firing = false;
            beamBehaviour.Clashing = false;

            StartCoroutine(SpawnSpecialBeamBlockExplosion(worldPos));
        }

        public IEnumerator SpawnSpecialBeamBlockExplosion(Vector3 position)
        {
            float scale = 1.35f * DBPU.Beam_SizeMultiplier;
            GameObject explosion = Instantiate(DBPU.KiBlast_Hit, position, Quaternion.identity);
            explosion.transform.localScale = new Vector3(scale, scale, scale);
            explosion.AddComponent<DestroyOverTime>().Timer = 2.5f;

            foreach (Transform child in explosion.transform)
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
                    ps.playbackSpeed *= 0.65f;
                    ps.startColor = new Color(1f, 0.55f, 0.85f, 0.12f);
                }
            }

            AudioSource boomAudio = explosion.AddComponent<AudioSource>();
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(boomAudio, false);
            boomAudio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            boomAudio.outputAudioMixerGroup = global.SoundEffects;
            boomAudio.spatialBlend = 1;
            boomAudio.volume = DBPU.PowersVolume;
            boomAudio.maxDistance = 1000;
            boomAudio.PlayOneShot(DBPU.Attack_KiBlast_Impact);

            yield return StartCoroutine(SpecialBeamExplosionDamage(position));

            yield return null;
        }

        public IEnumerator SpecialBeamExplosionDamage(Vector3 origin)
        {
            float blastDamage = GetCombinedBeamDamage(beamBehaviour) * 14f;
            float radius = 2.4f * DBPU.Beam_SizeMultiplier;

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                origin,
                radius,
                LayerMask.GetMask("Objects", "CollidingDebris"));

            foreach (Collider2D hit in hits)
            {
                if (hit.transform.root == beamBehaviour.transform.root) continue;
                if (hit.GetComponent<Rigidbody2D>() == null) continue;

                float dist = Mathf.Clamp(Vector2.Distance(origin, hit.transform.position), 0.75f, radius);
                float scaledDamage = blastDamage / Mathf.Clamp(dist * dist, 1f, 30f);
                Vector3 direction = (hit.transform.position - (Vector3)origin).normalized;
                Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();

                if (hit.GetComponent<LimbBehaviour>() != null && hit.isTrigger == false)
                {
                    var limb = hit.GetComponent<LimbBehaviour>();
                    if (KiShield.Protects(limb.Person, beamBehaviour.gameObject, origin,
                        scaledDamage / .12f, beamBehaviour.GetInstanceID())) continue;
                    EnergyAnnihilation.Expose(beamBehaviour.GetComponent<PersonBehaviour>(), limb,
                        KiCombatMath.Cap(BeamPhysics.BeamPower(beamBehaviour) / Mathf.Clamp(dist * dist, 1f, 30f)),
                        hit.ClosestPoint(origin), .12f, BeamStyle.Of(beamBehaviour).Mid);
                    limb.Damage(scaledDamage);
                    if (rb.velocity.magnitude < 10f)
                        rb.AddForce(direction * (Mathf.Clamp(80f / dist, 4f, 40f) * rb.mass), ForceMode2D.Impulse);
                }
                else if (hit.GetComponent<PhysicalBehaviour>() != null)
                {
                    if (rb.velocity.magnitude < 12f)
                        rb.AddForce(direction * (Mathf.Clamp(60f / dist, 5f, 35f) * rb.mass), ForceMode2D.Impulse);

                    if (hit.GetComponent<DestroyableBehaviour>() != null)
                        hit.GetComponent<DestroyableBehaviour>().Break();

                    if (hit.GetComponent<DamagableMachineryBehaviour>() != null && hit.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                        hit.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                }
            }

            yield return null;
        }

        public void Start()
        {
        }

        public void Update()
        {
            if (Global.main.Paused || beamBehaviour == null || !beamBehaviour.StillActive || beamBehaviour.MiddleBeam == null)
                return;

            if (beamBehaviour.MergeHost != null)
                return;

            if (beamBehaviour.Body != null && beamBehaviour.Body.Live)
            {
                beamBehaviour.Body.CastHits(this);
                TryAutoJoinNearbyClash();
                return;
            }

            Physics2D.queriesHitTriggers = true;

            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                origin: beamBehaviour.StartBeamPosition,
                size: new Vector2(0.1f, beamBehaviour.MiddleBeam.startWidth * 0.96f),
                angle: beamBehaviour.BeamDirection.z,
                direction: RayDirection,
                distance: beamBehaviour.Distance - beamBehaviour.MiddleMinusDistance,
                layerMask: LayerMask.GetMask("Objects", "CollidingDebris", "Bounds")
                );

            Physics2D.queriesHitTriggers = false;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.gameObject.transform.root.transform != beamBehaviour.transform)
                {
                    StartCoroutine(OnHit(hit, (beamBehaviour.MiddleBeam.GetPosition(1) - beamBehaviour.MiddleBeam.GetPosition(0)).normalized));
                }
            }

            if (HitAWall == false)
            {
                RaycastHit2D[] rayHits = Physics2D.RaycastAll(
                    origin: beamBehaviour.StartBeamPosition,
                    direction: (beamBehaviour.MiddleBeam.GetPosition(1) - beamBehaviour.MiddleBeam.GetPosition(0)).normalized,
                    distance: beamBehaviour.Distance - beamBehaviour.MiddleMinusDistance
                    );

                foreach (RaycastHit2D hit in rayHits)
                {
                    if (hit.collider.gameObject.transform.root.transform != beamBehaviour.transform && hit.collider.gameObject.layer == 11 && !hit.collider.GetComponent<KiShieldSurface>())
                    {
                        StartCoroutine(HitWall());
                    }
                }
            }

            TryAutoJoinNearbyClash();
        }

        void TryAutoJoinNearbyClash()
        {
            if (!DBPU.BeamAssistEnabled || beamBehaviour == null) return;
            if (!beamBehaviour.Firing || !beamBehaviour.StillActive || beamBehaviour.Clashing || beamBehaviour.MergeHost != null) return;

            PruneActiveBeamClashes();

            for (int i = 0; i < ActiveBeamClashes.Count; i++)
            {
                var pair = ActiveBeamClashes[i];
                BeamBehaviour sideA = pair.a;
                BeamBehaviour sideB = pair.b;
                if (sideA == null || sideB == null || !sideA.Clashing || !sideB.Clashing || !sideA.Firing || !sideB.Firing) continue;

                Vector3 clashPoint = (sideA.EndBeamPosition + sideB.EndBeamPosition) * 0.5f;
                float joinRadius = Mathf.Max(14f, (sideA.Width + sideB.Width + 6f) * DBPU.Beam_SizeMultiplier * 3f);

                if (!IsBeamAimedAtClash(beamBehaviour, clashPoint)) continue;
                if (DistancePointToBeam(beamBehaviour, clashPoint) > joinRadius) continue;

                if (beamBehaviour.transform.root != sideA.transform.root && CheckSameDirection(beamBehaviour, sideA))
                    TryMergeIntoSide(beamBehaviour, sideA);
                else if (beamBehaviour.transform.root != sideB.transform.root && CheckSameDirection(beamBehaviour, sideB))
                    TryMergeIntoSide(beamBehaviour, sideB);
            }
        }

        float DistancePointToBeam(BeamBehaviour b, Vector3 point)
        {
            Vector3 a = b.StartBeamPosition;
            Vector3 dir = b.BeamDirection.normalized;
            float along = Vector3.Dot(point - a, dir);
            if (along < 0f) return float.MaxValue;
            Vector3 closest = a + dir * Mathf.Clamp(along, 0f, b.Distance + 10f);
            return Vector3.Distance(closest, point);
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            BeamComponent otherComp = other.gameObject.GetComponent<BeamComponent>();
            if (otherComp != null)
            {
                if (TryMergeBeams(otherComp.beamBehaviour))
                    return;

                BeamBehaviour otherBeam = otherComp.beamBehaviour;
                if (beamBehaviour.Clashing == false && otherBeam.Clashing == false && !beamBehaviour.HeldByFighter && !otherBeam.HeldByFighter && CheckAngles(otherBeam))
                {
                    beamBehaviour.Clashing = true;
                    otherBeam.Clashing = true;
                    beamBehaviour.ClashOpponent = otherBeam;
                    otherBeam.ClashOpponent = beamBehaviour;
                    StartCoroutine(BeamClash(otherBeam));
                }
            }

            else if (other.gameObject.GetComponent<BallBehaviour.BallComponent>())
            {
                BallBehaviour.BallComponent ballComp = other.gameObject.GetComponent<BallBehaviour.BallComponent>();
                BallBehaviour ball = ballComp.ballBehaviour;
                if (ball == null || beamBehaviour.Clashing || ball.Orb != null) return;

                if (!ball.Clashing)
                {
                    beamBehaviour.Clashing = true;
                    ball.Clashing = true;
                    if (DBPU.BeamAssistEnabled)
                        ball.PushingBeams.Clear();
                    ball.PushingBeams.Add(beamBehaviour);
                    StartCoroutine(BeamBallClash(ball));
                }
                else if (DBPU.BeamAssistEnabled && TryJoinBallPush(ball))
                {
                    beamBehaviour.Clashing = true;
                    beamBehaviour.realSpeed = 0f;
                    ball.PushingBeams.Add(beamBehaviour);
                }
            }
        }

        bool TryMergeBeams(BeamBehaviour otherBeam)
        {
            if (!DBPU.BeamAssistEnabled) return false;
            if (otherBeam == null || beamBehaviour == null || otherBeam == beamBehaviour) return false;
            if (otherBeam.transform.root == beamBehaviour.transform.root) return false;
            if (beamBehaviour.MergeHost != null || otherBeam.MergeHost != null) return false;

            if (otherBeam.Clashing && otherBeam.ClashOpponent != null && !beamBehaviour.Clashing)
            {
                if (TryMergeIntoSide(beamBehaviour, otherBeam)) return true;
                if (TryMergeIntoSide(beamBehaviour, otherBeam.ClashOpponent)) return true;
            }

            if (beamBehaviour.Clashing && beamBehaviour.ClashOpponent != null && !otherBeam.Clashing)
            {
                if (TryMergeIntoSide(otherBeam, beamBehaviour)) return true;
                if (TryMergeIntoSide(otherBeam, beamBehaviour.ClashOpponent)) return true;
            }

            if (!beamBehaviour.Clashing && !otherBeam.Clashing)
                return TryFuseParallelBeams(beamBehaviour, otherBeam);

            return false;
        }

        bool TryFuseParallelBeams(BeamBehaviour a, BeamBehaviour b)
        {
            if (a.GetInstanceID() > b.GetInstanceID())
                return TryFuseParallelBeams(b, a);
            if (!a.Firing || !b.Firing || !a.StillActive || !b.StillActive) return false;
            if (a.Clashing || b.Clashing) return false;
            if (!CheckSameDirection(a, b)) return false;
            if (!CheckParallelProximity(a, b)) return false;
            BeamBehaviour host = a.Distance >= b.Distance ? a : b;
            BeamBehaviour incoming = host == a ? b : a;
            return ApplyBeamMerge(incoming, host, false);
        }

        bool IsBeamAimedAtClash(BeamBehaviour b, Vector3 clashPoint)
        {
            Vector3 toClash = clashPoint - b.StartBeamPosition;
            if (toClash.sqrMagnitude < 0.01f) return true;
            if (Vector3.Angle(b.BeamDirection.normalized, toClash.normalized) > 25f) return false;
            return Vector3.Dot(toClash, b.BeamDirection.normalized) > 0f;
        }

        void ScanForClashAssist(BeamBehaviour sideHost)
        {
            if (!DBPU.BeamAssistEnabled || sideHost == null) return;
            if (!sideHost.Clashing || !sideHost.Firing || sideHost.ClashOpponent == null || !sideHost.ClashOpponent.Firing) return;

            PruneActiveBeamClashes();

            Vector3 clashPoint = (sideHost.EndBeamPosition + sideHost.ClashOpponent.EndBeamPosition) * 0.5f;
            float radius = Mathf.Max(8f, (sideHost.Width + sideHost.ClashOpponent.Width + 4f) * DBPU.Beam_SizeMultiplier * 2.5f);

            Collider2D[] hits = Physics2D.OverlapCircleAll(clashPoint, radius);
            foreach (Collider2D hit in hits)
            {
                BeamComponent comp = hit.GetComponent<BeamComponent>();
                if (comp == null || comp.beamBehaviour == null) continue;

                BeamBehaviour candidate = comp.beamBehaviour;
                if (candidate == sideHost || candidate == sideHost.ClashOpponent) continue;
                if (candidate.MergeHost != null || candidate.Clashing) continue;
                if (!candidate.Firing || !candidate.StillActive) continue;
                if (candidate.transform.root == sideHost.transform.root) continue;
                if (!CheckSameDirection(candidate, sideHost)) continue;
                if (!IsBeamAimedAtClash(candidate, clashPoint)) continue;

                TryMergeIntoSide(candidate, sideHost);
            }
        }

        bool CheckParallelProximity(BeamBehaviour a, BeamBehaviour b)
        {
            Vector3 dir = a.BeamDirection.normalized;
            Vector3 delta = b.StartBeamPosition - a.StartBeamPosition;
            float lateral = Mathf.Abs(dir.x * delta.y - dir.y * delta.x);
            float maxSep = (a.Width + b.Width + a.ExtraWidth + b.ExtraWidth) * DBPU.Beam_SizeMultiplier * 4f + 5f;
            return lateral <= maxSep;
        }

        bool TryMergeIntoSide(BeamBehaviour incoming, BeamBehaviour host)
        {
            if (incoming == null || host == null || host.ClashOpponent == null) return false;
            if (!incoming.Firing || !incoming.StillActive || !host.Firing || !host.ClashOpponent.Firing) return false;
            if (!host.Clashing) return false;
            if (host.MergedBeams.Contains(incoming)) return false;
            if (!CheckSameDirection(incoming, host)) return false;
            return ApplyBeamMerge(incoming, host);
        }

        bool CheckSameDirection(BeamBehaviour a, BeamBehaviour b)
        {
            return Vector3.Angle(a.BeamDirection.normalized, b.BeamDirection.normalized) <= 18f;
        }

        bool ApplyBeamMerge(BeamBehaviour incoming, BeamBehaviour host, bool inClash = true)
        {
            incoming.MergeHost = host;
            if (inClash) incoming.Clashing = true;
            incoming.realSpeed = 0f;
            host.MergedBeams.Add(incoming);

            float preWidth = host.Width;
            host.Width = Mathf.Min(preWidth + incoming.Width * 0.75f, preWidth * 4.5f);
            host.MergeWidthBonus += host.Width - preWidth;
            host.ExtraWidth = Mathf.Max(host.ExtraWidth, incoming.Width * 0.35f);

            incoming.Distance = host.Distance;
            incoming.EndBeamPosition = host.EndBeamPosition;
            if (incoming.EndBeam != null && host.EndBeam != null)
                incoming.EndBeam.transform.position = host.EndBeam.transform.position;

            if (incoming.EndBeam != null)
            {
                Collider2D col = incoming.EndBeam.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }

            BeamComponent hostComp = host.EndBeam != null ? host.EndBeam.GetComponent<BeamComponent>() : null;
            if (hostComp == null) hostComp = this;
            hostComp.StartCoroutine(hostComp.AnimateBeamMerge(incoming, host));

            return true;
        }

        public IEnumerator AnimateBeamMerge(BeamBehaviour incoming, BeamBehaviour host)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            float startIncomingW = incoming.MiddleBeam != null ? incoming.MiddleBeam.startWidth : incoming.Width;
            float startHostW = host.MiddleBeam != null ? host.MiddleBeam.startWidth : host.Width;
            float targetHostW = (host.Width + host.ExtraWidth) * DBPU.Beam_SizeMultiplier;
            Color hostColor = host.MiddleBeam != null ? host.MiddleBeam.material.color : Color.white;
            Color incomingColor = incoming.MiddleBeam != null ? incoming.MiddleBeam.material.color : hostColor;
            Color blendTarget = Color.Lerp(hostColor, incomingColor, 0.4f);

            host.MergeVisualActive = true;

            while (elapsed < duration && incoming.MergeHost == host && host.Firing)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float swell = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);

                if (host.MiddleBeam != null && host.MiddleBeam.material != null)
                {
                    float w = Mathf.Lerp(startHostW, targetHostW * swell, t);
                    host.MiddleBeam.startWidth = w;
                    host.MiddleBeam.endWidth = w;
                    host.MiddleBeam.material.color = Color.Lerp(hostColor, blendTarget, t);
                }

                if (incoming.MiddleBeam != null && incoming.MiddleBeam.material != null)
                {
                    float w = Mathf.Lerp(startIncomingW, 0f, t);
                    incoming.MiddleBeam.startWidth = w;
                    incoming.MiddleBeam.endWidth = w;
                    Color c = incoming.MiddleBeam.material.color;
                    incoming.MiddleBeam.material.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t));
                }

                yield return null;
            }

            host.MergeVisualActive = false;
            FinalizeMergedBeamVisual(incoming, host);
        }

        void FinalizeMergedBeamVisual(BeamBehaviour incoming, BeamBehaviour host)
        {
            bool clashAssist = incoming.Clashing && host != null && host.Clashing;
            if (!clashAssist)
                incoming.Firing = false;

            if (incoming.MiddleBeam != null)
            {
                incoming.MiddleBeam.startWidth = 0f;
                incoming.MiddleBeam.endWidth = 0f;
                if (incoming.MiddleBeam.material != null)
                {
                    Color c = incoming.MiddleBeam.material.color;
                    incoming.MiddleBeam.material.color = new Color(c.r, c.g, c.b, 0f);
                }
            }

            if (host.MiddleBeam != null)
            {
                float w = (host.Width + host.ExtraWidth) * DBPU.Beam_SizeMultiplier;
                host.MiddleBeam.startWidth = w;
                host.MiddleBeam.endWidth = w;
            }

            if (host.Firing && !host.Clashing && host.MergedBeams != null && host.MergedBeams.Count > 0)
                host.MergeVisualActive = false;
        }

        float GetSingleBeamPower(BeamBehaviour b)
        {
            if (b == null || b.KiSys == null) return 0f;
            return b.RealDamage * PowerLostByKi(b);
        }

        float GetCombinedSidePower(BeamBehaviour host)
        {
            if (host == null) return 0f;
            PruneMergedBeams(host);
            float total = GetSingleBeamPower(host);
            for (int i = 0; i < host.MergedBeams.Count; i++)
            {
                BeamBehaviour m = host.MergedBeams[i];
                if (m != null && m.StillActive)
                    total += GetSingleBeamPower(m);
            }
            return total;
        }

        float GetCombinedBeamDamage(BeamBehaviour host)
        {
            if (host == null) return 0f;
            PruneMergedBeams(host);
            float total = host.RealDamage;
            for (int i = 0; i < host.MergedBeams.Count; i++)
            {
                BeamBehaviour m = host.MergedBeams[i];
                if (m != null && m.StillActive)
                    total += m.RealDamage;
            }
            return total;
        }

        float GetClashSideRoll(BeamBehaviour host)
        {
            return Random.Range(1f, 100f) * GetCombinedSidePower(host);
        }

        void PruneMergedBeams(BeamBehaviour host)
        {
            for (int i = host.MergedBeams.Count - 1; i >= 0; i--)
            {
                BeamBehaviour m = host.MergedBeams[i];
                if (m == null)
                {
                    host.MergedBeams.RemoveAt(i);
                    continue;
                }
                if (m.MergeHost == host) continue;
                if (!m.Firing)
                {
                    m.MergeHost = null;
                    m.Clashing = false;
                    host.MergedBeams.RemoveAt(i);
                }
            }
        }

        public void ReleaseMergedBeams(BeamBehaviour host)
        {
            if (host == null || host.MergedBeams == null) return;
            foreach (BeamBehaviour m in host.MergedBeams)
            {
                if (m == null) continue;
                m.MergeHost = null;
                m.Clashing = false;
                m.Firing = false;
                m.Distance = 0f;
                m.realSpeed = 0f;
            }
            host.MergedBeams.Clear();
            host.Width = Mathf.Max(0.1f, host.Width - host.MergeWidthBonus);
            host.MergeWidthBonus = 0f;
            host.MergeVisualActive = false;
        }

        void EndClashState(BeamBehaviour beam, bool preserveMerges)
        {
            if (beam == null) return;
            beam.ClashOpponent = null;
            beam.Clashing = false;
            if (preserveMerges && beam.MergedBeams != null)
            {
                foreach (BeamBehaviour m in beam.MergedBeams)
                {
                    if (m != null) m.Clashing = false;
                }
                return;
            }
            ReleaseMergedBeams(beam);
        }

        void ClearBeamClashPair(BeamBehaviour a, BeamBehaviour b, BeamBehaviour winner)
        {
            bool preserveA = winner == a && a != null && a.MergedBeams != null && a.MergedBeams.Count > 0;
            bool preserveB = winner == b && b != null && b.MergedBeams != null && b.MergedBeams.Count > 0;
            EndClashState(a, preserveA);
            EndClashState(b, preserveB);
        }

        bool TryJoinBallPush(BallBehaviour ball)
        {
            if (!DBPU.BeamAssistEnabled) return false;
            if (ball == null || beamBehaviour == null) return false;
            if (ball.transform.root == beamBehaviour.transform.root) return false;
            if (ball.PushingBeams.Contains(beamBehaviour)) return false;
            return CheckFacingBall(ball);
        }

        bool CheckFacingBall(BallBehaviour ball)
        {
            Vector3 ballPoint = ball.BallPosition + ball.BallDirection * ball.Distance;
            Vector3 toBall = ballPoint - beamBehaviour.StartBeamPosition;
            if (toBall.sqrMagnitude < 0.01f) return true;
            return Vector3.Angle(beamBehaviour.BeamDirection.normalized, toBall.normalized) <= 35f;
        }

        float GetCombinedBeamPushRoll(BallBehaviour ball)
        {
            float total = 0f;
            for (int i = ball.PushingBeams.Count - 1; i >= 0; i--)
            {
                BeamBehaviour b = ball.PushingBeams[i];
                if (b == null || !b.Firing)
                {
                    if (b != null) b.Clashing = false;
                    ball.PushingBeams.RemoveAt(i);
                    continue;
                }
                total += Random.Range(1f, 100f) * GetCombinedSidePower(b);
            }
            return total;
        }

        void FinishBeamBallWin(BallBehaviour otherBall)
        {
            foreach (BeamBehaviour b in otherBall.PushingBeams)
            {
                if (b == null) continue;
                b.TimeHolding = 0f;
                b.FinalHoldingTime = b.HoldingTime;
                b.realSpeed = b.OriginalSpeed;
                b.Clashing = false;
            }
            otherBall.PushingBeams.Clear();
            otherBall.Distance = 0f;
            otherBall.Firing = false;
            otherBall.Clashing = false;
            otherBall.DelayTime = 0f;
            otherBall.FinalUseDelay = otherBall.UseDelay;
            otherBall.Speed = otherBall.OriginalSpeed;
        }

        void FinishBallBeamsWin(BallBehaviour otherBall)
        {
            foreach (BeamBehaviour b in otherBall.PushingBeams)
            {
                if (b == null) continue;
                if (b == beamBehaviour && IsSpecialBeamCannon && !b.SbcBlockedThisFire)
                {
                    Vector3 clashPos = beamBehaviour.EndBeam != null
                        ? beamBehaviour.EndBeam.transform.position
                        : beamBehaviour.EndBeamPosition;
                    TriggerSpecialBeamBlock(clashPos, beamBehaviour.Distance);
                }
                else
                {
                    b.Distance = 0f;
                    b.Firing = false;
                }
                b.Clashing = false;
                b.TimeHolding = 0f;
                b.FinalHoldingTime = b.HoldingTime;
                if (b != beamBehaviour || !b.SbcBlockedThisFire)
                    b.realSpeed = b.OriginalSpeed;
            }
            otherBall.PushingBeams.Clear();
            otherBall.Distance += beamBehaviour.Distance;
            otherBall.DelayTime = 0f;
            otherBall.FinalUseDelay = otherBall.UseDelay;
            otherBall.Speed = otherBall.OriginalSpeed;
            otherBall.Clashing = false;
        }

        public void OnTriggerStay2D(Collider2D other)
        {
            BeamComponent otherComp = other.GetComponent<BeamComponent>();
            if (otherComp != null && TryMergeBeams(otherComp.beamBehaviour))
                return;

            if (!other.gameObject.GetComponent<BallBehaviour.BallComponent>()) return;
            BallBehaviour ball = other.gameObject.GetComponent<BallBehaviour.BallComponent>().ballBehaviour;
            if (ball == null || ball.Orb != null || !ball.Clashing || beamBehaviour.Clashing || !DBPU.BeamAssistEnabled) return;
            if (!TryJoinBallPush(ball)) return;
            beamBehaviour.Clashing = true;
            beamBehaviour.realSpeed = 0f;
            ball.PushingBeams.Add(beamBehaviour);
        }

        public IEnumerator BeamClash(BeamBehaviour otherBeam)
        {
            beamBehaviour.realSpeed = 0f;
            otherBeam.realSpeed = 0f;

            Vector3 thisDir = beamBehaviour.BeamDirection.normalized;
            Vector3 otherDir = otherBeam.BeamDirection.normalized;

            Vector3 thisEnd = beamBehaviour.StartBeamPosition + thisDir * beamBehaviour.Distance;
            Vector3 otherEnd = otherBeam.StartBeamPosition + otherDir * otherBeam.Distance;

            Vector3 collisionPoint = (thisEnd + otherEnd) / 2f;
            if (BeamPhysics.On && beamBehaviour.Body != null && beamBehaviour.Body.Live && otherBeam.Body != null && otherBeam.Body.Live)
            {
                collisionPoint = (beamBehaviour.Body.Tip + otherBeam.Body.Tip) * .5f;
                BeamDuel.Begin(beamBehaviour, otherBeam, collisionPoint);
            }

            float thisNewDistance = Vector3.Distance(beamBehaviour.StartBeamPosition, collisionPoint);
            float otherNewDistance = Vector3.Distance(otherBeam.StartBeamPosition, collisionPoint);

            float safeMargin = SafeMargin;
            thisNewDistance = Mathf.Max(0, thisNewDistance + safeMargin);
            otherNewDistance = Mathf.Max(0, otherNewDistance + safeMargin);

            beamBehaviour.Distance = thisNewDistance;
            beamBehaviour.EndBeamPosition = beamBehaviour.StartBeamPosition + thisDir * thisNewDistance;

            otherBeam.Distance = otherNewDistance;
            otherBeam.EndBeamPosition = otherBeam.StartBeamPosition + otherDir * otherNewDistance;

            RealityRift.TryBeamClash(beamBehaviour, otherBeam, collisionPoint);

            if (DBPU.BeamClashShockwaveEnabled)
                StartCoroutine(PlayClashShockwaveOneShot(GetBeamClashPoint(beamBehaviour, otherBeam), beamBehaviour, otherBeam));

            float waitElapsed = 0f;
            while (waitElapsed < 0.1f)
            {
                SyncBeamTipTransform(beamBehaviour);
                SyncBeamTipTransform(otherBeam);
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            StartCoroutine(BeamStruggle(otherBeam));

            yield return null;
        }

        public IEnumerator BeamStruggle(BeamBehaviour otherBeam)
        {
            beamBehaviour.realSpeed = 0f;
            otherBeam.realSpeed = 0f;

            Vector3 thisDir = beamBehaviour.BeamDirection.normalized;
            Vector3 otherDir = otherBeam.BeamDirection.normalized;

            float updateInterval = 0.01f;
            float elapsed = 0f;
            float timeBeforeAllowingWin = 1.0f;

            RegisterBeamClash(beamBehaviour, otherBeam);
            BeamDuel duel = BeamDuel.Of(beamBehaviour);

            while (beamBehaviour.Firing == true && otherBeam.Firing == true)
            {
                ScanForClashAssist(beamBehaviour);
                ScanForClashAssist(otherBeam);

                if (duel != null)
                {
                    duel.Step(Mathf.Min(Time.deltaTime, 0.1f));
                    elapsed += Time.deltaTime;
                }

                else
                {
                    float thisRoll = GetClashSideRoll(beamBehaviour);
                    float otherRoll = GetClashSideRoll(otherBeam);

                    float shiftAmount = (0.05f * DBPU.Beam_SpeedMultiplier) * Mathf.Clamp(thisRoll / Mathf.Max(otherRoll, 0.01f), 1f, 5f);

                    if (thisRoll > otherRoll)
                    {
                        beamBehaviour.Distance += shiftAmount;
                        otherBeam.Distance = Mathf.Max(0f, otherBeam.Distance - shiftAmount);
                    }

                    else
                    {
                        otherBeam.Distance += shiftAmount;
                        beamBehaviour.Distance = Mathf.Max(0f, beamBehaviour.Distance - shiftAmount);
                    }

                    beamBehaviour.EndBeamPosition = beamBehaviour.StartBeamPosition + thisDir * beamBehaviour.Distance;
                    otherBeam.EndBeamPosition = otherBeam.StartBeamPosition + otherDir * otherBeam.Distance;

                    SyncBeamTipTransform(beamBehaviour);
                    SyncBeamTipTransform(otherBeam);

                    elapsed += updateInterval;
                }

                if (elapsed >= timeBeforeAllowingWin)
                {
                    Vector3 thisEnd = beamBehaviour.EndBeamPosition;
                    Vector3 otherStart = otherBeam.StartBeamPosition;

                    Vector3 otherEnd = otherBeam.EndBeamPosition;
                    Vector3 thisStart = beamBehaviour.StartBeamPosition;

                    float dist1 = duel != null ? duel.GapTo(otherBeam) : Vector3.Distance(thisEnd, otherStart);
                    float dist2 = duel != null ? duel.GapTo(beamBehaviour) : Vector3.Distance(otherEnd, thisStart);

                    if (dist1 <= otherBeam.EndBeam.GetComponent<BeamComponent>().MinDistanceToLose)
                    {
                        if (duel != null) { duel.End(beamBehaviour); duel = null; }
                        beamBehaviour.Distance += otherBeam.Distance;
                        beamBehaviour.TimeHolding = 0f;
                        beamBehaviour.FinalHoldingTime = beamBehaviour.HoldingTime;
                        beamBehaviour.realSpeed = beamBehaviour.OriginalSpeed;

                        otherBeam.Distance = 0f;
                        otherBeam.Firing = false;

                        ClearBeamClashPair(beamBehaviour, otherBeam, beamBehaviour);
                        break;
                    }

                    if (dist2 <= MinDistanceToLose)
                    {
                        if (duel != null) { duel.End(otherBeam); duel = null; }
                        if (IsSpecialBeamCannon && !beamBehaviour.SbcBlockedThisFire)
                        {
                            Vector3 clashPos = (beamBehaviour.EndBeam != null)
                                ? beamBehaviour.EndBeam.transform.position
                                : beamBehaviour.EndBeamPosition;
                            TriggerSpecialBeamBlock(clashPos, beamBehaviour.Distance);
                        }

                        otherBeam.Distance += beamBehaviour.Distance;
                        otherBeam.TimeHolding = 0f;
                        otherBeam.FinalHoldingTime = otherBeam.HoldingTime;
                        otherBeam.realSpeed = otherBeam.OriginalSpeed;

                        if (!beamBehaviour.SbcBlockedThisFire)
                        {
                            beamBehaviour.Distance = 0f;
                            beamBehaviour.Firing = false;
                        }

                        ClearBeamClashPair(beamBehaviour, otherBeam, otherBeam);
                        break;
                    }
                }

                if (duel != null) yield return null;
                else yield return new WaitForSeconds(updateInterval);
            }

            if (duel != null) duel.End(null);
            UnregisterBeamClash(beamBehaviour, otherBeam);

            bool preserveThis = beamBehaviour.Firing && beamBehaviour.MergedBeams != null && beamBehaviour.MergedBeams.Count > 0;
            bool preserveOther = otherBeam.Firing && otherBeam.MergedBeams != null && otherBeam.MergedBeams.Count > 0;
            EndClashState(beamBehaviour, preserveThis);
            EndClashState(otherBeam, preserveOther);
            beamBehaviour.ClashOpponent = null;
            otherBeam.ClashOpponent = null;
            beamBehaviour.Clashing = false;
            beamBehaviour.TimeHolding = 0f;
            beamBehaviour.FinalHoldingTime = beamBehaviour.HoldingTime;
            if (!beamBehaviour.SbcBlockedThisFire)
                beamBehaviour.realSpeed = beamBehaviour.OriginalSpeed;

            otherBeam.Clashing = false;
            otherBeam.TimeHolding = 0f;
            otherBeam.FinalHoldingTime = beamBehaviour.HoldingTime;
            otherBeam.realSpeed = beamBehaviour.OriginalSpeed;
        }

        public IEnumerator BeamBallClash(BallBehaviour otherBall)
        {
            beamBehaviour.realSpeed = 0f;
            otherBall.Speed = 0f;

            yield return new WaitForSeconds(0.1f);

            StartCoroutine(BeamBallStruggle(otherBall));

            yield return null;
        }

        public IEnumerator BeamBallStruggle(BallBehaviour otherBall)
        {
            beamBehaviour.realSpeed = 0f;
            otherBall.Speed = 0f;

            Vector3 thisDir = beamBehaviour.BeamDirection.normalized;

            float updateInterval = 0.01f;
            float elapsed = 0f;
            float timeBeforeAllowingWin = 1.0f;

            while (otherBall.Firing == true && otherBall.PushingBeams.Count > 0)
            {
                foreach (BeamBehaviour b in otherBall.PushingBeams)
                {
                    if (b != null) b.realSpeed = 0f;
                }
                otherBall.Speed = 0f;

                float thisRoll = GetCombinedBeamPushRoll(otherBall);
                if (otherBall.PushingBeams.Count == 0) break;

                float otherRoll = Random.Range(1f, 100f) * otherBall.RealDamage * PowerLostByKiBall(otherBall);

                float shiftAmount = (0.05f * DBPU.Beam_SpeedMultiplier) * Mathf.Clamp(thisRoll / Mathf.Max(otherRoll, 0.01f), 1f, 5f);

                if (thisRoll > otherRoll)
                {
                    beamBehaviour.Distance += shiftAmount;
                    otherBall.Distance = Mathf.Max(0f, otherBall.Distance - shiftAmount);
                }
                else
                {
                    otherBall.Distance += shiftAmount * 2f;
                    beamBehaviour.Distance = Mathf.Max(0f, beamBehaviour.Distance - (shiftAmount * 2f));
                }

                beamBehaviour.EndBeamPosition = beamBehaviour.StartBeamPosition + thisDir * beamBehaviour.Distance;

                elapsed += updateInterval;

                if (elapsed >= timeBeforeAllowingWin)
                {
                    if (otherBall.Distance <= MinDistanceToLose)
                    {
                        beamBehaviour.Distance += otherBall.Distance;
                        beamBehaviour.TimeHolding = 0f;
                        beamBehaviour.FinalHoldingTime = beamBehaviour.HoldingTime;
                        beamBehaviour.realSpeed = beamBehaviour.OriginalSpeed;
                        FinishBeamBallWin(otherBall);
                        break;
                    }

                    if (beamBehaviour.Distance <= MinDistanceToLose)
                    {
                        FinishBallBeamsWin(otherBall);
                        break;
                    }
                }

                yield return new WaitForSeconds(updateInterval);
            }

            if (otherBall.PushingBeams.Count > 0)
            {
                foreach (BeamBehaviour b in otherBall.PushingBeams)
                {
                    if (b == null) continue;
                    b.Clashing = false;
                    b.TimeHolding = 0f;
                    b.FinalHoldingTime = b.HoldingTime;
                    if (!b.SbcBlockedThisFire)
                        b.realSpeed = b.OriginalSpeed;
                }
                otherBall.PushingBeams.Clear();
            }

            beamBehaviour.Clashing = false;
            otherBall.Clashing = false;
            otherBall.DelayTime = 0f;
            otherBall.FinalUseDelay = otherBall.UseDelay;
            otherBall.Speed = otherBall.OriginalSpeed;
        }

        bool CheckAngles(BeamBehaviour BB)
        {
            Vector3 thisDir = beamBehaviour.BeamDirection.normalized;
            Vector3 otherDir = BB.BeamDirection.normalized;

            float angleBetween = Vector3.Angle(thisDir, -otherDir);

            return angleBetween <= (BeamPhysics.On ? 40f : 10f);
        }

        float PowerLostByKi(BeamBehaviour BB)
        {
            float Percent = Mathf.Clamp((BB.KiSys.currentKi / BB.KiSys.maxKi) * 2f, 0.20f, 1f);

            return Percent;
        }

        float PowerLostByKiBall(BallBehaviour BB)
        {
            float Percent = Mathf.Clamp((BB.KiSys.currentKi / BB.KiSys.maxKi) * 2f, 0.20f, 1f);

            return Percent;
        }

        public IEnumerator HitWall()
        {
            if (beamBehaviour.realSpeed != 0f)
            {
                if (DistanceOfWallContact == 0f) DistanceOfWallContact = beamBehaviour.Distance;

                if (beamBehaviour.Distance > DistanceOfWallContact + (12f * DBPU.Beam_SizeMultiplier))
                {
                    HitAWall = true;
                    beamBehaviour.realSpeed = 0f;

                    if (IsSpecialBeamCannon && !beamBehaviour.SbcBlockedThisFire)
                    {
                        Vector3 tip = beamBehaviour.EndBeam != null
                            ? beamBehaviour.EndBeam.transform.position
                            : beamBehaviour.StartBeamPosition + beamBehaviour.BeamDirection * beamBehaviour.Distance;
                        TriggerSpecialBeamBlock(tip, beamBehaviour.Distance);
                    }
                }
            }

            yield return null;
        }

        public IEnumerator OnHit(RaycastHit2D hit, Vector2 direction)
        {
            if (hit.collider.gameObject.layer == 9 || hit.collider.gameObject.layer == 13)
            {
                if (hit.collider.gameObject.GetComponent<Rigidbody2D>())
                {
                    if (hit.collider.gameObject.GetComponent<BlastComponent>())
                    {
                        hit.collider.gameObject.GetComponent<BlastComponent>().HasHit = true;
                    }

                    float TimeScale = Global.main.SlowmotionTimescale;
                    if (!Global.main.SlowMotion) TimeScale = 1f;

                    Rigidbody2D rb = hit.collider.gameObject.GetComponent<Rigidbody2D>();

                    if (hit.collider.gameObject.GetComponent<LimbBehaviour>() && hit.collider.isTrigger != true)
                    {
                        LimbBehaviour limb = hit.collider.gameObject.GetComponent<LimbBehaviour>();
                        if (KiShield.Protects(limb.Person, beamBehaviour.gameObject, beamBehaviour.StartBeamPosition, (double)beamBehaviour.RealDamage * 60, beamBehaviour.GetInstanceID())) yield break;

                        if (BeamPhysics.On)
                        {
                            if (BeamPhysics.Ignores(beamBehaviour, limb.Person) || BeamPhysics.ReactToContact(beamBehaviour, limb)) yield break;
                        }

                        else if (KiDeflectUtils.TryDeflectBeamHit(beamBehaviour, limb))
                        {
                            yield break;
                        }

                        if (KiDeflectUtils.TryBeamSplitHit(beamBehaviour, limb, hit, direction))
                        {
                            yield break;
                        }

                        if (IsSpecialBeamCannon && !beamBehaviour.SbcBlockedThisFire && IsPersonStrongerThanAttacker(limb.Person))
                        {
                            TriggerSpecialBeamBlock(hit.point, GetDistAlongBeam(hit.point));
                            yield break;
                        }

                        // Contact has passed shielding, catches and beam splits. Only the
                        // touched tissue accumulates exposure, including an already dead body.
                        EnergyAnnihilation.Expose(beamBehaviour.GetComponent<PersonBehaviour>(), limb,
                            KiCombatMath.Cap(BeamPhysics.BeamPower(beamBehaviour)), hit.point,
                            Time.deltaTime, BeamStyle.Of(beamBehaviour).Mid);

                        if (KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                        {
                            float beamKnock = DBPU.EffectiveBeamKnockback * (beamBehaviour != null ? beamBehaviour.KnockbackOutput : 1f);
                            if (beamKnock > 0f)
                                rb.AddForce(direction * (((1.2f * TimeScale) * rb.mass * beamKnock)), ForceMode2D.Impulse);
                            yield return null;
                            yield break;
                        }

                        float beamDamage = GetCombinedBeamDamage(beamBehaviour);
                        float ForcePercent = (beamDamage * 60f) / MCP_Utils.AverageMaxHealth(limb.Person);
                        // Creature limbs erode through EnergyAnnihilation; immediate deletion
                        // would skip its skeleton/particle phase and break native limb links.
                        if (ForcePercent > 0.02f && DBPU.Beam_ToggleHeat == true && !KiDeflectUtils.IsBeamProtectedCorpse(limb.Person))
                        {
                            limb.PhysicalBehaviour.BurnProgress += (0.0008f * TimeScale);
                        }

                        if (ForcePercent > 0.005f)
                        {
                            limb.Damage(beamDamage * TimeScale);

                            float beamKnock = DBPU.EffectiveBeamKnockback * (beamBehaviour != null ? beamBehaviour.KnockbackOutput : 1f);
                            if (beamKnock > 0f)
                            {
                                float knockMult = IsSpecialBeamCannon ? 0.35f : 1f;
                                rb.AddForce(direction * ((((1.2f * TimeScale) * rb.mass * Mathf.Clamp(ForcePercent, 0.25f, 1f)) * beamKnock) * knockMult), ForceMode2D.Impulse);
                            }
                        }
                    }

                    else if (!hit.collider.gameObject.GetComponent<LimbBehaviour>() && hit.collider.gameObject.GetComponent<PhysicalBehaviour>())
                    {
                        if (DBPU.Beam_ToggleDelete == true) Destroy(hit.collider.gameObject);

                        else
                        {
                            float beamKnockObj = DBPU.EffectiveBeamKnockback * (beamBehaviour != null ? beamBehaviour.KnockbackOutput : 1f);
                            if (beamKnockObj > 0f)
                            {
                                rb.AddForce(direction * (((1f * TimeScale) * rb.mass * beamKnockObj)), ForceMode2D.Impulse);
                            }

                            if (DBPU.Beam_ToggleHeat == true)
                            {
                                hit.collider.gameObject.GetComponent<PhysicalBehaviour>().Temperature += (0.4f * TimeScale);
                            }

                            if (DBPU.Beam_ToggleDestroy == true)
                            {
                                if (hit.collider.gameObject.GetComponent<DestroyableBehaviour>())
                                {
                                    hit.collider.gameObject.GetComponent<DestroyableBehaviour>().Break();
                                }

                                if (hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>() && hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>().Destroyed == false)
                                {
                                    hit.collider.gameObject.GetComponent<DamagableMachineryBehaviour>().ForceBreak();
                                }
                            }
                        }
                    }
                }
            }

            yield return null;
        }

        public class ColliderVisualizer : MonoBehaviour
        {
            private BoxCollider2D box;
            private LineRenderer line;

            void Start()
            {
                box = GetComponent<BoxCollider2D>();

                line = gameObject.AddComponent<LineRenderer>();
                line.sortingLayerName = "Decals";
                line.sortingOrder = 1000;
                line.positionCount = 5;
                line.loop = false;
                line.widthMultiplier = 0.05f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = Color.red;
            }

            void Update()
            {
                if (box == null) return;

                Vector2 size = box.size;
                Vector2 offset = box.offset;
                Vector2 center = (Vector2)transform.position + (Vector2)(transform.rotation * offset);

                Vector2 halfSize = size * 0.5f;
                Vector2[] localCorners = new Vector2[4]
                {
                new Vector2(-halfSize.x, -halfSize.y),
                new Vector2(-halfSize.x,  halfSize.y),
                new Vector2( halfSize.x,  halfSize.y),
                new Vector2( halfSize.x, -halfSize.y)
                    };

                for (int i = 0; i < 4; i++)
                {
                    Vector2 worldPoint = center + (Vector2)(transform.rotation * Vector3.Scale(localCorners[i], transform.localScale));
                    line.SetPosition(i, worldPoint);
                }

                Vector2 firstWorldPoint = center + (Vector2)(transform.rotation * Vector3.Scale(localCorners[0], transform.localScale));
                line.SetPosition(4, firstWorldPoint);
            }
        }
    }
}
