namespace CockroachFantasia.Gameplay
{
    public static class CockroachRespawnRules
    {
        public static bool CanBegin(bool isRespawning, MatchPhase phase)
        {
            return !isRespawning && phase == MatchPhase.Playing;
        }

        public static bool CanComplete(bool isRespawning, double serverTime, double respawnEndTimestamp)
        {
            return isRespawning && serverTime >= respawnEndTimestamp;
        }
    }
}
