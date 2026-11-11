using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    public sealed partial class KiCombatQa
    {
        IEnumerator VerifyKiShield()
        {
            GameObject defender=null,attacker=null;PersonBehaviour target=null,source=null;KiShield shield=null;KiSystem energy=null;
            float health=0,spentFrom=0,integrity=0;int freezes=0;bool detailsBefore=false;
            if(!Check(()=>
            {
                foreach(var p in FindObjectsOfType<PersonBehaviour>())p.transform.position+=new Vector3(-180,0,0);
                defender=Spawn("[DBPU] (BM) Goku",new Vector3(1,30,0));attacker=Spawn("[DBPU] (BM) Goku",new Vector3(-12,30,0));
                DBPU.KiDrainMultiplier=0;DBPU.KiAttackCostMultiplier=1;
                Global.main.camera.transform.position=new Vector3(-2,31,-10);Global.main.camera.orthographicSize=7.5f;
                detailsBefore=Global.main.ShowLimbStatus;if(!detailsBefore)Global.main.ToggleLimbStatus();
            }))yield break;
            yield return new WaitForSecondsRealtime(1.8f);
            if(!Check(()=>
            {
                Prepare(defender,out target,out energy);KiSystem unused;Prepare(attacker,out source,out unused);
                foreach(var actor in new[]{defender,attacker})foreach(var r in actor.GetComponentsInChildren<SpriteRenderer>())r.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                foreach(var canvas in FindObjectsOfType<Canvas>())if(canvas.name!="DBPU Combat HUD")canvas.enabled=false;
                shield=defender.GetComponent<KiShield>();Assert(shield,"Native KiShield component missing");
                energy.requiredColor=new Color(.58f,.18f,1);freezes=defender.GetComponentsInChildren<FreezeBehaviour>().Length;
                health=target.Limbs.Sum(l=>l.Health);Assert(shield.TryRaise(),"Barrier failed to raise");
                Assert(shield.Using && shield.Shield && shield.Capacity>0 && shield.Radius>1,"Barrier runtime/collider not ready");
                Assert(defender.GetComponentsInChildren<FreezeBehaviour>().Length==freezes,"Shield froze the fighter");
                Assert(shield.Shield.GetComponentsInChildren<MeshRenderer>().Length>=4,"Native 3D surface layers missing");
                foreach(var r in shield.Shield.GetComponentsInChildren<MeshRenderer>())Assert(r.sharedMaterial.shader.isSupported,"Barrier shader unsupported");
                spentFrom=energy.currentKi;
            }))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                Assert(energy.currentKi<spentFrom && energy.currentKi>spentFrom-40,"Continuous shield upkeep not bounded");
                defender.transform.position+=Vector3.right;
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Near(0,Vector2.Distance(shield.Shield.transform.position,shield.Centre),.01f,"Barrier did not follow the torso");
                Global.main.TogglePaused();
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();spentFrom=energy.currentKi;
            yield return null;yield return null;
            if(!Check(()=>
            {
                Near(spentFrom,energy.currentKi,.0001f,"Paused barrier drained ki");Global.main.TogglePaused();
                Log("PASS native mobile 3D shield, torso follow, no added FreezeBehaviour, sustained upkeep and pause");
                integrity=shield.Integrity01;
            }))yield break;
            BlastComponent weak=null;
            if(!Check(()=>weak=ShieldBlast(source,shield.Centre+Vector2.left*(shield.Radius+3),Vector2.right,shield.Capacity*.03f,1400)))yield break;
            yield return new WaitForSeconds(.15f);
            if(!Check(()=>
            {
                Assert(!weak,"High-speed native blast tunneled through the barrier");Assert(shield.Using && shield.Integrity01<integrity,"Blast did not consume shield integrity");
                Near(health,target.Limbs.Sum(l=>l.Health),health*.00001f,"Blocked blast damaged protected limbs");
                Log("PASS real 1400-unit/s native blast intercepted without health leakage");
            }))yield break;
            BlastComponent friendly=null;
            if(!Check(()=>friendly=ShieldBlast(target,shield.Centre,Vector2.right,shield.Capacity*10,25)))yield break;
            yield return new WaitForSeconds(.3f);
            if(!Check(()=>
            {
                Assert(friendly && shield.Using,"Own outgoing shot was blocked");Destroy(friendly.gameObject);shield.CostPercent=0;energy.currentKi=1000;
            }))yield break;

            BallBehaviour sphere=null;GameObject orb=null;
            if(!Check(()=>
            {
                sphere=attacker.GetComponent<BallBehaviour>();Assert(sphere,"Native sphere controller missing");
                orb=new GameObject("Native sphere shield QA");orb.layer=9;orb.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
                var collider=orb.AddComponent<CircleCollider2D>();collider.radius=.35f;collider.isTrigger=true;
                var component=orb.AddComponent<BallBehaviour.BallComponent>();component.ballBehaviour=sphere;component.User=attacker;component.RayDirection=Vector2.right;
                var art=new GameObject("Native sphere image");art.transform.SetParent(orb.transform,false);var r=art.AddComponent<SpriteRenderer>();r.sprite=DBPU.KiShield_Sprite;r.sharedMaterial=ModAPI.FindMaterial("VeryBright");r.color=new Color(.7f,.15f,1,.8f);art.transform.localScale=Vector3.one*.03f;
                typeof(BallBehaviour).GetField("Ball",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sphere,orb);
                sphere.BallPosition=shield.Centre+Vector2.left*(shield.Radius+4);sphere.BallDirection=Vector2.right;sphere.Distance=0;
                sphere.Damage=shield.Capacity*.0005f/MCP_Utils.GetFinalMultiplier(source);sphere.Speed=sphere.OriginalSpeed=18;sphere.Firing=sphere.StillActive=true;
            }))yield break;
            yield return new WaitForSeconds(.7f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Assert(sphere.ShieldBarrier==shield,"Native sphere was not held on the shell");
                Near(shield.Radius+.35f,Vector2.Distance(orb.transform.position,shield.Centre),.06f,"Sphere did not stop at outside contact");
                Near(health,target.Limbs.Sum(l=>l.Health),health*.00001f,"Held native sphere leaked damage");
                Capture("20-shield-sphere-contact.png");defender.transform.position+=Vector3.right;
            }))yield break;
            yield return new WaitForSeconds(.25f);
            if(!Check(()=>Near(shield.Radius+.35f,Vector2.Distance(orb.transform.position,shield.Centre),.07f,"Held sphere did not follow moving shell")))yield break;
            yield return new WaitForSeconds(8.1f);
            if(!Check(()=>
            {
                Assert(sphere.ShieldDissolved && !sphere.Firing && shield.Using,"A sufficiently strong barrier failed to exhaust the held sphere");
                Near(health,target.Limbs.Sum(l=>l.Health),health*.00001f,"Sphere absorption damaged protected body");
                sphere.StillActive=false;sphere.ShieldBarrier=null;Destroy(orb);typeof(BallBehaviour).GetField("Ball",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sphere,null);
                Log("PASS native sphere sustained clash, outside pin, moving shell, no damage leakage and energy absorption");
            }))yield break;

            BeamBehaviour beam=null;
            if(!Check(()=>
            {
                beam=attacker.GetComponent<BeamBehaviour>();beam.EffectID="Energy Wave";beam.BeamType="ArmBeam";
                var arm=GetHand(source,0).Limb.transform;arm.rotation=Quaternion.Euler(0,0,90);
                var release=typeof(BeamBehaviour).GetMethod("Release",BindingFlags.NonPublic|BindingFlags.Instance);
                Assert(release!=null,"Native beam release method missing");
                var audio=attacker.AddComponent<AudioSource>();beam.StartCoroutine((IEnumerator)release.Invoke(beam,new object[]{arm,audio}));
            }))yield break;
            yield return new WaitForSeconds(1);
            if(!Check(()=>
            {
                Assert(beam.StillActive && beam.EndBeam && beam.MiddleBeam,"Native beam failed to release");
                Assert(beam.ShieldBarrier==shield,"Native beam did not stop at the barrier");
                Near(health,target.Limbs.Sum(l=>l.Health),health*.00001f,"Native beam leaked damage through shield");
                Capture("21-shield-native-beam.png");beam.CancelBeamState();Log("PASS native Energy Wave clipping and protected body health");
            }))yield break;
            yield return new WaitForSeconds(.1f);
            if(!Check(()=>
            {
                var loadout=GetHand(source,0);Assert(loadout.Equip(KiHandLoadout.ShareId),"Ki Share unavailable");
                var limb=loadout.Limb;limb.transform.up=-(target.transform.Find("Body/MiddleBody").position-limb.transform.position).normalized;
                Physics2D.SyncTransforms();energy.currentKi=800;source.GetComponent<KiSystem>().currentKi=1000;
                var support=AllyKiReservoir.GetOrCreate(target);
                var share=KiShareHand.GetOrCreate(limb);float before=energy.currentKi,receivedBefore=support.TotalReceived,donorBefore=source.GetComponent<KiSystem>().currentKi;
                float sent=share.TransferStep(.1f);
                Assert(sent>0 && share.Receiver==target,"Shield blocked beneficial Ki Share");
                Assert(energy.currentKi>before,"Shielded Ki Share did not replenish the receiver");
                Near(sent,support.TotalReceived-receivedBefore,.01f,"Shielded receiver lost transferred energy");
                Near(sent,donorBefore-source.GetComponent<KiSystem>().currentKi,.01f,"Shielded Ki Share did not debit donor");
                Assert(shield.Using,"Beneficial Ki Share collapsed the barrier");share.Cancel();
                support.ConsumeSupport(support.TotalReceived);energy.currentKi=1000;
                Log("PASS real Ki Share ray passes through active shield, restores receiver ki and pays donor once");
            }))yield break;
            if(!Check(()=>
            {
                var loadout=GetHand(source,0);Assert(loadout.Equip(KiHandLoadout.FistId),"Ki Fist unavailable");
                var fist=KiFistHand.GetOrCreate(loadout.Limb);source.GetComponent<KiSystem>().currentKi=1000;
                fist.Activate();Assert(fist.IsCharging,"Ki Fist charge refused");for(int i=0;i<30;i++)fist.ChargeStep(.1f);fist.Arm();
                Assert(fist.IsCharged && fist.StrikeShield(shield,shield.Centre+Vector2.left*shield.Radius),"Charged fist did not discharge against shell");
                Assert(!fist.IsCharged,"Shield impact duplicated a prepared fist");Log("PASS charged Ki Fist hits the barrier and consumes its strike once");
            }))yield break;
            if(shield.Using)shield.Lower(false);
            yield return new WaitForSeconds(.1f);
            if(!Check(()=>
            {
                // Power/Output composition is checked with the automatic strength model.
                var power=CombatPowerController.Bind(target);power.SetOutput(1);energy.currentKi=1000;power.Tick(.1f);shield.RefreshCapacity();float full=shield.Capacity;
                power.SetOutput(.25f);power.Tick(.1f);shield.RefreshCapacity();Near(.25f,shield.Capacity/full,.001f,"Shield Output counted more than once");
                power.SetOutput(1);power.Tick(.1f);var kaioken=target.GetComponent<Kaioken>();kaioken.ApplyConfiguredMultiplier(2);kaioken.ToggleFromPowersMenu();shield.RefreshCapacity();Near(2,shield.Capacity/full,.002f,"Shield Kaioken counted more than once");
                kaioken.ToggleFromPowersMenu();energy.currentKi=1000;shield.RefreshCapacity();Near(1,shield.Capacity/full,.002f,"Shield retained Kaioken bonus");
                Log("PASS shield strength tracks Output and exact Kaioken multiplier");
            }))yield break;
            yield return new WaitForSeconds(5.2f);
            BlastComponent strong=null;
            if(!Check(()=>
            {
                energy.currentKi=1000;Assert(shield.TryRaise(),"Barrier failed to recover before overpower test");
                strong=ShieldBlast(source,shield.Centre+Vector2.left*(shield.Radius+.5f),Vector2.right,shield.Capacity*5,8);
            }))yield break;
            yield return new WaitForSeconds(.15f);
            if(!Check(()=>
            {
                Assert(!shield.Using && shield.Cooldown>0 && shield.RealDurability==0,"Overpowered shield did not break");
                Assert(strong && !strong.AlreadyHit && strong.Damage>0,"Breaking shield erased the stronger incoming attack");
                Assert(!shield.TryRaise(),"Broken shield bypassed its recovery lockout");Destroy(strong.gameObject);
                Capture("22-shield-overload.png");Log("PASS stronger attack breaks through, keeps residual damage and recovery lockout");
            }))yield break;
            yield return new WaitForSeconds(5.2f);
            if(!Check(()=>
            {
                energy.currentKi=1000;Assert(shield.TryRaise(),"Shield did not recover");shield.CostPercent=.05f;energy.currentKi=101;
            }))yield break;
            yield return new WaitForSeconds(.15f);
            if(!Check(()=>
            {
                Assert(!shield.Using,"Empty Ki reserve left barrier active");
                Assert(defender.GetComponentsInChildren<FreezeBehaviour>().Length==freezes,"Barrier cleanup altered freeze ownership");
                Log("PASS Ki exhaustion collapses shield without freezing or leaving a collider");
                if(Global.main.ShowLimbStatus!=detailsBefore)Global.main.ToggleLimbStatus();
            }))yield break;
        }
        BlastComponent ShieldBlast(PersonBehaviour owner,Vector2 position,Vector2 direction,float damage,float speed)
        {
            var go=new GameObject("Native fast blast shield QA");go.layer=13;go.transform.position=position;go.transform.right=direction;
            var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var col=go.AddComponent<CircleCollider2D>();col.radius=.08f;
            var blast=go.AddComponent<BlastComponent>();blast.User=owner.gameObject;blast.Damage=damage;blast.realSpeed=blast.speed=speed;
            foreach(var own in owner.GetComponentsInChildren<Collider2D>())Physics2D.IgnoreCollision(col,own,true);
            return blast;
        }
    }
}
