using System;
using System.Collections.Generic;

namespace CargoSort.Core
{
    /// <summary>
    /// World Tour (the meta layer): winning levels gives stars, stars build a city's 4 landmarks (each pays
    /// coins), and finishing a city gives a big reward and moves the tour to the next city.
    /// Same data and rules as the web preview (web-preview/index.html, "World Tour").
    /// </summary>
    public static class WorldTour
    {
        /// <summary>One shape of a landmark drawing. Kind: 'r' rect (x, y, w, h from bottom-left), 'c' circle
        /// (cx, cy, r), 't' triangle (x1 y1 x2 y2 x3 y3). Shade: 'm' main, 'd' darker, 'l' light, 'w' warm light.
        /// Coordinates: x about -30..30, y up from the ground (0..100).</summary>
        public readonly struct Part
        {
            public readonly char Kind, Shade;
            public readonly float[] N;
            public Part(char kind, char shade, float[] n) { Kind = kind; Shade = shade; N = n; }
        }

        public sealed class Landmark
        {
            public readonly string Shape, Key;   // Key: name in Loc ("l_" + Key)
            public readonly int Color;           // 0xRRGGBB
            public Landmark(string shape, int color, string key) { Shape = shape; Color = color; Key = key; }
        }

        public sealed class City
        {
            public readonly string Key;          // name in Loc ("c_" + Key)
            public readonly int SkyLow, SkyHigh; // 0xRRGGBB, near the ground and at the top
            public readonly Landmark[] Marks;
            public City(string key, int skyLow, int skyHigh, params Landmark[] marks) { Key = key; SkyLow = skyLow; SkyHigh = skyHigh; Marks = marks; }
        }

        private static Part P(string ks, params float[] n) => new Part(ks[0], ks[1], n);

