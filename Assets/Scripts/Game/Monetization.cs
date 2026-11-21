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

    /// <summary>A real store (Unity IAP or Cafe Bazaar). Registered from Assets/Scripts/Monetization when the SDK is installed.</summary>
    public interface IStoreBackend
    {
        void Begin();

        /// <summary>Buys a product. Consumables (coins, packs) are consumed right away so they can be bought again.</summary>
        void Buy(string productId, bool consumable, Action<bool> onDone);

        /// <summary>Asks the store again for owned purchases (Remove Ads) and unfinished ones.</summary>
        void Restore();
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

    /// <summary>In-app purchases. Uses the store SDK when installed, otherwise a local test purchase.</summary>
    public static class Store
    {
        public const string RemoveAdsProductId = "remove_ads";

        /// <summary>Raised when remove-ads is bought or restored.</summary>
        public static event Action AdsRemoved;

        /// <summary>Product ids the store backends register (all shop packs).</summary>
        public static string[] ProductIds
        {
            get
            {
                var packs = Economy.AllPacks;
                var ids = new string[packs.Length];
                for (int i = 0; i < packs.Length; i++) ids[i] = packs[i].ProductId;
                return ids;
            }
        }

        public static bool IsConsumable(string productId) => productId != RemoveAdsProductId;

        /// <summary>Buys a pack. The caller gives the items when onDone(true) is called.</summary>
        public static void Buy(Pack pack, Action<bool> onDone)
        {
            var backend = MonetizationServices.StoreBackend;
            if (backend == null)
            {
                Debug.Log("[IAP] Purchasing " + pack.ProductId + " (test purchase, no store installed)");
                onDone?.Invoke(true);
                return;
            }
            backend.Buy(pack.ProductId, pack.Consumable, onDone);
        }

        /// <summary>Raised when the store hands over a purchase outside of Buy (one that was interrupted earlier).</summary>
        public static event Action<string> Delivered;

        /// <summary>Called by a store backend for a paid but not yet given purchase (for example the app closed mid-purchase).</summary>
        public static void Deliver(string productId) => Delivered?.Invoke(productId);

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

        // ----- Boosters you own (from packs, chests and the starter pack) -----

        public static int Owned(Booster b) => PlayerPrefs.GetInt("inv_" + Economy.Key(b), 0);

        public static void AddOwned(Booster b, int amount)
        {
            PlayerPrefs.SetInt("inv_" + Economy.Key(b), Mathf.Max(0, Owned(b) + amount));
            PlayerPrefs.Save();
        }

        // ----- Win streak, piggy bank, offers -----

        /// <summary>World Tour: stars to spend, the current city, and which of its landmarks are built (bits).</summary>
        public static int Stars
        {
            get => PlayerPrefs.GetInt("tour_stars", 0);
            set { PlayerPrefs.SetInt("tour_stars", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int TourCity
        {
            get => PlayerPrefs.GetInt("tour_city", 0);
            set { PlayerPrefs.SetInt("tour_city", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int TourBuilt
        {
            get => PlayerPrefs.GetInt("tour_built", 0);
            set { PlayerPrefs.SetInt("tour_built", value); PlayerPrefs.Save(); }
        }

        /// <summary>Rush Week: the week these points belong to, points, claimed track steps (bits), last week's result.</summary>
        public static int RushWeek
        {
            get => PlayerPrefs.GetInt("rush_week", -1);
            set { PlayerPrefs.SetInt("rush_week", value); PlayerPrefs.Save(); }
        }

        public static int RushPoints
        {
            get => PlayerPrefs.GetInt("rush_points", 0);
            set { PlayerPrefs.SetInt("rush_points", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int RushClaimed
        {
            get => PlayerPrefs.GetInt("rush_claimed", 0);
            set { PlayerPrefs.SetInt("rush_claimed", value); PlayerPrefs.Save(); }
        }

        /// <summary>0 = nothing to show.</summary>
        public static int RushResultRank
        {
            get => PlayerPrefs.GetInt("rush_result_rank", 0);
            set { PlayerPrefs.SetInt("rush_result_rank", value); PlayerPrefs.Save(); }
        }

        public static int RushResultPrize
        {
            get => PlayerPrefs.GetInt("rush_result_prize", 0);
            set { PlayerPrefs.SetInt("rush_result_prize", value); PlayerPrefs.Save(); }
        }

        /// <summary>Last week the player synced with the Rush server (-1 = never).</summary>
        public static int RushOnlineWeek
        {
            get => PlayerPrefs.GetInt("rush_online_week", -1);
            set { PlayerPrefs.SetInt("rush_online_week", value); PlayerPrefs.Save(); }
        }

        /// <summary>A finished week whose rank must still be asked from the server (-1 = none), and its points.</summary>
        public static int RushResultPending
        {
            get => PlayerPrefs.GetInt("rush_result_pending", -1);
            set { PlayerPrefs.SetInt("rush_result_pending", value); PlayerPrefs.Save(); }
        }

        public static int RushResultPoints
        {
            get => PlayerPrefs.GetInt("rush_result_points", 0);
            set { PlayerPrefs.SetInt("rush_result_points", value); PlayerPrefs.Save(); }
        }

        public static bool RushSeen
        {
            get => PlayerPrefs.GetInt("rush_seen", 0) == 1;
            set { PlayerPrefs.SetInt("rush_seen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool TourSeen
        {
            get => PlayerPrefs.GetInt("tour_seen", 0) == 1;
            set { PlayerPrefs.SetInt("tour_seen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int WinStreak
        {
            get => PlayerPrefs.GetInt("win_streak", 0);
            set { PlayerPrefs.SetInt("win_streak", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Piggy
        {
            get => PlayerPrefs.GetInt("piggy", 0);
            set { PlayerPrefs.SetInt("piggy", Mathf.Clamp(value, 0, Economy.PiggyMax)); PlayerPrefs.Save(); }
        }

        public static DateTime StarterStart
        {
            get => long.TryParse(PlayerPrefs.GetString("starter_start", ""), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
            set { PlayerPrefs.SetString("starter_start", value.Ticks.ToString()); PlayerPrefs.Save(); }
        }

        public static bool StarterBought
        {
            get => PlayerPrefs.GetInt("starter_bought", 0) == 1;
            set { PlayerPrefs.SetInt("starter_bought", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool StarterSeen
        {
            get => PlayerPrefs.GetInt("starter_seen", 0) == 1;
            set { PlayerPrefs.SetInt("starter_seen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Day number the free chest was last opened.</summary>
        public static int ChestDay
        {
            get => PlayerPrefs.GetInt("chest_day", -1);
            set { PlayerPrefs.SetInt("chest_day", value); PlayerPrefs.Save(); }
        }

        // ----- Skins -----

        public static string CrateSkin
        {
            get => PlayerPrefs.GetString("crate_skin", "wood");
            set { PlayerPrefs.SetString("crate_skin", value); PlayerPrefs.Save(); }
        }

        public static string TruckSkin
        {
            get => PlayerPrefs.GetString("truck_skin", "classic");
            set { PlayerPrefs.SetString("truck_skin", value); PlayerPrefs.Save(); }
        }

        public static bool OwnsSkin(string kind, Skin skin) => skin.Price == 0 || PlayerPrefs.GetInt("skin_" + kind + "_" + skin.Key, 0) == 1;

        public static void UnlockSkin(string kind, Skin skin)
        {
            PlayerPrefs.SetInt("skin_" + kind + "_" + skin.Key, 1);
            PlayerPrefs.Save();
        }
    }
}
