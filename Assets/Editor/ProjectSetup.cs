using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoSort.EditorTools
{
    /// <summary>
    /// One-time project setup that runs when the project is first opened: creates the Main scene,
    /// adds it to Build Settings, and sets portrait mobile player settings.
    /// Also available from the menu: Cargo Sort > Setup Project.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath)) Setup();
            };
        }

        [MenuItem("Cargo Sort/Setup Project")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Cargo Sort";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.k2288.cargosort");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.k2288.cargosort");

            EditorSceneManager.OpenScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cargo Sort] Project set up. Press Play to try the game.");
        }
    }
}
