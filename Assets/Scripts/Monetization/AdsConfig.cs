// Keys for the ad networks and stores. None of these are secret: they ship inside the app.
public static class AdsConfig
{
    // ----- Google Play build: Unity LevelPlay (https://platform.ironsrc.com) -----
#if UNITY_IOS
    public const string AppKey = "YOUR_IOS_APP_KEY";
    public const string RewardedAdUnitId = "YOUR_IOS_REWARDED_AD_UNIT_ID";
    public const string InterstitialAdUnitId = "YOUR_IOS_INTERSTITIAL_AD_UNIT_ID";
#else
    public const string AppKey = "YOUR_ANDROID_APP_KEY";
    public const string RewardedAdUnitId = "YOUR_ANDROID_REWARDED_AD_UNIT_ID";
    public const string InterstitialAdUnitId = "YOUR_ANDROID_INTERSTITIAL_AD_UNIT_ID";
#endif

    // ----- Cafe Bazaar / Myket builds: Tapsell Plus (https://dashboard.tapsell.ir) -----
    public const string TapsellAppKey = "YOUR_TAPSELL_APP_KEY";
    public const string TapsellRewardedZoneId = "YOUR_TAPSELL_REWARDED_ZONE_ID";
    public const string TapsellInterstitialZoneId = "YOUR_TAPSELL_INTERSTITIAL_ZONE_ID";

    // ----- Cafe Bazaar in-app payments: the RSA public key from the Bazaar developer panel -----
    public const string BazaarRsaPublicKey = "YOUR_BAZAAR_RSA_PUBLIC_KEY";

    public static bool LevelPlayConfigured => !AppKey.StartsWith("YOUR_");
    public static bool TapsellConfigured => !TapsellAppKey.StartsWith("YOUR_");
    public static bool BazaarConfigured => !BazaarRsaPublicKey.StartsWith("YOUR_");
}
