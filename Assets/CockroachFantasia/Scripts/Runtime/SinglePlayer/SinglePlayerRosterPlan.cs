using System;
using System.Collections.Generic;
using CockroachFantasia.Networking;

namespace CockroachFantasia.SinglePlayer
{
    public sealed class SinglePlayerRosterPlan
    {
        private static readonly LobbySeat[] AllSeats =
        {
            LobbySeat.Human,
            LobbySeat.CockroachOne,
            LobbySeat.CockroachTwo,
            LobbySeat.CockroachThree
        };

        public SinglePlayerRosterPlan(PlayerRole playerRole)
        {
            if (playerRole is not PlayerRole.Human and not PlayerRole.Cockroach)
                throw new ArgumentOutOfRangeException(nameof(playerRole));

            PlayerRole = playerRole;
            PlayerSeat = playerRole == PlayerRole.Human ? LobbySeat.Human : LobbySeat.CockroachOne;
            var botSeats = new List<LobbySeat>(3);
            foreach (var seat in AllSeats)
                if (seat != PlayerSeat) botSeats.Add(seat);
            BotSeats = botSeats;
        }

        public PlayerRole PlayerRole { get; }
        public LobbySeat PlayerSeat { get; }
        public IReadOnlyList<LobbySeat> BotSeats { get; }
    }
}
