using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using CockroachFantasia.UI;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class MatchResultsFormatterTests
    {
        [TestCase(MatchWinner.Human, PlayerRole.Human, "YOU WON")]
        [TestCase(MatchWinner.Human, PlayerRole.Cockroach, "YOU LOST")]
        [TestCase(MatchWinner.Cockroaches, PlayerRole.Cockroach, "YOU WON")]
        [TestCase(MatchWinner.Cockroaches, PlayerRole.Human, "YOU LOST")]
        public void OutcomeUsesTheLocalRole(MatchWinner winner, PlayerRole role, string expected)
        {
            Assert.That(MatchResultsFormatter.RoleOutcome(winner, role), Does.StartWith(expected));
        }

        [Test]
        public void HeadlineNamesTheSharedWinner()
        {
            Assert.That(MatchResultsFormatter.Headline(MatchWinner.Cockroaches), Does.Contain("ROACHES"));
            Assert.That(MatchResultsFormatter.Headline(MatchWinner.Human), Does.Contain("KITCHEN"));
        }
    }
}
