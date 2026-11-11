using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBPU
{
    // Tracks a fighter's skin selection. The screen itself is SkinMenuWindow.
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

        static SkinMenuWindow open;
        static SkinContextMenu openOwner;

        public static SkinMenuWindow OpenWindow { get { return open != null && open.Window ? open : null; } }

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
            gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("!SkinsMenu", "Skins Menu", "Skins Menu", () => OpenSkinsMenu()));
        }

        public void OpenSkinsMenu()
        {
            if (RefreshSkinList().Count == 0) return;
            if (OpenWindow != null)
            {
                if (openOwner == this) return;
                open.Window.CloseImmediate();
                open = null;
            }
            openOwner = this;
            open = new SkinMenuWindow(this);
        }

        internal void HandleWindowClosed()
        {
            if (openOwner != this) return;
            open = null;
            openOwner = null;
        }
    }
}
