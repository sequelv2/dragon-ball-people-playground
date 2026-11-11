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
            // Base Kaioken and Kaioken over SSJ1/2/3 (KaiokenGold) keep the original film-shader flame.
            // SSJ Blue and Kaioken over Blue use the soft procedural flame: a special smooth crimson mantle.
            return IsAdditional(id)||id=="Base"||id=="SSJ1"||id=="SSJ2"||id=="SSJ3"||id=="SSJGod"||id=="SSJBlueEvolution"||id=="Kaioken"||id=="KaiokenGold"||id=="UI"||id=="MUI"||(transition&&(id=="SSJBlue"||id=="KaiokenBlue"));
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
            // Look at every source of the current form/aura: some forms (e.g. Perfected SSJ Blue)
            // carry no AuraID, so the form name itself must also be checked.
            var ki=person.GetComponent<KiSystem>();
            string kiForm=ki!=null?ki.CurrentForm:null, menuForm=null, menuAura=null, handlerAura=null;
            try
            {
                var menu=person.Limbs!=null&&person.Limbs.Length>0&&person.Limbs[0]!=null?person.Limbs[0].GetComponent<TransformationContextMenu>():null;
                if(menu!=null){menuForm=menu.Form;menuAura=menu.CurrentAuraID;}
                var handler=person.GetComponent<AuraHandler>();
                if(handler!=null)handlerAura=handler.CurrentAuraID;
            }
            catch{}
            string form=!string.IsNullOrEmpty(menuForm)&&menuForm!="Base"?menuForm:kiForm;
            string resolved=ResolveAura(person,form,null);
            var keys=new[]{form,kiForm,menuForm,resolved,menuAura,handlerAura};
            if(string.IsNullOrEmpty(form)||form=="Base")return "Kaioken";
            foreach(var k in keys)if(!string.IsNullOrEmpty(k)&&k.IndexOf("Blue",StringComparison.OrdinalIgnoreCase)>=0)return "KaiokenBlue";
            // Gold (original style) only over Super Saiyan 1/2/3; God, Rose and others keep base Kaioken, as before.
            foreach(var k in keys)if(k=="SSJ"||k=="SSJ1"||k=="SSJ2"||k=="SSJ3"||k=="PSSJ"||k=="SSJGrade2")return "KaiokenGold";
            return "Kaioken";
        }
        public static bool KeepsInnerAura(string form)
        {return !string.IsNullOrEmpty(form)&&form!="Base";}
    }
}
