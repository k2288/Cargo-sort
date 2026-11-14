using System;
using System.Collections;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>
    /// What the game needs from an ad network. <see cref="DebugAds"/> is used in the editor and until
    /// LevelPlay is installed; Assets/Scripts/Monetization/LevelPlayAds.cs is the real one.
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

    /// <summary>A real store (Unity IAP). Registered from Assets/Scripts/Monetization when the package is installed.</summary>
    public interface IStoreBackend
    {
        void Begin();
        void BuyRemoveAds(Action<bool> onDone);
    }

    /// <summary>
    /// Hooks for real ad and store SDKs. They live outside this assembly (Assets/Scripts/Monetization)
    /// so the game still compiles before LevelPlay or Unity IAP are installed.
    /// </summary>
    public static class MonetizationServices
    {
        /// <summary>Adds a real ads component to the game object, or null to use <see cref="DebugAds"/>.</summary>
        public static Func<GameObject, IAdsService> CreateAds;
        public static IStoreBackend StoreBackend;

        /// <summary>False hides the No Ads button (a store build with no payment SDK yet).</summary>
        public static bool PurchasesEnabled = true;
    }

    /// <summary>Remove-ads purchase. Uses Unity IAP when installed, otherwise a local test unlock.</summary>
    public static class Store
    {
        public const string RemoveAdsProductId = "remove_ads";

        /// <summary>Raised when remove-ads is bought or restored.</summary>
        public static event Action AdsRemoved;

        public static void BuyRemoveAds(Action<bool> onDone)
        {
            var backend = MonetizationServices.StoreBackend;
            if (backend == null)
            {
                Debug.Log("[IAP] Purchasing " + RemoveAdsProductId + " (test unlock, no store installed)");
                Grant();
                onDone?.Invoke(true);
                return;
            }
            backend.BuyRemoveAds(ok =>
            {
                if (ok) Grant();
                onDone?.Invoke(ok);
            });
        }

        /// <summary>Called by the store on purchase or restore.</summary>
        public static void Grant()
        {
            if (Progress.AdsRemoved) return;
            Progress.AdsRemoved = true;
            AdsRemoved?.Invoke();
        }
    }

    /// <summary>Saved player progress.</summary>
    public static class Progress
    {
        private const string LevelKey = "level";
        private const string AdsRemovedKey = "ads_removed";

        public static int Coins
        {
            get => PlayerPrefs.GetInt("coins", 0);
            set { PlayerPrefs.SetInt("coins", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Theme
        {
            get => PlayerPrefs.GetInt("theme", 0);
            set { PlayerPrefs.SetInt("theme", value); PlayerPrefs.Save(); }
        }

        public static bool OwnsTheme(int index) => index == 0 || PlayerPrefs.GetInt("theme_owned_" + index, 0) == 1;

        public static void UnlockTheme(int index)
        {
            PlayerPrefs.SetInt("theme_owned_" + index, 1);
            PlayerPrefs.Save();
        }

        public static string Language
        {
            get => PlayerPrefs.GetString("language", Loc.DefaultLanguage);
            set { PlayerPrefs.SetString("language", value); PlayerPrefs.Save(); }
        }

        /// <summary>Day number (days since 2000-01-01, local time) of the last claimed daily gift.</summary>
        public static int LastDailyDay
        {
            get => PlayerPrefs.GetInt("daily_day", 0);
            set { PlayerPrefs.SetInt("daily_day", value); PlayerPrefs.Save(); }
        }

        public static int DailyStreak
        {
            get => PlayerPrefs.GetInt("daily_streak", 0);
            set { PlayerPrefs.SetInt("daily_streak", value); PlayerPrefs.Save(); }
        }

        /// <summary>Day number of the last finished daily challenge.</summary>
        public static int ChallengeDay
        {
            get => PlayerPrefs.GetInt("challenge_day", 0);
            set { PlayerPrefs.SetInt("challenge_day", value); PlayerPrefs.Save(); }
        }

        public static int ChallengesDone
        {
            get => PlayerPrefs.GetInt("challenges_done", 0);
            set { PlayerPrefs.SetInt("challenges_done", value); PlayerPrefs.Save(); }
        }

        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt("tutorial_done", 0) == 1;
            set { PlayerPrefs.SetInt("tutorial_done", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Sound
        {
            get => PlayerPrefs.GetInt("sound", 1) == 1;
            set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("vibration", 1) == 1;
            set { PlayerPrefs.SetInt("vibration", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Today => (int)(System.DateTime.Now.Date - new System.DateTime(2000, 1, 1)).TotalDays;

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
