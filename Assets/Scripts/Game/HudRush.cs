using System;
using CargoSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The Rush Week screen (points, the 5-step reward track, the group of 30) and its home button with the
    /// time left in the week.
    /// </summary>
    public sealed partial class Hud
    {
        public event Action OpenRush;
        public event Action<int> ClaimRush;

        private GameObject rushPanel, rushSide, rushSideDot;
        private RectTransform rushContent;
        private Text rushSideTime, rushEnds;
        private float nextRushTick;

        private void BuildRush(RectTransform safe)
        {
            var screen = MakeRect("Rush", safe);
            Stretch(screen);
            rushPanel = screen.gameObject;
            var bg = screen.gameObject.AddComponent<Image>();
            bg.sprite = Art.Gradient(new Color32(0x24, 0x39, 0x5A, 0xFF), new Color32(0x15, 0x23, 0x3A, 0xFF));

            var close = MakeCandy(screen, Red, () => rushPanel.SetActive(false), null, 0, "x", 60, 46);
            Anchor(close.Root, new Vector2(0, 1), new Vector2(110, -100), new Vector2(140, 140));
            var title = MakeText(screen, Loc.T("rush_week"), 74, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddShadow(title, new Color(0, 0, 0, 0.35f), 6);
            Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(560, 120));

            var viewport = MakeRect("Viewport", screen);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(0, -200);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            rushContent = MakeRect("Content", viewport);
            rushContent.anchorMin = new Vector2(0, 1);
            rushContent.anchorMax = new Vector2(1, 1);
            rushContent.pivot = new Vector2(0.5f, 1);
            rushContent.anchoredPosition = Vector2.zero;
            rushContent.sizeDelta = Vector2.zero;
            var layout = rushContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 30, 80);
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rushContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = rushContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            rushPanel.SetActive(false);
        }

        public bool RushOpen => rushPanel != null && rushPanel.activeSelf;

        public void ShowRush(bool show)
        {
            rushPanel.SetActive(show);
            if (!show) return;
            rushPanel.transform.SetAsLastSibling();
            toast.transform.SetAsLastSibling();
            RefreshRush();
            rushContent.anchoredPosition = Vector2.zero;
        }

        private RectTransform RushRow(float height)
        {
            var row = MakeRect("Row", rushContent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return row;
        }

        private void RushTitle(string text)
        {
            var t = MakeText(RushRow(80), text, 50, TextAnchor.MiddleLeft, Art.DisplayFont);
            t.color = Gold;
            AddShadow(t, new Color(0, 0, 0, 0.35f), 4);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
        }

        private Text RushNote(string text)
        {
            var t = MakeText(RushRow(70), text, 30, TextAnchor.MiddleCenter);
            t.color = Muted;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(t.rectTransform);
            return t;
        }

        /// <summary>Rebuilds the Rush Week screen.</summary>
        public void RefreshRush()
        {
            if (rushContent == null || !rushPanel.activeSelf) return;
            for (int i = rushContent.childCount - 1; i >= 0; i--) Destroy(rushContent.GetChild(i).gameObject);
            Rush.Rollover();
            int points = Progress.RushPoints, claimed = Progress.RushClaimed;

            // Header: time left, points, how to earn them
            var head = Card(RushRow(400), new Color32(0xF0, 0x6A, 0x2B, 0xFF));
            rushEnds = MakeText(head, Loc.T("rush_ends", Rush.LeftRaw), 34, TextAnchor.MiddleCenter);
            Anchor(rushEnds.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 60));
            var big = MakeText(head, Economy.Number(points), 130, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddShadow(big, new Color(0, 0, 0, 0.25f), 8);
            Anchor(big.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(700, 160));
            var unit = MakeText(head, Loc.T("rush_points"), 36, TextAnchor.MiddleCenter);
            Anchor(unit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(600, 60));
            var how = MakeText(head, Loc.T("rush_how"), 30, TextAnchor.MiddleCenter);
            how.horizontalOverflow = HorizontalWrapMode.Wrap;
            how.color = new Color(1, 1, 1, 0.88f);
            Anchor(how.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(900, 80));

            // Reward track
            RushTitle(Loc.T("rush_track"));
            for (int i = 0; i < RushWeek.Track.Length; i++)
            {
                int step = i, need = RushWeek.Track[i];
                bool got = points >= need, done = ((claimed >> i) & 1) == 1;
                var card = Card(RushRow(190), new Color32(0x2B, 0x46, 0x69, 0xFF));
                var circle = Img(card, Art.Circle, got ? Green : new Color(0, 0, 0, 0.3f), 0);
                Anchor(circle.rectTransform, new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(130, 130));
                var needText = MakeText(circle.rectTransform, Economy.Number(need), 44, TextAnchor.MiddleCenter, Art.DisplayFont);
                Stretch(needText.rectTransform);
                var items = MakeRect("Items", card);
                items.anchorMin = items.anchorMax = new Vector2(0, 0.5f);
                items.pivot = new Vector2(0, 0.5f);
                items.anchoredPosition = new Vector2(175, 0);
                items.sizeDelta = new Vector2(500, 150);
                var list = Rush.TrackRewards[i];
                for (int k = 0; k < list.Length; k++)
                {
                    var tile = ItemTile(items, list[k], 116);
                    Anchor(tile, new Vector2(0, 0.5f), new Vector2(58 + k * 124, 0), new Vector2(116, 140));
                }
                Candy b;
                if (done) b = MakeCandy(card, Green, null, null, 0, "check", 56, 36);
                else if (got) b = MakeCandy(card, Green, () => ClaimRush?.Invoke(step), Loc.T("claim"), 40, null, 0, 36);
                else b = MakeCandy(card, Quiet, null, Economy.Number(need - points), 40, null, 0, 36);
                Anchor(b.Root, new Vector2(1, 0.5f), new Vector2(-120, 6), new Vector2(190, 110));
                if (done) foreach (var g in card.GetComponentsInChildren<Graphic>()) g.color = new Color(g.color.r, g.color.g, g.color.b, g.color.a * 0.6f);
            }

            // The group
            RushTitle(Loc.T("rush_group"));
            RushNote(Loc.T("rush_prizes"));
            var rows = RushWeek.Standings(Rush.Week, points, Rush.WeekFraction);
            for (int i = 0; i < rows.Count; i++) RushStanding(i + 1, rows[i]);
            RushNote(Loc.T("rush_rivals_note")).rectTransform.parent.GetComponent<LayoutElement>().preferredHeight = 110;
        }

        private void RushStanding(int rank, RushWeek.Row row)
        {
            var r = RushRow(96);
            var bg = Img(r, Art.Rounded, row.IsPlayer ? (Color)Blue : new Color(1, 1, 1, 0.05f), 26);
            Stretch(bg.rectTransform);
            var rk = MakeText(bg.rectTransform, Economy.Number(rank), 42, TextAnchor.MiddleCenter, Art.DisplayFont);
            rk.color = row.IsPlayer ? Color.white : rank <= 3 ? Gold : Muted;
            Anchor(rk.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(100, 80));
            string name = row.IsPlayer ? Loc.T("you") : Loc.Persian ? PersianShaper.Fix(RushWeek.Names[row.Name, 1]) : RushWeek.Names[row.Name, 0];
            var nameText = MakeText(bg.rectTransform, name, 38, TextAnchor.MiddleLeft);
            nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0, 0.5f);
            nameText.rectTransform.pivot = new Vector2(0, 0.5f);
            nameText.rectTransform.anchoredPosition = new Vector2(130, 0);
            nameText.rectTransform.sizeDelta = new Vector2(360, 80);
            int prize = RushWeek.Prize(rank);
            if (prize > 0)
            {
                var coin = Coin(bg.rectTransform, 34);
                Anchor(coin, new Vector2(0, 0.5f), new Vector2(540, 0), new Vector2(34, 34));
                var p = MakeText(bg.rectTransform, Economy.Number(prize), 30, TextAnchor.MiddleLeft);
                p.color = Gold;
                p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(0, 0.5f);
                p.rectTransform.pivot = new Vector2(0, 0.5f);
                p.rectTransform.anchoredPosition = new Vector2(566, 0);
                p.rectTransform.sizeDelta = new Vector2(120, 60);
            }
            var pts = MakeText(bg.rectTransform, Economy.Number(row.Points), 42, TextAnchor.MiddleRight, Art.DisplayFont);
            Anchor(pts.rectTransform, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(180, 80));
        }

        // ----- Home: the Rush button with the time left -----

        private void BuildRushSide(RectTransform home)
        {
            rushSide = SideButton(home, Red, "bolt", "", new Vector2(1, 0.5f), new Vector2(-130, -420), () => OpenRush?.Invoke(), out rushSideTime).gameObject;
            var labelBg = Img((RectTransform)rushSideTime.transform.parent, Art.Rounded, new Color(0, 0, 0, 0.5f), 22);
            Anchor(labelBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(200, 50));
            labelBg.transform.SetSiblingIndex(rushSideTime.transform.GetSiblingIndex());
            rushSideTime.fontSize = 30;
            rushSideDot = Dot((RectTransform)rushSide.transform);
        }

        private void SetRushSide()
        {
            if (rushSide == null) return;
            Rush.Rollover();
            rushSide.SetActive(Rush.Open);
            rushSideTime.text = Rush.LeftText;
            rushSideDot.SetActive(Rush.Claimable);
        }

        /// <summary>Keeps the countdowns on the home button and the Rush screen current.</summary>
        private void UpdateRush()
        {
            if (Time.unscaledTime < nextRushTick) return;
            nextRushTick = Time.unscaledTime + 1f;
            if (rushSide != null && rushSide.activeInHierarchy) rushSideTime.text = Rush.LeftText;
            if (RushOpen && rushEnds != null) rushEnds.text = Loc.T("rush_ends", Rush.LeftRaw);
        }
    }
}
