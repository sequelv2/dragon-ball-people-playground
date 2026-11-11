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
    public class CharacterSkins : MonoBehaviour
    {
        public class Skins
        {
            public class SkinInfo
            {
                public Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>> Skins { get; set; }



                //public Dictionary<string, Dictionary<string, List<Sprite>>> Skins { get; set; }

                public Dictionary<string, Dictionary<string, List<Sprite>>> RequiredSkins { get; set; }
                public List<PhyAccessoryData> PhyAccessories { get; set; }

                ///public Sprite Illustration { get; set; }
                public Dictionary<string, Sprite> Illustrations { get; set; }
                public string DisplayName { get; set; }

                public string Name { get; set; }
                public string PackFolderName { get; set; }
                public Dictionary<string, Sprite> HairSprites { get; set; }
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

            public Dictionary<string, SkinInfo> SkinData { get; private set; } = new Dictionary<string, SkinInfo>();

            public void AddSkin(string key, SkinInfo skinInfo)
            {
                if (!SkinData.ContainsKey(key))
                {
                    SkinData[key] = skinInfo;
                }
            }

            public void UpsertSkin(string key, SkinInfo skinInfo)
            {
                if (string.IsNullOrEmpty(key) || skinInfo == null) return;
                SkinData[key] = skinInfo;
            }

            public bool RemoveSkin(string key)
            {
                return !string.IsNullOrEmpty(key) && SkinData.Remove(key);
            }
        }

        public Skins skins = new Skins();

        public string GetSkinTransformationTargetID(string skinKey, string suffixKey)
        {
            string fullSkinKey = skinKey + suffixKey;
            string transformationTargetID = "None";

            if (!skins.SkinData.ContainsKey(skinKey)) return transformationTargetID;

            var skinData = skins.SkinData[skinKey];

            if (!skinData.Skins.ContainsKey(fullSkinKey)) return transformationTargetID;

            var transformationVariants = skinData.Skins[fullSkinKey];

            var head = gameObject.GetComponent<PersonBehaviour>().Limbs[0].gameObject;
            var transformationMenu = head.GetComponent<TransformationContextMenu>();

            if (transformationMenu != null && !string.IsNullOrEmpty(transformationMenu.Form))
            {
                if (transformationVariants.ContainsKey(transformationMenu.Form))
                {
                    return transformationMenu.Form;
                }
            }

            if (transformationVariants.ContainsKey("None")) return "None";
            //if (transformationVariants.ContainsKey("Base")) return "Base";

            return transformationVariants.Keys.First();
        }

        public int GetSkinDamageVariantCount(string skinKey, string suffixKey)
        {
            string fullSkinKey = skinKey + suffixKey;

            if (!skins.SkinData.ContainsKey(skinKey)) return 0;

            var skinData = skins.SkinData[skinKey];

            if (!skinData.Skins.ContainsKey(fullSkinKey)) return 0;

            string transformationTargetID = GetSkinTransformationTargetID(skinKey, suffixKey);

            if (!skinData.Skins[fullSkinKey].ContainsKey(transformationTargetID)) return 0;

            return skinData.Skins[fullSkinKey][transformationTargetID].Count;
        }

        public void ApplySkin(PersonBehaviour person, string skinKey, string suffixKey, int Damage = 0)
        {
            if (!skins.SkinData.ContainsKey(skinKey))
            {
                Debug.LogWarning($"skin '{skinKey}' not found.");
                return;
            }

            DBPU.DbpuCustomSkinLoader.EnsureSkinSpritesLoaded(this, skinKey);

            var t = skins.SkinData[skinKey];

            string fullSkinKey = skinKey + suffixKey;
            string transformationTargetID = GetSkinTransformationTargetID(skinKey, suffixKey);
            string damageKey = "Damage" + Damage;

            if (!t.Skins.ContainsKey(fullSkinKey))
            {
                Debug.LogWarning($"skin variant '{fullSkinKey}' not found.");
                return;
            }

            if (!t.Skins[fullSkinKey].ContainsKey(transformationTargetID))
            {
                Debug.LogWarning($"skin transformation target '{transformationTargetID}' not found in skin '{fullSkinKey}'.");
                return;
            }

            if (!t.Skins[fullSkinKey][transformationTargetID].ContainsKey(damageKey))
            {
                Debug.LogWarning($"skin damage variant '{damageKey}' not found in skin '{fullSkinKey}' for transformation '{transformationTargetID}'.");
                return;
            }

            var selectedSprites = t.Skins[fullSkinKey][transformationTargetID][damageKey];
            if (selectedSprites == null || selectedSprites.Count < 14)
            {
                Debug.LogWarning($"skin sprites incomplete for '{fullSkinKey}' damage '{damageKey}'.");
                return;
            }

            Transform headTf = null;
            var personOnSelf = gameObject.GetComponent<PersonBehaviour>();
            if (personOnSelf?.Limbs != null)
            {
                for (int i = 0; i < personOnSelf.Limbs.Length; i++)
                {
                    if (personOnSelf.Limbs[i] != null
                        && personOnSelf.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headTf = personOnSelf.Limbs[i].transform;
                        break;
                    }
                }
                if (headTf == null && personOnSelf.Limbs.Length > 0 && personOnSelf.Limbs[0] != null)
                    headTf = personOnSelf.Limbs[0].transform;
            }
            Skins.SkinInfo t2 = null;
            if (headTf != null)
            {
                var skinMenu = headTf.GetComponent<SkinContextMenu>();
                if (skinMenu != null
                    && !string.IsNullOrEmpty(skinMenu.CurrentSkin)
                    && skins.SkinData.TryGetValue(skinMenu.CurrentSkin, out var currentInfo))
                    t2 = currentInfo;
            }
            if (t2 == null) t2 = t;

            Sprite headSprite = selectedSprites[0];
            List<Sprite> skin = new List<Sprite>
            {
                headSprite,
                selectedSprites[1],
                selectedSprites[2],
                selectedSprites[3],
                selectedSprites[4],
                selectedSprites[5],
                selectedSprites[6],
                selectedSprites[7],
                selectedSprites[8],
                selectedSprites[9],
                selectedSprites[10],
                selectedSprites[11],
                selectedSprites[12],
                selectedSprites[13]
            };

            person.ChangeSkin(skin);

            if (headSprite != null)
            {
                if (headTf == null && person != null)
                {
                    headTf = person.transform.root.Find("Head");
                    if (headTf == null && person.Limbs != null)
                    {
                        for (int i = 0; i < person.Limbs.Length; i++)
                        {
                            if (person.Limbs[i] != null
                                && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                headTf = person.Limbs[i].transform;
                                break;
                            }
                        }
                    }
                }
                if (headTf != null)
                    headTf.gameObject.ChangeLimbSprite(headSprite, LimbsStorage.FHead, LimbsStorage.BHead, LimbsStorage.DHead);
            }

            ApplySkinHair(person, t, damageKey, headTf);

            if (t2 != null && t2.PhyAccessories != null)
            {
                foreach (Skins.PhyAccessoryData data in t2.PhyAccessories)
                {
                    if (data == null || data.objForAttach == null) continue;
                    foreach (Transform child in data.objForAttach)
                    {
                        if (child != null && child.name == data.Name)
                        {
                            GameObject.Destroy(child.gameObject);
                        }
                    }
                }
            }

            if (t.PhyAccessories != null)
            {
                foreach (Skins.PhyAccessoryData data in t.PhyAccessories)
                {
                    if (data == null || data.objForAttach == null || data.sprites == null) continue;
                    PHY.Cro.CreatePhysicalCloth(data.objForAttach, data.connectedAnchor, data.layerName, data.order, data.sprites, data.minAngleFirstPiece, data.maxAngleFirstPiece, data.minAngle, data.maxAngle, data.pieceMass, data.pieceInertia, data.Name, data.HasCollision, data.CollideJustWithGround);
                }
            }
        }

        static Sprite ResolveFormHairSprite(PersonBehaviour person)
        {
            if (person == null) return null;
            var root = person.transform.root;
            Transform head = root.Find("Head");
            if (head == null && person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    if (person.Limbs[i] != null
                        && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        head = person.Limbs[i].transform;
                        break;
                    }
                }
            }
            if (head == null) return null;

            string formId = null;
            var menu = head.GetComponent<TransformationContextMenu>();
            if (menu != null) formId = menu.Form;

            var ct = root.GetComponent<CharacterTransformations>();
            if (ct?.transformations?.TransformationData != null && !string.IsNullOrEmpty(formId)
                && ct.transformations.TransformationData.TryGetValue(formId, out var stats)
                && stats != null && stats.HairSprite != null)
                return stats.HairSprite;

            var identity = root.GetComponent<DbpuCharacterIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.CharacterId) && !string.IsNullOrEmpty(formId))
            {
                var dto = CustomTransformationCatalog.GetForm(identity.CharacterId, formId);
                if (dto != null && !string.IsNullOrEmpty(dto.HairSpritePath))
                {
                    Sprite fromDto = CustomTransformationCatalog.ResolveSpriteReference(dto.HairSpritePath, root.gameObject, true);
                    if (fromDto != null) return fromDto;
                }
            }

            if (ct?.transformations?.TransformationData != null
                && ct.transformations.TransformationData.TryGetValue("Base", out var baseStats)
                && baseStats != null && baseStats.HairSprite != null)
                return baseStats.HairSprite;

            return null;
        }

        void ApplySkinHair(PersonBehaviour person, Skins.SkinInfo info, string damageKey, Transform headTf)
        {
            if (person == null) return;
            if (headTf == null)
            {
                headTf = person.transform.root.Find("Head");
                if (headTf == null && person.Limbs != null)
                {
                    for (int i = 0; i < person.Limbs.Length; i++)
                    {
                        if (person.Limbs[i] != null
                            && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            headTf = person.Limbs[i].transform;
                            break;
                        }
                    }
                }
            }
            if (headTf == null) return;

            Sprite packHair = null;
            if (info?.HairSprites != null)
            {
                if (!string.IsNullOrEmpty(damageKey) && info.HairSprites.TryGetValue(damageKey, out packHair) && packHair != null)
                { }
                else if (info.HairSprites.TryGetValue("Damage0", out packHair) && packHair != null)
                { }
                else
                    packHair = null;
            }

            if (packHair != null)
            {
                TmHairApply.ApplyHair(headTf, packHair, Color.white, Vector3.zero, false);
                return;
            }

            Sprite formHair = ResolveFormHairSprite(person);
            if (formHair != null)
                TmHairApply.ApplyHair(headTf, formHair, Color.white, Vector3.zero, false);
        }
    }
}
