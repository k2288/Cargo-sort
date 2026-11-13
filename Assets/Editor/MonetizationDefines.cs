using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace CargoSort.EditorTools
{
    /// <summary>
    /// Turns on CARGO_LEVELPLAY / CARGO_IAP when the LevelPlay (9+) and Unity IAP (5+) packages are installed,
    /// and off when they are removed, so the project always compiles.
    /// </summary>
    [InitializeOnLoad]
    public static class MonetizationDefines
    {
        private static readonly (string define, string type)[] Checks =
        {
            ("CARGO_LEVELPLAY", "Unity.Services.LevelPlay.LevelPlayRewardedAd"),
            ("CARGO_IAP", "UnityEngine.Purchasing.UnityIAPServices"),
        };

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone,
        };

        static MonetizationDefines()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.GetName().Name.Contains("LevelPlay") || a.GetName().Name.Contains("Purchasing")))
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
