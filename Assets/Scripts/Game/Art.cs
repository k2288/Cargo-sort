using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>Procedural sprites and colors, so the game needs no imported art to run.</summary>
    public static class Art
    {
        public static readonly Color Background = new Color32(0x27, 0x3A, 0x55, 0xFF);
        public static readonly Color Ground = new Color32(0x32, 0x48, 0x68, 0xFF);
        public static readonly Color Slot = new Color32(0x1C, 0x2B, 0x40, 0xFF);
        public static readonly Color TruckBed = new Color32(0x4A, 0x55, 0x66, 0xFF);
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

        /// <summary>A crate: dark outline, colored body, and a tape stripe.</summary>
        public static Transform Crate(Transform parent, int colorIndex, float size)
        {
            var c = CrateColor(colorIndex);
            var root = new GameObject("Crate").transform;
            root.SetParent(parent, false);
            Box(root, "Outline", Vector2.zero, new Vector2(size, size), Darken(c, 0.45f), 10);
            Box(root, "Body", Vector2.zero, new Vector2(size * 0.86f, size * 0.86f), c, 11);
            Box(root, "Tape", Vector2.zero, new Vector2(size * 0.86f, size * 0.16f), Darken(c, 0.2f), 12);
            return root;
        }

        /// <summary>Raises a crate's sorting orders while it flies so it draws above everything.</summary>
        public static void SetLift(Transform crate, bool lifted)
        {
            int baseOrder = lifted ? 40 : 10;
            var renderers = crate.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = baseOrder + i;
        }
    }
}
