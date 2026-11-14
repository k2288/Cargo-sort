using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>All screen UI, built in code: top bar, boosters, and the win, lose, daily gift and shop panels.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public event Action Undo, AddSlot, Hint, Restart, Revive, Retry, RemoveAds, OpenShop, FreeCoins, ToggleLanguage;
        public event Action<bool> ClaimWin;   // true = x3 with ad
        public event Action<bool> ClaimDaily; // true = x2 with ad
        public event Action<int> ThemeTapped;

        public const int FreeCoinsAmount = 50;

        private Font font;
        private GameObject canvasRoot;
        private Text levelText, trucksText, coinsText, undoLabel, toastText;
        private Text winTitle, winReward, dailyTitle, dailyReward, shopCoins;
        private GameObject winPanel, losePanel, dailyPanel, shopPanel, removeAdsButton, toast;
        private readonly List<Text> themeButtons = new List<Text>();
        private float toastUntil;

        private static readonly Color ButtonColor = new Color32(0xFF, 0xB7, 0x2B, 0xFF);
        private static readonly Color ButtonAdColor = new Color32(0x5B, 0xD1, 0x6A, 0xFF);
        private static readonly Color ButtonQuiet = new Color32(0x9F, 0xB3, 0xCC, 0xFF);
        private static readonly Color PanelColor = new Color32(0x15, 0x22, 0x35, 0xF2);
        private static readonly Color Gold = new Color32(0xFF, 0xCC, 0x33, 0xFF);

        public bool PanelOpen => winPanel.activeSelf || losePanel.activeSelf || dailyPanel.activeSelf || shopPanel.activeSelf;

        public void Build()
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            themeButtons.Clear();
            font = Resources.Load<Font>("Fonts/Vazirmatn-Bold");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            // Top bar
            levelText = MakeText(safe, "", 62, TextAnchor.UpperCenter);
            Anchor(levelText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(600, 90));
            trucksText = MakeText(safe, "", 36, TextAnchor.UpperCenter);
            trucksText.color = new Color(1, 1, 1, 0.75f);
            Anchor(trucksText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(600, 60));

            Anchor(MakeButton(safe, Loc.T("reset"), ButtonColor, () => Restart?.Invoke(), 34), new Vector2(0, 1), new Vector2(130, -90), new Vector2(200, 100));
            Anchor(MakeButton(safe, Loc.T("shop"), ButtonColor, () => OpenShop?.Invoke(), 34), new Vector2(1, 1), new Vector2(-130, -90), new Vector2(200, 100));
            coinsText = MakeText(safe, "", 34, TextAnchor.UpperCenter);
            coinsText.color = Gold;
            Anchor(coinsText.rectTransform, new Vector2(1, 1), new Vector2(-130, -165), new Vector2(260, 50));

            // Boosters
            var undo = MakeButton(safe, "", ButtonColor, () => Undo?.Invoke(), 38);
            undoLabel = undo.GetComponentInChildren<Text>();
            Anchor(undo, new Vector2(0.5f, 0), new Vector2(-330, 110), new Vector2(300, 130));
            Anchor(MakeButton(safe, Loc.T("slot"), ButtonAdColor, () => AddSlot?.Invoke(), 38), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(300, 130));
            Anchor(MakeButton(safe, Loc.T("hint"), ButtonAdColor, () => Hint?.Invoke(), 38), new Vector2(0.5f, 0), new Vector2(330, 110), new Vector2(300, 130));

            // Win panel: coins reward, x3 with a rewarded ad
            winPanel = MakePanel(safe, new Vector2(900, 700), out var winBox);
            winTitle = MakeText(winBox, "", 66, TextAnchor.MiddleCenter);
            Anchor(winTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(860, 110));
            winReward = MakeText(winBox, "", 58, TextAnchor.MiddleCenter);
            winReward.color = Gold;
            Anchor(winReward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(860, 90));
            Anchor(MakeButton(winBox, Loc.T("claim_x3"), ButtonAdColor, () => ClaimWin?.Invoke(true), 46), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(640, 140));
            Anchor(MakeButton(winBox, Loc.T("claim"), ButtonColor, () => ClaimWin?.Invoke(false), 46), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(420, 130));

            // Lose panel
            losePanel = MakePanel(safe, new Vector2(900, 680), out var loseBox);
            var loseTitle = MakeText(loseBox, Loc.T("lose_title"), 70, TextAnchor.MiddleCenter);
            Anchor(loseTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(860, 120));
            Anchor(MakeButton(loseBox, Loc.T("revive"), ButtonAdColor, () => Revive?.Invoke(), 42), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(760, 140));
            Anchor(MakeButton(loseBox, Loc.T("retry"), ButtonColor, () => Retry?.Invoke(), 44), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(420, 130));

            // Daily gift panel
            dailyPanel = MakePanel(safe, new Vector2(900, 760), out var dailyBox);
            dailyTitle = MakeText(dailyBox, "", 62, TextAnchor.MiddleCenter);
            Anchor(dailyTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(860, 100));
            dailyReward = MakeText(dailyBox, "", 64, TextAnchor.MiddleCenter);
            dailyReward.color = Gold;
            Anchor(dailyReward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(860, 100));
            Anchor(MakeButton(dailyBox, Loc.T("claim_x2"), ButtonAdColor, () => ClaimDaily?.Invoke(true), 46), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(640, 140));
            Anchor(MakeButton(dailyBox, Loc.T("claim"), ButtonColor, () => ClaimDaily?.Invoke(false), 46), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(420, 130));
            var dailyHint = MakeText(dailyBox, Loc.T("daily_hint"), 32, TextAnchor.MiddleCenter);
            dailyHint.color = new Color(1, 1, 1, 0.7f);
            Anchor(dailyHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -305), new Vector2(860, 60));

            BuildShop(safe);

            // Toast
            toast = MakeRect("Toast", safe).gameObject;
            var toastImg = toast.AddComponent<Image>();
            toastImg.color = PanelColor;
            toastImg.raycastTarget = false;
            Anchor((RectTransform)toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(820, 130));
            toastText = MakeText(toast.transform, "", 40, TextAnchor.MiddleCenter);
            toastText.raycastTarget = false;
            Stretch(toastText.rectTransform);
            toast.SetActive(false);

            winPanel.SetActive(false);
            losePanel.SetActive(false);
            dailyPanel.SetActive(false);
            shopPanel.SetActive(false);
        }

        private void BuildShop(RectTransform safe)
        {
            shopPanel = MakePanel(safe, new Vector2(960, 1560), out var box);
            var title = MakeText(box, Loc.T("shop_title"), 66, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(900, 100));
            shopCoins = MakeText(box, "", 44, TextAnchor.MiddleCenter);
            shopCoins.color = Gold;
            Anchor(shopCoins.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(900, 70));

            for (int i = 0; i < Themes.All.Length; i++)
            {
                int index = i;
                var theme = Themes.All[i];
                float y = -270 - i * 150;

                var swatch = MakeRect("Swatch", box);
                swatch.gameObject.AddComponent<Image>().color = theme.Background;
                Anchor(swatch, new Vector2(0.5f, 1), new Vector2(-370, y), new Vector2(110, 110));
                var inner = MakeRect("Ground", swatch);
                inner.gameObject.AddComponent<Image>().color = theme.Ground;
                Anchor(inner, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(80, 40));

                var name = MakeText(box, Loc.T(theme.NameKey), 42, TextAnchor.MiddleCenter);
                Anchor(name.rectTransform, new Vector2(0.5f, 1), new Vector2(-90, y), new Vector2(400, 100));

                var button = MakeButton(box, "", ButtonColor, () => ThemeTapped?.Invoke(index), 36);
                Anchor(button, new Vector2(0.5f, 1), new Vector2(290, y), new Vector2(280, 110));
                themeButtons.Add(button.GetComponentInChildren<Text>());
            }

            Anchor(MakeButton(box, Loc.T("free_coins", FreeCoinsAmount), ButtonAdColor, () => FreeCoins?.Invoke(), 42), new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(700, 130));
            removeAdsButton = MakeButton(box, Loc.T("remove_ads"), ButtonColor, () => RemoveAds?.Invoke(), 40).gameObject;
            Anchor((RectTransform)removeAdsButton.transform, new Vector2(0.5f, 0), new Vector2(-190, 310), new Vector2(400, 120));
            Anchor(MakeButton(box, Loc.T("other_language"), ButtonQuiet, () => ToggleLanguage?.Invoke(), 40), new Vector2(0.5f, 0), new Vector2(230, 310), new Vector2(320, 120));
            Anchor(MakeButton(box, Loc.T("close"), ButtonColor, () => shopPanel.SetActive(false), 44), new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(420, 130));
        }

        private void Update()
        {
            if (toast != null && toast.activeSelf && Time.unscaledTime > toastUntil) toast.SetActive(false);
        }

        public void SetLevel(int level) => levelText.text = Loc.T("level", level);
        public void SetTrucks(int shipped, int total) => trucksText.text = Loc.T("trucks", shipped, total);
        public void SetUndoLabel(int freeUndos) => undoLabel.text = freeUndos > 0 ? Loc.T("undo", freeUndos) : Loc.T("undo_ad");
        public void SetRemoveAdsVisible(bool visible) => removeAdsButton.SetActive(visible);
        public void ShowLose(bool show) => losePanel.SetActive(show);

        public void SetCoins(int coins)
        {
            coinsText.text = Loc.T("coins", coins);
            shopCoins.text = Loc.T("coins", coins);
        }

        public void ShowWin(bool show, int level = 0, int reward = 0)
        {
            winPanel.SetActive(show);
            if (!show) return;
            winTitle.text = Loc.T("win_title", level);
            winReward.text = Loc.T("reward", reward);
        }

        public void ShowDaily(bool show, int day = 0, int reward = 0)
        {
            dailyPanel.SetActive(show);
            if (!show) return;
            dailyTitle.text = Loc.T("daily_title", day);
            dailyReward.text = Loc.T("reward", reward);
        }

        public void ShowShop(bool show)
        {
            shopPanel.SetActive(show);
            if (show) shopPanel.transform.SetAsLastSibling();
        }

        public void RefreshShop()
        {
            for (int i = 0; i < themeButtons.Count; i++)
            {
                var label = themeButtons[i];
                var image = label.transform.parent.GetComponent<Image>();
                if (i == Progress.Theme)
                {
                    label.text = Loc.T("selected");
                    image.color = ButtonQuiet;
                }
                else if (Progress.OwnsTheme(i))
                {
                    label.text = Loc.T("use");
                    image.color = ButtonColor;
                }
                else
                {
                    label.text = Loc.T("coins", Themes.All[i].Price);
                    image.color = Progress.Coins >= Themes.All[i].Price ? ButtonAdColor : ButtonQuiet;
                }
            }
        }

        public void Toast(string message, float seconds = 1.6f)
        {
            toastText.text = message;
            toast.SetActive(true);
            toast.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + seconds;
        }

        // ---------- UI helpers ----------

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

        private Text MakeText(Transform parent, string text, int size, TextAnchor align)
        {
            var rt = MakeRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private RectTransform MakeButton(Transform parent, string label, Color color, Action onClick, int fontSize = 60)
        {
            var rt = MakeRect("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Art.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var t = MakeText(rt, label, fontSize, TextAnchor.MiddleCenter);
            t.color = new Color32(0x1B, 0x1B, 0x1B, 0xFF);
            Stretch(t.rectTransform);
            return rt;
        }

        private GameObject MakePanel(Transform parent, Vector2 size, out Transform box)
        {
            var dim = MakeRect("Panel", parent);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var b = MakeRect("Box", dim);
            var img = b.gameObject.AddComponent<Image>();
            img.sprite = Art.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.3f;
            img.color = PanelColor;
            Anchor(b, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            box = b;
            return dim.gameObject;
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
