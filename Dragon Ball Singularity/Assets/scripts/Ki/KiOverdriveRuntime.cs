using System;
using UnityEngine;

namespace DBPU
{
    internal static class KiSurgeArt
    {
        static Material material;
        public static Material Material
        {
            get
            {
                if (!material) material = new Material(Shader.Find("Sprites/Default")) { name="DBPU Ki Surge", mainTexture=Texture2D.whiteTexture };
                return material;
            }
        }
        public static LineRenderer Line(Transform parent,string name,int points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.sharedMaterial = Material; line.useWorldSpace = true; line.positionCount=points;
            line.sortingLayerName="Top"; line.sortingOrder=40; line.numCapVertices=2; line.numCornerVertices=1;
            return line;
        }
        public static Color Fade(Color color,float alpha) { color.a=Mathf.Clamp01(alpha); return color; }
        public static float Wave(float seed,float time) { return Mathf.PerlinNoise(seed,time)*2-1; }
    }

    public sealed class KiOverdriveAura : MonoBehaviour
    {
        PersonBehaviour person;
        AllyKiReservoir energy;
        AuraHandler handler;
        float nextAttachAttempt;
        readonly MaterialPropertyBlock sourcePalette=new MaterialPropertyBlock();
        public static Func<KiOverdriveAura,bool> TryAttachVolume;
        public PersonBehaviour Person { get { return person; } }
        public GameObject CurrentNativeAura { get; private set; }
        public string AuraId { get; private set; }
        public Color Deep { get; private set; }
        public Color Mid { get; private set; }
        public Color Hot { get; private set; }
        // WorldSize is the unexpanded body sprite bounds, not the aura footprint.
        public Vector2 WorldCentre { get; private set; }
        public Vector2 WorldSize { get; private set; }
        public float VisualStrength { get; private set; }
        public float Power { get { return energy?energy.PowerMultiplier:1; } }
        public bool VolumeAttached { get; private set; }
        public bool ShouldRender { get { return isActiveAndEnabled && VisualStrength>.001f && CurrentNativeAura; } }
        public static KiOverdriveAura Bind(PersonBehaviour person,AllyKiReservoir reservoir)
        {
            var go=new GameObject("DBPU Ki Overdrive Native Aura Fury"); go.transform.SetParent(person.transform,false);
            var effect=go.AddComponent<KiOverdriveAura>(); effect.person=person; effect.energy=reservoir;
            effect.Initialize(); return effect;
        }
        void Initialize()
        {
            handler=person?person.GetComponent<AuraHandler>():null;
        }
        void HideOwnedVisuals()
        {
            VisualStrength=0;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(renderer)renderer.enabled=false;
        }
        void OnDisable(){HideOwnedVisuals();}
        void ReadNativePalette()
        {
            var profile=RefinedAuras.Look(AuraId);Deep=profile.Deep;Mid=profile.Mid;Hot=profile.Light;
            if(!CurrentNativeAura)return;
            var refined=CurrentNativeAura.GetComponent<RefinedAuraVisual>();
            if(refined && refined.TargetLook!=null)
            {
                Deep=refined.TargetLook.Deep;Mid=refined.TargetLook.Mid;Hot=refined.TargetLook.Light;return;
            }
            if(AuraId!=null && AuraId.StartsWith("Naruto/",StringComparison.Ordinal))
            {
                var renderer=CurrentNativeAura.GetComponentInChildren<MeshRenderer>(true);
                if(renderer)
                {
                    sourcePalette.Clear();renderer.GetPropertyBlock(sourcePalette);
                    Color tint=sourcePalette.GetColor("_Color"),core=sourcePalette.GetColor("_Core");
                    if(tint.maxColorComponent>.01f){Mid=tint;Deep=tint*.32f;Deep=new Color(Deep.r,Deep.g,Deep.b,1);Hot=core.maxColorComponent>.01f?core:Color.white;}
                }
                return;
            }
            foreach(var ps in CurrentNativeAura.GetComponentsInChildren<ParticleSystem>(true))
            {
                if(!ps || ps.name.IndexOf("lightning",StringComparison.OrdinalIgnoreCase)>=0 || ps.name.IndexOf("spark",StringComparison.OrdinalIgnoreCase)>=0)continue;
                var color=ps.main.startColor;Color tint=color.Evaluate(.5f);
                if(tint.maxColorComponent>.01f){Mid=new Color(tint.r,tint.g,tint.b,1);Deep=Color.Lerp(Color.black,Mid,.32f);Deep=new Color(Deep.r,Deep.g,Deep.b,1);Hot=Color.Lerp(Mid,Color.white,.65f);break;}
            }
        }
        void LateUpdate()
        {
            if(!person || !energy || !person.IsAlive()){Destroy(gameObject);return;}
            if(!energy.OverdriveActive){HideOwnedVisuals();enabled=false;return;}
            if(!handler)handler=person.GetComponent<AuraHandler>();
            CurrentNativeAura=handler?handler.CurrentAuraObject:null;
            AuraId=handler?handler.CurrentAuraID:"Base";
            if(!CurrentNativeAura && AuraId!=null && AuraId.StartsWith("Naruto/",StringComparison.Ordinal))
            {
                var chakra=person.transform.Find("Naruto GPU chakra field");
                if(chakra)CurrentNativeAura=chakra.gameObject;
            }
            VisualStrength=handler && handler.IsAuraEffectivelyVisible && CurrentNativeAura?handler.OverdriveVisualStrength:0;
            if(Global.main && Global.main.Paused)return;
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var limb in person.Limbs)
            {
                if(!limb)continue;var sr=limb.GetComponent<SpriteRenderer>();if(!sr)continue;
                var bounds=sr.bounds;minX=Mathf.Min(minX,bounds.min.x);maxX=Mathf.Max(maxX,bounds.max.x);minY=Mathf.Min(minY,bounds.min.y);maxY=Mathf.Max(maxY,bounds.max.y);
            }
            WorldCentre=minX<float.MaxValue?new Vector2((minX+maxX)*.5f,(minY+maxY)*.5f):(Vector2)person.transform.position;
            WorldSize=minX<float.MaxValue?new Vector2(Mathf.Clamp(maxX-minX,.35f,100),Mathf.Clamp(maxY-minY,.75f,100)):new Vector2(.6f,1.8f);
            ReadNativePalette();
            if(!VolumeAttached && TryAttachVolume!=null && Time.unscaledTime>=nextAttachAttempt)
            {
                nextAttachAttempt=Time.unscaledTime+.75f;
                try{VolumeAttached=TryAttachVolume(this);}catch(Exception ex){Debug.LogWarning("[DBPU Ki Overdrive] Volume attachment: "+ex.Message);}
            }
        }
    }

    public sealed class KiImpactBurst : MonoBehaviour
    {
        // Preserve the impact API used by ordinary strikes and internal pulses.
        // Rendering and lifetime are owned by the shared Unity volume component.
        public static KiImpactBurst Spawn(Vector2 point,int tier,bool penetrated,bool internalBurst=false,Vector2 direction=default(Vector2),Color? effectTint=null)
        {
            var effect=KiStrikeImpactVfx.Spawn(point,tier,penetrated,internalBurst,direction,effectTint);
            return effect?effect.gameObject.AddComponent<KiImpactBurst>():null;
        }
    }

    public sealed class KiInternalDisruption : MonoBehaviour
    {
        PersonBehaviour victim;
        float attackStrength,remainingDamage,elapsed,nextPulse;
        int pulses;
        public float ResistanceRatio { get; private set; }
        public static KiInternalDisruption Inflict(PersonBehaviour target,float strength,float damageBudget)
        {
            if(!target || !target.IsAlive())return null;
            var effect=target.GetComponent<KiInternalDisruption>();
            if(!effect)effect=target.gameObject.AddComponent<KiInternalDisruption>();
            effect.victim=target;effect.attackStrength=Mathf.Max(effect.attackStrength,strength);
            effect.remainingDamage=KiCombatMath.Cap((double)effect.remainingDamage+damageBudget,1e35f);
            effect.pulses=0;effect.elapsed=effect.nextPulse=0;return effect;
        }
        void Update()
        {
            if(!victim || !victim.IsAlive()){Destroy(this);return;}
            if(Global.main && Global.main.Paused)return;
            Advance(Time.deltaTime);
        }
        public void Advance(float dt)
        {
            if(!victim || victim.Limbs==null)return;
            elapsed+=Mathf.Clamp(dt,0,.25f);
            if(elapsed<nextPulse)return;
            nextPulse=elapsed+.28f;
            float defense=Mathf.Max(.01f,MCP_Utils.GetFinalMultiplier(victim));
            ResistanceRatio=Mathf.Clamp(attackStrength/defense,0,100);
            float penetration=Mathf.Clamp01((ResistanceRatio-.35f)/1.15f);
            float portion=remainingDamage/Mathf.Max(1,7-pulses);
            remainingDamage=Mathf.Max(0,remainingDamage-portion);
            int index=pulses%Mathf.Max(1,victim.Limbs.Length);
            // Strong defenders contain the intrusion; an equal or weaker defender takes
            // escalating pulses through separate physical parts instead of an instant kill.
            var limb=victim.Limbs[index];
            if(limb && limb.Health>0 && penetration>0)
            {
                float damage=KiCombatMath.Cap((double)portion*penetration*(.7f+pulses*.12f),1e35f);
                limb.Damage(damage);
                KiImpactBurst.Spawn(limb.transform.position,0,true,true);
            }
            pulses++;
            if(pulses>=7 || elapsed>2.6f || remainingDamage<=0)Destroy(this);
        }
    }
}
