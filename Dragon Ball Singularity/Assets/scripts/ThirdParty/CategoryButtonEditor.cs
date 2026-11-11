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

public class CategoryButtonEditor : MonoBehaviour
{
    public ItemButtonBehaviour GetIcon(string spawnableAsset)
    {
        if (string.IsNullOrEmpty(spawnableAsset)) return null;
        foreach (ItemButtonBehaviour item in GameObject.FindObjectsOfType<ItemButtonBehaviour>(true))
        {
            if (item == null || item.Item == null) continue;
            if (item.Item.name == spawnableAsset) return item;
        }
        return null;
    }

    public List<ItemButtonBehaviour> GetIconsByOrderOverride(string subcategory)
    {
        List<ItemButtonBehaviour> items = new List<ItemButtonBehaviour>();
        if (string.IsNullOrEmpty(subcategory)) return items;
        foreach (ItemButtonBehaviour item in GameObject.FindObjectsOfType<ItemButtonBehaviour>(true))
        {
            if (item == null || item.Item == null || string.IsNullOrEmpty(item.Item.NameToOrderBy)) continue;
            if (item.Item.NameToOrderBy.Contains(subcategory) && item.Item.NameToOrderBy != subcategory)
                items.Add(item);
        }
        return items;
    }

    public void Start()
    {
        if (DBPU.DBPU.Subcategories == null) return;

        foreach (var subcat in DBPU.DBPU.Subcategories)
        {
            if (subcat == null || string.IsNullOrEmpty(subcat.Name)) continue;
            var subcatbutton = GetIcon(subcat.Name);
            if (subcatbutton == null || subcatbutton.gameObject == null) continue;

            if (subcatbutton.GetComponent<SubcategorySpawnableEnabler>() != null) continue;

            var subspawnen = subcatbutton.gameObject.AddComponent<SubcategorySpawnableEnabler>();
            var list = new List<GameObject>();
            AudioSource a = null;
            var uiSound = GameObject.Find("UI Sound Controller");
            if (uiSound != null) a = uiSound.GetComponent<AudioSource>();

            foreach (var spawnable in GetIconsByOrderOverride(subcat.OrderOverride))
            {
                if (spawnable == null || spawnable.gameObject == null || spawnable.Item == null) continue;
                var go = spawnable.gameObject;
                bool wasActive = go.activeSelf;
                try
                {
                    if (!wasActive) go.SetActive(true);
                }
                catch
                {
                    continue;
                }

                list.Add(go);
                subspawnen.spawnables.Add(go);

                if (go.transform.childCount > 3)
                {
                    var child3 = go.transform.GetChild(3);
                    if (child3 != null)
                    {
                        var btn = child3.GetComponent<Button>();
                        if (btn != null) Destroy(btn);
                        var del = child3.GetComponent<DeleteContraptionButtonBehaviour>();
                        if (del != null) Destroy(del);
                        child3.localScale = new Vector3(2, 2, 2);
                        child3.localPosition = new Vector3(121.6356f, -120.8005f, 0);
                        child3.gameObject.SetActive(true);
                        var tip = child3.GetComponent<HasTooltipBehaviour>();
                        if (tip != null) tip.Text = "This item belongs to: " + subcat.Name;
                        var img = child3.GetComponent<Image>();
                        if (img != null && subcat.EmptyIcon != null) img.sprite = subcat.EmptyIcon;
                    }
                }
                if (go.transform.childCount > 4)
                {
                    var child4 = go.transform.GetChild(4);
                    if (child4 != null) child4.gameObject.SetActive(false);
                }

                go.SetActive(false);
            }

            var button = subcatbutton.GetComponent<Button>();
            var image = subcatbutton.GetComponent<Image>();
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (a != null && DBPU.DBPU.SubcategoryOpen != null)
                    a.PlayOneShot(DBPU.DBPU.SubcategoryOpen);
                subspawnen.Open = !subspawnen.Open;
                if (image != null && subcat.Icon != null && subcat.OpenIcon != null)
                    image.sprite = (image.sprite == subcat.Icon ? subcat.OpenIcon : subcat.Icon);

                foreach (var item in list)
                {
                    if (item != null) item.SetActive(subspawnen.Open);
                }
            });
        }
    }
}
