using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;

namespace CargoSort.EditorTools
{
    /// <summary>
    /// Adds the Tapsell Plus Android library to Cafe Bazaar and Myket builds. It comes from Maven Central,
    /// so no External Dependency Manager or "Resolve" step is needed (works in CI too).
    /// </summary>
    public sealed class TapsellGradle : IPostGenerateGradleAndroidProject
    {
        private const string Dependency = "ir.tapsell.plus:tapsell-plus-sdk-unity:2.3.3";

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            bool iranStore = defines.Contains("CARGO_STORE_BAZAAR") || defines.Contains("CARGO_STORE_MYKET");
            if (!iranStore || !defines.Contains("CARGO_TAPSELL")) return;

            AddDependency(unityLibraryPath, Dependency);

            // Cafe Bazaar payments (Poolakey). Its Android library is on JitPack.
            if (defines.Contains("CARGO_STORE_BAZAAR") && defines.Contains("CARGO_POOLAKEY"))
            {
                AddJitPack(Path.GetDirectoryName(unityLibraryPath));
                AddDependency(unityLibraryPath, PoolakeyDependency);
            }
        }

        private const string PoolakeyDependency = "com.github.cafebazaar.Poolakey:poolakey:2.0.0";

        private static void AddDependency(string unityLibraryPath, string dependency)
        {
            var gradle = Path.Combine(unityLibraryPath, "build.gradle");
            if (!File.Exists(gradle)) return;
            if (File.ReadAllText(gradle).Contains(dependency)) return;
            File.AppendAllText(gradle, "\n// Added by TapsellGradle.cs\ndependencies {\n    implementation '" + dependency + "'\n}\n");
            UnityEngine.Debug.Log("[Build] Added " + dependency);
        }

        /// <summary>Adds the JitPack repository next to mavenCentral() in settings.gradle.</summary>
        private static void AddJitPack(string projectRoot)
        {
            var settings = Path.Combine(projectRoot, "settings.gradle");
            if (!File.Exists(settings)) return;
            var text = File.ReadAllText(settings);
            if (text.Contains("jitpack.io")) return;
            const string anchor = "mavenCentral()";
            int index = text.LastIndexOf(anchor, System.StringComparison.Ordinal);
            if (index < 0)
            {
                UnityEngine.Debug.LogWarning("[Build] settings.gradle has no mavenCentral(); JitPack not added");
                return;
            }
            text = text.Insert(index + anchor.Length, "\n        maven { url 'https://jitpack.io' }");
            File.WriteAllText(settings, text);
            UnityEngine.Debug.Log("[Build] Added JitPack repository");
        }
    }
}
