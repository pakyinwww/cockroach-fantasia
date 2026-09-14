using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(OwnerNetworkTransform))]
    public sealed class MovementSanityMonitor : NetworkBehaviour
    {
        private static readonly Bounds KitchenBounds = new Bounds(new Vector3(0f, 1.05f, 0f),
            new Vector3(17f, 2.7f, 13f));

        [SerializeField, Min(0.1f)] private float roleBaseSpeed = 3.2f;
        private NetworkVariable<int> correctionCount;
        private MovementSanityValidator validator;
        private OwnerNetworkTransform networkTransform;

        public int CorrectionCount => correctionCount.Value;

        public void Configure(float baseSpeed)
        {
            roleBaseSpeed = baseSpeed;
        }

        private void Awake()
        {
            correctionCount ??= new NetworkVariable<int>();
            networkTransform = GetComponent<OwnerNetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                validator = new MovementSanityValidator(KitchenBounds, roleBaseSpeed);
                validator.Reset(transform.position);
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || validator == null) return;
            var result = validator.Evaluate(transform.position, Time.fixedDeltaTime);
            if (!result.RequiresCorrection) return;

            correctionCount.Value++;
            var rotation = transform.rotation;
            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = result.Correction;
            if (controller != null) controller.enabled = true;
            networkTransform.SetState(result.Correction, rotation, transform.localScale, false);
            Debug.LogWarning($"Corrected {OwnerClientId} movement violation: {result.Violation}.");
        }
    }
}
