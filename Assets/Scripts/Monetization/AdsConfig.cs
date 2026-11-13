// Fill these in from the LevelPlay dashboard (https://platform.ironsrc.com) before release.
// App keys and ad unit IDs are not secret: they ship inside the app.
public static class AdsConfig
{
#if UNITY_IOS
    public const string AppKey = "YOUR_IOS_APP_KEY";
    public const string RewardedAdUnitId = "YOUR_IOS_REWARDED_AD_UNIT_ID";
    public const string InterstitialAdUnitId = "YOUR_IOS_INTERSTITIAL_AD_UNIT_ID";
#else
    public const string AppKey = "YOUR_ANDROID_APP_KEY";
    public const string RewardedAdUnitId = "YOUR_ANDROID_REWARDED_AD_UNIT_ID";
    public const string InterstitialAdUnitId = "YOUR_ANDROID_INTERSTITIAL_AD_UNIT_ID";
#endif

    public static bool IsConfigured => !AppKey.StartsWith("YOUR_");
}
