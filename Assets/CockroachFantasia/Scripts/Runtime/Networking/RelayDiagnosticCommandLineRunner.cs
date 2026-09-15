using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    public sealed class RelayDiagnosticCommandLineRunner : MonoBehaviour
    {
        private const string HostSwitch = "-relayHostSmoke";
        private const string ClientSwitch = "-relayJoinSmoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (!Debug.isDebugBuild) return;
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Contains(HostSwitch) && !arguments.Contains(ClientSwitch))
            {
                return;
            }

            DontDestroyOnLoad(new GameObject(nameof(RelayDiagnosticCommandLineRunner))
                .AddComponent<RelayDiagnosticCommandLineRunner>());
        }

        private async void Start()
        {
            try
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;

                var arguments = Environment.GetCommandLineArgs();
                var isHost = arguments.Contains(HostSwitch);
                var codeFile = GetArgument(arguments, "-roomCodeFile");
                var durationSeconds = GetIntArgument(arguments, "-relayDurationSeconds", 600);
                var postSuccessHoldSeconds = GetIntArgument(arguments, "-relayPostSuccessHoldSeconds", isHost ? 0 : 10, true);

                if (string.IsNullOrWhiteSpace(codeFile))
                {
                    throw new InvalidOperationException("-roomCodeFile is required for Relay diagnostics.");
                }

                var coordinator = SessionCoordinator.Instance ??
                    throw new InvalidOperationException("Session coordinator is unavailable.");

                if (isHost)
                {
                    if (!await coordinator.HostPrivateSessionAsync())
                    {
                        throw new InvalidOperationException(coordinator.StatusMessage);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(codeFile)) ?? ".");
                    File.WriteAllText(codeFile, coordinator.RoomCode);
                    Debug.Log($"RELAY_DIAGNOSTIC_ROOM_CODE {coordinator.RoomCode}");
                }
                else
                {
                    var roomCode = await WaitForRoomCodeAsync(codeFile, TimeSpan.FromSeconds(90));
                    if (!await coordinator.JoinPrivateSessionAsync(roomCode))
                    {
                        throw new InvalidOperationException(coordinator.StatusMessage);
                    }
                }

                await WaitForFourPlayersAsync(TimeSpan.FromSeconds(120));
                ValidateOwnedPlayerObjects();
                Debug.Log("RELAY_DIAGNOSTIC_FOUR_PLAYERS_CONNECTED");

                var end = Time.realtimeSinceStartupAsDouble + durationSeconds;
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    ValidateOwnedPlayerObjects();
                    await Task.Delay(1000);
                }

                Debug.Log($"RELAY_DIAGNOSTIC_SUCCESS role={(isHost ? "host" : "client")} duration={durationSeconds}");
                if (postSuccessHoldSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(postSuccessHoldSeconds));
                }

                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("RELAY_DIAGNOSTIC_FAILED");
                Application.Quit(3);
            }
        }

        private static async Task<string> WaitForRoomCodeAsync(string path, TimeSpan timeout)
        {
            var end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                if (File.Exists(path))
                {
                    var code = SessionCoordinator.NormalizeRoomCode(File.ReadAllText(path));
                    if (code.Length > 0)
                    {
                        return code;
                    }
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for the host room code.");
        }

        private static async Task WaitForFourPlayersAsync(TimeSpan timeout)
        {
            var end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                var manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening && manager.ConnectedClientsIds.Count == SessionCoordinator.MaximumPlayers)
                {
                    return;
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for four NGO peers.");
        }

        private static void ValidateOwnedPlayerObjects()
        {
            var manager = NetworkManager.Singleton ?? throw new InvalidOperationException("NetworkManager is missing.");
            if (!manager.IsListening || manager.ConnectedClientsIds.Count != SessionCoordinator.MaximumPlayers)
            {
                throw new InvalidOperationException("A peer disconnected during the Relay soak.");
            }

            var playerObjects = manager.SpawnManager.SpawnedObjectsList.Where(item => item.IsPlayerObject).ToArray();
            if (playerObjects.Length != SessionCoordinator.MaximumPlayers ||
                playerObjects.Select(item => item.OwnerClientId).Distinct().Count() != SessionCoordinator.MaximumPlayers)
            {
                throw new InvalidOperationException("Expected exactly one uniquely owned player object per peer.");
            }
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

        private static int GetIntArgument(string[] arguments, string key, int fallback, bool allowZero = false)
        {
            return int.TryParse(GetArgument(arguments, key), out var value) && (value > 0 || allowZero && value == 0)
                ? value
                : fallback;
        }
    }
}
