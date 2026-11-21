using System.Collections.Generic;
using CargoSort.Core;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// Draws World Tour pictures (a city skyline, one landmark, a city stamp) into small textures, from the
    /// shared shape data in <see cref="WorldTour"/>. Same look as the SVG drawings in the web preview.
    /// Pictures are cached, so each one is drawn once.
    /// </summary>
    public static class TourArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Silhouette = new Color(1, 1, 1, 0.17f);
        private static readonly Color GroundStrip = new Color(20 / 255f, 30 / 255f, 50 / 255f, 0.55f);
        private static readonly Color Sun = new Color(1, 1, 1, 0.55f);
        private static readonly Color Warm = new Color32(0xFF, 0xD7, 0x5E, 0xFF);

        /// <summary>A city's skyline (100 x 60): all 4 landmarks, built ones in color.</summary>
        public static Sprite Skyline(int city, int mask)
        {
            return Get($"sky{city}_{mask}", 0, 0, 100, 60, 500, 300, city, c =>
            {
                c.Circle(78, 16, 7, Sun);
                c.Rect(-10, 54, 120, 10, GroundStrip);
                for (int i = 0; i < WorldTour.MarksPerCity; i++) c.Landmark(WorldTour.Cities[city].Marks[i], WorldTour.Slots[i], 54, 0.5f, WorldTour.IsBuilt(mask, i));
            });
        }

        /// <summary>One landmark on its city's sky (square), for the landmark list.</summary>
        public static Sprite Mark(int city, int mark, bool built)
        {
            return Get($"mark{city}_{mark}_{built}", 0, 0, 60, 60, 180, 180, city, c =>
            {
                c.Rect(-10, 56, 80, 10, GroundStrip);
                c.Landmark(WorldTour.Cities[city].Marks[mark], 30, 56, 0.52f, built);
            });
        }

        /// <summary>A square stamp of a city (the middle of its skyline) for the cities grid.</summary>
        public static Sprite Stamp(int city, int mask)
        {
            return Get($"stamp{city}_{mask}", 20, 4, 60, 56, 180, 168, city, c =>
            {
                c.Rect(-10, 54, 120, 10, GroundStrip);
                for (int i = 0; i < WorldTour.MarksPerCity; i++) c.Landmark(WorldTour.Cities[city].Marks[i], WorldTour.Slots[i], 54, 0.5f, WorldTour.IsBuilt(mask, i));
            });
        }

        private static Sprite Get(string key, float vx, float vy, float vw, float vh, int w, int h, int city, System.Action<Canvas> draw)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var canvas = new Canvas(vx, vy, vw, vh, w, h);
            var c = WorldTour.Cities[city];
            canvas.Sky(Hex(c.SkyHigh), Hex(c.SkyLow));
            draw(canvas);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(canvas.Pixels());
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = sprite;
            return sprite;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        private static Color ShadeOf(char shade, Color main)
        {
            switch (shade)
            {
                case 'd': return new Color(main.r * 0.72f, main.g * 0.72f, main.b * 0.72f, 1);
                case 'l': return Color.Lerp(main, Color.white, 0.6f);
                case 'w': return Warm;
                default: return main;
            }
        }

        /// <summary>A tiny software painter in SVG coordinates (y down), with 3 x 3 samples per pixel for smooth edges.</summary>
        private sealed class Canvas
        {
            private readonly float vx, vy, vw, vh;
            private readonly int w, h;
            private readonly Color[] px;

            public Canvas(float vx, float vy, float vw, float vh, int w, int h)
            {
                this.vx = vx; this.vy = vy; this.vw = vw; this.vh = vh; this.w = w; this.h = h;
                px = new Color[w * h];
            }

            /// <summary>Same as the preview: a gradient over a sky box from y = -100 to y = 300.</summary>
            public void Sky(Color high, Color low)
            {
                for (int y = 0; y < h; y++)
                {
                    float sy = vy + (y + 0.5f) / h * vh;
                    var col = Color.Lerp(high, low, Mathf.Clamp01((sy + 100) / 400f));
                    for (int x = 0; x < w; x++) px[y * w + x] = col;
                }
            }

            public void Rect(float x, float y, float rw, float rh, Color col) =>
                Fill(x, y, x + rw, y + rh, col, (sx, sy) => sx >= x && sx <= x + rw && sy >= y && sy <= y + rh);

            public void Circle(float cx, float cy, float r, Color col) =>
                Fill(cx - r, cy - r, cx + r, cy + r, col, (sx, sy) => (sx - cx) * (sx - cx) + (sy - cy) * (sy - cy) <= r * r);

            public void Triangle(float x1, float y1, float x2, float y2, float x3, float y3, Color col)
            {
                Fill(Mathf.Min(x1, Mathf.Min(x2, x3)), Mathf.Min(y1, Mathf.Min(y2, y3)), Mathf.Max(x1, Mathf.Max(x2, x3)), Mathf.Max(y1, Mathf.Max(y2, y3)), col, (sx, sy) =>
                {
                    float d1 = (sx - x2) * (y1 - y2) - (x1 - x2) * (sy - y2);
                    float d2 = (sx - x3) * (y2 - y3) - (x2 - x3) * (sy - y3);
                    float d3 = (sx - x1) * (y3 - y1) - (x3 - x1) * (sy - y1);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    return !(neg && pos);
                });
            }

            /// <summary>A landmark standing on the ground at (x, ground), scale s. Not built = a faint silhouette.</summary>
            public void Landmark(WorldTour.Landmark mark, float x, float ground, float s, bool built)
            {
                var main = Hex(mark.Color);
                foreach (var p in WorldTour.Shapes[mark.Shape])
                {
                    var col = built ? ShadeOf(p.Shade, main) : Silhouette;
                    var n = p.N;
                    switch (p.Kind)
                    {
                        case 'r': Rect(x + n[0] * s, ground - (n[1] + n[3]) * s, n[2] * s, n[3] * s, col); break;
                        case 'c': Circle(x + n[0] * s, ground - n[1] * s, n[2] * s, col); break;
                        default: Triangle(x + n[0] * s, ground - n[1] * s, x + n[2] * s, ground - n[3] * s, x + n[4] * s, ground - n[5] * s, col); break;
                    }
                }
            }

            private void Fill(float minX, float minY, float maxX, float maxY, Color col, System.Func<float, float, bool> inside)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - vx) / vw * w) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt((maxX - vx) / vw * w) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt((minY - vy) / vh * h) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt((maxY - vy) / vh * h) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int hits = 0;
                        for (int j = 0; j < 3; j++)
                            for (int i = 0; i < 3; i++)
                                if (inside(vx + (x + (i + 0.5f) / 3f) / w * vw, vy + (y + (j + 0.5f) / 3f) / h * vh)) hits++;
                        if (hits == 0) continue;
                        float a = col.a * hits / 9f;
                        ref var d = ref px[y * w + x];
                        d = new Color(d.r + (col.r - d.r) * a, d.g + (col.g - d.g) * a, d.b + (col.b - d.b) * a, 1);
                    }
            }

            /// <summary>Unity textures start at the bottom row.</summary>
            public Color32[] Pixels()
            {
                var o = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++) o[(h - 1 - y) * w + x] = px[y * w + x];
                return o;
            }
        }
    }
}
