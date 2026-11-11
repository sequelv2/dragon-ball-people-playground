using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using DBPU.NarutoFX;
namespace DBPU
{
    public partial class DBPU
    {
        static void ConfigureNarutoForms(GameObject instance,CharacterTransformations forms,CharacterSkins.Skins.SkinInfo skin,List<Sprite> baseline,Sprite hair)
        {
            string[] ids={"FoxLeak","Fox1","Fox2","Fox3","Sage","SageRobe","KCM1","KCM2","KuramaSage","SixPaths","SixPathsCloak"};
            string[] names={"Fox Chakra Leakage","One-Tail Cloak","Two-Tail Cloak","Three-Tail Cloak","Sage Mode","Sage Mode — Red Robe","Kurama Chakra Mode I","Kurama Mode II","Kurama II + Sage Mode","Six Paths Sage Mode","Six Paths Sage + Kurama"};
            string[] heads={"Fox","Fox","Fox","Fox","Sage","Sage","Kurama","Kurama","KuramaSage","SixPaths","SixPathsCloak"};
            float[] powers={2,3,5,8,10,10,25,125,312.5f,3125,15625};
            float[] drain={12,20,30,45,35,35,60,95,130,170,230};
            const string dir="Assets/Sprites/Characters/Naruto/Forms/";
            var faces=new Dictionary<string,Sprite>();
            foreach(string h in heads)if(!faces.ContainsKey(h))faces[h]=ModAPI.LoadSprite(dir+h+"Head.png",3);
            var bodies=new Dictionary<string,List<Sprite>>();
            foreach(string variant in new[]{"KCM1","KCM2","SixPathsCloak"})
            {
                string[] parts={"Upper","Middle","Lower","ULeg","LLeg","Foot","ULeg","LLeg","Foot","UArm","LArm","UArm","LArm"};
                var body=new List<Sprite>{faces[variant=="SixPathsCloak"?variant:"Kurama"]};
                foreach(var p in parts)body.Add(ModAPI.LoadSprite(dir+variant+"/"+p+".png"));bodies[variant]=body;
            }
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];List<Sprite> sprites=new List<Sprite>(id=="KCM1"?bodies["KCM1"]:id=="KCM2"||id=="KuramaSage"?bodies["KCM2"]:id=="SixPathsCloak"?bodies["SixPathsCloak"]:baseline);
                sprites[0]=faces[heads[i]];
                skin.Skins["Base"][id]=new Dictionary<string,List<Sprite>>{{"Damage0",sprites}};
                Color kiColor=i<4?new Color(1,.12f,.025f):i<6?new Color(.3f,.85f,.32f):new Color(1,.8f,.14f);
                forms.transformations.AddTransformation(id,new CharacterTransformations.Transformations.TransformationStats
                {
                    ID=id,Name=names[i],Description=names[i]+". Power: x"+powers[i].ToString(System.Globalization.CultureInfo.InvariantCulture)+" relative to Naruto's base."+(id=="SixPathsCloak"?" Nine visual Truth-Seeking Orbs.":""),
                    PowerMultiplier=.25f*powers[i],Size=1.66f,KiDrainRate=drain[i],AuraID="Naruto/"+id,requiredKiColor=kiColor,
                    HeadSprite=sprites[0],HairSprite=hair,UseHairLocalPosition=true,HairLocalPosition=new Vector3(-.025f,.12f,0),
                    RequiredSkin=sprites,Icon=faces[heads[i]],Illustration=faces[heads[i]],SkinDamageAmount=0
                });
            }
            instance.AddComponent<NarutoChakraController>().Initialize(instance.GetComponent<PersonBehaviour>());
        }
    }
    public sealed class NarutoChakraController:MonoBehaviour
    {
        static string directory;static AssetBundle bundle;
        public ChakraRig Rig {get;private set;}
        PersonBehaviour person;KiSystem ki;AuraHandler aura;LightSprite light;string current;float charge;
        public static void SetDirectory(string path){directory=path;}
        public void Initialize(PersonBehaviour p)
        {
            person=p;ki=GetComponent<KiSystem>();aura=GetComponent<AuraHandler>();
            try
            {
                if(!bundle)bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"Assets/NarutoVFX/naruto-vfx"));
                if(!bundle)throw new InvalidOperationException("Naruto Unity VFX bundle missing");
                ChakraRig.Prepare(name=>bundle.LoadAsset<Shader>("assets/shaders/"+name.ToLowerInvariant()+".shader"));
                var body=new SpriteRenderer[p.Limbs.Length];for(int i=0;i<body.Length;i++)body[i]=p.Limbs[i].GetComponent<SpriteRenderer>();
                Rig=new ChakraRig(transform,p.Limbs[2].transform,p.Limbs[3].transform,body,p.Limbs[0].transform.Find("Hair").GetComponent<SpriteRenderer>());
                light=ModAPI.CreateLight(p.Limbs[2].transform,new Color(1,.45f,.08f),2.6f,0);
            }
            catch(Exception e){Debug.LogError("[DBPU Naruto] "+e);enabled=false;}
        }
        void LateUpdate()
        {
            if(Rig==null||!person||!ki||!aura)return;
            string next=ki.CurrentForm??"Base";
            if(current!=next)
            {
                current=next;Rig.SetForm(next);
                if(next!="Base"){aura.DestroyAura();aura.AuraActive=true;}
            }
            float dt=Global.main!=null&&Global.main.Paused?0:Time.deltaTime;
            charge=Mathf.MoveTowards(charge,ki.KiChargeActiveForRegen?1:0,dt*2.5f);
            Rig.Tick(dt,aura.IsAuraEffectivelyActive&&aura.AuraVisible,DBPU.AurasEnabled,Mathf.Max(charge,aura.OverdriveVisualStrength*.9f));
            if(light!=null)
            {
                light.Color=current!=null&&current.StartsWith("Fox",StringComparison.Ordinal)?new Color(1,.08f,.02f):(current=="Sage"||current=="SageRobe"?new Color(.24f,.95f,.35f):new Color(1,.72f,.14f));
                light.Brightness=Rig.Fade*(.13f+charge*.12f);light.Radius=2.2f+charge*.6f;
            }
        }
        void OnDisable(){if(light!=null)light.Brightness=0;}
        void OnDestroy(){if(Rig!=null)Rig.Dispose();if(light!=null)Destroy(light.gameObject);}
    }
}
