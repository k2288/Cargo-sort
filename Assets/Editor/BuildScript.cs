using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CargoSort.EditorTools
{
    /// <summary>Command-line builds (used by the GitHub Actions workflow). Also in the menu: Cargo Sort > Build Android APK.</summary>
    public static class BuildScript
    {
        [MenuItem("Cargo Sort/Build Android APK")]
        public static void BuildAndroid()
        {
            ProjectSetup.Setup();

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            EditorUserBuildSettings.buildAppBundle = false;

            // -store googleplay|bazaar|myket picks the store from the command line (CI); otherwise the menu choice is used.
            switch (Arg("-store"))
            {
                case "bazaar": StoreTarget.Set(StoreTarget.Bazaar); break;
                case "myket": StoreTarget.Set(StoreTarget.Myket); break;
                case "googleplay": StoreTarget.Set(null); break;
            }

            string file = $"CargoSort-{StoreTarget.Current}.apk";
            string path = Arg("-customBuildPath") ?? "Builds/Android/" + file;
            if (Directory.Exists(path) || !path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(path, file);
            else
                path = Path.Combine(Path.GetDirectoryName(path) ?? ".", file);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Cargo Sort] Build {report.summary.result}: {path} ({report.summary.totalSize / (1024 * 1024)} MB)");

            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
