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

            var gradle = Path.Combine(unityLibraryPath, "build.gradle");
            if (!File.Exists(gradle)) return;
            var text = File.ReadAllText(gradle);
            if (text.Contains(Dependency)) return;
            File.AppendAllText(gradle, "\n// Added by TapsellGradle.cs\ndependencies {\n    implementation '" + Dependency + "'\n}\n");
            UnityEngine.Debug.Log("[Build] Added Tapsell Plus " + Dependency);
        }
    }
}
