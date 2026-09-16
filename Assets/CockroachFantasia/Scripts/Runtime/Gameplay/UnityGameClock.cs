using UnityEngine;

namespace CockroachFantasia.Gameplay
{
    public sealed class UnityGameClock : IGameClock
    {
        public double Now => Time.unscaledTimeAsDouble;
    }
}
