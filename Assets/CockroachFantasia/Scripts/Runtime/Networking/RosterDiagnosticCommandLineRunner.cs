using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    public sealed class RosterDiagnosticCommandLineRunner : MonoBehaviour
    {
        private const string HostSwitch = "-rosterHostSmoke";
        private const string JoinSwitch = "-rosterJoinSmoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Contains(HostSwitch) && !arguments.Contains(JoinSwitch))
            {
                return;
            }

            DontDestroyOnLoad(new GameObject(nameof(RosterDiagnosticCommandLineRunner))
                .AddComponent<RosterDiagnosticCommandLineRunner>());
        }

        private async void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var coordinator = SessionCoordinator.Instance ??
                    throw new InvalidOperationException("Session coordinator is unavailable.");
                var isHost = arguments.Contains(HostSwitch);
                var codeFile = RequireArgument(arguments, "-roomCodeFile");

                if (isHost)
                {
                    if (!await coordinator.HostPrivateSessionAsync())
                    {
                        throw new InvalidOperationException(coordinator.StatusMessage);
                    }

                    System.IO.File.WriteAllText(codeFile, coordinator.RoomCode);
                }
                else
                {
                    var roomCode = await WaitForTextFileAsync(codeFile, TimeSpan.FromSeconds(90));
                    if (!await coordinator.JoinPrivateSessionAsync(roomCode))
                    {
                        throw new InvalidOperationException(coordinator.StatusMessage);
                    }
                }

                await WaitUntilAsync(() => NetworkRoster.Instance != null, TimeSpan.FromSeconds(30), "NetworkRoster");
                var roster = NetworkRoster.Instance;
                var expectedPlayers = GetIntArgument(arguments, "-rosterExpectedPlayers", 4);
                await WaitUntilAsync(() => roster.Entries.Count == expectedPlayers, TimeSpan.FromSeconds(60), "roster players");

                var localId = NetworkManager.Singleton.LocalClientId;
                var displayName = RequireArgument(arguments, "-rosterName");
                roster.SetLocalDisplayName(displayName);
                await WaitUntilAsync(
                    () => roster.TryGetEntry(localId, out var entry) && entry.DisplayName.ToString() == displayName,
                    TimeSpan.FromSeconds(15), "display-name replication");

                var seatText = RequireArgument(arguments, "-rosterSeat");
                if (!Enum.TryParse(seatText, true, out LobbySeat requestedSeat))
                {
                    throw new InvalidOperationException("Unknown roster seat: " + seatText);
                }

                bool? accepted = null;
                string response = null;
                void OnResolved(bool wasAccepted, string message)
                {
                    accepted = wasAccepted;
                    response = message;
                }

                roster.LocalSeatRequestResolved += OnResolved;
                roster.RequestSeat(requestedSeat);
                await WaitUntilAsync(() => accepted.HasValue, TimeSpan.FromSeconds(15), "seat response");
                roster.LocalSeatRequestResolved -= OnResolved;

                var allowRace = arguments.Contains("-allowSeatRace");
                if (!allowRace && accepted != true)
                {
                    throw new InvalidOperationException("Seat claim was rejected: " + response);
                }

                if (accepted == true)
                {
                    await WaitUntilAsync(
                        () => roster.TryGetEntry(localId, out var entry) && entry.Seat == requestedSeat && !entry.Ready,
                        TimeSpan.FromSeconds(15), "seat replication");
                }

                if (arguments.Contains("-requireValidDistribution"))
                {
                    await WaitUntilAsync(
                        () => RosterRules.HasValidRoleDistribution(roster.Entries),
                        TimeSpan.FromSeconds(30), "one Human and three Cockroaches");
                }

                var snapshot = string.Join("|", roster.Entries.OrderBy(entry => entry.ClientId)
                    .Select(entry => $"{entry.ClientId}:{entry.DisplayName}:{entry.Seat}:{entry.Ready}:{entry.Connected}"));
                Debug.Log($"ROSTER_DIAGNOSTIC_SNAPSHOT {snapshot}");

                var end = Time.realtimeSinceStartupAsDouble + GetIntArgument(arguments, "-rosterDurationSeconds", 8);
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    ValidateRosterInvariant(roster);
                    await Task.Delay(100);
                }

                Debug.Log($"ROSTER_DIAGNOSTIC_SUCCESS outcome={(accepted == true ? "won" : "rejected")}");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("ROSTER_DIAGNOSTIC_FAILED");
                Application.Quit(5);
            }
        }

        private static void ValidateRosterInvariant(NetworkRoster roster)
        {
            var connected = roster.Entries.Where(entry => entry.Connected).ToArray();
            var occupied = connected.Where(entry => entry.Seat != LobbySeat.None).ToArray();
            if (occupied.Select(entry => entry.Seat).Distinct().Count() != occupied.Length ||
                occupied.Count(entry => entry.Role == PlayerRole.Human) > 1 ||
                occupied.Count(entry => entry.Role == PlayerRole.Cockroach) > 3)
            {
                throw new InvalidOperationException("Authoritative roster invariant was violated.");
            }
        }

        private static async Task<string> WaitForTextFileAsync(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (System.IO.File.Exists(path))
                {
                    var text = System.IO.File.ReadAllText(path).Trim();
                    if (text.Length > 0) return text;
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for the host room code.");
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string operation)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(100);
            }

            throw new TimeoutException("Timed out waiting for " + operation + ".");
        }

        private static string RequireArgument(string[] arguments, string key)
        {
            var value = GetArgument(arguments, key);
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException(key + " requires a value.")
                : value;
        }

        private static string GetArgument(string[] arguments, string key)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return string.Empty;
        }

        private static int GetIntArgument(string[] arguments, string key, int fallback)
        {
            return int.TryParse(GetArgument(arguments, key), out var value) && value > 0 ? value : fallback;
        }
    }
}
