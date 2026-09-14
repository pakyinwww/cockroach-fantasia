using CockroachFantasia.Networking;

namespace CockroachFantasia.Gameplay
{
    public static class SwatterAttackRules
    {
        public const double CooldownSeconds = 1.1d;

        public static bool CanStart(LobbySeat seat, MatchPhase phase, double serverTime,
            double lastAcceptedServerTime)
        {
            return seat == LobbySeat.Human && phase == MatchPhase.Playing &&
                   serverTime - lastAcceptedServerTime >= CooldownSeconds;
        }
    }
}
