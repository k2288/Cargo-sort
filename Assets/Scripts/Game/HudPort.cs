using System;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>The port-building screen: a little harbor scene that fills in as the player builds its pieces.</summary>
    public sealed partial class Hud
    {
        public event Action OpenPort;
        public event Action<int> BuildPortItem;

        private RectTransform portButton, portScene;
        private Text portLabel, portTitle, portProgress, portCoins;
        private GameObject portPanel;
        private readonly RectTransform[] portItems = new RectTransform[Port.Items];
        private readonly Text[] portRowButtons = new Text[Port.Items];
        private bool portHighlighted;

        private static readonly Color Ghost = new Color(1, 1, 1, 0.22f);

        private void BuildPortButton(RectTransform safe)
        {
            portButton = MakeButton(safe, Loc.T("port"), ButtonColor, () => OpenPort?.Invoke(), 32);
            portLabel = portButton.GetComponentInChildren<Text>();
            Anchor(portButton, new Vector2(1, 1), new Vector2(-130, -245), new Vector2(200, 90));
        }

        private void BuildPortPanel(RectTransform safe)
        {
            portPanel = MakePanel(safe, new Vector2(960, 1700), out var box);
            portTitle = MakeText(box, "", 62, TextAnchor.MiddleCenter);
            Anchor(portTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -75), new Vector2(900, 100));
            portProgress = MakeText(box, "", 38, TextAnchor.MiddleCenter);
            portProgress.color = new Color(1, 1, 1, 0.8f);
            Anchor(portProgress.rectTransform, new Vector2(0.5f, 1), new Vector2(-220, -150), new Vector2(420, 60));
            portCoins = MakeText(box, "", 38, TextAnchor.MiddleCenter);
            portCoins.color = Gold;
            Anchor(portCoins.rectTransform, new Vector2(0.5f, 1), new Vector2(220, -150), new Vector2(420, 60));

            portScene = MakeRect("Scene", box);
            portScene.gameObject.AddComponent<RectMask2D>();
            Anchor(portScene, new Vector2(0.5f, 1), new Vector2(0, -460), new Vector2(880, 520));

            for (int i = 0; i < Port.Items; i++)
            {
                int item = i;
                float y = -790 - i * 118;
                var name = MakeText(box, Loc.T(Port.ItemKeys[i]), 40, TextAnchor.MiddleCenter);
                Anchor(name.rectTransform, new Vector2(0.5f, 1), new Vector2(-170, y), new Vector2(480, 100));
                var button = MakeButton(box, "", ButtonColor, () => BuildPortItem?.Invoke(item), 36);
                Anchor(button, new Vector2(0.5f, 1), new Vector2(270, y), new Vector2(320, 100));
                portRowButtons[i] = button.GetComponentInChildren<Text>();
            }

            Anchor(MakeButton(box, Loc.T("close"), ButtonColor, () => portPanel.SetActive(false), 44), new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(420, 130));
            portPanel.SetActive(false);
        }

        public void ShowPort(bool show)
        {
            portPanel.SetActive(show);
            if (!show) return;
            portPanel.transform.SetAsLastSibling();
            RefreshPort();
        }

        /// <summary>Redraws the port scene and the build list for the current port and coins.</summary>
        public void RefreshPort()
        {
            UpdatePortButton(Progress.Coins);
            if (portPanel == null || !portPanel.activeSelf) return;
            portTitle.text = Port.Name;
            portProgress.text = Loc.T("port_progress", Port.BuiltCount, Port.Items);
            portCoins.text = Loc.T("coins", Progress.Coins);

            for (int i = portScene.childCount - 1; i >= 0; i--) Destroy(portScene.GetChild(i).gameObject);
            DrawPortBackground();
            for (int i = 0; i < Port.Items; i++) portItems[i] = DrawPortItem(i, Port.IsBuilt(i));

            for (int i = 0; i < Port.Items; i++)
            {
                var label = portRowButtons[i];
                var image = label.transform.parent.GetComponent<Image>();
                if (Port.IsBuilt(i))
                {
                    label.text = Loc.T("built");
                    image.color = ButtonQuiet;
                }
                else
                {
                    label.text = Loc.T("build_for", Port.Cost(i));
                    image.color = Progress.Coins >= Port.Cost(i) ? ButtonAdColor : ButtonQuiet;
                }
            }
        }

        /// <summary>Pops a freshly built piece into the scene.</summary>
        public void AnimatePortItem(int item)
        {
            if (item < 0 || item >= Port.Items || portItems[item] == null) return;
            StartCoroutine(PopRoutine(portItems[item]));
        }

        private System.Collections.IEnumerator PopRoutine(RectTransform rt)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                if (rt == null) yield break;
                // Overshoot curve: 0 -> 1.18 -> 1
                float s = t < 0.6f ? Mathf.SmoothStep(0f, 1.18f, t / 0.6f) : Mathf.Lerp(1.18f, 1f, (t - 0.6f) / 0.4f);
                rt.localScale = new Vector3(s, s, 1);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private void UpdatePortButton(int coins)
        {
            if (portLabel == null) return;
            portHighlighted = Port.CanBuildSomething(coins);
            portLabel.text = Loc.T(portHighlighted ? "port_ready" : "port");
            portButton.GetComponent<Image>().color = portHighlighted ? ButtonAdColor : ButtonColor;
            if (!portHighlighted) portButton.localScale = Vector3.one;
        }

        private void PulsePortButton()
        {
            if (portHighlighted && portButton != null)
                portButton.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f));
        }

        // ---------- Drawing (scene coordinates: x -440..440, y -260..260) ----------

        private void DrawPortBackground()
        {
            var bg = MakeRect("Sky", portScene);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Port.Sky;
            Part(portScene, new Vector2(300, 170), new Vector2(80, 80), new Color32(0xFF, 0xF3, 0xB0, 0xFF), Art.Circle);
            Part(portScene, new Vector2(0, -200), new Vector2(880, 120), Port.Water, null);
            Part(portScene, new Vector2(-155, -175), new Vector2(570, 170), new Color32(0xC9, 0xB2, 0x7C, 0xFF), null);
            Part(portScene, new Vector2(-155, -95), new Vector2(570, 12), new Color32(0x9C, 0x86, 0x58, 0xFF), null);
        }

        private RectTransform DrawPortItem(int item, bool built)
        {
            Vector2 basePoint;
            switch (item)
            {
                case 0: basePoint = new Vector2(190, -95); break;
                case 1: basePoint = new Vector2(-300, -90); break;
                case 2: basePoint = new Vector2(60, -90); break;
                case 3: basePoint = new Vector2(-170, -200); break;
                case 4: basePoint = new Vector2(-120, -90); break;
                default: basePoint = new Vector2(350, -170); break;
            }
            var root = MakeRect("Item" + item, portScene);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = basePoint;

            Color C(byte r, byte g, byte b) => built ? (Color)new Color32(r, g, b, 0xFF) : Ghost;
            switch (item)
            {
                case 0: // Pier
                    for (int p = -1; p <= 1; p++) Part(root, new Vector2(p * 60, -35), new Vector2(12, 60), C(0x5E, 0x3B, 0x1A), null);
                    Part(root, new Vector2(0, 0), new Vector2(170, 18), C(0x8B, 0x5A, 0x2B), Art.Rounded);
                    break;
                case 1: // Warehouse
                    Part(root, new Vector2(0, 65), new Vector2(200, 130), C(0xB5, 0x53, 0x3C), null);
                    Part(root, new Vector2(0, 140), new Vector2(224, 26), C(0x6B, 0x2E, 0x22), Art.Rounded);
                    Part(root, new Vector2(0, 35), new Vector2(72, 70), C(0x4A, 0x2A, 0x20), null);
                    Part(root, new Vector2(-65, 90), new Vector2(34, 26), built ? Art.Window : Ghost, null);
                    Part(root, new Vector2(65, 90), new Vector2(34, 26), built ? Art.Window : Ghost, null);
                    break;
                case 2: // Crane
                    Part(root, new Vector2(0, 130), new Vector2(26, 260), C(0xFF, 0xC2, 0x3A), null);
                    Part(root, new Vector2(110, 250), new Vector2(260, 20), C(0xFF, 0xC2, 0x3A), null);
                    Part(root, new Vector2(-30, 238), new Vector2(40, 36), C(0x55, 0x55, 0x55), null);
                    Part(root, new Vector2(200, 175), new Vector2(4, 130), C(0x33, 0x33, 0x33), null);
                    Part(root, new Vector2(200, 92), new Vector2(46, 46), built ? Art.CrateColor(0) : Ghost, Art.Rounded);
                    break;
                case 3: // Truck
                    Part(root, new Vector2(-30, 30), new Vector2(120, 55), C(0x34, 0x98, 0xDB), Art.Rounded);
                    Part(root, new Vector2(55, 22), new Vector2(46, 42), C(0xF1, 0xC4, 0x0F), Art.Rounded);
                    Part(root, new Vector2(62, 30), new Vector2(22, 16), built ? Art.Window : Ghost, null);
                    foreach (float x in new[] { -60f, -10f, 55f })
                        Part(root, new Vector2(x, -5), new Vector2(26, 26), built ? Art.Wheel : Ghost, Art.Circle);
                    break;
                case 4: // Lighthouse
                    Part(root, new Vector2(0, 115), new Vector2(46, 230), C(0xF5, 0xF5, 0xF5), null);
                    Part(root, new Vector2(0, 60), new Vector2(46, 26), C(0xE7, 0x4C, 0x3C), null);
                    Part(root, new Vector2(0, 150), new Vector2(46, 26), C(0xE7, 0x4C, 0x3C), null);
                    Part(root, new Vector2(0, 243), new Vector2(56, 30), C(0x33, 0x33, 0x33), null);
                    Part(root, new Vector2(0, 243), new Vector2(26, 26), C(0xFF, 0xE0, 0x66), Art.Circle);
                    Part(root, new Vector2(0, 266), new Vector2(66, 16), C(0xE7, 0x4C, 0x3C), Art.Rounded);
                    break;
                default: // Cargo ship
                    Part(root, new Vector2(-55, 45), new Vector2(50, 40), C(0xF5, 0xF5, 0xF5), null);
                    Part(root, new Vector2(10, 42), new Vector2(40, 30), built ? Art.CrateColor(1) : Ghost, null);
                    Part(root, new Vector2(55, 42), new Vector2(40, 30), built ? Art.CrateColor(2) : Ghost, null);
                    Part(root, new Vector2(32, 72), new Vector2(40, 30), built ? Art.CrateColor(3) : Ghost, null);
                    Part(root, new Vector2(0, 0), new Vector2(190, 56), C(0xC0, 0x39, 0x2B), Art.Rounded);
                    break;
            }
            return root;
        }

        private static void Part(Transform parent, Vector2 center, Vector2 size, Color color, Sprite sprite)
        {
            var rt = MakeRect("Part", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite == null) return;
            img.sprite = sprite;
            if (sprite == Art.Rounded)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2f;
            }
        }
    }
}
