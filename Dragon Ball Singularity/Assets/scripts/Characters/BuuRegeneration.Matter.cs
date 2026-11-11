using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Surviving Majin matter: the original limbs that still exist and the crumbs
    // native Crush registered with them. Matter is always real and attackable;
    // this part only finds it, moves it with forces and merges it on arrival.
    public sealed partial class BuuRegeneration
    {
        sealed class Remnant
        {
            public PhysicalBehaviour Physical;
            public int LimbIndex;
            public bool Scattered;
            public bool Merged;
            public float NextTwitch;
            public float Seed;
            public bool Erased;
        }

        [SkipSerialisation] List<Remnant> remnants = new List<Remnant>();
        [SkipSerialisation] Remnant remnantAnchor;
        [SkipSerialisation] int anchorIndex = -1;
        [SkipSerialisation] float nextRemnantScan;
        [SkipSerialisation] bool lateMatter;
        [SkipSerialisation] readonly Queue<int> clusterQueue = new Queue<int>();
        [SkipSerialisation] bool[] clusterVisited;

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

        bool HasActualRecoverySource()
        {
            for (int i = 0; i < tissues.Length; i++)
                if (IsMatter(i) && tissues[i].SourceMatter) return true;
            foreach (var remnant in remnants) if (LiveRemnant(remnant)) return true;
            return false;
        }

        static bool LiveRemnant(Remnant remnant)
        {
            return remnant != null && !remnant.Erased && remnant.Physical && remnant.Physical.gameObject.activeInHierarchy &&
                !remnant.Physical.isDisintegrated && remnant.Physical.rigidbody;
        }

        static bool DeadRemnant(Remnant remnant) { return !LiveRemnant(remnant); }

        void ScanRemnants(bool originalSourcesOnly)
        {
            nextRemnantScan = Time.time + .2f;
            remnants.RemoveAll(DeadRemnant);
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
                    if (known) continue;
                    remnants.Add(new Remnant { Physical = physical, LimbIndex = i, Seed = Noise(i * 17.1f + k * 3.7f) });
                    physical.OnDisintegration += OnRemnantDisintegrated;
                    BuuFleshAssets.Attach(physical, i * 17 + k);
                    BuuFleshAssets.Tint(physical, palette.Tissue);
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

        bool EnsureRecoverySource()
        {
            if (remnantAnchor != null && LiveRemnant(remnantAnchor)) return true;
            if (remnantAnchor == null && IsMatter(anchorIndex) && tissues[anchorIndex].SourceMatter) return true;
            bool found = SetRecoveryAnchor();
            if (found && growthStarted) PlanGroundedGrowth();
            return found;
        }

        Vector3 PhysicalRecoveryOrigin()
        { return remnantAnchor != null ? remnantAnchor.Physical.transform.position : tissues[anchorIndex].Limb.transform.position; }

        Vector2 MovingAssemblyOrigin()
        {
            var limb = LimbAt(anchorIndex);
            return growthStarted && limb && !limb.PhysicalBehaviour.isDisintegrated
                ? (Vector2)limb.transform.position : (Vector2)PhysicalRecoveryOrigin();
        }

        // The largest connected surviving cluster wins, never a privileged head.
        int FindAnchor(bool originalMatterOnly)
        {
            float bestMass = -1f;
            int best = -1;
            bool[] visited = ClusterScratch();
            for (int i = 0; i < tissues.Length; i++)
            {
                if (visited[i] || !IsMatter(i) || (originalMatterOnly && !tissues[i].SourceMatter)) continue;
                clusterQueue.Clear();
                clusterQueue.Enqueue(i); visited[i] = true;
                float mass = 0f, largest = -1f;
                int candidate = i;
                while (clusterQueue.Count > 0)
                {
                    int index = clusterQueue.Dequeue();
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
                        visited[j] = true; clusterQueue.Enqueue(j);
                    }
                }
                if (mass > bestMass) { bestMass = mass; best = candidate; }
            }
            return best;
        }

        // The anchor's still-connected original limbs need no reconstruction.
        bool[] AnchorCluster()
        {
            var visited = new bool[tissues.Length];
            if (remnantAnchor != null || !IsMatter(anchorIndex)) return visited;
            clusterQueue.Clear();
            clusterQueue.Enqueue(anchorIndex); visited[anchorIndex] = true;
            while (clusterQueue.Count > 0)
            {
                int current = clusterQueue.Dequeue();
                var connections = tissues[current].Limb.NodeBehaviour.Connections;
                if (connections == null) continue;
                foreach (var node in connections)
                {
                    if (!node) continue;
                    int next = IndexOf(person.Limbs, node.GetComponent<LimbBehaviour>());
                    if (next < 0 || visited[next] || !IsMatter(next) || !tissues[next].SourceMatter) continue;
                    visited[next] = true; clusterQueue.Enqueue(next);
                }
            }
            return visited;
        }

        bool[] ClusterScratch()
        {
            if (clusterVisited == null || clusterVisited.Length != tissues.Length) clusterVisited = new bool[tissues.Length];
            else System.Array.Clear(clusterVisited, 0, clusterVisited.Length);
            return clusterVisited;
        }

        // A detached original limb is loose matter; one still hinged to the
        // surviving body only moves once nearly everything else is gone.
        bool IsLooseSource(int index)
        {
            var limb = tissues[index].Limb;
            return limb.IsDismembered || !limb.NodeBehaviour.IsConnectedToRoot || regrowingCount >= tissues.Length - 1;
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

        // Rest is inert: the authored flesh keeps its static pose, but the rubber
        // still reacts to landing impacts as the pieces come to a stop.
        void HoldMatterAtRest()
        {
            foreach (var remnant in remnants)
                if (LiveRemnant(remnant)) BuuFleshAssets.Animate(remnant.Physical, 0f, 0f);
        }

        void TwitchBody(Rigidbody2D body, float seed, ref float nextTwitch)
        {
            float delay = seed * BuuRecoveryTimeline.TwitchStagger;
            if (!body || body.bodyType != RigidbodyType2D.Dynamic || stageAge < delay) return;
            float strength = Mathf.Clamp01((stageAge - delay) / BuuRecoveryTimeline.TwitchRamp);
            if (stageAge < nextTwitch) return;
            float beat = Noise(seed * 31f + nextTwitch * 9.7f);
            nextTwitch = stageAge + .08f + beat * .16f;
            Vector2 toward = (Vector2)PhysicalRecoveryOrigin() - body.position;
            if (toward.sqrMagnitude > .001f) toward.Normalize();
            Vector2 impulse = new Vector2((beat - .5f) * .18f, .10f + beat * .11f) + toward * .08f;
            body.AddForce(impulse * body.mass * strength, ForceMode2D.Impulse);
            body.AddTorque((beat - .5f) * body.mass * .12f * strength, ForceMode2D.Impulse);
        }

        void QuiverMatter()
        {
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                TwitchBody(body, remnant.Seed, ref remnant.NextTwitch);
                float strength = Mathf.Clamp01((stageAge - remnant.Seed * BuuRecoveryTimeline.TwitchStagger) / BuuRecoveryTimeline.TwitchRamp);
                BuuFleshAssets.Animate(remnant.Physical, .12f + strength * .48f, Mathf.Max(0f, Mathf.Sin(stageAge * 23f + remnant.Seed * 31f)) * strength);
                if (effects) effects.Quiver(body.position, Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.001f, body.mass)) * .10f, .045f, .15f), strength);
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (!IsMatter(i) || !tissue.SourceMatter || !IsLooseSource(i)) continue;
                float seed = Noise(i * 7.3f);
                TwitchBody(tissue.Body, seed, ref tissue.NextTwitch);
                if (effects) effects.Quiver(tissue.Body.position, .12f,
                    Mathf.Clamp01((stageAge - seed * BuuRecoveryTimeline.TwitchStagger) / BuuRecoveryTimeline.TwitchRamp));
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
                TrackMerge(remnant, origin);
            }
            DriveAnchorFlesh(progress);
            for (int i = 0; i < tissues.Length; i++)
            {
                var tissue = tissues[i];
                if (!IsMatter(i) || !tissue.SourceMatter || (i == anchorIndex && remnantAnchor == null)) continue;
                if (!tissue.Limb.IsDismembered && tissue.Limb.NodeBehaviour.IsConnectedToRoot) continue;
                SteerMatter(tissue.Body, origin, Noise(i * 7.3f), dt, progress);
            }
        }

        // Each crumb sinks into the growing body once, with a wet closing ring and
        // a swallow of the anchor matter. Knocked away again, it can merge again.
        void TrackMerge(Remnant remnant, Vector2 origin)
        {
            float distance = Vector2.Distance(remnant.Physical.rigidbody.position, origin);
            if (remnant.Merged)
            {
                if (distance > BuuRecoveryTimeline.MergeRelease) remnant.Merged = false;
                return;
            }
            if (distance > BuuRecoveryTimeline.MergeRadius) return;
            remnant.Merged = true;
            float size = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.001f, remnant.Physical.rigidbody.mass)) * .12f, .05f, .22f);
            if (effects) effects.Merge(remnant.Physical.rigidbody.position, size);
            if (remnantAnchor != null && LiveRemnant(remnantAnchor)) BuuFleshAssets.Pulse(remnantAnchor.Physical, size * 2.2f);
        }

        // The anchor crumb is the body's seed: it breathes harder as more matter arrives.
        void DriveAnchorFlesh(float progress)
        {
            if (remnantAnchor == null || !LiveRemnant(remnantAnchor)) return;
            float breath = .5f + .5f * Mathf.Sin(stageAge * (5.5f + progress * 4f));
            BuuFleshAssets.Animate(remnantAnchor.Physical, .75f + progress * .25f, breath * (.35f + progress * .55f));
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

        float GatheredMassFraction()
        {
            float all = 0f, gathered = 0f;
            Vector2 centre = PhysicalRecoveryOrigin();
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                float mass = Mathf.Max(.001f, remnant.Physical.rigidbody.mass);
                all += mass;
                if (((Vector2)remnant.Physical.transform.position - centre).sqrMagnitude < BuuRecoveryTimeline.GatherRadiusSquared) gathered += mass;
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                if (!IsMatter(i) || !tissues[i].SourceMatter) continue;
                float mass = Mathf.Max(.001f, tissues[i].Body.mass);
                all += mass;
                if ((tissues[i].Body.position - centre).sqrMagnitude < BuuRecoveryTimeline.GatherRadiusSquared) gathered += mass;
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
                if (IsStill(body)) quiet += mass;
            }
            for (int i = 0; i < tissues.Length; i++)
            {
                if (!IsMatter(i) || !tissues[i].SourceMatter || !IsLooseSource(i)) continue;
                var body = tissues[i].Body;
                float mass = Mathf.Max(.001f, body.mass);
                all += mass;
                if (IsStill(body)) quiet += mass;
            }
            return all > 0f ? quiet / all : 1f;
        }

        static bool IsStill(Rigidbody2D body)
        {
            return body.velocity.sqrMagnitude < BuuRecoveryTimeline.StillSpeedSquared
                && Mathf.Abs(body.angularVelocity) < BuuRecoveryTimeline.StillSpin;
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

        // Crumbs that missed the reform keep crawling home and are swallowed at the waist.
        void ReclaimLateMatter(float dt)
        {
            if (Time.time >= nextRemnantScan) ScanRemnants(false);
            var back = LimbAt(BackFootIndex);
            var front = LimbAt(FrontFootIndex);
            Vector2 point = front && back ? ((Vector2)front.transform.position + (Vector2)back.transform.position) * .5f
                : (Vector2)person.transform.position;
            point += Vector2.up * .06f;
            foreach (var remnant in remnants)
            {
                if (!LiveRemnant(remnant)) continue;
                var body = remnant.Physical.rigidbody;
                float distance = Vector2.Distance(body.position, point);
                if (distance < BuuRecoveryTimeline.LateMergeRadius)
                {
                    if (effects) effects.Merge(body.position, Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.001f, body.mass)) * .12f, .05f, .22f));
                    ConsumePiece(remnant.Physical, remnant.LimbIndex);
                    continue;
                }
                SteerMatter(body, point, remnant.Seed, dt, 1f);
                BuuFleshAssets.Animate(remnant.Physical, .65f, Mathf.Clamp01(1f - distance / 3f));
            }
            remnants.RemoveAll(DeadRemnant);
            if (remnants.Count == 0) lateMatter = HasOwnedRemnants();
        }
    }
}
