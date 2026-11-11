using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace DBPU
{
    public sealed partial class KiCombatQa
    {
        IEnumerator VerifyReferenceHud()
        {
            GameObject[] actors=new GameObject[3];CombatPowerController[] powers=new CombatPowerController[3];
            float health=0,baseDamage=0;LimbBehaviour head=null;bool detailBefore=false,legacyAuras=true;
            if(!Check(()=>
            {
                // The combat HUD is only drawn in the native Detail view (S).
                detailBefore=Global.main.ShowLimbStatus;if(!detailBefore)Global.main.ToggleLimbStatus();
                foreach(var p in FindObjectsOfType<PersonBehaviour>())p.transform.position+=new Vector3(-180,0,0);
                DBPU.AurasEnabled=true;DBPU.KiChargeFxEnabled=true;DBPU.SparkingEnabled=true;DBPU.KiDrainMultiplier=0;
                Global.main.camera.transform.position=new Vector3(0,32,-10);Global.main.camera.orthographicSize=4.6f;
                Screen.SetResolution(1600,900,false);
                for(int i=0;i<3;i++)actors[i]=Spawn("[DBPU] (BM) Goku",new Vector3((i-1)*5.4f,30,0));
            }))yield break;
            yield return new WaitForSecondsRealtime(1.8f);
            if(!Check(()=>
            {
                for(int i=0;i<3;i++)
                {
                    PersonBehaviour p;KiSystem k;Prepare(actors[i],out p,out k);powers[i]=CombatPowerController.Bind(p);
                    foreach(var r in actors[i].GetComponentsInChildren<SpriteRenderer>())r.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                    var menu=actors[i].GetComponentInChildren<TransformationContextMenu>();Assert(menu,"Native head transformation menu absent");menu.ApplyForm("SSJ"+(i+1));
                }
            }))yield break;
            yield return new WaitForSecondsRealtime(3);
            if(!Check(()=>
            {
                for(int i=0;i<3;i++)
                {
                    var p=powers[i];Assert(p.Ki.CurrentForm=="SSJ"+(i+1),"Native form did not commit "+(i+1));
                    var aura=p.GetComponent<AuraHandler>();aura.AuraActive=aura.AuraVisible=true;aura.CreateAura("SSJ"+(i+1));
                    var root=aura.CurrentAuraObject;Assert(root,"Missing runtime aura");
                    // SSJ1-3 moved to the Redux aura rig; the CanonKi checks apply only where the old rig remains.
                    if(!root.GetComponent<CinematicAuraRig>()){legacyAuras=false;Log("SKIP CanonKi aura checks: SSJ"+(i+1)+" uses "+root.name);continue;}
                    var renderers=root.GetComponentsInChildren<MeshRenderer>();Assert(renderers.Length>=3,"Previous aura layers missing");
                    Assert(renderers.Any(r=>r.sharedMaterial && r.sharedMaterial.shader.name=="DBPU/Cinematic/CanonKi"),"Previous CanonKi aura was not restored");
                    foreach(var r in renderers)
                    {
                        Assert(r.sharedMaterial && r.sharedMaterial.shader.isSupported,"Restored aura shader fallback");
                        Assert(!r.sharedMaterial.shader.name.Contains("SaiyanEnergy") && !r.sharedMaterial.shader.name.Contains("Reconstructed"),"Experimental aura still active");
                    }
                    Assert(p.GetComponent<CombatHudView>() && p.PowerLevel>0,"Live HUD/power model absent");
                }
                head=powers[0].Person.Limbs.First(l=>l.name=="Head");head.Health=head.InitialHealth*.65f;
                health=head.InitialHealth;baseDamage=CombatPowerController.AttackHealth(powers[0].Person);
                foreach(float value in CombatPowerMath.Steps)
                {
                    powers[0].SetOutput(value);Near(health*value,head.InitialHealth,health*.00001f,"Output health scaling");
                    Near(.65f,head.Health/head.InitialHealth,.0001f,"Output healed or damaged wound fraction");
                    Near(baseDamage,CombatPowerController.AttackHealth(powers[0].Person),Mathf.Max(.01f,baseDamage*.0001f),"Attack suppression was applied twice");
                    Near(baseDamage*value,CombatPowerController.AttackHealth(powers[0].Person)*powers[0].Output,Mathf.Max(.01f,baseDamage*.0001f),"Actual attack output must scale once");
                }
                powers[0].SetOutput(1);Near(health,head.InitialHealth,health*.0001f,"Output round trip health");
                for(int n=0;n<200;n++){powers[0].Cycle(1);powers[0].Cycle(-1);}
                Near(health,head.InitialHealth,health*.0001f,"Repeated suppression drift");
                powers[0].SetOutput(.1f);powers[1].SetOutput(.5f);powers[2].SetOutput(1);
                Log("PASS five output levels, reverse cycle, one-fold damage, preserved wounds and 400-cycle stability");
            }))yield break;
            BlastBehaviour probe=powers[0].GetComponent<BlastBehaviour>();float fullBlast=0;
            if(!Check(()=>
            {
                Assert(probe,"Native ki blast damage probe missing");powers[0].SetOutput(1);probe.Firing=true;
                foreach(var canvas in FindObjectsOfType<Canvas>())if(canvas.name!="DBPU Combat HUD")canvas.enabled=false;
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>{fullBlast=probe.RealDamage;Assert(fullBlast>0,"Native blast damage did not update");}))yield break;
            foreach(float value in CombatPowerMath.Steps)
            {
                powers[0].SetOutput(value);yield return null;yield return new WaitForEndOfFrame();
                if(!Check(()=>Near(fullBlast*value,probe.RealDamage,Mathf.Max(1,fullBlast*.0001f),"Native blast damage applied Output twice")))yield break;
            }
            if(!Check(()=>{probe.Firing=false;powers[0].SetOutput(.1f);Log("PASS actual native BlastBehaviour damage at all five output values");}))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Canvas.ForceUpdateCanvases();
                foreach(var p in powers)
                {
                    var view=p.GetComponent<CombatHudView>();Assert(view.Panel && view.Panel.gameObject.activeInHierarchy,"HUD panel absent");
                    var corners=new Vector3[4];view.Panel.GetWorldCorners(corners);
                    Assert(corners.All(v=>v.x>=-1 && v.x<=Screen.width+1 && v.y>=-1 && v.y<=Screen.height+1),"HUD outside viewport");
                    foreach(var label in view.Panel.GetComponentsInChildren<TextMeshProUGUI>())Assert(!label.isTextOverflowing,"HUD text overflow "+label.text);
                }
                Capture("09-reference-ssj-hud.png");
                Log("PASS previous SSJ1/2/3 CanonKi auras restored and HUD viewport/text bounds");
            }))yield break;
            // The same scene without the HUD, as a backdrop for HUD design mockups.
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-dbpu-qa-clean-frame")>=0)
            {
                var hudRoot=GameObject.Find("DBPU Combat HUD");
                if(hudRoot)
                {
                    hudRoot.SetActive(false);yield return null;yield return new WaitForEndOfFrame();
                    Check(()=>Capture("09-clean-frame.png"));
                    hudRoot.SetActive(true);yield return null;
                }
            }
            yield return VerifyShieldHud(powers[1]);
            if(finished)yield break;
            if(legacyAuras)
            {
                var sample=powers[1].GetComponent<AuraHandler>().CurrentAuraObject.GetComponentInChildren<MeshRenderer>();
                var block=new MaterialPropertyBlock();sample.GetPropertyBlock(block);float clock=block.GetFloat("_VfxTime");
                yield return null;yield return new WaitForEndOfFrame();
                if(!Check(()=>{sample.GetPropertyBlock(block);Assert(block.GetFloat("_VfxTime")>clock,"Aura did not advance on the next frame");if(!Global.main.Paused)Global.main.TogglePaused();}))yield break;
                yield return null;sample.GetPropertyBlock(block);clock=block.GetFloat("_VfxTime");
                yield return null;yield return null;
                if(!Check(()=>{sample.GetPropertyBlock(block);Near(clock,block.GetFloat("_VfxTime"),.000001f,"Paused aura clock advanced");if(Global.main.Paused)Global.main.TogglePaused();}))yield break;
            }
            yield return VerifyKaiokenHud(powers[0]);
            if(finished)yield break;
            yield return VerifyHudPointerAndMotion(powers[1]);
            if(finished)yield break;
            double before=0;float multiplier=0;
            if(!Check(()=>
            {
                powers[2].Ki.currentKi=powers[2].Ki.maxKi; powers[2].Tick(.1f);before=powers[2].PowerLevel;
                multiplier=powers[2].Multipliers.FinalMultiplier;
                DBPU.KiChargeSparkingOverchargeEnabled=false;DBPU.KiChargeAutoTransformWhileChargingEnabled=false;
                foreach(var input in powers[2].GetComponentsInChildren<KiChargeHeadHoldInput>())input.enabled=false;
                powers[2].GetComponent<KiChargeModeController>().SetHoldDesired(true);
            }))yield break;
            yield return new WaitForSeconds(.7f);
            if(!Check(()=>
            {
                Assert(powers[2].Charge01>.1f && powers[2].PowerLevel>before,"Charging did not raise live power");
                powers[2].GetComponent<KiChargeModeController>().SetHoldDesired(false);
                Assert(powers[2].GetComponent<KiSparkingController>().TryStartOverload(),"Native Sparking did not start");
            }))yield break;
            yield return new WaitForSeconds(.25f);
            if(!Check(()=>
            {
                Assert(powers[2].Ki.SparkingOverloadActive && powers[2].PowerLevel>before*1.2,"Sparking did not raise power");
                Assert(powers[2].Multipliers.FinalMultiplier>multiplier*1.2,"Sparking bonus did not reach real attacks");
                powers[2].Ki.Strain=.61f;
            }))yield break;
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("10-sparking-output-strain.png");
                Assert(powers[2].GetComponent<CombatHudView>().Art.Strain>.5f,"Strain gauge did not read the native strain");
                Log("PASS per-frame aura time, pause, charge power, real Sparking attack multiplier and native strain gauge");
                if(Global.main.ShowLimbStatus!=detailBefore)Global.main.ToggleLimbStatus();
            }))yield break;
        }

        IEnumerator VerifyKaiokenHud(CombatPowerController power)
        {
            Kaioken kaioken=null;BlastBehaviour blast=null;LimbBehaviour wounded=null;
            double baselinePower=0;float baselineHealth=0,baselineDamage=0,baselineSphere=0,savedOutput=0,maxMultiplier=0;
            if(!Check(()=>
            {
                kaioken=power.GetComponent<Kaioken>();blast=power.GetComponent<BlastBehaviour>();
                Assert(kaioken && blast,"Native Goku Kaioken/blast components missing");
                Assert(!kaioken.Active,"Kaioken unexpectedly active before ratio checks");
                savedOutput=power.Output;power.SetOutput(1);
                DBPU.KaiokenStrainDamageEnabled=false;DBPU.KaiokenStrainMultiplier=0;
                maxMultiplier=DBPU.KaiokenMaxMultiplier;DBPU.KaiokenMaxMultiplier=Mathf.Max(20,maxMultiplier);
                foreach(var input in power.GetComponentsInChildren<KiChargeHeadHoldInput>())input.enabled=false;
                power.GetComponent<KiChargeModeController>().SetHoldDesired(false);
                power.Ki.currentKi=power.Ki.maxKi;power.Tick(.1f);
                wounded=power.Person.Limbs.First(l=>l.name=="Head");wounded.Health=wounded.InitialHealth*.65f;
                baselineHealth=wounded.InitialHealth;baselinePower=power.PowerLevel;blast.Firing=true;
                Assert(baselinePower>0 && baselineHealth>0,"Invalid native Kaioken baseline");
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                baselineDamage=blast.RealDamage;Assert(baselineDamage>0,"Native Kaioken blast baseline absent");
                baselineSphere=KaiokenSphereDamage(power);Assert(baselineSphere>0,"Native Spirit Bomb damage baseline absent");
                kaioken.ApplyConfiguredMultiplier(20);power.Refresh();
                Near(baselineHealth,wounded.InitialHealth,baselineHealth*.0003f,"Inactive Kaioken configuration changed health");
                Near(1,(float)(power.PowerLevel/baselinePower),.0003f,"Inactive Kaioken configuration changed power");
                kaioken.ApplyConfiguredMultiplier(2);kaioken.ToggleFromPowersMenu();
                Assert(kaioken.Active,"Native Kaioken did not activate");
            }))yield break;
            foreach(float factor in new[]{2f,3f,5f,10f,20f})
            {
                if(!Check(()=>kaioken.ApplyConfiguredMultiplier(factor)))yield break;
                yield return null;yield return new WaitForEndOfFrame();
                if(!Check(()=>
                {
                    power.Ki.currentKi=power.Ki.maxKi;power.Tick(.1f);
                    Near(factor,(float)(power.PowerLevel/baselinePower),factor*.0003f,"Kaioken HUD must apply selected factor once");
                    Near(factor,wounded.InitialHealth/baselineHealth,factor*.0003f,"Kaioken maximum health factor");
                    Near(factor,blast.RealDamage/baselineDamage,factor*.0003f,"Native Kaioken blast damage factor");
                    Near(factor,KaiokenSphereDamage(power)/baselineSphere,factor*.0003f,"Native Kaioken Spirit Bomb damage factor");
                    Near(.65f,wounded.Health/wounded.InitialHealth,.0003f,"Kaioken multiplier healed or damaged wound fraction");
                    Log("KAIOKEN x"+factor+" power ratio="+(power.PowerLevel/baselinePower)+" health ratio="+(wounded.InitialHealth/baselineHealth)+" native blast ratio="+(blast.RealDamage/baselineDamage));
                    if(factor==2)Capture("11-kaioken-x2-hud.png");
                }))yield break;
            }
            if(!Check(()=>
            {
                // Changing and ending Kaioken in the same frame must undo the
                // actual health bonus, including the previous coroutine race.
                kaioken.ApplyConfiguredMultiplier(3);kaioken.ToggleFromPowersMenu();
                Assert(!kaioken.Active,"Native Kaioken did not deactivate immediately");
                power.Ki.currentKi=power.Ki.maxKi;power.Tick(.1f);
                Near(1,(float)(power.PowerLevel/baselinePower),.0003f,"Immediate Kaioken exit power drift");
                Near(baselineHealth,wounded.InitialHealth,baselineHealth*.0003f,"Immediate Kaioken exit health drift");
                Near(.65f,wounded.Health/wounded.InitialHealth,.0003f,"Immediate Kaioken exit wound fraction");
                for(int cycle=0;cycle<20;cycle++)
                {
                    kaioken.ApplyConfiguredMultiplier(2);kaioken.ToggleFromPowersMenu();
                    kaioken.ApplyConfiguredMultiplier(20);kaioken.ToggleFromPowersMenu();
                }
                power.Ki.currentKi=power.Ki.maxKi;power.Tick(.1f);
                Near(1,(float)(power.PowerLevel/baselinePower),.0005f,"Repeated Kaioken cycles inflated HUD power");
                Near(baselineHealth,wounded.InitialHealth,baselineHealth*.0005f,"Repeated Kaioken cycles inflated health");
                Near(.65f,wounded.Health/wounded.InitialHealth,.0005f,"Repeated Kaioken cycles changed wound fraction");
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Near(1,blast.RealDamage/baselineDamage,.0005f,"Kaioken exit left native attack bonus behind");
                Near(1,KaiokenSphereDamage(power)/baselineSphere,.0005f,"Kaioken exit left Spirit Bomb attack bonus behind");
                blast.Firing=false;power.SetOutput(savedOutput);DBPU.KaiokenMaxMultiplier=maxMultiplier;
                Log("PASS exact Kaioken x2/x3/x5/x10/x20 HUD, real health, native blast and Spirit Bomb ratios; inactive configuration, same-frame exit, preserved wounds and 20 enter/change/exit cycles");
            }))yield break;
        }

        // The shield gauge appears while the barrier is up and flashes BROKEN through its cooldown.
        IEnumerator VerifyShieldHud(CombatPowerController power)
        {
            KiShield shield=null;CombatHudView view=null;
            if(!Check(()=>
            {
                shield=power.GetComponent<KiShield>();view=power.GetComponent<CombatHudView>();
                if(!shield){Log("SKIP shield HUD: fighter has no KiShield");return;}
                power.Ki.currentKi=power.Ki.maxKi;Assert(shield.TryRaise(),"Barrier failed to raise for the HUD check");
            }))yield break;
            if(!shield)yield break;
            yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(view.Art.Shielded && !view.Art.ShieldBroken,"HUD did not show the raised barrier");
                Capture("09b-hud-shield.png");shield.Lower(true);
            }))yield break;
            yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(view.Art.Shielded && view.Art.ShieldBroken,"HUD did not show the broken barrier");
                Capture("09c-hud-shield-broken.png");
            }))yield break;
            yield return new WaitForSecondsRealtime(3.2f);yield return null;
            if(!Check(()=>
            {
                Assert(!view.Art.Shielded,"Shield gauge stayed after the cooldown");
                Log("PASS shield gauge raised, broken and cleared after cooldown");
            }))yield break;
        }

        float KaiokenSphereDamage(CombatPowerController power)
        {
            var arm=power.Person.Limbs.First(KiHandLoadout.IsHand);
            var sphere=SpiritBombProjectile.Create(power.Person,arm);
            Assert(sphere,"Native Spirit Bomb damage probe could not instantiate");
            try
            {
                float energy=power.Ki.maxKi*.10f;
                Near(energy,sphere.AddEnergy(energy,false),Mathf.Max(.001f,energy*.0001f),"Spirit Bomb probe did not accept fixed energy");
                sphere.Launch(Vector2.up);
                Assert(sphere.Phase==SpiritBombPhase.Flight,"Spirit Bomb probe did not launch");
                return sphere.Damage;
            }
            finally
            {
                // Prevent any physics tick, collision or donation from the
                // disposable launch snapshot before deferred destruction.
                sphere.gameObject.SetActive(false);Destroy(sphere.gameObject);
            }
        }

        static Vector2 HudScreenPoint(RectTransform rect,Vector2 local)
        {return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local));}

        IEnumerator VerifyHudPointerAndMotion(CombatPowerController power)
        {
            CombatHudView view=null;CombatHudOutputButton button=null;PointerEventData click=null;
            int blockerCount=0;float savedOutput=0,fullHealth=0;LimbBehaviour head=null;
            if(!Check(()=>
            {
                view=power.GetComponent<CombatHudView>();Assert(view && view.OutputButton,"Output circle button absent");
                button=view.OutputButton;Assert(EventSystem.current,"Native EventSystem absent");
                savedOutput=power.Output;power.SetOutput(1);head=power.Person.Limbs.First(l=>l.name=="Head");fullHealth=head.InitialHealth;
                Canvas.ForceUpdateCanvases();
                var raycaster=button.canvas.GetComponent<GraphicRaycaster>();Assert(raycaster,"HUD native GraphicRaycaster absent");
                click=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=HudScreenPoint(button.rectTransform,button.rectTransform.rect.center)};
                var hits=new List<RaycastResult>();raycaster.Raycast(click,hits);
                Assert(hits.Any(hit=>hit.gameObject==button.gameObject),"Gray circle is not a native UI hit target");
                click.position=HudScreenPoint(button.rectTransform,button.rectTransform.rect.max-Vector2.one);
                hits.Clear();raycaster.Raycast(click,hits);
                Assert(!hits.Any(hit=>hit.gameObject==button.gameObject),"Circle corner captures world input outside the round face");
                click.position=HudScreenPoint(view.Panel,new Vector2(380,84));hits.Clear();raycaster.Raycast(click,hits);
                Assert(!hits.Any(hit=>hit.gameObject.transform.IsChildOf(view.Panel)),"Gauge bars unexpectedly capture world input");
                click.position=HudScreenPoint(button.rectTransform,button.rectTransform.rect.center);
                blockerCount=Global.main.ActiveUiBlockers;
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerDownHandler);
                Assert(Global.main.ActiveUiBlockers==blockerCount+1 && CombatHudView.InputCaptured,"Output press did not protect the world beneath the HUD");
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerClickHandler);
                Near(.01f,power.Output,.00001f,"Left click must advance Output one step");
                Near(fullHealth*.01f,head.InitialHealth,fullHealth*.00001f,"Output circle did not change actual maximum health");
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerClickHandler);
                Near(.01f,power.Output,.00001f,"Duplicate pointer event cycled Output twice");
            }))yield break;
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(Global.main.ActiveUiBlockers==blockerCount,"Output click leaked its UI blocker");
                click.button=PointerEventData.InputButton.Right;
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerClickHandler);
                Near(1,power.Output,.00001f,"Right click must reverse Output one step");
                Near(fullHealth,head.InitialHealth,fullHealth*.00001f,"Reverse circle click did not restore maximum health");
            }))yield break;
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(Global.main.ActiveUiBlockers==blockerCount,"Reverse Output click leaked its UI blocker");
                click.button=PointerEventData.InputButton.Left;
                ExecuteEvents.Execute(button.gameObject,click,ExecuteEvents.pointerDownHandler);
                view.enabled=false;
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(Global.main.ActiveUiBlockers==blockerCount,"Disabling a pressed HUD leaked its UI blocker");
                view.enabled=true;power.SetOutput(savedOutput);
                Log("PASS native circle raycast, transparent bars/corners, left/reverse click, one-step real health changes, duplicate-event guard and UI-blocker release/cancel");
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();

            Vector3 start=power.transform.position;float peak=0,peakAngle=0;
            for(int frame=0;frame<24;frame++)
            {
                power.transform.position+=new Vector3(.12f,0,0);
                yield return null;yield return new WaitForEndOfFrame();
                if(!Check(()=>
                {
                    Assert(Finite(view.MotionOffset.x) && Finite(view.MotionOffset.y) && Finite(view.MotionAngle),"HUD motion produced an invalid value");
                    peak=Mathf.Max(peak,view.MotionOffset.magnitude);peakAngle=Mathf.Max(peakAngle,Mathf.Abs(view.MotionAngle));
                    Assert(peak<=50 && peakAngle<=5,"HUD inertia exceeds a small readable movement");
                }))yield break;
            }
            if(!Check(()=>
            {
                Assert(peak>.1f || peakAngle>.02f,"Moving fighter produced no HUD inertia");
                Global.main.TogglePaused();
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            Vector2 frozen=view.MotionOffset;float frozenAngle=view.MotionAngle;
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Near(frozen.x,view.MotionOffset.x,.0001f,"Paused HUD inertia moved horizontally");
                Near(frozen.y,view.MotionOffset.y,.0001f,"Paused HUD inertia moved vertically");
                Near(frozenAngle,view.MotionAngle,.0001f,"Paused HUD inertia rotated");
                Global.main.TogglePaused();Capture("12-hud-inertia.png");
            }))yield break;
            yield return new WaitForSecondsRealtime(1.6f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(view.MotionOffset.magnitude<.25f && Mathf.Abs(view.MotionAngle)<.06f,"HUD did not settle after the fighter stopped");
                power.transform.position+=new Vector3(30,0,0);
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(view.MotionOffset.magnitude<.25f && Mathf.Abs(view.MotionAngle)<.06f,"Teleport did not reset HUD inertia");
                power.transform.position=start;
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Log("PASS bounded HUD inertia (peak "+peak+" pixels, "+peakAngle+" degrees), native pause, stop/settle and teleport reset");
                Capture("13-interactive-softened-hud.png");
                if(Global.main.ShowLimbStatus)Global.main.ToggleLimbStatus();
            }))yield break;
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                var group=view.Panel.GetComponent<CanvasGroup>();
                Assert(view.Visibility<=0 && group.alpha<.001f && !group.blocksRaycasts,"HUD stayed visible outside Detail view");
                Assert(!view.TryCycleOutput(1),"Hidden HUD still accepted Output clicks");
                Global.main.ToggleLimbStatus();
            }))yield break;
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                var group=view.Panel.GetComponent<CanvasGroup>();
                Assert(Global.main.ShowLimbStatus && view.Visibility>=1 && group.alpha>.999f,"HUD did not return with Detail view");
                Log("PASS HUD hides and fades back with the native Detail view");
            }))yield break;
        }
    }
}
