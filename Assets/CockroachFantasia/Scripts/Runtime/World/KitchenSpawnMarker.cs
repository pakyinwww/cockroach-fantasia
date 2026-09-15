using CockroachFantasia.Networking;
using UnityEngine;

namespace CockroachFantasia.World
{
    public sealed class KitchenSpawnMarker : MonoBehaviour
    {
        [SerializeField] private LobbySeat seat;
        public LobbySeat Seat => seat;

        public void Configure(LobbySeat spawnSeat)
        {
            seat = spawnSeat;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = seat == LobbySeat.Human ? Color.cyan : new Color(0.75f, 0.25f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(0.35f, 0.3f, 0.35f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.7f);
        }
    }
}
