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
            ApplySizeSettings();
            ApplyVersionAndSigning();
            AppIcons.Apply();

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

        /// <summary>
        /// Settings that make the APK smaller. Most of the APK is native code (the Unity engine and the game compiled by
        /// IL2CPP, once per CPU type), so these strip unused engine and .NET code and compile for size.
        /// Assets/link.xml keeps the SDK code that stripping cannot see is used.
        /// "-abi arm64" (or "armv7") on the command line builds for one CPU type only, which roughly halves the native code.
        /// </summary>
        private static void ApplySizeSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Master);
            PlayerSettings.stripUnusedMeshComponents = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;

            switch (Arg("-abi"))
            {
                case "arm64": PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; break;
                case "armv7": PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7; break;
            }
            Debug.Log($"[Cargo Sort] Size settings: stripping Medium, engine stripping on, IL2CPP size build, CPU {PlayerSettings.Android.targetArchitectures}");
        }

        /// <summary>
        /// Version and release signing from the command line (GameCI passes these). Without a keystore
        /// the APK is debug-signed, which is fine for testing but not for store uploads.
        /// </summary>
        private static void ApplyVersionAndSigning()
        {
            var version = Arg("-buildVersion");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            if (int.TryParse(Arg("-androidVersionCode"), out int code) && code > 0)
                PlayerSettings.Android.bundleVersionCode = code;

            string keystore = Arg("-androidKeystoreName");
            string storePass = Arg("-androidKeystorePass");
            if (!string.IsNullOrEmpty(keystore) && !string.IsNullOrEmpty(storePass) && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName");
                PlayerSettings.Android.keyaliasPass = Arg("-androidKeyaliasPass");
                Debug.Log("[Cargo Sort] Release signing with " + keystore);
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("[Cargo Sort] No keystore given: debug-signed test build.");
            }
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
