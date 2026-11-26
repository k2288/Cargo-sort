#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// Development builds only: shows purchase ([IAP]) messages, warnings, errors and exceptions on screen, so a failed
    /// Bazaar purchase can be diagnosed on the phone without a cable. Tap "Hide" to close, "Copy" to copy the text.
    /// </summary>
    public sealed class DevLog : MonoBehaviour
    {
        private const int MaxLines = 40;
        private static readonly List<string> lines = new List<string>();
        private static readonly object gate = new object();
        private static bool visible;
        private Vector2 scroll;
        private GUIStyle text, button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("DevLog");
            DontDestroyOnLoad(go);
            go.AddComponent<DevLog>();
            Application.logMessageReceivedThreaded += OnLog;
            Add($"Dev build {Application.version} ({Application.identifier}), Android API {SystemInfo.operatingSystem}");
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            bool iap = message.Contains("[IAP]") || message.Contains("Poolakey") || message.Contains("Bazaar");
            if (!iap && type == LogType.Log) return;
            string line = $"{System.DateTime.Now:HH:mm:ss} {type}: {message}";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack)) line += "\n" + stack.Split('\n')[0];
            Add(line);
            if (iap || type != LogType.Warning) visible = true;
        }

        private static void Add(string line)
        {
            lock (gate)
            {
                lines.Add(line);
                if (lines.Count > MaxLines) lines.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            float s = Screen.width / 1080f;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * s), wordWrap = true };
                text.normal.textColor = Color.white;
                button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(36 * s) };
            }
            if (!visible)
            {
                if (GUI.Button(new Rect(Screen.width - 170 * s, Screen.height - 110 * s, 150 * s, 80 * s), "Log", button)) visible = true;
                return;
            }
            string all;
            lock (gate) all = string.Join("\n", lines);
            var box = new Rect(20 * s, Screen.height * 0.35f, Screen.width - 40 * s, Screen.height * 0.6f);
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = (box.width - 60 * s) / 2;
            if (GUI.Button(new Rect(box.x + 20 * s, box.y + 20 * s, w, 90 * s), "Copy", button)) GUIUtility.systemCopyBuffer = all;
            if (GUI.Button(new Rect(box.x + 40 * s + w, box.y + 20 * s, w, 90 * s), "Hide", button)) visible = false;
            var view = new Rect(box.x + 20 * s, box.y + 130 * s, box.width - 40 * s, box.height - 150 * s);
            float h = text.CalcHeight(new GUIContent(all), view.width - 30 * s);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 30 * s, h));
            GUI.Label(new Rect(0, 0, view.width - 30 * s, h), all, text);
            GUI.EndScrollView();
        }
    }
}
#endif
