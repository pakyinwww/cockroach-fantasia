using System;

namespace CockroachFantasia.Gameplay
{
    public sealed class MatchStateMachine
    {
        private readonly double durationSeconds;
        private readonly double countdownSeconds;
        private readonly int quotaPoints;

        public MatchPhase Phase { get; private set; } = MatchPhase.Loading;
        public MatchWinner Winner { get; private set; }
        public int DepositedPoints { get; private set; }
        public double PhaseEndTimestamp { get; private set; }
        public double PlayingEndTimestamp { get; private set; }

        public MatchStateMachine(double duration, int quota, double countdown)
        {
            if (duration <= 0d) throw new ArgumentOutOfRangeException(nameof(duration));
            if (quota <= 0) throw new ArgumentOutOfRangeException(nameof(quota));
            if (countdown < 0d) throw new ArgumentOutOfRangeException(nameof(countdown));
            durationSeconds = duration;
            quotaPoints = quota;
            countdownSeconds = countdown;
        }

        public bool BeginCountdown(double serverTime)
        {
            if (Phase != MatchPhase.Loading) return false;
            Phase = MatchPhase.Countdown;
            PhaseEndTimestamp = serverTime + countdownSeconds;
            return true;
        }

        public void Tick(double serverTime)
        {
            if (Phase == MatchPhase.Countdown && serverTime >= PhaseEndTimestamp)
            {
                Phase = MatchPhase.Playing;
                PlayingEndTimestamp = PhaseEndTimestamp + durationSeconds;
                PhaseEndTimestamp = PlayingEndTimestamp;
            }

            if (Phase != MatchPhase.Playing) return;
            ResolvePlaying(serverTime);
        }

        public bool TryDepositPoints(int points, double serverTime)
        {
            if (Phase != MatchPhase.Playing || points <= 0) return false;
            DepositedPoints += points;
            ResolvePlaying(serverTime);
            return true;
        }

        private void ResolvePlaying(double serverTime)
        {
            if (DepositedPoints >= quotaPoints)
            {
                Finish(MatchWinner.Cockroaches);
                return;
            }

            if (serverTime >= PlayingEndTimestamp)
                Finish(MatchWinner.Human);
        }

        private void Finish(MatchWinner winner)
        {
            if (Phase == MatchPhase.Results) return;
            Winner = winner;
            Phase = MatchPhase.Results;
        }
    }
}
