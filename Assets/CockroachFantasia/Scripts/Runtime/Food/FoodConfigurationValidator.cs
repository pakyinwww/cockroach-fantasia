using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

namespace CockroachFantasia.Food
{
    public static class FoodConfigurationValidator
    {
        public const int RequiredAvailablePoints = 18;

        public static bool TryValidate(FoodSpawnSet spawnSet, out int availablePoints, out string rejection)
        {
            availablePoints = 0;
            if (spawnSet == null || spawnSet.Entries == null || spawnSet.Entries.Length == 0)
            {
                rejection = "The fixed food spawn set is empty.";
                return false;
            }

            var markers = new HashSet<int>();
            var definitions = new Dictionary<FoodSize, FoodDefinition>();
            foreach (var entry in spawnSet.Entries)
            {
                if (entry.MarkerIndex < 0 || !markers.Add(entry.MarkerIndex))
                {
                    rejection = $"Food marker {entry.MarkerIndex} is invalid or duplicated.";
                    return false;
                }

                var definition = entry.Definition;
                if (definition == null || definition.Points <= 0 || definition.CarrySpeedMultiplier <= 0f ||
                    definition.CarrySpeedMultiplier > 1f)
                {
                    rejection = $"Food at marker {entry.MarkerIndex} has a malformed definition.";
                    return false;
                }

                if (definitions.TryGetValue(definition.Size, out var existing) && existing != definition)
                {
                    rejection = $"Food size {definition.Size} maps to multiple definitions.";
                    return false;
                }

                definitions[definition.Size] = definition;
                if (definition.NetworkPrefab == null ||
                    definition.NetworkPrefab.GetComponent<NetworkObject>() == null ||
                    definition.NetworkPrefab.GetComponent<FoodItem>() == null)
                {
                    rejection = $"{definition.DisplayName} has no valid network prefab.";
                    return false;
                }

                availablePoints += definition.Points;
            }

            if (definitions.Keys.Count != 3 || !System.Enum.GetValues(typeof(FoodSize)).Cast<FoodSize>()
                    .All(definitions.ContainsKey))
            {
                rejection = "The spawn set must contain Small, Medium, and Large food.";
                return false;
            }

            if (availablePoints != RequiredAvailablePoints)
            {
                rejection = $"Authored food is worth {availablePoints}; expected {RequiredAvailablePoints}.";
                return false;
            }

            rejection = string.Empty;
            return true;
        }
    }
}
