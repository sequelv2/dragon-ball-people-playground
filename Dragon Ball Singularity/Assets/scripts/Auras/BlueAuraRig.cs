using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // One look of the Super Saiyan Blue aura family. Sizes are in fighter heights with the feet at 0;
    // colours are display colours, as they should appear on screen.
    public sealed class BlueAuraProfile
    {
        public Vector4 Shape;  // half width, height, bottom, crown taper
        public Vector4 Motion; // rise speed, tongue length, lobe size, column width
        public Vector4 Look;   // body opacity, front opacity, glow, streak contrast
        public Vector4 Inner;  // inner shell height, width, strength, speed (Kaioken over Blue only)
        public float Gain=1;
        public float WhiteCap=.8f,WhiteKnee=.6f; // linear; see _WhiteCap and _WhiteKnee
        public Color Rim,Fringe,Body,Hot,Column,Tip,Glow;
        public Color InRim,InFringe,InBody,InHot,InColumn,InTip,InGlow;
        public int Licks,Motes,Streaks;
        public Color MoteA,MoteB;
    }

    public static class BlueAuraAssets
    {
        static AssetBundle bundle;
        static bool attempted;
        static string directory;
        public static Material Field,Tongue,Spark;
        // The game reports how its camera renders (see BeamFxAssets.Hdr); the preview leaves it unset.
        public static Func<float> Hdr;
        static Color X(string hex) { Color c; ColorUtility.TryParseHtmlString("#"+hex,out c); return c; }
        // Colours measured from the reference animations the user chose (SSJ Blue; Kaioken over Blue).
        public static readonly Dictionary<string,BlueAuraProfile> Profiles=new Dictionary<string,BlueAuraProfile>
        {
            // A round bulb of flame: cyan and white columns, a vivid cyan one in the middle, lavender lobes on the flanks.
            {"SSJBlue",new BlueAuraProfile
            {
                Shape=new Vector4(.56f,1.36f,-.1f,.14f),Motion=new Vector4(1f,.24f,.075f,.5f),Look=new Vector4(.92f,.16f,.7f,1.5f),
                Rim=X("5A6FE0"),Fringe=X("A99CFF"),Body=X("6FDCFF"),Hot=X("F6FFFF"),Column=X("1EC0FF"),Tip=X("3F86EE"),Glow=X("1E2E8C"),
                Licks=10,Motes=14,Streaks=4,MoteA=X("BDF3FF"),MoteB=X("FFFFFF")
            }},
            // The same flame, deeper blue: azure columns instead of white, an indigo rim.
            {"SSJBlueEvolution",new BlueAuraProfile
            {
                Shape=new Vector4(.6f,1.42f,-.1f,.12f),Motion=new Vector4(1.12f,.27f,.085f,.5f),Look=new Vector4(.93f,.17f,.8f,1.4f),
                Rim=X("2A3CD0"),Fringe=X("7466FF"),Body=X("3C9BFF"),Hot=X("BFE6FF"),Column=X("0E5CFF"),Tip=X("2346DC"),Glow=X("182088"),
                Licks=13,Motes=18,Streaks=6,MoteA=X("7FC6FF"),MoteB=X("E6F6FF")
            }},
            // A red flame with a smaller white-blue one inside it and magenta light between them.
            {"KaiokenBlue",new BlueAuraProfile
            {
                // The white-blue shell covers the whole fighter with room over the head; the red burns above and round it.
                Shape=new Vector4(.68f,1.78f,-.1f,.14f),Motion=new Vector4(1.3f,.27f,.09f,.42f),Look=new Vector4(.94f,.15f,.85f,1.2f),
                Inner=new Vector4(.8f,.68f,1f,1.1f),
                // A hard cap: the rose-magenta mix the user chose in game on 2026-11-06.
                WhiteCap=.72f,WhiteKnee=1f,
                Rim=X("B5122A"),Fringe=X("FF4F7E"),Body=X("F5283C"),Hot=X("FF4A5C"),Column=X("F21E36"),Tip=X("E11A3E"),Glow=X("5A0820"),
                InRim=X("EE5AEA"),InFringe=X("F9A8F8"),InBody=X("CFE9FF"),InHot=X("FFFFFF"),InColumn=X("8CC8FF"),InTip=X("FBB0F4"),InGlow=X("F030E0"),
                Licks=14,Motes=18,Streaks=8,MoteA=X("FF7A9A"),MoteB=X("FFE0F0")
            }},
        };
        public static void Initialize(string path) { directory=path; }
        public static bool Handles(string id) { return id!=null&&Profiles.ContainsKey(id); }
        // The preview project compiles the shaders itself.
        public static void UseMaterials(Material field,Material tongue,Material spark) { Field=field;Tongue=tongue;Spark=spark;attempted=true; }
        public static bool Prepare()
        {
            if(Field)return true;
            if(attempted)return false;
            attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/BlueAura/saiyan_blue_aura"));
                if(!bundle)foreach(var loaded in AssetBundle.GetAllLoadedAssetBundles())if(loaded.Contains("saiyanblueaura_field")){bundle=loaded;break;}
                if(!bundle)throw new InvalidOperationException("AssetBundle not loaded");
                Field=bundle.LoadAsset<Material>("saiyanblueaura_field");
                Tongue=bundle.LoadAsset<Material>("saiyanblueaura_tongue");
                Spark=bundle.LoadAsset<Material>("saiyanblueaura_spark");
                if(!Field||!Tongue||!Spark)throw new InvalidOperationException("Materials missing");
                if(!Field.shader.isSupported||!Tongue.shader.isSupported||!Spark.shader.isSupported)throw new InvalidOperationException("Unsupported GPU shader");
                return true;
            }
            catch(Exception ex)
            {
                Field=null;
                Debug.LogWarning("[DBPU BlueAura] Original effects retained: "+ex.Message);
                return false;
            }
        }
        public static GameObject Create(string id,bool transition)
        {
            if(!Handles(id)||!Prepare())return null;
            return new GameObject((transition?"Blue transformation veil/":"BlueAura/")+id);
        }
    }

    // Draws one Blue aura. Depends on UnityEngine only, so the preview project can render it without the game.
    // The root stands where AuraHandler puts refined auras rooted at the feet, at their scale: the fighter's
    // feet at local y=-.875 and the fighter 1.75 tall. The aura grows inside the shader, from the feet.
    public sealed class BlueAuraRig : MonoBehaviour
    {
        // The fighter's feet and height in the rig's local space, measured in game (BlueAuraCommon.cginc agrees).
        public const float Feet=-1.4f,Height=1.86f;
        // From the MiddleBody limb down to the soles, per unit of the fighter's scale.
        public const float MiddleAboveFeet=1.35f;
        struct Layer{public Renderer Renderer;public MaterialPropertyBlock Block;public float Mode;}
        static Mesh fieldQuad,burstQuad;
        readonly List<Layer> layers=new List<Layer>();
        BlueAuraProfile profile;
        Vector4[] palette=new Vector4[14];
        bool transition,stopping;
        float seed,smoothedCharge,previousCharge,smoothedFury,age,burstAge=1,hidden,hideTimer,leanX,leanVelocity;
        Vector3 lastPosition;
        bool hasPosition;
        Renderer lickRenderer,sparkRenderer;
        MaterialPropertyBlock lickBlock,sparkBlock;
        Mesh lickMesh,sparkMesh;
        readonly List<Vector3> positions=new List<Vector3>();
        readonly List<Vector2> uv0=new List<Vector2>(),uv1=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        public float Charge {get{return smoothedCharge;}}
        public float Overdrive {get{return smoothedFury;}}
        public string ProfileId {get;private set;}
        // 1 while Kaioken over Blue has the plain aura step aside.
        public float Hidden {get{return hidden;}}

        static Mesh Quad(string name,float x0,float x1,float y0,float y1)
        {
            var mesh=new Mesh {name=name};
            Func<float,float,Vector3> at=(x,y)=>new Vector3(x*Height,Feet+y*Height,0);
            mesh.vertices=new[]{at(x0,y0),at(x1,y0),at(x0,y1),at(x1,y1)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            mesh.triangles=new[]{0,2,1,1,2,3};
            mesh.RecalculateBounds();
            mesh.hideFlags=HideFlags.DontUnloadUnusedAsset;
            return mesh;
        }
        Renderer Child(string childName,Mesh mesh,Material material,bool front,int order)
        {
            var go=new GameObject(childName);
            go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            r.sortingLayerName=front?"Top":"Background";r.sortingOrder=order;
            return r;
        }
        void AddField(string childName,Mesh mesh,float mode,bool front,int order)
        {
            layers.Add(new Layer{Renderer=Child(childName,mesh,BlueAuraAssets.Field,front,order),Block=new MaterialPropertyBlock(),Mode=mode});
        }
        public void Setup(string id,bool isTransition,int identity=0)
        {
            profile=BlueAuraAssets.Profiles[id];
            ProfileId=id;
            transition=isTransition;
            seed=Mathf.Repeat(identity*.618034f,17f);
            if(!fieldQuad)fieldQuad=Quad("Blue aura field",-1.35f,1.35f,-.45f,2.5f);
            if(!burstQuad)burstQuad=Quad("Blue aura burst",-2.1f,2.1f,-.9f,2.8f);
            SetColors(profile.Rim,profile.Fringe,profile.Body,profile.Hot,profile.Column,profile.Tip,profile.Glow);
            AddField("Blue aura body",fieldQuad,1,false,-12);
            AddField("Blue aura front",fieldQuad,2,true,4);
            AddField("Blue aura burst",burstQuad,3,true,5);
            lickMesh=new Mesh {name="Blue aura licks"};lickMesh.MarkDynamic();
            lickRenderer=Child("Blue aura licks",lickMesh,BlueAuraAssets.Tongue,false,-13);lickBlock=new MaterialPropertyBlock();
            sparkMesh=new Mesh {name="Blue aura sparks"};sparkMesh.MarkDynamic();
            sparkRenderer=Child("Blue aura sparks",sparkMesh,BlueAuraAssets.Spark,true,6);sparkBlock=new MaterialPropertyBlock();
            // The transformation veil opens with the shock ring.
            if(transition)burstAge=0;
        }
        static Vector4 E(Color c)
        {
            Func<float,float> e=v=>-Mathf.Log(1-Mathf.Clamp(v,0,.985f));
            return new Vector4(e(c.r),e(c.g),e(c.b),1);
        }
        void SetColors(Color rim,Color fringe,Color body,Color hot,Color column,Color tip,Color glow)
        {
            palette[0]=E(rim);palette[1]=E(fringe);palette[2]=E(body);palette[3]=E(hot);palette[4]=E(column);palette[5]=E(tip);palette[6]=E(glow);
            palette[7]=E(profile.InRim);palette[8]=E(profile.InFringe);palette[9]=E(profile.InBody);palette[10]=E(profile.InHot);
            palette[11]=E(profile.InColumn);palette[12]=E(profile.InTip);palette[13]=E(profile.InGlow);
        }
        static readonly string[] PaletteNames={"_Rim","_Fringe","_Body","_Hot","_Column","_Tip","_Glow","_InRim","_InFringe","_InBody","_InHot","_InColumn","_InTip","_InGlow"};
        // Kept for the shared aura interface; the Blue family keeps its own colours.
        public void SetPalette(Color a,Color b,Color c) {}
        // A custom transformation colour replaces the blue in every layer.
        public void Recolor(Color primary,Color bright)
        {
            if(profile==null)return;
            SetColors(primary*.75f,Color.Lerp(primary,bright,.5f),Color.Lerp(bright,Color.white,.55f),Color.Lerp(bright,Color.white,.85f),bright,primary,primary*.35f);
        }
        // Kaioken over Blue draws the whole look itself; the plain aura underneath fades while it is called.
        public void Suppress() { hideTimer=.2f; }
        public void Tick(float clock,float delta,float charge,float cover,float opacity,bool freeze,float overdrive=0)
        {
            if(profile==null||freeze)return;
            age+=delta;
            smoothedFury=Mathf.MoveTowards(smoothedFury,Mathf.Clamp01(overdrive),delta*3.5f);
            float effectiveCharge=Mathf.Max(Mathf.Clamp01(charge),smoothedFury*.9f);
            smoothedCharge=Mathf.MoveTowards(smoothedCharge,effectiveCharge,delta*(effectiveCharge>smoothedCharge?2.5f:3f));
            if(!transition&&smoothedCharge>.15f&&previousCharge<=.15f)burstAge=0;
            previousCharge=smoothedCharge;
            burstAge+=delta/.6f;
            hideTimer-=delta;
            hidden=Mathf.MoveTowards(hidden,hideTimer>0?1:0,delta*6f);
            // The crown trails the fighter's motion, on a loose spring.
            Vector3 position=transform.position;
            float scale=Mathf.Max(.05f,Mathf.Abs(transform.lossyScale.y))*Height;
            float target=0;
            if(hasPosition&&delta>0)
            {
                float vx=(position.x-lastPosition.x)/delta;
                if(Mathf.Abs(vx)<60f)target=Mathf.Clamp(-vx/scale*.045f,-.3f,.3f);
            }
            lastPosition=position;hasPosition=true;
            leanVelocity+=((target-leanX)*70f-leanVelocity*10f)*delta;
            leanX+=leanVelocity*delta;
            float time=clock+seed;
            float fade=opacity*(1-hidden);
            float flash=transition?cover*.8f:Mathf.Clamp01(1-age/.35f)*.6f;
            float hdr=BlueAuraAssets.Hdr!=null?BlueAuraAssets.Hdr():0;
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                if(!layer.Renderer)continue;
                var block=layer.Block;
                Fill(block,time,flash,hdr);
                float alpha=fade;
                if(transition)alpha*=layer.Mode>2.5f?1:cover;
                if(layer.Mode>2.5f)
                {
                    bool on=burstAge<1;
                    layer.Renderer.enabled=on;
                    if(!on)continue;
                    block.SetFloat("_Burst",burstAge);
                }
                block.SetFloat("_Mode",layer.Mode);
                block.SetFloat("_Opacity",alpha);
                layer.Renderer.SetPropertyBlock(block);
            }
            float lickAlpha=fade*(transition?cover:1);
            Fill(lickBlock,time,flash,hdr);lickBlock.SetFloat("_Opacity",lickAlpha);lickRenderer.SetPropertyBlock(lickBlock);
            Fill(sparkBlock,time,flash,hdr);sparkBlock.SetFloat("_WhiteCap",.93f);sparkBlock.SetFloat("_Opacity",fade);sparkRenderer.SetPropertyBlock(sparkBlock);
            BuildLicks(time,lickAlpha>.001f);
            BuildSparks(time,fade>.001f&&!transition);
        }
        void Fill(MaterialPropertyBlock block,float time,float flash,float hdr)
        {
            for(int i=0;i<PaletteNames.Length;i++)block.SetVector(PaletteNames[i],palette[i]);
            block.SetVector("_Shape",profile.Shape);block.SetVector("_Motion",profile.Motion);
            block.SetVector("_Look",profile.Look);block.SetVector("_Inner",profile.Inner);
            block.SetVector("_Lean",new Vector4(leanX,0,0,0));
            block.SetFloat("_Clock",time);block.SetFloat("_Seed",seed);
            block.SetFloat("_Charge",smoothedCharge);block.SetFloat("_Fury",smoothedFury);
            block.SetFloat("_Flash",flash);block.SetFloat("_Gain",profile.Gain);block.SetFloat("_Hdr",hdr);block.SetFloat("_WhiteCap",profile.WhiteCap);block.SetFloat("_WhiteKnee",profile.WhiteKnee);
        }
        static float Noise(float n) { return Mathf.Repeat(Mathf.Sin(n*12.9898f)*43758.5453f,1f); }
        void ShellSize(out float halfW,out float height,out float bottom)
        {
            halfW=profile.Shape.x*(1+smoothedCharge*.3f+smoothedFury*.16f);
            height=profile.Shape.y*(1+smoothedCharge*.2f+smoothedFury*.12f);
            bottom=profile.Shape.z;
        }
        static Vector3 Local(float x,float y) { return new Vector3(x*Height,Feet+y*Height,0); }
        // Licks of flame leave the surface, rise and burn out. Stateless: each one repeats on its own cycle.
        void BuildLicks(float time,bool visible)
        {
            positions.Clear();uv0.Clear();colors.Clear();triangles.Clear();
            if(visible)
            {
                float halfW,height,bottom;ShellSize(out halfW,out height,out bottom);
                float energy=Mathf.Clamp01(smoothedCharge*.75f+smoothedFury);
                int count=Mathf.RoundToInt(profile.Licks*(1+energy*.6f));
                const int Rows=8;
                for(int i=0;i<count;i++)
                {
                    float life=(.42f+.3f*Noise(i+.3f))/(1+energy*.35f);
                    float tick=time/life+Noise(i+1.7f)*7f;
                    int cycle=Mathf.FloorToInt(tick);
                    float a=tick-cycle;
                    float n=i*3.17f+cycle*5.3f;
                    // Mostly from the crown, a few from the flanks.
                    float h0=.5f+.46f*Mathf.Sqrt(Noise(n));
                    float side=Noise(n+1)<.5f?-1:1;
                    float yy=h0*2-1;
                    float hw=halfW*Mathf.Max(.3f,1-profile.Shape.w*yy);
                    float bx=side*hw*Mathf.Sqrt(Mathf.Max(0,1-yy*yy))*(.62f+.3f*Noise(n+5));
                    float by=bottom+h0*height-.06f;
                    var dir=new Vector2(side*(.08f+.5f*(1-h0))*(.6f+.8f*Noise(n+6)),1).normalized;
                    var perp=new Vector2(dir.y,-dir.x);
                    float travel=a*(.16f+.1f*Noise(n+7))*profile.Motion.x;
                    float length=(.24f+.16f*Noise(n+2))*(.6f+.6f*Mathf.Sqrt(a))*(1+energy*.35f);
                    float width=(.07f+.04f*Noise(n+3))*(1+energy*.2f);
                    float fadeIn=Mathf.Clamp01(a/.12f);
                    var tint=new Color(Mathf.Repeat(n*.1234f,1f),a,0,fadeIn*.7f);
                    int start=positions.Count;
                    for(int j=0;j<=Rows;j++)
                    {
                        float v=j/(float)Rows;
                        float sway=Mathf.Sin(v*3.1f+time*5.3f+n)*.035f*v;
                        Vector2 c=new Vector2(bx,by)+dir*(travel+length*v)+perp*sway;
                        float lift=Mathf.Clamp01((c.y-bottom)/height);
                        c.x+=leanX*lift*lift;
                        Vector2 l=c-perp*width,r=c+perp*width;
                        positions.Add(Local(l.x,l.y));uv0.Add(new Vector2(0,v));colors.Add(tint);
                        positions.Add(Local(r.x,r.y));uv0.Add(new Vector2(1,v));colors.Add(tint);
                        if(j>0)
                        {
                            int b=start+j*2;
                            triangles.Add(b-2);triangles.Add(b);triangles.Add(b-1);
                            triangles.Add(b-1);triangles.Add(b);triangles.Add(b+1);
                        }
                    }
                }
            }
            lickMesh.Clear(false);
            lickMesh.SetVertices(positions);lickMesh.SetUVs(0,uv0);lickMesh.SetColors(colors);lickMesh.SetTriangles(triangles,0,false);
            lickMesh.bounds=new Bounds(Local(0,1),new Vector3(5*Height,5*Height,.1f));
        }
        void SparkQuad(float x,float y,float halfW,float halfH,float kind,Color color)
        {
            int b=positions.Count;
            positions.Add(Local(x-halfW,y-halfH));positions.Add(Local(x+halfW,y-halfH));
            positions.Add(Local(x-halfW,y+halfH));positions.Add(Local(x+halfW,y+halfH));
            uv0.Add(new Vector2(0,0));uv0.Add(new Vector2(1,0));uv0.Add(new Vector2(0,1));uv0.Add(new Vector2(1,1));
            for(int i=0;i<4;i++){uv1.Add(new Vector2(kind,0));colors.Add(color);}
            triangles.Add(b);triangles.Add(b+2);triangles.Add(b+1);triangles.Add(b+1);triangles.Add(b+2);triangles.Add(b+3);
        }
        // Motes drifting up through the aura, twinkles, and fast streaks that multiply while charging.
        void BuildSparks(float time,bool visible)
        {
            positions.Clear();uv0.Clear();uv1.Clear();colors.Clear();triangles.Clear();
            if(visible)
            {
                float halfW,height,bottom;ShellSize(out halfW,out height,out bottom);
                float energy=Mathf.Clamp01(smoothedCharge*.75f+smoothedFury);
                int streaks=Mathf.RoundToInt(profile.Streaks*(1+energy*2.5f));
                for(int i=0;i<profile.Motes+streaks;i++)
                {
                    bool streak=i>=profile.Motes;
                    float rate=streak?.9f+.6f*Noise(i+.9f):.22f+.2f*Noise(i+.5f);
                    float tick=time*rate*(1+energy*.6f)+Noise(i+3.3f)*9f;
                    int cycle=Mathf.FloorToInt(tick);
                    float a=tick-cycle;
                    float n=i*2.31f+cycle*4.7f;
                    float y=bottom+(Noise(n+2)*.55f+a*.75f)*height;
                    float h=Mathf.Clamp01((y-bottom)/height),yy=h*2-1;
                    float hw=halfW*Mathf.Max(.3f,1-profile.Shape.w*yy)*Mathf.Sqrt(Mathf.Max(.05f,1-yy*yy));
                    float x=(Noise(n)*2-1)*hw*1.1f+Mathf.Sin(time*1.3f+n)*.03f+leanX*h*h;
                    float fade=Mathf.Sin(a*Mathf.PI);
                    var tint=Color.Lerp(profile.MoteA,profile.MoteB,Noise(n+4));
                    if(streak)SparkQuad(x,y,.006f,.05f+.06f*Noise(n+5),2,new Color(tint.r,tint.g,tint.b,fade*.75f));
                    else
                    {
                        float size=(.008f+.012f*Noise(n+3))*(1+energy*.6f);
                        bool star=i%5==0;
                        SparkQuad(x,y,star?size*2.2f:size,star?size*2.2f:size,star?3:1,new Color(tint.r,tint.g,tint.b,fade*.9f));
                    }
                }
            }
            sparkMesh.Clear(false);
            sparkMesh.SetVertices(positions);sparkMesh.SetUVs(0,uv0);sparkMesh.SetUVs(1,uv1);sparkMesh.SetColors(colors);sparkMesh.SetTriangles(triangles,0,false);
            sparkMesh.bounds=new Bounds(Local(0,1),new Vector3(5*Height,5*Height,.1f));
        }
        public void StopEmission() { stopping=true; }
        public bool Stopping { get { return stopping; } }
        void OnDestroy()
        {
            if(lickMesh)Destroy(lickMesh);
            if(sparkMesh)Destroy(sparkMesh);
        }
    }
}
