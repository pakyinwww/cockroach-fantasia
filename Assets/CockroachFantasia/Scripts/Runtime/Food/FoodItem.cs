using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Food
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FoodItem : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;

        [SerializeField] private FoodDefinition definition;
        private NetworkVariable<FoodSize> size;
        private NetworkVariable<FoodLifecycleState> lifecycle;
        private NetworkVariable<ulong> carrierClientId;

        public FoodDefinition Definition => definition;
        public FoodSize Size => size.Value;
        public FoodLifecycleState Lifecycle => lifecycle.Value;
        public ulong CarrierClientId => carrierClientId.Value;

        public void Configure(FoodDefinition foodDefinition)
        {
            definition = foodDefinition;
        }

        public void InitializeBeforeSpawn()
        {
            if (NetworkObject.IsSpawned || definition == null) return;
            size.Value = definition.Size;
            lifecycle.Value = FoodLifecycleState.World;
            carrierClientId.Value = NoCarrier;
        }

        private void Awake()
        {
            size ??= new NetworkVariable<FoodSize>();
            lifecycle ??= new NetworkVariable<FoodLifecycleState>();
            carrierClientId ??= new NetworkVariable<ulong>(NoCarrier);
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

            InitializeBeforeSpawn();
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
