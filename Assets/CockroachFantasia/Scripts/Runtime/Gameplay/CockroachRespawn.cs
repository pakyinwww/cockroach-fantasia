using System;
using System.Linq;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Networking;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Gameplay
{
    [RequireComponent(typeof(NetworkObject), typeof(CockroachMotor), typeof(CockroachFoodCarrier))]
    public sealed class CockroachRespawn : NetworkBehaviour
    {
        private const float OccupiedSpawnRadius = 0.4f;

        private NetworkVariable<bool> respawning;
        private NetworkVariable<double> respawnEndTimestamp;
        private CockroachMotor motor;
        private CockroachFoodCarrier carrier;
        private CharacterController controller;
        private Transform body;
        private Vector3 bodyRestScale;
        private GameObject puff;

        public bool IsRespawning => respawning.Value;
        public double RespawnEndTimestamp => respawnEndTimestamp.Value;
        public double RemainingRespawnSeconds => IsRespawning && NetworkManager != null
            ? Math.Max(0d, RespawnEndTimestamp - NetworkManager.ServerTime.Time)
            : 0d;

        private void Awake()
        {
            respawning ??= new NetworkVariable<bool>();
            respawnEndTimestamp ??= new NetworkVariable<double>();
            motor = GetComponent<CockroachMotor>();
            carrier = GetComponent<CockroachFoodCarrier>();
            controller = GetComponent<CharacterController>();
            body = transform.Find("GreyboxBody");
            if (body != null) bodyRestScale = body.localScale;
        }

        public override void OnNetworkSpawn()
        {
            respawning.OnValueChanged += OnRespawningChanged;
            ApplyPresentation(respawning.Value);
        }

        public override void OnNetworkDespawn()
        {
            respawning.OnValueChanged -= OnRespawningChanged;
            ClearPuff();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !CockroachRespawnRules.CanComplete(respawning.Value,
                    NetworkManager.ServerTime.Time, respawnEndTimestamp.Value)) return;
            CompleteRespawnByServer();
        }

        public bool ApplyConfirmedHitByServer()
        {
            var game = NetworkGameManager.Instance;
            if (!IsServer || game == null ||
                !CockroachRespawnRules.CanBegin(respawning.Value, game.Phase)) return false;
            carrier.ReleaseCarriedFoodByServer();
            respawnEndTimestamp.Value = NetworkManager.ServerTime.Time + game.Rules.RespawnDelaySeconds;
            respawning.Value = true;
            return true;
        }

        private void CompleteRespawnByServer()
        {
            var marker = SelectFreeNestSpawn();
            if (marker == null)
            {
                Debug.LogWarning("No Cockroach nest respawn marker is available; retrying next frame.");
                return;
            }

            motor.RecoverTo(marker.transform.position);
            respawnEndTimestamp.Value = 0d;
            respawning.Value = false;
        }

        private KitchenSpawnMarker SelectFreeNestSpawn()
        {
            var ownSeat = GetComponent<NetworkRoleAvatar>().Seat;
            var markers = UnityEngine.Object.FindObjectsByType<KitchenSpawnMarker>(FindObjectsSortMode.None)
                .Where(marker => marker.Seat is LobbySeat.CockroachOne or LobbySeat.CockroachTwo or
                    LobbySeat.CockroachThree)
                .OrderBy(marker => marker.Seat == ownSeat ? 0 : 1)
                .ThenBy(marker => marker.Seat)
                .ToArray();
            var activeOthers = UnityEngine.Object.FindObjectsByType<CockroachRespawn>(FindObjectsSortMode.None)
                .Where(other => other != this && !other.IsRespawning)
                .Select(other => other.transform.position)
                .ToArray();
            return markers.FirstOrDefault(marker => activeOthers.All(position =>
                Vector3.Distance(position, marker.transform.position) >= OccupiedSpawnRadius));
        }

        private void OnRespawningChanged(bool previous, bool current)
        {
            ApplyPresentation(current);
        }

        private void ApplyPresentation(bool knockedOut)
        {
            var playing = NetworkGameManager.Instance != null && NetworkGameManager.Instance.AcceptsGameplayRequests;
            motor.SetControlState(playing, knockedOut);
            if (controller != null) controller.enabled = !knockedOut;
            if (body != null)
                body.localScale = knockedOut
                    ? new Vector3(bodyRestScale.x * 1.35f, bodyRestScale.y * 0.22f, bodyRestScale.z * 1.35f)
                    : bodyRestScale;
            if (knockedOut) CreatePuff();
            else ClearPuff();
        }

        private void CreatePuff()
        {
            ClearPuff();
            puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "CartoonKnockoutPuff";
            puff.transform.SetParent(transform, false);
            puff.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            puff.transform.localScale = new Vector3(0.42f, 0.14f, 0.42f);
            Destroy(puff.GetComponent<Collider>());
        }

        private void ClearPuff()
        {
            if (puff != null) Destroy(puff);
            puff = null;
        }
    }
}
