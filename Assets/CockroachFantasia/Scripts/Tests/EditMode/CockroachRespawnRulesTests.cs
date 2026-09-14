using CockroachFantasia.Gameplay;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class CockroachRespawnRulesTests
    {
        [Test]
        public void HitStartsOnlyOnceDuringPlaying()
        {
            Assert.That(CockroachRespawnRules.CanBegin(false, MatchPhase.Playing), Is.True);
            Assert.That(CockroachRespawnRules.CanBegin(true, MatchPhase.Playing), Is.False);
            Assert.That(CockroachRespawnRules.CanBegin(false, MatchPhase.Countdown), Is.False);
            Assert.That(CockroachRespawnRules.CanBegin(false, MatchPhase.Results), Is.False);
        }

        [Test]
        public void RespawnCompletesAtSharedServerDeadline()
        {
            Assert.That(CockroachRespawnRules.CanComplete(true, 12.999d, 13d), Is.False);
            Assert.That(CockroachRespawnRules.CanComplete(true, 13d, 13d), Is.True);
            Assert.That(CockroachRespawnRules.CanComplete(false, 20d, 13d), Is.False);
        }
    }
}
