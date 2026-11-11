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
        Color tint;
        float pixel=.01f;
        public void Initialize(KiShield shield)
        {
            owner=shield;tint=shield.Tint;radius=shield.Radius;
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
        public void Collapse(bool shattered){if(collapse>=0)return;broken=shattered;collapse=0;owner=null;}
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
            if(owner){tint=Color.Lerp(tint,owner.Tint,1-Mathf.Exp(-10*dt));radius=owner.Radius;integrity=owner.Integrity01;}
            float fade=collapse<0?Mathf.SmoothStep(0,1,clock/.18f):Mathf.Clamp01(1-collapse/.6f);
            float size=collapse<0?Mathf.Lerp(.88f,1,Mathf.SmoothStep(0,1,clock/.18f)):broken?1+collapse*.55f:1-collapse*.20f;
            hits.RemoveAll(h=>clock-h.Seen>1);
            rear.Clear();front.Clear();details.Clear();impacts.Clear();
            Hemisphere(rear,false,size,fade);Hemisphere(front,true,size,fade);
            Grid(size,fade);Rim(size,fade);Stars(size,fade);
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
                Color color=Color.Lerp(tint,Color.white,rim*.65f);
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
            Color color=C(Color.Lerp(tint,Color.white,.72f),.20f*fade);float width=Mathf.Max(radius*.0028f,pixel*1.1f);
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
                Line(details,n*radius*size,m*radius*size,radius*.0048f,C(Color.Lerp(tint,Color.white,.8f),.75f*fade,1.6f));
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
                Diamond(details,at,radius*(.01f+Hash(i+9)*.018f),C(Color.Lerp(tint,Color.white,.75f),alpha));
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
