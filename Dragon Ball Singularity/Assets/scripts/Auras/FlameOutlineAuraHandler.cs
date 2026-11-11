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
    public class FlameOutlineAuraHandler : MonoBehaviour
    {
        public PersonBehaviour person;
        public List<SetAuraBase> AuraParts = new List<SetAuraBase>();
        public Type AuraType;
        public Type InnerAuraType;
        public Type AccentAuraType;

        public bool AuraOn = false;
        public bool TemporaryAura = false;
        bool setup = false;
        AuraHandler nativeAura;

        static bool IsPhysicalClothRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) return true;

            Transform t = renderer.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.StartsWith("Cloth piece")) return true;
                if (n == "Tail" || n == "Waist" || n == "Belt") return true;
                t = t.parent;
            }

            return false;
        }

        static bool IsAuraAccessory(SpriteRenderer renderer)
        {
            if (renderer == null) return false;
            string n = renderer.gameObject.name;
            return n == "Hair" || n == "Cape" || n == "Thingy";
        }

        void RegisterRenderer(SpriteRenderer renderer, HashSet<SpriteRenderer> registered)
        {
            if (renderer == null || renderer.sprite == null || registered.Contains(renderer)) return;
            if (renderer.gameObject.name == "CaptureSprite" || renderer.gameObject.name == "BackgroundSprite") return;
            if (IsPhysicalClothRenderer(renderer)) return;

            registered.Add(renderer);

            if (AccentAuraType != null)
            {
                SetAuraBase accentAura = renderer.gameObject.AddComponent(AccentAuraType) as SetAuraBase;
                accentAura.SetMaterial();
                accentAura.AuraOn = false;
                accentAura.ReadDataFromMaterial();
                AuraParts.Add(accentAura);
            }

            SetAuraBase aura = renderer.gameObject.AddComponent(AuraType) as SetAuraBase;
            aura.SetMaterial();
            aura.AuraOn = false;
            aura.ReadDataFromMaterial();
            AuraParts.Add(aura);

            if (InnerAuraType != null)
            {
                SetAuraBase innerAura = renderer.gameObject.AddComponent(InnerAuraType) as SetAuraBase;
                innerAura.SetMaterial();
                innerAura.AuraOn = false;
                innerAura.ReadDataFromMaterial();
                AuraParts.Add(innerAura);
            }
        }

        public void Setup()
        {
            setup = true;
            person = GetComponent<PersonBehaviour>();
            if (person == null) return;
            if(AuraType==typeof(PurpleFlameAura)||AuraType==typeof(WhisFlameAura))
            {
                nativeAura=GetComponent<AuraHandler>()??gameObject.AddComponent<AuraHandler>();
                nativeAura.person=person;
                nativeAura.CurrentAuraID=AuraType==typeof(WhisFlameAura)?"Whis":"Beerus";
                return;
            }

            HashSet<SpriteRenderer> registered = new HashSet<SpriteRenderer>();

            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                RegisterRenderer(limb.GetComponent<SpriteRenderer>(), registered);
            }

            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            {
                if (registered.Contains(renderer)) continue;
                if (!IsAuraAccessory(renderer)) continue;
                RegisterRenderer(renderer, registered);
            }
        }

        public void ToggleAura()
        {
            if (!setup) Setup();

            AuraOn = nativeAura!=null ? !nativeAura.AuraActive : !AuraOn;
            if(nativeAura!=null){nativeAura.AuraActive=AuraOn;return;}
            foreach (var Auras in AuraParts)
            {
                if (AuraOn) Auras.EnableAura();
                else Auras.DisableAura();
            }
        }

        private void Update()
        {
            if (person == null || !setup) return;
            if(nativeAura!=null)
            {
                if(person.Consciousness<=.8f){nativeAura.AuraActive=false;TemporaryAura=false;}
                AuraOn=nativeAura.AuraActive;
                return;
            }


            Vector2 pivot = base.transform.Find("Body/MiddleBody").position;
            foreach (var aura in AuraParts)
            {
                if (aura == null || aura.bgSR == null) continue;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                aura.bgSR.GetPropertyBlock(block);
                block.SetVector("_Pivot", pivot);
                aura.bgSR.SetPropertyBlock(block);
            }

            if (person.Consciousness <= 0.8f && AuraOn == true)
            {
                AuraOn = false;
                foreach (var Auras in AuraParts) Auras.DisableAura();
                TemporaryAura = false;
            }
        }
    }
}
