using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Authored hemispheres and curved surface geometry, animated continuously.
    // No video frames, sprite sequences, or per-hit texture allocations.
    public sealed class KiShieldVisual:MonoBehaviour
    {
        sealed class Layer
        {
            public Mesh Mesh;public MeshRenderer Renderer;
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Color> Colors=new List<Color>();
            public readonly List<int> Indices=new List<int>();
            public void Clear(){Vertices.Clear();Colors.Clear();Indices.Clear();}
            public void Upload(float radius){Mesh.Clear();Mesh.SetVertices(Vertices);Mesh.SetColors(Colors);Mesh.SetTriangles(Indices,0);Mesh.bounds=new Bounds(Vector3.zero,Vector3.one*(radius*3+3));}
        }
        sealed class Impact {public int Id;public Vector2 Normal;public float Began,Seen,Strength;}
        static Material material;
        KiShield owner;Layer rear,front,details,impacts;
        readonly List<Impact> hits=new List<Impact>();
        float clock,collapse=-1,radius=3.4f,integrity=1;bool broken;
        Color tint,hot=Color.white,deep=Color.black;
        float shiftAge=10,raiseAge=0,perfectAge=10;
        readonly float[] crackAngle=new float[8],crackSeed=new float[8];
        float pixel=.01f;
        public void Initialize(KiShield shield)
        {
            owner=shield;tint=shield.Tint;hot=shield.TintHot;deep=shield.TintDeep;radius=shield.Radius;
            for(int i=0;i<crackAngle.Length;i++){crackAngle[i]=(i*45f+UnityEngine.Random.Range(-18f,18f))*Mathf.Deg2Rad;crackSeed[i]=UnityEngine.Random.value*100f;}
            if(!material){material=new Material(Shader.Find("Sprites/Default")){name="DBPU shared shield radiance"};material.mainTexture=Texture2D.whiteTexture;}
            rear=Make("Shield hemisphere Rear","Background",-3);front=Make("Shield hemisphere Front","Bubbles",20);
            details=Make("Shield meridians and integrity rim","Bubbles",21);impacts=Make("Shield impact refraction and arcs","Bubbles",25);
            Tick(0);
        }
        Layer Make(string name,string sorting,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var mesh=new Mesh{name=name};mesh.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingLayerName=sorting;renderer.sortingOrder=order;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return new Layer{Mesh=mesh,Renderer=renderer};
        }
        public void Hit(Vector2 point,float strength,int source)
        {
            Vector2 normal=(point-(Vector2)transform.position).normalized;if(normal.sqrMagnitude<.5f)normal=Vector2.left;
            Impact hit=hits.Find(h=>h.Id==source);
            if(hit==null){if(hits.Count>=6)hits.RemoveAt(0);hit=new Impact{Id=source,Normal=normal,Began=clock};hits.Add(hit);}
            hit.Normal=normal;hit.Seen=clock;hit.Strength=Mathf.Max(.2f,strength);
        }
        public void Collapse(bool shattered)
        {
            if(collapse>=0)return;broken=shattered;collapse=0;owner=null;
            if(!shattered)return;
            // Glass-like shell fragments in the barrier's own colour.
            var shards=DbpuParticles.Create("DBPU Ki Shield shards",DbpuFx.Shard,120,26);shards.Release();
            Vector2 c=transform.position;
            for(int i=0;i<70;i++)
            {
                Vector2 dir=DbpuFx.Rotate(Vector2.right,UnityEngine.Random.value*6.283f);
                float s=UnityEngine.Random.Range(.12f,.36f)*Mathf.Clamp(radius/2.5f,.6f,2.2f);
                Color col=UnityEngine.Random.value<.5f?Color.Lerp(hot,Color.white,.5f):tint;
                shards.Emit(c+dir*radius*UnityEngine.Random.Range(.85f,1.02f),dir*UnityEngine.Random.Range(2.5f,8f),UnityEngine.Random.Range(.7f,1.3f),s,DbpuFx.A(col,.9f),DbpuFx.A(tint,0f),1.4f,4f,UnityEngine.Random.Range(-480f,480f));
            }
            var flash=DbpuParticles.Create("DBPU Ki Shield break flash",DbpuFx.Glow,4,27);flash.Release();
            flash.Emit(c,Vector2.zero,.4f,radius*1.6f,DbpuFx.A(Color.Lerp(hot,Color.white,.6f),.75f),DbpuFx.A(tint,0f),0,0,0,false,radius*2.6f);
        }
        // Bright sweep when the fighter's form / aura colour changes while shielded.
        public void FormShift(){shiftAge=0;}
        public void Perfect(){perfectAge=0;}
        void Update()
        {
            if(collapse<0)return;if(Global.main && Global.main.Paused)return;
            collapse+=Time.deltaTime;Tick(Time.deltaTime);if(collapse>.65f)Destroy(gameObject);
        }
        public void Tick(float dt)
        {
            if(rear==null)return;clock+=dt;
            var camera=Global.main?Global.main.camera:Camera.main;
            if(camera && camera.orthographic)pixel=Mathf.Min(radius*.02f,2*camera.orthographicSize/Mathf.Max(1,Screen.height));
            shiftAge+=dt;raiseAge+=dt;perfectAge+=dt;
            if(owner)
            {
                float blend=1-Mathf.Exp(-(shiftAge<.8f?5f:10f)*dt);
                tint=Color.Lerp(tint,owner.Tint,blend);hot=Color.Lerp(hot,owner.TintHot,blend);deep=Color.Lerp(deep,owner.TintDeep,blend);
                radius=owner.Radius;integrity=owner.Integrity01;
            }
            float fade=collapse<0?Mathf.SmoothStep(0,1,clock/.18f):Mathf.Clamp01(1-collapse/.6f);
            float size=collapse<0?Mathf.Lerp(.88f,1,Mathf.SmoothStep(0,1,clock/.18f)):broken?1+collapse*.55f:1-collapse*.20f;
            hits.RemoveAll(h=>clock-h.Seen>1);
            rear.Clear();front.Clear();details.Clear();impacts.Clear();
            Hemisphere(rear,false,size,fade);Hemisphere(front,true,size,fade);
            float flicker=collapse<0 && integrity<.25f?(.65f+.35f*Mathf.PerlinNoise(clock*14f,.3f)):1f;
            Grid(size,fade*flicker);Rim(size,fade*flicker);Stars(size,fade);Arcs(size,fade*flicker);
            if(collapse<0 && integrity<.62f)Cracks(size,fade,Mathf.Clamp01((.62f-integrity)/.62f));
            Pulses(size,fade);
            foreach(var hit in hits)ImpactGeometry(hit,size,fade);
            if(broken && collapse>=0)Shatter(size,fade);
            rear.Upload(radius);front.Upload(radius);details.Upload(radius);impacts.Upload(radius);
        }
        Color C(Color color,float alpha,float brightness=1){return new Color(color.r*brightness,color.g*brightness,color.b*brightness,alpha);}
        float Deformation(Vector2 normal)
        {
            float value=0;
            foreach(var hit in hits)
            {
                float angle=Mathf.Acos(Mathf.Clamp(Vector2.Dot(normal,hit.Normal),-1,1));
                value+=Mathf.Sin(angle*17-(clock-hit.Began)*16)*Mathf.Exp(-angle*2.5f)*.012f*Mathf.Exp(-(clock-hit.Seen)*4);
            }
            return value;
        }
        void Hemisphere(Layer layer,bool forward,float size,float fade)
        {
            const int rows=22,columns=96;
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
            {
                float latitude=row*Mathf.PI/(rows*2),angle=col*Mathf.PI*2/columns;
                var normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float radial=Mathf.Sin(latitude),depth=Mathf.Cos(latitude);
                float r=radius*size*(1+Deformation(normal)*radial*radial);
                layer.Vertices.Add(new Vector3(normal.x*radial*r,normal.y*radial*r,(forward?-1:1)*depth*Mathf.Min(radius*.18f,.8f)));
                float rim=Mathf.Pow(radial,8),light=.72f+.28f*Mathf.Clamp01(normal.y*.5f+.5f);
                Color color=Color.Lerp(Color.Lerp(Color.Lerp(deep,tint,.6f),tint,radial),hot,rim*.75f);
                float alpha=(forward?.024f:.09f)+(forward?.12f:.16f)*rim;
                layer.Colors.Add(C(color,alpha*fade,light*1.15f));
                if(row>0 && col>0){int n=row*(columns+1)+col;layer.Indices.Add(n);layer.Indices.Add(n-1);layer.Indices.Add(n-columns-2);layer.Indices.Add(n);layer.Indices.Add(n-columns-2);layer.Indices.Add(n-columns-1);}
            }
        }
        void Line(Layer layer,Vector3 a,Vector3 b,float width,Color color)
        {
            Vector2 dir=(Vector2)(b-a);if(dir.sqrMagnitude<.0000001f)return;
            Vector3 normal=new Vector3(-dir.y,dir.x).normalized;float half=Mathf.Max(width,pixel*1.1f)*.5f,edge=pixel*.7f;
            Color clear=color;clear.a=0;int start=layer.Vertices.Count;
            for(int end=0;end<2;end++)
            {
                Vector3 point=end==0?a:b;
                layer.Vertices.Add(point-normal*(half+edge));layer.Colors.Add(clear);
                layer.Vertices.Add(point-normal*half);layer.Colors.Add(color);
                layer.Vertices.Add(point+normal*half);layer.Colors.Add(color);
                layer.Vertices.Add(point+normal*(half+edge));layer.Colors.Add(clear);
            }
            for(int strip=0;strip<3;strip++){int n=start+strip;layer.Indices.Add(n);layer.Indices.Add(n+1);layer.Indices.Add(n+5);layer.Indices.Add(n);layer.Indices.Add(n+5);layer.Indices.Add(n+4);}
        }
        void Grid(float size,float fade)
        {
            Color color=C(Color.Lerp(tint,hot,.72f),.22f*fade);float width=Mathf.Max(radius*.0028f,pixel*1.1f);
            for(int meridian=0;meridian<7;meridian++)
            {
                float az=meridian*Mathf.PI/7+clock*.075f;Vector3 before=Vector3.zero;
                for(int i=0;i<=64;i++)
                {
                    float p=i*Mathf.PI/64;Vector3 at=new Vector3(Mathf.Sin(p)*Mathf.Cos(az),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(az)*.18f)*radius*size;
                    if(i>0)Line(details,before,at,width,color);before=at;
                }
            }
            for(int latitudeIndex=-1;latitudeIndex<=1;latitudeIndex++)
            {
                float latitude=latitudeIndex*.55f;
                float r=Mathf.Sqrt(1-latitude*latitude);Vector3 before=Vector3.zero;
                for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;Vector3 at=new Vector3(Mathf.Cos(a)*r,latitude+Mathf.Sin(a)*r*.15f,Mathf.Sin(a)*r*.18f)*radius*size;if(i>0)Line(details,before,at,width,color);before=at;}
            }
        }
        void Rim(float size,float fade)
        {
            for(int i=0;i<192;i++)
            {
                float a=i*Mathf.PI/96,b=(i+1)*Mathf.PI/96;Vector3 n=new Vector3(Mathf.Cos(a),Mathf.Sin(a)),m=new Vector3(Mathf.Cos(b),Mathf.Sin(b));
                Line(details,n*radius*size,m*radius*size,radius*.035f,C(tint,.09f*fade,1.3f));
                Line(details,n*radius*size,m*radius*size,radius*.015f,C(tint,.19f*fade,1.3f));
                Line(details,n*radius*size,m*radius*size,radius*.0048f,C(Color.Lerp(hot,Color.white,.45f),.75f*fade,1.6f));
                float turn=Mathf.Repeat(i/192f+clock*.015f,1);
                if(turn<integrity && i%64<55)Line(details,n*radius*size*1.045f,m*radius*size*1.045f,radius*.012f,C(Color.Lerp(tint,Color.white,.25f),.65f*fade));
            }
        }
        static float Hash(int n){return Mathf.Repeat(Mathf.Sin(n*127.1f+91.7f)*43758.54f,1);}
        void Diamond(Layer layer,Vector3 position,float width,Color color)
        {
            int n=layer.Vertices.Count;layer.Vertices.Add(position+Vector3.up*width);layer.Vertices.Add(position+Vector3.right*width*.5f);layer.Vertices.Add(position-Vector3.up*width);layer.Vertices.Add(position-Vector3.right*width*.5f);
            for(int i=0;i<4;i++)layer.Colors.Add(color);layer.Indices.Add(n);layer.Indices.Add(n+1);layer.Indices.Add(n+2);layer.Indices.Add(n);layer.Indices.Add(n+2);layer.Indices.Add(n+3);
        }
        void Stars(float size,float fade)
        {
            for(int i=0;i<22;i++)
            {
                float life=Mathf.Repeat(Hash(i+11)+clock*(.08f+Hash(i+53)*.08f),1),angle=Hash(i+31)*Mathf.PI*2+clock*.05f;
                float r=.35f+Hash(i+2)*.55f;Vector2 xy=Vector2.ClampMagnitude(new Vector2(Mathf.Cos(angle)*r,(life*2-1)*.8f),.90f);Vector3 at=new Vector3(xy.x,xy.y,-.25f)*radius*size;
                float alpha=Mathf.Pow(Mathf.Sin(life*Mathf.PI),2)*(.15f+Hash(i)*.4f)*fade;
                Diamond(details,at,radius*(.01f+Hash(i+9)*.018f),C(Color.Lerp(hot,Color.white,.5f),alpha));
            }
        }
        void ImpactGeometry(Impact hit,float size,float fade)
        {
            float alpha=Mathf.Exp(-(clock-hit.Seen)*5)*fade;Vector2 normal=hit.Normal,tangent=new Vector2(-normal.y,normal.x);
            Vector3 point=(Vector3)(normal*radius*size);point.z=-.3f;
            GlowDisc(point,radius*.24f,C(tint,alpha*.22f,1.5f));
            GlowDisc(point,radius*.11f,C(Color.white,alpha*.95f,3));
            for(int ring=0;ring<3;ring++)
            {
                float life=Mathf.Repeat((clock-hit.Began)*1.25f+ring/3f,1);Vector3 before=Vector3.zero;
                for(int i=0;i<=64;i++)
                {
                    float a=i*Mathf.PI/32;Vector3 at=point+(Vector3)(normal*Mathf.Cos(a)*radius*life*.16f+tangent*Mathf.Sin(a)*radius*life*.33f);
                    if(i>0)Line(impacts,before,at,radius*.003f,C(Color.Lerp(tint,Color.white,.5f),alpha*(1-life)*.6f));before=at;
                }
            }
            float theta=Mathf.Atan2(normal.y,normal.x);
            for(int branch=0;branch<8;branch++)
            {
                float side=branch%2==0?1:-1,reach=.35f+Hash(branch+hit.Id%37)*1.3f;Vector3 previous=point;
                for(int n=1;n<=18;n++)
                {
                    float s=n/18f,angle=theta+side*s*reach;
                    float epoch=clock*18,phase=epoch-Mathf.Floor(epoch);
                    float noise=Mathf.Lerp(Hash(branch*71+n*13+Mathf.FloorToInt(epoch)*3),Hash(branch*71+n*13+(Mathf.FloorToInt(epoch)+1)*3),Mathf.SmoothStep(0,1,phase))-.5f;
                    float r=radius*size*(1+noise*.045f*s);Vector3 at=new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,-.35f);
                    if(branch>=4)at=Vector3.Lerp(at,point+(Vector3)(-normal*radius*s*.6f+tangent*side*radius*s*.6f),.55f);
                    float visibility=alpha*(1-s*.7f);
                    Line(impacts,previous,at,radius*.015f,C(tint,visibility*.22f,1.5f));Line(impacts,previous,at,Mathf.Max(radius*.0035f,pixel*1.5f),C(Color.white,visibility*.92f,1.8f));previous=at;
                }
            }
        }
        void Arc(Layer layer,float r,float start,float span,float width,Color color)
        {
            int n=Mathf.Max(2,Mathf.CeilToInt(span/(Mathf.PI/48)));
            for(int i=0;i<n;i++)
            {
                float a=start+span*i/n,b=start+span*(i+1)/n;
                Line(layer,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-.2f),new Vector3(Mathf.Cos(b)*r,Mathf.Sin(b)*r,-.2f),width,color);
            }
        }
        // Rotating HUD arcs outside the shell; the long arc shrinks with integrity.
        void Arcs(float size,float fade)
        {
            float r=radius*size,spin=clock*.32f;
            Arc(details,r*1.085f,spin+.4f,(.55f+1.25f*integrity),radius*.034f,C(tint,.42f*fade,1.2f));
            Arc(details,r*1.085f,spin+.4f,(.55f+1.25f*integrity),radius*.009f,C(Color.Lerp(hot,Color.white,.4f),.8f*fade,1.4f));
            Arc(details,r*1.13f,-spin*1.4f+Mathf.PI,.42f,radius*.012f,C(hot,.55f*fade));
            Arc(details,r*1.13f,-spin*1.4f+Mathf.PI+.6f,.12f,radius*.012f,C(hot,.55f*fade));
            for(int t=0;t<12;t++)
            {
                float a=spin*.5f+t*Mathf.PI/6;Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));
                Line(details,d*r*1.035f,d*r*1.06f,radius*.006f,C(hot,.35f*fade));
            }
        }
        void Cracks(float size,float fade,float damage)
        {
            int visible=Mathf.CeilToInt(damage*crackAngle.Length);
            for(int i=0;i<visible && i<crackAngle.Length;i++)
            {
                float reach=Mathf.Clamp01(damage*1.6f-i*.12f)*.55f;if(reach<=.02f)continue;
                Vector3 before=new Vector3(Mathf.Cos(crackAngle[i]),Mathf.Sin(crackAngle[i]))*radius*size;
                for(int k=1;k<=6;k++)
                {
                    float t=k/6f,jag=(Mathf.PerlinNoise(crackSeed[i],k*.8f)-.5f)*.5f;
                    float ang=crackAngle[i]+jag*t*.6f;Vector3 at=new Vector3(Mathf.Cos(ang),Mathf.Sin(ang))*radius*size*(1-reach*t);at.z=-.32f;
                    Line(impacts,before,at,Mathf.Max(radius*.006f,pixel*1.4f),C(Color.Lerp(hot,Color.white,.7f),.85f*fade*(1-t*.4f),1.4f));
                    if(k==3)
                    {
                        float side=Hash(i*7)>.5f?1:-1;Vector3 branch=at+new Vector3(Mathf.Cos(ang+side*1.2f),Mathf.Sin(ang+side*1.2f))*radius*reach*.25f;
                        Line(impacts,at,branch,Mathf.Max(radius*.004f,pixel*1.2f),C(Color.white,.6f*fade));
                    }
                    before=at;
                }
            }
        }
        void Pulses(float size,float fade)
        {
            if(raiseAge<.45f)
            {
                float t=raiseAge/.45f;Arc(impacts,radius*size*(1+t*.55f),0,Mathf.PI*2,radius*(.05f-t*.035f),C(Color.Lerp(hot,Color.white,.5f),(1-t)*.8f,1.5f));
                GlowDisc(new Vector3(0,0,-.3f),radius*size*(.9f+t*.4f),C(hot,(1-t)*.18f,1.4f));
            }
            if(shiftAge<.8f)
            {
                float t=shiftAge/.8f;Arc(impacts,radius*size*(.25f+t*.85f),0,Mathf.PI*2,radius*.03f,C(Color.Lerp(hot,Color.white,.4f),(1-t)*.9f*fade,1.6f));
                Arc(impacts,radius*size*1.005f,0,Mathf.PI*2,radius*.025f*(1-t),C(Color.white,(1-t)*.8f*fade,1.8f));
            }
            if(perfectAge<.35f)
            {
                float t=perfectAge/.35f;Arc(impacts,radius*size,0,Mathf.PI*2,radius*.06f*(1-t),C(Color.white,(1-t)*fade,2f));
                GlowDisc(new Vector3(0,0,-.3f),radius*size*1.1f,C(Color.white,(1-t)*.25f*fade,2f));
            }
        }
        void GlowDisc(Vector3 centre,float radius,Color color)
        {
            int n=impacts.Vertices.Count;impacts.Vertices.Add(centre);impacts.Colors.Add(color);Color clear=color;clear.a=0;
            for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;impacts.Vertices.Add(centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius);impacts.Colors.Add(clear);if(i>0){impacts.Indices.Add(n);impacts.Indices.Add(n+i);impacts.Indices.Add(n+i+1);}}
        }
        void Shatter(float size,float fade)
        {
            for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36;Vector3 p=new Vector3(Mathf.Cos(a),Mathf.Sin(a))*(radius*size+collapse*radius*Hash(i+3)*.3f);Diamond(impacts,p,radius*.025f*fade,C(Color.Lerp(tint,Color.white,.65f),fade*.8f));}
        }
        void OnDestroy(){foreach(var layer in new[]{rear,front,details,impacts})if(layer!=null && layer.Mesh)Destroy(layer.Mesh);}
    }
}
