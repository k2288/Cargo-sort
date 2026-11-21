using System;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The home screen: logo, the World Tour city banner, a winding path of levels, a big Play button, and
    /// side buttons for the World Tour, the shop, the starter pack, the daily challenge and the free gift.
    /// </summary>
    public sealed partial class Hud
    {
        private GameObject homePanel, starterSide, challengeDot, chestDot, streakTag;
        private RectTransform pathRoot;
        private Candy playButton;
        private Text playSub, streakText, starterTime;
        private Image homeBg;

        private void BuildHome(RectTransform safe)
        {
            var home = MakeRect("Home", safe);
            Stretch(home);
            homePanel = home.gameObject;
            homeBg = home.gameObject.AddComponent<Image>();

            // Logo
            var logo = MakeRect("Logo", home);
            Anchor(logo, new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(900, 380));
            logo.localRotation = Quaternion.Euler(0, 0, 3f);
            var cargo = MakeText(logo, "CARGO", 200, TextAnchor.MiddleCenter, Art.LogoFont);
            cargo.color = Gold;
            AddOutline(cargo, new Color32(0xB5, 0x64, 0x0A, 0xFF), 7);
            AddShadow(cargo, new Color32(0x6A, 0x36, 0x00, 0xFF), 16);
            Anchor(cargo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 200));
            var sort = MakeText(logo, "SORT", 170, TextAnchor.MiddleCenter, Art.LogoFont);
            AddOutline(sort, new Color32(0x2F, 0x86, 0xE8, 0xFF), 7);
            AddShadow(sort, new Color32(0x1B, 0x56, 0xA3, 0xFF), 16);
            Anchor(sort.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(900, 180));

            // World Tour city banner under the logo (also adds the Tour side button)
            BuildCityBar(home);

            pathRoot = MakeRect("Path", home);
            Anchor(pathRoot, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(600, 640));

            // Side buttons
            SideButton(home, Purple, "cart", Loc.T("shop"), new Vector2(0, 0.5f), new Vector2(130, -190), () => OpenShop?.Invoke(), out _);
            starterSide = SideButton(home, Red, "star", "", new Vector2(0, 0.5f), new Vector2(130, -420), () => OpenStarter?.Invoke(), out starterTime).gameObject;
            var starterLabelBg = Img((RectTransform)starterTime.transform.parent, Art.Rounded, new Color(0, 0, 0, 0.5f), 22);
            Anchor(starterLabelBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(190, 50));
            starterLabelBg.transform.SetSiblingIndex(starterTime.transform.GetSiblingIndex());
            starterTime.fontSize = 32;
            var challenge = SideButton(home, Blue, "trophy", Loc.T("challenge"), new Vector2(1, 0.5f), new Vector2(-130, 40), () => Challenge?.Invoke(), out _);
            challengeDot = Dot(challenge);
            var chest = SideButton(home, Yellow, "gift", Loc.T("chest"), new Vector2(1, 0.5f), new Vector2(-130, -190), () => OpenChest?.Invoke(), out _);
            chestDot = Dot(chest);

            // Win streak and Play
            playButton = MakeCandy(home, Green, () => Play?.Invoke(), "Level", 92, null, 0, 64);
            Anchor(playButton.Root, new Vector2(0.5f, 0), new Vector2(0, 210), new Vector2(820, 220));
            playSub = MakeText(playButton.Top, Loc.T("play_hard"), 34, TextAnchor.MiddleCenter);
            Anchor(playSub.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(600, 50));

            var tag = MakeRect("Streak", home);
            Anchor(tag, new Vector2(0.5f, 0), new Vector2(0, 360), new Vector2(170, 80));
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

        private RectTransform SideButton(RectTransform home, Color color, string icon, string label, Vector2 anchor, Vector2 pos, Action onClick, out Text labelText)
        {
            var b = MakeCandy(home, color, onClick, null, 0, icon, 92, 50);
            Anchor(b.Root, anchor, pos, new Vector2(170, 170));
            labelText = MakeText(b.Root, label, 36, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(260, 50));
            AddShadow(labelText, new Color(0, 0, 0, 0.45f), 4);
            return b.Root;
        }

        private GameObject Dot(RectTransform button)
        {
            var dot = Pill(button, Red, "", new Vector2(40, 40), 20);
            Anchor(dot, new Vector2(1, 1), new Vector2(-12, -12), new Vector2(50, 50));
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
            var bg = Art.Background;
            homeBg.sprite = Art.Gradient(Color.Lerp(bg, new Color32(0x9F, 0xD4, 0xFF, 0xFF), 0.38f), Art.Darken(bg, 0.3f));
            homeBg.color = Color.white;
            playButton.Label.text = Loc.T("level", level);
            playButton.Label.rectTransform.offsetMin = new Vector2(0, hard ? 40 : 0);
            playSub.gameObject.SetActive(hard);
            playButton.BaseColor = hard ? Red : Green;
            playButton.SetColor(playButton.BaseColor);
            challengeDot.SetActive(challengeReady);
            chestDot.SetActive(chestReady);
            streakTag.SetActive(streak > 0);
            streakText.text = Economy.Number(streak);
            starterSide.SetActive(Economy.StarterActive);
            SetCityBar();
            DrawPath(level);
        }

        /// <summary>Two finished levels at the bottom, the current one, then locked ones going up.</summary>
        private void DrawPath(int current)
        {
            for (int i = pathRoot.childCount - 1; i >= 0; i--) Destroy(pathRoot.GetChild(i).gameObject);
            int first = Mathf.Max(1, current - 2);
            const int count = 6;
            float height = pathRoot.sizeDelta.y;
            var pos = new Vector2[count];
            float[] xs = { 0, 150, 0, -150 };
            for (int i = 0; i < count; i++)
                pos[i] = new Vector2(xs[i % 4], -height / 2 + 70 + i * (height - 140) / (count - 1));

            // Dotted road between the stops (a gentle S-curve).
            for (int i = 1; i < count; i++)
            {
                Vector2 a = pos[i - 1], b = pos[i];
                for (int k = 1; k < 8; k++)
                {
                    float t = k / 8f, u = 1 - t;
                    float my = (a.y + b.y) / 2f;
                    var p = u * u * u * a + 3 * u * u * t * new Vector2(a.x, my) + 3 * u * t * t * new Vector2(b.x, my) + t * t * t * b;
                    var under = Img(pathRoot, Art.Circle, new Color(0, 0, 0, 0.28f), 0);
                    Anchor(under.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(34, 34));
                    var dash = Img(pathRoot, Art.Circle, new Color(1, 1, 1, 0.55f), 0);
                    Anchor(dash.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(13, 13));
                }
            }

            for (int i = 0; i < count; i++)
            {
                int n = first + i;
                bool done = n < current, now = n == current, hard = LevelGeneratorIsHard(n);
                float r = now ? 160 : 112;
                Color locked = hard ? (Color)new Color32(0x7A, 0x3A, 0x44, 0xFF) : (Color)new Color32(0x3D, 0x52, 0x72, 0xFF);
                Color fill = done ? Green : now ? (hard ? Red : Yellow) : locked;
                if (now)
                {
                    var glow = Img(pathRoot, Art.Circle, new Color(1, 1, 1, 0.18f), 0);
                    Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(r + 50, r + 50));
                }
                var edge = Img(pathRoot, Art.Circle, Art.Darken(fill, 0.42f), 0);
                Anchor(edge.rectTransform, new Vector2(0.5f, 0.5f), pos[i] + new Vector2(0, -10), new Vector2(r, r));
                var face = Img(pathRoot, Art.Circle, fill, 0);
                Anchor(face.rectTransform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(r, r));
                var shine = Img(face.rectTransform, Art.Circle, new Color(1, 1, 1, 0.3f), 0);
                Anchor(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, r * 0.22f), new Vector2(r * 0.62f, r * 0.3f));
                if (done)
                {
                    var check = Icon(face.rectTransform, "check", r * 0.5f);
                    Anchor(check.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 0.5f, r * 0.5f));
                }
                else if (n > current + 1)
                {
                    var lockIcon = Icon(face.rectTransform, "lock", r * 0.42f);
                    lockIcon.color = new Color(1, 1, 1, 0.55f);
                    Anchor(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 0.42f, r * 0.42f));
                }
                else
                {
                    var t = MakeText(face.rectTransform, Economy.Number(n), now ? 72 : 52, TextAnchor.MiddleCenter, Art.DisplayFont);
                    AddOutline(t, Art.Darken(fill, 0.5f), 3);
                    Stretch(t.rectTransform);
                }
            }
        }

        private static bool LevelGeneratorIsHard(int level) => CargoSort.Core.LevelGenerator.IsHard(level);

        private void UpdateHome()
        {
            if (homePanel == null || !homePanel.activeSelf) return;
            playButton.Root.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 3.9f));
            if (starterSide.activeSelf)
            {
                var left = Economy.StarterLeft;
                starterTime.text = Economy.Clock(left);
                if (left <= TimeSpan.Zero) starterSide.SetActive(false);
            }
        }
    }
}
