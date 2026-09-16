using System.Collections;
using System.Linq;
using CockroachFantasia.World;
using CockroachFantasia.SinglePlayer;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    public sealed class KitchenPlayerSpawner : MonoBehaviour
    {
        private const string HumanResource = "Networking/HumanPlayer";
        private const string CockroachResource = "Networking/CockroachPlayer";

        private IEnumerator Start()
        {
            yield return null;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) yield break;

            var markers = Object.FindObjectsByType<KitchenSpawnMarker>(FindObjectsSortMode.None)
                .ToDictionary(marker => marker.Seat);
            if (SinglePlayerCoordinator.Instance != null && SinglePlayerCoordinator.Instance.IsActive)
            {
                SpawnSinglePlayerParty(manager, markers, SinglePlayerCoordinator.Instance.RosterPlan);
                yield break;
            }

            if (NetworkRoster.Instance == null) yield break;
            foreach (var entry in NetworkRoster.Instance.Entries.Where(item => item.Connected))
            {
                if (!markers.TryGetValue(entry.Seat, out var marker) ||
                    !manager.ConnectedClients.TryGetValue(entry.ClientId, out var client))
                    continue;

                var existing = client.PlayerObject;
                if (existing != null && existing.IsSpawned) existing.Despawn(true);

                var resource = entry.Role == PlayerRole.Human ? HumanResource : CockroachResource;
                var prefab = Resources.Load<GameObject>(resource);
                if (prefab == null)
                {
                    Debug.LogError($"Role prefab is missing at Resources/{resource}.");
                    continue;
                }

                var avatarObject = Instantiate(prefab, marker.transform.position, marker.transform.rotation);
                avatarObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(entry.ClientId, true);
                avatarObject.GetComponent<NetworkRoleAvatar>().AssignSeatByServer(entry.Seat);
            }
        }

        private static void SpawnSinglePlayerParty(NetworkManager manager,
            System.Collections.Generic.IReadOnlyDictionary<LobbySeat, KitchenSpawnMarker> markers,
            SinglePlayerRosterPlan plan)
        {
            if (plan == null || !markers.TryGetValue(plan.PlayerSeat, out var playerMarker)) return;
            var existing = manager.LocalClient?.PlayerObject;
            if (existing != null && existing.IsSpawned) existing.Despawn(true);

            var player = SpawnAvatar(plan.PlayerSeat, playerMarker, false);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(manager.LocalClientId, true);
            player.GetComponent<NetworkRoleAvatar>().AssignSeatByServer(plan.PlayerSeat);

            foreach (var seat in plan.BotSeats)
            {
                if (!markers.TryGetValue(seat, out var marker)) continue;
                var bot = SpawnAvatar(seat, marker, true);
                bot.GetComponent<NetworkObject>().Spawn(true);
                var identity = bot.GetComponent<NetworkRoleAvatar>();
                identity.AssignSeatByServer(seat);
                identity.AssignBotByServer(true);
                bot.AddComponent<SinglePlayerBotController>();
            }
        }

        private static GameObject SpawnAvatar(LobbySeat seat, KitchenSpawnMarker marker, bool bot)
        {
            var resource = seat == LobbySeat.Human ? HumanResource : CockroachResource;
            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) throw new System.InvalidOperationException($"Missing Resources/{resource}.");
            var avatar = Instantiate(prefab, marker.transform.position, marker.transform.rotation);
            avatar.name = bot ? $"Bot {seat}" : $"Player {seat}";
            return avatar;
        }
    }
}
