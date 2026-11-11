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
    public class PotaraWearerBehaviour : MonoBehaviour
    {
        public bool Equipped;
        public static bool FusionInProgress;

        public void NotifyLimbContact(GameObject otherObj) => TryFusion(otherObj);

        public void OnCollisionEnter2D(Collision2D collision) => TryFusion(collision.gameObject);

        void TryFusion(GameObject otherObj)
        {
            if (!Equipped || FusionInProgress) return;

            var otherPerson = otherObj.transform.root.GetComponent<PersonBehaviour>();
            if (otherPerson == null || otherPerson == GetComponent<PersonBehaviour>()) return;

            var otherWearer = otherPerson.GetComponent<PotaraWearerBehaviour>();
            if (otherWearer == null || !otherWearer.Equipped) return;

            var thisPerson = GetComponent<PersonBehaviour>();
            string nameA = thisPerson.gameObject.name;
            string nameB = otherPerson.gameObject.name;

            var handler = thisPerson.Limbs[11].GetComponent<FusionHandler>();
            if (handler == null) handler = thisPerson.Limbs[13].GetComponent<FusionHandler>();
            if (handler == null) handler = otherPerson.Limbs[11].GetComponent<FusionHandler>();
            if (handler == null) handler = otherPerson.Limbs[13].GetComponent<FusionHandler>();
            if (handler == null) return;

            foreach (var fusion in handler.fusions.FusionData)
            {
                if (fusion.Value.FusionType != "Potara") continue;
                if (!FusionHandler.FusionPairMatches(fusion.Value.Character1, fusion.Value.Character2, nameA, nameB)) continue;

                FusionInProgress = true;
                PotaraFusionHelpers.RemovePotaraEarring(thisPerson);
                PotaraFusionHelpers.RemovePotaraEarring(otherPerson);
                handler.StartCoroutine(FusionComplete(handler, thisPerson, otherPerson, fusion.Key));
                return;
            }
        }

        IEnumerator FusionComplete(FusionHandler handler, PersonBehaviour person, PersonBehaviour otherPerson, string fusionKey)
        {
            yield return handler.Fuse(person, otherPerson, fusionKey);
            FusionInProgress = false;
        }
    }
}
