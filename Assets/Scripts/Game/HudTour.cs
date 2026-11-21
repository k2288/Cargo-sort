using System;
using CargoSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The World Tour screen: the current city's skyline, its 4 landmarks to build with stars, and a grid of
    /// all cities (visited, current, locked). Also the city banner on the home screen.
    /// </summary>
    public sealed partial class Hud
    {
        public event Action OpenTour;
        public event Action<int> BuildLandmark;

        private GameObject tourPanel;
        private RectTransform tourContent;
        private Text tourStars;

        // Home banner
        private Image cityBarSky;
        private Text cityBarName, cityBarStars;
        private RectTransform cityBarFill;
        private GameObject cityBarDot, tourSideDot;

        private void BuildTour(RectTransform safe)
        {
            var screen = MakeRect("Tour", safe);
            Stretch(screen);
            tourPanel = screen.gameObject;
            var bg = screen.gameObject.AddComponent<Image>();
            bg.sprite = Art.Gradient(new Color32(0x24, 0x39, 0x5A, 0xFF), new Color32(0x15, 0x23, 0x3A, 0xFF));

            var close = MakeCandy(screen, Red, () => tourPanel.SetActive(false), null, 0, "x", 60, 46);
            Anchor(close.Root, new Vector2(0, 1), new Vector2(110, -100), new Vector2(140, 140));
            var title = MakeText(screen, Loc.T("world_tour"), 74, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddShadow(title, new Color(0, 0, 0, 0.35f), 6);
            Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(520, 120));
            var pill = MakeRect("Stars", screen);
            Anchor(pill, new Vector2(1, 1), new Vector2(-170, -100), new Vector2(240, 104));
            Stretch(Img(pill, Art.Rounded, new Color(0, 0, 0, 0.38f), 52).rectTransform);
            var star = Icon(pill, "star", 64);
            star.color = Gold;
            Anchor(star.rectTransform, new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(64, 64));
            tourStars = MakeText(pill, "0", 46, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(tourStars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30, 2), new Vector2(150, 90));

            var viewport = MakeRect("Viewport", screen);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(0, -200);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            tourContent = MakeRect("Content", viewport);
            tourContent.anchorMin = new Vector2(0, 1);
            tourContent.anchorMax = new Vector2(1, 1);
            tourContent.pivot = new Vector2(0.5f, 1);
            tourContent.anchoredPosition = Vector2.zero;
            tourContent.sizeDelta = Vector2.zero;
            var layout = tourContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 30, 80);
            layout.spacing = 34;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            tourContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = tourContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            tourPanel.SetActive(false);
        }

        public bool TourOpen => tourPanel != null && tourPanel.activeSelf;

        public void ShowTour(bool show)
        {
            tourPanel.SetActive(show);
            if (!show) return;
            tourPanel.transform.SetAsLastSibling();
            toast.transform.SetAsLastSibling();
            RefreshTour();
            tourContent.anchoredPosition = Vector2.zero;
        }

        private RectTransform TourRow(float height)
        {
            var row = MakeRect("Row", tourContent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return row;
        }

        private void TourTitle(string text)
        {
            var t = MakeText(TourRow(80), text, 50, TextAnchor.MiddleLeft, Art.DisplayFont);
            t.color = Gold;
            AddShadow(t, new Color(0, 0, 0, 0.35f), 4);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
        }

        /// <summary>Rebuilds the tour screen for the current city, landmarks and stars.</summary>
        public void RefreshTour()
        {
            if (tourContent == null || !tourPanel.activeSelf) return;
            for (int i = tourContent.childCount - 1; i >= 0; i--) Destroy(tourContent.GetChild(i).gameObject);
            int city = Progress.TourCity, mask = Progress.TourBuilt, stars = Progress.Stars;
            tourStars.text = Economy.Number(stars);

            if (!WorldTour.Finished(city))
            {
                var c = WorldTour.Cities[city];
                // City card: skyline, name, "City 1 of 8", progress bar
                var card = Card(TourRow(780), new Color32(0x26, 0x40, 0x5F, 0xFF));
                var sky = Img(card, TourArt.Skyline(city, mask), Color.white, 0);
                sky.rectTransform.anchorMin = new Vector2(0, 1);
                sky.rectTransform.anchorMax = new Vector2(1, 1);
                sky.rectTransform.pivot = new Vector2(0.5f, 1);
                sky.rectTransform.offsetMin = new Vector2(20, -560);
                sky.rectTransform.offsetMax = new Vector2(-20, -20);
                var name = MakeText(card, Loc.T("c_" + c.Key), 66, TextAnchor.MiddleLeft, Art.DisplayFont);
                AddShadow(name, new Color(0, 0, 0, 0.3f), 5);
                Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(330, 150), new Vector2(580, 90));
                var count = MakeText(card, Loc.T("city_n", city + 1, WorldTour.Cities.Length), 32, TextAnchor.MiddleRight);
                count.color = Muted;
                Anchor(count.rectTransform, new Vector2(1, 0), new Vector2(-230, 150), new Vector2(400, 60));
                Meter(card, new Vector2(0, 70), WorldTour.BuiltCount(mask) / (float)WorldTour.MarksPerCity, 900, 36);

                TourTitle(Loc.T("landmarks"));
                for (int i = 0; i < WorldTour.MarksPerCity; i++) LandmarkRow(city, mask, stars, i);
                if (!WorldTour.CanBuildAny(city, mask, stars) && mask != WorldTour.AllBuilt)
                {
                    var hint = MakeText(TourRow(60), Loc.T("need_stars"), 34, TextAnchor.MiddleCenter);
                    hint.color = Muted;
                    Stretch(hint.rectTransform);
                }
            }
            else
            {
                var card = Card(TourRow(720), new Color32(0x26, 0x40, 0x5F, 0xFF));
                var sky = Img(card, TourArt.Skyline(WorldTour.Cities.Length - 1, WorldTour.AllBuilt), Color.white, 0);
                sky.rectTransform.anchorMin = new Vector2(0, 1);
                sky.rectTransform.anchorMax = new Vector2(1, 1);
                sky.rectTransform.pivot = new Vector2(0.5f, 1);
                sky.rectTransform.offsetMin = new Vector2(20, -560);
                sky.rectTransform.offsetMax = new Vector2(-20, -20);
                var done = MakeText(card, Loc.T("tour_done"), 40, TextAnchor.MiddleCenter);
                done.horizontalOverflow = HorizontalWrapMode.Wrap;
                Anchor(done.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(860, 120));
            }

            TourTitle(Loc.T("cities"));
            for (int r = 0; r * 4 < WorldTour.Cities.Length; r++)
            {
                var row = TourRow(290);
                for (int k = 0; k < 4 && r * 4 + k < WorldTour.Cities.Length; k++) StampTile(row, r * 4 + k, k, city, mask);
            }
        }

        private void LandmarkRow(int city, int mask, int stars, int i)
        {
            var mark = WorldTour.Cities[city].Marks[i];
            bool built = WorldTour.IsBuilt(mask, i);
            int cost = WorldTour.Cost(city, i);
            var card = Card(TourRow(210), new Color32(0x33, 0x50, 0x7A, 0xFF));
            var art = Img(card, TourArt.Mark(city, i, built), Color.white, 0);
            Anchor(art.rectTransform, new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(150, 150));
            RowTexts(card, Loc.T("l_" + mark.Key), "+" + Economy.Number(WorldTour.MarkCoins(city, i)), 230);
            var coin = Coin(card, 36);
            Anchor(coin, new Vector2(0, 0.5f), new Vector2(214, -32), new Vector2(36, 36));
            foreach (var t in card.GetComponentsInChildren<Text>())
                if (t.text.StartsWith("+")) t.rectTransform.anchoredPosition += new Vector2(26, 0);

            Candy b;
            if (built) b = MakeCandy(card, Green, null, Loc.T("built"), 38, null, 0, 36);
            else
            {
                b = MakeCandy(card, stars >= cost ? Green : Quiet, () => BuildLandmark?.Invoke(i), Economy.Number(cost), 44, null, 0, 36);
                b.Label.rectTransform.offsetMin = new Vector2(50, 0);
                var star = Icon(b.Top, "star", 52);
                Anchor(star.rectTransform, new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(52, 52));
            }
            if (built)
            {
                var check = Icon(b.Top, "check", 40);
                Anchor(check.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(40, 40));
                b.Label.rectTransform.offsetMin = new Vector2(40, 0);
            }
            Anchor(b.Root, new Vector2(1, 0.5f), new Vector2(-150, 6), new Vector2(240, 120));
        }

        /// <summary>A city in the cities grid: colored when visited, outlined when current, dark with a lock when locked.</summary>
        private void StampTile(RectTransform row, int index, int column, int current, int mask)
        {
            var tile = MakeRect("City", row);
            tile.anchorMin = new Vector2(column / 4f, 0);
            tile.anchorMax = new Vector2((column + 1) / 4f, 1);
            tile.offsetMin = new Vector2(10, 0);
            tile.offsetMax = new Vector2(-10, 0);
            bool done = index < current, now = index == current, locked = index > current;
            if (now)
            {
                var ring = Img(tile, Art.Rounded, Gold, 34);
                ring.rectTransform.anchorMin = new Vector2(0, 1);
                ring.rectTransform.anchorMax = new Vector2(1, 1);
                ring.rectTransform.pivot = new Vector2(0.5f, 1);
                ring.rectTransform.offsetMin = new Vector2(-8, -218);
                ring.rectTransform.offsetMax = new Vector2(8, 8);
            }
            var pic = Img(tile, TourArt.Stamp(index, done ? WorldTour.AllBuilt : now ? mask : 0), locked ? new Color(0.45f, 0.45f, 0.5f) : Color.white, 0);
            pic.rectTransform.anchorMin = new Vector2(0, 1);
            pic.rectTransform.anchorMax = new Vector2(1, 1);
            pic.rectTransform.pivot = new Vector2(0.5f, 1);
            pic.rectTransform.offsetMin = new Vector2(0, -210);
            pic.rectTransform.offsetMax = Vector2.zero;
            var name = MakeText(tile, Loc.T("c_" + WorldTour.Cities[index].Key), 30, TextAnchor.MiddleCenter);
            name.color = Muted;
            Anchor(name.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(240, 50));
            if (done || locked)
            {
                var badge = Img(tile, Art.Circle, done ? Green : Quiet, 0);
                Anchor(badge.rectTransform, new Vector2(1, 1), new Vector2(-10, -4), new Vector2(64, 64));
                var ic = Icon(badge.rectTransform, done ? "check" : "lock", 38);
                Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            }
        }

        private void Meter(RectTransform parent, Vector2 bottomPos, float fill, float width, float height)
        {
            var back = Img(parent, Art.Rounded, new Color(0, 0, 0, 0.3f), height / 2);
            Anchor(back.rectTransform, new Vector2(0.5f, 0), bottomPos, new Vector2(width, height));
            var bar = Img(back.rectTransform, Art.Rounded, Gold, height / 2);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            bar.gameObject.SetActive(fill > 0);
        }

        // ----- Home: city banner and the Tour side button -----

        private void BuildCityBar(RectTransform home)
        {
            var bar = MakeCandy(home, new Color32(0x3A, 0x5B, 0x88, 0xFF), () => OpenTour?.Invoke(), null, 0, null, 0, 44);
            Anchor(bar.Root, new Vector2(0.5f, 1), new Vector2(0, -690), new Vector2(780, 160));
            cityBarSky = Img(bar.Top, null, Color.white, 0);
            Anchor(cityBarSky.rectTransform, new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(220, 132));
            cityBarName = MakeText(bar.Top, "", 50, TextAnchor.MiddleLeft, Art.DisplayFont);
            cityBarName.rectTransform.anchorMin = cityBarName.rectTransform.anchorMax = new Vector2(0, 0.5f);
            cityBarName.rectTransform.pivot = new Vector2(0, 0.5f);
            cityBarName.rectTransform.anchoredPosition = new Vector2(262, 22);
            cityBarName.rectTransform.sizeDelta = new Vector2(320, 70);
            var back = Img(bar.Top, Art.Rounded, new Color(0, 0, 0, 0.3f), 13);
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0, 0.5f);
            back.rectTransform.pivot = new Vector2(0, 0.5f);
            back.rectTransform.anchoredPosition = new Vector2(262, -32);
            back.rectTransform.sizeDelta = new Vector2(300, 26);
            var fill = Img(back.rectTransform, Art.Rounded, Gold, 13);
            cityBarFill = fill.rectTransform;
            cityBarFill.anchorMin = Vector2.zero;
            cityBarFill.offsetMin = cityBarFill.offsetMax = Vector2.zero;

            var chip = Img(bar.Top, Art.Rounded, new Color(0, 0, 0, 0.3f), 36);
            Anchor(chip.rectTransform, new Vector2(1, 0.5f), new Vector2(-105, 0), new Vector2(170, 72));
            var star = Icon(chip.rectTransform, "star", 46);
            star.color = Gold;
            Anchor(star.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(46, 46));
            cityBarStars = MakeText(chip.rectTransform, "0", 42, TextAnchor.MiddleCenter, Art.DisplayFont);
            cityBarStars.color = new Color32(0xFF, 0xE0, 0x66, 0xFF);
            Anchor(cityBarStars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(24, 2), new Vector2(110, 70));
            cityBarDot = Dot(bar.Root);

            var tour = SideButton(home, Blue, "globe", Loc.T("tour_short"), new Vector2(0, 0.5f), new Vector2(130, 40), () => OpenTour?.Invoke(), out _);
            tourSideDot = Dot(tour);
        }

        private void SetCityBar()
        {
            if (cityBarSky == null) return;
            int city = Progress.TourCity;
            bool finished = WorldTour.Finished(city);
            int shown = finished ? WorldTour.Cities.Length - 1 : city, mask = finished ? WorldTour.AllBuilt : Progress.TourBuilt;
            cityBarSky.sprite = TourArt.Skyline(shown, mask);
            cityBarName.text = Loc.T("c_" + WorldTour.Cities[shown].Key);
            cityBarFill.anchorMax = new Vector2(WorldTour.BuiltCount(mask) / (float)WorldTour.MarksPerCity, 1);
            cityBarFill.gameObject.SetActive(WorldTour.BuiltCount(mask) > 0);
            cityBarStars.text = Economy.Number(Progress.Stars);
            bool canBuild = WorldTour.CanBuildAny(city, Progress.TourBuilt, Progress.Stars);
            cityBarDot.SetActive(canBuild);
            tourSideDot.SetActive(canBuild);
        }
    }
}
