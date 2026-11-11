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
    public class Barrage : MonoBehaviour
    {
        public GameObject projectilePrefab;
        public Sprite projectileSprite;
        public float duration = 0.2f;
        public const float baseDuration = 0.15f;
        public float damageOutput = 1f;
        public float speedOutput = 1f;
        public int projectileCount = 5;
        public float spawnInterval = 0.015f;
        public float distance = 3f;

        public float minSpawnInterval = 0.008f;
        public float maxSpawnInterval = 0.05f;

        public float randomHeightMin = 0.15f;
        public float randomHeightMax = 1f;
        public float randomPositionOffsetMagnitude = 0.4f;
        public float positiveAdditionAngle = 90f;
        public float negativeAdditionAngle = -270f;

        public float fixedOffsetValue = 1f;
        public int maxConsecutiveNegatives = 2;

        public bool useRandomHeight = true;
        private PersonBehaviour person;
        private PhysicalBehaviour phys;

        private int consecutiveNegativeCount = 0;
        private Coroutine barrageCoroutine;
        private bool isFiring = false;

        private PunchPower punchPower;
        public bool Enabled = true;

        private float time = 0;
        private List<GameObject> objects = new List<GameObject>();

        private MultipliersAndStuffs Multipliers;
        private List<SpriteRenderer> arms = new List<SpriteRenderer>();

        private void OnDestroy()
        {
            foreach (var obj in objects)
            {
                Destroy(obj);
            }
        }

        void Start()
        {
            person = transform.root.gameObject.GetComponent<PersonBehaviour>();
            phys = GetComponent<PhysicalBehaviour>();

            foreach (var limb in person.Limbs)
            {
                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("Arm"))
                {
                    var renderer = limb.gameObject.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                        arms.Add(renderer);
                }
            }

            Multipliers = transform.root.gameObject.GetComponent<MultipliersAndStuffs>();
            RefreshPunchPowerReference();
        }

        void RefreshPunchPowerReference()
        {
            if (punchPower != null) return;
            var lowerArm = person.Limbs.FirstOrDefault(x => x.name == "LowerArmFront")
                ?? person.Limbs.FirstOrDefault(x => x.name.Contains("LowerArm"));
            if (lowerArm != null)
                punchPower = lowerArm.GetComponent<PunchPower>();
        }

        Sprite CreateProjectileSprite(LimbBehaviour lowerArm)
        {
            if (lowerArm == null) return null;
            var frontArm = lowerArm.GetComponent<SpriteRenderer>();
            if (frontArm == null || frontArm.sprite == null) return null;

            Sprite originalSprite = frontArm.sprite;
            Texture2D readable = DuplicateProjectileTexture(originalSprite.texture);
            if (readable == null) return originalSprite;

            Rect rect = originalSprite.rect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (w <= 0 || h <= 0) return originalSprite;

            Color[] pixels;
            try
            {
                pixels = readable.GetPixels(x, y, w, h);
            }
            catch
            {
                return originalSprite;
            }

            var newTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            newTexture.filterMode = FilterMode.Point;
            newTexture.wrapMode = TextureWrapMode.Clamp;
            newTexture.SetPixels(pixels);
            newTexture.Apply(false, false);

            return Sprite.Create(newTexture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), originalSprite.pixelsPerUnit);
        }

        static Texture2D DuplicateProjectileTexture(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.filterMode = FilterMode.Point;
                copy.wrapMode = source.wrapMode;
                copy.SetPixels32(source.GetPixels32());
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                return null;
            }
        }

        void Update()
        {
            if (!EnergyAnatomy.CanFight(person))
            {
                StopBarrage();
                foreach (var renderer in arms)
                {
                    if (!renderer || !renderer.gameObject.activeInHierarchy) continue;
                    var part = renderer.GetComponent<LimbBehaviour>();
                    if (part && part.PhysicalBehaviour && !part.PhysicalBehaviour.isDisintegrated) renderer.enabled = true;
                }
                return;
            }
            if (Enabled == true && DbpuInputUtil.Down(DBPU.BarrageActionId, KeyCode.H) && CeceUtils.IsSelected(phys))
            {
                StartBarrage();
                foreach (var r in arms)
                {
                    r.enabled = false;
                }
                time = 0;
            }

            if (time < 0.5f && isFiring == true)
            {
                duration = baseDuration / 1.2f;
                time += Time.deltaTime;
            }

            else
            {
                duration = baseDuration;
            }

            if (DbpuInputUtil.Up(DBPU.BarrageActionId, KeyCode.H) || (Enabled == false && isFiring == true))
            {
                StopBarrage();

                if (Time.timeScale >= 0.01f)
                {
                    foreach (var r in arms)
                    {
                        r.enabled = true;
                    }
                }
            }

            if (Time.timeScale >= 0.01f && !DbpuInputUtil.Held(DBPU.BarrageActionId, KeyCode.H))
            {
                foreach (var r in arms)
                {
                    r.enabled = true;
                }
            }
        }

        void StartBarrage()
        {
            if (barrageCoroutine != null) return;

            RefreshPunchPowerReference();
            var lowerArm = person.Limbs.FirstOrDefault(x => x.name == "LowerArmFront")
                ?? person.Limbs.FirstOrDefault(x => x.name.Contains("LowerArm"));
            if (punchPower == null && lowerArm != null)
            {
                punchPower = lowerArm.GetComponent<PunchPower>();
                if (punchPower == null)
                {
                    punchPower = lowerArm.gameObject.AddComponent<PunchPower>();
                }
            }

            if (projectileSprite == null)
                projectileSprite = CreateProjectileSprite(lowerArm);
            if (projectileSprite == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (!limb.name.Contains("Arm")) continue;
                    projectileSprite = CreateProjectileSprite(limb);
                    if (projectileSprite != null) break;
                }
            }
            if (projectileSprite == null)
                projectileSprite = DBPU.KiBlast_Sprite;

            isFiring = true;
            barrageCoroutine = StartCoroutine(SpawnProjectiles());
        }

        void StopBarrage()
        {
            isFiring = false;
            if (barrageCoroutine != null)
            {
                StopCoroutine(barrageCoroutine);
                barrageCoroutine = null;
            }
        }

        IEnumerator SpawnProjectiles()
        {
            RefreshPunchPowerReference();
            if (Multipliers == null)
                Multipliers = transform.root.gameObject.GetComponent<MultipliersAndStuffs>();

            float multiplier = Multipliers != null ? Mathf.Max(0.01f, Multipliers.FinalMultiplier) : 1f;
            float baseDamage = punchPower != null && punchPower.Damage >= 0f
                ? punchPower.Damage
                : CombatPowerController.AttackHealth(person) * 0.01f / Mathf.Max(1f, multiplier);
            float punchDamageOutput = punchPower != null ? punchPower.DamageOutput : 1f;
            float Damage = (((baseDamage / 20f) * multiplier) * punchDamageOutput * damageOutput) * DBPU.DamageMultiplier * CombatPowerController.OutputFor(person);

            float speedMultiplier = MapToInvertedOutput(Damage, 5000f, 2000000000000f, minSpawnInterval, maxSpawnInterval) / Mathf.Max(speedOutput, 0.0001f);

            while (isFiring)
            {
                for (int i = 0; i < projectileCount; i++)
                {
                    SpawnProjectile(i);

                    yield return new WaitForSeconds(Mathf.Clamp(speedMultiplier, minSpawnInterval, maxSpawnInterval));

                    if (!isFiring) yield break;
                }
            }
        }

        void SpawnProjectile(int projectileIndex)
        {
            if (!EnergyAnatomy.CanFight(person)) return;
            if (projectileSprite == null)
            {
                Debug.LogError("No sprite assigned to projectileSprite! Assign a sprite in the Inspector.");
                return;
            }

            GameObject projectile = new GameObject("proj");
            projectile.transform.localScale = transform.root.localScale;
            var sr = projectile.AddComponent<SpriteRenderer>();

            projectile.transform.rotation *= Quaternion.Euler(0, 0, 90);
            objects.Add(projectile);

            int WhatLayer = UnityEngine.Random.Range(0, 2);

            sr.sprite = projectileSprite;
            var colorLimb = person.Limbs.FirstOrDefault(x => x.name == "LowerArmFront")
                ?? person.Limbs.FirstOrDefault(x => x.name.Contains("LowerArm"));
            if (colorLimb != null)
                sr.color = colorLimb.gameObject.GetComponent<SpriteRenderer>().color;
            sr.sortingLayerName = (WhatLayer == 0) ? "Background" : "Foreground";
            sr.sortingOrder = 5;

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.numCornerVertices = 20;
            trail.numCapVertices = 20;
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0, 0.15f);
            widthCurve.AddKey(1, 0.01f);
            trail.widthCurve = widthCurve;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(sr.color, 0.0f), new GradientColorKey(sr.color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0.0f), new GradientAlphaKey(0f, 1.0f) }
                );

            trail.colorGradient = gradient;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.sortingLayerName = (WhatLayer == 0) ? "Background" : "Foreground";
            trail.sortingOrder = sr.sortingOrder - 1;

            projectile.transform.position = transform.position;

            StartCoroutine(MoveAlongBezier(projectile, projectileIndex));
        }

        IEnumerator MoveAlongBezier(GameObject projectile, int projectileIndex)
        {
            TrailRenderer trail = projectile.GetComponent<TrailRenderer>();

            Vector3 startPos = transform.position + (transform.right * (Random.Range(0.4f, -0.8f) * Mathf.Sign(transform.root.localScale.x)));
            Vector3 direction = transform.root.localScale.x > 0 ? transform.right : -transform.right;

            RaycastHit2D hit = Physics2D.Raycast(startPos, direction, distance * Mathf.Abs(transform.root.localScale.x));
            while (hit.collider != null && hit.collider.transform.root == transform.root)
            {
                hit = Physics2D.Raycast((Vector3)hit.point + direction * Mathf.Abs(transform.root.localScale.x) * 0.1f, direction, distance * Mathf.Abs(transform.root.localScale.x) - Vector3.Distance(startPos, hit.point));
            }

            Vector3 endPos;
            if (hit.collider != null)
            {
                endPos = hit.point;
            }

            else
            {
                endPos = transform.position + direction * distance * Mathf.Abs(transform.root.localScale.x);
            }

            var dist = Vector3.Distance(endPos, startPos);
            var factor = distance / dist;
            float heightOffset;
            float addition;

            if (useRandomHeight)
            {
                heightOffset = Random.Range(randomHeightMin, randomHeightMax) * Mathf.Abs(transform.root.localScale.x);
                float randVal = Random.value;
                float plus = (randVal > 0.5f) ? randomPositionOffsetMagnitude : -randomPositionOffsetMagnitude;
                startPos += transform.up * Random.Range(0f, plus) * Mathf.Abs(transform.root.localScale.x);
                endPos += transform.up * Random.Range(0f, plus) * Mathf.Abs(transform.root.localScale.x);
                projectile.transform.position = startPos;
                heightOffset *= (randVal > 0.5f) ? 1f : -1f;
                addition = (randVal > 0.5f) ? positiveAdditionAngle : negativeAdditionAngle;
            }

            else
            {
                float baseSign;
                if (consecutiveNegativeCount >= maxConsecutiveNegatives)
                {
                    baseSign = fixedOffsetValue;
                    consecutiveNegativeCount = 0;
                }

                else
                {
                    if (Random.value < 0.5f)
                    {
                        baseSign = -fixedOffsetValue;
                        consecutiveNegativeCount++;
                    }

                    else
                    {
                        baseSign = fixedOffsetValue;
                        consecutiveNegativeCount = 0;
                    }
                }

                heightOffset = baseSign + Random.Range(-0.2f, 0.2f);
                addition = (baseSign > 0) ? positiveAdditionAngle : negativeAdditionAngle;
            }

            Vector3 controlPoint1 = startPos + transform.up * heightOffset;
            Vector3 controlPoint2 = endPos + transform.up * heightOffset;

            Vector3 midPoint = CalculateCubicBezierPoint(0.5f, startPos, controlPoint1, controlPoint2, endPos);
            Vector3 initialDirection = (midPoint - startPos).normalized;
            float initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;

            if (useRandomHeight)
            {
                initialAngle += (addition == positiveAdditionAngle) ? -270f : 90f;
            }

            else
            {
                initialAngle += (heightOffset < 0 ? -180f : 0f);
            }

            Quaternion initialRotation = Quaternion.Euler(0, 0, initialAngle);

            Vector3 finalDirection = (endPos - controlPoint1).normalized;
            float kiss = heightOffset < 0 ? 10f : -10f;
            kiss = transform.root.localScale.x > 0 ? kiss : -kiss;
            float finalAngle = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg + (addition + kiss);
            Quaternion finalRotation = Quaternion.Euler(0, 0, finalAngle);

            projectile.transform.rotation = initialRotation;
            SpriteRenderer sr = projectile.GetComponent<SpriteRenderer>();

            /*float damage = (((punchPower.Damage / 20f) * Multipliers.FinalMultiplier) * punchPower.DamageOutput) * DBPU.DamageMultiplier;
            float speedMultiplier = MapToInvertedOutput(damage, 10000f, 2000000000000f, 1f, 2f);

            float travelTime = (duration / 2f) * speedMultiplier;*/

            float travelTime = duration;

            float elapsedTime = 0f;

            while (elapsedTime < Mathf.Clamp(duration * factor, 0.05f, 0.3f))
            {
                float t = elapsedTime / travelTime;
                Vector3 currentPos = Mathf.Pow(1 - t, 3) * startPos +
                                        3 * Mathf.Pow(1 - t, 2) * t * controlPoint1 +
                                        3 * (1 - t) * Mathf.Pow(t, 2) * controlPoint2 +
                                        Mathf.Pow(t, 3) * endPos;
                projectile.transform.position = currentPos;
                projectile.transform.rotation = Quaternion.Lerp(initialRotation, finalRotation, t);

                Vector2 boxSize = new Vector2(sr.sprite.bounds.size.x * projectile.transform.lossyScale.x, sr.sprite.bounds.size.y * projectile.transform.lossyScale.y);

                Collider2D[] hits = Physics2D.OverlapBoxAll(projectile.transform.position, boxSize, projectile.transform.rotation.eulerAngles.z);

                foreach (Collider2D col in hits)
                {
                    if (col.transform.root != transform.root)
                    {
                        Vector2 hitPoint = col.ClosestPoint(projectile.transform.position);
                        /*Vector2 normal = ((Vector2)projectile.transform.position - hitPoint).normalized;
                        Shot shot = new Shot(normal, hitPoint, 50, true, null);
                        col.transform.SendMessage("Shot", shot, SendMessageOptions.DontRequireReceiver);*/

                        float Damage = (punchPower.Damage / 20f) * punchPower.DamageOutput;

                        SkinMaterialHandler skin;
                        if (col.TryGetComponent<SkinMaterialHandler>(out skin))
                        {
                            float damagePercentage = ((Damage * Multipliers.FinalMultiplier) / MCP_Utils.AverageMaxHealth(skin.gameObject.GetComponent<LimbBehaviour>().Person)) * 100f;
                            float bluntVisualDamage = (100f * Mathf.Clamp(damagePercentage, 0f, 2.5f)) / skin.intensityMultiplier;
                            float bulletVisualDamage = (100f * Mathf.Clamp(damagePercentage, 0f, 2.5f)) / skin.intensityMultiplier;

                            if (damagePercentage > 0.02f)
                            {
                                var targetLimb = skin.gameObject.GetComponent<LimbBehaviour>();
                                float dealtDamage = Damage * Multipliers.FinalMultiplier * damageOutput * DBPU.DamageMultiplier * CombatPowerController.OutputFor(person);
                                CombatDismemberment.TryMeleeSever(person, targetLimb, dealtDamage,
                                    Vector3.Distance(startPos, endPos) / Mathf.Max(.02f, travelTime), hitPoint, endPos - startPos);
                                targetLimb.Damage(dealtDamage);
                                skin.AddDamagePoint(DamageType.Blunt, hitPoint, bluntVisualDamage / 5f);
                                skin.AddDamagePoint(DamageType.Bullet, hitPoint, bulletVisualDamage / 5f);
                            }
                        }

                        if (UnityEngine.Random.value < 0.3f && col.GetComponent<LimbBehaviour>() != null)
                        {
                            var particle = Instantiate(DBPU.barrageImpact, hitPoint, DBPU.barrageImpact.transform.rotation);
                            particle.transform.localScale /= 2.5f;

                            var partsys = particle.GetComponent<ParticleSystem>();
                            var renderer = particle.GetComponent<ParticleSystemRenderer>();
                            renderer.sortingLayerName = col.GetComponent<Renderer>().sortingLayerName;
                            renderer.sortingOrder = col.GetComponent<Renderer>().sortingOrder + 1;

                            foreach (Transform child in particle.transform)
                            {
                                var renderers = child.gameObject.GetComponent<ParticleSystemRenderer>();
                                renderers.sortingLayerName = col.GetComponent<Renderer>().sortingLayerName;
                                renderers.sortingOrder = col.GetComponent<Renderer>().sortingOrder + 1;
                            }

                            partsys.Play();
                            Destroy(particle, 1.5f);
                        }

                        Rigidbody2D rb = col.GetComponent<Rigidbody2D>();

                        if (rb != null)
                        {
                            Vector2 forceDir = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;

                            if (col.TryGetComponent<LimbBehaviour>(out LimbBehaviour limb))
                            {
                                float damagePercentage = ((Damage * Multipliers.FinalMultiplier) / MCP_Utils.AverageMaxHealth(limb.Person)) * 100f;
                                float knockbackImpulse = (damagePercentage >= 25f) ? punchPower.maxKnockbackImpulse : punchPower.maxKnockbackImpulse * (damagePercentage / 25f);

                                if (damagePercentage > 0.05f) rb.AddForce(forceDir * Mathf.Clamp(Mathf.Abs(knockbackImpulse - knockbackImpulse * 0.02f * (rb.mass / 0.14f)), (punchPower.maxKnockback / 50f) * (rb.mass / 0.5f), (punchPower.maxKnockback / 2.5f) * (rb.mass / 0.5f)), ForceMode2D.Impulse);
                            }

                            else
                            {
                                float damagePercentage = ((Damage * Multipliers.FinalMultiplier) / rb.mass) / 1000f;
                                float knockbackImpulse = (damagePercentage >= 25f) ? punchPower.maxKnockbackImpulse : punchPower.maxKnockbackImpulse * (damagePercentage / 25f);

                                if (damagePercentage > 0.05f) rb.AddForce(forceDir * Mathf.Clamp(Mathf.Abs(knockbackImpulse - knockbackImpulse * 0.01f * (rb.mass / 0.14f)), (punchPower.maxKnockback / 100f) * (rb.mass / 0.5f), (punchPower.maxKnockback / 5f) * (rb.mass / 0.5f)), ForceMode2D.Impulse);

                                if ((damagePercentage / 2f > UnityEngine.Random.Range(0, 100f)) && col.gameObject.GetComponent<DestroyableBehaviour>() && DBPU.PhysAtkCanDestroy == true) col.gameObject.GetComponent<DestroyableBehaviour>().Break();
                            }
                        }

                        Destroy(projectile);
                        yield break;
                    }
                }

                if (t >= 0.75f)
                {
                    float fadeT = (t - 0.75f) / 0.25f;
                    float newAlpha = Mathf.Lerp(1f, 0f, fadeT);
                    Color spriteColor = sr.color;
                    spriteColor.a = newAlpha;
                    sr.color = spriteColor;

                    Gradient newGradient = new Gradient();
                    newGradient.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(sr.color, 0.0f), new GradientColorKey(sr.color, 1.0f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(newAlpha * 0.5f, 0.0f), new GradientAlphaKey(0f, 1.0f) }
                        );
                    trail.colorGradient = newGradient;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            projectile.transform.position = endPos;
            projectile.transform.rotation = finalRotation;
            Color finalColor = sr.color;
            finalColor.a = 0f;
            sr.color = finalColor;
            Destroy(projectile);
        }

        private IEnumerator ResetMovement(LimbBehaviour targetLimb)
        {
            if (targetLimb == null) yield break;

            if (targetLimb.gameObject.TryGetComponent<Rigidbody2D>(out Rigidbody2D rigidbody))
            {
                if (rigidbody.bodyType == RigidbodyType2D.Static)
                {
                    yield break;
                }
            }

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                foreach (var limb in targetLimb.Person.Limbs)
                {
                    Rigidbody2D rb = limb.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.bodyType != RigidbodyType2D.Static) rb.velocity = Vector2.zero;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0;
            point += 3 * uu * t * p1;
            point += 3 * u * tt * p2;
            point += ttt * p3;
            return point;
        }

        float MapToInvertedOutput(float input, float inputMin, float inputMax, float outputMin, float outputMax, float exponent = 0.065f)
        {
            if (input <= inputMin) return outputMax;
            if (input >= inputMax) return outputMin;

            float normalizedInput = (input - inputMin) / (inputMax - inputMin);
            float curveFactor = Mathf.Pow(normalizedInput, exponent);

            float output = (1 - curveFactor) * outputMax + curveFactor * outputMin;

            return Mathf.Max(output, outputMin);
        }
    }
}
