using System;
using System.Collections.Generic;
using CargoSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The home screen: the current World Tour city as the backdrop (sky, sun, its four landmarks, ground), the
    /// city banner, a winding road of levels, a big Play button, and a tab bar along the bottom (Tour, Shop,
    /// starter pack, Rush Week, daily challenge, free gift).
    /// </summary>
    public sealed partial class Hud
    {
        private GameObject homePanel, starterSide, challengeDot, chestDot, streakTag;
        private RectTransform pathRoot, pathMarker, pathHalo;
        private Candy playButton;
        private Text playSub, streakText, starterTime;
        private Image homeBg, homeGround, homeSkyline, homeStrip;
        private readonly System.Collections.Generic.List<KeyValuePair<int, RectTransform>> tabs = new System.Collections.Generic.List<KeyValuePair<int, RectTransform>>();

        private static Color Rgb(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        private void BuildHome(RectTransform safe)
        {
            tabs.Clear();
            var home = MakeRect("Home", safe);
            Stretch(home);
            homePanel = home.gameObject;
            homeBg = home.gameObject.AddComponent<Image>();      // the sky

            // Sun and soft clouds
            var halo = Img(home, Art.Circle, new Color(1, 1, 1, 0.2f), 0);
            Anchor(halo.rectTransform, new Vector2(0.8f, 0.66f), Vector2.zero, new Vector2(340, 340));
            var sun = Img(home, Art.Circle, new Color(1, 1, 1, 0.62f), 0);
            Anchor(sun.rectTransform, new Vector2(0.8f, 0.66f), Vector2.zero, new Vector2(200, 200));
            Cloud(home, new Vector2(0.18f, 0.74f), 1.4f);
            Cloud(home, new Vector2(0.86f, 0.55f), 0.9f);
            Cloud(home, new Vector2(0.1f, 0.5f), 0.8f);

            // The city: landmarks on the horizon, then the ground in front of them
            homeSkyline = Img(home, null, Color.white, 0);
            homeSkyline.preserveAspect = true;
            homeSkyline.rectTransform.anchorMin = new Vector2(0, 0.32f);
            homeSkyline.rectTransform.anchorMax = new Vector2(1, 0.32f);
            homeSkyline.rectTransform.pivot = new Vector2(0.5f, 0);
            homeSkyline.rectTransform.offsetMin = Vector2.zero;
            homeSkyline.rectTransform.sizeDelta = new Vector2(0, 948);
            homeGround = Img(home, Art.Gradient(Color.gray, Color.black), Color.white, 0);
            homeGround.rectTransform.anchorMin = Vector2.zero;
            homeGround.rectTransform.anchorMax = new Vector2(1, 0.32f);
            homeGround.rectTransform.offsetMin = homeGround.rectTransform.offsetMax = Vector2.zero;
            homeStrip = Img(home, Art.Rounded, new Color(1, 1, 1, 0.35f), 0);
            homeStrip.rectTransform.anchorMin = new Vector2(0, 0.32f);
            homeStrip.rectTransform.anchorMax = new Vector2(1, 0.32f);
            homeStrip.rectTransform.pivot = new Vector2(0.5f, 1);
            homeStrip.rectTransform.sizeDelta = new Vector2(0, 14);
            homeStrip.rectTransform.anchoredPosition = Vector2.zero;

            pathRoot = MakeRect("Path", home);
            Anchor(pathRoot, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 1000));

            // Bottom tab bar
            var barEdge = Img(home, Art.Rounded, new Color32(0xB7, 0x93, 0xFF, 0xFF), 64);
            barEdge.rectTransform.anchorMin = Vector2.zero;
            barEdge.rectTransform.anchorMax = new Vector2(1, 0);
            barEdge.rectTransform.pivot = new Vector2(0.5f, 0);
            barEdge.rectTransform.offsetMin = new Vector2(0, -80);
            barEdge.rectTransform.sizeDelta = new Vector2(0, 340);
            var bar = Img(barEdge.rectTransform, Art.Rounded, new Color32(0x4A, 0x23, 0x96, 0xFF), 64);
            Stretch(bar.rectTransform);
            bar.rectTransform.offsetMax = new Vector2(0, -10);
            var barShine = Img(bar.rectTransform, Art.Rounded, new Color(1, 1, 1, 0.14f), 64);
            barShine.rectTransform.anchorMin = new Vector2(0, 0.7f);
            barShine.rectTransform.anchorMax = Vector2.one;
            barShine.rectTransform.offsetMin = new Vector2(12, 0);
            barShine.rectTransform.offsetMax = new Vector2(-12, -8);

            // World Tour city banner under the top bar (also adds the Tour tab)
            BuildCityBar(home);

            // Tabs
            SideButton(home, Purple, "cart", Loc.T("shop"), 1, () => OpenShop?.Invoke(), out _);
            starterSide = SideButton(home, Red, "star", "", 2, () => OpenStarter?.Invoke(), out starterTime).gameObject;
            var starterLabelBg = Img((RectTransform)starterTime.transform.parent, Art.Rounded, new Color(0, 0, 0, 0.5f), 22);
            Anchor(starterLabelBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(170, 46));
            starterLabelBg.transform.SetSiblingIndex(starterTime.transform.GetSiblingIndex());
            starterTime.fontSize = 28;
            var challenge = SideButton(home, Blue, "trophy", Loc.T("challenge"), 4, () => Challenge?.Invoke(), out _);
            challengeDot = Dot(challenge);
            var chest = SideButton(home, Yellow, "gift", Loc.T("chest"), 5, () => OpenChest?.Invoke(), out _);
            chestDot = Dot(chest);
            BuildRushSide(home);

            // Win streak and Play
            playButton = MakeCandy(home, Yellow, () => Play?.Invoke(), "Level", 84, null, 0, 90);
            Anchor(playButton.Root, new Vector2(0.5f, 0), new Vector2(0, 430), new Vector2(680, 190));
            playSub = MakeText(playButton.Top, Loc.T("play_hard"), 32, TextAnchor.MiddleCenter);
            Anchor(playSub.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(600, 46));

            var tag = MakeRect("Streak", home);
            Anchor(tag, new Vector2(0.5f, 0), new Vector2(330, 590), new Vector2(170, 80));
            streakTag = tag.gameObject;
            Stretch(Img(tag, Art.Rounded, new Color32(0x9C, 0x23, 0x18, 0xFF), 40).rectTransform);
            var tagFace = Img(tag, Art.Rounded, new Color32(0xF0, 0x6A, 0x3A, 0xFF), 40);
            Stretch(tagFace.rectTransform);
            tagFace.rectTransform.offsetMin = new Vector2(0, 8);
            var flame = Icon(tagFace.rectTransform, "flame", 50);
            Anchor(flame.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(50, 50));
            streakText = MakeText(tagFace.rectTransform, "", 48, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(streakText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(22, 2), new Vector2(110, 70));
            homePanel.SetActive(false);
        }

        /// <summary>A soft white cloud made of three circles (fraction of the home screen, scale).</summary>
        private static void Cloud(RectTransform home, Vector2 at, float scale)
        {
            var root = MakeRect("Cloud", home);
            Anchor(root, at, Vector2.zero, new Vector2(10, 10));
            float[][] parts = { new[] { -50f, 0, 90 }, new[] { 0f, 22, 120 }, new[] { 52f, 0, 100 } };
            foreach (var p in parts)
            {
                var c = Img(root, Art.Circle, new Color(1, 1, 1, 0.8f), 0);
                Anchor(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(p[0], p[1]) * scale, new Vector2(p[2], p[2] * 0.8f) * scale);
            }
        }

        /// <summary>A button in the bottom tab bar. order = its place from the left (the bar spreads the visible ones evenly).</summary>
        private RectTransform SideButton(RectTransform home, Color color, string icon, string label, int order, Action onClick, out Text labelText)
        {
            var b = MakeCandy(home, color, onClick, null, 0, icon, 74, 40);
            Anchor(b.Root, new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(136, 136));
            labelText = MakeText(b.Root, label, 30, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(230, 46));
            AddOutline(labelText, Ink, 3);
            tabs.Add(new KeyValuePair<int, RectTransform>(order, b.Root));
            return b.Root;
        }

        /// <summary>Spreads the visible tabs evenly along the bar, in order.</summary>
        private void LayoutTabs()
        {
            var active = new System.Collections.Generic.List<KeyValuePair<int, RectTransform>>();
            foreach (var t in tabs) if (t.Value != null && t.Value.gameObject.activeSelf) active.Add(t);
            active.Sort((x, y) => x.Key.CompareTo(y.Key));
            float width = 1000f, step = width / Mathf.Max(1, active.Count);
            for (int i = 0; i < active.Count; i++)
                active[i].Value.anchoredPosition = new Vector2(-width / 2f + step * (i + 0.5f), 175);
        }

        private GameObject Dot(RectTransform button)
        {
            var dot = Pill(button, Red, "", new Vector2(36, 36), 20);
            Anchor(dot, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(46, 46));
            return dot.gameObject;
        }

        /// <summary>Shows the home screen (true) or the game (false).</summary>
        public void ShowHome(bool show)
        {
            homePanel.SetActive(show);
            SetGameChromeVisible(!show);
            if (show) SetTip(null);
        }

        /// <summary>Fills in the home screen for the next level and the current offers.</summary>
        public void SetHome(int level, bool hard, int streak, bool challengeReady, bool chestReady)
        {
            // Backdrop: the city of the World Tour (the last one when the tour is finished).
            int tour = Progress.TourCity;
            bool finished = WorldTour.Finished(tour);
            int shown = finished ? WorldTour.Cities.Length - 1 : tour, mask = finished ? WorldTour.AllBuilt : Progress.TourBuilt;
            var city = WorldTour.Cities[shown];
            Color high = Rgb(city.SkyHigh), low = Rgb(city.SkyLow);
            homeBg.sprite = Art.Gradient(high, low);
            homeBg.color = Color.white;
            homeGround.sprite = Art.Gradient(Art.Darken(low, 0.5f), Art.Darken(low, 0.72f));
            homeSkyline.sprite = TourArt.Backdrop(shown, mask);

            playButton.Label.text = Loc.T("level", level);
            playButton.Label.rectTransform.offsetMin = new Vector2(0, hard ? 36 : 0);
            playSub.gameObject.SetActive(hard);
            playButton.BaseColor = hard ? Red : Yellow;
            playButton.SetColor(playButton.BaseColor);
            challengeDot.SetActive(challengeReady);
            chestDot.SetActive(chestReady);
            streakTag.SetActive(streak > 0);
            streakText.text = Economy.Number(streak);
            starterSide.SetActive(Economy.StarterActive);
            SetCityBar();
            SetRushSide();
            LayoutTabs();
            DrawPath(level);
        }

        /// <summary>One finished level behind, the current one, then the next ones going up, on a winding sand road.</summary>
        private void DrawPath(int current)
        {
            for (int i = pathRoot.childCount - 1; i >= 0; i--) Destroy(pathRoot.GetChild(i).gameObject);
            int first = Mathf.Max(1, current - 1);
            const int count = 6;
            float height = pathRoot.sizeDelta.y;
            var pos = new Vector2[count];
            float[] xs = { -210, 50, 240, 40, -230, -20 };
            for (int i = 0; i < count; i++)
                pos[i] = new Vector2(xs[i], -height / 2 + 90 + i * (height - 200) / (count - 1));

            // The road: sample the curve once, then draw edge, sand and dashes in three passes so the edge never cuts into the sand.
            var samples = new System.Collections.Generic.List<Vector2>();
            for (int i = 1; i < count; i++)
            {
                Vector2 a = pos[i - 1], b = pos[i];
                float my = (a.y + b.y) / 2f;
                for (int k = 0; k <= 22; k++)
                {
                    float t = k / 22f, u = 1 - t;
                    samples.Add(u * u * u * a + 3 * u * u * t * new Vector2(a.x, my) + 3 * u * t * t * new Vector2(b.x, my) + t * t * t * b);
                }
            }
            foreach (var p in samples) PathDot(p, 96, new Color32(0x9B, 0x5B, 0x2E, 0xFF));
            foreach (var p in samples) PathDot(p, 78, new Color32(0xFF, 0xE3, 0xA6, 0xFF));
            for (int i = 0; i < samples.Count; i += 4) PathDot(samples[i], 12, new Color(1, 1, 1, 0.75f));

            pathMarker = null;
            pathHalo = null;
            for (int i = 0; i < count; i++)
            {
                int n = first + i;
                bool done = n < current, now = n == current, hard = LevelGeneratorIsHard(n);
                float r = now ? 170 : 124;
                Color fill = done ? Green : now ? (hard ? Red : Yellow) : hard ? (Color)new Color32(0xB2, 0x4A, 0x6A, 0xFF) : Purple;
                if (now)
                {
                    var glow = Img(pathRoot, Art.Circle, new Color(1, 1, 1, 0.55f), 0);
                    Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(r + 56, r + 56));
                    pathHalo = glow.rectTransform;
                }
                var edge = Img(pathRoot, Art.Circle, Art.Darken(fill, 0.45f), 0);
                Anchor(edge.rectTransform, new Vector2(0.5f, 0.5f), pos[i] + new Vector2(0, -12), new Vector2(r + 14, r + 14));
                var ring = Img(pathRoot, Art.Circle, Color.white, 0);
                Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(r + 14, r + 14));
                var face = Img(pathRoot, Art.Circle, fill, 0);
                Anchor(face.rectTransform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(r, r));
                var shine = Img(face.rectTransform, Art.Circle, new Color(1, 1, 1, 0.38f), 0);
                Anchor(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, r * 0.26f), new Vector2(r * 0.62f, r * 0.3f));
                var t = MakeText(face.rectTransform, Economy.Number(n), now ? 84 : 60, TextAnchor.MiddleCenter, Art.DisplayFont);
                AddOutline(t, Art.Darken(fill, 0.5f), 4);
                Stretch(t.rectTransform);
                if (done)
                    for (int s = 0; s < 3; s++)
                    {
                        var star = Icon(pathRoot, "star", 40);
                        star.color = Gold;
                        Anchor(star.rectTransform, new Vector2(0.5f, 0.5f), pos[i] + new Vector2((s - 1) * 38, -r / 2 - 18 + (s == 1 ? -6 : 0)), new Vector2(40, 40));
                    }
                if (now)
                {
                    var arrow = Icon(pathRoot, "play", 80);
                    arrow.color = Color.white;
                    arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
                    Anchor(arrow.rectTransform, new Vector2(0.5f, 0.5f), pos[i] + new Vector2(0, r / 2 + 70), new Vector2(80, 80));
                    arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
                    var shadow = arrow.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color32(0xB5, 0x56, 0x0A, 0xFF);
                    shadow.effectDistance = new Vector2(0, -6);
                    pathMarker = arrow.rectTransform;
                }
            }
        }

        private void PathDot(Vector2 p, float size, Color color)
        {
            var dot = Img(pathRoot, Art.Circle, color, 0);
            Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(size, size));
        }

        private float markerWave;

        private static bool LevelGeneratorIsHard(int level) => CargoSort.Core.LevelGenerator.IsHard(level);

        private void UpdateHome()
        {
            if (homePanel == null || !homePanel.activeSelf) return;
            playButton.Root.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 3.9f));
            float wave = Mathf.Sin(Time.unscaledTime * 3.6f);
            if (pathHalo != null) pathHalo.localScale = Vector3.one * (1f + 0.08f * wave);
            if (pathMarker != null) pathMarker.anchoredPosition += new Vector2(0, (wave - markerWave) * 12f);
            markerWave = wave;
            if (starterSide.activeSelf)
            {
                var left = Economy.StarterLeft;
                starterTime.text = Economy.Clock(left);
                if (left <= TimeSpan.Zero) { starterSide.SetActive(false); LayoutTabs(); }
            }
        }
    }
}
