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
    public class InstantTransmission : MonoBehaviour
    {
        private PersonBehaviour person;
        private KiSystem KiSys;
        private AudioSource audioSource;

        public GameObject MaskObject;
        public GameObject Shadow;

        public bool Active = false;
        public bool CanUse = false;
        public bool Teleporting = false;
        public bool IsTeleporting = false;
        public float CostPercent = 0.01f;

        public List<Sprite> Effect = new List<Sprite>
        {
            DBPU.TeleportEffect_1,
            DBPU.TeleportEffect_2,
            DBPU.TeleportEffect_3,
            DBPU.TeleportEffect_4,
            DBPU.TeleportEffect_5
            };

        private void Awake()
        {
            audioSource = new GameObject("BeamAudioSource").AddComponent<AudioSource>();
            audioSource.transform.SetParent(transform.Find("Head"), false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.75f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;
        }

        private void Start()
        {
            if (!GetComponent<KiSystem>()) gameObject.AddComponent<KiSystem>();
            KiSys = GetComponent<KiSystem>();

            person = GetComponent<PersonBehaviour>();

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                if (limb.name == "Head")
                {
                    limb.gameObject.AddComponent<UseEventTrigger>().Action = () =>
                    {
                        if (Active == false) return;

                        if (CanUse == false) StartCoroutine(WaitForClick(limb.transform));
                        else CanUse = false;
                    };
                }
            }
        }

        private IEnumerator WaitForClick(Transform Head)
        {
            CanUse = true;

            while (Active == true && CanUse == true)
            {
                if (DbpuInputUtil.Down(DBPU.InstantTransmissionActionId, KeyCode.R) && Teleporting == false && KiSys.currentKi > KiSys.maxKi / 10f)
                {
                    StartCoroutine(KiCost());
                    StartCoroutine(Teleport(Head));
                    Teleporting = true;

                    yield return null;
                }

                yield return null;
            }

            yield return null;
        }

        private IEnumerator KiCost()
        {
            if (DBPU.KiAttackCostMultiplier == 0f) yield break;

            float OGKiDrain = KiSys.currentKiDrainRate;
            float KiRegen = KiSys.baseRegen;

            if (OGKiDrain != 0f) KiSys.baseRegen = 0f;

            float FinalKi = KiSys.currentKi - (KiSys.maxKi * CostPercent);
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

        private IEnumerator Teleport(Transform Head)
        {
            if (audioSource.volume != DBPU.PowersVolume) audioSource.volume = DBPU.PowersVolume;

            //CreateShadow(Head);

            //yield return new WaitUntil(() => !Input.GetMouseButtonDown(0) || !person.IsAlive());

            //yield return new WaitUntil(() => Input.GetMouseButtonDown(0) || !person.IsAlive());

            audioSource.PlayOneShot(DBPU.Misc_IT);

            foreach (var fix in GetComponentsInChildren<FixedJoint2D>())
            {
                foreach (var sr in fix.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }

                foreach (var col in fix.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                {
                    if (!col.gameObject.GetComponent<FreezeBehaviour>()) col.gameObject.AddComponent<FreezeBehaviour>();
                    col.isTrigger = true;
                }
            }

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                if (!limb.gameObject.GetComponent<FreezeBehaviour>()) limb.gameObject.AddComponent<FreezeBehaviour>();

                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    foreach (var hin in limb.gameObject.GetComponentsInChildren<HingeJoint2D>())
                    {
                        if (hin.connectedBody.gameObject.transform.root.gameObject != gameObject)
                        {
                            if (hin.connectedBody.gameObject.GetComponent<LimbBehaviour>())
                            {
                                foreach (var limbs2 in hin.connectedBody.gameObject.GetComponent<LimbBehaviour>().Person.Limbs)
                                {
                                    if (!limbs2.gameObject.GetComponent<FreezeBehaviour>()) limbs2.gameObject.AddComponent<FreezeBehaviour>();
                                }

                                foreach (var sr in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                {
                                    sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                }

                                foreach (var col in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<Collider2D>())
                                {
                                    col.isTrigger = true;
                                }

                                foreach (var fix in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<FixedJoint2D>())
                                {
                                    foreach (var sr in fix.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                    {
                                        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                    }

                                    foreach (var col in fix.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                                    {
                                        if (!col.gameObject.GetComponent<FreezeBehaviour>()) col.gameObject.AddComponent<FreezeBehaviour>();
                                        col.isTrigger = true;
                                    }
                                }
                            }

                            else if (hin.connectedBody.gameObject.GetComponent<Rigidbody2D>())
                            {
                                foreach (var sr in hin.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                {
                                    sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                }

                                foreach (var col in hin.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                                {
                                    if (!col.gameObject.GetComponent<FreezeBehaviour>()) col.gameObject.AddComponent<FreezeBehaviour>();
                                    col.isTrigger = true;
                                }
                            }
                        }
                    }
                }
            }

            foreach (var col in GetComponentsInChildren<Collider2D>())
            {
                col.isTrigger = true;
            }

            //yield return new WaitForFixedUpdate();

            Vector3 NewPosition = Global.main.MousePosition - Head.position;

            GameObject MaskObject = new GameObject();
            SpriteMask mask = MaskObject.gameObject.AddComponent<SpriteMask>();
            mask.transform.position = MCP_Utils.GetMiddlePositionOfBody(person);
            mask.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            mask.sprite = Effect[0];
            mask.isCustomRangeActive = false;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            for (int i = 0; i < Effect.Count; i++)
            {
                mask.sprite = Effect[i];

                yield return new WaitForSeconds(0.005f);
            }

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                limb.transform.position += NewPosition;

                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    foreach (var hin in limb.gameObject.GetComponentsInChildren<HingeJoint2D>())
                    {
                        if (hin.connectedBody.gameObject.transform.root.gameObject != gameObject)
                        {
                            if (hin.connectedBody.gameObject.GetComponent<LimbBehaviour>())
                            {
                                foreach (var limbs2 in hin.connectedBody.gameObject.GetComponent<LimbBehaviour>().Person.Limbs)
                                {
                                    limbs2.transform.position += NewPosition;
                                }

                                foreach (var fix in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<FixedJoint2D>())
                                {
                                    fix.connectedBody.gameObject.transform.position += NewPosition;
                                }
                            }

                            else if (hin.connectedBody.gameObject.GetComponent<Rigidbody2D>())
                            {
                                hin.connectedBody.transform.position += NewPosition;
                            }
                        }
                    }
                }
            }

            foreach (var fix in GetComponentsInChildren<FixedJoint2D>())
            {
                fix.connectedBody.gameObject.transform.position += NewPosition;
            }

            mask.transform.position = MCP_Utils.GetMiddlePositionOfBody(person);

            for (int i = Effect.Count - 1; i > 0; i--)
            {
                mask.sprite = Effect[i];

                yield return new WaitForSeconds(0.005f);
            }

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.maskInteraction = SpriteMaskInteraction.None;
            }

            if (MaskObject != null) Destroy(MaskObject);

            foreach (var limb in person.Limbs)
            {
                if (limb.gameObject == null) continue;

                Destroy(limb.gameObject.GetComponent<FreezeBehaviour>());

                if (MCP_Utils.LimbCanUsePowers(limb) && limb.name.Contains("LowerArm"))
                {
                    foreach (var hin in limb.gameObject.GetComponentsInChildren<HingeJoint2D>())
                    {
                        if (hin.connectedBody.gameObject.transform.root.gameObject != gameObject)
                        {
                            if (hin.connectedBody.gameObject.GetComponent<LimbBehaviour>())
                            {
                                foreach (var limbs2 in hin.connectedBody.gameObject.GetComponent<LimbBehaviour>().Person.Limbs)
                                {
                                    if (limbs2.gameObject.GetComponent<FreezeBehaviour>()) Destroy(limbs2.gameObject.GetComponent<FreezeBehaviour>());
                                    MCP_Utils.ForceClearViaExit(limbs2.PhysicalBehaviour);
                                }

                                foreach (var sr in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                {
                                    sr.maskInteraction = SpriteMaskInteraction.None;
                                }

                                foreach (var col in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<Collider2D>())
                                {
                                    col.isTrigger = false;
                                }

                                foreach (var fix in hin.connectedBody.gameObject.transform.root.GetComponentsInChildren<FixedJoint2D>())
                                {
                                    foreach (var sr in fix.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                    {
                                        sr.maskInteraction = SpriteMaskInteraction.None;
                                    }

                                    foreach (var col in fix.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                                    {
                                        if (col.gameObject.GetComponent<FreezeBehaviour>()) Destroy(col.gameObject.GetComponent<FreezeBehaviour>());
                                        MCP_Utils.ForceClearViaExit(col.gameObject.GetComponent<PhysicalBehaviour>());
                                        col.isTrigger = false;
                                    }
                                }
                            }

                            else if (hin.connectedBody.gameObject.GetComponent<Rigidbody2D>())
                            {
                                foreach (var sr in hin.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                                {
                                    sr.maskInteraction = SpriteMaskInteraction.None;
                                }

                                foreach (var col in hin.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                                {
                                    if (col.gameObject.GetComponent<FreezeBehaviour>()) Destroy(col.gameObject.GetComponent<FreezeBehaviour>());
                                    MCP_Utils.ForceClearViaExit(col.gameObject.GetComponent<PhysicalBehaviour>());
                                    col.isTrigger = false;
                                }
                            }
                        }
                    }
                }
            }

            foreach (var col in GetComponentsInChildren<Collider2D>())
            {
                col.isTrigger = false;
            }

            foreach (var fix in GetComponentsInChildren<FixedJoint2D>())
            {
                foreach (var sr in fix.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.maskInteraction = SpriteMaskInteraction.None;
                }

                foreach (var col in fix.connectedBody.transform.root.GetComponentsInChildren<Collider2D>())
                {
                    if (col.gameObject.GetComponent<FreezeBehaviour>()) Destroy(col.gameObject.GetComponent<FreezeBehaviour>());
                    MCP_Utils.ForceClearViaExit(col.gameObject.GetComponent<PhysicalBehaviour>());
                    col.isTrigger = false;
                }
            }

            person.ForceAllLimbsClearViaExit();

            IsTeleporting = false;
            Teleporting = false;

            yield return null;
        }

        private IEnumerator CreateShadow(Transform Head)
        {
            Shadow = new GameObject();
            Shadow.transform.position = Head.position;

            foreach (var fix in GetComponentsInChildren<FixedJoint2D>())
            {
                foreach (var sr in fix.connectedBody.transform.root.GetComponentsInChildren<SpriteRenderer>())
                {
                    GameObject ShadowImage = new GameObject("Shadow Image");
                    ShadowImage.AddComponent<SpriteRenderer>().sprite = sr.sprite;
                    //ShadowImage.AddComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f);
                    ShadowImage.transform.position = sr.gameObject.transform.position;
                    ShadowImage.transform.localScale = sr.gameObject.transform.localScale;
                    ShadowImage.transform.SetParent(Shadow.transform);
                }
            }

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                GameObject ShadowImage = new GameObject("Shadow Image");
                ShadowImage.AddComponent<SpriteRenderer>().sprite = sr.sprite;
                //ShadowImage.AddComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f);
                ShadowImage.transform.position = sr.gameObject.transform.position;
                ShadowImage.transform.localScale = sr.gameObject.transform.localScale;
                ShadowImage.transform.SetParent(Shadow.transform);
            }

            Shadow.transform.position = Global.main.MousePosition;

            while (IsTeleporting == true || Teleporting == true)
            {
                Shadow.transform.position = Global.main.MousePosition;
            }

            Destroy(Shadow);

            yield return null;
        }
    }
}
