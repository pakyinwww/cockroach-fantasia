using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.Networking
{
    public sealed class SessionLifecycleCommandLineRunner : MonoBehaviour
    {
        private const string HostSwitch = "-sessionLifecycleHost";
        private const string RejectSwitch = "-sessionExpectJoinFailure";
        private const string RejoinSwitch = "-sessionRejoinSmoke";
        private const string HostLossSwitch = "-sessionHostLossSmoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Contains(HostSwitch) && !arguments.Contains(RejectSwitch) &&
                !arguments.Contains(RejoinSwitch) && !arguments.Contains(HostLossSwitch))
            {
                return;
            }

            DontDestroyOnLoad(new GameObject(nameof(SessionLifecycleCommandLineRunner))
                .AddComponent<SessionLifecycleCommandLineRunner>());
        }

        private async void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

            try
            {
                var arguments = Environment.GetCommandLineArgs();
                if (arguments.Contains(HostSwitch))
                {
                    await RunHostAsync(arguments);
                }
                else if (arguments.Contains(RejectSwitch))
                {
                    await RunExpectedRejectionAsync(arguments);
                }
                else if (arguments.Contains(RejoinSwitch))
                {
                    await RunRejoinAsync(arguments);
                }
                else
                {
                    await RunHostLossAsync(arguments);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("SESSION_LIFECYCLE_DIAGNOSTIC_FAILED");
                Application.Quit(4);
            }
        }

        private static async Task RunHostAsync(string[] arguments)
        {
            var coordinator = RequireCoordinator();
            if (!await coordinator.HostPrivateSessionAsync())
            {
                throw new InvalidOperationException(coordinator.StatusMessage);
            }

            var codeFile = RequireArgument(arguments, "-roomCodeFile");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(codeFile)) ?? ".");
            File.WriteAllText(codeFile, coordinator.RoomCode);
            Debug.Log($"SESSION_LIFECYCLE_ROOM_CODE {coordinator.RoomCode}");

            var lockAfterSeconds = GetIntArgument(arguments, "-lockAfterSeconds", 0);
            if (lockAfterSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(lockAfterSeconds));
                if (!await coordinator.SetSessionLockedAsync(true))
                {
                    throw new InvalidOperationException("Host could not lock the Session.");
                }

                File.WriteAllText(codeFile + ".locked", coordinator.RoomCode);
                Debug.Log("SESSION_LIFECYCLE_LOCKED");
            }

            await Task.Delay(TimeSpan.FromSeconds(GetIntArgument(arguments, "-sessionDurationSeconds", 60)));
            Debug.Log("SESSION_LIFECYCLE_HOST_SUCCESS");
            Application.Quit(0);
        }

        private static async Task RunExpectedRejectionAsync(string[] arguments)
        {
            var coordinator = RequireCoordinator();
            var expectedText = RequireArgument(arguments, RejectSwitch);
            if (!Enum.TryParse(expectedText, true, out SessionFailureKind expected))
            {
                throw new InvalidOperationException($"Unknown expected failure kind: {expectedText}");
            }

            var directCode = GetArgument(arguments, "-roomCode");
            var roomCode = string.IsNullOrWhiteSpace(directCode)
                ? await WaitForRoomCodeAsync(RequireArgument(arguments, "-roomCodeFile"), TimeSpan.FromSeconds(90))
                : directCode;

            var joined = await coordinator.JoinPrivateSessionAsync(roomCode);
            if (joined || coordinator.LastFailureKind != expected)
            {
                throw new InvalidOperationException(
                    $"Expected {expected}, got joined={joined}, failure={coordinator.LastFailureKind}, message={coordinator.StatusMessage}");
            }

            Debug.Log($"SESSION_LIFECYCLE_EXPECTED_REJECTION_SUCCESS kind={expected}");
            Application.Quit(0);
        }

        private static async Task RunRejoinAsync(string[] arguments)
        {
            var coordinator = RequireCoordinator();
            var roomCode = await WaitForRoomCodeAsync(RequireArgument(arguments, "-roomCodeFile"), TimeSpan.FromSeconds(90));
            if (!await coordinator.JoinPrivateSessionAsync(roomCode))
            {
                throw new InvalidOperationException(coordinator.StatusMessage);
            }

            await WaitUntilAsync(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient,
                TimeSpan.FromSeconds(30), "initial NGO connection");
            await coordinator.LeaveAsync();
            await WaitUntilAsync(() => NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening,
                TimeSpan.FromSeconds(30), "NGO shutdown");

            if (!await coordinator.JoinPrivateSessionAsync(roomCode))
            {
                throw new InvalidOperationException("Rejoin failed: " + coordinator.StatusMessage);
            }

            await WaitUntilAsync(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient,
                TimeSpan.FromSeconds(30), "rejoined NGO connection");
            if (FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length != 1 ||
                NetworkManager.Singleton.LocalClient?.PlayerObject == null ||
                NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.Count(item => item.IsPlayerObject) != 2)
            {
                throw new InvalidOperationException(
                    "Rejoin created a duplicate manager, retained a stale player, or lost the owned player object.");
            }

            Debug.Log("SESSION_LIFECYCLE_REJOIN_SUCCESS");
            await coordinator.LeaveAsync();
            Application.Quit(0);
        }

        private static async Task RunHostLossAsync(string[] arguments)
        {
            var coordinator = RequireCoordinator();
            var roomCode = await WaitForRoomCodeAsync(RequireArgument(arguments, "-roomCodeFile"), TimeSpan.FromSeconds(90));
            if (!await coordinator.JoinPrivateSessionAsync(roomCode))
            {
                throw new InvalidOperationException(coordinator.StatusMessage);
            }

            await WaitUntilAsync(
                () => coordinator.LastFailureKind == SessionFailureKind.HostLeft &&
                      coordinator.State == SessionConnectionState.Failed &&
                      SceneManager.GetActiveScene().name == "FrontEnd" &&
                      NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening,
                TimeSpan.FromSeconds(120), "host-loss cleanup");
            Debug.Log("SESSION_LIFECYCLE_HOST_LOSS_SUCCESS");
            Application.Quit(0);
        }

        private static SessionCoordinator RequireCoordinator()
        {
            return SessionCoordinator.Instance ?? throw new InvalidOperationException("Session coordinator is unavailable.");
        }

        private static async Task<string> WaitForRoomCodeAsync(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                {
                    var code = File.ReadAllText(path).Trim();
                    if (code.Length > 0)
                    {
                        return code;
                    }
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for a room code.");
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string operation)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"Timed out waiting for {operation}.");
        }

        private static string RequireArgument(string[] arguments, string key)
        {
            var value = GetArgument(arguments, key);
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"{key} requires a value.")
                : value;
        }

        private static string GetArgument(string[] arguments, string key)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        private static int GetIntArgument(string[] arguments, string key, int fallback)
        {
            return int.TryParse(GetArgument(arguments, key), out var value) && value >= 0 ? value : fallback;
        }
    }
}
