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
    public class FusionHandler : MonoBehaviour
    {
        public class Fusions
        {
            public class FusionInfo
            {
                public string Character1 { get; set; }
                public string Character2 { get; set; }

                public string ResultCharacter { get; set; }

                public string FusionType { get; set; }
                public string FusionAnimationID { get; set; }

                public AudioClip VoiceLines { get; set; } = null;
            }

            public Dictionary<string, FusionInfo> FusionData { get; private set; } = new Dictionary<string, FusionInfo>();

            public void AddTransformation(string key, FusionInfo fusionInfo)
            {
                if (!FusionData.ContainsKey(key))
                {
                    FusionData[key] = fusionInfo;
                }
            }
        }

        static Fusions SharedFusions;
        static readonly object FusionInitLock = new object();

        public Fusions fusions
        {
            get
            {
                EnsureSharedFusions();
                return SharedFusions;
            }
            set { }
        }

        public static bool FusionPairMatches(string character1, string character2, string nameA, string nameB)
        {
            return (character1 == nameA && character2 == nameB) || (character1 == nameB && character2 == nameA);
        }

        static void EnsureSharedFusions()
        {
            if (SharedFusions != null) return;
            lock (FusionInitLock)
            {
                if (SharedFusions != null) return;
                var built = new Fusions();
                built.AddTransformation("Gogeta", new Fusions.FusionInfo()
                {
                    Character1 = "Goku",
                    Character2 = "Vegeta",
                    ResultCharacter = "Gogeta",
                    FusionType = "Metamoro",
                    FusionAnimationID = "1",
                });
                built.AddTransformation("Tiencha", new Fusions.FusionInfo()
                {
                    Character1 = "Tenshinhan",
                    Character2 = "Yamcha",
                    ResultCharacter = "Tiencha",
                    FusionType = "Metamoro",
                    FusionAnimationID = "1",
                });
                built.AddTransformation("Teen Gotenks", new Fusions.FusionInfo()
                {
                    Character1 = "Teen Goten",
                    Character2 = "Teen Trunks",
                    ResultCharacter = "Teen Gotenks",
                    FusionType = "Metamoro",
                    FusionAnimationID = "1",
                });
                built.AddTransformation("Teen GotenksReverse", new Fusions.FusionInfo()
                {
                    Character1 = "Teen Trunks",
                    Character2 = "Teen Goten",
                    ResultCharacter = "Teen Gotenks",
                    FusionType = "Metamoro",
                    FusionAnimationID = "1",
                });
                built.AddTransformation("Vegito", new Fusions.FusionInfo()
                {
                    Character1 = "Goku",
                    Character2 = "Vegeta",
                    ResultCharacter = "Vegito",
                    FusionType = "Potara",
                    FusionAnimationID = "1",
                });
                built.AddTransformation("VegitoReverse", new Fusions.FusionInfo()
                {
                    Character1 = "Vegeta",
                    Character2 = "Goku",
                    ResultCharacter = "Vegito",
                    FusionType = "Potara",
                    FusionAnimationID = "1",
                });
                SharedFusions = built;
            }
        }

        public void Start()
        {
            EnsureSharedFusions();
        }

        public bool canFuse = false;

        public void OnCollisionEnter2D(Collision2D collision)
        {
            if (canFuse == false) return;

            if (collision.gameObject.transform.root.GetComponent<PersonBehaviour>() != null && collision.gameObject.transform.root.GetComponent<PersonBehaviour>() != transform.root.gameObject.GetComponent<PersonBehaviour>() && collision.gameObject.GetComponentInChildren<FusionHandler>() != null)
            {
                var otherPerson = collision.gameObject.transform.root.gameObject.GetComponent<PersonBehaviour>();
                var thisPerson = transform.root.gameObject.GetComponent<PersonBehaviour>();
                string nameA = transform.root.gameObject.name;
                string nameB = collision.gameObject.transform.root.gameObject.name;

                foreach (var fusion in fusions.FusionData)
                {
                    if (fusion.Value.FusionType != "Metamoro") continue;
                    if (!FusionPairMatches(fusion.Value.Character1, fusion.Value.Character2, nameA, nameB)) continue;

                    if ((thisPerson.transform.localScale.x < 0f && otherPerson.transform.localScale.x < 0f) || (thisPerson.transform.localScale.x > 0f && otherPerson.transform.localScale.x > 0f)) return;
                    else if (MCP_Utils.GetMiddlePositionOfBody(thisPerson).x < MCP_Utils.GetMiddlePositionOfBody(otherPerson).x) if (thisPerson.transform.localScale.x < 0f && otherPerson.transform.localScale.x > 0f) return;
                    else if (MCP_Utils.GetMiddlePositionOfBody(thisPerson).x > MCP_Utils.GetMiddlePositionOfBody(otherPerson).x) if (thisPerson.transform.localScale.x > 0f && otherPerson.transform.localScale.x < 0f) return;

                    canFuse = false;
                    StartCoroutine(Fuse(thisPerson, otherPerson, fusion.Key));
                    return;
                }
            }
        }

        public IEnumerator Fuse(PersonBehaviour person, PersonBehaviour otherPerson, string fusionKey)
        {
            if (!fusions.FusionData.ContainsKey(fusionKey))
            {
                Debug.LogWarning($"Fusion '{fusionKey}' not found.");
                yield break;
            }

            string char1Transformation = "Base";
            string char2Transformation = "Base";

            if (person.gameObject.GetComponent<CharacterTransformations>() && otherPerson.gameObject.GetComponent<CharacterTransformations>())
            {
                var menu1 = person.Limbs[0].gameObject.GetComponent<TransformationContextMenu>();
                var menu2 = otherPerson.Limbs[0].gameObject.GetComponent<TransformationContextMenu>();
                if (menu1 != null && menu2 != null)
                {
                    char1Transformation = menu1.Form;
                    char2Transformation = menu2.Form;

                    if (char1Transformation != char2Transformation) { ModAPI.Notify("Unable to fuse. Characters must be on similar power levels."); yield break; }
                }
            }

            foreach (FusionHandler handler in person.gameObject.GetComponentsInChildren<FusionHandler>()) handler.canFuse = false;
            foreach (FusionHandler handler in otherPerson.gameObject.GetComponentsInChildren<FusionHandler>()) handler.canFuse = false;

            var t = fusions.FusionData[fusionKey];

            if (t.FusionType == "Metamoro")
            {
                if (DBPU.AFWEnabled == false) ModAPI.Notify("!Animation Framework isn't enabled, the Fusion Animation won't be played!");

                Vector3 Char1Pos = MCP_Utils.GetMiddlePositionOfBody(person);
                Vector3 Char2Pos = MCP_Utils.GetMiddlePositionOfBody(otherPerson);

                float XDistance = (Mathf.Abs(Char1Pos.x - Char2Pos.x) - 1.3f) / 2f;
                float direction = (Char1Pos.x < Char2Pos.x) ? 1f : -1f;

                float Scale1 = person.transform.root.localScale.x / 1.0417f;
                float Scale2 = otherPerson.transform.root.localScale.x / 1.0417f;

                if (DBPU.AFWEnabled == true)
                {
                    foreach (var limbs in person.Limbs)
                    {
                        limbs.gameObject.AddComponent<FreezeBehaviour>();
                        limbs.gameObject.GetComponent<Collider2D>().enabled = false;
                        limbs.transform.position += new Vector3((-1.65f + (XDistance * Scale1)) * direction, -0.05f, 0f);
                    }

                    foreach (var limbs in otherPerson.Limbs)
                    {
                        limbs.gameObject.AddComponent<FreezeBehaviour>();
                        limbs.gameObject.GetComponent<Collider2D>().enabled = false;
                        limbs.transform.position += new Vector3((1.65f - (XDistance * Scale2)) * direction, -0.05f, 0f);
                    }

                    Action<int, Transform> onFrameCallback = (frameIdx, root) =>
                    {
                        if (frameIdx == 1)
                        {
                            GetComponent<PhysicalBehaviour>().PlayClipOnce(DBPU.Misc_Movement1);
                        }

                        if (frameIdx == 4)
                        {
                            GetComponent<PhysicalBehaviour>().PlayClipOnce(DBPU.Voice_Fusion_GokuXVegeta);
                        }
                    };

                    Action<int, Transform> onFrameCallback2 = (frameIdx, root) =>
                    {
                    };

                    Char1Pos = MCP_Utils.GetMiddlePositionOfBody(person);
                    Char2Pos = MCP_Utils.GetMiddlePositionOfBody(otherPerson);

                    AnimationFramework.PlayAnimation(
                        target: person.transform.root,
                        animationPath: DBPU.ModFolder + "/Assets/Animations/MetamoroDance.json",
                        onFrame: onFrameCallback,
                        playbackSpeed: 1f,
                        false,
                        null,
                        null,
                        () => Char1Pos
                        );

                    AnimationFramework.PlayAnimation(
                        target: otherPerson.transform.root,
                        animationPath: DBPU.ModFolder + "/Assets/Animations/MetamoroDance.json",
                        null,
                        playbackSpeed: 1f,
                        false,
                        null,
                        null,
                        () => Char2Pos
                        );

                    yield return new WaitForSeconds(4.5f);
                }

                else
                {
                    foreach (var limbs in person.Limbs)
                    {
                        limbs.gameObject.AddComponent<FreezeBehaviour>();
                        limbs.gameObject.GetComponent<Collider2D>().enabled = false;
                    }

                    foreach (var limbs in otherPerson.Limbs)
                    {
                        limbs.gameObject.AddComponent<FreezeBehaviour>();
                        limbs.gameObject.GetComponent<Collider2D>().enabled = false;
                    }

                    GetComponent<PhysicalBehaviour>().PlayClipOnce(DBPU.Voice_Fusion_GokuXVegeta);

                    yield return new WaitForSeconds(4.1f);
                }

                yield return RunFusionVFXAndSpawn(person, otherPerson, t, char1Transformation, char2Transformation, true);
            }

            if (t.FusionType == "Potara")
            {
                foreach (var limbs in person.Limbs)
                {
                    limbs.gameObject.AddComponent<FreezeBehaviour>();
                    limbs.GetComponent<Collider2D>().enabled = false;
                }

                foreach (var limbs in otherPerson.Limbs)
                {
                    limbs.gameObject.AddComponent<FreezeBehaviour>();
                    limbs.GetComponent<Collider2D>().enabled = false;
                }

                bool supportsUnfuse = fusionKey == "Vegito" || fusionKey == "VegitoReverse";
                yield return RunFusionVFXAndSpawn(person, otherPerson, t, char1Transformation, char2Transformation, supportsUnfuse);
                yield break;
            }

            yield return null;
        }

        IEnumerator RunFusionVFXAndSpawn(PersonBehaviour person, PersonBehaviour otherPerson, Fusions.FusionInfo t, string char1Transformation, string char2Transformation, bool supportsUnfuse)
        {
            Vector3 MidlePos = (MCP_Utils.GetMiddlePositionOfBody(person) + MCP_Utils.GetMiddlePositionOfBody(otherPerson)) * 0.5f;

            var currentAura = Instantiate(DBPU.Aura_MetamoroFusion, MidlePos - new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            currentAura.gameObject.layer = 9;
            currentAura.AddComponent<DestroyOverTime>().Timer = 20f;
            currentAura.AddComponent<SortingGroup>().sortingLayerName = "Bubbles";

            AudioSource audioSource = currentAura.AddComponent<AudioSource>();
            audioSource.transform.SetParent(currentAura.transform, false);
            Global global = FindObjectOfType<Global>();
            global.AddAudioSource(audioSource, false);
            audioSource.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.85f;
            audioSource.outputAudioMixerGroup = global.SoundEffects;
            audioSource.spatialBlend = 1;
            audioSource.volume = 1;
            audioSource.maxDistance = 1000;
            audioSource.PlayOneShot(DBPU.Misc_MetamoroFusion);

            float intensity = 2f;

            foreach (var transforms in currentAura.GetComponentsInChildren<Transform>())
            {
                transforms.position = new Vector3(transforms.position.x, transforms.position.y, 0f);
            }

            foreach (var ps in currentAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.startColor.mode == ParticleSystemGradientMode.Color) main.startColor = new ParticleSystem.MinMaxGradient(main.startColor.color * intensity);
            }

            yield return new WaitForSeconds(4.6f);

            float Opac = 1f;

            while (Opac > 0f)
            {
                foreach (SpriteRenderer renderers in person.gameObject.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderers.color = new Color(renderers.color.r, renderers.color.g, renderers.color.b, Opac);
                }

                foreach (SpriteRenderer renderers in otherPerson.gameObject.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderers.color = new Color(renderers.color.r, renderers.color.g, renderers.color.b, Opac);
                }

                Opac -= 0.1f;

                yield return new WaitForSeconds(0.1f);
            }

            var fusionSpawnable = ModAPI.FindSpawnable(DBPU.ModTag + t.ResultCharacter);
            if (fusionSpawnable == null) fusionSpawnable = ModAPI.FindSpawnable(t.ResultCharacter);
            if (fusionSpawnable == null)
            {
                Debug.LogWarning($"Fusion result spawnable '{t.ResultCharacter}' not found.");
                yield break;
            }

            GameObject FusionCharacter = GameObject.Instantiate(fusionSpawnable.Prefab, MidlePos - new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            CatalogBehaviour.PerformMod(fusionSpawnable, FusionCharacter);

            while (Opac < 1f)
            {
                Opac += 0.1f;

                foreach (SpriteRenderer renderers in FusionCharacter.gameObject.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderers.color = new Color(renderers.color.r, renderers.color.g, renderers.color.b, Opac);
                }

                yield return new WaitForSeconds(0.05f);
            }

            if (char1Transformation == char2Transformation && FusionCharacter.GetComponent<CharacterTransformations>())
            {
                FusionCharacter.GetComponent<CharacterTransformations>().ApplyTransformation(FusionCharacter.GetComponent<PersonBehaviour>(), char1Transformation);
            }

            if (supportsUnfuse)
            {
                var fusionComp = FusionCharacter.AddComponent<FusionComponent>();
                fusionComp.Character1 = person.gameObject;
                fusionComp.Character2 = otherPerson.gameObject;

                var unfuseButton = new ContextMenuButton("!Unfuse", "Unfuse", "Unfuse", new UnityAction[] { () => fusionComp.DoDefuse() });

                foreach (var limbs in FusionCharacter.GetComponent<PersonBehaviour>().Limbs)
                {
                    limbs.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(unfuseButton);

                    MCP_Utils.MoveContextButton(limbs.GetComponent<PhysicalBehaviour>().ContextMenuOptions, "!Unfuse", 0);
                }
            }
            else
            {
                foreach (var limb in person.Limbs) Destroy(limb.gameObject);
                foreach (var limb in otherPerson.Limbs) Destroy(limb.gameObject);
            }
        }

        public class FusionComponent : MonoBehaviour
        {
            public GameObject Character1;
            public GameObject Character2;

            private bool Unfusing = false;

            public void Start()
            {
                Character1.active = false;
                Character2.active = false;

                gameObject.AddComponent<DestroyWithObject>().Object = Character1;
                gameObject.AddComponent<DestroyWithObject>().Object = Character2;

                foreach (SpriteRenderer renderers in Character1.GetComponentsInChildren<SpriteRenderer>()) renderers.color = Color.white;

                foreach (SpriteRenderer renderers in Character2.GetComponentsInChildren<SpriteRenderer>()) renderers.color = Color.white;

                StartCoroutine(CountDown());
            }

            public IEnumerator CountDown()
            {
                yield return new WaitForSeconds(60f);

                StartCoroutine(Defuse());
            }

            public void DoDefuse()
            {
                if (Unfusing == true) return;
                else Unfusing = true;

                StartCoroutine(Defuse());
            }

            public IEnumerator Defuse()
            {
                Character1.active = true;
                Character2.active = true;

                foreach (var limbs in GetComponent<PersonBehaviour>().Limbs)
                {
                    limbs.gameObject.AddComponent<FreezeBehaviour>();
                    limbs.gameObject.GetComponent<Collider2D>().enabled = false;
                }

                foreach (SpriteRenderer renderers in gameObject.GetComponentsInChildren<SpriteRenderer>()) renderers.color = Color.clear;

                float ForceDirection = (MCP_Utils.GetMiddlePositionOfBody(Character1.GetComponent<PersonBehaviour>()).x > MCP_Utils.GetMiddlePositionOfBody(Character2.GetComponent<PersonBehaviour>()).x) ? 1f : -1f;

                Vector3 Char1DistFromFuse = MCP_Utils.GetMiddlePositionOfBody(GetComponent<PersonBehaviour>()) - MCP_Utils.GetMiddlePositionOfBody(Character1.GetComponent<PersonBehaviour>());
                Vector3 Char2DistFromFuse = MCP_Utils.GetMiddlePositionOfBody(GetComponent<PersonBehaviour>()) - MCP_Utils.GetMiddlePositionOfBody(Character2.GetComponent<PersonBehaviour>());
                Vector3 UnfusePlusPos = (Char1DistFromFuse + Char2DistFromFuse) / 2f;

                foreach (var c1 in Character1.GetComponentsInChildren<Collider2D>())
                    foreach (var c2 in Character2.GetComponentsInChildren<Collider2D>())
                        Physics2D.IgnoreCollision(c1, c2, true);

                foreach (var limbs in Character1.GetComponent<PersonBehaviour>().Limbs)
                {
                    Destroy(limbs.gameObject.GetComponent<FreezeBehaviour>());
                    limbs.gameObject.GetComponent<Collider2D>().enabled = true;
                    limbs.transform.position += UnfusePlusPos + new Vector3(-0.7f * ForceDirection, 0f);
                }

                foreach (var limbs in Character2.GetComponent<PersonBehaviour>().Limbs)
                {
                    Destroy(limbs.gameObject.GetComponent<FreezeBehaviour>());
                    limbs.gameObject.GetComponent<Collider2D>().enabled = true;
                    limbs.transform.position += UnfusePlusPos + new Vector3(0.7f * ForceDirection, 0f);
                }

                IgnoreColliderWithSelves();

                yield return new WaitForFixedUpdate();

                foreach (var limbs in Character1.GetComponent<PersonBehaviour>().Limbs) { limbs.GetComponent<Rigidbody2D>().AddForce(new Vector2((4f * limbs.GetComponent<Rigidbody2D>().mass) * ForceDirection, 0f), ForceMode2D.Impulse); limbs.GetComponent<Rigidbody2D>().AddTorque((-35f * limbs.GetComponent<Rigidbody2D>().mass) * ForceDirection); }
                foreach (var limbs in Character2.GetComponent<PersonBehaviour>().Limbs) { limbs.GetComponent<Rigidbody2D>().AddForce(new Vector2((-4f * limbs.GetComponent<Rigidbody2D>().mass) * ForceDirection, 0f), ForceMode2D.Impulse); limbs.GetComponent<Rigidbody2D>().AddTorque((35f * limbs.GetComponent<Rigidbody2D>().mass) * ForceDirection); }

                yield return new WaitForSeconds(0.25f);

                foreach (var limbs in Character1.GetComponent<PersonBehaviour>().Limbs) limbs.gameObject.FixColliders();
                foreach (var limbs in Character2.GetComponent<PersonBehaviour>().Limbs) limbs.gameObject.FixColliders();

                IgnoreColliderWithSelves();

                Destroy(gameObject);

                yield return null;
            }

            void IgnoreColliderWithSelves()
            {
                foreach (var c1 in Character1.GetComponentsInChildren<Collider2D>())
                    foreach (var c2 in Character1.GetComponentsInChildren<Collider2D>())
                        Physics2D.IgnoreCollision(c1, c2, true);

                foreach (var c1 in Character2.GetComponentsInChildren<Collider2D>())
                    foreach (var c2 in Character2.GetComponentsInChildren<Collider2D>())
                        Physics2D.IgnoreCollision(c1, c2, true);
            }
        }
    }
}
