using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Hollow rims never modify the body's material or mouse-hover property blocks.
    public sealed class GodBodyGlow : MonoBehaviour
    {
        sealed class Part
        {
            public SpriteRenderer Source;
            public MeshRenderer Rim;
            public MeshFilter Filter;
            public MaterialPropertyBlock Block=new MaterialPropertyBlock();
            public Sprite Sprite;
            public Vector4 Rect;
        }
        readonly List<Part> parts=new List<Part>();
        readonly Dictionary<Sprite,Mesh> meshes=new Dictionary<Sprite,Mesh>();
        Material material;
        public float Strength=1;
        public Color Tint=new Color(1,.83f,.09f,1);
        float clock;
        public void Bind(PersonBehaviour person)
        {
            if(CinematicAssets.RimShader==null||person==null)return;
            material=new Material(CinematicAssets.RimShader){name="God hollow gold rim"};
            var seen=new HashSet<SpriteRenderer>();
            foreach(var limb in person.Limbs)
            {
                if(limb==null)continue;
                foreach(var source in limb.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(!seen.Add(source)||source.sprite==null)continue;
                    string name=source.name.ToLowerInvariant();
                    if(name.Contains("highlight")||name.Contains("selection")||name.Contains("outline")||name.Contains("hover")||name.Contains("glow"))continue;
                    // Hair and its nested glow sprites must not generate a second
                    // silhouette. SSJ3 makes that duplicate especially conspicuous.
                    bool hair=false;
                    for(var node=source.transform;node!=null;node=node.parent)
                    {
                        if(node.name.ToLowerInvariant().Contains("hair")){hair=true;break;}
                        if(node==limb.transform)break;
                    }
                    if(hair)continue;
                    var go=new GameObject("God silhouette light");go.transform.SetParent(source.transform,false);
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder-1;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                    parts.Add(new Part{Source=source,Rim=renderer,Filter=go.AddComponent<MeshFilter>()});
                }
            }
        }
        void UpdateSprite(Part part)
        {
            var sprite=part.Source.sprite;part.Sprite=sprite;
            Vector2 min=new Vector2(1,1),max=Vector2.zero;
            foreach(var uv in sprite.uv){min=Vector2.Min(min,uv);max=Vector2.Max(max,uv);}
            part.Rect=new Vector4(min.x,min.y,max.x,max.y);
            Mesh mesh;
            if(!meshes.TryGetValue(sprite,out mesh))
            {
                var bounds=sprite.bounds;float pad=1.4f/sprite.pixelsPerUnit;
                float x0=bounds.min.x-pad,x1=bounds.max.x+pad,y0=bounds.min.y-pad,y1=bounds.max.y+pad;
                float du=(max.x-min.x)*pad/Mathf.Max(.001f,bounds.size.x),dv=(max.y-min.y)*pad/Mathf.Max(.001f,bounds.size.y);
                mesh=new Mesh{name="Cached hollow sprite rim"};
                mesh.vertices=new[]{new Vector3(x0,y0,0),new Vector3(x1,y0,0),new Vector3(x1,y1,0),new Vector3(x0,y1,0)};
                mesh.uv=new[]{new Vector2(min.x-du,min.y-dv),new Vector2(max.x+du,min.y-dv),new Vector2(max.x+du,max.y+dv),new Vector2(min.x-du,max.y+dv)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(sprite,mesh);
            }
            part.Filter.sharedMesh=mesh;
            part.Block.SetTexture("_MainTex",sprite.texture);part.Block.SetVector("_SpriteRect",part.Rect);
        }
        void LateUpdate()
        {
            if(Global.main==null||!Global.main.Paused)clock+=Time.deltaTime;
            float pulse=.48f+.08f*Mathf.Sin(clock*3.1f)+.035f*Mathf.Sin(clock*5.3f);
            foreach(var part in parts)
            {
                if(part.Rim==null)continue;
                var source=part.Source;
                bool visible=source!=null&&source.enabled&&source.gameObject.activeInHierarchy&&source.sprite!=null&&Strength>.001f;
                part.Rim.enabled=visible;if(!visible)continue;
                if(source.sprite!=part.Sprite)UpdateSprite(part);
                part.Rim.transform.localScale=new Vector3(source.flipX?-1:1,source.flipY?-1:1,1);
                part.Block.SetColor("_Color",new Color(Tint.r,Tint.g,Tint.b,source.color.a));
                part.Block.SetFloat("_Strength",Strength*pulse);part.Rim.SetPropertyBlock(part.Block);
            }
        }
        void OnDisable(){foreach(var part in parts)if(part.Rim!=null)part.Rim.enabled=false;}
        void OnDestroy()
        {
            foreach(var part in parts)if(part.Rim!=null)Destroy(part.Rim.gameObject);
            foreach(var mesh in meshes.Values)Destroy(mesh);
            if(material!=null)Destroy(material);
        }
    }
}
