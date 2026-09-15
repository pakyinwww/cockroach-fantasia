using System.Collections;
using System.Linq;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Food
{
    public sealed class KitchenFoodSpawner : MonoBehaviour
    {
        [SerializeField] private FoodSpawnSet spawnSet;

        public FoodSpawnSet SpawnSet => spawnSet;

        public void Configure(FoodSpawnSet fixedSpawnSet)
        {
            spawnSet = fixedSpawnSet;
        }

        private IEnumerator Start()
        {
            yield return null;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) yield break;
            if (!FoodConfigurationValidator.TryValidate(spawnSet, out _, out var rejection))
            {
                if (Debug.isDebugBuild || Application.isEditor)
                    Debug.LogError("Food configuration rejected: " + rejection);
                yield break;
            }

            var markers = Object.FindObjectsByType<FoodSpawnMarker>(FindObjectsSortMode.None)
                .ToDictionary(marker => marker.Index);
            foreach (var entry in spawnSet.Entries)
            {
                if (!markers.TryGetValue(entry.MarkerIndex, out var marker))
                {
                    Debug.LogError($"Food marker {entry.MarkerIndex} is missing from Kitchen.");
                    continue;
                }

                var food = Instantiate(entry.Definition.NetworkPrefab, marker.transform.position,
                    marker.transform.rotation);
                food.GetComponent<FoodItem>().InitializeBeforeSpawn();
                food.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            }
        }
    }
}
