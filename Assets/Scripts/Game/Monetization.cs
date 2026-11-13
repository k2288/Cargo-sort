using System;
using System.Collections;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// What the game needs from an ad network. Swap <see cref="DebugAds"/> for a real one
    /// (Unity LevelPlay or Google AdMob) before release; nothing else has to change.
    /// </summary>
    public interface IAdsService
    {
        bool RewardedReady { get; }
        void ShowRewarded(string placement, Action<bool> onDone);
        void ShowInterstitial(string placement, Action onDone);
    }

    /// <summary>Stand-in ads for development: shows a 1 second "ad" overlay and always rewards.</summary>
    public sealed class DebugAds : MonoBehaviour, IAdsService
    {
        private bool showing;
        private string label;

        public bool RewardedReady => !showing;

        public void ShowRewarded(string placement, Action<bool> onDone) => StartCoroutine(Fake("REWARDED AD\n" + placement, () => onDone(true)));

        public void ShowInterstitial(string placement, Action onDone) => StartCoroutine(Fake("INTERSTITIAL AD\n" + placement, onDone));

        private IEnumerator Fake(string text, Action done)
        {
            showing = true;
            label = text;
            Debug.Log("[Ads] " + text.Replace('\n', ' '));
            yield return new WaitForSecondsRealtime(1f);
            showing = false;
            done?.Invoke();
        }

        private void OnGUI()
        {
            if (!showing) return;
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Screen.height / 30 };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), label + "\n(test)", style);
        }
    }

    /// <summary>
    /// Remove-ads purchase. This stub just unlocks locally; wire it to Unity IAP
    /// (product id "remove_ads", non-consumable) before release.
    /// </summary>
    public static class Store
    {
        public const string RemoveAdsProductId = "remove_ads";

        public static void BuyRemoveAds(Action<bool> onDone)
        {
            Debug.Log("[IAP] Purchasing " + RemoveAdsProductId + " (stub)");
            Progress.AdsRemoved = true;
            onDone?.Invoke(true);
        }
    }

    /// <summary>Saved player progress.</summary>
    public static class Progress
    {
        private const string LevelKey = "level";
        private const string AdsRemovedKey = "ads_removed";

        public static int Level
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            set { PlayerPrefs.SetInt(LevelKey, value); PlayerPrefs.Save(); }
        }

        public static bool AdsRemoved
        {
            get => PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;
            set { PlayerPrefs.SetInt(AdsRemovedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
