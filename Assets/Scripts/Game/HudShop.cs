using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// The shop screen (starter pack, free gift, piggy bank, coin packs, bundles, crate and truck skins,
    /// themes) and the starter pack popup.
    /// </summary>
    public sealed partial class Hud
    {
        public event Action<Pack> BuyPack;
        public event Action<string, Skin> SkinTapped;   // kind: "crate" or "truck"
        public event Action<int> ThemeTapped;

        private GameObject shopPanel, starterPanel;
        private RectTransform shopContent, starterBody;
        private Text shopCoins;
        private readonly List<Text> starterTimers = new List<Text>();
        private float nextTimerUpdate;

        private void BuildShop(RectTransform safe)
        {
            var screen = MakeRect("Shop", safe);
            Stretch(screen);
            shopPanel = screen.gameObject;
            var bg = screen.gameObject.AddComponent<Image>();
            bg.sprite = Art.Gradient(new Color32(0x7A, 0x48, 0xD8, 0xFF), new Color32(0x34, 0x19, 0x6F, 0xFF));

            var close = MakeCandy(screen, Red, () => shopPanel.SetActive(false), null, 0, "x", 60, 46);
            Anchor(close.Root, new Vector2(0, 1), new Vector2(110, -100), new Vector2(140, 140));
            var title = MakeText(screen, Loc.T("shop"), 80, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddOutline(title, Ink, 5);
            Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(500, 120));
            var pill = MakeRect("Coins", screen);
            Anchor(pill, new Vector2(1, 1), new Vector2(-190, -100), new Vector2(300, 104));
            Stretch(Img(pill, Art.Rounded, new Color(0, 0, 0, 0.38f), 52).rectTransform);
            var coin = Coin(pill, 80);
            Anchor(coin, new Vector2(0, 0.5f), new Vector2(52, 0), new Vector2(80, 80));
            shopCoins = MakeText(pill, "0", 46, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(shopCoins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36, 2), new Vector2(200, 90));

            // Scrolling list
            var viewport = MakeRect("Viewport", screen);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(0, -200);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            shopContent = MakeRect("Content", viewport);
            shopContent.anchorMin = new Vector2(0, 1);
            shopContent.anchorMax = new Vector2(1, 1);
            shopContent.pivot = new Vector2(0.5f, 1);
            shopContent.anchoredPosition = Vector2.zero;
            shopContent.sizeDelta = Vector2.zero;
            var layout = shopContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 30, 80);
            layout.spacing = 34;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            shopContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = shopContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            shopPanel.SetActive(false);
        }

        public void ShowShop(bool show)
        {
            shopPanel.SetActive(show);
            if (!show) return;
            shopPanel.transform.SetAsLastSibling();
            toast.transform.SetAsLastSibling();
            RefreshShop();
            shopContent.anchoredPosition = Vector2.zero;
        }

        /// <summary>Rebuilds the shop list for the current coins, offers and owned items.</summary>
        public void RefreshShop()
        {
            if (shopContent == null || !shopPanel.activeSelf) return;
            SetCoins(Progress.Coins);
            for (int i = shopContent.childCount - 1; i >= 0; i--) Destroy(shopContent.GetChild(i).gameObject);
            starterTimers.RemoveAll(t => t == null || !t.transform.IsChildOf(starterPanel.transform));
            bool paid = MonetizationServices.PurchasesEnabled;

            if (Economy.StarterActive) StarterHero(Section(560), false);
            ChestRow(Section(210));
            if (paid) PiggyHero(Section(560));

            if (paid)
            {
                SectionTitle(Loc.T("coins_sect"));
                for (int i = 0; i < Economy.CoinPacks.Length; i += 2)
                {
                    var row = Section(400);
                    CoinTile(row, Economy.CoinPacks[i], i, true);
                    if (i + 1 < Economy.CoinPacks.Length) CoinTile(row, Economy.CoinPacks[i + 1], i + 1, false);
                }
                SectionTitle(Loc.T("bundles"));
                BundleHero(Section(560), Economy.BoosterBundle, "booster_bundle", "booster_bundle_sub", new Color32(0x4A, 0x6C, 0xF0, 0xFF));
                if (!Progress.AdsRemoved) BundleHero(Section(560), Economy.NoAds, "noads_pack", "noads_pack_sub", new Color32(0xD9, 0x48, 0x3E, 0xFF));
            }

            SectionTitle(Loc.T("crates"));
            foreach (var skin in Economy.CrateSkins) SkinRow("crate", skin);
            SectionTitle(Loc.T("trucks_sect"));
            foreach (var skin in Economy.TruckSkins) SkinRow("truck", skin);
            SectionTitle(Loc.T("themes"));
            for (int i = 0; i < Themes.All.Length; i++) ThemeRow(i);
        }

        private RectTransform Section(float height)
        {
            var row = MakeRect("Row", shopContent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return row;
        }

        private void SectionTitle(string text)
        {
            var row = Section(80);
            var t = MakeText(row, text, 50, TextAnchor.MiddleLeft, Art.DisplayFont);
            t.color = Gold;
            AddOutline(t, new Color32(0x5A, 0x2A, 0x00, 0xFF), 3);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 0);
        }

        private static RectTransform Card(RectTransform row, Color color)
        {
            var shadow = Img(row, Art.Rounded, Ink, 54);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-6, -16);
            shadow.rectTransform.offsetMax = new Vector2(6, 6);
            var card = Img(row, Art.Rounded, color, 50);
            Stretch(card.rectTransform);
            var shine = Img(card.rectTransform, Art.Rounded, new Color(1, 1, 1, 0.16f), 50);
            shine.rectTransform.anchorMin = new Vector2(0, 0.5f);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;
            return card.rectTransform;
        }

        private void Tag(RectTransform card, string key, Vector2 anchor, Vector2 pos)
        {
            if (string.IsNullOrEmpty(key)) return;
            var tag = Pill(card, new Color32(0xFF, 0xE0, 0x66, 0xFF), "", new Vector2(250, 60), 30);
            Anchor(tag, anchor, pos, new Vector2(260, 70));
            var t = MakeText(tag, Loc.T(key), 30, TextAnchor.MiddleCenter);
            t.color = new Color32(0x7A, 0x3A, 0x00, 0xFF);
            Stretch(t.rectTransform);
        }

        private Text CardTitle(RectTransform card, string text, float y, int size = 60)
        {
            var t = MakeText(card, text, size, TextAnchor.MiddleCenter, Art.DisplayFont);
            AddShadow(t, new Color(0, 0, 0, 0.3f), 5);
            Anchor(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(800, 90));
            return t;
        }

        private Candy PriceButton(RectTransform card, Pack pack, float y, float width, string label = null)
        {
            var b = MakeCandy(card, Green, () => BuyPack?.Invoke(pack), label ?? pack.Price, 48);
            Anchor(b.Root, new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(width, 140));
            return b;
        }

        // ----- Starter pack -----

        private void StarterHero(RectTransform row, bool inPopup)
        {
            var card = inPopup ? row : Card(row, new Color32(0xD9, 0x45, 0x75, 0xFF));
            Tag(card, Economy.Starter.TagKey, new Vector2(1, 1), new Vector2(-150, -6));
            if (!inPopup) CardTitle(card, Loc.T("starter_pack"), -70);
            var items = MakeRect("Items", card);
            Anchor(items, new Vector2(0.5f, 0.5f), new Vector2(0, inPopup ? 70 : 20), new Vector2(860, 200));
            ItemRow(items, Economy.Starter.Items, 165);
            var timerBg = Img(card, Art.Rounded, new Color(0, 0, 0, 0.45f), 26);
            Anchor(timerBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, inPopup ? 210 : 200), new Vector2(380, 56));
            var timer = MakeText(timerBg.rectTransform, Loc.T("ends_in", Economy.Clock(Economy.StarterLeft)), 32, TextAnchor.MiddleCenter);
            Stretch(timer.rectTransform);
            starterTimers.Add(timer);

            var buy = PriceButton(card, Economy.Starter, 80, 620);
            // Old price, struck through, on the left of the button.
            buy.Label.rectTransform.offsetMin = new Vector2(140, 0);
            var old = MakeText(buy.Top, Economy.Starter.OldPrice, 34, TextAnchor.MiddleCenter);
            old.color = new Color(1, 1, 1, 0.75f);
            Anchor(old.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 0), new Vector2(240, 60));
            var strike = Img(old.rectTransform, null, new Color(1, 1, 1, 0.8f), 0);
            Anchor(strike.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(old.preferredWidth + 10, 5));
        }

        private void BuildStarterPanel(RectTransform safe)
        {
            starterPanel = MakePopup(safe, new Vector2(920, 840), out var box);
            box.GetComponentInChildren<Image>().color = new Color32(0xD9, 0x45, 0x75, 0xFF);
            CloseButton(box, () => starterPanel.SetActive(false));
            var ribbon = Pill(box, Gold, "", new Vector2(520, 100), 50);
            Anchor(ribbon, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(530, 110));
            var title = MakeText(ribbon, Loc.T("starter_pack"), 56, TextAnchor.MiddleCenter, Art.DisplayFont);
            title.color = new Color32(0x7A, 0x3A, 0x00, 0xFF);
            Stretch(title.rectTransform);
            starterBody = MakeRect("Body", box);
            Stretch(starterBody);
            starterPanel.SetActive(false);
        }

        public void ShowStarter(bool show)
        {
            starterPanel.SetActive(show);
            if (!show) return;
            for (int i = starterBody.childCount - 1; i >= 0; i--) Destroy(starterBody.GetChild(i).gameObject);
            starterTimers.RemoveAll(t => t == null || t.transform.IsChildOf(starterPanel.transform));
            StarterHero(starterBody, true);
            starterPanel.transform.SetAsLastSibling();
            PopIn(starterPanel);
        }

        private void UpdateShopTimers()
        {
            if (Time.unscaledTime < nextTimerUpdate || starterTimers.Count == 0) return;
            nextTimerUpdate = Time.unscaledTime + 0.5f;
            var left = Economy.StarterLeft;
            string text = Loc.T("ends_in", Economy.Clock(left));
            foreach (var t in starterTimers) if (t != null) t.text = text;
            if (left <= TimeSpan.Zero)
            {
                starterTimers.Clear();
                if (starterPanel.activeSelf) starterPanel.SetActive(false);
                if (shopPanel.activeSelf) RefreshShop();
            }
        }

        // ----- Free gift, piggy bank -----

        private void ChestRow(RectTransform row)
        {
            var card = Card(row, PanelLight);
            var art = Img(card, Art.Rounded, Yellow, 36);
            Anchor(art.rectTransform, new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(150, 150));
            var ic = Icon(art.rectTransform, "gift", 96);
            Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            RowTexts(card, Loc.T("chest"), Loc.T("chest_sub"));
            bool ready = Economy.ChestReady;
            var b = ready
                ? MakeCandy(card, Green, () => OpenChest?.Invoke(), Loc.T("open"), 42, null, 0, 36, Loc.T("watch_ad"))
                : MakeCandy(card, Quiet, null, Loc.T("chest_tomorrow"), 38, null, 0, 36);
            Anchor(b.Root, new Vector2(1, 0.5f), new Vector2(-170, 6), new Vector2(280, 130));
        }

        private void RowTexts(RectTransform card, string title, string sub, float left = 215)
        {
            var t = MakeText(card, title, 46, TextAnchor.MiddleLeft, Art.DisplayFont);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 0.5f);
            t.rectTransform.pivot = new Vector2(0, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(left, 28);
            t.rectTransform.sizeDelta = new Vector2(440, 70);
            var s = MakeText(card, sub, 30, TextAnchor.MiddleLeft);
            s.color = Muted;
            s.horizontalOverflow = HorizontalWrapMode.Wrap;
            s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0, 0.5f);
            s.rectTransform.pivot = new Vector2(0, 0.5f);
            s.rectTransform.anchoredPosition = new Vector2(left, -32);
            s.rectTransform.sizeDelta = new Vector2(Mathf.Max(260, 880 - left - 320), 70);
        }

        private void PiggyHero(RectTransform row)
        {
            var card = Card(row, new Color32(0xE0, 0x6A, 0x96, 0xFF));
            CardTitle(card, Loc.T("piggy_bank"), -70);
            int coins = Progress.Piggy;
            var pig = Icon(card, "piggy", 150);
            pig.color = new Color32(0xFF, 0xE3, 0xEE, 0xFF);
            Anchor(pig.rectTransform, new Vector2(0.5f, 1), new Vector2(-150, -205), new Vector2(150, 150));
            var coin = Coin(card, 80);
            Anchor(coin, new Vector2(0.5f, 1), new Vector2(10, -205), new Vector2(80, 80));
            var amount = MakeText(card, Economy.Number(coins), 84, TextAnchor.MiddleLeft, Art.DisplayFont);
            amount.color = Gold;
            AddOutline(amount, new Color32(0x8A, 0x4A, 0x00, 0xFF), 4);
            amount.rectTransform.anchorMin = amount.rectTransform.anchorMax = new Vector2(0.5f, 1);
            amount.rectTransform.pivot = new Vector2(0, 0.5f);
            amount.rectTransform.anchoredPosition = new Vector2(70, -205);
            amount.rectTransform.sizeDelta = new Vector2(300, 100);

            var meter = Img(card, Art.Rounded, new Color(0, 0, 0, 0.3f), 20);
            meter.rectTransform.anchorMin = new Vector2(0, 1);
            meter.rectTransform.anchorMax = new Vector2(1, 1);
            meter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            meter.rectTransform.anchoredPosition = new Vector2(0, -305);
            meter.rectTransform.sizeDelta = new Vector2(-80, 40);
            var fill = Img(meter.rectTransform, Art.Rounded, Gold, 20);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(coins / (float)Economy.PiggyMax), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var sub = MakeText(card, Loc.T("piggy_sub"), 30, TextAnchor.MiddleCenter);
            Anchor(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(900, 50));

            if (coins >= Economy.PiggyMin) PriceButton(card, Economy.Piggy, 80, 620, Loc.T("piggy_break", Economy.Piggy.PriceRaw));
            else
            {
                var b = MakeCandy(card, Quiet, null, Loc.T("piggy_min", Economy.PiggyMin), 44);
                Anchor(b.Root, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(620, 140));
            }
        }

        // ----- Coins and bundles -----

        private void CoinTile(RectTransform row, Pack pack, int index, bool left)
        {
            // Two tiles share the row: each takes half the width (the row width depends on the phone).
            var holder = MakeRect("Tile", row);
            holder.anchorMin = new Vector2(left ? 0 : 0.5f, 0);
            holder.anchorMax = new Vector2(left ? 0.5f : 1, 1);
            holder.offsetMin = new Vector2(left ? 0 : 16, 0);
            holder.offsetMax = new Vector2(left ? -16 : 0, 0);
            var card = Card(holder, PanelLight);
            Tag(card, pack.TagKey, new Vector2(0.5f, 1), new Vector2(0, -6));
            int coins = index + 2;
            for (int i = 0; i < coins; i++)
            {
                var c = Coin(card, 80);
                Anchor(c, new Vector2(0.5f, 1), new Vector2((i - (coins - 1) / 2f) * 46, -110 - (i % 2) * 14), new Vector2(80, 80));
            }
            var amount = MakeText(card, Economy.Number(pack.Items[0].Amount), 66, TextAnchor.MiddleCenter, Art.DisplayFont);
            amount.color = Gold;
            AddOutline(amount, new Color32(0x8A, 0x4A, 0x00, 0xFF), 4);
            Anchor(amount.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(400, 90));
            var b = MakeCandy(card, Green, () => BuyPack?.Invoke(pack), pack.Price, 40);
            Anchor(b.Root, new Vector2(0.5f, 0), new Vector2(0, 75), new Vector2(340, 120));
        }

        private void BundleHero(RectTransform row, Pack pack, string titleKey, string subKey, Color color)
        {
            var card = Card(row, color);
            Tag(card, pack.TagKey, new Vector2(1, 1), new Vector2(-150, -6));
            CardTitle(card, Loc.T(titleKey), -70);
            var sub = MakeText(card, Loc.T(subKey), 32, TextAnchor.MiddleCenter);
            Anchor(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(900, 50));
            var items = MakeRect("Items", card);
            Anchor(items, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(860, 200));
            ItemRow(items, pack.Items, pack.Items.Length > 4 ? 140 : 165);
            PriceButton(card, pack, 80, 620);
        }

        // ----- Skins and themes -----

        private void SkinRow(string kind, Skin skin)
        {
            var row = Section(210);
            var card = Card(row, PanelLight);
            bool truck = kind == "truck";
            float artWidth = truck ? 250 : 150;
            var art = Img(card, Art.Rounded, Art.Background, 36);
            Anchor(art.rectTransform, new Vector2(0, 0.5f), new Vector2(35 + artWidth / 2, 0), new Vector2(artWidth, 150));
            var holder = MakeRect("Preview", art.rectTransform);
            Stretch(holder);
            holder.gameObject.AddComponent<RectMask2D>();
            if (truck) DrawShapes(holder, Art.TruckShapes(3, 2, skin.Key), 48f, new Vector2(-2, -5));
            else DrawShapes(holder, Art.CrateShapes(1, 0.86f, skin.Key), 120f, new Vector2(0, 4));

            bool owned = Progress.OwnsSkin(kind, skin);
            string current = truck ? Progress.TruckSkin : Progress.CrateSkin;
            RowTexts(card, Loc.T((truck ? "truck_" : "skin_") + skin.Key), Loc.T(owned ? "owned" : "skin_sub"), 70 + artWidth);
            OwnedButton(card, skin.Key == current, owned, skin.Price, () => SkinTapped?.Invoke(kind, skin));
        }

        private void ThemeRow(int index)
        {
            var theme = Themes.All[index];
            var row = Section(210);
            var card = Card(row, PanelLight);
            var swatch = Img(card, Art.Rounded, theme.Background, 36);
            Anchor(swatch.rectTransform, new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(150, 150));
            var ground = Img(swatch.rectTransform, Art.Rounded, theme.Ground, 14);
            Anchor(ground.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(110, 40));
            bool owned = Progress.OwnsTheme(index);
            RowTexts(card, Loc.T(theme.NameKey), Loc.T(owned ? "owned" : "theme_sub"));
            OwnedButton(card, index == Progress.Theme, owned, theme.Price, () => ThemeTapped?.Invoke(index));
        }

        /// <summary>Selected (gray), Use (blue), or a coin price (green if affordable).</summary>
        private void OwnedButton(RectTransform card, bool selected, bool owned, int price, Action onTap)
        {
            Candy b;
            if (selected) b = MakeCandy(card, Quiet, null, Loc.T("selected"), 36, null, 0, 36);
            else if (owned) b = MakeCandy(card, Blue, onTap, Loc.T("use"), 40, null, 0, 36);
            else
            {
                b = MakeCandy(card, Progress.Coins >= price ? Green : Quiet, onTap, Economy.Number(price), 42, null, 0, 36);
                b.Label.rectTransform.offsetMin = new Vector2(50, 0);
                var coin = Coin(b.Top, 50);
                Anchor(coin, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(50, 50));
            }
            Anchor(b.Root, new Vector2(1, 0.5f), new Vector2(-170, 6), new Vector2(280, 120));
        }
    }
}
