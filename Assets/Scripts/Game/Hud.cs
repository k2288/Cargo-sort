using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>
    /// All screen UI, built in code: top bar, booster bar, popups (win, lose, daily gift, settings,
    /// rewards), the home screen (HudHome.cs) and the shop (HudShop.cs).
    /// Buttons are glossy "candy" buttons that press down, like the web preview.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public event Action Restart, Revive, Retry, LoseUndo, OpenShop, Challenge, ToggleSound, ToggleVibration,
            ToggleLanguage, GoHome, Play, OpenChest, OpenStarter, RestorePurchases, Privacy, RewardClaimed;
        public event Action<bool> ClaimWin;   // true = x3 with ad
        public event Action<bool> ClaimDaily; // true = x2 with ad
        public event Action<Booster> BoosterTapped;
        public event Action<int> DevGoToLevel;
        public event Action DevAddCoins;

        // Candy colors (face; the bottom edge is a darker shade).
        public static readonly Color Yellow = new Color32(0xFF, 0xB2, 0x2A, 0xFF);
        public static readonly Color Green = new Color32(0x45, 0xC2, 0x55, 0xFF);
        public static readonly Color Blue = new Color32(0x3A, 0x94, 0xF0, 0xFF);
        public static readonly Color Purple = new Color32(0x9A, 0x5B, 0xEA, 0xFF);
        public static readonly Color Red = new Color32(0xEE, 0x54, 0x44, 0xFF);
        public static readonly Color Quiet = new Color32(0x80, 0x94, 0xAF, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xCC, 0x33, 0xFF);
        private static readonly Color CardColor = new Color32(0x26, 0x3D, 0x5E, 0xFF);
        private static readonly Color Muted = new Color32(0xB6, 0xC6, 0xDA, 0xFF);

        private GameObject canvasRoot;
        private RectTransform safeRect;
        private Text levelText, trucksText, coinsText, toastText, tipText, craneTipText;
        private Text winTitle, winReward, winStreak, winPiggy, dailyTitle, dailyReward, loseWarn;
        private GameObject tip, toast, winPanel, losePanel, dailyPanel, settingsPanel, rewardPanel, craneTip, gameRow, winChips;
        private GameObject topLeft, titleBox, boosterBar, restoreButton, privacyButton;
        private RectTransform coinPill, rewardItems;
        private Text rewardTitle;
        private Candy soundSwitch, vibeSwitch;
        private readonly Dictionary<Booster, Candy> boosterButtons = new Dictionary<Booster, Candy>();
        private readonly Dictionary<Booster, RectTransform> boosterBadges = new Dictionary<Booster, RectTransform>();
        private float toastUntil;
        private bool craneOn;

        public bool PanelOpen =>
            winPanel.activeSelf || losePanel.activeSelf || dailyPanel.activeSelf || settingsPanel.activeSelf || rewardPanel.activeSelf || devPanel.activeSelf ||
            shopPanel.activeSelf || TourOpen || starterPanel.activeSelf || homePanel.activeSelf;

        public bool HomeVisible => homePanel != null && homePanel.activeSelf;

        public void Build()
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            boosterButtons.Clear();
            boosterBadges.Clear();
            EnsureEventSystem();

            canvasRoot = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var safe = MakeRect("Safe", canvasRoot.transform);
            safe.gameObject.AddComponent<SafeArea>();
            Stretch(safe);
            safeRect = safe;

            BuildHome(safe);        // under everything else, so the top bar stays on top of it
            BuildTopBar(safe);
            BuildBoosterBar(safe);
            BuildTip(safe);
            BuildWinPanel(safe);
            BuildLosePanel(safe);
            BuildDailyPanel(safe);
            BuildShop(safe);
            BuildTour(safe);
            BuildSettings(safe);
            BuildStarterPanel(safe);
            BuildRewardPanel(safe);
            BuildDevPanel(safe);
            BuildToast(safe);
        }

        // ---------- Top bar and boosters ----------

        private void BuildTopBar(RectTransform safe)
        {
            var home = MakeCandy(safe, Blue, () => GoHome?.Invoke(), null, 0, "home", 78);
            Anchor(home.Root, new Vector2(0, 1), new Vector2(110, -100), new Vector2(140, 140));
            topLeft = home.Root.gameObject;

            var title = MakeRect("Title", safe);
            Anchor(title, new Vector2(0, 1), new Vector2(410, -100), new Vector2(360, 150));
            titleBox = title.gameObject;
            levelText = MakeText(title, "", 60, TextAnchor.MiddleLeft, Art.DisplayFont);
            Anchor(levelText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(360, 90));
            AddShadow(levelText, new Color(0, 0, 0, 0.35f), 5);
            trucksText = MakeText(title, "", 34, TextAnchor.MiddleLeft);
            trucksText.color = Muted;
            Anchor(trucksText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(360, 50));

            var gear = MakeCandy(safe, Quiet, OpenSettings, null, 0, "gear", 80);
            Anchor(gear.Root, new Vector2(1, 1), new Vector2(-110, -100), new Vector2(140, 140));

            coinPill = MakeRect("Coins", safe);
            Anchor(coinPill, new Vector2(1, 1), new Vector2(-355, -100), new Vector2(270, 96));
            var pillBg = Img(coinPill, Art.Rounded, new Color(0, 0, 0, 0.38f), 48);
            Stretch(pillBg.rectTransform);
            var coin = Coin(coinPill, 72);
            Anchor(coin, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(72, 72));
            coinsText = MakeText(coinPill, "0", 42, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(coinsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-4, 2), new Vector2(120, 90));
            var plus = MakeCandy(coinPill, Green, () => OpenShop?.Invoke(), null, 0, "plus", 42, 36);
            Anchor(plus.Root, new Vector2(1, 0.5f), new Vector2(-44, 4), new Vector2(72, 72));
        }

        private void BuildBoosterBar(RectTransform safe)
        {
            var bar = MakeRect("Boosters", safe);
            Anchor(bar, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(940, 260));
            boosterBar = bar.gameObject;
            var colors = new Dictionary<Booster, Color> { { Booster.Undo, Blue }, { Booster.Slot, Purple }, { Booster.Crane, Yellow }, { Booster.Magnet, Red }, { Booster.Shuffle, Green } };
            var icons = new Dictionary<Booster, string> { { Booster.Undo, "undo" }, { Booster.Slot, "slot" }, { Booster.Crane, "crane" }, { Booster.Magnet, "magnet" }, { Booster.Shuffle, "shuffle" } };
            for (int i = 0; i < Economy.Boosters.Length; i++)
            {
                var b = Economy.Boosters[i];
                var button = MakeCandy(bar, colors[b], () => BoosterTapped?.Invoke(b), null, 0, icons[b], 92, 54);
                Anchor(button.Root, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 186, 30), new Vector2(156, 146));
                boosterButtons[b] = button;
                var name = MakeText(bar, Loc.T(Economy.Key(b)), 32, TextAnchor.MiddleCenter, Art.DisplayFont);
                Anchor(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 186, -85), new Vector2(186, 50));
                AddShadow(name, new Color(0, 0, 0, 0.4f), 4);
                var badge = MakeRect("Badge", button.Root);
                Anchor(badge, new Vector2(1, 1), new Vector2(-6, -4), new Vector2(72, 72));
                boosterBadges[b] = badge;
            }

            craneTip = MakeRect("CraneTip", safe).gameObject;
            var tipRt = (RectTransform)craneTip.transform;
            Anchor(tipRt, new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(880, 90));
            Stretch(Img(tipRt, Art.Rounded, Gold, 40).rectTransform);
            craneTipText = MakeText(tipRt, Loc.T("crane_tip"), 38, TextAnchor.MiddleCenter);
            craneTipText.color = new Color32(0x3A, 0x24, 0x00, 0xFF);
            Stretch(craneTipText.rectTransform);
            craneTip.SetActive(false);
        }

        /// <summary>Badges: free/owned count, else the coin price, else an ad icon.</summary>
        public void SetBoosters(Func<Booster, int> count, int coins)
        {
            foreach (var b in Economy.Boosters)
            {
                var badge = boosterBadges[b];
                for (int i = badge.childCount - 1; i >= 0; i--) Destroy(badge.GetChild(i).gameObject);
                int n = count(b);
                if (n > 0)
                {
                    Pill(badge, Red, Economy.Number(n), new Vector2(72, 72), 40);
                }
                else if (coins >= Economy.Price(b))
                {
                    var pill = Pill(badge, new Color32(0x2A, 0x3A, 0x52, 0xFF), "", new Vector2(130, 64), 32);
                    var coin = Coin(pill, 38);
                    Anchor(coin, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(38, 38));
                    var t = MakeText(pill, Economy.Number(Economy.Price(b)), 32, TextAnchor.MiddleCenter, Art.DisplayFont);
                    Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(16, 2), new Vector2(90, 60));
                    ((RectTransform)pill).anchoredPosition = new Vector2(-30, 0);
                }
                else
                {
                    var pill = Pill(badge, Green, "", new Vector2(72, 72), 30);
                    var play = Icon(pill, "play", 36);
                    Anchor(play.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(3, 0), new Vector2(36, 36));
                }
                boosterButtons[b].SetColor(b == Booster.Crane && craneOn ? Gold : boosterButtons[b].BaseColor);
            }
        }

        public void SetCraneMode(bool on)
        {
            craneOn = on;
            craneTip.SetActive(on);
            boosterButtons[Booster.Crane].SetColor(on ? Gold : Yellow);
        }

        private bool hardLevel;

        public void SetLevel(int level, bool hard = false)
        {
            hardLevel = hard;
            levelText.text = level <= 0 ? Loc.T("challenge") : Loc.T("level", level);
            levelText.color = hard ? (Color)new Color32(0xFF, 0x6B, 0x5E, 0xFF) : Color.white;
            levelText.fontSize = 54;
        }

        public void SetTrucks(int shipped, int total) =>
            trucksText.text = (hardLevel ? Loc.T("play_hard") + "  " : "") + Loc.T("trucks", shipped, total);

        public void SetCoins(int coins)
        {
            coinsText.text = Economy.Number(coins);
            if (shopCoins != null) shopCoins.text = Economy.Number(coins);
        }

        private void Update()
        {
            if (toast != null && toast.activeSelf && Time.unscaledTime > toastUntil) toast.SetActive(false);
            if (craneOn && boosterButtons.TryGetValue(Booster.Crane, out var crane))
                crane.Root.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 7f));
            UpdateHome();
            UpdateShopTimers();
        }

        // ---------- Win, lose, daily ----------

        private void BuildWinPanel(RectTransform safe)
        {
            winPanel = MakePopup(safe, new Vector2(900, 860), out var box);
            winTitle = Title(box, "", 330);
            var reward = MakeRect("Reward", box);
            Anchor(reward, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(600, 120));
            var coin = Coin(reward, 100);
            Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-110, 0), new Vector2(100, 100));
            winReward = MakeText(reward, "", 96, TextAnchor.MiddleLeft, Art.DisplayFont);
            winReward.color = Gold;
            AddOutline(winReward, new Color32(0x8A, 0x4A, 0x00, 0xFF), 4);
            Anchor(winReward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(130, 0), new Vector2(260, 120));

            winChips = MakeRect("Chips", box).gameObject;
            var chips = (RectTransform)winChips.transform;
            Anchor(chips, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(800, 80));
            winStreak = Chip(chips, "flame", new Color32(0xFF, 0xB2, 0x7A, 0xFF), new Vector2(-280, 0));
            winPiggy = Chip(chips, "piggy", new Color32(0xFF, 0xB3, 0xD1, 0xFF), new Vector2(0, 0));
            winStars = Chip(chips, "star", new Color32(0xFF, 0xE0, 0x66, 0xFF), new Vector2(280, 0));

            var x3 = MakeCandy(box, Green, () => ClaimWin?.Invoke(true), Loc.T("claim_x3"), 52, null, 0, 0, Loc.T("watch_ad"));
            Anchor(x3.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(700, 170));
            var claim = MakeCandy(box, Yellow, () => ClaimWin?.Invoke(false), Loc.T("claim"), 52);
            Anchor(claim.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(480, 150));
            winPanel.SetActive(false);
        }

        private Text Chip(RectTransform parent, string icon, Color color, Vector2 pos)
        {
            var chip = MakeRect("Chip", parent);
            Anchor(chip, new Vector2(0.5f, 0.5f), pos, new Vector2(270, 76));
            Stretch(Img(chip, Art.Rounded, new Color(1, 1, 1, 0.09f), 38).rectTransform);
            var ic = Icon(chip, icon, 46);
            ic.color = color;
            Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(46, 46));
            var t = MakeText(chip, "", 34, TextAnchor.MiddleCenter);
            t.color = color;
            Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(25, 0), new Vector2(260, 70));
            return t;
        }

        private Text winStars;

        /// <summary>streak 0 hides the chips (daily challenge). starsAdded: World Tour stars from this win.</summary>
        public void ShowWin(bool show, int level = 0, int reward = 0, int streak = 0, int piggyAdded = 0, int starsAdded = 0)
        {
            winPanel.SetActive(show);
            if (!show) return;
            winTitle.text = level > 0 ? Loc.T("win_title", level) : Loc.T("challenge_win");
            winReward.text = "+" + Economy.Number(reward);
            winChips.SetActive(streak > 0);
            winStreak.text = Loc.T("win_streak", streak);
            winPiggy.transform.parent.gameObject.SetActive(piggyAdded > 0);
            winPiggy.text = Loc.T("piggy_add", piggyAdded);
            winStars.transform.parent.gameObject.SetActive(starsAdded > 0);
            winStars.text = Loc.T("win_stars", starsAdded);
            // Center the chips that are shown.
            var shown = new System.Collections.Generic.List<RectTransform>();
            foreach (var t in new[] { winStreak, winPiggy, winStars })
                if (t.transform.parent.gameObject.activeSelf) shown.Add((RectTransform)t.transform.parent);
            for (int i = 0; i < shown.Count; i++) shown[i].anchoredPosition = new Vector2((i - (shown.Count - 1) / 2f) * 280, 0);
            PopIn(winPanel);
        }

        private void BuildLosePanel(RectTransform safe)
        {
            losePanel = MakePopup(safe, new Vector2(900, 860), out var box);
            loseTitle = Title(box, Loc.T("lose_title"), 330);
            var text = MakeText(box, Loc.T("lose_text"), 40, TextAnchor.MiddleCenter);
            loseText = text;
            text.color = Muted;
            Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(840, 60));
            loseWarn = MakeText(box, "", 38, TextAnchor.MiddleCenter);
            loseWarn.color = new Color32(0xFF, 0x8F, 0x80, 0xFF);
            Anchor(loseWarn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(840, 60));
            var revive = MakeCandy(box, Green, () => Revive?.Invoke(), Loc.T("revive"), 46, null, 0, 0, Loc.T("watch_ad"));
            reviveLabel = revive.Label;
            Anchor(revive.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(780, 170));
            var undo = MakeCandy(box, Blue, () => LoseUndo?.Invoke(), Loc.T("lose_undo"), 48);
            Anchor(undo.Root, new Vector2(0.5f, 0.5f), new Vector2(-195, -240), new Vector2(370, 150));
            var retry = MakeCandy(box, Quiet, () => Retry?.Invoke(), Loc.T("retry"), 48);
            Anchor(retry.Root, new Vector2(0.5f, 0.5f), new Vector2(195, -240), new Vector2(370, 150));
            losePanel.SetActive(false);
        }

        private Text loseTitle, loseText, reviveLabel;

        /// <summary>streakAtRisk: a win streak (2+) the player loses if they give up. exploded: a bomb went off (revive adds moves).</summary>
        public void ShowLose(bool show, int streakAtRisk = 0, bool exploded = false)
        {
            losePanel.SetActive(show);
            if (!show) return;
            loseTitle.text = Loc.T(exploded ? "bomb_title" : "lose_title");
            loseText.text = Loc.T(exploded ? "bomb_text" : "lose_text");
            reviveLabel.text = Loc.T(exploded ? "bomb_revive" : "revive");
            loseWarn.gameObject.SetActive(streakAtRisk >= 2);
            loseWarn.text = Loc.T("streak_warn", streakAtRisk);
            PopIn(losePanel);
        }

        private void BuildDailyPanel(RectTransform safe)
        {
            dailyPanel = MakePopup(safe, new Vector2(900, 820), out var box);
            dailyTitle = Title(box, "", 310);
            var reward = MakeRect("Reward", box);
            Anchor(reward, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(600, 120));
            var coin = Coin(reward, 100);
            Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-110, 0), new Vector2(100, 100));
            dailyReward = MakeText(reward, "", 96, TextAnchor.MiddleLeft, Art.DisplayFont);
            dailyReward.color = Gold;
            AddOutline(dailyReward, new Color32(0x8A, 0x4A, 0x00, 0xFF), 4);
            Anchor(dailyReward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(130, 0), new Vector2(260, 120));
            var x2 = MakeCandy(box, Green, () => ClaimDaily?.Invoke(true), Loc.T("claim_x2"), 52, null, 0, 0, Loc.T("watch_ad"));
            Anchor(x2.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(700, 170));
            var claim = MakeCandy(box, Yellow, () => ClaimDaily?.Invoke(false), Loc.T("claim"), 52);
            Anchor(claim.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -205), new Vector2(480, 150));
            var hint = MakeText(box, Loc.T("daily_hint"), 32, TextAnchor.MiddleCenter);
            hint.color = Muted;
            Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -340), new Vector2(860, 60));
            dailyPanel.SetActive(false);
        }

        public bool DailyVisible => dailyPanel.activeSelf;

        public void ShowDaily(bool show, int day = 0, int reward = 0)
        {
            dailyPanel.SetActive(show);
            if (!show) return;
            dailyTitle.text = Loc.T("daily_title", day);
            dailyReward.text = "+" + Economy.Number(reward);
            dailyPanel.transform.SetAsLastSibling();
            PopIn(dailyPanel);
        }

        // ---------- Settings ----------

        private void BuildSettings(RectTransform safe)
        {
            settingsPanel = MakePopup(safe, new Vector2(900, 1040), out var box);
            CloseButton(box, () => settingsPanel.SetActive(false));
            Title(box, Loc.T("settings"), 420);
            soundSwitch = SettingRow(box, Loc.T("sound"), 270, () => ToggleSound?.Invoke());
            vibeSwitch = SettingRow(box, Loc.T("vibration"), 130, () => ToggleVibration?.Invoke());
            var langRow = Row(box, Loc.T("language"), -10);
            var lang = MakeCandy(langRow, Blue, () => ToggleLanguage?.Invoke(), Loc.T("other_language"), 38, null, 0, 30);
            Anchor(lang.Root, new Vector2(1, 0.5f), new Vector2(-130, 4), new Vector2(220, 96));

            gameRow = MakeRect("GameRow", box).gameObject;
            var gr = (RectTransform)gameRow.transform;
            Anchor(gr, new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(820, 150));
            var restart = MakeCandy(gr, Blue, () => { settingsPanel.SetActive(false); Restart?.Invoke(); }, Loc.T("restart"), 46);
            Anchor(restart.Root, new Vector2(0.5f, 0.5f), new Vector2(-207, 0), new Vector2(390, 140));
            var home = MakeCandy(gr, Purple, () => { settingsPanel.SetActive(false); GoHome?.Invoke(); }, Loc.T("home"), 46);
            Anchor(home.Root, new Vector2(0.5f, 0.5f), new Vector2(207, 0), new Vector2(390, 140));

            var restore = MakeCandy(box, Quiet, () => RestorePurchases?.Invoke(), Loc.T("restore"), 36, null, 0, 30);
            Anchor(restore.Root, new Vector2(0.5f, 0.5f), new Vector2(-207, -360), new Vector2(390, 110));
            restoreButton = restore.Root.gameObject;
            var privacy = MakeCandy(box, Quiet, () => Privacy?.Invoke(), Loc.T("privacy"), 36, null, 0, 30);
            Anchor(privacy.Root, new Vector2(0.5f, 0.5f), new Vector2(207, -360), new Vector2(390, 110));
            privacyButton = privacy.Root.gameObject;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dev = MakeCandy(box, Purple, () => { settingsPanel.SetActive(false); ShowDevPanel(); }, "DEV", 34, null, 0, 30);
            Anchor(dev.Root, new Vector2(0, 1), new Vector2(110, -70), new Vector2(150, 90));
#endif
            settingsPanel.SetActive(false);
        }

        private RectTransform Row(Transform box, string label, float y)
        {
            var row = MakeRect("Row", box);
            Anchor(row, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(820, 120));
            Stretch(Img(row, Art.Rounded, new Color(1, 1, 1, 0.07f), 40).rectTransform);
            var t = MakeText(row, label, 42, TextAnchor.MiddleLeft);
            Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(230, 0), new Vector2(400, 100));
            return row;
        }

        private Candy SettingRow(Transform box, string label, float y, Action onTap)
        {
            var row = Row(box, label, y);
            var sw = MakeCandy(row, Green, onTap, null, 0, null, 0, 44);
            Anchor(sw.Root, new Vector2(1, 0.5f), new Vector2(-110, 4), new Vector2(160, 88));
            var knob = Img(sw.Top, Art.Circle, Color.white, 0);
            knob.name = "Knob";
            Anchor(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36, 0), new Vector2(70, 70));
            return sw;
        }

        private void OpenSettings() => ShowSettings();

        public void ShowSettings()
        {
            RefreshSettings();
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            PopIn(settingsPanel);
        }

        public void RefreshSettings()
        {
            SetSwitch(soundSwitch, Progress.Sound);
            SetSwitch(vibeSwitch, Progress.Vibration);
            gameRow.SetActive(!HomeVisible);
            restoreButton.SetActive(MonetizationServices.PurchasesEnabled);
            privacyButton.SetActive(!string.IsNullOrEmpty(Economy.PrivacyUrl));
        }

        private static void SetSwitch(Candy sw, bool on)
        {
            sw.SetColor(on ? Green : (Color)new Color32(0x5F, 0x6E, 0x84, 0xFF));
            var knob = sw.Top.Find("Knob") as RectTransform;
            if (knob != null) knob.anchoredPosition = new Vector2(on ? 36 : -36, 0);
        }

        // ---------- Developer panel (editor and development builds only) ----------

        private GameObject devPanel;
        private Text devLevelText;
        private int devLevel = 15;

        private void BuildDevPanel(RectTransform safe)
        {
            devPanel = MakePopup(safe, new Vector2(900, 900), out var box);
            CloseButton(box, () => devPanel.SetActive(false));
            Title(box, Loc.T("dev_title"), 340);
            var note = MakeText(box, Loc.T("dev_note"), 30, TextAnchor.MiddleCenter);
            note.color = Muted;
            Anchor(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(840, 50));

            int[] steps = { -10, -1, 1, 10 };
            float[] xs = { -330, -190, 190, 330 };
            for (int i = 0; i < steps.Length; i++)
            {
                int step = steps[i];
                var b = MakeCandy(box, Quiet, () => SetDevLevel(devLevel + step), (step > 0 ? "+" : "") + step, 40, null, 0, 34);
                Anchor(b.Root, new Vector2(0.5f, 0.5f), new Vector2(xs[i], 130), new Vector2(124, 110));
            }
            devLevelText = MakeText(box, "", 70, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(devLevelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 136), new Vector2(220, 110));
            var go = MakeCandy(box, Purple, () => { devPanel.SetActive(false); DevGoToLevel?.Invoke(devLevel); }, "", 48);
            Anchor(go.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(620, 140));
            devGoLabel = go.Label;

            int[] shortcuts = { 40, 48, 56, 64 };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                int n = shortcuts[i];
                var b = MakeCandy(box, Blue, () => { devPanel.SetActive(false); DevGoToLevel?.Invoke(n); }, n.ToString(), 40, null, 0, 34);
                Anchor(b.Root, new Vector2(0.5f, 0.5f), new Vector2(-300 + i * 200, -190), new Vector2(170, 110));
            }
            var coins = MakeCandy(box, Yellow, () => DevAddCoins?.Invoke(), "+1000", 42, null, 0, 34);
            Anchor(coins.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(360, 110));
            devPanel.SetActive(false);
            SetDevLevel(devLevel);
        }

        private Text devGoLabel;

        private void SetDevLevel(int n)
        {
            devLevel = Mathf.Clamp(n, 1, 999);
            if (devLevelText != null) devLevelText.text = devLevel.ToString();
            if (devGoLabel != null) devGoLabel.text = Loc.T("dev_go", devLevel);
        }

        private void ShowDevPanel()
        {
            devPanel.SetActive(true);
            devPanel.transform.SetAsLastSibling();
            PopIn(devPanel);
        }

        // ---------- Reward popup ----------

        private void BuildRewardPanel(RectTransform safe)
        {
            rewardPanel = MakePopup(safe, new Vector2(900, 720), out var box);
            rewardTitle = Title(box, "", 260);
            rewardItems = MakeRect("Items", box);
            Anchor(rewardItems, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(860, 220));
            var claim = MakeCandy(box, Green, () => { rewardPanel.SetActive(false); RewardClaimed?.Invoke(); }, Loc.T("claim"), 52);
            Anchor(claim.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(520, 160));
            rewardPanel.SetActive(false);
        }

        /// <summary>Shows what the player got; RewardClaimed fires when they tap Claim.</summary>
        public void ShowReward(string titleKey, IList<Item> items)
        {
            rewardTitle.text = Loc.T(titleKey);
            ItemRow(rewardItems, items, 170);
            rewardPanel.SetActive(true);
            rewardPanel.transform.SetAsLastSibling();
            PopIn(rewardPanel);
        }

        /// <summary>A centered row of item tiles (coins, boosters, no ads).</summary>
        private void ItemRow(RectTransform row, IList<Item> items, float tile)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            float gap = 16, width = items.Count * tile + (items.Count - 1) * gap;
            for (int i = 0; i < items.Count; i++)
            {
                var t = ItemTile(row, items[i], tile);
                Anchor(t, new Vector2(0.5f, 0.5f), new Vector2(-width / 2 + tile / 2 + i * (tile + gap), 0), new Vector2(tile, tile * 1.18f));
            }
        }

        private RectTransform ItemTile(Transform parent, Item item, float size)
        {
            var tile = MakeRect("Item", parent);
            Stretch(Img(tile, Art.Rounded, new Color(0, 0, 0, 0.22f), 34).rectTransform);
            float art = size * 0.5f;
            if (item.Kind == ItemKind.Coins)
            {
                var coin = Coin(tile, art);
                Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(0, size * 0.14f), new Vector2(art, art));
            }
            else
            {
                var bg = Img(tile, Art.Rounded, item.Kind == ItemKind.NoAds ? Red : BoosterColor(item.AsBooster), 26);
                Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, size * 0.14f), new Vector2(art, art));
                var ic = Icon(bg.rectTransform, item.Kind == ItemKind.NoAds ? "noads" : BoosterIcon(item.AsBooster), art * 0.7f);
                Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(art * 0.7f, art * 0.7f));
            }
            string label = item.Kind == ItemKind.Coins ? Economy.Number(item.Amount) : item.Kind == ItemKind.NoAds ? Loc.T("noads_item") : "×" + Economy.Number(item.Amount);
            var t = MakeText(tile, label, Mathf.RoundToInt(size * (item.Kind == ItemKind.NoAds ? 0.16f : 0.22f)), TextAnchor.MiddleCenter, item.Kind == ItemKind.NoAds ? Art.UIFont : Art.DisplayFont);
            Anchor(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0, size * 0.2f), new Vector2(size, size * 0.3f));
            return tile;
        }

        private static Color BoosterColor(Booster b) => b == Booster.Undo ? Blue : b == Booster.Slot ? Purple : b == Booster.Crane ? Yellow : b == Booster.Magnet ? Red : Green;
        private static string BoosterIcon(Booster b) => b == Booster.Slot ? "slot" : Economy.Key(b);

        // ---------- Tip, toast, coins ----------

        private void BuildTip(RectTransform safe)
        {
            tip = MakeRect("Tip", safe).gameObject;
            var img = Img((RectTransform)tip.transform, Art.Rounded, new Color32(0xFF, 0xF4, 0xD6, 0xF2), 36);
            img.raycastTarget = false;
            Anchor((RectTransform)tip.transform, new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(940, 110));
            Stretch(img.rectTransform);
            tipText = MakeText(tip.transform, "", 32, TextAnchor.MiddleCenter);
            tipText.color = new Color32(0x1B, 0x1B, 0x1B, 0xFF);
            Stretch(tipText.rectTransform);
            tip.SetActive(false);
        }

        private void BuildToast(RectTransform safe)
        {
            toast = MakeRect("Toast", safe).gameObject;
            var img = Img((RectTransform)toast.transform, Art.Rounded, new Color32(0x1B, 0x2B, 0x40, 0xF4), 40);
            img.raycastTarget = false;
            Anchor((RectTransform)toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(880, 140));
            Stretch(img.rectTransform);
            toastText = MakeText(toast.transform, "", 40, TextAnchor.MiddleCenter);
            toastText.raycastTarget = false;
            Stretch(toastText.rectTransform);
            toast.SetActive(false);
        }

        /// <summary>Shows a tutorial tip under the top bar, or hides it when text is null.</summary>
        public void SetTip(string text)
        {
            tip.SetActive(!string.IsNullOrEmpty(text));
            if (tip.activeSelf) tipText.text = text;
        }

        public void Toast(string message, float seconds = 1.6f)
        {
            toastText.text = message;
            toast.SetActive(true);
            toast.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Gold coins fly from the middle of the screen to the coin counter.</summary>
        public void FlyCoins(int amount)
        {
            if (amount <= 0) return;
            StartCoroutine(FlyCoinsRoutine(Mathf.Clamp(amount / 10, 4, 12)));
        }

        private IEnumerator FlyCoinsRoutine(int count)
        {
            var target = coinsText.rectTransform;
            var coins = new List<RectTransform>();
            for (int i = 0; i < count; i++)
            {
                var rt = Coin(safeRect, 64);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64, 64);
                rt.anchoredPosition = UnityEngine.Random.insideUnitCircle * 140f;
                coins.Add(rt);
            }
            var starts = coins.ConvertAll(c => c.position);
            for (float t = 0; t < 1f + count * 0.05f; t += Time.unscaledDeltaTime / 0.6f)
            {
                if (target == null) yield break;
                for (int i = 0; i < coins.Count; i++)
                {
                    if (coins[i] == null) continue;
                    float k = Mathf.Clamp01(t - i * 0.05f);
                    k = k * k * (3f - 2f * k);
                    coins[i].position = Vector3.Lerp(starts[i], target.position, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 120f;
                    if (k >= 1f)
                    {
                        Destroy(coins[i].gameObject);
                        coins[i] = null;
                        Feedback.Click();
                        target.localScale = Vector3.one * 1.3f;
                    }
                }
                target.localScale = Vector3.Lerp(target.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
                yield return null;
            }
            foreach (var c in coins) if (c != null) Destroy(c.gameObject);
            if (target != null) target.localScale = Vector3.one;
        }

        /// <summary>Shows or hides the in-game parts (home button, level title, boosters).</summary>
        private void SetGameChromeVisible(bool visible)
        {
            topLeft.SetActive(visible);
            titleBox.SetActive(visible);
            boosterBar.SetActive(visible);
            if (!visible) craneTip.SetActive(false);
            else craneTip.SetActive(craneOn);
        }

        // ---------- Candy buttons and other UI helpers ----------

        /// <summary>A glossy button: darker bottom edge, colored face with a shine, and a top part that presses down.</summary>
        public sealed class Candy
        {
            public RectTransform Root, Top;
            public Image Edge, Face;
            public Text Label;
            public Outline LabelOutline;
            public Color BaseColor;

            public void SetColor(Color face)
            {
                Face.color = face;
                Edge.color = Art.Darken(face, 0.42f);
                if (LabelOutline != null) LabelOutline.effectColor = Art.Darken(face, 0.5f);
            }
        }

        /// <summary>Moves the top of a candy button down while it is held.</summary>
        private sealed class PressDown : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public RectTransform Top;
            public float Depth = 10f;

            public void OnPointerDown(PointerEventData e) => Set(-Depth);
            public void OnPointerUp(PointerEventData e) => Set(0);
            public void OnPointerExit(PointerEventData e) => Set(0);

            private void Set(float y)
            {
                if (Top == null) return;
                Top.offsetMin = new Vector2(0, y);
                Top.offsetMax = new Vector2(0, y);
            }
        }

        private Candy MakeCandy(Transform parent, Color color, Action onClick, string label, int fontSize,
            string icon = null, float iconSize = 0, float radius = 46, string subLabel = null)
        {
            var root = MakeRect("Button", parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var c = new Candy { Root = root, BaseColor = color };
            float edge = Mathf.Clamp(radius * 0.22f, 6, 12);
            c.Edge = Img(root, Art.Rounded, Color.white, radius);
            c.Edge.raycastTarget = false;
            Stretch(c.Edge.rectTransform);
            c.Edge.rectTransform.offsetMin = new Vector2(0, -edge);
            c.Edge.rectTransform.offsetMax = new Vector2(0, -edge);

            c.Top = MakeRect("Top", root);
            Stretch(c.Top);
            c.Face = Img(c.Top, Art.Rounded, Color.white, radius);
            c.Face.raycastTarget = false;
            Stretch(c.Face.rectTransform);
            var shine = Img(c.Top, Art.Rounded, new Color(1, 1, 1, 0.32f), radius * 0.8f);
            shine.raycastTarget = false;
            shine.rectTransform.anchorMin = new Vector2(0, 0.52f);
            shine.rectTransform.anchorMax = new Vector2(1, 1);
            shine.rectTransform.offsetMin = new Vector2(radius * 0.25f + 4, 0);
            shine.rectTransform.offsetMax = new Vector2(-radius * 0.25f - 4, -5);

            if (!string.IsNullOrEmpty(icon))
            {
                var ic = Icon(c.Top, icon, iconSize);
                Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
                var sh = ic.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.25f);
                sh.effectDistance = new Vector2(0, -4);
            }
            if (!string.IsNullOrEmpty(label))
            {
                c.Label = MakeText(c.Top, label, fontSize, TextAnchor.MiddleCenter, Art.DisplayFont);
                Stretch(c.Label.rectTransform);
                if (!string.IsNullOrEmpty(subLabel)) c.Label.rectTransform.offsetMin = new Vector2(0, fontSize * 0.55f);
                c.LabelOutline = AddOutline(c.Label, Color.black, 3);
            }
            if (!string.IsNullOrEmpty(subLabel))
            {
                var sub = MakeText(c.Top, subLabel, Mathf.RoundToInt(fontSize * 0.5f), TextAnchor.MiddleCenter);
                Anchor(sub.rectTransform, new Vector2(0.5f, 0), new Vector2(0, fontSize * 0.62f), new Vector2(600, fontSize * 0.7f));
                sub.color = new Color(1, 1, 1, 0.92f);
            }
            c.SetColor(color);

            var press = root.gameObject.AddComponent<PressDown>();
            press.Top = c.Top;
            press.Depth = edge * 0.85f;
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Feedback.Click();
                onClick?.Invoke();
            });
            return c;
        }

        /// <summary>A rounded popup card on a dark backdrop.</summary>
        private GameObject MakePopup(Transform parent, Vector2 size, out RectTransform box)
        {
            var dim = MakeRect("Popup", parent);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.68f);
            var frame = MakeRect("Frame", dim);
            Anchor(frame, new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(16, 16));
            Stretch(Img(frame, Art.Rounded, new Color(1, 1, 1, 0.12f), 70).rectTransform);
            var shadow = Img(frame, Art.Rounded, new Color(0, 0, 0, 0.35f), 64);
            Anchor(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -26), size);
            shadow.transform.SetAsFirstSibling();
            box = MakeRect("Box", frame);
            Anchor(box, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch(Img(box, Art.Rounded, CardColor, 64).rectTransform);
            var top = Img(box, Art.Rounded, new Color(1, 1, 1, 0.05f), 64);
            top.rectTransform.anchorMin = new Vector2(0, 0.55f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            return dim.gameObject;
        }

        private Text Title(RectTransform box, string text, float y)
        {
            var t = MakeText(box, text, 70, TextAnchor.MiddleCenter, Art.DisplayFont);
            Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(860, 110));
            AddShadow(t, new Color(0, 0, 0, 0.35f), 6);
            return t;
        }

        private void CloseButton(RectTransform box, Action onClick)
        {
            var x = MakeCandy(box, Red, onClick, null, 0, "x", 52, 56);
            Anchor(x.Root, new Vector2(1, 1), new Vector2(-10, -14), new Vector2(116, 116));
        }

        private void PopIn(GameObject panel)
        {
            var box = panel.transform.childCount > 0 ? panel.transform.GetChild(0) : null;
            if (box != null && isActiveAndEnabled) StartCoroutine(PopRoutine(box));
        }

        private static IEnumerator PopRoutine(Transform t)
        {
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.28f)
            {
                if (t == null) yield break;
                float x = k - 1f;
                float s = 1f + 2.4f * x * x * x + 1.4f * x * x; // back-out: 0 -> overshoot -> 1
                t.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, s);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private Text MakeText(Transform parent, string text, int size, TextAnchor align, Font font = null)
        {
            var rt = MakeRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : Art.UIFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Outline AddOutline(Text t, Color color, float size)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(size, -size);
            return o;
        }

        private static void AddShadow(Text t, Color color, float size)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = new Vector2(0, -size);
        }

        /// <summary>A rounded image whose corners are about radius UI pixels (sliced sprite).</summary>
        private static Image Img(RectTransform parent, Sprite sprite, Color color, float radius)
        {
            var rt = MakeRect("Image", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == Art.Rounded && radius > 0)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 25f / radius; // the sprite has 16 px corners at 64 px per unit
            }
            return img;
        }

        private static Image Icon(RectTransform parent, string name, float size)
        {
            var img = Img(parent, Art.Icon(name), Color.white, 0);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>A gold coin: rim, face and a little shine.</summary>
        private static RectTransform Coin(RectTransform parent, float size)
        {
            var rim = Img(parent, Art.Circle, new Color32(0xC9, 0x8A, 0x00, 0xFF), 0);
            var rt = rim.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            var face = Img(rt, Art.Circle, new Color32(0xFF, 0xCC, 0x33, 0xFF), 0);
            face.rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
            face.rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
            face.rectTransform.offsetMin = face.rectTransform.offsetMax = Vector2.zero;
            var inner = Img(rt, Art.Circle, new Color32(0xFF, 0xDD, 0x66, 0xFF), 0);
            inner.rectTransform.anchorMin = new Vector2(0.24f, 0.24f);
            inner.rectTransform.anchorMax = new Vector2(0.76f, 0.76f);
            inner.rectTransform.offsetMin = inner.rectTransform.offsetMax = Vector2.zero;
            var shine = Img(rt, Art.Circle, new Color(1, 1, 1, 0.7f), 0);
            shine.rectTransform.anchorMin = new Vector2(0.26f, 0.56f);
            shine.rectTransform.anchorMax = new Vector2(0.42f, 0.72f);
            shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>A small rounded label (badges, tags).</summary>
        private RectTransform Pill(RectTransform parent, Color color, string text, Vector2 size, int fontSize)
        {
            var white = Img(parent, Art.Rounded, Color.white, size.y / 2);
            Anchor(white.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(10, 10));
            var bg = Img(white.rectTransform, Art.Rounded, color, size.y / 2);
            Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            if (!string.IsNullOrEmpty(text))
            {
                var t = MakeText(bg.rectTransform, text, fontSize, TextAnchor.MiddleCenter, Art.DisplayFont);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(0, -2);
            }
            return white.rectTransform;
        }

        /// <summary>Draws a list of world-unit shapes as UI images (skin previews in the shop).</summary>
        private static void DrawShapes(RectTransform parent, List<Shape> shapes, float pixelsPerUnit, Vector2 offset)
        {
            // UI draws in child order, so add the parts by sorting order (keeping list order for ties).
            var order = new List<int>();
            for (int i = 0; i < shapes.Count; i++) order.Add(i);
            order.Sort((a, b) => shapes[a].Order != shapes[b].Order ? shapes[a].Order.CompareTo(shapes[b].Order) : a.CompareTo(b));
            foreach (int i in order)
            {
                var s = shapes[i];
                Vector2 size = s.Size * pixelsPerUnit;
                Image img;
                if (s.Round) img = Img(parent, Art.Circle, s.Color, 0);
                else if (Mathf.Min(s.Size.x, s.Size.y) >= 0.5f) img = Img(parent, Art.Rounded, s.Color, 0.25f * pixelsPerUnit);
                else img = Img(parent, Art.Rounded, s.Color, 0);
                Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), offset + s.Pos * pixelsPerUnit, size);
                if (s.Angle != 0f) img.rectTransform.localRotation = Quaternion.Euler(0, 0, s.Angle);
            }
        }
    }

    /// <summary>Keeps UI inside the phone's safe area (notches, rounded corners).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect applied;

        private void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
