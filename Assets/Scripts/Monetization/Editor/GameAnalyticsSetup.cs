using System.IO;
using GameAnalyticsSDK.Setup;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Writes the GameAnalytics settings asset (Resources/GameAnalytics/Settings) with the Android keys
/// from AdsConfig, so no manual setup in the GameAnalytics window is needed (works in CI too).
/// </summary>
public sealed class GameAnalyticsSetup : IPreprocessBuildWithReport
{
    private const string Folder = "Assets/Resources/GameAnalytics";
    private const string AssetPath = Folder + "/Settings.asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Ensure();

    [InitializeOnLoadMethod]
    private static void OnLoad() => EditorApplication.delayCall += Ensure;

    [MenuItem("Cargo Sort/Apply GameAnalytics Keys")]
    public static void Ensure()
    {
        if (!AdsConfig.GameAnalyticsConfigured) return;
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

        var settings = AssetDatabase.LoadAssetAtPath<Settings>(AssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<Settings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }

        int index = settings.Platforms.IndexOf(RuntimePlatform.Android);
        if (index < 0)
        {
            settings.AddPlatform(RuntimePlatform.Android);
            index = settings.Platforms.Count - 1;
        }
        if (settings.GetGameKey(index) == AdsConfig.GameAnalyticsGameKey &&
            settings.GetSecretKey(index) == AdsConfig.GameAnalyticsSecretKey) return;

        settings.UpdateGameKey(index, AdsConfig.GameAnalyticsGameKey);
        settings.UpdateSecretKey(index, AdsConfig.GameAnalyticsSecretKey);
        settings.Build[index] = PlayerSettings.bundleVersion;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cargo Sort] GameAnalytics keys applied for Android");
    }
}
