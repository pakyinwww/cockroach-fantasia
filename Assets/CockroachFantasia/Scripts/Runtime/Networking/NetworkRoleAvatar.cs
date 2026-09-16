using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoleAvatar : NetworkBehaviour
    {
        private readonly NetworkVariable<LobbySeat> seat = new();
        private readonly NetworkVariable<bool> bot = new();
        public LobbySeat Seat => seat.Value;
        public bool IsBot => bot.Value;

        public void AssignSeatByServer(LobbySeat assignedSeat)
        {
            if (!IsSpawned || !IsServer) return;
            seat.Value = assignedSeat;
        }

        public void AssignBotByServer(bool value)
        {
            if (!IsSpawned || !IsServer) return;
            bot.Value = value;
            GetComponent<CockroachFantasia.Characters.CockroachMotor>()?.RefreshLocalPresentation();
            GetComponent<CockroachFantasia.Characters.HumanMotor>()?.RefreshLocalPresentation();
        }
    }
}
