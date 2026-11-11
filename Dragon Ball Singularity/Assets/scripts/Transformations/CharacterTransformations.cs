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
    public class CharacterTransformations : MonoBehaviour
    {
        public class Transformations
        {
            public class TransformationStats
            {
                public float PowerMultiplier { get; set; }
                public float Size { get; set; }
                public Sprite HairSprite { get; set; }
                public bool UseHairLocalPosition { get; set; }
                public Vector3 HairLocalPosition { get; set; }
                public Sprite HeadSprite { get; set; }
                public float KiDrainRate { get; set; }
                public string AuraID { get; set; }
                public AudioClip BurstSFX { get; set; } = DBPU.Aura_Burst_Basic;
                public Color requiredKiColor { get; set; } = Color.clear;

                public List<Type> SpecialBehaviours { get; set; } = new List<Type>();

                public PhyAccessoryData PhyAccessory { get; set; }
                public List<(SpriteRenderer spriteRenderer, bool Visible)> SituationalAccessories { get; set; }
                public List<Sprite> RequiredSkin { get; set; } = null;
                public string RequiredSkin_Suffix { get; set; } = "";

                public Dictionary<string, Dictionary<string, List<Sprite>>> DamageSkins { get; set; }
                public float SkinDamageAmount { get; set; } = 0;
                public string CutsceneID { get; set; } = null;

                public Sprite Icon { get; set; }
                public Sprite Illustration { get; set; }
                public string Name { get; set; }
                public string Description { get; set; }

                public string ID { get; set; }

                public LimbLayoutData LimbLayout { get; set; }
            }

            public class LimbLayoutData
            {
                public float HeadScale = 1f;
                public Dictionary<string, Vector3> Positions { get; set; }
                public Dictionary<string, Vector2> Anchors { get; set; }
            }

            public class PhyAccessoryData
            {
                public Transform objForAttach { get; set; }
                public Vector2 connectedAnchor { get; set; }
                public string layerName { get; set; }
                public int order { get; set; }
                public Sprite[] sprites { get; set; }
                public float minAngleFirstPiece { get; set; }
                public float maxAngleFirstPiece { get; set; }
                public float minAngle { get; set; }
                public float maxAngle { get; set; }
                public float pieceMass { get; set; }
                public float pieceInertia { get; set; }
                public string Name { get; set; }
                public bool HasCollision { get; set; }
                public bool CollideJustWithGround { get; set; } = true;
            }

            public Dictionary<string, TransformationStats> TransformationData { get; private set; } = new Dictionary<string, TransformationStats>();
            public List<string> FormOrder = new List<string>();

            public CharacterTransformations Owner;

            public void AddTransformation(string key, TransformationStats transformationStats)
            {
                if (!TransformationData.ContainsKey(key))
                {
                    TransformationData[key] = transformationStats;
                    if (!FormOrder.Contains(key))
                        FormOrder.Add(key);
                    if (Owner != null) Owner.NotifyFormRegistered(key, transformationStats);
                }
            }

            public List<KeyValuePair<string, TransformationStats>> GetOrderedEntries()
            {
                var result = new List<KeyValuePair<string, TransformationStats>>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (FormOrder != null)
                {
                    for (int i = 0; i < FormOrder.Count; i++)
                    {
                        string id = FormOrder[i];
                        if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                        if (!TransformationData.TryGetValue(id, out var stats) || stats == null) continue;
                        result.Add(new KeyValuePair<string, TransformationStats>(id, stats));
                    }
                }
                foreach (var kvp in TransformationData)
                {
                    if (kvp.Value == null || string.IsNullOrEmpty(kvp.Key) || !seen.Add(kvp.Key)) continue;
                    result.Add(kvp);
                    if (FormOrder != null && !FormOrder.Contains(kvp.Key))
                        FormOrder.Add(kvp.Key);
                }
                return result;
            }

            public bool MoveForm(string formId, int direction)
            {
                if (string.IsNullOrEmpty(formId) || FormOrder == null || direction == 0) return false;
                GetOrderedEntries();
                int index = FormOrder.FindIndex(id => string.Equals(id, formId, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return false;
                int target = index + (direction < 0 ? -1 : 1);
                if (target < 0 || target >= FormOrder.Count) return false;
                string temp = FormOrder[target];
                FormOrder[target] = FormOrder[index];
                FormOrder[index] = temp;
                return true;
            }

            public void ApplyFormOrder(IList<string> order)
            {
                if (FormOrder == null) FormOrder = new List<string>();
                FormOrder.Clear();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (order != null)
                {
                    for (int i = 0; i < order.Count; i++)
                    {
                        string id = order[i];
                        if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                        if (TransformationData != null && TransformationData.ContainsKey(id))
                            FormOrder.Add(id);
                    }
                }
                if (TransformationData == null) return;
                foreach (var kvp in TransformationData)
                {
                    if (kvp.Value == null || string.IsNullOrEmpty(kvp.Key) || !seen.Add(kvp.Key)) continue;
                    FormOrder.Add(kvp.Key);
                }
            }
        }

        public Transformations transformations = new Transformations();

        void Awake()
        {
            transformations.Owner = this;
        }

        public void NotifyFormRegistered(string key, Transformations.TransformationStats stats)
        {
            var identity = transform.GetComponent<DbpuCharacterIdentity>();
            if (identity == null || string.IsNullOrEmpty(identity.CharacterId) || stats == null) return;
            CharacterPowerCatalog.RegisterForm(identity.CharacterId, identity.DisplayName, key, stats);
        }

        public float GetEffectivePower(string formId, float defaultPower)
        {
            var identity = transform.GetComponent<DbpuCharacterIdentity>();
            string characterId = identity != null ? identity.CharacterId : null;
            return CharacterPowerCatalog.GetEffectivePower(characterId, formId, defaultPower);
        }

        public float GetEffectivePower(Transformations.TransformationStats stats)
        {
            if (stats == null) return 1f;
            return GetEffectivePower(stats.ID, stats.PowerMultiplier);
        }

        public float GetDisplayPower(Transformations.TransformationStats stats)
        {
            if (stats == null) return 1f;
            var identity = transform.GetComponent<DbpuCharacterIdentity>();
            string characterId = identity != null ? identity.CharacterId : null;
            return CharacterPowerCatalog.GetDisplayPower(characterId, stats.ID, stats.PowerMultiplier);
        }

        public void AddTransformation(string key, Transformations.TransformationStats transformationStats)
        {
            transformations.AddTransformation(key, transformationStats);
        }

        public void AddTransformationContextButtons(PersonBehaviour person, GameObject referenceLimb)
        {
            var pb = referenceLimb.GetComponent<PhysicalBehaviour>();

            if (pb == null) return;

            foreach (var kvp in transformations.GetOrderedEntries())
            {
                string formName = kvp.Key;
                pb.ContextMenuOptions.Buttons.Add(new ContextMenuButton(formName, formName, formName, new UnityEngine.Events.UnityAction[1]
                {
                () =>
                    {
                        ApplyTransformation(person, formName);
                        }
                    }));
            }
        }

        public void ApplyTransformation(PersonBehaviour person, string transformationKey, bool IsOnKiDepleted = false)
        {
            var menu = person != null && person.Limbs != null && person.Limbs.Length > 0 && person.Limbs[0] != null
                ? person.Limbs[0].GetComponent<TransformationContextMenu>() : null;
            var ki = person != null ? person.GetComponent<KiSystem>() : null;
            string previous = ki != null && !string.IsNullOrEmpty(ki.CurrentForm) ? ki.CurrentForm : (menu != null ? menu.Form : null);
            var transition = person != null ? person.GetComponent<KiFormTransition>() : null;
            if (IsOnKiDepleted && transition != null) transition.Cancel();
            if (!IsOnKiDepleted && KiFormTransition.Begin(this, person, previous, transformationKey,
                () => ApplyTransformationImmediate(person, transformationKey, false, previous))) return;
            if (transition != null && transition.Busy && !transition.Committing) transition.Cancel();
            ApplyTransformationImmediate(person, transformationKey, IsOnKiDepleted, previous);
        }

        private void ApplyTransformationImmediate(PersonBehaviour person, string transformationKey, bool IsOnKiDepleted, string previousOverride)
        {
            if (!transformations.TransformationData.ContainsKey(transformationKey))
            {
                Debug.LogWarning($"transformation '{transformationKey}' not found.");
                return;
            }

            var t = transformations.TransformationData[transformationKey];

            var personBehaviour = gameObject.GetComponent<PersonBehaviour>();
            TransformationContextMenu transformationMenu = null;
            if (personBehaviour != null && personBehaviour.Limbs != null && personBehaviour.Limbs.Length > 0 && personBehaviour.Limbs[0] != null)
                transformationMenu = personBehaviour.Limbs[0].gameObject.GetComponent<TransformationContextMenu>();
            if (transformationMenu == null)
            {
                var headMenuTf = person != null ? person.transform.root.Find("Head") : transform.Find("Head");
                if (headMenuTf != null)
                    transformationMenu = headMenuTf.GetComponent<TransformationContextMenu>();
            }

            string previousFormKey = !string.IsNullOrEmpty(previousOverride) ? previousOverride :
                (transformationMenu != null && !string.IsNullOrEmpty(transformationMenu.Form) ? transformationMenu.Form : transformationKey);
            if (!transformations.TransformationData.ContainsKey(previousFormKey))
                previousFormKey = transformationKey;

            var t2 = transformations.TransformationData[previousFormKey];

            var identity = person.transform.root.GetComponent<DbpuCharacterIdentity>();
            string characterId = identity != null ? identity.CharacterId : null;

            float newPower = CharacterPowerCatalog.GetEffectivePower(characterId, t.ID, t.PowerMultiplier);
            float ratio = newPower;

            float ratio2 = CharacterPowerCatalog.GetEffectivePower(characterId, t2.ID, t2.PowerMultiplier);

            var kiSystem = person.GetComponent<KiSystem>();
            if (kiSystem != null)
            {
                kiSystem.EnsureReadyForTransforms();
                kiSystem.ResetValues();
            }

            var root = person.transform.root.gameObject;
            if (!root.GetComponent<MultipliersAndStuffs>()) root.AddComponent<MultipliersAndStuffs>();
            root.GetComponent<MultipliersAndStuffs>().FormMultiplier = newPower;

            if (IsOnKiDepleted == true && transformationMenu != null)
            {
                transformationMenu.PreviousMultiplier = newPower;
                transformationMenu.Form = t.ID;
                transformationMenu.CurrentAuraID = t.AuraID;

                transformationMenu.FormSelected = transformationMenu.OriginalFormSelected;
                transformationMenu.PositionSelectedFrame = transformationMenu.OriginalSelectedFrame;

                if (gameObject.GetComponent<AuraHandler>()) gameObject.GetComponent<AuraHandler>().DestroyAura();
                if (gameObject.GetComponent<AuraHandler>()) gameObject.GetComponent<AuraHandler>().AuraActive = false;
            }

            // Innate strength is already in the limbs, and repeating it would compound on every ki depletion.
            float breakingScale = ratio / CharacterPowerCatalog.GetInnatePower(characterId);

            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                if (IsOnKiDepleted == true)
                {
                    if (!GogitoVitality.Scale(limb, ratio / ratio2))
                    {
                        if (!float.IsInfinity(limb.InitialHealth * (ratio / ratio2))) limb.InitialHealth *= ratio / ratio2;
                        if (!float.IsInfinity(limb.Health * (ratio / ratio2))) limb.Health *= ratio / ratio2;
                    }
                    if (!float.IsInfinity(limb.BreakingThreshold * breakingScale)) limb.BreakingThreshold *= breakingScale;
                }
            }

            var head = person.transform.root.Find("Head");
            if (head == null) return;

            var humanController = head.gameObject.GetComponent<HumanController>();
            bool sameForm = string.Equals(previousFormKey, transformationKey, StringComparison.Ordinal);
            bool powerChanged = Mathf.Abs(newPower - ratio2) > 0.0001f;
            if (humanController != null && (!sameForm || powerChanged || IsOnKiDepleted))
                humanController.StartChangeWeight();

            if (head.transform.root.gameObject.GetComponent<AuraHandler>())
            {
                head.transform.root.gameObject.GetComponent<AuraHandler>().CurrentAuraID = t.AuraID;
            }

            if (transformationMenu != null)
                transformationMenu.Form = t.ID;

            if (head != null)
            {
                var headMenu = head.GetComponent<TransformationContextMenu>();
                if (headMenu != null && headMenu != transformationMenu)
                {
                    headMenu.Form = t.ID;
                    headMenu.CurrentAuraID = t.AuraID;
                }
            }

            if (!sameForm || IsOnKiDepleted)
            {
                if (head.gameObject.GetComponent<SkinContextMenu>())
                {
                    var skinsMenu = head.gameObject.GetComponent<SkinContextMenu>();
                    CharacterSkins CSkins = gameObject.GetComponent<CharacterSkins>();

                    if (CSkins != null && skinsMenu.CurrentSkin != null && CSkins.skins.SkinData.ContainsKey(skinsMenu.CurrentSkin))
                    {
                        if (t.RequiredSkin_Suffix != "") skinsMenu.Skin_Suffix = t.RequiredSkin_Suffix;
                        else skinsMenu.Skin_Suffix = "";

                        int CurrentDamage = skinsMenu.CurrentDamage;

                        int SkinDamageVariantCount = CSkins.GetSkinDamageVariantCount(skinsMenu.CurrentSkin, skinsMenu.Skin_Suffix);

                        if (SkinDamageVariantCount > 0)
                        {
                            if (CurrentDamage > SkinDamageVariantCount - 1)
                            {
                                CurrentDamage = SkinDamageVariantCount - 1;
                            }

                            if (t.SkinDamageAmount > 0 && skinsMenu.SkinDamage == true)
                            {
                                int DamageAmount = (int)UnityEngine.Random.Range(0, t.SkinDamageAmount + 1);

                                if (CurrentDamage + DamageAmount < SkinDamageVariantCount) CurrentDamage += DamageAmount;
                                else CurrentDamage = SkinDamageVariantCount - 1;
                            }
                        }

                        skinsMenu.CurrentDamage = CurrentDamage;

                        CSkins.ApplySkin(person, skinsMenu.CurrentSkin, skinsMenu.Skin_Suffix, CurrentDamage);
                    }

                    else if (t.RequiredSkin != null)
                    {
                        if (skinsMenu != null) skinsMenu.Skin_Suffix = "";
                        person.ChangeSkin(t.RequiredSkin);
                    }
                }

                else if (t.RequiredSkin != null)
                {
                    person.ChangeSkin(t.RequiredSkin);
                }
            }

            if (t.HeadSprite != null) head.gameObject.ChangeLimbSprite(t.HeadSprite, LimbsStorage.FHead, LimbsStorage.BHead, LimbsStorage.DHead);

            var eyeGlow = head.gameObject.GetComponent<TransformationEyeGlow>();
            if (eyeGlow == null) eyeGlow = head.gameObject.AddComponent<TransformationEyeGlow>();
            eyeGlow.SetForm(t.ID);

            if (t.Size > 0f)
            {
                var giantForm = person.GetComponent<PiccoloGiantForm>();
                if (giantForm != null) giantForm.SyncAfterTransformation(t.Size);
                else
                {
                    float currentMeters = Mathf.Abs(person.transform.localScale.y) * 1.68f;
                    if (Mathf.Abs(currentMeters - t.Size) > 0.02f)
                    {
                        GameObject sizeLimb = null;
                        if (person.Limbs != null && person.Limbs.Length > 2 && person.Limbs[2] != null)
                            sizeLimb = person.Limbs[2].gameObject;
                        else if (person.Limbs != null)
                        {
                            for (int i = 0; i < person.Limbs.Length; i++)
                            {
                                if (person.Limbs[i] != null && person.Limbs[i].name.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    sizeLimb = person.Limbs[i].gameObject;
                                    break;
                                }
                            }
                        }
                        if (sizeLimb != null)
                        {
                            person.transform.root.transform.SetHeight(person, sizeLimb, t.Size);
                            person.ForceAllLimbsClearViaExit();
                        }
                    }
                }
            }

            var limbLayoutHandler = person.GetComponent<PersonLimbLayout>();
            if (t.LimbLayout != null)
            {
                if (limbLayoutHandler == null) limbLayoutHandler = person.gameObject.AddComponent<PersonLimbLayout>();
                limbLayoutHandler.Apply(t.LimbLayout);
            }
            else if (limbLayoutHandler != null && limbLayoutHandler.CustomLayoutActive)
            {
                limbLayoutHandler.Restore();
            }

            if (t.PhyAccessory != null && t.PhyAccessory.objForAttach != null)
            {
                foreach (Transform child in t.PhyAccessory.objForAttach)
                {
                    if (child != null && child.name == t.PhyAccessory.Name)
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                }

                PHY.Cro.CreatePhysicalCloth(t.PhyAccessory.objForAttach, t.PhyAccessory.connectedAnchor, t.PhyAccessory.layerName, t.PhyAccessory.order, t.PhyAccessory.sprites, t.PhyAccessory.minAngleFirstPiece, t.PhyAccessory.maxAngleFirstPiece, t.PhyAccessory.minAngle, t.PhyAccessory.maxAngle, t.PhyAccessory.pieceMass, t.PhyAccessory.pieceInertia, t.PhyAccessory.Name, t.PhyAccessory.HasCollision, t.PhyAccessory.CollideJustWithGround);
            }

            if (t.SituationalAccessories != null)
            {
                foreach (var Accessory in t.SituationalAccessories)
                {
                    if (Accessory.spriteRenderer == null) continue;
                    if (Accessory.Visible == false) Accessory.spriteRenderer.color = Color.clear;

                    else Accessory.spriteRenderer.color = Color.white;
                }
            }

            if (head != null && t.HairSprite != null)
            {
                var hairObject = head.Find("Hair");
                Transform hairTransform;
                var headSr = head.gameObject.GetComponent<SpriteRenderer>();

                if (hairObject == null)
                {
                    var newHairObject = new GameObject("Hair");
                    newHairObject.transform.SetParent(head);
                    newHairObject.transform.localScale = Vector3.one;

                    var hairSprite = newHairObject.AddComponent<SpriteRenderer>();
                    hairSprite.sprite = t.HairSprite;
                    if (headSr != null)
                    {
                        hairSprite.sortingLayerName = headSr.sortingLayerName;
                        hairSprite.sortingOrder = headSr.sortingOrder + 1;
                    }
                    hairTransform = newHairObject.transform;
                }

                else
                {
                    var hairSprite = hairObject.GetComponent<SpriteRenderer>();

                    if (hairSprite != null)
                    {
                        hairSprite.sprite = t.HairSprite;
                    }

                    hairTransform = hairObject;
                }

                hairTransform.localPosition = t.UseHairLocalPosition ? t.HairLocalPosition : Vector3.zero;

                var hairGlow = hairTransform.GetComponent<TransformationHairGlow>();
                if (hairGlow == null) hairGlow = hairTransform.gameObject.AddComponent<TransformationHairGlow>();
                hairGlow.SetForm(t.ID);
            }

            else if (head != null)
            {
                var hairObject = head.Find("Hair");
                if (hairObject != null)
                {
                    var hairGlow = hairObject.GetComponent<TransformationHairGlow>();
                    if (hairGlow == null) hairGlow = hairObject.gameObject.AddComponent<TransformationHairGlow>();
                    hairGlow.SetForm(t.ID);
                }
            }

            if (!string.IsNullOrEmpty(characterId))
            {
                var formDto = CustomTransformationCatalog.GetForm(characterId, transformationKey);
                if (formDto != null)
                {
                    if (CustomTransformationCatalog.BakeVirtualSpritePaths(formDto, root))
                        CustomTransformationCatalog.UpsertForm(characterId, null, formDto);
                    CustomTransformationCatalog.ApplyLimbSpritePaths(person, formDto);
                    if (!string.IsNullOrEmpty(formDto.HairSpritePath))
                    {
                        Sprite hairSrc = CustomTransformationCatalog.ResolveSpriteReference(formDto.HairSpritePath, root, true);
                        if (hairSrc != null && head != null)
                        {
                            TmHairApply.ApplyHair(
                                head,
                                hairSrc,
                                new Color(formDto.HairColorR, formDto.HairColorG, formDto.HairColorB, 1f),
                                new Vector3(formDto.HairLocalX, formDto.HairLocalY, formDto.HairLocalZ),
                                formDto.UseHairLocalPosition);
                        }
                    }
                    var formTint = root.GetComponent<CustomFormHairTintBehaviour>();
                    if (formTint == null) formTint = root.AddComponent<CustomFormHairTintBehaviour>();
                    formTint.CharacterId = characterId;
                    formTint.NotifyFormApplied(transformationKey);
                }
            }

            if (kiSystem != null)
            {
                kiSystem.ApplyTransformation(transformationKey);
                if (t.requiredKiColor != Color.clear) kiSystem.requiredColor = t.requiredKiColor;
                else kiSystem.requiredColor = Color.clear;
            }

            if (transformationKey != previousFormKey)
            {
                if (t2.SpecialBehaviours != null)
                {
                    foreach (Type oldType in t2.SpecialBehaviours)
                    {
                        if (oldType == null || (t.SpecialBehaviours != null && t.SpecialBehaviours.Contains(oldType))) continue;

                        var existing = root.GetComponent(oldType);
                        if (existing) GameObject.Destroy(existing);
                    }
                }

                if (t.SpecialBehaviours != null)
                {
                    foreach (Type newType in t.SpecialBehaviours)
                    {
                        if (newType == null || root.GetComponent(newType)) continue;

                        root.AddComponent(newType);
                    }
                }
            }

            bool targetExponential = t.SpecialBehaviours != null && t.SpecialBehaviours.Contains(typeof(ExponentialGrowth));
            var exponentialGrowth = root.GetComponent<ExponentialGrowth>();
            if (targetExponential)
            {
                if (exponentialGrowth == null) exponentialGrowth = root.AddComponent<ExponentialGrowth>();
                else exponentialGrowth.Kickstart();
            }
            else if (exponentialGrowth != null)
            {
                GameObject.Destroy(exponentialGrowth);
            }

            Debug.Log($"{person.name} transformed into {transformationKey}");
        }

        void CopyValues(Component origin, Component target)
        {
            System.Type type = origin.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (PropertyInfo prop in type.GetProperties(flags))
            {
                if (prop.CanWrite && prop.CanRead && prop.Name != "name")
                {
                    try
                    {
                        prop.SetValue(target, prop.GetValue(origin));
                    }

                    catch { }
                }
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                try
                {
                    field.SetValue(target, field.GetValue(origin));
                }

                catch { }
            }
        }
    }
}
