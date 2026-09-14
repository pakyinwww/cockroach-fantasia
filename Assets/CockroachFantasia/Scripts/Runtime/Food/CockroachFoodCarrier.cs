using System;
using System.Collections;
using System.Linq;
using CockroachFantasia.Characters;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CockroachFantasia.Food
{
    [RequireComponent(typeof(NetworkObject), typeof(CockroachMotor), typeof(NetworkRoleAvatar))]
    public sealed class CockroachFoodCarrier : NetworkBehaviour
    {
        public const ulong NoFood = ulong.MaxValue;

        [SerializeField, Min(0.1f)] private float pickupRadius = 0.75f;
        [SerializeField, Min(0.1f)] private float dropRayDistance = 3f;

        private NetworkVariable<ulong> carriedFoodNetworkId;
        private CockroachMotor motor;
        private GameObject carriedVisual;
        private ulong renderedFoodId = NoFood;

        public ulong CarriedFoodNetworkId => carriedFoodNetworkId.Value;
        public bool IsCarrying => carriedFoodNetworkId.Value != NoFood;
        public bool HasCarriedVisual => carriedVisual != null;
        public FoodSize? CarriedVisualSize { get; private set; }

        private void Awake()
        {
            motor = GetComponent<CockroachMotor>();
            carriedFoodNetworkId ??= new NetworkVariable<ulong>(NoFood);
        }

        public override void OnNetworkSpawn()
        {
            carriedFoodNetworkId.OnValueChanged += OnCarriedFoodChanged;
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            RefreshCarriedPresentation();
        }

        public override void OnNetworkDespawn()
        {
            carriedFoodNetworkId.OnValueChanged -= OnCarriedFoodChanged;
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (IsServer) ReleaseCarriedFoodByServer();
            ClearCarriedPresentation();
        }

        private void Update()
        {
            if (IsSpawned && IsOwner && motor.CanAcceptInput && Keyboard.current?.eKey.wasPressedThisFrame == true)
                RequestInteract();
            if (IsSpawned && renderedFoodId != carriedFoodNetworkId.Value)
                RefreshCarriedPresentation();
        }

        public void RequestInteract()
        {
            if (!IsSpawned || !IsOwner || !motor.CanAcceptInput) return;
            if (IsCarrying)
            {
                RequestDropRpc();
                return;
            }

            var target = FindBestNearbyFood();
            if (target != null) RequestPickupRpc(new NetworkObjectReference(target.NetworkObject));
        }

        public void RequestPickupForDiagnostics(FoodItem target)
        {
            if (IsSpawned && IsOwner && target != null && Debug.isDebugBuild)
                PreparePickupForDiagnosticsRpc(new NetworkObjectReference(target.NetworkObject));
        }

        public void RequestDropForDiagnostics()
        {
            if (IsSpawned && IsOwner) RequestDropRpc();
        }

        public bool PreparePickupForRespawnDiagnosticsByServer(FoodItem target)
        {
            if (!IsServer || !Debug.isDebugBuild ||
                !Environment.GetCommandLineArgs().Contains("-respawnSmoke") || target == null) return false;
            motor.RecoverTo(target.transform.position + Vector3.right * 0.25f);
            TryPickupByServer(new NetworkObjectReference(target.NetworkObject), OwnerClientId);
            return carriedFoodNetworkId.Value == target.NetworkObjectId;
        }

        [Rpc(SendTo.Server)]
        private void RequestPickupRpc(NetworkObjectReference targetReference, RpcParams rpcParams = default)
        {
            TryPickupByServer(targetReference, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        private void PreparePickupForDiagnosticsRpc(NetworkObjectReference targetReference,
            RpcParams rpcParams = default)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild ||
                (!arguments.Contains("-foodCarrySmoke") && !arguments.Contains("-respawnSmoke")) ||
                !targetReference.TryGet(out var targetObject))
                return;
            motor.RecoverTo(targetObject.transform.position + Vector3.right * 0.25f);
            TryPickupByServer(targetReference, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        private void RequestDropRpc(RpcParams rpcParams = default)
        {
            if (!CanProcessRequest(rpcParams.Receive.SenderClientId)) return;
            ReleaseCarriedFoodByServer();
        }

        public bool ReleaseCarriedFoodByServer()
        {
            if (!IsServer || !TryGetCarriedFood(out var food)) return false;
            var dropped = food.TryDropByServer(OwnerClientId, ResolveFloorPosition());
            if (dropped) carriedFoodNetworkId.Value = NoFood;
            return dropped;
        }

        public bool TryDepositByServer()
        {
            var game = NetworkGameManager.Instance;
            if (!IsServer || game == null || !game.AcceptsGameplayRequests ||
                !TryGetCarriedFood(out var food) || food.Definition == null) return false;
            var points = food.Definition.Points;
            if (!food.TryDepositByServer(OwnerClientId)) return false;
            carriedFoodNetworkId.Value = NoFood;
            if (!game.TryDepositPointsByServer(points))
                throw new InvalidOperationException("A validated food deposit was rejected by match state.");
            food.NetworkObject.Despawn(true);
            return true;
        }

        private bool CanProcessRequest(ulong senderClientId)
        {
            if (!IsServer || senderClientId != OwnerClientId ||
                NetworkGameManager.Instance == null || !NetworkGameManager.Instance.AcceptsGameplayRequests)
                return false;
            var identity = GetComponent<NetworkRoleAvatar>();
            return identity != null && identity.Seat is LobbySeat.CockroachOne or LobbySeat.CockroachTwo or
                LobbySeat.CockroachThree;
        }

        private void TryPickupByServer(NetworkObjectReference targetReference, ulong senderClientId)
        {
            if (!CanProcessRequest(senderClientId) || IsCarrying ||
                !targetReference.TryGet(out var targetObject))
                return;
            var food = targetObject.GetComponent<FoodItem>();
            if (food == null || food.Lifecycle != FoodLifecycleState.World ||
                Vector3.Distance(transform.position, food.transform.position) > pickupRadius)
                return;

            // RPCs are processed serially on the server. TryClaimByServer changes the
            // lifecycle immediately, so simultaneous requests yield one winner.
            if (!food.TryClaimByServer(OwnerClientId)) return;
            carriedFoodNetworkId.Value = targetObject.NetworkObjectId;
            if (Debug.isDebugBuild && Environment.GetCommandLineArgs().Contains("-foodDepositSmoke"))
                StartCoroutine(DepositAfterDiagnosticDelay());
        }

        private IEnumerator DepositAfterDiagnosticDelay()
        {
            yield return new WaitForSeconds(0.5f);
            UnityEngine.Object.FindFirstObjectByType<NestZone>()?.TryDepositCarrierByServer(this);
        }

        private FoodItem FindBestNearbyFood()
        {
            return UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                .Where(food => food.Lifecycle == FoodLifecycleState.World &&
                               Vector3.Distance(transform.position, food.transform.position) <= pickupRadius)
                .OrderBy(food => Vector3.SqrMagnitude(food.transform.position - transform.position))
                .ThenBy(food => food.NetworkObjectId)
                .FirstOrDefault();
        }

        private bool TryGetCarriedFood(out FoodItem food)
        {
            food = null;
            return carriedFoodNetworkId.Value != NoFood && NetworkManager != null &&
                   NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(carriedFoodNetworkId.Value,
                       out var networkObject) && (food = networkObject.GetComponent<FoodItem>()) != null;
        }

        private Vector3 ResolveFloorPosition()
        {
            var origin = transform.position + transform.forward * 0.35f + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, dropRayDistance,
                    ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.04f;
            return transform.position + transform.forward * 0.35f;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == OwnerClientId) ReleaseCarriedFoodByServer();
        }

        private void OnCarriedFoodChanged(ulong previous, ulong current)
        {
            RefreshCarriedPresentation();
        }

        private void RefreshCarriedPresentation()
        {
            ClearCarriedPresentation();
            renderedFoodId = carriedFoodNetworkId.Value;
            if (!TryGetCarriedFood(out var food) || food.Definition == null || motor.CarrySocket == null)
            {
                motor.SetCarrySpeedMultiplier(1f);
                return;
            }

            var source = food.Definition.NetworkPrefab;
            var sourceFilter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
            var sourceRenderer = source != null ? source.GetComponentInChildren<MeshRenderer>() : null;
            if (sourceFilter != null && sourceRenderer != null)
            {
                carriedVisual = new GameObject("CarriedFoodVisual", typeof(MeshFilter), typeof(MeshRenderer));
                carriedVisual.transform.SetParent(motor.CarrySocket, false);
                carriedVisual.transform.localScale = source.transform.localScale;
                carriedVisual.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                carriedVisual.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                CarriedVisualSize = food.Size;
            }
            motor.SetCarrySpeedMultiplier(food.Definition.CarrySpeedMultiplier);
        }

        private void ClearCarriedPresentation()
        {
            if (carriedVisual != null) Destroy(carriedVisual);
            carriedVisual = null;
            CarriedVisualSize = null;
            if (motor != null) motor.SetCarrySpeedMultiplier(1f);
        }
    }
}
