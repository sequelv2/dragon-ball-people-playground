using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace DBPU
{
    // Ki Fist presentation modelled on the reference: a hot white orb on each fist/foot,
    // a thin comet trail while swinging, and an anime impact (star flash, double ring,
    // radial speed lines, diamond debris). Colours follow the fighter's current aura.
    [DefaultExecutionOrder(285)]
    public sealed class KiFistGlowFx : MonoBehaviour
    {
        const int Samples = 30;
        const float TrailLife = .30f;
        KiFistHand owner;
        SpriteRenderer halo, body, core, flare;
        readonly LineRenderer[] arcs = new LineRenderer[3];
        Mesh trailMesh; MeshRenderer trail;
        readonly Vector3[] points = new Vector3[Samples];
        readonly float[] born = new float[Samples];
        readonly Vector3[] vertices = new Vector3[Samples * 2];
        readonly Color[] colors = new Color[Samples * 2];
        DbpuParticles sparks;
        float clock, shown, arcClock, emitDebt; int count; bool showing;
        Vector2 lastPoint; bool havePoint;

        public static KiFistGlowFx Create(KiFistHand owner)
        {
            if (!owner) return null;
            var go = new GameObject("DBPU Ki Fist anime glow");
            go.transform.SetParent(owner.transform, false);
            var fx = go.AddComponent<KiFistGlowFx>(); fx.owner = owner; fx.Build();
            return fx;
        }
        void Build()
        {
            halo = DbpuFx.MakeSprite(transform, "Ki halo", DbpuFx.Glow, 36);
            flare = DbpuFx.MakeSprite(transform, "Overcharge star", DbpuFx.Star, 37);
            body = DbpuFx.MakeSprite(transform, "Ki body", DbpuFx.Glow, 38);
            core = DbpuFx.MakeSprite(transform, "Ki core", DbpuFx.Glow, 39, true);
            for (int i = 0; i < arcs.Length; i++) arcs[i] = DbpuFx.Line(transform, "Overcharge crackle " + i, 40);
            var t = new GameObject("Ki swing trail"); t.transform.SetParent(transform, false);
            trailMesh = new Mesh { name = "Ki Fist comet trail" }; trailMesh.MarkDynamic();
            var uv = new Vector2[Samples * 2]; var tris = new int[(Samples - 1) * 6];
            for (int i = 0; i < Samples; i++)
            {
                float u = i / (float)(Samples - 1); uv[i * 2] = new Vector2(u, 0); uv[i * 2 + 1] = new Vector2(u, 1);
                if (i == Samples - 1) continue; int v = i * 2, k = i * 6;
                tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2; tris[k + 3] = v + 1; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
            }
            trailMesh.vertices = vertices; trailMesh.uv = uv; trailMesh.colors = colors; trailMesh.triangles = tris;
            t.AddComponent<MeshFilter>().sharedMesh = trailMesh;
            trail = t.AddComponent<MeshRenderer>(); trail.sharedMaterial = DbpuFx.MeshMaterial(DbpuFx.Ribbon);
            trail.sortingLayerName = "Top"; trail.sortingOrder = 35;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            sparks = DbpuParticles.Create("DBPU Ki Fist sparks", DbpuFx.Streak, 96, 41);
            sparks.StretchFactor = .035f;
        }
        public void Show() { showing = true; if (!gameObject.activeSelf) gameObject.SetActive(true); }
        public void Hide() { showing = false; }

        void LateUpdate()
        {
            if (!owner) { Destroy(gameObject); return; }
            float dt = DbpuFx.Dt; clock += dt;
            bool valid = owner.Equipped && KiCombatMath.ValidStrikeLimb(owner.Limb, owner.Person);
            shown = Mathf.MoveTowards(shown, showing && valid ? 1f : 0f, (dt > 0 ? dt : 0f) * 7f);
            if (!owner.Limb) return;
            Vector2 point = KiCombatMath.StrikePoint(owner.Limb);
            transform.position = new Vector3(point.x, point.y, -.3f); transform.rotation = Quaternion.identity;
            DbpuFx.WorldScale(transform, Vector3.one);
            var rb = owner.Limb.GetComponent<Rigidbody2D>();
            Vector2 velocity = rb ? rb.GetPointVelocity(point) : Vector2.zero;
            if (havePoint && dt > 0f && velocity.sqrMagnitude < .0001f) velocity = (point - lastPoint) / dt;
            lastPoint = point; havePoint = true;
            float speed = velocity.magnitude;
            var sprite = owner.Limb.GetComponent<SpriteRenderer>();
            float limb = sprite ? Mathf.Clamp(Mathf.Min(sprite.bounds.size.x, sprite.bounds.size.y) * 1.6f, .18f, 1.2f) : .3f;
            float charge = owner.Charge01, over = owner.Overcharge01;
            bool armed = owner.IsCharged, charging = owner.IsCharging, overcharged = owner.IsOvercharged;
            float motion = Mathf.Clamp01((speed - 2.5f) / 9f);
            float energy = armed ? 1f : charging ? .55f + .45f * charge : .42f + .45f * motion;
            float size = limb * (.62f + charge * .55f + over * .75f + motion * .15f);
            float pulse = 1f + Mathf.Sin(clock * (charging ? 24f : 13f)) * (charging ? .09f : .045f);
            Color tint = owner.EffectTint;
            float angle = speed > .5f ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0f;
            float stretch = 1f + Mathf.Clamp01((speed - 5f) / 14f) * .9f;

            DbpuFx.Place(halo, point, -.3f, size * 2.6f * pulse * stretch, size * 2.6f * pulse, angle, DbpuFx.A(tint, .34f * energy * shown));
            DbpuFx.Place(body, point, -.31f, size * 1.15f * pulse * Mathf.Lerp(1f, stretch, .7f), size * 1.15f * pulse, angle, DbpuFx.A(Color.Lerp(tint, Color.white, .62f), .86f * energy * shown));
            DbpuFx.Place(core, point, -.32f, size * .48f * pulse, size * .48f * pulse, 0, DbpuFx.A(Color.white, Mathf.Clamp01(.55f + energy * .45f) * shown));
            float star = Mathf.Clamp01(over * 1.2f + (armed ? .45f : 0f) + (charging ? charge * .35f : 0f));
            DbpuFx.Place(flare, point, -.29f, size * 3.4f * (.92f + .08f * Mathf.Sin(clock * 9f)), size * 3.4f, clock * 40f, DbpuFx.A(Color.Lerp(tint, Color.white, .5f), star * .85f * shown));

            UpdateArcs(point, size, tint, (charging || overcharged || armed) && shown > .05f ? Mathf.Max(over, charging ? .45f : .3f) : 0f, dt);
            if (dt > 0f && shown > .05f)
            {
                if (charging)
                {
                    emitDebt += dt * (30f + charge * 50f);
                    while (emitDebt >= 1f)
                    {
                        emitDebt -= 1f;
                        Vector2 dir = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.2832f);
                        float r = size * UnityEngine.Random.Range(2.2f, 3.6f);
                        sparks.Emit(point + dir * r, -dir * r / .22f, .22f, size * .32f, DbpuFx.A(Color.white, .9f), DbpuFx.A(tint, .2f), 0, 0, 0, true);
                    }
                }
                else if (overcharged || armed)
                {
                    emitDebt += dt * 22f;
                    while (emitDebt >= 1f)
                    {
                        emitDebt -= 1f;
                        Vector2 dir = DbpuFx.Rotate(Vector2.right, UnityEngine.Random.value * 6.2832f);
                        sparks.Emit(point + dir * size * .5f, dir * UnityEngine.Random.Range(1.5f, 4f) + velocity * .25f, UnityEngine.Random.Range(.12f, .26f), size * .26f, Color.white, DbpuFx.A(tint, 0f), 3f, 0, 0, true);
                    }
                }
                else emitDebt = 0f;
            }
            UpdateTrail(point, size, tint, speed, energy);
            if (!showing && shown <= 0f && count == 0 && sparks.Count == 0) gameObject.SetActive(false);
        }
        void UpdateArcs(Vector2 point, float size, Color tint, float strength, float dt)
        {
            arcClock -= dt;
            bool refresh = arcClock <= 0f;
            if (refresh) arcClock = .045f;
            for (int a = 0; a < arcs.Length; a++)
            {
                var line = arcs[a];
                if (strength <= .01f) { line.positionCount = 0; continue; }
                if (!refresh && line.positionCount > 0) continue;
                const int n = 7;
                line.positionCount = n;
                float start = UnityEngine.Random.value * 6.2832f, sweep = UnityEngine.Random.Range(.9f, 1.8f);
                for (int i = 0; i < n; i++)
                {
                    float f = i / (float)(n - 1), ang = start + sweep * f;
                    float r = size * (.75f + UnityEngine.Random.Range(-.25f, .45f) * strength + (i == 0 || i == n - 1 ? -.1f : 0f));
                    line.SetPosition(i, new Vector3(point.x + Mathf.Cos(ang) * r, point.y + Mathf.Sin(ang) * r, -.33f));
                }
                line.startWidth = line.endWidth = Mathf.Max(.008f, size * .045f);
                line.startColor = DbpuFx.A(Color.white, .95f * strength * shown); line.endColor = DbpuFx.A(Color.Lerp(tint, Color.white, .5f), .7f * strength * shown);
            }
        }
        void UpdateTrail(Vector2 point, float size, Color tint, float speed, float energy)
        {
            if (shown > .05f && speed > 4.2f)
            {
                if (count > 0 && Vector2.Distance(points[0], point) > 6f) count = 0;
                if (count == 0 || Vector2.Distance(points[0], point) > .02f)
                {
                    for (int i = Samples - 1; i > 0; i--) { points[i] = points[i - 1]; born[i] = born[i - 1]; }
                    points[0] = new Vector3(point.x, point.y, -.28f); born[0] = clock; count = Mathf.Min(Samples, count + 1);
                }
            }
            while (count > 0 && clock - born[count - 1] > TrailLife) count--;
            trail.enabled = count > 1;
            if (!trail.enabled) return;
            float width = size * (.38f + Mathf.Clamp01((speed - 4f) / 12f) * .3f) * (owner.IsOvercharged ? 1.6f : 1f);
            Color head = DbpuFx.A(Color.white, .95f * shown), tail = DbpuFx.A(Color.Lerp(tint, Color.white, .35f), 0f);
            for (int i = 0; i < Samples; i++)
            {
                int n = Mathf.Min(i, count - 1), before = Mathf.Max(0, n - 1), after = Mathf.Min(count - 1, n + 1);
                Vector3 tangent = (points[before] - points[after]); if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.right; tangent.Normalize();
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0);
                float age = Mathf.Clamp01((clock - born[n]) / TrailLife), along = n / (float)Mathf.Max(1, count - 1);
                float fade = i < count ? (1f - age) : 0f;
                float w = width * Mathf.Lerp(1f, .12f, along) * fade;
                Vector3 local = trail.transform.InverseTransformPoint(points[n]);
                Vector3 offset = trail.transform.InverseTransformVector(normal * w);
                vertices[i * 2] = local - offset; vertices[i * 2 + 1] = local + offset;
                Color c = Color.Lerp(head, tail, along); c.a *= fade * fade;
                colors[i * 2] = colors[i * 2 + 1] = c;
            }
            trailMesh.vertices = vertices; trailMesh.colors = colors; trailMesh.RecalculateBounds();
        }
        void OnDestroy() { if (trailMesh) Destroy(trailMesh); if (sparks) Destroy(sparks.gameObject); }
    }

    // Tier 0: light swing, tier 1: charged blow, tier 2: overcharged / infused burst.
    [DefaultExecutionOrder(295)]
    public sealed class KiFistImpactFx : MonoBehaviour
    {
        struct Ray { public float Angle, Start, Speed, Length, Width; }
        SpriteRenderer flash, core, glow;
        SpriteRenderer[] spikes, rings, lines;
        Ray[] rays; float[] spikeLength;
        float age, duration, radius; int tier; Vector2 centre, direction; Color tint;
        static int active;

        public static void Spawn(Vector2 point, Vector2 direction, int tier, Color tint)
        {
            if (active >= 18) return;
            var go = new GameObject("DBPU Ki Fist anime impact");
            go.transform.position = new Vector3(point.x, point.y, -.4f);
            var fx = go.AddComponent<KiFistImpactFx>(); active++;
            fx.Setup(point, direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right, Mathf.Clamp(tier, 0, 2), tint);
        }
        void Setup(Vector2 point, Vector2 dir, int level, Color colour)
        {
            centre = point; direction = dir; tier = level; tint = colour;
            duration = tier == 2 ? 1.05f : tier == 1 ? .62f : .36f;
            radius = tier == 2 ? 3.6f : tier == 1 ? 2.1f : 1.05f;
            glow = DbpuFx.MakeSprite(transform, "Impact bloom", DbpuFx.Glow, 42);
            core = DbpuFx.MakeSprite(transform, "Impact core", DbpuFx.Glow, 47, true);
            flash = DbpuFx.MakeSprite(transform, "Impact flash", DbpuFx.Glow, 48, true);
            int spikeCount = tier == 2 ? 10 : tier == 1 ? 6 : 0;
            spikes = new SpriteRenderer[spikeCount]; spikeLength = new float[spikeCount];
            float baseAngle = UnityEngine.Random.value * 360f;
            for (int i = 0; i < spikeCount; i++)
            {
                spikes[i] = DbpuFx.MakeSprite(transform, "Star spike", DbpuFx.Streak, 46, true);
                spikeLength[i] = (i % 2 == 0 ? 1f : .55f) * UnityEngine.Random.Range(.8f, 1.15f);
                spikes[i].transform.rotation = Quaternion.Euler(0, 0, baseAngle + i * 360f / spikeCount);
            }
            int ringCount = tier == 2 ? 3 : tier == 1 ? 2 : 1;
            rings = new SpriteRenderer[ringCount];
            for (int i = 0; i < ringCount; i++) rings[i] = DbpuFx.MakeSprite(transform, "Shock ring", i == 0 ? DbpuFx.RingThick : DbpuFx.Ring, 44);
            int lineCount = tier == 2 ? 34 : tier == 1 ? 18 : 9;
            lines = new SpriteRenderer[lineCount]; rays = new Ray[lineCount];
            float dirAngle = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < lineCount; i++)
            {
                lines[i] = DbpuFx.MakeSprite(transform, "Speed line", DbpuFx.Streak, 45);
                float spread = UnityEngine.Random.value < .72f ? UnityEngine.Random.Range(-1.9f, 1.9f) : UnityEngine.Random.Range(-3.14f, 3.14f);
                rays[i] = new Ray
                {
                    Angle = dirAngle + spread, Start = radius * UnityEngine.Random.Range(.12f, .35f),
                    Speed = radius * UnityEngine.Random.Range(1.6f, 3.2f), Length = radius * UnityEngine.Random.Range(.35f, .9f),
                    Width = radius * UnityEngine.Random.Range(.018f, .04f)
                };
            }
            var debris = DbpuParticles.Create("DBPU Ki Fist diamonds", DbpuFx.Square, 64, 46);
            debris.AutoDestroy = true;
            int pieces = tier == 2 ? 26 : tier == 1 ? 12 : 5;
            for (int i = 0; i < pieces; i++)
            {
                Vector2 v = DbpuFx.Rotate(direction, UnityEngine.Random.Range(-2.2f, 2.2f)) * UnityEngine.Random.Range(4f, 13f) * (radius / 2.4f);
                float s = UnityEngine.Random.Range(.05f, .14f) * (radius / 2f);
                debris.Emit(centre, v, UnityEngine.Random.Range(.35f, .8f) * (tier == 2 ? 1.3f : 1f), s, Color.white, DbpuFx.A(Color.Lerp(tint, Color.white, .5f), 0f), 3.2f, 2f, UnityEngine.Random.Range(-400f, 400f), false, s * .4f);
            }
            var streaks = DbpuParticles.Create("DBPU Ki Fist sparks burst", DbpuFx.Streak, 64, 46);
            streaks.AutoDestroy = true; streaks.StretchFactor = .05f;
            for (int i = 0; i < pieces + 6; i++)
            {
                Vector2 v = DbpuFx.Rotate(direction, UnityEngine.Random.Range(-1.4f, 1.4f)) * UnityEngine.Random.Range(8f, 22f) * (radius / 2.4f);
                streaks.Emit(centre, v, UnityEngine.Random.Range(.12f, .3f), radius * .06f, Color.white, DbpuFx.A(tint, 0f), 5f, 0, 0, true);
            }
            DbpuFx.Shake(centre, tier == 2 ? 2.4f : tier == 1 ? 1.1f : .45f);
            DbpuFx.Sound(centre, tier == 2 ? "Assets/SFX/Techniques/KiFist/HitHeavy.wav" : tier == 1 ? "Assets/SFX/Techniques/KiFist/HitMedium.wav" : "Assets/SFX/Techniques/KiFist/HitLight.wav", tier == 2 ? 1f : .8f);
            Draw();
        }
        void LateUpdate()
        {
            float dt = DbpuFx.Dt; if (dt <= 0f && DbpuFx.Paused) return;
            age += dt; if (age >= duration) { Destroy(gameObject); return; }
            Draw();
        }
        void Draw()
        {
            float t = Mathf.Clamp01(age / duration);
            Color hot = Color.Lerp(tint, Color.white, .55f);
            DbpuFx.Place(flash, centre, -.45f, radius * .9f * (1f + t * 1.4f), radius * .9f * (1f + t * 1.4f), 0, DbpuFx.A(Color.white, Mathf.Clamp01(1f - t * 4.5f)));
            DbpuFx.Place(core, centre, -.44f, radius * .55f * (1f - t * .35f), radius * .55f * (1f - t * .35f), 0, DbpuFx.A(Color.white, Mathf.Pow(1f - t, 1.6f)));
            DbpuFx.Place(glow, centre, -.40f, radius * 2.6f * (.8f + t * .5f), radius * 2.6f * (.8f + t * .5f), 0, DbpuFx.A(tint, .5f * Mathf.Pow(1f - t, 1.3f)));
            float st = Mathf.Clamp01(t / .3f);
            for (int i = 0; i < spikes.Length; i++)
            {
                float length = radius * 2.5f * spikeLength[i] * Mathf.Sqrt(1f - st) * (.6f + .4f * Mathf.Sin(age * 50f + i));
                var tr = spikes[i].transform; tr.position = new Vector3(centre.x, centre.y, -.43f);
                DbpuFx.WorldScale(tr, new Vector3(Mathf.Max(.0001f, length), radius * .07f, 1));
                spikes[i].color = DbpuFx.A(Color.white, (1f - st) * .95f); spikes[i].enabled = st < 1f;
            }
            for (int i = 0; i < rings.Length; i++)
            {
                float delay = i * .07f, local = Mathf.Clamp01((age - delay) / Mathf.Max(.05f, duration - delay));
                float r = radius * (.22f + Mathf.Pow(local, .55f) * (1.05f + i * .3f));
                float alpha = (age < delay ? 0f : 1f) * Mathf.Pow(1f - local, 1.25f) * (i == 0 ? .85f : .7f - i * .12f);
                DbpuFx.Place(rings[i], centre, -.42f, r * 2f, r * 2f, 0, DbpuFx.A(i == 0 ? hot : Color.white, alpha));
            }
            for (int i = 0; i < lines.Length; i++)
            {
                var ray = rays[i]; float travel = ray.Start + ray.Speed * Mathf.Pow(t, .7f);
                float length = ray.Length * (1f - t * .6f);
                Vector2 dir = new Vector2(Mathf.Cos(ray.Angle), Mathf.Sin(ray.Angle));
                DbpuFx.Place(lines[i], centre + dir * (travel + length * .5f), -.41f, length, ray.Width * (1f - t * .5f), ray.Angle * Mathf.Rad2Deg, DbpuFx.A(Color.white, (1f - t) * .9f));
            }
        }
        void OnDestroy() { active = Mathf.Max(0, active - 1); }
    }

    // Fighter-wide Ki Fist switch: both hands and both feet, as in the reference.
    public static class KiFistFighter
    {
        public const string OnHint = "ON  ·  SWING HARD  ·  HOLD B ON AN UPPER ARM TO OVERCHARGE";
        public static string KeyLabel { get { return DbpuInputRegistration.GetActionKeyLabel(KiFistOverchargeInput.ActionId, KiFistOverchargeInput.DefaultKey); } }
        public static string Hint { get { return "ON  ·  SWING HARD  ·  HOLD " + KeyLabel + " ON AN UPPER ARM TO OVERCHARGE"; } }
        public static IEnumerable<LimbBehaviour> StrikeLimbs(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) yield break;
            foreach (var limb in person.Limbs) if (KiHandLoadout.IsStrikeLimb(limb) && limb.Health > 0f) yield return limb;
        }
        public static bool IsOn(PersonBehaviour person)
        {
            foreach (var limb in StrikeLimbs(person)) { var fist = limb.GetComponent<KiFistHand>(); if (fist && fist.Equipped) return true; }
            return false;
        }
        public static bool SetOn(PersonBehaviour person, bool on, bool announce = true)
        {
            if (!person || !person.IsAlive() || !person.GetComponent<KiSystem>()) return false;
            bool any = false;
            foreach (var limb in new List<LimbBehaviour>(StrikeLimbs(person)))
            {
                var loadout = KiHandLoadout.GetOrCreate(limb); if (!loadout) continue;
                if (on) { var fist = limb.GetComponent<KiFistHand>(); if (fist && fist.Equipped) { any = true; continue; } }
                else if (loadout.SelectedId != KiHandLoadout.FistId) continue;
                if (loadout.Equip(on ? KiHandLoadout.FistId : KiHandLoadout.NoneId)) any = true;
            }
            if (announce) Announce(person, on);
            return any;
        }
        public static void Announce(PersonBehaviour person, bool on)
        {
            DbpuWorldLabel.Show(person, "KI FIST", on ? Hint : "OFF", on ? 3.2f : 1.6f);
            DbpuFx.Sound(DbpuFx.Centre(person), on ? "Assets/SFX/Techniques/KiFist/On.wav" : "Assets/SFX/Techniques/KiFist/Off.wav", .55f);
        }
        public static void Toggle(PersonBehaviour person) { SetOn(person, !IsOn(person)); }
    }

    // "B": tap on a fighter toggles Ki Fist; hold on an arm (upper or lower) to overcharge
    // that fist, on a leg for that foot, on the body for both fists.
    [DefaultExecutionOrder(-235)]
    public sealed class KiFistOverchargeInput : MonoBehaviour
    {
        public const string ActionId = "dbpu.kifist.toggle";
        public const KeyCode DefaultKey = KeyCode.B;
        const float HoldThreshold = .22f;
        static KiFistOverchargeInput instance;
        PersonBehaviour person;
        readonly List<KiFistHand> charging = new List<KiFistHand>();
        LimbBehaviour hovered;
        float held; bool claimed, holding; GameObject loop;

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Ki Fist toggle / overcharge input"); DontDestroyOnLoad(go);
            instance = go.AddComponent<KiFistOverchargeInput>();
        }
        static bool Pressed() { return InputSystem.Has(ActionId) ? InputSystem.Down(ActionId) : Input.GetKeyDown(DefaultKey); }
        static bool Holding() { return InputSystem.Has(ActionId) ? InputSystem.Held(ActionId) : Input.GetKey(DefaultKey); }
        static bool CanRead()
        {
            if (!KiCombatMath.CanAct() || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput || KiFistInput.InputCaptured) return false;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return false;
            var focused = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return !focused || (!focused.GetComponent<TMP_InputField>() && !focused.GetComponent<InputField>());
        }
        static LimbBehaviour Hovered()
        {
            var selection = SelectionController.Main;
            var physical = selection != null ? selection.CurrentlyUnderMouse : null;
            var limb = physical ? physical.GetComponent<LimbBehaviour>() : null;
            if (limb && limb.Person && limb.Person.GetComponent<KiSystem>()) return limb;
            var tool = Global.main && Global.main.ToolControllerBehaviour ? Global.main.ToolControllerBehaviour.CurrentTool : null;
            physical = tool != null ? tool.ActiveSingleSelected : null;
            limb = physical ? physical.GetComponent<LimbBehaviour>() : null;
            return limb && limb.Person && limb.Person.GetComponent<KiSystem>() ? limb : null;
        }
        void Update()
        {
            if (claimed)
            {
                if (!person || !person.IsAlive() || !KiCombatMath.HasFocus) { Finish(false); return; }
                if (!Holding()) { Finish(true); return; }
                held += Time.unscaledDeltaTime;
                if (!holding && held >= HoldThreshold) BeginOvercharge();
                if (holding) Report();
                return;
            }
            if (!CanRead() || !Pressed()) return;
            hovered = Hovered(); if (!hovered) return;
            person = hovered.Person; claimed = true; holding = false; held = 0f;
        }
        void BeginOvercharge()
        {
            holding = true; charging.Clear();
            if (!KiFistFighter.IsOn(person)) KiFistFighter.SetOn(person, true, false);
            var targets = new List<LimbBehaviour>();
            var strike = CombatAbilityWheel.ResolveAbilityLimb(hovered);
            if (strike) targets.Add(strike);
            else foreach (var limb in KiFistFighter.StrikeLimbs(person)) if (KiHandLoadout.IsHand(limb)) targets.Add(limb);
            foreach (var limb in targets)
            {
                var fist = limb ? limb.GetComponent<KiFistHand>() : null;
                if (!fist || !fist.Equipped) continue;
                if (fist.BeginHeldCharge()) { fist.ChargeRate = 2.6f; charging.Add(fist); }
            }
            if (charging.Count == 0) { DbpuWorldLabel.Show(person, "KI FIST", "NOT ENOUGH KI", 1.2f); return; }
            loop = DbpuFx.Sound(DbpuFx.Centre(person), "Assets/SFX/Techniques/KiFist/Charge.wav", .5f, person.transform, true);
        }
        void Report()
        {
            float best = 0f, over = 0f; bool any = false;
            foreach (var fist in charging) if (fist && fist.IsCharging) { any = true; best = Mathf.Max(best, fist.Charge01); over = Mathf.Max(over, fist.Overcharge01); }
            if (!any) { DbpuWorldLabel.Show(person, "KI FIST", "OVERCHARGED  ·  SWING HARD", 2.5f); StopLoop(); return; }
            string text = over > 0f ? "OVERCHARGING " + Mathf.RoundToInt(over * 100f) + "%" : "CHARGING " + Mathf.RoundToInt(best * 100f) + "%";
            DbpuWorldLabel.Show(person, "KI FIST", text, .5f, best * .5f + over * .5f);
        }
        void Finish(bool released)
        {
            if (claimed && !holding && released && person) KiFistFighter.Toggle(person);
            else if (holding)
            {
                bool armed = false;
                foreach (var fist in charging) if (fist) { fist.ReleaseHeldCharge(); fist.ChargeRate = 1f; armed |= fist.IsCharged; }
                if (person) DbpuWorldLabel.Show(person, "KI FIST", armed ? "OVERCHARGED  ·  SWING HARD" : "CHARGE TOO SHORT", armed ? 3f : 1.2f);
                if (armed && person) DbpuFx.Sound(DbpuFx.Centre(person), "Assets/SFX/Techniques/KiFist/Ready.wav", .6f);
            }
            StopLoop(); charging.Clear(); claimed = holding = false; person = null; hovered = null; held = 0f;
        }
        void StopLoop() { if (loop) Destroy(loop); loop = null; }
        void OnDisable() { if (claimed) Finish(false); }
    }
}
