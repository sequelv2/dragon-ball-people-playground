using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    // Time services shared by Singularity modules.
    //  Hitstop: a few frames of near-frozen time on heavy hits. Uses the same ownership
    //  protocol as Reality Rift: own Global's private time target while active and give it
    //  back only if nobody else (pause, slow motion, Reality Rift) changed it meanwhile.
    //  WorldFreeze: stops every rigidbody except an exempt set (Hit's Time Skip, Time Prison).
    public static class SingularityTime
    {
        public static void Hitstop(float seconds, float scale = .05f)
        {
            if (seconds <= 0f) return;
            SingularityTimeDriver.Ensure().Request(Mathf.Min(seconds, .3f), Mathf.Clamp(scale, .01f, .5f));
        }

        public static bool WorldFrozen { get { return SingularityWorldFreeze.Active; } }
    }

    public sealed class SingularityTimeDriver : MonoBehaviour
    {
        static SingularityTimeDriver instance;
        static readonly FieldInfo targetField = typeof(Global).GetField("timeScaleTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        Global owner; bool owning; float scale, savedTarget, savedTime, until, cooldownUntil;

        public static SingularityTimeDriver Ensure()
        {
            if (instance) return instance;
            var go = new GameObject("DBPU Singularity time");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SingularityTimeDriver>();
            return instance;
        }

        public void Request(float seconds, float s)
        {
            if (targetField == null || RealityRift.Active || !Global.main) return;
            if (Global.main.Paused || Global.main.GetPausedMenu() || Time.timeScale <= 0f) return;
            if (!owning && Time.unscaledTime < cooldownUntil) return;
            if (!owning)
            {
                owner = Global.main;
                try { savedTarget = (float)targetField.GetValue(owner); } catch { return; }
                savedTime = Time.timeScale;
                if (savedTime <= s) return;     // already slower than the hitstop
                owning = true; scale = s;
            }
            else scale = Mathf.Min(scale, s);
            until = Mathf.Max(until, Time.unscaledTime + seconds);
            try { targetField.SetValue(owner, scale); } catch { owning = false; return; }
            Time.timeScale = scale;
        }

        void Update()
        {
            if (!owning) return;
            float current;
            try { current = owner ? (float)targetField.GetValue(owner) : -1f; } catch { current = -1f; }
            if (!owner || !Mathf.Approximately(current, scale)) { owning = false; return; }   // revoked by the user
            Time.timeScale = scale;
            if (Time.unscaledTime >= until) Restore();
        }

        void Restore()
        {
            try
            {
                if (owner && Mathf.Approximately((float)targetField.GetValue(owner), scale)) targetField.SetValue(owner, savedTarget);
            }
            catch { }
            bool paused = Global.main && (Global.main.Paused || Global.main.GetPausedMenu());
            if (!paused && Time.timeScale > 0f && Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = savedTime;
            owning = false;
            cooldownUntil = Time.unscaledTime + .08f;
        }

        void OnDisable() { if (owning) Restore(); }
    }

    public sealed class SingularityWorldFreeze : MonoBehaviour
    {
        struct Frozen
        {
            public Rigidbody2D Body; public Vector2 Velocity; public float Angular; public RigidbodyConstraints2D Constraints;
        }
        sealed class Hit { public Rigidbody2D Body; public Vector2 Impulse, Point; }

        public static SingularityWorldFreeze Current { get; private set; }
        public static bool Active { get { return Current; } }
        readonly List<Frozen> frozen = new List<Frozen>();
        readonly List<ParticleSystem> pausedParticles = new List<ParticleSystem>();
        readonly List<Hit> queued = new List<Hit>();
        readonly HashSet<Transform> exempt = new HashSet<Transform>();
        float until; Action<SingularityWorldFreeze> finished;
        public float Remaining { get { return Mathf.Max(0f, until - Time.unscaledTime); } }
        public int QueuedHits { get { return queued.Count; } }

        // exemptRoots: transforms whose rigidbodies keep moving (the time-skipper).
        public static SingularityWorldFreeze Begin(IEnumerable<Transform> exemptRoots, float seconds, Action<SingularityWorldFreeze> onEnd)
        {
            if (Current) return null;
            var go = new GameObject("DBPU world freeze");
            var freeze = go.AddComponent<SingularityWorldFreeze>();
            Current = freeze;
            if (exemptRoots != null) foreach (var t in exemptRoots) if (t) freeze.exempt.Add(t.root);
            freeze.until = Time.unscaledTime + seconds;
            freeze.finished = onEnd;
            freeze.Capture();
            return freeze;
        }

        public bool IsExempt(Transform t) { return t && exempt.Contains(t.root); }

        void Capture()
        {
            Rigidbody2D[] bodies;
            try { bodies = FindObjectsOfType<Rigidbody2D>(); } catch { bodies = new Rigidbody2D[0]; }
            foreach (var body in bodies)
            {
                if (!body || body.bodyType != RigidbodyType2D.Dynamic || exempt.Contains(body.transform.root)) continue;
                frozen.Add(new Frozen { Body = body, Velocity = body.velocity, Angular = body.angularVelocity, Constraints = body.constraints });
                body.velocity = Vector2.zero; body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            ParticleSystem[] systems;
            try { systems = FindObjectsOfType<ParticleSystem>(); } catch { systems = new ParticleSystem[0]; }
            foreach (var ps in systems)
            {
                if (!ps || !ps.isPlaying || exempt.Contains(ps.transform.root)) continue;
                ps.Pause(false);
                pausedParticles.Add(ps);
            }
        }

        // Blows landed while time is stopped are stored and delivered together on release.
        public void QueueHit(Rigidbody2D body, Vector2 impulse, Vector2 point)
        {
            if (!body || exempt.Contains(body.transform.root)) return;
            foreach (var h in queued)
                if (h.Body == body) { h.Impulse += impulse; return; }
            queued.Add(new Hit { Body = body, Impulse = impulse, Point = point });
        }

        public void End() { until = 0f; }

        void Update()
        {
            if (DbpuFx.Paused) { until += Time.unscaledDeltaTime; return; }
            if (Time.unscaledTime < until) return;
            Release();
        }

        void Release()
        {
            foreach (var f in frozen)
            {
                if (!f.Body) continue;
                f.Body.constraints = f.Constraints;
                f.Body.velocity = f.Velocity;
                f.Body.angularVelocity = f.Angular;
            }
            frozen.Clear();
            foreach (var ps in pausedParticles) if (ps) ps.Play(false);
            pausedParticles.Clear();
            foreach (var h in queued)
            {
                if (!h.Body || h.Body.bodyType != RigidbodyType2D.Dynamic) continue;
                h.Body.AddForceAtPosition(h.Impulse, h.Point, ForceMode2D.Impulse);
            }
            var callback = finished; finished = null;
            if (Current == this) Current = null;
            if (callback != null)
            {
                try { callback(this); } catch (Exception ex) { Debug.LogWarning("[Singularity] freeze end: " + ex.Message); }
            }
            queued.Clear();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (frozen.Count > 0)
            {
                foreach (var f in frozen) if (f.Body) { f.Body.constraints = f.Constraints; f.Body.velocity = f.Velocity; }
                frozen.Clear();
            }
            foreach (var ps in pausedParticles) if (ps) ps.Play(false);
            pausedParticles.Clear();
            if (Current == this) Current = null;
        }
    }
}
