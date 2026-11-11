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
    public static class PotaraFusionHelpers
    {
        public static Sprite CachedPotaraSprite;
        public const float PotaraWornVisualScale = 0.4f;
        public static readonly Vector3 PotaraWornLocalPosition = new Vector3(-0.05f, -0.09f, 0f);
        public const float PotaraWorldVisualScale = 0.4f;

        public static PhysicalBehaviour TryResolvePhysicalBehaviour(Transform t)
        {
            while (t != null)
            {
                if (Global.main != null && Global.main.PhysicalObjectsInWorldByTransform.TryGetValue(t, out PhysicalBehaviour pb))
                    return pb;
                t = t.parent;
            }
            return null;
        }

        public static void AttachPotaraEarring(PersonBehaviour person)
        {
            if (person == null || !person.IsAlive()) return;

            var wearer = person.GetComponent<PotaraWearerBehaviour>();
            if (wearer == null) wearer = person.gameObject.AddComponent<PotaraWearerBehaviour>();
            if (wearer.Equipped) return;

            var head = person.Limbs[0].transform;
            var existing = head.Find("PotaraEarring");
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var potaraObject = new GameObject("PotaraEarring");
            potaraObject.transform.SetParent(head);
            potaraObject.transform.localPosition = PotaraWornLocalPosition;
            potaraObject.transform.localRotation = Quaternion.identity;
            potaraObject.transform.localScale = new Vector3(PotaraWornVisualScale, PotaraWornVisualScale, 1f);
            var potaraSprite = potaraObject.AddComponent<SpriteRenderer>();
            potaraSprite.sprite = CachedPotaraSprite;
            potaraSprite.sortingLayerName = head.gameObject.GetComponent<SpriteRenderer>().sortingLayerName;
            potaraSprite.sortingOrder = head.gameObject.GetComponent<SpriteRenderer>().sortingOrder + 2;

            wearer.Equipped = true;

            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                var relay = limb.gameObject.GetComponent<PotaraWearerLimbRelay>();
                if (relay == null) relay = limb.gameObject.AddComponent<PotaraWearerLimbRelay>();
                relay.Owner = wearer;
            }

            head.gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("RemovePotara", "Remove Potara", "Remove Potara Earring", new UnityAction[]
            {
                () => RemovePotaraEarring(person)
            }));
        }

        public static void RemovePotaraEarring(PersonBehaviour person)
        {
            if (person == null) return;

            var wearer = person.GetComponent<PotaraWearerBehaviour>();
            if (wearer != null) wearer.Equipped = false;

            var head = person.Limbs[0].transform;
            var existing = head.Find("PotaraEarring");
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
        }
    }
}
