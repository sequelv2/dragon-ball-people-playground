using UnityEngine;

namespace DBPU
{
    // Shared by the native Unity preview and the mod. Bundles contain only native
    // renderers, particle systems, meshes and Animator assets, never this component.
    public sealed class CinematicAuraRig : MonoBehaviour
    {
        struct Layer {public Renderer Renderer;public MaterialPropertyBlock Block;public float Opacity;public bool Ring;}
        struct Emitter {public ParticleSystem System;public ParticleSystem.MinMaxCurve Rate;public float Debt;}
        Layer[] layers;Emitter[] emitters;Animator animator;
        Color deep,mid,hot;
        float smoothedCharge,previousCharge,ringAge=10,seed,smoothedFury;
        bool paused,transition,stopping;
        public float Charge {get{return smoothedCharge;}}
        public float Overdrive {get{return smoothedFury;}}
        public void Setup(string id,bool isTransition,int identity=0)
        {
            transition=isTransition;seed=Mathf.Repeat(identity*.618034f,17f);
            var renderers=GetComponentsInChildren<Renderer>(true);layers=new Layer[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {
                var r=renderers[i];var material=r.sharedMaterial;
                layers[i]=new Layer{Renderer=r,Block=new MaterialPropertyBlock(),Opacity=material&&material.HasProperty("_Opacity")?material.GetFloat("_Opacity"):1,Ring=r.name.Contains("ShockRing")};
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                bool front=r.name.Contains("Front")||r.name.Contains("ShockRing");
                r.sortingLayerName=front?"Top":"Background";r.sortingOrder=front?4:id.StartsWith("Kaioken")?-12:-8;
            }
            var systems=GetComponentsInChildren<ParticleSystem>(true);emitters=new Emitter[systems.Length];
            for(int i=0;i<systems.Length;i++)
            {
                var p=systems[i];emitters[i]=new Emitter{System=p,Rate=p.emission.rateOverTime};
                p.useAutoRandomSeed=false;p.randomSeed=(uint)(Mathf.Abs(identity)+37+i*193);
                p.Play(true);
            }
            animator=GetComponent<Animator>();if(animator)animator.Play(isTransition?"Transform":"Breathing",0,0);
        }
        public void SetPalette(Color a,Color b,Color c){deep=a;mid=b;hot=c;}
        public void Tick(float clock,float delta,float charge,float cover,float opacity,bool freeze,float overdrive=0)
        {
            if(layers==null)return;
            if(freeze!=paused)
            {
                paused=freeze;
                foreach(var e in emitters)if(e.System){if(freeze)e.System.Pause(true);else {e.System.Play(true);if(stopping)e.System.Stop(true,ParticleSystemStopBehavior.StopEmitting);}}
                if(animator)animator.speed=freeze?0:1;
            }
            if(freeze)return;
            smoothedFury=Mathf.MoveTowards(smoothedFury,Mathf.Clamp01(overdrive),delta*3.5f);
            float effectiveCharge=Mathf.Max(Mathf.Clamp01(charge),smoothedFury*.9f);
            smoothedCharge=Mathf.MoveTowards(smoothedCharge,effectiveCharge,delta*(effectiveCharge>smoothedCharge?2.5f:3f));
            if(smoothedCharge>.15f&&previousCharge<=.15f)ringAge=0;
            previousCharge=smoothedCharge;ringAge+=delta;
            if(animator&&!transition)animator.SetFloat("Charge",smoothedCharge);
            for(int i=0;i<emitters.Length;i++)
            {
                var e=emitters[i];
                if(!e.System)continue;
                var emission=e.System.emission;var rate=ScaleRate(e.Rate,1+smoothedCharge*.7f);emission.rateOverTime=rate;
                // Additional native particles preserve their original textures, colours,
                // shapes and bursts; no external rate/colour snapshot is restored later.
                if(!stopping && e.System.isPlaying && emission.enabled && smoothedFury>.001f)
                {
                    var main=e.System.main;float sample=Mathf.Repeat(e.System.time/Mathf.Max(.1f,main.duration),1);
                    float amount=Mathf.Max(e.Rate.Evaluate(sample),Mathf.Min(16,main.maxParticles*.025f));
                    e.Debt+=Mathf.Max(0,amount)*smoothedFury*1.35f*delta;
                    int extra=Mathf.Min(64,Mathf.FloorToInt(e.Debt));if(extra>0){e.System.Emit(extra);e.Debt-=extra;}
                }
                else e.Debt=0;
                emitters[i]=e;
            }
            foreach(var layer in layers)
            {
                if(!layer.Renderer)continue;var block=layer.Block;
                block.SetFloat("_VfxTime",clock);block.SetFloat("_Charge",smoothedCharge);
                block.SetFloat("_Overdrive",smoothedFury);
                block.SetFloat("_Seed",seed);block.SetColor("_Deep",deep);block.SetColor("_Mid",mid);block.SetColor("_Hot",hot);
                float alpha=layer.Opacity*opacity;
                if(transition)alpha*=cover;
                if(layer.Ring)
                {
                    if(!transition)
                    {
                        float life=Mathf.Clamp01(ringAge/.5f);alpha*=Mathf.Sin(life*Mathf.PI);
                        layer.Renderer.transform.localScale=new Vector3(1.2f+life*3.5f,.18f+life*.6f,1);
                    }
                    else alpha*=1-cover*.25f;
                }
                block.SetFloat("_Opacity",alpha);layer.Renderer.SetPropertyBlock(block);
            }
        }
        public void StopEmission()
        {stopping=true;if(emitters!=null)foreach(var e in emitters)if(e.System)e.System.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        static ParticleSystem.MinMaxCurve ScaleRate(ParticleSystem.MinMaxCurve source,float factor)
        {
            if(source.mode==ParticleSystemCurveMode.Constant)source.constant*=factor;
            else if(source.mode==ParticleSystemCurveMode.TwoConstants){source.constantMin*=factor;source.constantMax*=factor;}
            else source.curveMultiplier*=factor;
            return source;
        }
        public void AdvancePreview(float seconds)
        {
            if(emitters!=null)foreach(var e in emitters)if(e.System)e.System.Simulate(seconds,false,true,true);
            if(animator)animator.Update(seconds);
        }
        public void StepPreview(float seconds)
        {
            if(emitters!=null)foreach(var e in emitters)if(e.System)e.System.Simulate(seconds,false,false,true);
            if(animator)animator.Update(seconds);
        }
    }
}
