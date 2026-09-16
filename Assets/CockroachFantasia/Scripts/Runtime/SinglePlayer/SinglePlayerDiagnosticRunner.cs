using System;
using System.Linq;
using System.Threading.Tasks;
using CockroachFantasia.Characters;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Food;
using CockroachFantasia.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.SinglePlayer
{
    public sealed class SinglePlayerDiagnosticRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-singlePlayerSmoke")) return;
            DontDestroyOnLoad(new GameObject(nameof(SinglePlayerDiagnosticRunner))
                .AddComponent<SinglePlayerDiagnosticRunner>());
        }

        private async void Start()
        {
            try
            {
                var role = Environment.GetCommandLineArgs().Contains("-singlePlayerCockroach")
                    ? PlayerRole.Cockroach
                    : PlayerRole.Human;
                await WaitUntilAsync(() => SceneManager.GetActiveScene().name == "FrontEnd",
                    TimeSpan.FromSeconds(15), "offline-capable front end");
                SinglePlayerCoordinator.Instance.StartGame(role);
                await WaitUntilAsync(() => SceneManager.GetActiveScene().name == "Kitchen" &&
                                           NetworkGameManager.Instance != null &&
                                           NetworkGameManager.Instance.AcceptsGameplayRequests,
                    TimeSpan.FromSeconds(25), "solo Kitchen match");
                await WaitUntilAsync(() => FindObjectsByType<NetworkRoleAvatar>(FindObjectsSortMode.None).Length == 4,
                    TimeSpan.FromSeconds(10), "four solo avatars");

                var avatars = FindObjectsByType<NetworkRoleAvatar>(FindObjectsSortMode.None);
                var local = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkRoleAvatar>();
                if (avatars.Count(avatar => avatar.IsBot) != 3 || local.IsBot ||
                    (role == PlayerRole.Human) != (local.Seat == LobbySeat.Human))
                    throw new InvalidOperationException("The solo role assignment is incorrect.");
                if (avatars.Count(avatar => avatar.Seat == LobbySeat.Human) != 1 ||
                    avatars.Count(avatar => avatar.GetComponent<CockroachMotor>() != null) != 3)
                    throw new InvalidOperationException("Solo mode did not fill one Human and three Cockroaches.");

                var starts = avatars.Where(avatar => avatar.IsBot)
                    .ToDictionary(avatar => avatar, avatar => avatar.transform.position);
                await Task.Delay(2500);
                if (!starts.Any(pair => Vector3.Distance(pair.Key.transform.position, pair.Value) > 0.05f))
                    throw new InvalidOperationException("Solo bots did not begin navigating the kitchen.");

                await WaitUntilAsync(() => FindObjectsByType<CockroachFoodCarrier>(FindObjectsSortMode.None)
                                               .Any(carrier => carrier.GetComponent<NetworkRoleAvatar>().IsBot &&
                                                               carrier.IsCarrying) ||
                                           NetworkGameManager.Instance.DepositedPoints > 0,
                    TimeSpan.FromSeconds(20), "a bot Cockroach to collect food");
                if (role == PlayerRole.Cockroach)
                {
                    var botSwatter = avatars.Single(avatar => avatar.Seat == LobbySeat.Human)
                        .GetComponent<SwatterAttack>();
                    await WaitUntilAsync(() => botSwatter.ConfirmedImpactSequence > 0,
                        TimeSpan.FromSeconds(20), "the bot Human to land a harmless swat");
                }

                Debug.Log($"SINGLE_PLAYER_DIAGNOSTIC_SUCCESS role={role} avatars=4 bots=3 playing=true " +
                          $"foodAi=true swatterAi={(role == PlayerRole.Human ? "not-applicable" : "true")}");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("SINGLE_PLAYER_DIAGNOSTIC_FAILED");
                Application.Quit(5);
            }
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
    }
}
