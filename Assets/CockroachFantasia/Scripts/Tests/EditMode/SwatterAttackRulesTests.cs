using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class SwatterAttackRulesTests
    {
        [Test]
        public void OnlyHumanCanStartDuringPlaying()
        {
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.Human, MatchPhase.Playing, 10d,
                double.NegativeInfinity), Is.True);
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.CockroachOne, MatchPhase.Playing, 10d,
                double.NegativeInfinity), Is.False);
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.Human, MatchPhase.Countdown, 10d,
                double.NegativeInfinity), Is.False);
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.Human, MatchPhase.Results, 10d,
                double.NegativeInfinity), Is.False);
        }

        [Test]
        public void ServerCooldownIsInclusiveAtOnePointOneSeconds()
        {
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.Human, MatchPhase.Playing, 21.099d, 20d), Is.False);
            Assert.That(SwatterAttackRules.CanStart(LobbySeat.Human, MatchPhase.Playing, 21.1d, 20d), Is.True);
        }
    }
}
