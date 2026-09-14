using System;
using System.Linq;
using System.Threading.Tasks;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

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

                if (arguments.Contains("-rosterAttemptStartRejected"))
                {
                    var rejection = await RequestLobbyActionAsync(roster, roster.RequestStartMatch);
                    if (rejection.Accepted)
                        throw new InvalidOperationException("An unauthorized or invalid match start was accepted.");
                    Debug.Log($"ROSTER_START_REJECTED {rejection.Message}");
                }

                if (arguments.Contains("-rosterReady"))
                {
                    var readyResult = await RequestLobbyActionAsync(roster, () => roster.SetLocalReady(true));
                    if (!readyResult.Accepted)
                        throw new InvalidOperationException("Ready request was rejected: " + readyResult.Message);
                    await WaitUntilAsync(() => roster.TryGetEntry(localId, out var entry) && entry.Ready,
                        TimeSpan.FromSeconds(15), "ready replication");
                }

                var expectKitchen = arguments.Contains("-rosterExpectKitchen");
                if (arguments.Contains("-rosterStartMatch"))
                {
                    await WaitUntilAsync(() => roster.CanLocalHostStart, TimeSpan.FromSeconds(30), "start gate");
                    roster.RequestStartMatch();
                    expectKitchen = true;
                }

                if (arguments.Contains("-rosterDisconnectOnLoading"))
                {
                    await WaitUntilAsync(() => roster.IsLoading, TimeSpan.FromSeconds(30), "loading state");
                    await coordinator.LeaveAsync();
                    Debug.Log("ROSTER_LOADING_DISCONNECT_SUCCESS");
                    Application.Quit(0);
                    return;
                }

                if (arguments.Contains("-rosterExpectLoadingAbort"))
                {
                    await WaitUntilAsync(() => roster.IsLoading, TimeSpan.FromSeconds(30), "loading state");
                    await WaitUntilAsync(() => !roster.IsLoading && roster.Entries.Count == expectedPlayers - 1,
                        TimeSpan.FromSeconds(30), "loading abort and disconnect cleanup");
                    if (SceneManager.GetActiveScene().name == "Kitchen")
                        throw new InvalidOperationException("A partial match started after a loading disconnect.");
                    Debug.Log("ROSTER_LOADING_ABORT_SUCCESS");
                    expectKitchen = false;
                }

                if (expectKitchen)
                {
                    await WaitUntilAsync(() => SceneManager.GetActiveScene().name == "Kitchen",
                        TimeSpan.FromSeconds(60), "synchronized Kitchen load");
                    if (!roster.TryGetEntry(localId, out var preserved) || preserved.Seat != requestedSeat)
                        throw new InvalidOperationException("Assigned role was not preserved into Kitchen.");
                    Debug.Log($"ROSTER_KITCHEN_SUCCESS client={localId} seat={preserved.Seat}");
                }

                if (arguments.Contains("-movementSmoke"))
                {
                    await RunMovementSmokeAsync(arguments, localId, requestedSeat, expectedPlayers);
                }

                if (arguments.Contains("-matchStateSmoke"))
                {
                    await WaitUntilAsync(() =>
                    {
                        var current = NetworkGameManager.Instance;
                        return current != null && current.Phase == MatchPhase.Playing &&
                               current.PlayingEndTimestamp > NetworkManager.Singleton.ServerTime.Time &&
                               current.RemainingPlayingSeconds > 230d && current.RemainingPlayingSeconds <= 240d;
                    }, TimeSpan.FromSeconds(15), "coherent authoritative Playing state");
                    var game = NetworkGameManager.Instance;
                    Debug.Log($"MATCH_STATE_DIAGNOSTIC phase={game.Phase} " +
                              $"deadline={game.PlayingEndTimestamp:F3} remaining={game.RemainingPlayingSeconds:F3}");
                }

                if (arguments.Contains("-foodSpawnSmoke"))
                {
                    var authored = UnityEngine.Object.FindFirstObjectByType<KitchenFoodSpawner>()?.SpawnSet;
                    if (!FoodConfigurationValidator.TryValidate(authored, out var authoredPoints, out var rejection))
                        throw new InvalidOperationException("Invalid authored food: " + rejection);
                    await WaitUntilAsync(() => UnityEngine.Object.FindObjectsByType<FoodItem>(
                            FindObjectsSortMode.None).Length == authored.Entries.Length,
                        TimeSpan.FromSeconds(15), "authoritative food spawn set");
                    var items = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None);
                    if (items.Any(item => item.Lifecycle != FoodLifecycleState.World ||
                                          item.CarrierClientId != FoodItem.NoCarrier ||
                                          item.Definition == null || item.Size != item.Definition.Size) ||
                        items.Sum(item => item.Definition.Points) != authoredPoints)
                        throw new InvalidOperationException("Replicated food state did not match the authored set.");
                    var sizes = string.Join(",", items.GroupBy(item => item.Size).OrderBy(group => group.Key)
                        .Select(group => $"{group.Key}:{group.Count()}"));
                    Debug.Log($"FOOD_SPAWN_DIAGNOSTIC items={items.Length} points={authoredPoints} sizes={sizes}");
                }

                if (arguments.Contains("-foodCarrySmoke"))
                {
                    await RunFoodCarrySmokeAsync(requestedSeat, arguments.Contains("-foodDepositSmoke"));
                }

                if (arguments.Contains("-swatterSmoke"))
                {
                    await RunSwatterSmokeAsync(requestedSeat, arguments.Contains("-respawnSmoke"));
                }

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

        private static async Task RunSwatterSmokeAsync(LobbySeat requestedSeat, bool verifyRespawn)
        {
            await WaitUntilAsync(() => NetworkGameManager.Instance != null &&
                                       NetworkGameManager.Instance.AcceptsGameplayRequests &&
                                       UnityEngine.Object.FindObjectsByType<CockroachMotor>(
                                           FindObjectsSortMode.None).Length == 3,
                TimeSpan.FromSeconds(20), "swatter prerequisites");
            var attack = UnityEngine.Object.FindFirstObjectByType<SwatterAttack>();
            if (attack == null) throw new InvalidOperationException("Human swatter component is missing.");
            var startedAt = NetworkGameManager.Instance.PlayingEndTimestamp -
                            NetworkGameManager.Instance.Rules.MatchDurationSeconds;
            await WaitUntilAsync(() => NetworkManager.Singleton.ServerTime.Time >=
                                       startedAt + (verifyRespawn ? 1.2d : 0.75d),
                TimeSpan.FromSeconds(5), "swatter test time");
            if (requestedSeat == LobbySeat.Human)
                attack.RequestSwingForDiagnostics();
            await WaitUntilAsync(() => attack.ConfirmedImpactSequence == 1,
                TimeSpan.FromSeconds(5), "confirmed swatter impact");
            if (attack.LastConfirmedHitCount != 3)
                throw new InvalidOperationException($"Expected 3 host-computed hits, got {attack.LastConfirmedHitCount}.");
            Debug.Log("SWATTER_DIAGNOSTIC sequence=1 hits=3 reach=1.8 windup=0.25 cooldown=1.1");

            if (verifyRespawn)
            {
                await WaitUntilAsync(() => UnityEngine.Object.FindObjectsByType<CockroachRespawn>(
                        FindObjectsSortMode.None).Count(respawn => respawn.IsRespawning) == 3,
                    TimeSpan.FromSeconds(3), "three replicated knockouts");
                var knockedOut = UnityEngine.Object.FindObjectsByType<CockroachRespawn>(FindObjectsSortMode.None);
                var earliest = knockedOut.Min(respawn => respawn.RespawnEndTimestamp);
                var latest = knockedOut.Max(respawn => respawn.RespawnEndTimestamp);
                var foods = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None);
                if (latest - earliest > 0.1d || foods.Length != 9 ||
                    foods.Any(food => food.Lifecycle != FoodLifecycleState.World) ||
                    foods.Sum(food => food.Definition.Points) != FoodConfigurationValidator.RequiredAvailablePoints ||
                    knockedOut.Any(respawn => respawn.GetComponent<CharacterController>().enabled ||
                                              respawn.GetComponent<CockroachMotor>().CanAcceptInput))
                    throw new InvalidOperationException("Knockout state or cargo recovery disagreed.");
                Debug.Log($"RESPAWN_DIAGNOSTIC phase=knockedOut players=3 cargoWorld=9 " +
                          $"deadline={latest:F3} remaining={knockedOut[0].RemainingRespawnSeconds:F2}");

                await WaitUntilAsync(() => UnityEngine.Object.FindObjectsByType<CockroachRespawn>(
                        FindObjectsSortMode.None).All(respawn => !respawn.IsRespawning),
                    TimeSpan.FromSeconds(6), "safe nest respawn");
                var restored = UnityEngine.Object.FindObjectsByType<CockroachRespawn>(FindObjectsSortMode.None);
                if (restored.Any(respawn => !respawn.GetComponent<CharacterController>().enabled ||
                                            !respawn.GetComponent<CockroachMotor>().CanAcceptInput) ||
                    restored.Select(respawn => respawn.transform.position)
                        .Any(position => position.x < -8f || position.x > -5.5f ||
                                         position.z < 5.2f || position.z > 6.1f))
                    throw new InvalidOperationException("A Cockroach was not restored at a safe nest spawn.");
                Debug.Log("RESPAWN_DIAGNOSTIC phase=restored players=3 collision=true input=true");
            }

            if (!verifyRespawn)
            {
                if (requestedSeat == LobbySeat.Human)
                    attack.RequestSwingForDiagnostics();
                await Task.Delay(500);
                if (attack.ConfirmedImpactSequence != 1)
                    throw new InvalidOperationException("Server cooldown accepted a second immediate swat.");
                Debug.Log("SWATTER_COOLDOWN_DIAGNOSTIC rejectedImmediateRepeat=true");
            }
        }

        private static async Task RunFoodCarrySmokeAsync(LobbySeat requestedSeat, bool deposit)
        {
            await WaitUntilAsync(() => NetworkGameManager.Instance != null &&
                                       NetworkGameManager.Instance.AcceptsGameplayRequests &&
                                       UnityEngine.Object.FindObjectsByType<FoodItem>(
                                           FindObjectsSortMode.None).Length == 9,
                TimeSpan.FromSeconds(20), "food carrying prerequisites");
            var game = NetworkGameManager.Instance;
            var playingStartedAt = game.PlayingEndTimestamp - game.Rules.MatchDurationSeconds;
            await WaitUntilAsync(() => NetworkManager.Singleton.ServerTime.Time >= playingStartedAt + 0.75d,
                TimeSpan.FromSeconds(5), "food pickup contest time");

            var localCarrier = NetworkManager.Singleton.LocalClient.PlayerObject?
                .GetComponent<CockroachFoodCarrier>();
            var contested = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                // The first authored item touches the nest trigger. Use the next
                // Small item so physics cannot auto-deposit before this test asks.
                .OrderBy(food => food.NetworkObjectId).Skip(1).First();
            if (requestedSeat != LobbySeat.Human)
            {
                if (localCarrier == null)
                    throw new InvalidOperationException("Cockroach player has no food carrier component.");
                localCarrier.RequestPickupForDiagnostics(contested);
            }

            if (!deposit)
            {
                await WaitUntilAsync(() =>
                {
                    if (NetworkManager.Singleton.ServerTime.Time < playingStartedAt + 1.75d) return false;
                    var replicatedItems = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None);
                    var replicatedCarriers = UnityEngine.Object.FindObjectsByType<CockroachFoodCarrier>(
                        FindObjectsSortMode.None);
                    return replicatedItems.Count(food => food.Lifecycle == FoodLifecycleState.Carried) == 1 &&
                           replicatedCarriers.Count(carrier => carrier.IsCarrying) == 1;
                }, TimeSpan.FromSeconds(5), "food pickup replication");
                var items = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None);
                var carriers = UnityEngine.Object.FindObjectsByType<CockroachFoodCarrier>(FindObjectsSortMode.None);
                var carriedItems = items.Where(food => food.Lifecycle == FoodLifecycleState.Carried).ToArray();
                var winners = carriers.Where(carrier => carrier.IsCarrying).ToArray();
                if (carriedItems.Length != 1 || winners.Length != 1 ||
                    carriedItems[0].CarrierClientId != winners[0].OwnerClientId ||
                    carriedItems[0].NetworkObjectId != winners[0].CarriedFoodNetworkId ||
                    !winners[0].HasCarriedVisual || winners[0].CarriedVisualSize != carriedItems[0].Size ||
                    Math.Abs(winners[0].GetComponent<CockroachMotor>().CurrentSpeedMultiplier -
                             carriedItems[0].Definition.CarrySpeedMultiplier) > 0.001f)
                    throw new InvalidOperationException("Atomic pickup state, visual, or speed penalty disagreed.");
                Debug.Log($"FOOD_CARRY_DIAGNOSTIC phase=carried winner={winners[0].OwnerClientId} " +
                          $"food={carriedItems[0].NetworkObjectId} size={carriedItems[0].Size} " +
                          $"speed={carriedItems[0].Definition.CarrySpeedMultiplier:F2}");
                if (localCarrier != null && localCarrier.IsCarrying)
                    localCarrier.RequestDropForDiagnostics();
            }
            var expectedItems = deposit ? 8 : 9;
            var expectedPoints = deposit ? 1 : 0;
            await WaitUntilAsync(() => NetworkManager.Singleton.ServerTime.Time >= playingStartedAt + 2.75d &&
                                       UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                                           .Count(food => food.Lifecycle == FoodLifecycleState.World) == expectedItems &&
                                       UnityEngine.Object.FindObjectsByType<CockroachFoodCarrier>(
                                           FindObjectsSortMode.None).All(carrier => !carrier.IsCarrying) &&
                                       NetworkGameManager.Instance.DepositedPoints == expectedPoints,
                TimeSpan.FromSeconds(5), deposit ? "food deposit replication" : "food drop replication");
            var remainingPoints = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                .Sum(food => food.Definition.Points);
            if (remainingPoints + expectedPoints != FoodConfigurationValidator.RequiredAvailablePoints)
                throw new InvalidOperationException("Food was duplicated or lost after its transaction.");
            Debug.Log(deposit
                ? "FOOD_DEPOSIT_DIAGNOSTIC score=1/12 items=8 totalAccountedPoints=18"
                : "FOOD_CARRY_DIAGNOSTIC phase=dropped items=9 points=18");
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

        private static async Task RunMovementSmokeAsync(string[] arguments, ulong localId, LobbySeat expectedSeat,
            int expectedPlayers)
        {
            await WaitUntilAsync(() => NetworkGameManager.Instance != null &&
                                       NetworkGameManager.Instance.Phase == MatchPhase.Playing,
                TimeSpan.FromSeconds(15), "Playing phase before movement");
            await WaitUntilAsync(() =>
            {
                var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                return player != null && player.GetComponent<NetworkRoleAvatar>() != null;
            }, TimeSpan.FromSeconds(30), "local role avatar");
            await WaitUntilAsync(() => UnityEngine.Object.FindObjectsByType<NetworkRoleAvatar>(
                    FindObjectsSortMode.None).Length == expectedPlayers,
                TimeSpan.FromSeconds(30), "remote role avatars");

            var localObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            var identity = localObject.GetComponent<NetworkRoleAvatar>();
            if (identity.Seat != expectedSeat)
                throw new InvalidOperationException($"Spawned {identity.Seat} instead of {expectedSeat}.");

            var before = UnityEngine.Object.FindObjectsByType<NetworkRoleAvatar>(FindObjectsSortMode.None)
                .ToDictionary(avatar => avatar.OwnerClientId, avatar => avatar.transform.position);
            var localStart = localObject.transform.position;
            using var sentBytes = ProfilerRecorder.StartNew(ProfilerCategory.Network, "Total Bytes Sent", 128);
            long sampledBytes = 0;
            for (var frame = 0; frame < 90; frame++)
            {
                var move = GetDiagnosticMove(expectedSeat, frame);
                if (localObject.TryGetComponent<CockroachMotor>(out var cockroach))
                    cockroach.SimulateInput(move, new Vector2(0.25f, 0f), 1f / 30f);
                else if (localObject.TryGetComponent<HumanMotor>(out var human))
                    human.SimulateInput(move, new Vector2(0.25f, 0f), 1f / 30f);
                else
                    throw new InvalidOperationException("Role avatar has no movement controller.");

                sampledBytes += Math.Max(0, sentBytes.LastValue);
                await Task.Delay(33);
            }

            if (Vector3.Distance(localStart, localObject.transform.position) < 0.08f)
                throw new InvalidOperationException("Owner movement waited or failed to move locally.");

            if (arguments.Contains("-movementTeleportViolation"))
            {
                localObject.transform.position = new Vector3(50f, 5f, 50f);
                var monitor = localObject.GetComponent<MovementSanityMonitor>();
                await WaitUntilAsync(() => monitor.CorrectionCount > 0 &&
                                           Mathf.Abs(localObject.transform.position.x) < 9f &&
                                           Mathf.Abs(localObject.transform.position.z) < 7f,
                    TimeSpan.FromSeconds(15), "host movement correction");
                Debug.Log($"MOVEMENT_CORRECTION_SUCCESS corrections={monitor.CorrectionCount}");
            }

            await Task.Delay(2500);
            var avatars = UnityEngine.Object.FindObjectsByType<NetworkRoleAvatar>(FindObjectsSortMode.None);
            var bounds = new Bounds(new Vector3(0f, 1.05f, 0f), new Vector3(17.2f, 3f, 13.2f));
            if (avatars.Length != expectedPlayers || avatars.Any(avatar => !bounds.Contains(avatar.transform.position)))
                throw new InvalidOperationException("Remote player state was missing or outside playable bounds.");
            var remoteMoved = avatars.Any(avatar => avatar.OwnerClientId != localId &&
                                                    before.TryGetValue(avatar.OwnerClientId, out var start) &&
                                                    Vector3.Distance(start, avatar.transform.position) > 0.05f);
            if (expectedPlayers > 1 && !remoteMoved)
                throw new InvalidOperationException("No interpolated remote movement was observed.");

            Debug.Log($"MOVEMENT_DIAGNOSTIC_SUCCESS remoteCount={avatars.Length} sampledSentBytes={sampledBytes} " +
                      $"sampleSeconds=3 unreliableDeltas=true");
        }

        private static Vector2 GetDiagnosticMove(LobbySeat seat, int frame)
        {
            if (frame >= 30) return Vector2.up;
            return seat switch
            {
                LobbySeat.CockroachOne => Vector2.left,
                LobbySeat.CockroachThree => Vector2.right,
                _ => Vector2.up
            };
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

        private static async Task<(bool Accepted, string Message)> RequestLobbyActionAsync(
            NetworkRoster roster, Action request)
        {
            bool? accepted = null;
            var message = string.Empty;
            void OnResolved(bool value, string response)
            {
                accepted = value;
                message = response;
            }

            roster.LocalLobbyActionResolved += OnResolved;
            request();
            await WaitUntilAsync(() => accepted.HasValue, TimeSpan.FromSeconds(15), "lobby action response");
            roster.LocalLobbyActionResolved -= OnResolved;
            return (accepted == true, message);
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
