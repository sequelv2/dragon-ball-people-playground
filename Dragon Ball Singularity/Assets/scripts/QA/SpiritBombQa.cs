using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBPU
{
    public sealed partial class KiCombatQa
    {
        IEnumerator VerifySpiritBomb()
        {
            SpiritBombChannel channel = null;
            SpiritBombProjectile bomb = null;
            KiShareHand sharing = null;
            float shortRadius = 0, shortEnergy = 0, beforeDonation = 0, donated = 0;
            GameObject victimRoot = null, wall = null;
            PersonBehaviour victim = null;
            LimbBehaviour chest = null;
            if (!Check(() =>
            {
                // The preceding regression uses Gogito, whose actual native loadout
                // intentionally has no Genkidama. Exercise Goku's real power setup.
                subject.transform.position = new Vector3(-45,30,0);
                opponent.transform.position = new Vector3(-50,30,0);
                subject = Spawn("[DBPU] (BM) Goku", new Vector3(0,30,0));
            })) yield break;
            yield return new WaitForSecondsRealtime(1.5f);
            if (!Check(() =>
            {
                Prepare(subject, out person, out ki);
                hand = GetHand(person,0); otherHand = GetHand(person,1);
                support = AllyKiReservoir.GetOrCreate(person);
                foreach(var renderer in subject.GetComponentsInChildren<SpriteRenderer>())
                    renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                Assert(SpiritBombVfx.HasBakedPlasma, "The restored reference plasma atlas did not load");
                hand.Equip(KiHandLoadout.NoneId);
                support.ConsumeSupport(support.TotalReceived); ki.currentKi = 1000;
                foreach (var limb in person.Limbs) limb.Health = limb.InitialHealth;
                Assert(hand.Equip("SpiritBomb"), "Native fighter does not expose Spirit Bomb");
                Assert(hand.GetEntries().Count(e => SpiritBombChannel.IsSpirit(e.Id)) == 1, "Hand menu shows two fixed-size variants");
                channel = SpiritBombChannel.GetOrCreate(person); channel.enabled = false;
                Assert(channel.BeginCharge(hand.Limb), "Spirit Bomb charge did not start");
                for (int i=0;i<50;i++) channel.ChargeStep(.1f);
                shortRadius = channel.ChargingBomb.Radius; shortEnergy = channel.ChargingBomb.Energy;
                Near(90, shortEnergy, .01f, "Five-second charge energy"); Near(910, ki.currentKi, .03f, "Five-second charge expense");
                channel.CancelCharge(); ki.currentKi = 1000;
                Assert(hand.Equip("SuperSpiritBomb"), "Legacy Super Spirit Bomb alias missing");
                Assert(channel.BeginCharge(hand.Limb), "Legacy alias failed to charge");
                for (int i=0;i<50;i++) channel.ChargeStep(.1f);
                Near(shortRadius, channel.ChargingBomb.Radius, .001f, "Technique selection still determines size");
                Near(shortEnergy, channel.ChargingBomb.Energy, .01f, "Legacy alias has a different charge curve");
                channel.CancelCharge(); ki.currentKi=1000;
                Assert(hand.Equip("SpiritBomb") && channel.BeginCharge(hand.Limb), "Main charge failed to restart");
                for(int i=0;i<100;i++) channel.ChargeStep(.1f);
                bomb=channel.ChargingBomb;
                Assert(bomb.Radius>shortRadius,"Longer creation did not increase size");
                beforeDonation=bomb.Energy;
                Assert(donorHand.Equip(KiHandLoadout.ShareId),"Donor cannot select Ki Share");
                sharing=KiShareHand.GetOrCreate(donorHand.Limb); donorKi.currentKi=1000;
                Global.main.camera.transform.position=new Vector3(0,bomb.transform.position.y-2,-10);
                Global.main.camera.orthographicSize=11;
                Log("PASS Unity volume shader, continuous time/expense growth, identical Spirit/Super aliases and single wheel entry");
            })) yield break;
            yield return null;
            for(int i=0;i<55;i++)
            {
                if(!Check(()=>
                {
                    Vector2 aim=(Vector2)bomb.transform.position-KiCombatMath.HandPoint(donorHand.Limb);
                    donorHand.Limb.transform.rotation=Quaternion.FromToRotation(-Vector3.up,aim.normalized);
                    Physics2D.SyncTransforms();
                    float before=donorKi.currentKi, energy=bomb.Energy;
                    float sent=sharing.TransferStep(.1f); donated+=sent;
                    Assert(sent>0 && sharing.BombReceiver==bomb,"Directed Ki Share did not target the charge sphere");
                    Near(before-sent,donorKi.currentKi,.002f,"Sphere donation did not debit donor exactly once");
                    Near(energy+sent,bomb.Energy,.003f,"Sphere donation created/lost Ki");
                    Near(0,donorSupport.DonateToBomb(bomb,.1f),0,"Two transfers exceeded one frame's budget");
                }))yield break;
                yield return null;
            }
            if(!Check(()=>
            {
                Near(beforeDonation+donated,bomb.Energy,.03f,"Accumulated sphere transfer conservation");
                Assert(bomb.Radius>shortRadius*2,"Shared energy did not substantially enlarge the sphere");
                float ordinary=support.TotalReceived, source=donorKi.currentKi, energy=bomb.Energy;
                float redirected=donorSupport.DonateTo(support,.1f);
                Assert(redirected>0,"Sharing with caster did not feed their gathering sphere");
                Near(ordinary,support.TotalReceived,.01f,"Caster and sphere both received the same donation");
                Near(energy+redirected,bomb.Energy,.01f,"Caster donation was not routed to bomb");
                Near(source-redirected,donorKi.currentKi,.01f,"Redirected donation conservation");
                Log("PASS actual Ki Share ray targeting, visible growth, conservation, caster redirect and per-frame donor budget; shared="+bomb.DonatedEnergy);
            }))yield break;
            yield return null;
            if(!Check(()=>
            {
                Vector2 start=KiCombatMath.HandPoint(donorHand.Limb),aim=((Vector2)bomb.transform.position-start).normalized;
                wall=new GameObject("QA solid Ki blocker");wall.transform.position=start+aim*.7f;
                wall.AddComponent<BoxCollider2D>().size=Vector2.one*.4f;Physics2D.SyncTransforms();
                float before=donorKi.currentKi;
                Near(0,sharing.TransferStep(.1f),0,"Ki stream passed through a solid obstruction");
                Near(before,donorKi.currentKi,0,"Blocked stream spent Ki");
                Destroy(wall);sharing.Cancel();
            }))yield break;
            yield return new WaitForSeconds(.35f);
            if(!Check(()=>
            {
                // Frame the final shared size, not the smaller position before donations.
                Global.main.camera.transform.position=bomb.transform.position+new Vector3(0,-1.8f,-10);
                Global.main.camera.orthographicSize=bomb.Radius+4;
            }))yield break;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("05-spirit-gathering.png")))yield break;
            Vector3 frozenPosition=Vector3.zero; float frozenEnergy=0, frozenRadius=0, frozenKi=0;
            if(!Check(()=>
            {
                Assert(channel.FreezeCharge(),"Release did not prepare a stationary sphere");
                Assert(channel.IsReady && bomb.Phase==SpiritBombPhase.Ready,"Release launched the sphere");
                frozenPosition=bomb.transform.position;frozenEnergy=bomb.Energy;frozenRadius=bomb.Radius;frozenKi=ki.currentKi;
                subject.transform.position+=Vector3.right*2;
                Near(0,channel.ChargeStep(.1f),0,"Ready sphere continued charging");
                float source=donorKi.currentKi;
                Near(0,donorSupport.DonateToBomb(bomb,.1f),0,"Ready sphere accepted more energy");
                Near(source,donorKi.currentKi,0,"Rejected ready donation spent Ki");
                Assert(!SpiritBombTargeting.TryClickWorld(new Vector2(900,900)),"Empty-space click launched sphere");
                Physics2D.SyncTransforms();
                Assert(!SpiritBombTargeting.TryClickWorld(hand.Limb.transform.position),"Self click launched sphere");
            }))yield break;
            yield return new WaitForSeconds(.35f);
            if(!Check(()=>
            {
                Near(0,Vector3.Distance(frozenPosition,bomb.transform.position),.001f,"Ready sphere followed its moving caster");
                Near(frozenEnergy,bomb.Energy,0,"Ready energy changed");Near(frozenRadius,bomb.Radius,0,"Ready size changed");
                Near(frozenKi,ki.currentKi,.001f,"Waiting sphere spent caster Ki");
                Assert(channel.IsReady,"Empty/self click consumed prepared sphere");
                Log("PASS release holds sphere in world space; fixed energy/size, no idle cost/donation; empty/self clicks preserve it");
            }))yield break;
            yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("05b-spirit-ready.png")))yield break;
            float healthBefore=0, distanceBefore=0;
            if(!Check(()=>
            {
                Vector3 position=bomb.transform.position+Vector3.right*(bomb.Radius+2.5f)-Vector3.up*.5f;
                victimRoot=Spawn("Human",position);victim=victimRoot.GetComponent<PersonBehaviour>();
                Assert(victim && victim.IsAlive(),"Native containment victim failed to spawn");
                chest=victim.Limbs.First(l=>l.name=="UpperBody");
                foreach(var r in victimRoot.GetComponentsInChildren<SpriteRenderer>())r.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                Physics2D.SyncTransforms();
                Assert(SpiritBombTargeting.ResolveTarget(person,chest.transform.position)==victim,"Enemy under pointer was not resolved");
                Assert(SpiritBombTargeting.TryClickWorld(chest.transform.position),"Enemy click did not launch prepared sphere");
                Assert(SpiritBombTargeting.InputCaptured,"Launch click was not captured from native dragging");
                Assert(bomb.Damage>0 && Finite(bomb.Damage),"Released damage is not finite/positive");
                float stronger=bomb.Damage;
                foreach(var limb in victim.Limbs)
                {
                    limb.InitialHealth=KiCombatMath.Cap((double)stronger*20,1e37f);limb.Health=limb.InitialHealth;
                    var rb=limb.GetComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Dynamic;rb.gravityScale=0;
                }
                healthBefore=chest.Health;
                Assert(hand.Equip(KiHandLoadout.FistId),"Cannot switch ability after throwing");
                Assert(bomb.Phase==SpiritBombPhase.Flight,"Changing hand technique cancelled an already released sphere");
                float energy=bomb.Energy, donorBefore=donorKi.currentKi;
                Near(0,donorSupport.DonateToBomb(bomb,.1f),0,"Released sphere still accepts donations");
                Near(energy,bomb.Energy,0,"Released snapshot changed");Near(donorBefore,donorKi.currentKi,0,"Rejected donation spent Ki");
                Physics2D.SyncTransforms();
                Global.main.camera.transform.position=new Vector3(position.x-3,position.y,-10);
                Global.main.camera.orthographicSize=12;
            }))yield break;
            yield return null;yield return null;
            if(!Check(()=>Assert(!SpiritBombTargeting.InputCaptured,"Target selection retained the pointer blocker")))yield break;
            float deadline=Time.realtimeSinceStartup+10;
            while(bomb && bomb.Phase==SpiritBombPhase.Flight && Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(()=>
            {
                Assert(bomb && bomb.Phase==SpiritBombPhase.Containment,"Actual swept sphere collision did not start containment");
                Assert(bomb.CapturedCount>0,"Enemy was not captured on contact");
                distanceBefore=Vector2.Distance(chest.transform.position,bomb.transform.position);
                Log("PASS swept collision, enemy containment, launch snapshot, no post-launch donation and independent fired lifetime; damage="+bomb.Damage);
            }))yield break;
            yield return new WaitForSeconds(.25f);
            if(!Check(()=>Assert(Vector2.Distance(chest.transform.position,bomb.transform.position)<distanceBefore-.1f,"Vortex did not physically draw the ragdoll toward its epicentre")))yield break;
            deadline=Time.realtimeSinceStartup+15;
            int observed=0;float lastHealth=healthBefore,healthBeforeFinal=0;
            while(bomb && bomb.Phase==SpiritBombPhase.Containment && Time.realtimeSinceStartup<deadline)
            {
                if(bomb.PulsesApplied>observed)
                {
                    if(!Check(()=>
                    {
                        Assert(chest.Health<lastHealth,"A containment pulse caused no health damage");
                        lastHealth=chest.Health;observed=bomb.PulsesApplied;
                        Log("PULSE "+observed+" chest health="+lastHealth);
                    }))yield break;
                    if(observed==4)
                    {
                        yield return new WaitForSeconds(.04f);
                        yield return new WaitForEndOfFrame();
                        if(!Check(()=>Capture("06-spirit-epicentre.png")))yield break;
                    }
                    healthBeforeFinal=chest.Health;
                }
                yield return null;
            }
            if(!Check(()=>
            {
                Assert(bomb && bomb.FinalApplied && bomb.PulsesApplied==8,"Eight containment pulses did not precede the final blast");
                Assert(observed==8,"Native QA did not observe all eight pulses");
                Assert(chest.Health<healthBeforeFinal,"Final explosion applied no additional damage");
                float after=chest.Health;bomb.Detonate();Near(after,chest.Health,0,"Final explosion damaged a second time");
                Assert(bomb.CapturedCount==0,"Detonation retained captured people");
                foreach(var limb in victim.Limbs)Assert(Finite(limb.Health),"Spirit Bomb generated invalid health");
                Log("PASS physical inward capture, all eight damaging pulses, final radial damage, single final application and released victims");
            }))yield break;
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("07-spirit-detonation.png")))yield break;
            if(!Check(()=>
            {
                ki.currentKi=1000;Assert(hand.Equip("SpiritBomb"),"Cannot select a new Spirit Bomb");
                channel.enabled=true;Assert(channel.BeginCharge(hand.Limb),"Charge after previous shot failed");
                Open(hand);Select(hand,"SpiritBomb");
            }))yield break;
            float pauseEnergy=ki.currentKi;
            yield return new WaitForSecondsRealtime(.25f);
            if(!Check(()=>
            {
                Near(pauseEnergy,ki.currentKi,.001f,"Ability wheel did not suspend Spirit Bomb spending");
                wheel.Close(false);channel.CancelCharge();
                Assert(!channel.IsCharging,"Cancel left an active gathering sphere");
            }))yield break;
            yield return null;yield return null;
            if(!Check(()=>
            {
                Released("Spirit Bomb cancellation");
                Assert(hand.Equip(KiHandLoadout.FistId),"Cannot leave Genkidama after cancel");
                Log("PASS existing wheel input/cost suspension and charge cancellation; no global time/physics settings changed");
                if(victimRoot)Destroy(victimRoot);
            }))yield break;
            yield return VerifySpiritShockwavePhysics();
        }

        GameObject GravityQaBody(List<GameObject> fixtures,string name,Vector2 position,float mass,bool pinned=false,bool compound=false)
        {
            var go=new GameObject(name);go.transform.position=position;fixtures.Add(go);
            go.layer=LayerMask.NameToLayer("Objects");
            var rb=go.AddComponent<Rigidbody2D>();rb.mass=mass;rb.gravityScale=0;rb.bodyType=pinned?RigidbodyType2D.Kinematic:RigidbodyType2D.Dynamic;
            go.AddComponent<BoxCollider2D>().size=Vector2.one*.35f;
            if(compound)go.AddComponent<CircleCollider2D>().radius=.14f;
            var line=KiSurgeArt.Line(go.transform,name+" outline",5);line.startWidth=line.endWidth=.035f;
            line.startColor=line.endColor=new Color(.9f,.95f,1,.9f);line.useWorldSpace=false;
            line.SetPositions(new[]{new Vector3(-.18f,-.18f,-.3f),new Vector3(.18f,-.18f,-.3f),new Vector3(.18f,.18f,-.3f),new Vector3(-.18f,.18f,-.3f),new Vector3(-.18f,-.18f,-.3f)});
            return go;
        }
        IEnumerator VerifySpiritShockwavePhysics()
        {
            float cleanupDeadline=Time.realtimeSinceStartup+35;
            while(SpiritNukeShockwave.ActiveCount>0 && Time.realtimeSinceStartup<cleanupDeadline)yield return null;
            if(!Check(()=>Assert(SpiritNukeShockwave.ActiveCount==0,"Previous native shockwave did not finish")))yield break;
            var fixtures=new List<GameObject>();SpiritBombChannel channel=null;SpiritBombProjectile probe=null;SpiritNukeShockwave blast=null;
            Rigidbody2D light=null,heavy=null,pinned=null,far=null;
            Vector2 pinnedStart=Vector2.zero,farStart=Vector2.zero,flightStart=Vector2.zero;
            float initialRadius=0,temperature=0;
            if(!Check(()=>
            {
                subject.transform.position=new Vector3(80,90,0);ki.currentKi=1000;
                Assert(hand.Equip("SpiritBomb"),"Cannot equip gravity probe");
                channel=SpiritBombChannel.GetOrCreate(person);channel.enabled=false;
                Assert(channel.BeginCharge(hand.Limb),"Gravity probe did not gather");
                for(int i=0;i<50;i++)channel.ChargeStep(.1f);
                probe=channel.ChargingBomb;probe.SnapGatherAnchor();
                Vector2 centre=probe.transform.position;
                Assert(probe.GetComponentsInChildren<TMPro.TMP_Text>(true).Length==0,"Genkidama still creates floating instructions/readouts");
                light=GravityQaBody(fixtures,"QA light prop",centre+Vector2.up*(probe.Radius+1.4f),1).GetComponent<Rigidbody2D>();
                heavy=GravityQaBody(fixtures,"QA heavy compound prop",light.position,50,false,true).GetComponent<Rigidbody2D>();
                foreach(var c in heavy.GetComponents<Collider2D>())Physics2D.IgnoreCollision(c,light.GetComponent<Collider2D>());
                pinned=GravityQaBody(fixtures,"QA pinned prop",centre+new Vector2(-4,4),2,true).GetComponent<Rigidbody2D>();pinnedStart=pinned.position;
                far=GravityQaBody(fixtures,"QA outside gravity",centre+Vector2.up*(probe.GravityRange+6),1).GetComponent<Rigidbody2D>();farStart=far.position;
                Physics2D.SyncTransforms();flightStart=centre;
                Assert(channel.Launch(Vector2.right),"Gravity probe launch failed");
            }))yield break;
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            if(!Check(()=>
            {
                Assert(probe.Phase==SpiritBombPhase.Flight && Vector2.Distance(probe.transform.position,flightStart)>.1f,"Probe did not actually fly");
                Vector2 direction=((Vector2)probe.transform.position-light.position).normalized;
                Assert(Vector2.Dot(light.velocity,direction)>.1f,"Flight gravity did not attract an ordinary prop");
                Near(0,(light.velocity-heavy.velocity).magnitude,.03f,"Gravity depends on mass or duplicate collider count");
                Near(0,Vector2.Distance(pinnedStart,pinned.position),.001f,"Gravity moved a pinned object");
                Near(0,Vector2.Distance(farStart,far.position),.001f,"Gravity moved an object outside its range");
                Assert(probe.BeginContainment(),"Cannot enter gravity containment test");
                Vector2 centre=probe.transform.position;
                light.position=heavy.position=centre+Vector2.up*(probe.Radius+1.4f);light.velocity=heavy.velocity=Vector2.zero;
                Log("PASS actual flight attracts arbitrary props, equal acceleration across 1/50 mass and compound colliders; pinned/out-of-range bodies stay still");
            }))yield break;
            for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            if(!Check(()=>
            {
                Assert(light.velocity.y<-.1f && heavy.velocity.y<-.1f,"Containment gravity did not attract props outside the core");
                Vector2 centre=probe.transform.position;
                light.position=heavy.position=centre+Vector2.up*(probe.Radius*.7f);light.velocity=heavy.velocity=Vector2.down*6;
                far.position=centre+Vector2.up*(probe.Radius*2+3);far.velocity=Vector2.zero;
                temperature=hand.Limb.PhysicalBehaviour.Temperature;
                Physics2D.SyncTransforms();probe.Detonate();blast=probe.DetonationShockwave;
                Assert(blast && SpiritNukeShockwave.ActiveCount==1,"Detonation did not emit one native Nuke Shockwave");
                var native=blast.NativeEffect;
                var original=ModAPI.FindSpawnable("Atom Bomb").Prefab.GetComponent<AtomBombBehaviour>().ExplosionPrefab.GetComponent<NewAtomBombExplosionBehaviour>();
                Assert(!native.enabled && native.VaporisationDistance==0,"Nuclear Start/disintegration was not suppressed");
                Assert(native.ShockwaveSprite.sprite==original.ShockwaveSprite.sprite && native.ShockwaveSprite.sharedMaterial==original.ShockwaveSprite.sharedMaterial,"Shockwave does not use the exact native visual asset");
                Near(original.MaxColliderSize,native.MaxColliderSize,0,"Native wave range changed");
                Near(original.MaxForceMagnitude,native.MaxForceMagnitude,0,"Native pressure changed");
                Assert(native.PointEffector.enabled && native.ShockwaveCollider.enabled && native.ShockwaveCollider.usedByEffector,"Native physical effector is inactive");
                var mushroom=native.transform.GetChild(0).GetComponent<ParticleSystem>();
                Assert(mushroom && !mushroom.isPlaying && mushroom.particleCount==0,"Mushroom cloud was not removed");
                initialRadius=native.ShockwaveCollider.radius;
                probe.Detonate();Assert(SpiritNukeShockwave.ActiveCount==1 && probe.DetonationShockwave==blast,"Repeat detonation spawned another wave");
                foreach(var renderer in probe.GetComponentsInChildren<Renderer>())Assert(!renderer.enabled,"Custom sphere/air/explosion graphics remain after detonation");
                Global.main.camera.transform.position=new Vector3(centre.x,centre.y,-10);Global.main.camera.orthographicSize=45;
                Log("PASS containment gravity, restored reference visuals without world labels, exact native Nuke Shockwave assets/force/range and once-only detonation");
            }))yield break;
            yield return new WaitForSeconds(.22f);
            yield return new WaitForEndOfFrame();if(!Check(()=>Capture("08-native-nuke-shockwave.png")))yield break;
            if(!Check(()=>
            {
                Assert(blast && blast.NativeEffect.ShockwaveCollider.radius>initialRadius,"Native shockwave did not expand");
                Assert(light.velocity.y>1 && heavy.velocity.y>1,"Native wave did not reverse infall and repel props");
                Assert(far.velocity.y>1,"Native wave did not push the distant prop");
                Near(temperature,hand.Limb.PhysicalBehaviour.Temperature,1,"Shockwave caused nuclear global heating");
                Near(0,Vector2.Distance(pinnedStart,pinned.position),.001f,"Native wave moved a pinned prop");
                Log("PASS native wave expansion, physical outward impulse at near/far distances, pinned body unchanged and no nuclear heating");
            }))yield break;
            cleanupDeadline=Time.realtimeSinceStartup+35;
            while(blast && Time.realtimeSinceStartup<cleanupDeadline)yield return null;
            if(!Check(()=>
            {
                Assert(!blast && SpiritNukeShockwave.ActiveCount==0,"Native wave survived its lifetime");
                foreach(var fixture in fixtures)if(fixture)Destroy(fixture);
                hand.Equip(KiHandLoadout.FistId);
                Log("PASS native wave lifetime cleanup; no custom air streams/clouds or persistent force field");
            }))yield break;
        }
    }
}
