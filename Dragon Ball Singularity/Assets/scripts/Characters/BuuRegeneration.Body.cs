using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The body itself: what the healthy Majin looked like (its blueprint), how to
    // rebuild the native joint, node and circulation graph from it, and healing.
    public sealed partial class BuuRegeneration
    {
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

        [SkipSerialisation] Tissue[] tissues;

        void CaptureBody(LimbBehaviour[] limbs)
        {
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
                if (capture)
                {
                    BodyBlueprint[i] = Capture(limb, limbs);
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

        void RepairStaleColliders()
        {
            // Skin replacement destroys prefab colliders after this component may
            // already have captured the body. Refresh native cached pointers once.
            for (int i = 0; i < tissues.Length; i++)
                if (tissues[i] != null && tissues[i].Limb && !tissues[i].Limb.Collider)
                {
                    RefreshColliderCache();
                    RestoreSelfCollisions();
                    BuuMatter.ReapplyInterior(person);
                    return;
                }
        }

        void RestoreColliders(Tissue tissue)
        {
            for (int c = 0; c < tissue.Colliders.Length; c++)
                if (tissue.Colliders[c]) tissue.Colliders[c].enabled = tissue.ColliderEnabled[c] || (tissue.WasDisintegrated && !tissue.Colliders[c].isTrigger);
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
    }
}
