using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>All screen UI, built in code: top bar, booster buttons, win and lose panels.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public event Action Undo, AddSlot, Hint, Restart, Next, Revive, Retry, RemoveAds;

        private Font font;
        private Text levelText, trucksText, undoLabel, toastText;
        private GameObject winPanel, losePanel, removeAdsButton, toast;
        private Text winTitle;
        private float toastUntil;

        private static readonly Color ButtonColor = new Color32(0xFF, 0xB7, 0x2B, 0xFF);
        private static readonly Color ButtonAdColor = new Color32(0x5B, 0xD1, 0x6A, 0xFF);
        private static readonly Color PanelColor = new Color32(0x15, 0x22, 0x35, 0xEE);

        public void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var safe = MakeRect("Safe", canvasGo.transform);
            safe.gameObject.AddComponent<SafeArea>();
            Stretch(safe);

            // Top bar
            levelText = MakeText(safe, "Level 1", 64, TextAnchor.UpperCenter);
            Anchor(levelText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(600, 90));
            trucksText = MakeText(safe, "", 38, TextAnchor.UpperCenter);
            trucksText.color = new Color(1, 1, 1, 0.75f);
            Anchor(trucksText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(600, 60));

            var restart = MakeButton(safe, "Reset", ButtonColor, () => Restart?.Invoke(), 34);
            Anchor(restart, new Vector2(0, 1), new Vector2(130, -90), new Vector2(200, 100));
            removeAdsButton = MakeButton(safe, "No Ads", ButtonColor, () => RemoveAds?.Invoke(), 34).gameObject;
            Anchor((RectTransform)removeAdsButton.transform, new Vector2(1, 1), new Vector2(-130, -90), new Vector2(200, 100));

            // Boosters
            var undo = MakeButton(safe, "Undo", ButtonColor, () => Undo?.Invoke(), 40);
            undoLabel = undo.GetComponentInChildren<Text>();
            Anchor(undo, new Vector2(0.5f, 0), new Vector2(-330, 110), new Vector2(280, 130));
            var slot = MakeButton(safe, "+Slot (AD)", ButtonAdColor, () => AddSlot?.Invoke(), 40);
            Anchor(slot, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(280, 130));
            var hint = MakeButton(safe, "Hint (AD)", ButtonAdColor, () => Hint?.Invoke(), 40);
            Anchor(hint, new Vector2(0.5f, 0), new Vector2(330, 110), new Vector2(280, 130));

            // Win panel
            winPanel = MakePanel(safe, out var winBox);
            winTitle = MakeText(winBox, "Level complete!", 72, TextAnchor.MiddleCenter);
            Anchor(winTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 120));
            Anchor(MakeButton(winBox, "Next", ButtonColor, () => Next?.Invoke(), 56), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(420, 150));

            // Lose panel
            losePanel = MakePanel(safe, out var loseBox);
            var loseTitle = MakeText(loseBox, "Dock is full!", 72, TextAnchor.MiddleCenter);
            Anchor(loseTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(800, 120));
            Anchor(MakeButton(loseBox, "+1 Slot & Continue (AD)", ButtonAdColor, () => Revive?.Invoke(), 44), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(620, 140));
            Anchor(MakeButton(loseBox, "Retry", ButtonColor, () => Retry?.Invoke(), 44), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(420, 130));

            // Toast
            toast = MakeRect("Toast", safe).gameObject;
            toast.AddComponent<Image>().color = PanelColor;
            Anchor((RectTransform)toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(760, 130));
            toastText = MakeText(toast.transform, "", 42, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform);
            toast.GetComponent<Image>().raycastTarget = false;
            toastText.raycastTarget = false;
            toast.SetActive(false);

            ShowWin(false, 0);
            ShowLose(false);
        }

        private void Update()
        {
            if (toast != null && toast.activeSelf && Time.unscaledTime > toastUntil) toast.SetActive(false);
        }

        public void SetLevel(int level) => levelText.text = "Level " + level;
        public void SetTrucks(int shipped, int total) => trucksText.text = $"Trucks {shipped}/{total}";
        public void SetUndoLabel(string label) => undoLabel.text = label;
        public void SetRemoveAdsVisible(bool visible) => removeAdsButton.SetActive(visible);
        public void ShowLose(bool show) => losePanel.SetActive(show);
        public bool PanelOpen => winPanel.activeSelf || losePanel.activeSelf;

        public void ShowWin(bool show, int level)
        {
            winPanel.SetActive(show);
            if (show) winTitle.text = $"Level {level} complete!";
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
            t.fontStyle = FontStyle.Bold;
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

        private GameObject MakePanel(Transform parent, out Transform box)
        {
            var dim = MakeRect("Panel", parent);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            var b = MakeRect("Box", dim);
            b.gameObject.AddComponent<Image>().color = PanelColor;
            Anchor(b, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 620));
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
