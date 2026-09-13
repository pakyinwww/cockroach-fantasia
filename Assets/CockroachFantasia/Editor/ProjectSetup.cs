using System;
using System.IO;
using CockroachFantasia.App;
using CockroachFantasia.Networking;
using CockroachFantasia.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CockroachFantasia.Editor
{
    public static class ProjectSetup
    {
        private const string Root = "Assets/CockroachFantasia";
        private const string SettingsRoot = Root + "/Settings";
        private const string ScenesRoot = Root + "/Scenes";
        private const string DiagnosticAvatarPath = Root + "/Resources/Networking/DiagnosticAvatar.prefab";
        private const string NetworkRosterPath = Root + "/Resources/Networking/NetworkRoster.prefab";
        private const string NetworkPrefabsPath = Root + "/Resources/Networking/CockroachNetworkPrefabs.asset";

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
            CreateNetworkingAssets();
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
                "Resources/Networking",
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
                if (definition.Name == "Lobby")
                {
                    CreateLobbyInterface(scene);
                }

                EditorSceneManager.SaveScene(scene, path);
                buildScenes[index] = new EditorBuildSettingsScene(path, true);
            }

            EditorBuildSettings.scenes = buildScenes;
            EditorSceneManager.OpenScene(buildScenes[0].path, OpenSceneMode.Single);
        }

        private static void CreateNetworkingAssets()
        {
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiagnosticAvatarPath);
            if (avatarPrefab == null)
            {
                var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = "DiagnosticAvatar";
                avatar.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
                avatar.AddComponent<NetworkObject>();
                avatar.AddComponent<DiagnosticAvatar>();
                avatarPrefab = PrefabUtility.SaveAsPrefabAsset(avatar, DiagnosticAvatarPath);
                UnityEngine.Object.DestroyImmediate(avatar);
            }

            var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (prefabList == null)
            {
                prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(prefabList, NetworkPrefabsPath);
            }

            if (!prefabList.Contains(avatarPrefab))
            {
                prefabList.Add(new NetworkPrefab { Prefab = avatarPrefab });
                EditorUtility.SetDirty(prefabList);
            }

            var rosterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkRosterPath);
            if (rosterPrefab == null)
            {
                var roster = new GameObject("NetworkRoster");
                roster.AddComponent<NetworkObject>();
                roster.AddComponent<NetworkRoster>();
                rosterPrefab = PrefabUtility.SaveAsPrefabAsset(roster, NetworkRosterPath);
                UnityEngine.Object.DestroyImmediate(roster);
            }

            if (!prefabList.Contains(rosterPrefab))
            {
                prefabList.Add(new NetworkPrefab { Prefab = rosterPrefab });
                EditorUtility.SetDirty(prefabList);
            }
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

        private static void CreateLobbyInterface(Scene scene)
        {
            if (GameObject.Find("LobbyCanvas") != null)
            {
                return;
            }

            var canvasObject = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateText(canvasObject.transform, "Title", "CHOOSE YOUR KITCHEN ROLE", 44,
                new Vector2(0.5f, 0.87f), new Vector2(900f, 80f));

            var nameInput = CreateInputField(canvasObject.transform, "DisplayName", "Display name",
                new Vector2(0.5f, 0.75f), new Vector2(520f, 64f));
            var buttons = new Button[4];
            var labels = new Text[4];
            var seatNames = new[] { "HUMAN", "COCKROACH 1", "COCKROACH 2", "COCKROACH 3" };
            for (var index = 0; index < buttons.Length; index++)
            {
                var x = 0.2f + index * 0.2f;
                buttons[index] = CreateButton(canvasObject.transform, $"Seat{index + 1}",
                    new Vector2(x, 0.48f), new Vector2(310f, 250f), out labels[index]);
                labels[index].text = seatNames[index] + "\nOpen";
            }

            var status = CreateText(canvasObject.transform, "Status", "Pick a free seat.", 28,
                new Vector2(0.5f, 0.22f), new Vector2(1000f, 70f));
            canvasObject.AddComponent<LobbyRosterPresenter>().Configure(buttons, labels, nameInput, status);

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize,
            Vector2 anchor, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.95f, 0.85f);
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 size,
            out Text label)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            gameObject.GetComponent<Image>().color = new Color(0.24f, 0.13f, 0.1f, 0.96f);
            label = CreateText(gameObject.transform, "Label", string.Empty, 27, new Vector2(0.5f, 0.5f), size - new Vector2(24f, 24f));
            return gameObject.GetComponent<Button>();
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholderValue,
            Vector2 anchor, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            gameObject.GetComponent<Image>().color = new Color(0.1f, 0.07f, 0.06f, 0.95f);

            var inputText = CreateText(gameObject.transform, "Text", string.Empty, 27,
                new Vector2(0.5f, 0.5f), size - new Vector2(32f, 8f));
            inputText.alignment = TextAnchor.MiddleLeft;
            var placeholder = CreateText(gameObject.transform, "Placeholder", placeholderValue, 27,
                new Vector2(0.5f, 0.5f), size - new Vector2(32f, 8f));
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(1f, 0.95f, 0.85f, 0.5f);

            var input = gameObject.GetComponent<InputField>();
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = NetworkRoster.MaximumDisplayNameCharacters;
            return input;
        }
    }
}
