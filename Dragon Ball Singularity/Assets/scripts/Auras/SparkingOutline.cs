using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Shaders for the Sparking outline, and the distance field baked once for each sprite they draw.
    public static class SparkingOutlineAssets
    {
        public sealed class Baked { public Texture2D Field; public Mesh Quad; }
        // Thickness is authored in the game's pixels, so sprites of any resolution get the same outline.
        public const float PixelsPerUnit=35,RangePixels=7;
        const int Oversample=3,MaxSide=256,MaxSearch=24;
        static readonly Dictionary<Sprite,Baked> baked=new Dictionary<Sprite,Baked>();
        static string directory;
        static bool attempted;
        static AssetBundle bundle;
        static Material bake;
        public static Material Draw { get; private set; }

        public static void Initialize(string path) { directory=path; }
        public static bool Prepare()
        {
            if(Draw)return true;
            if(attempted)return false;
            attempted=true;
            try
            {
                bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/VFX/SparkingOutline/sparking-outline"));
                if(!bundle)throw new IOException("Bundle is missing");
                var drawShader=bundle.LoadAsset<Shader>("assets/sparkingoutline/sparkingoutline.shader");
                var bakeShader=bundle.LoadAsset<Shader>("assets/sparkingoutline/sparkingoutlinebake.shader");
                if(!drawShader||!bakeShader||!drawShader.isSupported||!bakeShader.isSupported)throw new IOException("Shader is unsupported");
                bake=new Material(bakeShader){name="Sparking outline bake",hideFlags=HideFlags.DontUnloadUnusedAsset};
                Draw=new Material(drawShader){name="Sparking outline",hideFlags=HideFlags.DontUnloadUnusedAsset};
                return true;
            }
            catch(Exception ex)
            {
                Debug.LogWarning("[DBPU SparkingOutline] Outline unavailable: "+ex.Message);
                return false;
            }
        }

        public static Baked Get(Sprite sprite)
        {
            Baked value;
            if(baked.TryGetValue(sprite,out value))return value;
            Forget();
            value=Bake(sprite);
            baked.Add(sprite,value);
            return value;
        }

        // Skins can create and destroy sprites at runtime.
        static void Forget()
        {
            foreach(var dead in baked.Keys.Where(key=>!key).ToArray())
            {
                UnityEngine.Object.Destroy(baked[dead].Field);
                UnityEngine.Object.Destroy(baked[dead].Quad);
                baked.Remove(dead);
            }
        }

        static Baked Bake(Sprite sprite)
        {
            var texture=sprite.texture;
            Vector2 min=new Vector2(1,1),max=Vector2.zero;
            foreach(var uv in sprite.uv){min=Vector2.Min(min,uv);max=Vector2.Max(max,uv);}
            var bounds=sprite.bounds;
            float pad=RangePixels/PixelsPerUnit;
            Vector2 uvPerUnit=new Vector2((max.x-min.x)/Mathf.Max(.001f,bounds.size.x),(max.y-min.y)/Mathf.Max(.001f,bounds.size.y));
            Vector2 lower=min-uvPerUnit*pad,upper=max+uvPerUnit*pad;
            float rangeTexels=pad*sprite.pixelsPerUnit;

            int width=Mathf.Clamp(Mathf.CeilToInt((upper.x-lower.x)*texture.width*Oversample),8,MaxSide);
            int height=Mathf.Clamp(Mathf.CeilToInt((upper.y-lower.y)*texture.height*Oversample),8,MaxSide);
            bake.SetVector("_SpriteRect",new Vector4(min.x,min.y,max.x,max.y));
            bake.SetVector("_Extended",new Vector4(lower.x,lower.y,upper.x,upper.y));
            bake.SetFloat("_Search",Mathf.Min(MaxSearch,Mathf.CeilToInt(rangeTexels)+1));
            bake.SetFloat("_RangeTexels",rangeTexels);

            var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var active=RenderTexture.active;
            Graphics.Blit(texture,target,bake);
            RenderTexture.active=target;
            // Copied out of the render texture because a render texture can lose its contents with the device.
            var field=new Texture2D(width,height,TextureFormat.RGBA32,false,true){name="Sparking outline field",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontUnloadUnusedAsset};
            field.ReadPixels(new Rect(0,0,width,height),0,0,false);
            field.Apply(false,true);
            RenderTexture.active=active;
            RenderTexture.ReleaseTemporary(target);

            float x0=bounds.min.x-pad,x1=bounds.max.x+pad,y0=bounds.min.y-pad,y1=bounds.max.y+pad;
            var quad=new Mesh{name="Sparking outline quad",hideFlags=HideFlags.DontUnloadUnusedAsset};
            quad.vertices=new[]{new Vector3(x0,y0,0),new Vector3(x1,y0,0),new Vector3(x1,y1,0),new Vector3(x0,y1,0)};
            quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            quad.triangles=new[]{0,1,2,0,2,3};
            quad.RecalculateBounds();
            return new Baked{Field=field,Quad=quad};
        }
    }

    // Draws the outline behind the whole body while Sparking is overloaded. Nothing is created until the first
    // ignition. The quads live under their own root, so copying a fighter does not copy them and nothing that
    // scans the body's hierarchy finds them.
    [DefaultExecutionOrder(320)]
    public sealed class SparkingOutline : MonoBehaviour
    {
        sealed class Part
        {
            public SpriteRenderer Source;
            public Transform Transform;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public Sprite Sprite;
            public MaterialPropertyBlock Block;
        }
        const float IgniteSeconds=.2f,BurstSeconds=.55f,FadeOutSeconds=.4f,RescanSeconds=.75f,RecolorSpeed=7;
        readonly List<Part> parts=new List<Part>();
        readonly HashSet<SpriteRenderer> known=new HashSet<SpriteRenderer>();
        KiSparkingController controller;
        PersonBehaviour person;
        AuraHandler aura;
        Transform root;
        float clock,fade,burst,seed,rescan;
        bool wasOverloaded;
        Color line=Color.white,fringe=new Color(1,.84f,.3f),halo=new Color(.95f,.62f,.14f);

        public float Fade { get { return fade; } }
        public Color Halo { get { return halo; } }
        public int PartCount { get { return parts.Count; } }
        public int ShownParts { get { return parts.Count(part=>part.Renderer&&part.Renderer.enabled); } }

        void Awake()
        {
            controller=GetComponent<KiSparkingController>();
            person=GetComponent<PersonBehaviour>();
            aura=GetComponent<AuraHandler>();
            seed=Mathf.Repeat(GetInstanceID()*.618034f,17f);
        }

        // Dark palettes such as Black Frieza's would vanish under BlendOp Max, so hue is kept and brightness is not.
        static Color Lit(Color color,float peak)
        {
            float top=Mathf.Max(.001f,color.r,color.g,color.b)/peak;
            return new Color(color.r/top,color.g/top,color.b/top,1);
        }

        void FollowAuraColor(float blend)
        {
            if(aura==null)return;
            Color deep,mid,light;
            aura.GetAuraColors(out deep,out mid,out light);
            line=Color.Lerp(line,Color.Lerp(Lit(light,1),Color.white,.5f),blend);
            fringe=Color.Lerp(fringe,Lit(mid,1),blend);
            halo=Color.Lerp(halo,Lit(Color.Lerp(mid,deep,.45f),.95f),blend);
        }

        void Scan()
        {
            if(!root)root=new GameObject("Sparking outline").transform;
            // Limbs sit in three sorting layers: the far arm and leg in Background, the near ones in Foreground.
            // The outline goes under the lowest of them, which is still above the back auras (order -8 and below).
            int lowestLayer=int.MaxValue,lowest=0,layer=0;
            foreach(var limb in person.Limbs)
            {
                if(limb==null)continue;
                foreach(var source in limb.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(Skip(source.name))continue;
                    int value=SortingLayer.GetLayerValueFromID(source.sortingLayerID);
                    if(value<lowestLayer||value==lowestLayer&&source.sortingOrder<lowest){lowestLayer=value;lowest=source.sortingOrder;layer=source.sortingLayerID;}
                    if(!known.Add(source))continue;
                    var go=new GameObject("Sparking outline part");
                    go.transform.SetParent(root,false);
                    var renderer=go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=SparkingOutlineAssets.Draw;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    parts.Add(new Part{Source=source,Transform=go.transform,Renderer=renderer,Filter=go.AddComponent<MeshFilter>(),Block=new MaterialPropertyBlock()});
                }
            }
            foreach(var part in parts){part.Renderer.sortingLayerID=layer;part.Renderer.sortingOrder=lowest-1;}
        }

        static bool Skip(string name)
        {
            name=name.ToLowerInvariant();
            return name.Contains("highlight")||name.Contains("selection")||name.Contains("outline")||name.Contains("hover")||name.Contains("glow");
        }

        void LateUpdate()
        {
            if(controller==null||person==null)return;
            bool overloaded=controller.CurrentPhase==KiSparkingController.SparkPhase.Overload;
            if(overloaded&&!wasOverloaded)burst=1;
            wasOverloaded=overloaded;
            if(!overloaded&&fade<=0)return;
            if(!SparkingOutlineAssets.Prepare())return;

            bool paused=Global.main!=null&&Global.main.Paused;
            float step=paused?0:Time.deltaTime;
            clock+=step;
            // A new form mid-Sparking blends over; a fresh ignition starts in the right colour.
            FollowAuraColor(fade<=0?1:Mathf.Clamp01(step*RecolorSpeed));
            fade=Mathf.MoveTowards(fade,overloaded?1:0,step/(overloaded?IgniteSeconds:FadeOutSeconds));
            burst=Mathf.MoveTowards(burst,0,step/BurstSeconds);
            rescan-=step;
            if(rescan<=0||parts.Count==0){rescan=RescanSeconds;Scan();}

            float eased=burst*burst;
            for(int i=parts.Count-1;i>=0;i--)
            {
                var part=parts[i];
                var source=part.Source;
                if(source==null)
                {
                    if(part.Renderer)Destroy(part.Renderer.gameObject);
                    parts.RemoveAt(i);
                    continue;
                }
                bool visible=fade>0&&source.enabled&&source.gameObject.activeInHierarchy&&source.sprite!=null&&source.color.a>.01f;
                part.Renderer.enabled=visible;
                if(!visible)continue;
                if(source.sprite!=part.Sprite)
                {
                    part.Sprite=source.sprite;
                    var baked=SparkingOutlineAssets.Get(part.Sprite);
                    part.Filter.sharedMesh=baked.Quad;
                    part.Block.SetTexture("_MainTex",baked.Field);
                    part.Block.SetFloat("_Range",SparkingOutlineAssets.RangePixels);
                    part.Block.SetFloat("_Seed",seed);
                }
                var from=source.transform;
                var scale=from.lossyScale;
                part.Transform.SetPositionAndRotation(from.position,from.rotation);
                part.Transform.localScale=new Vector3(source.flipX?-scale.x:scale.x,source.flipY?-scale.y:scale.y,1);
                part.Block.SetFloat("_Fade",fade*source.color.a);
                part.Block.SetFloat("_Burst",eased);
                part.Block.SetFloat("_Clock",clock);
                part.Block.SetColor("_Line",line);
                part.Block.SetColor("_Fringe",fringe);
                part.Block.SetColor("_Halo",halo);
                part.Renderer.SetPropertyBlock(part.Block);
            }
        }

        void OnDisable()
        {
            foreach(var part in parts)if(part.Renderer)part.Renderer.enabled=false;
        }

        void OnDestroy()
        {
            if(root)Destroy(root.gameObject);
        }
    }
}
