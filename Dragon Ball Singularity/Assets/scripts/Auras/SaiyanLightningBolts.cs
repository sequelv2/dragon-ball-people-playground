using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The SSJ2/3 lightning, drawn after the user's first reference GIF: flashes decided about 16 times a second
    // (a flash holds for 1 to 5 drawings and every drawing is a new shape), bolts that run part of the way round
    // the aura's outline with hard right-angled steps and sharp spikes, now and then one straight across, forks
    // and sparks. Produces the points SaiyanLightning.shader takes, in world units from the aura quad's centre.
    // Depends on UnityEngine only.
    public sealed class SaiyanLightningBolts
    {
        public const int MaxPoints = 96;
        const float Rate = 1000f / 60f;
        public readonly Vector4[] Points = new Vector4[MaxPoints];
        public int Count { get; private set; }
        public bool On { get; private set; }
        public float Brightness { get; private set; }

        readonly System.Random random;
        readonly List<Vector2> anchors = new List<Vector2>(32);
        float timer;
        int run;
        // The egg, in world units from the quad's centre: centre, half width, height above and below the centre.
        Vector2 centre;
        float halfWidth, up, down, unit, width;

        public SaiyanLightningBolts(int seed) { random = new System.Random(seed * 7919 + 31); }

        float Range(float a, float b) { return a + (b - a) * (float)random.NextDouble(); }

        public void Step(float delta, int level, float charge, Vector2 eggCentre, float eggHalfWidth, float eggUp, float eggDown, float bodyUnit)
        {
            centre = eggCentre; halfWidth = eggHalfWidth; up = eggUp; down = eggDown; unit = bodyUnit;
            timer += delta;
            if (timer < 1f / Rate) return;
            timer = Mathf.Min(timer - 1f / Rate, 1f / Rate);
            bool ssj3 = level >= 3;
            float chance = (ssj3 ? .42f : .3f) * (1 + charge * .8f);
            float hold = ssj3 ? .6f : .55f;
            if (On) On = run < 5 && random.NextDouble() < hold;
            else On = random.NextDouble() < chance;
            if (!On) { run = 0; return; }
            run++;
            Brightness = run == 1 ? 1f : .88f;
            width = unit * (ssj3 ? .034f : .03f) * (1 + charge * .2f);
            Generate(ssj3);
        }

        // A point of the egg at angle theta from the top and radius rho (1 = the aura's edge).
        Vector2 Outline(float theta, float rho)
        {
            float ex = Mathf.Sin(theta) * rho, ey = Mathf.Cos(theta) * rho;
            float lift = Mathf.Max(ey, 0);
            return centre + new Vector2(ex * halfWidth / (1 + .6f * lift * lift), ey > 0 ? ey * up : ey * down);
        }

        void Add(Vector2 p, float halfThickness, float kind)
        {
            if (Count >= MaxPoints) return;
            Points[Count++] = new Vector4(p.x, p.y, halfThickness, kind);
        }

        // The reference's line: straight runs with right-angled steps (out, along, back) and sharp spikes off
        // the path; thick in the middle, thin at the ends.
        void Jagged(float scale)
        {
            if (anchors.Count < 2 || Count + anchors.Count * 3 + 8 > MaxPoints) return;
            int start = Count;
            float total = anchors.Count - 1;
            System.Func<float, float> thickness = t => width * scale * (.4f + .6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            Add(anchors[0], thickness(0), 0);
            for (int i = 1; i < anchors.Count; i++)
            {
                Vector2 a = anchors[i - 1], b = anchors[i], along = b - a;
                if (along.sqrMagnitude < 1e-8f) continue;
                Vector2 normal = new Vector2(-along.y, along.x).normalized;
                float side = random.NextDouble() < .5 ? -1 : 1;
                double roll = random.NextDouble();
                float t0 = (i - 1) / total, t1 = i / total;
                if (roll < .28 && i < anchors.Count - 1)
                {
                    float off = Range(.05f, .09f) * unit * side;
                    float s = Range(.3f, .45f), e = Range(.55f, .7f);
                    Add(a + along * s + normal * off * .1f, thickness(Mathf.Lerp(t0, t1, s)), 1);
                    Add(a + along * s + normal * off, thickness(Mathf.Lerp(t0, t1, s)), 1);
                    Add(a + along * e + normal * off, thickness(Mathf.Lerp(t0, t1, e)), 1);
                }
                else if (roll < .5 && i < anchors.Count - 1)
                {
                    float s = Range(.4f, .6f);
                    Add(a + along * s + normal * Range(.06f, .12f) * unit * side, thickness(Mathf.Lerp(t0, t1, s)) * .8f, 1);
                }
                Add(b, thickness(t1), 1);
            }
            // A fork off one of the corners, heading away from the middle of the aura.
            if (random.NextDouble() < .55 && Count - start > 3)
            {
                Vector2 from = Points[start + random.Next(1, Count - start - 1)];
                Vector2 heading = (from - centre).normalized;
                heading = (heading + new Vector2(Range(-.6f, .6f), Range(-.3f, .6f))).normalized;
                Add(from, width * scale * .6f, 0);
                int segments = random.Next(2, 4);
                for (int s = 0; s < segments; s++)
                {
                    from += heading * Range(.07f, .13f) * unit + new Vector2(-heading.y, heading.x) * Range(-.05f, .05f) * unit;
                    Add(from, width * scale * (.6f - .15f * s), 1);
                }
            }
            // Sparks thrown off the line.
            int sparks = random.Next(1, 4);
            for (int s = 0; s < sparks && Count > start; s++)
            {
                Vector2 near = Points[start + random.Next(0, Count - start)];
                Add(near + new Vector2(Range(-.1f, .1f), Range(-.06f, .14f)) * unit, width * Range(.7f, 1.2f), 2);
            }
        }

        void Generate(bool ssj3)
        {
            Count = 0;
            int bolts = ssj3 ? random.Next(2, 4) : random.Next(1, 3);
            for (int b = 0; b < bolts; b++)
            {
                // Round part of the aura's outline, like the reference's loops round its aura.
                float theta = Range(-Mathf.PI, Mathf.PI);
                float span = Range(.7f, 1.5f) * (random.NextDouble() < .5 ? -1 : 1);
                float rho = Range(.6f, .95f);
                int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(span) * 5.5f), 4, 9);
                anchors.Clear();
                for (int i = 0; i <= steps; i++)
                    anchors.Add(Outline(theta + span * i / steps + Range(-.05f, .05f), Mathf.Clamp(rho + Range(-.06f, .06f), .45f, .97f)));
                Jagged(1f);
            }
            if (random.NextDouble() < (ssj3 ? .5 : .3))
            {
                // Now and then a bolt straight across the aura.
                float side = random.NextDouble() < .5 ? -1 : 1;
                float height = Range(.35f, .7f) * Mathf.PI;
                Vector2 a = Outline(side * height, Range(.75f, .92f)), c = Outline(-side * (height + Range(-.25f, .25f)), Range(.75f, .92f));
                int steps = Mathf.Clamp(Mathf.RoundToInt((c - a).magnitude / (.2f * unit)), 4, 9);
                anchors.Clear();
                for (int i = 0; i <= steps; i++)
                    anchors.Add(Vector2.Lerp(a, c, i / (float)steps) + new Vector2(0, (i == 0 || i == steps) ? 0 : Range(-.07f, .07f) * unit));
                Jagged(.85f);
            }
        }

        public float GlowWidth { get { return .05f * unit; } }
        public float BlurWidth { get { return .16f * unit; } }
    }
}
