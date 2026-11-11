using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DBPU
{
    [Serializable] public sealed class RiftMeshDescription
    {
        public string name;public float[] pivot,explode,axis,tint;public float spin,phase;public int layer;
        public Vector3 Pivot {get{return Vector(pivot,Vector3.zero);}}
        public Vector3 Explosion {get{return Vector(explode,Vector3.forward*5);}}
        public Vector3 Axis {get{return Vector(axis,Vector3.up).normalized;}}
        public Color Tint {get{return tint!=null && tint.Length>=3?new Color(tint[0],tint[1],tint[2],1):new Color(.3f,.8f,1);}}
        static Vector3 Vector(float[] a,Vector3 fallback){return a!=null && a.Length>=3?new Vector3(a[0],a[1],a[2]):fallback;}
    }
    [Serializable] public sealed class RiftGeometryDescription
    {public int version;public RiftMeshDescription[] shards,realm;}

    public static class RealityRiftAssets
    {
        static string directory;static bool attempted,ready;static AssetBundle bundle;
        public static Material Composite,Crystal,Environment,Barrier,Backdrop,Bloom;
        public static AudioClip Crack,Ambience,Exit;
        public static Mesh[] Shards,Realm;public static RiftGeometryDescription Geometry;
        public static bool Ready {get{Prepare();return ready;}}
        public static void Initialize(string path){directory=path;}
        static Material LoadMaterial(string name)
        {
            var value=bundle.LoadAsset<Material>("assets/realityrift/generated/"+name+".mat");
            if(!value || !value.shader || !value.shader.isSupported)throw new IOException("Unsupported Reality Rift material: "+name);
            return value;
        }
        static void Prepare()
        {
            if(attempted)return;attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/RealityRift/reality-rift"));
                if(!bundle)throw new IOException("Blender/Unity Reality Rift bundle is missing");
                Composite=LoadMaterial("riftcomposite");Crystal=LoadMaterial("riftcrystal");Environment=LoadMaterial("riftenvironment");
                Barrier=LoadMaterial("riftbarrier");Backdrop=LoadMaterial("riftbackdrop");Bloom=LoadMaterial("riftbloom");
                var metadata=bundle.LoadAsset<TextAsset>("assets/realityrift/imported/reality-rift-geometry.json");
                if(!metadata)throw new IOException("Blender fracture metadata is missing");
                // PPG byte-loads this assembly; Json.NET does not require Unity's
                // editor-time serializable type registry for imported mod types.
                Geometry=Newtonsoft.Json.JsonConvert.DeserializeObject<RiftGeometryDescription>(metadata.text);
                if(Geometry==null || Geometry.shards==null || Geometry.shards.Length<40 || Geometry.shards.Length>60 || Geometry.realm==null || Geometry.realm.Length<20)
                    throw new IOException("Invalid authored fracture/realm geometry: shards="+(Geometry==null || Geometry.shards==null?-1:Geometry.shards.Length)+", realm="+(Geometry==null || Geometry.realm==null?-1:Geometry.realm.Length));
                Shards=new Mesh[Geometry.shards.Length];Realm=new Mesh[Geometry.realm.Length];
                for(int i=0;i<Shards.Length;i++)
                {Shards[i]=bundle.LoadAsset<Mesh>("assets/realityrift/generated/shards"+i.ToString("D4")+".asset");if(!Shards[i])throw new IOException("Missing Blender shard "+i);}
                for(int i=0;i<Realm.Length;i++)
                {Realm[i]=bundle.LoadAsset<Mesh>("assets/realityrift/generated/realm"+i.ToString("D4")+".asset");if(!Realm[i])throw new IOException("Missing Blender realm mesh "+i);}
                Crack=bundle.LoadAsset<AudioClip>("assets/realityrift/imported/audio/rift-crack.wav");
                Ambience=bundle.LoadAsset<AudioClip>("assets/realityrift/imported/audio/rift-ambience.wav");
                Exit=bundle.LoadAsset<AudioClip>("assets/realityrift/imported/audio/rift-exit.wav");
                if(!Crack || !Ambience || !Exit)throw new IOException("Original rift sound design is incomplete");
                ready=true;
            }
            catch(Exception ex){Debug.LogError("[DBPU Reality Rift Assets] "+ex);}
        }
    }

    // Rendering is isolated to two disabled cameras. Participant layers are
    // swapped only inside one synchronous camera.Render and restored in finally.
    [DefaultExecutionOrder(360)]
    public sealed class RealityRiftVisual : MonoBehaviour
    {
        sealed class SpriteState {public SpriteRenderer Renderer;public Color Base,Last;public bool Applied;}
        sealed class LayerState {public GameObject Object;public int Layer;}
        RealityRift owner;Camera view,realmCamera,actorCamera;
        GameObject stage;Transform[] shards,realm;
        MeshRenderer[] shardRenderers,realmRenderers;MeshRenderer backdrop,barrier;
        Mesh sphere,quad;Material composite,bloom;RenderTexture captured,dimension,bloomA,bloomB;
        Texture previousCaptured;RealityRiftCameraEffect proxy;
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        readonly List<SpriteState> sprites=new List<SpriteState>(64);
        readonly List<Renderer> participantRenderers=new List<Renderer>(256);
        readonly List<LayerState> layers=new List<LayerState>(256);
        readonly HashSet<GameObject> layerObjects=new HashSet<GameObject>();
        AudioSource crack,ambience;AudioLowPassFilter lowpass;bool addedLowpass,savedLowpassEnabled,ducking,pausedAudio,disposed,playedExit;
        float savedCutoff,savedResonance,savedVolume,ownedVolume,refreshAge;
        RealityRiftPhase lastPhase;
        int renderLayer;
        public bool SceneCaptured {get;private set;}
        public int RenderedShardCount {get{return shards==null?0:shards.Length;}}
        public int RealmMeshCount {get{return realm==null?0:realm.Length;}}
        public int ParticipantRendererCount {get{return participantRenderers.Count;}}
        public float RealmMix {get;private set;}
        public RenderTexture RealmTexture {get{return dimension;}}
        public Material CompositeMaterial {get{return composite;}}
        public bool AudioDucked {get{return ducking;}}
        float ShaderSeed {get{return ((uint)owner.Seed&65535u)/65535f*97.37f;}}
        Color AdaptiveTint()
        {
            if(DBPU.KiBlast_CustomColorEnabled)return DBPU.GetKiBlastCustomColor(1);
            var a=owner.Attacker?owner.Attacker.GetComponent<KiSystem>():null;var b=owner.Defender?owner.Defender.GetComponent<KiSystem>():null;
            Color tint=Color.Lerp(a?a.requiredColor:new Color(.2f,.8f,1),b?b.requiredColor:new Color(.8f,.2f,1),.5f);
            return new Color(Mathf.Max(.15f,tint.r),Mathf.Max(.15f,tint.g),Mathf.Max(.15f,tint.b),1);
        }

        public static RealityRiftVisual Create(RealityRift owner)
        {
            if(!owner || !RealityRiftAssets.Ready)throw new InvalidOperationException("Reality Rift requires the complete Blender/Unity assets");
            var go=new GameObject("DBPU authored crystalline dimension, scene and sound");go.transform.SetParent(owner.transform,false);
            var result=go.AddComponent<RealityRiftVisual>();result.owner=owner;result.Initialize();return result;
        }
        void Initialize()
        {
            view=Global.main.camera;composite=new Material(RealityRiftAssets.Composite){name="DBPU per-event dimensional composite"};
            bloom=new Material(RealityRiftAssets.Bloom){name="DBPU local crystal glare"};
            previousCaptured=Shader.GetGlobalTexture("_RiftCapturedScene");
            uint used=0;foreach(var renderer in FindObjectsOfType<Renderer>())used|=1u<<renderer.gameObject.layer;
            renderLayer=-1;for(int i=31;i>=0;i--)if((used&(1u<<i))==0){renderLayer=i;break;}
            if(renderLayer<0)throw new InvalidOperationException("No isolated renderer layer is available for the dimensional scene");
            stage=new GameObject("DBPU Blender dimension render stage");stage.transform.SetParent(transform,false);stage.transform.localPosition=new Vector3(0,0,10000);
            realmCamera=NewCamera("DBPU crystalline space camera",stage.transform);
            realmCamera.transform.localPosition=new Vector3(0,0,-10);realmCamera.fieldOfView=53.130102f;realmCamera.nearClipPlane=.05f;realmCamera.farClipPlane=180;
            realmCamera.clearFlags=CameraClearFlags.SolidColor;realmCamera.backgroundColor=new Color(.003f,.004f,.012f,1);
            actorCamera=NewCamera("DBPU isolated live combat render",transform);
            var data=RealityRiftAssets.Geometry;
            shards=new Transform[data.shards.Length];shardRenderers=new MeshRenderer[shards.Length];
            realm=new Transform[data.realm.Length];realmRenderers=new MeshRenderer[realm.Length];
            for(int i=0;i<shards.Length;i++)
            {var renderer=NewMesh(stage.transform,"Blender glass shard "+i,RealityRiftAssets.Shards[i],RealityRiftAssets.Crystal,20);shardRenderers[i]=renderer;shards[i]=renderer.transform;}
            for(int i=0;i<realm.Length;i++)
            {var renderer=NewMesh(stage.transform,"Blender crystalline layer "+i,RealityRiftAssets.Realm[i],RealityRiftAssets.Environment,0);realmRenderers[i]=renderer;realm[i]=renderer.transform;}
            quad=Quad();backdrop=NewMesh(stage.transform,"Raymarched depths beyond the physical crystals",quad,RealityRiftAssets.Backdrop,-100);
            backdrop.transform.localPosition=new Vector3(0,0,85);backdrop.transform.localScale=new Vector3(105,65,1);
            sphere=Sphere();barrier=NewMesh(transform,"Adaptive fractured ki barrier",sphere,RealityRiftAssets.Barrier,44);
            barrier.gameObject.layer=0;barrier.sortingLayerName="Top";
            stage.SetActive(false);
            proxy=view.gameObject.AddComponent<RealityRiftCameraEffect>();proxy.Visual=this;
            CaptureSprites(owner.Attacker);CaptureSprites(owner.Defender);RefreshParticipants();
            var audio=new GameObject("Original dimensional glass and sub-bass");audio.transform.SetParent(transform,false);
            crack=audio.AddComponent<AudioSource>();crack.playOnAwake=false;crack.spatialBlend=0;crack.dopplerLevel=0;crack.bypassListenerEffects=true;
            var low=new GameObject("Dimensional resonant ambience");low.transform.SetParent(transform,false);
            ambience=low.AddComponent<AudioSource>();ambience.playOnAwake=false;ambience.spatialBlend=0;ambience.loop=true;ambience.clip=RealityRiftAssets.Ambience;ambience.volume=0;
            var reverb=low.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.Cave;reverb.decayTime=4.2f;
            BeginDuck();crack.PlayOneShot(RealityRiftAssets.Crack,.8f);lastPhase=owner.Phase;
        }
        Camera NewCamera(string name,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<renderLayer;camera.allowHDR=true;camera.allowMSAA=false;camera.useOcclusionCulling=false;return camera;
        }
        MeshRenderer NewMesh(Transform parent,string name,Mesh mesh,Material material,int order)
        {
            var go=new GameObject(name);go.layer=renderLayer;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        void CaptureSprites(PersonBehaviour person)
        {
            if(!person)return;foreach(var renderer in person.GetComponentsInChildren<SpriteRenderer>(true))
                sprites.Add(new SpriteState{Renderer=renderer,Base=renderer.color,Last=renderer.color});
        }
        void BeginDuck()
        {
            var listener=FindObjectOfType<AudioListener>();if(!listener)return;
            lowpass=listener.GetComponent<AudioLowPassFilter>();addedLowpass=!lowpass;
            if(!lowpass)lowpass=listener.gameObject.AddComponent<AudioLowPassFilter>();
            savedLowpassEnabled=lowpass.enabled;savedCutoff=lowpass.cutoffFrequency;savedResonance=lowpass.lowpassResonanceQ;
            lowpass.enabled=true;lowpass.cutoffFrequency=1200;lowpass.lowpassResonanceQ=1.15f;
            savedVolume=AudioListener.volume;ownedVolume=savedVolume*.50f;AudioListener.volume=ownedVolume;ducking=true;
        }
        void RestoreDuck()
        {
            if(!ducking)return;ducking=false;
            if(Mathf.Abs(AudioListener.volume-ownedVolume)<.0001f)AudioListener.volume=savedVolume;
            if(!lowpass)return;
            if(addedLowpass)Destroy(lowpass);
            else
            {
                if(Mathf.Abs(lowpass.cutoffFrequency-1200)<.1f)lowpass.cutoffFrequency=savedCutoff;
                if(Mathf.Abs(lowpass.lowpassResonanceQ-1.15f)<.001f)lowpass.lowpassResonanceQ=savedResonance;
                lowpass.enabled=savedLowpassEnabled;
            }
            lowpass=null;
        }
        void LateUpdate()
        {
            if(!owner || disposed)return;
            bool frozen=owner.Frozen;
            if(frozen!=pausedAudio)
            {
                pausedAudio=frozen;if(frozen){crack.Pause();ambience.Pause();}else{crack.UnPause();ambience.UnPause();}
            }
            if(frozen)return;
            if(owner.Phase!=lastPhase)
            {
                if(owner.Phase!=RealityRiftPhase.Hitstop)RestoreDuck();
                if(owner.Phase==RealityRiftPhase.Dimension && !ambience.isPlaying)ambience.Play();
                if(owner.Phase==RealityRiftPhase.Exiting && !playedExit){crack.PlayOneShot(RealityRiftAssets.Exit,.9f);playedExit=true;}
                lastPhase=owner.Phase;
            }
            float phase=owner.PhaseAge,dimensionAge=owner.DimensionAge;
            RealmMix=owner.Phase==RealityRiftPhase.Hitstop?Mathf.SmoothStep(0,.48f,Mathf.InverseLerp(.24f,.5f,phase)):
                owner.Phase==RealityRiftPhase.Fracturing?Mathf.SmoothStep(.48f,1,Mathf.Clamp01(phase/.48f)):
                owner.Phase==RealityRiftPhase.Dimension?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01(phase/RealityRift.ExitDuration));
            float envTime=owner.Phase==RealityRiftPhase.Hitstop?phase:
                owner.Phase==RealityRiftPhase.Fracturing?RealityRift.HitstopDuration+phase:
                RealityRift.HitstopDuration+RealityRift.FracturingDuration+dimensionAge+(owner.Phase==RealityRiftPhase.Exiting?phase:0);
            float seed=ShaderSeed,power=owner.Intensity;
            float motionTime=envTime+2.6f*(1-Mathf.Exp(-envTime*4));
            for(int i=0;i<realm.Length;i++)
            {
                var info=RealityRiftAssets.Geometry.realm[i];float localTime=motionTime*(.35f+(i%7)*.027f),p=info.phase+seed;
                realm[i].localPosition=info.Pivot+new Vector3(Mathf.Sin(localTime*.73f+p)*.31f,Mathf.Cos(localTime*.67f+p)*.33f,Mathf.Sin(localTime*.59f+p)*.46f);
                realm[i].localRotation=Quaternion.AngleAxis(info.spin*localTime*.55f,info.Axis)*Quaternion.Euler(Mathf.Sin(localTime*.72f+p)*4,Mathf.Cos(localTime*.65f+p)*5,0);
                block.Clear();block.SetFloat("_DimensionAge",envTime);block.SetFloat("_Seed",seed);block.SetFloat("_Intensity",power);
                block.SetFloat("_Opacity",1);block.SetColor("_Tint",info.Tint);realmRenderers[i].SetPropertyBlock(block);
            }
            float fracture=owner.Phase==RealityRiftPhase.Hitstop?Mathf.SmoothStep(0,.3f,Mathf.InverseLerp(.27f,.5f,phase)):
                owner.Phase==RealityRiftPhase.Fracturing?Mathf.Lerp(.3f,1,Mathf.Clamp01(phase/RealityRift.FracturingDuration)):1;
            float exit=owner.Phase==RealityRiftPhase.Exiting?Mathf.Clamp01(phase/RealityRift.ExitDuration):0;
            for(int i=0;i<shards.Length;i++)
            {
                var info=RealityRiftAssets.Geometry.shards[i];bool visible=owner.Phase!=RealityRiftPhase.Dimension;
                shardRenderers[i].enabled=visible;
                float flight=owner.Phase==RealityRiftPhase.Exiting?Mathf.SmoothStep(0,1,exit):Mathf.SmoothStep(0,1,fracture);
                shards[i].localPosition=info.Pivot+info.Explosion*flight;
                shards[i].localRotation=Quaternion.AngleAxis(info.spin*flight,info.Axis);
                block.Clear();block.SetFloat("_PhaseAge",phase);block.SetFloat("_DimensionAge",dimensionAge);block.SetFloat("_Seed",seed);
                block.SetFloat("_Intensity",power);block.SetFloat("_Opacity",owner.Phase==RealityRiftPhase.Exiting?1-exit*.75f:1-fracture*.92f);
                block.SetFloat("_RiftSceneFlip",0);block.SetColor("_Tint",Color.Lerp(new Color(.08f,1,.86f),new Color(.8f,.10f,1),(i%9)/8f));
                shardRenderers[i].SetPropertyBlock(block);
            }
            block.Clear();block.SetFloat("_DimensionAge",envTime);block.SetFloat("_Seed",seed);block.SetFloat("_Intensity",power);block.SetFloat("_Opacity",1);
            block.SetFloat("_Aspect",view.aspect);backdrop.SetPropertyBlock(block);
            barrier.enabled=owner.Phase==RealityRiftPhase.Hitstop && phase<.315f;
            if(barrier.enabled)
            {
                float radius=Mathf.Clamp(view.orthographicSize*.49f,1.4f,8);
                barrier.transform.position=new Vector3(owner.Centre.x,owner.Centre.y,-.4f);barrier.transform.rotation=Quaternion.Euler(phase*13,phase*19,phase*7);
                barrier.transform.localScale=Vector3.one*radius*2;
                block.Clear();block.SetFloat("_Age",phase);block.SetFloat("_Intensity",power);block.SetFloat("_Seed",seed);
                block.SetFloat("_Dissolve",Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.315f,phase)));block.SetColor("_Tint",AdaptiveTint());barrier.SetPropertyBlock(block);
            }
            float reflex=owner.Phase==RealityRiftPhase.Hitstop?.14f:owner.Phase==RealityRiftPhase.Dimension?.42f:.28f;
            if(owner.Phase==RealityRiftPhase.Exiting)reflex*=1-exit;
            for(int i=0;i<sprites.Count;i++)
            {
                var s=sprites[i];if(!s.Renderer)continue;
                if(s.Applied && !Near(s.Renderer.color,s.Last))s.Base=s.Renderer.color;
                Color acid=Color.Lerp(new Color(.2f,1,.63f),new Color(1,.26f,.87f),Mathf.Sin(envTime*2.1f+i*.31f+seed)*.5f+.5f);
                s.Last=Color.Lerp(s.Base,new Color(s.Base.r*(.45f+acid.r),s.Base.g*(.45f+acid.g),s.Base.b*(.45f+acid.b),s.Base.a),reflex);
                s.Renderer.color=s.Last;s.Applied=true;
            }
            ambience.volume=owner.Phase==RealityRiftPhase.Dimension?Mathf.Min(.42f,dimensionAge*.35f):owner.Phase==RealityRiftPhase.Exiting?.4f*(1-exit):0;
            refreshAge+=Time.unscaledDeltaTime;if(refreshAge>.15f){refreshAge=0;RefreshParticipants();}
        }
        static bool Near(Color a,Color b){return Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)+Mathf.Abs(a.a-b.a)<.002f;}
        void AddRenderers(GameObject root)
        {if(!root)return;foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r && !participantRenderers.Contains(r))participantRenderers.Add(r);}
        void RefreshParticipants()
        {
            participantRenderers.Clear();AddRenderers(owner.Attacker?owner.Attacker.gameObject:null);AddRenderers(owner.Defender?owner.Defender.gameObject:null);
            foreach(var person in new[]{owner.Attacker,owner.Defender})
            {
                var beam=person?person.GetComponent<BeamBehaviour>():null;if(!beam)continue;
                AddRenderers(beam.StartBeam);AddRenderers(beam.EndBeam);if(beam.MiddleBeam && !participantRenderers.Contains(beam.MiddleBeam))participantRenderers.Add(beam.MiddleBeam);
            }
            foreach(var impact in FindObjectsOfType<KiStrikeImpactVfx>())
                if(Vector2.Distance(impact.transform.position,owner.Centre)<35)AddRenderers(impact.gameObject);
        }
        void EnsureTextures(RenderTexture source)
        {
            if(captured && dimension && dimension.width==source.width && dimension.height==source.height && captured.IsCreated() && dimension.IsCreated())return;
            ReleaseTexture(ref captured);ReleaseTexture(ref dimension);ReleaseTexture(ref bloomA);ReleaseTexture(ref bloomB);SceneCaptured=false;
            captured=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGBHalf){name="DBPU captured reality before fracture"};captured.Create();
            dimension=new RenderTexture(source.width,source.height,24,RenderTextureFormat.ARGBHalf){name="DBPU Blender dimension and live fighters"};dimension.Create();
            int width=Mathf.Max(16,source.width/4),height=Mathf.Max(16,source.height/4);
            bloomA=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf){name="DBPU crystal glare A",filterMode=FilterMode.Bilinear};bloomA.Create();
            bloomB=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf){name="DBPU crystal glare B",filterMode=FilterMode.Bilinear};bloomB.Create();
        }
        internal void Render(RenderTexture source,RenderTexture destination)
        {
            if(disposed || !owner || !view || !composite){Graphics.Blit(source,destination);return;}
            EnsureTextures(source);
            if(!SceneCaptured){Graphics.Blit(source,captured);Shader.SetGlobalTexture("_RiftCapturedScene",captured);SceneCaptured=true;}
            var previousActive=RenderTexture.active;
            try
            {
                stage.SetActive(true);realmCamera.aspect=source.width/(float)source.height;realmCamera.targetTexture=dimension;
                bool roaming=owner.Phase==RealityRiftPhase.Dimension;
                realmCamera.transform.localPosition=roaming?new Vector3(Mathf.Sin(owner.DimensionAge*.12f)*.17f,Mathf.Cos(owner.DimensionAge*.14f)*.11f,-10):new Vector3(0,0,-10);
                realmCamera.transform.localRotation=roaming?Quaternion.Euler(Mathf.Sin(owner.DimensionAge*.11f)*.7f,Mathf.Cos(owner.DimensionAge*.13f)*.8f,Mathf.Sin(owner.DimensionAge*.08f)*.6f):Quaternion.identity;
                realmCamera.Render();stage.SetActive(false);
                actorCamera.CopyFrom(view);actorCamera.enabled=false;actorCamera.cullingMask=1<<renderLayer;
                actorCamera.targetTexture=dimension;actorCamera.clearFlags=CameraClearFlags.Depth;actorCamera.allowHDR=true;actorCamera.allowMSAA=false;actorCamera.useOcclusionCulling=false;
                actorCamera.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
                layers.Clear();layerObjects.Clear();
                foreach(var r in participantRenderers)
                    if(r && r.enabled && r.gameObject.activeInHierarchy && layerObjects.Add(r.gameObject))
                    {layers.Add(new LayerState{Object=r.gameObject,Layer=r.gameObject.layer});r.gameObject.layer=renderLayer;}
                try{actorCamera.Render();}
                finally{foreach(var saved in layers)if(saved.Object)saved.Object.layer=saved.Layer;}
            }
            finally{stage.SetActive(false);RenderTexture.active=previousActive;}
            Graphics.Blit(dimension,bloomA,bloom,0);
            for(int pass=0;pass<2;pass++)
            {
                bloom.SetVector("_Direction",new Vector4(1.35f,0,0,0));Graphics.Blit(bloomA,bloomB,bloom,1);
                bloom.SetVector("_Direction",new Vector4(0,1.35f,0,0));Graphics.Blit(bloomB,bloomA,bloom,1);
            }
            Vector3 centre=view.WorldToViewportPoint(new Vector3(owner.Centre.x,owner.Centre.y,0));
            composite.SetFloat("_Phase",(float)owner.Phase);composite.SetFloat("_PhaseAge",owner.PhaseAge);composite.SetFloat("_DimensionAge",owner.DimensionAge);
            composite.SetFloat("_Seed",ShaderSeed);composite.SetFloat("_Intensity",owner.Intensity);composite.SetVector("_Centre",new Vector4(centre.x,centre.y,0,0));
            composite.SetFloat("_Opacity",1);composite.SetFloat("_RealmMix",RealmMix);composite.SetTexture("_RealmTex",dimension);
            composite.SetTexture("_BloomTex",bloomA);composite.SetFloat("_BloomStrength",.45f+owner.Intensity*.12f);
            composite.SetFloat("_AtlasTime",owner.Phase==RealityRiftPhase.Hitstop?0:owner.Phase==RealityRiftPhase.Fracturing?Mathf.Min(.999f,owner.PhaseAge/RealityRift.FracturingDuration):owner.Phase==RealityRiftPhase.Exiting?Mathf.Min(.999f,owner.PhaseAge/RealityRift.ExitDuration):.99f);
            Graphics.Blit(source,destination,composite);
        }
        public void AbortAfterRenderError(Exception ex)
        {Debug.LogError("[DBPU Reality Rift Render] "+ex);if(owner)owner.EndImmediately();}
        static void ReleaseTexture(ref RenderTexture target)
        {if(!target)return;target.Release();Destroy(target);target=null;}
        void OnDestroy()
        {
            if(disposed)return;disposed=true;RestoreDuck();
            foreach(var s in sprites)if(s.Renderer && s.Applied && Near(s.Renderer.color,s.Last))s.Renderer.color=s.Base;
            if(proxy){proxy.Visual=null;Destroy(proxy);}
            if(Shader.GetGlobalTexture("_RiftCapturedScene")==captured)Shader.SetGlobalTexture("_RiftCapturedScene",previousCaptured);
            ReleaseTexture(ref captured);ReleaseTexture(ref dimension);ReleaseTexture(ref bloomA);ReleaseTexture(ref bloomB);
            if(composite)Destroy(composite);if(bloom)Destroy(bloom);if(sphere)Destroy(sphere);if(quad)Destroy(quad);
        }
        static Mesh Quad()
        {
            var mesh=new Mesh{name="Dimensional full view surface"};mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Sphere()
        {
            const int columns=64,rows=32;var vertices=new Vector3[(columns+1)*(rows+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
            {
                int i=row*(columns+1)+col;float y=row/(float)rows,a=col*Mathf.PI*2/columns,b=y*Mathf.PI;
                normals[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));vertices[i]=normals[i]*.5f;uv[i]=new Vector2(col/(float)columns,y);
                if(row==rows || col==columns)continue;int t=(row*columns+col)*6;
                triangles[t]=i;triangles[t+1]=i+columns+1;triangles[t+2]=i+1;triangles[t+3]=i+1;triangles[t+4]=i+columns+1;triangles[t+5]=i+columns+2;
            }
            var mesh=new Mesh{name="Adaptive spherical ki fracture surface"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
    }
    public sealed class RealityRiftCameraEffect : MonoBehaviour
    {
        public RealityRiftVisual Visual;
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!Visual){Graphics.Blit(source,destination);return;}
            try{Visual.Render(source,destination);}
            catch(Exception ex){Graphics.Blit(source,destination);if(Visual)Visual.AbortAfterRenderError(ex);}
        }
    }
}
