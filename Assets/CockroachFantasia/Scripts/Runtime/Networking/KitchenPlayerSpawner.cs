using System.Collections;
using System.Linq;
using CockroachFantasia.World;
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
            if (manager == null || !manager.IsServer || NetworkRoster.Instance == null) yield break;

            var markers = Object.FindObjectsByType<KitchenSpawnMarker>(FindObjectsSortMode.None)
                .ToDictionary(marker => marker.Seat);
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
                avatarObject.GetComponent<NetworkRoleAvatar>().InitializeBeforeSpawn(entry.Seat);
                avatarObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(entry.ClientId, true);
            }
        }
    }
}
