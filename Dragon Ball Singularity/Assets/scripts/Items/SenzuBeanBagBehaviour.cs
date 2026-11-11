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
    public class SenzuBeanBagBehaviour : MonoBehaviour
    {
        public bool Open = false;

        public void Use()
        {
            if (Open)
            {
                GameObject Senzu = GameObject.Instantiate(ModAPI.FindSpawnable("[DBPU] Senzu Bean").Prefab, transform.position + (transform.up * 0.2f), Quaternion.identity);
                CatalogBehaviour.PerformMod(ModAPI.FindSpawnable("[DBPU] Senzu Bean"), Senzu);
                Senzu.gameObject.GetComponent<PhysicalBehaviour>().SpawnSpawnParticles = false;
            }
        }
    }
}
