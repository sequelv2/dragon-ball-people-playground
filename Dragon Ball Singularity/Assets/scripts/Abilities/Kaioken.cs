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
    public class Kaioken : MonoBehaviour
    {
        public MultipliersAndStuffs Multipliers;
        public KiSystem KiSys;
        public AuraHandler AuraHandler;

        public GameObject KaiokenAura;
        public AudioSource audioSource;

        public bool AuraON = true;
        public bool Active = false;
        public float Multiplier = 2f;
        public float PreviousMultiplier = 2f;
        public float AppliedHealthMultiplier = 1f;
        public bool HealthMultiplierTracked;
        public float HealthScale { get { return Mathf.Max(1f, KiCombatMath.Finite(AppliedHealthMultiplier, 1f)); } }
        public float StrainMultiplier = 1f;
        public string LastForm = "Base";

        public List<string> CompatibleForms = new List<string>
        {
            "Base",
            "SSJ1",
            "SSJ2",
            "SSJ3",
            "SSJBlue",
            "PSSJBlue"
            };

        private void OnDestroy()
        {
            if (KaiokenAura != null) Destroy(KaiokenAura);
        }

        private void Awake()
        {
            audioSource = new GameObject("KaiokenAudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Body/UpperBody"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;

            UnityAction[] ReloadSound = { () => ToggleSound() };
            UnityAction[] ReloadAura = { () => ToggleAura() };

            var contextMenuButton = new ContextMenuButton("!Kaioken", "Kaioken Multiplier", "Kaioken Multiplier", new UnityAction[] { () => ShowKaiokenDialog(ReloadSound, ReloadAura) });
            transform.Find("Body/UpperBody").GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(contextMenuButton);
        }

        private void Start()
        {
            KiSys = GetComponent<KiSystem>();
            if (!transform.root.GetComponent<MultipliersAndStuffs>()) transform.root.gameObject.AddComponent<MultipliersAndStuffs>();
            Multipliers = transform.root.GetComponent<MultipliersAndStuffs>();

            /*transform.Find("Body/UpperBody").gameObject.AddComponent<UseEventTrigger>().Action = () =>
            {
                if (!GetComponent<PersonBehaviour>().IsAlive()) return;

                var transformationMenu = GetComponent<PersonBehaviour>().Limbs[0].GetComponent<TransformationContextMenu>();
                if (transformationMenu && !CompatibleForms.Contains(transformationMenu.Form)) return;

                StopAllCoroutines();
                if (!Active) StartCoroutine(Activate());
                else StartCoroutine(Deactivate());
                };*/

            if (transform.root.gameObject.GetComponent<AuraHandler>()) AuraHandler = transform.root.gameObject.GetComponent<AuraHandler>();
            // Scenes saved before explicit health tracking already contain the
            // active health bonus. Resume the mode without applying it twice.
            if (Active && GetComponent<PersonBehaviour>().IsAlive())
            {
                if (!HealthMultiplierTracked)
                    AppliedHealthMultiplier = Mathf.Max(1f, KiCombatMath.Finite(PreviousMultiplier, Multiplier));
                HealthMultiplierTracked = true;
                StartCoroutine(Activate());
            }
        }

        private void Update()
        {
            if (audioSource.volume != DBPU.PowersVolume) audioSource.volume = DBPU.PowersVolume;

            if (DbpuInputUtil.Down(DBPU.KaiokenActionId, KeyCode.K) && CeceUtils.IsSelected(transform.Find("Body/UpperBody").gameObject.GetComponent<PhysicalBehaviour>()))
            {
                if (!GetComponent<PersonBehaviour>().IsAlive()) return;

                var transformationMenu = GetComponent<PersonBehaviour>().Limbs[0].GetComponent<TransformationContextMenu>();
                if (transformationMenu && !CompatibleForms.Contains(transformationMenu.Form)) return;

                StopAllCoroutines();
                if (!Active) StartCoroutine(Activate());
                else StartCoroutine(Deactivate());
            }

            if (Active)
            {
                if (DBPU.AurasEnabled && AuraON)
                {
                    var native=KaiokenAura!=null?KaiokenAura.GetComponent<RefinedAuraVisual>():null;
                    if(KaiokenAura==null||(native!=null&&native.ProfileId!=CinematicAssets.KaiokenProfile(GetComponent<PersonBehaviour>())))CreateAura();
                }
                if (AuraON && KaiokenAura != null && KaiokenAura.GetComponent<RefinedAuraVisual>() == null) KaiokenAura.transform.position = GetComponent<PersonBehaviour>().Limbs[2].transform.position + new Vector3(0f, -1.1f, 0f);

                if (KiSys.Strain > 0.99f && DBPU.KaiokenStrainDamageEnabled)
                {
                    foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
                    {
                        limb.Health -= (limb.InitialHealth / 2000f) * Multiplier * (StrainMultiplier * DBPU.StrainDamageMultiplier) * Time.deltaTime;
                    }
                }
            }
        }

        public void ToggleFromPowersMenu()
        {
            if (!GetComponent<PersonBehaviour>().IsAlive()) return;
            var transformationMenu = GetComponent<PersonBehaviour>().Limbs[0].GetComponent<TransformationContextMenu>();
            if (transformationMenu && !CompatibleForms.Contains(transformationMenu.Form)) return;
            StopAllCoroutines();
            if (!Active) StartCoroutine(Activate());
            else StartCoroutine(Deactivate());
        }

        private void CreateAura()
        {
            if (KaiokenAura != null) Destroy(KaiokenAura);
            KaiokenAura = RefinedAuras.Create(CinematicAssets.KaiokenProfile(GetComponent<PersonBehaviour>()));
            if (KaiokenAura != null)
            {
                KaiokenAura.GetComponent<RefinedAuraVisual>().KaiokenPerson = GetComponent<PersonBehaviour>();
                KaiokenAura.AddComponent<DestroyWithObject>().Object = gameObject;
                return;
            }

            KaiokenAura = Instantiate(DBPU.Aura_Kaioken, GetComponent<PersonBehaviour>().Limbs[2].transform.position, Quaternion.identity);
            KaiokenAura.transform.localScale = new Vector3(1.3f + Multiplier / 32f, 1.3f + Multiplier / 32f);

            foreach (var lightning in KaiokenAura.GetComponentsInChildren<ParticleSystem>().Where(x => x.name == "lightning"))
            {
                lightning.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Top";
            }

            foreach (var spark in KaiokenAura.GetComponentsInChildren<ParticleSystem>().Where(x => x.name == "spark"))
            {
                spark.GetComponent<ParticleSystemRenderer>().sortingLayerName = "Top";
            }

            foreach (var aura in KaiokenAura.transform.Cast<Transform>().Where(x => x.name == "aura"))
            {
                aura.Find("aura").localEulerAngles = new Vector3(180, 0, 0);
                aura.Find("aura").localPosition = new Vector3(0, 1.4f, 0);
            }
        }

        // Keep the requested setting separate from the multiplier actually
        // applied to native health. Configuration and deactivation can happen
        // in the same frame, before the activation coroutine resumes.
        private void ApplyPowerMultiplier(float target)
        {
            var person = GetComponent<PersonBehaviour>();
            if (!person) return;
            if (!Multipliers) Multipliers = GetComponent<MultipliersAndStuffs>();
            float wanted = Mathf.Max(1f, KiCombatMath.Finite(target, 1f));
            float ratio = wanted / HealthScale;
            HealthMultiplierTracked = true;
            if (Mathf.Abs(ratio - 1f) > .000001f)
            {
                foreach (var limb in person.Limbs)
                {
                    if (!limb || limb.InitialHealth <= 0f) continue;
                    if (GogitoVitality.Scale(limb, ratio)) continue;
                    double fraction = Math.Max(0d, (double)limb.Health / limb.InitialHealth);
                    limb.InitialHealth = KiCombatMath.Cap((double)limb.InitialHealth * ratio, float.MaxValue);
                    limb.Health = KiCombatMath.Cap(limb.InitialHealth * fraction, float.MaxValue);
                }
                AppliedHealthMultiplier = wanted;
                MCP_Utils.InvalidatePersonStatCache(person);
                person.ChangeWeightBasedOnHealth();
            }
            if (Multipliers)
            {
                Multipliers.KaiokenMultiplier = wanted;
                Multipliers.Recalculate();
            }
            var power = person.GetComponent<CombatPowerController>();
            if (power) power.Refresh();
        }

        private void RefreshMultiplierAppearance()
        {
            KaiokenLook.ApplyBodyTint(GetComponent<PersonBehaviour>(), Multiplier);
            if (AuraON && KaiokenAura != null && KaiokenAura.GetComponent<RefinedAuraVisual>() == null)
                KaiokenAura.transform.localScale = new Vector3(1.3f + Multiplier / 32f, 1.3f + Multiplier / 32f);
        }

        private IEnumerator Activate()
        {
            Multiplier = Mathf.Clamp(KiCombatMath.Finite(Multiplier, 2f), 1f, Mathf.Max(1f, KiCombatMath.Finite(DBPU.KaiokenMaxMultiplier, 20f)));
            Active = true;

            audioSource.Stop();
            audioSource.PlayOneShot(DBPU.Aura_Kaioken_Start);
            audioSource.clip = DBPU.Aura_Kaioken_Loop;
            audioSource.loop = true;
            audioSource.Play();

            if (AuraON) CreateAura();

            KaiokenLook.ApplyBodyTint(GetComponent<PersonBehaviour>(), Multiplier);

            PreviousMultiplier = Multiplier;
            ApplyPowerMultiplier(Multiplier);

            var transformationMenu = GetComponent<PersonBehaviour>().Limbs[0].GetComponent<TransformationContextMenu>();

            if (transformationMenu)
            {
                StrainMultiplier = GetStrainMultiplier(transformationMenu.Form);
            }

            if (transformationMenu != null && !CinematicAssets.KeepsInnerAura(transformationMenu.Form) && AuraHandler != null)
                AuraHandler.DestroyAura();
            if (transformationMenu != null)
                LastForm = transformationMenu.Form;

            while (Active && GetComponent<PersonBehaviour>().IsAlive())
            {
                KiSys.StrainRate = 0.001f * Multiplier * StrainMultiplier * DBPU.KaiokenStrainMultiplier;

                if (PreviousMultiplier != Multiplier || AppliedHealthMultiplier != Multiplier)
                {
                    Multiplier = Mathf.Clamp(KiCombatMath.Finite(Multiplier, 2f), 1f, Mathf.Max(1f, KiCombatMath.Finite(DBPU.KaiokenMaxMultiplier, 20f)));
                    ApplyPowerMultiplier(Multiplier);
                    RefreshMultiplierAppearance();
                    PreviousMultiplier = Multiplier;
                }

                if (transformationMenu)
                {
                    StrainMultiplier = GetStrainMultiplier(transformationMenu.Form);

                    if (!CompatibleForms.Contains(transformationMenu.Form))
                    {
                        StartCoroutine(Deactivate());
                    }

                    if (AuraHandler != null && CinematicAssets.KeepsInnerAura(transformationMenu.Form) && (LastForm != transformationMenu.Form || !AuraHandler.AuraVisible))
                    {
                        LastForm = transformationMenu.Form;
                        AuraHandler.AuraVisible = true;
                        AuraHandler.CreateAura(transformationMenu.CurrentAuraID);
                    }

                    else if (!CinematicAssets.KeepsInnerAura(transformationMenu.Form) && AuraHandler != null && AuraHandler.AuraVisible == true)
                    {
                        LastForm = transformationMenu.Form;
                        AuraHandler.AuraVisible = false;
                        AuraHandler.DestroyAura();
                    }
                }

                yield return new WaitForEndOfFrame();
            }

            if (Active && !GetComponent<PersonBehaviour>().IsAlive()) StartCoroutine(Deactivate());
        }

        private IEnumerator Deactivate()
        {
            Active = false;
            audioSource.Stop();
            audioSource.PlayOneShot(DBPU.Aura_Kaioken_End);

            var transformationMenu = GetComponent<PersonBehaviour>().Limbs[0].GetComponent<TransformationContextMenu>();

            if (KaiokenAura != null) Destroy(KaiokenAura);

            ApplyPowerMultiplier(1f);

            if (AuraHandler != null)
                AuraHandler.AuraVisible = true;

            if (AuraHandler != null && transformationMenu != null && AuraHandler.AuraActive == true && !transformationMenu.Form.Contains("SSJBlue") && GetComponent<PersonBehaviour>().IsAlive())
            {
                AuraHandler.CreateAura(transformationMenu.CurrentAuraID);
            }

            foreach (var limb in GetComponent<PersonBehaviour>().Limbs)
            {
                limb.GetComponent<SpriteRenderer>().color = Color.white;
                foreach (SpriteRenderer sr in limb.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.color = Color.white;
                }
            }

            Multipliers.KaiokenMultiplier = 1f;
            KiSys.StrainRate = 0f;

            yield return null;
        }

        private void ToggleSound()
        {
            audioSource.volume = audioSource.volume == 1 ? 0 : 1;
            ShowKaiokenDialog(new UnityAction[] { () => ToggleSound() }, new UnityAction[] { () => ToggleAura() });
        }

        private void ToggleAura()
        {
            AuraON = !AuraON;

            if (Active)
            {
                if (AuraON) CreateAura();
                else if (KaiokenAura != null) Destroy(KaiokenAura);
            }

            ShowKaiokenDialog(new UnityAction[] { () => ToggleSound() }, new UnityAction[] { () => ToggleAura() });
        }

        private void ShowKaiokenDialog(UnityAction[] reloadSound, UnityAction[] reloadAura)
        {
            DialogBox Dialog = DialogBoxManager.Dialog("<b>Choose the Kaioken Multiplier<b>",
                new DialogButton("<b><size=75%>X2<b>", true, new UnityAction[] { () => SetMultiplier(2f) }),
                new DialogButton("<b><size=75%>X3<b>", true, new UnityAction[] { () => SetMultiplier(3f) }),
                new DialogButton("<b><size=75%>X4<b>", true, new UnityAction[] { () => SetMultiplier(4f) }),
                new DialogButton("<b><size=75%>X8<b>", true, new UnityAction[] { () => SetMultiplier(8f) }),
                new DialogButton("<b><size=75%>X12<b>", true, new UnityAction[] { () => SetMultiplier(12f) }),
                new DialogButton("<b><size=75%>X20<b>", true, new UnityAction[] { () => SetMultiplier(20f) }),

                new DialogButton("<b><size=60%>Custom<b>", true, new UnityAction[] { () => CustomMultiplier() }),

                new DialogButton(audioSource.volume == 1 ? "<b><size=50%>Sound <color=green>ON<color=white><b>" : "<b><size=50%>Sound <color=red>OFF<color=white><b>", true, reloadSound),
                new DialogButton(AuraON ? "<b><size=60%>Aura <color=green>ON<color=white><b>" : "<b><size=60%>Aura <color=red>OFF<color=white><b>", true, reloadAura),
                new DialogButton("<b><size=60%>Close<b>", true, new UnityAction[] { () => { } })
                );

            Dialog.TextMesh.gameObject.GetComponent<LayoutElement>().minWidth = 900;
        }

        private void CustomMultiplier()
        {
            Utils.OpenFloatInputDialog(Multiplier, GetComponent<PersonBehaviour>(), delegate (PersonBehaviour person, float value)
            {
                if (value < 1f) value = 1f;
                SetMultiplier(value);
            }, "Set Kaioken Multiplier", "Default: 2");
        }

        private void SetMultiplier(float multiplier)
        {
            Multiplier = Mathf.Clamp(KiCombatMath.Finite(multiplier, 2f), 1f, Mathf.Max(1f, KiCombatMath.Finite(DBPU.KaiokenMaxMultiplier, 20f)));
            if (Active)
            {
                ApplyPowerMultiplier(Multiplier);
                RefreshMultiplierAppearance();
                PreviousMultiplier = Multiplier;
                audioSource.PlayOneShot(DBPU.Aura_Kaioken_Start);
            }
        }

        public void ApplyConfiguredMultiplier(float multiplier)
        {
            SetMultiplier(multiplier);
        }

        private float GetStrainMultiplier(string form)
        {
            switch (form)
            {
                case "Base": return 1f;
                case "SSJ1": return 5f;
                case "PSSJ": return 1.2f;
                case "SSJ2": return 6f;
                case "SSJ3": return 8f;
                case "SSJBlue": return 1.5f;
                case "PSSJBlue": return 1.2f;
                default: return 1f;
            }
        }
    }
}
