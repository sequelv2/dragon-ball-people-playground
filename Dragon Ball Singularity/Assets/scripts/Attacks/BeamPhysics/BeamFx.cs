using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // How one technique's energy looks. The colours come from its original beam texture.
    public struct BeamStyle
    {
        public Color Hot, Mid, Deep;
        public float Radius, Spiral, Helix;

        static BeamStyle Make(Color hot, Color mid, Color deep, float radius = .52f, float spiral = 0f, float helix = 1f)
        { return new BeamStyle { Hot = hot, Mid = mid, Deep = deep, Radius = radius, Spiral = spiral, Helix = helix }; }

        public static BeamStyle Of(BeamBehaviour beam)
        {
            switch (beam ? beam.EffectID : "")
            {
                case "Rose Kamehameha": return Make(new Color(1f, .93f, .97f), new Color(1f, .4f, .78f), new Color(.6f, .05f, .45f));
                case "Full Force Kamehameha": return Make(new Color(.95f, .98f, 1f), new Color(.45f, .8f, 1f), new Color(.09f, .45f, .85f));
                case "Galick Gun": return Make(new Color(.99f, .93f, 1f), new Color(.85f, .45f, 1f), new Color(.4f, .08f, .85f));
                case "Final Flash": return Make(new Color(1f, 1f, .93f), new Color(1f, .78f, .4f), new Color(.85f, .42f, .1f));
                case "Energy Wave":
                    if (beam.Character == "Beerus") return Make(new Color(.95f, .88f, 1f), new Color(.56f, .31f, 1f), new Color(.25f, .08f, .75f));
                    return Make(new Color(1f, 1f, .72f), new Color(1f, .82f, .08f), new Color(.8f, .45f, .02f));
                case "Last Emperor": return Make(new Color(1f, .85f, .9f), new Color(1f, .5f, .62f), new Color(.75f, .1f, .3f));
                case "Gigantic Full Blast":
                case "Gigantic Roar":
                case "Max Cannon": return Make(new Color(.88f, 1f, .85f), new Color(.2f, .92f, .25f), new Color(.02f, .5f, .12f));
                case "Special Beam Cannon": return Make(new Color(1f, .9f, .3f), new Color(1f, .34f, .72f), new Color(.55f, .06f, .42f), .16f, 1f, 0f);
                default: return Make(new Color(.93f, .99f, 1f), new Color(.2f, .78f, 1f), new Color(.05f, .3f, .95f));
            }
        }

        public static BeamStyle Of(DbpuPalette palette) { return Make(Color.Lerp(palette.Hot, Color.white, .4f), palette.Vivid, palette.Deep); }
    }

    // The beam shaders and the mesh they draw on. Without the bundle, ribbons fall back to the beam's
    // own line renderer and radial effects to the mod's procedural sprites.
    public static class BeamFxAssets
    {
        static bool attempted; static AssetBundle bundle; static Material ribbon, burst, ground; static Mesh quad;
        public static bool Ready { get { Load(); return ribbon && burst && ground; } }
        public static Material Ribbon { get { Load(); return ribbon; } }
        public static Material Burst { get { Load(); return burst; } }
        public static Material Ground { get { Load(); return ground; } }
        public static float Hdr { get { var c = Global.main ? Global.main.camera : null; return c && c.allowHDR ? 2.8f : 1f; } }

        static void Load()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                bundle = AssetBundle.LoadFromFile(Path.Combine(Singularity.ModDirectory, "Assets/VFX/BeamFx/beamfx"));
                if (!bundle) throw new IOException("Bundle is missing");
                ribbon = Take("beamribbon"); burst = Take("beamburst"); ground = Take("beamground");
            }
            catch (Exception ex)
            {
                ribbon = burst = ground = null;
                Debug.LogWarning("[Singularity] Beam shaders unavailable: " + ex.Message);
            }
        }

        static Material Take(string name)
        {
            var value = bundle.LoadAsset<Material>("assets/beamfx/" + name + ".mat");
            if (!value || !value.shader || !value.shader.isSupported) throw new IOException(name + " is unsupported");
            return value;
        }

        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "DBPU beam quad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) };
                quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateBounds();
                return quad;
            }
        }
    }

    // One radial effect around a beam: a quad on the burst shader, or a soft sprite without it.
    public sealed class BeamQuad
    {
        public const int Crown = 0, Rays = 1, Flame = 2, Orb = 3, Wash = 4, Arc = 5, Slash = 6;
        readonly GameObject go; readonly MeshRenderer view; readonly SpriteRenderer sprite; readonly MaterialPropertyBlock block;
        public int Mode; public float Power = 1f, Opacity = 1f, Spread, Speed = 1f; public Vector2 Dir = Vector2.right;
        public Color Hot = Color.white, Mid = Color.white, Deep = Color.white, Alt = Color.white;
        readonly float seed; float clock;

        public BeamQuad(string name, int mode, string layer, int order)
        {
            Mode = mode; seed = UnityEngine.Random.value * 40f;
            go = new GameObject(name);
            if (BeamFxAssets.Ready)
            {
                go.AddComponent<MeshFilter>().sharedMesh = BeamFxAssets.Quad;
                view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = BeamFxAssets.Burst;
                view.sortingLayerName = layer; view.sortingOrder = order;
                view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
                block = new MaterialPropertyBlock();
            }
            else if (mode == Crown || mode == Orb) sprite = DbpuFx.MakeSprite(go.transform, name, DbpuFx.Glow, order, true, layer);
        }

        public void Style(BeamStyle style) { Hot = style.Hot; Mid = style.Mid; Deep = style.Deep; }

        public void Place(Vector2 at, float radius)
        {
            if (!go) return;
            go.transform.position = new Vector3(at.x, at.y, -.4f);
            go.transform.localScale = new Vector3(radius, radius, 1f);
            if (sprite) sprite.transform.localScale = Vector3.one * 2f;
        }

        public void Apply(float dt)
        {
            if (!go) return;
            clock += dt * Speed;
            if (sprite)
            {
                sprite.enabled = true;
                sprite.color = DbpuFx.A(Color.Lerp(Mid, Hot, .4f), Mathf.Clamp01(Opacity * Power * .8f));
                return;
            }
            if (!view) return;
            view.enabled = Opacity > .002f && Power > .002f;
            if (!view.enabled) return;
            block.Clear();
            block.SetColor("_Hot", Hot); block.SetColor("_Mid", Mid); block.SetColor("_Deep", Deep); block.SetColor("_Alt", Alt);
            block.SetVector("_Dir", new Vector4(Dir.x, Dir.y, 0f, 0f));
            block.SetFloat("_Clock", clock); block.SetFloat("_Seed", seed); block.SetFloat("_Power", Power); block.SetFloat("_Opacity", Opacity);
            block.SetFloat("_Mode", Mode); block.SetFloat("_Spread", Spread); block.SetFloat("_Hdr", BeamFxAssets.Hdr);
            view.SetPropertyBlock(block);
        }

        public void Hide() { if (view) view.enabled = false; if (sprite) sprite.enabled = false; }
        public void Destroy() { if (go) UnityEngine.Object.Destroy(go); }
    }

    // The plasma column: a triangle strip along the spline, rebuilt every frame.
    public sealed class BeamRibbon
    {
        public const int MaxSections = 120;
        readonly GameObject go; readonly Mesh mesh; readonly MeshRenderer view; readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Vector3[] vertices = new Vector3[MaxSections * 2];
        readonly Vector2[] uvs = new Vector2[MaxSections * 2];
        readonly Color[] colors = new Color[MaxSections * 2];
        readonly float seed = UnityEngine.Random.value * 40f;

        public BeamRibbon()
        {
            go = new GameObject("DBPU beam column");
            mesh = new Mesh { name = "DBPU beam column" }; mesh.MarkDynamic();
            var triangles = new int[(MaxSections - 1) * 6];
            for (int i = 0; i < MaxSections - 1; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh.vertices = vertices; mesh.uv = uvs; mesh.colors = colors; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 40000f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = BeamFxAssets.Ribbon;
            view.sortingLayerName = "Background"; view.sortingOrder = -14;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
        }

        public void Draw(Vector2[] nodes, float[] arcs, float[] half, float[] heat, float[] alpha, int count, BeamStyle style, float width, float strain, float opacity, float clock)
        {
            if (!go) return;
            count = Mathf.Min(count, MaxSections);
            view.enabled = count >= 2 && opacity > .002f && width > .002f;
            if (!view.enabled) return;
            for (int i = 0; i < MaxSections; i++)
            {
                int v = i * 2;
                if (i >= count)
                {
                    vertices[v] = vertices[v + 1] = i > 0 ? vertices[v - 1] : Vector3.zero;
                    colors[v] = colors[v + 1] = Color.clear;
                    continue;
                }
                Vector2 a = nodes[Mathf.Max(0, i - 1)], b = nodes[Mathf.Min(count - 1, i + 1)], d = b - a;
                float m = d.magnitude;
                Vector2 n = m > .0001f ? new Vector2(-d.y, d.x) / m : Vector2.up;
                Vector2 p = nodes[i]; float h = half[i];
                vertices[v] = p + n * h; vertices[v + 1] = p - n * h;
                uvs[v] = new Vector2(arcs[i], 1f); uvs[v + 1] = new Vector2(arcs[i], -1f);
                colors[v] = colors[v + 1] = new Color(heat[i], 0f, 0f, alpha[i]);
            }
            mesh.vertices = vertices; mesh.uv = uvs; mesh.colors = colors;
            block.Clear();
            block.SetColor("_Hot", style.Hot); block.SetColor("_Mid", style.Mid); block.SetColor("_Deep", style.Deep);
            block.SetFloat("_Clock", clock); block.SetFloat("_BeamWidth", Mathf.Max(.04f, width)); block.SetFloat("_Strain", strain);
            block.SetFloat("_Flow", 46f / Mathf.Max(.2f, width)); block.SetFloat("_Seed", seed); block.SetFloat("_Helix", style.Helix);
            block.SetFloat("_Spiral", style.Spiral); block.SetFloat("_CoreRadius", style.Radius); block.SetFloat("_Opacity", opacity); block.SetFloat("_Hdr", BeamFxAssets.Hdr);
            view.SetPropertyBlock(block);
        }

        public void Hide() { if (view) view.enabled = false; }
        public void Destroy() { if (go) UnityEngine.Object.Destroy(go); if (mesh) UnityEngine.Object.Destroy(mesh); }
    }

    // Shared particle fields; each survives between effects and refills after a scene change.
    public static class BeamParticles
    {
        static DbpuParticles sparks, shards, stars, dust, chips, embers;
        static DbpuParticles Field(ref DbpuParticles field, string name, Texture2D texture, int capacity, int order)
        {
            if (!field) field = DbpuParticles.Create(name, texture, capacity, order);
            return field;
        }
        public static DbpuParticles Sparks { get { return Field(ref sparks, "DBPU beam sparks", DbpuFx.Spark, 900, 66); } }
        public static DbpuParticles Shards { get { return Field(ref shards, "DBPU beam shards", DbpuFx.Shard, 260, 65); } }
        public static DbpuParticles Stars { get { return Field(ref stars, "DBPU beam twinkles", DbpuFx.Star, 180, 64); } }
        public static DbpuParticles Dust { get { return Field(ref dust, "DBPU beam dust", DbpuFx.Glow, 420, 40); } }
        public static DbpuParticles Chips { get { return Field(ref chips, "DBPU beam rock chips", DbpuFx.Shard, 360, 41); } }
        public static DbpuParticles Embers { get { return Field(ref embers, "DBPU beam embers", DbpuFx.Spark, 360, 42); } }
    }
}
