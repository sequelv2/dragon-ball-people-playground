using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // What beams do to the terrain: gouged furrows, scorch marks, dust and embers. The rock slabs and thrown
    // chips are switched off (BeamSlabs.Enabled, ThrowChips). None of it has colliders; the map stays as it is.
    public static class BeamGround
    {
        static readonly RaycastHit2D[] hits = new RaycastHit2D[24];

        // The first solid, static surface below a point.
        public static bool Find(Vector2 from, float reach, Transform ignoreRoot, out Vector2 point, out Vector2 normal)
        {
            point = from; normal = Vector2.up;
            int n = Physics2D.RaycastNonAlloc(from + Vector2.up * .3f, Vector2.down, hits, reach + .3f);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (!col || col.isTrigger || !IsTerrain(col, ignoreRoot)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; normal = hits[i].normal; found = true; }
            }
            return found;
        }

        public static bool IsTerrain(Collider2D col, Transform ignoreRoot)
        {
            if (ignoreRoot && col.transform.root == ignoreRoot) return false;
            var body = col.attachedRigidbody;
            if (body && body.bodyType == RigidbodyType2D.Dynamic) return false;
            return !col.GetComponent<KiShieldSurface>() && !col.GetComponent<LimbBehaviour>() && !col.GetComponent<BeamComponent>();
        }

        public static void Scorch(Vector2 at, float radius, Color glow) { BeamScorch.Stamp(at, radius, glow); }

        // Flying chips of rock where energy meets the ground. Off.
        public static bool ThrowChips;

        // A foot or a skid dragged through the ground at speed.
        public static void Tear(Vector2 at, Vector2 motion, float intensity, Color glow)
        {
            float s = Mathf.Clamp01(intensity);
            Vector2 forward = motion.sqrMagnitude > .01f ? motion.normalized : Vector2.right;
            forward = new Vector2(Mathf.Sign(forward.x), 0f);
            var chips = BeamParticles.Chips; var dust = BeamParticles.Dust; var embers = BeamParticles.Embers;
            int count = ThrowChips ? 1 + Mathf.RoundToInt(s * 5f) : 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 v = forward * Random.Range(2f, 11f) * (.6f + s) + Vector2.up * Random.Range(2.5f, 10f) * (.6f + s);
                Color c = Color.Lerp(new Color(.2f, .16f, .13f), new Color(.46f, .38f, .3f), Random.value);
                chips.Emit(at + Random.insideUnitCircle * .1f, v, Random.Range(.5f, 1.1f), Random.Range(.05f, .2f) * (.8f + s * .8f), c, DbpuFx.A(c, .9f), .3f, 26f, Random.Range(-500f, 500f));
            }
            for (int i = 0; i < 2 + Mathf.RoundToInt(s * 3f); i++)
            {
                Color c = new Color(.6f, .55f, .49f, .34f);
                dust.Emit(at + Vector2.up * .1f, new Vector2(forward.x * Random.Range(-1f, 5f), Random.Range(.5f, 3f)) * (.7f + s), Random.Range(.5f, 1.2f), Random.Range(.28f, .6f) * (.8f + s), c, DbpuFx.A(c, 0f), 2.2f, -.4f, 0f, false, Random.Range(.8f, 1.5f) * (1f + s));
            }
            for (int i = 0; i < Mathf.RoundToInt(s * 6f); i++)
                embers.Emit(at, new Vector2(forward.x * Random.Range(1f, 12f), Random.Range(1f, 8f)), Random.Range(.2f, .5f), Random.Range(.04f, .09f), Color.Lerp(glow, Color.white, .5f), DbpuFx.A(glow, 0f), .8f, 14f, 0f, true);
        }

        // Rock thrown up all around a blow into the ground.
        public static void Erupt(Vector2 at, float scale, Color glow, Vector2 normal)
        {
            float s = Mathf.Clamp(scale, .2f, 5f);
            // Only ground takes a burn: the mark is clipped to a horizontal line, so it would hang off a wall.
            Vector2 burn, burnNormal;
            if (normal.y > .5f && Find(at + Vector2.up * 1.5f, 3f, null, out burn, out burnNormal) && burnNormal.y > .5f) Scorch(burn, 1.1f + s * 1.5f, glow);
            int slabs = 2 + Mathf.RoundToInt(s * 4f);
            for (int i = 0; i < slabs; i++)
            {
                float x = (i + .5f - slabs * .5f) / Mathf.Max(1f, slabs) * (1.6f + s * 2.6f);
                Vector2 ground, n;
                Vector2 p = at + new Vector2(x, 0f);
                if (!Find(p + Vector2.up * 3f, 7f, null, out ground, out n)) continue;
                BeamSlabs.Raise(ground, Mathf.Lerp(1.1f, .5f, Mathf.Abs(x) / (1f + s * 1.6f)) * (.5f + s * .55f) * Random.Range(.7f, 1.2f), Mathf.Sign(x) * Random.Range(5f, 28f), glow);
            }
            var chips = BeamParticles.Chips; var dust = BeamParticles.Dust; var embers = BeamParticles.Embers;
            Vector2 up = normal.sqrMagnitude > .1f ? normal.normalized : Vector2.up;
            for (int i = 0; ThrowChips && i < 12 + Mathf.RoundToInt(s * 16f); i++)
            {
                Vector2 v = DbpuFx.Rotate(up, Random.Range(-1f, 1f)) * Random.Range(5f, 19f) * (.6f + s * .35f);
                Color c = Color.Lerp(new Color(.18f, .14f, .12f), new Color(.5f, .4f, .3f), Random.value);
                chips.Emit(at, v, Random.Range(.6f, 1.5f), Random.Range(.07f, .3f) * (.7f + s * .4f), c, DbpuFx.A(c, .9f), .25f, 24f, Random.Range(-600f, 600f));
            }
            for (int i = 0; i < 8 + Mathf.RoundToInt(s * 8f); i++)
            {
                Color c = new Color(.55f, .5f, .45f, .4f);
                float side = Random.value < .5f ? -1f : 1f;
                dust.Emit(at, new Vector2(side * Random.Range(1.5f, 7f), Random.Range(.5f, 5f)) * (.6f + s * .35f), Random.Range(.7f, 1.6f), Random.Range(.4f, .8f) * (.7f + s * .4f), c, DbpuFx.A(c, 0f), 2f, -.3f, 0f, false, Random.Range(1.2f, 2.4f) * (.7f + s * .5f));
            }
            for (int i = 0; i < 8 + Mathf.RoundToInt(s * 12f); i++)
            {
                Vector2 v = DbpuFx.Rotate(up, Random.Range(-1.3f, 1.3f)) * Random.Range(4f, 20f);
                embers.Emit(at, v, Random.Range(.25f, .7f), Random.Range(.04f, .11f), Color.Lerp(glow, Color.white, .5f), DbpuFx.A(glow, 0f), .6f, 16f, 0f, true);
            }
        }
    }

    // A trench behind a dragged foot: a dark strip cut in below the surface, hot where it is fresh.
    public sealed class BeamFurrow : MonoBehaviour
    {
        const int Capacity = 72; const float Life = 22f, Depth = .34f;
        static readonly List<BeamFurrow> live = new List<BeamFurrow>();
        readonly Vector2[] points = new Vector2[Capacity];
        readonly float[] born = new float[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 2];
        readonly Vector2[] uvs = new Vector2[Capacity * 2];
        readonly Color[] colors = new Color[Capacity * 2];
        Mesh mesh; MeshRenderer view; MaterialPropertyBlock block; Color glow; int count; float clock, last, seed, arc;
        public bool Full { get { return count >= Capacity; } }
        public Vector2 LastPoint { get { return count > 0 ? points[count - 1] : Vector2.zero; } }

        public static BeamFurrow Begin(Color glow)
        {
            if (!BeamFxAssets.Ready) return null;
            live.RemoveAll(existing => !existing);
            if (live.Count >= 10) { if (live[0]) Destroy(live[0].gameObject); live.RemoveAt(0); }
            var go = new GameObject("DBPU beam furrow");
            var f = go.AddComponent<BeamFurrow>();
            f.glow = glow; f.seed = Random.value * 30f;
            f.mesh = new Mesh { name = "DBPU beam furrow" }; f.mesh.MarkDynamic();
            var triangles = new int[(Capacity - 1) * 6];
            for (int i = 0; i < Capacity - 1; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            f.mesh.vertices = f.vertices; f.mesh.uv = f.uvs; f.mesh.colors = f.colors; f.mesh.triangles = triangles;
            f.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 40000f);
            go.AddComponent<MeshFilter>().sharedMesh = f.mesh;
            f.view = go.AddComponent<MeshRenderer>();
            f.view.sharedMaterial = BeamFxAssets.Ground;
            f.view.sortingLayerName = "Top"; f.view.sortingOrder = 2;
            f.view.shadowCastingMode = ShadowCastingMode.Off; f.view.receiveShadows = false;
            f.block = new MaterialPropertyBlock();
            live.Add(f);
            return f;
        }

        public void Add(Vector2 surface)
        {
            if (count >= Capacity) return;
            if (count > 0) arc += Vector2.Distance(points[count - 1], surface);
            points[count] = surface; born[count] = clock;
            int v = count * 2;
            float jag = Random.Range(.7f, 1.2f);
            vertices[v] = new Vector3(surface.x, surface.y + .04f, 0f);
            vertices[v + 1] = new Vector3(surface.x, surface.y - Depth * jag, 0f);
            uvs[v] = new Vector2(arc, 0f); uvs[v + 1] = new Vector2(arc, 1f);
            count++; last = clock;
            for (int i = count * 2; i < Capacity * 2; i++) vertices[i] = vertices[v + 1];
            mesh.vertices = vertices; mesh.uv = uvs;
        }

        void Update()
        {
            float dt = DbpuFx.Dt;
            clock += dt;
            if (count == 0) { if (clock > 2f) Destroy(gameObject); return; }
            float fade = 1f - DbpuFx.Smooth(Life - 6f, Life, clock - last);
            if (fade <= 0f) { Destroy(gameObject); return; }
            for (int i = 0; i < count; i++)
                colors[i * 2] = colors[i * 2 + 1] = new Color(Mathf.Clamp01(1f - (clock - born[i]) / 3.2f), 0f, 0f, 1f);
            mesh.colors = colors;
            block.Clear();
            block.SetColor("_Glow", glow); block.SetFloat("_Mode", 0f); block.SetFloat("_Opacity", fade);
            block.SetFloat("_Seed", seed); block.SetFloat("_Hdr", BeamFxAssets.Hdr);
            view.SetPropertyBlock(block);
        }

        void OnDestroy() { live.Remove(this); if (mesh) Destroy(mesh); }
    }

    // Burnt ground under a blow, clipped to the surface it landed on.
    public sealed class BeamScorch : MonoBehaviour
    {
        const float Life = 40f;
        static readonly List<BeamScorch> live = new List<BeamScorch>();
        MeshRenderer view; MaterialPropertyBlock block; Color glow; float age, seed, surface;

        public static void Stamp(Vector2 at, float radius, Color glow)
        {
            if (!BeamFxAssets.Ready) return;
            live.RemoveAll(c => !c);
            foreach (var near in live)
            {
                // Repeated blows in one place deepen the mark rather than piling up quads.
                if (Vector2.Distance(near.transform.position, at) < radius * .45f)
                {
                    float r = Mathf.Min(7f, Mathf.Max(near.transform.localScale.x, radius) + .08f);
                    near.transform.localScale = new Vector3(r, r * .55f, 1f); near.age = 0f;
                    return;
                }
            }
            if (live.Count >= 16) { if (live[0]) Destroy(live[0].gameObject); live.RemoveAt(0); }
            var go = new GameObject("DBPU beam scorch");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = new Vector3(radius, radius * .55f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = BeamFxAssets.Quad;
            var s = go.AddComponent<BeamScorch>();
            s.view = go.AddComponent<MeshRenderer>();
            s.view.sharedMaterial = BeamFxAssets.Ground;
            s.view.sortingLayerName = "Top"; s.view.sortingOrder = 1;
            s.view.shadowCastingMode = ShadowCastingMode.Off; s.view.receiveShadows = false;
            s.block = new MaterialPropertyBlock();
            s.glow = glow; s.seed = Random.value * 30f; s.surface = at.y + .03f;
            live.Add(s);
        }

        void Update()
        {
            age += DbpuFx.Dt;
            if (age >= Life) { Destroy(gameObject); return; }
            block.Clear();
            block.SetColor("_Glow", glow); block.SetFloat("_Mode", 1f); block.SetFloat("_Heat", Mathf.Clamp01(1f - age / 4f));
            block.SetFloat("_Opacity", 1f - DbpuFx.Smooth(Life * .7f, Life, age)); block.SetFloat("_SurfaceY", surface);
            block.SetFloat("_Seed", seed); block.SetFloat("_Hdr", BeamFxAssets.Hdr);
            view.SetPropertyBlock(block);
        }

        void OnDestroy() { live.Remove(this); }
    }

    // Slabs of rock forced up out of the ground. They spring up, stand a while, then crumble away.
    public sealed class BeamSlabs : MonoBehaviour
    {
        const int Capacity = 56, Shapes = 6;
        // Off: energy meeting the ground leaves no shards of rock.
        public static bool Enabled;
        sealed class Slab { public Transform T; public MeshRenderer View; public Vector2 Base; public float Age, Life, Height, Width, Lean, Heat; public Color Glow; public bool Active; }
        static BeamSlabs instance;
        static Mesh[] shapes;
        readonly List<Slab> slabs = new List<Slab>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Material material;

        static Mesh Shape(int seed)
        {
            // An irregular shard, wide at the root, drawn with vertex colours.
            var random = new System.Random(seed * 7919 + 13);
            float R(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
            var outline = new List<Vector2> { new Vector2(-.5f, -.02f), new Vector2(R(-.5f, -.38f), R(.1f, .3f)), new Vector2(R(-.3f, -.16f), R(.48f, .7f)), new Vector2(R(-.1f, .12f), 1f), new Vector2(R(.18f, .32f), R(.45f, .68f)), new Vector2(R(.34f, .5f), R(.08f, .3f)), new Vector2(.5f, -.02f) };
            var vertices = new Vector3[outline.Count + 1]; var colors = new Color[vertices.Length];
            vertices[0] = new Vector3(0f, .15f, 0f); colors[0] = new Color(.14f, .11f, .095f, 1f);
            for (int i = 0; i < outline.Count; i++)
            {
                vertices[i + 1] = outline[i];
                float t = Mathf.Clamp01(outline[i].y);
                colors[i + 1] = Color.Lerp(new Color(.07f, .055f, .05f, 1f), new Color(.34f, .27f, .22f, 1f), t * (outline[i].x > 0f ? .55f : 1f));
            }
            var triangles = new int[(outline.Count - 1) * 3];
            for (int i = 0; i < outline.Count - 1; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
            var mesh = new Mesh { name = "DBPU rock slab", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Raise(Vector2 ground, float height, float lean, Color glow)
        {
            if (!Enabled) return;
            if (!instance)
            {
                var go = new GameObject("DBPU rock slabs");
                instance = go.AddComponent<BeamSlabs>();
                instance.material = new Material(Shader.Find("Sprites/Default")) { name = "DBPU rock slab" };
            }
            if (shapes == null) { shapes = new Mesh[Shapes]; for (int i = 0; i < Shapes; i++) shapes[i] = Shape(i); }
            Slab slab = null, oldest = null;
            foreach (var s in instance.slabs)
            {
                if (!s.Active) { slab = s; break; }
                if (oldest == null || s.Age / s.Life > oldest.Age / oldest.Life) oldest = s;
            }
            if (slab == null)
            {
                if (instance.slabs.Count < Capacity)
                {
                    var go = new GameObject("DBPU rock slab");
                    go.transform.SetParent(instance.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = shapes[instance.slabs.Count % Shapes];
                    var view = go.AddComponent<MeshRenderer>();
                    view.sharedMaterial = instance.material;
                    view.sortingLayerName = "Top"; view.sortingOrder = -1;
                    view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
                    slab = new Slab { T = go.transform, View = view };
                    instance.slabs.Add(slab);
                }
                else slab = oldest;
            }
            slab.Base = ground; slab.Age = 0f; slab.Life = Random.Range(4f, 8f); slab.Height = Mathf.Clamp(height, .15f, 3.4f);
            slab.Width = slab.Height * Random.Range(.55f, .95f); slab.Lean = lean; slab.Heat = 1f; slab.Glow = glow; slab.Active = true;
            slab.T.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = DbpuFx.Dt;
            if (dt <= 0f) return;
            foreach (var s in slabs)
            {
                if (!s.Active) continue;
                s.Age += dt; s.Heat = Mathf.Max(0f, s.Heat - dt * .9f);
                if (s.Age >= s.Life) { s.Active = false; s.T.gameObject.SetActive(false); continue; }
                float rise = StfOut(s.Age / .1f);
                float sink = DbpuFx.Smooth(s.Life - 1.1f, s.Life, s.Age);
                float h = s.Height * rise * (1f + .22f * Mathf.Sin(Mathf.Clamp01(s.Age / .1f) * Mathf.PI));
                s.T.position = new Vector3(s.Base.x, s.Base.y - sink * s.Height * 1.05f, 0f);
                s.T.rotation = Quaternion.Euler(0f, 0f, s.Lean * (1f + sink * .5f));
                s.T.localScale = new Vector3(s.Width, h, 1f);
                block.Clear();
                block.SetColor("_Color", Color.Lerp(Color.white, Color.Lerp(s.Glow, Color.white, .3f) * 2.6f, s.Heat * s.Heat * .6f));
                s.View.SetPropertyBlock(block);
            }
        }

        static float StfOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    }
}
