using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    public sealed partial class KiCombatQa
    {
        sealed class RiftQaState
        {
            public Vector3 CameraPosition;public float CameraSize,TimeScale,ListenerVolume;
            public readonly Dictionary<Rigidbody2D,float> Gravity=new Dictionary<Rigidbody2D,float>();
            public readonly Dictionary<SpriteRenderer,Color> Colors=new Dictionary<SpriteRenderer,Color>();
            public readonly Dictionary<SpriteRenderer,Material> Materials=new Dictionary<SpriteRenderer,Material>();
            public readonly Dictionary<AudioLowPassFilter,Vector2> Filters=new Dictionary<AudioLowPassFilter,Vector2>();
        }
        IEnumerator VerifyRealityRift()
        {
            GameObject firstRoot=null,secondRoot=null,weakRoot=null,uiRoot=null,ueRoot=null,backdrop=null,unrelatedObject=null;
            GameObject beamFirstRoot=null,beamSecondRoot=null;PersonBehaviour beamFirst=null,beamSecond=null;KiSystem beamFirstKi=null,beamSecondKi=null;
            PersonBehaviour first=null,second=null,weak=null,ui=null,ue=null;
            KiSystem firstKi=null,secondKi=null,weakKi=null,uiKi=null,ueKi=null;
            KiFistHand firstFist=null,secondFist=null;Rigidbody2D firstBody=null,secondBody=null,unrelated=null;
            AudioSource testAudio=null;AudioClip testClip=null;RiftQaState saved=null;
            RealityRift rift=null;Component visual=null;RenderTexture realmTexture=null;
            float firstKiBefore=0,secondKiBefore=0,pausedAge=0,pauseBegan=0,pauseDuration=0,entryReal=0;
            if(!Check(()=>
            {
                foreach(var actor in FindObjectsOfType<PersonBehaviour>())actor.transform.position+=Vector3.left*220;
                DBPU.KiDrainMultiplier=0;DBPU.KiAttackCostMultiplier=1;DBPU.DamageMultiplier=1;
                firstRoot=Spawn("[DBPU] (BM) Gogeta",new Vector3(-2,30,0));
                secondRoot=Spawn("[DBPU] (BM) Gogeta",new Vector3(2,30,0));
                weakRoot=Spawn("[DBPU] (BM) Goku",new Vector3(-12,30,0));
                uiRoot=Spawn("[DBPU] (BM) Goku",new Vector3(-5,30,0));
                ueRoot=Spawn("[DBPU] (BM) Vegeta",new Vector3(5,30,0));
                Global.main.camera.transform.position=new Vector3(0,31,-10);Global.main.camera.orthographicSize=5.5f;
                backdrop=FistVisualBackdrop(new Vector3(0,31,7));
                unrelatedObject=new GameObject("QA unrelated body and silent audio ownership probe");unrelatedObject.transform.position=new Vector3(18,31,0);
                unrelated=unrelatedObject.AddComponent<Rigidbody2D>();unrelated.bodyType=RigidbodyType2D.Kinematic;
                unrelated.gravityScale=.37f;unrelated.drag=.123f;unrelated.angularDrag=.456f;unrelated.velocity=Vector2.right*80;
                testAudio=unrelatedObject.AddComponent<AudioSource>();testAudio.spatialBlend=0;testAudio.loop=true;testAudio.volume=.31f;
                testClip=AudioClip.Create("QA silent playing source for duck/restore",4800,1,48000,false);testAudio.clip=testClip;testAudio.Play();
                Assert(RealityRiftAssets.Ready,"Authored Reality Rift Unity assets are unavailable");
                Log("RIFT PIPELINE "+(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name:"built-in")+" / "+SystemInfo.graphicsDeviceName);
            }))yield break;
            yield return new WaitForSecondsRealtime(1.8f);
            if(!Check(()=>
            {
                Prepare(firstRoot,out first,out firstKi);Prepare(secondRoot,out second,out secondKi);
                Prepare(weakRoot,out weak,out weakKi);Prepare(uiRoot,out ui,out uiKi);Prepare(ueRoot,out ue,out ueKi);
                foreach(var actor in new[]{firstRoot,secondRoot,weakRoot,uiRoot,ueRoot})
                    foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>())renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                foreach(var canvas in FindObjectsOfType<Canvas>())canvas.enabled=false;
                RiftApplyForm(first,"SSJ1");RiftApplyForm(second,"SSJ1");RiftApplyForm(ui,"UI");RiftApplyForm(ue,"UE");
            }))yield break;
            yield return new WaitForSecondsRealtime(3);
            if(!Check(()=>
            {
                Assert(firstKi.CurrentForm=="SSJ1" && secondKi.CurrentForm=="SSJ1" && uiKi.CurrentForm=="UI" && ueKi.CurrentForm=="UE","Native transformation interface did not commit the requested real forms");
                Prepare(firstRoot,out first,out firstKi);Prepare(secondRoot,out second,out secondKi);Prepare(uiRoot,out ui,out uiKi);Prepare(ueRoot,out ue,out ueKi);
                Assert(RealityRift.ReferenceReady,"Native Human prefab reference could not be read: "+RealityRift.ReferenceDiagnostics);
                Log("RIFT REFERENCE "+RealityRift.ReferenceDiagnostics);
                Log("RIFT POWER threshold="+RealityRift.PowerThreshold+" GogetaSSJ1="+RealityRift.EffectivePower(first)+" UI="+RealityRift.EffectivePower(ui)+" UE="+RealityRift.EffectivePower(ue)+" weakBase="+RealityRift.EffectivePower(weak));
                Assert(RealityRift.PowerEligible(first,second),"Two real Gogeta SSJ1 fighters failed their own native threshold");
                Assert(Math.Abs(RealityRift.EffectivePower(first)/RealityRift.PowerThreshold-1)<.005,"Native Gogeta SSJ1 reference does not agree with the actual initialized fighter");
                bool uiUeExpected=RealityRift.EffectivePower(ui)>=RealityRift.PowerThreshold*.99999 && RealityRift.EffectivePower(ue)>=RealityRift.PowerThreshold*.99999;
                Assert(RealityRift.PowerEligible(ui,ue)==uiUeExpected,"UI/UE names overrode the actual configured two-sided power gate");
                Log("RIFT UI/UE configured eligibility="+uiUeExpected+"; native power threshold takes precedence over form names");
                Assert(!RealityRift.PowerEligible(weak,weak) && !RealityRift.PowerEligible(first,weak),"A weak participant bypassed the two-sided power gate");
                Assert(!RealityRift.TryBegin(first,weak,new Vector2(0,31)),"An arbitrary weak-fighter hit opened a rift");
                Assert(!RealityRift.TryBegin(first,second,new Vector2(0,31)),"Power alone opened a rift without a real attack/contact");
                double full=RealityRift.EffectivePower(first);firstKi.currentKi=100;
                Assert(Math.Abs(RealityRift.EffectivePower(first)/full-1)<.00001,"Current Ki reserve incorrectly changed the reality-fracture power gate");firstKi.currentKi=1000;
                var output=CombatPowerController.Bind(first);output.SetOutput(.25f);output.Tick(.1f);
                Assert(!RealityRift.PowerEligible(first,second),"Quarter actual output bypassed the Gogeta SSJ1 threshold");output.SetOutput(1);output.Tick(.1f);
                Assert(RealityRift.PowerEligible(first,second),"Restoring actual output did not recover native eligibility");
                var firstHand=GetHand(first,0);var secondHand=GetHand(second,0);
                Assert(firstHand.Equip(KiHandLoadout.FistId) && secondHand.Equip(KiHandLoadout.FistId),"Cannot equip opposing native Ki Fists");
                firstFist=KiFistHand.GetOrCreate(firstHand.Limb);secondFist=KiFistHand.GetOrCreate(secondHand.Limb);
                firstBody=firstHand.Limb.GetComponent<Rigidbody2D>();secondBody=secondHand.Limb.GetComponent<Rigidbody2D>();
                foreach(var actor in new[]{firstRoot,secondRoot})foreach(var collider in actor.GetComponentsInChildren<Collider2D>())
                    collider.enabled=collider.GetComponent<KiFistHand>()==firstFist || collider.GetComponent<KiFistHand>()==secondFist;
                foreach(var joint in firstBody.GetComponents<Joint2D>())joint.enabled=false;
                foreach(var joint in secondBody.GetComponents<Joint2D>())joint.enabled=false;
                firstBody.gravityScale=secondBody.gravityScale=1;
                RiftPlaceHand(firstFist,new Vector2(-3,31));RiftPlaceHand(secondFist,new Vector2(3,31));
                firstBody.velocity=Vector2.right*4;secondBody.velocity=Vector2.left*4;
            }))yield break;
            yield return new WaitForFixedUpdate();
            if(!Check(()=>
            {
                Assert(firstFist.RealityApproachVelocity.x>1 && secondFist.RealityApproachVelocity.x<-1,"Contact rejection fixture has no actual opposing body motion");
                Assert(!RealityRift.TryFistClash(firstFist,secondFist,new Vector2(0,31)),"Separated moving fists opened a rift");
                RiftPlaceHand(firstFist,new Vector2(-.65f,31));RiftPlaceHand(secondFist,new Vector2(.65f,31));
                firstBody.velocity=secondBody.velocity=Vector2.right*4;
            }))yield break;
            yield return new WaitForFixedUpdate();
            if(!Check(()=>
            {
                Assert(!RealityRift.TryFistClash(firstFist,secondFist,new Vector2(0,31)),"Parallel same-direction fists opened a rift");
                firstBody.bodyType=secondBody.bodyType=RigidbodyType2D.Dynamic;
                firstBody.collisionDetectionMode=secondBody.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                RiftPlaceHand(firstFist,new Vector2(-.4f,31));RiftPlaceHand(secondFist,new Vector2(.4f,31));
                firstBody.velocity=Vector2.right*8;secondBody.velocity=Vector2.left*8;
                firstKi.currentKi=secondKi.currentKi=1000;firstKiBefore=firstKi.currentKi;secondKiBefore=secondKi.currentKi;
                saved=RiftSaveState(first,second);Log("PASS true form power, reserve independence, output, weak-side, no-contact and parallel-motion rejection");
            }))yield break;
            float deadline=Time.realtimeSinceStartup+5;
            while(!RealityRift.Active && Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(()=>
            {
                rift=RealityRift.Active;Assert(rift,"Real opposing 8-unit/s native hand contact did not open a rift");
                entryReal=Time.realtimeSinceStartup-rift.PhaseAge;
                Assert(rift.Phase==RealityRiftPhase.Hitstop,"Actual collision skipped hitstop");
                Near(.05f,Time.timeScale,.0001f,"Clash did not own the exact .05 slow-motion scale");
                Near(firstKiBefore-20,firstKi.currentKi,.1f,"First passive fist was not paid exactly once");
                Near(secondKiBefore-20,secondKi.currentKi,.1f,"Second passive fist was not paid exactly once");
                Assert(!firstFist.IsCharged && !secondFist.IsCharged && !firstFist.DischargeForTest(secondFist.Limb,rift.Centre),"A committed clash reused the same physical strike");
                firstBody.velocity=secondBody.velocity=Vector2.zero;firstBody.bodyType=secondBody.bodyType=RigidbodyType2D.Kinematic;
                visual=RiftVisual(rift);Assert(visual,"Reality fracture has no native visual owner");
                Assert(!rift.RequestExit(first,second,rift.Centre,true),"Exit bypassed the dimension phase/minimum time");
            }))yield break;
            while(rift && rift.Phase==RealityRiftPhase.Hitstop && rift.PhaseAge<.085f)yield return null;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("70-reality-impact-static-cracks.png");
                Assert(RiftVisualValue<bool>(visual,"SceneCaptured"),"The fractured surface did not capture the real scene");
                Assert(RiftVisualValue<bool>(visual,"AudioDucked"),"Hitstop did not apply the configured audio treatment");
                Assert(Global.main.camera.orthographicSize<saved.CameraSize,"Hitstop camera did not zoom toward the actual clash");
                Global.main.TogglePaused();pausedAge=rift.PhaseAge;pauseBegan=Time.realtimeSinceStartup;
            }))yield break;
            yield return new WaitForSecondsRealtime(.17f);
            if(!Check(()=>
            {
                Near(pausedAge,rift.PhaseAge,.0001f,"User pause did not freeze the reality-fracture phase clock");
                Assert(Global.main.Paused && Time.timeScale<.05f,"Reality fracture overrode the native pause ramp");
                Global.main.TogglePaused();pauseDuration+=Time.realtimeSinceStartup-pauseBegan;
            }))yield break;
            while(rift && rift.Phase==RealityRiftPhase.Hitstop && rift.PhaseAge<.24f)yield return null;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("71-reality-transparent-pressure-sphere.png")))yield break;
            while(rift && rift.Phase==RealityRiftPhase.Hitstop)yield return null;
            if(!Check(()=>
            {
                Assert(rift && rift.Phase==RealityRiftPhase.Fracturing,"Hitstop did not proceed to authored surface fracture");
                float elapsed=Time.realtimeSinceStartup-entryReal-pauseDuration;
                Assert(elapsed>=.45f && elapsed<1.5f,"The .5 active-real-second hitstop timer did not complete: "+elapsed);
                Near(saved.TimeScale,Time.timeScale,.0001f,"Hitstop did not restore the prior owned time scale");
                Log("PASS actual two-fist commitment, scene capture, camera zoom, audio duck, user pause and .5 unscaled hitstop (observed "+elapsed+" active seconds)");
            }))yield break;
            while(rift && rift.Phase==RealityRiftPhase.Fracturing && rift.PhaseAge<.20f)yield return null;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("72-reality-animated-blender-shards.png");
                Assert(RiftVisualValue<int>(visual,"RenderedShardCount")>=12,"Imported animated fracture shards are missing");
                foreach(var mesh in RealityRiftAssets.Shards)Assert(mesh && mesh.vertexCount>3 && mesh.bounds.size.z>.001f,"An authored fracture shard has no real mesh depth");
            }))yield break;
            while(rift && rift.Phase!=RealityRiftPhase.Dimension)yield return null;
            if(!Check(()=>
            {
                Assert(rift && rift.PhysicsApplied && rift.ParticipantBodyCount>=20,"Dimension physics did not cover the two native fighters");
                foreach(var pair in saved.Gravity)if(pair.Key)Near(0,pair.Key.gravityScale,.0001f,"A participant retained world gravity");
                RiftAssertUnrelated(unrelated);
                Assert(!rift.RequestExit(first,second,rift.Centre,true),"A final hit exited before two active dimension seconds");
                float edge=Global.main.camera.orthographicSize*Global.main.camera.aspect*.88f;
                firstBody.bodyType=RigidbodyType2D.Dynamic;firstBody.position=rift.Centre+new Vector2(edge+.6f,0);firstBody.velocity=Vector2.right*35;
            }))yield break;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            if(!Check(()=>
            {
                Assert(firstBody.velocity.x<0 && firstBody.velocity.magnitude<=65.01f,"Participant did not bounce from the elastic camera boundary");
                firstBody.bodyType=RigidbodyType2D.Kinematic;firstBody.velocity=Vector2.zero;RiftPlaceHand(firstFist,new Vector2(-1,31));RiftPlaceHand(secondFist,new Vector2(1,31));
                Assert(RiftVisualValue<int>(visual,"RealmMeshCount")>=3,"Dimension contains no imported volumetric environment meshes");
                Assert(RiftVisualValue<int>(visual,"ParticipantRendererCount")>=20,"Dynamic dimension reflection did not include native fighter sprites");
                realmTexture=RiftVisualValue<RenderTexture>(visual,"RealmTexture");Assert(realmTexture && realmTexture.IsCreated(),"3D dimension was not rendered to a live texture");
            }))yield break;
            while(rift && rift.DimensionAge<2.25f)yield return null;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("73-reality-crystalline-dimension.png");
                Assert(RiftVisualValue<float>(visual,"RealmMix")>.7f,"Dimension did not replace the captured world background");
                Assert(saved.Colors.Any(pair=>pair.Key && Vector4.Distance(pair.Key.color,pair.Value)>.02f),"The acid-colour dimension did not reflect dynamically on real participant sprite colours");
                firstKi.currentKi=1000;Assert(firstFist.BeginHeldCharge(),"Final actual strong hit could not charge");
                for(int i=0;i<61;i++)firstFist.ChargeStep(.1f);firstFist.ReleaseHeldCharge();
                Assert(firstFist.IsOvercharged,"Final actual hit was not strongly charged");
                Assert(firstFist.DischargeForTest(secondFist.Limb,secondFist.Limb.transform.position),"Final physical-strike API failed to deliver its paid hit");
                Assert(rift.Phase==RealityRiftPhase.Exiting,"A second strong real hit between the same combatants did not request exit");
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("74-reality-final-hit-exit-flash.png")))yield break;
            while(RealityRift.Active)yield return null;
            yield return null;yield return null;
            if(!Check(()=>
            {
                RiftAssertRestored(saved);RiftAssertUnrelated(unrelated);Near(.31f,testAudio.volume,.002f,"Unrelated playing audio volume was not restored");
                Assert(!realmTexture || !realmTexture.IsCreated(),"Dimension render texture survived its owner");
                Assert(!RiftFindVisual(),"Reality visual component survived the completed rift");
                Log("PASS participant-only gravity/boundary, imported shards/realm, dynamic sprite reflection, strong-hit exit and physics/camera/audio/GPU resource restoration");
                firstRoot.transform.position+=Vector3.left*180;secondRoot.transform.position+=Vector3.left*180;weakRoot.transform.position+=Vector3.left*180;
                uiRoot.transform.position+=Vector3.left*180;ueRoot.transform.position+=Vector3.left*180;
                Global.main.camera.transform.position=new Vector3(0,31,-10);Global.main.camera.orthographicSize=6.5f;
                beamFirstRoot=Spawn("[DBPU] (BM) Gogeta",new Vector3(-5,30,0));beamSecondRoot=Spawn("[DBPU] (BM) Gogeta",new Vector3(5,30,0));
            }))yield break;
            yield return new WaitForSecondsRealtime(1.8f);
            if(!Check(()=>
            {
                Prepare(beamFirstRoot,out beamFirst,out beamFirstKi);Prepare(beamSecondRoot,out beamSecond,out beamSecondKi);
                RiftApplyForm(beamFirst,"SSJ1");RiftApplyForm(beamSecond,"SSJ1");
                foreach(var actor in new[]{beamFirstRoot,beamSecondRoot})foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>())renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
            }))yield break;
            yield return new WaitForSecondsRealtime(3);
            yield return RiftStartNativeBeamClash(beamFirst,beamSecond);
            if(finished)yield break;
            if(!Check(()=>
            {
                rift=RealityRift.Active;Assert(rift,"Native Gogeta SSJ1 Energy Wave tip clash did not open a rift");
                Assert((rift.Attacker==beamFirst && rift.Defender==beamSecond)||(rift.Attacker==beamSecond && rift.Defender==beamFirst),"Beam clash chose unrelated combatants");
                saved=RiftSaveState(beamFirst,beamSecond,false);saved.TimeScale=1;
                RiftCancelBeams(beamFirst,beamSecond);Log("PASS native Energy Wave prefabs, tip colliders and BeamComponent.BeamClash integration at the exact real Gogeta SSJ1 threshold");
            }))yield break;
            while(rift && rift.Phase!=RealityRiftPhase.Dimension)yield return null;
            while(rift && rift.Phase==RealityRiftPhase.Dimension && rift.DimensionAge<17.8f)
            {
                if(!Check(()=>RiftAssertUnrelated(unrelated)))yield break;
                yield return new WaitForSecondsRealtime(.5f);
            }
            if(!Check(()=>Assert(rift && rift.DimensionAge>=17.75f,"Dimension timer ended before its eighteen active seconds")))yield break;
            while(RealityRift.Active)yield return null;
            yield return null;yield return null;
            if(!Check(()=>
            {
                foreach(var actor in new[]{beamFirst,beamSecond})foreach(var limb in actor.Limbs)Near(1,limb.GetComponent<Rigidbody2D>().gravityScale,.0001f,"Timer exit left participant gravity changed");
                Near(1,Time.timeScale,.0001f,"Timer exit left slow motion active");RiftAssertUnrelated(unrelated);
                Assert(!RiftFindVisual(),"Timer exit left the dimension renderer active");
                Log("PASS full eighteen-active-second dimension timeout and automatic cleanup");
            }))yield break;
            yield return new WaitForSecondsRealtime(3.2f);
            yield return RiftStartNativeBeamClash(beamFirst,beamSecond);
            if(finished)yield break;
            if(!Check(()=>{rift=RealityRift.Active;Assert(rift,"Cancellation fixture did not create a real clash");RiftCancelBeams(beamFirst,beamSecond);}))yield break;
            while(rift && rift.Phase!=RealityRiftPhase.Dimension)yield return null;
            bool pauseAudio=false;
            if(!Check(()=>
            {
                Global.main.TogglePaused();pauseAudio=AudioListener.pause;
                Destroy(beamFirstRoot);
            }))yield break;
            yield return null;yield return null;yield return null;
            float pauseSettleDeadline=Time.realtimeSinceStartup+1;
            while(Global.main.Paused && Time.timeScale>.0001f && Time.realtimeSinceStartup<pauseSettleDeadline)yield return null;
            if(!Check(()=>
            {
                Assert(!RealityRift.Active && Global.main.Paused,"Destroying a fighter failed cleanup or unpaused the user");
                Near(0,Time.timeScale,.0001f,"Cancellation unpaused a user's zero time scale");
                Assert(AudioListener.pause==pauseAudio,"Cancellation overwrote the user's audio pause");
                foreach(var limb in beamSecond.Limbs)Near(1,limb.GetComponent<Rigidbody2D>().gravityScale,.0001f,"Cancellation failed to restore the surviving fighter");
                Assert(!RiftFindVisual(),"Cancellation left a scene compositor or dimension geometry alive");
                Global.main.TogglePaused();
                Log("PASS destroyed-fighter cancellation while paused, surviving physics, pause/audio ownership and renderer cleanup");
                if(backdrop){var material=backdrop.GetComponent<MeshRenderer>().sharedMaterial;Destroy(material.mainTexture);Destroy(material);Destroy(backdrop.GetComponent<MeshFilter>().sharedMesh);Destroy(backdrop);}
                foreach(var root in new[]{firstRoot,secondRoot,weakRoot,uiRoot,ueRoot,beamFirstRoot,beamSecondRoot,unrelatedObject})if(root)Destroy(root);
                if(testClip)Destroy(testClip);
            }))yield break;
            float resumeDeadline=Time.realtimeSinceStartup+1;
            while(Time.timeScale<.99f && Time.realtimeSinceStartup<resumeDeadline)yield return null;
            if(!Check(()=>
            {
                Assert(Time.timeScale>.99f,"Resuming after paused cancellation retained the cinematic slow-motion target");
                Log("ALL REALITY RIFT NATIVE CLASH, POWER, PHASE, PHYSICS, VISUAL AND RESTORATION CHECKS PASSED");
            }))yield break;
        }

        void RiftApplyForm(PersonBehaviour person,string form)
        {
            var menu=person.GetComponentInChildren<TransformationContextMenu>();Assert(menu,"Native form menu missing");
            Assert(menu.TransformationsData.transformations.TransformationData.ContainsKey(form),"Native form is absent: "+form);
            menu.ApplyForm(form);
        }
        void RiftPlaceHand(KiFistHand hand,Vector2 centre)
        {
            var collider=hand.GetComponent<Collider2D>();var body=hand.GetComponent<Rigidbody2D>();
            body.position+=centre-(Vector2)collider.bounds.center;body.angularVelocity=0;Physics2D.SyncTransforms();
        }
        RiftQaState RiftSaveState(PersonBehaviour a,PersonBehaviour b,bool camera=true)
        {
            var result=new RiftQaState{CameraPosition=Global.main.camera.transform.position,CameraSize=Global.main.camera.orthographicSize,TimeScale=Time.timeScale,ListenerVolume=AudioListener.volume};
            foreach(var person in new[]{a,b})foreach(var limb in person.Limbs)
            {
                var body=limb.GetComponent<Rigidbody2D>();if(body)result.Gravity[body]=body.gravityScale;
                var renderer=limb.GetComponent<SpriteRenderer>();if(renderer){result.Colors[renderer]=renderer.color;result.Materials[renderer]=renderer.sharedMaterial;}
            }
            foreach(var filter in FindObjectsOfType<AudioLowPassFilter>())result.Filters[filter]=new Vector2(filter.enabled?1:0,filter.cutoffFrequency);
            return result;
        }
        void RiftAssertRestored(RiftQaState saved)
        {
            foreach(var pair in saved.Gravity)if(pair.Key)Near(pair.Value,pair.Key.gravityScale,.0001f,"Participant gravity was not restored");
            foreach(var pair in saved.Colors)if(pair.Key)Assert(Vector4.Distance(pair.Key.color,pair.Value)<.005f,"Participant reflection tint was not restored");
            foreach(var pair in saved.Materials)if(pair.Key)Assert(pair.Key.sharedMaterial==pair.Value,"Participant sprite material was not restored");
            foreach(var pair in saved.Filters)if(pair.Key){Assert(pair.Key.enabled==(pair.Value.x>0),"Existing audio filter enable state was not restored");Near(pair.Value.y,pair.Key.cutoffFrequency,.1f,"Existing audio filter cutoff was not restored");}
            Assert(FindObjectsOfType<AudioLowPassFilter>().Length==saved.Filters.Count,"A temporary cinematic low-pass filter survived cleanup");
            Near(saved.TimeScale,Time.timeScale,.0001f,"Prior time scale was not restored");Near(saved.ListenerVolume,AudioListener.volume,.001f,"Listener volume was not restored");
            Near(saved.CameraSize,Global.main.camera.orthographicSize,.001f,"Camera zoom was not restored");
            Assert(Vector3.Distance(saved.CameraPosition,Global.main.camera.transform.position)<.01f,"Camera position was not restored");
        }
        void RiftAssertUnrelated(Rigidbody2D body)
        {
            Assert(body && body.bodyType==RigidbodyType2D.Kinematic,"Rift modified an unrelated body type");
            Near(.37f,body.gravityScale,.00001f,"Rift modified unrelated gravity");Near(.123f,body.drag,.00001f,"Rift modified unrelated drag");
            Near(.456f,body.angularDrag,.00001f,"Rift modified unrelated angular drag");Near(80,body.velocity.x,.001f,"Rift clamped an unrelated body's velocity");
        }
        Component RiftFindVisual()
        {
            return FindObjectsOfType<MonoBehaviour>().FirstOrDefault(component=>component && component.GetType().Name=="RealityRiftVisual");
        }
        Component RiftVisual(RealityRift owner)
        {
            return owner.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(component=>component && component.GetType().Name=="RealityRiftVisual") ?? RiftFindVisual();
        }
        T RiftVisualValue<T>(Component visual,string name)
        {
            Assert(visual,"Reality visual disappeared before property "+name);
            var property=visual.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            Assert(property!=null,"Reality visual verification property is absent: "+name);
            return (T)property.GetValue(visual,null);
        }
        IEnumerator RiftStartNativeBeamClash(PersonBehaviour first,PersonBehaviour second)
        {
            if(!Check(()=>
            {
                foreach(var actor in new[]{first,second})
                {
                    var energy=actor.GetComponent<KiSystem>();energy.currentKi=energy.maxKi=1000;energy.baseRegen=0;
                    foreach(var body in actor.GetComponentsInChildren<Rigidbody2D>()){body.bodyType=RigidbodyType2D.Kinematic;body.velocity=Vector2.zero;body.gravityScale=1;}
                    var loadout=GetHand(actor,0);loadout.Equip(KiHandLoadout.NoneId);
                    var arm=loadout.Limb.transform;arm.rotation=Quaternion.Euler(0,0,actor==first?90:-90);
                    var beam=actor.GetComponent<BeamBehaviour>();beam.CancelBeamState();beam.EffectID="Energy Wave";beam.BeamType="ArmBeam";beam.Charge=beam.MaxCharge;
                    var release=typeof(BeamBehaviour).GetMethod("Release",BindingFlags.NonPublic|BindingFlags.Instance);
                    Assert(release!=null,"Native Energy Wave release method is missing");
                    beam.StartCoroutine((IEnumerator)release.Invoke(beam,new object[]{arm,beam.BeamAudio}));
                }
            }))yield break;
            float deadline=Time.realtimeSinceStartup+6;
            while(!RealityRift.Active && Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(()=>
            {
                Assert(RealityRift.Active,"Opposing native Energy Wave tip colliders did not create a reality fracture");
                foreach(var person in new[]{first,second})
                {
                    var beam=person.GetComponent<BeamBehaviour>();Assert(beam.StillActive && beam.EndBeam && beam.MiddleBeam,"Beam integration fixture did not use native rendered Energy Wave prefabs");
                    var component=beam.EndBeam.GetComponent<BeamComponent>();Assert(component && beam.Clashing,"Native beam-clash component did not enter its real clash state");
                    component.StopAllCoroutines();
                }
            }))yield break;
        }
        void RiftCancelBeams(PersonBehaviour a,PersonBehaviour b)
        {foreach(var person in new[]{a,b})if(person){var beam=person.GetComponent<BeamBehaviour>();if(beam)beam.CancelBeamState();}}
    }
}
