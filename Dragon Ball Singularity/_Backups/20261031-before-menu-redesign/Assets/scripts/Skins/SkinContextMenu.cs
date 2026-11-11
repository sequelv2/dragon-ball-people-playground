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
    public class SkinContextMenu : MonoBehaviour
    {
        public string CurrentSkin;
        public string Skin_Suffix = "";
        public string PreviousSkin;
        public int CurrentDamage = 0;
        public int PreviousDamage = 0;
        public bool SkinDamage = false;

        public int Pages = 0;
        public int Page = 0;
        public int PreviousPage = 0;

        public PersonBehaviour person;
        public CharacterSkins SkinsData;

        public List<KeyValuePair<string, CharacterSkins.Skins.SkinInfo>> RefreshSkinList()
        {
            if (SkinsData == null || SkinsData.skins == null || SkinsData.skins.SkinData == null)
            {
                Pages = 0;
                Page = 0;
                return new List<KeyValuePair<string, CharacterSkins.Skins.SkinInfo>>();
            }

            var list = SkinsData.skins.SkinData.ToList();
            Pages = Mathf.Max(0, list.Count - 1);
            if (Page < 0) Page = 0;
            else if (Page > Pages) Page = Pages;
            return list;
        }

        public void JumpToSkin(string skinKey)
        {
            var list = RefreshSkinList();
            if (string.IsNullOrEmpty(skinKey) || list.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Key, skinKey, StringComparison.OrdinalIgnoreCase))
                {
                    Page = i;
                    PreviousPage = i;
                    return;
                }
                var info = list[i].Value;
                if (info != null && string.Equals(info.Name, skinKey, StringComparison.OrdinalIgnoreCase))
                {
                    Page = i;
                    PreviousPage = i;
                    return;
                }
            }
        }

        public void Start()
        {
            person = transform.root.gameObject.GetComponent<PersonBehaviour>();
            RefreshSkinList();
        }

        public void Awake()
        {
            gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("!SkinsMenu", "Skins Menu", "Skins Menu", () =>
            {
                var firstList = RefreshSkinList();
                if (firstList.Count == 0) return;
                if (Page < 0 || Page >= firstList.Count) Page = 0;
                DBPU.DbpuCustomSkinLoader.EnsureSkinSpritesLoaded(SkinsData, firstList[Page].Key);
                firstList = RefreshSkinList();
                if (Page < 0 || Page >= firstList.Count) return;
                var firstValue = firstList[Page].Value;
                if (firstValue == null) return;
                if (firstValue.Illustrations == null)
                    firstValue.Illustrations = new Dictionary<string, Sprite>();

                int CountOfVariants = firstValue.Illustrations.Count;
                if (CountOfVariants > 0)
                {
                    if (CurrentDamage < 0) CurrentDamage = 0;
                    else if (CurrentDamage > CountOfVariants - 1) CurrentDamage = CountOfVariants - 1;
                }

                Sprite ResolveIllustration(CharacterSkins.Skins.SkinInfo info, int damage)
                {
                    if (info?.Illustrations == null) return null;
                    string key = "Damage" + damage;
                    if (info.Illustrations.TryGetValue(key, out var spr) && spr != null) return spr;
                    if (info.Illustrations.TryGetValue("Damage0", out var d0) && d0 != null) return d0;
                    foreach (var kv in info.Illustrations)
                        if (kv.Value != null) return kv.Value;
                    return null;
                }

                GameObject Canvas = new GameObject("TCanvas");
                Canvas.transform.SetParent(GameObject.Find("Canvas").transform);
                var canvasImg = Canvas.GetOrAddComponent<Image>();
                canvasImg.color = Color.clear;
                canvasImg.raycastTarget = false;
                RectTransform canvasRt = Canvas.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.anchoredPosition = Vector2.zero;
                canvasRt.sizeDelta = new Vector2(1400f, 650f);
                canvasRt.localScale = Vector3.one;
                Canvas.AddComponent<ExistIf>().obj = gameObject;

                float skinMenuScale = 1f;

                GameObject NameArea = new GameObject("NameArea");
                NameArea.transform.SetParent(Canvas.transform);
                Image nameAreaImg = NameArea.GetOrAddComponent<Image>();
                nameAreaImg.sprite = DBPU.SNameArea;
                nameAreaImg.raycastTarget = true;
                RectTransform nameAreaRt = NameArea.GetComponent<RectTransform>();
                nameAreaRt.anchoredPosition = Vector2.zero;
                nameAreaRt.localScale = new Vector3(5.56f, 2.78f, 1f);
                nameAreaRt.localPosition = new Vector3(430f, 240f, 0f);
                var nameAreaDrag = NameArea.AddComponent<ModMenuDragResize>();
                nameAreaDrag.moveRoot = canvasRt;

                GameObject textstorageobject = new GameObject("SkinsTitle");
                textstorageobject.transform.SetParent(NameArea.transform, false);

                RectTransform textRectTransform = textstorageobject.AddComponent<RectTransform>();
                textRectTransform.anchoredPosition = new Vector2(0f, 26f);
                textRectTransform.localScale = new Vector3(1.65f, 3f);

                TextMeshProUGUI textstorage = textstorageobject.AddComponent<TextMeshProUGUI>();
                textstorage.text = "SKINS";
                textstorage.fontSize = 10;
                textstorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage.alignment = TextAlignmentOptions.Center;
                textstorage.color = new Color(0f, 0f, 0f);
                textstorage.enableWordWrapping = false;
                textstorage.raycastTarget = false;

                GameObject NameText = new GameObject("Name Text");
                NameText.transform.SetParent(NameArea.transform, false);

                RectTransform NameTextRectTransform = NameText.AddComponent<RectTransform>();
                NameTextRectTransform.anchoredPosition = new Vector2(0f, 0f);
                NameTextRectTransform.localScale = new Vector3(1.5f, 3f);
                NameTextRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                NameTextRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                NameTextRectTransform.pivot = new Vector2(0.5f, 0.5f);
                NameTextRectTransform.offsetMax = new Vector2(25f, 20f);
                NameTextRectTransform.offsetMin = new Vector2(-25f, -70f);

                TextMeshProUGUI NameTextStorage = NameText.AddComponent<TextMeshProUGUI>();
                NameTextStorage.text = firstValue.DisplayName;
                NameTextStorage.fontSize = 8;
                NameTextStorage.enableAutoSizing = true;
                NameTextStorage.fontSizeMin = 3;
                NameTextStorage.fontSizeMax = 8;
                NameTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                NameTextStorage.alignment = TextAlignmentOptions.Center;
                NameTextStorage.color = new Color(0f, 0f, 0f);
                NameTextStorage.enableWordWrapping = false;
                NameTextStorage.raycastTarget = false;

                MenuUI.AttachResizeHandle(
                    Canvas.transform,
                    () => skinMenuScale,
                    (u) =>
                    {
                        skinMenuScale = u;
                        canvasRt.localScale = new Vector3(u, u, 1f);
                    },
                    0.6f,
                    2.5f,
                    12f);

                GameObject Illustration = new GameObject("Illustration");
                Illustration.transform.SetParent(Canvas.transform);
                Illustration.GetOrAddComponent<Image>().sprite = ResolveIllustration(firstValue, CurrentDamage);
                Illustration.GetComponent<RectTransform>().anchoredPosition = new Vector2(-380f, -34f);
                Illustration.GetComponent<RectTransform>().localScale = new Vector3(5.65f, 8.251f, 1f);

                GameObject DamageVariant = new GameObject("DamageVariant");
                DamageVariant.transform.SetParent(Canvas.transform);
                DamageVariant.GetOrAddComponent<Image>().sprite = DBPU.SDamageVariantArea;
                DamageVariant.GetComponent<Image>().color = Color.clear;
                if (CountOfVariants > 1) DamageVariant.GetComponent<Image>().color = Color.white;
                DamageVariant.GetComponent<RectTransform>().anchoredPosition = new Vector2(430f, 5f);
                DamageVariant.GetComponent<RectTransform>().localScale = new Vector3(2.6f, 1.434f, 1f);

                GameObject DamageVariantText = new GameObject("Damage Text");
                DamageVariantText.transform.SetParent(DamageVariant.transform, false);

                RectTransform DamageVariantTextRectTransform = DamageVariantText.AddComponent<RectTransform>();
                DamageVariantTextRectTransform.localScale = new Vector3(3.3f, 6f, 0f);
                DamageVariantTextRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                DamageVariantTextRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                DamageVariantTextRectTransform.pivot = new Vector2(-0.075f, 1.025f);
                DamageVariantTextRectTransform.offsetMax = new Vector2(-25f, 49f);
                DamageVariantTextRectTransform.offsetMin = new Vector2(8f, 35f);

                TextMeshProUGUI DamageVariantTextStorage = DamageVariantText.AddComponent<TextMeshProUGUI>();
                DamageVariantTextStorage.text = CurrentDamage.ToString();
                DamageVariantTextStorage.fontSize = 8;
                DamageVariantTextStorage.enableAutoSizing = false;
                DamageVariantTextStorage.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                DamageVariantTextStorage.alignment = TextAlignmentOptions.Center;
                DamageVariantTextStorage.color = new Color(0f, 0f, 0f, 1f);
                if (CountOfVariants - 1 == 0) DamageVariantTextStorage.color = Color.clear;
                DamageVariantTextStorage.enableWordWrapping = false;

                GameObject NextDamage = new GameObject("NextDamage");
                NextDamage.transform.SetParent(Canvas.transform);
                NextDamage.GetOrAddComponent<Image>().sprite = DBPU.SNextDamage;
                NextDamage.GetComponent<Image>().color = Color.clear;
                if (CurrentDamage < CountOfVariants - 1) NextDamage.GetComponent<Image>().color = Color.white;
                NextDamage.GetComponent<RectTransform>().anchoredPosition = new Vector2(636f, 5f);
                NextDamage.GetComponent<RectTransform>().localScale = new Vector3(1.434f, 1.434f, 1f);

                GameObject PrevioursDamage = new GameObject("PreviousDamage");
                PrevioursDamage.transform.SetParent(Canvas.transform);
                PrevioursDamage.GetOrAddComponent<Image>().sprite = DBPU.SPreviousDamage;
                PrevioursDamage.GetComponent<Image>().color = Color.clear;
                if (CurrentDamage > 0) PrevioursDamage.GetComponent<Image>().color = Color.white;
                PrevioursDamage.GetComponent<RectTransform>().anchoredPosition = new Vector2(224f, 5f);
                PrevioursDamage.GetComponent<RectTransform>().localScale = new Vector3(1.434f, 1.434f, 1f);

                NextDamage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    if (CurrentDamage >= CountOfVariants - 1) return;

                    CurrentDamage += 1;
                    DamageVariantTextStorage.text = CurrentDamage.ToString();
                    Illustration.GetComponent<Image>().sprite = ResolveIllustration(firstValue, CurrentDamage);

                    if (CurrentDamage >= CountOfVariants - 1)
                    {
                        NextDamage.GetComponent<Image>().color = Color.clear;
                    }

                    if (CurrentDamage > 0)
                    {
                        PrevioursDamage.GetComponent<Image>().color = Color.white;
                    }
                });

                PrevioursDamage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    if (CurrentDamage <= 0) return;

                    CurrentDamage -= 1;
                    DamageVariantTextStorage.text = CurrentDamage.ToString();
                    Illustration.GetComponent<Image>().sprite = ResolveIllustration(firstValue, CurrentDamage);

                    if (CurrentDamage < CountOfVariants)
                    {
                        NextDamage.GetComponent<Image>().color = Color.white;
                    }

                    if (CurrentDamage <= 0)
                    {
                        PrevioursDamage.GetComponent<Image>().color = Color.clear;
                    }
                });

                GameObject NextPage = new GameObject("NextPage");
                NextPage.transform.SetParent(Canvas.transform);
                NextPage.GetOrAddComponent<Image>().sprite = DBPU.SNext;
                NextPage.GetComponent<Image>().color = Color.clear;
                if (Page < Pages) NextPage.GetComponent<Image>().color = Color.white;
                NextPage.GetComponent<RectTransform>().anchoredPosition = new Vector2(-26f, -34f);
                NextPage.GetComponent<RectTransform>().localScale = new Vector3(1.434f, 6.636f, 1f);

                GameObject PrevioursPage = new GameObject("PreviousPage");
                PrevioursPage.transform.SetParent(Canvas.transform);
                PrevioursPage.GetOrAddComponent<Image>().sprite = DBPU.SPrevious;
                PrevioursPage.GetComponent<Image>().color = Color.clear;
                if (Page > 0) PrevioursPage.GetComponent<Image>().color = Color.white;
                PrevioursPage.GetComponent<RectTransform>().anchoredPosition = new Vector2(-734f, -34f);
                PrevioursPage.GetComponent<RectTransform>().localScale = new Vector3(1.434f, 6.636f, 1f);

                NextPage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    firstList = RefreshSkinList();
                    if (Page >= Pages || firstList.Count == 0) return;

                    Page += 1;

                    firstValue = firstList[Page].Value;
                    if (firstValue == null) return;
                    DBPU.DbpuCustomSkinLoader.EnsureSkinSpritesLoaded(SkinsData, firstList[Page].Key);
                    firstList = RefreshSkinList();
                    if (Page < 0 || Page >= firstList.Count) return;
                    firstValue = firstList[Page].Value;
                    if (firstValue == null) return;
                    if (firstValue.Illustrations == null)
                        firstValue.Illustrations = new Dictionary<string, Sprite>();
                    CountOfVariants = firstValue.Illustrations.Count;
                    if (CurrentDamage > CountOfVariants - 1) CurrentDamage = Mathf.Max(0, CountOfVariants - 1);

                    DamageVariantTextStorage.text = CurrentDamage.ToString();
                    NameTextStorage.text = firstValue.DisplayName;

                    Illustration.GetComponent<Image>().sprite = ResolveIllustration(firstValue, CurrentDamage);

                    if (CurrentDamage >= CountOfVariants - 1)
                    {
                        NextDamage.GetComponent<Image>().color = Color.clear;
                    }

                    if (CurrentDamage > 0)
                    {
                        PrevioursDamage.GetComponent<Image>().color = Color.white;
                    }

                    if (CountOfVariants - 1 == 0)
                    {
                        NextDamage.GetComponent<Image>().color = Color.clear;
                        PrevioursDamage.GetComponent<Image>().color = Color.clear;

                        DamageVariant.GetComponent<Image>().color = Color.clear;
                        DamageVariantTextStorage.color = Color.clear;
                    }

                    else
                    {
                        DamageVariant.GetComponent<Image>().color = Color.white;
                        DamageVariantTextStorage.color = new Color(0f, 0f, 0f, 1f);
                    }

                    if (Page > 0)
                    {
                        PrevioursPage.GetComponent<Image>().color = Color.white;
                    }

                    if (Page >= Pages)
                    {
                        NextPage.GetComponent<Image>().color = Color.clear;
                    }
                });

                PrevioursPage.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    firstList = RefreshSkinList();
                    if (Page <= 0 || firstList.Count == 0) return;

                    Page -= 1;

                    firstValue = firstList[Page].Value;
                    if (firstValue == null) return;
                    DBPU.DbpuCustomSkinLoader.EnsureSkinSpritesLoaded(SkinsData, firstList[Page].Key);
                    firstList = RefreshSkinList();
                    if (Page < 0 || Page >= firstList.Count) return;
                    firstValue = firstList[Page].Value;
                    if (firstValue == null) return;
                    if (firstValue.Illustrations == null)
                        firstValue.Illustrations = new Dictionary<string, Sprite>();
                    CountOfVariants = firstValue.Illustrations.Count;
                    if (CurrentDamage > CountOfVariants - 1) CurrentDamage = Mathf.Max(0, CountOfVariants - 1);

                    DamageVariantTextStorage.text = CurrentDamage.ToString();
                    NameTextStorage.text = firstValue.DisplayName;

                    Illustration.GetComponent<Image>().sprite = ResolveIllustration(firstValue, CurrentDamage);

                    if (CurrentDamage < CountOfVariants)
                    {
                        NextDamage.GetComponent<Image>().color = Color.white;
                    }

                    if (CurrentDamage <= 0)
                    {
                        PrevioursDamage.GetComponent<Image>().color = Color.clear;
                    }

                    if (CountOfVariants - 1 == 0)
                    {
                        NextDamage.GetComponent<Image>().color = Color.clear;
                        PrevioursDamage.GetComponent<Image>().color = Color.clear;

                        DamageVariant.GetComponent<Image>().color = Color.clear;
                        DamageVariantTextStorage.color = Color.clear;
                    }

                    else
                    {
                        DamageVariant.GetComponent<Image>().color = Color.white;
                        DamageVariantTextStorage.color = new Color(0f, 0f, 0f, 1f);
                    }

                    if (Page < Pages)
                    {
                        NextPage.GetComponent<Image>().color = Color.white;
                    }

                    if (Page <= 0)
                    {
                        PrevioursPage.GetComponent<Image>().color = Color.clear;
                    }
                });

                GameObject Cancel = new GameObject("Cancel");
                Cancel.transform.SetParent(Canvas.transform);
                Cancel.GetOrAddComponent<Image>().sprite = DBPU.SCancel;
                Cancel.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    CurrentDamage = PreviousDamage;
                    Page = PreviousPage;

                    Destroy(Canvas);
                });
                Cancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(430f, -366f);
                Cancel.GetComponent<RectTransform>().localScale = new Vector3(4.394f, 1.614f, 1f);

                GameObject textstorageobject1 = new GameObject("cancel text");
                textstorageobject1.transform.SetParent(Cancel.transform, false);

                RectTransform textRectTransform1 = textstorageobject1.AddComponent<RectTransform>();
                textRectTransform1.anchoredPosition = new Vector2(0f, 0f);
                textRectTransform1.localScale = new Vector3(0.55f, 1f);
                textRectTransform1.localScale *= 3f;

                TextMeshProUGUI textstorage1 = textstorageobject1.AddComponent<TextMeshProUGUI>();
                textstorage1.text = "CANCEL";
                textstorage1.fontSize = 10;
                textstorage1.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage1.alignment = TextAlignmentOptions.Center;
                textstorage1.color = new Color(1f, 1f, 1f);
                textstorage1.enableWordWrapping = false;
                textstorage1.autoSizeTextContainer = true;

                GameObject Accept = new GameObject("Accept");
                Accept.transform.SetParent(Canvas.transform);
                Accept.GetOrAddComponent<Image>().sprite = DBPU.SAccept;
                Accept.GetComponent<RectTransform>().anchoredPosition = new Vector2(430f, -175f);
                Accept.GetComponent<RectTransform>().localScale = new Vector3(4.394f, 1.614f, 1f);
                Accept.GetOrAddComponent<Button>().onClick.AddListener(() =>
                {
                    firstList = RefreshSkinList();
                    if (firstList.Count == 0) return;
                    var value = firstList[Page].Value;

                    SkinsData.ApplySkin(person, value.Name, Skin_Suffix, CurrentDamage);

                    PreviousDamage = CurrentDamage;
                    CurrentSkin = value.Name;
                    PreviousSkin = CurrentSkin;
                    PreviousPage = Page;

                    Destroy(Canvas);
                });
                GameObject textstorageobject2 = new GameObject("accept text");
                textstorageobject2.transform.SetParent(Accept.transform, false);

                RectTransform textRectTransform2 = textstorageobject2.AddComponent<RectTransform>();
                textRectTransform2.anchoredPosition = new Vector2(0f, 0f);
                textRectTransform2.localScale = new Vector3(0.55f, 1f);
                textRectTransform2.localScale *= 3f;

                TextMeshProUGUI textstorage2 = textstorageobject2.AddComponent<TextMeshProUGUI>();
                textstorage2.text = "ACCEPT";
                textstorage2.fontSize = 10;
                textstorage2.font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                textstorage2.alignment = TextAlignmentOptions.Center;
                textstorage2.color = new Color(1f, 1f, 1f);
                textstorage2.enableWordWrapping = false;
                textstorage2.autoSizeTextContainer = true;
            }));
        }
    }
}
