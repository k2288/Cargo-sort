using System.Collections.Generic;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// One rounded box or circle of a drawing, in world units. The same list is turned into sprites on
    /// the board and into UI images in the shop, so skin previews look exactly like the game.
    /// </summary>
    public struct Shape
    {
        public bool Round;     // circle (Size.x = diameter) instead of a rounded box
        public Vector2 Pos, Size;
        public float Angle;
        public Color Color;
        public int Order;
        public bool Body;      // truck part that bounces (above the wheels)
    }

    /// <summary>A shape list with short helpers for drawing code.</summary>
    public sealed class ShapeList : List<Shape>
    {
        public void Box(Vector2 pos, Vector2 size, Color color, int order, float angle = 0) =>
            Add(new Shape { Pos = pos, Size = size, Color = color, Order = order, Angle = angle });

        public void Box(bool body, Vector2 pos, Vector2 size, Color color, int order) =>
            Add(new Shape { Body = body, Pos = pos, Size = size, Color = color, Order = order });

        public void Dot(Vector2 pos, float diameter, Color color, int order) => Dot(false, pos, diameter, color, order);

        public void Dot(bool body, Vector2 pos, float diameter, Color color, int order) =>
            Add(new Shape { Body = body, Round = true, Pos = pos, Size = new Vector2(diameter, diameter), Color = color, Order = order });

        /// <summary>A wheel with a hubcap.</summary>
        public void Wheel(Vector2 pos, float diameter)
        {
            Dot(pos, diameter, Art.Wheel, 1);
            Dot(pos, diameter * 0.45f, new Color32(0x9A, 0xA3, 0xAD, 0xFF), 2);
            Dot(pos, diameter * 0.16f, Art.Wheel, 3);
        }
    }

    /// <summary>Procedural sprites and colors, so the game needs no imported art to run.</summary>
    public static class Art
    {
        // Colors of the current theme (bought in the shop).
        public static Color Background => Themes.Current.Background;
        public static Color Ground => Themes.Current.Ground;
        public static Color Slot => Themes.Current.Slot;
        public static Color TruckBed => Themes.Current.TruckBed;
        public static readonly Color Wheel = new Color32(0x15, 0x18, 0x1E, 0xFF);
        public static readonly Color Window = new Color32(0xCF, 0xEA, 0xFF, 0xFF);

        public static readonly Color[] CrateColors =
        {
            new Color32(0xF0, 0x4E, 0x4E, 0xFF), // red
            new Color32(0x3E, 0x8E, 0xF7, 0xFF), // blue
            new Color32(0x4C, 0xC9, 0x5B, 0xFF), // green
            new Color32(0xFF, 0xCC, 0x33, 0xFF), // yellow
            new Color32(0xA8, 0x5C, 0xF0, 0xFF), // purple
            new Color32(0xFF, 0x8A, 0x2B, 0xFF), // orange
            new Color32(0x2F, 0xD6, 0xD6, 0xFF), // cyan
            new Color32(0xFF, 0x6F, 0xB5, 0xFF), // pink
        };

        private static Sprite rounded;
        private static Sprite circle;

        public static Color CrateColor(int index) => CrateColors[Mathf.Abs(index) % CrateColors.Length];

        public static Color Lighten(Color c, float amount) =>
            new Color(c.r + (1f - c.r) * amount, c.g + (1f - c.g) * amount, c.b + (1f - c.b) * amount, c.a);

        public static Color Darken(Color c, float amount) =>
            new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

        /// <summary>White rounded rectangle, 9-sliced so any size keeps round corners. 1 unit = 64 px.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64, radius = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return circle;
            }
        }

        public static SpriteRenderer Box(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Rounded;
            if (Mathf.Min(size.x, size.y) >= 0.5f)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            else
            {
                // Too thin for the 9-slice corners: stretch the whole sprite instead.
                go.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer Dot(Transform parent, string name, Vector2 localPos, float diameter, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * diameter;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Circle;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A crate in the player's crate skin.</summary>
        public static Transform Crate(Transform parent, int colorIndex, float size)
        {
            var root = new GameObject("Crate").transform;
            root.SetParent(parent, false);
            BuildSprites(root, CrateShapes(colorIndex, size, Progress.CrateSkin), null);
            return root;
        }

        /// <summary>Crate drawings. wood: frame, planks, brace, nails. gift: ribbon and bow. metal: ridges and rivets. candy: stripes.</summary>
        public static List<Shape> CrateShapes(int colorIndex, float size, string skin)
        {
            var c = CrateColor(colorIndex);
            var list = new ShapeList();
            list.Box(new Vector2(0.035f * size, -0.08f * size), new Vector2(size, size * 0.96f), new Color(0, 0, 0, 0.22f), 9);

            switch (skin)
            {
                case "gift":
                {
                    var ribbon = new Color32(0xFF, 0xF6, 0xD6, 0xFF);
                    var gold = new Color32(0xFF, 0xC8, 0x3A, 0xFF);
                    list.Box(Vector2.zero, new Vector2(size, size), Darken(c, 0.3f), 10);
                    list.Box(new Vector2(0, 0.02f * size), new Vector2(size * 0.9f, size * 0.88f), c, 11);
                    list.Box(Vector2.zero, new Vector2(size * 0.2f, size), ribbon, 12);
                    list.Box(Vector2.zero, new Vector2(size, size * 0.2f), ribbon, 12);
                    list.Box(Vector2.zero, new Vector2(size * 0.07f, size), gold, 13);
                    list.Box(Vector2.zero, new Vector2(size, size * 0.07f), gold, 13);
                    list.Dot(new Vector2(-0.15f * size, 0.5f * size), size * 0.32f, ribbon, 13);
                    list.Dot(new Vector2(0.15f * size, 0.5f * size), size * 0.32f, ribbon, 13);
                    list.Dot(new Vector2(0, 0.47f * size), size * 0.15f, gold, 14);
                    list.Box(new Vector2(-0.25f * size, 0.28f * size), new Vector2(size * 0.2f, size * 0.07f), new Color(1, 1, 1, 0.5f), 14);
                    break;
                }
                case "metal":
                {
                    list.Box(Vector2.zero, new Vector2(size, size), new Color32(0x56, 0x60, 0x6D, 0xFF), 10);
                    list.Box(Vector2.zero, new Vector2(size * 0.86f, size * 0.86f), c, 11);
                    foreach (float k in new[] { -0.2f, 0f, 0.2f }) list.Box(new Vector2(0, k * size), new Vector2(size * 0.86f, size * 0.05f), Darken(c, 0.25f), 12);
                    list.Box(new Vector2(0, 0.34f * size), new Vector2(size * 0.86f, size * 0.07f), Lighten(c, 0.4f), 13);
                    var rivet = new Color32(0xD5, 0xDC, 0xE5, 0xFF);
                    for (int i = 0; i < 4; i++) list.Dot(new Vector2((i % 2 == 0 ? -1 : 1) * size * 0.43f, (i < 2 ? 1 : -1) * size * 0.43f), size * 0.09f, rivet, 14);
                    break;
                }
                case "candy":
                {
                    float inner = size * 0.86f;
                    list.Box(Vector2.zero, new Vector2(size, size), Darken(c, 0.3f), 10);
                    list.Box(Vector2.zero, new Vector2(inner, inner), c, 11);
                    foreach (float off in new[] { -0.28f, 0f, 0.28f })
                    {
                        float d = off * inner;
                        float length = 1.414f * (inner - 1.414f * Mathf.Abs(d)) * 0.88f;
                        list.Box(new Vector2(d * 0.707f, -d * 0.707f), new Vector2(length, size * 0.12f), new Color(1, 1, 1, 0.38f), 12, 45f);
                    }
                    list.Box(new Vector2(-0.14f * size, 0.27f * size), new Vector2(size * 0.32f, size * 0.1f), new Color(1, 1, 1, 0.65f), 14);
                    break;
                }
                default: // wood
                {
                    var frame = Darken(c, 0.42f);
                    var groove = Darken(c, 0.16f);
                    list.Box(Vector2.zero, new Vector2(size, size), frame, 10);
                    list.Box(Vector2.zero, new Vector2(size * 0.8f, size * 0.8f), c, 11);
                    list.Box(new Vector2(0, size * 0.135f), new Vector2(size * 0.8f, size * 0.045f), groove, 12);
                    list.Box(new Vector2(0, -size * 0.135f), new Vector2(size * 0.8f, size * 0.045f), groove, 12);
                    list.Box(Vector2.zero, new Vector2(size * 0.98f, size * 0.13f), frame, 13, 45f);
                    list.Box(new Vector2(-size * 0.12f, size * 0.33f), new Vector2(size * 0.42f, size * 0.06f), new Color(1, 1, 1, 0.4f), 14);
                    var nail = Lighten(c, 0.35f);
                    for (int i = 0; i < 4; i++) list.Dot(new Vector2((i % 2 == 0 ? -1 : 1) * size * 0.42f, (i < 2 ? 1 : -1) * size * 0.42f), size * 0.08f, nail, 14);
                    break;
                }
            }
            return list;
        }

        // ---------- Trucks ----------

        public const float SlotSpacing = 0.95f;

        /// <summary>Big trucks pack their spots a little tighter so two still fit side by side.</summary>
        public static float SlotSpacingFor(int capacity) => capacity >= 4 ? 0.88f : SlotSpacing;

        public static Vector3 TruckSlotLocal(int slot, int capacity) =>
            new Vector3((slot - (capacity - 1) / 2f) * SlotSpacingFor(capacity) - 0.45f, 0.12f, 0f);

        /// <summary>Flatbed truck: side rails, cab with window, headlight and roof light, hubcap wheels, plus the skin's extras.</summary>
        /// <param name="color2">Second color of a two-color truck (-1 = one color): its half of the rail and its spots are tinted.</param>
        public static List<Shape> TruckShapes(int color, int capacity, string skin, int color2 = -1)
        {
            var list = new ShapeList();

            var c = CrateColor(color);
            float spacing = SlotSpacingFor(capacity);
            float bedWidth = capacity * spacing + 0.2f;
            float bedLeft = TruckSlotLocal(0, capacity).x - spacing / 2f - 0.1f;
            float bedCenter = bedLeft + bedWidth / 2f;
            float cabX = bedLeft + bedWidth + 0.5f;
            float length = cabX + 0.45f - bedLeft + 0.3f;

            list.Box(false, new Vector2((bedLeft + cabX + 0.45f) / 2f + 0.1f, -0.86f), new Vector2(length, 0.2f), new Color(0, 0, 0, 0.25f), 0);
            list.Wheel(new Vector2(bedLeft + 0.55f, -0.62f), 0.52f);
            list.Wheel(new Vector2(bedLeft + bedWidth - 0.4f, -0.62f), 0.52f);
            list.Wheel(new Vector2(cabX + 0.05f, -0.62f), 0.52f);

            list.Box(true, new Vector2(bedCenter, -0.52f), new Vector2(bedWidth + 0.1f, 0.16f), new Color32(0x2A, 0x2F, 0x37, 0xFF), 4);
            list.Box(true, new Vector2(bedCenter, 0.05f), new Vector2(bedWidth, 1.15f), TruckBed, 4);
            list.Box(true, new Vector2(bedCenter, 0.58f), new Vector2(bedWidth, 0.08f), Lighten(TruckBed, 0.25f), 5);
            if (color2 >= 0)
            {
                list.Box(true, new Vector2(bedLeft + bedWidth * 0.25f, -0.44f), new Vector2(bedWidth / 2f, 0.22f), c, 5);
                list.Box(true, new Vector2(bedLeft + bedWidth * 0.75f, -0.44f), new Vector2(bedWidth / 2f, 0.22f), CrateColor(color2), 5);
            }
            else list.Box(true, new Vector2(bedCenter, -0.44f), new Vector2(bedWidth, 0.22f), c, 5);
            list.Box(true, new Vector2(bedCenter, -0.37f), new Vector2(bedWidth, 0.05f), Lighten(c, 0.35f), 6);
            for (int i = 0; i < capacity; i++)
            {
                list.Box(true, TruckSlotLocal(i, capacity), new Vector2(0.86f, 0.86f), Darken(TruckBed, 0.25f), 5);
                if (color2 < 0) continue;
                // Two-color truck: tint each spot with the color it takes (first half: color, second half: color2).
                var tintColor = CrateColor(i < capacity / 2 ? color : color2);
                list.Box(true, TruckSlotLocal(i, capacity), new Vector2(0.62f, 0.62f), new Color(tintColor.r, tintColor.g, tintColor.b, 0.45f), 6);
            }

            list.Box(true, new Vector2(cabX, -0.05f), new Vector2(0.9f, 1.0f), c, 4);
            list.Box(true, new Vector2(cabX, 0.4f), new Vector2(0.9f, 0.1f), Lighten(c, 0.3f), 5);
            list.Box(true, new Vector2(cabX - 0.05f, -0.32f), new Vector2(0.8f, 0.07f), Darken(c, 0.25f), 5);
            list.Box(true, new Vector2(cabX + 0.12f, 0.15f), new Vector2(0.5f, 0.48f), Window, 5);
            list.Box(true, new Vector2(cabX + 0.02f, 0.26f), new Vector2(0.14f, 0.2f), new Color(1, 1, 1, 0.75f), 6);
            list.Box(true, new Vector2(cabX - 0.02f, 0.53f), new Vector2(0.3f, 0.1f), new Color32(0xFF, 0xB7, 0x2B, 0xFF), 5);
            list.Box(true, new Vector2(cabX + 0.48f, -0.22f), new Vector2(0.12f, 0.16f), new Color32(0xFF, 0xF3, 0xB0, 0xFF), 5);
            list.Box(true, new Vector2(cabX + 0.45f, -0.48f), new Vector2(0.16f, 0.12f), new Color32(0x7B, 0x87, 0x94, 0xFF), 5);

            switch (skin)
            {
                case "bigrig":
                    list.Box(true, new Vector2(cabX - 0.52f, 0.62f), new Vector2(0.13f, 1.0f), new Color32(0xC9, 0xD1, 0xDB, 0xFF), 3);
                    list.Box(true, new Vector2(cabX - 0.52f, 1.12f), new Vector2(0.2f, 0.09f), new Color32(0x7B, 0x87, 0x94, 0xFF), 3);
                    list.Box(true, new Vector2(cabX, 0.6f), new Vector2(0.95f, 0.2f), Darken(c, 0.2f), 5);
                    list.Box(true, new Vector2(cabX + 0.5f, -0.4f), new Vector2(0.14f, 0.36f), new Color32(0xE6, 0xEC, 0xF2, 0xFF), 6);
                    break;
                case "icecream":
                    var cone = new Color32(0xE3, 0xA8, 0x57, 0xFF);
                    list.Box(true, new Vector2(cabX, 0.92f), new Vector2(0.42f, 0.1f), cone, 6);
                    list.Box(true, new Vector2(cabX, 0.8f), new Vector2(0.3f, 0.14f), cone, 6);
                    list.Box(true, new Vector2(cabX, 0.66f), new Vector2(0.17f, 0.14f), cone, 6);
                    list.Dot(true, new Vector2(cabX, 1.07f), 0.5f, new Color32(0xFF, 0x9E, 0xC7, 0xFF), 7);
                    list.Dot(true, new Vector2(cabX + 0.1f, 1.14f), 0.12f, Color.white, 8);
                    list.Dot(true, new Vector2(cabX - 0.25f, -0.2f), 0.09f, Color.white, 6);
                    list.Dot(true, new Vector2(cabX + 0.2f, -0.28f), 0.09f, new Color32(0x7B, 0xE3, 0xFF, 0xFF), 6);
                    list.Dot(true, new Vector2(cabX - 0.05f, -0.1f), 0.09f, new Color32(0xFF, 0xE0, 0x66, 0xFF), 6);
                    list.Box(true, new Vector2(bedCenter, -0.44f), new Vector2(bedWidth, 0.08f), new Color32(0xFF, 0x9E, 0xC7, 0xFF), 7);
                    break;
                case "racing":
                    list.Box(true, new Vector2(cabX, -0.14f), new Vector2(0.9f, 0.12f), Color.white, 6);
                    list.Box(true, new Vector2(cabX, -0.26f), new Vector2(0.9f, 0.06f), new Color32(0x1B, 0x1B, 0x1B, 0xFF), 6);
                    list.Box(true, new Vector2(cabX - 0.28f, 0.7f), new Vector2(0.55f, 0.09f), new Color32(0x1B, 0x1B, 0x1B, 0xFF), 5);
                    list.Box(true, new Vector2(cabX - 0.42f, 0.6f), new Vector2(0.07f, 0.2f), new Color32(0x1B, 0x1B, 0x1B, 0xFF), 5);
                    list.Box(true, new Vector2(bedCenter, -0.44f), new Vector2(bedWidth, 0.07f), Color.white, 7);
                    break;
            }
            return list;
        }

        /// <summary>Turns shapes into sprites. Body shapes go under body when it is given.</summary>
        public static void BuildSprites(Transform parent, List<Shape> shapes, Transform body)
        {
            foreach (var s in shapes)
            {
                var target = s.Body && body != null ? body : parent;
                if (s.Round)
                {
                    Dot(target, "Dot", s.Pos, s.Size.x, s.Color, s.Order);
                    continue;
                }
                var sr = Box(target, "Part", s.Pos, s.Size, s.Color, s.Order);
                if (s.Angle != 0f) sr.transform.localRotation = Quaternion.Euler(0, 0, s.Angle);
            }
        }

        // ---------- UI art ----------

        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> gradients = new Dictionary<int, Sprite>();
        private static Font uiFont, logoFont;

        /// <summary>A white icon from Resources/Icons (tint it with Image.color).</summary>
        public static Sprite Icon(string name)
        {
            if (icons.TryGetValue(name, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>("Icons/" + name);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("Icons/" + name);
                if (tex != null) sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            icons[name] = sprite;
            return sprite;
        }

        /// <summary>A vertical gradient (top color to bottom color), for screen backgrounds.</summary>
        public static Sprite Gradient(Color top, Color bottom)
        {
            int key = ((Color32)top).GetHashCode() * 31 + ((Color32)bottom).GetHashCode();
            if (gradients.TryGetValue(key, out var cached) && cached != null) return cached;
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            gradients[key] = sprite;
            return sprite;
        }

        /// <summary>Vazirmatn: has Persian and Latin letters.</summary>
        public static Font UIFont
        {
            get
            {
                if (uiFont == null) uiFont = Resources.Load<Font>("Fonts/Vazirmatn-Bold");
                if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return uiFont;
            }
        }

        /// <summary>Lilita One: the chunky logo and button font (Latin only).</summary>
        public static Font LogoFont
        {
            get
            {
                if (logoFont == null) logoFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                return logoFont != null ? logoFont : UIFont;
            }
        }

        /// <summary>Buttons and titles: Lilita One in English, Vazirmatn in Persian.</summary>
        public static Font DisplayFont => Loc.Persian ? UIFont : LogoFont;

        /// <summary>A wheel with a hubcap.</summary>
        public static void DrawWheel(Transform parent, Vector2 pos, float diameter, int order)
        {
            Dot(parent, "Tire", pos, diameter, Wheel, order);
            Dot(parent, "Hub", pos, diameter * 0.45f, new Color32(0x9A, 0xA3, 0xAD, 0xFF), order + 1);
            Dot(parent, "Bolt", pos, diameter * 0.16f, Wheel, order + 2);
        }

        private static Font labelFont;

        /// <summary>World-space text (used for "?" crates, ice counters and the "Next" label).</summary>
        public static TextMesh Label(Transform parent, string text, Vector2 localPos, float height, Color color, int order)
        {
            if (labelFont == null)
            {
                labelFont = Resources.Load<Font>("Fonts/Vazirmatn-Bold");
                if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = labelFont;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = labelFont.material;
            mr.sortingOrder = order;
            return tm;
        }

        /// <summary>A crate whose color is still a secret.</summary>
        public static Transform HiddenCrate(Transform parent, float size)
        {
            var root = new GameObject("HiddenCrate").transform;
            root.SetParent(parent, false);
            Box(root, "Outline", Vector2.zero, new Vector2(size, size), new Color32(0x3A, 0x40, 0x4A, 0xFF), 10);
            Box(root, "Body", Vector2.zero, new Vector2(size * 0.86f, size * 0.86f), new Color32(0x6B, 0x73, 0x80, 0xFF), 11);
            Label(root, "?", new Vector2(0, 0.02f), size * 0.7f, Color.white, 12);
            return root;
        }

        /// <summary>Raises a crate's sorting orders while it flies so it draws above everything.</summary>
        public static void SetLift(Transform crate, bool lifted)
        {
            var renderers = crate.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder += lifted ? 30 : -30;
        }
    }
}
