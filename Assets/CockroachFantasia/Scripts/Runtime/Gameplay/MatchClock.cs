using System;

namespace CockroachFantasia.Gameplay
{
    public static class MatchClock
    {
        public static double RemainingSeconds(double authoritativeEndTimestamp, double synchronizedServerTime)
        {
            return Math.Max(0d, authoritativeEndTimestamp - synchronizedServerTime);
        }
    }
}
