using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    public enum BuuRecoveryStage { Idle, Rest, Quiver, Gather, Grow }

    /// <summary>
    /// A single Majin body. Its original native limbs are reassembled; no substitute
    /// Person, head core, hidden immortal object or cloned fighter is spawned.
    /// </summary>
    public sealed class BuuRegeneration : MonoBehaviour, Messages.IOnBeforeSerialise, Messages.IOnAfterDeserialise
    {
        public bool RegenerationEnabled = true;
        public int RecoveryCount;
        // Plain data intentionally survives the game's copy/save serialization.
        public Blueprint[] BodyBlueprint;
        public bool[] ErasedMatter;
        public NativeStatSnapshot[] NativeStats;

        [Serializable]
        public struct NativeStatSnapshot
        {
            public bool HasLimb;
            public float Health;
            public float InitialHealth;
            public float BreakingThreshold;
            public float GForcePassoutThreshold;
            public float ImpactDamageMultiplier;
        }

        [Serializable]
        public struct Blueprint
        {
            public Vector3 Position;
            public float Angle;
            public Vector3 Scale;
            public int Parent;
            public int Source;
            public int BrainNeighbour;
            public int[] Connected;
            public int[] Nodes;
            public int[] PushesTo;
            public bool IsRoot;
            public bool HasJoint;
            public bool Pump;
            public Vector2 Anchor;
            public Vector2 ConnectedAnchor;
            public JointAngleLimits2D Limits;
            public JointMotor2D Motor;
            public bool UseLimits;
            public bool UseMotor;
            public bool EnableCollision;
            public float BreakForce;
            public float BreakTorque;
            public float BreakingThreshold;
            public float RegenerationSpeed;
            public float Gravity;
            public RigidbodyType2D BodyType;
            public bool[] RendererEnabled;
            public bool[] ColliderEnabled;
            public bool Simulated;
        }

        sealed class Tissue
        {
            public LimbBehaviour Limb;
            public PhysicalBehaviour Physical;
            public Rigidbody2D Body;
            public Collider2D[] Colliders;
            public bool[] ColliderEnabled;
            public Renderer[] Renderers;
            public bool[] RendererEnabled;
            public bool Simulated;
            public bool SourceMatter;
            public bool WasDisintegrated;
            public Vector3 From;
            public float FromAngle;
            public Vector3 FromScale;
            public float LastWoundTime = -10f;
            public float NextTwitch;
        }

        sealed class Remnant
        {
            public PhysicalBehaviour Physical;
            public int LimbIndex;
            public bool Scattered;
            public float NextTwitch;
            public float Seed;
            public bool Erased;
        }

        [SkipSerialisation] PersonBehaviour person;
        [SkipSerialisation] Tissue[] tissues;
        [SkipSerialisation] BuuRegenerationFx effects;
        [SkipSerialisation] BuuAssemblyView assembly;
        [SkipSerialisation] bool[] rebuildingLimbs;
        [SkipSerialisation] PhysicalBehaviour disintegratingSource;
        [SkipSerialisation] bool aborting;
        [SkipSerialisation] Barrage[] suspendedBarrages;
        [SkipSerialisation] bool[] barrageEnabled;
        [SkipSerialisation] DisintegrationCounterBehaviour disintegrationCounter;
        [SkipSerialisation] int anchorIndex = -1;
        [SkipSerialisation] bool initialized;
        [SkipSerialisation] bool reforming;
        [SkipSerialisation] float damageAge;
        [SkipSerialisation] float reformAge;
        [SkipSerialisation] float reformDuration;
        [SkipSerialisation] bool personEnabled;
        [SkipSerialisation] List<Remnant> remnants = new List<Remnant>();
        [SkipSerialisation] Remnant remnantAnchor;
        [SkipSerialisation] float nextRemnantScan;
        [SkipSerialisation] float remnantAngle;
        [SkipSerialisation] bool uprightRecovery;
        [SkipSerialisation] int regrowingCount;
        [SkipSerialisation] BuuRecoveryStage stage;
        [SkipSerialisation] float stageAge;
        [SkipSerialisation] float quietAge;
        [SkipSerialisation] bool growthStarted;
        [SkipSerialisation] bool lateMatter;
        [SkipSerialisation] float groundLift;
        [SkipSerialisation] readonly RaycastHit2D[] groundHits = new RaycastHit2D[32];
        [SkipSerialisation] readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];

        public bool IsReforming { get { return reforming; } }
        public BuuRecoveryStage Stage { get { return stage; } }
        public float StageAge { get { return stageAge; } }
        public int AssemblyPieceCount(int index)
        { return assembly && assembly.PieceCounts != null && index >= 0 && index < assembly.PieceCounts.Length ? assembly.PieceCounts[index] : 0; }
        public int AssembledPieceCount(int index)
        { return assembly && assembly.ArrivedCounts != null && index >= 0 && index < assembly.ArrivedCounts.Length ? assembly.ArrivedCounts[index] : 0; }

        public static BuuRegeneration Attach(PersonBehaviour owner)
        {
            if (!owner) return null;
            var regeneration = owner.GetComponent<BuuRegeneration>();
            if (!regeneration) regeneration = owner.gameObject.AddComponent<BuuRegeneration>();
            regeneration.Initialize();
            return regeneration;
        }

        void Start() { Initialize(); }

        void Initialize()
        {
            if (initialized) return;
            person = GetComponent<PersonBehaviour>();
            if (!person || person.Limbs == null || person.Limbs.Length == 0) return;
            var limbs = person.Limbs;
            bool capture = BodyBlueprint == null || BodyBlueprint.Length != limbs.Length;
            if (capture) BodyBlueprint = new Blueprint[limbs.Length];
            if (ErasedMatter == null || ErasedMatter.Length != limbs.Length) ErasedMatter = new bool[limbs.Length];
            tissues = new Tissue[limbs.Length];
            for (int i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (!limb) continue;
                var tissue = new Tissue
                {
                    Limb = limb, Physical = limb.PhysicalBehaviour,
                    Body = limb.GetComponent<Rigidbody2D>(),
                    Colliders = limb.GetComponentsInChildren<Collider2D>(true),
                    Renderers = limb.GetComponentsInChildren<Renderer>(true)
                };
                tissue.ColliderEnabled = new bool[tissue.Colliders.Length];
                tissue.RendererEnabled = new bool[tissue.Renderers.Length];
                for (int k = 0; k < tissue.Colliders.Length; k++) tissue.ColliderEnabled[k] = tissue.Colliders[k].enabled;
                for (int k = 0; k < tissue.Renderers.Length; k++) tissue.RendererEnabled[k] = tissue.Renderers[k].enabled;
                tissue.Simulated = tissue.Body && tissue.Body.simulated;
                tissues[i] = tissue;
                if (capture) BodyBlueprint[i] = Capture(limb, limbs);
                if (capture)
                {
                    BodyBlueprint[i].RendererEnabled = (bool[])tissue.RendererEnabled.Clone();
                    BodyBlueprint[i].ColliderEnabled = (bool[])tissue.ColliderEnabled.Clone();
                    BodyBlueprint[i].Simulated = tissue.Simulated;
                }
                else
                {
                    var blueprint = BodyBlueprint[i];
                    if (blueprint.RendererEnabled != null && blueprint.RendererEnabled.Length == tissue.RendererEnabled.Length)
                        tissue.RendererEnabled = (bool[])blueprint.RendererEnabled.Clone();
                    if (blueprint.ColliderEnabled != null && blueprint.ColliderEnabled.Length == tissue.ColliderEnabled.Length)
                        tissue.ColliderEnabled = (bool[])blueprint.ColliderEnabled.Clone();
                    tissue.Simulated = blueprint.Simulated;
                }
                // Only the root coordinator owns recovery. The head can disappear.
                tissue.Physical.OnDisintegration += OnTissueDisintegrated;
                AddMenus(limb.PhysicalBehaviour);
            }
            disintegrationCounter = GetComponentInParent<DisintegrationCounterBehaviour>();
            MarkRuntimeAudio();
            effects = BuuRegenerationFx.Attach(person);
            assembly = BuuAssemblyView.Attach(person);
            initialized = true;
        }

        void MarkRuntimeAudio()
        {
            // These internal children are recreated by the fighter's spawn recipe.
            // Saving them too produces duplicate paths on the second native copy.
            foreach (var audio in GetComponentsInChildren<AudioSource>(true))
            {
                if (!audio || audio.GetComponent<PhysicalBehaviour>()) continue;
                string name = audio.gameObject.name;
                if (name != "BeamAudioSource" && name != "BlastAudioSource" && name != "AuraAudioSource") continue;
                var limb = audio.transform.parent ? audio.transform.parent.GetComponent<LimbBehaviour>() : null;
                if (limb && limb.Person == person && !audio.GetComponent<Optout>()) audio.gameObject.AddComponent<Optout>();
            }
        }

        Blueprint Capture(LimbBehaviour limb, LimbBehaviour[] limbs)
        {
            var result = new Blueprint
            {
                Position = transform.InverseTransformPoint(limb.transform.position),
                Parent = -1,
                Angle = Mathf.DeltaAngle(transform.eulerAngles.z, limb.transform.eulerAngles.z),
                Scale = limb.transform.localScale,
                Source = IndexOf(limbs, limb.CirculationBehaviour.Source ? limb.CirculationBehaviour.Source.Limb : null),
                BrainNeighbour = IndexOf(limbs, limb.NearestLimbToBrain),
                Connected = Indices(limb.ConnectedLimbs, limbs),
                Nodes = NodeIndices(limb.NodeBehaviour.Connections, limbs),
                PushesTo = CirculationIndices(limb.CirculationBehaviour.PushesTo, limbs),
                IsRoot = limb.NodeBehaviour.IsRoot,
                HasJoint = limb.HasJoint && limb.Joint,
                Pump = limb.CirculationBehaviour.WasInitiallyPumping || limb.CirculationBehaviour.IsPump,
                BreakingThreshold = limb.BreakingThreshold,
                RegenerationSpeed = limb.RegenerationSpeed,
                Gravity = limb.PhysicalBehaviour.rigidbody.gravityScale,
                BodyType = limb.PhysicalBehaviour.rigidbody.bodyType
            };
            if (result.HasJoint)
            {
                var joint = limb.Joint;
                result.Parent = IndexOf(limbs, joint.connectedBody ? joint.connectedBody.GetComponent<LimbBehaviour>() : null);
                result.Anchor = joint.anchor;
                // Character art layouts can move limbs after native prefab joints
                // were authored. Cache a coincident pivot in the actual rest pose.
                result.ConnectedAnchor = joint.connectedBody
                    ? (Vector2)joint.connectedBody.transform.InverseTransformPoint(limb.transform.TransformPoint(joint.anchor))
                    : joint.connectedAnchor;
                result.Limits = joint.limits;
                result.Motor = joint.motor;
                result.UseLimits = joint.useLimits;
                result.UseMotor = joint.useMotor;
                result.EnableCollision = joint.enableCollision;
                result.BreakForce = joint.breakForce;
                result.BreakTorque = joint.breakTorque;
            }
            return result;
        }

        static int IndexOf(LimbBehaviour[] limbs, LimbBehaviour limb)
        { return limb ? Array.IndexOf(limbs, limb) : -1; }

        static int[] Indices(List<LimbBehaviour> list, LimbBehaviour[] limbs)
        {
            if (list == null) return new int[0];
            int[] result = new int[list.Count];
            for (int i = 0; i < result.Length; i++) result[i] = IndexOf(limbs, list[i]);
            return result;
        }

        static int[] NodeIndices(ConnectedNodeBehaviour[] nodes, LimbBehaviour[] limbs)
        {
            if (nodes == null) return new int[0];
            int[] result = new int[nodes.Length];
            for (int i = 0; i < result.Length; i++) result[i] = IndexOf(limbs, nodes[i] ? nodes[i].GetComponent<LimbBehaviour>() : null);
            return result;
        }

        static int[] CirculationIndices(CirculationBehaviour[] circulation, LimbBehaviour[] limbs)
        {
            if (circulation == null) return new int[0];
            int[] result = new int[circulation.Length];
            for (int i = 0; i < result.Length; i++) result[i] = IndexOf(limbs, circulation[i] ? circulation[i].Limb : null);
            return result;
        }

        void AddMenus(PhysicalBehaviour physical)
        {
            if (!physical || !physical.ContextMenuOptions) return;
            physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("buuRegeneration", () => RegenerationEnabled ? "Disable Majin regeneration" : "Enable Majin regeneration", "Allow surviving Majin matter to repair and reassemble this body", () =>
            {
                RegenerationEnabled = !RegenerationEnabled;
                if (!RegenerationEnabled && reforming) StopRecovery();
            }));
            physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("buuReassemble", "Reassemble Majin body", "Gather the surviving pieces and regrow missing tissue", Reassemble));
        }

        void Update()
        {
            if (!initialized) Initialize();
            if (!initialized || !person || (Global.main && Global.main.Paused)) return;
            // Skin replacement destroys prefab colliders after this component may
            // already have captured the body. Refresh native cached pointers once.
            bool staleCollider = false;
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Limb && !tissues[i].Limb.Collider) { staleCollider = true; break; }
            if (staleCollider)
            {
                RefreshColliderCache();
                RestoreSelfCollisions();
                BuuMatter.ReapplyInterior(person);
            }
            if (person.GetComponent<HakaiDissolveMarker>())
            {
                if (reforming) CancelForErasure();
                return;
            }
            if (person.GetComponent<TimePrison>() || RealityRift.Active ||
                (SingularityWorldFreeze.Active && !SingularityWorldFreeze.Current.IsExempt(transform))) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (reforming)
            {
                if (!RegenerationEnabled) { StopRecovery(); return; }
                AdvanceReform(dt);
                return;
            }
            if (!RegenerationEnabled) return;
            if (lateMatter) ReclaimLateMatter(dt);
            bool damage = NeedsReassembly();
            damageAge = damage ? damageAge + dt : 0f;
            if (!HasNativeMatter())
            {
                if (Time.time >= nextRemnantScan) ScanRemnants(false);
                if (FindRemnant() == null) return;
            }
            RestoreMajinMind();
            for (int i = 0; i < tissues.Length; i++)
                if (IsMatter(i)) Heal(tissues[i], BodyBlueprint[i], dt, false);
            if (damageAge >= .16f) BeginReform();
        }

        bool IsMatter(int index)
        {
            if (index < 0 || index >= tissues.Length) return false;
            var tissue = tissues[index];
            return tissue != null && tissue.Limb && tissue.Physical && tissue.Body &&
                !ErasedMatter[index] && tissue.Limb.gameObject.activeInHierarchy && !tissue.Physical.isDisintegrated;
        }

        bool HasNativeMatter()
        {
            for (int i = 0; i < tissues.Length; i++) if (IsMatter(i)) return true;
            return false;
        }

        static bool LiveRemnant(Remnant remnant)
        {
            return remnant != null && !remnant.Erased && remnant.Physical && remnant.Physical.gameObject.activeInHierarchy &&
                !remnant.Physical.isDisintegrated && remnant.Physical.rigidbody;
        }

        void ScanRemnants(bool originalSourcesOnly)
        {
            nextRemnantScan = Time.time + .2f;
            remnants.RemoveAll(r => !LiveRemnant(r));
            // Native Crush explicitly registers these fragments with THIS limb.
            // Never search the world or borrow matter belonging to another Buu.
            for (int i = 0; i < tissues.Length && remnants.Count < 256; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || ErasedMatter[i] || (originalSourcesOnly && !tissue.SourceMatter)) continue;
                var pieces = tissue.Limb.DestroyWith;
                if (pieces == null) continue;
                for (int k = pieces.Count - 1; k >= 0 && remnants.Count < 256; k--)
                {
                    if (!pieces[k]) continue;
                    var physical = pieces[k].GetComponent<PhysicalBehaviour>();
                    if (!physical || physical.isDisintegrated || !physical.gameObject.activeInHierarchy) continue;
                    bool known = false;
                    for (int p = 0; p < remnants.Count; p++) if (remnants[p].Physical == physical) { known = true; break; }
                    if (!known)
                    {
                        remnants.Add(new Remnant { Physical = physical, LimbIndex = i, Seed = Noise(i * 17.1f + k * 3.7f) });
                        physical.OnDisintegration += OnRemnantDisintegrated;
                        BuuFleshAssets.Attach(physical, i * 17 + k);
                        bool evil = person.name.IndexOf("Evil", StringComparison.OrdinalIgnoreCase) >= 0;
                        BuuFleshAssets.Tint(physical, evil ? new Color(.48f, .46f, .50f) : new Color(.93f, .48f, .68f));
                    }
                }
            }
        }

        Remnant FindRemnant()
        {
            Remnant best = null;
            float mass = -1f;
            foreach (var remnant in remnants)
                if (LiveRemnant(remnant) && remnant.Physical.rigidbody.mass > mass)
                { best = remnant; mass = remnant.Physical.rigidbody.mass; }
            return best;
        }

        bool SetRecoveryAnchor()
        {
            int native = FindAnchor(true);
            if (native >= 0)
            {
                anchorIndex = native;
                remnantAnchor = null;
                var tissue = tissues[native];
                tissue.Body.bodyType = BodyBlueprint[native].BodyType;
                tissue.Body.simulated = true;
                RestoreColliders(tissue);
                return true;
            }
            var shard = FindRemnant();
            if (shard == null)
            {
                ScanRemnants(true);
                shard = FindRemnant();
            }
            if (shard == null) return false;
            if (remnantAnchor != shard)
            {
                remnantAnchor = shard;
                anchorIndex = shard.LimbIndex;
                // Small fragments can spin rapidly; preserve a readable body frame.
                remnantAngle = transform.eulerAngles.z;
            }
            return true;
        }

        Vector3 PhysicalRecoveryOrigin()
        { return remnantAnchor != null ? remnantAnchor.Physical.transform.position : tissues[anchorIndex].Limb.transform.position; }

        Vector2 MovingAssemblyOrigin()
        {
            var limb = LimbAt(anchorIndex);
            return growthStarted && limb && !limb.PhysicalBehaviour.isDisintegrated
                ? (Vector2)limb.transform.position : (Vector2)PhysicalRecoveryOrigin();
        }

        Vector3 RecoveryOrigin()
        {
            return PhysicalRecoveryOrigin() + Vector3.up * (growthStarted ? groundLift : 0f);
        }

        float RecoveryAngle()
        { return remnantAnchor != null || uprightRecovery ? remnantAngle : tissues[anchorIndex].Limb.transform.eulerAngles.z - BodyBlueprint[anchorIndex].Angle; }

        // The largest connected surviving cluster wins, never a privileged head.
        int FindAnchor(bool originalMatterOnly)
        {
            float bestMass = -1f;
            int best = -1;
            bool[] visited = new bool[tissues.Length];
            for (int i = 0; i < tissues.Length; i++)
            {
                if (visited[i] || !IsMatter(i) || (originalMatterOnly && !tissues[i].SourceMatter)) continue;
                var queue = new Queue<int>();
                queue.Enqueue(i); visited[i] = true;
                float mass = 0f, largest = -1f;
                int candidate = i;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    var tissue = tissues[index];
                    float partMass = Mathf.Max(.001f, tissue.Body.mass);
                    mass += partMass;
                    if (partMass > largest) { largest = partMass; candidate = index; }
                    var connected = tissue.Limb.NodeBehaviour.Connections;
                    if (connected == null) continue;
                    for (int k = 0; k < connected.Length; k++)
                    {
                        if (!connected[k]) continue;
                        int j = IndexOf(person.Limbs, connected[k].GetComponent<LimbBehaviour>());
                        if (j < 0 || visited[j] || !IsMatter(j) || (originalMatterOnly && !tissues[j].SourceMatter)) continue;
                        visited[j] = true; queue.Enqueue(j);
                    }
                }
                if (mass > bestMass) { bestMass = mass; best = candidate; }
            }
            return best;
        }

        bool NeedsReassembly()
        {
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical) continue;
                if (tissue.Physical.isDisintegrated || tissue.Limb.IsDismembered) return true;
                var blueprint = BodyBlueprint[i];
                if (blueprint.HasJoint && LimbAt(blueprint.Parent) && (!tissue.Limb.HasJoint || !tissue.Limb.Joint)) return true;
            }
            return false;
        }

        public void Reassemble()
        {
            Initialize();
            if (initialized && RegenerationEnabled && !reforming) BeginReform();
        }

        void BeginReform()
        {
            if (person.GetComponent<HakaiDissolveMarker>()) return;
            RefreshColliderCache();
            ScanRemnants(false);
            // Capture actual surviving matter before any missing limb is activated.
            // These flags remain the source of truth through every later stage.
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb) continue;
                tissue.SourceMatter = IsMatter(i);
                tissue.NextTwitch = .08f + Noise(i * 7.3f) * .35f;
                if (tissue.Limb.Joint) tissue.Limb.Joint.useMotor = false;
            }
            anchorIndex = FindAnchor(false);
            remnantAnchor = null;
            if (anchorIndex < 0)
            {
                remnantAnchor = FindRemnant();
                if (remnantAnchor == null) return;
                anchorIndex = remnantAnchor.LimbIndex;
                remnantAngle = transform.eulerAngles.z;
            }
            int nativeSurvivors = 0;
            regrowingCount = 0;
            for (int i = 0; i < tissues.Length; i++)
            {
                if (IsMatter(i)) nativeSurvivors++;
                else if (tissues[i] != null && tissues[i].Limb) regrowingCount++;
            }
            uprightRecovery = nativeSurvivors <= 1;
            if (uprightRecovery) remnantAngle = transform.eulerAngles.z;
            reforming = true; reformAge = 0f; damageAge = 0f;
            stage = BuuRecoveryStage.Rest; stageAge = 0f; quietAge = 0f;
            growthStarted = false; groundLift = 0f;
            personEnabled = person.enabled;
            person.enabled = false;
            ScatterRemnants();
        }

        void BeginGrowth()
        {
            if (!EnsureRecoverySource()) { AbortWithoutMatter(); return; }
            SuspendOwnBarrages();
            bool[] intactCluster = AnchorCluster();
            // Detached original limbs become real owned crumbs before the hidden
            // native slot is moved to its final position. Their physical matter
            // remains where it actually landed and remains attackable.
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (!IsMatter(i) || !tissue.SourceMatter || intactCluster[i] || (i == anchorIndex && remnantAnchor == null)) continue;
                float chance = tissue.Limb.ShatteredObjectChance;
                tissue.Limb.ShatteredObjectChance = 1f;
                tissue.Limb.Crush();
                tissue.Limb.ShatteredObjectChance = chance;
            }
            ScanRemnants(true);
            var sourcePositions = new List<Vector2>(remnants.Count + tissues.Length);
            foreach (var remnant in remnants)
                if (LiveRemnant(remnant)) sourcePositions.Add(remnant.Physical.transform.position);
            for (int i = 0; i < tissues.Length; i++)
                if (IsMatter(i) && tissues[i].SourceMatter) sourcePositions.Add(tissues[i].Limb.transform.position);
            int actualSources = 0;
            regrowingCount = 0;
            rebuildingLimbs = new bool[tissues.Length];
            for (int i = 0; i < tissues.Length; i++)
            {
                if (IsMatter(i) && tissues[i].SourceMatter) actualSources++;
                else if (tissues[i] != null && tissues[i].Limb) regrowingCount++;
                rebuildingLimbs[i] = !intactCluster[i] || !IsMatter(i);
            }
            if (actualSources <= 1) { uprightRecovery = true; remnantAngle = transform.eulerAngles.z; }
            stage = BuuRecoveryStage.Grow; stageAge = 0f; reformAge = 0f;
            growthStarted = true;
            PlanGroundedGrowth();
            Vector3 recoveryOrigin = RecoveryOrigin();
            var sourceBody = remnantAnchor != null ? remnantAnchor.Physical.rigidbody : tissues[anchorIndex].Body;
            Vector2 inheritedVelocity = sourceBody.velocity;
            float inheritedSpin = remnantAnchor != null ? 0f : sourceBody.angularVelocity;
            float distance = 0f;
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical || !tissue.Body) continue;
                tissue.SourceMatter = tissue.SourceMatter && IsMatter(i);
                tissue.WasDisintegrated = tissue.Physical.isDisintegrated || !tissue.Limb.gameObject.activeSelf;
                tissue.From = tissue.Limb.transform.position;
                tissue.FromAngle = tissue.Limb.transform.eulerAngles.z;
                tissue.FromScale = tissue.Limb.transform.localScale;
                if (!tissue.SourceMatter)
                {
                    tissue.From = recoveryOrigin;
                    Reactivate(tissue);
                    tissue.Body.velocity = inheritedVelocity;
                    tissue.Body.angularVelocity = inheritedSpin;
                }
                tissue.Limb.transform.localScale = BodyBlueprint[i].Scale;
                distance = Mathf.Max(distance, Vector3.Distance(tissue.From, recoveryOrigin));
                if (tissue.Limb.Joint) tissue.Limb.Joint.enabled = false;
                var gore = tissue.Limb.GetComponent<GoreStringBehaviour>();
                if (gore) gore.DestroyJoint();
            }
            // Establish the scaffold once. From here native joints, motors and
            // dynamic rigidbodies own the pose; the visual pieces follow them.
            if (uprightRecovery && remnantAnchor == null)
                tissues[anchorIndex].Limb.transform.rotation = Quaternion.Euler(0f, 0f,
                    RecoveryAngle() + BodyBlueprint[anchorIndex].Angle);
            SetCanonicalGrowthPose();
            RestoreBodyTopology(false);
            if (person) person.enabled = personEnabled;
            if (assembly) assembly.Begin(sourcePositions.ToArray(), rebuildingLimbs);
            GateAssemblingColliders();
            RestoreSelfCollisions();
            reformDuration = Mathf.Clamp(3.2f + distance * .035f, 3.2f, 4.0f);
        }

        bool[] AnchorCluster()
        {
            var visited = new bool[tissues.Length];
            if (remnantAnchor != null || !IsMatter(anchorIndex)) return visited;
            var queue = new Queue<int>();
            queue.Enqueue(anchorIndex); visited[anchorIndex] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                var connections = tissues[current].Limb.NodeBehaviour.Connections;
                if (connections == null) continue;
                foreach (var node in connections)
                {
                    if (!node) continue;
                    int next = IndexOf(person.Limbs, node.GetComponent<LimbBehaviour>());
                    if (next < 0 || visited[next] || !IsMatter(next) || !tissues[next].SourceMatter) continue;
                    visited[next] = true; queue.Enqueue(next);
                }
            }
            return visited;
        }

        void SuspendOwnBarrages()
        {
            if (suspendedBarrages != null) return;
            var owned = new List<Barrage>();
            foreach (var barrage in GetComponentsInChildren<Barrage>(true))
            {
                if (!barrage) continue;
                var limb = barrage.GetComponent<LimbBehaviour>();
                if ((limb && limb.Person == person) || barrage.gameObject == gameObject) owned.Add(barrage);
            }
            suspendedBarrages = owned.ToArray();
            barrageEnabled = new bool[suspendedBarrages.Length];
            for (int i = 0; i < suspendedBarrages.Length; i++)
            {
                barrageEnabled[i] = suspendedBarrages[i].enabled;
                // Barrage.Update otherwise unconditionally reveals the arm sprites
                // after this body's constructor has deliberately hidden them.
                suspendedBarrages[i].enabled = false;
            }
        }

        void RestoreOwnBarrages()
        {
            if (suspendedBarrages == null) return;
            for (int i = 0; i < suspendedBarrages.Length; i++)
                if (suspendedBarrages[i]) suspendedBarrages[i].enabled = barrageEnabled[i];
            suspendedBarrages = null;
            barrageEnabled = null;
        }

        void SetCanonicalGrowthPose()
        {
            Vector3 origin = RecoveryOrigin();
            float angle = RecoveryAngle();
            Quaternion rotate = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(transform.eulerAngles.z, angle));
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical || tissue.Physical.isDisintegrated) continue;
                if (i != anchorIndex || remnantAnchor != null)
                {
                    tissue.Limb.transform.position = origin + rotate * transform.TransformVector(BodyBlueprint[i].Position - BodyBlueprint[anchorIndex].Position);
                    tissue.Limb.transform.rotation = Quaternion.Euler(0f, 0f, angle + BodyBlueprint[i].Angle);
                }
                tissue.Limb.transform.localScale = BodyBlueprint[i].Scale;
            }
        }

        void GateAssemblingColliders()
        {
            if (rebuildingLimbs == null) return;
            bool changed = false;
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical || tissue.Physical.isDisintegrated) continue;
                // Dynamic limbs need contact with the world before the final
                // visible piece arrives, including feet growing on solid ground.
                foreach (var collider in tissue.Colliders) if (collider && !collider.enabled && !collider.isTrigger) { changed = true; break; }
                RestoreColliders(tissue);
            }
            if (changed) RestoreSelfCollisions();
        }

        void Reactivate(Tissue tissue)
        {
            tissue.Physical.isDisintegrated = false;
            tissue.Limb.gameObject.SetActive(true);
            tissue.Physical.enabled = true;
            tissue.Limb.enabled = true;
            for (int r = 0; r < tissue.Renderers.Length; r++)
                if (tissue.Renderers[r]) tissue.Renderers[r].enabled = tissue.RendererEnabled[r];
            // Disintegrated limbs loaded from saves have disabled renderers. The
            // primary skin renderer must become visible when new tissue grows.
            if (tissue.Physical.spriteRenderer) tissue.Physical.spriteRenderer.enabled = true;
            if (tissue.WasDisintegrated && disintegrationCounter)
                disintegrationCounter.DisintegrationCount = Mathf.Max(0, disintegrationCounter.DisintegrationCount - 1);
        }

        static float Noise(float seed)
        { return Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f); }

        bool EnsureRecoverySource()
        {
            if (remnantAnchor != null && LiveRemnant(remnantAnchor)) return true;
            if (remnantAnchor == null && IsMatter(anchorIndex) && tissues[anchorIndex].SourceMatter) return true;
            bool found = SetRecoveryAnchor();
            if (found && growthStarted) PlanGroundedGrowth();
            return found;
        }

        void ScatterRemnants()
        {
            Vector2 centre = PhysicalRecoveryOrigin();
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant) || remnant.Scattered) continue;
                remnant.Scattered = true;
                remnant.NextTwitch = .1f + remnant.Seed * .34f;
                var body = remnant.Physical.rigidbody;
                BuuFleshAssets.Animate(remnant.Physical, 0f, 0f);
                if (body.bodyType != RigidbodyType2D.Dynamic) continue;
                Vector2 direction = body.position - centre;
                if (direction.sqrMagnitude < .005f)
                    direction = new Vector2(Mathf.Cos(remnant.Seed * 23f), .15f + remnant.Seed);
                direction.Normalize();
                // Keep the native blast's velocity. Only a modest, bounded extra
                // tear impulse gives previously stationary crumbs their own flight.
                float kick = Mathf.Clamp01(1f - body.velocity.magnitude / 16f);
                Vector2 impulse = (direction * (1.3f + remnant.Seed * 2.4f) + Vector2.up * .8f) * kick;
                body.AddForce(impulse * body.mass, ForceMode2D.Impulse);
                body.AddTorque((remnant.Seed - .5f) * body.mass * 1.2f * kick, ForceMode2D.Impulse);
            }
        }

        void TwitchBody(Rigidbody2D body, float seed, ref float nextTwitch, float dt)
        {
            float delay = seed * .32f;
            if (!body || body.bodyType != RigidbodyType2D.Dynamic || stageAge < delay) return;
            float strength = Mathf.Clamp01((stageAge - delay) / .38f);
            if (stageAge < nextTwitch) return;
            float beat = Noise(seed * 31f + nextTwitch * 9.7f);
            nextTwitch = stageAge + .08f + beat * .16f;
            Vector2 toward = (Vector2)PhysicalRecoveryOrigin() - body.position;
            if (toward.sqrMagnitude > .001f) toward.Normalize();
            Vector2 impulse = new Vector2((beat - .5f) * .18f, .10f + beat * .11f) + toward * .08f;
            body.AddForce(impulse * body.mass * strength, ForceMode2D.Impulse);
            body.AddTorque((beat - .5f) * body.mass * .12f * strength, ForceMode2D.Impulse);
        }

        void QuiverMatter(float dt)
        {
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                TwitchBody(body, remnant.Seed, ref remnant.NextTwitch, dt);
                float quiverStrength = Mathf.Clamp01((stageAge - remnant.Seed * .32f) / .38f);
                BuuFleshAssets.Animate(remnant.Physical, .12f + quiverStrength * .48f, Mathf.Max(0f, Mathf.Sin(stageAge * 23f + remnant.Seed * 31f)) * quiverStrength);
                if (effects)
                {
                    float strength = Mathf.Clamp01((stageAge - remnant.Seed * .32f) / .38f);
                    effects.Quiver(body.position, Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.001f, body.mass)) * .10f, .045f, .15f), strength);
                }
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (!IsMatter(i) || !tissue.SourceMatter) continue;
                if (!tissue.Limb.IsDismembered && tissue.Limb.NodeBehaviour.IsConnectedToRoot && regrowingCount < tissues.Length - 1) continue;
                float seed = Noise(i * 7.3f);
                TwitchBody(tissue.Body, seed, ref tissue.NextTwitch, dt);
                if (effects) effects.Quiver(tissue.Body.position, .12f, Mathf.Clamp01((stageAge - seed * .32f) / .38f));
            }
        }

        void SteerMatter(Rigidbody2D body, Vector2 target, float seed, float dt, float urgency)
        {
            if (!body || body.bodyType != RigidbodyType2D.Dynamic) return;
            Vector2 delta = target - body.position;
            float distance = delta.magnitude;
            if (distance < .08f) return;
            Vector2 direction = delta / distance;
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            float pulse = .65f + .35f * Mathf.Sin(Time.time * (7f + seed * 5f) + seed * 41f);
            float speed = Mathf.Min(18f, distance * (3.3f + urgency * 2f)) * (.65f + urgency * .35f);
            Vector2 desire = direction * speed + tangent * Mathf.Sin(Time.time * 10f + seed * 30f) * Mathf.Min(.9f, distance * .28f);
            desire += Vector2.up * Mathf.Max(0f, pulse - .75f) * 2f;
            Vector2 acceleration = Vector2.ClampMagnitude((desire - body.velocity) * (4.5f + pulse * 3f), 32f);
            body.AddForce(acceleration * body.mass * dt, ForceMode2D.Impulse);
        }

        void GatherMatter(float dt, float progress)
        {
            Vector2 origin = PhysicalRecoveryOrigin();
            if (effects) effects.Gather(origin, progress);
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant) || remnant == remnantAnchor) continue;
                var body = remnant.Physical.rigidbody;
                SteerMatter(body, origin, remnant.Seed, dt, progress);
                BuuFleshAssets.Animate(remnant.Physical, .7f + progress * .3f, Mathf.Clamp01(1f - Vector2.Distance(body.position, origin) / 3f));
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (!IsMatter(i) || !tissue.SourceMatter || (i == anchorIndex && remnantAnchor == null)) continue;
                if (!tissue.Limb.IsDismembered && tissue.Limb.NodeBehaviour.IsConnectedToRoot) continue;
                SteerMatter(tissue.Body, origin, Noise(i * 7.3f), dt, progress);
            }
        }

        bool OwnCollider(Collider2D collider)
        {
            if (!collider) return false;
            if (collider.transform.root == transform.root) return true;
            var physical = collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<PhysicalBehaviour>() : null;
            if (!physical) return false;
            foreach (var remnant in remnants) if (remnant.Physical == physical) return true;
            return false;
        }

        void PlanGroundedGrowth()
        {
            groundLift = 0f;
            if (remnantAnchor == null || !LiveRemnant(remnantAnchor)) return;
            Vector2 seed = remnantAnchor.Physical.rigidbody.position;
            float ground = float.NegativeInfinity;
            int contacts = remnantAnchor.Physical.rigidbody.GetContacts(groundContacts);
            for (int i = 0; i < contacts; i++)
            {
                var contact = groundContacts[i];
                Collider2D other = OwnCollider(contact.collider) ? contact.otherCollider : contact.collider;
                if (!other || other.isTrigger || OwnCollider(other) || Mathf.Abs(contact.normal.y) < .45f || contact.point.y > seed.y + .15f) continue;
                ground = Mathf.Max(ground, contact.point.y);
            }
            if (float.IsNegativeInfinity(ground))
            {
                int count = Physics2D.RaycastNonAlloc(seed + Vector2.up * .3f, Vector2.down, groundHits, 1.4f);
                for (int i = 0; i < count; i++)
                {
                    var hit = groundHits[i];
                    if (!hit.collider || hit.collider.isTrigger || OwnCollider(hit.collider) || hit.normal.y < .45f) continue;
                    ground = Mathf.Max(ground, hit.point.y);
                }
            }
            if (float.IsNegativeInfinity(ground)) return;
            float lowest = 0f;
            Quaternion rotate = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(transform.eulerAngles.z, RecoveryAngle()));
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical.spriteRenderer || !tissue.Physical.spriteRenderer.sprite) continue;
                Vector3 offset = rotate * transform.TransformVector(BodyBlueprint[i].Position - BodyBlueprint[anchorIndex].Position);
                float bottom = tissue.Physical.spriteRenderer.sprite.bounds.min.y * Mathf.Abs(BodyBlueprint[i].Scale.y * tissue.Limb.transform.parent.lossyScale.y);
                lowest = Mathf.Min(lowest, offset.y + bottom);
            }
            groundLift = Mathf.Max(0f, ground + .02f - seed.y - lowest);
        }

        void StopRecovery()
        {
            if (growthStarted) { FinishReform(); return; }
            reforming = false; stage = BuuRecoveryStage.Idle; stageAge = 0f;
            if (person) person.enabled = personEnabled;
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Limb && tissues[i].Limb.Joint)
                    tissues[i].Limb.Joint.useMotor = BodyBlueprint[i].UseMotor;
            RestoreOwnBarrages();
        }

        void AdvanceReform(float dt)
        {
            // Only matter present before this recovery may sustain it. Freshly
            // growing shapes cannot rescue an erased final survivor.
            if (!EnsureRecoverySource()) { AbortWithoutMatter(); return; }
            stageAge += dt;
            if (stage == BuuRecoveryStage.Rest)
            {
                ScatterRemnants();
                // Ballistic flight comes first. Most surviving tissue then gets
                // about a full second of stillness before the first living twitch.
                quietAge = QuietMatterFraction() >= .55f ? quietAge + dt : Mathf.Max(0f, quietAge - dt * 2f);
                if (quietAge >= 1.0f || stageAge >= 2.25f)
                {
                    stage = BuuRecoveryStage.Quiver; stageAge = 0f;
                    if (effects) effects.Begin(PhysicalRecoveryOrigin());
                }
                return;
            }
            if (stage == BuuRecoveryStage.Quiver)
            {
                QuiverMatter(dt);
                if (stageAge >= .78f) { stage = BuuRecoveryStage.Gather; stageAge = 0f; }
                return;
            }
            if (stage == BuuRecoveryStage.Gather)
            {
                GatherMatter(dt, Mathf.Clamp01(stageAge / 1.5f));
                if ((stageAge >= 1.25f && GatheredMassFraction() >= .4f) || stageAge >= 1.85f) BeginGrowth();
                return;
            }
            if (!growthStarted) { BeginGrowth(); return; }
            reformAge += dt;
            float progress = Mathf.Clamp01(reformAge / reformDuration);
            if (assembly) assembly.Tick(progress);
            GateAssemblingColliders();
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical || !tissue.Body) continue;
                if (tissue.Physical.isDisintegrated) continue; // A blast may interrupt recovery.
                Heal(tissue, BodyBlueprint[i], dt, false);
            }
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                Vector2 destination = MovingAssemblyOrigin();
                SteerMatter(body, destination, remnant.Seed, dt, 1f);
                BuuFleshAssets.Animate(remnant.Physical, .65f + Mathf.Sin(progress * 12f + remnant.Seed * 19f) * .2f, progress);
            }
            RestoreMajinMind();
            if (progress >= 1f) FinishReform();
        }

        float GatheredMassFraction()
        {
            float all = 0f, gathered = 0f;
            Vector2 centre = PhysicalRecoveryOrigin();
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                float mass = Mathf.Max(.001f, remnant.Physical.rigidbody.mass);
                all += mass;
                if (((Vector2)remnant.Physical.transform.position - centre).sqrMagnitude < 2.25f) gathered += mass;
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                if (!IsMatter(i) || !tissues[i].SourceMatter) continue;
                float mass = Mathf.Max(.001f, tissues[i].Body.mass);
                all += mass;
                if ((tissues[i].Body.position - centre).sqrMagnitude < 2.25f) gathered += mass;
            }
            return all > 0f ? gathered / all : 0f;
        }

        float QuietMatterFraction()
        {
            float all = 0f, quiet = 0f;
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                float mass = Mathf.Max(.001f, body.mass);
                all += mass;
                if (body.velocity.sqrMagnitude < .2025f && Mathf.Abs(body.angularVelocity) < 45f) quiet += mass;
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                if (!IsMatter(i) || !tissues[i].SourceMatter) continue;
                var tissue = tissues[i];
                if (!tissue.Limb.IsDismembered && tissue.Limb.NodeBehaviour.IsConnectedToRoot && regrowingCount < tissues.Length - 1) continue;
                float mass = Mathf.Max(.001f, tissue.Body.mass);
                all += mass;
                if (tissue.Body.velocity.sqrMagnitude < .2025f && Mathf.Abs(tissue.Body.angularVelocity) < 45f) quiet += mass;
            }
            return all > 0f ? quiet / all : 1f;
        }

        LimbBehaviour LimbAt(int index)
        { return index >= 0 && index < tissues.Length && tissues[index] != null ? tissues[index].Limb : null; }

        void RestoreBodyTopology(bool complete)
        {
            // Rebuild only the body's original graph. Missing GameObjects deliberately
            // stay missing: native Delete and scene clearing must never spawn a clone.
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical || tissue.Physical.isDisintegrated) continue;
                var limb = tissue.Limb;
                var blueprint = BodyBlueprint[i];
                limb.transform.localScale = blueprint.Scale;
                limb.IsDismembered = false;
                limb.BreakingThreshold = blueprint.BreakingThreshold;
                limb.RegenerationSpeed = blueprint.RegenerationSpeed;
                limb.CurrentlyShattered = LimbBehaviour.ShatterFlags.None;
                var parent = LimbAt(blueprint.Parent);
                if (blueprint.HasJoint && parent && !parent.PhysicalBehaviour.isDisintegrated)
                {
                    if (!limb.Joint) limb.Joint = limb.gameObject.AddComponent<HingeJoint2D>();
                    var joint = limb.Joint;
                    joint.autoConfigureConnectedAnchor = false;
                    joint.connectedBody = parent.PhysicalBehaviour.rigidbody;
                    joint.anchor = blueprint.Anchor;
                    joint.connectedAnchor = blueprint.ConnectedAnchor;
                    joint.limits = blueprint.Limits;
                    limb.OriginalJointLimits = new Vector2(blueprint.Limits.min, blueprint.Limits.max);
                    joint.motor = blueprint.Motor;
                    joint.useLimits = blueprint.UseLimits;
                    joint.useMotor = blueprint.UseMotor;
                    joint.enableCollision = blueprint.EnableCollision;
                    joint.breakForce = blueprint.BreakForce;
                    joint.breakTorque = blueprint.BreakTorque;
                    joint.enabled = true;
                    limb.HasJoint = true;
                }
                else limb.HasJoint = limb.Joint;
                limb.ConnectedLimbs = new List<LimbBehaviour>();
                foreach (int j in blueprint.Connected ?? new int[0])
                {
                    var connected = LimbAt(j);
                    if (connected && !connected.PhysicalBehaviour.isDisintegrated) limb.ConnectedLimbs.Add(connected);
                }
                var nodes = new List<ConnectedNodeBehaviour>();
                foreach (int j in blueprint.Nodes ?? new int[0])
                {
                    var connected = LimbAt(j);
                    if (connected && !connected.PhysicalBehaviour.isDisintegrated) nodes.Add(connected.NodeBehaviour);
                }
                limb.NodeBehaviour.Connections = nodes.ToArray();
                limb.NodeBehaviour.IsRoot = blueprint.IsRoot;
                var source = LimbAt(blueprint.Source);
                limb.CirculationBehaviour.Source = source && !source.PhysicalBehaviour.isDisintegrated ? source.CirculationBehaviour : null;
                var pushes = new List<CirculationBehaviour>();
                foreach (int j in blueprint.PushesTo ?? new int[0])
                {
                    var child = LimbAt(j);
                    if (child && !child.PhysicalBehaviour.isDisintegrated) pushes.Add(child.CirculationBehaviour);
                }
                limb.CirculationBehaviour.PushesTo = pushes.ToArray();
                limb.NearestLimbToBrain = LimbAt(blueprint.BrainNeighbour);
                limb.CirculationBehaviour.IsDisconnected = false;
                Heal(tissue, blueprint, complete ? 1f : .05f, complete);
                RestoreColliders(tissue);
                tissue.Body.simulated = tissue.Simulated || tissue.WasDisintegrated;
                tissue.Body.gravityScale = blueprint.Gravity;
                tissue.Body.bodyType = blueprint.BodyType;
                if (complete) ErasedMatter[i] = false; // This is new tissue grown from a valid survivor.
                limb.NodeBehaviour.ResetValue();
            }
            RestoreSelfCollisions();
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Limb && tissues[i].Limb.NodeBehaviour.IsRoot)
                    tissues[i].Limb.NodeBehaviour.RootPropagation();
            RestoreMajinMind();
        }

        void FinishReform()
        {
            if (!reforming) return;
            if (!growthStarted)
            {
                BeginGrowth();
                if (!reforming || !growthStarted) return;
            }
            if (!SetRecoveryAnchor()) { AbortWithoutMatter(); return; }
            reformAge = reformDuration;
            Vector2 collectedAt = MovingAssemblyOrigin();
            reforming = false;
            stage = BuuRecoveryStage.Idle; stageAge = 0f;
            if (person) person.enabled = personEnabled;
            RestoreBodyTopology(true);
            RecoveryCount++;
            if (assembly) assembly.Finish();
            if (effects && anchorIndex >= 0 && LimbAt(anchorIndex)) effects.Complete(LimbAt(anchorIndex).transform.position);
            // Only matter which actually reached the growing body is incorporated.
            // Distant crumbs keep crawling afterward instead of vanishing remotely.
            ConsumeMatterNear(collectedAt, .42f);
            ScanRemnants(false);
            lateMatter = HasOwnedRemnants();
            remnantAnchor = null;
            growthStarted = false; groundLift = 0f;
            RestoreOwnBarrages();
        }

        bool HasOwnedRemnants()
        {
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || tissue.Limb.DestroyWith == null) continue;
                foreach (var piece in tissue.Limb.DestroyWith)
                {
                    if (!piece) continue;
                    var physical = piece.GetComponent<PhysicalBehaviour>();
                    if (physical && !physical.isDisintegrated && physical.gameObject.activeInHierarchy) return true;
                }
            }
            return false;
        }

        void ConsumePiece(PhysicalBehaviour physical, int owner)
        {
            if (!physical) return;
            physical.OnDisintegration -= OnRemnantDisintegrated;
            if (!physical.isDisintegrated && physical.gameObject.activeInHierarchy) physical.Disintegrate();
            // Native pseudo-child arrays skip destroyed references. Remove the
            // matching counted disintegration as the actual fragment is removed.
            if (physical.isDisintegrated && disintegrationCounter)
                disintegrationCounter.DisintegrationCount = Mathf.Max(0, disintegrationCounter.DisintegrationCount - 1);
            var limb = LimbAt(owner);
            if (limb && limb.DestroyWith != null) limb.DestroyWith.Remove(physical.gameObject);
            Destroy(physical.gameObject);
        }

        void ConsumeMatterNear(Vector2 point, float radius)
        {
            float squared = radius * radius;
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || tissue.Limb.DestroyWith == null) continue;
                for (int k = tissue.Limb.DestroyWith.Count - 1; k >= 0; k--)
                {
                    var piece = tissue.Limb.DestroyWith[k];
                    if (!piece) { tissue.Limb.DestroyWith.RemoveAt(k); continue; }
                    var physical = piece.GetComponent<PhysicalBehaviour>();
                    if (!physical) continue;
                    if (!physical.isDisintegrated && ((Vector2)physical.transform.position - point).sqrMagnitude > squared) continue;
                    ConsumePiece(physical, i);
                }
            }
        }

        void ReclaimLateMatter(float dt)
        {
            if (Time.time >= nextRemnantScan) ScanRemnants(false);
            var front = LimbAt(6);
            var back = LimbAt(9);
            Vector2 point = front && back ? ((Vector2)front.transform.position + (Vector2)back.transform.position) * .5f
                : (Vector2)person.transform.position;
            point += Vector2.up * .06f;
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                float distance = Vector2.Distance(body.position, point);
                if (distance < .38f) { ConsumePiece(remnant.Physical, remnant.LimbIndex); continue; }
                SteerMatter(body, point, remnant.Seed, dt, 1f);
                BuuFleshAssets.Animate(remnant.Physical, .65f, Mathf.Clamp01(1f - distance / 3f));
            }
            remnants.RemoveAll(r => !LiveRemnant(r));
            if (remnants.Count == 0) lateMatter = HasOwnedRemnants();
        }

        void RestoreColliders(Tissue tissue)
        {
            for (int c = 0; c < tissue.Colliders.Length; c++)
                if (tissue.Colliders[c]) tissue.Colliders[c].enabled = tissue.ColliderEnabled[c] || (tissue.WasDisintegrated && !tissue.Colliders[c].isTrigger);
        }

        void RestoreSelfCollisions()
        {
            // Disable/enable clears Unity's ignore pairs. The native stack already
            // owns each pair, so reapply directly without incrementing its refcounts.
            for (int i = 0; i < tissues.Length; i++)
            {
                var a = tissues[i];
                if (a == null || !a.Limb) continue;
                for (int j = i + 1; j < tissues.Length; j++)
                {
                    var b = tissues[j];
                    if (b == null || !b.Limb) continue;
                    foreach (var colliderA in a.Colliders)
                        if (colliderA && colliderA.enabled && !colliderA.isTrigger)
                            foreach (var colliderB in b.Colliders)
                                if (colliderB && colliderB.enabled && !colliderB.isTrigger)
                                    Physics2D.IgnoreCollision(colliderA, colliderB, true);
                }
            }
        }

        void RefreshColliderCache()
        {
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Physical) continue;
                var current = tissue.Limb.GetComponentsInChildren<Collider2D>(true);
                var enabled = new bool[current.Length];
                var physical = new List<Collider2D>();
                tissue.Limb.Collider = null;
                for (int c = 0; c < current.Length; c++)
                {
                    int previous = Array.IndexOf(tissue.Colliders, current[c]);
                    enabled[c] = previous >= 0 && previous < tissue.ColliderEnabled.Length
                        ? tissue.ColliderEnabled[previous] : current[c].enabled || (!current[c].isTrigger && tissue.Physical.isDisintegrated);
                    if (current[c].gameObject == tissue.Limb.gameObject && !current[c].isTrigger)
                    {
                        physical.Add(current[c]);
                        if (!tissue.Limb.Collider) tissue.Limb.Collider = current[c];
                    }
                }
                tissue.Colliders = current;
                tissue.ColliderEnabled = enabled;
                tissue.Physical.colliders = physical.ToArray();
                BodyBlueprint[i].ColliderEnabled = (bool[])enabled.Clone();
            }
        }

        void AbortWithoutMatter()
        {
            if (aborting) return;
            aborting = true;
            reforming = false;
            stage = BuuRecoveryStage.Idle; stageAge = 0f;
            if (assembly) assembly.Cancel(false);
            if (person) person.enabled = personEnabled;
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Physical || !tissue.Limb) continue;
                RestoreColliders(tissue);
                if (tissue.Body) tissue.Body.simulated = tissue.Simulated;
                if (tissue.Body) tissue.Body.bodyType = BodyBlueprint[i].BodyType;
                if (tissue.Limb.Joint) tissue.Limb.Joint.enabled = true;
                if (growthStarted && !tissue.SourceMatter && !tissue.Physical.isDisintegrated && tissue.Physical != disintegratingSource)
                    tissue.Physical.Disintegrate();
            }
            growthStarted = false; groundLift = 0f;
            RestoreOwnBarrages();
            aborting = false;
        }

        void CancelForErasure()
        {
            reforming = false;
            stage = BuuRecoveryStage.Idle; stageAge = 0f;
            if (assembly) assembly.Cancel(false);
            if (person) person.enabled = personEnabled;
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (tissue == null || !tissue.Limb || !tissue.Body) continue;
                tissue.Body.bodyType = BodyBlueprint[i].BodyType;
                tissue.Body.simulated = tissue.Simulated;
                RestoreColliders(tissue);
                if (tissue.Limb.Joint) tissue.Limb.Joint.enabled = true;
            }
            growthStarted = false; groundLift = 0f;
            RestoreOwnBarrages();
        }

        void RestoreMajinMind()
        {
            if (!person) return;
            person.Braindead = false;
            person.BrainDamaged = false;
            person.BrainDamagedTime = -1f;
            person.SeizureTime = 0f;
            person.Consciousness = 1f;
            person.OxygenLevel = 1f;
            person.PainLevel = 0f;
            person.ShockLevel = 0f;
        }

        void Heal(Tissue tissue, Blueprint blueprint, float dt, bool complete)
        {
            var limb = tissue.Limb;
            var physical = tissue.Physical;
            if (!limb || !physical || physical.isDisintegrated) return;
            float amount = complete ? 1f : 1f - Mathf.Exp(-dt * 5.5f);
            bool wound = limb.Health < limb.InitialHealth * .98f || limb.Broken || physical.BurnProgress > .03f || limb.SkinMaterialHandler.AcidProgress > .03f;
            limb.Health = Mathf.Lerp(Mathf.Max(0f, limb.Health), limb.InitialHealth, amount);
            limb.Numbness = Mathf.Lerp(limb.Numbness, 0f, amount);
            limb.LungsPunctured = false;
            limb.Frozen = false;
            limb.HealBone();
            limb.InternalTemperature = Mathf.Lerp(limb.InternalTemperature, limb.BodyTemperature, amount);
            physical.Temperature = Mathf.Lerp(physical.Temperature, limb.BodyTemperature, amount);
            physical.BurnProgress = Mathf.Lerp(physical.BurnProgress, 0f, amount);
            if (physical.OnFire) physical.Extinguish();
            var circulation = limb.CirculationBehaviour;
            circulation.HealBleeding();
            circulation.IsPump = blueprint.Pump;
            circulation.BloodFlow = 1f;
            float deficit = Mathf.Max(0f, 1f - circulation.GetAmountOfBlood());
            if (deficit > .001f) circulation.AddLiquid(limb.GetOriginalBloodType(), deficit * amount);
            var skin = limb.SkinMaterialHandler;
            skin.ShouldRot = false;
            skin.RottenProgress = Mathf.Lerp(skin.RottenProgress, 0f, amount);
            skin.AcidProgress = Mathf.Lerp(skin.AcidProgress, 0f, amount);
            if (complete)
            {
                skin.ClearAllDamage();
                skin.currentDamagePointCount = 0;
                skin.Sync();
            }
            else if (skin.damagePoints != null)
            {
                bool changed = false;
                bool remaining = false;
                for (int k = 0; k < Mathf.Min(skin.damagePoints.Length, skin.currentDamagePointCount); k++)
                {
                    if (skin.damagePoints[k].z <= 0f) continue;
                    skin.damagePoints[k].z = Mathf.Lerp(skin.damagePoints[k].z, 0f, amount);
                    if (skin.damagePoints[k].z < .001f) skin.damagePoints[k].z = 0f;
                    if (skin.damagePoints[k].z > 0f) remaining = true;
                    changed = true;
                }
                if (!remaining && skin.currentDamagePointCount != 0) { skin.currentDamagePointCount = 0; changed = true; }
                if (changed) skin.Sync();
            }
            if (wound && effects && Time.time - tissue.LastWoundTime > .65f)
            {
                tissue.LastWoundTime = Time.time;
                effects.Wound(limb.transform.position, Mathf.Clamp(limb.transform.lossyScale.magnitude * .18f, .1f, .55f));
            }
        }

        void OnTissueDisintegrated(object sender, EventArgs args)
        {
            if (!RegenerationEnabled) return;
            var physical = sender as PhysicalBehaviour;
            if (!physical) return;
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Physical == physical)
                {
                    if (!aborting && !ErasedMatter[i] && (!reforming || tissues[i].SourceMatter)) ScanRemnants(reforming);
                    if (reforming)
                    {
                        tissues[i].SourceMatter = false;
                        if (growthStarted && !HasActualRecoverySource())
                        {
                            disintegratingSource = physical;
                            AbortWithoutMatter();
                            disintegratingSource = null;
                        }
                    }
                    if (effects && !ErasedMatter[i] && stage != BuuRecoveryStage.Rest) effects.Wound(physical.transform.position, .55f);
                    break;
                }
        }

        bool HasActualRecoverySource()
        {
            for (int i = 0; i < tissues.Length; i++)
                if (IsMatter(i) && tissues[i].SourceMatter) return true;
            foreach (var remnant in remnants) if (LiveRemnant(remnant)) return true;
            return false;
        }

        void OnRemnantDisintegrated(object sender, EventArgs args)
        {
            var physical = sender as PhysicalBehaviour;
            if (!physical) return;
            foreach (var remnant in remnants)
                if (remnant.Physical == physical) remnant.Erased = true;
            if (reforming && growthStarted && !HasActualRecoverySource()) AbortWithoutMatter();
        }

        /// <summary>
        /// Erased matter cannot sustain recovery. Another surviving piece may still
        /// grow a replacement shape; local destruction does not poison the whole Buu.
        /// </summary>
        public static void Erase(LimbBehaviour limb)
        {
            if (!limb || !limb.Person) return;
            var regeneration = limb.Person.GetComponent<BuuRegeneration>();
            if (!regeneration) return;
            regeneration.Initialize();
            if (!regeneration.initialized) return;
            int index = IndexOf(limb.Person.Limbs, limb);
            if (index >= 0) regeneration.ErasedMatter[index] = true;
        }

        public void OnBeforeSerialise()
        {
            MarkRuntimeAudio();
            // A copy made mid-flight must not be saved as permanently nonsimulated.
            if (reforming) FinishReform();
            RestoreOwnBarrages();
            CaptureNativeStats();
        }

        void CaptureNativeStats()
        {
            var owner = GetComponent<PersonBehaviour>();
            if (!owner || owner.Limbs == null) return;
            NativeStats = new NativeStatSnapshot[owner.Limbs.Length];
            for (int i = 0; i < owner.Limbs.Length; i++)
            {
                var limb = owner.Limbs[i];
                if (!limb) continue;
                NativeStats[i] = new NativeStatSnapshot
                {
                    HasLimb = true,
                    Health = limb.Health,
                    InitialHealth = limb.InitialHealth,
                    BreakingThreshold = limb.BreakingThreshold,
                    GForcePassoutThreshold = limb.GForcePassoutThreshold,
                    ImpactDamageMultiplier = limb.ImpactDamageMultiplier
                };
            }
        }

        void RestoreNativeStats()
        {
            var owner = GetComponent<PersonBehaviour>();
            if (!owner || owner.Limbs == null || NativeStats == null || NativeStats.Length != owner.Limbs.Length) return;
            // Native CreateNew injects saved fields before rerunning the spawn
            // recipe. Undo that recipe's second multiplication, using exact saved
            // values rather than inferring health or reviving a damaged snapshot.
            for (int i = 0; i < owner.Limbs.Length; i++)
            {
                var limb = owner.Limbs[i];
                var saved = NativeStats[i];
                if (!limb || !saved.HasLimb) continue;
                limb.Health = saved.Health;
                limb.InitialHealth = saved.InitialHealth;
                limb.BreakingThreshold = saved.BreakingThreshold;
                limb.GForcePassoutThreshold = saved.GForcePassoutThreshold;
                limb.ImpactDamageMultiplier = saved.ImpactDamageMultiplier;
            }
        }

        public void OnAfterDeserialise(List<GameObject> objects)
        {
            RestoreOwnBarrages();
            RestoreNativeStats();
            Unsubscribe();
            initialized = false; reforming = false;
            stage = BuuRecoveryStage.Idle; stageAge = 0f; growthStarted = false; groundLift = 0f;
            remnants = new List<Remnant>();
            remnantAnchor = null;
            Initialize();
        }

        void Unsubscribe()
        {
            if (tissues == null) return;
            foreach (var tissue in tissues)
                if (tissue != null && tissue.Physical) tissue.Physical.OnDisintegration -= OnTissueDisintegrated;
            foreach (var tissue in tissues)
            {
                if (tissue == null || !tissue.Limb || tissue.Limb.DestroyWith == null) continue;
                foreach (var piece in tissue.Limb.DestroyWith)
                    if (piece)
                    {
                        var physical = piece.GetComponent<PhysicalBehaviour>();
                        if (physical) physical.OnDisintegration -= OnRemnantDisintegrated;
                    }
            }
        }

        void OnDestroy() { if (assembly) assembly.Cancel(false); RestoreOwnBarrages(); Unsubscribe(); }
    }
}
