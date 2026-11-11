using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DBPU.NarutoFX
{
    // Shared immutable GPU resources. No per-frame mesh or particle simulation on the CPU.
    public sealed class ChakraRig : IDisposable
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static Mesh quad,tube,coat,collar,motes,earMesh;
        readonly List<GameObject> owned=new List<GameObject>();
        readonly List<Renderer> all=new List<Renderer>();
        readonly List<SpriteRenderer> covers=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> source=new List<SpriteRenderer>();
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        readonly MeshRenderer[] flames=new MeshRenderer[3],tails=new MeshRenderer[3],orbs=new MeshRenderer[9];
        readonly MeshRenderer[] coats=new MeshRenderer[3];
        MeshRenderer dust,ears;Transform anchor,middle;SpriteRenderer hair;string form="Base";
        Color color,core;bool fox,gold,sageRobe;float fade,clock;
        public int TailCount {get;private set;}
        public int OrbCount {get;private set;}
        public bool RobeVisible {get{return sageRobe||form=="KCM2"||form=="KuramaSage"||form=="SixPathsCloak";}}
        public float Fade {get{return fade;}}
        public int RendererCount {get{return all.Count;}}
        public static void Prepare(Func<string,Shader> load)
        {
            if(materials.Count>0)return;
            foreach(string name in new[]{"Chakra","Tail","Mantle","Coat","Orb","Motes"})
            {
                var shader=load(name);if(!shader||!shader.isSupported)throw new InvalidOperationException("Naruto shader unavailable: "+name);
                materials.Add(name,new Material(shader){name="Naruto shared "+name,enableInstancing=true});
            }
            quad=Grid(1,1,false);tube=Grid(40,12,true);coat=MakeCoat();collar=MakeCollar();motes=MakeMotes();earMesh=MakeEars();
        }
        public ChakraRig(Transform root,Transform torso,Transform hips,SpriteRenderer[] body,SpriteRenderer hairRenderer)
        {
            middle=torso;hair=hairRenderer;var head=body[0];int order=head.sortingOrder;int layer=head.sortingLayerID;
            anchor=Obj("Naruto GPU chakra field",root).transform;
            for(int i=0;i<3;i++)
            {
                flames[i]=MeshObject("Flow layer "+i,anchor,quad,"Chakra",layer,order-8+i);
                flames[i].transform.localScale=new Vector3(i==1?1.2f:1.5f,i==1?2.55f:2.75f,1);
                flames[i].transform.localPosition=new Vector3((i-1)*.035f,0,.1f+i*.02f);
                if(i==2){flames[i].sortingOrder=order+4;}
                tails[i]=MeshObject("Fox tail "+(i+1),hips,tube,"Tail",layer,order-5-i);
                tails[i].transform.localPosition=new Vector3(-.08f,-.03f,.06f);
                tails[i].transform.localRotation=Quaternion.Euler(0,0,(i-1)*33);
            }
            dust=MeshObject("Rising chakra grains GPU",anchor,motes,"Motes",layer,order+5);
            for(int i=0;i<body.Length;i++)
            {
                var go=Obj("Chakra mantle",body[i].transform);var sr=go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial=materials["Mantle"];sr.sortingLayerID=body[i].sortingLayerID;sr.sortingOrder=body[i].sortingOrder+1;
                sr.sprite=body[i].sprite;covers.Add(sr);source.Add(body[i]);all.Add(sr);
            }
            coats[0]=MeshObject("Flowing open coat back",body[1].transform,coat,"Coat",layer,order-3);
            coats[0].transform.localPosition=new Vector3(-.04f,.12f,.03f);
            coats[1]=MeshObject("Open coat front hem",body[1].transform,coat,"Coat",layer,order-1);
            coats[1].transform.localPosition=new Vector3(.055f,.12f,0);
            coats[1].transform.localScale=new Vector3(-.36f,.90f,1);
            coats[2]=MeshObject("Raised cloak collar",body[1].transform,collar,"Coat",layer,order-1);
            coats[2].transform.localPosition=new Vector3(0,.15f,0);
            ears=MeshObject("Fox chakra ears",body[0].transform,earMesh,"Coat",layer,order-3);
            for(int i=0;i<9;i++){orbs[i]=MeshObject("Truth Seeking Orb "+(i+1),anchor,quad,"Orb",layer,order-2);orbs[i].transform.localScale=Vector3.one*.13f;}
            SetForm("Base");
        }
        public void SetForm(string id)
        {
            form=id??"Base";TailCount=form=="Fox1"?1:form=="Fox2"?2:form=="Fox3"?3:0;OrbCount=form=="SixPathsCloak"?9:0;
            fox=form=="FoxLeak"||TailCount>0;sageRobe=form=="SageRobe";
            gold=form=="KCM1"||form=="KCM2"||form=="KuramaSage"||form=="SixPathsCloak";
            bool sage=form=="Sage"||sageRobe;
            color=fox?new Color(.98f,.045f,.015f):sage?new Color(.12f,.66f,.25f):new Color(1,.57f,.025f);
            core=fox?new Color(1,.49f,.12f):sage?new Color(.7f,1,.55f):new Color(1,.94f,.43f);
            for(int i=0;i<3;i++){tails[i].enabled=i<TailCount;coats[i].enabled=RobeVisible;}
            coats[2].enabled=RobeVisible&&!sageRobe;ears.enabled=TailCount>0;
            for(int i=0;i<9;i++)orbs[i].enabled=i<OrbCount;
            for(int i=0;i<covers.Count;i++)covers[i].enabled=TailCount>0||(sageRobe&&(i==10||i==12));
            foreach(var r in flames)r.enabled=form!="Base";dust.enabled=form!="Base";
            if(hair)hair.color=gold?new Color(1f,.96f,.62f):Color.white;
            fade=0;clock=0;
        }
        public void Tick(float dt,bool auraVisible,bool effectsEnabled,float charge)
        {
            if(!middle)return;clock+=dt;
            fade=Mathf.MoveTowards(fade,auraVisible&&effectsEnabled&&form!="Base"?1:0,dt*4);
            float scale=Mathf.Abs(middle.lossyScale.y);
            float floorY=source[6]&&source[9]?Mathf.Min(source[6].bounds.min.y,source[9].bounds.min.y):middle.position.y-1.25f*scale;
            anchor.position=new Vector3(middle.position.x,floorY-.035f*scale,middle.position.z);
            anchor.rotation=Quaternion.identity;
            float sign=anchor.parent.lossyScale.x<0?-1:1;
            anchor.localScale=new Vector3(sign*(1+charge*.09f),1+charge*.11f,1);
            for(int i=0;i<3;i++)
            {
                Set(flames[i],color,core,fade,charge,i*3.71f,i==2?1:0,0);
                Set(tails[i],color,core,effectsEnabled?Mathf.Min(clock*4,1):0,charge,i*2.2f,0,0);
                block.SetFloat("_Length",1.15f+i*.18f);tails[i].SetPropertyBlock(block);
                Color fabric=sageRobe?new Color(.64f,.045f,.055f):new Color(1,.81f,.22f);
                if(i==2)fabric=new Color(.018f,.021f,.031f);
                Set(coats[i],fabric,new Color(.018f,.021f,.031f),Mathf.Min(clock*5,1),charge,i*1.8f,0,gold?1:0);
                if(i<2)coats[i].transform.localScale=new Vector3(i==0?1:-.36f,(form=="SixPathsCloak"?.64f:1)*(i==0?1:.90f),1);
            }
            Set(dust,core,core,fade,charge,0,0,0);
            Set(ears,color,core,effectsEnabled?.65f:0,charge,0,0,0);
            for(int i=0;i<covers.Count;i++)
            {
                if(!source[i]){covers[i].enabled=false;continue;}
                covers[i].sprite=source[i].sprite;covers[i].flipX=source[i].flipX;covers[i].flipY=source[i].flipY;
                if(!covers[i].enabled)continue;
                Set(covers[i],sageRobe?new Color(.65f,.045f,.045f):color,core,sageRobe?1:(effectsEnabled?.8f:0),charge,0,0,0);
                block.SetFloat("_Kind",sageRobe?1:0);covers[i].SetPropertyBlock(block);
                block.SetTexture("_MainTex",source[i].sprite.texture);covers[i].SetPropertyBlock(block);
            }
            for(int i=0;i<OrbCount;i++)
            {
                float a=i*Mathf.PI*2/9+Time.time*.22f;
                orbs[i].transform.localPosition=new Vector3(-.20f+Mathf.Cos(a)*.52f,1.30f+Mathf.Sin(a)*.57f,.1f);
                Set(orbs[i],Color.black,Color.black,Mathf.Min(clock*3,1),0,0,0,0);
            }
        }
        void Set(Renderer r,Color tint,Color hot,float opacity,float charge,float seed,float front,float goldValue)
        {
            block.Clear();block.SetColor("_Color",tint);block.SetColor("_Core",hot);block.SetFloat("_Fade",opacity);
            block.SetFloat("_Charge",charge);block.SetFloat("_Seed",seed);block.SetFloat("_Front",front);block.SetFloat("_Gold",goldValue);r.SetPropertyBlock(block);
        }
        GameObject Obj(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=parent.gameObject.layer;owned.Add(go);return go;}
        MeshRenderer MeshObject(string name,Transform parent,Mesh mesh,string mat,int layer,int order)
        {
            var go=Obj(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=materials[mat];r.sortingLayerID=layer;r.sortingOrder=order;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;all.Add(r);return r;
        }
        public void Dispose(){foreach(var go in owned)if(go)UnityEngine.Object.Destroy(go);owned.Clear();if(hair)hair.color=Color.white;}
        static Mesh Grid(int nx,int ny,bool isTube)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){float u=x/(float)nx,w=y/(float)ny;v.Add(new Vector3(u-.5f,w,0));uv.Add(new Vector2(u,w));}
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int a=y*(nx+1)+x,b=a+nx+1;tr.AddRange(isTube?new[]{a,a+1,b,a+1,b+1,b}:new[]{a,b,a+1,a+1,b,b+1});}
            var m=new Mesh{name=isTube?"Shared chakra tail surface":"Shared chakra plane"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.bounds=new Bounds(Vector3.zero,Vector3.one*8);return m;
        }
        static Mesh MakeCoat()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();int ny=24,nx=6;
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){float t=y/(float)ny,u=x/(float)nx;float width=.19f+t*.21f;v.Add(new Vector3(-.10f-t*t*.19f+(u-.5f)*width,-t*1.12f,.01f+Mathf.Sin(u*Mathf.PI)*.03f));uv.Add(new Vector2(u,t));}
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int a=y*(nx+1)+x,b=a+nx+1;tr.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var m=new Mesh{name="Shared open cloak"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.bounds=new Bounds(Vector3.zero,Vector3.one*4);return m;
        }
        static Mesh MakeCollar()
        {
            var m=new Mesh{name="Shared high cloak collar"};m.vertices=new[]{new Vector3(-.19f,-.12f),new Vector3(-.25f,.16f),new Vector3(-.02f,.03f),new Vector3(.11f,-.13f),new Vector3(.15f,.05f),new Vector3(.02f,.01f)};
            m.uv=new[]{new Vector2(0,1),new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)};m.triangles=new[]{0,1,2,3,4,5};m.RecalculateBounds();return m;
        }
        static Mesh MakeMotes()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var seeds=new List<Vector2>();var tr=new List<int>();
            for(int i=0;i<64;i++){int s=v.Count;float a=(i*.6180339f)%1,b=(i*.41421356f+.2f)%1;v.AddRange(new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});for(int j=0;j<4;j++)seeds.Add(new Vector2(a,b));tr.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});}
            var m=new Mesh{name="64 GPU advected chakra grains"};m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,seeds);m.SetTriangles(tr,0);m.bounds=new Bounds(Vector3.up,Vector3.one*6);return m;
        }
        static Mesh MakeEars()
        {
            var m=new Mesh{name="Fox chakra ear silhouette"};m.vertices=new[]{new Vector3(-.2f,.14f),new Vector3(-.20f,.39f),new Vector3(-.02f,.19f),new Vector3(.05f,.16f),new Vector3(.17f,.35f),new Vector3(.21f,.12f)};
            m.uv=new[]{new Vector2(.3f,.4f),new Vector2(.5f,.1f),new Vector2(.7f,.4f),new Vector2(.3f,.4f),new Vector2(.5f,.1f),new Vector2(.7f,.4f)};m.triangles=new[]{0,1,2,3,4,5};m.RecalculateBounds();return m;
        }
    }
}
