using System;
using System.Collections.Generic;
using UnityEngine;
using Mod;
namespace DBPU
{
    public partial class DBPU
    {
        static void RegisterNaruto()
        {
            ModAPIPlus.RegisterSubcategory("Naruto", "Naruto crossover characters", UniqueTag+"4", "Naruto");
            ModAPIPlus.CreateCustomHuman("Naruto Uzumaki", "Naruto Uzumaki: Fox Chakra, Sage, Kurama and Six Paths forms. Uses DBPU combat and transformation controls.", "Assets/Icons/Naruto.png", SetupNaruto, "Naruto", "01");

        }
        internal static void SetupNaruto(GameObject instance)
        {
            const string folder="Assets/Sprites/Characters/Naruto/";
            instance.name="Naruto Uzumaki";
            var person=instance.GetComponent<PersonBehaviour>();
            var head=instance.transform.Find("Head");
            var middle=instance.transform.Find("Body/MiddleBody");
            var sprites=LoadCharacterLimbSprites(folder);
            sprites[0]=ModAPI.LoadSprite(folder+"HeadDetail.png",3);
            person.ChangeSkin(sprites);
            person.transform.root.transform.SetHeight(person,middle.gameObject,1.66f);
            ApplyGohanImmunities(head,person);
            var identity=instance.GetComponent<DbpuCharacterIdentity>();
            identity.SpriteFolder=folder.TrimEnd('/');
            var hair=new GameObject("Hair");hair.transform.SetParent(head,false);
            hair.transform.localPosition=new Vector3(-.025f,.12f,0);
            var renderer=hair.AddComponent<SpriteRenderer>();renderer.sprite=ModAPI.LoadSprite(folder+"Hair.png");
            Sprite authoredHair=renderer.sprite;
            var headRenderer=head.GetComponent<SpriteRenderer>();renderer.sortingLayerID=headRenderer.sortingLayerID;renderer.sortingOrder=headRenderer.sortingOrder+1;
            var ki=instance.AddComponent<KiSystem>();ki.maxKi=12000f;ki.baseRegen=180f;ki.barColor=new Color(1,.55f,.12f);
            instance.AddComponent<AuraHandler>().person=person;
            var forms=instance.AddComponent<CharacterTransformations>();
            forms.transformations.AddTransformation("Base",new CharacterTransformations.Transformations.TransformationStats
            {
                ID="Base",Name="Base",Description="Naruto Uzumaki — Shippuden. Base shinobi form.",
                PowerMultiplier=.25f,Size=1.66f,KiDrainRate=0,AuraID="Base",SkinDamageAmount=0,
                HeadSprite=sprites[0],HairSprite=renderer.sprite,UseHairLocalPosition=true,HairLocalPosition=hair.transform.localPosition,
                Icon=ModAPI.LoadSprite("Assets/Icons/Naruto.png"),Illustration=ModAPI.LoadSprite("Assets/Icons/Naruto.png")
            });
            var skins=instance.AddComponent<CharacterSkins>();
            var baseSkin=new CharacterSkins.Skins.SkinInfo {Name="Base",DisplayName="Shippuden"};
            baseSkin.Skins=new Dictionary<string,Dictionary<string,Dictionary<string,List<Sprite>>>>
            {{"Base",new Dictionary<string,Dictionary<string,List<Sprite>>>{{"None",new Dictionary<string,List<Sprite>>{{"Damage0",sprites}}}}}};
            baseSkin.Illustrations=new Dictionary<string,Sprite>{{"Damage0",ModAPI.LoadSprite("Assets/Icons/Naruto.png")}};
            skins.skins.AddSkin("Base",baseSkin);
            ConfigureNarutoForms(instance,forms,baseSkin,sprites,renderer.sprite);
            var skinMenu=head.gameObject.AddComponent<SkinContextMenu>();skinMenu.SkinsData=skins;skinMenu.CurrentSkin="Base";skinMenu.PreviousSkin="Base";
            var menu=head.gameObject.AddComponent<TransformationContextMenu>();menu.TransformationsData=forms;menu.person=person;menu.skinContextMenu=skinMenu;
            foreach(var limb in person.Limbs)
            {
                // Deliberately below DBPU's Saiyan-saga fighters; not a claim of cross-series canon numbers.
                limb.Health*=30000000000f*HealthMultiplier;limb.InitialHealth*=30000000000f*HealthMultiplier;
                limb.BreakingThreshold*=30000000000f;limb.GForcePassoutThreshold*=100;
                limb.ImpactPainMultiplier=0;limb.ImpactDamageMultiplier/=10000;limb.ShotDamageMultiplier=0;
                limb.SkinMaterialHandler.ShouldRecolourWhenCold=false;
            }
            person.Limbs[11].gameObject.AddComponent<PunchPower>();person.Limbs[13].gameObject.AddComponent<PunchPower>();
            PowerSetup.BeginPowers(head.gameObject,person,instance).Finish();
            PowerSetup.CompletePhysicalPowers(instance,person);
            CeceUtils.DelayedInvoke(.1f,()=>{
                if(!person)return;
                // Refresh the stock Base artwork after legacy form snapshots have been applied.
                var b=forms.transformations.TransformationData["Base"];
                b.HeadSprite=sprites[0];b.HairSprite=authoredHair;b.UseHairLocalPosition=true;b.HairLocalPosition=new Vector3(-.025f,.12f,0);
                forms.ApplyTransformation(person,"Base");
            });
        }
    }
}

