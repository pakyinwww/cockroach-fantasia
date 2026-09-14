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
            if (!IsServer) return;
            if (definition == null)
            {
                Debug.LogError("Spawned food has no definition.");
                NetworkObject.Despawn(true);
                return;
            }

            InitializeBeforeSpawn();
        }
    }
}
