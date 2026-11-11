using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Everything that happens where a beam is being stopped: a white-hot knot of plasma, a crown of
    // feathered spikes, shafts of light, sparks, shards, twinkles, lightning, pressure rings and the
    // grinding roar. A hold passes one colour; a clash passes both sides'.
    public sealed class BeamContact
    {
        readonly BeamQuad orb, crown, crownBack, rays, flareA, flareB, arc;
        readonly GameObject loop; readonly AudioSource loopSource;
        BeamStyle a, b; bool twoSided;
        float sparkDebt, shardDebt, starDebt, lightningClock, ringClock, shakeClock, arcClock, clock;
        Vector2 at, axis;
        public float Volume = .8f;

        public BeamContact(BeamStyle first, BeamStyle second, bool clash, Vector2 point)
        {
            a = first; b = second; twoSided = clash; at = point;
            rays = new BeamQuad("DBPU beam light shafts", BeamQuad.Rays, "Background", -30);
            crownBack = new BeamQuad("DBPU beam contact crown back", BeamQuad.Crown, "Background", -10);
            crown = new BeamQuad("DBPU beam contact crown", BeamQuad.Crown, "Background", -9);
            orb = new BeamQuad("DBPU beam contact orb", BeamQuad.Orb, "Background", -8);
            arc = new BeamQuad("DBPU beam contact arc", BeamQuad.Arc, "Background", -7);
            flareA = new BeamQuad("DBPU beam hand flare", BeamQuad.Flame, "Foreground", 6);
            flareB = new BeamQuad("DBPU beam hand flare", BeamQuad.Flame, "Foreground", 6);
            flareA.Hide(); flareB.Hide();
            try
            {
                loop = MCP_Utils.PlayAudio(new Vector3(point.x, point.y, 0f), DBPU.Attack_Kamehameha_Loop, null, false, Mathf.Clamp(DBPU.PowersVolume, 0f, 2f) * Volume, true, 1f, 1000f, .6f, "DBPU beam contact roar");
                loopSource = loop ? loop.GetComponent<AudioSource>() : null;
                if (loopSource) loopSource.pitch = clash ? .82f : .68f;
            }
            catch (System.Exception ex) { Debug.LogWarning("[Singularity] beam roar: " + ex.Message); }
        }

        // incoming: the direction the (first) beam is travelling at the contact.
        public void Step(float dt, Vector2 point, Vector2 incoming, float strain, float size)
        {
            clock += dt;
            at = point; axis = incoming.sqrMagnitude > .001f ? incoming.normalized : Vector2.right;
            float s = Mathf.Clamp01(strain), w = Mathf.Max(size, .5f);
            if (loop) loop.transform.position = new Vector3(at.x, at.y, 0f);
            if (loopSource) loopSource.volume = Mathf.Clamp(DBPU.PowersVolume, 0f, 2f) * Volume * (.55f + s * .45f);

            rays.Mid = a.Mid; rays.Hot = a.Hot; rays.Alt = twoSided ? b.Mid : a.Mid; rays.Dir = -axis; rays.Speed = 1f;
            rays.Power = .55f + s * .45f; rays.Opacity = .45f + s * .55f;
            rays.Place(at, 8f + w * 3f + s * (9f + w * 4f)); rays.Apply(dt);

            // The knot sits on the beam's side of the hands, so the fighter stays in view behind it.
            Vector2 knot = twoSided ? at : at - axis * w * (.3f + s * .1f);
            crownBack.Style(a); crownBack.Dir = -axis; crownBack.Spread = .74f; crownBack.Speed = 1.5f; crownBack.Power = 1f; crownBack.Opacity = .96f;
            crownBack.Place(knot, w * (2.1f + s * 1.5f)); crownBack.Apply(dt);
            crown.Style(twoSided ? b : a); crown.Dir = twoSided ? axis : new Vector2(-axis.y, axis.x) * Mathf.Sign(Mathf.Sin(clock * 1.3f) + .001f); crown.Spread = twoSided ? .74f : .9f; crown.Speed = 1.9f; crown.Power = 1f; crown.Opacity = .9f;
            crown.Place(knot, w * (1.7f + s * 1.3f)); crown.Apply(dt);
            orb.Style(a); orb.Hot = Color.white; orb.Speed = 1.4f; orb.Opacity = 1f;
            orb.Place(knot, w * (.4f + s * .2f) * (1f + .07f * Mathf.Sin(clock * 31f))); orb.Apply(dt);

            arcClock -= dt;
            if (arcClock <= 0f) arcClock = Random.Range(.55f, 1.1f);
            arc.Style(a); arc.Dir = DbpuFx.Rotate(Vector2.up, Mathf.Sin(clock * .4f) * .9f); arc.Spread = .62f; arc.Power = 1f - arcClock / 1.1f; arc.Opacity = (arcClock / 1.1f) * .9f;
            arc.Place(at, 6f + w * 2f + s * 6f); arc.Apply(dt);

            EmitSparks(dt, s, w);

            lightningClock -= dt;
            if (lightningClock <= 0f)
            {
                lightningClock = Mathf.Lerp(.28f, .07f, s) * Random.Range(.7f, 1.3f);
                Vector2 end = at + Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.2f + w * 1.6f + s * 5f);
                Color tint = Color.Lerp(Random.value < .5f || !twoSided ? a.Mid : b.Mid, Color.white, .55f);
                SingularityFx.Lightning(at, end, tint, .05f + s * .05f, .16f, 11, .42f + s * .4f);
            }
            ringClock -= dt;
            if (ringClock <= 0f)
            {
                ringClock = Mathf.Lerp(.55f, .24f, s);
                Shock(at, a, 2.4f + w * .9f + s * 4f, .38f);
                KiSpaceRefraction.Pulse(at, 1.6f + w * .5f + s * 2f, .25f + s * .3f, .34f, axis);
            }
            shakeClock -= dt;
            if (shakeClock <= 0f)
            {
                shakeClock = .07f;
                DbpuFx.Shake(at, .3f + s * 1.5f);
            }
        }

        void EmitSparks(float dt, float s, float w)
        {
            var sparks = BeamParticles.Sparks; var shards = BeamParticles.Shards; var stars = BeamParticles.Stars;
            sparkDebt += dt * (170f + s * 620f);
            int n = Mathf.Min(48, (int)sparkDebt); sparkDebt -= n;
            for (int i = 0; i < n; i++)
            {
                // A jet hitting a wall: most of the energy fans out across the blocking surface.
                Vector2 side = new Vector2(-axis.y, axis.x) * (Random.value < .5f ? -1f : 1f);
                Vector2 dir = (side * Random.Range(.5f, 1.2f) - axis * Random.Range(-.25f, .9f) + Random.insideUnitCircle * .35f).normalized;
                var style = !twoSided || Random.value < .5f ? a : b;
                float speed = Random.Range(7f, 22f + s * 26f);
                sparks.Emit(at + dir * w * Random.Range(.1f, .4f), dir * speed, Random.Range(.16f, .5f), Random.Range(.05f, .13f + s * .07f), Color.Lerp(style.Hot, Color.white, .6f), DbpuFx.A(style.Mid, 0f), 2.2f, 3f, 0f, true);
            }
            shardDebt += dt * (8f + s * 42f);
            n = Mathf.Min(10, (int)shardDebt); shardDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var style = !twoSided || Random.value < .5f ? a : b;
                Color c = Color.Lerp(style.Mid, Color.white, Random.Range(.2f, .8f));
                shards.Emit(at + dir * w * .4f, dir * Random.Range(5f, 16f), Random.Range(.25f, .6f), Random.Range(.12f, .36f), c, DbpuFx.A(style.Mid, .4f), 2f, 0f, Random.Range(-720f, 720f));
            }
            starDebt += dt * (3f + s * 14f);
            n = Mathf.Min(4, (int)starDebt); starDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = at + Random.insideUnitCircle * (2f + w * 1.5f + s * 7f) + Vector2.up * Random.Range(0f, 3f);
                var style = !twoSided || p.x < at.x ? a : b;
                stars.Emit(p, Random.insideUnitCircle * .5f + Vector2.up * .4f, Random.Range(.5f, 1.3f), Random.Range(.16f, .38f), Color.Lerp(style.Hot, Color.white, .5f), DbpuFx.A(style.Mid, 0f), .4f, 0f, 0f, false, -1f, .9f);
            }
        }

        // The aura boiling off a hand that is holding the beam back.
        public void Flare(int index, Vector2 hand, float s, DbpuPalette palette, float dt)
        {
            var flare = index == 0 ? flareA : flareB;
            flare.Hot = Color.Lerp(palette.Hot, Color.white, .4f); flare.Mid = palette.Vivid; flare.Deep = palette.Deep; flare.Speed = 1.7f;
            flare.Opacity = .72f + s * .28f;
            flare.Place(hand + Vector2.up * (.22f + s * .3f), .95f + s * 1.25f + .1f * Mathf.Sin(clock * 27f + index));
            flare.Apply(dt);
        }

        public void HideFlare(int index) { (index == 0 ? flareA : flareB).Hide(); }

        public void End(float burst)
        {
            orb.Destroy(); crown.Destroy(); crownBack.Destroy(); rays.Destroy(); flareA.Destroy(); flareB.Destroy(); arc.Destroy();
            if (loop) Object.Destroy(loop);
            if (burst <= 0f) return;
            Burst(at, axis, a, twoSided ? b : a, burst);
        }

        // A thin, broken shock front racing outward.
        public static void Shock(Vector2 at, BeamStyle style, float radius, float seconds)
        {
            var ring = new BeamQuad("DBPU beam shock", BeamQuad.Arc, "Background", -6);
            ring.Style(style); ring.Dir = Random.insideUnitCircle.normalized; ring.Spread = 1f; ring.Power = .25f;
            BeamPhysics.Fade(ring, at, radius, seconds, .05f, true);
        }

        // One violent release, sized by scale.
        public static void Burst(Vector2 at, Vector2 axis, BeamStyle first, BeamStyle second, float scale)
        {
            float s = Mathf.Clamp(scale, .2f, 4f);
            var flash = new BeamQuad("DBPU beam burst orb", BeamQuad.Orb, "Background", -5);
            flash.Style(first); flash.Hot = Color.white; flash.Speed = 2f;
            BeamPhysics.Fade(flash, at, .9f + s * 1.1f, .2f + s * .05f, .4f);
            var spikes = new BeamQuad("DBPU beam burst crown", BeamQuad.Crown, "Background", -6);
            spikes.Style(first); spikes.Speed = 2.6f;
            BeamPhysics.Fade(spikes, at, 3.2f + s * 3.6f, .28f + s * .06f, 1.15f);
            var shafts = new BeamQuad("DBPU beam burst shafts", BeamQuad.Rays, "Background", -29);
            shafts.Style(first); shafts.Alt = second.Mid; shafts.Dir = axis; shafts.Speed = 1.4f;
            BeamPhysics.Fade(shafts, at, 9f + s * 8f, .4f + s * .08f, .3f);
            var crescent = new BeamQuad("DBPU beam burst arc", BeamQuad.Arc, "Background", -4);
            crescent.Style(first); crescent.Dir = DbpuFx.Rotate(Vector2.up, Random.Range(-1f, 1f)); crescent.Spread = .6f; crescent.Power = .3f;
            BeamPhysics.Fade(crescent, at, 6f + s * 5f, .42f, .1f, true);
            Shock(at, first, 3.4f + s * 4.4f, .45f);
            SingularityFx.Burst(at, first.Mid, 1f + s, 18 + (int)(s * 20f));
            KiSpaceRefraction.Pulse(at, 3f + s * 3.2f, .5f + s * .3f, .55f + s * .2f, axis);
            var shards = BeamParticles.Shards; var sparks = BeamParticles.Sparks;
            for (int i = 0; i < 18 + (int)(s * 26f); i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var style = Random.value < .5f ? first : second;
                shards.Emit(at + dir * .2f, dir * Random.Range(6f, 22f) * (.7f + s * .3f), Random.Range(.3f, .8f), Random.Range(.14f, .5f), Color.Lerp(style.Mid, Color.white, Random.Range(.2f, .8f)), DbpuFx.A(style.Mid, .3f), 1.8f, 0f, Random.Range(-720f, 720f));
                sparks.Emit(at, dir * Random.Range(8f, 34f) * (.7f + s * .3f), Random.Range(.2f, .55f), Random.Range(.06f, .16f), Color.white, DbpuFx.A(style.Mid, 0f), 2f, 4f, 0f, true);
            }
            for (int i = 0; i < 4 + (int)(s * 3f); i++)
                SingularityFx.Lightning(at, at + Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f + s * 3f), Color.Lerp(first.Mid, Color.white, .6f), .07f, .22f, 12, .55f);
            DbpuFx.Shake(at, 1.2f + s * 1.6f);
        }
    }

    // The sky takes on the colours of what is happening: a soft wash behind the fighters, split
    // across the clash when two sides are fighting over it.
    [DefaultExecutionOrder(320)]
    public sealed class BeamScene : MonoBehaviour
    {
        static BeamScene instance;
        BeamQuad wash; float level, wanted; Vector2 at, axis = Vector2.right; Color left = Color.black, right = Color.black;

        public static void Request(Vector2 point, Vector2 direction, Color from, Color to, float strength)
        {
            if (!BeamFxAssets.Ready) return;
            if (!instance)
            {
                var go = new GameObject("DBPU beam scene");
                instance = go.AddComponent<BeamScene>();
            }
            if (strength < instance.wanted) return;
            instance.wanted = strength; instance.at = point; instance.axis = direction; instance.left = from; instance.right = to;
        }

        void LateUpdate()
        {
            level = Mathf.MoveTowards(level, wanted, Time.unscaledDeltaTime * (wanted > level ? 1.4f : .9f));
            wanted = 0f;
            var cam = Global.main ? Global.main.camera : Camera.main;
            if (!cam) return;
            if (level <= .002f) { if (wash != null) wash.Hide(); return; }
            if (wash == null) wash = new BeamQuad("DBPU beam sky wash", BeamQuad.Wash, "Bottom", 30000);
            wash.Mid = Color.Lerp(left, Color.black, .45f); wash.Alt = Color.Lerp(right, Color.black, .45f);
            wash.Dir = axis; wash.Power = 1f; wash.Opacity = level * .82f;
            float view = Mathf.Max(cam.orthographicSize, 4f) * Mathf.Max(cam.aspect, 1f);
            wash.Place(at, view * 4.2f);
            wash.Apply(Time.unscaledDeltaTime);
        }

        void OnDestroy() { if (wash != null) wash.Destroy(); }
    }
}
