using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Matte rubber, not ki: one mesh and a fixed particle budget for each Majin.
    [SkipSerialisation]
    [DefaultExecutionOrder(320)]
    public sealed class BuuRegenerationFx : MonoBehaviour
    {
        const int ParticleBudget = 112, TremorBudget = 24;
        static Texture2D atlas;
        static Material material;
        static AudioClip gatherSound, fuseSound, woundSound, pullSound;
        static int users;
        readonly Particle[] particles = new Particle[ParticleBudget];
        readonly Tremor[] tremors = new Tremor[TremorBudget];
        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Vector2> uv = new List<Vector2>(4096);
        readonly List<Color> colours = new List<Color>(4096);
        readonly List<int> triangles = new List<int>(6144);
        Mesh mesh;
        MeshRenderer renderer;
        AudioSource matterAudio;
        PersonBehaviour person;
        Color tint = new Color(1f, .43f, .66f);
        int cursor, tremorCount;
        float clock, nextWound, nextWoundSound, nextGatherSound;
        bool registered, audioPaused;

        struct Particle
        {
            public bool Active, Inward;
            public int Cell;
            public Vector2 Origin, Position, Velocity, Target;
            public float Age, Life, Size, Rotation, Spin, Seed;
            public Color Colour;
        }
        struct Tremor { public Vector2 Point; public float Size, Strength, Seed; }

        public static BuuRegenerationFx Attach(PersonBehaviour person)
        {
            if (!person) return null;
            var existing = person.GetComponentInChildren<BuuRegenerationFx>();
            if (existing)
            {
                existing.person = person;
                existing.Build();
                return existing;
            }
            var root = new GameObject("Majin living matter");
            root.transform.SetParent(person.transform, false);
            // Native copy/save reconstructs the fighter from its spawn recipe. Runtime art
            // must not enter RelevantTransforms or try to serialise generated meshes/clips.
            root.AddComponent<Optout>();
            var fx = root.AddComponent<BuuRegenerationFx>();
            fx.person = person;
            if (person.name.IndexOf("Evil", System.StringComparison.OrdinalIgnoreCase) >= 0)
                fx.SetTint(new Color(.61f, .62f, .63f));
            fx.Build();
            return fx;
        }

        public void SetTint(Color skin)
        {
            if (!Finite(skin.r) || !Finite(skin.g) || !Finite(skin.b)) return;
            tint = new Color(Mathf.Clamp01(skin.r), Mathf.Clamp01(skin.g), Mathf.Clamp01(skin.b), 1f);
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool Finite(Vector2 point) { return Finite(point.x) && Finite(point.y); }
        static float Edge(float low, float high, float value)
        {
            float t = Mathf.Clamp01((value - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        void Build()
        {
            if (registered && mesh && renderer && matterAudio) return;
            Prepare();
            if (!registered) { users++; registered = true; }
            if (!GetComponent<Optout>()) gameObject.AddComponent<Optout>();
            if (mesh) Destroy(mesh);
            mesh = new Mesh { name = "Majin viscous matter mesh" }; mesh.MarkDynamic();
            var filter = GetComponent<MeshFilter>();
            if (!filter) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            renderer = GetComponent<MeshRenderer>();
            if (!renderer) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = "Top";
            renderer.sortingOrder = 7;
            renderer.enabled = false;
            var soundTransform = transform.Find("Majin matter audio");
            GameObject soundRoot = soundTransform ? soundTransform.gameObject : new GameObject("Majin matter audio");
            soundRoot.transform.SetParent(transform, false);
            if (!soundRoot.GetComponent<Optout>()) soundRoot.AddComponent<Optout>();
            matterAudio = soundRoot.GetComponent<AudioSource>();
            if (!matterAudio) matterAudio = soundRoot.AddComponent<AudioSource>();
            matterAudio.playOnAwake = false;
            matterAudio.loop = false;
            matterAudio.spatialBlend = .65f;
            matterAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            matterAudio.minDistance = 4f;
            matterAudio.maxDistance = 30f;
        }

        static void Prepare()
        {
            if (atlas && material) return;
            const int cell = 64, size = cell * 2;
            atlas = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Majin rubber and steam atlas", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (int type = 0; type < 4; type++)
            for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
            {
                float px = (x + .5f) / cell * 2f - 1f, py = (y + .5f) / cell * 2f - 1f;
                float angle = Mathf.Atan2(py, px);
                float radius = Mathf.Sqrt(px * px + py * py);
                float edge = .81f + Mathf.Sin(angle * 3f + .7f) * .052f + Mathf.Sin(angle * 5f) * .025f;
                float alpha = 1f - Edge(edge - .035f, edge + .035f, radius);
                float body = Mathf.Lerp(.42f, 1f, Mathf.Clamp01((edge - radius) * 9f));
                body *= .87f + .13f * Mathf.Clamp01(py + .65f);
                float highlight = Mathf.Exp(-((px + .23f) * (px + .23f) * 28f + (py - .31f) * (py - .31f) * 62f));
                body = Mathf.Lerp(body, 1.24f, highlight * .75f);
                // Broad, slightly uneven folds keep living matter from looking like
                // a polished plastic sphere. It remains Majin tissue, never red gore.
                float fold = py + .14f * Mathf.Sin(px * 8f + .6f);
                body *= 1f - Mathf.Exp(-fold * fold * 420f) * .105f * Mathf.Clamp01(1f - radius);
                if (type == 1)
                {
                    float cloud = Mathf.Exp(-radius * radius * 4.7f);
                    float lobes = .78f + .22f * Mathf.PerlinNoise(px * 3f + 8f, py * 3f + 4f);
                    alpha = cloud * lobes * (1f - Edge(.72f, .99f, radius));
                    body = 1f;
                }
                else if (type == 2)
                {
                    float dropRadius = Mathf.Sqrt(px * px * (1f + .45f * py) + py * py);
                    alpha = 1f - Edge(.69f, .77f, dropRadius);
                    body = Mathf.Lerp(.54f, 1f, Mathf.Clamp01((.77f - dropRadius) * 9f));
                    body = Mathf.Lerp(body, 1.22f, highlight * .7f);
                }
                else if (type == 3)
                {
                    float ring = Mathf.Abs(radius - .68f);
                    alpha = (1f - Edge(.08f, .15f, ring)) * (1f - Edge(.86f, .94f, radius));
                    body = .85f + .15f * py;
                }
                pixels[(type / 2 * cell + y) * size + type % 2 * cell + x] = new Color(body, body, body, alpha);
            }
            atlas.SetPixels(pixels); atlas.Apply(false, true);
            material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Majin matte matter", mainTexture = atlas, hideFlags = HideFlags.HideAndDontSave
            };
            gatherSound = MakeSound("Majin gathering rubber", .36f, 0);
            fuseSound = MakeSound("Majin matter fusion", .26f, 1);
            woundSound = MakeSound("Majin small bubble", .13f, 2);
            pullSound = MakeSound("Majin living tissue pull", .86f, 3);
        }

        static AudioClip MakeSound(string name, float duration, int kind)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            uint noise = 0x94d03a7u + (uint)kind * 541u;
            float filteredNoise = 0f, phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate, t = time / duration;
                noise = unchecked(noise * 1664525u + 1013904223u);
                filteredNoise = Mathf.Lerp(filteredNoise, ((noise >> 8) / 8388608f - 1f), .13f);
                float frequency = kind == 0 ? 80f + 160f * t * t : kind == 1 ? 95f + 190f * Mathf.Exp(-time * 28f)
                    : kind == 2 ? 195f - 100f * t : 68f + 32f * Mathf.Sin(t * 4f) + 35f * t;
                phase += frequency * Mathf.PI * 2f / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * Mathf.Exp(-time * (kind == 3 ? 1.2f : kind == 0 ? 3f : 12f));
                float body = Mathf.Sin(phase) * .35f + Mathf.Sin(phase * 1.81f) * .09f;
                float bubbles = 0f;
                for (int b = 0; b < (kind == 2 ? 1 : kind == 3 ? 5 : 3); b++)
                {
                    float u = time - (.013f + b * (kind == 3 ? .14f : .072f));
                    if (u < 0f) continue;
                    float popEnvelope = Mathf.Exp(-u * 62f) * Mathf.Min(1f, u * 1500f);
                    bubbles += Mathf.Sin((u * (310f - b * 62f) - u * u * 420f) * Mathf.PI * 2f) * popEnvelope * .24f;
                }
                float air = filteredNoise * Mathf.Exp(-time * (kind == 3 ? 2f : 18f)) * (kind == 3 ? .27f : .17f);
                float value = body * envelope + bubbles + air;
                float edge = Mathf.Min(1f, time / .004f) * Mathf.Clamp01((duration - time) / .015f);
                samples[i] = value / (1f + Mathf.Abs(value)) * edge;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        void Sound(AudioClip clip, float volume, Vector2 point)
        {
            if (!matterAudio || !clip || (Global.main && Global.main.Paused)) return;
            float setting = Finite(DBPU.PowersVolume) ? Mathf.Clamp(DBPU.PowersVolume, 0f, 2f) : 0f;
            if (setting <= 0f) return;
            matterAudio.transform.position = new Vector3(point.x, point.y, 0f);
            matterAudio.pitch = Random.Range(.94f, 1.06f);
            matterAudio.PlayOneShot(clip, volume * setting);
        }

        public void Begin(Vector2 anchor)
        {
            if (!Finite(anchor)) return;
            Sound(gatherSound, .31f, anchor);
            // The real pieces provide the spectacle. Only a little wet surface
            // matter rises here; no decorative cloud pretends to be surviving flesh.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2.399963f;
                Vector2 outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float radius = .04f + Random.value * .15f;
                Emit(anchor + outward * radius, outward * .15f, .48f + Random.value * .55f,
                    .018f + Random.value * .028f, i % 4 == 0 ? 2 : 0, tint, true, anchor);
            }
            Steam(anchor, 2, .55f);
        }

        public void Quiver(Vector2 point, float strength) { Quiver(point, .12f, strength); }
        public void Quiver(Vector2 point, float size, float strength)
        {
            if (tremorCount >= TremorBudget || !Finite(point) || !Finite(size) || !Finite(strength)) return;
            tremors[tremorCount] = new Tremor
            {
                Point = point, Size = Mathf.Clamp(size, .035f, .35f), Strength = Mathf.Clamp01(strength),
                Seed = tremorCount * 2.399963f + .8f
            };
            tremorCount++;
        }

        public void Gather(Vector2 centre, float progress)
        {
            if (!Finite(centre) || !Finite(progress) || clock < nextGatherSound) return;
            nextGatherSound = clock + 2.6f;
            Sound(pullSound, .34f, centre);
        }

        // Retained API compatibility: body construction is exclusively opaque pieces.
        public void Link(Vector2 from, Vector2 to, float progress) { }
        public void GrowBody(Vector2 pelvis, Vector2 crown, float bodyWidth, float progress) { }

        public void Wound(Vector2 point, float scale)
        {
            // Several healing limbs can request this together. Bound emission independently of FPS.
            if (clock < nextWound || !Finite(point) || !Finite(scale)) return;
            nextWound = clock + .11f;
            scale = Mathf.Clamp(scale, .12f, 2f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Random.insideUnitCircle * (.12f * scale);
                Emit(point + offset, offset * .4f, .28f + Random.value * .2f,
                    (.025f + Random.value * .045f) * scale, 0, tint, true, point);
            }
            Emit(point, Vector2.zero, .4f, .15f * scale, 3, Lighten(tint, .25f), false, point);
            Steam(point, 1, scale * .7f);
            if (clock >= nextWoundSound)
            {
                nextWoundSound = clock + .65f;
                Sound(woundSound, .085f, point);
            }
        }

        public void Complete(Vector2 point)
        {
            if (!Finite(point)) return;
            Sound(fuseSound, .46f, point);
            Steam(point, 5, .85f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 offset = Random.insideUnitCircle * .24f;
                Emit(point + offset, offset * .65f, .32f + Random.value * .35f,
                    .025f + Random.value * .045f, 0, tint, true, point);
            }
        }

        static Color Lighten(Color c, float amount) { return Color.Lerp(c, Color.white, amount); }
        void Steam(Vector2 centre, int count, float scale)
        {
            Color vapour = Color.Lerp(tint, new Color(.95f, .89f, .94f), .86f); vapour.a = .24f;
            for (int i = 0; i < count; i++)
                Emit(centre + Random.insideUnitCircle * .15f * scale,
                    new Vector2(Random.Range(-.12f, .12f), Random.Range(.16f, .4f)) * scale,
                    .65f + Random.value * .65f, (.12f + Random.value * .13f) * scale,
                    1, vapour, false, centre);
        }

        void Emit(Vector2 point, Vector2 velocity, float life, float size, int cell, Color colour, bool inward, Vector2 target)
        {
            // Circular replacement makes heavy repeated damage cheap and deterministic in memory.
            int index = cursor; cursor = (cursor + 1) % ParticleBudget;
            particles[index] = new Particle
            {
                Active = true, Position = point, Origin = point, Velocity = velocity, Life = life,
                Size = size, Cell = cell, Colour = colour, Inward = inward, Target = target,
                Rotation = Random.value * Mathf.PI * 2f, Spin = Random.Range(-2f, 2f), Seed = Random.value * 10f
            };
        }

        void LateUpdate()
        {
            if (!mesh || !person) return;
            if ((Global.main && Global.main.Paused) || person.GetComponent<TimePrison>() || RealityRift.Active ||
                (SingularityWorldFreeze.Active && !SingularityWorldFreeze.Current.IsExempt(person.transform)))
            {
                if (matterAudio && matterAudio.isPlaying) { matterAudio.Pause(); audioPaused = true; }
                tremorCount = 0; return;
            }
            if (audioPaused) { if (matterAudio) matterAudio.UnPause(); audioPaused = false; }
            float dt = Time.deltaTime; if (dt <= 0f) { tremorCount = 0; return; }
            clock += dt;
            vertices.Clear(); uv.Clear(); colours.Clear(); triangles.Clear();
            for (int i = 0; i < tremorCount; i++) DrawTremor(tremors[i]);
            for (int i = 0; i < ParticleBudget; i++)
            {
                Particle p = particles[i]; if (!p.Active) continue;
                p.Age += dt;
                float t = p.Age / p.Life;
                if (t >= 1f) { p.Active = false; particles[i] = p; continue; }
                if (p.Inward)
                {
                    Vector2 path = p.Target - p.Origin, normal = new Vector2(-path.y, path.x).normalized;
                    p.Position = Vector2.Lerp(p.Origin, p.Target, t * t * (3f - 2f * t))
                        + normal * Mathf.Sin(t * Mathf.PI) * Mathf.Sin(p.Seed + t * 3f) * .13f;
                }
                else
                {
                    p.Position += p.Velocity * dt;
                    if (p.Cell == 1) p.Velocity += Vector2.up * dt * .055f;
                    else p.Velocity *= Mathf.Exp(-dt * 3f);
                }
                float fade = Mathf.Min(1f, t * 8f) * Mathf.Clamp01((1f - t) * (p.Cell == 1 ? 1.8f : 6f));
                Color colour = p.Colour; colour.a *= fade;
                float extent = p.Size * (p.Cell == 1 ? .7f + t * 1.7f : 1f - t * .42f);
                float squash = p.Cell == 1 ? 1.12f : 1f + Mathf.Sin(t * Mathf.PI * 2f + p.Seed) * .22f;
                Quad(p.Position, new Vector2(extent * squash, extent / squash), p.Rotation + p.Age * p.Spin, p.Cell, colour);
                particles[i] = p;
            }
            tremorCount = 0;
            mesh.Clear();
            renderer.enabled = vertices.Count > 0;
            if (!renderer.enabled) return;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0, true);
        }

        void DrawTremor(Tremor tremor)
        {
            if (tremor.Strength <= .01f) return;
            float pulse = Mathf.Sin(clock * 12.3f + tremor.Seed);
            Vector2 radius = new Vector2(tremor.Size * (.24f + pulse * .018f), tremor.Size * (.2f - pulse * .012f));
            Color colour = tint; colour.a = tremor.Strength * .38f;
            Quad(tremor.Point, radius, tremor.Seed, 0, colour);
        }

        void Vertex(Vector2 world, Vector2 texture, Color colour)
        {
            vertices.Add(transform.InverseTransformPoint(new Vector3(world.x, world.y, -.04f)));
            uv.Add(texture); colours.Add(colour);
        }
        void Indices(int start)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        void Quad(Vector2 position, Vector2 radius, float angle, int cell, Color colour)
        {
            int start = vertices.Count;
            Vector2 x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius.x;
            Vector2 y = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * radius.y;
            // Half texel inset prevents neighbouring atlas cells bleeding into the rubber rim.
            float u = cell % 2 * .5f + .004f, v = cell / 2 * .5f + .004f, span = .492f;
            Vertex(position - x - y, new Vector2(u, v), colour);
            Vertex(position + x - y, new Vector2(u + span, v), colour);
            Vertex(position + x + y, new Vector2(u + span, v + span), colour);
            Vertex(position - x + y, new Vector2(u, v + span), colour);
            Indices(start);
        }
        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (!registered) return;
            registered = false;
            users = Mathf.Max(0, users - 1);
            if (users != 0) return;
            if (material) Destroy(material);
            if (atlas) Destroy(atlas);
            if (gatherSound) Destroy(gatherSound);
            if (fuseSound) Destroy(fuseSound);
            if (woundSound) Destroy(woundSound);
            if (pullSound) Destroy(pullSound);
            material = null; atlas = null;
            gatherSound = null; fuseSound = null; woundSound = null; pullSound = null;
        }
    }
}
