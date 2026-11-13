using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace CargoSort.EditorTools
{
    /// <summary>
    /// Turns SDK defines on when the SDK is in the project and off when it is removed, so the project
    /// always compiles: CARGO_LEVELPLAY (LevelPlay 9+), CARGO_IAP (Unity IAP 5+), CARGO_TAPSELL (Tapsell Plus 2+),
    /// CARGO_POOLAKEY (Cafe Bazaar Poolakey). Tapsell and Poolakey are imported as .unitypackage into Assets.
    /// </summary>
    [InitializeOnLoad]
    public static class MonetizationDefines
    {
        private static readonly (string define, string type)[] Checks =
        {
            ("CARGO_LEVELPLAY", "Unity.Services.LevelPlay.LevelPlayRewardedAd"),
            ("CARGO_IAP", "UnityEngine.Purchasing.UnityIAPServices"),
            ("CARGO_TAPSELL", "TapsellPlusSDK.TapsellPlus"),
            ("CARGO_POOLAKEY", "Bazaar.Poolakey.Payment"),
        };

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone,
        };

        private static bool IsCandidate(string assembly) =>
            assembly.Contains("LevelPlay") || assembly.Contains("Purchasing") || assembly == "Assembly-CSharp";

        static MonetizationDefines()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && IsCandidate(a.GetName().Name))
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Select(t => t.FullName)
                .ToHashSet();

            foreach (var target in Targets)
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';')
                    .Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                bool changed = false;
                foreach (var (define, type) in Checks)
                {
                    bool installed = types.Contains(type);
                    if (installed && !defines.Contains(define)) { defines.Add(define); changed = true; }
                    if (!installed && defines.Contains(define)) { defines.Remove(define); changed = true; }
                }
                if (changed) PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
        }
    }
}
