using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The native Ball power remains the equipped technique. Its old fixed-size charge
    // is replaced only for the two compatible Genkidama IDs.
    public sealed class SpiritBombChannel : MonoBehaviour
    {
        public SpiritBombProjectile ChargingBomb { get; private set; }
        public bool IsCharging { get { return ChargingBomb && ChargingBomb.Phase == SpiritBombPhase.Charging; } }
        public bool IsReady { get { return ChargingBomb && ChargingBomb.Phase == SpiritBombPhase.Ready; } }
        public bool HasPendingBomb { get { return IsCharging || IsReady; } }
        public PersonBehaviour Person { get { return person; } }
        public float ChargeSeconds { get; private set; }
        PersonBehaviour person;
        KiSystem ki;
        LimbBehaviour arm;
        bool heldDirect, continuousSeen, releaseRequested;
        KeyCode key, secondary;
        float headOffset;
        int startFrame, lastActivationFrame = -1;

        public static SpiritBombChannel GetOrCreate(PersonBehaviour person)
        {
            if (!person) return null;
            SpiritBombTargeting.Ensure();
            var channel = person.GetComponent<SpiritBombChannel>();
            return channel ? channel : person.gameObject.AddComponent<SpiritBombChannel>();
        }
        void Awake() { person = GetComponent<PersonBehaviour>(); ki = GetComponent<KiSystem>(); }
        public void Activate(LimbBehaviour limb)
        {
            if (!KiCombatMath.CanAct() || lastActivationFrame == Time.frameCount || IsReady) return;
            lastActivationFrame = Time.frameCount;
            if (IsCharging)
            {
                if (heldDirect && Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary))) return;
                releaseRequested = true; return;
            }
            if (!BeginCharge(limb)) return;
            key = KeyCode.F; secondary = KeyCode.None;
            InputAction action;
            if (InputSystem.Actions.TryGetValue("activateDirect", out action)) { key = action.Key; secondary = action.SecondaryKey; }
            heldDirect = Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary));
        }
        Vector2 TorsoPoint()
        {
            var upper = person.transform.Find("Body/UpperBody");
            var middle = person.transform.Find("Body/MiddleBody");
            if (upper && middle) return ((Vector2)upper.position + (Vector2)middle.position) * .5f;
            return upper ? (Vector2)upper.position : (Vector2)person.transform.position;
        }
        public bool BeginCharge(LimbBehaviour limb)
        {
            if (HasPendingBomb || !person || !ki || KiCombatMath.MaxKi(ki) <= 0 || !KiCombatMath.ValidHand(limb, person) || ki.currentKi <= 0) return false;
            var native = GetComponent<BallBehaviour>();
            if (!native || !native.Active || !IsSpirit(native.EffectID) || !KiHandLoadout.LegacyAllowed(limb, native.EffectID)) return false;
            var bomb = SpiritBombProjectile.Create(person, limb);
            if (!bomb) return false;
            arm = limb; ChargingBomb = bomb; ChargeSeconds = 0;
            var head = person.transform.Find("Head");
            headOffset = head ? Mathf.Max(.3f, head.position.y - TorsoPoint().y) : .8f;
            heldDirect = continuousSeen = releaseRequested = false; startFrame = Time.frameCount;
            FollowOwner(); bomb.SnapGatherAnchor(); return true;
        }
        public static bool IsSpirit(string id)
        { return id == "Spirit Bomb" || id == "Super Spirit Bomb" || id == "SpiritBomb" || id == "SuperSpiritBomb"; }
        void Update()
        {
            if (!HasPendingBomb) return;
            if (!KiCombatMath.ValidHand(arm, person)) { CancelCharge(); return; }
            var native = GetComponent<BallBehaviour>();
            if (!native || !native.Active || !IsSpirit(native.EffectID)) { CancelCharge(); return; }
            if (!KiCombatMath.HasFocus) { if (IsCharging) FreezeCharge(); return; }
            if (!KiCombatMath.CanAct()) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelCharge(); return; }
            if (IsReady) return;
            bool continuous = (heldDirect && Input.GetKey(key) && (secondary == KeyCode.None || Input.GetKey(secondary)))
                || (arm.PhysicalBehaviour && arm.PhysicalBehaviour.IsBeingUsedContinuously());
            if (continuous) continuousSeen = true;
            if ((heldDirect || continuousSeen) && !continuous && Time.frameCount > startFrame + 1) releaseRequested = true;
            // Stop spending on the actual release frame. Release never fires the sphere.
            if (releaseRequested) { FreezeCharge(); return; }
            ChargeStep(Time.deltaTime);
            if (ChargeSeconds >= 300) FreezeCharge();
        }
        void LateUpdate() { if (IsCharging) FollowOwner(); }
        public float ChargeStep(float dt)
        {
            if (!IsCharging || !ki) return 0;
            dt = KiCombatMath.Step(dt); ChargeSeconds += dt;
            float cost = SpiritBombMath.ChargeCost(KiCombatMath.MaxKi(ki), dt, DBPU.KiAttackCostMultiplier);
            float paid = Mathf.Min(Mathf.Max(0, KiCombatMath.Finite(ki.currentKi)), cost);
            float equivalent = cost > 0 ? paid / cost * KiCombatMath.MaxKi(ki) * .018f * dt : KiCombatMath.MaxKi(ki) * .018f * dt;
            float accepted = ChargingBomb.AddEnergy(equivalent, false);
            if (equivalent > 0) ki.currentKi = Mathf.Max(0, ki.currentKi - paid * (accepted / equivalent));
            ChargingBomb.ChargeSeconds = ChargeSeconds;
            FollowOwner(); return paid;
        }
        public void FollowOwner()
        {
            if (!IsCharging || !person) return;
            Vector2 top = TorsoPoint() + Vector2.up * headOffset;
            ChargingBomb.SetGatherAnchor(top + Vector2.up * (ChargingBomb.Radius + 1.25f));
        }
        public bool FreezeCharge()
        {
            if (!IsCharging) return false;
            if (ChargingBomb.Energy <= 0) { CancelCharge(); return false; }
            ChargingBomb.Prepare(); heldDirect = continuousSeen = releaseRequested = false;
            SpiritBombTargeting.Register(this); return true;
        }
        public bool LaunchAt(PersonBehaviour target)
        {
            if (!IsReady || !target || target == person || !target.IsAlive()) return false;
            var bomb = ChargingBomb; ChargingBomb = null;
            SpiritBombTargeting.Unregister(this); bomb.LaunchAt(target); return true;
        }
        // Compatibility for deterministic physics checks; gameplay launches only via target selection.
        public bool Launch(Vector2 direction)
        {
            if (IsCharging && !FreezeCharge()) return false;
            if (!IsReady) return false;
            var bomb = ChargingBomb; ChargingBomb = null;
            SpiritBombTargeting.Unregister(this); bomb.Launch(direction); return true;
        }
        public void CancelCharge()
        {
            SpiritBombTargeting.Unregister(this);
            if (HasPendingBomb) Destroy(ChargingBomb.gameObject);
            ChargingBomb = null; heldDirect = continuousSeen = releaseRequested = false;
        }
        void OnDisable() { CancelCharge(); }
        void OnDestroy() { CancelCharge(); }
    }

    public enum SpiritBombPhase { Charging, Ready, Flight, Containment, Detonating, Finished }

    public sealed class SpiritBombProjectile : MonoBehaviour
    {
        static readonly List<SpiritBombProjectile> live = new List<SpiritBombProjectile>();
        public SpiritBombPhase Phase { get; private set; }
        public SpiritNukeShockwave DetonationShockwave { get; private set; }
        public PersonBehaviour Owner { get; private set; }
        public float Energy { get; private set; }
        public float DonatedEnergy { get; private set; }
        public float ChargeSeconds;
        public float ReferenceKi { get; private set; }
        public SpiritOrb Orb { get; private set; }
        public Vector2 CurrentVelocity { get { return velocity; } }
        bool held;
        public float Radius { get { return SpiritBombMath.Radius(Energy, ReferenceKi); } }
        public float Damage { get; private set; }
        public int PulsesApplied { get; private set; }
        public bool FinalApplied { get; private set; }
        public bool CanReceive { get { return Phase == SpiritBombPhase.Charging && Owner && Owner.IsAlive() && isActiveAndEnabled; } }
        public int CapturedCount { get { return victims.Count; } }
        public static int ActiveCount { get { Prune(); return live.Count; } }
        readonly Collider2D[] overlaps = new Collider2D[512];
        readonly RaycastHit2D[] casts = new RaycastHit2D[128];
        readonly List<PersonBehaviour> victims = new List<PersonBehaviour>(24);
        readonly HashSet<int> damaged = new HashSet<int>();
        readonly HashSet<int> pulled = new HashSet<int>();
        readonly Collider2D[] gravityColliders = new Collider2D[1024];
        public float GravityRange { get { return Mathf.Min(160,Radius*2.2f+3); } }
        CircleCollider2D trigger;
        Rigidbody2D body;
        SpiritBombVfx visual;
        AudioSource sound;
        Vector2 velocity;
        public Vector2 TravelDirection { get { return velocity.sqrMagnitude > .001f ? velocity.normalized : Vector2.right; } }
        float age, phaseAge, nextPulse = .35f, nextScan;
        float launchPower;
        Vector2 gatherAnchor, gatherVelocity;
        PersonBehaviour homingTarget;
        float flightLimit = 12;
        UnityEngine.SceneManagement.Scene birthScene;

        static void Prune() { live.RemoveAll(b => !b || b.Phase >= SpiritBombPhase.Detonating); }
        public static SpiritBombProjectile Create(PersonBehaviour owner, LimbBehaviour arm)
        {
            Prune(); if (!owner || !arm || live.Count >= 8) return null;
            var go = new GameObject("DBPU Spirit Bomb - gathering energy"); go.layer = 9;
            go.transform.position = arm.transform.position + Vector3.up * 2;
            var bomb = go.AddComponent<SpiritBombProjectile>();
            bomb.Owner = owner; bomb.ReferenceKi = Mathf.Max(1, KiCombatMath.MaxKi(owner.GetComponent<KiSystem>()));
            bomb.birthScene = owner.gameObject.scene;
            if (bomb.birthScene.IsValid() && bomb.birthScene.isLoaded && bomb.birthScene.name != "DontDestroyOnLoad")
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, bomb.birthScene);
            bomb.body = go.AddComponent<Rigidbody2D>(); bomb.body.bodyType = RigidbodyType2D.Kinematic;
            bomb.body.interpolation = RigidbodyInterpolation2D.None;
            bomb.body.gravityScale = 0; bomb.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            bomb.trigger = go.AddComponent<CircleCollider2D>(); bomb.trigger.isTrigger = true; bomb.trigger.enabled = false;
            if (OrbFx.Active) bomb.Orb = new SpiritOrb(bomb, owner, "Spirit Bomb");
            bomb.visual = SpiritBombVfx.Create(bomb);
            bomb.sound = go.AddComponent<AudioSource>();
            if (Global.main) { Global.main.AddAudioSource(bomb.sound, false); bomb.sound.outputAudioMixerGroup = Global.main.SoundEffects; }
            bomb.sound.spatialBlend = 1; bomb.sound.maxDistance = 200; bomb.sound.volume = .22f;
            bomb.sound.clip = DBPU.Attack_GalickGun_Charging; bomb.sound.loop = true;
            if (bomb.sound.clip) bomb.sound.Play();
            live.Add(bomb); return bomb;
        }
        public float AddEnergy(float offered, bool donated)
        {
            if (!CanReceive) return 0;
            float accepted = SpiritBombMath.Accept(offered, Energy);
            Energy += accepted;
            if (donated) DonatedEnergy = KiCombatMath.Cap((double)DonatedEnergy + accepted);
            if (donated && accepted > 0)
            {
                if (visual) visual.DonationFlash();
                var channel = Owner.GetComponent<SpiritBombChannel>();
                if (channel) channel.FollowOwner();
            }
            return accepted;
        }
        // Analytic ray/sphere targeting works even while the non-solid charge collider is disabled.
        public static SpiritBombProjectile OnRay(Vector2 origin, Vector2 direction, float range, out float distance)
        {
            distance = range + 1; SpiritBombProjectile best = null;
            direction.Normalize(); if (direction.sqrMagnitude < .5f) return null;
            for (int i = 0; i < live.Count; i++)
            {
                var bomb = live[i]; if (!bomb || !bomb.CanReceive) continue;
                Vector2 delta = (Vector2)bomb.transform.position - origin;
                float projection = Vector2.Dot(delta, direction), r = bomb.Radius;
                float side2 = delta.sqrMagnitude - projection * projection;
                if (side2 > r * r || projection + r < 0) continue;
                float entry = Mathf.Max(0, projection - Mathf.Sqrt(Mathf.Max(0, r * r - side2)));
                if (entry <= range && entry < distance) { best = bomb; distance = entry; }
            }
            return best;
        }
        // Hands or a clash have it: it stays where it is put and stops pulling.
        public void SetHeld(bool on) { held = on; if (on) { velocity = Vector2.zero; if (body) body.velocity = Vector2.zero; } }
        public void HoldAt(Vector2 centre)
        {
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            if (body) body.position = centre;
        }
        public void Redirect(Vector2 newVelocity, PersonBehaviour newOwner)
        {
            homingTarget = null; velocity = newVelocity; flightLimit = phaseAge + 8f;
            if (newOwner) Owner = newOwner;
        }
        public void Vanish()
        {
            Phase = SpiritBombPhase.Finished; live.Remove(this);
            if (trigger) trigger.enabled = false;
            Destroy(gameObject);
        }
        public void SetGatherAnchor(Vector2 point) { gatherAnchor = point; }
        public void SnapGatherAnchor() { transform.position = new Vector3(gatherAnchor.x, gatherAnchor.y, -.15f); }
        void LateUpdate()
        {
            if (Phase != SpiritBombPhase.Charging || (Global.main && Global.main.Paused)) return;
            Vector2 position = Vector2.SmoothDamp(transform.position, gatherAnchor, ref gatherVelocity, .18f, 80, Time.deltaTime);
            transform.position = new Vector3(position.x, position.y, -.15f);
        }
        public void Prepare()
        {
            if (!CanReceive || Energy <= 0) return;
            Phase = SpiritBombPhase.Ready; phaseAge = 0; gatherVelocity = Vector2.zero;
            body.position = transform.position; body.velocity = Vector2.zero;
            if (sound) { sound.volume = .12f; sound.pitch = .85f; }
            gameObject.name = "DBPU Spirit Bomb - ready, click an enemy";
        }
        public void LaunchAt(PersonBehaviour target)
        {
            if (!target || target == Owner || !target.IsAlive()) return;
            homingTarget = target;
            Launch(AimPoint(target) - (Vector2)transform.position);
            flightLimit = Mathf.Clamp(Vector2.Distance(BodyCentre(target), transform.position) / Mathf.Max(1, velocity.magnitude) + 5, 12, 40);
        }
        // With hands able to meet it, it comes along the ground at them instead of burying itself short of them.
        Vector2 AimPoint(PersonBehaviour target)
        {
            Vector2 aim = BodyCentre(target), ground, normal;
            if (Orb != null && BeamGround.Find(aim, 8f, target.transform.root, out ground, out normal)) aim.y = Mathf.Max(aim.y, ground.y + Radius * 1.04f);
            return aim;
        }
        public void Launch(Vector2 direction)
        {
            if ((Phase != SpiritBombPhase.Charging && Phase != SpiritBombPhase.Ready) || !Owner || Energy <= 0) return;
            var support = Owner.GetComponent<AllyKiReservoir>();
            var mult = Owner.GetComponent<MultipliersAndStuffs>();
            launchPower = KiCombatMath.NativeMultiplier(Owner) * (support ? support.PowerMultiplier : 1) * CombatPowerController.Mode(Owner);
            double health = KiCombatMath.Health(Owner) / (support ? Mathf.Max(1, support.HealthMultiplier) : 1) / CombatPowerController.HealthScale(Owner) / CombatPowerController.KaiokenHealthScale(Owner);
            double output = mult ? Mathf.Clamp(KiCombatMath.Finite(mult.KiOutput, 1), 0, 1000) : 1;
            Damage = KiCombatMath.Cap(health * launchPower * output * SpiritBombMath.DamageScale(Energy, ReferenceKi)
                * Mathf.Clamp(KiCombatMath.Finite(DBPU.DamageMultiplier, 1), 0, 1000), 1e35f);
            Phase = SpiritBombPhase.Flight; phaseAge = 0;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            velocity = (direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right) * SpiritBombMath.Speed(Radius)
                * Mathf.Clamp(KiCombatMath.Finite(DBPU.Beam_SpeedMultiplier, 1), .1f, 4);
            trigger.radius = Radius; trigger.enabled = true;
            gameObject.name = "DBPU Spirit Bomb - released";
            sound.Stop(); sound.pitch = 1; sound.clip = DBPU.Attack_Kamehameha_Loop; sound.volume = .3f; if (sound.clip) sound.Play();
        }
        void Update()
        {
            if (Global.main && Global.main.Paused) return;
            if ((Phase == SpiritBombPhase.Charging || Phase == SpiritBombPhase.Ready) && (!Owner || !Owner.IsAlive())) { Destroy(gameObject); return; }
            if (Phase != SpiritBombPhase.Charging && Phase != SpiritBombPhase.Ready) Advance(Time.deltaTime);
        }
        public void Advance(float dt)
        {
            dt = KiCombatMath.Step(dt); if (dt <= 0) return;
            age += dt; phaseAge += dt;
            if (Phase == SpiritBombPhase.Containment)
            {
                if (phaseAge >= nextScan) { ScanVictims(); nextScan = phaseAge + .1f; }
                ExposeContainedMatter(dt);
                while (PulsesApplied < SpiritBombMath.PulseCount && phaseAge + .00001f >= nextPulse)
                {
                    ApplyPulse(); nextPulse += SpiritBombMath.PulseInterval;
                }
                if (phaseAge >= SpiritBombMath.ContainmentDuration) Detonate();
            }
            else if (Phase == SpiritBombPhase.Flight && held) phaseAge -= dt;
            else if (Phase == SpiritBombPhase.Flight && phaseAge >= flightLimit) Detonate();
            else if (Phase == SpiritBombPhase.Detonating && phaseAge >= 1.8f)
            { Phase = SpiritBombPhase.Finished; Destroy(gameObject); }
        }
        void FixedUpdate()
        {
            if (!Global.main || Global.main.Paused || held) return;
            if(Phase==SpiritBombPhase.Flight || Phase==SpiritBombPhase.Containment) PullVictims(Time.fixedDeltaTime);
            if (Phase == SpiritBombPhase.Flight)
            {
                if (homingTarget && homingTarget.IsAlive())
                {
                    Vector2 aim = (AimPoint(homingTarget) - body.position).normalized;
                    Vector2 direction = Vector2.Lerp(velocity.normalized, aim, 1 - Mathf.Exp(-4 * Time.fixedDeltaTime)).normalized;
                    velocity = direction * velocity.magnitude;
                }
                Vector2 start = body.position; float travel = velocity.magnitude * Time.fixedDeltaTime;
                float allowed=travel;KiShield shell;
                if(KiShield.ClipAttack(Owner?Owner.gameObject:null,GetInstanceID(),start,velocity.normalized,ref allowed,Radius,(double)Damage*1.5,out shell))
                {body.position=start+velocity.normalized*allowed;return;}
                int n = Physics2D.CircleCastNonAlloc(start, Radius, velocity.normalized, casts, travel);
                float nearest = travel + 1; Collider2D contact = null;
                for (int i = 0; i < n; i++)
                {
                    var hit = casts[i]; if (!SolidTarget(hit.collider) || hit.distance >= nearest) continue;
                    nearest = hit.distance; contact = hit.collider;
                }
                Vector2 pos = start + velocity.normalized * (contact ? Mathf.Max(0, nearest) : travel);
                if (contact)
                {
                    // Complete the final sweep before stopping. Clearing velocity after
                    // MovePosition can cancel its pending movement in this physics step.
                    body.position = pos; BeginContainment();
                }
                else body.MovePosition(pos);
            }
        }
        bool SolidTarget(Collider2D col)
        {
            if (!col || col.isTrigger || col == trigger || col.GetComponentInParent<SpiritBombProjectile>()) return false;
            var limb = col.GetComponent<LimbBehaviour>();
            if (limb && limb.Person == Owner) return false;
            if (limb && Orb != null && (Orb.ImmuneTo.Contains(limb.Person) || SpherePhysics.ReactToContact(Orb, limb))) return false;
            return limb || col.gameObject.layer == 11 || col.GetComponent<PhysicalBehaviour>();
        }
        public bool BeginContainment()
        {
            if (Phase != SpiritBombPhase.Flight) return false;
            Phase = SpiritBombPhase.Containment; phaseAge = 0; nextScan = 0; nextPulse = .35f;
            homingTarget = null; velocity = Vector2.zero; body.velocity = Vector2.zero;
            ScanVictims(); visual.Pulse();
            gameObject.name = "DBPU Spirit Bomb - containment"; return true;
        }
        void ScanVictims()
        {
            // The renderer may still interpolate the previous physics position at impact.
            int n = Physics2D.OverlapCircleNonAlloc(body.position, Radius * 1.02f, overlaps);
            for (int i = 0; i < n && victims.Count < 24; i++)
            {
                var col = overlaps[i]; if (!col || col.isTrigger) continue;
                var limb = col.GetComponent<LimbBehaviour>();
                if (limb && limb.Person && limb.Person != Owner && !victims.Contains(limb.Person)) victims.Add(limb.Person);
            }
        }
        public void PullVictims(float dt)
        {
            if(Phase!=SpiritBombPhase.Flight && Phase!=SpiritBombPhase.Containment)return;
            dt=Mathf.Clamp(dt,0,.1f);if(dt<=0)return;
            Vector2 centre = body.position;float range=GravityRange;
            int count=Physics2D.OverlapCircleNonAlloc(centre,range,gravityColliders);pulled.Clear();
            for(int i=0;i<count;i++)
            {
                var col=gravityColliders[i];if(!col || col.isTrigger)continue;
                PullBody(col.attachedRigidbody,centre,range,dt);
            }
            if(count==gravityColliders.Length && Global.main)
                for(int i=0;i<Global.main.PhysicalObjectsInWorld.Count;i++)
                {var physical=Global.main.PhysicalObjectsInWorld[i];if(physical)PullBody(physical.rigidbody,centre,range,dt);}
        }
        void PullBody(Rigidbody2D rb,Vector2 centre,float range,float dt)
        {
                if(!rb || rb==body || !rb.simulated || rb.bodyType!=RigidbodyType2D.Dynamic || !pulled.Add(rb.GetInstanceID()))return;
                var limb=rb.GetComponent<LimbBehaviour>();
                if(limb && Orb!=null && Orb.ImmuneTo.Contains(limb.Person))return;
                var barrier=limb && limb.Person?limb.Person.GetComponent<KiShield>():null;
                if(barrier && barrier.Using && Vector2.Distance(centre,barrier.Centre)>barrier.Radius)return;
                // Shared torso target for ragdolls; props use their own centre of mass.
                Vector2 anchor=limb && limb.Person ? BodyCentre(limb.Person) : rb.worldCenterOfMass;
                Vector2 delta=centre-anchor;float distance=delta.magnitude;
                if(distance>range)return;
                float falloff=Mathf.Pow(Mathf.Clamp01(1-distance/range),.65f);
                Vector2 desired=Vector2.ClampMagnitude(delta*5.5f,52);
                float acceleration=(30+Mathf.Min(Radius,30)*6)*falloff;
                Vector2 correction=Vector2.ClampMagnitude((desired-rb.velocity)*4.5f,acceleration)*dt;
                rb.AddForce(correction*rb.mass,ForceMode2D.Impulse);
        }
        static Vector2 BodyCentre(PersonBehaviour person)
        {
            var chest = person.transform.Find("Body/UpperBody");
            return chest ? (Vector2)chest.position : (Vector2)person.transform.position;
        }
        // Contact exposure follows real containment time, independent of the coarse
        // health pulses. Limbs outside the sphere and protected by a shield are spared.
        void ExposeContainedMatter(float dt)
        {
            if (dt <= 0f || held || (Orb != null && (Orb.Held || Orb.Dueling))) return;
            Vector2 centre = body.position;
            double power = Orb != null ? Orb.Power : DbpuFx.PowerOf(Owner)
                * Mathf.Clamp(.7f + Mathf.Sqrt(Mathf.Max(0f, Energy / Mathf.Max(1f, ReferenceKi))) * .7f, .7f, 3f);
            foreach (var victim in victims)
            {
                if (!victim || victim == Owner || victim.Limbs == null || (Orb != null && Orb.ImmuneTo.Contains(victim))) continue;
                if (KiShield.Protects(victim, Owner ? Owner.gameObject : null, centre,
                    (double)Damage * 1.5d, GetInstanceID())) continue;
                foreach (var limb in victim.Limbs)
                {
                    if (!limb || !limb.gameObject.activeInHierarchy || !limb.PhysicalBehaviour || limb.PhysicalBehaviour.isDisintegrated) continue;
                    float distance = Vector2.Distance(limb.transform.position, centre);
                    if (distance > Radius * 1.02f) continue;
                    var collider = limb.GetComponent<Collider2D>();
                    EnergyAnnihilation.Expose(Owner, limb,
                        KiCombatMath.Cap(power * SpiritBombMath.Falloff(distance, Radius)),
                        collider ? collider.ClosestPoint(centre) : (Vector2)limb.transform.position,
                        dt, new Color(.45f, .82f, 1f));
                }
            }
        }

        void ApplyPulse()
        {
            PulsesApplied++;
            foreach (var victim in victims)
            {
                if (!victim || victim == Owner || victim.Limbs == null) continue;
                float distance = Vector2.Distance(BodyCentre(victim), transform.position);
                if (distance > Radius * 1.05f) continue;
                float dose = KiCombatMath.Cap((double)Damage * .28 * SpiritBombMath.Falloff(distance, Radius), 1e35f);
                foreach (var limb in victim.Limbs)
                    if (limb && Vector2.Distance(limb.transform.position, transform.position) <= Radius * 1.12f) Hurt(limb, dose);
            }
            if (visual) visual.Pulse();
        }
        void Hurt(LimbBehaviour limb, float dose)
        {
            if (!limb || limb.Person == Owner || dose <= 0 || (Orb != null && Orb.ImmuneTo.Contains(limb.Person))) return;
            if(KiShield.Protects(limb.Person,Owner?Owner.gameObject:null,body.position,dose/Mathf.Max(.001f,Time.deltaTime),GetInstanceID()))return;
            float fraction = dose / Mathf.Max(1, KiCombatMath.Finite(limb.InitialHealth, 1e32f));
            if (fraction < .0001f) return;
            // A lethal final dose commits the same progressive erasure as sustained
            // contact. The annihilation runtime owns Majin matter and the visible finish.
            if (Phase == SpiritBombPhase.Detonating && fraction >= 1f)
            {
                double power = Orb != null ? Orb.Power : DbpuFx.PowerOf(Owner)
                    * Mathf.Clamp(.7f + Mathf.Sqrt(Mathf.Max(0f, Energy / Mathf.Max(1f, ReferenceKi))) * .7f, .7f, 3f);
                float falloff = SpiritBombMath.Falloff(Vector2.Distance(body.position, limb.transform.position), Radius * 1.65f);
                EnergyAnnihilation.Detonate(Owner, limb, KiCombatMath.Cap(power * falloff),
                    limb.transform.position, new Color(.45f, .82f, 1f));
            }
            limb.Damage(dose);
            if (limb.PhysicalBehaviour && DBPU.Beam_ToggleHeat)
                limb.PhysicalBehaviour.BurnProgress = Mathf.Min(1, limb.PhysicalBehaviour.BurnProgress + Mathf.Clamp(fraction * .015f, 0, .06f));
        }
        public void Detonate()
        {
            if (Phase != SpiritBombPhase.Flight && Phase != SpiritBombPhase.Containment) return;
            Phase = SpiritBombPhase.Detonating; phaseAge = 0; FinalApplied = true;
            trigger.enabled = false; live.Remove(this);
            float reach = Radius * 1.65f;
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, reach, overlaps); damaged.Clear();
            for (int i = 0; i < n; i++)
            {
                var col = overlaps[i]; if (!col || col.isTrigger) continue;
                var limb = col.GetComponent<LimbBehaviour>();
                if (limb && limb.Person == Owner) continue;
                if (!damaged.Add(col.gameObject.GetInstanceID())) continue;
                float distance = Vector2.Distance(transform.position, col.transform.position);
                float falloff = SpiritBombMath.Falloff(distance, reach);
                if (limb) Hurt(limb, KiCombatMath.Cap((double)Damage * 1.8 * falloff, 1e35f));
            }
            victims.Clear();
            DetonationShockwave = SpiritNukeShockwave.Emit(body.position);
            if (visual) visual.Detonate();
            if (Orb != null)
            {
                OrbFx.Explosion(body.position, Radius * 1.25f + 2f, Orb.Look, Vector2.up);
                Orb.WentOff();
            }
            if (sound) { sound.Stop(); sound.loop = false; sound.volume = .6f; if (DBPU.Attack_Kamehameha_Fire) sound.PlayOneShot(DBPU.Attack_Kamehameha_Fire); }
        }
        void OnDisable() { live.Remove(this); victims.Clear(); if (sound) sound.Stop(); }
        void OnDestroy() { live.Remove(this); if (Orb != null) Orb.Dispose(); }
    }
}
