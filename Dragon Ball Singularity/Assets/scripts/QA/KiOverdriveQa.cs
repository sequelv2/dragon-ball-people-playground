using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    public sealed partial class KiCombatQa
    {
        // Runs only in the disposable native scene, after the fist accounting checks.
        IEnumerator VerifyNativeOverdriveVisuals()
        {
            AuraHandler aura=person?person.GetComponent<AuraHandler>():null;
            if(!Check(()=>Assert(aura && support && ki,"Overdrive visual QA requires the existing native subject")))yield break;
            bool savedAuras=DBPU.AurasEnabled,savedActive=aura.AuraActive,savedVisible=aura.AuraVisible;
            string savedId=aura.CurrentAuraID;
            float savedTune=aura.AuraTuneScale,savedY=aura.AuraTuneY;
            float savedChargeScale=DBPU.KiChargeAuraScaleMultiplier,savedChargeSpeed=DBPU.KiChargeAuraParticleSpeedMultiplier;
            float savedSupport=support.SupportEnergy,savedRestored=support.restoredDonation,savedObserved=support.observedKi,savedKi=ki.currentKi;
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var chargeFlag=typeof(AuraHandler).GetField("_chargeVisualIntensityActive",flags);
            var sparkFlag=typeof(AuraHandler).GetField("_sparkingVisualIntensityActive",flags);
            bool savedCharge=chargeFlag!=null&&(bool)chargeFlag.GetValue(aura);
            bool savedSpark=sparkFlag!=null&&(bool)sparkFlag.GetValue(aura);
            var initialRefined=aura.CurrentAuraObject?aura.CurrentAuraObject.GetComponent<RefinedAuraVisual>():null;
            AuraLook initialPalette=initialRefined?initialRefined.TargetLook:null;
            KiOverdriveAura mode=null;GameObject native=null;RefinedAuraVisual refined=null;
            ParticleSystem[] systems=null;ParticleSystemShapeType[] shapes=null;Material[] nativeMaterials=null;
            var primary=new Color(.18f,.78f,.94f,1);var secondary=new Color(.84f,.98f,1,1);
            try
            {
                if(!Check(()=>
                {
                    DBPU.AurasEnabled=true;
                    support.ConsumeSupport(support.TotalReceived);ki.currentKi=Mathf.Max(1,support.ReferenceKi);
                    aura.DestroyAura();aura.CurrentAuraID=string.IsNullOrEmpty(savedId)?"Base":savedId;
                    aura.AuraActive=false;aura.AuraVisible=true;
                    aura.SetChargeVisualActive(false);aura.SetSparkingPulse(false);
                    support.ReturnSupport(support.ReferenceKi*1.2f);
                    Assert(support.OverdriveActive,"Visual QA did not activate Overdrive");
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    native=aura.CurrentAuraObject;mode=person.GetComponentInChildren<KiOverdriveAura>(true);
                    Assert(native && native.GetComponentsInChildren<Renderer>(true).Length>0,"Native source aura did not appear");
                    Assert(!aura.AuraActive,"Overdrive overwrote the user's inactive aura flag");
                    Assert(mode && mode.VolumeAttached && mode.ShouldRender,"Native Overdrive GPU volume did not bind after two frames");
                    bool volumeVisible=false;
                    foreach(var renderer in mode.GetComponentsInChildren<MeshRenderer>(true))
                        if(renderer && renderer.enabled && renderer.sharedMaterial==KiCombatVfxAssets.Overdrive && renderer.sharedMaterial.shader.isSupported)volumeVisible=true;
                    Assert(volumeVisible,"Bound Overdrive volume has no enabled supported GPU renderer");
                    Assert(mode.CurrentNativeAura==native,"Overdrive did not bind to the actual native aura");
                    Assert(mode.GetComponentsInChildren<LineRenderer>(true).Length==0,"Legacy Overdrive strips are still present");
                    systems=native.GetComponentsInChildren<ParticleSystem>(true);shapes=new ParticleSystemShapeType[systems.Length];nativeMaterials=new Material[systems.Length];
                    for(int i=0;i<systems.Length;i++)
                    {
                        shapes[i]=systems[i].shape.shapeType;
                        var renderer=systems[i].GetComponent<ParticleSystemRenderer>();nativeMaterials[i]=renderer?renderer.sharedMaterial:null;
                    }
                    aura.ApplyCustomAuraColors(primary,secondary,true,1);
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(aura.CurrentAuraObject==native && mode.CurrentNativeAura==native,"Custom palette replaced the native aura");
                    refined=native.GetComponent<RefinedAuraVisual>();
                    if(refined)AssertOverdriveColour(primary,mode.Mid,"Overdrive did not inherit the native custom colour");
                    else if(systems.Length>0)AssertOverdriveColour(systems[0].main.startColor.Evaluate(.5f),mode.Mid,"Overdrive did not inherit the native particle gradient");
                    Assert(native.GetComponentsInChildren<ParticleSystem>(true).Length==systems.Length,"Overdrive replaced the native emitter hierarchy");
                    for(int i=0;i<systems.Length;i++)
                    {
                        Assert(systems[i] && systems[i].shape.shapeType==shapes[i],"Native particle emission shape changed");
                        var renderer=systems[i].GetComponent<ParticleSystemRenderer>();
                        Assert((renderer?renderer.sharedMaterial:null)==nativeMaterials[i],"Native particle material was replaced");
                    }
                }))yield break;
                yield return new WaitForEndOfFrame();
                if(!Check(()=>{Capture("04b-native-overdrive-fury.png");aura.ToggleAuraVisibility(false);}))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(!mode.ShouldRender && aura.CurrentAuraObject==native && !native.activeInHierarchy,"Aura visibility did not hide the native and volume effects together");
                    aura.ToggleAuraVisibility(true);
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(mode.ShouldRender && aura.CurrentAuraObject==native,"Visibility restore rebuilt or lost the source aura");
                    DBPU.AurasEnabled=false;
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(support.OverdriveActive && !aura.CurrentAuraObject && !mode.ShouldRender,"Global aura toggle did not hide an active Overdrive");
                    DBPU.AurasEnabled=true;
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(aura.CurrentAuraObject && mode.ShouldRender && !aura.AuraActive,"Overdrive did not recover from the global aura toggle");
                    support.ConsumeSupport(support.TotalReceived);
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(!support.OverdriveActive && !aura.AuraActive && !aura.CurrentAuraObject,"Originally inactive aura persisted after Overdrive");
                    Assert(!mode.ShouldRender,"Overdrive volume survived the inactive mode");
                    aura.AuraActive=true;aura.AuraVisible=true;aura.CreateAura(aura.CurrentAuraID,false);
                    native=aura.CurrentAuraObject;Assert(native,"Explicitly active native aura did not start");
                    aura.ApplyCustomAuraColors(primary,secondary,true,1);support.ReturnSupport(support.ReferenceKi*1.2f);
                    aura.SetChargeVisualActive(true);aura.SetSparkingPulse(true);
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(aura.AuraActive && aura.CurrentAuraObject==native && mode.ShouldRender,"Overdrive replaced an already active native aura");
                    refined=native.GetComponent<RefinedAuraVisual>();
                    if(refined)
                    {
                        Near(1,refined.ChargeAmount,.001f,"Native charge request was lost during Overdrive");
                        AssertOverdriveColour(primary,refined.TargetLook.Mid,"Native custom palette was overwritten during Overdrive");
                    }
                    support.ConsumeSupport(support.TotalReceived);
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(!support.OverdriveActive && aura.AuraActive && aura.CurrentAuraObject==native,"Overdrive shutdown removed the independently active native aura");
                    if(chargeFlag!=null)Assert((bool)chargeFlag.GetValue(aura),"Overdrive shutdown cleared the native charge request");
                    if(sparkFlag!=null)Assert((bool)sparkFlag.GetValue(aura),"Overdrive shutdown cleared the native sparking request");
                    if(refined)
                    {
                        Near(1,refined.ChargeAmount,.001f,"Independent charge stopped with Overdrive");
                        AssertOverdriveColour(primary,refined.TargetLook.Mid,"Custom palette was restored to an older value");
                    }
                    aura.SetSparkingPulse(false);aura.SetChargeVisualActive(false);DBPU.AurasEnabled=false;
                }))yield break;
                yield return null;yield return null;
                if(!Check(()=>
                {
                    Assert(!aura.CurrentAuraObject && !mode.ShouldRender,"Global aura toggle did not release all Overdrive visuals");
                    Near(savedTune,aura.AuraTuneScale,0,"Overdrive changed aura tune scale");Near(savedY,aura.AuraTuneY,0,"Overdrive changed aura tune position");
                    Near(savedChargeScale,DBPU.KiChargeAuraScaleMultiplier,0,"Overdrive changed the configured charge size");
                    Near(savedChargeSpeed,DBPU.KiChargeAuraParticleSpeedMultiplier,0,"Overdrive changed the configured particle speed");
                    Log("PASS native Overdrive volume/source binding, original inactive/active aura ownership, custom palette, particle shapes/materials, visibility, charge/sparking coexistence and global cleanup");
                }))yield break;
            }
            finally
            {
                // This disposable test never saves settings, changes forms, or removes the fighter.
                DBPU.AurasEnabled=savedAuras;
                if(ki)ki.currentKi=savedKi;
                if(support)
                {
                    support.SupportEnergy=savedSupport;support.restoredDonation=savedRestored;support.observedKi=savedObserved;support.SupportStep(0);
                }
                if(aura)
                {
                    aura.DestroyAura();aura.CurrentAuraID=savedId;aura.AuraActive=savedActive;aura.AuraVisible=savedVisible;
                    if(savedAuras && savedVisible && aura.IsAuraEffectivelyActive)
                    {
                        aura.CreateAura(savedId,false);
                        var restored=aura.CurrentAuraObject?aura.CurrentAuraObject.GetComponent<RefinedAuraVisual>():null;
                        if(restored && initialPalette!=null){restored.SetStartingLook(initialPalette);restored.TargetLook=initialPalette;restored.PaletteMix=1;}
                    }
                    aura.SetChargeVisualActive(savedCharge);aura.SetSparkingPulse(savedSpark);
                }
            }
        }
        void AssertOverdriveColour(Color expected,Color actual,string reason)
        {
            Near(expected.r,actual.r,.003f,reason+" red");Near(expected.g,actual.g,.003f,reason+" green");Near(expected.b,actual.b,.003f,reason+" blue");
        }
    }
}
