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
    public static class DbpuAutoLimbCompat
    {
        static readonly HashSet<int> PendingPersonSync = new HashSet<int>();

        public static void SyncPerson(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null) return;
            var t = AccessTools.TypeByName("Mod.AutoLimbReposition");
            if (t == null) return;
            var anchorField = AccessTools.Field(t, "AnchorPosition");
            if (anchorField == null) return;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null || limb.Joint == null) continue;
                var aut = limb.GetComponent(t) as MonoBehaviour;
                if (aut == null) continue;
                try { anchorField.SetValue(aut, limb.Joint.connectedAnchor); }
                catch { }
            }
        }

        public static void SyncPersonDelayed(PersonBehaviour person, params float[] delays)
        {
            if (person == null) return;
            int id = person.GetInstanceID();
            if (!PendingPersonSync.Add(id)) return;
            if (delays == null || delays.Length == 0)
                delays = new[] { 0.05f, 0.2f, 0.45f };
            var go = new GameObject("DbpuAutoLimbSyncDelay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DbpuAutoLimbSyncHost>().Run(person, delays, id);
        }

        internal static void ClearPending(int personId)
        {
            PendingPersonSync.Remove(personId);
        }
    }
}
