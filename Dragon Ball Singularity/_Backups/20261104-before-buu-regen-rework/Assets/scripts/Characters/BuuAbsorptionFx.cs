using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Blender's pressure shapes, material maps and animation curves drive this
    // disposable view. The original native people remain the gameplay actors.
    [SkipSerialisation]
    [DefaultExecutionOrder(500)]
    public sealed class BuuAbsorptionFx : MonoBehaviour
    {
        public const float Duration = 7.2f;
        [Serializable] public sealed class Motion
        {
            public int version;
            public Vector3[] vertices, pressureA, pressureB, pressureHead;
            public Vector2[] uv;
            public int[] triangles;
            public Frame[] choreography;
        }
        [Serializable] public sealed class Frame { public float grip, wrap, struggle, pull, merge; }
        sealed class Pose
        {
            public Rigidbody2D Body;
            public string Name;
            public Vector2 Position, Pivot, NewPosition;
            public float Rotation, NewRotation;
            public Vector3 Scale;
            public int Parent = -1;
            public bool Applied;
        }
        sealed class Coat
        {
            public SpriteRenderer Source;
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Size;
            public Vector3 Centre;
            public float Delay;
            public int Index;
            public bool Owner;
        }
        sealed class Cord
        {
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Vector3[] Vertices, Normals;
            public Transform Destination;
            public int Seed;
        }
        static bool attempted;
        static AssetBundle bundle;
        static Material tissue;
        static Mesh authoredMesh;
        static Motion motion;
        public static bool AssetsReady { get { Load(); return tissue && authoredMesh && motion != null; } }
        static void Load()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                string path = Path.Combine(Singularity.ModDirectory, "Assets/VFX/MajinAbsorption");
                bundle = AssetBundle.LoadFromFile(Path.Combine(path, "majinabsorption"));
                if (!bundle) throw new IOException("Missing authored absorption bundle");
                tissue = bundle.LoadAsset<Material>("assets/majinabsorption/majinabsorption.mat");
                authoredMesh = bundle.LoadAsset<Mesh>("assets/majinabsorption/shroud.asset");
                motion = JsonUtility.FromJson<Motion>(File.ReadAllText(Path.Combine(path, "shroud-motion.json")));
                if (!tissue || !tissue.shader || !tissue.shader.isSupported || !authoredMesh || motion.version != 2
                    || motion.choreography.Length != 217 || motion.pressureA.Length != motion.vertices.Length)
                    throw new IOException("Invalid Blender absorption material/animation");
            }
            catch (Exception error)
            { tissue = null; authoredMesh = null; motion = null; Debug.LogError("BuuAbsorptionFx: " + error); }
        }

        PersonBehaviour owner, victim;
        readonly List<Pose> poses = new List<Pose>();
        readonly List<Coat> coats = new List<Coat>();
        readonly List<Cord> cords = new List<Cord>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Mesh cocoon;
        MeshRenderer cocoonRenderer;
        Transform cocoonTransform;
        Vector3[] deformed;
        Vector2 centre, size;
        Vector2 movingCentre;
        float age, grey;
        Frame current = new Frame();
        public float Struggle { get { return current.struggle; } }
        public float Wrap { get { return current.wrap; } }
        public float Pull { get { return current.pull; } }
        public int TissueViews { get { return coats.Count + cords.Count + 1; } }
        public bool CompletelyWrapped { get { return age >= 4.65f; } }

        public static BuuAbsorptionFx Create(PersonBehaviour source, PersonBehaviour target, Vector2 origin, Vector2 dimensions)
        {
            if (!AssetsReady) return null;
            var root = new GameObject("Blender Majin engulfing flesh");
            root.transform.SetParent(source.transform, true);
            root.AddComponent<Optout>();
            var fx = root.AddComponent<BuuAbsorptionFx>();
            fx.owner = source; fx.victim = target; fx.centre = origin; fx.size = dimensions;
            fx.grey = source.name.IndexOf("Evil Buu", StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : 0f;
            fx.CapturePose(); fx.BuildViews(); fx.Tick(0f);
            return fx;
        }

        void CapturePose()
        {
            foreach (var body in victim.GetComponentsInChildren<Rigidbody2D>())
            {
                var limb = body.GetComponent<LimbBehaviour>();
                if (!limb) continue;
                poses.Add(new Pose { Body = body, Name = limb.name, Position = body.position,
                    Rotation = body.rotation, Scale = body.transform.localScale });
            }
            for (int i = 0; i < poses.Count; i++)
            {
                var pose = poses[i]; var limb = pose.Body.GetComponent<LimbBehaviour>();
                if (!limb.Joint || !limb.Joint.connectedBody) continue;
                pose.Parent = poses.FindIndex(other => other.Body == limb.Joint.connectedBody);
                pose.Pivot = pose.Body.transform.TransformPoint(limb.Joint.anchor);
            }
        }
        MeshRenderer View(string name, Mesh mesh, int sortingLayer, int order)
        {
            var root = new GameObject(name); root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = root.AddComponent<MeshRenderer>(); view.sharedMaterial = tissue;
            view.sortingLayerID = sortingLayer; view.sortingOrder = order;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            return view;
        }
        void BuildViews()
        {
            AddCoats(victim, false); AddCoats(owner, true);
            var reference = victim.Limbs[0].GetComponent<SpriteRenderer>();
            cocoon = Instantiate(authoredMesh); cocoon.name = "Majin pressure shroud";
            cocoon.MarkDynamic(); deformed = new Vector3[motion.vertices.Length];
            cocoonRenderer = View("Complete living cocoon", cocoon, reference.sortingLayerID, reference.sortingOrder + 28);
            cocoonTransform = cocoonRenderer.transform;
            string[] destinations = { "UpperBody", "LowerArmFront", "LowerArm", "LowerLegFront", "LowerLeg", "Head", "MiddleBody" };
            for (int i = 0; i < destinations.Length; i++)
            {
                Transform destination = null;
                foreach (var limb in victim.Limbs) if (limb && limb.name == destinations[i]) { destination = limb.transform; break; }
                if (!destination) continue;
                const int rings = 32, sides = 8;
                var cord = new Cord { Mesh = new Mesh { name = "Majin prehensile flesh cord" },
                    Destination = destination, Seed = i, Vertices = new Vector3[(rings + 1) * (sides + 1)],
                    Normals = new Vector3[(rings + 1) * (sides + 1)] };
                var uv = new Vector2[cord.Vertices.Length]; var triangles = new int[rings * sides * 6];
                for (int j = 0; j <= rings; j++) for (int k = 0; k <= sides; k++)
                    uv[j * (sides + 1) + k] = new Vector2((float)k / sides, (float)j / rings * 3f);
                int at = 0;
                for (int j = 0; j < rings; j++) for (int k = 0; k < sides; k++)
                {
                    int a = j * (sides + 1) + k, b = a + sides + 1;
                    triangles[at++] = a; triangles[at++] = b; triangles[at++] = a + 1;
                    triangles[at++] = a + 1; triangles[at++] = b; triangles[at++] = b + 1;
                }
                cord.Mesh.vertices = cord.Vertices; cord.Mesh.uv = uv; cord.Mesh.triangles = triangles; cord.Mesh.MarkDynamic();
                cord.Renderer = View("Living flesh tether " + i, cord.Mesh, reference.sortingLayerID, reference.sortingOrder + 24);
                cords.Add(cord);
            }
        }
        void AddCoats(PersonBehaviour person, bool own)
        {
            int index = 0;
            foreach (var limb in person.Limbs)
            {
                if (!limb) continue;
                var source = limb.GetComponent<SpriteRenderer>();
                if (!source || !source.sprite) continue;
                Vector3 scale = source.transform.lossyScale;
                Vector3 dimensions = Vector3.Scale(source.sprite.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f));
                dimensions.z = Mathf.Max(.07f, dimensions.x * .65f);
                var view = View((own ? "Merging into Buu " : "Clinging flesh ") + limb.name,
                    authoredMesh, source.sortingLayerID, source.sortingOrder + (own ? 14 : 17));
                float delay = limb.name == "Head" ? .40f : limb.name.Contains("Arm") ? .05f : limb.name.Contains("Leg") ? .18f : 0f;
                coats.Add(new Coat { Source = source, Transform = view.transform, Renderer = view,
                    Size = dimensions, Centre = source.sprite.bounds.center, Delay = delay, Index = index++, Owner = own });
            }
        }

        static float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
        public void Tick(float time)
        {
            age = Mathf.Clamp(time, 0f, Duration);
            float sample = age * 30f;
            int a = Mathf.Clamp(Mathf.FloorToInt(sample), 0, motion.choreography.Length - 1);
            int b = Math.Min(a + 1, motion.choreography.Length - 1); float blend = sample - a;
            Frame from = motion.choreography[a], to = motion.choreography[b];
            current.grip = Mathf.Lerp(from.grip, to.grip, blend); current.wrap = Mathf.Lerp(from.wrap, to.wrap, blend);
            current.struggle = Mathf.Lerp(from.struggle, to.struggle, blend); current.pull = Mathf.Lerp(from.pull, to.pull, blend);
            current.merge = Mathf.Lerp(from.merge, to.merge, blend);
            AnimatePose(); AnimateCoats(); AnimateCocoon(); AnimateCords();
        }
        void LateUpdate()
        {
            // Native pose controllers may update after the absorption coordinator.
            // Reapply the same sampled pose; this does not advance the pause clock.
            if (owner && victim) { AnimatePose(); AnimateCoats(); }
        }
        Vector2 OwnerCentre()
        {
            foreach (var limb in owner.Limbs) if (limb && limb.name == "MiddleBody") return limb.transform.position;
            return owner.transform.position;
        }
        Vector2 RootOffset()
        {
            float resistance = current.struggle;
            return new Vector2(Mathf.Sin(age * 8f) * .035f, Mathf.Sin(age * 12.3f) * .026f) * resistance * size.y;
        }
        float StruggleAngle(Pose pose)
        {
            float side = pose.Name.Contains("Front") ? 1f : -1f;
            float jerk = Mathf.Sin(age * 8.2f + side) + .37f * Mathf.Sin(age * 19f + side * 3f);
            float factor = pose.Name.Contains("LowerArm") ? 38f : pose.Name.Contains("UpperArm") ? -22f
                : pose.Name.Contains("Head") ? -12f : pose.Name.Contains("Leg") ? 12f : 4.5f;
            return jerk * factor * side * current.struggle;
        }
        static Vector2 Rotate(Vector2 value, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad; float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector2(value.x * c - value.y * s, value.x * s + value.y * c);
        }
        void SolvePose(int index, int depth)
        {
            var pose = poses[index]; if (pose.Applied || depth > poses.Count) return;
            pose.Applied = true;
            float angle = StruggleAngle(pose);
            if (pose.Parent >= 0 && pose.Parent != index)
            {
                SolvePose(pose.Parent, depth + 1); var parent = poses[pose.Parent];
                float parentTurn = parent.NewRotation - parent.Rotation;
                pose.NewRotation = pose.Rotation + parentTurn + angle;
                pose.NewPosition = parent.NewPosition + Rotate(pose.Pivot - parent.Position, parentTurn)
                    - Rotate(pose.Pivot - pose.Position, pose.NewRotation - pose.Rotation);
            }
            else { pose.NewRotation = pose.Rotation + angle; pose.NewPosition = pose.Position + RootOffset(); }
        }
        void AnimatePose()
        {
            if (!victim || !owner) return;
            foreach (var pose in poses) pose.Applied = false;
            movingCentre = Vector2.Lerp(centre + RootOffset(), OwnerCentre(), current.pull);
            float shrink = Mathf.Lerp(1f, .035f, current.pull);
            for (int i = 0; i < poses.Count; i++)
            {
                SolvePose(i, 0); var pose = poses[i]; if (!pose.Body) continue;
                Vector2 location = movingCentre + (pose.NewPosition - centre - RootOffset()) * shrink;
                pose.Body.transform.position = new Vector3(location.x, location.y, pose.Body.transform.position.z);
                pose.Body.transform.rotation = Quaternion.Euler(0f, 0f, pose.NewRotation + current.pull * 18f);
                pose.Body.transform.localScale = pose.Scale * shrink;
            }
        }
        void Apply(MeshRenderer view, float coverage, float opacity, float tension, float seed)
        {
            block.Clear(); block.SetFloat("_Clock", age); block.SetFloat("_Coverage", coverage);
            block.SetFloat("_Opacity", opacity); block.SetFloat("_Tension", tension); block.SetFloat("_Seed", seed);
            block.SetFloat("_Grey", grey); view.SetPropertyBlock(block);
            view.enabled = opacity > .005f && coverage > .001f;
        }
        void AnimateCoats()
        {
            foreach (var coat in coats)
            {
                if (!coat.Source) { coat.Renderer.enabled = false; continue; }
                coat.Transform.position = coat.Source.transform.TransformPoint(coat.Centre) + new Vector3(0, 0, -.025f);
                coat.Transform.rotation = coat.Source.transform.rotation;
                float amount = coat.Owner ? current.merge : current.wrap;
                float coverage = coat.Owner ? current.merge * 1.5f - coat.Delay * .25f
                    : current.wrap * 1.65f - coat.Delay;
                float shrink = coat.Owner ? 1f : Mathf.Lerp(1f, .035f, current.pull);
                float pulse = 1.02f + .09f * amount + .025f * Mathf.Sin(age * 10f + coat.Index);
                coat.Transform.localScale = coat.Size * (pulse * shrink);
                float opacity = coat.Owner ? Ease((age - 5.25f) / .7f) * (1f - Ease((age - 6.7f) / .5f)) * .92f
                    : Mathf.Lerp(.64f, 1f, current.wrap);
                Apply(coat.Renderer, coverage, opacity, current.struggle, coat.Index * 1.31f);
            }
        }
        void AnimateCocoon()
        {
            float pressure = current.struggle;
            float left = pressure * Mathf.Pow(.5f + .5f * Mathf.Sin(age * 6.4f), 3f);
            float right = pressure * Mathf.Pow(.5f + .5f * Mathf.Sin(age * 7.2f + 2f), 3f);
            float head = pressure * Mathf.Pow(.5f + .5f * Mathf.Sin(age * 8f + 4f), 3f);
            for (int i = 0; i < deformed.Length; i++)
                deformed[i] = motion.vertices[i] + motion.pressureA[i] * left + motion.pressureB[i] * right + motion.pressureHead[i] * head;
            cocoon.vertices = deformed; cocoon.RecalculateNormals(); cocoon.RecalculateBounds();
            cocoonTransform.position = new Vector3(movingCentre.x, movingCentre.y, -.16f);
            float shrink = Mathf.Lerp(1f, .025f, current.pull);
            float breathe = 1f + .026f * Mathf.Sin(age * 7f);
            cocoonTransform.localScale = new Vector3(Mathf.Max(size.x * 1.6f, size.y * .36f) * shrink * breathe,
                size.y * 1.15f * shrink, size.y * .24f * shrink);
            cocoonTransform.rotation = Quaternion.Euler(0f, 0f, current.pull * 18f);
            float coverage = Ease((current.wrap - .45f) / .55f) * 1.25f;
            Apply(cocoonRenderer, coverage, Ease((age - 2.6f) / 1.1f), pressure, 2.31f);
        }
        void AnimateCords()
        {
            foreach (var cord in cords)
            {
                if (!cord.Destination) { cord.Renderer.enabled = false; continue; }
                Vector3 start = OwnerCentre(); start.z = -.12f;
                Vector3 end = cord.Destination.position; end.z = -.13f;
                float reach = Ease((age - cord.Seed * .075f) / .85f);
                end = Vector3.Lerp(start, end, reach);
                Vector3 delta = end - start;
                Vector3 side = delta.sqrMagnitude > .0001f ? new Vector3(-delta.y, delta.x, 0f).normalized : Vector3.up;
                float fade = 1f - Ease((age - 6.65f) / .45f);
                const int rings = 32, sides = 8;
                for (int j = 0; j <= rings; j++)
                {
                    float t = (float)j / rings;
                    float arch = Mathf.Sin(t * Mathf.PI);
                    Vector3 position = Vector3.Lerp(start, end, t) + side * arch
                        * (Mathf.Sin(age * 3.5f + t * 6f + cord.Seed) * .14f + (cord.Seed - 3) * .055f) * size.y;
                    position.y -= arch * .16f * size.y * (1f - current.pull);
                    float radius = Mathf.Max(.018f, size.y * .027f) * (1f + .17f * Mathf.Sin(t * 42f - age * 10f + cord.Seed))
                        * Mathf.Lerp(1.3f, .44f, t) * reach * fade;
                    for (int k = 0; k <= sides; k++)
                    {
                        float angle = (float)k / sides * Mathf.PI * 2f;
                        Vector3 normal = side * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle);
                        int index = j * (sides + 1) + k;
                        cord.Vertices[index] = cord.Renderer.transform.InverseTransformPoint(position + normal * radius);
                        cord.Normals[index] = normal;
                    }
                }
                cord.Mesh.vertices = cord.Vertices; cord.Mesh.normals = cord.Normals; cord.Mesh.RecalculateBounds();
                Apply(cord.Renderer, 1.2f, reach * fade, current.struggle, cord.Seed * .93f);
            }
        }
        public void Dispose() { Destroy(gameObject); }
        void OnDestroy()
        {
            if (cocoon) Destroy(cocoon);
            foreach (var cord in cords) if (cord.Mesh) Destroy(cord.Mesh);
        }
    }
}
