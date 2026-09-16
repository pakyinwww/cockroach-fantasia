using CockroachFantasia.Gameplay;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class MatchStateMachineTests
    {
        [Test]
        public void LoadingCountdownPlayingAndTimeoutFollowAuthoritativeTimestamps()
        {
            var match = new MatchStateMachine(240d, 12, 3d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Loading));
            Assert.That(match.TryDepositPoints(3, 10d), Is.False);

            Assert.That(match.BeginCountdown(10d), Is.True);
            Assert.That(match.PhaseEndTimestamp, Is.EqualTo(13d));
            match.Tick(12.999d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Countdown));
            match.Tick(13d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(match.PlayingEndTimestamp, Is.EqualTo(253d));

            match.Tick(252.999d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Playing));
            match.Tick(253d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Human));
            Assert.That(match.TryDepositPoints(12, 253d), Is.False);
        }

        [Test]
        public void QuotaWinsWhenDepositAndTimeoutShareServerTick()
        {
            var match = new MatchStateMachine(240d, 12, 0d);
            match.BeginCountdown(20d);
            match.Tick(20d);

            Assert.That(match.TryDepositPoints(12, 260d), Is.True);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Cockroaches));
            match.Tick(260d);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Cockroaches));
        }

        [Test]
        public void GameplayMutationIsRejectedOutsidePlayingAndCompletionOccursOnce()
        {
            var match = new MatchStateMachine(240d, 12, 1d);
            Assert.That(match.TryDepositPoints(1, 0d), Is.False);
            match.BeginCountdown(0d);
            Assert.That(match.BeginCountdown(0.5d), Is.False);
            Assert.That(match.TryDepositPoints(1, 0.5d), Is.False);
            match.Tick(1d);
            Assert.That(match.TryDepositPoints(6, 2d), Is.True);
            Assert.That(match.TryDepositPoints(6, 3d), Is.True);
            Assert.That(match.DepositedPoints, Is.EqualTo(12));
            Assert.That(match.TryDepositPoints(4, 4d), Is.False);
            Assert.That(match.DepositedPoints, Is.EqualTo(12));
        }

        [Test]
        public void ClientsDeriveTheSameClampedTimeFromSharedDeadline()
        {
            const double sharedDeadline = 253d;
            Assert.That(MatchClock.RemainingSeconds(sharedDeadline, 13d), Is.EqualTo(240d));
            Assert.That(MatchClock.RemainingSeconds(sharedDeadline, 200.25d), Is.EqualTo(52.75d));
            Assert.That(MatchClock.RemainingSeconds(sharedDeadline, 253.5d), Is.Zero);
        }

        [Test]
        public void DiagnosticFinishResolvesOnceAndOnlyWhilePlaying()
        {
            var match = new MatchStateMachine(240d, 12, 0d);
            Assert.That(match.FinishForDiagnostics(MatchWinner.Human), Is.False);
            match.BeginCountdown(0d);
            match.Tick(0d);
            Assert.That(match.FinishForDiagnostics(MatchWinner.Cockroaches), Is.True);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Cockroaches));
            Assert.That(match.FinishForDiagnostics(MatchWinner.Human), Is.False);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Cockroaches));
        }
    }
}
