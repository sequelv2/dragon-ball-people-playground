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
    class DbpuAutoLimbSyncHost : MonoBehaviour
    {
        public void Run(PersonBehaviour person, float[] delays, int personId)
        {
            StartCoroutine(RunRoutine(person, delays, personId));
        }

        IEnumerator RunRoutine(PersonBehaviour person, float[] delays, int personId)
        {
            float elapsed = 0f;
            for (int i = 0; i < delays.Length; i++)
            {
                float wait = Mathf.Max(0.01f, delays[i] - elapsed);
                yield return new WaitForSecondsRealtime(wait);
                elapsed = delays[i];
                if (person == null)
                {
                    DbpuAutoLimbCompat.ClearPending(personId);
                    Destroy(gameObject);
                    yield break;
                }
                DbpuAutoLimbCompat.SyncPerson(person);
            }
            DbpuAutoLimbCompat.ClearPending(personId);
            Destroy(gameObject);
        }
    }
}
