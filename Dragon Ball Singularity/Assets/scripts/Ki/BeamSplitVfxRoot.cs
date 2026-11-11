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
    public class BeamSplitVfxRoot : MonoBehaviour
    {
        public BeamBehaviour BeamHost;
        public StraightBeamBehaviour StraightHost;
        public float SpawnedAt;
        public float MinStraightSeconds = 0.68f;

        private void Update()
        {
            if (BeamHost != null)
            {
                if (BeamHost.EndBeam == null || !BeamHost.StillActive)
                    Destroy(gameObject);
                return;
            }

            if (StraightHost != null)
            {
                if ((Time.time - SpawnedAt >= MinStraightSeconds && !StraightHost.Firing) || Time.time - SpawnedAt > 3.5f)
                    Destroy(gameObject);
                return;
            }

            Destroy(gameObject);
        }
    }
}
