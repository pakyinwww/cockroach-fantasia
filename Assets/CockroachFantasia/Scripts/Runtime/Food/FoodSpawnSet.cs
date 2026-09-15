using System;
using UnityEngine;

namespace CockroachFantasia.Food
{
    [CreateAssetMenu(fileName = "FoodSpawnSet", menuName = "Cockroach Fantasia/Food Spawn Set")]
    public sealed class FoodSpawnSet : ScriptableObject
    {
        [SerializeField] private FoodSpawnEntry[] entries = Array.Empty<FoodSpawnEntry>();

        public FoodSpawnEntry[] Entries => entries;

        public void Configure(FoodSpawnEntry[] spawnEntries)
        {
            entries = spawnEntries ?? Array.Empty<FoodSpawnEntry>();
        }
    }
}
