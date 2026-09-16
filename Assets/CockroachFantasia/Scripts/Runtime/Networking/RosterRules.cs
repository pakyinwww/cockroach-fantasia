using System.Collections.Generic;

namespace CockroachFantasia.Networking
{
    public static class RosterRules
    {
        public static bool IsSelectableSeat(LobbySeat seat)
        {
            return seat is LobbySeat.Human or LobbySeat.CockroachOne or LobbySeat.CockroachTwo or LobbySeat.CockroachThree;
        }

        public static bool CanClaimSeat(IEnumerable<RosterEntry> entries, ulong claimantId, LobbySeat seat,
            bool rosterLocked, out string rejection)
        {
            if (rosterLocked)
            {
                rejection = "Roles are locked while the match starts.";
                return false;
            }

            if (!IsSelectableSeat(seat))
            {
                rejection = "Choose one of the four role seats.";
                return false;
            }

            foreach (var entry in entries)
            {
                if (entry.Connected && entry.ClientId != claimantId && entry.Seat == seat)
                {
                    rejection = seat == LobbySeat.Human
                        ? "Someone else claimed the Human seat first."
                        : "Someone else claimed that Cockroach seat first.";
                    return false;
                }
            }

            rejection = string.Empty;
            return true;
        }

        public static bool HasValidRoleDistribution(IEnumerable<RosterEntry> entries, int requiredPlayers = 4)
        {
            var connected = 0;
            var humans = 0;
            var cockroaches = 0;
            var occupied = new HashSet<LobbySeat>();

            foreach (var entry in entries)
            {
                if (!entry.Connected)
                {
                    continue;
                }

                connected++;
                if (!IsSelectableSeat(entry.Seat) || !occupied.Add(entry.Seat))
                {
                    return false;
                }

                if (entry.Role == PlayerRole.Human) humans++;
                if (entry.Role == PlayerRole.Cockroach) cockroaches++;
            }

            return connected == requiredPlayers && humans == 1 && cockroaches == 3;
        }

        public static bool CanStartMatch(IEnumerable<RosterEntry> entries, bool rosterLocked, out string rejection)
        {
            if (rosterLocked)
            {
                rejection = "The lobby is already loading.";
                return false;
            }

            var snapshot = new List<RosterEntry>(entries);
            if (!HasValidRoleDistribution(snapshot))
            {
                rejection = "Fill exactly one Human and three Cockroach seats first.";
                return false;
            }

            if (snapshot.Exists(entry => entry.Connected && !entry.Ready))
            {
                rejection = "Every player must be ready.";
                return false;
            }

            rejection = string.Empty;
            return true;
        }
    }
}
