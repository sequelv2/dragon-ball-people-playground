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
    public static class DbpuLimbDurability
    {
        public const float DefaultResistance = 25f;

        // Permission belongs to one synchronous damage operation and one limb.
        // Strong attacks must not permanently remove a fighter's durability.
        static readonly Dictionary<LimbBehaviour, int> dismembermentScopes = new Dictionary<LimbBehaviour, int>();

        public static IDisposable AllowDismemberment(LimbBehaviour limb)
        {
            return new DismembermentScope(limb);
        }

        sealed class DismembermentScope : IDisposable
        {
            LimbBehaviour limb;
            public DismembermentScope(LimbBehaviour value)
            {
                limb = value;
                if (!limb) return;
                int count;
                dismembermentScopes.TryGetValue(limb, out count);
                dismembermentScopes[limb] = count + 1;
            }
            public void Dispose()
            {
                if (ReferenceEquals(limb, null)) return;
                int count;
                if (dismembermentScopes.TryGetValue(limb, out count))
                {
                    if (count <= 1) dismembermentScopes.Remove(limb);
                    else dismembermentScopes[limb] = count - 1;
                }
                limb = null;
            }
        }

        public static void EnsurePerson(PersonBehaviour person)
        {
            if (person == null) return;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                limb.gameObject.GetOrAddComponent<DbpuLimbDurabilityLink>();

                if (limb.Joint != null)
                {
                    limb.Joint.breakForce = float.MaxValue;
                    limb.Joint.breakTorque = float.MaxValue;
                }
            }
        }

        public static float GetResistance(LimbBehaviour limb)
        {
            if (limb == null) return 1f;

            var hcLink = limb.GetComponent<HumanController.HarmonyPatchLink>();
            if (hcLink != null && hcLink.Controller != null)
            {
                float r = hcLink.Controller.Features.LimbBreakResistance;
                if (r > 1f) return r;
            }

            var link = limb.GetComponent<DbpuLimbDurabilityLink>();
            if (link != null && link.Resistance > 1f) return link.Resistance;

            return 1f;
        }

        public static bool IsProtected(LimbBehaviour limb)
        {
            return GetResistance(limb) > 1f;
        }

        public static bool IsDbpuFighter(LimbBehaviour limb)
        {
            if (!limb) return false;
            if (limb.GetComponent<DbpuLimbDurabilityLink>()) return true;
            return limb.Person && limb.Person.GetComponent<DbpuCharacterIdentity>();
        }

        // Piccolo regrows lost limbs and a Majin reforms its rubber body, so
        // they are the only DBPU fighters whose limbs can be torn off.
        public static bool CanLoseLimbs(PersonBehaviour person)
        {
            if (!person) return false;
            if (person.GetComponent<BuuRegeneration>()) return true;
            var identity = person.GetComponent<DbpuCharacterIdentity>();
            if (identity && !string.IsNullOrEmpty(identity.CharacterId))
                return identity.CharacterId == "Piccolo" || identity.CharacterId.EndsWith("/Piccolo", StringComparison.OrdinalIgnoreCase);
            return person.GetComponent<PiccoloGiantForm>();
        }

        public static bool ShouldBlockDismemberment(LimbBehaviour limb)
        {
            if (IsDbpuFighter(limb) && !CanLoseLimbs(limb.Person)) return true;
            if (limb && dismembermentScopes.ContainsKey(limb)) return false;
            // A Majin survives severing by reforming its rubber body. Keep its
            // combat strength, but let the native slicing/crushing actually happen.
            if (limb && limb.Person && limb.Person.GetComponent<BuuRegeneration>()) return false;
            return IsProtected(limb);
        }
    }
}
