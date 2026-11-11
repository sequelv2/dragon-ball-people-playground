using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The recovery stage machine. Rest: matter flies and lies still. Quiver: it
    // twitches. Gather: it crawls to the anchor. Grow: opaque pieces rebuild every
    // missing native part on live joints. Then the body closes and shivers once.
    public sealed partial class BuuRegeneration
    {
        [SkipSerialisation] bool reforming;
        [SkipSerialisation] BuuRecoveryStage stage;
        [SkipSerialisation] float stageAge;
        [SkipSerialisation] float damageAge;
        [SkipSerialisation] float quietAge;
        [SkipSerialisation] float reformAge;
        [SkipSerialisation] float reformDuration;
        [SkipSerialisation] bool growthStarted;
        [SkipSerialisation] bool personEnabled;
        [SkipSerialisation] bool[] rebuildingLimbs;
        [SkipSerialisation] int regrowingCount;
        [SkipSerialisation] bool uprightRecovery;
        [SkipSerialisation] float remnantAngle;
        [SkipSerialisation] float groundLift;
        [SkipSerialisation] PhysicalBehaviour disintegratingSource;
        [SkipSerialisation] bool aborting;
        [SkipSerialisation] Barrage[] suspendedBarrages;
        [SkipSerialisation] bool[] barrageEnabled;
        [SkipSerialisation] float afterShockAge = -1f;
        [SkipSerialisation] float[] afterShockSpin;
        [SkipSerialisation] readonly RaycastHit2D[] groundHits = new RaycastHit2D[32];
        [SkipSerialisation] readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];

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

        void BeginReform()
        {
            if (person.GetComponent<HakaiDissolveMarker>()) return;
            EndAfterShock();
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
            foreach (var remnant in remnants) remnant.Merged = false;
            uprightRecovery = nativeSurvivors <= 1;
            if (uprightRecovery) remnantAngle = transform.eulerAngles.z;
            reforming = true; reformAge = 0f; damageAge = 0f;
            EnterStage(BuuRecoveryStage.Rest); quietAge = 0f;
            growthStarted = false; groundLift = 0f;
            personEnabled = person.enabled;
            person.enabled = false;
            ScatterRemnants();
        }

        void EnterStage(BuuRecoveryStage next)
        {
            stage = next; stageAge = 0f;
        }

        void AdvanceReform(float dt)
        {
            // Only matter present before this recovery may sustain it. Freshly
            // growing shapes cannot rescue an erased final survivor.
            if (!EnsureRecoverySource()) { AbortWithoutMatter(); return; }
            stageAge += dt;
            switch (stage)
            {
                case BuuRecoveryStage.Rest: TickRest(dt); break;
                case BuuRecoveryStage.Quiver: TickQuiver(); break;
                case BuuRecoveryStage.Gather: TickGather(dt); break;
                default: TickGrow(dt); break;
            }
        }

        void TickRest(float dt)
        {
            ScatterRemnants();
            HoldMatterAtRest();
            // Ballistic flight comes first. Most surviving tissue then gets
            // about a full second of stillness before the first living twitch.
            quietAge = QuietMatterFraction() >= BuuRecoveryTimeline.RestStillMassFraction
                ? quietAge + dt : Mathf.Max(0f, quietAge - dt * 2f);
            if (quietAge < BuuRecoveryTimeline.RestStillness && stageAge < BuuRecoveryTimeline.RestLongest) return;
            EnterStage(BuuRecoveryStage.Quiver);
            if (effects) effects.Begin(PhysicalRecoveryOrigin());
        }

        void TickQuiver()
        {
            QuiverMatter();
            if (stageAge >= BuuRecoveryTimeline.QuiverDuration) EnterStage(BuuRecoveryStage.Gather);
        }

        void TickGather(float dt)
        {
            GatherMatter(dt, Mathf.Clamp01(stageAge / BuuRecoveryTimeline.GatherRamp));
            if ((stageAge >= BuuRecoveryTimeline.GatherShortest && GatheredMassFraction() >= BuuRecoveryTimeline.GatherMassFraction)
                || stageAge >= BuuRecoveryTimeline.GatherLongest) BeginGrowth();
        }

        void TickGrow(float dt)
        {
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
            Vector2 destination = MovingAssemblyOrigin();
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                SteerMatter(remnant.Physical.rigidbody, destination, remnant.Seed, dt, 1f);
                BuuFleshAssets.Animate(remnant.Physical, .65f + Mathf.Sin(progress * 12f + remnant.Seed * 19f) * .2f, progress);
                if (remnant != remnantAnchor) TrackMerge(remnant, destination);
            }
            RestoreMajinMind();
            if (progress >= 1f) FinishReform();
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
            EnterStage(BuuRecoveryStage.Grow); reformAge = 0f;
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
            reformDuration = BuuRecoveryTimeline.GrowDuration(distance);
        }

        Vector3 RecoveryOrigin()
        {
            return PhysicalRecoveryOrigin() + Vector3.up * (growthStarted ? groundLift : 0f);
        }

        float RecoveryAngle()
        { return remnantAnchor != null || uprightRecovery ? remnantAngle : tissues[anchorIndex].Limb.transform.eulerAngles.z - BodyBlueprint[anchorIndex].Angle; }

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

        // A body regrown from a lone crumb stands on the ground the crumb lies on.
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
            EnterStage(BuuRecoveryStage.Idle);
            if (person) person.enabled = personEnabled;
            RestoreBodyTopology(true);
            RecoveryCount++;
            if (assembly) assembly.Finish();
            AnnounceClosedBody();
            // Only matter which actually reached the growing body is incorporated.
            // Distant crumbs keep crawling afterward instead of vanishing remotely.
            ConsumeMatterNear(collectedAt, BuuRecoveryTimeline.MergeRadius);
            ScanRemnants(false);
            lateMatter = HasOwnedRemnants();
            remnantAnchor = null;
            growthStarted = false; groundLift = 0f;
            RestoreOwnBarrages();
            BeginAfterShock();
        }

        // The finished body exhales: a fusion pulse at the centre of the body (not
        // wherever the last crumb happened to lie) and steam from the head holes.
        void AnnounceClosedBody()
        {
            if (!effects) return;
            effects.Complete(DbpuFx.BodyBounds(person).center);
            var head = LimbAt(HeadIndex);
            if (!head || !IsMatter(HeadIndex)) return;
            var sprite = head.PhysicalBehaviour.spriteRenderer;
            float height = sprite && sprite.sprite ? sprite.sprite.bounds.extents.y * Mathf.Abs(head.transform.lossyScale.y) : .12f;
            float width = sprite && sprite.sprite ? sprite.sprite.bounds.extents.x * Mathf.Abs(head.transform.lossyScale.x) : .1f;
            Vector2 up = head.transform.up;
            effects.Vent((Vector2)head.transform.position + up * height * .62f, up, width * .8f);
        }

        void BeginAfterShock()
        {
            if (afterShockSpin == null || afterShockSpin.Length != tissues.Length) afterShockSpin = new float[tissues.Length];
            else System.Array.Clear(afterShockSpin, 0, afterShockSpin.Length);
            afterShockAge = 0f;
        }

        void EndAfterShock() { afterShockAge = -1f; }

        // A decaying rubber ripple through the closed body. Each part spins against
        // its parent with equal and opposite angular momentum, like a muscle: the
        // body wobbles in place and never gains momentum, drift or a push.
        void AfterShock(float dt)
        {
            if (afterShockAge < 0f || afterShockSpin == null) return;
            afterShockAge += dt;
            bool done = afterShockAge >= BuuRecoveryTimeline.AfterShockDuration;
            float decay = Mathf.Exp(-afterShockAge * 6f);
            for (int i = 0; i < tissues.Length && i < afterShockSpin.Length; i++)
            {
                var tissue = tissues[i];
                var joint = tissue != null && tissue.Limb ? tissue.Limb.Joint : null;
                var parent = joint && joint.enabled ? joint.connectedBody : null;
                if (!IsMatter(i) || !parent || tissue.Body.bodyType != RigidbodyType2D.Dynamic || parent.bodyType != RigidbodyType2D.Dynamic)
                { afterShockSpin[i] = 0f; continue; }
                float spin = done ? 0f : Mathf.Sin(afterShockAge * 29f - i * .85f) * decay * ShiverAmplitude(tissue.Limb);
                float delta = spin - afterShockSpin[i];
                afterShockSpin[i] = spin;
                tissue.Body.angularVelocity += delta;
                parent.angularVelocity -= delta * tissue.Body.inertia / Mathf.Max(1e-6f, parent.inertia);
            }
            if (done) afterShockAge = -1f;
        }

        // Degrees per second. Arms and head wobble visibly; legs barely, so a
        // standing body never loses its footing to the shiver.
        static float ShiverAmplitude(LimbBehaviour limb)
        {
            switch (limb.RoughClassification)
            {
                case LimbBehaviour.BodyPart.Arms: return 240f;
                case LimbBehaviour.BodyPart.Head: return 150f;
                case LimbBehaviour.BodyPart.Legs: return 30f;
                default: return 60f;
            }
        }

        void StopRecovery()
        {
            if (growthStarted) { FinishReform(); return; }
            reforming = false; EnterStage(BuuRecoveryStage.Idle);
            if (person) person.enabled = personEnabled;
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Limb && tissues[i].Limb.Joint)
                    tissues[i].Limb.Joint.useMotor = BodyBlueprint[i].UseMotor;
            RestoreOwnBarrages();
        }

        void AbortWithoutMatter()
        {
            if (aborting) return;
            aborting = true;
            reforming = false;
            EnterStage(BuuRecoveryStage.Idle);
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
            EnterStage(BuuRecoveryStage.Idle);
            EndAfterShock();
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
    }
}