        public static readonly Dictionary<string, Part[]> Shapes = new Dictionary<string, Part[]>
        {
            ["tower"] = new[]
            {
                P("td", -10, 0, 10, 0, 0, 24),
                P("rm", -2.2f, 0, 4.4f, 72),
                P("rd", -9, 57, 18, 6),
                P("cm", 0, 64, 7.5f),
                P("rl", -7, 63, 14, 1.6f),
                P("rd", -0.8f, 72, 1.6f, 24),
            },
            ["arch"] = new[]
            {
                P("rm", -17, 0, 10, 42),
                P("rm", 7, 0, 10, 42),
                P("rm", -17, 40, 34, 13),
                P("rd", -17, 40, 34, 3),
                P("tm", -12, 53, 12, 53, 0, 66),
                P("rl", -3, 46, 6, 4),
            },
            ["dome"] = new[]
            {
                P("rm", -15, 0, 30, 26),
                P("cm", 0, 26, 13),
                P("rd", -1, 38, 2, 9),
                P("rd", -4, 0, 8, 12),
                P("rd", -22, 0, 3.4f, 44),
                P("rd", 18.6f, 0, 3.4f, 44),
                P("td", -22.5f, 44, -18, 44, -20.3f, 53),
                P("td", 18, 44, 22.5f, 44, 20.3f, 53),
                P("cl", -7, 16, 2),
                P("cl", 7, 16, 2),
            },
            ["bridge"] = new[]
            {
                P("rd", -30, 15, 60, 4),
                P("rm", -17, 0, 4, 46),
                P("rm", 13, 0, 4, 46),
                P("rd", -17, 38, 4, 2),
                P("rd", 13, 38, 4, 2),
                P("tm", -15, 46, -29, 19, -27, 19),
                P("tm", -15, 46, -1, 19, -3, 19),
                P("tm", 15, 46, 29, 19, 27, 19),
                P("tm", 15, 46, 1, 19, 3, 19),
            },
            ["pyramid"] = new[]
            {
                P("tm", -28, 0, 28, 0, 0, 42),
                P("td", 0, 42, 28, 0, 8, 0),
                P("tl", -3, 38, 3, 38, 0, 42),
            },
            ["skyscraper"] = new[]
            {
                P("rm", -9, 0, 18, 30),
                P("rm", -6.5f, 30, 13, 26),
                P("rm", -4.5f, 56, 9, 20),
                P("rm", -2.5f, 76, 5, 12),
                P("rd", -0.6f, 88, 1.2f, 14),
                P("rl", -1, 4, 2, 80),
                P("rd", -9, 28, 18, 2),
                P("rd", -6.5f, 54, 13, 2),
            },
            ["sail"] = new[]
            {
                P("rd", -13, 0, 4, 74),
                P("tm", -9, 2, -9, 70, 17, 8),
                P("tm", -9, 2, 17, 8, 15, 0),
                P("rl", -16, 52, 7, 2),
                P("rd", -15, 0, 30, 3),
            },
            ["frame"] = new[]
            {
                P("rm", -13, 0, 6, 62),
                P("rm", 7, 0, 6, 62),
                P("rm", -13, 56, 26, 7),
                P("rd", -13, 0, 26, 4),
                P("rw", -11, 58, 22, 2),
            },
            ["lattice"] = new[]
            {
                P("tm", -19, 0, -11, 0, -2.5f, 52),
                P("tm", 19, 0, 11, 0, 2.5f, 52),
                P("rd", -13, 20, 26, 3),
                P("rd", -6, 46, 12, 3),
                P("tm", -4.5f, 48, 4.5f, 48, 0, 92),
                P("rd", -0.6f, 90, 1.2f, 10),
                P("td", -8, 20, 8, 20, 0, 10),
            },
            ["clock"] = new[]
            {
                P("rm", -6.5f, 0, 13, 62),
                P("rd", -7.5f, 56, 15, 4),
                P("cl", 0, 48, 4.6f),
                P("cd", 0, 48, 1.2f),
                P("td", -7.5f, 60, 7.5f, 60, 0, 80),
                P("rd", -0.8f, 80, 1.6f, 7),
                P("rl", -2, 8, 4, 8),
            },
            ["wheel"] = new[]
            {
                P("td", 0, 36, 25.3f, 36, 26.7f, 36),
                P("td", 0, 36, 21.82f, 49, 23.22f, 49),
                P("td", 0, 36, 12.3f, 58.52f, 13.7f, 58.52f),
                P("td", 0, 36, -0.7f, 62, 0.7f, 62),
                P("td", 0, 36, -13.7f, 58.52f, -12.3f, 58.52f),
                P("td", 0, 36, -23.22f, 49, -21.82f, 49),
                P("td", 0, 36, -26.7f, 36, -25.3f, 36),
                P("td", 0, 36, -23.22f, 23, -21.82f, 23),
                P("td", 0, 36, -13.7f, 13.48f, -12.3f, 13.48f),
                P("td", 0, 36, -0.7f, 10, 0.7f, 10),
                P("td", 0, 36, 12.3f, 13.48f, 13.7f, 13.48f),
                P("td", 0, 36, 21.82f, 23, 23.22f, 23),
                P("cm", 26, 36, 3),
                P("cm", 22.52f, 49, 3),
                P("cm", 13, 58.52f, 3),
                P("cm", 0, 62, 3),
                P("cm", -13, 58.52f, 3),
                P("cm", -22.52f, 49, 3),
                P("cm", -26, 36, 3),
                P("cm", -22.52f, 23, 3),
                P("cm", -13, 13.48f, 3),
                P("cm", -0, 10, 3),
                P("cm", 13, 13.48f, 3),
                P("cm", 22.52f, 23, 3),
                P("cd", 0, 36, 3.5f),
                P("td", -14, 0, -10, 0, 0, 36),
                P("td", 14, 0, 10, 0, 0, 36),
            },
            ["statue"] = new[]
            {
                P("rd", -9, 0, 18, 24),
                P("rm", -7, 24, 14, 5),
                P("tm", -6.5f, 29, 6.5f, 29, 0, 63),
                P("cm", 0, 65, 4.2f),
                P("tm", -4.5f, 67, 4.5f, 67, 0, 74),
                P("rm", 3, 60, 2.6f, 17),
                P("cw", 4.3f, 80, 3.2f),
                P("rl", -7, 10, 14, 2),
            },
            ["pagoda"] = new[]
            {
                P("rd", -6, 0, 12, 14),
                P("tm", -17, 14, 17, 14, 0, 22),
                P("rd", -5, 21, 10, 10),
                P("tm", -14, 31, 14, 31, 0, 38),
                P("rd", -4, 37, 8, 9),
                P("tm", -11, 46, 11, 46, 0, 52),
                P("rd", -0.6f, 51, 1.2f, 13),
                P("rw", -2, 3, 4, 6),
            },
            ["torii"] = new[]
            {
                P("rm", -15, 0, 4.5f, 42),
                P("rm", 10.5f, 0, 4.5f, 42),
                P("rm", -19, 40, 38, 5),
                P("rm", -15, 32, 30, 3),
                P("rd", -21, 44, 42, 3),
            },
            ["mountain"] = new[]
            {
                P("tm", -30, 0, 30, 0, 0, 48),
                P("td", 0, 48, 30, 0, 10, 0),
                P("tl", -9.5f, 33, 9.5f, 33, 0, 48),
                P("tl", -9.5f, 33, -4, 33, -6.5f, 29),
                P("tl", -1, 33, 5, 33, 2, 29),
            },
            ["cluster"] = new[]
            {
                P("rm", -22, 0, 10, 40),
                P("rd", -11, 0, 10, 60),
                P("rm", 0, 0, 10, 50),
                P("rd", 11, 0, 10, 34),
                P("rw", -8, 8, 3, 44),
                P("rw", 3, 8, 3, 34),
                P("rw", -19, 8, 3, 26),
                P("rw", 14, 6, 3, 22),
            },
            ["gothic"] = new[]
            {
                P("rm", -15, 0, 30, 30),
                P("rm", -16, 0, 9, 50),
                P("rm", 7, 0, 9, 50),
                P("rd", -16, 48, 9, 2),
                P("rd", 7, 48, 9, 2),
                P("cl", 0, 22, 4.5f),
                P("td", -2.2f, 30, 2.2f, 30, 0, 64),
                P("rd", -3, 0, 6, 11),
            },
            ["spire"] = new[]
            {
                P("rm", -7.5f, 0, 15, 56),
                P("rm", -5.5f, 56, 11, 10),
                P("tl", -5.5f, 66, 5.5f, 66, 0, 86),
                P("rd", -0.5f, 86, 1, 12),
                P("rl", -1, 5, 2, 48),
                P("rw", -5, 5, 2, 40),
                P("rw", 3, 5, 2, 40),
            },
        };

