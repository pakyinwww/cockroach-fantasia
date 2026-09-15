using System;
using Unity.Collections;
using Unity.Netcode;

namespace CockroachFantasia.Networking
{
    public struct RosterEntry : INetworkSerializable, IEquatable<RosterEntry>
    {
        public ulong ClientId;
        public FixedString32Bytes DisplayName;
        public LobbySeat Seat;
        public bool Ready;
        public bool Connected;

        public PlayerRole Role => Seat switch
        {
            LobbySeat.Human => PlayerRole.Human,
            LobbySeat.CockroachOne or LobbySeat.CockroachTwo or LobbySeat.CockroachThree => PlayerRole.Cockroach,
            _ => PlayerRole.Unassigned
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref Connected);
        }

        public bool Equals(RosterEntry other)
        {
            return ClientId == other.ClientId && DisplayName.Equals(other.DisplayName) && Seat == other.Seat &&
                   Ready == other.Ready && Connected == other.Connected;
        }

        public override bool Equals(object obj) => obj is RosterEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, DisplayName, (byte)Seat, Ready, Connected);
    }
}
