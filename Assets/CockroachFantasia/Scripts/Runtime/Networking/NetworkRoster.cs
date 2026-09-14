using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoster : NetworkBehaviour
    {
        public const int MaximumDisplayNameCharacters = 16;
        private static NetworkRoster instance;

        private NetworkList<RosterEntry> entries;
        private NetworkVariable<bool> locked;
        private NetworkVariable<bool> loading;
        private bool sceneLoadRequested;
        private int startAttempt;

        public static NetworkRoster Instance => instance;
        public IReadOnlyList<RosterEntry> Entries => CreateSnapshot();
        public bool IsLocked => locked.Value;
        public bool IsLoading => loading.Value;
        public bool CanLocalHostStart => NetworkManager != null && NetworkManager.IsHost &&
                                         RosterRules.CanStartMatch(CreateSnapshot(), locked.Value, out _);

        public event Action Changed;
        public event Action<bool, string> LocalSeatRequestResolved;
        public event Action<bool, string> LocalLobbyActionResolved;

        private void Awake()
        {
            // NetworkList is not serialized by Unity. Constructing it in Awake also
            // covers prefab instances created through Unity's native object path.
            entries ??= new NetworkList<RosterEntry>();
            locked ??= new NetworkVariable<bool>();
            loading ??= new NetworkVariable<bool>();
        }

        public override void OnNetworkSpawn()
        {
            instance = this;
            entries.OnListChanged += OnEntriesChanged;
            locked.OnValueChanged += OnLockedChanged;
            loading.OnValueChanged += OnLoadingChanged;

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
            loading.OnValueChanged -= OnLoadingChanged;
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

        public void SetLocalReady(bool ready)
        {
            SetReadyRpc(ready);
        }

        public void RequestStartMatch()
        {
            RequestStartMatchRpc();
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var index = FindIndex(clientId);
            if (index < 0 || locked.Value || loading.Value || !RosterRules.IsSelectableSeat(entries[index].Seat))
            {
                ResolveLobbyActionRpc(false, new FixedString64Bytes(
                    loading.Value ? "The match is loading." : "Choose a role before getting ready."),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            var entry = entries[index];
            entry.Ready = ready;
            entries[index] = entry;
            ResolveLobbyActionRpc(true, new FixedString64Bytes(ready ? "Ready!" : "Not ready."),
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestStartMatchRpc(RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (clientId != NetworkManager.ServerClientId)
            {
                ResolveLobbyActionRpc(false, new FixedString64Bytes("Only the host can start the match."),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            if (!RosterRules.CanStartMatch(CreateSnapshot(), locked.Value, out var rejection))
            {
                ResolveLobbyActionRpc(false, new FixedString64Bytes(rejection),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            BeginMatchStart(clientId);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ResolveSeatRequestRpc(bool accepted, FixedString64Bytes message, RpcParams rpcParams = default)
        {
            LocalSeatRequestResolved?.Invoke(accepted, message.ToString());
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ResolveLobbyActionRpc(bool accepted, FixedString64Bytes message, RpcParams rpcParams = default)
        {
            LocalLobbyActionResolved?.Invoke(accepted, message.ToString());
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

            if (loading.Value && !sceneLoadRequested)
            {
                AbortMatchStart("A player disconnected. The lobby was reopened.");
            }
        }

        private async void BeginMatchStart(ulong requestingClientId)
        {
            var attempt = ++startAttempt;
            locked.Value = true;
            loading.Value = true;

            var coordinator = SessionCoordinator.Instance;
            var diagnosticHold = GetDiagnosticStartHoldMilliseconds();
            if (diagnosticHold > 0)
            {
                await Task.Delay(diagnosticHold);
                if (attempt != startAttempt || !loading.Value) return;
            }

            if (coordinator == null || !await coordinator.SetSessionLockedAsync(true))
            {
                if (attempt == startAttempt)
                    AbortMatchStart("The room could not be locked. Please try again.", requestingClientId);
                return;
            }

            if (attempt != startAttempt || !loading.Value)
            {
                await coordinator.SetSessionLockedAsync(false);
                return;
            }

            if (!RosterRules.HasValidRoleDistribution(CreateSnapshot()) ||
                CreateSnapshot().Any(entry => entry.Connected && !entry.Ready))
            {
                AbortMatchStart("The roster changed. Everyone must ready up again.", requestingClientId);
                return;
            }

            sceneLoadRequested = true;
            var status = NetworkManager.SceneManager.LoadScene("Kitchen", LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                sceneLoadRequested = false;
                AbortMatchStart("Kitchen could not be loaded. Please try again.", requestingClientId);
                return;
            }

            ResolveLobbyActionRpc(true, new FixedString64Bytes("Loading Kitchen…"),
                RpcTarget.Single(requestingClientId, RpcTargetUse.Temp));
        }

        private async void AbortMatchStart(string message, ulong requestingClientId = ulong.MaxValue)
        {
            startAttempt++;
            sceneLoadRequested = false;
            loading.Value = false;
            locked.Value = false;
            if (SessionCoordinator.Instance != null)
                await SessionCoordinator.Instance.SetSessionLockedAsync(false);
            if (requestingClientId != ulong.MaxValue && NetworkManager != null &&
                NetworkManager.ConnectedClientsIds.Contains(requestingClientId))
            {
                ResolveLobbyActionRpc(false, new FixedString64Bytes(message),
                    RpcTarget.Single(requestingClientId, RpcTargetUse.Temp));
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

        private void OnLoadingChanged(bool previous, bool current)
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

        private static int GetDiagnosticStartHoldMilliseconds()
        {
            if (!Debug.isDebugBuild) return 0;
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], "-rosterDiagnosticStartHoldMs", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(arguments[index + 1], out var milliseconds))
                    return Math.Clamp(milliseconds, 0, 10000);
            }

            return 0;
        }
    }
}
