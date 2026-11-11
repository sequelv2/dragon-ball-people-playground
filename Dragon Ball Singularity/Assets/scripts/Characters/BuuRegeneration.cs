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
    /// <remarks>
    /// One component, split by responsibility:
    /// <list type="bullet">
    /// <item>BuuRegeneration.cs: lifecycle, public API, save/copy and native events.</item>
    /// <item>BuuRegeneration.Body.cs: blueprint capture, native topology rebuild, healing.</item>
    /// <item>BuuRegeneration.Matter.cs: surviving tissue and crumbs, the anchor, steering and merging.</item>
    /// <item>BuuRegeneration.Recovery.cs: the Rest, Quiver, Gather and Grow stage machine.</item>
    /// </list>
    /// Timings live in <see cref="BuuRecoveryTimeline"/>, colours in <see cref="BuuPalette"/>.
    /// Presentation (<see cref="BuuRegenerationFx"/>, <see cref="BuuAssemblyView"/>, flesh views)
    /// only follows this core; it never owns matter, joints or damage.
    /// </remarks>
    public sealed partial class BuuRegeneration : MonoBehaviour, Messages.IOnBeforeSerialise, Messages.IOnAfterDeserialise
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

        // Native human limb slots this component refers to by role.
        const int HeadIndex = 0, BackFootIndex = 6, FrontFootIndex = 9;

        [SkipSerialisation] PersonBehaviour person;
        [SkipSerialisation] BuuRegenerationFx effects;
        [SkipSerialisation] BuuAssemblyView assembly;
        [SkipSerialisation] BuuPalette palette;
        [SkipSerialisation] DisintegrationCounterBehaviour disintegrationCounter;
        [SkipSerialisation] bool initialized;

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
            palette = BuuPalette.For(person);
            CaptureBody(person.Limbs);
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
            RepairStaleColliders();
            if (person.GetComponent<HakaiDissolveMarker>())
            {
                if (reforming) CancelForErasure();
                return;
            }
            if (TimeStopped()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            AfterShock(dt);
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
            if (damageAge >= BuuRecoveryTimeline.DamageConfirmation) BeginReform();
        }

        bool TimeStopped()
        {
            return person.GetComponent<TimePrison>() || RealityRift.Active ||
                (SingularityWorldFreeze.Active && !SingularityWorldFreeze.Current.IsExempt(transform));
        }

        public void Reassemble()
        {
            Initialize();
            if (initialized && RegenerationEnabled && !reforming) BeginReform();
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

        static float Noise(float seed)
        { return Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f); }

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

        void OnRemnantDisintegrated(object sender, EventArgs args)
        {
            var physical = sender as PhysicalBehaviour;
            if (!physical) return;
            foreach (var remnant in remnants)
                if (remnant.Physical == physical) remnant.Erased = true;
            if (reforming && growthStarted && !HasActualRecoverySource()) AbortWithoutMatter();
        }

        public void OnBeforeSerialise()
        {
            MarkRuntimeAudio();
            // A copy made mid-flight must not be saved as permanently nonsimulated.
            if (reforming) FinishReform();
            EndAfterShock();
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
            afterShockAge = -1f;
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
