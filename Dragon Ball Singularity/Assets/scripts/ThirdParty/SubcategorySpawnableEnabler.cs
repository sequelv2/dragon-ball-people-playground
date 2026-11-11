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

public class SubcategorySpawnableEnabler : MonoBehaviour
{
    public bool Open = false;
    public List<GameObject> spawnables = new List<GameObject>();

    public void OnEnable()
    {
        foreach (var spawn in spawnables)
        {
            spawn.SetActive(Open);
        }
    }
}