        public static readonly City[] Cities =
        {
            new City("tehran", 0xFFB86B, 0xFF7A6B, new Landmark("tower", 0xE9E4DA, "milad"), new Landmark("arch", 0xF1ECE2, "azadi"), new Landmark("dome", 0x43B3CC, "mosque"), new Landmark("bridge", 0xE0A050, "nature")),
            new City("istanbul", 0xFF9E80, 0x9B5FD1, new Landmark("dome", 0xD88B6B, "hagia"), new Landmark("clock", 0xCDB48E, "galata"), new Landmark("bridge", 0xE85B5B, "bosphorus"), new Landmark("arch", 0xE7C47A, "bazaar")),
            new City("dubai", 0xFFD27A, 0xFF9A52, new Landmark("skyscraper", 0xCFE3F2, "khalifa"), new Landmark("sail", 0xF4F7FB, "arab"), new Landmark("frame", 0xF2B84B, "frame"), new Landmark("cluster", 0x8FB5D8, "marina")),
            new City("cairo", 0xFFCF7A, 0xE98B4A, new Landmark("pyramid", 0xE8C27A, "pyramid"), new Landmark("tower", 0xD9C9A8, "cairotower"), new Landmark("dome", 0xCFAE7C, "citadel"), new Landmark("arch", 0xC99A62, "gate")),
            new City("paris", 0xB9D8FF, 0xFF9EC1, new Landmark("lattice", 0xB68A5E, "eiffel"), new Landmark("arch", 0xEFE5D0, "arc"), new Landmark("pyramid", 0x9FD3EF, "louvre"), new Landmark("gothic", 0xD8CDB6, "notredame")),
            new City("london", 0xA9C4E8, 0x6F86B8, new Landmark("clock", 0xD9B56D, "bigben"), new Landmark("bridge", 0x9DB7D6, "towerbridge"), new Landmark("wheel", 0xF2F5FA, "eye"), new Landmark("dome", 0xE6E2D6, "stpauls")),
            new City("newyork", 0xFFB199, 0x6A7FD6, new Landmark("statue", 0x6FC7A9, "liberty"), new Landmark("skyscraper", 0xC8CCD6, "empire"), new Landmark("bridge", 0xB89A7A, "brooklyn"), new Landmark("spire", 0xDFE3EA, "chrysler")),
            new City("tokyo", 0xFFC2D4, 0x7A8EE0, new Landmark("lattice", 0xFF5A4F, "tokyotower"), new Landmark("mountain", 0x7F9CC9, "fuji"), new Landmark("pagoda", 0xE0564A, "pagoda"), new Landmark("torii", 0xF0503C, "torii")),
        };

