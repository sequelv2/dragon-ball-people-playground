using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    // Runs only in the explicitly requested disposable native QA process.
    public sealed partial class KiCombatQa
    {
        IEnumerator VerifyKiFistModes()
        {
            GameObject fighterRoot=null,victimRoot=null;PersonBehaviour fighter=null,victim=null;
            KiSystem energy=null,victimEnergy=null;KiHandLoadout loadout=null;KiFistHand fist=null;
            AllyKiReservoir received=null;LimbBehaviour probe=null;CombatPowerController power=null;
            float normalDamage=0,godDamage=0,fullDamage=0,before=0;string identityBefore=null;
            if(!Check(()=>
            {
                foreach(var actor in FindObjectsOfType<PersonBehaviour>())actor.transform.position+=new Vector3(-180,0,0);
                DBPU.KiDrainMultiplier=0;DBPU.KiAttackCostMultiplier=1;DBPU.DamageMultiplier=1;
                fighterRoot=Spawn("[DBPU] (BM) Goku",new Vector3(-2,30,0));
                victimRoot=Spawn("[DBPU] (BM) Goku",new Vector3(2.5f,30,0));
                Global.main.camera.transform.position=new Vector3(.1f,31,-10);Global.main.camera.orthographicSize=3.5f;
            }))yield break;
            yield return new WaitForSecondsRealtime(1.8f);
            if(!Check(()=>
            {
                Prepare(fighterRoot,out fighter,out energy);Prepare(victimRoot,out victim,out victimEnergy);
                foreach(var actor in new[]{fighterRoot,victimRoot})foreach(var r in actor.GetComponentsInChildren<SpriteRenderer>())r.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                foreach(var canvas in FindObjectsOfType<Canvas>())if(canvas.name!="DBPU Combat HUD")canvas.enabled=false;
                received=AllyKiReservoir.GetOrCreate(fighter);received.ConsumeSupport(received.TotalReceived);
                loadout=GetHand(fighter,0);Assert(loadout.Equip(KiHandLoadout.FistId),"Cannot equip unified Ki Fist");
                fist=KiFistHand.GetOrCreate(loadout.Limb);power=CombatPowerController.Bind(fighter);power.SetOutput(1);power.Tick(.1f);
                probe=victim.Limbs.First(l=>l.name.Contains("LowerArm"));
                var entries=loadout.GetEntries();Assert(entries.Count(e=>e.Id==KiHandLoadout.FistId)==1,"Ki Fist wheel entry duplicated");
                Assert(entries.All(e=>e.Id!=KiHandLoadout.InfusionId),"Separate All Energy attack remains in the wheel");
                energy.CurrentForm="Base";Assert(!fist.GodModeAvailable && !fist.SetMode(KiFistMode.God),"Base form illegally enabled God Fist");
                energy.CurrentForm="SSJ1";Assert(!fist.GodModeAvailable,"SSJ1 illegally enabled God Fist");
                foreach(string form in new[]{"SSJGod","SSJBlue","PSSJBlue","SSJBlueEvolution","UI","MUI"})
                {energy.CurrentForm=form;Assert(fist.GodModeAvailable,"Missing divine form eligibility: "+form);}
                foreach(string form in new[]{"SSJ1","SSJ2","SSJ3","UE","Golden","Beast"})
                {energy.CurrentForm=form;Assert(!fist.GodModeAvailable,"Unexpected divine form eligibility: "+form);}
                var identity=fighter.GetComponent<DbpuCharacterIdentity>();Assert(identity,"Native character identity missing");identityBefore=identity.CharacterId;
                energy.CurrentForm="Base";
                foreach(string id in new[]{"BM/Beerus","BM/Whis","DBSSH/Beerus","DBSSH/Whis"})
                {identity.CharacterId=id;Assert(fist.GodModeAvailable,"Divine character unavailable: "+id);}
                identity.CharacterId=identityBefore;
                Assert(fist.SetMode(KiFistMode.Ki),"Cannot return to ordinary mode");energy.currentKi=1000;
                Assert(fist.IsReady && !fist.IsCharged,"Freshly equipped Ki Fist cannot strike without charging");
                ResetFistProbe(probe,fighter);float kiBefore=energy.currentKi,healthBefore=probe.Health;
                Assert(fist.DischargeForTest(probe,probe.transform.position),"Ordinary ready swing required a precharge");
                Assert(probe.Health<healthBefore && energy.currentKi<kiBefore,"Ordinary ready strike did not deal damage/pay ki");
                Assert(!fist.DischargeForTest(probe,probe.transform.position),"One physical swing discharged twice");
                Log("PASS unified wheel, divine form/character whitelist, passive stock strike, real ki cost and single discharge");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            Rigidbody2D movingHand=null;Vector2 handPosition=Vector2.zero;float handRotation=0,contactHealth=0;
            Joint2D[] handJoints=null;bool[] jointStates=null;
            Collider2D[] enemyColliders=null;bool[] enemyCollisionStates=null;
            if(!Check(()=>
            {
                energy.currentKi=1000;fist.Cancel();fist.SetMode(KiFistMode.Ki);ResetFistProbe(probe,fighter);contactHealth=probe.Health;
                movingHand=loadout.Limb.GetComponent<Rigidbody2D>();Assert(movingHand,"Native hand rigidbody missing");
                handPosition=movingHand.position;handRotation=movingHand.rotation;
                handJoints=loadout.Limb.GetComponents<Joint2D>();jointStates=handJoints.Select(j=>j.enabled).ToArray();
                foreach(var joint in handJoints)joint.enabled=false;
                enemyColliders=victim.GetComponentsInChildren<Collider2D>();enemyCollisionStates=enemyColliders.Select(c=>c.enabled).ToArray();
                foreach(var collider in enemyColliders)if(collider.GetComponent<LimbBehaviour>()!=probe)collider.enabled=false;
                var handCollider=loadout.Limb.GetComponent<Collider2D>();Assert(handCollider,"Native hand collider missing");
                foreach(var collider in fighter.GetComponentsInChildren<Collider2D>())if(collider!=handCollider)Physics2D.IgnoreCollision(handCollider,collider,true);
                movingHand.position=(Vector2)probe.transform.position+Vector2.left;
                movingHand.rotation=90;movingHand.gravityScale=0;movingHand.bodyType=RigidbodyType2D.Dynamic;
                movingHand.collisionDetectionMode=CollisionDetectionMode2D.Continuous;movingHand.velocity=Vector2.right*15;
                Physics2D.SyncTransforms();
            }))yield break;
            yield return new WaitForSeconds(.1f);
            if(!Check(()=>
            {
                Assert(probe.Health<contactHealth,"A real moving hand did not discharge Ki Fist on contact");
                Near(980,energy.currentKi,.05f,"Physical contact failed to charge exactly one passive Ki Fist cost");
                movingHand.velocity=Vector2.zero;movingHand.angularVelocity=0;movingHand.bodyType=RigidbodyType2D.Kinematic;before=energy.currentKi;
            }))yield break;
            yield return new WaitForSeconds(.2f);
            if(!Check(()=>
            {
                Near(before,energy.currentKi,.05f,"Resting contact spent Ki Fist repeatedly");
                movingHand.bodyType=RigidbodyType2D.Kinematic;movingHand.position=handPosition;movingHand.rotation=handRotation;
                for(int i=0;i<handJoints.Length;i++)if(handJoints[i])handJoints[i].enabled=jointStates[i];
                for(int i=0;i<enemyColliders.Length;i++)if(enemyColliders[i])enemyColliders[i].enabled=enemyCollisionStates[i];
                Physics2D.SyncTransforms();Log("PASS real 15-unit/s native hand contact, one passive charge and no resting-contact repeats");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                Assert(fist.IsReady,"Ordinary Ki Fist did not recover after a hit");energy.currentKi=1000;
                Assert(fist.BeginHeldCharge() && fist.IsHeldCharging,"R hold could not start a captured-limb charge");
                for(int i=0;i<61;i++)fist.ChargeStep(.1f);
                fist.ReleaseHeldCharge();Assert(fist.IsCharged && fist.IsOvercharged,"Own-ki full overcharge still requires a donor");
                Assert(energy.currentKi<880 && received.TotalReceived<.001f,"Own-ki charge did not pay its cost or created donation energy");
                ShowFistForQa(fist);
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("30-fist-white-overcharge.png");Assert(fist.GetComponentInChildren<KiStrikeLimbVfx>(true),"Continuous charged-limb VFX missing");
                foreach(var r in fist.GetComponentsInChildren<MeshRenderer>(true))Assert(r.sharedMaterial && r.sharedMaterial.shader.isSupported,"Charged limb shader fallback");
                before=energy.currentKi;Global.main.TogglePaused();
            }))yield break;
            yield return null;yield return null;
            if(!Check(()=>
            {
                Assert(fist.IsCharged,"Pause lost a paid prepared strike");Near(before,energy.currentKi,.001f,"Pause spent prepared-fist energy");
                Global.main.TogglePaused();fist.Cancel();energy.currentKi=1000;
                Log("PASS own-ki overcharge, continuous limb VFX and paused prepared energy");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                energy.CurrentForm="Base";energy.currentKi=1000;fist.SetMode(KiFistMode.Ki);ModesCharge(fist,30);
                ResetFistProbe(probe,fighter);float healthBefore=probe.Health;
                Assert(fist.DischargeForTest(probe,probe.transform.position),"Normal full charge failed");normalDamage=healthBefore-probe.Health;
                Assert(Finite(normalDamage) && normalDamage>0,"Normal fist damage invalid");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                energy.CurrentForm="SSJGod";energy.currentKi=1000;Assert(fist.SetMode(KiFistMode.God),"God form could not select God Fist");ModesCharge(fist,30);
                ResetFistProbe(probe,fighter);float healthBefore=probe.Health;
                Assert(fist.DischargeForTest(probe,probe.transform.position),"God Fist did not hit");godDamage=healthBefore-probe.Health;
                Assert(Finite(godDamage) && godDamage>normalDamage,"God Fist did not improve the same charged strike");
                Log("PASS ordinary/God damage is finite and God mode strengthens the same strike");
            }))yield break;
            yield return new WaitForSeconds(.06f);yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("31-fist-god-impact-rings.png")))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                energy.CurrentForm="Base";energy.currentKi=1000;fist.SetMode(KiFistMode.Ki);power.SetOutput(1);power.Tick(.1f);ModesCharge(fist,30);
                ResetFistProbe(probe,fighter);float healthBefore=probe.Health;Assert(fist.DischargeForTest(probe,probe.transform.position),"Full Output probe failed");fullDamage=healthBefore-probe.Health;
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                energy.currentKi=1000;power.SetOutput(.25f);power.Tick(.1f);ModesCharge(fist,30);
                ResetFistProbe(probe,fighter);float healthBefore=probe.Health;Assert(fist.DischargeForTest(probe,probe.transform.position),"Quarter Output probe failed");
                Near(fullDamage*.25f,healthBefore-probe.Health,Mathf.Max(1,fullDamage*.001f),"Ki Fist applied Output more than once");
                power.SetOutput(1);power.Tick(.1f);Log("PASS actual Ki Fist damage scales with Output once");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                energy.currentKi=1000;received.ReturnSupport(200);float stored=received.TotalReceived;
                Assert(loadout.Equip(KiHandLoadout.InfusionId),"Legacy All Energy ID did not migrate");
                Assert(loadout.SelectedId==KiHandLoadout.FistId && fist.Mode==KiFistMode.Infusion,"Legacy scene retained a separate attack");
                ModesCharge(fist,30);Near(stored,fist.InfusedEnergy,.02f,"Infusion did not reserve the received energy once");
                Near(0,received.TotalReceived,.02f,"Infusion duplicated reserved energy");
                fist.Cancel();Near(stored,received.TotalReceived,.02f,"Cancelling a mode lost reserved energy");
                energy.currentKi=1000;ModesCharge(fist,30);ResetFistProbe(probe,fighter);
                Assert(fist.DischargeForTest(probe,probe.transform.position),"Infusion did not discharge");
                Near(0,received.TotalReceived,.02f,"Successful hit refunded consumed infusion");
                Assert(victim.GetComponent<KiInternalDisruption>(),"Infusion did not apply internal disruption");
                Assert(!fist.DischargeForTest(probe,probe.transform.position),"Infusion struck twice");
                var disruption=victim.GetComponent<KiInternalDisruption>();if(disruption)Destroy(disruption);
                Log("PASS legacy migration, one-time infusion reservation, cancel refund, actual hit consumption and internal damage");
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                var foot=fighter.Limbs.First(KiHandLoadout.IsFoot);var footLoadout=KiHandLoadout.GetOrCreate(foot);
                Assert(footLoadout.Equip(KiHandLoadout.FistId),"Unified Ki Fist cannot target a chosen foot");
                var kick=KiFistHand.GetOrCreate(foot);energy.CurrentForm="SSJBlue";energy.currentKi=1000;
                Assert(kick.GodModeAvailable && kick.SetMode(KiFistMode.God),"God Fist unavailable on selected foot");ModesCharge(kick,30);
                ResetFistProbe(probe,fighter);Assert(kick.DischargeForTest(probe,probe.transform.position),"Prepared God kick did not strike");
                Assert(loadout.SelectedId==KiHandLoadout.FistId,"Foot mode changed the other hand loadout");footLoadout.Equip(KiHandLoadout.NoneId);
                energy.currentKi=1000;fist.SetMode(KiFistMode.God);ModesCharge(fist,30);energy.CurrentForm="Base";
            }))yield break;
            yield return null;yield return null;
            if(!Check(()=>
            {
                Assert(fist.Mode!=KiFistMode.God && !fist.IsCharged,"Leaving divine form retained a prepared God Fist");
                Log("PASS explicitly selected foot shares the new architecture; losing divine form cancels God mode/preparation");
                energy.currentKi=1000;var menu=fighter.GetComponentInChildren<TransformationContextMenu>();
                Assert(menu,"Native transformation menu missing");menu.ApplyForm("SSJGod");
            }))yield break;
            yield return new WaitForSeconds(3);
            if(!Check(()=>
            {
                Assert(energy.CurrentForm=="SSJGod","Native God transformation did not commit");
                energy.currentKi=1000;fist.SetMode(KiFistMode.God);ModesCharge(fist,61);ShowFistForQa(fist);
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("32-fist-god-overcharge.png");ResetFistProbe(probe,fighter);
                Assert(fist.DischargeForTest(probe,probe.transform.position),"Fully charged native God Fist did not hit");
            }))yield break;
            yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("33-fist-full-god-impact.png");fist.Cancel();loadout.Equip(KiHandLoadout.NoneId);energy.CurrentForm="Base";
                Assert(!fist.IsCharged && !fist.IsCharging,"Unequip retained a prepared fist");
                Log("ALL KI FIST MODE CHECKS PASSED");
            }))yield break;
        }
        void ModesCharge(KiFistHand fist,int steps)
        {
            Assert(fist.BeginHeldCharge(),"Captured limb refused a held charge");
            for(int i=0;i<steps;i++)fist.ChargeStep(.1f);
            fist.ReleaseHeldCharge();Assert(fist.IsCharged,"Releasing held R did not retain its paid strike");
        }
        void ResetFistProbe(LimbBehaviour probe,PersonBehaviour fighter)
        {
            probe.InitialHealth=KiCombatMath.Cap((double)KiCombatMath.Health(fighter)*20,1e35f);probe.Health=probe.InitialHealth;
        }
        void ShowFistForQa(KiFistHand fist)
        {
            var method=typeof(KiFistHand).GetMethod("ShowVisual",BindingFlags.NonPublic|BindingFlags.Instance);
            Assert(method!=null,"Limb visual entry point missing");method.Invoke(fist,null);
        }

    }
}
