using System;
using System.IO;
using UnityEngine;

namespace DBPU
{
    public static class CinematicAssets
    {
        static AssetBundle bundle;
        static bool attempted;
        static string directory;
        public static Shader RimShader { get; private set; }
        public static void Initialize(string path) { directory=path; }
        public static bool IsAdditional(string id)
        {return id=="Mystic"||id=="BeastGohan"||id=="Frieza"||id=="GoldenFrieza"||id=="BlackFrieza"||id=="Broly"||id=="PowerAwakened"||id=="OrangePiccolo"||id=="UE"||id=="SSJRose"||id=="Buu"||id=="Beerus"||id=="Whis";}
        public static bool Handles(string id,bool transition=false)
        {
            return IsAdditional(id)||id=="Base"||id=="SSJ1"||id=="SSJ2"||id=="SSJ3"||id=="SSJGod"||id=="SSJBlueEvolution"||id=="Kaioken"||id=="KaiokenGold"||id=="KaiokenBlue"||id=="UI"||id=="MUI"||(transition&&id=="SSJBlue");
        }
        static bool Prepare()
        {
            if(bundle)return true;if(attempted)return false;attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/Cinematic/cinematic-auras"));
                if(!bundle)throw new InvalidOperationException("AssetBundle not loaded");
                RimShader=bundle.LoadAsset<Shader>("assets/shaders/bodyrim.shader");
                Debug.Log("[DBPU Cinematic] Unity 2020 native VFX bundle loaded.");return true;
            }
            catch(Exception ex){Debug.LogWarning("[DBPU Cinematic] Original effects retained: "+ex.Message);return false;}
        }
        public static GameObject Create(string id,bool transition)
        {
            if(!Handles(id,transition)||!Prepare())return null;
            GameObject go=null;
            try
            {
                string path="assets/generated/"+id.ToLowerInvariant()+(transition?"transition":"")+".prefab";
                var prefab=bundle.LoadAsset<GameObject>(path);if(!prefab)return null;
                go=UnityEngine.Object.Instantiate(prefab);
                foreach(var r in go.GetComponentsInChildren<Renderer>())
                    if(!r.sharedMaterial||!r.sharedMaterial.shader.isSupported)throw new InvalidOperationException("Unsupported GPU shader");
                var visual=go.AddComponent<RefinedAuraVisual>();visual.SetupCinematic(RefinedAuras.Look(id),transition);
                if(!transition&&(id=="SSJ2"||id=="SSJ3"||id=="Mystic"||id=="BeastGohan"))visual.AttachMysticLightning();
                return go;
            }
            catch(Exception ex){if(go)UnityEngine.Object.Destroy(go);Debug.LogWarning("[DBPU Cinematic] "+id+": "+ex.Message);return null;}
        }
        public static string ResolveAura(PersonBehaviour person,string form,string fallback="Base")
        {
            if(person!=null&&person.Limbs!=null&&person.Limbs.Length>0&&person.Limbs[0]!=null)
            {
                var menu=person.Limbs[0].GetComponent<TransformationContextMenu>();
                if(menu!=null&&menu.TransformationsData!=null&&menu.TransformationsData.transformations!=null)
                {
                    var data=menu.TransformationsData.transformations.TransformationData;
                    if(form!=null&&data.ContainsKey(form))return data[form].AuraID;
                }
            }
            return string.IsNullOrEmpty(fallback)?"Base":fallback;
        }
        public static string KaiokenProfile(PersonBehaviour person)
        {
            if(person==null)return "Kaioken";
            var ki=person.GetComponent<KiSystem>();
            string form=ki!=null?ki.CurrentForm:"Base";
            string aura=ResolveAura(person,form,form);
            if(aura=="SSJBlue"||aura=="SSJBlueEvolution")return "KaiokenBlue";
            if(aura=="SSJ1"||aura=="SSJ2"||aura=="SSJ3")return "KaiokenGold";
            return "Kaioken";
        }
        public static bool KeepsInnerAura(string form)
        {return !string.IsNullOrEmpty(form)&&form!="Base";}
    }
}
