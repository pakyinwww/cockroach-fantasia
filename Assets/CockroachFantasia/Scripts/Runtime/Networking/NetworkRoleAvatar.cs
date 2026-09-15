using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoleAvatar : NetworkBehaviour
    {
        private readonly NetworkVariable<LobbySeat> seat = new();
        public LobbySeat Seat => seat.Value;

        public void InitializeBeforeSpawn(LobbySeat assignedSeat)
        {
            seat.Value = assignedSeat;
        }
    }
}
