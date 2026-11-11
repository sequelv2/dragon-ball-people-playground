using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mod;

namespace DBPU
{
    public partial class DBPU
    {
        static void RegisterGogito()
        {
            ModAPIPlus.RegisterSubcategory("Fusion", "Fusion warriors", UniqueTag+"5", "Gogito");
            ModAPIPlus.CreateCustomHuman("Gogito", "Gogeta and Vegito fused. Potara and Metamoran vest. Base power starts at twice Gogeta Blue; SSJ1, SSJ2, SSJ3, God, Blue and Kaioken use DBPU controls.",
                "Assets/Icons/Gogito.png", SetupGogito, "Fusion", "01");
        }

        internal static float GogitoBasePower()
        {
            float blue=CharacterPowerCatalog.GetDefaultPower("BM/Gogeta","SSJBlue",1000000f);
            var custom=CustomTransformationCatalog.GetForms("BM/Gogeta").FirstOrDefault(f=>f.ID=="SSJBlue");
            if(custom!=null && custom.PowerMultiplier>0)blue=custom.PowerMultiplier;
            blue=CharacterPowerCatalog.GetEffectivePower("BM/Gogeta","SSJBlue",blue);
            if(float.IsNaN(blue)||float.IsInfinity(blue)||blue<=0)blue=1000000f;
            return blue*2f;
        }

