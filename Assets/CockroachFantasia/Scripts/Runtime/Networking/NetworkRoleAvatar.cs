using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoleAvatar : NetworkBehaviour
    {
        private NetworkVariable<LobbySeat> seat;
        public LobbySeat Seat => seat.Value;

        private void Awake()
        {
            seat ??= new NetworkVariable<LobbySeat>();
        }

        public void InitializeBeforeSpawn(LobbySeat assignedSeat)
        {
            seat.Value = assignedSeat;
        }
    }
}
