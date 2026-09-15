using CockroachFantasia.UI;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class MatchHudFormatterTests
    {
        [TestCase(240d, "4:00")]
        [TestCase(10d, "0:10")]
        [TestCase(9.01d, "0:10")]
        [TestCase(0d, "0:00")]
        [TestCase(-4d, "0:00")]
        public void TimerRoundsUpAndNeverShowsNegative(double seconds, string expected)
        {
            Assert.That(MatchHudFormatter.FormatTimer(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void ScoreAndRespawnUseCompactAtAGlanceFormats()
        {
            Assert.That(MatchHudFormatter.FormatScore(7, 12), Is.EqualTo("FOOD  7 / 12"));
            Assert.That(MatchHudFormatter.FormatRespawn(2.34d), Is.EqualTo("BACK IN 2.3s"));
        }
    }
}
