using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Impact frames: heavy blows between fighters stop time for a few frames with a flash,
    // a ring, a bend in space and camera shake, scaled by impact speed. Two strikes meeting head-on burst
    // into a clash: a double shockwave pushes everything nearby and throws both fighters
    // apart. (Clashes strong enough for Reality Rift are left to Reality Rift.)
    public sealed class ImpactFramesModule : SingularityModule
    {
        public override string Id { get { return "impact"; } }
        public override string Title { get { return "Impact Frames & Clashes"; } }
        public override string Summary { get { return "Heavy blows between fighters freeze time for a few frames with a flash and shockwave; two strikes meeting head-on burst into a clash."; } }
        public const string SfxClash = "Assets/SFX/Singularity/Impact/Clash.wav";
        readonly Dictionary<long, float> pairCooldown = new Dictionary<long, float>();
        float hitstopCooldown, cleanup;
        static readonly Collider2D[] nearby = new Collider2D[64];

        public override void Boot()
        {
            DbpuFx.PreloadExtra(SfxClash);
            HitLens.Initialize(Singularity.ModDirectory);
            Singularity.LimbImpact += OnImpact;
        }

        public override void Tick(float dt)
        {
            hitstopCooldown -= Time.unscaledDeltaTime;
            cleanup -= Time.unscaledDeltaTime;
            if (cleanup <= 0f) { cleanup = 5f; pairCooldown.Clear(); }
        }

        void OnImpact(SingularityImpact i)
        {
            if (!Enabled || !i.OtherPerson || !i.Person || i.OtherPerson == i.Person) return;
            if (RealityRift.Active || SingularityWorldFreeze.Active || i.Speed < 7f) return;
            if (!i.Person.GetComponent<KiSystem>() && !i.OtherPerson.GetComponent<KiSystem>()) return;
            int a = i.Person.GetInstanceID(), b = i.OtherPerson.GetInstanceID();
            long key = a < b ? ((long)a << 32) ^ (uint)b : ((long)b << 32) ^ (uint)a;
            float until;
            if (pairCooldown.TryGetValue(key, out until) && Time.unscaledTime < until) return;
            bool strikeA = KiHandLoadout.IsStrikeLimb(i.Limb), strikeB = i.OtherLimb && KiHandLoadout.IsStrikeLimb(i.OtherLimb);
            if (!strikeA && !strikeB && i.Speed < 14f) return;
            pairCooldown[key] = Time.unscaledTime + .3f;
            var ra = i.Limb ? i.Limb.GetComponent<Rigidbody2D>() : null;
            var rb = i.OtherLimb ? i.OtherLimb.GetComponent<Rigidbody2D>() : null;
            bool headOn = strikeA && strikeB && ra && rb && Vector2.Dot(ra.velocity, rb.velocity) < 0f
                && ra.velocity.magnitude > 5f && rb.velocity.magnitude > 5f;
            if (headOn && i.Speed > 11f) Clash(i);
            else Heavy(i);
        }

        void Heavy(SingularityImpact i)
        {
            float s = Mathf.InverseLerp(7f, 30f, i.Speed);
            if (hitstopCooldown <= 0f) { SingularityTime.Hitstop(Mathf.Lerp(.03f, .1f, s)); hitstopCooldown = .25f; }
            SingularityFx.Flash(i.Point, new Color(1f, 1f, 1f, .9f), Mathf.Lerp(.5f, 1.3f, s), .18f);
            SingularityFx.Ring(i.Point, new Color(1f, .9f, .6f, .85f), Mathf.Lerp(.8f, 2.2f, s), .28f, s > .5f ? 2f : 1f);
            HitLens.Pulse(i.Point, Mathf.Lerp(.2f, .6f, s), Mathf.Lerp(.28f, .5f, s), new Color(1f, .9f, .6f));
            // An equipped Ki Fist already sends its own pressure wave.
            if (!KiFistEquipped(i.Limb) && !KiFistEquipped(i.OtherLimb))
                KiSpaceRefraction.Pulse(i.Point, Mathf.Lerp(1.8f, 5f, s), Mathf.Lerp(.35f, .85f, s), Mathf.Lerp(.35f, .75f, s), i.RelativeVelocity);
            if (s > .35f) SingularityFx.Burst(i.Point, new Color(1f, .85f, .5f), .7f + s, 8 + (int)(s * 14f));
            DbpuFx.Shake(i.Point, Mathf.Lerp(.6f, 2.4f, s));
            DbpuFx.Sound(i.Point, s > .55f ? "Assets/SFX/Techniques/KiFist/HitHeavy.wav" : "Assets/SFX/Techniques/KiFist/HitMedium.wav", Mathf.Lerp(.45f, .9f, s));
        }

        static bool KiFistEquipped(LimbBehaviour limb)
        {
            var fist = limb ? limb.GetComponent<KiFistHand>() : null;
            return fist && fist.Equipped;
        }

        void Clash(SingularityImpact i)
        {
            Vector2 p = i.Point;
            float s = Mathf.InverseLerp(11f, 40f, i.Speed);
            HitLens.Pulse(p, .8f + s * .5f, .6f, new Color(.7f, .85f, 1f), .5f);
            KiSpaceRefraction.Pulse(p, 6f + s * 5f, 1f, .7f + s * .4f, i.RelativeVelocity);
            SingularityTime.Hitstop(Mathf.Lerp(.08f, .14f, s)); hitstopCooldown = .4f;
            SingularityFx.Flash(p, Color.white, 2f + s * 2f, .3f);
            SingularityFx.Ring(p, new Color(1f, .95f, .8f, .95f), 3f + s * 3f, .45f, 2f);
            SingularityFx.Ring(p, new Color(.7f, .85f, 1f, .7f), 5f + s * 4f, .7f, 1f);
            SingularityFx.Burst(p, new Color(1f, .9f, .6f), 1.4f + s, 26);
            SingularityFx.Dust(p + Vector2.down * .4f, 1f + s);
            DbpuFx.Shake(p, 3f + s * 3f);
            DbpuFx.Sound(p, ImpactFramesModule.SfxClash, 1f);
            float radius = 3.5f + s * 3f, force = 10f + s * 18f;
            int n = Physics2D.OverlapCircleNonAlloc(p, radius, nearby);
            var touched = new HashSet<Rigidbody2D>();
            for (int k = 0; k < n; k++)
            {
                var col = nearby[k];
                var body = col ? col.attachedRigidbody : null;
                if (!body || body.bodyType != RigidbodyType2D.Dynamic || !touched.Add(body)) continue;
                var root = body.transform.root;
                if (root == i.Person.transform.root || root == i.OtherPerson.transform.root) continue;
                Vector2 d = body.position - p;
                float fall = 1f - Mathf.Clamp01(d.magnitude / radius);
                body.AddForce(d.normalized * force * fall * body.mass, ForceMode2D.Impulse);
            }
            Vector2 ca = DbpuFx.Centre(i.Person), cb = DbpuFx.Centre(i.OtherPerson);
            SingularityFighters.Push(i.Person, (ca - p).normalized * (6f + s * 8f) + Vector2.up * 2f);
            SingularityFighters.Push(i.OtherPerson, (cb - p).normalized * (6f + s * 8f) + Vector2.up * 2f);
            DbpuWorldLabel.Show(i.Person, "CLASH!", "", .9f);
        }
    }
}
