using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace CargoSort.EditorTools
{
    /// <summary>
    /// Sets the game's own app icon (Assets/Icons) for every Android icon kind: legacy, round and
    /// adaptive (background + foreground layers). Runs before each build, and from the menu.
    /// </summary>
    public static class AppIcons
    {
        private const string Folder = "Assets/Icons/";

        [MenuItem("Cargo Sort/Apply App Icons")]
        public static void Apply()
        {
            var legacy = Load("icon.png");
            var round = Load("icon-round.png") ?? legacy;
            var back = Load("icon-adaptive-back.png");
            var fore = Load("icon-adaptive-fore.png");
            if (legacy == null)
            {
                Debug.LogWarning("[Cargo Sort] Assets/Icons/icon.png is missing: keeping the default icon.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);

            var target = NamedBuildTarget.Android;
            SetAll(target, AndroidPlatformIconKind.Legacy, icon => icon.SetTexture(legacy));
            SetAll(target, AndroidPlatformIconKind.Round, icon => icon.SetTexture(round));
            if (back != null && fore != null)
                SetAll(target, AndroidPlatformIconKind.Adaptive, icon => icon.SetTextures(back, fore));
            Debug.Log("[Cargo Sort] App icons applied");
        }

        private static void SetAll(NamedBuildTarget target, PlatformIconKind kind, System.Action<PlatformIcon> set)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) set(icon);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        private static Texture2D Load(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + file);
    }
}
