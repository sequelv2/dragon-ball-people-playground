using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Blender-authored living flesh, rendered over the native physical fragment.
    // This never creates a replacement collider, rigidbody, survivor or character.
    public static class BuuFleshAssets
    {
        static AssetBundle bundle;
        static Material material;
        static Mesh quad;
        static bool attempted;
        static readonly Dictionary<int, BuuFleshView> views = new Dictionary<int, BuuFleshView>();
        public static bool Ready { get { Load(); return material && quad; } }
        internal static Material Material { get { Load(); return material; } }
        internal static Mesh Quad { get { Load(); return quad; } }

        static void Load()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                string root = Singularity.ModDirectory;
                if (string.IsNullOrEmpty(root)) throw new IOException("Mod folder is not initialized");
                bundle = AssetBundle.LoadFromFile(Path.Combine(root, "Assets/VFX/MajinFlesh/majinflesh"));
                if (!bundle) throw new IOException("Majin flesh bundle is missing");
                material = bundle.LoadAsset<Material>("assets/majinflesh/majinflesh.mat");
                if (!material || !material.shader || !material.shader.isSupported)
                    throw new IOException("Majin flesh shader is unavailable");
                if (!material.GetTexture("_FleshAtlas") || !material.GetTexture("_NormalAtlas"))
                    throw new IOException("Blender flesh atlases are missing");
                quad = new Mesh { name = "Majin native fragment visual quad" };
                quad.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateBounds();
                quad.UploadMeshData(true);
            }
            catch (Exception error)
            {
                material = null;
                Debug.LogWarning("[Singularity] Authored Majin flesh unavailable; native fragments remain visible: " + error.Message);
            }
        }

        public static BuuFleshView Attach(PhysicalBehaviour part, int variation)
        {
            if (!part || part.isDisintegrated || part.GetComponent<LimbBehaviour>() || !Ready) return null;
            int key = part.GetInstanceID();
            BuuFleshView existing;
            if (views.TryGetValue(key, out existing) && existing) return existing;
            existing = part.GetComponentInChildren<BuuFleshView>();
            if (existing) { views[key] = existing; return existing; }
            var root = new GameObject("Blender Majin living flesh");
            root.transform.SetParent(part.transform, false);
            root.AddComponent<Optout>();
            var view = root.AddComponent<BuuFleshView>();
            view.Bind(part, variation);
            views[key] = view;
            return view;
        }

        public static void Animate(PhysicalBehaviour part, float activity, float contraction)
        {
            if (!part) return;
            BuuFleshView view;
            if (views.TryGetValue(part.GetInstanceID(), out view) && view) view.SetActivity(activity, contraction);
        }
        public static void Tint(PhysicalBehaviour part, Color tint)
        {
            if (!part) return;
            BuuFleshView view;
            if (views.TryGetValue(part.GetInstanceID(), out view) && view) view.SetTint(tint);
        }
        internal static void Forget(int key, BuuFleshView view)
        {
            BuuFleshView current;
            if (views.TryGetValue(key, out current) && current == view) views.Remove(key);
        }
    }

    [SkipSerialisation]
    [DefaultExecutionOrder(310)]
    public sealed class BuuFleshView : MonoBehaviour
    {
        PhysicalBehaviour part;
        SpriteRenderer original;
        MeshRenderer view;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Vector2 size;
        Color tint;
        float activity, contraction, animationClock;
        int variation, key, lastDrivenFrame = -2;
        bool originalEnabled;
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        internal void Bind(PhysicalBehaviour physical, int index)
        {
            part = physical; key = part.GetInstanceID();
            variation = (index % 8 + 8) % 8;
            original = part.spriteRenderer ? part.spriteRenderer : part.GetComponent<SpriteRenderer>();
            originalEnabled = original && original.enabled;
            tint = InferTint(original);
            Bounds bounds = FragmentBounds(part);
            size = new Vector2(Mathf.Clamp(bounds.size.x * 1.28f, .025f, .85f), Mathf.Clamp(bounds.size.y * 1.28f, .025f, .85f));
            transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, -.015f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(size.x, size.y, 1f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = BuuFleshAssets.Quad;
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = BuuFleshAssets.Material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.sortingLayerID = original ? original.sortingLayerID : SortingLayer.NameToID("Default");
            view.sortingOrder = original ? original.sortingOrder + 1 : 1;
            if (original) original.enabled = false;
            Apply(0f);
        }

        static Color InferTint(SpriteRenderer source)
        {
            Color pink = new Color(.93f, .48f, .68f), grey = new Color(.48f, .46f, .50f);
            if (!source || !source.sprite || !source.sprite.texture || !source.sprite.texture.isReadable) return pink;
            try
            {
                var sprite = source.sprite; var rect = sprite.rect;
                float r = 0f, g = 0f, b = 0f, weight = 0f;
                for (int y = 1; y < 5; y++)
                for (int x = 1; x < 5; x++)
                {
                    var colour = sprite.texture.GetPixel(Mathf.Clamp(Mathf.FloorToInt(rect.x + rect.width * x / 5f), 0, sprite.texture.width - 1),
                        Mathf.Clamp(Mathf.FloorToInt(rect.y + rect.height * y / 5f), 0, sprite.texture.height - 1));
                    r += colour.r * colour.a; g += colour.g * colour.a; b += colour.b * colour.a; weight += colour.a;
                }
                if (weight > .01f && r / weight - g / weight < .13f && b / weight - g / weight < .14f) return grey;
            }
            catch { }
            return pink;
        }

        static Bounds FragmentBounds(PhysicalBehaviour physical)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;
            Action<Vector2> include = point => { min = Vector2.Min(min, point); max = Vector2.Max(max, point); any = true; };
            foreach (var collider in physical.GetComponents<Collider2D>())
            {
                if (!collider || collider.isTrigger) continue;
                var polygon = collider as PolygonCollider2D;
                if (polygon)
                {
                    for (int path = 0; path < polygon.pathCount; path++)
                        foreach (var point in polygon.GetPath(path)) include(point + polygon.offset);
                    continue;
                }
                var box = collider as BoxCollider2D;
                if (box) { include(box.offset - box.size * .5f); include(box.offset + box.size * .5f); continue; }
                var circle = collider as CircleCollider2D;
                if (circle) { include(circle.offset - Vector2.one * circle.radius); include(circle.offset + Vector2.one * circle.radius); continue; }
                var capsule = collider as CapsuleCollider2D;
                if (capsule) { include(capsule.offset - capsule.size * .5f); include(capsule.offset + capsule.size * .5f); continue; }
                var edge = collider as EdgeCollider2D;
                if (edge) foreach (var point in edge.points) include(point + edge.offset);
            }
            if (!any)
            {
                var source = physical.spriteRenderer;
                if (source && source.sprite) return source.sprite.bounds;
                return new Bounds(Vector3.zero, Vector3.one * .08f);
            }
            return new Bounds((min + max) * .5f, max - min);
        }

        public void SetTint(Color colour)
        {
            if (!Finite(colour.r) || !Finite(colour.g) || !Finite(colour.b)) return;
            tint = new Color(Mathf.Clamp01(colour.r), Mathf.Clamp01(colour.g), Mathf.Clamp01(colour.b), 1f);
            Apply(activity > .001f ? animationClock * 24f : 0f);
        }
        public void SetActivity(float amount, float pull)
        {
            if (!Finite(amount) || !Finite(pull)) return;
            activity = Mathf.Clamp01(amount); contraction = Mathf.Clamp01(pull);
            lastDrivenFrame = Time.frameCount;
        }
        void LateUpdate()
        {
            if (!part || !view) return;
            if (part.isDisintegrated || !part.gameObject.activeInHierarchy) { view.enabled = false; return; }
            view.enabled = true;
            if (original) original.enabled = false;
            // The owner drives this every active stage. If its time is stopped the
            // authored pose stays still too, even when the orphaned shard is outside it.
            if ((Global.main && Global.main.Paused) || RealityRift.Active || lastDrivenFrame != Time.frameCount) return;
            if (activity > .001f) animationClock += Time.deltaTime * (.15f + activity * 1.45f);
            float frame = activity > .001f ? animationClock * 24f : 0f;
            float squash = contraction * .045f;
            transform.localScale = new Vector3(size.x * (1f + squash), size.y * (1f - squash), 1f);
            Apply(frame);
        }
        void Apply(float frame)
        {
            if (!view) return;
            block.Clear();
            block.SetColor("_Tint", tint);
            block.SetFloat("_Frame", frame);
            block.SetFloat("_Variant", variation);
            block.SetFloat("_Activity", activity);
            block.SetFloat("_Contraction", contraction);
            block.SetFloat("_Opacity", 1f);
            view.SetPropertyBlock(block);
        }
        void OnDestroy()
        {
            BuuFleshAssets.Forget(key, this);
            if (original && part && !part.isDisintegrated) original.enabled = originalEnabled;
        }
    }
}
