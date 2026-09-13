using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoster : NetworkBehaviour
    {
        public const int MaximumDisplayNameCharacters = 16;
        private static NetworkRoster instance;

        private NetworkList<RosterEntry> entries;
        private NetworkVariable<bool> locked;

        public static NetworkRoster Instance => instance;
        public IReadOnlyList<RosterEntry> Entries => CreateSnapshot();
        public bool IsLocked => locked.Value;

        public event Action Changed;
        public event Action<bool, string> LocalSeatRequestResolved;

        private void Awake()
        {
            // NetworkList is not serialized by Unity. Constructing it in Awake also
            // covers prefab instances created through Unity's native object path.
            entries ??= new NetworkList<RosterEntry>();
            locked ??= new NetworkVariable<bool>();
        }

        public override void OnNetworkSpawn()
        {
            instance = this;
            entries.OnListChanged += OnEntriesChanged;
            locked.OnValueChanged += OnLockedChanged;

            if (!IsServer)
            {
                Changed?.Invoke();
                return;
            }

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                AddPlayerIfMissing(clientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            entries.OnListChanged -= OnEntriesChanged;
            locked.OnValueChanged -= OnLockedChanged;
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        public void RequestSeat(LobbySeat seat)
        {
            RequestSeatRpc(seat);
        }

        public void SetLocalDisplayName(string displayName)
        {
            SetDisplayNameRpc(new FixedString32Bytes(SanitizeDisplayName(displayName)));
        }

        public bool TryGetEntry(ulong clientId, out RosterEntry entry)
        {
            var index = FindIndex(clientId);
            if (index >= 0)
            {
                entry = entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public void SetLockedByServer(bool value)
        {
            if (IsServer)
            {
                locked.Value = value;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSeatRpc(LobbySeat seat, RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var index = FindIndex(clientId);
            if (index < 0)
            {
                ResolveSeatRequestRpc(false, new FixedString64Bytes("You are no longer in the roster."),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            if (!RosterRules.CanClaimSeat(CreateSnapshot(), clientId, seat, locked.Value, out var rejection))
            {
                ResolveSeatRequestRpc(false, new FixedString64Bytes(rejection),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            var entry = entries[index];
            if (entry.Seat == seat)
            {
                ResolveSeatRequestRpc(true, new FixedString64Bytes($"Still playing {GetSeatLabel(seat)}."),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            entry.Seat = seat;
            entry.Ready = false;
            entries[index] = entry;
            ResolveSeatRequestRpc(true, new FixedString64Bytes($"Claimed {GetSeatLabel(seat)}."),
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetDisplayNameRpc(FixedString32Bytes requestedName, RpcParams rpcParams = default)
        {
            var index = FindIndex(rpcParams.Receive.SenderClientId);
            if (index < 0)
            {
                return;
            }

            var entry = entries[index];
            entry.DisplayName = new FixedString32Bytes(SanitizeDisplayName(requestedName.ToString()));
            entries[index] = entry;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ResolveSeatRequestRpc(bool accepted, FixedString64Bytes message, RpcParams rpcParams = default)
        {
            LocalSeatRequestResolved?.Invoke(accepted, message.ToString());
        }

        private void OnClientConnected(ulong clientId)
        {
            AddPlayerIfMissing(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var index = FindIndex(clientId);
            if (index >= 0)
            {
                entries.RemoveAt(index);
            }
        }

        private void AddPlayerIfMissing(ulong clientId)
        {
            if (FindIndex(clientId) >= 0)
            {
                return;
            }

            entries.Add(new RosterEntry
            {
                ClientId = clientId,
                DisplayName = new FixedString32Bytes($"Player {clientId + 1}"),
                Seat = LobbySeat.None,
                Ready = false,
                Connected = true
            });
        }

        private int FindIndex(ulong clientId)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].ClientId == clientId)
                {
                    return index;
                }
            }

            return -1;
        }

        private List<RosterEntry> CreateSnapshot()
        {
            var snapshot = new List<RosterEntry>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                snapshot.Add(entries[index]);
            }

            return snapshot;
        }

        private void OnEntriesChanged(NetworkListEvent<RosterEntry> changeEvent)
        {
            Changed?.Invoke();
        }

        private void OnLockedChanged(bool previous, bool current)
        {
            Changed?.Invoke();
        }

        private static string SanitizeDisplayName(string displayName)
        {
            var sanitized = new string((displayName ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is ' ' or '_' or '-')
                .Take(MaximumDisplayNameCharacters)
                .ToArray()).Trim();
            return sanitized.Length == 0 ? "Player" : sanitized;
        }

        private static string GetSeatLabel(LobbySeat seat)
        {
            return seat switch
            {
                LobbySeat.Human => "Human",
                LobbySeat.CockroachOne => "Cockroach 1",
                LobbySeat.CockroachTwo => "Cockroach 2",
                LobbySeat.CockroachThree => "Cockroach 3",
                _ => "seat"
            };
        }
    }
}
