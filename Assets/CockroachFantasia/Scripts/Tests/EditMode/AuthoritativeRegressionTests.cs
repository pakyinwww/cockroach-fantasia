using System.Collections.Generic;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class AuthoritativeRegressionTests
    {
        [Test]
        public void SecondHumanAndDuplicateSeatInvalidateRoster()
        {
            var roster = new List<RosterEntry>
            {
                Entry(0, LobbySeat.Human), Entry(1, LobbySeat.Human),
                Entry(2, LobbySeat.CockroachTwo), Entry(3, LobbySeat.CockroachThree)
            };
            Assert.That(RosterRules.HasValidRoleDistribution(roster), Is.False);
            Assert.That(RosterRules.CanClaimSeat(roster, 2, LobbySeat.Human, false, out _), Is.False);
        }

        [Test]
        public void UnauthorizedCarrierCannotDropOrDeposit()
        {
            var state = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;
            Assert.That(FoodLifecycleRules.TryClaim(ref state, ref carrier, 7), Is.True);
            Assert.That(FoodLifecycleRules.TryDrop(ref state, ref carrier, 8), Is.False);
            Assert.That(FoodLifecycleRules.TryDeposit(ref state, ref carrier, 8), Is.False);
            Assert.That(state, Is.EqualTo(FoodLifecycleState.Carried));
            Assert.That(carrier, Is.EqualTo(7));
        }

        [Test]
        public void DepositProcessedBeforeHitAtDeadlineWinsExactlyOnce()
        {
            var match = PlayingMatch();
            var state = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;
            FoodLifecycleRules.TryClaim(ref state, ref carrier, 7);
            Assert.That(FoodLifecycleRules.TryDeposit(ref state, ref carrier, 7), Is.True);
            Assert.That(match.TryDepositPoints(12, 240d), Is.True);
            Assert.That(FoodLifecycleRules.TryDrop(ref state, ref carrier, 7), Is.False);
            match.Tick(240d);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Cockroaches));
        }

        [Test]
        public void HitProcessedBeforeDepositReturnsFoodAndTimeoutWinsExactlyOnce()
        {
            var match = PlayingMatch();
            var state = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;
            FoodLifecycleRules.TryClaim(ref state, ref carrier, 7);
            Assert.That(FoodLifecycleRules.TryDrop(ref state, ref carrier, 7), Is.True);
            Assert.That(FoodLifecycleRules.TryDeposit(ref state, ref carrier, 7), Is.False);
            match.Tick(240d);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Human));
            Assert.That(match.TryDepositPoints(12, 240d), Is.False);
        }

        [Test]
        public void HitAtTimeoutCannotBeginAfterResultResolution()
        {
            var match = PlayingMatch();
            match.Tick(240d);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(CockroachRespawnRules.CanBegin(false, match.Phase), Is.False);
            Assert.That(match.Winner, Is.EqualTo(MatchWinner.Human));
        }

        [Test]
        public void DisconnectWhileCarryingReturnsExactlyOneFoodToWorld()
        {
            var state = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;
            FoodLifecycleRules.TryClaim(ref state, ref carrier, 12);
            Assert.That(FoodLifecycleRules.TryDrop(ref state, ref carrier, 12), Is.True);
            Assert.That(state, Is.EqualTo(FoodLifecycleState.World));
            Assert.That(carrier, Is.EqualTo(FoodItem.NoCarrier));
            Assert.That(FoodLifecycleRules.TryDrop(ref state, ref carrier, 12), Is.False);
        }

        [Test]
        public void MultiColliderSwatSelectsEachNetworkTargetOnce()
        {
            var selected = new HashSet<ulong>();
            var colliderOwners = new ulong[] { 3, 3, 3, 8, 8, 11 };
            var accepted = 0;
            foreach (var id in colliderOwners)
                if (SwatterAttackRules.TrySelectUniqueTarget(id, selected)) accepted++;
            Assert.That(accepted, Is.EqualTo(3));
            Assert.That(selected, Is.EquivalentTo(new ulong[] { 3, 8, 11 }));
        }

        [Test]
        public void LockedRosterRejectsLateRoleClaims()
        {
            Assert.That(RosterRules.CanClaimSeat(new List<RosterEntry>(), 9, LobbySeat.CockroachOne,
                true, out var rejection), Is.False);
            Assert.That(rejection, Does.Contain("locked"));
        }

        private static MatchStateMachine PlayingMatch()
        {
            var match = new MatchStateMachine(240d, 12, 0d);
            match.BeginCountdown(0d);
            match.Tick(0d);
            return match;
        }

        private static RosterEntry Entry(ulong id, LobbySeat seat) => new()
        {
            ClientId = id, Seat = seat, Ready = true, Connected = true
        };
    }
}
