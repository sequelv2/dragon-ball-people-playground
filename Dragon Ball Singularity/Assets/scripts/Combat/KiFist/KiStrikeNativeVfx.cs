using UnityEngine;

namespace DBPU
{
    // The only trail geometry is the path swept by the physical limb. All
    // plasma structure and depth are integrated from the Unity 3D density field.
    [DefaultExecutionOrder(280)]
    public sealed class KiStrikeLimbVfx : MonoBehaviour
    {
        const int Samples=40;
        KiFistHand owner;
        MeshRenderer coating,trail;
        Mesh wake;
        readonly Vector3[] points=new Vector3[Samples];
        readonly float[] born=new float[Samples];
        readonly Vector3[] vertices=new Vector3[Samples*2];
        readonly Color[] colors=new Color[Samples*2];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        float clock;int count;bool showing;

        public static KiStrikeLimbVfx Create(KiFistHand owner)
        {
            if(!owner || !KiCombatVfxAssets.Ready)return null;
            var go=new GameObject("DBPU volumetric charged strike");go.transform.SetParent(owner.transform,false);
            var effect=go.AddComponent<KiStrikeLimbVfx>();effect.owner=owner;
            effect.coating=KiCombatVfxAssets.Layer(go.transform,"Raymarched turbulent fist plasma",KiCombatVfxAssets.Limb,37);
            effect.trail=KiCombatVfxAssets.Layer(go.transform,"Raymarched swept plasma wake",KiCombatVfxAssets.Trail,36,false);
            effect.wake=new Mesh{name="DBPU physical strike path"};effect.wake.MarkDynamic();
            var uv=new Vector2[Samples*2];var triangles=new int[(Samples-1)*6];
            for(int i=0;i<Samples;i++)
            {
                float t=i/(float)(Samples-1);uv[i*2]=new Vector2(t,0);uv[i*2+1]=new Vector2(t,1);
                if(i==Samples-1)continue;int v=i*2,k=i*6;
                triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;
                triangles[k+3]=v+1;triangles[k+4]=v+3;triangles[k+5]=v+2;
            }
            effect.wake.vertices=effect.vertices;effect.wake.uv=uv;effect.wake.triangles=triangles;
            effect.trail.gameObject.AddComponent<MeshFilter>().sharedMesh=effect.wake;
            return effect;
        }
        public void Show(){showing=true;if(!gameObject.activeSelf)gameObject.SetActive(true);}
        public void Hide(){showing=false;if(coating)coating.enabled=false;}
        void LateUpdate()
        {
            if(!owner){Destroy(gameObject);return;}
            if(Global.main && Global.main.Paused)return;
            clock+=Time.deltaTime;
            bool alive=owner.Equipped && (owner.IsReady || owner.IsCharging || owner.IsCharged) && KiCombatMath.ValidStrikeLimb(owner.Limb,owner.Person);
            if(!alive)showing=false;
            Vector3 centre=KiCombatMath.StrikePoint(owner.Limb);centre.z=-.18f;
            transform.position=centre;transform.rotation=Quaternion.identity;KiCombatVfxAssets.WorldScale(transform,Vector3.one);
            var sprite=owner.Limb?owner.Limb.GetComponent<SpriteRenderer>():null;
            float limbSize=sprite?Mathf.Clamp(Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y),.25f,2):.45f;
            float charge=owner.Charge01,over=owner.Overcharge01;
            float radius=limbSize*(.24f+charge*.31f+over*.30f);
            float intensity=owner.IsCharging || owner.IsCharged?1:.25f;
            coating.enabled=showing;
            if(showing)
            {
                coating.transform.localScale=new Vector3(radius,radius,1);
                properties.Clear();properties.SetFloat("_Clock",clock);properties.SetFloat("_Age",clock);
                properties.SetFloat("_Charge",charge);properties.SetFloat("_Power",charge+over*2);
                properties.SetFloat("_Intensity",1+over);properties.SetFloat("_Opacity",intensity);
                properties.SetColor("_Tint",owner.EffectTint);coating.SetPropertyBlock(properties);
                if(count>0 && Vector3.Distance(points[0],centre)>Mathf.Max(8,radius*30))count=0;
                if(count==0 || Vector3.Distance(points[0],centre)>.012f)
                {
                    for(int i=Samples-1;i>0;i--){points[i]=points[i-1];born[i]=born[i-1];}
                    points[0]=centre;born[0]=clock;count=Mathf.Min(Samples,count+1);
                }
            }
            while(count>0 && clock-born[count-1]>.23f)count--;
            trail.enabled=count>1;
            if(trail.enabled)
            {
                for(int i=0;i<Samples;i++)
                {
                    int n=Mathf.Min(i,count-1),before=Mathf.Max(0,n-1),after=Mathf.Min(count-1,n+1);
                    Vector3 tangent=(points[before]-points[after]).normalized,normal=new Vector3(-tangent.y,tangent.x,0);
                    float fade=i<count?Mathf.Clamp01(1-(clock-born[n])/.23f):0;
                    Vector3 at=transform.InverseTransformPoint(points[n]);float width=radius*(.4f+charge*.28f)*fade;
                    Vector3 localOffset=transform.InverseTransformVector(normal*width);
                    vertices[i*2]=at-localOffset;vertices[i*2+1]=at+localOffset;
                    colors[i*2]=colors[i*2+1]=new Color(1,1,1,fade*fade*intensity);
                }
                wake.vertices=vertices;wake.colors=colors;wake.RecalculateBounds();
                properties.SetFloat("_Clock",clock);properties.SetFloat("_Opacity",.9f);trail.SetPropertyBlock(properties);
            }
            if(!showing && count==0)gameObject.SetActive(false);
        }
        void OnDestroy(){if(wake)Destroy(wake);}
    }

    [DefaultExecutionOrder(290)]
    public sealed class KiStrikeImpactVfx : MonoBehaviour
    {
        static int active;
        MeshRenderer volume;
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        float age,duration,size;int tier;Vector2 direction;Color tint;bool internalBurst;
        public static KiStrikeImpactVfx Spawn(Vector2 point,int tier,bool penetrated,bool internalBurst,Vector2 direction,Color? effectTint=null)
        {
            if(active>=24 || !KiCombatVfxAssets.Ready)return null;
            var go=new GameObject(internalBurst?"DBPU internal pressure plasma":"DBPU volumetric ki collision and spacetime shock");
            go.transform.position=new Vector3(point.x,point.y,-.20f);
            var effect=go.AddComponent<KiStrikeImpactVfx>();effect.direction=direction.sqrMagnitude>.001f?direction.normalized:Vector2.right;
            effect.tier=Mathf.Clamp(tier,0,2);effect.internalBurst=internalBurst;
            effect.duration=internalBurst?.38f:tier>=2?1.65f:tier==1?1.25f:.67f;
            effect.size=(internalBurst?.48f:tier>=2?5.3f:tier==1?3.8f:1.65f)*(penetrated?1:.58f);
            effect.tint=effectTint ?? new Color(.8f,.92f,1);
            effect.volume=KiCombatVfxAssets.Layer(go.transform,"Integrated electric pressure volume",KiCombatVfxAssets.Impact,40);
            effect.volume.transform.localScale=new Vector3(effect.size,effect.size,1);
            active++;effect.Draw();
            if(!internalBurst)
                KiSpaceRefraction.Pulse(point,effect.size*1.85f,tier>=2?1:tier==1?.85f:.50f,effect.duration+.42f,effect.direction);
            return effect;
        }
        void LateUpdate()
        {
            if(Global.main && Global.main.Paused)return;
            age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}Draw();
        }
        void Draw()
        {
            properties.Clear();properties.SetFloat("_Clock",age);properties.SetFloat("_Age",age/duration*1.38f);
            properties.SetFloat("_Power",internalBurst?.1f:.65f+tier*.9f);properties.SetFloat("_Charge",1);
            properties.SetFloat("_Intensity",internalBurst?.5f:1+tier*.4f);properties.SetFloat("_Opacity",internalBurst?.64f:1);
            properties.SetColor("_Tint",tint);properties.SetVector("_HitDirection",new Vector4(direction.x,direction.y,0,0));
            volume.SetPropertyBlock(properties);
        }
        void OnDestroy(){active=Mathf.Max(0,active-1);}
    }

    // One post-process on the gameplay camera. It samples the actual scene
    // colour once, so every shock front refracts scenery and fighters together.
    [RequireComponent(typeof(Camera)),DefaultExecutionOrder(310)]
    public sealed class KiSpaceRefraction : MonoBehaviour
    {
        const int Capacity=12;
        struct Wave {public Vector2 point;public float age,duration,radius,strength,delay,phase;public bool lens;}
        readonly Wave[] waves=new Wave[Capacity];
        readonly Vector4[] data=new Vector4[Capacity],shape=new Vector4[Capacity];
        static KiSpaceRefraction current;
        Camera view;Material material;int count;
        public bool BypassForQa;
        public static bool Ready {get {return KiCombatVfxAssets.Ready && KiCombatVfxAssets.Refraction.shader.isSupported;}}
        public static int ActiveWaveCount {get {return current?current.count:0;}}
        public float OldestWaveAge {get {return count>0?waves[0].age:0;}}

        public static void Pulse(Vector2 point,float radius,float strength,float duration,Vector2 direction)
        {
            if(!Ready || !Finite(point.x) || !Finite(point.y) || !Finite(radius) || !Finite(strength) || !Finite(duration))return;
            var camera=Global.main?Global.main.camera:Camera.main;if(!camera)return;
            if(!current || current.view!=camera)
            {
                current=camera.GetComponent<KiSpaceRefraction>();
                if(!current)current=camera.gameObject.AddComponent<KiSpaceRefraction>();
            }
            radius=Mathf.Clamp(radius,.15f,40);strength=Mathf.Clamp01(strength);duration=Mathf.Clamp(duration,.25f,2.8f);
            if(strength<=0)return;
            float phase=Mathf.Atan2(direction.y,direction.x);
            current.Add(point,radius,strength,duration,0,phase,true);
            current.Add(point,radius*.90f,strength*.42f,duration*.88f,.065f,phase+1.2f,false);
            if(strength>.65f)current.Add(point,radius*.80f,strength*.21f,duration*.75f,.13f,phase+2.4f,false);
        }
        static bool Finite(float n){return !float.IsNaN(n) && !float.IsInfinity(n);}
        void Awake()
        {
            view=GetComponent<Camera>();
            if(Ready)material=new Material(KiCombatVfxAssets.Refraction){name="DBPU scene refraction instance",hideFlags=HideFlags.HideAndDontSave};
        }
        void Add(Vector2 point,float radius,float strength,float duration,float delay,float phase,bool lens)
        {
            if(count==Capacity)
            {
                int weakest=0;float score=float.MaxValue;
                for(int i=0;i<count;i++){float s=waves[i].strength*(1-Mathf.Clamp01(waves[i].age/waves[i].duration));if(s<score){score=s;weakest=i;}}
                for(int i=weakest;i<count-1;i++)waves[i]=waves[i+1];count--;
            }
            waves[count++]=new Wave{point=point,radius=radius,strength=strength,duration=duration,delay=delay,phase=phase,lens=lens};
        }
        void LateUpdate()
        {
            if(Global.main && Global.main.Paused)return;
            for(int i=count-1;i>=0;i--)
            {
                waves[i].age+=Time.deltaTime;
                if(waves[i].age<waves[i].duration+waves[i].delay)continue;
                for(int j=i;j<count-1;j++)waves[j]=waves[j+1];count--;
            }
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!material || count==0 || BypassForQa){Graphics.Blit(source,destination);return;}
            float worldHeight=view.orthographic?view.orthographicSize*2:2*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)*Mathf.Abs(view.transform.position.z);
            worldHeight=Mathf.Max(.01f,worldHeight);int visible=0;
            for(int i=0;i<count;i++)
            {
                Wave w=waves[i];float elapsed=w.age-w.delay;if(elapsed<0)continue;
                var centre=view.WorldToViewportPoint(new Vector3(w.point.x,w.point.y,0));if(centre.z<=0)continue;
                float t=Mathf.Clamp01(elapsed/w.duration),fade=Mathf.Pow(1-t,1.2f);
                float radius=w.radius/worldHeight*(.035f+.965f*Mathf.Pow(t,.66f));
                if(centre.x+radius*source.height/source.width<0 || centre.x-radius*source.height/source.width>1 || centre.y+radius<0 || centre.y-radius>1)continue;
                float amplitude=Mathf.Min(.032f,w.radius/worldHeight*.085f*w.strength)*fade;
                data[visible]=new Vector4(centre.x,centre.y,radius,amplitude);
                shape[visible]=new Vector4(Mathf.Max(.003f,w.radius/worldHeight*(.10f-.045f*t)),w.lens?w.strength*fade*.65f:0,w.phase+elapsed*2,.20f*w.strength);
                visible++;
            }
            if(visible==0){Graphics.Blit(source,destination);return;}
            material.SetInt("_WaveCount",visible);material.SetVectorArray("_WaveData",data);material.SetVectorArray("_WaveShape",shape);
            Graphics.Blit(source,destination,material);
        }
        void OnDisable(){count=0;}
        void OnDestroy(){if(material)Destroy(material);if(current==this)current=null;}
    }
}
