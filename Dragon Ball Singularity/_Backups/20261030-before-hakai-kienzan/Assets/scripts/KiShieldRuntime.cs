using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    public partial class KiShield
    {
        public bool Active,Using;
        public GameObject Shield;
        public PhysicalProperties properties;
        public float Durability,RealDurability,CostPercent=.05f,shieldDiameterMeters=23f;
        public bool OverrideShieldScale;
        public Vector3 OverrideShieldScaleValue=Vector3.one;
        public float Integrity01 {get;private set;}=1;
        public float Capacity {get;private set;}
        public float Radius {get;private set;}
        public float Cooldown {get;private set;}
        public bool UnderPressure {get{return Using && clock-lastImpact<.18f;}}
        public Vector2 Centre {get{return torso?(Vector2)torso.position:(Vector2)transform.position;}}
        public PersonBehaviour Person {get;private set;}
        public KiSystem Energy {get;private set;}
        public Color Tint {get;private set;}
        internal KiShieldVisual Visual;
        readonly Dictionary<int,Contact> contacts=new Dictionary<int,Contact>();
        readonly List<int> stale=new List<int>();
        static readonly List<KiShield> live=new List<KiShield>();
        Transform torso;float clock,lastImpact=-10,bodyRadius=2,nextBounds;
        UseEventTrigger use;
        sealed class Contact {public int Frame=-1;public float Held,Seen;}

        void Ensure()
        {
            if(!Person)Person=GetComponent<PersonBehaviour>();if(!Energy)Energy=GetComponent<KiSystem>();
            if(!torso)torso=transform.Find("Body/MiddleBody");
        }
        void Start()
        {
            Ensure();if(!Energy)Energy=gameObject.AddComponent<KiSystem>();
            var upper=transform.Find("Body/UpperBody");
            if(upper)
            {
                use=upper.gameObject.AddComponent<UseEventTrigger>();use.Action=()=>{if(Using)Lower(false);else if(Active)TryRaise();};
                var physical=upper.GetComponent<PhysicalBehaviour>();
                if(physical && physical.ContextMenuOptions!=null)
                    physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("!KiBarrier",()=>Using?"Ki Shield: lower":"Ki Shield: raise","A mobile ki barrier. Attacks consume its strength and your ki.",()=>{if(Using)Lower(false);else {Active=true;TryRaise();}}));
            }
            // Old saved scenes must create a fresh collider owned by this runtime.
            bool resume=Using;
            if(resume && Shield && !Shield.GetComponent<KiShieldSurface>())
                foreach(var limb in Person.Limbs){var freeze=limb?limb.GetComponent<FreezeBehaviour>():null;if(freeze)Destroy(freeze);}
            Using=false;if(Shield)Destroy(Shield);Shield=null;if(resume && Active)TryRaise();
        }
        void Update()
        {
            Ensure();if(Global.main && Global.main.Paused)return;
            float dt=Mathf.Clamp(Time.deltaTime,0,.1f);clock+=dt;Cooldown=Mathf.Max(0,Cooldown-dt);
            if(!Using){if(Cooldown<=0)Integrity01=Mathf.MoveTowards(Integrity01,1,dt*.20f);return;}
            if(!Active || !Person || !Person.IsAlive() || !TorsoAlive()){Lower(false);return;}
            RefreshCapacity();UpdateRadius();
            float cost=Mathf.Max(0,KiCombatMath.Finite(DBPU.KiAttackCostMultiplier,1));
            float maximum=KiCombatMath.MaxKi(Energy);
            Energy.currentKi=Mathf.Max(0,Energy.currentKi-maximum*Mathf.Max(0,CostPercent)*cost*dt);
            if(cost>0 && Energy.currentKi<=maximum*.1f){Lower(true);return;}
            if(clock-lastImpact>1.75f && Energy.currentKi>maximum*.3f)Integrity01=Mathf.MoveTowards(Integrity01,1,dt*.055f);
            RealDurability=Capacity*Integrity01;
            if(Visual)Visual.Tick(dt);
            if(Shield)Shield.transform.position=Centre;
            stale.Clear();foreach(var entry in contacts)if(clock-entry.Value.Seen>1)stale.Add(entry.Key);
            foreach(int id in stale)contacts.Remove(id);
        }
        void FixedUpdate(){if(Using && Shield && (!Global.main || !Global.main.Paused))Shield.transform.position=Centre;}
        bool TorsoAlive(){var limb=torso?torso.GetComponent<LimbBehaviour>():null;return !limb || limb.Health>0;}
        public void RefreshCapacity()
        {
            Ensure();if(!Person || !Energy)return;
            var power=CombatPowerController.Bind(Person);power.Refresh();
            double strength=Durability>0?(double)Durability*MCP_Utils.GetFinalMultiplier(Person)*CombatPowerController.OutputFor(Person):power.PowerLevel*1.2;
            Capacity=KiCombatMath.Cap(Math.Max(1,strength),float.MaxValue);RealDurability=Capacity*Integrity01;
            Tint=DBPU.KiBlast_CustomColorEnabled?DBPU.GetKiBlastCustomColor(1):Energy.requiredColor;
            if(Tint==Color.clear)Tint=new Color(1,.83f,.24f);Tint=new Color(Mathf.Max(.08f,Tint.r),Mathf.Max(.08f,Tint.g),Mathf.Max(.08f,Tint.b),1);
        }
        void UpdateRadius()
        {
            if(clock>=nextBounds)
            {
                nextBounds=clock+.25f;Bounds bounds=new Bounds();bool found=false;int n=0;
                foreach(var limb in Person.Limbs)
                {
                    if(!limb || limb.Health<=0 || !limb.transform.IsChildOf(Person.transform) || n++>=16)continue;
                    var sprite=limb.GetComponent<SpriteRenderer>();if(!sprite || !sprite.sprite)continue;
                    if(!found){bounds=sprite.bounds;found=true;}else bounds.Encapsulate(sprite.bounds);
                }
                if(found)bodyRadius=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.4f;
            }
            float wanted=OverrideShieldScale?Mathf.Max(Mathf.Abs(OverrideShieldScaleValue.x),Mathf.Abs(OverrideShieldScaleValue.y))*11.4f:Mathf.Clamp(shieldDiameterMeters,5,60)*.15f;
            float radius=Mathf.Clamp(Mathf.Max(wanted,bodyRadius),.8f,64);
            if(Mathf.Abs(radius-Radius)>.01f){Radius=radius;var edge=Shield?Shield.GetComponent<EdgeCollider2D>():null;if(edge){var p=new Vector2[129];for(int i=0;i<p.Length;i++){float a=i*Mathf.PI/64;p[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}edge.points=p;}}
        }
        public bool TryRaise()
        {
            Ensure();if(Using)return true;
            if(!isActiveAndEnabled || !Person || !Person.IsAlive() || !TorsoAlive() || !Energy || Cooldown>0 || Integrity01<.2f)return false;
            if(DBPU.KiAttackCostMultiplier>0 && Energy.currentKi<KiCombatMath.MaxKi(Energy)*.12f)return false;
            Active=Using=true;RefreshCapacity();
            Shield=new GameObject("DBPU mobile Ki Shield");Shield.layer=11;Shield.transform.position=Centre;
            var rb=Shield.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
            var edge=Shield.AddComponent<EdgeCollider2D>();edge.edgeRadius=.025f;
            var surface=Shield.AddComponent<KiShieldSurface>();surface.Owner=this;
            Radius=0;nextBounds=0;UpdateRadius();
            foreach(var col in Person.GetComponentsInChildren<Collider2D>())if(col && col!=edge)Physics2D.IgnoreCollision(edge,col,true);
            Visual=Shield.AddComponent<KiShieldVisual>();Visual.Initialize(this);
            if(!live.Contains(this))live.Add(this);return true;
        }
        public void Lower(bool broken=false)
        {
            if(!Using && !Shield)return;
            Using=false;live.Remove(this);contacts.Clear();
            if(broken){Integrity01=0;Cooldown=3f;}
            if(Shield)
            {
                var collider=Shield.GetComponent<EdgeCollider2D>();if(collider)collider.enabled=false;
                if(Visual)Visual.Collapse(broken);else Destroy(Shield);
            }
            Shield=null;Visual=null;RealDurability=0;
        }
        // Returns whether this shell still stopped the attack. A broken barrier
        // immediately lets the same source continue, rather than deleting it.
        public bool ReceiveDamage(double damage,Vector2 point,int source=0)
        {
            if(!Using)return false;
            damage=double.IsPositiveInfinity(damage)?Math.Max(1,Capacity)*100d:Math.Max(0,double.IsNaN(damage)?0:damage);
            float fraction=(float)Math.Min(100,damage/Math.Max(1,Capacity));
            Integrity01=Mathf.Max(0,Integrity01-fraction*.60f);lastImpact=clock;
            float cost=Mathf.Max(0,DBPU.KiAttackCostMultiplier);
            Energy.currentKi=Mathf.Max(0,Energy.currentKi-KiCombatMath.MaxKi(Energy)*fraction*.20f*cost);
            RealDurability=Capacity*Integrity01;
            if(Visual)Visual.Hit(point,Mathf.Clamp01(fraction*5),source);
            if(Integrity01<=.001f || (cost>0 && Energy.currentKi<=KiCombatMath.MaxKi(Energy)*.1f)){Lower(true);return false;}
            return true;
        }
        public bool ReceivePressure(int source,double perSecond,Vector2 point,float dt,out float held)
        {
            held=0;if(!Using)return false;Contact contact;
            if(!contacts.TryGetValue(source,out contact))contacts[source]=contact=new Contact();
            if(clock-contact.Seen>.25f)contact.Held=0;contact.Seen=clock;
            if(contact.Frame==Time.frameCount){held=contact.Held;return Using;}
            contact.Frame=Time.frameCount;dt=Mathf.Clamp(dt,0,.1f);contact.Held+=dt;held=contact.Held;
            return ReceiveDamage(perSecond*dt,point,source);
        }
        public float HeldTime(int source){Contact contact;return contacts.TryGetValue(source,out contact)?contact.Held:0;}
        public static void ConstrainBall(BallBehaviour ball)
        {
            if(ball.ShieldDissolved){ball.Distance=ball.ShieldStopDistance;return;}
            ball.ShieldBarrier=null;if(!ball.Firing || !ball.ProjectileObject)return;
            var collider=ball.ProjectileObject.GetComponent<CircleCollider2D>();
            float padding=collider?Mathf.Abs(collider.radius*ball.ProjectileObject.transform.lossyScale.x):.1f;
            float distance=ball.Distance;KiShield shield;
            if(!ClipAttack(ball.gameObject,ball.GetInstanceID(),ball.BallPosition,ball.BallDirection,ref distance,padding,(double)ball.RealDamage*60,out shield))return;
            ball.ShieldBarrier=shield;ball.Distance=distance;
            if(shield.HeldTime(ball.GetInstanceID())>=8 && shield.Integrity01>.2f && shield.Capacity>(double)ball.RealDamage*120)
            {
                ball.ShieldDissolved=true;ball.ShieldStopDistance=distance;ball.Firing=false;
                foreach(var c in ball.ProjectileObject.GetComponentsInChildren<Collider2D>())c.enabled=false;
            }
        }
        public static bool HandleBlast(BlastComponent blast,Collider2D collision=null,float advance=-1)
        {
            if(!blast || blast.AlreadyHit || (Global.main && Global.main.Paused))return false;
            KiShield barrier=null;Vector2 point=blast.transform.position;
            var surface=collision?collision.GetComponent<KiShieldSurface>():null;
            if(surface)
            {
                barrier=surface.Owner;if(!barrier || !barrier.Using)return false;
                if(blast.User && blast.User.transform.root==barrier.transform.root)
                {var own=blast.GetComponent<Collider2D>();if(own)Physics2D.IgnoreCollision(own,collision,true);return true;}
                point=barrier.Centre+((Vector2)blast.transform.position-barrier.Centre).normalized*barrier.Radius;
            }
            else
            {
                var body=blast.GetComponent<Rigidbody2D>();if(!body)return false;
                Vector2 velocity=blast.HandleSelfMovement?(Vector2)blast.transform.right*blast.realSpeed:body.velocity;
                var c=blast.GetComponent<CircleCollider2D>();float padding=c?Mathf.Abs(c.radius*blast.transform.lossyScale.x):.03f;
                float entry;if(!TryTrace(blast.User,body.position,velocity.normalized,velocity.magnitude*(advance>=0?advance:Time.deltaTime)+padding,padding,out barrier,out entry,out point))return false;
            }
            double budget=barrier.RealDurability/.6;
            if(!barrier.ReceiveDamage(blast.Damage,point,blast.GetInstanceID())){blast.Damage=KiCombatMath.Cap(Math.Max(0,blast.Damage-budget),float.MaxValue);return false;}
            blast.AlreadyHit=true;blast.HasHit=false;var rb=blast.GetComponent<Rigidbody2D>();if(rb)rb.simulated=false;
            foreach(var col in blast.GetComponentsInChildren<Collider2D>())col.enabled=false;
            UnityEngine.Object.Destroy(blast.gameObject);return true;
        }
        public static bool TryTrace(GameObject attacker,Vector2 origin,Vector2 direction,float distance,float padding,out KiShield barrier,out float entry,out Vector2 point)
        {
            barrier=null;entry=distance;point=origin;direction.Normalize();if(direction.sqrMagnitude<.5f || distance<0)return false;
            for(int i=live.Count-1;i>=0;i--)
            {
                var shield=live[i];if(!shield){live.RemoveAt(i);continue;}
                if(!shield.Using || !shield.isActiveAndEnabled || !shield.Person || !shield.Person.IsAlive() || (attacker && attacker.transform.root==shield.transform.root))continue;
                Vector2 delta=origin-shield.Centre;float r=shield.Radius+Mathf.Max(0,padding);
                // Only incoming attacks cross the outside of the shell. Friendly
                // fire leaving the interior is not caught on the far side.
                if(delta.sqrMagnitude<r*r-.002f)continue;
                float along=Vector2.Dot(delta,direction),discriminant=along*along-delta.sqrMagnitude+r*r;
                if(discriminant<0)continue;float t=-along-Mathf.Sqrt(discriminant);
                if(t<-.001f || t>entry)continue;
                entry=Mathf.Max(0,t);barrier=shield;Vector2 normal=(origin+direction*entry-shield.Centre).normalized;point=shield.Centre+normal*shield.Radius;
            }
            return barrier;
        }
        public static bool ClipAttack(GameObject attacker,int source,Vector2 origin,Vector2 direction,ref float distance,float padding,double pressure,out KiShield barrier)
        {
            float entry;Vector2 point;barrier=null;
            if(!TryTrace(attacker,origin,direction,distance,padding,out barrier,out entry,out point))return false;
            float held;if(!barrier.ReceivePressure(source,pressure,point,Time.deltaTime,out held)){barrier=null;return false;}
            distance=entry;return true;
        }
        public static bool Protects(PersonBehaviour person,GameObject attacker,Vector2 origin,double damage,int source)
        {
            if(!person)return false;var shield=person.GetComponent<KiShield>();if(!shield || !shield.Using)return false;
            if(attacker && attacker.transform.root==person.transform.root)return false;
            Vector2 normal=origin-shield.Centre;if(normal.sqrMagnitude<shield.Radius*shield.Radius*.95f)return false;
            float held;return shield.ReceivePressure(source,damage,shield.Centre+normal.normalized*shield.Radius,Time.deltaTime,out held);
        }
        public static float EstimateDiameterForPerson(PersonBehaviour person,float overscale=1.12f,int maxLimbIndexExclusive=-1)
        {
            if(!person || person.Limbs==null)return 23;Bounds bounds=new Bounds();bool found=false;
            int end=maxLimbIndexExclusive>0?Mathf.Min(maxLimbIndexExclusive,person.Limbs.Length):person.Limbs.Length;
            for(int i=0;i<end;i++){var l=person.Limbs[i];var r=l?l.GetComponent<SpriteRenderer>():null;if(!r)continue;if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
            return found?Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.y)*overscale/.3f,23,60):23;
        }
        void OnDisable(){Lower(false);live.Remove(this);}
        void OnDestroy(){Lower(false);live.Remove(this);if(use)Destroy(use);}
    }
    public sealed class KiShieldSurface:MonoBehaviour
    {
        public KiShield Owner;
        void OnCollisionEnter2D(Collision2D hit)
        {
            if(!Owner || !Owner.Using || !hit.rigidbody || hit.relativeVelocity.sqrMagnitude<2.25f)return;
            if(hit.gameObject.GetComponent<BlastComponent>())return;
            var limb=hit.gameObject.GetComponent<LimbBehaviour>();if(limb && limb.Person==Owner.Person)return;
            // An equipped Ki Fist owns this contact, including its cooldown.
            // Its callback may already have consumed the charge in this step.
            var fist=limb?limb.GetComponent<KiFistHand>():null;if(fist && fist.Equipped)return;
            double damage=limb && limb.Person?(double)CombatPowerController.AttackHealth(limb.Person)*CombatPowerController.OutputFor(limb.Person)*Mathf.Clamp(hit.relativeVelocity.magnitude/30,.02f,1):Owner.Capacity*Mathf.Min(.025f,hit.rigidbody.mass*hit.relativeVelocity.magnitude*.00002f);
            Owner.ReceiveDamage(damage,hit.contactCount>0?hit.GetContact(0).point:(Vector2)hit.transform.position,hit.rigidbody.GetInstanceID());
        }
    }
}
