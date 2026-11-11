using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Per-form values for the Dragon Ball Redux aura shaders. The shaders and silhouettes are Redux's own
    // (Assets/VFX/ReduxAura/redux_aura); these numbers are tuned here against Redux screenshots.
    public sealed class ReduxAuraProfile
    {
        public Vector4 Plume, Dynamics, BodyParams, ShellParams;
        public Vector2 Size;
        public Color Core, Inner, Edge, Highlight, Atmosphere, Accent, Outer;
        public float AtmosphereOpacity, ShellOpacity, BodyOpacity, CoreOpacity, FrontOpacity, SheetOpacity;
        public int Sheets, Motes, Streaks;
        public bool Lightning;
    }

    public static class ReduxAuraAssets
    {
        static AssetBundle bundle;
        static bool attempted;
        static string directory;
        public static Material Body, Glow, Sheets, Effects;
        public static GameObject Lightning;
        static Color C(float r,float g,float b) { return new Color(r,g,b,1); }
        static ReduxAuraProfile Gold(Vector2 size,Vector4 plume,Vector4 dynamics,int sheets,int motes,int streaks,bool lightning)
        {
            return new ReduxAuraProfile
            {
                Size=size,Plume=plume,Dynamics=dynamics,BodyParams=new Vector4(1,.8f,.3f,.12f),ShellParams=new Vector4(1.07f,.44f,0,0),
                Core=C(1,.98f,.86f),Inner=C(1,.82f,.25f),Edge=C(1,.55f,.06f),Highlight=C(1,.97f,.66f),Atmosphere=C(.5f,.33f,.06f),Accent=C(1,.46f,.06f),Outer=C(.85f,.55f,.12f),
                AtmosphereOpacity=.5f,ShellOpacity=1,BodyOpacity=1,CoreOpacity=.65f,FrontOpacity=.8f,SheetOpacity=1,
                Sheets=sheets,Motes=motes,Streaks=streaks,Lightning=lightning
            };
        }
        public static readonly Dictionary<string,ReduxAuraProfile> Profiles=new Dictionary<string,ReduxAuraProfile>
        {
            {"SSJ1",Gold(new Vector2(.86f,.84f),new Vector4(.88f,1f,.3f,.4f),new Vector4(.8f,.5f,2,3),14,10,1,false)},
            {"SSJ2",Gold(new Vector2(.98f,1.04f),new Vector4(.96f,1.16f,.3f,.45f),new Vector4(1f,.65f,1.7f,3),18,14,2,true)},
            {"SSJ3",Gold(new Vector2(1.08f,1.28f),new Vector4(1.04f,1.4f,.3f,.5f),new Vector4(1.2f,.8f,1.5f,3),24,18,3,true)},
        };
        public static void Initialize(string path) { directory=path; }
        public static bool Handles(string id) { return id!=null&&Profiles.ContainsKey(id); }
        public static bool Prepare()
        {
            if(Body)return true;
            if(attempted)return false;
            attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/ReduxAura/redux_aura"));
                // Unity refuses a second copy of a loaded bundle: Dragon Ball Redux may be installed too.
                if(!bundle)foreach(var loaded in AssetBundle.GetAllLoadedAssetBundles())if(loaded.Contains("reduxaura_body")){bundle=loaded;break;}
                if(!bundle)throw new InvalidOperationException("AssetBundle not loaded");
                Body=bundle.LoadAsset<Material>("reduxaura_body");
                Glow=bundle.LoadAsset<Material>("reduxaura_glow");
                Sheets=bundle.LoadAsset<Material>("reduxaura_sheets");
                Effects=bundle.LoadAsset<Material>("reduxaura_effects");
                Lightning=bundle.LoadAsset<GameObject>("reduxaura_lightningprefab");
                if(!Body||!Glow||!Sheets||!Effects)throw new InvalidOperationException("Materials missing");
                if(!Body.shader.isSupported||!Sheets.shader.isSupported||!Effects.shader.isSupported)throw new InvalidOperationException("Unsupported GPU shader");
                return true;
            }
            catch(Exception ex)
            {
                Body=null;
                Debug.LogWarning("[DBPU ReduxAura] Original effects retained: "+ex.Message);
                return false;
            }
        }
        public static GameObject Create(string id,bool transition)
        {
            if(!Handles(id)||!Prepare())return null;
            return new GameObject((transition?"Redux transformation veil/":"ReduxAura/")+id);
        }
    }

    // The Redux look: the same layered quads, shard sheets, motes and lightning the shaders were written for.
    // Depends on UnityEngine only, so the preview project can render it without the game.
    public sealed class ReduxAuraRig : MonoBehaviour
    {
        // The shaders put uv.y=0 at local y=-Height/2: the feet, at the scale AuraHandler gives refined auras.
        const float Height=1.75f;
        const float HalfWidth=2.4f*2.2f,Top=2.4f*3.1f,Below=2.4f*.4f;
        // The silhouettes start above uv.y=0, so their layers sit lower to root the flames at the feet.
        const float FootDrop=.9f,GroundDrop=.5f;
        enum Mode {BodyBack=0,AtmosphereBack=1,FlowFront=2,CoreBack=3,SaturatedOuterShell=5}
        struct Layer {public Renderer Renderer;public MaterialPropertyBlock Block;public float Mode;public float Opacity;public bool Sheet;}
        static Mesh quad;
        readonly List<Layer> layers=new List<Layer>();
        ReduxAuraProfile profile;
        bool transition,stopping,paused;
        float seed,smoothedCharge,smoothedFury,age,stream;
        Vector3 lastPosition;
        bool hasPosition;
        Vector2 direction=Vector2.right;
        Color deep,mid,hot;
        Color core,inner,edge,highlight,atmosphere,accent,outer;
        Mesh sheetMesh,effectMesh;
        ParticleSystem lightning;
        readonly List<Vector3> positions=new List<Vector3>();
        readonly List<Vector2> uv0=new List<Vector2>(),uv1=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        public float Charge {get{return smoothedCharge;}}
        public float Overdrive {get{return smoothedFury;}}

        static Mesh Quad()
        {
            if(quad)return quad;
            quad=new Mesh {name="Redux aura quad"};
            float bottom=-Height*.5f-Below;
            quad.vertices=new[]{new Vector3(-HalfWidth,bottom,0),new Vector3(HalfWidth,bottom,0),new Vector3(-HalfWidth,Top+bottom,0),new Vector3(HalfWidth,Top+bottom,0)};
            quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            quad.colors=new[]{Color.white,Color.white,Color.white,Color.white};
            quad.triangles=new[]{0,2,1,1,2,3};
            quad.bounds=new Bounds(new Vector3(0,bottom+Top*.5f,0),new Vector3(HalfWidth*2,Top,.1f));
            quad.hideFlags=HideFlags.DontUnloadUnusedAsset;
            return quad;
        }
        Renderer Child(string childName,Mesh mesh,Material material,bool front,int order,float drop=0)
        {
            var go=new GameObject(childName);
            go.transform.SetParent(transform,false);
            go.transform.localPosition=new Vector3(0,-drop,0);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.sortingLayerName=front?"Top":"Background";r.sortingOrder=order;
            return r;
        }
        void AddQuad(string childName,Material material,Mode mode,bool front,int order,float opacity,float drop)
        {
            if(opacity<=0)return;
            layers.Add(new Layer{Renderer=Child(childName,Quad(),material,front,order,drop),Block=new MaterialPropertyBlock(),Mode=(float)mode,Opacity=opacity});
        }
        public void Setup(string id,bool isTransition,int identity=0)
        {
            profile=ReduxAuraAssets.Profiles[id];
            transition=isTransition;
            seed=Mathf.Repeat(identity*.618034f,17f);
            core=profile.Core;inner=profile.Inner;edge=profile.Edge;highlight=profile.Highlight;
            atmosphere=profile.Atmosphere;accent=profile.Accent;outer=profile.Outer;
            AddQuad("Redux atmosphere",ReduxAuraAssets.Glow,Mode.AtmosphereBack,false,-13,profile.AtmosphereOpacity,GroundDrop);
            AddQuad("Redux outer shell",ReduxAuraAssets.Body,Mode.SaturatedOuterShell,false,-12,profile.ShellOpacity,FootDrop);
            AddQuad("Redux body",ReduxAuraAssets.Body,Mode.BodyBack,false,-11,profile.BodyOpacity,FootDrop);
            AddQuad("Redux core",ReduxAuraAssets.Body,Mode.CoreBack,false,-10,profile.CoreOpacity,GroundDrop);
            sheetMesh=new Mesh {name="Redux sheets"};sheetMesh.MarkDynamic();
            layers.Add(new Layer{Renderer=Child("Redux sheets",sheetMesh,ReduxAuraAssets.Sheets,false,-9,FootDrop),Block=new MaterialPropertyBlock(),Opacity=profile.SheetOpacity,Sheet=true});
            AddQuad("Redux front flow",ReduxAuraAssets.Body,Mode.FlowFront,true,4,profile.FrontOpacity,FootDrop);
            if(transition)return;
            effectMesh=new Mesh {name="Redux motes"};effectMesh.MarkDynamic();
            Child("Redux motes",effectMesh,ReduxAuraAssets.Effects,true,6,GroundDrop);
            if(profile.Lightning&&ReduxAuraAssets.Lightning)
            {
                var clone=UnityEngine.Object.Instantiate(ReduxAuraAssets.Lightning,transform,false);
                clone.name="lightning";
                clone.transform.localPosition=Vector3.zero;clone.transform.localScale=Vector3.one*.8f;
                var r=clone.GetComponent<ParticleSystemRenderer>();
                if(r!=null){r.sortingLayerName="Top";r.sortingOrder=7;}
                lightning=clone.GetComponent<ParticleSystem>();
                if(lightning!=null){lightning.useAutoRandomSeed=false;lightning.randomSeed=(uint)(Mathf.Abs(identity)+41);lightning.Play(true);}
            }
        }
        public void SetPalette(Color a,Color b,Color c){deep=a;mid=b;hot=c;}
        // A custom transformation colour replaces the gold in every layer.
        public void Recolor(Color primary,Color bright)
        {
            core=Color.Lerp(bright,Color.white,.75f);inner=Color.Lerp(primary,bright,.6f);edge=primary;highlight=Color.Lerp(bright,Color.white,.4f);
            atmosphere=primary*.5f;accent=primary;outer=primary*.85f;
            core.a=inner.a=edge.a=highlight.a=atmosphere.a=accent.a=outer.a=1;
        }
        public void Tick(float clock,float delta,float charge,float cover,float opacity,bool freeze,float overdrive=0)
        {
            if(profile==null)return;
            if(freeze!=paused)
            {
                paused=freeze;
                if(lightning){if(freeze)lightning.Pause(true);else {lightning.Play(true);if(stopping)lightning.Stop(true,ParticleSystemStopBehavior.StopEmitting);}}
            }
            if(freeze)return;
            age+=delta;
            smoothedFury=Mathf.MoveTowards(smoothedFury,Mathf.Clamp01(overdrive),delta*3.5f);
            float effectiveCharge=Mathf.Max(Mathf.Clamp01(charge),smoothedFury*.9f);
            smoothedCharge=Mathf.MoveTowards(smoothedCharge,effectiveCharge,delta*(effectiveCharge>smoothedCharge?2.5f:3f));
            // The plume trails the body's motion.
            Vector3 position=transform.position;
            if(hasPosition&&delta>0)
            {
                Vector2 velocity=(position-lastPosition)/delta;
                float speed=velocity.magnitude;
                if(speed>.2f&&speed<60f)direction=Vector2.Lerp(direction,-velocity/speed,Mathf.Clamp01(delta*8f)).normalized;
                stream=Mathf.MoveTowards(stream,Mathf.Clamp01(speed/18f),delta*4f);
            }
            lastPosition=position;hasPosition=true;
            float time=clock+seed;
            float flash=transition?cover:Mathf.Clamp01(1-age/.45f);
            var state=new Vector4(smoothedCharge,flash,0,.5f+.5f*Mathf.Sin(time*6f));
            float grow=1+smoothedCharge*.4f+smoothedFury*.2f;
            var expansion=new Vector4(profile.Size.x*grow,Mathf.Min(1.36f,profile.Size.y*(1+smoothedCharge*.28f+smoothedFury*.2f)),0,0);
            var flare=new Vector4(1+flash*.12f,1+flash*.22f,flash,0);
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                if(!layer.Renderer)continue;
                var block=layer.Block;
                block.SetColor("_CoreColor",core);block.SetColor("_InnerColor",inner);block.SetColor("_EdgeColor",edge);
                block.SetColor("_HighlightColor",highlight);block.SetColor("_AccentColor",accent);
                block.SetVector("_Plume",profile.Plume);block.SetVector("_Dynamics",profile.Dynamics);block.SetVector("_ShellParams",profile.ShellParams);
                block.SetVector("_Expansion",expansion);block.SetVector("_State",state);block.SetVector("_Flare",flare);
                block.SetVector("_VelocityDirection",direction);block.SetFloat("_StreamBlend",stream);
                block.SetFloat("_CharacterHeight",Height);block.SetFloat("_PhaseOffset",seed);block.SetFloat("_PreviewTime",time);
                float alpha=layer.Opacity*opacity;
                if(transition)alpha*=cover;
                if(!layer.Sheet)
                {
                    block.SetColor("_AtmosphereColor",atmosphere);block.SetColor("_OuterColor",outer);
                    block.SetVector("_BodyParams",profile.BodyParams);
                    block.SetFloat("_LayerMode",layer.Mode);block.SetFloat("_UsePreviewTime",1);
                    block.SetFloat("_Opacity",alpha);
                }
                else BuildSheets(time,alpha);
                layer.Renderer.SetPropertyBlock(block);
            }
            if(effectMesh)BuildEffects(time,opacity);
        }
        static float Noise(float n) { return Mathf.Repeat(Mathf.Sin(n*12.9898f)*43758.5453f,1f); }
        // Shards fan out of the ground and live briefly; the shader cuts them against the silhouette.
        void BuildSheets(float time,float alpha)
        {
            positions.Clear();uv0.Clear();colors.Clear();triangles.Clear();
            const int Rows=10;
            for(int s=0;s<profile.Sheets;s++)
            {
                float rate=.45f+.35f*Noise(s+1.3f);
                float tick=time*rate+Noise(s+7.1f)*11f;
                int cycle=Mathf.FloorToInt(tick);
                float life=tick-cycle;
                float n=s*3.17f+cycle*5.3f;
                float side=Noise(n)*2-1;
                float fade=Mathf.SmoothStep(0,1,life/.2f)*(1-Mathf.SmoothStep(.6f,1,life))*alpha;
                float length=Height*(.8f+1.3f*Noise(n+1))*(1+smoothedCharge*.4f)*(.75f+.25f*life);
                float width=Height*(.07f+.09f*Noise(n+2));
                float baseX=side*Height*.42f;
                float lean=side*(.25f+.55f*Noise(n+3));
                int start=positions.Count;
                for(int j=0;j<=Rows;j++)
                {
                    float v=j/(float)Rows;
                    float cx=baseX+lean*length*v*v+Mathf.Sin(v*4+time*2+n)*.03f*Height*v;
                    float y=-Height*.3f+length*v;
                    float w=width*(1-v);
                    var color=new Color(Noise(n+4),life,0,fade);
                    positions.Add(new Vector3(cx-w,y,0));uv0.Add(new Vector2(0,v));colors.Add(color);
                    positions.Add(new Vector3(cx+w,y,0));uv0.Add(new Vector2(1,v));colors.Add(color);
                    if(j>0)
                    {
                        int a=start+j*2;
                        triangles.Add(a-2);triangles.Add(a);triangles.Add(a-1);
                        triangles.Add(a-1);triangles.Add(a);triangles.Add(a+1);
                    }
                }
            }
            sheetMesh.Clear(false);
            sheetMesh.SetVertices(positions);sheetMesh.SetUVs(0,uv0);sheetMesh.SetColors(colors);sheetMesh.SetTriangles(triangles,0,false);
            sheetMesh.bounds=new Bounds(new Vector3(0,Height,0),new Vector3(Height*5,Height*5,.1f));
        }
        void EffectQuad(float x,float y,float halfW,float halfH,float kind,Color color)
        {
            int b=positions.Count;
            positions.Add(new Vector3(x-halfW,y-halfH,0));positions.Add(new Vector3(x+halfW,y-halfH,0));
            positions.Add(new Vector3(x-halfW,y+halfH,0));positions.Add(new Vector3(x+halfW,y+halfH,0));
            uv0.Add(new Vector2(0,0));uv0.Add(new Vector2(1,0));uv0.Add(new Vector2(0,1));uv0.Add(new Vector2(1,1));
            for(int i=0;i<4;i++){uv1.Add(new Vector2(kind,0));colors.Add(color);}
            triangles.Add(b);triangles.Add(b+2);triangles.Add(b+1);triangles.Add(b+1);triangles.Add(b+2);triangles.Add(b+3);
        }
        // Kind: 1 soft mote with a halo, 2 thin rising streak, 3 four-point star.
        void BuildEffects(float time,float opacity)
        {
            positions.Clear();uv0.Clear();uv1.Clear();colors.Clear();triangles.Clear();
            float boost=1+smoothedCharge*.8f+smoothedFury;
            for(int i=0;i<profile.Motes+profile.Streaks;i++)
            {
                bool streak=i>=profile.Motes;
                float rate=streak?.5f:.16f+.16f*Noise(i+.5f);
                float tick=time*rate+Noise(i+3.3f)*9f;
                int cycle=Mathf.FloorToInt(tick);
                float life=tick-cycle;
                float n=i*2.31f+cycle*4.7f;
                float fade=Mathf.Sin(life*Mathf.PI)*opacity;
                float x=(Noise(n)*2-1)*Height*(.45f+.55f*Noise(n+1));
                float y=-Height*.45f+(Noise(n+2)*.5f+life*.9f)*Height*1.3f;
                x+=Mathf.Sin(time*.9f+n)*.05f*Height;
                float size=Height*(.012f+.018f*Noise(n+3));
                Color tint=Color.Lerp(mid,hot,Noise(n+4));
                if(streak)EffectQuad(x,y,Height*.012f,Height*(.09f+.08f*Noise(n+5)),2,new Color(tint.r,tint.g,tint.b,fade*.5f));
                else EffectQuad(x,y,size*boost,size*boost,i%5==0?3:1,new Color(tint.r,tint.g,tint.b,fade*.9f));
            }
            effectMesh.Clear(false);
            effectMesh.SetVertices(positions);effectMesh.SetUVs(0,uv0);effectMesh.SetUVs(1,uv1);effectMesh.SetColors(colors);effectMesh.SetTriangles(triangles,0,false);
            effectMesh.bounds=new Bounds(new Vector3(0,Height,0),new Vector3(Height*5,Height*5,.1f));
        }
        public void StopEmission()
        {stopping=true;if(lightning)lightning.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        public void StepPreview(float seconds)
        {if(lightning)lightning.Simulate(seconds,true,false,true);}
        void OnDestroy()
        {
            if(sheetMesh)Destroy(sheetMesh);
            if(effectMesh)Destroy(effectMesh);
        }
    }
}
