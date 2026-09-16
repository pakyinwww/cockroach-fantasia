using System;

namespace CockroachFantasia.UI
{
    public static class MatchHudFormatter
    {
        public static string FormatTimer(double remainingSeconds)
        {
            var totalSeconds = Math.Max(0, (int)Math.Ceiling(remainingSeconds));
            return $"{totalSeconds / 60:0}:{totalSeconds % 60:00}";
        }

        public static string FormatScore(int points, int quota)
        {
            return $"FOOD  {Math.Max(0, points)} / {Math.Max(1, quota)}";
        }

        public static string FormatRespawn(double remainingSeconds)
        {
            return $"BACK IN {Math.Max(0d, remainingSeconds):0.0}s";
        }
    }
}