        /// <summary>Where the 4 landmarks stand on a 100 x 60 skyline (ground at y = 54, drawn at half size).</summary>
        public static readonly float[] Slots = { 16, 39, 62, 85 };
        public const int MarksPerCity = 4;
        public const int AllBuilt = (1 << MarksPerCity) - 1;

        public static int Cost(int city, int mark) => 3 + mark + city;              // stars
        public static int MarkCoins(int city, int mark) => 40 + 20 * mark + 10 * city;
        public static int CityCoins(int city) => 200 + 50 * city;                   // plus 1 Crane, 1 Magnet, 1 Shuffle
        public static int StarsForWin(int level) => level <= 0 ? 0 : LevelGenerator.IsHard(level) ? 2 : 1;
        public static bool IsBuilt(int mask, int mark) => ((mask >> mark) & 1) == 1;
        public static int BuiltCount(int mask) { int n = 0; for (int i = 0; i < MarksPerCity; i++) if (IsBuilt(mask, i)) n++; return n; }
        public static bool Finished(int city) => city >= Cities.Length;

        public static bool CanBuildAny(int city, int mask, int stars)
        {
            if (Finished(city)) return false;
            for (int i = 0; i < MarksPerCity; i++)
                if (!IsBuilt(mask, i) && stars >= Cost(city, i)) return true;
            return false;
        }

        public enum BuildResult { Failed, Built, CityDone }

        /// <summary>Spends stars on a landmark of the current city. CityDone: all 4 built, the tour moved on.</summary>
        public static BuildResult Build(ref int city, ref int mask, ref int stars, int mark, out int coins)
        {
            coins = 0;
            if (Finished(city) || mark < 0 || mark >= MarksPerCity || IsBuilt(mask, mark)) return BuildResult.Failed;
            int cost = Cost(city, mark);
            if (stars < cost) return BuildResult.Failed;
            stars -= cost;
            mask |= 1 << mark;
            coins = MarkCoins(city, mark);
            if (mask != AllBuilt) return BuildResult.Built;
            city++;
            mask = 0;
            return BuildResult.CityDone;
        }
    }
}
