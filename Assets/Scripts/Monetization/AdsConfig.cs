// Keys for the ad networks and stores. None of these are secret: they ship inside the app.
public static class AdsConfig
{
    // ----- Google Play build: Unity LevelPlay (https://platform.ironsrc.com) -----
#if UNITY_IOS
    public const string AppKey = "YOUR_IOS_APP_KEY";
    public const string RewardedAdUnitId = "YOUR_IOS_REWARDED_AD_UNIT_ID";
    public const string InterstitialAdUnitId = "YOUR_IOS_INTERSTITIAL_AD_UNIT_ID";
#else
    public const string AppKey = "2988ea5f5";
    public const string RewardedAdUnitId = "qn0rqs148gv18bfd";
    public const string InterstitialAdUnitId = "vxtuuf5soc2onvik";
#endif

    // ----- Cafe Bazaar / Myket builds: Tapsell Plus (https://dashboard.tapsell.ir) -----
    public const string TapsellAppKey = "gedlrkgciabhnhqtddhforqbelrkcjtoshkqjjiakkmqfspdptnqhtoqonrharljlojgfh";
    public const string TapsellRewardedZoneId = "6af95c6daeabeb750feb8c11";
    public const string TapsellInterstitialZoneId = "6af95d666cbc4169d6c382e6";

    // ----- Analytics: GameAnalytics (https://gameanalytics.com), Android game -----
    public const string GameAnalyticsGameKey = "b08b38c38f8adf414a04ae312ec1c1d7";
    public const string GameAnalyticsSecretKey = "3180372bdb34883ca14a1558b73ed5f33eb9aa42";

    // ----- Cafe Bazaar in-app payments: the RSA public key from the Bazaar developer panel -----
    public const string BazaarRsaPublicKey = "MIHNMA0GCSqGSIb3DQEBAQUAA4G7ADCBtwKBrwCfAXrDaUXK6+oIkWaFFN9tMTFDVlieZk3I1MhDtFmbnAd6oyZwIxhzjKbw3rXZvyJzkEr+svgEWWR8OABIOOciFhs6V5iYrizsFqJ8yWnufZZbP3Uw9/gOwEU2CrMyWFy0pWnP9pjSybrVg0IZcQ44EY0KV+myrWIMaeXI9WEWLD/RkoYszSGEUy+4962i2VgmKoU+9igDVP0S+XCwWZa4zoXatf7j5VLVkMIIaJkCAwEAAQ==";

    public static bool LevelPlayConfigured => !AppKey.StartsWith("YOUR_");
    public static bool TapsellConfigured => !TapsellAppKey.StartsWith("YOUR_");
    public static bool GameAnalyticsConfigured => GameAnalyticsGameKey.Length == 32;
    public static bool BazaarConfigured => !BazaarRsaPublicKey.StartsWith("YOUR_");
}
