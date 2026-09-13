using System;
using System.IO;
using CockroachFantasia.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.Editor
{
    public static class ProjectSetup
    {
        private const string Root = "Assets/CockroachFantasia";
        private const string SettingsRoot = Root + "/Settings";
        private const string ScenesRoot = Root + "/Scenes";

        private static readonly (string Name, string Purpose)[] Scenes =
        {
            ("Bootstrap", "Persistent services, settings, audio, and error handling"),
            ("FrontEnd", "Private room creation and room-code joining"),
            ("Lobby", "Networked roster, role seats, and ready state"),
            ("Kitchen", "The single networked MVP match")
        };

        [MenuItem("Cockroach Fantasia/Set Up Project Foundation")]
        public static void Run()
        {
            CreateFolders();
            ConfigurePlayer();
            ConfigureRenderPipeline();
            CreateScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cockroach Fantasia project foundation is ready.");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildWindowsDevelopment()
        {
            try
            {
                Run();
                Directory.CreateDirectory("Builds/WindowsDevelopment");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Array.ConvertAll(EditorBuildSettings.scenes, item => item.path),
                    locationPathName = "Builds/WindowsDevelopment/CockroachFantasia.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CreateFolders()
        {
            string[] folders =
            {
                "Art/Characters", "Art/Environment", "Art/Food", "Art/UI", "Art/VFX",
                "Audio/Music", "Audio/SFX", "Data/MatchRules", "Data/FoodDefinitions",
                "Input", "Prefabs/Characters", "Prefabs/Food", "Prefabs/Networking",
                "Prefabs/Props", "Prefabs/UI", "Scenes", "Settings",
                "Scripts/Runtime/App", "Scripts/Runtime/Camera", "Scripts/Runtime/Characters",
                "Scripts/Runtime/Food", "Scripts/Runtime/Gameplay", "Scripts/Runtime/Networking",
                "Scripts/Runtime/UI", "Scripts/Tests/EditMode", "Scripts/Tests/PlayMode"
            };

            foreach (var folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(Root, folder));
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Cockroach Fantasia";
            PlayerSettings.productName = "Cockroach Fantasia";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var projectSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandling = projectSettings.FindProperty("activeInputHandler");
            if (inputHandling != null)
            {
                inputHandling.intValue = 1;
                projectSettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureRenderPipeline()
        {
            const string rendererPath = SettingsRoot + "/CockroachRenderer.asset";
            const string pipelinePath = SettingsRoot + "/CockroachURP.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            var serializedPipeline = new SerializedObject(pipeline);
            var rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        private static void CreateScenes()
        {
            var buildScenes = new EditorBuildSettingsScene[Scenes.Length];

            for (var index = 0; index < Scenes.Length; index++)
            {
                var definition = Scenes[index];
                var path = $"{ScenesRoot}/{definition.Name}.unity";
                var scene = File.Exists(path)
                    ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var rootName = $"{definition.Name}Root";
                var root = GameObject.Find(rootName) ?? new GameObject(rootName);
                var marker = root.GetComponent<FoundationMarker>() ?? root.AddComponent<FoundationMarker>();
                marker.Configure(definition.Purpose);

                if (definition.Name == "Bootstrap" && root.GetComponent<ServicesBootstrap>() == null)
                {
                    root.AddComponent<ServicesBootstrap>();
                }

                CreateCameraAndLight(scene);

                EditorSceneManager.SaveScene(scene, path);
                buildScenes[index] = new EditorBuildSettingsScene(path, true);
            }

            EditorBuildSettings.scenes = buildScenes;
            EditorSceneManager.OpenScene(buildScenes[0].path, OpenSceneMode.Single);
        }

        private static void CreateCameraAndLight(Scene scene)
        {
            if (UnityEngine.Object.FindFirstObjectByType<Camera>() != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -6f), Quaternion.Euler(12f, 0f, 0f));
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.91f, 0.78f);
        }
    }
}
