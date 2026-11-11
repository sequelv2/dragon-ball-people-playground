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
    public class PotaraSpawnableBehaviour : MonoBehaviour
    {
        bool attached;

        public void OnCollisionEnter2D(Collision2D other) => TryAttach(other.gameObject);

        public void OnTriggerEnter2D(Collider2D other) => TryAttach(other.gameObject);

        public void FixedUpdate()
        {
            if (attached) return;
            var hits = Physics2D.OverlapCircleAll(transform.position, 0.15f);
            foreach (var hit in hits)
            {
                if (hit.gameObject != gameObject) TryAttach(hit.gameObject);
            }
        }

        void TryAttach(GameObject other)
        {
            if (attached) return;

            var pb = PotaraFusionHelpers.TryResolvePhysicalBehaviour(other.transform);
            if (pb == null) return;

            var limb = pb.GetComponent<LimbBehaviour>();
            if (limb == null || limb.name != "Head") return;

            var person = limb.Person;
            if (person == null || !person.IsAlive()) return;

            attached = true;
            PotaraFusionHelpers.AttachPotaraEarring(person);
            Destroy(gameObject);
        }
    }
}
