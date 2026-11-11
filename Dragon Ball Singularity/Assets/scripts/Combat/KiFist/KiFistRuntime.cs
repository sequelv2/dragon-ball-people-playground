using System;
using UnityEngine;

namespace DBPU
{
    // All energy below is expressed in the fighter's ordinary KiSystem units.
    // SupportEnergy is a separate, non-transferable balance, never a second copy of currentKi.
    public static class KiCombatMath
    {
        static readonly bool qaFocusOverride = Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-combat-qa") >= 0
            || Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-annihilation-qa") >= 0
            || Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-torso-picker-qa") >= 0;
        public static bool HasFocus { get { return Application.isFocused || qaFocusOverride; } }
        public static float Finite(float value, float fallback = 0f)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value; }
        public static float Cap(double value, float maximum = 1e32f)
        { return double.IsNaN(value) || value <= 0d ? 0f : (float)Math.Min(value, maximum); }
        public static float Step(float dt) { return KiEnergyMath.Step(dt); }
        public static float MaxKi(KiSystem ki) { return ki ? Mathf.Clamp(Finite(ki.maxKi), 0f, 1e30f) : 0f; }
        public static float Health(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return 1f;
            double health = 0d; int count = 0;
            foreach (var limb in person.Limbs)
                if (limb) { health += Math.Max(0d, Finite(limb.InitialHealth, 1e32f)); count++; }
            return Mathf.Max(1f, Cap(health / Math.Max(1, count)));
        }
        public static float Multiplier(PersonBehaviour person)
        {
            var multipliers = person ? person.GetComponent<MultipliersAndStuffs>() : null;
            return multipliers ? Mathf.Clamp(Finite(multipliers.FinalMultiplier, 1f), 0.01f, 1e24f) : 1f;
        }
        public static float NativeMultiplier(PersonBehaviour person)
        {
            var m = person ? person.GetComponent<MultipliersAndStuffs>() : null;
            if (!m) return 1f;
            double value = (double)Mathf.Max(0.01f, Finite(m.FormMultiplier, 1f)) * Mathf.Max(0.01f, Finite(m.BoostMultiplier, 1f))
                * Mathf.Max(0.01f, Finite(m.KaiokenMultiplier, 1f)) * Mathf.Max(0.01f, Finite(m.RageMultiplier, 1f))
                * Mathf.Max(0.01f, Finite(m.MajinMultiplier, 1f)) * Mathf.Max(0.01f, Finite(m.ExponentialMultiplier, 1f))
                * Mathf.Max(0.01f, Finite(m.OtherMultiplier, 1f));
            return Mathf.Clamp(Cap(value, 1e24f), 0.01f, 1e24f);
        }
        public static float TransferBudget(float current, float maximum, float dt)
        { return KiEnergyMath.DonationBudget(current, maximum, dt); }
        public static float SupportMultiplier(float support, float capacity)
        { return KiEnergyMath.SupportMultiplier(support, capacity); }
        public static float OwnChargeCost(float maximum, float before, float after, float costMultiplier)
        { return KiEnergyMath.ChargeCost(maximum, before, after, Finite(costMultiplier, 1f)); }
        public static bool CanAct()
        {
            return HasFocus && Time.timeScale > 0f && Global.main && !Global.main.Paused
                && !Global.main.GetPausedMenu() && !Global.ActiveUiBlock && !DialogBox.IsAnyDialogboxOpen
                && !CombatTransformationWheel.InputCaptured && !CombatAbilityWheel.InputCaptured;
        }
        public static bool ValidHand(LimbBehaviour limb, PersonBehaviour person)
        {
            return KiHandLoadout.IsHand(limb) && ValidStrikeLimb(limb, person);
        }
        public static bool ValidStrikeLimb(LimbBehaviour limb, PersonBehaviour person)
        {
            return KiHandLoadout.IsStrikeLimb(limb) && person && limb.Person == person && limb.Health > 0f && person.IsAlive()
                && limb.transform.root == person.transform.root && MCP_Utils.LimbCanUsePowers(limb);
        }
        public static Vector2 HandPoint(LimbBehaviour limb)
        { return limb ? (Vector2)(limb.transform.position - limb.transform.up * 0.3f) : Vector2.zero; }
        public static Vector2 StrikePoint(LimbBehaviour limb)
        {
            if (!limb) return Vector2.zero;
            if (!KiHandLoadout.IsFoot(limb)) return HandPoint(limb);
            var collider = limb.GetComponent<Collider2D>();
            return collider ? (Vector2)collider.bounds.center : (Vector2)limb.transform.position;
        }
        public static Vector2 StrikeDirection(LimbBehaviour limb)
        {
            if (!limb) return Vector2.right;
            return KiHandLoadout.IsFoot(limb) ? ((Vector2)limb.transform.TransformVector(Vector3.right)).normalized : -(Vector2)limb.transform.up;
        }
    }

    // The wheel targets one foot even when hovering its shin or thigh. Forwarding
    // Use retains that same target without equipping or creating a strike on its own.
    public sealed class KiStrikeLimbAlias : MonoBehaviour
    {
        LimbBehaviour source, foot;
        UseEventTrigger trigger;
        public LimbBehaviour StrikeLimb { get { return foot; } }
        public static void Bind(LimbBehaviour source, LimbBehaviour foot)
        {
            if (!source || !KiHandLoadout.IsFoot(foot) || source.Person != foot.Person) return;
            var alias = source.GetComponent<KiStrikeLimbAlias>();
            if (!alias) alias = source.gameObject.AddComponent<KiStrikeLimbAlias>();
            alias.source = source; alias.foot = foot;
            if (!alias.trigger) { alias.trigger = source.gameObject.AddComponent<UseEventTrigger>(); alias.trigger.Action = alias.Activate; }
        }
        public void Activate()
        {
            if (!source || !foot || source.Health <= 0 || source.Person != foot.Person
                || !KiCombatMath.ValidStrikeLimb(foot, source.Person) || !source.transform.IsChildOf(source.Person.transform)) return;
            var loadout = foot.GetComponent<KiHandLoadout>();
            if (!loadout || (loadout.SelectedId != KiHandLoadout.FistId && loadout.SelectedId != KiHandLoadout.InfusionId)) return;
            var strike = foot.GetComponent<KiFistHand>();
            if (strike && strike.Equipped) strike.Activate();
        }
        void OnDestroy() { if (trigger) Destroy(trigger); }
    }

    public enum KiFistMode { Ki, God, Infusion }

    public class KiFistHand : MonoBehaviour
    {
        public bool Equipped;
        // Multiplies charge speed; the overcharge key (B) uses a fast rate.
        public float ChargeRate = 1f;
        [SerializeField] KiFistMode selectedMode;
        public KiFistMode Mode { get { return selectedMode; } private set { selectedMode=value; } }
        // Retain old scene/loadout API while storing one technique and its selected mode.
        public bool InfuseAllEnergy { get { return Mode==KiFistMode.Infusion; } set { if(value)SetMode(KiFistMode.Infusion);else if(InfuseAllEnergy)SetMode(KiFistMode.Ki); } }
        public float InfusedEnergy { get { return infusedEnergy; } }
        public LimbBehaviour Limb { get; private set; }
        public PersonBehaviour Person { get; private set; }
        public float Charge01 { get { return Mathf.Clamp01(chargeSeconds / 3f); } }
        public float Overcharge01 { get { return Mathf.Clamp01((chargeSeconds - 3f) / 3f); } }
        public bool IsCharged { get { return armed; } }
        public bool IsOvercharged { get { return chargeSeconds >= 5.95f || (infusedEnergy > 0 && Charge01 >= .99f); } }
        public bool IsCharging { get { return charging; } }
        public bool IsHeldCharging { get { return charging && heldCharge; } }
        public bool IsReady { get { return Equipped && !charging && !armed && recovery<=0 && KiCombatMath.ValidStrikeLimb(Limb,Person) && ki && ki.currentKi>=SwingCost; } }
        public bool GodModeAvailable
        {
            get
            {
                if(!Person)return false;
                var identity=Person.GetComponent<DbpuCharacterIdentity>();string id=identity?identity.CharacterId:null;
                if(id=="BM/Beerus" || id=="BM/Whis" || id=="DBSSH/Beerus" || id=="DBSSH/Whis")return true;
                if(string.IsNullOrEmpty(id) && (Person.name.IndexOf("Beerus",StringComparison.OrdinalIgnoreCase)>=0 || Person.name.IndexOf("Whis",StringComparison.OrdinalIgnoreCase)>=0))return true;
                string form=ki?ki.CurrentForm:null;
                return form=="SSJGod" || form=="SSJBlue" || form=="PSSJBlue" || form=="SSJBlueEvolution" || form=="UI" || form=="MUI";
            }
        }
        public Color EffectTint
        {
            get
            {
                if(Mode==KiFistMode.Infusion)return new Color(1,.88f,.48f);
                // Ordinary Ki Fist takes the colour of the fighter's current aura/form.
                if(Mode!=KiFistMode.God)return Person?Color.Lerp(DbpuAuraPalette.Get(Person).Accent,Color.white,.18f):new Color(.80f,.92f,1);
                var identity=Person?Person.GetComponent<DbpuCharacterIdentity>():null;string id=identity?identity.CharacterId:"";
                if(id!=null && id.IndexOf("Beerus",StringComparison.OrdinalIgnoreCase)>=0)return new Color(.85f,.48f,1);
                string form=ki?ki.CurrentForm:"";
                if(form=="UI" || form=="MUI" || (id!=null && id.IndexOf("Whis",StringComparison.OrdinalIgnoreCase)>=0))return new Color(.80f,.94f,1);
                if(form=="SSJBlue" || form=="PSSJBlue" || form=="SSJBlueEvolution")return new Color(.32f,.85f,1);
                return new Color(1,.38f,.52f);
            }
        }
        public string Status
        {
            get
            {
                if (armed) return infusedEnergy > 0 ? "ENERGY INFUSION — internal disruption ready" : IsOvercharged ? "FULLY OVERCHARGED — swing hard" : "CHARGED — swing hard or Use to dash";
                if (charging && chargeSeconds >= 3f) return "OVERCHARGING " + Mathf.RoundToInt(Overcharge01 * 100f) + "%";
                if (charging) return "CHARGING " + Mathf.RoundToInt(Charge01 * 100f) + "%";
                return KiFistInput.ModeLabel(Mode)+" — swing hard. Tap R: mode / hold R: charge.";
            }
        }
        KiSystem ki;
        AllyKiReservoir reservoir;
        UseEventTrigger useTrigger;
        KiFistGlowFx visual;
        bool charging, armed, continuousSeen, lunged,heldCharge;
        float recovery;
        Vector2 previousPoint;bool havePrevious;
        Vector2 realityVelocity;float realityVelocityTime=-100;
        internal Vector2 RealityApproachVelocity
        {
            get
            {
                if(Time.fixedTime-realityVelocityTime<=Time.fixedDeltaTime*1.1f)return realityVelocity;
                var body=Limb?Limb.GetComponent<Rigidbody2D>():null;
                return body?body.GetPointVelocity(KiCombatMath.StrikePoint(Limb)):Vector2.zero;
            }
        }
        internal bool RealityStrikeAvailable {get{return Equipped && !charging && recovery<=0 && (armed || IsReady)
            && (Mode!=KiFistMode.God || GodModeAvailable) && KiCombatMath.ValidStrikeLimb(Limb,Person);}}
        internal bool CommitRealityClash()
        {
            if(!CommitStrike())return false;
            infusedEnergy=0;Cancel();return true;
        }
        readonly RaycastHit2D[] sweepHits=new RaycastHit2D[32];
        float chargeSeconds, armedLifetime, overchargeEnergy, armedSupportMultiplier = 1f;
        float baseHealth = -1f;
        float infusedEnergy;
        KeyCode directKey, directSecondary;
        bool directHeld;
        int startFrame;
        int lastActivationFrame = -1;
        float SwingCost { get { return KiCombatMath.MaxKi(ki)*(Mode==KiFistMode.God?.035f:.02f)*Mathf.Clamp(KiCombatMath.Finite(DBPU.KiAttackCostMultiplier,1),0,100); } }

        public bool SetMode(KiFistMode mode)
        {
            if(mode!=KiFistMode.Ki && mode!=KiFistMode.God && mode!=KiFistMode.Infusion)return false;
            if(mode==KiFistMode.God && !GodModeAvailable)return false;
            if(Mode==mode)return true;
            Cancel();Mode=mode;return true;
        }
        public bool CycleMode()
        {
            if(Mode==KiFistMode.Ki && GodModeAvailable)return SetMode(KiFistMode.God);
            if(Mode!=KiFistMode.Infusion && reservoir && reservoir.TotalReceived>0)return SetMode(KiFistMode.Infusion);
            return SetMode(KiFistMode.Ki);
        }
        public bool BeginHeldCharge()
        {
            if(armed)Cancel();
            if(!charging && !BeginCharge())return false;
            heldCharge=true;directHeld=false;return true;
        }
        public void ReleaseHeldCharge(){if(heldCharge){heldCharge=false;Arm();}ChargeRate=1f;}
        public void CancelHeldCharge(){ReleaseHeldCharge();}

        public static KiFistHand GetOrCreate(LimbBehaviour limb)
        {
            if (!KiHandLoadout.IsStrikeLimb(limb)) return null;
            var hand = limb.GetComponent<KiFistHand>(); if (!hand) hand = limb.gameObject.AddComponent<KiFistHand>();
            hand.Bind(limb); return hand;
        }
        void Awake() { Bind(GetComponent<LimbBehaviour>()); }
        void Bind(LimbBehaviour limb)
        {
            Limb = limb; Person = limb ? limb.Person : null;
            if (!Person) return;
            ki = Person.GetComponent<KiSystem>(); reservoir = AllyKiReservoir.GetOrCreate(Person);
            if (baseHealth < 0f)
            {
                var multipliers = Person.GetComponent<MultipliersAndStuffs>();
                float formHealth = multipliers ? Mathf.Max(.01f, KiCombatMath.Finite(multipliers.FormMultiplier, 1f)) : 1f;
                // Only remove multipliers actually applied to InitialHealth.
                // Dividing by Boost/Rage/etc. canceled a fighter's earned power
                // when the first Ki Fist was equipped after that boost.
                baseHealth = KiCombatMath.Cap((double)KiCombatMath.Health(Person) / formHealth
                    / CombatPowerController.KaiokenHealthScale(Person) / reservoir.HealthMultiplier
                    / CombatPowerController.HealthScale(Person));
            }
            if (!useTrigger) { useTrigger = gameObject.AddComponent<UseEventTrigger>(); useTrigger.Action = Activate; }
        }
        public void Activate()
        {
            if (!Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidStrikeLimb(Limb, Person)) return;
            // The engine broadcasts Use to every component, while UseEventTrigger also
            // relays it through Action. Only the native trigger handles that message.
            if (lastActivationFrame == Time.frameCount) return;
            lastActivationFrame = Time.frameCount;
            if (armed) { Lunge(); return; }
            if (charging) { Arm(); return; }
            BeginCharge();
        }
        public bool BeginCharge()
        {
            if (!Equipped || !KiCombatMath.ValidStrikeLimb(Limb, Person) || charging || armed) return false;
            if(Mode==KiFistMode.God && !GodModeAvailable){Mode=KiFistMode.Ki;return false;}
            if (!ki) ki = Person.GetComponent<KiSystem>();
            if (!ki || KiCombatMath.MaxKi(ki) <= 0f) return false;
            if (InfuseAllEnergy && (!reservoir || reservoir.TotalReceived <= 0)) return false;
            chargeSeconds = overchargeEnergy = 0f; armedSupportMultiplier = 1f;
            charging = true; heldCharge=continuousSeen = lunged = false; startFrame = Time.frameCount;
            directKey = KeyCode.F; directSecondary = KeyCode.None;
            InputAction action;
            if (InputSystem.Actions.TryGetValue("activateDirect",out action)) { directKey=action.Key;directSecondary=action.SecondaryKey; }
            directHeld=Input.GetKey(directKey) && (directSecondary==KeyCode.None || Input.GetKey(directSecondary));
            return true;
        }
        void Update()
        {
            if (!Equipped || !KiCombatMath.ValidStrikeLimb(Limb, Person)) { Cancel(); return; }
            if(Mode==KiFistMode.God && !GodModeAvailable){Cancel();Mode=KiFistMode.Ki;}
            if (!KiCombatMath.CanAct())
            {
                // Freeze paid, prepared energy while choosing a technique on the other hand.
                // A held charge is armed once so it cannot keep spending ki behind the menu.
                if (Global.main && Global.main.Paused && KiCombatMath.HasFocus) { HideVisual(); return; }
                if (KiCombatMath.HasFocus && Global.main && (Global.ActiveUiBlock || CombatAbilityWheel.AnyMenuCaptured
                    || Global.main.GetPausedMenu() || DialogBox.IsAnyDialogboxOpen))
                { if (charging) Arm(); HideVisual(); return; }
                Cancel(); return;
            }
            if(!charging)recovery=Mathf.Max(0,recovery-KiCombatMath.Step(Time.deltaTime));
            bool directContinuous=directHeld && Input.GetKey(directKey) && (directSecondary==KeyCode.None || Input.GetKey(directSecondary));
            bool continuous = (Limb.PhysicalBehaviour && Limb.PhysicalBehaviour.IsBeingUsedContinuously()) || directContinuous;
            if (!charging && !armed && continuous) Activate();
            if (charging)
            {
                if (continuous) continuousSeen = true;
                if (!heldCharge && (continuousSeen || directHeld) && !continuous && Time.frameCount > startFrame + 1) Arm();
                else
                {
                    ChargeStep(Time.deltaTime);
                    if ((!heldCharge && !continuousSeen && chargeSeconds >= 3f) || chargeSeconds >= 6f) Arm();
                }
            }
            if (armed) { armedLifetime -= KiCombatMath.Step(Time.deltaTime); if (armedLifetime <= 0f) Cancel(); }
            if (charging || armed || IsReady) ShowVisual(); else HideVisual();
        }
        public void ChargeStep(float dt)
        {
            if (!charging || !Equipped || !KiCombatMath.ValidStrikeLimb(Limb, Person) || !ki) return;
            dt = KiCombatMath.Step(dt); if (dt <= 0f) return;
            recovery=Mathf.Max(0,recovery-dt);
            float maximum = KiCombatMath.MaxKi(ki);
            float after = KiEnergyMath.AdvanceCharge(chargeSeconds, dt * Mathf.Clamp(KiCombatMath.Finite(ChargeRate, 1f), .1f, 6f), true);
            float cost = KiCombatMath.OwnChargeCost(maximum, chargeSeconds, after, DBPU.KiAttackCostMultiplier)*(Mode==KiFistMode.God?1.35f:1);
            if (cost > Mathf.Max(0f, KiCombatMath.Finite(ki.currentKi))) { Arm(); return; }
            ki.currentKi = Mathf.Max(0f, ki.currentKi - cost); chargeSeconds = after;
        }
        public void Arm()
        {
            if (!charging) return;
            charging = heldCharge = false;
            if (chargeSeconds < 0.25f) { Cancel(); return; }
            armedSupportMultiplier = reservoir ? reservoir.PowerMultiplier : 1f;
            float needed = KiCombatMath.MaxKi(ki) * 0.1f;
            if (InfuseAllEnergy && reservoir) infusedEnergy = reservoir.ConsumeSupport(reservoir.TotalReceived);
            // Support is consumed at arming once; a second hand cannot spend the same donation.
            if (!InfuseAllEnergy && chargeSeconds >= 5.95f && needed > 0f && reservoir && reservoir.TotalReceived >= needed)
                overchargeEnergy = reservoir.ConsumeSupport(Mathf.Min(reservoir.TotalReceived, needed * 2f));
            armed = true; lunged = false; armedLifetime = chargeSeconds >= 2.9f ? 15f : 10f;
        }
        void Lunge()
        {
            if (lunged || !armed || !Person || Person.Limbs == null) return;
            lunged = true; Vector2 direction = KiCombatMath.StrikeDirection(Limb);
            foreach (var member in Person.Limbs)
            {
                if (!member) continue; var body = member.GetComponent<Rigidbody2D>();
                if (!body || body.bodyType != RigidbodyType2D.Dynamic) continue;
                float forward = Vector2.Dot(body.velocity, direction);
                float add = Mathf.Clamp((IsOvercharged ? 20f : 13f) - forward, 0f, 13f);
                body.AddForce(direction * body.mass * add, ForceMode2D.Impulse);
            }
        }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if ((!armed && !IsReady) || !Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidStrikeLimb(Limb, Person) || !collision.gameObject) return;
            var shell=collision.gameObject.GetComponent<KiShieldSurface>();
            if(shell && shell.Owner && shell.Owner.Person!=Person)
            {
                if(collision.relativeVelocity.sqrMagnitude>=2.25f)StrikeShield(shell.Owner,collision.contactCount>0?collision.GetContact(0).point:(Vector2)collision.transform.position);
                return;
            }
            var target = collision.gameObject.GetComponent<LimbBehaviour>();
            if (!target || !target.Person || target.Person == Person) return;
            // Resting contact does not waste the attack. A deliberate punch or dash is required.
            var body = GetComponent<Rigidbody2D>();
            if (collision.relativeVelocity.sqrMagnitude < 2.25f && (!body || body.velocity.sqrMagnitude < 6.25f)) return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)target.transform.position;
            var opposingFist=target.GetComponent<KiFistHand>();
            if(opposingFist && RealityRift.TryFistClash(this,opposingFist,point))return;
            DischargeForTest(target, point);
        }
        void FixedUpdate()
        {
            Vector2 point=KiCombatMath.StrikePoint(Limb);
            var approachBody=GetComponent<Rigidbody2D>();
            realityVelocity=approachBody?approachBody.GetPointVelocity(point):Vector2.zero;realityVelocityTime=Time.fixedTime;
            if(!havePrevious){previousPoint=point;havePrevious=true;return;}
            Vector2 delta=point-previousPoint,from=previousPoint;previousPoint=point;
            if((!armed && !IsReady) || !KiCombatMath.CanAct() || delta.sqrMagnitude<.0009f || delta.sqrMagnitude>144)return;
            var body=GetComponent<Rigidbody2D>();if(!body || body.velocity.sqrMagnitude<2.25f)return;
            var collider=GetComponent<Collider2D>();float radius=collider?Mathf.Clamp(Mathf.Min(collider.bounds.extents.x,collider.bounds.extents.y),.035f,.35f):.08f;
            int count=Physics2D.CircleCastNonAlloc(from,radius,delta.normalized,sweepHits,delta.magnitude);
            int nearest=-1;float distance=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var c=sweepHits[i].collider;if(!c || c.isTrigger)continue;
                var limb=c.GetComponent<LimbBehaviour>();var shell=c.GetComponent<KiShieldSurface>();
                if((limb && limb.Person==Person) || (shell && shell.Owner && shell.Owner.Person==Person))continue;
                if(sweepHits[i].distance<distance){distance=sweepHits[i].distance;nearest=i;}
            }
            if(nearest<0)return;var hit=sweepHits[nearest];var barrier=hit.collider.GetComponent<KiShieldSurface>();
            if(barrier && barrier.Owner)StrikeShield(barrier.Owner,hit.point);
            else
            {
                var target=hit.collider.GetComponent<LimbBehaviour>();
                if(target)
                {
                    var opposingFist=target.GetComponent<KiFistHand>();
                    if(!opposingFist || !RealityRift.TryFistClash(this,opposingFist,hit.point))DischargeForTest(target,hit.point);
                }
            }
        }
        bool CommitStrike()
        {
            if(!Equipped || charging || recovery>0 || !KiCombatMath.ValidStrikeLimb(Limb,Person))return false;
            if(Mode==KiFistMode.God && !GodModeAvailable){Cancel();Mode=KiFistMode.Ki;return false;}
            if(!armed)
            {
                if(!IsReady)return false;
                ki.currentKi=Mathf.Max(0,ki.currentKi-SwingCost);chargeSeconds=0;
                armedSupportMultiplier=reservoir?reservoir.PowerMultiplier:1;
            }
            recovery=.22f;return true;
        }
        float AttackMultiplier()
        {
            return KiCombatMath.NativeMultiplier(Person)*Mathf.Max(armedSupportMultiplier,reservoir?reservoir.PowerMultiplier:1)
                *CombatPowerController.Mode(Person)*CombatPowerController.OutputFor(Person)*(Mode==KiFistMode.God?1.75f:1);
        }
        double StrikeDamage(float attack)
        {
            return (double)Mathf.Max(1,baseHealth)*attack*Mathf.Lerp(.16f,.9f,Charge01)*(1+Overcharge01*6)
                *Mathf.Clamp(KiCombatMath.Finite(DBPU.DamageMultiplier,1),0,1000);
        }
        public bool StrikeShield(KiShield shield,Vector2 point)
        {
            if(!shield || !shield.Using || shield.Person==Person || !CommitStrike())return false;
            double damage=StrikeDamage(AttackMultiplier());
            if(infusedEnergy>0)damage*=1+Mathf.Sqrt(infusedEnergy/Mathf.Max(1,KiCombatMath.MaxKi(ki)))*4;
            bool blocked=shield.ReceiveDamage(damage,point,Limb.GetInstanceID());
            int tier=infusedEnergy>0 || IsOvercharged?2:Charge01>=.5f?1:0;
            infusedEnergy=0;KiFistImpactFx.Spawn(point,((Vector2)shield.Centre-point).normalized,blocked?Mathf.Max(0,tier-1):tier,EffectTint);Cancel();return true;
        }
        public bool DischargeForTest(LimbBehaviour target, Vector2 point)
        {
            if (!target || !target.Person || target.Person == Person || !CommitStrike()) return false;
            bool overcharged = IsOvercharged;
            float charge = Charge01;
            float supportAtStrike = reservoir ? reservoir.PowerMultiplier : 1f;
            // A consumed support bonus is retained for this fist without multiplying it twice.
            // Compose the dedicated support factor explicitly: FinalMultiplier may still
            // contain its previous-frame value immediately after support was consumed.
            float attackMultiplier = AttackMultiplier();
            double raw = StrikeDamage(attackMultiplier);
            float damage = KiCombatMath.Cap(raw,1e35f);
            float victimHealth = Mathf.Max(1f, KiCombatMath.Finite(target.InitialHealth, 1e32f));
            float damageFraction = KiCombatMath.Cap((double)damage / victimHealth, 1000f);
            // Existing transformed durability is held in InitialHealth; apply the same minimum
            // damage fraction as DBPU's other physical attacks, without flat-health execution.
            if (damageFraction > 0.001f)
            {
                var victimBody = target.GetComponent<Rigidbody2D>();
                Vector2 impactVelocity = RealityApproachVelocity - (victimBody ? victimBody.GetPointVelocity(point) : Vector2.zero);
                CombatDismemberment.TryMeleeSever(Person, target, damage, impactVelocity.magnitude, point, impactVelocity);
                target.Damage(damage);
                if (target.SkinMaterialHandler)
                    target.SkinMaterialHandler.AddDamagePoint(DamageType.Blunt, point, Mathf.Clamp(damageFraction * 55f, 0f, 45f));
                Vector2 direction = ((Vector2)target.transform.position - KiCombatMath.StrikePoint(Limb)).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = KiCombatMath.StrikeDirection(Limb);
                // Swings launch the opponent (reference behaviour); overcharged blows also lift.
                var swingBody = GetComponent<Rigidbody2D>();
                float swing = swingBody ? swingBody.GetPointVelocity(point).magnitude : 0f;
                Vector2 strikeDirection = KiCombatMath.StrikeDirection(Limb);
                if (swingBody && swing > 1f) strikeDirection = swingBody.GetPointVelocity(point).normalized;
                direction = (direction * .45f + strikeDirection * .55f).normalized;
                if (overcharged) direction = (direction + Vector2.up * .22f).normalized;
                float launch = overcharged ? 34f : charge >= .5f ? 22f : 13f;
                float speed = Mathf.Clamp(Mathf.Max(launch * Mathf.Sqrt(Mathf.Clamp(damageFraction, .05f, 4f)), launch * .55f + swing * .35f), 0f, 60f)
                    * Mathf.Clamp(KiCombatMath.Finite(DBPU.Universal_KnockbackMultiplier, 1f), 0f, 5f);
                foreach (var member in target.Person.Limbs)
                {
                    if (!member) continue; var body = member.GetComponent<Rigidbody2D>();
                    if (!body || body.bodyType != RigidbodyType2D.Dynamic) continue;
                    // Spread a bounded velocity change across this victim's body only.
                    body.AddForce(direction * body.mass * Mathf.Min(speed, 75f), ForceMode2D.Impulse);
                }
            }
            float payload=infusedEnergy; infusedEnergy=0;
            if(payload>0)
            {
                float payloadScale=1f+Mathf.Sqrt(payload/Mathf.Max(1,KiCombatMath.MaxKi(ki)))*4f;
                KiInternalDisruption.Inflict(target.Person,attackMultiplier,KiCombatMath.Cap(raw*payloadScale,1e35f));
            }
            Vector2 impactDirection=((Vector2)target.transform.position-KiCombatMath.StrikePoint(Limb)).normalized;
            if(impactDirection.sqrMagnitude<.001f)impactDirection=KiCombatMath.StrikeDirection(Limb);
            int impactTier=payload>0 || overcharged?2:charge>=.5f?1:0;
            if(damageFraction<=.001f)impactTier=Mathf.Max(0,impactTier-1);
            KiFistImpactFx.Spawn(point,impactDirection,impactTier,EffectTint);
            if(impactTier==2 && Person)DbpuWorldLabel.Hide(Person);
            if(RealityRift.Active && damageFraction>.001f)RealityRift.Active.RequestExit(Person,target.Person,point,overcharged);
            Cancel(); return true;
        }
        void ShowVisual()
        {
            if (!visual) visual = KiFistGlowFx.Create(this);
            if (visual) visual.Show();
        }
        void HideVisual() { if (visual) visual.Hide(); }
        public void Cancel()
        {
            if(infusedEnergy>0 && reservoir)reservoir.ReturnSupport(infusedEnergy);
            infusedEnergy=0;
            charging = armed = heldCharge = continuousSeen = lunged = false; ChargeRate = 1f;
            chargeSeconds = overchargeEnergy = 0f; armedSupportMultiplier = 1f; HideVisual();
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { if (visual) Destroy(visual.gameObject); if (useTrigger) Destroy(useTrigger); }
    }

    public class KiShareHand : MonoBehaviour
    {
        public bool Equipped;
        public LimbBehaviour Limb { get; private set; }
        public PersonBehaviour Person { get; private set; }
        public PersonBehaviour Receiver { get; private set; }
        public SpiritBombProjectile BombReceiver { get; private set; }
        public bool IsSharing { get; private set; }
        public string Status { get { return IsSharing && BombReceiver ? "GATHERING KI → SPIRIT BOMB" : IsSharing && Receiver ? "SHARING KI → " + Receiver.name : "Aim at a fighter or Spirit Bomb, then hold Use (20% reserve)"; } }
        public const float Range = 18f;
        readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        AllyKiReservoir reservoir;
        UseEventTrigger useTrigger;
        KiCombatVisual visual;
        bool active, continuousSeen;
        int startFrame;
        KeyCode directKey, directSecondary;
        bool directHeld;
        int lastActivationFrame = -1;
        public static KiShareHand GetOrCreate(LimbBehaviour limb)
        {
            if (!KiHandLoadout.IsHand(limb)) return null;
            var hand = limb.GetComponent<KiShareHand>(); if (!hand) hand = limb.gameObject.AddComponent<KiShareHand>();
            hand.Bind(limb); return hand;
        }
        void Awake() { Bind(GetComponent<LimbBehaviour>()); }
        void Bind(LimbBehaviour limb)
        {
            Limb = limb; Person = limb ? limb.Person : null;
            if (!Person) return; reservoir = AllyKiReservoir.GetOrCreate(Person);
            if (!useTrigger) { useTrigger = gameObject.AddComponent<UseEventTrigger>(); useTrigger.Action = Activate; }
        }
        public void Activate()
        {
            if (!Equipped || !KiCombatMath.CanAct() || !KiCombatMath.ValidHand(Limb, Person)) return;
            if (lastActivationFrame == Time.frameCount) return;
            lastActivationFrame = Time.frameCount;
            active = !active; continuousSeen = false; startFrame = Time.frameCount;
            directKey=KeyCode.F;directSecondary=KeyCode.None;
            InputAction action;
            if(InputSystem.Actions.TryGetValue("activateDirect",out action)){directKey=action.Key;directSecondary=action.SecondaryKey;}
            directHeld=Input.GetKey(directKey) && (directSecondary==KeyCode.None || Input.GetKey(directSecondary));
        }
        void Update()
        {
            if (!Equipped || !KiCombatMath.ValidHand(Limb, Person)) { Cancel(); return; }
            if (!KiCombatMath.CanAct())
            {
                if (Global.main && Global.main.Paused && KiCombatMath.HasFocus) { IsSharing = false; HideVisual(); return; }
                Cancel(); return;
            }
            bool continuous = (Limb.PhysicalBehaviour && Limb.PhysicalBehaviour.IsBeingUsedContinuously())
                || (directHeld && Input.GetKey(directKey) && (directSecondary==KeyCode.None || Input.GetKey(directSecondary)));
            if (!active && continuous) Activate();
            if (active && continuous) continuousSeen = true;
            if (active && (continuousSeen || directHeld) && !continuous && Time.frameCount > startFrame + 1) active = false;
            if (active) TransferStep(Time.deltaTime); else { IsSharing = false; Receiver = null; BombReceiver = null; HideVisual(); }
        }
        public float TransferStep(float dt)
        {
            IsSharing = false; Receiver = null; BombReceiver = null;
            if (!Equipped || !KiCombatMath.ValidHand(Limb, Person) || !reservoir) { HideVisual(); return 0f; }
            dt = KiCombatMath.Step(dt); if (dt <= 0f) { HideVisual(); return 0f; }
            Vector2 origin = KiCombatMath.HandPoint(Limb), direction = -(Vector2)Limb.transform.up;
            int count = Physics2D.RaycastNonAlloc(origin, direction, hits, Range);
            float closest = Range + 1f; LimbBehaviour candidate = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; if (!hit.collider || hit.collider.isTrigger || hit.collider.GetComponent<KiShieldSurface>()) continue;
                var limb = hit.collider.GetComponent<LimbBehaviour>();
                if (limb && limb.Person == Person) continue;
                if (hit.distance >= closest) continue;
                closest = hit.distance; candidate = limb;
            }
            float bombDistance;
            var bomb = SpiritBombProjectile.OnRay(origin, direction, Range, out bombDistance);
            if (bomb && bombDistance <= closest + .001f)
            {
                float sent = reservoir.DonateToBomb(bomb, dt);
                if (sent <= 0) { HideVisual(); return 0; }
                BombReceiver = bomb; IsSharing = true;
                if (!visual) visual = KiCombatVisual.Create("DBPU Genkidama Ki Stream", transform);
                if (visual) visual.ShowTransfer(origin, origin + direction.normalized * bombDistance);
                return sent;
            }
            // The first solid obstruction blocks the stream; the first living fighter in the hand's direction
            // receives energy without any class or team assignment.
            if (!candidate || !candidate.Person || candidate.Health <= 0f || !candidate.Person.IsAlive()
               ) { HideVisual(); return 0f; }
            var receiver = AllyKiReservoir.GetOrCreate(candidate.Person);
            if (!receiver || Vector2.Distance(origin, candidate.transform.position) > Range) { HideVisual(); return 0f; }
            float transferred = reservoir.DonateTo(receiver, dt);
            if (transferred <= 0f) { HideVisual(); return 0f; }
            Receiver = candidate.Person; IsSharing = true;
            var channel = candidate.Person.GetComponent<SpiritBombChannel>();
            if (channel && channel.IsCharging) BombReceiver = channel.ChargingBomb;
            if (!visual) visual = KiCombatVisual.Create("DBPU Allied Ki Stream", transform);
            if (visual) visual.ShowTransfer(origin, BombReceiver ? BombReceiver.transform.position : candidate.transform.position);
            return transferred;
        }
        public void Cancel() { active = continuousSeen = IsSharing = false; Receiver = null; BombReceiver = null; HideVisual(); }
        void HideVisual() { if (visual) visual.gameObject.SetActive(false); }
        void OnDisable() { Cancel(); }
        void OnDestroy() { if (visual) Destroy(visual.gameObject); if (useTrigger) Destroy(useTrigger); }
    }

    // Bounded procedural effects: one mesh, four lines, no particles instantiated per frame.
    public class KiCombatVisual : MonoBehaviour
    {
        const int Segments = 48;
        static Material sharedMaterial;
        readonly Vector3[] vertices = new Vector3[Segments + 2];
        readonly Color[] colours = new Color[Segments + 2];
        readonly LineRenderer[] lines = new LineRenderer[4];
        Mesh mesh;
        MeshRenderer meshRenderer;
        bool impact;
        float impactAge;
        bool impactOvercharged, impactHit;
        public static KiCombatVisual Create(string name, Transform parent)
        {
            if (!sharedMaterial)
            {
                var shader = Shader.Find("Sprites/Default"); if (!shader) return null;
                sharedMaterial = new Material(shader) { name = "DBPU Ki Combat Shared" };
                sharedMaterial.mainTexture = Texture2D.whiteTexture;
            }
            var go = new GameObject(name); if (parent) go.transform.SetParent(parent, false);
            return go.AddComponent<KiCombatVisual>();
        }
        void Awake()
        {
            mesh = new Mesh { name = "Ki combat radial glow" }; mesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.sortingLayerName = "Top"; meshRenderer.sortingOrder = 32;
            int[] triangles = new int[Segments * 3];
            for (int i = 0; i < Segments; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            mesh.vertices = vertices; mesh.colors = colours; mesh.triangles = triangles;
            for (int i = 0; i < lines.Length; i++)
            {
                var child = new GameObject("Ki energy trace " + i); child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>(); lines[i] = line;
                line.sharedMaterial = sharedMaterial; line.useWorldSpace = true;
                line.sortingLayerName = "Top"; line.sortingOrder = 33;
                line.numCapVertices = 2; line.numCornerVertices = 2; line.positionCount = 0;
            }
        }
        void Position(Vector2 position)
        {
            gameObject.SetActive(true); transform.position = new Vector3(position.x, position.y, -0.05f);
            transform.rotation = Quaternion.identity;
            // Effects stay in world units even on characters scaled or flipped by a transformation.
            var scale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f);
        }
        void Disc(float radius, Color centre)
        {
            meshRenderer.enabled = radius > 0f;
            vertices[0] = Vector3.zero; colours[0] = centre;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                colours[i + 1] = new Color(centre.r, centre.g, centre.b, 0f);
            }
            mesh.vertices = vertices; mesh.colors = colours; mesh.RecalculateBounds();
        }
        void Ring(LineRenderer line, Vector2 centre, float radius, float phase, Color colour, float width, float ellipse = 1f)
        {
            line.positionCount = 33; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = colour;
            for (int i = 0; i < 33; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / 32f;
                line.SetPosition(i, new Vector3(centre.x + Mathf.Cos(angle) * radius, centre.y + Mathf.Sin(angle) * radius * ellipse, -0.07f));
            }
        }
        public void ShowFist(Vector2 position, float charge, float extra, bool overcharged)
        {
            Position(position); float time = Time.time;
            float radius = Mathf.Lerp(0.12f, 0.46f, charge) + extra * 0.22f;
            radius *= 1f + Mathf.Sin(time * 18f) * 0.035f;
            Color gold = Color.Lerp(new Color(1f, 0.83f, 0.24f, 0.9f), new Color(0.77f, 1f, 0.75f, 0.95f), extra * 0.32f);
            Disc(radius * 1.9f, new Color(1f, 0.95f, 0.68f, 0.92f));
            Ring(lines[0], position, radius * 0.63f, time * 4f, new Color(1f, 1f, 0.93f, 0.95f), 0.03f + charge * 0.02f);
            Ring(lines[1], position, radius, time * -4f, gold, 0.019f + extra * 0.018f, 0.55f + 0.25f * Mathf.Sin(time * 3f));
            for (int arc = 0; arc < 2; arc++)
            {
                var line = lines[arc + 2]; line.positionCount = 13;
                line.startWidth = line.endWidth = 0.008f + extra * 0.012f;
                line.startColor = line.endColor = overcharged ? Color.white : gold;
                for (int i = 0; i < 13; i++)
                {
                    float angle = time * (arc == 0 ? 6f : -5f) + i * 0.33f + arc * Mathf.PI;
                    float r = radius * (1.12f + Mathf.Sin(i * 9f + Mathf.Floor(time * 24f)) * (0.1f + 0.18f * extra));
                    line.SetPosition(i, new Vector3(position.x + Mathf.Cos(angle) * r, position.y + Mathf.Sin(angle) * r, -0.08f));
                }
            }
        }
        public void ShowTransfer(Vector2 from, Vector2 to)
        {
            Position(to); Disc(0.38f, new Color(0.7f, 1f, 0.74f, 0.58f));
            float time = Time.time; Vector2 delta = to - from;
            Vector2 normal = delta.sqrMagnitude > 0.01f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
            for (int lane = 0; lane < 4; lane++)
            {
                var line = lines[lane]; line.positionCount = 10;
                line.startWidth = 0.035f; line.endWidth = 0.012f;
                line.startColor = new Color(0.65f, 1f, 0.73f, 0.05f); line.endColor = new Color(1f, 1f, 0.88f, 0.9f);
                float head = Mathf.Repeat(time * 0.65f + lane * 0.25f, 1f);
                for (int i = 0; i < 10; i++)
                {
                    float t = Mathf.Clamp01(head - (9 - i) * 0.012f);
                    Vector2 point = from + delta * t + normal * Mathf.Sin(t * 17f + time * 6f + lane) * Mathf.Sin(t * Mathf.PI) * 0.12f;
                    line.SetPosition(i, new Vector3(point.x, point.y, -0.06f));
                }
            }
        }
        public static void Impact(Vector2 point, bool overcharged, bool hit)
        {
            var effect = Create("DBPU Ki Fist Impact", null); if (!effect) return;
            effect.impact = true; effect.impactOvercharged = overcharged; effect.impactHit = hit; effect.Position(point);
        }
        void Update()
        {
            if (!impact || (Global.main && Global.main.Paused)) return;
            impactAge += Time.deltaTime; if (impactAge >= 0.9f) { Destroy(gameObject); return; }
            float t = impactAge / 0.9f, alpha = (1f - t) * (1f - t);
            float size = impactOvercharged ? 2.8f : 1.25f;
            if (!impactHit) size *= 0.45f;
            Disc(size * (0.16f + t * 1.2f), new Color(1f, 0.96f, 0.7f, alpha));
            for (int i = 0; i < 4; i++)
                Ring(lines[i], transform.position, size * (t * (1.6f + i * 0.28f) + i * 0.05f), i,
                    new Color(1f, 0.91f - i * 0.055f, 0.54f, alpha * (1f - i * 0.15f)), Mathf.Lerp(0.045f, 0.008f, t), i % 2 == 0 ? 1f : 0.65f);
        }
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
