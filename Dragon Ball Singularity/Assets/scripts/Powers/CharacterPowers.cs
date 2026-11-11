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
    public class CharacterPowers : MonoBehaviour
    {
        public static class PowerGroups
        {
            public const string KiBlast = "KiBlast";
            public const string Beam = "Beam";
            public const string Solar = "Solar";
            public const string Shield = "Shield";
            public const string FinalExplosion = "FinalExplosion";
            public const string Hellzone = "Hellzone";
            public const string Ball = "Ball";
            public const string GogetaSpecial = "GogetaSpecial";
            public const string StraightBeam = "StraightBeam";
            public const string Growth = "Growth";
            public const string DisasterRay = "DisasterRay";
            public const string EyeLaser = "EyeLaser";
            public const string BeerusSpecial = "BeerusSpecial";
        }

        public class PowerStats
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public Sprite Icon { get; set; }
            public Sprite Illustration { get; set; }
            public string PowerType { get; set; }
            public bool Active { get; set; }
            public Func<bool> IsActive { get; set; }
            public Func<string> GetStatusText { get; set; }
            public Action Apply { get; set; }
            public Action Disable { get; set; }
            public Func<bool> GetModeBarrage { get; set; }
            public Action<bool> SetModeBarrage { get; set; }
            public bool Passive { get; set; }
        }

        public Dictionary<string, PowerStats> PowerData = new Dictionary<string, PowerStats>();
        public List<string> PowerOrder = new List<string>();

        public void AddPower(PowerStats stats)
        {
            if (PowerData.ContainsKey(stats.ID)) return;
            PowerData[stats.ID] = stats;
            PowerOrder.Add(stats.ID);
        }

        public void InsertPowerAtStart(PowerStats stats)
        {
            if (PowerData.ContainsKey(stats.ID)) return;
            PowerData[stats.ID] = stats;
            PowerOrder.Insert(0, stats.ID);
        }

        public void MovePowerBlockToStart(IReadOnlyList<string> idsInOrder)
        {
            if (idsInOrder == null || idsInOrder.Count == 0) return;
            var present = new List<string>();
            for (int i = 0; i < idsInOrder.Count; i++)
            {
                string id = idsInOrder[i];
                if (PowerData.ContainsKey(id)) present.Add(id);
            }
            if (present.Count == 0) return;
            for (int i = 0; i < idsInOrder.Count; i++)
                PowerOrder.Remove(idsInOrder[i]);
            PowerOrder.InsertRange(0, present);
        }

        public void HarvestPowerStates()
        {
            foreach (var id in PowerOrder)
            {
                if (!PowerData.ContainsKey(id)) continue;
                var stats = PowerData[id];
                if (stats.Passive) continue;
                if (stats.IsActive != null) stats.Active = stats.IsActive();
            }
        }

        public void ApplyPower(string id)
        {
            if (!PowerData.ContainsKey(id)) return;
            var power = PowerData[id];
            if (power.Passive) return;

            HarvestPowerStates();
            bool wasActive = power.Active;
            power.Apply?.Invoke();
            HarvestPowerStates();

            if (power.Active && !wasActive && !string.IsNullOrEmpty(power.PowerType))
            {
                foreach (var otherId in PowerOrder)
                {
                    if (otherId == id) continue;
                    if (!PowerData.TryGetValue(otherId, out var other)) continue;
                    if (other.Passive || other.PowerType != power.PowerType) continue;
                    if (!other.Active) continue;
                    other.Disable?.Invoke();
                }
                HarvestPowerStates();
            }
            KiHandLoadout.NotifyPowerApplied(this, id);
        }

        public List<PowerStats> GetOrderedPowers()
        {
            var list = new List<PowerStats>();
            foreach (var id in PowerOrder)
                if (PowerData.ContainsKey(id)) list.Add(PowerData[id]);
            return list;
        }

        public string GetStatus(PowerStats stats)
        {
            if (stats.Passive) return "Always active";
            if (stats.GetStatusText != null) return stats.GetStatusText();
            return stats.Active ? "Enabled" : "Disabled";
        }
    }
}
