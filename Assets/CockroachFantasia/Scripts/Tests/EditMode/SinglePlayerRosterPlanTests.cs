using System;
using CockroachFantasia.Networking;
using CockroachFantasia.SinglePlayer;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class SinglePlayerRosterPlanTests
    {
        [Test]
        public void HumanPlayerLeavesAllThreeCockroachSeatsForBots()
        {
            var plan = new SinglePlayerRosterPlan(PlayerRole.Human);
            Assert.That(plan.PlayerSeat, Is.EqualTo(LobbySeat.Human));
            Assert.That(plan.BotSeats, Is.EquivalentTo(new[]
                { LobbySeat.CockroachOne, LobbySeat.CockroachTwo, LobbySeat.CockroachThree }));
        }

        [Test]
        public void CockroachPlayerGetsFirstRoachSeatAndBotsFillRemainingRoles()
        {
            var plan = new SinglePlayerRosterPlan(PlayerRole.Cockroach);
            Assert.That(plan.PlayerSeat, Is.EqualTo(LobbySeat.CockroachOne));
            Assert.That(plan.BotSeats, Is.EquivalentTo(new[]
                { LobbySeat.Human, LobbySeat.CockroachTwo, LobbySeat.CockroachThree }));
        }

        [Test]
        public void UnassignedRoleIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SinglePlayerRosterPlan(PlayerRole.Unassigned));
        }
    }
}