        internal static void SetupGogito(GameObject instance)
        {
            const string folder="Assets/Sprites/Characters/Gogito/";
            instance.name="Gogito";
            var person=instance.GetComponent<PersonBehaviour>();
            var head=instance.transform.Find("Head");
            var upper=instance.transform.Find("Body/UpperBody");
            var middle=instance.transform.Find("Body/MiddleBody");
            var lower=instance.transform.Find("Body/LowerBody");
            var sprites=LoadCharacterLimbSprites(folder);
            person.ChangeSkin(sprites);
            instance.transform.SetHeight(person,middle.gameObject,1.8f);
            ApplyGohanImmunities(head,person);
            instance.GetComponent<DbpuCharacterIdentity>().SpriteFolder=folder.TrimEnd('/');
            var hr=head.GetComponent<SpriteRenderer>();
            var hair=new GameObject("Hair");hair.transform.SetParent(head,false);
            var hairRenderer=hair.AddComponent<SpriteRenderer>();
            hairRenderer.sortingLayerID=hr.sortingLayerID;hairRenderer.sortingOrder=hr.sortingOrder+1;
            // A side-view sprite shows the near earring; its far counterpart is behind the head.
            foreach(bool near in new[]{false,true})
            {
                var e=new GameObject(near?"Potara":"PotaraFar");e.transform.SetParent(head,false);
                e.transform.localPosition=PotaraFusionHelpers.PotaraWornLocalPosition+new Vector3(near?0:.10f,0,0);
                e.transform.localScale=Vector3.one*PotaraFusionHelpers.PotaraWornVisualScale;
                var sr=e.AddComponent<SpriteRenderer>();sr.sprite=PotaraFusionHelpers.CachedPotaraSprite;
                sr.sortingLayerID=hr.sortingLayerID;sr.sortingOrder=hr.sortingOrder+(near?2:-1);
            }
            var ki=instance.AddComponent<KiSystem>();ki.maxKi=200000000f;ki.baseRegen=2000000f;
            instance.AddComponent<AuraHandler>().person=person;
            var forms=instance.AddComponent<CharacterTransformations>();
            var skins=instance.AddComponent<CharacterSkins>();
            var skin=new CharacterSkins.Skins.SkinInfo{Name="Base",DisplayName="Gogito fusion vest"};
            skin.Skins=new Dictionary<string,Dictionary<string,Dictionary<string,List<Sprite>>>>
            {{"Base",new Dictionary<string,Dictionary<string,List<Sprite>>>{{"None",new Dictionary<string,List<Sprite>>{{"Damage0",sprites}}}}}};
            skin.Illustrations=new Dictionary<string,Sprite>{{"Damage0",ModAPI.LoadSprite("Assets/Icons/Gogito.png")}};
            skins.skins.AddSkin("Base",skin);
            string[] ids={"Base","SSJ1","SSJ2","SSJ3","SSJGod","SSJBlue"};
            string[] names={"Base","Super Saiyan 1","Super Saiyan 2","Super Saiyan 3","Super Saiyan God","Super Saiyan Blue"};
            float[] relative={1,50,100,400,20000,1000000};
            float[] drains={0,400000,800000,2400000,500000,1200000};
            Vector2[] hairPixels={new Vector2(-3,11),new Vector2(-3,10),new Vector2(-3,12),new Vector2(-9,-4),new Vector2(-3,9),new Vector2(-3,10)};
            float basePower=GogitoBasePower();
            for(int i=0;i<ids.Length;i++)
            {
                var icon=ModAPI.LoadSprite(folder+"Icon-"+ids[i]+".png");
                var headSprite=ModAPI.LoadSprite(folder+"Head-"+ids[i]+".png");
                var formSprites=new List<Sprite>(sprites);formSprites[0]=headSprite;
                skin.Skins["Base"][ids[i]]=new Dictionary<string,List<Sprite>>{{"Damage0",formSprites}};
                forms.transformations.AddTransformation(ids[i],new CharacterTransformations.Transformations.TransformationStats
                {
                    ID=ids[i],Name=names[i],PowerMultiplier=basePower*relative[i],Size=1.8f,
                    Description=i==0?"Gogeta + Vegito fusion. Base strength: twice the configured Gogeta Blue power.":"Gogito transformation. "+relative[i].ToString("0")+"x base power; standard DBPU ki, combat and aura.",
                    KiDrainRate=drains[i],AuraID=ids[i],SkinDamageAmount=0,HeadSprite=headSprite,
                    HairSprite=ModAPI.LoadSprite(folder+"Hair-"+ids[i]+".png"),UseHairLocalPosition=true,
                    HairLocalPosition=new Vector3(hairPixels[i].x/35f,hairPixels[i].y/35f,0),Icon=icon,Illustration=icon
                });
            }
            hairRenderer.sprite=forms.transformations.TransformationData["Base"].HairSprite;
            hair.transform.localPosition=forms.transformations.TransformationData["Base"].HairLocalPosition;
            var skinMenu=head.gameObject.AddComponent<SkinContextMenu>();skinMenu.SkinsData=skins;skinMenu.CurrentSkin="Base";skinMenu.PreviousSkin="Base";
            var menu=head.gameObject.AddComponent<TransformationContextMenu>();menu.TransformationsData=forms;menu.person=person;menu.skinContextMenu=skinMenu;
            foreach(var limb in person.Limbs)
            {
                limb.Health*=5e22f*HealthMultiplier;limb.InitialHealth*=5e22f*HealthMultiplier;
                limb.BreakingThreshold*=5e22f;limb.GForcePassoutThreshold*=100f;
                limb.ImpactPainMultiplier=0;limb.ImpactDamageMultiplier/=10000f;limb.ShotDamageMultiplier=0;
                limb.FreezingTemperature=-300;limb.DiscomfortingHeatTemperature=2000;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold=false;
            }
            instance.AddComponent<GogitoVitality>();
            var kaioken=instance.AddComponent<Kaioken>();kaioken.CompatibleForms.Add("SSJGod");
            instance.AddComponent<BlastBehaviour>();
            var beam=instance.AddComponent<BeamBehaviour>();beam.EffectID="Kamehameha";beam.Character="Gogeta";
            lower.gameObject.AddComponent<Flight>();instance.AddComponent<InstantTransmission>();
            person.Limbs[1].gameObject.AddComponent<Barrage>();
            person.Limbs[11].gameObject.AddComponent<PunchPower>();person.Limbs[13].gameObject.AddComponent<PunchPower>();
            var powers=PowerSetup.BeginPowers(head.gameObject,person,instance);var refs=powers.Refs;
            powers.Grant("KiBlast",new PowerCatalog.Overrides{BlockWhen=()=>refs.BeamStillBlocked,OnEnable=()=>refs.Beam.Active=false});
            powers.GrantMany(PowerSetup.BeamBlock(refs,()=>refs.Blast.Active=false),"EnergyWave","Kamehameha","FullForceKamehameha");
            powers.Finish();PowerSetup.CompletePhysicalPowers(instance,person);
            upper.GetComponent<PhysicalBehaviour>().ContextMenuOptions.Buttons.Add(new ContextMenuButton("ToggleAura",
                (Func<string>)(()=>instance.GetComponent<AuraHandler>().AuraActive?"Disable Aura":"Enable Aura"),"Toggle Aura",
                ()=>instance.GetComponent<AuraHandler>().AuraActive=!instance.GetComponent<AuraHandler>().AuraActive));
            CeceUtils.DelayedInvoke(.2f,()=>{if(person)menu.ApplyForm("Base");});
        }
    }

    // Keep Gogito's unusually large stats finite without losing health on Blue/Kaioken -> Base.
    // Other characters retain the existing DBPU calculation. Injury fractions are preserved.
    public sealed class GogitoVitality:MonoBehaviour
    {
        readonly Dictionary<LimbBehaviour,double> nominal=new Dictionary<LimbBehaviour,double>();
        public static bool Scale(LimbBehaviour limb,float ratio)
        {
            var guard=limb.Person?limb.Person.GetComponent<GogitoVitality>():null;
            if(!guard)return false;
            if(ratio<=0||float.IsNaN(ratio)||float.IsInfinity(ratio))return true;
            double hp;if(!guard.nominal.TryGetValue(limb,out hp))hp=limb.InitialHealth;
            double fraction=limb.InitialHealth>0?Math.Max(0,(double)limb.Health/limb.InitialHealth):0;
            hp=Math.Min(1e250,hp*ratio);guard.nominal[limb]=hp;
            limb.InitialHealth=(float)Math.Min(1e35,hp);
            limb.Health=(float)Math.Min(1e35,limb.InitialHealth*fraction);
            return true;
        }
    }
}
