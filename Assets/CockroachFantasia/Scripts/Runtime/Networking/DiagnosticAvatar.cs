using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticAvatar : NetworkBehaviour
    {
        private static readonly Color[] PlayerColors =
        {
            new Color(0.95f, 0.32f, 0.32f),
            new Color(0.25f, 0.72f, 1f),
            new Color(0.47f, 0.88f, 0.38f),
            new Color(1f, 0.75f, 0.2f)
        };

        public override void OnNetworkSpawn()
        {
            name = $"DiagnosticAvatar_P{OwnerClientId}";
            ApplyPlayerColor();

            if (IsServer)
            {
                transform.position = new Vector3((OwnerClientId - 1.5f) * 1.5f, 1f, 0f);
            }
        }

        private void ApplyPlayerColor()
        {
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", PlayerColors[OwnerClientId % (ulong)PlayerColors.Length]);
            renderer.SetPropertyBlock(block);
        }
    }
}
