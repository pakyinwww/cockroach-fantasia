using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoleAvatar : NetworkBehaviour
    {
        private readonly NetworkVariable<LobbySeat> seat = new();
        public LobbySeat Seat => seat.Value;

        public void AssignSeatByServer(LobbySeat assignedSeat)
        {
            if (!IsSpawned || !IsServer) return;
            seat.Value = assignedSeat;
        }
    }
}
