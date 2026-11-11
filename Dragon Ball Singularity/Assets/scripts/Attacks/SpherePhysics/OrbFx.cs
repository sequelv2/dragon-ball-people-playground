using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace DBPU
{
    // How one sphere technique looks: its colours and how its light behaves.
    public sealed class OrbStyle
    {
        public Color Hot = Color.white, Mid = Color.white, Deep = Color.gray, Rim = Color.white, Smoke = new Color(.5f, .47f, .44f);
        public float RimAmount = 1f, Core = 4.5f, CoreWidth = .34f, CoreX, CoreY = -.08f, Level = 2f, Strokes, Lines, Rings, Mottle, Veil, Wrap, Flares, Bolts, Scale = 1.4f, Flow = 1f, Twist = .07f, Corona = 1f, Tongues, Reach = .6f, SmokeAmount = .55f;

        public BeamStyle Beam { get { return new BeamStyle { Hot = Hot, Mid = Mid, Deep = Deep, Radius = .52f, Spiral = 0f, Helix = 1f }; } }

        public static OrbStyle Tinted(Color tint)
        {
            tint.a = 1f;
            return new OrbStyle { Hot = Color.Lerp(tint, Color.white, .78f), Mid = tint, Deep = tint * .55f, Rim = Color.Lerp(tint, Color.white, .45f), Smoke = Color.Lerp(new Color(.5f, .47f, .44f), tint, .35f) };
        }

        static readonly Dictionary<string, OrbStyle> looks = new Dictionary<string, OrbStyle>
        {
            { "Death Ball", new OrbStyle { Hot = new Color(1f, .92f, .98f), Mid = new Color(.96f, .3f, .72f), Deep = new Color(.42f, .05f, .5f), Rim = new Color(1f, .78f, .96f), Core = 5.5f, CoreWidth = .33f, CoreX = .08f, CoreY = .14f, Level = 2.1f, Mottle = .9f, Flares = .7f, Bolts = 10f, Scale = 1.3f, Flow = 1f, Twist = .02f, Corona = 1.05f, Tongues = .5f, Reach = .7f, Smoke = new Color(.42f, .22f, .46f) } },
            { "Supernova", new OrbStyle { Hot = new Color(1f, .97f, .78f), Mid = new Color(1f, .66f, .14f), Deep = new Color(.95f, .38f, .05f), Rim = new Color(1f, .94f, .6f), Core = 6.5f, CoreWidth = .38f, CoreX = .1f, CoreY = .16f, Level = 2.4f, Mottle = .9f, Flares = .9f, Bolts = 12f, Scale = 1.1f, Flow = .6f, Twist = .01f, Corona = 1f, Tongues = .8f, Reach = .8f, Smoke = new Color(.5f, .34f, .24f) } },
            { "Beerus Ball", new OrbStyle { Hot = new Color(1f, .96f, .72f), Mid = new Color(1f, .62f, .1f), Deep = new Color(.92f, .34f, .05f), Rim = new Color(1f, .93f, .55f), Core = 6f, CoreWidth = .36f, CoreX = .1f, CoreY = .16f, Level = 2.2f, Mottle = 1f, Flares = 1f, Bolts = 14f, Scale = 1.25f, Flow = .7f, Twist = .012f, Corona = 1f, Tongues = .8f, Reach = .85f, Smoke = new Color(.42f, .24f, .42f) } },
            { "Gigantic Crusher", new OrbStyle { Hot = new Color(.85f, 1f, .86f), Mid = new Color(.13f, .85f, .58f), Deep = new Color(.01f, .3f, .26f), Rim = new Color(.55f, 1f, .92f), RimAmount = .45f, Core = 2.6f, CoreWidth = .42f, CoreY = .22f, Level = 1.7f, Veil = 1f, Wrap = 1f, Scale = 1.5f, Flow = .9f, Twist = .03f, Corona = .8f, Reach = 1f, Smoke = new Color(.24f, .38f, .32f) } },
            { "Spirit Bomb", new OrbStyle { Hot = Color.white, Mid = new Color(.24f, .92f, .95f), Deep = new Color(.07f, .2f, .85f), Rim = new Color(.9f, 1f, 1f), Core = 6f, CoreWidth = .42f, Level = 2.4f, Strokes = 1f, Lines = .8f, Rings = 1f, Scale = 1.4f, Flow = .8f, Twist = .11f, Corona = 1.1f, Reach = .7f, Smoke = new Color(.45f, .6f, .85f) } },
            { "Hakai Ball", new OrbStyle { Hot = new Color(1f, .78f, 1f), Mid = new Color(.62f, .2f, .92f), Deep = new Color(.2f, .03f, .42f), Rim = new Color(1f, .62f, 1f), Smoke = new Color(.3f, .16f, .4f) } },
            { "Stardust Breaker", new OrbStyle { Hot = Color.white, Mid = new Color(.6f, .86f, 1f), Deep = new Color(.26f, .2f, .82f), Rim = new Color(1f, .92f, 1f), Smoke = new Color(.5f, .55f, .8f) } },
            { "Cataclysmic Orb", new OrbStyle { Hot = new Color(1f, .88f, 1f), Mid = new Color(.58f, .22f, 1f), Deep = new Color(.16f, .02f, .46f), Rim = new Color(.9f, .7f, 1f), Core = 4f, CoreWidth = .3f, Level = 1.8f, Strokes = 1f, Lines = 1f, Rings = .8f, Bolts = 4f, Scale = 1.3f, Flow = 1.8f, Twist = .3f, Corona = 1.1f, Reach = .8f, Smoke = new Color(.36f, .2f, .48f) } },
            { "Final Explosion", new OrbStyle { Hot = new Color(1f, 1f, .88f), Mid = new Color(1f, .82f, .26f), Deep = new Color(.96f, .46f, .06f), Rim = new Color(1f, .97f, .7f), Core = 5f, CoreWidth = .7f, Level = 2.4f, Mottle = .6f, Scale = 1.6f, Flow = 1.2f, Twist = .02f, Corona = .9f, Tongues = .4f, Reach = .2f, Smoke = new Color(.5f, .4f, .28f), SmokeAmount = .3f } },
        };
        static OrbStyle fallback;

        public static OrbStyle Of(string technique)
        {
            OrbStyle look;
            if (technique == "Super Spirit Bomb") technique = "Spirit Bomb";
            if (technique != null && looks.TryGetValue(technique, out look)) return look;
            return fallback ?? (fallback = Tinted(new Color(1f, .85f, .3f)));
        }
    }

    public static class OrbFxAssets
    {
        static bool attempted; static AssetBundle bundle; static Material orb, burst, shot, ring;
        public static bool Ready { get { Load(); return orb && burst && shot && ring; } }
        public static Material Orb { get { Load(); return orb; } }
        public static Material Burst { get { Load(); return burst; } }
        public static Material Shot { get { Load(); return shot; } }
        public static Material Ring { get { Load(); return ring; } }

        static void Load()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                bundle = AssetBundle.LoadFromFile(Path.Combine(Singularity.ModDirectory, "Assets/VFX/OrbFx/orbfx"));
                if (!bundle) throw new IOException("Bundle is missing");
                orb = Take("kiorb"); burst = Take("kiburst"); shot = Take("kishot"); ring = Take("kiring");
            }
            catch (Exception ex)
            {
                orb = burst = shot = ring = null;
                Debug.LogWarning("[Singularity] Sphere shaders unavailable: " + ex.Message);
            }
        }

        static Material Take(string name)
        {
            var value = bundle.LoadAsset<Material>("assets/orbfx/" + name + ".mat");
            if (!value || !value.shader || !value.shader.isSupported) throw new IOException(name + " is unsupported");
            return value;
        }
    }

    // One big sphere on the screen: the body and its corona on a single quad.
    public sealed class OrbView
    {
        readonly GameObject go; readonly MeshRenderer view; readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float seed = Random.value * 40f;
        float clock;
        public OrbStyle Look;
        public float Opacity = 1f, Heat, Stream;
        public Vector2 Move, PressDirection;
        public float Press;

        public OrbView(OrbStyle look, string layer = "Background", int order = -18)
        {
            Look = look;
            go = new GameObject("DBPU ki sphere");
            go.AddComponent<MeshFilter>().sharedMesh = BeamFxAssets.Quad;
            view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = OrbFxAssets.Orb;
            view.sortingLayerName = layer; view.sortingOrder = order;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            view.enabled = false;
        }

        public void Place(Vector2 at, float radius, float dt)
        {
            if (!go) return;
            clock += dt;
            // The quad grows with the streaming flames, so they never run off its edge.
            float r = Mathf.Max(radius, .001f), size = r * (1f + Look.Reach * (1f + Stream));
            go.transform.position = new Vector3(at.x, at.y, -.4f);
            go.transform.localScale = new Vector3(size, size, 1f);
            view.enabled = Opacity > .002f;
            if (!view.enabled) return;
            block.Clear();
            block.SetColor("_Hot", Look.Hot); block.SetColor("_Mid", Look.Mid); block.SetColor("_Deep", Look.Deep);
            block.SetColor("_Rim", new Color(Look.Rim.r, Look.Rim.g, Look.Rim.b, Look.RimAmount));
            block.SetVector("_Move", new Vector4(Move.x, Move.y, Stream, 0f));
            block.SetVector("_Contact", new Vector4(PressDirection.x, PressDirection.y, Press, 0f));
            block.SetFloat("_Clock", clock); block.SetFloat("_Seed", seed); block.SetFloat("_Body", r / size); block.SetFloat("_Reach", Look.Reach);
            block.SetFloat("_Opacity", Opacity); block.SetFloat("_Hdr", BeamFxAssets.Hdr); block.SetFloat("_Heat", Heat); block.SetFloat("_Px", OrbFx.PixelSize / r);
            block.SetVector("_CoreAt", new Vector4(Look.CoreX, Look.CoreY, 0f, 0f));
            block.SetFloat("_Core", Look.Core); block.SetFloat("_CoreWidth", Look.CoreWidth); block.SetFloat("_Level", Look.Level);
            block.SetFloat("_Mottle", Look.Mottle); block.SetFloat("_Veil", Look.Veil); block.SetFloat("_Wrap", Look.Wrap); block.SetFloat("_Flares", Look.Flares);
            block.SetFloat("_Strokes", Look.Strokes); block.SetFloat("_Lines", Look.Lines); block.SetFloat("_Rings", Look.Rings);
            block.SetFloat("_Scale", Look.Scale); block.SetFloat("_Flow", Look.Flow); block.SetFloat("_Twist", Look.Twist);
            block.SetFloat("_Corona", Look.Corona); block.SetFloat("_Tongues", Look.Tongues);
            view.SetPropertyBlock(block);
        }

        public void Hide() { if (view) view.enabled = false; }
        public void Destroy() { if (go) UnityEngine.Object.Destroy(go); }
    }

    // Every small blast in the world, drawn in one call.
    [DefaultExecutionOrder(330)]
    public sealed class OrbShots : MonoBehaviour
    {
        const int Capacity = 640;
        sealed class Shot { public Transform T; public Rigidbody2D Body; public Color Tint; public float Size, TailSeconds, TailMax, Seed, Opacity = 1f; public Vector2 Last, Direction = Vector2.right; public bool Seen; }
        static OrbShots instance;
        readonly Dictionary<Transform, Shot> shots = new Dictionary<Transform, Shot>(Capacity);
        readonly List<Transform> dead = new List<Transform>();
        readonly Vector3[] vertices = new Vector3[Capacity * 4];
        readonly Vector2[] uvs = new Vector2[Capacity * 4], data = new Vector2[Capacity * 4];
        readonly Color[] colors = new Color[Capacity * 4];
        Mesh mesh; MeshRenderer view; MaterialPropertyBlock block; int drawn;

        static OrbShots Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("DBPU ki shots");
                instance = go.AddComponent<OrbShots>();
                instance.Build(go);
                return instance;
            }
        }

        void Build(GameObject go)
        {
            mesh = new Mesh { name = "DBPU ki shots" }; mesh.MarkDynamic();
            var triangles = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices; mesh.uv = uvs; mesh.uv2 = data; mesh.colors = colors; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 60000f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = OrbFxAssets.Shot;
            view.sortingLayerName = "Foreground"; view.sortingOrder = 8;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        public static void Add(Transform t, Color tint, float size, float tailSeconds = .05f, float tailMax = 9f)
        {
            if (!t || !OrbFxAssets.Ready) return;
            var all = Instance.shots;
            Shot shot;
            if (!all.TryGetValue(t, out shot))
            {
                if (all.Count >= Capacity) return;
                shot = new Shot { T = t, Body = t.GetComponent<Rigidbody2D>(), Seed = Random.value * 20f };
                all[t] = shot;
            }
            tint.a = 1f;
            shot.Tint = tint; shot.Size = size; shot.TailSeconds = tailSeconds; shot.TailMax = tailMax;
        }

        public static void Remove(Transform t) { if (instance && t) instance.shots.Remove(t); }

        void LateUpdate()
        {
            dead.Clear();
            int n = 0;
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            foreach (var pair in shots)
            {
                var s = pair.Value;
                if (!pair.Key) { dead.Add(pair.Key); continue; }
                Vector2 at = pair.Key.position;
                Vector2 velocity = s.Body && s.Body.simulated && s.Body.bodyType != RigidbodyType2D.Static ? s.Body.velocity : Vector2.zero;
                if (velocity.sqrMagnitude < .01f && s.Seen) velocity = (at - s.Last) / dt;
                s.Last = at; s.Seen = true;
                float speed = velocity.magnitude;
                if (speed > .5f) s.Direction = velocity / speed;
                float tail = Mathf.Min(s.TailMax, speed * s.TailSeconds) / s.Size;
                Vector2 side = new Vector2(-s.Direction.y, s.Direction.x) * s.Size * 1.7f;
                Vector2 front = s.Direction * s.Size * 1.7f, back = -s.Direction * s.Size * (tail + 1.7f);
                int v = n * 4;
                vertices[v] = at + back - side; vertices[v + 1] = at + back + side; vertices[v + 2] = at + front + side; vertices[v + 3] = at + front - side;
                float rear = -(tail + 1.7f);
                uvs[v] = new Vector2(rear, -1.7f); uvs[v + 1] = new Vector2(rear, 1.7f); uvs[v + 2] = new Vector2(1.7f, 1.7f); uvs[v + 3] = new Vector2(1.7f, -1.7f);
                Color c = s.Tint; c.a = s.Opacity;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;
                data[v] = data[v + 1] = data[v + 2] = data[v + 3] = new Vector2(s.Seed, Mathf.Max(tail, .01f));
                n++;
            }
            foreach (var key in dead) shots.Remove(key);
            for (int i = n * 4; i < drawn * 4; i++) vertices[i] = Vector3.zero;
            drawn = n;
            view.enabled = n > 0;
            if (n == 0) return;
            mesh.vertices = vertices; mesh.uv = uvs; mesh.uv2 = data; mesh.colors = colors;
            block.SetFloat("_Hdr", BeamFxAssets.Hdr);
            view.SetPropertyBlock(block);
        }
    }

    // The fireballs: a pool of quads playing the soft flipbook baked in Cycles.
    [DefaultExecutionOrder(331)]
    public sealed class OrbBursts : MonoBehaviour
    {
        const int Capacity = 56;
        sealed class Burst
        {
            public GameObject Go; public MeshRenderer View; public MaterialPropertyBlock Block;
            public Vector2 At; public float Radius, Life, Age, Delay, Smoke, Rise; public Color Hot, Mid, Deep, SmokeColor; public bool Live;
        }
        static OrbBursts instance;
        readonly List<Burst> pool = new List<Burst>(Capacity);

        public static void Play(Vector2 at, float radius, Color hot, Color mid, Color deep, Color smokeColor, float seconds, float smoke, float delay = 0f, float rise = 0f)
        {
            if (!OrbFxAssets.Ready) return;
            if (!instance)
            {
                var host = new GameObject("DBPU ki bursts");
                instance = host.AddComponent<OrbBursts>();
            }
            var b = instance.Take(radius);
            if (b == null) return;
            b.At = at; b.Radius = radius; b.Life = Mathf.Max(.1f, seconds); b.Age = 0f; b.Delay = delay; b.Smoke = smoke; b.Rise = rise;
            b.Hot = hot; b.Mid = mid; b.Deep = deep; b.SmokeColor = smokeColor; b.Live = true;
            float flip = Random.value < .5f ? -1f : 1f;
            b.Go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));
            b.Go.transform.localScale = new Vector3(radius * flip, radius, 1f);
            b.Go.transform.position = new Vector3(at.x, at.y, -.45f);
            b.View.enabled = false;
        }

        Burst Take(float radius)
        {
            Burst weakest = null;
            foreach (var b in pool)
            {
                if (!b.Live) return b;
                if (weakest == null || b.Radius < weakest.Radius || (b.Radius == weakest.Radius && b.Age / b.Life > weakest.Age / weakest.Life)) weakest = b;
            }
            if (pool.Count < Capacity)
            {
                var go = new GameObject("DBPU ki burst");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = BeamFxAssets.Quad;
                var view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = OrbFxAssets.Burst;
                view.sortingLayerName = "Bubbles"; view.sortingOrder = 90;
                view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
                var made = new Burst { Go = go, View = view, Block = new MaterialPropertyBlock() };
                pool.Add(made);
                return made;
            }
            return weakest != null && weakest.Radius <= radius ? weakest : null;
        }

        void LateUpdate()
        {
            // Real time, so a burst still blooms during the hitstop that goes with it.
            float dt = DbpuFx.Paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, .1f);
            foreach (var b in pool)
            {
                if (!b.Live) continue;
                if (b.Delay > 0f) { b.Delay -= dt; continue; }
                b.Age += dt;
                if (b.Age >= b.Life) { b.Live = false; b.View.enabled = false; continue; }
                float t = b.Age / b.Life;
                b.View.enabled = true;
                if (b.Rise > 0f) b.Go.transform.position = new Vector3(b.At.x, b.At.y + b.Rise * t, -.45f);
                b.Block.Clear();
                b.Block.SetColor("_Hot", b.Hot); b.Block.SetColor("_Mid", b.Mid); b.Block.SetColor("_Deep", b.Deep); b.Block.SetColor("_Smoke", b.SmokeColor);
                // Nearly all of the growth happens in the first moments, then it hangs and rolls.
                float grown = 1f - (1f - t) * (1f - t);
                b.Go.transform.localScale = new Vector3(b.Radius * Mathf.Sign(b.Go.transform.localScale.x) * (.82f + grown * .24f), b.Radius * (.82f + grown * .24f), 1f);
                b.Block.SetFloat("_Frame", 63f * Mathf.Pow(t, .62f)); b.Block.SetFloat("_Age", t); b.Block.SetFloat("_SmokeAmount", b.Smoke); b.Block.SetFloat("_Hdr", BeamFxAssets.Hdr);
                b.Block.SetFloat("_Opacity", Mathf.Clamp01(b.Age / .03f) * (1f - DbpuFx.Smooth(.72f, 1f, t)));
                b.View.SetPropertyBlock(b.Block);
            }
        }
    }

    public static class OrbFx
    {
        public static bool Ready { get { return OrbFxAssets.Ready && BeamFxAssets.Ready; } }
        // Whether the new look is in charge of this attack.
        public static bool Active { get { return SpherePhysics.On && Ready; } }

        static Color Half(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        // How much of the world one pixel covers right now.
        public static float PixelSize
        {
            get
            {
                var cam = Global.main ? Global.main.camera : Camera.main;
                return cam ? cam.orthographicSize * 2f / Mathf.Max(1f, Screen.height) : .02f;
            }
        }

        // Prefab effects kept only for their sound or logic.
        public static void Mute(GameObject effect)
        {
            if (!effect) return;
            foreach (var r in effect.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        public static void Muzzle(Vector2 at, Vector2 direction, Color tint, float size)
        {
            if (!Ready) return;
            tint.a = 1f;
            Color hot = Color.Lerp(tint, Color.white, .75f);
            var crown = new BeamQuad("DBPU ki muzzle", BeamQuad.Crown, "Foreground", 7);
            crown.Mid = tint; crown.Hot = hot; crown.Deep = Half(tint, .6f); crown.Dir = direction; crown.Spread = .8f; crown.Speed = 3f;
            BeamPhysics.Fade(crown, at, size * 1.9f, .1f, .3f);
            var sparks = BeamParticles.Sparks;
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = DbpuFx.Rotate(direction, Random.Range(-.6f, .6f));
                sparks.Emit(at, d * Random.Range(6f, 16f), Random.Range(.08f, .2f), Random.Range(.03f, .07f), Color.white, DbpuFx.A(tint, 0f), 2f, 0f, 0f, true);
            }
        }

        // A soft pressure front racing out, behind or in front of whatever is at the centre.
        public static void Ring(Vector2 at, float radius, OrbStyle look, float seconds, float dome = 0f, string layer = "Background", int order = -24, float from = .25f)
        {
            if (!Ready) return;
            var go = new GameObject("DBPU pressure ring");
            go.AddComponent<MeshFilter>().sharedMesh = BeamFxAssets.Quad;
            var view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = OrbFxAssets.Ring;
            view.sortingLayerName = layer; view.sortingOrder = order;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            go.transform.position = new Vector3(at.x, at.y, -.42f);
            go.transform.localScale = new Vector3(radius, radius, 1f);
            BeamFxRunner.Run(RingRoutine(go, view, look, seconds, dome, from));
        }

        static IEnumerator RingRoutine(GameObject go, MeshRenderer view, OrbStyle look, float seconds, float dome, float from)
        {
            var block = new MaterialPropertyBlock();
            float age = 0f, seed = Random.value * 7f, size = go.transform.localScale.x;
            while (age < seconds && go)
            {
                float t = age / seconds, grown = 1f - (1f - t) * (1f - t);
                block.Clear();
                block.SetColor("_Hot", look.Hot); block.SetColor("_Mid", look.Mid);
                block.SetFloat("_Radius", Mathf.Lerp(from, .97f, grown));
                block.SetFloat("_Thick", Mathf.Max(PixelSize * 1.6f, size * .0045f) / size);
                block.SetFloat("_Dome", dome); block.SetFloat("_Seed", seed); block.SetFloat("_Hdr", BeamFxAssets.Hdr);
                block.SetFloat("_Opacity", 1f - t * t);
                view.SetPropertyBlock(block);
                yield return null;
                age += Time.unscaledDeltaTime;
            }
            if (go) UnityEngine.Object.Destroy(go);
        }

        // Rock torn loose under a big sphere, drifting up into its light.
        static DbpuParticles rocks; static float rockDebt;
        public static void Lift(Vector2 at, float radius, OrbStyle look, float dt)
        {
            Vector2 ground, normal;
            if (radius < 2f || !Ready || !BeamGround.Find(at, radius + 5f, null, out ground, out normal)) return;
            if (!rocks) rocks = DbpuParticles.Create("DBPU lifted rock", DbpuFx.Square, 240, 41);
            rockDebt += dt * Mathf.Clamp(radius * 2.4f, 5f, 34f);
            int count = (int)rockDebt; rockDebt -= count;
            float size = Mathf.Clamp(radius * .12f, 1f, 3f);
            for (int i = 0; i < count; i++)
            {
                Vector2 from = ground + new Vector2(Random.Range(-1f, 1f) * radius * 1.1f, Random.Range(0f, .4f));
                Color c = Color.Lerp(look.Deep, look.Mid, Random.value * .6f);
                rocks.Emit(from, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(2.5f, 5f + radius * .25f)), Random.Range(.8f, 1.8f), Random.Range(.06f, .22f) * size, c, DbpuFx.A(c, 0f), .6f, 0f, Random.Range(-180f, 180f));
            }
        }

        // A blast going off: fire, a thin shock front and sparks, cheap enough for a barrage.
        public static void SmallBurst(Vector2 at, float radius, Color tint)
        {
            if (!Ready) return;
            tint.a = 1f;
            Color hot = Color.Lerp(tint, Color.white, .78f);
            OrbBursts.Play(at, radius * 1.3f, hot, tint, Half(tint, .5f), Color.Lerp(new Color(.5f, .47f, .44f), tint, .3f), Mathf.Clamp(.34f + radius * .05f, .34f, .9f), .34f);
            var sparks = BeamParticles.Sparks;
            for (int i = 0; i < 5 + (int)Mathf.Min(radius * 2f, 12f); i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                sparks.Emit(at, d * Random.Range(3f, 7f + radius * 3.4f), Random.Range(.1f, .32f), Random.Range(.03f, .08f + radius * .012f), Color.white, DbpuFx.A(tint, 0f), 2.2f, 4f, 0f, true);
            }
            if (radius > 1.8f)
            {
                var ring = new BeamQuad("DBPU ki shock", BeamQuad.Arc, "Bubbles", 88);
                ring.Mid = tint; ring.Hot = hot; ring.Dir = Random.insideUnitCircle.normalized; ring.Spread = 1f; ring.Power = .25f;
                BeamPhysics.Fade(ring, at, Mathf.Min(radius * 1.5f, 5f), .22f, .05f, true);
            }
            DbpuFx.Shake(at, Mathf.Min(.2f + radius * .1f, 1f));
        }

        // A sphere going off, sized by the radius of what it reaches.
        public static void Explosion(Vector2 at, float radius, OrbStyle look, Vector2 normal)
        {
            if (!Ready) return;
            var style = look.Beam;
            float scale = Mathf.Clamp(radius * .13f, .5f, 4f);
            OrbBursts.Play(at, radius * 1.32f, look.Hot, look.Mid, look.Deep, look.Smoke, Mathf.Clamp(1f + radius * .03f, 1f, 3.4f), look.SmokeAmount);
            if (radius > 5f)
            {
                // Lobes breaking out of the main fireball a moment after it, then rolling up.
                int lobes = radius > 20f ? 4 : 3;
                for (int i = 0; i < lobes; i++)
                {
                    Vector2 off = DbpuFx.Rotate(Vector2.right, (i + Random.value) / lobes * 6.283f) * radius * Random.Range(.25f, .45f);
                    OrbBursts.Play(at + off, radius * Random.Range(.55f, .75f), look.Hot, look.Mid, look.Deep, look.Smoke, Mathf.Clamp(.8f + radius * .022f, .8f, 2.6f), look.SmokeAmount, Random.Range(.01f, .09f), radius * .1f);
                }
            }
            BeamContact.Burst(at, normal.sqrMagnitude > .01f ? normal : Vector2.up, style, style, scale);
            var shafts = new BeamQuad("DBPU sphere blast shafts", BeamQuad.Rays, "Background", -29);
            shafts.Style(style); shafts.Alt = look.Deep; shafts.Dir = Vector2.up; shafts.Speed = .8f;
            BeamPhysics.Fade(shafts, at, radius * 3.4f, Mathf.Clamp(.6f + radius * .02f, .6f, 1.8f), .25f);
            Ring(at, radius * 2.4f, look, Mathf.Clamp(.45f + radius * .012f, .45f, 1.2f), .55f, "Bubbles", 88, .12f);
            Vector2 ground, n;
            if (BeamGround.Find(at + Vector2.up * .5f, radius * 1.1f + 2f, null, out ground, out n))
            {
                BeamGround.Erupt(ground, Mathf.Clamp(radius * .16f, .7f, 5f), look.Mid, n);
                int rocks = Mathf.Clamp((int)(radius * .5f), 0, 16);
                for (int i = 0; i < rocks; i++)
                {
                    float x = Random.Range(-1f, 1f) * radius * .9f;
                    Vector2 g2, n2;
                    if (BeamGround.Find(ground + new Vector2(x, radius * .3f + 2f), radius * .6f + 5f, null, out g2, out n2))
                        BeamSlabs.Raise(g2, Random.Range(.5f, 1.4f) * Mathf.Clamp(radius * .1f, .8f, 3.4f), Mathf.Sign(x) * Random.Range(4f, 30f), look.Mid);
                }
            }
            if (radius > 3f)
            {
                BeamFxRunner.Run(SkyFlash(at, look, Mathf.Clamp(radius * .06f, .4f, 1f), Mathf.Clamp(.6f + radius * .02f, .6f, 1.6f)));
                SingularityTime.Hitstop(Mathf.Clamp(.07f + radius * .004f, .07f, .16f), .05f);
            }
            DbpuFx.Shake(at, Mathf.Min(1f + radius * .12f, 4f));
            DbpuFx.Sound(at, ImpactFramesModule.SfxClash, 1f);
            if (radius > 6f) DbpuFx.Sound(at, "Assets/SFX/Techniques/HakaiBall/Shatter.wav", .8f);
        }

        static IEnumerator SkyFlash(Vector2 at, OrbStyle look, float strength, float seconds)
        {
            float age = 0f;
            while (age < seconds)
            {
                BeamScene.Request(at, Vector2.up, look.Mid, look.Mid, strength * (1f - age / seconds));
                yield return null;
                age += Time.unscaledDeltaTime;
            }
        }

        // Falling in on itself and winking out.
        public static void Implode(Vector2 at, float radius, OrbStyle look)
        {
            if (!Ready) return;
            var style = look.Beam;
            float size = Mathf.Clamp(radius, 1f, 9f);
            var sparks = BeamParticles.Sparks; var stars = BeamParticles.Stars;
            for (int i = 0; i < 30; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                float life = Random.Range(.14f, .3f);
                sparks.Emit(at + d * radius, -d * (radius / life), life, Random.Range(.05f, .14f), Color.white, DbpuFx.A(look.Mid, .2f), 0f, 0f, 0f, true);
            }
            for (int i = 0; i < 10; i++)
                stars.Emit(at + Random.insideUnitCircle * size, Random.insideUnitCircle * 1.2f, Random.Range(.3f, .8f), Random.Range(.16f, .4f), Color.white, DbpuFx.A(look.Mid, 0f), .4f, 0f, 0f, false, -1f, .9f);
            BeamContact.Burst(at, Vector2.up, style, style, Mathf.Clamp(size * .35f, .6f, 2.4f));
        }
    }
}
