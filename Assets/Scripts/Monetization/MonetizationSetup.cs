using CargoSort.Game;
using UnityEngine;

/// <summary>
/// Plugs the right ad network and store into the game for the store this build is for.
/// The store is chosen in the menu Cargo Sort > Store (sets CARGO_STORE_BAZAAR / CARGO_STORE_MYKET;
/// neither means Google Play). SDK defines (CARGO_LEVELPLAY, CARGO_IAP, CARGO_TAPSELL, CARGO_POOLAKEY)
/// are set automatically by Assets/Editor/MonetizationDefines.cs when the SDKs are in the project.
/// This folder has no .asmdef on purpose, so it can see the SDK code.
/// </summary>
public static class MonetizationSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
#if CARGO_STORE_BAZAAR || CARGO_STORE_MYKET
        RegisterIranStores();
#else
        RegisterGooglePlay();
#endif
    }

    private static void RegisterGooglePlay()
    {
#if CARGO_LEVELPLAY && !UNITY_EDITOR
        if (AdsConfig.LevelPlayConfigured)
            MonetizationServices.CreateAds = go => { var ads = go.AddComponent<LevelPlayAds>(); ads.Begin(); return ads; };
        else
            Debug.LogWarning("[Ads] LevelPlay keys are placeholders in AdsConfig. Using test ads.");
#endif
#if CARGO_IAP
        var store = new UnityIapStore();
        MonetizationServices.StoreBackend = store;
        store.Begin();
#endif
    }

    private static void RegisterIranStores()
    {
#if CARGO_TAPSELL && !UNITY_EDITOR
        if (AdsConfig.TapsellConfigured)
            MonetizationServices.CreateAds = go => { var ads = go.AddComponent<TapsellAds>(); ads.Begin(); return ads; };
        else
            Debug.LogWarning("[Ads] Tapsell keys are placeholders in AdsConfig. Using test ads.");
#endif

#if CARGO_STORE_BAZAAR && CARGO_POOLAKEY && !UNITY_EDITOR
        if (AdsConfig.BazaarConfigured)
        {
            var store = new BazaarStore();
            MonetizationServices.StoreBackend = store;
            store.Begin();
        }
#elif CARGO_STORE_MYKET && !UNITY_EDITOR
        // Myket payments are not wired yet: hide the No Ads button instead of faking a purchase.
        MonetizationServices.PurchasesEnabled = false;
#endif
    }
}
