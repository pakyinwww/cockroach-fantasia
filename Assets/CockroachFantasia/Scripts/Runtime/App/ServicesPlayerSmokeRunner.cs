using System;
using UnityEngine;

namespace CockroachFantasia.App
{
    public sealed class ServicesPlayerSmokeRunner : MonoBehaviour
    {
        private const string CommandLineSwitch = "-servicesSmokeTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (!Debug.isDebugBuild) return;
            if (!Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argument => string.Equals(argument, CommandLineSwitch, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            new GameObject(nameof(ServicesPlayerSmokeRunner)).AddComponent<ServicesPlayerSmokeRunner>();
        }

        private async void Start()
        {
            var bootstrap = ServicesBootstrap.Instance;
            if (bootstrap == null)
            {
                Debug.LogError("UNITY_SERVICES_PLAYER_SMOKE_FAILED: services bootstrap is missing.");
                Application.Quit(2);
                return;
            }

            var result = await bootstrap.InitializeAsync();
            if (!result.Succeeded || string.IsNullOrWhiteSpace(bootstrap.PlayerId))
            {
                Debug.LogError($"UNITY_SERVICES_PLAYER_SMOKE_FAILED: {result.Message}");
                Application.Quit(2);
                return;
            }

            Debug.Log($"UNITY_SERVICES_PLAYER_SMOKE_SUCCESS player={bootstrap.PlayerId}");
            Application.Quit(0);
        }
    }
}
