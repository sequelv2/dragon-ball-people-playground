using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    public sealed class AuraLook
    {
        public string Id;
        public Color Deep, Mid, Light;
        public int Ribbons;
        public float Speed, Curl, Width;
        public AuraLook(string id, Color deep, Color mid, Color light, int ribbons, float speed, float curl, float width)
        { Id=id; Deep=deep; Mid=mid; Light=light; Ribbons=ribbons; Speed=speed; Curl=curl; Width=width; }
    }
    public static class RefinedAuras
    {
        static Color C(int r,int g,int b) { return new Color(r/255f,g/255f,b/255f,1f); }
        static readonly Dictionary<string, AuraLook> Looks = new Dictionary<string, AuraLook>
        {
            { "Mystic", new AuraLook("Mystic", C(145,161,207), C(226,235,255), C(255,255,255), 16, .40f, .09f, .14f) },
            { "BeastGohan", new AuraLook("BeastGohan", C(144,26,214), C(211,176,255), C(255,252,255), 16, .40f, .09f, .14f) },
            { "Frieza", new AuraLook("Frieza", C(86,15,161), C(178,90,250), C(245,213,255), 16, .40f, .09f, .14f) },
            { "GoldenFrieza", new AuraLook("GoldenFrieza", C(171,55,9), C(255,187,25), C(255,246,162), 16, .40f, .09f, .14f) },
            { "BlackFrieza", new AuraLook("BlackFrieza", C(32,12,56), C(92,61,126), C(217,190,242), 16, .40f, .09f, .14f) },
            { "Broly", new AuraLook("Broly", C(27,142,12), C(138,246,24), C(246,255,173), 16, .40f, .09f, .14f) },
            { "PowerAwakened", new AuraLook("PowerAwakened", C(170,183,53), C(244,245,166), C(255,255,234), 16, .40f, .09f, .14f) },
            { "OrangePiccolo", new AuraLook("OrangePiccolo", C(197,49,6), C(255,140,16), C(255,232,115), 16, .40f, .09f, .14f) },
            { "UE", new AuraLook("UE", C(84,8,159), C(182,45,238), C(255,174,246), 16, .40f, .09f, .14f) },
            { "SSJRose", new AuraLook("SSJRose", C(133,9,64), C(255,75,162), C(255,202,229), 16, .40f, .09f, .14f) },
            { "Buu", new AuraLook("Buu", C(155,16,84), C(250,96,174), C(255,223,235), 16, .40f, .09f, .14f) },
            { "Beerus", new AuraLook("Beerus", C(64,14,135), C(151,61,235), C(220,188,255), 16, .40f, .09f, .14f) },
            { "Whis", new AuraLook("Whis", C(89,113,228), C(156,233,255), C(249,255,255), 16, .40f, .09f, .14f) },
            { "Base", new AuraLook("Base", C(172,172,172), C(231,231,231), C(255,255,255), 12, .28f, .045f, .14f) },
            { "Kaioken", new AuraLook("Kaioken", C(225,0,28), C(255,45,62), C(255,220,212), 25, 1.3f, .19f, .22f) },
            { "SSJ1", new AuraLook("SSJ1", C(244,149,18), C(255,219,69), C(255,249,184), 12, .28f, .045f, .14f) },
            { "SSJ2", new AuraLook("SSJ2", C(244,149,18), C(255,224,79), C(255,251,198), 18, .48f, .075f, .085f) },
            { "SSJ3", new AuraLook("SSJ3", C(245,157,22), C(255,226,89), C(255,251,195), 24, .64f, .055f, .10f) },
            { "SSJGod", new AuraLook("SSJGod", C(245,22,39), C(255,112,12), C(255,241,93), 12, .32f, .145f, .16f) },
            { "SSJBlue", new AuraLook("SSJBlue", C(9,45,168), C(0,207,255), C(105,245,255), 18, .36f, .065f, .14f) },
            { "SSJBlueEvolution", new AuraLook("SSJBlueEvolution", C(24,86,238), C(37,200,255), C(215,255,255), 22, .52f, .12f, .09f) },
            { "KaiokenGold", new AuraLook("KaiokenGold", C(210,0,27), C(255,16,51), C(255,89,108), 25,1.3f,.19f,.22f) },
            { "KaiokenBlue", new AuraLook("KaiokenBlue", C(206,0,25), C(255,3,65), C(255,25,107), 25,1.3f,.19f,.22f) },
            { "UI", new AuraLook("UI", C(95,39,221), C(92,194,255), C(234,247,255), 20,.35f,.12f,.14f) },
            { "MUI", new AuraLook("MUI", C(117,61,243), C(115,218,255), C(248,253,255), 24,.35f,.12f,.14f) }
        };
        static Material surface, mist, cover, godFire;
        static bool failed;
        static string assetRoot;
        // Footprint adjustment for the original prefab only, if native loading fails.
        public static bool UsesOriginal(string id) { return id=="Base"; }
        public static bool Contains(string id) { return id != null && Looks.ContainsKey(id); }
        public static AuraLook Look(string id) { return Looks[id!=null && Looks.ContainsKey(id) ? id : "SSJ1"]; }
        public static void Initialize(string modDirectory)
        {
            if (string.IsNullOrEmpty(modDirectory)) throw new ArgumentException("Mod directory is required");
            failed = false; assetRoot=modDirectory; CinematicAssets.Initialize(modDirectory);
            Debug.Log("[RefinedAuras] GodFilm revision initialized; God and Ultra Instinct body outlines disabled.");
        }
        static bool Prepare()
        {
            if (surface != null) return true;
            if (failed) return false;
            try
            {
                // Unlit vertex colours: particle surface lighting desaturates mesh-based auras.
                var shader=Shader.Find("Sprites/Default");
                if(shader==null) throw new InvalidOperationException("Sprites/Default unavailable");
                var built=new Material(shader) {name="RefinedAura/Unlit vivid HD"};
                built.mainTexture=Texture2D.whiteTexture;
                built.SetColor("_Color",Color.white);
                built.SetColor("_RendererColor",Color.white);
                built.DisableKeyword("PIXELSNAP_ON");
                mist=new Material(built) {name="RefinedAura/Vivid flames and lightning"};
                cover=new Material(built) {name="RefinedAura/Readable colour veil"};
                godFire=new Material(built) {name="RefinedAura/Anime fire atlas"};
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false);
                if(!texture.LoadImage(File.ReadAllBytes(Path.Combine(assetRoot,"Assets/VFX/RefinedAuras/GodAnimeFireAtlas.png")),true))
                    throw new InvalidOperationException("God anime atlas failed to decode");
                texture.filterMode=FilterMode.Trilinear;texture.wrapMode=TextureWrapMode.Clamp;
                godFire.mainTexture=texture;
                surface=built;
                Debug.Log("[RefinedAuras] v6 filled anime field + readable foreground + Kaioken shell.");
                return true;
            }
            catch (Exception ex)
            {
                failed=true;
                Debug.LogWarning("[RefinedAuras] v6 fallback: "+ex.Message); return false;
            }
        }
        public static GameObject Create(string id, bool transition=false)
        {
            if(CinematicAssets.Handles(id,transition))
            {
                var native=CinematicAssets.Create(id,transition);
                if(native!=null)return native;
                // Preserve the game's working original SSJ prefabs if GPU resources are unavailable.
                if(id=="Base"||id=="SSJ1"||id=="SSJ2"||id=="SSJ3"||id=="UI"||id=="MUI"||id=="KaiokenGold"||id=="KaiokenBlue")return null;
            }
            if (transition || CinematicAssets.IsAdditional(id)) return null;
            if (!Contains(id) || !Prepare()) return null;
            var go=new GameObject(transition ? "Ki transformation veil" : "RefinedAura/"+id);
            var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[] { cover, id=="SSJGod"?godFire:mist };
            renderer.sortingLayerName=transition ? "Top" : "Background"; renderer.sortingOrder=transition ? 100 : -10;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            var visual=go.AddComponent<RefinedAuraVisual>();
            visual.Setup(Look(id),go.AddComponent<MeshFilter>(),transition);
            if (!transition)
            {
                var front=new GameObject("Transparent ki over character");
                front.transform.SetParent(go.transform,false);
                var fr=front.AddComponent<MeshRenderer>();
                fr.sharedMaterials=new[] {id=="SSJGod"?godFire:cover};fr.sortingLayerName="Top";fr.sortingOrder=5;
                fr.shadowCastingMode=ShadowCastingMode.Off;fr.receiveShadows=false;
                visual.FrontMesh=new Mesh {name="Readable foreground ki"};
                visual.FrontMesh.MarkDynamic();front.AddComponent<MeshFilter>().sharedMesh=visual.FrontMesh;
            }
            if(!transition && (id=="SSJ2" || id=="SSJ3"))
                visual.AttachMysticLightning();
            return go;
        }
    }
    // Continuous curved geometry. Every vertex remains inside the shared 1.5 x 2.6 envelope.
    public sealed class RefinedAuraVisual : MonoBehaviour
    {
        public float SpeedMultiplier=1f;
        public bool IsTransition;
        public float CoverAmount;
        public bool OuterOnly;
        public Mesh FrontMesh;
        public PersonBehaviour KaiokenPerson;
        public AuraLook TargetLook;
        public float PaletteMix=1f;
        public string ProfileId {get{return look!=null?look.Id:null;}}
        public float ChargeAmount;
        public float OverdriveAmount;
        public bool IsCinematic { get { return cinematic!=null; } }
        public float SmoothedCharge { get { return cinematic!=null?cinematic.Charge:Mathf.Max(ChargeAmount,Mathf.Clamp01(OverdriveAmount)*.9f); } }
        CinematicAuraRig cinematic;
        public void SetupCinematic(AuraLook profile,bool transition)
        {
            look=profile;TargetLook=profile;IsTransition=transition;
            cinematic=gameObject.AddComponent<CinematicAuraRig>();
            cinematic.Setup(profile.Id,transition,GetInstanceID());
            cinematic.SetPalette(profile.Deep,profile.Mid,profile.Light);
            try
            {
                Color tint=profile.Id.StartsWith("Kaioken")?new Color(1,.015f,.035f):profile.Id=="SSJGod"?new Color(1,.28f,.08f):Color.Lerp(profile.Mid,profile.Light,.25f);
                fireLight=ModAPI.CreateLight(transform,tint,3.7f,0);
            }
            catch(Exception ex){Debug.LogWarning("[DBPU Cinematic] Light: "+ex.Message);}
        }
        public void SetStartingLook(AuraLook profile){look=profile;}
        AuraLook look;
        Mesh mesh;
        float renderElapsed;
        LightSprite fireLight;
        ParticleSystem mysticLightning;
        public void AttachMysticLightning()
        {
            if(DBPU.Aura_Mystic==null) { Debug.LogWarning("[RefinedAuras] Mystic prefab unavailable"); return; }
            foreach(var source in DBPU.Aura_Mystic.GetComponentsInChildren<ParticleSystem>(true))
            {
                if(!string.Equals(source.name,"lightning",StringComparison.OrdinalIgnoreCase)) continue;
                // Clone the original effect, retaining texture, material, emission, shape and animation.
                var clone=UnityEngine.Object.Instantiate(source.gameObject,transform,false);
                clone.name="lightning";
                // Original prefab uses a foot origin at scale 1; refined mesh uses a centred origin at 1.25.
                clone.transform.localPosition=new Vector3(0,-1.2f,0);
                clone.transform.localScale=new Vector3(.8f,.8f,1f);
                mysticLightning=clone.GetComponent<ParticleSystem>();
                if(look.Id=="Mystic"||look.Id=="BeastGohan")
                {
                    var main=mysticLightning.main;main.maxParticles=32;
                }
                var renderer=clone.GetComponent<ParticleSystemRenderer>();
                if(renderer!=null) renderer.sortingLayerName="Top"; // Same as AuraHandler's Mystic setup.
                clone.SetActive(true);
                mysticLightning.Clear(true);mysticLightning.Play(true);
                return;
            }
            Debug.LogWarning("[RefinedAuras] Original Mystic lightning child unavailable");
        }
        static float GodHeat(float time) { return .5f+.3f*Mathf.Sin(time*2.2f)+.2f*Mathf.Sin(time*3.7f+.8f); }
        void UpdateFireLight()
        {
            if(fireLight==null) return;
            float heat=GodHeat(clock*(IsTransition?3.4f:1f));
            float fade=opacity*Mathf.SmoothStep(0,1,age/.18f);
            if(retirement>=0f) fade*=1f-Mathf.SmoothStep(0,1,retirement/retireDuration);
            float charge=SmoothedCharge;
            fireLight.Brightness=(cinematic!=null?.20f+.10f*heat+.22f*charge:.50f+.25f*heat)*fade*(1+Mathf.Clamp01(OverdriveAmount)*.7f);
            fireLight.Radius=3.4f+.35f*heat+charge*1.2f;
        }
        void OnDisable() { if(fireLight!=null) fireLight.Brightness=0f; }
        float clock, age, opacity=1f, retirement=-1f, retireDuration;
        Transform follow;
        Vector3 followOffset;
        // --- Motion physics: the plume is a loose mass tethered to the body, not a rigid decal. ---
        // Root (y=0) stays planted; the tip (y=1) is where nearly all the visible displacement happens.
        const float LeanDrag=0.011f;      // world velocity (units/s) -> local lean, before scale correction
        const float LeanMax=0.30f;        // hard cap on lean, in local units (envelope half-width is .75)
        const float LeanSpeedCap=42f;     // anchor deltas above this are treated as a teleport/respawn, not motion
        const float LeanStiffness=90f;    // spring constant pulling the plume back toward centerline
        const float LeanDamping=9f;       // damping ratio ~0.47: one clear overshoot past center, then settles
        Vector3 lastAnchorPos;
        bool anchorInit;
        Vector2 lean, leanVel;
        List<Vector3> vertices, normalData;
        List<Vector4> tangents;
        List<Color> colors, frontColors;
        List<Vector2> uvs;
        List<int> softTriangles, flowTriangles, frontTriangles;
        public void Setup(AuraLook profile, MeshFilter filter, bool transition)
        {
            look=profile; TargetLook=profile; IsTransition=transition;
            anchorInit=false; lean=Vector2.zero; leanVel=Vector2.zero;
            // Allocate legacy CPU mesh buffers only for effects using that fallback.
            vertices=new List<Vector3>(6500);normalData=new List<Vector3>();tangents=new List<Vector4>();
            colors=new List<Color>(6500);uvs=new List<Vector2>(6500);
            softTriangles=new List<int>(6000);flowTriangles=new List<int>(30000);
            frontColors=new List<Color>(6500);frontTriangles=new List<int>(30000);
            mesh=new Mesh { name="Continuous ki surface" }; mesh.MarkDynamic(); filter.sharedMesh=mesh; Render();
            if(profile.Id=="SSJGod")
            {
                try { fireLight=ModAPI.CreateLight(transform,new Color(1f,.72f,.13f,1f),3.5f,0f); }
                catch(Exception ex) { Debug.LogWarning("[RefinedAuras] God light unavailable: "+ex.Message); }
            }
        }
        GodBodyGlow bodyGlow;
        public void BindPerson(PersonBehaviour person)
        {
            bool rim=look.Id.StartsWith("Kaioken")&&look.Id!="KaiokenBlue";
            if(!rim || IsTransition || person==null || bodyGlow!=null) return;
            bodyGlow=gameObject.AddComponent<GodBodyGlow>();
            if(look.Id.StartsWith("Kaioken"))bodyGlow.Tint=new Color(1,.08f,.13f,1);
            bodyGlow.Bind(person);
        }
        public void SetOpacity(float value) { opacity=Mathf.Clamp01(value); }
        public void SetTint(Color primary, Color secondary)
        {
            look=new AuraLook(look.Id,primary*.45f,primary,secondary,look.Ribbons,look.Speed,look.Curl,look.Width); TargetLook=look;
        }
        public void Retire(float seconds, Transform anchor)
        {
            if(cinematic!=null) cinematic.StopEmission();
            if(mysticLightning!=null) mysticLightning.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            retirement=0f; retireDuration=seconds; follow=anchor;
            if (anchor!=null) followOffset=transform.position-anchor.position;
        }
        void LateUpdate()
        {
            if (mesh==null && cinematic==null) return;
            if(KaiokenPerson!=null)
            {
                if(!KaiokenPerson.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
                var ki=KaiokenPerson.GetComponent<Kaioken>();
                if(ki==null || !ki.Active || !ki.AuraON || !DBPU.AurasEnabled) { Destroy(gameObject); return; }
                BindPerson(KaiokenPerson);
                OuterOnly=CinematicAssets.KeepsInnerAura(ki.LastForm);
                float scale=Mathf.Max(.01f,Mathf.Abs(KaiokenPerson.transform.localScale.x));
                var handler=KaiokenPerson.GetComponent<AuraHandler>();
                OverdriveAmount=handler!=null?handler.OverdriveVisualStrength:0;
                float tune=handler!=null?Mathf.Max(.1f,handler.AuraTuneScale/3.4f):1f;
                transform.localScale=new Vector3(1.25f*scale*tune,1.25f*scale*tune,1);
                transform.position=KaiokenPerson.Limbs[2].transform.position+new Vector3(0,.4f*scale+(handler!=null?handler.AuraTuneY+.09f:0),0);
                SpeedMultiplier=1f+Mathf.Clamp(ki.Multiplier/40f,0,.65f);
                if(cinematic==null)GetComponent<MeshRenderer>().sortingOrder=OuterOnly?-8:-10;
                var chargeController=KaiokenPerson.GetComponent<KiChargeModeController>();
                ChargeAmount=chargeController!=null&&chargeController.IsActivelyCharging?1:0;
                float swell=1+SmoothedCharge*.32f;
                transform.localScale=new Vector3(transform.localScale.x*swell,transform.localScale.y*(1+SmoothedCharge*.24f),1);
                if(look.Id=="KaiokenBlue")
                {
                    // Use the same charge envelope as the inner aura: it must not escape the mantle.
                    float grow=Mathf.Clamp(DBPU.KiChargeAuraScaleMultiplier,1f,3f)-1f;
                    float charge=handler!=null&&handler.CurrentAuraID=="SSJBlue"?Mathf.Max(ChargeAmount,OverdriveAmount*.9f):SmoothedCharge;
                    transform.localScale=new Vector3(1.25f*scale*tune*(1+grow*1.52f*charge)*(1+OverdriveAmount*.15f),1.25f*scale*tune*(1+grow*1.12f*charge)*(1+OverdriveAmount*.24f),1);
                }
            }
            bool frozen=Global.main!=null&&Global.main.Paused;
            float dt=frozen?0:Time.deltaTime;
            if(cinematic==null)UpdatePhysics(dt);
            float fury=Mathf.Clamp01(OverdriveAmount);
            age+=dt; clock+=dt*Mathf.Clamp(SpeedMultiplier*(1+fury*1.9f),.1f,6f);
            if (retirement>=0f)
            {
                retirement+=dt;
                if (follow!=null) transform.position=follow.position+followOffset;
                if (retirement>=retireDuration) { Destroy(gameObject); return; }
            }
            UpdateFireLight();
            if(bodyGlow!=null) bodyGlow.Strength=opacity*(retirement>=0f?1f-Mathf.Clamp01(retirement/retireDuration):1f);
            if(cinematic!=null)
            {
                cinematic.SetPalette(Blend(look.Deep,TargetLook.Deep),Blend(look.Mid,TargetLook.Mid),Blend(look.Light,TargetLook.Light));
                float fade=opacity*Mathf.SmoothStep(0,1,age/.12f);
                if(retirement>=0)fade*=1-Mathf.SmoothStep(0,1,retirement/retireDuration);
                cinematic.Tick(clock,dt,ChargeAmount,CoverAmount,fade,frozen,fury);
                return;
            }
            // Movement and lifetime run every frame; only mesh animation is rate limited.
            renderElapsed+=Time.deltaTime;
            float interval=look.Id=="SSJGod"?1f/60f:1f/30f;
            if(IsTransition || renderElapsed>=interval)
            {
                renderElapsed=Mathf.Repeat(renderElapsed,interval);
                Render();
            }
        }
        void UpdatePhysics(float dt)
        {
            Vector3 anchor=transform.position;
            if(!anchorInit) { lastAnchorPos=anchor; anchorInit=true; return; }
            if(dt<=0f) return;
            Vector3 vel=(anchor-lastAnchorPos)/dt;
            lastAnchorPos=anchor;
            // A sudden respawn/repositioning is not body motion; don't let it whip the plume.
            if(vel.magnitude>LeanSpeedCap) vel=vel.normalized*LeanSpeedCap;
            float invScale=1f/Mathf.Max(.05f,Mathf.Abs(transform.localScale.x));
            // Inertia: the mass trails opposite the body's velocity (a torch's flame trailing the swing).
            Vector2 target=new Vector2(-vel.x,-vel.y)*LeanDrag*invScale;
            if(target.magnitude>LeanMax) target=target.normalized*LeanMax;
            Vector2 accel=(target-lean)*LeanStiffness-leanVel*LeanDamping;
            leanVel+=accel*dt;
            lean+=leanVel*dt;
        }
        static float Envelope(float y) { return .75f*Mathf.Pow(Mathf.Max(0f,Mathf.Sin(Mathf.PI*Mathf.Clamp01(y))),.45f); }
        Color Blend(Color a,Color b) { return Color.Lerp(a,b,PaletteMix); }
        void Vertex(float x,float y,float z,Color color,float alpha,float u,float v,float tilt=0f)
        {
            float bound=look.Id=="SSJGod"?.75f:Envelope(y); x=Mathf.Clamp(x,-bound,bound);
            // Elastic lean, rooted at the base: near y=0 the plume is anchored to the body, by y=1 it swings freely.
            float leanWeight=Mathf.Pow(Mathf.Clamp01(y),1.35f);
            x=Mathf.Clamp(x+lean.x*leanWeight,-.75f,.75f);
            float yLocal=Mathf.Clamp((Mathf.Clamp01(y)-.5f)*2.6f+lean.y*leanWeight*.32f,-1.3f,1.3f);
            vertices.Add(new Vector3(x,yLocal,Mathf.Clamp(z,-.14f,.14f)));
            // Unlit shader needs neither normals nor tangents.
            color.a=Mathf.Clamp01(alpha)*opacity*(IsTransition ? 1f : Mathf.SmoothStep(0,1,age/.09f));
            if (retirement>=0) color.a*=1f-Mathf.SmoothStep(0,1,retirement/retireDuration);
            colors.Add(color); uvs.Add(new Vector2(u,v));
        }
        void Quad(List<int> indices,int a,int b,int c,int d)
        { indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(b);indices.Add(d);indices.Add(c); }
        static float Ramp(float low,float high,float value)
        { return Mathf.SmoothStep(0f,1f,Mathf.Clamp01((value-low)/(high-low))); }
        void Render()
        {
            vertices.Clear();normalData.Clear();tangents.Clear();colors.Clear();uvs.Clear();softTriangles.Clear();flowTriangles.Clear();
            Color deep=Blend(look.Deep,TargetLook.Deep), mid=Blend(look.Mid,TargetLook.Mid), light=Blend(look.Light,TargetLook.Light);
            bool god=look.Id=="SSJGod", blue=look.Id=="SSJBlue", evolution=look.Id=="SSJBlueEvolution", kaioken=look.Id=="Kaioken", baseline=look.Id=="Base";
            float time=clock*(IsTransition?3.4f:1.0f)*(look.Id=="SSJ3"?1.4f:look.Id=="SSJ2"?1.2f:1f);
            int bands=god?24:48, rows=god?64:128;
            // Full filled flame field, not a hollow ring. Normal alpha retains the saturated anime palette.
            for(int j=0;j<=rows;j++)
            {
                float y=j/(float)rows;
                for(int i=0;i<=bands;i++)
                {
                    float x=i/(float)bands*2-1, edge=Mathf.Abs(x);
                    float boil=.5f+.25f*Mathf.Sin(y*24-time*10+x*9)+.16f*Mathf.Sin(y*35-time*17-x*13);
                    float lobe=.83f+.17f*Mathf.Pow(.5f+.5f*Mathf.Sin(y*(god?36:22)-time*(god?15:9)+x*5),kaioken?3f:1f);
                    float border=1f-Ramp(lobe-(god?.14f:.055f),lobe,edge);
                    float inner=1f-Ramp(.3f,.86f,edge);
                    float cell=Ramp(.34f,.63f,boil);
                    Color col=Color.Lerp(deep,mid,Ramp(.07f,.75f,inner)+cell*.24f);
                    col=Color.Lerp(col,light,inner*(evolution?.56f:blue?.52f:god?.40f:.48f)*(.63f+.37f*cell));
                    if(god) col=Color.Lerp(col,mid,Ramp(.42f,.70f,boil)*.5f);
                    // Dissipation instead of a lid: each column fades out starting at a different, flickering height.
                    float wisp=.5f+.5f*Mathf.Sin(x*11f-time*7f+y*19f)+.28f*Mathf.Sin(x*23f+time*4.2f-y*31f);
                    float fadeFrom=.62f+.20f*wisp;
                    float topFade=1f-Ramp(fadeFrom,1f,y);
                    float alpha=border*(baseline?.58f:.94f)*Ramp(0,.02f,y)*topFade;
                    if(god)
                    {
                        // Scarlet heat haze, with space between the actual gold flames.
                        col=Color.Lerp(deep,mid,cell*.32f);
                        alpha=0f; // God is formed entirely by separate flames; no filled backing sheet.
                    }
                    if(look.Id=="SSJ2" || look.Id=="SSJ3") alpha*=.78f;
                    if(OuterOnly) alpha*=Ramp(.37f,.63f,edge);
                    if(IsTransition) alpha*=Mathf.Lerp(.14f,1f,CoverAmount);
                    Vertex(x*Envelope(y),y,.08f*boil,col,alpha,i/(float)bands,y,x*.8f);
                    if(j>0 && i>0) { int a=j*(bands+1)+i;Quad(softTriangles,a-bands-2,a-bands-1,a-1,a); }
                }
            }
            // Only this single sheet overlays the sprite: no accumulated foreground bloom.
            frontTriangles.Clear();frontTriangles.AddRange(softTriangles);
            int fieldVertices=vertices.Count;
            frontColors.Clear();
            for(int i=0;i<fieldVertices;i++)
            {
                var c=colors[i];float y=vertices[i].y/2.6f+.5f;
                float flow=.5f+.5f*Mathf.Sin(vertices[i].x*19-y*27+time*13);
                c.a*=y>.65f ? .045f+.035f*flow : .09f+.09f*flow;
                frontColors.Add(c);
            }
            // Broad tapered tongues cross the entire field, with individual births and tips.
            if(god) GodFlames(time,deep,mid,light);
            int count=god?0:look.Ribbons+8;
            for(int tongue=0;tongue<count;tongue++)
            {
                float seed=tongue*2.399963f;
                float travel=Mathf.Repeat(time*(god?.8f:evolution?.75f:kaioken?1.15f:.65f)+seed*.17f,1f);
                float span=god?.47f:kaioken?.27f:.34f;
                int begin=vertices.Count;
                for(int j=0;j<=24;j++)
                {
                    float q=j/24f,y=Mathf.Repeat(travel+q*span,1f);
                    float life=Mathf.Max(0,Mathf.Sin(Mathf.PI*y))*Mathf.Max(0,Mathf.Sin(Mathf.PI*q));
                    float lane=Mathf.Sin(seed)*.8f;
                    if(OuterOnly) lane=(tongue%2==0?-1:1)*(.76f+.15f*Mathf.Sin(seed));
                    float bend=(god?.19f:.095f)*(1+Mathf.Clamp01(OverdriveAmount)*.75f)*Mathf.Sin(y*12-time*6+seed)*life;
                    float center=lane*Envelope(y)+bend;
                    float width=(god?.16f:blue?.22f:evolution?.20f:.12f)*(1+Mathf.Clamp01(OverdriveAmount)*.25f)*Mathf.Pow(Mathf.Max(0,1-q),.65f)*life;
                    for(int k=0;k<5;k++)
                    {
                        float across=k/4f*2-1, ridge=1-across*across;
                        Color col=Color.Lerp(mid,light,ridge*(god?.60f:.86f));
                        float alpha=life*ridge*(baseline?.14f:.27f);
                        Vertex(center+across*width,y,.04f*Mathf.Sin(seed+q*5),col,alpha,k/4f,q);
                        if(j>0 && k>0 && y>=Mathf.Repeat(travel+(j-1)/24f*span,1f))
                        { int a=begin+j*5+k;Quad(flowTriangles,a-6,a-5,a-1,a); }
                    }
                }
            }
            int sparks=god?12:evolution?20:blue?0:8;
            for(int j=0;j<sparks;j++)
            {
                float y=Mathf.Repeat(time*(god?.65f:.65f)+j*.173f,1f);
                float x=Envelope(y)*Mathf.Sin(j*7+time*.3f)*.9f;
                Diamond(x,y,evolution?.016f:.011f,light,Mathf.Pow(Mathf.Sin(y*Mathf.PI),2)*.8f);
            }
            if(blue) BlueGrains(time);
            bool saiyanArcs=look.Id=="SSJ2" || look.Id=="SSJ3";
            if(!saiyanArcs && !evolution && IsTransition && TargetLook.Id!="SSJBlueEvolution")
            for(int side=-1;side<=1;side+=2)
            {
                float tick=time*(look.Id=="SSJ3"?3.6f:3f)+(side>0?.47f:0f);
                int burst=(int)tick;
                float pulse=Mathf.Repeat(tick,1f);
                // Hard attacks and short holds, with a new jagged path on every burst.
                if(pulse>.34f) continue;
                float alpha=(1f-Ramp(.24f,.34f,pulse));
                for(int pass=0;pass<2;pass++)
                for(int j=0;j<16;j++)
                {
                    float y=.10f+j*.05f, yn=y+.05f;
                    float x=side*Envelope(y)*(.77f+.16f*Mathf.Sin(j*7.3f+burst*5.1f+side));
                    float xn=side*Envelope(yn)*(.77f+.16f*Mathf.Sin((j+1)*7.3f+burst*5.1f+side));
                    float width=pass==0?.014f:.0045f;
                    Color c=pass==0?new Color(.12f,.65f,1f):Color.white;
                    Bolt(x,y,xn,yn,width,c,alpha);
                    if(j%4==1)
                        Bolt(x,y,x-side*.12f,yn+.022f,width*.65f,c,alpha*.85f);
                }
            }
            mesh.Clear(false);mesh.SetVertices(vertices);
            mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;
            mesh.SetTriangles(softTriangles,0,false);mesh.SetTriangles(flowTriangles,1,false);
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(1.5f,2.6f,.3f));
            if(FrontMesh!=null)
            {
                while(frontColors.Count<vertices.Count) frontColors.Add(new Color(0,0,0,0));
                FrontMesh.Clear(false);FrontMesh.SetVertices(vertices);
                FrontMesh.SetColors(frontColors);FrontMesh.SetUVs(0,uvs);FrontMesh.subMeshCount=1;
                FrontMesh.SetTriangles(frontTriangles,0,false);FrontMesh.bounds=mesh.bounds;
            }
        }
        void BlueGrains(float time)
        {
            // Fixed mesh budget; independent lifetimes recycle only while invisible.
            for(int i=0;i<120;i++)
            {
                float seed=i*3.713f+19f;
                float travel=time*(.22f+.24f*FireNoise(seed))+FireNoise(seed+1);
                float phase=Mathf.Repeat(travel,1f);
                float cycle=travel-phase;
                float y=.035f+.93f*phase;
                float lane=FireNoise(seed+cycle*7.31f+2)*2f-1f;
                float drift=.055f*Mathf.Sin(time*2.3f+seed)+.024f*Mathf.Sin(time*4.1f+seed*2f);
                float x=Envelope(y)*Mathf.Clamp(lane*.91f+drift,-.95f,.95f);
                float fade=Ramp(0,.12f,phase)*(1f-Ramp(.72f,.98f,phase));
                float size=(.0025f+.0023f*FireNoise(seed+3))*(1f-.45f*Ramp(.65f,1f,phase));
                float alpha=fade*(.65f+.3f*FireNoise(seed+4));
                int b=vertices.Count;
                float sy=size/2.6f;
                Vertex(x-size,y-sy,0,Color.white,alpha,0,0);
                Vertex(x+size,y-sy,0,Color.white,alpha,1,0);
                Vertex(x-size,y+sy,0,Color.white,alpha,0,1);
                Vertex(x+size,y+sy,0,Color.white,alpha,1,1);
                Quad(flowTriangles,b,b+1,b+2,b+3);
            }
        }
        static float FireNoise(float seed) { return Mathf.Repeat(Mathf.Sin(seed*12.9898f)*43758.5453f,1f); }
        static float GodFlameAngle(float time,float seed)
        {
            float wave=.56f*Mathf.Sin(time*1.7f+seed)+.28f*Mathf.Sin(time*2.9f+seed*.37f)+.16f*(FireNoise(seed)-.5f)*2f;
            return Mathf.Clamp(wave,-1f,1f)*(Mathf.PI/6f);
        }
        void GodFlames(float time,Color red,Color orange,Color gold)
        {
            for(int flame=0;flame<18;flame++)
            {
                float tick=time/(1.25f+.65f*FireNoise(flame+1))+flame*.381966f;
                int cycle=(int)tick;
                float phase=Mathf.Repeat(tick,1f),seed=flame*13.731f+cycle*27.17f;
                float life=Ramp(.02f,.18f,phase)*(1f-Ramp(.76f,.98f,phase));
                bool rim=flame<10;
                float side=flame%2==0?-1f:1f;
                float cy=rim?.22f+.53f*(flame/2)/4f:.24f+.47f*FireNoise(seed+2);
                cy=Mathf.Clamp(cy+(phase-.5f)*.12f,.22f,.78f);
                float cx=rim?side*Envelope(cy)*.54f:(FireNoise(seed+3)*2f-1f)*Envelope(cy)*.5f;
                float width=(.38f+.18f*FireNoise(seed+4))*(.9f+.1f*phase);
                float height=.53f+.20f*FireNoise(seed+5);
                float angle=GodFlameAngle(time,seed),sn=Mathf.Sin(angle),cs=Mathf.Sin(angle+Mathf.PI*.5f);
                int first=(int)(FireNoise(seed+8)*4f),second=(first+1+(int)(FireNoise(seed+9)*2f))%4;
                float morph=Ramp(.18f,.82f,phase);
                for(int pass=0;pass<2;pass++)
                {
                    int tile=pass==0?first:second,begin=vertices.Count;
                    float weight=pass==0?1f-morph:morph;
                    for(int j=0;j<=6;j++) for(int k=0;k<=6;k++)
                    {
                        float u=k/6f,v=j/6f;
                        float px=(u-.5f)*width,py=(v-.5f)*height;
                        float warp=Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI);
                        px+=.022f*Mathf.Sin(v*8f-phase*7f+seed)*warp;
                        py+=.018f*Mathf.Sin(u*7f-phase*6f+seed)*warp;
                        float x=cx+px*cs-py*sn,y=cy+(px*sn+py*cs)/2.6f;
                        float alpha=life*weight*(rim?.82f:.70f);
                        // Half-texel inset prevents sampling a neighbouring atlas cell.
                        float tu=(tile%2)*.5f+.0008f+u*.4984f;
                        float tv=(tile/2)*.5f+.0008f+v*.4984f;
                        Vertex(x,y,0,Color.white,alpha,tu,tv);
                        var front=colors[colors.Count-1];
                        float actualY=vertices[vertices.Count-1].y/2.6f+.5f;
                        front.a*=flame%4==0?(actualY>.65f?.025f:.14f):0f;
                        frontColors.Add(front);
                        if(j>0&&k>0)
                        {
                            int n=begin+j*7+k;Quad(flowTriangles,n-8,n-7,n-1,n);
                            if(flame%4==0)Quad(frontTriangles,n-8,n-7,n-1,n);
                        }
                    }
                }
            }
        }
        void Bolt(float x,float y,float xn,float yn,float width,Color color,float alpha)
        {
            int b=vertices.Count;
            Vertex(x-width,y,0,color,alpha,0,0);Vertex(x+width,y,0,color,alpha,1,0);
            Vertex(xn-width,yn,0,color,alpha,0,1);Vertex(xn+width,yn,0,color,alpha,1,1);
            Quad(flowTriangles,b,b+1,b+2,b+3);
        }
        void Diamond(float x,float y,float size,Color color,float alpha)
        {
            int b=vertices.Count;
            Vertex(x-size,y,0,color,0,0,.5f);Vertex(x,y+size*1.8f,0,color,alpha,.5f,1);
            Vertex(x,y-size*1.8f,0,color,alpha,.5f,0);Vertex(x+size,y,0,color,0,1,.5f);Quad(softTriangles,b,b+1,b+2,b+3);
        }
        void OnDestroy() { if(fireLight!=null) Destroy(fireLight.gameObject); if(mesh!=null) Destroy(mesh); if(FrontMesh!=null) Destroy(FrontMesh); }
    }
}
