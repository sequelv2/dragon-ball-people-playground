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
    [DefaultExecutionOrder(-140)]
    public class KiChargeModeController : MonoBehaviour
    {
        public bool ModeEnabled { get => DBPU.KiChargeFxEnabled; set => DBPU.KiChargeFxEnabled = value; }
        public bool IsActivelyCharging { get; private set; }

        private const string KiChargeInAnimationRelativePath = "/Assets/Animations/kichargein/kichargein.json";
        private const string KiChargeOutAnimationRelativePath = "/Assets/Animations/kichargeoutt/kichargeoutt.json";

        private static Sprite _kiChargeBlackSquareSprite;
        private static Material _kiChargeDebrisMaterial;
        private static float _kiChargeInDuration = 0.7f;
        private static int _kiChargeInFrameCount = 1;

        private struct HoldLimbPose
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private struct ChargeLimbRbState
        {
            public Rigidbody2D Rigidbody;
            public RigidbodyType2D BodyType;
            public RigidbodyConstraints2D Constraints;
            public float GravityScale;
            public bool Simulated;
            public Vector2 Velocity;
            public float AngularVelocity;
        }

        private static void EnsureKiChargeDebrisAssets()
        {
            if (_kiChargeBlackSquareSprite != null && _kiChargeDebrisMaterial != null) return;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var cols = new Color[64];
            for (int i = 0; i < cols.Length; i++)
                cols[i] = Color.black;
            tex.SetPixels(cols);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _kiChargeBlackSquareSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            _kiChargeDebrisMaterial = new Material(ModAPI.FindMaterial("Sprites-Default"));
        }

        private KiSystem kiSystem;
        private PersonBehaviour person;
        private AuraHandler auraHandler;
        private Transform spawnAnchor;
        private Coroutine debrisCoroutine;
        private bool debrisSpawning;
        private readonly List<GameObject> chargeBlocks = new List<GameObject>();
        private int totalBlocksSpawned;
        private bool pendingHold;
        private float _sparkingPrepAccum;
        private bool _kiChargeForcedAura;
        private bool _kiChargeForcedAuraVisible;
        private float _autoTransformTierAccum;
        private bool _holdChargePoseActive;
        private bool _hasCapturedHoldPose;
        private HoldLimbPose[] _holdChargePose;
        private Coroutine _chargeIntroPoseCo;
        private readonly List<ChargeLimbRbState> _chargeLimbRbStates = new List<ChargeLimbRbState>();
        private Coroutine _releaseFreezeCo;

        private static string GetKiChargeInAnimationPath()
        {
            return DBPU.ModFolder + KiChargeInAnimationRelativePath;
        }

        private static string GetKiChargeOutAnimationPath()
        {
            return DBPU.ModFolder + KiChargeOutAnimationRelativePath;
        }

        private static bool CanPlayChargeAnimationsNow()
        {
            if (DBPU.AFWEnabled) return true;
            return GameObject.Find("AnimationFrameworkManager") != null;
        }

        private static void EnsureKiChargeAnimationMeta()
        {
            // Keep a safe fallback for timing and frame index. We still capture the last frame
            // through onFrame callback, so this value is only used as a guard timer.
            _kiChargeInDuration = 0.7f;
            _kiChargeInFrameCount = 1;
        }

        private void CaptureCurrentLimbPose()
        {
            if (person == null || person.Limbs == null || person.Limbs.Count() == 0) return;

            int count = person.Limbs.Count();
            if (_holdChargePose == null || _holdChargePose.Length != count)
                _holdChargePose = new HoldLimbPose[count];

            for (int i = 0; i < count; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                _holdChargePose[i].Position = limb.transform.position;
                _holdChargePose[i].Rotation = limb.transform.rotation;
            }

            _hasCapturedHoldPose = true;
        }

        private IEnumerator ActivateHoldPoseAfterIntro()
        {
            EnsureKiChargeAnimationMeta();
            yield return new WaitForSeconds(Mathf.Max(0.05f, _kiChargeInDuration));

            if (!IsActivelyCharging) yield break;
            if (!_hasCapturedHoldPose)
                CaptureCurrentLimbPose();

            _holdChargePoseActive = _hasCapturedHoldPose;
            _chargeIntroPoseCo = null;
        }

        private void ReleaseHoldChargePose()
        {
            _holdChargePoseActive = false;
            _hasCapturedHoldPose = false;
            if (_chargeIntroPoseCo != null)
            {
                StopCoroutine(_chargeIntroPoseCo);
                _chargeIntroPoseCo = null;
            }
        }

        private void ApplyChargeFreeze()
        {
            if (person == null || person.Limbs == null) return;
            if (_releaseFreezeCo != null)
            {
                StopCoroutine(_releaseFreezeCo);
                _releaseFreezeCo = null;
            }

            _chargeLimbRbStates.RemoveAll(s => s.Rigidbody == null);
            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                var rb = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.rigidbody : limb.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                bool alreadyLocked = false;
                for (int i = 0; i < _chargeLimbRbStates.Count; i++)
                {
                    if (_chargeLimbRbStates[i].Rigidbody == rb)
                    {
                        alreadyLocked = true;
                        break;
                    }
                }
                if (alreadyLocked) continue;

                _chargeLimbRbStates.Add(new ChargeLimbRbState
                {
                    Rigidbody = rb,
                    BodyType = rb.bodyType,
                    Constraints = rb.constraints,
                    GravityScale = rb.gravityScale,
                    Simulated = rb.simulated,
                    Velocity = rb.velocity,
                    AngularVelocity = rb.angularVelocity
                });

                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }

        private void ClearChargeFreezeImmediate()
        {
            foreach (var state in _chargeLimbRbStates)
            {
                var rb = state.Rigidbody;
                if (rb == null) continue;
                rb.bodyType = state.BodyType;
                rb.constraints = state.Constraints;
                rb.gravityScale = state.GravityScale;
                rb.simulated = state.Simulated;
                rb.velocity = state.Velocity;
                rb.angularVelocity = state.AngularVelocity;
            }
            _chargeLimbRbStates.Clear();
        }

        private IEnumerator ReleaseChargeFreezeAfter(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            ClearChargeFreezeImmediate();
            _releaseFreezeCo = null;
        }

        void Awake()
        {
            kiSystem = GetComponent<KiSystem>();
            person = GetComponent<PersonBehaviour>();
            auraHandler = GetComponent<AuraHandler>();
        }

        void Start()
        {
            if (person != null && person.Limbs.Count() > 2)
                spawnAnchor = person.Limbs[2].transform;
            else if (person != null && person.Limbs.Count() > 0)
                spawnAnchor = person.transform;
        }

        public void SetHoldDesired(bool hold)
        {
            pendingHold = hold;
        }

        public void ForceEndCharge(bool preserveAuraForSparkingOverload = false)
        {
            if (IsActivelyCharging)
                EndCharge(preserveAuraForSparkingOverload);
        }

        void Update()
        {
            if (kiSystem == null || person == null) return;
            if (!person.IsAlive())
            {
                if (IsActivelyCharging) EndCharge();
                _sparkingPrepAccum = 0f;
                _autoTransformTierAccum = 0f;
                if (kiSystem != null) kiSystem.SparkingOvercharge01 = 0f;
                return;
            }

            var spark = GetComponent<KiSparkingController>();
            bool sparkOverload = spark != null && spark.CurrentPhase == KiSparkingController.SparkPhase.Overload;

            if (sparkOverload && IsActivelyCharging)
                EndCharge();

            bool want = pendingHold && DBPU.KiChargeFxEnabled && !sparkOverload;

            if (kiSystem.currentKi < kiSystem.maxKi * 0.995f)
            {
                _sparkingPrepAccum = 0f;
                _autoTransformTierAccum = 0f;
                kiSystem.SparkingOvercharge01 = 0f;
            }

            if (want && !IsActivelyCharging)
                BeginCharge();
            else if (!want && IsActivelyCharging)
                EndCharge();

            if (want && IsActivelyCharging && kiSystem.currentKi >= kiSystem.maxKi * 0.995f)
            {
                if (DBPU.SparkingEnabled && DBPU.KiChargeSparkingOverchargeEnabled)
                {
                    float dur = DBPU.SparkingOverchargeHoldSeconds > 0.05f ? DBPU.SparkingOverchargeHoldSeconds : 3f;
                    _sparkingPrepAccum += Time.deltaTime / dur;
                    if (_sparkingPrepAccum >= 1f)
                    {
                        _sparkingPrepAccum = 0f;
                        kiSystem.SparkingOvercharge01 = 0f;
                        spark?.TryStartOverload();
                        EndCharge();
                    }
                    else
                        kiSystem.SparkingOvercharge01 = Mathf.Clamp01(_sparkingPrepAccum);
                }
                else
                {
                    _sparkingPrepAccum = 0f;
                    kiSystem.SparkingOvercharge01 = 0f;
                }

                if (DBPU.KiChargeAutoTransformWhileChargingEnabled)
                    TryProgressAutoTransformCharge(Time.deltaTime);
                else
                    _autoTransformTierAccum = 0f;
            }
            else if (!want)
            {
                _sparkingPrepAccum = 0f;
                _autoTransformTierAccum = 0f;
                kiSystem.SparkingOvercharge01 = 0f;
            }
        }

        void LateUpdate()
        {
            if (!_holdChargePoseActive || !IsActivelyCharging || person == null || _holdChargePose == null) return;

            int count = Mathf.Min(person.Limbs.Count(), _holdChargePose.Length);
            for (int i = 0; i < count; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;

                limb.transform.position = _holdChargePose[i].Position;
                limb.transform.rotation = _holdChargePose[i].Rotation;

                if (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.rigidbody != null)
                {
                    limb.PhysicalBehaviour.rigidbody.velocity = Vector2.zero;
                    limb.PhysicalBehaviour.rigidbody.angularVelocity = 0f;
                }
            }
        }

        /// <summary>Steps transformations by ascending PowerMultiplier (same idea as transformation menu list order).</summary>
        private void TryProgressAutoTransformCharge(float dt)
        {
            float need = Mathf.Max(1f, DBPU.KiChargeAutoTransformSecondsPerTier);
            _autoTransformTierAccum += dt;
            while (_autoTransformTierAccum >= need)
            {
                if (!TryStepTransformationUpOneTier())
                {
                    _autoTransformTierAccum = 0f;
                    break;
                }
                _autoTransformTierAccum -= need;
            }
        }

        private bool TryStepTransformationUpOneTier()
        {
            var ct = GetComponent<CharacterTransformations>();
            if (ct == null || person == null || person.Limbs.Count() < 1 || kiSystem == null) return false;
            var tm = person.Limbs[0].GetComponent<TransformationContextMenu>();
            if (tm == null || tm.TransformationsData == null) return false;

            var data = tm.TransformationsData.transformations.TransformationData;
            if (data == null || data.Count < 2) return false;

            var ordered = data.OrderBy(kvp => kvp.Value.PowerMultiplier).ThenBy(kvp => kvp.Key).Select(kvp => kvp.Key).ToList();

            string cur = kiSystem.CurrentForm;
            if (string.IsNullOrEmpty(cur) || !data.ContainsKey(cur))
                cur = tm.Form;
            if (string.IsNullOrEmpty(cur) || !data.ContainsKey(cur))
                cur = ordered[0];

            int idx = ordered.IndexOf(cur);
            if (idx < 0 || idx >= ordered.Count - 1) return false;

            string nextKey = ordered[idx + 1];
            var nextStats = data[nextKey];
            float prevMul = tm.PreviousMultiplier;
            float nextMul = tm.GetStatsEffectivePower(nextStats);

            Action applyTier = () =>
            {
            ct.ApplyTransformation(person, nextKey);

            tm.Form = nextStats.ID;
            tm.CurrentAuraID = nextStats.AuraID;
            foreach (var limb in person.Limbs)
            {
                if (!float.IsInfinity(limb.InitialHealth * (nextMul / prevMul)))
                    limb.InitialHealth *= nextMul / prevMul;
                if (!float.IsInfinity(limb.Health * (nextMul / prevMul)))
                    limb.Health *= nextMul / prevMul;
            }
            person.ChangeWeightBasedOnHealth();
            CombatTransformationWheel.Instance.ShowFormTitle(tm, nextKey);
            tm.PreviousMultiplier = nextMul;

            if (tm.CurrentAuraID != null && !DBPU.BaseConsideredForms.Contains(tm.Form))
            {
                if (tm.AuraHandler != null)
                {
                    tm.AuraHandler.AuraActive = DBPU.AurasEnabled;
                    tm.AuraHandler.CreateAura(tm.CurrentAuraID);
                }
            }
            else if (tm.AuraHandler != null)
                tm.AuraHandler.DestroyAura();

            };
            if (!KiFormTransition.Begin(ct, person, cur, nextKey, applyTier)) applyTier();

            return true;
        }

        private void BeginCharge()
        {
            if (IsActivelyCharging) return;
            IsActivelyCharging = true;
            kiSystem.KiChargeActiveForRegen = true;

            if (DBPU.AurasEnabled && person != null && person.Limbs.Count() > 2)
            {
                auraHandler = auraHandler != null ? auraHandler : GetComponent<AuraHandler>();
                if (auraHandler == null)
                {
                    auraHandler = gameObject.AddComponent<AuraHandler>();
                    auraHandler.person = person;
                }
                if (auraHandler != null && kiSystem != null)
                {
                    auraHandler.CancelKiChargeOutwardFadeIfRunning();
                    auraHandler.CurrentAuraID = CinematicAssets.ResolveAura(person,kiSystem.CurrentForm,auraHandler.CurrentAuraID);
                    if (!auraHandler.AuraVisible)
                    {
                        auraHandler.AuraVisible = true;
                        _kiChargeForcedAuraVisible = true;
                    }
                    if (!auraHandler.AuraActive)
                    {
                        auraHandler.AuraActive = true;
                        _kiChargeForcedAura = true;
                    }
                    if (!auraHandler.HasSpawnedAuraInstance)
                        auraHandler.CreateAura(auraHandler.CurrentAuraID);
                    auraHandler.SetChargeVisualActive(true);
                }
            }
            if (debrisCoroutine == null && spawnAnchor != null)
                debrisCoroutine = StartCoroutine(SpawnDebrisLoop());
        }

        private void EndCharge(bool preserveAuraForSparkingOverload = false)
        {
            if (!IsActivelyCharging) return;
            IsActivelyCharging = false;
            kiSystem.KiChargeActiveForRegen = false;
            _sparkingPrepAccum = 0f;
            if (kiSystem != null) kiSystem.SparkingOvercharge01 = 0f;

            if (auraHandler != null)
            {
                if (preserveAuraForSparkingOverload)
                {
                    auraHandler.CancelKiChargeOutwardFadeIfRunning();
                    auraHandler.SetChargeVisualActive(true);
                    _kiChargeForcedAura = false;
                    _kiChargeForcedAuraVisible = false;
                    }
                else
                {
                    bool defAura = _kiChargeForcedAura;
                    bool defVis = _kiChargeForcedAuraVisible;
                    _kiChargeForcedAura = false;
                    _kiChargeForcedAuraVisible = false;
                    auraHandler.NotifyKiChargeReleased(defAura, defVis);
                    }
            }
            if (debrisCoroutine != null)
            {
                StopCoroutine(debrisCoroutine);
                debrisCoroutine = null;
            }
            StopDebrisFadeOut();
        }

        private IEnumerator SpawnDebrisLoop()
        {
            debrisSpawning = true;
            totalBlocksSpawned = 0;
            int maxActive = Mathf.Max(1, DBPU.KiChargeDebrisMaxActive);
            int maxSession = Mathf.Max(1, DBPU.KiChargeDebrisMaxSession);
            float interval = Mathf.Max(0.1f, DBPU.KiChargeDebrisSpawnInterval);

            EnsureKiChargeDebrisAssets();

            while (IsActivelyCharging && totalBlocksSpawned < maxSession)
            {
                chargeBlocks.RemoveAll(b => b == null);
                if (chargeBlocks.Count >= maxActive)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                float ringMin = Mathf.Max(0.08f, DBPU.KiChargeDebrisRingMin);
                float ringMax = Mathf.Max(ringMin + 0.08f, DBPU.KiChargeDebrisRingMax);
                float yOff = DBPU.KiChargeDebrisSpawnYOffset;

                int batch = UnityEngine.Random.Range(2, 4);
                for (int i = 0; i < batch && totalBlocksSpawned < maxSession && chargeBlocks.Count < maxActive; i++)
                {
                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = UnityEngine.Random.Range(ringMin, ringMax);
                    Vector3 startPosition = spawnAnchor.position + new Vector3(Mathf.Cos(angle) * distance, yOff, Mathf.Sin(angle) * distance);
                    startPosition.z = 0f;

                    var chargeBlock = new GameObject("KiChargeDebris");
                    chargeBlock.transform.position = startPosition;
                    chargeBlock.layer = 0;
                    chargeBlock.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.14f, 0.38f);
                    var sr = chargeBlock.AddComponent<SpriteRenderer>();
                    sr.sprite = _kiChargeBlackSquareSprite;
                    sr.sharedMaterial = _kiChargeDebrisMaterial;
                    sr.color = Color.black;
                    sr.sortingLayerName = "Default";
                    sr.sortingOrder = 5;
                    if (UnityEngine.Random.value < 0.7f)
                        chargeBlock.transform.eulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-35f, 35f));

                    var rising = chargeBlock.AddComponent<KiChargeRisingBlock>();
                    rising.targetHeight = UnityEngine.Random.Range(1.4f, 2.9f);
                    rising.riseDuration = UnityEngine.Random.Range(1.35f, 2.15f);
                    rising.rotationSpeed = UnityEngine.Random.Range(-20f, 20f);

                    chargeBlocks.Add(chargeBlock);
                    totalBlocksSpawned++;
                }
                yield return new WaitForSeconds(interval);
            }
            debrisCoroutine = null;
        }

        private void StopDebrisFadeOut()
        {
            foreach (var block in chargeBlocks.ToList())
            {
                if (block != null)
                    StartCoroutine(FadeOutBlock(block));
            }
            chargeBlocks.Clear();
            totalBlocksSpawned = 0;
        }

        private IEnumerator FadeOutBlock(GameObject block)
        {
            var renderer = block != null ? block.GetComponent<SpriteRenderer>() : null;
            if (renderer == null)
            {
                if (block != null) Destroy(block);
                yield break;
            }
            Color originalColor = renderer.color;
            float fadeTime = 1f;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(originalColor.a, 0f, elapsed / fadeTime);
                renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
            Destroy(block);
        }

        void OnDestroy()
        {
            if (kiSystem != null)
                kiSystem.KiChargeActiveForRegen = false;
            if (auraHandler != null)
            {
                if (_kiChargeForcedAura)
                {
                    auraHandler.AuraActive = false;
                    _kiChargeForcedAura = false;
                }
                if (_kiChargeForcedAuraVisible)
                {
                    auraHandler.AuraVisible = false;
                    _kiChargeForcedAuraVisible = false;
                }
            }
            foreach (var b in chargeBlocks)
                if (b != null) Destroy(b);
            chargeBlocks.Clear();
        }
    }
}
