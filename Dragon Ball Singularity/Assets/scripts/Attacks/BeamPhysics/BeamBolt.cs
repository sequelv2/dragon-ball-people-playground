using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // What is left of a beam after a swat: the firer has been cut off and the head flies on by itself
    // like a ki blast, trailing a short tail, until it hits something or is gone from sight.
    public sealed class BeamBolt : MonoBehaviour
    {
        const int MaxTail = 24; const float Life = 3f, FadeTime = .4f, Spacing = .7f;
        public static readonly List<BeamBolt> All = new List<BeamBolt>();
        public Vector2 Position { get; private set; }
        public Vector2 Direction { get { return BeamBody.Direction(angle); } }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        static int mask = -1, blastMask = -1;

        BeamStyle style; PersonBehaviour holder; Transform holderRoot;
        BeamRibbon ribbon; BeamQuad orb, crown;
        float angle, curl, curlLeft, speed, width, damage, age, clock;
        readonly List<Vector2> trail = new List<Vector2>();
        readonly Vector2[] nodes = new Vector2[MaxTail + 1];
        readonly float[] arcs = new float[MaxTail + 1], half = new float[MaxTail + 1], heat = new float[MaxTail + 1], alpha = new float[MaxTail + 1];

        // curl turns it, in degrees per unit travelled, through at most curlAngle degrees.
        public static BeamBolt Launch(Vector2 at, float angle, float curl, float curlAngle, float speed, float width, float damage, BeamStyle style, PersonBehaviour holder)
        {
            var bolt = new GameObject("DBPU beam bolt").AddComponent<BeamBolt>();
            bolt.Position = at; bolt.angle = angle; bolt.curl = curl; bolt.curlLeft = curlAngle; bolt.speed = speed;
            bolt.width = Mathf.Clamp(width, .5f, 6f); bolt.damage = damage; bolt.style = style;
            bolt.holder = holder; bolt.holderRoot = holder ? holder.transform.root : null;
            if (BeamFxAssets.Ready) bolt.ribbon = new BeamRibbon();
            bolt.orb = new BeamQuad("DBPU beam bolt orb", BeamQuad.Orb, "Background", -11);
            bolt.crown = new BeamQuad("DBPU beam bolt crown", BeamQuad.Crown, "Background", -12);
            bolt.orb.Style(style); bolt.crown.Style(style);
            bolt.crown.Spread = .72f; bolt.crown.Speed = 1.4f;
            bolt.trail.Add(at);
            All.Add(bolt);
            return bolt;
        }

        public static void DestroyAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }

        void Update()
        {
            float dt = DbpuFx.Dt;
            clock += dt;
            if (dt > 0f && !Fly(dt)) return;
            Draw(dt);
        }

        // False once it has burst or run out.
        bool Fly(float dt)
        {
            age += dt;
            if (age >= Life) { Destroy(gameObject); return false; }
            float step = speed * dt;
            float turn = Mathf.Min(curlLeft, Mathf.Abs(curl) * step);
            angle += Mathf.Sign(curl) * turn; curlLeft -= turn;
            Vector2 direction = Direction, point, normal;
            if (FindHit(direction, step, out point, out normal)) { Burst(point, normal, direction); return false; }
            Position += direction * step;
            while (Vector2.Distance(trail[trail.Count - 1], Position) >= Spacing)
            {
                trail.Add(Vector2.MoveTowards(trail[trail.Count - 1], Position, Spacing));
                if (trail.Count > MaxTail) trail.RemoveAt(0);
            }
            return true;
        }

        bool FindHit(Vector2 direction, float step, out Vector2 point, out Vector2 normal)
        {
            point = Position; normal = -direction;
            if (mask < 0) mask = LayerMask.GetMask("Objects", "CollidingDebris", "Bounds") | 1 << 11;
            int n = Physics2D.CircleCastNonAlloc(Position, Mathf.Min(width * .25f, .6f), direction, hits, step, mask);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (!col || col.isTrigger || (holderRoot && col.transform.root == holderRoot) || hits[i].distance >= best) continue;
                best = hits[i].distance; point = hits[i].point; normal = hits[i].normal;
            }
            return best != float.MaxValue;
        }

        void Burst(Vector2 point, Vector2 normal, Vector2 direction)
        {
            float scale = Mathf.Clamp(width * .55f, .6f, 3f);
            if (normal.y > .5f) BeamGround.Erupt(point, scale, style.Mid, normal);
            BeamContact.Burst(point, normal, style, style, scale);
            DbpuFx.Sound(point, ImpactFramesModule.SfxClash, .9f);
            if (blastMask < 0) blastMask = LayerMask.GetMask("Objects", "CollidingDebris");
            float radius = 1.5f + width;
            foreach (var col in Physics2D.OverlapCircleAll(point, radius, blastMask))
            {
                if (!col || col.isTrigger || (holderRoot && col.transform.root == holderRoot)) continue;
                Vector2 away = (Vector2)col.bounds.center - point;
                float fall = 1f - Mathf.Clamp01(away.magnitude / radius) * .7f;
                var body = col.attachedRigidbody;
                if (body && body.bodyType == RigidbodyType2D.Dynamic) body.AddForce((away.normalized + direction * .4f) * 12f * fall * body.mass, ForceMode2D.Impulse);
                var limb = col.GetComponent<LimbBehaviour>();
                if (limb)
                {
                    if (KiShield.Protects(limb.Person, holder ? holder.gameObject : null, point,
                        damage * 300f * fall / .12f, GetInstanceID())) continue;
                    EnergyAnnihilation.Expose(holder, limb, KiCombatMath.Cap(DbpuFx.PowerOf(holder) * fall),
                        col.ClosestPoint(point), .12f, style.Mid);
                    if (!limb.IsDismembered && !limb.ImmuneToDamage) limb.Damage(damage * 300f * fall);
                }
            }
            Destroy(gameObject);
        }

        void Draw(float dt)
        {
            float opacity = Mathf.Clamp01((Life - age) / FadeTime);
            Vector2 forward = Direction;
            float breath = 1f + .025f * Mathf.Sin(clock * 9f);
            orb.Place(Position, width * .6f * breath); orb.Opacity = opacity; orb.Apply(dt);
            crown.Dir = -forward; crown.Place(Position, width * 2.1f); crown.Opacity = opacity; crown.Apply(dt);
            if (ribbon == null) return;
            int count = 0;
            foreach (var p in trail) nodes[count++] = p;
            nodes[count++] = Position;
            arcs[0] = 0f;
            for (int i = 1; i < count; i++) arcs[i] = arcs[i - 1] + Vector2.Distance(nodes[i - 1], nodes[i]);
            float length = Mathf.Max(.01f, arcs[count - 1]), radius = Mathf.Max(.05f, style.Radius);
            for (int i = 0; i < count; i++)
            {
                float t = arcs[i] / length;
                half[i] = width * .5f / radius * Mathf.Lerp(.15f, 1f, t);
                heat[i] = 0f;
                alpha[i] = Mathf.Clamp01(t * 2f);
            }
            ribbon.Draw(nodes, arcs, half, heat, alpha, count, style, width, 0f, opacity, clock);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (ribbon != null) ribbon.Destroy();
            if (orb != null) orb.Destroy();
            if (crown != null) crown.Destroy();
        }
    }
}
