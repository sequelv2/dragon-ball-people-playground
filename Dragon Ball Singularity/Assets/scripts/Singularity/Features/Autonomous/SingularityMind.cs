using System;
using System.Collections.Generic;
using UnityEngine;
using float4 = Unity.Mathematics.float4;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // The brain of one AI fighter. Runs late in FixedUpdate (after the game's pose pass and
    // the flight brake, before the beam rig), in this order: body upkeep, senses, bookkeeping
    // and learning at 4 Hz, a tactic decision when one is due, the tactic's own step,
    // reflexes, the strike engine, movement, and finally the pose write.
    //
    // Three layers decide what happens:
    //   policy   the network plus priors plus the planner's look-ahead pick a tactic;
    //   director form changes follow pressure, as in the series, not a timer;
    //   governor reaction delay, stamina, combo length, breathers and accuracy bound what any
    //            tactic may do. Learning changes the policy only, never the governor.
    // ---------------------------------------------------------------------------------
    [DefaultExecutionOrder(30000)]
    public sealed class SingularityMind : MonoBehaviour, Messages.IOnBeforeSerialise
    {
        // ------------------------------------------------------------------------- registry
        public static readonly List<SingularityMind> All = new List<SingularityMind>();
        static readonly Dictionary<int, SingularityMind> byPerson = new Dictionary<int, SingularityMind>();
        static bool hooked; static float lastDemoAt;
        static readonly AiDecision demo = new AiDecision();
        static readonly float4[] demoH1 = new float4[AiPolicyNet.HiddenBlocks], demoH2 = new float4[AiPolicyNet.HiddenBlocks];
        static readonly float[] demoLogits = new float[AiTactic.Count], demoFeatures = new float[AiPolicyNet.In];

        public static SingularityMind First
        {
            get { for (int i = 0; i < All.Count; i++) if (All[i] && All[i].AiOn) return All[i]; return null; }
        }

        public static SingularityMind For(PersonBehaviour person, bool create)
        {
            if (!person) return null;
            var mind = person.GetComponent<SingularityMind>();
            if (!mind && create) mind = person.gameObject.AddComponent<SingularityMind>();
            return mind;
        }

        public static void DisableAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) { All[i].resumeOnRevive = false; All[i].SetAi(false); }
        }

        static SingularityMind Of(PersonBehaviour person)
        {
            if (!person) return null;
            SingularityMind mind;
            return byPerson.TryGetValue(person.GetInstanceID(), out mind) && mind && mind.AiOn ? mind : null;
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            PunchPower.MeleeResolved += OnMelee;
            Singularity.LimbImpact += OnImpact;
        }

        static void OnMelee(MeleeHitInfo info)
        {
            var attacker = info.Attacker ? info.Attacker.Person : null; var victim = info.Victim ? info.Victim.Person : null;
            var mine = Of(attacker);
            if (mine != null) mine.strikes.OnMelee(info);
            var theirs = Of(victim);
            if (theirs != null && attacker) theirs.Provoked(attacker, info.Outcome == MeleeOutcome.Damaged ? .6f : .25f);
            // A fighter the player is driving just landed a real hit: that is a demonstration.
            if (mine == null && attacker && victim && info.Outcome == MeleeOutcome.Damaged) Demonstrate(attacker, victim, AiTactic.MeleeCombo);
        }

        static void OnImpact(SingularityImpact impact)
        {
            if (impact == null || !impact.Person || !impact.OtherPerson || impact.Person == impact.OtherPerson) return;
            var a = Of(impact.Person); var b = Of(impact.OtherPerson);
            if (a != null) a.Touched(impact.Limb, impact.OtherPerson, impact);
            if (b != null) b.Touched(impact.OtherLimb, impact.Person, impact);
        }

        // Imitation learning from a fighter the player controls: the situation is rebuilt from that
        // fighter's side and the policy for its opponent's character is nudged towards what was done.
        static void Demonstrate(PersonBehaviour actor, PersonBehaviour target, int tactic)
        {
            if (!AutonomousConfig.LearnFromPlayer || All.Count == 0 || Time.time - lastDemoAt < .25f || !actor || !target) return;
            lastDemoAt = Time.time;
            var from = SingularityFighters.Torso(actor); var to = SingularityFighters.Torso(target);
            if (!from || !to || !from.PhysicalBehaviour || !to.PhysicalBehaviour) return;
            var a = from.PhysicalBehaviour.rigidbody; var b = to.PhysicalBehaviour.rigidbody;
            if (!a || !b) return;
            Array.Clear(demoFeatures, 0, demoFeatures.Length);
            float facing = actor.transform.localScale.x < 0f ? -1f : 1f;
            Vector2 r = b.worldCenterOfMass - a.worldCenterOfMass, w = b.velocity - a.velocity;
            float distance = Mathf.Max(.01f, r.magnitude);
            demoFeatures[0] = Mathf.Clamp(r.x * facing / 10f, -2f, 2f); demoFeatures[1] = Mathf.Clamp(r.y / 10f, -2f, 2f);
            demoFeatures[2] = Mathf.Clamp(distance / 15f, 0f, 2f);
            demoFeatures[3] = Mathf.Clamp(w.x * facing / 15f, -2f, 2f); demoFeatures[4] = Mathf.Clamp(w.y / 15f, -2f, 2f);
            demoFeatures[5] = Mathf.Clamp(-Vector2.Dot(w, r / distance) / 15f, -2f, 2f);
            demoFeatures[6] = SingularityFighters.HealthFraction(actor); demoFeatures[7] = 1f; demoFeatures[8] = Mathf.Clamp01(actor.Consciousness);
            demoFeatures[9] = Mathf.Clamp01(actor.ShockLevel); demoFeatures[10] = Mathf.Clamp01(actor.PainLevel);
            demoFeatures[11] = SingularityFighters.KiFraction(actor); demoFeatures[12] = 1f;
            demoFeatures[21] = Mathf.Clamp(Mathf.Log10(SingularityFighters.PowerRatio(actor, target)) / 3f, -1f, 1f);
            demoFeatures[26] = .5f; demoFeatures[29] = SingularityFighters.HealthFraction(target);
            demoFeatures[30] = target.Consciousness < .4f ? 1f : 0f; demoFeatures[31] = r.x * facing >= 0f ? 1f : -1f;
            Pack(demoFeatures, demo.X);
            var memory = AiMemoryStore.For(SingularityFighters.CharacterId(target));
            memory.Net.Forward(demo.X, demoH1, demoH2, demoLogits);
            Softmax(demoLogits, ~0, 1f, demo.Probability, false);
            memory.Imitate(demo, tactic, .03f);
        }

        static void Pack(float[] features, float4[] x)
        {
            for (int b = 0; b < x.Length; b++) x[b] = new float4(features[b * 4], features[b * 4 + 1], features[b * 4 + 2], features[b * 4 + 3]);
        }

        // Fills `probability` over the allowed tactics; returns a sample (or the best one when not sampling).
        static int Softmax(float[] score, int mask, float temperature, float[] probability, bool sample)
        {
            float top = float.MinValue; int best = 0;
            for (int i = 0; i < score.Length; i++)
                if ((mask & (1 << i)) != 0 && score[i] > top) { top = score[i]; best = i; }
            float sum = 0f, t = Mathf.Max(.2f, temperature);
            for (int i = 0; i < score.Length; i++)
            {
                probability[i] = (mask & (1 << i)) != 0 ? Mathf.Exp(Mathf.Clamp((score[i] - top) / t, -30f, 0f)) : 0f;
                sum += probability[i];
            }
            if (sum <= 0f) { probability[best] = 1f; return best; }
            for (int i = 0; i < score.Length; i++) probability[i] /= sum;
            if (!sample) return best;
            float roll = UnityEngine.Random.value, run = 0f;
            for (int i = 0; i < score.Length; i++) { run += probability[i]; if (roll <= run && probability[i] > 0f) return i; }
            return best;
        }

        // --------------------------------------------------------------------------- state
        PersonBehaviour person; AiRestoreLedger ledger; KinematicsRig rig; KineticStrikeEngine strikes; ThreatSensor sensor; AbilityBridge abilities;
        readonly AiFoe foe = new AiFoe(); AiBrainMemory memory; AiPlanSlot planSlot; ImpactFramesModule impactFrames;
        public bool AiOn { get; private set; }
        public AiDifficulty Difficulty { get; private set; }
        public AiStance Stance { get; private set; }
        public AiDifficultyProfile Profile { get; private set; }
        public KinematicsRig Rig { get { return rig; } }
        public KineticStrikeEngine Strikes { get { return strikes; } }
        public ThreatSensor Sensor { get { return sensor; } }
        public AbilityBridge Abilities { get { return abilities; } }
        public AiRestoreLedger Ledger { get { return ledger; } }
        public AiFoe Foe { get { return foe; } }
        public AiBrainMemory Memory { get { return memory; } }
        public PersonBehaviour Person { get { return person; } }
        public bool Flying { get { return abilities != null && abilities.Flying; } }
        public bool CanAct { get; private set; }
        public float Tier01 { get; private set; }
        public int Tactic { get; private set; }
        public int LastAction { get { return Tactic; } }
        public float Stamina { get { return stamina; } }
        public float Pressure { get { return pressure; } }
        public float StopRisk { get { return stopRisk; } }
        public float ReactionDelay { get { return Mathf.Lerp(.18f, .035f, Tier01) * Profile.ReactionScale; } }
        public bool HasTarget { get { return foe.Valid; } }
        // Counters read by the QA harness and the overlay.
        public int Evades, Blocks, Parries, ShieldsRaised, GuardsHeld, BlastsFired, BeamsFired, Transforms, KaiokenUses, Barrages, FistStrikes, Flanks, Decisions, StrikesStarted;
        public readonly int[] TacticCount = new int[AiTactic.Count];
        // Test hooks for the QA harness: limit the tactics on offer, keep the fighter on its feet.
        public int TacticFilter = ~0;
        public bool GroundOnly;

        bool resumeOnRevive; PersonBehaviour forcedTarget;
        // perception delay ring (filtered foe state, as it was)
        const int RingSize = 64;
        readonly float[] ringTime = new float[RingSize]; readonly Vector2[] ringPos = new Vector2[RingSize], ringVel = new Vector2[RingSize], ringAcc = new Vector2[RingSize];
        int ringAt; Vector2 seenPos, seenVel, seenAcc; float seenAge;
        // governor
        float stamina = 1f, breatherUntil, recoverUntil, lastLabelAt = -10f, lastHitstopAt = -10f, capableClock, bookClock, staleClock, thinkClock;
        int comboLeft, comboCount, lastSlot = -1; float nextStrikeAt;
        // director
        float pressure, taken8, dealt8, lastHealth = 1f, foeLastHealth = 1f, lastEscalationAt = -20f, foeFormPower = 1f, lastHitAt = -10f, lastHurtAt = -10f;
        int healthMarks, lastFormIndex = -1, transformSteps = 1; bool commandedForm; string lastForm = "";
        // reward window
        int windowEvades, windowHits, windowWhiffs, windowStopped; float damageRateTaken, damageRateDealt;
        // tactic
        int phase, queued = -1, shotsLeft, failures, aimArm = -1; float tacticClock, nextShotAt, phaseClock, fistCharge, leanBias;
        Vector2 aimError, holdPoint, moveGoal, moveFeed; float moveCap, moveAccel = 1f; bool moving, blocking, thinkDue = true;
        readonly bool[] armReserved = new bool[2];
        // reflex
        int answeredKey; bool answered; float strikeSeenAt = -1f, threatSeenAt = -1f; Rigidbody2D parryLimb;
        // geometry for this step
        Vector2 me, foePos, foeVel, toFoe; float dist = 99f, idealRange = 1f, armReach = .9f, front = 1f;
        // policy scratch
        readonly AiDecision[] decisions = new AiDecision[8]; int decisionAt;
        readonly float[] features = new float[AiPolicyNet.In], logits = new float[AiTactic.Count], prior = new float[AiTactic.Count], planBonus = new float[AiTactic.Count], score = new float[AiTactic.Count];
        readonly float[] reflexProbability = new float[AiTactic.Count];
        float powerRatio = 1f; bool canFight = true;
        readonly float4[] hidden1 = new float4[AiPolicyNet.HiddenBlocks], hidden2 = new float4[AiPolicyNet.HiddenBlocks];
        readonly byte[] bestPlan = new byte[AiPlanSlot.Horizon];
        float stopRisk, punchPercent, punchStop, kickPercent, kickStop; bool punchStopped, kickStopped;
        bool ringActive; Bounds ring; bool matchOpen, victoryShown; float foeDownSince = -1f, selfDownSince = -1f, idleSince;
        public byte[] BestPlan { get { return bestPlan; } }
        public float[] LastProbability { get { return decisions[decisionAt % decisions.Length].Probability; } }

        void Awake()
        {
            person = GetComponent<PersonBehaviour>();
            for (int i = 0; i < decisions.Length; i++) decisions[i] = new AiDecision();
            Difficulty = AutonomousConfig.Difficulty; Stance = AutonomousConfig.Stance; Profile = AiDifficultyProfile.For(Difficulty);
            // A cloned object can carry a copy of this component: one brain per fighter.
            var others = GetComponents<SingularityMind>();
            if (others.Length > 1 && others[0] != this) Destroy(this);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { resumeOnRevive = false; SetAi(false); All.Remove(this); }
        void OnDestroy() { resumeOnRevive = false; SetAi(false); All.Remove(this); }

        // A copy or save made while the AI holds the body must not carry the AI's pose index.
        public void OnBeforeSerialise() { if (AiOn && rig != null && rig.PoseHeld) person.OverridePoseIndex = -1; }

        // ----------------------------------------------------------------------- lifecycle
        public void SetAi(bool on)
        {
            if (on == AiOn) return;
            if (on)
            {
                if (!person) person = GetComponent<PersonBehaviour>();
                if (!person || person.Limbs == null || !person.IsAlive() || !AutonomousMindModule.On) return;
                if (ledger == null)
                {
                    ledger = new AiRestoreLedger(); rig = new KinematicsRig(person, ledger); strikes = new KineticStrikeEngine(rig, this);
                    sensor = new ThreatSensor(rig); abilities = new AbilityBridge(person, rig, ledger);
                }
                rig.Build();
                if (!rig.Built) { ModAPI.Notify("Autonomous AI: this body has no torso to drive."); return; }
                abilities.Refresh(); abilities.TakeControl();
                rig.TakePose();
                sensor.Reset();
                planSlot = AiPlannerService.Acquire();
                impactFrames = Singularity.Get<ImpactFramesModule>();
                Hook();
                byPerson[person.GetInstanceID()] = this;
                AiOn = true; resumeOnRevive = false;
                stamina = 1f; pressure = 0f; taken8 = dealt8 = 0f; healthMarks = 0; comboLeft = 0; queued = -1; thinkDue = true; answered = false;
                lastHealth = SingularityFighters.HealthFraction(person); lastFormIndex = abilities.FormIndex; lastForm = abilities.Form;
                foe.Bind(null); memory = null; matchOpen = false; victoryShown = false; holdPoint = rig.Com; Tactic = AiTactic.FlightIntercept;
                CanAct = true; Tier01 = abilities.Tier01;
                if (abilities.Skipped.Count > 0) Debug.Log("[Singularity] AI on " + SingularityFighters.DisplayName(person) + " leaves out: " + string.Join(", ", abilities.Skipped.ToArray()));
                Callout("SINGULARITY MIND", Difficulty.ToString().ToUpperInvariant() + "  ·  " + Stance.ToString().ToUpperInvariant(), 1.8f, true);
            }
            else
            {
                AiOn = false;
                if (strikes != null) strikes.AbortAll();
                if (abilities != null) abilities.LetGo();
                if (ledger != null) ledger.RestoreAll();
                if (planSlot != null) { AiPlannerService.Release(planSlot); planSlot = null; }
                if (person) byPerson.Remove(person.GetInstanceID());
                CloseMatch(false, false);
                AiMemoryStore.Save();
            }
        }

        public void CycleDifficulty()
        {
            Difficulty = (AiDifficulty)(((int)Difficulty + 1) % 4);
            Profile = AiDifficultyProfile.For(Difficulty);
            AutonomousConfig.Difficulty = Difficulty; AutonomousConfig.Save();
        }

        public void SetDifficulty(AiDifficulty difficulty) { Difficulty = difficulty; Profile = AiDifficultyProfile.For(difficulty); }

        public void CycleStance()
        {
            Stance = (AiStance)(((int)Stance + 1) % 3);
            AutonomousConfig.Stance = Stance; AutonomousConfig.Save();
        }

        public void SetStance(AiStance stance) { Stance = stance; }
        public void ForceTarget(PersonBehaviour target) { forcedTarget = target; bookClock = 0f; }

        // Direct coaching: the player rates what the fighter did in the last two seconds.
        public void Coach(float sign)
        {
            if (memory == null) { ModAPI.Notify("The AI has no opponent to learn about yet."); return; }
            int taught = 0; float now = Time.time;
            for (int i = 0; i < decisions.Length; i++)
            {
                var d = decisions[i];
                if (d.Action < 0 || now - d.At > 2f) continue;
                memory.Reinforce(d, 1.5f * Mathf.Sign(sign), .05f * Profile.LearnRate);
                taught++;
            }
            memory.Coachings += taught > 0 ? 1 : 0;
            ModAPI.Notify(taught > 0 ? (sign > 0f ? "AI coached: more of that." : "AI coached: less of that.") : "Nothing in the last two seconds to coach.");
        }

        public string DescribeMemory() { return AiMemoryStore.Describe(memory); }

        public void ResetMemory()
        {
            if (memory == null) { ModAPI.Notify("The AI has no opponent memory to reset."); return; }
            memory.Forget(); AiMemoryStore.Save();
            ModAPI.Notify("AI memory vs " + memory.OpponentId + " was reset.");
        }

        public bool ExportState(float[] state, float[] probability)
        {
            if (!AiOn) return false;
            Array.Copy(features, state, Mathf.Min(features.Length, state.Length));
            var last = LastProbability;
            Array.Copy(last, probability, Mathf.Min(last.Length, probability.Length));
            return true;
        }

        public void InjectWeights(float[] weights) { if (memory != null && weights != null && weights.Length == AiPolicyNet.WeightCount) memory.LoadWeights(weights); }

        void Callout(string title, string subtitle, float seconds, bool always = false)
        {
            if (!AutonomousConfig.Cinematics || !person) return;
            if (!always && Time.time - lastLabelAt < 2.5f) return;
            lastLabelAt = Time.time;
            DbpuWorldLabel.Show(person, title, subtitle, seconds);
        }

        void Update()
        {
            if (AiOn || !resumeOnRevive || !person) return;
            // Brought back (Senzu, a wish, regeneration): the AI picks the body up again.
            if (person.IsAlive() && AutonomousMindModule.On) SetAi(true);
        }

        // ---------------------------------------------------------------------------- step
        void FixedUpdate()
        {
            if (!AiOn) return;
            if (!person || !AutonomousMindModule.On) { SetAi(false); return; }
            if (DbpuFx.Paused) return;
            float dt = Time.fixedDeltaTime, now = Time.time;
            if (!person.IsAlive()) { resumeOnRevive = true; SetAi(false); return; }
            // Stopped time and the Reality Rift belong to someone else: no thought, no force.
            if (RealityRift.Active || (SingularityWorldFreeze.Active && !SingularityWorldFreeze.Current.IsExempt(transform))) { strikes.AbortAll(); return; }

            staleClock -= dt;
            string form = abilities.Form;
            if (staleClock <= 0f || !ReferenceEquals(form, lastForm))
            {
                staleClock = .5f;
                if (rig.Stale() || form != lastForm) Rebuild(form);
            }
            rig.TakePose();
            rig.BeginStep();
            if (!rig.Built) return;
            sensor.Step(dt);

            capableClock -= dt;
            if (capableClock <= 0f)
            {
                capableClock = .1f;
                CanAct = person.Consciousness >= .8f && !person.Braindead && person.ShockLevel < .9f && canFight;
            }
            Govern(dt);
            Perceive(dt, now);
            bookClock -= dt;
            if (bookClock <= 0f)
            {
                bookClock = .25f; Bookkeeping(.25f, now);
                // A target chosen just now has not been looked at yet.
                if (foe.Valid && ringTime[ringAt] <= 0f) Perceive(dt, now);
            }

            if (!CanAct)
            {
                // Knocked out or in shock: the body is nobody's for now.
                strikes.AbortAll(); EndTactic();
                if (selfDownSince < 0f) selfDownSince = now;
                rig.WritePose(1f);
                return;
            }
            selfDownSince = -1f;
            armReserved[0] = armReserved[1] = false; blocking = false; moving = false; leanBias = 0f;

            // A native guard, a running beam or a transformation owns the body while it lasts.
            bool guarded = abilities.Guarded, anchored = abilities.Anchored, changing = abilities.Transforming;
            if (guarded && Tactic != AiTactic.KiGuard) { strikes.AbortAll(); EndTactic(); Begin(AiTactic.KiGuard); GuardsHeld++; windowEvades++; }

            if (foe.Valid)
            {
                thinkClock -= dt;
                if (thinkDue || (thinkClock <= 0f && Interruptible())) Think(now);
                if (Execute(dt, now)) { EndTactic(); thinkDue = true; }
                if (!guarded && !anchored && !changing) Reflex(dt, now);
            }
            else Idle(dt, now);

            if (!guarded) Posture();
            if (guarded || changing) strikes.AbortAll(); else strikes.Step(dt);
            if (!guarded && !anchored && !changing) Locomotion(dt);
            rig.WritePose(strikes.Releasing ? 0f : 1f);
        }

        void Rebuild(string form)
        {
            // A new form resizes limbs and re-weighs them; a severed limb changes what can strike.
            strikes.AbortAll();
            rig.Build();
            abilities.Refresh(); abilities.TakeControl();
            int index = abilities.FormIndex;
            if (form != lastForm && !commandedForm && lastFormIndex >= 0 && index >= 0 && index < lastFormIndex) pressure += .6f;   // knocked back down: reason to climb again
            commandedForm = false; lastForm = form; lastFormIndex = index; Tier01 = abilities.Tier01;
            if (foe.Valid) foe.Bind(foe.Person);
        }

        // Stamina, timers, and the things that switch themselves off.
        void Govern(float dt)
        {
            float regen = Profile.StaminaRegen * (Tactic == AiTactic.DisengageCharge || !foe.Valid ? 2f : 1f) * Mathf.Lerp(.6f, 1f, lastHealth);
            stamina = Mathf.Clamp01(stamina + regen * dt - (abilities.ShieldUp ? .04f * dt : 0f));
            if (abilities.KaiokenOn && abilities.Strain > .8f) abilities.SetKaioken(false);
        }

        // The opponent as this fighter sees it: the filtered state from one reaction time ago.
        void Perceive(float dt, float now)
        {
            me = rig.Com;
            if (!foe.Valid) { dist = 99f; return; }
            var track = sensor.PersonTrack(foe.Person);
            Vector2 p, v, a;
            if (track != null && track.Filter.Started) { p = track.Filter.Position; v = track.Filter.Velocity; a = track.Filter.Acceleration; }
            else { p = foe.Torso.worldCenterOfMass; v = foe.Torso.velocity; a = Vector2.zero; }
            ringAt = (ringAt + 1) % RingSize;
            ringTime[ringAt] = now; ringPos[ringAt] = p; ringVel[ringAt] = v; ringAcc[ringAt] = a;
            float delay = ReactionDelay; int at = ringAt;
            for (int i = 0; i < RingSize; i++)
            {
                int k = (ringAt - i + RingSize) % RingSize;
                if (ringTime[k] <= 0f) break;
                at = k;
                if (now - ringTime[k] >= delay) break;
            }
            seenPos = ringPos[at]; seenVel = ringVel[at]; seenAcc = ringAcc[at]; seenAge = Mathf.Clamp(now - ringTime[at], 0f, .4f);
            foePos = PredictPoint(foe.Torso, 0f); foeVel = seenVel;
            toFoe = foePos - me; dist = Mathf.Max(.01f, toFoe.magnitude);
            front = rig.Facing * (toFoe.x >= 0f ? 1f : -1f);
            var arm = rig.ChainUsable(KinematicsRig.ArmFront) ? rig.Chains[KinematicsRig.ArmFront] : rig.Chains[KinematicsRig.ArmBack];
            armReach = Mathf.Max(.2f, arm.Reach);
            idealRange = .78f * armReach + foe.TorsoRadius + sensor.BodyRadius * .35f;
        }

        // Where a body of the opponent will be `seconds` from now, judged from the delayed, filtered state.
        public Vector2 PredictPoint(Rigidbody2D body, float seconds)
        {
            if (!body) return me;
            if (!foe.Valid || body.transform.root != foe.Person.transform.root) return body.worldCenterOfMass + body.velocity * seconds;
            float t = seenAge + seconds;
            Vector2 offset = body.worldCenterOfMass - foe.Torso.worldCenterOfMass;
            return seenPos + seenVel * t + seenAcc * (.5f * t * t * .6f) + offset;
        }

        // Where a hand rests between strikes.
        public Vector2 GuardPoint(int slot)
        {
            var chain = rig.Chains[slot];
            Vector2 root = rig.Anchor(rig.Parts[chain.Upper]);
            Vector2 forward = foe.Valid ? new Vector2(toFoe.x >= 0f ? 1f : -1f, 0f) : new Vector2(rig.Facing, 0f);
            float r = chain.Reach;
            if (chain.IsLeg) return root + Vector2.down * (r * (Flying ? .86f : .97f)) + forward * (r * (slot == KinematicsRig.LegFront ? .1f : -.08f) * (Flying ? 1.4f : 1f));
            return slot == KinematicsRig.ArmFront ? root + forward * (r * .46f) + Vector2.up * (r * .2f) : root + forward * (r * .3f) + Vector2.up * (r * .06f);
        }

        // ---------------------------------------------------------------------- bookkeeping
        void Bookkeeping(float dt, float now)
        {
            Tier01 = abilities.Tier01;
            ringActive = AutonomousMindModule.RingFor(me, out ring);
            canFight = EnergyAnatomy.CanFight(person);
            powerRatio = foe.Valid ? SingularityFighters.PowerRatio(person, foe.Person) : 1f;
            float health = SingularityFighters.HealthFraction(person);
            float taken = Mathf.Max(0f, lastHealth - health);
            if (taken > .0005f) lastHurtAt = now;
            lastHealth = health;
            float dealt = 0f;
            if (foe.Valid)
            {
                float foeHealth = SingularityFighters.HealthFraction(foe.Person);
                if (now - lastHitAt < 1.5f) dealt = Mathf.Max(0f, foeLastHealth - foeHealth);
                foeLastHealth = foeHealth;
            }
            float decay = Mathf.Exp(-dt / 8f);
            taken8 = taken8 * decay + taken; dealt8 = dealt8 * decay + dealt;
            damageRateTaken = Mathf.Lerp(damageRateTaken, taken / dt, .3f); damageRateDealt = Mathf.Lerp(damageRateDealt, dealt / dt, .3f);

            WatchTracks(now, taken);
            ChooseTarget(now);
            Direct(dt, now, health);
            Outlook();
            Learn(dealt, taken);
            Judge(now);
            PublishPlan();

            // Fly while there is someone to fight; a fighter with nothing to do comes back down.
            if (abilities.HasFlight && !GroundOnly)
            {
                if (foe.Valid && !abilities.Flying && CanAct) abilities.SetFlight(true);
                else if (!foe.Valid && now - idleSince > 3f && sensor.GroundDistance < 29f) ledger.Restore(AiRestoreLedger.Kind.FlightState, abilities.Flight);
            }
        }

        // Hostility from what the senses saw, and demonstrations by fighters the player drives.
        void WatchTracks(float now, float taken)
        {
            AiTrack nearest = null; float nearestDistance = 6f;
            for (int i = 0; i < sensor.Tracks.Length; i++)
            {
                var t = sensor.Tracks[i];
                if (t.Kind == AiTrackKind.None) continue;
                if (t.Kind == AiTrackKind.Person)
                {
                    float d = Vector2.Distance(t.Position, me);
                    if (d < nearestDistance) { nearestDistance = d; nearest = t; }
                    // A fast, close approach is a threat in itself.
                    if (d < 4f && Vector2.Dot(t.Velocity - rig.ComVelocity, (me - t.Position).normalized) > 9f) t.Hostility = Mathf.Min(1f, t.Hostility + .1f);
                    continue;
                }
                if (!t.Owner || t.Owner == person) continue;
                if (!t.Counted && t.Tti < 1.2f)
                {
                    t.Counted = true;
                    var owner = sensor.PersonTrack(t.Owner);
                    if (owner != null) owner.Hostility = Mathf.Min(1f, owner.Hostility + .35f);
                }
                if (now - t.FirstSeen <= .3f && Of(t.Owner) == null && (t.Kind == AiTrackKind.Blast || t.Kind == AiTrackKind.Beam))
                    Demonstrate(t.Owner, person, t.Kind == AiTrackKind.Beam ? AiTactic.ChargedBeam : AiTactic.KiBlastVolley);
            }
            // Hurt with no one to blame it on: suspect whoever is closest.
            if (taken > .004f && nearest != null && now - lastHurtAt < .3f) nearest.Hostility = Mathf.Min(1f, nearest.Hostility + .15f);
            // Being held is an attack.
            if (foe.Valid)
                for (int i = 0; i < foe.Grips.Length; i++)
                {
                    var grip = foe.Grips[i];
                    if (grip && grip.isHolding && grip.CurrentlyHolding && grip.CurrentlyHolding.transform.root == transform.root) Provoked(foe.Person, .3f);
                }
        }

        public void Provoked(PersonBehaviour by, float amount)
        {
            if (!by || by == person || sensor == null) return;
            var track = sensor.PersonTrack(by);
            if (track != null) track.Hostility = Mathf.Min(1f, track.Hostility + amount);
        }

        void Touched(LimbBehaviour mine, PersonBehaviour other, SingularityImpact impact)
        {
            strikes.OnImpact(mine, other);
            // A contact made by this fighter's own live strike is its attack, not the other's.
            if (!strikes.IsStriking(mine) && impact.Speed >= 5f) Provoked(other, Mathf.Clamp(impact.Energy / Mathf.Max(1f, rig.Mass * 12f), .05f, .5f));
        }

        void ChooseTarget(float now)
        {
            PersonBehaviour best = null; float bestScore = float.MinValue;
            if (forcedTarget) { if (forcedTarget.IsAlive()) best = forcedTarget; else forcedTarget = null; }
            if (!best)
                for (int i = 0; i < sensor.Tracks.Length; i++)
                {
                    var t = sensor.Tracks[i];
                    if (t.Kind != AiTrackKind.Person || !t.Person || !t.Person.IsAlive()) continue;
                    bool hostile = t.Hostility > .05f;
                    if (Stance == AiStance.Guardian && !hostile) continue;
                    if (Stance == AiStance.Duel && !hostile && !t.KiFighter) continue;
                    // In a running tournament only those on the ring count.
                    if (ringActive && (t.Position.x < ring.min.x - 2f || t.Position.x > ring.max.x + 2f || t.Position.y < ring.max.y - 2f)) continue;
                    float d = Vector2.Distance(t.Position, me);
                    float value = t.Hostility * 3f + 1f / (1f + d / 8f) + (t.Person == foe.Person ? .35f : 0f) - (t.Person.Consciousness < .4f ? 1.2f : 0f);
                    if (value > bestScore) { bestScore = value; best = t.Person; }
                }
            if (best == foe.Person) return;
            CloseMatch(false, false);
            foe.Bind(best);
            if (!foe.Valid) { memory = null; idleSince = now; EndTactic(); strikes.AbortAll(); return; }
            memory = AiMemoryStore.For(foe.CharacterId);
            foeLastHealth = SingularityFighters.HealthFraction(foe.Person);
            foeFormPower = foe.Multipliers ? foe.Multipliers.FormMultiplier : 1f;
            matchOpen = true; victoryShown = false; foeDownSince = -1f; thinkDue = true;
            for (int i = 0; i < RingSize; i++) ringTime[i] = 0f;
        }

        // Section 6: pressure builds the way it does in the series, and a form change answers it.
        void Direct(float dt, float now, float health)
        {
            if (!foe.Valid) { pressure = Mathf.Max(0f, pressure - dt * .1f); return; }
            float ratio = powerRatio;
            float rate = (taken8 - dealt8) * 2f + (ratio < 1f ? Mathf.Clamp(-Mathf.Log10(ratio), 0f, 3f) * .25f : 0f);
            var track = sensor.PersonTrack(foe.Person);
            if (track != null && track.PeakSpeed > 28f) rate += .05f;
            pressure = Mathf.Clamp(pressure + rate * dt, 0f, 4f);
            if (rate < 0f) pressure = Mathf.Max(0f, pressure - dt * .03f);
            // 65 / 45 / 25 percent: each line crossed is a jolt, once, until health climbs back above it.
            Mark(health, .65f, 1); Mark(health, .45f, 2); Mark(health, .25f, 4);
            float theirs = foe.Multipliers ? foe.Multipliers.FormMultiplier : 1f;
            if (theirs > foeFormPower * 1.1f) pressure += .6f;
            foeFormPower = theirs;
            transformSteps = pressure >= 2.6f ? 2 : 1;
        }

        void Mark(float health, float line, int bit)
        {
            if (health < line && (healthMarks & bit) == 0) { healthMarks |= bit; pressure += .5f; }
            else if (health > line + .1f) healthMarks &= ~bit;
        }

        bool EscalationReady { get { return pressure >= 1f && Time.time - lastEscalationAt > 6f && abilities.KiFraction > .2f && abilities.CanTransform(1); } }

        // Section 5.5: would a hit clear the power-ratio stop, at the speed this fighter can expect to keep?
        void Outlook()
        {
            stopRisk = 0f; punchStopped = kickStopped = false;
            if (!foe.Valid) return;
            float expected = Mathf.Clamp(Profile.MaxStrikeSpeed * strikes.GateRatio, 5f, 25f);
            int arm = rig.ChainUsable(KinematicsRig.ArmFront) ? KinematicsRig.ArmFront : KinematicsRig.ArmBack;
            int leg = rig.ChainUsable(KinematicsRig.LegFront) ? KinematicsRig.LegFront : KinematicsRig.LegBack;
            bool punch = abilities.MeleeOutlook(arm, foe, expected, out punchPercent, out punchStop);
            bool kick = abilities.MeleeOutlook(leg, foe, expected, out kickPercent, out kickStop);
            punchStopped = !punch || punchPercent <= punchStop; kickStopped = !kick || kickPercent <= kickStop;
            if (!DBPU.PhysAtkStopLimbs) { stopRisk = punchStopped && kickStopped ? .5f : 0f; return; }
            stopRisk = punchStopped && kickStopped ? 1f : punchStopped ? .45f : 0f;
            if (strikes.StoppedStreak >= 2) stopRisk = Mathf.Max(stopRisk, .8f);
        }

        // Section 9: a contextual-bandit policy gradient step every 250 ms.
        void Learn(float dealt, float taken)
        {
            var decision = decisions[decisionAt % decisions.Length];
            if (memory == null || decision.Action < 0 || !foe.Valid) { windowEvades = windowHits = windowWhiffs = windowStopped = 0; return; }
            float reward = 10f * dealt - 10f * taken + .3f * windowEvades + .4f * windowHits - .2f * windowWhiffs - .3f * windowStopped;
            windowEvades = windowHits = windowWhiffs = windowStopped = 0;
            memory.Baseline = Mathf.Lerp(memory.Baseline, reward, .02f);
            float advantage = reward - memory.Baseline;
            if (Mathf.Abs(advantage) < .01f) return;
            memory.Reinforce(decision, advantage, .02f * Profile.LearnRate);
        }

        void Judge(float now)
        {
            if (!foe.Valid || !matchOpen) return;
            if (foe.Down) { if (foeDownSince < 0f) foeDownSince = now; }
            else foeDownSince = -1f;
            if (foeDownSince >= 0f && now - foeDownSince > 1.2f) CloseMatch(true, true);
            else if (selfDownSince >= 0f && now - selfDownSince > 1.2f) CloseMatch(true, false);
        }

        void CloseMatch(bool decided, bool won)
        {
            if (!matchOpen) return;
            matchOpen = false;
            if (memory == null || !decided) return;
            memory.Matches++; if (won) memory.Wins++; else memory.Losses++;
            memory.Dirty = true;
            AiMemoryStore.Save();
            if (won && !victoryShown)
            {
                victoryShown = true;
                // Victory beat: power down, guard down, stay watchful.
                if (abilities.KaiokenOn) abilities.SetKaioken(false);
                abilities.LowerShield(); abilities.ChargeKi(false);
                Callout("VICTORY", SingularityFighters.DisplayName(person).ToUpperInvariant(), 2.2f, true);
            }
        }

        void PublishPlan()
        {
            if (planSlot == null || !foe.Valid) return;
            var s = new AiPlanState
            {
                Distance = dist, Closing = Vector2.Dot(rig.ComVelocity - foeVel, toFoe / dist), Health = lastHealth, FoeHealth = foeLastHealth,
                Ki = abilities.KiFraction, Stamina = stamina, StopRisk = stopRisk, Reach = idealRange, BarrageReach = abilities.BarrageReach,
                Speed = Mathf.Lerp(9f, 24f, Tier01), FoeSpeed = Mathf.Clamp(foeVel.magnitude, 2f, 30f), FoeRate = Mathf.Clamp(damageRateTaken + .01f, .01f, .6f),
                Threat = sensor.ThreatLevel, MeleeGain = Mathf.Clamp(Mathf.Max(punchPercent, kickPercent) / 100f, 0f, .5f) * strikes.HitRate * 1.5f,
                BlastGain = Mathf.Clamp(punchPercent / 100f * .6f, .002f, .2f), BeamGain = Mathf.Clamp(punchPercent / 100f * 6f, .02f, .6f),
                BarrageGain = Mathf.Clamp(punchPercent / 100f * 1.2f, .002f, .3f), FistGain = Mathf.Clamp(punchPercent / 100f * 2.2f, .005f, .5f),
                Aggression = Mathf.Clamp01(.3f + damageRateTaken * 6f), Mask = Mask(), NextFormGain = abilities.CanTransform(1) ? 1 : 0
            };
            planSlot.Publish(ref s);
        }

        // --------------------------------------------------------------------------- policy
        int Mask()
        {
            float ki = abilities.KiFraction, now = Time.time;
            bool breather = now < breatherUntil || now < recoverUntil, down = foe.Down;
            bool anyLimb = rig.ChainUsable(0) || rig.ChainUsable(1) || rig.ChainUsable(2) || rig.ChainUsable(3);
            bool threat = sensor.ThreatLevel > .15f || strikeSeenAt >= 0f;
            int mask = 1 << AiTactic.FlightIntercept;
            if (!down && !breather && anyLimb) mask |= 1 << AiTactic.MeleeCombo;
            if (!down && !breather && anyLimb && stamina >= .3f && dist < 9f) mask |= 1 << AiTactic.AfterimageFlank;
            if (threat && stamina >= .2f) mask |= 1 << AiTactic.EvadeDash;
            if (threat) mask |= (1 << AiTactic.EvadeWeave) | (1 << AiTactic.GuardBlock);
            if (threat && abilities.HasShield && ki > .15f) mask |= 1 << AiTactic.KiShield;
            if (!down && !breather && abilities.BlastAffordable && dist > 1.6f) mask |= 1 << AiTactic.KiBlastVolley;
            if (!down && !breather && abilities.BeamAffordable && !abilities.BeamBusy && dist > 5f) mask |= 1 << AiTactic.ChargedBeam;
            if (!down && !breather && abilities.HasBarrage && front > 0f && dist <= abilities.BarrageReach * 1.4f && stamina > .2f) mask |= 1 << AiTactic.Barrage;
            if (!down && !breather && abilities.HasKiFist && ki > .12f && anyLimb) mask |= 1 << AiTactic.KiFist;
            if (ki < .6f || stamina < .35f || down) mask |= 1 << AiTactic.DisengageCharge;
            if (!down && abilities.KaiokenAllowed && !abilities.KaiokenOn && abilities.Strain < .5f && pressure >= .5f && ki > .15f) mask |= 1 << AiTactic.Kaioken;
            if (!down && EscalationReady) mask |= 1 << AiTactic.Transform;
            return (mask & TacticFilter) | (1 << AiTactic.FlightIntercept);
        }

        void BuildFeatures()
        {
            float facing = rig.Facing; Vector2 w = foeVel - rig.ComVelocity;
            var threat = sensor.TopThreat;
            features[0] = Mathf.Clamp(toFoe.x * facing / 10f, -2f, 2f); features[1] = Mathf.Clamp(toFoe.y / 10f, -2f, 2f);
            features[2] = Mathf.Clamp(dist / 15f, 0f, 2f);
            features[3] = Mathf.Clamp(w.x * facing / 15f, -2f, 2f); features[4] = Mathf.Clamp(w.y / 15f, -2f, 2f);
            features[5] = Mathf.Clamp(-Vector2.Dot(w, toFoe / dist) / 15f, -2f, 2f);
            features[6] = lastHealth;
            var chest = rig.Parts[AiLimb.UpperBody].Limb;
            features[7] = chest && chest.CirculationBehaviour ? Mathf.Clamp01(chest.CirculationBehaviour.GetAmountOfBlood()) : 1f;
            features[8] = Mathf.Clamp01(person.Consciousness); features[9] = Mathf.Clamp01(person.ShockLevel); features[10] = Mathf.Clamp01(person.PainLevel);
            features[11] = abilities.KiFraction; features[12] = stamina; features[13] = sensor.ThreatLevel;
            features[14] = threat != null ? 1f / (1f + threat.Tti * 4f) : 0f;
            Vector2 from = threat != null ? (threat.Position - me).normalized : Vector2.zero;
            features[15] = from.x * facing; features[16] = from.y;
            features[17] = Mathf.Clamp01(damageRateTaken * 5f); features[18] = Mathf.Clamp01(damageRateDealt * 5f);
            features[19] = Tier01; features[20] = Mathf.Clamp(Mathf.Log10(Mathf.Max(1f, foeFormPower)) / 8f, 0f, 1.5f);
            features[21] = Mathf.Clamp(Mathf.Log10(Mathf.Max(1e-4f, powerRatio)) / 3f, -1f, 1f);
            features[22] = stopRisk; features[23] = Mathf.Clamp01(sensor.GroundDistance / 10f);
            features[24] = Mathf.Clamp01(Mathf.Min(sensor.WallLeft, sensor.WallRight) / 10f); features[25] = Flying ? 1f : 0f;
            features[26] = strikes.HitRate; features[27] = strikes.StopRate; features[28] = strikes.WhiffRate;
            features[29] = foeLastHealth; features[30] = foe.Down ? 1f : 0f; features[31] = front;
        }

        // Hand-written starting point. The network learns the correction to it.
        void Priors()
        {
            float ki = abilities.KiFraction, threat = sensor.ThreatLevel;
            bool near = dist <= idealRange * 1.6f, far = dist > 6f, mid = !near && !far;
            bool close = strikeSeenAt >= 0f;
            float mercy = foeLastHealth < .25f && lastHealth > .6f ? Profile.Mercy : 1f;
            float push = (mercy - 1f) * 2f;      // Easy and Normal ease off a badly hurt opponent
            prior[AiTactic.FlightIntercept] = far ? 1.4f : mid ? .9f : -1.5f;
            prior[AiTactic.MeleeCombo] = (near ? 1.8f : mid ? .6f : -.6f) - stopRisk * 2.6f - (stamina < .2f ? 1f : 0f) + push;
            prior[AiTactic.AfterimageFlank] = (mid ? .5f : near ? .1f : -.4f) + (strikes.WhiffRate > .4f ? .5f : 0f) + (front < 0f ? .6f : 0f) - stopRisk * 1.5f + push;
            prior[AiTactic.EvadeDash] = threat * 1.6f - 1.2f;
            prior[AiTactic.EvadeWeave] = (close ? 1.4f : 0f) - 1.2f;
            prior[AiTactic.GuardBlock] = (close ? 1.2f : threat * .8f) - 1.1f + (lastHealth < .4f ? .3f : 0f);
            prior[AiTactic.Parry] = (close ? 1f : 0f) - 1.3f + Tier01 * .5f;
            prior[AiTactic.KiShield] = threat * 1.2f - 1.4f;
            prior[AiTactic.KiGuard] = -3f;
            prior[AiTactic.KiBlastVolley] = (far ? 1.1f : mid ? .7f : -.8f) + stopRisk - (ki < .15f ? 2f : 0f) + push * .5f;
            prior[AiTactic.ChargedBeam] = (far ? .6f : -1.2f) + stopRisk * .8f - (ki < .3f ? 2f : 0f) + push * .5f;
            prior[AiTactic.Barrage] = (near ? .5f : -1.5f) - stopRisk;
            prior[AiTactic.KiFist] = (far ? -.6f : .4f) + stopRisk * .9f - (ki < .2f ? 1.5f : 0f) + push * .5f;
            prior[AiTactic.DisengageCharge] = (foe.Down ? 1.5f : ki < .25f ? 1.6f : ki < .5f ? .2f : -1.5f) + (stamina < .2f ? .8f : 0f);
            prior[AiTactic.Kaioken] = pressure * 1.2f - 1f + stopRisk * .8f;
            prior[AiTactic.Transform] = pressure >= 1.6f ? 6f : 2.5f;
        }

        void Think(float now)
        {
            thinkDue = false; thinkClock = Profile.ThinkInterval;
            if (memory == null) return;
            int mask = Mask();
            int action;
            if (queued >= 0 && (mask & (1 << queued)) != 0) { action = queued; queued = -1; }
            else
            {
                queued = -1;
                BuildFeatures();
                decisionAt++;
                var decision = decisions[decisionAt % decisions.Length];
                Pack(features, decision.X);
                memory.Net.Forward(decision.X, hidden1, hidden2, logits);
                Priors();
                if (planSlot != null) planSlot.Read(planBonus, bestPlan);
                for (int i = 0; i < score.Length; i++) score[i] = logits[i] + prior[i] + planBonus[i] + AiIpc.Priors[i];
                decision.Temperature = Profile.Temperature;
                action = Softmax(score, mask, decision.Temperature, decision.Probability, true);
                decision.Action = action; decision.At = now;
                memory.Uses[action]++; memory.Dirty = true;
                Decisions++;
            }
            if (action != Tactic || Finished()) { EndTactic(); Begin(action); }
        }

        bool Interruptible()
        {
            return Tactic == AiTactic.FlightIntercept || Tactic == AiTactic.DisengageCharge;
        }

        bool Finished() { return tacticClock <= 0f; }

        // --------------------------------------------------------------------------- tactics
        void Begin(int tactic)
        {
            Tactic = tactic; tacticClock = 0f; phase = 0; phaseClock = 0f; failures = 0;
            TacticCount[tactic]++;
            float now = Time.time;
            switch (tactic)
            {
                case AiTactic.MeleeCombo:
                    comboLeft = Profile.ComboHits; nextStrikeAt = now;
                    break;
                case AiTactic.AfterimageFlank:
                    stamina -= .22f; Flanks++;
                    if (AutonomousConfig.Cinematics) SingularityFx.Afterimage(person, new Color(.8f, .9f, 1f, .5f), .35f);
                    Callout("AFTERIMAGE", "", 1f);
                    break;
                case AiTactic.EvadeDash: Dash(); break;
                case AiTactic.EvadeWeave: Evades++; windowEvades++; break;
                case AiTactic.GuardBlock: Blocks++; break;
                case AiTactic.Parry:
                    if (parryLimb && foe.Valid)
                    {
                        int slot = Vector2.Distance(rig.Tip(KinematicsRig.ArmFront), parryLimb.worldCenterOfMass) <= Vector2.Distance(rig.Tip(KinematicsRig.ArmBack), parryLimb.worldCenterOfMass) ? KinematicsRig.ArmFront : KinematicsRig.ArmBack;
                        if (!rig.ChainUsable(slot) || strikes.Busy(slot)) slot = slot == KinematicsRig.ArmFront ? KinematicsRig.ArmBack : KinematicsRig.ArmFront;
                        if (rig.ChainUsable(slot) && strikes.Begin(slot, foe.Person, parryLimb, .08f, Mathf.Min(16f, Profile.MaxStrikeSpeed), .03f, true)) { Parries++; stamina -= .05f; }
                    }
                    break;
                case AiTactic.KiShield:
                    if (abilities.RaiseShield()) { ShieldsRaised++; stamina -= .08f; }
                    break;
                case AiTactic.KiBlastVolley:
                    shotsLeft = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 5f, Tier01) * (Difficulty == AiDifficulty.Easy ? .6f : 1f)), 1, 6);
                    nextShotAt = now; aimArm = abilities.UsableArm; RollAim();
                    break;
                case AiTactic.ChargedBeam: aimArm = abilities.UsableArm; RollAim(); break;
                case AiTactic.KiFist: aimArm = abilities.UsableArm; fistCharge = UnityEngine.Random.Range(.5f, 1.2f); break;
                case AiTactic.Kaioken:
                    if (abilities.SetKaioken(true)) { KaiokenUses++; Callout("KAIOKEN!", "", 1.4f); }
                    break;
            }
        }

        // Leaves nothing half done: an aimed arm is let go, a hold is released.
        void EndTactic()
        {
            switch (Tactic)
            {
                case AiTactic.KiBlastVolley: abilities.ReleaseAim(aimArm); break;
                case AiTactic.ChargedBeam:
                    if (abilities.BeamCharging) abilities.StopBeam();
                    abilities.ReleaseAim(aimArm);
                    break;
                case AiTactic.Barrage: abilities.StopBarrage(); break;
                case AiTactic.KiFist: if (phase < 2) abilities.DropFist(aimArm); break;
                case AiTactic.DisengageCharge: abilities.ChargeKi(false); break;
                case AiTactic.KiShield: abilities.LowerShield(); break;
            }
            tacticClock = 0f; phase = 0;
        }

        void RollAim()
        {
            float degrees = (UnityEngine.Random.value - .5f) * 2f * Profile.BlastNoise;
            aimError = new Vector2(0f, Mathf.Tan(degrees * Mathf.Deg2Rad));
        }

        // True when the tactic is over.
        bool Execute(float dt, float now)
        {
            tacticClock += dt; phaseClock += dt;
            switch (Tactic)
            {
                case AiTactic.FlightIntercept:
                    Approach(foe.Down ? 3f : 1f);
                    return tacticClock > Profile.ThinkInterval * 3f || (tacticClock > .15f && !foe.Down && dist <= idealRange * 1.3f);
                case AiTactic.MeleeCombo: return Combo(now);
                case AiTactic.AfterimageFlank: return Flank();
                case AiTactic.EvadeDash: return tacticClock > .3f;
                case AiTactic.EvadeWeave:
                    leanBias = (toFoe.x >= 0f ? 1f : -1f) * 22f;
                    if (!Flying) rig.Aim(AiLimb.UpperBody, 90f + leanBias, 1f);
                    return tacticClock > .35f;
                case AiTactic.GuardBlock:
                    blocking = true;
                    return tacticClock > 1f || (tacticClock > .4f && sensor.ThreatLevel < .1f && strikeSeenAt < 0f);
                case AiTactic.Parry: return tacticClock > .12f && !strikes.AnyBusy;
                case AiTactic.KiShield:
                    blocking = true;
                    return !abilities.ShieldUp || tacticClock > 2.5f || (tacticClock > .45f && sensor.ThreatLevel < .1f && strikeSeenAt < 0f);
                case AiTactic.KiGuard: return !abilities.Guarded && tacticClock > .3f;
                case AiTactic.KiBlastVolley: return Volley(now);
                case AiTactic.ChargedBeam: return BeamTactic(dt, now);
                case AiTactic.Barrage: return BarrageTactic();
                case AiTactic.KiFist: return FistTactic(now);
                case AiTactic.DisengageCharge: return Disengage();
                case AiTactic.Kaioken: blocking = true; return tacticClock > .5f;
                case AiTactic.Transform: return TransformBeat(now);
            }
            return true;
        }

        float CruiseSpeed { get { return Mathf.Lerp(9f, 24f, Tier01) * (stamina < .2f ? .7f : 1f); } }

        void MoveTo(Vector2 goal, Vector2 feed, float cap, float accel) { moving = true; moveGoal = goal; moveFeed = feed; moveCap = cap; moveAccel = accel; }

        // Stand at striking distance on this fighter's own side of the opponent, shoulders level with their chest.
        void Approach(float rangeScale)
        {
            Vector2 away = dist > .05f ? -toFoe / dist : new Vector2(-rig.Facing, 0f);
            away.y *= .35f;
            if (away.sqrMagnitude < 1e-4f) away = new Vector2(-rig.Facing, 0f);
            away.Normalize();
            Vector2 goal = foePos + away * (idealRange * rangeScale);
            var shoulder = rig.Parts[rig.ChainUsable(KinematicsRig.ArmFront) ? AiLimb.UpperArmFront : AiLimb.UpperArm];
            if (shoulder.Usable) goal.y = foePos.y - (rig.Anchor(shoulder).y - me.y) * .6f;
            MoveTo(goal, foeVel, CruiseSpeed, 1f);
            holdPoint = goal;
        }

        void KeepRange(float min, float max)
        {
            Vector2 away = dist > .05f ? -toFoe / dist : new Vector2(-rig.Facing, 0f);
            float wanted = Mathf.Clamp(dist, min, max);
            MoveTo(foePos + away * wanted + Vector2.up * (Flying ? .4f : 0f), foeVel * .5f, CruiseSpeed * .8f, 1f);
        }

        int BusyStrikes()
        {
            int n = 0;
            for (int i = 0; i < KinematicsRig.ChainCount; i++) if (strikes.Busy(i)) n++;
            return n;
        }

        int PickSlot()
        {
            bool mayKick = Flying || Difficulty >= AiDifficulty.Hard;
            bool wantKick = mayKick && (comboCount % 3 == 2 || (punchStopped && !kickStopped));
            if (punchStopped && !kickStopped) wantKick = true;
            if (wantKick)
            {
                if (rig.ChainUsable(KinematicsRig.LegFront) && !strikes.Busy(KinematicsRig.LegFront)) return KinematicsRig.LegFront;
                if (rig.ChainUsable(KinematicsRig.LegBack) && !strikes.Busy(KinematicsRig.LegBack)) return KinematicsRig.LegBack;
            }
            int first = lastSlot == KinematicsRig.ArmFront ? KinematicsRig.ArmBack : KinematicsRig.ArmFront;
            int second = first == KinematicsRig.ArmFront ? KinematicsRig.ArmBack : KinematicsRig.ArmFront;
            if (rig.ChainUsable(first) && !strikes.Busy(first)) return first;
            if (rig.ChainUsable(second) && !strikes.Busy(second)) return second;
            // No arm left: the legs carry on.
            if (rig.ChainUsable(KinematicsRig.LegFront) && !strikes.Busy(KinematicsRig.LegFront)) return KinematicsRig.LegFront;
            if (rig.ChainUsable(KinematicsRig.LegBack) && !strikes.Busy(KinematicsRig.LegBack)) return KinematicsRig.LegBack;
            return -1;
        }

        // The limb speed to ask for: tier and stamina set the base, the stop threshold may ask for more.
        float StrikeSpeed(int slot)
        {
            float speed = Mathf.Lerp(13f, 26f, Tier01);
            float percent = slot < 2 ? punchPercent : kickPercent, stop = slot < 2 ? punchStop : kickStop;
            if (percent > 0f && stop > 0f)
            {
                // percent was worked out at the expected gate speed; scale to the speed that clears the stop with a margin.
                float expected = Mathf.Clamp(Profile.MaxStrikeSpeed * strikes.GateRatio, 5f, 25f);
                float needGate = expected * stop / percent * 1.1f;
                if (needGate <= 25f) speed = Mathf.Max(speed, needGate / Mathf.Max(.3f, strikes.GateRatio));
            }
            if (stamina < .2f) speed *= .7f;
            return Mathf.Clamp(speed, KineticStrikeEngine.MinSpeed, Profile.MaxStrikeSpeed);
        }

        float ChamberTime { get { return Mathf.Lerp(.15f, .06f, Tier01) * Profile.TelegraphScale; } }

        bool StartStrike(int slot, float now)
        {
            if (slot < 0) return false;
            float speed = StrikeSpeed(slot);
            bool head = foe.Head && UnityEngine.Random.value < Mathf.Lerp(.15f, .4f, Tier01);
            if (!strikes.Begin(slot, foe.Person, head ? foe.Head : foe.Torso, head ? foe.HeadRadius : foe.TorsoRadius, speed, ChamberTime, false)) return false;
            StrikesStarted++; comboCount++; lastSlot = slot;
            stamina -= .05f + .05f * (speed / 25f) * (speed / 25f);
            return true;
        }

        bool Combo(float now)
        {
            Approach(1f);
            if (foe.Down || tacticClock > 5f) return !strikes.AnyBusy;
            if (comboLeft <= 0)
            {
                if (strikes.AnyBusy) return false;
                breatherUntil = now + Profile.Breather;      // the opening every combo leaves
                return true;
            }
            if (now < nextStrikeAt || strikes.Releasing || BusyStrikes() >= 2) return false;
            if (dist > idealRange * 2.2f) return tacticClock > 1.4f && !strikes.AnyBusy;
            if (StartStrike(PickSlot(), now)) { comboLeft--; nextStrikeAt = now + Mathf.Lerp(.34f, .16f, Tier01) + ChamberTime; }
            return false;
        }

        // A fast arc over the opponent to its far side, then straight into a combo. No teleport: thrust only.
        bool Flank()
        {
            float side = toFoe.x >= 0f ? 1f : -1f;
            if (phase == 0)
            {
                Vector2 over = foePos + Vector2.up * (foe.Height * .6f + .5f) - new Vector2(side * .3f, 0f);
                if (!Flying) { phase = 1; phaseClock = 0f; return false; }
                MoveTo(over, foeVel, CruiseSpeed * 1.6f, 1.8f);
                if (Vector2.Distance(me, over) < .7f || phaseClock > .5f) { phase = 1; phaseClock = 0f; if (AutonomousConfig.Cinematics) SingularityFx.Afterimage(person, new Color(.8f, .9f, 1f, .5f), .3f); }
                return false;
            }
            Vector2 behind = foePos + new Vector2(side * idealRange, 0f);
            if (phase == 1) { holdPoint = behind; phase = 2; }
            MoveTo(holdPoint, foeVel, CruiseSpeed * 1.6f, 1.8f);
            if (Vector2.Distance(me, holdPoint) < .5f || phaseClock > .7f) { queued = AiTactic.MeleeCombo; return true; }
            return false;
        }

        // A burst sideways out of the line of whatever is coming.
        void Dash()
        {
            var threat = sensor.TopThreat;
            Vector2 line = threat != null && threat.Velocity.sqrMagnitude > 1f ? threat.Velocity.normalized : (toFoe.sqrMagnitude > 1e-4f ? toFoe.normalized : Vector2.right);
            Vector2 side = new Vector2(-line.y, line.x);
            Vector2 source = threat != null ? threat.Position : foePos;
            if (Vector2.Dot(side, me - source) < 0f) side = -side;
            if (!Flying) { side = new Vector2(side.x >= 0f ? 1f : -1f, 0f); if (!person.IsTouchingFloor) return; }
            else side = (side + Vector2.up * .25f).normalized;
            if (!sensor.PathClear(side, 1.4f)) side = -side;
            if (!sensor.PathClear(side, 1.4f)) side = Flying ? Vector2.up : side;
            rig.Kick(side * Mathf.Lerp(7f, 11f, Tier01));
            stamina -= .18f; Evades++; windowEvades++;
            if (AutonomousConfig.Cinematics && Tier01 > .5f) SingularityFx.Afterimage(person, new Color(.8f, .9f, 1f, .45f), .25f);
        }

        bool Volley(float now)
        {
            if (aimArm < 0 || foe.Down || !abilities.BlastAffordable || failures > 3 || tacticClock > 4f) return true;
            armReserved[aimArm] = true;
            KeepRange(5.5f, 12f);
            float lead = dist / Mathf.Max(20f, abilities.Blast.Speed);
            Vector2 target = PredictPoint(foe.Torso, lead);
            // The aim error is an angle off the line of fire.
            Vector2 line = (target - me).normalized;
            target += new Vector2(-line.y, line.x) * (aimError.y * dist);
            bool steady = abilities.AimArm(aimArm, target, 4f + Profile.BlastNoise * .5f);
            if (!steady || now < nextShotAt || strikes.Busy(aimArm)) return false;
            if (abilities.FireBlast(aimArm))
            {
                shotsLeft--; BlastsFired++; stamina -= .03f; lastHitAt = now;
                nextShotAt = now + Mathf.Lerp(.45f, .2f, Tier01) * Mathf.Sqrt(Profile.TelegraphScale);
                RollAim();
            }
            else { failures++; nextShotAt = now + .15f; }
            if (shotsLeft > 0) return false;
            recoverUntil = now + .25f;
            return true;
        }

        // The three-press beam: charge, release, stop. A long charge is a real opening for the opponent.
        bool BeamTactic(float dt, float now)
        {
            if (aimArm < 0) return true;
            Vector2 target = PredictPoint(foe.Torso, Mathf.Clamp(dist / 50f, 0f, .4f));
            Vector2 line = (target - me).normalized;
            target += new Vector2(-line.y, line.x) * (aimError.y * dist * .5f);
            if (phase == 0)
            {
                if (!abilities.StartBeam(aimArm)) return true;
                phase = 1; phaseClock = 0f; stamina -= .2f;
                return false;
            }
            if (phase == 1)
            {
                armReserved[aimArm] = true;
                KeepRange(7f, 16f);
                bool steady = abilities.AimArm(aimArm, target, 5f);
                if (abilities.BeamFiring) { phase = 2; phaseClock = 0f; BeamsFired++; lastHitAt = now; return false; }
                // Rushed while charging, or it never finished: give it up and get out of the way.
                if (!abilities.BeamCharging || phaseClock > 10f || (dist < idealRange * 2f && strikeSeenAt >= 0f)) { abilities.StopBeam(); queued = AiTactic.EvadeDash; return true; }
                if (abilities.BeamCharged && steady && abilities.ReleaseBeam()) { phase = 2; phaseClock = 0f; BeamsFired++; lastHitAt = now; Callout("HAAAA!", "", 1.2f); }
                return false;
            }
            if (!abilities.BeamBusy) { recoverUntil = now + .7f; return true; }
            lastHitAt = now;
            var arm = rig.Parts[rig.Chains[aimArm].Lower];
            bool on = arm.Tf && Vector2.Angle(-(Vector2)arm.Tf.up, target - (Vector2)arm.Tf.position) < 14f;
            abilities.TrackBeamTarget(on, dt);
            float balance, gap;
            if (abilities.Duel(out balance, out gap))
            {
                // A struggle is decided by power and ki behind the beam: add what there is, or get out before it arrives.
                if (balance < -.1f && gap < 5f && abilities.KaiokenAllowed && !abilities.KaiokenOn) { if (abilities.SetKaioken(true)) KaiokenUses++; }
                if (balance < -.15f && gap < 2.4f) { abilities.StopBeam(); queued = AiTactic.EvadeDash; recoverUntil = now + .4f; return true; }
                return false;
            }
            if (abilities.BeamHeld > Mathf.Lerp(1.6f, 3.2f, Tier01) || abilities.BeamOffTarget > .7f || abilities.KiFraction < .08f) abilities.StopBeam();
            return false;
        }

        bool BarrageTactic()
        {
            if (phase == 0)
            {
                Approach(1f);
                if (tacticClock > 1.2f) return true;
                if (dist > abilities.BarrageReach * .9f || front <= 0f) return false;
                if (!abilities.StartBarrage()) return true;
                phase = 1; phaseClock = 0f; stamina -= .12f; Barrages++; lastHitAt = Time.time;
                return false;
            }
            Approach(1f); lastHitAt = Time.time;
            if (phaseClock < Mathf.Lerp(.6f, 1.2f, Tier01) && dist <= abilities.BarrageReach * 1.2f && front > 0f && abilities.BarrageFiring) return false;
            abilities.StopBarrage();
            breatherUntil = Time.time + Profile.Breather * .6f;
            return true;
        }

        // Charge ki into a fist while closing, then put it through the strike engine.
        bool FistTactic(float now)
        {
            if (aimArm < 0 || tacticClock > 5f) return true;
            int slot = aimArm == 0 ? KinematicsRig.ArmFront : KinematicsRig.ArmBack;
            var fist = abilities.Fist(aimArm);
            if (phase == 0)
            {
                Approach(1.5f);
                if (!abilities.BeginFist(aimArm)) return ++failures > 20;
                fist = abilities.Fist(aimArm);
                if (fist && (fist.IsCharged || fist.Charge01 * 3f >= fistCharge)) { abilities.ArmFist(aimArm); phase = 1; phaseClock = 0f; }
                return false;
            }
            if (phase == 1)
            {
                Approach(1f);
                if (!fist || !fist.IsCharged) return phaseClock > .3f;
                if (dist > idealRange * 2f || strikes.Busy(slot)) return phaseClock > 2.5f;
                if (StartStrike(slot, now)) { phase = 2; phaseClock = 0f; FistStrikes++; }
                return false;
            }
            Approach(1f);
            return !strikes.Busy(slot);
        }

        bool Disengage()
        {
            Vector2 away = dist > .05f ? -toFoe / dist : new Vector2(-rig.Facing, 0f);
            if (foe.Down)
            {
                // Never press a fighter who is out: stand off and wait.
                MoveTo(foePos + away * Mathf.Max(2.6f, idealRange * 2.5f), Vector2.zero, CruiseSpeed * .6f, 1f);
                return tacticClock > .6f;
            }
            if (phase == 0)
            {
                MoveTo(foePos + away * 10f + Vector2.up * (Flying ? 1f : 0f), Vector2.zero, CruiseSpeed, 1f);
                if (dist >= 8f || phaseClock > 1.2f) { phase = 1; phaseClock = 0f; holdPoint = me; abilities.ChargeKi(true); }
                return false;
            }
            MoveTo(holdPoint, Vector2.zero, CruiseSpeed * .5f, 1f);
            if (abilities.KiFraction >= .85f || phaseClock > 4f || (dist < 4f && strikeSeenAt >= 0f) || sensor.ThreatLevel > .5f) { abilities.ChargeKi(false); return true; }
            return false;
        }

        // The beat before a transformation: guard up, stand still, then the form change itself.
        bool TransformBeat(float now)
        {
            blocking = true;
            if (phase == 0)
            {
                if (phaseClock < .35f) return false;
                if (!abilities.CanTransform(transformSteps)) transformSteps = 1;
                commandedForm = true;
                if (!abilities.Transform(transformSteps)) { commandedForm = false; lastEscalationAt = now; return true; }
                pressure = Mathf.Max(0f, pressure - transformSteps); lastEscalationAt = now; Transforms++;
                phase = 1; phaseClock = 0f;
                return false;
            }
            return (phaseClock > 1f && !abilities.Transforming) || phaseClock > 4.5f;
        }

        // ---------------------------------------------------------------------------- reflex
        // Fast answers to things about to land. Each needs one reaction time of continuous sight first,
        // and only a share of them (by difficulty) is answered at all: the rest get through.
        void Reflex(float dt, float now)
        {
            if (Tactic == AiTactic.Transform || now < recoverUntil) { strikeSeenAt = -1f; return; }
            float delay = ReactionDelay;
            Rigidbody2D limb; float tti;
            bool striking = sensor.IncomingStrike(foe, out limb, out tti);
            if (striking) { if (strikeSeenAt < 0f) strikeSeenAt = now; parryLimb = limb; }
            else strikeSeenAt = -1f;
            var threat = sensor.TopThreat;
            bool shot = threat != null && threat.Tti < .9f;
            if (shot) { if (threatSeenAt < 0f || threat.Key != answeredKey) { threatSeenAt = now; answeredKey = threat.Key; answered = false; } }
            else { threatSeenAt = -1f; if (!striking) answered = false; }
            if (answered || AiTactic.IsDefensive(Tactic)) return;
            bool readStrike = striking && now - strikeSeenAt >= delay, readShot = shot && now - threat.FirstSeen >= delay && now - threatSeenAt >= delay * .5f;
            if (!readStrike && !readShot) return;
            answered = true;
            if (UnityEngine.Random.value > Profile.DodgeChance) return;         // read it, too late to answer
            // A committed strike is not pulled back; the answer waits for the limb to come home.
            if (strikes.Releasing) return;
            int mask = 0; float ki = abilities.KiFraction;
            if (readShot)
            {
                bool heavy = threat.Kind == AiTrackKind.Beam || threat.Kind == AiTrackKind.Orb;
                // A beam or a big sphere this fighter is strong enough for is caught by the mod's own guard: stand and meet it.
                if (heavy && BeamPhysics.On && BeamPhysics.CanReact(person, threat.Owner ? threat.Owner.transform.root : null)
                    && (threat.Kind == AiTrackKind.Beam ? BeamPhysics.Ratio(person, threat.Beam.Beam) : threat.Orb.RatioFor(person)) >= BeamPhysics.CatchRatio) return;
                if (stamina >= .2f) mask |= 1 << AiTactic.EvadeDash;
                if (abilities.HasShield && ki > .15f) mask |= 1 << AiTactic.KiShield;
                if (!heavy) mask |= (1 << AiTactic.GuardBlock) | (1 << AiTactic.EvadeWeave);
            }
            else
            {
                mask = (1 << AiTactic.GuardBlock) | (1 << AiTactic.EvadeWeave);
                if (stamina >= .2f) mask |= 1 << AiTactic.EvadeDash;
                if (rig.ChainUsable(KinematicsRig.ArmFront) || rig.ChainUsable(KinematicsRig.ArmBack)) mask |= 1 << AiTactic.Parry;
            }
            if (mask == 0) return;
            Priors();
            for (int i = 0; i < score.Length; i++) score[i] = prior[i] + logits[i];
            int action = Softmax(score, mask, Profile.Temperature, reflexProbability, true);
            strikes.AbortAll();
            EndTactic(); Begin(action);
        }

        // ------------------------------------------------------------------------------ idle
        void Idle(float dt, float now)
        {
            if (Tactic != AiTactic.FlightIntercept) { EndTactic(); Tactic = AiTactic.FlightIntercept; }
            // Vigilant: hold the spot and watch whoever is nearest.
            if (Flying) MoveTo(holdPoint, Vector2.zero, CruiseSpeed * .4f, .6f);
            AiTrack nearest = null; float best = 12f;
            for (int i = 0; i < sensor.Tracks.Length; i++)
            {
                var t = sensor.Tracks[i];
                if (t.Kind != AiTrackKind.Person) continue;
                float d = Vector2.Distance(t.Position, me);
                if (d < best) { best = d; nearest = t; }
            }
            if (nearest != null) rig.Aim(AiLimb.Head, 90f - Mathf.Clamp((nearest.Position.x - me.x) * 6f, -14f, 14f), 0f);
        }

        // ---------------------------------------------------------------------------- posture
        void Posture()
        {
            if (!foe.Valid) return;
            float side = toFoe.x >= 0f ? 1f : -1f;
            rig.Aim(AiLimb.Head, 90f - side * 8f, 0f);
            bool soft = Time.time < strikes.SoftUntil;
            for (int arm = 0; arm < 2; arm++)
            {
                if (armReserved[arm] || strikes.Busy(arm) || !rig.ChainUsable(arm)) continue;
                if (blocking)
                {
                    // Forearms crossed between the chest and whatever is coming.
                    var chain = rig.Chains[arm];
                    Vector2 root = rig.Anchor(rig.Parts[chain.Upper]);
                    Vector2 cross = root + new Vector2(side * chain.Reach * .34f, chain.Reach * (arm == 0 ? .3f : .16f));
                    rig.Reach(arm, cross, 2f, true);
                }
                else
                {
                    rig.Reach(arm, GuardPoint(arm), soft ? -.5f : 0f, false);
                    // A shoulder freed for a block, a shot or a strike gets its limits back once it is home.
                    rig.RestoreLimits(rig.Chains[arm].Upper);
                }
            }
            if (!Flying) return;
            // In the air the legs trail instead of hanging.
            for (int leg = KinematicsRig.LegFront; leg <= KinematicsRig.LegBack; leg++)
                if (!strikes.Busy(leg) && rig.ChainUsable(leg)) rig.Reach(leg, GuardPoint(leg), 0f, false);
        }

        // --------------------------------------------------------------------------- movement
        void Locomotion(float dt)
        {
            bool flying = Flying;
            Vector2 velocity = rig.ComVelocity;
            Vector2 goal = moving ? moveGoal : holdPoint;
            if (flying && sensor.GroundDistance < 29f) goal.y = Mathf.Max(goal.y, sensor.GroundPoint.y + rig.Height * .55f);
            if (ringActive)
            {
                // Leaving the ring is losing.
                goal.x = Mathf.Clamp(goal.x, ring.min.x + 1f, ring.max.x - 1f);
                goal.y = Mathf.Clamp(goal.y, ring.max.y + rig.Height * .55f, ring.max.y + 6f);
            }
            float cap = moving ? moveCap : CruiseSpeed * .4f;
            Vector2 want = Vector2.ClampMagnitude((goal - me) * 6f, cap) + (moving ? moveFeed : Vector2.zero);
            float limit = Mathf.Lerp(45f, 150f, Tier01) * (moving ? moveAccel : .6f) * (stamina < .2f ? .6f : 1f);
            Vector2 a = Vector2.ClampMagnitude((want - velocity) / .1f, limit);
            if (flying) a += want * (.05f / dt);            // what the flight brake takes off every step
            else
            {
                if (!person.IsTouchingFloor) return;         // no flight, no footing: ballistic
                a.y = 0f; a = Vector2.ClampMagnitude(a, 32f);
            }
            if (strikes.Releasing)
            {
                // The controller lets go along the strike line so it does not brake the lunge.
                Vector2 axis = strikes.ReleaseAxis;
                a -= axis * Vector2.Dot(a, axis);
            }
            rig.Accelerate(a);
            if (flying) rig.Attitude(Mathf.Clamp(-velocity.x * 1.2f, -16f, 16f) + leanBias, 9f, 420f, 2600f, dt);
        }

        // ---------------------------------------------------------------------------- results
        public void OnStrikeFinished(ref AiStrikeRecord record)
        {
            float now = Time.time;
            switch (record.Result)
            {
                case AiStrikeResult.Hit:
                    windowHits++; lastHitAt = now;
                    // Hitstop on a clean heavy hit, unless Impact Frames is already doing it.
                    if (AutonomousConfig.Cinematics && record.GateSpeed >= 15f && record.DamagePercent >= 3f && now - lastHitstopAt > .6f && (impactFrames == null || !impactFrames.Enabled))
                    {
                        lastHitstopAt = now;
                        SingularityTime.Hitstop(.06f);
                        DbpuFx.Sound(rig.Com, "Assets/SFX/Techniques/KiFist/HitHeavy.wav", .7f);
                    }
                    break;
                case AiStrikeResult.Stopped: windowStopped++; pressure += .2f; lastHitAt = now; break;
                case AiStrikeResult.Ignored: lastHitAt = now; break;
                case AiStrikeResult.Whiff: windowWhiffs++; recoverUntil = Mathf.Max(recoverUntil, strikes.SoftUntil); break;
            }
        }
    }
}
