using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Every shape the menus use lives on one generated texture, so whole windows batch into a
    // couple of draw calls and the mod ships no extra UI art. Built once and kept for the session.
    public static class DbAtlas
    {
        const int Size = 1024;
        static Texture2D texture;
        static Color32[] pixels;
        static int cursorX = 2, cursorY = 2, shelf;

        public static Sprite Chamfer, ChamferLine, ChamferTight, ChamferTightLine, Round, RoundLine, Pill, PillLine, Slant, SlantLine;
        public static Sprite Disc, Ring, RingThin, Glow, EdgeGlow, GradUp, GradRight, Hairline, Streaks, Halftone, Burst, Wedge;
        public static Sprite Star, Close, Check, Chevron, ChevronLeft, Search, Grip, Plus, Minus, Reset, Bolt;
        public static readonly Sprite[] DragonBall = new Sprite[8];

        public static void Ensure()
        {
            if (texture) return;
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false)
            {
                name = "DBS UI Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            pixels = new Color32[Size * Size];
            cursorX = 2; cursorY = 2; shelf = 0;
            Build();
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            pixels = null;
        }

        static void Build()
        {
            var chamfer = ChamferPolygon(96, 96, 22f);
            Chamfer = Add(96, 96, 30, (x, y) => Fill(PolygonDistance(x, y, chamfer)));
            ChamferLine = Add(96, 96, 30, (x, y) => Stroke(PolygonDistance(x, y, chamfer), 1.6f));
            var tight = ChamferPolygon(48, 48, 10f);
            ChamferTight = Add(48, 48, 14, (x, y) => Fill(PolygonDistance(x, y, tight)));
            ChamferTightLine = Add(48, 48, 14, (x, y) => Stroke(PolygonDistance(x, y, tight), 1.4f));

            Round = Add(48, 48, 14, (x, y) => Fill(RoundedBox(x, y, 48, 48, 9f)));
            RoundLine = Add(48, 48, 14, (x, y) => Stroke(RoundedBox(x, y, 48, 48, 9f), 1.4f));
            Pill = Add(64, 64, 31, (x, y) => Fill(RoundedBox(x, y, 64, 64, 31.5f)));
            PillLine = Add(64, 64, 31, (x, y) => Stroke(RoundedBox(x, y, 64, 64, 31.5f), 1.5f));

            var slant = new[] { new Vector2(0.5f, 1), new Vector2(73, 1), new Vector2(95.5f, 47), new Vector2(22, 47) };
            Slant = Add(96, 48, new Vector4(26, 0, 26, 0), (x, y) => Fill(PolygonDistance(x, y, slant)));
            SlantLine = Add(96, 48, new Vector4(26, 0, 26, 0), (x, y) => Stroke(PolygonDistance(x, y, slant), 1.5f));

            Disc = Add(96, 96, 0, (x, y) => Fill(Length(x - 48, y - 48) - 47f));
            Ring = Add(160, 160, 0, (x, y) => Stroke(Length(x - 80, y - 80) - 76f, 7f));
            RingThin = Add(160, 160, 0, (x, y) => Stroke(Length(x - 80, y - 80) - 78f, 1.8f));
            Glow = Add(128, 128, 0, (x, y) =>
            {
                float d = Mathf.Clamp01(1f - Length(x - 64, y - 64) / 63f);
                return d * d * (3f - 2f * d) * d;
            });
            EdgeGlow = Add(96, 96, 40, (x, y) =>
            {
                float d = RoundedBox(x, y, 96, 96, 34f);
                float t = Mathf.Clamp01(1f - Mathf.Abs(d + 16f) / 16f);
                return t * t;
            });
            GradUp = Add(8, 96, 0, (x, y) => y / 95f);
            GradRight = Add(96, 8, 0, (x, y) => x / 95f);
            Hairline = Add(96, 4, new Vector4(2, 0, 2, 0), (x, y) => Mathf.Sin(Mathf.Clamp01(x / 95f) * Mathf.PI));
            Streaks = Add(256, 64, 0, StreakPattern);
            Halftone = Add(32, 32, 0, (x, y) => Fill(Length(Wrap(x, 16) - 8, Wrap(y, 16) - 8) - 2.2f));
            Burst = Add(256, 256, 0, BurstPattern);
            Wedge = Add(128, 128, 0, (x, y) => Mathf.Clamp01(1f - Length(x - 64, y - 64) / 63f));

            var star = StarPolygon(32f, 32f, 30f, 12.5f);
            Star = Add(64, 64, 0, (x, y) => Fill(PolygonDistance(x, y, star)));
            Close = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 8, 8, 24, 24, 1.8f), Capsule(x, y, 24, 8, 8, 24, 1.8f))));
            Check = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 7, 16, 13, 9, 1.9f), Capsule(x, y, 13, 9, 25, 24, 1.9f))));
            Chevron = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 12, 8, 21, 16, 2f), Capsule(x, y, 21, 16, 12, 24, 2f))));
            ChevronLeft = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 20, 8, 11, 16, 2f), Capsule(x, y, 11, 16, 20, 24, 2f))));
            Search = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Mathf.Abs(Length(x - 14, y - 18) - 7.5f) - 1.5f, Capsule(x, y, 19.5f, 12.5f, 26, 6, 1.9f))));
            Grip = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 27, 6, 6, 27, 1.2f), Mathf.Min(Capsule(x, y, 27, 14, 14, 27, 1.2f), Capsule(x, y, 27, 22, 22, 27, 1.2f)))));
            Plus = Add(32, 32, 0, (x, y) => Fill(Mathf.Min(Capsule(x, y, 16, 7, 16, 25, 1.9f), Capsule(x, y, 7, 16, 25, 16, 1.9f))));
            Minus = Add(32, 32, 0, (x, y) => Fill(Capsule(x, y, 7, 16, 25, 16, 1.9f)));
            Reset = Add(32, 32, 0, ResetIcon);
            var bolt = new[] { new Vector2(18, 29), new Vector2(7, 14), new Vector2(15, 14), new Vector2(12, 3), new Vector2(25, 18), new Vector2(16.5f, 18) };
            Bolt = Add(32, 32, 0, (x, y) => Fill(PolygonDistance(x, y, bolt)));

            for (int stars = 1; stars <= 7; stars++) DragonBall[stars] = AddBall(stars);
        }

        static float ResetIcon(float x, float y)
        {
            float d = Length(x - 16, y - 16);
            float angle = Mathf.Atan2(y - 16, x - 16);
            float arc = Mathf.Abs(d - 9f) - 1.7f;
            if (angle > .25f && angle < 1.35f) arc = 9f;
            float head = Mathf.Min(Capsule(x, y, 25, 21, 25, 13.5f, 1.7f), Capsule(x, y, 25, 21, 18, 21, 1.7f));
            return Fill(Mathf.Min(arc, head));
        }

        static float StreakPattern(float x, float y)
        {
            float value = 0f;
            for (int i = 0; i < 9; i++)
            {
                float row = 3.5f + i * 7f;
                float start = (i * 83) % 256;
                float span = 48f + (i * 37) % 96;
                float along = Wrap(x - start, 256f);
                if (along > span) continue;
                float t = along / span;
                float thin = .7f + (i % 3) * .3f;
                float line = Mathf.Clamp01(thin + .5f - Mathf.Abs(y - row));
                value = Mathf.Max(value, line * Mathf.Sin(t * Mathf.PI));
            }
            return value;
        }

        static float BurstPattern(float x, float y)
        {
            float dx = x - 128, dy = y - 128;
            float d = Length(dx, dy) / 127f;
            if (d >= 1f) return 0f;
            float angle = Mathf.Atan2(dy, dx);
            float rays = Mathf.Abs(Mathf.Sin(angle * 11f)) * .6f + Mathf.Abs(Mathf.Sin(angle * 23f + 1.3f)) * .4f;
            rays = Mathf.Pow(rays, 6f);
            return rays * Mathf.Clamp01((d - .2f) * 3f) * Mathf.Clamp01((1f - d) * 2.5f);
        }

        // Round, orange and starred: the same seven-ball motif the mod's art uses.
        static Sprite AddBall(int stars)
        {
            const int size = 64;
            var centres = StarLayout(stars);
            return AddColored(size, size, (x, y) =>
            {
                float dx = x - 32f, dy = y - 32f;
                float d = Length(dx, dy);
                float alpha = Mathf.Clamp01(30.5f - d);
                float light = Mathf.Clamp01(1f - Length(dx + 9f, dy - 11f) / 40f);
                Color c = Color.Lerp(DbTheme.Hex(0xC85A08), DbTheme.Hex(0xFFA11F), light * 1.25f);
                c = Color.Lerp(c, DbTheme.Hex(0xFFD98A), Mathf.Pow(light, 3f));
                c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - Length(dx + 11f, dy - 14f) / 7f) * .9f);
                float star = 0f;
                for (int i = 0; i < centres.Count; i++)
                {
                    float sx = centres[i].x, sy = centres[i].y;
                    star = Mathf.Max(star, Fill(StarDistance(x - sx, y - sy, 4.6f, 1.9f)));
                }
                c = Color.Lerp(c, DbTheme.Hex(0xD7141A), star);
                c.a = alpha;
                return c;
            });
        }

        static List<Vector2> StarLayout(int stars)
        {
            var list = new List<Vector2>();
            if (stars == 1) { list.Add(new Vector2(32, 32)); return list; }
            if (stars == 7) list.Add(new Vector2(32, 32));
            int ring = stars == 7 ? 6 : stars;
            float radius = ring <= 2 ? 9f : ring == 3 ? 10.5f : 13f;
            for (int i = 0; i < ring; i++)
            {
                float a = Mathf.PI * .5f + i * Mathf.PI * 2f / ring;
                list.Add(new Vector2(32 + Mathf.Cos(a) * radius, 32 + Mathf.Sin(a) * radius));
            }
            return list;
        }

        static Sprite Add(int w, int h, float border, Func<float, float, float> coverage)
        {
            return Add(w, h, new Vector4(border, border, border, border), coverage);
        }

        static Sprite Add(int w, int h, Vector4 border, Func<float, float, float> coverage)
        {
            return Place(w, h, border, (x, y) =>
            {
                float a = Mathf.Clamp01(coverage(x + .5f, y + .5f));
                return new Color(1f, 1f, 1f, a);
            });
        }

        static Sprite AddColored(int w, int h, Func<float, float, Color> shade)
        {
            return Place(w, h, Vector4.zero, (x, y) => shade(x + .5f, y + .5f));
        }

        static Sprite Place(int w, int h, Vector4 border, Func<int, int, Color> shade)
        {
            if (cursorX + w + 2 > Size) { cursorX = 2; cursorY += shelf + 3; shelf = 0; }
            if (cursorY + h + 2 > Size) throw new InvalidOperationException("DBS UI atlas is full");
            int ox = cursorX, oy = cursorY;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[(oy + y) * Size + ox + x] = (Color32)shade(x, y);
            cursorX += w + 3;
            shelf = Mathf.Max(shelf, h);
            var sprite = Sprite.Create(texture, new Rect(ox, oy, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static float Fill(float distance) { return Mathf.Clamp01(.5f - distance); }
        static float Stroke(float distance, float width) { return Mathf.Clamp01(.5f - (Mathf.Abs(distance + width * .5f) - width * .5f)); }
        static float Length(float x, float y) { return Mathf.Sqrt(x * x + y * y); }
        static float Wrap(float value, float size) { value %= size; return value < 0f ? value + size : value; }

        static float RoundedBox(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * .5f) - (w * .5f - r - .5f);
            float qy = Mathf.Abs(y - h * .5f) - (h * .5f - r - .5f);
            float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
            return Length(ax, ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            return Length(px - dx * t, py - dy * t) - r;
        }

        // Top-right and bottom-left corners cut at 45 degrees: the signature silhouette of the menus.
        static Vector2[] ChamferPolygon(float w, float h, float cut)
        {
            const float m = .5f;
            return new[]
            {
                new Vector2(m + cut, m), new Vector2(w - m, m), new Vector2(w - m, h - m - cut),
                new Vector2(w - m - cut, h - m), new Vector2(m, h - m), new Vector2(m, m + cut)
            };
        }

        static Vector2[] StarPolygon(float cx, float cy, float outer, float inner)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * .5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return points;
        }

        static float StarDistance(float x, float y, float outer, float inner)
        {
            // Folded star distance, cheap enough to evaluate per pixel for every star.
            float a = Mathf.Atan2(x, y);
            float sector = Mathf.PI * 2f / 5f;
            a = Mathf.Abs(Wrap(a, sector) - sector * .5f);
            float d = Length(x, y);
            float px = Mathf.Sin(a) * d, py = Mathf.Cos(a) * d;
            float ax = Mathf.Sin(sector * .5f) * outer, ay = Mathf.Cos(sector * .5f) * outer;
            float bx = 0f, by = inner;
            float ex = bx - ax, ey = by - ay;
            float t = Mathf.Clamp01(((px - ax) * ex + (py - ay) * ey) / (ex * ex + ey * ey));
            float dist = Length(px - ax - ex * t, py - ay - ey * t);
            float side = (px - ax) * ey - (py - ay) * ex;
            return side > 0f ? dist : -dist;
        }

        static float PolygonDistance(float x, float y, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                float ex = b.x - a.x, ey = b.y - a.y;
                float wx = x - a.x, wy = y - a.y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float dx = wx - ex * t, dy = wy - ey * t;
                best = Mathf.Min(best, dx * dx + dy * dy);
                if ((a.y > y) != (b.y > y) && x < ex * (y - a.y) / ey + a.x) inside = !inside;
            }
            float distance = Mathf.Sqrt(best);
            return inside ? -distance : distance;
        }
    }
}
