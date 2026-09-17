using UnityEditor;
using UnityEditor.SceneManagement;

namespace CockroachFantasia.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartupScene
    {
        private const string BootstrapScenePath = "Assets/CockroachFantasia/Scenes/Bootstrap.unity";

        static PlayModeStartupScene()
        {
            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrap != null && EditorSceneManager.playModeStartScene != bootstrap)
                EditorSceneManager.playModeStartScene = bootstrap;
        }
    }
}
