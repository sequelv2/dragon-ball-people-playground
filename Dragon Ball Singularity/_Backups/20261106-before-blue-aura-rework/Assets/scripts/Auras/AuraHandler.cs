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
    public class AuraHandler : MonoBehaviour
    {
        public PersonBehaviour person;

        public AudioSource audioSource;
        private GameObject currentAura;
        private RefinedAuraVisual _refinedAura;
        private KiChargeModeController _cinematicChargeController;
        private GameObject _retiringAura;
        private Vector3 CompactAuraScale(Vector3 scale)
        {
            if(RefinedAuras.UsesOriginal(CurrentAuraID)) scale.x*=.88f;
            return scale;
        }
        private Vector3 baseScale = new Vector3(1.25f, 1.25f, 1f);
        private bool _perLimbAura;
        private bool _uiBodyAura;
        private bool _tuneMenuRegistered;

        public float AuraTuneScale = 3.4f;
        public float AuraTuneY = -0.09f;

        private float ExtraPosition = 0f;

        public string CurrentAuraID { get; set; } = "Base";
        public bool AuraActive { get; set; } = false;
        public bool AuraVisible { get; set; } = true;

        public bool HasSpawnedAuraInstance => currentAura != null;
        public GameObject CurrentAuraObject => currentAura;
        AllyKiReservoir _overdriveReservoir;
        // A separate contribution to the current visual, never a saved aura/charge setting.
        public float OverdriveVisualStrength
        {
            get
            {
                if(_overdriveReservoir==null)_overdriveReservoir=GetComponent<AllyKiReservoir>();
                if(!_overdriveReservoir || !_overdriveReservoir.isActiveAndEnabled || !_overdriveReservoir.OverdriveActive
                    || !_overdriveReservoir.Person || !_overdriveReservoir.Person.IsAlive())return 0;
                return Mathf.Clamp(.55f+_overdriveReservoir.StoredBars*.12f,.55f,1f);
            }
        }
        public bool IsAuraEffectivelyActive { get { return AuraActive || OverdriveVisualStrength>.001f; } }
        public bool IsAuraEffectivelyVisible { get { return DBPU.AurasEnabled && AuraVisible && IsAuraEffectivelyActive; } }

        private struct AuraParticleSpeedBackup
        {
            public ParticleSystem Ps;
            public float BaseSimulationSpeed;
            public float OverdriveEmissionDebt;
        }

        private readonly List<AuraParticleSpeedBackup> _auraParticleSpeedBackups = new List<AuraParticleSpeedBackup>();
        private Vector3 _auraVisualBaseScale = Vector3.one;
        private bool _chargeVisualIntensityActive;
        private bool _sparkingVisualIntensityActive;
        private bool _sparkingAuraStyleCached;
        private bool _hasCustomAuraColors;
        private Color _customAuraPrimary, _customAuraBright;

        private struct AuraSpriteColorBackup
        {
            public SpriteRenderer Renderer;
            public Color Color;
        }

        private struct AuraParticleColorBackup
        {
            public ParticleSystem Particle;
            public ParticleSystem.MinMaxGradient StartColor;
        }

        private readonly List<AuraSpriteColorBackup> _sparkingSpriteBackups = new List<AuraSpriteColorBackup>();
        private readonly List<AuraParticleColorBackup> _sparkingParticleColorBackups = new List<AuraParticleColorBackup>();

        private bool _kiChargeOutwardFadeRunning;
        private Coroutine _kiChargeOutwardFadeCo;
        private readonly List<Color> _kiChargeFadeSpriteColors = new List<Color>();

        void RegisterAuraTuneContextMenu()
        {
            if (_tuneMenuRegistered || person == null) return;
            Transform upper = transform.Find("Body/UpperBody");
            if (upper == null && person.transform != null)
                upper = person.transform.Find("Body/UpperBody");
            if (upper == null) return;
            var pb = upper.GetComponent<PhysicalBehaviour>();
            if (pb == null || pb.ContextMenuOptions == null) return;

            pb.ContextMenuOptions.Buttons.Add(new ContextMenuButton(
                "TuneAura",
                (Func<string>)(() => "Tune Aura Scale/Pos"),
                "Adjust aura scale and vertical position live",
                () => OpenAuraTuneMenu()));
            _tuneMenuRegistered = true;
        }

        public void OpenAuraTuneMenu()
        {
            PullGlobalAuraTune();
            DbpuConfigMenuUI.Open(
                "Aura Scale & Position",
                () => PushGlobalAuraTuneAndSave(),
                new DbpuMenuButton[]
                {
                    new DbpuMenuButton("Close", () =>
                    {
                        PushGlobalAuraTuneAndSave();
                        DbpuConfigMenuUI.Close();
                    }),
                    new DbpuMenuButton("Reset Defaults", () =>
                    {
                        AuraTuneScale = 3.4f;
                        AuraTuneY = -0.09f;
                        ApplyAuraTuneLive();
                        PushGlobalAuraTuneAndSave();
                        OpenAuraTuneMenu();
                    }),
                    new DbpuMenuButton("Notify Values", () =>
                    {
                        PushGlobalAuraTuneAndSave();
                        ModAPI.Notify("Aura Scale=" + AuraTuneScale.ToString("0.###") + "  Y=" + AuraTuneY.ToString("0.###"));
                    }),
                    new DbpuMenuButton("Type Scale...", () =>
                    {
                        DbpuConfigMenuUI.Hide();
                        Utils.OpenFloatInputDialog(AuraTuneScale, person, (PersonBehaviour p, float v) =>
                        {
                            AuraTuneScale = Mathf.Clamp(v, 0.1f, 40f);
                            ApplyAuraTuneLive();
                            PushGlobalAuraTuneAndSave();
                            OpenAuraTuneMenu();
                        }, "Aura Scale", "Default: 3.4");
                    }),
                    new DbpuMenuButton("Type Y Offset...", () =>
                    {
                        DbpuConfigMenuUI.Hide();
                        Utils.OpenFloatInputDialog(AuraTuneY, person, (PersonBehaviour p, float v) =>
                        {
                            AuraTuneY = Mathf.Clamp(v, -8f, 8f);
                            ApplyAuraTuneLive();
                            PushGlobalAuraTuneAndSave();
                            OpenAuraTuneMenu();
                        }, "Aura Y Offset", "Default: -0.09");
                    }),
                },
                new DbpuMenuSlider[]
                {
                    new DbpuMenuSlider("Scale", () => AuraTuneScale, v =>
                    {
                        AuraTuneScale = v;
                        ApplyAuraTuneLive();
                        PushGlobalAuraTuneAndSave();
                    }, 0.5f, 20f, false),
                    new DbpuMenuSlider("Y Offset", () => AuraTuneY, v =>
                    {
                        AuraTuneY = v;
                        ApplyAuraTuneLive();
                        PushGlobalAuraTuneAndSave();
                    }, -4f, 4f, false),
                },
                null);
        }

        public void PullGlobalAuraTune()
        {
            AuraTuneScale = Mathf.Max(0.1f, DBPU.UiAuraScale);
            AuraTuneY = DBPU.UiAuraYOffset;
        }

        public void PushGlobalAuraTuneAndSave()
        {
            DBPU.UiAuraScale = Mathf.Max(0.1f, AuraTuneScale);
            DBPU.UiAuraYOffset = AuraTuneY;
            if (ModConfigs.configurations == null)
                ModConfigs.configurations = new ModConfigs();
            ModConfigs.configurations.UiAuraScale = DBPU.UiAuraScale;
            ModConfigs.configurations.UiAuraYOffset = DBPU.UiAuraYOffset;
            ModConfigs.Save();

            var handlers = UnityEngine.Object.FindObjectsOfType<AuraHandler>();
            for (int i = 0; i < handlers.Length; i++)
            {
                var h = handlers[i];
                if (h == null || h == this) continue;
                h.PullGlobalAuraTune();
                h.ApplyAuraTuneLive();
            }
        }

        struct AuraParticleSizeBackup
        {
            public ParticleSystem Ps;
            public ParticleSystem.MinMaxCurve StartSize;
        }

        readonly List<AuraParticleSizeBackup> _auraParticleSizeBackups = new List<AuraParticleSizeBackup>();
        float _auraParticleSizeRefScale = 3.4f;

        void CaptureAuraParticleSizeBackups()
        {
            _auraParticleSizeBackups.Clear();
            _auraParticleSizeRefScale = Mathf.Max(0.1f, AuraTuneScale);
            if (currentAura == null) return;
            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                _auraParticleSizeBackups.Add(new AuraParticleSizeBackup
                {
                    Ps = ps,
                    StartSize = main.startSize
                });
            }
        }

        void ApplyAuraParticleSizeTune()
        {
            if (_auraParticleSizeBackups.Count == 0) return;
            float ratio = Mathf.Max(0.05f, AuraTuneScale / Mathf.Max(0.1f, _auraParticleSizeRefScale));
            for (int i = 0; i < _auraParticleSizeBackups.Count; i++)
            {
                var e = _auraParticleSizeBackups[i];
                if (e.Ps == null) continue;
                var main = e.Ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var src = e.StartSize;
                if (src.mode == ParticleSystemCurveMode.TwoConstants)
                    main.startSize = new ParticleSystem.MinMaxCurve(src.constantMin * ratio, src.constantMax * ratio);
                else if (src.mode == ParticleSystemCurveMode.Constant)
                    main.startSize = src.constant * ratio;
                else
                    main.startSize = src;
            }
        }

        public void ApplyAuraTuneLive()
        {
            if (currentAura == null || person == null) return;

            if (_refinedAura != null) { UpdateAuraPosition(); return; }
            float personScale = Mathf.Abs(person.transform.localScale.x);
            Vector3 scale;
            if (_uiBodyAura || CurrentAuraID == "UI" || CurrentAuraID == "MUI")
            {
                _uiBodyAura = true;
                float auraSize = Mathf.Max(0.1f, AuraTuneScale);
                scale = personScale > 1.15f
                    ? new Vector3(auraSize * (1f + (personScale - 1f)), auraSize * (1f + (personScale - 1f)), 1f)
                    : new Vector3(auraSize, auraSize, 1f);
                ExtraPosition = 0f;
            }
            else
            {
                float auraMult = personScale > 1.15f ? (1f + (personScale - 1f)) : 1f;
                float mul = Mathf.Max(0.1f, AuraTuneScale / 3.4f);
                scale = new Vector3(baseScale.x * auraMult * mul, baseScale.y * auraMult * mul, 1f);
                ExtraPosition = personScale > 1.15f ? Mathf.Max(0f, Mathf.Abs(scale.x) - 1.3f) : 0f;
            }

            currentAura.transform.localScale = CompactAuraScale(scale);
            _auraVisualBaseScale = currentAura.transform.localScale;
            currentAura.transform.position = GetAuraPosition();
            ApplyAuraParticleSizeTune();
        }

        private void RefreshAuraVisualCaches()
        {
            var previous=_auraParticleSpeedBackups.ToArray();
            _auraParticleSpeedBackups.Clear();
            if (currentAura == null) return;
            _auraVisualBaseScale = currentAura.transform.localScale;
            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var entry=new AuraParticleSpeedBackup {Ps=ps,BaseSimulationSpeed=ps.main.simulationSpeed};
                // Palette/tune refreshes may happen during charge or Overdrive. The same
                // emitter keeps its original speed baseline rather than caching our boost.
                for(int i=0;i<previous.Length;i++)if(previous[i].Ps==ps){entry=previous[i];break;}
                _auraParticleSpeedBackups.Add(entry);
            }
        }

        public void SetChargeVisualActive(bool on)
        {
            _chargeVisualIntensityActive = on;
        }

        public void SetSparkingPulse(bool on)
        {
            _sparkingVisualIntensityActive = on;
            if (!on)
                RestoreSparkingAuraStyle();
        }

        private void CacheSparkingAuraStyleIfNeeded()
        {
            if (_sparkingAuraStyleCached || currentAura == null) return;
            _sparkingSpriteBackups.Clear();
            _sparkingParticleColorBackups.Clear();

            foreach (var s in currentAura.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (s == null) continue;
                _sparkingSpriteBackups.Add(new AuraSpriteColorBackup { Renderer = s, Color = s.color });
            }

            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                _sparkingParticleColorBackups.Add(new AuraParticleColorBackup { Particle = ps, StartColor = main.startColor });
            }
            _sparkingAuraStyleCached = true;
        }

        private void RestoreSparkingAuraStyle()
        {
            if (!_sparkingAuraStyleCached) return;
            for (int i = 0; i < _sparkingSpriteBackups.Count; i++)
            {
                var b = _sparkingSpriteBackups[i];
                if (b.Renderer == null) continue;
                b.Renderer.color = b.Color;
            }
            for (int i = 0; i < _sparkingParticleColorBackups.Count; i++)
            {
                var b = _sparkingParticleColorBackups[i];
                if (b.Particle == null) continue;
                var main = b.Particle.main;
                main.startColor = b.StartColor;
            }
            _sparkingSpriteBackups.Clear();
            _sparkingParticleColorBackups.Clear();
            _sparkingAuraStyleCached = false;
        }

        private void ApplySparkingAuraStylePulse()
        {
            if (currentAura == null) return;
            CacheSparkingAuraStyleIfNeeded();

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.5f);
            Color bright = Color.Lerp(new Color(0.75f, 0.62f, 1f, 1f), new Color(0.92f, 0.95f, 1f, 1f), pulse);
            Color deep = Color.Lerp(new Color(0.35f, 0.12f, 0.58f, 1f), new Color(0.65f, 0.24f, 0.95f, 1f), pulse);

            for (int i = 0; i < _sparkingSpriteBackups.Count; i++)
            {
                var b = _sparkingSpriteBackups[i];
                if (b.Renderer == null) continue;
                float a = b.Color.a > 0.01f ? b.Color.a : 1f;
                Color tint = Color.Lerp(deep, bright, 0.58f);
                b.Renderer.color = new Color(tint.r, tint.g, tint.b, a);
            }

            for (int i = 0; i < _sparkingParticleColorBackups.Count; i++)
            {
                var b = _sparkingParticleColorBackups[i];
                if (b.Particle == null) continue;
                var main = b.Particle.main;
                main.startColor = new ParticleSystem.MinMaxGradient(deep, bright);
                main.simulationSpeed = Mathf.Max(0.01f, main.simulationSpeed) * (1.02f + pulse * 0.08f);
            }
        }

        public void CancelKiChargeOutwardFadeIfRunning()
        {
            if (_kiChargeOutwardFadeCo != null)
            {
                StopCoroutine(_kiChargeOutwardFadeCo);
                _kiChargeOutwardFadeCo = null;
            }
            _kiChargeOutwardFadeRunning = false;
            if (currentAura != null)
            {
                foreach (var s in currentAura.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (s == null) continue;
                    Color c = s.color;
                    s.color = new Color(c.r, c.g, c.b, 1f);
                }
                if (_refinedAura != null) _refinedAura.SetOpacity(1f);
                float sm = _refinedAura == null && _sparkingVisualIntensityActive ? 1.12f : 1f;
                currentAura.transform.localScale = _auraVisualBaseScale * sm;
                UpdateAuraPosition();
            }
            if (!_sparkingVisualIntensityActive)
                RestoreSparkingAuraStyle();
        }

        public void NotifyKiChargeReleased(bool deactivateAuraAfter, bool deactivateVisibleAfter)
        {
            SetChargeVisualActive(false);
            if(OverdriveVisualStrength>.001f)
            {
                // End only the charge controller's temporary request. The current
                // Overdrive contribution keeps the native aura alive independently.
                if(deactivateAuraAfter)AuraActive=false;
                if(deactivateVisibleAfter)AuraVisible=false;
                CancelKiChargeOutwardFadeIfRunning();
                if(currentAura)currentAura.SetActive(AuraVisible);
                return;
            }
            if (currentAura == null || !gameObject.activeInHierarchy)
            {
                if (deactivateAuraAfter)
                {
                    AuraActive = false;
                    if (deactivateVisibleAfter) AuraVisible = false;
                    if (currentAura != null) DestroyAura();
                }
                else if (DBPU.AurasEnabled && AuraActive && AuraVisible && gameObject.activeInHierarchy)
                {
                    string keepId = string.IsNullOrEmpty(CurrentAuraID) ? "Base" : CurrentAuraID;
                    CreateAura(keepId);
                }
                return;
            }
            if (!deactivateAuraAfter)
            {
                CancelKiChargeOutwardFadeIfRunning();
                return;
            }
            CancelKiChargeOutwardFadeIfRunning();
            _kiChargeOutwardFadeCo = StartCoroutine(KiChargeOutwardFadeRoutine(deactivateAuraAfter, deactivateVisibleAfter));
        }

        private IEnumerator KiChargeOutwardFadeRoutine(bool deactivateAuraAfter, bool deactivateVisibleAfter)
        {
            _kiChargeOutwardFadeRunning = true;
            GameObject auraGo = currentAura;
            if (auraGo == null)
            {
                _kiChargeOutwardFadeRunning = false;
                _kiChargeOutwardFadeCo = null;
                if (deactivateAuraAfter) AuraActive = false;
                if (deactivateVisibleAfter) AuraVisible = false;
                yield break;
            }

            Vector3 scaleAtRelease = auraGo.transform.localScale;
            const float dur = 0.52f;
            const float outwardMul = 1.42f;

            _kiChargeFadeSpriteColors.Clear();
            var sprites = auraGo.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
                if (s != null) _kiChargeFadeSpriteColors.Add(s.color);
            }

            var particles = auraGo.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps != null)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            float startVol = audioSource != null ? audioSource.volume : 1f;
            float elapsed = 0f;
            while (elapsed < dur && auraGo != null && currentAura == auraGo)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float sEase = u * u * (3f - 2f * u);
                auraGo.transform.position = GetAuraPosition();
                auraGo.transform.localScale = Vector3.Lerp(scaleAtRelease, scaleAtRelease * (_refinedAura != null ? 1f : outwardMul), sEase);
                float aMul = 1f - sEase;
                if (_refinedAura != null) _refinedAura.SetOpacity(aMul);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null) continue;
                    Color c = i < _kiChargeFadeSpriteColors.Count ? _kiChargeFadeSpriteColors[i] : sprites[i].color;
                    sprites[i].color = new Color(c.r, c.g, c.b, c.a * aMul);
                }
                if (deactivateAuraAfter && audioSource != null)
                    audioSource.volume = Mathf.Lerp(startVol, 0f, sEase);
                yield return null;
            }

            _kiChargeOutwardFadeRunning = false;
            _kiChargeOutwardFadeCo = null;

            if (deactivateAuraAfter)
            {
                AuraActive = false;
                if (deactivateVisibleAfter) AuraVisible = false;
                if (audioSource != null)
                {
                    audioSource.volume = startVol;
                    audioSource.Stop();
                }
                DestroyAura();
            }
            else if (auraGo != null && currentAura == auraGo)
            {
                if (audioSource != null)
                    audioSource.volume = startVol;
                auraGo.transform.position = GetAuraPosition();
                auraGo.transform.localScale = _auraVisualBaseScale;
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null) continue;
                    Color c = i < _kiChargeFadeSpriteColors.Count ? _kiChargeFadeSpriteColors[i] : Color.white;
                    sprites[i].color = c;
                }
                foreach (var ps in particles)
                {
                    if (ps != null) ps.Play();
                }
                RefreshAuraVisualCaches();
            }
        }

        private void Start()
        {
            if (person == null)
                person = GetComponent<PersonBehaviour>() ?? transform.root.GetComponent<PersonBehaviour>();
            if (person == null || person.Limbs == null || person.Limbs.Length < 3 || person.Limbs[2] == null)
                return;

            if (audioSource == null)
            {
                audioSource = new GameObject("AuraAudioSource").AddComponent<AudioSource>();
                audioSource.transform.SetParent(person.Limbs[2].transform, false);
                Global global = FindObjectOfType<Global>();
                if (global != null)
                {
                    global.AddAudioSource(audioSource, false);
                    audioSource.outputAudioMixerGroup = global.SoundEffects;
                }
                audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
                audioSource.spatialBlend = 1;
                audioSource.volume = 1;
                audioSource.maxDistance = 1000;
            }

            InitializeAuraDictionary();
            PullGlobalAuraTune();
            RegisterAuraTuneContextMenu();
        }

        private void InitializeAuraDictionary()
        {
            if (AurasStorage.AurasVFX != null && AurasStorage.AurasVFX.Count > 0) return;

            AurasStorage.AurasVFX["Base"] = DBPU.Aura_White;
            AurasStorage.AurasVFX["SSJ1"] = DBPU.Aura_SSJ1;
            AurasStorage.AurasVFX["SSJ2"] = DBPU.Aura_SSJ2;
            AurasStorage.AurasVFX["SSJ3"] = DBPU.Aura_SSJ3;
            AurasStorage.AurasVFX["SSJGod"] = DBPU.Aura_SSJG;
            AurasStorage.AurasVFX["SSJBlue"] = DBPU.Aura_SSJB;
            AurasStorage.AurasVFX["SSJBlueEvolution"] = DBPU.Aura_SSJBE;
            AurasStorage.AurasVFX["Mystic"] = DBPU.Aura_Mystic;
            AurasStorage.AurasVFX["BeastGohan"] = DBPU.Aura_Mystic;
            AurasStorage.AurasVFX["Frieza"] = DBPU.Aura_Frieza;
            AurasStorage.AurasVFX["GoldenFrieza"] = DBPU.Aura_GoldenFrieza;
            AurasStorage.AurasVFX["BlackFrieza"] = DBPU.Aura_Frieza != null ? DBPU.Aura_Frieza : DBPU.Aura_White;
            AurasStorage.AurasVFX["Broly"] = DBPU.Aura_Broly;
            AurasStorage.AurasVFX["PowerAwakened"] = DBPU.Aura_White;
            AurasStorage.AurasVFX["OrangePiccolo"] = DBPU.Aura_SSJ1;
            AurasStorage.AurasVFX["UI"] = DBPU.Aura_MUI != null ? DBPU.Aura_MUI : DBPU.Aura_White;
            AurasStorage.AurasVFX["MUI"] = DBPU.Aura_MUI != null ? DBPU.Aura_MUI : DBPU.Aura_White;
            AurasStorage.AurasVFX["UE"] = DBPU.Aura_SSJBE;
            AurasStorage.AurasVFX["SSJRose"] = DBPU.Aura_Frieza;
            AurasStorage.AurasVFX["Buu"] = DBPU.Aura_Frieza;
            AurasStorage.AurasVFX["Beerus"] = DBPU.Aura_SSJBE;
            AurasStorage.AurasVFX["Whis"] = DBPU.Aura_White;


            AurasStorage.AurasSFX["Base"] = DBPU.Aura_SSJ1_Loop;
            AurasStorage.AurasSFX["SSJ1"] = DBPU.Aura_SSJ1_Loop;
            AurasStorage.AurasSFX["SSJ2"] = DBPU.Aura_SSJ2_Loop;
            AurasStorage.AurasSFX["SSJ3"] = DBPU.Aura_SSJ2_Loop;
            AurasStorage.AurasSFX["SSJGod"] = DBPU.Aura_SSJGod_Loop;
            AurasStorage.AurasSFX["SSJBlue"] = DBPU.Aura_SSJBlue_Loop;
            AurasStorage.AurasSFX["SSJBlueEvolution"] = DBPU.Aura_SSJBlueEvolution_Loop;
            AurasStorage.AurasSFX["Mystic"] = DBPU.Aura_SSJ2_Loop;
            AurasStorage.AurasSFX["BeastGohan"] = DBPU.Aura_SSJ2_Loop;
            AurasStorage.AurasSFX["Frieza"] = DBPU.Aura_Frieza_Loop;
            AurasStorage.AurasSFX["GoldenFrieza"] = DBPU.Aura_GoldenFrieza_Loop;
            AurasStorage.AurasSFX["BlackFrieza"] = DBPU.Aura_Frieza_Loop != null ? DBPU.Aura_Frieza_Loop : DBPU.Aura_SSJ1_Loop;
            AurasStorage.AurasSFX["Broly"] = DBPU.Aura_Broly_Loop;
            AurasStorage.AurasSFX["PowerAwakened"] = DBPU.Aura_SSJ1_Loop;
            AurasStorage.AurasSFX["OrangePiccolo"] = DBPU.Aura_SSJ1_Loop;
            AurasStorage.AurasSFX["UI"] = DBPU.Aura_SSJGod_Loop;
            AurasStorage.AurasSFX["MUI"] = DBPU.Aura_SSJGod_Loop;
            AurasStorage.AurasSFX["UE"] = DBPU.Aura_SSJBlueEvolution_Loop;
            AurasStorage.AurasSFX["SSJRose"] = DBPU.Aura_Frieza_Loop;
            AurasStorage.AurasSFX["Buu"] = DBPU.Aura_Frieza_Loop;
            AurasStorage.AurasSFX["Beerus"] = DBPU.Aura_SSJBlueEvolution_Loop;
            AurasStorage.AurasSFX["Whis"] = DBPU.Aura_SSJGod_Loop;

        }

        private void Update()
        {
            if (currentAura != null && (DBPU.AurasEnabled == false || !IsAuraEffectivelyActive) && !_kiChargeOutwardFadeRunning)
            {
                DestroyAura();
            }

            if (currentAura == null && IsAuraEffectivelyVisible)
            {
                CreateAura(CurrentAuraID,OverdriveVisualStrength<=.001f);
            }
            if(currentAura && !_kiChargeOutwardFadeRunning && currentAura.activeSelf!=AuraVisible)currentAura.SetActive(AuraVisible);
        }

        private void LateUpdate()
        {
            if (currentAura == null || !IsAuraEffectivelyVisible) return;
            float fury=OverdriveVisualStrength;

            if(_cinematicChargeController==null)_cinematicChargeController=GetComponent<KiChargeModeController>();
            if(_cinematicChargeController!=null&&_cinematicChargeController.IsActivelyCharging)
                _chargeVisualIntensityActive=true;
            UpdateAuraPosition();
            if (_kiChargeOutwardFadeRunning) return;

            if (!_perLimbAura && _refinedAura == null)
            {
                float scaleMul = 1f;
                if (_chargeVisualIntensityActive || _sparkingVisualIntensityActive)
                    scaleMul *= DBPU.KiChargeAuraScaleMultiplier;
                if (_sparkingVisualIntensityActive)
                    scaleMul *= 1.12f;
                scaleMul*=1+fury*.24f;
                currentAura.transform.localScale = _auraVisualBaseScale * scaleMul;
            }

            float speedMul = (_chargeVisualIntensityActive || _sparkingVisualIntensityActive) ? DBPU.KiChargeAuraParticleSpeedMultiplier : 1f;
            if (_refinedAura != null)
            {
                _refinedAura.SpeedMultiplier = Mathf.Min(speedMul,1.75f);
                _refinedAura.ChargeAmount=(_chargeVisualIntensityActive||_sparkingVisualIntensityActive)?1f:0f;
                _refinedAura.OverdriveAmount=fury;
            }
            for (int i = 0; i < _auraParticleSpeedBackups.Count; i++)
            {
                var e = _auraParticleSpeedBackups[i];
                if (e.Ps == null) continue;
                var main = e.Ps.main;
                main.simulationSpeed = e.BaseSimulationSpeed * speedMul * (1+fury*1.7f);
                if((_refinedAura==null || !_refinedAura.IsCinematic) && fury>.001f && e.Ps.isPlaying && e.Ps.emission.enabled
                    && !(Global.main && Global.main.Paused))
                {
                    var emission=e.Ps.emission;
                    float sample=Mathf.Repeat(e.Ps.time/Mathf.Max(.1f,main.duration),1);
                    float rate=Mathf.Max(emission.rateOverTime.Evaluate(sample),Mathf.Min(16,main.maxParticles*.025f));
                    e.OverdriveEmissionDebt+=Mathf.Max(0,rate)*fury*1.25f*Time.deltaTime;
                    int extra=Mathf.Min(64,Mathf.FloorToInt(e.OverdriveEmissionDebt));
                    if(extra>0){e.Ps.Emit(extra);e.OverdriveEmissionDebt-=extra;}
                }
                else e.OverdriveEmissionDebt=0;
                _auraParticleSpeedBackups[i]=e;
            }

            if (_sparkingVisualIntensityActive)
                ApplySparkingAuraStylePulse();
        }

        public void CreateAura(string auraID)
        {
            CreateAura(auraID, true);
        }

        public void CreateAura(string auraID, bool playSound)
        {
            _hasCustomAuraColors = false;
            if (auraID != null && auraID.StartsWith("Naruto/", StringComparison.Ordinal)) { CurrentAuraID = auraID; if (currentAura != null) DestroyAura(); return; }
            if (person == null)
                person = GetComponent<PersonBehaviour>() ?? transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            if (KiFormTransition.DefersAura(person)) return;
            if (AuraVisible == true && IsAuraEffectivelyActive)
            {
                CurrentAuraID = auraID;

                if (currentAura != null)
                {
                    if (_refinedAura != null && RefinedAuras.Contains(auraID))
                    {
                        if (_retiringAura != null) Destroy(_retiringAura);
                        _retiringAura = currentAura;
                        _refinedAura.Retire(.24f, person.Limbs[2].transform);
                        currentAura = null;
                        _refinedAura = null;
                    }
                    DestroyAura();
                }

                bool isUiAura = auraID == "UI" || auraID == "MUI";
                if (isUiAura)
                    PullGlobalAuraTune();
                GameObject prefab = null;
                AurasStorage.AurasVFX.TryGetValue(auraID, out prefab);
                if (prefab == null && isUiAura)
                    prefab = DBPU.Aura_MUI;
                if (prefab == null && isUiAura)
                    AurasStorage.AurasVFX.TryGetValue("SSJBlue", out prefab);

                currentAura = RefinedAuras.Create(auraID);
                _refinedAura = currentAura != null ? currentAura.GetComponent<RefinedAuraVisual>() : null;
                if (_refinedAura != null)
                {
                    _perLimbAura = false;
                    _uiBodyAura = false;
                    currentAura.AddComponent<DestroyWithObject>().Object = person.gameObject;
                    UpdateAuraPosition();
                }
                else if (prefab != null)
                {
                    _perLimbAura = false;
                    _uiBodyAura = isUiAura;
                    ExtraPosition = 0f;
                    currentAura = Instantiate(prefab, GetAuraPosition(), Quaternion.identity);
                    float personScale = Mathf.Abs(person.transform.localScale.x);
                    float auraSize = isUiAura ? Mathf.Max(0.1f, AuraTuneScale) : 1.25f;
                    currentAura.transform.localScale = personScale > 1.15f
                        ? new Vector3(auraSize * (1f + (personScale - 1f)), auraSize * (1f + (personScale - 1f)), 1f)
                        : new Vector3(auraSize, auraSize, 1f);
                    if (!isUiAura)
                        ExtraPosition = personScale > 1.15f ? (Mathf.Abs(currentAura.transform.localScale.x) - 1.3f) : 0f;
                    currentAura.transform.localScale = CompactAuraScale(currentAura.transform.localScale);
                    currentAura.AddComponent<DestroyWithObject>().Object = person.gameObject;
                    ConfigureParticleSystems();

                    if (isUiAura)
                        ApplyUltraInstinctLimbAuraTint(auraID == "MUI");
                    else if (auraID == "PowerAwakened")
                        ApplyAuraTint(new Color(0.92f, 1f, 0.55f));
                    else if (auraID == "OrangePiccolo")
                        ApplyAuraTint(new Color(1f, 0.78f, 0.22f));
                    else if (auraID == "BeastGohan")
                        ApplyAuraTint(new Color(0.95f, 0.35f, 0.55f));
                    else if (auraID == "BlackFrieza")
                        ApplyCustomAuraColors(new Color(0.35f, 0.08f, 0.85f), new Color(0.72f, 0.32f, 1f), true, 1f);

                    CaptureAuraParticleSizeBackups();
                    ApplyAuraParticleSizeTune();
                }

                if (playSound && audioSource != null && AurasStorage.AurasSFX.TryGetValue(auraID, out AudioClip sound))
                {
                    audioSource.Stop();
                    audioSource.PlayOneShot(DBPU.Aura_Burst_Basic);
                    audioSource.clip = sound;
                    audioSource.loop = true;
                    audioSource.Play();
                }
                else if (playSound)
                {
                    Debug.LogWarning($"Aura {auraID} not found in registry");
                }

                RefreshAuraVisualCaches();
            }
        }

        static float GetLimbAuraScale(string limbName)
        {
            if (string.IsNullOrEmpty(limbName)) return 0.24f;
            if (limbName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0) return 0.34f;
            if (limbName.IndexOf("UpperBody", StringComparison.OrdinalIgnoreCase) >= 0) return 0.4f;
            if (limbName.IndexOf("MiddleBody", StringComparison.OrdinalIgnoreCase) >= 0) return 0.42f;
            if (limbName.IndexOf("LowerBody", StringComparison.OrdinalIgnoreCase) >= 0) return 0.36f;
            if (limbName.IndexOf("UpperArm", StringComparison.OrdinalIgnoreCase) >= 0) return 0.26f;
            if (limbName.IndexOf("LowerArm", StringComparison.OrdinalIgnoreCase) >= 0) return 0.24f;
            if (limbName.IndexOf("UpperLeg", StringComparison.OrdinalIgnoreCase) >= 0) return 0.28f;
            if (limbName.IndexOf("LowerLeg", StringComparison.OrdinalIgnoreCase) >= 0) return 0.26f;
            if (limbName.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) >= 0) return 0.2f;
            return 0.24f;
        }

        void ApplyUltraInstinctLimbAuraTint(bool mastered)
        {
            if (currentAura == null) return;
            Color deep = mastered ? new Color(0.12f, 0.38f, 0.92f) : new Color(0.1f, 0.32f, 0.88f);
            Color mid = mastered ? new Color(0.38f, 0.72f, 1f) : new Color(0.28f, 0.62f, 1f);
            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(deep, mid);
                main.simulationSpeed = mastered ? 1.12f : 1.02f;
            }
        }

        public void DestroyAura()
        {
            _refinedAura = null;
            if (audioSource != null)
                audioSource.Stop();

            if (currentAura != null)
            {
                Destroy(currentAura);
                currentAura = null;
            }

            _perLimbAura = false;
            _uiBodyAura = false;
            _chargeVisualIntensityActive = false;
            _sparkingVisualIntensityActive = false;
            RestoreSparkingAuraStyle();
            _auraParticleSpeedBackups.Clear();
            _auraParticleSizeBackups.Clear();
        }

        private Vector3 GetAuraPosition()
        {
            if (person == null || person.Limbs == null || person.Limbs.Length < 3 || person.Limbs[2] == null)
                return transform.position;
            if (_refinedAura != null && _refinedAura.IsRedux)
                return person.Limbs[2].transform.position + new Vector3(0f, 0.4f * Mathf.Abs(person.transform.localScale.x), 0f); // Rooted at the feet: the Y tune does not apply.
            if (_refinedAura != null)
                return person.Limbs[2].transform.position + new Vector3(0f,
                    0.4f * Mathf.Abs(person.transform.localScale.x) + AuraTuneY + 0.09f, 0f);
            if (_uiBodyAura)
                return person.Limbs[2].transform.position + new Vector3(0f, AuraTuneY, 0f);
            return person.Limbs[2].transform.position + new Vector3(0f, -1.1f - ExtraPosition + (AuraTuneY - (-0.09f)), 0f);
        }

        private void UpdateAuraPosition()
        {
            if (currentAura == null) return;
            if (_refinedAura != null)
            {
                _refinedAura.BindPerson(person);
                float s = Mathf.Max(0.01f, Mathf.Abs(person.transform.localScale.x));
                float tune = Mathf.Max(0.1f, AuraTuneScale / 3.4f);
                // Native rig smooths charge; grow from the original footprint without accumulating scale.
                _auraVisualBaseScale = new Vector3(1.25f * s * tune, 1.25f * s * tune, 1f);
                float charge=_refinedAura.SmoothedCharge;
                if (_refinedAura.IsRedux)
                {
                    currentAura.transform.localScale = _auraVisualBaseScale;
                    currentAura.transform.position = GetAuraPosition();
                    currentAura.transform.rotation = Quaternion.identity;
                    return;
                }
                float grow=Mathf.Clamp(DBPU.KiChargeAuraScaleMultiplier,1f,3f)-1f;
                float fury=OverdriveVisualStrength;
                currentAura.transform.localScale = new Vector3(_auraVisualBaseScale.x*(1+grow*1.52f*charge)*(1+fury*.15f),_auraVisualBaseScale.y*(1+grow*1.12f*charge)*(1+fury*.24f),1);
                currentAura.transform.position = GetAuraPosition();
                currentAura.transform.rotation = Quaternion.identity;
                return;
            }
            if (_perLimbAura)
            {
                float personScale = person != null ? Mathf.Abs(person.transform.localScale.x) : 1f;
                float scaleMul = personScale > 1.15f ? personScale : 1f;
                for (int i = 0; i < currentAura.transform.childCount; i++)
                {
                    var child = currentAura.transform.GetChild(i);
                    if (child == null) continue;
                    var follow = child.GetComponent<LimbAuraFollow>();
                    string limbName = follow != null && follow.Target != null ? follow.Target.name : child.name;
                    float local = GetLimbAuraScale(limbName) * scaleMul;
                    if (_chargeVisualIntensityActive || _sparkingVisualIntensityActive)
                        local *= DBPU.KiChargeAuraScaleMultiplier;
                    if (_sparkingVisualIntensityActive)
                        local *= 1.12f;
                    local*=1+OverdriveVisualStrength*.24f;
                    child.localScale = new Vector3(local, local, 1f);
                }
                return;
            }

            float personScale2 = Mathf.Abs(person.transform.localScale.x);
            float auraMult = personScale2 > 1.15f ? (1f + (personScale2 - 1f)) : 1f;
            if (_uiBodyAura)
            {
                ExtraPosition = 0f;
                float auraSize = Mathf.Max(0.1f, AuraTuneScale);
                _auraVisualBaseScale = personScale2 > 1.15f
                    ? new Vector3(auraSize * (1f + (personScale2 - 1f)), auraSize * (1f + (personScale2 - 1f)), 1f)
                    : new Vector3(auraSize, auraSize, 1f);
            }
            else
            {
                float mul = Mathf.Max(0.1f, AuraTuneScale / 3.4f);
                currentAura.transform.localScale = new Vector3(baseScale.x * auraMult * mul, baseScale.y * auraMult * mul, 1f);
                _auraVisualBaseScale = currentAura.transform.localScale;
                ExtraPosition = personScale2 > 1.15f ? Mathf.Max(0f, Mathf.Abs(currentAura.transform.localScale.x) - 1.3f) : 0f;
                currentAura.transform.localScale = CompactAuraScale(currentAura.transform.localScale);
                _auraVisualBaseScale = currentAura.transform.localScale;
            }
            currentAura.transform.position = GetAuraPosition();
        }

        private void ConfigureParticleSystems()
        {
            if (currentAura == null) return;
            foreach (var lightning in currentAura.GetComponentsInChildren<ParticleSystem>().Where(x => x.name == "lightning"))
            {
                lightning.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Top";
            }

            foreach (var spark in currentAura.GetComponentsInChildren<ParticleSystem>().Where(x => x.name == "spark"))
            {
                spark.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Top";
            }

            float auraLift = _uiBodyAura ? 0.55f : 1.4f;
            foreach (var aura in currentAura.transform.Cast<Transform>().Where(x => x.name == "aura"))
            {
                var inner = aura.Find("aura");
                if (inner == null) continue;
                inner.localEulerAngles = new Vector3(180, 0, 0);
                inner.localPosition = new Vector3(0, auraLift, 0);

                foreach (var outer in inner.Cast<Transform>().Where(x => x.name == "outer"))
                {
                    outer.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Background";
                }
            }

            if (_uiBodyAura)
            {
                foreach (var aura in currentAura.GetComponentsInChildren<Transform>(true))
                {
                    if (aura == null || aura.name != "aura") continue;
                    var inner = aura.Find("aura");
                    if (inner == null) continue;
                    inner.localEulerAngles = new Vector3(180, 0, 0);
                    inner.localPosition = new Vector3(0f, 0.55f, 0f);
                }
            }

            if (_perLimbAura)
            {
                foreach (Transform piece in currentAura.transform)
                {
                    foreach (var aura in piece.Cast<Transform>().Where(x => x.name == "aura"))
                    {
                        var inner = aura.Find("aura");
                        if (inner == null) continue;
                        inner.localEulerAngles = new Vector3(180, 0, 0);
                        inner.localPosition = new Vector3(0, 0.35f, 0);
                    }
                }
            }
        }

        private void ApplyAuraTint(Color tint)
        {
            if (currentAura == null) return;
            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;

                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(main.startColor.color * tint);
                }

                else if (main.startColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(main.startColor.colorMin * tint, main.startColor.colorMax * tint);
                }
            }
        }

        // The palette of the selected aura, also while it is hidden; custom colours win over the form's own.
        public void GetAuraColors(out Color deep, out Color mid, out Color light)
        {
            if (_hasCustomAuraColors)
            {
                deep = _customAuraPrimary * 0.45f;
                mid = _customAuraPrimary;
                light = _customAuraBright;
                return;
            }
            var look = RefinedAuras.Look(CurrentAuraID);
            deep = look.Deep;
            mid = look.Mid;
            light = look.Light;
        }

        public void ApplyCustomAuraColors(Color primary, Color secondary, bool useSecondary, float scaleMultiplier)
        {
            if (currentAura == null) return;

            Color bright = useSecondary ? secondary : Color.Lerp(primary, Color.white, 0.35f);
            _hasCustomAuraColors = true;
            _customAuraPrimary = primary;
            _customAuraBright = bright;
            if (_refinedAura != null) _refinedAura.SetTint(primary, bright);
            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(primary, bright);
            }

            foreach (var sr in currentAura.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                float a = sr.color.a > 0.01f ? sr.color.a : 1f;
                sr.color = new Color(primary.r, primary.g, primary.b, a);
            }

            if (scaleMultiplier > 0.01f)
                SetAuraScale(scaleMultiplier);

            RefreshAuraVisualCaches();
        }

        public void SetAuraScale(float scaleMultiplier)
        {
            if (currentAura == null) return;
            if (_refinedAura != null) { UpdateAuraPosition(); return; }
            if (_perLimbAura)
            {
                float personScale = person != null ? Mathf.Abs(person.transform.localScale.x) : 1f;
                float scaleMul = personScale > 1.15f ? personScale : 1f;
                for (int i = 0; i < currentAura.transform.childCount; i++)
                {
                    var child = currentAura.transform.GetChild(i);
                    if (child == null) continue;
                    var follow = child.GetComponent<LimbAuraFollow>();
                    string limbName = follow != null && follow.Target != null ? follow.Target.name : child.name;
                    float local = GetLimbAuraScale(limbName) * scaleMul * Mathf.Max(0.01f, scaleMultiplier);
                    child.localScale = new Vector3(local, local, 1f);
                }
                return;
            }

            currentAura.transform.localScale = CompactAuraScale(baseScale * scaleMultiplier);
        }

        public void ToggleAuraVisibility(bool visible)
        {
            AuraVisible = visible;
            if (currentAura != null)
            {
                currentAura.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            if (_retiringAura != null) Destroy(_retiringAura);
            if (_refinedAura != null) DestroyAura();
        }

        private void OnDestroy()
        {
            DestroyAura();
        }

        public class LimbAuraFollow : MonoBehaviour
        {
            public Transform Target;

            void LateUpdate()
            {
                if (Target == null)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position = Target.position;
                transform.rotation = Quaternion.identity;
            }
        }
    }
}
