using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Food
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FoodItem : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;

        [SerializeField] private FoodDefinition definition;
        private readonly NetworkVariable<FoodSize> size = new();
        private readonly NetworkVariable<FoodLifecycleState> lifecycle = new();
        private readonly NetworkVariable<ulong> carrierClientId = new(NoCarrier);

        public FoodDefinition Definition => definition;
        public FoodSize Size => size.Value;
        public FoodLifecycleState Lifecycle => lifecycle.Value;
        public ulong CarrierClientId => carrierClientId.Value;

        public void Configure(FoodDefinition foodDefinition)
        {
            definition = foodDefinition;
        }

        public override void OnNetworkSpawn()
        {
            lifecycle.OnValueChanged += OnLifecycleChanged;
            ApplyWorldPresentation(lifecycle.Value == FoodLifecycleState.World);
            if (!IsServer) return;
            if (definition == null)
            {
                Debug.LogError("Spawned food has no definition.");
                NetworkObject.Despawn(true);
                return;
            }

            size.Value = definition.Size;
            lifecycle.Value = FoodLifecycleState.World;
            carrierClientId.Value = NoCarrier;
        }

        public override void OnNetworkDespawn()
        {
            lifecycle.OnValueChanged -= OnLifecycleChanged;
        }

        public bool TryClaimByServer(ulong clientId)
        {
            if (!IsServer) return false;
            var nextLifecycle = lifecycle.Value;
            var nextCarrier = carrierClientId.Value;
            if (!FoodLifecycleRules.TryClaim(ref nextLifecycle, ref nextCarrier, clientId)) return false;
            carrierClientId.Value = nextCarrier;
            lifecycle.Value = nextLifecycle;
            return true;
        }

        public bool TryDropByServer(ulong clientId, Vector3 floorPosition)
        {
            if (!IsServer) return false;
            var nextLifecycle = lifecycle.Value;
            var nextCarrier = carrierClientId.Value;
            if (!FoodLifecycleRules.TryDrop(ref nextLifecycle, ref nextCarrier, clientId)) return false;
            transform.position = floorPosition;
            carrierClientId.Value = nextCarrier;
            lifecycle.Value = nextLifecycle;
            return true;
        }

        public bool TryDepositByServer(ulong clientId)
        {
            if (!IsServer) return false;
            var nextLifecycle = lifecycle.Value;
            var nextCarrier = carrierClientId.Value;
            if (!FoodLifecycleRules.TryDeposit(ref nextLifecycle, ref nextCarrier, clientId)) return false;
            carrierClientId.Value = nextCarrier;
            lifecycle.Value = nextLifecycle;
            return true;
        }

        private void OnLifecycleChanged(FoodLifecycleState previous, FoodLifecycleState current)
        {
            ApplyWorldPresentation(current == FoodLifecycleState.World);
        }

        private void ApplyWorldPresentation(bool visible)
        {
            foreach (var itemRenderer in GetComponentsInChildren<Renderer>(true))
                itemRenderer.enabled = visible;
            foreach (var itemCollider in GetComponentsInChildren<Collider>(true))
                itemCollider.enabled = visible;
        }
    }
}
