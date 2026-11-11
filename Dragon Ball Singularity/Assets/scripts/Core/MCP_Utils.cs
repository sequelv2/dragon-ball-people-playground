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
    /*
    [HarmonyPatch(typeof(PhysicalBehaviour), "HandleSounds")]
    public static class PhysicalBehaviour_HandleSounds_WeightImpactPatch
    {
        static bool Prefix(PhysicalBehaviour __instance, Collision2D collision, float impulse)
        {
            if (__instance == null || !__instance.rigidbody)
                return true;

            LimbBehaviour limb = __instance.GetComponent<LimbBehaviour>();
            if (limb == null || limb.Person == null)
                return true;

            var link = limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (link == null || link.Controller == null)
                return true;

            float weight = MCP_Utils.AverageWeight(limb.Person);

            if (weight <= 0.4f)
                return true;

            PhysicalProperties props = __instance.Properties;
            if (props == null)
                return true;

            float weightScale = Mathf.Max(1f, weight / 0.12f);

            float impactIntensity = impulse * props.ImpactIntensityMutliplier;

            if (impactIntensity < 0.002f)
                return false;

            // Keep volume using REAL mass, otherwise impacts become too quiet/weird.
            float realMass = Mathf.Max(1f, __instance.rigidbody.mass);

            // Use "fake normal mass" only for deciding HardImpact vs SoftImpact.
            float perceivedMass = Mathf.Max(1f, __instance.rigidbody.mass / weightScale);

            bool hardImpact = impactIntensity > 0.5f / perceivedMass;

            float volume = impactIntensity * 0.3f / realMass * props.HitVolumeMultiplier;

            if (UserPreferenceManager.Current.ClampVolume)
                volume = Mathf.Clamp01(volume);

            if (__instance.OverrideImpactSounds != null && __instance.OverrideImpactSounds.Length != 0)
            {
                __instance.PlayClipOnce(__instance.OverrideImpactSounds.PickRandom(), volume);
            }
            else if (hardImpact)
            {
                if (props.HardImpact != null && props.HardImpact.Length != 0)
                    __instance.PlayClipOnce(props.HardImpact.PickRandom(), volume);
            }
            else
            {
                if (props.SoftImpact != null && props.SoftImpact.Length != 0)
                    __instance.PlayClipOnce(props.SoftImpact.PickRandom(), volume);
            }

            return false;
        }
    }
    */


    public static class MCP_Utils
    {
        const float PersonStatCacheSeconds = 0.5f;
        static readonly Dictionary<int, float> _maxHealthCache = new Dictionary<int, float>();
        static readonly Dictionary<int, float> _maxHealthCacheUntil = new Dictionary<int, float>();
        static readonly Dictionary<int, float> _weightCache = new Dictionary<int, float>();
        static readonly Dictionary<int, float> _weightCacheUntil = new Dictionary<int, float>();
        static int _objectsDebrisMask = -1;

        public static int ObjectsDebrisMask
        {
            get
            {
                if (_objectsDebrisMask < 0)
                    _objectsDebrisMask = LayerMask.GetMask("Objects", "CollidingDebris");
                return _objectsDebrisMask;
            }
        }

        public static void InvalidatePersonStatCache(PersonBehaviour person)
        {
            if (person == null) return;
            int id = person.GetInstanceID();
            _maxHealthCache.Remove(id);
            _maxHealthCacheUntil.Remove(id);
            _weightCache.Remove(id);
            _weightCacheUntil.Remove(id);
        }

        public static bool LimbCanUsePowers(LimbBehaviour limb)
        {
            return limb != null && limb.GetComponent<CellMaxTailSegment>() == null && EnergyAnatomy.CanUse(limb);
        }

        public static GameObject PlayAudio(Vector3 targetPos, AudioClip sound, Transform attached = null, bool autoDelete = true, float volume = 1f, bool loop = false, float spatialBlend = 1f, float maxDistance = 1000f, float distortionLevel = 0.85f, string name = "SoundEffect")
        {
            GameObject soundObject = new GameObject(name);
            if (attached != null) soundObject.transform.SetParent(attached);
            soundObject.transform.position = targetPos;
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            Global global = GameObject.FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = distortionLevel;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = spatialBlend;
            audioSource.volume = volume;
            audioSource.maxDistance = maxDistance;
            audioSource.clip = sound;
            audioSource.loop = loop;
            audioSource.Play();
            if (autoDelete) GameObject.Destroy(soundObject, audioSource.clip.length);

            return soundObject;
        }

        public static void SafeRefreshOutline(PhysicalBehaviour phys)
        {
            if (phys == null) return;
            var person = phys.GetComponentInParent<PersonBehaviour>();
            if (person == null && phys.transform != null && phys.transform.root != null)
                person = phys.transform.root.GetComponent<PersonBehaviour>();
            if (person != null)
            {
                DeferredOutlineRefresh.RequestPerson(person);
                return;
            }
            DeferredOutlineRefresh.RequestSingle(phys);
        }

        static void IgnoreSelfCollisions(GameObject root)
        {
            if (root == null) return;
            var colliders = root.GetComponentsInChildren<Collider2D>(true);
            if (colliders == null || colliders.Length < 2) return;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    if (colliders[j] == null) continue;
                    Physics2D.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        public static void ChangeLimbSprite(this GameObject limb, Sprite skin, Texture flesh, Texture bone, Texture damage, bool FixColliders = true)
        {
            if (limb == null || skin == null) return;
            var sr = limb.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sprite = skin;
            if (sr.material != null)
            {
                sr.material.SetTexture("_FleshTex", flesh);
                sr.material.SetTexture("_BoneTex", bone);
                sr.material.SetTexture("_DamageTex", damage);
            }

            if (FixColliders) limb.FixColliders();

            IgnoreSelfCollisions(limb.transform.root != null ? limb.transform.root.gameObject : limb);

            var phys = limb.GetComponent<PhysicalBehaviour>();
            SafeRefreshOutline(phys);
        }

        public static void ChangeLimbSpriteFixed(this GameObject limb, Sprite skin, Texture flesh, Texture bone, Texture damage, Sprite collidersSkin)
        {
            if (limb == null) return;
            var sr = limb.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (collidersSkin != null)
            {
                sr.sprite = collidersSkin;
                if (sr.material != null)
                {
                    sr.material.SetTexture("_FleshTex", flesh);
                    sr.material.SetTexture("_BoneTex", bone);
                    sr.material.SetTexture("_DamageTex", damage);
                }
                limb.FixColliders();
            }

            if (skin != null) sr.sprite = skin;
            if (sr.material != null)
            {
                sr.material.SetTexture("_FleshTex", flesh);
                sr.material.SetTexture("_BoneTex", bone);
                sr.material.SetTexture("_DamageTex", damage);
            }

            IgnoreSelfCollisions(limb.transform.root != null ? limb.transform.root.gameObject : limb);

            SafeRefreshOutline(limb.GetComponent<PhysicalBehaviour>());
        }

        public static void ChangeSkin(this PersonBehaviour person, List<Sprite> skin, bool FixColliders = true)
        {
            if (person == null || person.Limbs == null || skin == null) return;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                if (i >= skin.Count) break;
                var limbBeh = person.Limbs[i];
                if (limbBeh == null || limbBeh.gameObject == null || skin[i] == null) continue;

                var limb = limbBeh.gameObject;
                var sr = limb.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                sr.sprite = skin[i];
                if (sr.material != null)
                {
                    sr.material.SetTexture("_FleshTex", LimbsStorage.FleshLayer[i]);
                    sr.material.SetTexture("_BoneTex", LimbsStorage.BoneLayer[i]);
                    sr.material.SetTexture("_DamageTex", LimbsStorage.DamageLayer[i]);
                }
                if (FixColliders) limb.FixColliders();
            }

            IgnoreSelfCollisions(person.gameObject);
            CeceUtils.FixBruises(person);
            DeferredOutlineRefresh.RequestPerson(person);
            DbpuMakerPhysics.CalmPerson(person);
        }

        public static void CreateAccessory(this Transform attachedObject, string name, Sprite sprite, Color? color = null, Vector3 positionOffSet = default, Quaternion rotationOffSet = default, Vector3 localScale = default, string sortingLayerName = null, int sortingOrder = -999)
        {
            if (attachedObject == null || sprite == null) return;

            var accessoryObject = new GameObject(name);
            accessoryObject.transform.SetParent(attachedObject);
            accessoryObject.transform.localPosition = (positionOffSet == default) ? Vector3.zero : positionOffSet;
            accessoryObject.transform.localRotation = (rotationOffSet == default) ? Quaternion.Euler(0f, 0f, 0f) : rotationOffSet;
            accessoryObject.transform.localScale = (localScale == default) ? Vector3.one : localScale;
            var accessorySprite = accessoryObject.AddComponent<SpriteRenderer>();
            accessorySprite.sprite = sprite;
            accessorySprite.color = color ?? Color.white;
            accessorySprite.sortingLayerName = (sortingLayerName == null) ? attachedObject.gameObject.GetComponent<SpriteRenderer>().sortingLayerName : sortingLayerName;
            accessorySprite.sortingOrder = (sortingOrder == -999) ? attachedObject.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 1 : sortingOrder;
        }

        public static void ModifyAccessory(this Transform attachedObject, string name, Sprite sprite = null, Color? color = null, Vector3 positionOffSet = default, Quaternion rotationOffSet = default, Vector3 localScale = default, string sortingLayerName = null, int sortingOrder = -999)
        {
            if (attachedObject == null || attachedObject.Find(name) == null) return;

            var accessoryObject = attachedObject.Find(name);
            if (positionOffSet != default) accessoryObject.transform.localPosition = positionOffSet;
            if (rotationOffSet != default) accessoryObject.transform.localRotation = rotationOffSet;
            if (localScale != default) accessoryObject.transform.localScale = localScale;
            var accessorySprite = accessoryObject.GetComponent<SpriteRenderer>();
            if (sprite != null) accessorySprite.sprite = sprite;
            if (color != null) accessorySprite.color = color ?? Color.white;
            if (sortingLayerName != null) accessorySprite.sortingLayerName = sortingLayerName;
            if (sortingOrder != -999) accessorySprite.sortingOrder = sortingOrder;
        }

        public static void SetHeight(this Transform root, PersonBehaviour person, GameObject originPoint, float Meters = 1.75f)
        {
            person.SetPersonScale(Meters / 1.68f, originPoint.transform);
        }

        public static void SetPersonScale(this PersonBehaviour person, float uniformScale, Transform anchor)
        {
            Transform personTransform = person.transform;
            Vector3 anchorBefore = anchor.position;

            var ix = personTransform.localScale.x;
            personTransform.localScale = new Vector3(uniformScale * (ix / Mathf.Abs(ix)), uniformScale, personTransform.localScale.z);

            Vector3 anchorAfter = anchor.position;
            personTransform.position -= (anchorAfter - anchorBefore);

            person.SyncLimbsPhysics();
            person.ForceAllLimbsClearViaExit();
        }

        public static void SyncLimbsPhysics(this PersonBehaviour person)
        {
            foreach (var limb in person.Limbs)
            {
                if (limb == null || limb.gameObject == null) continue;

                var rb = limb.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.position = limb.transform.position;
                    rb.rotation = limb.transform.eulerAngles.z;
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                if (limb.PhysicalBehaviour != null)
                {
                    limb.PhysicalBehaviour.RecalculateMassBasedOnSize();
                }
            }
        }

        public static void ChangeWeightBasedOnHealth(this PersonBehaviour person)
        {
            double baseHP = 1000.0;
            double baseWeight = 0.1;
            double factor = 1.365;

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                double scaled = Math.Log10(limb.InitialHealth / baseHP);
                float newWeigt = (float)(baseWeight * Math.Pow(factor, scaled));

                if (newWeigt < 0.15f) newWeigt = 0.15f;
                if (newWeigt > 10000f) newWeigt = 10000f;

                float scaleFactor = Mathf.Abs(person.transform.localScale.x) * Mathf.Abs(person.transform.localScale.y);
                float finalWeight = newWeigt / scaleFactor;

                limb.BaseStrength = finalWeight * 130f;
                limb.BreakingThreshold = Mathf.Clamp(finalWeight * 400000f, 1f, 100000000f);
                limb.PhysicalBehaviour.TrueInitialMass = finalWeight;
                limb.PhysicalBehaviour.RecalculateMassBasedOnSize();

                limb.FrictionBurnWoundMinSpeedSqrd = Mathf.Clamp(finalWeight * 10000f, 60f, 1000000000f);
                if (person.gameObject.GetComponentInChildren<HumanController>()) limb.GForcePassoutThreshold = person.gameObject.GetComponentInChildren<HumanController>().Features.KnockoutResistance * 10f;
                limb.ImpactPainMultiplier = 0f;
                limb.ImpactDamageMultiplier = Mathf.Clamp(0.0001f / Mathf.Max(0.15f, finalWeight), 1e-12f, 0.0001f);
                if (limb.PhysicalBehaviour.TrueInitialMass <= 40f)
                    limb.SkinMaterialHandler.intensityMultiplier = Mathf.Clamp(0.01f / Mathf.Max(0.15f, finalWeight), 1e-6f, 0.01f);
                else
                    limb.SkinMaterialHandler.intensityMultiplier = 0.0000001f;

                try
                {
                    var phys = limb.PhysicalBehaviour;
                    if (phys != null && phys.Properties != null)
                    {
                        var props = ScriptableObject.Instantiate(phys.Properties);
                        props.ImpactIntensityMutliplier = Mathf.Min(props.ImpactIntensityMutliplier, 0.05f / Mathf.Max(1f, finalWeight));
                        props.Softness = 0f;
                        props.Brittleness = 0f;
                        if (props.HardImpact != null && props.SoftImpact != null)
                            props.HardImpact = props.SoftImpact;
                        phys.Properties = props;
                    }
                }
                catch { }

                DecalDescriptor originalDecal = limb.BloodDecal;
                DecalDescriptor customDecal = ScriptableObject.Instantiate(originalDecal);
                customDecal.name = "CustomBloodDecal";

                if (limb.PhysicalBehaviour.TrueInitialMass >= 0.3f) customDecal.Color = Color.clear;
                else customDecal.Color = Color.white;

                limb.BloodDecal = customDecal;
            }
        }

        public static void AddTrueDelete(this GameObject limb)
        {
            limb.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("True Delete", "True Delete", "True Delete", () =>
            {
                GameObject.Destroy(limb.transform.root.gameObject);
            }));
        }

        public static float AverageCurrentHealth(PersonBehaviour person)
        {
            float currentAverageHealth = 0f;

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                currentAverageHealth += limb.Health;
            }

            currentAverageHealth /= (float)person.Limbs.Length;

            return currentAverageHealth;
        }

        public static float AverageMaxHealth(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length == 0) return 1f;

            int id = person.GetInstanceID();
            float now = Time.unscaledTime;
            if (_maxHealthCacheUntil.TryGetValue(id, out float until) && now < until
                && _maxHealthCache.TryGetValue(id, out float cached))
                return cached;

            float maxAverageHealth = 0f;
            int members = 0;

            foreach (var limb in person.Limbs)
            {
                if (limb == null || limb.gameObject == null) continue;

                maxAverageHealth += limb.InitialHealth;
                members += 1;
            }

            if (members <= 0) return 1f;
            maxAverageHealth /= (float)members;

            _maxHealthCache[id] = maxAverageHealth;
            _maxHealthCacheUntil[id] = now + PersonStatCacheSeconds;
            return maxAverageHealth;
        }

        public static float GetFinalMultiplier(PersonBehaviour person)
        {
            if (person == null) return 1f;
            var mult = person.GetComponent<MultipliersAndStuffs>();
            if (mult == null) mult = person.transform.root.GetComponent<MultipliersAndStuffs>();
            if (mult == null) return 1f;
            return Mathf.Max(1f, mult.FinalMultiplier);
        }

        public static float ResolveMeleeBaseDamage(PersonBehaviour attacker, MultipliersAndStuffs mult, float explicitDamage, float healthFactor)
        {
            if (explicitDamage >= 0f) return explicitDamage;
            if (mult != null)
            {
                mult.Recalculate();
            }
            float health = CombatPowerController.AttackHealth(attacker);
            float finalMult = mult != null ? Mathf.Max(1f, mult.FinalMultiplier) : GetFinalMultiplier(attacker);
            return health * healthFactor / finalMult;
        }

        public static float MeleeStopThreshold(float attackerPower, float victimPower, float baseStop = 0.5f)
        {
            float ratio = attackerPower / Mathf.Max(1f, victimPower);
            if (ratio < 0.75f) return Mathf.Max(baseStop, 8f);
            if (ratio < 1.25f) return Mathf.Max(baseStop, 2f);
            if (ratio < 3f) return Mathf.Max(baseStop, 1f);
            return baseStop;
        }

        public static bool MeetsMinDamageFraction(float damage, float maxHealth, float minFraction = 0.001f)
        {
            if (damage <= 0f || maxHealth <= 0f) return false;
            return damage / maxHealth > minFraction;
        }

        public static bool MeetsMinLimbDamage(LimbBehaviour limb, float damage, float minFraction = 0.001f)
        {
            if (limb == null) return false;
            return MeetsMinDamageFraction(damage, limb.InitialHealth, minFraction);
        }

        public static float AverageWeight(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null || person.Limbs.Length == 0) return 1f;

            int id = person.GetInstanceID();
            float now = Time.unscaledTime;
            if (_weightCacheUntil.TryGetValue(id, out float until) && now < until
                && _weightCache.TryGetValue(id, out float cached))
                return cached;

            float averageWeight = 0f;
            int members = 0;

            foreach (var limb in person.Limbs)
            {
                if (limb == null || limb.gameObject == null || limb.PhysicalBehaviour == null) continue;

                averageWeight += limb.PhysicalBehaviour.TrueInitialMass;
                members += 1;
            }

            if (members <= 0) return 1f;
            averageWeight /= (float)members;

            _weightCache[id] = averageWeight;
            _weightCacheUntil[id] = now + PersonStatCacheSeconds;
            return averageWeight;
        }

        public static Vector3 GetMiddlePositionOfBody(PersonBehaviour person)
        {
            float X = 0f;
            float Y = 0f;
            float Z = 0f;
            float limbs = 0f;

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null || limb.IsDismembered == true) continue;

                X += limb.transform.position.x;
                Y += limb.transform.position.y;
                Z += limb.transform.position.z;

                limbs += 1f;
            }

            X /= limbs;
            Y /= limbs;
            Z /= limbs;

            return new Vector3(X, Y, Z);
        }

        public static void ForceAllLimbsClearViaExit(this PersonBehaviour person)
        {
            foreach (var limb in person.Limbs)
            {
                ForceClearViaExit(limb.PhysicalBehaviour);
            }
        }

        public static void ForceClearViaExit(PhysicalBehaviour phys)
        {
            var bufferField = typeof(PhysicalBehaviour).GetField("onCollisionStayBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (bufferField == null) return;

            var buffer = bufferField.GetValue(phys) as Array;
            if (buffer == null) return;

            foreach (var entry in buffer)
            {
                if (entry == null) continue;

                var activeField = entry.GetType().GetField("Active");
                activeField?.SetValue(entry, false);
            }
        }

        public static bool CheckContextButton(ContextMenuOptionComponent buttons, string Identity)
        {
            if (buttons == null) return false;

            ContextMenuButton button = buttons.Buttons.FirstOrDefault(c => c.Identity == Identity);

            return !button.Equals(default(ContextMenuButton));
        }

        public static void AddContextButtonInOrder(ContextMenuOptionComponent buttons, ContextMenuButton button, int newPosition)
        {
            if (buttons == null) return;

            buttons.Buttons.Insert(Mathf.Clamp(newPosition, 0, buttons.Buttons.Count), button);
        }

        public static void MoveContextButton(ContextMenuOptionComponent buttons, string Identity, int newPosition)
        {
            if (buttons == null || buttons.Buttons == null) return;

            ContextMenuButton button = buttons.Buttons.FirstOrDefault(c => c.Identity == Identity);
            if (button.Equals(default(ContextMenuButton))) return;

            int from = buttons.Buttons.IndexOf(button);
            if (from < 0) return;
            buttons.Buttons.RemoveAt(from);
            int to = Mathf.Clamp(newPosition, 0, buttons.Buttons.Count);
            buttons.Buttons.Insert(to, button);
        }

        public static void RemoveContextButton(ContextMenuOptionComponent buttons, string Identity)
        {
            if (buttons == null) return;

            ContextMenuButton button = buttons.Buttons.FirstOrDefault(c => c.Identity == Identity);

            if (!button.Equals(default(ContextMenuButton))) buttons.Buttons.Remove(button);
        }

        public static PhysicalProperties NewPhysicalProperties(PhysicalPropertiesController physicalPropertiesController, PhysicalBehaviour Phys)
        {
            PhysicalProperties customProp = ScriptableObject.Instantiate(Phys.Properties);
            customProp.name = "CustomProperties";

            if (physicalPropertiesController.ImmuneToBullets == true) { customProp.BulletSpeedAbsorptionPower = 0f; customProp.Brittleness = 1f; customProp.IsPoolable = false; }
            if (physicalPropertiesController.ImmuneToStabbing == true) customProp.Softness = 0f;
            if (physicalPropertiesController.ImmuneToFire == true) { customProp.Flammability = 0f; customProp.Burnrate = 0f; customProp.BurningTemperatureThreshold = 10000f; customProp.SizzleSounds = null; }
            if (physicalPropertiesController.ImmuneToElectricity == true) customProp.Conducting = false;
            if (physicalPropertiesController.Weight > 0.4f) customProp.HardImpact = customProp.SoftImpact;
            customProp.ImpactIntensityMutliplier = 0.25f / (physicalPropertiesController.Weight / 0.12f);
            customProp.HitVolumeMultiplier = 4f * (physicalPropertiesController.Weight / 0.12f);

            return customProp;
        }

        public class PhysicalPropertiesController
        {
            public bool ImmuneToFire { get; set; } = false;
            public bool ImmuneToElectricity { get; set; } = false;
            public bool ImmuneToStabbing { get; set; } = false;
            public bool ImmuneToBullets { get; set; } = false;
            public float Weight { get; set; } = 0.12f;
        }

    }
}
