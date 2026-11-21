using CargoSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The title screen shown when the game opens: the current World Tour city (sky, sun, landmarks, ground) with the
    /// logo, a loading bar and "Tap to start". Tapping it (once the bar is full) fades it out into the level map.
    /// </summary>
    public sealed partial class Hud
    {
        private const float SplashLoadSeconds = 1.7f, SplashFadeSeconds = 0.45f;

        private GameObject splashPanel;
        private CanvasGroup splashGroup;
        private Image splashSky, splashGround, splashLandmarks;
        private RectTransform splashFill;
        private Text splashTap, splashCity;
        private float splashShownAt = -1f, splashFadeStart = -1f;

        public bool SplashVisible => splashPanel != null && splashPanel.activeSelf;

        private void BuildSplash(RectTransform safe)
        {
            var root = MakeRect("Splash", safe);
            Stretch(root);
            splashPanel = root.gameObject;
            splashGroup = splashPanel.AddComponent<CanvasGroup>();
            splashSky = splashPanel.AddComponent<Image>();

            var halo = Img(root, Art.Circle, new Color(1, 1, 1, 0.18f), 0);
            Anchor(halo.rectTransform, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(720, 720));
            var sun = Img(root, Art.Circle, new Color(1, 1, 1, 0.5f), 0);
            Anchor(sun.rectTransform, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(440, 440));
            Cloud(root, new Vector2(0.17f, 0.5f), 1.4f);
            Cloud(root, new Vector2(0.86f, 0.4f), 1f);
            Cloud(root, new Vector2(0.11f, 0.62f), 0.8f);

            splashLandmarks = Img(root, null, Color.white, 0);
            splashLandmarks.preserveAspect = true;
            splashLandmarks.rectTransform.anchorMin = new Vector2(0, 0.266f);
            splashLandmarks.rectTransform.anchorMax = new Vector2(1, 0.266f);
            splashLandmarks.rectTransform.pivot = new Vector2(0.5f, 0);
            splashLandmarks.rectTransform.offsetMin = Vector2.zero;
            splashLandmarks.rectTransform.sizeDelta = new Vector2(0, 690);
            splashGround = Img(root, Art.Gradient(Color.gray, Color.black), Color.white, 0);
            splashGround.rectTransform.anchorMin = Vector2.zero;
            splashGround.rectTransform.anchorMax = new Vector2(1, 0.266f);
            splashGround.rectTransform.offsetMin = splashGround.rectTransform.offsetMax = Vector2.zero;
            var strip = Img(root, Art.Rounded, new Color(1, 1, 1, 0.35f), 0);
            strip.rectTransform.anchorMin = new Vector2(0, 0.266f);
            strip.rectTransform.anchorMax = new Vector2(1, 0.266f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1);
            strip.rectTransform.sizeDelta = new Vector2(0, 14);
            strip.rectTransform.anchoredPosition = Vector2.zero;

            // Logo
            var logo = MakeRect("Logo", root);
            Anchor(logo, new Vector2(0.5f, 1), new Vector2(0, -430), new Vector2(940, 400));
            logo.localRotation = Quaternion.Euler(0, 0, 3f);
            var cargo = MakeText(logo, "CARGO", 230, TextAnchor.MiddleCenter, Art.LogoFont);
            cargo.color = new Color32(0xFF, 0xD2, 0x3A, 0xFF);
            AddOutline(cargo, new Color32(0x7A, 0x3D, 0x00, 0xFF), 9);
            AddShadow(cargo, new Color32(0x7A, 0x3D, 0x00, 0xFF), 20);
            Anchor(cargo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(940, 230));
            var sort = MakeText(logo, "SORT", 190, TextAnchor.MiddleCenter, Art.LogoFont);
            AddOutline(sort, new Color32(0x1B, 0x4A, 0xA3, 0xFF), 9);
            AddShadow(sort, new Color32(0x1B, 0x4A, 0xA3, 0xFF), 20);
            Anchor(sort.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(940, 200));

            splashCity = MakeText(root, "", 40, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddShadow(splashCity, new Color(0, 0, 0, 0.35f), 4);
            Anchor(splashCity.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -740), new Vector2(800, 60));

            // Loading bar, "Tap to start" and the version
            var barBack = Img(root, Art.Rounded, Color.white, 22);
            Anchor(barBack.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(560, 46));
            var barIn = Img(barBack.rectTransform, Art.Rounded, new Color32(0x2B, 0x11, 0x70, 0xFF), 17);
            Stretch(barIn.rectTransform);
            barIn.rectTransform.offsetMin = new Vector2(6, 6);
            barIn.rectTransform.offsetMax = new Vector2(-6, -6);
            var fill = Img(barIn.rectTransform, Art.Rounded, new Color32(0xFF, 0xB2, 0x2A, 0xFF), 17);
            splashFill = fill.rectTransform;
            splashFill.anchorMin = Vector2.zero;
            splashFill.anchorMax = new Vector2(0, 1);
            splashFill.offsetMin = splashFill.offsetMax = Vector2.zero;

            splashTap = MakeText(root, Loc.T("tap_start"), 64, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddOutline(splashTap, Ink, 5);
            Anchor(splashTap.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(800, 100));
            var version = MakeText(root, "v" + Application.version, 28, TextAnchor.MiddleCenter);
            version.color = new Color(1, 1, 1, 0.75f);
            Anchor(version.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(400, 44));

            var button = splashPanel.AddComponent<Button>();
            button.targetGraphic = splashSky;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (splashFadeStart >= 0f || Time.unscaledTime - splashShownAt < SplashLoadSeconds) return;
                Feedback.Click();
                splashFadeStart = Time.unscaledTime;
            });
            splashPanel.SetActive(false);
        }

        /// <summary>Shows the title screen for the current World Tour city. Call once, right after the HUD is built at start-up.</summary>
        public void ShowSplash()
        {
            int tour = Progress.TourCity;
            bool finished = WorldTour.Finished(tour);
            int shown = finished ? WorldTour.Cities.Length - 1 : tour, mask = finished ? WorldTour.AllBuilt : Progress.TourBuilt;
            var city = WorldTour.Cities[shown];
            Color low = Rgb(city.SkyLow);
            splashSky.sprite = Art.Gradient(Rgb(city.SkyHigh), low);
            splashSky.color = Color.white;
            splashGround.sprite = Art.Gradient(Art.Darken(low, 0.5f), Art.Darken(low, 0.72f));
            splashLandmarks.sprite = TourArt.SplashBackdrop(shown, mask);
            splashCity.text = Loc.Persian ? Loc.T("c_" + city.Key) : Loc.T("c_" + city.Key).ToUpperInvariant();
            splashTap.text = Loc.T("tap_start");
            splashTap.gameObject.SetActive(false);
            splashFill.anchorMax = new Vector2(0, 1);
            splashGroup.alpha = 1f;
            splashShownAt = Time.unscaledTime;
            splashFadeStart = -1f;
            splashPanel.SetActive(true);
            splashPanel.transform.SetAsLastSibling();
        }

        private void UpdateSplash()
        {
            if (!SplashVisible) return;
            float t = Time.unscaledTime - splashShownAt;
            splashFill.anchorMax = new Vector2(Mathf.Clamp01(1f - Mathf.Pow(1f - Mathf.Clamp01(t / SplashLoadSeconds), 3f)), 1);
            bool ready = t >= SplashLoadSeconds;
            splashTap.gameObject.SetActive(ready);
            if (ready) splashTap.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5.5f));
            if (splashFadeStart >= 0f)
            {
                float k = (Time.unscaledTime - splashFadeStart) / SplashFadeSeconds;
                splashGroup.alpha = 1f - Mathf.Clamp01(k);
                if (k >= 1f) { splashPanel.SetActive(false); splashFadeStart = -1f; }
            }
        }
    }
}
