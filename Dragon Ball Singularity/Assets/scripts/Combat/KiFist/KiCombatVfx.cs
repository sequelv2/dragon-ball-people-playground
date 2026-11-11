using System;
using System.IO;
using UnityEngine;

namespace DBPU
{
    public static class KiCombatVfxAssets
    {
        static string directory;
        static bool attempted,loaded;
        static AssetBundle bundle;
        public static Material Limb,Impact,Trail,Overdrive,Refraction;
        public static Mesh Quad;
        public static bool Ready { get { Prepare();return loaded; } }
        public static void Initialize(string path)
        {
            directory=path;
            KiOverdriveAura.TryAttachVolume=KiRagingAuraVolume.Attach;
        }
        static void Prepare()
        {
            if(attempted)return;attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/Combat/ki-combat"));
                if(!bundle)throw new IOException("Combat VFX bundle could not be loaded");
                Limb=bundle.LoadAsset<Material>("assets/combat/generated/kilimb.mat");
                Impact=bundle.LoadAsset<Material>("assets/combat/generated/kiimpact.mat");
                Trail=bundle.LoadAsset<Material>("assets/combat/generated/kitrail.mat");
                Overdrive=bundle.LoadAsset<Material>("assets/combat/generated/kioverdrive.mat");
                Refraction=bundle.LoadAsset<Material>("assets/combat/generated/kirefraction.mat");
                Quad=bundle.LoadAsset<Mesh>("assets/combat/generated/kiquad.asset");
                foreach(var material in new[]{Limb,Impact,Trail,Overdrive,Refraction})
                    if(!material || !material.shader.isSupported)throw new IOException("Missing/unsupported Combat VFX shader");
                if(!Quad)throw new IOException("Combat VFX shared geometry is missing");
                loaded=true;
            }
            catch(Exception ex){Debug.LogError("[DBPU Combat VFX] "+ex);}
        }
        internal static MeshRenderer Layer(Transform parent,string name,Material material,int order,bool quad=true)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            if(quad)go.AddComponent<MeshFilter>().sharedMesh=Quad;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.sortingLayerName="Top";renderer.sortingOrder=order;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return renderer;
        }
        internal static void WorldScale(Transform target,Vector3 scale)
        {
            Vector3 inherited=target.parent?target.parent.lossyScale:Vector3.one;
            target.localScale=new Vector3(scale.x/Mathf.Max(.001f,Mathf.Abs(inherited.x)),scale.y/Mathf.Max(.001f,Mathf.Abs(inherited.y)),scale.z/Mathf.Max(.001f,Mathf.Abs(inherited.z)));
        }
    }

    [DefaultExecutionOrder(320)]
    public sealed class KiRagingAuraVolume : MonoBehaviour
    {
        KiOverdriveAura aura;MeshRenderer renderer;float clock;
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        public static bool Attach(KiOverdriveAura aura)
        {
            if(!aura || !KiCombatVfxAssets.Ready)return false;
            var effect=aura.GetComponent<KiRagingAuraVolume>();
            if(!effect){effect=aura.gameObject.AddComponent<KiRagingAuraVolume>();effect.aura=aura;effect.renderer=KiCombatVfxAssets.Layer(aura.transform,"Overdrive volume in native aura colours",KiCombatVfxAssets.Overdrive,0);}
            return true;
        }
        void LateUpdate()
        {
            if(!aura || !renderer)return;
            renderer.enabled=aura.ShouldRender;if(!renderer.enabled)return;
            if(Global.main && Global.main.Paused)return;clock+=Time.deltaTime;
            Vector2 size=aura.WorldSize;float strength=aura.VisualStrength;
            renderer.transform.position=new Vector3(aura.WorldCentre.x,aura.WorldCentre.y+size.y*.08f,.12f);renderer.transform.rotation=Quaternion.identity;
            KiCombatVfxAssets.WorldScale(renderer.transform,new Vector3(Mathf.Max(size.x*.9f,size.y*.42f)*(1+strength*.18f),size.y*.63f*(1+strength*.2f),1));
            renderer.sortingLayerName="Default";renderer.sortingOrder=-5;
            if(aura.CurrentNativeAura)
            {
                var source=aura.CurrentNativeAura.GetComponentInChildren<Renderer>();
                if(source){renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder-1;}
            }
            Color tint=Color.Lerp(aura.Deep,aura.Mid,.72f);float max=Mathf.Max(.01f,tint.maxColorComponent);
            if(max>1)tint/=max;
            properties.SetFloat("_Clock",clock);properties.SetFloat("_Age",clock);properties.SetFloat("_Power",strength*2.5f);
            properties.SetFloat("_Intensity",strength*3);properties.SetFloat("_Opacity",.5f+strength*.28f);properties.SetColor("_Tint",tint);
            renderer.SetPropertyBlock(properties);
        }
    }
}
