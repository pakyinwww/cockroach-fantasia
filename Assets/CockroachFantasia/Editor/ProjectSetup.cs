using System;
using System.IO;
using CockroachFantasia.App;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using CockroachFantasia.UI;
using CockroachFantasia.World;
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
        private const string CockroachPlayerPath = Root + "/Resources/Networking/CockroachPlayer.prefab";
        private const string HumanPlayerPath = Root + "/Resources/Networking/HumanPlayer.prefab";
        private const string NetworkPrefabsPath = Root + "/Resources/Networking/CockroachNetworkPrefabs.asset";
        private const string MatchRulesPath = Root + "/Data/MatchRules/DefaultMatchRules.asset";
        private const string FoodDataRoot = Root + "/Data/FoodDefinitions";
        private const string FoodPrefabRoot = Root + "/Resources/Food";
        private const string FoodSpawnSetPath = FoodDataRoot + "/FixedKitchenFood.asset";

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
            CreateMatchRules();
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

        public static void CaptureKitchenPreviewBatch()
        {
            try
            {
                Run();
                EditorSceneManager.OpenScene($"{ScenesRoot}/Kitchen.unity", OpenSceneMode.Single);
                var camera = UnityEngine.Object.FindFirstObjectByType<Camera>() ??
                             throw new InvalidOperationException("Kitchen preview camera is missing.");
                var target = new RenderTexture(1280, 720, 24);
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Builds/Previews");
                File.WriteAllBytes("Builds/Previews/KitchenGreybox.png", image.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                EditorApplication.Exit(0);
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
                "Art/Characters", "Art/Environment", "Art/Food", "Art/Materials", "Art/UI", "Art/VFX",
                "Audio/Music", "Audio/SFX", "Data/MatchRules", "Data/FoodDefinitions",
                "Input", "Prefabs/Characters", "Prefabs/Food", "Prefabs/Networking",
                "Prefabs/Props", "Prefabs/UI", "Scenes", "Settings",
                "Resources/Networking", "Resources/Food",
                "Scripts/Runtime/App", "Scripts/Runtime/Camera", "Scripts/Runtime/Characters",
                "Scripts/Runtime/Food", "Scripts/Runtime/Gameplay", "Scripts/Runtime/Networking",
                "Scripts/Runtime/UI", "Scripts/Runtime/World", "Scripts/Tests/EditMode", "Scripts/Tests/PlayMode"
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

        private static void CreateMatchRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<MatchRules>(MatchRulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<MatchRules>();
                AssetDatabase.CreateAsset(rules, MatchRulesPath);
            }

            rules.Configure(240f, 12, 3f, 3f);
            EditorUtility.SetDirty(rules);
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
                var sceneAlreadyExists = File.Exists(path);
                var scene = sceneAlreadyExists
                    ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var rootName = $"{definition.Name}Root";
                var root = GameObject.Find(rootName) ?? new GameObject(rootName);
                var marker = root.GetComponent<FoundationMarker>() ?? root.AddComponent<FoundationMarker>();
                if (marker.ScenePurpose != definition.Purpose)
                {
                    marker.Configure(definition.Purpose);
                    EditorUtility.SetDirty(marker);
                }

                if (definition.Name == "Bootstrap" && root.GetComponent<ServicesBootstrap>() == null)
                {
                    root.AddComponent<ServicesBootstrap>();
                }

                CreateCameraAndLight(scene);
                if (definition.Name == "Lobby")
                {
                    CreateLobbyInterface(scene);
                }
                else if (definition.Name == "Kitchen")
                {
                    if (root.GetComponent<KitchenPlayerSpawner>() == null)
                    {
                        root.AddComponent<KitchenPlayerSpawner>();
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                    var gameManager = root.GetComponent<NetworkGameManager>();
                    if (gameManager == null)
                    {
                        if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                        gameManager = root.AddComponent<NetworkGameManager>();
                    }
                    gameManager.Configure(AssetDatabase.LoadAssetAtPath<MatchRules>(MatchRulesPath));
                    EditorUtility.SetDirty(gameManager);
                    var foodSpawner = root.GetComponent<KitchenFoodSpawner>() ?? root.AddComponent<KitchenFoodSpawner>();
                    foodSpawner.Configure(AssetDatabase.LoadAssetAtPath<FoodSpawnSet>(FoodSpawnSetPath));
                    EditorUtility.SetDirty(foodSpawner);
                    CreateKitchenLayout(scene);
                    CreateKitchenHud(scene);
                }

                if (!sceneAlreadyExists || scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene, path);
                }
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

            var cockroachPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CockroachPlayerPath);
            if (cockroachPrefab == null)
            {
                var cockroach = new GameObject("CockroachPlayer");
                cockroach.AddComponent<NetworkObject>();
                var controller = cockroach.AddComponent<CharacterController>();
                controller.radius = 0.16f;
                controller.height = 0.28f;
                controller.center = new Vector3(0f, 0.14f, 0f);
                controller.stepOffset = 0.08f;
                controller.slopeLimit = 52f;

                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "GreyboxBody";
                body.transform.SetParent(cockroach.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.13f, 0f);
                body.transform.localScale = new Vector3(0.3f, 0.12f, 0.42f);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = GetGreyboxMaterial("Nest", new Color(0.29f, 0.14f, 0.19f));

                var pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(cockroach.transform, false);
                pivot.localPosition = new Vector3(0f, 0.17f, 0f);
                var cameraObject = new GameObject("OwnerCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(pivot, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.02f;
                camera.fieldOfView = 68f;
                var listener = cameraObject.GetComponent<AudioListener>();
                listener.enabled = false;

                var carrySocket = new GameObject("CarrySocket").transform;
                carrySocket.SetParent(cockroach.transform, false);
                carrySocket.localPosition = new Vector3(0f, 0.23f, 0.24f);

                var motor = cockroach.AddComponent<CockroachMotor>();
                motor.Configure(pivot, camera, listener, carrySocket);
                cockroachPrefab = PrefabUtility.SaveAsPrefabAsset(cockroach, CockroachPlayerPath);
                UnityEngine.Object.DestroyImmediate(cockroach);
            }
            cockroachPrefab = EnsurePlayerNetworking(CockroachPlayerPath, 3.2f);
            cockroachPrefab = EnsureCockroachFoodCarrier(CockroachPlayerPath);
            cockroachPrefab = EnsureCockroachRespawn(CockroachPlayerPath);

            if (!prefabList.Contains(cockroachPrefab))
            {
                prefabList.Add(new NetworkPrefab { Prefab = cockroachPrefab });
                EditorUtility.SetDirty(prefabList);
            }

            var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPlayerPath);
            if (humanPrefab == null)
            {
                var human = new GameObject("HumanPlayer");
                human.AddComponent<NetworkObject>();
                var controller = human.AddComponent<CharacterController>();
                controller.radius = 0.38f;
                controller.height = 1.8f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.stepOffset = 0.3f;
                controller.slopeLimit = 45f;

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "GreyboxBody";
                body.transform.SetParent(human.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = GetGreyboxMaterial("Counter", new Color(0.38f, 0.58f, 0.62f));

                var pivot = new GameObject("ViewPivot").transform;
                pivot.SetParent(human.transform, false);
                pivot.localPosition = new Vector3(0f, 1.62f, 0f);
                var cameraObject = new GameObject("OwnerCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(pivot, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.04f;
                camera.fieldOfView = 72f;
                var listener = cameraObject.GetComponent<AudioListener>();
                listener.enabled = false;

                var swatterSocket = new GameObject("SwatterSocket").transform;
                swatterSocket.SetParent(pivot, false);
                swatterSocket.localPosition = new Vector3(0.3f, -0.24f, 0.52f);
                swatterSocket.localRotation = Quaternion.Euler(8f, -8f, 0f);

                var motor = human.AddComponent<HumanMotor>();
                motor.Configure(pivot, camera, listener, swatterSocket);
                humanPrefab = PrefabUtility.SaveAsPrefabAsset(human, HumanPlayerPath);
                UnityEngine.Object.DestroyImmediate(human);
            }
            humanPrefab = EnsurePlayerNetworking(HumanPlayerPath, 4.5f);
            humanPrefab = EnsureHumanSwatter(HumanPlayerPath);

            if (!prefabList.Contains(humanPrefab))
            {
                prefabList.Add(new NetworkPrefab { Prefab = humanPrefab });
                EditorUtility.SetDirty(prefabList);
            }

            CreateFoodAssets(prefabList);
        }

        private static void CreateFoodAssets(NetworkPrefabsList prefabList)
        {
            var small = EnsureFoodDefinition(FoodSize.Small, "Cracker Crumb", 1, 0.95f,
                PrimitiveType.Cube, new Vector3(0.18f, 0.06f, 0.14f), new Color(0.82f, 0.59f, 0.27f));
            var medium = EnsureFoodDefinition(FoodSize.Medium, "Cheese Cube", 2, 0.85f,
                PrimitiveType.Cube, new Vector3(0.22f, 0.2f, 0.22f), new Color(1f, 0.72f, 0.12f));
            var large = EnsureFoodDefinition(FoodSize.Large, "Doughnut Piece", 3, 0.70f,
                PrimitiveType.Sphere, new Vector3(0.34f, 0.14f, 0.3f), new Color(0.93f, 0.42f, 0.55f));

            foreach (var definition in new[] { small, medium, large })
            {
                if (!prefabList.Contains(definition.NetworkPrefab))
                    prefabList.Add(new NetworkPrefab { Prefab = definition.NetworkPrefab });
            }
            EditorUtility.SetDirty(prefabList);

            var spawnSet = AssetDatabase.LoadAssetAtPath<FoodSpawnSet>(FoodSpawnSetPath);
            if (spawnSet == null)
            {
                spawnSet = ScriptableObject.CreateInstance<FoodSpawnSet>();
                AssetDatabase.CreateAsset(spawnSet, FoodSpawnSetPath);
            }
            spawnSet.Configure(new[]
            {
                new FoodSpawnEntry(0, small), new FoodSpawnEntry(1, small), new FoodSpawnEntry(2, small),
                new FoodSpawnEntry(6, medium), new FoodSpawnEntry(7, medium), new FoodSpawnEntry(8, medium),
                new FoodSpawnEntry(12, large), new FoodSpawnEntry(13, large), new FoodSpawnEntry(14, large)
            });
            EditorUtility.SetDirty(spawnSet);
        }

        private static FoodDefinition EnsureFoodDefinition(FoodSize size, string label, int points,
            float speedMultiplier, PrimitiveType primitive, Vector3 scale, Color color)
        {
            var definitionPath = $"{FoodDataRoot}/{size}Food.asset";
            var definition = AssetDatabase.LoadAssetAtPath<FoodDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FoodDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var prefabPath = $"{FoodPrefabRoot}/{size}Food.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var food = GameObject.CreatePrimitive(primitive);
                food.name = size + "Food";
                food.transform.localScale = scale;
                food.GetComponent<Renderer>().sharedMaterial = GetGreyboxMaterial("Food" + size, color);
                food.AddComponent<NetworkObject>();
                food.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                food.AddComponent<FoodItem>().Configure(definition);
                prefab = PrefabUtility.SaveAsPrefabAsset(food, prefabPath);
                UnityEngine.Object.DestroyImmediate(food);
            }

            definition.Configure(size, label, points, speedMultiplier, prefab);
            EditorUtility.SetDirty(definition);
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            contents.GetComponent<FoodItem>().Configure(definition);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return definition;
        }

        private static GameObject EnsurePlayerNetworking(string prefabPath, float baseSpeed)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset.GetComponent<OwnerNetworkTransform>() != null &&
                asset.GetComponent<MovementSanityMonitor>() != null &&
                asset.GetComponent<NetworkRoleAvatar>() != null)
                return asset;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var transformSync = root.GetComponent<OwnerNetworkTransform>() ?? root.AddComponent<OwnerNetworkTransform>();
            transformSync.ConfigureForPlayerMotion();
            var monitor = root.GetComponent<MovementSanityMonitor>() ?? root.AddComponent<MovementSanityMonitor>();
            monitor.Configure(baseSpeed);
            if (root.GetComponent<NetworkRoleAvatar>() == null) root.AddComponent<NetworkRoleAvatar>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject EnsureCockroachFoodCarrier(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset.GetComponent<CockroachFoodCarrier>() != null) return asset;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            root.AddComponent<CockroachFoodCarrier>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject EnsureHumanSwatter(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset.GetComponent<SwatterAttack>() != null &&
                asset.transform.Find("ViewPivot/SwatterSocket/SwatterVisual") != null) return asset;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root.GetComponent<SwatterAttack>() == null) root.AddComponent<SwatterAttack>();
            var socket = root.GetComponent<HumanMotor>().SwatterSocket;
            if (socket.Find("SwatterVisual") == null)
            {
                var visual = new GameObject("SwatterVisual").transform;
                visual.SetParent(socket, false);
                var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                handle.name = "Handle";
                handle.transform.SetParent(visual, false);
                handle.transform.localPosition = new Vector3(0f, 0f, 0.22f);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                handle.transform.localScale = new Vector3(0.025f, 0.28f, 0.025f);
                UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());
                handle.GetComponent<Renderer>().sharedMaterial = GetGreyboxMaterial("SwatterHandle",
                    new Color(0.28f, 0.72f, 0.9f));
                var paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                paddle.name = "Paddle";
                paddle.transform.SetParent(visual, false);
                paddle.transform.localPosition = new Vector3(0f, 0f, 0.62f);
                paddle.transform.localScale = new Vector3(0.38f, 0.055f, 0.42f);
                UnityEngine.Object.DestroyImmediate(paddle.GetComponent<Collider>());
                paddle.GetComponent<Renderer>().sharedMaterial = GetGreyboxMaterial("SwatterPaddle",
                    new Color(1f, 0.38f, 0.58f));
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject EnsureCockroachRespawn(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset.GetComponent<CockroachRespawn>() != null) return asset;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            root.AddComponent<CockroachRespawn>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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
            var existingCanvas = GameObject.Find("LobbyCanvas");
            if (existingCanvas != null && existingCanvas.GetComponent<LobbyRosterPresenter>() != null &&
                GameObject.Find("Ready") != null && GameObject.Find("Start") != null &&
                GameObject.Find("LoadingPanel") != null)
            {
                return;
            }

            if (existingCanvas != null) UnityEngine.Object.DestroyImmediate(existingCanvas);
            var existingEventSystem = GameObject.Find("EventSystem");
            if (existingEventSystem != null) UnityEngine.Object.DestroyImmediate(existingEventSystem);

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

            var ready = CreateButton(canvasObject.transform, "Ready", new Vector2(0.38f, 0.22f),
                new Vector2(340f, 82f), out var readyLabel);
            readyLabel.text = "READY UP";
            var start = CreateButton(canvasObject.transform, "Start", new Vector2(0.62f, 0.22f),
                new Vector2(340f, 82f), out var startLabel);
            startLabel.text = "START MATCH";
            var status = CreateText(canvasObject.transform, "Status", "Pick a free seat.", 25,
                new Vector2(0.5f, 0.11f), new Vector2(1200f, 64f));

            var loadingPanel = new GameObject("LoadingPanel", typeof(RectTransform), typeof(Image));
            loadingPanel.transform.SetParent(canvasObject.transform, false);
            var loadingRect = loadingPanel.GetComponent<RectTransform>();
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.offsetMin = loadingRect.offsetMax = Vector2.zero;
            loadingPanel.GetComponent<Image>().color = new Color(0.08f, 0.035f, 0.025f, 0.94f);
            CreateText(loadingPanel.transform, "LoadingText", "LOADING KITCHEN…", 52,
                new Vector2(0.5f, 0.5f), new Vector2(900f, 120f));
            loadingPanel.SetActive(false);

            canvasObject.AddComponent<LobbyRosterPresenter>().Configure(buttons, labels, nameInput, status,
                ready, readyLabel, start, loadingPanel);

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

        private static void CreateKitchenHud(Scene scene)
        {
            var existing = GameObject.Find("MatchHudCanvas");
            if (existing != null && existing.GetComponent<MatchHudPresenter>() != null &&
                existing.transform.Find("ResultsPanel") != null) return;
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var canvasObject = new GameObject("MatchHudCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var timer = CreateText(canvasObject.transform, "Timer", "4:00", 46,
                new Vector2(0.5f, 0.95f), new Vector2(240f, 70f));
            var score = CreateText(canvasObject.transform, "Score", "FOOD  0 / 12", 34,
                new Vector2(0.82f, 0.95f), new Vector2(360f, 64f));
            var announcement = CreateText(canvasObject.transform, "Announcement", string.Empty, 42,
                new Vector2(0.5f, 0.82f), new Vector2(760f, 80f));

            var roachPanel = CreateFullScreenPanel(canvasObject.transform, "CockroachHud");
            var carry = CreateText(roachPanel.transform, "Carry", "CARRY  EMPTY  •  SPEED 100%", 28,
                new Vector2(0.5f, 0.11f), new Vector2(720f, 54f));
            var prompt = CreateText(roachPanel.transform, "InteractPrompt", "E  PICK UP NEARBY FOOD", 25,
                new Vector2(0.5f, 0.055f), new Vector2(620f, 48f));
            var respawn = CreateText(roachPanel.transform, "RespawnCountdown", string.Empty, 44,
                new Vector2(0.5f, 0.5f), new Vector2(520f, 90f));

            var humanPanel = CreateFullScreenPanel(canvasObject.transform, "HumanHud");
            var reticle = CreateText(humanPanel.transform, "Reticle", "+", 36,
                new Vector2(0.5f, 0.5f), new Vector2(70f, 70f));
            var swatter = CreateText(humanPanel.transform, "SwatterReadiness", "SWATTER READY", 28,
                new Vector2(0.83f, 0.08f), new Vector2(360f, 54f));

            var resultsPanel = CreateFullScreenPanel(canvasObject.transform, "ResultsPanel");
            var resultsBackdrop = resultsPanel.AddComponent<Image>();
            resultsBackdrop.color = new Color(0.08f, 0.025f, 0.015f, 0.92f);
            var resultsHeadline = CreateText(resultsPanel.transform, "ResultsHeadline", "THE KITCHEN IS SAVED!", 58,
                new Vector2(0.5f, 0.7f), new Vector2(1100f, 100f));
            var resultsDetail = CreateText(resultsPanel.transform, "ResultsDetail", "FINAL FOOD  0 / 12", 30,
                new Vector2(0.5f, 0.54f), new Vector2(900f, 120f));
            var rematch = CreateButton(resultsPanel.transform, "Rematch", new Vector2(0.38f, 0.3f),
                new Vector2(340f, 82f), out var rematchLabel);
            rematchLabel.text = "REMATCH";
            var returnToMenu = CreateButton(resultsPanel.transform, "ReturnToMenu", new Vector2(0.62f, 0.3f),
                new Vector2(340f, 82f), out var returnLabel);
            returnLabel.text = "RETURN TO MENU";
            var waitingForHost = CreateText(resultsPanel.transform, "WaitingForHost", "WAITING FOR HOST…", 28,
                new Vector2(0.5f, 0.3f), new Vector2(700f, 82f));
            resultsPanel.SetActive(false);

            canvasObject.AddComponent<MatchHudPresenter>().Configure(timer, score, announcement, roachPanel,
                carry, prompt, respawn, humanPanel, reticle, swatter, resultsPanel, resultsHeadline,
                resultsDetail, rematch, returnToMenu, waitingForHost);
        }

        private static GameObject CreateFullScreenPanel(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panel;
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

        private static void CreateKitchenLayout(Scene scene)
        {
            if (GameObject.Find("KitchenLayout") != null) return;

            var shellMaterial = GetGreyboxMaterial("Shell", new Color(0.42f, 0.31f, 0.24f));
            var counterMaterial = GetGreyboxMaterial("Counter", new Color(0.38f, 0.58f, 0.62f));
            var cabinetMaterial = GetGreyboxMaterial("Cabinet", new Color(0.72f, 0.48f, 0.25f));
            var nestMaterial = GetGreyboxMaterial("Nest", new Color(0.29f, 0.14f, 0.19f));
            var routeMaterial = GetGreyboxMaterial("Route", new Color(0.82f, 0.72f, 0.38f));

            var layout = new GameObject("KitchenLayout");
            SceneManager.MoveGameObjectToScene(layout, scene);
            var shell = NewGroup(layout.transform, "01_Shell_And_Bounds");
            CreateBlock(shell, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(18f, 0.5f, 14f), shellMaterial);
            CreateBlock(shell, "Bound_Back", new Vector3(0f, 2f, 7f), new Vector3(18f, 4f, 0.35f), shellMaterial);
            CreateBlock(shell, "Bound_Front", new Vector3(0f, 2f, -7f), new Vector3(18f, 4f, 0.35f), shellMaterial);
            CreateBlock(shell, "Bound_Left", new Vector3(-9f, 2f, 0f), new Vector3(0.35f, 4f, 14f), shellMaterial);
            CreateBlock(shell, "Bound_Right", new Vector3(9f, 2f, 0f), new Vector3(0.35f, 4f, 14f), shellMaterial);

            var routes = NewGroup(layout.transform, "02_Three_Routes");
            var floorRoute = NewGroup(routes, "Route_A_Floor_Long");
            CreateBlock(floorRoute, "Sight_Blocker_A", new Vector3(-1.4f, 1.15f, 0.6f),
                new Vector3(2.5f, 2.3f, 2.3f), cabinetMaterial);
            CreateBlock(floorRoute, "Sight_Blocker_B", new Vector3(4.1f, 0.9f, -0.3f),
                new Vector3(2.2f, 1.8f, 3.3f), cabinetMaterial);
            CreateBlock(floorRoute, "Low_Cover", new Vector3(-4.3f, 0.35f, -1.3f),
                new Vector3(2.2f, 0.7f, 1.1f), routeMaterial);

            var counterRoute = NewGroup(routes, "Route_B_Counter_High");
            CreateBlock(counterRoute, "Counter_Left", new Vector3(-5.9f, 1.25f, -4.8f),
                new Vector3(5.4f, 0.45f, 2.4f), counterMaterial);
            CreateBlock(counterRoute, "Counter_Right", new Vector3(3.2f, 1.25f, -4.8f),
                new Vector3(8.7f, 0.45f, 2.4f), counterMaterial);
            CreateRamp(counterRoute, "Roach_Ramp_Left", new Vector3(-6.1f, 0.58f, -2.75f), -18f,
                new Vector3(1.2f, 0.22f, 4.3f), routeMaterial);
            CreateRamp(counterRoute, "Roach_Ramp_Right", new Vector3(6.2f, 0.58f, -2.75f), -18f,
                new Vector3(1.2f, 0.22f, 4.3f), routeMaterial);

            var cabinetRoute = NewGroup(routes, "Route_C_Cabinet_Tunnel");
            CreateBlock(cabinetRoute, "Tunnel_Roof", new Vector3(5.9f, 0.78f, 3.7f),
                new Vector3(5.2f, 0.22f, 1.8f), cabinetMaterial);
            CreateBlock(cabinetRoute, "Tunnel_Back", new Vector3(5.9f, 0.4f, 4.55f),
                new Vector3(5.2f, 0.8f, 0.18f), cabinetMaterial);
            CreateBlock(cabinetRoute, "Tunnel_Pier_Left", new Vector3(3.35f, 0.4f, 3.7f),
                new Vector3(0.18f, 0.8f, 1.8f), cabinetMaterial);
            CreateBlock(cabinetRoute, "Tunnel_Pier_Right", new Vector3(8.45f, 0.4f, 3.7f),
                new Vector3(0.18f, 0.8f, 1.8f), cabinetMaterial);

            var nest = NewGroup(layout.transform, "03_Protected_Nest");
            CreateBlock(nest, "Nest_Ceiling", new Vector3(-6.4f, 0.62f, 5.75f),
                new Vector3(4.2f, 0.24f, 2.2f), nestMaterial);
            CreateBlock(nest, "Nest_Left_Wall", new Vector3(-8.35f, 0.32f, 5.45f),
                new Vector3(0.3f, 0.64f, 2.8f), nestMaterial);
            CreateBlock(nest, "Entrance_Left_Jamb", new Vector3(-7.15f, 0.32f, 4.72f),
                new Vector3(1.55f, 0.64f, 0.3f), nestMaterial);
            CreateBlock(nest, "Entrance_Right_Jamb", new Vector3(-5.15f, 0.32f, 4.72f),
                new Vector3(1.55f, 0.64f, 0.3f), nestMaterial);
            CreateBlock(nest, "Human_Stop_Lintel", new Vector3(-6.15f, 0.59f, 4.72f),
                new Vector3(0.55f, 0.18f, 0.3f), nestMaterial);

            var entrance = new GameObject("Nest_Entrance").transform;
            entrance.SetParent(nest, false);
            entrance.SetPositionAndRotation(new Vector3(-6.15f, 0.05f, 4.55f), Quaternion.Euler(0f, 180f, 0f));
            var nestZoneObject = new GameObject("Nest_Interior", typeof(BoxCollider), typeof(NestZone));
            nestZoneObject.transform.SetParent(nest, false);
            nestZoneObject.transform.SetPositionAndRotation(new Vector3(-6.4f, 0.27f, 5.7f), Quaternion.identity);
            nestZoneObject.transform.localScale = new Vector3(3.6f, 0.5f, 1.7f);
            nestZoneObject.GetComponent<NestZone>().Configure(entrance);

            var spawns = NewGroup(layout.transform, "04_Player_Spawns");
            CreateSpawn(spawns, "Spawn_Human", LobbySeat.Human, new Vector3(0f, 0.05f, -0.8f), 180f);
            CreateSpawn(spawns, "Respawn_Roach_1", LobbySeat.CockroachOne, new Vector3(-7.35f, 0.05f, 5.65f), 180f);
            CreateSpawn(spawns, "Respawn_Roach_2", LobbySeat.CockroachTwo, new Vector3(-6.7f, 0.05f, 5.65f), 180f);
            CreateSpawn(spawns, "Respawn_Roach_3", LobbySeat.CockroachThree, new Vector3(-6.05f, 0.05f, 5.65f), 180f);

            var food = NewGroup(layout.transform, "05_Food_Spawn_Markers_18");
            var foodPositions = new[]
            {
                new Vector3(-4.9f, 0.12f, 4.6f), new Vector3(-3.7f, 0.12f, 3.4f),
                new Vector3(-7.7f, 0.12f, 2.7f), new Vector3(-4.8f, 0.12f, 1.5f),
                new Vector3(4.1f, 0.12f, 3.7f), new Vector3(7.1f, 0.12f, 3.7f),
                new Vector3(-2.9f, 0.12f, -0.2f), new Vector3(0.4f, 0.12f, 2.7f),
                new Vector3(2.6f, 0.12f, 1.9f), new Vector3(6.9f, 0.12f, 1.3f),
                new Vector3(-7.2f, 1.62f, -4.8f), new Vector3(-4.9f, 1.62f, -4.6f),
                new Vector3(-8f, 0.12f, -3.1f), new Vector3(-2.7f, 0.12f, -5.8f),
                new Vector3(0.2f, 1.62f, -4.8f), new Vector3(3.5f, 1.62f, -4.9f),
                new Vector3(6.7f, 1.62f, -4.7f), new Vector3(8f, 0.12f, -5.8f)
            };
            for (var index = 0; index < foodPositions.Length; index++)
            {
                var markerObject = new GameObject($"Food_{index + 1:00}_{(FoodRiskLevel)(index / 6)}");
                markerObject.transform.SetParent(food, false);
                markerObject.transform.position = foodPositions[index];
                markerObject.AddComponent<FoodSpawnMarker>().Configure(index, (FoodRiskLevel)(index / 6));
            }

            var safety = NewGroup(layout.transform, "06_Recovery_And_Safety");
            var recoveryPoint = new GameObject("Recovery_Point").transform;
            recoveryPoint.SetParent(safety, false);
            recoveryPoint.position = Vector3.up * 0.1f;
            var recovery = new GameObject("Out_Of_Bounds_Recovery", typeof(BoxCollider), typeof(KitchenRecoveryVolume));
            recovery.transform.SetParent(safety, false);
            recovery.transform.position = new Vector3(0f, -2f, 0f);
            recovery.transform.localScale = new Vector3(22f, 1.5f, 18f);
            recovery.GetComponent<KitchenRecoveryVolume>().Configure(recoveryPoint);

            var clutter = NewGroup(layout.transform, "07_Non_Blocking_Clutter");
            CreateClutter(clutter, "Huge_Mug", PrimitiveType.Cylinder, new Vector3(-0.4f, 1.75f, -4.8f),
                new Vector3(0.7f, 0.5f, 0.7f), routeMaterial);
            CreateClutter(clutter, "Fruit_Bowl", PrimitiveType.Sphere, new Vector3(4.7f, 1.63f, -4.7f),
                new Vector3(1.2f, 0.35f, 1.2f), nestMaterial);

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 12.5f, -15.5f), Quaternion.Euler(33f, 0f, 0f));
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        private static void CreateRamp(Transform parent, string name, Vector3 position, float xRotation,
            Vector3 scale, Material material)
        {
            var ramp = CreateBlock(parent, name, position, scale, material);
            ramp.transform.rotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        private static void CreateSpawn(Transform parent, string name, LobbySeat seat, Vector3 position, float yaw)
        {
            var marker = new GameObject(name, typeof(KitchenSpawnMarker));
            marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            marker.GetComponent<KitchenSpawnMarker>().Configure(seat);
        }

        private static void CreateClutter(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            var clutter = GameObject.CreatePrimitive(type);
            clutter.name = name;
            clutter.transform.SetParent(parent, false);
            clutter.transform.position = position;
            clutter.transform.localScale = scale;
            clutter.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(clutter.GetComponent<Collider>());
        }

        private static Material GetGreyboxMaterial(string name, Color color)
        {
            var path = $"{Root}/Art/Materials/Greybox_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = $"Greybox_{name}", color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
