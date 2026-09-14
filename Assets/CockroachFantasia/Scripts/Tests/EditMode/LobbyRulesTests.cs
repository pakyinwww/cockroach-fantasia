using System.Collections.Generic;
using CockroachFantasia.Networking;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class LobbyRulesTests
    {
        [Test]
        public void StartRequiresExactlyFourValidSeats()
        {
            var roster = ReadyRoster();
            roster.RemoveAt(roster.Count - 1);

            Assert.That(RosterRules.CanStartMatch(roster, false, out var rejection), Is.False);
            Assert.That(rejection, Does.Contain("exactly one Human"));
        }

        [Test]
        public void StartRequiresEveryPlayerReady()
        {
            var roster = ReadyRoster();
            var unready = roster[2];
            unready.Ready = false;
            roster[2] = unready;

            Assert.That(RosterRules.CanStartMatch(roster, false, out var rejection), Is.False);
            Assert.That(rejection, Does.Contain("Every player"));
        }

        [Test]
        public void CompleteReadyRosterCanStartUnlessLocked()
        {
            var roster = ReadyRoster();

            Assert.That(RosterRules.CanStartMatch(roster, false, out _), Is.True);
            Assert.That(RosterRules.CanStartMatch(roster, true, out var rejection), Is.False);
            Assert.That(rejection, Does.Contain("already loading"));
        }

        private static List<RosterEntry> ReadyRoster()
        {
            return new List<RosterEntry>
            {
                Entry(0, LobbySeat.Human),
                Entry(1, LobbySeat.CockroachOne),
                Entry(2, LobbySeat.CockroachTwo),
                Entry(3, LobbySeat.CockroachThree)
            };
        }

        private static RosterEntry Entry(ulong clientId, LobbySeat seat)
        {
            return new RosterEntry { ClientId = clientId, Seat = seat, Ready = true, Connected = true };
        }
    }
}
