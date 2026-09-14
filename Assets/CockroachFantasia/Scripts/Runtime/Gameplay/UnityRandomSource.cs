using UnityEngine;

namespace CockroachFantasia.Gameplay
{
    public sealed class UnityRandomSource : IRandomSource
    {
        public int Range(int minimumInclusive, int maximumExclusive)
        {
            return Random.Range(minimumInclusive, maximumExclusive);
        }

        public float Value => Random.value;
    }
}
