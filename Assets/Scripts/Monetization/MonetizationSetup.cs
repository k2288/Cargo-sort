using CargoSort.Game;
using UnityEngine;

/// <summary>
/// Plugs the real ad and store SDKs into the game when their packages are installed.
/// CARGO_LEVELPLAY and CARGO_IAP are set automatically by Assets/Editor/MonetizationDefines.cs.
/// This folder has no .asmdef on purpose, so it can see the SDK assemblies.
/// </summary>
public static class MonetizationSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
#if CARGO_LEVELPLAY && !UNITY_EDITOR
        if (AdsConfig.IsConfigured)
        {
            MonetizationServices.CreateAds = go =>
            {
                var ads = go.AddComponent<LevelPlayAds>();
                ads.Begin();
                return ads;
            };
        }
        else
        {
            Debug.LogWarning("[Ads] LevelPlay is installed but AdsConfig has placeholder keys. Using test ads.");
        }
#endif

#if CARGO_IAP
        var store = new UnityIapStore();
        MonetizationServices.StoreBackend = store;
        store.Begin();
#endif
    }
}
