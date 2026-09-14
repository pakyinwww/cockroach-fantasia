using System;

namespace CockroachFantasia.Food
{
    [Serializable]
    public struct FoodSpawnEntry
    {
        public int MarkerIndex;
        public FoodDefinition Definition;

        public FoodSpawnEntry(int markerIndex, FoodDefinition definition)
        {
            MarkerIndex = markerIndex;
            Definition = definition;
        }
    }
}
